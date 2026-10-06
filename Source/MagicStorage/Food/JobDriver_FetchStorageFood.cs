using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    public sealed class JobDriver_FetchStorageFood : JobDriver
    {
        private StorageNetwork reservedNetwork;
        private bool delivered;
        private StorageFoodPurpose Purpose => StorageFoodRequest.Purpose(job);
        private Building_StorageFoodOutlet Outlet => job.GetTarget(TargetIndex.B).Thing as Building_StorageFoodOutlet;
        private Thing Food => job.GetTarget(TargetIndex.A).Thing;

        private bool ValidRequest() => StorageFoodRequest.CanUse(pawn, Outlet, Purpose, job.targetC) &&
            StorageFoodWorkUtility.StillNeeded(pawn, Purpose, job.targetC, job.playerForced) &&
            StorageFoodRequest.CanEat(pawn, Food, Purpose, job.targetC);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (delivered) return true;
            if (!ValidRequest() || job.count <= 0 || Outlet.Network.AvailableToWithdraw(Food) <= 0) return false;
            if (job.targetC.HasThing)
            {
                LocalTargetInfo reserve = Purpose == StorageFoodPurpose.Hopper ? new LocalTargetInfo(job.targetC.Thing.Position) : job.targetC;
                if (!pawn.Reserve(reserve, job, 1, -1, null, errorOnFailed)) return false;
            }
            return true;
        }

        internal bool EnsureReservation()
        {
            if (delivered) return true;
            if (!ValidRequest() || job.count <= 0) { ReleaseReservation(); return false; }
            if (reservedNetwork != Outlet.Network) { ReleaseReservation(); reservedNetwork = Outlet.Network; }
            int wanted = Math.Min(job.count, StorageFoodRequest.Wanted(pawn, Food, Purpose, job.targetC));
            int count = reservedNetwork.ReserveOutgoing(this, Food, wanted);
            if (count <= 0 || (StorageFoodRequest.IsAnimalWork(Purpose) && count < wanted))
            { ReleaseReservation(); return false; }
            job.count = count;
            return true;
        }

        private void ReleaseReservation() { reservedNetwork?.ReleaseOutgoing(this); reservedNetwork = null; }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(condition =>
            {
                ReleaseReservation();
                if (!delivered && pawn.Map != null) pawn.Map.GetComponent<MapComponent_StorageNetworks>().DelayFoodRetry(pawn);
            });
            SetFinalizerJob(condition => condition == JobCondition.Succeeded && delivered ? ContinueWithFood() : null);
            this.FailOn(() => !delivered && !EnsureReservation());
            yield return Toils_General.Do(() =>
            {
                if (delivered) EndJobWith(JobCondition.Succeeded);
                else if (!EnsureReservation()) EndJobWith(JobCondition.Incompletable);
            });
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch).FailOnDestroyedOrNull(TargetIndex.B);
            yield return Toils_General.Do(() =>
            {
                if (delivered) { EndJobWith(JobCondition.Succeeded); return; }
                if (!EnsureReservation()) { EndJobWith(JobCondition.Incompletable); return; }
                Thing received;
                StorageTransferResult result;
                try { result = reservedNetwork.TryWithdrawReserved(this, Food, job.count, pawn.inventory.innerContainer, out received); }
                finally { ReleaseReservation(); }
                if (result.Transferred > 0 && received != null)
                {
                    job.targetA = received; job.count = result.Transferred; delivered = true;
                }
                EndJobWith(delivered ? JobCondition.Succeeded : JobCondition.Incompletable);
            });
        }

        private Job ContinueWithFood()
        {
            if (Food == null || Food.Destroyed || !pawn.inventory.innerContainer.Contains(Food)) return null;
            Job next;
            switch (Purpose)
            {
                case StorageFoodPurpose.Pack: return null;
                case StorageFoodPurpose.FeedPatient:
                case StorageFoodPurpose.WardenFeed:
                    next = JobMaker.MakeJob(JobDefOf.FeedPatient, Food, job.targetC); break;
                case StorageFoodPurpose.Deliver:
                    next = JobMaker.MakeJob(JobDefOf.DeliverFood, Food, job.targetC);
                    next.targetC = RCellFinder.SpotToChewStandingNear(job.targetC.Pawn, Food); break;
                case StorageFoodPurpose.BottleFeed:
                    next = ChildcareUtility.MakeBottlefeedJob(job.targetC.Pawn, Food); break;
                case StorageFoodPurpose.Train:
                    return JobMaker.MakeJob(JobDefOf.Train, job.targetC);
                case StorageFoodPurpose.Tame:
                    return JobMaker.MakeJob(JobDefOf.Tame, job.targetC);
                case StorageFoodPurpose.Hopper:
                case StorageFoodPurpose.GrowthVat:
                case StorageFoodPurpose.Biosculpter:
                    // Native hauling explicitly supports already-carried targets; avoid a second pickup.
                    if (StorageTransfer.Move(pawn.inventory.innerContainer, pawn.carryTracker.innerContainer,
                        Food, job.count, pawn.Map, pawn.Position, out var carried, false) <= 0 || carried == null) return null;
                    job.targetA = carried;
                    next = Purpose == StorageFoodPurpose.Hopper ?
                        JobMaker.MakeJob(JobDefOf.HaulToCell, carried, job.targetC.Thing.Position) :
                        JobMaker.MakeJob(JobDefOf.HaulToContainer, carried, job.targetC);
                    next.haulMode = Purpose == StorageFoodPurpose.Hopper ? HaulMode.ToCellStorage : HaulMode.ToContainer;
                    break;
                default:
                    Thing ingestible = Food;
                    if (!pawn.RaceProps.ToolUser)
                    {
                        // Animals' native Ingest driver approaches a spawned food target.
                        if (!pawn.inventory.innerContainer.TryDrop(Food, pawn.Position, pawn.Map, ThingPlaceMode.Near,
                            out ingestible, nearPlaceValidator: cell => !cell.IsForbidden(pawn) && pawn.CanReach(cell, PathEndMode.Touch, pawn.NormalMaxDanger())))
                        {
                            var manager = pawn.Map.GetComponent<MapComponent_StorageNetworks>();
                            var recovery = manager.CreateRecovery(pawn.Position);
                            StorageTransfer.Move(pawn.inventory.innerContainer, recovery.Contents, Food, job.count, pawn.Map, pawn.Position, false);
                            manager.DelayFoodRetry(pawn);
                            return null;
                        }
                    }
                    next = JobMaker.MakeJob(JobDefOf.Ingest, ingestible);
                    next.ignoreForbidden = job.ignoreForbidden;
                    next.overeat = job.overeat;
                    break;
            }
            next.count = Math.Min(job.count, next.GetTarget(TargetIndex.A).Thing == null || Purpose == StorageFoodPurpose.BottleFeed ? Food.stackCount : next.GetTarget(TargetIndex.A).Thing.stackCount);
            next.playerForced = job.playerForced;
            return next;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref delivered, "storageFoodDelivered", false);
        }
    }
}
