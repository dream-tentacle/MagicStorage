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
        private List<ThingDef> bottomWorkbenches = new List<ThingDef>();

        public GameComponent_CosmicCraftingUI(Game game) { }

        internal bool IsWorkbenchFavorite(ThingDef table) => favoriteWorkbenches.Contains(table);
        internal bool IsWorkbenchAtBottom(ThingDef table) => bottomWorkbenches.Contains(table);
        internal int WorkbenchSortGroup(ThingDef table) => IsWorkbenchAtBottom(table) ? 2 : IsWorkbenchFavorite(table) ? 0 : 1;

        internal int WorkbenchFavoriteOrder(ThingDef table)
        {
            int index = favoriteWorkbenches.IndexOf(table);
            return index < 0 ? int.MaxValue : index;
        }

        internal void ToggleWorkbenchFavorite(ThingDef table)
        {
            if (!favoriteWorkbenches.Remove(table))
            {
                bottomWorkbenches.Remove(table);
                favoriteWorkbenches.Insert(0, table);
            }
        }

        internal void ToggleWorkbenchBottom(ThingDef table)
        {
            if (!bottomWorkbenches.Remove(table))
            {
                favoriteWorkbenches.Remove(table);
                bottomWorkbenches.Add(table);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref CompactWorkbenches, "compactWorkbenches", false);
            Scribe_Values.Look(ref CompactRecipes, "compactRecipes", false);
            Scribe_Values.Look(ref CategorizedRecipes, "categorizedRecipes", false);
            Scribe_Collections.Look(ref favoriteWorkbenches, "favoriteWorkbenches", LookMode.Def);
            Scribe_Collections.Look(ref bottomWorkbenches, "bottomWorkbenches", LookMode.Def);
            if (favoriteWorkbenches == null) favoriteWorkbenches = new List<ThingDef>();
            if (bottomWorkbenches == null) bottomWorkbenches = new List<ThingDef>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                bottomWorkbenches.RemoveAll(table => table == null);
                favoriteWorkbenches.RemoveAll(table => table == null || bottomWorkbenches.Contains(table));
            }
        }
    }
}
