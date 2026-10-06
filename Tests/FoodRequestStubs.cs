// Engine boundaries for the actual request selector, fetch driver and think nodes.
// Eligibility of native work targets is controlled explicitly, not an emulation of game AI.
using System;
using System.Collections.Generic;
using MagicStorage;
using RimWorld;
using Verse;
using Verse.AI;

namespace Verse
{
    public class DefModExtension { }
    public static class DefDatabase<T> where T : class
    {
        private static readonly Dictionary<string, T> values = new Dictionary<string, T>();
        public static T GetNamed(string name)
        {
            if (!values.TryGetValue(name, out var value))
            {
                var purpose = (StorageFoodPurpose)Enum.Parse(typeof(StorageFoodPurpose), name.Substring("MS_StorageFood".Length));
                value = new JobDef { extension = new StorageFoodJobSettings { purpose = purpose } } as T;
                values[name] = value;
            }
            return value;
        }
    }
    public class Faction { public static readonly Faction OfPlayer = new Faction(); }
    public partial struct IntVec3
    {
        public bool InHorDistOf(IntVec3 other, float distance) => DistanceToSquared(other) <= distance * distance;
        public bool IsForbidden(Pawn pawn) => false;
        public static bool operator ==(IntVec3 a, IntVec3 b) => a.Equals(b);
        public static bool operator !=(IntVec3 a, IntVec3 b) => !a.Equals(b);
    }
    public partial class Thing
    {
        public bool sociallyProper = true, workNeeded = true, acceptedAsNutrition = true;
        public int nutritionWanted = 10;
        public float GetStatValue(object stat) => def.nutrition;
        public bool IsSociallyProper(Pawn pawn, bool forPrisoner = false, bool animalsCare = true) => sociallyProper;
    }
    public partial class ThingOwner
    {
        public bool rejectDrop;
        public bool TryDrop(Thing item, IntVec3 position, Map map, ThingPlaceMode mode, out Thing result,
            Action<Thing, int> placedAction = null, Predicate<IntVec3> nearPlaceValidator = null, bool playDropSound = true)
        {
            result = null;
            if (rejectDrop || !Contains(item) || (nearPlaceValidator != null && !nearPlaceValidator(position))) return false;
            int count = Math.Min(item.stackCount, map.DropBudget);
            if (count <= 0) return false;
            map.DropBudget -= count;
            bool complete = count == item.stackCount;
            Thing part = item.SplitOff(count);
            part.forbidden = item.forbidden;
            part.Map = map; part.Position = position; part.Spawned = true;
            map.Ground.Add(part); result = part; placedAction?.Invoke(part, count);
            return complete;
        }
    }
    public enum ThingPlaceMode { Near }
    public partial class Pawn
    {
        public bool IsAnimal, IsColonyMechPlayerControlled, IsPrisonerOfColony, formingCaravan;
        public int massSpace = 100;
        public PawnRoping roping = new PawnRoping();
        public RaceProperties RaceProps = new RaceProperties();
        public MindState mindState = new MindState();
        public bool CanReach(IntVec3 cell, PathEndMode mode, int danger) => true;
        public bool CanReserve(Pawn target) => true;
    }
    public class PawnRoping { public bool IsRoped; public LocalTargetInfo RopedTo; }
    public class RaceProperties { public bool ToolUser = true; }
    public class MindState { public PawnDuty duty; public int lastIngestTick = -2000; }
    public class PawnDuty { public LocalTargetInfo focus; }
    public partial class Map { public ResourceCounter resourceCounter = new ResourceCounter(); }
    public class ResourceCounter { public float TotalHumanEdibleNutrition; }
}
namespace Verse.AI
{
    public class MentalStateWorker { public virtual bool StateCanOccur(Pawn pawn) => true; }
}
namespace RimWorld.Planet
{
    public static class CaravanUtility { public static bool IsFormingCaravan(this Pawn pawn) => pawn.formingCaravan; }
}
namespace RimWorld
{
    public static class StatDefOf { public static readonly object Nutrition = new object(); }
    public static class MassUtility
    {
        public static int CountToPickUpUntilOverEncumbered(Pawn pawn, Thing food) => pawn.massSpace;
        public static bool IsOverEncumbered(Pawn pawn) => pawn.massSpace <= 0;
    }
    public static class GatheringsUtility { public static bool InGatheringArea(IntVec3 point, IntVec3 center, Map map) => point.InHorDistOf(center, 8f); }
    public static class RCellFinder { public static IntVec3 SpotToChewStandingNear(Pawn pawn, Thing food) => pawn.Position; }
    public static partial class JobDefOf
    {
        public static readonly JobDef FeedPatient = new JobDef(), DeliverFood = new JobDef(), BottlefeedBaby = new JobDef(),
            Train = new JobDef(), Tame = new JobDef(), HaulToCell = new JobDef(), HaulToContainer = new JobDef();
    }
    public class JobDriver_InteractAnimal { public static float RequiredNutritionPerFeed(Pawn animal) => 0.125f; }
    public static partial class FoodUtility
    {
        public static Thing DispenserSource;
        public static bool TryFindBestFoodSourceFor(Pawn getter, Pawn eater, bool desperate, out Thing source, out ThingDef def, bool canRefillDispenser = true, bool canUsePackAnimalInventory = false)
        { source = DispenserSource; def = source?.def; return source != null; }
        public static int StackCountForNutrition(float wanted, float perItem) => (int)Math.Ceiling(wanted / perItem);
        public class ThoughtFromIngesting { public Thought thought = new Thought(); }
        public class Thought { public List<ThoughtStage> stages = new List<ThoughtStage> { new ThoughtStage() }; }
        public class ThoughtStage { public float baseMoodEffect; }
        public static List<ThoughtFromIngesting> ThoughtsFromIngesting(Pawn pawn, Thing food, ThingDef def) => new List<ThoughtFromIngesting>();
    }
    public class JobGiver_PackFood : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) => JobGiver_GetFood.VanillaFoodJob;
        public static float GetInventoryPackableFoodNutrition(Pawn pawn)
        {
            float result = 0;
            for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
            { var item = pawn.inventory.innerContainer[i]; if (IsGoodPackableFoodFor(item, pawn, false)) result += item.stackCount * item.def.nutrition; }
            return result;
        }
        public static bool IsGoodPackableFoodFor(Thing food, Pawn pawn, bool checkMass) =>
            food.def.ingestible.preferability >= FoodPreferability.MealAwful && (!checkMass || pawn.massSpace > 0) && !pawn.ForbiddenFoods.Contains(food.def);
    }
    public class JobGiver_EatInGatheringArea : ThinkNode_JobGiver { protected override Job TryGiveJob(Pawn pawn) => JobGiver_GetFood.VanillaFoodJob; }
    public class Building_NutrientPasteDispenser : Thing
    {
        public Thing Hopper;
        public bool HasEnoughFeedstockInHoppers() => false;
        public Thing AdjacentReachableHopper(Pawn pawn) => Hopper;
    }
    public class JobGiver_BingeFood : ThinkNode_JobGiver
    {
        protected int IngestInterval(Pawn pawn) => 1100;
        protected override Job TryGiveJob(Pawn pawn) => JobGiver_GetFood.VanillaFoodJob;
    }
    public class JobGiver_Autofeed : ThinkNode_JobGiver { protected override Job TryGiveJob(Pawn pawn) => JobGiver_GetFood.VanillaFoodJob; }
    public enum AutofeedMode { Urgent }
    public static class ChildcareUtility
    {
        public static Pawn Baby;
        public static Thing Source;
        public static bool HasBreastfeeder;
        public static Pawn FindAutofeedBaby(Pawn pawn, AutofeedMode mode, out Thing source) { source = Source; return Baby; }
        public static bool ImmobileBreastfeederAvailable(Pawn pawn, Pawn baby, bool forced, out Pawn mother, out object reason)
        { mother = null; reason = null; return HasBreastfeeder; }
        public static Job MakeBottlefeedJob(Pawn baby, Thing food) => JobMaker.MakeJob(JobDefOf.BottlefeedBaby, baby, food);
    }
}
namespace MagicStorage
{
    internal static class StorageFoodWorkUtility
    {
        internal static bool StillNeeded(Pawn pawn, StorageFoodPurpose purpose, LocalTargetInfo target, bool forced = false) => !target.HasThing || target.Thing.workNeeded;
        internal static bool DeviceAccepts(Pawn pawn, Thing target, Thing food, StorageFoodPurpose purpose) => target?.acceptedAsNutrition == true;
        internal static int DeviceCount(Thing target, Thing food, StorageFoodPurpose purpose) => target.nutritionWanted;
    }
}
