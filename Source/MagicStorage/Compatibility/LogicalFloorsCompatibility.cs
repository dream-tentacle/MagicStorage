using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;

namespace MagicStorage
{
    // Optional floor adapter; storage services have no assembly dependency on DDLF.
    internal sealed class LogicalFloorsCompatibility : IStorageCompatibility
    {
        private static Func<Thing, int> getFloor;
        private static Func<Map, int, IDisposable> enterFloor;
        public string PackageId => "moont.dd.logicalfloors";

        public void Apply(Harmony harmony)
        {
            Type api = AccessTools.TypeByName("LogicalFloors.API.FloorApi");
            Type routing = AccessTools.TypeByName("LogicalFloors.FieldRouting");
            Type bootstrap = AccessTools.TypeByName("LogicalFloors.Bootstrap");
            var ready = bootstrap == null ? null : AccessTools.Field(bootstrap, "Ready");
            var supports = api == null ? null : AccessTools.Method(api, "SupportsApi", new[] { typeof(int) });
            var floor = api == null ? null : AccessTools.Method(api, "GetFloor", new[] { typeof(Thing) });
            var enter = api == null ? null : AccessTools.Method(api, "EnterFloor", new[] { typeof(Map), typeof(int) });
            // EnterFloor alone cannot route raw per-floor Map fields in our code.
            // Reuse DDLF's own routing for construction and repair validation.
            var route = routing == null ? null : AccessTools.Method(routing, "Transpile", new[] { typeof(IEnumerable<CodeInstruction>) });
            if (supports?.ReturnType != typeof(bool) || !supports.IsStatic ||
                floor?.ReturnType != typeof(int) || !floor.IsStatic ||
                enter?.ReturnType != typeof(IDisposable) || !enter.IsStatic ||
                route?.ReturnType != typeof(IEnumerable<CodeInstruction>) || !route.IsStatic ||
                ready?.FieldType != typeof(bool) || !ready.IsStatic ||
                !(bool)supports.Invoke(null, new object[] { 1 }))
                throw new InvalidOperationException("Logical Floors API/routing was not found; no floor patches installed.");

            // DDLF populates the transpiler's field table in Bootstrap, not FieldRouting's
            // static constructor. Ensure its normal startup has run regardless of mod order.
            RuntimeHelpers.RunClassConstructor(bootstrap.TypeHandle);
            if (!(bool)ready.GetValue(null))
                throw new InvalidOperationException("Logical Floors did not initialize; no floor patches installed.");

            var resolvedFloor = (Func<Thing, int>)Delegate.CreateDelegate(typeof(Func<Thing, int>), floor);
            var resolvedEnter = (Func<Map, int, IDisposable>)Delegate.CreateDelegate(typeof(Func<Map, int, IDisposable>), enter);
            Type positionType = AccessTools.TypeByName("LogicalFloors.API.FloorPosition");
            Type floorsType = AccessTools.TypeByName("LogicalFloors.Floors");
            var positionConstructor = positionType?.GetConstructor(new[] { typeof(Map), typeof(int), typeof(IntVec3) });
            var place = positionType == null ? null : AccessTools.Method(api, "TryPlaceItem", new[]
                { typeof(Thing), positionType, typeof(ThingPlaceMode), typeof(Thing).MakeByRefType() });
            var set = floorsType == null ? null : AccessTools.Method(floorsType, "Set", new[] { typeof(Thing), typeof(int) });
            var hasFloor = positionType == null ? null : AccessTools.Method(api, "HasStructuralFloor", new[] { positionType });
            if (positionConstructor == null || place?.ReturnType != typeof(bool) || !place.IsStatic ||
                set?.ReturnType != typeof(void) || !set.IsStatic || hasFloor?.ReturnType != typeof(bool) || !hasFloor.IsStatic)
                throw new InvalidOperationException("Logical Floors item placement API was not found; no floor patches installed.");
            var resolvedSet = (Action<Thing, int>)Delegate.CreateDelegate(typeof(Action<Thing, int>), set);
            var item = Expression.Parameter(typeof(Thing), "item");
            var map = Expression.Parameter(typeof(Map), "map");
            var destinationFloor = Expression.Parameter(typeof(int), "floor");
            var cell = Expression.Parameter(typeof(IntVec3), "cell");
            var result = Expression.Parameter(typeof(Thing).MakeByRefType(), "result");
            var resolvedPlace = Expression.Lambda<StorageFloorContext.ItemPlacer>(Expression.Call(place, item,
                Expression.New(positionConstructor, map, destinationFloor, cell),
                Expression.Constant(ThingPlaceMode.Direct), result), item, map, destinationFloor, cell, result).Compile();
            var resolvedHasFloor = Expression.Lambda<Func<Map, int, IntVec3, bool>>(Expression.Call(hasFloor,
                Expression.New(positionConstructor, map, destinationFloor, cell)), map, destinationFloor, cell).Compile();
            var check = AccessTools.Method(typeof(ConstructionSite), nameof(ConstructionSite.CanSupply));
            var make = AccessTools.Method(typeof(ConstructionSite), nameof(ConstructionSite.MakeFrame));
            var deliver = AccessTools.Method(typeof(ConstructionSupply), nameof(ConstructionSupply.DeliverTo));
            var canRepair = AccessTools.Method(typeof(MapComponent_ConstructionRepair), nameof(MapComponent_ConstructionRepair.CanRepair));
            var startRepair = AccessTools.Method(typeof(MapComponent_ConstructionRepair), nameof(MapComponent_ConstructionRepair.TryStart));
            var finishRepair = AccessTools.Method(typeof(MapComponent_ConstructionRepair), "CompleteRepair");
            var describeRepair = AccessTools.Method(typeof(MapComponent_ConstructionRepair), nameof(MapComponent_ConstructionRepair.DescribeTarget));
            var reserved = AccessTools.Method(typeof(StorageShelfSupply), nameof(StorageShelfSupply.Reserved));
            var shelfSettings = AccessTools.Method(typeof(Building_StorageSupplyShelf), nameof(Building_StorageSupplyShelf.Notify_SettingsChanged));
            var shelfMerge = AccessTools.Method(typeof(StorageShelfHaulingPatches), "SkipShelfMerge");
            var shelfTemperature = AccessTools.Method(typeof(StorageShelfTemperaturePatches), "AdjustItemTemperature");
            var dirty = AccessTools.Method(typeof(MapComponent_StorageNetworks), "DirtyDrawing");
            var targets = new List<MethodBase> { check, make, deliver, canRepair, startRepair, finishRepair,
                describeRepair, reserved, shelfSettings, shelfMerge, shelfTemperature, dirty };
            getFloor = resolvedFloor; enterFloor = resolvedEnter;
            try
            {
                harmony.Patch(check, prefix: Hook(nameof(BeginSite)), finalizer: Hook(nameof(EndSite)), transpiler: new HarmonyMethod(route));
                harmony.Patch(make, prefix: Hook(nameof(BeginSite)), finalizer: Hook(nameof(EndSite)), transpiler: new HarmonyMethod(route));
                harmony.Patch(deliver, prefix: Hook(nameof(BeginDelivery)), finalizer: Hook(nameof(EndSite)));
                harmony.Patch(canRepair, prefix: Hook(nameof(BeginDelivery)), finalizer: Hook(nameof(EndSite)), transpiler: new HarmonyMethod(route));
                harmony.Patch(startRepair, prefix: Hook(nameof(BeginDelivery)), finalizer: Hook(nameof(EndSite)));
                harmony.Patch(finishRepair, prefix: Hook(nameof(BeginDelivery)), finalizer: Hook(nameof(EndSite)));
                harmony.Patch(describeRepair, prefix: Hook(nameof(BeginDelivery)), finalizer: Hook(nameof(EndSite)), transpiler: new HarmonyMethod(route));
                harmony.Patch(reserved, prefix: Hook(nameof(BeginSite)), finalizer: Hook(nameof(EndSite)), transpiler: new HarmonyMethod(route));
                harmony.Patch(shelfSettings, prefix: Hook(nameof(BeginShelf)), finalizer: Hook(nameof(EndSite)), transpiler: new HarmonyMethod(route));
                harmony.Patch(shelfMerge, prefix: Hook(nameof(BeginSite)), finalizer: Hook(nameof(EndSite)), transpiler: new HarmonyMethod(route));
                harmony.Patch(shelfTemperature, prefix: Hook(nameof(BeginItemTemperature)), finalizer: Hook(nameof(EndSite)), transpiler: new HarmonyMethod(route));
                harmony.Patch(dirty, transpiler: new HarmonyMethod(route));
                Type shelfDrawingType = AccessTools.TypeByName("MagicStorage.StorageShelfDrawing");
                var findShelf = shelfDrawingType == null ? null : AccessTools.Method(shelfDrawingType, "TryGetShelf");
                if (findShelf != null)
                {
                    targets.Add(findShelf);
                    harmony.Patch(findShelf, prefix: Hook(nameof(BeginSite)), finalizer: Hook(nameof(EndSite)), transpiler: new HarmonyMethod(route));
                }
                // Custom drawing methods also read Map fields outside the game assembly.
                foreach (var name in new[] { "MagicStorage.StorageNetworkDrawing", "MagicStorage.Graphic_StorageConduit" })
                {
                    Type drawingType = AccessTools.TypeByName(name);
                    if (drawingType == null) continue;
                    foreach (var method in AccessTools.GetDeclaredMethods(drawingType))
                    {
                        if (method.Name != "Print" && method.Name != "ShouldLinkWith" && method.Name != "Dirty") continue;
                        targets.Add(method);
                        bool ownerArgument = method.Name == "Print" || method.Name == "ShouldLinkWith";
                        harmony.Patch(method, prefix: ownerArgument ? Hook(nameof(BeginDelivery)) : null,
                            finalizer: ownerArgument ? Hook(nameof(EndSite)) : null, transpiler: new HarmonyMethod(route));
                    }
                }
                StorageFloorContext.FloorOf = resolvedFloor;
                StorageFloorContext.EnterFloor = resolvedEnter;
                StorageFloorContext.SetDetachedFloor = resolvedSet;
                StorageFloorContext.PlaceItem = resolvedPlace;
                StorageFloorContext.HasFloorAt = resolvedHasFloor;
            }
            catch
            {
                foreach (var target in targets) harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
                throw;
            }
        }

        private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(LogicalFloorsCompatibility), name)
        { priority = Priority.Last };

        private static IDisposable Enter(Thing target) => target?.Spawned == true && !target.Destroyed
            ? enterFloor(target.Map, getFloor(target)) : null;

        private static void BeginSite(Thing __0, out IDisposable __state) => __state = Enter(__0);
        private static void BeginDelivery(Thing __1, out IDisposable __state) => __state = Enter(__1);
        private static void BeginShelf(Building_StorageSupplyShelf __instance, out IDisposable __state) => __state = Enter(__instance);
        private static void BeginItemTemperature(Thing __0, out IDisposable __state) => __state = Enter(__0);
        // A finalizer restores the caller's floor even if validation, a factory or a transfer throws.
        private static void EndSite(IDisposable __state) => __state?.Dispose();
    }
}
