// Native enumeration/launch boundaries and a forwarding mod call. Production
// Harmony hooks, staging, reservations, rollback and inventory indexes are real.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Verse;

namespace Verse
{
    public partial class Thing { internal static Action<Thing> BeforePaymentTestSplit; }
    public static class PaymentDestroyBoundary
    { public static void Destroy(this Thing item, DestroyMode mode) { item.Destroy(); } }
}

namespace RimWorld
{
    public static partial class TradeUtility
    {
        internal static int NativeLaunches;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IEnumerable<Thing> AllLaunchableThingsForTrade(Map map, ITrader trader = null)
        {
            var seen = new HashSet<Thing>();
            foreach (var beacon in Building_OrbitalTradeBeacon.AllPowered(map))
                foreach (var cell in beacon.TradeableCells)
                    foreach (var item in cell.GetThingList(map))
                        if (item.Spawned && !item.Destroyed && item.def.category == ThingCategory.Item &&
                            PlayerSellableNow(item, trader) && seen.Add(item)) yield return item;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LaunchThingsOfType(ThingDef resDef, int debt, Map map, TradeShip trader)
        {
            NativeLaunches++;
            foreach (var item in AllLaunchableThingsForTrade(map, trader).Where(t => t.Spawned && t.def == resDef).ToArray())
            {
                if (debt <= 0) break;
                int count = Math.Min(debt, item.stackCount);
                if (trader == null) item.SplitOff(count).Destroy();
                else trader.GiveSoldThingToTrader(item, count, TradeSession.playerNegotiator);
                debt -= count;
            }
        }

        public static void LaunchSilver(Map map, int fee) => LaunchThingsOfType(ThingDefOf.Silver, fee, map, null);
    }
}

internal static class ForwardingPaymentMod
{
    internal static int Balance(Map map, ThingDef def) =>
        RimWorld.TradeUtility.AllLaunchableThingsForTrade(map).Where(t => t.def == def).Sum(t => t.stackCount);
    internal static void Pay(Map map, ThingDef def, int count) =>
        RimWorld.TradeUtility.LaunchThingsOfType(def, count, map, null);
}
