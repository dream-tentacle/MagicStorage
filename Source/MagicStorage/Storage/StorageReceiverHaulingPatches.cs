using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageReceiverHaulingPatches
    {
        static StorageReceiverHaulingPatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.receivers");
            harmony.Patch(AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell)),
                prefix: new HarmonyMethod(typeof(StorageReceiverHaulingPatches), nameof(CheckCapacity)));
        }

        // The native destination search checks settings, but does not call the parent's Accepts.
        private static bool CheckCapacity(IntVec3 c, Map map, Thing t, ref bool __result)
        {
            if (!(map.haulDestinationManager.SlotGroupParentAt(c) is Building_StorageReceiver receiver) ||
                receiver.Accepts(t)) return true;
            __result = false;
            return false;
        }

    }
}
