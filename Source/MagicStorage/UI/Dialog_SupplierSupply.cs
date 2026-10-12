using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Dialog_SupplierSupply : Window
    {
        private readonly Building_TransportSupplier supplier;
        public override Vector2 InitialSize => new Vector2(620, 480);
        public Dialog_SupplierSupply(Building_TransportSupplier supplier)
        {
            this.supplier = supplier;
            doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!supplier.Spawned || !supplier.DirectSupply) { Close(); return; }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 30, 36), "MS_Transport_ConfigureTitle".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 42, rect.width, 82), "MS_Transport_DirectHint".Translate());
            for (int i = 0; i < Building_TransportSupplier.SlotCount; i++)
            {
                int slot = i;
                ThingDef itemDef = supplier.SelectedDef(slot);
                Rect row = new Rect(0, 135 + i * 80, rect.width, 72);
                Widgets.DrawMenuSection(row);
                Widgets.Label(new Rect(10, row.y + 4, row.width - 20, 24), "MS_Transport_PrioritySlot".Translate(slot + 1));
                if (itemDef != null) Widgets.DefIcon(new Rect(10, row.y + 34, 28, 28), itemDef);
                Widgets.Label(new Rect(48, row.y + 35, row.width - 260, 28),
                    itemDef?.LabelCap.ToString() ?? "MS_Shelf_Empty".Translate().ToString());
                if (Widgets.ButtonText(new Rect(row.width - 194, row.y + 33, 96, 30), "MS_Shelf_Select".Translate()))
                    Find.WindowStack.Add(new Dialog_StorageShelfItemPicker(supplier, slot));
                if (Widgets.ButtonText(new Rect(row.width - 90, row.y + 33, 80, 30), "MS_Shelf_Clear".Translate()))
                    supplier.SetSelection(slot, null);
            }
        }
    }
}
