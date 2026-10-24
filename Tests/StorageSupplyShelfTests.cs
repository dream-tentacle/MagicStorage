using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MagicStorage;
using RimWorld;
using Verse;

internal static class StorageSupplyShelfTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string label)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(label + ": " + expected + " != " + actual); }
    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Building_StorageSupplyShelf Shelf = new Building_StorageSupplyShelf();
        internal readonly ThingDef X = new ThingDef(), Y = new ThingDef { stackLimit = 10 }, Z = new ThingDef { stackLimit = 1 };
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture(int slots = 64, int shelfSlots = 3)
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Unit.SlotCapacity = slots;
            var parent = new StorageSettings();
            foreach (ThingDef def in new[] { X, Y, Z }) parent.filter.SetAllow(def, true);
            Shelf.def.building = new BuildingProperties { fixedStorageSettings = parent, maxItemsInCell = shelfSlots };
            Thing[] buildings = { Core, Unit, Shelf };
            for (int i = 0; i < buildings.Length; i++)
            {
                Thing building = buildings[i];
                building.Map = Map; building.Position = new IntVec3(i, 0); building.Spawned = true; building.Faction = Faction.OfPlayer;
                var node = new CompStorageNode { parent = building };
                if (building == Core) Core.node = node;
                if (building == Unit) Unit.node = node;
                if (building == Shelf) Shelf.node = node;
                Manager.Register(node);
            }
            Manager.FinalizeInit();
            Map.haulDestinationManager.SlotParents[Shelf.Position] = Shelf;
        }
        internal Thing Store(ThingDef def, int count, string variant = null)
        {
            var item = new Thing { def = def, stackCount = count, variant = variant };
            Unit.Inventory.Contents.TryAdd(item, false); return item;
        }
        internal Thing Stock(ThingDef def) => Shelf.GetSlotGroup().HeldThings.FirstOrDefault(t => t.def == def);
        internal void Tick() => Shelf.RefreshSupply();
        internal long Pending()
        {
            var holders = new List<IThingHolder>(); Manager.GetChildHolders(holders);
            return holders.Cast<StorageRecoveryBatch>().Sum(h => h.PendingCount);
        }
    }
    internal static int Run()
    {
        NativeMergeBoundary();
        PresetClipboard();
        Configuration(); Restocking(); RestockLimits(); StackVariants(); Reservations(); ConfigurationChanges(); NetworkAndPlacement(); Removal(); Filters(); Warnings(); WarningReports();
        StreamingRefill(); StreamingRollback(); StreamingNetworkChange();
        StockSelectionCommands();
        DuplicateSlots(); SharedFilterRestocking(); ModeSwitchReturns(); SharedFilterPresets();
        ConfigurableSlotCounts(); DifferentSizePresets();
        return assertions;
    }

    private static void NativeMergeBoundary()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(StorageShelfHaulingPatches).TypeHandle);
        var giver = new WorkGiver_Merge();
        var pawn = new Pawn();
        WorkGiver_Merge.OriginalCalls = 0;
        foreach (int slots in new[] { 3, 10 })
        foreach (bool filterMode in new[] { false, true })
        {
            var f = new Fixture(shelfSlots: slots);
            if (filterMode)
            {
                f.Shelf.TrySetFilterMode(true);
                f.Shelf.SharedFilter.SetAllow(f.Y, true);
                for (int i = 0; i < slots; i++) f.Shelf.SetFilterLimit(i, 0);
            }
            int[] counts = { 2, 5, 7 };
            for (int i = 0; i < counts.Length; i++)
            {
                if (filterMode) f.Shelf.SetFilterLimit(i, counts[i]);
                else { f.Shelf.SetSelection(i, f.Y); f.Shelf.SetRestockLimit(i, counts[i]); }
            }
            f.Store(f.Y, 10); f.Store(f.Y, 10); f.Tick();
            for (int i = 0; i < counts.Length; i++)
            {
                Thing stock = f.Shelf.StockAt(i);
                Equal(true, stock != null, "partial stack stocked before merge check");
                foreach (bool forced in new[] { false, true })
                    Equal<Verse.AI.Job>(null, giver.JobOnThing(pawn, stock, forced), "supply shelf never creates native merge job");
                Equal(counts[i], stock.stackCount, "merge check preserves independent slot quantity");
            }
            Equal(0, WorkGiver_Merge.OriginalCalls, "both shelf sizes and modes skip merge factory");

            Thing moved = f.Shelf.StockAt(0);
            moved.Position = new IntVec3(20, 20);
            Equal(WorkGiver_Merge.VanillaJob, giver.JobOnThing(pawn, moved), "stock moved off shelf uses native merge factory");
            moved.Position = f.Shelf.Position;
            moved.Spawned = false;
            Equal(WorkGiver_Merge.VanillaJob, giver.JobOnThing(pawn, moved), "carried stock is not treated as shelf stock");
            WorkGiver_Merge.OriginalCalls = 0;
        }
        var receiver = new Building_StorageReceiver();
        var map = new Map();
        map.haulDestinationManager.SlotParents[receiver.Position] = receiver;
        var ordinary = new Thing { Map = map, Spawned = true, Position = receiver.Position };
        Equal(WorkGiver_Merge.VanillaJob, giver.JobOnThing(pawn, ordinary), "receiver stock still uses native merge factory");
        map.haulDestinationManager.SlotParents.Clear();
        Equal(WorkGiver_Merge.VanillaJob, giver.JobOnThing(pawn, ordinary), "other cells still use native merge factory");
        Equal(WorkGiver_Merge.VanillaJob, giver.JobOnThing(pawn, null), "null input remains owned by native factory");
        Equal(3, WorkGiver_Merge.OriginalCalls, "unrelated merge calls pass through");
    }

    private static void ConfigurableSlotCounts()
    {
        foreach (int count in new[] { 1, 2, 4, 6, 9, 16 })
        {
            var f = new Fixture(shelfSlots: count);
            Equal(count, f.Shelf.SlotCount, "slot count follows only native building parameter");
            for (int i = 0; i < count; i++)
            {
                Equal(true, f.Shelf.SetSelection(i, f.X), "all configured slots accept selections");
                f.Shelf.SetRestockLimit(i, i + 1);
            }
            Equal(false, f.Shelf.SetSelection(count, f.X), "selection cannot exceed configured slot count");
            int total = count * (count + 1) / 2;
            int stored = 0;
            while (stored < total + 30) { f.Store(f.X, 75); stored += 75; }
            f.Tick();
            Equal(count, f.Shelf.GetSlotGroup().HeldThingsCount, "normal mode fills all configured positions");
            for (int i = 0; i < count; i++) Equal(i + 1, f.Shelf.StockAt(i).stackCount, "each position has its own normal cap");
            f.Shelf.StockAt(count - 1).SplitOff(1); f.Tick();
            Equal(count, f.Shelf.StockAt(count - 1).stackCount, "last configured position replenishes");
            Equal((long)stored - total - 1, f.Core.Network.GetTotalCount(f.X), "dynamic slot refill conserves inventory");
            Equal(true, f.Shelf.TrySetFilterMode(true), "mode change returns every configured position");
            Equal((long)stored - 1, f.Core.Network.GetTotalCount(f.X), "return includes positions after third");
            f.Shelf.SharedFilter.SetAllow(f.X, true);
            for (int i = 0; i < count; i++) f.Shelf.SetFilterLimit(i, 2);
            f.Tick();
            Equal(count, f.Shelf.GetSlotGroup().HeldThingsCount, "filter mode fills all positions");
            for (int i = 0; i < count; i++) Equal(2, f.Shelf.StockAt(i).stackCount, "filter caps exist for every position");
            Scribe.mode = LoadSaveMode.PostLoadInit;
            try { f.Shelf.ExposeData(); } finally { Scribe.mode = LoadSaveMode.Inactive; }
            Equal(2, f.Shelf.FilterLimit(count - 1), "load normalization preserves last slot settings");
            Equal(true, f.Shelf.TrySetFilterMode(false), "switch off returns all filter positions");
            f.Shelf.SlotFilter(count - 1).WarnWhenRestockFails = true;
            f.Manager.Unregister(f.Core.node); f.Tick();
            Equal(StorageShelfFailureReason.NetworkUnavailable, f.Shelf.RestockFailure(count - 1).Reason,
                "warning data covers final configured position");
            Equal(0L, f.Pending(), "dynamic shelf operations leave no hidden recovery stock");
        }

        // Six different types exercise native placement capacity without any merge shortcut.
        var many = new Fixture(shelfSlots: 6);
        for (int i = 0; i < 6; i++)
        {
            var itemDef = new ThingDef();
            many.Shelf.GetParentStoreSettings().filter.SetAllow(itemDef, true);
            many.Shelf.SetSelection(i, itemDef); many.Shelf.SetRestockLimit(i, 3); many.Store(itemDef, 3);
        }
        many.Tick();
        Equal(6, many.Shelf.GetSlotGroup().HeldThingsCount, "six different types use native configured storage capacity");
        Equal(6, many.Shelf.GetSlotGroup().HeldThings.Select(t => t.def).Distinct().Count(), "six distinct types remain independently stocked");
    }

    private static void DifferentSizePresets()
    {
        var large = new Fixture(shelfSlots: 6);
        for (int i = 0; i < 6; i++) { large.Shelf.SetSelection(i, large.X); large.Shelf.SetRestockLimit(i, i + 2); }
        PresetCommand(large.Shelf, "MS_Shelf_CopyPreset").action();
        var small = new Fixture(shelfSlots: 2);
        PresetCommand(small.Shelf, "MS_Shelf_PastePreset").action();
        Equal(2, small.Shelf.SlotCount, "large preset does not change receiver's native capacity");
        Equal(3, small.Shelf.RestockLimit(1), "large preset truncated to receiver's positions");
        PresetCommand(small.Shelf, "MS_Shelf_CopyPreset").action();
        var larger = new Fixture(shelfSlots: 9);
        PresetCommand(larger.Shelf, "MS_Shelf_PastePreset").action();
        Equal(9, larger.Shelf.SlotCount, "small preset retains larger receiver capacity");
        Equal(large.X, larger.Shelf.SelectedDef(1), "shared prefix of preset retained");
        Equal<ThingDef>(null, larger.Shelf.SelectedDef(8), "additional normal positions start unconfigured");
        Equal(75, larger.Shelf.FilterLimit(8), "additional filter positions receive default cap");
        Equal(true, larger.Shelf.SetSelection(8, larger.Y), "new last position remains configurable after paste");
        larger.Shelf.TrySetFilterMode(true); larger.Shelf.SetFilterLimit(8, 4);
        PresetCommand(larger.Shelf, "MS_Shelf_CopyPreset").action();
        PresetCommand(large.Shelf, "MS_Shelf_PastePreset").action();
        Equal(true, large.Shelf.FilterMode, "filter-mode presets also support a different receiver count");
        Equal(6, large.Shelf.SlotCount, "filter paste keeps receiver's own count");
    }

    private static void DuplicateSlots()
    {
        var f = new Fixture();
        for (int i = 0; i < 3; i++)
        { f.Shelf.SetSelection(i, f.X); f.Shelf.SetRestockLimit(i, (i + 1) * 5); }
        f.Store(f.X, 75); f.Tick();
        Equal(3, f.Shelf.GetSlotGroup().HeldThingsCount, "same item occupies three distinct stacks");
        for (int i = 0; i < 3; i++) Equal((i + 1) * 5, f.Shelf.StockAt(i).stackCount, "each duplicate has its own limit");
        Thing first = f.Shelf.StockAt(0), second = f.Shelf.StockAt(1), third = f.Shelf.StockAt(2);
        second.SplitOff(3); f.Tick();
        Equal(5, first.stackCount, "refilling second slot does not merge into first");
        Equal(10, second.stackCount, "second duplicate replenished exactly");
        Equal(15, third.stackCount, "third duplicate stays independent");
        Equal(0, f.Shelf.StockSlot(first), "first real stack bound to first display position");
        Equal(1, f.Shelf.StockSlot(second), "second real stack bound to second display position");
        first.SplitOff(first.stackCount); f.Tick();
        Equal(5, f.Shelf.StockAt(0).stackCount, "fully consumed duplicate is replaced in its own slot");
        Equal(second, f.Shelf.StockAt(1), "other slot reference survives replacement");
        Scribe.mode = LoadSaveMode.PostLoadInit;
        try { f.Shelf.ExposeData(); } finally { Scribe.mode = LoadSaveMode.Inactive; }
        Equal(f.X, f.Shelf.SelectedDef(1), "load normalization keeps duplicate configuration");
        Equal(f.X, f.Shelf.SelectedDef(2), "all three repeated selections survive normalization");
    }

    private static void SharedFilterRestocking()
    {
        var f = new Fixture();
        Equal(false, f.Shelf.FilterMode, "filter mode defaults off");
        Command_Toggle toggle = f.Shelf.GetGizmos().OfType<Command_Toggle>().Single();
        Equal(false, toggle.isActive(), "native toggle starts unchecked");
        toggle.toggleAction();
        Equal(true, toggle.isActive(), "native toggle reflects enabled filter mode");
        Equal(true, f.Shelf.TrySetFilterMode(true), "empty shelf enables filter mode");
        PresetCommand(f.Shelf, "MS_Shelf_Configure").action();
        Equal(true, Find.WindowStack.Windows.Last() is Dialog_StorageShelfSharedFilter, "configure opens shared filter directly");
        f.Shelf.SharedFilter.SetAllow(f.X, true); f.Shelf.SharedFilter.SetAllow(f.Y, true);
        f.Shelf.SetFilterLimit(0, 4); f.Shelf.SetFilterLimit(1, 7); f.Shelf.SetFilterLimit(2, 6);
        f.Store(f.Z, 1); f.Store(f.X, 15); f.Store(f.Y, 10); f.Tick();
        Equal(f.X, f.Shelf.StockAt(0).def, "first matching inventory type selected, excluding disallowed types");
        Equal(4, f.Shelf.StockAt(0).stackCount, "first filter slot uses its own cap");
        Equal(7, f.Shelf.StockAt(1).stackCount, "second filter slot independently capped");
        Equal(4, f.Shelf.StockAt(2).stackCount, "third slot accepts available partial stock");
        f.Tick();
        Equal(f.X, f.Shelf.StockAt(2).def, "partial existing stack does not switch to another allowed type");
        Equal(10L, f.Core.Network.GetTotalCount(f.Y), "other allowed item not mixed into existing stack");
        Thing emptied = f.Shelf.StockAt(1); emptied.SplitOff(emptied.stackCount); f.Tick();
        Equal(f.Y, f.Shelf.StockAt(1).def, "empty slot selects next matching available inventory item");
        Equal(7, f.Shelf.StockAt(1).stackCount, "replacement type retains slot quantity setting");
        f.Shelf.SetFilterLimit(1, 100); f.Tick();
        Equal(10, f.Shelf.StockAt(1).stackCount, "item's native stack limit caps filter mode");
        f.Shelf.SetFilterLimit(0, 0); f.Shelf.StockAt(0).SplitOff(2); f.Store(f.X, 20); f.Tick();
        Equal(2, f.Shelf.StockAt(0).stackCount, "zero filter limit pauses only that slot");
        Equal(6, f.Shelf.StockAt(2).stackCount, "another filter slot still tops up");
        f.Shelf.SharedFilter.SetAllow(f.X, false); f.Tick();
        Equal(false, f.Shelf.GetSlotGroup().HeldThings.Any(t => t.def == f.X), "changing shared filter removes rejected stock");

        f = new Fixture(); f.Shelf.TrySetFilterMode(true); f.Shelf.SharedFilter.SetAllow(f.X, true);
        f.X.hasQualityComp = true;
        f.Shelf.SharedFilter.AllowedQualityLevels = new QualityRange { min = QualityCategory.Good, max = QualityCategory.Legendary };
        Thing poor = f.Store(f.X, 15, "poor"); poor.hasQuality = true; poor.quality = QualityCategory.Poor;
        Thing good = f.Store(f.X, 5, "good"); good.hasQuality = true; good.quality = QualityCategory.Good;
        f.Tick();
        Equal("good", f.Shelf.StockAt(0).variant, "native shared quality filter applies to actual source stacks");
        Equal(15L, f.Core.Network.GetTotalCount(f.X), "nonmatching variant remains stored");
    }

    private static void ModeSwitchReturns()
    {
        var f = new Fixture(1);
        for (int i = 0; i < 3; i++) { f.Shelf.SetSelection(i, f.X); f.Shelf.SetRestockLimit(i, 10); }
        f.Store(f.X, 60); f.Tick();
        f.Core.Settings.allow = false;
        int windows = Find.WindowStack.Windows.Count;
        Equal(false, f.Shelf.TrySetFilterMode(true), "rejected return blocks switch");
        Equal(false, f.Shelf.FilterMode, "failed switch retains normal mode");
        Equal(3, f.Shelf.GetSlotGroup().HeldThingsCount, "filter rejection keeps stock on shelf");
        Equal(windows + 1, Find.WindowStack.Windows.Count, "failed mode switch opens a message box");
        Equal(true, Find.WindowStack.Windows.Last() is Dialog_MessageBox, "failure is a modal message");
        f.Core.Settings.allow = true;
        Thing reserved = f.Shelf.StockAt(1); f.Map.reservationManager.Reserved.Add(reserved);
        Equal(false, f.Shelf.TrySetFilterMode(true), "reserved shelf item blocks switch");
        f.Map.reservationManager.Reserved.Clear();
        Equal(true, f.Shelf.TrySetFilterMode(true), "all duplicate stacks return into one partial network stack");
        Equal(0, f.Shelf.GetSlotGroup().HeldThingsCount, "successful switch clears shelf first");
        Equal(60L, f.Core.Network.GetTotalCount(f.X), "mode switch conserves all materials");
        Equal(10, f.Shelf.RestockLimit(1), "normal mode limits retained");
        f.Shelf.SharedFilter.SetAllow(f.X, true);
        for (int i = 0; i < 3; i++) f.Shelf.SetFilterLimit(i, 5);
        f.Tick();
        f.Core.Settings.allow = false;
        Equal(false, f.Shelf.TrySetFilterMode(false), "failed returns also block switching off");
        Equal(true, f.Shelf.FilterMode, "failed switch off preserves filter mode");
        f.Core.Settings.allow = true;
        Equal(true, f.Shelf.TrySetFilterMode(false), "switching off also returns contents");
        Equal(60L, f.Core.Network.GetTotalCount(f.X), "switching off returns exact filtered quantity");
        Equal(0, f.Shelf.GetSlotGroup().HeldThingsCount, "switching off leaves shelf empty until next restock");
        Equal(5, f.Shelf.FilterLimit(1), "filter mode limits retained");
        f.Tick(); Equal(10, f.Shelf.StockAt(1).stackCount, "normal mode configuration restored");

        f = new Fixture(1); f.Shelf.SetSelection(0, f.X); f.Shelf.SetRestockLimit(0, 20);
        f.Store(f.X, 20); f.Tick(); f.Store(f.Y, 10);
        Equal(false, f.Shelf.TrySetFilterMode(true), "full incompatible network blocks mode switch");
        Equal(20, f.Shelf.StockAt(0).stackCount, "capacity failure preserves shelf contents");
        f.Manager.Unregister(f.Core.node);
        Equal(false, f.Shelf.TrySetFilterMode(true), "disconnected shelf with contents cannot switch");
        Equal(false, f.Shelf.FilterMode, "network failure retains original mode");
        Equal(0L, f.Pending(), "failed mode switches create no hidden recovery stock");
    }

    private static void SharedFilterPresets()
    {
        var f = new Fixture(); f.Shelf.TrySetFilterMode(true);
        f.Shelf.SharedFilter.SetAllow(f.X, true); f.Shelf.SetFilterLimit(0, 3); f.Shelf.SetFilterLimit(1, 8);
        PresetCommand(f.Shelf, "MS_Shelf_CopyPreset").action();
        f.Shelf.SharedFilter.SetAllow(f.X, false); f.Shelf.SetFilterLimit(0, 9);
        var other = new Fixture();
        PresetCommand(other.Shelf, "MS_Shelf_PastePreset").action();
        Equal(true, other.Shelf.FilterMode, "preset includes mode");
        Equal(true, other.Shelf.SharedFilter.Allows(f.X), "preset filter snapshot survives later edits");
        Equal(3, other.Shelf.FilterLimit(0), "preset first slot cap retained");
        Equal(8, other.Shelf.FilterLimit(1), "preset second slot cap retained");
        other.Shelf.SharedFilter.SetAllow(f.X, false);
        PresetCommand(other.Shelf, "MS_Shelf_PastePreset").action();
        Equal(true, other.Shelf.SharedFilter.Allows(f.X), "pasted filter is independent of reusable clipboard");
    }

    private static void StockSelectionCommands()
    {
        var previousSelection = Find.Selector.Selected;
        try
        {
            var f = new Fixture();
            f.Shelf.SetSelection(0, f.X); f.Shelf.SetSelection(1, f.Y);
            f.Store(f.X, 5); f.Store(f.Y, 3); f.Tick();
            Find.Selector.Selected = f.Shelf;
            var commands = f.Shelf.GetGizmos().OfType<Command_SelectStorage>().ToList();
            Equal(2, commands.Count, "selected shelf offers one command per physical stock stack");
            Equal(true, commands.Any(command => command.Target == f.Stock(f.X)), "first stock stack has a selection command");
            Equal(true, commands.Any(command => command.Target == f.Stock(f.Y)), "second stock stack has a selection command");
            commands[0].action();
            Equal(commands[0].Target, Find.Selector.Selected, "stock command selects the physical item");
            Equal(8, f.Shelf.GetSlotGroup().HeldThings.Sum(item => item.stackCount), "selecting stock leaves shelf inventory intact");
            Find.Selector.Selected = null;
            Equal(0, f.Shelf.GetGizmos().OfType<Command_SelectStorage>().Count(), "stock buttons require a single selection");
            Find.Selector.Selected = f.Shelf;
            f.Shelf.Spawned = false;
            Equal(0, f.Shelf.GetGizmos().OfType<Command_SelectStorage>().Count(), "unspawned shelf does not offer stock selection");
        }
        finally { Find.Selector.Selected = previousSelection; }
    }

    private static Command_Action PresetCommand(Building_StorageSupplyShelf shelf, string key) =>
        shelf.GetGizmos().OfType<Command_Action>().Single(command => command.defaultLabel == key.Translate());

    private static void PresetClipboard()
    {
        Translation.Values["MS_Shelf_NeedCopiedPreset"] = "需要先复制预设";
        var f = new Fixture();
        Command_Action paste = PresetCommand(f.Shelf, "MS_Shelf_PastePreset");
        Equal(true, paste.disabled, "paste starts disabled without a copied preset");
        Equal("需要先复制预设", paste.disabledReason, "disabled paste explains the required copy");
        f.Shelf.SetSelection(0, f.X); f.Shelf.SetRestockLimit(0, 12);
        f.Shelf.SetSelection(1, f.Y); f.Shelf.SetRestockLimit(1, 0);
        f.Shelf.GetStoreSettings().Priority = StoragePriority.Low;
        StorageShelfFilter sourceFilter = f.Shelf.SlotFilter(0);
        var steel = new ThingDef();
        sourceFilter.Quality.min = QualityCategory.Excellent;
        sourceFilter.HitPoints = new FloatRange(0.6f, 0.9f);
        sourceFilter.AnyMaterial = false; sourceFilter.Materials.Add(steel);
        sourceFilter.WarnWhenRestockFails = true;
        Command_Action copy = PresetCommand(f.Shelf, "MS_Shelf_CopyPreset");
        Equal(false, copy.alsoClickIfOtherInGroupClicked, "grouped copy snapshots only its representative shelf");
        copy.action();

        // Changes after copying must not alter the clipboard snapshot.
        sourceFilter.Materials.Clear(); sourceFilter.Quality.min = QualityCategory.Awful;
        f.Shelf.SetSelection(0, f.Z); f.Shelf.GetStoreSettings().Priority = StoragePriority.Critical;
        var targets = new[]
        {
            new Building_StorageSupplyShelf { def = f.Shelf.def, Faction = Faction.OfPlayer },
            new Building_StorageSupplyShelf { def = f.Shelf.def, Faction = Faction.OfPlayer }
        };
        foreach (Building_StorageSupplyShelf target in targets)
        {
            target.SetSelection(0, f.Y); target.SetSelection(1, f.X); target.SetSelection(2, f.Z);
            paste = PresetCommand(target, "MS_Shelf_PastePreset");
            Equal(false, paste.disabled, "copied preset enables paste on other shelves");
            Equal(true, paste.alsoClickIfOtherInGroupClicked, "paste participates in native multi-selection group activation");
            paste.action();
            Equal(f.X, target.SelectedDef(0), "paste replaces swapped first selection");
            Equal(f.Y, target.SelectedDef(1), "paste replaces swapped second selection");
            Equal<ThingDef>(null, target.SelectedDef(2), "empty preset slot clears previous selection");
            Equal(12, target.RestockLimit(0), "copied restock quantity retained");
            Equal(0, target.RestockLimit(1), "paused restocking retained");
            Equal(StoragePriority.Low, target.GetStoreSettings().Priority, "copied priority retained");
            StorageShelfFilter filter = target.SlotFilter(0);
            Equal(QualityCategory.Excellent, filter.Quality.min, "quality snapshot survives source edits");
            Equal(0.6f, filter.HitPoints.min, "minimum condition retained");
            Equal(0.9f, filter.HitPoints.max, "maximum condition retained");
            Equal(false, filter.AnyMaterial, "material restriction retained");
            Equal(steel, filter.Materials.Single(), "material snapshot survives source edits");
            Equal(true, filter.WarnWhenRestockFails, "restock warning preference retained");
        }
        targets[0].SlotFilter(0).Materials.Clear(); targets[0].SetRestockLimit(0, 1);
        Equal(steel, targets[1].SlotFilter(0).Materials.Single(), "pasted shelves have independent material lists");
        PresetCommand(targets[0], "MS_Shelf_PastePreset").action();
        Equal(steel, targets[0].SlotFilter(0).Materials.Single(), "editing a pasted shelf leaves clipboard reusable");
        Equal(12, targets[0].RestockLimit(0), "repeated paste restores snapshot quantity");
        var empty = new Building_StorageSupplyShelf { def = f.Shelf.def, Faction = Faction.OfPlayer };
        PresetCommand(empty, "MS_Shelf_CopyPreset").action();
        PresetCommand(targets[0], "MS_Shelf_PastePreset").action();
        Equal(true, Enumerable.Range(0, 3).All(slot => targets[0].SelectedDef(slot) == null), "copying an empty shelf replaces the previous clipboard preset");
    }

    private static long copiedStackReferences;
    private static void BeforeSnapshot(List<Thing> result, out int __state) { __state = result.Count; }
    private static void AfterSnapshot(List<Thing> result, int __state) { copiedStackReferences += result.Count - __state; }

    private static void StreamingRefill()
    {
        const string patchId = "magicstorage.tests.shelf-snapshots";
        var harmony = new Harmony(patchId);
        harmony.Patch(AccessTools.Method(typeof(StorageNetwork), nameof(StorageNetwork.GetStacks)),
            prefix: new HarmonyMethod(typeof(StorageSupplyShelfTests), nameof(BeforeSnapshot)),
            postfix: new HarmonyMethod(typeof(StorageSupplyShelfTests), nameof(AfterSnapshot)));
        try
        {
            var large = new Fixture(10000); large.Shelf.SetSelection(0, large.X); large.Shelf.SetRestockLimit(0, 1);
            for (int i = 0; i < 10000; i++) large.Store(large.X, 1);
            var snapshot = new List<Thing>(); copiedStackReferences = 0;
            large.Core.Network.GetStacks(large.X, snapshot);
            Equal(10000L, copiedStackReferences, "snapshot observer verifies all copied references in control path");
            copiedStackReferences = 0; Thing.StackCountReads = 0; large.Tick();
            Equal(1, large.Stock(large.X).stackCount, "large inventory only supplies one requested item");
            Equal(0L, copiedStackReferences, "shelf never requests a complete same-def stack snapshot");
            Equal(true, Thing.StackCountReads < 200, "one-item refill does not inspect quantities of ten thousand remaining stacks");
            Equal(9999L, large.Core.Network.GetTotalCount(large.X), "unvisited stock retained in network");

            var f = new Fixture(); f.Shelf.SetSelection(0, f.X); f.Shelf.SetRestockLimit(0, 12);
            var second = new Building_StorageUnit { SlotCapacity = 64 };
            second.Map = f.Map; second.Position = new IntVec3(3, 0); second.Spawned = true; second.Faction = Faction.OfPlayer;
            second.node = new CompStorageNode { parent = second }; f.Manager.Register(second.node);
            f.Store(f.X, 2); f.Store(f.X, 2); f.Store(f.X, 2);
            for (int i = 0; i < 3; i++) second.Inventory.Contents.TryAdd(new Thing { def = f.X, stackCount = 2 }, false);
            f.Tick();
            Equal(12, f.Stock(f.X).stackCount, "stream crosses units while consuming all tiny stacks");
            Equal(0L, f.Core.Network.GetTotalCount(f.X), "removing final stack from each unit safely removes its def bucket");
            Equal(0, f.Unit.Inventory.UsedSlots, "first unit emptied");
            Equal(0, second.Inventory.UsedSlots, "second unit emptied");
            Equal(0L, f.Pending(), "streamed success leaves no recovery stock");
        }
        finally { harmony.UnpatchAll(patchId); }
    }

    private static void StreamingRollback()
    {
        var f = new Fixture(); f.Shelf.SetSelection(0, f.X); f.Shelf.SetRestockLimit(0, 5);
        f.Shelf.SlotFilter(0).WarnWhenRestockFails = true;
        for (int i = 0; i < 20; i++) f.Store(f.X, 5, i.ToString());
        f.Map.DropBudget = 0; Thing.StackCountReads = 0; f.Tick();
        Equal(true, Thing.StackCountReads < 2500, "failed full-stack rollback terminates without repeated attempts on returned references");
        Equal(100L, f.Core.Network.GetTotalCount(f.X), "failed streamed attempts restore exact physical count");
        Equal(20, f.Unit.Inventory.UsedSlots, "different returned variants remain stored");
        Equal(0L, f.Pending(), "all failed drops returned without hidden remainder");
        Equal(StorageShelfFailureReason.PlacementFailed, f.Shelf.RestockFailure(0).Reason, "stream preserves placement failure diagnosis");
        f.Map.DropBudget = int.MaxValue; f.Tick();
        Equal(5, f.Stock(f.X).stackCount, "subsequent refill succeeds after placement becomes possible");
        Equal(95L, f.Core.Network.GetTotalCount(f.X), "successful retry removes exactly one stack");

        f = new Fixture(); f.Shelf.SetSelection(0, f.X); f.Shelf.SetRestockLimit(0, 12);
        f.Store(f.X, 5); f.Store(f.X, 5); f.Store(f.X, 5); f.Map.DropBudget = 5;
        f.Shelf.SlotFilter(0).WarnWhenRestockFails = true; f.Tick();
        Equal(5, f.Stock(f.X).stackCount, "first successful source remains on shelf after later failures");
        Equal(10L, f.Core.Network.GetTotalCount(f.X), "later compatible rollback merges retain remaining stock");
        Equal(0L, f.Pending(), "merged rollback leaves no staging remainder");
        Equal(StorageShelfFailureReason.PlacementFailed, f.Shelf.RestockFailure(0).Reason, "partial refill still reports placement failure");
    }

    private static void StreamingNetworkChange()
    {
        var f = new Fixture(); Thing first = f.Store(f.X, 2); f.Store(f.X, 3);
        int visits = 0;
        foreach (Thing source in f.Core.Network.EnumerateStacksForTransfer(f.X))
        {
            visits++;
            Equal(first, source, "stream begins with the current def bucket's first stack");
            f.Manager.Unregister(f.Core.node);
        }
        Equal(1, visits, "invalidated network stops stream before another source is yielded");
        Equal(5L, f.Unit.Inventory.GetTotalCount(f.X), "network change during traversal never removes items");
    }
    private static void Configuration()
    {
        var f = new Fixture();
        Equal(1, f.Shelf.AllSlotCellsList().Count, "single physical cell");
        Equal(false, f.Shelf.HaulDestinationEnabled, "native incoming hauling disabled");
        Equal(StoragePriority.Critical, f.Shelf.GetStoreSettings().Priority, "new shelf defaults to critical priority");
        Equal(true, f.Shelf.SetSelection(0, f.X), "first selection");
        Equal(true, f.Shelf.SetSelection(1, f.X), "duplicate selections allowed");
        Equal(f.X, f.Shelf.SelectedDef(1), "duplicate selection retained");
        f.Shelf.SetSelection(1, null);
        Equal(false, f.Shelf.SetSelection(-1, f.X), "negative slot rejected");
        Equal(false, f.Shelf.SetSelection(3, f.X), "fourth slot rejected");
        Equal(false, f.Shelf.SetSelection(1, new ThingDef()), "parent shelf filter enforced");
        f.Y.specialDisallowed = true;
        Equal(false, f.Shelf.SetSelection(1, f.Y), "native special exclusions enforced");
        f.Y.specialDisallowed = false; f.Y.PlayerAcquirable = false;
        Equal(false, f.Shelf.SetSelection(1, f.Y), "non-player items excluded");
        f.Y.PlayerAcquirable = true; f.Y.virtualDefParent = f.X;
        Equal(false, f.Shelf.SetSelection(1, f.Y), "virtual defs excluded");
        f.Y.virtualDefParent = null;
        Equal(true, f.Shelf.SetSelection(0, null), "selection can be cleared");
        f.Shelf.GetStoreSettings().Priority = StoragePriority.Low;
        f.Shelf.SetSelection(0, f.X);
        Equal(StoragePriority.Low, f.Shelf.GetStoreSettings().Priority, "changing selection preserves custom priority");
        f.Shelf.SetSelection(0, null);
        Equal(StoragePriority.Low, f.Shelf.GetStoreSettings().Priority, "clearing selection preserves custom priority");
        Scribe.mode = LoadSaveMode.PostLoadInit;
        try { f.Shelf.ExposeData(); }
        finally { Scribe.mode = LoadSaveMode.Inactive; }
        Equal(StoragePriority.Low, f.Shelf.GetStoreSettings().Priority, "load normalization preserves priority");
        f.Shelf.SpawnSetup(f.Map, true);
        Equal(StoragePriority.Low, f.Shelf.GetStoreSettings().Priority, "map spawning preserves priority");
        Equal(false, f.Shelf.HaulDestinationEnabled, "low priority does not enable incoming hauling");
    }
    private static void Restocking()
    {
        var f = new Fixture();
        f.Shelf.SetSelection(0, f.X); f.Shelf.SetSelection(1, f.Y); f.Shelf.SetSelection(2, f.Z);
        Thing source = f.Store(f.X, 75); f.Store(f.X, 75); f.Store(f.Y, 7); f.Store(f.Z, 1);
        f.Tick();
        Equal(3, f.Shelf.GetSlotGroup().HeldThingsCount, "three configured real stacks");
        Equal(source, f.Stock(f.X), "actual object transferred without cloning");
        Equal(true, source.Spawned && source.holdingOwner == null, "ordinary spawned item exposed to native AI");
        Equal(true, f.Shelf.Accepts(source), "stock has native storage priority");
        Equal(false, f.Shelf.Accepts(new Thing { def = f.X }), "unspawned source not treated as existing stock");
        Equal(75, f.Stock(f.X).stackCount, "full stack limit");
        Equal(7, f.Stock(f.Y).stackCount, "partial restock when network short");
        Equal(1, f.Stock(f.Z).stackCount, "nonstackable item uses one place");
        Equal(75L, f.Core.Network.GetTotalCount(f.X), "network count updated");
        f.Tick(); Equal(3, f.Shelf.GetSlotGroup().HeldThingsCount, "full shelf is idempotent");
        Thing carried = source.SplitOff(20); var inventory = new ThingOwner(null); inventory.TryAdd(carried);
        f.Tick();
        Equal(source, f.Stock(f.X), "native stock object retained during top-up");
        Equal(75, source.stackCount, "consumed amount replenished");
        Equal(55L, f.Core.Network.GetTotalCount(f.X), "only deficit withdrawn");
        Equal(150L, f.Core.Network.GetTotalCount(f.X) + source.stackCount + carried.stackCount, "quantity conserved");
        Equal(0L, f.Pending(), "successful supply leaves no hidden items");
    }
    private static void RestockLimits()
    {
        var f = new Fixture();
        f.Shelf.SetSelection(0, f.X); f.Shelf.SetSelection(1, f.Y); f.Shelf.SetSelection(2, f.Z);
        Equal(75, f.Shelf.RestockLimit(0), "new selection defaults to stack limit");
        Equal(10, f.Shelf.RestockLimit(1), "each type has its own default");
        Equal(false, f.Shelf.SetRestockLimit(3, 4), "invalid slot limit rejected");
        f.Shelf.SetRestockLimit(0, 12); f.Shelf.SetRestockLimit(1, 3); f.Shelf.SetRestockLimit(2, 0);
        f.Store(f.X, 75); f.Store(f.Y, 10); f.Store(f.Z, 1); f.Tick();
        Equal(12, f.Stock(f.X).stackCount, "first slot stocked to custom limit");
        Equal(3, f.Stock(f.Y).stackCount, "second slot independently limited");
        Equal<Thing>(null, f.Stock(f.Z), "zero limit pauses empty slot");
        Equal(63L, f.Core.Network.GetTotalCount(f.X), "only requested stock withdrawn");
        Equal(7L, f.Core.Network.GetTotalCount(f.Y), "second slot quantity conserved");
        Equal(1L, f.Core.Network.GetTotalCount(f.Z), "paused slot leaves network stock alone");
        Thing stock = f.Stock(f.X); stock.SplitOff(5); f.Tick();
        Equal(stock, f.Stock(f.X), "limited refill retains real stock reference");
        Equal(12, stock.stackCount, "refill stops at custom limit");
        Equal(58L, f.Core.Network.GetTotalCount(f.X), "only consumed deficit replaced");
        f.Shelf.SetRestockLimit(0, 4); f.Tick();
        Equal(12, stock.stackCount, "lowering limit does not remove stock");
        Equal(58L, f.Core.Network.GetTotalCount(f.X), "lowered limit does not over-withdraw");
        stock.SplitOff(9); f.Tick();
        Equal(4, stock.stackCount, "stock below lowered limit refilled to new target");
        f.Shelf.SetRestockLimit(0, 0); stock.SplitOff(2); f.Tick();
        Equal(2, stock.stackCount, "zero limit preserves remaining stock without refill");
        f.Shelf.SetRestockLimit(0, 20); f.Tick();
        Equal(20, stock.stackCount, "raising limit resumes and adds exact deficit");
        f.Shelf.SetSelection(0, f.X);
        Equal(20, f.Shelf.RestockLimit(0), "selecting same item preserves limit");
        f.Shelf.SetRestockLimit(1, int.MaxValue);
        Equal(10, f.Shelf.RestockLimit(1), "limit cannot exceed stack size");
        f.Shelf.SetRestockLimit(1, -1);
        Equal(0, f.Shelf.RestockLimit(1), "negative limit clamped to zero");
        Scribe.mode = LoadSaveMode.PostLoadInit;
        try { f.Shelf.ExposeData(); }
        finally { Scribe.mode = LoadSaveMode.Inactive; }
        Equal(20, f.Shelf.RestockLimit(0), "load normalization preserves configured limit");
        Equal(0, f.Shelf.RestockLimit(1), "load normalization preserves intentional zero");
        f.Shelf.SetSelection(1, null);
        Equal(0, f.Shelf.RestockLimit(1), "cleared selection resets limit");
        Equal(false, f.Shelf.SetRestockLimit(1, 5), "unconfigured slot cannot acquire a limit");
        f.Shelf.SetSelection(0, null); f.Shelf.SetSelection(0, f.Y);
        Equal(10, f.Shelf.RestockLimit(0), "changed item starts with its own stack size");
    }
    private static void StackVariants()
    {
        var f = new Fixture(); f.Shelf.SetSelection(0, f.X);
        f.Store(f.X, 20, "a"); f.Store(f.X, 40, "b"); f.Store(f.X, 30, "a");
        f.Tick();
        Equal(1, f.Shelf.GetSlotGroup().HeldThingsCount, "different quality or material cannot create second same-def stack");
        Equal("a", f.Stock(f.X).variant, "first compatible variant chosen");
        Equal(50, f.Stock(f.X).stackCount, "compatible sources combine");
        Equal(40L, f.Core.Network.GetTotalCount(f.X), "incompatible source remains in network");
        f.Tick(); Equal(50, f.Stock(f.X).stackCount, "incompatible source not used for top-up");
    }
    private static void Reservations()
    {
        var f = new Fixture(); f.Shelf.SetSelection(0, f.X); Thing source = f.Store(f.X, 75);
        object claim = new object(); f.Core.Network.ReserveOutgoing(claim, source, 25);
        f.Tick();
        Equal(50, f.Stock(f.X).stackCount, "network claim excluded from supply");
        Equal(25L, f.Core.Network.GetTotalCount(f.X), "reserved material remains");
        f.Core.Network.ReleaseOutgoing(claim);
        Thing stock = f.Stock(f.X); f.Map.reservationManager.Reserved.Add(stock);
        f.Tick(); Equal(50, stock.stackCount, "native reservation prevents top-up");
        f.Map.reservationManager.Reserved.Clear(); f.Map.physicalInteractionReservationManager.Reserved.Add(stock);
        f.Tick(); Equal(50, stock.stackCount, "physical interaction prevents top-up");
        f.Map.physicalInteractionReservationManager.Reserved.Clear(); stock.forbidden = true;
        f.Tick(); Equal(50, stock.stackCount, "forbidden stack left alone");
        stock.forbidden = false; f.Tick(); Equal(75, stock.stackCount, "restocking resumes after release");
    }
    private static void ConfigurationChanges()
    {
        var f = new Fixture(); f.Shelf.SetSelection(0, f.X); f.Store(f.X, 20); f.Store(f.Y, 7); f.Tick();
        Thing old = f.Stock(f.X); f.Map.reservationManager.Reserved.Add(old);
        f.Shelf.SetSelection(0, f.Y); f.Tick();
        Equal(true, old.Spawned, "configuration change preserves reserved object");
        Equal(true, f.Shelf.Accepts(old), "reserved old stock remains protected from auto hauling");
        f.Map.reservationManager.Reserved.Clear(); f.Tick();
        Equal(false, old.Spawned, "released old stock returned");
        Equal(20L, f.Core.Network.GetTotalCount(f.X), "old quantity returned to network");
        Equal(7, f.Stock(f.Y).stackCount, "replacement supplied");
        f.Core.Settings.allow = false; f.Shelf.SetSelection(0, null); f.Tick();
        Equal(0, f.Shelf.GetSlotGroup().HeldThingsCount, "rejected old stock removed from shelf");
        Equal(7, f.Map.Ground.Where(t => t.def == f.Y).Sum(t => t.stackCount), "rejected stock placed nearby");
        Equal(false, f.Map.Ground.Any(t => t.Position == f.Shelf.Position), "cleanup cannot put old stock straight back on shelf");

        var blocked = new Fixture(); blocked.Shelf.SetSelection(0, blocked.X); blocked.Store(blocked.X, 30); blocked.Tick();
        blocked.Core.Settings.allow = false; blocked.Map.DropBudget = 0; blocked.Shelf.SetSelection(0, null); blocked.Tick();
        Equal(30L, blocked.Pending(), "failed cleanup placement preserved in saved recovery");
        blocked.Map.DropBudget = int.MaxValue;
        Find.TickManager.TicksGame = ((Find.TickManager.TicksGame / 250) + 1) * 250;
        blocked.Manager.MapComponentTick();
        Equal(0L, blocked.Pending(), "cleanup recovery retried");
        Equal(false, blocked.Map.Ground.Any(t => t.Position == blocked.Shelf.Position), "retry still excludes shelf cell");
    }
    private static void NetworkAndPlacement()
    {
        var f = new Fixture(); f.Shelf.SetSelection(0, f.X); f.Store(f.X, 60);
        f.Map.ShelfBlocked = true; f.Tick();
        Equal(60L, f.Core.Network.GetTotalCount(f.X), "blocked shelf does not withdraw");
        f.Map.ShelfBlocked = false; f.Map.DropBudget = 0; f.Tick();
        Equal(60L, f.Core.Network.GetTotalCount(f.X), "failed placement rolls back to network");
        Equal(0L, f.Pending(), "placement rollback leaves no hidden stock");
        f.Map.DropBudget = int.MaxValue; f.Map.ShelfFire = true; f.Tick();
        Equal(0, f.Shelf.GetSlotGroup().HeldThingsCount, "burning cell not supplied");
        f.Map.ShelfFire = false; f.Shelf.forbidden = true; f.Tick();
        Equal(0, f.Shelf.GetSlotGroup().HeldThingsCount, "forbidden shelf not supplied");
        f.Shelf.forbidden = false; f.Tick(); Thing stock = f.Stock(f.X);
        f.Store(f.X, 15); f.Manager.Unregister(f.Core.node); f.Tick();
        Equal(stock, f.Stock(f.X), "disconnection keeps usable stock");
        Equal(60, stock.stackCount, "disconnection stops top-up");
        f.Manager.Register(f.Core.node); f.Tick(); Equal(75, stock.stackCount, "reconnection resumes supply");
        var other = new Building_StorageCore { Map = f.Map, Position = new IntVec3(3, 0), Spawned = true, Faction = Faction.OfPlayer };
        other.node = new CompStorageNode { parent = other }; f.Manager.Register(other.node);
        stock.SplitOff(10); f.Tick(); Equal(65, stock.stackCount, "two cores stop supply");
    }
    private static void Removal()
    {
        var f = new Fixture(); f.Shelf.SetSelection(0, f.X); f.Shelf.SetSelection(1, f.Y);
        f.Store(f.X, 25); f.Store(f.Y, 8); f.Tick(); f.Map.DropBudget = 0;
        f.Shelf.DeSpawn(DestroyMode.KillFinalize);
        Equal(33L, f.Pending(), "destruction preserves both stacks when drops blocked");
        Equal(0, f.Map.Ground.Count, "destruction does not leave orphaned spawned stacks");
        f.Map.DropBudget = int.MaxValue;
        Find.TickManager.TicksGame = ((Find.TickManager.TicksGame / 250) + 1) * 250;
        f.Manager.MapComponentTick();
        Equal(33, f.Map.Ground.Sum(t => t.stackCount), "destroyed shelf contents eventually dropped without loss");
    }
    private static void Filters()
    {
        var f = new Fixture(); f.X.MadeFromStuff = f.X.hasQualityComp = true;
        var steel = new ThingDef(); var wood = new ThingDef();
        f.Shelf.SetSelection(0, f.X); f.Shelf.SetSelection(1, f.Y); f.Shelf.SetRestockLimit(0, 4);
        StorageShelfFilter filter = f.Shelf.SlotFilter(0);
        filter.Quality = new QualityRange { min = QualityCategory.Good, max = QualityCategory.Legendary };
        filter.HitPoints = new FloatRange(0.8f, 1f); filter.AnyMaterial = false; filter.Materials.Add(steel);
        Thing poor = f.Store(f.X, 6, "poor"); poor.hasQuality = true; poor.quality = QualityCategory.Poor; poor.Stuff = steel;
        Thing damaged = f.Store(f.X, 7, "damaged"); damaged.hasQuality = true; damaged.quality = QualityCategory.Good; damaged.Stuff = steel; damaged.HitPoints = 60;
        Thing wrongStuff = f.Store(f.X, 8, "wood"); wrongStuff.hasQuality = true; wrongStuff.quality = QualityCategory.Good; wrongStuff.Stuff = wood;
        Thing accepted = f.Store(f.X, 9, "good steel"); accepted.hasQuality = true; accepted.quality = QualityCategory.Good; accepted.Stuff = steel; accepted.HitPoints = 90;
        f.Tick(); Thing stock = f.Stock(f.X);
        Equal(4, stock.stackCount, "filtered restock still respects quantity limit");
        Equal(QualityCategory.Good, stock.quality, "quality retained through actual split");
        Equal(90, stock.HitPoints, "condition retained through actual split");
        Equal(steel, stock.Stuff, "allowed material selected");
        Equal(6, poor.stackCount, "poor quality untouched");
        Equal(7, damaged.stackCount, "damaged item untouched");
        Equal(8, wrongStuff.stackCount, "wrong material untouched");
        Equal(26L, f.Core.Network.GetTotalCount(f.X), "filtered withdrawal conserves count");
        Equal(QualityCategory.Awful, f.Shelf.SlotFilter(1).Quality.min, "other slot has independent quality settings");
        f.Shelf.SetSelection(0, f.X);
        Equal(filter, f.Shelf.SlotFilter(0), "same selection retains filters");
        Thing excellent = f.Store(f.X, 10, "excellent steel"); excellent.hasQuality = true; excellent.quality = QualityCategory.Excellent; excellent.Stuff = steel; excellent.HitPoints = 90;
        filter.Quality.min = QualityCategory.Excellent;
        f.Map.reservationManager.Reserved.Add(stock); f.Tick();
        Equal(stock, f.Stock(f.X), "rejected reserved stock is not replaced");
        Equal(1, f.Shelf.GetSlotGroup().HeldThingsCount, "reserved rejected stock prevents a second same-def stack");
        Equal(10, excellent.stackCount, "replacement not extracted while old stock reserved");
        f.Map.reservationManager.Reserved.Clear(); stock.forbidden = true; f.Tick();
        Equal(stock, f.Stock(f.X), "rejected forbidden stock is left alone");
        Equal(1, f.Shelf.GetSlotGroup().HeldThingsCount, "forbidden rejected stock also prevents duplicate supply");
        stock.forbidden = false; f.Tick();
        Equal(false, stock.Spawned, "released rejected stock removed safely");
        Equal(QualityCategory.Excellent, f.Stock(f.X).quality, "replacement obeys new quality filter");
        Equal(4, f.Stock(f.X).stackCount, "replacement still bounded");
        Equal(36L, f.Core.Network.GetTotalCount(f.X), "rejected stock returned before replacement");
        filter.Materials.Clear(); f.Tick();
        Equal<Thing>(null, f.Stock(f.X), "empty allowed material list prevents supply");
        Equal(40L, f.Core.Network.GetTotalCount(f.X), "excluded stock returned without loss");
        filter.AnyMaterial = true; f.Tick();
        Equal(4, f.Stock(f.X).stackCount, "allow-any-material resumes supply");
        f.Shelf.SetSelection(0, null); f.Shelf.SetSelection(0, f.X);
        Equal(true, f.Shelf.SlotFilter(0).AnyMaterial, "changed selection resets material filter");
        Equal(QualityCategory.Awful, f.Shelf.SlotFilter(0).Quality.min, "changed selection resets quality filter");
        f.Y.useHitPoints = false;
        var plain = new Thing { def = f.Y, HitPoints = 0 };
        StorageShelfFilter plainFilter = f.Shelf.SlotFilter(1);
        plainFilter.Quality.min = QualityCategory.Legendary; plainFilter.HitPoints = new FloatRange(0.9f, 1f); plainFilter.AnyMaterial = false;
        Equal(true, plainFilter.Allows(plain), "inapplicable attributes do not reject plain items");
    }
    private static void Warnings()
    {
        var f = new Fixture(); f.Shelf.SetSelection(0, f.X);
        Equal(false, f.Shelf.SlotFilter(0).WarnWhenRestockFails, "warning defaults off");
        f.Tick(); Equal<StorageShelfRestockFailure>(null, f.Shelf.RestockFailure(0), "unmonitored shortage has no warning");
        f.Shelf.SlotFilter(0).WarnWhenRestockFails = true; f.Shelf.SetRestockLimit(0, 12); f.Tick();
        Equal(StorageShelfFailureReason.NoStock, f.Shelf.RestockFailure(0).Reason, "empty network shortage reported");
        Equal(12, f.Shelf.RestockFailure(0).Target, "report retains target");
        f.Store(f.X, 5); f.Tick();
        Equal(5, f.Shelf.RestockFailure(0).Current, "partial replenishment reports actual count");
        Equal(StorageShelfFailureReason.NoStock, f.Shelf.RestockFailure(0).Reason, "partial replenishment still shortage");
        f.Store(f.X, 7); f.Tick();
        Equal<StorageShelfRestockFailure>(null, f.Shelf.RestockFailure(0), "full replenishment clears warning");
        Thing stock = f.Stock(f.X); stock.SplitOff(4); f.Map.reservationManager.Reserved.Add(stock); f.Tick();
        Equal<StorageShelfRestockFailure>(null, f.Shelf.RestockFailure(0), "normal reserved pickup not reported as failure");
        f.Map.reservationManager.Reserved.Clear(); f.Tick();
        Equal(StorageShelfFailureReason.NoStock, f.Shelf.RestockFailure(0).Reason, "released short stock checked again");
        f.Shelf.SlotFilter(0).WarnWhenRestockFails = false;
        Equal<StorageShelfRestockFailure>(null, f.Shelf.RestockFailure(0), "disabling option hides cached warning immediately");
        f.Shelf.SlotFilter(0).WarnWhenRestockFails = true; f.Shelf.SetRestockLimit(0, 0);
        Equal<StorageShelfRestockFailure>(null, f.Shelf.RestockFailure(0), "intentional pause clears cached warning");
        f.Tick(); Equal<StorageShelfRestockFailure>(null, f.Shelf.RestockFailure(0), "paused slot never warned");
        f.Shelf.SetSelection(0, null); f.Tick();
        Equal<StorageShelfRestockFailure>(null, f.Shelf.RestockFailure(0), "unconfigured slot never warned");

        f = new Fixture(); f.Shelf.SetSelection(0, f.X); f.Shelf.SlotFilter(0).WarnWhenRestockFails = true;
        f.X.hasQualityComp = true; f.Shelf.SlotFilter(0).Quality.min = QualityCategory.Excellent;
        Thing poor = f.Store(f.X, 30); poor.hasQuality = true; poor.quality = QualityCategory.Poor;
        f.Tick(); Equal(StorageShelfFailureReason.FilterMismatch, f.Shelf.RestockFailure(0).Reason, "filter mismatch distinguished from no inventory");
        f.Shelf.SlotFilter(0).Quality.min = QualityCategory.Awful;
        Equal<StorageShelfRestockFailure>(null, f.Shelf.RestockFailure(0), "changed filter suppresses obsolete diagnostic");
        f.Tick(); Equal(30, f.Shelf.RestockFailure(0).Current, "new settings evaluated on next supply cycle");
        f.Shelf.forbidden = true; f.Tick();
        Equal<StorageShelfRestockFailure>(null, f.Shelf.RestockFailure(0), "intentionally forbidden shelf does not warn");
        f.Shelf.forbidden = false;

        f = new Fixture(); f.Shelf.SetSelection(0, f.X); f.Shelf.SlotFilter(0).WarnWhenRestockFails = true;
        Thing source = f.Store(f.X, 75); object claim = new object(); f.Core.Network.ReserveOutgoing(claim, source, 75);
        f.Tick(); Equal(StorageShelfFailureReason.ReservedStock, f.Shelf.RestockFailure(0).Reason, "unavailable source reservation distinguished");
        f.Core.Network.ReleaseOutgoing(claim); f.Map.ShelfBlocked = true; f.Tick();
        Equal(StorageShelfFailureReason.NoSpace, f.Shelf.RestockFailure(0).Reason, "placement cell rejection reported");
        Equal(75L, f.Core.Network.GetTotalCount(f.X), "blocked alert does not withdraw material");
        f.Map.ShelfBlocked = false; f.Map.DropBudget = 0; f.Tick();
        Equal(StorageShelfFailureReason.PlacementFailed, f.Shelf.RestockFailure(0).Reason, "failed actual drop distinguished");
        Equal(75L, f.Core.Network.GetTotalCount(f.X), "drop failure conserves source stock");
        f.Map.DropBudget = int.MaxValue; f.Map.ShelfFire = true; f.Tick();
        Equal(StorageShelfFailureReason.Burning, f.Shelf.RestockFailure(0).Reason, "burning shelf reported");
        f.Map.ShelfFire = false; f.Manager.Unregister(f.Core.node); f.Tick();
        Equal(StorageShelfFailureReason.NetworkUnavailable, f.Shelf.RestockFailure(0).Reason, "disconnected network reported");
        f.Manager.Register(f.Core.node); f.Tick();
        Equal<StorageShelfRestockFailure>(null, f.Shelf.RestockFailure(0), "network recovery and full supply clear warning");
        f.Manager.Unregister(f.Core.node); f.Tick();
        Equal<StorageShelfRestockFailure>(null, f.Shelf.RestockFailure(0), "full shelf does not warn solely because network disabled");
    }
    private static void WarningReports()
    {
        Translation.Values["MS_Shelf_AlertOne"] = "补货失败：{0}";
        Translation.Values["MS_Shelf_AlertMany"] = "多个物品补货失败";
        Translation.Values["MS_Shelf_AlertSeparator"] = "、";
        Translation.Values["MS_Shelf_AlertDetail"] = "{0}, slot {1}: {2}/{3}, missing {4}; {5}; {6}, {8}, {7}";
        Find.Maps.Clear();
        var f = new Fixture(); f.X.label = "food"; f.Y.label = "medicine"; f.Z.label = "shell";
        Find.Maps.Add(f.Map); f.Shelf.SetSelection(0, f.X); f.Shelf.SlotFilter(0).WarnWhenRestockFails = true; f.Tick();
        var alert = new Alert_StorageShelfRestockFailed(); AlertReport report = alert.GetReport();
        Equal(true, report.active, "native alert report active for selected shortage");
        Equal(f.Shelf, report.culpritsThings.Single(), "click target is failing shelf");
        Equal("补货失败：food", alert.GetLabel(), "single failed item shown by name");
        Equal(true, alert.GetExplanation().ToString().Contains("missing 75"), "explanation contains shortage amount");
        Equal(true, alert.GetExplanation().ToString().Contains("test map"), "explanation contains map location");
        f.Shelf.SetSelection(1, f.Y); f.Shelf.SetSelection(2, f.Z);
        f.Shelf.SlotFilter(1).WarnWhenRestockFails = f.Shelf.SlotFilter(2).WarnWhenRestockFails = true; f.Tick();
        report = alert.GetReport();
        Equal(1, report.culpritsThings.Count, "several failing slots have one shelf jump target");
        Equal("补货失败：food、medicine、shell", alert.GetLabel(), "up to three item names retained");
        var other = new Fixture(); other.X.label = "clothes";
        Find.Maps.Add(other.Map); other.Shelf.SetSelection(0, other.X); other.Shelf.SlotFilter(0).WarnWhenRestockFails = true; other.Tick();
        report = alert.GetReport();
        Equal(2, report.culpritsThings.Count, "failures across maps have distinct shelf targets");
        Equal("多个物品补货失败", alert.GetLabel(), "more than three item types use compact label");
        Equal(true, alert.GetExplanation().ToString().Contains("clothes"), "compact label does not omit detail");
        other.Shelf.SlotFilter(0).WarnWhenRestockFails = false;
        alert.GetReport(); Equal("补货失败：food、medicine、shell", alert.GetLabel(), "disabled warning removed without another supply tick");
        f.Shelf.SetSelection(2, null); alert.GetReport();
        Equal("补货失败：food、medicine", alert.GetLabel(), "cleared selection removed from alert");
        f.Shelf.SlotFilter(0).WarnWhenRestockFails = f.Shelf.SlotFilter(1).WarnWhenRestockFails = false;
        Equal(false, alert.GetReport().active, "all disabled warnings remove native alert");
        f.Shelf.SlotFilter(0).WarnWhenRestockFails = true; f.Tick();
        f.Shelf.DeSpawn(); Equal(false, alert.GetReport().active, "removed shelf no longer reported");
        Equal(0, f.Manager.SupplyShelves.Count, "removed shelf no longer registered for reports");
        Find.Maps.Clear();
    }
}
