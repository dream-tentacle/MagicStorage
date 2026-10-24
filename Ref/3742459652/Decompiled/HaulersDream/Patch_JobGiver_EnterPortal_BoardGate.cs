using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_EnterPortal), "TryGiveJob")]
public static class Patch_JobGiver_EnterPortal_BoardGate
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadPortal ?? true;
	}

	private static bool Prefix(Pawn pawn, ref Job __result)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.enableBulkLoadPortal)
		{
			return true;
		}
		PawnDuty val = pawn?.mindState?.duty;
		if (val == null || val.def != DutyDefOf.LoadAndEnterPortal)
		{
			return true;
		}
		Thing thing = ((LocalTargetInfo)(ref val.focus)).Thing;
		MapPortal val2 = (MapPortal)(object)((thing is MapPortal) ? thing : null);
		if (val2 == null)
		{
			return true;
		}
		if (!BulkLoadAntiConflict.HasGoodsLeftToLoad(val2.leftToLoad))
		{
			return true;
		}
		HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
		if (instance != null)
		{
			MapPortalBulkTarget mapPortalBulkTarget = MapPortalBulkTarget.TryCreate(val2);
			if (mapPortalBulkTarget != null)
			{
				instance.LoadRegisterOrUpdate(mapPortalBulkTarget);
				if (!instance.LoadHasWork(mapPortalBulkTarget, pawn))
				{
					return true;
				}
			}
		}
		if (instance != null && instance.LoadAnyClaimsInProgress(MapPortalBulkTarget.LedgerKey(val2)))
		{
			__result = null;
			return false;
		}
		if (BulkLoadAntiConflict.AnyHdLoaderForPortal(val2))
		{
			__result = null;
			return false;
		}
		return true;
	}
}
