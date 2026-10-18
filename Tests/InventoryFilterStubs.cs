using System;
using System.Collections.Generic;

namespace Verse
{
    public interface IExposable { void ExposeData(); }
    public abstract class ModSettings { public virtual void ExposeData() { } }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public enum LookMode { Deep, Value }
    public static class Scribe
    {
        public static LoadSaveMode mode;
        public static readonly Dictionary<string, object> Data = new Dictionary<string, object>();
        private static readonly List<string> path = new List<string>();
        public static string Key(string key) => string.Join("/", path) + (path.Count > 0 ? "/" : "") + key;
        public static bool EnterNode(string key) { if (mode == LoadSaveMode.Inactive) return false; path.Add(key); return true; }
        public static void ExitNode() { path.RemoveAt(path.Count - 1); }
    }
    public static class Scribe_Values
    {
        public static string LastKey;
        public static object LastValue;
        public static void Look<T>(ref T value, string key, T defaultValue = default(T))
        {
            LastKey = key; LastValue = value;
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[Scribe.Key(key)] = value;
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
                value = Scribe.Data.TryGetValue(Scribe.Key(key), out object saved) && saved != null ? (T)saved : defaultValue;
        }
    }
    public static class Scribe_Deep { public static void Look<T>(ref T value, string key) { } }
    public static class Scribe_Defs
    {
        public static int Calls;
        public static string LastKey;
        public static object LastValue;
        public static void Look<T>(ref T value, string key) { Calls++; LastKey = key; LastValue = value; }
    }
    public static class Scribe_Collections
    {
        public static string DeepKey;
        public static object DeepValue;
        public static void Look<T>(ref List<T> value, string key, LookMode mode)
        {
            if (mode == LookMode.Deep) { DeepKey = key; DeepValue = value; return; }
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[Scribe.Key(key)] = value == null ? null : new List<T>(value);
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
                value = Scribe.Data.TryGetValue(Scribe.Key(key), out object saved) && saved != null ? new List<T>((IEnumerable<T>)saved) : null;
        }
    }
    public struct FloatRange
    {
        public float min, max;
        public FloatRange(float min, float max) { this.min = min; this.max = max; }
        public static FloatRange ZeroToOne => new FloatRange(0f, 1f);
    }
    public class Def
    {
        private static int next;
        public string defName = "Def" + ++next;
    }
    public class ModContentPack { public string PackageId; }
    public class ThingDef : Def { public object uiIcon; public bool Hidden, MissingResolvedIcon; public ModContentPack modContentPack; }
    public class SpecialThingFilterDef : Def { }
    public static class DefDatabase<T> where T : Def, new()
    {
        public static readonly List<T> AllDefsListForReading = new List<T>();
        public static T GetNamedSilentFail(string name) => AllDefsListForReading.Find(def => def.defName == name);
    }
    public static class StringExtensions { public static bool NullOrEmpty(this string text) => string.IsNullOrEmpty(text); }
    public static class BaseContent
    {
        public static readonly object BadTex = new object(), ClearTex = new object();
    }
    public static class Widgets
    {
        public static object GetIconFor(ThingDef def) => def.MissingResolvedIcon ? null : def.uiIcon;
    }
    public class HiddenItemsManager
    {
        public bool Hidden(ThingDef def)
        {
            if (def == null) throw new ArgumentNullException("key");
            return def.Hidden;
        }
    }
    public static class Find { public static readonly HiddenItemsManager HiddenItemsManager = new HiddenItemsManager(); }
    public class Thing
    {
        public ThingDef def;
        public Thing Inner;
        public int Quality;
        public virtual ModContentPack ContentSource => def?.modContentPack;
    }
    public class ThingCategoryDef
    {
        public readonly HashSet<ThingDef> Descendants = new HashSet<ThingDef>();
        public bool ContainedInThisOrDescendant(ThingDef def) => Descendants.Contains(def);
    }
    public class ThingFilter
    {
        public readonly List<ThingDef> AllowedThingDefs = new List<ThingDef>();
        private readonly HashSet<SpecialThingFilterDef> disallowed = new HashSet<SpecialThingFilterDef>();
        public FloatRange AllowedHitPointsPercents;
        public FloatRange AllowedMentalBreakChance = FloatRange.ZeroToOne;
        public RimWorld.QualityRange AllowedQualityLevels = RimWorld.QualityRange.All;
        public Func<Thing, bool> Predicate;
        public bool Allows(Thing item) => Predicate != null ? Predicate(item.Inner ?? item) : AllowedThingDefs.Contains((item.Inner ?? item).def);
        public bool Allows(SpecialThingFilterDef def) => !disallowed.Contains(def);
        public void SetDisallowAll() { AllowedThingDefs.Clear(); disallowed.Clear(); }
        public void SetAllow(ThingDef def, bool allowed)
        { if (allowed) { if (!AllowedThingDefs.Contains(def)) AllowedThingDefs.Add(def); } else AllowedThingDefs.Remove(def); }
        public void SetAllow(SpecialThingFilterDef def, bool allowed)
        { if (allowed) disallowed.Remove(def); else disallowed.Add(def); }
        public void CopyAllowancesFrom(ThingFilter other)
        {
            AllowedThingDefs.Clear();
            AllowedThingDefs.AddRange(other.AllowedThingDefs);
            AllowedHitPointsPercents = other.AllowedHitPointsPercents;
            AllowedMentalBreakChance = other.AllowedMentalBreakChance;
            AllowedQualityLevels = other.AllowedQualityLevels;
            disallowed.Clear(); foreach (var def in other.disallowed) disallowed.Add(def);
            Predicate = other.Predicate;
        }
    }
}
namespace MagicStorage
{
    public enum StorageRefreshTask { TestTask, Count }
}
namespace RimWorld
{
    public struct QualityRange
    {
        public int min, max;
        public QualityRange(int min, int max) { this.min = min; this.max = max; }
        public static QualityRange All => new QualityRange(0, 6);
    }
    public static class MinifiedThingUtility
    {
        public static Verse.Thing GetInnerIfMinified(this Verse.Thing item) => item.Inner ?? item;
    }
}
