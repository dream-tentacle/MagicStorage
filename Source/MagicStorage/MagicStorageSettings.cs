using System;
using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    public sealed class MagicStorageSettings : ModSettings
    {
        public List<StorageInventoryRule> InventoryFilters = new List<StorageInventoryRule>();
        public const int DefaultUnitSlots = 64;
        public const int MinUnitSlots = 1;
        public const int MaxUnitSlots = 10000;
        private int storageUnitSlots = DefaultUnitSlots;
        public const int DefaultConstructionSupplyRadius = 40;
        public const int MinConstructionSupplyRadius = 1;
        public const int MaxConstructionSupplyRadius = 500;
        private int constructionSupplyRadius = DefaultConstructionSupplyRadius;
        public int ConstructionSupplyRadius
        {
            get => constructionSupplyRadius;
            set => constructionSupplyRadius = Math.Max(MinConstructionSupplyRadius, Math.Min(MaxConstructionSupplyRadius, value));
        }
        public const int DefaultCosmicWorkbenchWorkSpeedPercent = 100;
        public const int MinCosmicWorkbenchWorkSpeedPercent = 50;
        public const int MaxCosmicWorkbenchWorkSpeedPercent = 1000;
        public const int CosmicWorkbenchWorkSpeedStep = 10;
        private int cosmicWorkbenchWorkSpeedPercent = DefaultCosmicWorkbenchWorkSpeedPercent;
        public int CosmicWorkbenchWorkSpeedPercent
        {
            get => cosmicWorkbenchWorkSpeedPercent;
            set
            {
                int clamped = Math.Max(MinCosmicWorkbenchWorkSpeedPercent, Math.Min(MaxCosmicWorkbenchWorkSpeedPercent, value));
                cosmicWorkbenchWorkSpeedPercent = (clamped + CosmicWorkbenchWorkSpeedStep / 2) /
                    CosmicWorkbenchWorkSpeedStep * CosmicWorkbenchWorkSpeedStep;
            }
        }
        public float CosmicWorkbenchWorkSpeedFactor => CosmicWorkbenchWorkSpeedPercent / 100f;
        public const int DefaultRefreshSeconds = 1;
        public const int MinRefreshSeconds = 1;
        public const int MaxRefreshSeconds = 60;
        public bool SimplifiedMode = true;
        public bool WorkbenchUnlockMode;
        public bool NewWorkbenchOrdersFirst = true;
        public bool AllowPrinterParameterChanges;
        public bool CaravanItemFilters;
        public bool TradeItemFilters;
        public bool TidySupplyShelf = true;
        private int overallRefreshSeconds = DefaultRefreshSeconds;
        private List<int> refreshSeconds = CreateRefreshDefaults();
        public const int DefaultCraftingMaterialRecheckSeconds = 30;
        private int craftingMaterialRecheckSeconds = DefaultCraftingMaterialRecheckSeconds;
        public int CraftingMaterialRecheckSeconds
        {
            get => craftingMaterialRecheckSeconds;
            set => craftingMaterialRecheckSeconds = Math.Max(0, Math.Min(60, value));
        }
        public int CraftingMaterialRecheckTicks => CraftingMaterialRecheckSeconds * 60;

        private static List<int> CreateRefreshDefaults()
        {
            var result = new List<int>();
            for (int i = 0; i < (int)StorageRefreshTask.Count; i++) result.Add(DefaultRefreshSeconds);
            return result;
        }

        private static int ClampRefresh(int seconds) => Math.Max(MinRefreshSeconds, Math.Min(MaxRefreshSeconds, seconds));
        public int OverallRefreshSeconds
        {
            get => overallRefreshSeconds;
            set => overallRefreshSeconds = ClampRefresh(value);
        }
        public int GetIndividualRefreshSeconds(StorageRefreshTask task) => refreshSeconds[(int)task];
        public void SetIndividualRefreshSeconds(StorageRefreshTask task, int seconds) => refreshSeconds[(int)task] = ClampRefresh(seconds);
        public int GetRefreshTicks(StorageRefreshTask task) =>
            (SimplifiedMode ? OverallRefreshSeconds : GetIndividualRefreshSeconds(task)) * 60;

        public void RestoreDefaults()
        {
            StorageUnitSlots = DefaultUnitSlots;
            ConstructionSupplyRadius = DefaultConstructionSupplyRadius;
            CosmicWorkbenchWorkSpeedPercent = DefaultCosmicWorkbenchWorkSpeedPercent;
            WorkbenchUnlockMode = false;
            NewWorkbenchOrdersFirst = true;
            AllowPrinterParameterChanges = false;
            CaravanItemFilters = false;
            TradeItemFilters = false;
            TidySupplyShelf = true;
            SimplifiedMode = true;
            OverallRefreshSeconds = DefaultRefreshSeconds;
            refreshSeconds = CreateRefreshDefaults();
            CraftingMaterialRecheckSeconds = DefaultCraftingMaterialRecheckSeconds;
        }

        public int StorageUnitSlots
        {
            get => storageUnitSlots;
            set => storageUnitSlots = Math.Max(MinUnitSlots, Math.Min(MaxUnitSlots, value));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref InventoryFilters, "storageInventoryFilters", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (InventoryFilters == null) InventoryFilters = new List<StorageInventoryRule>();
                InventoryFilters.RemoveAll(rule => rule == null);
            }
            Scribe_Values.Look(ref storageUnitSlots, "storageUnitSlots", DefaultUnitSlots);
            StorageUnitSlots = storageUnitSlots;
            Scribe_Values.Look(ref constructionSupplyRadius, "constructionSupplyRadius", DefaultConstructionSupplyRadius);
            ConstructionSupplyRadius = constructionSupplyRadius;
            Scribe_Values.Look(ref cosmicWorkbenchWorkSpeedPercent, "cosmicWorkbenchWorkSpeedPercent", DefaultCosmicWorkbenchWorkSpeedPercent);
            CosmicWorkbenchWorkSpeedPercent = cosmicWorkbenchWorkSpeedPercent;
            Scribe_Values.Look(ref SimplifiedMode, "simplifiedMode", true);
            Scribe_Values.Look(ref WorkbenchUnlockMode, "workbenchUnlockMode", false);
            Scribe_Values.Look(ref NewWorkbenchOrdersFirst, "newWorkbenchOrdersFirst", true);
            Scribe_Values.Look(ref AllowPrinterParameterChanges, "allowPrinterParameterChanges", false);
            Scribe_Values.Look(ref CaravanItemFilters, "caravanItemFilters", false);
            Scribe_Values.Look(ref TradeItemFilters, "tradeItemFilters", false);
            Scribe_Values.Look(ref TidySupplyShelf, "tidySupplyShelf", true);
            Scribe_Values.Look(ref overallRefreshSeconds, "overallRefreshSeconds", DefaultRefreshSeconds);
            Scribe_Values.Look(ref craftingMaterialRecheckSeconds, "craftingMaterialRecheckSeconds", DefaultCraftingMaterialRecheckSeconds);
            CraftingMaterialRecheckSeconds = craftingMaterialRecheckSeconds;
            Scribe_Collections.Look(ref refreshSeconds, "refreshSeconds", LookMode.Value);
            OverallRefreshSeconds = overallRefreshSeconds;
            if (refreshSeconds == null) refreshSeconds = CreateRefreshDefaults();
            while (refreshSeconds.Count < (int)StorageRefreshTask.Count) refreshSeconds.Add(DefaultRefreshSeconds);
            for (int i = 0; i < refreshSeconds.Count; i++) refreshSeconds[i] = ClampRefresh(refreshSeconds[i]);
        }
    }
}
