// Map placement, serialization and native job execution remain engine boundaries.
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using Verse;

namespace RimWorld
{
    public static class ThingCategoryDefOf
    { public static readonly ThingCategoryDef Foods = new ThingCategoryDef(); }
    public interface IStoreSettingsParent { }
    public interface ISlotGroupParent : IHaulDestination, IStoreSettingsParent
    {
        StorageSettings GetStoreSettings();
        SlotGroup GetSlotGroup();
        List<IntVec3> AllSlotCellsList();
    }
    public class ThingFilter
    {
        public ThingFilter(System.Action changed = null) { }
        public static ThingFilter CreateOnlyEverStorableThingFilter() => new ThingFilter();
        public readonly HashSet<ThingDef> Allowed = new HashSet<ThingDef>();
        public IEnumerable<ThingDef> AllowedThingDefs => Allowed;
        public bool Allows(ThingDef def) => Allowed.Contains(def);
        public QualityRange AllowedQualityLevels = QualityRange.All;
        public FloatRange AllowedHitPointsPercents = new FloatRange(0f, 1f);
        public bool Allows(Thing thing) => thing != null && Allows(thing.def) &&
            (!thing.def.useHitPoints || AllowedHitPointsPercents.IncludesEpsilon(
                (float)System.Math.Round((double)thing.HitPoints / thing.MaxHitPoints, 2))) &&
            (!thing.hasQuality || AllowedQualityLevels.Includes(thing.quality));
        public void SetDisallowAll() { Allowed.Clear(); }
        public void SetAllow(ThingDef def, bool allowed) { if (allowed) Allowed.Add(def); else Allowed.Remove(def); }
        public void SetAllow(ThingCategoryDef category, bool allowed)
        { foreach (ThingDef def in category.DescendantThingDefs) SetAllow(def, allowed); }
        public bool IsAlwaysDisallowedDueToSpecialFilters(ThingDef def) => def.specialDisallowed;
        public void CopyAllowancesFrom(ThingFilter filter)
        {
            Allowed.Clear();
            foreach (var def in filter.Allowed) Allowed.Add(def);
            AllowedQualityLevels = filter.AllowedQualityLevels;
            AllowedHitPointsPercents = filter.AllowedHitPointsPercents;
        }
    }
    public class StorageSettings
    {
        public object owner;
        public bool allow = true;
        private StoragePriority priority = StoragePriority.Normal;
        public StoragePriority Priority
        {
            get => priority;
            set
            {
                priority = value;
                // Like vanilla, assigning priority immediately sorts the owning map.
                if (owner is IHaulDestination && owner is Building building && building.Map != null)
                    building.Map.haulDestinationManager.Notify_HaulDestinationChangedPriority();
            }
        }
        public ThingFilter filter = new ThingFilter();
        public StorageSettings(object owner = null) { this.owner = owner; }
        public bool AllowedToAccept(Thing item) => allow;
        private static readonly StorageSettings everStorable = new StorageSettings();
        public static StorageSettings EverStorableFixedSettings() => everStorable;
    }
    public class SlotGroup
    {
        public readonly ISlotGroupParent parent;
        public StorageSettings Settings => parent.GetStoreSettings();
        public SlotGroup(ISlotGroupParent parent) { this.parent = parent; }
        public IEnumerable<Thing> HeldThings
        {
            get
            {
                var building = (Building)parent;
                if (!building.Spawned) throw new System.NullReferenceException("Despawned slot group parent has no map.");
                return building.Map.Ground.Where(t => t.Spawned && t.Map == building.Map &&
                    parent.AllSlotCellsList().Contains(t.Position) && t.def.category == ThingCategory.Item);
            }
        }
        public int HeldThingsCount => HeldThings.Count();
    }
    public class HaulDestinationManager
    {
        public readonly List<IHaulSource> AllHaulSourcesListForReading = new List<IHaulSource>();
        public readonly List<ISlotGroupParent> AllHaulDestinationsListForReading = new List<ISlotGroupParent>();
        public readonly List<SlotGroup> AllGroupsListForReading = new List<SlotGroup>();
        public int Sorts;
        public int MaxSortDepth;
        private int sortDepth;
        public readonly Dictionary<IntVec3, ISlotGroupParent> SlotParents = new Dictionary<IntVec3, ISlotGroupParent>();
        public ISlotGroupParent SlotGroupParentAt(IntVec3 cell) => SlotParents.TryGetValue(cell, out var parent) ? parent : null;
        public void AddHaulDestination(ISlotGroupParent parent)
        {
            AllHaulDestinationsListForReading.Add(parent);
            InsertionSort(AllHaulDestinationsListForReading,
                (a, b) => ((int)b.GetStoreSettings().Priority).CompareTo((int)a.GetStoreSettings().Priority));
            AllGroupsListForReading.Add(parent.GetSlotGroup());
            InsertionSort(AllGroupsListForReading,
                (a, b) => ((int)b.Settings.Priority).CompareTo((int)a.Settings.Priority));
            foreach (IntVec3 cell in parent.AllSlotCellsList()) SlotParents[cell] = parent;
        }
        public void RemoveHaulDestination(ISlotGroupParent parent)
        {
            if (!AllHaulDestinationsListForReading.Remove(parent))
            {
                Log.Error("Removing haul destination that isn't registered " + parent);
                return;
            }
            AllGroupsListForReading.Remove(parent.GetSlotGroup());
            foreach (IntVec3 cell in parent.AllSlotCellsList()) SlotParents.Remove(cell);
        }
        public void Notify_HaulDestinationChangedPriority()
        {
            Sorts++;
            sortDepth++;
            MaxSortDepth = System.Math.Max(MaxSortDepth, sortDepth);
            try
            {
                InsertionSort(AllHaulDestinationsListForReading,
                    (a, b) => ((int)b.GetStoreSettings().Priority).CompareTo((int)a.GetStoreSettings().Priority));
                InsertionSort(AllGroupsListForReading,
                    (a, b) => ((int)b.Settings.Priority).CompareTo((int)a.Settings.Priority));
            }
            finally { sortDepth--; }
        }
        // Preserve vanilla's in-place algorithm: a reentrant comparison can lose entries.
        private static void InsertionSort<T>(IList<T> list, System.Comparison<T> comparison)
        {
            int count = list.Count;
            for (int i = 1; i < count; i++)
            {
                T value = list[i];
                int previous = i - 1;
                while (previous >= 0 && comparison(list[previous], value) > 0)
                {
                    list[previous + 1] = list[previous];
                    previous--;
                }
                list[previous + 1] = value;
            }
        }
    }
    public class ListerHaulables { public void Notify_SlotGroupChanged(SlotGroup group) { } }
    public interface IHaulSource { ThingOwner GetDirectlyHeldThings(); }
}
namespace Verse
{
    public class ThingCategoryDef
    { public readonly List<ThingDef> DescendantThingDefs = new List<ThingDef>(); }
    public enum DestroyMode { Vanish, KillFinalize, Deconstruct }
    public partial class Thing
    {
        public virtual void DeSpawn(DestroyMode mode = DestroyMode.Vanish) { Map?.Ground.Remove(this); Spawned = false; }
        public bool IsForbidden(object faction) => forbidden;
    }
    public partial class Building : Thing
    {
        public new IThingHolder ParentHolder => Map;
        public virtual void ExposeData() { }
        public virtual void Destroy(DestroyMode mode = DestroyMode.Vanish) { base.Destroy(); }
        public virtual IEnumerable<Gizmo> GetGizmos() { yield break; }
        public CompStorageNode node;
        public int MaxItemsInCell => 10;
#if TRANSPORT_SUPPLIER_TESTS
        public new string LabelCap => "receiver";
#else
        public string LabelCap => "receiver";
#endif
        public readonly List<ThingComp> TestComps = new List<ThingComp>();
        public T GetComp<T>() where T : class => node as T ?? TestComps.Find(comp => comp is T) as T;
        public virtual void SpawnSetup(Map map, bool respawningAfterLoad) { Map = map; Spawned = true; }
        public virtual void SetFaction(Faction faction, Pawn recruiter = null) { Faction = faction; }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (this is RimWorld.ISlotGroupParent parent &&
                Map.haulDestinationManager.AllGroupsListForReading.Contains(parent.GetSlotGroup()))
                Map.haulDestinationManager.RemoveHaulDestination(parent);
            Map.GetComponent<MapComponent_StorageNetworks>().Unregister(node);
            base.DeSpawn(mode);
        }
        public virtual string GetInspectString() => "";
        public virtual void TickRare() { }
        protected virtual void Tick() { }
        public void RunTransportTestTick() { Tick(); }
        public virtual void DrawExtraSelectionOverlays() { }
    }
    public partial class Map
    {
        public RimWorld.HaulDestinationManager haulDestinationManager = new RimWorld.HaulDestinationManager();
        public RimWorld.ListerHaulables listerHaulables = new RimWorld.ListerHaulables();
    }
    public partial class ReservationManager
    {
        public readonly HashSet<Thing> Reserved = new HashSet<Thing>();
        public bool IsReservedByAnyoneOf(Thing item, object faction) => Reserved.Contains(item);
    }
}
