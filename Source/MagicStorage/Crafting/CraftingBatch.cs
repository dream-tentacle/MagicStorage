using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // A core-owned unfinished batch. Ingredients remain real Things until completion;
    // only this batch owns them, including while its worker eats, sleeps or is replaced.
    public sealed class CraftingBatch : IExposable, IThingHolder, ISuspendableThingHolder
    {
        private Building_StorageCore core;
        private ThingOwner<Thing> ingredients;
        private CraftingBatchProducts output;
        private ThingOwner products => output.Contents;
        private Thing dominant;
        private float requiredWork, completedWork;
        private bool generated, committed, credited, faulted;
        public float Progress => requiredWork > 0 ? Math.Min(1f, completedWork / requiredWork) : 0f;
        internal bool Finished => requiredWork > 0 && completedWork >= requiredWork;
        internal bool Faulted => faulted;
        internal bool Committed => committed;
        internal float RequiredWork => requiredWork;
        internal ThingOwner Ingredients => ingredients;
        public IThingHolder ParentHolder => core;
        public bool IsContentsSuspended => true;

        public CraftingBatch() { InitializeHolders(); }
        internal CraftingBatch(Building_StorageCore core) : this() { this.core = core; }
        private void InitializeHolders()
        {
            if (ingredients == null) ingredients = new ThingOwner<Thing>(this, false, LookMode.Deep);
            if (output == null) output = new CraftingBatchProducts(this);
            ingredients.dontTickContents = products.dontTickContents = true;
        }
        public ThingOwner GetDirectlyHeldThings() => ingredients;
        public void GetChildHolders(List<IThingHolder> children)
        {
            // Two containers have the same holder; explicitly expose the output container.
            children.Add(output);
            ThingOwnerUtility.AppendThingHoldersFromThings(children, ingredients);
        }
        internal bool Initialize(CraftingOrder order)
        {
            var input = Snapshot(ingredients);
            dominant = CraftingServices.Production.SelectDominant(order.Recipe, input);
            requiredWork = CraftingServices.Production.WorkRequired(order.Recipe, dominant);
            if (float.IsNaN(requiredWork) || float.IsInfinity(requiredWork) || requiredWork < 0) return false;
            requiredWork = Math.Max(1f, requiredWork);
            return true;
        }
        internal bool Valid(CraftingOrder order)
        {
            if (faulted || requiredWork <= 0) return false;
            if (committed) return true;
            if (dominant != null && (dominant.Destroyed || !ingredients.Contains(dominant))) return false;
            if (order.Recipe.ingredients.Count > 0 && ingredients.Count == 0) return false;
            for (int i = 0; i < ingredients.Count; i++)
                if (ingredients[i].Destroyed || (order.Recipe.interruptIfIngredientIsRotting && ingredients[i].GetRotStage() != RotStage.Fresh)) return false;
            return true;
        }
        internal void Work(CraftingOrder order, Pawn worker, Building_CosmicWorkbench bench, int delta)
        {
            if (delta <= 0 || Finished || !Valid(order)) return;
            float speed = CraftingServices.Production.WorkSpeed(order.Recipe, worker, bench);
            if (speed <= 0 || float.IsNaN(speed) || float.IsInfinity(speed)) return;
            int appliedTicks = Math.Min(delta, Math.Max(1, (int)Math.Ceiling((requiredWork - completedWork) / speed)));
            completedWork = Math.Min(requiredWork, completedWork + speed * appliedTicks);
            CraftingServices.Production.Learn(order.Recipe, worker, appliedTicks);
        }

        internal bool Complete(CraftingOrder order, Pawn worker)
        {
            if (faulted || !Finished || !core.CanWork) return false;
            try
            {
                if (!committed)
                {
                    if (!Valid(order)) return false;
                    var input = Snapshot(ingredients);
                    if (!generated)
                    {
                        // Stage all output before consuming anything. These containers are
                        // saved with the order, and products are never regenerated on retry.
                        foreach (var product in CraftingServices.Production.MakeProducts(order.Recipe, worker, input, dominant))
                        {
                            if (product == null || product.Destroyed) continue;
                            if (!products.TryAdd(product, false)) throw new InvalidOperationException("Could not stage recipe product.");
                        }
                        generated = true;
                    }
                    foreach (var ingredient in input)
                        CraftingServices.Production.Consume(order.Recipe, ingredient, core.Map);
                    if (ingredients.Count != 0) throw new InvalidOperationException("Recipe left unconsumed ingredients.");
                    committed = true;
                }
                if (!credited)
                {
                    credited = true;
                    if (order.Mode == CraftingRepeatMode.RepeatCount) order.RepeatCount = Math.Max(0, order.RepeatCount - 1);
                    try { CraftingServices.Production.NotifyCompleted(worker, Snapshot(products)); }
                    catch (Exception exception) { Log.Error("[MagicStorage] Production notification failed: " + exception); }
                }
                // Like vanilla DropOnFloor, release beside the worker at the workbench.
                // Read the order at completion so changing delivery does not interrupt work.
                IntVec3 dropCell = order.DropProductsOnFloor && worker != null && worker.Spawned && worker.Map == core.MapHeld
                    ? worker.Position : core.PositionHeld;
                return ReturnContents(products, !order.DropProductsOnFloor, dropCell);
            }
            catch (Exception exception)
            {
                // Do not retry potentially stateful recipe callbacks after an exception.
                // Keep the batch inspectable and its real items owned until cancellation.
                faulted = true; order.Suspended = true;
                Log.Error("[MagicStorage] Crafting batch stopped: " + exception);
                return false;
            }
        }

        internal bool Cancel(bool tryStore = true)
        {
            // Uncommitted generated objects must never be refunded alongside their inputs.
            if (!committed)
                foreach (var product in Snapshot(products)) product.Destroy();
            return ReturnContents(ingredients, tryStore) && ReturnContents(products, tryStore);
        }
        private bool ReturnContents(ThingOwner contents, bool tryStore, IntVec3? dropCell = null)
        {
            Map map = core.MapHeld;
            if (contents.Count == 0) return true;
            if (map == null) return false;
            if (tryStore && core.CanWork)
                foreach (var item in Snapshot(contents)) core.Network.TryStore(contents, item, item.stackCount);
            if (contents.Count == 0) return true;
            var networks = map.GetComponent<MapComponent_StorageNetworks>();
            IntVec3 origin = dropCell ?? core.PositionHeld;
            var recovery = networks.CreateRecovery(origin);
            foreach (var item in Snapshot(contents))
                StorageTransfer.Move(contents, recovery.Contents, item, item.stackCount, map, origin, false);
            networks.ReleaseRecovery(recovery);
            return contents.Count == 0;
        }
        internal static List<Thing> Snapshot(ThingOwner owner)
        { var result = new List<Thing>(); for (int i = 0; i < owner.Count; i++) result.Add(owner[i]); return result; }
        public void ExposeData()
        {
            Scribe_References.Look(ref core, "core");
            Scribe_Deep.Look(ref ingredients, "ingredients", this);
            Scribe_Deep.Look(ref output, "products", this);
            Scribe_References.Look(ref dominant, "dominant");
            Scribe_Values.Look(ref requiredWork, "requiredWork");
            Scribe_Values.Look(ref completedWork, "completedWork");
            Scribe_Values.Look(ref generated, "generated");
            Scribe_Values.Look(ref committed, "committed");
            Scribe_Values.Look(ref credited, "credited");
            Scribe_Values.Look(ref faulted, "faulted");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) InitializeHolders();
        }
    }

    public sealed class CraftingBatchProducts : IExposable, IThingHolder, ISuspendableThingHolder
    {
        private readonly CraftingBatch parent;
        private ThingOwner<Thing> contents;
        public CraftingBatchProducts(CraftingBatch parent)
        { this.parent = parent; contents = new ThingOwner<Thing>(this, false, LookMode.Deep) { dontTickContents = true }; }
        internal ThingOwner Contents => contents;
        public IThingHolder ParentHolder => parent;
        public bool IsContentsSuspended => true;
        public ThingOwner GetDirectlyHeldThings() => contents;
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, contents);
        public void ExposeData()
        {
            Scribe_Deep.Look(ref contents, "contents", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (contents == null) contents = new ThingOwner<Thing>(this, false, LookMode.Deep);
                contents.dontTickContents = true;
            }
        }
    }
}
