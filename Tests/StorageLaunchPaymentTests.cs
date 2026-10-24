using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using HarmonyLib;
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
        ExtractionCleanup(); NativePaths(); PaymentsWithoutBeacon(); ForeignContainer();
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
        int rejects = Messages.Rejects, errors = Log.Errors.Count;
        f.Pay(def, 10);
        Equal(rejects, Messages.Rejects, "storage source does not reject the whole payment");
        Equal(errors + 1, Log.Errors.Count, "native path handles an unpaid remainder");
        Equal(true, ground.Destroyed, "native ground deduction remains authoritative");
        Equal(50L, f.Core.Network.GetTotalCount(def), "reserved stock is not supplied");
        Equal(true, f.Core.Network.HasOutgoingReservation(claim, stock, 10), "other task reservation preserved");
        f.Core.Network.ReleaseOutgoing(claim);
        Equal(50, f.Balance(def), "released stack becomes visible again");
        var incoming = new object();
        f.Core.Network.ReserveIncoming(incoming, new Thing { def = def, stackCount = 1 }, 1);
        Equal(0, f.Balance(def), "incoming merge claim excludes its real destination stack");
        f.Core.Network.ReleaseIncoming(incoming);
        f.Pay(def, 50);
        Equal(0, f.Balance(def), "exact balance pays completely");
        Equal(0L, f.Core.Network.GetTotalCount(def), "exact payment updates storage index");
    }

    private static void Eligibility()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 20);
        f.Beacon.Powered = false; Equal(20, f.Balance(def), "network balance does not require a powered beacon");
        f.Beacon.Powered = true; f.Beacon.TradeableCells.Remove(f.Core.Position);
        Equal(20, f.Balance(def), "uncovered core remains available for payment");
        f.Beacon.TradeableCells.Add(f.Unit.Position); Equal(20, f.Balance(def), "payment does not depend on which node is covered");
        f.Beacon.TradeableCells.Add(f.Core.Position); f.Map.Fog.Add(f.Core.Position);
        Equal(0, f.Balance(def), "fogged core excluded"); f.Map.Fog.Clear();
        f.Core.Faction = new Faction(); Equal(0, f.Balance(def), "foreign core excluded");
        f.Core.Faction = Faction.OfPlayer; stock.stale = true;
        Equal(0, f.Balance(def), "native sellability rule applied to stored resources"); stock.stale = false;
        var ground = f.Ground(def, 5);
        Equal(25, f.Balance(def), "initial query can cover requested fee");
        f.Beacon.TradeableCells.Remove(f.Core.Position);
        f.Pay(def, 25);
        Equal(true, ground.Destroyed, "payment succeeds after core loses beacon coverage");
        Equal(0L, f.Core.Network.GetTotalCount(def), "uncovered network supplies the remaining fee");
        f.Stock(def, 20); ground = f.Ground(def, 5);
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

    private static void ExtractionCleanup()
    {
        foreach (bool throws in new[] { false, true })
        {
            var f = new Fixture(); var def = new ThingDef { stackLimit = 1000 };
            f.Ground(def, 5); var first = f.Stock(def, 20); var second = f.Stock(def, 20);
            Thing.BeforePaymentTestSplit = item =>
            {
                if (item != second) return;
                if (throws) throw new InvalidOperationException("injected extraction failure");
                f.Map.Fog.Add(f.Core.Position);
            };
            try { f.Pay(def, 30); Equal(false, throws, "ineligible source stops supplying further items"); }
            catch (InvalidOperationException) { Equal(true, throws, "injected exception propagated after cleanup"); }
            finally { Thing.BeforePaymentTestSplit = null; f.Map.Fog.Clear(); }
            Equal(20, f.Balance(def), "only the unconsumed extraction is restored");
            Equal(20L, f.Core.Network.GetTotalCount(def), "unconsumed network stock restored to index");
            Equal(0, f.Map.Ground.Where(t => t.Spawned && !t.Destroyed).Sum(t => t.stackCount), "existing ground deduction is not rolled back");
            Equal(true, first.Destroyed, "already consumed stored stock is not recreated");
            Equal(20, f.Core.Network.AvailableToWithdraw(second), "failure releases the source reservation");
        }

        var failed = new Fixture(); var resource = new ThingDef(); failed.Stock(resource, 40); bool injected = false;
        Thing.BeforePaymentTestSplit = item =>
        {
            if (!injected && item.holdingOwner?.Owner is StorageRecoveryBatch)
            {
                injected = true;
                throw new InvalidOperationException("injected native consumption failure");
            }
        };
        try { failed.Pay(resource, 10); throw new Exception("expected native consumption exception"); }
        catch (InvalidOperationException) { }
        finally { Thing.BeforePaymentTestSplit = null; }
        Equal(40, failed.Balance(resource), "native exception restores our unconsumed staged stack");
        failed.Pay(resource, 10);
        Equal(30, failed.Balance(resource), "next payment has no stale extraction scope or reservations");

        var outer = new Fixture(); var inner = new Fixture();
        outer.Stock(resource, 40); inner.Stock(resource, 30); bool nested = false;
        Thing.BeforePaymentTestSplit = item =>
        {
            if (nested || !(item.holdingOwner?.Owner is StorageRecoveryBatch)) return;
            nested = true; inner.Pay(resource, 7);
        };
        try { outer.Pay(resource, 9); }
        finally { Thing.BeforePaymentTestSplit = null; }
        Equal(true, nested, "native consumption triggered a nested payment");
        Equal(31, outer.Balance(resource), "outer source retained its own extraction scope");
        Equal(23, inner.Balance(resource), "nested source paid independently");
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
        Equal(calls + 2, TradeUtility.NativeLaunches, "ordinary payment still runs the original loop");
        Equal(33, f.Balance(def), "ground-only payment leaves network intact");
        f.Manager.Unregister(f.Core.node);
        f.Pay(def, 1);
        Equal(calls + 3, TradeUtility.NativeLaunches, "map without eligible core keeps native payment");
        f.Pay(def, 0); f.Pay(def, -1);
        Equal(30L, f.Unit.Inventory.GetTotalCount(def), "zero and negative fees never consume stored stock");
    }

    private static void PaymentsWithoutBeacon()
    {
        var f = new Fixture(); var silver = ThingDefOf.Silver;
        f.Map.Beacons.Clear(); f.Stock(silver, 50); var ground = f.Ground(silver, 40, 90);
        Equal(50, f.Balance(silver), "stored payment balance available with no beacons on map");
        TradeUtility.LaunchSilver(f.Map, 35);
        Equal(15L, f.Core.Network.GetTotalCount(silver), "native LaunchSilver consumes storage without any beacon");
        Equal(40, ground.stackCount, "ground silver outside beacon coverage remains untouched");
        int rejects = Messages.Rejects;
        TradeUtility.LaunchSilver(f.Map, 16);
        Equal(rejects, Messages.Rejects, "source exhaustion does not introduce a whole-payment rejection");
        Equal(0L, f.Core.Network.GetTotalCount(silver), "source supplies available stock to the native loop");
        Equal(40, ground.stackCount, "insufficient payment leaves uncovered ground untouched");

        f = new Fixture(); f.Ground(silver, 5); f.Stock(silver, 20); var last = f.Stock(silver, 20);
        Thing.BeforePaymentTestSplit = item => { if (item == last) f.Beacon.Powered = false; };
        try { TradeUtility.LaunchSilver(f.Map, 30); }
        finally { Thing.BeforePaymentTestSplit = null; }
        Equal(15L, f.Core.Network.GetTotalCount(silver), "beacon losing power during staging does not invalidate stored payment");
        Equal(0, f.Map.Ground.Where(t => t.Spawned && !t.Destroyed).Sum(t => t.stackCount), "already staged ground included exactly once");
    }

    // A synthetic storage mod extends the native grid enumeration with held items.
    // Its own hooks execute; production code never inspects this container.
    private static class ContainerMod
    {
        internal static Map Map;
        internal static ThingOwner Contents;
        internal static int Before, After;
        private static void Query(Map map, IntVec3 cell, ref IEnumerable<Thing> __result)
        {
            if (map == Map && cell == new IntVec3(2, 0))
                __result = __result.Concat(Contents.ToArray());
        }
        private static void Begin() { Before++; }
        private static void End() { After++; }
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(TradeUtility), nameof(TradeUtility.LaunchThingsAt)),
                postfix: new HarmonyMethod(typeof(ContainerMod), nameof(Query)));
            harmony.Patch(AccessTools.Method(typeof(TradeUtility), nameof(TradeUtility.LaunchThingsOfType)),
                prefix: new HarmonyMethod(typeof(ContainerMod), nameof(Begin)),
                postfix: new HarmonyMethod(typeof(ContainerMod), nameof(End)));
        }
    }

    private static void ForeignContainer()
    {
        var harmony = new Harmony("magicstorage.tests.payment.container");
        ContainerMod.Install(harmony);
        try
        {
            foreach (bool ready in new[] { true, false })
            {
                var f = new Fixture(); var def = new ThingDef(); f.Stock(def, 40);
                if (!ready) { f.Manager.Unregister(f.Core.node); f.Manager.EnsureCurrent(); }
                ContainerMod.Map = f.Map; ContainerMod.Contents = new ThingOwner(null);
                var held = new Thing { def = def, stackCount = 30 };
                ContainerMod.Contents.TryAdd(held, false);
                Equal(ready ? 70 : 30, f.Balance(def), "container query preserved with core ready=" + ready);
                int before = ContainerMod.Before, after = ContainerMod.After, calls = TradeUtility.NativeLaunches;
                int rejects = Messages.Rejects;
                f.Pay(def, 12);
                Equal(18, held.stackCount, "native extended source deducts held goods with core ready=" + ready);
                Equal(40L, f.Unit.Inventory.GetTotalCount(def), "foreign-only payment never extracts our stock");
                Equal(calls + 1, TradeUtility.NativeLaunches, "original payment executes for foreign container");
                Equal(before + 1, ContainerMod.Before, "foreign prefix still executes");
                Equal(after + 1, ContainerMod.After, "foreign postfix still executes");
                Equal(rejects, Messages.Rejects, "no storage-network rejection affects foreign payment");
                if (ready)
                {
                    f.Pay(def, 25);
                    Equal(true, held.Destroyed, "native container source pays its remaining eighteen");
                    Equal(33L, f.Core.Network.GetTotalCount(def), "storage source supplies only the seven remaining");
                }
            }
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ContainerMod.Map = null; ContainerMod.Contents = null;
        }
    }
}
