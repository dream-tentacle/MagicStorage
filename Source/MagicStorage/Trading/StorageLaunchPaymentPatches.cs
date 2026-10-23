using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Native faction fees, ransom and mods share these query/payment entry points.
    // A non-null trader belongs to the separate ship trade session and is untouched.
    [StaticConstructorOnStartup]
    internal static class StorageLaunchPaymentPatches
    {
        static StorageLaunchPaymentPatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.trading.payment");
            harmony.Patch(AccessTools.Method(typeof(TradeUtility), nameof(TradeUtility.AllLaunchableThingsForTrade)),
                postfix: new HarmonyMethod(typeof(StorageLaunchPaymentPatches), nameof(LaunchableThings)));
            harmony.Patch(AccessTools.Method(typeof(TradeUtility), nameof(TradeUtility.LaunchThingsOfType)),
                prefix: new HarmonyMethod(typeof(StorageLaunchPaymentPatches), nameof(Pay)));
        }

        private static void LaunchableThings(Map map, ITrader trader, ref IEnumerable<Thing> __result)
        {
            if (map != null && trader == null) __result = WithStorage(__result, map);
        }

        private static IEnumerable<Thing> WithStorage(IEnumerable<Thing> original, Map map)
        {
            var seen = new HashSet<Thing>();
            foreach (Thing item in original)
                if (seen.Add(item)) yield return item;
            foreach (StorageNetwork network in Networks(map))
                foreach (Thing item in Stocks(network, null))
                    if (seen.Add(item)) yield return item;
        }

        private static List<StorageNetwork> Networks(Map map)
        {
            map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            var result = new List<StorageNetwork>();
            var seen = new HashSet<StorageNetwork>();
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
                if (building is Building_StorageCore core && StorageTradeSession.OrbitalCoreEligible(core, map) &&
                    seen.Add(core.Network)) result.Add(core.Network);
            return result;
        }

        private static List<Thing> Stocks(StorageNetwork network, ThingDef def)
        {
            var stacks = new List<Thing>();
            if (def == null) network.GetAllStacks(stacks);
            else network.GetStacks(def, stacks);
            stacks.RemoveAll(item => item == null || item.Destroyed || item.stackCount <= 0 ||
                item.def.category != ThingCategory.Item || !TradeUtility.PlayerSellableNow(item, null) ||
                // The native API returns real stacks and callers sum stackCount. Never
                // change a real stack or fabricate a copy to represent a reserved part.
                network.AvailableToWithdraw(item) != item.stackCount);
            stacks.Sort((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
            return stacks;
        }

        private static IEnumerable<Thing> Ground(Map map, ThingDef def)
        {
            var seen = new HashSet<Thing>();
            foreach (Building_OrbitalTradeBeacon beacon in Building_OrbitalTradeBeacon.AllPowered(map))
                foreach (IntVec3 cell in beacon.TradeableCells)
                    foreach (Thing item in cell.GetThingList(map))
                        if (item.def == def && StorageMaterialTransaction.GroundAvailable(map, item, false) &&
                            seen.Add(item)) yield return item;
        }

        private static bool Pay(ThingDef resDef, int debt, Map map, TradeShip trader)
        {
            if (map == null || trader != null || debt <= 0 || resDef == null) return true;
            List<StorageNetwork> networks = Networks(map);
            if (networks.Count == 0)
            {
                // A caller may have checked the balance while a core was still
                // covered. Losing that core must not turn a fee into a partial charge.
                long count = 0;
                foreach (Thing item in Ground(map, resDef))
                {
                    count += item.stackCount;
                    if (count >= debt) return true;
                }
                return Reject();
            }

            // Match the native ground-payment policy: ground job reservations do not
            // prevent launch. Stored stock still uses the shared outgoing reservations.
            using (var payment = new StorageMaterialTransaction(map, IntVec3.Invalid, false))
            {
                int remaining = debt;
                var seen = new HashSet<Thing>();
                foreach (Thing item in Ground(map, resDef))
                {
                    int count = Math.Min(remaining, item.stackCount);
                    if (!payment.Add(item, count)) return Reject();
                    seen.Add(item);
                    remaining -= count;
                    if (remaining == 0) break;
                }
                foreach (StorageNetwork network in networks)
                {
                    if (remaining == 0) break;
                    foreach (Thing item in Stocks(network, resDef))
                    {
                        if (!seen.Add(item)) continue;
                        int count = Math.Min(remaining, item.stackCount);
                        if (!payment.Add(item, count, network, network.Core)) return Reject();
                        remaining -= count;
                        if (remaining == 0) break;
                    }
                }
                if (remaining != 0 || !Eligible(payment, map) || !payment.TryStage() ||
                    !Eligible(payment, map) || !payment.ConsumeAll()) return Reject();
            }
            return false;
        }

        private static bool Eligible(StorageMaterialTransaction payment, Map map)
        {
            foreach (var request in payment.Requests)
                if (request.Network != null &&
                    !StorageTradeSession.OrbitalCoreEligible(request.Network.Core, map)) return false;
            return true;
        }

        private static bool Reject()
        {
            Messages.Message("MS_Payment_Unavailable".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }
}
