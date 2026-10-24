using System;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(Toils_Haul), "CarryHauledThingToCell")]
public static class Patch_CarryHauledThingToCell_ReRoute
{
	private static void Postfix(Toil __result, TargetIndex squareIndex, PathEndMode pathEndMode)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		if (__result != null)
		{
			__result.endConditions.Insert(0, () => ReRouteIfDestinationFilled(__result, squareIndex, pathEndMode));
		}
	}

	private static JobCondition ReRouteIfDestinationFilled(Toil toil, TargetIndex squareIndex, PathEndMode pathEndMode)
	{
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Invalid comparison between Unknown and I4
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Pawn actor = toil.actor;
			Job val = actor?.jobs?.curJob;
			if (val == null || val.def != JobDefOf.HaulToCell || (int)val.haulMode != 1)
			{
				return (JobCondition)1;
			}
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings == null || !settings.haulToStack)
			{
				return (JobCondition)1;
			}
			Pawn_CarryTracker carryTracker = actor.carryTracker;
			Thing val2 = ((carryTracker != null) ? carryTracker.CarriedThing : null);
			if (val2?.def == null)
			{
				return (JobCondition)1;
			}
			LocalTargetInfo target = val.GetTarget(squareIndex);
			IntVec3 cell = ((LocalTargetInfo)(ref target)).Cell;
			if (StoreUtility.IsValidStorageFor(cell, ((Thing)actor).Map, val2))
			{
				return (JobCondition)1;
			}
			if (HaulChurnPolicy.ShouldBail(HaulChurnGuard.PeekRetargets(val)))
			{
				return (JobCondition)1;
			}
			IntVec3 val3 = default(IntVec3);
			if (StoreUtility.TryFindBestBetterStoreCellFor(val2, actor, ((Thing)actor).Map, (StoragePriority)0, ((Thing)actor).Faction, ref val3, true) && ((IntVec3)(ref val3)).IsValid && val3 != cell)
			{
				HaulChurnGuard.CountRetarget(val);
				val.SetTarget(squareIndex, LocalTargetInfo.op_Implicit(val3));
				actor.pather.StartPath(LocalTargetInfo.op_Implicit(val3), pathEndMode);
			}
			return (JobCondition)1;
		}
		catch (Exception ex)
		{
			HDGuard.SeamDegraded(ex, "Toils_Haul.CarryHauledThingToCell (HD stacking re-route)", toil?.actor, "in-flight re-route skipped; vanilla's fail-on drops the item as before.");
			return (JobCondition)1;
		}
	}

	private static Exception Finalizer(Exception __exception)
	{
		return HDGuard.SeamThrew(__exception, "Toils_Haul.CarryHauledThingToCell (HD stacking re-route wrap)", null, "in-flight re-route not attached; vanilla's fail-on handles a filled cell as before.");
	}
}
