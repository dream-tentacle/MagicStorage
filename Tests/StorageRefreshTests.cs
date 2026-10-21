using System;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class StorageRefreshTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Refresh: " + message + "; expected " + expected + ", got " + actual); }

    internal static int Run()
    {
        int originalTick = Find.TickManager.TicksGame;
        try
        {
            DefaultsAndModes(); IndependentTimers(); DeferredSettingsLoad(); SettingsWindow(); ReceiverAndMaintenance(); RecoveryRetry(); FacilityCadence();
            return checks;
        }
        finally { MagicStorageMod.Settings.RestoreDefaults(); Find.TickManager.TicksGame = originalTick; }
    }

    private static void DefaultsAndModes()
    {
        var settings = MagicStorageMod.Settings;
        settings.RestoreDefaults();
        Equal(true, settings.SimplifiedMode, "simple mode enabled by default");
        Equal(100, settings.CosmicWorkbenchWorkSpeedPercent, "workbench speed defaults to one hundred percent");
        settings.CosmicWorkbenchWorkSpeedPercent = int.MinValue;
        Equal(50, settings.CosmicWorkbenchWorkSpeedPercent, "workbench speed lower bound");
        settings.CosmicWorkbenchWorkSpeedPercent = int.MaxValue;
        Equal(1000, settings.CosmicWorkbenchWorkSpeedPercent, "workbench speed upper bound");
        settings.CosmicWorkbenchWorkSpeedPercent = 154;
        Equal(150, settings.CosmicWorkbenchWorkSpeedPercent, "workbench speed snaps down to ten percent steps");
        settings.CosmicWorkbenchWorkSpeedPercent = 155;
        Equal(160, settings.CosmicWorkbenchWorkSpeedPercent, "workbench speed snaps up to ten percent steps");
        settings.RestoreDefaults();
        Equal(100, settings.CosmicWorkbenchWorkSpeedPercent, "restore defaults resets workbench speed");
        Equal(1, settings.OverallRefreshSeconds, "overall default is one second");
        Equal(30, settings.CraftingMaterialRecheckSeconds, "material retry defaults to thirty seconds");
        Equal(1800, settings.CraftingMaterialRecheckTicks, "material retry uses normal game ticks");
        settings.SimplifiedMode = false;
        for (int i = 0; i < (int)StorageRefreshTask.Count; i++)
        {
            var task = (StorageRefreshTask)i;
            Equal(1, settings.GetIndividualRefreshSeconds(task), "individual default is one second");
            settings.SetIndividualRefreshSeconds(task, i + 2);
            Equal((i + 2) * 60, settings.GetRefreshTicks(task), "detailed mode uses own interval");
        }
        settings.OverallRefreshSeconds = 60; settings.SimplifiedMode = true;
        Equal(30, settings.CraftingMaterialRecheckSeconds, "overall refresh never overrides material retry");
        for (int i = 0; i < (int)StorageRefreshTask.Count; i++)
        {
            var task = (StorageRefreshTask)i;
            Equal(3600, settings.GetRefreshTicks(task), "overall setting controls every task");
            Equal(i + 2, settings.GetIndividualRefreshSeconds(task), "simple mode preserves detailed values");
        }
        settings.SimplifiedMode = false;
        Equal(120, settings.GetRefreshTicks(StorageRefreshTask.NetworkMaintenance), "turning simple mode off restores independent interval");
        settings.OverallRefreshSeconds = 0;
        Equal(1, settings.OverallRefreshSeconds, "overall lower bound");
        settings.OverallRefreshSeconds = int.MaxValue;
        Equal(60, settings.OverallRefreshSeconds, "overall upper bound");
        settings.SetIndividualRefreshSeconds(StorageRefreshTask.FuelSupply, -1);
        Equal(1, settings.GetIndividualRefreshSeconds(StorageRefreshTask.FuelSupply), "individual lower bound");
        settings.SetIndividualRefreshSeconds(StorageRefreshTask.FuelSupply, int.MaxValue);
        Equal(60, settings.GetIndividualRefreshSeconds(StorageRefreshTask.FuelSupply), "individual upper bound");
        settings.CraftingMaterialRecheckSeconds = -1;
        Equal(0, settings.CraftingMaterialRecheckTicks, "zero disables retry cache");
        settings.CraftingMaterialRecheckSeconds = int.MaxValue;
        Equal(60, settings.CraftingMaterialRecheckSeconds, "material retry upper bound");
        settings.RestoreDefaults();
        Equal(30, settings.CraftingMaterialRecheckSeconds, "restore defaults resets independent material retry");
    }

    private static void IndependentTimers()
    {
        var settings = MagicStorageMod.Settings;
        settings.SimplifiedMode = false;
        var timers = new StorageRefreshTimer[(int)StorageRefreshTask.Count];
        for (int i = 0; i < timers.Length; i++)
        {
            var task = (StorageRefreshTask)i;
            settings.SetIndividualRefreshSeconds(task, i + 1);
            Equal(true, timers[i].IsDue(task, 0), "first check can run");
            Equal(false, timers[i].IsDue(task, (i + 1) * 60 - 1), "own interval has not elapsed");
            Equal(true, timers[i].IsDue(task, (i + 1) * 60), "own deadline executes once");
            Equal(false, timers[i].IsDue(task, (i + 1) * 60), "same tick never executes twice");
        }
        settings.SimplifiedMode = true; settings.OverallRefreshSeconds = 1;
        var first = new StorageRefreshTimer(); var second = new StorageRefreshTimer();
        Equal(true, first.IsDue(StorageRefreshTask.Receiver, 0), "first owner's initial check");
        Equal(true, second.IsDue(StorageRefreshTask.Receiver, 30), "second owner keeps own phase");
        Equal(true, first.IsDue(StorageRefreshTask.Receiver, 60), "first owner deadline");
        Equal(false, second.IsDue(StorageRefreshTask.Receiver, 60), "first owner does not advance second owner");
        Equal(true, second.IsDue(StorageRefreshTask.Receiver, 90), "second owner's deadline");
        settings.OverallRefreshSeconds = 60;
        Equal(false, first.IsDue(StorageRefreshTask.Receiver, 120), "increasing interval applies to existing timer");
        settings.OverallRefreshSeconds = 1;
        Equal(true, first.IsDue(StorageRefreshTask.Receiver, 120), "decreasing interval takes effect without resetting owner");
        Equal(true, first.IsDue(StorageRefreshTask.Receiver, 10000), "long gap performs one scheduled action");
        Equal(false, first.IsDue(StorageRefreshTask.Receiver, 10000), "long gap does not replay missed actions");
        Equal(true, first.IsDue(StorageRefreshTask.Receiver, 100), "clock rewind starts a fresh schedule");
        Equal(false, first.IsDue(StorageRefreshTask.Receiver, 159), "rewound schedule still honors interval");
        settings.RestoreDefaults();
    }

    private static void DeferredSettingsLoad()
    {
        var previous = MagicStorageMod.Settings;
        int reads = Mod.SettingsReads;
        LongEventHandler.Defer = true;
        try
        {
            var mod = new MagicStorageMod(new ModContentPack());
            Equal(reads, Mod.SettingsReads, "settings with Def references are not read during mod construction");
            Equal(previous, MagicStorageMod.Settings, "default settings remain available until defs finish loading");
            Equal(1, LongEventHandler.Pending.Count, "settings initialization is queued after loading");
            LongEventHandler.Finish();
            Equal(reads + 1, Mod.SettingsReads, "settings load once after the loading callback runs");
            Equal(0, LongEventHandler.Pending.Count, "settings callback completes without requeuing");
            Listing_Standard.NumericLabels.Clear();
            mod.DoSettingsWindowContents(new UnityEngine.Rect(0, 0, 500, 400));
            Equal(true, Listing_Standard.NumericLabels.Contains("MS_SettingsUnitSlots"), "general settings UI initializes after deferred loading");
        }
        finally
        {
            LongEventHandler.Defer = false;
            LongEventHandler.Pending.Clear();
        }
    }

    private static void SettingsWindow()
    {
        var mod = new MagicStorageMod(new ModContentPack());
        Listing_Standard.NumericLabels.Clear(); mod.DoSettingsWindowContents(new UnityEngine.Rect(0, 0, 500, 400));
        Equal(1, Listing_Standard.NumericLabels.Count, "general tab only contains the unit slot numeric setting");
        Equal(true, Listing_Standard.NumericLabels.Contains("MS_SettingsUnitSlots"), "general tab shows slot capacity");
        Equal(false, Listing_Standard.NumericLabels.Contains("MS_SettingsOverallRefresh"), "general tab hides performance fields");
        TabDrawer.Tabs.Find(tab => tab.label == "MS_SettingsTabPerformance").clickedAction();
        Listing_Standard.NumericLabels.Clear(); mod.DoSettingsWindowContents(new UnityEngine.Rect(0, 0, 500, 400));
        Equal(2, Listing_Standard.NumericLabels.Count, "performance simple view contains overall and material retry intervals");
        Equal(false, Listing_Standard.NumericLabels.Contains("MS_SettingsUnitSlots"), "performance tab hides slot capacity");
        Equal(true, Listing_Standard.NumericLabels.Contains("MS_SettingsOverallRefresh"), "simple view shows overall refresh");
        Equal(true, Listing_Standard.NumericLabels.Contains("MS_SettingsCraftingMaterialRecheck"), "simple view shows separate material retry");
        MagicStorageMod.Settings.SimplifiedMode = false;
        Listing_Standard.NumericLabels.Clear(); mod.DoSettingsWindowContents(new UnityEngine.Rect(0, 0, 500, 400));
        Equal(11, Listing_Standard.NumericLabels.Count, "performance detailed view contains ten intervals and material retry");
        Equal(true, Listing_Standard.NumericLabels.Contains("MS_SettingsCraftingMaterialRecheck"), "detailed view also shows material retry");
        Equal(false, Listing_Standard.NumericLabels.Contains("MS_SettingsOverallRefresh"), "detailed view hides overall refresh");
        for (int i = 0; i < (int)StorageRefreshTask.Count; i++)
            Equal(true, Listing_Standard.NumericLabels.Contains("MS_SettingsRefresh_" + (StorageRefreshTask)i), "detailed field visible");
        TabDrawer.Tabs.Find(tab => tab.label == "MS_SettingsTabGeneral").clickedAction();
        Listing_Standard.NumericLabels.Clear(); mod.DoSettingsWindowContents(new UnityEngine.Rect(0, 0, 500, 400));
        Equal(1, Listing_Standard.NumericLabels.Count, "returning to general hides the detailed intervals");
        Equal(false, MagicStorageMod.Settings.SimplifiedMode, "switching tabs preserves performance settings");
        Equal(false, MagicStorageMod.Settings.CaravanItemFilters, "caravan item filters default off");
        Equal(false, MagicStorageMod.Settings.TradeItemFilters, "trade item filters default off");
        TabDrawer.Tabs.Find(tab => tab.label == "MS_SettingsTabAccessibility").clickedAction();
        Listing_Standard.Events.Clear();
        Listing_Standard.NumericLabels.Clear();
        mod.DoSettingsWindowContents(new UnityEngine.Rect(0, 0, 500, 400));
        Equal("MS_SettingsAccessibilityNotice", Listing_Standard.Events[0], "conflict explanation is first on accessibility tab");
        Equal("MS_SettingsCaravanItemFilters", Listing_Standard.Events[1], "accessibility tab exposes caravan toggle");
        Equal("MS_SettingsTradeItemFilters", Listing_Standard.Events[2], "accessibility tab exposes independent trade toggle");
        Equal(0, Listing_Standard.NumericLabels.Count, "accessibility tab has no performance or storage numeric settings");
        MagicStorageMod.Settings.CaravanItemFilters = true;
        MagicStorageMod.Settings.TradeItemFilters = true;
        MagicStorageMod.Settings.RestoreDefaults();
        Equal(false, MagicStorageMod.Settings.CaravanItemFilters, "restore defaults disables caravan filter hook");
        Equal(false, MagicStorageMod.Settings.TradeItemFilters, "restore defaults disables trade filter hook");
    }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Manager.FinalizeInit();
        }
        internal void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building building) building.node = node;
            if (thing is Building_StorageCore core) core.node = node;
            if (thing is Building_StorageUnit unit) unit.node = node;
            Manager.Register(node);
        }
        internal void Tick(int tick) { Find.TickManager.TicksGame = tick; Manager.MapComponentTick(); }
    }

    private static void ReceiverAndMaintenance()
    {
        var settings = MagicStorageMod.Settings;
        settings.SimplifiedMode = false;
        settings.SetIndividualRefreshSeconds(StorageRefreshTask.NetworkMaintenance, 60);
        settings.SetIndividualRefreshSeconds(StorageRefreshTask.Receiver, 2);
        var f = new Fixture(); var receiver = new Building_StorageReceiver(); f.Register(receiver, 2);
        f.Map.haulDestinationManager.SlotParents[receiver.Position] = receiver;
        f.Tick(0);
        var item = new Thing { Map = f.Map, Position = receiver.Position, Spawned = true, stackCount = 10 };
        f.Map.Ground.Add(item); receiver.Notify_ReceivedThing(item);
        f.Tick(119); Equal(true, item.Spawned, "delivery notification does not bypass configured interval");
        f.Tick(120); Equal(10L, f.Unit.Inventory.GetTotalCount(item.def), "receiver runs independently of slower network maintenance");
        int policyCalls = 0; bool permitted = true; object token = new object();
        f.Core.Network.ReserveOutgoing(token, item, 4, usable: () => { policyCalls++; return permitted; });
        f.Tick(3599); Equal(0, policyCalls, "network purpose polling waits for configured interval");
        f.Tick(3600); Equal(1, policyCalls, "network purpose polling runs at configured deadline");
        permitted = false;
        Equal(false, f.Core.Network.TryWithdrawReserved(token, item, 4, new ThingOwner(null), out _).Complete,
            "real withdrawal rechecks policy immediately despite slow polling");
        Equal(10, item.stackCount, "failed purpose check never awards items");
        settings.RestoreDefaults();
    }

    private static void RecoveryRetry()
    {
        var settings = MagicStorageMod.Settings;
        settings.SimplifiedMode = false; settings.SetIndividualRefreshSeconds(StorageRefreshTask.RecoveryRelease, 3);
        var f = new Fixture(); f.Map.DropBudget = 0;
        var batch = f.Manager.CreateRecovery(new IntVec3(3, 0));
        var item = new Thing(); batch.Contents.TryAdd(item, false);
        f.Tick(0); f.Map.DropBudget = 100;
        f.Tick(179); Equal(false, item.Spawned, "pending item waits for placement retry interval");
        f.Tick(180); Equal(true, item.Spawned, "pending item released at independent retry deadline");
        settings.RestoreDefaults();
    }

    private static void FacilityCadence()
    {
        var settings = MagicStorageMod.Settings;
        settings.SimplifiedMode = false;
        settings.SetIndividualRefreshSeconds(StorageRefreshTask.ShelfRestock, 2);
        settings.SetIndividualRefreshSeconds(StorageRefreshTask.ConstructionSupply, 2);
        settings.SetIndividualRefreshSeconds(StorageRefreshTask.ApparelCleanup, 2);

        var f = new Fixture(); var def = new ThingDef();
        var shelf = new Building_StorageSupplyShelf();
        var storageSettings = new StorageSettings(); storageSettings.filter.SetAllow(def, true);
        shelf.def.building = new BuildingProperties { fixedStorageSettings = storageSettings };
        f.Register(shelf, 2); f.Manager.EnsureCurrent();
        Equal(true, shelf.SetSelection(0, def), "timed shelf fixture accepts configured resource");
        Find.TickManager.TicksGame = 0; shelf.RunTransportTestTick();
        f.Unit.Inventory.Contents.TryAdd(new Thing { def = def, stackCount = 3 }, false);
        Find.TickManager.TicksGame = 119; shelf.RunTransportTestTick();
        Equal(0, shelf.GetSlotGroup().HeldThingsCount, "shelf waits for independent restock interval");
        Find.TickManager.TicksGame = 120; shelf.RunTransportTestTick();
        Equal(3, shelf.GetSlotGroup().HeldThings.Sum(t => t.stackCount), "normal tick restocks shelf at configured deadline");

        f = new Fixture(); var supplier = new Building_ConstructionSupplyUnit(); f.Register(supplier, 2);
        var frame = new Frame { Map = f.Map, Position = new IntVec3(3, 0), Spawned = true, Faction = Faction.OfPlayer,
            def = new ThingDef { IsFrame = true, entityDefToBuild = new ThingDef { category = ThingCategory.Building } } };
        frame.Costs.Add(new ThingDefCountClass { thingDef = def, count = 10 }); f.Map.listerThings.Things.Add(frame);
        Find.TickManager.TicksGame = 0; supplier.RunTransportTestTick();
        f.Unit.Inventory.Contents.TryAdd(new Thing { def = def, stackCount = 3 }, false);
        Find.TickManager.TicksGame = 119; supplier.RunTransportTestTick();
        Equal(0, frame.resourceContainer.Count, "construction supplier waits for independent interval");
        Find.TickManager.TicksGame = 120; supplier.RunTransportTestTick();
        Equal(3, frame.resourceContainer.Sum(t => t.stackCount), "normal tick supplies construction at configured deadline");
        Equal(0f, frame.workDone, "refresh never contributes free construction work");

        f = new Fixture(); var adapter = new Building_StorageApparelAdapter(); f.Register(adapter, 2); f.Manager.EnsureCurrent();
        Find.TickManager.TicksGame = 0; adapter.RunTransportTestTick();
        var apparel = new Apparel { def = new ThingDef { IsApparel = true, stackLimit = 1 } };
        adapter.Contents.TryAdd(apparel, false);
        Find.TickManager.TicksGame = 119; adapter.RunTransportTestTick();
        Equal(true, adapter.HasApparel, "apparel cleanup waits for independent interval");
        Find.TickManager.TicksGame = 120; adapter.RunTransportTestTick();
        Equal(false, adapter.HasApparel, "normal tick cleans up abandoned staged apparel");
        Equal(1L, f.Unit.Inventory.GetTotalCount(apparel.def), "cleanup preserves real apparel");
        settings.RestoreDefaults();
    }
}
