using System;
using HarmonyLib;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageShelfTemperaturePatches
    {
        static StorageShelfTemperaturePatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.shelves.temperature");
            harmony.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.AmbientTemperature)),
                postfix: new HarmonyMethod(typeof(StorageShelfTemperaturePatches), nameof(AdjustItemTemperature)));
        }

        // Change the temperature read by the item; the room temperature stays untouched.
        private static void AdjustItemTemperature(Thing __instance, ref float __result)
        {
            if (!__instance.Spawned || __instance.def.category != ThingCategory.Item ||
                !(__instance.Map.haulDestinationManager.SlotGroupParentAt(__instance.Position)
                    is Building_StorageSupplyShelf shelf) || !shelf.LowTemperatureMode)
                return;

            if (__result > -10f) __result = Math.Max(__result - 100f, -10f);
        }
    }
}
