using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class RemoveFloorRouteExecutor
{
	private static WorkGiver_ConstructRemoveFloor removeFloorScanner;

	private static bool removeFloorScannerResolved;

	public static WorkGiver_ConstructRemoveFloor RemoveFloorScanner()
	{
		if (removeFloorScannerResolved)
		{
			return removeFloorScanner;
		}
		removeFloorScannerResolved = true;
		List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			if (typeof(WorkGiver_ConstructRemoveFloor).IsAssignableFrom(allDefsListForReading[i].giverClass))
			{
				WorkGiver worker = allDefsListForReading[i].Worker;
				WorkGiver_ConstructRemoveFloor val = (WorkGiver_ConstructRemoveFloor)(object)((worker is WorkGiver_ConstructRemoveFloor) ? worker : null);
				if (val != null)
				{
					removeFloorScanner = val;
					break;
				}
			}
		}
		return removeFloorScanner;
	}

	public static void ExecuteRemoveFloorRouteSynced(Pawn pawn, IntVec3 anchor, RemoveFloorRouteMode mode, int amount, int radius, float maxDistance, bool replace, List<IntVec3> mustInclude, RouteSelectionMethod selectionMethod, int exactMax)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (((pawn != null) ? ((Thing)pawn).Map : null) != null && ((IntVec3)(ref anchor)).IsValid && GenGrid.InBounds(anchor, ((Thing)pawn).Map))
		{
			Execute(pawn, anchor, mode, amount, radius, maxDistance, replace, mustInclude, selectionMethod, exactMax);
		}
	}

	public static void Execute(Pawn pawn, IntVec3 anchor, RemoveFloorRouteMode mode, int amount, int radius, float maxDistance, bool replace, IReadOnlyList<IntVec3> mustInclude = null, RouteSelectionMethod selectionMethod = RouteSelectionMethod.MostStopsPerTravel, int exactMax = 12, RemoveFloorRoutePlan precomputed = null)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Expected O, but got Unknown
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Unknown result type (might be due to invalid IL or missing references)
		//IL_029e: Unknown result type (might be due to invalid IL or missing references)
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null)
		{
			return;
		}
		WorkGiver_ConstructRemoveFloor val = RemoveFloorScanner();
		if (val == null)
		{
			return;
		}
		RemoveFloorRoutePlan removeFloorRoutePlan = ((precomputed != null && precomputed.cells.Count > 0) ? precomputed : RemoveFloorRoutePlanner.Plan(pawn, anchor, mode, amount, radius, maxDistance, mustInclude, selectionMethod, exactMax));
		if (removeFloorRoutePlan.cells.Count == 0)
		{
			if (MultiplayerCompat.ShouldShowLocalFeedback)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.NoTargets")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
			}
			return;
		}
		int count = removeFloorRoutePlan.cells.Count;
		Map map = ((Thing)pawn).Map;
		List<Job> list = new List<Job>(removeFloorRoutePlan.cells.Count);
		List<IntVec3> list2 = new List<IntVec3>(removeFloorRoutePlan.cells.Count);
		for (int i = 0; i < removeFloorRoutePlan.cells.Count; i++)
		{
			IntVec3 val2 = removeFloorRoutePlan.cells[i];
			if (map.designationManager.DesignationAt(val2, DesignationDefOf.RemoveFloor) == null)
			{
				map.designationManager.AddDesignation(new Designation(LocalTargetInfo.op_Implicit(val2), DesignationDefOf.RemoveFloor, (ColorDef)null));
			}
			if (((WorkGiver_Scanner)val).HasJobOnCell(pawn, val2, true))
			{
				Job val3 = ((WorkGiver_Scanner)val).JobOnCell(pawn, val2, true);
				if (val3 != null)
				{
					list.Add(val3);
					list2.Add(val2);
				}
			}
		}
		if (list.Count == 0)
		{
			if (MultiplayerCompat.ShouldShowLocalFeedback)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.NoTargets")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
			}
			return;
		}
		int num = (replace ? QueueReplace(pawn, val, list, list2) : QueueAppend(pawn, val, list));
		HDLog.Dbg(string.Format("{0} planned a {1} remove-floor route ({2}): {3}/{4} cell(s), ", pawn, mode, replace ? "replace" : "append", num, count) + $"cappedAmount={removeFloorRoutePlan.cappedByAmount}, cappedDist={removeFloorRoutePlan.cappedByDistance}.");
		if (MultiplayerCompat.ShouldShowLocalFeedback)
		{
			if (num == 0)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.NoTargets")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
			}
			else if (removeFloorRoutePlan.cappedByDistance)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.DistanceLimited", NamedArgument.op_Implicit(num))), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.CautionInput, false);
			}
			else if (removeFloorRoutePlan.cappedByAmount)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Capped", NamedArgument.op_Implicit(num))), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.CautionInput, false);
			}
			else if (num < count)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Partial", NamedArgument.op_Implicit(num), NamedArgument.op_Implicit(count - num))), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.CautionInput, false);
			}
			else
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Planned", NamedArgument.op_Implicit(num))), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.SilentInput, false);
			}
		}
	}

	private static int QueueReplace(Pawn pawn, WorkGiver_ConstructRemoveFloor scanner, List<Job> jobs, List<IntVec3> cells)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		pawn.jobs.ClearQueuedJobs(true);
		int num = 0;
		bool flag = false;
		for (int i = 0; i < jobs.Count; i++)
		{
			Job val = jobs[i];
			val.workGiverDef = ((WorkGiver)scanner).def;
			if (!flag)
			{
				if (pawn.jobs.TryTakeOrderedJobPrioritizedWork(val, (WorkGiver)(object)scanner, cells[i]))
				{
					flag = true;
					num++;
				}
				continue;
			}
			val.playerForced = true;
			if (val.TryMakePreToilReservations(pawn, false))
			{
				pawn.jobs.jobQueue.EnqueueLast(val, (JobTag?)((WorkGiver)scanner).def.tagToGive);
				num++;
			}
		}
		return num;
	}

	private static int QueueAppend(Pawn pawn, WorkGiver_ConstructRemoveFloor scanner, List<Job> jobs)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		for (int i = 0; i < jobs.Count; i++)
		{
			Job val = jobs[i];
			val.workGiverDef = ((WorkGiver)scanner).def;
			val.playerForced = true;
			if (val.TryMakePreToilReservations(pawn, false))
			{
				pawn.jobs.jobQueue.EnqueueLast(val, (JobTag?)((WorkGiver)scanner).def.tagToGive);
				num++;
			}
		}
		if (num > 0)
		{
			Job curJob = pawn.jobs.curJob;
			if (curJob == null)
			{
				pawn.jobs.CheckForJobOverride(0f, false);
			}
			else if (curJob.def.isIdle)
			{
				pawn.jobs.EndCurrentJob((JobCondition)16, true, true);
			}
		}
		return num;
	}
}
