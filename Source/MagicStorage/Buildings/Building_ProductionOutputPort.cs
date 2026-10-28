using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class Building_ProductionOutputPort : Building
    {
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;

        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }
    }
}
