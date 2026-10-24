using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(StoreUtility), "IsGoodStoreCell")]
public static class Patch_IsGoodStoreCell_HonourCommitments
{
	private static void Postfix(ref bool __result, IntVec3 __0, Map __1, Thing __2, Pawn __3)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Invalid comparison between Unknown and I4
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		if (!__result || __3 == null || StorageCommitments.InsideSpaceScan || StorageCommitments.InForcedOrder || !StorageCommitments.AnyClaims || __2?.def == null || (int)__2.def.category != 2 || !StorageCommitments.GatesVanillaStorage(__1))
		{
			return;
		}
		ISlotGroup val = BulkHaul.BudgetGroupOf(__1.haulDestinationManager.SlotGroupAt(__0));
		if (val != null)
		{
			bool observationTruncated;
			int num = StorageCommitments.FreeUnitsFor(__3, val, __2.def, __2, out observationTruncated);
			if (!observationTruncated && num <= 0)
			{
				__result = false;
			}
		}
	}
}
