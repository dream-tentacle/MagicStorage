using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet")]
public static class Patch_WorkGiver_DoBill_TryFindBestBillIngredientsInSet
{
	private static void Prefix(List<Thing> availableThings, Bill bill, IntVec3 rootCell, ref bool alreadySorted)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (!Patch_WorkGiver_DoBill_TryFindBestBillIngredients.AddedThisSearch && settings != null && settings.shareForCrafting && availableThings != null && !CommonSenseCompat.GathersIngredients)
		{
			Pawn currentWorker = Patch_WorkGiver_DoBill_TryFindBestBillIngredients.CurrentWorker;
			if (BillRouteGate.WorkerMayShareCraft(currentWorker) && bill?.recipe != null)
			{
				InventoryShare.AddSharableStacksForBill(currentWorker, bill, availableThings);
				Patch_WorkGiver_DoBill_TryFindBestBillIngredients.AddedThisSearch = true;
			}
		}
		if (settings != null && availableThings != null && bill?.recipe != null && !bill.recipe.allowMixingIngredients && !alreadySorted && SpoilingFirst.ReorderInPlace(availableThings, settings))
		{
			alreadySorted = true;
		}
	}
}
