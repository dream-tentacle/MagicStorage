// Controlled native trade boundary. Tests exercise the production Harmony hooks,
// session, available-count override and actual storage transfer, not rendering or native pricing.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace UnityEngine
{
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public float yMin { get => y; set { height -= value - y; y = value; } }
    }
}
namespace Verse
{
    public static partial class Widgets { public static void Label(UnityEngine.Rect rect, string text) { } }
    public static class Messages
    {
        public static int Rejects;
        public static string LastText;
        public static void Message(string text, object type, bool historical) { Rejects++; LastText = text; }
    }
    public class Window
    {
        public virtual UnityEngine.Vector2 InitialSize => new UnityEngine.Vector2(1024, 900);
        public virtual QuickSearchWidget CommonSearchWidget => new QuickSearchWidget();
        protected virtual UnityEngine.Rect QuickSearchWidgetRect(UnityEngine.Rect winRect, UnityEngine.Rect inRect) => inRect;
        public virtual void PreOpen() { }
        public virtual void PostOpen() { }
        public virtual void PostClose() { }
        public virtual void DoWindowContents(UnityEngine.Rect rect) { }
    }
    public partial class WindowStack
    {
        internal Action<Window> BeforeAdd, AfterAdd;
        public T WindowOfType<T>() where T : class => Windows.OfType<T>().FirstOrDefault();
        public void Add(Window window) { BeforeAdd?.Invoke(window); window.PreOpen(); Windows.Add(window); window.PostOpen(); AfterAdd?.Invoke(window); }
        public bool TryRemove(Window window, bool doCloseSound = true)
        {
            if (!Windows.Remove(window)) return false;
            window.PostClose(); return true;
        }
    }
    public static class ThingMaker { public static Thing MakeThing(ThingDef def) => new Thing { def = def }; }
    public class Room { }
    public static class RoomBoundary { public static Room GetRoom(this Thing thing) => null; public static Room GetRoom(this IntVec3 cell, Map map) => null; }
    public static partial class GenRadial { public static int NumCellsInRadius(float radius) => 0; public static IntVec3[] RadialPattern = new IntVec3[0]; }
    public partial class Thing
    {
        public string LabelCap => "test item";
        public bool PreventPlayerSellingThingsNearby(out string reason) { reason = null; return false; }
    }
    public partial class Map
    {
        public BuildingLister listerBuildings = new BuildingLister();
        public Reachability reachability = new Reachability();
        public PassingShipManager passingShipManager = new PassingShipManager();
        public readonly List<Building_OrbitalTradeBeacon> Beacons = new List<Building_OrbitalTradeBeacon>();
    }
    public class BuildingLister { public List<Building> allBuildingsColonist = new List<Building>(); }
    public enum TraverseMode { PassDoors }
    public class Reachability
    {
        public HashSet<Thing> Unreachable = new HashSet<Thing>();
        public bool CanReach(IntVec3 start, Thing target, PathEndMode endMode, TraverseMode mode, Danger danger) => !Unreachable.Contains(target);
    }
    public partial class Pawn : ITrader
    {
        public bool CanTradeNow = true, AllowedTrade = true;
        public TraderKindDef TraderKind = new TraderKindDef();
        public IEnumerable<Thing> Goods => inventory.innerContainer;
        public TradePermission CanTradeWith(Faction faction, TraderKindDef kind) => new TradePermission { Accepted = AllowedTrade };
        public System.Action<Thing> BeforeReceivingTrade;
        public void GiveSoldThingToTrader(Thing item, int count, Pawn negotiator)
        { BeforeReceivingTrade?.Invoke(item); inventory.innerContainer.TryAdd(item.SplitOff(count), false); }
        bool ITrader.CanTradeNow => CanTradeNow;
        Faction ITrader.Faction => Faction;
        TraderKindDef ITrader.TraderKind => TraderKind;
        public void GiveSoldThingToPlayer(Thing item, int count, Pawn negotiator)
        {
            var part = item.SplitOff(count); part.Spawned = true; part.Map = negotiator.Map;
            part.Map.Ground.Add(part);
        }
    }
    public struct TradePermission { public bool Accepted; }
}
namespace RimWorld
{
    public static class MessageTypeDefOf { public static object RejectInput; }
    public enum Transactor { Colony, Trader }
    public enum TradeAction { None, PlayerSells, PlayerBuys }
    public enum TradeCurrency { Silver, Favor }
    public class TraderKindDef { }
    public interface ITrader
    {
        IEnumerable<Thing> Goods { get; }
        bool CanTradeNow { get; }
        Faction Faction { get; }
        TraderKindDef TraderKind { get; }
        void GiveSoldThingToTrader(Thing item, int count, Pawn negotiator);
        void GiveSoldThingToPlayer(Thing item, int count, Pawn negotiator);
    }
    public class PassingShipManager
    { public readonly List<TradeShip> passingShips = new List<TradeShip>(); }
    public class Building_CommsConsole : Building { public bool CanUseCommsNow = true; }
    public class Building_OrbitalTradeBeacon
    {
        public bool Powered = true;
        public readonly HashSet<IntVec3> TradeableCells = new HashSet<IntVec3>();
        public static IEnumerable<Building_OrbitalTradeBeacon> AllPowered(Map map) => map.Beacons.Where(b => b.Powered);
    }
    public class TradeShip : ITrader
    {
        public Map Map;
        public bool CanTradeNow { get; set; } = true;
        public Faction Faction { get; set; }
        public TraderKindDef TraderKind { get; } = new TraderKindDef();
        public readonly ThingOwner Stock = new ThingOwner(null);
        public IEnumerable<Thing> Goods => Stock;
        public int DropPods;
        public void GiveSoldThingToTrader(Thing item, int count, Pawn negotiator) => Stock.TryAdd(item.SplitOff(count), false);
        public void GiveSoldThingToPlayer(Thing item, int count, Pawn negotiator)
        {
            var part = item.SplitOff(count); part.Spawned = true; part.Map = Map;
            Map.Ground.Add(part); DropPods++;
        }
    }
    public static class PawnCapacityDefOf { public static object Talking = new object(); }
    public static partial class ThingDefOf { public static ThingDef Silver = new ThingDef(); }
    public static partial class TradeUtility { public static bool PlayerSellableNow(Thing item, ITrader trader) => !item.stale; }
    public static class TradeSession
    {
        public static ITrader trader;
        public static Pawn playerNegotiator;
        public static TradeDeal deal;
        public static bool giftMode;
        public static TradeCurrency TradeCurrency = TradeCurrency.Silver;
    }
    public class Tradeable
    {
        public Thing AnyThing => thingsColony.FirstOrDefault() ?? thingsTrader.FirstOrDefault();
        public readonly List<Thing> thingsColony = new List<Thing>(), thingsTrader = new List<Thing>();
        public int CountToTransfer;
        public int CountToTransferToSource => Math.Max(0, CountToTransfer);
        public int CountToTransferToDestination => Math.Max(0, -CountToTransfer);
        public TradeAction ActionToDo => CountToTransfer < 0 ? TradeAction.PlayerSells : CountToTransfer > 0 ? TradeAction.PlayerBuys : TradeAction.None;
        public ThingDef ThingDef => (thingsColony.FirstOrDefault() ?? thingsTrader.FirstOrDefault())?.def;
        public bool IsCurrency => ThingDef == ThingDefOf.Silver;
        public bool IsFavor => false;
        public void AddThing(Thing item, Transactor trans) => (trans == Transactor.Colony ? thingsColony : thingsTrader).Add(item);
        public virtual int CountHeldBy(Transactor trans) => (trans == Transactor.Colony ? thingsColony : thingsTrader).Sum(t => t.stackCount);
        public int CountPostDealFor(Transactor trans) => CountHeldBy(trans) + (trans == Transactor.Colony ? CountToTransfer : -CountToTransfer);
        public virtual void ResolveTrade()
        {
            if (ActionToDo == TradeAction.PlayerBuys)
            {
                int remaining = CountToTransferToSource;
                foreach (Thing stock in thingsTrader)
                {
                    int count = Math.Min(remaining, stock.stackCount);
                    if (count <= 0) continue;
                    TradeSession.trader.GiveSoldThingToPlayer(stock, count, TradeSession.playerNegotiator);
                    remaining -= count;
                }
            }
            else if (ActionToDo == TradeAction.PlayerSells)
            {
                int remaining = CountToTransferToDestination;
                foreach (Thing stock in thingsColony)
                {
                    int count = Math.Min(remaining, stock.stackCount);
                    if (count <= 0) continue;
                    TradeSession.trader.GiveSoldThingToTrader(stock, count, TradeSession.playerNegotiator);
                    remaining -= count;
                }
            }
        }
    }
    public class Tradeable_Pawn : Tradeable { }
    public class Tradeable_RoyalFavor : Tradeable { }
    public static partial class TransferableUtility
    {
        public static Tradeable TradeableMatching(Thing item, List<Tradeable> list) => list.Find(t => t.ThingDef == item.def);
    }
    public class TradeDeal
    {
        public static List<Thing> OrdinaryGoods = new List<Thing>();
        public static int NativeFills, NativeExecutions;
        public static bool RefuseExecution, ThrowExecution;
        public readonly List<Tradeable> AllTradeables = new List<Tradeable>();
        public readonly List<string> cannotSellReasons = new List<string>();
        public Tradeable CurrencyTradeable => AllTradeables.Find(t => t.IsCurrency);
        public TradeDeal() { Reset(); }
        public void Reset() { AllTradeables.Clear(); cannotSellReasons.Clear(); AddAllTradeables(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddAllTradeables()
        {
            NativeFills++;
            foreach (Thing item in OrdinaryGoods)
            {
                var line = new Tradeable(); line.AddThing(item, Transactor.Colony); AllTradeables.Add(line);
            }
        }
        public void UpdateCurrencyCount()
        { if (!TradeSession.giftMode && CurrencyTradeable != null) CurrencyTradeable.CountToTransfer = -AllTradeables.Where(t => !t.IsCurrency).Sum(t => t.CountToTransfer); }
        private void LimitCurrencyCountToFunds()
        {
            if (CurrencyTradeable != null && CurrencyTradeable.CountToTransfer > CurrencyTradeable.CountHeldBy(Transactor.Trader))
                CurrencyTradeable.CountToTransfer = CurrencyTradeable.CountHeldBy(Transactor.Trader);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool TryExecute(out bool actuallyTraded)
        {
            NativeExecutions++; actuallyTraded = false;
            if (ThrowExecution) throw new InvalidOperationException("Controlled native trade exception");
            if (RefuseExecution) return false;
            UpdateCurrencyCount(); LimitCurrencyCountToFunds();
            foreach (var line in AllTradeables) { if (line.ActionToDo != TradeAction.None) actuallyTraded = true; line.ResolveTrade(); }
            Reset(); return true;
        }
    }
    public class Dialog_Trade : Window
    {
        internal static Action ConstructorHook;
        private readonly bool giftsOnly = false;
        public int Flashes;
        public Dialog_Trade(Pawn negotiator, ITrader trader)
        {
            TradeSession.playerNegotiator = negotiator; TradeSession.trader = trader;
            TradeSession.giftMode = false; TradeSession.deal = new TradeDeal();
            ConstructorHook?.Invoke();
        }
        public void FlashSilver() { Flashes++; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override void PostOpen() { if (giftsOnly) TradeSession.giftMode = true; }
        public override void DoWindowContents(UnityEngine.Rect rect) { }
        public override void PostClose() { }
    }
}
