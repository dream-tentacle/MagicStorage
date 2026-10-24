using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal static class StorageShelfSupply
    {
        internal static bool Reserved(Building_StorageSupplyShelf shelf, Thing item) =>
            shelf.Map.reservationManager.IsReservedByAnyoneOf(item, shelf.Faction) ||
            shelf.Map.physicalInteractionReservationManager.IsReserved(item);

        internal static void Process(Building_StorageSupplyShelf shelf)
        {
            shelf.ClearRestockFailures();
            if (!shelf.Spawned || shelf.Faction != Faction.OfPlayer ||
                shelf.IsForbidden(Faction.OfPlayer)) return;
            var items = new List<Thing>(shelf.GetSlotGroup().HeldThings);
            items.Sort((a, b) => Reserved(shelf, b).CompareTo(Reserved(shelf, a)));
            shelf.ResolveStockSlots(items);
            if (shelf.Position.ContainsStaticFire(shelf.Map))
            {
                for (int i = 0; i < shelf.SlotCount; i++)
                {
                    Thing stock = shelf.StockAt(i);
                    if (stock != null && (Reserved(shelf, stock) || stock.IsForbidden(shelf.Faction))) continue;
                    shelf.RecordRestockFailure(i, stock?.stackCount ?? 0, StorageShelfFailureReason.Burning);
                }
                return;
            }
            foreach (Thing item in items)
            {
                int slot = shelf.StockSlot(item);
                if ((slot < 0 || !shelf.AllowsStock(slot, item)) &&
                    !Reserved(shelf, item) && !item.IsForbidden(shelf.Faction)) RemoveStock(shelf, item);
            }
            for (int i = 0; i < shelf.SlotCount; i++)
            {
                Thing stock = shelf.StockAt(i);
                if (stock != null && !shelf.AllowsStock(i, stock)) continue;
                ThingDef itemDef = shelf.FilterMode ? stock?.def : shelf.SelectedDef(i);
                if (shelf.FilterMode && stock == null && shelf.FilterLimit(i) > 0 && shelf.CanWork)
                {
                    Thing candidate = null;
                    shelf.Network.VisitStoredStacksMatching(d => shelf.SharedFilter.Allows(d), t =>
                    {
                        if (shelf.AllowsStock(i, t) && shelf.Network.AvailableToWithdraw(t) > 0)
                        { candidate = t; return false; }
                        return true;
                    });
                    itemDef = candidate?.def;
                }
                int limit = shelf.SupplyLimit(i, itemDef);
                if (itemDef == null || limit == 0) continue;
                if (stock != null && stock.stackCount >= limit) continue;
                if (stock != null && (Reserved(shelf, stock) || stock.IsForbidden(shelf.Faction))) continue;
                StorageShelfFailureReason reason = shelf.CanWork
                    ? Refill(shelf, i, itemDef, ref stock, limit) : StorageShelfFailureReason.NetworkUnavailable;
                shelf.RecordRestockFailure(i, stock?.stackCount ?? 0, reason);
                shelf.SetStock(i, stock);
            }
        }

        internal static bool TryReturnAll(Building_StorageSupplyShelf shelf)
        {
            if (!shelf.Spawned) return true;
            shelf.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            var items = new List<Thing>(shelf.GetSlotGroup().HeldThings);
            if (items.Count == 0) return true;
            if (!shelf.CanWork) return false;
            StorageNetwork network = shelf.Network;
            // Reject known failures before touching any item or native pickup reservation.
            foreach (Thing item in items)
                if (Reserved(shelf, item) || item.IsForbidden(shelf.Faction) ||
                    network.GetCountCanAccept(item) < item.stackCount) return false;
            foreach (Thing item in items)
            {
                if (!shelf.CanWork || shelf.Network != network) return false;
                var manager = shelf.Map.GetComponent<MapComponent_StorageNetworks>();
                var batch = manager.CreateRecovery(shelf.Position);
                batch.ExcludeCell(shelf.Position);
                bool complete = false;
                try
                {
                    item.DeSpawn();
                    if (batch.Contents.TryAdd(item, false))
                        complete = network.TryStore(batch.Contents, item, item.stackCount).Complete;
                }
                finally
                {
                    // A failed or partial transfer keeps the remainder on the same shelf.
                    if (!item.Destroyed && !item.Spawned &&
                        (item.holdingOwner == null || batch.Contents.Contains(item)))
                        GenSpawn.Spawn(item, shelf.Position, shelf.Map, Rot4.North);
                    manager.ReleaseRecovery(batch);
                }
                if (!complete) return false;
            }
            return true;
        }

        private static void RemoveStock(Building_StorageSupplyShelf shelf, Thing item)
        {
            var manager = shelf.Map.GetComponent<MapComponent_StorageNetworks>();
            var batch = manager.CreateRecovery(shelf.Position);
            batch.ExcludeCell(shelf.Position);
            try
            {
                item.DeSpawn();
                if (!batch.Contents.TryAdd(item, false))
                { manager.PreserveLooseThing(item, shelf.Position); return; }
                if (shelf.CanWork) shelf.Network.TryStore(batch.Contents, item, item.stackCount);
            }
            finally { manager.ReleaseRecovery(batch); }
        }

        private static StorageShelfFailureReason Refill(Building_StorageSupplyShelf shelf, int slot, ThingDef itemDef, ref Thing stock, int limit)
        {
            StorageNetwork network = shelf.Network;
            bool anySource = false;
            bool matched = false, compatible = false, placeable = false, reservedStock = false;
            bool transferFailed = false, placementFailed = false;
            foreach (Thing source in network.EnumerateStacksForTransfer(itemDef))
            {
                anySource = true;
                if (!shelf.CanWork || shelf.Network != network) break;
                int missing = Math.Max(0, limit - (stock?.stackCount ?? 0));
                if (missing == 0) break;
                if (!shelf.AllowsStock(slot, source)) continue;
                matched = true;
                if (stock != null && !stock.CanStackWith(source)) continue;
                compatible = true;
                int available = network.AvailableToWithdraw(source);
                if (available < source.stackCount) reservedStock = true;
                if (available <= 0) continue;
                if ((stock == null && shelf.GetSlotGroup().HeldThingsCount >= shelf.SlotCount) ||
                    !StoreUtility.IsGoodStoreCell(shelf.Position, shelf.Map, source, null, shelf.Faction)) continue;
                placeable = true;
                int count = Math.Min(missing, available);
                if (count <= 0) continue;
                object claim = new object();
                int reserved = network.ReserveOutgoing(claim, source, count, shelf);
                if (reserved <= 0) { reservedStock = true; continue; }
                var manager = shelf.Map.GetComponent<MapComponent_StorageNetworks>();
                var batch = manager.CreateRecovery(shelf.Position);
                batch.ExcludeCell(shelf.Position);
                try
                {
                    network.TryWithdrawReserved(claim, source, reserved, batch.Contents, out Thing received);
                    if (received == null) { transferFailed = true; continue; }
                    received.SetForbidden(false, false);
                    // Direct mode never searches adjacent cells. The saved batch protects a failed placement.
                    if (PlaceStock(shelf, batch.Contents, received, ref stock)) shelf.SetStock(slot, stock);
                    else
                    {
                        placementFailed = true;
                        if (!received.Destroyed && batch.Contents.Contains(received))
                            network.TryStore(batch.Contents, received, received.stackCount);
                    }
                }
                finally { network.ReleaseOutgoing(claim); manager.ReleaseRecovery(batch); }
                if ((stock?.stackCount ?? 0) >= limit) break;
            }
            if ((stock?.stackCount ?? 0) >= limit) return StorageShelfFailureReason.None;
            if (!shelf.CanWork || shelf.Network != network) return StorageShelfFailureReason.NetworkUnavailable;
            if (placementFailed) return StorageShelfFailureReason.PlacementFailed;
            if (transferFailed) return StorageShelfFailureReason.TransferFailed;
            if (!matched) return anySource ? StorageShelfFailureReason.FilterMismatch : StorageShelfFailureReason.NoStock;
            if (!compatible) return StorageShelfFailureReason.IncompatibleStack;
            if (!placeable && !reservedStock) return StorageShelfFailureReason.NoSpace;
            if (reservedStock) return StorageShelfFailureReason.ReservedStock;
            return StorageShelfFailureReason.NoStock;
        }

        private static bool PlaceStock(Building_StorageSupplyShelf shelf, ThingOwner holder, Thing received, ref Thing stock)
        {
            bool couldMergeIntoAnotherSlot = false;
            foreach (Thing existing in shelf.GetSlotGroup().HeldThings)
                if (existing != stock && existing.CanStackWith(received))
                { couldMergeIntoAnotherSlot = true; break; }
            if (!couldMergeIntoAnotherSlot)
            {
                if (!holder.TryDrop(received, shelf.Position, shelf.Map, ThingPlaceMode.Direct,
                    out Thing placed, playDropSound: false)) return false;
                stock = placed;
                return true;
            }
            // Native direct dropping merges into the first compatible stack in the cell.
            // Duplicate selections need an exact destination or a distinct physical stack.
            if (stock != null) return stock.TryAbsorbStack(received, true);
            stock = GenSpawn.Spawn(received, shelf.Position, shelf.Map, Rot4.North);
            return stock != null && stock.Spawned;
        }
    }
}
