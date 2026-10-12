using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Building_FuelSupplier : Building
    {
        public const float SupplyRadius = 20f;
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true;

        protected override void Tick()
        {
            base.Tick();
            if (Spawned && this.IsHashIntervalTick(60)) SupplyFuel();
        }

        internal void SupplyFuel(bool immediate = false)
        {
            if (!Spawned || Faction != Faction.OfPlayer) return;
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (!CanWork) return;
            StorageNetwork network = Network;
            var targets = new List<Thing>();
            // RimWorld maintains this component index; unrelated map items are never scanned.
            foreach (Thing target in Map.listerThings.ThingsInGroup(ThingRequestGroup.Refuelable))
                if (target.Spawned && !target.Destroyed && target.Map == Map && target.Faction == Faction &&
                    target.def.category == ThingCategory.Building &&
                    target.Position.DistanceToSquared(Position) <= SupplyRadius * SupplyRadius)
                    targets.Add(target);
            targets.Sort((a, b) =>
            {
                int distance = a.Position.DistanceToSquared(Position).CompareTo(b.Position.DistanceToSquared(Position));
                return distance != 0 ? distance : a.thingIDNumber.CompareTo(b.thingIDNumber);
            });
            foreach (Thing target in targets)
            {
                if (!CanWork || Network != network) break;
                CompRefuelable refuelable = target.TryGetComp<CompRefuelable>();
                if (StorageFuelSupply.NeedsFuel(refuelable, immediate))
                    StorageFuelSupply.Deliver(this, network, refuelable, refuelable.Props.fuelFilter.AllowedThingDefs, immediate);
            }
        }

        public override void SetFaction(Faction faction, Pawn recruiter = null)
        {
            base.SetFaction(faction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            GenDraw.DrawRadiusRing(Position, SupplyRadius);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!Spawned || Faction != Faction.OfPlayer) yield break;
            var immediate = new Command_Action
            {
                defaultLabel = "MS_FuelSupply_Immediate".Translate(),
                defaultDesc = "MS_FuelSupply_ImmediateDesc".Translate(SupplyRadius),
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Gizmos/MagicStorage/Refuel"),
                action = () => SupplyFuel(true)
            };
            if (!CanWork) immediate.Disable(StorageNetwork.StatusLabel(Network));
            yield return immediate;
        }

        public override string GetInspectString() =>
            (base.GetInspectString() + "\n" + "MS_FuelSupply_Use".Translate(SupplyRadius)).Trim();
    }

    public sealed class PlaceWorker_FuelSupplier : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        { GenDraw.DrawRadiusRing(center, Building_FuelSupplier.SupplyRadius); }
    }
}
