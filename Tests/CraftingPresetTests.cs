using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class CraftingPresetTests
{
    private static int checks;
    private static readonly ThingDef steel = new ThingDef { defName = "Steel" }, wood = new ThingDef { defName = "Wood" };
    private static readonly RecipeDef first = new RecipeDef { defName = "MakeFirst" }, second = new RecipeDef { defName = "MakeSecond" };
    private static readonly SpecialThingFilterDef special = new SpecialThingFilterDef { defName = "AllowFresh" };
    private static void Equal<T>(T expected, T actual, string message)
    {
        checks++;
        if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual);
    }
    public static int Main()
    {
        var culture = CultureInfo.CurrentCulture;
        string testRoot = Path.GetFullPath(Path.Combine("Tests", "bin", "CraftingPresetData-" + Guid.NewGuid().ToString("N")));
        GenFilePaths.SaveDataFolderPath = testRoot;
        try
        {
            DefDatabase<ThingDef>.AllDefsListForReading.AddRange(new[] { steel, wood });
            DefDatabase<RecipeDef>.AllDefsListForReading.AddRange(new[] { first, second });
            DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.Add(special);
            first.defaultIngredientFilter.SetAllow(wood, true);
            RoundTripAndFiles(); ApplyAndIsolation(); MissingAndValidation(); WorkbenchRestrictions();
            Console.WriteLine("Crafting presets: " + checks + " checks passed.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            // This test owns a flat preset directory under its unique workspace test root.
            string testParent = Path.GetFullPath(Path.Combine("Tests", "bin")) + Path.DirectorySeparatorChar;
            if (!testRoot.StartsWith(testParent, StringComparison.OrdinalIgnoreCase)) throw new Exception("Unexpected test path.");
            if (Directory.Exists(CraftingPresets.FolderPath))
            {
                foreach (string file in Directory.GetFiles(CraftingPresets.FolderPath)) File.Delete(file);
                Directory.Delete(CraftingPresets.FolderPath);
                Directory.Delete(Path.GetDirectoryName(CraftingPresets.FolderPath));
                Directory.Delete(testRoot);
            }
        }
    }

    private static CraftingOrderList Sample()
    {
        MagicStorageMod.Settings.NewWorkbenchOrdersFirst = false;
        var list = new CraftingOrderList { GroupingMode = true, UngroupedCollapsed = true };
        var a = list.Add(first); var b = list.Add(second); var c = list.Add(first);
        var group = list.CreateGroup(new[] { a, c }, "Tools <&>");
        group.Collapsed = true;
        list.MoveGroup(null, group, true);
        a.Mode = CraftingRepeatMode.TargetCount; a.TargetCount = 60; a.RepeatCount = 13; a.UnpauseAt = 12;
        a.Suspended = true; a.PauseWhenSatisfied = true; a.Paused = true; a.DropProductsOnFloor = true;
        a.Worker = new Pawn(); a.WorkerKind = CraftingWorkerKind.NonMechs; a.SkillRange = new IntRange(4, 21);
        a.IncludeEquipped = true; a.IncludeTainted = false; a.LimitToAllowedStuff = true;
        a.HitPoints = new FloatRange(.15f, .85f); a.Quality = new QualityRange(QualityCategory.Good, QualityCategory.Legendary);
        a.Ingredients.SetDisallowAll(); a.Ingredients.SetAllow(steel, true); a.Ingredients.SetAllow(special, false);
        a.Ingredients.AllowedQualityLevels = new QualityRange(QualityCategory.Normal, QualityCategory.Excellent);
        a.Ingredients.AllowedHitPointsPercents = new FloatRange(.3f, .9f);
        a.Ingredients.AllowedMentalBreakChance = new FloatRange(.1f, .7f);
        a.AdditionalCounts.SetAllow(wood, true); a.AdditionalCounts.SetAllow(special, false);
        a.AdditionalCounts.AllowedQualityLevels = new QualityRange(QualityCategory.Poor, QualityCategory.Good);
        a.AdditionalCounts.AllowedHitPointsPercents = new FloatRange(.2f, .8f);
        a.AdditionalCounts.AllowedMentalBreakChance = new FloatRange(.4f, .6f);
        a.Batch = new CraftingBatch();
        b.Mode = CraftingRepeatMode.Forever; b.WorkerKind = CraftingWorkerKind.Printers;
        c.Mode = CraftingRepeatMode.RepeatCount; c.RepeatCount = 7;
        return list;
    }

    private static void RoundTripAndFiles()
    {
        Equal(0, CraftingPresets.List().Count, "listing before any saves creates no files");
        var list = Sample(); var preset = CraftingPreset.Capture(list);
        Equal(null, preset.Orders[0].Worker, "capture omits assigned colonist");
        Equal(null, preset.Orders[0].Batch, "capture omits batch and invested materials");
        Equal(false, preset.Orders[0].Paused, "capture resets transient pause state");
        Equal(true, list.Orders[0].Paused, "capture leaves running order unchanged");
        list.Orders[0].Ingredients.SetAllow(wood, true);
        list.RenameGroup(list.GroupsInUse().Single(), "Changed later");
        Equal(false, preset.Orders[0].Ingredients.AllowedThingDefs.Contains(wood), "snapshot filters are independent");
        Equal("Tools <&>", preset.Groups[0].Name, "snapshot names are independent");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        var xml = CraftingPresets.Encode("My <&> ../queue", preset);
        List<string> missing;
        var loaded = CraftingPresets.Decode(XElement.Parse(xml.ToString()), out missing);
        Equal(0, missing.Count, "all definitions resolved");
        Equal(xml.ToString(), CraftingPresets.Encode("My <&> ../queue", loaded).ToString(), "all serialized fields round trip across cultures");
        Equal("MakeFirst,MakeFirst,MakeSecond", string.Join(",", loaded.Orders.Select(o => o.Recipe.defName)), "production priority is preserved");
        Equal(null, loaded.Groups[1], "ungrouped position survives round trip");
        CraftingPresets.Save("My <&> ../queue", preset);
        var entry = CraftingPresets.List().Single();
        Equal("My <&> ../queue", entry.Name, "names are XML data, not paths");
        Equal(CraftingPresets.FolderPath, Path.GetDirectoryName(entry.Path), "name cannot escape preset directory");
        var disk = CraftingPresets.Load(entry, out missing);
        Equal(xml.ToString(), CraftingPresets.Encode(entry.Name, disk).ToString(), "filesystem round trip");
        CraftingPresets.Save("Renamed", disk, entry);
        Equal(1, CraftingPresets.List().Count, "overwrite does not create a duplicate file");
        Equal("Renamed", CraftingPresets.List().Single().Name, "overwrite replaces metadata");
        Equal(0, Directory.GetFiles(CraftingPresets.FolderPath, "*.tmp").Length, "atomic save leaves no temporary files");
        string original = File.ReadAllText(entry.Path);
        File.WriteAllText(entry.Path, "<!DOCTYPE data [<!ENTITY x SYSTEM 'file:///unreadable'>]><data>&x;</data>");
        bool rejected = false;
        try { CraftingPresets.Load(entry, out missing); } catch (XmlException) { rejected = true; }
        Equal(true, rejected, "external XML entities are rejected");
        File.WriteAllText(entry.Path, original);
    }

    private static void ApplyAndIsolation()
    {
        var preset = CraftingPreset.Capture(Sample());
        var target = new CraftingOrderList(); var original = target.Add(second);
        var oldBatch = original.Batch = new CraftingBatch();
        MagicStorageMod.Settings.NewWorkbenchOrdersFirst = true;
        Equal(true, target.ApplyPreset(preset, false), "append succeeds");
        Equal(original, target.Orders[0], "append leaves existing task priority unchanged");
        Equal(oldBatch, original.Batch, "append keeps running batch");
        Equal(0, oldBatch.CancelCalls, "append does not cancel old work");
        Equal("MakeSecond,MakeSecond,MakeFirst,MakeFirst", string.Join(",", target.Orders.Select(o => o.Recipe.defName)), "append merges the ungrouped block and appends new groups");
        Equal(4, target.Orders.Select(o => o.Id).Distinct().Count(), "imported task IDs are unique");
        Equal(false, target.GroupingMode, "append preserves target view mode");
        var imported = target.Orders.First(o => o.Recipe == first);
        Equal(null, imported.Batch, "import does not reuse work in progress");
        Equal(null, imported.Worker, "import has no colonist reference");
        Equal(false, imported.Paused, "import recomputes pause state");
        Equal(true, imported.Suspended, "manual suspension is preserved");
        Equal(true, imported.DropProductsOnFloor, "drop destination is preserved");
        Equal(CraftingWorkerKind.NonMechs, imported.WorkerKind, "worker kind preserved");
        Equal(21, imported.SkillRange.max, "unlimited skill range preserved");
        Equal(true, imported.LimitToAllowedStuff, "material counting restriction preserved");
        Equal(60, imported.TargetCount, "target quantity preserved");
        Equal(12, imported.UnpauseAt, "resume threshold preserved");
        Equal(false, imported.Ingredients.Allows(special), "special material filter preserved");
        Equal(false, imported.IncludeTainted, "tainted counting preserved");
        Equal(true, target.ApplyPreset(preset, false), "same preset can append again");
        Equal(7, target.Orders.Select(o => o.Id).Distinct().Count(), "repeat import never collides with task IDs");
        Equal(2, target.GroupsInUse().Select(g => g.Id).Distinct().Count(), "repeat import creates independent groups");
        imported.Ingredients.SetAllow(wood, true);
        Equal(false, preset.Orders[0].Ingredients.AllowedThingDefs.Contains(wood), "editing import cannot mutate snapshot");
        int oldMaxId = target.Orders.Max(o => o.Id);
        Equal(true, target.ApplyPreset(preset, true), "replace succeeds after refund");
        Equal(1, oldBatch.CancelCalls, "replacement invokes material return on old batch");
        Equal(null, target.Find(original.Id), "stale jobs cannot find old task");
        Equal(true, target.Orders.All(o => o.Id > oldMaxId), "replacement does not reuse job identities");
        Equal(3, target.Orders.Count, "replacement removes previous queue");
        Equal("MakeFirst,MakeFirst,MakeSecond", string.Join(",", target.Orders.Select(o => o.Recipe.defName)), "replace preserves saved queue regardless of new-order preference");
        Equal(true, target.GroupingMode, "replacement restores grouping mode");
        Equal(true, target.UngroupedCollapsed, "replacement restores ungrouped collapse");
        Equal(true, target.GroupsInUse().Single().Collapsed, "replacement restores group collapse");
        Equal(null, target.DisplayGroups()[1], "replacement restores ungrouped position");
        var blocked = new CraftingOrderList(); var kept = blocked.Add(first);
        kept.Batch = new CraftingBatch { CanCancel = false };
        Equal(false, blocked.ApplyPreset(preset, true), "failed refund stops replacement");
        Equal(kept, blocked.Orders.Single(), "failed refund retains original task");
        Equal(false, blocked.ApplyPreset(new CraftingPreset(), true), "empty or wholly unavailable preset cannot clear queue");
        Equal(1, kept.Batch.CancelCalls, "empty import does not touch batch");
    }

    private static void MissingAndValidation()
    {
        var xml = CraftingPresets.Encode("Missing", CraftingPreset.Capture(Sample()));
        DefDatabase<RecipeDef>.AllDefsListForReading.Remove(first);
        DefDatabase<ThingDef>.AllDefsListForReading.Remove(wood);
        List<string> missing;
        var loaded = CraftingPresets.Decode(xml, out missing);
        Equal(1, loaded.Orders.Count, "unavailable recipes are skipped");
        Equal(true, missing.Contains(first.defName), "missing recipe is reported");
        Equal(1, loaded.Groups.Count, "empty named group pruned while ungrouped block survives");
        Equal(0, loaded.Orders[0].Ingredients.AllowedThingDefs.Count, "missing default material never broadens allowed materials");
        DefDatabase<RecipeDef>.AllDefsListForReading.Add(first);
        var partial = CraftingPresets.Decode(xml, out missing);
        Equal(true, missing.Contains(wood.defName), "missing count filter definition is reported");
        Equal(0, partial.Orders[0].AdditionalCounts.AllowedThingDefs.Count, "missing additional count filter remains empty");
        DefDatabase<ThingDef>.AllDefsListForReading.Add(wood);
        Equal(xml.ToString(), CraftingPresets.Encode("Missing", CraftingPresets.Decode(xml, out missing)).ToString(), "re-enabled definitions restore from unchanged XML");
        Reject(xml, x => x.SetAttributeValue("version", 9), "unsupported format");
        Reject(xml, x => x.Element("orders").Elements().First().Element("mode").Value = "99", "invalid repeat mode");
        Reject(xml, x => x.Element("orders").Elements().First().Element("targetCount").Value = "0", "invalid target");
        Reject(xml, x => x.Element("orders").Elements().First().Element("repeatCount").Value = "-1", "negative quantity");
        Reject(xml, x => x.Element("orders").Elements().First().Element("group").Value = "Unknown", "unknown group");
        Reject(xml, x => x.Element("orders").Elements().First().Element("skill").SetAttributeValue("max", 22), "invalid skill");
        Reject(xml, x => x.Element("orders").Elements().First().Element("hitPoints").SetAttributeValue("min", "NaN"), "non-finite range");
        Reject(xml, x => x.Element("groups").Add(new XElement(x.Element("groups").Elements().First())), "duplicate group");
        var style = new Precept_ThingStyle { def = new Def { defName = "Statue" }, ThingDef = steel };
        first.Precepts.Add(style);
        var styled = new CraftingOrderList(); styled.Add(first, style);
        var styleXml = CraftingPresets.Encode("Ideology", CraftingPreset.Capture(styled));
        var otherSaveStyle = new Precept_ThingStyle { def = style.def, ThingDef = steel };
        first.Precepts.Clear(); first.Precepts.Add(otherSaveStyle);
        var matched = CraftingPresets.Decode(styleXml, out missing);
        Equal(otherSaveStyle, matched.Orders.Single().Precept, "ideology task resolves against current save");
        first.Precepts.Clear();
        Equal(0, CraftingPresets.Decode(styleXml, out missing).Orders.Count, "unmatched ideology task is skipped");
        Equal(1, missing.Count, "unmatched ideology task is reported");
    }
    private static void WorkbenchRestrictions()
    {
        var list = new CraftingOrderList();
        var order = list.Add(first);
        var bench = new Building_CosmicWorkbench();
        var other = new Building_CosmicWorkbench();
        Equal(true, order.AllowsWorkbench(other), "unrestricted order accepts any bench");
        order.RestrictWorkbench = true; order.Workbench = bench;
        Equal(true, order.AllowsWorkbench(bench), "assigned bench can execute");
        Equal(false, order.AllowsWorkbench(other), "another bench cannot execute");
        Equal(false, order.AllowsWorkbench(null), "missing executor cannot match assigned bench");
        var duplicate = list.Duplicate(order);
        Equal(bench, duplicate.Workbench, "duplicate preserves assigned bench");
        Equal(true, duplicate.RestrictWorkbench, "duplicate preserves restriction");
        var preset = CraftingPreset.Capture(list);
        Equal(false, preset.Orders[0].RestrictWorkbench, "portable preset clears local assignment");
        Equal<Building_CosmicWorkbench>(null, preset.Orders[0].Workbench, "preset does not retain map building");
        order.Workbench = null;
        Equal(false, order.AllowsWorkbench(other), "lost assigned bench never broadens permission");
        Equal(false, order.AllowsWorkbench(null), "lost assigned bench cannot match null");
    }
    private static void Reject(XElement original, Action<XElement> change, string message)
    {
        var xml = new XElement(original); change(xml);
        bool rejected = false;
        try { List<string> missing; CraftingPresets.Decode(xml, out missing); }
        catch (FormatException) { rejected = true; }
        Equal(true, rejected, message);
    }
}
