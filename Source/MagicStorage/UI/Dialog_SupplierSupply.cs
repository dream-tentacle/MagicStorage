using System;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Dialog_SupplierSupply : Window
    {
        private readonly Building_TransportSupplier supplier;
        private readonly string[] limitBuffers = new string[Building_TransportSupplier.SlotCount];
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(Math.Min(660, UI.screenWidth), Math.Min(660, UI.screenHeight));
        public Dialog_SupplierSupply(Building_TransportSupplier supplier)
        {
            this.supplier = supplier;
            for (int i = 0; i < limitBuffers.Length; i++) limitBuffers[i] = supplier.TargetLimit(i).ToString();
            doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!supplier.Spawned || !supplier.DirectSupply) { Close(); return; }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 30, 36), "MS_Transport_ConfigureTitle".Translate());
            Text.Font = GameFont.Small;
            string hint = "MS_Transport_DirectHint".Translate();
            float hintHeight = Text.CalcHeight(hint, rect.width);
            Widgets.Label(new Rect(0, 42, rect.width, hintHeight), hint);
            float start = 42 + hintHeight + 12;
            Rect viewport = new Rect(0, start, rect.width, Math.Max(1, rect.height - start - 60));
            Rect view = new Rect(0, 0, rect.width - 20, Building_TransportSupplier.SlotCount * 120);
            Widgets.BeginScrollView(viewport, ref scroll, view);
            for (int i = 0; i < Building_TransportSupplier.SlotCount; i++)
            {
                int slot = i;
                ThingDef itemDef = supplier.SelectedDef(slot);
                Rect row = new Rect(0, i * 120, view.width, 110);
                Widgets.DrawMenuSection(row);
                Widgets.Label(new Rect(10, row.y + 4, row.width - 20, 24), "MS_Transport_PrioritySlot".Translate(slot + 1));
                if (itemDef != null) Widgets.DefIcon(new Rect(10, row.y + 34, 28, 28), itemDef);
                Widgets.Label(new Rect(48, row.y + 35, row.width - 260, 28),
                    itemDef?.LabelCap.ToString() ?? "MS_Shelf_Empty".Translate().ToString());
                if (Widgets.ButtonText(new Rect(row.width - 194, row.y + 33, 96, 30), "MS_Shelf_Select".Translate()))
                    Find.WindowStack.Add(new Dialog_StorageShelfItemPicker(supplier, slot));
                if (Widgets.ButtonText(new Rect(row.width - 90, row.y + 33, 80, 30), "MS_Shelf_Clear".Translate()))
                    supplier.SetSelection(slot, null);
                Rect label = new Rect(10, row.y + 74, row.width - 150, 28);
                Rect input = new Rect(row.width - 130, row.y + 74, 120, 28);
                Widgets.Label(label, "MS_Transport_TargetLimit".Translate());
                int limit = supplier.TargetLimit(slot);
                Widgets.TextFieldNumeric(input, ref limit, ref limitBuffers[slot], 0, int.MaxValue);
                supplier.SetTargetLimit(slot, limit);
                TooltipHandler.TipRegion(new Rect(10, row.y + 74, row.width - 20, 28), "MS_Transport_TargetLimitHelp".Translate());
            }
            Widgets.EndScrollView();
        }
    }
}
