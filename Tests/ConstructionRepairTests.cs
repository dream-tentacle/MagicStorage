// Exercises the production coordinator and real network reservations/transactions.
// Native breakdown transitions and graphics are controlled boundaries, not game tests.
using System;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class ConstructionRepairTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Construction repair: " + message + "; expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Building_ConstructionSupplyUnit Supplier = new Building_ConstructionSupplyUnit();
        internal MapComponent_StorageNetworks Networks => Map.GetComponent<MapComponent_StorageNetworks>();
        internal MapComponent_ConstructionRepair Repairs => Map.GetComponent<MapComponent_ConstructionRepair>();
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Register(Supplier, 2);
            Networks.EnsureCurrent();
        }
        internal void Register(Thing building, int x)
        {
            building.Map = Map; building.Position = new IntVec3(x, 0); building.Spawned = true; building.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = building };
            if (building is Building native) native.node = node;
            if (building is Building_StorageUnit unit) unit.node = node;
            Networks.Register(node);
        }
        internal Thing Stock(int count)
        {
            var item = new Thing { def = ThingDefOf.ComponentIndustrial, stackCount = count };
            Unit.Inventory.Contents.TryAdd(item, false);
            return item;
        }
        internal Building Broken(int x = 4)
        {
            var building = new Building { Map = Map, Position = new IntVec3(x, 0), Spawned = true, Faction = Faction.OfPlayer,
                def = new ThingDef { category = ThingCategory.Building, building = new BuildingProperties() } };
            building.TestComps.Add(new CompBreakdownable { parent = building });
            Map.GetComponent<BreakdownManager>().brokenDownThings.Add(building);
            return building;
        }
        internal void Tick(int delta)
        { Find.TickManager.TicksGame += delta; Repairs.MapComponentTick(); }
        internal long StockCount => Core.Network.GetTotalCount(ThingDefOf.ComponentIndustrial);
        internal Command_Toggle Toggle => Supplier.GetGizmos().OfType<Command_Toggle>().Single();
    }

    internal static int Run()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(ConstructionRepairPatches).TypeHandle);
        TimingAndConsumption(); ModeAndSave(); OverlapAndReservations(); Eligibility(); Cancellation(); GraphicsFallback(); Diagnostics();
        NativeDispatchBoundary();
        return checks;
    }

    private static void NativeDispatchBoundary()
    {
        var f = new Fixture(); f.Stock(2); var target = f.Broken();
        var pawn = new Pawn { Map = f.Map, Faction = Faction.OfPlayer };
        var giver = new WorkGiver_FixBrokenDownBuilding();
        WorkGiver_FixBrokenDownBuilding.NativeEligibility = true;
        WorkGiver_FixBrokenDownBuilding.CheckCalls = WorkGiver_FixBrokenDownBuilding.JobCalls = 0;
        Equal(true, giver.HasJobOnThing(pawn, target), "native eligibility runs before automatic repair starts");
        Equal(WorkGiver_FixBrokenDownBuilding.NativeJob, giver.JobOnThing(pawn, target), "native job factory runs before automatic repair starts");
        f.Supplier.RefreshSupply();
        foreach (bool forced in new[] { false, true })
        {
            Equal(false, giver.HasJobOnThing(pawn, target, forced), "active automatic repair blocks colonist eligibility");
            Equal<Verse.AI.Job>(null, giver.JobOnThing(pawn, target, forced), "active automatic repair blocks direct job requests");
        }
        Equal(1, WorkGiver_FixBrokenDownBuilding.CheckCalls, "blocked checks do not search for ground components");
        Equal(1, WorkGiver_FixBrokenDownBuilding.JobCalls, "blocked requests do not create hauling repair jobs");
        var unrelated = f.Broken(10);
        Equal(true, giver.HasJobOnThing(pawn, unrelated), "other broken buildings keep native dispatch");
        Equal(WorkGiver_FixBrokenDownBuilding.NativeJob, giver.JobOnThing(pawn, unrelated), "other targets keep native job creation");
        f.Toggle.toggleAction();
        Equal(true, giver.HasJobOnThing(pawn, target), "disabling mode restores native dispatch immediately");
        Equal(WorkGiver_FixBrokenDownBuilding.NativeJob, giver.JobOnThing(pawn, target), "cancelled target can get native repair job");

        // An already-reserved colonist repair prevents automatic repair from starting.
        f.Map.reservationManager.Reserved.Add(target);
        f.Toggle.toggleAction(); f.Supplier.RefreshSupply();
        Equal(false, f.Repairs.IsRepairing(target), "existing colonist reservation is never taken over");
        f.Map.reservationManager.Reserved.Clear(); f.Supplier.RefreshSupply();
        Equal(false, giver.HasJobOnThing(pawn, target), "automatic repair owns target after colonist reservation ends");
        f.Tick(179);
        Equal(false, giver.HasJobOnThing(pawn, target), "dispatch stays blocked until repair completes");
        f.Tick(1);
        Equal(false, f.Repairs.IsRepairing(target), "completion removes target ownership");
        int checksBefore = WorkGiver_FixBrokenDownBuilding.CheckCalls;
        WorkGiver_FixBrokenDownBuilding.NativeEligibility = false;
        try
        {
            Equal(false, giver.HasJobOnThing(pawn, target), "native checks decide whether completed target has work");
            Equal(checksBefore + 1, WorkGiver_FixBrokenDownBuilding.CheckCalls, "completed target reaches native eligibility again");
        }
        finally { WorkGiver_FixBrokenDownBuilding.NativeEligibility = true; }
    }

    private static void TimingAndConsumption()
    {
        var f = new Fixture(); var target = f.Broken(); var comp = target.GetComp<CompBreakdownable>();
        int beams = MoteMaker.Beams;
        f.Supplier.RefreshSupply();
        Equal(0, f.Repairs.ActiveCount, "no materials means no repair");
        Equal(beams, MoteMaker.Beams, "no materials means no laser");
        Thing stock = f.Stock(2);
        f.Supplier.RefreshSupply();
        Equal(1, f.Repairs.ActiveCount, "existing supply scan also starts repair");
        Equal(DefDatabase<ThingDef>.RepairBeam, MoteMaker.Def, "uses separate repair laser definition");
        var beam = MoteMaker.LastBeam;
        Equal(2L, f.StockCount, "laser start does not consume material");
        Equal(1, f.Core.Network.AvailableToWithdraw(stock), "one component is reserved during laser");
        f.Tick(179);
        Equal(true, comp.BrokenDown, "not repaired before three seconds");
        Equal(2L, f.StockCount, "no premature material consumption");
        f.Tick(1);
        Equal(false, comp.BrokenDown, "repaired exactly after 180 ticks");
        Equal(1, comp.Repairs, "native repaired notification called once");
        Equal(1L, f.StockCount, "consumes exactly one ordinary component");
        Equal(true, beam.Destroyed, "completed repair stops beam");
        Equal(0, f.Repairs.ActiveCount, "completed operation removed");
        f.Supplier.RefreshSupply(); f.Tick(180);
        Equal(1L, f.StockCount, "healthy building does not repeatedly consume");
        comp.BrokenDown = true; f.Map.GetComponent<BreakdownManager>().brokenDownThings.Add(target);
        f.Supplier.RefreshSupply(); f.Tick(180);
        Equal(0L, f.StockCount, "a later breakdown consumes a new component");
        Equal(2, comp.Repairs, "later breakdown can be repaired again");
    }

    private static void ModeAndSave()
    {
        var f = new Fixture(); f.Stock(1); f.Broken();
        Equal(true, f.Supplier.RepairMode, "repair mode defaults on");
        Equal(true, f.Toggle.isActive(), "toggle reflects default mode");
        f.Toggle.toggleAction(); f.Supplier.RefreshSupply();
        Equal(false, f.Supplier.RepairMode, "toggle disables repair mode");
        Equal(0, f.Repairs.ActiveCount, "disabled mode starts no repair");
        object saved = null, defaultValue = null;
        try
        {
            Scribe_Values.Boundary = (key, value, fallback) =>
            { if (key == "repairMode") { saved = value; defaultValue = fallback; } return value; };
            f.Supplier.ExposeData();
            Equal(false, saved, "disabled mode exposed to save");
            Equal(true, defaultValue, "missing save field defaults on");
            var loaded = new Building_ConstructionSupplyUnit();
            Scribe_Values.Boundary = (key, value, fallback) => key == "repairMode" ? saved : value;
            loaded.ExposeData();
            Equal(false, loaded.RepairMode, "saved disabled state restored");
        }
        finally { Scribe_Values.Boundary = null; }
        f.Toggle.toggleAction(); f.Supplier.RefreshSupply();
        var beam = MoteMaker.LastBeam;
        Equal(1, f.Repairs.ActiveCount, "re-enabling mode allows repair");
        f.Toggle.toggleAction();
        Equal(0, f.Repairs.ActiveCount, "disabling mode cancels immediately");
        Equal(true, beam.Destroyed, "mode cancellation destroys beam");
        Equal(1L, f.StockCount, "mode cancellation consumes nothing");
    }

    private static void OverlapAndReservations()
    {
        var f = new Fixture(); Thing stock = f.Stock(1); var first = f.Broken(4); var second = f.Broken(6);
        var other = new Building_ConstructionSupplyUnit(); f.Register(other, 3); f.Networks.EnsureCurrent();
        int beams = MoteMaker.Beams;
        f.Supplier.RefreshSupply(); other.RefreshSupply(); f.Supplier.RefreshSupply();
        Equal(1, f.Repairs.ActiveCount, "one component cannot fund multiple concurrent repairs");
        Equal(beams + 1, MoteMaker.Beams, "overlapping scans never duplicate beams");
        Equal(0, f.Core.Network.AvailableToWithdraw(stock), "reserved component unavailable to other consumers");
        Equal(0, f.Core.Network.ReserveOutgoing(new object(), stock, 1), "other tasks cannot claim repair material");
        f.Tick(180);
        Equal(false, first.GetComp<CompBreakdownable>().BrokenDown, "nearest breakdown repaired first");
        Equal(true, second.GetComp<CompBreakdownable>().BrokenDown, "unfunded breakdown remains broken");
        Equal(0L, f.StockCount, "overlap consumes only one component");

        f = new Fixture(); stock = f.Stock(1); f.Broken(); var owner = new object();
        f.Core.Network.ReserveOutgoing(owner, stock, 1);
        f.Supplier.RefreshSupply();
        Equal(0, f.Repairs.ActiveCount, "existing material reservations respected");
        f.Core.Network.ReleaseOutgoing(owner); f.Supplier.RefreshSupply();
        Equal(1, f.Repairs.ActiveCount, "repair starts after material reservation released");
        f.Repairs.MapRemoved();
        Equal(1, f.Core.Network.AvailableToWithdraw(stock), "map removal releases live reservation");
    }

    private static void Eligibility()
    {
        var f = new Fixture(); f.Stock(5); var target = f.Broken();
        Action<string> skipped = reason =>
        { f.Supplier.RefreshSupply(); Equal(0, f.Repairs.ActiveCount, reason); };
        target.forbidden = true; skipped("forbidden target skipped"); target.forbidden = false;
        target.burning = true; skipped("burning target skipped"); target.burning = false;
        target.Faction = new Faction(); skipped("foreign target skipped"); target.Faction = Faction.OfPlayer;
        target.def.building.repairable = false; skipped("non-repairable target skipped"); target.def.building.repairable = true;
        target.Position = new IntVec3(43, 0); skipped("target outside configured radius skipped"); target.Position = new IntVec3(4, 0);
        target.Spawned = false; skipped("despawned target skipped"); target.Spawned = true;
        f.Map.designationManager.Deconstruct.Add(target); skipped("deconstruction target skipped"); f.Map.designationManager.Deconstruct.Clear();
        f.Map.reservationManager.Reserved.Add(target); skipped("colonist-reserved target skipped"); f.Map.reservationManager.Reserved.Clear();
        f.Map.physicalInteractionReservationManager.Reserved.Add(target); skipped("physical interaction target skipped"); f.Map.physicalInteractionReservationManager.Reserved.Clear();
        target.GetComp<CompBreakdownable>().BrokenDown = false; skipped("healthy target skipped even with stale index");
        target.GetComp<CompBreakdownable>().BrokenDown = true;
        target.Position = new IntVec3(42, 0); f.Supplier.RefreshSupply();
        Equal(1, f.Repairs.ActiveCount, "supply radius boundary is inclusive");
        f.Repairs.MapRemoved();
    }

    private static void Cancellation()
    {
        foreach (string reason in new[] { "network", "material", "target", "burning", "reservation", "range", "healthy", "supplier", "faction" })
        {
            var f = new Fixture(); Thing stock = f.Stock(1); var target = f.Broken();
            f.Supplier.RefreshSupply(); var beam = MoteMaker.LastBeam;
            switch (reason)
            {
                case "network": f.Networks.Unregister(f.Core.node); break;
                case "material": stock.Destroy(); break;
                case "target": target.Spawned = false; break;
                case "burning": target.burning = true; break;
                case "reservation": f.Map.reservationManager.Reserved.Add(target); break;
                case "range": target.Position = new IntVec3(100, 0); break;
                case "healthy": target.GetComp<CompBreakdownable>().Notify_Repaired(); break;
                case "supplier": f.Supplier.DeSpawn(); break;
                case "faction": f.Supplier.SetFaction(new Faction()); break;
            }
            f.Tick(180);
            Equal(0, f.Repairs.ActiveCount, reason + " cancels repair");
            Equal(true, beam.Destroyed, reason + " cancels beam");
            Equal(reason == "healthy" ? 1 : 0, target.GetComp<CompBreakdownable>().Repairs, reason + " does not perform automatic repair");
            if (reason != "material")
                Equal(1L, f.Unit.Inventory.GetTotalCount(ThingDefOf.ComponentIndustrial), reason + " preserves physical material even while topology is dirty");
            if (reason != "network" && reason != "material" && reason != "faction" && reason != "supplier")
                Equal(1, f.Core.Network.AvailableToWithdraw(stock), reason + " releases material claim");
        }
    }

    private static void GraphicsFallback()
    {
        ThingDef original = DefDatabase<ThingDef>.RepairBeam;
        try
        {
            DefDatabase<ThingDef>.RepairBeam = null;
            var f = new Fixture(); f.Stock(1); var target = f.Broken();
            f.Supplier.RefreshSupply();
            Equal(DefDatabase<ThingDef>.RepairBeamFallback, MoteMaker.Def, "core-only beam used without Biotech");
            f.Tick(180);
            Equal(false, target.GetComp<CompBreakdownable>().BrokenDown, "repair works without Biotech beam assets");
        }
        finally { DefDatabase<ThingDef>.RepairBeam = original; }
    }

    private static void Diagnostics()
    {
        var f = new Fixture(); var stock = f.Stock(1); var target = f.Broken();
        f.Map.reservationManager.Reserved.Add(target);
        f.Supplier.RefreshSupply();
        int beams = MoteMaker.Beams;
        try
        {
            Prefs.DevMode = false;
            Equal(false, f.Supplier.GetGizmos().OfType<Command_Action>().Any(), "diagnostic gizmo hidden during ordinary play");
            Prefs.DevMode = true;
            f.Supplier.GetGizmos().OfType<Command_Action>().Single().action();
            string report = UnityEngine.GUIUtility.systemCopyBuffer;
            Equal(true, report.Contains("supplyScans=1"), "report records supply entry");
            Equal(true, report.Contains("repairScans=1"), "report records repair scan entry");
            Equal(true, report.Contains("availableComponents=1"), "report shows available ordinary components");
            Equal(true, report.Contains("breakdowns=1"), "report shows indexed breakdown count");
            Equal(true, report.Contains("reserved=True"), "report identifies reservation blocker");
            Equal(true, report.Contains("eligible=False"), "report identifies ineligible target");
            Equal(1L, f.StockCount, "report consumes no material");
            Equal(1, f.Core.Network.AvailableToWithdraw(stock), "report reserves no material");
            Equal(beams, MoteMaker.Beams, "report creates no laser");
            Equal(0, f.Repairs.ActiveCount, "report starts no repair");
        }
        finally { Prefs.DevMode = false; }
    }
}
