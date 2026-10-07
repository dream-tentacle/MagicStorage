// Controlled placement boundary used by supply shelves and saved recovery batches.
using System;
namespace Verse
{
    public partial class ThingOwner
    {
        public bool rejectDrop;
        public bool TryDrop(Thing item, IntVec3 position, Map map, ThingPlaceMode mode, out Thing result,
            Action<Thing, int> placedAction = null, Predicate<IntVec3> nearPlaceValidator = null, bool playDropSound = true)
        {
            result = null;
            if (rejectDrop || !Contains(item)) return false;
            if (nearPlaceValidator != null && !nearPlaceValidator(position))
            {
                if (mode != ThingPlaceMode.Near || !nearPlaceValidator(position + new IntVec3(1, 0))) return false;
                position += new IntVec3(1, 0);
            }
            if (mode == ThingPlaceMode.Direct)
            {
                if (map.DropBudget < item.stackCount) return false;
                foreach (Thing existing in map.Ground)
                    if (existing.Spawned && existing.Position == position && existing.CanStackWith(item) &&
                        existing.def.stackLimit - existing.stackCount >= item.stackCount)
                    { map.DropBudget -= item.stackCount; existing.TryAbsorbStack(item, true); result = existing; return true; }
                if (map.Ground.FindAll(t => t.Spawned && t.Position == position).Count >= 3) return false;
            }
            int count = Math.Min(item.stackCount, map.DropBudget);
            if (count <= 0) return false;
            map.DropBudget -= count;
            bool complete = count == item.stackCount;
            Thing part = item.SplitOff(count);
            part.forbidden = item.forbidden;
            part.Map = map; part.Position = position; part.Spawned = true;
            map.Ground.Add(part); result = part; placedAction?.Invoke(part, count);
            return complete;
        }
    }
    public enum ThingPlaceMode { Near, Direct }
}
