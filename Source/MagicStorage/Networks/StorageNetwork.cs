using System;
using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    public enum StorageNetworkStatus { Rebuilding, NoCore, MultipleCores, Ready }

    // Runtime-only connected component. Units remain the owners of all saved items.
    public sealed class StorageNetwork
    {
        private readonly List<Building_StorageCore> cores = new List<Building_StorageCore>();
        private readonly List<Building_StorageUnit> units = new List<Building_StorageUnit>();
        // Buckets reference persistent container indexes; topology never copies all stacks.
        private readonly Dictionary<ThingDef, HashSet<Building_StorageUnit>> byDef = new Dictionary<ThingDef, HashSet<Building_StorageUnit>>();
        private readonly Dictionary<ThingDef, long> counts = new Dictionary<ThingDef, long>();
        private bool current;
        private bool transferring;
        private readonly StorageIncomingReservations incoming;
        private readonly StorageOutgoingReservations outgoing;
        private readonly HashSet<Thing> nodes = new HashSet<Thing>();
        private readonly List<CompStorageNode> members = new List<CompStorageNode>();
        private static readonly IComparer<Building_StorageUnit> unitOrder =
            Comparer<Building_StorageUnit>.Create((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
        internal int NodeCount => members.Count;

        public StorageNetwork() : this(new StorageOutgoingReservations()) { }
        internal StorageNetwork(StorageOutgoingReservations reservations)
        { outgoing = reservations; incoming = new StorageIncomingReservations(units, outgoing.IsReserved); }
        internal bool ContainsNode(Thing thing) => nodes.Contains(thing);

        public StorageNetworkStatus Status => !current ? StorageNetworkStatus.Rebuilding :
            cores.Count == 0 ? StorageNetworkStatus.NoCore :
            cores.Count > 1 ? StorageNetworkStatus.MultipleCores : StorageNetworkStatus.Ready;
        public bool CanWork => Status == StorageNetworkStatus.Ready;
        public Building_StorageCore Core => cores.Count == 1 ? cores[0] : null;
        public int UnitCount => units.Count;
        public long Revision { get; private set; }
        public long SlotCapacity
        {
            get { long count = 0; foreach (var unit in units) count += unit.SlotCapacity; return count; }
        }
        public long UsedSlots
        {
            get { long count = 0; foreach (var unit in units) count += unit.Inventory.UsedSlots; return count; }
        }

        internal void AddNode(CompStorageNode node)
        {
            if (!nodes.Add(node.parent)) return;
            members.Add(node);
            node.Network = this;
            if (node.parent is Building_StorageCore core) { cores.Add(core); incoming.Clear(); }
            if (node.parent is Building_StorageUnit unit)
            {
                int index = units.BinarySearch(unit, unitOrder);
                units.Insert(index < 0 ? ~index : index, unit);
                foreach (var def in unit.Inventory.IndexedDefs)
                    AddInventoryCount(unit, def, unit.Inventory.GetTotalCount(def));
            }
            Revision++;
        }

        internal void FinishRebuild()
        {
            current = true;
        }

        internal void Absorb(StorageNetwork other)
        {
            incoming.Clear();
            other.Invalidate();
            foreach (var node in other.members) AddNode(node);
        }

        internal void Invalidate() { current = false; incoming.Clear(); Revision++; }

        internal void GetMatchingStacks(Predicate<ThingDef> filter, List<Thing> result)
        {
            if (!CanWork) return;
            foreach (var pair in byDef)
                if (filter(pair.Key)) foreach (var unit in pair.Value) unit.Inventory.GetStacks(pair.Key, result);
        }

        internal void GetAllStacks(List<Thing> result)
        {
            if (!current) return;
            foreach (var pair in byDef)
                foreach (var unit in pair.Value) unit.Inventory.GetStacks(pair.Key, result);
        }

        internal int AvailableToWithdraw(Thing item, object owner = null)
        {
            if (!CanWork || item == null || item.Destroyed || incoming.IsStackReserved(item) ||
                !(item.holdingOwner?.Owner is Building_StorageUnit unit) || !nodes.Contains(unit)) return 0;
            return outgoing.Available(item, owner);
        }

        internal int ReserveOutgoing(object owner, Thing item, int count, Thing endpoint = null,
            IStorageReservationClient client = null, Func<bool> usable = null)
        {
            if (transferring) { outgoing.Release(owner); return 0; }
            outgoing.ValidateInventory();
            return outgoing.Reserve(owner, item, Math.Min(count, AvailableToWithdraw(item, owner)), Core, endpoint, client, usable);
        }
        internal void ReleaseOutgoing(object owner) => outgoing.Release(owner);
        internal bool HasOutgoingReservation(object owner, Thing item, int count) =>
            outgoing.CountFor(owner, item) >= count && AvailableToWithdraw(item, owner) >= count;

        internal StorageTransferResult TryWithdrawReserved(object owner, Thing item, int count,
            ThingOwner destination, out Thing received)
        {
            received = null;
            try
            {
                if (!CanWork || transferring) return StorageTransferResult.Failed(count, StorageFailure.NetworkUnavailable);
                outgoing.Validate();
                if (destination == null || destination.Owner is Building_StorageUnit || count <= 0 ||
                    outgoing.CountFor(owner, item) < count || AvailableToWithdraw(item, owner) < count)
                    return StorageTransferResult.Failed(count, StorageFailure.Reserved);
                transferring = true;
                int moved = 0;
                var claim = outgoing.BeginCollection(owner);
                try
                {
                    // No merge: the receiving job must target this exact split object.
                    moved = StorageTransfer.Move(item.holdingOwner, destination, item, count,
                        Core.Map, Core.Position, out received, false);
                }
                finally { outgoing.FinishCollection(claim, moved == count); transferring = false; }
                return new StorageTransferResult(count, moved, moved == count ? StorageFailure.None : StorageFailure.InsufficientCapacity);
            }
            finally { outgoing.Release(owner); }
        }

        public long GetTotalCount(ThingDef def)
        {
            return current && def != null && counts.TryGetValue(def, out long count) ? count : 0;
        }

        public void GetStacks(ThingDef def, List<Thing> result)
        {
            if (current && def != null && byDef.TryGetValue(def, out var bucket))
                foreach (var unit in bucket) unit.Inventory.GetStacks(def, result);
        }

        internal void NotifyInventoryChanged(Building_StorageUnit unit, ThingDef def, int delta, Thing item)
        {
            outgoing.ValidateItem(item);
            if (!current || !nodes.Contains(unit)) return;
            AddInventoryCount(unit, def, delta);
            Revision++;
        }

        private void AddInventoryCount(Building_StorageUnit unit, ThingDef def, long delta)
        {
            counts.TryGetValue(def, out long previous);
            long total = previous + delta;
            if (total == 0) { counts.Remove(def); byDef.Remove(def); return; }
            counts[def] = total;
            if (!byDef.TryGetValue(def, out var bucket))
                byDef.Add(def, bucket = new HashSet<Building_StorageUnit>());
            if (unit.Inventory.GetTotalCount(def) > 0) bucket.Add(unit);
            else bucket.Remove(unit);
        }

        public int GetCountCanAccept(Thing item)
        {
            if (!CanWork || item == null || !Core.Settings.AllowedToAccept(item)) return 0;
            return incoming.Plan(item, item.stackCount).Count;
        }

        internal int ReserveIncoming(object owner, Thing item, int count)
        {
            if (owner == null) return 0;
            if (!CanWork || transferring || item == null || !Core.Settings.AllowedToAccept(item))
            { incoming.Release(owner); return 0; }
            return incoming.Reserve(owner, item, count).Count;
        }

        internal int IncomingCountFor(object owner) => incoming.CountFor(owner);
        internal void ReleaseIncoming(object owner) { incoming.Release(owner); }

        // Source must be an actual holder (e.g. a colonist's carry container).
        public StorageTransferResult TryStore(ThingOwner source, Thing item, int count)
        { return TryStoreReserved(source, item, count, new object()); }

        internal StorageTransferResult TryStoreReserved(ThingOwner source, Thing item, int count, object reservationOwner)
        {
            int requested = count;
            if (!CanWork || transferring)
            { incoming.Release(reservationOwner); return StorageTransferResult.Failed(requested, StorageFailure.NetworkUnavailable); }
            if (source == null || item == null || count <= 0 || !source.Contains(item) ||
                reservationOwner == null || source.Owner is Building_StorageUnit)
            { incoming.Release(reservationOwner); return StorageTransferResult.Failed(requested, StorageFailure.InvalidSource); }
            if (!Core.Settings.AllowedToAccept(item))
            { incoming.Release(reservationOwner); return StorageTransferResult.Failed(requested, StorageFailure.FilterRejected); }
            var plan = incoming.Reserve(reservationOwner, item, Math.Min(count, item.stackCount));
            int moved = 0;
            transferring = true;
            try
            {
                // Honor the exact claim: an unreserved merge could consume another hauler's space.
                foreach (var allocation in plan.Allocations)
                {
                    if (!CanWork || item.Destroyed || !source.Contains(item)) break;
                    int accepted = StorageTransfer.Move(source, allocation.Unit.Inventory.Contents, item,
                        allocation.Count, Core.Map, Core.Position, allocation.Stack != null, allocation.Stack);
                    moved += accepted;
                    if (accepted != allocation.Count) break;
                }
            }
            finally { incoming.Release(reservationOwner); transferring = false; }
            return new StorageTransferResult(requested, moved, moved == requested ? StorageFailure.None :
                !CanWork ? StorageFailure.NetworkUnavailable : StorageFailure.InsufficientCapacity);
        }

        public StorageTransferResult TryTransferTo(Thing item, int count, ThingOwner destination)
        {
            if (!CanWork || transferring) return StorageTransferResult.Failed(count, StorageFailure.NetworkUnavailable);
            if (item == null || count <= 0 || destination == null || destination.Owner is Building_StorageUnit ||
                !(item.holdingOwner?.Owner is Building_StorageUnit unit) || !nodes.Contains(unit))
                return StorageTransferResult.Failed(count, StorageFailure.InvalidSource);
            // Keep incoming stack claims stable; the future withdrawal UI can retry once delivery ends.
            if (incoming.IsStackReserved(item)) return StorageTransferResult.Failed(count, StorageFailure.Reserved);
            int amount = Math.Min(count, Math.Min(outgoing.Available(item), destination.GetCountCanAccept(item)));
            if (amount <= 0) return StorageTransferResult.Failed(count, StorageFailure.InsufficientCapacity);
            transferring = true;
            int moved;
            try { moved = StorageTransfer.Move(unit.Inventory.Contents, destination, item, amount, Core.Map, Core.Position); }
            finally { transferring = false; }
            return new StorageTransferResult(count, moved, moved == count ? StorageFailure.None : StorageFailure.InsufficientCapacity);
        }

        public static string StatusLabel(StorageNetwork network)
        {
            return ("MS_Status_" + (network?.Status ?? StorageNetworkStatus.Rebuilding)).Translate();
        }
    }
}
