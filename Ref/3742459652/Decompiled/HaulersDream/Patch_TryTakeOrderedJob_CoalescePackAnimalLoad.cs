using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(Pawn_JobTracker), "TryTakeOrderedJob")]
public static class Patch_TryTakeOrderedJob_CoalescePackAnimalLoad
{
	private static readonly FieldRef<Pawn_JobTracker, Pawn> PawnOf = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

	[HarmonyPriority(600)]
	private static bool Prefix(Pawn_JobTracker __instance, Job job, JobTag? tag, bool requestQueueing, ref bool __result)
	{
		if (job?.def != JobDefOf.GiveToPackAnimal)
		{
			return true;
		}
		Pawn pawn = PawnOf.Invoke(__instance);
		if (!PackAnimalLoad.ShouldRedirectGiveToPackAnimal(pawn, job))
		{
			return true;
		}
		Thing thing = ((LocalTargetInfo)(ref job.targetA)).Thing;
		int count = ((job.count > 0) ? job.count : thing.stackCount);
		Job val = PackAnimalLoad.FindActiveLoadJob(pawn);
		if (val != null)
		{
			PackAnimalLoad.AppendToLoadJob(val, thing, count);
			__result = true;
			return false;
		}
		Job val2 = PackAnimalLoad.BuildRedirectJob(pawn, job);
		if (val2 == null)
		{
			return true;
		}
		__result = __instance.TryTakeOrderedJob(val2, tag, requestQueueing);
		return false;
	}
}
