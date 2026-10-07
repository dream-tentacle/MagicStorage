// Minimal engine boundary for headless tests of the linked production code.
// These tests do not exercise RimWorld serialization, drawing, or mod callbacks.
using System;
using System.Collections.Generic;

namespace RimWorld { public static class MapMeshFlagDefOf { public static object Things; } }
namespace Verse
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class StaticConstructorOnStartupAttribute : Attribute { }
    public enum ThingCategory { Item, Building }
    public enum LookMode { Deep, Def, Value }
    public enum LoadSaveMode { Inactive, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Scribe_Deep { public static void Look<T>(ref T value, string key, params object[] args) { } }
    public static class Scribe_Collections { public static void Look<T>(ref List<T> value, string key, LookMode mode, params object[] args) { } }
    public static class Translation
    {
        public static readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public static string Translate(this string text, params object[] args) =>
            Values.TryGetValue(text, out string format) ? string.Format(format, args) : text;
    }
    public static class Log
    {
        public static readonly List<string> Messages = new List<string>();
        public static readonly List<string> Errors = new List<string>();
        public static void Warning(string text) { }
        public static void Message(string text) { Messages.Add(text); }
        public static void Error(string text) { Errors.Add(text); }
    }
    public static class Find { public static TickManager TickManager = new TickManager(); public static WindowStack WindowStack = new WindowStack(); public static Selector Selector = new Selector(); public static readonly List<Map> Maps = new List<Map>(); }
    public class TickManager { public int TicksGame = 1; }
    public partial struct IntVec3 : IEquatable<IntVec3>
    {
        public int x, z;
        public static readonly IntVec3 Invalid = new IntVec3(-1000, -1000);
        public IntVec3(int x, int z) { this.x = x; this.z = z; }
        public static IntVec3 operator +(IntVec3 a, IntVec3 b) => new IntVec3(a.x + b.x, a.z + b.z);
        public bool Equals(IntVec3 b) => x == b.x && z == b.z;
        public override bool Equals(object b) => b is IntVec3 value && Equals(value);
        public override int GetHashCode() => x * 397 ^ z;
        public int DistanceToSquared(IntVec3 b) => (x-b.x)*(x-b.x)+(z-b.z)*(z-b.z);
        public float DistanceTo(IntVec3 b) => (float)Math.Sqrt(DistanceToSquared(b));
    }
    public static class GenAdj
    {
        public static IntVec3[] CardinalDirections = { new IntVec3(1,0), new IntVec3(-1,0), new IntVec3(0,1), new IntVec3(0,-1) };
    }
    public interface IExposable { void ExposeData(); }
    public interface IThingHolder { IThingHolder ParentHolder { get; } ThingOwner GetDirectlyHeldThings(); void GetChildHolders(List<IThingHolder> list); }
    public partial class ThingDef { public int stackLimit = 75; public ThingCategory category; public bool destroyOnDrop; }
    public partial class Thing
    {
        private static int nextId;
        public int thingIDNumber = ++nextId;
        public ThingDef def = new ThingDef();
        public int stackCount = 1;
        public bool Destroyed, Spawned;
        public ThingOwner holdingOwner;
        public object ParentHolder => holdingOwner?.Owner;
        public Faction Faction;
        public Map Map { get; set; }
        public IntVec3 Position;
        public int width = 1, height = 1;
        public string variant;
        public IEnumerable<IntVec3> OccupiedRect()
        { for (int x = 0; x < width; x++) for (int z = 0; z < height; z++) yield return Position + new IntVec3(x,z); }
        public bool CanStackWith(Thing other) => !Destroyed && !other.Destroyed && def == other.def && variant == other.variant;
        public bool TryAbsorbStack(Thing other, bool respectStackLimit)
        {
            if (!CanStackWith(other)) return false;
            int take = respectStackLimit ? Math.Min(def.stackLimit - stackCount, other.stackCount) : other.stackCount;
            stackCount += take; other.stackCount -= take;
            if (other.stackCount == 0) { other.holdingOwner?.Remove(other); other.Destroyed = true; return true; }
            return false;
        }
        public Thing SplitOff(int count)
        {
            if (count >= stackCount) { holdingOwner?.Remove(this); if (Spawned) DeSpawn(); return this; }
            stackCount -= count;
            return new Thing { def = def, stackCount = count, variant = variant,
                Stuff = Stuff, HitPoints = HitPoints, MaxHitPoints = MaxHitPoints, quality = quality, hasQuality = hasQuality };
        }
    }
    public partial class ThingOwner : IEnumerable<Thing>
    {
        protected readonly List<Thing> items = new List<Thing>();
        protected int maxStacks = 999999;
        public bool dontTickContents;
        public IThingHolder Owner;
        public ThingOwner(IThingHolder owner) { Owner = owner; }
        public int Count => items.Count;
        public Thing this[int index] => items[index];
        public bool Contains(Thing item) => items.Contains(item);
        IEnumerator<Thing> IEnumerable<Thing>.GetEnumerator() => items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public virtual int GetCountCanAccept(Thing item, bool merge = true)
        {
            int space = Math.Max(0, maxStacks - Count) * item.def.stackLimit;
            if (merge) foreach (var stack in items) if (stack.CanStackWith(item)) space += stack.def.stackLimit - stack.stackCount;
            return Math.Min(item.stackCount, space);
        }
        public virtual bool TryAdd(Thing item, bool merge = true)
        {
            if (item.holdingOwner != null) return false;
            if (merge) foreach (var stack in items) if (stack.TryAbsorbStack(item, true)) return true;
            if (items.Count >= maxStacks) return false;
            items.Add(item); item.holdingOwner = this; NotifyAdded(item); return true;
        }
        public virtual int TryAdd(Thing item, int count, bool merge = true)
        {
            if (count <= 0) return 0;
            var part = item.SplitOff(Math.Min(count, item.stackCount));
            int amount = part.stackCount;
            return TryAdd(part, merge) ? amount : 0;
        }
        public Thing Take(Thing item, int count) => Contains(item) ? item.SplitOff(count) : null;
        public virtual bool Remove(Thing item)
        {
            if (!items.Remove(item)) return false;
            item.holdingOwner = null; NotifyRemoved(item); return true;
        }
        protected virtual void NotifyAdded(Thing item) { }
        protected virtual void NotifyRemoved(Thing item) { }
    }
    public class ThingOwner<T> : ThingOwner where T : Thing
    { public ThingOwner(IThingHolder owner, bool single = false, LookMode mode = LookMode.Deep) : base(owner) { } }
    public partial class Map : IThingHolder
    {
        public MapPawns mapPawns = new MapPawns();
        public MapDrawer mapDrawer = new MapDrawer();
        public ReservationManager reservationManager = new ReservationManager();
        public object component;
        public MagicStorage.MapComponent_CosmicCrafting craftingComponent;
        public T GetComponent<T>() => typeof(T) == typeof(MagicStorage.MapComponent_CosmicCrafting)
            ? (T)(object)(craftingComponent ?? (craftingComponent = new MagicStorage.MapComponent_CosmicCrafting(this))) : (T)component;
        public IThingHolder ParentHolder => null;
        public ThingOwner GetDirectlyHeldThings() => null;
        public void GetChildHolders(List<IThingHolder> list) { }
    }
    public class MapDrawer { public void MapMeshDirty(IntVec3 cell, object flag, bool adjacent, bool sections) { } }
    public class MapComponent
    {
        public Map map;
        public MapComponent(Map map) { this.map = map; }
        public virtual void MapComponentTick() { }
        public virtual void ExposeData() { }
        public virtual void FinalizeInit() { }
    }
}
namespace MagicStorage
{
    using Verse;
    public class CompStorageNode { public Thing parent; public StorageNetwork Network; }
    public class Building_StorageUnit : Thing, IThingHolder
    {
        public int SlotCapacity = 64;
        public CompStorageNode node;
        public StorageNetwork Network => node?.Network;
        public StorageInventory Inventory;
        public Building_StorageUnit() { Inventory = new StorageInventory(this); }
        public new IThingHolder ParentHolder => Map;
        public ThingOwner GetDirectlyHeldThings() => Inventory.Contents;
        public void GetChildHolders(List<IThingHolder> children) { }
    }
}
