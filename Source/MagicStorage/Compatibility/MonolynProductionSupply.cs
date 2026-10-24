using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Optional integration: the producer keeps ownership of ingredients until its
    // original Finish/Cancel logic consumes or returns them.
    internal static class MonolynProductionSupply
    {
        private static bool reportedFailure;

        internal static void Apply(Harmony harmony)
        {
            Type producer = AccessTools.TypeByName("ASEL.Building_MonolynProducer");
            var tick = producer == null ? null : AccessTools.DeclaredMethod(producer, "Tick", Type.EmptyTypes);
            if (producer == null || !typeof(Thing).IsAssignableFrom(producer) || tick == null ||
                tick.ReturnType != typeof(void) ||
                AccessTools.Field(producer, "working")?.FieldType != typeof(bool) ||
                AccessTools.Field(producer, "pause")?.FieldType != typeof(bool) ||
                AccessTools.Field(producer, "requiredCost")?.FieldType != typeof(List<ThingDefCountClass>) ||
                AccessTools.Field(producer, "innerContainer")?.FieldType != typeof(ThingOwner))
                throw new InvalidOperationException("Monolyn production material API was not found.");

            harmony.Patch(tick, prefix: new HarmonyMethod(typeof(MonolynProductionSupply), nameof(TickPrefix)));
        }

        private static void TickPrefix(Thing __instance, bool ___working, bool ___pause,
            List<ThingDefCountClass> ___requiredCost, ThingOwner ___innerContainer)
        {
            if (!___working || ___pause || !__instance.IsHashIntervalTick(300)) return;
            try { Supply(__instance, ___requiredCost, ___innerContainer); }
            catch (Exception exception)
            {
                // Keep the original producer and pawn delivery path running on failure.
                if (reportedFailure) return;
                reportedFailure = true;
                Log.Error("[MagicStorage] Monolyn production supply failed: " + exception);
            }
        }

        private static bool CanSupply(Thing producer) =>
            producer != null && producer.Spawned && !producer.Destroyed && producer.Map != null &&
            producer.Faction == Faction.OfPlayer && !producer.IsForbidden(Faction.OfPlayer) &&
            !producer.Position.Fogged(producer.Map) &&
            !producer.Map.reservationManager.IsReservedByAnyoneOf(producer, Faction.OfPlayer) &&
            !producer.Map.physicalInteractionReservationManager.IsReserved(producer);

        internal static void Supply(Thing producer, List<ThingDefCountClass> costs, ThingOwner container)
        {
            if (costs == null || costs.Count == 0 || container == null ||
                !ReferenceEquals(container.Owner, producer) || !CanSupply(producer)) return;
            Map map = producer.Map;
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            if (manager == null) return;
            manager.EnsureCurrent();
            bool changed = false;
            try
            {
                // A delivery removes completed entries; never enumerate the live list.
                foreach (var cost in new List<ThingDefCountClass>(costs))
                {
                    if (cost?.thingDef == null || cost.count <= 0 || !costs.Contains(cost)) continue;
                    using (var transfer = new StorageMaterialTransaction(map, producer.Position))
                    {
                        Plan(manager, map, cost, transfer);
                        if (transfer.Requests.Count == 0 || !transfer.TryStage()) continue;
                        foreach (var request in transfer.Requests)
                        {
                            try
                            {
                                transfer.Deliver(request, item =>
                                {
                                    if (!CanSupply(producer) || producer.Map != map || !costs.Contains(cost) || cost.count <= 0)
                                        return false;
                                    return StorageTransfer.Move(item.holdingOwner, container, item,
                                        Math.Min(request.Count, cost.count), map, producer.Position) > 0;
                                });
                            }
                            finally
                            {
                                // Receipts survive a callback throwing after acceptance.
                                // Only actual ownership transfer reduces the missing cost.
                                int received = request.DeliveredCount;
                                if (received > 0)
                                {
                                    cost.count = Math.Max(0, cost.count - received);
                                    if (cost.count == 0) costs.Remove(cost);
                                    changed = true;
                                }
                            }
                        }
                    }
                }
            }
            finally { if (changed) map.resourceCounter.UpdateResourceCounts(); }
        }

        private static void Plan(MapComponent_StorageNetworks manager, Map map, ThingDefCountClass cost,
            StorageMaterialTransaction transfer)
        {
            int remaining = cost.count;
            var stacks = new List<Thing>();
            foreach (var unit in manager.StorageUnits)
            {
                if (remaining == 0) break;
                if (!unit.Spawned || unit.Destroyed || unit.Map != map || unit.Faction != Faction.OfPlayer ||
                    unit.Position.Fogged(map) || unit.Network?.CanWork != true) continue;
                stacks.Clear();
                unit.Inventory.GetStacks(cost.thingDef, stacks);
                foreach (Thing item in stacks)
                {
                    int count = Math.Min(remaining, unit.Network.AvailableToWithdraw(item));
                    if (count > 0 && transfer.Add(item, count, unit.Network)) remaining -= count;
                    if (remaining == 0) break;
                }
            }
        }
    }
}
