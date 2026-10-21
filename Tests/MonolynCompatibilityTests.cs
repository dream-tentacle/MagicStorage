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
        LaterMaterialMissing(); DuplicateCosts(); GroundReservations(); ExtractionRollback(); StoredExtractionRollback(); EffectFailure();
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
        Equal(false, f.Consume(10), "reserved quantity cannot fund immediate construction");
        Equal(20, item.stackCount, "insufficient unreserved stock remains unchanged");
        Equal(true, f.Core.Network.HasOutgoingReservation(claim, item, 15), "existing reservation survives failed consumption");
        f.Manager.MapComponentTick(); f.Manager.MapComponentTick();
        Equal(0, client.Calls, "failed consumption does not invalidate the existing owner");
        Equal(true, f.Consume(5), "unreserved remainder can still fund construction");
        Equal(true, f.Core.Network.HasOutgoingReservation(claim, item, 15), "consuming free remainder preserves existing claim");
        f.Manager.Unregister(f.Core.node); f.Manager.EnsureCurrent();
        Equal(false, f.First.Network.CanWork, "fixture network disconnected");
        Equal(false, f.Consume(5), "disconnected stock cannot be consumed");
        Equal(15L, f.First.Inventory.GetTotalCount(f.Steel), "disconnected physical inventory remains intact");
        Equal(0, f.Map.resourceCounter.GetCount(f.Steel), "disconnected stock is absent from resource count");
    }

    private static void WholeReservedStack()
    {
        var f = new Fixture(); Thing item = f.Store(f.First, f.Steel, 10);
        var client = new Client(); object claim = new object();
        f.Core.Network.ReserveOutgoing(claim, item, 5, client: client);
        Equal(false, f.Consume(10), "whole stack cannot be consumed while partly reserved");
        f.Manager.MapComponentTick();
        Equal(0, client.Calls, "reservation owner is not interrupted");
        Equal(false, item.Destroyed, "reserved stack remains a real item");
        f.Core.Network.ReleaseOutgoing(claim);
        Equal(true, f.Consume(10), "whole stack can be consumed after owner releases it");
    }

    private static void IncomingMergeReservation()
    {
        var f = new Fixture(); f.Store(f.First, f.Steel, 60);
        var incoming = new Thing { def = f.Steel, stackCount = 15 };
        var carry = new ThingOwner(null); carry.TryAdd(incoming, false);
        object claim = new object();
        Equal(15, f.Core.Network.ReserveIncoming(claim, incoming, 15), "incoming merge reserved");
        Equal(false, f.Consume(60), "incoming merge target cannot be consumed mid-delivery");
        Equal(15, f.Core.Network.TryStoreReserved(carry, incoming, 15, claim).Transferred,
            "incoming reservation completes without losing carried material");
        Equal(75L, f.First.Network.GetTotalCount(f.Steel), "existing and incoming material preserved exactly");
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
        Thing other = f.Store(f.Second, f.Component, 10);
        var recipe = new RecipeDef();
        recipe.fixedIngredientFilter.SetAllow(f.Steel, true); recipe.fixedIngredientFilter.SetAllow(f.Component, true);
        var metal = new IngredientCount { count = 10 }; metal.filter.SetAllow(f.Steel, true);
        var component = new IngredientCount { count = 3 }; component.filter.SetAllow(f.Component, true);
        recipe.ingredients.Add(metal); recipe.ingredients.Add(component);
        var order = f.Core.Crafting.Add(recipe);
        Equal(true, CraftingMaterialPreparation.Prepare(f.Core, order,
            new Dictionary<Thing, int> { { steel, 10 }, { other, 3 } }), "crafting batch owns materials before other consumers run");
        Equal(true, f.Consume(10), "Monolyn consumes only the remaining network stock");
        f.Manager.MapComponentTick(); f.Manager.MapComponentTick();
        Equal(13, order.Batch.Ingredients[0].stackCount + order.Batch.Ingredients[1].stackCount,
            "external consumption cannot touch prepared materials");
        Equal(7, f.Core.Network.AvailableToWithdraw(other), "unprepared components remain available without claims");
    }

    private static void Insufficient()
    {
        var f = new Fixture(); f.Store(f.First, f.Steel, 4);
        Equal(false, f.Consume(6), "insufficient deduction returns false");
        Equal(4L, f.First.Inventory.GetTotalCount(f.Steel), "insufficient stock is never partially consumed");
        Equal(true, f.Consume(4), "unchanged stock remains available for a smaller cost");
        Equal(false, f.Consume(1), "empty sources return false");
    }

    private static void LaterMaterialMissing()
    {
        var f = new Fixture(); Thing steel = f.Store(f.First, f.Steel, 20);
        int effects = Effecter.Triggers;
        Equal(false, f.Consume(10, 1), "later missing component rejects entire cost");
        Equal(20, steel.stackCount, "earlier sufficient material is not charged");
        Equal(20L, f.First.Inventory.GetTotalCount(f.Steel), "failed multi-material plan leaves index unchanged");
        Equal(20, f.Core.Network.AvailableToWithdraw(steel), "failed plan leaks no reservation");
        Equal(effects, Effecter.Triggers, "failed plan emits no paid-transfer effects");
    }

    private static void DuplicateCosts()
    {
        var f = new Fixture(); f.Store(f.First, f.Steel, 8);
        var def = new ThingDef { costList = new List<ThingDefCountClass> {
            new ThingDefCountClass { thingDef = f.Steel, count = 5 },
            new ThingDefCountClass { thingDef = f.Steel, count = 5 } } };
        Equal(false, f.Worker.DestroyResources(f.Map, def), "repeated costs cannot reuse the same five items");
        Equal(8L, f.First.Inventory.GetTotalCount(f.Steel), "failed repeated cost leaves entire stock");
        f.Store(f.Second, f.Steel, 4);
        Equal(true, f.Worker.DestroyResources(f.Map, def), "repeated costs aggregate across storage units");
        Equal(2L, f.First.Network.GetTotalCount(f.Steel), "aggregate cost charged exactly once");
    }

    private static void GroundReservations()
    {
        var f = new Fixture();
        var ground = new Thing { def = f.Steel, stackCount = 10, Map = f.Map, Spawned = true };
        f.Map.Ground.Add(ground); f.Map.listerThings.Things.Add(ground);
        f.Map.reservationManager.Reserved.Add(ground);
        Equal(false, f.Consume(5), "normal ground reservation blocks consumption");
        f.Map.reservationManager.Reserved.Clear(); f.Map.physicalInteractionReservationManager.Reserved.Add(ground);
        Equal(false, f.Consume(5), "physical interaction reservation blocks consumption");
        Equal(10, ground.stackCount, "both reservation failures preserve ground quantity");
        f.Map.physicalInteractionReservationManager.Reserved.Clear();
        Equal(true, f.Consume(5), "released ground stock is available again");
        Equal(5, ground.stackCount, "ground deduction remains exact");
    }

    private static void ExtractionRollback()
    {
        foreach (bool wholeGroundStack in new[] { false, true })
        {
            var f = new Fixture(); Thing component = f.Store(f.First, f.Component, 3);
            var ground = new Thing { def = f.Steel, stackCount = 10, Map = f.Map, Spawned = true };
            f.Map.Ground.Add(ground); f.Map.listerThings.Things.Add(ground);
            if (wholeGroundStack) f.Map.DropBudget = 0;
            Thing.BeforeMonolynTestSplit = item =>
            {
                if (item != ground) return;
                Thing.BeforeMonolynTestSplit = null;
                f.Manager.Unregister(f.Core.node); f.Manager.EnsureCurrent();
            };
            try { Equal(false, f.Consume(wholeGroundStack ? 10 : 5, 1), "network loss during extraction aborts payment"); }
            finally { Thing.BeforeMonolynTestSplit = null; }
            Equal(3, component.stackCount, "later component is never destroyed after failed extraction");
            Equal(3L, f.First.Inventory.GetTotalCount(f.Component), "component ownership and index remain correct");
            var holders = new List<IThingHolder>(); f.Manager.GetChildHolders(holders);
            long pending = 0; foreach (StorageRecoveryBatch batch in holders) pending += batch.PendingCount;
            if (wholeGroundStack)
            {
                Equal(10L, pending, "blocked full-stack rollback stays in saved recovery");
                Equal(false, ground.Destroyed, "recovery preserves original whole stack");
                f.Map.DropBudget = int.MaxValue; Find.TickManager.TicksGame += 1000; f.Manager.MapComponentTick();
                Equal(ground, f.Map.Ground[0], "recovery later releases original whole stack");
            }
            else
            {
                Equal(10, ground.stackCount, "partial ground withdrawal is restored to original stack");
                Equal(0L, pending, "successful rollback leaves no duplicate recovery stock");
            }
        }
    }

    private static void EffectFailure()
    {
        var f = new Fixture(); f.Store(f.First, f.Steel, 10);
        Effecter.ThrowOnTrigger = true;
        try { Equal(true, f.Consume(5), "cosmetic effect failure cannot reject paid construction"); }
        finally { Effecter.ThrowOnTrigger = false; }
        Equal(5L, f.First.Inventory.GetTotalCount(f.Steel), "effect failure does not charge twice or refund payment");
    }

    private static void StoredExtractionRollback()
    {
        var f = new Fixture(); Thing steel = f.Store(f.First, f.Steel, 20);
        var component = new Thing { def = f.Component, stackCount = 3, Map = f.Map, Spawned = true };
        f.Map.Ground.Add(component); f.Map.listerThings.Things.Add(component);
        Thing.BeforeMonolynTestSplit = item =>
        {
            if (item != steel) return;
            Thing.BeforeMonolynTestSplit = null;
            f.Map.reservationManager.Reserved.Add(component);
        };
        try { Equal(false, f.Consume(10, 1), "new ground reservation after storage withdrawal aborts entire payment"); }
        finally { Thing.BeforeMonolynTestSplit = null; }
        Equal(20L, f.First.Inventory.GetTotalCount(f.Steel), "withdrawn material restored to original storage inventory");
        Equal(20L, f.Core.Network.GetTotalCount(f.Steel), "restored material reindexed in network exactly once");
        Equal(20, f.Core.Network.AvailableToWithdraw(steel), "failed extraction releases its temporary storage claim");
        Equal(3, component.stackCount, "newly reserved ground component remains untouched");
        Equal(0, f.Map.Ground.FindAll(item => item.def == f.Steel).Count, "successful storage restoration creates no loose duplicate");
    }
}
