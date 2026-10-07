using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Dialog_StorageSupplyShelf : Window
    {
        private readonly Building_StorageSupplyShelf shelf;
        private readonly string[] limitBuffers = new string[Building_StorageSupplyShelf.SlotCount];
        private readonly ThingDef[] displayedDefs = new ThingDef[Building_StorageSupplyShelf.SlotCount];
        public override Vector2 InitialSize => new Vector2(620, 630);
        public Dialog_StorageSupplyShelf(Building_StorageSupplyShelf shelf)
        {
            this.shelf = shelf;
            doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!shelf.Spawned) { Close(); return; }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 30, 36), "MS_Shelf_Title".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 42, rect.width, 58), "MS_Shelf_ConfigureDesc".Translate());
            StorageSettings settings = shelf.GetStoreSettings();
            Rect priorityRect = new Rect(0, 108, rect.width, 32);
            if (Widgets.ButtonText(priorityRect, "Priority".Translate() + ": " + settings.Priority.Label().CapitalizeFirst()))
            {
                var options = new List<FloatMenuOption>();
                foreach (StoragePriority priority in Enum.GetValues(typeof(StoragePriority)))
                {
                    if (priority == StoragePriority.Unstored) continue;
                    StoragePriority selected = priority;
                    options.Add(new FloatMenuOption(selected.Label().CapitalizeFirst(), () => settings.Priority = selected));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(priorityRect, "MS_Shelf_PriorityHint".Translate());
            Widgets.Label(new Rect(0, 148, rect.width, 52), "MS_Shelf_PriorityHint".Translate());
            var stocks = new List<Thing>(shelf.GetSlotGroup().HeldThings);
            for (int i = 0; i < Building_StorageSupplyShelf.SlotCount; i++)
            {
                int slot = i;
                ThingDef itemDef = shelf.SelectedDef(slot);
                Rect row = new Rect(0, 208 + i * 105, rect.width, 97);
                Widgets.DrawMenuSection(row);
                Widgets.Label(new Rect(row.x + 10, row.y + 6, row.width - 214, 25), "MS_Shelf_Slot".Translate(slot + 1));
                if (itemDef != null) Widgets.DefIcon(new Rect(10, row.y + 40, 32, 32), itemDef);
                string label = itemDef?.LabelCap.ToString() ?? "MS_Shelf_Empty".Translate().ToString();
                Widgets.Label(new Rect(50, row.y + 33, row.width - 260, 28), label);
                if (itemDef != null)
                {
                    int limit = shelf.RestockLimit(slot);
                    if (displayedDefs[slot] != itemDef) limitBuffers[slot] = limit.ToString();
                    displayedDefs[slot] = itemDef;
                    Widgets.Label(new Rect(row.width - 194, row.y + 6, 100, 25), "MS_Shelf_Limit".Translate());
                    Rect limitRect = new Rect(row.width - 90, row.y + 4, 80, 26);
                    Widgets.TextFieldNumeric(limitRect, ref limit, ref limitBuffers[slot], 0, itemDef.stackLimit);
                    shelf.SetRestockLimit(slot, limit);
                    TooltipHandler.TipRegion(limitRect, "MS_Shelf_LimitTip".Translate(itemDef.stackLimit));
                    int count = stocks.Where(t => t.def == itemDef).Sum(t => t.stackCount);
                    Widgets.Label(new Rect(50, row.y + 62, row.width - 260, 25),
                        "MS_Shelf_Stock".Translate(count, shelf.RestockLimit(slot)));
                    if (Widgets.ButtonText(new Rect(row.width - 194, row.y + 74, 184, 22), "MS_Shelf_Filter".Translate()))
                        Find.WindowStack.Add(new Dialog_StorageShelfFilter(shelf, slot));
                }
                else { displayedDefs[slot] = null; limitBuffers[slot] = null; }
                if (Widgets.ButtonText(new Rect(row.width - 194, row.y + 40, 96, 32), "MS_Shelf_Select".Translate()))
                    Find.WindowStack.Add(new Dialog_StorageShelfItemPicker(shelf, slot));
                if (Widgets.ButtonText(new Rect(row.width - 90, row.y + 40, 80, 32), "MS_Shelf_Clear".Translate()))
                    shelf.SetSelection(slot, null);
            }
        }
    }

    // Native category/search rules, with clickable single-choice rows in place of checkboxes.
    public sealed class Dialog_StorageShelfItemPicker : Window
    {
        private const float RowHeight = 26;
        private readonly Building_StorageSupplyShelf shelf;
        private readonly int slot;
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private readonly HashSet<ThingCategoryDef> opened = new HashSet<ThingCategoryDef>();
        private readonly Dictionary<ThingCategoryDef, bool> matchingCategories = new Dictionary<ThingCategoryDef, bool>();
        private readonly List<Row> rows = new List<Row>();
        private Vector2 scroll;
        private sealed class Row
        {
            internal ThingCategoryDef Category;
            internal ThingDef Item;
            internal int Indent;
            internal bool Expanded;
        }
        public override Vector2 InitialSize => new Vector2(Math.Min(650, UI.screenWidth), Math.Min(760, UI.screenHeight));
        public Dialog_StorageShelfItemPicker(Building_StorageSupplyShelf shelf, int slot)
        {
            this.shelf = shelf; this.slot = slot;
            doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
            ThingDef selected = shelf.SelectedDef(slot);
            if (selected?.thingCategories != null)
                foreach (ThingCategoryDef category in selected.thingCategories)
                {
                    opened.Add(category);
                    foreach (ThingCategoryDef parent in category.Parents) opened.Add(parent);
                }
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!shelf.Spawned) { Close(); return; }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 30, 36), "MS_Shelf_PickTitle".Translate(slot + 1));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 40, rect.width, 44), "MS_Shelf_PickHint".Translate());
            search.OnGUI(new Rect(4, 91, rect.width - 8, 24), () => scroll = Vector2.zero);
            matchingCategories.Clear(); rows.Clear();
            ThingCategoryDef root = shelf.GetParentStoreSettings().filter.DisplayRootCategory.catDef;
            AddChildren(root, 0, false);
            search.noResultsMatched = !rows.Any(r => r.Item != null);
            Rect listRect = new Rect(0, 125, rect.width, rect.height - 185);
            Widgets.DrawMenuSection(listRect);
            Rect view = new Rect(0, 0, listRect.width - 20, rows.Count * RowHeight + 8);
            Widgets.BeginScrollView(listRect.ContractedBy(4), ref scroll, view);
            for (int i = 0; i < rows.Count; i++)
            {
                if ((i + 1) * RowHeight < scroll.y || i * RowHeight > scroll.y + listRect.height) continue;
                DrawRow(rows[i], new Rect(0, i * RowHeight, view.width, RowHeight));
            }
            Widgets.EndScrollView();
        }
        private bool Visible(ThingDef itemDef) => shelf.CanSelect(itemDef) && !Find.HiddenItemsManager.Hidden(itemDef);
        private bool CategoryMatches(ThingCategoryDef category)
        {
            if (matchingCategories.TryGetValue(category, out bool value)) return value;
            value = (search.filter.Matches(category.label) && category.DescendantThingDefs.Any(Visible)) ||
                category.childThingDefs.Any(d => Visible(d) && search.filter.Matches(d)) ||
                category.childCategories.Any(CategoryMatches);
            matchingCategories.Add(category, value);
            return value;
        }
        private void AddChildren(ThingCategoryDef category, int indent, bool parentMatched)
        {
            foreach (ThingCategoryDef child in category.childCategories)
            {
                if (!child.DescendantThingDefs.Any(Visible) || (!parentMatched && !CategoryMatches(child))) continue;
                bool expanded = search.filter.Active || opened.Contains(child);
                rows.Add(new Row { Category = child, Indent = indent, Expanded = expanded });
                if (expanded) AddChildren(child, indent + 1,
                    parentMatched || (search.filter.Active && search.filter.Matches(child.label)));
            }
            foreach (ThingDef itemDef in category.SortedChildThingDefs)
                if (Visible(itemDef) && (parentMatched || search.filter.Matches(itemDef)))
                    rows.Add(new Row { Item = itemDef, Indent = indent });
        }
        private void DrawRow(Row row, Rect rect)
        {
            float indent = row.Indent * 18;
            if (row.Category != null)
            {
                if (Widgets.ButtonImage(new Rect(indent, rect.y + 4, 18, 18), row.Expanded ? TexButton.Collapse : TexButton.Reveal))
                { if (!opened.Remove(row.Category)) opened.Add(row.Category); }
                Widgets.Label(new Rect(indent + 24, rect.y, rect.width - indent - 24, rect.height), row.Category.LabelCap);
                TooltipHandler.TipRegion(rect, row.Category.description);
                return;
            }
            bool elsewhere = Enumerable.Range(0, Building_StorageSupplyShelf.SlotCount)
                .Any(i => i != slot && shelf.SelectedDef(i) == row.Item);
            if (shelf.SelectedDef(slot) == row.Item) Widgets.DrawHighlightSelected(rect);
            else if (!elsewhere) Widgets.DrawHighlightIfMouseover(rect);
            Color previous = GUI.color;
            if (elsewhere) GUI.color = Color.gray;
            Widgets.DefIcon(new Rect(indent + 4, rect.y + 3, 20, 20), row.Item);
            Widgets.Label(new Rect(indent + 30, rect.y, rect.width - indent - 30, rect.height), row.Item.LabelCap);
            GUI.color = previous;
            TooltipHandler.TipRegion(rect, row.Item.DescriptionDetailed +
                (elsewhere ? "\n\n" + "MS_Shelf_AlreadySelected".Translate().ToString() : ""));
            if (!elsewhere && Widgets.ButtonInvisible(rect) && shelf.SetSelection(slot, row.Item)) Close();
        }
    }
}
