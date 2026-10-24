// Controlled DDLF API/routing and native boundaries; exercises the real Harmony adapter.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MagicStorage;
using RimWorld;
using Verse;

internal static class LogicalFloorsCompatibilityTests
{
    private static int checks, wipeFloor, repairBeamFloor;
    private static bool throwOnCheck;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Logical Floors: " + message + "; expected " + expected + ", got " + actual); }

    internal static int Run()
    {
        var harmony = new Harmony("magicstorage.tests.logicalfloors");
        var native = new Harmony("magicstorage.tests.logicalfloors.native");
        LogicalFloors.API.FloorApi.Supported = false;
        try { new LogicalFloorsCompatibility().Apply(harmony); throw new Exception("Unsupported API accepted"); }
        catch (InvalidOperationException) { checks++; }
        Equal(false, Harmony.GetPatchInfo(AccessTools.Method(typeof(ConstructionSite), "MakeFrame"))?.Owners.Contains(harmony.Id) == true,
            "unsupported API installs no patches");
        LogicalFloors.API.FloorApi.Supported = true;
        try
        {
            new LogicalFloorsCompatibility().Apply(harmony);
            native.Patch(AccessTools.Method(typeof(GenSpawn), "Spawn"), prefix: Hook(nameof(Spawn)));
            native.Patch(AccessTools.Method(typeof(GenSpawn), "WipeExistingThings"), prefix: Hook(nameof(Wipe)));
            native.Patch(AccessTools.Method(typeof(GenConstruct), "FirstBlockingThing"), prefix: Hook(nameof(Check)));
            native.Patch(AccessTools.Method(typeof(MoteMaker), "MakeInteractionOverlay"), prefix: Hook(nameof(RepairBeam)));
            native.Patch(AccessTools.Method(typeof(CompBreakdownable), "Notify_Repaired"), prefix: Hook(nameof(RepairNotification)));
            // These native methods are already routed by DDLF in the real game.
            var route = new HarmonyMethod(typeof(LogicalFloors.FieldRouting), "Transpile");
            native.Patch(AccessTools.Method(typeof(Blueprint_Build), "MakeSolidThing"), transpiler: route);
            native.Patch(AccessTools.Method(typeof(Verse.AI.EnrouteUtility), "GetSpaceRemainingWithEnroute"), transpiler: route);
            foreach (int floor in new[] { 0, 1, 2, 11 }) FrameStaysOnBlueprintFloor(floor);
            TerrainChecksUseTargetFloor(); DeliveryUsesTargetFloor(); RepairUsesTargetFloor(); ExceptionsRestoreCaller();
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id); native.UnpatchAll(native.Id);
            throwOnCheck = false;
        }
        // Removing/omitting the optional adapter leaves the original construction path intact.
        var plain = new Fixture();
        Equal(true, ConstructionSite.MakeFrame(plain.Blueprint).Spawned, "ordinary construction still works without adapter");
        return checks;
    }

    private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(LogicalFloorsCompatibilityTests), name);
    private static void Spawn(Thing __0) => LogicalFloors.API.FloorApi.Floors[__0] = LogicalFloors.API.FloorApi.CurrentFloor;
    private static void Wipe() => wipeFloor = LogicalFloors.API.FloorApi.CurrentFloor;
    private static void RepairBeam() => repairBeamFloor = LogicalFloors.API.FloorApi.CurrentFloor;
    private static void RepairNotification(CompBreakdownable __instance) =>
        Equal(LogicalFloors.API.FloorApi.GetFloor(__instance.parent), LogicalFloors.API.FloorApi.CurrentFloor,
            "repair notification runs on target floor");
    private static void Check(Thing __0)
    {
        Equal(LogicalFloors.API.FloorApi.GetFloor(__0), LogicalFloors.API.FloorApi.CurrentFloor, "native blocker check uses target floor");
        if (throwOnCheck) throw new InvalidOperationException("controlled blocker check failure");
    }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly TerrainGrid TargetTerrain = new TerrainGrid();
        internal readonly PlanManager TargetPlans = new PlanManager();
        internal readonly EnrouteManager TargetEnroute = new EnrouteManager();
        internal readonly ThingDef Steel = new ThingDef();
        internal readonly Blueprint_Build Blueprint;
        internal readonly Building_ConstructionSupplyUnit Supplier = new Building_ConstructionSupplyUnit();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();

        internal Fixture(int floor = 1, Blueprint_Build blueprint = null)
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(new Building_StorageCore(), 0); Register(Unit, 1); Register(Supplier, 2);
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            Blueprint = blueprint ?? new Blueprint_Build();
            Blueprint.def = new ThingDef { entityDefToBuild = new ThingDef { category = ThingCategory.Building }, IsBlueprint = true };
            Blueprint.Map = Map; Blueprint.Position = new IntVec3(4, 0);
            Blueprint.Spawned = true; Blueprint.Faction = Faction.OfPlayer;
            Blueprint.Costs.Add(new ThingDefCountClass { thingDef = Steel, count = 10 });
            Map.listerThings.Things.Add(Blueprint);
            LogicalFloors.API.FloorApi.Floors[Blueprint] = floor;
            LogicalFloors.FieldRouting.TargetMap = Map; LogicalFloors.FieldRouting.TargetFloor = floor;
            LogicalFloors.FieldRouting.Terrain = TargetTerrain;
            LogicalFloors.FieldRouting.Plans = TargetPlans;
            LogicalFloors.FieldRouting.Enroute = TargetEnroute;
        }

        private void Register(Thing building, int x)
        {
            building.Map = Map; building.Position = new IntVec3(x, 0); building.Spawned = true; building.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = building };
            if (building is Building native) native.node = node;
            if (building is Building_StorageUnit unit) unit.node = node;
            Map.GetComponent<MapComponent_StorageNetworks>().Register(node);
        }
        internal void Stock() => Unit.Inventory.Contents.TryAdd(new Thing { def = Steel, stackCount = 10 }, false);
    }

    private static void FrameStaysOnBlueprintFloor(int floor)
    {
        var f = new Fixture(floor);
        f.TargetEnroute.Claims[f.Blueprint] = new Dictionary<ThingDef, int> { [f.Steel] = 3 };
        using (LogicalFloors.API.FloorApi.EnterFloor(f.Map, 7))
        {
            Frame frame = ConstructionSite.MakeFrame(f.Blueprint);
            Equal(floor, LogicalFloors.API.FloorApi.GetFloor(frame), "new frame inherits blueprint floor " + floor);
            Equal(floor, wipeFloor, "wipe uses blueprint floor");
            Equal(1, f.TargetPlans.Plan.Removed, "target-floor plan cleared");
            Equal(0, f.Map.planManager.Plan.Removed, "ground plan untouched");
            Equal(3, f.TargetEnroute.GetEnroute(frame, f.Steel), "native reservation transfer uses target floor");
            Equal(7, LogicalFloors.API.FloorApi.CurrentFloor, "direct conversion restores caller floor");
            Equal(true, f.Blueprint.Destroyed, "blueprint replaced normally");
        }
    }

    private static void TerrainChecksUseTargetFloor()
    {
        var f = new Fixture(); f.Blueprint.def.entityDefToBuild = new TerrainDef();
        f.Map.terrainGrid.Removable.Add(f.Blueprint.Position);
        Equal(true, ConstructionSite.CanSupply(f.Blueprint), "removable ground flooring does not block upper blueprint");
        f.Map.terrainGrid.Removable.Clear(); f.TargetTerrain.Removable.Add(f.Blueprint.Position);
        Equal(false, ConstructionSite.CanSupply(f.Blueprint), "removable upper flooring blocks upper blueprint");
        Equal(false, ConstructionSite.CanSupply(null), "null target remains rejected");
        f.Blueprint.Spawned = false;
        Equal(false, ConstructionSite.CanSupply(f.Blueprint), "despawned target remains rejected");
    }

    private static void DeliveryUsesTargetFloor()
    {
        var f = new Fixture(11); f.Stock();
        using (LogicalFloors.API.FloorApi.EnterFloor(f.Map, 2))
        {
            Equal(10, ConstructionSupply.DeliverTo(f.Supplier, f.Blueprint), "cross-floor delivery retains existing material count");
            Equal(11, LogicalFloors.API.FloorApi.GetFloor(f.Blueprint.Made), "delivery creates frame in basement");
            Equal(10, f.Blueprint.Made.resourceContainer.Sum(t => t.stackCount), "materials reach frame");
            Equal(0L, f.Supplier.Network.GetTotalCount(f.Steel), "materials deducted once");
            Equal(2, LogicalFloors.API.FloorApi.CurrentFloor, "nested delivery restores caller floor");
        }
    }

    private static void RepairUsesTargetFloor()
    {
        var f = new Fixture(3);
        var target = new Building { Map = f.Map, Spawned = true, Faction = Faction.OfPlayer,
            Position = new IntVec3(5, 0), def = new ThingDef { category = ThingCategory.Building, building = new BuildingProperties() } };
        target.TestComps.Add(new CompBreakdownable { parent = target });
        f.Map.GetComponent<BreakdownManager>().brokenDownThings.Add(target);
        LogicalFloors.API.FloorApi.Floors[target] = 3;
        f.Unit.Inventory.Contents.TryAdd(new Thing { def = ThingDefOf.ComponentIndustrial, stackCount = 1 }, false);
        using (LogicalFloors.API.FloorApi.EnterFloor(f.Map, 2))
        {
            f.Supplier.RefreshSupply();
            Equal(3, repairBeamFloor, "repair laser created in target floor context");
            Equal(2, LogicalFloors.API.FloorApi.CurrentFloor, "starting repair restores caller floor");
            Find.TickManager.TicksGame += 180;
            f.Map.GetComponent<MapComponent_ConstructionRepair>().MapComponentTick();
            Equal(false, target.GetComp<CompBreakdownable>().BrokenDown, "upper-floor building repaired");
            Equal(0L, f.Supplier.Network.GetTotalCount(ThingDefOf.ComponentIndustrial), "cross-floor repair consumes one component");
            Equal(2, LogicalFloors.API.FloorApi.CurrentFloor, "repair completion restores caller floor");
        }
    }

    private sealed class FailingBlueprint : Blueprint_Build
    {
        protected override Thing MakeSolidThing(out bool shouldSelect)
        { shouldSelect = false; throw new InvalidOperationException("controlled factory failure"); }
    }

    private static void ExceptionsRestoreCaller()
    {
        var f = new Fixture(); f.Stock();
        using (LogicalFloors.API.FloorApi.EnterFloor(f.Map, 2))
        {
            throwOnCheck = true;
            try
            {
                ExpectFailure(() => ConstructionSite.CanSupply(f.Blueprint));
                ExpectFailure(() => ConstructionSite.MakeFrame(f.Blueprint));
                ExpectFailure(() => ConstructionSupply.DeliverTo(f.Supplier, f.Blueprint));
            }
            finally { throwOnCheck = false; }
            Equal(true, f.Blueprint.Spawned, "failed checks leave blueprint intact");
            Equal(10L, f.Supplier.Network.GetTotalCount(f.Steel), "failed checks do not consume stock");
            var factory = new Fixture(1, new FailingBlueprint());
            ExpectFailure(() => ConstructionSite.MakeFrame(factory.Blueprint));
            Equal(true, factory.Blueprint.Spawned, "failed factory leaves blueprint intact");
        }
    }

    private static void ExpectFailure(Action action)
    {
        bool failed = false;
        try { action(); }
        catch (Exception e) when (e is InvalidOperationException || e is TargetInvocationException) { failed = true; }
        Equal(true, failed, "original exception is preserved");
        Equal(2, LogicalFloors.API.FloorApi.CurrentFloor, "exception restores enclosing caller floor");
    }
}

