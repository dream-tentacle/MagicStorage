using System;
using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    internal sealed class StorageTradeRequest
    {
        internal object Line;
        internal StorageNetwork Network;
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
        private readonly Dictionary<Thing, StorageNetwork> collectedSources = new Dictionary<Thing, StorageNetwork>();
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
                        !seen.Add(request.Item) || request.Network.ReserveOutgoing(request, request.Item,
                            request.Count, request.Network.Core, this, request.Usable ?? usable) != request.Count)
                    { Dispose(); return false; }
                    byItem.Add(request.Item, request);
                }
                if (!Valid()) { Dispose(); return false; }
                return true;
            }
            catch { Dispose(); throw; }
        }

        internal int Available(StorageNetwork network, Thing item)
        {
            if (byItem.TryGetValue(item, out var request) && !request.Collected && request.Network == network)
                return network.AvailableToWithdraw(item, request);
            return network.AvailableToWithdraw(item);
        }

        internal bool Valid()
        {
            if (disposed || invalid || (usable != null && !usable())) return false;
            foreach (var request in requests)
                if (!request.Collected && ((request.Usable != null && !request.Usable()) ||
                    !request.Network.HasOutgoingReservation(request, request.Item, request.Count)))
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
                var transfer = request.Network.TryWithdrawReserved(request, request.Item, request.Count,
                    destination, out Thing received);
                if (received != null) { result.Add(received); collectedSources[received] = request.Network; }
                if (!transfer.Complete) { invalid = true; return false; }
                request.Collected = true;
            }
            return true;
        }

        internal StorageNetwork SourceFor(Thing received) =>
            collectedSources.TryGetValue(received, out var network) ? network : null;

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
