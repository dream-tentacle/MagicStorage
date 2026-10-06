using RimWorld;
using Verse;

namespace MagicStorage
{
    // Not an IHaulSource or a container: only explicit food requests may withdraw.
    public sealed class Building_StorageFoodOutlet : Building
    {
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true;
        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().MarkDirty();
        }
        public override string GetInspectString() =>
            (base.GetInspectString() + "\n" + "MS_FoodOutletUse".Translate()).Trim();
    }
}
