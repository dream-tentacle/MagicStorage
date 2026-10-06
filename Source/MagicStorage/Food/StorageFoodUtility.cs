using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    [DefOf]
    public static class StorageFoodJobDefOf
    {
        public static JobDef MS_TakeStorageFood;
        static StorageFoodJobDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(StorageFoodJobDefOf)); }
    }

    internal static class StorageFoodUtility
    {
        internal static bool CanRequest(Pawn pawn) => pawn != null && pawn.Spawned && pawn.IsColonist &&
            !pawn.Drafted && !pawn.Downed && !pawn.InMentalState && !pawn.DevelopmentalStage.Baby() &&
            pawn.inventory != null && pawn.needs?.food != null && pawn.carryTracker != null &&
            pawn.carryTracker.CarriedThing == null && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);

        internal static bool CanUse(Pawn pawn, Building_StorageFoodOutlet outlet) => CanRequest(pawn) &&
            outlet != null && outlet.CanWork && outlet.Map == pawn.Map && outlet.Faction == pawn.Faction &&
            !outlet.IsForbidden(pawn) && !outlet.IsBurning() &&
            pawn.CanReach(outlet, PathEndMode.Touch, pawn.NormalMaxDanger());

        internal static bool CanEat(Pawn pawn, Thing item)
        {
            if (item == null || item.Destroyed || !item.def.IsNutritionGivingIngestible || item.def.IsDrug ||
                item is Corpse || !item.IngestibleNow || item.IsDessicated() || item.IsForbidden(pawn) ||
                !pawn.WillEat(item, pawn, careIfNotAcceptableForTitle: true, allowVenerated: false)) return false;
            bool starving = pawn.needs.food.CurCategory == HungerCategory.Starving;
            FoodPreferability minimum = starving ? FoodPreferability.DesperateOnly :
                (int)pawn.needs.food.CurCategory >= 2 || pawn.genes?.DontMindRawFood == true ?
                    FoodPreferability.RawBad : FoodPreferability.MealAwful;
            return item.def.ingestible.preferability >= minimum &&
                item.def.ingestible.preferability <= FoodPreferability.MealLavish &&
                (starving || !item.IsNotFresh()) && FoodUtility.NutritionForEater(pawn, item) > 0f;
        }

        internal static Job FindJob(Pawn pawn)
        {
            if (!CanRequest(pawn)) return null;
            var manager = pawn.Map.GetComponent<MapComponent_StorageNetworks>();
            if (!manager.CanTryFood(pawn)) return null;
            Thing best = null;
            Building_StorageFoodOutlet bestOutlet = null;
            float bestScore = float.NegativeInfinity;
            int bestCount = 0;
            // Group by network so each network inventory is scanned once per request.
            var closest = new Dictionary<StorageNetwork, Building_StorageFoodOutlet>();
            foreach (var outlet in manager.FoodOutlets)
            {
                if (!CanUse(pawn, outlet)) continue;
                if (!closest.TryGetValue(outlet.Network, out var old) ||
                    pawn.Position.DistanceToSquared(outlet.Position) < pawn.Position.DistanceToSquared(old.Position))
                    closest[outlet.Network] = outlet;
            }
            var candidates = new List<Thing>();
            foreach (var pair in closest)
            {
                candidates.Clear();
                pair.Key.GetMatchingStacks(def => def.IsNutritionGivingIngestible && !def.IsDrug, candidates);
                float distance = pawn.Position.DistanceTo(pair.Value.Position);
                foreach (var item in candidates)
                {
                    int available = pair.Key.AvailableToWithdraw(item);
                    if (available <= 0 || !CanEat(pawn, item)) continue;
                    int count = Math.Min(available, Math.Min(pawn.carryTracker.MaxStackSpaceEver(item.def),
                        FoodUtility.WillIngestStackCountOf(pawn, item.def, FoodUtility.NutritionForEater(pawn, item))));
                    if (count <= 0) continue;
                    float score = FoodUtility.FoodOptimality(pawn, item, item.def, distance);
                    if (score <= bestScore) continue;
                    best = item; bestOutlet = pair.Value; bestCount = count; bestScore = score;
                }
            }
            if (best == null) return null;
            var job = JobMaker.MakeJob(StorageFoodJobDefOf.MS_TakeStorageFood, best, bestOutlet);
            job.count = bestCount;
            return job;
        }
    }

    // The vanilla node remains a child and still defines the hunger priority.
    // Ordered children avoid PrioritySorter's random tie ordering.
    public sealed class ThinkNode_StorageFoodPriority : ThinkNode_Priority
    {
        public override float GetPriority(Pawn pawn) => subNodes[1].GetPriority(pawn);
    }

    public sealed class JobGiver_GetStorageFood : JobGiver_GetFood
    {
        protected override Job TryGiveJob(Pawn pawn) => GetPriority(pawn) > 0f ? StorageFoodUtility.FindJob(pawn) : null;
    }
}
