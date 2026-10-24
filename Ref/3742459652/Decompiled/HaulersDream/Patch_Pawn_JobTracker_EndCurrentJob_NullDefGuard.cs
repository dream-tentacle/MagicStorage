using HarmonyLib;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(Pawn_JobTracker), "EndCurrentJob")]
public static class Patch_Pawn_JobTracker_EndCurrentJob_NullDefGuard
{
	private static readonly FieldRef<Pawn_JobTracker, Pawn> PawnOf = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

	private static bool Prefix(Pawn_JobTracker __instance)
	{
		Job curJob = __instance.curJob;
		if (curJob == null || curJob.def != null)
		{
			return true;
		}
		Pawn obj = PawnOf.Invoke(__instance);
		if (obj != null)
		{
			obj.ClearReservationsForJob(curJob);
		}
		if (obj != null)
		{
			Pawn_PathFollower pather = obj.pather;
			if (pather != null)
			{
				pather.StopDead();
			}
		}
		__instance.curDriver = null;
		__instance.curJob = null;
		return false;
	}
}
