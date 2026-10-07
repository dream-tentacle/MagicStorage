using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Saved delivery buffer for destruction, rollback and confirmed manual withdrawals.
    public sealed class StorageRecoveryBatch : IExposable, IThingHolder, ISuspendableThingHolder
    {
        private readonly MapComponent_StorageNetworks owner;
        private ThingOwner<Thing> contents;
        private IntVec3 origin;
        private bool manualWithdrawal;
        private bool forbidOnRelease;
        private IntVec3 excludedCell = IntVec3.Invalid;
        internal ThingOwner Contents => contents;
        public bool HasContents => contents != null && contents.Count > 0;
        public bool IsContentsSuspended => true;
        public IThingHolder ParentHolder => owner;

        internal void ConfigureWithdrawal(bool forbid)
        { manualWithdrawal = true; forbidOnRelease = forbid; }

        internal void ExcludeCell(IntVec3 cell) { excludedCell = cell; }

        internal long PendingCount
        {
            get { long total = 0; for (int i = 0; i < contents.Count; i++) total += contents[i].stackCount; return total; }
        }

        public StorageRecoveryBatch(MapComponent_StorageNetworks owner)
        {
            this.owner = owner;
            contents = new ThingOwner<Thing>(this, false, LookMode.Deep) { dontTickContents = true };
        }

        public StorageRecoveryBatch(MapComponent_StorageNetworks owner, IntVec3 origin) : this(owner)
        { this.origin = origin; }

        public ThingOwner GetDirectlyHeldThings() => contents;
        public void GetChildHolders(List<IThingHolder> children)
        { ThingOwnerUtility.AppendThingHoldersFromThings(children, contents); }

        internal void TryRelease(Map map, int budget)
        {
            if (!origin.InBounds(map)) return;
            for (int i = contents.Count - 1; i >= 0 && budget-- > 0; i--)
            {
                Thing item = contents[i];
                if (!manualWithdrawal)
                    contents.TryDrop(item, origin, map, ThingPlaceMode.Near, out _,
                        nearPlaceValidator: cell => cell != excludedCell, playDropSound: false);
                else
                {
                    item.SetForbidden(forbidOnRelease, false);
                    contents.TryDrop(item, origin, map, ThingPlaceMode.Near, out _,
                        placedAction: (placed, count) => placed.SetForbidden(forbidOnRelease, false),
                        nearPlaceValidator: cell => cell != excludedCell && CanReleaseAt(item, cell, map), playDropSound: false);
                }
            }
        }

        private bool CanReleaseAt(Thing item, IntVec3 cell, Map map)
        {
            // Forbiddable does not participate in CanStackWith. Do not change existing stacks' flags.
            foreach (Thing other in cell.GetThingList(map))
                if (other.CanStackWith(item) &&
                    (other.TryGetComp<CompForbiddable>()?.Forbidden ?? false) != forbidOnRelease) return false;
            return true;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Values.Look(ref manualWithdrawal, "manualWithdrawal", false);
            Scribe_Values.Look(ref forbidOnRelease, "forbidOnRelease", false);
            Scribe_Values.Look(ref excludedCell, "excludedCell", IntVec3.Invalid);
            Scribe_Deep.Look(ref contents, "contents", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (contents == null) contents = new ThingOwner<Thing>(this, false, LookMode.Deep);
                contents.dontTickContents = true;
            }
        }
    }
}
