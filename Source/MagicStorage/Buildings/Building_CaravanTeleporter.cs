using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class Building_CaravanTeleporter : Building
    {
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true;

        public override void SetFaction(Faction faction, Pawn recruiter = null)
        {
            base.SetFaction(faction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!Spawned || Faction != Faction.OfPlayer) yield break;
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            var command = new Command_Action
            {
                defaultLabel = "MS_Caravan_Open".Translate(),
                defaultDesc = "MS_Caravan_OpenDesc".Translate(),
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Gizmos/MagicStorage/TransferItems"),
                action = () =>
                {
                    Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
                    if (CanWork) Find.WindowStack.Add(new Dialog_StorageCaravan(this));
                }
            };
            if (!CanWork) command.Disable(StorageNetwork.StatusLabel(Network));
            else if (!Map.Tile.LayerDef.canFormCaravans) command.Disable("MS_Caravan_LayerUnavailable".Translate());
            yield return command;
        }
    }
}
