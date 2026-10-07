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
            var stocks = new Dictionary<ThingDef, Thing>();
            var items = new List<Thing>(shelf.GetSlotGroup().HeldThings);
            if (shelf.Position.ContainsStaticFire(shelf.Map))
            {
                for (int i = 0; i < Building_StorageSupplyShelf.SlotCount; i++)
                {
                    Thing stock = items.Find(t => shelf.AllowsStock(i, t));
                    if (stock != null && (Reserved(shelf, stock) || stock.IsForbidden(shelf.Faction))) continue;
                    shelf.RecordRestockFailure(i, stock?.stackCount ?? 0, StorageShelfFailureReason.Burning);
                }
                return;
            }
            // Prefer a reserved stack as the retained representative of a configured type.
            items.Sort((a, b) => Reserved(shelf, b).CompareTo(Reserved(shelf, a)));
            var configured = new Dictionary<ThingDef, int>();
            for (int i = 0; i < Building_StorageSupplyShelf.SlotCount; i++)
                if (shelf.SelectedDef(i) != null) configured.Add(shelf.SelectedDef(i), i);
            foreach (Thing item in items)
            {
                if (configured.TryGetValue(item.def, out int slot) && shelf.AllowsStock(slot, item) &&
                    !stocks.ContainsKey(item.def)) stocks.Add(item.def, item);
                else if (!Reserved(shelf, item) && !item.IsForbidden(shelf.Faction)) RemoveStock(shelf, item);
            }
            for (int i = 0; i < Building_StorageSupplyShelf.SlotCount; i++)
            {
                ThingDef itemDef = shelf.SelectedDef(i);
                int limit = shelf.RestockLimit(i);
                if (itemDef == null || limit == 0) continue;
                stocks.TryGetValue(itemDef, out Thing stock);
                if (stock != null && stock.stackCount >= limit) continue;
                // A rejected but reserved/forbidden stack keeps its type's one stack occupied.
                if (stock == null && items.Exists(t => t.Spawned && t.Map == shelf.Map && t.Position == shelf.Position && t.def == itemDef)) continue;
                if (stock != null && (Reserved(shelf, stock) || stock.IsForbidden(shelf.Faction))) continue;
                StorageShelfFailureReason reason = shelf.CanWork
                    ? Refill(shelf, i, itemDef, ref stock, limit) : StorageShelfFailureReason.NetworkUnavailable;
                shelf.RecordRestockFailure(i, stock?.stackCount ?? 0, reason);
            }
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
            var sources = new List<Thing>();
            StorageNetwork network = shelf.Network;
            network.GetStacks(itemDef, sources);
            bool matched = false, compatible = false, placeable = false, reservedStock = false;
            bool transferFailed = false, placementFailed = false;
            foreach (Thing source in sources)
            {
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
                if ((stock == null && shelf.GetSlotGroup().HeldThingsCount >= Building_StorageSupplyShelf.SlotCount) ||
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
                    if (batch.Contents.TryDrop(received, shelf.Position, shelf.Map, ThingPlaceMode.Direct,
                        out Thing placed, playDropSound: false)) stock = placed;
                    else
                    {
                        placementFailed = true;
                        if (!received.Destroyed && batch.Contents.Contains(received))
                            network.TryStore(batch.Contents, received, received.stackCount);
                    }
                }
                finally { network.ReleaseOutgoing(claim); manager.ReleaseRecovery(batch); }
            }
            if ((stock?.stackCount ?? 0) >= limit) return StorageShelfFailureReason.None;
            if (!shelf.CanWork || shelf.Network != network) return StorageShelfFailureReason.NetworkUnavailable;
            if (placementFailed) return StorageShelfFailureReason.PlacementFailed;
            if (transferFailed) return StorageShelfFailureReason.TransferFailed;
            if (!matched) return sources.Count == 0 ? StorageShelfFailureReason.NoStock : StorageShelfFailureReason.FilterMismatch;
            if (!compatible) return StorageShelfFailureReason.IncompatibleStack;
            if (!placeable && !reservedStock) return StorageShelfFailureReason.NoSpace;
            if (reservedStock) return StorageShelfFailureReason.ReservedStock;
            return StorageShelfFailureReason.NoStock;
        }
    }
}
