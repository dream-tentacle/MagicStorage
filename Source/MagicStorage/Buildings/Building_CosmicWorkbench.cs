using System.Collections.Generic;
using Verse;
using RimWorld;

namespace MagicStorage
{
    public sealed class Building_CosmicWorkbench : Building
    {
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true;
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!Spawned || Faction != Faction.OfPlayer) yield break;
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (!CanWork) yield break;
            yield return new Command_Action
            {
                defaultLabel = "MS_Craft_Open".Translate(),
                defaultDesc = "MS_Craft_OpenDesc".Translate(),
                icon = def.uiIcon,
                action = () =>
                {
                    if (!Spawned || Faction != Faction.OfPlayer) return;
                    Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
                    if (CanWork) Find.WindowStack.Add(new Dialog_CosmicCrafting(Network.Core));
                }
            };
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        { base.SpawnSetup(map, respawningAfterLoad); map.GetComponent<MapComponent_CosmicCrafting>().Register(this); }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        { Map.GetComponent<MapComponent_CosmicCrafting>().Unregister(this); base.DeSpawn(mode); }
        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }
    }
}
