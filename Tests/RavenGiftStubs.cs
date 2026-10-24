// Inspected Raven 1.6/1.7 gift boundary. Production Harmony hooks, storage,
// reservations, staging and restoration run unchanged; Raven UI/value calculation is modeled.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace RimWorld
{
    public sealed class TransferableOneWay
    {
        public readonly List<Thing> things = new List<Thing>();
        public int CountToTransfer { get; private set; }
        public Thing AnyThing => things.FirstOrDefault();
        public int MaxCount
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get { return things.Sum(item => item.stackCount); }
        }
        public void AdjustTo(int count) { CountToTransfer = Math.Max(0, Math.Min(count, MaxCount)); }
        public void ForceTo(int count) { CountToTransfer = count; }
    }
}

namespace RavenRace.Features.FusangOrganization.UI
{
    public sealed class RadioSession
    {
        public Map CurrentMap;
        public Map Map => CurrentMap;
        public string MapReason => "Radio unavailable";
        public string ReportText, Receipt;
        public int Receipts;
        public RadioSession(Map map) { CurrentMap = map; }
        public void Report(string text) { ReportText = text; }
        public void GiftReceipt(string text) { Receipt = text; Receipts++; }
    }
    public abstract class RadioPage
    {
        protected readonly RadioSession Session;
        protected RadioPage(RadioSession session) { Session = session; }
    }
}

namespace RavenRace.Features.Operator.UI
{
    using RavenRace.Features.FusangOrganization.UI;
    using RavenRace.Features.Operator.Gifting;

    public sealed class RadioGiftTrader : ITrader
    {
        private readonly Map map;
        internal static readonly Dictionary<Map, List<Thing>> ExtraNative = new Dictionary<Map, List<Thing>>();
        public RadioGiftTrader(Map map) { this.map = map; }
        public IEnumerable<Thing> Goods => Enumerable.Empty<Thing>();
        public bool CanTradeNow => true;
        public Faction Faction => Faction.OfPlayer;
        public TraderKindDef TraderKind => new TraderKindDef();
        public void GiveSoldThingToTrader(Thing item, int count, Pawn negotiator) { }
        public void GiveSoldThingToPlayer(Thing item, int count, Pawn negotiator) { }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn negotiator)
        {
            if (map == null) return Enumerable.Empty<Thing>();
            var native = TradeUtility.AllLaunchableThingsForTrade(map, this);
            return ExtraNative.TryGetValue(map, out var held) ? native.Concat(held) : native;
        }
    }

    public sealed class RadioGiftPage : RadioPage
    {
        private List<TransferableOneWay> groups = new List<TransferableOneWay>();
        internal List<TransferableOneWay> Rows => groups;
        public RadioGiftPage(RadioSession session) : base(session) { }
        internal void RefreshForTest() { Refresh(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Refresh()
        {
            var selected = groups.Where(group => group.AnyThing != null).ToDictionary(group => group.AnyThing.def, group => group.CountToTransfer);
            groups = new RadioGiftTrader(Session.Map).ColonyThingsWillingToBuy(null).GroupBy(item => item.def).Select(bucket =>
            {
                var group = new TransferableOneWay(); group.things.AddRange(bucket);
                if (selected.TryGetValue(bucket.Key, out int count)) group.AdjustTo(count);
                return group;
            }).ToList();
        }
    }

    public static class RadioGiftActions
    {
        internal static int OriginalCalls;
        internal static bool Refuse;
        internal static Action<RadioSession, List<TransferableOneWay>> BeforeOriginal;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Submit(RadioSession session, List<TransferableOneWay> groups)
        {
            OriginalCalls++;
            BeforeOriginal?.Invoke(session, groups);
            if (Refuse) return false;
            if (session.Map == null) { session.Report(session.MapReason); return false; }
            var selected = groups.Where(group => group.CountToTransfer > 0).ToList();
            if (selected.Count == 0) { session.Report("Select a gift"); return false; }
            var valid = new HashSet<Thing>(new RadioGiftTrader(session.Map).ColonyThingsWillingToBuy(null));
            if (selected.Any(group => group.things.Where(item => !item.Destroyed && valid.Contains(item)).Sum(item => item.stackCount) < group.CountToTransfer))
            { session.Report("Gift changed"); return false; }
            int favor = 0;
            foreach (var group in selected)
            {
                int remaining = group.CountToTransfer;
                foreach (Thing item in group.things.ToList())
                    if (remaining > 0 && !item.Destroyed && valid.Contains(item))
                    {
                        int count = Math.Min(remaining, item.stackCount);
                        GiftingManager.HandleGift(item, count, out int change); favor += change;
                        item.SplitOff(count).Destroy(DestroyMode.Vanish); remaining -= count;
                    }
                group.AdjustTo(0);
            }
            session.GiftReceipt("Native receipt: " + favor);
            return true;
        }
    }
}

namespace RavenRace.Features.Operator.Gifting
{
    public static class GiftingManager
    {
        internal static int Favor, Calls;
        internal static Action<Thing, int> BeforeGift;
        public static void HandleGift(Thing item, int count, out int favorChange)
        { BeforeGift?.Invoke(item, count); Calls++; Favor += count; favorChange = count; }
    }
}
