using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using Verse;

internal static class StorageTradeIntegrationTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception(message + "; expected " + expected + ", got " + actual); }
    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Pawn Negotiator = new Pawn(), Trader = new Pawn();
        internal Fixture()
        {
            Find.WindowStack.Windows.Clear(); TradeDeal.RefuseExecution = TradeDeal.ThrowExecution = false;
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1);
            Negotiator.Spawned = Trader.Spawned = true; Negotiator.Map = Trader.Map = Map;
            Map.GetComponent<MapComponent_StorageNetworks>().MapComponentTick();
        }
        internal void Register(Thing thing, int x)
        {
            thing.Spawned = true; thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageCore core) { core.node = node; Map.listerBuildings.allBuildingsColonist.Add(core); }
            if (thing is Building_StorageUnit unit) unit.node = node;
            Map.GetComponent<MapComponent_StorageNetworks>().Register(node);
        }
        internal Thing Stock(ThingDef def, int count)
        { var item = new Thing { def = def, stackCount = count }; Unit.Inventory.Contents.TryAdd(item, false); return item; }
        internal Dialog_Trade Open()
        { StorageTradeSession.Open(Negotiator, Trader); return Find.WindowStack.WindowOfType<Dialog_Trade>(); }
        internal void Close(Dialog_Trade dialog)
        { dialog.PostClose(); Find.WindowStack.Windows.Remove(dialog); }
    }

    public static int Main()
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(typeof(StorageTradePatches).TypeHandle);
            OrdinaryAndScoped(); ReservedSale(); BuyWithCoreSilver(); RefusalAndCancellation(); ChangedSources(); MultiCoreAndGifts(); Orbital();
            Console.WriteLine("PASS: " + checks + " trade integration assertions (actual production Harmony hooks and sessions; native UI, pricing and delivery boundaries stubbed).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void OrdinaryAndScoped()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 12);
        TradeDeal.OrdinaryGoods.Clear(); var outside = new Thing { def = def, stackCount = 99 }; TradeDeal.OrdinaryGoods.Add(outside);
        var ordinary = new Dialog_Trade(f.Negotiator, f.Trader);
        Equal(outside, TradeSession.deal.AllTradeables[0].thingsColony[0], "ordinary inventory is unchanged");
        Equal(false, StorageTradeSession.TryGet(TradeSession.deal, out _), "ordinary deal has no network context");
        int fills = TradeDeal.NativeFills;
        var dialog = f.Open(); var deal = TradeSession.deal;
        Equal(fills, TradeDeal.NativeFills, "network entry suppresses only native inventory collection");
        Equal(true, StorageTradeSession.TryGet(deal, out _), "initial constructor binds exact deal");
        var line = deal.AllTradeables.Find(t => t.ThingDef == def);
        Equal(12, line.CountHeldBy(Transactor.Colony), "only network inventory displayed");
        Equal(false, line.thingsColony.Contains(outside), "ground storage never appears");
        Equal(true, line is Tradeable_Storage, "network row uses quantity and transfer override");
        deal.Reset();
        Equal(12, deal.AllTradeables.Find(t => t.ThingDef == def).CountHeldBy(Transactor.Colony), "reset preserves network-only source");
        f.Close(dialog);
        Equal(false, StorageTradeSession.TryGet(deal, out _), "close removes context binding");
        ordinary = new Dialog_Trade(f.Negotiator, f.Trader);
        Equal(outside, TradeSession.deal.AllTradeables[0].thingsColony[0], "subsequent ordinary trade is untouched");
        Equal(12, stock.stackCount, "opening/reset/cancel never extract stock");
    }

    private static void ReservedSale()
    {
        var f = new Fixture(); var def = new ThingDef(); var a = f.Stock(def, 75); f.Stock(def, 75);
        var other = new object(); f.Core.Network.ReserveOutgoing(other, a, 70);
        var dialog = f.Open(); var line = TradeSession.deal.AllTradeables.Find(t => t.ThingDef == def);
        Equal(80, line.CountHeldBy(Transactor.Colony), "display subtracts existing claim");
        line.CountToTransfer = -40;
        Equal(true, TradeSession.deal.TryExecute(out bool traded), "scoped transaction succeeds");
        Equal(true, traded, "native execution sees actual trade");
        Equal(110L, f.Core.Network.GetTotalCount(def), "native sale uses indexed network withdrawal");
        Equal(70, a.stackCount, "sale never consumes the first stack's seventy reserved items");
        Equal(40, f.Trader.Goods.Where(t => t.def == def).Sum(t => t.stackCount), "trader receives exact real goods");
        Equal(0, f.Map.Ground.Count, "sale does not spawn goods on ground");
        Equal(true, f.Core.Network.HasOutgoingReservation(other, a, 70), "other reservation survives sale");
        f.Close(dialog);
    }

    private static void BuyWithCoreSilver()
    {
        var f = new Fixture(); var product = new ThingDef(); var silver = f.Stock(ThingDefOf.Silver, 20);
        f.Trader.inventory.innerContainer.TryAdd(new Thing { def = product, stackCount = 6 });
        var dialog = f.Open(); TradeSession.deal.AllTradeables.Find(t => t.ThingDef == product).CountToTransfer = 4;
        Equal(true, TradeSession.deal.TryExecute(out _), "purchase can pay from network silver");
        Equal(16L, f.Core.Network.GetTotalCount(ThingDefOf.Silver), "network payment updates silver index");
        Equal(4, f.Map.Ground.Where(t => t.def == product).Sum(t => t.stackCount), "native purchased goods delivered to ground");
        Equal(0L, f.Core.Network.GetTotalCount(product), "purchases are not silently inserted into network");
        f.Close(dialog);

        f = new Fixture(); f.Stock(new ThingDef(), 1);
        f.Negotiator.inventory.innerContainer.TryAdd(new Thing { def = ThingDefOf.Silver, stackCount = 100 });
        TradeDeal.OrdinaryGoods.Add(new Thing { def = ThingDefOf.Silver, stackCount = 100 });
        f.Trader.inventory.innerContainer.TryAdd(new Thing { def = product, stackCount = 6 });
        dialog = f.Open(); TradeSession.deal.AllTradeables.Find(t => t.ThingDef == product).CountToTransfer = 4;
        int executed = TradeDeal.NativeExecutions;
        Equal(false, TradeSession.deal.TryExecute(out _), "outside and carried silver cannot fund network trade");
        Equal(executed, TradeDeal.NativeExecutions, "failed preflight never starts native transfer");
        Equal(0, f.Map.Ground.Count, "failed purchase never delivers goods"); f.Close(dialog);
    }

    private static void RefusalAndCancellation()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 10); var dialog = f.Open();
        TradeSession.deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -4;
        TradeDeal.RefuseExecution = true;
        Equal(false, TradeSession.deal.TryExecute(out _), "native refusal preserved");
        Equal(10, f.Core.Network.AvailableToWithdraw(stock), "native refusal finalizer releases claims");
        Equal(10, stock.stackCount, "native refusal never extracts goods");
        TradeDeal.RefuseExecution = false; TradeDeal.ThrowExecution = true;
        try { TradeSession.deal.TryExecute(out _); throw new Exception("Expected controlled exception"); }
        catch (InvalidOperationException) { }
        Equal(10, f.Core.Network.AvailableToWithdraw(stock), "native exception finalizer releases claims");
        TradeDeal.ThrowExecution = false;
        Equal(true, StorageTradeSession.TryGet(TradeSession.deal, out var session), "context still attached after refusal");
        Equal(true, session.Prepare(out _), "prepare acquires sale reservation");
        Equal(6, f.Core.Network.AvailableToWithdraw(stock), "prepared quantity held");
        f.Close(dialog);
        Equal(10, f.Core.Network.AvailableToWithdraw(stock), "closing window releases prepared claims");
    }

    private static void ChangedSources()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 10); var dialog = f.Open();
        TradeSession.deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -4;
        f.Unit.Inventory.Contents.Remove(stock);
        int executed = TradeDeal.NativeExecutions;
        Equal(false, TradeSession.deal.TryExecute(out _), "removed inventory rejects stale selection");
        Equal(executed, TradeDeal.NativeExecutions, "stale selection stops before merchant transfer");
        f.Close(dialog);
        f = new Fixture(); stock = f.Stock(def, 10); dialog = f.Open();
        TradeSession.deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -4;
        f.Register(new Building_StorageCore(), 2);
        Equal(false, TradeSession.deal.TryExecute(out _), "multiple cores disabling network rejects sale");
        Equal(10, stock.stackCount, "disabled network never loses goods"); f.Close(dialog);
        f = new Fixture(); stock = f.Stock(def, 10); dialog = f.Open();
        TradeSession.deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -4;
        stock.stale = true;
        Equal(false, TradeSession.deal.TryExecute(out _), "no longer sellable goods rejected on confirmation");
        Equal(10, stock.stackCount, "changed item eligibility preserves goods"); f.Close(dialog);
        f = new Fixture(); stock = f.Stock(def, 10); dialog = f.Open();
        TradeSession.deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -4;
        f.Map.reachability.Unreachable.Add(f.Core);
        Equal(false, TradeSession.deal.TryExecute(out _), "core reachability rechecked on confirmation");
        Equal(10, stock.stackCount, "unreachable core goods preserved"); f.Close(dialog);
    }

    private static void MultiCoreAndGifts()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Stock(def, 10);
        var secondCore = new Building_StorageCore(); var secondUnit = new Building_StorageUnit();
        f.Register(secondCore, 50); f.Register(secondUnit, 51);
        f.Map.GetComponent<MapComponent_StorageNetworks>().MapComponentTick();
        secondUnit.Inventory.Contents.TryAdd(new Thing { def = def, stackCount = 12 });
        var dialog = f.Open(); var deal = TradeSession.deal;
        Equal(22, deal.AllTradeables.Find(t => t.ThingDef == def).CountHeldBy(Transactor.Colony), "separate valid cores aggregate in one row");
        TradeSession.giftMode = true; deal.Reset();
        Equal(1, deal.AllTradeables.Count, "gift reset keeps only network rows");
        deal.AllTradeables[0].CountToTransfer = -15;
        Equal(true, deal.TryExecute(out _), "gift uses same scoped network transfer");
        Equal(7L, f.Core.Network.GetTotalCount(def) + secondCore.Network.GetTotalCount(def), "gift deducts across networks accurately");
        f.Close(dialog);
    }
    private static void Orbital()
    {
        var f = new Fixture(); var product = new ThingDef(); var stock = f.Stock(product, 15); f.Stock(ThingDefOf.Silver, 20);
        var ship = new TradeShip { Map = f.Map }; f.Map.passingShipManager.passingShips.Add(ship);
        var console = new Building_CommsConsole { Spawned = true, Map = f.Map, Position = new IntVec3(5, 0) };
        var beacon = new Building_OrbitalTradeBeacon(); f.Map.Beacons.Add(beacon);
        Equal(false, StorageTradeSession.HasOrbitalCore(f.Map), "powered beacon outside core is insufficient");
        beacon.TradeableCells.Add(f.Core.Position);
        Equal(true, StorageTradeSession.HasOrbitalCore(f.Map), "beacon coverage enables whole core network");
        f.Map.reachability.Unreachable.Add(f.Core);
        StorageTradeSession.Open(f.Negotiator, ship, console); var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
        var deal = TradeSession.deal; var line = deal.AllTradeables.Find(t => t.ThingDef == product);
        Equal(15, line.CountHeldBy(Transactor.Colony), "orbital inventory does not require ground merchant reachability");
        Equal(false, beacon.TradeableCells.Contains(f.Unit.Position), "fixture unit outside beacon");
        line.CountToTransfer = -5;
        Equal(true, deal.TryExecute(out _), "orbital sale succeeds from remotely held items");
        Equal(10L, f.Core.Network.GetTotalCount(product), "orbital sale updates network index");
        Equal(5, ship.Goods.Where(t => t.def == product).Sum(t => t.stackCount), "actual ship receives exact real goods");
        Equal(0, f.Map.Ground.Count, "orbital sale does not spawn stock on ground");
        line = deal.AllTradeables.Find(t => t.ThingDef == product); line.CountToTransfer = 2;
        Equal(true, deal.TryExecute(out _), "orbital purchase succeeds with network silver");
        Equal(18L, f.Core.Network.GetTotalCount(ThingDefOf.Silver), "orbital payment uses core silver");
        Equal(1, ship.DropPods, "native ship purchase delivery invoked");
        Equal(2, f.Map.Ground.Where(t => t.def == product).Sum(t => t.stackCount), "purchased items stay outside storage");
        Equal(10L, f.Core.Network.GetTotalCount(product), "purchases not inserted into network");
        line = deal.AllTradeables.Find(t => t.ThingDef == product); line.CountToTransfer = -3;
        beacon.Powered = false;
        Equal(false, deal.TryExecute(out _), "beacon power loss prevents stale sale");
        Equal(10, f.Core.Network.AvailableToWithdraw(stock), "failed orbital preflight leaves no claims");
        beacon.Powered = true; beacon.TradeableCells.Clear();
        Equal(false, deal.TryExecute(out _), "lost core coverage prevents stale sale");
        beacon.TradeableCells.Add(f.Core.Position); console.CanUseCommsNow = false;
        Equal(false, deal.TryExecute(out _), "console power loss prevents stale sale");
        console.CanUseCommsNow = true; ship.CanTradeNow = false;
        Equal(false, deal.TryExecute(out _), "ship departure prevents stale sale");
        ship.CanTradeNow = true; f.Negotiator.health.capacities.manipulation = false;
        Equal(false, deal.TryExecute(out _), "loss of talking ability prevents trade");
        f.Negotiator.health.capacities.manipulation = true; f.Map.passingShipManager.passingShips.Clear();
        Equal(false, deal.TryExecute(out _), "removed ship cannot trade");
        f.Close(dialog);
        f.Map.passingShipManager.passingShips.Add(ship);
        var secondCore = new Building_StorageCore(); var secondUnit = new Building_StorageUnit();
        f.Register(secondCore, 50); f.Register(secondUnit, 51); f.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
        secondUnit.Inventory.Contents.TryAdd(new Thing { def = product, stackCount = 12 });
        StorageTradeSession.Open(f.Negotiator, ship, console); dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
        Equal(10, TradeSession.deal.AllTradeables.Find(t => t.ThingDef == product).CountHeldBy(Transactor.Colony), "uncovered separate network omitted");
        beacon.TradeableCells.Add(secondCore.Position); TradeSession.deal.Reset();
        Equal(22, TradeSession.deal.AllTradeables.Find(t => t.ThingDef == product).CountHeldBy(Transactor.Colony), "covered separate networks aggregate");
        f.Close(dialog);
        var ordinary = new Dialog_Trade(f.Negotiator, ship);
        Equal(false, StorageTradeSession.TryGet(TradeSession.deal, out _), "ordinary orbital trade retains native scope");
    }
}
