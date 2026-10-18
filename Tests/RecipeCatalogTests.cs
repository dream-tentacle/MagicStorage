using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class RecipeCatalogTests
{
    private static int assertions;
    private sealed class ReplacementWorkGiver { }
    private sealed class DerivedWorkGiver : WorkGiver_DoBill { }

    private static void Check(bool result, string message)
    { assertions++; if (!result) throw new Exception(message); }
    private static ThingDef Table(Type type, params RecipeDef[] recipes) =>
        new ThingDef { thingClass = type, AllRecipes = recipes.ToList() };
    private static WorkGiverDef Giver(WorkTypeDef type, Type implementation, params ThingDef[] tables) =>
        new WorkGiverDef { workType = type, giverClass = implementation, fixedBillGiverDefs = tables.ToList() };

    private static void Main()
    {
        var tailoring = new WorkTypeDef { labelShort = "tailor" };
        var smithing = new WorkTypeDef { labelShort = "smith", naturalPriority = 1 };
        var doctor = new WorkTypeDef { labelShort = "doctor" };
        var pants = new RecipeDef { label = "Pants" };
        var vanillaProduct = new RecipeDef { label = "Vanilla product" };
        var unnamedMedicine = new RecipeDef { defName = "UnnamedMedicine", label = null,
            products = new List<ThingDefCountClass> { new ThingDefCountClass {
                thingDef = new ThingDef { defName = "Medicine", label = "医药" } } } };
        var unnamedNoProduct = new RecipeDef { defName = "NoProductRecipe", label = null };
        var gestating = new RecipeDef { gestationCycles = 1 };
        var forming = new RecipeDef { formingTicks = 1 };
        var resurrection = new RecipeDef { mechResurrection = true };
        var surgery = new RecipeDef { IsSurgery = true };
        var wrongWorkType = new RecipeDef { requiredGiverWorkType = smithing };
        var automatic = new RecipeDef();
        var unsupportedTable = new RecipeDef();
        var unsupportedWorkType = new RecipeDef();
        var hand = Table(typeof(Building_WorkTable), pants);
        var electric = Table(typeof(Building_WorkTable), pants);
        var mechTable = Table(typeof(Building_WorkTable), gestating, forming, resurrection, surgery, wrongWorkType);
        var replacement = Giver(tailoring, typeof(ReplacementWorkGiver), hand, electric, mechTable);
        var derived = Giver(tailoring, typeof(DerivedWorkGiver), hand);
        var shared = new RecipeDef { label = "Shared recipe" };
        var smithOnly = new RecipeDef { label = "Smithing only", requiredGiverWorkType = smithing };
        var tailorShared = Table(typeof(Building_WorkTable), shared, smithOnly);
        var smithShared = Table(typeof(Building_WorkTable), shared, smithOnly);
        DefDatabase<WorkGiverDef>.AllDefsListForReading.AddRange(new[]
        {
            Giver(tailoring, typeof(WorkGiver_CosmicCrafting)),
            Giver(smithing, typeof(WorkGiver_CosmicCrafting)),
            replacement, derived,
            Giver(tailoring, typeof(ReplacementWorkGiver), tailorShared),
            Giver(smithing, typeof(WorkGiver_DoBill), smithShared),
            Giver(smithing, typeof(WorkGiver_DoBill), Table(typeof(Building_WorkTable), vanillaProduct, unnamedMedicine, unnamedNoProduct)),
            Giver(tailoring, typeof(ReplacementWorkGiver), Table(typeof(Building_WorkTableAutonomous), automatic)),
            Giver(tailoring, typeof(ReplacementWorkGiver), Table(typeof(object), unsupportedTable)),
            Giver(doctor, typeof(ReplacementWorkGiver), Table(typeof(Building_WorkTable), unsupportedWorkType)),
            new WorkGiverDef { workType = tailoring, giverClass = typeof(ReplacementWorkGiver) }
        });

        Check(CraftingRecipeCatalog.Available.Contains(pants), "A replaced tailoring work giver must expose its clothing recipe.");
        Check(CraftingRecipeCatalog.Routes(pants).Contains(replacement), "Keep the replacement's work definition for worker eligibility.");
        Check(CraftingRecipeCatalog.Routes(pants).Contains(derived), "Derived work giver implementations must also be supported.");
        Check(CraftingRecipeCatalog.Routes(pants).Count == 2, "One route per work giver even when a recipe uses multiple benches.");
        Check(CraftingRecipeCatalog.Workbenches(pants).Count == 2, "Deduplicate the recipe's workbenches.");
        Check(CraftingRecipeCatalog.HasType(pants, tailoring), "Clothing remains tailoring work.");
        Check(!CraftingRecipeCatalog.HasType(pants, smithing), "Clothing must not acquire an unrelated work type.");
        Check(CraftingRecipeCatalog.WorkLabels(pants) == "Tailor", "English work types display labelShort when the generic label is absent, without duplicate routes.");
        Check(CraftingRecipeCatalog.WorkLabels(shared) == "Tailor / Smith", "Shared recipes display each localized work type name.");
        tailoring.labelShort = "裁缝";
        Check(CraftingRecipeCatalog.WorkLabels(pants) == "裁缝", "Work names use the current language rather than a cached English label.");
        tailoring.labelShort = "tailor";
        Check(CraftingRecipeCatalog.Available.Contains(vanillaProduct), "Original production work givers remain supported.");
        Check(CraftingRecipeCatalog.AvailableWorkbenches(tailoring).Contains(hand), "Replacement work givers expose their workbenches in the browser.");
        Check(CraftingRecipeCatalog.AvailableWorkbenches(smithing).Contains(smithShared), "The smithing category includes the smithing table.");
        Check(!CraftingRecipeCatalog.AvailableWorkbenches(smithing).Contains(tailorShared), "A shared recipe must not put a tailoring table in the smithing category.");
        Check(!CraftingRecipeCatalog.AvailableWorkbenches(tailoring).Contains(smithShared), "A shared recipe must not put a smithing table in the tailoring category.");
        Check(CraftingRecipeCatalog.AvailableForWorkbench(smithShared).Contains(smithOnly), "A work-type restricted recipe appears on a matching table.");
        Check(!CraftingRecipeCatalog.AvailableForWorkbench(tailorShared).Contains(smithOnly), "Another table's route cannot expose a recipe with a mismatching required work type.");
        Check(!CraftingRecipeCatalog.AvailableForWorkbench(hand).Contains(vanillaProduct), "A workbench picker excludes other workbenches' recipes.");
        Check(CraftingRecipeCatalog.AvailableForWorkbench(hand).Count(r => r == pants) == 1, "Multiple work givers must not duplicate a workbench recipe.");
        foreach (var excluded in new[] { gestating, forming, resurrection, surgery, wrongWorkType, automatic, unsupportedTable, unsupportedWorkType })
        {
            Check(!CraftingRecipeCatalog.Supports(excluded), "Unsupported production must not enter the catalog.");
            Check(!CraftingRecipeCatalog.Available.Contains(excluded), "Unsupported production must not appear in the picker.");
        }

        hand.IsResearchFinished = electric.IsResearchFinished = false;
        Check(!CraftingRecipeCatalog.Available.Contains(pants), "Both workbenches locked hides clothing from addition.");
        Check(CraftingRecipeCatalog.Supports(pants), "Workbench research must not erase an existing order's route.");
        electric.IsResearchFinished = true;
        Check(CraftingRecipeCatalog.CanAdd(pants), "Researching either workbench enables the shared recipe.");
        Check(!CraftingRecipeCatalog.AvailableWorkbenches().Contains(hand), "An unlocked alternative must not expose a locked workbench.");
        Check(!CraftingRecipeCatalog.AvailableForWorkbench(hand).Any(), "An unlocked alternative must not bypass the selected workbench's research gate.");
        Check(CraftingRecipeCatalog.AvailableForWorkbench(electric).Contains(pants), "The unlocked workbench exposes its shared recipe.");
        pants.AvailableNow = false;
        Check(!CraftingRecipeCatalog.CanAdd(pants), "The recipe's own unlock conditions still apply.");
        Check(!CraftingRecipeCatalog.AvailableForWorkbench(electric).Any(), "The workbench picker hides recipes whose own research is locked.");
        Check(!CraftingRecipeCatalog.AvailableWorkbenches().Contains(electric), "Do not offer a workbench with no unlocked supported recipes.");
        Check(CraftingRecipeCatalog.Supports(pants), "Recipe availability does not mutate the catalog.");
        Check(!CraftingRecipeCatalog.CanAdd(null), "A missing recipe cannot be added.");
        Check(CraftingRecipeCatalog.Label(vanillaProduct) == "Vanilla product", "Preserve existing recipe names.");
        Check(CraftingRecipeCatalog.Label(unnamedMedicine) == "制作医药", "A null name uses the product name.");
        Check(CraftingRecipeCatalog.Label(unnamedNoProduct) == "NoProductRecipe", "No products falls back to the recipe defName.");
        var shown = CraftingRecipeCatalog.Available.Where(r => CraftingRecipeCatalog.MatchesSearch(r, "")).ToList();
        Check(shown.Contains(unnamedMedicine) && shown.Contains(unnamedNoProduct), "Empty-name recipes do not interrupt building the unfiltered list.");
        Check(shown.Select(CraftingRecipeCatalog.Label).SequenceEqual(shown.Select(CraftingRecipeCatalog.Label).OrderBy(s => s)),
            "Sort by displayed fallback names.");
        Check(CraftingRecipeCatalog.MatchesSearch(unnamedMedicine, "医药"), "Search finds the fallback product name.");
        Check(CraftingRecipeCatalog.MatchesSearch(unnamedNoProduct, "noproduct"), "Search finds fallback defName without case sensitivity.");
        Check(!CraftingRecipeCatalog.MatchesSearch(unnamedMedicine, "钢铁"), "Fallback search still excludes unrelated recipes.");
        unnamedMedicine.label = "";
        Check(CraftingRecipeCatalog.Label(unnamedMedicine) == "制作医药", "Empty names also use the product name.");
        unnamedMedicine.label = "  ";
        Check(CraftingRecipeCatalog.Label(unnamedMedicine) == "制作医药", "Whitespace names also use the product name.");
        unnamedMedicine.products.Insert(0, null);
        unnamedMedicine.products.Insert(0, new ThingDefCountClass());
        Check(CraftingRecipeCatalog.Label(unnamedMedicine) == "制作医药", "Skip empty product entries when a valid product follows.");
        unnamedMedicine.products[2].thingDef.label = null;
        Check(CraftingRecipeCatalog.Label(unnamedMedicine) == "制作Medicine", "A nameless product uses its defName.");
        unnamedNoProduct.products = null;
        Check(CraftingRecipeCatalog.Label(unnamedNoProduct) == "NoProductRecipe", "A null product list falls back to recipe defName.");
        unnamedNoProduct.products = new List<ThingDefCountClass> { null, new ThingDefCountClass() };
        Check(CraftingRecipeCatalog.Label(unnamedNoProduct) == "NoProductRecipe", "An entirely empty product list falls back to recipe defName.");
        assertions += RecipeCategoryTests.Run();
        Console.WriteLine("PASS: " + assertions + " recipe catalog and category assertions.");
    }
}

