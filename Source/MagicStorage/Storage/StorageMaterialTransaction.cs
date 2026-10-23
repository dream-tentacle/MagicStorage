using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Runtime-only coordinator. Real items are staged in the existing saved recovery
    // holder; this class introduces no new save data or replacement inventory objects.
    internal sealed class StorageMaterialTransaction : IDisposable
    {
        internal sealed class Request
        {
            internal Thing Item, Received;
            internal ThingOwner Source;
            internal StorageNetwork Network;
            internal Thing Endpoint;
            internal int Count;
            internal IntVec3 Cell;
            internal bool Submitted;
            internal int DeliveredCount;
            internal bool Delivered => DeliveredCount > 0;
        }

        private readonly Map map;
        private readonly IntVec3 recoveryCell;
        private readonly MapComponent_StorageNetworks manager;
        private readonly bool respectGroundReservations;
        private readonly List<Request> requests = new List<Request>();
        private readonly Dictionary<Thing, Request> byItem = new Dictionary<Thing, Request>();
        private StorageRecoveryBatch staging;
        private bool attempted, staged, disposed;
        internal IReadOnlyList<Request> Requests => requests;

        internal StorageMaterialTransaction(Map map, IntVec3 recoveryCell, bool respectGroundReservations = true)
        {
            this.map = map;
            this.recoveryCell = recoveryCell;
            this.respectGroundReservations = respectGroundReservations;
            manager = map.GetComponent<MapComponent_StorageNetworks>();
        }

        internal bool Add(Thing item, int count, StorageNetwork network = null, Thing endpoint = null)
        {
            if (disposed || attempted || item == null || count <= 0) return false;
            if (byItem.TryGetValue(item, out var existing))
            {
                if (existing.Network != network || existing.Endpoint != endpoint ||
                    (long)existing.Count + count > Available(existing)) return false;
                existing.Count += count;
                return true;
            }
            var unit = item.holdingOwner?.Owner as Building_StorageUnit;
            var request = new Request { Item = item, Source = item.holdingOwner, Network = network,
                Endpoint = endpoint, Count = count, Cell = unit == null ? item.Position : unit.Position };
            if (Available(request) < count) return false;
            requests.Add(request); byItem.Add(item, request);
            return true;
        }

        internal static bool GroundAvailable(Map map, Thing item, bool respectReservations = true) =>
            item != null && !item.Destroyed && item.Spawned && item.Map == map && item.stackCount > 0 &&
            item.holdingOwner == null && (!respectReservations ||
                (!map.reservationManager.IsReservedByAnyoneOf(item, Faction.OfPlayer) &&
                 !map.physicalInteractionReservationManager.IsReserved(item)));

        private int Available(Request request)
        {
            if (request.Network == null)
                return GroundAvailable(map, request.Item, respectGroundReservations) && request.Item.Position == request.Cell ? request.Item.stackCount : 0;
            var unit = request.Source?.Owner as Building_StorageUnit;
            if (unit == null || !unit.Spawned || unit.Destroyed || unit.Map != map || unit.Faction != Faction.OfPlayer ||
                unit.Network != request.Network || request.Item.holdingOwner != request.Source) return 0;
            return request.Network.AvailableToWithdraw(request.Item);
        }

        internal bool TryStage()
        {
            if (disposed || attempted || manager == null || requests.Count == 0) return false;
            attempted = true;
            manager.EnsureCurrent();
            foreach (var request in requests)
                if (Available(request) < request.Count) return false;
            // One reservation owner per stack. A later reservation failure cannot leave
            // a partly extracted batch; Dispose releases every earlier claim.
            foreach (var request in requests)
                if (request.Network != null && request.Network.ReserveOutgoing(request,
                    request.Item, request.Count, request.Endpoint) != request.Count) return false;
            staging = manager.CreateRecovery(recoveryCell == IntVec3.Invalid ? requests[0].Cell : recoveryCell);
            foreach (var request in requests)
            {
                if (request.Network != null)
                {
                    if (!request.Network.TryWithdrawReserved(request, request.Item, request.Count,
                        staging.Contents, out request.Received).Complete) return false;
                }
                else
                {
                    if (Available(request) < request.Count) return false;
                    request.Received = request.Item.SplitOff(request.Count);
                    if (request.Received == null || !staging.Contents.TryAdd(request.Received, false)) return false;
                }
            }
            foreach (var request in requests)
                if (!Held(request, request.Count)) return false;
            staged = true;
            return true;
        }

        private bool Held(Request request, int count) => request.Received != null && !request.Received.Destroyed &&
            staging != null && staging.Contents.Contains(request.Received) && request.Received.stackCount >= count;

        private bool CanSubmit(Request request, int count) => !disposed && staged && request != null && !request.Submitted &&
            requests.Contains(request) && count > 0 && count <= request.Count && Held(request, count);

        internal bool Consume(Request request, int count)
        {
            if (!CanSubmit(request, count)) return false;
            request.Submitted = true;
            Thing consumed = null;
            try
            {
                consumed = request.Received.SplitOff(count);
                if (consumed == null) return false;
                consumed.Destroy(DestroyMode.Vanish);
                return consumed.Destroyed;
            }
            finally
            {
                // Keep ownership even if a custom destruction callback rejects/throws.
                // Already destroyed quantities are never reconstructed or refunded.
                if (consumed != null && !consumed.Destroyed && !consumed.Spawned && consumed.holdingOwner == null)
                {
                    if (consumed != request.Received && Held(request, 1)) request.Received.TryAbsorbStack(consumed, true);
                    if (!consumed.Destroyed && consumed.holdingOwner == null && !staging.Contents.TryAdd(consumed, false))
                        manager.PreserveLooseThing(consumed, request.Cell);
                }
            }
        }

        internal bool ConsumeAll()
        {
            if (disposed || !staged || requests.Count == 0) return false;
            // Validate the complete batch before the first irreversible operation.
            foreach (var request in requests)
                if (!CanSubmit(request, request.Count)) return false;
            foreach (var request in requests)
                if (!Consume(request, request.Count)) return false;
            return true;
        }

        internal bool Deliver(Request request, Func<Thing, bool> receiver)
        {
            if (!CanSubmit(request, request.Count) || receiver == null) return false;
            request.Submitted = true;
            try { receiver(request.Received); }
            finally
            {
                Thing item = request.Received;
                // Ownership is authoritative even if the receiver returns false or
                // throws after accepting the real item. A true return alone is not proof.
                bool left = item.Destroyed || item.Spawned ||
                    (item.holdingOwner != null && item.holdingOwner != staging.Contents && item.holdingOwner != request.Source);
                request.DeliveredCount = left ? request.Count : Math.Max(0, request.Count - item.stackCount);
            }
            return request.Delivered;
        }

        private void Restore(Request request)
        {
            Thing item = request.Received;
            if (item == null || item.Destroyed || item.Spawned) return;
            if (item.holdingOwner == null && !staging.Contents.TryAdd(item, false))
            { manager.PreserveLooseThing(item, request.Cell); return; }
            if (!staging.Contents.Contains(item)) return;
            if (request.Source != null)
            {
                var unit = request.Source.Owner as Building_StorageUnit;
                if (unit != null && unit.Spawned && !unit.Destroyed && unit.Map == map)
                    StorageTransfer.Move(staging.Contents, request.Source, item, item.stackCount, map, request.Cell);
            }
            else
            {
                if (GroundAvailable(map, request.Item, respectGroundReservations) && request.Item.Position == request.Cell)
                    request.Item.TryAbsorbStack(item, true);
                if (!item.Destroyed && staging.Contents.Contains(item))
                    staging.Contents.TryDrop(item, request.Cell, map, ThingPlaceMode.Direct, out _, playDropSound: false);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var request in requests)
            {
                try { request.Network?.ReleaseOutgoing(request); }
                catch (Exception exception) { Log.Error("[MagicStorage] Material reservation release failed: " + exception); }
            }
            if (staging == null) return;
            try
            {
                foreach (var request in requests)
                {
                    try { Restore(request); }
                    catch (Exception exception) { Log.Error("[MagicStorage] Material restoration failed: " + exception); }
                }
            }
            finally { manager.ReleaseRecovery(staging); }
        }
    }
}
