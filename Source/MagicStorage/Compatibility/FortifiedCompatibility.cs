using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    // DMS uses Fortified's worktables. No hard reference to either mod assembly.
    internal sealed class FortifiedCompatibility : IStorageCompatibility
    {
        private static readonly Type tableType = AccessTools.TypeByName("Fortified.Building_WorkTableAutonomous");
        private static Type driverType;
        private static FieldInfo containerField;
        private static bool ready;
        public string PackageId => "AOBA.Framework";

        internal static bool IsTable(Thing thing) => thing != null && tableType != null && tableType.IsInstanceOfType(thing);
        internal static bool CanSupply(Thing thing) => !IsTable(thing) || ready;

        public void Apply(Harmony harmony)
        {
            Type worker = AccessTools.TypeByName("Fortified.WorkGiver_DoAutonomousBill");
            driverType = AccessTools.TypeByName("Fortified.JobDriver_DoAutonomousBill");
            containerField = tableType == null ? null : AccessTools.Field(tableType, "innerContainer");
            var jobOnThing = worker == null ? null : AccessTools.DeclaredMethod(worker, "JobOnThing",
                new[] { typeof(Pawn), typeof(Thing), typeof(bool) });
            var reserve = driverType == null ? null : AccessTools.DeclaredMethod(driverType, "TryMakePreToilReservations",
                new[] { typeof(bool) });
            if (tableType == null || !typeof(Building_WorkTable).IsAssignableFrom(tableType) ||
                driverType == null || !typeof(JobDriver).IsAssignableFrom(driverType) ||
                containerField == null || containerField.IsStatic || containerField.FieldType != typeof(ThingOwner) ||
                jobOnThing == null || jobOnThing.ReturnType != typeof(Job) || reserve == null || reserve.ReturnType != typeof(bool))
                throw new InvalidOperationException("Fortified autonomous production API was not found.");

            harmony.Patch(jobOnThing, postfix: new HarmonyMethod(typeof(FortifiedCompatibility), nameof(AttachPlan))
                { priority = Priority.Last });
            harmony.Patch(reserve, postfix: new HarmonyMethod(typeof(FortifiedCompatibility), nameof(Prepare))
                { priority = Priority.Last });
            ready = true;
        }

        private static void AttachPlan(Pawn __0, ref Job __result)
        {
            if (!ready || __result?.def?.driverClass != driverType || !IsTable(__result.targetA.Thing)) return;
            VanillaBillStorage.AttachStoredPlan(__0, ref __result);
        }

        private static void Prepare(JobDriver __instance, ref bool __result)
        {
            if (!__result || !ready || !IsTable(__instance.job?.targetA.Thing)) return;
            try
            {
                var container = containerField.GetValue(__instance.job.targetA.Thing) as ThingOwner;
                if (container == null)
                { __result = SupplyFailed(__instance.job, "Machine ingredient container is missing."); return; }
                VanillaBillStorage.PrepareForDriver(__instance, ref __result, container);
                if (!__result && __instance.pawn.CurJob != __instance.job)
                    SupplyFailed(__instance.job, "Queued job ingredient preflight failed.");
            }
            catch (Exception exception) { __result = false; VanillaBillStorage.Report(exception); }
        }

        internal static bool SupplyFailed(Job job, string reason)
        {
            Log.Warning("[MagicStorage] Fortified production supply failed (" +
                job?.targetA.Thing?.def?.defName + "): " + reason);
            return false;
        }
    }
}
