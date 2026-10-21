using System;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class FuelSupplierTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string message)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Building_FuelSupplier Supplier = new Building_FuelSupplier();
        internal readonly ThingDef Fuel = new ThingDef { stackLimit = 1000 };
        internal StorageNetwork Network => Core.Network;
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Register(Supplier, 2); Manager.EnsureCurrent();
        }
        internal void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageUnit unit) unit.node = node; else ((Building)thing).node = node;
            Manager.Register(node);
        }
        internal Thing Store(int count, ThingDef def = null)
        {
            var item = new Thing { def = def ?? Fuel, stackCount = count };
            Equal(true, Unit.Inventory.Contents.TryAdd(item, false), "fixture stores fuel within native stack limit"); return item;
        }
        internal CompRefuelable Target(int x, int z = 0, ThingDef fuel = null)
        {
            var building = new ThingWithComps
            {
                def = new ThingDef { category = ThingCategory.Building }, Map = Map,
                Position = new IntVec3(x, z), Spawned = true, Faction = Faction.OfPlayer
            };
            var comp = new CompRefuelable { parent = building, TargetFuelLevel = 10 };
            comp.Props.fuelFilter.SetAllow(fuel ?? Fuel, true);
            building.AllComps.Add(comp); Map.listerThings.Things.Add(building); return comp;
        }
    }
    internal static int Run()
    {
        assertions = 0;
        AreaAndTargets(); NativeRules(); HalfFullGenerator(); ImmediateSupply(); ReservationsAndRecovery(); OverlapAndOrder(); NetworkAndTicking(); CommitBoundaryExceptions();
        return assertions;
    }
    private static void AreaAndTargets()
    {
        var f = new Fixture(); f.Store(100);
        var near = f.Target(3); var boundary = f.Target(22); var outside = f.Target(23);
        var diagonal = f.Target(18, 16); var enemy = f.Target(5); enemy.parent.Faction = new Faction();
        var item = f.Target(6); item.parent.def.category = ThingCategory.Item;
        var departed = f.Target(7); departed.parent.Spawned = false;
        var foreignMap = f.Target(8); foreignMap.parent.Map = new Map();
        f.Supplier.SupplyFuel();
        Equal(10f, near.Fuel, "near fuel building supplied");
        Equal(10f, boundary.Fuel, "exact 20-cell radius included");
        Equal(0f, outside.Fuel, "outside radius excluded");
        Equal(0f, diagonal.Fuel, "circular radius rather than square area");
        Equal(0f, enemy.Fuel, "enemy building ignored");
        Equal(0f, item.Fuel, "refuelable non-building ignored");
        Equal(0f, departed.Fuel, "despawned building ignored");
        Equal(0f, foreignMap.Fuel, "other map ignored");
        Equal(80L, f.Network.GetTotalCount(f.Fuel), "only delivered fuel deducted");
        Equal(false, (object)f.Supplier is IThingHolder, "area supplier has no internal inventory");
        Equal(0, f.Map.Ground.Count, "area delivery creates no ground items");
        f.Supplier.DrawExtraSelectionOverlays(); Equal(20f, GenDraw.Radius, "selection radius ring");
        new PlaceWorker_FuelSupplier().DrawGhost(new ThingDef(), new IntVec3(0, 0), new Rot4(), new UnityEngine.Color());
        Equal(20f, GenDraw.Radius, "placement radius ring");
    }
    private static void NativeRules()
    {
        var f = new Fixture(); f.Store(100); var wrong = new ThingDef(); f.Store(50, wrong);
        var disabled = f.Target(3); disabled.allowAutoRefuel = false;
        var threshold = f.Target(4); threshold.Fuel = 9; threshold.Props.autoRefuelPercent = 0.8f;
        var emptyOnly = f.Target(5); emptyOnly.Fuel = 2; emptyOnly.Props.allowRefuelIfNotEmpty = false;
        var cooling = f.Target(6);
        var cooldown = new CompInteractable { OnCooldown = true }; cooldown.Props.cooldownPreventsRefuel = true;
        ((ThingWithComps)cooling.parent).AllComps.Add(cooldown);
        var wrongFuel = f.Target(7, fuel: new ThingDef()); var full = f.Target(8); full.Fuel = 10;
        f.Supplier.SupplyFuel();
        Equal(0f, disabled.Fuel, "auto refueling disabled"); Equal(9f, threshold.Fuel, "native percentage threshold respected");
        Equal(2f, emptyOnly.Fuel, "empty-only refueling respected"); Equal(0f, cooling.Fuel, "interaction cooldown respected");
        Equal(0f, wrongFuel.Fuel, "fuel filter respected"); Equal(10f, full.Fuel, "full target unchanged");
        Equal(100L, f.Network.GetTotalCount(f.Fuel), "skipped targets do not withdraw fuel");
        Equal(50L, f.Network.GetTotalCount(wrong), "unrelated stock untouched");
        cooldown.OnCooldown = false; threshold.Fuel = 8; emptyOnly.Fuel = 0;
        f.Supplier.SupplyFuel(); Equal(10f, threshold.Fuel, "threshold crossing permits refill to target");
        Equal(10f, emptyOnly.Fuel, "empty target can refill"); Equal(10f, cooling.Fuel, "cooldown ending permits refill");
    }
    private static void HalfFullGenerator()
    {
        var f = new Fixture(); f.Store(100); var generator = f.Target(3);
        generator.Props.fuelCapacity = 60; generator.TargetFuelLevel = 60; generator.Fuel = 30;
        Equal(false, generator.ShouldAutoRefuelNow, "half-full generator does not trigger native 30 percent threshold");
        f.Supplier.SupplyFuel(); Equal(30f, generator.Fuel, "area supplier waits above native 30 percent threshold");
        Equal(100L, f.Network.GetTotalCount(f.Fuel), "half-full generator consumes nothing");
        generator.Fuel = 18; generator.ShouldAutoRefuelNowIgnoringFuelPct = false;
        f.Supplier.SupplyFuel(); Equal(18f, generator.Fuel, "non-percentage native restrictions still block supply");
        Equal(100L, f.Network.GetTotalCount(f.Fuel), "blocked generator leaves source stock untouched");
        generator.ShouldAutoRefuelNowIgnoringFuelPct = true;
        f.Supplier.SupplyFuel(); Equal(60f, generator.Fuel, "exact native threshold triggers full refill");
        Equal(58L, f.Network.GetTotalCount(f.Fuel), "threshold refill consumes 42 missing units");
        generator.TargetFuelLevel = 40; generator.Fuel = 12;
        f.Supplier.SupplyFuel(); Equal(40f, generator.Fuel, "configured target rather than capacity respected");
        Equal(30L, f.Network.GetTotalCount(f.Fuel), "configured target consumes exact difference");
    }
    private static void ImmediateSupply()
    {
        var f = new Fixture(); f.Store(100); var target = f.Target(3);
        target.Props.fuelCapacity = 60; target.TargetFuelLevel = 60; target.Fuel = 30;
        var disabledTarget = f.Target(4); disabledTarget.Fuel = 5; disabledTarget.allowAutoRefuel = false;
        var immediate = f.Supplier.GetGizmos().OfType<Command_Action>().Single();
        Equal("MS_FuelSupply_Immediate", immediate.defaultLabel, "area supply has native immediate command");
        Equal(false, immediate.disabled, "connected area supplier enables command");
        immediate.action(); Equal(60f, target.Fuel, "immediate action bypasses half-full percentage threshold");
        Equal(5f, disabledTarget.Fuel, "immediate action respects target automatic refueling toggle");
        Equal(70L, f.Network.GetTotalCount(f.Fuel), "immediate action only consumes enabled target's missing fuel");
        target.Fuel = 30; f.Supplier.SupplyFuel(); Equal(30f, target.Fuel, "immediate action does not permanently bypass threshold");
        target.ShouldAutoRefuelNowIgnoringFuelPct = false; immediate.action();
        Equal(30f, target.Fuel, "immediate action respects non-percentage native restrictions");
        target.ShouldAutoRefuelNowIgnoringFuelPct = true; target.Props.allowRefuelIfNotEmpty = false; immediate.action();
        Equal(30f, target.Fuel, "immediate action respects empty-only refueling");
        target.Props.allowRefuelIfNotEmpty = true; target.allowAutoRefuel = false; immediate.action();
        Equal(30f, target.Fuel, "immediate action preserves disabled generator");
        f.Manager.Unregister(f.Supplier.node); f.Manager.EnsureCurrent();
        Equal(true, f.Supplier.GetGizmos().OfType<Command_Action>().Single().disabled, "disconnected supplier disables immediate gizmo");
    }
    private static void ReservationsAndRecovery()
    {
        var f = new Fixture(); var stack = f.Store(30); object reservation = new object();
        f.Network.ReserveOutgoing(reservation, stack, 20); var target = f.Target(3); target.TargetFuelLevel = 20;
        f.Supplier.SupplyFuel(); Equal(10f, target.Fuel, "available fraction supplied while reservations remain");
        Equal(20L, f.Network.GetTotalCount(f.Fuel), "reserved fuel kept");
        Equal(0, f.Network.AvailableToWithdraw(stack), "other task's claim still active");
        f.Network.ReleaseOutgoing(reservation); f.Supplier.SupplyFuel(); Equal(10f, target.Fuel, "released stock still respects target threshold");
        Equal(20L, f.Network.GetTotalCount(f.Fuel), "above-threshold target consumes nothing");
        target.Fuel = 6; f.Supplier.SupplyFuel(); Equal(20f, target.Fuel, "released fuel available once native threshold is reached");
        Equal(6L, f.Network.GetTotalCount(f.Fuel), "exact remaining requirement deducted");
        var atomic = new Fixture(); atomic.Store(9); var a = atomic.Target(3);
        a.Props.atomicFueling = true; a.Props.fuelCapacity = 10;
        atomic.Supplier.SupplyFuel(); Equal(0f, a.Fuel, "atomic target waits for enough fuel");
        Equal(9L, atomic.Network.GetTotalCount(atomic.Fuel), "insufficient atomic stock unchanged");
        atomic.Store(1); atomic.Supplier.SupplyFuel(); Equal(10f, a.Fuel, "atomic supply collects several stacks");
        Equal(0L, atomic.Network.GetTotalCount(atomic.Fuel), "atomic stock consumed once");
    }
    private static void OverlapAndOrder()
    {
        var f = new Fixture(); f.Store(15); var far = f.Target(10); var near = f.Target(4);
        var second = new Building_FuelSupplier(); f.Register(second, 3); f.Manager.EnsureCurrent();
        f.Supplier.SupplyFuel(); second.SupplyFuel();
        Equal(10f, near.Fuel, "nearest target supplied first even if registered last");
        Equal(5f, far.Fuel, "far target gets remaining stock");
        Equal(0L, f.Network.GetTotalCount(f.Fuel), "overlapping suppliers cannot consume twice");
        f.Store(10); second.SupplyFuel(); Equal(5f, far.Fuel, "overlapping supplier also respects native threshold");
        far.Fuel = 3; second.SupplyFuel(); Equal(10f, far.Fuel, "new fuel supplied when native threshold is reached");
        Equal(1, near.RefuelCalls, "full target not refueled by second supplier");
    }
    private static void NetworkAndTicking()
    {
        var f = new Fixture(); var target = f.Target(3); int originalTick = Find.TickManager.TicksGame;
        try
        {
            Find.TickManager.TicksGame = 0; f.Supplier.RunTransportTestTick(); f.Store(30);
            Find.TickManager.TicksGame = 59;
            f.Supplier.RunTransportTestTick(); Equal(0f, target.Fuel, "no scan between configured refresh intervals");
            Find.TickManager.TicksGame++; f.Supplier.RunTransportTestTick(); Equal(10f, target.Fuel, "configured periodic tick supplies fuel");
        }
        finally { Find.TickManager.TicksGame = originalTick; }
        target.Fuel = 0; f.Manager.Unregister(f.Supplier.node); f.Supplier.SupplyFuel();
        Equal(0f, target.Fuel, "disconnected supplier cannot deliver");
        Equal(20L, f.Unit.Inventory.GetTotalCount(f.Fuel), "disconnected supplier leaves source stock");
    }

    private static void CommitBoundaryExceptions()
    {
        var f = new Fixture(); f.Store(20); var target = f.Target(3);
        target.BeforeRefuel = () => { throw new InvalidOperationException("before adding fuel"); };
        bool threw = false;
        try { f.Supplier.SupplyFuel(); } catch (InvalidOperationException) { threw = true; }
        Equal(true, threw, "pre-refuel failure observed");
        Equal(0f, target.Fuel, "pre-refuel failure adds no fuel");
        Equal(20L, f.Network.GetTotalCount(f.Fuel), "pre-refuel failure restores entire batch");
        target.BeforeRefuel = null; f.Supplier.SupplyFuel();
        Equal(10f, target.Fuel, "retry after pre-refuel failure delivers once");
        Equal(10L, f.Network.GetTotalCount(f.Fuel), "retry charges exact fuel quantity");

        f = new Fixture(); f.Store(20); f.Store(20); target = f.Target(3); target.TargetFuelLevel = 30;
        target.AfterRefuel = () => { throw new InvalidOperationException("after adding fuel"); };
        threw = false;
        try { f.Supplier.SupplyFuel(); } catch (InvalidOperationException) { threw = true; }
        Equal(true, threw, "post-refuel signal failure observed");
        Equal(20f, target.Fuel, "first stack already changed actual fuel");
        Equal(20L, f.Network.GetTotalCount(f.Fuel), "credited fuel remains charged while unprocessed stack is restored");
        Equal(0, f.Map.Ground.Count, "unused material returns directly to inventory");
        target.AfterRefuel = null; f.Supplier.SupplyFuel(true);
        Equal(30f, target.Fuel, "later supply only fills remaining demand");
        Equal(10L, f.Network.GetTotalCount(f.Fuel), "post-exception retry does not charge or credit first stack again");
    }
}
