// Controlled native boundaries based on the RimSage 1.6/1.7 source. Tests execute
// the production transpiler through real Harmony, without starting the game.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace UnityEngine { public sealed class Event { } }
namespace Verse
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StaticConstructorOnStartupAttribute : Attribute { }
    public static class Find { public static Map CurrentMap; }
    public static class DebugSettings { public static bool godMode; }
    public static class Log { public static readonly List<string> Errors = new List<string>(); public static void Error(string text) => Errors.Add(text); }
    public sealed class Faction { public static readonly Faction OfPlayer = new Faction(); }
    public sealed class StuffProperties
    {
        public float commonality;
        public string Category = "Metal";
        public bool CanMake(ThingDef building) => building.MadeFromStuff && building.stuffCategories.Contains(Category);
    }
    public sealed class ThingDef
    {
        public bool IsStuff = true, MadeFromStuff;
        public float BaseMarketValue;
        public StuffProperties stuffProps = new StuffProperties();
        public readonly List<string> stuffCategories = new List<string> { "Metal" };
    }
    public sealed class Thing
    { public ThingDef def; public int stackCount = 1; public bool Destroyed; public object holdingOwner; }
    public sealed class ListerThings
    {
        public readonly Dictionary<ThingDef, List<Thing>> ByDef = new Dictionary<ThingDef, List<Thing>>();
        private readonly List<Thing> empty = new List<Thing>();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public List<Thing> ThingsOfDef(ThingDef def) => ByDef.TryGetValue(def, out var list) ? list : empty;
    }
    public struct IntVec3
    {
        public bool InFog;
        public bool Fogged(Map map) => InFog;
    }
    public sealed class Map
    {
        public readonly ListerThings listerThings = new ListerThings();
        public readonly RimWorld.ResourceCounter resourceCounter = new RimWorld.ResourceCounter();
        public MagicStorage.MapComponent_StorageNetworks Manager = new MagicStorage.MapComponent_StorageNetworks();
        public T GetComponent<T>() where T : class => Manager as T;
    }
}
namespace MagicStorage
{
    using Verse;
    public sealed class MapComponent_StorageNetworks
    { public readonly List<Building_StorageUnit> StorageUnits = new List<Building_StorageUnit>(); }
    public sealed class Building_StorageUnit
    {
        public Map Map;
        public Faction Faction = Faction.OfPlayer;
        public bool Spawned = true, Destroyed;
        public IntVec3 Position;
        public readonly StorageInventory Inventory = new StorageInventory();
    }
    public sealed class StorageInventory
    {
        public readonly object Contents = new object();
        public readonly Dictionary<ThingDef, List<Thing>> Stacks = new Dictionary<ThingDef, List<Thing>>();
        public int CountQueries;
        public long GetTotalCount(ThingDef def)
        { CountQueries++; return Stacks.TryGetValue(def, out var list) ? list.Sum(t => (long)t.stackCount) : 0; }
        public Thing StackAt(ThingDef def, int index) => Stacks.TryGetValue(def, out var list) && index < list.Count ? list[index] : null;
    }
}
namespace RimWorld
{
    using Verse;
    public sealed class ResourceCounter
    { public readonly Dictionary<ThingDef, int> AllCountedAmounts = new Dictionary<ThingDef, int>(); }
    public sealed class Designator_Build
    {
        public bool CanInteract = true;
        public int Rejections, NonStuffInputs;
        public readonly ThingDef Building = new ThingDef { MadeFromStuff = true };
        public readonly List<ThingDef> Options = new List<ThingDef>();
        public Map Map => Find.CurrentMap;

        // Retain the original filtering, short circuits, ordering and empty-menu exit.
        // UI creation is reduced to recording which material options would be offered.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ProcessInput(UnityEngine.Event ev)
        {
            if (!CanInteract) return;
            if (Building.MadeFromStuff)
            {
                var options = new List<ThingDef>();
                foreach (ThingDef item in Map.resourceCounter.AllCountedAmounts.Keys
                    .OrderByDescending(d => d.stuffProps?.commonality ?? float.PositiveInfinity).ThenBy(d => d.BaseMarketValue))
                {
                    if (item.IsStuff && item.stuffProps.CanMake(Building) &&
                        (DebugSettings.godMode || Map.listerThings.ThingsOfDef(item).Count > 0))
                        options.Add(item);
                }
                if (options.Count == 0) { Rejections++; return; }
                Options.AddRange(options);
            }
            else NonStuffInputs++;
        }
    }
}
