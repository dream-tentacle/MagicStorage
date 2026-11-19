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
            BoundColonistParameters();
            StatuesInitializeBeforeStorage();
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
            var precept = new Precept_ThingStyle();
            products = PrinterCraftingProduction.MakeProducts(printer, recipe, input, material, precept).ToList();
            Check(products.Cast<MinifiedThing>().All(p => p.InnerThing.StyleSourcePrecept == precept),
                "cultural precept is bound to the inner product before minification");

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
    private static void BoundColonistParameters()
    {
        var map = new Map();
        var pawn = new Pawn { Spawned = true, Map = map, Faction = Faction.OfPlayer };
        var crafting = new SkillDef();
        var art = new SkillDef();
        var smithing = new WorkTypeDef();
        var artistic = new WorkTypeDef();
        var speed = new StatDef();
        var yield = new StatDef();
        pawn.skills.Records[crafting] = new SkillRecord { Level = 17 };
        pawn.skills.Records[art] = new SkillRecord { Level = 4 };
        pawn.Stats[speed] = 1.75f; pawn.Stats[yield] = 2.5f;
        var recipe = new RecipeDef { workSkill = crafting, workSpeedStat = speed, efficiencyStat = yield };
        recipe.Routes.Add(new WorkGiverDef { workType = smithing });
        var artRecipe = new RecipeDef { workSkill = art };
        artRecipe.Routes.Add(new WorkGiverDef { workType = artistic });
        Check(PrinterPawnBinding.IsAvailable(pawn, map), "living colonist on this map is available");
        Check(!PrinterPawnBinding.IsAvailable(pawn, new Map()), "binding cannot operate across maps");
        pawn.Dead = true;
        Check(!PrinterPawnBinding.IsAvailable(pawn, map), "dead binding pauses printing");
        pawn.Dead = false; pawn.Spawned = false;
        Check(!PrinterPawnBinding.IsAvailable(pawn, map), "despawned binding pauses printing");
        pawn.Spawned = true;
        Check(PrinterPawnBinding.WorkSpeed(pawn, recipe, 9) == 1.75f, "speed uses bound colonist's recipe stat");
        Check(PrinterPawnBinding.Efficiency(pawn, recipe, 9) == 2.5f, "yield uses bound colonist's recipe stat");
        Check(PrinterPawnBinding.WorkSpeed(pawn, artRecipe, 9) == 1 &&
            PrinterPawnBinding.Efficiency(pawn, artRecipe, 9) == 1, "stat-free recipes use vanilla unit factors");
        Check(PrinterPawnBinding.WorkSpeed(null, recipe, 9) == 9 &&
            PrinterPawnBinding.Efficiency(null, recipe, 8) == 8, "unbinding restores manual factors");
        Check(PrinterPawnBinding.RecipeSkillLevel(pawn, recipe) == 17 &&
            PrinterPawnBinding.RecipeSkillLevel(pawn, artRecipe) == 4, "each recipe uses its own work skill");

        var printer = new Building_CosmicPrinter { BoundPawn = pawn };
        printer.Settings.Efficiency = 10; printer.Settings.SkillLevel = 1;
        printer.Settings.InspirationChance = printer.Settings.FailureChance = 0;
        recipe.products.Add(new ThingDefCountClass { thingDef = new ThingDef { Quality = true }, count = 2 });
        var output = PrinterCraftingProduction.MakeProducts(printer, recipe, new List<Thing>(), null).ToList();
        Check(output.Sum(product => product.stackCount) == 5, "bound production uses colonist yield instead of manual multiplier");
        Check(QualityUtility.LastSkill == 17, "actual product quality uses the recipe skill");
        Check(printer.Settings.Efficiency == 10 && printer.Settings.SkillLevel == 1,
            "bound production preserves manual settings for later unbinding");
        var otherSkillPreview = printer.Settings.QualityProbabilities(4);
        var normal = new CosmicPrinterSettings { SkillLevel = 4, InspirationChance = 0, FailureChance = 0 };
        Check(otherSkillPreview.SequenceEqual(normal.QualityProbabilities()), "preview accepts selected work skill without changing manual skill");

        pawn.DisabledWork.Add(smithing);
        Check(PrinterPawnBinding.SkillLevel(pawn, crafting, smithing) == 0 &&
            PrinterPawnBinding.RecipeSkillLevel(pawn, recipe) == 0, "disabled work type counts as zero in preview and production");
        PrinterCraftingProduction.MakeProducts(printer, recipe, new List<Thing>(), null).ToList();
        Check(QualityUtility.LastSkill == 0, "disabled work type produces using skill zero");
        Check(PrinterPawnBinding.RecipeSkillLevel(pawn, artRecipe) == 4, "disabling one work type preserves other work skills");
        recipe.Routes.Add(new WorkGiverDef { workType = artistic });
        Check(PrinterPawnBinding.RecipeSkillLevel(pawn, recipe) == 17, "multi-route recipe can use an enabled work type");
        pawn.skills.Records[crafting].TotallyDisabled = true;
        Check(PrinterPawnBinding.RecipeSkillLevel(pawn, recipe) == 0, "disabled skill counts as zero even with an enabled route");
        Check(PrinterPawnBinding.SkillLevel(pawn, null, artistic) == 0, "work without a skill previews level zero");
        printer.BoundPawn = null;
        output = PrinterCraftingProduction.MakeProducts(printer, recipe, new List<Thing>(), null).ToList();
        Check(output.Sum(product => product.stackCount) == 20 && QualityUtility.LastSkill == 1,
            "unbinding restores manual yield and quality together");
    }

    private static void StatuesInitializeBeforeStorage()
    {
        var printer = new Building_CosmicPrinter();
        var recipe = new RecipeDef();
        recipe.products.Add(new ThingDefCountClass
        { thingDef = new ThingDef { Statue = true, Quality = true, Minifiable = true, stackLimit = 1 }, count = 2 });
        foreach (var boundPawn in new[] { null, new Pawn() })
        {
            printer.BoundPawn = boundPawn;
            var output = PrinterCraftingProduction.MakeProducts(printer, recipe, new List<Thing>(), null).ToList();
            Check(output.Count == 2, "each statue is returned as an individual product");
            Check(output.Cast<MinifiedThing>().All(item => item.InnerThing.Statue.Active &&
                item.InnerThing.Statue.SavedSubject != null && item.InnerThing.Statue.InitializationCalls == 1),
                "bound and unbound printers capture every statue subject before storage without drawing");
        }

        try
        {
            ThingMaker.OnMade = thing => { thing.Statue.Active = true; thing.Statue.SavedSubject = "Existing subject"; };
            var existing = PrinterCraftingProduction.MakeProducts(printer, recipe, new List<Thing>(), null).Cast<MinifiedThing>().ToList();
            Check(existing.All(item => item.InnerThing.Statue.SavedSubject == "Existing subject" &&
                item.InnerThing.Statue.InitializationCalls == 0), "already initialized statues retain their subject");
        }
        finally { ThingMaker.OnMade = null; }

        foreach (Action<CompStatue> failure in new Action<CompStatue>[]
        {
            statue => statue.parent.Destroy(),
            statue => { },
            statue => { statue.Active = true; throw new InvalidOperationException("Rendering initialization failed"); }
        })
        {
            Thing created = null;
            try
            {
                ThingMaker.OnMade = thing => created = thing;
                CompStatue.InitializeOverride = failure;
                bool stopped = false;
                try { PrinterCraftingProduction.MakeProducts(printer, recipe, new List<Thing>(), null).ToList(); }
                catch (InvalidOperationException) { stopped = true; }
                Check(stopped, "initialization failure stops output generation for the batch");
                Check(created != null && created.Destroyed, "failed statue is discarded before packaging or storage");
            }
            finally { ThingMaker.OnMade = null; CompStatue.InitializeOverride = null; }
        }
    }

    private sealed class SpecialIngredient : Thing
    {
        public override IEnumerable<Thing> ButcherProducts(object pawn, float efficiency)
        { ButcherCalls++; LastEfficiency = efficiency; yield return new Thing { def = new ThingDef() }; }
    }
}
