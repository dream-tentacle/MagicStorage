using RimWorld;
using Verse;

namespace MagicStorage
{
    // An order-driven endpoint, with no persistent item buffer or polling tick.
    public sealed class Building_NivarianCannonExtractor : Building
    {
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && !Destroyed && Faction == Faction.OfPlayer &&
            Network?.CanWork == true && GetComp<CompPowerTrader>()?.PowerOn == true &&
            (GetComp<CompFlickable>()?.SwitchIsOn ?? true);

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            NivarianCannonCompatibility.Register(this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            NivarianCannonCompatibility.Unregister(this);
            base.DeSpawn(mode);
        }

        public override void SetFaction(Faction faction, Pawn recruiter = null)
        {
            base.SetFaction(faction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }

        public override string GetInspectString()
        {
            string state = !NivarianCannonCompatibility.Ready ? "Unavailable" :
                !CanWork ? "Offline" : NivarianCannonCompatibility.HasAdjacentCannon(this) ? "Ready" : "Unlinked";
            return (base.GetInspectString() + "\n" + ("MS_NivarianExtractor_" + state).Translate()).Trim();
        }
    }
}
