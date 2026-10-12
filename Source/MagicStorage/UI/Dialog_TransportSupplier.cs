using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MagicStorage
{
    public sealed class Dialog_TransportSupplier : Window
    {
        private const float RowHeight = 30f;
        private const float QuantityWidth = 230f;
        private const float SideWidth = 155f;
        private readonly Building_TransportSupplier supplier;
        private readonly StorageQuantityDrag quantityDrag = new StorageQuantityDrag();
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private StorageNetwork network;
        private long revision;
        private double capacity;
        private List<StorageTransportRow> rows = new List<StorageTransportRow>();
        private List<StorageTransportRow> visible = new List<StorageTransportRow>();
        private TransferableSorterDef primary = TransferableSorterDefOf.Category;
        private TransferableSorterDef secondary = TransferableSorterDefOf.MarketValue;
        private ThingCategoryDef category;
        private Vector2 scroll;
        private string feedback = "";
        public override Vector2 InitialSize => new Vector2(Math.Min(1100f, UI.screenWidth), UI.screenHeight);
        public override QuickSearchWidget CommonSearchWidget => search;

        public Dialog_TransportSupplier(Building_TransportSupplier supplier)
        {
            this.supplier = supplier;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            soundAppear = SoundDefOf.CommsWindow_Open;
            soundClose = SoundDefOf.CommsWindow_Close;
            commonSearchWidgetOffset.x += 18f;
            commonSearchWidgetOffset.y -= 18f;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            capacity = supplier.TransferCapacity;
            Refresh();
        }
        public override void Notify_CommonSearchChanged() => ApplyFilter();
        public override void PostClose() { quantityDrag.Reset(); base.PostClose(); }
        protected override Rect QuickSearchWidgetRect(Rect winRect, Rect inRect)
        {
            if (inRect.width < 950f) return new Rect(inRect.x, inRect.y + inRect.height - 86f, 200f, 24f);
            return base.QuickSearchWidgetRect(winRect, inRect);
        }

        private void Refresh()
        {
            if (supplier.Spawned) supplier.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            network = supplier.Network;
            revision = network?.Revision ?? -1;
            rows = StorageTransportExchange.Snapshot(supplier, network);
            ApplyFilter();
        }
        private void ApplyFilter()
        {
            quantityDrag.Reset();
            visible = rows.Where(row => search.filter.Matches(row.Label) && (category == null || InCategory(row.ThingDef, category)))
                .OrderBy(row => row, primary.Comparer).ThenBy(row => row, secondary.Comparer)
                .ThenBy(row => TransferableUIUtility.DefaultListOrderPriority(row.ThingDef))
                .ThenBy(row => row.ThingDef.label).ThenBy(row => row.AnyThing.HitPoints).ToList();
            search.noResultsMatched = visible.Count == 0;
            scroll = Vector2.zero;
        }
        private static bool InCategory(ThingDef def, ThingCategoryDef wanted)
        {
            if (def.thingCategories == null) return false;
            foreach (var leaf in def.thingCategories)
                for (var current = leaf; current != null; current = current.parent) if (current == wanted) return true;
            return false;
        }
        private void ChooseCategory()
        {
            var categories = new HashSet<ThingCategoryDef>();
            foreach (var row in rows)
                if (row.ThingDef.thingCategories != null)
                    foreach (var leaf in row.ThingDef.thingCategories)
                        for (var current = leaf; current != null; current = current.parent) categories.Add(current);
            var options = new List<FloatMenuOption> { new FloatMenuOption("MS_UI_AllCategories".Translate(), () => { category = null; ApplyFilter(); }) };
            foreach (var value in categories.OrderBy(c => c.label))
            {
                var selected = value;
                options.Add(new FloatMenuOption(selected.LabelCap, () => { category = selected; ApplyFilter(); }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        private void ChooseSorter(bool first)
        {
            var options = new List<FloatMenuOption>();
            foreach (var value in DefDatabase<TransferableSorterDef>.AllDefsListForReading)
            {
                var selected = value;
                options.Add(new FloatMenuOption(selected.LabelCap, () => { if (first) primary = selected; else secondary = selected; ApplyFilter(); }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!supplier.Spawned || supplier.Destroyed || supplier.Faction != Faction.OfPlayer || supplier.DirectSupply) { Close(); return; }
            if (network != supplier.Network || revision != (supplier.Network?.Revision ?? -1))
            { Refresh(); feedback = "MS_Transport_Refreshed".Translate(); }
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            quantityDrag.BeginGUI(supplier.CanWork);
            try
            {
                Text.Font = GameFont.Small;
                DrawHeader(rect);
                DrawList(new Rect(0, 112, rect.width, Math.Max(0, rect.height - 242)));
                DrawFooter(rect);
            }
            finally { quantityDrag.EndGUI(); Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = oldWrap; GUI.color = oldColor; }
        }

        private void DrawHeader(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(0, 0, 60, 27), "SortBy".Translate());
            if (Widgets.ButtonText(new Rect(70, 0, 130, 27), primary.LabelCap.ToString().Truncate(128))) ChooseSorter(true);
            if (Widgets.ButtonText(new Rect(210, 0, 130, 27), secondary.LabelCap.ToString().Truncate(128))) ChooseSorter(false);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(350, 0, rect.width - 350, 30), "MS_Transport_Title".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(new Rect(0, 32, 195, 25), (category?.LabelCap.ToString() ?? "MS_UI_AllCategories".Translate().ToString()).Truncate(190))) ChooseCategory();
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(205, 30, rect.width - 205, 30), "MS_Transport_CapacityRule".Translate(capacity.ToString("0.##")));
            float quantityX = rect.width - 16 - SideWidth - QuantityWidth;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(48, 67, Math.Max(0, quantityX - SideWidth - 48), 36), "MS_UI_Item".Translate());
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(quantityX - SideWidth, 64, SideWidth, 40), "MS_Transport_NetworkColumn".Translate());
            Widgets.Label(new Rect(quantityX, 64, QuantityWidth, 40), "MS_Transport_ChangeColumn".Translate());
            Widgets.Label(new Rect(quantityX + QuantityWidth, 64, SideWidth, 40), "MS_Transport_SupplyColumn".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawLineHorizontal(0, 110, rect.width - 16);
        }

        private void DrawList(Rect viewport)
        {
            quantityDrag.SetViewport(viewport);
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
        private void DrawRow(Rect rect, StorageTransportRow row, int index)
        {
            if ((index & 1) == 1) Widgets.DrawLightHighlight(rect);
            Widgets.BeginGroup(rect);
            try
            {
                float quantityX = rect.width - SideWidth - QuantityWidth;
                float infoWidth = quantityX - SideWidth;
                Widgets.ThingIcon(new Rect(0, 0, 27, 27), row.AnyThing);
                Widgets.InfoCardButton(30, 0, row.AnyThing);
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                Widgets.Label(new Rect(58, 0, Math.Max(0, infoWidth - 58), RowHeight), row.Label);
                Text.WordWrap = true;
                TooltipHandler.TipRegion(new Rect(0, 0, infoWidth, RowHeight), row.AnyThing.DescriptionDetailed);
                DrawCount(new Rect(infoWidth, 0, SideWidth, RowHeight), row.NetworkCount, row.NetworkAfter,
                    "MS_Transport_Available".Translate(row.Available));
                DrawQuantity(new Rect(quantityX, 0, QuantityWidth, RowHeight), row);
                DrawCount(new Rect(quantityX + QuantityWidth, 0, SideWidth, RowHeight), row.SupplyCount, row.SupplyAfter,
                    "MS_Transport_SupplyTip".Translate());
            }
            finally { Text.Anchor = TextAnchor.UpperLeft; Widgets.EndGroup(); }
        }
        private static void DrawCount(Rect rect, long before, long after, string tip)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, before == after ? before.ToString() : before + " → " + after);
            TooltipHandler.TipRegion(rect, tip);
            Text.Anchor = TextAnchor.UpperLeft;
        }
        private void DrawQuantity(Rect rect, StorageTransportRow row)
        {
            Rect amount = new Rect(rect.center.x - 42, rect.center.y - 12.5f, 84, 25).Rounded();
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && supplier.CanWork;
            try
            {
                GUI.SetNextControlName("MS_Transport_" + row.Representative.thingIDNumber);
                string value = Widgets.TextField(amount, row.Buffer);
                if (value != row.Buffer) row.EditChange(value);
                int step = GenUI.CurrentAdjustmentMultiplier();
                if (row.Change > -row.SupplyCount)
                {
                    quantityDrag.ButtonText(new Rect(amount.x - 58, rect.y, 29, rect.height), "<<", row.Representative.thingIDNumber,
                        () => row.SetChange(-row.SupplyCount));
                    quantityDrag.ButtonText(new Rect(amount.x - 29, rect.y, 29, rect.height), "<", row.Representative.thingIDNumber,
                        () => row.SetChange(row.Change - Math.Min(step, row.Change + row.SupplyCount)));
                }
                if (row.Change < row.Available)
                {
                    quantityDrag.ButtonText(new Rect(amount.xMax, rect.y, 29, rect.height), ">", row.Representative.thingIDNumber,
                        () => row.SetChange(row.Change + Math.Min(step, row.Available - row.Change)));
                    long massLimit = StorageTransportExchange.MassLimitChange(supplier, network, rows, row, capacity);
                    bool stopAtMass = row.Change < massLimit && massLimit < row.Available;
                    quantityDrag.ButtonText(new Rect(amount.xMax + 29, rect.y, 29, rect.height), stopAtMass ? ">M" : ">>",
                        row.Representative.thingIDNumber,
                        () => row.SetChange(stopAtMass ? StorageTransportExchange.MassLimitChange(supplier, network, rows, row, capacity) : row.Available));
                }
            }
            finally { GUI.enabled = enabled; }
            TooltipHandler.TipRegion(rect, "MS_Transport_QuantityTip".Translate());
        }

        private void DrawFooter(Rect rect)
        {
            double mass = StorageTransportExchange.ProjectedMass(supplier, network, rows);
            bool withinCapacity = mass <= capacity + StorageTransportExchange.MassTolerance;
            float y = rect.height - 126;
            Color color = GUI.color;
            if (!withinCapacity) GUI.color = Color.red;
            Widgets.Label(new Rect(0, y, rect.width, 26), "MS_Transport_Mass".Translate(
                supplier.StoredMass.ToString("0.##"), mass.ToString("0.##"), capacity.ToString("0.##")));
            GUI.color = color;
            string status = !supplier.CanWork ? StorageNetwork.StatusLabel(network) : !withinCapacity ? "MS_Transport_Overweight".Translate().ToString() :
                feedback.Length > 0 ? feedback : "MS_Transport_Hint".Translate().ToString();
            Text.Font = GameFont.Tiny;
            float statusX = rect.width < 950 ? 210 : 0;
            Widgets.Label(new Rect(statusX, y + 28, rect.width - statusX, 37), status);
            Text.Font = GameFont.Small;
            Rect accept = new Rect(rect.width / 2 - 80, rect.height - 55, 160, 40);
            if (Widgets.ButtonText(new Rect(accept.x - 170, accept.y, 160, 40), "ResetButton".Translate()))
            { foreach (var row in rows) row.SetChange(0); feedback = ""; ApplyFilter(); GUI.FocusControl(null); }
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && supplier.CanWork && withinCapacity;
            bool confirm;
            try { confirm = Widgets.ButtonText(accept, "AcceptButton".Translate()); }
            finally { GUI.enabled = enabled; }
            if (confirm)
            {
                if (!rows.Any(row => row.Change != 0)) Close();
                else if (StorageTransportExchange.Execute(supplier, network, rows, capacity, out string error))
                { SoundDefOf.ExecuteTrade.PlayOneShotOnCamera(); Close(false); }
                else { Refresh(); feedback = error.Translate(); SoundDefOf.ClickReject.PlayOneShotOnCamera(); }
                Event.current.Use();
            }
            if (Widgets.ButtonText(new Rect(accept.xMax + 10, accept.y, 160, 40), "CancelButton".Translate()))
            { Close(); Event.current.Use(); }
        }
    }
}
