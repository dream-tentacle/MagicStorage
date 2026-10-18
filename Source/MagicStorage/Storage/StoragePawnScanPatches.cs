using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StoragePawnScanPatches
    {
        [ThreadStatic]
        private static Map pawnQueryMap;
        private static readonly string storageNamespace = typeof(StoragePawnScanPatches).Namespace;
        private static readonly string storageNamespacePrefix = storageNamespace + ".";
        private static readonly Predicate<IThingHolder> includeHolder = IncludeHolder;

        private struct QueryState
        {
            internal bool Entered;
            internal Map PreviousMap;
        }

        static StoragePawnScanPatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.pawnscan");
            harmony.Patch(AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.AllPawnsUnspawned)),
                prefix: new HarmonyMethod(typeof(StoragePawnScanPatches), nameof(BeginQuery)),
                finalizer: new HarmonyMethod(typeof(StoragePawnScanPatches), nameof(EndQuery)));
            harmony.Patch(AccessTools.Method(typeof(Map), nameof(Map.GetChildHolders)),
                postfix: new HarmonyMethod(typeof(StoragePawnScanPatches), nameof(FilterHolders)));
            // A minified storage building can be below a vanilla holder rather than directly
            // in the map's list. Exclude the same containers before visiting descendants.
            harmony.Patch(AccessTools.Method(typeof(ThingOwnerUtility), "GetAllThingsRecursively", new[]
                { typeof(IThingHolder), typeof(List<Thing>), typeof(bool), typeof(Predicate<IThingHolder>) }),
                prefix: new HarmonyMethod(typeof(StoragePawnScanPatches), nameof(FilterDescendants)));
        }

        private static void BeginQuery(Map ___map, out QueryState __state)
        {
            __state = new QueryState { Entered = true, PreviousMap = pawnQueryMap };
            pawnQueryMap = ___map;
        }

        private static Exception EndQuery(QueryState __state, Exception __exception)
        {
            // A preceding patch may skip our prefix or throw before it runs. Restoring a
            // saved value is also safe if another finalizer makes cleanup run a second time.
            if (__state.Entered) pawnQueryMap = __state.PreviousMap;
            return __exception;
        }

        private static void FilterHolders(Map __instance, List<IThingHolder> outChildren)
        {
            if (pawnQueryMap == null || !ReferenceEquals(__instance, pawnQueryMap)) return;
            for (int i = outChildren.Count - 1; i >= 0; i--)
            {
                // All of this mod's containers are excluded, including nested pawns, as
                // requested. Physical inventory remains visible outside this pawn query.
                if (!IncludeHolder(outChildren[i])) outChildren.RemoveAt(i);
            }
        }

        private static bool IncludeHolder(IThingHolder holder)
        {
            string holderNamespace = holder?.GetType().Namespace;
            return holderNamespace != storageNamespace &&
                (holderNamespace == null || !holderNamespace.StartsWith(storageNamespacePrefix, StringComparison.Ordinal));
        }

        private static void FilterDescendants(ref Predicate<IThingHolder> passCheck)
        {
            if (pawnQueryMap == null) return;
            passCheck = passCheck == null ? includeHolder : Combine(passCheck);
        }

        private static Predicate<IThingHolder> Combine(Predicate<IThingHolder> existing)
            => holder => IncludeHolder(holder) && existing(holder);
    }
}
