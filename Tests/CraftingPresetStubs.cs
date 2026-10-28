using System;
using System.Collections.Generic;

namespace Verse
{
    public class Def { public string defName; }
    public class ThingDef : Def { }
    public class SpecialThingFilterDef : Def { }
    public class RecipeDef : Def
    {
        public ThingFilter defaultIngredientFilter = new ThingFilter(), fixedIngredientFilter = new ThingFilter();
        public List<RimWorld.Precept_ThingStyle> Precepts = new List<RimWorld.Precept_ThingStyle>();
    }
    public static class DefDatabase<T> where T : Def, new()
    {
        public static readonly List<T> AllDefsListForReading = new List<T>();
        public static T GetNamedSilentFail(string name) => AllDefsListForReading.Find(def => def.defName == name);
    }
    public class Pawn { }
    public static class GenFilePaths { public static string SaveDataFolderPath; }
    public struct FloatRange { public float min, max; public FloatRange(float min, float max) { this.min = min; this.max = max; } }
    public struct IntRange { public int min, max; public IntRange(int min, int max) { this.min = min; this.max = max; } }
    public interface IExposable { void ExposeData(); }
    public enum LookMode { Deep }
    public enum LoadSaveMode { Inactive, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Scribe_Values { public static void Look<T>(ref T value, string name, T defaultValue = default(T)) { } }
    public static class Scribe_References { public static void Look<T>(ref T value, string name) { } }
    public static class Scribe_Defs { public static void Look<T>(ref T value, string name) { } }
    public static class Scribe_Deep { public static void Look<T>(ref T value, string name) { } }
    public static class Scribe_Collections { public static void Look<T>(ref List<T> value, string name, LookMode mode) { } }
    public static class CollectionExtensions
    {
        public static int Count<T>(this List<T> source, Func<T, bool> predicate) => System.Linq.Enumerable.Count(source, predicate);
    }
    public class ThingFilter
    {
        public readonly HashSet<ThingDef> AllowedThingDefs = new HashSet<ThingDef>();
        private readonly HashSet<SpecialThingFilterDef> disallowed = new HashSet<SpecialThingFilterDef>();
        public RimWorld.QualityRange AllowedQualityLevels = RimWorld.QualityRange.All;
        public FloatRange AllowedHitPointsPercents = new FloatRange(0, 1), AllowedMentalBreakChance = new FloatRange(0, 1);
        public bool Allows(SpecialThingFilterDef def) => !disallowed.Contains(def);
        public void SetAllow(ThingDef def, bool allow) { if (allow) AllowedThingDefs.Add(def); else AllowedThingDefs.Remove(def); }
        public void SetAllow(SpecialThingFilterDef def, bool allow) { if (allow) disallowed.Remove(def); else disallowed.Add(def); }
        public void SetDisallowAll() { AllowedThingDefs.Clear(); disallowed.Clear(); }
        public void CopyAllowancesFrom(ThingFilter other)
        {
            AllowedThingDefs.Clear(); AllowedThingDefs.UnionWith(other.AllowedThingDefs);
            disallowed.Clear(); disallowed.UnionWith(other.disallowed);
            AllowedQualityLevels = other.AllowedQualityLevels; AllowedHitPointsPercents = other.AllowedHitPointsPercents;
            AllowedMentalBreakChance = other.AllowedMentalBreakChance;
        }
    }
}
namespace RimWorld
{
    public enum QualityCategory { Awful, Poor, Normal, Good, Excellent, Masterwork, Legendary }
    public struct QualityRange
    {
        public QualityCategory min, max;
        public QualityRange(QualityCategory min, QualityCategory max) { this.min = min; this.max = max; }
        public static QualityRange All => new QualityRange(QualityCategory.Awful, QualityCategory.Legendary);
    }
    public class Precept_ThingStyle { public Verse.Def def; public Verse.ThingDef ThingDef; }
}
namespace MagicStorage
{
    public sealed class CraftingBatch
    {
        public float Progress => .5f;
        public bool CanCancel = true;
        public int CancelCalls;
        public bool Cancel(bool tryStore = true) { CancelCalls++; return CanCancel; }
    }
    public sealed class MagicStorageSettings { public bool NewWorkbenchOrdersFirst; }
    public static class MagicStorageMod { public static MagicStorageSettings Settings = new MagicStorageSettings(); }
    public static class CraftingRecipeCatalog
    {
        public static IEnumerable<RimWorld.Precept_ThingStyle> PreceptsFor(Verse.RecipeDef recipe) => recipe.Precepts;
    }
    public static class CraftingOrderLoadNotice { public static void Report(int count) { } }
}
