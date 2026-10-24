using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;

namespace MagicStorage
{
    // Optional construction/repair adapter; the services have no dependency on DDLF.
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
                throw new InvalidOperationException("Logical Floors construction API/routing was not found; no construction patches installed.");

            // DDLF populates the transpiler's field table in Bootstrap, not FieldRouting's
            // static constructor. Ensure its normal startup has run regardless of mod order.
            RuntimeHelpers.RunClassConstructor(bootstrap.TypeHandle);
            if (!(bool)ready.GetValue(null))
                throw new InvalidOperationException("Logical Floors did not initialize; no construction patches installed.");

            var resolvedFloor = (Func<Thing, int>)Delegate.CreateDelegate(typeof(Func<Thing, int>), floor);
            var resolvedEnter = (Func<Map, int, IDisposable>)Delegate.CreateDelegate(typeof(Func<Map, int, IDisposable>), enter);
            var check = AccessTools.Method(typeof(ConstructionSite), nameof(ConstructionSite.CanSupply));
            var make = AccessTools.Method(typeof(ConstructionSite), nameof(ConstructionSite.MakeFrame));
            var deliver = AccessTools.Method(typeof(ConstructionSupply), nameof(ConstructionSupply.DeliverTo));
            var canRepair = AccessTools.Method(typeof(MapComponent_ConstructionRepair), nameof(MapComponent_ConstructionRepair.CanRepair));
            var startRepair = AccessTools.Method(typeof(MapComponent_ConstructionRepair), nameof(MapComponent_ConstructionRepair.TryStart));
            var finishRepair = AccessTools.Method(typeof(MapComponent_ConstructionRepair), "CompleteRepair");
            var describeRepair = AccessTools.Method(typeof(MapComponent_ConstructionRepair), nameof(MapComponent_ConstructionRepair.DescribeTarget));
            var targets = new[] { check, make, deliver, canRepair, startRepair, finishRepair, describeRepair };
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
        // A finalizer restores the caller's floor even if validation, a factory or a transfer throws.
        private static void EndSite(IDisposable __state) => __state?.Dispose();
    }
}
