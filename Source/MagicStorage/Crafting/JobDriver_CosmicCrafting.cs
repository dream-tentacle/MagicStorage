using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    internal interface ICraftingWorkBehavior
    {
        Toil MakeWorkToil(JobDriver_CosmicCrafting driver);
    }
    internal sealed class VanillaCraftingWorkBehavior : ICraftingWorkBehavior
    {
        public Toil MakeWorkToil(JobDriver_CosmicCrafting driver)
        {
            var toil = ToilMaker.MakeToil("CosmicCraftingWork");
            toil.initAction = () =>
            {
                driver.WorkStartedAt = Find.TickManager.TicksGame;
                if (!driver.BeginBatch()) driver.EndJobWith(JobCondition.Incompletable);
            };
            toil.tickIntervalAction = delta =>
            {
                var pawn = toil.actor;
                var order = driver.Order;
                if (order?.Batch == null || !order.Batch.Valid(order))
                { driver.EndJobWith(JobCondition.Incompletable); return; }
                order.Batch.Work(order, pawn, driver.Bench, delta);
                pawn.GainComfortFromCellIfPossible(delta, chairsOnly: true);
                if (order.Batch.Finished) { driver.FinishBatch(); return; }
                // Port of Toils_Recipe.DoRecipeWork's long-work override. Unlike normal
                // finite recipes, every test recipe needs this because none will finish.
                if (Find.TickManager.TicksGame - driver.WorkStartedAt >= 3000 && pawn.IsHashIntervalTick(1000, delta))
                    pawn.jobs.CheckForJobOverride();
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithEffect(() => driver.Order?.Recipe.effectWorking, TargetIndex.A);
            toil.PlaySustainerOrSound(() => driver.Order?.Recipe.soundWorking);
            toil.activeSkill = () => driver.Order?.Recipe.workSkill;
            // None anchors the vanilla mote to the actor rather than the building.
            toil.WithProgressBar(TargetIndex.None, () => driver.Order?.Progress ?? 0f, alwaysShow: true);
            return toil;
        }
    }

    public sealed class JobDriver_CosmicCrafting : JobDriver, IStorageNetworkClient
    {
        private CraftingMaterialLease lease;
        private CraftingOrder claimedOrder;
        private MapComponent_CosmicCrafting dispatcher;
        private int revision;
        private bool finishRegistered;
        private int nextPolicyCheck;
        internal int WorkStartedAt;
        internal Building_CosmicWorkbench Bench => job.GetTarget(TargetIndex.A).Thing as Building_CosmicWorkbench;
        internal Building_StorageCore Core => job.GetTarget(TargetIndex.B).Thing as Building_StorageCore;
        internal CraftingOrder Order => Core?.Crafting.Find(job.count);

        public override string GetReport()
        {
            var recipe = Order?.Recipe;
            if (recipe == null) return base.GetReport();
            var product = recipe.specialProducts == null && recipe.products?.Count == 1
                ? recipe.products[0].thingDef : null;
            return "MS_Craft_WorkReport".Translate(product?.label ?? recipe.label);
        }

        private void RegisterFinish()
        { if (!finishRegistered) { AddFinishAction(_ => ReleaseAssignment()); finishRegistered = true; } }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            RegisterFinish();
            if (Core == null || job.workGiverDef == null) return false;
            pawn.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (!MapComponent_CosmicCrafting.CanUseBench(pawn, Bench, Core.Network)) return false;
            if (!pawn.Reserve(Bench, job, 1, -1, null, errorOnFailed) ||
                !pawn.ReserveSittableOrSpot(Bench.InteractionCell, job, errorOnFailed)) return false;
            return EnsureAssignment();
        }
        private bool EnsureAssignment()
        {
            if (claimedOrder != null && claimedOrder.Batch != null && dispatcher?.AssignedWorker(claimedOrder) == pawn)
                return claimedOrder.Batch.Valid(claimedOrder);
            // The map owns claims across graph rebuilds; the lease resolves its core's
            // current network while retaining the same exact material allocations.
            if (lease != null && lease.Network != Core?.Network) ReleaseAssignment();
            if (lease != null) return lease.Network == Core?.Network && lease.Valid(claimedOrder);
            if (pawn.Map == null || Core == null || job.workGiverDef == null ||
                !MapComponent_CosmicCrafting.CanUseBench(pawn, Bench, Core.Network)) return false;
            dispatcher = pawn.Map.GetComponent<MapComponent_CosmicCrafting>();
            if (!dispatcher.TryClaim(this, out lease)) return false;
            claimedOrder = Order; revision = claimedOrder.Revision;
            return true;
        }
        internal bool BeginBatch()
        {
            var order = Order;
            if (order == null) return false;
            if (order.Batch != null) return order.Batch.Valid(order);
            if (lease == null || !lease.Valid(order)) return false;
            var batch = new CraftingBatch(Core);
            order.Batch = batch;
            try
            {
                if (!lease.Collect(order, batch.Ingredients) || !batch.Initialize(order))
                {
                    if (batch.Cancel()) order.Batch = null;
                    return false;
                }
                return true;
            }
            catch (System.Exception exception)
            {
                if (batch.Cancel()) order.Batch = null;
                order.Suspended = true;
                Log.Error("[MagicStorage] Could not initialize crafting batch: " + exception);
                return false;
            }
            finally { lease.Release(); lease = null; }
        }
        internal void FinishBatch()
        {
            var order = Order;
            if (order?.Batch == null || !order.Batch.Complete(order, pawn))
            { EndJobWith(JobCondition.Incompletable); return; }
            order.Batch = null;
            EndJobWith(JobCondition.Succeeded);
        }
        internal bool CanContinue()
        {
            if (ended) return false;
            pawn.Map?.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (Core == null || !Core.Spawned || Bench == null || !Bench.Spawned || Order == null ||
                Bench.IsForbidden(pawn) || Bench.IsBurning() || Bench.Network != Core.Network ||
                Bench.Faction != pawn.Faction || Core.Faction != pawn.Faction ||
                Bench.InteractionCell.IsForbidden(pawn) || job.workGiverDef == null || !Core.CanWork) return false;
            if (!EnsureAssignment() || Order != claimedOrder || claimedOrder.Revision != revision) return false;
            if (Find.TickManager.TicksGame < nextPolicyCheck) return true;
            bool allowed = dispatcher.CanRun(Core, claimedOrder) &&
                CraftingServices.Workers.Allows(claimedOrder, pawn, job.workGiverDef.workType);
            if (allowed) nextPolicyCheck = Find.TickManager.TicksGame + 60;
            return allowed;
        }
        internal void OnMaterialsInvalidated()
        { if (!ended && Order?.Batch == null) EndJobWith(JobCondition.Incompletable); }

        void IStorageNetworkClient.OnStorageNetworksRebuilt()
        {
            if (!ended && !CanContinue()) pawn.Map.GetComponent<MapComponent_StorageNetworks>().Defer(() =>
            { if (!ended) EndJobWith(JobCondition.Incompletable); });
        }
        internal void ReleaseAssignment()
        {
            lease?.Release(); lease = null;
            dispatcher?.Release(claimedOrder, this);
            claimedOrder = null;
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            RegisterFinish();
            this.FailOn(() => !CanContinue());
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            yield return CraftingServices.Work.MakeWorkToil(this);
        }
        public override void ExposeData()
        { base.ExposeData(); Scribe_Values.Look(ref WorkStartedAt, "cosmicWorkStartedAt"); }
    }
}
