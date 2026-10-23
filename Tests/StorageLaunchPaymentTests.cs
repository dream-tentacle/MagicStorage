using System;
using System.Linq;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using Verse;

internal static class StorageLaunchPaymentTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Launch payment: " + message + "; expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map { UseCellThingLists = true };
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Building_OrbitalTradeBeacon Beacon = new Building_OrbitalTradeBeacon();
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Manager.EnsureCurrent();
            Beacon.TradeableCells.Add(Core.Position); Beacon.TradeableCells.Add(new IntVec3(2, 0));
            Map.Beacons.Add(Beacon);
        }
        internal void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageUnit unit) unit.node = node;
            if (thing is Building_StorageCore core) { core.node = node; Map.listerBuildings.allBuildingsColonist.Add(core); }
            Manager.Register(node);
        }
        internal Thing Stock(ThingDef def, int count)
        {
            var item = new Thing { def = def, stackCount = count };
            Unit.Inventory.Contents.TryAdd(item, false); return item;
        }
        internal Thing Ground(ThingDef def, int count, int x = 2)
        {
            var item = new Thing { def = def, stackCount = count, Spawned = true, Map = Map, Position = new IntVec3(x, 0) };
            Map.Ground.Add(item); return item;
        }
        internal int Balance(ThingDef def) => ForwardingPaymentMod.Balance(Map, def);
        internal void Pay(ThingDef def, int count) => ForwardingPaymentMod.Pay(Map, def, count);
    }

    internal static int Run()
    {
        RuntimeHelpers.RunClassConstructor(typeof(StorageLaunchPaymentPatches).TypeHandle);
        Queries(); MixedAndGeneric(); ReservedAndInsufficient(); Eligibility(); MultipleNetworks();
        Rollback(); NativePaths();
        return checks;
    }

    private static void Queries()
    {
        var f = new Fixture(); var def = new ThingDef();
        var stock = f.Stock(def, 50); f.Ground(def, 10); f.Ground(def, 90, 90);
        var overlap = new Building_OrbitalTradeBeacon();
        overlap.TradeableCells.UnionWith(f.Beacon.TradeableCells); f.Map.Beacons.Add(overlap);
        f.Map.listerBuildings.allBuildingsColonist.Add(f.Core);
        Equal(60, f.Balance(def), "native ground plus stored stock counted once under overlapping beacons/cores");
        Equal(true, TradeUtility.AllLaunchableThingsForTrade(f.Map).Contains(stock), "query returns original real stack");
        Equal(10, TradeUtility.AllLaunchableThingsForTrade(f.Map, new TradeShip()).Sum(t => t.stackCount), "explicit ship query remains native");
        Equal(50L, f.Core.Network.GetTotalCount(def), "queries do not extract or mutate stock");
        Equal(2, f.Map.Ground.Count, "queries create no loose copies");
    }

    private static void MixedAndGeneric()
    {
        var f = new Fixture(); var silver = ThingDefOf.Silver;
        var ground = f.Ground(silver, 20); f.Stock(silver, 50);
        f.Map.reservationManager.Reserved.Add(ground);
        TradeUtility.LaunchSilver(f.Map, 35);
        Equal(true, ground.Destroyed, "native silver forwarding consumes ground first even when a job reserves it");
        Equal(35L, f.Core.Network.GetTotalCount(silver), "storage supplies exactly fifteen remaining silver");
        Equal(35, f.Balance(silver), "post-payment query sees updated index");
        var def = new ThingDef(); f.Stock(def, 25); f.Pay(def, 20);
        Equal(5L, f.Unit.Inventory.GetTotalCount(def), "forwarding mod pays a resource other than silver");
        f.Pay(def, 5);
        Equal(0L, f.Core.Network.GetTotalCount(def), "whole final stack consumed once");
        Equal(0, f.Core.Network.AvailableToWithdraw(ground), "destroyed ground is not a stored source");
    }

    private static void ReservedAndInsufficient()
    {
        var f = new Fixture(); var def = new ThingDef();
        var ground = f.Ground(def, 5); var stock = f.Stock(def, 50); var claim = new object();
        f.Core.Network.ReserveOutgoing(claim, stock, 10);
        Equal(5, f.Balance(def), "partly reserved real stack excluded instead of overstating available count");
        int rejects = Messages.Rejects;
        f.Pay(def, 10);
        Equal(rejects + 1, Messages.Rejects, "insufficient available payment rejected");
        Equal(5, ground.stackCount, "failed payment never partly consumes ground");
        Equal(50L, f.Core.Network.GetTotalCount(def), "failed payment never consumes stored stock");
        Equal(true, f.Core.Network.HasOutgoingReservation(claim, stock, 10), "other task reservation preserved");
        f.Core.Network.ReleaseOutgoing(claim);
        Equal(55, f.Balance(def), "released stack becomes visible again");
        var incoming = new object();
        f.Core.Network.ReserveIncoming(incoming, new Thing { def = def, stackCount = 1 }, 1);
        Equal(5, f.Balance(def), "incoming merge claim excludes its real destination stack");
        f.Core.Network.ReleaseIncoming(incoming);
        f.Pay(def, 56);
        Equal(55, f.Balance(def), "one resource short leaves whole payment untouched");
        f.Pay(def, 55);
        Equal(0, f.Balance(def), "exact balance pays completely");
        Equal(0L, f.Core.Network.GetTotalCount(def), "exact payment updates storage index");
    }

    private static void Eligibility()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 20);
        f.Beacon.Powered = false; Equal(0, f.Balance(def), "unpowered beacon hides network");
        f.Beacon.Powered = true; f.Beacon.TradeableCells.Remove(f.Core.Position);
        Equal(0, f.Balance(def), "covering a unit alone is insufficient");
        f.Beacon.TradeableCells.Add(f.Unit.Position); Equal(0, f.Balance(def), "only core coverage qualifies");
        f.Beacon.TradeableCells.Add(f.Core.Position); f.Map.Fog.Add(f.Core.Position);
        Equal(0, f.Balance(def), "fogged core excluded"); f.Map.Fog.Clear();
        f.Core.Faction = new Faction(); Equal(0, f.Balance(def), "foreign core excluded");
        f.Core.Faction = Faction.OfPlayer; stock.stale = true;
        Equal(0, f.Balance(def), "native sellability rule applied to stored resources"); stock.stale = false;
        var ground = f.Ground(def, 5);
        Equal(25, f.Balance(def), "initial query can cover requested fee");
        f.Beacon.TradeableCells.Remove(f.Core.Position);
        f.Pay(def, 25);
        Equal(5, ground.stackCount, "lost core coverage between query and payment cannot partly consume ground");
        Equal(20L, f.Core.Network.GetTotalCount(def), "lost coverage leaves network untouched");
        f.Beacon.TradeableCells.Add(f.Core.Position);
        var duplicate = new Building_StorageCore(); f.Register(duplicate, 2); f.Manager.EnsureCurrent();
        Equal(5, f.Balance(def), "invalid multiple-core network excluded while native ground remains");
    }

    private static void MultipleNetworks()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Stock(def, 20);
        var core = new Building_StorageCore(); var unit = new Building_StorageUnit();
        f.Register(core, 50); f.Register(unit, 51); f.Manager.EnsureCurrent();
        f.Beacon.TradeableCells.Add(core.Position);
        unit.Inventory.Contents.TryAdd(new Thing { def = def, stackCount = 25 }, false);
        Equal(45, f.Balance(def), "query combines eligible independent networks");
        f.Pay(def, 30);
        Equal(15, f.Balance(def), "single payment spans networks");
        Equal(0L, f.Core.Network.GetTotalCount(def), "first network emptied first");
        Equal(15L, core.Network.GetTotalCount(def), "second network pays only remainder");
    }

    private static void Rollback()
    {
        foreach (bool throws in new[] { false, true })
        {
            var f = new Fixture(); var def = new ThingDef { stackLimit = 1000 };
            f.Ground(def, 5); var first = f.Stock(def, 20); var second = f.Stock(def, 20);
            Thing.BeforePaymentTestSplit = item =>
            {
                if (item != second) return;
                if (throws) throw new InvalidOperationException("injected extraction failure");
                f.Beacon.Powered = false;
            };
            try { f.Pay(def, 30); Equal(false, throws, "nonthrowing loss of coverage rejects payment"); }
            catch (InvalidOperationException) { Equal(true, throws, "injected exception propagated after cleanup"); }
            finally { Thing.BeforePaymentTestSplit = null; f.Beacon.Powered = true; }
            Equal(45, f.Balance(def), "failed staging or final eligibility restores complete balance");
            Equal(40L, f.Core.Network.GetTotalCount(def), "extracted network stock restored to index");
            Equal(5, f.Map.Ground.Where(t => t.Spawned && !t.Destroyed).Sum(t => t.stackCount), "staged ground returned without duplication");
            Equal(40, f.Core.Network.AvailableToWithdraw(first) + f.Core.Network.AvailableToWithdraw(second), "failure releases all payment reservations");
        }
    }

    private static void NativePaths()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Stock(def, 30); f.Ground(def, 10);
        int calls = TradeUtility.NativeLaunches; var ship = new TradeShip();
        TradeUtility.LaunchThingsOfType(def, 5, f.Map, ship);
        Equal(calls + 1, TradeUtility.NativeLaunches, "explicit ship payment runs original");
        Equal(5, ship.Stock.Sum(t => t.stackCount), "ship receives native ground goods");
        Equal(30L, f.Core.Network.GetTotalCount(def), "ship payment leaves network unchanged");
        f.Pay(def, 2);
        Equal(calls + 1, TradeUtility.NativeLaunches, "network payment suppresses native second charge");
        Equal(33, f.Balance(def), "ground-only payment leaves network intact");
        f.Beacon.TradeableCells.Remove(f.Core.Position);
        f.Pay(def, 1);
        Equal(calls + 2, TradeUtility.NativeLaunches, "map without eligible core keeps native payment");
        f.Pay(def, 0); f.Pay(def, -1);
        Equal(30L, f.Core.Network.GetTotalCount(def), "zero and negative fees never consume stored stock");
    }
}
