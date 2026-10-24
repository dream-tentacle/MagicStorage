using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_Haul), "TryGiveJob")]
public static class Patch_JobGiver_Haul_AnimalBulk
{
	private static void Postfix(ref Job __result, Pawn pawn)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.allowAnimals || !settings.bulkHaul || __result == null || ((pawn != null) ? pawn.RaceProps : null) == null || pawn.RaceProps.Humanlike || pawn.RaceProps.IsMechanoid)
		{
			return;
		}
		Thing thing = ((LocalTargetInfo)(ref __result.targetA)).Thing;
		if (thing != null)
		{
			Job val = BulkHaul.TryBuildBulkJob(pawn, thing, __result, forced: false);
			if (val != null)
			{
				__result = val;
			}
		}
	}

	private static Exception Finalizer(Exception __exception, Pawn pawn)
	{
		return HDGuard.SeamThrew(__exception, "JobGiver_Haul.TryGiveJob (HD animal bulk-haul)", pawn, "this animal's haul-job selection failed this scan.");
	}
}
