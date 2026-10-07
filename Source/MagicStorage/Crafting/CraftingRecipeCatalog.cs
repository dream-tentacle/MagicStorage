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
        private static void EnsureLoaded()
        {
            if (routes != null) return;
            routes = new Dictionary<RecipeDef, List<WorkGiverDef>>();
            var types = new HashSet<WorkTypeDef>(DefDatabase<WorkGiverDef>.AllDefsListForReading
                .Where(w => w.giverClass == typeof(WorkGiver_CosmicCrafting)).Select(w => w.workType));
            foreach (var giver in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                if (giver.giverClass != typeof(WorkGiver_DoBill) || !types.Contains(giver.workType) || giver.fixedBillGiverDefs == null) continue;
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
                    }
                }
            }
        }
        internal static IEnumerable<RecipeDef> Available
        { get { EnsureLoaded(); return routes.Keys.Where(r => r.AvailableNow).OrderBy(r => r.label); } }
        internal static IReadOnlyList<WorkGiverDef> Routes(RecipeDef recipe)
        { EnsureLoaded(); return recipe != null && routes.TryGetValue(recipe, out var list) ? list : (IReadOnlyList<WorkGiverDef>)Array.Empty<WorkGiverDef>(); }
        internal static bool Supports(RecipeDef recipe) => Routes(recipe).Count > 0;
        internal static bool HasType(RecipeDef recipe, WorkTypeDef type) => Routes(recipe).Any(w => w.workType == type);
        internal static string WorkLabels(RecipeDef recipe) => string.Join(" / ", Routes(recipe).Select(w => w.workType.LabelCap.ToString()).Distinct());
    }
}
