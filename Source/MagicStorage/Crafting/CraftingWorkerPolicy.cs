using System.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal interface ICraftingWorkerPolicy
    {
        bool Allows(CraftingOrder order, Pawn pawn, WorkTypeDef workType);
    }

    internal sealed class VanillaCraftingWorkerPolicy : ICraftingWorkerPolicy
    {
        public bool Allows(CraftingOrder order, Pawn pawn, WorkTypeDef workType)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.workSettings == null ||
                !pawn.workSettings.WorkIsActive(workType) || pawn.WorkTypeIsDisabled(workType)) return false;
            if (!CraftingRecipeCatalog.Routes(order.Recipe).Any(w => w.workType == workType && RouteAllows(w, pawn))) return false;
            // Mandatory recipe skill requirements are separate from the bill's optional restrictions.
            return order.Recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn) == null && RestrictionAllows(order, pawn);
        }

        private static bool RouteAllows(WorkGiverDef giver, Pawn pawn)
        {
            if (!giver.nonColonistsCanDo && !pawn.IsColonist && !pawn.IsColonyMech && !pawn.IsColonySubhuman) return false;
            if (pawn.WorkTagIsDisabled(giver.workTags) || (pawn.RaceProps.IsMechanoid && !giver.canBeDoneByMechs)) return false;
            foreach (var capacity in giver.requiredCapacities)
                if (!pawn.health.capacities.CapableOf(capacity)) return false;
            return true;
        }

        // Port of Bill.PawnAllowedToStartAnew. In vanilla a named worker bypasses the
        // user skill range and kind restriction, but never the recipe's hard requirements.
        private static bool RestrictionAllows(CraftingOrder order, Pawn pawn)
        {
            if (order.Worker != null) return order.Worker == pawn;
            if (order.WorkerKind == CraftingWorkerKind.Slaves && !pawn.IsSlave) return false;
            if (order.WorkerKind == CraftingWorkerKind.Mechs && !pawn.IsColonyMechPlayerControlled) return false;
            if (order.WorkerKind == CraftingWorkerKind.NonMechs && pawn.IsColonyMechPlayerControlled) return false;
            if (order.Recipe.workSkill != null && (pawn.skills != null || pawn.IsColonyMech))
            {
                int level = pawn.skills != null ? pawn.skills.GetSkill(order.Recipe.workSkill).Level : pawn.RaceProps.mechFixedSkillLevel;
                if (level < order.SkillRange.min || level > order.SkillRange.max) return false;
            }
            return !ModsConfig.BiotechActive || !order.Recipe.mechanitorOnlyRecipe || MechanitorUtility.IsMechanitor(pawn);
        }
    }
}
