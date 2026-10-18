using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using Verse;

internal static class StorageReceiverTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string label)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(label + ": " + expected + " != " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Building_StorageReceiver Receiver = new Building_StorageReceiver();
        internal readonly ThingDef X = new ThingDef();
        internal readonly ThingDef Y = new ThingDef();
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture(int slots = 64)
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Core.Map = Unit.Map = Receiver.Map = Map;
            Core.Faction = Unit.Faction = Receiver.Faction = Faction.OfPlayer;
            Core.Spawned = Unit.Spawned = Receiver.Spawned = true;
            Unit.SlotCapacity = slots;
            Core.node = new CompStorageNode { parent = Core };
            Unit.node = new CompStorageNode { parent = Unit };
            Receiver.node = new CompStorageNode { parent = Receiver };
            Map.haulDestinationManager.SlotParents[Receiver.Position] = Receiver;
            Manager.Register(Core.node); Manager.Register(Unit.node); Manager.Register(Receiver.node);
            Manager.FinalizeInit(); Tick();
        }
        internal void Tick(int ticks = 60)
        { Find.TickManager.TicksGame += ticks; Manager.MapComponentTick(); }
        internal Thing Deliver(ThingDef def, int count, string variant = null)
        {
            var item = new Thing { def = def, stackCount = count, variant = variant, Map = Map,
                Position = Receiver.Position, Spawned = true };
            Map.Ground.Add(item); Receiver.Notify_ReceivedThing(item);
            return item;
        }
        internal long Pending()
        {
            var holders = new List<IThingHolder>(); Manager.GetChildHolders(holders);
            long result = 0; foreach (StorageRecoveryBatch holder in holders) result += holder.PendingCount;
            return result;
        }
    }

    internal static int Run()
    {
        RuntimeHelpers.RunClassConstructor(typeof(StorageReceiverHaulingPatches).TypeHandle);
        NativeBoundary(); DeferredAndMixed(); FullAndPartial(); NetworkChanges(); PolicyAndReservations(); Removal();
        CapacityPlanning(); AutomaticIntake(); DeliveryCallbacks(); GodModeMapIntake();
        return assertions;
    }

    private static Command_Action MapIntakeCommand(Building_StorageCore core)
    {
        foreach (Gizmo gizmo in core.GetGizmos())
            if (gizmo is Command_Action command && command.defaultLabel == "DEV: 吸收全图") return command;
        return null;
    }

    private static void GodModeMapIntake()
    {
        try
        {
            var f = new Fixture(1);
            // Share the live map list so successful intake mutates the iterated source.
            f.Map.listerThings.Things = f.Map.Ground;
            f.Unit.Inventory.Contents.TryAdd(new Thing { def = f.X, stackCount = 65 });
            var blocked = f.Deliver(f.Y, 10);
            var forbidden = f.Deliver(f.X, 10); forbidden.forbidden = true;
            var first = f.Deliver(f.X, 4); first.Position = new IntVec3(20, 20);
            var partial = f.Deliver(f.X, 20); partial.Position = new IntVec3(30, 30);
            DebugSettings.godMode = false;
            Equal<Command_Action>(null, MapIntakeCommand(f.Core), "map intake hidden outside god mode");
            DebugSettings.godMode = true;
            Command_Action command = MapIntakeCommand(f.Core);
            Equal(true, command != null, "god mode exposes map intake");
            Equal<object>(null, command.icon, "map intake does not set an icon");
            command.action();
            Equal(true, blocked.Spawned, "incompatible first item stays on map when slots are full");
            Equal(10, blocked.stackCount, "failed intake preserves incompatible item count");
            Equal(true, forbidden.Spawned, "god mode intake respects forbidden flag");
            Equal(10, forbidden.stackCount, "forbidden stack quantity unchanged");
            Equal(false, first.Spawned, "later distant item absorbed after earlier capacity failure");
            Equal(75L, f.Core.Network.GetTotalCount(f.X), "later stacks fill remaining merge capacity");
            Equal(14, partial.stackCount, "partially accepted stack keeps exact remainder on map");
            Equal(true, partial.Spawned, "partial remainder stays spawned");
            Equal(1, f.Unit.Inventory.UsedSlots, "map intake never exceeds slot capacity");
            Equal(0L, f.Pending(), "map intake leaves no detached items in recovery");

            var filtered = new Fixture();
            filtered.Map.listerThings.Things = filtered.Map.Ground;
            var rejected = filtered.Deliver(filtered.X, 10);
            filtered.Core.Settings.allow = false;
            MapIntakeCommand(filtered.Core).action();
            Equal(true, rejected.Spawned, "map intake respects core filter");
            Equal(0L, filtered.Core.Network.GetTotalCount(filtered.X), "rejected item not stored");
            filtered.Core.Settings.allow = true;
            filtered.Manager.Unregister(filtered.Core.node);
            MapIntakeCommand(filtered.Core).action();
            Equal(true, rejected.Spawned, "unavailable network leaves map contents intact");
        }
        finally { DebugSettings.godMode = false; }
    }

    private static void NativeBoundary()
    {
        new MagicStorageMod(new ModContentPack());
        Equal(0, Log.Errors.Count, "bootstrap has no patch errors");
        Equal(true, Log.Messages.Exists(s => s.Contains("receivers use native hauling")), "native hauling bootstrap");
        var f = new Fixture();
        Equal(true, (object)f.Receiver is ISlotGroupParent, "receiver exposes native slot group");
        Equal(false, (object)f.Core is IHaulDestination, "core is no longer a hauling destination");
        Equal(1, f.Receiver.AllSlotCellsList().Count, "one real receiving cell");
        Equal<StorageSettings>(f.Core.Settings, f.Receiver.GetStoreSettings(), "shared core settings");
        int sorts = f.Map.haulDestinationManager.Sorts;
        f.Core.Settings.Priority = StoragePriority.Critical; f.Tick();
        Equal(StoragePriority.Critical, f.Receiver.GetStoreSettings().Priority, "core priority reaches receiver");
        Equal(true, f.Map.haulDestinationManager.Sorts > sorts, "priority change reorders native destinations");
        Verse.AI.HaulAIUtility.OriginalCalls = 0;
        Verse.AI.HaulAIUtility.HaulToStorageJob(new Pawn(), new Thing(), false);
        Equal(1, Verse.AI.HaulAIUtility.OriginalCalls, "native job factory is not intercepted");
    }

    private static void DeferredAndMixed()
    {
        var f = new Fixture();
        var x = f.Deliver(f.X, 30); var y = f.Deliver(f.Y, 40);
        Equal(0L, f.Core.Network.GetTotalCount(f.X), "spawn callback does not take items during placement");
        f.Manager.MapComponentTick();
        Equal(true, x.Spawned, "same-tick processing leaves placement intact");
        f.Tick();
        Equal(30L, f.Core.Network.GetTotalCount(f.X), "steel received");
        Equal(40L, f.Core.Network.GetTotalCount(f.Y), "wood received independently");
        Equal(x, f.Unit.Inventory.Contents[0], "real object transferred without cloning");
        Equal(0, f.Map.Ground.Count, "receiver emptied");
        Equal(0L, f.Pending(), "successful transfer leaves no recovery contents");
        f.Deliver(f.X, 20); f.Tick();
        Equal(50L, f.Core.Network.GetTotalCount(f.X), "second delivery merges in unit");
        Equal(2, f.Unit.Inventory.UsedSlots, "two item types occupy two slots");
    }

    private static void FullAndPartial()
    {
        var f = new Fixture(1);
        f.Unit.Inventory.Contents.TryAdd(new Thing { def = f.X, stackCount = 70 });
        var x = f.Deliver(f.X, 20); f.Tick();
        Equal(75L, f.Core.Network.GetTotalCount(f.X), "only remaining five capacity transferred");
        Equal(15, x.stackCount, "partial remainder stays in receiving cell");
        Equal(true, x.Spawned, "remainder remains spawned");
        Equal(false, f.Receiver.Accepts(new Thing { def = f.Y }), "full network rejects new type");
        f.Tick(60); Equal(15, x.stackCount, "full-network retries do not consume items");
        f.Core.Network.TryTransferTo(f.Unit.Inventory.Contents[0], 20, new ThingOwner(null));
        f.Tick(60);
        Equal(70L, f.Core.Network.GetTotalCount(f.X), "waiting stack resumes when space opens");
        Equal(0, f.Map.Ground.Count, "waiting stack fully consumed after space opens");

        var competing = new Fixture(1);
        var first = competing.Deliver(competing.X, 10); var second = competing.Deliver(competing.Y, 10);
        competing.Tick();
        Equal(1, competing.Unit.Inventory.UsedSlots, "competing deliveries never overfill unit");
        Equal(true, second.Spawned, "losing delivery remains safe in buffer");
        Equal(10, second.stackCount, "losing delivery quantity intact");
    }

    private static void NetworkChanges()
    {
        var f = new Fixture(); var item = f.Deliver(f.X, 10);
        f.Manager.Unregister(f.Core.node); f.Tick();
        Equal(false, f.Receiver.CanWork, "no core disables receiver");
        Equal(true, item.Spawned, "disconnection retains buffered stack");
        Equal(false, f.Receiver.Accepts(item), "disconnected receiver refuses new deliveries");
        f.Manager.Register(f.Core.node); f.Tick(); f.Tick();
        Equal(10L, f.Core.Network.GetTotalCount(f.X), "reconnection processes waiting stack");
        var secondCore = new Building_StorageCore { Map = f.Map, Spawned = true, Faction = Faction.OfPlayer };
        secondCore.node = new CompStorageNode { parent = secondCore };
        f.Manager.Register(secondCore.node); var waiting = f.Deliver(f.Y, 4); f.Tick();
        Equal(false, f.Receiver.CanWork, "two cores disable receiver");
        Equal(true, waiting.Spawned, "two-core conflict preserves waiting item");
        f.Manager.Unregister(secondCore.node); f.Tick(); f.Tick();
        Equal(4L, f.Core.Network.GetTotalCount(f.Y), "resolves conflict without loss");
    }

    private static void PolicyAndReservations()
    {
        var f = new Fixture(); var item = f.Deliver(f.X, 10);
        f.Core.Settings.allow = false; f.Tick();
        Equal(true, item.Spawned, "filter rejection retains buffer");
        Equal(false, f.Receiver.Accepts(item), "filter controls intake");
        f.Core.Settings.allow = true; item.forbidden = true; f.Tick(60);
        Equal(true, item.Spawned, "forbidden ground item left alone");
        item.forbidden = false; f.Map.reservationManager.Reserved.Add(item); f.Tick(60);
        Equal(true, item.Spawned, "another job's reserved object left alone");
        f.Map.reservationManager.Reserved.Clear(); f.Tick(60);
        Equal(10L, f.Core.Network.GetTotalCount(f.X), "released item enters storage");
    }

    private static bool GoodCell(Fixture f, Thing item, IntVec3? cell = null) =>
        StoreUtility.IsGoodStoreCell(cell ?? f.Receiver.Position, f.Map, item, null, Faction.OfPlayer);

    private static void CapacityPlanning()
    {
        var f = new Fixture(1);
        f.Unit.Inventory.Contents.TryAdd(new Thing { def = f.X, stackCount = 70 });
        Equal(true, GoodCell(f, new Thing { def = f.X, stackCount = 20 }), "partial merge space permits destination");
        Equal(false, GoodCell(f, new Thing { def = f.Y }), "full slots reject incompatible item at planning");
        f.Unit.Inventory.Contents[0].stackCount = 75;
        Equal(false, GoodCell(f, new Thing { def = f.X }), "full stacks reject same item at planning");
        Equal(true, GoodCell(f, new Thing { def = f.X }, new IntVec3(5, 5)), "other storage cells retain native behavior");
        var empty = new Fixture();
        empty.Map.ShelfBlocked = true;
        Equal(false, GoodCell(empty, new Thing { def = empty.X }), "native blockers still checked");
        empty.Map.ShelfBlocked = false; empty.Core.Settings.allow = false;
        Equal(false, GoodCell(empty, new Thing { def = empty.X }), "core filter rejects destination");
        empty.Core.Settings.allow = true; empty.Manager.Unregister(empty.Core.node); empty.Tick();
        Equal(false, GoodCell(empty, new Thing { def = empty.X }), "offline receiver excluded");
    }

    private static void AutomaticIntake()
    {
        var f = new Fixture(); var loose = f.Deliver(f.X, 10);
        f.Tick();
        Equal(false, loose.Spawned, "ordinary drop automatically stored without hauling signal");
        Equal(10L, f.Core.Network.GetTotalCount(f.X), "ordinary drop quantity stored");
        f.Tick(200);
        f.Deliver(f.X, 2); f.Tick();
        Equal(12L, f.Core.Network.GetTotalCount(f.X), "intake still works after more than three seconds");

        var full = new Fixture(1);
        full.Unit.Inventory.Contents.TryAdd(new Thing { def = full.X, stackCount = 75 });
        var waiting = full.Deliver(full.Y, 3); full.Tick(200);
        Equal(true, waiting.Spawned, "full network leaves ordinary drop safe on surface");
        full.Core.Network.TryTransferTo(full.Unit.Inventory.Contents[0], 75, new ThingOwner(null));
        full.Tick(60);
        Equal(3L, full.Core.Network.GetTotalCount(full.Y), "waiting drop enters storage when space opens without a new haul");
    }

    private static Pawn Hauler(Fixture f)
    {
        var pawn = new Pawn { Map = f.Map, Spawned = true, Faction = Faction.OfPlayer };
        pawn.carryTracker = new Pawn_CarryTracker(pawn);
        // Inventory unloading mods can use the native placement toil without setting ToCellStorage.
        pawn.jobs.curJob = new Verse.AI.Job { haulMode = HaulMode.ToCellNonStorage, targetBCell = f.Receiver.Position };
        return pawn;
    }

    private static void DeliveryCallbacks()
    {
        var f = new Fixture(); var oldStack = f.Deliver(f.X, 10); var pawn = Hauler(f);
        pawn.carryTracker.innerContainer.TryAdd(new Thing { def = f.X, stackCount = 5 });
        int placedCount = 0; Thing callbackThing = null;
        Equal(true, pawn.carryTracker.TryDropCarriedThing(f.Receiver.Position, ThingPlaceMode.Direct, out var result,
            (item, count) => { placedCount += count; callbackThing = item; }), "merge delivery succeeds");
        Equal(oldStack, result, "native merge uses existing stack");
        Equal(oldStack, callbackThing, "original placement callback preserved");
        Equal(5, placedCount, "callback preserves actual delivered count");
        Equal(true, oldStack.Spawned, "drop callback defers intake");
        f.Tick(); Equal(15L, f.Core.Network.GetTotalCount(f.X), "merged delivery stored without hauling mode requirement");

        var counted = new Fixture(); pawn = Hauler(counted);
        pawn.carryTracker.innerContainer.TryAdd(new Thing { def = counted.X, stackCount = 10 });
        Equal(true, pawn.carryTracker.TryDropCarriedThing(counted.Receiver.Position, 3, ThingPlaceMode.Direct,
            out result), "counted drop succeeds");
        Equal(7, pawn.carryTracker.CarriedThing.stackCount, "counted drop keeps undelivered cargo");
        counted.Tick(); Equal(3L, counted.Core.Network.GetTotalCount(counted.X), "counted delivery stored without intake signal");

        var partial = new Fixture(); var stack = partial.Deliver(partial.X, 70);
        partial.Deliver(partial.Y, 1); partial.Deliver(new ThingDef(), 1);
        pawn = Hauler(partial); pawn.carryTracker.innerContainer.TryAdd(new Thing { def = partial.X, stackCount = 10 });
        Equal(false, pawn.carryTracker.TryDropCarriedThing(partial.Receiver.Position, ThingPlaceMode.Direct,
            out result), "partly placed delivery reports false at placement boundary");
        Equal(75, stack.stackCount, "part of delivery merged before placement failed");
        Equal(5, pawn.carryTracker.CarriedThing.stackCount, "undelivered partial quantity stays carried");
        partial.Tick(); Equal(75L, partial.Core.Network.GetTotalCount(partial.X), "partly successful drop stored without intake signal");
    }

    private static void Removal()
    {
        var f = new Fixture(); f.Deliver(f.X, 10); f.Deliver(f.Y, 20);
        f.Map.DropBudget = 0;
        f.Receiver.DeSpawn(DestroyMode.KillFinalize);
        Equal(30L, f.Pending(), "blocked destruction drops preserved in recovery");
        Equal(0L, f.Core.Network.GetTotalCount(f.X), "removal does not silently store the buffer");
        f.Map.DropBudget = int.MaxValue;
        Find.TickManager.TicksGame = ((Find.TickManager.TicksGame / 250) + 1) * 250;
        f.Manager.MapComponentTick();
        Equal(0L, f.Pending(), "pending destruction drops released when placement works");
        Equal(2, f.Map.Ground.Count, "both buffered stacks dropped");
        Equal(30, f.Map.Ground[0].stackCount + f.Map.Ground[1].stackCount, "destruction conserves total count");
    }
}
