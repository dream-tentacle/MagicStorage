using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using Verse;

internal static class RecipeCategoryTests
{
    private static int assertions;
    private static void Check(bool value, string message)
    { assertions++; if (!value) throw new Exception(message); }
    private static ThingCategoryDef Category(string name, ThingCategoryDef parent = null) =>
        new ThingCategoryDef { defName = name, label = name, parent = parent };
    private static RecipeDef Recipe(string label, params ThingCategoryDef[] categories) => new RecipeDef
    {
        label = label,
        products = new List<ThingDefCountClass> { new ThingDefCountClass
        { thingDef = new ThingDef { thingCategories = categories.ToList() } } }
    };
    private static IEnumerable<RecipeDef> AllRecipes(IEnumerable<CraftingRecipeCategory> nodes) =>
        nodes.SelectMany(node => node.Recipes.Concat(AllRecipes(node.Children)));

    internal static int Run()
    {
        var root = Category("Root");
        var weapons = Category("Weapons", root);
        var ranged = Category("Ranged", weapons);
        var bows = Category("Bows", ranged);
        var apparel = Category("Apparel", root);
        var modded = Category("Modded");
        var bow = Recipe("Bow", weapons, ranged, bows);
        var gun = Recipe("Gun", ranged);
        var shirt = Recipe("Shirt", apparel);
        var unknown = new RecipeDef { label = "Burn apparel" };
        var emptyCategories = Recipe("Uncategorized product");
        var rootOnly = Recipe("Root only", root);
        var orphan = Recipe("Modded product", modded);
        var input = new[] { gun, shirt, bow, unknown, emptyCategories, rootOnly, orphan, bow, null };
        var tree = CraftingRecipeCategories.Build(input);
        Check(tree.Where(node => node.Category != null).Select(node => node.Category.label)
            .SequenceEqual(new[] { "Apparel", "Modded", "Weapons" }), "Use the vanilla parent hierarchy and sort top-level categories by their displayed names.");
        Check(!tree.Any(node => node.Category == root), "The vanilla Root is hidden.");
        var weaponNode = tree.Single(node => node.Category == weapons);
        var rangedNode = weaponNode.Children.Single();
        Check(rangedNode.Category == ranged && rangedNode.Recipes.SequenceEqual(new[] { gun }), "Recipes remain directly in their own category alongside subcategories.");
        Check(rangedNode.Children.Single().Category == bows && rangedNode.Children.Single().Recipes.SequenceEqual(new[] { bow }), "A product with multiple categories appears only in its deepest category.");
        Check(weaponNode.Recipes.Count == 0, "Do not duplicate descendant recipes in parent categories.");
        Check(tree.Single(node => node.Category == modded).Recipes.Contains(orphan), "A mod category without a Root parent is still shown.");
        var other = tree.Last();
        Check(other.Category == null && other.Recipes.Count == 3, "Keep recipes without products or usable categories in Other recipes, after named categories.");
        Check(other.Recipes.Contains(unknown) && other.Recipes.Contains(rootOnly) && other.Recipes.Contains(emptyCategories), "Special recipes and unclassified products remain addable.");
        Check(AllRecipes(tree).Count() == 7 && AllRecipes(tree).Distinct().Count() == 7, "No visible recipe is lost or shown twice.");

        var filtered = CraftingRecipeCategories.Build(new[] { shirt });
        Check(filtered.Count == 1 && filtered[0].Category == apparel && AllRecipes(filtered).SequenceEqual(new[] { shirt }), "Build only from current work type, mod and search results; omit empty categories.");
        Check(CraftingRecipeCategories.Build(Array.Empty<RecipeDef>()).Count == 0, "Empty search results produce no category headings.");

        var twoProducts = Recipe("Two products", apparel);
        twoProducts.products.Add(new ThingDefCountClass { thingDef = new ThingDef { thingCategories = new List<ThingCategoryDef> { bows } } });
        Check(CraftingRecipeCategories.Build(new[] { twoProducts }).Single().Category == apparel, "Classify recipes by their first product, as in Nice Bill Tab.");
        twoProducts.products.Insert(0, null);
        Check(CraftingRecipeCategories.Build(new[] { twoProducts }).Single().Category == apparel, "Ignore malformed empty product entries before the first actual product.");

        apparel.label = "Z clothing";
        weapons.label = "A weapons";
        Check(CraftingRecipeCategories.Build(new[] { shirt, bow }).Select(node => node.Category)
            .SequenceEqual(new[] { weapons, apparel }), "Category ordering follows localized labels, not defNames.");
        var loop = Category("Malformed"); loop.parent = loop;
        Check(AllRecipes(CraftingRecipeCategories.Build(new[] { Recipe("Cyclic category", loop) })).Count() == 1,
            "An invalid mod parent cycle cannot hang the picker or discard the recipe.");
        return assertions;
    }
}
