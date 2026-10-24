using HarmonyLib;
using RimWorld;

namespace HaulersDream;

[HarmonyPatch(typeof(MapPortal), "get_AnyPawnCanLoadAnythingNow")]
public static class Patch_MapPortal_AnyPawnCanLoadAnythingNow
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadPortal ?? true;
	}

	private static bool Prefix(MapPortal __instance, ref bool __result)
	{
		if (!BulkLoadAntiConflict.AnyHdLoaderForPortal(__instance))
		{
			HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
			if (instance == null || !instance.LoadAnyClaimsInProgress(MapPortalBulkTarget.LedgerKey(__instance)))
			{
				return true;
			}
		}
		__result = true;
		return false;
	}
}
