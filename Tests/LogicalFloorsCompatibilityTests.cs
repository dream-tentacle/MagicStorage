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
            FloorNetworkConnections(); ShelfSuppliesAcrossFloors(); PlacementFailurePreservesOwnership();
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id); native.UnpatchAll(native.Id);
            throwOnCheck = false;
            StorageFloorContext.FloorOf = null;
            StorageFloorContext.EnterFloor = null;
            StorageFloorContext.SetDetachedFloor = null;
            StorageFloorContext.PlaceItem = null;
            StorageFloorContext.HasFloorAt = null;
            LogicalFloors.API.FloorApi.StructuralFloor = true;
        }
        // Removing/omitting the optional adapter leaves the original construction path intact.
        var plain = new Fixture();
        Equal(true, ConstructionSite.MakeFrame(plain.Blueprint).Spawned, "ordinary construction still works without adapter");
        return checks;
    }

    private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(LogicalFloorsCompatibilityTests), name);
    private static void Spawn(Thing __0)
    {
        if (!LogicalFloors.API.FloorApi.Floors.ContainsKey(__0))
            LogicalFloors.API.FloorApi.Floors[__0] = LogicalFloors.API.FloorApi.CurrentFloor;
    }
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

    private static CompStorageNode RegisterFloorNode(Map map, Thing thing, int x, int floor)
    {
        thing.Map = map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
        LogicalFloors.Floors.Set(thing, floor);
        var node = new CompStorageNode { parent = thing };
        if (thing is Building_StorageUnit unit) unit.node = node;
        if (thing is Building building) building.node = node;
        map.GetComponent<MapComponent_StorageNetworks>().Register(node);
        return node;
    }

    private static void FloorNetworkConnections()
    {
        foreach (int floor in new[] { 1, 2, 11 })
        {
            var map = new Map(); var manager = map.GetComponent<MapComponent_StorageNetworks>();
            var core = RegisterFloorNode(map, new Building_StorageCore(), 0, 0);
            RegisterFloorNode(map, new Building_StorageConduit(), 1, 0);
            var bridge = RegisterFloorNode(map, new Building_StorageConduit(), 1, floor);
            var shelf = RegisterFloorNode(map, new Building_StorageSupplyShelf(), 2, floor);
            Equal(core.Network, shelf.Network, "same-coordinate cables connect floors incrementally");
            manager.MarkDirty(); manager.EnsureCurrent();
            Equal(core.Network, shelf.Network, "rebuild follows each node floor after crossing cable");
            manager.Unregister(bridge); manager.EnsureCurrent();
            Equal(false, core.Network == shelf.Network, "removing vertical cable splits floors");
            manager.Register(bridge); manager.EnsureCurrent();
            Equal(core.Network, shelf.Network, "replacing vertical cable rejoins floors");

            var lowerBuilding = RegisterFloorNode(map, new Building_StorageUnit(), 20, 0);
            var upperBuilding = RegisterFloorNode(map, new Building_StorageUnit(), 20, floor);
            var upperNeighbor = RegisterFloorNode(map, new Building_StorageSupplyShelf(), 21, floor);
            Equal(false, lowerBuilding.Network == upperBuilding.Network, "overlapping devices do not connect across floors");
            Equal(upperBuilding.Network, upperNeighbor.Network, "ordinary same-floor adjacency remains connected");
            var cable = RegisterFloorNode(map, new Building_StorageConduit(), 30, 0);
            var shiftedCable = RegisterFloorNode(map, new Building_StorageConduit(), 31, floor);
            Equal(false, cable.Network == shiftedCable.Network, "offset cables do not connect across floors");
            var device = RegisterFloorNode(map, new Building_StorageSupplyShelf(), 30, floor);
            Equal(false, cable.Network == device.Network, "cable cannot connect directly to another-floor device");
            var enemyCable = new Building_StorageConduit();
            var enemy = RegisterFloorNode(map, enemyCable, 30, floor);
            enemyCable.Faction = new Faction(); manager.MarkDirty(); manager.EnsureCurrent();
            Equal(false, cable.Network == enemy.Network, "vertical bridge respects faction");
            Equal(false, lowerBuilding.Network == upperBuilding.Network, "rebuild also separates overlapping devices");
        }
    }

    private static void ShelfSuppliesAcrossFloors()
    {
        foreach (var pair in new[] { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 0, 11 }, new[] { 11, 2 }, new[] { 2, 11 } })
        {
            var map = new Map(); var manager = map.GetComponent<MapComponent_StorageNetworks>();
            var unit = new Building_StorageUnit(); var shelf = new Building_StorageSupplyShelf();
            var def = new ThingDef(); var parent = new StorageSettings(); parent.filter.SetAllow(def, true);
            shelf.def.building = new BuildingProperties { fixedStorageSettings = parent, maxItemsInCell = 3 };
            RegisterFloorNode(map, new Building_StorageCore(), 0, pair[0]);
            RegisterFloorNode(map, unit, 1, pair[0]);
            RegisterFloorNode(map, new Building_StorageConduit(), 2, pair[0]);
            RegisterFloorNode(map, new Building_StorageConduit(), 2, pair[1]);
            RegisterFloorNode(map, shelf, 3, pair[1]);
            manager.EnsureCurrent(); map.haulDestinationManager.SlotParents[shelf.Position] = shelf;
            var source = new Thing { def = def, stackCount = 60 };
            LogicalFloors.Floors.Set(source, pair[0]); unit.Inventory.Contents.TryAdd(source, false);
            var foreign = new Thing { def = def, stackCount = 4, Map = map, Position = shelf.Position, Spawned = true };
            LogicalFloors.Floors.Set(foreign, pair[0]); map.Ground.Add(foreign);
            shelf.GetStoreSettings();
            shelf.SetStock(0, foreign);
            Equal<Thing>(null, shelf.StockAt(0), "same-coordinate other-floor stack is not shelf stock");
            Equal(false, shelf.Accepts(foreign), "other-floor stack is not stored on this shelf");
            int[] limits = { 3, 5, 7 };
            for (int i = 0; i < limits.Length; i++) { shelf.SetSelection(i, def); shelf.SetRestockLimit(i, limits[i]); }
            LogicalFloors.FieldRouting.TargetMap = map; LogicalFloors.FieldRouting.TargetFloor = pair[1];
            LogicalFloors.FieldRouting.Reservations = new ReservationManager();
            LogicalFloors.FieldRouting.Interactions = new PhysicalInteractionReservationManager();
            using (LogicalFloors.API.FloorApi.EnterFloor(map, 8))
            {
                shelf.RefreshSupply();
                for (int i = 0; i < limits.Length; i++)
                {
                    Thing stock = shelf.StockAt(i);
                    Equal(true, stock != null && stock.Spawned, "all duplicate selections are physically stocked");
                    Equal(pair[1], LogicalFloors.API.FloorApi.GetFloor(stock), "output uses shelf floor, not source or caller floor");
                    Equal(limits[i], stock.stackCount, "cross-floor duplicate slots retain independent quantities");
                }
                Equal(45L, unit.Inventory.GetTotalCount(def), "cross-floor output consumes exactly the requested stock");
                Equal(4, foreign.stackCount, "other-floor stack is not merged into or taken");
                shelf.RefreshSupply();
                Equal(45L, unit.Inventory.GetTotalCount(def), "full shelf does not repeatedly extract stock");
                shelf.StockAt(0).stackCount = 1;
                LogicalFloors.FieldRouting.Reservations.Reserved.Add(shelf.StockAt(0));
                Equal(true, StorageShelfSupply.Reserved(shelf, shelf.StockAt(0)), "reservation lookup routes to shelf floor outside refresh");
                shelf.RefreshSupply();
                Equal(1, shelf.StockAt(0).stackCount, "reserved destination stack is not refilled");
                LogicalFloors.FieldRouting.Reservations.Reserved.Clear();
                shelf.RefreshSupply();
                Equal(3, shelf.StockAt(0).stackCount, "unreserved exact duplicate slot refills on its own floor");
                Equal(8, LogicalFloors.API.FloorApi.CurrentFloor, "supply restores enclosing caller floor");
            }
        }
    }

    private static void PlacementFailurePreservesOwnership()
    {
        var map = new Map();
        var shelf = new Building_StorageSupplyShelf { Map = map, Spawned = true, Position = new IntVec3(5, 5) };
        LogicalFloors.Floors.Set(shelf, 11);
        var holder = map.GetComponent<MapComponent_StorageNetworks>().CreateRecovery(shelf.Position).Contents;
        var item = new Thing { stackCount = 10 }; LogicalFloors.Floors.Set(item, 0); holder.TryAdd(item, false);
        LogicalFloors.API.FloorApi.StructuralFloor = false;
        try
        {
            Equal(false, StorageFloorContext.TryDrop(holder, item, shelf, out _), "missing structural floor refuses direct placement");
            Equal(true, holder.Contains(item), "rejected API placement restores original owner");
            Equal<Thing>(null, StorageFloorContext.SpawnDistinct(holder, item, shelf), "missing structural floor refuses distinct spawn");
            Equal(true, holder.Contains(item), "rejected distinct spawn never detaches stock");
            Equal(false, item.Spawned, "rejected placement never falls back to ground floor");
            Equal(0, LogicalFloors.API.FloorApi.GetFloor(item), "rejected placement preserves remembered membership");
        }
        finally { LogicalFloors.API.FloorApi.StructuralFloor = true; }

        var original = StorageFloorContext.PlaceItem;
        StorageFloorContext.PlaceItem = ThrowPlacement;
        try
        {
            bool threw = false;
            try { StorageFloorContext.TryDrop(holder, item, shelf, out _); }
            catch (InvalidOperationException) { threw = true; }
            Equal(true, threw, "placement exception is preserved");
            Equal(true, holder.Contains(item), "placement exception retains real item in original owner");
            Equal(10, item.stackCount, "placement exception does not duplicate or consume stock");
        }
        finally { StorageFloorContext.PlaceItem = original; }
    }

    private static bool ThrowPlacement(Thing item, Map map, int floor, IntVec3 cell, out Thing result)
    { result = null; throw new InvalidOperationException("controlled placement failure"); }
}

