using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal static class CraftingWorkbenchUnlocks
    {
        internal static bool Free(ThingDef table) => table != null &&
            (!table.MadeFromStuff || table.CostStuffCount <= 0) &&
            (table.CostList == null || !table.CostList.Any(cost => cost.count > 0));

        internal static bool IsUnlocked(ThingDef table) => table != null &&
            (!MagicStorageMod.Settings.WorkbenchUnlockMode || Free(table) ||
                Current.Game?.GetComponent<GameComponent_WorkbenchUnlocks>()?.IsUnlocked(table.defName) == true);

        internal static bool TryGetCosts(ThingDef table, ThingDef stuff, out List<ThingDefCountClass> costs)
        {
            costs = null;
            if (table == null) return false;
            if (Free(table)) { costs = new List<ThingDefCountClass>(); return true; }
            if (table.MadeFromStuff && (stuff == null || !GenStuff.AllowedStuffsFor(table).Contains(stuff))) return false;
            if (!table.MadeFromStuff && stuff != null) return false;
            costs = table.CostListAdjusted(stuff).Where(cost => cost.count > 0)
                .GroupBy(cost => cost.thingDef)
                .Select(group => new ThingDefCountClass(group.Key, group.Sum(cost => cost.count))).ToList();
            return costs.All(cost => cost.thingDef != null);
        }

        // Network stacks come first; only connected, usable shelf stock can supplement them.
        internal static List<Thing> Stocks(Building_StorageCore core, List<ThingDefCountClass> costs)
        {
            var result = new List<Thing>();
            if (core == null || !core.Spawned || core.Destroyed || core.Faction != Faction.OfPlayer) return result;
            var manager = core.Map.GetComponent<MapComponent_StorageNetworks>();
            manager.EnsureCurrent();
            var network = core.Network;
            if (network?.CanWork != true) return result;
            var defs = new HashSet<ThingDef>(costs.Select(cost => cost.thingDef));
            network.GetMatchingStacks(defs.Contains, result);
            foreach (var shelf in manager.SupplyShelves)
            {
                if (!shelf.Spawned || shelf.Network != network) continue;
                foreach (var item in shelf.GetSlotGroup().HeldThings)
                    if (defs.Contains(item.def) && NetworkCraftingMaterialPolicy.Available(network, item) > 0 &&
                        StorageMaterialTransaction.GroundAvailable(core.Map, item)) result.Add(item);
            }
            return result.Distinct().Where(item => Available(network, item) > 0).ToList();
        }

        internal static int Available(StorageNetwork network, Thing item) =>
            NetworkCraftingMaterialPolicy.Available(network, item);

        internal static bool TryUnlock(Building_StorageCore core, ThingDef table, ThingDef stuff)
        {
            var state = Current.Game?.GetComponent<GameComponent_WorkbenchUnlocks>();
            if (state == null || !MagicStorageMod.Settings.WorkbenchUnlockMode || table == null) return false;
            if (!GameComponent_WorkbenchUnlocks.ValidateDefName(table.defName) || !table.IsResearchFinished) return false;
            if (IsUnlocked(table)) return true;
            if (!TryGetCosts(table, stuff, out var costs)) return false;
            var stocks = Stocks(core, costs);
            var network = core?.Network;
            if (network?.CanWork != true) return false;
            var allocations = new Dictionary<Thing, int>();
            foreach (var cost in costs)
            {
                int remaining = cost.count;
                foreach (var item in stocks.Where(item => item.def == cost.thingDef))
                {
                    int take = Math.Min(remaining, Available(network, item));
                    if (take <= 0) continue;
                    allocations.Add(item, take);
                    remaining -= take;
                    if (remaining == 0) break;
                }
                if (remaining > 0) return false;
            }
            using (var transaction = new StorageMaterialTransaction(core.Map, core.Position))
            {
                foreach (var allocation in allocations)
                    if (!transaction.Add(allocation.Key, allocation.Value,
                        allocation.Key.Spawned ? null : network, allocation.Key.Spawned ? null : core)) return false;
                if (!transaction.TryStage() || !transaction.ConsumeAll()) return false;
            }
            state.Unlock(table.defName);
            return true;
        }
    }
}
