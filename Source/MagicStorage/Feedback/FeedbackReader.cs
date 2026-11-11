using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MagicStorage
{
    internal static class FeedbackReader
    {
        private const string Endpoint = "https://mengjing-mod-feedback.stirringblimp1.chatgpt.site/api/game-feedback/query";
        private const int MaxResponseBytes = 4 * 1024 * 1024;
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            return new HttpClient { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = MaxResponseBytes };
        }

        [DataContract]
        internal sealed class Item
        {
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "receipt")] public string Receipt { get; set; }
            [DataMember(Name = "title")] public string Title { get; set; }
            [DataMember(Name = "description")] public string Description { get; set; }
            [DataMember(Name = "steps")] public string Steps { get; set; }
            [DataMember(Name = "status")] public string Status { get; set; }
            [DataMember(Name = "gameVersion")] public string GameVersion { get; set; }
            [DataMember(Name = "modVersion")] public string ModVersion { get; set; }
            [DataMember(Name = "createdAt")] public string CreatedAt { get; set; }
            [DataMember(Name = "updatedAt")] public string UpdatedAt { get; set; }
            [DataMember(Name = "mergedReceipt")] public string MergedReceipt { get; set; }
        }

        [DataContract]
        internal sealed class Reply
        {
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "body")] public string Body { get; set; }
            [DataMember(Name = "authorName")] public string AuthorName { get; set; }
            [DataMember(Name = "isAdmin")] public bool IsAdmin { get; set; }
            [DataMember(Name = "kind")] public string Kind { get; set; }
            [DataMember(Name = "createdAt")] public string CreatedAt { get; set; }
        }

        [DataContract]
        internal sealed class Page
        {
            [DataMember(Name = "items")] public Item[] Items { get; set; }
            [DataMember(Name = "item")] public Item Item { get; set; }
            [DataMember(Name = "replies")] public Reply[] Replies { get; set; }
            [DataMember(Name = "nextFeedbackId")] public string NextFeedbackId { get; set; }
            [DataMember(Name = "nextReplyId")] public string NextReplyId { get; set; }
        }

        [DataContract]
        private sealed class Query
        {
            [DataMember(Name = "protocol")] public int Protocol = 1;
            [DataMember(Name = "modId")] public string ModId = "magic-storage";
            [DataMember(Name = "steamId")] public string SteamId { get; set; }
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "before")] public string Before { get; set; }
            [DataMember(Name = "after")] public string After { get; set; }
        }

        internal sealed class Result
        {
            internal Page Page;
            internal string ErrorKey;
        }

        internal static Task<Result> ReadAsync(string steamId, string id = null, string before = null, string after = null)
        {
            if (string.IsNullOrEmpty(steamId) || !Regex.IsMatch(steamId, @"^[1-9][0-9]{16}$"))
                return Task.FromResult(new Result { ErrorKey = "MS_FeedbackNeedSteam" });
            return Task.Run(async () =>
            {
                try
                {
                    using (var body = new MemoryStream())
                    {
                        new DataContractJsonSerializer(typeof(Query)).WriteObject(body,
                            new Query { SteamId = steamId, Id = id, Before = before, After = after });
                        using (var content = new ByteArrayContent(body.ToArray()))
                        {
                            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                            using (HttpResponseMessage response = await Client.PostAsync(Endpoint, content).ConfigureAwait(false))
                            {
                                int status = (int)response.StatusCode;
                                if (!response.IsSuccessStatusCode)
                                    return new Result { ErrorKey = status == 404 ? "MS_FeedbackNotFound" : status == 429 ? "MS_FeedbackQueryRateLimited" : "MS_FeedbackQueryFailed" };
                                byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                                return new Result { Page = Parse(bytes, id != null) };
                            }
                        }
                    }
                }
                catch (TaskCanceledException) { return new Result { ErrorKey = "MS_FeedbackTimeout" }; }
                catch { return new Result { ErrorKey = "MS_FeedbackQueryFailed" }; }
            });
        }

        internal static Page Parse(byte[] bytes, bool detail)
        {
            if (bytes == null || bytes.Length > MaxResponseBytes) throw new SerializationException();
            using (var stream = new MemoryStream(bytes))
            {
                var page = (Page)new DataContractJsonSerializer(typeof(Page)).ReadObject(stream);
                if (page == null || (detail ? page.Item == null || page.Replies == null : page.Items == null))
                    throw new SerializationException();
                if ((page.Items != null && page.Items.Length > 30) || (page.Replies != null && page.Replies.Length > 100))
                    throw new SerializationException();
                foreach (Item item in page.Items ?? new Item[0]) ValidateItem(item);
                if (page.Item != null) ValidateItem(page.Item);
                foreach (Reply reply in page.Replies ?? new Reply[0])
                    if (reply == null || reply.Body == null) throw new SerializationException();
                return page;
            }
        }

        private static void ValidateItem(Item item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id) || item.Title == null || item.Description == null)
                throw new SerializationException();
        }
    }
}
