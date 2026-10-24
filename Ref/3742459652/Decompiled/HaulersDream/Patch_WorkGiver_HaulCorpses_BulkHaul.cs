using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_HaulCorpses), "JobOnThing")]
public static class Patch_WorkGiver_HaulCorpses_BulkHaul
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
		return HDGuard.SeamThrew(__exception, "WorkGiver_HaulCorpses.JobOnThing (HD bulk-haul)", pawn, "this pawn's corpse-hauling job could not be built this scan.");
	}
}
