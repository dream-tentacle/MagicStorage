using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(HaulAIUtility), "HaulToCellStorageJob")]
public static class Patch_HaulToCellStorageJob_ClampToCommitments
{
	private static void Postfix(Job __result, Pawn __0, Thing __1, IntVec3 __2)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Invalid comparison between Unknown and I4
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		if (__result == null || __0 == null || __1?.def == null || StorageCommitments.InsideSpaceScan || StorageCommitments.InForcedOrder || !StorageCommitments.AnyClaims)
		{
			return;
		}
		Map map = ((Thing)__0).Map;
		if (!StorageCommitments.GatesVanillaStorage(map) || (int)__1.def.category != 2)
		{
			return;
		}
		ISlotGroup val = BulkHaul.BudgetGroupOf(map.haulDestinationManager.SlotGroupAt(__2));
		if (val == null)
		{
			return;
		}
		bool observationTruncated;
		int num = StorageCommitments.FreeUnitsFor(__0, val, __1.def, __1, out observationTruncated);
		if (num != int.MaxValue)
		{
			int num2 = Math.Max(1, num);
			int num3 = (observationTruncated ? Math.Max(__result.count, 1) : ((__result.count > 0) ? Math.Min(__result.count, num2) : num2));
			if (num3 != __result.count)
			{
				StorageCommitments.Trace("count", __0, val, __1.def, num3, num);
				__result.count = num3;
			}
		}
	}
}
