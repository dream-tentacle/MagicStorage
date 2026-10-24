using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_DoBill), "JobOnThing")]
[HarmonyPriority(0)]
public static class Patch_WorkGiver_DoBill_Routing
{
	private static void Postfix(ref Job __result, Pawn pawn, Thing thing, bool forced)
	{
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.storageRouting || !settings.routeIngredients)
		{
			return;
		}
		Job val = __result;
		if (val == null || ((pawn != null) ? ((Thing)pawn).Map : null) == null || val.def != JobDefOf.DoBill || val.bill?.recipe == null || forced || val.playerForced || !BillRouteGate.MayRouteToInventory(((LocalTargetInfo)(ref val.targetA)).Thing))
		{
			return;
		}
		IntVec3 position = ((LocalTargetInfo)(ref val.targetA)).Thing.Position;
		if (!((IntVec3)(ref position)).IsValid || !StorageRouting.MayRoute(pawn))
		{
			return;
		}
		List<LocalTargetInfo> list = CollectPlannedIngredients(val);
		if (list.Count != 0)
		{
			Job val2 = StorageRouting.TryRouteToConsumer(pawn, null, position, list, settings.routeToEqualPriority, settings.routeToStockpiles);
			if (val2 != null)
			{
				__result = val2;
			}
		}
	}

	private static List<LocalTargetInfo> CollectPlannedIngredients(Job job)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		List<LocalTargetInfo> targetQueueB = job.targetQueueB;
		List<LocalTargetInfo> list = new List<LocalTargetInfo>(targetQueueB?.Count ?? 0);
		if (targetQueueB != null)
		{
			for (int i = 0; i < targetQueueB.Count; i++)
			{
				LocalTargetInfo val = targetQueueB[i];
				Thing thing = ((LocalTargetInfo)(ref val)).Thing;
				if (thing != null && thing.Spawned && !(thing.ParentHolder is Pawn_InventoryTracker) && !(thing is UnfinishedThing))
				{
					list.Add(targetQueueB[i]);
				}
			}
		}
		return list;
	}
}
