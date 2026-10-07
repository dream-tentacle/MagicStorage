using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Building_ConstructionSupplyUnit : Building
    {
        public const float SupplyRadius = 40f;
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true;

        public override void TickRare()
        {
            base.TickRare();
            if (!Spawned) return;
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (CanWork) ConstructionSupply.Deliver(this);
        }

        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            GenDraw.DrawRadiusRing(Position, SupplyRadius);
        }

        public override string GetInspectString() =>
            (base.GetInspectString() + "\n" + "MS_ConstructionSupplyUse".Translate(SupplyRadius)).Trim();
    }

    public sealed class PlaceWorker_ConstructionSupply : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        { GenDraw.DrawRadiusRing(center, Building_ConstructionSupplyUnit.SupplyRadius); }
    }
}
