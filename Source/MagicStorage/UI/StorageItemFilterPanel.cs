using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MagicStorage
{
    // Rule definitions are shared settings; each panel owns its active filter modes.
    internal sealed class StorageItemFilterPanel
    {
        private readonly Map map;
        private readonly QuickSearchWidget search;
        private readonly Action changed, rulesChanged, resetSearch;
        private ModContentPack selectedSource;
        private readonly List<StorageInventoryRule> customRules;
        private readonly List<StorageInventoryFilterOption> filterOptions = new List<StorageInventoryFilterOption>();
        private readonly Dictionary<StorageInventoryRule, ThingDef> ruleIcons = new Dictionary<StorageInventoryRule, ThingDef>();
        private readonly int defaultFilterCount;
        private Vector2 filterScroll;
        private enum RuleAction { None, Delete, Edit }
        private RuleAction ruleAction;

        internal StorageItemFilterPanel(Map map, QuickSearchWidget search, Action changed, Action rulesChanged, Action resetSearch = null)
        {
            this.map = map; this.search = search; this.changed = changed;
            this.rulesChanged = rulesChanged; this.resetSearch = resetSearch;
            customRules = MagicStorageMod.Settings.InventoryFilters;
            foreach (var root in DefDatabase<ThingCategoryDef>.AllDefsListForReading.Where(def => def.resourceReadoutRoot))
                filterOptions.Add(new StorageInventoryFilterOption(root));
            defaultFilterCount = filterOptions.Count;
            foreach (var rule in customRules) filterOptions.Add(new StorageInventoryFilterOption(rule));
        }

        internal static float Width(float width) => Math.Min(260f, width * 0.25f);
        internal bool Allows(Thing item) => StorageInventoryFilters.Allows(item, filterOptions, selectedSource);
        internal bool CanGroup(Thing left, Thing right) => StorageInventoryFilters.CanGroup(left, right, customRules);

        internal void Draw(Rect rect, IEnumerable<Thing> sourceItems = null)
        {
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            Color color = GUI.color;
            Widgets.BeginGroup(rect);
            try { DrawSidebarContents(rect.AtZero(), sourceItems); }
            finally { Widgets.EndGroup(); Text.Font = font; Text.Anchor = anchor; GUI.color = color; }
        }

        private static string SourceLabel(ModContentPack source) => source.ModMetaData?.ShortName ?? source.Name;
        internal void DrawSourceButton(Rect rect, IEnumerable<Thing> items)
        {
            string label = "MS_UI_ModFilter".Translate(selectedSource != null ? SourceLabel(selectedSource) : "MS_UI_ModAll".Translate().ToString());
            TooltipHandler.TipRegion(rect, label);
            if (!Widgets.ButtonText(rect, label.Truncate(Math.Max(0f, rect.width - 8f)))) return;
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("MS_UI_ModAll".Translate(), () => { selectedSource = null; changed(); })
            };
            foreach (var source in items.Select(StorageInventoryFilters.SourceOf).Where(pack => pack != null)
                .GroupBy(pack => pack.PackageId, StringComparer.OrdinalIgnoreCase).Select(group => group.First())
                .OrderBy(SourceLabel, StringComparer.CurrentCultureIgnoreCase))
            {
                var chosen = source;
                options.Add(new FloatMenuOption(SourceLabel(source), () => { selectedSource = chosen; changed(); }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawSidebarContents(Rect rect, IEnumerable<Thing> sourceItems)
        {
            if (sourceItems != null) DrawSourceButton(new Rect(8f, 8f, rect.width - 56f, 24f), sourceItems);
            Rect reset = new Rect(rect.width - 36f, 6f, 30f, 30f);
            Widgets.DrawHighlightIfMouseover(reset);
            GUI.DrawTexture(reset.ContractedBy(4f), TexButton.Reload);
            TooltipHandler.TipRegion(reset, "MS_UI_FilterReset".Translate());
            if (Widgets.ButtonInvisible(reset))
            {
                search?.Reset();
                resetSearch?.Invoke();
                selectedSource = null;
                ruleAction = RuleAction.None;
                foreach (var option in filterOptions) option.Mode = StorageInventoryFilterMode.Off;
                changed();
            }
            Rect viewport = new Rect(0f, 46f, rect.width, Math.Max(0f, rect.height - 46f));
            Widgets.DrawMenuSection(viewport);
            const float step = 52f;
            float width = viewport.width - 24f;
            int columns = Math.Max(1, (int)(width / step));
            int defaultRows = (defaultFilterCount + columns - 1) / columns;
            float customY = 30f + defaultRows * step + 16f;
            int customRows = (customRules.Count + 3 + columns - 1) / columns;
            Rect content = new Rect(0f, 0f, width, Math.Max(viewport.height - 8f, customY + 30f + customRows * step));
            StorageInventoryRule selectedRule = null;
            Widgets.BeginScrollView(viewport.ContractedBy(4f), ref filterScroll, content);
            try
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(8f, 4f, width - 8f, 24f), "MS_UI_FilterCategories".Translate());
                Widgets.DrawLineHorizontal(8f, customY - 8f, width - 16f);
                Widgets.Label(new Rect(8f, customY, width - 8f, 24f), "MS_UI_FilterCustom".Translate());
                Text.Font = GameFont.Small;
                for (int i = 0; i < filterOptions.Count; i++)
                {
                    bool isDefault = i < defaultFilterCount;
                    int index = isDefault ? i : i - defaultFilterCount;
                    float y = isDefault ? 30f : customY + 30f;
                    if (DrawFilterButton(new Rect(8f + index % columns * step, y + index / columns * step, 44f, 44f), filterOptions[i]))
                        selectedRule = filterOptions[i].Rule;
                }
                int addIndex = customRules.Count;
                Rect add = new Rect(8f + addIndex % columns * step, customY + 30f + addIndex / columns * step, 44f, 44f);
                if (DrawRuleTool(add, TexButton.Plus, "MS_UI_FilterCreate".Translate()))
                {
                    ruleAction = RuleAction.None;
                    OpenRuleEditor();
                }
                int minusIndex = addIndex + 1;
                Rect minus = new Rect(8f + minusIndex % columns * step, customY + 30f + minusIndex / columns * step, 44f, 44f);
                if (DrawRuleTool(minus, TexButton.Minus, "MS_UI_FilterDeleteMode".Translate(),
                    ruleAction == RuleAction.Delete ? Color.red : Color.white))
                    ruleAction = ruleAction == RuleAction.Delete ? RuleAction.None : RuleAction.Delete;
                int editIndex = addIndex + 2;
                Rect edit = new Rect(8f + editIndex % columns * step, customY + 30f + editIndex / columns * step, 44f, 44f);
                if (DrawRuleTool(edit, TexButton.Rename, "MS_UI_FilterEditMode".Translate(),
                    ruleAction == RuleAction.Edit ? Color.green : Color.white))
                    ruleAction = ruleAction == RuleAction.Edit ? RuleAction.None : RuleAction.Edit;
            }
            finally { Widgets.EndScrollView(); }
            // Change the lists only after all filter buttons have finished drawing.
            if (selectedRule != null && ruleAction == RuleAction.Edit) OpenRuleEditor(selectedRule);
            else if (selectedRule != null && ruleAction == RuleAction.Delete && customRules.Remove(selectedRule))
            {
                filterOptions.RemoveAll(option => option.Rule == selectedRule);
                ruleIcons.Remove(selectedRule);
                MagicStorageMod.Settings.Write();
                if (customRules.Count == 0) ruleAction = RuleAction.None;
                rulesChanged();
            }
        }

        private void OpenRuleEditor(StorageInventoryRule existingRule = null)
        {
            Find.WindowStack.Add(new Dialog_StorageInventoryRule(map, customRules.Count + 1, rule =>
            {
                if (existingRule == null)
                {
                    customRules.Add(rule);
                    filterOptions.Add(new StorageInventoryFilterOption(rule));
                }
                ruleIcons.Remove(rule);
                MagicStorageMod.Settings.Write();
                rulesChanged();
            }, existingRule));
        }

        private static bool DrawRuleTool(Rect rect, Texture2D icon, TaggedString tooltip, Color? color = null)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.25f, 0.25f, 0.25f));
            Widgets.DrawHighlightIfMouseover(rect);
            Color previous = GUI.color;
            GUI.color = color ?? Color.white;
            GUI.DrawTexture(rect.ContractedBy(10f), icon);
            GUI.color = previous;
            TooltipHandler.TipRegion(rect, tooltip);
            return Widgets.ButtonInvisible(rect);
        }

        private bool DrawFilterButton(Rect rect, StorageInventoryFilterOption option)
        {
            if (ruleAction != RuleAction.None && option.Rule != null)
            {
                bool deleting = ruleAction == RuleAction.Delete;
                return DrawRuleTool(rect, deleting ? TexButton.CloseXBig : TexButton.Rename,
                    (deleting ? "MS_UI_FilterDeleteRule" : "MS_UI_FilterEditRule").Translate(option.Rule.Name),
                    deleting ? Color.red : Color.white);
            }
            Color color = option.Mode == StorageInventoryFilterMode.Include ? new Color(0.24f, 0.48f, 0.16f) :
                option.Mode == StorageInventoryFilterMode.Exclude ? new Color(0.58f, 0.18f, 0.18f) : new Color(0.25f, 0.25f, 0.25f);
            Widgets.DrawBoxSolid(rect, color);
            Widgets.DrawHighlightIfMouseover(rect);
            if (option.Category != null) GUI.DrawTexture(rect.ContractedBy(6f), option.Category.icon);
            else
            {
                if (!ruleIcons.TryGetValue(option.Rule, out ThingDef icon))
                {
                    icon = option.Rule.ResolveIcon();
                    ruleIcons.Add(option.Rule, icon);
                }
                if (icon != null) Widgets.ThingIcon(rect.ContractedBy(6f), icon);
                else GUI.DrawTexture(rect.ContractedBy(10f), TexButton.CategorizedResourceReadout);
            }
            string name = option.Category?.LabelCap.ToString() ?? option.Rule.Name;
            string mode = ("MS_UI_FilterMode" + option.Mode).Translate();
            TooltipHandler.TipRegion(rect, name + "\n" + mode + "\n" + "MS_UI_FilterControls".Translate());
            if (Event.current.type == EventType.MouseDown && Mouse.IsOver(rect) &&
                (Event.current.button == 0 || Event.current.button == 1))
            {
                option.Toggle(Event.current.button == 1);
                Event.current.Use();
                changed();
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }
            return false;
        }

    }
}
