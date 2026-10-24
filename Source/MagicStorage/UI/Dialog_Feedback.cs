using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    internal sealed class Dialog_Feedback : Window
    {
        private string title = string.Empty;
        private string description = string.Empty;
        private string message = string.Empty;
        private bool showMods;
        private Vector2 bodyScroll;
        private Vector2 modScroll;
        private float bodyHeight = 500f;
        private FeedbackUploader.Capture capture;
        private Task<FeedbackUploader.Result> upload;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(680f, UI.screenWidth), Mathf.Min(720f, UI.screenHeight));

        private Dialog_Feedback()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            capture = FeedbackUploader.Collect();
        }

        internal static void Open()
        {
            if (Find.WindowStack.WindowOfType<Dialog_Feedback>() == null)
                Find.WindowStack.Add(new Dialog_Feedback());
        }

        public override void DoWindowContents(Rect rect)
        {
            PollUpload();
            GameFont oldFont = Text.Font;
            bool oldEnabled = GUI.enabled;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0, 0, rect.width - 35f, 36f), "MS_FeedbackTitle".Translate());
                Text.Font = GameFont.Small;
                var viewport = new Rect(0, 48f, rect.width, Mathf.Max(40f, rect.height - 144f));
                float width = Mathf.Max(1f, rect.width - 20f);
                Widgets.BeginScrollView(viewport, ref bodyScroll, new Rect(0, 0, width, Mathf.Max(viewport.height, bodyHeight)));
                try { bodyHeight = DrawFields(width); }
                finally { Widgets.EndScrollView(); GUI.enabled = oldEnabled; }
                Widgets.Label(new Rect(0, rect.height - 84f, rect.width, 42f), upload != null ? "MS_FeedbackUploading".Translate().ToString() : message);
                if (Widgets.ButtonText(new Rect(0, rect.height - 36f, 120f, 36f), "MS_FeedbackClose".Translate())) Close();
                Widgets.Label(new Rect(132f, rect.height - 30f, Mathf.Max(0f, rect.width - 304f), 30f), "MS_FeedbackNetworkHint".Translate());
                GUI.enabled = oldEnabled && upload == null;
                if (Widgets.ButtonText(new Rect(rect.width - 160f, rect.height - 36f, 160f, 36f), "MS_FeedbackSend".Translate()) && oldEnabled && upload == null)
                {
                    string validationError = FeedbackUploader.ValidateInput(title, description);
                    if (validationError != null) message = validationError.Translate();
                    else if (FeedbackUploader.CoolingDown) message = "MS_FeedbackCooldown".Translate();
                    else
                    {
                        try
                        {
                            capture = FeedbackUploader.Collect();
                            if (string.IsNullOrWhiteSpace(capture.Log)) message = "MS_FeedbackNoLog".Translate();
                            else upload = FeedbackUploader.UploadAsync(title.Trim(), description.Trim(), capture);
                        }
                        catch { message = "MS_FeedbackUploadFailed".Translate(); }
                    }
                }
            }
            finally { Text.Font = oldFont; GUI.enabled = oldEnabled; }
        }

        private float DrawFields(float width)
        {
            float y = 0f;
            string privacy = "MS_FeedbackPrivacy".Translate();
            float height = Text.CalcHeight(privacy, width);
            Widgets.Label(new Rect(0, y, width, height), privacy);
            y += height + 12f;
            string versions = "MS_FeedbackVersions".Translate(capture.GameVersion, capture.ModVersion);
            height = Text.CalcHeight(versions, width);
            Widgets.Label(new Rect(0, y, width, height), versions);
            y += height + 10f;
            string steamId = "MS_FeedbackSteamId".Translate(string.IsNullOrEmpty(capture.SteamId) ? "MS_FeedbackSteamUnavailable".Translate().ToString() : capture.SteamId);
            height = Text.CalcHeight(steamId, width);
            Widgets.Label(new Rect(0, y, width, height), steamId);
            y += height + 10f;
            int logKb = Encoding.UTF8.GetByteCount(capture.Log ?? string.Empty) / 1024;
            Widgets.Label(new Rect(0, y, width, 28f), "MS_FeedbackCollected".Translate(capture.Mods.Count, logKb));
            y += 30f;
            if (Widgets.ButtonText(new Rect(0, y, 150f, 28f), (showMods ? "MS_FeedbackHideMods" : "MS_FeedbackShowMods").Translate()))
                showMods = !showMods;
            y += 38f;
            if (showMods)
            {
                var list = new StringBuilder();
                for (int i = 0; i < capture.Mods.Count; i++)
                {
                    var mod = capture.Mods[i];
                    list.Append(i + 1).Append(". ").Append(mod.Name).Append(" (").Append(mod.PackageId).AppendLine(")");
                }
                string text = list.ToString();
                height = Math.Max(100f, Text.CalcHeight(text, width - 20f));
                Widgets.BeginScrollView(new Rect(0, y, width, 110f), ref modScroll, new Rect(0, 0, width - 20f, height));
                try { Widgets.Label(new Rect(0, 0, width - 20f, height), text); }
                finally { Widgets.EndScrollView(); }
                y += 120f;
            }
            GUI.enabled = upload == null;
            Widgets.Label(new Rect(0, y, width, 25f), "MS_FeedbackSubject".Translate());
            title = Widgets.TextField(new Rect(0, y + 27f, width, 30f), title, 120);
            y += 67f;
            Widgets.Label(new Rect(0, y, width, 25f), "MS_FeedbackDescription".Translate());
            description = Widgets.TextArea(new Rect(0, y + 27f, width, 145f), description);
            if (description.Length > 6000) description = description.Substring(0, 6000);
            return y + 185f;
        }

        private void PollUpload()
        {
            if (upload == null || !upload.IsCompleted) return;
            try
            {
                FeedbackUploader.Result result = upload.Result;
                message = result.Success ? "MS_FeedbackSuccess".Translate(result.Receipt).ToString() : result.MessageKey.Translate().ToString();
            }
            catch { message = "MS_FeedbackUploadFailed".Translate(); }
            finally { upload = null; }
        }
    }
}

