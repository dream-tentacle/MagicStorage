using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class ConstructionSupplyTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Construction supply: " + message + "; expected " + expected + ", got " + actual); }
    internal static int Run()
    {
        RangeAndScan(); ReservationsAndOverlap(); BlueprintTransition(); Eligibility(); NetworkAndPartialSupply();
        DeliveryBeam();
        return checks;
    }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Building_ConstructionSupplyUnit Supplier = new Building_ConstructionSupplyUnit();
        internal readonly ThingDef Steel = new ThingDef(), Wood = new ThingDef();
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Register(Supplier, 2);
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
        }
        internal void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building building) building.node = node;
            if (thing is Building_StorageUnit unit) unit.node = node;
            Map.GetComponent<MapComponent_StorageNetworks>().Register(node);
        }
        internal Thing Stock(ThingDef def, int count)
        { var item = new Thing { def = def, stackCount = count }; Unit.Inventory.Contents.TryAdd(item, false); return item; }
        internal Frame Site(int x, int z, int steel = 10, int wood = 0)
        {
            var frame = new Frame { def = new ThingDef { entityDefToBuild = new ThingDef { category = ThingCategory.Building }, IsFrame = true } };
            if (steel > 0) frame.Costs.Add(new ThingDefCountClass { thingDef = Steel, count = steel });
            if (wood > 0) frame.Costs.Add(new ThingDefCountClass { thingDef = Wood, count = wood });
            Spawn(frame, x, z); return frame;
        }
        internal void Spawn(Thing site, int x, int z)
        { site.Map = Map; site.Position = new IntVec3(x, z); site.Spawned = true; site.Faction = Faction.OfPlayer; Map.listerThings.Things.Add(site); }
        internal Blueprint_Storage Blueprint(int x = 4)
        {
            var blueprint = new Blueprint_Storage { def = new ThingDef { entityDefToBuild = new ThingDef { category = ThingCategory.Building }, IsBlueprint = true } };
            blueprint.Costs.Add(new ThingDefCountClass { thingDef = Steel, count = 10 });
            Spawn(blueprint, x, 0); return blueprint;
        }
        internal int Stored(Frame frame, ThingDef def) => frame.resourceContainer.Where(t => t.def == def).Sum(t => t.stackCount);
        internal long Remaining(ThingDef def) => Core.Network.GetTotalCount(def);
    }

    private static void RangeAndScan()
    {
        var f = new Fixture(); f.Stock(f.Steel, 75); f.Stock(f.Wood, 20);
        var boundary = f.Site(42, 0, 10, 5); // 40 cells from unit at (2,0).
        var inside = f.Site(26, 32, 10, 5); // Exactly 40 cells on a diagonal.
        var outside = f.Site(42, 1); var otherSide = f.Site(-38, 0);
        f.Supplier.TickRare();
        Equal(10, f.Stored(boundary, f.Steel), "inclusive radius at 40 cells");
        Equal(10, f.Stored(inside, f.Steel), "circular diagonal boundary");
        Equal(5, f.Stored(inside, f.Wood), "one cycle supplies multiple materials");
        Equal(10, f.Stored(otherSide, f.Steel), "one cycle supplies every site in range");
        Equal(0, f.Stored(outside, f.Steel), "outside circle is excluded");
        Equal(0f, inside.workDone, "supply does not perform construction work");
        Equal(45L, f.Remaining(f.Steel), "only delivered steel leaves storage");
        f.Supplier.TickRare();
        Equal(45L, f.Remaining(f.Steel), "completed material lists are not supplied twice");
        f.Supplier.DrawExtraSelectionOverlays();
        Equal(40f, GenDraw.Radius, "selection overlay matches supply radius");
        new PlaceWorker_ConstructionSupply().DrawGhost(new ThingDef(), new IntVec3(), new Rot4(), new UnityEngine.Color());
        Equal(40f, GenDraw.Radius, "placement overlay matches supply radius");

        f = new Fixture(); f.Stock(f.Steel, 12);
        var farther = f.Site(20, 0); var nearer = f.Site(4, 0);
        f.Supplier.TickRare();
        Equal(10, f.Stored(nearer, f.Steel), "nearer site receives materials first regardless of list order");
        Equal(2, f.Stored(farther, f.Steel), "remaining stock reaches next site in same cycle");
    }

    private static void ReservationsAndOverlap()
    {
        var f = new Fixture(); var stock = f.Stock(f.Steel, 20); var site = f.Site(4, 0);
        var claim = new object();
        Equal(16, f.Core.Network.ReserveOutgoing(claim, stock, 16), "other consumer reserves real material");
        Equal(4, ConstructionSupply.DeliverTo(f.Supplier, site), "supply only takes unreserved remainder");
        Equal(true, f.Core.Network.HasOutgoingReservation(claim, stock, 16), "other consumer claim remains valid");
        f.Core.Network.ReleaseOutgoing(claim);
        f.Map.enrouteManager.Claims[site] = new Dictionary<ThingDef, int> { [f.Steel] = 3 };
        Equal(3, ConstructionSupply.DeliverTo(f.Supplier, site), "subtracts existing contents and in-flight materials");
        Equal(7, f.Stored(site, f.Steel), "leaves exact room for hauler");
        var second = new Building_ConstructionSupplyUnit(); f.Register(second, 3);
        second.TickRare();
        Equal(7, f.Stored(site, f.Steel), "overlapping supplier does not duplicate in-flight material");
        f.Map.enrouteManager.Claims.Remove(site);
        second.TickRare();
        Equal(10, f.Stored(site, f.Steel), "cancelled hauling claim allows next cycle to fill deficit");
        Equal(10L, f.Remaining(f.Steel), "overlap preserves total inventory");
    }

    private static void BlueprintTransition()
    {
        var f = new Fixture(); var blueprint = f.Blueprint();
        f.Supplier.TickRare();
        Equal(false, blueprint.Destroyed, "no stock leaves blueprint intact");
        Equal(0, blueprint.FactoryCalls, "no stock does not create empty frame");
        f.Stock(f.Steel, 20);
        var style = new object(); var settings = new object(); var group = new object(); var glower = new object();
        blueprint.Style = style; blueprint.Settings = settings; blueprint.Group = group; blueprint.Glower = glower;
        blueprint.Stuff = f.Wood; blueprint.Rotation = new Rot4 { value = 2 }; blueprint.Graphic = new Graphic_Random();
        Find.Selector.Selected = blueprint;
        var pawn = new Pawn(); f.Map.mapPawns.AllPawnsSpawned.Add(pawn);
        f.Map.enrouteManager.Claims[blueprint] = new Dictionary<ThingDef, int> { [f.Steel] = 3 };
        f.Supplier.TickRare();
        var frame = blueprint.Made;
        Equal(true, blueprint.Destroyed, "first usable delivery replaces blueprint");
        Equal(true, frame.Spawned, "frame spawned through native boundary");
        Equal(7, f.Stored(frame, f.Steel), "blueprint delivery respects transferred enroute claims");
        Equal(1, blueprint.FactoryCalls, "virtual native factory called once");
        Equal(settings, frame.Settings, "storage blueprint override preserves settings");
        Equal(group, frame.Group, "storage blueprint override preserves group");
        Equal(style, frame.Style, "style retained"); Equal(glower, frame.Glower, "glower retained");
        Equal(f.Wood, frame.Stuff, "building stuff retained");
        Equal(2, frame.Rotation.value, "rotation retained");
        Equal((int?)blueprint.thingIDNumber, frame.overrideGraphicIndex, "random graphic identity retained");
        Equal(Faction.OfPlayer, frame.Faction, "automated frame retains faction without fake worker");
        Equal(frame, Find.Selector.Selected, "selection follows replacement");
        Equal(frame, pawn.pather.New, "walking pawns notified about transformed target");
        Equal(1, f.Map.planManager.Plan.Removed, "plan marking removed on occupied cell");
        Equal(3, f.Map.enrouteManager.GetEnroute(frame, f.Steel), "in-flight claim belongs to frame");
        Equal(0f, frame.workDone, "new frame still requires native construction");
        f.Supplier.TickRare();
        Equal(13L, f.Remaining(f.Steel), "repeated scan does not redeliver material");
        Equal(1, blueprint.FactoryCalls, "repeated scan does not recreate blueprint");
    }

    private static void Eligibility()
    {
        var f = new Fixture(); f.Stock(f.Steel, 75); var site = f.Site(4, 0);
        site.forbidden = true; Equal(0, ConstructionSupply.DeliverTo(f.Supplier, site), "forbidden site skipped");
        site.forbidden = false; site.burning = true; Equal(0, ConstructionSupply.DeliverTo(f.Supplier, site), "burning site skipped");
        site.burning = false; site.Faction = new Faction(); Equal(0, ConstructionSupply.DeliverTo(f.Supplier, site), "foreign site skipped");
        site.Faction = Faction.OfPlayer; GenConstruct.Blockers[site] = new Thing();
        Equal(0, ConstructionSupply.DeliverTo(f.Supplier, site), "obstructed site skipped"); GenConstruct.Blockers.Remove(site);
        site.Spawned = false; Equal(0, ConstructionSupply.DeliverTo(f.Supplier, site), "removed site skipped"); site.Spawned = true;
        site.Map = new Map(); Equal(0, ConstructionSupply.DeliverTo(f.Supplier, site), "other map skipped"); site.Map = f.Map;
        var install = new Blueprint_Install(); f.Spawn(install, 5, 0);
        Equal(0, ConstructionSupply.DeliverTo(f.Supplier, install), "reinstallation does not consume building materials");
        var floor = f.Blueprint(6); floor.def.entityDefToBuild = new TerrainDef();
        f.Map.terrainGrid.Removable.Add(floor.Position);
        Equal(0, ConstructionSupply.DeliverTo(f.Supplier, floor), "floor replacement waits for old floor removal");
        Equal(false, floor.Destroyed, "waiting floor stays blueprint");
        f.Map.terrainGrid.Removable.Clear();
        Equal(10, ConstructionSupply.DeliverTo(f.Supplier, floor), "cleared terrain blueprint may receive materials");
        var attachment = f.Site(7, 0); ((ThingDef)attachment.def.entityDefToBuild).building = new BuildingProperties { isAttachment = true };
        Equal(0, ConstructionSupply.DeliverTo(f.Supplier, attachment), "attachment waits for supporting wall");
        GenConstruct.Walls[attachment] = new Thing { def = new ThingDef { IsFrame = true } };
        Equal(0, ConstructionSupply.DeliverTo(f.Supplier, attachment), "unfinished supporting wall is insufficient");
        GenConstruct.Walls[attachment].def.IsFrame = false;
        Equal(10, ConstructionSupply.DeliverTo(f.Supplier, attachment), "finished supporting wall allows supply");
        Equal(10, ConstructionSupply.DeliverTo(f.Supplier, site), "cleared eligible site resumes supply");
    }

    private static void DeliveryBeam()
    {
        var f = new Fixture(); f.Stock(f.Steel, 3); f.Stock(f.Steel, 10); f.Stock(f.Wood, 5);
        var site = f.Site(6, 0, 10, 5); site.width=3; site.height=2;
        MoteMaker.Beams=0;
        Equal(15, ConstructionSupply.DeliverTo(f.Supplier, site), "beam follows successful multi-material transfer");
        Equal(1, MoteMaker.Beams, "multiple materials and source stacks generate a single beam per site");
        Equal(DefDatabase<ThingDef>.SupplyBeam, MoteMaker.Def, "uses the graser visual definition");
        Equal(f.Supplier.Position, MoteMaker.Source.Cell, "beam starts at supplier");
        Equal(site.Position, MoteMaker.Destination.Cell, "beam ends at construction site");
        Equal(f.Map, MoteMaker.Destination.Map, "beam stays on delivery map");
        Equal(1f, MoteMaker.DestinationOffset.x, "beam targets center of wide building");
        Equal(0.5f, MoteMaker.DestinationOffset.z, "beam targets center of tall building");
        Equal(0, ConstructionSupply.DeliverTo(f.Supplier, site), "filled site has no additional transfer");
        Equal(1, MoteMaker.Beams, "filled site does not generate misleading beam");

        var blueprint = f.Blueprint(8); blueprint.width=2;
        Equal(3, ConstructionSupply.DeliverTo(f.Supplier, blueprint), "partial blueprint delivery can display beam");
        Equal(2, MoteMaker.Beams, "partial delivery produces one beam");
        Equal(blueprint.Position, MoteMaker.Destination.Cell, "blueprint transformation retains beam endpoint");
        Equal(0.5f, MoteMaker.DestinationOffset.x, "blueprint center captured before destruction");
        Equal(0, ConstructionSupply.DeliverTo(f.Supplier, blueprint.Made), "empty network cannot deliver");
        Equal(2, MoteMaker.Beams, "no stock produces no beam");

        f.Stock(f.Steel, 10); var forbidden=f.Site(9, 0); forbidden.forbidden=true;
        Equal(0, ConstructionSupply.DeliverTo(f.Supplier, forbidden), "forbidden target is not supplied");
        Equal(2, MoteMaker.Beams, "forbidden target produces no beam");
        var beamDef = DefDatabase<ThingDef>.SupplyBeam;
        try
        {
            DefDatabase<ThingDef>.SupplyBeam=null;
            Equal(7, ConstructionSupply.DeliverTo(f.Supplier, blueprint.Made), "missing DLC visual does not block material supply");
            Equal(2, MoteMaker.Beams, "missing visual does not spawn an invalid mote");
        }
        finally { DefDatabase<ThingDef>.SupplyBeam=beamDef; }
    }

    private static void NetworkAndPartialSupply()
    {
        var f = new Fixture(); f.Stock(f.Steel, 3); var site = f.Site(5, 0, 10, 5);
        f.Supplier.TickRare(); Equal(3, f.Stored(site, f.Steel), "partial supply retains actual materials in frame");
        f.Stock(f.Steel, 10); f.Stock(f.Wood, 5);
        var manager = f.Map.GetComponent<MapComponent_StorageNetworks>();
        manager.Unregister(f.Supplier.node); f.Supplier.TickRare();
        Equal(3, f.Stored(site, f.Steel), "disconnected supplier stops");
        manager.Register(f.Supplier.node); f.Supplier.TickRare();
        Equal(10, f.Stored(site, f.Steel), "reconnection supplies missing remainder");
        Equal(5, f.Stored(site, f.Wood), "new stock supplies second material");
        var next = f.Site(6, 0, 2);
        var secondCore = new Building_StorageCore(); f.Register(secondCore, -1); f.Supplier.TickRare();
        Equal(0, f.Stored(next, f.Steel), "multiple cores disable supply");
        manager.Unregister(secondCore.node); f.Supplier.TickRare();
        Equal(2, f.Stored(next, f.Steel), "removing extra core restores supply");
        var last = f.Site(7, 0, 1); f.Supplier.SetFaction(new Faction()); f.Supplier.TickRare();
        Equal(0, f.Stored(last, f.Steel), "captured supplier stops supplying player sites");
    }
}
