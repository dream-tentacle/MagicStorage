using System;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static partial class StorageTradeIntegrationTests
{
    private static void InventoryApiMap()
    {
        Equal(2, StorageTradeApi.ApiVersion, "new inventory capabilities advertise API 2");
        Equal(StorageTradeInventoryStatus.InvalidRequest, StorageTradeApi.CreateInventory(null).Status, "inventory requires options");
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 12); f.Stock(ThingDefOf.Silver, 10);
        var shelfStock = f.ShelfStock(def, 6, out _);
        var previousDeal = TradeSession.deal; var previousTrader = TradeSession.trader; var previousNegotiator = TradeSession.playerNegotiator;
        var result = StorageTradeApi.CreateInventory(Options(f));
        Equal(StorageTradeInventoryStatus.Ready, result.Status, "map inventory opens independently");
        using (var inventory = result.Inventory)
        {
            Equal(previousDeal, TradeSession.deal, "inventory never replaces global deal");
            Equal(previousTrader, TradeSession.trader, "inventory never replaces global trader");
            Equal(previousNegotiator, TradeSession.playerNegotiator, "inventory never replaces negotiator");
            Equal(0, Find.WindowStack.Windows.Count, "inventory opens no UI");
            Equal(3, inventory.Items.Count, "inventory includes unit, shelf and currency stacks");
            Equal(true, inventory.TryReserve(new[] { new StorageTradeItemRequest(stock, 4) }, out var claim), "external caller claims network stock");
            using (claim)
            {
                Equal(8, inventory.Available(stock), "inventory excludes outstanding claims");
                Equal(8, f.Core.Network.AvailableToWithdraw(stock), "claims also exclude ordinary network users");
                Equal(false, inventory.TryReserve(new[] { new StorageTradeItemRequest(stock, 9) }, out _), "overlapping network reservation refused");
                Equal(true, claim.Deliver((item, count) => f.Trader.GiveSoldThingToTrader(item, count, f.Negotiator)), "external receiver gets real network goods");
                Equal(false, claim.Deliver((item, count) => { }), "reservation cannot deliver twice");
            }
            Equal(8, stock.stackCount, "network deduction exact");
            Equal(true, inventory.TryReserve(new[] { new StorageTradeItemRequest(shelfStock, 4) }, out claim), "external shelf claim succeeds");
            using (claim)
            {
                Equal(2, inventory.Available(shelfStock), "shelf availability excludes claims");
                using (var other = StorageTradeApi.CreateInventory(Options(f)).Inventory)
                {
                    Equal(false, other.TryReserve(new[] { new StorageTradeItemRequest(shelfStock, 3) }, out _), "separate inventories share shelf claims");
                }
                Equal(true, claim.Deliver((item, count) => f.Trader.GiveSoldThingToTrader(item, count, f.Negotiator)), "shelf delivery shares common transaction");
            }
            Equal(2, shelfStock.stackCount, "shelf deduction exact");
            Equal(true, inventory.TryReserve(new[] { new StorageTradeItemRequest(stock, 2) }, out claim), "claim before refresh");
            Equal(true, inventory.Refresh(), "refresh succeeds");
            Equal(false, claim.IsValid, "refresh cancels old claims");
            claim.Dispose();
            Equal(8, inventory.Available(stock), "refresh releases old claims");
        }
        Equal(StorageTradeInventoryStatus.NoEligibleInventory,
            StorageTradeApi.CreateInventory(new StorageTradeOptions { Negotiator = f.Negotiator, Trader = f.Trader,
                NetworkScope = StorageTradeNetworkScope.SpecifiedCores, Cores = new[] { new Building_StorageCore() } }).Status,
            "invalid explicit inventory scope refused");
    }

    private static StorageTradeOptions PortableOptions(Fixture f, Func<bool> valid = null)
    {
        f.Map.GetComponent<MapComponent_StorageNetworks>().Unregister(f.Unit.node);
        f.Unit.Spawned = f.Negotiator.Spawned = false; f.Unit.Map = f.Negotiator.Map = null;
        return new StorageTradeOptions { Negotiator = f.Negotiator, Trader = f.Trader, Mode = StorageTradeMode.Portable,
            Units = new[] { f.Unit }, IsStillValid = valid ?? (() => true) };
    }

    private static void InventoryApiPortable()
    {
        var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 10); f.Stock(ThingDefOf.Silver, 8);
        bool valid = true, fail = false;
        var options = PortableOptions(f, () => { if (fail) throw new InvalidOperationException("controlled carrier failure"); return valid; });
        var result = StorageTradeApi.CreateInventory(options);
        Equal(StorageTradeInventoryStatus.Ready, result.Status, "portable inventory needs no map or live network");
        using (var inventory = result.Inventory)
        {
            options.Units = new[] { new Building_StorageUnit() };
            Equal(true, inventory.Refresh(), "caller options cannot redirect portable inventory");
            Equal(2, inventory.Items.Count, "portable goods and silver included");
            Equal(false, inventory.TryReserve(new[] { new StorageTradeItemRequest(stock, 1), new StorageTradeItemRequest(stock, 1) }, out _), "duplicate stack refused");
            Equal(10, inventory.Available(stock), "failed duplicate reservation releases prior claim");
            Equal(true, inventory.TryReserve(new[] { new StorageTradeItemRequest(stock, 4) }, out var claim), "portable reservation succeeds");
            using (claim)
            {
                Equal(6, inventory.Available(stock), "portable outstanding claims excluded");
                var otherOptions = PortableOptions(f);
                using (var other = StorageTradeApi.CreateInventory(otherOptions).Inventory)
                {
                    Equal(6, other.Available(stock), "independent inventories see shared portable claims");
                    Equal(false, other.TryReserve(new[] { new StorageTradeItemRequest(stock, 7) }, out _), "portable oversell refused");
                }
                Equal(true, claim.Deliver((item, count) => f.Trader.GiveSoldThingToTrader(item, count, f.Negotiator)), "portable sale delivers exact real goods");
            }
            Equal(6, stock.stackCount, "portable sale subtracts exact amount");
            Equal(6L, f.Unit.Inventory.GetTotalCount(def), "portable indexes stay synchronized");
            Equal(true, inventory.TryReserve(new[] { new StorageTradeItemRequest(stock, 2) }, out claim), "claim before carrier departure");
            valid = false;
            Equal(false, claim.IsValid, "carrier departure invalidates claim");
            Equal(false, claim.Deliver((item, count) => { throw new Exception("Must not deliver"); }), "invalid carrier cannot deliver");
            claim.Dispose(); valid = true;
            Equal(6, inventory.Available(stock), "cancellation restores availability");
            f.Unit.Spawned = true;
            Equal(0, inventory.Available(stock), "landed unit cannot be used through portable access");
            f.Unit.Spawned = false;
            fail = true; Equal(false, inventory.Refresh(), "callback error contained"); fail = false;
            Equal(false, inventory.Refresh(), "faulted callback stays invalid");
        }
        options = PortableOptions(f); options.IsStillValid = null;
        Equal(StorageTradeInventoryStatus.InvalidRequest, StorageTradeApi.CreateInventory(options).Status, "portable callback required");
        options.IsStillValid = () => true; options.Units = new[] { f.Unit, null };
        Equal(StorageTradeInventoryStatus.InvalidRequest, StorageTradeApi.CreateInventory(options).Status, "null portable unit refused");
        options.Units = new[] { f.Unit }; f.Unit.Faction = new Faction();
        Equal(StorageTradeInventoryStatus.NoEligibleInventory, StorageTradeApi.CreateInventory(options).Status, "nonplayer portable unit refused");
    }

    private static void InventoryApiRecovery()
    {
        foreach (bool portable in new[] { false, true })
        {
            var f = new Fixture(); var def = new ThingDef(); var stock = f.Stock(def, 10);
            using (var inventory = StorageTradeApi.CreateInventory(portable ? PortableOptions(f) : Options(f)).Inventory)
            {
                Equal(true, inventory.TryReserve(new[] { new StorageTradeItemRequest(stock, 4) }, out var claim), "claim before receiver exception");
                try { claim.Deliver((item, count) => { throw new InvalidOperationException("controlled receiver failure"); }); }
                catch (InvalidOperationException) { }
                finally { claim.Dispose(); }
                Equal(10L, f.Unit.Inventory.GetTotalCount(def), "receiver exception restores unconsumed goods");
                Equal(true, inventory.TryReserve(new[] { new StorageTradeItemRequest(stock, 10) }, out claim), "claim full stack before no-op receiver");
                Equal(false, claim.Deliver((item, count) => { }), "no-op receiver is reported as failed delivery");
                Equal(10L, f.Unit.Inventory.GetTotalCount(def), "no-op receiver restores full stack");
                claim.Dispose();
                inventory.Refresh(); stock = inventory.Items.Single();
                Equal(true, inventory.TryReserve(new[] { new StorageTradeItemRequest(stock, 4) }, out claim), "claim before partial receiver failure");
                try
                {
                    claim.Deliver((item, count) =>
                    {
                        f.Trader.GiveSoldThingToTrader(item, 2, f.Negotiator);
                        throw new InvalidOperationException("receiver accepted only part");
                    });
                }
                catch (InvalidOperationException) { }
                finally { claim.Dispose(); }
                Equal(8L, f.Unit.Inventory.GetTotalCount(def), "only unreceived remainder is restored");
                Equal(2, f.Trader.Goods.Where(item => item.def == def).Sum(item => item.stackCount), "accepted goods are never duplicated by rollback");
                inventory.Refresh(); stock = inventory.Items.Single();
                Equal(true, inventory.TryReserve(new[] { new StorageTradeItemRequest(stock, 8) }, out claim), "claim before inventory disposal");
                inventory.Dispose();
                Equal(false, claim.IsValid, "inventory disposal cancels all batches");
                claim.Dispose();
            }
        }
        var recovery = new Fixture(); var recoveryDef = new ThingDef(); var original = recovery.Stock(recoveryDef, 10);
        int slots = MagicStorageMod.Settings.StorageUnitSlots; MagicStorageMod.Settings.StorageUnitSlots = 1;
        try
        {
            using (var inventory = StorageTradeApi.CreateInventory(PortableOptions(recovery)).Inventory)
            {
                inventory.TryReserve(new[] { new StorageTradeItemRequest(original, 10) }, out var claim);
                Equal(false, claim.Deliver((item, count) =>
                {
                    recovery.Stock(new ThingDef(), 1); // Receiver fills the just-freed slot, then fails to consume the goods.
                }), "full recovery container does not turn failure into success");
                Equal(10L, recovery.Unit.Inventory.GetTotalCount(recoveryDef), "full container still preserves original goods");
                Equal(2, recovery.Unit.Inventory.UsedSlots, "rollback may exceed capacity to preserve goods");
                claim.Dispose();
            }
        }
        finally { MagicStorageMod.Settings.StorageUnitSlots = slots; }
    }

    private sealed class PortableTrader : ITrader
    {
        public readonly ThingOwner Stock = new ThingOwner(null), Purchases = new ThingOwner(null);
        public System.Collections.Generic.IEnumerable<Thing> Goods => Stock;
        public bool CanTradeNow => true;
        public Faction Faction => Faction.OfPlayer;
        public TraderKindDef TraderKind { get; } = new TraderKindDef();
        public void GiveSoldThingToTrader(Thing item, int count, Pawn negotiator) => Stock.TryAdd(item.SplitOff(count), false);
        public void GiveSoldThingToPlayer(Thing item, int count, Pawn negotiator) => Purchases.TryAdd(item.SplitOff(count), false);
    }

    private static void InventoryApiPortableWindow()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Stock(def, 10); f.Stock(ThingDefOf.Silver, 12);
        var trader = new PortableTrader(); trader.Stock.TryAdd(new Thing { def = def, stackCount = 5 });
        var options = PortableOptions(f); options.Trader = trader;
        Equal(true, StorageTradeApi.Open(options).Opened, "portable opener supports non-Pawn custom trader");
        var dialog = Find.WindowStack.WindowOfType<Dialog_StorageTrade>();
        Equal(true, dialog != null, "portable opener retains native window even with DTI enabled");
        Equal(false, dialog.UseItemFilters, "portable UI avoids map filter widgets");
        var deal = TradeSession.deal;
        Equal(10, deal.AllTradeables.Find(line => line.ThingDef == def).CountHeldBy(Transactor.Colony), "portable window lists unit stock");
        deal.AllTradeables.Find(line => line.ThingDef == def).CountToTransfer = 3;
        Equal(true, deal.TryExecute(out _), "portable purchase uses shared silver settlement");
        Equal(9L, f.Unit.Inventory.GetTotalCount(ThingDefOf.Silver), "portable purchase deducts storage silver");
        Equal(3, trader.Purchases.Sum(item => item.stackCount), "purchase keeps caller delivery callback");
        deal.AllTradeables.Find(line => line.ThingDef == def).CountToTransfer = -4;
        Equal(true, deal.TryExecute(out _), "portable sale uses same transaction as map sale");
        Equal(6L, f.Unit.Inventory.GetTotalCount(def), "portable window sale deducts exact goods");
        f.Close(dialog);
    }
}
