// Controlled placement/reservation boundaries. Tests link the real shelf and restocking service.
using System.Collections.Generic;
using Verse;
namespace Verse
{
    public partial class ThingDef
    {
        public bool PlayerAcquirable = true, specialDisallowed;
        public ThingDef virtualDefParent;
    }
    public partial struct IntVec3
    { public bool ContainsStaticFire(Map map) => map.ShelfFire; }
    public class PhysicalInteractionReservationManager
    {
        public readonly HashSet<Thing> Reserved = new HashSet<Thing>();
        public bool IsReserved(Thing item) => Reserved.Contains(item);
    }
    public partial class Map
    {
        public bool ShelfFire, ShelfBlocked;
        public PhysicalInteractionReservationManager physicalInteractionReservationManager = new PhysicalInteractionReservationManager();
    }
}
namespace RimWorld
{
    public class Command_SelectStorage : Command_Action { public Thing Target; }
    public static class ContainingSelectionUtility
    {
        public static Gizmo CreateSelectStorageGizmo(string label, string description, Thing thingToSelect,
            Thing iconThing = null, bool groupable = true) => new Command_SelectStorage
        {
            Target = thingToSelect, defaultLabel = label, defaultDesc = description,
            action = () => Find.Selector.Select(thingToSelect, false, true)
        };
    }
    public static partial class StoreUtility
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static bool IsGoodStoreCell(IntVec3 c, Map map, Thing t, Pawn carrier, Faction faction) => !map.ShelfBlocked;
    }
}
namespace MagicStorage
{
    public class Dialog_StorageSupplyShelf { public Dialog_StorageSupplyShelf(Building_StorageSupplyShelf shelf) { } }
}
