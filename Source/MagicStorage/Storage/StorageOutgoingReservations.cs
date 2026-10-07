using System;
using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    // Claims refer to real stacks; ownership stays in the unit until collection.
    internal sealed class StorageOutgoingReservations
    {
        private readonly Dictionary<object, StorageReservation> claims = new Dictionary<object, StorageReservation>();
        private readonly Queue<StorageReservation> notifications = new Queue<StorageReservation>();
        internal void Release(object owner)
        {
            if (owner == null || !claims.TryGetValue(owner, out var claim)) return;
            claim.State = StorageReservationState.Released;
            claims.Remove(owner);
        }
        internal int CountFor(object owner, Thing item) => owner != null && claims.TryGetValue(owner, out var claim) &&
            claim.State == StorageReservationState.Reserved && claim.Item == item ? claim.Count : 0;
        internal int Available(Thing item, object except = null)
        {
            if (item == null || item.Destroyed) return 0;
            long count = item.stackCount;
            foreach (var pair in claims)
                if (!ReferenceEquals(pair.Key, except) && pair.Value.Item == item &&
                    pair.Value.State == StorageReservationState.Reserved) count -= pair.Value.Count;
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
            { existing.Count = count; return count; }
            Release(owner);
            claims[owner] = new StorageReservation { Owner = owner, Item = item, Count = count,
                Source = item.holdingOwner.Owner as Building_StorageUnit, Core = core,
                Endpoint = endpoint, Client = client, Usable = usable };
            return count;
        }

        private void Invalidate(StorageReservation claim, StorageReservationFailure reason)
        {
            if (claim.State != StorageReservationState.Reserved) return;
            claim.State = StorageReservationState.Invalid;
            claim.Failure = reason;
            notifications.Enqueue(claim);
        }

        internal void NotifyNodeUnavailable(Thing node)
        {
            foreach (var claim in claims.Values)
                if (claim.Source == node || claim.Core == node || claim.Endpoint == node)
                    Invalidate(claim, StorageReservationFailure.NodeUnavailable);
        }

        // Inventory callbacks only check physical availability; policy/topology checks wait
        // until the graph is current. No user code is called from a ThingOwner mutation.
        internal void ValidateInventory(Building_StorageUnit source = null, ThingDef def = null)
        {
            if (claims.Count == 0) return;
            var totals = new Dictionary<Thing, long>();
            foreach (var claim in claims.Values)
                if (claim.State == StorageReservationState.Reserved)
                {
                    totals.TryGetValue(claim.Item, out long count);
                    totals[claim.Item] = count + claim.Count;
                }
            foreach (var claim in claims.Values)
            {
                if (claim.State != StorageReservationState.Reserved ||
                    (source != null && claim.Source != source) || (def != null && claim.Item.def != def)) continue;
                if (claim.Source == null || claim.Item.Destroyed || claim.Item.holdingOwner != claim.Source.Inventory.Contents ||
                    !claim.Source.Inventory.Contents.Contains(claim.Item))
                    Invalidate(claim, StorageReservationFailure.ItemUnavailable);
                // If an outside mutation overdraws a stack, invalidate all conflicting
                // claims rather than arbitrarily favoring dictionary iteration order.
                else if (totals[claim.Item] > claim.Item.stackCount)
                    Invalidate(claim, StorageReservationFailure.InsufficientCount);
            }
        }

        internal void Validate()
        {
            if (claims.Count == 0) return;
            ValidateInventory();
            foreach (var claim in new List<StorageReservation>(claims.Values))
            {
                if (claim.State != StorageReservationState.Reserved) continue;
                var network = claim.Core?.Network;
                if (network == null || !network.CanWork || network.Core != claim.Core || claim.Source.Network != network ||
                    (claim.Endpoint != null && !network.ContainsNode(claim.Endpoint)))
                    Invalidate(claim, StorageReservationFailure.NetworkUnavailable);
                else if (claim.Usable != null && !claim.Usable())
                    Invalidate(claim, StorageReservationFailure.PolicyChanged);
            }
        }

        internal StorageReservation BeginCollection(object owner)
        {
            if (!claims.TryGetValue(owner, out var claim) || claim.State != StorageReservationState.Reserved) return null;
            // The source's normal removal notifications must not invalidate this collection.
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
