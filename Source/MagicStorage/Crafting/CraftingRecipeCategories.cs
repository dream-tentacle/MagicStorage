using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace MagicStorage
{
    internal sealed class CraftingRecipeCategory
    {
        // A null category represents recipes without a usable product category.
        internal readonly ThingCategoryDef Category;
        internal readonly List<CraftingRecipeCategory> Children = new List<CraftingRecipeCategory>();
        internal readonly List<RecipeDef> Recipes = new List<RecipeDef>();
        internal CraftingRecipeCategory(ThingCategoryDef category) { Category = category; }
    }

    internal static class CraftingRecipeCategories
    {
        internal static List<CraftingRecipeCategory> Build(IEnumerable<RecipeDef> recipes)
        {
            var roots = new List<CraftingRecipeCategory>();
            var other = new CraftingRecipeCategory(null);
            foreach (var recipe in recipes.Where(recipe => recipe != null).Distinct())
            {
                var product = recipe.products?.FirstOrDefault(item => item?.thingDef != null)?.thingDef;
                List<ThingCategoryDef> bestPath = null;
                if (product?.thingCategories != null)
                {
                    foreach (var category in product.thingCategories)
                    {
                        var path = CategoryPath(category);
                        if (bestPath == null || path.Count > bestPath.Count) bestPath = path;
                    }
                }
                if (bestPath == null || bestPath.Count == 0)
                { other.Recipes.Add(recipe); continue; }

                var siblings = roots;
                CraftingRecipeCategory node = null;
                foreach (var category in bestPath)
                {
                    node = siblings.FirstOrDefault(item => item.Category == category);
                    if (node == null) { node = new CraftingRecipeCategory(category); siblings.Add(node); }
                    siblings = node.Children;
                }
                node.Recipes.Add(recipe);
            }
            Sort(roots);
            if (other.Recipes.Count > 0) { SortRecipes(other); roots.Add(other); }
            return roots;
        }

        private static List<ThingCategoryDef> CategoryPath(ThingCategoryDef category)
        {
            var path = new List<ThingCategoryDef>();
            var visited = new HashSet<ThingCategoryDef>();
            for (var current = category; current != null && current.defName != "Root" && visited.Add(current); current = current.parent)
                path.Add(current);
            path.Reverse();
            return path;
        }

        private static void Sort(List<CraftingRecipeCategory> nodes)
        {
            nodes.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(
                a.Category.label ?? a.Category.defName, b.Category.label ?? b.Category.defName));
            foreach (var node in nodes) { Sort(node.Children); SortRecipes(node); }
        }
        private static void SortRecipes(CraftingRecipeCategory node) => node.Recipes.Sort((a, b) =>
            StringComparer.CurrentCultureIgnoreCase.Compare(CraftingRecipeCatalog.Label(a), CraftingRecipeCatalog.Label(b)));
    }
}
