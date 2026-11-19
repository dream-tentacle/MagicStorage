using System;
using LudeonTK;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal static class StorageDebugActions
    {
        [DebugAction("Magic storage", "填满选中的存储单元：随机物资", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FillSelectedWithMaterials()
        {
            var unit = Find.Selector.SingleSelectedThing as Building_StorageUnit;
            if (unit == null || !unit.Spawned || unit.Destroyed || unit.Map != Find.CurrentMap)
            {
                Messages.Message("MS_DebugFill_SelectUnit".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            var defs = DefDatabase<ThingDef>.AllDefsListForReading;
            // Check before retrying so an empty eligible pool cannot loop forever.
            if (!defs.Exists(IsEligibleItem))
            {
                Messages.Message("MS_DebugFill_NoMaterials".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            unit.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            int added = 0;
            int freeSlots = Math.Max(0, unit.SlotCapacity - unit.Inventory.UsedSlots);
            for (int i = 0; i < freeSlots; i++)
            {
                ThingDef def;
                do { def = defs[Rand.Range(0, defs.Count)]; }
                while (!IsEligibleItem(def));
                Thing item = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                item.stackCount = def.stackLimit;
                bool stored;
                try { stored = unit.Inventory.Contents.TryAdd(item, false); }
                finally
                {
                    if (!item.Destroyed && item.holdingOwner == null) item.Destroy(DestroyMode.Vanish);
                }
                if (!stored) break;
                added++;
            }
            Messages.Message("MS_DebugFill_Completed".Translate(added, unit.Inventory.UsedSlots, unit.SlotCapacity),
                MessageTypeDefOf.TaskCompletion, false);
        }

        private static bool IsEligibleItem(ThingDef def) =>
            def != null && def.category == ThingCategory.Item && !def.destroyOnDrop && def.stackLimit > 0;
    }
}
