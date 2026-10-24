using HarmonyLib;
using RimWorld;

namespace HaulersDream;

[HarmonyPatch(typeof(CompTransporter), "get_AnyPawnCanLoadAnythingNow")]
public static class Patch_CompTransporter_AnyPawnCanLoadAnythingNow
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadTransporters ?? true;
	}

	private static bool Prefix(CompTransporter __instance, ref bool __result)
	{
		if (!BulkLoadAntiConflict.AnyHdLoaderForGroup(__instance))
		{
			HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
			if (instance == null || !instance.LoadAnyClaimsInProgress(__instance.groupID))
			{
				return true;
			}
		}
		__result = true;
		return false;
	}
}
