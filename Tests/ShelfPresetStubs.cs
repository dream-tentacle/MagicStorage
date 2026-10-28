using System;
using System.Collections.Generic;

namespace Verse
{
    public class Def { public string defName; }
    public class ThingDef : Def { }
    public class SpecialThingFilterDef : Def { }
    public static class DefDatabase<T> where T : Def, new()
    {
        public static readonly List<T> AllDefsListForReading = new List<T>();
        public static T GetNamedSilentFail(string name) => AllDefsListForReading.Find(def => def.defName == name);
    }
    public static class GenFilePaths { public static string SaveDataFolderPath; }
    public struct FloatRange
    {
        public float min, max;
        public FloatRange(float min, float max) { this.min = min; this.max = max; }
    }
    public class ThingFilter
    {
        public readonly HashSet<ThingDef> AllowedThingDefs = new HashSet<ThingDef>();
        private readonly HashSet<SpecialThingFilterDef> disallowed = new HashSet<SpecialThingFilterDef>();
        public RimWorld.QualityRange AllowedQualityLevels = RimWorld.QualityRange.All;
        public FloatRange AllowedHitPointsPercents = new FloatRange(0, 1);
        public FloatRange AllowedMentalBreakChance = new FloatRange(0, 1);
        public bool Allows(SpecialThingFilterDef def) => !disallowed.Contains(def);
        public void SetAllow(ThingDef def, bool allow)
        { if (allow) AllowedThingDefs.Add(def); else AllowedThingDefs.Remove(def); }
        public void SetAllow(SpecialThingFilterDef def, bool allow)
        { if (allow) disallowed.Remove(def); else disallowed.Add(def); }
    }
}
namespace RimWorld
{
    public enum QualityCategory { Awful, Poor, Normal, Good, Excellent, Masterwork, Legendary }
    public enum StoragePriority { Unstored, Low, Normal, Preferred, Important, Critical }
    public struct QualityRange
    {
        public QualityCategory min, max;
        public QualityRange(QualityCategory min, QualityCategory max) { this.min = min; this.max = max; }
        public static QualityRange All => new QualityRange(QualityCategory.Awful, QualityCategory.Legendary);
    }
}
namespace MagicStorage
{
    // Only game boundary state is stubbed; the linked production XML and filesystem codec is exercised.
    public class StorageShelfFilter
    {
        public RimWorld.QualityRange Quality = RimWorld.QualityRange.All;
        public Verse.FloatRange HitPoints = new Verse.FloatRange(0, 1);
        public bool AnyMaterial = true, WarnWhenRestockFails;
        public List<Verse.ThingDef> Materials = new List<Verse.ThingDef>();
    }
    public class Building_StorageSupplyShelf
    {
        internal sealed class ShelfPreset
        {
            internal List<Verse.ThingDef> Selections = new List<Verse.ThingDef>();
            internal List<int> RestockLimits = new List<int>(), FilterLimits = new List<int>();
            internal List<StorageShelfFilter> Filters = new List<StorageShelfFilter>();
            internal RimWorld.StoragePriority Priority;
            internal bool FilterMode, DistinctItems, LowTemperatureMode;
            internal readonly Verse.ThingFilter SharedFilter = new Verse.ThingFilter();
        }
    }
}
