// Controlled engine boundary. Tests link the actual supply, transaction and
// transfer code; native pathing, the ingredient solver and Harmony are not run.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;

namespace HarmonyLib
{
    public static class Priority { public const int Last = 0; }
    public class HarmonyMethod { public int priority; public HarmonyMethod(Type type, string name) { } }
    public class Harmony
    {
        public Harmony(string id) { }
        public void Patch(MethodInfo target, HarmonyMethod prefix = null, HarmonyMethod postfix = null,
            HarmonyMethod finalizer = null) { if (target == null) throw new Exception("Missing fixture patch target"); }
    }
    public static class AccessTools
    {
        public static Type TypeByName(string name) => typeof(AccessTools).Assembly.GetType(name);
        public static FieldInfo Field(Type type, string name) => type.GetField(name);
        public static MethodInfo DeclaredMethod(Type type, string name, Type[] args) => type.GetMethod(name, args);
        public static MethodInfo Method(Type type, string name) => type.GetMethod(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
    }
}
namespace Verse
{
    public sealed class StaticConstructorOnStartupAttribute : Attribute { }
    public interface IExposable { void ExposeData(); }
    public interface IThingHolder { }
    public enum ThingCategory { Item, Building }
    public enum DestroyMode { Vanish }
    public enum ThingPlaceMode { Direct }
    public enum PathEndMode { Touch }
    public enum Danger { Deadly }
    public enum LookMode { Deep }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public class Faction { public static readonly Faction OfPlayer = new Faction(); }
    public struct IntVec3 : IEquatable<IntVec3>
    {
        public int x, z;
        public IntVec3(int x, int z) { this.x = x; this.z = z; }
        public static readonly IntVec3 Invalid = new IntVec3(-1000, -1000);
        public int DistanceToSquared(IntVec3 other) => (x-other.x)*(x-other.x)+(z-other.z)*(z-other.z);
        public bool InBounds(Map map) => x >= 0 && z >= 0;
        public bool Fogged(Map map) => map.Fog.Contains(this);
        public bool InHorDistOf(IntVec3 other, float radius) => (long)(x-other.x)*(x-other.x) + (long)(z-other.z)*(z-other.z) <= radius*radius;
        public int GetItemCount(Map map) { var cell = this; return map.Ground.Count(t => t.Spawned && t.Position == cell); }
        public List<Thing> GetThingList(Map map) { var cell = this; return map.Ground.Where(t => t.Spawned && t.Position == cell).ToList(); }
        public bool Equals(IntVec3 other) => x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is IntVec3 cell && Equals(cell);
        public override int GetHashCode() => x * 397 ^ z;
        public static bool operator ==(IntVec3 a, IntVec3 b) => a.Equals(b);
        public static bool operator !=(IntVec3 a, IntVec3 b) => !a.Equals(b);
    }
    public struct LocalTargetInfo
    {
        public Thing Thing;
        public IntVec3 Cell;
        public static implicit operator LocalTargetInfo(Thing item) => new LocalTargetInfo { Thing = item };
        public static implicit operator LocalTargetInfo(IntVec3 cell) => new LocalTargetInfo { Cell = cell };
    }
    public static class DefDatabase<T> where T : class
    {
        public static Dictionary<string,T> Defs = new Dictionary<string,T>();
        public static T GetNamedSilentFail(string name) => Defs.TryGetValue(name,out var value) ? value : null;
    }
    public static class MoteMaker
    {
        public static List<Tuple<ThingDef, Thing, Thing>> Beams = new List<Tuple<ThingDef, Thing, Thing>>();
        public static void MakeInteractionOverlay(ThingDef def, Thing source, Thing target) =>
            Beams.Add(Tuple.Create(def,source,target));
    }
    public class ModContentPack { public bool IsOfficialMod = true; public string Name = "Test mod"; }
    public class ThingDef { public float BaseMarketValue; public Type thingClass; public bool IsMechGestator => typeof(RimWorld.Building_MechGestator).IsAssignableFrom(thingClass); public ModContentPack modContentPack = new ModContentPack(); public bool IsMedicine; public float Potency; public string defName; public int stackLimit = 75; public ThingCategory category = ThingCategory.Item; }
    public class Thing
    {
        public IntVec3 InteractionCell => Position;
        public IntVec3 PositionHeld => Position;
        public float GetStatValue(object stat) => def.Potency;
        public ThingDef def = new ThingDef();
        public int thingIDNumber;
        public int stackCount = 1;
        public string variant;
        public bool Spawned, Destroyed, Forbidden, Burning;
        public ThingOwner holdingOwner;
        public Map Map;
        public IntVec3 Position;
        public Faction Faction = Faction.OfPlayer;
        public bool IsForbidden(Pawn pawn) => Forbidden;
        public bool IsForbidden(Faction faction) => Forbidden;
        public bool IsBurning() => Burning;
        public bool CanStackWith(Thing other) => !Destroyed && !other.Destroyed && other.def == def && other.variant == variant;
        public void DeSpawn() { Map?.Ground.Remove(this); Spawned = false; }
        public void Destroy(DestroyMode mode = DestroyMode.Vanish) { DeSpawn(); holdingOwner?.Remove(this); Destroyed = true; }
        public Thing SplitOff(int count)
        {
            if (count >= stackCount) { holdingOwner?.Remove(this); if (Spawned) DeSpawn(); return this; }
            stackCount -= count;
            return new Thing { def = def, stackCount = count, variant = variant };
        }
        public bool TryAbsorbStack(Thing other, bool respectLimit)
        {
            if (other == this || other.def != def || other.variant != variant || other.Destroyed) return false;
            int amount = respectLimit ? Math.Min(Math.Max(0, def.stackLimit-stackCount), other.stackCount) : other.stackCount;
            stackCount += amount; other.stackCount -= amount;
            if (other.stackCount != 0) return false;
            other.Destroy(); return true;
        }
    }
    public class Corpse : Thing { public bool Bugged; }
    public class Building : Thing
    {
        public MagicStorage.CompStorageNode Node;
        public T GetComp<T>() where T : class => Node as T;
        public virtual void SetFaction(Faction faction, Pawn recruiter = null) { Faction = faction; }
    }
    public class Pawn : Thing, RimWorld.IBillGiver
    {
        public bool Dead; public float MaxMedicinePotency = 100;
        public Job CurJob;
        public bool CanReserve(LocalTargetInfo target) => target.Thing == null ? !Map.Blocked.Contains(target.Cell) :
            !Map.reservationManager.Claims.TryGetValue(target.Thing, out var claim) || claim.Worker == this;
        public bool CanReach(IntVec3 cell, PathEndMode mode, Danger danger) => !Map.Blocked.Contains(cell);
        public bool Reserve(LocalTargetInfo target, Job job, int maxPawns, int count, object layer, bool error)
        {
            if (!CanReserve(target)) return false;
            if (target.Thing != null) Map.reservationManager.Claims[target.Thing] = new Claim { Worker = this, Job = job };
            return true;
        }
    }
    public class Claim { public Pawn Worker; public Job Job; }
    public class ReservationManager
    {
        public readonly Dictionary<Thing, Claim> Claims = new Dictionary<Thing, Claim>();
        public bool ReservedBy(LocalTargetInfo item, Pawn pawn, Job job) => item.Thing != null && Claims.TryGetValue(item.Thing, out var claim) && claim.Worker == pawn && claim.Job == job;
        public void Release(LocalTargetInfo item, Pawn pawn, Job job) { if (ReservedBy(item,pawn,job)) Claims.Remove(item.Thing); }
        public bool IsReservedByAnyoneOf(LocalTargetInfo item, Faction faction) => item.Thing != null && Claims.ContainsKey(item.Thing);
    }
    public class PhysicalInteractionReservationManager : ReservationManager
    {
        public void Reserve(Pawn pawn, Job job, LocalTargetInfo target) { Claims[target.Thing] = new Claim { Worker = pawn, Job = job }; }
        public void TryRelease(Pawn pawn, Job job, LocalTargetInfo target) => Release(target,pawn,job);
        public bool IsReserved(LocalTargetInfo target) => target.Thing != null && Claims.ContainsKey(target.Thing);
    }
    public class ThingOwner : IEnumerable<Thing>
    {
        private readonly List<Thing> items = new List<Thing>();
        public IThingHolder Owner;
        public Action<Thing> AfterAdd;
        public ThingOwner(IThingHolder owner) { Owner = owner; }
        public int Count => items.Count;
        public int GetCountCanAccept(Thing item, bool merge = true) => item.stackCount;
        public bool Contains(Thing item) => items.Contains(item);
        public bool TryAdd(Thing item, bool merge = true)
        {
            if (item.Destroyed || item.holdingOwner != null) return false;
            if (merge) foreach (var other in items.ToArray()) if (other.TryAbsorbStack(item,true)) return true;
            item.holdingOwner = this; items.Add(item); AfterAdd?.Invoke(item); return true;
        }
        public void Remove(Thing item) { items.Remove(item); if (item.holdingOwner == this) item.holdingOwner = null; }
        public Thing Take(Thing item, int count) => Contains(item) ? item.SplitOff(Math.Min(count,item.stackCount)) : null;
        public bool TryDrop(Thing item, IntVec3 cell, Map map, ThingPlaceMode mode, out Thing received, bool playDropSound = false)
        { received = item; if (!Contains(item)) return false; GenSpawn.Spawn(item,cell,map); return true; }
        public IEnumerator<Thing> GetEnumerator() => items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public class ThingCountClass : IExposable
    {
        public Thing thing; public int Count;
        public ThingCountClass() { }
        public void ExposeData() { Scribe_References.Look(ref thing,"thing"); Scribe_Values.Look(ref Count,"count"); }
        public ThingCountClass(Thing item, int count) { thing = item; Count = count; }
    }
    public class FloatMenuOption { }
    public enum ThingRequestGroup { HaulableEver }
    public class ListerThings
    {
        private readonly Map map;
        public ListerThings(Map map) { this.map = map; }
        public List<Thing> ThingsInGroup(ThingRequestGroup group) => map.Ground;
    }
    public class Map : IThingHolder
    {
        public ListerThings listerThings => new ListerThings(this);
        public readonly List<Thing> Ground = new List<Thing>();
        public readonly HashSet<IntVec3> Blocked = new HashSet<IntVec3>(), Fog = new HashSet<IntVec3>();
        public MagicStorage.MapComponent_StorageNetworks Manager;
        public ReservationManager reservationManager = new ReservationManager();
        public PhysicalInteractionReservationManager physicalInteractionReservationManager = new PhysicalInteractionReservationManager();
        public HaulDestinationManager haulDestinationManager = new HaulDestinationManager();
        public ResourceCounter resourceCounter = new ResourceCounter();
        public T GetComponent<T>() where T : class => Manager as T;
    }
    public class HaulDestinationManager
    {
        public HashSet<IntVec3> StorageCells = new HashSet<IntVec3>();
        public object SlotGroupParentAt(IntVec3 cell) => StorageCells.Contains(cell) ? this : null;
    }
    public class ResourceCounter { public int Updates; public void UpdateResourceCounts() { Updates++; } }
    public static class GenSpawn
    {
        public static Action<Thing> AfterSpawn;
        public static bool CanSpawnAt(ThingDef def, IntVec3 cell, Map map) => cell.InBounds(map) && !map.Blocked.Contains(cell);
        public static bool SpawningWipes(ThingDef a, ThingDef b) => false;
        public static Thing Spawn(Thing item, IntVec3 cell, Map map)
        {
            item.holdingOwner?.Remove(item); item.Map = map; item.Position = cell; item.Spawned = true;
            item.stackCount = Math.Min(item.stackCount,item.def.stackLimit); map.Ground.Add(item);
            AfterSpawn?.Invoke(item); return item;
        }
    }
    public static class GenTicks { public static int TicksGame = 100; }
    public static class Log
    {
        public static void WarningOnce(string message, int key) { }
        public static readonly List<string> Warnings = new List<string>();
        public static void Warning(string message) { Warnings.Add(message); }
        public static void Error(string message) { }
        public static void ErrorOnce(string message, int key) { }
    }
    public static class Scribe
    {
        public static LoadSaveMode mode;
        public static Dictionary<string, object> Data;
        public static Dictionary<string, object> Save(IExposable obj)
        {
            var previous = Data; Data = new Dictionary<string, object>(); var result = Data;
            obj.ExposeData(); Data = previous; return result;
        }
        public static void Load(IExposable obj, Dictionary<string, object> data)
        { var previous = Data; Data = data; obj.ExposeData(); Data = previous; }
    }
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string key, T fallback = default(T))
        { if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = value; else if (Scribe.mode == LoadSaveMode.LoadingVars) value = Scribe.Data.TryGetValue(key,out var saved) ? (T)saved : fallback; }
    }
    public static class Scribe_References { public static void Look<T>(ref T value,string key) => Scribe_Values.Look(ref value,key); }
    public static class Scribe_Defs { public static void Look<T>(ref T value,string key) => Scribe_Values.Look(ref value,key); }
    public static class Scribe_Deep
    {
        public static void Look<T>(ref T value,string key) where T : class, IExposable
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = value == null ? null : Scribe.Save(value);
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (!Scribe.Data.TryGetValue(key,out var saved) || saved == null) { value = null; return; }
                value = (T)Activator.CreateInstance(typeof(T),true); Scribe.Load(value,(Dictionary<string,object>)saved);
            }
        }
    }
    public static class Scribe_Collections
    {
        public static void Look<T>(ref List<T> values,string key,LookMode mode) where T : class,IExposable
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = values?.Select(v => v == null ? null : Scribe.Save(v)).ToList();
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                values = new List<T>();
                if (!Scribe.Data.TryGetValue(key,out var saved) || saved == null) return;
                foreach (var node in (List<Dictionary<string,object>>)saved)
                { var entry = (T)Activator.CreateInstance(typeof(T),true); Scribe.Load(entry,node); values.Add(entry); }
            }
        }
    }
}
namespace RimWorld
{
    public interface IBillGiver { }
    public class Building_WorkTable : Building, IBillGiver
    { public List<IntVec3> Cells = Enumerable.Range(0,20).Select(i => new IntVec3(i+20,20)).ToList(); }
    public class Building_WorkTableAutonomous : Building_WorkTable { }
    public class Building_MechGestator : Building_WorkTableAutonomous, IThingHolder
    {
        public ThingOwner innerContainer;
        public Building_MechGestator() { innerContainer = new ThingOwner(this); def.defName = "MechGestator"; def.thingClass = GetType(); }
    }
    public class Recipe_InstallImplant { }
    public class Recipe_InstallArtificialBodyPart { }
    public class Recipe_InstallNaturalBodyPart { }
    public class Recipe_RemoveImplant { }
    public class Recipe_Surgery { }
    public class Recipe_ExecuteByCut { }
    public class Recipe_RemoveBodyPart { }
    public static class StatDefOf { public static object MedicalPotency; }
    public class MedicalCareCategory
    {
        public float MaxPotency;
        public bool AllowsMedicine(ThingDef def) => def.Potency <= MaxPotency;
    }
    public class RecipeDef {
        public ThingFilter fixedIngredientFilter = new ThingFilter();
        // Vanilla existence-only query; no solver, reservations or withdrawals.
        public IEnumerable<ThingDef> PotentiallyMissingIngredients(Pawn billDoer, Map map)
        {
            foreach (var ingredient in ingredients)
            {
                if (map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Any(t =>
                    (billDoer == null || !t.IsForbidden(billDoer)) && !t.Position.Fogged(map) &&
                    (ingredient.IsFixedIngredient || fixedIngredientFilter.Allows(t)) && ingredient.filter.Allows(t))) continue;
                var representative = ingredient.IsFixedIngredient ? ingredient.filter.AllowedThingDefs.First() :
                    ingredient.filter.AllowedThingDefs.OrderBy(d => d.BaseMarketValue).FirstOrDefault(d => fixedIngredientFilter.Allows(d));
                if (representative != null) yield return representative;
            }
        }
        public bool IsSurgery, allowMixingIngredients; public ModContentPack modContentPack = new ModContentPack(); public string defName; public Type workerClass; public List<IngredientCount> ingredients = new List<IngredientCount> { new IngredientCount() }; }
    public class IngredientCount { public bool IsFixedIngredient; public ThingFilter filter = new ThingFilter(); }
    public class ThingFilter
    {
        public List<ThingDef> AllowedThingDefs = new List<ThingDef>();
        public Predicate<Thing> AllowsThing = t => true;
        public bool Allows(ThingDef def) => AllowedThingDefs.Count == 0 || AllowedThingDefs.Contains(def);
        public bool Allows(Thing item) => Allows(item.def) && AllowsThing(item);
    }
    public class BillStack { public IBillGiver billGiver; public void DoListing() { } }
    public static class HealthCardUtility
    {
        private static bool CanDoRecipeWithMedicineRestriction(IBillGiver giver, RecipeDef recipe) => false;
    }
    public class Bill
    {
        public Thing xenogerm; public BillStack billStack; public RecipeDef recipe = new RecipeDef(); public float ingredientSearchRadius = 999;
        public bool DeletedOrDereferenced, suspended; public int nextTickToSearchForIngredients;
        public Predicate<Thing> Filter = t => true;
        public bool IsFixedOrAllowedIngredient(Thing item) => Filter(item);
        public bool IsFixedOrAllowedIngredient(ThingDef def) => true;
        public bool PawnAllowedToStartAnew(Pawn pawn) => true;
    }
    public class Bill_Production : Bill { }
    public class Bill_Medical : Bill { public List<Thing> uniqueRequiredIngredients; }
    public enum FormingState { Gathering, Forming }
    public class Bill_ProductionMech : Bill_Autonomous { public FormingState State; }
    public class Bill_ResurrectMech : Bill_Autonomous { }
    public class Bill_Autonomous : Bill_Production { }
    public class WorkGiver_DoBill
    {
        public static MedicalCareCategory GetMedicalCareCategory(Pawn patient) => new MedicalCareCategory { MaxPotency = patient.MaxMedicinePotency };
        private static bool TryFindBestBillIngredients() => false;
        private static bool TryFindBestBillIngredientsInSet() => false;
        public static Job TryStartNewDoBillJob() => null;
    }
    public static class JobDefOf { public static readonly JobDef DoBill = new JobDef { driverClass = typeof(JobDriver_DoBill) }; }
}
namespace Verse.AI
{
    public class JobDef { public Type driverClass; }
    public class Job
    {
        public JobDef def = RimWorld.JobDefOf.DoBill;
        public RimWorld.Bill bill;
        public LocalTargetInfo targetA;
        public List<LocalTargetInfo> targetQueueB = new List<LocalTargetInfo>();
        public List<int> countQueue = new List<int>();
        public List<ThingCountClass> placedThings;
        public void Clear() { }
        public Job Clone() => (Job)MemberwiseClone();
        public void ExposeData() { }
    }
    public class JobDriver { public Pawn pawn; public Job job; }
    public class JobDriver_DoBill : JobDriver { public bool TryMakePreToilReservations(bool error) => true; }
    public static class Toils_JobTransforms
    { private static IEnumerable<IntVec3> IngredientPlaceCellsInOrder(Thing table) => table is RimWorld.Building_WorkTable workTable ? workTable.Cells : new List<IntVec3> { table.Position, new IntVec3(table.Position.x + 1, table.Position.z) }; }
    public static class HaulAIUtility
    {
        public static void UpdateJobWithPlacedThings(Job job, Thing item, int count)
        { if (job.placedThings == null) job.placedThings = new List<ThingCountClass>(); job.placedThings.Add(new ThingCountClass(item,count)); }
    }
}
namespace Fortified
{
    public class Building_WorkTableAutonomous : RimWorld.Building_WorkTable, IThingHolder
    {
        public ThingOwner innerContainer;
        public Building_WorkTableAutonomous() { innerContainer = new ThingOwner(this); }
    }
    public class WorkGiver_DoAutonomousBill
    {
        public Job JobOnThing(Pawn pawn, Thing thing, bool forced) => null;
    }
    public class JobDriver_DoAutonomousBill : JobDriver
    {
        public bool TryMakePreToilReservations(bool error) => true;
    }
}
namespace MagicStorage
{
    internal static class MagicStorageMod { internal static readonly MagicStorageSettings Settings = new MagicStorageSettings(); }
    internal sealed class MagicStorageSettings { public bool ShowProductionOutputBeams = true; }
    public sealed class Building_CosmicMechGestator : RimWorld.Building_MechGestator { }
    public class CompStorageNode { public StorageNetwork Network; public Thing parent; }
    public class Inventory { public ThingOwner Contents; public void RefreshStack(Thing item) { } }
    public class Building_StorageUnit : Building, IThingHolder
    {
        public Inventory Inventory; public StorageNetwork Network;
        public Building_StorageUnit() { Inventory = new Inventory { Contents = new ThingOwner(this) }; }
    }
    public class Building_StorageCore : Building { public StorageNetwork Network; }
    public class StorageNetwork
    {
        public List<CompStorageNode> Members = new List<CompStorageNode>();
        public bool CanWork = true, HasProductionOutputPort = true; public Building_StorageCore Core;
        public List<Building_StorageUnit> Units = new List<Building_StorageUnit>();
        public Dictionary<object, Tuple<Thing,int>> Claims = new Dictionary<object, Tuple<Thing,int>>();
        public int AvailableToWithdraw(Thing item) => CanWork ? item.stackCount - Claims.Values.Where(c=>c.Item1==item).Sum(c=>c.Item2) : 0;
        public int ReserveOutgoing(object owner,Thing item,int count,Thing endpoint = null)
        { int amount = Math.Min(count,AvailableToWithdraw(item)); if (amount > 0) Claims[owner] = Tuple.Create(item,amount); return amount; }
        public void ReleaseOutgoing(object owner) => Claims.Remove(owner);
        public StorageTransferResult TryWithdrawReserved(object owner,Thing item,int count,ThingOwner destination,out Thing received)
        {
            received = null;
            if (!Claims.TryGetValue(owner,out var claim) || claim.Item1 != item || claim.Item2 < count) return StorageTransferResult.Failed(count,StorageFailure.Reserved);
            int moved = StorageTransfer.Move(item.holdingOwner,destination,item,count,Core.Map,Core.Position,out received,false);
            Claims.Remove(owner); return new StorageTransferResult(count,moved,StorageFailure.None);
        }
        public void GetMatchingStacks(Predicate<ThingDef> filter,List<Thing> result)
        { foreach (var unit in Units) foreach (var item in unit.Inventory.Contents) if (filter(item.def)) result.Add(item); }
    }
    public class StorageRecoveryBatch { public ThingOwner Contents; public StorageRecoveryBatch(IThingHolder owner) { Contents = new ThingOwner(owner); } }
    public class MapComponent_StorageNetworks : IThingHolder
    {
        public List<StorageNetwork> Networks = new List<StorageNetwork>();
        public List<StorageRecoveryBatch> Recovery = new List<StorageRecoveryBatch>();
        public void EnsureCurrent() { }
        public void NotifyFactionChanged(Thing thing) { }
        public StorageRecoveryBatch CreateRecovery(IntVec3 cell) { var batch = new StorageRecoveryBatch(this); Recovery.Add(batch); return batch; }
        public void ReleaseRecovery(StorageRecoveryBatch batch) { if (batch.Contents.Count == 0) Recovery.Remove(batch); }
        public void PreserveLooseThing(Thing item,IntVec3 cell) { CreateRecovery(cell).Contents.TryAdd(item,false); }
    }
}
