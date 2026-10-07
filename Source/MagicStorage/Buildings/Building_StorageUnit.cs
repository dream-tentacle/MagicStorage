using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class Building_StorageUnit : Building, IThingHolder, IThingHolderTickable, ISuspendableThingHolder
    {
        private StorageInventory inventory;
        private int dataVersion = 1;
        public StorageInventory Inventory => inventory ?? (inventory = new StorageInventory(this));
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public int SlotCapacity => GetComp<CompStorageNode>()?.SlotCapacity ?? 64;
        public bool ShouldTickContents => false;
        public bool IsContentsSuspended => true;
        public ThingOwner GetDirectlyHeldThings() => Inventory.Contents;
        public void GetChildHolders(List<IThingHolder> children)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(children, GetDirectlyHeldThings());
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dataVersion, "storageDataVersion", 1);
            Scribe_Deep.Look(ref inventory, "storageInventory", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Inventory.Contents.dontTickContents = true;
                Inventory.RebuildIndex();
            }
        }

        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Map releaseMap = MapHeld;
            IntVec3 releaseCell = PositionHeld;
            StorageRecoveryBatch batch = null;
            if (Inventory.UsedSlots > 0)
            {
                if (releaseMap == null)
                {
                    // Do not orphan contents when an unspawned loaded unit is destroyed.
                    Log.Warning("[MagicStorage] Refused to destroy a loaded unit without a map for its contents.");
                    return;
                }
                var manager = releaseMap.GetComponent<MapComponent_StorageNetworks>();
                manager.NotifyNodeUnavailable(this);
                batch = manager.CreateRecovery(releaseCell);
                while (Inventory.Contents.Count > 0)
                {
                    Thing item = Inventory.Contents[Inventory.Contents.Count - 1];
                    int moved = StorageTransfer.Move(Inventory.Contents, batch.Contents, item, item.stackCount, releaseMap, releaseCell, false);
                    if (moved == 0)
                    {
                        Log.Error("[MagicStorage] Unit destruction stopped because its inventory could not be transferred.");
                        return;
                    }
                }
            }
            base.Destroy(mode);
            // Remove the building first so it cannot obstruct placement. Try every stack
            // immediately; only undelivered contents remain in the saved retry buffer.
            if (batch != null) releaseMap.GetComponent<MapComponent_StorageNetworks>().ReleaseRecovery(batch);
        }

        public override string GetInspectString()
        {
            return (base.GetInspectString() + "\n" + "MS_UnitCapacity".Translate(Inventory.UsedSlots, SlotCapacity)).Trim();
        }
    }
}
