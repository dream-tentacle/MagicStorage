using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_UnloadCarriers), "JobOnThing")]
public static class Patch_WorkGiver_UnloadCarriers_JobOn
{
	private static bool Prefix(Pawn pawn, Thing t, bool forced, ref Job __result)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (!BulkUnloadGate.ShouldHandle(pawn, t))
		{
			return true;
		}
		if (!BulkUnloadGate.CanDoBulkUnload(pawn, (Pawn)t, forced))
		{
			return true;
		}
		__result = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_UnloadCarrierInBulk, LocalTargetInfo.op_Implicit(t));
		return false;
	}
}
