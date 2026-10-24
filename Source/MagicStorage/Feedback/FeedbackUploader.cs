using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace MagicStorage
{
    internal static class FeedbackUploader
    {
        private const string Endpoint = "https://mengjing-mod-feedback.vividheron129.chatgpt.site/api/game-feedback";
        private const int MaxLogCharacters = 250000;
        private const int MaxRequestBytes = 2 * 1024 * 1024;
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private static DateTime lastUploadUtc = DateTime.MinValue;

        internal sealed class ModInfo
        {
            internal string Name;
            internal string PackageId;
            internal string Version;
        }

        internal sealed class Capture
        {
            internal string SteamId;
            internal string GameVersion;
            internal string ModVersion;
            internal string Log;
            internal List<ModInfo> Mods;
        }

        internal sealed class Result
        {
            internal bool Success;
            internal string MessageKey;
            internal string Receipt = string.Empty;
        }

        internal static bool CoolingDown => DateTime.UtcNow - lastUploadUtc < TimeSpan.FromSeconds(60);

        // This method must run on the game thread. The upload task receives only copied data.
        internal static Capture Collect()
        {
            var result = new Capture
            {
                SteamId = CollectSteamId(),
                GameVersion = Cap(VersionControl.CurrentVersionString, 64),
                ModVersion = Cap(UpdateNoticeUtility.CurrentVersion ?? "unknown", 64),
                Mods = new List<ModInfo>()
            };
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                if (result.Mods.Count >= 500) break;
                result.Mods.Add(new ModInfo
                {
                    Name = Cap(mod.Name, 160),
                    PackageId = Cap(mod.PackageId, 160),
                    Version = Cap(mod.ModMetaData?.ModVersion ?? "unknown", 64)
                });
            }
            result.Log = CollectLog();
            return result;
        }

        private static string CollectSteamId()
        {
            try
            {
                if (!Verse.Steam.SteamManager.Initialized) return string.Empty;
                // Steamworks is supplied by the game; avoid an extra version-specific DLL reference.
                Type steamUser = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("Steamworks.SteamUser", false))
                    .FirstOrDefault(type => type != null);
                var method = steamUser?.GetMethod("GetSteamID", Type.EmptyTypes);
                string id = method?.Invoke(null, null)?.ToString() ?? string.Empty;
                return Regex.IsMatch(id, @"^[1-9][0-9]{16}$") ? id : string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string CollectLog()
        {
            try
            {
                LogMessage[] messages;
                using (Log.LockMessages()) messages = Log.Messages.ToArray();
                if (messages.Length > 0)
                {
                    var text = new StringBuilder(65536);
                    for (int i = Math.Max(0, messages.Length - 2000); i < messages.Length; i++)
                    {
                        LogMessage message = messages[i];
                        text.Append('[').Append(message.type).Append("] ").Append(Cap(message.ToString(), 65536)).AppendLine();
                        if (!string.IsNullOrEmpty(message.StackTrace) && message.StackTrace != "No stack trace.")
                            text.AppendLine(Cap(message.StackTrace, 65536));
                        if (text.Length > MaxLogCharacters * 2)
                            text.Remove(0, text.Length - MaxLogCharacters);
                    }
                    return Tail(text.ToString(), MaxLogCharacters);
                }
            }
            catch { }
            try
            {
                string path = UnityEngine.Application.consoleLogPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return string.Empty;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    bool truncated = stream.Length > 512000;
                    if (truncated) stream.Seek(-512000, SeekOrigin.End);
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        if (truncated) reader.ReadLine();
                        return Tail(reader.ReadToEnd(), MaxLogCharacters);
                    }
                }
            }
            catch { return string.Empty; }
        }

        internal static string ValidateInput(string title, string description)
        {
            if ((title ?? string.Empty).Trim().Length < 4) return "MS_FeedbackTitleTooShort";
            if ((description ?? string.Empty).Trim().Length < 10) return "MS_FeedbackDescriptionTooShort";
            return null;
        }

        internal static Task<Result> UploadAsync(string title, string description, Capture capture)
        {
            string validationError = ValidateInput(title, description);
            if (validationError != null) return Task.FromResult(new Result { MessageKey = validationError });
            if (CoolingDown) return Task.FromResult(new Result { MessageKey = "MS_FeedbackCooldown" });
            lastUploadUtc = DateTime.UtcNow;
            return Task.Run(async () =>
            {
                try
                {
                    string log = capture.Log;
                    byte[] bytes = Encoding.UTF8.GetBytes(BuildJson(title, description, capture, log));
                    while (bytes.Length > MaxRequestBytes && log.Length > 1000)
                    {
                        log = Tail(log, log.Length / 2);
                        bytes = Encoding.UTF8.GetBytes(BuildJson(title, description, capture, log));
                    }
                    if (bytes.Length > MaxRequestBytes) return new Result { MessageKey = "MS_FeedbackTooLarge" };
                    using (var content = new ByteArrayContent(bytes))
                    {
                        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                        using (HttpResponseMessage response = await Client.PostAsync(Endpoint, content).ConfigureAwait(false))
                        {
                            if ((int)response.StatusCode == 201)
                            {
                                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                                Match match = Regex.Match(body, @"""receipt""\s*:\s*""([A-Z0-9-]{1,40})""");
                                return new Result { Success = true, MessageKey = "MS_FeedbackSuccess", Receipt = match.Success ? match.Groups[1].Value : string.Empty };
                            }
                            int status = (int)response.StatusCode;
                            return new Result { MessageKey = status == 429 ? "MS_FeedbackRateLimited" : status == 413 ? "MS_FeedbackTooLarge" : "MS_FeedbackUploadFailed" };
                        }
                    }
                }
                catch (TaskCanceledException) { return new Result { MessageKey = "MS_FeedbackTimeout" }; }
                catch { return new Result { MessageKey = "MS_FeedbackUploadFailed" }; }
            });
        }

        private static string BuildJson(string title, string description, Capture capture, string log)
        {
            var json = new StringBuilder(log.Length + 4096);
            json.Append("{\"protocol\":1,\"modId\":\"magic-storage\",\"title\":");
            WriteString(json, title);
            json.Append(",\"description\":"); WriteString(json, description);
            json.Append(",\"steamId\":"); WriteString(json, capture.SteamId);
            json.Append(",\"gameVersion\":"); WriteString(json, capture.GameVersion);
            json.Append(",\"modVersion\":"); WriteString(json, capture.ModVersion);
            json.Append(",\"log\":"); WriteString(json, log);
            json.Append(",\"mods\":[");
            for (int i = 0; i < capture.Mods.Count; i++)
            {
                if (i > 0) json.Append(',');
                ModInfo mod = capture.Mods[i];
                json.Append("{\"name\":"); WriteString(json, mod.Name);
                json.Append(",\"packageId\":"); WriteString(json, mod.PackageId);
                json.Append(",\"version\":"); WriteString(json, mod.Version);
                json.Append('}');
            }
            return json.Append("]}").ToString();
        }

        private static void WriteString(StringBuilder json, string value)
        {
            json.Append('"');
            foreach (char c in value ?? string.Empty)
            {
                if (c == '"' || c == '\\') json.Append('\\').Append(c);
                else if (c < 32) json.Append("\\u").Append(((int)c).ToString("x4"));
                else json.Append(c);
            }
            json.Append('"');
        }

        private static string Cap(string value, int max) => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value.Substring(0, max);
        private static string Tail(string value, int max) => value.Length <= max ? value : "[Earlier log entries omitted]\n" + value.Substring(value.Length - max);
    }
}


