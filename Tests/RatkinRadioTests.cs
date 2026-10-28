using System;
using System.Linq;
using HarmonyLib;
using MagicStorage;
using RatkinUnderground;
using RimWorld;
using Verse;
using DynamicTradeInterface.Patches;
using DynamicTradeInterface.UserInterface;

internal static partial class StorageTradeIntegrationTests
{
    private sealed class RadioFixture
    {
        internal readonly Fixture Ground = new Fixture();
        internal readonly Dialog_RKU_Radio Radio;
        internal readonly ThingDef Def = new ThingDef();
        internal readonly Thing Stock;
        internal RadioFixture()
        {
            TradeDeal.OrdinaryGoods.Clear();
            Stock = Ground.Stock(Def, 10); Ground.Stock(ThingDefOf.Silver, 20);
            Radio = new Dialog_RKU_Radio
            {
                radio = new Thing { Spawned = true, Map = Ground.Map, Faction = Faction.OfPlayer },
                Actor = Ground.Negotiator
            };
            Radio.Stock.Add(new Thing { def = Def, stackCount = 8 });
            Radio.Stock.Add(new Thing { def = ThingDefOf.Silver, stackCount = 50 });
            Find.WindowStack.Add(Radio);
        }
        internal void Open(bool storage = true)
        { if (storage) Radio.StartStorage(); else Radio.Start(); Radio.PendingOpen?.Invoke(); }
        internal void Close()
        {
            var native = Find.WindowStack.WindowOfType<Dialog_Trade>();
            if (native != null) Find.WindowStack.TryRemove(native, false);
            var dynamic = Find.WindowStack.WindowOfType<Window_DynamicTrade>();
            if (dynamic != null) Find.WindowStack.TryRemove(dynamic, false);
            Radio.EndRadio();
        }
    }

    private static void RadioCompatibility()
    {
        var inactive = new RadioFixture(); inactive.Radio.Start();
        Equal(false, inactive.Radio.CanTradeNow, "unpatched radio reproduces active-trade self-lock");
        inactive.Close();
        new RatkinRadioCompatibility().Apply(new Harmony("magicstorage.tests.ratkinradio"));
        bool replacement = Dialog_TradePatches.Replace; Dialog_TradePatches.Replace = false;
        try
        {
            RadioButtonVisibility(); RadioSaleAndPurchase(); RadioCancellationAndReentry(); RadioInvalidation();
            RadioFallback(); RadioDelayedOpen(); RadioDynamicWindow();
        }
        finally { Dialog_TradePatches.Replace = replacement; }
    }

    private static void RadioButtonVisibility()
    {
        var f = new RadioFixture();
        var rect = new UnityEngine.Rect(0, 0, 764, 564);
        f.Radio.SetReady(false);
        int draws = Widgets.RadioStorageDraws;
        f.Radio.DoWindowContents(rect);
        Equal(draws, Widgets.RadioStorageDraws, "storage button is hidden before requesting trade");
        f.Radio.Component.isWaitingForTrade = true;
        f.Radio.DoWindowContents(rect);
        Equal(draws, Widgets.RadioStorageDraws, "storage button is hidden during trade countdown");
        f.Radio.Component.isWaitingForTrade = false;
        f.Radio.SetReady(true);
        f.Radio.DoWindowContents(rect);
        Equal(draws + 1, Widgets.RadioStorageDraws, "storage button appears when original trade is ready");
        Equal(true, Widgets.RadioStorageActive, "ready storage button is usable with a core");
        f.Radio.StartStorage();
        draws = Widgets.RadioStorageDraws;
        f.Radio.DoWindowContents(rect);
        Equal(draws, Widgets.RadioStorageDraws, "storage button hides after starting trade");
        f.Close();
    }

