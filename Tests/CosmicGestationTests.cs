// Exercise production catalog, allocation, delivery, rollback and refunds.
// Only engine/storage boundaries are simulated; native cycles and Pawn generation
// are deliberately left to the real game and are not claimed by these tests.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;
using Verse.AI;

internal static class CosmicGestationTests
{
    private static int checks;
    private static ThingDef steel = new ThingDef { defName = "Steel" };
    private static ThingDef component = new ThingDef { defName = "Component" };
    private static ThingDef mech = new ThingDef { race = new RaceProperties { IsMechanoid = true } };
    private static RecipeDef recipe, twoMaterials, resurrect;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private sealed class CustomTable : Building_MechGestator { }
    private sealed class CustomRecipe : RecipeDef { }
    private sealed class CustomWorker : RecipeWorker { }
    private static RecipeDef Recipe() => new RecipeDef { products = new List<ThingDefCountClass>
        { new ThingDefCountClass { thingDef = mech, count = 1 } }, ingredients = new List<IngredientCount>
        { new IngredientCount { filter = new ThingFilter(steel), Count = 10 } } };
    private static ThingDef Source(Type type, params RecipeDef[] recipes) => new ThingDef
        { thingClass = type, AllRecipes = recipes.ToList() };

    private static void Main()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(CosmicGestationPatches).TypeHandle);
        Catalog(); ReadOnlyPlanning(); ShelfMaterials(); ShortageDetails(); DeliveryAndResume(); Failures(); Eligibility(); Refunds();
        MenuFailures();
        checks += CosmicGestationPowerTests.Run();
        Console.WriteLine("PASS: " + checks + " cosmic gestation assertions.");
    }
    private static void Catalog()
    {
        recipe = Recipe(); twoMaterials = Recipe();
        twoMaterials.ingredients.Add(new IngredientCount { filter = new ThingFilter(component), Count = 2 });
        resurrect = Recipe(); resurrect.mechResurrection = true; resurrect.products.Clear();
        var customTableRecipe = Recipe();
        var customWorkerRecipe = Recipe(); customWorkerRecipe.Worker = new CustomWorker();
        var unfinished = Recipe(); unfinished.UsesUnfinishedThing = true;
        var target = new ThingDef { defName = "MS_CosmicMechGestator" };
        var source = Source(typeof(Building_MechGestator), recipe, twoMaterials, resurrect, customWorkerRecipe, unfinished);
        DefDatabase<ThingDef>.AllDefsListForReading.AddRange(new[] { target, source,
            Source(typeof(CustomTable), customTableRecipe) });
        Check(CosmicGestationCatalog.Available(recipe), "Native source recipes are available.");
        Check(target.AllRecipes.Count == 3, "Import native production and resurrection only.");
        Check(!target.AllRecipes.Contains(customTableRecipe), "Do not infer compatibility from a custom table subclass.");
        Check(!CosmicGestationCatalog.UsesNativePipeline(customWorkerRecipe), "Reject custom recipe workers.");
        Check(!CosmicGestationCatalog.UsesNativePipeline(new CustomRecipe()), "Reject custom recipe types.");
        Check(!CosmicGestationCatalog.UsesNativePipeline(unfinished), "Reject unfinished-work production.");
        source.IsResearchFinished = false;
        Check(!CosmicGestationCatalog.Available(recipe), "Source table research still gates imported recipes.");
        source.IsResearchFinished = true; recipe.AvailableNow = false;
        Check(!CosmicGestationCatalog.Available(recipe), "Recipe research still applies.");
        recipe.AvailableNow = true;
        Check(!CosmicGestationCatalog.Supports(new Bill { recipe = recipe }), "Reject non-mech bill implementations.");
    }
    private sealed class Fixture
    {
        internal Map Map = new Map();
        internal Pawn Pawn = new Pawn { Faction = Faction.OfPlayer, mechanitor = new MechanitorTracker() };
        internal StorageNetwork Network = new StorageNetwork();
        internal Building_CosmicMechGestator Table;
        internal Bill_Mech Bill;
        internal Job Job;
        internal Fixture(RecipeDef selected = null)
        {
            Network.Core = new Building_StorageCore { Map = Map, Faction = Faction.OfPlayer, Network = Network };
            var unit = (Building_StorageUnit)Network.Contents.Owner;
            unit.Map = Map; unit.Faction = Faction.OfPlayer; unit.Network = Network;
            Table = new Building_CosmicMechGestator { Map = Map, Faction = Faction.OfPlayer,
                node = new CompStorageNode { Network = Network } };
            Bill = selected == resurrect ? (Bill_Mech)new Bill_ResurrectMech() : new Bill_ProductionMech();
            Bill.recipe = selected ?? recipe; Bill.billStack = new BillStack { billGiver = Table };
            Table.MenuBills.Add(Bill);
            Job = new Job { bill = Bill };
        }
        internal Thing Stock(ThingDef def, int count)
        { var item = new Thing { def = def, stackCount = count }; Network.Contents.TryAdd(item, false); return item; }
        internal Thing ShelfStock(ThingDef def, int count, Thing stock = null)
        {
            var shelf = new Building_StorageSupplyShelf { Map = Map, Faction = Faction.OfPlayer,
                Network = Network, Position = new IntVec3 { x = Map.haulDestinationManager.Shelves.Count + 1 } };
            Map.haulDestinationManager.Shelves.Add(shelf);
            Map.GetComponent<MapComponent_StorageNetworks>().SupplyShelves.Add(shelf);
            var item = stock ?? new Thing();
            item.def = def; item.stackCount = count; item.Map = Map; item.Position = shelf.Position;
            shelf.Slots.HeldThings.Add(item); Map.Dropped.Add(item); return item;
        }
        internal int Total(ThingDef def) => Network.Contents.Where(t => t.def == def).Sum(t => t.stackCount);
    }
    private static void ReadOnlyPlanning()
    {
        var f = new Fixture(); var source = f.Stock(steel, 8);
        var inside = new Thing { def = steel, stackCount = 4 }; f.Table.innerContainer.TryAdd(inside);
        f.Network.Reserved = 2;
        Check(CosmicGestationMaterials.TryPlan(f.Table, f.Bill, f.Pawn, out var plan), "Combine held materials with available network stock.");
        Check(plan[inside] == 4 && plan[source] == 6, "Only plan the remaining quantity.");
        Check(f.Total(steel) == 8 && source.holdingOwner == f.Network.Contents && f.Table.ActiveBill == null,
            "Preview does not move materials or bind a bill.");
        f.Network.Reserved = 3;
        Check(!CosmicGestationMaterials.TryPlan(f.Table, f.Bill, f.Pawn, out _), "Respect stock reserved by other consumers.");
    }
    private static void DeliveryAndResume()
    {
        foreach (int count in new[] { 10, 50 })
        {
            var f = new Fixture(); f.Stock(steel, count);
            Check(CosmicGestationMaterials.Prepare(f.Table, f.Job, f.Pawn), "Prepare a network-only batch.");
            Check(f.Total(steel) == count - 10 && f.Table.innerContainer.Sum(t => t.stackCount) == 10, "Take exactly one batch.");
            Check(f.Table.ActiveBill == f.Bill && f.Bill.BoundPawn == f.Pawn, "Bind the native active bill before returning.");
            Check(f.Job.targetQueueB.Count == 1 && f.Job.countQueue.Single() == 10,
                "Whole-stack withdrawal must not duplicate the job target.");
            Check(CosmicGestationMaterials.Prepare(f.Table, f.Job, f.Pawn), "Resume materials already inside the gestator.");
            Check(f.Total(steel) == count - 10 && f.Job.targetQueueB.Count == 1, "Resuming never withdraws twice.");
        }
        var partial = new Fixture(); partial.Stock(steel, 10);
        partial.Table.innerContainer.TryAdd(new Thing { def = steel, stackCount = 4 });
        Check(CosmicGestationMaterials.Prepare(partial.Table, partial.Job, partial.Pawn), "Prepare a partly delivered batch.");
        Check(partial.Total(steel) == 4 && partial.Job.countQueue.Sum() == 10, "Queue both existing and new ingredients exactly once.");
    }
    private static void ShelfMaterials()
    {
        var prefer = new Fixture(); prefer.Stock(steel, 12); var untouched = prefer.ShelfStock(steel, 20);
        Check(CosmicGestationMaterials.Prepare(prefer.Table, prefer.Job, prefer.Pawn), "Network alone satisfies a batch.");
        Check(untouched.Spawned && untouched.stackCount == 20 && prefer.Total(steel) == 2,
            "Do not take everyday shelf supplies when storage is sufficient.");
        var f = new Fixture(); f.Stock(steel, 6); var shelf = f.ShelfStock(steel, 8);
        Check(CosmicGestationMaterials.TryPlan(f.Table, f.Bill, f.Pawn, out var plan) && plan[shelf] == 4,
            "Connected shelves supplement the exact network deficit.");
        Check(shelf.stackCount == 8 && f.Table.ActiveBill == null, "Shelf preview is read-only.");
        Check(CosmicGestationMaterials.Prepare(f.Table, f.Job, f.Pawn), "Shared transaction stages storage and shelf materials together.");
        Check(f.Total(steel) == 0 && shelf.stackCount == 4 && f.Job.countQueue.Sum() == 10,
            "Shelf extraction preserves total quantity and real job targets.");
        Check(CosmicGestationMaterials.Prepare(f.Table, f.Job, f.Pawn) && shelf.stackCount == 4,
            "Resuming does not withdraw shelf materials twice.");
        foreach (string restriction in new[] { "reserved", "physical", "forbidden", "shelfForbidden", "burning", "fire", "disconnected", "faction" })
        {
            var blocked = new Fixture(); var item = blocked.ShelfStock(steel, 10);
            var source = blocked.Map.haulDestinationManager.Shelves.Single();
            if (restriction == "reserved") blocked.Map.reservationManager.Reserved.Add(item);
            if (restriction == "physical") blocked.Map.physicalInteractionReservationManager.Reserved.Add(item);
            if (restriction == "forbidden") item.Forbidden = true;
            if (restriction == "shelfForbidden") source.Forbidden = true;
            if (restriction == "burning") source.Burning = true;
            if (restriction == "fire") blocked.Map.Fire = true;
            if (restriction == "disconnected") source.Network = new StorageNetwork();
            if (restriction == "faction") source.Faction = new Faction();
            Check(!CosmicGestationMaterials.TryPlan(blocked.Table, blocked.Bill, blocked.Pawn, out _),
                "Reject unusable shelf stock: " + restriction);
        }
        var rollback = new Fixture(twoMaterials); var steelShelf = rollback.ShelfStock(steel, 10);
        var componentShelf = rollback.ShelfStock(component, 2); rollback.Table.innerContainer.Capacity = 6;
        Check(!CosmicGestationMaterials.Prepare(rollback.Table, rollback.Job, rollback.Pawn), "Partial shelf delivery aborts the batch.");
        Check(rollback.Table.innerContainer.Count == 0 && rollback.Table.ActiveBill == null &&
            rollback.Total(steel) + rollback.Map.Dropped.Where(t => !t.Destroyed && t.def == steel).Sum(t => t.stackCount) == 10 &&
            componentShelf.Spawned && componentShelf.stackCount == 2,
            "Failed delivery preserves delivered splits and returns undelivered shelf stock.");
    }
    private static void ShortageDetails()
    {
        var f = new Fixture(twoMaterials); f.Stock(steel, 10);
        var missing = new List<string>();
        Check(!CosmicGestationMaterials.TryPlan(f.Table, f.Bill, f.Pawn, out _, missing) &&
            missing.Count == 1 && missing[0] == "Component 0/2", "Report only genuinely insufficient ingredients.");
        f = new Fixture(twoMaterials); f.Stock(steel, 5); f.ShelfStock(steel, 2); f.ShelfStock(component, 1);
        f.Table.innerContainer.TryAdd(new Thing { def = steel, stackCount = 1 }); f.Network.Reserved = 1;
        missing.Clear();
        Check(!CosmicGestationMaterials.TryPlan(f.Table, f.Bill, f.Pawn, out _, missing) &&
            missing.SequenceEqual(new[] { "Steel 7/10", "Component 1/2" }),
            "Count held, withdrawable network and shelf stock while excluding other claims.");
        var blocked = f.Map.haulDestinationManager.Shelves[1].Slots.HeldThings.Single();
        f.Map.reservationManager.Reserved.Add(blocked); missing.Clear();
        CosmicGestationMaterials.TryPlan(f.Table, f.Bill, f.Pawn, out _, missing);
        Check(missing[1] == "Component 0/2", "Unavailable shelf reservations do not inflate displayed stock.");
    }
    private static void MenuFailures()
    {
        var f = new Fixture(twoMaterials); f.Stock(steel, 10); var giver = new WorkGiver_CosmicGestation();
        JobFailReason.Clear();
        Check(giver.JobOnThing(f.Pawn, f.Table, true) == null &&
            JobFailReason.Reason == "Insufficient materials: Component 0/2", "Actual menu hook replaces vanilla's overwritten failure.");
        Check(JobFailReason.CustomJobString == f.Bill.Label && WorkGiver_CosmicGestation.MaterialFailure == null,
            "Keep bill label and clear menu diagnostic context.");
        f.Table.LateFailure = "bandwidth"; JobFailReason.Clear();
        giver.JobOnThing(f.Pawn, f.Table, true);
        Check(JobFailReason.Reason == "bandwidth", "A later native failure must not be replaced with material shortage.");
        f.Table.LateFailure = null; f.ShelfStock(component, 2); JobFailReason.Clear();
        Check(giver.JobOnThing(f.Pawn, f.Table, true) != null && !JobFailReason.HaveReason,
            "Shelf supplies enable the menu without a stale shortage message.");
    }
    private static void Failures()
    {
        var f = new Fixture(twoMaterials); f.Stock(steel, 10); f.Stock(component, 2);
        f.Network.FailWithdrawal = 2;
        Check(!CosmicGestationMaterials.Prepare(f.Table, f.Job, f.Pawn), "Abort if the second withdrawal fails.");
        Check(f.Total(steel) == 10 && f.Total(component) == 2 && f.Table.innerContainer.Count == 0,
            "Return the first withdrawn material on a later failure.");
        Check(f.Table.ActiveBill == null && f.Network.Claims == 0, "Failure leaves no active order or reservation.");
        var partial = new Fixture(); partial.Stock(steel, 10); partial.Table.innerContainer.Capacity = 6;
        Check(!CosmicGestationMaterials.Prepare(partial.Table, partial.Job, partial.Pawn), "Abort a partial container transfer.");
        Check(partial.Total(steel) == 10 && partial.Table.innerContainer.Count == 0 && partial.Network.Claims == 0,
            "Recover both the delivered split and the staging remainder.");
        var held = new Fixture(); held.Stock(steel, 5);
        Check(!CosmicGestationMaterials.Prepare(held.Table, held.Job, held.Pawn) && held.Total(steel) == 5,
            "Insufficient materials are never partly withdrawn.");
        var disconnected = new Fixture(); disconnected.Stock(steel, 10); disconnected.Network.CanWork = false;
        Check(!CosmicGestationMaterials.Prepare(disconnected.Table, disconnected.Job, disconnected.Pawn), "Do not prepare while disconnected.");
    }
    private static void Eligibility()
    {
        var f = new Fixture(); f.Stock(steel, 10); f.Pawn.mechanitor = null;
        Check(!CosmicGestationMaterials.TryPlan(f.Table, f.Bill, f.Pawn, out _), "A non-mechanitor cannot prepare a bill.");
        var corpseDef = new ThingDef(); resurrect.ingredients[0].filter = new ThingFilter(corpseDef);
        var r = new Fixture(resurrect);
        var corpse = new Corpse { def = corpseDef, stackCount = 10,
            InnerPawn = new Pawn { Faction = new Faction() } };
        r.Network.Contents.TryAdd(corpse);
        Check(!CosmicGestationMaterials.TryPlan(r.Table, r.Bill, r.Pawn, out _), "Reject another faction's resurrection subject.");
        corpse.InnerPawn.Faction = Faction.OfPlayer; r.Pawn.mechanitor.CanResurrect = false;
        Check(!CosmicGestationMaterials.TryPlan(r.Table, r.Bill, r.Pawn, out _), "Check resurrection bandwidth for stored corpses.");
        r.Pawn.mechanitor.CanResurrect = true;
        Check(CosmicGestationMaterials.TryPlan(r.Table, r.Bill, r.Pawn, out _), "A valid stored resurrection subject is usable.");
        var onShelf = new Fixture(resurrect);
        var shelfCorpse = new Corpse { InnerPawn = new Pawn { Faction = Faction.OfPlayer } };
        onShelf.ShelfStock(corpseDef, 10, shelfCorpse);
        Check(((Bill_ResurrectMech)onShelf.Bill).AllAvailableResurrectionCorpses(onShelf.Pawn).Contains(shelfCorpse),
            "Resurrection eligibility sees corpse candidates from connected shelves.");
        Check(CosmicGestationMaterials.Prepare(onShelf.Table, onShelf.Job, onShelf.Pawn) &&
            onShelf.Table.innerContainer.Contains(shelfCorpse) && shelfCorpse.InnerPawn.Faction == Faction.OfPlayer,
            "Shared extraction preserves the real corpse and its inner pawn.");
    }
    private static void Refunds()
    {
        var f = new Fixture(); f.Stock(steel, 10);
        CosmicGestationMaterials.Prepare(f.Table, f.Job, f.Pawn);
        f.Table.EjectContents();
        Check(f.Total(steel) == 10 && f.Table.innerContainer.Count == 0, "Cancellation before arrival returns prepared ingredients.");
        var full = new Fixture(); full.Stock(steel, 10);
        CosmicGestationMaterials.Prepare(full.Table, full.Job, full.Pawn); full.Network.AcceptReturns = false;
        full.Table.EjectContents();
        Check(full.Map.Dropped.Sum(t => t.stackCount) == 10 && full.Table.innerContainer.Count == 0,
            "Network rejection releases real materials through recovery.");
        var completed = new Fixture(); var pawn = new Pawn(); completed.Bill.State = FormingState.Formed;
        completed.Table.ActiveBill = completed.Bill; completed.Table.innerContainer.TryAdd(pawn);
        completed.Table.EjectContents();
        Check(completed.Network.Contents.Count == 0 && pawn.Destroyed, "Finished Pawns follow native ejection, never ingredient refunds.");
    }
}

