using System;
using HarmonyLib;
using HaulersDream.Core;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(Toils_Haul), "PlaceHauledThingInCell")]
public static class Patch_PlaceHauledThingInCell_ChurnGuard
{
	private static void Postfix(Toil __result, Toil nextToilOnPlaceFailOrIncomplete, bool storageMode)
	{
		if (!storageMode || nextToilOnPlaceFailOrIncomplete == null || __result == null)
		{
			return;
		}
		Toil toil = __result;
		Action original = toil.initAction;
		if (original == null)
		{
			return;
		}
		toil.initAction = delegate
		{
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Invalid comparison between Unknown and I4
			//IL_009b: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Invalid comparison between Unknown and I4
			//IL_010b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_013e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0158: Unknown result type (might be due to invalid IL or missing references)
			//IL_0172: Unknown result type (might be due to invalid IL or missing references)
			Pawn actor = toil.actor;
			Pawn_JobTracker val = actor?.jobs;
			Job val2 = val?.curJob;
			if (val2 == null)
			{
				HaulChurnGuard.EnterPlacementWrapper();
				try
				{
					original();
					return;
				}
				finally
				{
					HaulChurnGuard.ExitPlacementWrapper();
				}
			}
			Pawn_CarryTracker carryTracker = actor.carryTracker;
			Thing val3 = ((carryTracker != null) ? carryTracker.CarriedThing : null);
			int num = val3?.stackCount ?? 0;
			HaulMode haulMode = val2.haulMode;
			HaulChurnGuard.EnterPlacementWrapper();
			try
			{
				original();
			}
			finally
			{
				HaulChurnGuard.ExitPlacementWrapper();
			}
			bool flag = val.curJob == val2;
			if (flag && (int)val2.haulMode == 2 && (int)haulMode != 2)
			{
				HaulChurnGuard.NoteAsideDegrade(val2);
			}
			Pawn_CarryTracker carryTracker2 = actor.carryTracker;
			Thing val4 = ((carryTracker2 != null) ? carryTracker2.CarriedThing : null);
			bool flag2 = val4 != null;
			bool madeProgress = flag2 && val4 == val3 && val4.stackCount < num;
			if (!HaulChurnPolicy.CountsAsRetarget(flag, flag2, madeProgress))
			{
				if (flag)
				{
					if (flag2)
					{
						HaulChurnGuard.NotifyProgress(val2);
					}
					else
					{
						if (HaulChurnGuard.WasAsideDegraded(val2))
						{
							IntVec3 cell = ((LocalTargetInfo)(ref val2.targetB)).Cell;
							HDLog.Dbg("aside-degrade " + ((Entity)actor).LabelShort + ": vanilla placed " + $"{Gen.ToStringSafe<Thing>(val3)} at {cell} " + $"(inHome={InventoryDrop.IsInHome(((Thing)actor).Map, cell)}, " + $"pawnAt={((Thing)actor).Position}); backing the thing off.");
							HaulChurnGuard.StampBackoff(val3);
						}
						HaulChurnGuard.NotifyPlaced(val2);
					}
				}
			}
			else
			{
				int num2 = HaulChurnGuard.CountRetarget(val2);
				if (HaulChurnPolicy.ShouldBail(num2))
				{
					HaulChurnGuard.StampBackoff(val4);
					HaulChurnGuard.NotifyPlaced(val2);
					HDLog.Dbg($"{actor} storage haul churned {num2} consecutive zero-progress arrivals " + "(carrying " + ((Entity)val4).LabelShort + "); ending the job and backing off.");
					val.EndCurrentJob((JobCondition)4, true, true);
				}
			}
		};
	}

	private static Exception Finalizer(Exception __exception)
	{
		return HDGuard.SeamThrew(__exception, "Toils_Haul.PlaceHauledThingInCell (HD churn guard)", null, "storage-haul placement could not be wrapped; vanilla behavior stands for this toil.");
	}
}
