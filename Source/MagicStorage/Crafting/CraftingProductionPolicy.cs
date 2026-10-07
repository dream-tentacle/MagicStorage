using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal sealed class VanillaCraftingProductionPolicy : ICraftingProductionPolicy
    {
        // Port of Toils_Recipe.CalculateDominantIngredient, without Job.bill or UnfinishedThing.
        public Thing SelectDominant(RecipeDef recipe, List<Thing> ingredients)
        {
            if (ingredients.Count == 0) return null;
            if (recipe.productHasIngredientStuff) return ingredients[0];
            if (recipe.products.Any(p => p.thingDef.MadeFromStuff) || recipe.unfinishedThingDef?.MadeFromStuff == true)
                return ingredients.Where(t => t.def.IsStuff).RandomElementByWeight(t => t.stackCount);
            return ingredients.RandomElementByWeight(t => t.stackCount);
        }
        public float WorkRequired(RecipeDef recipe, Thing dominant)
        {
            if (recipe == RecipeDefOf.SmeltOrDestroyThing && dominant?.Smeltable == true) return recipe.smeltingWorkAmount;
            return recipe.WorkAmountForStuff(dominant?.def.IsStuff == true ? dominant.def : null);
        }
        public float WorkSpeed(RecipeDef recipe, Pawn worker, Building_CosmicWorkbench bench)
        {
            float speed = recipe.workSpeedStat == null ? 1f : worker.GetStatValue(recipe.workSpeedStat);
            if (recipe.workTableSpeedStat != null) speed *= bench.GetStatValue(recipe.workTableSpeedStat);
            return speed;
        }
        public void Learn(RecipeDef recipe, Pawn worker, int ticks)
        {
            // All orders retain unfinished work, so award the vanilla unfinished-work
            // rate to the actual contributor instead of awarding it again on completion.
            if (recipe.workSkill != null && worker.skills != null)
                worker.skills.Learn(recipe.workSkill, 0.1f * recipe.workSkillLearnFactor * ticks);
        }
        public IEnumerable<Thing> MakeProducts(RecipeDef recipe, Pawn worker, List<Thing> ingredients, Thing dominant)
        {
            // The core remains the holder, giving stripping callbacks a valid map/position.
            if (recipe.autoStripCorpses)
                foreach (var ingredient in ingredients)
                    if (ingredient is IStrippable strippable && strippable.AnythingToStrip()) strippable.Strip();
            // GenRecipe is the product rule utility, not the vanilla worktable execution
            // pipeline. A null bill giver supplies the normal 1x table yield factor.
            ThingStyleDef style = null;
            if (ModsConfig.IdeologyActive && recipe.products.Count == 1)
                style = Faction.OfPlayer.ideos.PrimaryIdeo.style.StyleForThingDef(recipe.ProducedThingDef)?.styleDef;
            return GenRecipe.MakeRecipeProducts(recipe, worker, ingredients, dominant, null, style: style);
        }
        public void Consume(RecipeDef recipe, Thing ingredient, Map map) => recipe.Worker.ConsumeIngredient(ingredient, recipe, map);
        public void NotifyCompleted(Pawn worker, List<Thing> products)
        {
            RecordsUtility.Notify_BillDone(worker, products);
            if (products.Count > 0) Find.QuestManager.Notify_ThingsProduced(worker, products);
        }
    }
}
