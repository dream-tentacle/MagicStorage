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
    internal static class CraftingPresets
    {
        internal sealed class Entry { internal string Path, Name; }
        internal static string FolderPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "MagicStorage", "CraftingPresets");

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

        internal static void Save(string name, CraftingPreset preset, Entry overwrite = null)
        {
            Directory.CreateDirectory(FolderPath);
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

        internal static CraftingPreset Load(Entry entry, out List<string> missing) => Decode(Read(entry.Path), out missing);
        private static XElement Read(string path)
        {
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return XDocument.Load(reader).Root ?? throw new FormatException("Empty crafting preset.");
        }

        internal static XElement Encode(string name, CraftingPreset preset) =>
            new XElement("MagicStorageCraftingPreset", new XAttribute("version", 1), new XAttribute("name", name),
                new XElement("groupingMode", preset.GroupingMode), new XElement("ungroupedCollapsed", preset.UngroupedCollapsed),
                new XElement("groups", preset.Groups.Select(group => group == null
                    ? new XElement("li", new XAttribute("ungrouped", true))
                    : new XElement("li", new XAttribute("id", group.Id), new XAttribute("name", group.Name),
                        new XAttribute("collapsed", group.Collapsed)))),
                new XElement("orders", preset.Orders.Select(order => new XElement("li",
                    new XElement("recipe", order.Recipe.defName), new XElement("group", order.GroupId ?? ""),
                    new XElement("precept", order.Precept?.def.defName ?? ""),
                    new XElement("preceptThing", order.Precept?.ThingDef?.defName ?? ""),
                    new XElement("mode", (int)order.Mode), new XElement("repeatCount", order.RepeatCount),
                    new XElement("targetCount", order.TargetCount), new XElement("unpauseAt", order.UnpauseAt),
                    new XElement("suspended", order.Suspended), new XElement("pauseWhenSatisfied", order.PauseWhenSatisfied),
                    new XElement("dropProductsOnFloor", order.DropProductsOnFloor), new XElement("workerKind", (int)order.WorkerKind),
                    new XElement("skill", new XAttribute("min", order.SkillRange.min), new XAttribute("max", order.SkillRange.max)),
                    new XElement("includeEquipped", order.IncludeEquipped), new XElement("includeTainted", order.IncludeTainted),
                    new XElement("limitToAllowedStuff", order.LimitToAllowedStuff), Quality(order.Quality), Range("hitPoints", order.HitPoints),
                    Filter("ingredients", order.Ingredients), Filter("additionalCounts", order.AdditionalCounts)))));

        internal static CraftingPreset Decode(XElement root, out List<string> missing)
        {
            if (root.Name != "MagicStorageCraftingPreset" || (int?)root.Attribute("version") != 1)
                throw new FormatException("Unsupported crafting preset format.");
            var absent = new HashSet<string>(StringComparer.Ordinal);
            var preset = new CraftingPreset
            {
                GroupingMode = (bool)Required(root, "groupingMode"),
                UngroupedCollapsed = (bool)Required(root, "ungroupedCollapsed")
            };
            var groupIds = new HashSet<string>();
            bool ungrouped = false;
            foreach (var node in Required(root, "groups").Elements("li"))
            {
                if ((bool?)node.Attribute("ungrouped") == true)
                {
                    if (ungrouped) throw new FormatException("Duplicate ungrouped block.");
                    ungrouped = true; preset.Groups.Add(null); continue;
                }
                string id = (string)node.Attribute("id"), name = (string)node.Attribute("name");
                if (string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(name) || !groupIds.Add(id))
                    throw new FormatException("Invalid crafting group.");
                preset.Groups.Add(new CraftingOrderGroup { Id = id, Name = name, Collapsed = (bool?)node.Attribute("collapsed") ?? false });
            }
            if (!ungrouped) preset.Groups.Add(null);
            foreach (var node in Required(root, "orders").Elements("li"))
            {
                var recipe = Resolve<RecipeDef>((string)Required(node, "recipe"), absent);
                if (recipe == null) continue;
                Precept_ThingStyle precept = null;
                string preceptDef = (string)Required(node, "precept");
                if (!string.IsNullOrEmpty(preceptDef))
                {
                    string thingDef = (string)Required(node, "preceptThing");
                    precept = CraftingRecipeCatalog.PreceptsFor(recipe).FirstOrDefault(p =>
                        p.def.defName == preceptDef && p.ThingDef?.defName == thingDef);
                    if (precept == null) { absent.Add(recipe.defName + " / " + preceptDef); continue; }
                }
                var order = new CraftingOrder(0, recipe, precept)
                {
                    GroupId = (string)Required(node, "group"), Mode = ReadEnum<CraftingRepeatMode>(node, "mode"),
                    RepeatCount = Number(node, "repeatCount", 0, 999999), TargetCount = Number(node, "targetCount", 1, 999999),
                    Suspended = (bool)Required(node, "suspended"), PauseWhenSatisfied = (bool)Required(node, "pauseWhenSatisfied"),
                    DropProductsOnFloor = (bool)Required(node, "dropProductsOnFloor"), WorkerKind = ReadEnum<CraftingWorkerKind>(node, "workerKind"),
                    IncludeEquipped = (bool)Required(node, "includeEquipped"), IncludeTainted = (bool)Required(node, "includeTainted"),
                    LimitToAllowedStuff = (bool)Required(node, "limitToAllowedStuff"), HitPoints = ReadRange(node, "hitPoints"),
                    Quality = ReadQuality(node), Ingredients = new ThingFilter(), AdditionalCounts = new ThingFilter()
                };
                order.UnpauseAt = Number(node, "unpauseAt", 0, Math.Max(0, order.TargetCount - 1));
                if (order.GroupId.Length == 0) order.GroupId = null;
                else if (!groupIds.Contains(order.GroupId)) throw new FormatException("Unknown crafting group.");
                var skill = Required(node, "skill");
                int min = (int)skill.Attribute("min"), max = (int)skill.Attribute("max");
                if (min < 0 || min > 20 || max < min || max > CraftingOrder.UnlimitedSkillLevel)
                    throw new FormatException("Invalid skill range.");
                order.SkillRange = new IntRange(min, max);
                ReadFilter(Required(node, "ingredients"), order.Ingredients, absent);
                ReadFilter(Required(node, "additionalCounts"), order.AdditionalCounts, absent);
                preset.Orders.Add(order);
            }
            // Definitions may disappear; keep only groups still containing usable orders.
            preset.Groups.RemoveAll(group => group != null && !preset.Orders.Any(order => order.GroupId == group.Id));
            missing = absent.OrderBy(value => value, StringComparer.Ordinal).ToList();
            return preset;
        }

        private static XElement Filter(string name, ThingFilter filter) => new XElement(name,
            Names("allowedDefs", filter.AllowedThingDefs.Select(def => def.defName)),
            Names("disallowedSpecialFilters", DefDatabase<SpecialThingFilterDef>.AllDefsListForReading
                .Where(def => !filter.Allows(def)).Select(def => def.defName)),
            Quality(filter.AllowedQualityLevels), Range("hitPoints", filter.AllowedHitPointsPercents),
            Range("mentalBreakChance", filter.AllowedMentalBreakChance));

        private static void ReadFilter(XElement node, ThingFilter filter, HashSet<string> missing)
        {
            filter.SetDisallowAll();
            foreach (var item in Required(node, "allowedDefs").Elements("li"))
            {
                var def = Resolve<ThingDef>((string)item, missing);
                if (def != null) filter.SetAllow(def, true);
            }
            foreach (var item in Required(node, "disallowedSpecialFilters").Elements("li"))
            {
                var def = Resolve<SpecialThingFilterDef>((string)item, missing);
                if (def != null) filter.SetAllow(def, false);
            }
            filter.AllowedQualityLevels = ReadQuality(node);
            filter.AllowedHitPointsPercents = ReadRange(node, "hitPoints");
            filter.AllowedMentalBreakChance = ReadRange(node, "mentalBreakChance");
        }

        private static T Resolve<T>(string name, HashSet<string> missing) where T : Def, new()
        {
            var def = DefDatabase<T>.GetNamedSilentFail(name);
            if (def == null) missing.Add(name);
            return def;
        }
        private static T ReadEnum<T>(XElement node, string name) where T : struct
        {
            int value = (int)Required(node, name);
            if (!Enum.IsDefined(typeof(T), value)) throw new FormatException("Invalid " + name + ".");
            return (T)Enum.ToObject(typeof(T), value);
        }
        private static int Number(XElement node, string name, int min, int max)
        {
            int value = (int)Required(node, name);
            if (value < min || value > max) throw new FormatException("Invalid " + name + ".");
            return value;
        }
        private static XElement Required(XElement node, string name) => node.Element(name) ?? throw new FormatException("Missing field: " + name);
        private static XElement Names(string name, IEnumerable<string> values) => new XElement(name,
            values.OrderBy(value => value, StringComparer.Ordinal).Select(value => new XElement("li", value)));
        private static XElement Quality(QualityRange range) => new XElement("quality",
            new XAttribute("min", (int)range.min), new XAttribute("max", (int)range.max));
        private static XElement Range(string name, FloatRange range) => new XElement(name,
            new XAttribute("min", range.min), new XAttribute("max", range.max));
        private static QualityRange ReadQuality(XElement node)
        {
            var range = Required(node, "quality");
            int min = (int)range.Attribute("min"), max = (int)range.Attribute("max");
            if (min < 0 || max > (int)QualityCategory.Legendary || min > max) throw new FormatException("Invalid quality range.");
            return new QualityRange((QualityCategory)min, (QualityCategory)max);
        }
        private static FloatRange ReadRange(XElement node, string name)
        {
            var range = Required(node, name);
            float min = (float)range.Attribute("min"), max = (float)range.Attribute("max");
            if (float.IsNaN(min) || float.IsNaN(max) || min < 0 || max > 1 || min > max)
                throw new FormatException("Invalid range: " + name);
            return new FloatRange(min, max);
        }
    }
}
