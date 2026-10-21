using System;
using System.Linq;
using HarmonyLib;
using MagicStorage;
using RimWorld;
using Verse;
using Nivarian_Race.Code.Comps.BuildingComps;
using Nivarian_Race.Code.Logistics;
using Nivarian_Race.Code.Logistics.Orders;

internal static class NivarianCannonTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string message)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(message + ": " + expected + " != " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Building_NivarianCannonExtractor Port = new Building_NivarianCannonExtractor();
        internal readonly CannonOrderLedger Ledger = new CannonOrderLedger();
        internal readonly CannonNetworkMapComponent CannonNetwork = new CannonNetworkMapComponent();
        internal readonly ThingDef Steel = new ThingDef { stackLimit = 1000 };
        internal readonly Thing Target;
        internal readonly CompCannonNode Cannon;
        internal StorageNetwork Network => Core.Network;
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture()
        {
            CannonLogisticsUtility.Nodes.Clear();
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Register(Port, 2);
            Port.TestComps.Add(new CompPowerTrader()); Port.TestComps.Add(new CompFlickable());
            Port.SpawnSetup(Map, false); Manager.EnsureCurrent();
            Target = new Thing { Map = Map, Spawned = true, Faction = Faction.OfPlayer, Position = new IntVec3(20, 0) };
            Cannon = AddCannon(3, 0); CannonNetwork.Destination = Cannon;
        }
        internal void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Spawned = true; thing.Faction = Faction.OfPlayer; thing.Position = new IntVec3(x, 0);
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageUnit unit) unit.node = node; else ((Building)thing).node = node;
            Manager.Register(node);
        }
        internal CompCannonNode AddCannon(int x, int z)
        {
            var node = new CompCannonNode { parent = new Thing { Map = Map, Position = new IntVec3(x, z), Spawned = true, Faction = Faction.OfPlayer } };
            CannonLogisticsUtility.Nodes.Add(node); return node;
        }
        internal Thing Store(int count, ThingDef def = null)
        {
            var item = new Thing { def = def ?? Steel, stackCount = count };
            if (!Unit.Inventory.Contents.TryAdd(item, false)) throw new Exception("fixture store failed"); return item;
        }
        internal ResKey Key(CannonCargoSinkKind sink = CannonCargoSinkKind.Ingredient, int index = -1) =>
            new ResKey { destBuilding = Target, def = Steel, sink = sink, fuelCompIndex = index };
        internal void Match(int count, CannonCargoSinkKind sink = CannonCargoSinkKind.Ingredient, int index = -1)
        {
            Ledger.Need = count;
            var key = Key(sink, index);
            int deficit = count - Ledger.ReservedOf(key);
            if (deficit > 0) OrderController.Match(Ledger, CannonNetwork, Map,
                new OrderDemandCalculator.NeedEntry { key = key, need = count, priority = count }, deficit);
        }
        internal int Cargo => Cannon.Contents.Sum(item => item.stackCount);
    }

    internal static int Run()
    {
        new NivarianCannonCompatibility().Apply(new Harmony("mjcg.magicstorage.tests.nivarian"));
        BasicAndRemainder(); CapacityAndRouting(); ReservationsAndFailures(); LifecycleAndSources(); FuelOrderAndNativeReservations();
        AcceptanceBoundaries();
        return assertions;
    }
    private static void BasicAndRemainder()
    {
        var f = new Fixture(); f.Store(100); f.Match(30);
        Equal(30, f.Cargo, "actual items reach cannon"); Equal(70L, f.Network.GetTotalCount(f.Steel), "network withdraws exact demand");
        Equal(0, f.Ledger.GroundCalls, "fully met demand skips ground dispatch"); Equal(1, f.Cannon.Accepted.Count, "cargo carries an order");
        Equal(f.Target, f.Cannon.Accepted[0].target, "order keeps final destination"); Equal(true, f.Cannon.Accepted[0].Stocked, "native enqueue stocks order");
        Equal(false, (object)f.Port is IThingHolder, "extractor has no item buffer"); f.Match(30);
        Equal(30, f.Cargo, "reserved in-transit order prevents duplicate extraction");
        f = new Fixture(); f.Store(12); f.Match(30);
        Equal(12, f.Cargo, "partial stock supplied"); Equal(18, f.Ledger.GroundDeficit, "only unsatisfied remainder reaches ground intake");
        Equal(0, f.Map.Ground.Count, "no intermediate ground items");
        f = new Fixture(); var other = new ThingDef { stackLimit = 1000 }; f.Store(80, other); f.Match(30);
        Equal(0, f.Cargo, "unrequested type untouched"); Equal(30, f.Ledger.GroundDeficit, "missing type retains native behavior");
    }
    private static void CapacityAndRouting()
    {
        var f = new Fixture(); f.Store(200); f.Match(100);
        Equal(75, f.Cargo, "cannon physical capacity respected"); Equal(25, f.Ledger.GroundDeficit, "capacity shortage passed onward");
        f = new Fixture(); f.Store(100); f.Cannon.ShotLimit = 10; f.Match(30);
        Equal(10, f.Cargo, "single order respects shot limit"); Equal(20, f.Ledger.GroundDeficit, "remaining demand preserved");
        f = new Fixture(); f.Store(60); f.Cannon.Distance = int.MaxValue; f.Match(20);
        Equal(60L, f.Network.GetTotalCount(f.Steel), "unreachable destination causes no withdrawal");
        f = new Fixture(); f.Store(60); f.Cannon.Distance = 2; var diagonal = f.AddCannon(3, 1); diagonal.Distance = 1; f.Match(20);
        Equal(20, diagonal.Contents.Sum(item => item.stackCount), "diagonal adjacency and shorter route selected"); Equal(0, f.Cargo, "longer route left unused");
        f = new Fixture(); f.Store(60); f.Cannon.Load = 50; var light = f.AddCannon(3, 1); f.Match(20);
        Equal(20, light.Contents.Sum(item => item.stackCount), "equal-hop path prefers lower load");
        f = new Fixture(); f.Store(60); f.Cannon.parent.Position = new IntVec3(4, 0); f.Match(20);
        Equal(0, f.Cargo, "two-cell gap is not adjacency");
    }
    private static void ReservationsAndFailures()
    {
        var f = new Fixture(); var item = f.Store(60); var owner = new object();
        Equal(45, f.Network.ReserveOutgoing(owner, item, 45), "other task reserves stock"); f.Match(30);
        Equal(15, f.Cargo, "only unreserved stock extracted"); Equal(true, f.Network.HasOutgoingReservation(owner, item, 45), "other reservation preserved");
        Equal(15, f.Ledger.GroundDeficit, "reserved stock does not fulfil demand");
        f = new Fixture(); f.Store(60); f.Cannon.Reject = true; f.Match(20);
        Equal(60L, f.Network.GetTotalCount(f.Steel), "rejection restores original network stock"); Equal(0, f.Cargo, "rejected item not in cannon");
        Equal(true, f.Ledger.Orders.All(order => order.Cancelled), "failed orders cancelled");
        Equal(20, f.Ledger.GroundDeficit, "failure keeps original dispatch"); Equal(0, f.Map.Ground.Count, "rollback does not drop cargo");
        f.Cannon.Reject = false; f.Ledger.GroundDeficit = 0; f.Match(20);
        Equal(20, f.Cargo, "retry has no leaked reservation");
        f = new Fixture(); f.Store(60); f.Cannon.Throw = true; f.Match(20);
        Equal(60L, f.Network.GetTotalCount(f.Steel), "exception rolls material back"); Equal(20, f.Ledger.GroundDeficit, "exception preserves ground fallback");
        Equal(true, f.Ledger.Orders.All(order => order.Cancelled), "exception cancels open order");
        f = new Fixture(); f.Store(60); f.Ledger.OnPlace = () => { throw new InvalidOperationException("fixture place failure"); }; f.Match(20);
        Equal(60L, f.Network.GetTotalCount(f.Steel), "order creation exception rolls back withdrawn item"); Equal(20, f.Ledger.GroundDeficit, "order creation failure retains deficit");
    }
    private static void LifecycleAndSources()
    {
        Action<Fixture>[] disable = {
            f => f.Port.GetComp<CompPowerTrader>().PowerOn = false,
            f => f.Port.GetComp<CompFlickable>().SwitchIsOn = false,
            f => f.Port.Faction = new Faction(),
            f => f.Cannon.Powered = false,
            f => f.Cannon.parent.Faction = new Faction(),
            f => f.CannonNetwork.Destination = null,
            f => f.Target.Faction = new Faction(),
            f => f.Port.DeSpawn(),
            f => { f.Manager.Unregister(f.Core.node); f.Core.Spawned = false; }
        };
        foreach (var change in disable)
        {
            var f = new Fixture(); f.Store(60); change(f); f.Match(20);
            Equal(0, f.Cargo, "inactive endpoint never delivers"); Equal(20, f.Ledger.GroundDeficit, "inactive endpoint retains native dispatch");
        }
        var fixture = new Fixture(); fixture.Store(60); fixture.Port.DeSpawn(); fixture.Manager.Register(fixture.Port.node);
        fixture.Port.SpawnSetup(fixture.Map, true); fixture.Match(20);
        Equal(20, fixture.Cargo, "spawn after load re-registers extraction endpoint");
        var f2 = new Fixture(); f2.Store(60);
        var second = new Building_NivarianCannonExtractor(); f2.Register(second, 2); second.Position = new IntVec3(2, 1);
        second.TestComps.Add(new CompPowerTrader()); second.SpawnSetup(f2.Map, false); f2.Match(20);
        Equal(20, f2.Cargo, "multiple ports on same network do not duplicate demand");
    }
    private static void FuelOrderAndNativeReservations()
    {
        var f = new Fixture(); f.Store(100);
        var key = f.Key(CannonCargoSinkKind.Fuel, 2);
        f.Ledger.PlaceOrder(f.Target, new System.Collections.Generic.List<CannonCargoOrderLine> {
            new CannonCargoOrderLine { def = f.Steel, count = 12, sink = key.sink, fuelCompIndex = 2 }
        }, 30);
        f.Match(30, CannonCargoSinkKind.Fuel, 2);
        Equal(18, f.Cargo, "native in-flight reservation deducted before extraction");
        var line = f.Cannon.Accepted.Last().lines[0];
        Equal(CannonCargoSinkKind.Fuel, line.sink, "fuel sink preserved"); Equal(2, line.fuelCompIndex, "specific fuel component index preserved");
        Equal(0, f.Ledger.GroundCalls, "remaining demand met without duplicate native intake");
    }

    private static void AcceptanceBoundaries()
    {
        foreach (bool throws in new[] { false, true })
        {
            var f = new Fixture(); f.Store(60);
            f.Cannon.ThrowAfterAccept = throws; f.Cannon.ReturnFalseAfterAccept = !throws;
            f.Match(20);
            Equal(20, f.Cargo, "accepted cargo survives false return or post-acceptance exception");
            Equal(40L, f.Network.GetTotalCount(f.Steel), "accepted cargo not refunded");
            Equal(false, f.Ledger.Orders.Single().Cancelled, "actual accepted order is never cancelled");
            Equal(0, f.Ledger.GroundDeficit, "accepted cargo does not trigger duplicate ground supply");
            f.Match(20);
            Equal(20, f.Cargo, "later demand matching does not duplicate accepted order");
        }

        var partial = new Fixture(); partial.Store(60); partial.Cannon.AcceptLimit = 7;
        partial.Cannon.ReturnFalseAfterAccept = true; partial.Match(20);
        Equal(7, partial.Cargo, "partial enqueue retains only actually accepted cargo");
        Equal(53L, partial.Network.GetTotalCount(partial.Steel), "partial enqueue returns unaccepted remainder");
        Equal(7, partial.Ledger.Orders.Single().lines[0].count, "partial receipt corrects native order reservation");
        Equal(7, partial.Ledger.Orders.Single().lines[0].originalCount, "partial receipt keeps original order quantity consistent");
        Equal(13, partial.Ledger.GroundDeficit, "only actual partial shortage falls back to ground supply");
        partial.Cannon.AcceptLimit = int.MaxValue; partial.Cannon.ReturnFalseAfterAccept = false;
        partial.Match(20);
        Equal(20, partial.Cargo, "later matching fulfils only remaining partial demand");
        Equal(40L, partial.Network.GetTotalCount(partial.Steel), "partial retry charges total requested quantity once");

        var noAccept = new Fixture(); noAccept.Store(60); noAccept.Cannon.ReturnTrueWithoutAccept = true; noAccept.Match(20);
        Equal(0, noAccept.Cargo, "success status alone is not treated as physical acceptance");
        Equal(60L, noAccept.Network.GetTotalCount(noAccept.Steel), "false success restores source stock");
        Equal(true, noAccept.Ledger.Orders.All(order => order.Cancelled), "unstocked false-success order cancelled");
        Equal(20, noAccept.Ledger.GroundDeficit, "unaccepted demand retains native fallback");
    }
}
