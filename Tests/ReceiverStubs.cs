// Map placement, serialization and native job execution remain engine boundaries.
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using Verse;

namespace RimWorld
{
    public interface IStoreSettingsParent { }
    public interface ISlotGroupParent : IHaulDestination, IStoreSettingsParent
    {
        StorageSettings GetStoreSettings();
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
        public bool IsAlwaysDisallowedDueToSpecialFilters(ThingDef def) => def.specialDisallowed;
        public void CopyAllowancesFrom(ThingFilter filter) { foreach (var def in filter.Allowed) Allowed.Add(def); }
    }
    public class StorageSettings
    {
        public object owner;
        public bool allow = true;
        public StoragePriority Priority;
        public ThingFilter filter = new ThingFilter();
        public StorageSettings(object owner = null) { }
        public bool AllowedToAccept(Thing item) => allow;
        public static StorageSettings EverStorableFixedSettings() => new StorageSettings();
    }
    public class SlotGroup
    {
        private readonly ISlotGroupParent parent;
        public SlotGroup(ISlotGroupParent parent) { this.parent = parent; }
        public IEnumerable<Thing> HeldThings
        {
            get
            {
                var building = (Building)parent;
                return building.Map.Ground.Where(t => t.Spawned && t.Map == building.Map &&
                    parent.AllSlotCellsList().Contains(t.Position) && t.def.category == ThingCategory.Item);
            }
        }
        public int HeldThingsCount => HeldThings.Count();
    }
    public class HaulDestinationManager
    {
        public readonly List<IHaulSource> AllHaulSourcesListForReading = new List<IHaulSource>();
        public int Sorts;
        public void Notify_HaulDestinationChangedPriority() { Sorts++; }
    }
    public class ListerHaulables { public void Notify_SlotGroupChanged(SlotGroup group) { } }
    public interface IHaulSource { ThingOwner GetDirectlyHeldThings(); }
}
namespace Verse
{
    public enum DestroyMode { Vanish, KillFinalize, Deconstruct }
    public partial class Thing
    {
        public virtual void DeSpawn(DestroyMode mode = DestroyMode.Vanish) { Map?.Ground.Remove(this); Spawned = false; }
        public bool IsForbidden(object faction) => forbidden;
    }
    public class Building : Thing
    {
        public new IThingHolder ParentHolder => Map;
        public virtual void ExposeData() { }
        public virtual void Destroy(DestroyMode mode = DestroyMode.Vanish) { base.Destroy(); }
        public virtual IEnumerable<Gizmo> GetGizmos() { yield break; }
        public CompStorageNode node;
        public int MaxItemsInCell => 10;
        public string LabelCap => "receiver";
        public T GetComp<T>() where T : class => node as T;
        public virtual void SpawnSetup(Map map, bool respawningAfterLoad) { Map = map; Spawned = true; }
        public virtual void SetFaction(Faction faction, Pawn recruiter = null) { Faction = faction; }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        { Map.GetComponent<MapComponent_StorageNetworks>().Unregister(node); base.DeSpawn(mode); }
        public virtual string GetInspectString() => "";
        public virtual void TickRare() { }
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
