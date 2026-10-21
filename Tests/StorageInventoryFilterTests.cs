using System;
using System.Collections.Generic;
using MagicStorage;
using Verse;

internal static class StorageInventoryFilterTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    {
        checks++;
        if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual);
    }
    public static int Main()
    {
        var food = new ThingDef(); var gun = new ThingDef(); var wood = new ThingDef();
        var meal = new Thing { def = food }; var weapon = new Thing { def = gun, Quality = 3 };
        var log = new Thing { def = wood };
        var foodCategory = new ThingCategoryDef(); foodCategory.Descendants.Add(food);
        var weaponCategory = new ThingCategoryDef(); weaponCategory.Descendants.Add(gun);
        var a = new StorageInventoryFilterOption(foodCategory);
        var b = new StorageInventoryFilterOption(weaponCategory);
        var rule = new StorageInventoryRule { Filter = new ThingFilter { Predicate = item => item.Quality >= 3 } };
        var c = new StorageInventoryFilterOption(rule);
        var options = new List<StorageInventoryFilterOption> { a, b, c };
        Equal(true, StorageInventoryFilters.Allows(log, options), "all-off shows all items");
        a.Toggle(false);
        Equal(true, StorageInventoryFilters.Allows(meal, options), "whitelist includes matching category");
        Equal(false, StorageInventoryFilters.Allows(weapon, options), "whitelist hides nonmatches");
        b.Toggle(false);
        Equal(true, StorageInventoryFilters.Allows(weapon, options), "multiple whitelists form a union");
        Equal(false, StorageInventoryFilters.Allows(log, options), "union does not include unrelated items");
        c.Toggle(true);
        Equal(false, StorageInventoryFilters.Allows(weapon, options), "blacklist takes priority over whitelist");
        Equal(true, StorageInventoryFilters.Allows(meal, options), "blacklist leaves unrelated whitelist match");
        options.Reverse();
        Equal(false, StorageInventoryFilters.Allows(weapon, options), "blacklist priority is independent of order");
        a.Toggle(false); b.Toggle(false);
        Equal(true, StorageInventoryFilters.Allows(log, options), "blacklist-only defaults to include");
        Equal(false, StorageInventoryFilters.Allows(weapon, options), "blacklist-only excludes matching items");
        c.Toggle(true);
        Equal(StorageInventoryFilterMode.Off, c.Mode, "same mouse button turns rule off");
        c.Toggle(false); c.Toggle(true);
        Equal(StorageInventoryFilterMode.Exclude, c.Mode, "opposite mouse button changes mode");
        c.Toggle(false);
        Equal(StorageInventoryFilterMode.Include, c.Mode, "switch back to whitelist");
        Equal(true, StorageInventoryFilters.Allows(new Thing { Inner = weapon }, options), "custom filter handles minified contents");
        c.Toggle(false); b.Toggle(false);
        Equal(true, StorageInventoryFilters.Allows(new Thing { Inner = weapon }, options), "category filter handles minified contents");
        Equal(false, StorageInventoryFilters.Allows(new Thing { def = gun, Quality = 0 },
            new[] { new StorageInventoryFilterOption(rule) { Mode = StorageInventoryFilterMode.Include } }), "custom rule uses individual quality");
        Equal(true, StorageInventoryFilters.CanGroup(meal, new Thing { def = food }, new StorageInventoryRule[0]), "same source without rule differences can group");
        Equal(false, StorageInventoryFilters.CanGroup(weapon, new Thing { def = gun, Quality = 0 }, new[] { rule }), "custom rule differences prevent grouping");
        Equal(false, StorageInventoryFilters.CanGroup(null, meal, new[] { rule }), "empty representatives cannot group");
        var settings = new MagicStorageSettings();
        Equal(false, settings.CaravanItemFilters, "caravan filters default off");
        Equal(false, settings.TradeItemFilters, "trade filters default off");
        Scribe.mode = LoadSaveMode.Saving;
        settings.CaravanItemFilters = true; settings.TradeItemFilters = false; settings.ExposeData();
        Scribe.mode = LoadSaveMode.LoadingVars;
        var loadedSettings = new MagicStorageSettings(); loadedSettings.ExposeData();
        Equal(true, loadedSettings.CaravanItemFilters, "caravan toggle persists");
        Equal(false, loadedSettings.TradeItemFilters, "trade toggle persists independently");
        Scribe.mode = LoadSaveMode.Inactive;
        settings.InventoryFilters.Add(rule);
        settings.RestoreDefaults();
        Equal(rule, settings.InventoryFilters[0], "resetting tuning settings preserves user rules");
        settings.ExposeData();
        Equal("storageInventoryFilters", Scribe_Collections.DeepKey, "rules are serialized by mod settings");
        Equal(true, ReferenceEquals(settings.InventoryFilters, Scribe_Collections.DeepValue), "mod settings serialize the rule library");
        var otherWindow = new StorageInventoryFilterOption(settings.InventoryFilters[0]);
        Equal(StorageInventoryFilterMode.Off, otherWindow.Mode, "new windows share definitions without sharing activation");
        rule.Filter.AllowedHitPointsPercents = new FloatRange(0.5f, 0.8f);
        Scribe.mode = LoadSaveMode.PostLoadInit;
        rule.ExposeData();
        Equal(0f, rule.Filter.AllowedHitPointsPercents.min, "durability minimum is unrestricted after loading");
        Equal(1f, rule.Filter.AllowedHitPointsPercents.max, "durability maximum is unrestricted after loading");
        ModFiltering();
        IconSelection();
        EditDraft();
        MissingModRoundTrip();
        Console.WriteLine("PASS: " + checks + " inventory filter assertions (native filter matching and save APIs stubbed).");
        return 0;
    }

    private static void ModFiltering()
    {
        var modA = new ModContentPack { PackageId = "author.modA" };
        var sameModA = new ModContentPack { PackageId = "AUTHOR.MODA" };
        var modB = new ModContentPack { PackageId = "author.modB" };
        var itemA = new Thing { def = new ThingDef { modContentPack = modA }, Quality = 3 };
        var itemB = new Thing { def = new ThingDef { modContentPack = modB }, Quality = 3 };
        var noSource = new Thing { def = new ThingDef() };
        var category = new ThingCategoryDef();
        category.Descendants.Add(itemA.def); category.Descendants.Add(itemB.def);
        var categoryOption = new StorageInventoryFilterOption(category);
        var rule = new StorageInventoryRule { Filter = new ThingFilter { Predicate = item => item.Quality >= 3 } };
        var customOption = new StorageInventoryFilterOption(rule);
        var options = new[] { categoryOption, customOption };
        Equal(true, StorageInventoryFilters.Allows(itemA, options, modA), "source alone includes matching items");
        Equal(false, StorageInventoryFilters.Allows(itemB, options, modA), "source alone excludes other mods");
        Equal(true, StorageInventoryFilters.Allows(itemA, options, sameModA), "source identity uses case-insensitive package ID");
        Equal(false, StorageInventoryFilters.Allows(noSource, options, modA), "unknown source does not match a selected mod");
        Equal(true, StorageInventoryFilters.Allows(noSource, options), "all mods keeps items without source metadata");
        categoryOption.Toggle(false);
        Equal(true, StorageInventoryFilters.Allows(itemA, options, modA), "source intersects category whitelist");
        Equal(false, StorageInventoryFilters.Allows(itemB, options, modA), "category whitelist cannot bypass the selected source");
        category.Descendants.Remove(itemA.def);
        Equal(false, StorageInventoryFilters.Allows(itemA, options, modA), "matching source cannot bypass category whitelist");
        customOption.Toggle(false);
        Equal(true, StorageInventoryFilters.Allows(itemA, options, modA), "custom whitelist still unions with category whitelist inside source");
        Equal(false, StorageInventoryFilters.Allows(itemB, options, modA), "multiple whitelists cannot include another mod");
        customOption.Toggle(true);
        category.Descendants.Add(itemA.def);
        Equal(false, StorageInventoryFilters.Allows(itemA, options, modA), "blacklist still overrides matching source and whitelist");
        categoryOption.Toggle(false);
        Equal(false, StorageInventoryFilters.Allows(itemA, options, modA), "blacklist alone still applies inside selected source");
        customOption.Toggle(true);
        var packedA = new Thing { def = new ThingDef { modContentPack = modB }, Inner = itemA };
        Equal(modA, StorageInventoryFilters.SourceOf(packedA), "source choices use the packed item's content source");
        Equal(true, StorageInventoryFilters.Allows(packedA, options, modA), "packed items match the inner source");
        Equal(false, StorageInventoryFilters.Allows(packedA, options, modB), "wrapper source does not match the packed item");
        Equal(true, StorageInventoryFilters.Allows(itemB, options), "resetting source to all restores other mods");
        Equal(false, StorageInventoryFilters.Allows(null, options, modA), "missing items cannot match a source");
    }

    private static void IconSelection()
    {
        var valid = new ThingDef { uiIcon = new object() };
        var hidden = new ThingDef { uiIcon = new object(), Hidden = true };
        var bad = new ThingDef { uiIcon = BaseContent.BadTex };
        var clear = new ThingDef { uiIcon = BaseContent.ClearTex };
        var unresolved = new ThingDef { uiIcon = new object(), MissingResolvedIcon = true };
        var iconRule = new StorageInventoryRule();
        iconRule.Filter.AllowedThingDefs.AddRange(new[] { null, new ThingDef(), bad, clear, hidden, unresolved, valid });
        Equal(valid, iconRule.ResolveIcon(), "automatic icon skips missing, invalid, transparent, hidden and unresolved entries");
        Equal(false, StorageInventoryRule.HasUsableIcon(null), "null def cannot be an icon");
        Equal(false, StorageInventoryRule.HasUsableIcon(bad), "error texture cannot be an icon");
        Equal(false, StorageInventoryRule.HasUsableIcon(clear), "transparent placeholder cannot be an icon");
        Equal(false, StorageInventoryRule.HasUsableIcon(unresolved), "missing resolved texture cannot be an icon");
        var custom = new ThingDef { uiIcon = new object() };
        iconRule.IconDef = custom;
        Equal(custom, iconRule.ResolveIcon(), "manual icon can be outside the allowed items");
        iconRule.ExposeData();
        Equal("iconDef", Scribe_Values.LastKey, "manual icon is serialized with the rule");
        Equal(custom.defName, Scribe_Values.LastValue, "selected item is persisted by name");
        iconRule.IconDef = null;
        Equal(valid, iconRule.ResolveIcon(), "clearing manual icon restores automatic selection");
        iconRule.IconDef = bad;
        Equal(valid, iconRule.ResolveIcon(), "invalid manual icon falls back to an allowed item");
        iconRule.IconDef = null;
        iconRule.Filter.AllowedThingDefs.Clear();
        Equal(null, iconRule.ResolveIcon(), "empty filter requests the generic fallback icon");
        iconRule.Filter.AllowedThingDefs.Add(null);
        Equal(null, iconRule.ResolveIcon(), "unresolved-only filter safely requests fallback icon");
    }

    private static void EditDraft()
    {
        var first = new ThingDef { uiIcon = new object() };
        var second = new ThingDef { uiIcon = new object() };
        var original = new StorageInventoryRule { Name = "original", IconDef = first };
        original.Filter.AllowedThingDefs.Add(first);
        var option = new StorageInventoryFilterOption(original) { Mode = StorageInventoryFilterMode.Exclude };
        ThingFilter originalFilter = original.Filter;
        StorageInventoryRule draft = original.Clone();
        draft.Name = "edited"; draft.IconDef = second;
        draft.Filter.AllowedThingDefs.Clear(); draft.Filter.AllowedThingDefs.Add(second);
        Equal("original", original.Name, "draft name does not mutate original before confirmation");
        Equal(first, original.IconDef, "draft icon does not mutate original before confirmation");
        Equal(first, original.Filter.AllowedThingDefs[0], "draft filter is independent from original");
        original.CopyFrom(draft);
        Equal(original, option.Rule, "editing preserves the rule object referenced by its button");
        Equal(StorageInventoryFilterMode.Exclude, option.Mode, "editing preserves active blacklist state");
        Equal(originalFilter, original.Filter, "commit preserves filter object identity");
        Equal("edited", original.Name, "commit updates rule name");
        Equal(second, original.IconDef, "commit updates icon");
        Equal(second, original.Filter.AllowedThingDefs[0], "commit updates allowed items");
        draft.Filter.AllowedThingDefs.Clear();
        Equal(1, original.Filter.AllowedThingDefs.Count, "committed filter does not share the draft list");
    }

    private static StorageInventoryRule ReadRule()
    {
        var rule = new StorageInventoryRule();
        Scribe.mode = LoadSaveMode.LoadingVars; rule.ExposeData();
        Scribe.mode = LoadSaveMode.PostLoadInit; rule.ExposeData();
        Scribe.mode = LoadSaveMode.Inactive;
        return rule;
    }

    private static void SaveRule(StorageInventoryRule rule)
    {
        Scribe.Data.Clear();
        Scribe.mode = LoadSaveMode.Saving; rule.ExposeData();
        Scribe.mode = LoadSaveMode.Inactive;
    }

    private static void MissingModRoundTrip()
    {
        var x = new ThingDef { defName = "A_X", uiIcon = new object() };
        var vanilla = new ThingDef { defName = "Vanilla", uiIcon = new object() };
        var special = new SpecialThingFilterDef { defName = "A_Special" };
        DefDatabase<ThingDef>.AllDefsListForReading.AddRange(new[] { x, vanilla });
        DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.Add(special);
        var original = new StorageInventoryRule { Name = "saved rule", IconDef = x };
        original.Filter.SetAllow(x, true); original.Filter.SetAllow(vanilla, true);
        original.Filter.SetAllow(special, false);
        original.Filter.AllowedQualityLevels = new RimWorld.QualityRange(2, 5);
        SaveRule(original);
        DefDatabase<ThingDef>.AllDefsListForReading.Remove(x);
        DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.Remove(special);
        int hardLookups = Scribe_Defs.Calls;
        var missing = ReadRule();
        Equal(hardLookups, Scribe_Defs.Calls, "missing mods do not use error-reporting Def deserialization");
        Equal("saved rule", missing.Name, "rule survives when mod A is absent");
        Equal(1, missing.Filter.AllowedThingDefs.Count, "missing item does not enter runtime filter as null");
        Equal(vanilla, missing.Filter.AllowedThingDefs[0], "installed item remains allowed");
        Equal(null, missing.IconDef, "missing manual icon is safely unresolved");
        Equal(vanilla, missing.ResolveIcon(), "missing manual icon falls back to an installed allowed item");
        Equal(original.Filter.AllowedQualityLevels, missing.Filter.AllowedQualityLevels, "quality range survives round trip");
        var draft = missing.Clone(); draft.Name = "renamed without A"; missing.CopyFrom(draft);
        missing.Filter.SetAllow(vanilla, false);
        SaveRule(missing);
        var names = (List<string>)Scribe.Data["filter/allowedDefs"];
        Equal(true, names.Contains(x.defName), "saving without A preserves missing item name");
        Equal(false, names.Contains(vanilla.defName), "deselecting an installed item is persisted");
        Equal(true, ((List<string>)Scribe.Data["filter/disallowedSpecialFilters"]).Contains(special.defName), "missing special-filter name is preserved");
        Equal(x.defName, Scribe.Data["iconDef"], "missing manual icon name is preserved");
        DefDatabase<ThingDef>.AllDefsListForReading.Add(x);
        DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.Add(special);
        var restored = ReadRule();
        Equal("renamed without A", restored.Name, "edits made while mod is absent survive");
        Equal(true, restored.Filter.Allows(new Thing { def = x }), "re-enabling A restores item X selection");
        Equal(false, restored.Filter.Allows(new Thing { def = vanilla }), "deselected installed item stays excluded");
        Equal(false, restored.Filter.Allows(special), "re-enabling A restores special-filter exclusion");
        Equal(x, restored.IconDef, "re-enabling A restores manually selected icon");
        DefDatabase<ThingDef>.AllDefsListForReading.Remove(x);
        var absentAgain = ReadRule();
        Equal(0, absentAgain.Filter.AllowedThingDefs.Count, "rule with only missing items has an empty runtime filter");
        Equal(null, absentAgain.ResolveIcon(), "rule with only missing items safely uses the generic icon");
        absentAgain.IconDef = null; SaveRule(absentAgain);
        DefDatabase<ThingDef>.AllDefsListForReading.Add(x);
        Equal(null, ReadRule().IconDef, "explicitly choosing automatic icon clears even a missing override");
    }
}
