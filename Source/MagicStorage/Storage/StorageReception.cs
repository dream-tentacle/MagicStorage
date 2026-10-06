using Verse;

namespace MagicStorage
{
    internal static class StorageReception
    {
        // Bridge the map cell to the existing holder-to-unit transfer. A saved recovery
        // holder owns each detached stack until stored or put back, including partial failures.
        internal static StorageTransferResult Transfer(StorageNetwork network, Thing item, Map map, IntVec3 cell)
        {
            if (network?.CanWork != true || network.Core.Map != map)
                return StorageTransferResult.Failed(0, StorageFailure.NetworkUnavailable);
            if (item == null || !item.Spawned || item.Map != map || item.Position != cell)
                return StorageTransferResult.Failed(0, StorageFailure.InvalidSource);
            int count = network.GetCountCanAccept(item);
            if (count <= 0) return StorageTransferResult.Failed(item.stackCount, StorageFailure.InsufficientCapacity);
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            var batch = manager.CreateRecovery(cell);
            Thing part = null;
            try
            {
                part = item.SplitOff(count);
                if (!batch.Contents.TryAdd(part, false))
                    return StorageTransferResult.Failed(count, StorageFailure.InvalidSource);
                return network.TryStore(batch.Contents, part, count);
            }
            finally
            {
                if (part != null && !part.Destroyed && !part.Spawned && part.holdingOwner == null)
                    manager.PreserveLooseThing(part, cell);
                manager.ReleaseRecovery(batch);
            }
        }
    }
}
