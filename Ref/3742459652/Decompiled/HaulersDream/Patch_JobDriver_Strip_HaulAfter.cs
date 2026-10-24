using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobDriver_Strip), "MakeNewToils")]
public static class Patch_JobDriver_Strip_HaulAfter
{
	private static IEnumerable<Toil> Postfix(IEnumerable<Toil> toils, JobDriver_Strip __instance)
	{
		((JobDriver)__instance).AddFinishAction((Action<JobCondition>)delegate(JobCondition cond)
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Invalid comparison between Unknown and I4
			Pawn pawn = ((JobDriver)__instance).pawn;
			Job job = ((JobDriver)__instance).job;
			CorpseStripper.ApplyTaintedPolicyToPending(pawn, ((job != null) ? ((LocalTargetInfo)(ref job.targetA)).Thing : null) is Corpse);
			if ((int)cond == 2)
			{
				Pawn pawn2 = ((JobDriver)__instance).pawn;
				Job job2 = ((JobDriver)__instance).job;
				CorpseStripper.QueueReStripIfNeeded(pawn2, (job2 != null) ? ((LocalTargetInfo)(ref job2.targetA)).Thing : null);
			}
		});
		return toils;
	}
}
