using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    public enum StorageFoodPurpose { Eat, Pack, FeedPatient, Deliver, BottleFeed, Train, Tame, Gathering, Binge, Hopper, GrowthVat, Biosculpter, WardenFeed }

    public sealed class StorageFoodJobSettings : DefModExtension
    {
        public StorageFoodPurpose purpose;
    }

    // JobDef + native job targets carry the whole request through saves. No pooled Job subclasses.
    internal static class StorageFoodRequest
    {
        internal static StorageFoodPurpose Purpose(Job job) => job.def.GetModExtension<StorageFoodJobSettings>()?.purpose ?? StorageFoodPurpose.Eat;
        internal static bool IsDevice(StorageFoodPurpose p) => p == StorageFoodPurpose.Hopper || p == StorageFoodPurpose.GrowthVat || p == StorageFoodPurpose.Biosculpter;
        internal static bool IsAnimalWork(StorageFoodPurpose p) => p == StorageFoodPurpose.Train || p == StorageFoodPurpose.Tame;
        internal static Pawn Eater(Pawn getter, StorageFoodPurpose p, LocalTargetInfo target) =>
            p == StorageFoodPurpose.FeedPatient || p == StorageFoodPurpose.WardenFeed || p == StorageFoodPurpose.Deliver || p == StorageFoodPurpose.BottleFeed || IsAnimalWork(p) ? target.Pawn : getter;

        internal static bool CanRequest(Pawn pawn, StorageFoodPurpose purpose)
        {
            if (pawn == null || !pawn.Spawned || pawn.Faction != Faction.OfPlayer || pawn.IsFormingCaravan() || pawn.Drafted || pawn.Downed ||
                pawn.DevelopmentalStage.Baby() || (pawn.InMentalState && purpose != StorageFoodPurpose.Binge) ||
                pawn.inventory == null || pawn.carryTracker == null || pawn.carryTracker.CarriedThing != null) return false;
            if (purpose == StorageFoodPurpose.Eat && pawn.IsAnimal) return pawn.needs?.food != null;
            return (pawn.IsColonist || pawn.IsColonyMechPlayerControlled) &&
                pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
        }

        internal static bool CanUse(Pawn pawn, Building_StorageFoodOutlet outlet, StorageFoodPurpose purpose, LocalTargetInfo target)
        {
            if (!CanRequest(pawn, purpose) || outlet == null || !outlet.CanWork || outlet.Map != pawn.Map ||
                (purpose != StorageFoodPurpose.Binge && outlet.IsForbidden(pawn)) || outlet.IsBurning() ||
                !pawn.CanReach(outlet, PathEndMode.Touch, pawn.NormalMaxDanger())) return false;
            if (pawn.roping.IsRoped && !outlet.Position.InHorDistOf(pawn.roping.RopedTo.Cell, 8f)) return false;
            if (purpose == StorageFoodPurpose.Pack && !outlet.Position.InHorDistOf(pawn.Position, 20f)) return false;
            if (purpose != StorageFoodPurpose.Binge && !IsDevice(purpose))
            {
                Pawn eater = Eater(pawn, purpose, target);
                if (eater != null && !outlet.IsSociallyProper(pawn) && !outlet.IsSociallyProper(eater, eater.IsPrisonerOfColony, !pawn.IsAnimal)) return false;
            }
            if (purpose == StorageFoodPurpose.Gathering && (!target.IsValid ||
                !GatheringsUtility.InGatheringArea(outlet.Position, target.Cell, pawn.Map) ||
                !outlet.Position.InHorDistOf(pawn.Position, 14f))) return false;
            return true;
        }

        internal static bool CanEat(Pawn getter, Thing item, StorageFoodPurpose purpose, LocalTargetInfo target, bool allowVenerated = true)
        {
            if (item == null || item.Destroyed || !item.def.IsNutritionGivingIngestible || item.def.IsDrug ||
                item is Corpse || !item.IngestibleNow || item.IsDessicated() ||
                (purpose != StorageFoodPurpose.Binge && item.IsForbidden(getter))) return false;
            if (IsDevice(purpose)) return StorageFoodWorkUtility.DeviceAccepts(getter, target.Thing, item, purpose);
            Pawn eater = Eater(getter, purpose, target);
            if (eater?.needs?.food == null || !eater.WillEat(item, getter, careIfNotAcceptableForTitle: true,
                allowVenerated: purpose == StorageFoodPurpose.FeedPatient && allowVenerated)) return false;
            bool starving = eater.needs.food.CurCategory == HungerCategory.Starving || purpose == StorageFoodPurpose.Binge;
            FoodPreferability min = eater.IsAnimal ? FoodPreferability.NeverForNutrition : starving ? FoodPreferability.DesperateOnly :
                (int)eater.needs.food.CurCategory >= 2 || eater.genes?.DontMindRawFood == true ? FoodPreferability.RawBad : FoodPreferability.MealAwful;
            if (purpose == StorageFoodPurpose.BottleFeed) min = FoodPreferability.RawBad;
            if (purpose == StorageFoodPurpose.Binge) min = FoodPreferability.RawTasty;
            if (purpose == StorageFoodPurpose.Gathering) min = FoodPreferability.RawTasty;
            if (IsAnimalWork(purpose)) min = FoodPreferability.NeverForNutrition;
            var max = IsAnimalWork(purpose) ? FoodPreferability.RawTasty : FoodPreferability.MealLavish;
            if (item.def.ingestible.preferability < min || item.def.ingestible.preferability > max ||
                (!starving && item.IsNotFresh()) || FoodUtility.NutritionForEater(eater, item) <= 0f) return false;
            if (purpose == StorageFoodPurpose.Pack)
            {
                if (!JobGiver_PackFood.IsGoodPackableFoodFor(item, getter, checkMass: true)) return false;
                foreach (var thought in FoodUtility.ThoughtsFromIngesting(getter, item, item.def))
                    if (thought.thought.stages[0].baseMoodEffect < 0f) return false;
            }
            return true;
        }

        internal static int Wanted(Pawn pawn, Thing food, StorageFoodPurpose purpose, LocalTargetInfo target)
        {
            float nutrition = food.GetStatValue(StatDefOf.Nutrition);
            if (nutrition <= 0f) return 0;
            int wanted;
            if (IsDevice(purpose)) wanted = StorageFoodWorkUtility.DeviceCount(target.Thing, food, purpose);
            else if (purpose == StorageFoodPurpose.Pack)
            {
                float held = JobGiver_PackFood.GetInventoryPackableFoodNutrition(pawn);
                wanted = Math.Max(1, (int)Math.Floor((pawn.needs.food.MaxLevel - held) / nutrition));
                wanted = Math.Min(wanted, MassUtility.CountToPickUpUntilOverEncumbered(pawn, food));
            }
            else if (IsAnimalWork(purpose))
                wanted = FoodUtility.StackCountForNutrition(JobDriver_InteractAnimal.RequiredNutritionPerFeed(target.Pawn) * 8f,
                    FoodUtility.NutritionForEater(target.Pawn, food));
            else if (purpose == StorageFoodPurpose.Binge) wanted = food.def.ingestible.defaultNumToIngestAtOnce;
            else
            {
                Pawn eater = Eater(pawn, purpose, target);
                wanted = FoodUtility.WillIngestStackCountOf(eater, food.def, FoodUtility.NutritionForEater(eater, food));
            }
            return Math.Max(0, Math.Min(wanted, pawn.carryTracker.MaxStackSpaceEver(food.def)));
        }

        internal static Job Find(Pawn pawn, StorageFoodPurpose purpose, LocalTargetInfo target = default(LocalTargetInfo), bool forced = false, bool allowVenerated = false)
        {
            if (!CanRequest(pawn, purpose) || !StorageFoodWorkUtility.StillNeeded(pawn, purpose, target, forced)) return null;
            var manager = pawn.Map.GetComponent<MapComponent_StorageNetworks>();
            if (!manager.CanTryFood(pawn)) return null;
            var closest = new Dictionary<StorageNetwork, Building_StorageFoodOutlet>();
            foreach (var outlet in manager.FoodOutlets)
            {
                if (!CanUse(pawn, outlet, purpose, target)) continue;
                if (!closest.TryGetValue(outlet.Network, out var previous) ||
                    pawn.Position.DistanceToSquared(outlet.Position) < pawn.Position.DistanceToSquared(previous.Position)) closest[outlet.Network] = outlet;
            }
            Thing best = null; Building_StorageFoodOutlet port = null; int count = 0;
            float bestScore = float.NegativeInfinity;
            var items = new List<Thing>();
            foreach (var pair in closest)
            {
                items.Clear(); pair.Key.GetMatchingStacks(d => d.IsNutritionGivingIngestible && !d.IsDrug, items);
                foreach (var item in items)
                {
                    int available = pair.Key.AvailableToWithdraw(item);
                    if (available <= 0 || !CanEat(pawn, item, purpose, target, allowVenerated)) continue;
                    int wanted = Wanted(pawn, item, purpose, target);
                    if (IsAnimalWork(purpose) && available < wanted) continue;
                    if (purpose == StorageFoodPurpose.Pack && JobGiver_PackFood.GetInventoryPackableFoodNutrition(pawn) +
                        item.GetStatValue(StatDefOf.Nutrition) * Math.Min(available, wanted) < 0.8f) continue;
                    int take = Math.Min(available, wanted);
                    if (take <= 0) continue;
                    float distance = pawn.Position.DistanceTo(pair.Value.Position);
                    float score = IsDevice(purpose) ? -distance :
                        FoodUtility.FoodOptimality(Eater(pawn, purpose, target), item, item.def, distance, purpose == StorageFoodPurpose.Pack);
                    if (score <= bestScore) continue;
                    best = item; port = pair.Value; count = take; bestScore = score;
                }
            }
            if (best == null) return purpose == StorageFoodPurpose.FeedPatient && !allowVenerated ? Find(pawn, purpose, target, forced, true) : null;
            var job = Make(purpose, best, port, count, target);
            job.playerForced = forced;
            return job;
        }

        internal static Job Make(StorageFoodPurpose purpose, Thing food, Building_StorageFoodOutlet outlet, int count, LocalTargetInfo target)
        {
            JobDef def = DefDatabase<JobDef>.GetNamed("MS_StorageFood" + purpose);
            Job job = JobMaker.MakeJob(def, food, outlet, target);
            job.count = count;
            job.ignoreForbidden = purpose == StorageFoodPurpose.Binge;
            job.overeat = purpose == StorageFoodPurpose.Binge;
            return job;
        }
    }
}
