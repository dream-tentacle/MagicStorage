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
    public static partial class StoreUtility
    {
        public static bool IsGoodStoreCell(IntVec3 position, Map map, Thing item, Pawn carrier, Faction faction) => !map.ShelfBlocked;
    }
}
namespace MagicStorage
{
    public class Dialog_StorageSupplyShelf { public Dialog_StorageSupplyShelf(Building_StorageSupplyShelf shelf) { } }
}
