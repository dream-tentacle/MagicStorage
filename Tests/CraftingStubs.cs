// Controlled engine boundaries. Production allocation, order, worker-policy,
// dispatcher and driver code are linked below; navigation/rendering/Scribe are not simulated.
using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using Verse;
using Verse.AI;

namespace Verse
{
    public static class Scribe_Defs { public static void Look<T>(ref T value, string key) { } }
    public static class Scribe_References { public static void Look<T>(ref T value, string key) { } }
    public struct IntRange { public int min, max; public IntRange(int a, int b) { min = a; max = b; } }
    public struct FloatRange { public float min, max; public FloatRange(float a, float b) { min = a; max = b; } public bool IncludesEpsilon(float v) => v >= min && v <= max; }
    public class SkillDef { }
    public class RecipeDef
    {
        public bool AvailableNow = true, allowMixingIngredients, ignoreIngredientCountTakeEntireStacks, interruptIfIngredientIsRotting, mechanitorOnlyRecipe;
        public RimWorld.ThingFilter fixedIngredientFilter = new RimWorld.ThingFilter(), defaultIngredientFilter;
        public List<IngredientCount> ingredients = new List<IngredientCount>();
        public IngredientValueGetter IngredientValueGetter = new IngredientValueGetter();
        public SkillDef workSkill;
        public int minimumSkill;
        public object effectWorking, soundWorking;
        public object specialProducts;
        public List<ThingDefCountClass> products = new List<ThingDefCountClass>();
        public ThingDef ProducedThingDef => products.FirstOrDefault()?.thingDef;
        public RimWorld.WorkGiverDef route;
        public object FirstSkillRequirementPawnDoesntSatisfy(Pawn pawn) => (pawn.skills?.GetSkill(workSkill).Level ?? pawn.RaceProps.mechFixedSkillLevel) < minimumSkill ? new object() : null;
    }
    public class IngredientValueGetter
    { public readonly Dictionary<ThingDef, float> Values = new Dictionary<ThingDef, float>(); public float ValuePerUnitOf(ThingDef def) => Values.TryGetValue(def, out var v) ? v : 1; }
    public class IngredientCount
    { public RimWorld.ThingFilter filter = new RimWorld.ThingFilter(); public bool IsFixedIngredient => filter.Allowed.Count == 1; public float count; public float GetBaseCount() => count; }
    public class ThingDefCountClass { public ThingDef thingDef; public int count; }
    public enum ThingRequestGroup { MinifiedThing, Blueprint, BuildingFrame }
    public partial struct IntVec3 { public bool Standable(Map map) => true; public bool Fogged(Map map) => false; }
    public enum Danger { Some }
    public partial class Thing { public string LabelShortCap => "test pawn"; public RimWorld.RotStage GetRotStage() => stale ? RimWorld.RotStage.Rotting : RimWorld.RotStage.Fresh; }
    public partial class ThingDef
    {
        public bool useHitPoints = true, MadeFromStuff, hasQualityComp;
        public RimWorld.ApparelProperties apparel;
        public bool HasComp(Type type) => type == typeof(RimWorld.CompQuality) && hasQualityComp;
    }
    public partial class Thing
    {
        public int MaxHitPoints = 100;
        public ThingDef Stuff;
        public RimWorld.QualityCategory quality;
        public bool hasQuality;
        public void Destroy() { holdingOwner?.Remove(this); Destroyed = true; }
        public bool TryGetQuality(out RimWorld.QualityCategory result) { result = quality; return hasQuality; }
        public bool SpawnedOrAnyParentSpawned => Spawned;
        public IntVec3 PositionHeld => Position;
        public Map MapHeld => Map;
    }
    public partial class ThingOwner { public List<Thing>.Enumerator GetEnumerator() => items.GetEnumerator(); }
    public partial class Map
    {
        public ThingLister listerThings = new ThingLister();
    }
    public class ThingLister
    {
        public List<Thing> Things = new List<Thing>();
        public IEnumerable<Thing> ThingsOfDef(ThingDef def) => Things.Where(t => t.def == def);
        public IEnumerable<Thing> ThingsInGroup(ThingRequestGroup group) => Things.Where(t =>
            group == ThingRequestGroup.Blueprint ? t is RimWorld.Blueprint :
            group == ThingRequestGroup.BuildingFrame ? t is RimWorld.Frame : t is MinifiedThing);
    }
    public partial class Pawn
    {
        public bool Dead, IsSlave, IsColonyMech, IsColonySubhuman, mechanitor;
        public RimWorld.WorkTags DisabledTags;
        public WorkSettings workSettings = new WorkSettings();
        public Skills skills = new Skills();
        public Job CurJob => jobs.curJob;
        public int ComfortTicks;
        public float CraftingXp;
        public bool IsFreeColonist = true;
        public PawnEquipment equipment = new PawnEquipment();
        public PawnApparel apparel = new PawnApparel();
        public bool WorkTypeIsDisabled(RimWorld.WorkTypeDef type) => workSettings.Disabled.Contains(type);
        public bool WorkTagIsDisabled(RimWorld.WorkTags tags) => (DisabledTags & tags) != 0;
        public bool CanReserveAndReach(Thing target, PathEndMode mode, Danger danger) => !Unreachable.Contains(target) && !Map.reservationManager.Reserved.Contains(target);
        public bool CanReserveSittableOrSpot(IntVec3 cell) => true;
        public bool ReserveSittableOrSpot(IntVec3 cell, Job job, bool error) => true;
        public void GainComfortFromCellIfPossible(int delta, bool chairsOnly) { ComfortTicks += delta; }
        public bool IsHashIntervalTick(int interval, int delta) => Find.TickManager.TicksGame % interval < delta;
    }
    public class WorkSettings
    { public HashSet<RimWorld.WorkTypeDef> Disabled = new HashSet<RimWorld.WorkTypeDef>(); public bool WorkIsActive(RimWorld.WorkTypeDef type) => !Disabled.Contains(type); }
    public class Skills { public SkillRecord record = new SkillRecord(); public SkillRecord GetSkill(SkillDef def) => record; }
    public class SkillRecord { public int Level = 10; }
    public class PawnEquipment { public List<Thing> AllEquipmentListForReading = new List<Thing>(); }
    public class PawnApparel { public List<Thing> WornApparel = new List<Thing>(); }
    public static class ModsConfig { public static bool BiotechActive = true; }
}
namespace RimWorld
{
    public class ApparelProperties { public bool careIfWornByCorpse = true; }
    public class CompQuality { }
    public enum RotStage { Fresh, Rotting }
    public enum QualityCategory { Awful, Poor, Normal, Good, Excellent, Masterwork, Legendary }
    public struct QualityRange
    { public QualityCategory min, max; public static QualityRange All => new QualityRange { max = QualityCategory.Legendary }; public bool Includes(QualityCategory v) => v >= min && v <= max; }
    public class Apparel : Thing { public bool WornByCorpse; }
    public class WorkTypeDef { }
    [Flags] public enum WorkTags { None = 0, Intellectual = 1 }
    public class WorkGiverDef
    { public WorkTypeDef workType; public bool nonColonistsCanDo, canBeDoneByMechs = true; public WorkTags workTags; public List<object> requiredCapacities = new List<object>(); }
    public abstract class WorkGiver { public WorkGiverDef def; public virtual Job NonScanJob(Pawn pawn) => null; }
    public static class MechanitorUtility { public static bool IsMechanitor(Pawn pawn) => pawn.mechanitor; }
}
namespace Verse.AI
{
    public enum ToilCompleteMode { Never }
    public static class ToilMaker { public static Toil MakeToil(string name) => new Toil(); }
    public static class CraftingToilExtensions
    {
        public static void WithEffect(this Toil toil, Func<object> effect, TargetIndex index) { }
        public static void PlaySustainerOrSound(this Toil toil, Func<object> sound) { }
        public static void WithProgressBar(this Toil toil, TargetIndex target, Func<float> progress, bool alwaysShow)
        { toil.ProgressTarget = target; toil.ProgressGetter = progress; }
    }
    public partial class Pawn_JobTracker
    { public int OverrideChecks; public Action OnOverride; public void CheckForJobOverride() { OverrideChecks++; OnOverride?.Invoke(); } }
}
namespace MagicStorage
{
    public class Building_CosmicWorkbench : Thing
    { public CompStorageNode node; public StorageNetwork Network => node?.Network; public IntVec3 InteractionCell => Position; }
    internal static class CraftingRecipeCatalog
    {
        internal static bool Supports(RecipeDef recipe) => recipe != null;
        internal static bool HasType(RecipeDef recipe, RimWorld.WorkTypeDef type) => recipe.route.workType == type;
        internal static IReadOnlyList<RimWorld.WorkGiverDef> Routes(RecipeDef recipe) => new[] { recipe.route };
    }
    internal sealed class TestProductCounter : ICraftingProductCounter
    { internal long Amount; public bool CanCount(RecipeDef recipe) => true; public long Count(Building_StorageCore core, CraftingOrder order) => Amount; }
    // Product/skill/game notification boundary; batch lifecycle remains production code.
    internal class TestProductionPolicy : ICraftingProductionPolicy
    {
        internal float Required = 10000, Speed = 1;
        internal int MakeCalls, ConsumeCalls, NotifyCalls;
        internal bool ThrowGeneration;
        internal ThingDef Product = new ThingDef();
        internal int ProductCount = 4;
        public Thing SelectDominant(RecipeDef recipe, List<Thing> input) => input.FirstOrDefault();
        public float WorkRequired(RecipeDef recipe, Thing dominant) => Required;
        public float WorkSpeed(RecipeDef recipe, Pawn pawn, Building_CosmicWorkbench bench) => Speed;
        public void Learn(RecipeDef recipe, Pawn worker, int ticks) { worker.CraftingXp += ticks * 0.1f; }
        public IEnumerable<Thing> MakeProducts(RecipeDef recipe, Pawn worker, List<Thing> input, Thing dominant)
        {
            MakeCalls++;
            yield return new Thing { def = Product, stackCount = ProductCount };
            if (ThrowGeneration) throw new Exception("controlled generation failure");
        }
        public void Consume(RecipeDef recipe, Thing item, Map map) { ConsumeCalls++; item.Destroy(); }
        public void NotifyCompleted(Pawn worker, List<Thing> products) { NotifyCalls++; }
    }
    internal sealed class VanillaCraftingProductionPolicy : TestProductionPolicy { }
}
