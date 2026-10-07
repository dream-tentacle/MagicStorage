using System;
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
            // Ownership and physical inventory determine counts, not network availability
            // or reservations. Spawned output shelves are already counted by vanilla.
            foreach (Building_StorageUnit unit in manager.StorageUnits)
            {
                if (!unit.Spawned || unit.Destroyed || unit.Map != ___map || unit.Faction != Faction.OfPlayer) continue;
                foreach (Thing stored in unit.Inventory.Contents)
                {
                    Thing item = stored.GetInnerIfMinified();
                    if (item == null || item.Destroyed || !item.def.CountAsResource ||
                        !shouldCount(__instance, item) || !counts.TryGetValue(item.def, out int current)) continue;
                    counts[item.def] = (int)Math.Min(int.MaxValue, (long)current + item.stackCount);
                }
            }
        }
    }
}
