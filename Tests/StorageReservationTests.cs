using System;
using System.Collections.Generic;
using MagicStorage;
using Verse;

internal static class StorageReservationTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Reservations: " + message + "; expected " + expected + ", got " + actual); }

    private sealed class Client : IStorageReservationClient
    {
        internal int Calls;
        internal StorageReservation Last;
        internal StorageReservationFailure Reason;
        public void OnReservationInvalidated(StorageReservation reservation, StorageReservationFailure reason)
        { Calls++; Last = reservation; Reason = reason; }
    }
    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Thing Endpoint = new Thing();
        internal readonly Thing Item = new Thing { stackCount = 10 };
        internal readonly Client Consumer = new Client();
        internal readonly object Token = new object();
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Register(Endpoint, -1);
            Manager.MapComponentTick(); Unit.Inventory.Contents.TryAdd(Item);
        }
        internal CompStorageNode Register(Thing thing, int x)
        {
            thing.Spawned = true; thing.Map = Map; thing.Position = new IntVec3(x, 0);
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageCore core) core.node = node;
            if (thing is Building_StorageUnit unit) unit.node = node;
            Manager.Register(node); return node;
        }
        internal void Reserve(Func<bool> usable = null)
        { Equal(4, Core.Network.ReserveOutgoing(Token, Item, 4, Endpoint, Consumer, usable), "acquire exact claim"); }
    }
    internal static int Run()
    {
        var f = new Fixture(); f.Reserve();
        f.Manager.NotifyNodeUnavailable(f.Unit);
        Equal(false, f.Core.Network.HasOutgoingReservation(f.Token, f.Item, 4), "node invalidation is synchronous");
        Equal(0, f.Consumer.Calls, "callback deferred during destruction");
        f.Manager.NotifyNodeUnavailable(f.Unit); f.Manager.MapComponentTick(); f.Manager.MapComponentTick();
        Equal(1, f.Consumer.Calls, "duplicate node events notify once");
        Equal(StorageReservationFailure.NodeUnavailable, f.Consumer.Reason, "node failure reason");
        Equal(f.Unit, f.Consumer.Last.Source, "reservation records source");

        f = new Fixture(); f.Reserve();
        f.Unit.Inventory.Contents.Remove(f.Item);
        Equal(false, f.Core.Network.HasOutgoingReservation(f.Token, f.Item, 4), "external removal invalidates immediately");
        Equal(0, f.Consumer.Calls, "inventory mutation does not invoke clients");
        f.Manager.MapComponentTick();
        Equal(StorageReservationFailure.ItemUnavailable, f.Consumer.Reason, "external removal reason");

        f = new Fixture(); f.Reserve();
        f.Item.stackCount = 3; f.Manager.MapComponentTick();
        Equal(StorageReservationFailure.InsufficientCount, f.Consumer.Reason, "unannounced count loss detected");
        Equal(1, f.Consumer.Calls, "count loss callback");

        f = new Fixture(); bool edible = true; f.Reserve(() => edible);
        edible = false;
        Equal(false, f.Core.Network.TryWithdrawReserved(f.Token, f.Item, 4, new ThingOwner(null), out _).Complete,
            "final collection rechecks purpose policy");
        Equal(10, f.Item.stackCount, "invalid collection preserves items");

        f = new Fixture(); f.Reserve();
        var other = new object();
        f.Core.Network.ReserveOutgoing(other, f.Item, 6);
        Equal(true, f.Core.Network.TryWithdrawReserved(f.Token, f.Item, 4, new ThingOwner(null), out _).Complete, "normal collection succeeds");
        f.Manager.MapComponentTick();
        Equal(0, f.Consumer.Calls, "normal collection does not notify invalidation");
        Equal(true, f.Core.Network.HasOutgoingReservation(other, f.Item, 6), "collection preserves other consumers' claims");
        f.Manager.NotifyNodeUnavailable(f.Unit); f.Manager.MapComponentTick();
        Equal(0, f.Consumer.Calls, "completed collection no longer depends on source");

        f = new Fixture(); f.Reserve(); f.Core.Network.ReleaseOutgoing(f.Token);
        f.Manager.NotifyNodeUnavailable(f.Unit); f.Manager.MapComponentTick();
        Equal(0, f.Consumer.Calls, "voluntary release is silent");

        f = new Fixture(); f.Reserve();
        f.Manager.MarkDirty(); f.Manager.MapComponentTick();
        Equal(6, f.Core.Network.AvailableToWithdraw(f.Item), "network replacement retains exact claims without job restoration");
        Equal(0, f.Consumer.Calls, "unchanged connectivity does not notify");
        f.Register(new Thing(), 50); f.Manager.MapComponentTick();
        Equal(6, f.Core.Network.AvailableToWithdraw(f.Item), "unrelated construction retains claim");

        f = new Fixture(); f.Reserve();
        f.Manager.NotifyNodeUnavailable(f.Endpoint); f.Manager.MapComponentTick();
        Equal(StorageReservationFailure.NodeUnavailable, f.Consumer.Reason, "outlet invalidation affects dependent claim");
        Equal(10L, f.Unit.Inventory.GetTotalCount(f.Item.def), "outlet failure preserves stored contents");

        f = new Fixture(); f.Reserve();
        f.Manager.Unregister(f.Unit.node); f.Manager.MapComponentTick();
        Equal(1, f.Consumer.Calls, "unit unregister uses common invalidation");
        Equal(StorageReservationFailure.NodeUnavailable, f.Consumer.Reason, "unregister reports node reason");

        f = new Fixture(); f.Reserve();
        f.Register(new Building_StorageCore(), 2); f.Manager.MapComponentTick();
        Equal(StorageReservationFailure.NetworkUnavailable, f.Consumer.Reason, "two cores invalidate affected reservations");

        f = new Fixture();
        var second = new Thing { stackCount = 5 }; f.Unit.Inventory.Contents.TryAdd(second);
        int failedGroups = 0;
        var lease = new CraftingMaterialLease(f.Core.Network, f.Endpoint, null, () => failedGroups++);
        Equal(true, lease.Acquire(new Dictionary<Thing, int> { { f.Item, 4 }, { second, 3 } }), "group reserves multiple stacks");
        f.Unit.Inventory.Contents.Remove(f.Item); f.Manager.MapComponentTick();
        Equal(1, failedGroups, "one invalid ingredient notifies group once");
        Equal(5, f.Core.Network.AvailableToWithdraw(second), "group failure releases all other ingredients");
        f.Manager.MapComponentTick();
        Equal(1, failedGroups, "released group never repeats notification");

        f = new Fixture(); f.Reserve();
        f.Manager.NotifyNodeUnavailable(f.Unit);
        f.Core.Network.ReleaseOutgoing(f.Token);
        f.Reserve(); f.Manager.MapComponentTick();
        Equal(0, f.Consumer.Calls, "released stale notification cannot cancel a replacement reservation");
        Equal(6, f.Core.Network.AvailableToWithdraw(f.Item), "replacement reservation remains held");

        f = new Fixture(); f.Manager.Unregister(f.Unit.node);
        f.Register(f.Unit, 3); f.Register(new Thing(), 1);
        var wire = f.Register(new Thing(), 2); f.Manager.MapComponentTick(); f.Reserve();
        f.Manager.Unregister(wire); f.Manager.MapComponentTick();
        Equal(StorageReservationFailure.NetworkUnavailable, f.Consumer.Reason, "conduit split invalidates unreachable source");
        Equal(10L, f.Unit.Inventory.GetTotalCount(f.Item.def), "disconnection does not move or destroy contents");
        return checks;
    }
}
