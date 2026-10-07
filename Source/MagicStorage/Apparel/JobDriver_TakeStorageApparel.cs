using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    public sealed class JobDriver_TakeStorageApparel : JobDriver, IStorageReservationClient, IStorageNetworkClient
    {
        private bool registered, prepared;
        private Building_StorageCore reservedCore;
        internal Apparel Item => job.GetTarget(TargetIndex.A).Thing as Apparel;
        internal Building_StorageApparelAdapter Adapter => job.GetTarget(TargetIndex.B).Thing as Building_StorageApparelAdapter;
        private Building_StorageCore Core => job.GetTarget(TargetIndex.C).Thing as Building_StorageCore;
        private void RegisterActions()
        {
            if (registered) return;
            registered = true;
            AddFinishAction(condition =>
            {
                reservedCore?.Network?.ReleaseOutgoing(this);
                if (condition != JobCondition.Succeeded && Adapter?.HasPrepared(pawn, Item) == true) Adapter.ReturnApparel();
            });
            SetFinalizerJob(condition => condition == JobCondition.Succeeded && prepared && Adapter?.HasPrepared(pawn, Item) == true
                ? JobMaker.MakeJob(JobDefOf.Wear, Item) : null);
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            RegisterActions();
            pawn.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (!CanCollect() || !pawn.Reserve(Adapter, job, 1, -1, null, errorOnFailed) ||
                !pawn.ReserveSittableOrSpot(Adapter.InteractionCell, job, errorOnFailed)) return false;
            return EnsureClaim();
        }
        private bool CanCollect()
        {
            if (!StorageApparelServices.CanAccess(pawn, Adapter) || Core == null || Core.Faction != pawn.Faction ||
                Adapter.Network != Core.Network || !StorageApparelServices.Policy.CanOptimize(pawn)) return false;
            if (prepared) return Adapter.HasPrepared(pawn, Item);
            return !Adapter.HasApparel && Item != null && Item.holdingOwner?.Owner is Building_StorageUnit unit &&
                unit.Network == Core.Network && !unit.IsForbidden(pawn) && !unit.IsBurning() &&
                StorageApparelServices.Policy.Allows(pawn, Item) && Core.Network.AvailableToWithdraw(Item, this) >= 1;
        }
        private bool EnsureClaim()
        {
            if (prepared) return true;
            if (!CanCollect()) return false;
            reservedCore = Core;
            return Core.Network.HasOutgoingReservation(this, Item, 1) ||
                Core.Network.ReserveOutgoing(this, Item, 1, Adapter, this, CanCollect) == 1;
        }
        private bool CanContinue()
        {
            pawn.Map?.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            return CanCollect() && EnsureClaim();
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            RegisterActions();
            this.FailOn(() => !CanContinue());
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell);
            yield return Toils_General.Do(() =>
            {
                if (!CanContinue()) { EndJobWith(JobCondition.Incompletable); return; }
                if (!prepared)
                {
                    var result = Core.Network.TryWithdrawReserved(this, Item, 1, Adapter.Contents, out Thing received);
                    if (!result.Complete || !(received is Apparel apparel))
                    { Adapter.ReturnApparel(); EndJobWith(JobCondition.Incompletable); return; }
                    Adapter.PrepareFor(pawn);
                    job.SetTarget(TargetIndex.A, apparel);
                    prepared = true;
                }
                // JobTracker's finalizer starts the unmodified vanilla Wear driver.
                EndJobWith(JobCondition.Succeeded);
            });
        }
        void IStorageReservationClient.OnReservationInvalidated(StorageReservation reservation, StorageReservationFailure reason)
        { if (!ended) EndJobWith(JobCondition.Incompletable); }
        void IStorageNetworkClient.OnStorageNetworksRebuilt()
        {
            if (!ended && !CanContinue()) pawn.Map.GetComponent<MapComponent_StorageNetworks>().Defer(() =>
            { if (!ended) EndJobWith(JobCondition.Incompletable); });
        }
        public override void ExposeData()
        { base.ExposeData(); Scribe_Values.Look(ref prepared, "apparelPrepared", false); }
    }
}
