using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace MagicStorage
{
    internal sealed class StorageTradeSession : IDisposable
    {
        private static StorageTradeSession opening;
        private static readonly Dictionary<TradeDeal, StorageTradeSession> sessions = new Dictionary<TradeDeal, StorageTradeSession>();
        private static readonly Action<TradeDeal> limitCurrency = AccessTools.MethodDelegate<Action<TradeDeal>>(
            AccessTools.Method(typeof(TradeDeal), "LimitCurrencyCountToFunds"));
        private readonly StorageTradeInventory inventory;
        private readonly Pawn negotiator;
        private readonly ITrader trader;
        private string unavailableReason;
        internal bool IsOrbital { get; }
        internal bool IsPortable { get; }
        private TradeDeal deal;
        private StorageTradeTransaction transaction;
        private bool closed;

        private StorageTradeSession(StorageTradeOptions options)
        {
            negotiator = options.Negotiator; trader = options.Trader;
            inventory = new StorageTradeInventory(options);
            IsOrbital = options.Mode == StorageTradeMode.Orbital;
            IsPortable = options.Mode == StorageTradeMode.Portable;
        }

        internal static bool TryGet(TradeDeal deal, out StorageTradeSession session)
        {
            if (sessions.TryGetValue(deal, out session)) return true;
            // Dialog_Trade constructs and fills its TradeDeal before assigning TradeSession.deal.
            if (opening != null && opening.deal == null && ReferenceEquals(TradeSession.trader, opening.trader) &&
                TradeSession.playerNegotiator == opening.negotiator)
            {
                session = opening;
                session.deal = deal;
                sessions.Add(deal, session);
                return true;
            }
            return false;
        }

        // Internal convenience adapter. External mods use StorageTradeApi and explicit modes/scopes.
        internal static StorageTradeOpenResult Open(Pawn negotiator, ITrader trader, Building_CommsConsole console = null,
            Thing groundTarget = null, Func<bool> groundTargetUsable = null) => StorageTradeApi.Open(new StorageTradeOptions
        {
            Negotiator = negotiator, Trader = trader, CommsConsole = console,
            Mode = console != null ? StorageTradeMode.Orbital : StorageTradeMode.Ground,
            GroundTarget = groundTarget, IsStillValid = groundTargetUsable
        });

        internal static bool HasTradeWindow() => Find.WindowStack.WindowOfType<Dialog_Trade>() != null ||
            DynamicTradeInterfaceCompatibility.HasTradeWindow();

        internal static StorageTradeOpenResult OpenValidated(StorageTradeOptions options)
        {
            var session = new StorageTradeSession(options);
            string reason = session.inventory.TraderUnavailableReason();
            if (reason != null)
            { session.Dispose(); return new StorageTradeOpenResult(StorageTradeOpenStatus.TraderUnavailable, reason); }
            reason = session.inventory.NetworkUnavailableReason();
            if (reason != null)
            { session.Dispose(); return new StorageTradeOpenResult(StorageTradeOpenStatus.NoEligibleNetwork, reason, !HasTradeWindow()); }
            // A callback must not replace the global trade while we validate the request.
            if (HasTradeWindow())
            { session.Dispose(); return new StorageTradeOpenResult(StorageTradeOpenStatus.TradeWindowAlreadyOpen); }
            var previousTrader = TradeSession.trader;
            var previousNegotiator = TradeSession.playerNegotiator;
            var previousDeal = TradeSession.deal;
            bool previousGiftMode = TradeSession.giftMode;
            var previousWindows = new HashSet<Window>(Find.WindowStack.Windows.OfType<Window>());
            opening = session;
            string failure = "The trade window was not retained or the session became unavailable during opening.";
            try
            {
                Find.WindowStack.Add(new Dialog_StorageTrade(options.Negotiator, options.Trader, session));
                if (!session.closed && session.deal != null && session.Usable() &&
                    (Find.WindowStack.Windows.OfType<Dialog_StorageTrade>().Any(window => window.Session == session) ||
                     DynamicTradeInterfaceCompatibility.HasWindow(session)))
                    return new StorageTradeOpenResult(StorageTradeOpenStatus.Opened);
            }
            catch (Exception error)
            {
                failure = error.Message;
                Log.Error("[MagicStorage] Unable to open storage trade: " + error);
            }
            finally { opening = null; }

            bool clean = true;
            foreach (var window in Find.WindowStack.Windows.OfType<Dialog_StorageTrade>().Where(window => window.Session == session).ToList())
            {
                try { Find.WindowStack.TryRemove(window, false); }
                catch (Exception error) { clean = false; Log.Error("[MagicStorage] Failed trade window cleanup: " + error); }
            }
            clean &= DynamicTradeInterfaceCompatibility.CloseWindows(session);
            session.Dispose();
            if (ReferenceEquals(TradeSession.trader, session.trader) && TradeSession.playerNegotiator == session.negotiator &&
                (ReferenceEquals(TradeSession.deal, session.deal) || ReferenceEquals(TradeSession.deal, previousDeal)))
            {
                TradeSession.trader = previousTrader; TradeSession.playerNegotiator = previousNegotiator;
                TradeSession.deal = previousDeal; TradeSession.giftMode = previousGiftMode;
            }
            bool restored = ReferenceEquals(TradeSession.trader, previousTrader) && TradeSession.playerNegotiator == previousNegotiator &&
                ReferenceEquals(TradeSession.deal, previousDeal) && TradeSession.giftMode == previousGiftMode;
            bool unexpectedWindow = Find.WindowStack.Windows.OfType<Window>().Any(window => !previousWindows.Contains(window));
            return new StorageTradeOpenResult(StorageTradeOpenStatus.OpenFailed, failure, clean && restored && !HasTradeWindow() && !unexpectedWindow);
        }

        internal static bool HasCore(Map map)
        {
            if (map == null) return false;
            map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
                if (building is Building_StorageCore core && core.CanWork) return true;
            return false;
        }

        private bool Usable()
        {
            unavailableReason = UnavailableReason();
            return unavailableReason == null;
        }

        private string UnavailableReason()
        {
            if (closed) return "MS_Trade_Fail_SessionClosed".Translate();
            if (!ReferenceEquals(TradeSession.trader, trader)) return "MS_Trade_Fail_TraderChanged".Translate();
            if (TradeSession.playerNegotiator != negotiator) return "MS_Trade_Fail_NegotiatorChanged".Translate();
            return inventory.UnavailableReason;
        }

        internal static bool HasOrbitalCore(Map map) => StorageTradeInventory.HasOrbitalCore(map);
        internal static bool PaymentCoreEligible(Building_StorageCore core, Map map) =>
            StorageTradeInventory.PaymentCoreEligible(core, map);

        internal int Available(Thing item) => inventory.Available(item, transaction);

        internal void FillInventory()
        {
            transaction?.Dispose(); transaction = null;
            if (Usable() && inventory.Refresh())
                foreach (Thing item in inventory.Items) Add(item, Transactor.Colony);
            foreach (string reason in inventory.CannotSellReasons)
                if (!deal.cannotSellReasons.Contains(reason)) deal.cannotSellReasons.Add(reason);
            if (!TradeSession.giftMode)
            {
                foreach (Thing good in trader.Goods) Add(good, Transactor.Trader);
                if (deal.AllTradeables.Find(t => t.IsCurrency) == null)
                {
                    Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver); silver.stackCount = 0;
                    Add(silver, Transactor.Trader);
                }
            }
            if (TradeSession.TradeCurrency == TradeCurrency.Favor) deal.AllTradeables.Add(new Tradeable_RoyalFavor());
        }

        private void Add(Thing item, Transactor trans)
        {
            var candidates = MagicStorageMod.Settings.TradeItemFilters && item.def != ThingDefOf.Silver
                ? deal.AllTradeables.Where(candidate => StorageInventoryFilters.CanGroup(item, candidate.AnyThing,
                    MagicStorageMod.Settings.InventoryFilters)).ToList() : deal.AllTradeables;
            Tradeable entry = TransferableUtility.TradeableMatching(item, candidates);
            if (entry == null)
            {
                entry = item is Pawn ? (Tradeable)new Tradeable_Pawn() : new Tradeable_Storage(this);
                deal.AllTradeables.Add(entry);
            }
            entry.AddThing(item, trans);
        }

        internal bool Prepare(out StorageTradeTransaction prepared)
        {
            prepared = null;
            transaction?.Dispose(); transaction = null;
            if (!Usable()) return Reject(unavailableReason);
            deal.UpdateCurrencyCount();
            // Match native affordability before limiting the trader's payment after its short-funds confirmation.
            if (!TradeSession.giftMode)
            {
                var currency = deal.CurrencyTradeable;
                if (currency == null) return Reject("MS_Trade_Fail_CurrencyMissing".Translate());
                if (currency.CountPostDealFor(Transactor.Colony) < 0)
                    return Reject("MS_Trade_Fail_CurrencyCount".Translate(-(long)currency.CountToTransfer,
                        currency.CountHeldBy(Transactor.Colony)));
            }
            limitCurrency(deal);
            var requests = new List<StorageTradeRequest>();
            foreach (Tradeable line in deal.AllTradeables)
            {
                if (line.ActionToDo == TradeAction.PlayerBuys && line.CountToTransferToSource > line.CountHeldBy(Transactor.Trader))
                    return Reject("MS_Trade_Fail_BuyCount".Translate(ItemLabel(line.AnyThing),
                        line.CountToTransferToSource, line.CountHeldBy(Transactor.Trader)));
                if (line.ActionToDo != TradeAction.PlayerSells) continue;
                int remaining = line.CountToTransferToDestination;
                string sourceReason = null;
                foreach (Thing item in line.thingsColony.OrderBy(inventory.IsShelfItem))
                {
                    if (remaining == 0) break;
                    string itemReason = inventory.ItemUnavailableReason(item);
                    if (itemReason != null)
                    {
                        if (sourceReason == null) sourceReason = itemReason;
                        continue;
                    }
                    int count = Math.Min(remaining, Available(item));
                    if (count <= 0) continue;
                    requests.Add(inventory.CreateRequest(item, line, count, Usable));
                    remaining -= count;
                }
                if (remaining != 0)
                    return Reject("MS_Trade_Fail_SellCount".Translate(ItemLabel(line.AnyThing),
                        line.CountToTransferToDestination, line.CountToTransferToDestination - remaining, sourceReason ?? ""));
            }
            var candidate = new StorageTradeTransaction(requests, Usable);
            if (!candidate.Reserve())
            {
                string reason = unavailableReason;
                if (reason == null)
                    reason = (candidate.FailureKey ?? "MS_Trade_Fail_ReservationChanged").Translate(
                        ItemLabel(candidate.FailedRequest?.Item), candidate.FailedRequest?.Count ?? 0, candidate.FailureAvailable);
                return Reject(reason);
            }
            inventory.Track(candidate);
            transaction = prepared = candidate;
            return true;
        }

        private static string ItemLabel(Thing item) => item?.LabelCap ?? "MS_Trade_UnknownItem".Translate().ToString();

        private bool Reject(string reason)
        {
            Find.WindowStack.WindowOfType<Dialog_Trade>()?.FlashSilver();
            DynamicTradeInterfaceCompatibility.FlashSilver(this);
            Messages.Message("MS_Trade_Failed".Translate(reason), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        internal void Deliver(Tradeable_Storage line)
        {
            if (transaction == null) throw new InvalidOperationException("Storage trade has no prepared transaction.");
            if (transaction.PendingFor(line) != line.CountToTransferToDestination)
                throw new InvalidOperationException("Storage trade quantity changed after reservation.");
            if (!inventory.Deliver(transaction, line, (item, count) => trader.GiveSoldThingToTrader(item, count, negotiator)))
                throw new InvalidOperationException("Storage trade delivery did not consume the staged goods.");
        }

        internal void Finish(StorageTradeTransaction prepared)
        {
            prepared?.Dispose();
            if (ReferenceEquals(transaction, prepared)) transaction = null;
        }

        public void Dispose()
        {
            if (closed) return;
            closed = true; transaction?.Dispose(); transaction = null;
            if (deal != null) sessions.Remove(deal);
            inventory.Dispose();
        }
    }

    internal sealed class Dialog_StorageTrade : Dialog_Trade
    {
        private readonly StorageTradeSession session;
        private StorageItemFilterPanel itemFilters;
        private bool filtersWereEnabled;
        internal bool UseItemFilters => MagicStorageMod.Settings.TradeItemFilters && session?.IsPortable != true;
        internal StorageItemFilterPanel ItemFilters => itemFilters ?? (itemFilters = new StorageItemFilterPanel(
            TradeSession.playerNegotiator.Map, CommonSearchWidget, () => StorageNativeItemFilters.InvalidateTrade(this),
            () => StorageNativeItemFilters.RebuildTrade(this)));
        public override Vector2 InitialSize => UseItemFilters
            ? new Vector2(Math.Min(1340f, UI.screenWidth), UI.screenHeight) : base.InitialSize;
        internal StorageTradeSession Session => session;
        internal Dialog_StorageTrade(Pawn negotiator, ITrader trader, StorageTradeSession session) : base(negotiator, trader)
        { this.session = session; }

        protected override Rect QuickSearchWidgetRect(Rect winRect, Rect inRect)
        {
            var rect = base.QuickSearchWidgetRect(winRect, inRect);
            if (UseItemFilters) rect.x += StorageItemFilterPanel.Width(inRect.width) + 16f;
            return rect;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 26f),
                (session.IsPortable ? "MS_Trade_PortableInventoryNotice" :
                    session.IsOrbital ? "MS_Trade_OrbitalInventoryNotice" : "MS_Trade_InventoryNotice").Translate());
            inRect.yMin += 28f;
            bool enabled = UseItemFilters;
            if (enabled != filtersWereEnabled)
            { filtersWereEnabled = enabled; StorageNativeItemFilters.RebuildTrade(this); }
            if (!enabled) { base.DoWindowContents(inRect); return; }
            float width = StorageItemFilterPanel.Width(inRect.width);
            ItemFilters.Draw(new Rect(inRect.x, inRect.y, width, inRect.height),
                TradeSession.deal.AllTradeables.SelectMany(row => row.thingsColony.Concat(row.thingsTrader)));
            var body = new Rect(inRect.x + width + 16f, inRect.y, inRect.width - width - 16f, inRect.height);
            Widgets.BeginGroup(body);
            try { base.DoWindowContents(body.AtZero()); }
            finally { Widgets.EndGroup(); }
        }

        public override void PostClose()
        {
            try { base.PostClose(); }
            finally { if (!DynamicTradeInterfaceCompatibility.DeferClose(this)) session.Dispose(); }
        }
    }
}
