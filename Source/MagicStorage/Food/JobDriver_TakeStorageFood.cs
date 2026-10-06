using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    public sealed class JobDriver_TakeStorageFood : JobDriver
    {
        private StorageNetwork reservedNetwork;
        private bool delivered;
        private Building_StorageFoodOutlet Outlet => job.GetTarget(TargetIndex.B).Thing as Building_StorageFoodOutlet;
        private Thing Food => job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => delivered ||
            (StorageFoodUtility.CanUse(pawn, Outlet) && StorageFoodUtility.CanEat(pawn, Food) &&
             job.count > 0 && Outlet.Network.AvailableToWithdraw(Food) > 0);

        internal bool EnsureReservation()
        {
            if (delivered) return true;
            if (!StorageFoodUtility.CanUse(pawn, Outlet) || !StorageFoodUtility.CanEat(pawn, Food) || job.count <= 0)
            { ReleaseReservation(); return false; }
            var network = Outlet.Network;
            if (reservedNetwork != network) { ReleaseReservation(); reservedNetwork = network; }
            int wanted = Math.Min(job.count, FoodUtility.WillIngestStackCountOf(pawn, Food.def, FoodUtility.NutritionForEater(pawn, Food)));
            int count = network.ReserveOutgoing(this, Food, wanted);
            if (count <= 0) { ReleaseReservation(); return false; }
            job.count = count;
            return true;
        }

        private void ReleaseReservation()
        {
            reservedNetwork?.ReleaseOutgoing(this);
            reservedNetwork = null;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(condition =>
            {
                ReleaseReservation();
                if (!delivered && pawn.Map != null) pawn.Map.GetComponent<MapComponent_StorageNetworks>().DelayFoodRetry(pawn);
            });
            // Hand off to the unchanged Ingest driver with the exact item now in inventory.
            SetFinalizerJob(condition =>
            {
                if (condition != JobCondition.Succeeded || !delivered || Food == null ||
                    Food.Destroyed || !pawn.inventory.innerContainer.Contains(Food)) return null;
                var ingest = JobMaker.MakeJob(JobDefOf.Ingest, Food);
                ingest.count = Math.Min(job.count, Food.stackCount);
                return ingest;
            });
            this.FailOn(() => !delivered && !EnsureReservation());
            yield return Toils_General.Do(() =>
            {
                if (delivered) EndJobWith(JobCondition.Succeeded);
                else if (!EnsureReservation()) EndJobWith(JobCondition.Incompletable);
            });
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch)
                .FailOnDestroyedOrNull(TargetIndex.B).FailOnForbidden(TargetIndex.B);
            yield return Toils_General.Do(() =>
            {
                if (delivered) { EndJobWith(JobCondition.Succeeded); return; }
                if (!EnsureReservation()) { EndJobWith(JobCondition.Incompletable); return; }
                StorageTransferResult result;
                Thing received;
                try
                {
                    result = reservedNetwork.TryWithdrawReserved(this, Food, job.count,
                        pawn.inventory.innerContainer, out received);
                }
                finally { ReleaseReservation(); }
                if (result.Transferred > 0 && received != null)
                {
                    job.SetTarget(TargetIndex.A, received);
                    job.count = result.Transferred;
                    delivered = true;
                }
                EndJobWith(delivered ? JobCondition.Succeeded : JobCondition.Incompletable);
            });
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref delivered, "storageFoodDelivered", false);
        }
    }
}
