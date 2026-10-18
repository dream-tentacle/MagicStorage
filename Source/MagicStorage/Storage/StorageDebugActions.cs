using System;
using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal static class StorageDebugActions
    {
        private static readonly string[] materials =
            { "WoodLog", "Steel", "ComponentIndustrial", "Plasteel", "ComponentSpacer", "Bioferrite", "MealSimple", "RawRice" };

        [DebugAction("Magic storage", "填满选中的存储单元：随机物资", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FillSelectedWithMaterials() => FillSelected(materials);

        private static void FillSelected(string[] defNames)
        {
            var unit = Find.Selector.SingleSelectedThing as Building_StorageUnit;
            if (unit == null || !unit.Spawned || unit.Destroyed || unit.Map != Find.CurrentMap)
            {
                Messages.Message("MS_DebugFill_SelectUnit".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            var defs = new List<ThingDef>();
            foreach (string name in defNames)
            {
                // DLC resources are optional; draw uniformly from the loaded pool.
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def != null && def.category == ThingCategory.Item && !def.destroyOnDrop && def.stackLimit > 0)
                    defs.Add(def);
            }
            if (defs.Count == 0)
            {
                Messages.Message("MS_DebugFill_NoMaterials".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            unit.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            int added = 0;
            int freeSlots = Math.Max(0, unit.SlotCapacity - unit.Inventory.UsedSlots);
            for (int i = 0; i < freeSlots; i++)
            {
                ThingDef def = defs[Rand.Range(0, defs.Count)];
                Thing item = ThingMaker.MakeThing(def);
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
    }
}
