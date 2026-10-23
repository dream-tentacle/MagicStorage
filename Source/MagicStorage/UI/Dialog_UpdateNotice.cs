using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    internal sealed class Dialog_UpdateNotice : Window
    {
        private readonly List<UpdateNoticeDef> notices;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(760f, UI.screenWidth), Mathf.Min(640f, UI.screenHeight));

        private Dialog_UpdateNotice(List<UpdateNoticeDef> notices)
        {
            this.notices = notices;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnAccept = true;
        }

        internal static void Open(List<UpdateNoticeDef> notices)
        {
            if (!notices.NullOrEmpty()) Find.WindowStack.Add(new Dialog_UpdateNotice(notices));
        }

        public override void DoWindowContents(Rect rect)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
            try
            {
                Text.Font = GameFont.Medium;
                string title = UpdateNoticeUtility.OwnMod?.Name ?? "MS_UpdateNotice_Title".Translate().ToString();
                float titleHeight = Text.CalcHeight(title, rect.width - 30f);
                Widgets.Label(new Rect(0f, 0f, rect.width - 30f, titleHeight), title);
                Text.Font = GameFont.Small;
                float subtitleHeight = Text.CalcHeight("MS_UpdateNotice_Title".Translate(), rect.width);
                Widgets.Label(new Rect(0f, titleHeight + 6f, rect.width, subtitleHeight), "MS_UpdateNotice_Title".Translate());

                float top = titleHeight + subtitleHeight + 20f;
                var viewport = new Rect(0f, top, rect.width, Mathf.Max(0f, rect.height - top - 52f));
                float contentWidth = Mathf.Max(1f, viewport.width - 20f);
                var view = new Rect(0f, 0f, contentWidth, Mathf.Max(viewport.height, DrawNotices(contentWidth, false)));
                Widgets.BeginScrollView(viewport, ref scroll, view);
                try { DrawNotices(contentWidth, true); }
                finally { Widgets.EndScrollView(); }

                if (Widgets.ButtonText(new Rect((rect.width - 160f) / 2f, rect.height - 36f, 160f, 36f),
                    "MS_UpdateNotice_Confirm".Translate())) Close();
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWrap;
            }
        }

        // Measure and draw through the same layout so translated lines cannot overlap.
        private float DrawNotices(float width, bool draw)
        {
            float y = 0f;
            foreach (UpdateNoticeDef notice in notices)
            {
                if (y > 0f) y += 22f;
                string header = "<b>" + "MS_UpdateNotice_Version".Translate(notice.version) + "</b>";
                DrawText(header, 0f, ref y, width, draw);
                y += 6f;
                if (draw) Widgets.DrawLineHorizontal(0f, y, width);
                y += 12f;
                if (!notice.sections.NullOrEmpty())
                {
                    bool first = true;
                    foreach (UpdateNoticeSection section in notice.sections)
                    {
                        if (section == null || section.entries.NullOrEmpty()) continue;
                        if (!first) y += 16f;
                        if (!section.heading.NullOrEmpty())
                        {
                            DrawText("<b>" + section.heading + "</b>", 0f, ref y, width, draw);
                            y += 6f;
                        }
                        DrawEntries(section.entries, ref y, width, draw);
                        first = false;
                    }
                }
                else DrawEntries(notice.entries, ref y, width, draw);
            }
            return y;
        }

        private static void DrawEntries(List<string> entries, ref float y, float width, bool draw)
        {
            if (entries.NullOrEmpty()) return;
            const float indent = 18f;
            bool first = true;
            foreach (string entry in entries)
            {
                if (entry.NullOrEmpty()) continue;
                if (!first) y += 10f;
                if (draw) Widgets.Label(new Rect(0f, y, indent, Text.LineHeight), "•");
                DrawText(entry, indent, ref y, Mathf.Max(1f, width - indent), draw);
                first = false;
            }
        }

        private static void DrawText(string text, float x, ref float y, float width, bool draw)
        {
            float height = Text.CalcHeight(text, width);
            if (draw) Widgets.Label(new Rect(x, y, width, height), text);
            y += height;
        }
    }
}
