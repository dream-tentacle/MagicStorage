using System;
using Verse;

namespace MagicStorage
{
    public enum StorageFailure { None, NetworkUnavailable, InvalidSource, FilterRejected, InsufficientCapacity, Reserved }

    public struct StorageTransferResult
    {
        public readonly int Requested;
        public readonly int Transferred;
        public readonly StorageFailure Failure;
        public bool Complete => Requested > 0 && Requested == Transferred;
        public StorageTransferResult(int requested, int transferred, StorageFailure failure)
        { Requested = requested; Transferred = transferred; Failure = failure; }
        public static StorageTransferResult Failed(int requested, StorageFailure failure)
        { return new StorageTransferResult(requested, 0, failure); }
    }

    internal static class StorageTransfer
    {
        // Explicitly account for partial merges. A merged-away Thing reference may be destroyed.
        internal static int Move(ThingOwner source, ThingOwner destination, Thing item, int count,
            Map recoveryMap, IntVec3 recoveryCell, bool merge = true, Thing mergeTarget = null)
        { return Move(source, destination, item, count, recoveryMap, recoveryCell, out _, merge, mergeTarget); }

        internal static int Move(ThingOwner source, ThingOwner destination, Thing item, int count,
            Map recoveryMap, IntVec3 recoveryCell, out Thing received, bool merge = true, Thing mergeTarget = null)
        {
            received = null;
            int amount = Math.Min(count, Math.Min(item.stackCount, destination.GetCountCanAccept(item, merge)));
            if (mergeTarget != null)
            {
                if (!destination.Contains(mergeTarget) || !mergeTarget.CanStackWith(item)) return 0;
                amount = Math.Min(amount, Math.Max(0, mergeTarget.def.stackLimit - mergeTarget.stackCount));
            }
            if (amount <= 0 || source == destination || !source.Contains(item)) return 0;
            ThingDef def = item.def;
            Thing part = null;
            try
            {
                part = source.Take(item, amount);
                if (part == null) return 0;
                if (mergeTarget != null) mergeTarget.TryAbsorbStack(part, true);
                else destination.TryAdd(part, merge);
                if (!part.Destroyed && part.holdingOwner == destination) received = part;
                return part.Destroyed || part.holdingOwner == destination ? amount : amount - part.stackCount;
            }
            finally
            {
                if (part != null && !part.Destroyed && !part.Spawned && part.holdingOwner == null)
                {
                    if (item != part && source.Contains(item)) item.TryAbsorbStack(part, false);
                    if (!part.Destroyed && part.holdingOwner == null && !source.TryAdd(part, false))
                        recoveryMap.GetComponent<MapComponent_StorageNetworks>().PreserveLooseThing(part, recoveryCell);
                }
                (source.Owner as Building_StorageUnit)?.Inventory.RefreshDef(def);
                (destination.Owner as Building_StorageUnit)?.Inventory.RefreshDef(def);
            }
        }
    }
}
