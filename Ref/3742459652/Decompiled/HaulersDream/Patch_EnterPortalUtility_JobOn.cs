using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(EnterPortalUtility), "JobOnPortal")]
public static class Patch_EnterPortalUtility_JobOn
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadPortal ?? true;
	}

	private static bool Prefix(Pawn p, MapPortal portal, ref Job __result)
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
		Job val = TransportLoad.TryGiveBulkJob(p, mapPortalBulkTarget);
		if (val == null)
		{
			HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
			if (instance != null && instance.LoadFullyClaimedByOthers(mapPortalBulkTarget))
			{
				return false;
			}
			return true;
		}
		__result = val;
		return false;
	}
}