    private static void RadioSaleAndPurchase()
    {
        var f = new RadioFixture();
        var claim = new object(); f.Ground.Core.Network.ReserveOutgoing(claim, f.Stock, 3);
        f.Open();
        Equal(true, Find.WindowStack.Windows.Contains(f.Radio), "original radio window stays open");
        Equal(true, Find.WindowStack.WindowOfType<Dialog_StorageTrade>() != null, "radio opens storage trade");
        Equal(true, ReferenceEquals(f.Radio, TradeSession.trader), "original radio merchant is retained");
        Equal(true, f.Radio.CanTradeNow && f.Radio.InProgress, "active trade remains usable with reentry locked");
        Equal(0, f.Ground.Map.Beacons.Count, "radio needs no orbital beacon");
        var line = TradeSession.deal.AllTradeables.Find(item => item.ThingDef == f.Def);
        Equal(7, line.CountHeldBy(Transactor.Colony), "radio excludes already reserved stock");
        line.CountToTransfer = -4;
        Equal(true, TradeSession.deal.TryExecute(out _), "radio sale completes despite in-progress flag");
        Equal(6L, f.Ground.Core.Network.GetTotalCount(f.Def), "radio consumes exact sold quantity");
        Equal(4, f.Radio.pendingCargo.Where(item => item.def == ThingDefOf.Silver).Sum(item => item.stackCount),
            "sale proceeds use original cargo delivery");
        f.Ground.Core.Network.ReleaseOutgoing(claim); f.Close();
        Equal(1, f.Radio.DeliveryLetters, "original radio close issues delivery letter");
        Equal(false, f.Radio.Component.canTrade, "actual sale triggers original cooldown");

        f = new RadioFixture(); f.Open();
        TradeSession.deal.AllTradeables.Find(item => item.ThingDef == f.Def).CountToTransfer = 2;
        Equal(true, TradeSession.deal.TryExecute(out _), "radio purchase spends stored silver");
        Equal(18L, f.Ground.Core.Network.GetTotalCount(ThingDefOf.Silver), "purchase charges exact stored silver");
        Equal(2, f.Radio.pendingCargo.Where(item => item.def == f.Def).Sum(item => item.stackCount),
            "bought goods enter original pending cargo");
        Equal(10L, f.Ground.Core.Network.GetTotalCount(f.Def), "purchase leaves stored goods intact");
        f.Close();
    }

    private static void RadioCancellationAndReentry()
    {
        var f = new RadioFixture(); f.Radio.StartStorage(); f.Radio.StartStorage();
        Equal(1, f.Radio.GeneratedStocks, "new button follows original readiness gate during dialogue");
        f.Radio.PendingOpen(); var deal = TradeSession.deal; f.Radio.StartStorage();
        Equal(1, f.Radio.GeneratedStocks, "active trade disables new storage button");
        Equal(true, ReferenceEquals(deal, TradeSession.deal), "disabled button preserves current deal");
        TradeSession.deal.AllTradeables.Find(item => item.ThingDef == f.Def).CountToTransfer = -5;
        Find.WindowStack.TryRemove(Find.WindowStack.WindowOfType<Dialog_Trade>(), false);
        Equal(true, f.Radio.InProgress, "closing only trade child preserves Ratkin's original flag lifecycle");
        Equal(true, Find.WindowStack.Windows.Contains(f.Radio), "closing child leaves original radio main window open");
        f.Close();
        Equal(false, f.Radio.InProgress, "original radio close clears in-progress state");
        Equal(10L, f.Ground.Core.Network.GetTotalCount(f.Def), "cancellation leaves goods intact");
        Equal(20L, f.Ground.Core.Network.GetTotalCount(ThingDefOf.Silver), "cancellation leaves silver intact");
        Equal(0, f.Radio.DeliveryLetters, "cancellation creates no delivery");
        Equal(true, f.Radio.Component.canTrade, "cancellation does not start cooldown");
        f = new RadioFixture(); f.Radio.Start(); f.Radio.Component.canTrade = false;
        Equal(false, f.Radio.CanTradeNow, "cooldown remains effective while in progress"); f.Close();

        f = new RadioFixture(); f.Open(false);
        Equal(null, Find.WindowStack.WindowOfType<Dialog_StorageTrade>(), "original trade button retains ordinary inventory");
        Equal(true, Find.WindowStack.WindowOfType<Dialog_Trade>() != null, "original trade button still opens native trade");
        f.Close();
        f = new RadioFixture(); f.Open();
        Equal(true, Find.WindowStack.WindowOfType<Dialog_StorageTrade>() != null, "radio can reopen after cancelling and closing");
        f.Close();
    }