// Minimal engine data types; this project compiles the production catalog itself.
namespace Verse
{
    public static class Translation
    {
        public static string Translate(this string key, string product) => key == "RecipeMake" ? "制作" + product : key;
        public static string CapitalizeFirst(this string text) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);
    }
    public static class DefDatabase<T>
    { public static readonly List<T> AllDefsListForReading = new List<T>(); }
    public sealed class ThingDef
    {
        public string defName, label;
        public Type thingClass;
        public bool IsResearchFinished = true;
        public List<RecipeDef> AllRecipes = new List<RecipeDef>();
        public List<ThingCategoryDef> thingCategories;
    }
    public sealed class ThingCategoryDef
    {
        public string defName, label;
        public ThingCategoryDef parent;
    }
    public sealed class RecipeDef
    {
        public string defName;
        public string label = "recipe";
        public List<ThingDefCountClass> products = new List<ThingDefCountClass>();
        public bool AvailableNow = true, IsSurgery = false, mechResurrection = false;
        public int gestationCycles = 0, formingTicks = 0;
        public WorkTypeDef requiredGiverWorkType = null;
    }
    public sealed class ThingDefCountClass { public ThingDef thingDef; }
}
namespace RimWorld
{
    public class Building_WorkTable { }
    public class Building_WorkTableAutonomous : Building_WorkTable { }
    public class WorkGiver_DoBill { }
    public sealed class WorkTypeDef
    {
        public string label, labelShort;
        public int naturalPriority = 0;
        public string LabelCap => label;
    }
    public sealed class WorkGiverDef
    {
        public Type giverClass;
        public WorkTypeDef workType;
        public List<ThingDef> fixedBillGiverDefs;
    }
}
namespace MagicStorage { internal sealed class WorkGiver_CosmicCrafting { } }
