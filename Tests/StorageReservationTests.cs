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
        checks = 0;
        IncomingIndexes(); IncomingIndexMutations(); ScopedOutgoingValidation(); ConflictingCountLoss();
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
        f.Item.stackCount = 3; Find.TickManager.TicksGame += 60; f.Manager.MapComponentTick();
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

    private static void IncomingIndexes()
    {
        var f = new Fixture(); f.Unit.SlotCapacity = 3;
        var incoming = new StorageIncomingReservations(new[] { f.Unit });
        object a = new object(), b = new object();
        var item = new Thing { def = f.Item.def, stackCount = 100 };
        Equal(20, incoming.Reserve(a, item, 20).Count, "first merge claim");
        Equal(45, incoming.Reserve(b, item, 45).Count, "second merge claim");
        Equal(65L, incoming.ReservedOnStack(f.Item), "shared merge space is summed");
        Equal(true, incoming.IsStackReserved(f.Item), "indexed stack lookup sees either owner");
        incoming.Release(a); incoming.Release(a);
        Equal(45L, incoming.ReservedOnStack(f.Item), "repeated release cannot remove another owner's contribution");
        Equal(50, incoming.Reserve(b, item, 50).Count, "amend excludes own previous allocation");
        Equal(50L, incoming.ReservedOnStack(f.Item), "amend replaces rather than adds quantities");
        Equal(30, incoming.Reserve(a, item, 30).Count, "one plan can claim merge space and a new slot");
        Equal(65L, incoming.ReservedOnStack(f.Item), "both owners fill existing space");
        Equal(1, incoming.ReservedSlots(f.Unit), "new slot counted once regardless of its item quantity");
        Equal(5, incoming.Reserve(b, item, 5).Count, "smaller amendment releases unused space");
        Equal(20L, incoming.ReservedOnStack(f.Item), "other owner's merge allocation retained");
        var other = new Thing { stackCount = 100 };
        Equal(75, incoming.Reserve(b, other, 100).Count, "different type amendment respects other's slot claim");
        Equal(15L, incoming.ReservedOnStack(f.Item), "moving claim to another type releases old merge contribution");
        Equal(2, incoming.ReservedSlots(f.Unit), "both owners now reserve one slot");
        item.Destroyed = true;
        Equal(0, incoming.Reserve(a, item, 10).Count, "failed replacement releases previous claim");
        Equal(false, incoming.IsStackReserved(f.Item), "last merge contribution removed");
        Equal(1, incoming.ReservedSlots(f.Unit), "failed replacement preserves other slot claim");
        incoming.Clear(); incoming.Release(b);
        Equal(0, incoming.ReservedSlots(f.Unit), "clear resets slot index and stale release is harmless");
        Equal(0, incoming.CountFor(b), "clear removes owning plan");
        other.stackCount = 200;
        Equal(150, incoming.Reserve(a, other, 150).Count, "cleared indexes permit full free capacity");
    }

    private static void IncomingIndexMutations()
    {
        var f = new Fixture(); f.Unit.SlotCapacity = 12;
        var unit = new Building_StorageUnit { SlotCapacity = 9 }; f.Register(unit, 2);
        var types = new[] { f.Item.def, new ThingDef(), new ThingDef() };
        var stacks = new List<Thing> { f.Item };
        foreach (var type in types)
        {
            var stack = new Thing { def = type, stackCount = 40 };
            unit.Inventory.Contents.TryAdd(stack, false); stacks.Add(stack);
        }
        var incoming = new StorageIncomingReservations(new[] { f.Unit, unit });
        var plans = new Dictionary<object, StorageIncomingPlan>();
        var owners = new object[16]; for (int i = 0; i < owners.Length; i++) owners[i] = new object();
        var random = new Random(271828);
        for (int step = 0; step < 300; step++)
        {
            object owner = owners[random.Next(owners.Length)];
            if (step % 47 == 0) { incoming.Clear(); plans.Clear(); }
            else if (random.Next(3) == 0) { incoming.Release(owner); plans.Remove(owner); }
            else
            {
                var item = new Thing { def = types[random.Next(types.Length)], stackCount = 200 };
                var plan = incoming.Reserve(owner, item, random.Next(1, 201));
                plans.Remove(owner); if (plan.Count > 0) plans.Add(owner, plan);
            }
            var expectedStacks = new Dictionary<Thing, long>();
            var expectedSlots = new Dictionary<Building_StorageUnit, int>();
            foreach (var plan in plans.Values)
                foreach (var allocation in plan.Allocations)
                    if (allocation.Stack == null)
                    {
                        expectedSlots.TryGetValue(allocation.Unit, out int slots); expectedSlots[allocation.Unit] = slots + 1;
                    }
                    else
                    {
                        expectedStacks.TryGetValue(allocation.Stack, out long count); expectedStacks[allocation.Stack] = count + allocation.Count;
                    }
            foreach (Thing stack in stacks)
            {
                expectedStacks.TryGetValue(stack, out long expected);
                Equal(expected, incoming.ReservedOnStack(stack), "incremental merge totals match independently summed plans");
                Equal(expected > 0, incoming.IsStackReserved(stack), "indexed membership matches independent plans");
                Equal(true, expected <= stack.def.stackLimit - stack.stackCount, "random replacement never overbooks a physical stack");
            }
            foreach (var storageUnit in new[] { f.Unit, unit })
            {
                expectedSlots.TryGetValue(storageUnit, out int expected);
                Equal(expected, incoming.ReservedSlots(storageUnit), "incremental slot totals match independent plans");
                Equal(true, expected <= storageUnit.SlotCapacity - storageUnit.Inventory.UsedSlots, "random replacement never overbooks slots");
            }
        }
    }

    private static void ScopedOutgoingValidation()
    {
        var f = new Fixture(); f.Unit.SlotCapacity = 600;
        var items = new Thing[500]; var owners = new object[500];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = new Thing { def = f.Item.def, stackCount = 2 };
            f.Unit.Inventory.Contents.TryAdd(items[i], false); owners[i] = new object();
        }
        int policyChecks = 0; bool permitted = true; var client = new Client();
        Thing.StackCountReads = 0;
        for (int i = 0; i < items.Length; i++)
            Equal(1, f.Core.Network.ReserveOutgoing(owners[i], items[i], 1, f.Endpoint, client,
                () => { policyChecks++; return permitted; }), "large batch reserves exact stack");
        Equal(true, Thing.StackCountReads < 500 * 12, "batch reservation reads grow linearly instead of rescanning all earlier claims");
        Equal(0, policyChecks, "reservation preserves original deferred purpose check");
        Thing.StackCountReads = 0;
        Equal(true, f.Core.Network.TryWithdrawReserved(owners[0], items[0], 1, new ThingOwner(null), out _).Complete,
            "scoped validation permits target collection");
        Equal(1, policyChecks, "collection checks its own purpose without invoking 499 unrelated policies");
        Equal(true, Thing.StackCountReads < 50, "collection inspects only the target's physical claims");
        permitted = false; Find.TickManager.TicksGame += 60; f.Manager.MapComponentTick();
        Equal(500, policyChecks, "periodic validation still checks every remaining purpose");
        Equal(499, client.Calls, "periodic invalidation delivers remaining callbacks once");
        Equal(2, f.Core.Network.AvailableToWithdraw(items[499]), "invalidated unrelated claims release items");
    }

    private static void ConflictingCountLoss()
    {
        var f = new Fixture(); var other = new object(); var client = new Client(); f.Reserve();
        Equal(6, f.Core.Network.ReserveOutgoing(other, f.Item, 6, f.Endpoint, client), "two owners share exact physical stack");
        f.Item.stackCount = 6;
        var replacement = new Thing { stackCount = 5 }; f.Unit.Inventory.Contents.TryAdd(replacement, false);
        Equal(0, f.Core.Network.ReserveOutgoing(f.Token, replacement, 1), "invalid prior claim cannot be silently replaced on a different stack");
        Equal(false, f.Core.Network.HasOutgoingReservation(other, f.Item, 6), "targeted count check invalidates every competing owner on overdrawn stack");
        Equal(0, f.Consumer.Calls, "targeted invalidation still defers callbacks");
        f.Manager.MapComponentTick();
        Equal(1, f.Consumer.Calls, "first owner notified once"); Equal(1, client.Calls, "other conflicting owner notified once");
        Equal(StorageReservationFailure.InsufficientCount, client.Reason, "shared count loss preserves failure reason");
        Equal(5, f.Core.Network.AvailableToWithdraw(replacement), "rejected replacement leaves other stack unclaimed");
    }
}
