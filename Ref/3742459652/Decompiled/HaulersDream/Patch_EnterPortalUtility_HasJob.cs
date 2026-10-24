using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(EnterPortalUtility), "HasJobOnPortal")]
public static class Patch_EnterPortalUtility_HasJob
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadPortal ?? true;
	}

	private static bool Prefix(Pawn pawn, MapPortal portal, ref bool __result)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.enableBulkLoadPortal)
		{
			return true;
		}
		MapPortalBulkTarget mapPortalBulkTarget = MapPortalBulkTarget.TryCreate(portal);
		if (mapPortalBulkTarget == null)
		{
			return true;
		}
		if (!TransportLoad.HasPotentialBulkWorkPortal(pawn, mapPortalBulkTarget))
		{
			HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
			if (instance != null && instance.LoadFullyClaimedByOthers(mapPortalBulkTarget))
			{
				__result = false;
				return false;
			}
			return true;
		}
		if (TransportLoad.TryGiveBulkJob(pawn, mapPortalBulkTarget) == null)
		{
			return true;
		}
		__result = true;
		return false;
	}
}
