using System;
using System.Linq;
using MagicStorage;
using RimWorld;
using UnityEngine;
using Verse;

internal static class StorageGraphicsTests
{
    private static int checks;
    private static void Equal<T>(T expected,T actual,string message)
    { checks++;if(!Equals(expected,actual)) throw new Exception(message+": expected "+expected+", got "+actual); }
    private static IntVec3 Cell(int x,int z) => new IntVec3(x,0,z);
    private static ThingDef Node(bool conduit=false,int width=1,int height=1) => new ThingDef
    { thingClass=conduit?typeof(Building_StorageConduit):typeof(Building),size=new IntVec2(width,height),comps={new CompProperties_StorageNode()} };
    private static ThingDef Plan(ThingDef built,bool frame=false) => new ThingDef
    { entityDefToBuild=built,IsBlueprint=!frame,IsFrame=frame,size=built.size };
    private static ThingWithComps Spawn(Map map,ThingDef def,IntVec3 cell,Rot4 rot=default(Rot4),Faction faction=null)
    { var t=new ThingWithComps { def=def,Position=cell,Rotation=rot,Faction=faction??Faction.OfPlayer };map.Spawn(t);return t; }
    private static Graphic_StorageConduit Graphic(Color? color=null)
    {
        var graphic=new Graphic_StorageConduit();
        graphic.Init(new GraphicRequest { path="CableAtlas",shader=new Shader("Cutout"),color=color??Color.white,drawSize=Vector2.one,graphicData=new GraphicData() });
        return graphic;
    }
    public static int Main()
    {
        try
        {
            LinkShapes(); PlannedConnectivity(); Previews(); Overlay(); PlanLifecycle(); Placement();
            Console.WriteLine("PASS: "+checks+" storage graphics assertions (actual mod classes; Unity draw calls and engine lifecycle boundaries stubbed).");
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
    }
    private static void LinkShapes()
    {
        for(int mask=0;mask<16;mask++)
        {
            var map=new Map();var def=Node(true);var root=Spawn(map,def,Cell(20,20));
            for(int i=0;i<4;i++) if((mask&(1<<i))!=0) Spawn(map,def,root.Position+GenAdj.CardinalDirections[i]);
            Equal((LinkDirections)mask,StorageConnectionQuery.LinksAt(map,root.Position,Faction.OfPlayer,false),"built shape "+mask);
            Printer_Plane.Calls.Clear();Graphic().Print(new SectionLayer(new Section(map)),root,0);
            Equal(1,Printer_Plane.Calls.Count,"standalone cable prints one tile");
            Equal((LinkDirections)mask,Printer_Plane.Calls[0].Material.Links,"atlas shape "+mask);

            var plannedMap=new Map();var blueprint=Spawn(plannedMap,Plan(def),Cell(20,20));
            for(int i=0;i<4;i++) if((mask&(1<<i))!=0) Spawn(plannedMap,Plan(def,i%2==0),blueprint.Position+GenAdj.CardinalDirections[i]);
            Equal((LinkDirections)mask,StorageConnectionQuery.LinksAt(plannedMap,blueprint.Position,Faction.OfPlayer,true),"planned shape "+mask);
            Equal(LinkDirections.None,StorageConnectionQuery.LinksAt(plannedMap,blueprint.Position,Faction.OfPlayer,false),"planned neighbors cannot conduct "+mask);
            Printer_Plane.Calls.Clear();Graphic().Print(new SectionLayer(new Section(plannedMap)),blueprint,0);
            Equal((LinkDirections)mask,Printer_Plane.Calls[0].Material.Links,"blueprint atlas shape "+mask);
        }
        var boundary=new Map();var def2=Node(true);Spawn(boundary,def2,Cell(1,0));Spawn(boundary,def2,Cell(0,1));
        Equal(LinkDirections.Up|LinkDirections.Right,StorageConnectionQuery.LinksAt(boundary,Cell(0,0),Faction.OfPlayer,true),"map edge clips neighbors");
    }
    private static void PlannedConnectivity()
    {
        var map=new Map();var conduit=Node(true);var cell=Cell(16,20);
        var plan=Spawn(map,Plan(conduit),cell);
        var frame=Spawn(map,Plan(conduit,true),Cell(17,20));
        Equal(LinkDirections.Right,StorageConnectionQuery.LinksAt(map,cell,Faction.OfPlayer,true),"blueprint connects to frame across section boundary");
        Equal(LinkDirections.None,StorageConnectionQuery.LinksAt(map,cell,Faction.OfPlayer,false),"frame excluded from actual connection");
        map.Remove(frame);
        Equal(LinkDirections.None,StorageConnectionQuery.LinksAt(map,cell,Faction.OfPlayer,true),"cancelled frame no longer links");
        var complete=Spawn(map,conduit,Cell(17,20));
        Equal(LinkDirections.Right,StorageConnectionQuery.LinksAt(map,cell,Faction.OfPlayer,false),"completed frame becomes actual neighbor");
        complete.Faction=new Faction();
        Equal(LinkDirections.None,StorageConnectionQuery.LinksAt(map,cell,Faction.OfPlayer,true),"foreign faction excluded");
        map.Remove(complete);Spawn(map,new ThingDef(),Cell(17,20));
        Equal(LinkDirections.None,StorageConnectionQuery.LinksAt(map,cell,Faction.OfPlayer,true),"ordinary electric conduit cannot join storage");
        var unit=Spawn(map,Node(false,1,2),Cell(15,20));
        Equal(LinkDirections.Left,StorageConnectionQuery.LinksAt(map,cell,Faction.OfPlayer,true),"facility footprint connects");
        Equal(LinkDirections.None,StorageConnectionQuery.LinksAt(map,cell,Faction.OfPlayer,true,null,unit),"relocating facility can be excluded from preview");
        Equal(0,map.Networks.Registrations,"visual queries do not register any working node");
    }
    private static void Previews()
    {
        var green=new Color(0.5f,1f,0.6f,0.4f);var def=Node(true);def.uiIconPath="MenuIcon";
        Graphics.Calls.Clear();Graphic(green).DrawWorker(new Vector3(5,2,5),Rot4.North,def,null,0);
        Equal(1,Graphics.Calls.Count,"one mouse ghost");
        Equal("MenuIcon",Graphics.Calls[0].Material.Path,"ghost uses menu icon rather than atlas tile");
        Equal("EdgeDetect",Graphics.Calls[0].Material.Shader.Name,"native ghost shader");
        Equal(green,Graphics.Calls[0].Material.Color,"native validity color preserved");

        var map=new Map();Find.CurrentMap=map;var facility=Node(false,1,2);
        Graphics.Calls.Clear();new CompProperties_StorageNode().DrawGhost(Cell(20,20),Rot4.North,facility,green,AltitudeLayer.Blueprint);
        Equal(2,Graphics.Calls.Count,"vertical facility previews full footprint");
        Equal(LinkDirections.Up,Graphics.Calls[0].Material.Links,"north footprint start");
        Equal(LinkDirections.Down,Graphics.Calls[1].Material.Links,"north footprint end");
        Graphics.Calls.Clear();StorageNetworkDrawing.DrawGhost(facility,Cell(20,20),Rot4.East,green,null);
        Equal(2,Graphics.Calls.Count,"rotated facility previews full footprint");
        Equal(LinkDirections.Right,Graphics.Calls[0].Material.Links,"rotated footprint start");
        Equal(LinkDirections.Left,Graphics.Calls[1].Material.Links,"rotated footprint end");
        Spawn(map,Node(true),Cell(19,20));
        var red=new Color(1,0,0,0.4f);Graphics.Calls.Clear();StorageNetworkDrawing.DrawGhost(facility,Cell(20,20),Rot4.East,red,null);
        Equal(LinkDirections.Right|LinkDirections.Left,Graphics.Calls[0].Material.Links,"preview shows connection into existing cable");
        Equal(red,Graphics.Calls[0].Material.Color,"invalid preview remains red");
        Graphics.Calls.Clear();StorageNetworkDrawing.DrawGhost(def,Cell(20,20),Rot4.North,green,null);
        Equal(0,Graphics.Calls.Count,"cable comp does not double draw native mouse ghost");
    }
    private static void Overlay()
    {
        var map=new Map();Find.CurrentMap=map;Find.Selector.SelectedObjectsListForReading.Clear();Find.DesignatorManager.SelectedDesignator=null;
        var def=Node(true);var parent=Spawn(map,def,Cell(10,10));
        var layer=new SectionLayer_StorageNetwork(new Section(map));
        layer.DrawLayer();Equal(0,layer.DrawCalls,"overlay hidden when idle");
        Find.DesignatorManager.SelectedDesignator=new Designator_Place { PlacingDef=def };
        layer.DrawLayer();Equal(1,layer.DrawCalls,"build or install tool enables overlay");
        Equal(false,StorageNetworkDrawing.ShouldDraw(new Map()),"overlay limited to current map");
        Find.DesignatorManager.SelectedDesignator=null;Find.Selector.SelectedObjectsListForReading.Add(parent);
        Equal(true,StorageNetworkDrawing.ShouldDraw(map),"selection enables overlay");
        Find.Selector.SelectedObjectsListForReading.Clear();
        var planned=Spawn(map,Plan(def),Cell(11,10));
        Printer_Plane.Calls.Clear();layer.PrintForTest(parent);layer.PrintForTest(planned);
        Equal(2,Printer_Plane.Calls.Count,"actual and planned tiles both visible");
        Equal(1f,Printer_Plane.Calls[0].Material.Color.a,"actual overlay opaque");
        Equal(0.45f,Printer_Plane.Calls[1].Material.Color.a,"planned overlay visually distinct");
        Equal(LinkDirections.None,Printer_Plane.Calls[0].Material.Links,"actual overlay does not claim unfinished connection");
        Equal(LinkDirections.Left,Printer_Plane.Calls[1].Material.Links,"planned overlay shows intended connection");
        Equal((float)AltitudeLayer.MapDataOverlay,Printer_Plane.Calls[0].Position.y,"overlay above buildings");
        Equal(3600,Printer_Plane.Calls[0].Material.renderQueue,"overlay render queue");
        var overlapping=Spawn(map,Node(),parent.Position);
        Printer_Plane.Calls.Clear();layer.PrintForTest(parent);layer.PrintForTest(overlapping);
        Equal(1,Printer_Plane.Calls.Count,"overlapping actual footprints do not double print");
        map.Fog.Add(parent.Position);Printer_Plane.Calls.Clear();layer.PrintForTest(parent);
        Equal(0,Printer_Plane.Calls.Count,"fogged cell hidden");
        map.Fog.Clear();parent.Faction=new Faction();Printer_Plane.Calls.Clear();layer.PrintForTest(parent);
        Equal(0,Printer_Plane.Calls.Count,"foreign facility hidden");

        var facility=Spawn(map,Node(false,2,2),Cell(30,30));Printer_Plane.Calls.Clear();layer.PrintForTest(facility);
        Equal(4,Printer_Plane.Calls.Count,"all cells of larger facility drawn");
        var cable=Spawn(map,def,Cell(29,30));Printer_Plane.Calls.Clear();Graphic().Print(layer,cable,0);
        Equal(2,Printer_Plane.Calls.Count,"ground cable extends into facility");
        Equal(30.5f,Printer_Plane.Calls[1].Position.x,"extension printed in facility cell");
        Equal((float)AltitudeLayer.Conduits,Printer_Plane.Calls[1].Position.y,"extension stays below building");
    }
    private static void PlanLifecycle()
    {
        var built=Node(true);var blueprint=Plan(built);var frame=Plan(built,true);var install=Plan(built);var foreign=Plan(new ThingDef());
        DefDatabase<ThingDef>.AllDefsListForReading.Clear();DefDatabase<ThingDef>.AllDefsListForReading.AddRange(new[]{built,blueprint,frame,install,foreign});
        StoragePlanGraphics.Initialize();StoragePlanGraphics.Initialize();
        foreach(var plan in new[]{blueprint,frame,install})
        {
            Equal(1,plan.comps.Count(p=>p.compClass==typeof(CompStoragePlanGraphics)),"exactly one visual comp on each implied plan");
            Equal(false,plan.comps.Any(p=>p is CompProperties_StorageNode),"plans have no operational storage component");
        }
        Equal(0,foreign.comps.Count,"other mods' plans untouched");
        Equal(1,built.comps.Count,"actual building comps unchanged");
        var map=new Map();var t=Spawn(map,blueprint,Cell(16,20));var comp=new CompStoragePlanGraphics { parent=t };
        comp.PostSpawnSetup(false);
        Equal(true,map.mapDrawer.DirtySections.Contains("0,1"),"blueprint spawn invalidates own section");
        Equal(true,map.mapDrawer.DirtySections.Contains("1,1"),"blueprint spawn invalidates neighboring section");
        Equal(0,map.Networks.Registrations,"blueprint notification never registers a live node");
        map.mapDrawer.DirtySections.Clear();map.Remove(t);comp.PostDeSpawn(map);
        Equal(true,map.mapDrawer.DirtySections.Contains("1,1"),"cancelled blueprint refreshes adjacent section");
        var f=Spawn(map,frame,Cell(16,20));new CompStoragePlanGraphics { parent=f }.PostSpawnSetup(false);
        Equal(0,map.Networks.Registrations,"frame remains visual-only");
        var real=Spawn(map,built,Cell(16,21));new CompStorageNode { parent=real,props=built.comps[0] }.PostSpawnSetup(false);
        Equal(1,map.Networks.Registrations,"only completed building registers storage node");
    }
    private static void Placement()
    {
        var map=new Map();var def=Node(true);var place=new PlaceWorker_StorageConduit();
        Equal(true,place.AllowsPlacing(def,Cell(5,5),Rot4.North,map).Accepted,"empty cell accepted");
        var plan=Spawn(map,Plan(def),Cell(5,5));
        Equal(false,place.AllowsPlacing(def,Cell(5,5),Rot4.North,map).Accepted,"existing blueprint blocks duplicate");
        Equal(true,place.AllowsPlacing(def,Cell(5,5),Rot4.North,map,plan).Accepted,"ignored placement target honored");
        map.Remove(plan);var frame=Spawn(map,Plan(def,true),Cell(5,5));
        Equal(false,place.AllowsPlacing(def,Cell(5,5),Rot4.North,map).Accepted,"existing frame blocks duplicate");
        map.Remove(frame);Spawn(map,def,Cell(5,5));
        Equal(false,place.AllowsPlacing(def,Cell(5,5),Rot4.North,map).Accepted,"existing conduit blocks duplicate");
    }
}
