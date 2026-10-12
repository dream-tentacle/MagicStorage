using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class MagicStorageMod : Mod
    {
        internal static MagicStorageSettings Settings { get; private set; } = new MagicStorageSettings();
        private string unitSlotsBuffer;

        public MagicStorageMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MagicStorageSettings>();
            unitSlotsBuffer = Settings.StorageUnitSlots.ToString();
            var assembly = typeof(MagicStorageMod).Assembly;
            string build = assembly.ManifestModule.ModuleVersionId.ToString("N");
            Log.Message("[MagicStorage] Storage receivers use native hauling. Build=" + build);
        }

        public override string SettingsCategory() => "MS_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            int slots = Settings.StorageUnitSlots;
            listing.TextFieldNumericLabeled("MS_SettingsUnitSlots".Translate(), ref slots, ref unitSlotsBuffer,
                MagicStorageSettings.MinUnitSlots, MagicStorageSettings.MaxUnitSlots);
            Settings.StorageUnitSlots = slots;
            listing.Label("MS_SettingsUnitSlotsHelp".Translate());
            if (listing.ButtonText("MS_SettingsRestoreDefaults".Translate()))
            {
                Settings.StorageUnitSlots = MagicStorageSettings.DefaultUnitSlots;
                unitSlotsBuffer = Settings.StorageUnitSlots.ToString();
            }
            listing.End();
        }
    }
}
