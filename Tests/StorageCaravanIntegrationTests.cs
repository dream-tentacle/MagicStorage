using System;
using System.Linq;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using RimWorld.Planet;
using Verse;
using MapParent = RimWorld.Planet.MapParent;

internal static class StorageCaravanIntegrationTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Building_CaravanTeleporter Teleporter = new Building_CaravanTeleporter();
        internal readonly Pawn Pawn = new Pawn();
        internal StorageNetwork Network => Core.Network;
        internal Fixture()
        {
            GenWorldClosest.Passable = true; PlanetTile.Layer.canFormCaravans = true;
            CaravanExitMapUtility.Last = null; CaravanExitMapUtility.ReturnNull = CaravanExitMapUtility.ThrowAfterDeparture = false;
            CaravanInventoryUtility.Calls = 0; CaravanInventoryUtility.OnChoose = null;
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Register(Teleporter, 2);
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            Pawn.Map = Map; Pawn.Spawned = true; Pawn.Faction = Faction.OfPlayer;
            Pawn.carryTracker = new Pawn_CarryTracker(Pawn);
            Map.CaravanCandidates.Add(Pawn);
        }
        internal void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageUnit unit) unit.node = node; else ((Building)thing).node = node;
            Map.GetComponent<MapComponent_StorageNetworks>().Register(node);
        }
        internal Thing Stock(ThingDef def, int count)
        { var item = new Thing { def = def, stackCount = count }; Unit.Inventory.Contents.TryAdd(item, false); return item; }
        internal Dialog_StorageCaravan Open()
        {
            var dialog = new Dialog_StorageCaravan(Teleporter); dialog.PostOpen();
            dialog.transferables.Find(row => row.AnyThing == Pawn).AdjustTo(1);
            return dialog;
        }
        internal void Select(Dialog_FormCaravan dialog, ThingDef def, int count) => dialog.transferables.Find(row => row.ThingDef == def).AdjustTo(count);
        internal long CargoCount(ThingDef def) => Pawn.inventory.innerContainer.Where(item => item.def == def).Sum(item => (long)item.stackCount);
    }

    public static int Main()
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(typeof(StorageCaravanPatches).TypeHandle);
            ScopeAndAvailability(); SuccessfulDeparture(); CancellationAndReset(); ChangedSelection();
            OriginalChecks(); ActualStackMass(); FailedDelivery(); FailedWorldCreation(); CarriedThings(); IrreversibleException();
            WorldCommands(); WorldSourceSelection();
            Console.WriteLine("PASS: " + checks + " caravan integration assertions (production Harmony hooks, storage claims, withdrawal and rollback; native UI and world creation boundaries stubbed).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void WorldCommands()
    {
        Translation.Values["MS_Caravan_WorldOpen"] = "魔法存储组建远行队";
        Translation.Values["MS_Caravan_NoTeleporter"] = "该网络未建立远行队传送器";
        var f = new Fixture();
        var secondCore = new Building_StorageCore(); var secondUnit = new Building_StorageUnit();
        f.Register(secondCore, 10); f.Register(secondUnit, 11);
        var mapParent = new Settlement { Map = f.Map, Tile = f.Map.Tile };
        WorldGizmoUtility.Configure(mapParent); Find.WorldSelector.SelectedObjects.Add(mapParent);
        WorldGizmoUtility.WorldUIOnGUI();
        Equal(3, WorldGizmoUtility.Drawn.Count, "one command per working core appended to original world gizmo");
        Equal(WorldGizmoUtility.NativeCommand, WorldGizmoUtility.Drawn[0], "original gizmo retained first and unchanged");
        var commands = WorldGizmoUtility.Drawn.Skip(1).Cast<Command_Action>().ToList();
        Equal("魔法存储组建远行队", commands[0].defaultLabel, "requested world command label");
        Equal(false, commands[0].groupable || commands[1].groupable, "separate networks cannot be grouped into one button");
        Equal(false, commands[0].disabled, "core with connected teleporter enabled");
        Equal(true, commands[1].disabled, "other network does not borrow first network's teleporter");
        Equal("该网络未建立远行队传送器", commands[1].disabledReason, "exact missing teleporter reason");
        Find.WindowStack.Windows.Clear(); commands[0].action();
        var dialog = (Dialog_StorageCaravan)Find.WindowStack.Windows.Last(); dialog.PostOpen();
        Equal(f.Network, dialog.Network, "first core command binds first network");
        var secondTeleporter = new Building_CaravanTeleporter(); f.Register(secondTeleporter, 12);
        commands = StorageCaravanWorldGizmos.CommandsFor(f.Map).ToList();
        Equal(false, commands[1].disabled, "newly constructed second terminal enables second core");
        commands[1].action(); dialog = (Dialog_StorageCaravan)Find.WindowStack.Windows.Last(); dialog.PostOpen();
        Equal(secondCore.Network, dialog.Network, "second core command binds its own network");
        var duplicate = new Building_StorageCore(); f.Register(duplicate, 13);
        Equal(1, StorageCaravanWorldGizmos.CommandsFor(f.Map).Count(), "both cores in invalid multiple-core network hidden");
        int windows = Find.WindowStack.Windows.Count; commands[1].action();
        Equal(windows, Find.WindowStack.Windows.Count, "stale button cannot open an invalid network");
        f.Map.GetComponent<MapComponent_StorageNetworks>().Unregister(f.Teleporter.node); f.Teleporter.Spawned = false;
        commands[0].action();
        Equal(windows, Find.WindowStack.Windows.Count, "removed teleporter rechecked when clicked");
        Equal("该网络未建立远行队传送器", Messages.Last, "removed teleporter reports same exact reason");
        var remaining = StorageCaravanWorldGizmos.CommandsFor(f.Map).Single();
        Equal(true, remaining.disabled, "working core without terminal is shown disabled");
        f.Map.GetComponent<MapComponent_StorageNetworks>().Unregister(f.Core.node); f.Core.Spawned = false;
        Equal(0, StorageCaravanWorldGizmos.CommandsFor(f.Map).Count(), "despawned core removed from registry and world buttons");
    }

    private static void WorldSourceSelection()
    {
        var first = new Fixture(); var second = new Fixture(); second.Map.Tile = new PlanetTile(20);
        var a = new Settlement { Map = first.Map, Tile = first.Map.Tile };
        var b = new Settlement { Map = second.Map, Tile = second.Map.Tile };
        WorldGizmoUtility.Configure(a, b);
        WorldGizmoUtility.WorldUIOnGUI();
        var command = (Command_Action)WorldGizmoUtility.Drawn[1]; command.action();
        Equal(first.Teleporter, ((Dialog_StorageCaravan)Find.WindowStack.Windows.Last()).Teleporter, "no-selection fallback matches native first settlement");
        Find.WorldSelector.SelectedTile = b.Tile;
        WorldGizmoUtility.WorldUIOnGUI(); command = (Command_Action)WorldGizmoUtility.Drawn[1]; command.action();
        Equal(second.Teleporter, ((Dialog_StorageCaravan)Find.WindowStack.Windows.Last()).Teleporter, "selected settlement tile uses that map's cores");
        Find.WorldSelector.SelectedObjects.Add(a);
        WorldGizmoUtility.WorldUIOnGUI(); command = (Command_Action)WorldGizmoUtility.Drawn[1]; command.action();
        Equal(first.Teleporter, ((Dialog_StorageCaravan)Find.WindowStack.Windows.Last()).Teleporter, "selected settlement object takes native precedence over tile");
        Find.WorldSelector.SelectedObjects.Clear();
        var otherParent = new MapParent { Map = second.Map, Tile = b.Tile };
        Find.WorldObjects.Parents[b.Tile.tile] = otherParent;
        WorldGizmoUtility.WorldUIOnGUI(); command = (Command_Action)WorldGizmoUtility.Drawn[1]; command.action();
        Equal(second.Teleporter, ((Dialog_StorageCaravan)Find.WindowStack.Windows.Last()).Teleporter, "non-settlement parent uses native formation map");
        WorldGizmoUtility.NativeCommand.defaultLabel = "CommandSendCaravan";
        WorldGizmoUtility.WorldUIOnGUI(); Equal(1, WorldGizmoUtility.Drawn.Count, "send-to-destination gizmo is not expanded");
        WorldGizmoUtility.Available = false;
        WorldGizmoUtility.WorldUIOnGUI(); Equal(0, WorldGizmoUtility.Drawn.Count, "no magic buttons when native caravan gizmo is absent");
        WorldGizmoUtility.Available = true; WorldGizmoUtility.TilePickerActive = true;
        WorldGizmoUtility.WorldUIOnGUI(); Equal(0, WorldGizmoUtility.Drawn.Count, "native tile picker branch also hides added buttons");
    }

    private static void ScopeAndAvailability()
    {
        var f = new Fixture(); var def = new ThingDef(); var first = f.Stock(def, 75); f.Stock(def, 5);
        var claim = new object(); f.Network.ReserveOutgoing(claim, first, 70);
        var ground = new Thing { def = def, stackCount = 99, Spawned = true, Map = f.Map }; f.Map.Ground.Add(ground);
        var held = new Thing { stackCount = 2 }; f.Pawn.carryTracker.innerContainer.TryAdd(held, false);
        var ordinary = new Dialog_FormCaravan(f.Map); ordinary.PostOpen();
        Equal(true, ordinary.transferables.Any(line => line.things.Contains(ground)), "ordinary window retains ground items");
        Equal(true, ordinary.transferables.Any(line => line.things.Contains(held)), "ordinary window retains carried items");
        Equal(IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, ordinary.InventoryModeForTest(), "ordinary inventory mode untouched");
        int nativeFills = Dialog_FormCaravan.NativeItemFills;
        var dialog = f.Open(); var row = dialog.transferables.Find(line => line.ThingDef == def);
        Equal(nativeFills, Dialog_FormCaravan.NativeItemFills, "network window suppresses native item collection");
        Equal(false, dialog.transferables.Any(line => line.things.Contains(ground) || line.things.Contains(held)), "only network cargo is selectable");
        Equal(true, row.things.Contains(first), "real physical stock is retained for native item details");
        Equal(10, row.MaxCount, "native UI count excludes seventy reserved items");
        Equal(10, row.GetMaximumToTransfer(), "automatic selection upper bound excludes reservations");
        row.AdjustTo(80); Equal(10, row.CountToTransfer, "manual selection cannot exceed available cargo");
        Equal(80L, f.Network.GetTotalCount(def), "opening and selection do not extract stock");
        Equal(true, dialog.forcePause, "inherited native window remains paused");
        Equal(IgnorePawnsInventoryMode.DontIgnore, dialog.InventoryModeForTest(), "retained backpacks are counted");
        Equal(TransferAsOneMode.PodsOrCaravanPacking, TransferableUtility.LastMode, "native caravan grouping mode used");
        Equal(99, ordinary.transferables.Find(line => line.things.Contains(ground)).MaxCount, "normal rows keep native count");
        ordinary.transferables.Find(line => line.AnyThing == f.Pawn).AdjustTo(1);
        Equal(true, ordinary.SendForTest(), "ordinary departure stays native");
        Equal(null, CaravanExitMapUtility.Last, "ordinary window does not invoke teleport");
    }

    private static void SuccessfulDeparture()
    {
        var f = new Fixture(); var def = new ThingDef(); var first = f.Stock(def, 75); f.Stock(def, 5);
        first.forbidden = true;
        var claim = new object(); f.Network.ReserveOutgoing(claim, first, 70);
        f.Pawn.inventory.innerContainer.TryAdd(new Thing { stackCount = 3 }, false);
        var dialog = f.Open(); f.Select(dialog, def, 9);
        int nativeDepartures = Dialog_FormCaravan.NativeDepartures;
        Equal(true, dialog.SendForTest(), "valid caravan departs immediately");
        Equal(nativeDepartures, Dialog_FormCaravan.NativeDepartures, "native packing and walking flow skipped");
        Equal(71L, f.Network.GetTotalCount(def), "exact stock deducted and network index updated");
        Equal(70, first.stackCount, "other job's reservation stays intact");
        Equal(true, f.Network.HasOutgoingReservation(claim, first, 70), "other job's claim survives");
        Equal(9L, f.CargoCount(def), "actual cargo appears in member inventory");
        Equal(false, f.Pawn.inventory.innerContainer.Where(item => item.def == def).Any(item => item.forbidden), "departing cargo is unforbidden");
        Equal(3, f.Pawn.inventory.innerContainer.First(item => item.def != def).stackCount, "existing backpack preserved");
        Equal(f.Pawn, CaravanExitMapUtility.Last.Pawns.Single(), "selected pawn becomes world caravan member");
        Equal(1, CaravanExitMapUtility.Last.Tile.tile, "caravan is created at source world tile");
        Equal(3, CaravanExitMapUtility.Last.Destination.tile, "chosen destination passed to native departure");
        Equal(0, f.Map.Ground.Count, "network cargo is never spawned for hauling");
    }

    private static void CancellationAndReset()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 10);
        var dialog = f.Open(); f.Select(dialog, def, 8); dialog.Close();
        Equal(10, f.Network.AvailableToWithdraw(stock), "cancel leaves stock free of new claims");
        Equal(10L, f.Network.GetTotalCount(def), "cancel leaves stock intact");
        dialog = f.Open(); var more = f.Stock(new ThingDef(), 3); dialog.Reset();
        Equal(true, dialog.transferables.Any(row => row.things.Contains(more)), "reset rebuilds network inventory");
        Equal(false, dialog.transferables.Any(row => row.CountToTransfer > 0), "reset clears selection");
    }

    private static void ChangedSelection()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 10);
        var dialog = f.Open(); f.Select(dialog, def, 8);
        f.Network.ReserveOutgoing(new object(), stock, 5);
        Equal(false, dialog.SendForTest(), "new claim rejects stale quantity");
        Equal(10L, f.Network.GetTotalCount(def), "stale quantity never extracts cargo");
        f = new Fixture(); stock = f.Stock(def, 10); dialog = f.Open(); f.Select(dialog, def, 3);
        f.Map.CaravanCandidates.Remove(f.Pawn);
        Equal(false, dialog.SendForTest(), "pawn eligibility rechecked before departure");
        Equal(10L, f.Network.GetTotalCount(def), "changed pawn preserves cargo");
        f = new Fixture(); stock = f.Stock(def, 10); dialog = f.Open(); f.Select(dialog, def, 3);
        f.Teleporter.SetFaction(new Faction());
        Equal(false, dialog.SendForTest(), "changed endpoint rejects departure");
        Equal(10, stock.stackCount, "changed endpoint leaves cargo intact");
    }

    private static void OriginalChecks()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Stock(def, 10); var dialog = f.Open(); f.Select(dialog, def, 3);
        dialog.ClearRouteForTest(); Equal(false, dialog.SendForTest(), "native route requirement kept");
        f = new Fixture(); f.Stock(def, 10); dialog = f.Open(); f.Select(dialog, def, 3);
        f.Pawn.Downed = true; Equal(false, dialog.SendForTest(), "native capable colonist requirement kept");
        f = new Fixture(); f.Stock(def, 10); dialog = f.Open(); f.Select(dialog, def, 3);
        f.Pawn.inventory.innerContainer.TryAdd(new Thing { stackCount = 20 }, false); dialog.MassCapacity = 22;
        Equal(false, dialog.SendForTest(), "retained inventory participates in native overweight check");
        Equal(10L, f.Network.GetTotalCount(def), "overweight does not consume stock");
        f = new Fixture(); f.Stock(def, 10); dialog = f.Open(); f.Select(dialog, def, 3);
        GenWorldClosest.Passable = false;
        Equal(false, dialog.SendForTest(), "unpassable world location rejected before collection");
        Equal(10L, f.Network.GetTotalCount(def), "unpassable location preserves goods");
    }

    private static void ActualStackMass()
    {
        var f = new Fixture(); var def = new ThingDef(); var first = f.Stock(def, 75); var second = f.Stock(def, 5);
        second.Stuff = new ThingDef { transportMass = 10 };
        f.Network.ReserveOutgoing(new object(), first, 70);
        var dialog = f.Open(); f.Select(dialog, def, 9); dialog.MassCapacity = 20;
        Equal(false, dialog.SendForTest(), "actual withdrawable stack mass rechecked after native estimate");
        Equal(80L, f.Network.GetTotalCount(def), "actual overweight leaves all goods intact");
    }

    private sealed class RejectInventory : ThingOwner
    {
        internal ThingDef RejectDef;
        internal RejectInventory(ThingDef rejectDef) : base(null) { RejectDef = rejectDef; }
        public override bool TryAdd(Thing item, bool merge = true) => item.def != RejectDef && base.TryAdd(item, merge);
    }

    private sealed class LimitedInventory : ThingOwner
    {
        internal LimitedInventory() : base(null) { }
        public override int GetCountCanAccept(Thing item, bool merge = true) => Math.Min(2, item.stackCount);
    }

    private static void FailedDelivery()
    {
        var f = new Fixture(); var a = new ThingDef(); var b = new ThingDef();
        var first = f.Stock(a, 5); var second = f.Stock(b, 7); first.forbidden = second.forbidden = true;
        var dialog = f.Open(); f.Select(dialog, a, 4); f.Select(dialog, b, 5);
        f.Pawn.inventory.innerContainer = new RejectInventory(b);
        Equal(false, dialog.SendForTest(), "second inventory delivery refusal fails entire departure");
        Equal(5L, f.Network.GetTotalCount(a), "previously delivered first cargo restored");
        Equal(7L, f.Network.GetTotalCount(b), "rejected second cargo restored");
        Equal(0, f.Pawn.inventory.innerContainer.Count, "failed departure leaves no collected cargo in backpack");
        Equal(true, f.Unit.Inventory.Contents.All(item => item.forbidden), "rollback retains original forbidden flags");
        Equal(5, f.Network.AvailableToWithdraw(first) + f.Unit.Inventory.Contents.Where(item => item != first && item.def == a).Sum(item => f.Network.AvailableToWithdraw(item)), "rollback releases all claims");
        Equal(null, CaravanExitMapUtility.Last, "failed inventory delivery never creates world caravan");
        f = new Fixture(); f.Stock(a, 5); dialog = f.Open(); f.Select(dialog, a, 4);
        f.Pawn.inventory.innerContainer = new LimitedInventory();
        Equal(false, dialog.SendForTest(), "partial recipient acceptance rejects departure");
        Equal(5L, f.Network.GetTotalCount(a), "both accepted split and unaccepted remainder restored");
        Equal(0, f.Pawn.inventory.innerContainer.Count, "partial recipient retains no rejected cargo");
        f = new Fixture(); f.Unit.SlotCapacity = 1; first = f.Stock(a, 5);
        dialog = f.Open(); f.Select(dialog, a, 4); f.Pawn.inventory.innerContainer = new RejectInventory(a);
        Equal(false, dialog.SendForTest(), "full storage unit still handles a failed withdrawal");
        Equal(5L, f.Network.GetTotalCount(a), "rollback merges back into exact original stack without a free slot");
        Equal(5, first.stackCount, "original physical stack restored");
        Equal(0, f.Map.Ground.Count, "full unit rollback does not spill cargo onto map");
        f = new Fixture(); f.Stock(a, 5); dialog = f.Open(); f.Select(dialog, a, 4);
        CaravanInventoryUtility.OnChoose = () => f.Teleporter.Faction = new Faction();
        Equal(false, dialog.SendForTest(), "endpoint change during collection rolls back");
        Equal(5L, f.Network.GetTotalCount(a), "endpoint change restores real stock");
    }

    private static void FailedWorldCreation()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Stock(def, 5); var dialog = f.Open(); f.Select(dialog, def, 4);
        CaravanExitMapUtility.ReturnNull = true;
        Equal(false, dialog.SendForTest(), "native creation refusal fails departure");
        Equal(5L, f.Network.GetTotalCount(def), "native refusal restores collected stock");
        Equal(0L, f.CargoCount(def), "native refusal empties staged cargo from backpack");
        Equal(true, f.Pawn.Spawned, "native refusal leaves pawn on map");
    }

    private static void CarriedThings()
    {
        var f = new Fixture(); var held = new Thing { stackCount = 4 }; f.Pawn.carryTracker.innerContainer.TryAdd(held, false);
        var dialog = f.Open(); Equal(true, dialog.SendForTest(), "zero cargo teleport allowed");
        Equal(true, f.Map.Ground.Contains(held), "ordinary held cargo left on source map");
        Equal(false, held.Destroyed, "held cargo not destroyed by native exit");
        f = new Fixture(); var child = new Pawn { Map = f.Map, Spawned = false, IsColonist = false };
        f.Pawn.carryTracker.innerContainer.TryAdd(child, false); dialog = f.Open();
        Equal(true, dialog.transferables.Any(row => row.AnyThing == child), "native carried pawn option retained");
        Equal(true, dialog.SendForTest(), "carrier departs with carried pawn unselected");
        Equal(true, f.Map.Ground.Contains(child), "unselected carried pawn stays on map");
        Equal(1, CaravanExitMapUtility.Last.Pawns.Count, "unselected carried pawn not automatically taken");
        f = new Fixture(); child = new Pawn { Map = f.Map, Spawned = false, IsColonist = false };
        f.Pawn.carryTracker.innerContainer.TryAdd(child, false); dialog = f.Open();
        dialog.transferables.Find(row => row.AnyThing == child).AdjustTo(1);
        Equal(true, dialog.SendForTest(), "selected carried pawn may depart");
        Equal(2, CaravanExitMapUtility.Last.Pawns.Count, "selected carried pawn added exactly once");
        f = new Fixture(); var def = new ThingDef(); f.Stock(def, 5);
        f.Pawn.carryTracker.innerContainer.TryAdd(new Thing(), false); f.Pawn.carryTracker.innerContainer.rejectDrop = true;
        dialog = f.Open(); f.Select(dialog, def, 4);
        Equal(false, dialog.SendForTest(), "drop refusal prevents native destruction of carried cargo");
        Equal(5L, f.Network.GetTotalCount(def), "drop refusal restores network cargo");
    }

    private static void IrreversibleException()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Stock(def, 5); var dialog = f.Open(); f.Select(dialog, def, 4);
        CaravanExitMapUtility.ThrowAfterDeparture = true;
        Equal(false, dialog.InstantForTest(), "native callback exception is reported");
        Equal(true, dialog.Closed, "partially departed window cannot resend");
        Equal(4L, f.CargoCount(def), "already departed pawn retains its collected cargo");
        Equal(1L, f.Network.GetTotalCount(def), "departed cargo is not duplicated back into network");
    }
}
