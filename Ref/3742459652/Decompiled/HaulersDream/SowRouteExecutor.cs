using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class SowRouteExecutor
{
	private static WorkGiver_GrowerSow sowScanner;

	private static bool sowScannerResolved;

	public static WorkGiver_GrowerSow SowScanner()
	{
		if (sowScannerResolved)
		{
			return sowScanner;
		}
		sowScannerResolved = true;
		List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			if (typeof(WorkGiver_GrowerSow).IsAssignableFrom(allDefsListForReading[i].giverClass))
			{
				WorkGiver worker = allDefsListForReading[i].Worker;
				WorkGiver_GrowerSow val = (WorkGiver_GrowerSow)(object)((worker is WorkGiver_GrowerSow) ? worker : null);
				if (val != null)
				{
					sowScanner = val;
					break;
				}
			}
		}
		return sowScanner;
	}

	public static void ExecuteSowRouteSynced(Pawn pawn, IntVec3 anchor, SowRouteMode mode, int amount, int radius, float maxDistance, bool smart, bool replace, List<IntVec3> mustInclude, RouteSelectionMethod selectionMethod, int exactMax)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if (((pawn != null) ? ((Thing)pawn).Map : null) != null && ((IntVec3)(ref anchor)).IsValid && GenGrid.InBounds(anchor, ((Thing)pawn).Map))
		{
			Zone obj = ((Thing)pawn).Map.zoneManager.ZoneAt(anchor);
			Zone_Growing val = (Zone_Growing)(object)((obj is Zone_Growing) ? obj : null);
			if (val != null)
			{
				Execute(pawn, anchor, val, mode, amount, radius, maxDistance, smart, replace, mustInclude, selectionMethod, exactMax);
			}
		}
	}

	public static void Execute(Pawn pawn, IntVec3 anchor, Zone_Growing zone, SowRouteMode mode, int amount, int radius, float maxDistance, bool smart, bool replace, IReadOnlyList<IntVec3> mustInclude = null, RouteSelectionMethod selectionMethod = RouteSelectionMethod.MostStopsPerTravel, int exactMax = 12, SowRoutePlan precomputed = null)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02be: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Unknown result type (might be due to invalid IL or missing references)
		//IL_0291: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || zone == null)
		{
			return;
		}
		WorkGiver_GrowerSow val = SowScanner();
		if (val == null)
		{
			return;
		}
		SowRoutePlan sowRoutePlan = ((precomputed != null && precomputed.cells.Count > 0) ? precomputed : SowRoutePlanner.Plan(pawn, anchor, zone, mode, amount, radius, maxDistance, smart, mustInclude, selectionMethod, exactMax));
		if (sowRoutePlan.cells.Count == 0)
		{
			if (MultiplayerCompat.ShouldShowLocalFeedback)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.NoTargets")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
			}
			return;
		}
		int count = sowRoutePlan.cells.Count;
		List<Job> list = new List<Job>(sowRoutePlan.cells.Count);
		List<IntVec3> list2 = new List<IntVec3>(sowRoutePlan.cells.Count);
		for (int i = 0; i < sowRoutePlan.cells.Count; i++)
		{
			IntVec3 val2 = sowRoutePlan.cells[i];
			WorkGiver_Grower.wantedPlantDef = null;
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
		HDLog.Dbg(string.Format("{0} planned a {1} sow route ({2}): {3}/{4} cell(s), ", pawn, mode, replace ? "replace" : "append", num, count) + $"smart={smart}, plant={((Def)(sowRoutePlan.plantDef?)).defName}, cappedAmount={sowRoutePlan.cappedByAmount}, cappedDist={sowRoutePlan.cappedByDistance}.");
		if (MultiplayerCompat.ShouldShowLocalFeedback)
		{
			if (num == 0)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.NoTargets")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
			}
			else if (sowRoutePlan.cappedByDistance)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.DistanceLimited", NamedArgument.op_Implicit(num))), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.CautionInput, false);
			}
			else if (sowRoutePlan.cappedByAmount)
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

	private static int QueueReplace(Pawn pawn, WorkGiver_GrowerSow scanner, List<Job> jobs, List<IntVec3> cells)
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

	private static int QueueAppend(Pawn pawn, WorkGiver_GrowerSow scanner, List<Job> jobs)
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
