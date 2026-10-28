using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class CosmicWorkbenchRoomRoles
    {
        // Kitchen has no field in the vanilla RoomRoleDefOf class.
        private static RoomRoleDef Kitchen => DefDatabase<RoomRoleDef>.GetNamed("Kitchen");
        internal static IEnumerable<RoomRoleDef> Choices
        {
            get
            {
                yield return RoomRoleDefOf.Workshop;
                yield return Kitchen;
                yield return RoomRoleDefOf.Laboratory;
            }
        }

        internal static bool Supports(RoomRoleDef role) => role != null &&
            (role == RoomRoleDefOf.Workshop || role == Kitchen || role == RoomRoleDefOf.Laboratory);

        static CosmicWorkbenchRoomRoles()
        {
            var harmony = new Harmony("mjcg.magicstorage.crafting.roomroles");
            harmony.Patch(AccessTools.Method(typeof(RoomRoleWorker_Workshop), nameof(RoomRoleWorker_Workshop.GetScore)),
                postfix: Hook(nameof(WorkshopScore)));
            harmony.Patch(AccessTools.Method(typeof(RoomRoleWorker_Kitchen), nameof(RoomRoleWorker_Kitchen.GetScore)),
                postfix: Hook(nameof(KitchenScore)));
            harmony.Patch(AccessTools.Method(typeof(RoomRoleWorker_Laboratory), nameof(RoomRoleWorker_Laboratory.GetScore)),
                postfix: Hook(nameof(LaboratoryScore)));
            harmony.Patch(AccessTools.Method(typeof(StatPart_WorkTableRoomRole), nameof(StatPart_WorkTableRoomRole.Applies)),
                prefix: Hook(nameof(RoomPenalty)));
            harmony.Patch(AccessTools.Method(typeof(StatPart_WorkTableRoomRole), nameof(StatPart_WorkTableRoomRole.ExplanationPart)),
                postfix: Hook(nameof(PenaltyExplanation)));
            harmony.Patch(AccessTools.Method(typeof(CompReportWorkSpeed), nameof(CompReportWorkSpeed.CompInspectStringExtra)),
                postfix: Hook(nameof(WorkSpeedReport)));
            harmony.Patch(AccessTools.Method(typeof(BuildingProperties), nameof(BuildingProperties.SpecialDisplayStats)),
                postfix: Hook(nameof(OptimalRoomReport)));
        }

        private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(CosmicWorkbenchRoomRoles), name);

        internal static void Refresh(Building_CosmicWorkbench bench)
        {
            if (!bench.Spawned) return;
            bench.GetRoom()?.Notify_ContainedThingSpawnedOrDespawned(bench);
            // Room changes can also affect the other benches sharing that room.
            foreach (var stat in DefDatabase<StatDef>.AllDefsListForReading)
                if (stat.parts != null && stat.parts.Any(part => part is StatPart_WorkTableRoomRole))
                    stat.Worker.TryClearCache();
        }

        private static void WorkshopScore(RoomRoleWorker_Workshop __instance, Room __0, ref float __result)
            => AdjustScore(__instance, __0, RoomRoleDefOf.Workshop, 27f, ref __result);
        private static void KitchenScore(RoomRoleWorker_Kitchen __instance, Room __0, ref float __result)
            => AdjustScore(__instance, __0, Kitchen, 28f, ref __result);
        private static void LaboratoryScore(RoomRoleWorker_Laboratory __instance, Room __0, ref float __result)
            => AdjustScore(__instance, __0, RoomRoleDefOf.Laboratory, 60f, ref __result);

        private static void AdjustScore(RoomRoleWorker worker, Room room, RoomRoleDef role, float weight, ref float score)
        {
            foreach (var thing in room.ContainedAndAdjacentThings)
            {
                if (!(thing is Building_CosmicWorkbench bench)) continue;
                // Remove this bench's XML-based contribution, then add its selected
                // purpose with the same weight as a vanilla bench of that purpose.
                score -= worker.GetScoreDeltaIfBuildingPlaced(room, bench.def);
                if (bench.PreferredRoomRole == role) score += weight;
            }
        }

        private static bool RoomPenalty(Thing __0, ref bool __result)
        {
            if (!(__0 is Building_CosmicWorkbench bench)) return true;
            Room room = bench.GetRoom();
            __result = room != null && !room.PsychologicallyOutdoors && room.Role != bench.PreferredRoomRole;
            return false;
        }

        private static void PenaltyExplanation(StatRequest __0, ref string __result)
        {
            if (__result != null && __0.HasThing && __0.Thing is Building_CosmicWorkbench bench)
                __result = "NotInRoomRole".Translate(bench.PreferredRoomRole.label).CapitalizeFirst() + ": x" +
                    bench.def.building.workTableNotInRoomRoleFactor.ToStringPercent();
        }

        private static void WorkSpeedReport(CompReportWorkSpeed __instance, ref string __result)
        {
            if (__result == null || !(__instance.parent is Building_CosmicWorkbench bench) ||
                bench.def.building?.workTableRoomRole == null) return;
            string original = "NotInRoomRole".Translate(bench.def.building.workTableRoomRole.label);
            string selected = "NotInRoomRole".Translate(bench.PreferredRoomRole.label);
            __result = __result.Replace(original, selected);
        }

        private static void OptimalRoomReport(StatRequest req, ref IEnumerable<StatDrawEntry> __result)
        {
            if (req.HasThing && req.Thing is Building_CosmicWorkbench bench)
                __result = ReplaceOptimalRoom(__result, bench);
        }

        private static IEnumerable<StatDrawEntry> ReplaceOptimalRoom(IEnumerable<StatDrawEntry> entries, Building_CosmicWorkbench bench)
        {
            string label = "StatsReport_OptimalRoom".Translate();
            foreach (var entry in entries)
            {
                if (entry.stat == null && entry.LabelCap == label.CapitalizeFirst())
                    yield return new StatDrawEntry(entry.category, label, bench.PreferredRoomRole.LabelCap,
                        "Stat_Building_OptimalRoom_Desc".Translate(), entry.DisplayPriorityWithinCategory);
                else yield return entry;
            }
        }
    }
}
