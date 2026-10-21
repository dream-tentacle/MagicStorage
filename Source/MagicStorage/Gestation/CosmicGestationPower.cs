using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // The cosmic gestator has no electrical components. Adapt only the two
    // electrical checks inside its native parent; keep native bills and visuals.
    [StaticConstructorOnStartup]
    internal static class CosmicGestationPower
    {
        static CosmicGestationPower()
        {
            var harmony = new Harmony("mjcg.magicstorage.gestation.power");
            harmony.Patch(AccessTools.PropertyGetter(typeof(Building_MechGestator), nameof(Building_MechGestator.PoweredOn)),
                prefix: new HarmonyMethod(typeof(CosmicGestationPower), nameof(PoweredOn)));
            harmony.Patch(AccessTools.Method(typeof(Building_MechGestator), "Tick"),
                transpiler: new HarmonyMethod(typeof(CosmicGestationPower), nameof(SkipElectricalUpdate)));
        }

        private static bool PoweredOn(Building_MechGestator __instance, ref bool __result)
        {
            if (!(__instance is Building_CosmicMechGestator table)) return true;
            __result = table.NetworkReady;
            return false;
        }

        internal static IEnumerable<CodeInstruction> SkipElectricalUpdate(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var interval = AccessTools.Method(typeof(Gen), nameof(Gen.IsHashIntervalTick),
                new[] { typeof(Thing), typeof(int) });
            var calls = code.Where(i => i.Calls(interval)).ToList();
            if (calls.Count != 1)
                throw new System.InvalidOperationException("Expected one native mech gestator electrical update interval.");
            // Same stack signature; retain existing labels and exception boundaries.
            calls[0].opcode = OpCodes.Call;
            calls[0].operand = AccessTools.Method(typeof(CosmicGestationPower), nameof(ShouldUpdatePower));
            return code;
        }

        private static bool ShouldUpdatePower(Thing thing, int interval) =>
            !(thing is Building_CosmicMechGestator) && thing.IsHashIntervalTick(interval);
    }
}
