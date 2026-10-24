using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ResourceDeliverJobFor")]
[HarmonyPriority(0)]
public static class Patch_ResourceDeliverJobFor_Routing
{
	private static void Postfix(ref Job __result, Pawn pawn, IConstructible c, bool forced)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Invalid comparison between Unknown and I4
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.storageRouting || !settings.routeSupplies)
		{
			return;
		}
		Job val = __result;
		if (val == null || ((pawn != null) ? ((Thing)pawn).Map : null) == null || val.def != JobDefOf.HaulToContainer || (int)val.haulMode != 3 || c is Blueprint_Install || forced || val.playerForced)
		{
			return;
		}
		Thing val2 = (Thing)(((LocalTargetInfo)(ref val.targetC)).Thing ?? ((object)((LocalTargetInfo)(ref val.targetB)).Thing) ?? ((object)((c is Thing) ? c : null)));
		if (val2 == null || !val2.Spawned)
		{
			return;
		}
		IntVec3 position = val2.Position;
		if (!((IntVec3)(ref position)).IsValid || !StorageRouting.MayRoute(pawn))
		{
			return;
		}
		List<LocalTargetInfo> list = CollectPlannedStacks(val);
		if (list.Count != 0)
		{
			Job val3 = StorageRouting.TryRouteToConsumer(pawn, ((LocalTargetInfo)(ref val.targetA)).Thing?.def, position, list, settings.routeToEqualPriority, settings.routeToStockpiles);
			if (val3 != null)
			{
				__result = val3;
			}
		}
	}

	private static List<LocalTargetInfo> CollectPlannedStacks(Job job)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		List<LocalTargetInfo> list = new List<LocalTargetInfo>(1 + (job.targetQueueA?.Count ?? 0));
		if (((LocalTargetInfo)(ref job.targetA)).Thing != null)
		{
			list.Add(job.targetA);
		}
		if (job.targetQueueA != null)
		{
			for (int i = 0; i < job.targetQueueA.Count; i++)
			{
				LocalTargetInfo val = job.targetQueueA[i];
				if (((LocalTargetInfo)(ref val)).Thing != null)
				{
					list.Add(job.targetQueueA[i]);
				}
			}
		}
		return list;
	}
}
