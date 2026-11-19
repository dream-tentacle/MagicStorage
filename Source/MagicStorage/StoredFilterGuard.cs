using System;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // 仓库里的堆已 despawn，原版过滤器都假定物品结构完好；例如内部容器为空的尸体
    // （Corpse.InnerPawn 为 null）会让原版尸体特殊过滤器抛空引用异常。配方判定和货架、
    // 库存规则、燃料等物品级过滤器都会碰到这种堆，所以这里统一兜底为“不允许”，
    // 并按定义只报告一次。
    internal static class StoredFilterGuard
    {
        private const string StageBill = "Bill.IsFixedOrAllowedIngredient";
        private const string StageFilter = "ThingFilter.Allows";
        private const string StageIngredient = "IngredientCount.Allows";

        // 与原版尸体名称使用相同的完整检查：内部角色、def、kindDef 都必须存在。
        internal static bool IsUsable(Thing thing) =>
            thing != null && !thing.Destroyed && thing.def != null &&
            (!(thing is Corpse corpse) || !corpse.Bugged);

        internal static bool CheckInventoryItem(Thing thing)
        {
            if (IsUsable(thing)) return true;
            // Do not read labels or ToString here: those can fail on the invalid item.
            string identity = "thingID=" + (thing?.thingIDNumber.ToString() ?? "<null>") +
                "; DefName=" + (thing?.def?.defName ?? "<null>");
            Log.WarningOnce("[MagicStorage] Invalid inventory item skipped; the item was not deleted. " +
                identity, ("MagicStorage.InvalidInventoryItem|" + identity).GetHashCode());
            return false;
        }

        // 等价于 filter.Allows(thing)，包含特殊过滤器，以及只用 ThingDef 重载时
        // 会丢失的品质与耐久百分比限制。
        internal static bool Allows(ThingFilter filter, Thing thing)
        {
            if (filter == null || !IsUsable(thing)) return false;
            try { return filter.Allows(thing); }
            catch (Exception exception) { Report(StageFilter, thing, exception); return false; }
        }

        // 等价于 bill.IsFixedOrAllowedIngredient(thing)。
        internal static bool IsFixedOrAllowedIngredient(Bill bill, Thing thing)
        {
            if (bill == null || !IsUsable(thing)) return false;
            try { return bill.IsFixedOrAllowedIngredient(thing); }
            catch (Exception exception) { Report(StageBill, thing, exception); return false; }
        }

        // 等价于 recipe.ingredients.Any(ingredient => ingredient.filter.Allows(thing))。
        internal static bool AnyIngredientAllows(RecipeDef recipe, Thing thing)
        {
            if (recipe?.ingredients == null || !IsUsable(thing)) return false;
            try
            {
                foreach (var ingredient in recipe.ingredients)
                {
                    if (ingredient?.filter == null) continue;
                    if (ingredient.filter.Allows(thing)) return true;
                }
                return false;
            }
            catch (Exception exception) { Report(StageIngredient, thing, exception); return false; }
        }

        // 等价于 ingredient.filter.Allows(thing) &&
        // (ingredient.IsFixedIngredient || chosen.Allows(thing))，其中 chosen 是玩家
        // 为该配方选定的过滤器（订单材料或 bill 的 ingredientFilter）。
        internal static bool IngredientAllows(IngredientCount ingredient, ThingFilter chosen, Thing thing)
        {
            if (ingredient?.filter == null || !IsUsable(thing)) return false;
            try
            {
                if (!ingredient.filter.Allows(thing)) return false;
                return ingredient.IsFixedIngredient || (chosen != null && chosen.Allows(thing));
            }
            catch (Exception exception) { Report(StageIngredient, thing, exception); return false; }
        }

        private static void Report(string stage, Thing thing, Exception exception)
        {
            string defName = thing?.def?.defName ?? "<unknown>";
            string typeName = thing?.GetType().FullName ?? "<unknown>";
            // 制作窗口每次重绘都会评估仓库里的每一个堆，所以坏物品按阶段和定义
            // 只报告一次，避免刷屏。
            string key = "MagicStorage.FilterGuard|" + stage + "|" + defName + "|" + typeName;
            Log.WarningOnce("[MagicStorage] A stored item failed a vanilla filter check and was skipped." +
                " Stage=" + stage + "; DefName=" + defName + "; RuntimeType=" + typeName +
                "; DefinitionMod=" + (thing?.def?.modContentPack?.Name ?? "<unknown>") +
                "; Exception=" + exception, key.GetHashCode());
        }
    }
}
