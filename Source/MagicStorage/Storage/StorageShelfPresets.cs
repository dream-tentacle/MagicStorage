using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal static class StorageShelfPresets
    {
        internal sealed class Entry
        {
            internal string Path;
            internal string Name;
        }

        internal static string FolderPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "MagicStorage", "ShelfPresets");

        internal static List<Entry> List()
        {
            if (!Directory.Exists(FolderPath)) return new List<Entry>();
            return Directory.GetFiles(FolderPath, "*.xml").Select(path =>
            {
                string name;
                try { name = (string)Read(path).Attribute("name") ?? Path.GetFileNameWithoutExtension(path); }
                catch { name = Path.GetFileNameWithoutExtension(path); }
                return new Entry { Path = path, Name = name };
            }).OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        internal static void Save(string name, Building_StorageSupplyShelf.ShelfPreset preset, Entry overwrite = null)
        {
            Directory.CreateDirectory(FolderPath);
            // Player-supplied names are XML data, never filesystem paths.
            string path = overwrite?.Path ?? Path.Combine(FolderPath, Guid.NewGuid().ToString("N") + ".xml");
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                new XDocument(Encode(name, preset)).Save(temporary);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal static Building_StorageSupplyShelf.ShelfPreset Load(Entry entry, out List<string> missing) =>
            Decode(Read(entry.Path), out missing);

        private static XElement Read(string path)
        {
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return XDocument.Load(reader).Root ?? throw new FormatException("Empty preset.");
        }

        internal static XElement Encode(string name, Building_StorageSupplyShelf.ShelfPreset preset)
        {
            var filter = preset.SharedFilter;
            return new XElement("MagicStorageShelfPreset", new XAttribute("version", 1), new XAttribute("name", name),
                new XElement("filterMode", preset.FilterMode), new XElement("distinctItems", preset.DistinctItems),
                new XElement("lowTemperatureMode", preset.LowTemperatureMode), new XElement("priority", (int)preset.Priority),
                new XElement("slots", preset.Selections.Select((def, index) =>
                {
                    var slotFilter = preset.Filters[index];
                    return new XElement("li", new XElement("def", def?.defName ?? ""),
                        new XElement("restockLimit", preset.RestockLimits[index]),
                        new XElement("filterLimit", preset.FilterLimits[index]),
                        Quality(slotFilter.Quality), Range("hitPoints", slotFilter.HitPoints),
                        new XElement("anyMaterial", slotFilter.AnyMaterial),
                        new XElement("warnWhenRestockFails", slotFilter.WarnWhenRestockFails),
                        Names("materials", slotFilter.Materials.Where(material => material != null).Select(material => material.defName)));
                })),
                new XElement("sharedFilter", Names("allowedDefs", filter.AllowedThingDefs.Select(def => def.defName)),
                    Names("disallowedSpecialFilters", DefDatabase<SpecialThingFilterDef>.AllDefsListForReading
                        .Where(def => !filter.Allows(def)).Select(def => def.defName)),
                    Quality(filter.AllowedQualityLevels), Range("hitPoints", filter.AllowedHitPointsPercents),
                    Range("mentalBreakChance", filter.AllowedMentalBreakChance)));
        }

        internal static Building_StorageSupplyShelf.ShelfPreset Decode(XElement root, out List<string> missing)
        {
            if (root.Name != "MagicStorageShelfPreset" || (int?)root.Attribute("version") != 1)
                throw new FormatException("Unsupported shelf preset format.");
            var absent = new HashSet<string>(StringComparer.Ordinal);
            var preset = new Building_StorageSupplyShelf.ShelfPreset
            {
                FilterMode = (bool)Required(root, "filterMode"),
                DistinctItems = (bool)Required(root, "distinctItems"),
                LowTemperatureMode = (bool)Required(root, "lowTemperatureMode"),
                Priority = (StoragePriority)(int)Required(root, "priority")
            };
            if (!Enum.IsDefined(typeof(StoragePriority), preset.Priority)) throw new FormatException("Invalid storage priority.");
            foreach (XElement slot in Required(root, "slots").Elements("li"))
            {
                // Keep null selections in place so a missing item cannot shift later slots.
                preset.Selections.Add(Resolve<ThingDef>((string)Required(slot, "def"), absent));
                preset.RestockLimits.Add(Nonnegative(slot, "restockLimit"));
                preset.FilterLimits.Add(Nonnegative(slot, "filterLimit"));
                var filter = new StorageShelfFilter
                {
                    Quality = ReadQuality(slot), HitPoints = ReadRange(slot, "hitPoints"),
                    AnyMaterial = (bool)Required(slot, "anyMaterial"),
                    WarnWhenRestockFails = (bool)Required(slot, "warnWhenRestockFails")
                };
                foreach (XElement item in Required(slot, "materials").Elements("li"))
                {
                    ThingDef material = Resolve<ThingDef>((string)item, absent);
                    if (material != null) filter.Materials.Add(material);
                }
                preset.Filters.Add(filter);
            }
            if (preset.Selections.Count == 0) throw new FormatException("Preset has no slots.");
            XElement shared = Required(root, "sharedFilter");
            foreach (XElement item in Required(shared, "allowedDefs").Elements("li"))
            {
                ThingDef def = Resolve<ThingDef>((string)item, absent);
                if (def != null) preset.SharedFilter.SetAllow(def, true);
            }
            foreach (XElement item in Required(shared, "disallowedSpecialFilters").Elements("li"))
            {
                SpecialThingFilterDef def = Resolve<SpecialThingFilterDef>((string)item, absent);
                if (def != null) preset.SharedFilter.SetAllow(def, false);
            }
            preset.SharedFilter.AllowedQualityLevels = ReadQuality(shared);
            preset.SharedFilter.AllowedHitPointsPercents = ReadRange(shared, "hitPoints");
            preset.SharedFilter.AllowedMentalBreakChance = ReadRange(shared, "mentalBreakChance");
            missing = absent.OrderBy(value => value, StringComparer.Ordinal).ToList();
            return preset;
        }

        private static T Resolve<T>(string name, HashSet<string> missing) where T : Def, new()
        {
            if (string.IsNullOrEmpty(name)) return null;
            T def = DefDatabase<T>.GetNamedSilentFail(name);
            if (def == null) missing.Add(name);
            return def;
        }

        private static XElement Names(string name, IEnumerable<string> values) =>
            new XElement(name, values.OrderBy(value => value, StringComparer.Ordinal).Select(value => new XElement("li", value)));
        private static XElement Quality(QualityRange quality) =>
            new XElement("quality", new XAttribute("min", (int)quality.min), new XAttribute("max", (int)quality.max));
        private static XElement Range(string name, FloatRange range) =>
            new XElement(name, new XAttribute("min", range.min), new XAttribute("max", range.max));
        private static XElement Required(XElement parent, string name) =>
            parent.Element(name) ?? throw new FormatException("Missing preset field: " + name);
        private static int Nonnegative(XElement parent, string name)
        {
            int value = (int)Required(parent, name);
            if (value < 0) throw new FormatException("Invalid preset limit: " + name);
            return value;
        }
        private static QualityRange ReadQuality(XElement parent)
        {
            XElement quality = Required(parent, "quality");
            int min = (int)quality.Attribute("min"), max = (int)quality.Attribute("max");
            if (min < (int)QualityCategory.Awful || max > (int)QualityCategory.Legendary || min > max)
                throw new FormatException("Invalid quality range.");
            return new QualityRange((QualityCategory)min, (QualityCategory)max);
        }
        private static FloatRange ReadRange(XElement parent, string name)
        {
            XElement range = Required(parent, name);
            float min = (float)range.Attribute("min"), max = (float)range.Attribute("max");
            if (float.IsNaN(min) || float.IsNaN(max) || min < 0 || max > 1 || min > max)
                throw new FormatException("Invalid preset range: " + name);
            return new FloatRange(min, max);
        }
    }
}
