// Controlled native boundaries for the real supply service and blueprint adapter.
// Rendering, terrain legality and construction jobs still require in-game verification.
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace UnityEngine
{
    public struct Color { }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x, a.y-b.y, a.z-b.z);
    }
}
namespace Verse
{
    public struct TargetInfo
    {
        public IntVec3 Cell;
        public Map Map;
        public TargetInfo(IntVec3 cell, Map map) { Cell = cell; Map = map; }
    }
    public partial struct IntVec3
    { public UnityEngine.Vector3 ToVector3Shifted() => new UnityEngine.Vector3(x+0.5f, 0f, z+0.5f); }
    public static class CenterUtility
    { public static UnityEngine.Vector3 TrueCenter(this Thing thing) => new UnityEngine.Vector3(thing.Position.x+thing.width*0.5f, 0f, thing.Position.z+thing.height*0.5f); }
    public partial struct Rot4 { public int value; }
    public class Graphic { }
    public class Graphic_Random : Graphic { }
    public class TerrainDef { }
    public class BuildingProperties { public bool isAttachment, isPowerConduit; public RimWorld.StorageSettings fixedStorageSettings; }
    public partial class ThingDef
    {
        public object entityDefToBuild;
        public bool IsBlueprint, IsFrame, CanHaveFaction = true;
        public bool EverTransmitsPower, destroyable = true;
        public int ConstructionWidth = 1, ConstructionHeight = 1;
        public BuildingProperties building;
    }
    public partial class Thing
    {
        public Rot4 Rotation;
        public Graphic Graphic;
        public int? overrideGraphicIndex;
        public void SetFactionDirect(Faction faction) { Faction = faction; }
    }
    public class Selector
    {
        public Thing Selected;
        public int NumSelected => Selected == null ? 0 : 1;
        public bool IsSelected(Thing thing) => Selected == thing;
        public void Select(Thing thing, bool playSound, bool forceDesignatorDeselect) { Selected = thing; }
    }
    public static partial class GenDraw
    {
        public static float Radius;
        public static void DrawRadiusRing(IntVec3 center, float radius) { Radius = radius; }
    }
    public class PlaceWorker
    { public virtual void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, UnityEngine.Color ghostCol, Thing thing = null) { } }
    public static class GenSpawn
    {
        // Controlled conduit replacement boundary; other native wipe rules are not modeled.
        public static bool SpawningWipes(ThingDef newDef, ThingDef oldDef) => oldDef.destroyable &&
            (newDef.IsFrame && newDef.entityDefToBuild is ThingDef builtDef ? builtDef : newDef).EverTransmitsPower &&
            oldDef.building?.isPowerConduit == true;
        public static void WipeExistingThings(IntVec3 position, Rot4 rotation, ThingDef def, Map map, DestroyMode mode)
        {
            map.Wipes++;
            foreach (Thing thing in map.Ground.ToArray())
                if (thing.Spawned && thing.Position.x >= position.x && thing.Position.x < position.x + def.ConstructionWidth &&
                    thing.Position.z >= position.z && thing.Position.z < position.z + def.ConstructionHeight && SpawningWipes(def, thing.def))
                {
                    thing.DeSpawn(); thing.Destroy(); map.listerThings.Things.Remove(thing);
                }
        }
        public static Thing Spawn(Thing thing, IntVec3 position, Map map, Rot4 rotation)
        {
            thing.Position = position; thing.Map = map; thing.Rotation = rotation; thing.Spawned = true;
            map.listerThings.Things.Add(thing); return thing;
        }
    }
    public class TerrainGrid
    { public HashSet<IntVec3> Removable = new HashSet<IntVec3>(); public bool CanRemoveTopLayerAt(IntVec3 cell) => Removable.Contains(cell); }
    public class Plan
    { public int Removed; public void RemoveCell(IntVec3 cell) { Removed++; } }
    public class PlanManager
    { public Plan Plan = new Plan(); public bool TryGetPlan(IntVec3 cell, out Plan plan) { plan = Plan; return true; } }
    public partial class Map
    {
        public int Wipes;
        public RimWorld.EnrouteManager enrouteManager = new RimWorld.EnrouteManager();
        public TerrainGrid terrainGrid = new TerrainGrid();
        public PlanManager planManager = new PlanManager();
    }
    public class PawnPathFollower
    { public Thing Old, New; public void NotifyThingTransformed(Thing old, Thing replacement) { Old = old; New = replacement; } }
    public partial class Pawn { public PawnPathFollower pather = new PawnPathFollower(); }
}
namespace RimWorld
{
    public static class MoteMaker
    {
        public static int Beams;
        public static ThingDef Def;
        public static TargetInfo Source, Destination;
        public static UnityEngine.Vector3 SourceOffset, DestinationOffset;
        public static void MakeInteractionOverlay(ThingDef def, TargetInfo source, TargetInfo destination,
            UnityEngine.Vector3 sourceOffset, UnityEngine.Vector3 destinationOffset)
        {
            Beams++; Def=def; Source=source; Destination=destination;
            SourceOffset=sourceOffset; DestinationOffset=destinationOffset;
        }
    }
    public interface IConstructible { List<ThingDefCountClass> TotalMaterialCost(); }
    public interface IHaulEnroute { Map Map { get; } int SpaceRemainingFor(ThingDef material); }
    public class EnrouteManager
    {
        public readonly Dictionary<IHaulEnroute, Dictionary<ThingDef, int>> Claims = new Dictionary<IHaulEnroute, Dictionary<ThingDef, int>>();
        public int GetEnroute(IHaulEnroute site, ThingDef def) => Claims.TryGetValue(site, out var claims) && claims.TryGetValue(def, out int count) ? count : 0;
        public void SendReservations(IHaulEnroute from, IHaulEnroute to)
        { if (Claims.TryGetValue(from, out var claims)) { Claims.Remove(from); Claims[to] = claims; } }
    }
    public abstract class Blueprint : Thing, IConstructible
    {
        public List<ThingDefCountClass> Costs = new List<ThingDefCountClass>();
        public int FactoryCalls;
        public List<ThingDefCountClass> TotalMaterialCost() => Costs;
        protected abstract Thing MakeSolidThing(out bool shouldSelect);
    }
    public class Blueprint_Build : Blueprint, IHaulEnroute
    {
        public object Style, Glower;
        public Frame Made;
        Map IHaulEnroute.Map => Map;
        public int SpaceRemainingFor(ThingDef material) => Costs.Where(c => c.thingDef == material).Sum(c => c.count);
        protected override Thing MakeSolidThing(out bool shouldSelect)
        {
            FactoryCalls++; shouldSelect = false;
            Made = new Frame { Costs = Costs, Stuff = Stuff, Style = Style, Glower = Glower, width = width, height = height,
                def = new ThingDef { entityDefToBuild = def.entityDefToBuild, IsFrame = true,
                    ConstructionWidth = width, ConstructionHeight = height } };
            Map.enrouteManager.SendReservations(this, Made);
            return Made;
        }
        public new void Destroy() { base.Destroy(); Spawned = false; Map.listerThings.Things.Remove(this); }
    }
    public class Blueprint_Storage : Blueprint_Build
    {
        public object Settings, Group;
        protected override Thing MakeSolidThing(out bool shouldSelect)
        {
            var frame = (Frame)base.MakeSolidThing(out shouldSelect);
            frame.Settings = Settings; frame.Group = Group; Group = null;
            return frame;
        }
    }
    public class Blueprint_Install : Blueprint
    { protected override Thing MakeSolidThing(out bool shouldSelect) { shouldSelect = false; throw new Exception("Install blueprint must be excluded"); } }
    public class Frame : Building, IConstructible, IHaulEnroute, IThingHolder
    {
        public List<ThingDefCountClass> Costs = new List<ThingDefCountClass>();
        public ThingOwner resourceContainer;
        public object Style, Glower, Settings, Group;
        public float workDone;
        public Frame() { resourceContainer = new ThingOwner(this); }
        public List<ThingDefCountClass> TotalMaterialCost() => Costs;
        Map IHaulEnroute.Map => Map;
        public int SpaceRemainingFor(ThingDef material) => Math.Max(0, Costs.Where(c => c.thingDef == material).Sum(c => c.count) - resourceContainer.Where(t => t.def == material).Sum(t => t.stackCount));
        public ThingOwner GetDirectlyHeldThings() => resourceContainer;
        public void GetChildHolders(List<IThingHolder> children) { }
    }
    public static class GenConstruct
    {
        public static readonly Dictionary<Thing, Thing> Blockers = new Dictionary<Thing, Thing>();
        public static readonly Dictionary<Thing, List<Thing>> AdditionalBlockers = new Dictionary<Thing, List<Thing>>();
        public static readonly Dictionary<Thing, Thing> Walls = new Dictionary<Thing, Thing>();
        public static Thing FirstBlockingThing(Thing site, Pawn ignore) => Blockers.TryGetValue(site, out var blocker) ? blocker : null;
        public static bool BlocksConstruction(Thing site, Thing thing) =>
            (Blockers.TryGetValue(site, out var first) && first == thing) ||
            (AdditionalBlockers.TryGetValue(site, out var others) && others.Contains(thing));
        public static Thing GetWallAttachedTo(Thing site) => Walls.TryGetValue(site, out var wall) ? wall : null;
    }
}
namespace Verse.AI
{
    public static class EnrouteUtility
    {
        public static int GetSpaceRemainingWithEnroute(this RimWorld.IHaulEnroute site, ThingDef material) =>
            Math.Max(0, site.SpaceRemainingFor(material) - site.Map.enrouteManager.GetEnroute(site, material));
    }
}
