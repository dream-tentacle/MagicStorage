using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
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
            if (!shelf.Spawned || shelf.SelectedDef(slot) != itemDef) { Close(); return; }
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
