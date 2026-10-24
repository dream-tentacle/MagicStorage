using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using Verse;
using HarmonyLib;
using DynamicTradeInterface.Patches;
using DynamicTradeInterface.UserInterface;

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
            if (thing is Building_StorageSupplyShelf shelf)
            { shelf.node = node; Map.haulDestinationManager.SlotParents[thing.Position] = shelf; }
            Map.GetComponent<MapComponent_StorageNetworks>().Register(node);
        }
        internal Thing Stock(ThingDef def, int count)
        { var item = new Thing { def = def, stackCount = count }; Unit.Inventory.Contents.TryAdd(item, false); return item; }
        internal Thing ShelfStock(ThingDef def, int count, out Building_StorageSupplyShelf shelf)
        {
            shelf = new Building_StorageSupplyShelf();
            Register(shelf, 2);
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            var item = new Thing { def = def, stackCount = count, Spawned = true, Map = Map, Position = shelf.Position };
            Map.Ground.Add(item);
            return item;
        }
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
            ShelfTrading(); ShelfEligibility(); OrbitalShelfTrading();
            PublicApiValidation(); PublicApiScope(); PublicApiTopology(); PublicApiFailures();
            FailureDiagnostics(); ReservationDiagnostics();
            var harmony = new Harmony("magicstorage.tests.dynamictrade");
            harmony.Patch(AccessTools.Method(typeof(Dialog_Trade), nameof(Dialog_Trade.PostOpen)),
                prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Dialog_TradePatches), "Prefix")));
            new DynamicTradeInterfaceCompatibility().Apply(harmony);
            DynamicNativeFallback(); DynamicOrdinary(); DynamicSaleAndPurchase(); DynamicCancelAndFailures(); DynamicOrbital();
            DynamicShelfTrading();
            PublicApiDynamic();
            checks += CeleTechTradeTests.Run();
            checks += StorageLaunchPaymentTests.Run();
            checks += RavenGiftTests.Run();
            // The new scoped getter hook must leave all existing trade/payment paths intact.
            Dialog_TradePatches.Replace = false;
            OrdinaryAndScoped(); ReservedSale(); BuyWithCoreSilver();
            checks += StorageLaunchPaymentTests.Run();
            Console.WriteLine("PASS: " + checks + " trade integration assertions (actual production Harmony hooks and sessions; native UI, pricing and delivery boundaries stubbed).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static StorageTradeOptions Options(Fixture f) => new StorageTradeOptions { Negotiator = f.Negotiator, Trader = f.Trader };

    private static void RejectsWith(TradeDeal deal, string reason)
    {
        int executions = TradeDeal.NativeExecutions;
        Equal(false, deal.TryExecute(out _), "diagnosed refusal rejects trade");
        Equal(reason, Messages.LastText, "diagnosed refusal identifies the exact failed check");
        Equal(executions, TradeDeal.NativeExecutions, "diagnosed refusal never reaches native settlement");
    }

    private static void FailureDiagnostics()
    {
        Translation.Values["MS_Trade_Failed"] = "{0}";
        Translation.Values["MS_Trade_Fail_SellCount"] = "{0}|{1}|{2}|{3}";
        Translation.Values["MS_Trade_Fail_BuyCount"] = "{0}|{1}|{2}";
        Translation.Values["MS_Trade_Fail_CurrencyCount"] = "{0}|{1}";
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 10);
        var dialog = f.Open(); var deal = TradeSession.deal;
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -4;
        TradeSession.trader = new Pawn(); RejectsWith(deal, "MS_Trade_Fail_TraderChanged"); TradeSession.trader = f.Trader;
        TradeSession.playerNegotiator = new Pawn(); RejectsWith(deal, "MS_Trade_Fail_NegotiatorChanged"); TradeSession.playerNegotiator = f.Negotiator;
        f.Negotiator.Spawned = false; RejectsWith(deal, "MS_Trade_Fail_NegotiatorUnavailable"); f.Negotiator.Spawned = true;
        f.Trader.Spawned = false; f.Trader.CanTradeNow = false;
        RejectsWith(deal, "MS_Trade_Fail_TraderLeftMap"); f.Trader.Spawned = f.Trader.CanTradeNow = true;
        f.Trader.Map = new Map(); RejectsWith(deal, "MS_Trade_Fail_TraderLeftMap"); f.Trader.Map = f.Map;
        f.Trader.mindState.traderDismissed = true; RejectsWith(deal, "MS_Trade_Fail_TraderDismissed"); f.Trader.mindState.traderDismissed = false;
        f.Trader.CanTradeNow = false; RejectsWith(deal, "MS_Trade_Fail_TraderUnavailable"); f.Trader.CanTradeNow = true;
        f.Negotiator.AllowedTrade = false; RejectsWith(deal, "MS_Trade_Fail_TradePermission"); f.Negotiator.AllowedTrade = true;
        f.Map.reachability.Unreachable.Add(f.Core); RejectsWith(deal, "MS_Trade_Fail_CoreUnreachable"); f.Map.reachability.Unreachable.Clear();
        f.Map.listerBuildings.allBuildingsColonist.Remove(f.Core); RejectsWith(deal, "MS_Trade_Fail_NoCore"); f.Map.listerBuildings.allBuildingsColonist.Add(f.Core);
        stock.stale = true; RejectsWith(deal, "test item|4|0|MS_Trade_Fail_ItemNotSellable"); stock.stale = false;
        stock.stackCount = 2; RejectsWith(deal, "test item|4|2|"); stock.stackCount = 10;
        var owner = new object(); f.Core.Network.ReserveOutgoing(owner, stock, 8);
        RejectsWith(deal, "test item|4|2|"); f.Core.Network.ReleaseOutgoing(owner);
        Equal(10, stock.stackCount, "all diagnostics preserve stock");
        f.Close(dialog);

        f = new Fixture(); f.Stock(ThingDefOf.Silver, 20);
        var traderGoods = new Thing { def = def, stackCount = 5 }; f.Trader.inventory.innerContainer.TryAdd(traderGoods);
        dialog = f.Open(); deal = TradeSession.deal;
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = 3;
        traderGoods.stackCount = 2; RejectsWith(deal, "test item|3|2"); traderGoods.stackCount = 5;
        var silver = deal.CurrencyTradeable.thingsColony.Single(); silver.stackCount = 2;
        RejectsWith(deal, "3|2"); silver.stackCount = 20;
        deal.AllTradeables.Remove(deal.CurrencyTradeable); RejectsWith(deal, "MS_Trade_Fail_CurrencyMissing");
        f.Close(dialog);

        f = new Fixture(); bool valid = true, callbackThrows = false;
        var options = Options(f); options.IsStillValid = () => { if (callbackThrows) throw new Exception("Diagnostic callback failure"); return valid; };
        Equal(true, StorageTradeApi.Open(options).Opened, "diagnostic callback trade opens");
        dialog = Find.WindowStack.WindowOfType<Dialog_Trade>(); deal = TradeSession.deal;
        valid = false; RejectsWith(deal, "MS_Trade_Fail_CallbackRejected"); valid = true;
        callbackThrows = true; RejectsWith(deal, "MS_Trade_Fail_CallbackError"); callbackThrows = false;
        RejectsWith(deal, "MS_Trade_Fail_CallbackError");
        f.Close(dialog);

        f = new Fixture(); var ship = new TradeShip { Map = f.Map }; f.Map.passingShipManager.passingShips.Add(ship);
        var console = new Building_CommsConsole { Spawned = true, Map = f.Map };
        var beacon = new Building_OrbitalTradeBeacon(); beacon.TradeableCells.Add(f.Core.Position); f.Map.Beacons.Add(beacon);
        StorageTradeSession.Open(f.Negotiator, ship, console); dialog = Find.WindowStack.WindowOfType<Dialog_Trade>(); deal = TradeSession.deal;
        ship.CanTradeNow = false; RejectsWith(deal, "MS_Trade_Fail_ShipUnavailable"); ship.CanTradeNow = true;
        f.Map.passingShipManager.passingShips.Remove(ship); RejectsWith(deal, "MS_Trade_Fail_ShipUnavailable"); f.Map.passingShipManager.passingShips.Add(ship);
        console.Spawned = false; RejectsWith(deal, "MS_Trade_Fail_ConsoleMissing"); console.Spawned = true;
        console.CanUseCommsNow = false; RejectsWith(deal, "MS_Trade_Fail_ConsoleUnavailable"); console.CanUseCommsNow = true;
        f.Negotiator.health.capacities.manipulation = false; RejectsWith(deal, "MS_Trade_Fail_CannotTalk"); f.Negotiator.health.capacities.manipulation = true;
        beacon.Powered = false; RejectsWith(deal, "MS_Trade_Fail_CoreBeacon"); beacon.Powered = true;
        Equal(true, StorageTradeSession.TryGet(deal, out var session), "closed session fixture captures session");
        f.Close(dialog);
        Equal(false, session.Prepare(out _), "closed session remains invalid");
        Equal("MS_Trade_Fail_SessionClosed", Messages.LastText, "closed session explains why reopening is needed");
        foreach (string key in new[] { "MS_Trade_Failed", "MS_Trade_Fail_SellCount", "MS_Trade_Fail_BuyCount", "MS_Trade_Fail_CurrencyCount" })
            Translation.Values.Remove(key);
    }

    private static void ReservationDiagnostics()
    {
        var f = new Fixture(); var item = f.Stock(new ThingDef(), 3);
        var request = new StorageTradeRequest { Network = f.Core.Network, Item = item, Count = 5 };
        using (var tx = new StorageTradeTransaction(new List<StorageTradeRequest> { request }, () => true))
        {
            Equal(false, tx.Reserve(), "partial reservation is rejected");
            Equal("MS_Trade_Fail_ReserveCount", tx.FailureKey, "partial reservation reports its own failure");
            Equal(request, tx.FailedRequest, "failure retains the exact source stack");
            Equal(3, tx.FailureAvailable, "failure retains count before rolling back reservations");
        }
        Equal(3, f.Core.Network.AvailableToWithdraw(item), "diagnostic failure releases partial reservation");
        using (var tx = new StorageTradeTransaction(new List<StorageTradeRequest> { request, request }, () => true))
        {
            request.Count = 1;
            Equal(false, tx.Reserve(), "duplicate request still fails");
            Equal("MS_Trade_Fail_DuplicateItem", tx.FailureKey, "duplicate stack is distinguished from missing stock");
        }
        var shelfItem = f.ShelfStock(new ThingDef(), 2, out var shelf);
        request = new StorageTradeRequest { Network = f.Core.Network, Item = shelfItem, Shelf = shelf, Count = 3 };
        using (var tx = new StorageTradeTransaction(new List<StorageTradeRequest> { request }, () => true))
        {
            Equal(false, tx.Reserve(), "short shelf inventory still fails");
            Equal("MS_Trade_Fail_ShelfCount", tx.FailureKey, "shelf failure is distinguished from network reservation");
            Equal(2, tx.FailureAvailable, "shelf failure retains available count");
        }
    }

    private static void PublicApiValidation()
    {
        var f = new Fixture(); var options = Options(f);
        Equal(StorageTradeOpenStatus.InvalidRequest, StorageTradeApi.Open(null).Status, "null API request refused");
        options.Trader = null;
        Equal(StorageTradeOpenStatus.InvalidRequest, StorageTradeApi.Open(options).Status, "missing trader refused");
        options = Options(f); options.Mode = (StorageTradeMode)99;
        Equal(StorageTradeOpenStatus.InvalidRequest, StorageTradeApi.Open(options).Status, "unknown mode refused");
        options = Options(f); options.NetworkScope = StorageTradeNetworkScope.SpecifiedCores;
        Equal(StorageTradeOpenStatus.InvalidRequest, StorageTradeApi.Open(options).Status, "explicit empty scope never becomes all networks");
        options.Cores = new[] { f.Core, null };
        Equal(StorageTradeOpenStatus.InvalidRequest, StorageTradeApi.Open(options).Status, "null core refused");
        options = Options(f); options.Cores = new[] { f.Core };
        Equal(StorageTradeOpenStatus.InvalidRequest, StorageTradeApi.Open(options).Status, "cores cannot silently be ignored");
        options = Options(f); options.Trader = new TradeShip { Map = f.Map };
        Equal(StorageTradeOpenStatus.InvalidRequest, StorageTradeApi.Open(options).Status, "non-Pawn ground trader requires a physical target");
        options.Mode = StorageTradeMode.Orbital;
        Equal(StorageTradeOpenStatus.InvalidRequest, StorageTradeApi.Open(options).Status, "orbital request requires console");
        options = Options(f); options.IsStillValid = () => false;
        var result = StorageTradeApi.Open(options);
        Equal(StorageTradeOpenStatus.TraderUnavailable, result.Status, "custom validity failure refused");
        Equal(false, result.CanFallbackToVanilla, "unavailable trader does not recommend opening native trade");
        options.IsStillValid = () => { throw new InvalidOperationException("Controlled callback failure"); };
        Equal(StorageTradeOpenStatus.TraderUnavailable, StorageTradeApi.Open(options).Status, "callback exception is contained");
        options = Options(f); f.Trader.CanTradeNow = false;
        Equal(StorageTradeOpenStatus.TraderUnavailable, StorageTradeApi.Open(options).Status, "departed trader refused before constructor");
        f.Trader.CanTradeNow = true;
        f.Map.reachability.Unreachable.Add(f.Core);
        result = StorageTradeApi.Open(Options(f));
        Equal(StorageTradeOpenStatus.NoEligibleNetwork, result.Status, "ineligible cores cannot produce fake success");
        Equal(true, result.CanFallbackToVanilla, "absent network permits normal opener");
        f.Map.reachability.Unreachable.Clear();
        StorageTradeOpenStatus nested = StorageTradeOpenStatus.Opened;
        options = Options(f); options.IsStillValid = () => { nested = StorageTradeApi.Open(Options(f)).Status; return true; };
        result = StorageTradeApi.Open(options);
        Equal(StorageTradeOpenStatus.Opened, result.Status, "empty network is eligible for buying");
        Equal(StorageTradeOpenStatus.OpeningInProgress, nested, "validation callback cannot recursively overwrite trade globals");
        var deal = TradeSession.deal;
        result = StorageTradeApi.Open(Options(f));
        Equal(StorageTradeOpenStatus.TradeWindowAlreadyOpen, result.Status, "second native window refused");
        Equal(false, result.CanFallbackToVanilla, "existing window must not trigger ordinary fallback");
        Equal(deal, TradeSession.deal, "duplicate request leaves original deal intact");
        f.Close(Find.WindowStack.WindowOfType<Dialog_Trade>());
        Find.WindowStack.Add(new Dialog_Trade(f.Negotiator, f.Trader));
        deal = TradeSession.deal;
        Equal(StorageTradeOpenStatus.TradeWindowAlreadyOpen, StorageTradeApi.Open(Options(f)).Status, "ordinary trade window also blocks API");
        Equal(deal, TradeSession.deal, "ordinary active deal preserved");
        f.Close(Find.WindowStack.WindowOfType<Dialog_Trade>());
    }

    private static void PublicApiScope()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Stock(def, 10); f.Stock(ThingDefOf.Silver, 2);
        var selectedShelf = f.ShelfStock(def, 5, out _);
        var coreB = new Building_StorageCore(); var unitB = new Building_StorageUnit();
        f.Register(coreB, 50); f.Register(unitB, 51); f.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
        unitB.Inventory.Contents.TryAdd(new Thing { def = def, stackCount = 40 });
        Equal(true, unitB.Inventory.Contents.TryAdd(new Thing { def = ThingDefOf.Silver, stackCount = 60 }), "fixture stores other network silver within stack limit");
        var cores = new List<Building_StorageCore> { f.Core, f.Core };
        var options = Options(f); options.NetworkScope = StorageTradeNetworkScope.SpecifiedCores; options.Cores = cores;
        Equal(StorageTradeOpenStatus.Opened, StorageTradeApi.Open(options).Status, "explicit core opens with duplicates normalized");
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>(); var deal = TradeSession.deal;
        Equal(15, deal.AllTradeables.Find(t => t.ThingDef == def).CountHeldBy(Transactor.Colony), "scope includes selected shelf but excludes other network");
        Equal(2, deal.CurrencyTradeable.CountHeldBy(Transactor.Colony), "silver list uses identical core scope");
        cores.Clear(); cores.Add(coreB); options.NetworkScope = StorageTradeNetworkScope.AllEligible; options.Trader = new Pawn();
        deal.Reset();
        Equal(15, deal.AllTradeables.Find(t => t.ThingDef == def).CountHeldBy(Transactor.Colony), "mutating caller options and collection never changes live scope");
        f.Trader.inventory.innerContainer.TryAdd(new Thing { def = def, stackCount = 10 }); deal.Reset();
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = 3;
        Equal(false, deal.TryExecute(out _), "unselected silver cannot fund purchase");
        Equal(60L, coreB.Network.GetTotalCount(ThingDefOf.Silver), "failed purchase leaves other network currency intact");
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -12;
        Equal(true, deal.TryExecute(out _), "scoped mixed shelf sale succeeds");
        Equal(0L, f.Core.Network.GetTotalCount(def), "selected network deducted");
        Equal(3, selectedShelf.stackCount, "selected shelf pays shortfall");
        Equal(40L, coreB.Network.GetTotalCount(def), "other network goods untouched");
        f.Core.Spawned = false;
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -1;
        Equal(false, deal.TryExecute(out _), "invalid selected core cannot switch to another network on confirmation");
        deal.Reset();
        Equal(false, deal.AllTradeables.Any(t => t.thingsColony.Count != 0), "refresh after core loss never widens scope");
        f.Close(dialog); f.Core.Spawned = true;
        options = Options(f); options.NetworkScope = StorageTradeNetworkScope.SpecifiedCores; options.Cores = new[] { f.Core, coreB };
        Equal(StorageTradeOpenStatus.Opened, StorageTradeApi.Open(options).Status, "multiple explicit cores supported");
        Equal(43, TradeSession.deal.AllTradeables.Find(t => t.ThingDef == def).CountHeldBy(Transactor.Colony), "multiple selected networks aggregate once");
        f.Close(Find.WindowStack.WindowOfType<Dialog_Trade>());

        bool valid = true; options = Options(f); options.IsStillValid = () => valid;
        Equal(true, StorageTradeApi.Open(options).Opened, "callback-valid request opens");
        deal = TradeSession.deal; deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -1; valid = false;
        Equal(false, deal.TryExecute(out _), "custom validity rechecked at settlement");
        f.Close(Find.WindowStack.WindowOfType<Dialog_Trade>());
        options = Options(f); options.IsStillValid = () => { if (!valid) throw new Exception("Controlled later callback error"); return true; };
        valid = true; Equal(true, StorageTradeApi.Open(options).Opened, "second callback request opens");
        valid = false; deal = TradeSession.deal; deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -1;
        Equal(false, deal.TryExecute(out _), "later callback exception refuses trade without throwing");
        valid = true;
        Equal(false, deal.TryExecute(out _), "faulted callback session stays invalid");
        f.Close(Find.WindowStack.WindowOfType<Dialog_Trade>());
    }

    private static void PublicApiTopology()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 10);
        var options = Options(f); options.NetworkScope = StorageTradeNetworkScope.SpecifiedCores; options.Cores = new[] { f.Core };
        var manager = f.Map.GetComponent<MapComponent_StorageNetworks>(); var oldNetwork = f.Core.Network;
        manager.MarkDirty();
        Equal(true, StorageTradeApi.Open(options).Opened, "open resolves selected core after pending network rebuild");
        Equal(false, ReferenceEquals(oldNetwork, f.Core.Network), "fixture rebuilt network identity");
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>(); var deal = TradeSession.deal;
        Equal(10, deal.AllTradeables.Find(t => t.ThingDef == def).CountHeldBy(Transactor.Colony), "rebuilt selected network inventory visible");
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -3;
        manager.MarkDirty();
        Equal(false, deal.TryExecute(out _), "stale network source cannot settle after topology change");
        Equal(10, stock.stackCount, "topology rejection preserves real goods");
        deal.Reset(); deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -3;
        Equal(true, deal.TryExecute(out _), "refresh binds new network and allows correct sale");
        Equal(7L, f.Core.Network.GetTotalCount(def), "refreshed selected core deducts exact quantity");
        f.Close(dialog);
        var other = new Fixture(); options = Options(other); options.NetworkScope = StorageTradeNetworkScope.SpecifiedCores; options.Cores = new[] { f.Core };
        Equal(StorageTradeOpenStatus.NoEligibleNetwork, StorageTradeApi.Open(options).Status, "core on another map cannot be selected");
        options.Cores = new[] { other.Core, f.Core };
        Equal(StorageTradeOpenStatus.NoEligibleNetwork, StorageTradeApi.Open(options).Status, "partially invalid explicit selection cannot silently shrink");
    }

    private static void PublicApiFailures()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 10);
        var previousDeal = TradeSession.deal; var previousTrader = TradeSession.trader;
        var previousNegotiator = TradeSession.playerNegotiator; TradeSession.giftMode = true;
        Dialog_Trade.ConstructorHook = () => { throw new InvalidOperationException("Controlled constructor failure"); };
        StorageTradeOpenResult result;
        try { result = StorageTradeApi.Open(Options(f)); }
        finally { Dialog_Trade.ConstructorHook = null; }
        Equal(StorageTradeOpenStatus.OpenFailed, result.Status, "constructor failure reported");
        Equal(true, result.CanFallbackToVanilla, "clean constructor failure permits fallback");
        Equal(previousDeal, TradeSession.deal, "constructor failure restores previous deal");
        Equal(previousTrader, TradeSession.trader, "constructor failure restores previous trader");
        Equal(previousNegotiator, TradeSession.playerNegotiator, "constructor failure restores previous negotiator");
        Equal(true, TradeSession.giftMode, "constructor failure restores gift flag");
        TradeDeal failedDeal = null;
        Find.WindowStack.BeforeAdd = window =>
        {
            failedDeal = TradeSession.deal;
            failedDeal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -4;
            Equal(true, StorageTradeSession.TryGet(failedDeal, out var session) && session.Prepare(out _), "failure fixture reserves real network goods");
            throw new InvalidOperationException("Controlled pre-insert failure");
        };
        try { result = StorageTradeApi.Open(Options(f)); }
        finally { Find.WindowStack.BeforeAdd = null; }
        Equal(StorageTradeOpenStatus.OpenFailed, result.Status, "pre-insert failure reported");
        Equal(true, result.CanFallbackToVanilla, "clean pre-insert failure permits fallback");
        Equal(false, StorageTradeSession.TryGet(failedDeal, out _), "failed deal binding removed");
        Equal(10, f.Core.Network.AvailableToWithdraw(stock), "failed open releases prepared reservations");
        Find.WindowStack.AfterAdd = window => { throw new InvalidOperationException("Controlled post-insert failure"); };
        try { result = StorageTradeApi.Open(Options(f)); }
        finally { Find.WindowStack.AfterAdd = null; }
        Equal(StorageTradeOpenStatus.OpenFailed, result.Status, "post-insert failure reported");
        Equal(0, Find.WindowStack.Windows.Count, "partially inserted native window removed");
        Equal(true, result.CanFallbackToVanilla, "partial native window cleaned before fallback");
        Find.WindowStack.AfterAdd = window => Find.WindowStack.TryRemove(window, false);
        try { result = StorageTradeApi.Open(Options(f)); }
        finally { Find.WindowStack.AfterAdd = null; }
        Equal(StorageTradeOpenStatus.OpenFailed, result.Status, "silently removed window is never success");
        var unsupported = new Window();
        Find.WindowStack.AfterAdd = window =>
        {
            Find.WindowStack.Windows.Add(unsupported);
            Find.WindowStack.TryRemove(window, false);
        };
        try { result = StorageTradeApi.Open(Options(f)); }
        finally { Find.WindowStack.AfterAdd = null; }
        Equal(StorageTradeOpenStatus.OpenFailed, result.Status, "unknown replacement is not assumed successful");
        Equal(false, result.CanFallbackToVanilla, "unknown remaining window prevents unsafe fallback");
        Equal(true, Find.WindowStack.Windows.Contains(unsupported), "API never removes unrelated window");
        Find.WindowStack.TryRemove(unsupported, false);
        Equal(true, StorageTradeApi.Open(Options(f)).Opened, "failures do not poison next opening");
        f.Close(Find.WindowStack.WindowOfType<Dialog_Trade>());
    }

    private static void PublicApiDynamic()
    {
        var f = new Fixture(); f.Stock(new ThingDef(), 10);
        Equal(true, StorageTradeApi.Open(Options(f)).Opened, "public API recognizes successful DTI replacement");
        var window = Find.WindowStack.WindowOfType<Window_DynamicTrade>();
        Equal(true, window != null, "dynamic window retained");
        Equal(StorageTradeOpenStatus.TradeWindowAlreadyOpen, StorageTradeApi.Open(Options(f)).Status, "DTI blocks duplicate API opening");
        Find.WindowStack.TryRemove(window, false);
        Window_DynamicTrade.FailPreOpen = true;
        StorageTradeOpenResult result;
        try { result = StorageTradeApi.Open(Options(f)); }
        finally { Window_DynamicTrade.FailPreOpen = false; }
        Equal(StorageTradeOpenStatus.OpenFailed, result.Status, "DTI PreOpen failure reported");
        Equal(true, result.CanFallbackToVanilla, "clean DTI failure permits fallback");
        Find.WindowStack.AfterAdd = added =>
        {
            if (added is Window_DynamicTrade) throw new InvalidOperationException("Controlled partial DTI open");
        };
        try { result = StorageTradeApi.Open(Options(f)); }
        finally { Find.WindowStack.AfterAdd = null; }
        Equal(StorageTradeOpenStatus.OpenFailed, result.Status, "partial DTI failure reported");
        Equal(0, Find.WindowStack.Windows.Count, "partially inserted DTI window removed");
        Equal(true, result.CanFallbackToVanilla, "partial DTI cleanup permits fallback");
        Equal(true, StorageTradeApi.Open(Options(f)).Opened, "DTI can reopen after partial failure");
        Find.WindowStack.TryRemove(Find.WindowStack.WindowOfType<Window_DynamicTrade>(), false);
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

    private static void ShelfTrading()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Stock(def, 4);
        var shelfStock = f.ShelfStock(def, 6, out var shelf);
        var dialog = f.Open(); var deal = TradeSession.deal;
        var line = deal.AllTradeables.Find(t => t.ThingDef == def);
        Equal(10, line.CountHeldBy(Transactor.Colony), "network and connected shelf stock aggregate without duplicates");
        Equal(2, line.thingsColony.Count, "one real stack per inventory source");
        line.CountToTransfer = -7;
        Equal(true, deal.TryExecute(out _), "sale combines network and shelf goods");
        Equal(0L, f.Core.Network.GetTotalCount(def), "sale uses network goods before shelf supplies");
        Equal(3, shelfStock.stackCount, "sale takes only the shortfall from the shelf");
        Equal(7, f.Trader.Goods.Where(item => item.def == def).Sum(item => item.stackCount), "merchant receives exact mixed-source sale");
        Equal(3, deal.AllTradeables.Find(t => t.ThingDef == def).CountHeldBy(Transactor.Colony), "reset shows remaining shelf stock once");
        f.Close(dialog);

        f = new Fixture(); f.Stock(ThingDefOf.Silver, 2);
        shelfStock = f.ShelfStock(ThingDefOf.Silver, 10, out shelf);
        f.Trader.inventory.innerContainer.TryAdd(new Thing { def = def, stackCount = 6 });
        dialog = f.Open(); deal = TradeSession.deal;
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = 5;
        Equal(true, deal.TryExecute(out _), "shelf silver can supplement network currency");
        Equal(0L, f.Core.Network.GetTotalCount(ThingDefOf.Silver), "payment takes network silver first");
        Equal(7, shelfStock.stackCount, "payment consumes exact remaining silver from the shelf");
        Equal(5, f.Map.Ground.Where(item => item.def == def).Sum(item => item.stackCount), "purchases keep native ground delivery");
        f.Close(dialog);

        f = new Fixture(); shelfStock = f.ShelfStock(def, 6, out shelf);
        dialog = f.Open(); deal = TradeSession.deal;
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -4;
        TradeDeal.RefuseExecution = true;
        Equal(false, deal.TryExecute(out _), "native refusal preserves shelf-only goods");
        Equal(6, shelfStock.stackCount, "preflight does not extract shelf stock");
        Equal(true, shelfStock.Spawned, "refusal leaves shelf placement intact");
        TradeDeal.RefuseExecution = false; TradeDeal.ThrowExecution = true;
        try { deal.TryExecute(out _); throw new Exception("Expected controlled exception"); }
        catch (InvalidOperationException) { }
        TradeDeal.ThrowExecution = false;
        Equal(6, shelfStock.stackCount, "native exception before delivery does not extract shelf stock");
        TradeSession.giftMode = true; deal.Reset();
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -3;
        Equal(true, deal.TryExecute(out _), "shelf-only goods can be gifted");
        Equal(3, shelfStock.stackCount, "gift deducts exact shelf quantity");
        f.Close(dialog);

        f = new Fixture(); f.ShelfStock(def, 6, out shelf);
        dialog = f.Open(); deal = TradeSession.deal;
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -3;
        f.Trader.BeforeReceivingTrade = item => { throw new InvalidOperationException("Controlled delivery failure"); };
        try { deal.TryExecute(out _); throw new Exception("Expected delivery exception"); }
        catch (InvalidOperationException) { }
        Equal(6, f.Map.Ground.Where(item => item.def == def && item.Position == shelf.Position).Sum(item => item.stackCount),
            "failed merchant delivery restores all uncommitted goods to the shelf");
        Equal(0L, f.Core.Network.GetTotalCount(def), "shelf restoration does not silently move goods into network inventory");
        Equal(0, f.Trader.Goods.Where(item => item.def == def).Sum(item => item.stackCount), "failed delivery gives merchant no goods");
        f.Close(dialog);

        f = new Fixture(); f.ShelfStock(def, 6, out shelf);
        dialog = f.Open(); deal = TradeSession.deal;
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -3;
        f.Trader.BeforeReceivingTrade = item =>
        {
            f.Trader.inventory.innerContainer.TryAdd(item.SplitOff(item.stackCount), false);
            throw new InvalidOperationException("Controlled exception after acceptance");
        };
        try { deal.TryExecute(out _); throw new Exception("Expected post-acceptance exception"); }
        catch (InvalidOperationException) { }
        Equal(3, f.Map.Ground.Where(item => item.def == def).Sum(item => item.stackCount), "accepted shelf goods are never duplicated by rollback");
        Equal(3, f.Trader.Goods.Where(item => item.def == def).Sum(item => item.stackCount), "merchant retains goods already accepted before exception");
        f.Close(dialog);
    }

    private static void ShelfEligibility()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Stock(def, 2);
        var stock = f.ShelfStock(def, 6, out var shelf);
        var dialog = f.Open(); var deal = TradeSession.deal;
        var line = deal.AllTradeables.Find(t => t.ThingDef == def);
        line.CountToTransfer = -7;
        int executed = TradeDeal.NativeExecutions;
        f.Map.reservationManager.Reserved.Add(stock);
        Equal(2, line.CountHeldBy(Transactor.Colony), "live count excludes newly reserved shelf stock");
        Equal(false, deal.TryExecute(out _), "stale sale cannot take job-reserved shelf goods");
        Equal(executed, TradeDeal.NativeExecutions, "unavailable shelf stock blocks native execution");
        f.Map.reservationManager.Reserved.Clear(); f.Map.physicalInteractionReservationManager.Reserved.Add(stock);
        Equal(false, deal.TryExecute(out _), "physical interactions also block shelf sales");
        f.Map.physicalInteractionReservationManager.Reserved.Clear();
        shelf.forbidden = true;
        Equal(false, deal.TryExecute(out _), "forbidden shelves are not trade sources");
        shelf.forbidden = false; stock.forbidden = true;
        Equal(false, deal.TryExecute(out _), "forbidden shelf items are not trade sources");
        stock.forbidden = false; f.Map.ShelfFire = true;
        Equal(false, deal.TryExecute(out _), "burning shelf cells are not trade sources");
        f.Map.ShelfFire = false; shelf.node.Network = new StorageNetwork();
        Equal(false, deal.TryExecute(out _), "a shelf disconnected from its listed network cannot sell");
        shelf.node.Network = f.Core.Network; stock.stale = true;
        Equal(false, deal.TryExecute(out _), "native sellability is rechecked for shelf goods");
        Equal(6, stock.stackCount, "rejected shelf sales leave real stock intact");
        Equal(2L, f.Core.Network.GetTotalCount(def), "rejected shelf sales leave network stock intact");
        stock.stale = false; f.Close(dialog);
        f.Map.reservationManager.Reserved.Add(stock);
        dialog = f.Open();
        Equal(2, TradeSession.deal.AllTradeables.Find(t => t.ThingDef == def).CountHeldBy(Transactor.Colony), "initial inventory also omits reserved shelf stock");
        f.Close(dialog);
    }

    private static void OrbitalShelfTrading()
    {
        var f = new Fixture(); var def = new ThingDef();
        var stock = f.ShelfStock(def, 8, out var shelf);
        var ship = new TradeShip { Map = f.Map }; f.Map.passingShipManager.passingShips.Add(ship);
        var console = new Building_CommsConsole { Spawned = true, Map = f.Map };
        var beacon = new Building_OrbitalTradeBeacon(); f.Map.Beacons.Add(beacon);
        beacon.TradeableCells.Add(f.Core.Position);
        Equal(false, beacon.TradeableCells.Contains(shelf.Position), "shelf itself is outside beacon coverage");
        StorageTradeSession.Open(f.Negotiator, ship, console);
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>(); var deal = TradeSession.deal;
        var line = deal.AllTradeables.Find(t => t.ThingDef == def);
        Equal(8, line.CountHeldBy(Transactor.Colony), "covered core exposes connected shelf inventory");
        line.CountToTransfer = -3; beacon.Powered = false;
        Equal(false, deal.TryExecute(out _), "lost core coverage blocks shelf sale too");
        Equal(8, stock.stackCount, "lost beacon never deducts shelf stock");
        beacon.Powered = true;
        Equal(true, deal.TryExecute(out _), "orbital shelf-only sale succeeds through covered core");
        Equal(5, stock.stackCount, "orbital shelf sale deducts exact amount");
        Equal(3, ship.Goods.Where(item => item.def == def).Sum(item => item.stackCount), "ship receives actual shelf goods");
        f.Close(dialog);
    }

    private static void DynamicShelfTrading()
    {
        var f = new Fixture(); var def = new ThingDef();
        var stock = f.ShelfStock(def, 8, out _);
        var window = OpenDynamic(f); var deal = TradeSession.deal;
        Equal(8, window.ColonyCounts[def], "dynamic interface displays shelf-only inventory");
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -3;
        Equal(true, window.Accept(), "dynamic interface delivers shelf-only sale");
        Equal(5, stock.stackCount, "dynamic shelf sale consumes exact quantity");
        Equal(3, f.Trader.Goods.Where(item => item.def == def).Sum(item => item.stackCount), "dynamic sale delivers real shelf goods");
    }

    private static Window_DynamicTrade OpenDynamic(Fixture f)
    {
        StorageTradeSession.Open(f.Negotiator, f.Trader);
        var window = Find.WindowStack.WindowOfType<Window_DynamicTrade>();
        Equal(true, window != null, "optional mod still replaces the storage window");
        Equal(true, StorageTradeSession.TryGet(TradeSession.deal, out _), "replacement retains exact deal binding");
        Equal(null, Find.WindowStack.WindowOfType<Dialog_Trade>(), "native window removed during replacement");
        return window;
    }

    private static void DynamicNativeFallback()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Stock(def, 10);
        Dialog_TradePatches.Replace = false;
        var dialog = f.Open(); var deal = TradeSession.deal;
        Equal(true, dialog != null, "DTI native-window preference is preserved");
        Equal(10, deal.AllTradeables.Find(t => t.ThingDef == def).CountHeldBy(Transactor.Colony), "native fallback retains network stock");
        f.Close(dialog);
        Equal(false, StorageTradeSession.TryGet(deal, out _), "native fallback still cleans session on close");
        Dialog_TradePatches.Replace = true;
    }

    private static void DynamicOrdinary()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 10);
        TradeDeal.OrdinaryGoods.Clear(); TradeDeal.OrdinaryGoods.Add(new Thing { def = def, stackCount = 99 });
        Find.WindowStack.Add(new Dialog_Trade(f.Negotiator, f.Trader));
        var window = Find.WindowStack.WindowOfType<Window_DynamicTrade>(); var deal = TradeSession.deal;
        Equal(99, window.ColonyCounts[def], "ordinary dynamic trade still uses ordinary goods");
        Equal(false, StorageTradeSession.TryGet(deal, out _), "ordinary replacement never acquires storage session");
        StorageTradeSession.Open(f.Negotiator, f.Trader);
        Equal(deal, TradeSession.deal, "active ordinary dynamic window blocks a second global trade session");
        Equal(1, Find.WindowStack.Windows.Count, "duplicate open leaves one trade window");
        Find.WindowStack.TryRemove(window);
        Equal(10, stock.stackCount, "ordinary dynamic window never touches network stock");
    }

    private static void DynamicSaleAndPurchase()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 15); f.Stock(ThingDefOf.Silver, 20);
        var other = new object(); f.Core.Network.ReserveOutgoing(other, stock, 5);
        var window = OpenDynamic(f); var deal = TradeSession.deal;
        Equal(10, window.ColonyCounts[def], "colony table is populated before dynamic PreOpen completes");
        StorageTradeSession.Open(f.Negotiator, f.Trader);
        Equal(deal, TradeSession.deal, "active storage dynamic window blocks a second session");
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -4;
        Equal(true, window.Accept(), "dynamic window can execute network sale");
        Equal(11L, f.Core.Network.GetTotalCount(def), "dynamic sale deducts actual network stock");
        Equal(4, f.Trader.Goods.Where(t => t.def == def).Sum(t => t.stackCount), "dynamic sale delivers exact goods");
        Equal(true, f.Core.Network.HasOutgoingReservation(other, stock, 5), "dynamic sale preserves other reservations");
        Equal(false, StorageTradeSession.TryGet(deal, out _), "successful dynamic trade close releases session");
        Equal(false, DynamicTradeInterfaceCompatibility.HasTradeWindow(), "successful trade leaves no dynamic window");
        window = OpenDynamic(f); deal = TradeSession.deal;
        Equal(6, window.ColonyCounts[def], "reopened dynamic table shows remaining unreserved stock");
        Equal(4, window.TraderCounts[def], "reopened dynamic table shows merchant stock");
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = 2;
        Equal(true, window.Accept(), "dynamic purchase pays with network silver");
        Equal(18L, f.Core.Network.GetTotalCount(ThingDefOf.Silver), "dynamic purchase removes exact network silver");
        Equal(2, f.Map.Ground.Where(t => t.def == def).Sum(t => t.stackCount), "dynamic purchase keeps native ground delivery");
        Equal(false, StorageTradeSession.TryGet(deal, out _), "purchase close cleans dynamic session");
    }

    private static void DynamicCancelAndFailures()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 10);
        var window = OpenDynamic(f); var deal = TradeSession.deal;
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -4;
        Equal(true, StorageTradeSession.TryGet(deal, out var session), "dynamic session available for reservation");
        Equal(true, session.Prepare(out _), "dynamic trade can prepare reservations");
        Equal(6, f.Core.Network.AvailableToWithdraw(stock), "dynamic preparation holds exact quantity");
        Find.WindowStack.TryRemove(window);
        Equal(10, f.Core.Network.AvailableToWithdraw(stock), "dynamic cancellation releases all its reservations");
        Equal(false, StorageTradeSession.TryGet(deal, out _), "dynamic cancellation removes context");

        window = OpenDynamic(f); deal = TradeSession.deal;
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -4;
        TradeDeal.RefuseExecution = true;
        Equal(false, window.Accept(), "native refusal keeps the dynamic window open");
        Equal(10, f.Core.Network.AvailableToWithdraw(stock), "dynamic refusal releases prepared reservations");
        TradeDeal.RefuseExecution = false;
        stock.stale = true;
        Equal(false, window.Accept(), "changed stock eligibility rejects dynamic confirmation");
        Equal(1, window.Flashes, "storage preflight rejection flashes dynamic currency display");
        stock.stale = false;
        Equal(true, StorageTradeSession.TryGet(deal, out session), "replacement session remains registered");
        Equal(true, session.Prepare(out _), "prepare before exceptional dynamic close");
        Window_DynamicTrade.FailPostClose = true;
        try { Find.WindowStack.TryRemove(window); throw new Exception("Expected dynamic close exception"); }
        catch (InvalidOperationException) { }
        finally { Window_DynamicTrade.FailPostClose = false; }
        Equal(10, f.Core.Network.AvailableToWithdraw(stock), "exceptional dynamic close still releases reservations");
        Equal(false, StorageTradeSession.TryGet(deal, out _), "exceptional dynamic close still removes context");

        Window_DynamicTrade.FailPreOpen = true;
        try { Equal(StorageTradeOpenStatus.OpenFailed, StorageTradeSession.Open(f.Negotiator, f.Trader).Status, "dynamic open failure is reported explicitly"); }
        finally { Window_DynamicTrade.FailPreOpen = false; }
        Equal(false, StorageTradeSession.TryGet(TradeSession.deal, out _), "failed replacement leaves no session");
        Equal(0, Find.WindowStack.Windows.Count, "failed PreOpen leaves no trade window");
        Equal(10, stock.stackCount, "failed replacement does not extract any stock");
        window = OpenDynamic(f);
        Equal(10, window.ColonyCounts[def], "replacement failure does not poison the next opening");
        Find.WindowStack.TryRemove(window);
    }

    private static void DynamicOrbital()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 12); f.Stock(ThingDefOf.Silver, 20);
        var ship = new TradeShip { Map = f.Map }; f.Map.passingShipManager.passingShips.Add(ship);
        ship.Stock.TryAdd(new Thing { def = def, stackCount = 5 });
        var console = new Building_CommsConsole { Spawned = true, Map = f.Map };
        var beacon = new Building_OrbitalTradeBeacon(); beacon.TradeableCells.Add(f.Core.Position); f.Map.Beacons.Add(beacon);
        StorageTradeSession.Open(f.Negotiator, ship, console);
        var window = Find.WindowStack.WindowOfType<Window_DynamicTrade>(); var deal = TradeSession.deal;
        Equal(12, window.ColonyCounts[def], "orbital dynamic colony table shows covered network inventory");
        Equal(5, window.TraderCounts[def], "orbital dynamic trader table shows ship stock");
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = -3;
        beacon.Powered = false;
        Equal(false, window.Accept(), "dynamic orbital confirmation rechecks beacon power");
        Equal(12, f.Core.Network.AvailableToWithdraw(stock), "failed orbital confirmation leaves no reservations");
        beacon.Powered = true;
        Equal(true, window.Accept(), "dynamic orbital sale succeeds after power restored");
        Equal(9L, f.Core.Network.GetTotalCount(def), "dynamic orbital sale removes exact stock");
        Equal(false, StorageTradeSession.TryGet(deal, out _), "dynamic orbital sale close clears session");
        StorageTradeSession.Open(f.Negotiator, ship, console);
        window = Find.WindowStack.WindowOfType<Window_DynamicTrade>(); deal = TradeSession.deal;
        deal.AllTradeables.Find(t => t.ThingDef == def).CountToTransfer = 2;
        Equal(true, window.Accept(), "dynamic orbital purchase succeeds");
        Equal(18L, f.Core.Network.GetTotalCount(ThingDefOf.Silver), "dynamic orbital purchase pays network silver");
        Equal(1, ship.DropPods, "dynamic orbital purchase invokes native drop pod delivery");
        Equal(9L, f.Core.Network.GetTotalCount(def), "dynamic orbital purchases remain outside network");
        Equal(false, StorageTradeSession.TryGet(deal, out _), "dynamic orbital purchase close clears session");
    }
}