namespace LogicalFloors.API
{
    public static class FloorApi
    {
        public static bool Supported = true;
        public static int CurrentFloor;
        public static Map CurrentMap;
        public static readonly Dictionary<Thing, int> Floors = new Dictionary<Thing, int>();
        public static bool SupportsApi(int version) => Supported && version == 1;
        public static int GetFloor(Thing thing) => Floors.TryGetValue(thing, out int floor) ? floor : 0;
        public static IDisposable EnterFloor(Map map, int floor) => new Scope(map, floor);
        private sealed class Scope : IDisposable
        {
            private readonly Map previousMap;
            private readonly int previousFloor;
            public Scope(Map map, int floor)
            { previousMap = CurrentMap; previousFloor = CurrentFloor; CurrentMap = map; CurrentFloor = floor; }
            public void Dispose() { CurrentMap = previousMap; CurrentFloor = previousFloor; }
        }
    }
}

namespace LogicalFloors
{
    public static class Bootstrap
    {
        public static bool Ready;
        static Bootstrap() { Ready = true; }
    }

    public static class FieldRouting
    {
        public static Map TargetMap;
        public static int TargetFloor;
        public static TerrainGrid Terrain;
        public static PlanManager Plans;
        public static EnrouteManager Enroute;
        private static bool Target(Map map) => map == TargetMap && map == API.FloorApi.CurrentMap && API.FloorApi.CurrentFloor == TargetFloor;
        private static TerrainGrid TerrainAt(Map map) => Target(map) ? Terrain : map.terrainGrid;
        private static PlanManager PlansAt(Map map) => Target(map) ? Plans : map.planManager;
        private static EnrouteManager EnrouteAt(Map map) => Target(map) ? Enroute : map.enrouteManager;
        public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field && field.DeclaringType == typeof(Map))
                {
                    string getter = field.Name == "terrainGrid" ? nameof(TerrainAt) : field.Name == "planManager" ? nameof(PlansAt) :
                        field.Name == "enrouteManager" ? nameof(EnrouteAt) : null;
                    if (getter != null)
                    { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(FieldRouting), getter); }
                }
                yield return instruction;
            }
        }
    }
}
