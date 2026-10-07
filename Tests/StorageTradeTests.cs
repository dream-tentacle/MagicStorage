using System;
using System.Collections.Generic;
using MagicStorage;
using Verse;

internal static class StorageTradeTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Trade: " + message + "; expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Manager.MapComponentTick();
        }
        internal void Register(Thing thing, int x)
        {
            thing.Spawned = true; thing.Map = Map; thing.Position = new IntVec3(x, 0);
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageCore core) core.node = node;
            if (thing is Building_StorageUnit unit) unit.node = node;
            Manager.Register(node);
        }
        internal Thing Stock(int count, ThingDef def = null)
        {
            var item = new Thing { def = def ?? new ThingDef(), stackCount = count };
            Unit.Inventory.Contents.TryAdd(item, false); return item;
        }
        internal StorageTradeRequest Request(object line, Thing item, int count) =>
            new StorageTradeRequest { Line = line, Network = Core.Network, Item = item, Count = count };
    }

    internal static int Run()
    {
        checks = 0;
        ReservedStacks(); FailedPayment(); NodeFailure(); DuplicateRequests(); MultiNetwork(); CollectionFailure();
        return checks;
    }

    private static void ReservedStacks()
    {
        var f = new Fixture(); var def = new ThingDef(); var first = f.Stock(75, def); var second = f.Stock(75, def);
        var other = new object(); f.Core.Network.ReserveOutgoing(other, first, 70);
        var line = new object();
        using (var tx = new StorageTradeTransaction(new List<StorageTradeRequest> {
            f.Request(line, first, 5), f.Request(line, second, 35) }, () => true))
        {
            Equal(true, tx.Reserve(), "reserve forty without taking another consumer's seventy");
            Equal(5, tx.Available(f.Core.Network, first), "own claim still counts in displayed availability");
            Equal(0, f.Core.Network.AvailableToWithdraw(first), "other consumers cannot take sale claim");
            var output = new ThingOwner(null); var received = new List<Thing>();
            Equal(true, tx.Collect(line, output, received), "collect exact planned stacks");
            Equal(2, received.Count, "two genuine split objects");
            Equal(5, received[0].stackCount, "only five taken from mostly reserved stack");
            Equal(35, received[1].stackCount, "remaining thirty five taken from free stack");
            Equal(70, first.stackCount, "other consumer's material remains");
            Equal(40, second.stackCount, "second remainder");
            Equal(110L, f.Core.Network.GetTotalCount(def), "partial withdrawal refreshes network index");
            Equal(true, f.Core.Network.HasOutgoingReservation(other, first, 70), "unrelated claim remains valid");
        }
    }

    private static void FailedPayment()
    {
        var f = new Fixture(); var goods = f.Stock(20); var silver = f.Stock(3);
        using (var tx = new StorageTradeTransaction(new List<StorageTradeRequest> {
            f.Request(new object(), goods, 5), f.Request(new object(), silver, 5) }, () => true))
        {
            Equal(false, tx.Reserve(), "insufficient payment rejects whole transaction");
            Equal(20, f.Core.Network.AvailableToWithdraw(goods), "earlier goods claim released on later failure");
            Equal(3, f.Core.Network.AvailableToWithdraw(silver), "partial payment claim released");
            Equal(20, goods.stackCount, "preflight leaves goods stored");
            Equal(3, silver.stackCount, "preflight leaves silver stored");
        }
        bool usable = true;
        using (var tx = new StorageTradeTransaction(new List<StorageTradeRequest> { f.Request(new object(), goods, 4) }, () => usable))
        {
            Equal(true, tx.Reserve(), "initial policy permits trade"); usable = false;
            Equal(false, tx.Valid(), "trader or negotiation policy change invalidates before collection");
        }
        Equal(20, f.Core.Network.AvailableToWithdraw(goods), "cancel releases sale claims");
        bool permitted = true;
        var request = f.Request(new object(), goods, 4);
        request.Usable = () => permitted;
        using (var tx = new StorageTradeTransaction(new List<StorageTradeRequest> { request }, () => true))
        {
            Equal(true, tx.Reserve(), "stack-specific use policy permits initial reservation");
            permitted = false;
            Equal(false, tx.Valid(), "stack-specific policy change blocks collection");
            f.Manager.MapComponentTick();
            Equal(false, f.Core.Network.HasOutgoingReservation(request, goods, 4), "shared interface invalidates changed purpose");
        }
        Equal(20, f.Core.Network.AvailableToWithdraw(goods), "changed purpose releases all trade claims");
    }

    private static void NodeFailure()
    {
        var f = new Fixture(); var item = f.Stock(10); var line = new object();
        using (var tx = new StorageTradeTransaction(new List<StorageTradeRequest> { f.Request(line, item, 4) }, () => true))
        {
            Equal(true, tx.Reserve(), "acquire before source destruction");
            f.Manager.NotifyNodeUnavailable(f.Unit);
            Equal(false, tx.Valid(), "node destruction invalidates synchronously");
            Equal(false, tx.Collect(line, new ThingOwner(null), new List<Thing>()), "invalid claim never delivers");
            Equal(10, item.stackCount, "node failure does not deduct inventory");
        }
        f = new Fixture(); item = f.Stock(10);
        var incoming = new Thing { def = item.def, stackCount = 2 };
        f.Core.Network.ReserveIncoming(new object(), incoming, 2);
        using (var tx = new StorageTradeTransaction(new List<StorageTradeRequest> { f.Request(line, item, 1) }, () => true))
            Equal(false, tx.Reserve(), "incoming merge reservation excludes its stack from sale");
    }

    private static void DuplicateRequests()
    {
        var f = new Fixture(); var item = f.Stock(10);
        using (var tx = new StorageTradeTransaction(new List<StorageTradeRequest> {
            f.Request(new object(), item, 4), f.Request(new object(), item, 4) }, () => true))
        {
            Equal(false, tx.Reserve(), "duplicate real stack rejected rather than double counted");
            Equal(10, f.Core.Network.AvailableToWithdraw(item), "duplicate failure releases all claims");
        }
    }

    private static void MultiNetwork()
    {
        var f = new Fixture(); var secondCore = new Building_StorageCore(); var secondUnit = new Building_StorageUnit();
        f.Register(secondCore, 50); f.Register(secondUnit, 51); f.Manager.MapComponentTick();
        var def = new ThingDef(); var first = f.Stock(8, def); var second = new Thing { def = def, stackCount = 9 };
        secondUnit.Inventory.Contents.TryAdd(second); var line = new object();
        using (var tx = new StorageTradeTransaction(new List<StorageTradeRequest> {
            f.Request(line, first, 3), new StorageTradeRequest { Line = line, Network = secondCore.Network, Item = second, Count = 4 } }, () => true))
        {
            Equal(true, tx.Reserve(), "one line spans two independent networks");
            var output = new ThingOwner(null); var received = new List<Thing>();
            Equal(true, tx.Collect(line, output, received), "collect both networks");
            Equal(f.Core.Network, tx.SourceFor(received[0]), "first split records exact source");
            Equal(secondCore.Network, tx.SourceFor(received[1]), "identical second split records its own source");
            Equal(5L, f.Core.Network.GetTotalCount(def), "first network index");
            Equal(5L, secondCore.Network.GetTotalCount(def), "second network index");
        }
    }

    private sealed class RejectSecond : ThingOwner
    {
        internal RejectSecond() : base(null) { }
        public override bool TryAdd(Thing item, bool merge = true) => Count == 0 && base.TryAdd(item, merge);
    }

    private static void CollectionFailure()
    {
        var f = new Fixture(); var first = f.Stock(10); var second = f.Stock(10); var line = new object();
        using (var tx = new StorageTradeTransaction(new List<StorageTradeRequest> {
            f.Request(line, first, 3), f.Request(line, second, 4) }, () => true))
        {
            Equal(true, tx.Reserve(), "reserve before destination failure");
            var output = new RejectSecond(); var received = new List<Thing>();
            Equal(false, tx.Collect(line, output, received), "rejecting destination interrupts collection");
            Equal(1, received.Count, "already collected piece remains owned for rollback");
            Equal(f.Core.Network, tx.SourceFor(received[0]), "rollback has exact source");
            Equal(true, tx.SourceFor(received[0]).TryStore(output, received[0], 3).Complete, "uncommitted piece can return to network");
            Equal(10, first.stackCount, "first item restored");
            Equal(10, second.stackCount, "failed second transfer restores source");
        }
        Equal(10, f.Core.Network.AvailableToWithdraw(second), "failed transaction releases pending claim");
    }
}
