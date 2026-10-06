using System;
using MagicStorage;
using RimWorld;
using Verse;
using Verse.AI;

internal static class StorageFoodTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string message)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal Map Map = new Map();
        internal Building_StorageCore Core = new Building_StorageCore();
        internal Building_StorageUnit Unit = new Building_StorageUnit();
        internal Building_StorageFoodOutlet Outlet = new Building_StorageFoodOutlet();
        internal ThingDef Meal = new ThingDef { IsNutritionGivingIngestible = true };
        internal Pawn Pawn;
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture(bool outlet = true, int foodCount = 5)
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1);
            if (outlet) Register(Outlet, 2);
            Manager.MapComponentTick();
            if (foodCount > 0) Unit.Inventory.Contents.TryAdd(new Thing { def = Meal, stackCount = foodCount });
            Pawn = NewPawn();
        }
        internal Pawn NewPawn() => new Pawn { Map = Map, Spawned = true, Position = new IntVec3(5, 0) };
        internal CompStorageNode Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Spawned = true; thing.Position = new IntVec3(x, 0);
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageCore core) core.node = node;
            if (thing is Building_StorageUnit unit) unit.node = node;
            if (thing is Building_StorageFoodOutlet outlet) outlet.node = node;
            Manager.Register(node); return node;
        }
        internal JobDriver_TakeStorageFood Driver(Pawn pawn = null)
        {
            pawn = pawn ?? Pawn;
            var driver = new JobDriver_TakeStorageFood { pawn = pawn, job = StorageFoodUtility.FindJob(pawn) };
            if (driver.job == null) throw new Exception("Expected food job");
            driver.SetupToils(); return driver;
        }
    }

    internal static int Run()
    {
        StorageFoodJobDefOf.MS_TakeStorageFood = new JobDef();
        FallbackAndPolicies(); DeliveryAndIdentity(); CompetingClaims(); TopologyAndCancellation();
        return assertions;
    }

    private static void FallbackAndPolicies()
    {
        var branch = new ThinkNode_StorageFoodPriority();
        branch.subNodes.Add(new JobGiver_GetStorageFood()); branch.subNodes.Add(new JobGiver_GetFood());
        var noOutlet = new Fixture(false);
        Equal(JobGiver_GetFood.VanillaFoodJob, branch.Issue(noOutlet.Pawn), "no outlet falls back to unchanged vanilla child");
        var empty = new Fixture(foodCount: 0);
        Equal(JobGiver_GetFood.VanillaFoodJob, branch.Issue(empty.Pawn), "empty network falls back");
        var f = new Fixture();
        Equal(9.5f, branch.GetPriority(f.Pawn), "branch delegates hunger priority");
        Equal(StorageFoodJobDefOf.MS_TakeStorageFood, branch.Issue(f.Pawn).def, "outlet has explicit precedence");
        f.Pawn.needs.food.CurCategory = HungerCategory.Fed;
        Equal(0f, branch.GetPriority(f.Pawn), "satiated colonist does not get raised priority");
        Equal<Job>(null, new JobGiver_GetStorageFood().Issue(f.Pawn), "satiated colonist gets no outlet job");
        f.Pawn.needs.food.CurCategory = HungerCategory.Hungry;
        f.Pawn.ForbiddenFoods.Add(f.Meal);
        Equal(JobGiver_GetFood.VanillaFoodJob, branch.Issue(f.Pawn), "diet restriction falls back");
        f.Pawn.ForbiddenFoods.Clear(); f.Outlet.forbidden = true;
        Equal<Job>(null, StorageFoodUtility.FindJob(f.Pawn), "forbidden outlet skipped");
        f.Outlet.forbidden = false; f.Pawn.Unreachable.Add(f.Outlet);
        Equal<Job>(null, StorageFoodUtility.FindJob(f.Pawn), "unreachable outlet skipped");
        f.Pawn.Unreachable.Clear(); f.Meal.IsDrug = true;
        Equal<Job>(null, StorageFoodUtility.FindJob(f.Pawn), "drug consumption not exposed as meals");
        f.Meal.IsDrug = false; f.Unit.Inventory.Contents[0].stale = true;
        Equal<Job>(null, StorageFoodUtility.FindJob(f.Pawn), "nonstarving colonist rejects stale food");
        f.Unit.Inventory.Contents[0].stale = false; f.Pawn.DevelopmentalStage = DevelopmentalStage.Baby;
        Equal<Job>(null, StorageFoodUtility.FindJob(f.Pawn), "baby feeding remains outside scope");
        f.Pawn.DevelopmentalStage = DevelopmentalStage.Adult; f.Pawn.IsColonist = false;
        Equal<Job>(null, StorageFoodUtility.FindJob(f.Pawn), "noncolonists remain outside scope");
    }

    private static void DeliveryAndIdentity()
    {
        var f = new Fixture(); var originalStack = f.Unit.Inventory.Contents[0];
        var existing = new Thing { def = f.Meal, stackCount = 2 };
        f.Pawn.inventory.innerContainer.TryAdd(existing);
        var driver = f.Driver();
        Equal(true, driver.TryMakePreToilReservations(false), "food probe is valid");
        Equal(5, f.Core.Network.AvailableToWithdraw(originalStack), "probe does not hold outgoing claim");
        driver.RunTestToils();
        Equal<JobCondition?>(JobCondition.Succeeded, driver.EndCondition, "food delivered successfully");
        Equal(4L, f.Core.Network.GetTotalCount(f.Meal), "one meal removed from network");
        Equal(2, existing.stackCount, "existing inventory stack was not merged into");
        Equal(2, f.Pawn.inventory.innerContainer.Count, "withdrawn meal has its own identity");
        var received = driver.job.targetA;
        Equal(false, received == originalStack, "job target replaced with actual split");
        Equal(true, f.Pawn.inventory.innerContainer.Contains(received), "exact split belongs to colonist");
        var ingest = driver.GetFinalizerJob(JobCondition.Succeeded);
        Equal(JobDefOf.Ingest, ingest.def, "handoff uses original ingest job");
        Equal(received, ingest.targetA, "ingest references exact delivered food");
        Equal(1, ingest.count, "ingest quantity matches requested meal");
        Equal<Job>(null, driver.GetFinalizerJob(JobCondition.InterruptForced), "cancellation does not force new ingest job");
        driver.ended = false; driver.RunTestToils(2);
        Equal(4L, f.Core.Network.GetTotalCount(f.Meal), "reentering delivery does not duplicate withdrawal");

        var rejected = new Fixture(); var source = rejected.Unit.Inventory.Contents[0]; var owner = new object();
        rejected.Core.Network.ReserveOutgoing(owner, source, 1);
        Equal(0, rejected.Core.Network.TryWithdrawReserved(owner, source, 1, new RejectingOwner(), out var failedItem).Transferred, "rejected receiver rolls back split");
        Equal<Thing>(null, failedItem, "failed transfer exposes no invalid received object");
        Equal(5L, rejected.Core.Network.GetTotalCount(rejected.Meal), "rollback conserves food");
        Equal(5, rejected.Core.Network.AvailableToWithdraw(source), "failed receiver releases claim");
    }
    private sealed class RejectingOwner : ThingOwner
    { internal RejectingOwner() : base(null) { } public override bool TryAdd(Thing thing, bool merge = true) => false; }

    private static void CompetingClaims()
    {
        var f = new Fixture(foodCount: 2); var source = f.Unit.Inventory.Contents[0];
        var first = f.Driver(); var second = f.Driver(f.NewPawn());
        Equal(true, first.EnsureReservation(), "first meal claimed");
        Equal(true, second.EnsureReservation(), "second meal claimed");
        Equal(0, f.Core.Network.AvailableToWithdraw(source), "both meals unavailable to other requesters");
        Equal<Job>(null, StorageFoodUtility.FindJob(f.NewPawn()), "third colonist falls back instead of overbooking");
        Equal(0, f.Core.Network.TryTransferTo(source, 2, new ThingOwner(null)).Transferred, "manual transfer respects outgoing claims");
        Equal(2L, f.Core.Network.GetTotalCount(f.Meal), "claims do not remove food from inventory");
        second.RunTestToils(); first.RunTestToils();
        Equal(0L, f.Core.Network.GetTotalCount(f.Meal), "reverse collection order conserves quantity");
        Equal(false, first.job.targetA == second.job.targetA, "each consumer gets its own real item");
        var incoming = new Fixture(foodCount: 2); incoming.Unit.SlotCapacity = 1;
        var meal = incoming.Unit.Inventory.Contents[0]; var owner = new object();
        incoming.Core.Network.ReserveOutgoing(owner, meal, 1);
        Equal(0, incoming.Core.Network.GetCountCanAccept(new Thing { def = incoming.Meal, stackCount = 1 }), "incoming cannot merge into outgoing reserved stack");
        incoming.Core.Network.ReleaseOutgoing(owner);
        Equal(1, incoming.Core.Network.GetCountCanAccept(new Thing { def = incoming.Meal, stackCount = 1 }), "release makes merge space available again");
    }

    private static void TopologyAndCancellation()
    {
        var f = new Fixture(); var source = f.Unit.Inventory.Contents[0];
        var driver = f.Driver(); driver.EnsureReservation();
        driver.Cleanup(JobCondition.InterruptForced);
        Equal(5, f.Core.Network.AvailableToWithdraw(source), "interruption releases reservation without extracting food");
        Equal<Job>(null, StorageFoodUtility.FindJob(f.Pawn), "failed request temporarily allows vanilla fallback");
        Find.TickManager.TicksGame += 251;
        Equal(true, StorageFoodUtility.FindJob(f.Pawn) != null, "food retry recovers after short delay");
        driver = f.Driver(); driver.EnsureReservation();
        f.Pawn.jobs.curDriver = driver; f.Map.mapPawns.AllPawnsSpawned.Add(f.Pawn);
        f.Manager.MarkDirty();
        Equal(false, driver.EnsureReservation(), "network invalidation stops pending collection");
        f.Manager.MapComponentTick();
        Equal(4, f.Core.Network.AvailableToWithdraw(source), "active claim rebuilt from saved job targets");
        var extra = f.Register(new Building_StorageCore(), 3); f.Manager.MapComponentTick();
        Equal(false, driver.EnsureReservation(), "multiple cores block collection");
        Equal(5L, f.Unit.Inventory.GetTotalCount(f.Meal), "conflict preserves all stored food");
        f.Manager.Unregister(extra); f.Manager.MapComponentTick();
        Equal(true, driver.EnsureReservation(), "single-core recovery restores request");
        f.Pawn.ForbiddenFoods.Add(f.Meal);
        Equal(false, driver.EnsureReservation(), "policy change rechecked before delivery");
        Equal(5, f.Core.Network.AvailableToWithdraw(source), "policy rejection releases claim");
        f.Pawn.ForbiddenFoods.Clear();
        var reloaded = new JobDriver_TakeStorageFood { pawn = f.Pawn, job = driver.job };
        reloaded.SetupToils(); f.Pawn.jobs.curDriver = reloaded;
        f.Manager.MarkDirty(); f.Manager.MapComponentTick();
        Equal(4, f.Core.Network.AvailableToWithdraw(source), "replacement driver recreates transient claim after rebuild");
        reloaded.RunTestToils();
        Equal(4L, f.Core.Network.GetTotalCount(f.Meal), "restored request delivers exactly once");
    }
}
