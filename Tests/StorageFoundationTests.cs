using System;
using MagicStorage;
using Verse;

internal static class StorageFoundationTests
{
    private static int assertions;
    private static readonly ThingDef X = new ThingDef();
    private static readonly ThingDef Y = new ThingDef();
    private static void Equal<T>(T expected, T actual, string message)
    {
        assertions++;
        if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual);
    }
    private static Thing Item(ThingDef def, int count, string variant = null) => new Thing { def = def, stackCount = count, variant = variant };
    private static CompStorageNode Register(Map map, Thing thing, int x, int z = 0)
    {
        thing.Map = map; thing.Position = new IntVec3(x,z); thing.Spawned = true;
        var node = new CompStorageNode { parent = thing };
        if (thing is Building_StorageUnit unit) unit.node = node;
        if (thing is Building_StorageCore core) core.node = node;
        map.GetComponent<MapComponent_StorageNetworks>().Register(node);
        return node;
    }
    private static Map NewMap()
    { var map = new Map(); map.component = new MapComponent_StorageNetworks(map); return map; }
    private static void Tick(Map map) => map.GetComponent<MapComponent_StorageNetworks>().MapComponentTick();
    private static StorageTransferResult Deposit(StorageNetwork net, Thing item, int count)
    { var carry = new ThingOwner(null); carry.TryAdd(item, false); return net.TryStore(carry, item, count); }

    public static int Main()
    {
        try
        {
            TenUnitCounts(); CapacityAndRollback(); Topology();
            assertions += CosmicCraftingTests.Run();
            assertions += ConstructionSupplyTests.Run();
            assertions += StorageReservationTests.Run();
            assertions += StorageTradeTests.Run();
            IncomingSlotCompetition(); IncomingMergeCompetition(); IncomingExactAllocation(); IncomingLifecycle();
            assertions += StorageReceiverTests.Run();
            assertions += StorageSupplyShelfTests.Run();
            assertions += StorageResourceCounterTests.Run();
            assertions += StorageWithdrawalTests.Run();
            Console.WriteLine("PASS: " + assertions + " assertions (production storage, receiver, supply shelf, crafting and construction supply logic; game boundaries stubbed).");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void TenUnitCounts()
    {
        var map = NewMap(); var core = Register(map, new Building_StorageCore(), 0);
        var units = new Building_StorageUnit[10];
        int[] amounts = {20,0,35,0,10,0,0,15,0,0};
        for (int i=0;i<10;i++)
        {
            units[i] = new Building_StorageUnit(); Register(map,units[i],i+1);
            if (amounts[i]>0) units[i].Inventory.Contents.TryAdd(Item(X,amounts[i]));
        }
        Tick(map); var network = core.Network;
        Equal(80L, network.GetTotalCount(X), "ten-unit total");
        Equal(0L, network.GetTotalCount(Y), "absent type");
        units[1].Inventory.Contents.TryAdd(Item(X,12));
        Equal(92L,network.GetTotalCount(X),"stored addition");
        var output = new ThingOwner(null);
        Equal(5,network.TryTransferTo(units[2].Inventory.Contents[0],5,output).Transferred,"partial withdrawal");
        Equal(87L,network.GetTotalCount(X),"partial withdrawal refresh");
        // Remove unit 1, while a parallel wire keeps the remaining nine connected.
        for(int x=0;x<3;x++) Register(map,new Thing(),x,1);
        Tick(map);
        map.GetComponent<MapComponent_StorageNetworks>().Unregister(units[0].node);
        Equal(false,network.CanWork,"old network invalidated immediately");
        Tick(map);
        Equal(67L,core.Network.GetTotalCount(X),"disconnected unit subtracted");
        Equal(20L,units[0].Inventory.GetTotalCount(X),"disconnected contents preserved");
        map.GetComponent<MapComponent_StorageNetworks>().Register(units[0].node); Tick(map);
        Equal(87L,core.Network.GetTotalCount(X),"reconnection restores access only");
    }

    private sealed class RejectingOwner : ThingOwner
    { public RejectingOwner():base(null){} public override bool TryAdd(Thing item,bool merge=true) => false; }

    private static void CapacityAndRollback()
    {
        var map=NewMap(); var core=Register(map,new Building_StorageCore(),0);
        var unit=new Building_StorageUnit { SlotCapacity=1 }; Register(map,unit,1); Tick(map);
        Equal(50,Deposit(core.Network,Item(X,50),50).Transferred,"first stack");
        Equal(25,Deposit(core.Network,Item(X,30),30).Transferred,"full slots still allow partial merge");
        Equal(75L,core.Network.GetTotalCount(X),"normal stack limit");
        Equal(1,unit.Inventory.UsedSlots,"one actual stack");
        Equal(0,Deposit(core.Network,Item(Y,1),1).Transferred,"full slot blocks other type");
        Equal(0,core.Network.TryTransferTo(unit.Inventory.Contents[0],10,new RejectingOwner()).Transferred,"rejected destination");
        Equal(75L,core.Network.GetTotalCount(X),"failed split rolled back");
        var output=new ThingOwner(null); output.TryAdd(Item(X,70));
        Equal(10,core.Network.TryTransferTo(unit.Inventory.Contents[0],10,output).Transferred,"withdrawal merges and adds remainder");
        Equal(65L,core.Network.GetTotalCount(X),"withdrawal count after destination merge");
        Equal(0,Deposit(core.Network,Item(X,1,"different"),1).Transferred,"incompatible state cannot share occupied slot");
        var existing=unit.Inventory.Contents[0];
        Equal(0,core.Network.TryTransferTo(existing,65,new RejectingOwner()).Transferred,"whole-stack rollback");
        Equal(true,unit.Inventory.Contents.Contains(existing),"whole-stack identity retained");
        ((Building_StorageCore)core.parent).Settings.allow=false;
        Equal(StorageFailure.FilterRejected,Deposit(core.Network,Item(X,1),1).Failure,"filter rejects new input");
        Equal(65L,core.Network.GetTotalCount(X),"filter does not eject existing inventory");
    }

    private static void Topology()
    {
        var map=NewMap(); var core=Register(map,new Building_StorageCore { width=2,height=2 },0);
        var edge=Register(map,new Building_StorageUnit(),2,1);
        var diagonal=Register(map,new Building_StorageUnit(),3,2);
        Tick(map);
        Equal(core.Network,edge.Network,"whole footprint conducts");
        Equal(StorageNetworkStatus.NoCore,diagonal.Network.Status,"diagonal does not connect");
        var bridge=Register(map,new Thing(),2,2); Tick(map);
        Equal(core.Network,diagonal.Network,"wire connects separated unit");
        var second=Register(map,new Building_StorageCore(),3,3); Tick(map);
        Equal(StorageNetworkStatus.MultipleCores,core.Network.Status,"two cores stop network");
        Equal(StorageFailure.NetworkUnavailable,Deposit(core.Network,Item(X,1),1).Failure,"conflict blocks mutation");
        map.GetComponent<MapComponent_StorageNetworks>().Unregister(second); Tick(map);
        Equal(true,core.Network.CanWork,"removing extra core recovers");
        var old=core.Network;
        map.GetComponent<MapComponent_StorageNetworks>().Unregister(bridge);
        Equal(false,old.CanWork,"no mutation during topology changes"); Tick(map);
        Equal(StorageNetworkStatus.NoCore,diagonal.Network.Status,"wire removal splits graph");
        var foreign=Register(map,new Building_StorageCore { Faction=new Faction() },3,1); Tick(map);
        Equal(true,core.Network.CanWork,"foreign faction does not join");
        Equal(false,ReferenceEquals(core.Network,foreign.Network),"networks remain separate");
    }

    private static void IncomingSlotCompetition()
    {
        var map = NewMap(); var core = Register(map, new Building_StorageCore(), 0);
        var unit = new Building_StorageUnit { SlotCapacity = 1 }; Register(map, unit, 1); Tick(map);
        var net = core.Network; var first = new object(); var second = new object();
        var x = Item(X, 50); var y = Item(Y, 30);
        Equal(50, net.ReserveIncoming(first, x, 50), "reserve first empty slot");
        Equal(0L, net.GetTotalCount(X), "reservation is not inventory");
        Equal(0, net.GetCountCanAccept(y), "other item sees reserved slot");
        Equal(0, net.ReserveIncoming(second, y, 30), "two types cannot reserve same slot");
        Equal(0, Deposit(net, y, 30).Transferred, "unreserved writes also respect incoming capacity");
        net.ReleaseIncoming(first);
        Equal(30, net.ReserveIncoming(second, y, 30), "cancellation releases slot");
        var source = new ThingOwner(null);
        y = Item(Y, 30); source.TryAdd(y, false);
        Equal(30, net.TryStoreReserved(source, y, 30, second).Transferred, "reserved delivery");
        Equal(0, net.IncomingCountFor(second), "delivery releases reservation");
        Equal(30L, net.GetTotalCount(Y), "delivery becomes inventory");
    }

    private static void IncomingMergeCompetition()
    {
        var map = NewMap(); var core = Register(map, new Building_StorageCore(), 0);
        var unit = new Building_StorageUnit { SlotCapacity = 1 }; Register(map, unit, 1);
        unit.Inventory.Contents.TryAdd(Item(X, 50)); Tick(map);
        var net = core.Network; var a = new object(); var b = new object();
        var ca = new ThingOwner(null); var cb = new ThingOwner(null);
        var xa = Item(X, 15); var xb = Item(X, 10); ca.TryAdd(xa); cb.TryAdd(xb);
        Equal(15, net.ReserveIncoming(a, xa, 15), "reserve partial stack for first hauler");
        Equal(10, net.ReserveIncoming(b, xb, 10), "reserve remaining partial space");
        Equal(0, net.GetCountCanAccept(Item(X, 1)), "partial stack fully claimed");
        Equal(StorageFailure.Reserved, net.TryTransferTo(unit.Inventory.Contents[0], 1, new ThingOwner(null)).Failure,
            "withdrawal cannot invalidate incoming stack claim");
        Equal(10, net.TryStoreReserved(cb, xb, 10, b).Transferred, "second hauler arrives first");
        Equal(15, net.IncomingCountFor(a), "other claim retained after delivery");
        Equal(15, net.TryStoreReserved(ca, xa, 15, a).Transferred, "first hauler still fits");
        Equal(75L, net.GetTotalCount(X), "out-of-order deliveries preserve total");
        Equal(1, unit.Inventory.UsedSlots, "out-of-order merge uses one slot");
    }

    private static void IncomingExactAllocation()
    {
        var map = NewMap(); var core = Register(map, new Building_StorageCore(), 0);
        var unit = new Building_StorageUnit { SlotCapacity = 2 }; Register(map, unit, 1);
        var original = Item(X, 50); unit.Inventory.Contents.TryAdd(original); Tick(map);
        var net = core.Network; var a = new object(); var b = new object();
        var ca = new ThingOwner(null); var cb = new ThingOwner(null);
        var xa = Item(X, 25); var xb = Item(X, 75); ca.TryAdd(xa); cb.TryAdd(xb);
        net.ReserveIncoming(a, xa, 25); net.ReserveIncoming(b, xb, 75);
        Equal(75, net.TryStoreReserved(cb, xb, 75, b).Transferred, "new-slot claimant arrives before merge claimant");
        Equal(50, original.stackCount, "delivery does not steal claimed merge space");
        Equal(0, net.GetCountCanAccept(Item(X, 1)), "third hauler cannot steal promised space");
        Equal(25, net.TryStoreReserved(ca, xa, 25, a).Transferred, "original merge claim remains fulfillable");
        Equal(150L, net.GetTotalCount(X), "two full stacks after both deliveries");
        Equal(2, unit.Inventory.UsedSlots, "capacity never exceeded");
    }

    private static void IncomingLifecycle()
    {
        var map = NewMap(); var core = Register(map, new Building_StorageCore(), 0);
        var unit = new Building_StorageUnit { SlotCapacity = 3 }; Register(map, unit, 1); Tick(map);
        var net = core.Network; var owner = new object(); var item = Item(X, 200);
        var source = new ThingOwner(null); source.TryAdd(item, false);
        Equal(200, net.ReserveIncoming(owner, item, 200), "multi-slot reservation");
        Equal(0, net.GetCountCanAccept(Item(Y, 1)), "multi-slot reservation consumes three slots");
        ((Building_StorageCore)core.parent).Settings.allow = false;
        Equal(StorageFailure.FilterRejected, net.TryStoreReserved(source, item, 200, owner).Failure, "filter changed during hauling");
        Equal(200, item.stackCount, "rejected delivery remains in carrier");
        Equal(0, net.IncomingCountFor(owner), "rejected delivery releases capacity");
        ((Building_StorageCore)core.parent).Settings.allow = true;
        net.ReserveIncoming(owner, item, 200);
        var secondCore = Register(map, new Building_StorageCore(), 2);
        Equal(0, net.IncomingCountFor(owner), "topology invalidation clears stale claims");
        Tick(map);
        Equal(0, core.Network.ReserveIncoming(owner, item, 200), "two-core conflict refuses reservation");
        map.GetComponent<MapComponent_StorageNetworks>().Unregister(secondCore); Tick(map);
        net = core.Network;
        Equal(200, net.ReserveIncoming(owner, item, 200), "active job can rebuild its reservation after reconnect");
        Equal(200, net.TryStoreReserved(source, item, 200, owner).Transferred, "large delivery split into normal stacks");
        Equal(3, unit.Inventory.UsedSlots, "three normal stack slots used");
        Equal(200L, net.GetTotalCount(X), "large delivery quantity conserved");
        var stateful = Item(X, 1, "different");
        Equal(0, net.ReserveIncoming(new object(), stateful, 1), "same Def with different state cannot use remaining partial space");
        Equal(25, net.GetCountCanAccept(Item(X, 30)), "remaining unreserved compatible space is usable");
    }

}
