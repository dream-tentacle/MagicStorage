using System;
using System.Collections.Generic;
using System.Linq;
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
        internal Fixture(int slots = 64)
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Unit.SlotCapacity = slots;
            var parent = new StorageSettings();
            foreach (ThingDef def in new[] { X, Y, Z }) parent.filter.SetAllow(def, true);
            Shelf.def.building = new BuildingProperties { fixedStorageSettings = parent };
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
        }
        internal Thing Store(ThingDef def, int count, string variant = null)
        {
            var item = new Thing { def = def, stackCount = count, variant = variant };
            Unit.Inventory.Contents.TryAdd(item, false); return item;
        }
        internal Thing Stock(ThingDef def) => Shelf.GetSlotGroup().HeldThings.FirstOrDefault(t => t.def == def);
        internal void Tick() => Shelf.TickRare();
        internal long Pending()
        {
            var holders = new List<IThingHolder>(); Manager.GetChildHolders(holders);
            return holders.Cast<StorageRecoveryBatch>().Sum(h => h.PendingCount);
        }
    }
    internal static int Run()
    {
        Configuration(); Restocking(); RestockLimits(); StackVariants(); Reservations(); ConfigurationChanges(); NetworkAndPlacement(); Removal(); Filters(); Warnings(); WarningReports();
        return assertions;
    }
    private static void Configuration()
    {
        var f = new Fixture();
        Equal(1, f.Shelf.AllSlotCellsList().Count, "single physical cell");
        Equal(false, f.Shelf.HaulDestinationEnabled, "native incoming hauling disabled");
        Equal(StoragePriority.Critical, f.Shelf.GetStoreSettings().Priority, "new shelf defaults to critical priority");
        Equal(true, f.Shelf.SetSelection(0, f.X), "first selection");
        Equal(false, f.Shelf.SetSelection(1, f.X), "duplicate selection rejected");
        Equal<ThingDef>(null, f.Shelf.SelectedDef(1), "duplicate does not mutate configuration");
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
