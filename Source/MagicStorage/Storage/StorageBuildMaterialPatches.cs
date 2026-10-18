using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageBuildMaterialPatches
    {
        static StorageBuildMaterialPatches()
        {
            new Harmony("mjcg.magicstorage.buildmaterials").Patch(
                AccessTools.Method(typeof(Designator_Build), nameof(Designator_Build.ProcessInput)),
                transpiler: new HarmonyMethod(typeof(StorageBuildMaterialPatches), nameof(IncludeStoredMaterials)));
        }

        internal static IEnumerable<CodeInstruction> IncludeStoredMaterials(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            MethodInfo query = AccessTools.Method(typeof(ListerThings), nameof(ListerThings.ThingsOfDef),
                new[] { typeof(ThingDef) });
            MethodInfo count = AccessTools.PropertyGetter(typeof(List<Thing>), nameof(List<Thing>.Count));
            int match = -1, matches = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (!code[i].Calls(query)) continue;
                match = i;
                matches++;
            }
            int next = match + 1;
            while (next < code.Count && code[next].opcode == OpCodes.Nop) next++;
            if (matches != 1 || next >= code.Count || !code[next].Calls(count))
            {
                Log.Error("[MagicStorage] Build material query did not match exactly one ThingsOfDef().Count call; leaving ProcessInput unchanged.");
                return code;
            }

            // Same stack signature: the instance receiver becomes the first static argument.
            // Keep every existing label and exception boundary on the original instruction.
            code[match].opcode = OpCodes.Call;
            code[match].operand = AccessTools.Method(typeof(StorageBuildMaterialPatches), nameof(ThingsOfDefForBuild));
            return code;
        }

        private static List<Thing> ThingsOfDefForBuild(ListerThings lister, ThingDef def)
        {
            List<Thing> ground = lister.ThingsOfDef(def);
            if (ground.Count > 0) return ground;

            // Designator.Map also resolves Find.CurrentMap. This replacement is confined
            // to ProcessInput; ordinary map queries must never expose container contents.
            Map map = Find.CurrentMap;
            if (map == null || !ReferenceEquals(lister, map.listerThings)) return ground;
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            if (manager == null) return ground;
            foreach (Building_StorageUnit unit in manager.StorageUnits)
            {
                // Match stored-resource ownership and visibility, without requiring power,
                // a working network, unreserved stock, or enough material for construction.
                if (!unit.Spawned || unit.Destroyed || unit.Map != map || unit.Faction != Faction.OfPlayer ||
                    unit.Position.Fogged(map) || unit.Inventory.GetTotalCount(def) <= 0) continue;
                Thing stored = unit.Inventory.StackAt(def, 0);
                if (stored == null || stored.Destroyed || stored.stackCount <= 0 ||
                    stored.holdingOwner != unit.Inventory.Contents) continue;
                // The guarded call site only checks Count. One real stack establishes
                // presence without copying the inventory or altering the map's own list.
                return new List<Thing> { stored };
            }
            return ground;
        }
    }
}
