using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal sealed class StorageTradeRequest
    {
        internal object Line;
        internal StorageNetwork Network;
        internal Building_StorageSupplyShelf Shelf;
        internal Building_StorageUnit Unit;
        internal IntVec3 Cell;
        internal Thing Item;
        internal int Count;
        internal bool Collected;
        internal Func<bool> Usable;
    }

    // One owner per real stack: a claim cannot replace another stack's claim.
    internal sealed class StorageTradeTransaction : IDisposable, IStorageReservationClient
    {
        private static readonly Dictionary<Thing, List<StorageTradeRequest>> directClaims = new Dictionary<Thing, List<StorageTradeRequest>>();
        private readonly List<StorageTradeRequest> requests;
        private readonly Func<bool> usable;
        private readonly Dictionary<Thing, StorageTradeRequest> collectedSources = new Dictionary<Thing, StorageTradeRequest>();
        private readonly Dictionary<Thing, StorageTradeRequest> byItem = new Dictionary<Thing, StorageTradeRequest>();
        private bool invalid;
        private bool disposed;
        internal bool IsDisposed => disposed;
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
                    if (request.Item == null || (request.Network == null && request.Unit == null) || request.Count <= 0)
                    { Fail("MS_Trade_Fail_InvalidRequest", request); Dispose(); return false; }
                    if (!seen.Add(request.Item))
                    { Fail("MS_Trade_Fail_DuplicateItem", request); Dispose(); return false; }
                    if (request.Unit != null)
                    {
                        int available = PortableAvailable(request.Unit, request.Item);
                        if (available < request.Count)
                        { Fail("MS_Trade_Fail_ReserveCount", request, available); Dispose(); return false; }
                        if (!directClaims.TryGetValue(request.Item, out var claims))
                            directClaims.Add(request.Item, claims = new List<StorageTradeRequest>());
                        claims.Add(request);
                    }
                    else if (request.Shelf != null)
                    {
                        int available = ShelfAvailable(request.Network, request.Shelf, request.Item);
                        if (available < request.Count)
                        { Fail("MS_Trade_Fail_ShelfCount", request, available); Dispose(); return false; }
                        request.Cell = request.Shelf.Position;
                        if (!directClaims.TryGetValue(request.Item, out var claims))
                            directClaims.Add(request.Item, claims = new List<StorageTradeRequest>());
                        claims.Add(request);
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

        internal static int ShelfAvailable(StorageNetwork network, Building_StorageSupplyShelf shelf, Thing item,
            StorageTradeRequest owner = null) =>
            shelf != null && shelf.Spawned && shelf.Network == network && item != null && item.Spawned &&
            item.Map == shelf.Map && item.Position == shelf.Position &&
            StorageMaterialTransaction.GroundAvailable(shelf.Map, item)
                ? DirectAvailable(item, NetworkCraftingMaterialPolicy.Available(network, item), owner) : 0;

        internal static int PortableAvailable(Building_StorageUnit unit, Thing item, StorageTradeRequest owner = null)
        {
            if (unit == null || unit.Spawned || unit.Destroyed || unit.Faction != Faction.OfPlayer ||
                item == null || item.Destroyed || item.holdingOwner != unit.GetDirectlyHeldThings()) return 0;
            return DirectAvailable(item, item.stackCount, owner);
        }

        private static int DirectAvailable(Thing item, int available, StorageTradeRequest owner)
        {
            long reserved = 0;
            if (directClaims.TryGetValue(item, out var claims))
            {
                claims.RemoveAll(claim => claim.Collected || claim.Usable?.Invoke() == false);
                foreach (var claim in claims) if (!ReferenceEquals(claim, owner)) reserved += claim.Count;
                if (claims.Count == 0) directClaims.Remove(item);
            }
            return (int)Math.Max(0, available - reserved);
        }

        internal int AvailablePortable(Building_StorageUnit unit, Thing item)
        {
            byItem.TryGetValue(item, out var request);
            return PortableAvailable(unit, item, request);
        }

        internal int Available(StorageNetwork network, Thing item, Building_StorageSupplyShelf shelf = null)
        {
            if (shelf != null)
            {
                byItem.TryGetValue(item, out var owner);
                return ShelfAvailable(network, shelf, item, owner);
            }
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
                if (request.Unit != null)
                {
                    if (!directClaims.TryGetValue(request.Item, out var claims) || !claims.Contains(request) ||
                        PortableAvailable(request.Unit, request.Item, request) < request.Count)
                        return Fail("MS_Trade_Fail_ReservationChanged", request);
                }
                else if (request.Shelf != null)
                {
                    int available = ShelfAvailable(request.Network, request.Shelf, request.Item, request);
                    if (!directClaims.TryGetValue(request.Item, out var claims) || !claims.Contains(request) ||
                        request.Shelf.Position != request.Cell || available < request.Count)
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
                Thing received = null;
                bool complete;
                try
                {
                    complete = request.Unit != null ? CollectPortable(request, destination, out received) :
                        request.Shelf != null ? CollectShelf(request, destination, out received) :
                        request.Network.TryWithdrawReserved(request, request.Item, request.Count, destination, out received).Complete;
                }
                finally
                {
                    if (received != null) { result.Add(received); collectedSources[received] = request; }
                }
                if (!complete) { invalid = true; return false; }
                request.Collected = true;
            }
            return true;
        }

        private static bool CollectPortable(StorageTradeRequest request, ThingOwner destination, out Thing received)
        {
            received = null;
            Thing part = null;
            try
            {
                part = request.Unit.Inventory.Contents.Take(request.Item, request.Count);
                return part != null && destination.TryAdd(part, false) && part.stackCount == request.Count;
            }
            finally
            {
                // Portable recovery cannot depend on a map. Preserve even a part accepted before a notification throws.
                if (part != null && !part.Destroyed)
                {
                    if (destination.Contains(part)) received = part;
                    else if (part.holdingOwner == null && !part.Spawned) RestorePortable(request, part);
                }
                request.Unit.Inventory.RefreshStack(request.Item);
            }
        }

        private static void RestorePortable(StorageTradeRequest request, Thing part)
        {
            var source = request.Unit.Inventory.Contents;
            try
            {
                if (source.Contains(request.Item) && request.Item != part) request.Item.TryAbsorbStack(part, false);
                if (!part.Destroyed && part.holdingOwner == null && !source.RestoreTradeItem(part))
                    throw new InvalidOperationException("Unable to restore portable trade goods.");
            }
            finally { request.Unit.Inventory.RefreshStack(request.Item); }
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
            if (request.Unit != null)
            {
                RestorePortable(request, holder.Take(received, received.stackCount));
                return;
            }
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
            foreach (var request in requests)
            {
                request.Network?.ReleaseOutgoing(request);
                if (request.Item != null && directClaims.TryGetValue(request.Item, out var claims))
                {
                    claims.Remove(request);
                    if (claims.Count == 0) directClaims.Remove(request.Item);
                }
            }
        }
    }
}
