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
        private readonly Pawn negotiator;
        private readonly ITrader trader;
        private readonly Building_CommsConsole console;
        private TradeDeal deal;
        private StorageTradeTransaction transaction;
        private bool closed;

        private StorageTradeSession(Pawn negotiator, ITrader trader, Building_CommsConsole console)
        { this.negotiator = negotiator; this.trader = trader; this.console = console; }

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

        internal static void Open(Pawn negotiator, ITrader trader, Building_CommsConsole console = null)
        {
            if (opening != null || Find.WindowStack.WindowOfType<Dialog_Trade>() != null) return;
            var session = new StorageTradeSession(negotiator, trader, console);
            opening = session;
            try { Find.WindowStack.Add(new Dialog_StorageTrade(negotiator, trader, session)); }
            catch { session.Dispose(); throw; }
            finally { opening = null; }
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
            if (closed || !ReferenceEquals(TradeSession.trader, trader) ||
                TradeSession.playerNegotiator != negotiator || !negotiator.Spawned || !trader.CanTradeNow ||
                !negotiator.CanTradeWith(trader.Faction, trader.TraderKind).Accepted) return false;
            if (trader is Pawn pawnTrader)
                return pawnTrader.Spawned && negotiator.Map == pawnTrader.Map && !pawnTrader.mindState.traderDismissed;
            if (trader is TradeShip ship)
                return ship.Map == negotiator.Map && negotiator.Map.passingShipManager.passingShips.Contains(ship) &&
                    console != null && console.Spawned && console.Map == negotiator.Map && console.CanUseCommsNow &&
                    negotiator.health.capacities.CapableOf(PawnCapacityDefOf.Talking);
            return false;
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
            Map map = negotiator.Map;
            if (core == null || !core.CanWork || core.Faction != Faction.OfPlayer || core.Map != map || core.Position.Fogged(map)) return false;
            if (trader is TradeShip && !BeaconCovers(core)) return false;
            if (trader is Pawn pawnTrader &&
                !map.reachability.CanReach(pawnTrader.Position, core, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Some)) return false;
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
            return transaction?.Available(network, item) ?? network.AvailableToWithdraw(item);
        }

        internal void FillInventory()
        {
            transaction?.Dispose(); transaction = null;
            sources.Clear();
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
                    foreach (Thing item in stacks)
                    {
                        if (item == null || item.Destroyed || item is Pawn || sources.ContainsKey(item) ||
                            core.Network.AvailableToWithdraw(item) <= 0 || !TradeUtility.PlayerSellableNow(item, trader)) continue;
                        sources.Add(item, core.Network);
                        Add(item, Transactor.Colony);
                    }
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

        private void Add(Thing item, Transactor trans)
        {
            Tradeable entry = TransferableUtility.TradeableMatching(item, deal.AllTradeables);
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
            if (!Usable()) return Reject();
            negotiator.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            deal.UpdateCurrencyCount();
            // Match native affordability before limiting the trader's payment after its short-funds confirmation.
            if (!TradeSession.giftMode && (deal.CurrencyTradeable == null ||
                deal.CurrencyTradeable.CountPostDealFor(Transactor.Colony) < 0)) return Reject();
            limitCurrency(deal);
            var requests = new List<StorageTradeRequest>();
            foreach (Tradeable line in deal.AllTradeables)
            {
                if (line.ActionToDo == TradeAction.PlayerBuys && line.CountToTransferToSource > line.CountHeldBy(Transactor.Trader))
                    return Reject();
                if (line.ActionToDo != TradeAction.PlayerSells) continue;
                int remaining = line.CountToTransferToDestination;
                foreach (Thing item in line.thingsColony)
                {
                    if (remaining == 0) break;
                    if (!sources.TryGetValue(item, out StorageNetwork network) || !CoreEligible(network.Core, out _) ||
                        !TradeUtility.PlayerSellableNow(item, trader)) continue;
                    int count = Math.Min(remaining, network.AvailableToWithdraw(item));
                    if (count <= 0) continue;
                    requests.Add(new StorageTradeRequest { Line = line, Network = network, Item = item, Count = count,
                        Usable = () => Usable() && CoreEligible(network.Core, out _) && TradeUtility.PlayerSellableNow(item, trader) });
                    remaining -= count;
                }
                if (remaining != 0) return Reject();
            }
            var candidate = new StorageTradeTransaction(requests, Usable);
            if (!candidate.Reserve()) return Reject();
            transaction = prepared = candidate;
            return true;
        }

        private bool Reject()
        {
            Find.WindowStack.WindowOfType<Dialog_Trade>()?.FlashSilver();
            Messages.Message("MS_Trade_Unavailable".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        internal void Deliver(Tradeable_Storage line)
        {
            if (transaction == null) throw new InvalidOperationException("Storage trade has no prepared transaction.");
            if (transaction.PendingFor(line) != line.CountToTransferToDestination)
                throw new InvalidOperationException("Storage trade quantity changed after reservation.");
            Map map = negotiator.Map;
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            var batch = manager.CreateRecovery(trader is Pawn pawnTrader ? pawnTrader.Position : console.Position);
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
                    transaction.SourceFor(item)?.TryStore(batch.Contents, item, item.stackCount);
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
        }
    }

    internal sealed class Dialog_StorageTrade : Dialog_Trade
    {
        private readonly StorageTradeSession session;
        internal Dialog_StorageTrade(Pawn negotiator, ITrader trader, StorageTradeSession session) : base(negotiator, trader)
        { this.session = session; }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 26f),
                (TradeSession.trader is TradeShip ? "MS_Trade_OrbitalInventoryNotice" : "MS_Trade_InventoryNotice").Translate());
            inRect.yMin += 28f;
            base.DoWindowContents(inRect);
        }

        public override void PostClose()
        { try { base.PostClose(); } finally { session.Dispose(); } }
    }
}
