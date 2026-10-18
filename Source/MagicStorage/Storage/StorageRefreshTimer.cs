namespace MagicStorage
{
    public enum StorageRefreshTask
    {
        NetworkMaintenance, Receiver, FuelSupply, TransportSupply, CraftingMaintenance,
        CraftingPolicy, ShelfRestock, ConstructionSupply, ApparelCleanup, RecoveryRelease,
        Count
    }

    // Runtime-only, per owner. Read the current interval on every check so settings
    // changes take effect without resetting every building or accumulating missed runs.
    internal struct StorageRefreshTimer
    {
        private bool initialized;
        private int lastTick;

        internal bool IsDue(StorageRefreshTask task, int tick)
            => IsDue(tick, MagicStorageMod.Settings.GetRefreshTicks(task));

        internal bool IsDue(int tick, int interval)
        {
            if (initialized && tick >= lastTick && (long)tick - lastTick < interval) return false;
            initialized = true;
            lastTick = tick;
            return true;
        }

        internal void Start(int tick) { initialized = true; lastTick = tick; }
    }
}
