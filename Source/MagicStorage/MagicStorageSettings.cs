using System;
using Verse;

namespace MagicStorage
{
    public sealed class MagicStorageSettings : ModSettings
    {
        public const int DefaultUnitSlots = 64;
        public const int MinUnitSlots = 6;
        public const int MaxUnitSlots = 1000;
        private int storageUnitSlots = DefaultUnitSlots;

        public int StorageUnitSlots
        {
            get => storageUnitSlots;
            set => storageUnitSlots = Math.Max(MinUnitSlots, Math.Min(MaxUnitSlots, value));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref storageUnitSlots, "storageUnitSlots", DefaultUnitSlots);
            StorageUnitSlots = storageUnitSlots;
        }
    }
}
