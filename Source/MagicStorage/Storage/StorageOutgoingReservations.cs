using System;
using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    // Claims refer to real stacks; ownership stays in the unit until collection.
    internal sealed class StorageOutgoingReservations
    {
        private sealed class Claim { internal Thing Item; internal int Count; }
        private readonly Dictionary<object, Claim> claims = new Dictionary<object, Claim>();
        internal void Clear() => claims.Clear();
        internal void Release(object owner) { if (owner != null) claims.Remove(owner); }
        internal int CountFor(object owner, Thing item) => owner != null && claims.TryGetValue(owner, out var claim) && claim.Item == item ? claim.Count : 0;
        internal int Available(Thing item, object except = null)
        {
            if (item == null || item.Destroyed) return 0;
            long count = item.stackCount;
            foreach (var pair in claims)
                if (!ReferenceEquals(pair.Key, except) && pair.Value.Item == item) count -= pair.Value.Count;
            return (int)Math.Max(0L, count);
        }
        internal bool IsReserved(Thing item) => Available(item) < (item?.stackCount ?? 0);
        internal int Reserve(object owner, Thing item, int requested)
        {
            if (owner == null) return 0;
            int count = Math.Min(requested, Available(item, owner));
            if (count <= 0) { Release(owner); return 0; }
            claims[owner] = new Claim { Item = item, Count = count };
            return count;
        }
    }
}
