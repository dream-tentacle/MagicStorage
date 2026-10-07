using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    internal interface ICraftingProductionPolicy
    {
        Thing SelectDominant(RecipeDef recipe, List<Thing> ingredients);
        float WorkRequired(RecipeDef recipe, Thing dominant);
        float WorkSpeed(RecipeDef recipe, Pawn worker, Building_CosmicWorkbench bench);
        void Learn(RecipeDef recipe, Pawn worker, int ticks);
        IEnumerable<Thing> MakeProducts(RecipeDef recipe, Pawn worker, List<Thing> ingredients, Thing dominant);
        void Consume(RecipeDef recipe, Thing ingredient, Map map);
        void NotifyCompleted(Pawn worker, List<Thing> products);
    }
}
