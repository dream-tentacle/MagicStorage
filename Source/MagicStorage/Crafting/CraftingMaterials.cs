using System;
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
            // A read-only argument for vanilla ingredient helpers, never registered
            // in a BillStack. The parameterless constructor does not allocate a bill ID.
            var bill = new Bill_Production { recipe = recipe, ingredientFilter = order.Ingredients };
            var things = new List<Thing>();
            // WorkGiver_DoBill.IsUsableIngredient accepts fixed recipe ingredients
            // before consulting the recipe-wide and player-selected filters.
            network.GetMatchingStacks(d => bill.IsFixedOrAllowedIngredient(d) &&
                recipe.ingredients.Any(i => i.filter.Allows(d)), things);
            var stocks = Stocks(network, order, bill, things);
            var needs = recipe.ingredients.Select(ingredient => new CraftingMaterialNeed<Thing>
            {
                Value = ingredient.GetBaseCount(),
                CountRequired = t => ingredient.CountRequiredOfFor(t.def, recipe, bill),
                Allows = t => ingredient.filter.Allows(t) && (ingredient.IsFixedIngredient || order.Ingredients.Allows(t))
            }).ToList();
            if (CraftingMaterialPlanner.TryPlan(stocks, needs, recipe.allowMixingIngredients,
                recipe.ignoreIngredientCountTakeEntireStacks, out allocations)) return true;

            // Shelves are a fallback: do not scan or take their everyday supplies when
            // the network alone can satisfy this batch. Keep network stacks first.
            things.Clear();
            StorageMaterialSources.GetShelfStacks(network, d => bill.IsFixedOrAllowedIngredient(d) &&
                recipe.ingredients.Any(i => i.filter.Allows(d)), things);
            stocks.AddRange(Stocks(network, order, bill, things));
            return CraftingMaterialPlanner.TryPlan(stocks, needs, recipe.allowMixingIngredients,
                recipe.ignoreIngredientCountTakeEntireStacks, out allocations);
        }

        private static List<CraftingMaterialStock<Thing>> Stocks(StorageNetwork network, CraftingOrder order,
            Bill_Production bill, List<Thing> things)
        {
            var recipe = order.Recipe;
            return things.Where(t => Allows(order, bill, t))
                .Select(t => new CraftingMaterialStock<Thing> { Item = t, Kind = t.def,
                    Count = Available(network, t), Value = recipe.IngredientValueGetter.ValuePerUnitOf(t.def) })
                .Where(s => s.Count > 0 && (recipe.allowMixingIngredients ||
                    !recipe.ignoreIngredientCountTakeEntireStacks || s.Count == s.Item.stackCount))
                .OrderBy(s => recipe.allowMixingIngredients ? s.Value : 0d)
                .ThenBy(s => s.Item.PositionHeld.DistanceToSquared(network.Core.Position))
                .ThenBy(s => s.Item.thingIDNumber).ToList();
        }

        internal static bool Allows(CraftingOrder order, Bill_Production bill, Thing t) =>
            t != null && !t.Destroyed && bill.IsFixedOrAllowedIngredient(t) &&
                    order.Recipe.ingredients.Any(i => i.filter.Allows(t)) &&
                    (!order.Recipe.interruptIfIngredientIsRotting || t.GetRotStage() == RotStage.Fresh);

        internal static int Available(StorageNetwork network, Thing item) => StorageMaterialSources.Available(network, item);
    }

    // Preparation is synchronous. Publish the real holder before moving any ingredients
    // so partial failures remain saved and refundable, without a long-lived material lease.
    internal static class CraftingMaterialPreparation
    {
        internal static bool Prepare(Building_StorageCore core, CraftingOrder order, Dictionary<Thing, int> allocations)
        {
            if (order.Batch != null) return order.Batch.Valid(order);
            var network = core.Network;
            if (network?.CanWork != true || allocations == null) return false;
            var batch = new CraftingBatch(core);
            order.Batch = batch;
            bool prepared = false;
            try
            {
                var bill = new Bill_Production { recipe = order.Recipe, ingredientFilter = order.Ingredients };
                foreach (var pair in allocations)
                {
                    if (core.Network != network || !core.CanWork || pair.Value <= 0 ||
                        !NetworkCraftingMaterialPolicy.Allows(order, bill, pair.Key) ||
                        NetworkCraftingMaterialPolicy.Available(network, pair.Key) < pair.Value) return false;
                    if (pair.Key.Spawned)
                    {
                        if (!TakeShelfStock(pair.Key, pair.Value, batch.Ingredients)) return false;
                    }
                    else if (!network.TryTransferTo(pair.Key, pair.Value, batch.Ingredients, merge: false).Complete)
                        return false;
                }
                prepared = batch.Initialize(order);
                return prepared;
            }
            catch (Exception exception)
            {
                order.Suspended = true;
                Log.Error("[MagicStorage] Could not prepare crafting materials: " + exception);
                return false;
            }
            finally
            {
                if (!prepared)
                {
                    try
                    {
                        if (batch.Cancel()) order.Batch = null;
                        else order.Suspended = true;
                    }
                    catch (Exception exception)
                    {
                        order.Suspended = true;
                        Log.Error("[MagicStorage] Could not refund crafting materials; batch retained: " + exception);
                    }
                }
            }
        }

        private static bool TakeShelfStock(Thing item, int count, ThingOwner destination)
        {
            Map map = item.Map;
            IntVec3 cell = item.Position;
            Thing part = null;
            try
            {
                part = item.SplitOff(count);
                return part != null && destination.TryAdd(part, false);
            }
            finally
            {
                if (part != null && !part.Destroyed && !part.Spawned && part.holdingOwner == null)
                    map.GetComponent<MapComponent_StorageNetworks>().PreserveLooseThing(part, cell);
            }
        }
    }
}