    private static void RadioInvalidation()
    {
        for (int change = 0; change < 5; change++)
        {
            var f = new RadioFixture(); f.Open();
            TradeSession.deal.AllTradeables.Find(item => item.ThingDef == f.Def).CountToTransfer = -4;
            if (change == 0) f.Radio.radio.Spawned = false;
            if (change == 1) f.Radio.radio.Map = new Map();
            if (change == 2) Find.WindowStack.TryRemove(f.Radio, false);
            if (change == 3) f.Radio.Component.canTrade = false;
            if (change == 4) f.Radio.radio = new Thing { Spawned = true, Map = f.Ground.Map };
            Equal(false, TradeSession.deal.TryExecute(out _), "invalid radio blocks settlement after change " + change);
            Equal(10L, f.Ground.Core.Network.GetTotalCount(f.Def), "invalid radio never consumes stored goods");
            Equal(false, f.Radio.HasTraded, "invalid radio cannot reach delivery callbacks"); f.Close();
        }
    }

    private static void RadioFallback()
    {
        var f = new RadioFixture();
        f.Ground.Map.GetComponent<MapComponent_StorageNetworks>().Unregister(f.Ground.Core.node);
        f.Ground.Core.Spawned = false; f.Open();
        Equal(null, Find.WindowStack.WindowOfType<Dialog_Trade>(), "storage button is disabled without network");
        Equal(0, f.Radio.GeneratedStocks, "disabled button does not alter Ratkin trading state");
        f.Open(false);
        Equal(true, Find.WindowStack.WindowOfType<Dialog_Trade>() != null, "original trade still works without network");
        f.Close();
        f = new RadioFixture();
        Find.WindowStack.BeforeAdd = window =>
        { if (window is Dialog_StorageTrade) throw new InvalidOperationException("Controlled radio storage window failure"); };
        try
        {
            f.Open();
            Equal(null, Find.WindowStack.WindowOfType<Dialog_Trade>(), "explicit storage request does not switch inventory on failure");
            Equal(1, f.Radio.GeneratedStocks, "failed opening does not regenerate merchant goods");
        }
        finally { Find.WindowStack.BeforeAdd = null; f.Close(); }
    }

    private static void RadioDelayedOpen()
    {
        var f = new RadioFixture(); f.Radio.StartStorage(); f.Radio.EndRadio(); f.Radio.PendingOpen();
        Equal(null, Find.WindowStack.WindowOfType<Dialog_Trade>(), "closed radio cannot run delayed opener");
        f = new RadioFixture(); f.Radio.StartStorage();
        Find.WindowStack.Add(new Dialog_Trade(f.Ground.Negotiator, f.Ground.Trader));
        var deal = TradeSession.deal; f.Radio.PendingOpen();
        Equal(true, ReferenceEquals(deal, TradeSession.deal), "delayed opener cannot overwrite another trade");
        Equal(true, ReferenceEquals(f.Ground.Trader, TradeSession.trader), "busy refusal preserves other merchant"); f.Close();
    }

    private static void RadioDynamicWindow()
    {
        Dialog_TradePatches.Replace = true;
        var f = new RadioFixture(); f.Open();
        var window = Find.WindowStack.WindowOfType<Window_DynamicTrade>();
        Equal(true, window != null, "radio supports existing dynamic trade integration");
        Equal(10, window.ColonyCounts[f.Def], "dynamic radio window sees storage stock");
        TradeSession.deal.AllTradeables.Find(item => item.ThingDef == f.Def).CountToTransfer = -2;
        Equal(true, window.Accept(), "dynamic radio trade uses original merchant");
        Equal(8L, f.Ground.Core.Network.GetTotalCount(f.Def), "dynamic trade consumes exact quantity");
        f.Close(); Dialog_TradePatches.Replace = false;
    }
}
