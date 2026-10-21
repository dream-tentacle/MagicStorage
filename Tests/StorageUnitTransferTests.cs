using System;
using MagicStorage;
using Verse;

internal static class StorageUnitTransferTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string label)
    { assertions++; if (!Equals(expected, actual)) throw new Exception("Unit transfer: " + label + "; expected " + expected + ", got " + actual); }

    private sealed class Client : IStorageReservationClient
    {
        internal int Calls;
        internal StorageReservationFailure Failure;
        public void OnReservationInvalidated(StorageReservation reservation, StorageReservationFailure reason)
        { Calls++; Failure = reason; }
    }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Source = new Building_StorageUnit();
        internal readonly ThingDef X = new ThingDef(), Y = new ThingDef();
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal StorageNetwork Network => Core.Network;
        private int nextPosition = 2;
        internal Fixture(int sourceSlots = 3)
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Source.SlotCapacity = sourceSlots;
            Register(Core, 0); Register(Source, 1);
        }
        private void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            thing.Position = new IntVec3(x, 0);
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageCore core) core.node = node;
            if (thing is Building_StorageUnit unit) unit.node = node;
            Manager.Register(node); Manager.EnsureCurrent();
        }
        internal Building_StorageUnit AddUnit(int slots)
        {
            var unit = new Building_StorageUnit { SlotCapacity = slots };
            Register(unit, nextPosition++); return unit;
        }
        internal Building_StorageUnit AddForeignUnit()
        {
            Register(new Building_StorageCore(), 50);
            var unit = new Building_StorageUnit(); Register(unit, 51); return unit;
        }
        internal Thing Store(Building_StorageUnit unit, ThingDef def, int count, string variant = null)
        {
            var item = new Thing { def = def, stackCount = count, variant = variant };
            Equal(true, unit.Inventory.Contents.TryAdd(item, false), "fixture stores real stack");
            return item;
        }
    }

    internal static int Run()
    {
        assertions = 0;
        MergeThenEmptySlots(); PartialAndFull(); CancelWithoutSpace(); RespectOtherReservations();
        IncomingPartialSpace(); CancelIncomingToSource(); FiltersAndVariants(); InvalidSources(); LargeCounts();
        return assertions;
    }

    private static void MergeThenEmptySlots()
    {
        var f = new Fixture();
        var first = f.AddUnit(1); var second = f.AddUnit(1); var third = f.AddUnit(1);
        Thing x = f.Store(f.Source, f.X, 50), y = f.Store(f.Source, f.Y, 30);
        Thing partial = f.Store(first, f.X, 60);
        Equal(80L, f.Network.TransferInventory(f.Source), "moves all source contents");
        Equal(0L, f.Source.Inventory.TotalCount, "source emptied");
        Equal(75, partial.stackCount, "compatible partial stack filled first");
        Equal(35L, second.Inventory.TotalCount, "remainder uses another unit's empty slot");
        Equal(true, second.Inventory.Contents.Contains(x), "remaining original stack preserves identity");
        Equal(true, third.Inventory.Contents.Contains(y), "unmerged stack preserves identity");
        Equal(110L, f.Network.GetTotalCount(f.X), "network X count conserved");
        Equal(30L, f.Network.GetTotalCount(f.Y), "network Y count conserved");
        Equal(0, f.Map.Ground.Count, "nothing dropped on the ground");
    }

    private static void PartialAndFull()
    {
        var f = new Fixture(); var target = f.AddUnit(1); var foreign = f.AddForeignUnit();
        f.Store(target, f.X, 60); f.Store(f.Source, f.X, 50); f.Store(f.Source, f.Y, 20);
        Equal(15L, f.Network.TransferInventory(f.Source), "uses only available compatible space");
        Equal(55L, f.Source.Inventory.TotalCount, "capacity overflow stays in source");
        Equal(0L, foreign.Inventory.TotalCount, "another network is never a destination");
        Equal(0L, f.Network.TransferInventory(f.Source), "full destinations are a safe no-op");
        Equal(55L, f.Source.Inventory.TotalCount, "repeat preserves remainder");
        Equal(110L, f.Network.GetTotalCount(f.X), "partial transfer keeps X index consistent");
        Equal(20L, f.Network.GetTotalCount(f.Y), "unmoved type remains indexed");
        Equal(0, f.Map.Ground.Count, "overflow is not dropped");
    }

    private static void CancelWithoutSpace()
    {
        var f = new Fixture(); Thing item = f.Store(f.Source, f.X, 40);
        f.Store(f.Source, f.X, 10);
        var owner = new object(); var client = new Client();
        Equal(20, f.Network.ReserveOutgoing(owner, item, 20, client: client), "source reservation acquired");
        Equal(0L, f.Network.TransferInventory(f.Source), "source is excluded even when its stacks could merge");
        Equal(2, f.Source.Inventory.UsedSlots, "no self-transfer or self-compaction");
        Equal(false, f.Network.HasOutgoingReservation(owner, item, 20), "source reservation cancelled even with no destination");
        Equal(40, f.Network.AvailableToWithdraw(item), "source reserved quantity released immediately");
        Equal(0, client.Calls, "client callback is deferred until mutation finishes");
        f.Manager.MapComponentTick(); f.Manager.MapComponentTick();
        Equal(1, client.Calls, "cancelled claim notifies its owner once");
        Equal(StorageReservationFailure.PolicyChanged, client.Failure, "manual transfer reports changed policy");
        Equal(50L, f.Source.Inventory.TotalCount, "cancel does not consume items");
    }

    private static void RespectOtherReservations()
    {
        var f = new Fixture(1); var target = f.AddUnit(2);
        Thing sourceItem = f.Store(f.Source, f.X, 50), targetItem = f.Store(target, f.X, 50);
        var sourceOwner = new object(); var targetOwner = new object(); var incomingOwner = new object();
        Equal(10, f.Network.ReserveOutgoing(sourceOwner, sourceItem, 10), "source outgoing claim");
        Equal(10, f.Network.ReserveOutgoing(targetOwner, targetItem, 10), "target outgoing claim");
        Equal(75, f.Network.ReserveIncoming(incomingOwner, new Thing { def = f.Y, stackCount = 75 }, 75), "target empty slot promised to another delivery");
        Equal(0L, f.Network.TransferInventory(f.Source), "target reserved stack and reserved slot are protected");
        Equal(false, f.Network.HasOutgoingReservation(sourceOwner, sourceItem, 10), "only source outgoing claim cancelled");
        Equal(true, f.Network.HasOutgoingReservation(targetOwner, targetItem, 10), "target outgoing claim preserved");
        Equal(75, f.Network.IncomingCountFor(incomingOwner), "unrelated incoming allocation preserved");
        f.Network.ReleaseIncoming(incomingOwner);
        Equal(50L, f.Network.TransferInventory(f.Source), "released empty slot becomes usable");
        Equal(50, targetItem.stackCount, "transfer does not merge into the reserved target stack");
        Equal(2, target.Inventory.UsedSlots, "new stack uses the free slot");
        Equal(true, f.Network.HasOutgoingReservation(targetOwner, targetItem, 10), "target claim survives successful transfer");
        Equal(100L, f.Network.GetTotalCount(f.X), "reserved transfer conserves network stock");
    }

    private static void IncomingPartialSpace()
    {
        var f = new Fixture(1); var target = f.AddUnit(1);
        f.Store(f.Source, f.X, 75); f.Store(target, f.X, 50);
        var owner = new object();
        Equal(20, f.Network.ReserveIncoming(owner, new Thing { def = f.X, stackCount = 20 }, 20), "reserve target partial-stack space");
        Equal(5L, f.Network.TransferInventory(f.Source), "only unreserved partial space is consumed");
        Equal(70L, f.Source.Inventory.TotalCount, "reserved-space overflow stays in source");
        Equal(55L, target.Inventory.TotalCount, "target keeps room for promised delivery");
        Equal(20, f.Network.IncomingCountFor(owner), "incoming claim remains intact");
    }

    private static void CancelIncomingToSource()
    {
        var f = new Fixture(1); var target = f.AddUnit(1);
        f.Store(f.Source, f.X, 50); f.Store(target, f.X, 50);
        var owner = new object();
        Equal(40, f.Network.ReserveIncoming(owner, new Thing { def = f.X, stackCount = 40 }, 40), "incoming plan spans source and destination");
        Equal(25L, f.Network.TransferInventory(f.Source), "source-linked incoming plan is released before relocation");
        Equal(0, f.Network.IncomingCountFor(owner), "entire affected plan is invalidated for replanning");
        Equal(25L, f.Source.Inventory.TotalCount, "unmoved source remainder preserved");
        Equal(100L, f.Network.GetTotalCount(f.X), "incoming cancellation conserves items");
    }

    private static void FiltersAndVariants()
    {
        var f = new Fixture(1); var target = f.AddUnit(1);
        Thing item = f.Store(f.Source, f.X, 20, "quality A");
        f.Store(target, f.X, 60, "quality B");
        f.Core.Settings.allow = false;
        Equal(0L, f.Network.TransferInventory(f.Source), "incompatible stack state never merges");
        var empty = f.AddUnit(1);
        Equal(20L, f.Network.TransferInventory(f.Source), "existing stock ignores the incoming core filter");
        Equal(true, empty.Inventory.Contents.Contains(item), "quality and object identity are preserved");
        Equal(80L, f.Network.GetTotalCount(f.X), "stateful transfer conserves counts");
    }

    private static void InvalidSources()
    {
        var f = new Fixture(1); f.AddUnit(1); Thing item = f.Store(f.Source, f.X, 10);
        var owner = new object(); f.Network.ReserveOutgoing(owner, item, 4);
        f.Network.Invalidate();
        Equal(0L, f.Network.TransferInventory(f.Source), "unavailable network refuses relocation");
        f.Network.FinishRebuild();
        Equal(true, f.Network.HasOutgoingReservation(owner, item, 4), "refused operation preserves reservations");
        f.Source.Spawned = false;
        Equal(0L, f.Network.TransferInventory(f.Source), "unspawned source refused");
        f.Source.Spawned = true;
        Equal(0L, f.Network.TransferInventory(f.AddForeignUnit()), "foreign source refused");
        Equal(0L, f.Network.TransferInventory(null), "null source refused");
        Equal(10L, f.Source.Inventory.TotalCount, "invalid calls preserve contents");
    }

    private static void LargeCounts()
    {
        var f = new Fixture(2); var target = f.AddUnit(2); f.X.stackLimit = int.MaxValue;
        f.Store(f.Source, f.X, int.MaxValue); f.Store(f.Source, f.X, int.MaxValue);
        long total = (long)int.MaxValue * 2;
        Equal(total, f.Network.TransferInventory(f.Source), "transferred totals use 64-bit counts");
        Equal(total, target.Inventory.TotalCount, "destination total does not overflow");
        Equal(total, f.Network.GetTotalCount(f.X), "network total does not overflow");
        Equal(0L, f.Source.Inventory.TotalCount, "large source emptied");
    }
}
