using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageSelectionPatches
    {
        static StorageSelectionPatches()
        {
            new Harmony("mjcg.magicstorage.selection").Patch(
                AccessTools.Method(typeof(ContainingSelectionUtility), nameof(ContainingSelectionUtility.SelectableContainedThings)),
                prefix: new HarmonyMethod(typeof(StorageSelectionPatches), nameof(SkipUnselectableContents)));
        }

        private static bool SkipUnselectableContents(Thing container, ref IEnumerable<Thing> __result)
        {
            if (!(container is Building_StorageUnit || container is Building_StorageCore ||
                  container is Building_TransportSupplier || container is Building_StorageApparelAdapter) ||
                container.def.containedItemsSelectable || container.def.containedPawnsSelectable) return true;

            // Vanilla would reject every item after walking the container. Return the same
            // empty result before creating or enumerating its iterator instead.
            __result = Array.Empty<Thing>();
            return false;
        }
    }
}
