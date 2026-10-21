using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Dialog_CosmicCrafting : Window
    {
        private static readonly Texture2D CompactModeIcon = ContentFinder<Texture2D>.Get("UI/Gizmos/MagicStorage/CompactMode");
        private static readonly Texture2D FavoriteEmptyIcon = ContentFinder<Texture2D>.Get("UI/Gizmos/MagicStorage/FavoriteEmpty");
        private static readonly Texture2D FavoriteFilledIcon = ContentFinder<Texture2D>.Get("UI/Gizmos/MagicStorage/FavoriteFilled");
        private static readonly Texture2D FavoriteHoverIcon = ContentFinder<Texture2D>.Get("UI/Gizmos/MagicStorage/FavoriteHover");
        private static Vector2? lastClosedPosition;
        private readonly Building_StorageCore core;
        private readonly CosmicCraftingListFilter listFilter = new CosmicCraftingListFilter();
        private Vector2 scroll, workbenchScroll;
        private readonly GameComponent_CosmicCraftingUI uiState;
        private bool CompactMode
        {
            get => uiState.CompactWorkbenches;
            set => uiState.CompactWorkbenches = value;
        }
        private readonly Dictionary<CraftingOrder, string> statuses = new Dictionary<CraftingOrder, string>();
        private float refreshAt;
        private int orderReorderGroup = -1;
        private int orderDragButton = -1;
        private readonly Dictionary<CraftingOrder, long> productCounts = new Dictionary<CraftingOrder, long>();
        public override Vector2 InitialSize => new Vector2(Math.Min(1100, UI.screenWidth), Math.Min(800, UI.screenHeight));
        public Dialog_CosmicCrafting(Building_StorageCore core)
        {
            this.core = core;
            uiState = Current.Game.GetComponent<GameComponent_CosmicCraftingUI>();
            forcePause = false; absorbInputAroundWindow = true; doCloseX = true; doCloseButton = true;
            draggable = true;
        }
        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            if (lastClosedPosition.HasValue) windowRect.position = lastClosedPosition.Value;
        }
        public override void PostClose()
        {
            lastClosedPosition = windowRect.position;
            base.PostClose();
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!core.Spawned) { Close(); return; }
            core.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (Time.realtimeSinceStartup >= refreshAt)
            {
                statuses.Clear(); productCounts.Clear();
                foreach (var order in core.Crafting.Orders) statuses[order] = core.Map.GetComponent<MapComponent_CosmicCrafting>().Status(core, order);
                refreshAt = Time.realtimeSinceStartup + 0.5f;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 30, 35), "MS_Craft_Title".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 40, rect.width, 50), "MS_Craft_ProductionNotice".Translate());
            const float tabsY = 95f;
            float tabsHeight = listFilter.DrawTabs(new Rect(0, tabsY, rect.width, 32), SharedFilterChanged);
            float filterY = tabsY + tabsHeight + 8;
            float allRecipesWidth = Math.Min(136f, rect.width * 0.35f);
            listFilter.DrawSharedSourceButton(new Rect(0, filterY, rect.width - allRecipesWidth - 8, 32),
                CraftingRecipeCatalog.AvailableWorkbenches(), core.Crafting.Orders.Select(order => order.Recipe), SharedFilterChanged);
            if (Widgets.ButtonText(new Rect(rect.xMax - allRecipesWidth, filterY, allRecipesWidth, 32), "MS_Craft_AllRecipes".Translate()))
                OpenRecipePicker();
            float leftWidth = rect.width * 0.46f;
            float headingY = filterY + 44;
            float listY = headingY + 32;
            float listHeight = Math.Max(0, rect.height - listY - 60);
            string ordersHeading = "MS_Craft_Orders".Translate();
            Widgets.Label(new Rect(0, headingY, leftWidth, 28), ordersHeading);
            float hintX = Text.CalcSize(ordersHeading).x + 8f;
            Color headingColor = GUI.color;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(hintX, headingY, Math.Max(0, leftWidth - hintX), 28), "MS_Craft_DragToSort".Translate());
            GUI.color = headingColor;
            string workbenchesHeading = "MS_Craft_Workbenches".Translate();
            float headingX = leftWidth + 16;
            float compactX = Math.Min(headingX + Text.CalcSize(workbenchesHeading).x + 8f, rect.xMax - 28f);
            Widgets.Label(new Rect(headingX, headingY, compactX - headingX - 4f, 28), workbenchesHeading);
            if (DrawCompactModeButton(new Rect(compactX, headingY, 28, 28), CompactMode,
                (CompactMode ? "MS_Craft_CompactModeOn" : "MS_Craft_CompactModeOff").Translate()))
            { CompactMode = !CompactMode; workbenchScroll = Vector2.zero; }
            DrawOrders(new Rect(0, listY, leftWidth, listHeight),
                core.Crafting.Orders.Where(order => listFilter.Matches(order.Recipe)).ToList());
            DrawWorkbenches(new Rect(leftWidth + 16, listY, rect.width - leftWidth - 16, listHeight));
            Event current = Event.current;
            if (orderDragButton >= 0 && current.button == orderDragButton)
            {
                // The native reorder widget observes raw MouseUp even after the event is consumed.
                if (current.rawType == EventType.MouseUp)
                {
                    orderDragButton = -1;
                    if (current.type == EventType.MouseUp) current.Use();
                }
                else if (current.type == EventType.MouseDrag) current.Use();
            }
        }
        private void SharedFilterChanged()
        {
            scroll = Vector2.zero;
            workbenchScroll = Vector2.zero;
        }
        internal static bool DrawCompactModeButton(Rect rect, bool active, TaggedString tooltip)
            => DrawListModeButton(rect, CompactModeIcon, active, tooltip);
        internal static bool DrawListModeButton(Rect rect, Texture2D icon, bool active, TaggedString tooltip)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            Color previous = GUI.color;
            GUI.color = active ? Color.white : ColoredText.SubtleGrayColor;
            // Keep the hit area while displaying the PNG at 80% of its former size.
            GUI.DrawTexture(rect.ContractedBy(rect.width * 0.1f), icon);
            GUI.color = previous;
            TooltipHandler.TipRegion(rect, tooltip);
            return Widgets.ButtonInvisible(rect);
        }
        private void DrawWorkbenches(Rect outer)
        {
            Widgets.DrawMenuSection(outer);
            var tables = CraftingRecipeCatalog.AvailableWorkbenches(listFilter.WorkType)
                .Where(listFilter.MatchesWorkbench).OrderBy(uiState.WorkbenchFavoriteOrder).ToList();
            if (tables.Count == 0)
            { Widgets.Label(outer.ContractedBy(8), "MS_Craft_NoMatchingWorkbenches".Translate()); return; }
            float viewWidth = outer.width - 18;
            var heights = tables.Select(table => CompactMode
                ? Math.Max(32f, Text.CalcHeight(table.LabelCap, viewWidth - 108f) + 8f) : 64f).ToList();
            var view = new Rect(0, 0, viewWidth, heights.Sum());
            Widgets.BeginScrollView(outer, ref workbenchScroll, view);
            float y = 0;
            bool favoritesChanged = false;
            for (int index = 0; index < tables.Count; index++)
            {
                var table = tables[index];
                var row = new Rect(4, y, view.width - 8, heights[index] - (CompactMode ? 0f : 4f));
                y += heights[index];
                if (row.yMax < workbenchScroll.y || row.y > workbenchScroll.y + outer.height) continue;
                bool locked = !CraftingWorkbenchUnlocks.IsUnlocked(table);
                if (locked) Widgets.DrawBoxSolid(row, new Color(1f, 0.9f, 0.45f, 0.18f));
                Widgets.DrawHighlightIfMouseover(row);
                var pack = table.modContentPack;
                string source = pack?.ModMetaData?.ShortName ?? pack?.Name ?? "";
                if (CompactMode)
                {
                    Widgets.DefIcon(new Rect(row.x + 4, row.y + (row.height - 24f) / 2f, 24, 24), table);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(row.x + 36, row.y + 4, row.width - 100, row.height - 8), table.LabelCap);
                    Text.Anchor = TextAnchor.UpperLeft;
                    Widgets.DrawLineHorizontal(row.x, row.yMax - 1f, row.width, Widgets.SeparatorLineColor);
                }
                else
                {
                    Widgets.DefIcon(new Rect(row.x + 4, row.y + 8, 44, 44), table);
                    Widgets.Label(new Rect(row.x + 56, row.y + 3, row.width - 120, 28), table.LabelCap);
                    Widgets.Label(new Rect(row.x + 56, row.y + 31, row.width - 120, 26), source);
                }
                bool favorite = uiState.IsWorkbenchFavorite(table);
                var favoriteRect = new Rect(row.xMax - 58, row.y + (row.height - 24f) / 2f, 24, 24);
                TooltipHandler.TipRegion(favoriteRect,
                    (favorite ? "MS_Craft_UnfavoriteWorkbench" : "MS_Craft_FavoriteWorkbench").Translate());
                Texture2D favoriteIcon = Mouse.IsOver(favoriteRect) ? FavoriteHoverIcon :
                    (favorite ? FavoriteFilledIcon : FavoriteEmptyIcon);
                if (Widgets.ButtonImage(favoriteRect, favoriteIcon, Color.white, Color.white))
                {
                    uiState.ToggleWorkbenchFavorite(table);
                    favoritesChanged = true;
                }
                Widgets.InfoCardButton(row.xMax - 30, row.y + (row.height - 24f) / 2f, table);
                TooltipHandler.TipRegion(new Rect(row.x, row.y, row.width - 64, row.height),
                    table.LabelCap + "\n" + source + (locked ? "\n" + "MS_Craft_WorkbenchLockedHint".Translate().ToString() : ""));
                if (Widgets.ButtonInvisible(new Rect(row.x, row.y, row.width - 64, row.height)))
                    OpenRecipePicker(table);
            }
            Widgets.EndScrollView();
            if (favoritesChanged) workbenchScroll = Vector2.zero;
        }
        private void OpenRecipePicker(ThingDef table = null)
        {
            if (table != null && !CraftingWorkbenchUnlocks.IsUnlocked(table))
            {
                Find.WindowStack.Add(new Dialog_WorkbenchUnlock(core, table, () => OpenRecipePicker(table)));
                return;
            }
            Find.WindowStack.Add(new Dialog_CosmicRecipePicker(core, table, recipe =>
            {
                if (!CraftingRecipeCatalog.CanAdd(recipe)) return;
                core.Crafting.Add(recipe);
                scroll = Vector2.zero; refreshAt = 0;
            }));
        }
        private void DrawOrders(Rect rect, List<CraftingOrder> visible)
        {
            Widgets.DrawMenuSection(rect);
            if (visible.Count == 0)
            {
                Widgets.Label(rect.ContractedBy(8), (core.Crafting.Orders.Count == 0
                    ? "MS_Craft_NoOrders" : "MS_Craft_NoMatchingOrders").Translate());
                return;
            }
            const float step = 76f;
            Widgets.BeginGroup(rect);
            try
            {
                var viewport = rect.AtZero();
                if (Event.current.type == EventType.Repaint)
                    orderReorderGroup = ReorderableWidget.NewGroup((from, to) =>
                    { core.Crafting.Reorder(visible, from, to); refreshAt = 0; }, ReorderableDirection.Vertical, viewport, 0f);
                float contentHeight = visible.Count * step + 1f;
                float contentWidth = contentHeight > viewport.height ? rect.width - 18f : rect.width;
                var view = new Rect(0, 0, contentWidth, contentHeight);
                Widgets.BeginScrollView(viewport, ref scroll, view);
                try
                {
                    for (int i = 0; i < visible.Count; i++)
                    {
                        var order = visible[i];
                        var row = new Rect(0, i * step, view.width, step);
                        var icon = new Rect(row.x + 4, row.y + 6, 46, 46);
                        var title = new Rect(row.x + 58, row.y + 2, row.width - 120, 26);
                        if (row.yMax >= scroll.y && row.y <= scroll.y + viewport.height)
                        {
                            Color borderColor = GUI.color;
                            GUI.color = Widgets.SeparatorLineColor;
                            // Adjacent tasks share a single border pixel, closing every rectangle without gaps.
                            Widgets.DrawBox(new Rect(row.x, row.y, row.width, row.height + 1f));
                            GUI.color = borderColor;
                            DrawOrderRow(row, icon, title, order);
                        }
                        // Register offscreen rows too so native drag indexes remain stable while scrolling.
                        Event current = Event.current;
                        bool startReorder = current.type == EventType.MouseDown &&
                            (current.button == 0 || current.button == 1) && Mouse.IsOver(row);
                        if (ReorderableWidget.Reorderable(orderReorderGroup, row, useRightButton: current.button == 1))
                        {
                            Color previousColor = GUI.color;
                            GUI.color = new Color(1f, 1f, 1f, 0.5f);
                            Widgets.DrawBox(row, 2);
                            GUI.color = previousColor;
                        }
                        if (startReorder)
                        {
                            // Let row buttons handle their clicks first, then prevent window dragging.
                            orderDragButton = current.button;
                            current.Use();
                        }
                    }
                }
                finally { Widgets.EndScrollView(); }
            }
            finally { Widgets.EndGroup(); }
        }
        private void DrawOrderRow(Rect row, Rect icon, Rect title, CraftingOrder order)
        {
            Dialog_CosmicRecipePicker.DrawPreviewIcon(icon, order.Recipe.ProducedThingDef);
            Color previousColor = GUI.color;
            GUI.color = order.Suspended ? ColoredText.SubtleGrayColor : previousColor;
            Widgets.Label(title, CraftingRecipeCatalog.Label(order.Recipe).CapitalizeFirst().Truncate(title.width));
            GUI.color = previousColor;
            var delete = new Rect(row.xMax - 26, row.y + 2, 22, 22);
            var suspend = new Rect(delete.x - 28, delete.y, 22, 22);
            TooltipHandler.TipRegion(delete, "MS_Craft_Delete".Translate());
            TooltipHandler.TipRegion(suspend, (order.Suspended ? "MS_Craft_Resume" : "MS_Craft_Pause").Translate());
            if (Widgets.ButtonImage(delete, TexButton.Delete) && !ReorderableWidget.Dragging)
            { core.Crafting.Remove(order); refreshAt = 0; return; }
            if (Widgets.ButtonImage(suspend, order.Suspended ? TexButton.SpeedButtonTextures[1] : TexButton.Suspend) && !ReorderableWidget.Dragging)
            { order.Suspended = !order.Suspended; OrderChanged(order); }
            float controlsY = row.y + 32;
            var details = new Rect(row.xMax - 72, controlsY, 68, 25);
            string modeLabel = ModeLabel(order.Mode);
            float modeWidth = Math.Max(Math.Min(110f, row.width * 0.27f), Text.CalcSize(modeLabel).x + 16f);
            var mode = new Rect(details.x - 4 - modeWidth, controlsY, modeWidth, 25);
            var plus = new Rect(mode.x - 20, controlsY + 6, 14, 14);
            var minus = new Rect(plus.x - 22, plus.y, 14, 14);
            float progressRight = order.Mode == CraftingRepeatMode.Forever ? mode.x - 6 : minus.x - 6;
            var progress = new Rect(title.x, controlsY, Math.Max(0, progressRight - title.x), 25);
            string quantity;
            if (order.Mode == CraftingRepeatMode.Forever) quantity = "∞";
            else if (order.Mode == CraftingRepeatMode.RepeatCount) quantity = order.RepeatCount + "×";
            else
            {
                if (!productCounts.TryGetValue(order, out long count))
                { count = CraftingServices.Products.Count(core, order); productCounts[order] = count; }
                quantity = count + "/" + order.TargetCount;
            }
            if (!statuses.TryGetValue(order, out var status))
                statuses[order] = status = core.Map.GetComponent<MapComponent_CosmicCrafting>().Status(core, order);
            if (order.Suspended) status = "MS_Craft_Suspended".Translate();
            GUI.color = order.Suspended ? ColoredText.SubtleGrayColor : previousColor;
            Widgets.Label(progress, (quantity + "  " + status).Truncate(progress.width));
            if (order.Suspended)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(mode.x, controlsY, details.xMax - mode.x, 25), "MS_Craft_Suspended".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }
            GUI.color = previousColor;
            if (!order.Suspended)
            {
                if (Widgets.ButtonText(mode, modeLabel) && !ReorderableWidget.Dragging) ChooseOrderMode(order);
                if (Widgets.ButtonText(details, "MS_Craft_Details".Translate()) && !ReorderableWidget.Dragging)
                    Find.WindowStack.Add(new Dialog_CosmicCraftingDetails(core, order, () => refreshAt = 0));
            }
            if (order.Mode != CraftingRepeatMode.Forever)
            {
                if (Widgets.ButtonImage(minus, TexButton.Minus) && !ReorderableWidget.Dragging) AdjustOrderQuantity(order, -1);
                if (Widgets.ButtonImage(plus, TexButton.Plus) && !ReorderableWidget.Dragging) AdjustOrderQuantity(order, 1);
            }
            var statusBar = new Rect(row.x + 4, row.yMax - 11, row.width - 8, 6);
            Widgets.FillableBar(statusBar, order.Progress);
            TooltipHandler.TipRegion(statusBar, status);
            string tooltip = CraftingRecipeCatalog.Label(order.Recipe).CapitalizeFirst() + "\n" + quantity + "\n" +
                "MS_Craft_Progress".Translate(order.Progress.ToString("P0")) + "\n" +
                status + "\n" + "MS_Craft_DragOrder".Translate();
            TooltipHandler.TipRegion(title, tooltip);
            TooltipHandler.TipRegion(icon, tooltip);
            TooltipHandler.TipRegion(progress, tooltip);
        }
        private void ChooseOrderMode(CraftingOrder order)
        {
            var options = new List<FloatMenuOption>();
            foreach (CraftingRepeatMode mode in Enum.GetValues(typeof(CraftingRepeatMode)))
            {
                var chosen = mode;
                bool supported = mode != CraftingRepeatMode.TargetCount || CraftingServices.Products.CanCount(order.Recipe);
                options.Add(new FloatMenuOption(ModeLabel(mode), supported ? (Action)(() =>
                { order.Mode = chosen; order.Paused = false; OrderChanged(order); }) : null));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        internal static string ModeLabel(CraftingRepeatMode mode)
        {
            if (mode == CraftingRepeatMode.TargetCount) return "MS_Craft_Mode_TargetCount".Translate();
            BillRepeatModeDef definition = mode == CraftingRepeatMode.Forever ? BillRepeatModeDefOf.Forever : BillRepeatModeDefOf.RepeatCount;
            return definition.LabelCap;
        }
        private void AdjustOrderQuantity(CraftingOrder order, int direction)
        {
            int multiplier = GenUI.CurrentAdjustmentMultiplier();
            if (order.Mode == CraftingRepeatMode.TargetCount)
            {
                long delta = (long)direction * Math.Max(1, order.Recipe.targetCountAdjustment) * multiplier;
                int previous = order.TargetCount;
                order.TargetCount = (int)Math.Max(1L, Math.Min(999999L, order.TargetCount + delta));
                order.UnpauseAt = Math.Max(0, order.UnpauseAt + order.TargetCount - previous);
            }
            else if (order.Mode == CraftingRepeatMode.RepeatCount)
                order.RepeatCount = (int)Math.Max(0L, Math.Min(999999L, order.RepeatCount + (long)direction * multiplier));
            else return;
            OrderChanged(order);
        }
        private void OrderChanged(CraftingOrder order)
        {
            order.UnpauseAt = Math.Max(0, Math.Min(order.UnpauseAt, order.TargetCount - 1));
            order.Changed(); refreshAt = 0;
        }
    }

    internal sealed class Dialog_CosmicCraftingDetails : Window
    {
        private static Vector2? lastClosedPosition;
        private readonly Building_StorageCore core;
        private readonly CraftingOrder selected;
        private readonly Action changed;
        private readonly ThingFilterUI.UIState ingredientState = new ThingFilterUI.UIState();
        private Vector2 descriptionScroll, repeatScroll;
        private float repeatHeight = 400f;
        private readonly Dictionary<string, string> buffers = new Dictionary<string, string>();
        private readonly string description;
        private float countRefreshAt;
        private long displayedCount;
        public override Vector2 InitialSize => new Vector2(Math.Min(920, UI.screenWidth), Math.Min(740, UI.screenHeight));
        internal Dialog_CosmicCraftingDetails(Building_StorageCore core, CraftingOrder order, Action changed)
        {
            this.core = core; selected = order; this.changed = changed;
            description = RecipeDescription(order.Recipe);
            forcePause = true; absorbInputAroundWindow = true; doCloseX = true; doCloseButton = true;
            draggable = true;
            closeOnClickedOutside = true;
        }
        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            if (lastClosedPosition.HasValue) windowRect.position = lastClosedPosition.Value;
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!core.Spawned || !core.Crafting.Orders.Contains(selected)) { Close(); return; }
            core.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            Widgets.DefIcon(new Rect(0, 0, 30, 30), selected.Recipe);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(40, 0, rect.width - 70, 36), CraftingRecipeCatalog.Label(selected.Recipe).CapitalizeFirst());
            Text.Font = GameFont.Small;
            float width = (rect.width - 34f) / 3f;
            float height = rect.height - 110f;
            var left = new Rect(0, 50, width, height);
            var middle = new Rect(width + 17, 50, width, height);
            var right = new Rect((width + 17) * 2, 50, rect.width - (width + 17) * 2, height);
            DrawDescription(left);
            DrawProduction(middle);
            ThingFilterUI.DoThingFilterConfigWindow(right, ingredientState, selected.Ingredients,
                selected.Recipe.fixedIngredientFilter, forceHiddenFilters: selected.Recipe.forceHiddenSpecialFilters, map: core.Map);
        }
        public override void PostClose()
        {
            lastClosedPosition = windowRect.position;
            base.PostClose();
            // The embedded ingredient editor commits its changes when this modal closes.
            Changed();
        }
        private static string RecipeDescription(RecipeDef recipe)
        {
            var text = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(recipe.description)) text.AppendLine(recipe.description).AppendLine();
            var pack = recipe.modContentPack;
            if (pack != null) text.AppendLine("<color=#66D9E8>" + (pack.ModMetaData?.ShortName ?? pack.Name) + "</color>").AppendLine();
            text.AppendLine("WorkAmount".Translate() + ": " + recipe.WorkAmountTotal(null).ToStringWorkAmount());
            var product = recipe.ProducedThingDef;
            if (ModsConfig.BiotechActive && product != null && product.IsApparel)
                text.AppendLine("WearableBy".Translate() + ": " + product.apparel.developmentalStageFilter.ToCommaList().CapitalizeFirst());
            text.AppendLine("BillRequires".Translate() + ":");
            foreach (var ingredient in recipe.ingredients.OrderByDescending(i => i.CountFor(recipe)))
                if (!string.IsNullOrEmpty(ingredient.filter.Summary))
                    text.AppendLine(" - " + recipe.IngredientValueGetter.BillRequirementsDescription(recipe, ingredient));
            string extra = recipe.IngredientValueGetter.ExtraDescriptionLine(recipe);
            if (!string.IsNullOrEmpty(extra)) text.AppendLine().AppendLine(extra);
            if (recipe.skillRequirements != null && recipe.skillRequirements.Count > 0)
                text.AppendLine().AppendLine("MinimumSkills".Translate()).AppendLine(recipe.MinSkillString);
            text.AppendLine().Append("MS_Craft_WorkTypes".Translate(CraftingRecipeCatalog.WorkLabels(recipe)));
            return text.ToString();
        }
        private void DrawDescription(Rect rect)
        {
            Widgets.InfoCardButton(rect.x, rect.y, selected.Recipe);
            float pauseY = rect.y + 32;
            if (Widgets.ButtonText(new Rect(rect.x, pauseY, rect.width, 28),
                (selected.Suspended ? "MS_Craft_Suspended" : "MS_Craft_NotSuspended").Translate()))
            { selected.Suspended = !selected.Suspended; Changed(); }
            float footerHeight = selected.Mode == CraftingRepeatMode.TargetCount ? 124f : 0f;
            var outer = new Rect(rect.x, pauseY + 36, rect.width, rect.yMax - pauseY - 36 - footerHeight);
            var view = new Rect(0, 0, outer.width - 18, Text.CalcHeight(description, outer.width - 18) + 8);
            Widgets.BeginScrollView(outer, ref descriptionScroll, view);
            Widgets.Label(view, description);
            Widgets.EndScrollView();
            if (selected.Mode == CraftingRepeatMode.TargetCount &&
                Widgets.ButtonText(new Rect(rect.x, rect.yMax - 118, rect.width, 30), "MS_Craft_AlsoCounts".Translate()))
                Find.WindowStack.Add(new Dialog_CosmicAdditionalCounts(selected, core.Map, Changed));
        }
        private void DrawProduction(Rect rect)
        {
            const float workerHeight = 124f;
            var repeat = new Rect(rect.x, rect.y, rect.width, rect.height - workerHeight - 62);
            Widgets.DrawMenuSection(repeat);
            var inner = repeat.ContractedBy(6);
            var view = new Rect(0, 0, inner.width - 18, repeatHeight);
            Widgets.BeginScrollView(inner, ref repeatScroll, view);
            float y = 0, width = view.width;
            if (Widgets.ButtonText(new Rect(0, y, width, 28), ModeLabel(selected.Mode))) ChooseMode();
            y += 38;
            if (selected.Mode == CraftingRepeatMode.RepeatCount)
            {
                Label(ref y, width, "MS_Craft_RepeatCount".Translate());
                if (Number(ref y, width, "repeat", ref selected.RepeatCount, 0, 999999)) Changed();
            }
            if (selected.Mode == CraftingRepeatMode.TargetCount)
            {
                if (Time.realtimeSinceStartup >= countRefreshAt)
                {
                    // Show the actual total in the config dialog, rather than the execution counter's early-stop value.
                    displayedCount = CraftingServices.Products.Count(core, selected);
                    countRefreshAt = Time.realtimeSinceStartup + 0.5f;
                }
                Label(ref y, width, "MS_Craft_CurrentTarget".Translate(displayedCount, selected.TargetCount));
                string countHint = "MS_Craft_CountScope".Translate();
                if (selected.PauseWhenSatisfied && selected.Paused) countHint += "\n" + "MS_Craft_WaitingToResume".Translate();
                else if (displayedCount >= selected.TargetCount) countHint += "\n" + "MS_Craft_TargetReached".Translate();
                TooltipHandler.TipRegion(new Rect(0, y - 28, width, 28), countHint);
                int previousTarget = selected.TargetCount;
                if (Number(ref y, width, "target", ref selected.TargetCount, 1, 999999, selected.Recipe.targetCountAdjustment))
                {
                    selected.UnpauseAt = Math.Max(0, selected.UnpauseAt + selected.TargetCount - previousTarget);
                    buffers.Remove("unpause");
                    Changed();
                }
                var filters = new CraftingProductFilters(selected.Recipe);
                if (filters.IncludeEquipped) Check(ref y, width, "MS_Craft_IncludeEquipped", ref selected.IncludeEquipped);
                if (filters.IncludeTainted) Check(ref y, width, "MS_Craft_IncludeTainted", ref selected.IncludeTainted);
                Label(ref y, width, "MS_Craft_CountScope".Translate());
                if (filters.HitPoints)
                {
                    FloatRange before = selected.HitPoints;
                    Widgets.FloatRange(new Rect(0, y, width, 32), 184561, ref selected.HitPoints, 0f, 1f, "HitPoints", ToStringStyle.PercentZero);
                    selected.HitPoints.min = Mathf.Round(selected.HitPoints.min * 100f) / 100f;
                    selected.HitPoints.max = Mathf.Round(selected.HitPoints.max * 100f) / 100f;
                    if (before.min != selected.HitPoints.min || before.max != selected.HitPoints.max) Changed();
                    y += 40;
                }
                if (filters.Quality)
                {
                    QualityRange before = selected.Quality;
                    Widgets.QualityRange(new Rect(0, y, width, 32), 184562, ref selected.Quality);
                    if (before.min != selected.Quality.min || before.max != selected.Quality.max) Changed();
                    y += 40;
                }
                if (filters.AllowedStuff) Check(ref y, width, "MS_Craft_LimitStuff", ref selected.LimitToAllowedStuff);
                Check(ref y, width, "MS_Craft_PauseSatisfied", ref selected.PauseWhenSatisfied);
                if (selected.PauseWhenSatisfied)
                {
                    Label(ref y, width, "MS_Craft_Unpause".Translate());
                    if (Number(ref y, width, "unpause", ref selected.UnpauseAt, 0,
                        Math.Max(0, selected.TargetCount - 1), selected.Recipe.targetCountAdjustment)) Changed();
                }
            }
            repeatHeight = y + 8;
            Widgets.EndScrollView();
            var delivery = new Rect(rect.x, repeat.yMax + 10, rect.width, 42);
            Widgets.DrawMenuSection(delivery);
            if (Widgets.ButtonText(delivery.ContractedBy(4),
                (selected.DropProductsOnFloor ? "MS_Craft_DropProductsOnFloor" : "MS_Craft_StoreProductsInNetwork").Translate()))
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("MS_Craft_StoreProductsInNetwork".Translate(), () => selected.DropProductsOnFloor = false),
                    new FloatMenuOption("MS_Craft_DropProductsOnFloor".Translate(), () => selected.DropProductsOnFloor = true)
                }));
            TooltipHandler.TipRegion(delivery, "MS_Craft_NetworkDeliveryHint".Translate());
            DrawWorker(new Rect(rect.x, delivery.yMax + 10, rect.width, workerHeight));
        }
        private void DrawWorker(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(6);
            string workerLabel = selected.Worker != null ? selected.Worker.LabelShortCap.ToString() :
                (ModsConfig.BiotechActive && selected.Recipe.mechanitorOnlyRecipe ? "MS_Craft_AnyMechanitor" : "MS_Craft_Worker_" + selected.WorkerKind).Translate().ToString();
            if (Widgets.ButtonText(new Rect(inner.x, inner.y, inner.width, 28), workerLabel)) ChooseWorker();
            if (selected.Recipe.workSkill != null)
            {
                Widgets.Label(new Rect(inner.x, inner.y + 34, inner.width, 28), "MS_Craft_Skill".Translate(selected.Recipe.workSkill.LabelCap));
                IntRange before = selected.SkillRange;
                Widgets.IntRange(new Rect(inner.x, inner.y + 64, inner.width, 32), 184563, ref selected.SkillRange, 0, 20);
                if (before.min != selected.SkillRange.min || before.max != selected.SkillRange.max) Changed();
                TooltipHandler.TipRegion(new Rect(inner.x, inner.y + 34, inner.width, 62), "MS_Craft_NamedWorkerHint".Translate());
            }
        }
        private void ChooseMode()
        {
            var options = new List<FloatMenuOption>();
            foreach (CraftingRepeatMode mode in Enum.GetValues(typeof(CraftingRepeatMode)))
            {
                var chosen = mode;
                bool supported = mode != CraftingRepeatMode.TargetCount || CraftingServices.Products.CanCount(selected.Recipe);
                options.Add(new FloatMenuOption(ModeLabel(mode), supported ? (Action)(() =>
                { selected.Mode = chosen; selected.Paused = false; repeatScroll = Vector2.zero; Changed(); }) : null));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        private void Changed()
        {
            int previousUnpause = selected.UnpauseAt;
            selected.UnpauseAt = Math.Max(0, Math.Min(selected.UnpauseAt, selected.TargetCount - 1));
            if (previousUnpause != selected.UnpauseAt) buffers.Remove("unpause");
            selected.Changed(); changed(); countRefreshAt = 0;
        }
        private static string ModeLabel(CraftingRepeatMode mode) => Dialog_CosmicCrafting.ModeLabel(mode);
        private static void Label(ref float y, float width, string text)
        {
            float height = Math.Max(26f, Text.CalcHeight(text, width));
            Widgets.Label(new Rect(0, y, width, height), text);
            y += height + 2;
        }
        private bool Number(ref float y, float width, string key, ref int value, int min, int max, int step = 1)
        {
            if (!buffers.TryGetValue(key, out string buffer)) buffer = value.ToString();
            int before = value;
            step = Math.Max(1, step);
            long largeStep = (long)step * 10;
            const float gap = 2f;
            float buttonWidth = Math.Min(Math.Max(34f, Text.CalcSize("-" + largeStep).x + 12f), width / 5f - gap);
            float fieldWidth = width - (buttonWidth + gap) * 4;
            long delta = 0;
            if (Widgets.ButtonText(new Rect(0, y, buttonWidth, 26), "-" + largeStep)) delta = -largeStep;
            if (Widgets.ButtonText(new Rect(buttonWidth + gap, y, buttonWidth, 26), "-" + step)) delta = -step;
            float fieldX = (buttonWidth + gap) * 2;
            Widgets.TextFieldNumeric(new Rect(fieldX, y, fieldWidth, 26), ref value, ref buffer, min, max);
            if (Widgets.ButtonText(new Rect(fieldX + fieldWidth + gap, y, buttonWidth, 26), "+" + step)) delta = step;
            if (Widgets.ButtonText(new Rect(width - buttonWidth, y, buttonWidth, 26), "+" + largeStep)) delta = largeStep;
            if (delta != 0)
            {
                value = (int)Math.Max(min, Math.Min(max, value + delta * GenUI.CurrentAdjustmentMultiplier()));
                buffer = value.ToString();
            }
            buffers[key] = buffer;
            y += 34;
            return value != before;
        }
        private void Check(ref float y, float width, string key, ref bool value)
        {
            bool before = value;
            string label = key.Translate();
            float height = Math.Max(26f, Text.CalcHeight(label, width - 30));
            Widgets.CheckboxLabeled(new Rect(0, y, width, height), label, ref value);
            if (value != before) Changed();
            y += height + 2;
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
        internal WorkTypeDef WorkType => workType;
        private ModContentPack source;

        private bool MatchesWorkType(RecipeDef recipe) => recipe != null &&
            (workType == null || CraftingRecipeCatalog.HasType(recipe, workType));
        private bool MatchesSource(ModContentPack pack) => source == null ||
            string.Equals(pack?.PackageId, source.PackageId, StringComparison.OrdinalIgnoreCase);
        internal bool Matches(RecipeDef recipe) => MatchesWorkType(recipe) && MatchesSource(recipe.modContentPack);
        internal bool MatchesWorkbench(ThingDef table) => table != null && MatchesSource(table.modContentPack);

        internal float DrawTabs(Rect rect, Action changed)
        {
            var tabs = new List<TabRecord>();
            tabs.Add(new TabRecord("MS_Craft_All".Translate(), () =>
            { workType = null; changed(); }, workType == null));
            foreach (var type in CraftingRecipeCatalog.WorkTypes)
            {
                var chosen = type;
                tabs.Add(new TabRecord(type.labelShort.CapitalizeFirst(), () =>
                { workType = chosen; changed(); }, workType == type));
            }
            float minWidth = Mathf.Min(rect.width, Mathf.Max(90f, tabs.Max(t => Text.CalcSize(t.label).x + 32f)));
            float maxWidth = Mathf.Max(minWidth, 200f);
            float height = TabDrawer.GetOverflowTabHeight(rect, tabs, minWidth, maxWidth);
            TabDrawer.DrawTabsOverflow(rect, tabs, minWidth, maxWidth);
            return height;
        }

        // ShortName already falls back to Name in the game's metadata getter.
        private static string SourceLabel(ModContentPack pack) => pack.ModMetaData?.ShortName ?? pack.Name;

        internal void DrawSourceButton(Rect rect, IEnumerable<RecipeDef> sourceRecipes, Action changed)
        {
            DrawSourceChoices(rect, sourceRecipes.Where(MatchesWorkType).Select(recipe => recipe.modContentPack),
                "MS_Craft_SourceFilter", changed);
        }
        internal void DrawSharedSourceButton(Rect rect, IEnumerable<ThingDef> tables, IEnumerable<RecipeDef> recipes, Action changed)
        {
            // Either column may provide a source: existing orders can belong to mods with no workbench of their own.
            // Keep source choices independent of the currently selected work type and mod.
            var packs = tables.Select(table => table.modContentPack)
                .Concat(recipes.Where(recipe => recipe != null).Select(recipe => recipe.modContentPack));
            DrawSourceChoices(rect, packs, "MS_Craft_ModFilter", changed);
        }
        private void DrawSourceChoices(Rect rect, IEnumerable<ModContentPack> packs, string labelKey, Action changed)
        {
            string label = source != null ? SourceLabel(source) : "MS_Craft_All".Translate().ToString();
            if (!Widgets.ButtonText(rect, labelKey.Translate(label))) return;
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("MS_Craft_All".Translate(), () => { source = null; changed(); })
            };
            // Core, expansions and mods share the same content-pack identity.
            foreach (var pack in packs.Where(p => p != null).GroupBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First()).OrderBy(SourceLabel, StringComparer.CurrentCultureIgnoreCase))
            {
                var chosen = pack;
                options.Add(new FloatMenuOption(SourceLabel(pack), () => { source = chosen; changed(); }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    internal sealed class Dialog_CosmicRecipePicker : Window
    {
        private static readonly Texture2D CategoryModeIcon = ContentFinder<Texture2D>.Get("UI/Gizmos/MagicStorage/CategoryMode");
        private static Vector2? lastClosedPosition;
        private readonly Building_StorageCore core;
        private readonly Action<RecipeDef> add;
        private readonly GameComponent_CosmicCraftingUI uiState;
        private bool CompactMode
        {
            get => uiState.CompactRecipes;
            set => uiState.CompactRecipes = value;
        }
        private readonly ThingDef workbench;
        private readonly Dictionary<RecipeDef, RecipePreview> previews = new Dictionary<RecipeDef, RecipePreview>();
        private readonly CosmicCraftingListFilter listFilter = new CosmicCraftingListFilter();
        private readonly HashSet<ThingCategoryDef> collapsedCategories = new HashSet<ThingCategoryDef>();
        private bool otherRecipesCollapsed;
        private List<RecipeDef> categorizedRecipes;
        private List<CraftingRecipeCategory> categoryTree;
        private string search = "";
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(Math.Min(750, UI.screenWidth), Math.Min(750, UI.screenHeight));
        internal Dialog_CosmicRecipePicker(Building_StorageCore core, ThingDef workbench, Action<RecipeDef> add)
        {
            this.core = core; this.add = add; this.workbench = workbench;
            uiState = Current.Game.GetComponent<GameComponent_CosmicCraftingUI>();
            forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            draggable = true;
        }
        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            if (lastClosedPosition.HasValue) windowRect.position = lastClosedPosition.Value;
        }
        public override void PostClose()
        {
            lastClosedPosition = windowRect.position;
            base.PostClose();
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!core.Spawned) { Close(); return; }
            string heading = workbench != null
                ? "MS_Craft_WorkbenchRecipes".Translate(workbench.LabelCap) : "MS_Craft_AllRecipes".Translate();
            float compactX = Math.Min(Text.CalcSize(heading).x + 8f, rect.width - 90f);
            var headingRect = new Rect(0, 0, compactX - 4f, 30);
            Widgets.Label(headingRect, heading.Truncate(headingRect.width));
            TooltipHandler.TipRegion(headingRect, heading);
            if (Dialog_CosmicCrafting.DrawCompactModeButton(new Rect(compactX, 0, 28, 28), CompactMode,
                (CompactMode ? "MS_Craft_RecipeCompactModeOn" : "MS_Craft_RecipeCompactModeOff").Translate()))
            {
                CompactMode = !CompactMode;
                scroll = Vector2.zero;
            }
            if (Dialog_CosmicCrafting.DrawListModeButton(new Rect(compactX + 32f, 0, 28, 28), CategoryModeIcon,
                uiState.CategorizedRecipes, (uiState.CategorizedRecipes ? "MS_Craft_CategoryModeOn" : "MS_Craft_CategoryModeOff").Translate()))
            {
                uiState.CategorizedRecipes = !uiState.CategorizedRecipes;
                scroll = Vector2.zero;
            }
            string previousSearch = search;
            search = Widgets.TextField(new Rect(0, 38, rect.width, 30), search);
            if (search != previousSearch) scroll = Vector2.zero;
            float tabsHeight = listFilter.DrawTabs(new Rect(0, 80, rect.width, 32), () => scroll = Vector2.zero);
            var recipes = AvailableRecipes().ToList();
            var visible = recipes.Where(r => listFilter.Matches(r) &&
                CraftingRecipeCatalog.MatchesSearch(r, search)).ToList();
            float sourceY = 80 + tabsHeight + 8;
            listFilter.DrawSourceButton(new Rect(0, sourceY, rect.width, 32), recipes, () => scroll = Vector2.zero);
            float listY = sourceY + 40;
            var outer = new Rect(0, listY, rect.width, rect.height - listY - 60);
            Widgets.DrawMenuSection(outer);
            if (visible.Count == 0)
            {
                Widgets.Label(outer.ContractedBy(8), "MS_Craft_NoMatchingRecipes".Translate());
                return;
            }
            float viewWidth = outer.width - 18;
            var rows = new List<RecipeListRow>();
            if (uiState.CategorizedRecipes)
            {
                if (categorizedRecipes == null || !categorizedRecipes.SequenceEqual(visible))
                { categorizedRecipes = visible; categoryTree = CraftingRecipeCategories.Build(visible); }
                AppendCategoryRows(categoryTree, rows, 0);
            }
            else rows.AddRange(visible.Select(recipe => new RecipeListRow { Recipe = Preview(recipe) }));
            float totalHeight = 0;
            foreach (var entry in rows)
            {
                float indent = Math.Min(entry.Depth * 12f, viewWidth * 0.3f);
                float width = viewWidth - indent;
                entry.IngredientColumns = Math.Max(1, Mathf.FloorToInt((width * 0.42f - 36f) / (CompactMode ? 26f : 42f)));
                // Category headers always use the compact height, independently of recipe row style.
                float height = entry.Category != null ? 32f : RowHeight(entry.Recipe, width, entry.IngredientColumns);
                entry.Rect = new Rect(indent, totalHeight, width, height);
                totalHeight += height;
            }
            var view = new Rect(0, 0, viewWidth, totalHeight);
            Widgets.BeginScrollView(outer, ref scroll, view);
            foreach (var entry in rows)
            {
                var row = entry.Rect;
                if (row.yMax < scroll.y || row.y > scroll.y + outer.height) continue;
                if (entry.Category != null) DrawCategoryRow(row, entry.Category);
                else DrawRecipeRow(row, entry.Recipe, entry.IngredientColumns);
            }
            Widgets.EndScrollView();
        }
        private bool CategoryExpanded(CraftingRecipeCategory category) => category.Category == null
            ? !otherRecipesCollapsed : !collapsedCategories.Contains(category.Category);
        private void AppendCategoryRows(IEnumerable<CraftingRecipeCategory> nodes, List<RecipeListRow> rows, int depth)
        {
            foreach (var node in nodes)
            {
                rows.Add(new RecipeListRow { Category = node, Depth = depth });
                if (!CategoryExpanded(node)) continue;
                AppendCategoryRows(node.Children, rows, depth + 1);
                rows.AddRange(node.Recipes.Select(recipe => new RecipeListRow { Recipe = Preview(recipe), Depth = depth + 1 }));
            }
        }
        private void DrawCategoryRow(Rect row, CraftingRecipeCategory category)
        {
            bool expanded = CategoryExpanded(category);
            string label = category.Category == null ? "MS_Craft_OtherRecipes".Translate().ToString() : category.Category.LabelCap.ToString();
            Widgets.DrawHighlightIfMouseover(row);
            GUI.DrawTexture(new Rect(row.x + 6f, row.y + 8f, 16f, 16f), expanded ? TexButton.Collapse : TexButton.Reveal);
            var labelRect = new Rect(row.x + 28f, row.y, row.width - 34f, row.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label.Truncate(labelRect.width));
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(row, label);
            Widgets.DrawLineHorizontal(row.x, row.yMax - 1f, row.width, Widgets.SeparatorLineColor);
            if (!Widgets.ButtonInvisible(row)) return;
            if (category.Category == null) otherRecipesCollapsed = expanded;
            else if (expanded) collapsedCategories.Add(category.Category);
            else collapsedCategories.Remove(category.Category);
        }
        private IEnumerable<RecipeDef> AvailableRecipes() => workbench == null
            ? CraftingRecipeCatalog.Available
            : CraftingRecipeCatalog.AvailableForWorkbench(workbench, listFilter.WorkType);
        private RecipePreview Preview(RecipeDef recipe)
        {
            if (previews.TryGetValue(recipe, out var cached)) return cached;
            var entry = new RecipePreview
            {
                Recipe = recipe,
                Product = recipe.ProducedThingDef,
                Label = CraftingRecipeCatalog.Label(recipe).CapitalizeFirst(),
                Tooltip = CraftingRecipeCatalog.WorkLabels(recipe) + "\n\n" + recipe.description
            };
            foreach (var ingredient in recipe.ingredients)
            {
                // Use an ordinary-volume material first, as vanilla's ingredient summary does.
                // The displayed quantity is calculated for the actual representative shown.
                var candidates = ingredient.filter.AllowedThingDefs
                    .Where(def => (ingredient.IsFixedIngredient || recipe.fixedIngredientFilter.Allows(def)) &&
                        recipe.IngredientValueGetter.ValuePerUnitOf(def) > 0f).ToList();
                var defaults = candidates.Where(def => recipe.defaultIngredientFilter == null || recipe.defaultIngredientFilter.Allows(def)).ToList();
                var pool = defaults.Count > 0 ? defaults : candidates;
                var representative = pool.FirstOrDefault(def => !def.smallVolume) ?? pool.FirstOrDefault();
                string amount = representative != null
                    ? ingredient.CountRequiredOfFor(representative, recipe).ToString()
                    : ingredient.GetBaseCount().ToString("0.##");
                string tooltip = recipe.IngredientValueGetter.BillRequirementsDescription(recipe, ingredient);
                if (representative != null)
                    tooltip += "\n" + "MS_Craft_IngredientPreview".Translate(representative.LabelCap, amount, ingredient.filter.Summary);
                entry.Ingredients.Add(new IngredientPreview { Def = representative, Amount = amount, Tooltip = tooltip });
            }
            previews.Add(recipe, entry);
            return entry;
        }
        private float IngredientWidth(RecipePreview entry, int columns) => Math.Min(columns, entry.Ingredients.Count) * (CompactMode ? 26f : 42f);
        private float RowHeight(RecipePreview entry, float width, int columns)
        {
            float textWidth = width - (CompactMode ? 80f : 94f) - IngredientWidth(entry, columns);
            float textHeight = Text.CalcHeight(entry.Label, textWidth) + (CompactMode ? 8f : 12f);
            float ingredientHeight = Mathf.CeilToInt((float)entry.Ingredients.Count / columns) * (CompactMode ? 24f : 50f) + 8f;
            return Math.Max(CompactMode ? 32f : 58f, Math.Max(textHeight, ingredientHeight));
        }
        private void DrawRecipeRow(Rect row, RecipePreview entry, int columns)
        {
            Widgets.DrawHighlightIfMouseover(row);
            float iconSize = CompactMode ? 24f : 38f;
            var iconRect = new Rect(row.x + 5, row.y + (row.height - iconSize) / 2f, iconSize, iconSize);
            DrawPreviewIcon(iconRect, entry.Product);
            float ingredientsWidth = IngredientWidth(entry, columns);
            var labelRect = new Rect(row.x + (CompactMode ? 36f : 50f), row.y + 4,
                row.width - (CompactMode ? 80f : 94f) - ingredientsWidth, row.height - 8);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, entry.Label);
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(new Rect(row.x, row.y, labelRect.xMax - row.x, row.height), entry.Tooltip);
            float ingredientsX = row.xMax - 36f - ingredientsWidth;
            float ingredientsY = CompactMode
                ? row.y + (row.height - Mathf.CeilToInt((float)entry.Ingredients.Count / columns) * 24f) / 2f
                : row.y + 4f;
            for (int i = 0; i < entry.Ingredients.Count; i++)
            {
                var ingredient = entry.Ingredients[i];
                var cell = new Rect(ingredientsX + (i % columns) * (CompactMode ? 26f : 42f),
                    ingredientsY + (i / columns) * (CompactMode ? 24f : 50f), CompactMode ? 24f : 40f, CompactMode ? 24f : 48f);
                float ingredientIconSize = CompactMode ? 20f : 34f;
                DrawPreviewIcon(new Rect(cell.x + (CompactMode ? 2f : 3f), cell.y + (CompactMode ? 2f : 0f), ingredientIconSize, ingredientIconSize), ingredient.Def);
                if (!CompactMode)
                {
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.LowerCenter;
                    Widgets.Label(new Rect(cell.x - 1, cell.y + 29, cell.width + 2, 19), ingredient.Amount);
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                }
                TooltipHandler.TipRegion(cell, ingredient.Tooltip);
            }
            Widgets.InfoCardButton(row.xMax - 28, row.y + (row.height - 24) / 2, entry.Recipe);
            Widgets.DrawLineHorizontal(row.x + 4, row.yMax - 1, row.width - 8, new Color(1f, 1f, 1f, 0.25f));
            if (Widgets.ButtonInvisible(new Rect(row.x, row.y, row.width - 36, row.height)))
            {
                if (!AvailableRecipes().Contains(entry.Recipe)) return;
                add(entry.Recipe);
                Close();
            }
        }
        internal static void DrawPreviewIcon(Rect rect, ThingDef def)
        {
            if (def != null) Widgets.DefIcon(rect, def);
            else Widgets.DrawTextureFitted(rect, ThingCategoryDefOf.Manufactured.icon, 1f);
        }
        private sealed class RecipePreview
        {
            internal RecipeDef Recipe;
            internal ThingDef Product;
            internal string Label, Tooltip;
            internal readonly List<IngredientPreview> Ingredients = new List<IngredientPreview>();
        }
        private sealed class IngredientPreview
        {
            internal ThingDef Def;
            internal string Amount, Tooltip;
        }
        private sealed class RecipeListRow
        {
            internal CraftingRecipeCategory Category;
            internal RecipePreview Recipe;
            internal int Depth, IngredientColumns;
            internal Rect Rect;
        }
    }
}
