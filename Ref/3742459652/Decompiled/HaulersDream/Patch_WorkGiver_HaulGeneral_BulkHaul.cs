using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_HaulGeneral), "JobOnThing")]
public static class Patch_WorkGiver_HaulGeneral_BulkHaul
{
	private static void Postfix(ref Job __result, Pawn pawn, Thing t, bool forced)
	{
		Job val = BulkHaul.TryBuildBulkJob(pawn, t, __result, forced);
		if (val != null)
		{
			__result = val;
		}
	}

	private static Exception Finalizer(Exception __exception, Pawn pawn)
	{
		return HDGuard.SeamThrew(__exception, "WorkGiver_HaulGeneral.JobOnThing (HD bulk-haul)", pawn, "this pawn's hauling job could not be built this scan.");
	}
}
