using System;
using Verse;

namespace MagicStorage
{
    // Optional bindings are installed only after the floor adapter has initialized.
    internal static class StorageFloorContext
    {
        internal delegate bool ItemPlacer(Thing item, Map map, int floor, IntVec3 cell, out Thing result);
        internal static Func<Thing, int> FloorOf;
        internal static Func<Map, int, IDisposable> EnterFloor;
        internal static Action<Thing, int> SetDetachedFloor;
        internal static ItemPlacer PlaceItem;
        internal static Func<Map, int, IntVec3, bool> HasFloorAt;

        internal static bool SameFloor(Thing a, Thing b) => FloorOf == null || FloorOf(a) == FloorOf(b);
        internal static IDisposable Enter(Thing target) => EnterFloor != null && target?.Spawned == true
            ? EnterFloor(target.Map, FloorOf(target)) : null;
        internal static IDisposable Enter(Map map, Thing target) => EnterFloor != null && map != null && target != null
            ? EnterFloor(map, FloorOf(target)) : null;

        internal static bool TryDrop(ThingOwner holder, Thing item, Thing destination, out Thing result)
        {
            if (PlaceItem == null)
                return holder.TryDrop(item, destination.Position, destination.Map, ThingPlaceMode.Direct,
                    out result, playDropSound: false);

            result = null;
            if (!holder.Contains(item) || item.Spawned || item.Destroyed) return false;
            holder.Remove(item);
            try { return PlaceItem(item, destination.Map, FloorOf(destination), destination.Position, out result); }
            finally { RestoreDetached(holder, item, destination); }
        }

        internal static Thing SpawnDistinct(ThingOwner holder, Thing item, Thing destination)
        {
            if (SetDetachedFloor == null)
                return GenSpawn.Spawn(item, destination.Position, destination.Map, Rot4.North);

            if (!holder.Contains(item) || item.Spawned || item.Destroyed) return null;
            if (!HasFloorAt(destination.Map, FloorOf(destination), destination.Position)) return null;
            holder.Remove(item);
            try
            {
                // GenSpawn preserves remembered membership. Set it only while detached;
                // spawning directly keeps equal-definition shelf slots independent.
                SetDetachedFloor(item, FloorOf(destination));
                using (Enter(destination))
                    return GenSpawn.Spawn(item, destination.Position, destination.Map, Rot4.North);
            }
            finally { RestoreDetached(holder, item, destination); }
        }

        private static void RestoreDetached(ThingOwner holder, Thing item, Thing destination)
        {
            if (!item.Destroyed && !item.Spawned && item.holdingOwner == null && !holder.TryAdd(item, false))
                destination.Map.GetComponent<MapComponent_StorageNetworks>().PreserveLooseThing(item, destination.Position);
        }
    }
}
