using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    public sealed class GameComponent_CosmicCraftingUI : GameComponent
    {
        public bool CompactWorkbenches;
        public bool CompactRecipes;
        public bool CategorizedRecipes;
        private List<ThingDef> favoriteWorkbenches = new List<ThingDef>();

        public GameComponent_CosmicCraftingUI(Game game) { }

        internal bool IsWorkbenchFavorite(ThingDef table) => favoriteWorkbenches.Contains(table);

        internal int WorkbenchFavoriteOrder(ThingDef table)
        {
            int index = favoriteWorkbenches.IndexOf(table);
            return index < 0 ? int.MaxValue : index;
        }

        internal void ToggleWorkbenchFavorite(ThingDef table)
        {
            if (!favoriteWorkbenches.Remove(table)) favoriteWorkbenches.Insert(0, table);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref CompactWorkbenches, "compactWorkbenches", false);
            Scribe_Values.Look(ref CompactRecipes, "compactRecipes", false);
            Scribe_Values.Look(ref CategorizedRecipes, "categorizedRecipes", false);
            Scribe_Collections.Look(ref favoriteWorkbenches, "favoriteWorkbenches", LookMode.Def);
            if (favoriteWorkbenches == null) favoriteWorkbenches = new List<ThingDef>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) favoriteWorkbenches.RemoveAll(table => table == null);
        }
    }
}
