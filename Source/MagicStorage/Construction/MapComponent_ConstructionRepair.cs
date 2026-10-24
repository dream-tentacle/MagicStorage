using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Claims remain in network inventory until completion. Reloading restarts the
    // short animation without withdrawing, losing or duplicating any material.
    public sealed class MapComponent_ConstructionRepair : MapComponent
    {
        internal const int RepairTicks = 180;

        private sealed class Repair
        {
            internal Building_ConstructionSupplyUnit Supplier;
            internal Thing Target, Material;
            internal StorageNetwork Network;
            internal int FinishTick;
            internal MoteDualAttached Beam;
        }

        private readonly Dictionary<Thing, Repair> active = new Dictionary<Thing, Repair>();
        internal int ActiveCount => active.Count;
        internal bool IsRepairing(Thing target) => target != null && active.ContainsKey(target);

        public MapComponent_ConstructionRepair(Map map) : base(map) { }

        internal void Scan(Building_ConstructionSupplyUnit supplier)
        {
            supplier.RepairScanCount++;
            supplier.LastRepairScanTick = Find.TickManager.TicksGame;
            if (!supplier.RepairMode || !supplier.CanWork) return;
            var targets = new List<Thing>(map.GetComponent<BreakdownManager>().brokenDownThings);
            targets.Sort((a, b) =>
            {
                int distance = a.Position.DistanceToSquared(supplier.Position).CompareTo(b.Position.DistanceToSquared(supplier.Position));
                return distance != 0 ? distance : a.thingIDNumber.CompareTo(b.thingIDNumber);
            });
            foreach (Thing target in targets)
                if (!active.ContainsKey(target) && CanRepair(supplier, target)) TryStart(supplier, target);
        }

        // Read-only, explicitly requested from a developer gizmo. No reservations,
        // forced scans, repairs or log spam are introduced by this report.
        internal static string Diagnostics(Building_ConstructionSupplyUnit supplier)
        {
            var text = new StringBuilder("[MagicStorage repair diagnostics]\n");
            text.AppendLine("tick=" + Find.TickManager.TicksGame + ", repairMode=" + supplier.RepairMode +
                ", canWork=" + supplier.CanWork + ", network=" + supplier.Network?.Status);
            text.AppendLine("supplyScans=" + supplier.SupplyRefreshCount + ", lastSupply=" + supplier.LastSupplyRefreshTick +
                ", repairScans=" + supplier.RepairScanCount + ", lastRepair=" + supplier.LastRepairScanTick +
                ", radius=" + Building_ConstructionSupplyUnit.SupplyRadius);
            var map = supplier.Map;
            if (map == null) return text.AppendLine("supplier has no map").ToString();
            var repairs = map.GetComponent<MapComponent_ConstructionRepair>();
            var breakdowns = map.GetComponent<BreakdownManager>();
            var stacks = new List<Thing>();
            supplier.Network?.GetStacks(ThingDefOf.ComponentIndustrial, stacks);
            long available = 0;
            foreach (Thing item in stacks) available += supplier.Network.AvailableToWithdraw(item);
            text.AppendLine("repairManager=" + (repairs != null) + ", active=" + (repairs?.ActiveCount ?? 0) +
                ", componentStacks=" + stacks.Count + ", availableComponents=" + available +
                ", breakdownManager=" + (breakdowns != null) + ", breakdowns=" + (breakdowns?.brokenDownThings.Count ?? 0));
            if (breakdowns == null) return text.ToString();
            foreach (Thing target in breakdowns.brokenDownThings)
                text.AppendLine(DescribeTarget(supplier, target));
            return text.ToString();
        }

        internal static string DescribeTarget(Building_ConstructionSupplyUnit supplier, Thing target)
        {
            if (target == null) return "target=null";
            var building = target as Building;
            bool spawned = target.Spawned && target.Map == supplier.Map;
            return "target=" + target + ", position=" + target.Position + ", eligible=" + CanRepair(supplier, target) +
                ", spawnedOnMap=" + spawned + ", player=" + (target.Faction == Faction.OfPlayer) +
                ", distanceSquared=" + target.Position.DistanceToSquared(supplier.Position) +
                ", repairable=" + building?.def.building?.repairable +
                ", broken=" + building?.GetComp<CompBreakdownable>()?.BrokenDown +
                ", forbidden=" + (spawned && target.IsForbidden(Faction.OfPlayer)) +
                ", burning=" + (spawned && target.IsBurning()) +
                ", reserved=" + (spawned && target.Map.reservationManager.IsReservedByAnyoneOf(target, Faction.OfPlayer)) +
                ", interacting=" + (spawned && target.Map.physicalInteractionReservationManager.IsReserved(target)) +
                ", deconstruct=" + (spawned && target.Map.designationManager.DesignationOn(target, DesignationDefOf.Deconstruct) != null);
        }

        // Separate target boundaries also let optional floor compatibility enter
        // the target's context for native reservation, designation and repair calls.
        internal static bool CanRepair(Building_ConstructionSupplyUnit supplier, Thing target)
        {
            if (!supplier.RepairMode || !supplier.CanWork || !(target is Building building) ||
                !target.Spawned || target.Destroyed || target.Map != supplier.Map ||
                target.Faction != Faction.OfPlayer || building.def.building?.repairable != true ||
                target.Position.DistanceToSquared(supplier.Position) >
                    Building_ConstructionSupplyUnit.SupplyRadius * Building_ConstructionSupplyUnit.SupplyRadius ||
                target.IsForbidden(Faction.OfPlayer) || target.IsBurning() ||
                building.GetComp<CompBreakdownable>()?.BrokenDown != true) return false;

            return target.Map.designationManager.DesignationOn(target, DesignationDefOf.Deconstruct) == null &&
                !target.Map.reservationManager.IsReservedByAnyoneOf(target, Faction.OfPlayer) &&
                !target.Map.physicalInteractionReservationManager.IsReserved(target);
        }

        internal void TryStart(Building_ConstructionSupplyUnit supplier, Thing target)
        {
            if (active.ContainsKey(target) || !CanRepair(supplier, target)) return;
            var network = supplier.Network;
            var stacks = new List<Thing>();
            network.GetStacks(ThingDefOf.ComponentIndustrial, stacks);
            foreach (Thing item in stacks)
            {
                if (network.AvailableToWithdraw(item) < 1) continue;
                var repair = new Repair { Supplier = supplier, Target = target, Network = network,
                    Material = item, FinishTick = Find.TickManager.TicksGame + RepairTicks };
                if (network.ReserveOutgoing(repair, item, 1, supplier) != 1) continue;
                try
                {
                    var beamDef = DefDatabase<ThingDef>.GetNamedSilentFail("MS_Mote_ConstructionRepairBeam") ??
                        DefDatabase<ThingDef>.GetNamedSilentFail("MS_Mote_ConstructionRepairBeamFallback");
                    if (beamDef == null) return;
                    var sourceOffset = supplier.TrueCenter() - supplier.Position.ToVector3Shifted();
                    var targetOffset = target.TrueCenter() - target.Position.ToVector3Shifted();
                    repair.Beam = MoteMaker.MakeInteractionOverlay(beamDef,
                        new TargetInfo(supplier.Position, map), new TargetInfo(target.Position, map), sourceOffset, targetOffset);
                    active.Add(target, repair);
                    return;
                }
                finally
                {
                    if (!active.ContainsKey(target)) Release(repair);
                }
            }
        }

        public override void MapComponentTick()
        {
            if (active.Count == 0) return;
            map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            foreach (Repair repair in new List<Repair>(active.Values))
            {
                if (repair.Supplier.Map != map || repair.Supplier.Network != repair.Network ||
                    !CanRepair(repair.Supplier, repair.Target) ||
                    !repair.Network.HasOutgoingReservation(repair, repair.Material, 1))
                {
                    Cancel(repair);
                    continue;
                }
                if (Find.TickManager.TicksGame < repair.FinishTick) continue;
                try { CompleteRepair(repair.Supplier, repair.Target, repair); }
                finally { Cancel(repair); }
            }
        }

        private void CompleteRepair(Building_ConstructionSupplyUnit supplier, Thing target, Repair repair)
        {
            // Replace the long-lived claim with the existing atomic material
            // transaction. It handles extraction, consumption and rollback.
            repair.Network.ReleaseOutgoing(repair);
            using (var materials = new StorageMaterialTransaction(map, supplier.Position))
            {
                if (!CanRepair(supplier, target) ||
                    !materials.Add(repair.Material, 1, repair.Network, supplier) || !materials.TryStage() ||
                    !CanRepair(supplier, target)) return;
                if (materials.ConsumeAll()) ((Building)target).GetComp<CompBreakdownable>().Notify_Repaired();
            }
        }

        internal void CancelFor(Building_ConstructionSupplyUnit supplier)
        {
            foreach (Repair repair in new List<Repair>(active.Values))
                if (repair.Supplier == supplier) Cancel(repair);
        }

        private void Cancel(Repair repair)
        {
            active.Remove(repair.Target);
            Release(repair);
        }

        private static void Release(Repair repair)
        {
            repair.Network.ReleaseOutgoing(repair);
            if (repair.Beam != null && !repair.Beam.Destroyed) repair.Beam.Destroy();
        }

        public override void MapRemoved()
        {
            foreach (Repair repair in new List<Repair>(active.Values)) Cancel(repair);
            base.MapRemoved();
        }
    }
}
