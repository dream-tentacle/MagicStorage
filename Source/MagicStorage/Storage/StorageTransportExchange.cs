using System;
using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal sealed class StorageTransportRow : TransferableOneWay
    {
        internal readonly List<Thing> NetworkStacks = new List<Thing>();
        internal readonly List<Thing> SupplyStacks = new List<Thing>();
        internal long NetworkCount, Available, SupplyCount, Change;
        internal string Buffer = "0";
        internal Thing Representative => NetworkStacks.Count > 0 ? NetworkStacks[0] : SupplyStacks[0];
        public override Thing AnyThing => Representative.GetInnerIfMinified();
        internal long NetworkAfter => NetworkCount - Change;
        internal long SupplyAfter => SupplyCount + Change;

        internal void SetChange(long value)
        {
            Change = Math.Max(-SupplyCount, Math.Min(value, Available));
            Buffer = Change.ToString(CultureInfo.InvariantCulture);
        }

        internal void EditChange(string value)
        {
            if (value == "" || value == "-" || value == "+") { Buffer = value; Change = 0; return; }
            if (long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long parsed)) SetChange(parsed);
        }
    }

    internal static class StorageTransportExchange
    {
        // Small tolerance covers float mass stats without allowing another whole item.
        internal const double MassTolerance = 0.00001;
        internal static double UnitMass(Thing thing) => Math.Max(0, (double)thing.GetStatValue(StatDefOf.Mass));
        internal static double ContainerMass(ThingOwner contents)
        {
            double mass = 0;
            foreach (Thing item in contents) mass += UnitMass(item) * item.stackCount;
            return mass;
        }

        internal static int CountThatFits(Thing item, double freeMass)
        {
            if (double.IsNaN(freeMass) || freeMass < 0) return 0;
            double mass = UnitMass(item);
            if (double.IsNaN(mass) || double.IsInfinity(mass)) return 0;
            if (mass == 0 || double.IsPositiveInfinity(freeMass)) return item.stackCount;
            double tolerance = Math.Min(MassTolerance, mass * 0.000001);
            return (int)Math.Min(item.stackCount, Math.Floor((freeMass + tolerance) / mass));
        }

        internal static List<StorageTransportRow> Snapshot(Building_TransportSupplier supplier, StorageNetwork network)
        {
            var rows = new List<StorageTransportRow>();
            var byDef = new Dictionary<ThingDef, List<StorageTransportRow>>();
            var stacks = new List<Thing>();
            network?.GetAllStacks(stacks);
            foreach (Thing item in stacks) Add(rows, byDef, item, true, network);
            foreach (Thing item in supplier.Contents) Add(rows, byDef, item, false, network);
            return rows;
        }

        private static void Add(List<StorageTransportRow> rows, Dictionary<ThingDef, List<StorageTransportRow>> byDef,
            Thing item, bool fromNetwork, StorageNetwork network)
        {
            if (!byDef.TryGetValue(item.def, out var candidates)) byDef.Add(item.def, candidates = new List<StorageTransportRow>());
            StorageTransportRow row = null;
            foreach (var candidate in candidates)
                if (TransferableUtility.TransferAsOne(item, candidate.Representative, TransferAsOneMode.Normal)) { row = candidate; break; }
            if (row == null) { row = new StorageTransportRow(); rows.Add(row); candidates.Add(row); }
            row.things.Add(item);
            if (fromNetwork)
            {
                row.NetworkStacks.Add(item); row.NetworkCount += item.stackCount;
                row.Available += network.AvailableToWithdraw(item);
            }
            else { row.SupplyStacks.Add(item); row.SupplyCount += item.stackCount; }
        }

        // Use each actual stack's stat; native grouping can combine items with different mass.
        internal static double ProjectedMass(Building_TransportSupplier supplier, StorageNetwork network,
            IList<StorageTransportRow> rows, StorageTransportRow excludedRow = null)
        {
            double mass = supplier.StoredMass;
            foreach (var row in rows)
            {
                if (row == excludedRow || row.Change == 0) continue;
                double changeMass = ChangeMass(supplier, network, row, row.Change);
                if (double.IsPositiveInfinity(changeMass)) return changeMass;
                mass += changeMass;
            }
            return Math.Max(0, mass);
        }

        private static double ChangeMass(Building_TransportSupplier supplier, StorageNetwork network,
            StorageTransportRow row, long change)
        {
            if (change == 0) return 0;
            double mass = 0;
            long remaining = change >= 0 ? change : -change;
            foreach (Thing item in change >= 0 ? row.NetworkStacks : row.SupplyStacks)
            {
                long available = change >= 0 ? network?.AvailableToWithdraw(item) ?? 0 :
                    supplier.Contents.Contains(item) && !item.Destroyed ? item.stackCount : 0;
                long count = Math.Min(remaining, available);
                if (count > 0) mass += (change >= 0 ? 1 : -1) * UnitMass(item) * count;
                remaining -= count;
                if (remaining == 0) return mass;
            }
            return double.PositiveInfinity;
        }

        // The mass stopping point includes existing cargo and every other row's selection.
        // Search actual stack masses in the same order used when executing the exchange.
        internal static long MassLimitChange(Building_TransportSupplier supplier, StorageNetwork network,
            IList<StorageTransportRow> rows, StorageTransportRow row, double capacity)
        {
            double otherMass = ProjectedMass(supplier, network, rows, row);
            double tolerance = MassTolerance;
            foreach (Thing item in row.NetworkStacks)
            {
                double mass = UnitMass(item);
                if (mass > 0) tolerance = Math.Min(tolerance, mass * 0.000001);
            }
            foreach (Thing item in row.SupplyStacks)
            {
                double mass = UnitMass(item);
                if (mass > 0) tolerance = Math.Min(tolerance, mass * 0.000001);
            }
            long lower = -row.SupplyCount, upper = row.Available;
            while (lower < upper)
            {
                long distance = upper - lower;
                long middle = lower + distance / 2 + distance % 2;
                double mass = otherMass + ChangeMass(supplier, network, row, middle);
                if (mass <= capacity + tolerance) lower = middle;
                else upper = middle - 1;
            }
            return lower;
        }

        private sealed class Move
        {
            internal Thing Item, Received;
            internal ThingOwner Source;
            internal int Count;
            internal bool ToSupplier;
        }

        internal static bool Execute(Building_TransportSupplier supplier, StorageNetwork expected,
            IList<StorageTransportRow> rows, double capacity, out string error)
        {
            error = "MS_Transport_Unavailable";
            if (!supplier.Spawned || supplier.DirectSupply) return false;
            var manager = supplier.Map.GetComponent<MapComponent_StorageNetworks>();
            manager.EnsureCurrent();
            if (!supplier.CanWork || supplier.Network != expected) return false;
            if (!(ProjectedMass(supplier, expected, rows) <= capacity + MassTolerance))
            { error = "MS_Transport_Overweight"; return false; }
            var moves = new List<Move>();
            var seen = new HashSet<Thing>();
            foreach (var row in rows)
            {
                long remaining = row.Change >= 0 ? row.Change : -row.Change;
                foreach (Thing item in row.Change >= 0 ? row.NetworkStacks : row.SupplyStacks)
                {
                    int available = row.Change >= 0 ? expected.AvailableToWithdraw(item) :
                        supplier.Contents.Contains(item) && !item.Destroyed ? item.stackCount : 0;
                    int count = (int)Math.Min(remaining, available);
                    if (count > 0)
                    {
                        if (!seen.Add(item)) return false;
                        moves.Add(new Move { Item = item, Source = item.holdingOwner, Count = count, ToSupplier = row.Change >= 0 });
                        remaining -= count;
                    }
                    if (remaining == 0) break;
                }
                if (remaining != 0) return false;
            }
            StorageRecoveryBatch staging = manager.CreateRecovery(supplier.Position);
            try
            {
                // Reserve every withdrawal before collecting any of them.
                foreach (var move in moves)
                    if (move.ToSupplier && expected.ReserveOutgoing(move, move.Item, move.Count, supplier) != move.Count) return false;
                foreach (var move in moves)
                {
                    if (!move.ToSupplier) continue;
                    if (!expected.TryWithdrawReserved(move, move.Item, move.Count, staging.Contents, out move.Received).Complete) return false;
                }
                // Withdrawals can free slots for returns. Reserve all returns together before storing any.
                foreach (var move in moves)
                    if (!move.ToSupplier && expected.ReserveIncoming(move, move.Item, move.Count) != move.Count)
                    { error = "MS_Transport_NetworkCannotAccept"; return false; }
                foreach (var move in moves)
                {
                    if (move.ToSupplier) continue;
                    if (!expected.TryStoreReserved(supplier.Contents, move.Item, move.Count, move).Complete) return false;
                }
                if (!supplier.CanWork || supplier.Network != expected ||
                    supplier.StoredMass + ContainerMass(staging.Contents) > capacity + MassTolerance) return false;
                foreach (var move in moves)
                {
                    if (!move.ToSupplier || move.Received == null) continue;
                    if (StorageTransfer.Move(staging.Contents, supplier.Contents, move.Received, move.Received.stackCount,
                        supplier.Map, supplier.Position) != move.Count) return false;
                }
                return true;
            }
            finally
            {
                foreach (var move in moves) { expected.ReleaseIncoming(move); expected.ReleaseOutgoing(move); }
                // On a rejected exchange, restore collected stacks directly to their original owners.
                // Any exceptional failed restoration stays in the map's saved recovery buffer.
                foreach (var move in moves)
                    if (move.Received != null && staging.Contents.Contains(move.Received))
                        StorageTransfer.Move(staging.Contents, move.Source, move.Received, move.Received.stackCount,
                            supplier.Map, supplier.Position);
                manager.ReleaseRecovery(staging);
            }
        }
    }
}
