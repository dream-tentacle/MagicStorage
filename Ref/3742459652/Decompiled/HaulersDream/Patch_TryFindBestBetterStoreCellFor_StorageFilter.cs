using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(StoreUtility), "TryFindBestBetterStoreCellFor")]
public static class Patch_TryFindBestBetterStoreCellFor_StorageFilter
{
	private static void Postfix(Map map, ref IntVec3 foundCell, ref bool __result)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		if (StorageBuildingFilter.Enabled && StorageBuildingFilter.AnyStorageModPresent && __result && StorageBuildingFilter.CurrentContext != StorageFilterContext.Unload)
		{
			StorageBuildingFilter storageBuildingFilter = HaulersDreamMod.Settings?.storageBuildingFilter;
			if (storageBuildingFilter != null && !storageBuildingFilter.IsCellAllowed(foundCell, map))
			{
				__result = false;
				foundCell = IntVec3.Invalid;
			}
		}
	}
}
