using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MagicStorage;
using RimWorld;
using Verse;
using Verse.AI;

internal static class VanillaBillSupplyTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string reason)
    { checks++; if (!Equals(expected, actual)) throw new Exception(reason + ": expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly MapComponent_StorageNetworks Manager = new MapComponent_StorageNetworks();
        internal readonly Building_WorkTable Table;
        internal readonly Pawn Pawn = new Pawn();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly StorageNetwork Network = new StorageNetwork();
        internal readonly Building_ProductionOutputPort Port = new Building_ProductionOutputPort();
        internal readonly Bill_Production Bill = new Bill_Production();
        internal readonly ThingDef Steel = new ThingDef { defName = "Steel" };
        internal Fixture(bool fortified = false)
        {
            Table = fortified ? new Fortified.Building_WorkTableAutonomous() : new Building_WorkTable();
            GenSpawn.AfterSpawn = null;
            Map.Manager = Manager;
            Table.Map = Map; Table.Position = new IntVec3(10,10); Table.Spawned = true;
            Pawn.Map = Map; Pawn.Spawned = true;
            Unit.Map = Map; Unit.Position = new IntVec3(100,100); Unit.Spawned = true; Unit.Network = Network;
            Network.Core = new Building_StorageCore { Map = Map, Spawned = true, Network = Network }; Network.Units.Add(Unit);
            Manager.Networks.Add(Network);
            Port.Map = Map; Port.Position = new IntVec3(9,10); Port.Spawned = true;
            Port.Node = new CompStorageNode { Network = Network };
            Bill.billStack = new BillStack { billGiver = Table }; Bill.ingredientSearchRadius = 2;
        }
        internal Thing Stock(int count, ThingDef def = null)
        {
            var item = new Thing { def = def ?? Steel, stackCount = count, variant = "original qualities" };
            Unit.Inventory.Contents.TryAdd(item,false); return item;
        }
        internal Job Job(params object[] requirements)
        {
            var job = new Job { bill = Bill, targetA = Table };
            for (int i = 0; i < requirements.Length; i += 2)
            { job.targetQueueB.Add((Thing)requirements[i]); job.countQueue.Add((int)requirements[i+1]); }
            return job;
        }
        internal int StockCount => Unit.Inventory.Contents.Sum(t => t.stackCount);
        internal int Total => StockCount + Map.Ground.Where(t => !t.Destroyed).Sum(t => t.stackCount) +
            Manager.Recovery.Sum(b => b.Contents.Sum(t => t.stackCount));
    }

    private static object Call(string name, object[] args)
    {
        try { return typeof(VanillaBillStorage).GetMethod(name,BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,args); }
        catch (TargetInvocationException exception) { throw exception.InnerException; }
    }
    private static Job Attach(Fixture f, Job job)
    { var args = new object[] { f.Pawn, f.Bill, f.Table, job }; Call("AttachPlan",args); return (Job)args[3]; }
    private static bool Start(Fixture f, Job job, bool reserved = true)
    {
        f.Pawn.CurJob = job;
        var args = new object[] { new JobDriver_DoBill { job = job, pawn = f.Pawn }, reserved };
        Call("Prepare",args); return (bool)args[1];
    }
    private static List<Thing> Candidates(Fixture f, List<Thing> ground)
    {
        var begin = new object[] { f.Bill, f.Pawn, f.Table, null }; Call("BeginSearch",begin);
        try
        {
            var add = new object[] { f.Bill, ground, false }; Call("AddCandidates",add); return (List<Thing>)add[1];
        }
        finally { Call("EndSearch",new[] { begin[3], null }); }
    }

    private static void CandidateScopeAndPortRules()
    {
        var f = new Fixture(); var item = f.Stock(30); var ground = new List<Thing> { new Thing() };
        Equal(2,Candidates(f,ground).Count,"network output-port flag exposes its stock");
        Equal(1,ground.Count,"native scratch candidate list remains unchanged");
        Equal(30,f.StockCount,"search does not withdraw or change stackCount");
        Equal(0,f.Network.Claims.Count,"search does not reserve stock");
        f.Network.HasProductionOutputPort = false;
        Equal(1,Candidates(f,ground).Count,"network without output port is not exposed");
        f.Network.HasProductionOutputPort = true;
        f.Port.Position = new IntVec3(30,30);
        f.Bill.ingredientSearchRadius = 0;
        Equal(2,Candidates(f,ground).Count,"zero bill radius does not limit network supply");
        f.Port.Position = new IntVec3(9,10); f.Port.Forbidden = true;
        Equal(2,Candidates(f,ground).Count,"eligibility reads network topology flag rather than querying an individual port");
        f.Port.Forbidden = false; f.Network.CanWork = false;
        Equal(1,Candidates(f,ground).Count,"disconnected network is not exposed");
        f.Network.CanWork = true; f.Bill.Filter = t => false;
        Equal(1,Candidates(f,ground).Count,"bill material filter is respected");
        f.Bill.Filter = t => true; f.Bill.recipe.ingredients[0].filter.AllowsThing = t => false;
        Equal(1,Candidates(f,ground).Count,"recipe's actual-thing filter is respected");
        f.Bill.recipe.ingredients[0].filter.AllowsThing = t => true;
        f.Network.ReserveOutgoing(new object(),item,5);
        Equal(1,Candidates(f,ground).Count,"partly reserved stack is not advertised at its full count");
        f.Network.Claims.Clear();
        var begin = new object[] { f.Bill,f.Pawn,f.Table,null }; Call("BeginSearch",begin);
        var nested = new object[] { new Bill_Autonomous(),f.Pawn,f.Table,null }; Call("BeginSearch",nested);
        var add = new object[] { f.Bill,ground,false }; Call("AddCandidates",add);
        Equal(1,((List<Thing>)add[1]).Count,"unsupported nested search does not inherit outer stock");
        Call("EndSearch",new[] { nested[3],null });
        add = new object[] { f.Bill,ground,false }; Call("AddCandidates",add);
        Equal(2,((List<Thing>)add[1]).Count,"nested finalizer restores outer search");
        var fault = new Exception("native failure");
        Equal(fault,Call("EndSearch",new[] { begin[3],fault }),"native exception remains visible");
        add = new object[] { f.Bill,ground,false }; Call("AddCandidates",add);
        Equal(1,((List<Thing>)add[1]).Count,"exception finalizer leaves no stale search");
    }

    private static void NativeJobHandoff()
    {
        var f = new Fixture(); var stored = f.Stock(20);
        var ground = new Thing { def = f.Steel,stackCount = 4,Spawned = true,Map = f.Map,Position = new IntVec3(5,5) };
        f.Map.Ground.Add(ground);
        var job = f.Job(stored,8,ground,4);
        Equal(job,Attach(f,job),"original DoBill job identity is retained");
        Equal(JobDefOf.DoBill,job.def,"native job definition is retained");
        Equal(1,job.targetQueueB.Count,"only stored inputs leave native hauling queue");
        Equal(ground,job.targetQueueB[0].Thing,"ground ingredient stays in native hauling queue");
        Equal(4,job.countQueue[0],"ground quantity is unchanged");
        Equal(20,f.StockCount,"creating job is read-only");
        var probe = new object[] { new JobDriver_DoBill { job = job,pawn = f.Pawn },true };
        Call("Prepare",probe);
        Equal(true,(bool)probe[1],"preflight checks can validate a future production job");
        Equal(20,f.StockCount,"preflight reservation checks never materialize stock");
        Equal(null,job.placedThings,"preflight check does not register ingredients");
        Equal(false,Start(f,job,false),"failed original reservations do not start supply");
        Equal(20,f.StockCount,"failed workbench reservation leaves inventory untouched");
        Equal(true,Start(f,job),"accepted job receives ingredients");
        Equal(12,f.StockCount,"only requested quantity leaves inventory");
        Equal(8,job.placedThings.Sum(p=>p.Count),"received quantity is native placedThings");
        Equal("original qualities",job.placedThings[0].thing.variant,"real item data survives transfer");
        Equal(1,f.Map.reservationManager.Claims.Count,"delivered material has native reservation");
        Equal(1,f.Map.physicalInteractionReservationManager.Claims.Count,"delivered material is protected against hauling");
        Equal(0,f.Network.Claims.Count,"outgoing storage reservation ends after delivery");
        Equal(24,f.Total,"mixed ground and network materials are conserved");
        Equal(true,Start(f,job),"reentering reservations does not repeat delivery");
        Equal(12,f.StockCount,"second call cannot charge stock twice");
        Call("ClearPlan",new object[] { job });
        Equal(12,f.StockCount,"job cleanup does not refund native ingredients");
    }

    private static void ContentionAndInvalidation()
    {
        var f = new Fixture(); var item = f.Stock(10);
        var first = Attach(f,f.Job(item,6)); var second = Attach(f,f.Job(item,6));
        Equal(true,Start(f,first),"first job can acquire its batch");
        Equal(false,Start(f,second),"stale second plan fails without overdraw");
        Equal(4,f.StockCount,"second job cannot steal first job's materials");
        Equal(10,f.Total,"contending plans conserve real materials");
        foreach (string cause in new[] { "port", "bill", "filter", "map", "blocked", "reservation" })
        {
            f = new Fixture(); item = f.Stock(20); var job = Attach(f,f.Job(item,7));
            if (cause == "port") { f.Port.Spawned = false; f.Network.HasProductionOutputPort = false; }
            if (cause == "bill") f.Bill.suspended = true;
            if (cause == "filter") f.Bill.Filter = t=>false;
            if (cause == "map") f.Network.Core.Map = new Map();
            if (cause == "blocked") foreach (var cell in f.Table.Cells) f.Map.Blocked.Add(cell);
            if (cause == "reservation") f.Map.reservationManager.Claims[item] = new Claim { Worker = new Pawn(),Job = new Job() };
            Equal(false,Start(f,job),cause + " invalidation aborts supply");
            Equal(20,f.StockCount,cause + " invalidation happens before withdrawing");
            Equal(0,f.Map.Ground.Count,cause + " invalidation leaves no partial delivery");
        }
    }

    private static void SplitDeliveryAndRollback()
    {
        foreach (int failAt in new[] { 0,1,2,3 })
        {
            var f = new Fixture(); var item = f.Stock(185); var job = Attach(f,f.Job(item,175));
            int spawns = 0;
            GenSpawn.AfterSpawn = part => { if (++spawns == failAt) throw new InvalidOperationException("spawn callback failed"); };
            bool success = Start(f,job);
            Equal(failAt == 0,success,"only a fully delivered batch commits");
            Equal(185,f.Total,"split delivery and partial callback failures preserve every item");
            Equal(failAt == 0 ? 10 : 185,f.StockCount,"failed split deliveries restore original inventory");
            Equal(0,f.Network.Claims.Count,"callback failure cannot leak storage claims");
            if (success)
            {
                Equal(3,job.placedThings.Count,"overstacked source becomes legal physical stacks");
                Equal(175,job.placedThings.Sum(p=>p.Count),"legal stacks retain requested total");
                Equal(true,job.placedThings.All(p=>p.thing.stackCount<=p.thing.def.stackLimit),"nothing is truncated on spawn");
            }
            else
            {
                Equal(0,job.placedThings?.Count ?? 0,"rollback clears its own placedThings");
                Equal(0,f.Map.reservationManager.Claims.Count,"rollback releases its native item reservations");
                Equal(0,f.Map.physicalInteractionReservationManager.Claims.Count,"rollback releases physical reservations");
            }
        }
        var destroyed = new Fixture(); var source = destroyed.Stock(20); var failedJob = Attach(destroyed,destroyed.Job(source,8));
        GenSpawn.AfterSpawn = part=>part.Destroy();
        Equal(false,Start(destroyed,failedJob),"externally destroyed material aborts supply");
        Equal(12,destroyed.Total,"rollback does not manufacture replacements for consumed items");
        var unavailable = new Fixture(); source = unavailable.Stock(20); failedJob = Attach(unavailable,unavailable.Job(source,8));
        GenSpawn.AfterSpawn = part=> { unavailable.Unit.Spawned=false; throw new Exception("source removed"); };
        Equal(false,Start(unavailable,failedJob),"source loss during delivery is handled");
        Equal(20,unavailable.Total,"unavailable source retains real leftovers in saved recovery ownership");
        Equal(8,unavailable.Manager.Recovery.Sum(b=>b.Contents.Sum(t=>t.stackCount)),"saved recovery retains withdrawn batch");
        var closedPort = new Fixture(); source = closedPort.Stock(20); failedJob = Attach(closedPort,closedPort.Job(source,8));
        GenSpawn.AfterSpawn = part => { closedPort.Port.Spawned = false; closedPort.Network.HasProductionOutputPort = false; };
        Equal(false,Start(closedPort,failedJob),"port lost during final spawn cannot commit delivery");
        Equal(20,closedPort.StockCount,"last-callback invalidation restores the batch");
        var otherPort = new Fixture(); source = otherPort.Stock(20); failedJob = Attach(otherPort,otherPort.Job(source,8));
        otherPort.Port.Spawned = false; // Another port remains; the topology flag stays true.
        Equal(true,Start(otherPort,failedJob),"queued supply is not bound to one particular output port");
        Equal(12,otherPort.StockCount,"remaining output port keeps the network eligible");
        var removedTable = new Fixture(); source = removedTable.Stock(20); failedJob = Attach(removedTable,removedTable.Job(source,8));
        GenSpawn.AfterSpawn = part => { removedTable.Table.Spawned = false; removedTable.Table.Map = null; };
        Equal(false,Start(removedTable,failedJob),"workbench removal during spawn aborts cleanly");
        Equal(20,removedTable.Total,"rollback retains original map after workbench disappears");
        var interrupted = new Fixture(); source = interrupted.Stock(20); failedJob = Attach(interrupted,interrupted.Job(source,8));
        GenSpawn.AfterSpawn = part => interrupted.Pawn.CurJob = new Job();
        Equal(false,Start(interrupted,failedJob),"reentrant task interruption cannot commit an old job's delivery");
        Equal(20,interrupted.StockCount,"interrupted delivery restores surviving real items");
        GenSpawn.AfterSpawn = null;
    }

    private static void SavedPendingPlan()
    {
        var f = new Fixture(); var source = f.Stock(30); var original = Attach(f,f.Job(source,11));
        var saved = new Dictionary<string,object>(); Scribe.mode = LoadSaveMode.Saving; Scribe.Data = saved;
        Call("ExposePlan",new object[] { original }); Call("ClearPlan",new object[] { original });
        var loaded = f.Job(); Scribe.mode = LoadSaveMode.LoadingVars; Scribe.Data = saved;
        Call("ExposePlan",new object[] { loaded }); Scribe.mode = LoadSaveMode.Inactive;
        Equal(30,f.StockCount,"saving and loading a queued plan does not withdraw");
        Equal(true,Start(f,loaded),"pending plan survives a new Job instance after loading");
        Equal(19,f.StockCount,"loaded plan withdraws its exact saved quantity once");
        Equal(11,loaded.placedThings.Sum(p=>p.Count),"handoff leaves ordinary serializable placedThings");
        saved = new Dictionary<string,object>(); Scribe.mode = LoadSaveMode.Saving; Scribe.Data = saved;
        Call("ExposePlan",new object[] { loaded }); Scribe.mode = LoadSaveMode.Inactive;
        Equal(null,saved["MS_productionSupply"],"completed delivery leaves no pending plan to replay");
        var cloneFixture = new Fixture(); source = cloneFixture.Stock(30);
        original = Attach(cloneFixture,cloneFixture.Job(source,9));
        var cloned = original.Clone(); Call("ClonePlan",new object[] { original,cloned });
        Call("ClearPlan",new object[] { original });
        Equal(true,Start(cloneFixture,cloned),"cloned queued job retains an independent supply plan");
        Equal(21,cloneFixture.StockCount,"cloned job supplies its requested materials once");
    }

    public static int Main()
    {
        try
        {
            CandidateScopeAndPortRules(); NativeJobHandoff(); ContentionAndInvalidation(); SplitDeliveryAndRollback(); SavedPendingPlan();
            FortifiedSupply();
            Console.WriteLine("PASS: " + checks + " production supply assertions (actual supply/transfer code; controlled engine boundary).");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }

    private static object FortifiedCall(string name, object[] args)
    {
        try { return typeof(FortifiedCompatibility).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args); }
        catch (TargetInvocationException exception) { throw exception.InnerException; }
    }

    private static Job FortifiedJob(Fixture f, Job temporary)
    {
        // Model the framework's replacement, copying only its native queues/bill.
        Attach(f, temporary);
        var actual = new Job { def = new JobDef { driverClass = typeof(Fortified.JobDriver_DoAutonomousBill) },
            bill = temporary.bill, targetA = temporary.targetA,
            targetQueueB = temporary.targetQueueB, countQueue = temporary.countQueue };
        var args = new object[] { f.Pawn, actual };
        FortifiedCall("AttachPlan", args);
        return (Job)args[1];
    }

    private static bool FortifiedStart(Fixture f, Job job, bool probe = false)
    {
        if (!probe) f.Pawn.CurJob = job;
        var args = new object[] { new Fortified.JobDriver_DoAutonomousBill { pawn = f.Pawn, job = job }, true };
        FortifiedCall("Prepare", args);
        return (bool)args[1];
    }

    private static void FortifiedSupply()
    {
        var f = new Fixture(true);
        Equal(false, VanillaBillStorage.Supports(f.Bill, f.Pawn, f.Table), "unavailable adapter fails closed");
        new FortifiedCompatibility().Apply(new HarmonyLib.Harmony("tests"));
        var stock = f.Stock(30);
        var ground = new Thing { def = f.Steel, stackCount = 4, Spawned = true, Map = f.Map };
        var temporary = f.Job(stock, 8, ground, 4);
        Attach(f, temporary);
        Equal(2, temporary.targetQueueB.Count, "temporary job must retain warehouse ingredient queue");
        var job = FortifiedJob(f, temporary);
        Equal(1, job.targetQueueB.Count, "final job retains ground material hauling");
        Equal(ground, job.targetQueueB[0].Thing, "native hauling target preserved");
        Equal(true, FortifiedStart(f, job, true), "future job preflight succeeds");
        Equal(30, f.StockCount, "preflight does not withdraw");
        Equal(true, FortifiedStart(f, job), "actual task receives stored material");
        var container = ((Fortified.Building_WorkTableAutonomous)f.Table).innerContainer;
        Equal(22, f.StockCount, "warehouse debited exactly once");
        Equal(8, container.Sum(t => t.stackCount), "machine owns real materials");
        Equal(null, job.placedThings, "container delivery does not leave ground ingredients");
        Equal(true, FortifiedStart(f, job), "repeat reservation succeeds without double debit");
        Equal(22, f.StockCount, "repeat reservation cannot debit again");
        foreach (var item in container.ToArray()) item.Destroy();
        Equal(22, f.StockCount, "native finish destroys only delivered material");

        var shortage = new Fixture(true);
        var shortageJob = FortifiedJob(shortage, shortage.Job(shortage.Stock(20), 8));
        shortage.Network.HasProductionOutputPort = false;
        Log.Warnings.Clear();
        Equal(false, FortifiedStart(shortage, shortageJob), "missing port prevents start");
        Equal(true, Log.Warnings.Last().Contains("Ingredients or supply network"), "failed supply records its reason in the log");
        Equal(0, shortage.Map.resourceCounter.Updates, "validation failure skips resource recount");
        Equal(false, FortifiedStart(shortage, shortageJob), "retry cannot bypass failed payment");
        Equal(20, shortage.StockCount, "failed payment leaves warehouse untouched");

        var rollback = new Fixture(true);
        var one = rollback.Stock(20); var two = rollback.Stock(20);
        var rollbackJob = FortifiedJob(rollback, rollback.Job(one, 8, two, 6));
        var destination = ((Fortified.Building_WorkTableAutonomous)rollback.Table).innerContainer;
        var prior = new Thing { def = rollback.Steel, stackCount = 3 };
        destination.TryAdd(prior, false);
        int delivered = 0;
        destination.AfterAdd = item => { if (++delivered == 2) throw new Exception("receiver failed after acceptance"); };
        Equal(false, FortifiedStart(rollback, rollbackJob), "receiver exception prevents start");
        Equal(40, rollback.StockCount, "whole batch is restored after partial delivery");
        Equal(3, destination.Sum(t => t.stackCount), "rollback preserves preexisting materials");
        Equal(0, rollback.Network.Claims.Count, "failed batch releases warehouse reservations");
        destination.AfterAdd = null;
        Equal(true, FortifiedStart(rollback, rollbackJob), "failed plan can retry and pay normally");
        Equal(26, rollback.StockCount, "retry deducts the full batch once");

        var changed = new Fixture(true);
        var changedJob = FortifiedJob(changed, changed.Job(changed.Stock(20), 8, changed.Stock(20), 6));
        var changedContainer = ((Fortified.Building_WorkTableAutonomous)changed.Table).innerContainer;
        changedContainer.AfterAdd = item => changed.Network.HasProductionOutputPort = false;
        Equal(false, FortifiedStart(changed, changedJob), "commit check catches callback invalidation without per-item rescans");
        Equal(40, changed.StockCount, "commit failure restores the full batch");
        Equal(0, changedContainer.Count, "commit failure leaves no unpaid ingredients");
        Equal(1, changed.Map.resourceCounter.Updates, "attempted transfer refreshes resource counts after rollback");
        Equal(true, Log.Warnings.Last().Contains("changed during transfer"), "commit failure is logged");

        var invalidOwner = new Fixture(true);
        var invalidOwnerJob = FortifiedJob(invalidOwner, invalidOwner.Job(invalidOwner.Stock(20), 8));
        ((Fortified.Building_WorkTableAutonomous)invalidOwner.Table).innerContainer = new ThingOwner(invalidOwner.Unit);
        Equal(false, FortifiedStart(invalidOwner, invalidOwnerJob), "supply entry rejects a foreign container owner");
        Equal(20, invalidOwner.StockCount, "foreign container receives no materials");
        Equal(0, invalidOwner.Map.resourceCounter.Updates, "foreign container rejection skips resource recount");
    }
}
