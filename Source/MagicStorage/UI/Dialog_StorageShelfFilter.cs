using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Dialog_StorageShelfSharedFilter : Window
    {
        private readonly Building_StorageSupplyShelf shelf;
        private readonly ThingFilterUI.UIState filterState = new ThingFilterUI.UIState();
        private readonly string[] limitBuffers;
        private Vector2 limitsScroll;
        public override Vector2 InitialSize => new Vector2(Math.Min(620, UI.screenWidth), Math.Min(760, UI.screenHeight));

        public Dialog_StorageShelfSharedFilter(Building_StorageSupplyShelf shelf)
        {
            this.shelf = shelf;
            limitBuffers = new string[shelf.SlotCount];
            for (int i = 0; i < limitBuffers.Length; i++) limitBuffers[i] = shelf.FilterLimit(i).ToString();
            doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!shelf.Spawned || !shelf.FilterMode || shelf.SlotCount != limitBuffers.Length) { Close(); return; }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 30, 36), "MS_Shelf_FilterMode".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 42, rect.width, 58), "MS_Shelf_SharedFilterHint".Translate());
            StorageSettings settings = shelf.GetStoreSettings();
            if (Widgets.ButtonText(new Rect(0, 106, rect.width, 30),
                "Priority".Translate() + ": " + settings.Priority.Label().CapitalizeFirst()))
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
            int columns = Math.Min(3, shelf.SlotCount);
            int rows = (shelf.SlotCount - 1) / columns + 1;
            float contentHeight = rows * 66f;
            float limitsHeight = Math.Min(contentHeight, 132f);
            Rect limitsRect = new Rect(0, 144, rect.width, limitsHeight);
            Rect view = new Rect(0, 0, rect.width - (contentHeight > limitsHeight ? 20 : 0), contentHeight);
            float width = view.width / columns;
            Widgets.BeginScrollView(limitsRect, ref limitsScroll, view);
            for (int i = 0; i < shelf.SlotCount; i++)
            {
                float x = (i % columns) * width, y = (i / columns) * 66f;
                if (y + 66 < limitsScroll.y || y > limitsScroll.y + limitsHeight) continue;
                Widgets.Label(new Rect(x, y, width - 8, 28), "MS_Shelf_SlotLimit".Translate(i + 1));
                int limit = shelf.FilterLimit(i);
                Rect input = new Rect(x, y + 30, width - 8, 28);
                Widgets.TextFieldNumeric(input, ref limit, ref limitBuffers[i], 0, int.MaxValue);
                shelf.SetFilterLimit(i, limit);
                TooltipHandler.TipRegion(input, "MS_Shelf_SharedLimitTip".Translate());
            }
            Widgets.EndScrollView();
            float filterY = limitsRect.yMax + 4;
            ThingFilterUI.DoThingFilterConfigWindow(new Rect(0, filterY, rect.width, Math.Max(1, rect.height - filterY - 60)),
                filterState, shelf.SharedFilter, shelf.GetParentStoreSettings().filter, 8,
                forceHiddenFilters: HiddenSpecialFilters(), map: shelf.Map);
        }

        private static IEnumerable<SpecialThingFilterDef> HiddenSpecialFilters()
        {
            if (ModsConfig.IdeologyActive)
            {
                yield return SpecialThingFilterDefOf.AllowVegetarian;
                yield return SpecialThingFilterDefOf.AllowCarnivore;
                yield return SpecialThingFilterDefOf.AllowCannibal;
                yield return SpecialThingFilterDefOf.AllowInsectMeat;
            }
        }
    }

    public sealed class Dialog_StorageShelfFilter : Window
    {
        private readonly Building_StorageSupplyShelf shelf;
        private readonly int slot;
        private readonly ThingDef itemDef;
        private readonly List<ThingDef> materials;
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(560, Math.Min(650, UI.screenHeight));
        public Dialog_StorageShelfFilter(Building_StorageSupplyShelf shelf, int slot)
        {
            this.shelf = shelf; this.slot = slot; itemDef = shelf.SelectedDef(slot);
            materials = GenStuff.AllowedStuffsFor(itemDef).OrderBy(d => d.label).ToList();
            doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!shelf.Spawned || shelf.FilterMode || shelf.SelectedDef(slot) != itemDef) { Close(); return; }
            StorageShelfFilter filter = shelf.SlotFilter(slot);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 30, 36), "MS_Shelf_FilterTitle".Translate(slot + 1));
            Text.Font = GameFont.Small;
            Widgets.DefIcon(new Rect(0, 43, 28, 28), itemDef);
            Widgets.Label(new Rect(36, 43, rect.width - 36, 28), itemDef.LabelCap);
            string hint = "MS_Shelf_FilterHint".Translate();
            float hintHeight = Text.CalcHeight(hint, rect.width);
            Widgets.Label(new Rect(0, 78, rect.width, hintHeight), hint);
            float y = 78 + hintHeight + 12;
            if (itemDef.useHitPoints)
            {
                Widgets.FloatRange(new Rect(0, y, rect.width, 32), 78145300 + slot, ref filter.HitPoints,
                    0f, 1f, "HitPoints", ToStringStyle.PercentZero, 0f, GameFont.Small, null, 0.01f);
                y += 42;
            }
            if (itemDef.HasComp(typeof(CompQuality)))
            { Widgets.QualityRange(new Rect(0, y, rect.width, 32), 78145310 + slot, ref filter.Quality); y += 42; }
            if (!itemDef.MadeFromStuff) return;
            bool any = filter.AnyMaterial;
            Widgets.CheckboxLabeled(new Rect(0, y, rect.width, 28), "MS_Shelf_AnyMaterial".Translate(), ref any);
            if (any != filter.AnyMaterial)
            {
                if (!any && filter.Materials.Count == 0) filter.Materials.AddRange(materials);
                filter.AnyMaterial = any;
            }
            y += 36;
            if (filter.AnyMaterial) return;
            if (Widgets.ButtonText(new Rect(0, y, rect.width / 2 - 4, 28), "AllowAll".Translate()))
            { filter.Materials.Clear(); filter.Materials.AddRange(materials); }
            if (Widgets.ButtonText(new Rect(rect.width / 2 + 4, y, rect.width / 2 - 4, 28), "ClearAll".Translate()))
                filter.Materials.Clear();
            y += 36;
            search.OnGUI(new Rect(0, y, rect.width, 24), () => scroll = Vector2.zero); y += 32;
            Rect list = new Rect(0, y, rect.width, Math.Max(40, rect.height - y - 60));
            var visible = materials.Where(d => search.filter.Matches(d)).ToList();
            search.noResultsMatched = visible.Count == 0;
            Widgets.DrawMenuSection(list);
            Rect view = new Rect(0, 0, list.width - 24, visible.Count * 30);
            Widgets.BeginScrollView(list.ContractedBy(4), ref scroll, view);
            for (int i = 0; i < visible.Count; i++)
            {
                ThingDef material = visible[i]; bool allowed = filter.Materials.Contains(material);
                Widgets.DefIcon(new Rect(0, i * 30, 24, 24), material);
                Widgets.CheckboxLabeled(new Rect(32, i * 30, view.width - 32, 28), material.LabelCap, ref allowed);
                if (allowed && !filter.Materials.Contains(material)) filter.Materials.Add(material);
                else if (!allowed) filter.Materials.Remove(material);
            }
            Widgets.EndScrollView();
        }
    }
}
