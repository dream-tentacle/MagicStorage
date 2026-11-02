using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    internal sealed partial class BillStorageSupplyPlan
    {
        private sealed class ContainerReceipt
        {
            internal Thing Item;
            internal ThingOwner Staging;
            internal int Count;
        }

        // Keep whole, unmerged item identities until the batch commits, so rollback
        // cannot remove ingredients delivered previously by a pawn or another job.
        private bool PrepareContainer(Job job, Pawn pawn, ThingOwner container)
        {
            if (completed) return true;
            if (preparing || pawn.CurJob != job || !(job.targetA.Thing is Building_WorkTable table) ||
                !ReferenceEquals(container.Owner, table))
                return FortifiedCompatibility.SupplyFailed(job, "Invalid task or ingredient container owner.");
            Map map = table.Map;
            var manager = map?.GetComponent<MapComponent_StorageNetworks>();
            if (manager == null) return FortifiedCompatibility.SupplyFailed(job, "Storage network manager is unavailable.");
            manager.EnsureCurrent();
            if (!Valid(job, pawn, table))
                return FortifiedCompatibility.SupplyFailed(job, "Ingredients or supply network are no longer valid.");
            preparing = true;
            bool withdrawalAttempted = false;
            var receipts = new List<ContainerReceipt>();
            try
            {
                using (var transfer = new StorageMaterialTransaction(map, table.InteractionCell))
                {
                    try
                    {
                        foreach (var entry in entries)
                            if (!transfer.Add(entry.Item, entry.Count, entry.Core.Network, entry.Core))
                                return FortifiedCompatibility.SupplyFailed(job, "Unable to prepare the ingredient withdrawal.");
                        withdrawalAttempted = true;
                        if (!transfer.TryStage())
                            return FortifiedCompatibility.SupplyFailed(job, "Unable to withdraw the complete ingredient batch.");
                        foreach (var request in transfer.Requests)
                        {
                            bool accepted = false;
                            if (!transfer.Deliver(request, item =>
                            {
                                if (container.GetCountCanAccept(item, false) < request.Count) return false;
                                receipts.Add(new ContainerReceipt { Item = item, Staging = item.holdingOwner, Count = request.Count });
                                accepted = StorageTransfer.Move(item.holdingOwner, container, item, request.Count,
                                    map, table.InteractionCell, merge: false) == request.Count;
                                return accepted;
                            }) || !accepted || request.DeliveredCount != request.Count)
                                return FortifiedCompatibility.SupplyFailed(job, "Machine did not accept the complete ingredient transfer.");
                        }
                        // Validate the whole context once at commit, rather than
                        // rescanning all entries before each individual transfer.
                        if (pawn.CurJob != job || table.Map != map || !ValidContext(job, pawn, table))
                            return FortifiedCompatibility.SupplyFailed(job, "Task or supply network changed during transfer.");
                        foreach (var receipt in receipts)
                            if (receipt.Item.Destroyed || receipt.Item.holdingOwner != container ||
                                receipt.Item.stackCount != receipt.Count)
                                return FortifiedCompatibility.SupplyFailed(job, "Transferred ingredient ownership or quantity changed.");
                        completed = true;
                        return true;
                    }
                    finally
                    {
                        if (!completed)
                            foreach (var receipt in receipts)
                            {
                                try
                                {
                                    if (!receipt.Item.Destroyed && receipt.Item.holdingOwner == container)
                                        StorageTransfer.Move(container, receipt.Staging, receipt.Item,
                                            receipt.Item.stackCount, map, table.InteractionCell, merge: false);
                                }
                                catch (Exception exception) { VanillaBillStorage.Report(exception); }
                            }
                    }
                }
            }
            finally
            {
                preparing = false;
                // A failed staging attempt may still have withdrawn and restored
                // part of the batch. Failures before staging cannot change stock.
                if (withdrawalAttempted) map.resourceCounter.UpdateResourceCounts();
            }
        }
    }
}
