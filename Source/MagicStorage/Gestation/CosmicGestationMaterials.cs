using System;
using System.Collections.Generic;
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
            out Dictionary<Thing, int> allocations)
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
            return CraftingMaterialPlanner.TryPlan(stocks, needs, recipe.allowMixingIngredients,
                recipe.ignoreIngredientCountTakeEntireStacks, out allocations);

            void AddStocks(IEnumerable<Thing> items, bool inside)
            {
                foreach (var item in items.Where(t => Allows(bill, pawn, t)))
                {
                    int count = inside ? item.stackCount : network.AvailableToWithdraw(item);
                    if (count <= 0 || (!bill.recipe.allowMixingIngredients &&
                        bill.recipe.ignoreIngredientCountTakeEntireStacks && count != item.stackCount)) continue;
                    stocks.Add(new CraftingMaterialStock<Thing> { Item = item, Kind = item.def,
                        Count = count, Value = bill.recipe.IngredientValueGetter.ValuePerUnitOf(item.def) });
                }
            }
        }

        private sealed class Claim
        {
            internal Thing Item, Received, Delivered;
            internal int Count;
        }

        // Called only when the job is actually accepted, never during work/menu
        // searches. Staging and rollback keep every split Thing owned and saved.
        internal static bool Prepare(Building_CosmicMechGestator table, Job job, Pawn pawn)
        {
            if (!(job.bill is Bill_Mech bill) || bill.State != FormingState.Gathering ||
                !TryPlan(table, bill, pawn, out var plan)) return false;
            var network = table.Network;
            var manager = table.Map.GetComponent<MapComponent_StorageNetworks>();
            var staging = manager.CreateRecovery(table.InteractionCell);
            var existing = plan.Where(p => table.innerContainer.Contains(p.Key)).ToList();
            var claims = plan.Where(p => !table.innerContainer.Contains(p.Key))
                .Select(p => new Claim { Item = p.Key, Count = p.Value }).ToList();
            bool prepared = false;
            try
            {
                foreach (var claim in claims)
                    if (network.ReserveOutgoing(claim, claim.Item, claim.Count, table) != claim.Count) return false;
                foreach (var claim in claims)
                    if (!network.TryWithdrawReserved(claim, claim.Item, claim.Count, staging.Contents,
                        out claim.Received).Complete) return false;
                if (!table.NetworkReady || table.Network != network || bill.DeletedOrDereferenced ||
                    bill.suspended || !bill.PawnAllowedToStartAnew(pawn)) return false;
                foreach (var claim in claims)
                    if (StorageTransfer.Move(staging.Contents, table.innerContainer, claim.Received, claim.Count,
                        table.Map, table.InteractionCell, out claim.Delivered, false) != claim.Count) return false;

                // Bind the order in the same synchronous operation as delivery.
                // Deleting it before the worker arrives now follows native cleanup.
                bill.Notify_DoBillStarted(pawn);
                job.targetQueueB = new List<LocalTargetInfo>();
                job.countQueue = new List<int>();
                foreach (var pair in existing)
                {
                    job.targetQueueB.Add(pair.Key); job.countQueue.Add(pair.Value);
                }
                foreach (var claim in claims)
                { job.targetQueueB.Add(claim.Delivered); job.countQueue.Add(claim.Count); }
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
                foreach (var claim in claims)
                {
                    network.ReleaseOutgoing(claim);
                    if (!prepared && claim.Delivered != null && table.innerContainer.Contains(claim.Delivered))
                        StorageTransfer.Move(table.innerContainer, staging.Contents, claim.Delivered,
                            claim.Delivered.stackCount, table.Map, table.InteractionCell, false);
                }
                if (!prepared)
                {
                    foreach (var item in new List<Thing>(staging.Contents))
                        if (table.NetworkReady && table.Network == network)
                            network.TryStore(staging.Contents, item, item.stackCount);
                }
                manager.ReleaseRecovery(staging);
            }
        }
    }
}
