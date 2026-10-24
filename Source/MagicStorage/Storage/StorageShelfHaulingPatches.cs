using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageShelfHaulingPatches
    {
        static StorageShelfHaulingPatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.shelves.hauling");
            harmony.Patch(AccessTools.Method(typeof(WorkGiver_Merge), nameof(WorkGiver_Merge.JobOnThing)),
                prefix: new HarmonyMethod(typeof(StorageShelfHaulingPatches), nameof(SkipShelfMerge)));
        }

        // Compatible stacks on supply shelves have independent slot limits.
        // Native merging ignores those limits and must not pick these stacks up.
        private static bool SkipShelfMerge(Thing t, ref Job __result)
        {
            if (t == null || !t.Spawned ||
                !(t.Map.haulDestinationManager.SlotGroupParentAt(t.Position) is Building_StorageSupplyShelf))
                return true;

            __result = null;
            return false;
        }
    }
}
