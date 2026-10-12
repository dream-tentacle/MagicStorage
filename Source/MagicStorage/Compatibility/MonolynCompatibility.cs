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
            try
            {
                var manager = map.GetComponent<MapComponent_StorageNetworks>();
                foreach (var cost in buildingDef.costList)
                {
                    int remaining = cost.count;
                    if (remaining <= 0) continue;
                    var materials = map.listerThings.ThingsOfDef(cost.thingDef)
                        .Where(item => !item.Destroyed && item.Spawned).ToList();
                    ConsumeStacks(map, materials, ref remaining);
                    if (manager != null && remaining > 0)
                    {
                        foreach (var unit in manager.StorageUnits)
                        {
                            if (remaining <= 0) break;
                            if (!CanConsumeFrom(unit, map) || unit.Inventory.GetTotalCount(cost.thingDef) == 0) continue;
                            materials.Clear();
                            unit.Inventory.GetStacks(cost.thingDef, materials);
                            ConsumeStacks(map, materials, ref remaining);
                        }
                    }
                    if (remaining > 0)
                    {
                        Log.Warning("Not enough " + cost.thingDef.defName + " to consume.");
                        return false;
                    }
                }
                return true;
            }
            finally { map.resourceCounter.UpdateResourceCounts(); }
        }

        private static void ConsumeStacks(Map map, List<Thing> materials, ref int remaining)
        {
            foreach (Thing item in materials)
            {
                if (remaining <= 0) break;
                if (item.Destroyed || item.stackCount <= 0) continue;
                var unit = item.holdingOwner?.Owner as Building_StorageUnit;
                if (unit == null ? !item.Spawned || item.Map != map :
                    !CanConsumeFrom(unit, map) || item.holdingOwner != unit.Inventory.Contents) continue;
                int amount = Math.Min(item.stackCount, remaining);
                ShowTransferEffect(unit ?? item);
                try
                {
                    Thing consumed = unit == null ? item.SplitOff(amount) : unit.Inventory.Contents.Take(item, amount);
                    if (consumed == null) continue;
                    remaining -= consumed.stackCount;
                    consumed.Destroy(DestroyMode.Vanish);
                }
                // Partial Take needs a count update; whole removal already updates the index.
                finally { unit?.Inventory.RefreshStack(item); }
            }
        }

        // ResourceCounter includes physical player-owned stock even without a working network.
        private static bool CanConsumeFrom(Building_StorageUnit unit, Map map) =>
            unit.Spawned && !unit.Destroyed && unit.Map == map && unit.Faction == Faction.OfPlayer;

        private static void ShowTransferEffect(Thing source)
        {
            var def = lanternTransfer?.GetValue(null) as EffecterDef;
            if (def == null) return;
            Effecter effect = def.Spawn();
            var target = new TargetInfo(source.Position, source.Map);
            try { effect.Trigger(target, target, -1); }
            finally { effect.Cleanup(); }
        }
    }
}
