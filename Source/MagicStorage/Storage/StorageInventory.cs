using System;
using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    // Only contents is saved. Derived indexes can always be rebuilt from the real stacks.
    public sealed class StorageInventory : IExposable
    {
        private readonly Building_StorageUnit unit;
        private StorageThingOwner contents;
        private readonly Dictionary<ThingDef, List<Thing>> byDef = new Dictionary<ThingDef, List<Thing>>();
        private readonly Dictionary<ThingDef, List<Thing>> partialStacks = new Dictionary<ThingDef, List<Thing>>();
        private readonly Dictionary<ThingDef, long> counts = new Dictionary<ThingDef, long>();
        private sealed class StackRecord
        { internal ThingDef Def; internal int Count, Index; internal int PartialIndex = -1; }
        private readonly Dictionary<Thing, StackRecord> indexed = new Dictionary<Thing, StackRecord>();

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

        internal Thing StackAt(ThingDef def, int index) => byDef.TryGetValue(def, out var stacks) &&
            index >= 0 && index < stacks.Count ? stacks[index] : null;

        // Copy only candidates. Merging changes this index as stacks become full.
        internal void GetPartialStacks(ThingDef def, List<Thing> result)
        {
            if (def != null && partialStacks.TryGetValue(def, out var stacks)) result.AddRange(stacks);
        }

        internal int GetCountCanMerge(Thing item, int requested)
        {
            if (requested <= 0 || !partialStacks.TryGetValue(item.def, out var stacks)) return 0;
            int remaining = requested;
            foreach (Thing stack in stacks)
            {
                if (!stack.CanStackWith(item)) continue;
                remaining -= Math.Min(remaining, Math.Max(0, stack.def.stackLimit - stack.stackCount));
                if (remaining == 0) break;
            }
            return requested - remaining;
        }

        private void RemovePartial(StackRecord record)
        {
            if (record.PartialIndex < 0) return;
            var stacks = partialStacks[record.Def];
            Thing last = stacks[stacks.Count - 1];
            stacks[record.PartialIndex] = last;
            indexed[last].PartialIndex = record.PartialIndex;
            stacks.RemoveAt(stacks.Count - 1);
            if (stacks.Count == 0) partialStacks.Remove(record.Def);
            record.PartialIndex = -1;
        }

        private void RefreshPartial(Thing item, StackRecord record)
        {
            if (item.stackCount >= item.def.stackLimit) { RemovePartial(record); return; }
            if (record.PartialIndex >= 0) return;
            if (!partialStacks.TryGetValue(record.Def, out var stacks))
                partialStacks.Add(record.Def, stacks = new List<Thing>());
            record.PartialIndex = stacks.Count;
            stacks.Add(item);
        }

        // Read-only traversal can stop inside a bucket without copying its remaining stacks.
        internal bool VisitStacks(ThingDef def, Predicate<Thing> visit)
        {
            if (def != null && byDef.TryGetValue(def, out List<Thing> stacks))
                foreach (Thing stack in stacks) if (!visit(stack)) return false;
            return true;
        }

        internal void RefreshStack(Thing item)
        {
            if (item == null) return;
            indexed.TryGetValue(item, out var record);
            bool held = !item.Destroyed && item.stackCount > 0 && item.holdingOwner == contents;
            if (record != null && (!held || record.Def != item.def))
            {
                RemovePartial(record);
                var stacks = byDef[record.Def];
                Thing last = stacks[stacks.Count - 1];
                stacks[record.Index] = last;
                indexed[last].Index = record.Index;
                stacks.RemoveAt(stacks.Count - 1);
                if (stacks.Count == 0) byDef.Remove(record.Def);
                indexed.Remove(item);
                ChangeCount(item, record.Def, -record.Count);
                record = null;
            }
            if (!held) return;
            if (record == null)
            {
                if (!byDef.TryGetValue(item.def, out var stacks))
                    byDef.Add(item.def, stacks = new List<Thing>());
                record = new StackRecord { Def = item.def, Index = stacks.Count };
                indexed.Add(item, record);
                stacks.Add(item);
            }
            int delta = item.stackCount - record.Count;
            record.Count = item.stackCount;
            RefreshPartial(item, record);
            if (delta != 0) ChangeCount(item, record.Def, delta);
        }

        private void ChangeCount(Thing item, ThingDef def, int delta)
        {
            long total = GetTotalCount(def) + delta;
            if (total == 0) counts.Remove(def);
            else counts[def] = total;
            unit.Network?.NotifyInventoryChanged(unit, def, delta, item);
        }

        internal void RebuildIndex()
        {
            byDef.Clear();
            partialStacks.Clear();
            counts.Clear();
            indexed.Clear();
            for (int i = 0; i < contents.Count; i++)
            {
                Thing thing = contents[i];
                if (thing == null || thing.Destroyed || thing.stackCount <= 0) continue;
                if (!byDef.TryGetValue(thing.def, out List<Thing> stacks))
                    byDef.Add(thing.def, stacks = new List<Thing>());
                stacks.Add(thing);
                var record = new StackRecord { Def = thing.def, Count = thing.stackCount, Index = stacks.Count - 1 };
                indexed.Add(thing, record);
                RefreshPartial(thing, record);
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
                item.def.category != ThingCategory.Item || item.def.destroyOnDrop || item.def.stackLimit <= 0) return 0;
            maxStacks = unit.SlotCapacity;
            long space = (long)Math.Max(0, maxStacks - Count) * item.def.stackLimit;
            if (space >= item.stackCount) return item.stackCount;
            if (canMergeWithExistingStacks)
                space += unit.Inventory.GetCountCanMerge(item, (int)(item.stackCount - space));
            return (int)Math.Min(item.stackCount, space);
        }

        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
        {
            if (item == null || item.Spawned || item.stackCount > item.def.stackLimit ||
                GetCountCanAccept(item, canMergeWithExistingStacks) < item.stackCount) return false;
            if (item.holdingOwner != null) return false;
            if (canMergeWithExistingStacks)
            {
                var stacks = new List<Thing>();
                unit.Inventory.GetPartialStacks(item.def, stacks);
                foreach (Thing stack in stacks)
                {
                    if (!stack.CanStackWith(item)) continue;
                    try { stack.TryAbsorbStack(item, true); }
                    finally { unit.Inventory.RefreshStack(stack); }
                    if (item.Destroyed || item.stackCount <= 0) return true;
                }
            }
            return base.TryAdd(item, false);
        }

        public override int TryAdd(Thing item, int count, bool canMergeWithExistingStacks = true)
        {
            if (item == null || item.Spawned) return 0;
            int accepted = Math.Min(count, Math.Min(item.def.stackLimit, GetCountCanAccept(item, canMergeWithExistingStacks)));
            try { return base.TryAdd(item, accepted, canMergeWithExistingStacks); }
            finally { unit.Inventory.RefreshStack(item); }
        }

        protected override void NotifyAdded(Thing item)
        {
            base.NotifyAdded(item);
            unit.Inventory.RefreshStack(item);
        }

        protected override void NotifyRemoved(Thing item)
        {
            base.NotifyRemoved(item);
            unit.Inventory.RefreshStack(item);
        }
    }
}
