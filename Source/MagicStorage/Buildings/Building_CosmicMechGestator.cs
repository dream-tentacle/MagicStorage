using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Keep native bills, mechanitor bandwidth accounting, cycles and completion
    // callbacks. In particular, do not replace Notify_FormingCompleted.
    public sealed class Building_CosmicMechGestator : Building_MechGestator
    {
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool NetworkReady => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true;

        public override bool CanWork() => NetworkReady && base.CanWork();

        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }

        public override void EjectContents()
        {
            // Native cancellation destroys a finished, unreleased mech. Only return
            // real ingredients; never store a live Pawn or refund a completed batch.
            if (Map != null && (ActiveMechBill == null || ActiveMechBill.State != FormingState.Formed))
            {
                var manager = Map.GetComponent<MapComponent_StorageNetworks>();
                manager.EnsureCurrent();
                var network = NetworkReady ? Network : null;
                var recovery = manager.CreateRecovery(InteractionCell);
                try
                {
                    foreach (var item in new List<Thing>(innerContainer))
                    {
                        if (item is Pawn) continue;
                        if (network != null) network.TryStore(innerContainer, item, item.stackCount);
                        if (!item.Destroyed && innerContainer.Contains(item))
                            StorageTransfer.Move(innerContainer, recovery.Contents, item, item.stackCount,
                                Map, InteractionCell, false);
                    }
                }
                finally { manager.ReleaseRecovery(recovery); }
            }
            base.EjectContents();
        }
    }
}
