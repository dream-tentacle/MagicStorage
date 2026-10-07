using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Immediate transfers: never retain an unowned material or a claim between scans.
    // The network's transfer API preserves all existing food/crafting reservations.
    internal static class ConstructionSupply
    {
        internal static void Deliver(Building_ConstructionSupplyUnit supplier)
        {
            if (!supplier.CanWork) return;
            var targets = new List<Thing>();
            Collect(supplier, ThingRequestGroup.Blueprint, targets);
            Collect(supplier, ThingRequestGroup.BuildingFrame, targets);
            targets.Sort((a, b) =>
            {
                int distance = a.Position.DistanceToSquared(supplier.Position).CompareTo(b.Position.DistanceToSquared(supplier.Position));
                return distance != 0 ? distance : a.thingIDNumber.CompareTo(b.thingIDNumber);
            });
            foreach (Thing target in targets)
            {
                if (!supplier.CanWork) break;
                DeliverTo(supplier, target);
            }
        }

        private static void Collect(Building_ConstructionSupplyUnit supplier, ThingRequestGroup group, List<Thing> targets)
        {
            foreach (Thing target in supplier.Map.listerThings.ThingsInGroup(group))
                if (InRange(supplier, target)) targets.Add(target);
        }

        private static bool InRange(Building_ConstructionSupplyUnit supplier, Thing target) =>
            target != null && target.Spawned && target.Map == supplier.Map &&
            target.Position.DistanceToSquared(supplier.Position) <= Building_ConstructionSupplyUnit.SupplyRadius * Building_ConstructionSupplyUnit.SupplyRadius;

        internal static int DeliverTo(Building_ConstructionSupplyUnit supplier, Thing target)
        {
            if (!supplier.CanWork || !InRange(supplier, target) || !ConstructionSite.CanSupply(target)) return 0;
            // Capture cell endpoints before delivery replaces the blueprint. The short
            // visual remains at the site's center even if its frame completes immediately.
            Map map = supplier.Map;
            var source = new TargetInfo(supplier.Position, map);
            var destination = new TargetInfo(target.Position, map);
            var sourceOffset = supplier.TrueCenter() - supplier.Position.ToVector3Shifted();
            var destinationOffset = target.TrueCenter() - target.Position.ToVector3Shifted();
            int delivered = DeliverMaterials(supplier, target);
            if (delivered > 0)
            {
                var beamDef = DefDatabase<ThingDef>.GetNamedSilentFail("MS_Mote_ConstructionSupplyBeam");
                if (beamDef != null)
                    MoteMaker.MakeInteractionOverlay(beamDef, source, destination, sourceOffset, destinationOffset);
            }
            return delivered;
        }

        private static int DeliverMaterials(Building_ConstructionSupplyUnit supplier, Thing target)
        {
            var network = supplier.Network;
            var costs = ((IConstructible)target).TotalMaterialCost();
            var stacks = new List<Thing>();
            int delivered = 0;
            foreach (var cost in costs)
            {
                if (!supplier.CanWork || supplier.Network != network || !ConstructionSite.CanSupply(target)) break;
                int needed = ConstructionSite.Needed(target, cost.thingDef);
                if (needed <= 0) continue;
                stacks.Clear();
                network.GetStacks(cost.thingDef, stacks);
                foreach (Thing item in stacks)
                {
                    if (!supplier.CanWork || supplier.Network != network || !ConstructionSite.CanSupply(target)) break;
                    needed = ConstructionSite.Needed(target, cost.thingDef);
                    int count = Math.Min(needed, network.AvailableToWithdraw(item));
                    if (count <= 0) continue;
                    // Leave an empty blueprint untouched until we actually have usable stock.
                    if (target is Blueprint_Build blueprint)
                    {
                        target = ConstructionSite.MakeFrame(blueprint);
                        if (target == null) return delivered;
                    }
                    if (!(target is Frame frame) || !ConstructionSite.CanSupply(frame)) return delivered;
                    count = Math.Min(count, ConstructionSite.Needed(frame, cost.thingDef));
                    if (count <= 0) break;
                    delivered += network.TryTransferTo(item, count, frame.resourceContainer).Transferred;
                }
            }
            return delivered;
        }
    }
}
