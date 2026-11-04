using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    // A saved plan owns no items and holds no reservations. Only an accepted job
    // stages real items; after delivery native placedThings owns the recipe state.
    internal sealed partial class BillStorageSupplyPlan : IExposable
    {
        internal sealed class Entry : IExposable
        {
            internal Thing Item;
            internal int Count;
            internal Building_StorageCore Core;

            public void ExposeData()
            {
                Scribe_References.Look(ref Item, "item");
                Scribe_Values.Look(ref Count, "count");
                Scribe_References.Look(ref Core, "core");
            }
        }

        private sealed class Receipt
        {
            internal Thing Item;
            internal int Count;
            internal IntVec3 Cell;
            internal ThingOwner Staging;
            internal StorageMaterialTransaction.Request Request;
        }

        private static readonly Func<Thing, IEnumerable<IntVec3>> ingredientCells =
            (Func<Thing, IEnumerable<IntVec3>>)Delegate.CreateDelegate(typeof(Func<Thing, IEnumerable<IntVec3>>),
                AccessTools.Method(typeof(Toils_JobTransforms), "IngredientPlaceCellsInOrder"));
        private List<Entry> entries = new List<Entry>();
        private RecipeDef recipe;
        private List<ThingCountClass> gestatorContents;
        private bool completed, preparing;

        internal BillStorageSupplyPlan Copy() => new BillStorageSupplyPlan
        {
            recipe = recipe,
            gestatorContents = gestatorContents, // Immutable snapshot shared by queued job copies.
            completed = completed,
            // A reentrant clone during transfer must fail closed rather than
            // inheriting a partly delivered plan as an ingredient-free recipe.
            entries = preparing ? null : entries?.ConvertAll(entry => entry == null ? null :
                new Entry { Item = entry.Item, Count = entry.Count, Core = entry.Core })
        };

        internal static BillStorageSupplyPlan Capture(Job job, Pawn pawn, Thing table)
        {
            if (job.targetQueueB == null || job.countQueue == null || job.targetQueueB.Count != job.countQueue.Count)
                return null;
            var manager = table.Map.GetComponent<MapComponent_StorageNetworks>();
            manager.EnsureCurrent();
            var plan = new BillStorageSupplyPlan { recipe = job.bill.recipe };
            var remaining = new List<LocalTargetInfo>();
            var remainingCounts = new List<int>();
            var byItem = new Dictionary<Thing, Entry>();
            for (int i = 0; i < job.targetQueueB.Count; i++)
            {
                Thing item = job.targetQueueB[i].Thing;
                int count = job.countQueue[i];
                if (item == null || item.Spawned || VanillaBillStorage.IsSpecifiedIngredient(job.bill, item) ||
                    !(item.holdingOwner?.Owner is Building_StorageUnit unit))
                {
                    remaining.Add(job.targetQueueB[i]); remainingCounts.Add(count);
                    continue;
                }
                if (count <= 0 || item.Destroyed || item.def.stackLimit <= 0) return null;
                if (!byItem.TryGetValue(item, out var entry))
                {
                    if (!VanillaBillStorage.CanSupply(unit.Network, table.Map, pawn)) return null;
                    entry = new Entry { Item = item, Core = unit.Network.Core };
                    byItem.Add(item, entry); plan.entries.Add(entry);
                }
                if ((long)entry.Count + count > entry.Core.Network.AvailableToWithdraw(item)) return null;
                entry.Count += count;
            }
            if (plan.entries.Count == 0) return null;
            if (table is Building_MechGestator gestator)
            {
                plan.gestatorContents = new List<ThingCountClass>();
                foreach (Thing item in gestator.innerContainer)
                    plan.gestatorContents.Add(new ThingCountClass(item, item.stackCount));
            }
            // Publish both queues only after the complete plan has been validated.
            job.targetQueueB = remaining;
            job.countQueue = remainingCounts;
            return plan;
        }

        private bool Valid(Job job, Pawn pawn, Thing table)
        {
            if (!ValidContext(job, pawn, table)) return false;
            foreach (var entry in entries)
            {
                if (entry == null || entry.Item == null || entry.Item.Destroyed || entry.Item.Spawned ||
                    entry.Count <= 0 || entry.Item.def.stackLimit <= 0 || entry.Core == null ||
                    !(entry.Item.holdingOwner?.Owner is Building_StorageUnit unit) ||
                    !unit.Spawned || unit.Map != table.Map || unit.Network != entry.Core.Network ||
                    unit.Faction != pawn.Faction || unit.IsForbidden(pawn) || unit.Position.Fogged(unit.Map) ||
                    entry.Item.IsForbidden(pawn) || !pawn.CanReserve(entry.Item) ||
                    !VanillaBillStorage.AllowsIngredient(job.bill, entry.Item) ||
                    entry.Core.Network.AvailableToWithdraw(entry.Item) < entry.Count) return false;
            }
            return true;
        }

        private bool ValidContext(Job job, Pawn pawn, Thing table)
        {
            if (!VanillaBillStorage.Supports(job.bill, pawn, table) || job.bill.recipe != recipe ||
                job.bill.DeletedOrDereferenced || job.bill.suspended || !job.bill.PawnAllowedToStartAnew(pawn) ||
                entries == null || entries.Count == 0) return false;
            // A queued job may have selected the deficit before another delivery.
            if (table is Building_MechGestator gestator &&
                (gestatorContents == null || gestator.innerContainer.Count != gestatorContents.Count ||
                gestatorContents.Exists(e => e.thing == null || e.thing.stackCount != e.Count ||
                    !gestator.innerContainer.Contains(e.thing)))) return false;
            foreach (var entry in entries)
                if (entry?.Core == null || !VanillaBillStorage.CanSupply(entry.Core.Network, table.Map, pawn)) return false;
            return true;
        }

        internal bool CanPrepare(Job job, Pawn pawn, ThingOwner container = null)
        {
            if (completed) return true;
            Thing table = job.targetA.Thing;
            if (preparing || table == null) return false;
            var manager = table.Map?.GetComponent<MapComponent_StorageNetworks>();
            if (manager == null) return false;
            manager.EnsureCurrent();
            return Valid(job, pawn, table) && (container != null
                ? ReferenceEquals(container.Owner, table) : PlanCells(table, pawn) != null);
        }

        internal bool Prepare(Job job, Pawn pawn, ThingOwner container = null)
        {
            if (completed) return true;
            bool delivered = container == null ? PrepareOnMap(job, pawn) : PrepareContainer(job, pawn, container);
            if (delivered)
            {
                // Visual failures must never turn an already paid delivery into a failed job.
                try { ShowDeliveryBeams(job.targetA.Thing); }
                catch (Exception exception) { Log.ErrorOnce("[MagicStorage] Production supply beam failed: " + exception, 142770831); }
            }
            return delivered;
        }

        private void ShowDeliveryBeams(Thing target)
        {
            if (target == null || !target.Spawned) return;
            var beam = DefDatabase<ThingDef>.GetNamedSilentFail("MS_Mote_ConstructionSupplyBeam") ??
                DefDatabase<ThingDef>.GetNamedSilentFail("MS_Mote_ProductionSupplyBeamFallback");
            if (beam == null) return;
            var seen = new HashSet<StorageNetwork>();
            foreach (var entry in entries)
            {
                var network = entry.Core?.Network;
                if (network == null || !seen.Add(network)) continue;
                Building_ProductionOutputPort nearest = null;
                int distance = int.MaxValue;
                foreach (var member in network.Members)
                {
                    if (!(member.parent is Building_ProductionOutputPort port) || !port.Spawned || port.Map != target.Map) continue;
                    int squared = port.Position.DistanceToSquared(target.Position);
                    if (squared < distance)
                    {
                        nearest = port;
                        distance = squared;
                    }
                }
                if (nearest != null) MoteMaker.MakeInteractionOverlay(beam, nearest, target);
            }
        }

        private bool PrepareOnMap(Job job, Pawn pawn)
        {
            Thing table = job.targetA.Thing;
            if (preparing || table == null) return false;
            Map map = table.Map;
            var manager = map?.GetComponent<MapComponent_StorageNetworks>();
            if (manager == null) return false;
            manager.EnsureCurrent();
            if (!Valid(job, pawn, table)) return false;
            var destinations = PlanCells(table, pawn);
            if (destinations == null) return false;
            preparing = true;
            var receipts = new List<Receipt>();
            bool pickup = table is Building_MechGestator;
            bool staged = false;
            try
            {
                using (var transfer = new StorageMaterialTransaction(map, table.InteractionCell))
                {
                    try
                    {
                        foreach (var entry in entries)
                            if (!transfer.Add(entry.Item, entry.Count, entry.Core.Network, entry.Core)) return false;
                        if (!transfer.TryStage()) return false;
                        staged = true;
                        int nextCell = 0;
                        foreach (var request in transfer.Requests)
                        {
                            bool accepted = false;
                            bool delivered = transfer.Deliver(request, item =>
                            {
                                int remaining = request.Count;
                                while (remaining > 0)
                                {
                                    if (pawn.CurJob != job || !ValidContext(job, pawn, table) || table.Map != map) return false;
                                    int amount = Math.Min(remaining, item.def.stackLimit);
                                    if (amount <= 0) return false;
                                    var staging = item.holdingOwner;
                                    Thing part = staging?.Take(item, amount);
                                    if (part == null || part.stackCount != amount) return false;
                                    var receipt = new Receipt { Item = part, Count = amount, Cell = destinations[nextCell++],
                                        Staging = staging, Request = request };
                                    receipts.Add(receipt);
                                    if (!CanPlace(part, receipt.Cell, table, pawn) ||
                                        !pawn.Reserve(part, job, 1, -1, null, false)) return false;
                                    if (!pickup) map.physicalInteractionReservationManager.Reserve(pawn, job, part);
                                    GenSpawn.Spawn(part, receipt.Cell, map);
                                    if (!part.Spawned || part.Destroyed || part.Map != map ||
                                        part.Position != receipt.Cell || part.stackCount != amount) return false;
                                    if (!pickup) HaulAIUtility.UpdateJobWithPlacedThings(job, part, amount);
                                    remaining -= amount;
                                }
                                accepted = true;
                                return true;
                            });
                            if (!accepted || !delivered || request.DeliveredCount != request.Count) return false;
                        }
                        if (pawn.CurJob != job || !ValidContext(job, pawn, table) || table.Map != map) return false;
                        foreach (var receipt in receipts)
                            if (receipt.Item.Destroyed || !receipt.Item.Spawned || receipt.Item.Map != map ||
                                receipt.Item.Position != receipt.Cell || receipt.Item.stackCount != receipt.Count ||
                                !map.reservationManager.ReservedBy(receipt.Item, pawn, job) ||
                                (table is Pawn && !VanillaBillStorage.AllowsIngredient(job.bill, receipt.Item))) return false;
                        if (pickup)
                        {
                            // Let the native driver carry these real stacks into the gestator.
                            foreach (var receipt in receipts)
                            {
                                job.targetQueueB.Add(receipt.Item);
                                job.countQueue.Add(receipt.Count);
                            }
                        }
                        completed = true;
                        return true;
                    }
                    finally
                    {
                        if (!completed) Rollback(receipts, job, pawn, map, manager);
                    }
                }
            }
            finally
            {
                preparing = false;
                if (staged) map.resourceCounter.UpdateResourceCounts();
            }
        }

        private List<IntVec3> PlanCells(Thing table, Pawn pawn)
        {
            // Fully enumerate native placement order before callbacks can start a
            // nested search; its iterator uses a shared vanilla scratch set.
            var cells = new List<IntVec3>(ingredientCells(table));
            var selected = new HashSet<IntVec3>();
            var result = new List<IntVec3>();
            foreach (var entry in entries)
            {
                long parts = ((long)entry.Count + entry.Item.def.stackLimit - 1) / entry.Item.def.stackLimit;
                if (parts > cells.Count - selected.Count) return null;
                for (long i = 0; i < parts; i++)
                {
                    bool found = false;
                    foreach (var cell in cells)
                        if (!selected.Contains(cell) && CanPlace(entry.Item, cell, table, pawn))
                        { selected.Add(cell); result.Add(cell); found = true; break; }
                    if (!found) return null;
                }
            }
            return result;
        }

        private static bool CanPlace(Thing item, IntVec3 cell, Thing table, Pawn pawn)
        {
            Map map = table.Map;
            if (map == null || !table.Spawned || !cell.InBounds(map) || map.haulDestinationManager.SlotGroupParentAt(cell) != null ||
                cell.GetItemCount(map) != 0 || !pawn.CanReserve(cell) ||
                !GenSpawn.CanSpawnAt(item.def, cell, map) || !pawn.CanReach(cell, PathEndMode.Touch, Danger.Deadly)) return false;
            foreach (var other in cell.GetThingList(map))
                if (GenSpawn.SpawningWipes(item.def, other.def)) return false;
            return true;
        }

        private static void Rollback(List<Receipt> receipts, Job job, Pawn pawn, Map map,
            MapComponent_StorageNetworks manager)
        {
            // Reverse order returns the original source object before its split
            // parts. Rejoin actual survivors; never refund a destroyed object.
            for (int i = receipts.Count - 1; i >= 0; i--)
            {
                var receipt = receipts[i];
                Thing part = receipt.Item;
                try
                {
                    job.placedThings?.RemoveAll(placed => placed.thing == part);
                    map.physicalInteractionReservationManager.TryRelease(pawn, job, part);
                    if (map.reservationManager.ReservedBy(part, pawn, job))
                        map.reservationManager.Release(part, pawn, job);
                    if (part.Destroyed) continue;
                    if (part.Spawned)
                    {
                        if (part.Map != map || part.Position != receipt.Cell) continue;
                        part.DeSpawn();
                    }
                    if (part.holdingOwner != null && part.holdingOwner != receipt.Staging) continue;
                    if (part.holdingOwner == null && !receipt.Staging.TryAdd(part, false))
                    { manager.PreserveLooseThing(part, receipt.Cell); continue; }
                    Thing original = receipt.Request.Received;
                    if (part == original) continue;
                    if (original != null && !original.Destroyed && receipt.Staging.Contains(original))
                        original.TryAbsorbStack(part, false);
                    else if (receipt.Request.Source?.Owner is Building_StorageUnit unit && unit.Spawned && unit.Map == map)
                        StorageTransfer.Move(receipt.Staging, receipt.Request.Source, part, part.stackCount, map, receipt.Cell);
                }
                catch (Exception exception) { VanillaBillStorage.Report(exception); }
            }
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref recipe, "recipe");
            Scribe_Values.Look(ref completed, "completed");
            Scribe_Collections.Look(ref gestatorContents, "gestatorContents", LookMode.Deep);
            Scribe_Collections.Look(ref entries, "materials", LookMode.Deep);
        }
    }
}
