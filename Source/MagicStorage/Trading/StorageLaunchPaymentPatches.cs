using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
        [ThreadStatic] private static SupplyScope active;
        private static bool sourcePatched;

        private sealed class SupplyScope
        {
            internal SupplyScope Previous;
            internal StorageMaterialTransaction Transfer;
        }

        static StorageLaunchPaymentPatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.trading.payment");
            harmony.Patch(AccessTools.Method(typeof(TradeUtility), nameof(TradeUtility.LaunchThingsOfType)),
                prefix: new HarmonyMethod(typeof(StorageLaunchPaymentPatches), nameof(BeginSupply)),
                transpiler: new HarmonyMethod(typeof(StorageLaunchPaymentPatches), nameof(IncludeStoredSource)),
                finalizer: new HarmonyMethod(typeof(StorageLaunchPaymentPatches), nameof(EndSupply)));
            harmony.Patch(AccessTools.Method(typeof(TradeUtility), nameof(TradeUtility.AllLaunchableThingsForTrade)),
                postfix: new HarmonyMethod(typeof(StorageLaunchPaymentPatches), nameof(LaunchableThings)));
        }

        private static void LaunchableThings(Map map, ITrader trader, ref IEnumerable<Thing> __result)
        {
            if (sourcePatched && map != null && trader == null) __result = WithStorage(__result, map);
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
                if (building is Building_StorageCore core && StorageTradeSession.PaymentCoreEligible(core, map) &&
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

        internal static IEnumerable<CodeInstruction> IncludeStoredSource(IEnumerable<CodeInstruction> instructions,
            MethodBase __originalMethod)
        {
            var code = new List<CodeInstruction>(instructions);
            var locals = __originalMethod.GetMethodBody().LocalVariables;
            int match = -1, local = -1, matches = 0;
            for (int i = 0; i + 1 < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Ldnull || !code[i + 1].IsStloc()) continue;
                int candidate = code[i + 1].LocalIndex();
                if (candidate >= locals.Count || locals[candidate].LocalType != typeof(Thing)) continue;
                // The selected stack is initialized to null each iteration. Its first
                // read tests whether the existing source search found anything.
                int read = code.FindIndex(i + 2, c => c.IsLdloc() && c.LocalIndex() == candidate);
                if (read < 0) continue;
                int next = read + 1;
                while (next < code.Count && code[next].opcode == OpCodes.Nop) next++;
                if (next == code.Count || (code[next].opcode != OpCodes.Ldnull &&
                    code[next].opcode != OpCodes.Brtrue && code[next].opcode != OpCodes.Brtrue_S)) continue;
                match = read; local = candidate; matches++;
            }
            sourcePatched = matches == 1;
            if (!sourcePatched)
            {
                Log.Error("[MagicStorage] Launch payment source search did not match exactly once; stored payment queries are disabled.");
                return code;
            }

            // Keep the original load, including its labels/exception boundaries.
            // Replace only its value and write it back for the native deduction loop.
            code.InsertRange(match + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2), new CodeInstruction(OpCodes.Ldarg_3),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StorageLaunchPaymentPatches), nameof(StoredSource))),
                new CodeInstruction(OpCodes.Dup), CodeInstruction.StoreLocal(local)
            });
            return code;
        }

        private static void BeginSupply(out SupplyScope __state)
        {
            __state = new SupplyScope { Previous = active };
            active = __state;
        }

        private static Thing StoredSource(Thing original, ThingDef resDef, int debt, Map map, TradeShip trader)
        {
            if (original != null || trader != null || map == null || resDef == null || debt <= 0 || active == null)
                return original;
            active.Transfer?.Dispose();
            active.Transfer = null;
            foreach (StorageNetwork network in Networks(map))
                foreach (Thing item in Stocks(network, resDef))
                {
                    var transfer = new StorageMaterialTransaction(map, network.Core.Position);
                    active.Transfer = transfer;
                    if (transfer.Add(item, Math.Min(debt, item.stackCount), network, network.Core) &&
                        transfer.TryStage() && StorageTradeSession.PaymentCoreEligible(network.Core, map))
                        return transfer.Requests[0].Received;
                    transfer.Dispose();
                    active.Transfer = null;
                }
            return null;
        }

        private static void EndSupply(SupplyScope __state)
        {
            // Nested launches get their own extraction scope. Only our unconsumed
            // staged items are returned; existing payment side effects are untouched.
            if (__state == null) return;
            try { __state.Transfer?.Dispose(); }
            finally { active = __state.Previous; }
        }
    }
}
