using HarmonyLib;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(Pawn_JobTracker), "TryOpportunisticJob")]
public static class Patch_Pawn_JobTracker_TryOpportunisticJob_NoRouteRecurse
{
	private static bool Prefix(Job finalizerJob, Job job, ref Job __result)
	{
		if (!StorageRouting.IsRelocation(job))
		{
			return true;
		}
		__result = finalizerJob;
		return false;
	}
}
