using System;
using System.Linq;
using System.Runtime.CompilerServices;
using CeleTech.Base;
using DynamicTradeInterface.Patches;
using DynamicTradeInterface.UserInterface;
using HarmonyLib;
using MagicStorage;
using RimWorld;
using Verse;

internal static class CeleTechTradeTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("CeleTech trade: " + message + "; expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Pawn Actor = new Pawn();
        internal readonly ThingWithComps Shuttle = new ThingWithComps();
        internal readonly Comp_TraderShuttle Comp;
        internal readonly Landed_CMCTS Trader;
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture()
        {
            Find.WindowStack.Windows.Clear(); TradeDeal.RefuseExecution = TradeDeal.ThrowExecution = false;
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Manager.EnsureCurrent();
            Actor.Map = Map; Actor.Position = new IntVec3(10, 0); Actor.Spawned = true;
            Shuttle.Map = Map; Shuttle.Position = new IntVec3(11, 0); Shuttle.Spawned = true;
            Shuttle.def.category = ThingCategory.Building;
            Trader = new Landed_CMCTS { Map = Map };
            Comp = new Comp_TraderShuttle { parent = Shuttle, tradeShip = Trader };
            Shuttle.AllComps.Add(Comp);
        }
        internal void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageCore core) { core.node = node; Map.listerBuildings.allBuildingsColonist.Add(core); }
            if (thing is Building_StorageUnit unit) unit.node = node;
            if (thing is Building_StorageSupplyShelf shelf) { shelf.node = node; Map.haulDestinationManager.SlotParents[thing.Position] = shelf; }
            Manager.Register(node);
        }
        internal Thing Stock(ThingDef def, int count)
        { var item = new Thing { def = def, stackCount = count }; Unit.Inventory.Contents.TryAdd(item, false); return item; }
        internal Thing Goods(ThingDef def, int count)
        { var item = new Thing { def = def, stackCount = count }; Trader.Stock.TryAdd(item, false); return item; }
        internal Thing ShelfStock(ThingDef def, int count)
        {
            var shelf = new Building_StorageSupplyShelf(); Register(shelf, 2); Manager.EnsureCurrent();
            var item = new Thing { def = def, stackCount = count, Spawned = true, Map = Map, Position = shelf.Position };
            Map.Ground.Add(item); return item;
        }
        internal FloatMenuOption Option() => Comp.CompFloatMenuOptions(Actor).Last();
        internal void Open()
        {
            Option().action();
            Equal(DefDatabase<Verse.AI.JobDef>.GetNamed("MS_TradeWithStorageCeleTech"), Actor.jobs.curJob.def, "new menu assigns dedicated storage trade job");
            var driver = new JobDriver_StorageCeleTechTrade { pawn = Actor, job = Actor.jobs.curJob };
            Equal(true, driver.TryMakePreToilReservations(false), "job reserves physical shuttle");
            int visits = Verse.AI.Toils_Goto.SourceVisits;
            driver.SetupToils(); driver.RunTestToils();
            Equal(visits + 1, Verse.AI.Toils_Goto.SourceVisits, "job visits physical shuttle before opening trade");
        }
        internal void Close()
        {
            var window = Find.WindowStack.Windows.OfType<Window>().FirstOrDefault();
            if (window != null) Find.WindowStack.TryRemove(window, false);
        }
    }

    internal static int Run()
    {
        RuntimeHelpers.RunClassConstructor(typeof(StorageTradePatches).TypeHandle);
        new CeleTechTradeCompatibility().Apply(new Harmony("magicstorage.tests.celetech"));
        bool replacement = Dialog_TradePatches.Replace;
        Dialog_TradePatches.Replace = false;
        try { MenuAndNativeOption(); SellAndReceiveSilver(); BuyWithStoredSilver(); Invalidation(); ReservationsAndShelves(); DynamicWindow(); }
        finally { Dialog_TradePatches.Replace = replacement; }
        return checks;
    }

    private static void MenuAndNativeOption()
    {
        var f = new Fixture();
        var options = f.Comp.CompFloatMenuOptions(f.Actor).ToList();
        Equal(2, options.Count, "native option retained and storage option appended once");
        options[0].action(); Equal(1, f.Comp.NativeClicks, "original menu action unchanged");
        Equal(true, options[1].action != null, "storage option enabled for usable landed trader");
        Equal(true, CeleTechTradeCompatibility.TryGetTrader(f.Shuttle, out ITrader found) && ReferenceEquals(found, f.Trader), "reflection resolves original trader instance");
        f.Map.reachability.Unreachable.Add(f.Shuttle);
        Equal(null, f.Option().action, "unreachable shuttle has disabled storage option");
        f.Map.reachability.Unreachable.Clear(); f.Actor.skills.record.TotallyDisabled = true;
        Equal(null, f.Option().action, "social incapacity blocks menu");
        f.Actor.skills.record.TotallyDisabled = false; f.Actor.AllowedTrade = false;
        Equal(null, f.Option().action, "trade permission enforced");
        f.Actor.AllowedTrade = true; f.Trader.CanTradeNow = false;
        Equal(null, f.Option().action, "departed trader has disabled option");
        f.Trader.CanTradeNow = true;
        options = f.Comp.CompFloatMenuOptions(f.Actor).ToList();
        f.Comp.tradeShip = new Landed_CMCTS { Map = f.Map };
        options.Last().action();
        Equal(null, f.Actor.jobs.curJob, "stale menu cannot start trade with a replaced trader");
        f.Manager.Unregister(f.Core.node);
        Equal(1, f.Comp.CompFloatMenuOptions(f.Actor).Count(), "without storage core only native menu remains");
    }

    private static void SellAndReceiveSilver()
    {
        var f = new Fixture(); var goods = new ThingDef(); f.Stock(goods, 12); f.Goods(ThingDefOf.Silver, 100);
        Equal(0, f.Map.Beacons.Count, "fixture has no orbital beacons");
        Equal(false, f.Map.passingShipManager.passingShips.Contains(f.Trader), "landed trader is not an orbital passing ship");
        f.Open();
        Equal(true, Find.WindowStack.WindowOfType<Dialog_StorageTrade>() != null, "job opens storage dialog without comms console");
        var deal = TradeSession.deal;
        Equal(true, StorageTradeSession.TryGet(deal, out var session) && !session.IsOrbital, "landed TradeShip uses ground session rules");
        var line = deal.AllTradeables.Find(t => t.ThingDef == goods);
        Equal(12, line.CountHeldBy(Transactor.Colony), "network inventory visible for landed trader");
        line.CountToTransfer = -6;
        Equal(true, deal.TryExecute(out _), "landed trader accepts sale");
        Equal(6L, f.Core.Network.GetTotalCount(goods), "exact sold amount removed from network");
        Equal(6, f.Trader.Goods.Where(t => t.def == goods).Sum(t => t.stackCount), "original trader receives actual network goods");
        Equal(6f, f.Trader.soldSilverTotal, "original incoming-silver credit tracking callback retained");
        Equal(6, f.Map.Ground.Where(t => t.def == ThingDefOf.Silver && t.Position == f.Actor.Position).Sum(t => t.stackCount), "trader puts proceeds beside negotiator");
        Equal(0, f.Trader.DropPods, "landed purchases retain original ground delivery");
        f.Close(); Equal(false, StorageTradeSession.TryGet(deal, out _), "closing menu disposes landed trade session");
    }

    private static void BuyWithStoredSilver()
    {
        var f = new Fixture(); var goods = new ThingDef(); f.Stock(ThingDefOf.Silver, 50); f.Goods(goods, 10);
        f.Open(); var deal = TradeSession.deal;
        deal.AllTradeables.Find(t => t.ThingDef == goods).CountToTransfer = 2;
        Equal(true, deal.TryExecute(out _), "landed purchases spend network silver");
        Equal(48L, f.Core.Network.GetTotalCount(ThingDefOf.Silver), "network pays exact silver amount");
        Equal(2f, f.Trader.boughtSilverTotal, "original outgoing-silver credit tracking callback retained");
        Equal(2, f.Map.Ground.Where(t => t.def == goods && t.Position == f.Actor.Position).Sum(t => t.stackCount), "bought goods delivered through original trader callback");
        Equal(true, f.Trader.TraderDeliveries > 0 && f.Trader.PlayerDeliveries > 0, "both original ITrader callbacks used");
        f.Close();
    }

    private static void Invalidation()
    {
        foreach (int failure in new[] { 0, 1, 2, 3, 4 })
        {
            var f = new Fixture(); var goods = new ThingDef(); f.Stock(goods, 10);
            f.Open(); var deal = TradeSession.deal;
            deal.AllTradeables.Find(t => t.ThingDef == goods).CountToTransfer = -4;
            if (failure == 0) f.Shuttle.Spawned = false;
            if (failure == 1) f.Trader.CanTradeNow = false;
            if (failure == 2) f.Comp.tradeShip = new Landed_CMCTS { Map = f.Map };
            if (failure == 3) f.Shuttle.Map = new Map();
            if (failure == 4) f.Map.reachability.Unreachable.Add(f.Core);
            Equal(false, deal.TryExecute(out _), "unavailable shuttle or core rejects transaction " + failure);
            Equal(10L, f.Core.Network.GetTotalCount(goods), "invalid trade preserves stock " + failure);
            Equal(0, f.Trader.TraderDeliveries, "invalid trade never calls receive callback " + failure);
            f.Close();
        }
    }

    private static void ReservationsAndShelves()
    {
        var f = new Fixture(); var goods = new ThingDef(); Thing stock = f.Stock(goods, 10);
        Thing shelfStock = f.ShelfStock(goods, 6); object claim = new object();
        f.Core.Network.ReserveOutgoing(claim, stock, 7);
        f.Open(); var deal = TradeSession.deal;
        var line = deal.AllTradeables.Find(t => t.ThingDef == goods);
        Equal(9, line.CountHeldBy(Transactor.Colony), "available network portion and connected shelf both exposed");
        line.CountToTransfer = -7;
        Equal(true, deal.TryExecute(out _), "sale combines unreserved network stock with shelf stock");
        Equal(7L, f.Core.Network.GetTotalCount(goods), "other task's network reservation not consumed");
        Equal(2, shelfStock.stackCount, "shelf provides remaining sold quantity");
        Equal(true, f.Core.Network.HasOutgoingReservation(claim, stock, 7), "other task's reservation retained");
        f.Close(); f.Core.Network.ReleaseOutgoing(claim);
    }

    private static void DynamicWindow()
    {
        Dialog_TradePatches.Replace = true;
        try
        {
            var f = new Fixture(); var goods = new ThingDef(); f.Stock(goods, 9);
            f.Open(); var window = Find.WindowStack.WindowOfType<Window_DynamicTrade>();
            Equal(true, window != null, "dynamic trade interface can replace landed storage dialog");
            Equal(9, window.ColonyCounts[goods], "replacement interface sees network inventory");
            TradeSession.deal.AllTradeables.Find(t => t.ThingDef == goods).CountToTransfer = -3;
            Equal(true, window.Accept(), "replacement window accepts landed trade");
            Equal(6L, f.Core.Network.GetTotalCount(goods), "replacement trade deducts exact network quantity");
            Equal(3, f.Trader.Goods.Where(t => t.def == goods).Sum(t => t.stackCount), "replacement retains original trader delivery callback");
            Equal(false, StorageTradeSession.TryGet(TradeSession.deal, out _), "replacement close disposes landed session");
        }
        finally { Dialog_TradePatches.Replace = false; }
    }
}
