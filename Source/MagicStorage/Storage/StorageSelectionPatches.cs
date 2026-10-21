using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageSelectionPatches
    {
        static StorageSelectionPatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.selection");
            harmony.Patch(
                AccessTools.Method(typeof(ContainingSelectionUtility), nameof(ContainingSelectionUtility.SelectableContainedThings)),
                prefix: new HarmonyMethod(typeof(StorageSelectionPatches), nameof(SkipUnselectableContents)));
            harmony.Patch(AccessTools.Method(typeof(Selector), "SelectableObjectsUnderMouse"),
                postfix: new HarmonyMethod(typeof(StorageSelectionPatches), nameof(FilterUnderMouse)));
            harmony.Patch(AccessTools.Method(typeof(ThingSelectionUtility), nameof(ThingSelectionUtility.MultiSelectableThingsInScreenRectDistinct)),
                postfix: new HarmonyMethod(typeof(StorageSelectionPatches), nameof(FilterInRect)));
        }

        private static bool IsSupplyShelfItem(Thing thing) => thing != null && thing.Spawned &&
            thing.def.category == ThingCategory.Item &&
            thing.Map.haulDestinationManager.SlotGroupParentAt(thing.Position) is Building_StorageSupplyShelf;

        private static void FilterUnderMouse(ref IEnumerable<object> __result) =>
            __result = WithoutSupplyShelfItems(__result);

        private static IEnumerable<object> WithoutSupplyShelfItems(IEnumerable<object> candidates)
        {
            foreach (object candidate in candidates)
                if (!(candidate is Thing thing) || !IsSupplyShelfItem(thing)) yield return candidate;
        }

        private static void FilterInRect(ref IEnumerable<Thing> __result)
        {
            Event current = Event.current;
            if (current != null && current.rawType == EventType.MouseUp && current.button == 0)
                __result = WithoutSupplyShelfItemsInRect(__result);
        }

        // Only mouse selection is filtered. Explicit selection and other rectangle queries stay native.
        private static IEnumerable<Thing> WithoutSupplyShelfItemsInRect(IEnumerable<Thing> candidates)
        {
            foreach (Thing candidate in candidates)
                if (!IsSupplyShelfItem(candidate)) yield return candidate;
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
