using System;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace MagicStorage
{
    // Resolved only by the optional adapter. No storage service calls this class.
    internal sealed class LogicalFloorsApi
    {
        private delegate bool ItemPlacer(Thing item, Map map, int floor, IntVec3 cell, out Thing result);
        internal readonly Func<Thing, int> GetFloor;
        internal readonly Func<Map, int, IDisposable> EnterFloor;
        private readonly Action<Thing, int> setDetachedFloor;
        private readonly ItemPlacer placeItem;
        private readonly Func<Map, int, IntVec3, bool> hasFloor;
        private readonly Func<Map, int, bool> hasMapFloor;

        internal LogicalFloorsApi(Type api, Func<Thing, int> getFloor, Func<Map, int, IDisposable> enterFloor)
        {
            GetFloor = getFloor; EnterFloor = enterFloor;
            Type positionType = AccessTools.TypeByName("LogicalFloors.API.FloorPosition");
            Type floorsType = AccessTools.TypeByName("LogicalFloors.Floors");
            ConstructorInfo constructor = positionType?.GetConstructor(new[] { typeof(Map), typeof(int), typeof(IntVec3) });
            MethodInfo place = positionType == null ? null : AccessTools.Method(api, "TryPlaceItem", new[]
                { typeof(Thing), positionType, typeof(ThingPlaceMode), typeof(Thing).MakeByRefType() });
            MethodInfo set = floorsType == null ? null : AccessTools.Method(floorsType, "Set", new[] { typeof(Thing), typeof(int) });
            MethodInfo structural = positionType == null ? null : AccessTools.Method(api, "HasStructuralFloor", new[] { positionType });
            MethodInfo exists = AccessTools.Method(api, "HasFloor", new[] { typeof(Map), typeof(int) });
            if (constructor == null || place?.ReturnType != typeof(bool) || !place.IsStatic ||
                set?.ReturnType != typeof(void) || !set.IsStatic || structural?.ReturnType != typeof(bool) || !structural.IsStatic ||
                exists?.ReturnType != typeof(bool) || !exists.IsStatic)
                throw new InvalidOperationException("Logical Floors item placement API was not found; no floor patches installed.");
            hasMapFloor = (Func<Map, int, bool>)Delegate.CreateDelegate(typeof(Func<Map, int, bool>), exists);
            setDetachedFloor = (Action<Thing, int>)Delegate.CreateDelegate(typeof(Action<Thing, int>), set);
            var item = Expression.Parameter(typeof(Thing), "item");
            var map = Expression.Parameter(typeof(Map), "map");
            var floor = Expression.Parameter(typeof(int), "floor");
            var cell = Expression.Parameter(typeof(IntVec3), "cell");
            var result = Expression.Parameter(typeof(Thing).MakeByRefType(), "result");
            var position = Expression.New(constructor, map, floor, cell);
            placeItem = Expression.Lambda<ItemPlacer>(Expression.Call(place, item, position,
                Expression.Constant(ThingPlaceMode.Direct), result), item, map, floor, cell, result).Compile();
            hasFloor = Expression.Lambda<Func<Map, int, IntVec3, bool>>(Expression.Call(structural, position), map, floor, cell).Compile();
        }

        internal bool SameFloor(Thing a, Thing b) => GetFloor(a) == GetFloor(b);
        internal IDisposable Enter(Thing target) => target?.Spawned == true && !target.Destroyed
            ? EnterFloor(target.Map, GetFloor(target)) : null;

        // Drawing queries may encounter stale membership during cross-map arrivals.
        // A rejected query must be skipped, never continued against the caller's floor.
        internal bool TryEnter(Thing target, out IDisposable scope)
        {
            scope = null;
            if (target?.Spawned != true || target.Destroyed || target.Map == null) return false;
            int floor = GetFloor(target);
            if (!hasMapFloor(target.Map, floor)) return false;
            scope = EnterFloor(target.Map, floor);
            return true;
        }

        internal bool TryDrop(ThingOwner holder, Thing item, Building_StorageSupplyShelf shelf, out Thing result)
        {
            result = null;
            if (!holder.Contains(item) || item.Spawned || item.Destroyed) return false;
            holder.Remove(item);
            try { return placeItem(item, shelf.Map, GetFloor(shelf), shelf.Position, out result); }
            finally { RestoreDetached(holder, item, shelf); }
        }

        internal Thing SpawnDistinct(Thing item, Building_StorageSupplyShelf shelf)
        {
            if (item.Spawned || item.Destroyed || !hasFloor(shelf.Map, GetFloor(shelf), shelf.Position)) return null;
            ThingOwner holder = item.holdingOwner;
            holder?.Remove(item);
            try
            {
                // Only detached items may change remembered membership. Direct spawning
                // preserves the independent quantities of equal-definition shelf slots.
                setDetachedFloor(item, GetFloor(shelf));
                using (Enter(shelf)) return GenSpawn.Spawn(item, shelf.Position, shelf.Map, Rot4.North);
            }
            finally { RestoreDetached(holder, item, shelf); }
        }

        private static void RestoreDetached(ThingOwner holder, Thing item, Building_StorageSupplyShelf shelf)
        {
            if (item.Destroyed || item.Spawned || item.holdingOwner != null) return;
            if (holder == null || !holder.TryAdd(item, false))
                shelf.Map.GetComponent<MapComponent_StorageNetworks>().PreserveLooseThing(item, shelf.Position);
        }
    }
}
