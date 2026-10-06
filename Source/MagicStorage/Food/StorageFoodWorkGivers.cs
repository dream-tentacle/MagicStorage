using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    // Preserve the original WorkGiverDefs (work type, priority, capacities, mechs and manual orders).
    // Each adapter returns its original job when the outlet cannot satisfy this request.
    public sealed class WorkGiver_StorageFeedPatient : WorkGiver_FeedPatient
    {
        private Job Find(Pawn pawn, Thing target, bool forced) => StorageFoodWorkUtility.PatientEligible(pawn, target as Pawn, def, forced) ?
            StorageFoodRequest.Find(pawn, StorageFoodPurpose.FeedPatient, target, forced) : null;
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => Find(pawn, t, forced) != null || base.HasJobOnThing(pawn, t, forced);
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) => Find(pawn, t, forced) ?? base.JobOnThing(pawn, t, forced);
        public override string JobInfo(Pawn pawn, Job job) => job.def == StorageFoodJobDefOf.MS_TakeStorageFood || job.def.GetModExtension<StorageFoodJobSettings>() != null ? "" : base.JobInfo(pawn, job);
    }

    public sealed class WorkGiver_StorageWardenFeed : WorkGiver_Warden_Feed
    {
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (ShouldTakeCareOfPrisoner(pawn, t, forced) && WardenFeedUtility.ShouldBeFed((Pawn)t))
            {
                var job = StorageFoodRequest.Find(pawn, StorageFoodPurpose.WardenFeed, t, forced);
                if (job != null) return job;
            }
            return base.JobOnThing(pawn, t, forced);
        }
        public override string JobInfo(Pawn pawn, Job job) => job.def.GetModExtension<StorageFoodJobSettings>() != null ? "" : base.JobInfo(pawn, job);
    }

    public sealed class WorkGiver_StorageWardenDeliver : WorkGiver_Warden_DeliverFood
    {
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            StorageFoodRequest.Find(pawn, StorageFoodPurpose.Deliver, t, forced) ?? base.JobOnThing(pawn, t, forced);
    }

    public sealed class WorkGiver_StorageBottleFeed : WorkGiver_BottleFeedBaby
    {
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (CanCreateManualFeedingJob(pawn, t, forced))
            {
                var job = StorageFoodRequest.Find(pawn, StorageFoodPurpose.BottleFeed, t, forced);
                if (job != null) return job;
            }
            return base.JobOnThing(pawn, t, forced);
        }
    }

    public sealed class WorkGiver_StorageTrain : WorkGiver_Train
    {
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t is Pawn animal && animal.IsAnimal && animal.RaceProps.animalType != AnimalType.Dryad &&
                !HasFoodToInteractAnimal(pawn, animal))
            {
                var job = StorageFoodRequest.Find(pawn, StorageFoodPurpose.Train, t, forced);
                if (job != null) return job;
            }
            return base.JobOnThing(pawn, t, forced);
        }
    }

    public sealed class WorkGiver_StorageTame : WorkGiver_Tame
    {
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t is Pawn animal && animal.IsAnimal && !HasFoodToInteractAnimal(pawn, animal))
            {
                var job = StorageFoodRequest.Find(pawn, StorageFoodPurpose.Tame, t, forced);
                if (job != null) return job;
            }
            return base.JobOnThing(pawn, t, forced);
        }
    }

    public sealed class WorkGiver_StorageHopper : WorkGiver_CookFillHopper
    {
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            StorageFoodRequest.Find(pawn, StorageFoodPurpose.Hopper, t, forced) ?? base.JobOnThing(pawn, t, forced);
    }

    public sealed class WorkGiver_StorageGrowthVat : WorkGiver_HaulToGrowthVat
    {
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            StorageFoodRequest.Find(pawn, StorageFoodPurpose.GrowthVat, t, forced) != null || base.HasJobOnThing(pawn, t, forced);
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            StorageFoodRequest.Find(pawn, StorageFoodPurpose.GrowthVat, t, forced) ?? base.JobOnThing(pawn, t, forced);
    }

    public sealed class WorkGiver_StorageBiosculpter : WorkGiver_HaulToBiosculpterPod
    {
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            StorageFoodRequest.Find(pawn, StorageFoodPurpose.Biosculpter, t, forced) != null || base.HasJobOnThing(pawn, t, forced);
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            StorageFoodRequest.Find(pawn, StorageFoodPurpose.Biosculpter, t, forced) ?? base.JobOnThing(pawn, t, forced);
    }
}