namespace LogicalFloors.API
{
    public readonly struct FloorPosition
    {
        public readonly Map Map;
        public readonly int Floor;
        public readonly IntVec3 Cell;
        public FloorPosition(Map map, int floor, IntVec3 cell) { Map = map; Floor = floor; Cell = cell; }
    }

    public static class FloorApi
    {
        public static bool Supported = true;
        public static bool StructuralFloor = true;
        public static int CurrentFloor;
        public static Map CurrentMap;
        public static readonly Dictionary<Thing, int> Floors = new Dictionary<Thing, int>();
        public static bool SupportsApi(int version) => Supported && version == 1;
        public static int GetFloor(Thing thing) => Floors.TryGetValue(thing, out int floor) ? floor : 0;
        public static IDisposable EnterFloor(Map map, int floor) => new Scope(map, floor);
        public static bool HasStructuralFloor(FloorPosition destination) => StructuralFloor;
        public static bool TryPlaceItem(Thing item, FloorPosition destination, ThingPlaceMode mode, out Thing result)
        {
            if (item.Spawned || item.holdingOwner != null) throw new InvalidOperationException("API requires detached item");
            result = null;
            if (!HasStructuralFloor(destination)) return false;
            int previousFloor = GetFloor(item); Floors[item] = destination.Floor;
            var buffer = new ThingOwner(null); buffer.TryAdd(item, false);
            bool placed = false;
            try
            {
                using (EnterFloor(destination.Map, destination.Floor))
                    placed = buffer.TryDrop(item, destination.Cell, destination.Map, mode, out result,
                        placedAction: (stock, count) => Floors[stock] = destination.Floor, playDropSound: false);
                return placed;
            }
            finally
            {
                if (buffer.Contains(item)) buffer.Remove(item);
                if (!placed && !item.Spawned && !item.Destroyed) Floors[item] = previousFloor;
            }
        }
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
    public static class Floors
    {
        public static void Set(Thing item, int floor) => API.FloorApi.Floors[item] = floor;
    }
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
        public static ReservationManager Reservations;
        public static PhysicalInteractionReservationManager Interactions;
        private static bool Target(Map map) => map == TargetMap && map == API.FloorApi.CurrentMap && API.FloorApi.CurrentFloor == TargetFloor;
        private static TerrainGrid TerrainAt(Map map) => Target(map) ? Terrain : map.terrainGrid;
        private static PlanManager PlansAt(Map map) => Target(map) ? Plans : map.planManager;
        private static EnrouteManager EnrouteAt(Map map) => Target(map) ? Enroute : map.enrouteManager;
        private static ReservationManager ReservationsAt(Map map) => Target(map) && Reservations != null ? Reservations : map.reservationManager;
        private static PhysicalInteractionReservationManager InteractionsAt(Map map) => Target(map) && Interactions != null ? Interactions : map.physicalInteractionReservationManager;
        public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field && field.DeclaringType == typeof(Map))
                {
                    string getter = field.Name == "terrainGrid" ? nameof(TerrainAt) : field.Name == "planManager" ? nameof(PlansAt) :
                        field.Name == "enrouteManager" ? nameof(EnrouteAt) :
                        field.Name == "reservationManager" ? nameof(ReservationsAt) :
                        field.Name == "physicalInteractionReservationManager" ? nameof(InteractionsAt) : null;
                    if (getter != null)
                    { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(FieldRouting), getter); }
                }
                yield return instruction;
            }
        }
    }
}
