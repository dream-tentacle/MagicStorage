using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Dialog_CosmicCrafting : Window
    {
        private readonly Building_StorageCore core;
        private readonly CosmicCraftingListFilter listFilter = new CosmicCraftingListFilter();
        private CraftingOrder selected;
        private Vector2 scroll, detailScroll;
        private float detailHeight = 750f;
        private readonly Dictionary<string, string> buffers = new Dictionary<string, string>();
        private readonly Dictionary<CraftingOrder, string> statuses = new Dictionary<CraftingOrder, string>();
        private float refreshAt;
        private CraftingOrder countedOrder;
        private float countRefreshAt;
        private long displayedCount;
        public override Vector2 InitialSize => new Vector2(Math.Min(1100, UI.screenWidth), Math.Min(800, UI.screenHeight));
        public Dialog_CosmicCrafting(Building_StorageCore core)
        {
            this.core = core; selected = core.Crafting.Orders.FirstOrDefault();
            forcePause = false; absorbInputAroundWindow = true; doCloseX = true; doCloseButton = true;
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!core.Spawned) { Close(); return; }
            core.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (Time.realtimeSinceStartup >= refreshAt)
            {
                statuses.Clear();
                foreach (var order in core.Crafting.Orders) statuses[order] = core.Map.GetComponent<MapComponent_CosmicCrafting>().Status(core, order);
                refreshAt = Time.realtimeSinceStartup + 0.5f;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 30, 35), "MS_Craft_Title".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 40, rect.width, 50), "MS_Craft_ProductionNotice".Translate());
            float leftWidth = rect.width * 0.46f;
            if (Widgets.ButtonText(new Rect(0, 95, leftWidth, 32), "MS_Craft_Add".Translate()))
                Find.WindowStack.Add(new Dialog_CosmicRecipePicker(recipe =>
                {
                    if (!listFilter.Matches(recipe)) listFilter.Reset();
                    SelectOrder(core.Crafting.Add(recipe)); scroll = Vector2.zero; refreshAt = 0;
                }));
            float tabsHeight = listFilter.DrawTabs(new Rect(0, 137, leftWidth, 32), () => scroll = Vector2.zero);
            var visible = core.Crafting.Orders.Where(o => listFilter.Matches(o.Recipe)).ToList();
            float workbenchY = 137 + tabsHeight + 8;
            listFilter.DrawWorkbenchButton(new Rect(0, workbenchY, leftWidth, 32),
                visible.Select(o => o.Recipe), () => scroll = Vector2.zero);
            if (!visible.Contains(selected) && (selected != null || visible.Count > 0)) SelectOrder(visible.FirstOrDefault());
            float listY = workbenchY + 40;
            DrawOrders(new Rect(0, listY, leftWidth, rect.height - listY - 60), visible);
            if (selected != null && core.Crafting.Orders.Contains(selected))
                DrawDetails(new Rect(leftWidth + 16, 95, rect.width - leftWidth - 16, rect.height - 155));
        }
        private void SelectOrder(CraftingOrder order)
        { selected = order; buffers.Clear(); detailScroll = Vector2.zero; countRefreshAt = 0; }
        private void DrawOrders(Rect rect, List<CraftingOrder> visible)
        {
            Widgets.DrawMenuSection(rect);
            if (visible.Count == 0)
            { Widgets.Label(rect.ContractedBy(8), "MS_Craft_NoMatchingOrders".Translate()); return; }
            Rect view = new Rect(0, 0, rect.width - 18, visible.Count * 132);
            Widgets.BeginScrollView(rect, ref scroll, view);
            int index = 0;
            foreach (var order in visible)
            {
                Rect row = new Rect(4, index++ * 132, view.width - 8, 128);
                if (selected == order) Widgets.DrawHighlightSelected(row);
                if (Widgets.ButtonInvisible(new Rect(row.x, row.y, row.width, 62)))
                { SelectOrder(order); }
                Widgets.Label(new Rect(row.x + 6, row.y + 3, row.width - 12, 28), order.Recipe.LabelCap);
                Widgets.Label(new Rect(row.x + 6, row.y + 31, row.width - 12, 28),
                    statuses.TryGetValue(order, out var status) ? status : "");
                Rect progressRect = new Rect(row.x + 6, row.y + 60, row.width - 12, 22);
                Widgets.FillableBar(progressRect, order.Progress);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(progressRect, "MS_Craft_Progress".Translate(order.Progress.ToString("P0")));
                Text.Anchor = TextAnchor.UpperLeft;
                float y = row.y + 90, x = row.x + 6;
                if (Widgets.ButtonText(new Rect(x, y, 40, 28), "↑")) { core.Crafting.Move(order, -1); refreshAt = 0; }
                if (Widgets.ButtonText(new Rect(x + 44, y, 40, 28), "↓")) { core.Crafting.Move(order, 1); refreshAt = 0; }
                if (Widgets.ButtonText(new Rect(x + 88, y, 85, 28), (order.Suspended ? "MS_Craft_Resume" : "MS_Craft_Pause").Translate()))
                { order.Suspended = !order.Suspended; order.Changed(); refreshAt = 0; }
                if (Widgets.ButtonText(new Rect(x + 177, y, 85, 28), "MS_Craft_Delete".Translate()))
                {
                    core.Crafting.Remove(order);
                    if (selected == order) SelectOrder(visible.FirstOrDefault(o => core.Crafting.Orders.Contains(o)));
                    refreshAt = 0;
                }
            }
            Widgets.EndScrollView();
        }
        private void DrawDetails(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect view = new Rect(0, 0, rect.width - 20, detailHeight);
            Widgets.BeginScrollView(rect, ref detailScroll, view);
            float w = view.width - 16, y = 8;
            Widgets.Label(new Rect(8, y, w, 32), selected.Recipe.LabelCap); y += 34;
            Widgets.Label(new Rect(8, y, w, 50), "MS_Craft_WorkTypes".Translate(CraftingRecipeCatalog.WorkLabels(selected.Recipe))); y += 52;
            if (Widgets.ButtonText(new Rect(8, y, w, 30), ModeLabel(selected.Mode)))
            {
                var options = new List<FloatMenuOption>();
                foreach (CraftingRepeatMode mode in Enum.GetValues(typeof(CraftingRepeatMode)))
                {
                    var chosen = mode;
                    bool supported = mode != CraftingRepeatMode.TargetCount || CraftingServices.Products.CanCount(selected.Recipe);
                    options.Add(new FloatMenuOption(ModeLabel(mode), supported ? (Action)(() =>
                    { selected.Mode = chosen; selected.Paused = false; Changed(); }) : null));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            y += 38;
            if (selected.Mode == CraftingRepeatMode.RepeatCount) Number(ref y, w, "MS_Craft_RepeatCount", ref selected.RepeatCount, 0, 999999);
            if (selected.Mode == CraftingRepeatMode.TargetCount)
            {
                Number(ref y, w, "MS_Craft_TargetCount", ref selected.TargetCount, 1, 999999);
                if (countedOrder != selected || Time.realtimeSinceStartup >= countRefreshAt)
                {
                    countedOrder = selected;
                    displayedCount = CraftingServices.Products.Count(core, selected);
                    countRefreshAt = Time.realtimeSinceStartup + 0.5f;
                }
                Widgets.Label(new Rect(8, y, w, 28), "MS_Craft_ProductCount".Translate(displayedCount)); y += 32;
                if (Widgets.ButtonText(new Rect(8, y, w, 30), "MS_Craft_AlsoCounts".Translate()))
                    Find.WindowStack.Add(new Dialog_CosmicAdditionalCounts(selected, core.Map, Changed));
                y += 38;
                Check(ref y, w, "MS_Craft_PauseSatisfied", ref selected.PauseWhenSatisfied);
                if (selected.PauseWhenSatisfied) Number(ref y, w, "MS_Craft_Unpause", ref selected.UnpauseAt, 0, Math.Max(0, selected.TargetCount - 1));
            }
            Rect deliveryRect = new Rect(8, y, w, 28);
            // Delivery changes apply to the current iteration without invalidating its worker.
            Widgets.CheckboxLabeled(deliveryRect, "MS_Craft_DropProductsOnFloor".Translate(), ref selected.DropProductsOnFloor);
            y += 32;
            string workerLabel = selected.Worker != null ? selected.Worker.LabelShortCap.ToString() : ("MS_Craft_Worker_" + selected.WorkerKind).Translate().ToString();
            if (Widgets.ButtonText(new Rect(8, y, w, 30), "MS_Craft_Worker".Translate(workerLabel))) ChooseWorker();
            y += 38;
            if (selected.Recipe.workSkill != null)
            {
                Widgets.Label(new Rect(8, y, w, 28), "MS_Craft_Skill".Translate(selected.Recipe.workSkill.LabelCap)); y += 32;
                Number(ref y, w, "MS_Craft_MinSkill", ref selected.SkillRange.min, 0, selected.SkillRange.max);
                Number(ref y, w, "MS_Craft_MaxSkill", ref selected.SkillRange.max, selected.SkillRange.min, 20);
                Widgets.Label(new Rect(8, y, w, 48), "MS_Craft_NamedWorkerHint".Translate()); y += 52;
            }
            if (Widgets.ButtonText(new Rect(8, y, w, 30), "MS_Craft_Ingredients".Translate()))
                Find.WindowStack.Add(new Dialog_CosmicIngredients(selected, core.Map, Changed));
            y += 38;
            if (selected.Mode == CraftingRepeatMode.TargetCount)
            {
                var filters = new CraftingProductFilters(selected.Recipe);
                if (filters.IncludeEquipped) Check(ref y, w, "MS_Craft_IncludeEquipped", ref selected.IncludeEquipped);
                if (filters.IncludeTainted) Check(ref y, w, "MS_Craft_IncludeTainted", ref selected.IncludeTainted);
                if (filters.HitPoints)
                {
                    int min = Mathf.RoundToInt(selected.HitPoints.min * 100), max = Mathf.RoundToInt(selected.HitPoints.max * 100);
                    Number(ref y, w, "MS_Craft_MinHP", ref min, 0, max);
                    Number(ref y, w, "MS_Craft_MaxHP", ref max, min, 100);
                    selected.HitPoints = new FloatRange(min / 100f, max / 100f);
                }
                if (filters.Quality) { QualityButton(ref y, w, true); QualityButton(ref y, w, false); }
                if (filters.AllowedStuff) Check(ref y, w, "MS_Craft_LimitStuff", ref selected.LimitToAllowedStuff);
            }
            detailHeight = y + 8;
            Widgets.EndScrollView();
        }
        private void Changed()
        {
            if (selected != null)
            {
                selected.UnpauseAt = Math.Max(0, Math.Min(selected.UnpauseAt, selected.TargetCount - 1));
                selected.Changed();
            }
            refreshAt = 0;
            countRefreshAt = 0;
        }
        private static string ModeLabel(CraftingRepeatMode mode) => ("MS_Craft_Mode_" + mode).Translate();
        private void Number(ref float y, float width, string key, ref int value, int min, int max)
        {
            Widgets.Label(new Rect(8, y, width - 105, 28), key.Translate());
            if (!buffers.TryGetValue(key, out string buffer)) buffer = value.ToString();
            int before = value;
            Widgets.TextFieldNumeric(new Rect(width - 90, y, 98, 28), ref value, ref buffer, min, max);
            buffers[key] = buffer;
            if (value != before) Changed();
            y += 34;
        }
        private void Check(ref float y, float width, string key, ref bool value)
        {
            bool before = value;
            Widgets.CheckboxLabeled(new Rect(8, y, width, 28), key.Translate(), ref value);
            if (value != before) Changed();
            y += 32;
        }
        private void QualityButton(ref float y, float width, bool minimum)
        {
            var current = minimum ? selected.Quality.min : selected.Quality.max;
            if (Widgets.ButtonText(new Rect(8, y, width, 28), (minimum ? "MS_Craft_MinQuality" : "MS_Craft_MaxQuality").Translate(current.GetLabel())))
            {
                var options = new List<FloatMenuOption>();
                foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
                {
                    var chosen = quality;
                    if ((minimum && quality > selected.Quality.max) || (!minimum && quality < selected.Quality.min)) continue;
                    options.Add(new FloatMenuOption(quality.GetLabel(), () =>
                    { if (minimum) selected.Quality.min = chosen; else selected.Quality.max = chosen; Changed(); }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            y += 34;
        }
        private void ChooseWorker()
        {
            var order = selected;
            var options = new List<FloatMenuOption>();
            bool mechanitors = ModsConfig.BiotechActive && order.Recipe.mechanitorOnlyRecipe;
            foreach (CraftingWorkerKind kind in Enum.GetValues(typeof(CraftingWorkerKind)))
            {
                if (mechanitors && kind != CraftingWorkerKind.Anyone) continue;
                if (kind == CraftingWorkerKind.Slaves && !ModsConfig.IdeologyActive) continue;
                if ((kind == CraftingWorkerKind.Mechs || kind == CraftingWorkerKind.NonMechs) &&
                    (!ModsConfig.BiotechActive || !MechWorkUtility.AnyWorkMechCouldDo(order.Recipe))) continue;
                var chosen = kind;
                options.Add(new FloatMenuOption(mechanitors ? "MS_Craft_AnyMechanitor".Translate().ToString() : ("MS_Craft_Worker_" + kind).Translate().ToString(), () =>
                { order.Worker = null; order.WorkerKind = chosen; Changed(); }));
            }
            var types = CraftingRecipeCatalog.Routes(order.Recipe).Select(w => w.workType).Distinct().ToList();
            foreach (var pawn in PawnsFinder.AllMaps_FreeColonists.Where(p => !mechanitors || MechanitorUtility.IsMechanitor(p))
                .OrderBy(p => types.All(p.WorkTypeIsDisabled))
                .ThenByDescending(p => types.Any(t => p.workSettings.WorkIsActive(t)))
                .ThenByDescending(p => order.Recipe.workSkill == null ? 0 : p.skills.GetSkill(order.Recipe.workSkill).Level).ThenBy(p => p.LabelShortCap.ToString()))
            {
                var chosen = pawn;
                bool disabled = types.All(pawn.WorkTypeIsDisabled);
                string label = pawn.LabelShortCap;
                if (order.Recipe.workSkill != null) label += " (" + order.Recipe.workSkill.LabelCap + " " + pawn.skills.GetSkill(order.Recipe.workSkill).Level + ")";
                if (disabled) label += " — " + "MS_Craft_Incapable".Translate();
                else if (!types.Any(t => pawn.workSettings.WorkIsActive(t))) label += " — " + "MS_Craft_WorkDisabled".Translate();
                options.Add(new FloatMenuOption(label, disabled ? null : (Action)(() => { order.Worker = chosen; Changed(); })));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    internal sealed class Dialog_CosmicIngredients : Window
    {
        private readonly CraftingOrder order;
        private readonly Map map;
        private readonly Action changed;
        private readonly ThingFilterUI.UIState state = new ThingFilterUI.UIState();
        public override Vector2 InitialSize => new Vector2(500, Math.Min(720, UI.screenHeight));
        internal Dialog_CosmicIngredients(CraftingOrder order, Map map, Action changed)
        { this.order = order; this.map = map; this.changed = changed; forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(0, 0, rect.width, 30), "MS_Craft_Ingredients".Translate());
            ThingFilterUI.DoThingFilterConfigWindow(new Rect(0, 38, rect.width, rect.height - 100), state, order.Ingredients,
                order.Recipe.fixedIngredientFilter, forceHiddenFilters: order.Recipe.forceHiddenSpecialFilters, map: map);
        }
        public override void PostClose() { base.PostClose(); order.Changed(); changed(); }
    }

    internal sealed class Dialog_CosmicAdditionalCounts : Window
    {
        private readonly CraftingOrder order;
        private readonly Map map;
        private readonly Action changed;
        private readonly ThingFilter parent = ThingFilter.CreateOnlyEverStorableThingFilter();
        private readonly ThingFilterUI.UIState state = new ThingFilterUI.UIState();
        public override Vector2 InitialSize => new Vector2(500, Math.Min(720, UI.screenHeight));
        internal Dialog_CosmicAdditionalCounts(CraftingOrder order, Map map, Action changed)
        { this.order = order; this.map = map; this.changed = changed; forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(0, 0, rect.width, 30), "MS_Craft_AlsoCounts".Translate());
            string hint = "MS_Craft_AlsoCountsHint".Translate();
            float hintHeight = Text.CalcHeight(hint, rect.width);
            Widgets.Label(new Rect(0, 38, rect.width, hintHeight), hint);
            float y = 38 + hintHeight + 12;
            ThingFilterUI.DoThingFilterConfigWindow(new Rect(0, y, rect.width, rect.height - y - 62), state,
                order.AdditionalCounts, parent, forceHideHitPointsConfig: true, forceHideQualityConfig: true, map: map);
        }
        public override void PostClose() { base.PostClose(); changed(); }
    }

    internal sealed class CosmicCraftingListFilter
    {
        private WorkTypeDef workType;
        private ThingDef workbench;

        internal void Reset() { workType = null; workbench = null; }
        internal bool Matches(RecipeDef recipe) => recipe != null &&
            (workType == null || CraftingRecipeCatalog.HasType(recipe, workType)) &&
            (workbench == null || CraftingRecipeCatalog.Workbenches(recipe).Contains(workbench));

        internal float DrawTabs(Rect rect, Action changed)
        {
            var tabs = new List<TabRecord>();
            tabs.Add(new TabRecord("MS_Craft_All".Translate(), () =>
            { Reset(); changed(); }, workType == null));
            foreach (var type in CraftingRecipeCatalog.WorkTypes)
            {
                var chosen = type;
                tabs.Add(new TabRecord(type.LabelCap, () =>
                { workType = chosen; workbench = null; changed(); }, workType == type));
            }
            float minWidth = Mathf.Min(rect.width, Mathf.Max(90f, tabs.Max(t => Text.CalcSize(t.label).x + 32f)));
            float maxWidth = Mathf.Max(minWidth, 200f);
            float height = TabDrawer.GetOverflowTabHeight(rect, tabs, minWidth, maxWidth);
            TabDrawer.DrawTabsOverflow(rect, tabs, minWidth, maxWidth);
            return height;
        }

        internal void DrawWorkbenchButton(Rect rect, IEnumerable<RecipeDef> visibleRecipes, Action changed)
        {
            string label = workbench != null ? workbench.LabelCap.ToString() : "MS_Craft_All".Translate().ToString();
            if (!Widgets.ButtonText(rect, "MS_Craft_WorkbenchFilter".Translate(label))) return;
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("MS_Craft_All".Translate(), () => { workbench = null; changed(); })
            };
            foreach (var table in visibleRecipes.SelectMany(CraftingRecipeCatalog.Workbenches).Distinct().OrderBy(t => t.label))
            {
                var chosen = table;
                options.Add(new FloatMenuOption(table.LabelCap, () => { workbench = chosen; changed(); }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    internal sealed class Dialog_CosmicRecipePicker : Window
    {
        private readonly Action<RecipeDef> add;
        private readonly List<RecipeDef> recipes;
        private readonly CosmicCraftingListFilter listFilter = new CosmicCraftingListFilter();
        private string search = "";
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(Math.Min(750, UI.screenWidth), Math.Min(750, UI.screenHeight));
        internal Dialog_CosmicRecipePicker(Action<RecipeDef> add)
        { this.add = add; recipes = CraftingRecipeCatalog.Available.ToList(); forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(0, 0, rect.width - 30, 30), "MS_Craft_Add".Translate());
            string previousSearch = search;
            search = Widgets.TextField(new Rect(0, 38, rect.width, 30), search);
            if (search != previousSearch) scroll = Vector2.zero;
            float tabsHeight = listFilter.DrawTabs(new Rect(0, 80, rect.width, 32), () => scroll = Vector2.zero);
            var visible = recipes.Where(r => CraftingRecipeCatalog.CanAdd(r) && listFilter.Matches(r) &&
                r.label.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();
            float workbenchY = 80 + tabsHeight + 8;
            listFilter.DrawWorkbenchButton(new Rect(0, workbenchY, rect.width, 32), visible, () => scroll = Vector2.zero);
            float listY = workbenchY + 40;
            var outer = new Rect(0, listY, rect.width, rect.height - listY - 60);
            Widgets.DrawMenuSection(outer);
            if (visible.Count == 0)
            { Widgets.Label(outer.ContractedBy(8), "MS_Craft_NoMatchingRecipes".Translate()); return; }
            var view = new Rect(0, 0, outer.width - 18, visible.Count * 56);
            Widgets.BeginScrollView(outer, ref scroll, view);
            int index = 0;
            foreach (var recipe in visible)
            {
                Rect row = new Rect(0, index++ * 56, view.width, 52);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.Label(new Rect(4, row.y, row.width - 8, 26), recipe.LabelCap);
                Widgets.Label(new Rect(4, row.y + 26, row.width - 8, 26), CraftingRecipeCatalog.WorkLabels(recipe));
                if (Widgets.ButtonInvisible(row)) { if (CraftingRecipeCatalog.CanAdd(recipe)) add(recipe); Close(); }
            }
            Widgets.EndScrollView();
        }
    }
}
