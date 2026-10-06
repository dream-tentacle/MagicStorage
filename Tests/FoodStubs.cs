// Controlled food/policy/pathfinding boundaries for production food outlet code.
// This does not emulate food thoughts, digestion, game serialization, or pathfinding.
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Verse
{
    public partial class ThingDef
    {
        public bool IsNutritionGivingIngestible, IsDrug;
        public float nutrition = 0.5f, foodScore = 300f;
        public RimWorld.IngestibleProperties ingestible = new RimWorld.IngestibleProperties();
    }
    public partial class Thing
    {
        public bool IngestibleNow = true, forbidden, burning, dessicated, stale;
        public bool IsForbidden(Pawn pawn) => forbidden;
        public bool IsBurning() => burning;
        public bool IsDessicated() => dessicated;
        public bool IsNotFresh() => stale;
    }
    public class Corpse : Thing { }
    public enum DevelopmentalStage { Adult, Baby }
    public static class DevelopmentalStageUtility { public static bool Baby(this DevelopmentalStage stage) => stage == DevelopmentalStage.Baby; }
    public partial class Pawn
    {
        public bool IsColonist = true, Drafted, Downed, InMentalState;
        public DevelopmentalStage DevelopmentalStage;
        public PawnInventory inventory = new PawnInventory();
        public PawnNeeds needs = new PawnNeeds();
        public PawnHealth health = new PawnHealth();
        public PawnGenes genes = new PawnGenes();
        public readonly HashSet<ThingDef> ForbiddenFoods = new HashSet<ThingDef>();
        public readonly HashSet<Thing> Unreachable = new HashSet<Thing>();
        public bool CanReach(Thing thing, PathEndMode mode, int danger) => !Unreachable.Contains(thing);
        public int NormalMaxDanger() => 0;
        public bool WillEat(Thing food, Pawn getter, bool careIfNotAcceptableForTitle, bool allowVenerated) => !ForbiddenFoods.Contains(food.def);
    }
    public class PawnInventory { public ThingOwner innerContainer = new ThingOwner(null); }
    public class PawnNeeds { public RimWorld.Need_Food food = new RimWorld.Need_Food(); }
    public class PawnHealth { public PawnCapacities capacities = new PawnCapacities(); }
    public class PawnCapacities { public bool manipulation = true; public bool CapableOf(object def) => manipulation; }
    public class PawnGenes { public bool DontMindRawFood; }
}
namespace RimWorld
{
    public enum HungerCategory { Fed, Hungry, UrgentlyHungry, Starving }
    public enum FoodPreferability { NeverForNutrition, DesperateOnly, RawBad, RawTasty, MealAwful, MealSimple, MealFine, MealLavish }
    public class IngestibleProperties { public FoodPreferability preferability = FoodPreferability.MealSimple; public bool HumanEdible = true; public int defaultNumToIngestAtOnce = 1; }
    public class Need_Food { public HungerCategory CurCategory = HungerCategory.Hungry; public float NutritionWanted = 0.5f, MaxLevel = 1f, CurLevelPercentage = 0.2f; }
    public static class PawnCapacityDefOf { public static readonly object Manipulation = new object(); }
    public static partial class JobDefOf { public static readonly JobDef Ingest = new JobDef(); }
    public static partial class FoodUtility
    {
        public static float NutritionForEater(Pawn pawn, Thing food) => food.def.nutrition;
        public static int WillIngestStackCountOf(Pawn pawn, ThingDef def, float nutrition) => Math.Max(1, (int)Math.Ceiling(pawn.needs.food.NutritionWanted / nutrition));
        public static float FoodOptimality(Pawn pawn, Thing food, ThingDef def, float distance, bool takingToInventory = false) => def.foodScore - distance;
    }
    public class JobGiver_GetFood : ThinkNode_JobGiver
    {
        private HungerCategory minCategory = HungerCategory.Fed;
        private float maxLevelPercentage = 1f;
        public static readonly Job VanillaFoodJob = new Job();
        public static bool HasVanillaFood = true;
        public override float GetPriority(Pawn pawn) => pawn.needs?.food != null && pawn.needs.food.CurCategory != HungerCategory.Fed &&
            pawn.needs.food.CurCategory >= minCategory && pawn.needs.food.CurLevelPercentage <= maxLevelPercentage ? 9.5f : 0f;
        protected override Job TryGiveJob(Pawn pawn) => HasVanillaFood ? VanillaFoodJob : null;
    }
}
namespace Verse.AI
{
    public class ThinkNode
    {
        public List<ThinkNode> subNodes = new List<ThinkNode>();
        public virtual float GetPriority(Pawn pawn) => 0f;
        public virtual Job Issue(Pawn pawn) => null;
    }
    public class ThinkNode_Priority : ThinkNode
    {
        public override Job Issue(Pawn pawn)
        { foreach (var child in subNodes) { var job = child.Issue(pawn); if (job != null) return job; } return null; }
    }
    public abstract class ThinkNode_JobGiver : ThinkNode
    {
        protected abstract Job TryGiveJob(Pawn pawn);
        public override Job Issue(Pawn pawn) => TryGiveJob(pawn);
    }
}
namespace MagicStorage
{
    public class Building_StorageFoodOutlet : Thing
    {
        public CompStorageNode node;
        public StorageNetwork Network => node?.Network;
        public bool CanWork => Spawned && Network?.CanWork == true;
    }
}
