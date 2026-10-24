using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_GetJoy), "TryGiveJob")]
public static class Patch_JobGiver_GetJoy_UnloadFirst
{
	private static void Postfix(ref Job __result, Pawn pawn)
	{
		if (__result != null)
		{
			Job val;
			try
			{
				val = OpportunisticUnload.TryGetPreDowntimeUnloadJob(pawn, HaulersDreamMod.Settings?.unloadBeforeLeisure ?? false);
			}
			catch (Exception ex)
			{
				HDGuard.SeamDegraded(ex, "JobGiver_GetJoy.TryGiveJob (HD unload-before-leisure)", pawn, "kept vanilla's joy job, so the pawn still gets recreation.");
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
		return HDGuard.SeamThrew(__exception, "JobGiver_GetJoy.TryGiveJob (HD unload-before-leisure)", pawn, "this pawn's joy-job selection failed this scan.");
	}
}
