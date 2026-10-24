using System;
using System.Collections.Generic;
using System.Linq;
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
        internal ASEL.Building_MonolynProducer Producer(int steel, int components = 0, bool crucible = false)
        {
            var producer = crucible ? new ASEL.Building_Crucible() : new ASEL.Building_MonolynProducer();
            producer.Map = Map; producer.Spawned = true; producer.Faction = Faction.OfPlayer;
            producer.Position = new IntVec3(8, 0);
            if (steel > 0) producer.requiredCost.Add(new ThingDefCountClass(Steel, steel));
            if (components > 0) producer.requiredCost.Add(new ThingDefCountClass(Component, components));
            return producer;
        }
    }

    internal static int Run()
    {
        new MonolynCompatibility().Apply(new Harmony("magicstorage.tests.monolyn"));
        ContainerOnly(); MixedAndMultipleCosts(); MultipleUnits(); IneligibleUnits(); DisconnectedAndReserved();
        WholeReservedStack(); PreservedReservation(); CraftingGroupInvalidated(); IncomingMergeReservation(); Insufficient();
        LaterMaterialMissing(); DuplicateCosts(); GroundReservations(); ExtractionRollback(); StoredExtractionRollback(); EffectFailure();
        ProductionSupply(); ProductionReservations(); ProductionGuards(); ProductionReceiverFailures(); ProductionRollback();
        Equal(0, ASEL.PlaceWorker_LightBuilder.OriginalCalls, "prefix replaces original deduction without double charging");
        Equal(Effecter.Triggers, Effecter.Cleanups, "transfer effects cleaned up");
        return assertions;
    }

    private static void ProductionTick(ASEL.Building_MonolynProducer producer)
    {
        int tick = Find.TickManager.TicksGame;
        Find.TickManager.TicksGame += (300 - (tick + producer.thingIDNumber) % 300) % 300;
        producer.RunTick();
    }

    private static void ProductionSupply()
    {
        var f = new Fixture(); f.Store(f.First, f.Steel, 6); f.Store(f.Second, f.Steel, 9);
        f.Store(f.Second, f.Component, 4);
        var producer = f.Producer(10, 2, crucible: true);
        producer.innerContainer.TryAdd(new Thing { def = f.Steel, stackCount = 3 }, false);
        producer.requiredCost.Add(new ThingDefCountClass(f.Steel, 2));
        ProductionTick(producer);
        Equal(15, producer.innerContainer.Where(x => x.def == f.Steel).Sum(x => x.stackCount),
            "production receives only remaining costs, including duplicate material entries");
        Equal(2, producer.innerContainer.Where(x => x.def == f.Component).Sum(x => x.stackCount), "second material supplied");
        Equal(3L, f.Core.Network.GetTotalCount(f.Steel), "multi-unit source indexes updated exactly");
        Equal(2L, f.Core.Network.GetTotalCount(f.Component), "component remainder exact");
        Equal(0, producer.requiredCost.Count, "only fully supplied demands removed");
        Equal(0, f.Map.Ground.Count, "direct supply creates no ground material");
        Equal(1, producer.OriginalTicks, "Harmony prefix preserves original producer tick");
        ProductionTick(producer);
        Equal(3L, f.Core.Network.GetTotalCount(f.Steel), "repeated check does not charge twice");
        producer.requiredCost.Add(new ThingDefCountClass(f.Steel, 2));
        ProductionTick(producer);
        Equal(1L, f.Core.Network.GetTotalCount(f.Steel), "next production batch is replenished");
    }

    private static void ProductionReservations()
    {
        var f = new Fixture(); Thing steel = f.Store(f.First, f.Steel, 20);
        object claim = new object();
        f.Core.Network.ReserveOutgoing(claim, steel, 15);
        var producer = f.Producer(10);
        ProductionTick(producer);
        Equal(5, producer.requiredCost[0].count, "partial stock leaves exact missing quantity");
        Equal(5, producer.innerContainer.Sum(x => x.stackCount), "only unreserved stock supplied");
        Equal(true, f.Core.Network.HasOutgoingReservation(claim, steel, 15), "another consumer keeps reservation");
        f.Core.Network.ReleaseOutgoing(claim);
        ProductionTick(producer);
        Equal(0, producer.requiredCost.Count, "released stock satisfies remaining demand");
        Equal(10L, f.Core.Network.GetTotalCount(f.Steel), "no double deduction after partial delivery");

        f = new Fixture(); steel = f.Store(f.First, f.Steel, 20);
        var incoming = new Thing { def = f.Steel, stackCount = 5 };
        claim = new object(); f.Core.Network.ReserveIncoming(claim, incoming, 5);
        producer = f.Producer(10); ProductionTick(producer);
        Equal(0, producer.innerContainer.Count, "incoming merge reservation protects source stack");
        f.Core.Network.ReleaseIncoming(claim);
        ProductionTick(producer);
        Equal(10, producer.innerContainer.Sum(x => x.stackCount), "released incoming merge allows supply");
    }

    private static void ProductionGuards()
    {
        var f = new Fixture(); f.Store(f.First, f.Steel, 20);
        var producer = f.Producer(10);
        Find.TickManager.TicksGame += (300 - (Find.TickManager.TicksGame + producer.thingIDNumber) % 300) % 300 + 1;
        producer.RunTick();
        Equal(0, producer.innerContainer.Count, "production supply runs only at its interval");
        producer.working = false; ProductionTick(producer);
        producer.working = true; producer.pause = true; ProductionTick(producer);
        producer.pause = false; producer.forbidden = true; ProductionTick(producer);
        producer.forbidden = false; producer.Faction = new Faction(); ProductionTick(producer);
        producer.Faction = Faction.OfPlayer; producer.Spawned = false; ProductionTick(producer);
        producer.Spawned = true;
        f.Map.reservationManager.Reserved.Add(producer); ProductionTick(producer);
        f.Map.reservationManager.Reserved.Clear();
        f.Map.physicalInteractionReservationManager.Reserved.Add(producer); ProductionTick(producer);
        f.Map.physicalInteractionReservationManager.Reserved.Clear();
        Equal(20L, f.Core.Network.GetTotalCount(f.Steel), "inactive, forbidden, foreign, unspawned and pawn delivery guards preserve stock");
        f.Map.Fog.Add(f.First.Position); ProductionTick(producer);
        f.Map.Fog.Clear(); f.First.Faction = new Faction(); ProductionTick(producer);
        f.First.Faction = Faction.OfPlayer;
        f.Manager.Unregister(f.Core.node); f.Manager.EnsureCurrent(); ProductionTick(producer);
        Equal(0, producer.innerContainer.Count, "fogged, foreign and disconnected storage excluded");
        f.Manager.Register(f.Core.node); f.Manager.EnsureCurrent();
        ProductionTick(producer);
        Equal(10, producer.innerContainer.Sum(x => x.stackCount), "restored network resumes supply");
    }

    private sealed class ProductionContainer : ThingOwner
    {
        internal int Limit = int.MaxValue;
        internal bool Reject, ThrowBefore, ThrowAfter;
        internal ProductionContainer(IThingHolder owner) : base(owner) { }
        public override int GetCountCanAccept(Thing item, bool merge = true) => Math.Min(Limit, base.GetCountCanAccept(item, merge));
        public override bool TryAdd(Thing item, bool merge = true)
        {
            if (ThrowBefore) throw new InvalidOperationException("Injected production rejection");
            if (Reject) return false;
            bool accepted = base.TryAdd(item, merge);
            if (ThrowAfter) throw new InvalidOperationException("Injected failure after production acceptance");
            return accepted;
        }
    }

    private static void ProductionReceiverFailures()
    {
        foreach (int mode in new[] { 0, 1, 2, 3 })
        {
            var f = new Fixture(); Thing steel = f.Store(f.First, f.Steel, 20);
            var producer = f.Producer(10);
            var destination = new ProductionContainer(producer) {
                Reject = mode == 0, ThrowBefore = mode == 1, ThrowAfter = mode == 2, Limit = mode == 3 ? 4 : int.MaxValue };
            producer.innerContainer = destination;
            ProductionTick(producer);
            int accepted = mode == 2 ? 10 : mode == 3 ? 4 : 0;
            Equal(accepted, destination.Sum(x => x.stackCount), "actual acceptance tracked for receiver failure mode " + mode);
            Equal(20L - accepted, f.Core.Network.GetTotalCount(f.Steel), "undelivered stock restored for mode " + mode);
            Equal(10 - accepted, producer.requiredCost.Sum(x => x.count), "missing cost follows receipt even after exception " + mode);
            Equal(20, f.Core.Network.AvailableToWithdraw(steel) + destination.Sum(x => x.stackCount), "temporary reservation released " + mode);
            Equal(0, f.Map.Ground.Count, "rollback creates no loose duplicates " + mode);
        }
    }

    private static void ProductionRollback()
    {
        var f = new Fixture(); Thing steel = f.Store(f.First, f.Steel, 6); f.Store(f.Second, f.Steel, 6);
        var producer = f.Producer(10);
        Thing.BeforeMonolynTestSplit = item =>
        {
            if (item != steel) return;
            Thing.BeforeMonolynTestSplit = null;
            f.Manager.Unregister(f.Core.node); f.Manager.EnsureCurrent();
        };
        try { ProductionTick(producer); }
        finally { Thing.BeforeMonolynTestSplit = null; }
        Equal(0, producer.innerContainer.Count, "network loss during staging delivers no partial batch");
        Equal(10, producer.requiredCost[0].count, "failed staging leaves full missing cost");
        Equal(12L, f.First.Inventory.GetTotalCount(f.Steel) + f.Second.Inventory.GetTotalCount(f.Steel), "staged stock restored on network loss");
        f.Manager.Register(f.Core.node); f.Manager.EnsureCurrent(); ProductionTick(producer);
        Equal(10, producer.innerContainer.Sum(x => x.stackCount), "supply resumes after staging rollback");
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
