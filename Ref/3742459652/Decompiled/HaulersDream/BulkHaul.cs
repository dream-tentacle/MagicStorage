using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class BulkHaul
{
	private struct CachedPlan
	{
		public Job job;

		public int loadID;

		public int jobState;
	}

	private const float MinSearchRadius = 12f;

	private const float SearchRangeFraction = 0.5f;

	internal const int MaxStacks = 24;

	private const float PoolRadiusHops = 4f;

	[ThreadStatic]
	private static int cacheTick;

	[ThreadStatic]
	private static Dictionary<long, CachedPlan> planCache;

	[ThreadStatic]
	private static List<Thing> scratchPool;

	[ThreadStatic]
	private static Dictionary<ISlotGroup, StorageGroupBudget> scratchGroupBudgets;

	[ThreadStatic]
	private static List<Thing> scratchThings;

	[ThreadStatic]
	private static List<int> scratchCounts;

	[ThreadStatic]
	private static HashSet<IHaulDestination> scratchCorpseContainers;

	static BulkHaul()
	{
		CacheRegistry.Register(ClearPlanCache);
	}

	internal static Job TryBuildBulkJob(Pawn pawn, Thing primary, Job vanillaJob, bool forced, bool forceSweep = false)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null || primary == null)
		{
			return null;
		}
		if (!forced)
		{
			if (!MasterEnable.Active)
			{
				return null;
			}
			CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
			if (comp != null && !comp.autoHaulYields)
			{
				return null;
			}
			if (!YieldRouter.FitToStartHaul(pawn))
			{
				return null;
			}
			if (ForeignOrderGuard.ClaimedByForeignOrder(primary))
			{
				return null;
			}
			if (RimIOTCompat.IsPresent && RimIOTCompat.IsRimIOTHandledCell(((Thing)pawn).Map, primary.Position))
			{
				return null;
			}
		}
		if (!forced && !HasPotentialBulkWork(pawn, primary, vanillaJob))
		{
			return null;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : (-1));
		Dictionary<long, CachedPlan> dictionary = planCache ?? (planCache = new Dictionary<long, CachedPlan>());
		if (num != cacheTick)
		{
			dictionary.Clear();
			cacheTick = num;
		}
		long num2 = ((long)((Thing)pawn).thingIDNumber << 32) | (uint)primary.thingIDNumber;
		if (forced)
		{
			num2 = ~num2;
		}
		if (forceSweep)
		{
			num2 ^= 0x5F5F5F5F5F5F5F5FL;
		}
		int num3 = (forced ? JobStateSignature(pawn) : 0);
		if (dictionary.TryGetValue(num2, out var value) && (!forced || value.jobState == num3))
		{
			if (value.job == null)
			{
				return null;
			}
			if (value.job.loadID == value.loadID && value.job.def == HaulersDreamDefOf.HaulersDream_BulkHaul && ((LocalTargetInfo)(ref value.job.targetA)).Thing == primary)
			{
				return value.job;
			}
		}
		Job val = BuildBulkJob(pawn, primary, vanillaJob, forced, forceSweep);
		dictionary[num2] = new CachedPlan
		{
			job = val,
			loadID = (val?.loadID ?? (-1)),
			jobState = num3
		};
		return val;
	}

	private static int JobStateSignature(Pawn pawn)
	{
		int num = pawn.CurJob?.loadID ?? (-1);
		Pawn_JobTracker jobs = pawn.jobs;
		int? obj;
		if (jobs == null)
		{
			obj = null;
		}
		else
		{
			JobQueue jobQueue = jobs.jobQueue;
			obj = ((jobQueue != null) ? new int?(jobQueue.Count) : ((int?)null));
		}
		int? num2 = obj;
		int valueOrDefault = num2.GetValueOrDefault();
		return (num * 397) ^ valueOrDefault;
	}

	internal static void ClearPlanCache()
	{
		planCache?.Clear();
		cacheTick = -1;
		scratchPool?.Clear();
		scratchGroupBudgets?.Clear();
		scratchThings?.Clear();
		scratchCounts?.Clear();
		scratchCorpseContainers?.Clear();
	}

	private static bool ReservedByWildAnimal(Thing t)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (t == null)
		{
			obj = null;
		}
		else
		{
			Map map = t.Map;
			if (map == null)
			{
				obj = null;
			}
			else
			{
				PhysicalInteractionReservationManager physicalInteractionReservationManager = map.physicalInteractionReservationManager;
				obj = ((physicalInteractionReservationManager != null) ? physicalInteractionReservationManager.FirstReserverOf(LocalTargetInfo.op_Implicit(t)) : null);
			}
		}
		Pawn val = (Pawn)obj;
		if (val != null && val.RaceProps != null && val.RaceProps.Animal)
		{
			return ((Thing)val).Faction != Faction.OfPlayerSilentFail;
		}
		return false;
	}

	private static bool MaySweepCorpse(Pawn pawn, Corpse corpse, HaulersDreamSettings s)
	{
		if (corpse == null)
		{
			return true;
		}
		if (corpse.InnerPawn == null)
		{
			return false;
		}
		if (s == null)
		{
			return true;
		}
		if (!HaulerMaySweepCorpses(pawn, s))
		{
			return false;
		}
		Pawn innerPawn = corpse.InnerPawn;
		return CorpseHaulPolicy.CorpseMayBeSwept(innerPawn.RaceProps != null && innerPawn.RaceProps.Humanlike, corpseMassKg: (s.corpseMaxHaulMassKg > 0f) ? StatExtension.GetStatValue((Thing)(object)corpse, StatDefOf.Mass, true, -1) : 0f, corpseHaulHumanlike: s.corpseHaulHumanlike, corpseMaxHaulMassKg: s.corpseMaxHaulMassKg);
	}

	private static bool HaulerMaySweepCorpses(Pawn pawn, HaulersDreamSettings s)
	{
		if (s == null)
		{
			return true;
		}
		RaceProperties val = ((pawn != null) ? pawn.RaceProps : null);
		return CorpseHaulPolicy.HaulerMaySweepCorpses(val != null && val.IsMechanoid, val != null && val.Humanlike, s.corpseHaulByColonists, s.corpseHaulByMechs, s.corpseHaulByAnimals);
	}

	internal static float BudgetedCarriedMassKg(Pawn pawn, float corpseCarryAllowance)
	{
		if (corpseCarryAllowance <= 1f || float.IsNaN(corpseCarryAllowance) || float.IsInfinity(corpseCarryAllowance))
		{
			return MassUtility.GearAndInventoryMass(pawn);
		}
		float num = MassUtility.GearAndInventoryMass(pawn);
		ThingOwner<Thing> val = pawn?.inventory?.innerContainer;
		if (val == null)
		{
			return num;
		}
		for (int i = 0; i < ((ThingOwner)val).Count; i++)
		{
			Thing val2 = val[i];
			if (val2 is Corpse)
			{
				float statValue = StatExtension.GetStatValue(val2, StatDefOf.Mass, true, -1);
				num -= (statValue - CorpseHaulPolicy.BudgetMassKg(statValue, isCorpse: true, corpseCarryAllowance)) * (float)val2.stackCount;
			}
		}
		return num;
	}

	private static bool AcceptsHaulDestination(Job vanillaJob, Thing primary, HaulersDreamSettings s, bool forceSweep)
	{
		if (vanillaJob == null)
		{
			return false;
		}
		if (vanillaJob.def == JobDefOf.HaulToCell)
		{
			return true;
		}
		if (vanillaJob.def != JobDefOf.HaulToContainer)
		{
			return false;
		}
		if (!forceSweep)
		{
			if (primary is Corpse)
			{
				return CorpseSweepPolicy.CanAnchorSweep(s.bulkHaul, s.bulkHaulCorpses, s.autoStripMode == AutoStripMode.DisposalOnly, playerOrdered: false);
			}
			return false;
		}
		return true;
	}

	private static bool HasPotentialBulkWork(Pawn pawn, Thing primary, Job vanillaJob)
	{
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		Map val = ((pawn != null) ? ((Thing)pawn).Map : null);
		if (settings == null || !settings.bulkHaul || val == null || primary == null || !primary.Spawned)
		{
			return false;
		}
		if (!AcceptsHaulDestination(vanillaJob, primary, settings, forceSweep: false))
		{
			return false;
		}
		Corpse val2 = (Corpse)(object)((primary is Corpse) ? primary : null);
		if (val2 != null)
		{
			if (!CorpseSweepPolicy.CanAnchorSweep(settings.bulkHaul, settings.bulkHaulCorpses, settings.autoStripMode == AutoStripMode.DisposalOnly, playerOrdered: false))
			{
				return false;
			}
			if (!MaySweepCorpse(pawn, val2, settings))
			{
				return false;
			}
		}
		if (!MapGate.HdActiveOnMap(val))
		{
			return false;
		}
		if (((Thing)pawn).Faction != Faction.OfPlayerSilentFail || QuestUtility.IsQuestLodger(pawn))
		{
			return false;
		}
		if (!YieldRouter.IsEligible(pawn))
		{
			return false;
		}
		if (((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null)
		{
			return false;
		}
		if (settings.haulOversizedInInventory)
		{
			Pawn_CarryTracker carryTracker = pawn.carryTracker;
			int num = ((carryTracker != null) ? carryTracker.MaxStackSpaceEver(primary.def) : primary.def.stackLimit);
			if (primary.stackCount > num)
			{
				return true;
			}
		}
		IntVec3 cell = ((LocalTargetInfo)(ref vanillaJob.targetB)).Cell;
		IntVec3 val3 = cell - primary.Position;
		float num2 = Math.Max(12f, ((IntVec3)(ref val3)).LengthHorizontal * 0.5f);
		float num3 = num2 * 4f * (num2 * 4f);
		bool isPresent = RimIOTCompat.IsPresent;
		bool flag = CorpseSweepPolicy.CanSweepAsNeighbor(settings.bulkHaul, settings.bulkHaulCorpses, settings.autoStripMode == AutoStripMode.DisposalOnly, playerOrdered: false) && HaulerMaySweepCorpses(pawn, settings);
		foreach (Thing item in (HashSet<Thing>)val.listerHaulables.ThingsPotentiallyNeedingHauling())
		{
			if (item == null || item == primary || !item.Spawned || item.Map != val || item.def == null || !item.def.EverHaulable)
			{
				continue;
			}
			val3 = item.Position - primary.Position;
			if (!((float)((IntVec3)(ref val3)).LengthHorizontalSquared > num3))
			{
				Corpse val4 = (Corpse)(object)((item is Corpse) ? item : null);
				if ((val4 == null || (flag && MaySweepCorpse(pawn, val4, settings))) && (!isPresent || !RimIOTCompat.IsRimIOTHandledCell(val, item.Position)) && !CorpseStripper.ShouldLeaveTaintedApparel(item, settings))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static Job BuildBulkJob(Pawn pawn, Thing primary, Job vanillaJob, bool forced, bool forceSweep = false)
	{
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_029a: Unknown result type (might be due to invalid IL or missing references)
		//IL_054a: Unknown result type (might be due to invalid IL or missing references)
		//IL_058c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0419: Unknown result type (might be due to invalid IL or missing references)
		//IL_041e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0425: Unknown result type (might be due to invalid IL or missing references)
		//IL_0479: Unknown result type (might be due to invalid IL or missing references)
		//IL_047e: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		Map val = ((pawn != null) ? ((Thing)pawn).Map : null);
		if (settings == null || !settings.bulkHaul || val == null || primary == null || !primary.Spawned)
		{
			return null;
		}
		if (!AcceptsHaulDestination(vanillaJob, primary, settings, forceSweep))
		{
			return null;
		}
		Corpse val2 = (Corpse)(object)((primary is Corpse) ? primary : null);
		if (val2 != null)
		{
			if (!forceSweep && !CorpseSweepPolicy.CanAnchorSweep(settings.bulkHaul, settings.bulkHaulCorpses, settings.autoStripMode == AutoStripMode.DisposalOnly, forced || forceSweep))
			{
				return null;
			}
			if (!MaySweepCorpse(pawn, val2, settings))
			{
				return null;
			}
		}
		if (!MapGate.HdActiveOnMap(val))
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
		if (((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null)
		{
			return null;
		}
		Pawn_CarryTracker carryTracker = pawn.carryTracker;
		int num = ((carryTracker != null) ? carryTracker.MaxStackSpaceEver(primary.def) : primary.def.stackLimit);
		bool flag = primary.stackCount > num;
		IntVec3 cell = ((LocalTargetInfo)(ref vanillaJob.targetB)).Cell;
		if (!forced && RimIOTCompat.IsActive && RimIOTCompat.IsNetworkManagedCell(val, cell))
		{
			HDLog.Dbg($"{pawn} bulk-haul into a RimIOT network declined ({((Entity)primary).LabelShort} -> {cell}); " + "left to vanilla + RimIOT so the terminal haul/unload loop can't form.");
			return null;
		}
		IntVec3 val3 = cell - primary.Position;
		float num2 = Math.Max(12f, ((IntVec3)(ref val3)).LengthHorizontal * 0.5f);
		bool flag2 = forced && SecondTaskedNearby(pawn, primary, num2);
		BulkHaulPolicy.OrderedHaulPlan orderedHaulPlan = BulkHaulPolicy.DecideOrderedHaul(settings.bulkHaulTrigger, forced, forceSweep, flag2, forced && flag && settings.haulOversizedInInventory);
		if (orderedHaulPlan == BulkHaulPolicy.OrderedHaulPlan.VanillaSingle)
		{
			return null;
		}
		float baseCapKg = CarryMath.EffectiveCapacity(CarryCapacity.Of(pawn), settings.carryLimitFraction);
		float num3 = BulkHaulPolicy.CeilingKg(settings.overloadLevel, OverloadGate.NoOverloadFor(pawn, settings), baseCapKg, OverloadGate.MaxCeilingKg(settings));
		float num4 = BudgetedCarriedMassKg(pawn, settings.corpseCarryAllowance);
		float num5 = CECompat.AvailableBulk(pawn);
		float num6 = CorpseHaulPolicy.BudgetMassKg(StatExtension.GetStatValue(primary, StatDefOf.Mass, true, -1), primary is Corpse, settings.corpseCarryAllowance);
		int val4 = BulkHaulPolicy.CountWithinCeiling(num3, num4, num6, primary.stackCount);
		val4 = Math.Min(val4, CECompat.MaxFitCount(pawn, primary));
		float num7 = CECompat.BulkPerUnit(primary);
		if (num7 > 0f && !float.IsPositiveInfinity(num5))
		{
			val4 = Math.Min(val4, (int)Math.Floor(num5 / num7));
		}
		if (val4 <= 0)
		{
			return null;
		}
		if (BulkHaulPolicy.InventoryHaulWorseThanHands(CECompat.IsActive, forceSweep, val4, num, primary.stackCount))
		{
			return null;
		}
		Dictionary<ISlotGroup, StorageGroupBudget> dictionary = scratchGroupBudgets ?? (scratchGroupBudgets = new Dictionary<ISlotGroup, StorageGroupBudget>());
		dictionary.Clear();
		bool denied;
		StorageGroupBudget storageGroupBudget = ResolveGroupBudget(pawn, primary, cell, val, dictionary, out denied);
		ISlotGroup val5 = BudgetGroupOf(val.haulDestinationManager.SlotGroupAt(cell));
		int num8 = ((!denied) ? StorageCommitments.FreeUnitsFor(pawn, val5, primary.def, primary) : 0);
		if (num8 != int.MaxValue)
		{
			val4 = Math.Min(val4, num8);
			if (val4 <= 0)
			{
				if (StorageCommitments.AnyClaims)
				{
					StorageCommitments.Trace("bulk-decline", pawn, val5, primary.def, 0, num8);
				}
				return null;
			}
		}
		num4 += (float)val4 * num6;
		num5 -= (float)val4 * num7;
		List<Thing> list = scratchThings ?? (scratchThings = new List<Thing>());
		List<int> list2 = scratchCounts ?? (scratchCounts = new List<int>());
		list.Clear();
		list2.Clear();
		list.Add(primary);
		list2.Add(val4);
		if (orderedHaulPlan == BulkHaulPolicy.OrderedHaulPlan.SweepNeighbors)
		{
			List<Thing> pool = scratchPool ?? (scratchPool = new List<Thing>());
			BuildPoolInto(pool, pawn, primary, val, num2 * 4f, CorpseSweepPolicy.CanSweepAsNeighbor(settings.bulkHaul, settings.bulkHaulCorpses, settings.autoStripMode == AutoStripMode.DisposalOnly, forced || forceSweep));
			if (!denied)
			{
				storageGroupBudget?.Consume(primary.def, val4);
			}
			HashSet<IHaulDestination> hashSet = scratchCorpseContainers ?? (scratchCorpseContainers = new HashSet<IHaulDestination>());
			hashSet.Clear();
			if (primary is Corpse && vanillaJob.def == JobDefOf.HaulToContainer)
			{
				Thing thing = ((LocalTargetInfo)(ref vanillaJob.targetB)).Thing;
				IHaulDestination val6 = (IHaulDestination)(object)((thing is IHaulDestination) ? thing : null);
				if (val6 != null)
				{
					hashSet.Add(val6);
				}
			}
			HashSet<Thing> claimed = RouteSelection.ClaimedByOtherPawns(pawn);
			IntVec3 position = primary.Position;
			while (list.Count < 24 && num4 < num3 - 0.0001f)
			{
				int take;
				float unitMassKg;
				Thing val7 = TakeNearestEligible(pawn, pool, position, num2, claimed, num3, num4, num5, dictionary, settings.corpseCarryAllowance, hashSet, out take, out unitMassKg);
				if (val7 == null)
				{
					break;
				}
				list.Add(val7);
				list2.Add(take);
				num4 += (float)take * unitMassKg;
				num5 -= (float)take * CECompat.BulkPerUnit(val7);
				position = val7.Position;
			}
		}
		if (list.Count < 2)
		{
			int num9 = ((num8 == int.MaxValue) ? val4 : Math.Min(val4, num8));
			if (!flag2)
			{
				if (forceSweep)
				{
					if (!forced && BulkHaulPolicy.InventoryHaulWorseThanHands(CECompat.IsActive, forceSweep: false, val4, num, primary.stackCount))
					{
						return null;
					}
					list2[0] = Math.Min(list2[0], num9);
					if (list2[0] <= 0)
					{
						return null;
					}
				}
				else
				{
					if (!settings.haulOversizedInInventory || !BulkHaulPolicy.OversizedStackWorthInventory(primary.stackCount, num, num9))
					{
						return null;
					}
					list2[0] = Math.Min(list2[0], num9);
					if (list2[0] <= 0)
					{
						return null;
					}
				}
			}
		}
		Job val8 = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_BulkHaul, LocalTargetInfo.op_Implicit(primary));
		val8.targetQueueB = new List<LocalTargetInfo>(list.Count);
		val8.countQueue = new List<int>(list2);
		for (int i = 0; i < list.Count; i++)
		{
			val8.targetQueueB.Add(LocalTargetInfo.op_Implicit(list[i]));
		}
		val8.count = 1;
		HDLog.Dbg($"BulkHaul: {pawn} sweeping {list.Count} stacks (~{num4:0.#}kg / ceiling {(float.IsPositiveInfinity(num3) ? (-1f) : num3):0.#}kg, forced={forced}).");
		if (!forced)
		{
			HaulChurnGuard.NoteBulkAnchor(primary);
		}
		return val8;
	}

	internal static Job BuildBulkJobForced(Pawn pawn, Thing clicked)
	{
		if (pawn == null || clicked == null)
		{
			return null;
		}
		Job val = HaulAIUtility.HaulToStorageJob(pawn, clicked, true);
		if (val == null || (val.def != JobDefOf.HaulToCell && val.def != JobDefOf.HaulToContainer))
		{
			return null;
		}
		return BuildBulkJob(pawn, clicked, val, forced: true, forceSweep: true);
	}

	internal static Job BuildPickUpJob(Pawn pawn, Thing clicked)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null || clicked == null || !clicked.Spawned)
		{
			return null;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		Map map = ((Thing)pawn).Map;
		if (settings == null || map == null)
		{
			return null;
		}
		if (!MapGate.HdActiveOnMap(map))
		{
			return null;
		}
		if (((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null)
		{
			return null;
		}
		int num = MassClampedTake(pawn, clicked, clicked.stackCount, settings);
		if (num <= 0)
		{
			return null;
		}
		Job obj = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_BulkHaul, LocalTargetInfo.op_Implicit(clicked));
		obj.targetQueueB = new List<LocalTargetInfo> { LocalTargetInfo.op_Implicit(clicked) };
		obj.countQueue = new List<int> { num };
		obj.count = 1;
		obj.takeInventoryDelay = 120;
		return obj;
	}

	internal static int MassClampedTake(Pawn pawn, Thing thing, int planned, HaulersDreamSettings s)
	{
		if (pawn == null || thing == null || s == null)
		{
			return 0;
		}
		float baseCapKg = CarryMath.EffectiveCapacity(CarryCapacity.Of(pawn), s.carryLimitFraction);
		float ceilingKg = BulkHaulPolicy.CeilingKg(s.overloadLevel, OverloadGate.NoOverloadFor(pawn, s), baseCapKg, OverloadGate.MaxCeilingKg(s));
		float currentMassKg = BudgetedCarriedMassKg(pawn, s.corpseCarryAllowance);
		int val = BulkHaulPolicy.CountWithinCeiling(ceilingKg, currentMassKg, CorpseHaulPolicy.BudgetMassKg(StatExtension.GetStatValue(thing, StatDefOf.Mass, true, -1), thing is Corpse, s.corpseCarryAllowance), Math.Min(planned, thing.stackCount));
		val = Math.Min(val, CECompat.MaxFitCount(pawn, thing));
		float num = CECompat.BulkPerUnit(thing);
		float num2 = CECompat.AvailableBulk(pawn);
		if (num > 0f && !float.IsPositiveInfinity(num2))
		{
			val = Math.Min(val, (int)Math.Floor(num2 / num));
		}
		return val;
	}

	internal static Job BuildKeepJob(Pawn pawn, Thing clicked, int keepCount = -1)
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null || clicked == null || !clicked.Spawned)
		{
			return null;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || ((Thing)pawn).Map == null)
		{
			return null;
		}
		if (((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null)
		{
			return null;
		}
		int planned = ((keepCount > 0) ? keepCount : clicked.stackCount);
		int num = MassClampedTake(pawn, clicked, planned, settings);
		if (num <= 0)
		{
			return null;
		}
		Job obj = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_KeepInInventory, LocalTargetInfo.op_Implicit(clicked));
		obj.count = num;
		return obj;
	}

	internal static Job BuildKeepFromContainerJob(Pawn pawn, Thing clicked, Thing container, int keepCount = -1)
	{
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null || clicked == null || container == null || !container.Spawned)
		{
			return null;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || ((Thing)pawn).Map == null || container.Map != ((Thing)pawn).Map)
		{
			return null;
		}
		if (((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null)
		{
			return null;
		}
		ThingOwner val = ThingOwnerUtility.TryGetInnerInteractableThingOwner(container);
		if (val == null || !val.Contains(clicked))
		{
			return null;
		}
		int planned = ((keepCount > 0) ? keepCount : clicked.stackCount);
		int num = MassClampedTake(pawn, clicked, planned, settings);
		if (num <= 0)
		{
			return null;
		}
		Job obj = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_KeepInInventory, LocalTargetInfo.op_Implicit(clicked), LocalTargetInfo.op_Implicit(container));
		obj.count = num;
		return obj;
	}

	internal static List<Thing> BuildPool(Pawn pawn, Thing primary, Map map, float poolRadius)
	{
		List<Thing> list = new List<Thing>();
		BuildPoolInto(list, pawn, primary, map, poolRadius);
		return list;
	}

	private static void BuildPoolInto(List<Thing> pool, Pawn pawn, Thing primary, Map map, float poolRadius, bool includeCorpses = false)
	{
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		pool.Clear();
		float num = poolRadius * poolRadius;
		bool isPresent = RimIOTCompat.IsPresent;
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		bool flag = includeCorpses && HaulerMaySweepCorpses(pawn, settings);
		ICollection<Thing> collection = map.listerHaulables.ThingsPotentiallyNeedingHauling();
		IntVec3 val;
		if (collection is HashSet<Thing> hashSet)
		{
			{
				foreach (Thing item in hashSet)
				{
					if (item == null || item == primary || !item.Spawned || item.Map != map || !item.def.EverHaulable)
					{
						continue;
					}
					val = item.Position - primary.Position;
					if (!((float)((IntVec3)(ref val)).LengthHorizontalSquared > num))
					{
						Corpse val2 = (Corpse)(object)((item is Corpse) ? item : null);
						if ((val2 == null || (flag && !ReservedByWildAnimal(item) && MaySweepCorpse(pawn, val2, settings))) && (!isPresent || !RimIOTCompat.IsRimIOTHandledCell(map, item.Position)) && !CorpseStripper.ShouldLeaveTaintedApparel(item, settings))
						{
							pool.Add(item);
						}
					}
				}
				return;
			}
		}
		foreach (Thing item2 in collection)
		{
			if (item2 == null || item2 == primary || !item2.Spawned || item2.Map != map || !item2.def.EverHaulable)
			{
				continue;
			}
			val = item2.Position - primary.Position;
			if (!((float)((IntVec3)(ref val)).LengthHorizontalSquared > num))
			{
				Corpse val3 = (Corpse)(object)((item2 is Corpse) ? item2 : null);
				if ((val3 == null || (flag && !ReservedByWildAnimal(item2) && MaySweepCorpse(pawn, val3, settings))) && (!isPresent || !RimIOTCompat.IsRimIOTHandledCell(map, item2.Position)) && !CorpseStripper.ShouldLeaveTaintedApparel(item2, settings))
				{
					pool.Add(item2);
				}
			}
		}
	}

	private static Thing TakeNearestEligible(Pawn pawn, List<Thing> pool, IntVec3 from, float radius, HashSet<Thing> claimed, float ceiling, float runningMass, float bulkRoom, Dictionary<ISlotGroup, StorageGroupBudget> budgets, float corpseAllowance, HashSet<IHaulDestination> corpseContainers, out int take, out float unitMassKg)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		take = 0;
		unitMassKg = 0f;
		float num = radius * radius;
		Thing val2;
		float num5;
		int val3;
		IntVec3 cell = default(IntVec3);
		IHaulDestination val5 = default(IHaulDestination);
		bool flag;
		StorageGroupBudget storageGroupBudget;
		while (true)
		{
			int num2 = -1;
			float num3 = float.MaxValue;
			for (int i = 0; i < pool.Count; i++)
			{
				IntVec3 val = pool[i].Position - from;
				float num4 = ((IntVec3)(ref val)).LengthHorizontalSquared;
				if (num4 <= num && (num4 < num3 || (num4 == num3 && num2 >= 0 && pool[i].thingIDNumber < pool[num2].thingIDNumber)))
				{
					num3 = num4;
					num2 = i;
				}
			}
			if (num2 < 0)
			{
				return null;
			}
			val2 = pool[num2];
			pool.RemoveAt(num2);
			if (ForbidUtility.IsForbidden(val2, pawn) || claimed.Contains(val2) || ForeignOrderGuard.ClaimedByForeignOrder(val2))
			{
				continue;
			}
			num5 = CorpseHaulPolicy.BudgetMassKg(StatExtension.GetStatValue(val2, StatDefOf.Mass, true, -1), val2 is Corpse, corpseAllowance);
			val3 = BulkHaulPolicy.CountWithinCeiling(ceiling, runningMass, num5, val2.stackCount);
			val3 = Math.Min(val3, CECompat.MaxFitCount(pawn, val2));
			float num6 = CECompat.BulkPerUnit(val2);
			if (num6 > 0f && !float.IsPositiveInfinity(bulkRoom))
			{
				val3 = Math.Min(val3, (int)Math.Floor(bulkRoom / num6));
			}
			if (val3 <= 0 || !HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, val2, false) || !ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(val2), (PathEndMode)3))
			{
				continue;
			}
			StoragePriority val4 = StoreUtility.CurrentStoragePriorityOf(val2, false);
			if (!StoreUtility.TryFindBestBetterStorageFor(val2, pawn, ((Thing)pawn).Map, val4, ((Thing)pawn).Faction, ref cell, ref val5, false))
			{
				continue;
			}
			flag = !((IntVec3)(ref cell)).IsValid && val5 != null;
			if (corpseContainers != null && flag && val2 is Corpse && corpseContainers.Contains(val5))
			{
				continue;
			}
			storageGroupBudget = ResolveGroupBudget(pawn, val2, cell, ((Thing)pawn).Map, budgets, out var denied);
			if (!denied)
			{
				int num7 = storageGroupBudget?.AvailableFor(val2.def) ?? int.MaxValue;
				if (num7 == int.MaxValue)
				{
					break;
				}
				val3 = Math.Min(val3, num7);
				if (val3 > 0)
				{
					break;
				}
			}
		}
		storageGroupBudget?.Consume(val2.def, val3);
		if (corpseContainers != null && flag && val2 is Corpse)
		{
			corpseContainers.Add(val5);
		}
		unitMassKg = num5;
		take = val3;
		return val2;
	}

	internal static ISlotGroup BudgetGroupOf(SlotGroup slotGroup)
	{
		object obj;
		if (slotGroup != null)
		{
			ISlotGroup storageGroup = (ISlotGroup)(object)slotGroup.StorageGroup;
			obj = storageGroup;
			if (obj == null)
			{
				return (ISlotGroup)(object)slotGroup;
			}
		}
		else
		{
			obj = null;
		}
		return (ISlotGroup)obj;
	}

	private static StorageGroupBudget ResolveGroupBudget(Pawn pawn, Thing thing, IntVec3 cell, Map map, Dictionary<ISlotGroup, StorageGroupBudget> budgets, out bool denied)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		denied = false;
		if (!((IntVec3)(ref cell)).IsValid)
		{
			return null;
		}
		SlotGroup val = map.haulDestinationManager.SlotGroupAt(cell);
		if (val == null)
		{
			return null;
		}
		StorageBuildingFilter storageBuildingFilter = StorageCommitments.ActiveFilter();
		if (storageBuildingFilter != null && !storageBuildingFilter.IsGroupAllowed(val))
		{
			denied = true;
			return null;
		}
		ISlotGroup val2 = BudgetGroupOf(val);
		if (budgets.TryGetValue(val2, out var value))
		{
			PriceDefInto(value, pawn, thing, val2, map);
			return value;
		}
		StorageCommitments.GroupSpace groupSpace = StorageCommitments.MeasureGroup(pawn, thing, val2, map);
		value = new StorageGroupBudget(groupSpace.Unbounded ? int.MaxValue : groupSpace.EmptyCells);
		PriceDefInto(value, pawn, thing, val2, map);
		budgets[val2] = value;
		return value;
	}

	private static void PriceDefInto(StorageGroupBudget budget, Pawn pawn, Thing thing, ISlotGroup group, Map map)
	{
		if (!budget.Unbounded && !budget.IsPriced(thing.def))
		{
			StorageCommitments.GroupSpace groupSpace = StorageCommitments.MeasureGroup(pawn, thing, group, map);
			budget.PriceDef(thing.def, groupSpace.PartialSpace, groupSpace.PerCellCapacity);
			budget.Consume(thing.def, StorageCommitments.ClaimedByOthersFor(pawn, group, thing.def));
		}
	}

	internal static bool TryTakeoverSecondOrder(Pawn pawn, Job incoming, JobTag? tag, ref bool result)
	{
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.bulkHaul || pawn?.jobs == null || incoming == null)
		{
			return false;
		}
		Job curJob = pawn.CurJob;
		bool incomingIsBulk = incoming.def == HaulersDreamDefOf.HaulersDream_BulkHaul;
		bool flag = curJob != null && curJob.def == HaulersDreamDefOf.HaulersDream_BulkHaul && pawn.jobs.curDriver is JobDriver_BulkHaul jobDriver_BulkHaul && jobDriver_BulkHaul.IsStillLoading;
		Thing val = null;
		if (!flag && curJob != null && curJob.def == JobDefOf.HaulToCell && curJob.playerForced)
		{
			Pawn_CarryTracker carryTracker = pawn.carryTracker;
			Thing val2 = ((carryTracker != null) ? carryTracker.CarriedThing : null) ?? ((LocalTargetInfo)(ref curJob.targetA)).Thing;
			Thing thing = ((LocalTargetInfo)(ref incoming.targetA)).Thing;
			if (val2 != null && thing != null && ((Thing)pawn).Map != null)
			{
				float num = TakeoverNearbyRadius(pawn, thing);
				IntVec3 val3 = val2.PositionHeld - thing.PositionHeld;
				if ((float)((IntVec3)(ref val3)).LengthHorizontalSquared <= num * num)
				{
					val = val2;
				}
			}
		}
		switch (BulkHaulPolicy.DecideTakeover(settings.bulkHaulTrigger, incomingIsBulk, flag, val != null))
		{
		case BulkHaulPolicy.BulkTakeoverAction.AppendToActiveBulk:
		{
			Thing thing2 = ((LocalTargetInfo)(ref incoming.targetA)).Thing;
			AppendToBulkJob(curJob, thing2, thing2?.stackCount ?? 0);
			result = true;
			return true;
		}
		case BulkHaulPolicy.BulkTakeoverAction.TakeOverSoloHaul:
		{
			if (!pawn.jobs.IsCurrentJobPlayerInterruptible())
			{
				return false;
			}
			incoming.playerForced = true;
			if (!incoming.TryMakePreToilReservations(pawn, false))
			{
				return false;
			}
			if (val != null && val.Spawned)
			{
				AppendToBulkJob(incoming, val, val.stackCount);
			}
			else
			{
				Pawn_CarryTracker carryTracker2 = pawn.carryTracker;
				Thing val4 = default(Thing);
				if (((carryTracker2 != null) ? carryTracker2.CarriedThing : null) != null && pawn.carryTracker.TryDropCarriedThing(((Thing)pawn).Position, (ThingPlaceMode)1, ref val4, (Action<Thing, int>)null) && val4 != null)
				{
					AppendToBulkJob(incoming, val4, val4.stackCount);
				}
			}
			curJob.playerInterruptedForced = true;
			Pawn_StanceTracker stances = pawn.stances;
			if (stances != null)
			{
				stances.CancelBusyStanceSoft();
			}
			pawn.jobs.jobQueue.EnqueueFirst(incoming, (JobTag?)tag.GetValueOrDefault());
			pawn.jobs.curDriver.EndJobWith((JobCondition)16);
			HDLog.Dbg($"{pawn} second haul order — bulk sweep takes over now (folding in the first haul).");
			result = true;
			return true;
		}
		default:
			return false;
		}
	}

	private static float TakeoverNearbyRadius(Pawn pawn, Thing primary)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 val = default(IntVec3);
		StoreUtility.TryFindBestBetterStoreCellFor(primary, pawn, ((Thing)pawn).Map, StoreUtility.CurrentStoragePriorityOf(primary, false), ((Thing)pawn).Faction, ref val, false);
		float num;
		if (!((IntVec3)(ref val)).IsValid)
		{
			num = 12f;
		}
		else
		{
			IntVec3 val2 = val - primary.Position;
			num = ((IntVec3)(ref val2)).LengthHorizontal;
		}
		float num2 = num;
		return Math.Max(12f, num2 * 0.5f) * 4f;
	}

	internal static void AppendToBulkJob(Job job, Thing thing, int count)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		if (job == null || thing == null)
		{
			return;
		}
		if (job.targetQueueB == null)
		{
			job.targetQueueB = new List<LocalTargetInfo>();
		}
		if (job.countQueue == null)
		{
			job.countQueue = new List<int>();
		}
		for (int i = 0; i < job.targetQueueB.Count; i++)
		{
			LocalTargetInfo val = job.targetQueueB[i];
			if (((LocalTargetInfo)(ref val)).Thing == thing)
			{
				return;
			}
		}
		job.targetQueueB.Add(LocalTargetInfo.op_Implicit(thing));
		job.countQueue.Add((count > 0) ? count : thing.stackCount);
	}

	private static bool SecondTaskedNearby(Pawn pawn, Thing primary, float radius)
	{
		if (pawn.jobs == null)
		{
			return false;
		}
		float radiusSq = radius * radius;
		if (IsNearbyHaulOrder(pawn, pawn.CurJob, primary, radiusSq))
		{
			return true;
		}
		JobQueue jobQueue = pawn.jobs.jobQueue;
		if (jobQueue != null)
		{
			for (int i = 0; i < jobQueue.Count; i++)
			{
				if (IsNearbyHaulOrder(pawn, jobQueue[i]?.job, primary, radiusSq))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool IsNearbyHaulOrder(Pawn pawn, Job j, Thing primary, float radiusSq)
	{
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		if (j == null)
		{
			return false;
		}
		if (!j.playerForced)
		{
			return false;
		}
		if (j.def != JobDefOf.HaulToCell && j.def != JobDefOf.HaulToContainer && j.def != HaulersDreamDefOf.HaulersDream_BulkHaul)
		{
			return false;
		}
		Thing thing = ((LocalTargetInfo)(ref j.targetA)).Thing;
		if (thing == null || thing == primary)
		{
			return false;
		}
		if (!thing.Spawned)
		{
			Pawn_CarryTracker carryTracker = pawn.carryTracker;
			if (thing != ((carryTracker != null) ? carryTracker.CarriedThing : null))
			{
				return false;
			}
		}
		IntVec3 val = thing.PositionHeld - primary.PositionHeld;
		return (float)((IntVec3)(ref val)).LengthHorizontalSquared <= radiusSq;
	}
}
