using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class Alert_StorageShelfRestockFailed : Alert
    {
        private readonly List<StorageShelfRestockFailure> failures = new List<StorageShelfRestockFailure>();
        private readonly List<Thing> culprits = new List<Thing>();

        public Alert_StorageShelfRestockFailed() { defaultPriority = AlertPriority.High; }

        public override AlertReport GetReport()
        {
            failures.Clear(); culprits.Clear();
            foreach (Map map in Find.Maps)
            {
                var manager = map.GetComponent<MapComponent_StorageNetworks>();
                if (manager == null) continue;
                foreach (Building_StorageSupplyShelf shelf in manager.SupplyShelves)
                {
                    bool added = false;
                    for (int slot = 0; slot < Building_StorageSupplyShelf.SlotCount; slot++)
                    {
                        StorageShelfRestockFailure failure = shelf.RestockFailure(slot);
                        if (failure == null) continue;
                        failures.Add(failure);
                        if (!added) { culprits.Add(shelf); added = true; }
                    }
                }
            }
            return AlertReport.CulpritsAre(culprits);
        }

        public override string GetLabel()
        {
            var defs = failures.Select(f => f.ItemDef).Distinct().OrderBy(d => d.label).ToList();
            if (defs.Count == 0) return "MS_Shelf_AlertTitle".Translate();
            if (defs.Count > 3) return "MS_Shelf_AlertMany".Translate();
            string names = string.Join("MS_Shelf_AlertSeparator".Translate().ToString(), defs.Select(d => d.LabelCap.ToString()));
            return "MS_Shelf_AlertOne".Translate(names);
        }

        public override TaggedString GetExplanation()
        {
            var text = new StringBuilder("MS_Shelf_AlertExplanation".Translate().ToString());
            foreach (StorageShelfRestockFailure failure in failures)
            {
                string reason = ("MS_Shelf_Failure_" + failure.Reason).Translate();
                text.Append("\n\n").Append("MS_Shelf_AlertDetail".Translate(failure.ItemDef.LabelCap,
                    failure.Slot + 1, failure.Current, failure.Target, failure.Target - failure.Current,
                    reason, failure.Shelf.LabelCap, failure.Shelf.Position, failure.Shelf.Map.Parent.LabelCap));
            }
            return text.ToString();
        }
    }
}
