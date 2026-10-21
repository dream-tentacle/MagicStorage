using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class CosmicGestationPatches
    {
        static CosmicGestationPatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.gestation");
            Patch(harmony, typeof(WorkGiver_DoBill), "TryFindBestBillIngredients", nameof(FindIngredients));
            Patch(harmony, typeof(JobDriver_DoBill), nameof(JobDriver_DoBill.TryMakePreToilReservations), nameof(ReserveAndPrepare));
            Patch(harmony, typeof(Bill_Mech), nameof(Bill_Mech.BillTick), nameof(TickBill));
            harmony.Patch(AccessTools.Method(typeof(Building_WorkTable), nameof(Building_WorkTable.UsableForBillsAfterFueling)),
                postfix: new HarmonyMethod(typeof(CosmicGestationPatches), nameof(Usable)));
            harmony.Patch(AccessTools.Method(typeof(RecipeDef), nameof(RecipeDef.AvailableOnNow)),
                postfix: new HarmonyMethod(typeof(CosmicGestationPatches), nameof(RecipeAvailable)));
            harmony.Patch(AccessTools.Method(typeof(Bill_ResurrectMech), "AllAvailableResurrectionCorpses"),
                postfix: new HarmonyMethod(typeof(CosmicGestationPatches), nameof(ResurrectionCorpses)));
        }

        private static void Patch(Harmony harmony, System.Type type, string method, string prefix) =>
            harmony.Patch(AccessTools.Method(type, method),
                prefix: new HarmonyMethod(typeof(CosmicGestationPatches), prefix));

        private static bool FindIngredients(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen,
            List<IngredientCount> missingIngredients, ref bool __result)
        {
            if (!(billGiver is Building_CosmicMechGestator table)) return true;
            chosen.Clear();
            __result = CosmicGestationMaterials.TryPlan(table, bill, pawn, out var plan);
            if (__result)
                foreach (var pair in plan) chosen.Add(new ThingCount(pair.Key, pair.Value));
            else if (missingIngredients != null) missingIngredients.AddRange(bill.recipe.ingredients);
            return false;
        }

        private static bool ReserveAndPrepare(JobDriver_DoBill __instance, bool errorOnFailed, ref bool __result)
        {
            var job = __instance.job;
            if (!(job.targetA.Thing is Building_CosmicMechGestator table)) return true;
            var pawn = __instance.pawn;
            table.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            __result = false;
            if (!table.NetworkReady || !CosmicGestationCatalog.Supports(job.bill) ||
                !MechanitorUtility.IsMechanitor(pawn) ||
                !pawn.Reserve(table, job, 1, -1, null, errorOnFailed) ||
                !pawn.ReserveSittableOrSpot(table.InteractionCell, job, errorOnFailed)) return false;
            var bill = (Bill_Mech)job.bill;
            __result = bill.State != FormingState.Gathering || CosmicGestationMaterials.Prepare(table, job, pawn);
            return false;
        }

        private static bool TickBill(Bill_Mech __instance) =>
            !(__instance.billStack?.billGiver is Building_CosmicMechGestator table) || table.NetworkReady;

        private static void Usable(Building_WorkTable __instance, ref bool __result)
        {
            if (__instance is Building_CosmicMechGestator table) __result &= table.NetworkReady;
        }

        private static void RecipeAvailable(RecipeDef __instance, Thing thing, ref bool __result)
        {
            if (thing is Building_CosmicMechGestator) __result &= CosmicGestationCatalog.Available(__instance);
        }

        private static void ResurrectionCorpses(Bill_ResurrectMech __instance, Pawn pawn,
            ref IEnumerable<Corpse> __result)
        {
            if (!(__instance.billStack?.billGiver is Building_CosmicMechGestator table)) return;
            // The native eligibility check only sees map corpses. Use the actual
            // input candidates here as well, including a corpse already delivered.
            var items = new List<Thing>(table.innerContainer);
            if (table.NetworkReady)
                table.Network.GetMatchingStacks(d => __instance.recipe.fixedIngredientFilter.Allows(d), items);
            __result = items.OfType<Corpse>().Where(c => CosmicGestationMaterials.Allows(__instance, pawn, c));
        }
    }

    public sealed class WorkGiver_CosmicGestation : WorkGiver_DoBill
    {
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (!(thing is Building_CosmicMechGestator table)) return null;
            table.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (!table.NetworkReady)
            { JobFailReason.Is(StorageNetwork.StatusLabel(table.Network)); return null; }
            if (!MechanitorUtility.IsMechanitor(pawn))
            { JobFailReason.Is("NotAMechanitor".Translate()); return null; }
            return base.JobOnThing(pawn, thing, forced);
        }
    }
}
