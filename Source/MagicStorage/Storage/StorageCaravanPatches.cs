using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageCaravanPatches
    {
        private static readonly System.Reflection.MethodInfo checkForErrors = AccessTools.Method(typeof(Dialog_FormCaravan), "CheckForErrors");

        static StorageCaravanPatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.caravans");
            harmony.Patch(AccessTools.Method(typeof(WorldGizmoUtility), nameof(WorldGizmoUtility.WorldUIOnGUI)),
                transpiler: new HarmonyMethod(typeof(StorageCaravanWorldGizmos), nameof(StorageCaravanWorldGizmos.InsertCommands)));
            harmony.Patch(AccessTools.Method(typeof(Dialog_FormCaravan), "AddItemsToTransferables"),
                prefix: new HarmonyMethod(typeof(StorageCaravanPatches), nameof(FillItems)));
            harmony.Patch(checkForErrors, prefix: new HarmonyMethod(typeof(StorageCaravanPatches), nameof(Validate)));
            harmony.Patch(AccessTools.Method(typeof(Dialog_FormCaravan), "TryFormAndSendCaravan"),
                prefix: new HarmonyMethod(typeof(StorageCaravanPatches), nameof(Send)));
            // The native developer button must not bypass network withdrawals and reservations.
            harmony.Patch(AccessTools.Method(typeof(Dialog_FormCaravan), "DebugTryFormCaravanInstantly"),
                prefix: new HarmonyMethod(typeof(StorageCaravanPatches), nameof(Send)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(TransferableOneWay), nameof(TransferableOneWay.MaxCount)),
                prefix: new HarmonyMethod(typeof(StorageCaravanPatches), nameof(AvailableCount)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(Dialog_FormCaravan), "IgnoreInventoryMode"),
                prefix: new HarmonyMethod(typeof(StorageCaravanPatches), nameof(InventoryMode)));
        }

        private static bool FillItems(Dialog_FormCaravan __instance)
        {
            if (!(__instance is Dialog_StorageCaravan dialog)) return true;
            dialog.FillNetworkItems();
            return false;
        }

        private static bool Validate(Dialog_FormCaravan __instance, List<Pawn> pawns, ref bool __result)
        {
            if (!(__instance is Dialog_StorageCaravan dialog) || dialog.ValidateSelection(pawns)) return true;
            __result = false;
            return false;
        }

        private static bool Send(Dialog_FormCaravan __instance, PlanetTile ___startingTile,
            PlanetTile ___destinationTile, ref bool __result)
        {
            if (!(__instance is Dialog_StorageCaravan dialog)) return true;
            List<Pawn> pawns = TransferableUtility.GetPawnsFromTransferables(dialog.transferables);
            __result = (bool)checkForErrors.Invoke(dialog, new object[] { pawns }) &&
                StorageCaravanTransfer.Send(dialog, pawns, ___startingTile, ___destinationTile);
            return false;
        }

        private static bool AvailableCount(TransferableOneWay __instance, ref int __result)
        {
            if (!(__instance is Transferable_StorageCaravan row)) return true;
            __result = row.AvailableCount;
            return false;
        }

        private static bool InventoryMode(Dialog_FormCaravan __instance, ref IgnorePawnsInventoryMode __result)
        {
            if (!(__instance is Dialog_StorageCaravan)) return true;
            // Instant departure retains backpacks, even when they were assigned to unload.
            __result = IgnorePawnsInventoryMode.DontIgnore;
            return false;
        }
    }
}
