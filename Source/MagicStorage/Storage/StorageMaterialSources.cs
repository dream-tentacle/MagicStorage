using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Shared by production and gestation: shelves supplement stored inventory.
    internal static class StorageMaterialSources
    {
        internal static void GetShelfStacks(StorageNetwork network, Predicate<ThingDef> filter, List<Thing> result,
            bool includeReserved = false)
        {
            if (network?.CanWork != true) return;
            foreach (var shelf in network.Core.Map.GetComponent<MapComponent_StorageNetworks>().SupplyShelves)
                if (UsableShelf(network, shelf))
                    foreach (var item in shelf.GetSlotGroup().HeldThings)
                        if (filter(item.def) && Available(network, item, includeReserved) > 0) result.Add(item);
        }

        private static bool UsableShelf(StorageNetwork network, Building_StorageSupplyShelf shelf) =>
            shelf.Spawned && shelf.Map == network.Core.Map && shelf.Network == network &&
            shelf.Faction == network.Core.Faction && !shelf.IsForbidden(network.Core.Faction) &&
            !shelf.IsBurning() && !shelf.Position.ContainsStaticFire(shelf.Map);

        internal static int Available(StorageNetwork network, Thing item)
            => Available(network, item, false);

        private static int Available(StorageNetwork network, Thing item, bool ignoreReservations)
        {
            if (network?.CanWork != true || item == null || item.Destroyed) return 0;
            if (!item.Spawned) return network.AvailableToWithdraw(item);
            if (item.Map != network.Core.Map || item.IsForbidden(network.Core.Faction) || item.IsBurning()) return 0;
            var shelf = item.Map.haulDestinationManager.SlotGroupParentAt(item.Position) as Building_StorageSupplyShelf;
            return shelf != null && UsableShelf(network, shelf) &&
                StorageMaterialTransaction.GroundAvailable(item.Map, item, !ignoreReservations) ? item.stackCount : 0;
        }
    }
}
