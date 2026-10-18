using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageResourceCounter
    {
        private static readonly Func<ResourceCounter, Thing, bool> shouldCount =
            AccessTools.MethodDelegate<Func<ResourceCounter, Thing, bool>>(
                AccessTools.Method(typeof(ResourceCounter), "ShouldCount"));

        static StorageResourceCounter()
        {
            new Harmony("mjcg.magicstorage.resources").Patch(
                AccessTools.Method(typeof(ResourceCounter), nameof(ResourceCounter.UpdateResourceCounts)),
                postfix: new HarmonyMethod(typeof(StorageResourceCounter), nameof(AddStoredResources)));
        }

        private static void AddStoredResources(ResourceCounter __instance, Map ___map)
        {
            if (___map == null) return;
            var manager = ___map.GetComponent<MapComponent_StorageNetworks>();
            if (manager == null) return;
            var counts = __instance.AllCountedAmounts;
            List<Thing> stacks = null;
            // Ownership and physical inventory determine counts, not network availability
            // or reservations. Spawned output shelves are already counted by vanilla.
            foreach (Building_StorageUnit unit in manager.StorageUnits)
            {
                if (!unit.Spawned || unit.Destroyed || unit.Map != ___map || unit.Faction != Faction.OfPlayer ||
                    unit.Position.Fogged(___map)) continue;
                foreach (ThingDef def in unit.Inventory.IndexedDefs)
                {
                    bool minified = def.thingClass != null && typeof(MinifiedThing).IsAssignableFrom(def.thingClass);
                    if (!minified && (!def.CountAsResource || !counts.ContainsKey(def))) continue;
                    var rottable = def.GetCompProperties<CompProperties_Rottable>();
                    // Stored contents do not tick. Ordinary resources, including resources
                    // destroyed by rot, can use the quantity index directly.
                    if (!minified && (rottable == null || rottable.rotDestroys))
                    {
                        AddCount(counts, def, unit.Inventory.GetTotalCount(def));
                        continue;
                    }

                    if (stacks == null) stacks = new List<Thing>();
                    stacks.Clear();
                    unit.Inventory.GetStacks(def, stacks);
                    foreach (Thing stored in stacks)
                    {
                        Thing item = stored.GetInnerIfMinified();
                        if (item == null || item.Destroyed || !item.def.CountAsResource ||
                            !shouldCount(__instance, item)) continue;
                        AddCount(counts, item.def, item.stackCount);
                    }
                }
            }
        }

        private static void AddCount(Dictionary<ThingDef, int> counts, ThingDef def, long amount)
        {
            if (counts.TryGetValue(def, out int current))
                counts[def] = (int)Math.Min(int.MaxValue, (long)current + amount);
        }
    }
}
