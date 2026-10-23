using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class MagicStorageMod : Mod
    {
        internal static MagicStorageSettings Settings { get; private set; } = new MagicStorageSettings();
        private string unitSlotsBuffer;
        private string overallRefreshBuffer;
        private string craftingMaterialRecheckBuffer;
        private readonly string[] refreshBuffers = new string[(int)StorageRefreshTask.Count];
        private Vector2 settingsScroll;
        private float settingsHeight;
        private enum SettingsTab { General, Performance, Accessibility }
        private SettingsTab settingsTab;

        public MagicStorageMod(ModContentPack content) : base(content)
        {
            // Rules contain Def references, which do not exist during mod construction.
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                Settings = GetSettings<MagicStorageSettings>();
                unitSlotsBuffer = Settings.StorageUnitSlots.ToString();
                ResetRefreshBuffers();
            });
            var assembly = typeof(MagicStorageMod).Assembly;
            string build = assembly.ManifestModule.ModuleVersionId.ToString("N");
            Log.Message("[MagicStorage] Storage receivers use native hauling. Build=" + build);
        }

        public override string SettingsCategory() => "MS_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            bool tidySupplyShelf = Settings.TidySupplyShelf;
            var body = new Rect(inRect.x, inRect.y + 32f, inRect.width, Mathf.Max(0f, inRect.height - 32f));
            Widgets.DrawMenuSection(body);
            TabDrawer.DrawTabs(body, new List<TabRecord>
            {
                new TabRecord("MS_SettingsTabGeneral".Translate(), () => SelectSettingsTab(SettingsTab.General), settingsTab == SettingsTab.General),
                new TabRecord("MS_SettingsTabPerformance".Translate(), () => SelectSettingsTab(SettingsTab.Performance), settingsTab == SettingsTab.Performance),
                new TabRecord("MS_SettingsTabAccessibility".Translate(), () => SelectSettingsTab(SettingsTab.Accessibility), settingsTab == SettingsTab.Accessibility)
            });
            var viewport = new Rect(body.x + 12f, body.y + 12f, body.width - 24f, Mathf.Max(0f, body.height - 24f));
            var view = new Rect(0, 0, viewport.width - 20, Mathf.Max(viewport.height, settingsHeight));
            Widgets.BeginScrollView(viewport, ref settingsScroll, view);
            var listing = new Listing_Standard();
            listing.Begin(view);
            if (settingsTab == SettingsTab.Accessibility) DrawAccessibilitySettings(listing);
            else if (settingsTab == SettingsTab.Performance) DrawPerformanceSettings(listing);
            else DrawGeneralSettings(listing);
            listing.Gap();
            if (listing.ButtonText("MS_SettingsRestoreDefaults".Translate()))
            {
                Settings.RestoreDefaults();
                unitSlotsBuffer = Settings.StorageUnitSlots.ToString();
                ResetRefreshBuffers();
            }
            settingsHeight = listing.CurHeight;
            listing.End();
            Widgets.EndScrollView();
            if (tidySupplyShelf != Settings.TidySupplyShelf) StorageShelfDrawing.RefreshAll();
        }

        private void SelectSettingsTab(SettingsTab tab)
        {
            if (settingsTab == tab) return;
            settingsTab = tab;
            settingsScroll = Vector2.zero;
            settingsHeight = 0f;
        }

        private void DrawGeneralSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("MS_SettingsWorkbenchUnlockMode".Translate(), ref Settings.WorkbenchUnlockMode,
                "MS_SettingsWorkbenchUnlockModeHelp".Translate());
            listing.Gap();
            listing.CheckboxLabeled("MS_SettingsAllowPrinterParameterChanges".Translate(), ref Settings.AllowPrinterParameterChanges);
            listing.Gap();
            listing.Label("MS_SettingsUnitSlotsHelp".Translate());
            int slots = Settings.StorageUnitSlots;
            listing.TextFieldNumericLabeled("MS_SettingsUnitSlots".Translate(), ref slots, ref unitSlotsBuffer,
                MagicStorageSettings.MinUnitSlots, MagicStorageSettings.MaxUnitSlots);
            Settings.StorageUnitSlots = slots;
            listing.Gap();
            listing.Label("MS_SettingsCosmicWorkbenchWorkSpeed".Translate(Settings.CosmicWorkbenchWorkSpeedPercent));
            int speedStep = MagicStorageSettings.CosmicWorkbenchWorkSpeedStep;
            Settings.CosmicWorkbenchWorkSpeedPercent = (int)System.Math.Round(listing.Slider(
                Settings.CosmicWorkbenchWorkSpeedPercent / (float)speedStep,
                MagicStorageSettings.MinCosmicWorkbenchWorkSpeedPercent / (float)speedStep,
                MagicStorageSettings.MaxCosmicWorkbenchWorkSpeedPercent / (float)speedStep),
                System.MidpointRounding.AwayFromZero) * speedStep;
        }

        private void DrawAccessibilitySettings(Listing_Standard listing)
        {
            listing.Label("MS_SettingsAccessibilityNotice".Translate());
            listing.Gap();
            listing.CheckboxLabeled("MS_SettingsCaravanItemFilters".Translate(), ref Settings.CaravanItemFilters);
            listing.CheckboxLabeled("MS_SettingsTradeItemFilters".Translate(), ref Settings.TradeItemFilters);
            listing.Gap();
            listing.CheckboxLabeled("MS_SettingsTidySupplyShelf".Translate(), ref Settings.TidySupplyShelf,
                "MS_SettingsTidySupplyShelfHelp".Translate());
        }

        private void DrawPerformanceSettings(Listing_Standard listing)
        {
            listing.Label("MS_SettingsPerformanceNotice".Translate());
            listing.Gap();
            listing.CheckboxLabeled("MS_SettingsSimplifiedMode".Translate(), ref Settings.SimplifiedMode);
            listing.Label("MS_SettingsRefreshHelp".Translate());
            if (Settings.SimplifiedMode)
            {
                int seconds = Settings.OverallRefreshSeconds;
                listing.TextFieldNumericLabeled("MS_SettingsOverallRefresh".Translate(), ref seconds, ref overallRefreshBuffer,
                    MagicStorageSettings.MinRefreshSeconds, MagicStorageSettings.MaxRefreshSeconds);
                Settings.OverallRefreshSeconds = seconds;
            }
            else
            {
                for (int i = 0; i < (int)StorageRefreshTask.Count; i++)
                {
                    var task = (StorageRefreshTask)i;
                    int seconds = Settings.GetIndividualRefreshSeconds(task);
                    listing.TextFieldNumericLabeled(("MS_SettingsRefresh_" + task).Translate(), ref seconds, ref refreshBuffers[i],
                        MagicStorageSettings.MinRefreshSeconds, MagicStorageSettings.MaxRefreshSeconds);
                    Settings.SetIndividualRefreshSeconds(task, seconds);
                }
            }
            listing.Gap();
            listing.Label("MS_SettingsCraftingMaterialRecheckHelp".Translate());
            int materialSeconds = Settings.CraftingMaterialRecheckSeconds;
            listing.TextFieldNumericLabeled("MS_SettingsCraftingMaterialRecheck".Translate(), ref materialSeconds,
                ref craftingMaterialRecheckBuffer, 0, 60);
            Settings.CraftingMaterialRecheckSeconds = materialSeconds;
        }

        private void ResetRefreshBuffers()
        {
            overallRefreshBuffer = Settings.OverallRefreshSeconds.ToString();
            craftingMaterialRecheckBuffer = Settings.CraftingMaterialRecheckSeconds.ToString();
            for (int i = 0; i < refreshBuffers.Length; i++)
                refreshBuffers[i] = Settings.GetIndividualRefreshSeconds((StorageRefreshTask)i).ToString();
        }
    }
}
