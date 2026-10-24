using System.Collections.Generic;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_DoBill), "JobOnThing")]
public static class Patch_WorkGiver_DoBill_InventoryRoute
{
	private static void Postfix(ref Job __result, Pawn pawn, Thing thing, bool forced)
	{
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !GatherOwnershipPolicy.PlainGatherEnabled(settings.inventoryCraftDeliver, settings.shareForCrafting, settings.markForUnload))
		{
			return;
		}
		Job val = __result;
		if (val == null || val.def != JobDefOf.DoBill || val.bill?.recipe == null || val.bill.recipe.allowMixingIngredients || BillRouteGate.ChosenIngredientsUnstackable(val) || forced || val.playerForced || CommonSenseCompat.GathersIngredients || !BillRouteGate.MayRouteToInventory(((LocalTargetInfo)(ref val.targetA)).Thing) || BillPrepTracker.ShouldSkip(pawn) || !IsEligibleCrafter(pawn) || HoldsTaggedStockForBill(pawn, val.bill))
		{
			return;
		}
		List<LocalTargetInfo> targetQueueB = val.targetQueueB;
		List<int> countQueue = val.countQueue;
		if (targetQueueB == null || countQueue == null || countQueue.Count != targetQueueB.Count)
		{
			return;
		}
		List<int> list = new List<int>();
		for (int i = 0; i < targetQueueB.Count; i++)
		{
			LocalTargetInfo val2 = targetQueueB[i];
			Thing thing2 = ((LocalTargetInfo)(ref val2)).Thing;
			if (thing2 != null && thing2.Spawned && countQueue[i] > 0 && !(thing2.ParentHolder is Pawn_InventoryTracker) && !(thing2 is UnfinishedThing))
			{
				list.Add(i);
			}
		}
		if (list.Count >= 2)
		{
			Job val3 = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_BillPrepGather, val.targetA);
			val3.count = 1;
			val3.bill = val.bill;
			val3.targetQueueB = new List<LocalTargetInfo>(list.Count);
			val3.countQueue = new List<int>(list.Count);
			for (int j = 0; j < list.Count; j++)
			{
				val3.targetQueueB.Add(targetQueueB[list[j]]);
				val3.countQueue.Add(countQueue[list[j]]);
			}
			__result = val3;
			HDLog.Dbg($"[BillPrep] {pawn} gathers {list.Count} ingredient stacks for {((Def)val.bill.recipe).defName} in one sweep.");
		}
	}

	private static bool IsEligibleCrafter(Pawn pawn)
	{
		if (!BillRouteGate.WorkerMayShareCraft(pawn))
		{
			return false;
		}
		if (pawn == null || ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null)
		{
			return false;
		}
		if (pawn.Drafted)
		{
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings == null || settings.pauseWhileDrafted)
			{
				return false;
			}
		}
		return YieldRouter.IsEligible(pawn);
	}

	private static bool HoldsTaggedStockForBill(Pawn pawn, Bill bill)
	{
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		ThingOwner<Thing> val = pawn.inventory?.innerContainer;
		if (comp == null || val == null || bill?.recipe == null)
		{
			return false;
		}
		foreach (Thing item in comp.GetHashSet())
		{
			if (item != null && ((ThingOwner)val).Contains(item) && InventoryShare.IsUsableForBill(item, bill))
			{
				return true;
			}
		}
		return false;
	}
}
