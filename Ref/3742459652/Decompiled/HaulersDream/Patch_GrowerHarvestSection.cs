using System.Collections.Generic;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_GrowerHarvest), "JobOnCell")]
public static class Patch_GrowerHarvestSection
{
	private const int SectionSize = 8;

	private static void Postfix(ref Job __result, Pawn pawn, IntVec3 c, bool forced)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		if (__result == null || __result.def != JobDefOf.Harvest || forced)
		{
			return;
		}
		List<LocalTargetInfo> targetQueueA = __result.targetQueueA;
		if (targetQueueA == null || targetQueueA.Count <= 8)
		{
			return;
		}
		Map val = ((pawn != null) ? ((Thing)pawn).Map : null);
		if (val == null || !MapGate.HdActiveOnMap(val) || !YieldRouter.IsCandidate(pawn))
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null)
		{
			Plant plant = GridsUtility.GetPlant(c, val);
			HaulSourceType type = ((plant != null && ((Thing)plant).def.plant.IsTree) ? HaulSourceType.Logging : HaulSourceType.Harvest);
			if (settings.BehaviorFor(type) == YieldBehavior.DropThenHaul)
			{
				int num = HarvestSectionPolicy.Cap(targetQueueA.Count, 8);
				targetQueueA.RemoveRange(num, targetQueueA.Count - num);
			}
		}
	}
}
