using System;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
public static class Patch_JobGiver_GetFood_UnloadFirst
{
	private static void Postfix(ref Job __result, Pawn pawn)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected I4, but got Unknown
		if (__result != null && pawn?.needs?.food != null && OpportunisticUnloadPolicy.MaySwapFoodJobForUnload((int)pawn.needs.food.CurCategory))
		{
			Job val;
			try
			{
				val = OpportunisticUnload.TryGetPreDowntimeUnloadJob(pawn, HaulersDreamMod.Settings?.unloadBeforeEating ?? false);
			}
			catch (Exception ex)
			{
				HDGuard.SeamDegraded(ex, "JobGiver_GetFood.TryGiveJob (HD unload-before-eating)", pawn, "kept vanilla's food job, so the pawn still eats.");
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
		return HDGuard.SeamThrew(__exception, "JobGiver_GetFood.TryGiveJob (HD unload-before-eating)", pawn, "this pawn's food-job selection failed this scan.");
	}
}
