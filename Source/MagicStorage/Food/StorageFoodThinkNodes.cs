using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    public sealed class JobGiver_StorageFood : JobGiver_GetFood
    {
        private static readonly FieldInfo MinCategory = AccessTools.Field(typeof(JobGiver_GetFood), "minCategory");
        private static readonly FieldInfo MaxLevel = AccessTools.Field(typeof(JobGiver_GetFood), "maxLevelPercentage");
        protected override Job TryGiveJob(Pawn pawn)
        {
            // Preserve each XML node's own thresholds, including duty-specific overrides.
            var food = pawn.needs?.food;
            bool withinThreshold = food != null && (int)food.CurCategory >= (int)(HungerCategory)MinCategory.GetValue(this) &&
                food.CurLevelPercentage <= (float)MaxLevel.GetValue(this);
            if (withinThreshold)
            {
                var job = StorageFoodRequest.Find(pawn, StorageFoodPurpose.Eat);
                if (job != null) return job;
            }
            Job original = base.TryGiveJob(pawn);
            if (original != null || !withinThreshold || !StorageFoodRequest.CanRequest(pawn, StorageFoodPurpose.Hopper)) return original;
            // Native hungry pawns can refill a dispenser themselves, even outside cooking work.
            if (FoodUtility.TryFindBestFoodSourceFor(pawn, pawn, food.CurCategory == HungerCategory.Starving,
                out var source, out _, canRefillDispenser: true, canUsePackAnimalInventory: true) &&
                source is Building_NutrientPasteDispenser dispenser && !dispenser.HasEnoughFeedstockInHoppers())
            {
                var hopper = dispenser.AdjacentReachableHopper(pawn);
                if (hopper != null) return StorageFoodRequest.Find(pawn, StorageFoodPurpose.Hopper, hopper);
            }
            return null;
        }
    }

    public sealed class JobGiver_StoragePackFood : JobGiver_PackFood
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (StorageFoodRequest.CanRequest(pawn, StorageFoodPurpose.Pack) &&
                pawn.Map.resourceCounter.TotalHumanEdibleNutrition + StoredNutrition(pawn.Map) >= pawn.Map.mapPawns.ColonistsSpawnedCount * 1.5f)
            {
                var job = StorageFoodRequest.Find(pawn, StorageFoodPurpose.Pack);
                if (job != null) return job;
            }
            return base.TryGiveJob(pawn);
        }
        internal static float StoredNutrition(Map map)
        {
            var seen = new HashSet<StorageNetwork>();
            var foods = new List<Thing>();
            float nutrition = 0f;
            foreach (var outlet in map.GetComponent<MapComponent_StorageNetworks>().FoodOutlets)
            {
                if (!outlet.CanWork || !seen.Add(outlet.Network)) continue;
                foods.Clear();
                outlet.Network.GetMatchingStacks(d => d.IsNutritionGivingIngestible && !d.IsDrug, foods);
                foreach (var food in foods)
                    if (food.IngestibleNow && !food.IsNotFresh() && food.def.ingestible.HumanEdible)
                        nutrition += outlet.Network.AvailableToWithdraw(food) * food.GetStatValue(StatDefOf.Nutrition);
            }
            return nutrition;
        }
    }

    public sealed class JobGiver_StorageGatheringFood : JobGiver_EatInGatheringArea
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.mindState.duty != null)
            {
                var job = StorageFoodRequest.Find(pawn, StorageFoodPurpose.Gathering, pawn.mindState.duty.focus);
                if (job != null) return job;
            }
            return base.TryGiveJob(pawn);
        }
    }

    public sealed class JobGiver_StorageBingeFood : JobGiver_BingeFood
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (Find.TickManager.TicksGame - pawn.mindState.lastIngestTick > IngestInterval(pawn))
            {
                var job = StorageFoodRequest.Find(pawn, StorageFoodPurpose.Binge);
                if (job != null) return job;
            }
            return base.TryGiveJob(pawn);
        }
    }

    public sealed class MentalStateWorker_StorageBingeFood : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            if (!base.StateCanOccur(pawn) || pawn.needs?.food == null) return false;
            if (!pawn.Spawned) return true;
            float stored = pawn.Faction == Faction.OfPlayer && !pawn.IsFormingCaravan() ?
                JobGiver_StoragePackFood.StoredNutrition(pawn.Map) : 0f;
            return pawn.Map.resourceCounter.TotalHumanEdibleNutrition + stored > 10f;
        }
    }

    public sealed class JobGiver_StorageAutofeed : JobGiver_Autofeed
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (StorageFoodRequest.CanRequest(pawn, StorageFoodPurpose.BottleFeed) && pawn.CanReserve(pawn))
            {
                Pawn baby = ChildcareUtility.FindAutofeedBaby(pawn, AutofeedMode.Urgent, out var source);
                if (baby != null && baby.Spawned && !(source is Pawn) &&
                    !ChildcareUtility.ImmobileBreastfeederAvailable(pawn, baby, false, out _, out _))
                {
                    var job = StorageFoodRequest.Find(pawn, StorageFoodPurpose.BottleFeed, baby);
                    if (job != null) return job;
                }
            }
            return base.TryGiveJob(pawn);
        }
    }
}
