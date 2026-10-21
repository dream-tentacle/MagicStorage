using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Optional integration: resolve Monolyn by name, without referencing ASEL.dll.
    internal sealed class MonolynCompatibility : IStorageCompatibility
    {
        private static FieldInfo lanternTransfer;
        public string PackageId => "ASEL.MonolynRace";

        public void Apply(Harmony harmony)
        {
            Type worker = AccessTools.TypeByName("ASEL.PlaceWorker_LightBuilder");
            MethodInfo destroy = worker == null ? null : AccessTools.Method(worker, "DestroyResources",
                new[] { typeof(Map), typeof(BuildableDef) });
            if (destroy == null || destroy.ReturnType != typeof(bool))
                throw new MissingMethodException("ASEL.PlaceWorker_LightBuilder", "DestroyResources(Map, BuildableDef)");

            Type effects = AccessTools.TypeByName("ASEL.MNEffecterDefOf");
            lanternTransfer = effects == null ? null : AccessTools.Field(effects, "LanternTransfer");
            harmony.Patch(destroy, prefix: new HarmonyMethod(typeof(MonolynCompatibility), nameof(DestroyResourcesPrefix)));
        }

        private static bool DestroyResourcesPrefix(Map __0, BuildableDef __1, ref bool __result)
        {
            __result = ConsumeResources(__0, __1 as ThingDef);
            return false;
        }

        internal static bool ConsumeResources(Map map, ThingDef buildingDef)
        {
            if (map == null || buildingDef == null) return false;
            if (buildingDef.costList == null) return true;
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            if (manager == null) return false;
            try
            {
                manager.EnsureCurrent();
                using (var transfer = new StorageMaterialTransaction(map, IntVec3.Invalid))
                {
                    if (!TryPlan(map, manager, buildingDef, transfer)) return false;
                    if (transfer.Requests.Count == 0) return true;
                    if (!transfer.TryStage() || !transfer.ConsumeAll()) return false;
                    foreach (var request in transfer.Requests)
                    {
                        // Cosmetic callbacks must never turn a paid operation into a failure.
                        try { ShowTransferEffect(new TargetInfo(request.Cell, map)); }
                        catch (Exception exception) { Log.Warning("[MagicStorage] Monolyn transfer effect failed: " + exception); }
                    }
                    return true;
                }
            }
            catch (Exception exception)
            {
                Log.Error("[MagicStorage] Monolyn resource transfer failed: " + exception);
                return false;
            }
            finally { map.resourceCounter.UpdateResourceCounts(); }
        }

        private static bool TryPlan(Map map, MapComponent_StorageNetworks manager, ThingDef buildingDef,
            StorageMaterialTransaction transfer)
        {
            // Aggregate repeated entries so one stack cannot pay for two costs twice.
            foreach (var cost in buildingDef.costList.Where(c => c.count > 0).GroupBy(c => c.thingDef))
            {
                if (cost.Key == null) return false;
                long remaining = cost.Sum(c => (long)c.count);
                var seen = new HashSet<Thing>();
                foreach (Thing item in map.listerThings.ThingsOfDef(cost.Key))
                {
                    if (remaining == 0) break;
                    if (!seen.Add(item) || !StorageMaterialTransaction.GroundAvailable(map, item)) continue;
                    int count = (int)Math.Min(remaining, item.stackCount);
                    if (!transfer.Add(item, count)) return false;
                    remaining -= count;
                }
                if (remaining > 0)
                {
                    var stacks = new List<Thing>();
                    foreach (var unit in manager.StorageUnits)
                    {
                        if (remaining == 0) break;
                        if (!CanConsumeFrom(unit, map)) continue;
                        stacks.Clear(); unit.Inventory.GetStacks(cost.Key, stacks);
                        foreach (Thing item in stacks)
                        {
                            if (remaining == 0) break;
                            if (!seen.Add(item)) continue;
                            int count = (int)Math.Min(remaining, unit.Network.AvailableToWithdraw(item));
                            if (count <= 0) continue;
                            if (!transfer.Add(item, count, unit.Network)) return false;
                            remaining -= count;
                        }
                    }
                }
                if (remaining > 0) return false;
            }
            return true;
        }

        private static bool CanConsumeFrom(Building_StorageUnit unit, Map map) =>
            unit.Spawned && !unit.Destroyed && unit.Map == map && unit.Faction == Faction.OfPlayer &&
            !unit.Position.Fogged(map) && unit.Network?.CanWork == true;

        private static void ShowTransferEffect(TargetInfo target)
        {
            var def = lanternTransfer?.GetValue(null) as EffecterDef;
            if (def == null) return;
            Effecter effect = def.Spawn();
            try { effect.Trigger(target, target, -1); }
            finally { effect.Cleanup(); }
        }
    }
}
