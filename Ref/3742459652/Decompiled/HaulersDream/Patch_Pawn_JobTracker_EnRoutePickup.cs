using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(Pawn_JobTracker), "TryOpportunisticJob")]
public static class Patch_Pawn_JobTracker_EnRoutePickup
{
	private enum CandidateOutcome
	{
		RangeFail,
		HardFail,
		FullStop,
		Skip
	}

	internal sealed class EnRouteInfo
	{
		internal readonly IntVec3 jobCell;

		internal EnRouteInfo(IntVec3 jobCell)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			this.jobCell = jobCell;
		}
	}

	[ThreadStatic]
	private static List<Thing> scratchHaulables;

	[ThreadStatic]
	private static Dictionary<Thing, IntVec3> scratchStoreCellCache;

	[ThreadStatic]
	private static List<Thing> scratchTracked;

	private const int MaxExpansions = 12;

	private static readonly ConditionalWeakTable<Job, EnRouteInfo> enRouteJobs = new ConditionalWeakTable<Job, EnRouteInfo>();

	private static void Postfix(ref Job __result, Pawn ___pawn, Job job)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && settings.enRoutePickup && MasterEnable.Active && __result == null && job?.def != null && ___pawn != null && job.def.allowOpportunisticPrefix && !job.playerForced)
		{
			Job val = TryEnRouteJob(___pawn, job);
			if (val != null)
			{
				__result = val;
			}
		}
	}

	private static Job TryEnRouteJob(Pawn pawn, Job job)
	{
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		Map map = ((Thing)pawn).Map;
		if (map == null || !((Thing)pawn).Spawned)
		{
			return null;
		}
		if (((Thing)pawn).Faction != Faction.OfPlayerSilentFail || QuestUtility.IsQuestLodger(pawn))
		{
			return null;
		}
		if (!YieldRouter.IsEligible(pawn))
		{
			return null;
		}
		if (CaravanFormingUtility.IsFormingCaravan(pawn))
		{
			return null;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp == null || pawn.inventory == null)
		{
			return null;
		}
		if (!comp.autoHaulYields)
		{
			return null;
		}
		if (!YieldRouter.FitToStartHaul(pawn))
		{
			return null;
		}
		bool flag = ProtectedWork.IsProtected(job, nodeIsEmergency: false);
		if (flag && PawnUnloadChecker.AnyUnloadable(pawn, comp.GetHashSet()))
		{
			if (OpportunisticUnload.ShouldDivert(pawn, job, runOver: false, protectedZeroDetourOnly: true))
			{
				Job val = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_UnloadInventory);
				if (val.TryMakePreToilReservations(pawn, false))
				{
					OpportunisticUnload.NotifyDiverted(pawn);
					return val;
				}
			}
			return null;
		}
		if (!flag && job.def == JobDefOf.DoBill && job.bill != null && PawnUnloadChecker.AnyUnloadable(pawn, comp.GetHashSet()) && !HoldsStockUsableForBill(pawn, comp, job.bill) && OpportunisticUnload.ShouldDivert(pawn, job, runOver: false, protectedZeroDetourOnly: true))
		{
			Job val2 = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_UnloadInventory);
			if (val2.TryMakePreToilReservations(pawn, false))
			{
				OpportunisticUnload.NotifyDiverted(pawn);
				return val2;
			}
		}
		if (AlreadyHaulingIntoInventory(pawn))
		{
			return null;
		}
		bool flag2 = job.def == HaulersDreamDefOf.HaulersDream_UnloadInventory && settings.opportunisticDetour != OpportunisticDetour.Off;
		LocalTargetInfo val4;
		if (flag2)
		{
			IntVec3 val3 = FirstTrackedStoreCell(pawn, comp);
			if (!((IntVec3)(ref val3)).IsValid)
			{
				return null;
			}
			val4 = LocalTargetInfo.op_Implicit(val3);
		}
		else
		{
			val4 = ResolveJobTarget(job);
		}
		IntVec3 cell = ((LocalTargetInfo)(ref val4)).Cell;
		if (!((IntVec3)(ref cell)).IsValid || ForbidUtility.IsForbidden(cell, pawn))
		{
			return null;
		}
		if (IntVec3Utility.DistanceTo(((Thing)pawn).Position, cell) < 3f)
		{
			return null;
		}
		if (!flag2 && MustStandDownForUnload(pawn, comp, settings))
		{
			return null;
		}
		List<Thing> list = scratchHaulables ?? (scratchHaulables = new List<Thing>());
		list.Clear();
		ICollection<Thing> collection = map.listerHaulables.ThingsPotentiallyNeedingHauling();
		if (collection is HashSet<Thing> collection2)
		{
			list.AddRange(collection2);
		}
		else
		{
			list.AddRange(collection);
		}
		if (list.Count == 0)
		{
			return null;
		}
		list.Sort((Thing a, Thing b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
		Dictionary<Thing, IntVec3> dictionary = scratchStoreCellCache ?? (scratchStoreCellCache = new Dictionary<Thing, IntVec3>());
		dictionary.Clear();
		HashSet<Thing> claimed = RouteSelection.ClaimedByOtherPawns(pawn);
		EnRoutePickupPolicy.MaxRanges ranges = default(EnRoutePickupPolicy.MaxRanges);
		ranges.Reset();
		int zeroDetourBudget = (flag2 ? OpportunisticUnloadPolicy.DetourBudgetTiles(settings.opportunisticDetour) : (-1));
		float pawnToJob = IntVec3Utility.DistanceTo(((Thing)pawn).Position, cell);
		try
		{
			int num = 0;
			while (list.Count > 0)
			{
				if (num == list.Count)
				{
					ranges.Expand();
					num = 0;
					if (ranges.ExpandCount > 12)
					{
						return null;
					}
					continue;
				}
				Thing thing = list[num];
				Job picked;
				CandidateOutcome candidateOutcome = TryCandidate(pawn, thing, cell, pawnToJob, in ranges, claimed, comp, dictionary, map, settings, zeroDetourBudget, out picked);
				if (picked != null)
				{
					return picked;
				}
				switch (candidateOutcome)
				{
				case CandidateOutcome.RangeFail:
					if (settings.enRoutePathChecker == EnRoutePathChecker.Vanilla)
					{
						list.RemoveAt(num);
					}
					else
					{
						num++;
					}
					break;
				case CandidateOutcome.HardFail:
					list.RemoveAt(num);
					break;
				case CandidateOutcome.FullStop:
					return null;
				default:
					num++;
					break;
				}
			}
			return null;
		}
		finally
		{
			list.Clear();
			dictionary.Clear();
		}
	}

	private static bool HoldsStockUsableForBill(Pawn pawn, CompHauledToInventory comp, Bill bill)
	{
		if (bill?.recipe == null || comp == null)
		{
			return false;
		}
		ThingOwner<Thing> val = pawn.inventory?.innerContainer;
		if (val == null)
		{
			return false;
		}
		foreach (Thing item in comp.GetHashSet())
		{
			if (item != null && ((ThingOwner)val).Contains(item) && InventoryShare.IsUsableForBill(item, bill))
			{
				return true;
			}
		}
		return false;
	}

	private static CandidateOutcome TryCandidate(Pawn pawn, Thing thing, IntVec3 jobCell, float pawnToJob, in EnRoutePickupPolicy.MaxRanges ranges, HashSet<Thing> claimed, CompHauledToInventory comp, Dictionary<Thing, IntVec3> storeCellCache, Map map, HaulersDreamSettings s, int zeroDetourBudget, out Job picked)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		picked = null;
		if (thing == null || !thing.Spawned || thing.Map != map || (object)thing == ((ThingComp)comp).parent)
		{
			return CandidateOutcome.HardFail;
		}
		if (thing is Corpse || thing.def == null || !thing.def.EverHaulable)
		{
			return CandidateOutcome.HardFail;
		}
		float pawnToThing = IntVec3Utility.DistanceTo(((Thing)pawn).Position, thing.Position);
		float num = IntVec3Utility.DistanceTo(thing.Position, jobCell);
		if (zeroDetourBudget >= 0 && !OpportunisticUnloadPolicy.ShouldGrabOnWay(pawnToJob, pawnToThing, num, zeroDetourBudget))
		{
			return CandidateOutcome.HardFail;
		}
		switch (EnRoutePickupPolicy.CheckBeforeStore(pawnToThing, pawnToJob, num, in ranges))
		{
		case EnRoutePickupPolicy.EnRouteResult.RangeFail:
			return CandidateOutcome.RangeFail;
		case EnRoutePickupPolicy.EnRouteResult.HardFail:
			return CandidateOutcome.HardFail;
		default:
		{
			if (claimed.Contains(thing) || OwnJobTargets(pawn, thing))
			{
				return CandidateOutcome.HardFail;
			}
			if (map.reservationManager.FirstRespectedReserver(LocalTargetInfo.op_Implicit(thing), pawn, (ReservationLayerDef)null) != null)
			{
				return CandidateOutcome.HardFail;
			}
			if (ForbidUtility.IsForbidden(thing, pawn))
			{
				return CandidateOutcome.HardFail;
			}
			if (HaulChurnGuard.IsBackedOff(thing))
			{
				return CandidateOutcome.HardFail;
			}
			if (RimIOTCompat.IsPresent && RimIOTCompat.IsRimIOTHandledCell(map, thing.Position))
			{
				return CandidateOutcome.HardFail;
			}
			if (CorpseStripper.ShouldLeaveTaintedApparel(thing, s))
			{
				return CandidateOutcome.HardFail;
			}
			if (ForeignOrderGuard.ClaimedByForeignOrder(thing))
			{
				return CandidateOutcome.HardFail;
			}
			if (!HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, false))
			{
				return CandidateOutcome.HardFail;
			}
			if (!ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(thing), (PathEndMode)3))
			{
				return CandidateOutcome.HardFail;
			}
			if (!storeCellCache.TryGetValue(thing, out var value))
			{
				value = (storeCellCache[thing] = FindMidwayStoreCell(pawn, thing, jobCell, map));
			}
			if (!((IntVec3)(ref value)).IsValid)
			{
				return CandidateOutcome.HardFail;
			}
			float thingToStore = IntVec3Utility.DistanceTo(thing.Position, value);
			float storeToJob = IntVec3Utility.DistanceTo(value, jobCell);
			switch (EnRoutePickupPolicy.CheckAfterStore(pawnToThing, pawnToJob, thingToStore, storeToJob, in ranges))
			{
			case EnRoutePickupPolicy.EnRouteResult.RangeFail:
				return CandidateOutcome.RangeFail;
			case EnRoutePickupPolicy.EnRouteResult.HardFail:
				return CandidateOutcome.HardFail;
			default:
				switch (s.enRoutePathChecker)
				{
				case EnRoutePathChecker.Vanilla:
					if (!WithinRegionCount(pawn, thing, value, jobCell, map))
					{
						return CandidateOutcome.HardFail;
					}
					break;
				case EnRoutePathChecker.Pathfinding:
					if (!WithinPathCost(pawn, thing, value, jobCell, map))
					{
						return CandidateOutcome.HardFail;
					}
					break;
				case EnRoutePathChecker.Default:
					if (!WithinPathCost(pawn, thing, value, jobCell, map))
					{
						return CandidateOutcome.FullStop;
					}
					break;
				}
				picked = BuildEnRouteJob(pawn, thing, jobCell, s);
				if (picked == null)
				{
					return CandidateOutcome.HardFail;
				}
				return CandidateOutcome.Skip;
			}
		}
		}
	}

	private static Job BuildEnRouteJob(Pawn pawn, Thing thing, IntVec3 jobCell, HaulersDreamSettings s)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		int num = OverloadGate.CountToPickUp(pawn, thing, s);
		if (num <= 0)
		{
			return null;
		}
		Job obj = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_BulkHaul, LocalTargetInfo.op_Implicit(thing));
		obj.targetQueueB = new List<LocalTargetInfo> { LocalTargetInfo.op_Implicit(thing) };
		obj.countQueue = new List<int> { num };
		obj.count = 1;
		MarkEnRoute(obj, jobCell);
		HDLog.Dbg($"En-route: {pawn} grabs x{num} {((Def)(thing.def?)).label} on the way to its job.");
		return obj;
	}

	private static void MarkEnRoute(Job job, IntVec3 jobCell)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		if (job != null)
		{
			enRouteJobs.GetValue(job, (Job _) => new EnRouteInfo(jobCell));
		}
	}

	internal static bool IsEnRoute(Job job)
	{
		EnRouteInfo value;
		if (job != null)
		{
			return enRouteJobs.TryGetValue(job, out value);
		}
		return false;
	}

	internal static EnRouteInfo EnRouteData(Job job)
	{
		if (job == null || !enRouteJobs.TryGetValue(job, out var value))
		{
			return null;
		}
		return value;
	}

	private static bool AlreadyHaulingIntoInventory(Pawn pawn)
	{
		JobDef curJobDef = pawn.CurJobDef;
		if (curJobDef != null && HdJobDefSets.NoRecursionHaulJobs.Contains(curJobDef))
		{
			return true;
		}
		JobQueue val = pawn.jobs?.jobQueue;
		if (val != null)
		{
			for (int i = 0; i < val.Count; i++)
			{
				JobDef val2 = val[i]?.job?.def;
				if (val2 != null && HdJobDefSets.NoRecursionHaulJobs.Contains(val2))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool OwnJobTargets(Pawn pawn, Thing thing)
	{
		if (JobTargetsThing(pawn.CurJob, thing))
		{
			return true;
		}
		JobQueue val = pawn.jobs?.jobQueue;
		if (val != null)
		{
			for (int i = 0; i < val.Count; i++)
			{
				if (JobTargetsThing(val[i]?.job, thing))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool JobTargetsThing(Job j, Thing thing)
	{
		if (j == null)
		{
			return false;
		}
		if (((LocalTargetInfo)(ref j.targetA)).Thing == thing || ((LocalTargetInfo)(ref j.targetB)).Thing == thing || ((LocalTargetInfo)(ref j.targetC)).Thing == thing)
		{
			return true;
		}
		if (QueueHas(j.targetQueueA, thing) || QueueHas(j.targetQueueB, thing))
		{
			return true;
		}
		return false;
	}

	private static bool QueueHas(List<LocalTargetInfo> q, Thing thing)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		if (q == null)
		{
			return false;
		}
		for (int i = 0; i < q.Count; i++)
		{
			LocalTargetInfo val = q[i];
			if (((LocalTargetInfo)(ref val)).Thing == thing)
			{
				return true;
			}
		}
		return false;
	}

	private static bool MustStandDownForUnload(Pawn pawn, CompHauledToInventory comp, HaulersDreamSettings s)
	{
		bool flag = s.opportunisticUnload && s.markForUnload;
		if (!flag)
		{
			return false;
		}
		float num = PawnMassCache.Capacity(pawn);
		if (num <= 0f)
		{
			return false;
		}
		float num2 = TrackedMassCache.TrackedMass(pawn, comp) / num;
		TickManager tickManager = Find.TickManager;
		bool flag2 = ((tickManager != null) ? tickManager.TicksGame : 0) - comp.lastOpportunisticUnloadTick >= 250;
		if (!flag2 || num2 < 0.15f)
		{
			return false;
		}
		bool hasStorableCell = HasStorableTrackedCell(pawn, comp);
		return EnRouteMutexPolicy.MustStandDown(flag, num2, hasStorableCell, flag2);
	}

	private static bool HasStorableTrackedCell(Pawn pawn, CompHauledToInventory comp)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 val = FirstTrackedStoreCell(pawn, comp);
		return ((IntVec3)(ref val)).IsValid;
	}

	private static IntVec3 FirstTrackedStoreCell(Pawn pawn, CompHauledToInventory comp)
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		List<Thing> list = scratchTracked ?? (scratchTracked = new List<Thing>());
		list.Clear();
		foreach (Thing item in comp.PeekHashSet())
		{
			if (item != null && !item.Destroyed)
			{
				list.Add(item);
			}
		}
		list.Sort((Thing a, Thing b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
		IntVec3 result = IntVec3.Invalid;
		IntVec3 val = default(IntVec3);
		for (int num = 0; num < list.Count; num++)
		{
			StoreUtility.TryFindBestBetterStoreCellFor(list[num], pawn, ((Thing)pawn).Map, (StoragePriority)0, ((Thing)pawn).Faction, ref val, false);
			if (((IntVec3)(ref val)).IsValid)
			{
				result = val;
				break;
			}
		}
		list.Clear();
		return result;
	}

	private static LocalTargetInfo ResolveJobTarget(Job job)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		LocalTargetInfo val;
		IntVec3 cell;
		if (job.def == JobDefOf.DoBill && job.targetQueueB != null)
		{
			for (int i = 0; i < job.targetQueueB.Count; i++)
			{
				val = job.targetQueueB[i];
				cell = ((LocalTargetInfo)(ref val)).Cell;
				if (((IntVec3)(ref cell)).IsValid)
				{
					return job.targetQueueB[i];
				}
			}
		}
		cell = ((LocalTargetInfo)(ref job.targetA)).Cell;
		if (((IntVec3)(ref cell)).IsValid)
		{
			return job.targetA;
		}
		List<LocalTargetInfo> targetQueueA = job.targetQueueA;
		if (targetQueueA != null)
		{
			for (int j = 0; j < targetQueueA.Count; j++)
			{
				val = targetQueueA[j];
				cell = ((LocalTargetInfo)(ref val)).Cell;
				if (((IntVec3)(ref cell)).IsValid)
				{
					return targetQueueA[j];
				}
			}
		}
		return job.targetA;
	}

	private static IntVec3 FindMidwayStoreCell(Pawn pawn, Thing thing, IntVec3 jobCell, Map map)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		StoragePriority val = StoreUtility.CurrentStoragePriorityOf(thing, false);
		EnRoutePickupPolicy.Midway(thing.Position.x, thing.Position.y, thing.Position.z, jobCell.x, jobCell.y, jobCell.z, out var midX, out var _, out var midZ);
		using (StorageBuildingFilter.PushContext(StorageFilterContext.Opportunistic))
		{
			StorageBuildingFilter storageBuildingFilter = ((!StorageBuildingFilter.Enabled || StorageBuildingFilter.CurrentContext == StorageFilterContext.Unload) ? null : HaulersDreamMod.Settings?.storageBuildingFilter);
			IntVec3 result = IntVec3.Invalid;
			int bestDistSquared = int.MaxValue;
			List<SlotGroup> allGroupsListInPriorityOrder = map.haulDestinationManager.AllGroupsListInPriorityOrder;
			for (int i = 0; i < allGroupsListInPriorityOrder.Count; i++)
			{
				SlotGroup val2 = allGroupsListInPriorityOrder[i];
				if (((val2 != null) ? val2.Settings : null) == null)
				{
					continue;
				}
				StoragePriority priority = val2.Settings.Priority;
				if (priority <= val || (int)priority == 0 || !((IHaulDestination)val2.parent).Accepts(thing) || (storageBuildingFilter != null && !storageBuildingFilter.IsGroupAllowed(val2)))
				{
					continue;
				}
				List<IntVec3> cellsList = val2.CellsList;
				if (cellsList == null)
				{
					continue;
				}
				for (int j = 0; j < cellsList.Count; j++)
				{
					IntVec3 val3 = cellsList[j];
					int num = EnRoutePickupPolicy.MidwayDistanceSquared(val3.x, val3.z, midX, midZ);
					if (EnRoutePickupPolicy.MidwayBetter(num, bestDistSquared) && StoreUtility.IsGoodStoreCell(val3, map, thing, pawn, ((Thing)pawn).Faction) && (storageBuildingFilter == null || storageBuildingFilter.IsCellAllowed(val3, map)))
					{
						result = val3;
						bestDistSquared = num;
					}
				}
			}
			return result;
		}
	}

	private static bool WithinRegionCount(Pawn pawn, Thing thing, IntVec3 storeCell, IntVec3 jobCell, Map map)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		TraverseParms val = TraverseParms.For(pawn, (Danger)3, (TraverseMode)0, false, false, false, true);
		if (!RegionTraverser.WithinRegions(((Thing)pawn).Position, thing.Position, map, 25, val, (RegionType)14))
		{
			return false;
		}
		if (!RegionTraverser.WithinRegions(storeCell, jobCell, map, 25, val, (RegionType)14))
		{
			return false;
		}
		return true;
	}

	private static bool WithinPathCost(Pawn pawn, Thing thing, IntVec3 storeCell, IntVec3 jobCell, Map map)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		float num = PathCost(map, pawn, ((Thing)pawn).Position, thing, (PathEndMode)3);
		if (num <= 0f)
		{
			return false;
		}
		float num2 = PathCost(map, pawn, storeCell, jobCell, (PathEndMode)2);
		if (num2 <= 0f)
		{
			return false;
		}
		float num3 = PathCost(map, pawn, ((Thing)pawn).Position, jobCell, (PathEndMode)2);
		if (num3 <= 0f)
		{
			return false;
		}
		if (num + num2 > num3 * 1f)
		{
			return false;
		}
		float num4 = PathCost(map, pawn, thing.Position, storeCell, (PathEndMode)3);
		if (num4 <= 0f)
		{
			return false;
		}
		return EnRoutePickupPolicy.WithinPathLegBounds(num, num4, num2, num3);
	}

	private static float PathCost(Map map, Pawn pawn, IntVec3 start, Thing destThing, PathEndMode peMode)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		PawnPath val = map.pathFinder.FindPathNow(start, LocalTargetInfo.op_Implicit(destThing), pawn, (PathFinderCostTuning?)null, peMode);
		try
		{
			return (val != null && val.Found) ? val.TotalCost : 0f;
		}
		finally
		{
			if (val != null)
			{
				val.ReleaseToPool();
			}
		}
	}

	private static float PathCost(Map map, Pawn pawn, IntVec3 start, IntVec3 destCell, PathEndMode peMode)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		PawnPath val = map.pathFinder.FindPathNow(start, LocalTargetInfo.op_Implicit(destCell), pawn, (PathFinderCostTuning?)null, peMode);
		try
		{
			return (val != null && val.Found) ? val.TotalCost : 0f;
		}
		finally
		{
			if (val != null)
			{
				val.ReleaseToPool();
			}
		}
	}
}
