using System;
using System.Collections.Generic;
using System.Linq;
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

        private sealed class FuelClaim
        {
            internal Thing Item, Received;
            internal ThingOwner Source;
            internal int Count;
        }

        internal static void Deliver(Building supplier, StorageNetwork network, CompRefuelable target,
            IEnumerable<ThingDef> fuelTypes, bool immediate = false)
        {
            if (!CanDeliver(supplier, network, target, immediate)) return;
            var claims = new List<FuelClaim>();
            var stacks = new List<Thing>();
            int remaining = target.GetFuelCountToFullyRefuel();
            foreach (ThingDef itemDef in fuelTypes)
            {
                if (itemDef == null || !target.Props.fuelFilter.Allows(itemDef)) continue;
                stacks.Clear(); network.GetStacks(itemDef, stacks);
                foreach (Thing item in stacks)
                {
                    if (remaining == 0) break;
                    if (item.Destroyed || !target.Props.fuelFilter.Allows(item)) continue;
                    int count = Math.Min(remaining, network.AvailableToWithdraw(item));
                    if (count <= 0) continue;
                    claims.Add(new FuelClaim { Item = item, Source = item.holdingOwner, Count = count });
                    remaining -= count;
                }
                if (remaining == 0) break;
            }
            if (claims.Count == 0 || (target.Props.atomicFueling && remaining > 0)) return;
            var manager = supplier.Map.GetComponent<MapComponent_StorageNetworks>();
            StorageRecoveryBatch staging = manager.CreateRecovery(supplier.Position);
            try
            {
                foreach (FuelClaim claim in claims)
                    if (network.ReserveOutgoing(claim, claim.Item, claim.Count, supplier) != claim.Count) return;
                foreach (FuelClaim claim in claims)
                    if (!network.TryWithdrawReserved(claim, claim.Item, claim.Count, staging.Contents, out claim.Received).Complete) return;
                if (!CanDeliver(supplier, network, target, immediate)) return;
                // Native Refuel consumes real Things and pops from the list's end.
                target.Refuel(claims.AsEnumerable().Reverse().Select(c => c.Received).ToList());
            }
            finally
            {
                foreach (FuelClaim claim in claims)
                {
                    network.ReleaseOutgoing(claim);
                    if (claim.Received != null && staging.Contents.Contains(claim.Received))
                        StorageTransfer.Move(staging.Contents, claim.Source, claim.Received,
                            claim.Received.stackCount, supplier.Map, supplier.Position);
                }
                manager.ReleaseRecovery(staging);
            }
        }

        private static bool CanDeliver(Building supplier, StorageNetwork network, CompRefuelable target, bool immediate) =>
            supplier.Spawned && supplier.Faction == Faction.OfPlayer && network?.CanWork == true &&
            supplier.GetComp<CompStorageNode>()?.Network == network && NeedsFuel(target, immediate) &&
            target.parent.Map == supplier.Map && target.parent.Faction == supplier.Faction;
    }
}
