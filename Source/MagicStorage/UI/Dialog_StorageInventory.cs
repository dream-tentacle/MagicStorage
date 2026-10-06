using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MagicStorage
{
    // Independent trade-style UI. Only vanilla data rules and generic widgets are reused.
    [StaticConstructorOnStartup]
    public sealed class Dialog_StorageInventory : Window
    {
        private static readonly Texture2D DirectionArrow = ContentFinder<Texture2D>.Get("UI/Widgets/TradeArrow");
        private readonly Building_StorageCore core;
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private StorageNetwork network;
        private long revision = -1;
        private List<StorageItemRow> rows = new List<StorageItemRow>();
        private List<StorageInventoryEntry> entries = new List<StorageInventoryEntry>();
        private List<StorageInventoryEntry> visible = new List<StorageInventoryEntry>();
        private TransferableSorterDef primary = TransferableSorterDefOf.Category;
        private TransferableSorterDef secondary = TransferableSorterDefOf.MarketValue;
        private ThingCategoryDef category;
        private Vector2 scroll;
        private bool forbid;
        private string feedback = "";
        private const float RowHeight = 30f;
        private const float QuantityWidth = 240f;
        public override Vector2 InitialSize => new Vector2(Math.Min(1024f, UI.screenWidth), UI.screenHeight);
        public override QuickSearchWidget CommonSearchWidget => search;

        public Dialog_StorageInventory(Building_StorageCore core)
        {
            this.core = core;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            soundAppear = SoundDefOf.CommsWindow_Open;
            soundClose = SoundDefOf.CommsWindow_Close;
            commonSearchWidgetOffset.x += 18f;
            commonSearchWidgetOffset.y -= 18f;
        }

        public override void PostOpen() { base.PostOpen(); Refresh(); }
        public override void Notify_CommonSearchChanged() { ApplyFilter(); }

        protected override Rect QuickSearchWidgetRect(Rect winRect, Rect inRect)
        {
            // At narrower UI resolutions, keep search above the centered button group.
            if (inRect.width < 950f) return new Rect(inRect.x, inRect.y + inRect.height - 86f, 200f, 24f);
            return base.QuickSearchWidgetRect(winRect, inRect);
        }

        private void Refresh()
        {
            if (core.Spawned) core.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            network = core.Network;
            revision = network?.Revision ?? -1;
            rows = StorageWithdrawal.Snapshot(network);
            entries = rows.Select(row => new StorageInventoryEntry(row)).ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            visible = entries.Where(entry => search.filter.Matches(entry.Label) &&
                (category == null || InCategory(entry.ThingDef, category)))
                .OrderBy(entry => entry, primary.Comparer)
                .ThenBy(entry => entry, secondary.Comparer)
                .ThenBy(entry => TransferableUIUtility.DefaultListOrderPriority(entry.ThingDef))
                .ThenBy(entry => entry.ThingDef.label)
                .ThenBy(entry => entry.AnyThing.TryGetQuality(out var quality) ? (int)quality : -1)
                .ThenBy(entry => entry.AnyThing.HitPoints).ToList();
            search.noResultsMatched = visible.Count == 0;
            scroll = Vector2.zero;
        }

        private static bool InCategory(ThingDef def, ThingCategoryDef wanted)
        {
            if (def.thingCategories == null) return false;
            foreach (var leaf in def.thingCategories)
                for (var current = leaf; current != null; current = current.parent)
                    if (current == wanted) return true;
            return false;
        }

        private void ChooseCategory()
        {
            var categories = new HashSet<ThingCategoryDef>();
            foreach (var entry in entries)
                if (entry.ThingDef.thingCategories != null)
                    foreach (var leaf in entry.ThingDef.thingCategories)
                        for (var current = leaf; current != null; current = current.parent) categories.Add(current);
            var options = new List<FloatMenuOption> { new FloatMenuOption("MS_UI_AllCategories".Translate(), () => { category = null; ApplyFilter(); }) };
            foreach (var entry in categories.OrderBy(c => c.label))
            {
                var selected = entry;
                options.Add(new FloatMenuOption(entry.LabelCap, () => { category = selected; ApplyFilter(); }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ChooseSorter(bool first)
        {
            var options = new List<FloatMenuOption>();
            foreach (var def in DefDatabase<TransferableSorterDef>.AllDefsListForReading)
            {
                var selected = def;
                options.Add(new FloatMenuOption(def.LabelCap, () =>
                {
                    if (first) primary = selected; else secondary = selected;
                    ApplyFilter();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!core.Spawned || core.Destroyed || core.Faction != Faction.OfPlayer) { Close(); return; }
            if (network != core.Network || revision != (core.Network?.Revision ?? -1))
            { Refresh(); feedback = "MS_UI_Refreshed".Translate(); }
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                DrawHeader(rect);
                DrawList(new Rect(0f, 88f, rect.width, Math.Max(0f, rect.height - 218f)));
                DrawFooter(rect);
            }
            finally { Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = oldWrap; GUI.color = oldColor; }
        }

        private void DrawHeader(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(0, 0, 60, 27), "SortBy".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(new Rect(70, 0, 130, 27), primary.LabelCap.ToString().Truncate(128))) ChooseSorter(true);
            if (Widgets.ButtonText(new Rect(210, 0, 130, 27), secondary.LabelCap.ToString().Truncate(128))) ChooseSorter(false);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(350, 0, rect.width - 350, 30), "MS_UI_Title".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(new Rect(0, 30, 195, 25), (category?.LabelCap.ToString() ?? "MS_UI_AllCategories".Translate().ToString()).Truncate(190))) ChooseCategory();
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(205, 29, rect.width - 205, 27), "MS_UI_Capacity".Translate(network?.UnitCount ?? 0, network?.UsedSlots ?? 0, network?.SlotCapacity ?? 0));
            float quantityX = rect.width - 16f - 175f - QuantityWidth;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(80, 59, Math.Max(0, quantityX - 175 - 80), 26), "MS_UI_Item".Translate());
            Widgets.Label(new Rect(quantityX - 175, 59, 75, 26), "MS_UI_Total".Translate());
            Widgets.Label(new Rect(quantityX - 100, 59, 100, 26), "MS_UI_Available".Translate());
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(quantityX, 59, QuantityWidth, 26), "MS_UI_NegativeWithdraw".Translate());
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(quantityX + QuantityWidth, 59, 95, 26), "MS_UI_SelectedColumn".Translate());
            Widgets.Label(new Rect(quantityX + QuantityWidth + 100, 59, 75, 26), "MS_UI_Remaining".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(0, 86, rect.width - 16);
        }

        private void DrawList(Rect viewport)
        {
            Rect content = new Rect(0, 0, viewport.width - 16, 6 + visible.Count * RowHeight);
            Widgets.BeginScrollView(viewport, ref scroll, content);
            try
            {
                int first = Math.Max(0, (int)((scroll.y - 6) / RowHeight));
                int last = Math.Min(visible.Count, (int)((scroll.y + viewport.height) / RowHeight) + 1);
                for (int i = first; i < last; i++) DrawRow(new Rect(0, 6 + i * RowHeight, content.width, RowHeight), visible[i], i);
                if (visible.Count == 0) Widgets.Label(new Rect(12, 12, content.width - 24, 40), "MS_UI_Empty".Translate());
            }
            finally { Widgets.EndScrollView(); }
        }

        private void DrawRow(Rect rect, StorageInventoryEntry entry, int index)
        {
            if ((index & 1) == 1) Widgets.DrawLightHighlight(rect);
            Widgets.BeginGroup(rect);
            try
            {
                StorageItemRow row = entry.Row;
                float quantityX = rect.width - 175f - QuantityWidth;
                float infoWidth = quantityX - 175f;
                Rect info = new Rect(0, 0, infoWidth, RowHeight);
                Widgets.DrawHighlightIfMouseover(info);
                Widgets.ThingIcon(new Rect(0, 0, 27, 27), entry.AnyThing);
                Widgets.InfoCardButton(40, 0, entry.AnyThing);
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                Widgets.Label(new Rect(80, 0, Math.Max(0, infoWidth - 80), RowHeight), entry.LabelCap);
                Text.WordWrap = true;
                TooltipHandler.TipRegion(info, new TipSignal(entry.Description, row.Representative.thingIDNumber));
                DrawCount(new Rect(infoWidth, 0, 75, RowHeight), row.Total, "MS_UI_TotalTip".Translate(), false);
                DrawCount(new Rect(infoWidth + 75, 0, 100, RowHeight), row.Available, "MS_UI_AvailableTip".Translate(), false);
                DrawQuantity(new Rect(quantityX, 0, QuantityWidth, RowHeight), row);
                DrawCount(new Rect(quantityX + QuantityWidth, 0, 100, RowHeight), row.Requested, "MS_UI_SelectedTip".Translate(), true);
                DrawCount(new Rect(rect.width - 75, 0, 75, RowHeight), row.Total - row.Requested, "MS_UI_RemainingTip".Translate(), true);
            }
            finally { Text.Anchor = TextAnchor.UpperLeft; Widgets.EndGroup(); }
        }

        private static void DrawCount(Rect rect, long count, string tip, bool right)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            Text.Anchor = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            Rect label = rect; label.xMin += 5; label.xMax -= 5;
            Widgets.Label(label, count.ToString());
            TooltipHandler.TipRegion(rect, tip + "\n" + count);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawQuantity(Rect rect, StorageItemRow row)
        {
            Rect amount = new Rect(rect.center.x - 45, rect.center.y - 12.5f, 90, 25).Rounded();
            bool interactive = core.CanWork && row.Available > 0;
            if (interactive)
            {
                Rect field = amount.ContractedBy(2); field.xMin += 16; field.xMax -= 15;
                GUI.SetNextControlName("MS_Withdraw_" + row.Representative.thingIDNumber);
                string next = Widgets.TextField(field, row.Buffer);
                if (next != row.Buffer) row.EditRequested(next);
            }
            else
            {
                GUI.color = row.Requested == 0 ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(amount, (-row.Requested).ToString());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            int step = GenUI.CurrentAdjustmentMultiplier();
            bool single = row.Available == 1;
            if (interactive && row.Requested > 0)
            {
                Rect left = new Rect(amount.x - (single ? 60 : 30), rect.y, single ? 60 : 30, rect.height);
                if (Widgets.ButtonText(left, "<")) { row.SetRequested(row.Requested - step); SoundDefOf.Tick_High.PlayOneShotOnCamera(); }
                if (!single && Widgets.ButtonText(new Rect(amount.x - 60, rect.y, 30, rect.height), "<<"))
                { row.SetRequested(0); SoundDefOf.Tick_High.PlayOneShotOnCamera(); }
            }
            if (interactive && row.Requested < row.Available)
            {
                if (Widgets.ButtonText(new Rect(amount.xMax, rect.y, single ? 60 : 30, rect.height), ">"))
                { row.SetRequested(row.Requested + Math.Min(step, row.Available - row.Requested)); SoundDefOf.Tick_Low.PlayOneShotOnCamera(); }
                if (!single && Widgets.ButtonText(new Rect(amount.xMax + 30, rect.y, 30, rect.height), ">>"))
                { row.SetRequested(row.Available); SoundDefOf.Tick_Low.PlayOneShotOnCamera(); }
            }
            if (row.Requested != 0)
                GUI.DrawTexture(new Rect(amount.center.x - DirectionArrow.width / 2f, amount.center.y - DirectionArrow.height / 2f,
                    DirectionArrow.width, DirectionArrow.height), DirectionArrow);
            TooltipHandler.TipRegion(rect, "MS_UI_QuantityTip".Translate());
        }

        private void DrawFooter(Rect rect)
        {
            long selected = rows.Sum(row => row.Requested);
            float y = rect.height - 126;
            Widgets.Label(new Rect(0, y, rect.width - 295, 26), "MS_UI_Selected".Translate(rows.Count(row => row.Requested > 0), selected));
            Widgets.CheckboxLabeled(new Rect(rect.width - 285, y, 285, 26), "MS_UI_Forbid".Translate(), ref forbid);
            TooltipHandler.TipRegion(new Rect(rect.width - 285, y, 285, 26), "MS_UI_ForbidTip".Translate());
            string status = !core.CanWork ? StorageNetwork.StatusLabel(network) : feedback.Length > 0 ? feedback : "MS_UI_Hint".Translate().ToString();
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.width < 950 ? 210 : 0, y + 28, rect.width - (rect.width < 950 ? 210 : 0), 37), status);
            Text.Font = GameFont.Small;
            float center = rect.width / 2;
            Rect accept = new Rect(center - 80, rect.height - 55, 160, 40);
            if (Widgets.ButtonText(new Rect(accept.x - 170, accept.y, 160, 40), "ResetButton".Translate()))
            {
                foreach (var row in rows) row.SetRequested(0);
                feedback = ""; ApplyFilter(); GUI.FocusControl(null); SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && core.CanWork;
            bool confirm;
            try { confirm = Widgets.ButtonText(accept, "MS_UI_Withdraw".Translate()); }
            finally { GUI.enabled = enabled; }
            if (confirm)
            {
                if (selected == 0) Close();
                else
                {
                    var result = StorageWithdrawal.Execute(core, network, rows, forbid);
                    string message = "MS_UI_Result".Translate(result.Withdrawn - result.Pending, result.Pending, result.Requested - result.Withdrawn);
                    if (result.Withdrawn == result.Requested)
                    {
                        if (result.Pending > 0) Messages.Message(message, core, MessageTypeDefOf.NeutralEvent, false);
                        SoundDefOf.ExecuteTrade.PlayOneShotOnCamera();
                        Close(false);
                    }
                    else { Refresh(); feedback = message; SoundDefOf.ClickReject.PlayOneShotOnCamera(); }
                }
                Event.current.Use();
            }
            if (Widgets.ButtonText(new Rect(accept.xMax + 10, accept.y, 160, 40), "CancelButton".Translate()))
            { Close(); Event.current.Use(); }
        }
    }
}
