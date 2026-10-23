using System;
using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    internal sealed class StorageTradeRequest
    {
        internal object Line;
        internal StorageNetwork Network;
        internal Building_StorageSupplyShelf Shelf;
        internal IntVec3 Cell;
        internal Thing Item;
        internal int Count;
        internal bool Collected;
        internal Func<bool> Usable;
    }

    // One owner per real stack: a claim cannot replace another stack's claim.
    internal sealed class StorageTradeTransaction : IDisposable, IStorageReservationClient
    {
        private readonly List<StorageTradeRequest> requests;
        private readonly Func<bool> usable;
        private readonly Dictionary<Thing, StorageTradeRequest> collectedSources = new Dictionary<Thing, StorageTradeRequest>();
        private readonly Dictionary<Thing, StorageTradeRequest> byItem = new Dictionary<Thing, StorageTradeRequest>();
        private bool invalid;
        private bool disposed;

        internal StorageTradeTransaction(List<StorageTradeRequest> requests, Func<bool> usable)
        { this.requests = requests; this.usable = usable; }

        internal bool Reserve()
        {
            if (disposed || byItem.Count > 0) return false;
            try
            {
                var seen = new HashSet<Thing>();
                foreach (var request in requests)
                {
                    if (request.Item == null || request.Network == null || request.Count <= 0 ||
                        !seen.Add(request.Item))
                    { Dispose(); return false; }
                    if (request.Shelf != null)
                    {
                        if (ShelfAvailable(request.Network, request.Shelf, request.Item) < request.Count)
                        { Dispose(); return false; }
                        request.Cell = request.Shelf.Position;
                    }
                    else if (request.Network.ReserveOutgoing(request, request.Item,
                        request.Count, request.Network.Core, this, request.Usable ?? usable) != request.Count)
                    { Dispose(); return false; }
                    byItem.Add(request.Item, request);
                }
                if (!Valid()) { Dispose(); return false; }
                return true;
            }
            catch { Dispose(); throw; }
        }

        internal static int ShelfAvailable(StorageNetwork network, Building_StorageSupplyShelf shelf, Thing item) =>
            shelf != null && shelf.Spawned && shelf.Network == network && item != null && item.Spawned &&
            item.Map == shelf.Map && item.Position == shelf.Position &&
            StorageMaterialTransaction.GroundAvailable(shelf.Map, item)
                ? NetworkCraftingMaterialPolicy.Available(network, item) : 0;

        internal int Available(StorageNetwork network, Thing item, Building_StorageSupplyShelf shelf = null)
        {
            if (shelf != null) return ShelfAvailable(network, shelf, item);
            if (byItem.TryGetValue(item, out var request) && !request.Collected && request.Network == network)
                return network.AvailableToWithdraw(item, request);
            return network.AvailableToWithdraw(item);
        }

        internal bool Valid()
        {
            if (disposed || invalid || (usable != null && !usable())) return false;
            foreach (var request in requests)
                if (!request.Collected && ((request.Usable != null && !request.Usable()) ||
                    (request.Shelf != null ? request.Shelf.Position != request.Cell ||
                        ShelfAvailable(request.Network, request.Shelf, request.Item) < request.Count :
                        !request.Network.HasOutgoingReservation(request, request.Item, request.Count))))
                    return false;
            return true;
        }

        internal long PendingFor(object line)
        {
            long count = 0;
            foreach (var request in requests)
                if (!request.Collected && ReferenceEquals(request.Line, line)) count += request.Count;
            return count;
        }

        internal bool Collect(object line, ThingOwner destination, List<Thing> result)
        {
            // Check every remaining claim before making the next irreversible delivery.
            if (!Valid()) return false;
            foreach (var request in requests)
            {
                if (request.Collected || !ReferenceEquals(request.Line, line)) continue;
                Thing received;
                bool complete = request.Shelf != null ? CollectShelf(request, destination, out received) :
                    request.Network.TryWithdrawReserved(request, request.Item, request.Count, destination, out received).Complete;
                if (received != null) { result.Add(received); collectedSources[received] = request; }
                if (!complete) { invalid = true; return false; }
                request.Collected = true;
            }
            return true;
        }

        private static bool CollectShelf(StorageTradeRequest request, ThingOwner destination, out Thing received)
        {
            // Trade execution is synchronous. Recheck every shelf before collection,
            // then use the shared transaction to stage real items and restore failures.
            received = null;
            using (var transfer = new StorageMaterialTransaction(request.Shelf.Map, request.Cell))
            {
                if (!transfer.Add(request.Item, request.Count) || !transfer.TryStage()) return false;
                Thing delivered = null;
                var material = transfer.Requests[0];
                bool complete = transfer.Deliver(material, item => StorageTransfer.Move(item.holdingOwner,
                    destination, item, request.Count, request.Shelf.Map, request.Cell, out delivered, false) == request.Count);
                received = delivered;
                return complete && material.DeliveredCount == request.Count;
            }
        }

        internal StorageNetwork SourceFor(Thing received) =>
            collectedSources.TryGetValue(received, out var request) ? request.Network : null;

        internal void Restore(ThingOwner holder, Thing received)
        {
            if (!holder.Contains(received) || !collectedSources.TryGetValue(received, out var request)) return;
            if (request.Shelf != null && request.Shelf.Spawned && request.Shelf.Network == request.Network &&
                request.Shelf.Position == request.Cell && holder.TryDrop(received, request.Cell, request.Shelf.Map,
                    ThingPlaceMode.Direct, out _, playDropSound: false)) return;
            request.Network.TryStore(holder, received, received.stackCount);
        }

        public void OnReservationInvalidated(StorageReservation reservation, StorageReservationFailure reason)
        { invalid = true; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var request in requests) request.Network?.ReleaseOutgoing(request);
        }
    }
}
