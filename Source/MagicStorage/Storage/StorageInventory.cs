using System;
using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    // Only contents is saved. Both indexes can always be rebuilt from the real stacks.
    public sealed class StorageInventory : IExposable
    {
        private readonly Building_StorageUnit unit;
        private StorageThingOwner contents;
        private readonly Dictionary<ThingDef, List<Thing>> byDef = new Dictionary<ThingDef, List<Thing>>();
        private readonly Dictionary<ThingDef, long> counts = new Dictionary<ThingDef, long>();

        public StorageInventory(Building_StorageUnit unit)
        {
            this.unit = unit;
            contents = new StorageThingOwner(unit);
        }

        internal StorageThingOwner Contents => contents;
        public int UsedSlots => contents.Count;
        public int SlotCapacity => unit.SlotCapacity;
        internal IEnumerable<ThingDef> IndexedDefs => counts.Keys;

        public long GetTotalCount(ThingDef def)
        {
            return def != null && counts.TryGetValue(def, out long value) ? value : 0;
        }

        // Copies references into caller-owned storage; never expose a mutable index list.
        public void GetStacks(ThingDef def, List<Thing> result)
        {
            if (def != null && byDef.TryGetValue(def, out List<Thing> stacks))
                result.AddRange(stacks);
        }

        internal void RefreshDef(ThingDef def)
        {
            if (def == null) return;
            List<Thing> stacks = new List<Thing>();
            long count = 0;
            for (int i = 0; i < contents.Count; i++)
            {
                Thing thing = contents[i];
                if (thing.def == def && !thing.Destroyed)
                {
                    stacks.Add(thing);
                    count += thing.stackCount;
                }
            }
            if (stacks.Count == 0)
            {
                byDef.Remove(def);
                counts.Remove(def);
            }
            else
            {
                byDef[def] = stacks;
                counts[def] = count;
            }
            unit.Network?.NotifyInventoryChanged(unit, def);
        }

        internal void RebuildIndex()
        {
            byDef.Clear();
            counts.Clear();
            for (int i = 0; i < contents.Count; i++)
            {
                Thing thing = contents[i];
                if (thing == null || thing.Destroyed) continue;
                if (!byDef.TryGetValue(thing.def, out List<Thing> stacks))
                    byDef.Add(thing.def, stacks = new List<Thing>());
                stacks.Add(thing);
                counts[thing.def] = GetTotalCount(thing.def) + thing.stackCount;
            }
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref contents, "contents", unit);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (contents == null) contents = new StorageThingOwner(unit);
                contents.dontTickContents = true;
                RebuildIndex();
            }
        }
    }

    // Capacity is enforced at the container boundary as well as at the network API.
    public sealed class StorageThingOwner : ThingOwner<Thing>
    {
        private readonly Building_StorageUnit unit;

        public StorageThingOwner(Building_StorageUnit unit) : base(unit, false, LookMode.Deep)
        {
            this.unit = unit;
            dontTickContents = true;
        }

        public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true)
        {
            if (item == null || item.Destroyed || item.stackCount <= 0 ||
                item.def.category != ThingCategory.Item || item.def.destroyOnDrop) return 0;
            maxStacks = unit.SlotCapacity;
            return base.GetCountCanAccept(item, canMergeWithExistingStacks);
        }

        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
        {
            if (item == null || item.Spawned || item.stackCount > item.def.stackLimit ||
                GetCountCanAccept(item, canMergeWithExistingStacks) < item.stackCount) return false;
            try { return base.TryAdd(item, canMergeWithExistingStacks); }
            finally { unit.Inventory.RefreshDef(item.def); }
        }

        public override int TryAdd(Thing item, int count, bool canMergeWithExistingStacks = true)
        {
            if (item == null || item.Spawned) return 0;
            int accepted = Math.Min(count, Math.Min(item.def.stackLimit, GetCountCanAccept(item, canMergeWithExistingStacks)));
            try { return base.TryAdd(item, accepted, canMergeWithExistingStacks); }
            finally { unit.Inventory.RefreshDef(item.def); }
        }

        protected override void NotifyAdded(Thing item)
        {
            base.NotifyAdded(item);
            unit.Inventory.RefreshDef(item.def);
        }

        protected override void NotifyRemoved(Thing item)
        {
            base.NotifyRemoved(item);
            unit.Inventory.RefreshDef(item.def);
        }
    }
}
