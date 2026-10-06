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
    public class ThingFilter { public void SetDisallowAll() { } }
    public class StorageSettings
    {
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
        public int Sorts;
        public void Notify_HaulDestinationChangedPriority() { Sorts++; }
    }
    public class ListerHaulables { public void Notify_SlotGroupChanged(SlotGroup group) { } }
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
        public CompStorageNode node;
        public int MaxItemsInCell => 10;
        public string LabelCap => "receiver";
        public T GetComp<T>() where T : class => node as T;
        public virtual void SpawnSetup(Map map, bool respawningAfterLoad) { Map = map; Spawned = true; }
        public virtual void SetFaction(Faction faction, Pawn recruiter = null) { Faction = faction; }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        { Map.GetComponent<MapComponent_StorageNetworks>().Unregister(node); base.DeSpawn(mode); }
        public virtual string GetInspectString() => "";
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
