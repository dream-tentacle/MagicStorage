using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;
using Verse.AI;

internal static class StorageApparelTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Apparel adapter: " + message + "; expected " + expected + ", got " + actual); }
    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Pawn Pawn;
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Building_StorageApparelAdapter Adapter = new Building_StorageApparelAdapter();
        internal readonly ThingDef Def = new ThingDef { stackLimit = 1, IsApparel = true, apparel = new ApparelProperties() };
        internal readonly Apparel Item;
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture(float score = 2f)
        {
            StorageApparelServices.Policy = new VanillaStorageApparelPolicy();
            Find.TickManager.TicksGame = 1000;
            Map.component = new MapComponent_StorageNetworks(Map);
            Pawn = new Pawn { Map = Map, Spawned = true, Faction = Faction.OfPlayer };
            Map.mapPawns.AllPawnsSpawned.Add(Pawn);
            Register(Core, 0); Register(Unit, 1); Register(Adapter, 2); Manager.EnsureCurrent();
            Pawn.outfits.CurrentApparelPolicy.filter.SetAllow(Def, true);
            Item = new Apparel { def = Def, stackCount = 1, testScore = score };
            Unit.Inventory.Contents.TryAdd(Item, false);
        }
        internal void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Spawned = true; thing.Faction = Faction.OfPlayer; thing.Position = new IntVec3(x, 0);
            var node = new CompStorageNode { parent = thing };
            if (thing is Building building) building.node = node;
            if (thing is Building_StorageUnit unit) unit.node = node;
            Manager.Register(node);
        }
        internal Job FetchJob() => JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("MS_TakeStorageApparel"), Item, Adapter, Core);
        internal JobDriver_TakeStorageApparel Start(Pawn pawn = null, Building_StorageApparelAdapter adapter = null)
        {
            pawn = pawn ?? Pawn;
            var driver = new JobDriver_TakeStorageApparel { pawn = pawn,
                job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("MS_TakeStorageApparel"), Item, adapter ?? Adapter, Core) };
            pawn.jobs.curDriver = driver; pawn.jobs.curJob = driver.job;
            Equal(true, driver.TryMakePreToilReservations(false), "claim starts");
            driver.SetupToils(); return driver;
        }
        internal long Pending()
        { var holders = new List<IThingHolder>(); Manager.GetChildHolders(holders); return holders.Cast<StorageRecoveryBatch>().Sum(h => h.PendingCount); }
    }
    internal static int Run()
    { Selection(); Eligibility(); Reservations(); CollectionAndRecovery(); return checks; }

    private static void Selection()
    {
        var f = new Fixture(); var giver = new JobGiver_OptimizeStorageApparel();
        Job selected = giver.GiveJob(f.Pawn);
        Equal(DefDatabase<JobDef>.GetNamed("MS_TakeStorageApparel"), selected.def, "network garment becomes fetch job when map has none");
        Equal(f.Item, selected.targetA, "exact garment selected"); Equal(f.Adapter, selected.targetB, "adapter selected");
        Equal(f.Core, selected.targetC.Thing, "owning core carried in job");
        Equal(0, f.Pawn.mindState.nextApparelOptimizeTick, "successful network choice does not inherit vanilla no-result cooldown");
        Equal(NeededWarmth.Any, JobGiver_OptimizeApparel.CurrentWarmth, "native warmth context restored after comparison");
        Equal(true, f.Unit.Inventory.Contents.Contains(f.Item), "selection alone does not withdraw clothing");
        var loose = new Apparel { def = f.Def, testScore = 3f };
        var native = JobMaker.MakeJob(JobDefOf.Wear, loose); giver.NativeJob = native;
        Equal(native, giver.GiveJob(f.Pawn), "better native apparel retained");
        loose.testScore = 2f; Equal(native, giver.GiveJob(f.Pawn), "equal scores retain native choice");
        loose.testScore = 1f; Equal(f.Item, giver.GiveJob(f.Pawn).targetA, "better network clothing beats map choice");
        var removal = JobMaker.MakeJob(JobDefOf.RemoveApparel, loose); giver.NativeJob = removal;
        Equal(removal, giver.GiveJob(f.Pawn), "native removal takes precedence");
        giver.NativeJob = native; f.Pawn.mindState.nextApparelOptimizeTick = 2000;
        Equal<Job>(null, giver.GiveJob(f.Pawn), "native optimization interval respected");
        f.Pawn.mindState.nextApparelOptimizeTick = 0; giver.NativeJob = null;
        f.Item.testScore = 0.049f; Equal<Job>(null, giver.GiveJob(f.Pawn), "native minimum score gain respected");

        f = new Fixture(); giver = new JobGiver_OptimizeStorageApparel();
        var second = new Building_StorageApparelAdapter(); f.Register(second, 3); f.Manager.EnsureCurrent();
        Equal(f.Adapter, giver.GiveJob(f.Pawn).targetB, "nearest usable adapter chosen");
        f.Map.reservationManager.Reserved.Add(f.Adapter);
        Equal(second, giver.GiveJob(f.Pawn).targetB, "busy adapter does not hide another adapter in same network");
        f.Map.reservationManager.Reserved.Clear(); f.Pawn.Unreachable.Add(f.Adapter); f.Pawn.Unreachable.Add(second);
        Equal<Job>(null, giver.GiveJob(f.Pawn), "unreachable adapters never generate fetch jobs");
        f = new Fixture(); giver = new JobGiver_OptimizeStorageApparel(); f.Manager.Unregister(f.Core.node);
        Equal<Job>(null, giver.GiveJob(f.Pawn), "disconnected network cannot supply apparel");
        f = new Fixture(); giver = new JobGiver_OptimizeStorageApparel();
        f.Register(new Building_StorageCore(), 3); f.Manager.EnsureCurrent();
        Equal<Job>(null, giver.GiveJob(f.Pawn), "multiple cores disable apparel selection");
    }
    private static void Eligibility()
    {
        var f = new Fixture(); var policy = StorageApparelServices.Policy;
        Equal(true, policy.Allows(f.Pawn, f.Item), "allowed garment accepted");
        f.Pawn.outfits.CurrentApparelPolicy.filter.SetDisallowAll(); Equal(false, policy.Allows(f.Pawn, f.Item), "apparel policy respected");
        f.Pawn.outfits.CurrentApparelPolicy.filter.SetAllow(f.Def, true);
        f.Def.apparel.gender = Gender.Female; f.Pawn.gender = Gender.Male;
        Equal(false, policy.Allows(f.Pawn, f.Item), "gender restrictions respected"); f.Def.apparel.gender = Gender.None;
        f.Item.testBiocoded = true; Equal(false, policy.Allows(f.Pawn, f.Item), "another owner's biocoding rejected");
        f.Item.testOwner = f.Pawn; Equal(true, policy.Allows(f.Pawn, f.Item), "own biocoded garment accepted");
        f.Def.apparel.hasParts = false; Equal(false, policy.Allows(f.Pawn, f.Item), "required body parts checked"); f.Def.apparel.hasParts = true;
        f.Def.apparel.developmentalStageFilter.allowed = false; Equal(false, policy.Allows(f.Pawn, f.Item), "developmental stage checked");
        f.Def.apparel.developmentalStageFilter.allowed = true;
        f.Pawn.questLodger = true; Equal(false, policy.CanOptimize(f.Pawn), "quest lodgers excluded"); f.Pawn.questLodger = false;
        f.Pawn.IsMutant = true; f.Pawn.mutant.Def.disableApparel = true; Equal(false, policy.CanOptimize(f.Pawn), "mutants unable to wear apparel excluded");
        f.Pawn.IsMutant = false; f.Pawn.apparel.Locked.Add(new Apparel());
        Equal<Job>(null, new JobGiver_OptimizeStorageApparel().GiveJob(f.Pawn), "native score rejection for locked clothing retained");
    }
    private static void Reservations()
    {
        var f = new Fixture(); var driver = f.Start();
        Equal(0, f.Core.Network.AvailableToWithdraw(f.Item), "garment unavailable to competing storage consumers before arrival");
        Equal(true, f.Unit.Inventory.Contents.Contains(f.Item), "reserved garment stays in storage unit");
        Equal(false, f.Adapter.HasApparel, "walking does not stage garment early");
        var second = new Building_StorageApparelAdapter(); f.Register(second, 3); f.Manager.EnsureCurrent();
        Equal(false, driver.ended, "valid claim survives unrelated graph rebuild");
        var pawn = new Pawn { Map = f.Map, Spawned = true, Faction = Faction.OfPlayer };
        pawn.outfits.CurrentApparelPolicy.filter.SetAllow(f.Def, true);
        var competing = new JobDriver_TakeStorageApparel { pawn = pawn, job = JobMaker.MakeJob(driver.job.def, f.Item, second, f.Core) };
        Equal(false, competing.TryMakePreToilReservations(false), "second colonist cannot claim same garment at another adapter");
        driver.EndJobWith(JobCondition.InterruptForced);
        Equal(1, f.Core.Network.AvailableToWithdraw(f.Item), "walking interruption releases claim");
        Equal(true, f.Unit.Inventory.Contents.Contains(f.Item), "walking interruption preserves garment");

        f = new Fixture(); driver = f.Start(); f.Pawn.outfits.CurrentApparelPolicy.filter.SetDisallowAll(); f.Manager.MapComponentTick();
        Equal(true, driver.ended, "policy change invalidates unified claim");
        Equal(true, f.Unit.Inventory.Contents.Contains(f.Item), "invalidated policy never withdraws garment");
        f = new Fixture(); driver = f.Start(); f.Manager.Unregister(f.Adapter.node); f.Manager.MapComponentTick();
        Equal(true, driver.ended, "adapter removal invalidates pending fetch");
        f = new Fixture(); driver = f.Start(); f.Item.Destroy(); f.Manager.MapComponentTick();
        Equal(true, driver.ended, "externally destroyed garment invalidates fetch");
    }
    private static void CollectionAndRecovery()
    {
        var f = new Fixture(); var driver = f.Start(); driver.RunTestToils();
        Equal(JobCondition.Succeeded, driver.EndCondition.Value, "arrival completes retrieval");
        Equal(false, f.Unit.Inventory.Contents.Contains(f.Item), "arrival withdraws real garment");
        Equal(true, f.Adapter.HasPrepared(f.Pawn, f.Item), "adapter holds only prepared garment for recipient");
        var recipe = new RecipeDef(); recipe.products.Add(new ThingDefCountClass { thingDef = f.Def });
        var countOrder = f.Core.Crafting.Add(recipe);
        Equal(1L, new MapAndNetworkCraftingProductCounter().Count(f.Core, countOrder), "staged garment still counts toward a production target");
        var wear = driver.GetFinalizerJob(JobCondition.Succeeded);
        Equal(JobDefOf.Wear, wear.def, "handoff uses original Wear job definition"); Equal(f.Item, wear.targetA, "original Wear receives actual garment");
        Equal<Job>(null, driver.GetFinalizerJob(JobCondition.InterruptForced), "failed retrieval does not schedule Wear");
        var native = new NativeWearBoundary { pawn = f.Pawn, job = wear };
        f.Pawn.jobs.curJob = wear; f.Pawn.jobs.curDriver = native;
        f.Adapter.TickRare(); Equal(true, f.Adapter.HasApparel, "active native Wear retains staged garment");
        Equal(true, ((IApparelSource)f.Adapter).RemoveApparel(f.Item), "native apparel-source removal supported");
        Equal(false, f.Adapter.HasApparel, "native wearing consumes temporary buffer");
        Equal(0L, new MapAndNetworkCraftingProductCounter().Count(f.Core, countOrder), "garment removed for wearing is not counted twice");
        Equal<ThingOwner>(null, f.Item.holdingOwner, "source releases object for original apparel tracker");

        f = new Fixture(); driver = f.Start(); driver.RunTestToils(); f.Adapter.TickRare();
        Equal(false, f.Adapter.HasApparel, "abandoned handoff reclaimed");
        Equal(true, f.Unit.Inventory.Contents.Contains(f.Item), "interrupted Wear returns original garment to network");
        Equal(1L, f.Core.Network.GetTotalCount(f.Def), "returned garment indexed once");
        f = new Fixture(); driver = f.Start(); driver.RunTestToils(); f.Core.Settings.allow = false; f.Adapter.TickRare();
        Equal(false, f.Adapter.HasApparel, "filter-rejected return frees adapter");
        Equal(f.Item, f.Map.Ground.Single(), "rejected garment safely drops near adapter");
        f = new Fixture(); driver = f.Start(); driver.RunTestToils(); f.Core.Settings.allow = false; f.Map.DropBudget = 0; f.Adapter.TickRare();
        Equal(1L, f.Pending(), "blocked ground return saved in recovery");
        Equal(false, f.Adapter.HasApparel, "recovery owns item instead of busy adapter");
        f.Map.DropBudget = int.MaxValue; Find.TickManager.TicksGame = 1250; f.Manager.MapComponentTick();
        Equal(0L, f.Pending(), "retry releases blocked return"); Equal(f.Item, f.Map.Ground.Single(), "retry preserves exact garment without duplication");
        f = new Fixture(); driver = f.Start(); driver.RunTestToils(); f.Adapter.DeSpawn();
        Equal(true, f.Unit.Inventory.Contents.Contains(f.Item), "adapter removal returns prepared apparel before despawning");
        Equal(0, f.Manager.ApparelAdapters.Count, "removed adapter disappears from candidate registry");
    }
}
