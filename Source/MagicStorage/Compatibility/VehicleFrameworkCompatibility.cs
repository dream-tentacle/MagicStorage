using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Resolve the optional framework once; both game builds remain independent of Vehicles.dll.
    internal sealed class VehicleFrameworkCompatibility : IStorageCompatibility
    {
        private static Type vehicleType;
        private static MethodInfo addOrTransfer, getStatValue;
        private static object cargoCapacity;
        private static bool reportedFailure;
        public string PackageId => "SmashPhil.VehicleFramework";

        public void Apply(Harmony harmony)
        {
            Type vehicle = AccessTools.TypeByName("Vehicles.VehiclePawn");
            Type stats = AccessTools.TypeByName("Vehicles.VehicleStatDefOf");
            FieldInfo capacity = stats == null ? null : AccessTools.Field(stats, "CargoCapacity");
            MethodInfo add = vehicle == null ? null : AccessTools.Method(vehicle, "AddOrTransfer", new[] { typeof(Thing), typeof(int) });
            MethodInfo get = vehicle == null || capacity == null ? null :
                AccessTools.Method(vehicle, "GetStatValue", new[] { capacity.FieldType });
            object stat = capacity?.IsStatic == true ? capacity.GetValue(null) : null;
            if (vehicle == null || !typeof(Pawn).IsAssignableFrom(vehicle) || add?.ReturnType != typeof(int) ||
                get?.ReturnType != typeof(float) || stat == null)
                throw new InvalidOperationException("Vehicle Framework cargo API was not found.");
            addOrTransfer = add; getStatValue = get; cargoCapacity = stat;
            vehicleType = vehicle;
        }

        private static bool Valid(Building_TransportSupplier supplier, Pawn vehicle) =>
            vehicleType != null && vehicleType.IsInstanceOfType(vehicle) && supplier.Spawned &&
            supplier.Faction == Faction.OfPlayer && vehicle.Spawned && !vehicle.Destroyed &&
            vehicle.Map == supplier.Map && vehicle.Faction == supplier.Faction && vehicle.inventory != null &&
            vehicle.OccupiedRect().Contains(supplier.FrontCell);

        internal static Pawn FrontVehicle(Building_TransportSupplier supplier)
        {
            if (vehicleType == null || !supplier.Spawned || !supplier.FrontCell.InBounds(supplier.Map)) return null;
            foreach (Thing thing in supplier.FrontCell.GetThingList(supplier.Map))
                if (thing is Pawn vehicle && Valid(supplier, vehicle)) return vehicle;
            return null;
        }

        internal static float Capacity(Pawn vehicle)
        {
            try { return Math.Max(0f, (float)getStatValue.Invoke(vehicle, new[] { cargoCapacity })); }
            catch (Exception exception) { Report(exception); return 0f; }
        }

        internal static float Usage(Pawn vehicle) => MassUtility.GearAndInventoryMass(vehicle);

        private static int CountThatFits(Pawn vehicle, Thing item) =>
            StorageTransportExchange.CountThatFits(item, Capacity(vehicle) - Usage(vehicle));

        internal static void Supply(Building_TransportSupplier supplier, Pawn vehicle)
        {
            try
            {
                if (!Valid(supplier, vehicle)) return;
                if (supplier.DirectSupply) SupplyFromNetwork(supplier, vehicle);
                else SupplyFromBuffer(supplier, vehicle);
            }
            catch (Exception exception) { Report(exception); }
        }

        private static void SupplyFromBuffer(Building_TransportSupplier supplier, Pawn vehicle)
        {
            foreach (Thing item in new List<Thing>(supplier.Contents))
            {
                if (!Valid(supplier, vehicle)) return;
                if (item.Destroyed || !supplier.Contents.Contains(item)) continue;
                int count = CountThatFits(vehicle, item);
                if (count <= 0) continue;
                Thing part = supplier.Contents.Take(item, count);
                if (part == null) continue;
                try { Load(vehicle, part, part.stackCount); }
                finally
                {
                    // A rejected/partial load or a throwing cargo event must retain the real remainder.
                    if (!part.Destroyed && !part.Spawned && part.holdingOwner == null && !supplier.Contents.TryAdd(part, false))
                        supplier.Map.GetComponent<MapComponent_StorageNetworks>().PreserveLooseThing(part, supplier.Position);
                }
            }
        }

        private static void SupplyFromNetwork(Building_TransportSupplier supplier, Pawn vehicle)
        {
            StorageNetwork network = supplier.Network;
            var stacks = new List<Thing>();
            for (int slot = 0; slot < Building_TransportSupplier.SlotCount; slot++)
            {
                ThingDef itemDef = supplier.SelectedDef(slot);
                if (itemDef == null || supplier.TargetLimit(slot) == 0) continue;
                stacks.Clear(); network.GetStacks(itemDef, stacks);
                foreach (Thing item in stacks)
                {
                    if (!Valid(supplier, vehicle) || !supplier.CanWork || supplier.Network != network) return;
                    int deficit = supplier.TargetDeficit(vehicle.inventory.innerContainer, slot);
                    if (deficit == 0) break;
                    int count = Math.Min(deficit, Math.Min(network.AvailableToWithdraw(item), CountThatFits(vehicle, item)));
                    if (count <= 0) continue;
                    using (var transfer = new StorageMaterialTransaction(supplier.Map, supplier.Position))
                    {
                        if (!transfer.Add(item, count, network, supplier) || !transfer.TryStage()) continue;
                        if (!Valid(supplier, vehicle) || !supplier.CanWork || supplier.Network != network) return;
                        transfer.Deliver(transfer.Requests[0], staged =>
                        {
                            int amount = Math.Min(supplier.TargetDeficit(vehicle.inventory.innerContainer, slot), CountThatFits(vehicle, staged));
                            return amount > 0 && Load(vehicle, staged, amount) > 0;
                        });
                    }
                }
            }
        }

        // Native loading sends CargoAdded (including turret reload/stat refresh) and updates cargoToLoad.
        private static int Load(Pawn vehicle, Thing item, int count) =>
            (int)addOrTransfer.Invoke(vehicle, new object[] { item, count });

        private static void Report(Exception exception)
        {
            if (reportedFailure) return;
            reportedFailure = true;
            Log.Error("[MagicStorage] Vehicle Framework cargo supply failed: " + exception);
        }
    }
}
