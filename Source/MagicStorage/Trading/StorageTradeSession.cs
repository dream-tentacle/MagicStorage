using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace MagicStorage
{
    internal sealed class StorageTradeSession : IDisposable
    {
        private static StorageTradeSession opening;
        private static readonly Dictionary<TradeDeal, StorageTradeSession> sessions = new Dictionary<TradeDeal, StorageTradeSession>();
        private static readonly Action<TradeDeal> limitCurrency = AccessTools.MethodDelegate<Action<TradeDeal>>(
            AccessTools.Method(typeof(TradeDeal), "LimitCurrencyCountToFunds"));
        private readonly Dictionary<Thing, StorageNetwork> sources = new Dictionary<Thing, StorageNetwork>();
        private readonly Dictionary<Thing, Building_StorageSupplyShelf> shelfSources = new Dictionary<Thing, Building_StorageSupplyShelf>();
        private readonly Pawn negotiator;
        private readonly ITrader trader;
        private readonly Building_CommsConsole console;
        private readonly Thing groundTarget;
        private readonly Func<bool> additionalUsable;
        private readonly HashSet<Building_StorageCore> selectedCores;
        private bool callbackFailed;
        private string unavailableReason;
        internal bool IsOrbital { get; }
        private TradeDeal deal;
        private StorageTradeTransaction transaction;
        private bool closed;

        private StorageTradeSession(StorageTradeOptions options)
        {
            negotiator = options.Negotiator; trader = options.Trader; console = options.CommsConsole;
            groundTarget = options.GroundTarget; additionalUsable = options.IsStillValid;
            IsOrbital = options.Mode == StorageTradeMode.Orbital;
            selectedCores = options.NetworkScope == StorageTradeNetworkScope.SpecifiedCores
                ? new HashSet<Building_StorageCore>(options.Cores) : null;
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
            string reason = session.TraderUnavailableReason();
            if (reason != null)
                return new StorageTradeOpenResult(StorageTradeOpenStatus.TraderUnavailable, reason);
            reason = session.NetworkUnavailableReason();
            if (reason != null)
                return new StorageTradeOpenResult(StorageTradeOpenStatus.NoEligibleNetwork, reason, !HasTradeWindow());
            // A callback must not replace the global trade while we validate the request.
            if (HasTradeWindow()) return new StorageTradeOpenResult(StorageTradeOpenStatus.TradeWindowAlreadyOpen);
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
            return TraderUnavailableReason() ?? NetworkUnavailableReason();
        }

        private string TraderUnavailableReason()
        {
            if (closed) return "MS_Trade_Fail_SessionClosed".Translate();
            if (!negotiator.Spawned || negotiator.Destroyed) return "MS_Trade_Fail_NegotiatorUnavailable".Translate();
            if (trader is Pawn pawnTrader)
            {
                if (!pawnTrader.Spawned || pawnTrader.Destroyed || pawnTrader.Map != negotiator.Map)
                    return "MS_Trade_Fail_TraderLeftMap".Translate();
                if (pawnTrader.mindState.traderDismissed) return "MS_Trade_Fail_TraderDismissed".Translate();
            }
            if (!trader.CanTradeNow) return (IsOrbital ? "MS_Trade_Fail_ShipUnavailable" : "MS_Trade_Fail_TraderUnavailable").Translate();
            if (!negotiator.CanTradeWith(trader.Faction, trader.TraderKind).Accepted)
                return "MS_Trade_Fail_TradePermission".Translate();
            if (IsOrbital)
            {
                var ship = (TradeShip)trader;
                if (ship.Map != negotiator.Map || !negotiator.Map.passingShipManager.passingShips.Contains(ship))
                    return "MS_Trade_Fail_ShipUnavailable".Translate();
                if (console == null || console.Destroyed || !console.Spawned || console.Map != negotiator.Map)
                    return "MS_Trade_Fail_ConsoleMissing".Translate();
                if (!console.CanUseCommsNow) return "MS_Trade_Fail_ConsoleUnavailable".Translate();
                if (!negotiator.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
                    return "MS_Trade_Fail_CannotTalk".Translate();
            }
            else if (groundTarget == null || groundTarget.Destroyed || !groundTarget.Spawned || groundTarget.Map != negotiator.Map)
                return "MS_Trade_Fail_GroundTarget".Translate();
            if (callbackFailed) return "MS_Trade_Fail_CallbackError".Translate();
            try { return additionalUsable?.Invoke() == false ? "MS_Trade_Fail_CallbackRejected".Translate().ToString() : null; }
            catch (Exception error)
            {
                callbackFailed = true;
                Log.Error("[MagicStorage] Trade validity callback failed; this session is unavailable: " + error);
                return "MS_Trade_Fail_CallbackError".Translate();
            }
        }

        private string NetworkUnavailableReason()
        {
            negotiator.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (selectedCores != null)
            {
                if (selectedCores.Count == 0) return "MS_Trade_Fail_NoCore".Translate();
                foreach (var core in selectedCores)
                    if (!CoreEligible(core, out var reason)) return CoreUnavailableReason(core, reason);
                return null;
            }
            string firstReason = null;
            foreach (var core in negotiator.Map.listerBuildings.allBuildingsColonist.OfType<Building_StorageCore>())
            {
                if (CoreEligible(core, out var reason)) return null;
                if (firstReason == null) firstReason = CoreUnavailableReason(core, reason);
            }
            return firstReason ?? "MS_Trade_Fail_NoCore".Translate().ToString();
        }

        // Only called after CoreEligible fails; keep its existing inventory-filtering semantics.
        private string CoreUnavailableReason(Building_StorageCore core, string reason)
        {
            if (core == null || core.Destroyed) return "MS_Trade_Fail_CoreMissing".Translate();
            if (!core.CanWork) return "MS_Trade_Fail_CoreNotWorking".Translate(core.LabelCap);
            if (core.Faction != Faction.OfPlayer || core.Map != negotiator.Map || core.Position.Fogged(negotiator.Map))
                return "MS_Trade_Fail_CoreUnavailable".Translate(core.LabelCap);
            if (IsOrbital && !BeaconCovers(core)) return "MS_Trade_Fail_CoreBeacon".Translate(core.LabelCap);
            if (reason != null) return "MS_Trade_Fail_CoreSellingBlocked".Translate(core.LabelCap, reason);
            return "MS_Trade_Fail_CoreUnreachable".Translate(core.LabelCap);
        }

        internal static bool HasOrbitalCore(Map map)
        {
            if (!HasCore(map)) return false;
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
                if (building is Building_StorageCore core && core.CanWork && !core.Position.Fogged(map) &&
                    BeaconCovers(core)) return true;
            return false;
        }

        private static bool BeaconCovers(Building_StorageCore core) =>
            Building_OrbitalTradeBeacon.AllPowered(core.Map).Any(beacon => beacon.TradeableCells.Contains(core.Position));

        private bool CoreEligible(Building_StorageCore core, out string reason)
        {
            reason = null;
            if (selectedCores != null && !selectedCores.Contains(core)) return false;
            Thing groundActor = trader is Pawn ? trader as Pawn : groundTarget != null ? negotiator : null;
            return CoreEligible(core, negotiator.Map, IsOrbital, groundActor, out reason);
        }

        internal static bool PaymentCoreEligible(Building_StorageCore core, Map map) =>
            CoreEligible(core, map, false, null, out _);

        private static bool CoreEligible(Building_StorageCore core, Map map, bool orbital, Thing groundTrader, out string reason)
        {
            reason = null;
            if (core == null || core.Destroyed || !core.CanWork || core.Faction != Faction.OfPlayer || core.Map != map || core.Position.Fogged(map)) return false;
            if (orbital && !BeaconCovers(core)) return false;
            if (groundTrader != null &&
                !map.reachability.CanReach(groundTrader.Position, core, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Some)) return false;
            Room room = core.GetRoom();
            if (room == null) return true;
            int cells = GenRadial.NumCellsInRadius(6.9f);
            for (int i = 0; i < cells; i++)
            {
                IntVec3 cell = core.Position + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map) || cell.GetRoom(map) != room) continue;
                foreach (Thing thing in cell.GetThingList(map))
                    if (thing.PreventPlayerSellingThingsNearby(out reason)) return false;
            }
            return true;
        }

        internal int Available(Thing item)
        {
            if (!sources.TryGetValue(item, out StorageNetwork network)) return 0;
            shelfSources.TryGetValue(item, out var shelf);
            return transaction?.Available(network, item, shelf) ?? (shelf == null
                ? network.AvailableToWithdraw(item) : StorageTradeTransaction.ShelfAvailable(network, shelf, item));
        }

        internal void FillInventory()
        {
            transaction?.Dispose(); transaction = null;
            sources.Clear();
            shelfSources.Clear();
            if (Usable())
            {
                negotiator.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
                var seen = new HashSet<StorageNetwork>();
                foreach (Building building in negotiator.Map.listerBuildings.allBuildingsColonist)
                {
                    if (!(building is Building_StorageCore core)) continue;
                    if (!CoreEligible(core, out string reason))
                    {
                        if (reason != null && !deal.cannotSellReasons.Contains(reason)) deal.cannotSellReasons.Add(reason);
                        continue;
                    }
                    if (!seen.Add(core.Network)) continue;
                    var stacks = new List<Thing>(); core.Network.GetAllStacks(stacks);
                    stacks.Sort((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
                    foreach (Thing item in stacks) AddColony(item, core.Network);
                    foreach (var shelf in negotiator.Map.GetComponent<MapComponent_StorageNetworks>().SupplyShelves)
                        if (shelf.Spawned && shelf.Network == core.Network)
                            foreach (Thing item in shelf.GetSlotGroup().HeldThings.OrderBy(item => item.thingIDNumber))
                                AddColony(item, core.Network, shelf);
                }
            }
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

        private void AddColony(Thing item, StorageNetwork network, Building_StorageSupplyShelf shelf = null)
        {
            if (item == null || item.Destroyed || item is Pawn || sources.ContainsKey(item) ||
                (shelf == null ? network.AvailableToWithdraw(item) :
                    StorageTradeTransaction.ShelfAvailable(network, shelf, item)) <= 0 ||
                !TradeUtility.PlayerSellableNow(item, trader)) return;
            sources.Add(item, network);
            if (shelf != null) shelfSources.Add(item, shelf);
            Add(item, Transactor.Colony);
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
            negotiator.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
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
                foreach (Thing item in line.thingsColony.OrderBy(item => shelfSources.ContainsKey(item)))
                {
                    if (remaining == 0) break;
                    if (!sources.TryGetValue(item, out StorageNetwork network))
                    {
                        if (sourceReason == null) sourceReason = "MS_Trade_Fail_SourceChanged".Translate();
                        continue;
                    }
                    if (!CoreEligible(network.Core, out var coreReason))
                    {
                        if (sourceReason == null) sourceReason = CoreUnavailableReason(network.Core, coreReason);
                        continue;
                    }
                    if (!TradeUtility.PlayerSellableNow(item, trader))
                    {
                        if (sourceReason == null) sourceReason = "MS_Trade_Fail_ItemNotSellable".Translate();
                        continue;
                    }
                    shelfSources.TryGetValue(item, out var shelf);
                    int count = Math.Min(remaining, Available(item));
                    if (count <= 0) continue;
                    requests.Add(new StorageTradeRequest { Line = line, Network = network, Shelf = shelf, Item = item, Count = count,
                        Usable = () => Usable() && CoreEligible(network.Core, out _) && TradeUtility.PlayerSellableNow(item, trader) });
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
            Map map = negotiator.Map;
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            var batch = manager.CreateRecovery(groundTarget != null ? groundTarget.Position :
                trader is Pawn pawnTrader ? pawnTrader.Position : console.Position);
            var items = new List<Thing>();
            try
            {
                if (!transaction.Collect(line, batch.Contents, items))
                    throw new InvalidOperationException("Storage trade reservation became unavailable during delivery.");
                foreach (Thing item in items)
                    trader.GiveSoldThingToTrader(item, item.stackCount, negotiator);
            }
            finally
            {
                // Only undelivered pieces can be restored; never reclaim goods already owned by the trader.
                foreach (Thing item in items)
                {
                    if (!batch.Contents.Contains(item)) continue;
                    transaction.Restore(batch.Contents, item);
                }
                manager.ReleaseRecovery(batch);
            }
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
            sources.Clear();
            shelfSources.Clear();
        }
    }

    internal sealed class Dialog_StorageTrade : Dialog_Trade
    {
        private readonly StorageTradeSession session;
        private StorageItemFilterPanel itemFilters;
        private bool filtersWereEnabled;
        internal StorageItemFilterPanel ItemFilters => itemFilters ?? (itemFilters = new StorageItemFilterPanel(
            TradeSession.playerNegotiator.Map, CommonSearchWidget, () => StorageNativeItemFilters.InvalidateTrade(this),
            () => StorageNativeItemFilters.RebuildTrade(this)));
        public override Vector2 InitialSize => MagicStorageMod.Settings.TradeItemFilters
            ? new Vector2(Math.Min(1340f, UI.screenWidth), UI.screenHeight) : base.InitialSize;
        internal StorageTradeSession Session => session;
        internal Dialog_StorageTrade(Pawn negotiator, ITrader trader, StorageTradeSession session) : base(negotiator, trader)
        { this.session = session; }

        protected override Rect QuickSearchWidgetRect(Rect winRect, Rect inRect)
        {
            var rect = base.QuickSearchWidgetRect(winRect, inRect);
            if (MagicStorageMod.Settings.TradeItemFilters) rect.x += StorageItemFilterPanel.Width(inRect.width) + 16f;
            return rect;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 26f),
                (session.IsOrbital ? "MS_Trade_OrbitalInventoryNotice" : "MS_Trade_InventoryNotice").Translate());
            inRect.yMin += 28f;
            bool enabled = MagicStorageMod.Settings.TradeItemFilters;
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
