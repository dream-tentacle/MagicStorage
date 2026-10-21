using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class StorageInventoryRule : IExposable
    {
        internal string Name;
        internal ThingFilter Filter = new ThingFilter();
        private List<string> allowedDefNames = new List<string>();
        private List<string> disallowedSpecialNames = new List<string>();
        private string iconDefName;
        private ThingDef iconDef;
        internal ThingDef IconDef
        {
            get => iconDef;
            set { iconDef = value; iconDefName = value?.defName; }
        }

        internal StorageInventoryRule Clone()
        {
            var copy = new StorageInventoryRule();
            copy.CopyFrom(this);
            return copy;
        }

        internal void CopyFrom(StorageInventoryRule source)
        {
            Name = source.Name;
            iconDef = source.iconDef;
            iconDefName = source.iconDefName;
            allowedDefNames = new List<string>(source.allowedDefNames);
            disallowedSpecialNames = new List<string>(source.disallowedSpecialNames);
            Filter.CopyAllowancesFrom(source.Filter);
        }

        internal ThingDef ResolveIcon()
        {
            if (HasUsableIcon(IconDef)) return IconDef;
            return Filter.AllowedThingDefs.FirstOrDefault(def =>
                HasUsableIcon(def) && !Find.HiddenItemsManager.Hidden(def));
        }

        internal static bool HasUsableIcon(ThingDef def)
        {
            if (def == null || def.uiIcon == null || def.uiIcon == BaseContent.BadTex || def.uiIcon == BaseContent.ClearTex)
                return false;
            var texture = Widgets.GetIconFor(def);
            return texture != null && texture != BaseContent.BadTex && texture != BaseContent.ClearTex;
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // Keep absent mods' names while replacing the selections of installed defs.
                allowedDefNames = NamesForSave(allowedDefNames, Filter.AllowedThingDefs);
                disallowedSpecialNames = NamesForSave(disallowedSpecialNames,
                    DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.Where(def => !Filter.Allows(def)));
            }
            Scribe_Values.Look(ref Name, "name");
            var qualities = Filter.AllowedQualityLevels;
            var mentalBreakChance = Filter.AllowedMentalBreakChance;
            // The native XML layout is retained, but defs are read as names, not hard references.
            if (Scribe.EnterNode("filter"))
            {
                try
                {
                    Scribe_Collections.Look(ref allowedDefNames, "allowedDefs", LookMode.Value);
                    Scribe_Collections.Look(ref disallowedSpecialNames, "disallowedSpecialFilters", LookMode.Value);
                    Scribe_Values.Look(ref qualities, "allowedQualityLevels");
                    Scribe_Values.Look(ref mentalBreakChance, "allowedMentalBreakChance");
                }
                finally { Scribe.ExitNode(); }
            }
            Scribe_Values.Look(ref iconDefName, "iconDef");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (allowedDefNames == null) allowedDefNames = new List<string>();
                if (disallowedSpecialNames == null) disallowedSpecialNames = new List<string>();
                Filter = new ThingFilter();
                Filter.SetDisallowAll();
                foreach (string name in allowedDefNames)
                {
                    var def = FindDef<ThingDef>(name);
                    if (def != null) Filter.SetAllow(def, true);
                }
                foreach (string name in disallowedSpecialNames)
                {
                    var def = FindDef<SpecialThingFilterDef>(name);
                    if (def != null) Filter.SetAllow(def, false);
                }
                Filter.AllowedQualityLevels = qualities;
                Filter.AllowedMentalBreakChance = mentalBreakChance;
                iconDef = FindDef<ThingDef>(iconDefName);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (Filter == null) Filter = new ThingFilter();
                Filter.AllowedHitPointsPercents = new FloatRange(0f, 1f);
            }
        }

        private static T FindDef<T>(string name) where T : Def, new() =>
            name.NullOrEmpty() || name == "null" ? null : DefDatabase<T>.GetNamedSilentFail(name);

        private static List<string> NamesForSave<T>(List<string> previous, IEnumerable<T> current) where T : Def, new() =>
            previous.Where(name => !name.NullOrEmpty() && name != "null" && FindDef<T>(name) == null)
                .Concat(current.Where(def => def != null).Select(def => def.defName)).Distinct().ToList();
    }

    internal enum StorageInventoryFilterMode { Off, Include, Exclude }

    internal sealed class StorageInventoryFilterOption
    {
        internal readonly ThingCategoryDef Category;
        internal readonly StorageInventoryRule Rule;
        internal StorageInventoryFilterMode Mode;

        internal StorageInventoryFilterOption(ThingCategoryDef category) { Category = category; }
        internal StorageInventoryFilterOption(StorageInventoryRule rule) { Rule = rule; }

        internal bool Matches(Thing item) => Category != null
            ? Category.ContainedInThisOrDescendant(item.GetInnerIfMinified().def)
            : Rule.Filter.Allows(item);

        internal void Toggle(bool blacklist)
        {
            var selected = blacklist ? StorageInventoryFilterMode.Exclude : StorageInventoryFilterMode.Include;
            Mode = Mode == selected ? StorageInventoryFilterMode.Off : selected;
        }
    }

    internal static class StorageInventoryFilters
    {
        internal static bool CanGroup(Thing left, Thing right, IEnumerable<StorageInventoryRule> rules) =>
            left != null && right != null &&
            string.Equals(SourceOf(left)?.PackageId, SourceOf(right)?.PackageId, StringComparison.OrdinalIgnoreCase) &&
            rules.All(rule => rule.Filter.Allows(left) == rule.Filter.Allows(right));

        internal static ModContentPack SourceOf(Thing item) => item?.GetInnerIfMinified()?.ContentSource;

        internal static bool Allows(Thing item, IEnumerable<StorageInventoryFilterOption> options, ModContentPack source = null)
        {
            if (item == null) return false;
            // Source narrows the existing rule result; it is independent of whitelist unions.
            if (source != null && !string.Equals(SourceOf(item)?.PackageId, source.PackageId, StringComparison.OrdinalIgnoreCase))
                return false;
            bool hasWhitelist = false, included = false;
            foreach (var option in options)
            {
                if (option.Mode == StorageInventoryFilterMode.Off) continue;
                if (option.Mode == StorageInventoryFilterMode.Include) hasWhitelist = true;
                if (!option.Matches(item)) continue;
                if (option.Mode == StorageInventoryFilterMode.Exclude) return false;
                included = true;
            }
            return !hasWhitelist || included;
        }
    }
}
