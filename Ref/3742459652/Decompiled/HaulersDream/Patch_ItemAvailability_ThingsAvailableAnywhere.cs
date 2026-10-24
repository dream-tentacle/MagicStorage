using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(ItemAvailability), "ThingsAvailableAnywhere")]
public static class Patch_ItemAvailability_ThingsAvailableAnywhere
{
	private static void Postfix(ref bool __result, ThingDef need, int amount, Pawn pawn)
	{
		if (__result)
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && ((pawn != null) ? ((Thing)pawn).Map : null) != null)
		{
			int num = 0;
			if (settings.shareHandHauledToStorage)
			{
				num += CarriedHaulShare.CountStorageBoundCarried(((Thing)pawn).Map, pawn, need);
			}
			if (settings.buildFromInventory)
			{
				num += OrganicInventoryShare.CountOrganic(((Thing)pawn).Map, pawn, need);
			}
			else if (settings.shareForBuilding)
			{
				num += InventoryShare.CountSharable(((Thing)pawn).Map, pawn, need);
			}
			bool allowPartial = settings.buildFromInventory && settings.buildFromInventoryPartial;
			if (BuildFromInventorySource.IsAvailable(num, amount, allowPartial))
			{
				__result = true;
			}
		}
	}
}
