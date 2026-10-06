using System;
using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    internal sealed class StorageAllocation
    {
        internal Building_StorageUnit Unit;
        internal Thing Stack; // null reserves one new slot, irrespective of item type.
        internal int Count;
    }

    internal sealed class StorageIncomingPlan
    {
        internal readonly List<StorageAllocation> Allocations = new List<StorageAllocation>();
        internal int Count;
        internal void Add(Building_StorageUnit unit, Thing stack, int count)
        {
            Allocations.Add(new StorageAllocation { Unit = unit, Stack = stack, Count = count });
            Count += count;
        }
    }

    // Runtime capacity claims, keyed by the owning job driver. No item is owned here.
    // Active jobs recreate their claims after loading and after topology changes.
    internal sealed class StorageIncomingReservations
    {
        private readonly IList<Building_StorageUnit> units;
        private readonly Func<Thing, bool> unavailableStack;
        private readonly Dictionary<object, StorageIncomingPlan> claims = new Dictionary<object, StorageIncomingPlan>();

        internal StorageIncomingReservations(IList<Building_StorageUnit> units, Func<Thing, bool> unavailableStack = null)
        { this.units = units; this.unavailableStack = unavailableStack; }
        internal void Clear() { claims.Clear(); }
        internal void Release(object owner) { if (owner != null) claims.Remove(owner); }
        internal int CountFor(object owner)
        { return owner != null && claims.TryGetValue(owner, out var plan) ? plan.Count : 0; }

        internal bool IsStackReserved(Thing stack)
        {
            foreach (var plan in claims.Values)
                foreach (var allocation in plan.Allocations)
                    if (allocation.Stack == stack) return true;
            return false;
        }

        internal StorageIncomingPlan Reserve(object owner, Thing item, int count)
        {
            StorageIncomingPlan plan = Plan(item, count, owner);
            if (plan.Count > 0) claims[owner] = plan;
            else Release(owner);
            return plan;
        }

        internal StorageIncomingPlan Plan(Thing item, int requested, object exclude = null)
        {
            StorageIncomingPlan result = new StorageIncomingPlan();
            if (item == null || item.Destroyed || item.def.category != ThingCategory.Item ||
                item.def.destroyOnDrop || item.def.stackLimit <= 0 || requested <= 0) return result;
            int remaining = Math.Min(requested, item.stackCount);
            var reservedStacks = new Dictionary<Thing, long>();
            var reservedSlots = new Dictionary<Building_StorageUnit, int>();
            foreach (var pair in claims)
            {
                if (ReferenceEquals(pair.Key, exclude)) continue;
                foreach (var allocation in pair.Value.Allocations)
                {
                    if (allocation.Stack == null)
                    {
                        reservedSlots.TryGetValue(allocation.Unit, out int slots);
                        reservedSlots[allocation.Unit] = slots + 1;
                    }
                    else
                    {
                        reservedStacks.TryGetValue(allocation.Stack, out long count);
                        reservedStacks[allocation.Stack] = count + allocation.Count;
                    }
                }
            }

            // First allocate specific compatible partial stacks. Never infer compatibility from Def alone.
            List<Thing> stacks = new List<Thing>();
            foreach (var unit in units)
            {
                stacks.Clear();
                unit.Inventory.GetStacks(item.def, stacks);
                foreach (var stack in stacks)
                {
                    if (remaining == 0) return result;
                    if (!stack.CanStackWith(item) || unavailableStack?.Invoke(stack) == true) continue;
                    reservedStacks.TryGetValue(stack, out long reserved);
                    int take = (int)Math.Min(remaining, Math.Max(0L, stack.def.stackLimit - (long)stack.stackCount - reserved));
                    if (take <= 0) continue;
                    result.Add(unit, stack, take);
                    remaining -= take;
                }
            }
            foreach (var unit in units)
            {
                reservedSlots.TryGetValue(unit, out int reserved);
                int slots = Math.Max(0, unit.SlotCapacity - unit.Inventory.UsedSlots - reserved);
                while (slots-- > 0 && remaining > 0)
                {
                    int take = Math.Min(remaining, item.def.stackLimit);
                    result.Add(unit, null, take);
                    remaining -= take;
                }
                if (remaining == 0) break;
            }
            return result;
        }
    }
}
