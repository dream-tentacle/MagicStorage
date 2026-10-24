using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(StoreUtility), "TryFindBestBetterStorageFor")]
public static class Patch_TryFindBestBetterStorageFor_StorageFilter
{
	private static void Postfix(Map map, ref IntVec3 foundCell, ref IHaulDestination haulDestination, ref bool __result)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		if (StorageBuildingFilter.Enabled && StorageBuildingFilter.AnyStorageModPresent && __result && StorageBuildingFilter.CurrentContext != StorageFilterContext.Unload)
		{
			StorageBuildingFilter storageBuildingFilter = HaulersDreamMod.Settings?.storageBuildingFilter;
			if (storageBuildingFilter != null && !(((IntVec3)(ref foundCell)).IsValid ? storageBuildingFilter.IsCellAllowed(foundCell, map) : storageBuildingFilter.IsHaulDestinationAllowed(haulDestination)))
			{
				__result = false;
				foundCell = IntVec3.Invalid;
				haulDestination = null;
			}
		}
	}
}
