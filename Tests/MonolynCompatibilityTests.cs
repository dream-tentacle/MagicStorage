using System;
using System.Collections.Generic;
using HarmonyLib;
using MagicStorage;
using RimWorld;
using Verse;

internal static class MonolynCompatibilityTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string message)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(message + ": " + expected + " != " + actual); }

    private sealed class Client : IStorageReservationClient
    {
        internal int Calls;
        internal StorageReservationFailure Reason;
        public void OnReservationInvalidated(StorageReservation reservation, StorageReservationFailure reason)
        { Calls++; Reason = reason; }
    }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly ThingDef Steel = new ThingDef(), Component = new ThingDef();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit First = new Building_StorageUnit(), Second = new Building_StorageUnit();
        internal readonly ASEL.PlaceWorker_LightBuilder Worker = new ASEL.PlaceWorker_LightBuilder();
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Map.ResourceDefs.Add(Steel); Map.ResourceDefs.Add(Component);
            Register(Core, 0); Register(First, 1); Register(Second, 2); Manager.FinalizeInit();
        }
        private void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageUnit unit) unit.node = node;
            if (thing is Building_StorageCore core) core.node = node;
            Manager.Register(node);
        }
        internal Thing Store(Building_StorageUnit unit, ThingDef def, int count)
        {
            var item = new Thing { def = def, stackCount = count };
            if (!unit.Inventory.Contents.TryAdd(item, false)) throw new Exception("fixture insertion failed");
            return item;
        }
        internal bool Consume(int steel, int components = 0)
        {
            var def = new ThingDef { costList = new List<ThingDefCountClass> {
                new ThingDefCountClass { thingDef = Steel, count = steel },
                new ThingDefCountClass { thingDef = Component, count = components } } };
            return Worker.DestroyResources(Map, def);
        }
    }

    internal static int Run()
    {
        new MonolynCompatibility().Apply(new Harmony("magicstorage.tests.monolyn"));
        ContainerOnly(); MixedAndMultipleCosts(); MultipleUnits(); IneligibleUnits(); DisconnectedAndReserved();
        WholeReservedStack(); PreservedReservation(); CraftingGroupInvalidated(); IncomingMergeReservation(); Insufficient();
        Equal(0, ASEL.PlaceWorker_LightBuilder.OriginalCalls, "prefix replaces original deduction without double charging");
        Equal(Effecter.Triggers, Effecter.Cleanups, "transfer effects cleaned up");
        return assertions;
    }

    private static void ContainerOnly()
    {
        var f = new Fixture(); Thing stack = f.Store(f.First, f.Steel, 20);
        Equal(true, f.Consume(15), "container-only deduction succeeds through Harmony");
        Equal(5, stack.stackCount, "partial stack consumed");
        Equal(5L, f.First.Inventory.GetTotalCount(f.Steel), "unit index refreshed after partial split");
        Equal(5L, f.First.Network.GetTotalCount(f.Steel), "network index refreshed");
        Equal(5, f.Map.resourceCounter.GetCount(f.Steel), "resource counter refreshed");
        Equal(f.First.Position, Effecter.LastTarget.Cell, "container effect uses building position");
        Equal(true, f.Consume(5), "whole stack consumed");
        Equal(true, stack.Destroyed, "whole stack destroyed");
        Equal(0, f.First.Inventory.UsedSlots, "empty slot released");
    }

    private static void MixedAndMultipleCosts()
    {
        var f = new Fixture();
        var ground = new Thing { def = f.Steel, stackCount = 10, Map = f.Map, Spawned = true };
        f.Map.listerThings.Things.Add(ground);
        f.Store(f.First, f.Steel, 20); f.Store(f.Second, f.Component, 8);
        Equal(true, f.Consume(25, 3), "mixed source and multiple material deduction");
        Equal(true, ground.Destroyed, "ground consumed first");
        Equal(5L, f.First.Inventory.GetTotalCount(f.Steel), "only ground shortfall consumed from storage");
        Equal(5L, f.Second.Inventory.GetTotalCount(f.Component), "second material consumed exactly");
    }

    private static void MultipleUnits()
    {
        var f = new Fixture(); f.Store(f.First, f.Steel, 6); f.Store(f.Second, f.Steel, 9);
        Equal(true, f.Consume(13), "material spans storage units");
        Equal(1, f.Map.resourceCounter.Updates, "multiple deductions refresh global resource counts once");
        Equal(2L, f.First.Inventory.GetTotalCount(f.Steel) + f.Second.Inventory.GetTotalCount(f.Steel), "combined remainder exact");
    }

    private static void IneligibleUnits()
    {
        var f = new Fixture(); f.Store(f.First, f.Steel, 7); f.Store(f.Second, f.Steel, 9);
        f.First.Faction = new Faction(); f.Second.Spawned = false;
        Equal(false, f.Consume(5), "foreign and unspawned units excluded");
        Equal(7L, f.First.Inventory.GetTotalCount(f.Steel), "foreign stock unchanged");
        Equal(9L, f.Second.Inventory.GetTotalCount(f.Steel), "unspawned stock unchanged");
    }

    private static void DisconnectedAndReserved()
    {
        var f = new Fixture(); Thing item = f.Store(f.First, f.Steel, 20);
        object claim = new object();
        var client = new Client();
        Equal(15, f.Core.Network.ReserveOutgoing(claim, item, 15, client: client), "fixture reserves outgoing stock");
        Equal(true, f.Consume(10), "immediate consumption follows physical resource-count semantics");
        Equal(false, f.Core.Network.HasOutgoingReservation(claim, item, 15), "overdrawn reservation invalidated");
        Equal(0, client.Calls, "notification deferred until inventory mutation completes");
        f.Manager.MapComponentTick(); f.Manager.MapComponentTick();
        Equal(1, client.Calls, "affected reservation owner notified exactly once");
        Equal(StorageReservationFailure.InsufficientCount, client.Reason, "owner receives count loss reason");
        f.Manager.Unregister(f.Core.node); f.Manager.EnsureCurrent();
        Equal(false, f.First.Network.CanWork, "fixture network disconnected");
        Equal(true, f.Consume(5), "counted disconnected stock can still be consumed");
        Equal(5L, f.First.Inventory.GetTotalCount(f.Steel), "disconnected deduction updates physical inventory");
    }

    private static void WholeReservedStack()
    {
        var f = new Fixture(); Thing item = f.Store(f.First, f.Steel, 10);
        var client = new Client(); object claim = new object();
        f.Core.Network.ReserveOutgoing(claim, item, 5, client: client);
        Equal(true, f.Consume(10), "whole reserved stack consumed");
        f.Manager.MapComponentTick();
        Equal(1, client.Calls, "whole stack owner notified");
        Equal(StorageReservationFailure.ItemUnavailable, client.Reason, "whole stack reports removed item");
    }

    private static void IncomingMergeReservation()
    {
        var f = new Fixture(); f.Store(f.First, f.Steel, 60);
        var incoming = new Thing { def = f.Steel, stackCount = 15 };
        var carry = new ThingOwner(null); carry.TryAdd(incoming, false);
        object claim = new object();
        Equal(15, f.Core.Network.ReserveIncoming(claim, incoming, 15), "incoming merge reserved");
        Equal(true, f.Consume(60), "incoming merge target consumed");
        Equal(15, f.Core.Network.TryStoreReserved(carry, incoming, 15, claim).Transferred,
            "incoming reservation replans into freed space without losing carried material");
        Equal(15L, f.First.Network.GetTotalCount(f.Steel), "incoming material stored once after replan");
    }

    private static void PreservedReservation()
    {
        var f = new Fixture(); Thing item = f.Store(f.First, f.Steel, 20);
        var client = new Client(); object claim = new object();
        f.Core.Network.ReserveOutgoing(claim, item, 5, client: client);
        Equal(true, f.Consume(10), "consume quantity outside reservation");
        f.Manager.MapComponentTick();
        Equal(true, f.Core.Network.HasOutgoingReservation(claim, item, 5), "remaining quantity still satisfies reservation");
        Equal(0, client.Calls, "unaffected owner continues normally");
    }

    private static void CraftingGroupInvalidated()
    {
        var f = new Fixture(); Thing steel = f.Store(f.First, f.Steel, 20);
        Thing other = f.Store(f.Second, f.Component, 10); int canceled = 0;
        var lease = new CraftingMaterialLease(f.Core.Network, f.Core, null, () => canceled++);
        Equal(true, lease.Acquire(new Dictionary<Thing, int> { { steel, 10 }, { other, 3 } }), "crafting group reserved");
        Equal(true, f.Consume(20), "reserved ingredient consumed by Monolyn");
        f.Manager.MapComponentTick(); f.Manager.MapComponentTick();
        Equal(1, canceled, "real crafting lease receives one cancellation");
        Equal(10, f.Core.Network.AvailableToWithdraw(other), "canceled crafting group releases other ingredients");
    }

    private static void Insufficient()
    {
        var f = new Fixture(); f.Store(f.First, f.Steel, 4);
        Equal(false, f.Consume(6), "insufficient deduction returns false like original");
        Equal(0L, f.First.Inventory.GetTotalCount(f.Steel), "original partial-consumption behavior retained");
        Equal(false, f.Consume(1), "empty sources return false");
    }
}
