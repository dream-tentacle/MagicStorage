using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_EnterTransporter), "TryGiveJob")]
public static class Patch_JobGiver_EnterTransporter_BoardGate
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadTransporters ?? true;
	}

	private static bool Prefix(Pawn pawn, ref Job __result)
	{
		PawnDuty val = pawn?.mindState?.duty;
		if (val == null || val.def != DutyDefOf.LoadAndEnterTransporters)
		{
			return true;
		}
		int transportersGroup = val.transportersGroup;
		if (transportersGroup < 0)
		{
			return true;
		}
		if (!BulkLoadAntiConflict.GroupHasGoodsLeftToLoad(((Thing)pawn).Map, transportersGroup))
		{
			return true;
		}
		HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
		if (instance != null && instance.LoadAnyClaimsInProgress(transportersGroup))
		{
			__result = null;
			return false;
		}
		if (BulkLoadAntiConflict.AnyHdLoaderForGroupId(((Thing)pawn).Map, transportersGroup))
		{
			__result = null;
			return false;
		}
		return true;
	}
}
