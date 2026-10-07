using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal interface ICraftingMaterialPolicy
    {
        bool TryPlan(StorageNetwork network, CraftingOrder order, out Dictionary<Thing, int> allocations);
    }
    internal sealed class NetworkCraftingMaterialPolicy : ICraftingMaterialPolicy
    {
        public bool TryPlan(StorageNetwork network, CraftingOrder order, out Dictionary<Thing, int> allocations)
        {
            allocations = null;
            if (network == null || !network.CanWork) return false;
            var recipe = order.Recipe;
            var things = new List<Thing>();
            network.GetMatchingStacks(d => recipe.fixedIngredientFilter.Allows(d), things);
            var stocks = things.Where(t => !t.Destroyed && (!recipe.interruptIfIngredientIsRotting || t.GetRotStage() == RotStage.Fresh))
                .Select(t => new CraftingMaterialStock<Thing> { Item = t, Kind = t.def,
                    Count = network.AvailableToWithdraw(t), Value = recipe.IngredientValueGetter.ValuePerUnitOf(t.def) })
                .Where(s => s.Count > 0 && (!recipe.ignoreIngredientCountTakeEntireStacks || s.Count == s.Item.stackCount))
                .OrderBy(s => s.Value).ThenBy(s => s.Item.thingIDNumber).ToList();
            var needs = recipe.ingredients.Select(ingredient => new CraftingMaterialNeed<Thing>
            {
                Value = ingredient.GetBaseCount(),
                Allows = t => ingredient.filter.Allows(t) && (ingredient.IsFixedIngredient || order.Ingredients.Allows(t))
            }).ToList();
            return CraftingMaterialPlanner.TryPlan(stocks, needs, recipe.allowMixingIngredients,
                recipe.ignoreIngredientCountTakeEntireStacks, out allocations);
        }
    }

    // Each allocation has its own token because the shared withdrawal service owns one stack per token.
    // Partial acquisition is rolled back; supply shelves and manual withdrawals see the same claims.
    internal sealed class CraftingMaterialLease : IStorageReservationClient
    {
        private sealed class Claim { internal Thing Item; internal int Count; }
        private readonly List<Claim> claims = new List<Claim>();
        private readonly StorageNetwork initialNetwork;
        private readonly Building_StorageCore core;
        private readonly Thing endpoint;
        private readonly CraftingOrder order;
        private readonly System.Action invalidated;
        private bool released;
        internal StorageNetwork Network => core?.Network ?? initialNetwork;
        internal CraftingMaterialLease(StorageNetwork network, Thing endpoint = null,
            CraftingOrder order = null, System.Action invalidated = null)
        { initialNetwork = network; core = network.Core; this.endpoint = endpoint; this.order = order; this.invalidated = invalidated; }
        internal bool Acquire(Dictionary<Thing, int> allocations)
        {
            Release();
            released = false;
            foreach (var pair in allocations)
            {
                var claim = new Claim { Item = pair.Key, Count = pair.Value };
                claims.Add(claim);
                if (Network.ReserveOutgoing(claim, claim.Item, claim.Count, endpoint, this,
                    () => order == null || !order.Recipe.interruptIfIngredientIsRotting || claim.Item.GetRotStage() == RotStage.Fresh)
                    != claim.Count) { Release(); return false; }
            }
            return true;
        }
        internal bool Valid(CraftingOrder order)
        {
            if (released || !Network.CanWork) return false;
            foreach (var claim in claims)
                if (!Network.HasOutgoingReservation(claim, claim.Item, claim.Count) ||
                    (order.Recipe.interruptIfIngredientIsRotting && claim.Item.GetRotStage() != RotStage.Fresh)) return false;
            return true;
        }
        internal bool Collect(CraftingOrder order, ThingOwner destination)
        {
            if (!Valid(order)) return false;
            try
            {
                foreach (var claim in claims)
                    if (!Network.TryWithdrawReserved(claim, claim.Item, claim.Count, destination, out _).Complete) return false;
                return true;
            }
            finally { Release(); }
        }
        internal void Release()
        { foreach (var claim in claims) initialNetwork.ReleaseOutgoing(claim); claims.Clear(); released = true; }

        public void OnReservationInvalidated(StorageReservation reservation, StorageReservationFailure reason)
        {
            if (released) return;
            Release();
            invalidated?.Invoke();
        }
    }
}
