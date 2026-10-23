using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class CosmicPrinterProductionTests
{
    private static int assertions;
    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception(message); }
    private static int Main()
    {
        try
        {
            ParameterConfirmation();
            var printer = new Building_CosmicPrinter();
            printer.Settings.Efficiency = 2.5f; printer.Settings.QualityOffset = 1;
            var material = new Thing { def = new ThingDef(), DrawColor = 31 };
            var input = new List<Thing> { material };
            var productDef = new ThingDef { Quality = true, MadeFromStuff = true, stackLimit = 2 };
            var recipe = new RecipeDef { useIngredientsForColor = true };
            recipe.products.Add(new ThingDefCountClass { thingDef = productDef, count = 2 });
            var products = PrinterCraftingProduction.MakeProducts(printer, recipe, input, material).ToList();
            Check(products.Sum(p => p.stackCount) == 5, "base quantity is multiplied once");
            Check(products.Select(p => p.stackCount).SequenceEqual(new[] { 2, 2, 1 }), "large outputs respect stack limits");
            Check(products.All(p => p.Stuff == material.def && p.DrawColor == 31), "stuff and ingredient color are preserved");
            Check(products.All(p => p.Ingredients.Registered.SequenceEqual(new[] { material.def })), "ingredient provenance is preserved");
            Check(products.All(p => p.Quality.Quality == QualityCategory.Good), "machine quality offset applies to vanilla base quality");

            productDef.Minifiable = true; productDef.stackLimit = 1;
            recipe.products[0].count = 1; printer.Settings.Efficiency = 3;
            ModsConfig.IdeologyActive = true;
            products = PrinterCraftingProduction.MakeProducts(printer, recipe, input, material).ToList();
            Check(products.Count == 3 && products.All(p => p is MinifiedThing), "furniture yield creates distinct minified products");
            Check(products.Cast<MinifiedThing>().All(p => p.InnerThing.stackCount == 1), "minification cannot discard efficiency output");
            Check(products.Cast<MinifiedThing>().All(p => p.InnerThing.StyleDef == Faction.OfPlayer.ideos.PrimaryIdeo.Style), "player style applies before minification");

            recipe.products.Clear(); recipe.autoStripCorpses = true;
            recipe.specialProducts = new List<SpecialProductType> { SpecialProductType.Butchery };
            var corpse = new Corpse();
            // Special-product engine stubs return ordinary things with valid defs.
            corpse.InnerPawn = new SpecialIngredient();
            products = PrinterCraftingProduction.MakeProducts(printer, recipe, new List<Thing> { corpse }, corpse).ToList();
            Check(corpse.Strips == 1, "auto-strip runs before special products");
            Check(corpse.InnerPawn.ButcherCalls == 1 && corpse.InnerPawn.LastEfficiency == 3, "corpse yield uses inner pawn without a fake worker");
            Check(products.Count == 1, "special products enter normal printer finishing");

            var stone = new SpecialIngredient();
            recipe.specialProducts = new List<SpecialProductType> { SpecialProductType.Smelted, SpecialProductType.StoneBlocks };
            products = PrinterCraftingProduction.MakeProducts(printer, recipe, new List<Thing> { stone }, stone).ToList();
            Check(stone.SmeltCalls == 1 && stone.StoneCalls == 1 && stone.LastEfficiency == 3, "smelting and stonecutting receive machine efficiency");
            Check(products.Count == 2, "all special outputs are returned");

            var otherPrinter = new Building_CosmicPrinter();
            printer.Settings.Reset();
            Check(otherPrinter.Settings.Efficiency == 1 && otherPrinter.Settings.QualityOffset == 0, "printer settings are independent");
            Console.WriteLine("PASS: " + assertions + " printer production assertions (game object boundaries stubbed).");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }

    private static void ParameterConfirmation()
    {
        var live = new CosmicPrinterSettings();
        var draft = live.Copy();
        draft.WorkSpeed = 3; draft.Efficiency = 2; draft.SkillLevel = 20;
        draft.QualityOffset = 1; draft.InspirationChance = .8f; draft.FailureChance = .1f;
        draft.QualityProbabilities();
        Check(live.WorkSpeed == 1 && live.Efficiency == 1 && live.SkillLevel == 12,
            "editing and previewing a draft do not modify production parameters");
        Check(live.InspirationChance == .3f && live.FailureChance == .2f && live.QualityOffset == 0,
            "draft quality changes do not leak into live printer settings");
        Check(!live.TryApply(draft, false), "confirmation is rejected while mod option is off");
        Check(live.WorkSpeed == 1 && live.InspirationChance == .3f, "blocked confirmation preserves current settings");
        Check(live.TryApply(draft, true), "confirmation succeeds after editing is enabled");
        Check(live.WorkSpeed == 3 && live.Efficiency == 2 && live.SkillLevel == 20 && live.QualityOffset == 1 &&
            live.InspirationChance == .8f && live.FailureChance == .1f, "confirmation commits all six parameters");
        draft.Reset();
        Check(live.WorkSpeed == 3 && live.InspirationChance == .8f, "draft remains independent after confirmation");
        Check(!live.TryApply(draft, false) && live.WorkSpeed == 3, "disabling option again blocks later commits");
        draft.SkillLevel = 99; draft.Efficiency = float.NaN;
        Check(live.TryApply(draft, true) && live.SkillLevel == 20 && live.Efficiency == 1,
            "confirmed parameters are normalized before applying");
        Check(draft.SkillLevel == 99 && float.IsNaN(draft.Efficiency), "normalization does not mutate the draft object");
    }
    private sealed class SpecialIngredient : Thing
    {
        public override IEnumerable<Thing> ButcherProducts(object pawn, float efficiency)
        { ButcherCalls++; LastEfficiency = efficiency; yield return new Thing { def = new ThingDef() }; }
    }
}
