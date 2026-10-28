// Optional framework boundary. Tests run the production reflection adapter and supplier.
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWorld
{
    public static class MassUtility
    {
        public static float GearAndInventoryMass(Pawn pawn) => pawn.VehicleGearMass +
            (float)MagicStorage.StorageTransportExchange.ContainerMass(pawn.inventory.innerContainer);
    }
}
namespace Verse
{
    public partial class Pawn { public float VehicleGearMass; }
}
namespace Vehicles
{
    public sealed class VehicleStatDef { }
    public static class VehicleStatDefOf { public static VehicleStatDef CargoCapacity = new VehicleStatDef(); }
    public class VehiclePawn : Pawn
    {
        public float Capacity = 1000f;
        public int CargoAdded, AcceptLimit = int.MaxValue;
        public bool Reject, ThrowAfterLoad;
        public Action BeforeLoad, AfterLoad;
        public readonly Dictionary<ThingDef, int> PendingCargo = new Dictionary<ThingDef, int>();
        public float GetStatValue(VehicleStatDef stat) => Capacity;
        public int AddOrTransfer(Thing item, int count)
        {
            BeforeLoad?.Invoke();
            if (Reject) return 0;
            count = Math.Min(count, AcceptLimit);
            if (count <= 0) return 0;
            Thing part = item.holdingOwner == null ? item.SplitOff(count) : item.holdingOwner.Take(item, count);
            ThingDef def = part.def;
            if (!inventory.innerContainer.TryAdd(part, true)) throw new Exception("Unexpected fake inventory rejection");
            CargoAdded++;
            if (PendingCargo.ContainsKey(def)) PendingCargo[def] = Math.Max(0, PendingCargo[def] - count);
            AfterLoad?.Invoke();
            if (ThrowAfterLoad) throw new Exception("CargoAdded callback failed after acceptance");
            return count;
        }
    }
}
