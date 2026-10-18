using Verse;

namespace MagicStorage
{
    public sealed class GameComponent_CosmicCraftingUI : GameComponent
    {
        public bool CompactWorkbenches;
        public bool CompactRecipes;
        public bool CategorizedRecipes;

        public GameComponent_CosmicCraftingUI(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref CompactWorkbenches, "compactWorkbenches", false);
            Scribe_Values.Look(ref CompactRecipes, "compactRecipes", false);
            Scribe_Values.Look(ref CategorizedRecipes, "categorizedRecipes", false);
        }
    }
}
