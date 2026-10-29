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
        public void Patch(MethodInfo method, HarmonyMethod prefix = null, HarmonyMethod transpiler = null)
        { if (method == null) throw new Exception("Missing patch target"); }
    }
    public class CodeInstruction
    {
        public System.Reflection.Emit.OpCode opcode;
        public object operand;
        public CodeInstruction(System.Reflection.Emit.OpCode opcode, object operand = null)
        { this.opcode = opcode; this.operand = operand; }
    }
    public static class AccessTools
    {
        public static MethodInfo Method(Type type, string name, Type[] parameters = null) => parameters == null
            ? type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            : type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, parameters, null);
    }
}
namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x=x;this.y=y; }
        public static Vector2 operator *(Vector2 value, float scale) => new Vector2(value.x * scale, value.y * scale);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 up => new Vector3(0,1,0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
    }
    public struct Quaternion { public float Angle; public static Quaternion AngleAxis(float a, Vector3 v) => new Quaternion { Angle=a }; }
    public struct Color32 { public byte r; }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r=r;this.g=g;this.b=b;this.a=a; }
        public static Color white => new Color(1, 1, 1);
        public static Color operator *(Color left, Color right) => new Color(left.r*right.r,left.g*right.g,left.b*right.b,left.a*right.a);
    }
    public class Shader { public string Name; }
    public class Texture { public int width = 64, height = 64; }
    public class Texture2D : Texture { }
    public class RenderTexture : Texture { }
    public class Material { public string Name; public Verse.MaterialRequest Request; }
    public class Mesh { public Vector2 Size; public bool Flip; }
    public class DrawCommand { public Vector3 Center; public Vector2 Size; public float Angle; public bool Flip; public Material Material; }
    public static class Graphics
    {
        public static List<DrawCommand> Calls = new List<DrawCommand>();
        public static void DrawMesh(Mesh mesh, Vector3 pos, Quaternion rot, Material mat, int layer) =>
            Calls.Add(new DrawCommand { Center=pos,Size=mesh.Size,Angle=rot.Angle,Flip=mesh.Flip,Material=mat });
    }
}
namespace RimWorld
{
    public static class MapMeshFlagDefOf { public static ulong Things=1; }
    public class OverlayDrawer { public void DrawOverlay(Verse.Thing t) { } }
}
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
    public class ThingDef
    {
        public readonly List<DefModExtension> modExtensions = new List<DefModExtension>();
        public T GetModExtension<T>() where T : DefModExtension
        { foreach (var extension in modExtensions) if (extension is T match) return match; return null; }
        public ThingCategory category; public float Altitude; public bool dontPrint; public DrawerType drawerType;
        public GraphicData graphicData = new GraphicData();
    }
    public class DefModExtension { }
    public class ThingOverlays { public void ThingOverlaysOnGUI() { } }
    public class Thing
    {
        public ThingDef def=new ThingDef(); public bool Spawned=true; public Map Map;
        public IntVec3 Position; public int thingIDNumber=1; public Rot4 Rotation;
        public virtual Graphic Graphic { get; set; } = new Graphic(); public int stackCount=1;
        public int OverlayCalls;
        public virtual void DrawGUIOverlay() { OverlayCalls++; }
        public void DynamicDrawPhase(DrawPhase phase) { }
        public virtual void DeSpawn() { }
        public virtual void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false) { }
        protected virtual void DrawAt(Vector3 drawLoc, bool flip = false) { }
        public virtual void Print(SectionLayer layer) { }
    }
    public class ThingWithComps : Thing
    {
        protected override void DrawAt(Vector3 drawLoc, bool flip = false) { }
        public override void Print(SectionLayer layer) { }
    }
    public class SectionLayer { }
    public class SectionLayer_ThingsGeneral : SectionLayer { public void TakePrintFrom(Thing t) { } }
    public static class GenThing { public static Vector3 TrueCenter(Thing t) => default(Vector3); }
    public static class GenMapUI { public static Vector2 LabelDrawPosFor(Thing thing, float worldOffsetZ) => default(Vector2); }
    public class HaulDestinationManager { public object Parent; public object SlotGroupParentAt(IntVec3 cell) => Parent; }
    public class ListerThings { public List<Thing> AllThings=new List<Thing>(); }
    public class MapDrawer { public List<IntVec3> Dirty=new List<IntVec3>(); public void MapMeshDirty(IntVec3 c, ulong f, bool a, bool s) { Dirty.Add(c); } }
    public class DynamicDrawManager
    {
        public readonly List<Thing> Drawables=new List<Thing>();
        public void RegisterDrawable(Thing thing) { Drawables.Add(thing); }
        public void DeRegisterDrawable(Thing thing) { Drawables.Remove(thing); }
    }
    public class Map
    {
        public HaulDestinationManager haulDestinationManager=new HaulDestinationManager();
        public ListerThings listerThings=new ListerThings(); public MapDrawer mapDrawer=new MapDrawer();
        public DynamicDrawManager dynamicDrawManager=new DynamicDrawManager();
    }
    public static class Find { public static List<Map> Maps=new List<Map>(); }
    public class GraphicData { public float flipExtraRotation; }
    public static class BaseContent
    {
        public static readonly Graphic BadGraphic = new Graphic();
        public static readonly Texture2D BadTex = new Texture2D();
    }
    public class ShaderTypeDef { public Shader Shader; }
    public static class DefDatabase<T> { public static readonly List<T> AllDefsListForReading = new List<T>(); }
    public static class ShaderDatabase
    {
        public static readonly Shader Transparent = new Shader { Name = "map transparent" }, CutoutComplex = new Shader { Name = "map masked" };
        public static readonly Dictionary<Shader, Shader> UI = new Dictionary<Shader, Shader>();
        public static bool TryGetUIShader(Shader shader, out Shader ui) => UI.TryGetValue(shader, out ui);
    }
    public struct MaterialRequest
    {
        public Shader shader;
        public Texture mainTex, secondaryTex;
        public Texture2D maskTex;
        public Color color, colorTwo;
        public MaterialRequest(Texture texture, Shader shader, Color color)
        { mainTex=texture;this.shader=shader;this.color=color;colorTwo=Color.white;secondaryTex=null;maskTex=null; }
    }
    public static class MaterialPool
    {
        public static readonly Dictionary<Material, MaterialRequest> Requests = new Dictionary<Material, MaterialRequest>();
        public static bool TryGetRequestForMat(Material material, out MaterialRequest request) => Requests.TryGetValue(material, out request);
        public static Material MatFrom(MaterialRequest request)
        {
            var material = new Material { Request=request };
            Requests.Add(material, request);
            return material;
        }
    }
    public class ResolvedIcon
    {
        public Texture Texture;
        public Material Material;
        public float Scale = 1f, Angle;
        public Vector2 Proportions = new Vector2(1, 1);
        public Color Color = Color.white;
    }
    public static class Widgets
    {
        public static Func<Thing, ResolvedIcon> Resolve = item => new ResolvedIcon();
        public static int IconCalls;
        public static Texture GetIconFor(Thing item, Vector2 resolution, Rot4? rotation, bool stackOfOne,
            out float scale, out float angle, out Vector2 proportions, out Color color, out Material material)
        {
            IconCalls++;
            var icon = Resolve(item);
            scale=icon.Scale;angle=icon.Angle;proportions=icon.Proportions;color=icon.Color;material=icon.Material;
            return icon.Texture;
        }
    }
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
        public int SlotCount => Selections.Length;
        public Verse.ThingDef[] Selections=new Verse.ThingDef[3];
        public Verse.ThingDef SelectedDef(int i) => Selections[i];
        public Verse.Thing[] Stocks=new Verse.Thing[3];
        private readonly ShelfSlotGroup group = new ShelfSlotGroup();
        public class ShelfSlotGroup { public readonly List<Verse.Thing> HeldThings = new List<Verse.Thing>(); }
        public ShelfSlotGroup GetSlotGroup() => group;
        public Verse.Thing StockAt(int slot) => Stocks[slot];
        public int StockSlot(Verse.Thing item) => System.Array.IndexOf(Stocks, item);
        public void ResolveStockSlots(List<Verse.Thing> items)
        {
            foreach (var item in items)
                if (StockSlot(item) < 0)
                    for (int i = 0; i < SlotCount; i++)
                        if (Stocks[i] == null && Selections[i] == item.def) { Stocks[i] = item; break; }
        }
    }
}
