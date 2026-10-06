using System;
using System.Collections.Generic;
using MagicStorage;
using RimWorld;
using Verse;
using Verse.AI;

internal static class StorageFoodRequestTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string message)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }
    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Building_StorageFoodOutlet Outlet = new Building_StorageFoodOutlet();
        internal readonly ThingDef Meal = new ThingDef { IsNutritionGivingIngestible = true };
        internal readonly Pawn Getter;
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Register(Outlet, 2);
            Manager.MapComponentTick();
            Unit.Inventory.Contents.TryAdd(new Thing { def = Meal, stackCount = 20 });
            Getter = NewPawn();
        }
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Pawn NewPawn() => new Pawn { Faction = Faction.OfPlayer, Spawned = true, Map = Map, Position = new IntVec3(4, 0) };
        private void Register(Thing thing, int x)
        {
            thing.Faction = Faction.OfPlayer; thing.Spawned = true; thing.Map = Map; thing.Position = new IntVec3(x, 0);
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageCore core) core.node = node;
            if (thing is Building_StorageUnit unit) unit.node = node;
            if (thing is Building_StorageFoodOutlet outlet) outlet.node = node;
            Manager.Register(node);
        }
        internal JobDriver_FetchStorageFood Driver(StorageFoodPurpose purpose, Thing target = null)
        {
            var job = StorageFoodRequest.Find(Getter, purpose, target);
            if (job == null) throw new Exception("Expected request for " + purpose);
            var driver = new JobDriver_FetchStorageFood { pawn = Getter, job = job };
            driver.SetupToils(); return driver;
        }
    }

    internal static int Run()
    {
        RecipientPolicies(); Handoffs(); AnimalAndFailure(); PackAndContexts(); Reservations(); NativeNodeSettings();
        return assertions;
    }

    private static void RecipientPolicies()
    {
        var f = new Fixture(); var patient = f.NewPawn(); var food = f.Unit.Inventory.Contents[0];
        f.Getter.ForbiddenFoods.Add(f.Meal);
        Equal(true, StorageFoodRequest.CanEat(f.Getter, food, StorageFoodPurpose.FeedPatient, patient), "feeder's personal diet does not decide patient's meal");
        patient.ForbiddenFoods.Add(f.Meal); f.Getter.ForbiddenFoods.Clear();
        Equal(false, StorageFoodRequest.CanEat(f.Getter, food, StorageFoodPurpose.FeedPatient, patient), "patient policy is authoritative");
        patient.ForbiddenFoods.Clear(); patient.IsAnimal = true;
        f.Meal.ingestible.preferability = FoodPreferability.RawTasty;
        Equal(true, StorageFoodRequest.CanEat(f.Getter, food, StorageFoodPurpose.Train, patient), "raw animal feed accepted");
        f.Meal.ingestible.preferability = FoodPreferability.MealLavish;
        Equal(false, StorageFoodRequest.CanEat(f.Getter, food, StorageFoodPurpose.Train, patient), "training excludes meal-tier food");
        f.Meal.IsDrug = true;
        Equal(false, StorageFoodRequest.CanEat(f.Getter, food, StorageFoodPurpose.Eat, default(LocalTargetInfo)), "drug remains excluded");
        f.Meal.IsDrug = false; f.Getter.formingCaravan = true;
        Equal<Job>(null, StorageFoodRequest.Find(f.Getter, StorageFoodPurpose.Eat), "forming caravan remains entirely native");
        f.Getter.formingCaravan = false; f.Getter.needs.food = null; f.Getter.IsColonist = false; f.Getter.IsColonyMechPlayerControlled = true;
        Equal(true, StorageFoodRequest.CanRequest(f.Getter, StorageFoodPurpose.FeedPatient), "worker need not have its own hunger need");
        Equal(true, StorageFoodRequest.Find(f.Getter, StorageFoodPurpose.FeedPatient, patient) != null, "controlled medic can request patient's food");
    }

    private static void Handoffs()
    {
        foreach (var purpose in new[] { StorageFoodPurpose.FeedPatient, StorageFoodPurpose.WardenFeed, StorageFoodPurpose.Deliver, StorageFoodPurpose.BottleFeed })
        {
            var f = new Fixture(); var recipient = f.NewPawn();
            var driver = f.Driver(purpose, recipient); driver.RunTestToils();
            var received = driver.job.targetA; var next = driver.GetFinalizerJob(JobCondition.Succeeded);
            Equal(true, f.Getter.inventory.innerContainer.Contains(received), purpose + " withdraws real food into feeder inventory");
            Equal(19L, f.Core.Network.GetTotalCount(f.Meal), purpose + " removes exactly one portion");
            Equal(purpose == StorageFoodPurpose.BottleFeed ? JobDefOf.BottlefeedBaby : purpose == StorageFoodPurpose.Deliver ? JobDefOf.DeliverFood : JobDefOf.FeedPatient,
                next.def, purpose + " continues original task");
            Equal(recipient, purpose == StorageFoodPurpose.BottleFeed ? next.targetA : next.targetB, purpose + " retains recipient");
            Equal(received, purpose == StorageFoodPurpose.BottleFeed ? next.targetB : next.targetA, purpose + " uses transferred identity");
        }
        foreach (var purpose in new[] { StorageFoodPurpose.Train, StorageFoodPurpose.Tame })
        {
            var f = new Fixture(); var animal = f.NewPawn(); animal.IsAnimal = true;
            f.Meal.ingestible.preferability = FoodPreferability.RawTasty;
            var driver = f.Driver(purpose, animal); driver.RunTestToils();
            var next = driver.GetFinalizerJob(JobCondition.Succeeded);
            Equal(purpose == StorageFoodPurpose.Train ? JobDefOf.Train : JobDefOf.Tame, next.def, "native animal interaction resumes");
            Equal(animal, next.targetA, "animal target retained");
            Equal(false, next.targetC.IsValid, "taming must not walk to already-in-inventory food");
        }
        foreach (var purpose in new[] { StorageFoodPurpose.Hopper, StorageFoodPurpose.GrowthVat, StorageFoodPurpose.Biosculpter })
        {
            var f = new Fixture(); var device = new Thing { nutritionWanted = 3, Position = new IntVec3(8, 0) };
            var driver = f.Driver(purpose, device); driver.RunTestToils();
            var next = driver.GetFinalizerJob(JobCondition.Succeeded);
            Equal(3, next.count, "device amount uses outstanding demand");
            Equal(f.Getter.carryTracker.CarriedThing, next.targetA, "native hauling receives already-carried actual target");
            Equal(purpose == StorageFoodPurpose.Hopper ? JobDefOf.HaulToCell : JobDefOf.HaulToContainer, next.def, "native delivery driver selected");
            Equal(17L, f.Core.Network.GetTotalCount(f.Meal), "device extraction conserved");
        }
    }

    private static void AnimalAndFailure()
    {
        var f = new Fixture(); f.Getter.IsColonist = false; f.Getter.IsAnimal = true; f.Getter.RaceProps.ToolUser = false; f.Getter.health.capacities.manipulation = false;
        var driver = f.Driver(StorageFoodPurpose.Eat); driver.RunTestToils();
        var ingest = driver.GetFinalizerJob(JobCondition.Succeeded);
        Equal(JobDefOf.Ingest, ingest.def, "animal uses native ingestion");
        Equal(true, ingest.targetA.Spawned, "animal ingestion receives a spawned target");
        Equal(0, f.Getter.inventory.innerContainer.Count, "no food left stranded in animal inventory");
        var blocked = new Fixture(); blocked.Getter.IsAnimal = true; blocked.Getter.RaceProps.ToolUser = false;
        blocked.Getter.inventory.innerContainer.rejectDrop = true;
        driver = blocked.Driver(StorageFoodPurpose.Eat); driver.RunTestToils();
        Equal<Job>(null, driver.GetFinalizerJob(JobCondition.Succeeded), "placement failure does not create impossible ingest job");
        var holders = new List<IThingHolder>(); blocked.Manager.GetChildHolders(holders);
        Equal(1, holders.Count, "failed animal drop preserved in saved recovery holder");
        Equal(1, holders[0].GetDirectlyHeldThings()[0].stackCount, "failed drop conserves withdrawn portion");
        Equal(0, blocked.Getter.inventory.innerContainer.Count, "failed drop does not strand food on animal");
    }

    private static void PackAndContexts()
    {
        var f = new Fixture(); var node = new JobGiver_StoragePackFood();
        var pack = node.Issue(f.Getter);
        Equal(StorageFoodPurpose.Pack, StorageFoodRequest.Purpose(pack), "stored nutrition counts toward native reserve threshold");
        var driver = f.Driver(StorageFoodPurpose.Pack); driver.RunTestToils();
        Equal<Job>(null, driver.GetFinalizerJob(JobCondition.Succeeded), "packing does not immediately eat");
        Equal(2, f.Getter.inventory.innerContainer[0].stackCount, "packing uses nutrition-sized quantity");
        var far = new Fixture(); far.Getter.Position = new IntVec3(30, 0);
        Equal<Job>(null, StorageFoodRequest.Find(far.Getter, StorageFoodPurpose.Pack), "pack outlet respects native local search distance");
        f = new Fixture(); f.Getter.mindState.duty = new PawnDuty { focus = new IntVec3(30, 0) };
        Equal<Job>(null, StorageFoodRequest.Find(f.Getter, StorageFoodPurpose.Gathering, f.Getter.mindState.duty.focus), "gathering only uses an outlet in gathering area");
        f.Getter.mindState.duty.focus = f.Outlet.Position;
        Equal(true, StorageFoodRequest.Find(f.Getter, StorageFoodPurpose.Gathering, f.Getter.mindState.duty.focus) != null, "gathering outlet is usable");
        f.Getter.InMentalState = true; f.Outlet.forbidden = true; f.Unit.Inventory.Contents[0].forbidden = true;
        Equal<Job>(null, StorageFoodRequest.Find(f.Getter, StorageFoodPurpose.Eat), "ordinary request cannot override mental state");
        driver = f.Driver(StorageFoodPurpose.Binge); driver.RunTestToils();
        var binge = driver.GetFinalizerJob(JobCondition.Succeeded);
        Equal(true, binge.overeat && binge.ignoreForbidden, "binge preserves overeating and forbidden handling");
        f = new Fixture(); f.Getter.mindState.lastIngestTick = Find.TickManager.TicksGame;
        Equal(JobGiver_GetFood.VanillaFoodJob, new JobGiver_StorageBingeFood().Issue(f.Getter), "binge interval cannot be bypassed by outlet");
        ChildcareUtility.Baby = f.NewPawn(); ChildcareUtility.Source = f.Getter;
        Equal(JobGiver_GetFood.VanillaFoodJob, new JobGiver_StorageAutofeed().Issue(f.Getter), "breastfeeding keeps native precedence");
        ChildcareUtility.Source = null; ChildcareUtility.HasBreastfeeder = true;
        Equal(JobGiver_GetFood.VanillaFoodJob, new JobGiver_StorageAutofeed().Issue(f.Getter), "immobile breastfeeder keeps native precedence");
        ChildcareUtility.HasBreastfeeder = false;
        Equal(StorageFoodPurpose.BottleFeed, StorageFoodRequest.Purpose(new JobGiver_StorageAutofeed().Issue(f.Getter)), "urgent baby without physical food can request outlet");
        ChildcareUtility.Baby = null;
        var bingeWorker = new MentalStateWorker_StorageBingeFood();
        Equal(false, bingeWorker.StateCanOccur(f.Getter), "exactly ten nutrition retains native strict binge threshold");
        f.Map.resourceCounter.TotalHumanEdibleNutrition = 0.5f;
        Equal(true, bingeWorker.StateCanOccur(f.Getter), "stored food supplements native binge trigger threshold");
        f.Getter.formingCaravan = true;
        Equal(false, bingeWorker.StateCanOccur(f.Getter), "caravan binge eligibility remains native");
    }

    private static void Reservations()
    {
        var f = new Fixture(); var patient = f.NewPawn(); var driver = f.Driver(StorageFoodPurpose.FeedPatient, patient);
        Equal(true, driver.EnsureReservation(), "new request reserves food");
        Equal(19, f.Core.Network.AvailableToWithdraw(f.Unit.Inventory.Contents[0]), "claim reduces available food");
        patient.ForbiddenFoods.Add(f.Meal);
        Equal(false, driver.EnsureReservation(), "recipient policy change cancels claim before withdrawal");
        Equal(20, f.Core.Network.AvailableToWithdraw(f.Unit.Inventory.Contents[0]), "policy change releases claim");
        patient.ForbiddenFoods.Clear(); driver.EnsureReservation();
        f.Getter.jobs.curDriver = driver; f.Map.mapPawns.AllPawnsSpawned.Add(f.Getter);
        f.Manager.MarkDirty(); f.Manager.MapComponentTick();
        Equal(19, f.Core.Network.AvailableToWithdraw(f.Unit.Inventory.Contents[0]), "network rebuild restores new driver claim");
        patient.workNeeded = false;
        Equal(false, driver.EnsureReservation(), "recipient no longer needs food cancels claim");
        driver.Cleanup(JobCondition.InterruptForced);
        Equal(20L, f.Core.Network.GetTotalCount(f.Meal), "canceled request does not extract anything");
    }

    private static void NativeNodeSettings()
    {
        var f = new Fixture(); var node = new JobGiver_StorageFood();
        var min = HarmonyLib.AccessTools.Field(typeof(JobGiver_GetFood), "minCategory");
        var max = HarmonyLib.AccessTools.Field(typeof(JobGiver_GetFood), "maxLevelPercentage");
        min.SetValue(node, HungerCategory.Starving);
        Equal(JobGiver_GetFood.VanillaFoodJob, node.Issue(f.Getter), "special node minimum hunger threshold preserved");
        min.SetValue(node, HungerCategory.Fed); max.SetValue(node, 0.1f);
        Equal(JobGiver_GetFood.VanillaFoodJob, node.Issue(f.Getter), "special node maximum food threshold preserved");
        max.SetValue(node, 1f);
        Equal(StorageFoodPurpose.Eat, StorageFoodRequest.Purpose(node.Issue(f.Getter)), "native-configured node can select outlet");
        f.Meal.ingestible.preferability = FoodPreferability.RawBad;
        JobGiver_GetFood.HasVanillaFood = false;
        FoodUtility.DispenserSource = new Building_NutrientPasteDispenser { Hopper = new Thing { nutritionWanted = 4 } };
        try
        {
            Equal(StorageFoodPurpose.Hopper, StorageFoodRequest.Purpose(node.Issue(f.Getter)), "hungry pawn can refill empty dispenser from outlet");
        }
        finally { JobGiver_GetFood.HasVanillaFood = true; FoodUtility.DispenserSource = null; }
    }
}
