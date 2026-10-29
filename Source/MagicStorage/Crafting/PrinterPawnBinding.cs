using System.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal static class PrinterPawnBinding
    {
        internal static bool IsAvailable(Pawn pawn, Map map)
            => pawn != null && !pawn.Destroyed && !pawn.Dead && pawn.Spawned && pawn.Map == map &&
                pawn.Faction == Faction.OfPlayer && pawn.IsColonist && pawn.skills != null;

        internal static int SkillLevel(Pawn pawn, SkillDef skill, WorkTypeDef workType = null)
        {
            if (pawn?.skills == null || skill == null ||
                (workType != null && pawn.WorkTypeIsDisabled(workType))) return 0;
            var record = pawn.skills.GetSkill(skill);
            return record == null || record.TotallyDisabled ? 0 : record.Level;
        }

        internal static int RecipeSkillLevel(Pawn pawn, RecipeDef recipe, SkillDef skill = null)
        {
            if (pawn == null || recipe == null) return 0;
            var routes = CraftingRecipeCatalog.Routes(recipe);
            if (routes.Count > 0 && routes.All(route => pawn.WorkTypeIsDisabled(route.workType))) return 0;
            return SkillLevel(pawn, skill ?? recipe.workSkill);
        }

        internal static float WorkSpeed(Pawn pawn, RecipeDef recipe, float manualValue)
            => pawn == null ? manualValue : recipe.workSpeedStat == null ? 1f : pawn.GetStatValue(recipe.workSpeedStat);

        internal static float Efficiency(Pawn pawn, RecipeDef recipe, float manualValue)
            => pawn == null ? manualValue : recipe.efficiencyStat == null ? 1f : pawn.GetStatValue(recipe.efficiencyStat);
    }
}
