using System;
using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal sealed class StorageItemRow
    {
        internal readonly List<Thing> Stacks = new List<Thing>();
        internal Thing Representative => Stacks[0];
        internal Thing DisplayThing => Representative.GetInnerIfMinified();
        internal long Total, Available, Requested;
        internal string Buffer = "0";

        internal void Recount(StorageNetwork network)
        {
            Total = Available = 0;
            foreach (Thing item in Stacks)
            {
                if (item.Destroyed || !(item.holdingOwner?.Owner is Building_StorageUnit unit) || unit.Network != network) continue;
                Total += item.stackCount;
                Available += network.AvailableToWithdraw(item);
            }
        }

        internal void SetRequested(long value)
        { Requested = Math.Max(0, Math.Min(value, Available)); Buffer = (-Requested).ToString(CultureInfo.InvariantCulture); }

        // Negative quantities follow trade's sell direction. Keep long counts exact.
        internal void EditRequested(string text)
        {
            if (text == "" || text == "-") { Buffer = text; Requested = 0; return; }
            if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
                SetRequested(value == long.MinValue ? long.MaxValue : value < 0 ? -value : 0);
        }
    }

    internal struct StorageWithdrawalResult
    {
        internal long Requested, Withdrawn, Pending;
    }

    // The window keeps references to real stacks, never just def/count orders.
    internal static class StorageWithdrawal
    {
        internal static List<StorageItemRow> Snapshot(StorageNetwork network)
        {
            var result = new List<StorageItemRow>();
            if (network == null) return result;
            var stacks = new List<Thing>();
            network.GetAllStacks(stacks);
            var byDef = new Dictionary<ThingDef, List<StorageItemRow>>();
            foreach (Thing item in stacks)
            {
                if (!byDef.TryGetValue(item.def, out var candidates))
                    byDef.Add(item.def, candidates = new List<StorageItemRow>());
                StorageItemRow row = null;
                // Match the active trade window's grouping, including food kinds, quality,
                // minified items and mod-provided CanStackWith rules. Physical stack limits
                // do not limit a display row. No trade session or trade UI is constructed.
                foreach (var candidate in candidates)
                    if (TransferableUtility.TransferAsOne(item, candidate.Representative, TransferAsOneMode.Normal))
                    { row = candidate; break; }
                if (row == null) { row = new StorageItemRow(); candidates.Add(row); result.Add(row); }
                row.Stacks.Add(item);
            }
            foreach (var row in result) row.Recount(network);
            return result;
        }

        internal static StorageWithdrawalResult Execute(Building_StorageCore core, StorageNetwork expected,
            IList<StorageItemRow> rows, bool forbid)
        {
            var result = new StorageWithdrawalResult();
            foreach (var row in rows) result.Requested += Math.Max(0, row.Requested);
            if (result.Requested == 0 || core == null || !core.CanWork || core.Network != expected) return result;
            StorageRecoveryBatch batch = core.Map.GetComponent<MapComponent_StorageNetworks>().CreateRecovery(core.Position);
            batch.ConfigureWithdrawal(forbid);
            object claim = new object();
            foreach (var row in rows)
            {
                long remaining = Math.Max(0, row.Requested);
                foreach (Thing item in row.Stacks)
                {
                    if (remaining == 0 || !core.CanWork || core.Network != expected) break;
                    int amount = (int)Math.Min(remaining, expected.AvailableToWithdraw(item));
                    if (amount <= 0) continue;
                    int reserved = expected.ReserveOutgoing(claim, item, amount);
                    if (reserved <= 0) continue;
                    var moved = expected.TryWithdrawReserved(claim, item, reserved, batch.Contents, out _);
                    result.Withdrawn += moved.Transferred;
                    remaining -= moved.Transferred;
                }
            }
            core.Map.GetComponent<MapComponent_StorageNetworks>().ReleaseRecovery(batch);
            result.Pending = batch.PendingCount;
            return result;
        }
    }
}
