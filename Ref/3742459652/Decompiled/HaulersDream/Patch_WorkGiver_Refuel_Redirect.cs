using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_Refuel), "JobOnThing")]
public static class Patch_WorkGiver_Refuel_Redirect
{
	private static bool Prefix(object __instance, Pawn pawn, Thing t, bool forced, ref Job __result)
	{
		if (__instance == null || __instance.GetType() != typeof(WorkGiver_Refuel))
		{
			return true;
		}
		if (!BulkRefuel.FeatureEnabled)
		{
			return true;
		}
		if (!BulkRefuel.HasPotentialBulkRefuel(pawn, t))
		{
			return true;
		}
		Job val = BulkRefuel.TryGiveBulkRefuelJob(pawn, t, forced);
		if (val != null)
		{
			__result = val;
			return false;
		}
		return true;
	}
}
