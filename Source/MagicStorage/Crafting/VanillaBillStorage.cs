using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    // Only bill searches and accepted DoBill jobs are extended. Hauling, pathing,
    // carrying, recipe execution and product storage retain their native methods.
    [StaticConstructorOnStartup]
    internal static class VanillaBillStorage
    {
        private sealed class PlanHolder { internal BillStorageSupplyPlan Plan; }
        private sealed class SearchScope
        {
            internal SearchScope Previous;
            internal Bill Bill;
            internal List<Thing> Stock;
        }

        private static readonly ConditionalWeakTable<Job, PlanHolder> plans = new ConditionalWeakTable<Job, PlanHolder>();
        [ThreadStatic] private static SearchScope search;
        private static bool reportedFailure;

        static VanillaBillStorage()
        {
            var harmony = new Harmony("mjcg.magicstorage.billSupply");
            harmony.Patch(AccessTools.Method(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients"),
                prefix: new HarmonyMethod(typeof(VanillaBillStorage), nameof(BeginSearch)),
                finalizer: new HarmonyMethod(typeof(VanillaBillStorage), nameof(EndSearch)));
            harmony.Patch(AccessTools.Method(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet"),
                prefix: new HarmonyMethod(typeof(VanillaBillStorage), nameof(AddCandidates)));
            harmony.Patch(AccessTools.Method(typeof(WorkGiver_DoBill), nameof(WorkGiver_DoBill.TryStartNewDoBillJob)),
                postfix: new HarmonyMethod(typeof(VanillaBillStorage), nameof(AttachPlan)));
            harmony.Patch(AccessTools.Method(typeof(JobDriver_DoBill), nameof(JobDriver_DoBill.TryMakePreToilReservations)),
                postfix: new HarmonyMethod(typeof(VanillaBillStorage), nameof(Prepare)) { priority = Priority.Last });
            harmony.Patch(AccessTools.Method(typeof(Job), nameof(Job.ExposeData)),
                postfix: new HarmonyMethod(typeof(VanillaBillStorage), nameof(ExposePlan)));
            harmony.Patch(AccessTools.Method(typeof(Job), nameof(Job.Clear)),
                postfix: new HarmonyMethod(typeof(VanillaBillStorage), nameof(ClearPlan)));
            harmony.Patch(AccessTools.Method(typeof(Job), nameof(Job.Clone)),
                postfix: new HarmonyMethod(typeof(VanillaBillStorage), nameof(ClonePlan)));
        }

        internal static bool Supports(Bill bill, Pawn pawn, Thing giver) =>
            bill is Bill_Production && !(bill is Bill_Autonomous) &&
            giver is Building_WorkTable table && !(table is Building_WorkTableAutonomous) &&
            FortifiedCompatibility.CanSupply(table) &&
            bill.billStack?.billGiver == table && bill.recipe != null &&
            bill.recipe.ingredients.Count > 0 && table.Spawned && !table.Destroyed &&
            pawn != null && pawn.Spawned && pawn.Map == table.Map &&
            pawn.Faction == Faction.OfPlayer && table.Faction == pawn.Faction &&
            JobDefOf.DoBill.driverClass == typeof(JobDriver_DoBill);

        private static void BeginSearch(Bill bill, Pawn pawn, Thing billGiver, out SearchScope __state)
        {
            __state = new SearchScope { Previous = search };
            search = null;
            try
            {
                if (!Supports(bill, pawn, billGiver)) return;
                var stock = CollectStock(bill, pawn, (Building_WorkTable)billGiver);
                if (stock.Count == 0) return;
                __state.Bill = bill;
                __state.Stock = stock;
                search = __state;
            }
            catch (Exception exception) { Report(exception); }
        }

        private static Exception EndSearch(SearchScope __state, Exception __exception)
        {
            if (__state != null) search = __state.Previous;
            return __exception;
        }

        private static List<Thing> CollectStock(Bill bill, Pawn pawn, Building_WorkTable table)
        {
            var result = new List<Thing>();
            var manager = table.Map.GetComponent<MapComponent_StorageNetworks>();
            if (manager == null) return result;
            manager.EnsureCurrent();
            var stacks = new List<Thing>();
            foreach (var network in manager.Networks)
            {
                if (!CanSupply(network, table.Map, pawn)) continue;
                stacks.Clear();
                network.GetMatchingStacks(bill.IsFixedOrAllowedIngredient, stacks);
                foreach (var item in stacks)
                {
                    var unit = item.holdingOwner?.Owner as Building_StorageUnit;
                    // Native selection reads stackCount. Exclude partly claimed stacks
                    // rather than temporarily rewriting real items to fake availability.
                    if (unit != null && unit.Spawned && unit.Map == table.Map && unit.Network == network &&
                        unit.Faction == pawn.Faction && !unit.IsForbidden(pawn) && !unit.Position.Fogged(unit.Map) &&
                        !item.Destroyed && !item.Spawned && item.stackCount > 0 &&
                        network.AvailableToWithdraw(item) == item.stackCount &&
                        !item.IsForbidden(pawn) && pawn.CanReserve(item) && AllowsIngredient(bill, item))
                        result.Add(item);
                }
            }
            return result;
        }

        internal static bool AllowsIngredient(Bill bill, Thing item) =>
            bill.IsFixedOrAllowedIngredient(item) && bill.recipe.ingredients.Exists(ingredient => ingredient.filter.Allows(item));

        internal static bool CanSupply(StorageNetwork network, Map map, Pawn pawn) =>
            network?.CanWork == true && network.HasProductionOutputPort && network.Core != null &&
            network.Core.Spawned && !network.Core.Destroyed && network.Core.Map == map &&
            pawn != null && network.Core.Faction == pawn.Faction && pawn.Faction == Faction.OfPlayer;

        private static void AddCandidates(Bill bill, ref List<Thing> availableThings, ref bool alreadySorted)
        {
            if (search?.Bill != bill || search.Stock == null) return;
            // The original region-search lists are shared scratch buffers. Never put
            // stored things in them or keep their references after the search ends.
            var combined = new List<Thing>(availableThings);
            var seen = new HashSet<Thing>(combined);
            foreach (Thing item in search.Stock)
                if (seen.Add(item)) combined.Add(item);
            availableThings = combined;
            alreadySorted = false;
        }

        private static void AttachPlan(Pawn pawn, Bill bill, IBillGiver giver, ref Job __result)
        {
            Job job = __result;
            if (job == null || job.def != JobDefOf.DoBill || job.bill != bill ||
                job.targetA.Thing != giver || !Supports(bill, pawn, giver as Thing) || job.targetQueueB == null) return;
            // Fortified replaces this temporary job. Capture only its final job.
            if (FortifiedCompatibility.IsTable(giver as Thing)) return;
            AttachStoredPlan(pawn, ref __result);
        }

        internal static void AttachStoredPlan(Pawn pawn, ref Job __result)
        {
            Job job = __result;
            if (job == null || !Supports(job.bill, pawn, job.targetA.Thing) || job.targetQueueB == null) return;
            bool hasStoredMaterial = false;
            foreach (var target in job.targetQueueB)
                if (target.Thing?.holdingOwner?.Owner is Building_StorageUnit && !target.Thing.Spawned)
                    hasStoredMaterial = true;
            if (!hasStoredMaterial) return;
            try
            {
                var plan = BillStorageSupplyPlan.Capture(job, pawn, (Building_WorkTable)job.targetA.Thing);
                if (plan == null) { __result = null; return; }
                plans.GetValue(job, _ => new PlanHolder()).Plan = plan;
            }
            catch (Exception exception) { __result = null; Report(exception); }
        }

        private static void Prepare(JobDriver_DoBill __instance, ref bool __result)
            => PrepareForDriver(__instance, ref __result);

        internal static void PrepareForDriver(JobDriver __instance, ref bool __result, ThingOwner container = null)
        {
            Job job = __instance.job;
            if (job == null || !plans.TryGetValue(job, out var holder) || holder.Plan == null || !__result) return;
            // Vanilla also calls reservation checks while evaluating a future job.
            // Those probes may validate a plan but must never materialize it.
            if (__instance.pawn.CurJob != job)
            {
                try { __result = holder.Plan.CanPrepare(job, __instance.pawn, container); }
                catch (Exception exception) { __result = false; Report(exception); }
                return;
            }
            try { __result = holder.Plan.Prepare(job, __instance.pawn, container); }
            catch (Exception exception) { __result = false; Report(exception); }
            finally
            {
                // Keep failed plans attached: a retry must validate/pay again.
                if (__result) holder.Plan = null;
                if (!__result && job.bill != null)
                    job.bill.nextTickToSearchForIngredients = GenTicks.TicksGame + 60;
            }
        }

        private static void ExposePlan(Job __instance)
        {
            if (Scribe.mode == LoadSaveMode.Saving && !plans.TryGetValue(__instance, out _)) return;
            if (Scribe.mode == LoadSaveMode.Inactive) return;
            var holder = plans.GetValue(__instance, _ => new PlanHolder());
            Scribe_Deep.Look(ref holder.Plan, "MS_productionSupply");
        }

        private static void ClearPlan(Job __instance) => plans.Remove(__instance);

        private static void ClonePlan(Job __instance, Job __result)
        {
            if (__result != null && plans.TryGetValue(__instance, out var holder) && holder.Plan != null)
                plans.GetValue(__result, _ => new PlanHolder()).Plan = holder.Plan.Copy();
        }

        internal static void Report(Exception exception)
        {
            if (reportedFailure) return;
            reportedFailure = true;
            Log.Error("[MagicStorage] Production material supply failed: " + exception);
        }
    }
}
