using System;
using System.Collections.Generic;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobDriver_Slaughter), "MakeNewToils")]
public static class Patch_JobDriver_Slaughter_HaulAfter
{
	private static IEnumerable<Toil> Postfix(IEnumerable<Toil> toils, JobDriver_Slaughter __instance)
	{
		((JobDriver)__instance).AddFinishAction((Action<JobCondition>)delegate(JobCondition cond)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Invalid comparison between Unknown and I4
			if ((int)cond == 2)
			{
				Pawn pawn = ((JobDriver)__instance).pawn;
				Job job = ((JobDriver)__instance).job;
				SlaughterHaul.TryAppendHaul(pawn, (job != null) ? ((LocalTargetInfo)(ref job.targetA)).Thing : null, HaulKillSource.Slaughter);
			}
		});
		return toils;
	}
}
