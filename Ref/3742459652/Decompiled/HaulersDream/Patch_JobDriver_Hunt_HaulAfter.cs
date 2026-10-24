using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobDriver_Hunt), "MakeNewToils")]
public static class Patch_JobDriver_Hunt_HaulAfter
{
	private static IEnumerable<Toil> Postfix(IEnumerable<Toil> toils, JobDriver_Hunt __instance)
	{
		((JobDriver)__instance).AddFinishAction((Action<JobCondition>)delegate(JobCondition cond)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Invalid comparison between Unknown and I4
			if ((int)cond != 2)
			{
				SlaughterHaul.TryAppendHuntKillHaul(__instance);
			}
		});
		return toils;
	}
}
