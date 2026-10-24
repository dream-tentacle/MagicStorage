using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(RecipeWorkerCounter), "CountProducts")]
public static class Patch_CountProducts_BankedInventory
{
	private static void Postfix(Bill_Production bill, ref int __result)
	{
		if (bill != null && !bill.includeEquipped)
		{
			__result += CraftBatchPlanner.BankedInFlightProductCount(bill);
		}
	}
}
