// Rendering boundaries only: tests execute the production layout and draw routing,
// recording mesh commands instead of starting RimWorld or an engine renderer.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HarmonyLib
{
    public class HarmonyMethod { public HarmonyMethod(Type type, string name) { } }
    public class Harmony
    {
        public Harmony(string id) { }
        public void Patch(MethodInfo method, HarmonyMethod prefix)
        { if (method == null) throw new Exception("Missing patch target"); }
    }
    public static class AccessTools
    {
        public static MethodInfo Method(Type type, string name, Type[] parameters = null) => parameters == null
            ? type.GetMethod(name) : type.GetMethod(name, parameters);
    }
}
namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x=x;this.y=y; } }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 up => new Vector3(0,1,0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
    }
    public struct Quaternion { public float Angle; public static Quaternion AngleAxis(float a, Vector3 v) => new Quaternion { Angle=a }; }
    public struct Color32 { public byte r; }
    public class Material { public string Name; }
    public class Mesh { public Vector2 Size; public bool Flip; }
    public class DrawCommand { public Vector3 Center; public Vector2 Size; public float Angle; public bool Flip; public Material Material; }
    public static class Graphics
    {
        public static List<DrawCommand> Calls = new List<DrawCommand>();
        public static void DrawMesh(Mesh mesh, Vector3 pos, Quaternion rot, Material mat, int layer) =>
            Calls.Add(new DrawCommand { Center=pos,Size=mesh.Size,Angle=rot.Angle,Flip=mesh.Flip,Material=mat });
    }
}
namespace RimWorld { public static class MapMeshFlagDefOf { public static ulong Things=1; } }
namespace Verse
{
    [AttributeUsage(AttributeTargets.Class)] public class StaticConstructorOnStartupAttribute : Attribute { }
    public enum ThingCategory { Item, Building }
    public enum DrawerType { None, MapMeshOnly, RealtimeOnly, MapMeshAndRealTime }
    public enum DrawPhase { EnsureInitialized, ParallelPreDraw, Draw }
    public struct IntVec3
    {
        public int x,z;
        public Vector3 ToVector3Shifted() => new Vector3(x+0.5f,0,z+0.5f);
    }
    public struct Rot4
    {
        public int Value;
        public bool IsHorizontal => Value%2==1;
        public float AsAngle => Value*90;
        public static Rot4 North => new Rot4();
        public static Rot4 East => new Rot4 { Value=1 };
        public static Rot4 West => new Rot4 { Value=3 };
        public static bool operator ==(Rot4 a, Rot4 b) => a.Value==b.Value;
        public static bool operator !=(Rot4 a, Rot4 b) => a.Value!=b.Value;
        public override bool Equals(object obj) => obj is Rot4 r && r==this;
        public override int GetHashCode() => Value;
    }
    public class ThingDef { public ThingCategory category; public float Altitude; public bool dontPrint; public DrawerType drawerType; }
    public class Thing
    {
        public ThingDef def=new ThingDef(); public bool Spawned=true; public Map Map;
        public IntVec3 Position; public int thingIDNumber=1; public Rot4 Rotation;
        public Graphic Graphic=new Graphic(); public int stackCount=1;
        public void DynamicDrawPhase(DrawPhase phase) { }
    }
    public class SectionLayer_ThingsGeneral { public void TakePrintFrom(Thing t) { } }
    public static class GenThing { public static Vector3 TrueCenter(Thing t) => default(Vector3); }
    public static class GenMapUI { public static Vector2 LabelDrawPosFor(Thing thing, float worldOffsetZ) => default(Vector2); }
    public class HaulDestinationManager { public object Parent; public object SlotGroupParentAt(IntVec3 cell) => Parent; }
    public class ListerThings { public List<Thing> AllThings=new List<Thing>(); }
    public class MapDrawer { public List<IntVec3> Dirty=new List<IntVec3>(); public void MapMeshDirty(IntVec3 c, ulong f, bool a, bool s) { Dirty.Add(c); } }
    public class Map { public HaulDestinationManager haulDestinationManager=new HaulDestinationManager(); public ListerThings listerThings=new ListerThings(); public MapDrawer mapDrawer=new MapDrawer(); }
    public static class Find { public static List<Map> Maps=new List<Map>(); }
    public class GraphicData { public float flipExtraRotation; }
    public class Graphic
    {
        public Vector2 drawSize=new Vector2(1,1); public bool ShouldDrawRotated,WestFlipped,EastFlipped;
        public float DrawRotatedExtraAngleOffset; public GraphicData data;
        public Material Material=new Material { Name="item material" };
        public virtual Material MatAt(Rot4 r, Thing t) => Material;
        public static bool TryGetTextureAtlasReplacementInfo(Material mat, object g, bool flip, bool vertices,
            out Material replacement, out Vector2[] uvs, out Color32 color)
        { replacement=mat;uvs=null;color=default(Color32);return false; }
    }
    public class Graphic_StackCount : Graphic
    {
        public Graphic Single=new Graphic(),Stack=new Graphic();
        public Graphic SubGraphicFor(Thing t) => t.stackCount==1 ? Single : Stack;
    }
    public class Graphic_Random : Graphic { public Graphic Variant=new Graphic(); public Graphic SubGraphicFor(Thing t) => Variant; }
    public static class AtlasGroup { public static object ToAtlasGroup(this ThingCategory c) => null; }
    public static class MeshPool
    {
        public static Mesh GridPlane(Vector2 size) => new Mesh { Size=size };
        public static Mesh GridPlaneFlip(Vector2 size) => new Mesh { Size=size,Flip=true };
    }
    public static class Printer_Plane
    {
        public static List<DrawCommand> Calls=new List<DrawCommand>();
        public static void PrintPlane(SectionLayer_ThingsGeneral layer, Vector3 center, Vector2 size, Material mat,
            float angle, bool flip, Vector2[] uvs, Color32[] colors) =>
            Calls.Add(new DrawCommand { Center=center,Size=size,Material=mat,Angle=angle,Flip=flip });
    }
}
namespace MagicStorage
{
    public class MagicStorageSettings { public bool TidySupplyShelf=true; }
    public static class MagicStorageMod { public static MagicStorageSettings Settings=new MagicStorageSettings(); }
    public class Building_StorageSupplyShelf : Verse.Thing
    {
        public const int SlotCount=3;
        public Verse.ThingDef[] Selections=new Verse.ThingDef[3];
        public Verse.ThingDef SelectedDef(int i) => Selections[i];
    }
}
