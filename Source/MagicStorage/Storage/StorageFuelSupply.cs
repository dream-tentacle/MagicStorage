using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Shared by directional material suppliers and area fuel suppliers.
    internal static class StorageFuelSupply
    {
        internal static bool NeedsFuel(CompRefuelable target, bool immediate = false)
        {
            if (target == null || !target.parent.Spawned || target.parent.Destroyed ||
                target.parent.Position.Fogged(target.parent.Map) || !target.allowAutoRefuel || target.IsFull ||
                !(immediate ? target.ShouldAutoRefuelNowIgnoringFuelPct : target.ShouldAutoRefuelNow) ||
                (!target.Props.allowRefuelIfNotEmpty && target.Fuel > 0)) return false;
            CompInteractable interactable = target.parent.TryGetComp<CompInteractable>();
            return interactable == null || !interactable.Props.cooldownPreventsRefuel || !interactable.OnCooldown;
        }

        internal static void Deliver(Building supplier, StorageNetwork network, CompRefuelable target,
            IEnumerable<ThingDef> fuelTypes, bool immediate = false)
        {
            if (!CanDeliver(supplier, network, target, immediate)) return;
            using (var transfer = new StorageMaterialTransaction(supplier.Map, supplier.Position))
            {
                var stacks = new List<Thing>();
                var seen = new HashSet<Thing>();
                int remaining = target.GetFuelCountToFullyRefuel();
                foreach (ThingDef itemDef in fuelTypes)
                {
                    if (itemDef == null || !target.Props.fuelFilter.Allows(itemDef)) continue;
                    stacks.Clear(); network.GetStacks(itemDef, stacks);
                    foreach (Thing item in stacks)
                    {
                        if (remaining == 0) break;
                        if (!seen.Add(item) || item.Destroyed || !target.Props.fuelFilter.Allows(item)) continue;
                        int count = Math.Min(remaining, network.AvailableToWithdraw(item));
                        if (count <= 0) continue;
                        if (!transfer.Add(item, count, network, supplier)) return;
                        remaining -= count;
                    }
                    if (remaining == 0) break;
                }
                if (transfer.Requests.Count == 0 || (target.Props.atomicFueling && remaining > 0) || !transfer.TryStage()) return;
                if (!CanDeliver(supplier, network, target, immediate)) return;
                remaining = target.GetFuelCountToFullyRefuel();
                long total = 0; foreach (var request in transfer.Requests) total += request.Count;
                if (target.Props.atomicFueling && total < remaining) return;
                foreach (var request in transfer.Requests)
                {
                    int count = Math.Min(remaining, request.Count);
                    if (count <= 0) break;
                    float before = target.Fuel;
                    bool refueled = false;
                    try
                    {
                        // Like native Refuel(List<Thing>), apply fuel and its signal before
                        // destroying that stack. A throwing signal may still have added fuel.
                        target.Refuel(count);
                        refueled = true;
                    }
                    finally
                    {
                        if ((refueled || target.Fuel > before) && !transfer.Consume(request, count))
                            Log.Error("[MagicStorage] Refueled material could not be consumed.");
                    }
                    remaining -= count;
                }
            }
        }

        private static bool CanDeliver(Building supplier, StorageNetwork network, CompRefuelable target, bool immediate) =>
            supplier.Spawned && supplier.Faction == Faction.OfPlayer && network?.CanWork == true &&
            supplier.GetComp<CompStorageNode>()?.Network == network && NeedsFuel(target, immediate) &&
            target.parent.Map == supplier.Map && target.parent.Faction == supplier.Faction;
    }
}
