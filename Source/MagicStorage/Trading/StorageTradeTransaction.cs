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
        internal string FailureKey { get; private set; }
        internal StorageTradeRequest FailedRequest { get; private set; }
        internal int FailureAvailable { get; private set; }

        private bool Fail(string key, StorageTradeRequest request = null, int available = 0)
        {
            FailureKey = key; FailedRequest = request; FailureAvailable = available;
            return false;
        }

        internal StorageTradeTransaction(List<StorageTradeRequest> requests, Func<bool> usable)
        { this.requests = requests; this.usable = usable; }

        internal bool Reserve()
        {
            if (disposed || byItem.Count > 0) return Fail("MS_Trade_Fail_ReservationClosed");
            try
            {
                var seen = new HashSet<Thing>();
                foreach (var request in requests)
                {
                    if (request.Item == null || request.Network == null || request.Count <= 0)
                    { Fail("MS_Trade_Fail_InvalidRequest", request); Dispose(); return false; }
                    if (!seen.Add(request.Item))
                    { Fail("MS_Trade_Fail_DuplicateItem", request); Dispose(); return false; }
                    if (request.Shelf != null)
                    {
                        int available = ShelfAvailable(request.Network, request.Shelf, request.Item);
                        if (available < request.Count)
                        { Fail("MS_Trade_Fail_ShelfCount", request, available); Dispose(); return false; }
                        request.Cell = request.Shelf.Position;
                    }
                    else
                    {
                        int reserved = request.Network.ReserveOutgoing(request, request.Item,
                            request.Count, request.Network.Core, this, request.Usable ?? usable);
                        if (reserved != request.Count)
                        { Fail("MS_Trade_Fail_ReserveCount", request, reserved); Dispose(); return false; }
                    }
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
            if (disposed) return Fail("MS_Trade_Fail_ReservationClosed");
            if (invalid) return false;
            if (usable != null && !usable()) return Fail("MS_Trade_Fail_ReservationSession");
            foreach (var request in requests)
            {
                if (request.Collected) continue;
                if (request.Usable != null && !request.Usable())
                    return Fail("MS_Trade_Fail_ItemPolicyChanged", request);
                if (request.Shelf != null)
                {
                    int available = ShelfAvailable(request.Network, request.Shelf, request.Item);
                    if (request.Shelf.Position != request.Cell || available < request.Count)
                        return Fail("MS_Trade_Fail_ShelfCount", request, available);
                }
                else if (!request.Network.HasOutgoingReservation(request, request.Item, request.Count))
                    return Fail("MS_Trade_Fail_ReservationChanged", request);
            }
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
        {
            invalid = true;
            Fail("MS_Trade_Fail_ReservationChanged", reservation.Owner as StorageTradeRequest);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var request in requests) request.Network?.ReleaseOutgoing(request);
        }
    }
}
