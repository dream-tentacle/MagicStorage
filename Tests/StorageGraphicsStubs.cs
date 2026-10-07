// Controlled engine boundary for the REAL storage graphic/query/lifecycle classes.
// Records draw commands and mesh invalidations; does not render textures or simulate Unity.
using System;
using System.Collections;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace UnityEngine
{
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r=r; this.g=g; this.b=b; this.a=a; }
        public static Color white => new Color(1,1,1);
    }
    public struct Vector2 { public float x,y; public Vector2(float x,float y) { this.x=x;this.y=y; } public static Vector2 one => new Vector2(1,1); }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; } }
    public struct Quaternion { public static Quaternion identity => new Quaternion(); }
    public class Shader { public string Name; public Shader(string name) { Name=name; } }
    public class Material { public string Path; public Shader Shader; public Color Color; public int renderQueue; public Verse.LinkDirections Links; }
    public class Mesh { }
    public sealed class DrawCommand { public Vector3 Position; public Material Material; public Verse.SectionLayer Layer; }
    public static class Graphics
    {
        public static readonly List<DrawCommand> Calls = new List<DrawCommand>();
        public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer)
        { Calls.Add(new DrawCommand { Position=position,Material=material }); }
    }
}
namespace Verse
{
    public struct IntVec3 : IEquatable<IntVec3>
    {
        public int x,y,z;
        public IntVec3(int x,int y,int z) { this.x=x;this.y=y;this.z=z; }
        public static IntVec3 operator +(IntVec3 a,IntVec3 b) => new IntVec3(a.x+b.x,a.y+b.y,a.z+b.z);
        public bool Equals(IntVec3 b) => x==b.x && y==b.y && z==b.z;
        public override bool Equals(object obj) => obj is IntVec3 p && Equals(p);
        public override int GetHashCode() => x*397^z;
        public bool InBounds(Map map) => x>=0 && z>=0 && x<map.Size && z<map.Size;
        public bool Fogged(Map map) => map.Fog.Contains(this);
        public List<Thing> GetThingList(Map map) => map.At(this);
        public Vector3 ToVector3ShiftedWithAltitude(AltitudeLayer altitude) => new Vector3(x+0.5f,(float)altitude,z+0.5f);
    }
    public struct IntVec2 { public int x,z; public IntVec2(int x,int z) { this.x=x;this.z=z; } }
    public struct Rot4
    {
        public int AsInt; public Rot4(int i) { AsInt=i; }
        public static Rot4 North => new Rot4(0); public static Rot4 East => new Rot4(1);
        public static Rot4 South => new Rot4(2); public static Rot4 West => new Rot4(3);
    }
    public struct CellRect : IEnumerable<IntVec3>
    {
        public int minX,minZ,Width,Height;
        public CellRect(int x,int z,int w,int h) { minX=x;minZ=z;Width=w;Height=h; }
        public bool Contains(IntVec3 p) => p.x>=minX && p.z>=minZ && p.x<minX+Width && p.z<minZ+Height;
        public IEnumerator<IntVec3> GetEnumerator() { for(int x=minX;x<minX+Width;x++) for(int z=minZ;z<minZ+Height;z++) yield return new IntVec3(x,0,z); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public static class GenAdj
    {
        public static readonly IntVec3[] CardinalDirections = { new IntVec3(0,0,1),new IntVec3(1,0,0),new IntVec3(0,0,-1),new IntVec3(-1,0,0) };
        public static CellRect OccupiedRect(this Thing thing) => OccupiedRect(thing.Position,thing.Rotation,thing.def.size);
        public static CellRect OccupiedRect(IntVec3 center,Rot4 rot,IntVec2 size)
        {
            if (rot.AsInt%2==1) size=new IntVec2(size.z,size.x);
            if (size.x%2==0 && (rot.AsInt==2 || rot.AsInt==3)) center.x--;
            if (size.z%2==0 && (rot.AsInt==1 || rot.AsInt==2)) center.z--;
            return new CellRect(center.x-(size.x-1)/2,center.z-(size.z-1)/2,size.x,size.z);
        }
    }
    public enum AltitudeLayer { Conduits=1,Blueprint=2,MapDataOverlay=3 }
    public enum DestroyMode { Vanish }
    [Flags] public enum LinkDirections { None=0,Up=1,Right=2,Down=4,Left=8 }
    public class BuildableDef { }
    public class ThingDef : BuildableDef
    {
        public Type thingClass=typeof(Building); public BuildableDef entityDefToBuild;
        public bool IsBlueprint,IsFrame; public List<CompProperties> comps=new List<CompProperties>();
        public IntVec2 size=new IntVec2(1,1); public string uiIconPath="ConduitIcon";
    }
    public class Thing
    {
        public ThingDef def; public bool Spawned,Destroyed; public Map Map; public IntVec3 Position;
        public Rot4 Rotation; public Faction Faction=Faction.OfPlayer;
    }
    public class ThingWithComps : Thing { }
    public class Building : ThingWithComps { public virtual void SetFaction(Faction f,Pawn recruiter=null) { Faction=f; } }
    public class Pawn : Thing { }
    public class CompProperties
    {
        public Type compClass; public CompProperties() { } public CompProperties(Type type) { compClass=type; }
        public virtual void DrawGhost(IntVec3 c,Rot4 r,ThingDef d,Color col,AltitudeLayer a,Thing t=null) { }
    }
    public class ThingComp
    {
        public ThingWithComps parent; public CompProperties props;
        public virtual void PostSpawnSetup(bool respawningAfterLoad) { }
        public virtual void PostDeSpawn(Map map,DestroyMode mode=DestroyMode.Vanish) { }
        public virtual string CompInspectStringExtra() => null;
    }
    public class Map
    {
        public int Size=64; public HashSet<IntVec3> Fog=new HashSet<IntVec3>();
        private readonly Dictionary<IntVec3,List<Thing>> cells=new Dictionary<IntVec3,List<Thing>>();
        public MapDrawer mapDrawer=new MapDrawer(); public MagicStorage.MapComponent_StorageNetworks Networks=new MagicStorage.MapComponent_StorageNetworks();
        public T GetComponent<T>() => (T)(object)Networks;
        public List<Thing> At(IntVec3 c) { if (!cells.TryGetValue(c,out var things)) cells[c]=things=new List<Thing>();return things; }
        public void Spawn(Thing thing) { thing.Map=this;thing.Spawned=true;foreach(var c in thing.OccupiedRect()) At(c).Add(thing); }
        public void Remove(Thing thing) { foreach(var c in thing.OccupiedRect()) At(c).Remove(thing);thing.Spawned=false; }
    }
    public sealed class MapDrawer
    {
        public readonly HashSet<string> DirtySections=new HashSet<string>();
        public void MapMeshDirty(IntVec3 c,ulong flags,bool adjacent,bool sections)
        {
            DirtySections.Add((c.x/17)+","+(c.z/17));
            if(adjacent) for(int x=c.x-1;x<=c.x+1;x++) for(int z=c.z-1;z<=c.z+1;z++) if(x>=0 && z>=0) DirtySections.Add((x/17)+","+(z/17));
        }
    }
    public class Section { public Map map; public Section(Map map) { this.map=map; } }
    public class SectionLayer
    {
        protected Map Map; public ulong relevantChangeTypes; public int DrawCalls;
        public SectionLayer(Section section) { Map=section.map; }
        public virtual void DrawLayer() { DrawCalls++; }
    }
    public abstract class SectionLayer_Things : SectionLayer
    {
        protected bool requireAddToMapMesh; public SectionLayer_Things(Section s):base(s) { }
        protected abstract void TakePrintFrom(Thing t);
        public void PrintForTest(Thing t) => TakePrintFrom(t);
    }
    public static class Printer_Plane
    {
        public static readonly List<DrawCommand> Calls=new List<DrawCommand>();
        public static void PrintPlane(SectionLayer layer,Vector3 center,Vector2 size,Material mat,float rotation=0)
        { Calls.Add(new DrawCommand { Layer=layer,Position=center,Material=mat }); }
    }
    public static class MeshPool { public static Mesh plane10=new Mesh(); }
    public static class ShaderDatabase { public static Shader MetaOverlay=new Shader("Overlay"); }
    public sealed class ShaderTypeDef { public Shader Shader=new Shader("EdgeDetect"); }
    public static class ShaderTypeDefOf { public static ShaderTypeDef EdgeDetect=new ShaderTypeDef(); }
    public static class MaterialPool
    {
        public static Material MatFrom(string path,Shader shader,Color color,int queue=0) => new Material { Path=path,Shader=shader,Color=color,renderQueue=queue };
    }
    public static class MaterialAtlasPool
    {
        public static Material SubMaterialFromAtlas(Material root,LinkDirections links) => new Material { Path=root.Path,Shader=root.Shader,Color=root.Color,renderQueue=root.renderQueue,Links=links };
    }
    public class GraphicData { }
    public struct GraphicRequest { public string path;public Color color,colorTwo;public Vector2 drawSize;public GraphicData graphicData;public Shader shader; }
    public class Graphic
    {
        public string path; public Color color,colorTwo; public Vector2 drawSize;public GraphicData data;
        public virtual Material MatSingle => MaterialPool.MatFrom(path,null,color);
        public virtual void Init(GraphicRequest req) { path=req.path;color=req.color;colorTwo=req.colorTwo;drawSize=req.drawSize;data=req.graphicData; }
        public virtual Graphic GetColoredVersion(Shader shader,Color c,Color c2) { var g=new Graphic_Single();g.Init(new GraphicRequest { path=path,color=c,colorTwo=c2,drawSize=drawSize,graphicData=data,shader=shader });return g; }
        public virtual void DrawWorker(Vector3 loc,Rot4 rot,ThingDef def,Thing thing,float extraRotation) => Graphics.DrawMesh(MeshPool.plane10,loc,Quaternion.identity,MatSingle,0);
        public virtual void Print(SectionLayer layer,Thing thing,float extraRotation) { }
    }
    public class Graphic_Single : Graphic
    {
        private Shader shader;
        public override void Init(GraphicRequest req) { base.Init(req);shader=req.shader; }
        public override Material MatSingle => MaterialPool.MatFrom(path,shader,color);
    }
    public class Graphic_Linked : Graphic
    {
        protected Graphic subGraphic;
        public Graphic_Linked() { } public Graphic_Linked(Graphic g) { subGraphic=g;data=g.data; }
        public virtual bool ShouldLinkWith(IntVec3 cell,Thing parent) => false;
        protected Material LinkedDrawMatFrom(Thing parent,IntVec3 cell)
        {
            int mask=0;for(int i=0;i<4;i++) if(ShouldLinkWith(cell+GenAdj.CardinalDirections[i],parent)) mask|=1<<i;
            return MaterialAtlasPool.SubMaterialFromAtlas(subGraphic.MatSingle,(LinkDirections)mask);
        }
        public override Material MatSingle => MaterialAtlasPool.SubMaterialFromAtlas(subGraphic.MatSingle,LinkDirections.None);
        public override void Print(SectionLayer layer,Thing thing,float rotation)
        { Printer_Plane.PrintPlane(layer,thing.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.Conduits),Vector2.one,LinkedDrawMatFrom(thing,thing.Position),rotation); }
    }
    public static class GraphicDatabase
    {
        public static T Get<T>(string path,Shader shader,Vector2 size,Color color) where T:Graphic,new()
        { var g=new T();g.Init(new GraphicRequest { path=path,shader=shader,drawSize=size,color=color });return g; }
    }
    public struct AcceptanceReport
    {
        public bool Accepted;
        public static implicit operator AcceptanceReport(bool value) => new AcceptanceReport { Accepted=value };
        public static implicit operator AcceptanceReport(string value) => new AcceptanceReport { Accepted=false };
    }
    public class PlaceWorker { public virtual AcceptanceReport AllowsPlacing(BuildableDef d,IntVec3 c,Rot4 r,Map m,Thing ignore=null,Thing thing=null) => true; }
    public static class Translation { public static string Translate(this string key,params object[] args) => key; }
    public class StaticConstructorOnStartupAttribute : Attribute { }
    public static class DefDatabase<T> { public static readonly List<T> AllDefsListForReading=new List<T>(); }
    public class DesignatorManager { public object SelectedDesignator; }
    public static class Find
    { public static Map CurrentMap;public static DesignatorManager DesignatorManager=new DesignatorManager();public static Selector Selector=new Selector(); }
}
namespace RimWorld
{
    public class Faction { public static readonly Faction OfPlayer=new Faction(); }
    public class Selector { public readonly List<object> SelectedObjectsListForReading=new List<object>(); }
    public class Designator_Place { public Verse.BuildableDef PlacingDef; }
    public static class MapMeshFlagDefOf { public const ulong Things=1,Buildings=2,FogOfWar=4; }
}
namespace MagicStorage
{
    public class StorageNetwork { public static string StatusLabel(StorageNetwork network) => "test"; }
    public class MapComponent_StorageNetworks
    {
        public int Registrations,Unregistrations,FactionChanges;
        public void Register(CompStorageNode node) { Registrations++; }
        public void Unregister(CompStorageNode node) { Unregistrations++; }
        public void NotifyFactionChanged(Verse.Thing thing) { FactionChanges++; }
    }
}
