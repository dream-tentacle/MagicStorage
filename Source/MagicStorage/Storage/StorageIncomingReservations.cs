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
        private readonly Dictionary<Thing, long> reservedStacks = new Dictionary<Thing, long>();
        private readonly Dictionary<Building_StorageUnit, int> reservedSlots = new Dictionary<Building_StorageUnit, int>();

        internal StorageIncomingReservations(IList<Building_StorageUnit> units, Func<Thing, bool> unavailableStack = null)
        { this.units = units; this.unavailableStack = unavailableStack; }
        internal void Clear() { claims.Clear(); reservedStacks.Clear(); reservedSlots.Clear(); }
        internal void Release(object owner)
        {
            if (owner == null || !claims.TryGetValue(owner, out var plan)) return;
            ChangeIndex(plan, -1);
            claims.Remove(owner);
        }
        internal int CountFor(object owner)
        { return owner != null && claims.TryGetValue(owner, out var plan) ? plan.Count : 0; }

        internal long ReservedOnStack(Thing stack) => stack != null && reservedStacks.TryGetValue(stack, out long count) ? count : 0;
        internal int ReservedSlots(Building_StorageUnit unit) => reservedSlots.TryGetValue(unit, out int count) ? count : 0;
        internal bool IsStackReserved(Thing stack) => ReservedOnStack(stack) > 0;

        private void ChangeIndex(StorageIncomingPlan plan, int direction)
        {
            foreach (var allocation in plan.Allocations)
                if (allocation.Stack == null)
                {
                    int count = ReservedSlots(allocation.Unit) + direction;
                    if (count == 0) reservedSlots.Remove(allocation.Unit);
                    else reservedSlots[allocation.Unit] = count;
                }
                else
                {
                    long count = ReservedOnStack(allocation.Stack) + (long)direction * allocation.Count;
                    if (count == 0) reservedStacks.Remove(allocation.Stack);
                    else reservedStacks[allocation.Stack] = count;
                }
        }

        internal StorageIncomingPlan Reserve(object owner, Thing item, int count)
        {
            StorageIncomingPlan plan = Plan(item, count, owner);
            Release(owner);
            if (plan.Count > 0) { claims.Add(owner, plan); ChangeIndex(plan, 1); }
            return plan;
        }

        internal StorageIncomingPlan Plan(Thing item, int requested, object exclude = null)
        {
            StorageIncomingPlan result = new StorageIncomingPlan();
            if (item == null || item.Destroyed || item.def.category != ThingCategory.Item ||
                item.def.destroyOnDrop || item.def.stackLimit <= 0 || requested <= 0) return result;
            int remaining = Math.Min(requested, item.stackCount);
            // Replanning only subtracts this owner's old allocations. Other owners'
            // totals are maintained incrementally instead of rebuilt for every query.
            Dictionary<Thing, long> ownStacks = null;
            Dictionary<Building_StorageUnit, int> ownSlots = null;
            if (exclude != null && claims.TryGetValue(exclude, out var ownPlan))
            {
                ownStacks = new Dictionary<Thing, long>();
                ownSlots = new Dictionary<Building_StorageUnit, int>();
                foreach (var allocation in ownPlan.Allocations)
                {
                    if (allocation.Stack == null)
                    {
                        ownSlots.TryGetValue(allocation.Unit, out int slots);
                        ownSlots[allocation.Unit] = slots + 1;
                    }
                    else
                    {
                        ownStacks.TryGetValue(allocation.Stack, out long count);
                        ownStacks[allocation.Stack] = count + allocation.Count;
                    }
                }
            }

            // First allocate specific compatible partial stacks. Never infer compatibility from Def alone.
            List<Thing> stacks = new List<Thing>();
            foreach (var unit in units)
            {
                if (remaining == 0) return result;
                stacks.Clear();
                unit.Inventory.GetPartialStacks(item.def, stacks);
                foreach (var stack in stacks)
                {
                    long own = 0; ownStacks?.TryGetValue(stack, out own);
                    long reserved = ReservedOnStack(stack) - own;
                    int take = (int)Math.Min(remaining, Math.Max(0L, stack.def.stackLimit - (long)stack.stackCount - reserved));
                    if (take <= 0) continue;
                    if (unavailableStack?.Invoke(stack) == true || !stack.CanStackWith(item)) continue;
                    result.Add(unit, stack, take);
                    remaining -= take;
                    if (remaining == 0) return result;
                }
            }
            foreach (var unit in units)
            {
                int own = 0; ownSlots?.TryGetValue(unit, out own);
                int reserved = ReservedSlots(unit) - own;
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
