using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Only one already-collected garment is held here. Bulk inventory stays in units.
    public sealed class Building_StorageApparelAdapter : Building, IApparelSource, ISuspendableThingHolder
    {
        private ThingOwner<Thing> contents;
        private Pawn recipient;
        public Building_StorageApparelAdapter()
        { contents = new ThingOwner<Thing>(this, false, LookMode.Deep) { dontTickContents = true }; }
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true;
        public bool ApparelSourceEnabled => Spawned;
        public bool IsContentsSuspended => true;
        internal bool HasApparel => contents.Count > 0;
        internal bool HasPrepared(Pawn pawn, Apparel apparel) => recipient == pawn && contents.Contains(apparel);
        internal ThingOwner Contents => contents;
        public ThingOwner GetDirectlyHeldThings() => contents;
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, contents);
        public bool RemoveApparel(Apparel apparel)
        {
            bool removed = contents.Remove(apparel);
            if (contents.Count == 0) recipient = null;
            return removed;
        }
        internal void PrepareFor(Pawn pawn) { recipient = pawn; }

        internal void ReturnApparel()
        {
            if (!HasApparel || !Spawned) return;
            var manager = Map.GetComponent<MapComponent_StorageNetworks>();
            manager.EnsureCurrent();
            var recovery = manager.CreateRecovery(Position);
            try
            {
                foreach (Thing item in new List<Thing>(contents))
                {
                    if (CanWork) Network.TryStore(contents, item, item.stackCount);
                    if (!item.Destroyed && contents.Contains(item))
                        StorageTransfer.Move(contents, recovery.Contents, item, item.stackCount, Map, Position, false);
                }
                if (!HasApparel) recipient = null;
            }
            finally { manager.ReleaseRecovery(recovery); }
        }
        public override void TickRare()
        {
            base.TickRare();
            if (!HasApparel) { recipient = null; return; }
            // Wear is vanilla and may be interrupted without calling our fetch driver.
            // Keep its garment while that exact job is active; otherwise return it.
            if (recipient?.Spawned == true && recipient.Map == Map && recipient.jobs?.curDriver?.ended == false &&
                recipient.CurJob?.def == JobDefOf.Wear &&
                HasPrepared(recipient, recipient.CurJob.GetTarget(Verse.AI.TargetIndex.A).Thing as Apparel)) return;
            ReturnApparel();
        }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            var manager = Map.GetComponent<MapComponent_StorageNetworks>();
            manager.NotifyNodeUnavailable(this);
            ReturnApparel();
            // A rejected return must not remain in a despawned holder.
            if (HasApparel) { Log.Error("[MagicStorage] Apparel adapter removal stopped: garment could not be released."); return; }
            base.DeSpawn(mode);
        }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Spawned)
            {
                Map.GetComponent<MapComponent_StorageNetworks>().NotifyNodeUnavailable(this);
                ReturnApparel();
            }
            if (HasApparel)
            { Log.Error("[MagicStorage] Apparel adapter destruction stopped: garment could not be released."); return; }
            base.Destroy(mode);
        }
        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            if (Spawned && newFaction != Faction) ReturnApparel();
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref contents, "preparedApparel", this);
            Scribe_References.Look(ref recipient, "apparelRecipient");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) contents.dontTickContents = true;
        }
    }
}
