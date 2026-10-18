using System;
using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    // The window forces pause. Capture real stack masses once and recalculate selection
    // results only on edits; execution uses the live validation path in the exchange service.
    internal sealed class StorageTransportPreview : IDisposable
    {
        private readonly Building_TransportSupplier supplier;
        private readonly StorageNetwork network;
        private readonly IList<StorageTransportRow> rows;
        private readonly double capacity;
        private readonly Dictionary<Thing, double> masses = new Dictionary<Thing, double>();
        private readonly Dictionary<StorageTransportRow, long> limits = new Dictionary<StorageTransportRow, long>();
        private readonly Func<Thing, double> massOf;
        private bool resetting;
        internal double StoredMass { get; }
        internal double ProjectedMass { get; private set; }

        internal StorageTransportPreview(Building_TransportSupplier supplier, StorageNetwork network,
            IList<StorageTransportRow> rows, double capacity)
        {
            this.supplier = supplier; this.network = network; this.rows = rows; this.capacity = capacity;
            massOf = MassOf;
            foreach (var row in rows)
            {
                foreach (Thing item in row.NetworkStacks) MassOf(item);
                foreach (Thing item in row.SupplyStacks) MassOf(item);
                row.SelectionChanged += SelectionChanged;
            }
            double stored = 0;
            foreach (Thing item in supplier.Contents) stored += MassOf(item) * item.stackCount;
            StoredMass = stored;
            Recalculate();
        }

        private double MassOf(Thing item)
        {
            if (!masses.TryGetValue(item, out double mass))
                masses.Add(item, mass = StorageTransportExchange.UnitMass(item));
            return mass;
        }

        private void Recalculate()
        {
            ProjectedMass = StorageTransportExchange.ProjectedMass(supplier, network, rows, storedMass: StoredMass, massOf: massOf);
            limits.Clear();
        }

        internal long MassLimit(StorageTransportRow row)
        {
            if (!limits.TryGetValue(row, out long limit))
                limits.Add(row, limit = StorageTransportExchange.MassLimitChange(supplier, network, rows, row, capacity, StoredMass, massOf));
            return limit;
        }

        private void SelectionChanged() { if (!resetting) Recalculate(); }

        internal void ResetSelections()
        {
            resetting = true;
            try { foreach (var row in rows) row.SetChange(0); }
            finally { resetting = false; }
            Recalculate();
        }

        public void Dispose()
        {
            foreach (var row in rows) row.SelectionChanged -= SelectionChanged;
        }
    }
}
