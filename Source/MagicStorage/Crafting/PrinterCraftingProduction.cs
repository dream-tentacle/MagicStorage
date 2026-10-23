using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal static class PrinterCraftingProduction
    {
        internal static IEnumerable<Thing> MakeProducts(Building_CosmicPrinter printer, RecipeDef recipe,
            List<Thing> ingredients, Thing dominant)
        {
            if (recipe.autoStripCorpses)
                foreach (var ingredient in ingredients)
                    if (ingredient is IStrippable strippable && strippable.AnythingToStrip()) strippable.Strip();

            float efficiency = printer.Settings.Efficiency;
            if (recipe.products != null)
                foreach (var entry in recipe.products)
                {
                    int remaining = checked((int)Math.Ceiling(entry.count * (double)efficiency));
                    while (remaining > 0)
                    {
                        ThingDef stuff = entry.thingDef.MadeFromStuff ? dominant?.def : null;
                        Thing product = ThingMaker.MakeThing(entry.thingDef, stuff);
                        // Furniture must be produced as distinct minified things, never a
                        // multi-count inner thing (MakeMinified would discard the excess).
                        product.stackCount = Math.Min(remaining, entry.thingDef.Minifiable ? 1 : Math.Max(1, entry.thingDef.stackLimit));
                        remaining -= product.stackCount;
                        if (dominant != null && recipe.useIngredientsForColor) product.SetColor(dominant.DrawColor, false);
                        CompIngredients registered = product.TryGetComp<CompIngredients>();
                        if (registered != null)
                            foreach (var ingredient in ingredients) registered.RegisterIngredient(ingredient.def);
                        // Pawn production callbacks include cooking competence and
                        // infections and cannot be called with a machine/null pawn.
                        yield return Finish(product, printer);
                    }
                }
            if (recipe.specialProducts == null) yield break;
            foreach (var special in recipe.specialProducts)
                foreach (var ingredient in ingredients)
                {
                    IEnumerable<Thing> output;
                    switch (special)
                    {
                        case SpecialProductType.Butchery:
                            // Corpse.ButcherProducts assumes a pawn for filth and tales.
                            // Its inner pawn exposes the same yield/body-part calculation.
                            output = ingredient is Corpse corpse
                                ? corpse.InnerPawn.ButcherProducts(null, efficiency)
                                : ingredient.ButcherProducts(null, efficiency);
                            break;
                        case SpecialProductType.Smelted: output = ingredient.SmeltProducts(efficiency); break;
                        case SpecialProductType.StoneBlocks: output = ingredient.StoneBlockProducts(null, efficiency); break;
                        default: throw new InvalidOperationException("Unsupported printer special product: " + special);
                    }
                    foreach (var product in output) yield return Finish(product, printer);
                }
        }

        private static Thing Finish(Thing product, Building_CosmicPrinter printer)
        {
            product.TryGetComp<CompQuality>()?.SetQuality(printer.Settings.GenerateQuality(), ArtGenerationContext.Colony);
            if (ModsConfig.IdeologyActive)
                product.StyleDef = Faction.OfPlayer.ideos?.PrimaryIdeo?.GetStyleFor(product.def);
            if (product.StyleDef == null && !product.def.randomStyle.NullOrEmpty() && Rand.Chance(product.def.randomStyleChance))
                product.SetStyleDef(product.def.randomStyle.RandomElementByWeight(x => x.Chance).StyleDef);
            return product.def.Minifiable ? product.MakeMinified() : product;
        }
    }
}
