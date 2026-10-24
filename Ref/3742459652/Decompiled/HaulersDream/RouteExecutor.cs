using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class RouteExecutor
{
	private sealed class RouteJob
	{
		public Job job;

		public IntVec3 cell;

		public Thing stop;
	}

	public static void ExecuteRouteSynced(Pawn pawn, Thing clicked, string workGiverDefName, RouteMode mode, int amount, int radius, float maxDistance, bool smart, bool allowHarvest, int growthThreshold, bool replace, List<Thing> mustInclude, RouteSelectionMethod selectionMethod, RouteDistanceBasis distanceBasis, int exactMax, Thing startNode, Thing endNode, bool alsoBuild, List<IntVec3> roomAnchors, List<ThingDef> extraDefs)
	{
		RouteWorkKind routeWorkKind = WorkKindResolver.ResolveById(pawn, clicked, workGiverDefName);
		if (routeWorkKind != null)
		{
			Execute(pawn, clicked, routeWorkKind, mode, amount, radius, maxDistance, smart, allowHarvest, growthThreshold, replace, null, mustInclude, selectionMethod, distanceBasis, exactMax, startNode, endNode, alsoBuild, roomAnchors, extraDefs);
		}
	}

	public static void Execute(Pawn pawn, Thing clicked, RouteWorkKind kind, RouteMode mode, int amount, int radius, float maxDistance, bool smart, bool allowHarvest, int growthThreshold, bool replace, RoutePlan precomputed = null, IReadOnlyList<Thing> mustInclude = null, RouteSelectionMethod selectionMethod = RouteSelectionMethod.MostStopsPerTravel, RouteDistanceBasis distanceBasis = RouteDistanceBasis.StraightLine, int exactMax = 12, Thing startNode = null, Thing endNode = null, bool alsoBuild = false, IReadOnlyList<IntVec3> roomAnchors = null, IReadOnlyList<ThingDef> extraDefs = null)
	{
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_032a: Unknown result type (might be due to invalid IL or missing references)
		//IL_044b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0475: Unknown result type (might be due to invalid IL or missing references)
		//IL_047a: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_04fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0503: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04dc: Unknown result type (might be due to invalid IL or missing references)
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || clicked == null || kind?.scanner == null)
		{
			return;
		}
		RoutePlan routePlan = ((precomputed != null && precomputed.stops.Count > 0) ? precomputed : RoutePlanner.Plan(pawn, clicked, kind, mode, amount, radius, maxDistance, smart, allowHarvest, growthThreshold, mustInclude, selectionMethod, distanceBasis, exactMax, startNode, endNode, roomAnchors, extraDefs));
		if (routePlan.stops.Count == 0)
		{
			if (MultiplayerCompat.ShouldShowLocalFeedback)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.NoTargets")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
			}
			return;
		}
		int count = routePlan.stops.Count;
		for (int i = 0; i < routePlan.stops.Count; i++)
		{
			if (routePlan.stops[i] != null && routePlan.stops[i].Spawned)
			{
				EnsureDesignated(((Thing)pawn).Map, routePlan.stops[i], kind);
			}
		}
		List<RouteJob> list = new List<RouteJob>(routePlan.stops.Count);
		try
		{
			InventoryConstructDelivery.RouteIntent = ((!alsoBuild) ? ConstructRouteIntent.HaulOnly : ConstructRouteIntent.HaulBuild);
			if (alsoBuild)
			{
				Dictionary<ThingDef, int> dictionary = new Dictionary<ThingDef, int>();
				for (int j = 0; j < routePlan.stops.Count; j++)
				{
					Thing obj = routePlan.stops[j];
					IConstructible val = (IConstructible)(object)((obj is IConstructible) ? obj : null);
					if (val == null)
					{
						continue;
					}
					List<ThingDefCountClass> list2 = val.TotalMaterialCost();
					if (list2 == null)
					{
						continue;
					}
					for (int k = 0; k < list2.Count; k++)
					{
						ThingDef val2 = list2[k]?.thingDef;
						if (val2 != null)
						{
							int num = val.ThingCountNeeded(val2);
							if (num > 0)
							{
								dictionary.TryGetValue(val2, out var value);
								dictionary[val2] = value + num;
							}
						}
					}
				}
				InventoryConstructDelivery.RouteDemandByDef = dictionary;
			}
			for (int l = 0; l < routePlan.stops.Count; l++)
			{
				Thing val3 = routePlan.stops[l];
				if (val3 != null && val3.Spawned && !StopLostDesignation(((Thing)pawn).Map, val3, kind))
				{
					Job val4 = BuildJobForStop(pawn, val3, kind);
					if (val4 != null)
					{
						list.Add(new RouteJob
						{
							job = val4,
							cell = val3.Position,
							stop = val3
						});
					}
				}
			}
		}
		finally
		{
			InventoryConstructDelivery.RouteIntent = ConstructRouteIntent.None;
			InventoryConstructDelivery.RouteDemandByDef = null;
		}
		if (alsoBuild)
		{
			Dictionary<ThingDef, int> dictionary2 = new Dictionary<ThingDef, int>();
			for (int num2 = list.Count - 1; num2 >= 0; num2--)
			{
				RouteJob routeJob = list[num2];
				ThingDef val5 = ((LocalTargetInfo)(ref routeJob.job.targetA)).Thing?.def;
				if (val5 != null)
				{
					Thing stop = routeJob.stop;
					IConstructible val6 = (IConstructible)(object)((stop is IConstructible) ? stop : null);
					if (val6 != null)
					{
						dictionary2.TryGetValue(val5, out var value2);
						value2 = (dictionary2[val5] = value2 + Math.Max(0, val6.ThingCountNeeded(val5)));
						if (routeJob.job.def == HaulersDreamDefOf.HaulersDream_ConstructDeliverBuild)
						{
							routeJob.job.count = value2;
						}
					}
				}
			}
		}
		if (list.Count == 0)
		{
			if (MultiplayerCompat.ShouldShowLocalFeedback)
			{
				if (kind.scanner is WorkGiver_ConstructDeliverResourcesToBlueprints)
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.WaitingForMaterials")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.CautionInput, false);
				}
				else
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.NoTargets")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
				}
			}
			return;
		}
		int num4 = (replace ? QueueReplace(pawn, kind, list) : QueueAppend(pawn, kind, list));
		HDLog.Dbg(string.Format("{0} planned a {1} route ({2}): {3}/{4} stop(s) ", pawn, mode, replace ? "replace" : "append", num4, count) + $"({kind.gerund}), smart={smart}, ~{RouteEstimateHours(routePlan)}h, cappedAmount={routePlan.cappedByAmount}, cappedDist={routePlan.cappedByDistance}.");
		if (num4 > 0 && mode == RouteMode.Vein && routePlan.fogCaution && kind.designation == DesignationDefOf.Mine)
		{
			RegisterVeinTracker(pawn, clicked, amount, routePlan);
		}
		if (MultiplayerCompat.ShouldShowLocalFeedback)
		{
			if (num4 == 0)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.NoTargets")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
			}
			else if (routePlan.cappedByDistance)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.DistanceLimited", NamedArgument.op_Implicit(num4))), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.CautionInput, false);
			}
			else if (routePlan.cappedByAmount)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Capped", NamedArgument.op_Implicit(num4))), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.CautionInput, false);
			}
			else if (num4 < count)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Partial", NamedArgument.op_Implicit(num4), NamedArgument.op_Implicit(count - num4))), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.CautionInput, false);
			}
			else
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Planned", NamedArgument.op_Implicit(num4))), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.SilentInput, false);
			}
		}
	}

	public static Job BuildJobForStop(Pawn pawn, Thing t, RouteWorkKind kind)
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || t == null || kind?.scanner == null)
		{
			return null;
		}
		Job val = kind.scanner.JobOnThing(pawn, t, true);
		if (val != null)
		{
			return val;
		}
		if (kind.scanner is WorkGiver_ConstructDeliverResourcesToBlueprints)
		{
			Frame val2 = (Frame)(object)((t is Frame) ? t : null);
			if (val2 != null)
			{
				if (val2.IsCompleted() && InventoryConstructDelivery.RouteIntent == ConstructRouteIntent.HaulBuild && !WorkCapabilityProbe.IsDisabled(pawn, WorkTypeDefOf.Construction) && GenConstruct.CanConstruct((Thing)(object)val2, pawn, true, true, (JobDef)null))
				{
					Job obj = JobMaker.MakeJob(JobDefOf.FinishFrame, LocalTargetInfo.op_Implicit((Thing)(object)val2));
					obj.playerForced = true;
					return obj;
				}
				foreach (WorkGiver_Scanner item in ConstructTether.DeliverScanners())
				{
					if (item != kind.scanner)
					{
						val = item.JobOnThing(pawn, t, true);
						if (val != null)
						{
							return val;
						}
					}
				}
			}
			return TryDeliverFromOwnStock(pawn, t);
		}
		return null;
	}

	private static Job TryDeliverFromOwnStock(Pawn pawn, Thing blueprint)
	{
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		IConstructible val = (IConstructible)(object)((blueprint is IConstructible) ? blueprint : null);
		if (val == null)
		{
			return null;
		}
		Pawn_CarryTracker carryTracker = pawn.carryTracker;
		Thing val2 = ((carryTracker != null) ? carryTracker.CarriedThing : null);
		ThingOwner<Thing> val3 = pawn.inventory?.innerContainer;
		foreach (ThingDefCountClass item in val.TotalMaterialCost())
		{
			int num = val.ThingCountNeeded(item.thingDef);
			if (num <= 0)
			{
				continue;
			}
			Thing val4 = null;
			if (val2 != null && val2.def == item.thingDef && val2.stackCount > 0)
			{
				val4 = val2;
			}
			else if (val3 != null)
			{
				for (int i = 0; i < ((ThingOwner)val3).Count; i++)
				{
					Thing val5 = val3[i];
					if (val5 != null && val5.def == item.thingDef && val5.stackCount > 0)
					{
						val4 = val5;
						break;
					}
				}
			}
			if (val4 != null)
			{
				Job obj = JobMaker.MakeJob(JobDefOf.HaulToContainer);
				obj.targetA = LocalTargetInfo.op_Implicit(val4);
				obj.targetB = LocalTargetInfo.op_Implicit(blueprint);
				obj.targetC = LocalTargetInfo.op_Implicit(blueprint);
				obj.count = ((num < val4.stackCount) ? num : val4.stackCount);
				obj.haulMode = (HaulMode)3;
				return obj;
			}
		}
		return null;
	}

	private static int QueueReplace(Pawn pawn, RouteWorkKind kind, List<RouteJob> jobs)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		pawn.jobs.ClearQueuedJobs(true);
		int num = 0;
		bool flag = false;
		for (int i = 0; i < jobs.Count; i++)
		{
			RouteJob routeJob = jobs[i];
			routeJob.job.workGiverDef = ((WorkGiver)kind.scanner).def;
			if (!flag)
			{
				if (pawn.jobs.TryTakeOrderedJobPrioritizedWork(routeJob.job, (WorkGiver)(object)kind.scanner, routeJob.cell))
				{
					flag = true;
					num++;
				}
				continue;
			}
			routeJob.job.playerForced = true;
			if (routeJob.job.TryMakePreToilReservations(pawn, false))
			{
				pawn.jobs.jobQueue.EnqueueLast(routeJob.job, (JobTag?)((WorkGiver)kind.scanner).def.tagToGive);
				num++;
			}
		}
		return num;
	}

	private static int QueueAppend(Pawn pawn, RouteWorkKind kind, List<RouteJob> jobs)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		for (int i = 0; i < jobs.Count; i++)
		{
			RouteJob routeJob = jobs[i];
			routeJob.job.workGiverDef = ((WorkGiver)kind.scanner).def;
			routeJob.job.playerForced = true;
			if (routeJob.job.TryMakePreToilReservations(pawn, false))
			{
				pawn.jobs.jobQueue.EnqueueLast(routeJob.job, (JobTag?)((WorkGiver)kind.scanner).def.tagToGive);
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

	private static void RegisterVeinTracker(Pawn pawn, Thing clicked, int amount, RoutePlan plan)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		Game game = Current.Game;
		HaulersDreamGameComponent haulersDreamGameComponent = ((game != null) ? game.GetComponent<HaulersDreamGameComponent>() : null);
		if (haulersDreamGameComponent == null || clicked?.def == null || !TryGetQueueTailCell(pawn, out var cell, out var _))
		{
			return;
		}
		int num = RouteSelection.EffectiveAmount(amount);
		VeinRevealTracker veinRevealTracker = new VeinRevealTracker
		{
			pawn = pawn,
			veinDef = clicked.def,
			seed = clicked.Position,
			cap = num,
			lastCell = cell
		};
		for (int i = 0; i < plan.stops.Count; i++)
		{
			if (plan.stops[i] != null)
			{
				veinRevealTracker.included.Add(plan.stops[i].Position);
			}
		}
		if (veinRevealTracker.included.Count < num)
		{
			haulersDreamGameComponent.RegisterVeinTracker(veinRevealTracker);
		}
	}

	public static int AppendStops(Pawn pawn, RouteWorkKind kind, List<Thing> stops, out IntVec3 lastCell)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		lastCell = IntVec3.Invalid;
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || kind?.scanner == null || stops == null || stops.Count == 0)
		{
			return 0;
		}
		for (int i = 0; i < stops.Count; i++)
		{
			if (stops[i] != null && stops[i].Spawned)
			{
				EnsureDesignated(((Thing)pawn).Map, stops[i], kind);
			}
		}
		int num = 0;
		for (int j = 0; j < stops.Count; j++)
		{
			Thing val = stops[j];
			if (val == null || !val.Spawned || StopLostDesignation(((Thing)pawn).Map, val, kind))
			{
				continue;
			}
			Job val2 = kind.scanner.JobOnThing(pawn, val, true);
			if (val2 != null)
			{
				val2.workGiverDef = ((WorkGiver)kind.scanner).def;
				val2.playerForced = true;
				if (val2.TryMakePreToilReservations(pawn, false))
				{
					pawn.jobs.jobQueue.EnqueueLast(val2, (JobTag?)((WorkGiver)kind.scanner).def.tagToGive);
					lastCell = (((LocalTargetInfo)(ref val2.targetA)).IsValid ? ((LocalTargetInfo)(ref val2.targetA)).Cell : val.Position);
					num++;
				}
			}
		}
		return num;
	}

	public static bool TryGetQueueTailCell(Pawn pawn, out IntVec3 cell, out Job tailJob)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		cell = IntVec3.Invalid;
		tailJob = null;
		Pawn_JobTracker val = pawn?.jobs;
		if (val == null)
		{
			return false;
		}
		Job val2 = null;
		JobQueue jobQueue = val.jobQueue;
		if (jobQueue != null)
		{
			for (int num = jobQueue.Count - 1; num >= 0; num--)
			{
				Job val3 = jobQueue[num]?.job;
				if (val3 != null && val3.def != HaulersDreamDefOf.HaulersDream_SelfPickup && val3.def != HaulersDreamDefOf.HaulersDream_UnloadInventory)
				{
					val2 = val3;
					break;
				}
			}
		}
		if (val2 == null)
		{
			val2 = val.curJob;
		}
		if (val2 == null || !((LocalTargetInfo)(ref val2.targetA)).IsValid)
		{
			return false;
		}
		cell = ((LocalTargetInfo)(ref val2.targetA)).Cell;
		tailJob = val2;
		return true;
	}

	private static string RouteEstimateHours(RoutePlan plan)
	{
		return RouteEstimate.HoursFromTicks(plan.totalTicks).ToString("0.0");
	}

	private static bool StopLostDesignation(Map map, Thing t, RouteWorkKind kind)
	{
		if (kind.scope == RouteTargetScope.DesignatedOnly && kind.designation != null)
		{
			return map.designationManager.DesignationOn(t, kind.designation) == null;
		}
		return false;
	}

	private static void EnsureDesignated(Map map, Thing t, RouteWorkKind kind)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Expected O, but got Unknown
		if (kind.designation == null || kind.scope == RouteTargetScope.DesignatedOnly)
		{
			return;
		}
		DesignationManager designationManager = map.designationManager;
		if (kind.designation == DesignationDefOf.Mine || kind.designation == DesignationDefOf.MineVein)
		{
			if (designationManager.DesignationAt(t.Position, DesignationDefOf.Mine) == null && designationManager.DesignationAt(t.Position, DesignationDefOf.MineVein) == null)
			{
				designationManager.AddDesignation(new Designation(LocalTargetInfo.op_Implicit(t.Position), DesignationDefOf.Mine, (ColorDef)null));
			}
		}
		else if (designationManager.DesignationOn(t, kind.designation) == null)
		{
			designationManager.AddDesignation(new Designation(LocalTargetInfo.op_Implicit(t), kind.designation, (ColorDef)null));
		}
	}
}
