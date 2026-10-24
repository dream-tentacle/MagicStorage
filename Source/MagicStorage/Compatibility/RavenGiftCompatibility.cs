using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // All adaptation lives here. Raven still owns gift valuation, consumption and receipts.
    internal sealed class RavenGiftCompatibility : IStorageCompatibility
    {
        private sealed class GiftGroup
        {
            internal Map Map;
            internal readonly HashSet<Thing> Stored = new HashSet<Thing>();
        }

        private sealed class Submission : IDisposable
        {
            internal Map Map;
            internal object Session;
            internal StorageMaterialTransaction Transfer;
            internal readonly Dictionary<TransferableOneWay, List<Thing>> Originals = new Dictionary<TransferableOneWay, List<Thing>>();
            internal readonly List<Thing> Staged = new List<Thing>();
            public void Dispose()
            {
                if (active == this) active = null;
                try { Transfer?.Dispose(); }
                finally
                {
                    foreach (var entry in Originals)
                    {
                        entry.Key.things.Clear();
                        entry.Key.things.AddRange(entry.Value);
                    }
                }
            }
        }

        private static readonly ConditionalWeakTable<TransferableOneWay, GiftGroup> giftGroups =
            new ConditionalWeakTable<TransferableOneWay, GiftGroup>();
        [ThreadStatic] private static Submission active;
        private static Type traderType;
        private static FieldInfo traderMap, pageSession, pageGroups;
        private static MethodInfo sessionMap, sessionReport, colonyThings;
        public string PackageId => "ZuoYao.RavenRace";

        public void Apply(Harmony harmony)
        {
            Type trader = AccessTools.TypeByName("RavenRace.Features.Operator.UI.RadioGiftTrader");
            Type page = AccessTools.TypeByName("RavenRace.Features.Operator.UI.RadioGiftPage");
            Type session = AccessTools.TypeByName("RavenRace.Features.FusangOrganization.UI.RadioSession");
            Type actions = AccessTools.TypeByName("RavenRace.Features.Operator.UI.RadioGiftActions");
            var mapField = trader == null ? null : AccessTools.Field(trader, "map");
            var ownerField = page == null ? null : AccessTools.Field(page, "Session");
            var groupsField = page == null ? null : AccessTools.Field(page, "groups");
            var mapGetter = session == null ? null : AccessTools.PropertyGetter(session, "Map");
            var report = session == null ? null : AccessTools.Method(session, "Report", new[] { typeof(string) });
            var source = trader == null ? null : AccessTools.Method(trader, "ColonyThingsWillingToBuy", new[] { typeof(Pawn) });
            var refresh = page == null ? null : AccessTools.DeclaredMethod(page, "Refresh", Type.EmptyTypes);
            var submit = actions == null || session == null ? null : AccessTools.DeclaredMethod(actions, "Submit",
                new[] { session, typeof(List<TransferableOneWay>) });
            var maxCount = AccessTools.PropertyGetter(typeof(TransferableOneWay), "MaxCount");
            if (trader == null || !typeof(ITrader).IsAssignableFrom(trader) || trader.GetConstructor(new[] { typeof(Map) }) == null ||
                mapField?.FieldType != typeof(Map) || ownerField?.FieldType != session ||
                groupsField?.FieldType != typeof(List<TransferableOneWay>) || mapGetter?.ReturnType != typeof(Map) ||
                report?.ReturnType != typeof(void) || source?.ReturnType != typeof(IEnumerable<Thing>) ||
                refresh?.ReturnType != typeof(void) || submit?.ReturnType != typeof(bool) || !submit.IsStatic || maxCount == null)
                throw new InvalidOperationException("Raven Race gift API was not found; no gift patches installed.");

            traderType = trader; traderMap = mapField; pageSession = ownerField; pageGroups = groupsField;
            sessionMap = mapGetter; sessionReport = report; colonyThings = source;
            var targets = new[] { source, refresh, maxCount, submit };
            try
            {
                harmony.Patch(source, postfix: Hook(nameof(Inventory)));
                harmony.Patch(refresh, postfix: Hook(nameof(Refreshed)));
                harmony.Patch(maxCount, prefix: Hook(nameof(Maximum)));
                harmony.Patch(submit, prefix: Hook(nameof(BeginSubmit)), finalizer: Hook(nameof(EndSubmit)));
            }
            catch
            {
                foreach (var target in targets) harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
                throw;
            }
        }

        private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(RavenGiftCompatibility), name);
        private static Map MapOf(object session) => session == null ? null : (Map)sessionMap.Invoke(session, null);
        private static bool Eligible(StorageNetwork network, Map map) => network != null &&
            StorageTradeSession.PaymentCoreEligible(network.Core, map);

        private static bool Stored(Thing item, Map map, out StorageNetwork network)
        {
            network = null;
            if (item == null || item.Destroyed || !(item.holdingOwner?.Owner is Building_StorageUnit unit) ||
                !unit.Spawned || unit.Destroyed || unit.Map != map || unit.Faction != Faction.OfPlayer) return false;
            network = unit.Network;
            return Eligible(network, map);
        }

        private static void Inventory(object __instance, ref IEnumerable<Thing> __result)
        {
            Map map = (Map)traderMap.GetValue(__instance);
            if (map != null) __result = WithStorage(__result, map, (ITrader)__instance, active);
        }

        private static IEnumerable<Thing> WithStorage(IEnumerable<Thing> original, Map map, ITrader trader, Submission submission)
        {
            var seen = new HashSet<Thing>();
            foreach (Thing item in original) if (item != null && seen.Add(item)) yield return item;
            if (submission != null && submission.Map == map)
            {
                // During the original Submit, only prepared real objects may pass its source recheck.
                foreach (Thing item in submission.Staged)
                    if (!item.Destroyed && item.stackCount > 0 && seen.Add(item)) yield return item;
                yield break;
            }
            map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            var networks = new HashSet<StorageNetwork>();
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
            {
                if (!(building is Building_StorageCore core) || !Eligible(core.Network, map) || !networks.Add(core.Network)) continue;
                var stacks = new List<Thing>(); core.Network.GetAllStacks(stacks);
                stacks.Sort((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
                foreach (Thing item in stacks)
                    if (item != null && !item.Destroyed && item.def.category == ThingCategory.Item &&
                        core.Network.AvailableToWithdraw(item) > 0 && TradeUtility.PlayerSellableNow(item, trader) && seen.Add(item))
                        yield return item;
            }
        }

        private static void Tag(TransferableOneWay group, Map map)
        {
            var context = new GiftGroup { Map = map };
            foreach (Thing item in group.things)
                if (item?.holdingOwner?.Owner is Building_StorageUnit) context.Stored.Add(item);
            // Keep known storage references even if a stack leaves its unit between refreshes.
            if (giftGroups.TryGetValue(group, out var previous)) context.Stored.UnionWith(previous.Stored);
            giftGroups.Remove(group);
            if (context.Stored.Count != 0) giftGroups.Add(group, context);
        }

        private static void Refreshed(object __instance)
        {
            Map map = MapOf(pageSession.GetValue(__instance));
            if (map == null) return;
            foreach (var group in (List<TransferableOneWay>)pageGroups.GetValue(__instance))
            {
                Tag(group, map);
                if (giftGroups.TryGetValue(group, out _)) group.AdjustTo(Math.Min(group.CountToTransfer, group.MaxCount));
            }
        }

        private static bool Maximum(TransferableOneWay __instance, ref int __result)
        {
            if (!giftGroups.TryGetValue(__instance, out var context)) return true;
            context.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            long count = 0;
            var seen = new HashSet<Thing>();
            foreach (Thing item in __instance.things)
            {
                if (item == null || item.Destroyed || !seen.Add(item)) continue;
                if (context.Stored.Contains(item))
                    count += Stored(item, context.Map, out var network) ? network.AvailableToWithdraw(item) : StagedCount(item, context.Map);
                else count += item.stackCount;
            }
            __result = (int)Math.Min(int.MaxValue, count);
            return false;
        }

        private static int StagedCount(Thing item, Map map) => active != null && active.Map == map &&
            active.Staged.Contains(item) ? item.stackCount : 0;

        private static bool IsStorageReference(TransferableOneWay group, Thing item) =>
            item?.holdingOwner?.Owner is Building_StorageUnit ||
            (item != null && giftGroups.TryGetValue(group, out var context) && context.Stored.Contains(item));

        private static bool Reject(object session, ref bool result)
        {
            result = false;
            sessionReport.Invoke(session, new object[] { "MS_RavenGift_Unavailable".Translate().ToString() });
            return false;
        }

        private static bool BeginSubmit(object __0, List<TransferableOneWay> __1, ref bool __result, out Submission __state)
        {
            __state = null;
            if (active != null) return Reject(__0, ref __result);
            Map map = MapOf(__0);
            if (map == null || __1 == null) return true; // Preserve Raven's own error handling.
            var selected = __1.Where(group => group != null && group.CountToTransfer > 0).ToList();
            if (!selected.Any(group => group.things.Any(item => IsStorageReference(group, item)))) return true;
            var state = new Submission { Map = map, Session = __0, Transfer = new StorageMaterialTransaction(map, IntVec3.Invalid) };
            __state = state;
            try
            {
                map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
                var provider = Activator.CreateInstance(traderType, new object[] { map });
                var valid = new HashSet<Thing>((IEnumerable<Thing>)colonyThings.Invoke(provider, new object[] { null }));
                var seen = new HashSet<Thing>();
                foreach (var group in selected)
                {
                    if (state.Originals.ContainsKey(group)) return Reject(__0, ref __result);
                    Tag(group, map);
                    state.Originals.Add(group, group.things.ToList());
                    int remaining = group.CountToTransfer;
                    foreach (Thing item in group.things)
                    {
                        if (remaining == 0) break;
                        if (item == null || item.Destroyed || !valid.Contains(item)) continue;
                        if (!seen.Add(item)) return Reject(__0, ref __result);
                        if (IsStorageReference(group, item))
                        {
                            if (!Stored(item, map, out var network)) continue;
                            int count = Math.Min(remaining, network.AvailableToWithdraw(item));
                            if (count <= 0) continue;
                            if (!state.Transfer.Add(item, count, network, network.Core)) return Reject(__0, ref __result);
                            remaining -= count;
                        }
                        else remaining -= Math.Min(remaining, item.stackCount);
                    }
                    if (remaining != 0) return Reject(__0, ref __result);
                }
                if ((state.Transfer.Requests.Count != 0 && !state.Transfer.TryStage()) || MapOf(__0) != map ||
                    state.Transfer.Requests.Any(request => !Eligible(request.Network, map)))
                    return Reject(__0, ref __result);
                var prepared = state.Transfer.Requests.ToDictionary(request => request.Item, request => request.Received);
                state.Staged.AddRange(prepared.Values);
                foreach (var entry in state.Originals)
                {
                    entry.Key.things.Clear();
                    foreach (Thing item in entry.Value)
                    {
                        if (prepared.TryGetValue(item, out Thing received)) entry.Key.things.Add(received);
                        else if (!IsStorageReference(entry.Key, item)) entry.Key.things.Add(item);
                    }
                }
                active = state;
                return true;
            }
            catch (Exception error)
            {
                Log.Error("[MagicStorage] Raven gift preparation failed: " + error);
                return Reject(__0, ref __result);
            }
        }

        private static Exception EndSubmit(Submission __state, Exception __exception)
        {
            try { __state?.Dispose(); }
            catch (Exception error) { Log.Error("[MagicStorage] Raven gift cleanup failed: " + error); }
            return __exception;
        }
    }
}
