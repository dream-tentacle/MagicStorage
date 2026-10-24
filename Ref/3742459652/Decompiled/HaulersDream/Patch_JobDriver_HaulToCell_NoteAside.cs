using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobDriver_HaulToCell), "Notify_Starting")]
public static class Patch_JobDriver_HaulToCell_NoteAside
{
	private static void Postfix(JobDriver_HaulToCell __instance)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		Job val = ((JobDriver)(__instance?)).job;
		if (val != null && !val.playerForced && val.def == JobDefOf.HaulToCell && (int)val.haulMode == 2)
		{
			LocalTargetInfo target = val.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing?.def != null)
			{
				HaulAsideGuard.NoteAsideRun(thing);
			}
		}
	}

	private static Exception Finalizer(Exception __exception)
	{
		return HDGuard.SeamThrew(__exception, "JobDriver_HaulToCell.Notify_Starting (HD haul-aside note)", null, "aside suppression not stamped for this job; the other churn layers are unaffected.");
	}
}
