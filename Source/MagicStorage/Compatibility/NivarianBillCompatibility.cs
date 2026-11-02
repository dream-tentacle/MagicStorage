using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace MagicStorage
{
    // Nivarian bills bypass RecipeWorkerCounter and count spawned products directly.
    internal sealed class NivarianBillCompatibility : IStorageCompatibility
    {
        private static FieldInfo recipeField, modeField, targetField;
        private static object targetMode;
        private static bool reportedFailure;
        public string PackageId => "keeptpa.NivarianRace";

        public void Apply(Harmony harmony)
        {
            Type bill = AccessTools.TypeByName("Nivarian.NivarianBill") ??
                throw new TypeLoadException("Nivarian.NivarianBill");
            MethodInfo method = AccessTools.Method(bill, "ShouldDoNow", new[] { typeof(Map) });
            if (method == null || method.IsStatic || method.ReturnType != typeof(bool))
                throw new MissingMethodException(bill.FullName, "ShouldDoNow(Map)");
            recipeField = RequireField(bill, "recipe", typeof(RecipeDef));
            targetField = RequireField(bill, "targetCount", typeof(int));
            modeField = AccessTools.Field(bill, "repeatMode");
            if (modeField == null || !modeField.FieldType.IsEnum)
                throw new MissingFieldException(bill.FullName, "repeatMode");
            targetMode = Enum.Parse(modeField.FieldType, "TargetCount");
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(NivarianBillCompatibility), nameof(ShouldDoNowPostfix)));
        }

        private static FieldInfo RequireField(Type type, string name, Type fieldType)
        {
            FieldInfo field = AccessTools.Field(type, name);
            if (field == null || field.IsStatic || field.FieldType != fieldType)
                throw new MissingFieldException(type.FullName, name);
            return field;
        }

        private static void ShouldDoNowPostfix(object __instance, Map __0, ref bool __result)
        {
            // Preserve suspension, invalid recipes and any other reason the original declined.
            if (!__result || __0 == null) return;
            try
            {
                if (!targetMode.Equals(modeField.GetValue(__instance))) return;
                var recipe = (RecipeDef)recipeField.GetValue(__instance);
                ThingDef product = recipe?.products != null && recipe.products.Count > 0
                    ? recipe.products[0]?.thingDef : null;
                if (product == null) return;
                __result = BelowTarget(__0, product, (int)targetField.GetValue(__instance));
            }
            catch (Exception exception)
            {
                if (reportedFailure) return;
                reportedFailure = true;
                Log.Error("[MagicStorage] Nivarian bill target count failed: " + exception);
            }
        }

        internal static bool BelowTarget(Map map, ThingDef product, int target)
        {
            long count = 0;
            // Keep Nivarian's exact-def, first-product convention. ResourceCounter already
            // includes stored resources, so adding its result here would count them twice.
            foreach (Thing thing in map.listerThings.ThingsOfDef(product))
            {
                count += thing.stackCount;
                if (count >= target) return false;
            }
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            if (manager != null)
            {
                manager.EnsureCurrent();
                foreach (Building_StorageUnit unit in manager.StorageUnits)
                {
                    if (!StorageResourceCounter.IsCounted(unit, map)) continue;
                    // Per-unit indexes contain only held stock; spawned output shelves are
                    // already in the map list. Reserved stock still exists and still counts.
                    count += unit.Inventory.GetTotalCount(product);
                    if (count >= target) return false;
                }
            }
            return count < target;
        }
    }
}
