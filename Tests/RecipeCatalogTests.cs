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
        var tailoring = new WorkTypeDef { label = "Tailoring" };
        var smithing = new WorkTypeDef { label = "Smithing", naturalPriority = 1 };
        var doctor = new WorkTypeDef { label = "Doctor" };
        var pants = new RecipeDef { label = "Pants" };
        var vanillaProduct = new RecipeDef { label = "Vanilla product" };
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
        DefDatabase<WorkGiverDef>.AllDefsListForReading.AddRange(new[]
        {
            Giver(tailoring, typeof(WorkGiver_CosmicCrafting)),
            Giver(smithing, typeof(WorkGiver_CosmicCrafting)),
            replacement, derived,
            Giver(smithing, typeof(WorkGiver_DoBill), Table(typeof(Building_WorkTable), vanillaProduct)),
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
        Check(CraftingRecipeCatalog.Available.Contains(vanillaProduct), "Original production work givers remain supported.");
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
        pants.AvailableNow = false;
        Check(!CraftingRecipeCatalog.CanAdd(pants), "The recipe's own unlock conditions still apply.");
        Check(CraftingRecipeCatalog.Supports(pants), "Recipe availability does not mutate the catalog.");
        Check(!CraftingRecipeCatalog.CanAdd(null), "A missing recipe cannot be added.");
        Console.WriteLine("PASS: " + assertions + " recipe catalog assertions.");
    }
}

// Minimal engine data types; this project compiles the production catalog itself.
namespace Verse
{
    public static class DefDatabase<T>
    { public static readonly List<T> AllDefsListForReading = new List<T>(); }
    public sealed class ThingDef
    {
        public Type thingClass;
        public bool IsResearchFinished = true;
        public List<RecipeDef> AllRecipes = new List<RecipeDef>();
    }
    public sealed class RecipeDef
    {
        public string label = "recipe";
        public bool AvailableNow = true, IsSurgery = false, mechResurrection = false;
        public int gestationCycles = 0, formingTicks = 0;
        public WorkTypeDef requiredGiverWorkType = null;
    }
}
namespace RimWorld
{
    public class Building_WorkTable { }
    public class Building_WorkTableAutonomous : Building_WorkTable { }
    public class WorkGiver_DoBill { }
    public sealed class WorkTypeDef
    {
        public string label;
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
