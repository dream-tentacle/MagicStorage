using System.Collections.Generic;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_DropUnusedInventory), "TryGiveJob")]
public static class Patch_JobGiver_DropUnusedInventory_TryGiveJob
{
	private static void Prefix(Pawn pawn)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		if (pawn?.mindState == null || pawn.inventory == null)
		{
			return;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp == null)
		{
			return;
		}
		HashSet<Thing> hashSet = comp.GetHashSet();
		if (hashSet.Count == 0)
		{
			return;
		}
		bool flag = false;
		foreach (Thing item in hashSet)
		{
			ThingDef val = item?.def;
			if (val != null)
			{
				int preferabilityInt = ((val.ingestible != null) ? ((int)val.ingestible.preferability) : 0);
				if (DropUnusedFoodPolicy.IsRawFoodDropCandidate(val.IsIngestible, val.IsDrug, preferabilityInt))
				{
					flag = true;
					break;
				}
			}
		}
		if (flag)
		{
			TickManager tickManager = Find.TickManager;
			int num = ((tickManager != null) ? tickManager.TicksGame : pawn.mindState.lastInventoryRawFoodUseTick);
			if (num > pawn.mindState.lastInventoryRawFoodUseTick)
			{
				pawn.mindState.lastInventoryRawFoodUseTick = num;
			}
		}
	}
}
