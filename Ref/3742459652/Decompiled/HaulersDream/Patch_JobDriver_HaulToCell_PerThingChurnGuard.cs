using System;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobDriver_HaulToCell), "Notify_Starting")]
public static class Patch_JobDriver_HaulToCell_PerThingChurnGuard
{
	private static void Postfix(JobDriver_HaulToCell __instance)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Invalid comparison between Unknown and I4
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.haulToStack)
		{
			return;
		}
		Job job = ((JobDriver)__instance).job;
		object obj;
		if (job == null)
		{
			obj = null;
		}
		else
		{
			LocalTargetInfo target = job.GetTarget((TargetIndex)1);
			obj = ((LocalTargetInfo)(ref target)).Thing;
		}
		Thing val = (Thing)obj;
		if (job == null || job.playerForced || (int)job.haulMode != 1)
		{
			return;
		}
		Thing thing = val;
		if (thing?.def == null)
		{
			return;
		}
		((JobDriver)__instance).AddFinishAction((Action<JobCondition>)delegate(JobCondition condition)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Invalid comparison between Unknown and I4
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Invalid comparison between Unknown and I4
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Invalid comparison between Unknown and I4
			if ((int)condition == 2)
			{
				HaulChurnGuard.NoteThingHaulSucceeded(thing);
			}
			else if ((int)condition == 4 || (int)condition == 128)
			{
				HaulChurnGuard.NoteThingHaulFailed(thing);
			}
		});
	}

	private static Exception Finalizer(Exception __exception)
	{
		return HDGuard.SeamThrew(__exception, "JobDriver_HaulToCell.Notify_Starting (HD per-thing churn guard)", null, "per-thing churn tally not attached; the per-job arrival guard still applies for this job.");
	}
}
