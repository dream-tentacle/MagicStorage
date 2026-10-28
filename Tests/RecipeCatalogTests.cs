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
        Check(CraftingRecipeCatalog.HasWorkbenchType(tailorShared, shared, tailoring) &&
            !CraftingRecipeCatalog.HasWorkbenchType(tailorShared, shared, smithing),
            "Filtering a workbench snapshot must use that table's work type, not another table's route for a shared recipe.");
        Check(!CraftingRecipeCatalog.HasWorkbenchType(tailorShared, smithOnly, tailoring) &&
            CraftingRecipeCatalog.HasWorkbenchType(smithShared, smithOnly, smithing),
            "The workbench index preserves a recipe's required giver work type.");
        int tableRecipeReads = hand.AllRecipesReads;
        CraftingRecipeCatalog.AvailableForWorkbench(hand).ToList();
        Check(hand.AllRecipesReads == tableRecipeReads,
            "Browsing uses the initialized table index instead of reading the table's recipe collection again.");
        int availabilityReads = pants.AvailabilityReads;
        var browserSnapshot = CraftingRecipeCatalog.SnapshotWorkbenchTypes();
        Check(pants.AvailabilityReads == availabilityReads + 1,
            "A shared recipe's availability is evaluated once per snapshot, even when multiple tables provide it.");
        foreach (var type in new[] { null, tailoring, smithing })
            Check(new HashSet<ThingDef>(browserSnapshot.Where(pair => type == null || pair.Value.Contains(type)).Select(pair => pair.Key))
                .SetEquals(CraftingRecipeCatalog.AvailableWorkbenches(type)),
                "Snapshot work type tabs expose exactly the same researched, supported tables as a fresh browser.");
        foreach (var excluded in new[] { gestating, forming, resurrection, surgery, wrongWorkType, automatic, unsupportedTable, unsupportedWorkType })
        {
            Check(!CraftingRecipeCatalog.Supports(excluded), "Unsupported production must not enter the catalog.");
            Check(!CraftingRecipeCatalog.Available.Contains(excluded), "Unsupported production must not appear in the picker.");
        }

        hand.IsResearchFinished = electric.IsResearchFinished = false;
        Check(browserSnapshot.ContainsKey(hand) && browserSnapshot.ContainsKey(electric),
            "An open browser keeps its availability snapshot when research changes.");
        var reopenedSnapshot = CraftingRecipeCatalog.SnapshotWorkbenchTypes();
        Check(!reopenedSnapshot.ContainsKey(hand) && !reopenedSnapshot.ContainsKey(electric),
            "Reopening the browser reads the current research state.");
        Check(!CraftingRecipeCatalog.Available.Contains(pants), "Both workbenches locked hides clothing from addition.");
        Check(CraftingRecipeCatalog.Supports(pants), "Workbench research must not erase an existing order's route.");
        electric.IsResearchFinished = true;
        Check(CraftingRecipeCatalog.CanAdd(pants), "Researching either workbench enables the shared recipe.");
        Check(!CraftingRecipeCatalog.AvailableWorkbenches().Contains(hand), "An unlocked alternative must not expose a locked workbench.");
        Check(!CraftingRecipeCatalog.AvailableForWorkbench(hand).Any(), "An unlocked alternative must not bypass the selected workbench's research gate.");
        Check(CraftingRecipeCatalog.AvailableForWorkbench(electric).Contains(pants), "The unlocked workbench exposes its shared recipe.");
        pants.AvailableNow = false;
        Check(!CraftingRecipeCatalog.SnapshotWorkbenchTypes().ContainsKey(electric),
            "A fresh snapshot excludes a researched table whose only supported recipe is unavailable.");
        Check(!CraftingRecipeCatalog.CanAdd(pants), "The recipe's own unlock conditions still apply.");
        Check(!CraftingRecipeCatalog.AvailableForWorkbench(electric).Any(), "The workbench picker hides recipes whose own research is locked.");
        Check(!CraftingRecipeCatalog.AvailableWorkbenches().Contains(electric), "Do not offer a workbench with no unlocked supported recipes.");
        Check(CraftingRecipeCatalog.Supports(pants), "Recipe availability does not mutate the catalog.");
        pants.AvailableNow = true;
        hand.IsResearchFinished = true;
        CraftingWorkbenchUnlocks.Allows = table => table != hand && table != electric;
        Check(!CraftingRecipeCatalog.CanAdd(pants), "Hide a recipe when every source workbench is unpaid.");
        Check(!CraftingRecipeCatalog.Available.Contains(pants), "The all-recipes picker applies the unlock gate.");
        Check(!CraftingRecipeCatalog.AvailableForWorkbench(hand).Any(), "A locked workbench cannot expose its recipes.");
        Check(CraftingRecipeCatalog.AvailableWorkbenches().Contains(hand), "Locked workbenches stay in the browser for unlocking.");
        Check(CraftingRecipeCatalog.AvailableWorkbenches().Contains(electric), "Every researched alternative remains available to unlock.");
        Check(CraftingRecipeCatalog.SnapshotWorkbenchTypes().ContainsKey(hand),
            "Paid unlock gates do not hide tables from the snapshot's unlock browser.");
        CraftingWorkbenchUnlocks.Allows = table => table != hand;
        Check(CraftingRecipeCatalog.CanAdd(pants), "One paid alternative exposes a shared recipe.");
        Check(CraftingRecipeCatalog.AvailableForWorkbench(electric).Contains(pants), "A paid workbench exposes its recipes.");
        Check(!CraftingRecipeCatalog.AvailableForWorkbench(hand).Any(), "An alternative unlock does not pay for the selected workbench.");
        electric.IsResearchFinished = false;
        Check(!CraftingRecipeCatalog.CanAdd(pants), "The paid alternative must also meet its research conditions.");
        electric.IsResearchFinished = true;
        CraftingWorkbenchUnlocks.Allows = table => true;
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
        var sculpture = new ThingDef { label = "Sculpture" };
        var sculptureRecipe = new RecipeDef { label = "Make sculpture",
            products = new List<ThingDefCountClass> { new ThingDefCountClass { thingDef = sculpture } } };
        var primary = new Ideo(); var minor = new Ideo();
        var primarySculpture = new Precept_Building { ThingDef = sculpture, LabelCap = "Primary monument" };
        var minorSculpture = new Precept_Building { ThingDef = sculpture, LabelCap = "Minor monument" };
        primary.cachedPossibleBuildings.Add(primarySculpture);
        primary.cachedPossibleBuildings.Add(new Precept_Building { ThingDef = new ThingDef() });
        minor.cachedPossibleBuildings.Add(minorSculpture);
        Faction.OfPlayer.ideos.AllIdeos.AddRange(new[] { primary, minor });
        Check(CraftingRecipeCatalog.PreceptsFor(sculptureRecipe).SequenceEqual(new[] { primarySculpture, minorSculpture }),
            "Like ITab_Bills, expose each matching building precept from every player ideoligion.");
        Check(CraftingRecipeCatalog.Label(sculptureRecipe, primarySculpture) == "制作Primary monument",
            "Cultural entries use RecipeMake with the precept name.");
        Check(CraftingRecipeCatalog.Label(sculptureRecipe, null) == "Make sculpture", "Keep the ordinary recipe option.");
        Faction.OfPlayer.ideos.AllIdeos.Remove(minor);
        Check(CraftingRecipeCatalog.PreceptsFor(sculptureRecipe).SequenceEqual(new[] { primarySculpture }),
            "Reopening choices reads current faction ideoligions rather than a cached list.");
        primary.cachedPossibleBuildings.Clear();
        Check(!CraftingRecipeCatalog.PreceptsFor(sculptureRecipe).Any(), "Removed building precepts stop creating new options.");
        Faction.OfPlayer.ideos.AllIdeos.Clear();
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
        private List<RecipeDef> allRecipes = new List<RecipeDef>();
        public int AllRecipesReads;
        public List<RecipeDef> AllRecipes
        { get { AllRecipesReads++; return allRecipes; } set { allRecipes = value; } }
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
        public ThingDef ProducedThingDef => products?.FirstOrDefault(p => p?.thingDef != null)?.thingDef;
        private bool availableNow = true;
        public int AvailabilityReads;
        public bool AvailableNow
        { get { AvailabilityReads++; return availableNow; } set { availableNow = value; } }
        public bool IsSurgery = false, mechResurrection = false;
        public int gestationCycles = 0, formingTicks = 0;
        public WorkTypeDef requiredGiverWorkType = null;
    }
    public sealed class ThingDefCountClass { public ThingDef thingDef; }
}
namespace RimWorld
{
    public class Precept_ThingStyle { public string LabelCap; public ThingDef ThingDef; }
    public sealed class Precept_Building : Precept_ThingStyle { }
    public sealed class Ideo { public readonly List<Precept_Building> cachedPossibleBuildings = new List<Precept_Building>(); }
    public sealed class FactionIdeos { public readonly List<Ideo> AllIdeos = new List<Ideo>(); }
    public sealed class Faction { public static readonly Faction OfPlayer = new Faction(); public readonly FactionIdeos ideos = new FactionIdeos(); }
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
namespace MagicStorage
{
    internal sealed class WorkGiver_CosmicCrafting { }
    internal static class CraftingWorkbenchUnlocks
    {
        internal static Func<ThingDef, bool> Allows = table => true;
        internal static bool IsUnlocked(ThingDef table) => Allows(table);
    }
}
