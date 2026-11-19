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
            Port.Node = new CompStorageNode { Network = Network, parent = Port };
            Network.Members.Add(Port.Node);
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

    private static List<Thing> Search(Fixture f, Bill bill, Thing target, List<Thing> local)
    {
        var begin = new object[] { bill, f.Pawn, target, null };
        Call("BeginSearch", begin);
        try
        {
            var args = new object[] { bill, local, false };
            Call("AddCandidates", args);
            return (List<Thing>)args[1];
        }
        finally { Call("EndSearch", new[] { begin[3], null }); }
    }

    private static Job AttachSpecial(Fixture f, Bill bill, Thing target, Job job)
    {
        job.bill = bill;
        job.targetA = target;
        var args = new object[] { f.Pawn, bill, target, job };
        Call("AttachPlan", args);
        return (Job)args[3];
    }

    private static void ProductionDeliveryBeams()
    {
        var f = new Fixture();
        var beamDef = new ThingDef();
        DefDatabase<ThingDef>.Defs["MS_Mote_ConstructionSupplyBeam"] = beamDef;
        MoteMaker.Beams.Clear();
        var far = new Building_ProductionOutputPort { Map = f.Map, Spawned = true, Position = new IntVec3(50,50) };
        f.Network.Members.Insert(0,new CompStorageNode { parent = far, Network = f.Network });
        var other = new StorageNetwork();
        other.Core = new Building_StorageCore { Map = f.Map, Spawned = true, Network = other };
        var port = new Building_ProductionOutputPort { Map = f.Map, Spawned = true, Position = new IntVec3(0,0) };
        other.Members.Add(new CompStorageNode { parent = port, Network = other });
        var unit = new Building_StorageUnit { Map = f.Map, Spawned = true, Network = other };
        other.Units.Add(unit);
        f.Manager.Networks.Add(other);
        var third = new Thing { def = f.Steel, stackCount = 4 };
        unit.Inventory.Contents.TryAdd(third,false);
        var job = Attach(f,f.Job(f.Stock(8),2,f.Stock(5),1,third,3));
        var probe = new object[] { new JobDriver_DoBill { job = job, pawn = f.Pawn }, true };
        Call("Prepare",probe);
        Equal(0,MoteMaker.Beams.Count,"planning and reservation probe produce no beams");
        GenSpawn.AfterSpawn = item => f.Network.HasProductionOutputPort = false;
        Equal(false,Start(f,job),"failed transfer rolls back");
        Equal(0,MoteMaker.Beams.Count,"rollback never plays a beam");
        f.Network.HasProductionOutputPort = true;
        GenSpawn.AfterSpawn = null;
        Equal(true,Start(f,job),"retry delivers all network contributions");
        Equal(2,MoteMaker.Beams.Count,"one beam per contributing network, not per stack");
        Equal(true,MoteMaker.Beams.Any(b => b.Item2 == f.Port),"nearest port selected over farther port");
        Equal(true,MoteMaker.Beams.Any(b => b.Item2 == port),"second contributing network emits its own beam");
        Equal(true,MoteMaker.Beams.All(b => b.Item1 == beamDef && b.Item3 == f.Table),"construction supply visual targets bill giver");
        Equal(true,Start(f,job),"completed job can reenter reservations");
        Equal(2,MoteMaker.Beams.Count,"completed delivery never replays beams");
        DefDatabase<ThingDef>.Defs.Clear();
        var fallback = new ThingDef();
        DefDatabase<ThingDef>.Defs["MS_Mote_ProductionSupplyBeamFallback"] = fallback;
        job = Attach(f,f.Job(f.Stock(3),1));
        Equal(true,Start(f,job),"core-only delivery succeeds");
        Equal(fallback,MoteMaker.Beams.Last().Item1,"core-only delivery uses fallback beam");
        DefDatabase<ThingDef>.Defs.Clear();
    }

    private static void MedicalSupply()
    {
        var f = new Fixture();
        var patient = new Pawn { Map = f.Map, Spawned = true, Position = f.Table.Position,
            Faction = null, MaxMedicinePotency = 2 };
        var bill = new Bill_Medical { billStack = new BillStack { billGiver = patient } };
        bill.recipe.IsSurgery = true;
        bill.recipe.workerClass = typeof(Recipe_InstallImplant);
        var implant = f.Stock(1);
        var weak = new Thing { def = new ThingDef { IsMedicine = true, Potency = 1 }, Spawned = true, Map = f.Map };
        var stronger = f.Stock(3, new ThingDef { IsMedicine = true, Potency = 2 });
        var forbidden = f.Stock(3, new ThingDef { IsMedicine = true, Potency = 3 });
        var local = new List<Thing> { weak };
        var candidates = Search(f, bill, patient, local);
        Equal(3, candidates.Count, "includes allowed stored medicine and implant with local medicine");
        Equal(stronger, candidates[0], "allowed medicine ordered by potency");
        Equal(false, candidates.Contains(forbidden), "patient's medical care excludes stronger medicine");
        Equal(1, local.Count, "medical search leaves native scratch list alone");

        var mixed = AttachSpecial(f, bill, patient, f.Job(weak, 1, implant, 1));
        Equal(weak, mixed.targetQueueB.Single().Thing, "native medicine remains in the job");
        var cancelled = AttachSpecial(f, bill, patient, f.Job(implant, 1));
        Call("ClearPlan", new object[] { cancelled });
        Equal(7, f.StockCount, "discarded medical job did not withdraw");
        var competitor = AttachSpecial(f, bill, patient, f.Job(implant, 1));
        Equal(true, Start(f, mixed), "operation receives stored implant");
        Equal(implant, mixed.placedThings.Single().thing, "specific implant arrives at patient's ingredient cell");
        var otherDoctor = new Pawn { Map = f.Map, Spawned = true, CurJob = competitor };
        var competingStart = new object[] { new JobDriver_DoBill { pawn = otherDoctor, job = competitor }, true };
        Call("Prepare", competingStart);
        Equal(false, (bool)competingStart[1], "second doctor cannot spend the same implant");
        var drugJob = AttachSpecial(f, bill, patient, f.Job(stronger, 1));
        patient.MaxMedicinePotency = 1;
        Equal(false, Start(f, drugJob), "changed care restriction invalidates stored medicine before withdrawal");
        Equal(6, f.StockCount, "medicine remains available after rejection");

        patient.MaxMedicinePotency = 2;
        GenSpawn.AfterSpawn = t => patient.MaxMedicinePotency = 1;
        Equal(false, Start(f, drugJob), "care changed during transfer aborts delivery");
        Equal(6, f.StockCount, "aborted medicine is returned to storage");
        GenSpawn.AfterSpawn = null;
        bill.recipe.IsSurgery = false;
        Equal(false, VanillaBillStorage.Supports(bill, f.Pawn, patient), "non-surgical recipe excluded");
        bill.recipe.IsSurgery = true;
        patient.Dead = true;
        bill.recipe.modContentPack.IsOfficialMod = true;
        Equal(false, VanillaBillStorage.Supports(bill, f.Pawn, patient), "dead target excluded");
    }

    private sealed class ThirdPartyMedicalBill : Bill_Medical { }

    private static void ThirdPartySurgery()
    {
        var f = new Fixture();
        var patient = new Pawn { Map = f.Map, Spawned = true, Position = f.Table.Position };
        var bill = new ThirdPartyMedicalBill { billStack = new BillStack { billGiver = patient } };
        bill.recipe.IsSurgery = true;
        bill.recipe.allowMixingIngredients = true;
        bill.recipe.modContentPack.IsOfficialMod = false;
        var medicineA = f.Stock(3, new ThingDef { IsMedicine = true, Potency = 1 });
        var medicineB = f.Stock(2, new ThingDef { IsMedicine = true, Potency = 2 });
        var named = f.Stock(1);
        var germ = new Thing { Spawned = true, Map = f.Map };
        bill.uniqueRequiredIngredients = new List<Thing> { named };
        bill.xenogerm = germ;
        var candidates = Search(f, bill, patient, new List<Thing>());
        Equal(2, candidates.Count, "third-party medical bill can choose both medicine types");
        Equal(false, candidates.Contains(named), "named object is not offered as generic network stock");
        var nativeOnly = AttachSpecial(f, bill, patient, f.Job(named, 1));
        Equal(named, nativeOnly.targetQueueB.Single().Thing, "named-only job remains intact");
        var job = AttachSpecial(f, bill, patient, f.Job(medicineA, 2, named, 1, medicineB, 1, germ, 1));
        Equal(named, job.targetQueueB[0].Thing, "named item remains in native target queue");
        Equal(germ, job.targetQueueB[1].Thing, "xenogerm remains in native target queue");
        Equal(true, job.countQueue.SequenceEqual(new[] { 1, 1 }), "preserves quantities of exact targets");
        Equal(true, Start(f, job), "accepts mixed materials selected by native recipe solver");
        Equal(3, job.placedThings.Sum(t => t.Count), "only ordinary recipe ingredients withdrawn");
        Equal(true, f.Unit.Inventory.Contents.Contains(named), "named item was not debited or replaced");
    }

    private static void SurgeryMenuSupply()
    {
        var f = new Fixture();
        var patient = new Pawn { Map = f.Map, Spawned = true, MaxMedicinePotency = 1 };
        var recipe = new RecipeDef { IsSurgery = true, workerClass = typeof(Recipe_InstallImplant) };
        var eye = new ThingDef { defName = "BionicEye" };
        var herb = new ThingDef { defName = "Herbal", IsMedicine = true, Potency = 1 };
        var glitter = new ThingDef { defName = "Glitter", IsMedicine = true, Potency = 3 };
        recipe.ingredients[0].IsFixedIngredient = true;
        recipe.ingredients[0].filter.AllowedThingDefs.Add(eye);
        var meds = new IngredientCount();
        meds.filter.AllowedThingDefs.AddRange(new[] { herb, glitter });
        recipe.ingredients.Add(meds);
        var storedEye = f.Stock(1, eye);
        f.Stock(2, glitter);

        MethodInfo Patch(string name) => typeof(SurgeryMenuStorage).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        bool HasStockMedicine()
        {
            object[] args = { patient, recipe, false };
            Patch("IncludeMedicine").Invoke(null, args);
            return (bool)args[2];
        }

        Equal(0, Missing(recipe, f.Map).Count, "direct third-party menu query sees implant and medicine without patient context");
        Equal(false, HasStockMedicine(), "banned stock medicine does not clear the warning");
        var medical = new Bill_Medical { recipe = recipe, billStack = new BillStack { billGiver = patient } };
        Equal(storedEye, Search(f, medical, patient, new List<Thing>()).Single(),
            "menu availability does not bypass patient medicine restriction during actual selection");
        f.Map.Ground.Add(new Thing { def = herb, Spawned = true, Map = f.Map });
        Equal(0, Missing(recipe, f.Map).Count, "local medicine plus stored eye enables surgery");
        f.Map.Ground.Clear();
        f.Stock(2, herb);
        Equal(0, Missing(recipe, f.Map).Count, "all ingredients can be in storage");
        Equal(true, HasStockMedicine(), "permitted network medicine clears the warning");
        f.Network.HasProductionOutputPort = false;
        Equal(2, Missing(recipe, f.Map).Count, "disconnected port cannot satisfy missing ingredients");
        Equal(false, HasStockMedicine(), "disconnected port cannot clear warning");
        f.Network.HasProductionOutputPort = true;
        storedEye.Forbidden = true;
        Equal(eye, Missing(recipe, f.Map).Single(), "forbidden implant stays unavailable");
        storedEye.Forbidden = false;
        f.Unit.Forbidden = true;
        Equal(2, Missing(recipe, f.Map).Count, "forbidden unit exposes neither implant nor medicine");
        f.Unit.Forbidden = false;
        recipe.workerClass = typeof(Recipe_Surgery);
        recipe.modContentPack.IsOfficialMod = false;
        recipe.allowMixingIngredients = true;
        Equal(0, Missing(recipe, f.Map).Count, "third-party mixed surgery sees stored implant in menu");
        recipe.IsSurgery = false;
        Equal(0, Missing(recipe, f.Map).Count, "non-surgery queries also include available stock");
        Equal(5, f.StockCount, "opening menus never withdraws items");
        Equal(0, f.Network.Claims.Count, "opening menus does not reserve items");
    }

    private static List<ThingDef> Missing(RecipeDef recipe, Map map, Pawn worker = null)
    {
        object[] args = { recipe, worker, map, recipe.PotentiallyMissingIngredients(worker, map) };
        typeof(SurgeryMenuStorage).GetMethod("IncludeStock", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        return ((IEnumerable<ThingDef>)args[3]).ToList();
    }

    private static void GeneralMissingIngredients()
    {
        var f = new Fixture();
        var recipe = f.Bill.recipe;
        var ingredient = recipe.ingredients[0];
        ingredient.filter.AllowedThingDefs.Add(f.Steel);
        Equal(f.Steel, Missing(recipe, f.Map).Single(), "absent material remains missing");
        var item = f.Stock(1);
        Equal(0, Missing(recipe, f.Map).Count, "quest-style query sees stored material without a menu or worker");
        Equal(0, Missing(recipe, f.Map, f.Pawn).Count, "worker query includes eligible stock");
        var other = new Fixture();
        Equal(f.Steel, Missing(recipe, other.Map).Single(), "stock never crosses maps");
        Equal(f.Steel, Missing(recipe, other.Map, f.Pawn).Single(), "worker on another map does not supply stock");
        f.Network.CanWork = false;
        Equal(f.Steel, Missing(recipe, f.Map).Single(), "inactive network remains unavailable");
        f.Network.CanWork = true;
        f.Map.Fog.Add(f.Unit.Position);
        Equal(f.Steel, Missing(recipe, f.Map).Single(), "fogged storage remains unavailable");
        f.Map.Fog.Clear();
        f.Unit.Faction = null;
        Equal(f.Steel, Missing(recipe, f.Map).Single(), "foreign storage remains unavailable");
        f.Unit.Faction = Faction.OfPlayer;
        f.Network.ReserveOutgoing(new object(), item, 1);
        Equal(f.Steel, Missing(recipe, f.Map).Single(), "claimed stock remains unavailable");
        f.Network.Claims.Clear();
        f.Map.reservationManager.Claims[item] = new Claim { Worker = new Pawn() };
        Equal(f.Steel, Missing(recipe, f.Map, f.Pawn).Single(), "worker cannot advertise another worker's reserved stock");
        f.Map.reservationManager.Claims.Clear();
        recipe.fixedIngredientFilter.AllowsThing = t => false;
        Equal(f.Steel, Missing(recipe, f.Map).Single(), "recipe fixed filter checks actual stored items");
        ingredient.IsFixedIngredient = true;
        Equal(0, Missing(recipe, f.Map).Count, "fixed ingredient bypasses recipe fixed filter like vanilla");
        ingredient.IsFixedIngredient = false;
        recipe.fixedIngredientFilter.AllowsThing = t => true;
        ingredient.filter.AllowsThing = t => t.variant == "required quality";
        Equal(f.Steel, Missing(recipe, f.Map).Single(), "instance restrictions cannot be satisfied by matching def alone");
        item.variant = "required quality";
        Equal(0, Missing(recipe, f.Map).Count, "matching instance satisfies ingredient");
        var ground = new Thing { def = f.Steel, Map = f.Map, Spawned = true, variant = item.variant };
        f.Map.Ground.Add(ground);
        var unsatisfied = new IngredientCount { IsFixedIngredient = true };
        unsatisfied.filter.AllowedThingDefs.Add(f.Steel);
        unsatisfied.filter.AllowsThing = t => t.variant == "different quality";
        recipe.ingredients.Add(unsatisfied);
        Equal(f.Steel, Missing(recipe, f.Map).Single(), "ground-satisfied group cannot clear another group's identical missing def");
        ground.Forbidden = true;
        Equal(2, recipe.PotentiallyMissingIngredients(f.Pawn, f.Map).Count(), "worker query ignores forbidden ground stack for both groups");
        Equal(f.Steel, Missing(recipe, f.Map, f.Pawn).Single(), "storage removes exactly one of two identical missing defs");
        Equal(1, item.stackCount, "queries never change stored quantity");
        Equal(true, f.Unit.Inventory.Contents.Contains(item), "queries never extract the stored item");
        Equal(0, f.Network.Claims.Count, "queries do not claim stock");
        Equal(1, f.Map.Ground.Count, "queries do not spawn substitute ingredients");

        var extra = new ThingDef { defName = "OtherModMissingRequirement" };
        object[] args = { recipe, null, f.Map, new[] { f.Steel, extra } };
        typeof(SurgeryMenuStorage).GetMethod("IncludeStock", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        Equal(true, ((IEnumerable<ThingDef>)args[3]).SequenceEqual(new[] { f.Steel, extra }),
            "unresolved groups and additional missing results from other patches are retained");
        f.Unit.Inventory.Contents.Take(item, 1);
        Equal(2, Missing(recipe, f.Map, f.Pawn).Count, "next query observes withdrawal instead of reusing stale stock");
    }

    private sealed class ThirdPartyGestator : Building_MechGestator { }

    private sealed class ThirdPartyManufacture : Bill_ProductionMech { }

    private static void GestatorSupply()
    {
        var f = new Fixture();
        var machine = new Building_MechGestator { Map = f.Map, Spawned = true, Position = f.Table.Position };
        var bill = new Bill_ProductionMech { billStack = new BillStack { billGiver = machine } };
        var inside = new Thing { def = f.Steel, stackCount = 4 };
        machine.innerContainer.TryAdd(inside, false);
        var stock = f.Stock(20);
        var local = new List<Thing> { inside };
        var candidates = Search(f, bill, machine, local);
        Equal(inside, candidates[0], "gestator offers inside materials first");
        Equal(stock, candidates[1], "available deficit may be supplied by storage");
        var job = AttachSpecial(f, bill, machine, f.Job(inside, 4, stock, 6));
        Equal(inside, job.targetQueueB.Single().Thing, "preexisting ingredients stay in native queue");
        inside.stackCount++;
        Equal(false, Start(f, job), "changed gestator contents invalidate queued deficit");
        Equal(20, f.StockCount, "stale deficit did not withdraw from network");
        inside.stackCount--;
        Equal(true, Start(f, job), "gathering job accepts six missing units");
        Equal(14, f.StockCount, "only missing units withdrawn");
        Equal(null, job.placedThings, "network materials not marked as delivered before hauling");
        Equal(true, job.targetQueueB[1].Thing.Spawned, "native driver receives a real stack to haul inside");
        Equal(6, job.countQueue[1], "quantity passed to native driver");
        Equal(true, Start(f, job), "second reservation can reuse delivered stack");
        Equal(14, f.StockCount, "no duplicate withdrawal");
        var pending = AttachSpecial(f, bill, machine, f.Job(stock, 2));
        bill.State = FormingState.Forming;
        Equal(false, Start(f, pending), "formed job does not withdraw");
        Equal(14, f.StockCount, "no charge outside gathering");
        bill.State = FormingState.Gathering;
        machine.def.defName = "LargeMechGestator";
        Equal(true, VanillaBillStorage.Supports(bill, f.Pawn, machine), "large vanilla gestator included");
        var resurrection = new Bill_ResurrectMech { billStack = bill.billStack };
        Equal(false, VanillaBillStorage.Supports(resurrection, f.Pawn, machine), "resurrection excluded");
        machine.def.modContentPack.IsOfficialMod = false;
        machine.def.defName = "OtherMod_Gestator";
        bill.recipe.modContentPack.IsOfficialMod = false;
        Equal(true, VanillaBillStorage.Supports(bill, f.Pawn, machine), "third-party def reusing vanilla gestator class");

        var derived = new ThirdPartyGestator { Map = f.Map, Spawned = true, Position = f.Table.Position };
        derived.def.modContentPack.IsOfficialMod = false;
        var derivedBill = new ThirdPartyManufacture { billStack = new BillStack { billGiver = derived } };
        derivedBill.recipe.modContentPack.IsOfficialMod = false;
        Equal(stock, Search(f, derivedBill, derived, new List<Thing>()).Single(), "derived gestator and bill find network stock");
        var derivedJob = AttachSpecial(f, derivedBill, derived, f.Job(stock, 3));
        Equal(true, Start(f, derivedJob), "derived manufacture starts native ingredient hauling");
        Equal(true, derivedJob.targetQueueB.Single().Thing.Spawned, "derived gestator receives physical ingredient target");
        Equal(3, derivedJob.countQueue.Single(), "native queue gets requested quantity");
        var cosmic = new Building_CosmicMechGestator { Map = f.Map, Spawned = true };
        bill.billStack.billGiver = cosmic;
        Equal(false, VanillaBillStorage.Supports(bill, f.Pawn, cosmic), "cosmic gestator keeps its own supply path");
    }

    public static int Main()
    {
        try
        {
            CandidateScopeAndPortRules(); NativeJobHandoff(); ContentionAndInvalidation(); SplitDeliveryAndRollback(); SavedPendingPlan();
            FortifiedSupply(); ProductionDeliveryBeams(); MedicalSupply(); ThirdPartySurgery(); SurgeryMenuSupply(); GeneralMissingIngredients(); GestatorSupply();
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
