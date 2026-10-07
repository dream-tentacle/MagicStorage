using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal enum StorageShelfFailureReason
    {
        None, NoStock, FilterMismatch, ReservedStock, IncompatibleStack,
        NetworkUnavailable, NoSpace, PlacementFailed, TransferFailed, Burning
    }

    // Runtime result of one restocking attempt. Settings are saved; transient failures are recomputed.
    internal sealed class StorageShelfRestockFailure
    {
        internal readonly Building_StorageSupplyShelf Shelf;
        internal readonly int Slot, Current, Target;
        internal readonly ThingDef ItemDef;
        internal readonly StorageShelfFailureReason Reason;
        private readonly StorageShelfFilter filter;
        private readonly QualityRange quality;
        private readonly FloatRange hitPoints;
        private readonly bool anyMaterial;
        private readonly List<ThingDef> materials;

        internal StorageShelfRestockFailure(Building_StorageSupplyShelf shelf, int slot, int current, StorageShelfFailureReason reason)
        {
            Shelf = shelf; Slot = slot; Current = current; Target = shelf.RestockLimit(slot);
            ItemDef = shelf.SelectedDef(slot); Reason = reason;
            filter = shelf.SlotFilter(slot); quality = filter.Quality; hitPoints = filter.HitPoints;
            anyMaterial = filter.AnyMaterial; materials = new List<ThingDef>(filter.Materials);
        }

        internal bool IsCurrent
        {
            get
            {
                if (!Shelf.Spawned || Shelf.Destroyed || Shelf.Faction != Faction.OfPlayer ||
                    Shelf.SelectedDef(Slot) != ItemDef || Shelf.RestockLimit(Slot) != Target || Target == 0 ||
                    Shelf.SlotFilter(Slot) != filter || !filter.WarnWhenRestockFails ||
                    filter.Quality.min != quality.min || filter.Quality.max != quality.max ||
                    filter.HitPoints.min != hitPoints.min || filter.HitPoints.max != hitPoints.max ||
                    filter.AnyMaterial != anyMaterial) return false;
                if (anyMaterial) return true;
                if (filter.Materials.Count != materials.Count) return false;
                for (int i = 0; i < materials.Count; i++) if (filter.Materials[i] != materials[i]) return false;
                return true;
            }
        }
    }
}
