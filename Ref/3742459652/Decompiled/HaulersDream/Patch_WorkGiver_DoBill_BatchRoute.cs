using System.Collections.Generic;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_DoBill), "JobOnThing")]
public static class Patch_WorkGiver_DoBill_BatchRoute
{
	[HarmonyPriority(800)]
	private static void Postfix(ref Job __result, Pawn pawn, Thing thing, bool forced)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		Job val = __result;
		if (val == null || val.def != JobDefOf.DoBill || val.bill?.recipe == null || !BillRouteGate.MayRouteToInventory(((LocalTargetInfo)(ref val.targetA)).Thing) || BillRouteGate.ChosenIngredientsUnstackable(val))
		{
			return;
		}
		Building_WorkTable bench = (Building_WorkTable)((LocalTargetInfo)(ref val.targetA)).Thing;
		if (!BillRouteGate.WorkerMayShareCraft(pawn) || CommonSenseCompat.BatchSuppressedByCommonSense)
		{
			return;
		}
		Bill bill = val.bill;
		HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
		if (instance == null || !instance.IsBatchBill(bill) || !CraftBatchPlanner.CanPawnBatch(pawn, bill))
		{
			return;
		}
		Bill_Production val2 = (Bill_Production)(object)((bill is Bill_Production) ? bill : null);
		if (val2 != null && val2.repeatMode == BillRepeatModeDefOf.TargetCount && !GenList.NullOrEmpty<ThingDefCountClass>((IList<ThingDefCountClass>)((Bill)val2).recipe.products) && !BatchPausePolicy.MayCraftMore(CraftBatchPlanner.EffectiveProductCount(val2), val2.targetCount, val2.paused))
		{
			__result = null;
		}
		else
		{
			if (HoldsBatchStockForBill(pawn, bill))
			{
				return;
			}
			int num = instance.BatchSizeOf(bill);
			if (num < 1)
			{
				return;
			}
			CraftBatchPlan craftBatchPlan = CraftBatchPlanner.Resolve(pawn, bench, bill, num, 0);
			if (craftBatchPlan.feasible)
			{
				Job val3 = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_BatchCraft, val.targetA);
				val3.count = 1;
				val3.bill = bill;
				if (forced || val.playerForced)
				{
					val3.playerForced = true;
				}
				BatchCraftHandoff.Set(val3, craftBatchPlan);
				__result = val3;
				HDLog.Dbg($"[Batch] {pawn} batch-crafting {craftBatchPlan.resolvedReps}× {((Def)bill.recipe).defName} (requested {num}).");
			}
		}
	}

	private static bool HoldsBatchStockForBill(Pawn pawn, Bill bill)
	{
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		ThingOwner<Thing> val = pawn.inventory?.innerContainer;
		if (comp == null || val == null || bill?.recipe == null)
		{
			return false;
		}
		List<ThingDefCountClass> products = bill.recipe.products;
		bool flag = !GenList.NullOrEmpty<SpecialProductType>((IList<SpecialProductType>)bill.recipe.specialProducts);
		foreach (Thing item in comp.GetHashSet())
		{
			if (item == null || !((ThingOwner)val).Contains(item))
			{
				continue;
			}
			if (InventoryShare.IsUsableForBill(item, bill))
			{
				return true;
			}
			if (InventorySurplus.SurplusOf(pawn, item) <= 0)
			{
				continue;
			}
			if (flag)
			{
				return true;
			}
			if (products == null)
			{
				continue;
			}
			for (int i = 0; i < products.Count; i++)
			{
				if (products[i]?.thingDef == item.def)
				{
					return true;
				}
			}
		}
		return false;
	}
}
