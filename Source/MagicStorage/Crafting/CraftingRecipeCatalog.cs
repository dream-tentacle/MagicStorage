using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal static class CraftingRecipeCatalog
    {
        private static Dictionary<RecipeDef, List<WorkGiverDef>> routes;
        private static Dictionary<RecipeDef, List<ThingDef>> workbenches;
        private static Dictionary<ThingDef, Dictionary<RecipeDef, HashSet<WorkTypeDef>>> tableRecipes;
        private static List<WorkTypeDef> workTypes;
        private static void EnsureLoaded()
        {
            if (routes != null) return;
            routes = new Dictionary<RecipeDef, List<WorkGiverDef>>();
            workbenches = new Dictionary<RecipeDef, List<ThingDef>>();
            tableRecipes = new Dictionary<ThingDef, Dictionary<RecipeDef, HashSet<WorkTypeDef>>>();
            workTypes = DefDatabase<WorkGiverDef>.AllDefsListForReading
                .Where(w => w.giverClass == typeof(WorkGiver_CosmicCrafting) && w.workType != null)
                .Select(w => w.workType).Distinct().OrderByDescending(t => t.naturalPriority).ToList();
            var types = new HashSet<WorkTypeDef>(workTypes);
            foreach (var giver in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                // Production sources are described by their work type and bill tables;
                // mods can replace the work giver implementation without changing either.
                if (!types.Contains(giver.workType) || giver.fixedBillGiverDefs == null) continue;
                foreach (var table in giver.fixedBillGiverDefs)
                {
                    if (table.thingClass == null || !typeof(Building_WorkTable).IsAssignableFrom(table.thingClass) ||
                        typeof(Building_WorkTableAutonomous).IsAssignableFrom(table.thingClass)) continue;
                    foreach (var recipe in table.AllRecipes)
                    {
                        if (recipe.IsSurgery || recipe.gestationCycles > 0 || recipe.formingTicks > 0 || recipe.mechResurrection ||
                            (recipe.requiredGiverWorkType != null && recipe.requiredGiverWorkType != giver.workType)) continue;
                        if (!routes.TryGetValue(recipe, out var list)) routes.Add(recipe, list = new List<WorkGiverDef>());
                        if (!list.Contains(giver)) list.Add(giver);
                        if (!workbenches.TryGetValue(recipe, out var tables)) workbenches.Add(recipe, tables = new List<ThingDef>());
                        if (!tables.Contains(table)) tables.Add(table);
                        if (!tableRecipes.TryGetValue(table, out var recipes))
                            tableRecipes.Add(table, recipes = new Dictionary<RecipeDef, HashSet<WorkTypeDef>>());
                        if (!recipes.TryGetValue(recipe, out var recipeTypes))
                            recipes.Add(recipe, recipeTypes = new HashSet<WorkTypeDef>());
                        recipeTypes.Add(giver.workType);
                    }
                }
            }
        }
        internal static IEnumerable<RecipeDef> Available
        { get { EnsureLoaded(); return routes.Keys.Where(CanAdd).OrderBy(Label); } }
        internal static IEnumerable<RecipeDef> AvailableForWorkbench(ThingDef table, WorkTypeDef type = null, bool includeLocked = false)
        {
            EnsureLoaded();
            if (table == null || !table.IsResearchFinished || (!includeLocked && !CraftingWorkbenchUnlocks.IsUnlocked(table)))
                return Enumerable.Empty<RecipeDef>();
            return RecipesForWorkbench(table, type).Where(recipe => recipe.AvailableNow).OrderBy(Label);
        }
        internal static IEnumerable<ThingDef> AvailableWorkbenches(WorkTypeDef type = null)
        {
            EnsureLoaded();
            return tableRecipes.Keys.Where(table => table.IsResearchFinished &&
                RecipesForWorkbench(table, type).Any(recipe => recipe.AvailableNow)).OrderBy(table => table.label);
        }
        private static IEnumerable<RecipeDef> RecipesForWorkbench(ThingDef table, WorkTypeDef type)
            => tableRecipes.TryGetValue(table, out var recipes)
                ? recipes.Where(pair => type == null || pair.Value.Contains(type)).Select(pair => pair.Key)
                : Enumerable.Empty<RecipeDef>();

        internal static bool HasWorkbenchType(ThingDef table, RecipeDef recipe, WorkTypeDef type)
        {
            EnsureLoaded();
            return tableRecipes.TryGetValue(table, out var recipes) && recipes.TryGetValue(recipe, out var types) &&
                (type == null || types.Contains(type));
        }

        // A browser takes this snapshot once. Research changes are picked up when it is reopened.
        // Locked tables remain visible so the player can unlock them from the browser.
        internal static Dictionary<ThingDef, HashSet<WorkTypeDef>> SnapshotWorkbenchTypes()
        {
            EnsureLoaded();
            var result = new Dictionary<ThingDef, HashSet<WorkTypeDef>>();
            var availability = new Dictionary<RecipeDef, bool>();
            foreach (var table in tableRecipes)
            {
                if (!table.Key.IsResearchFinished) continue;
                var types = new HashSet<WorkTypeDef>();
                foreach (var recipe in table.Value)
                {
                    if (!availability.TryGetValue(recipe.Key, out bool available))
                        availability.Add(recipe.Key, available = recipe.Key.AvailableNow);
                    if (available) types.UnionWith(recipe.Value);
                }
                if (types.Count > 0) result.Add(table.Key, types);
            }
            return result;
        }
        internal static string Label(RecipeDef recipe)
        {
            if (recipe == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(recipe.label)) return recipe.label;
            ThingDef product = recipe.products?.FirstOrDefault(p => p?.thingDef != null)?.thingDef;
            if (product == null) return recipe.defName ?? string.Empty;
            string productLabel = !string.IsNullOrWhiteSpace(product.label) ? product.label : product.defName;
            return "RecipeMake".Translate(productLabel).ToString();
        }
        // ITab_Bills adds these beside the ordinary recipe, without creating new defs.
        internal static IEnumerable<Precept_ThingStyle> PreceptsFor(RecipeDef recipe)
        {
            foreach (var ideo in Faction.OfPlayer.ideos.AllIdeos)
                foreach (var precept in ideo.cachedPossibleBuildings)
                    if (precept.ThingDef == recipe.ProducedThingDef) yield return precept;
        }
        internal static string Label(RecipeDef recipe, Precept_ThingStyle precept) => precept != null
            ? "RecipeMake".Translate(precept.LabelCap).CapitalizeFirst().ToString() : Label(recipe);
        internal static bool MatchesSearch(RecipeDef recipe, string search) => recipe != null &&
            Label(recipe).IndexOf(search ?? string.Empty, StringComparison.CurrentCultureIgnoreCase) >= 0;
        // This gate only controls adding orders; existing orders keep their execution rules.
        internal static bool CanAdd(RecipeDef recipe) => recipe != null && recipe.AvailableNow &&
            Workbenches(recipe).Any(table => table.IsResearchFinished && CraftingWorkbenchUnlocks.IsUnlocked(table));
        internal static IReadOnlyList<WorkTypeDef> WorkTypes
        { get { EnsureLoaded(); return workTypes; } }
        internal static IReadOnlyList<ThingDef> Workbenches(RecipeDef recipe)
        { EnsureLoaded(); return recipe != null && workbenches.TryGetValue(recipe, out var list) ? list : (IReadOnlyList<ThingDef>)Array.Empty<ThingDef>(); }
        internal static IReadOnlyList<WorkGiverDef> Routes(RecipeDef recipe)
        { EnsureLoaded(); return recipe != null && routes.TryGetValue(recipe, out var list) ? list : (IReadOnlyList<WorkGiverDef>)Array.Empty<WorkGiverDef>(); }
        internal static bool Supports(RecipeDef recipe) => Routes(recipe).Count > 0;
        internal static bool HasType(RecipeDef recipe, WorkTypeDef type) => Routes(recipe).Any(w => w.workType == type);
        internal static string WorkLabels(RecipeDef recipe) => string.Join(" / ", Routes(recipe).Select(w => w.workType.labelShort.CapitalizeFirst()).Distinct());

    }
}
