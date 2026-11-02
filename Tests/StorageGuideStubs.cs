using System;
using System.Collections.Generic;

namespace HarmonyLib
{
    public class Harmony { public Harmony(string id) { } public void Patch(object method, HarmonyMethod prefix, HarmonyMethod postfix) { } }
    public class HarmonyMethod { public HarmonyMethod(Type type, string name) { } }
    public static class AccessTools { public static object Method(Type type, string name) => null; }
}
namespace Verse
{
    public class StaticConstructorOnStartup : Attribute { }
    public class Game { public MagicStorage.GameComponent_StorageGuideNotice Progress; public T GetComponent<T>() where T : class => Progress as T; }
    public static class Current { public static Game Game; }
    public class GameComponent { public GameComponent() { } public virtual void ExposeData() { } }
    public static class Scribe_Values
    {
        public static bool Loading;
        public static readonly Dictionary<string, object> Data = new Dictionary<string, object>();
        public static void Look<T>(ref T value, string key, T defaultValue)
        { if (Loading) value = Data.TryGetValue(key, out var saved) ? (T)saved : defaultValue; else Data[key] = value; }
    }
    public class Map { }
    public struct IntVec3
    {
        public T GetFirstThing<T>(Map map) where T : class => null;
        public List<Thing> GetThingList(Map map) => new List<Thing>();
    }
    public class ThingDef { public string defName; public int stackLimit = 75; public object entityDefToBuild; public Type thingClass; public bool HasComp(Type type) => false; }
    public class Thing
    {
        public ThingDef def;
        public T TryGetComp<T>() where T : class => null;
        public int stackCount = 1;
        public bool Spawned, Destroyed;
        public Map Map;
        public IntVec3 Position, PositionHeld;
        public RimWorld.Faction Faction;
        public void SetFaction(RimWorld.Faction faction) { Faction = faction; }
    }
    public class Frame : Thing { public void CompleteConstruction() { } }
    public class MinifiedThing : Thing { public Thing InnerThing; }
    public static class ThingMaker { public static Thing MakeThing(ThingDef def) => new Thing { def = def }; }
    public static class DefDatabase<T> where T : class
    {
        public static readonly Dictionary<string, T> Defs = new Dictionary<string, T>();
        public static T GetNamed(string name) => Defs[name];
        public static T GetNamedSilentFail(string name) => Defs.TryGetValue(name, out var value) ? value : null;
    }
    public struct TargetInfo { public TargetInfo(IntVec3 cell, Map map) { } }
    public static class Translation { public static string Translate(this string text) => text; }
    public class LetterStack
    {
        public readonly List<string> Titles = new List<string>();
        public void ReceiveLetter(string title, string text, object def, object target) { Titles.Add(title); }
    }
    public static class Find { public static LetterStack LetterStack = new LetterStack(); }
}
namespace RimWorld
{
    using Verse;
    public class CompTransporter { }
    public class Faction { public static readonly Faction OfPlayer = new Faction(); }
    public static class ThingDefOf
    {
        public static ThingDef Steel = new ThingDef { defName = "Steel" };
        public static ThingDef ComponentIndustrial = new ThingDef { defName = "ComponentIndustrial", stackLimit = 50 };
    }
    public static class LetterDefOf { public static object PositiveEvent = new object(), NeutralEvent = new object(); }
    public static class MinifyUtility
    {
        public static MinifiedThing MakeMinified(this Thing thing) => new MinifiedThing { InnerThing = thing };
    }
    public static class DropCellFinder { public static IntVec3 TradeDropSpot(Map map) => new IntVec3(); }
    public static class DropPodUtility
    {
        public static readonly List<List<Thing>> Pods = new List<List<Thing>>();
        public static Map LastMap;
        public static void DropThingGroupsNear(IntVec3 cell, Map map, List<List<Thing>> groups,
            int openDelay, bool instaDrop, bool leaveSlag, bool canRoofPunch, bool forbid, bool allowFogged)
        {
            if (openDelay != 110 || instaDrop || leaveSlag || canRoofPunch || forbid || allowFogged)
                throw new Exception("Unexpected reward pod settings");
            LastMap = map;
            Pods.AddRange(groups);
        }
    }
}
namespace MagicStorage
{
    public class Building_StorageCore : Verse.Thing { }
    public static class StorageTransportGroups { public static bool IsPlayerPod(RimWorld.CompTransporter pod) => false; }
}
