using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageTradePatches
    {
        static StorageTradePatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.trading");
            harmony.Patch(AccessTools.Method(typeof(TradeDeal), "AddAllTradeables"),
                prefix: new HarmonyMethod(typeof(StorageTradePatches), nameof(FillInventory)));
            harmony.Patch(AccessTools.Method(typeof(TradeDeal), nameof(TradeDeal.TryExecute)),
                prefix: new HarmonyMethod(typeof(StorageTradePatches), nameof(Prepare)),
                finalizer: new HarmonyMethod(typeof(StorageTradePatches), nameof(Finish)));
        }

        private static bool FillInventory(TradeDeal __instance)
        {
            if (!StorageTradeSession.TryGet(__instance, out var session)) return true;
            session.FillInventory();
            return false;
        }

        private static bool Prepare(TradeDeal __instance, ref bool actuallyTraded, ref bool __result,
            out StorageTradeTransaction __state)
        {
            __state = null;
            if (!StorageTradeSession.TryGet(__instance, out var session)) return true;
            if (session.Prepare(out __state)) return true;
            actuallyTraded = false; __result = false;
            return false;
        }

        private static Exception Finish(TradeDeal __instance, StorageTradeTransaction __state, Exception __exception)
        {
            if (StorageTradeSession.TryGet(__instance, out var session)) session.Finish(__state);
            else __state?.Dispose();
            return __exception;
        }
    }
}
