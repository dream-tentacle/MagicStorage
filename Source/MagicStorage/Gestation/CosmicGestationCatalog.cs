using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Only import tables and recipes that use the native mech gestation pipeline.
    // A container or a subclass of another mod's table is not an opt-in contract.
    [StaticConstructorOnStartup]
    internal static class CosmicGestationCatalog
    {
        private static readonly Dictionary<RecipeDef, List<ThingDef>> sources =
            new Dictionary<RecipeDef, List<ThingDef>>();

        static CosmicGestationCatalog()
        {
            if (!ModsConfig.BiotechActive) return;
            var target = DefDatabase<ThingDef>.GetNamedSilentFail("MS_CosmicMechGestator");
            if (target == null) return;
            foreach (var table in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (table.thingClass != typeof(Building_MechGestator)) continue;
                foreach (var recipe in table.AllRecipes)
                {
                    if (!UsesNativePipeline(recipe)) continue;
                    if (!sources.TryGetValue(recipe, out var tables))
                        sources.Add(recipe, tables = new List<ThingDef>());
                    if (!tables.Contains(table)) tables.Add(table);
                }
            }
            // AllRecipes is cached by ThingDef. Update that list too, so map listers
            // and the native Bills tab see exactly the same supported recipes.
            target.AllRecipes.Clear();
            target.AllRecipes.AddRange(sources.Keys.OrderBy(r => r.label));
        }

        internal static bool UsesNativePipeline(RecipeDef recipe) =>
            recipe != null && recipe.GetType() == typeof(RecipeDef) && !recipe.IsSurgery &&
            !recipe.UsesUnfinishedThing && recipe.mechanitorOnlyRecipe && recipe.gestationCycles > 0 &&
            recipe.formingTicks > 0 && recipe.Worker.GetType() == typeof(RecipeWorker) &&
            (recipe.requiredGiverWorkType == null || recipe.requiredGiverWorkType == WorkTypeDefOf.Smithing) &&
            (recipe.mechResurrection || (recipe.products != null && recipe.products.Count == 1 &&
                recipe.products[0].thingDef?.race?.IsMechanoid == true && recipe.products[0].count == 1));

        internal static bool Supports(Bill bill) => bill != null &&
            (bill.GetType() == typeof(Bill_ProductionMech) || bill.GetType() == typeof(Bill_ResurrectMech)) &&
            sources.ContainsKey(bill.recipe);

        internal static bool Available(RecipeDef recipe) => recipe != null && recipe.AvailableNow &&
            sources.TryGetValue(recipe, out var tables) && tables.Any(t => t.IsResearchFinished);
    }
}
