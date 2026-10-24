using HarmonyLib;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(Pawn_JobTracker), "TryTakeOrderedJob")]
public static class Patch_TryTakeOrderedJob_BulkHaulTakeover
{
	private static readonly FieldRef<Pawn_JobTracker, Pawn> PawnOf = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

	[HarmonyPriority(400)]
	private static bool Prefix(Pawn_JobTracker __instance, Job job, JobTag? tag, ref bool __result)
	{
		if (job == null || job.def != HaulersDreamDefOf.HaulersDream_BulkHaul)
		{
			return true;
		}
		Pawn val = PawnOf.Invoke(__instance);
		if (val == null)
		{
			return true;
		}
		return !BulkHaul.TryTakeoverSecondOrder(val, job, tag, ref __result);
	}
}
