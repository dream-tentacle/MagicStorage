using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    internal static class CosmicGestationMaterials
    {
        internal static bool Allows(Bill bill, Pawn pawn, Thing item) => item != null &&
            !item.Destroyed && !(item is Pawn) && !item.IsForbidden(pawn) &&
            bill.IsFixedOrAllowedIngredient(item) && bill.recipe.ingredients.Any(i => i.filter.Allows(item)) &&
            (!bill.recipe.interruptIfIngredientIsRotting || item.GetRotStage() == RotStage.Fresh) &&
            (!(item is Corpse corpse) || (corpse.InnerPawn.Faction == pawn.Faction &&
                pawn.mechanitor != null && pawn.mechanitor.HasBandwidthToResurrect(corpse)));

        internal static bool TryPlan(Building_CosmicMechGestator table, Bill bill, Pawn pawn,
            out Dictionary<Thing, int> allocations, List<string> missing = null)
        {
            allocations = null;
            if (!table.NetworkReady || !CosmicGestationCatalog.Supports(bill) ||
                !CosmicGestationCatalog.Available(bill.recipe) || !MechanitorUtility.IsMechanitor(pawn)) return false;
            var network = table.Network;
            var things = new List<Thing>();
            network.GetMatchingStacks(d => bill.IsFixedOrAllowedIngredient(d) &&
                bill.recipe.ingredients.Any(i => i.filter.Allows(d)), things);
            var stocks = new List<CraftingMaterialStock<Thing>>();
            // Reuse already delivered materials after an interrupted preparation.
            AddStocks(table.innerContainer, true);
            AddStocks(things, false);
            var recipe = bill.recipe;
            var needs = recipe.ingredients.Select(ingredient => new CraftingMaterialNeed<Thing>
            {
                Value = ingredient.GetBaseCount(),
                CountRequired = t => ingredient.CountRequiredOfFor(t.def, recipe, bill),
                Allows = t => ingredient.filter.Allows(t) &&
                    (ingredient.IsFixedIngredient || bill.ingredientFilter.Allows(t))
            }).ToList();
            if (CraftingMaterialPlanner.TryPlan(stocks, needs, recipe.allowMixingIngredients,
                recipe.ignoreIngredientCountTakeEntireStacks, out allocations)) return true;

            things.Clear();
            StorageMaterialSources.GetShelfStacks(network, d => bill.IsFixedOrAllowedIngredient(d) &&
                recipe.ingredients.Any(i => i.filter.Allows(d)), things);
            AddStocks(things, false);
            var shortages = missing == null ? null : new List<CraftingMaterialShortage>();
            bool complete = CraftingMaterialPlanner.TryPlan(stocks, needs, recipe.allowMixingIngredients,
                recipe.ignoreIngredientCountTakeEntireStacks, out allocations, shortages);
            if (!complete && missing != null)
                foreach (var shortage in shortages)
                {
                    var ingredient = recipe.ingredients[shortage.NeedIndex];
                    var kind = shortage.Kind as ThingDef;
                    double available = shortage.Available, required = shortage.Required;
                    if (ingredient.IsFixedIngredient)
                    {
                        kind = ingredient.FixedIngredient;
                        double value = recipe.IngredientValueGetter.ValuePerUnitOf(kind);
                        if (recipe.allowMixingIngredients && value > 0) available /= value;
                        required = !recipe.allowMixingIngredients && recipe.ignoreIngredientCountTakeEntireStacks
                            ? 1 : ingredient.CountRequiredOfFor(kind, recipe, bill);
                    }
                    missing.Add("MS_Gestation_MaterialCount".Translate(kind?.label ?? ingredient.filter.Summary,
                        available.ToString("0.##", CultureInfo.CurrentCulture),
                        required.ToString("0.##", CultureInfo.CurrentCulture)));
                }
            return complete;

            void AddStocks(IEnumerable<Thing> items, bool inside)
            {
                foreach (var item in items.Where(t => Allows(bill, pawn, t)))
                {
                    int count = inside ? item.stackCount : StorageMaterialSources.Available(network, item);
                    if (count <= 0 || (!bill.recipe.allowMixingIngredients &&
                        bill.recipe.ignoreIngredientCountTakeEntireStacks && count != item.stackCount)) continue;
                    stocks.Add(new CraftingMaterialStock<Thing> { Item = item, Kind = item.def,
                        Count = count, Value = bill.recipe.IngredientValueGetter.ValuePerUnitOf(item.def) });
                }
            }
        }

        // Called only when the job is actually accepted, never during work/menu
        // searches. Staging and rollback keep every split Thing owned and saved.
        internal static bool Prepare(Building_CosmicMechGestator table, Job job, Pawn pawn)
        {
            if (!(job.bill is Bill_Mech bill) || bill.State != FormingState.Gathering ||
                !TryPlan(table, bill, pawn, out var plan)) return false;
            var network = table.Network;
            var manager = table.Map.GetComponent<MapComponent_StorageNetworks>();
            var existing = plan.Where(p => table.innerContainer.Contains(p.Key)).ToList();
            var delivered = new List<ThingCount>();
            bool prepared = false;
            using (var transfer = new StorageMaterialTransaction(table.Map, table.InteractionCell))
            {
                try
                {
                    foreach (var pair in plan.Where(p => !table.innerContainer.Contains(p.Key)))
                        if (!Allows(bill, pawn, pair.Key) || StorageMaterialSources.Available(network, pair.Key) < pair.Value ||
                            !transfer.Add(pair.Key, pair.Value, pair.Key.Spawned ? null : network, table)) return false;
                    if (transfer.Requests.Count > 0 && !transfer.TryStage()) return false;
                    if (!table.NetworkReady || table.Network != network || bill.DeletedOrDereferenced ||
                        bill.suspended || !bill.PawnAllowedToStartAnew(pawn)) return false;
                    foreach (var request in transfer.Requests)
                    {
                        Thing received = null;
                        bool complete;
                        try
                        {
                            complete = transfer.Deliver(request, item => StorageTransfer.Move(item.holdingOwner,
                                table.innerContainer, item, request.Count, table.Map, table.InteractionCell,
                                out received, false) == request.Count);
                        }
                        finally
                        {
                            if (received != null) delivered.Add(new ThingCount(received, received.stackCount));
                        }
                        if (!complete || request.DeliveredCount != request.Count) return false;
                    }

                    // Bind the order in the same synchronous operation as delivery.
                    // Deleting it before the worker arrives now follows native cleanup.
                    bill.Notify_DoBillStarted(pawn);
                    job.targetQueueB = new List<LocalTargetInfo>();
                    job.countQueue = new List<int>();
                    foreach (var pair in existing)
                    {
                        job.targetQueueB.Add(pair.Key); job.countQueue.Add(pair.Value);
                    }
                    foreach (var item in delivered)
                    { job.targetQueueB.Add(item.Thing); job.countQueue.Add(item.Count); }
                    prepared = true;
                    return true;
                }
                catch (Exception exception)
                {
                    bill.suspended = true;
                    Log.Error("[MagicStorage] Could not prepare mech gestation materials: " + exception);
                    return false;
                }
                finally
                {
                    if (!prepared && delivered.Count > 0)
                    {
                        var recovery = manager.CreateRecovery(table.InteractionCell);
                        try
                        {
                            foreach (var entry in delivered)
                            {
                                var item = entry.Thing;
                                if (!table.innerContainer.Contains(item)) continue;
                                if (table.NetworkReady && table.Network == network)
                                    network.TryStore(table.innerContainer, item, item.stackCount);
                                if (!item.Destroyed && table.innerContainer.Contains(item))
                                    StorageTransfer.Move(table.innerContainer, recovery.Contents, item,
                                        item.stackCount, table.Map, table.InteractionCell, false);
                            }
                        }
                        finally { manager.ReleaseRecovery(recovery); }
                    }
                }
            }
        }
    }
}
