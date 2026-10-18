using System;
using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    // Claims refer to real stacks; ownership stays in the unit until collection.
    internal sealed class StorageOutgoingReservations
    {
        private readonly Dictionary<object, StorageReservation> claims = new Dictionary<object, StorageReservation>();
        private readonly Dictionary<Thing, HashSet<StorageReservation>> byItem = new Dictionary<Thing, HashSet<StorageReservation>>();
        private readonly Dictionary<Thing, HashSet<StorageReservation>> byNode = new Dictionary<Thing, HashSet<StorageReservation>>();
        private readonly Dictionary<Thing, long> reservedCounts = new Dictionary<Thing, long>();
        private readonly Queue<StorageReservation> notifications = new Queue<StorageReservation>();
        internal void Release(object owner)
        {
            if (owner == null || !claims.TryGetValue(owner, out var claim)) return;
            if (claim.State == StorageReservationState.Reserved) RemoveIndex(claim);
            claim.State = StorageReservationState.Released;
            claims.Remove(owner);
        }
        internal int CountFor(object owner, Thing item) => owner != null && claims.TryGetValue(owner, out var claim) &&
            claim.State == StorageReservationState.Reserved && claim.Item == item ? claim.Count : 0;
        internal int Available(Thing item, object except = null)
        {
            if (item == null || item.Destroyed) return 0;
            reservedCounts.TryGetValue(item, out long reserved);
            long count = item.stackCount - reserved;
            if (except != null && claims.TryGetValue(except, out var claim) &&
                claim.Item == item && claim.State == StorageReservationState.Reserved) count += claim.Count;
            return (int)Math.Max(0L, count);
        }
        internal bool IsReserved(Thing item) => Available(item) < (item?.stackCount ?? 0);
        internal int Reserve(object owner, Thing item, int requested, Building_StorageCore core,
            Thing endpoint, IStorageReservationClient client, Func<bool> usable)
        {
            if (owner == null) return 0;
            // An invalid request cannot silently replace itself before its owner handles failure.
            if (claims.TryGetValue(owner, out var existing) && existing.State == StorageReservationState.Invalid) return 0;
            int count = Math.Min(requested, Available(item, owner));
            if (count <= 0) { Release(owner); return 0; }
            if (existing != null && existing.Item == item && existing.Core == core && existing.Endpoint == endpoint)
            { RemoveIndex(existing); existing.Count = count; AddIndex(existing); return count; }
            Release(owner);
            var reservation = new StorageReservation { Owner = owner, Item = item, Count = count,
                Source = item.holdingOwner.Owner as Building_StorageUnit, Core = core,
                Endpoint = endpoint, Client = client, Usable = usable };
            claims[owner] = reservation;
            AddIndex(reservation);
            return count;
        }

        private static void AddBucket(Dictionary<Thing, HashSet<StorageReservation>> index, Thing key, StorageReservation claim)
        {
            if (key == null) return;
            if (!index.TryGetValue(key, out var bucket)) index.Add(key, bucket = new HashSet<StorageReservation>());
            bucket.Add(claim);
        }

        private static void RemoveBucket(Dictionary<Thing, HashSet<StorageReservation>> index, Thing key, StorageReservation claim)
        {
            if (key == null || !index.TryGetValue(key, out var bucket)) return;
            bucket.Remove(claim);
            if (bucket.Count == 0) index.Remove(key);
        }

        private void AddIndex(StorageReservation claim)
        {
            AddBucket(byItem, claim.Item, claim);
            AddBucket(byNode, claim.Source, claim); AddBucket(byNode, claim.Core, claim); AddBucket(byNode, claim.Endpoint, claim);
            reservedCounts.TryGetValue(claim.Item, out long total);
            reservedCounts[claim.Item] = total + claim.Count;
        }

        private void RemoveIndex(StorageReservation claim)
        {
            RemoveBucket(byItem, claim.Item, claim);
            RemoveBucket(byNode, claim.Source, claim); RemoveBucket(byNode, claim.Core, claim); RemoveBucket(byNode, claim.Endpoint, claim);
            long total = reservedCounts[claim.Item] - claim.Count;
            if (total == 0) reservedCounts.Remove(claim.Item);
            else reservedCounts[claim.Item] = total;
        }

        private void Invalidate(StorageReservation claim, StorageReservationFailure reason)
        {
            if (claim.State != StorageReservationState.Reserved) return;
            RemoveIndex(claim);
            claim.State = StorageReservationState.Invalid;
            claim.Failure = reason;
            notifications.Enqueue(claim);
        }

        internal void NotifyNodeUnavailable(Thing node)
        {
            if (node == null || !byNode.TryGetValue(node, out var bucket)) return;
            foreach (var claim in new List<StorageReservation>(bucket))
                Invalidate(claim, StorageReservationFailure.NodeUnavailable);
        }

        // Inventory callbacks only check physical availability; policy/topology checks wait
        // until the graph is current. No user code is called from a ThingOwner mutation.
        internal void ValidateInventory()
        {
            if (byItem.Count == 0) return;
            foreach (Thing item in new List<Thing>(byItem.Keys)) ValidateItem(item);
        }

        internal void ValidateInventoryFor(object owner, Thing item)
        {
            if (owner != null && claims.TryGetValue(owner, out var claim) && claim.Item != item)
                ValidateItem(claim.Item);
            ValidateItem(item);
        }

        private static StorageReservationFailure? InventoryFailure(StorageReservation claim, long total)
        {
            if (claim.Source == null || claim.Item.Destroyed || claim.Item.holdingOwner != claim.Source.Inventory.Contents ||
                claim.Item.stackCount <= 0) return StorageReservationFailure.ItemUnavailable;
            return total > claim.Item.stackCount ? StorageReservationFailure.InsufficientCount : (StorageReservationFailure?)null;
        }

        internal void ValidateItem(Thing item)
        {
            if (item == null || !byItem.TryGetValue(item, out var bucket)) return;
            long total = reservedCounts[item];
            // Valid inventory never mutates the bucket: avoid allocating a snapshot
            // on every count change, reservation request and collection.
            bool invalid = false;
            foreach (var claim in bucket)
                if (InventoryFailure(claim, total).HasValue) { invalid = true; break; }
            if (!invalid) return;
            foreach (var claim in new List<StorageReservation>(bucket))
            {
                // If an outside mutation overdraws a stack, invalidate all conflicting
                // claims rather than arbitrarily favoring dictionary iteration order.
                var failure = InventoryFailure(claim, total);
                if (failure.HasValue) Invalidate(claim, failure.Value);
            }
        }

        internal void Validate()
        {
            if (claims.Count == 0) return;
            ValidateInventory();
            foreach (var claim in new List<StorageReservation>(claims.Values))
                ValidatePolicy(claim);
        }

        internal void Validate(object owner)
        {
            if (owner == null || !claims.TryGetValue(owner, out var claim) || claim.State != StorageReservationState.Reserved) return;
            ValidateItem(claim.Item);
            ValidatePolicy(claim);
        }

        private void ValidatePolicy(StorageReservation claim)
        {
            if (claim.State != StorageReservationState.Reserved) return;
            var network = claim.Core?.Network;
            if (network == null || !network.CanWork || network.Core != claim.Core || claim.Source.Network != network ||
                (claim.Endpoint != null && !network.ContainsNode(claim.Endpoint)))
                Invalidate(claim, StorageReservationFailure.NetworkUnavailable);
            else if (claim.Usable != null && !claim.Usable())
                Invalidate(claim, StorageReservationFailure.PolicyChanged);
        }

        internal StorageReservation BeginCollection(object owner)
        {
            if (!claims.TryGetValue(owner, out var claim) || claim.State != StorageReservationState.Reserved) return null;
            // The source's normal removal notifications must not invalidate this collection.
            RemoveIndex(claim);
            claim.State = StorageReservationState.Collecting;
            return claim;
        }

        internal void FinishCollection(StorageReservation claim, bool collected)
        {
            claim.State = collected ? StorageReservationState.Collected : StorageReservationState.Released;
            claims.Remove(claim.Owner);
        }

        internal void DispatchNotifications()
        {
            int count = notifications.Count;
            while (count-- > 0)
            {
                var claim = notifications.Dequeue();
                if (claim.State != StorageReservationState.Invalid) continue;
                try { claim.Client?.OnReservationInvalidated(claim, claim.Failure); }
                catch (Exception exception) { Log.Error("[MagicStorage] Reservation invalidation callback failed: " + exception); }
                finally
                {
                    if (claims.TryGetValue(claim.Owner, out var current) && ReferenceEquals(current, claim))
                        claims.Remove(claim.Owner);
                }
            }
        }
    }
}
