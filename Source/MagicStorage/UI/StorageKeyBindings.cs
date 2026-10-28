using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    [DefOf]
    public static class StorageKeyBindingDefOf
    {
        public static KeyBindingDef MS_JumpToStorageCore;
        public static KeyBindingDef MS_ViewItems;
        public static KeyBindingDef MS_ProductionOrders;
        public static KeyBindingDef MS_ShelfCopyPreset;
        public static KeyBindingDef MS_ShelfPastePreset;
        public static KeyBindingDef MS_ShelfLocalPresets;

        static StorageKeyBindingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(StorageKeyBindingDefOf));
        }
    }

    [StaticConstructorOnStartup]
    internal static class StorageKeyBindings
    {
        static StorageKeyBindings()
        {
            new Harmony("mjcg.magicstorage.keybindings").Patch(
                AccessTools.Method(typeof(MapInterface), nameof(MapInterface.HandleLowPriorityInput)),
                postfix: new HarmonyMethod(typeof(StorageKeyBindings), nameof(HandleInput)));
        }

        private static void HandleInput()
        {
            Map map = Find.CurrentMap;
            if (map == null || !WorldRendererUtility.DrawingMap || Event.current == null ||
                !Find.WindowStack.GetsInput(null) || !StorageKeyBindingDefOf.MS_JumpToStorageCore.KeyDownEvent)
                return;

            Building_StorageCore selected = null;
            foreach (object thing in Find.Selector.SelectedObjectsListForReading)
                if (thing is Building_StorageCore core && core.Spawned && core.Map == map && core.Faction == Faction.OfPlayer)
                {
                    selected = core;
                    break;
                }

            Building_StorageCore next = map.GetComponent<MapComponent_CosmicCrafting>().NextPlayerCore(selected);
            if (next != null) CameraJumper.TryJumpAndSelect(next);
            Event.current.Use();
        }
    }
}
