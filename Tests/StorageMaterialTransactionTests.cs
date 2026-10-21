using System;
using System.Linq;
using MagicStorage;
using Verse;

internal static class StorageMaterialTransactionTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Material transaction: " + message + "; expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly ThingDef Def = new ThingDef();
        internal readonly Thing Item;
        internal StorageNetwork Network => Core.Network;
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Manager.EnsureCurrent();
            Item = new Thing { def = Def, stackCount = 20, variant = "unique material" };
            Unit.Inventory.Contents.TryAdd(Item, false);
        }
        private void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageUnit unit) unit.node = node; else ((Building)thing).node = node;
            Manager.Register(node);
        }
        internal StorageMaterialTransaction Open() => new StorageMaterialTransaction(Map, Unit.Position);
    }

    internal static int Run()
    {
        MergeAndConsume(); CompletePrecheck(); DeliveryReceipts(); FailedAndPartialDelivery(); UnavailableSource();
        return checks;
    }

    private static void MergeAndConsume()
    {
        var f = new Fixture(); var other = new object();
        f.Network.ReserveOutgoing(other, f.Item, 5);
        var tx = f.Open();
        Equal(true, tx.Add(f.Item, 10, f.Network), "first requirement accepted");
        Equal(true, tx.Add(f.Item, 5, f.Network), "repeated source requirement combined");
        Equal(false, tx.Add(f.Item, 1, f.Network), "combined requirement cannot steal reserved quantity");
        Equal(1, tx.Requests.Count, "combined source has one reservation owner");
        Equal(true, tx.TryStage(), "unreserved batch extracted");
        Equal(false, tx.TryStage(), "same transaction cannot extract twice");
        Equal(false, tx.Add(f.Item, 1, f.Network), "plan cannot grow after extraction");
        Equal(5, f.Item.stackCount, "unrelated reservation remains in original container");
        Equal(true, tx.Consume(tx.Requests[0], 7), "consumer charges only actual amount used");
        Equal(false, tx.Consume(tx.Requests[0], 7), "same request cannot be charged twice");
        tx.Dispose(); tx.Dispose();
        Equal(13L, f.Unit.Inventory.GetTotalCount(f.Def), "unused staged remainder restored once");
        Equal(13L, f.Network.GetTotalCount(f.Def), "restoration updates index once");
        Equal(true, f.Network.HasOutgoingReservation(other, f.Item, 5), "unrelated reservation stays valid");
        Equal("unique material", f.Item.variant, "transfer preserves item-specific data");
        Equal(0, f.Map.Ground.Count, "restoration does not create loose duplicates");
    }

    private static void CompletePrecheck()
    {
        var f = new Fixture();
        var ground = new Thing { def = f.Def, stackCount = 3, Spawned = true, Map = f.Map };
        f.Map.Ground.Add(ground);
        using (var tx = f.Open())
        {
            Equal(false, tx.ConsumeAll(), "unprepared transaction cannot consume");
            Equal(true, tx.Add(ground, 3), "ground material planned first");
            Equal(true, tx.Add(f.Item, 10, f.Network), "stored material planned second");
            f.Network.ReserveOutgoing(new object(), f.Item, 15);
            Equal(false, tx.TryStage(), "new competing reservation rejects whole batch");
        }
        Equal(3, ground.stackCount, "earlier ground material was never extracted");
        Equal(true, ground.Spawned, "failed precheck leaves original ground position");
        Equal(20L, f.Unit.Inventory.GetTotalCount(f.Def), "failed precheck never changes storage");
    }

    private static void DeliveryReceipts()
    {
        foreach (bool throws in new[] { false, true })
        {
            var f = new Fixture(); var destination = new ThingOwner(null);
            using (var tx = f.Open())
            {
                tx.Add(f.Item, 10, f.Network); Equal(true, tx.TryStage(), "delivery batch extracted");
                int calls = 0;
                try
                {
                    bool delivered = tx.Deliver(tx.Requests[0], item =>
                    {
                        calls++;
                        StorageTransfer.Move(item.holdingOwner, destination, item, item.stackCount, f.Map, f.Unit.Position, false);
                        if (throws) throw new InvalidOperationException("after acceptance");
                        return false; // A false status cannot undo actual ownership transfer.
                    });
                    Equal(true, delivered, "ownership confirms delivery despite false status");
                }
                catch (InvalidOperationException) { Equal(true, throws, "injected post-acceptance failure observed"); }
                Equal(10, tx.Requests[0].DeliveredCount, "receipt records accepted quantity after return or exception");
                Equal(false, tx.Deliver(tx.Requests[0], item => { calls++; return true; }), "delivered request cannot be replayed");
                Equal(1, calls, "receiver called only once");
            }
            Equal(10, destination.Sum(item => item.stackCount), "accepted items stay at destination");
            Equal(10L, f.Unit.Inventory.GetTotalCount(f.Def), "accepted items are never refunded");
            Equal(0, f.Map.Ground.Count, "accepted items do not enter recovery release");
        }
    }

    private static void FailedAndPartialDelivery()
    {
        var f = new Fixture();
        using (var tx = f.Open())
        {
            tx.Add(f.Item, 10, f.Network); tx.TryStage();
            Equal(false, tx.Deliver(tx.Requests[0], item => true), "success status without taking items does not confirm delivery");
        }
        Equal(20L, f.Unit.Inventory.GetTotalCount(f.Def), "false success restores original materials");
        f = new Fixture();
        using (var tx = f.Open())
        {
            tx.Add(f.Item, 10, f.Network); tx.TryStage();
            try { tx.Deliver(tx.Requests[0], item => { throw new InvalidOperationException("before acceptance"); }); }
            catch (InvalidOperationException) { }
            Equal(0, tx.Requests[0].DeliveredCount, "pre-acceptance failure confirms no delivery");
        }
        Equal(20L, f.Unit.Inventory.GetTotalCount(f.Def), "pre-acceptance failure restores complete quantity");
        f = new Fixture(); var partialDestination = new ThingOwner(null);
        using (var tx = f.Open())
        {
            tx.Add(f.Item, 10, f.Network); tx.TryStage();
            Equal(true, tx.Deliver(tx.Requests[0], item =>
            {
                StorageTransfer.Move(item.holdingOwner, partialDestination, item, 4, f.Map, f.Unit.Position, false);
                return false;
            }), "partial acceptance produces a receipt");
            Equal(4, tx.Requests[0].DeliveredCount, "receipt contains only actual transferred quantity");
        }
        Equal(4, partialDestination.Sum(item => item.stackCount), "partial destination retains accepted quantity");
        Equal(16L, f.Unit.Inventory.GetTotalCount(f.Def), "partial delivery restores only undelivered remainder");
    }

    private static void UnavailableSource()
    {
        var f = new Fixture();
        using (var tx = f.Open())
        {
            tx.Add(f.Item, 10, f.Network); tx.TryStage();
            f.Unit.Spawned = false; f.Map.DropBudget = 0;
        }
        var holders = new System.Collections.Generic.List<IThingHolder>(); f.Manager.GetChildHolders(holders);
        Equal(10L, holders.Cast<StorageRecoveryBatch>().Sum(batch => batch.PendingCount), "unavailable source and blocked map retain saved recovery ownership");
        Equal(10L, f.Unit.Inventory.GetTotalCount(f.Def), "rollback does not hide material in an unavailable source");
        f.Map.DropBudget = int.MaxValue; Find.TickManager.TicksGame += 1000; f.Manager.MapComponentTick();
        Equal(10, f.Map.Ground.Sum(item => item.stackCount), "existing recovery mechanism eventually releases pending items");
    }
}
