using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    internal sealed class Dialog_StorageInventoryIcon : Window
    {
        private const float RowHeight = 34f;
        private readonly ThingDef selected;
        private readonly Action<ThingDef> choose;
        private readonly List<ThingDef> choices;
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(600f, Math.Min(720f, UI.screenHeight));

        internal Dialog_StorageInventoryIcon(ThingDef selected, Action<ThingDef> choose)
        {
            this.selected = selected;
            this.choose = choose;
            choices = ThingFilter.CreateOnlyEverStorableThingFilter().AllowedThingDefs
                .Where(def => StorageInventoryRule.HasUsableIcon(def) && !Find.HiddenItemsManager.Hidden(def))
                .OrderBy(def => def.label).ThenBy(def => def.defName).ToList();
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width - 30f, 32f), "MS_UI_FilterChooseIcon".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 40f, rect.width, 40f), "MS_UI_FilterIconHint".Translate());
            search.OnGUI(new Rect(0f, 88f, rect.width, 24f), () => scroll = Vector2.zero);
            var visible = choices.Where(def => search.filter.Matches(def)).ToList();
            search.noResultsMatched = visible.Count == 0;
            Rect viewport = new Rect(0f, 124f, rect.width, rect.height - 184f);
            Widgets.DrawMenuSection(viewport);
            Rect content = new Rect(0f, 0f, viewport.width - 24f, visible.Count * RowHeight);
            Widgets.BeginScrollView(viewport.ContractedBy(4f), ref scroll, content);
            try
            {
                int first = Math.Max(0, (int)(scroll.y / RowHeight));
                int last = Math.Min(visible.Count, (int)((scroll.y + viewport.height) / RowHeight) + 1);
                for (int i = first; i < last; i++)
                {
                    ThingDef def = visible[i];
                    Rect row = new Rect(0f, i * RowHeight, content.width, RowHeight);
                    if (def == selected) Widgets.DrawHighlightSelected(row);
                    else Widgets.DrawHighlightIfMouseover(row);
                    Widgets.DefIcon(new Rect(4f, row.y + 3f, 28f, 28f), def);
                    Widgets.Label(new Rect(42f, row.y + 4f, row.width - 46f, 28f), def.LabelCap.ToString().Truncate(row.width - 46f));
                    TooltipHandler.TipRegion(row, def.LabelCap + "\n" + def.description);
                    if (Widgets.ButtonInvisible(row))
                    {
                        choose(def);
                        Close();
                    }
                }
                if (visible.Count == 0)
                    Widgets.Label(new Rect(8f, 8f, content.width - 16f, 40f), "MS_UI_Empty".Translate());
            }
            finally { Widgets.EndScrollView(); }
            float buttonWidth = (rect.width - 16f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, rect.height - 40f, buttonWidth, 40f), "MS_UI_FilterIconAutomatic".Translate()))
            {
                choose(null);
                Close();
            }
            if (Widgets.ButtonText(new Rect(buttonWidth + 16f, rect.height - 40f, buttonWidth, 40f), "CancelButton".Translate()))
                Close();
        }
    }
}
