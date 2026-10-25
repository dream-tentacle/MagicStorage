using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    public enum StorageTradeInventoryStatus { Ready = 0, InvalidRequest = 1, TraderUnavailable = 2, NoEligibleInventory = 3, Failed = 4 }

    public sealed class StorageTradeInventoryResult
    {
        public StorageTradeInventoryStatus Status { get; }
        public StorageTradeInventory Inventory { get; }
        public string Reason { get; }
        internal StorageTradeInventoryResult(StorageTradeInventoryStatus status, StorageTradeInventory inventory = null, string reason = null)
        { Status = status; Inventory = inventory; Reason = reason; }
    }

    public sealed class StorageTradeItemRequest
    {
        public Thing Item { get; }
        public int Count { get; }
        public StorageTradeItemRequest(Thing item, int count) { Item = item; Count = count; }
    }

    /// <summary>Main-thread inventory scope, independent of windows and global TradeSession. Dispose after use.</summary>
    public sealed class StorageTradeInventory : IDisposable
    {
        private readonly Pawn negotiator;
        private readonly ITrader trader;
        private readonly Building_CommsConsole console;
        private readonly Thing groundTarget;
        private readonly Func<bool> additionalUsable;
        private readonly HashSet<Building_StorageCore> selectedCores;
        private readonly Building_StorageUnit[] units;
        private readonly bool portable;
        private readonly bool IsOrbital;
        private readonly Dictionary<Thing, StorageTradeRequest> sources = new Dictionary<Thing, StorageTradeRequest>();
        private readonly List<Thing> items = new List<Thing>();
        private readonly List<StorageTradeTransaction> transactions = new List<StorageTradeTransaction>();
        private bool callbackFailed, closed, refreshing;
        private int revision;
        internal readonly List<string> CannotSellReasons = new List<string>();
        public IReadOnlyList<Thing> Items => items.AsReadOnly();
        public string UnavailableReason => TraderUnavailableReason() ?? NetworkUnavailableReason();
        public string LastFailureReason { get; private set; }

        internal StorageTradeInventory(StorageTradeOptions options)
        {
            negotiator = options.Negotiator; trader = options.Trader; console = options.CommsConsole;
            groundTarget = options.GroundTarget; additionalUsable = options.IsStillValid;
            portable = options.Mode == StorageTradeMode.Portable; IsOrbital = options.Mode == StorageTradeMode.Orbital;
            units = options.Units?.ToArray();
            selectedCores = options.NetworkScope == StorageTradeNetworkScope.SpecifiedCores
                ? new HashSet<Building_StorageCore>(options.Cores) : null;
        }

        /// <summary>Rebuilds the item snapshot and releases this inventory's outstanding reservations.</summary>
        public bool Refresh()
        {
            if (refreshing) return false;
            refreshing = true;
            try
            {
                ReleaseTransactions(); revision++;
                sources.Clear(); items.Clear(); CannotSellReasons.Clear();
                LastFailureReason = UnavailableReason;
                if (LastFailureReason != null) return false;
                if (portable)
                {
                    foreach (var unit in units)
                        foreach (Thing item in unit.GetDirectlyHeldThings())
                            Add(item, new StorageTradeRequest { Unit = unit });
                }
                else
                {
                    var seen = new HashSet<StorageNetwork>();
                    foreach (Building building in negotiator.Map.listerBuildings.allBuildingsColonist)
                    {
                        if (!(building is Building_StorageCore core)) continue;
                        if (!CoreEligible(core, out string reason))
                        {
                            if (reason != null && !CannotSellReasons.Contains(reason)) CannotSellReasons.Add(reason);
                            continue;
                        }
                        if (!seen.Add(core.Network)) continue;
                        var stacks = new List<Thing>(); core.Network.GetAllStacks(stacks);
                        foreach (Thing item in stacks.OrderBy(item => item.thingIDNumber))
                            Add(item, new StorageTradeRequest { Network = core.Network });
                        foreach (var shelf in negotiator.Map.GetComponent<MapComponent_StorageNetworks>().SupplyShelves)
                            if (shelf.Spawned && shelf.Network == core.Network)
                                foreach (Thing item in shelf.GetSlotGroup().HeldThings.OrderBy(item => item.thingIDNumber))
                                    Add(item, new StorageTradeRequest { Network = core.Network, Shelf = shelf });
                    }
                }
                return true;
            }
            finally { refreshing = false; }
        }

        private void Add(Thing item, StorageTradeRequest source)
        {
            if (item == null || item.Destroyed || item is Pawn || sources.ContainsKey(item) ||
                !TradeUtility.PlayerSellableNow(item, trader)) return;
            source.Item = item;
            if (SourceAvailable(source, null) <= 0) return;
            sources.Add(item, source); items.Add(item);
        }

        public int Available(Thing item) => UnavailableReason == null ? Available(item, null) : 0;

        internal int Available(Thing item, StorageTradeTransaction transaction)
        {
            // Called repeatedly while drawing native rows. Full reachability/policy checks belong to refresh and settlement.
            if (closed || item == null || item.Destroyed || !sources.TryGetValue(item, out var source)) return 0;
            return SourceAvailable(source, transaction);
        }

        private static int SourceAvailable(StorageTradeRequest source, StorageTradeTransaction transaction)
        {
            if (source.Unit != null) return transaction?.AvailablePortable(source.Unit, source.Item) ??
                StorageTradeTransaction.PortableAvailable(source.Unit, source.Item);
            return transaction?.Available(source.Network, source.Item, source.Shelf) ?? (source.Shelf == null
                ? source.Network.AvailableToWithdraw(source.Item) :
                StorageTradeTransaction.ShelfAvailable(source.Network, source.Shelf, source.Item));
        }

        internal bool IsShelfItem(Thing item) => sources.TryGetValue(item, out var source) && source.Shelf != null;

        internal string ItemUnavailableReason(Thing item)
        {
            if (closed || item == null || !sources.TryGetValue(item, out var source)) return "MS_Trade_Fail_SourceChanged".Translate();
            if (source.Unit != null)
            {
                if (!PortableUnitEligible(source.Unit) || item.holdingOwner != source.Unit.GetDirectlyHeldThings())
                    return "MS_Trade_Fail_SourceChanged".Translate();
            }
            else if (!CoreEligible(source.Network.Core, out var reason)) return CoreUnavailableReason(source.Network.Core, reason);
            if (!TradeUtility.PlayerSellableNow(item, trader)) return "MS_Trade_Fail_ItemNotSellable".Translate();
            return null;
        }

        private static bool PortableUnitEligible(Building_StorageUnit unit) =>
            unit != null && !unit.Destroyed && !unit.Spawned && unit.Faction == Faction.OfPlayer;

        internal StorageTradeRequest CreateRequest(Thing item, object line, int count, Func<bool> usable = null)
        {
            var source = sources[item]; int expectedRevision = revision;
            return new StorageTradeRequest
            {
                Item = item, Count = count, Line = line, Network = source.Network, Shelf = source.Shelf, Unit = source.Unit,
                Usable = () => revision == expectedRevision && UnavailableReason == null && ItemUnavailableReason(item) == null &&
                    (usable == null || usable())
            };
        }

        internal void Track(StorageTradeTransaction transaction)
        { transactions.RemoveAll(candidate => candidate.IsDisposed); transactions.Add(transaction); }

        /// <summary>Claims exact real stacks, including currency. Never removes goods until Deliver is called.</summary>
        public bool TryReserve(IEnumerable<StorageTradeItemRequest> requests, out StorageTradeReservation reservation)
        {
            reservation = null; LastFailureReason = UnavailableReason;
            if (LastFailureReason != null) return false;
            if (requests == null) { LastFailureReason = "Requests are required."; return false; }
            var claims = new List<StorageTradeRequest>();
            foreach (var request in requests)
            {
                if (request == null || request.Count <= 0 || ItemUnavailableReason(request.Item) != null)
                { LastFailureReason = "Invalid or unavailable inventory item."; return false; }
                claims.Add(CreateRequest(request.Item, request.Item, request.Count));
            }
            var transaction = new StorageTradeTransaction(claims, () => UnavailableReason == null);
            if (!transaction.Reserve())
            {
                LastFailureReason = transaction.FailureKey;
                transaction.Dispose(); return false;
            }
            Track(transaction);
            reservation = new StorageTradeReservation(this, transaction, claims.Select(claim => claim.Item).ToArray());
            return true;
        }

        internal bool Deliver(StorageTradeTransaction transaction, object line, Action<Thing, int> receiver)
        {
            if (!transaction.Valid()) return false;
            Map map = portable ? null : negotiator.Map;
            var manager = map?.GetComponent<MapComponent_StorageNetworks>();
            var recovery = manager?.CreateRecovery(groundTarget != null ? groundTarget.Position :
                trader is Pawn pawnTrader ? pawnTrader.Position : console != null ? console.Position : IntVec3.Invalid);
            ThingOwner holder = recovery != null ? recovery.Contents : new ThingOwner<Thing>(null, false, LookMode.Deep);
            var collected = new List<Thing>();
            try
            {
                if (!transaction.Collect(line, holder, collected)) return false;
                foreach (Thing item in collected)
                {
                    receiver(item, item.stackCount);
                    if (holder.Contains(item)) return false;
                }
                return true;
            }
            finally
            {
                foreach (Thing item in collected)
                    if (holder.Contains(item)) transaction.Restore(holder, item);
                if (recovery != null) manager.ReleaseRecovery(recovery);
            }
        }

        private void ReleaseTransactions()
        { foreach (var transaction in transactions) transaction.Dispose(); transactions.Clear(); }

        public void Dispose()
        {
            if (closed) return;
            closed = true; revision++; ReleaseTransactions(); sources.Clear(); items.Clear();
        }

        internal string TraderUnavailableReason()
        {
            if (closed) return "MS_Trade_Fail_SessionClosed".Translate();
            if (negotiator.Destroyed || (portable ? negotiator.Spawned : !negotiator.Spawned)) return "MS_Trade_Fail_NegotiatorUnavailable".Translate();
            if (!portable && trader is Pawn pawnTrader)
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
                var ship = (PassingShip)trader;
                if (ship.Map != negotiator.Map || !negotiator.Map.passingShipManager.passingShips.Contains(ship))
                    return "MS_Trade_Fail_ShipUnavailable".Translate();
                if (console == null || console.Destroyed || !console.Spawned || console.Map != negotiator.Map)
                    return "MS_Trade_Fail_ConsoleMissing".Translate();
                if (!console.CanUseCommsNow) return "MS_Trade_Fail_ConsoleUnavailable".Translate();
                if (!negotiator.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
                    return "MS_Trade_Fail_CannotTalk".Translate();
            }
            else if (!portable && (groundTarget == null || groundTarget.Destroyed || !groundTarget.Spawned || groundTarget.Map != negotiator.Map))
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

        internal string NetworkUnavailableReason()
        {
            if (portable)
            {
                foreach (var unit in units)
                    if (!PortableUnitEligible(unit)) return "MS_Trade_Fail_PortableUnit".Translate();
                return null;
            }
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
            if (!StorageTradeSession.HasCore(map)) return false;
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
                if (building is Building_StorageCore core && core.CanWork && !core.Position.Fogged(map) &&
                    BeaconCovers(core)) return true;
            return false;
        }

        internal static bool BeaconCovers(Building_StorageCore core) =>
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

    }

    /// <summary>A short-lived reservation for one synchronous settlement. Dispose on cancellation or failure.</summary>
    public sealed class StorageTradeReservation : IDisposable
    {
        private readonly StorageTradeInventory inventory;
        private readonly StorageTradeTransaction transaction;
        private readonly Thing[] items;
        private bool delivered;
        internal StorageTradeReservation(StorageTradeInventory inventory, StorageTradeTransaction transaction, Thing[] items)
        { this.inventory = inventory; this.transaction = transaction; this.items = items; }
        public bool IsValid => transaction.Valid();

        /// <summary>Passes detached real goods to the original receiver. It must consume or transfer each entire stack.</summary>
        public bool Deliver(Action<Thing, int> receiver)
        {
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));
            if (delivered || !transaction.Valid()) return false;
            delivered = true;
            try
            {
                foreach (Thing item in items)
                    if (!inventory.Deliver(transaction, item, receiver)) return false;
                return true;
            }
            finally { Dispose(); }
        }

        public void Dispose() => transaction.Dispose();
    }
}