namespace Verse
{
    public sealed class StaticConstructorOnStartupAttribute : Attribute { }
    public static class ModsConfig { public static bool BiotechActive = true; }
    public static class DefDatabase<T> where T : ThingDef
    {
        public static List<T> AllDefsListForReading = new List<T>();
        public static T GetNamedSilentFail(string name) => AllDefsListForReading.FirstOrDefault(d => d.defName == name);
    }
    public class ThingDef
    {
        public string defName; public Type thingClass; public bool IsResearchFinished = true;
        public string label => defName;
        public List<RecipeDef> AllRecipes = new List<RecipeDef>(); public RaceProperties race; public int stackLimit = 100;
    }
    public class RaceProperties { public bool IsMechanoid; }
    public class RecipeDef
    {
        public string label = "recipe"; public bool IsSurgery, UsesUnfinishedThing, mechResurrection;
        public bool mechanitorOnlyRecipe = true, AvailableNow = true, allowMixingIngredients;
        public bool interruptIfIngredientIsRotting, ignoreIngredientCountTakeEntireStacks;
        public int gestationCycles = 1, formingTicks = 120000;
        public WorkTypeDef requiredGiverWorkType; public RecipeWorker Worker = new RecipeWorker();
        public IngredientValueGetter IngredientValueGetter = new IngredientValueGetter();
        public List<IngredientCount> ingredients = new List<IngredientCount>();
        public List<ThingDefCountClass> products = new List<ThingDefCountClass>();
        public ThingFilter fixedIngredientFilter = new ThingFilter();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public bool AvailableOnNow(Thing thing) => true;
    }
    public class RecipeWorker { }
    public class IngredientValueGetter { public float ValuePerUnitOf(ThingDef def) => 1; }
    public class IngredientCount
    {
        public ThingFilter filter; public int Count; public bool IsFixedIngredient => true;
        public ThingDef FixedIngredient => filter.AnyAllowedDef;
        public string Summary => Count + "x " + filter.Summary;
        public float GetBaseCount() => Count;
        public int CountRequiredOfFor(ThingDef def, RecipeDef recipe, Bill bill) => Count;
    }
    public class ThingDefCountClass { public ThingDef thingDef; public int count; }
    public class ThingFilter
    {
        private ThingDef allowed; public ThingFilter(ThingDef def = null) { allowed = def; }
        public ThingDef AnyAllowedDef => allowed;
        public string Summary => allowed?.label ?? "any";
        public bool Allows(Thing item) => Allows(item.def);
        public bool Allows(ThingDef def) => allowed == null || allowed == def;
    }
    public class Faction { public static Faction OfPlayer = new Faction(); }
    public enum RotStage { Fresh, Rotting }
    public enum DestroyMode { Vanish }
    public struct IntVec3
    {
        public int x;
        public static IntVec3 Invalid => new IntVec3 { x = -999 };
        public static bool operator ==(IntVec3 a, IntVec3 b) => a.x == b.x;
        public static bool operator !=(IntVec3 a, IntVec3 b) => a.x != b.x;
        public override bool Equals(object other) => other is IntVec3 cell && cell == this;
        public override int GetHashCode() => x;
        public bool ContainsStaticFire(Map map) => map.Fire;
    }
    public static class Translation
    {
        public static string Translate(this string key, params object[] args) => string.Format(
            key == "MS_Gestation_MaterialCount" ? "{0} {1}/{2}" :
            key == "MS_Gestation_MissingMaterials" ? "Insufficient materials: {0}" :
            key == "MissingMaterials" ? "Needs {0}" : key, args);
        public static string ToCommaList(this IEnumerable<string> values) => string.Join(", ", values);
    }
    public struct ThingCount
    {
        public Thing Thing; public int Count;
        public ThingCount(Thing item, int count) { Thing = item; Count = count; }
    }
    public enum ThingPlaceMode { Direct }
    public class Thing
    {
        public ThingDef def = new ThingDef(); public int stackCount = 1;
        public bool Destroyed, Spawned = true, IntervalDue = true; public ThingOwner holdingOwner;
        public Faction Faction; public Map Map;
        public IntVec3 Position; public bool Forbidden, Burning;
        public bool IsForbidden(Pawn pawn) => Forbidden;
        public bool IsForbidden(Faction faction) => Forbidden;
        public bool IsBurning() => Burning;
        public void Destroy(DestroyMode mode) { holdingOwner?.Remove(this); Destroyed = true; }
        public Thing SplitOff(int count)
        {
            if (holdingOwner != null) return holdingOwner.Take(this, count);
            if (count == stackCount) { Map?.Dropped.Remove(this); Spawned = false; return this; }
            stackCount -= count; return new Thing { def = def, stackCount = count, Spawned = false };
        }
        public RotStage GetRotStage() => RotStage.Fresh;
        public bool CanStackWith(Thing item) => item.def == def;
        public void TryAbsorbStack(Thing other, bool limit)
        { stackCount += other.stackCount; other.stackCount = 0; other.Destroyed = true; }
    }
    public class Pawn : Thing
    {
        public MechanitorTracker mechanitor;
        public bool Reserve(Thing thing, Verse.AI.Job job, int max, int count, object layer, bool error) => true;
        public bool ReserveSittableOrSpot(IntVec3 cell, Verse.AI.Job job, bool error) => true;
    }
    public static class Gen
    { public static bool IsHashIntervalTick(this Thing thing, int interval) => thing.IntervalDue; }
    public class MechanitorTracker
    { public bool CanResurrect = true; public bool HasBandwidthToResurrect(Corpse corpse) => CanResurrect; }
    public class Corpse : Thing { public Pawn InnerPawn; }
    public class ThingOwner : IEnumerable<Thing>
    {
        private List<Thing> items = new List<Thing>(); public object Owner; public int Capacity = int.MaxValue;
        public ThingOwner(object owner) { Owner = owner; }
        public int Count => items.Count;
        public bool Contains(Thing item) => items.Contains(item);
        public int GetCountCanAccept(Thing item, bool merge) => Math.Max(0, Capacity - items.Sum(t => t.stackCount));
        public bool TryAdd(Thing item, bool merge = true)
        {
            if (GetCountCanAccept(item, merge) < item.stackCount) return false;
            item.holdingOwner?.Remove(item); items.Add(item); item.holdingOwner = this; item.Spawned = false; return true;
        }
        public void Remove(Thing item) { items.Remove(item); item.holdingOwner = null; }
        public bool TryDrop(Thing item, IntVec3 cell, Map map, ThingPlaceMode mode, out Thing dropped, bool playDropSound)
        { Remove(item); item.Spawned = true; item.Map = map; item.Position = cell; map.Dropped.Add(item); dropped = item; return true; }
        public Thing Take(Thing item, int count)
        {
            if (!Contains(item)) return null;
            if (count == item.stackCount) { Remove(item); return item; }
            item.stackCount -= count; return new Thing { def = item.def, stackCount = count, Spawned = false };
        }
        public IEnumerator<Thing> GetEnumerator() => items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public class Map
    {
        public List<Thing> Dropped = new List<Thing>(); private MapComponent_StorageNetworks manager;
        public bool Fire;
        public Reservations reservationManager = new Reservations(), physicalInteractionReservationManager = new Reservations();
        public HaulDestinations haulDestinationManager = new HaulDestinations();
        public T GetComponent<T>() where T : class => (manager ?? (manager = new MapComponent_StorageNetworks(this))) as T;
    }
    public class Reservations
    {
        public HashSet<Thing> Reserved = new HashSet<Thing>();
        public bool IsReservedByAnyoneOf(Thing item, Faction faction) => Reserved.Contains(item);
        public bool IsReserved(Thing item) => Reserved.Contains(item);
    }
    public class HaulDestinations
    {
        public List<Building_StorageSupplyShelf> Shelves = new List<Building_StorageSupplyShelf>();
        public object SlotGroupParentAt(IntVec3 cell) => Shelves.FirstOrDefault(s => s.Position == cell);
    }
    public static class Log { public static void Error(string text) { throw new Exception(text); } }
}
namespace Verse.AI
{
    public static class JobFailReason
    {
        public static string Reason, CustomJobString;
        public static bool HaveReason => Reason != null;
        public static void Is(string reason, string customJobString = null) { Reason = reason; CustomJobString = customJobString; }
        public static void Clear() { Reason = CustomJobString = null; }
    }
    public struct LocalTargetInfo
    { public Thing Thing; public static implicit operator LocalTargetInfo(Thing t) => new LocalTargetInfo { Thing = t }; }
    public class Job { public Bill bill; public LocalTargetInfo targetA; public List<LocalTargetInfo> targetQueueB; public List<int> countQueue; }
    public class JobDriver_DoBill
    {
        public Job job; public Pawn pawn;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public bool TryMakePreToilReservations(bool errorOnFailed) => true;
    }
}
namespace RimWorld
{
    public class WorkTypeDef { }
    public static class WorkTypeDefOf { public static WorkTypeDef Smithing = new WorkTypeDef(); }
    public enum FormingState { Gathering, Preparing, Forming, Formed }
    public class BillStack { public Building_MechGestator billGiver; }
    public class Bill
    {
        public RecipeDef recipe; public ThingFilter ingredientFilter = new ThingFilter(); public bool suspended, DeletedOrDereferenced;
        public BillStack billStack;
        public string Label => recipe.label;
        public bool IsFixedOrAllowedIngredient(Thing t) => IsFixedOrAllowedIngredient(t.def);
        public bool IsFixedOrAllowedIngredient(ThingDef def) => recipe.ingredients.Any(i => i.filter.Allows(def));
    }
    public class Bill_Mech : Bill
    {
        public FormingState State; public Pawn BoundPawn;
        public bool PawnAllowedToStartAnew(Pawn pawn) => BoundPawn == null || BoundPawn == pawn;
        public void Notify_DoBillStarted(Pawn pawn) { BoundPawn = pawn; billStack.billGiver.ActiveBill = this; }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void BillTick() { }
    }
    public class Bill_ProductionMech : Bill_Mech { }
    public class Bill_ResurrectMech : Bill_Mech
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public IEnumerable<Corpse> AllAvailableResurrectionCorpses(Pawn pawn) => new Corpse[0];
    }
    // Controlled native boundary: native assignment of JobFailReason occurs after
    // the patched ingredient finder, as verified in both supported game versions.
    public class WorkGiver_DoBill
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool TryFindBestBillIngredients(Bill bill, Pawn pawn, Thing billGiver,
            List<ThingCount> chosen, List<IngredientCount> missingIngredients) => false;
        public virtual Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            var table = (Building_MechGestator)thing;
            foreach (var bill in table.MenuBills)
            {
                var missing = new List<IngredientCount>();
                if (TryFindBestBillIngredients(bill, pawn, thing, new List<ThingCount>(), missing)) return new Job { bill = bill };
                JobFailReason.Is("MissingMaterials".Translate(missing.Select(i => i.Summary).ToCommaList()), bill.Label);
            }
            if (table.LateFailure != null) JobFailReason.Is(table.LateFailure);
            return null;
        }
    }
    public static class MechanitorUtility { public static bool IsMechanitor(Pawn pawn) => pawn?.mechanitor != null; }
    public class Building_WorkTable : Thing
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public bool UsableForBillsAfterFueling() => true;
    }
    public class Building_MechGestator : Building_WorkTable
    {
        public List<Bill> MenuBills = new List<Bill>(); public string LateFailure;
        public Bill_Mech ActiveBill; public Bill_Mech ActiveMechBill => ActiveBill;
        public ThingOwner innerContainer; public CompStorageNode node; public IntVec3 InteractionCell;
        public CompPowerTrader Power;
        public int PowerUpdates, CycleBranches;
        public bool PoweredOn
        { [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] get => Power.PowerOn; }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        protected virtual void Tick()
        {
            if (this.IsHashIntervalTick(250)) { Power.PowerOutput = 50; PowerUpdates++; }
            if (PoweredOn) CycleBranches++;
        }
        public void TickForTest() { Tick(); }
        public Building_MechGestator() { innerContainer = new ThingOwner(this); }
        public T GetComp<T>() where T : class => node as T;
        public virtual bool CanWork() => true;
        public virtual void SetFaction(Faction faction, Pawn recruiter = null) { Faction = faction; }
        public virtual void EjectContents()
        {
            foreach (var item in innerContainer.ToList())
            { innerContainer.Remove(item); if (item is Pawn) item.Destroyed = true; else Map.Dropped.Add(item); }
        }
    }
}
namespace MagicStorage
{
    public class CompStorageNode { public StorageNetwork Network; }
    public class Building_StorageUnit : Thing { public Inventory Inventory = new Inventory(); public StorageNetwork Network; }
    public class Building_StorageCore : Thing { public StorageNetwork Network; }
    public class Building_StorageSupplyShelf : Thing
    {
        public StorageNetwork Network; public SlotGroup Slots = new SlotGroup();
        public SlotGroup GetSlotGroup() => Slots;
    }
    public class SlotGroup { public List<Thing> HeldThings = new List<Thing>(); }
    public class Inventory { public void RefreshStack(Thing item) { } }
    public class StorageNetwork
    {
        public static string StatusLabel(StorageNetwork network) => "network unavailable";
        public Building_StorageCore Core;
        public bool CanWork = true, AcceptReturns = true; public int Reserved, FailWithdrawal;
        public ThingOwner Contents = new ThingOwner(new Building_StorageUnit());
        private Dictionary<object, int> claims = new Dictionary<object, int>(); private int withdrawal;
        public int Claims => claims.Count;
        internal void GetMatchingStacks(Predicate<ThingDef> filter, List<Thing> items)
        { items.AddRange(Contents.Where(t => filter(t.def))); }
        internal int AvailableToWithdraw(Thing item) => Contents.Contains(item) ? Math.Max(0, item.stackCount - Reserved) : 0;
        internal int ReserveOutgoing(object owner, Thing item, int count, Thing endpoint)
        { if (AvailableToWithdraw(item) < count) return 0; claims[owner] = count; return count; }
        internal void ReleaseOutgoing(object owner) { claims.Remove(owner); }
        internal StorageTransferResult TryWithdrawReserved(object owner, Thing item, int count, ThingOwner dest, out Thing received)
        {
            received = null; claims.Remove(owner);
            if (++withdrawal == FailWithdrawal) return StorageTransferResult.Failed(count, StorageFailure.Reserved);
            int moved = StorageTransfer.Move(Contents, dest, item, count, null, default(IntVec3), out received, false);
            return new StorageTransferResult(count, moved, StorageFailure.None);
        }
        public StorageTransferResult TryStore(ThingOwner source, Thing item, int count)
        {
            int moved = AcceptReturns ? StorageTransfer.Move(source, Contents, item, count, null, default(IntVec3), false) : 0;
            return new StorageTransferResult(count, moved, StorageFailure.None);
        }
    }
    public class StorageRecoveryBatch { public ThingOwner Contents; public StorageRecoveryBatch() { Contents = new ThingOwner(this); } }
    public class MapComponent_StorageNetworks
    {
        public List<Building_StorageSupplyShelf> SupplyShelves = new List<Building_StorageSupplyShelf>();
        private Map map; public MapComponent_StorageNetworks(Map value) { map = value; }
        public void EnsureCurrent() { }
        public void NotifyFactionChanged(Thing thing) { }
        public StorageRecoveryBatch CreateRecovery(IntVec3 cell) => new StorageRecoveryBatch();
        public void ReleaseRecovery(StorageRecoveryBatch batch)
        { foreach (var item in batch.Contents.ToList()) { batch.Contents.Remove(item); map.Dropped.Add(item); } }
        public void PreserveLooseThing(Thing item, IntVec3 cell) { map.Dropped.Add(item); }
    }
}
