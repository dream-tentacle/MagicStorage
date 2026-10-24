using System;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_GetRest), "TryGiveJob")]
public static class Patch_JobGiver_GetRest_UnloadFirst
{
	private static void Postfix(ref Job __result, Pawn pawn)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected I4, but got Unknown
		if (__result != null && pawn?.needs?.rest != null && OpportunisticUnloadPolicy.MaySwapRestJobForUnload((int)pawn.needs.rest.CurCategory))
		{
			Job val;
			try
			{
				val = OpportunisticUnload.TryGetPreDowntimeUnloadJob(pawn, HaulersDreamMod.Settings?.unloadBeforeSleep ?? false);
			}
			catch (Exception ex)
			{
				HDGuard.SeamDegraded(ex, "JobGiver_GetRest.TryGiveJob (HD unload-before-sleep)", pawn, "kept vanilla's rest job, so the pawn still sleeps.");
				return;
			}
			if (val != null)
			{
				__result = val;
			}
		}
	}

	private static Exception Finalizer(Exception __exception, Pawn pawn)
	{
		return HDGuard.SeamThrew(__exception, "JobGiver_GetRest.TryGiveJob (HD unload-before-sleep)", pawn, "this pawn's rest-job selection failed this scan.");
	}
}
