using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    internal sealed class Dialog_MyFeedback : Window
    {
        private readonly List<FeedbackReader.Item> items = new List<FeedbackReader.Item>();
        private readonly List<FeedbackReader.Reply> replies = new List<FeedbackReader.Reply>();
        private FeedbackReader.Item selected;
        private Task<FeedbackReader.Result> request;
        private string requestedId;
        private bool append;
        private string nextFeedbackId;
        private string nextReplyId;
        private string message = string.Empty;
        private Vector2 listScroll;
        private Vector2 detailScroll;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(780f, UI.screenWidth), Mathf.Min(720f, UI.screenHeight));

        private Dialog_MyFeedback()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            Start(null, false);
        }

        internal static void Open()
        {
            if (Find.WindowStack.WindowOfType<Dialog_MyFeedback>() == null)
                Find.WindowStack.Add(new Dialog_MyFeedback());
        }

        private void Start(string id, bool more)
        {
            if (request != null) return;
            requestedId = id;
            append = more;
            message = string.Empty;
            // Steam must be read on the game thread. The network task receives only strings.
            request = FeedbackReader.ReadAsync(FeedbackUploader.CollectSteamId(), id,
                id == null && more ? nextFeedbackId : null, id != null && more ? nextReplyId : null);
        }

        private void Poll()
        {
            if (request == null || !request.IsCompleted) return;
            try
            {
                FeedbackReader.Result result = request.Result;
                if (result.Page == null) { message = result.ErrorKey.Translate(); return; }
                FeedbackReader.Page page = result.Page;
                if (requestedId == null)
                {
                    if (!append) { items.Clear(); listScroll = Vector2.zero; }
                    items.AddRange(page.Items);
                    nextFeedbackId = page.NextFeedbackId;
                    selected = null;
                    if (items.Count == 0) message = "MS_FeedbackEmpty".Translate();
                }
                else
                {
                    if (!append) { replies.Clear(); detailScroll = Vector2.zero; }
                    selected = page.Item;
                    replies.AddRange(page.Replies);
                    nextReplyId = page.NextReplyId;
                }
            }
            catch { message = "MS_FeedbackQueryFailed".Translate(); }
            finally { request = null; }
        }

        public override void DoWindowContents(Rect rect)
        {
            Poll();
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            bool oldEnabled = GUI.enabled;
            try
            {
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0, 0, rect.width - 35f, 36f), "MS_FeedbackMyTitle".Translate());
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0, 44f, rect.width, 48f), request != null ? "MS_FeedbackLoading".Translate().ToString() : message);
                GUI.enabled = oldEnabled && request == null;
                var viewport = new Rect(0, 100f, rect.width, Mathf.Max(30f, rect.height - 152f));
                float width = Mathf.Max(1f, rect.width - 20f);
                bool detail = selected != null;
                float height = detail ? DrawDetail(width, false) : DrawList(width, false);
                Vector2 scroll = detail ? detailScroll : listScroll;
                Widgets.BeginScrollView(viewport, ref scroll, new Rect(0, 0, width, Mathf.Max(viewport.height, height)));
                try { if (detail) DrawDetail(width, true); else DrawList(width, true); }
                finally { Widgets.EndScrollView(); }
                if (detail) detailScroll = scroll; else listScroll = scroll;

                float buttonWidth = (rect.width - 24f) / 4f;
                float bottom = rect.height - 36f;
                if (Widgets.ButtonText(new Rect(0, bottom, buttonWidth, 36f), "MS_FeedbackRefresh".Translate()))
                    Start(selected?.Id, false);
                if (selected != null && Widgets.ButtonText(new Rect(buttonWidth + 8f, bottom, buttonWidth, 36f), "MS_FeedbackBack".Translate()))
                { selected = null; message = string.Empty; }
                string next = selected == null ? nextFeedbackId : nextReplyId;
                if (!string.IsNullOrEmpty(next) && Widgets.ButtonText(new Rect((buttonWidth + 8f) * 2f, bottom, buttonWidth, 36f), "MS_FeedbackLoadMore".Translate()))
                    Start(selected?.Id, true);
                GUI.enabled = oldEnabled;
                if (Widgets.ButtonText(new Rect((buttonWidth + 8f) * 3f, bottom, buttonWidth, 36f), "MS_FeedbackClose".Translate())) Close();
            }
            finally { Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = oldWrap; GUI.enabled = oldEnabled; }
        }

        private float DrawList(float width, bool draw)
        {
            float y = 0f;
            foreach (FeedbackReader.Item item in items)
            {
                string label = Plain(item.Title) + "\n" + Plain(item.Receipt) + " · " + Status(item.Status) + " · " + Date(item.CreatedAt);
                float height = Mathf.Max(64f, Text.CalcHeight(label, Mathf.Max(1f, width - 20f)) + 16f);
                if (draw && Widgets.ButtonText(new Rect(0, y, width, height), label)) Start(item.Id, false);
                y += height + 10f;
            }
            return y;
        }

        private float DrawDetail(float width, bool draw)
        {
            float y = 0f;
            Label(Plain(selected.Title), width, ref y, draw);
            Label(Plain(selected.Receipt) + " · " + Status(selected.Status), width, ref y, draw);
            Label("MS_FeedbackVersions".Translate(Plain(selected.GameVersion), Plain(selected.ModVersion)), width, ref y, draw);
            Label("MS_FeedbackDates".Translate(Date(selected.CreatedAt), Date(selected.UpdatedAt)), width, ref y, draw);
            if (!string.IsNullOrEmpty(selected.MergedReceipt))
                Label("MS_FeedbackMerged".Translate(Plain(selected.MergedReceipt)), width, ref y, draw);
            y += 8f;
            Label(Plain(selected.Description), width, ref y, draw);
            if (!string.IsNullOrEmpty(selected.Steps)) Label(Plain(selected.Steps), width, ref y, draw);
            y += 12f;
            Label("MS_FeedbackDiscussion".Translate(), width, ref y, draw);
            if (replies.Count == 0) Label("MS_FeedbackNoReplies".Translate(), width, ref y, draw);
            foreach (FeedbackReader.Reply reply in replies)
            {
                if (draw) Widgets.DrawLineHorizontal(0, y, width);
                y += 10f;
                string author = reply.Kind == "system" ? "MS_FeedbackSystem".Translate().ToString() : Plain(reply.AuthorName);
                if (reply.Kind != "system" && reply.IsAdmin) author += " · " + "MS_FeedbackAuthor".Translate();
                Label(author + " · " + Date(reply.CreatedAt), width, ref y, draw);
                Label(Plain(reply.Body), width, ref y, draw);
                y += 8f;
            }
            return y;
        }

        private static void Label(string value, float width, ref float y, bool draw)
        {
            float height = Text.CalcHeight(value, width);
            if (draw) Widgets.Label(new Rect(0, y, width, height), value);
            y += height + 8f;
        }

        private static string Plain(string value) => (value ?? string.Empty).Replace("<", "＜").Replace(">", "＞");

        private static string Date(string value)
        {
            DateTimeOffset date;
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                ? date.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) : Plain(value);
        }

        private static string Status(string value)
        {
            switch (value)
            {
                case "pending": case "confirmed": case "working": case "resolved": case "needs_info": case "declined":
                    return ("MS_FeedbackStatus_" + value).Translate();
                default: return Plain(value);
            }
        }
    }
}
