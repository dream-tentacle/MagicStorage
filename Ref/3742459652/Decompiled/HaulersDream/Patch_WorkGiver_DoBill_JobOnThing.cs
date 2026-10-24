using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_DoBill), "JobOnThing")]
public static class Patch_WorkGiver_DoBill_JobOnThing
{
	private static void Postfix(Job __result, Pawn pawn)
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.shareForCrafting || !settings.shareMeetInMiddle || pawn == null || !BillRouteGate.WorkerMayShareCraft(pawn) || __result == null || __result.def != JobDefOf.DoBill || __result.targetQueueB == null)
		{
			return;
		}
		for (int i = 0; i < __result.targetQueueB.Count; i++)
		{
			LocalTargetInfo val = __result.targetQueueB[i];
			Thing thing = ((LocalTargetInfo)(ref val)).Thing;
			if (((thing != null) ? thing.ParentHolder : null) is Pawn_InventoryTracker)
			{
				SharedInventoryApproach.MaybeApproach(thing, pawn);
			}
		}
	}
}
