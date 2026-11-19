using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal static class SurgeryMenuStorage
    {
        internal static void Apply(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(RecipeDef), nameof(RecipeDef.PotentiallyMissingIngredients)),
                postfix: new HarmonyMethod(typeof(SurgeryMenuStorage), nameof(IncludeStock)));
            harmony.Patch(AccessTools.Method(typeof(HealthCardUtility), "CanDoRecipeWithMedicineRestriction"),
                postfix: new HarmonyMethod(typeof(SurgeryMenuStorage), nameof(IncludeMedicine)));
        }

        private static bool Allows(RecipeDef recipe, IngredientCount ingredient, Thing item) =>
            StoredFilterGuard.IngredientAllows(ingredient, recipe.fixedIngredientFilter, item);

        private static List<Thing> StockMedicine(Pawn patient, RecipeDef recipe)
        {
            var care = WorkGiver_DoBill.GetMedicalCareCategory(patient);
            return VanillaBillStorage.CollectStock(patient.Map, null,
                def => def.IsMedicine && care.AllowsMedicine(def) &&
                    recipe.ingredients.Exists(i => i.filter.Allows(def)),
                item => recipe.ingredients.Exists(i => Allows(recipe, i, item)));
        }

        private static void IncludeStock(RecipeDef __instance, Pawn billDoer, Map map, ref IEnumerable<ThingDef> __result)
        {
            // This is a map-wide existence estimate used by surgery menus and quest
            // generation, not a patient-specific permission or a material reservation.
            // Actual bill supply and the medical warning still enforce medical care.
            if (map == null || __result == null || __instance.ingredients.Count == 0 ||
                (billDoer != null && (!billDoer.Spawned || billDoer.Map != map))) return;
            var missing = __result.ToList();
            if (missing.Count == 0) return;
            var stock = VanillaBillStorage.CollectStock(map, billDoer,
                def => __instance.ingredients.Exists(i => i.filter.Allows(def)),
                item => __instance.ingredients.Exists(i => Allows(__instance, i, item)));
            if (stock.Count == 0) return;
            var ground = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
            foreach (var ingredient in __instance.ingredients)
            {
                if (!stock.Exists(t => Allows(__instance, ingredient, t))) continue;
                // Vanilla reports one representative def per absent ingredient group.
                // Two groups can report the same def. Remove only a group missing on the map.
                var allowed = ingredient.filter.AllowedThingDefs;
                var representative = ingredient.IsFixedIngredient ? allowed.FirstOrDefault() :
                    allowed.Where(d => __instance.fixedIngredientFilter.Allows(d)).OrderBy(d => d.BaseMarketValue).FirstOrDefault();
                // Match vanilla's ground test exactly, including the optional worker.
                // Otherwise a group already satisfied on the map could remove another
                // group's identical representative from the original missing list.
                if (missing.Contains(representative) &&
                    !ground.Any(t => (billDoer == null || !t.IsForbidden(billDoer)) && !t.Position.Fogged(map) &&
                        (ingredient.IsFixedIngredient || __instance.fixedIngredientFilter.Allows(t)) && ingredient.filter.Allows(t)))
                    missing.Remove(representative);
            }
            __result = missing;
        }

        private static void IncludeMedicine(IBillGiver giver, RecipeDef recipe, ref bool __result)
        {
            if (!__result && giver is Pawn patient && patient.Spawned && !patient.Dead && VanillaBillStorage.SupportsSurgery(recipe))
                __result = StockMedicine(patient, recipe).Count != 0;
        }
    }
}
