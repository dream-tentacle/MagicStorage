using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class ConstructionRepairPatches
    {
        static ConstructionRepairPatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.construction.repair");
            harmony.Patch(AccessTools.Method(typeof(WorkGiver_FixBrokenDownBuilding), nameof(WorkGiver_FixBrokenDownBuilding.HasJobOnThing)),
                prefix: new HarmonyMethod(typeof(ConstructionRepairPatches), nameof(SkipCheck)));
            harmony.Patch(AccessTools.Method(typeof(WorkGiver_FixBrokenDownBuilding), nameof(WorkGiver_FixBrokenDownBuilding.JobOnThing)),
                prefix: new HarmonyMethod(typeof(ConstructionRepairPatches), nameof(SkipJob)));
        }

        private static bool IsAutomaticRepair(Thing target) => target?.Spawned == true &&
            target.Map.GetComponent<MapComponent_ConstructionRepair>()?.IsRepairing(target) == true;

        private static bool SkipCheck(Thing t, ref bool __result)
        {
            if (!IsAutomaticRepair(t)) return true;
            __result = false;
            return false;
        }

        // Also guard callers that directly ask for a job without checking HasJobOnThing.
        private static bool SkipJob(Thing t, ref Job __result)
        {
            if (!IsAutomaticRepair(t)) return true;
            __result = null;
            return false;
        }
    }
}
