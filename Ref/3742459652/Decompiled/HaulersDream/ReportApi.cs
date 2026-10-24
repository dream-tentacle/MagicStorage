using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HaulersDream.Core;
using RimWorld;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using Verse.Steam;

namespace HaulersDream;

internal static class ReportApi
{
	public const string Endpoint = "https://reports.refzlund.com/report/XfgH9t8FHTV39guTPe1V86xg";

	public const int MaxCommentChars = 4000;

	public const int MaxAttachments = 12;

	public const long MaxAttachmentBytes = 104857600L;

	private const int LogTailBytes = 5242880;

	public static string UserAgent()
	{
		return "HaulersDream/" + ModVersion() + " (RimWorld " + RwVersion() + ")";
	}

	public static UnityWebRequest NewRequest(string url, string method)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		UnityWebRequest val = new UnityWebRequest(url, method)
		{
			certificateHandler = (CertificateHandler)(object)new ReportCertificateHandler()
		};
		val.SetRequestHeader("User-Agent", UserAgent());
		return val;
	}

	public static string ReporterId()
	{
		if (HaulersDreamMod.Settings == null)
		{
			return "";
		}
		return HaulersDreamMod.Settings.ReporterId;
	}

	public static string ModVersionString()
	{
		return ModVersion();
	}

	public static string AttachmentUrl(string reportId, string fileName)
	{
		return "https://reports.refzlund.com/report/XfgH9t8FHTV39guTPe1V86xg/" + reportId + "/attachment?name=" + UnityWebRequest.EscapeURL(fileName ?? "attachment");
	}

	public static string MyReportsUrl()
	{
		return "https://reports.refzlund.com/report/XfgH9t8FHTV39guTPe1V86xg/mine";
	}

	public static string ThreadUrl(string reportId)
	{
		return "https://reports.refzlund.com/report/XfgH9t8FHTV39guTPe1V86xg/" + reportId + "/thread";
	}

	public static string CommentUrl(string reportId)
	{
		return "https://reports.refzlund.com/report/XfgH9t8FHTV39guTPe1V86xg/" + reportId + "/comment";
	}

	public static string StatusUrl()
	{
		return "https://reports.refzlund.com/report/XfgH9t8FHTV39guTPe1V86xg/status";
	}

	public static string BuildCommentJson(string body)
	{
		return "{\"body\":" + JsonStr(body ?? string.Empty) + "}";
	}

	public static string ContentTypeFor(string path)
	{
		string text = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
		if (text != null)
		{
			int length = text.Length;
			if (length != 4)
			{
				if (length == 5)
				{
					char c = text[4];
					if (c != 'g')
					{
						switch (c)
						{
						case 'p':
							if (!(text == ".webp"))
							{
								break;
							}
							return "image/webp";
						case 'm':
							if (!(text == ".webm"))
							{
								break;
							}
							return "video/webm";
						}
					}
					else if (text == ".jpeg")
					{
						goto IL_0130;
					}
				}
			}
			else
			{
				switch (text[2])
				{
				case 'p':
					break;
				case 'n':
					if (!(text == ".png"))
					{
						goto IL_0166;
					}
					return "image/png";
				case 'i':
					if (!(text == ".gif"))
					{
						goto IL_0166;
					}
					return "image/gif";
				case 'm':
					if (!(text == ".bmp"))
					{
						goto IL_0166;
					}
					return "image/bmp";
				case 'o':
					if (!(text == ".mov"))
					{
						goto IL_0166;
					}
					return "video/quicktime";
				case 'k':
					if (!(text == ".mkv"))
					{
						goto IL_0166;
					}
					return "video/x-matroska";
				default:
					goto IL_0166;
				}
				if (text == ".jpg")
				{
					goto IL_0130;
				}
				if (text == ".mp4")
				{
					return "video/mp4";
				}
			}
		}
		goto IL_0166;
		IL_0166:
		return null;
		IL_0130:
		return "image/jpeg";
	}

	public static string BuildJson(string typeId, string comment, bool includeGameLog)
	{
		StringBuilder stringBuilder = new StringBuilder(2048);
		stringBuilder.Append('{');
		stringBuilder.Append("\"comment\":").Append(JsonStr(comment ?? string.Empty));
		stringBuilder.Append(",\"reporterId\":").Append(JsonStr(ReporterId()));
		List<string> list = new List<string>(2);
		List<string> list2 = new List<string>(2);
		HDDebugLog.FlushBlocking();
		string reportLog = HDLog.GetReportLog();
		if (!string.IsNullOrEmpty(reportLog))
		{
			list.Add("Hauler's Dream");
			list2.Add(reportLog);
		}
		if (includeGameLog)
		{
			string text = ReadPlayerLogTail();
			if (!string.IsNullOrEmpty(text))
			{
				list.Add("Player.log");
				list2.Add(text);
			}
		}
		if (list.Count > 0)
		{
			stringBuilder.Append(",\"logs\":[");
			for (int i = 0; i < list.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append(',');
				}
				stringBuilder.Append("{\"name\":").Append(JsonStr(list[i])).Append(",\"text\":")
					.Append(JsonStr(list2[i]))
					.Append('}');
			}
			stringBuilder.Append(']');
		}
		stringBuilder.Append(",\"meta\":{");
		stringBuilder.Append("\"type\":").Append(JsonStr(typeId));
		stringBuilder.Append(",\"modVersion\":").Append(JsonStr(ModVersion()));
		stringBuilder.Append(",\"rwVersion\":").Append(JsonStr(RwVersion()));
		stringBuilder.Append(",\"os\":").Append(JsonStr(SystemInfo.operatingSystem ?? string.Empty));
		SteamIdentity(out var steamId, out var personaName);
		if (!string.IsNullOrEmpty(steamId))
		{
			stringBuilder.Append(",\"steamId\":").Append(JsonStr(steamId));
		}
		if (!string.IsNullOrEmpty(personaName))
		{
			stringBuilder.Append(",\"steamName\":").Append(JsonStr(personaName));
		}
		List<ModContentPack> runningModsListForReading = LoadedModManager.RunningModsListForReading;
		stringBuilder.Append(",\"activeModCount\":").Append(runningModsListForReading.Count.ToString(CultureInfo.InvariantCulture));
		stringBuilder.Append(",\"mods\":[");
		for (int j = 0; j < runningModsListForReading.Count; j++)
		{
			if (j > 0)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.Append("{\"name\":").Append(JsonStr(runningModsListForReading[j].Name ?? string.Empty)).Append(",\"packageId\":")
				.Append(JsonStr(runningModsListForReading[j].PackageIdPlayerFacing ?? string.Empty))
				.Append('}');
		}
		stringBuilder.Append(']');
		stringBuilder.Append('}');
		stringBuilder.Append('}');
		return stringBuilder.ToString();
	}

	private static void SteamIdentity(out string steamId, out string personaName)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		steamId = null;
		personaName = null;
		if (SteamManager.Initialized)
		{
			CSteamID steamID = SteamUser.GetSteamID();
			if (((CSteamID)(ref steamID)).IsValid())
			{
				steamId = steamID.m_SteamID.ToString(CultureInfo.InvariantCulture);
				personaName = SteamFriends.GetPersonaName();
			}
		}
	}

	private static string RwVersion()
	{
		string currentVersionStringWithRev = VersionControl.CurrentVersionStringWithRev;
		if (!string.IsNullOrEmpty(currentVersionStringWithRev))
		{
			return currentVersionStringWithRev;
		}
		return VersionControl.CurrentVersionString;
	}

	private static string ModVersion()
	{
		Version version = typeof(HaulersDreamMod).Assembly.GetName().Version;
		if (!(version != null))
		{
			return "unknown";
		}
		return version.ToString();
	}

	private static string ReadPlayerLogTail()
	{
		string consoleLogPath = Application.consoleLogPath;
		if (string.IsNullOrEmpty(consoleLogPath) || !File.Exists(consoleLogPath))
		{
			return null;
		}
		using FileStream fileStream = new FileStream(consoleLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		long num = Math.Max(0L, fileStream.Length - 5242880);
		if (num > 0)
		{
			fileStream.Seek(num, SeekOrigin.Begin);
		}
		using StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8);
		string text = streamReader.ReadToEnd();
		if (num > 0)
		{
			int num2 = text.IndexOf('\n');
			if (num2 >= 0 && num2 < text.Length - 1)
			{
				text = text.Substring(num2 + 1);
			}
			text = "[... truncated to the last " + 5120 + " KB ...]\n" + text;
		}
		return text;
	}

	private static string JsonStr(string s)
	{
		if (s == null)
		{
			return "\"\"";
		}
		StringBuilder stringBuilder = new StringBuilder(s.Length + 2);
		stringBuilder.Append('"');
		foreach (char c in s)
		{
			switch (c)
			{
			case '"':
				stringBuilder.Append("\\\"");
				continue;
			case '\\':
				stringBuilder.Append("\\\\");
				continue;
			case '\n':
				stringBuilder.Append("\\n");
				continue;
			case '\r':
				stringBuilder.Append("\\r");
				continue;
			case '\t':
				stringBuilder.Append("\\t");
				continue;
			case '\b':
				stringBuilder.Append("\\b");
				continue;
			case '\f':
				stringBuilder.Append("\\f");
				continue;
			}
			if (c < ' ')
			{
				StringBuilder stringBuilder2 = stringBuilder.Append("\\u");
				int num = c;
				stringBuilder2.Append(num.ToString("x4", CultureInfo.InvariantCulture));
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		stringBuilder.Append('"');
		return stringBuilder.ToString();
	}

	public static List<ReportSummary> ParseMyReports(string json)
	{
		List<ReportSummary> list = new List<ReportSummary>();
		if (!(MiniJson.Parse(json) is Dictionary<string, object> dictionary))
		{
			return list;
		}
		if (dictionary.TryGetValue("reports", out var value) && value is List<object> list2)
		{
			foreach (object item in list2)
			{
				if (item is Dictionary<string, object> o)
				{
					list.Add(new ReportSummary
					{
						id = Str(o, "id"),
						type = Str(o, "type"),
						title = Str(o, "title"),
						createdAt = Num(o, "createdAt"),
						status = Str(o, "status"),
						issueNumber = Num(o, "issueNumber"),
						url = Str(o, "url")
					});
				}
			}
		}
		return list;
	}

	public static ReportThread ParseThread(string json)
	{
		if (!(MiniJson.Parse(json) is Dictionary<string, object> dictionary))
		{
			return null;
		}
		ReportThread reportThread = new ReportThread
		{
			id = Str(dictionary, "id"),
			type = Str(dictionary, "type"),
			comment = Str(dictionary, "comment"),
			createdAt = Num(dictionary, "createdAt"),
			status = Str(dictionary, "status"),
			issueNumber = Num(dictionary, "issueNumber"),
			url = Str(dictionary, "url"),
			commentsUnavailable = Bool(dictionary, "commentsUnavailable")
		};
		if (dictionary.TryGetValue("comments", out var value) && value is List<object> list)
		{
			foreach (object item in list)
			{
				if (item is Dictionary<string, object> o)
				{
					reportThread.comments.Add(new IssueComment
					{
						author = Str(o, "author"),
						body = Str(o, "body"),
						createdAt = Str(o, "createdAt"),
						role = Str(o, "role")
					});
				}
			}
		}
		return reportThread;
	}

	public static StatusFeed ParseReportStatuses(string json)
	{
		StatusFeed statusFeed = new StatusFeed();
		if (!(MiniJson.Parse(json) is Dictionary<string, object> dictionary))
		{
			return statusFeed;
		}
		statusFeed.latestVersion = Str(dictionary, "latestVersion");
		statusFeed.now = (long)Num(dictionary, "now");
		if (dictionary.TryGetValue("reports", out var value) && value is List<object> list)
		{
			foreach (object item in list)
			{
				if (item is Dictionary<string, object> o)
				{
					statusFeed.reports.Add(new ReportStatus
					{
						ReportId = Str(o, "reportId"),
						Title = Str(o, "title"),
						Url = Str(o, "url"),
						Status = Str(o, "status"),
						StatusAt = (long)Num(o, "statusAt"),
						LastCommentAt = (long)Num(o, "lastCommentAt")
					});
				}
			}
		}
		return statusFeed;
	}

	private static string Str(Dictionary<string, object> o, string k)
	{
		if (o == null || !o.TryGetValue(k, out var value) || !(value is string result))
		{
			return null;
		}
		return result;
	}

	private static double Num(Dictionary<string, object> o, string k)
	{
		if (o != null && o.TryGetValue(k, out var value) && value is double)
		{
			return (double)value;
		}
		return 0.0;
	}

	private static bool Bool(Dictionary<string, object> o, string k)
	{
		bool flag = default(bool);
		int num;
		if (o != null && o.TryGetValue(k, out var value))
		{
			if (value is bool)
			{
				flag = (bool)value;
				num = 1;
			}
			else
			{
				num = 0;
			}
		}
		else
		{
			num = 0;
		}
		return (byte)((uint)num & (flag ? 1u : 0u)) != 0;
	}
}
