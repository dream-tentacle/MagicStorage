using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal sealed class NivarianCannonCompatibility : IStorageCompatibility
    {
        private static readonly ConditionalWeakTable<Map, HashSet<Building_NivarianCannonExtractor>> ports =
            new ConditionalWeakTable<Map, HashSet<Building_NivarianCannonExtractor>>();
        private static NivarianCannonApi api;
        private static bool reportedFailure;
        private static bool reportedSnapshotFailure;
        internal static bool Ready => api != null;
        public string PackageId => "keeptpa.NivarianRace";

        public void Apply(Harmony harmony)
        {
            var resolved = new NivarianCannonApi();
            harmony.Patch(resolved.Dispatch, prefix: new HarmonyMethod(typeof(NivarianCannonCompatibility), nameof(DispatchPrefix)));
            harmony.Patch(resolved.CaptureSnapshot, postfix: new HarmonyMethod(typeof(NivarianCannonCompatibility), nameof(CaptureSnapshotPostfix)));
            api = resolved;
        }

        // Bills must see deliverable inventory before they choose a recipe and create demands.
        // Use the native snapshot entries so ingredient filters, radii and cheapest-def selection
        // keep their original behavior. Capturing stock does not reserve or move any items.
        private static void CaptureSnapshotPostfix(Map __0, Thing __1, object __result)
        {
            if (api == null || __0 == null || __result == null || __1 == null || !__1.Spawned ||
                __1.Destroyed || __1.Map != __0 || __1.Faction != Faction.OfPlayer ||
                !ports.TryGetValue(__0, out var set) || set.Count == 0) return;
            try
            {
                var manager = __0.GetComponent<MapComponent_StorageNetworks>();
                manager.EnsureCurrent();
                object network = api.NetworkFor(__0);
                if (network == null) return;
                object destination = api.Covering(network, __1);
                if (destination == null) return;
                var sources = new HashSet<StorageNetwork>();
                foreach (var port in set)
                {
                    if (!port.CanWork || port.Map != __0 || sources.Contains(port.Network)) continue;
                    foreach (object source in api.Adjacent(port))
                    {
                        Thing parent = api.Parent(source);
                        if (!api.Operational(source) || !parent.Spawned || parent.Destroyed || parent.Map != __0 ||
                            parent.Faction != Faction.OfPlayer || api.Hops(network, source, destination) == int.MaxValue) continue;
                        sources.Add(port.Network);
                        break;
                    }
                }
                var stacks = new List<Thing>();
                foreach (StorageNetwork source in sources)
                {
                    stacks.Clear(); source.GetAllStacks(stacks);
                    foreach (Thing item in stacks)
                    {
                        int count = source.AvailableToWithdraw(item);
                        if (count <= 0 || !(item.holdingOwner?.Owner is Building_StorageUnit unit) ||
                            !unit.Spawned || unit.Destroyed || unit.Map != __0 || unit.Faction != Faction.OfPlayer) continue;
                        api.AddSnapshotStock(__result, item.def, count, unit.Position.DistanceToSquared(__1.Position));
                    }
                }
            }
            catch (Exception exception)
            {
                if (reportedSnapshotFailure) return;
                reportedSnapshotFailure = true;
                Log.Error("[MagicStorage] Nivarian bill inventory snapshot failed: " + exception);
            }
        }
        internal static void Register(Building_NivarianCannonExtractor port)
        { if (port.Map != null) ports.GetOrCreateValue(port.Map).Add(port); }
        internal static void Unregister(Building_NivarianCannonExtractor port)
        { if (port.Map != null && ports.TryGetValue(port.Map, out var set)) set.Remove(port); }
        internal static bool HasAdjacentCannon(Building_NivarianCannonExtractor port)
        {
            if (api == null || !port.Spawned) return false;
            foreach (object node in api.Adjacent(port))
                if (api.Operational(node) && api.Parent(node).Faction == port.Faction) return true;
            return false;
        }

        // Keep the original ground-intake path for any unmet remainder.
        private static bool DispatchPrefix(object __0, object __1, Map __2, object __3, ref int __4)
        {
            if (__4 <= 0 || api == null || __0 == null || __1 == null || __2 == null ||
                !ports.TryGetValue(__2, out var set) || set.Count == 0) return true;
            try { Dispatch(__0, __1, __2, __3, set, ref __4); }
            catch (Exception exception)
            {
                if (!reportedFailure)
                {
                    reportedFailure = true;
                    Log.Error("[MagicStorage] Nivarian cannon extraction failed; ground intake remains available: " + exception);
                }
            }
            return __4 > 0;
        }

        private sealed class Candidate
        {
            internal Building_NivarianCannonExtractor Port;
            internal object Node;
            internal int Hops, Load;
        }

        private static void Dispatch(object ledger, object cannonNetwork, Map map, object entry,
            HashSet<Building_NivarianCannonExtractor> set, ref int deficit)
        {
            var demand = api.ReadDemand(entry);
            if (demand.Def == null || demand.Target == null || !demand.Target.Spawned || demand.Target.Destroyed ||
                demand.Target.Map != map || demand.Target.Faction != Faction.OfPlayer) return;
            object destination = api.Covering(cannonNetwork, demand.Target);
            if (destination == null) return;
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            manager.EnsureCurrent();
            var candidates = new List<Candidate>();
            foreach (var port in set)
            {
                if (!port.CanWork || port.Map != map || port.Network.GetTotalCount(demand.Def) <= 0) continue;
                foreach (object source in api.Adjacent(port))
                {
                    Thing parent = api.Parent(source);
                    if (!api.Operational(source) || !parent.Spawned || parent.Map != map ||
                        parent.Faction != Faction.OfPlayer || api.Capacity(source) <= 0) continue;
                    int distance = api.Hops(cannonNetwork, source, destination);
                    if (distance != int.MaxValue)
                        candidates.Add(new Candidate { Port = port, Node = source, Hops = distance, Load = api.Load(cannonNetwork, source) });
                }
            }
            candidates.Sort((a, b) =>
            {
                int result = a.Hops.CompareTo(b.Hops);
                if (result == 0) result = a.Load.CompareTo(b.Load);
                if (result == 0) result = api.Parent(a.Node).thingIDNumber.CompareTo(api.Parent(b.Node).thingIDNumber);
                return result != 0 ? result : a.Port.thingIDNumber.CompareTo(b.Port.thingIDNumber);
            });
            if (candidates.Count == 0) return;
            int group = api.Group(ledger);
            foreach (var candidate in candidates)
            {
                if (deficit <= 0) break;
                Supply(candidate, demand, ledger, cannonNetwork, destination, group, ref deficit);
            }
        }

        private static void Supply(Candidate candidate, NivarianCannonApi.Demand demand, object ledger,
            object cannonNetwork, object destination, int group, ref int deficit)
        {
            var port = candidate.Port;
            StorageNetwork network = port.Network;
            var stacks = new List<Thing>(); network.GetStacks(demand.Def, stacks);
            foreach (Thing item in stacks)
            {
                if (deficit <= 0 || !port.CanWork || port.Network != network || !api.Operational(candidate.Node) ||
                    api.Hops(cannonNetwork, candidate.Node, destination) == int.MaxValue) break;
                int count = Math.Min(deficit, Math.Min(api.AvailableDemand(ledger, demand), Math.Min(
                    network.AvailableToWithdraw(item), Math.Min(api.Capacity(candidate.Node), api.ShotLimit(candidate.Node)))));
                if (count <= 0) continue;
                using (var transfer = new StorageMaterialTransaction(port.Map, port.Position))
                {
                    if (!transfer.Add(item, count, network, port) || !transfer.TryStage()) continue;
                    var request = transfer.Requests[0];
                    int id = 0;
                    object line = null;
                    try
                    {
                        if (!port.CanWork || port.Network != network || !api.Operational(candidate.Node) ||
                            api.Capacity(candidate.Node) < count || api.AvailableDemand(ledger, demand) < count ||
                            api.Hops(cannonNetwork, candidate.Node, destination) == int.MaxValue) continue;
                        id = api.PlaceOrder(ledger, demand, count, group, candidate.Node, out line);
                        if (id <= 0) continue;
                        transfer.Deliver(request, received => api.Enqueue(candidate.Node, received, demand, id, group));
                    }
                    finally
                    {
                        // An exception/false return after actual acceptance must not cancel
                        // that order or send the same demand through ground intake again.
                        if (request.Delivered)
                        {
                            deficit -= request.DeliveredCount;
                            if (request.DeliveredCount != count) api.ResizeOrderLine(ledger, line, request.DeliveredCount);
                        }
                        else if (id > 0) api.Cancel(ledger, id);
                    }
                }
            }
        }
    }
}
