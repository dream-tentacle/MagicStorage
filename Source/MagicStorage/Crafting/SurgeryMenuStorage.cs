using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal static class SurgeryMenuStorage
    {
        [ThreadStatic] private static Pawn menuPatient;

        internal static void Apply(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(BillStack), nameof(BillStack.DoListing)),
                prefix: new HarmonyMethod(typeof(SurgeryMenuStorage), nameof(WrapOptions)));
            harmony.Patch(AccessTools.Method(typeof(RecipeDef), nameof(RecipeDef.PotentiallyMissingIngredients)),
                postfix: new HarmonyMethod(typeof(SurgeryMenuStorage), nameof(IncludeStock)));
            harmony.Patch(AccessTools.Method(typeof(HealthCardUtility), "CanDoRecipeWithMedicineRestriction"),
                postfix: new HarmonyMethod(typeof(SurgeryMenuStorage), nameof(IncludeMedicine)));
        }

        private static void WrapOptions(BillStack __instance, ref Func<List<FloatMenuOption>> recipeOptionsMaker)
        {
            if (!(__instance.billGiver is Pawn patient) || !patient.Spawned || patient.Dead || recipeOptionsMaker == null) return;
            var original = recipeOptionsMaker;
            recipeOptionsMaker = () =>
            {
                var previous = menuPatient;
                menuPatient = patient;
                try { return original(); }
                finally { menuPatient = previous; }
            };
        }

        private static bool Allows(RecipeDef recipe, IngredientCount ingredient, Thing item) =>
            StoredFilterGuard.IngredientAllows(ingredient, recipe.fixedIngredientFilter, item);

        private static List<Thing> Stock(Pawn patient, RecipeDef recipe, bool medicineOnly)
        {
            var care = WorkGiver_DoBill.GetMedicalCareCategory(patient);
            return VanillaBillStorage.CollectStock(patient.Map, null,
                def => (!medicineOnly || def.IsMedicine) && (!def.IsMedicine || care.AllowsMedicine(def)) &&
                    recipe.ingredients.Exists(i => i.filter.Allows(def)),
                item => recipe.ingredients.Exists(i => Allows(recipe, i, item)));
        }

        private static void IncludeStock(RecipeDef __instance, Pawn billDoer, Map map, ref IEnumerable<ThingDef> __result)
        {
            if (menuPatient == null || menuPatient.Map != map || billDoer != null ||
                !VanillaBillStorage.SupportsSurgery(__instance)) return;
            var missing = __result.ToList();
            if (missing.Count == 0) return;
            var stock = Stock(menuPatient, __instance, false);
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
                if (missing.Contains(representative) &&
                    !ground.Any(t => !t.Position.Fogged(map) && Allows(__instance, ingredient, t)))
                    missing.Remove(representative);
            }
            __result = missing;
        }

        private static void IncludeMedicine(IBillGiver giver, RecipeDef recipe, ref bool __result)
        {
            if (!__result && giver is Pawn patient && patient.Spawned && !patient.Dead && VanillaBillStorage.SupportsSurgery(recipe))
                __result = Stock(patient, recipe, true).Count != 0;
        }
    }
}
