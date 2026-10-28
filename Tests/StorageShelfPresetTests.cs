using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class StorageShelfPresetTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string label)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(label + ": " + expected + " != " + actual); }

    private static int Main()
    {
        GenFilePaths.SaveDataFolderPath = Path.Combine(AppContext.BaseDirectory, "PresetTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var present = new ThingDef { defName = "Present" };
            var absent = new ThingDef { defName = "AbsentItem" };
            var material = new ThingDef { defName = "AbsentMaterial" };
            var special = new SpecialThingFilterDef { defName = "AbsentSpecial" };
            DefDatabase<ThingDef>.AllDefsListForReading.AddRange(new[] { present, absent, material });
            DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.Add(special);
            var preset = new Building_StorageSupplyShelf.ShelfPreset
            {
                Priority = StoragePriority.Important, FilterMode = true, DistinctItems = true, LowTemperatureMode = true,
                Selections = new List<ThingDef> { absent, null, present, absent },
                RestockLimits = new List<int> { 5, 6, 7, 8 }, FilterLimits = new List<int> { 11, 12, 13, 14 },
                Filters = Enumerable.Range(0, 4).Select(index => new StorageShelfFilter
                {
                    AnyMaterial = false, WarnWhenRestockFails = true,
                    Quality = new QualityRange(QualityCategory.Good, QualityCategory.Masterwork),
                    HitPoints = new FloatRange(.2f, .8f), Materials = new List<ThingDef> { material }
                }).ToList()
            };
            preset.SharedFilter.SetAllow(present, true);
            preset.SharedFilter.SetAllow(absent, true);
            preset.SharedFilter.SetAllow(special, false);
            preset.SharedFilter.AllowedQualityLevels = new QualityRange(QualityCategory.Normal, QualityCategory.Legendary);
            preset.SharedFilter.AllowedHitPointsPercents = new FloatRange(.1f, .9f);
            preset.SharedFilter.AllowedMentalBreakChance = new FloatRange(.3f, .7f);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            XElement xml = StorageShelfPresets.Encode("食物 <&> ../example", preset);
            List<string> missing;
            var loaded = StorageShelfPresets.Decode(XElement.Parse(xml.ToString()), out missing);
            Equal(0, missing.Count, "complete mod list has no missing definitions");
            Equal(xml.ToString(), StorageShelfPresets.Encode("食物 <&> ../example", loaded).ToString(), "all settings round trip, including ranges and special filters");
            StorageShelfPresets.Save("食物 <&> ../example", preset);
            var entry = StorageShelfPresets.List().Single();
            DefDatabase<ThingDef>.AllDefsListForReading.Remove(absent);
            DefDatabase<ThingDef>.AllDefsListForReading.Remove(material);
            DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.Clear();
            loaded = StorageShelfPresets.Decode(xml, out missing);
            Equal("AbsentItem,AbsentMaterial,AbsentSpecial", string.Join(",", missing), "missing names include selections, shared filter, materials and specials exactly once");
            Equal(4, loaded.Selections.Count, "slot count preserved");
            Equal<ThingDef>(null, loaded.Selections[0], "missing selected item becomes empty");
            Equal<ThingDef>(null, loaded.Selections[1], "original empty slot stays empty");
            Equal(present, loaded.Selections[2], "remaining selection stays in original slot");
            Equal(7, loaded.RestockLimits[2], "remaining quantity stays in original slot");
            Equal(13, loaded.FilterLimits[2], "filter limit stays in original slot");
            Equal(0, loaded.Filters[0].Materials.Count, "missing material skipped");
            Equal(false, loaded.Filters[0].AnyMaterial, "missing material never broadens filter to all materials");
            Equal(true, loaded.Filters[0].WarnWhenRestockFails, "warning setting retained");
            Equal(1, loaded.SharedFilter.AllowedThingDefs.Count, "missing shared allowed item skipped");
            Equal(true, loaded.SharedFilter.AllowedThingDefs.Contains(present), "present shared item retained");
            Equal(2, preset.SharedFilter.AllowedThingDefs.Count, "load does not mutate original snapshot");

            Equal("食物 <&> ../example", entry.Name, "names are XML data, not paths");
            string original = File.ReadAllText(entry.Path);
            loaded = StorageShelfPresets.Load(entry, out missing);
            Equal(3, missing.Count, "filesystem load reports absent defs without reference resolution errors");
            Equal(original, File.ReadAllText(entry.Path), "partial load keeps original preset file intact");
            DefDatabase<ThingDef>.AllDefsListForReading.AddRange(new[] { absent, material });
            DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.Add(special);
            loaded = StorageShelfPresets.Load(entry, out missing);
            Equal(0, missing.Count, "re-enabled mods resolve from unchanged file");
            Equal(absent, loaded.Selections[0], "re-enabled selected item restored");
            preset.FilterMode = false;
            preset.LowTemperatureMode = false;
            StorageShelfPresets.Save("replacement", preset, entry);
            Equal(1, StorageShelfPresets.List().Count, "overwrite replaces rather than adds file");
            Equal("replacement", StorageShelfPresets.List().Single().Name, "overwrite updates display name");
            loaded = StorageShelfPresets.Load(entry, out missing);
            Equal(false, loaded.FilterMode, "manual selection mode retained");
            Equal(false, loaded.LowTemperatureMode, "false toggle retained");
            Equal(0, Directory.GetFiles(StorageShelfPresets.FolderPath, "*.tmp").Length, "atomic save cleans temporary files");
            DefDatabase<ThingDef>.AllDefsListForReading.Clear();
            DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.Clear();
            loaded = StorageShelfPresets.Load(entry, out missing);
            Equal(4, missing.Count, "all unavailable definitions still load");
            Equal(true, loaded.Selections.All(def => def == null), "all unavailable selected slots become empty");
            Equal(0, loaded.SharedFilter.AllowedThingDefs.Count, "all unavailable shared items leave allow list empty");

            var bad = new XElement(xml);
            bad.SetAttributeValue("version", 999);
            Reject(bad, "unsupported version rejected");
            bad = new XElement(xml);
            bad.Element("slots").Element("li").Element("restockLimit").Value = "-1";
            Reject(bad, "negative limit rejected");
            bad = new XElement(xml);
            bad.Element("sharedFilter").Element("hitPoints").SetAttributeValue("min", "NaN");
            Reject(bad, "invalid range rejected");
            bad = new XElement(xml);
            bad.Element("slots").RemoveNodes();
            Reject(bad, "empty slot list rejected");
            File.WriteAllText(entry.Path, "<!DOCTYPE p [<!ENTITY x SYSTEM 'file:///unused'>]><p>&x;</p>");
            bool rejected = false;
            try { StorageShelfPresets.Load(entry, out missing); }
            catch (System.Xml.XmlException) { rejected = true; }
            Equal(true, rejected, "external entities are blocked");
            Equal(1, StorageShelfPresets.List().Count, "damaged file can still be listed for deletion");
            Console.WriteLine("PASS: " + assertions + " assertions (production preset codec/filesystem; game definitions stubbed).");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally
        {
            if (Directory.Exists(StorageShelfPresets.FolderPath))
            {
                foreach (string path in Directory.GetFiles(StorageShelfPresets.FolderPath)) File.Delete(path);
                Directory.Delete(StorageShelfPresets.FolderPath);
                Directory.Delete(Path.GetDirectoryName(StorageShelfPresets.FolderPath));
                Directory.Delete(GenFilePaths.SaveDataFolderPath);
            }
        }
    }

    private static void Reject(XElement xml, string label)
    {
        bool rejected = false;
        try { List<string> missing; StorageShelfPresets.Decode(xml, out missing); }
        catch (FormatException) { rejected = true; }
        Equal(true, rejected, label);
    }
}
