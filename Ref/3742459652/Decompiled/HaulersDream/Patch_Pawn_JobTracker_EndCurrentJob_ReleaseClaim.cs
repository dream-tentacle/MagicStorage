using HarmonyLib;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(Pawn_JobTracker), "EndCurrentJob")]
public static class Patch_Pawn_JobTracker_EndCurrentJob_ReleaseClaim
{
	private static readonly FieldRef<Pawn_JobTracker, Pawn> PawnOf = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

	private static bool Prepare()
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return true;
		}
		if (!settings.enableBulkLoadTransporters && !settings.enableBulkLoadPortal)
		{
			if (settings.enableVehicleFramework)
			{
				return settings.enableBulkLoadVehicles;
			}
			return false;
		}
		return true;
	}

	private static void Prefix(Pawn_JobTracker __instance)
	{
		Pawn val = PawnOf.Invoke(__instance);
		if (val != null && __instance.curJob != null)
		{
			JobDef def = __instance.curJob.def;
			if (def == HaulersDreamDefOf.HaulersDream_LoadTransportersInBulk || def == HaulersDreamDefOf.HaulersDream_LoadPortalInBulk || def == HaulersDreamDefOf.HaulersDream_LoadVehicleInBulk)
			{
				HaulersDreamGameComponent.Instance?.LoadReleaseClaimsForPawn(val);
			}
		}
	}
}
