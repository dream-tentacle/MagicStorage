using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class OpportunisticUnload
{
	internal const int DivertCooldownTicks = 250;

	[ThreadStatic]
	private static List<Thing> scratchRep;

	private static readonly ConcurrentDictionary<JobDef, WorkRunKind> runKindCache = new ConcurrentDictionary<JobDef, WorkRunKind>();

	internal static bool IsInDowntimeJob(Pawn pawn, HaulersDreamSettings s)
	{
		if (pawn == null || s == null)
		{
			return false;
		}
		if (s.unloadBeforeEating && pawn.CurJobDef == JobDefOf.Ingest)
		{
			return true;
		}
		if (s.unloadBeforeSleep && pawn.jobs?.curDriver is JobDriver_LayDown)
		{
			return true;
		}
		if (s.unloadBeforeLeisure && pawn.CurJobDef?.joyKind != null)
		{
			return true;
		}
		return false;
	}

	internal static bool IsEnteringDowntime(Pawn pawn, HaulersDreamSettings s)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Invalid comparison between Unknown and I4
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Invalid comparison between Unknown and I4
		if (pawn == null || s == null)
		{
			return false;
		}
		Pawn_NeedsTracker needs = pawn.needs;
		if (s.unloadBeforeEating && needs?.food != null && (int)needs.food.CurCategory >= 1)
		{
			return true;
		}
		if (s.unloadBeforeSleep && needs?.rest != null && (int)needs.rest.CurCategory >= 1)
		{
			return true;
		}
		if (s.unloadBeforeLeisure && pawn.timetable != null && pawn.timetable.CurrentAssignment == TimeAssignmentDefOf.Joy)
		{
			return true;
		}
		return IsInDowntimeJob(pawn, s);
	}

	internal static Job TryGetPreDowntimeUnloadJob(Pawn pawn, bool enabled)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (!enabled || settings == null || !settings.markForUnload)
		{
			return null;
		}
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || pawn.jobs == null || pawn.Drafted || pawn.Downed || !((Thing)pawn).Spawned)
		{
			return null;
		}
		if (((Thing)pawn).Faction != Faction.OfPlayerSilentFail || !YieldRouter.IsEligible(pawn))
		{
			return null;
		}
		if (ProtectedWork.IsRestingPatient(pawn) || CaravanFormingUtility.IsFormingCaravan(pawn) || pawn.mindState?.duty != null)
		{
			return null;
		}
		if (pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_UnloadInventory || PawnUnloadChecker.HasQueuedUnload(pawn))
		{
			return null;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp == null)
		{
			return null;
		}
		TickManager tickManager = Find.TickManager;
		if (((tickManager != null) ? tickManager.TicksGame : 0) - comp.lastOpportunisticUnloadTick < 250)
		{
			return null;
		}
		bool unloadAllSurplus = settings.unloadAllSurplus;
		if (!CaravanFormingUtility.IsFormingCaravan(pawn) && (unloadAllSurplus || settings.HasAnySurplusProducingRule))
		{
			PawnUnloadChecker.AdoptSurplusInventory(pawn, comp, unloadAllSurplus);
		}
		if (!MapGate.ShouldUnloadToStorage(((Thing)pawn).Map))
		{
			return PackAnimalLoad.TryGetOpportunisticLoadJob(pawn);
		}
		if (!PawnUnloadChecker.AnyUnloadable(pawn, comp.GetHashSet()))
		{
			return null;
		}
		Job val = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_UnloadInventory);
		if (!val.TryMakePreToilReservations(pawn, false))
		{
			return null;
		}
		NotifyDiverted(pawn);
		HDLog.Dbg($"{pawn} unloading before downtime ({comp.GetHashSet().Count} tracked).");
		return val;
	}

	internal static WorkRunKind ClassifyJobDef(JobDef def)
	{
		if (def == null || def.driverClass == null)
		{
			return WorkRunKind.Other;
		}
		if (runKindCache.TryGetValue(def, out var value))
		{
			return value;
		}
		WorkRunKind workRunKind = WorkRunPolicy.Classify(IsConstructionDriver(def.driverClass), IsYieldDriver(def.driverClass), IsHaulDriver(def.driverClass));
		runKindCache[def] = workRunKind;
		return workRunKind;
	}

	private static bool IsConstructionDriver(Type dc)
	{
		if (!typeof(JobDriver_ConstructFinishFrame).IsAssignableFrom(dc) && !typeof(JobDriver_PlaceNoCostFrame).IsAssignableFrom(dc))
		{
			return typeof(JobDriver_OverloadConstructDeliver).IsAssignableFrom(dc);
		}
		return true;
	}

	private static bool IsYieldDriver(Type dc)
	{
		if (!typeof(JobDriver_PlantWork).IsAssignableFrom(dc) && !typeof(JobDriver_Mine).IsAssignableFrom(dc) && !typeof(JobDriver_OperateDeepDrill).IsAssignableFrom(dc) && !typeof(JobDriver_GatherAnimalBodyResources).IsAssignableFrom(dc) && !typeof(JobDriver_Strip).IsAssignableFrom(dc))
		{
			return typeof(JobDriver_Deconstruct).IsAssignableFrom(dc);
		}
		return true;
	}

	private static bool IsHaulDriver(Type dc)
	{
		if (!typeof(JobDriver_HaulToCell).IsAssignableFrom(dc) && !typeof(JobDriver_HaulToContainer).IsAssignableFrom(dc))
		{
			return typeof(JobDriver_BulkHaul).IsAssignableFrom(dc);
		}
		return true;
	}

	internal static bool ShouldDivert(Pawn pawn, Job workJob, bool runOver = false, bool protectedZeroDetourOnly = false)
	{
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0303: Unknown result type (might be due to invalid IL or missing references)
		//IL_0308: Unknown result type (might be due to invalid IL or missing references)
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_0313: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.markForUnload)
		{
			return false;
		}
		if (!protectedZeroDetourOnly && !settings.opportunisticUnload)
		{
			return false;
		}
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || workJob?.def == null || pawn.Drafted || workJob.playerForced)
		{
			return false;
		}
		if (ProtectedWork.IsRestingPatient(pawn))
		{
			return false;
		}
		if (!protectedZeroDetourOnly && ProtectedWork.IsProtected(workJob, nodeIsEmergency: false))
		{
			return false;
		}
		if (workJob.def == HaulersDreamDefOf.HaulersDream_UnloadInventory || pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_UnloadInventory)
		{
			return false;
		}
		if (pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_BillPrepGather)
		{
			return false;
		}
		if (workJob.def.driverClass != null && workJob.def.driverClass.Assembly == typeof(OpportunisticUnload).Assembly)
		{
			return false;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp == null)
		{
			return false;
		}
		HashSet<Thing> hashSet = comp.GetHashSet();
		if (hashSet.Count == 0)
		{
			return false;
		}
		if (ConstructionMaterialHold.HoldsMaterialForActiveConstruction(pawn, comp))
		{
			return false;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : 0);
		if (num - comp.lastOpportunisticUnloadTick < 250)
		{
			return false;
		}
		if (!PawnUnloadChecker.AnyUnloadable(pawn, hashSet))
		{
			return false;
		}
		if (!protectedZeroDetourOnly && runOver && !IsEnteringDowntime(pawn, settings) && num - comp.lastYieldTick < settings.unloadGraceTicks)
		{
			return false;
		}
		IntVec3 b = ((LocalTargetInfo)(ref workJob.targetA)).Cell;
		LocalTargetInfo val;
		if (protectedZeroDetourOnly)
		{
			List<LocalTargetInfo> targetQueueB = workJob.targetQueueB;
			if (targetQueueB != null && targetQueueB.Count > 0)
			{
				val = targetQueueB[0];
				if (((LocalTargetInfo)(ref val)).HasThing)
				{
					val = targetQueueB[0];
					if (((LocalTargetInfo)(ref val)).Thing.Spawned)
					{
						val = targetQueueB[0];
						b = ((LocalTargetInfo)(ref val)).Thing.Position;
					}
				}
			}
		}
		if (!((IntVec3)(ref b)).IsValid)
		{
			List<LocalTargetInfo> targetQueueA = workJob.targetQueueA;
			if (targetQueueA != null && targetQueueA.Count > 0)
			{
				val = targetQueueA[0];
				b = ((LocalTargetInfo)(ref val)).Cell;
			}
		}
		if (!((IntVec3)(ref b)).IsValid)
		{
			return false;
		}
		float num2 = CarryCapacity.Of(pawn);
		if (num2 <= 0f)
		{
			return false;
		}
		float loadFraction = TrackedMassCache.TrackedMass(pawn, comp) / num2;
		if (!protectedZeroDetourOnly && !OpportunisticUnloadPolicy.ShouldAttemptDivert(loadFraction, num - comp.lastOpportunisticUnloadTick >= 250, hashSet.Count, num2 > 0f))
		{
			return false;
		}
		comp.lastOpportunisticUnloadTick = num;
		IntVec3 invalid = IntVec3.Invalid;
		List<Thing> list = scratchRep ?? (scratchRep = new List<Thing>());
		list.Clear();
		list.AddRange(hashSet);
		list.Sort((Thing a, Thing val3) => a.thingIDNumber.CompareTo(val3.thingIDNumber));
		for (int num3 = 0; num3 < list.Count; num3++)
		{
			Thing val2 = list[num3];
			if (val2 != null && !val2.Destroyed)
			{
				StoreUtility.TryFindBestBetterStoreCellFor(val2, pawn, ((Thing)pawn).Map, (StoragePriority)0, ((Thing)pawn).Faction, ref invalid, false);
				if (((IntVec3)(ref invalid)).IsValid)
				{
					break;
				}
			}
		}
		if (!((IntVec3)(ref invalid)).IsValid)
		{
			return false;
		}
		int pawnToTarget = CellDist(((Thing)pawn).Position, b);
		int pawnToStorage = CellDist(((Thing)pawn).Position, invalid);
		int storageToTarget = CellDist(invalid, b);
		if (protectedZeroDetourOnly)
		{
			OpportunisticDetour opportunisticDetour = HaulersDreamMod.Settings?.unloadDetour ?? OpportunisticDetour.Short;
			if (opportunisticDetour == OpportunisticDetour.Off)
			{
				return false;
			}
			return OpportunisticUnloadPolicy.ShouldUnloadZeroDetour(pawnToTarget, pawnToStorage, storageToTarget, OpportunisticUnloadPolicy.DetourBudgetTiles(opportunisticDetour));
		}
		if (!runOver)
		{
			return OpportunisticUnloadPolicy.ShouldUnloadOnWay(pawnToTarget, pawnToStorage, storageToTarget, loadFraction);
		}
		return OpportunisticUnloadPolicy.ShouldUnloadOnRunEnd(pawnToTarget, pawnToStorage, storageToTarget, loadFraction);
	}

	internal static bool ShouldUnloadBeforeRelocation(Pawn pawn, Job workJob)
	{
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b0: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.keepWorkingWhenFull || !settings.markForUnload)
		{
			return false;
		}
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || !((Thing)pawn).Map.IsPlayerHome || workJob?.def == null)
		{
			return false;
		}
		if (pawn.Drafted || workJob.playerForced)
		{
			return false;
		}
		if (ProtectedWork.IsProtected(workJob, nodeIsEmergency: false) || ProtectedWork.IsRestingPatient(pawn))
		{
			return false;
		}
		if (workJob.def == HaulersDreamDefOf.HaulersDream_UnloadInventory || pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_UnloadInventory || pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_BillPrepGather)
		{
			return false;
		}
		if (workJob.def.driverClass != null && workJob.def.driverClass.Assembly == typeof(OpportunisticUnload).Assembly)
		{
			return false;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp == null || comp.PeekHashSet().Count == 0)
		{
			return false;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : 0);
		if (num - comp.lastOpportunisticUnloadTick < 250)
		{
			return false;
		}
		if (!PawnUnloadChecker.AnyUnloadable(pawn, comp.PeekHashSet()))
		{
			return false;
		}
		PawnMass pawnMass = PawnMassCache.MassInfo(pawn);
		float capacity = pawnMass.Capacity;
		if (capacity <= 0f)
		{
			return false;
		}
		float encumbranceRatio = pawnMass.CurrentMass / capacity;
		float num2 = OverloadTuning.SpeedFactor(OverloadGate.NoOverloadFor(pawn, settings) ? 10 : settings.overloadLevel, encumbranceRatio);
		if (num2 >= 1f)
		{
			return false;
		}
		IntVec3 cell = ((LocalTargetInfo)(ref workJob.targetA)).Cell;
		if (!((IntVec3)(ref cell)).IsValid)
		{
			List<LocalTargetInfo> targetQueueA = workJob.targetQueueA;
			if (targetQueueA != null && targetQueueA.Count > 0)
			{
				LocalTargetInfo val = targetQueueA[0];
				cell = ((LocalTargetInfo)(ref val)).Cell;
			}
		}
		if (!((IntVec3)(ref cell)).IsValid)
		{
			return false;
		}
		comp.lastOpportunisticUnloadTick = num;
		IntVec3 invalid = IntVec3.Invalid;
		List<Thing> list = scratchRep ?? (scratchRep = new List<Thing>());
		list.Clear();
		foreach (Thing item in comp.PeekHashSet())
		{
			if (item != null)
			{
				list.Add(item);
			}
		}
		list.Sort((Thing a, Thing b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
		for (int num3 = 0; num3 < list.Count; num3++)
		{
			Thing val2 = list[num3];
			if (val2 != null && !val2.Destroyed)
			{
				StoreUtility.TryFindBestBetterStoreCellFor(val2, pawn, ((Thing)pawn).Map, (StoragePriority)0, ((Thing)pawn).Faction, ref invalid, false);
				if (((IntVec3)(ref invalid)).IsValid)
				{
					break;
				}
			}
		}
		if (!((IntVec3)(ref invalid)).IsValid)
		{
			return false;
		}
		int num4 = CellDist(((Thing)pawn).Position, cell);
		int num5 = CellDist(((Thing)pawn).Position, invalid);
		return KeepWorkingPolicy.ShouldUnloadBeforeNext(num2, num4, num5, settings.keepWorkingMarginCells);
	}

	internal static void NotifyDiverted(Pawn pawn)
	{
		CompHauledToInventory compHauledToInventory = ((pawn != null) ? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() : null);
		if (compHauledToInventory != null)
		{
			TickManager tickManager = Find.TickManager;
			compHauledToInventory.lastOpportunisticUnloadTick = ((tickManager != null) ? tickManager.TicksGame : 0);
		}
	}

	internal static Job TryGetEndOfRunUnloadJob(Pawn pawn)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || ((pawn != null) ? ((Thing)pawn).Map : null) == null || pawn.jobs == null)
		{
			return null;
		}
		if (((Thing)pawn).Faction != Faction.OfPlayerSilentFail)
		{
			return null;
		}
		if (ProtectedWork.IsRestingPatient(pawn))
		{
			return null;
		}
		if (!settings.markForUnload)
		{
			return null;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp == null)
		{
			return null;
		}
		HashSet<Thing> hashSet = comp.GetHashSet();
		if (hashSet.Count == 0)
		{
			return null;
		}
		if (ConstructionMaterialHold.HoldsMaterialForActiveConstruction(pawn, comp))
		{
			return null;
		}
		int num = ((!IsEnteringDowntime(pawn, settings)) ? settings.unloadGraceTicks : 0);
		if (num > 0)
		{
			TickManager tickManager = Find.TickManager;
			if (((tickManager != null) ? tickManager.TicksGame : 0) - comp.lastYieldTick < num)
			{
				return null;
			}
		}
		if (!MapGate.ShouldUnloadToStorage(((Thing)pawn).Map))
		{
			return PackAnimalLoad.TryGetOpportunisticLoadJob(pawn);
		}
		bool anyUnloadable = PawnUnloadChecker.AnyUnloadable(pawn, hashSet);
		bool alreadyUnloading = pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_UnloadInventory || PawnUnloadChecker.HasQueuedUnload(pawn);
		TickManager tickManager2 = Find.TickManager;
		int num2 = ((tickManager2 != null) ? tickManager2.TicksGame : 0);
		if (!UnloadPolicy.EndOfRunUnloadAllowed(settings.markForUnload, YieldRouter.IsEligible(pawn), pawn.Drafted, hashSet.Count, anyUnloadable, alreadyUnloading, num2 - comp.lastOpportunisticUnloadTick, 250))
		{
			return null;
		}
		Job val = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_UnloadInventory);
		if (!val.TryMakePreToilReservations(pawn, false))
		{
			return null;
		}
		NotifyDiverted(pawn);
		HDLog.Dbg($"{pawn} work ran dry with {hashSet.Count} tracked stacks — unloading before leisure.");
		return val;
	}

	private static int CellDist(IntVec3 a, IntVec3 b)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 val = a - b;
		return Mathf.RoundToInt(((IntVec3)(ref val)).LengthHorizontal);
	}
}
