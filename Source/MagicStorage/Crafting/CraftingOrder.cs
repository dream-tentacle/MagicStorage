using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public enum CraftingRepeatMode { RepeatCount, Forever, TargetCount }
    public enum CraftingWorkerKind { Anyone, Slaves, Mechs, NonMechs }

    // Owned by a core. No Bill, BillStack or vanilla worktable is constructed.
    public sealed class CraftingOrder : IExposable
    {
        public int Id;
        public RecipeDef Recipe;
        public CraftingRepeatMode Mode;
        public int RepeatCount = 1, TargetCount = 10, UnpauseAt = 5;
        public bool Suspended, PauseWhenSatisfied, Paused;
        public bool DropProductsOnFloor;
        public Pawn Worker;
        public CraftingWorkerKind WorkerKind;
        public IntRange SkillRange = new IntRange(0, 20);
        public ThingFilter Ingredients = new ThingFilter();
        public ThingFilter AdditionalCounts = new ThingFilter();
        public bool IncludeEquipped, IncludeTainted = true, LimitToAllowedStuff;
        public FloatRange HitPoints = new FloatRange(0f, 1f);
        public QualityRange Quality = QualityRange.All;
        public int Revision;
        public CraftingBatch Batch;
        public float Progress => Batch?.Progress ?? 0f;

        public CraftingOrder() { }
        public CraftingOrder(int id, RecipeDef recipe)
        {
            Id = id; Recipe = recipe;
            Ingredients.CopyAllowancesFrom(recipe.defaultIngredientFilter ?? recipe.fixedIngredientFilter);
        }

        public void Changed() { Revision++; }
        internal bool ShouldRun(long products) => CraftingRunPolicy.ShouldRun(Mode, Suspended,
            RepeatCount, TargetCount, PauseWhenSatisfied, UnpauseAt, products, ref Paused);

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Defs.Look(ref Recipe, "recipe");
            Scribe_Deep.Look(ref Batch, "batch");
            Scribe_Values.Look(ref Mode, "mode");
            Scribe_Values.Look(ref RepeatCount, "repeatCount", 1);
            Scribe_Values.Look(ref TargetCount, "targetCount", 10);
            Scribe_Values.Look(ref UnpauseAt, "unpauseAt", 5);
            Scribe_Values.Look(ref Suspended, "suspended");
            Scribe_Values.Look(ref DropProductsOnFloor, "dropProductsOnFloor", false);
            Scribe_Values.Look(ref PauseWhenSatisfied, "pauseWhenSatisfied");
            Scribe_Values.Look(ref Paused, "paused");
            Scribe_References.Look(ref Worker, "worker");
            Scribe_Values.Look(ref WorkerKind, "workerKind");
            Scribe_Values.Look(ref SkillRange, "skillRange", new IntRange(0, 20));
            Scribe_Deep.Look(ref Ingredients, "ingredients");
            Scribe_Deep.Look(ref AdditionalCounts, "additionalCounts");
            Scribe_Values.Look(ref IncludeEquipped, "includeEquipped");
            Scribe_Values.Look(ref IncludeTainted, "includeTainted", true);
            Scribe_Values.Look(ref LimitToAllowedStuff, "limitToAllowedStuff");
            Scribe_Values.Look(ref HitPoints, "hitPoints", new FloatRange(0f, 1f));
            Scribe_Values.Look(ref Quality, "quality", QualityRange.All);
        }
    }

    public sealed class CraftingOrderList : IExposable
    {
        private List<CraftingOrder> orders = new List<CraftingOrder>();
        private int nextId = 1;
        public IReadOnlyList<CraftingOrder> Orders => orders;
        public CraftingOrder Add(RecipeDef recipe)
        { var order = new CraftingOrder(nextId++, recipe); orders.Add(order); return order; }
        public CraftingOrder Find(int id) => orders.Find(order => order.Id == id);
        public void Remove(CraftingOrder order)
        {
            if (order.Batch != null && !order.Batch.Cancel()) return;
            order.Batch = null; order.Changed(); orders.Remove(order);
        }
        internal bool CancelAll(bool tryStore)
        {
            foreach (var order in orders)
            {
                if (order.Batch != null && !order.Batch.Cancel(tryStore)) return false;
                order.Batch = null; order.Changed();
            }
            return true;
        }
        public void Move(CraftingOrder order, int delta)
        {
            int index = orders.IndexOf(order), target = index + delta;
            if (index < 0 || target < 0 || target >= orders.Count) return;
            orders.RemoveAt(index); orders.Insert(target, order);
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref orders, "orders", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            { if (orders == null) orders = new List<CraftingOrder>(); orders.RemoveAll(o => o?.Recipe == null); }
        }
    }
}
