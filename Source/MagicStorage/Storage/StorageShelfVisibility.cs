using Verse;

namespace MagicStorage
{
    public sealed class StorageShelfProperties : DefModExtension
    {
        public bool hideStoredItems;
    }

    internal static class StorageShelfVisibility
    {
        internal static bool HidesContents(Building_StorageSupplyShelf shelf)
            => shelf?.def.GetModExtension<StorageShelfProperties>()?.hideStoredItems == true;

        internal static bool IsHidden(Thing item)
            => item != null && item.Spawned && item.def.category == ThingCategory.Item &&
                HidesContents(item.Map.haulDestinationManager.SlotGroupParentAt(item.Position) as Building_StorageSupplyShelf);

        // Gate the virtual call itself so custom item and component overlays are hidden too.
        internal static void DrawGUIOverlay(Thing item)
        {
            if (!IsHidden(item)) item.DrawGUIOverlay();
        }
    }
}
