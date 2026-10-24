using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class PackAnimalLoad
{
	private const float MinSearchRadius = 12f;

	private const int MaxStacks = 24;

	private const float PoolRadiusHops = 4f;

	internal static Pawn FindCarrier(Pawn pawn)
	{
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null)
		{
			return null;
		}
		Pawn val = GiveToPackAnimalUtility.UsablePackAnimalWithTheMostFreeSpace(pawn);
		if (val != null)
		{
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings != null && !settings.enableVehicleFramework && VehicleFrameworkCompat.IsVehicle((Thing)(object)val))
			{
				return null;
			}
		}
		return val;
	}

	internal static bool HasDepositableSurplus(Pawn pawn)
	{
		ThingOwner<Thing> val = pawn?.inventory?.innerContainer;
		CompHauledToInventory compHauledToInventory = ((pawn != null) ? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() : null);
		if (val == null || compHauledToInventory == null)
		{
			return false;
		}
		foreach (Thing item in compHauledToInventory.PeekHashSet())
		{
			if (item != null && !item.Destroyed && ((ThingOwner)val).Contains(item) && InventorySurplus.SurplusOf(pawn, item) > 0)
			{
				return true;
			}
		}
		return false;
	}

	internal static bool HasLoadJob(Pawn pawn)
	{
		if (pawn?.jobs == null)
		{
			return false;
		}
		if (pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_LoadPackAnimal)
		{
			return true;
		}
		JobQueue jobQueue = pawn.jobs.jobQueue;
		if (jobQueue != null)
		{
			for (int i = 0; i < jobQueue.Count; i++)
			{
				if (jobQueue[i]?.job?.def == HaulersDreamDefOf.HaulersDream_LoadPackAnimal)
				{
					return true;
				}
			}
		}
		return false;
	}

	internal static void MaybeAutoDivert(Pawn pawn, HaulersDreamSettings s)
	{
		if (s != null && ((pawn != null) ? ((Thing)pawn).Map : null) != null && !pawn.Drafted)
		{
			bool isPlayerHome = ((Thing)pawn).Map.IsPlayerHome;
			bool flag = HasLoadJob(pawn);
			bool flag2 = !flag && HasDepositableSurplus(pawn);
			Pawn val = ((s.autoDivertToPackAnimal && s.enableOnNonHomeMaps && !isPlayerHome && flag2) ? FindCarrier(pawn) : null);
			if (PackAnimalLoadPolicy.ShouldAutoDivert(s.autoDivertToPackAnimal, s.enableOnNonHomeMaps, isPlayerHome, val != null, flag2, flag))
			{
				QueueDepositOnly(pawn, val);
			}
		}
	}

	internal static Job TryGetOpportunisticLoadJob(Pawn pawn)
	{
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || ((pawn != null) ? ((Thing)pawn).Map : null) == null || pawn.jobs == null)
		{
			return null;
		}
		bool isPlayerHome = ((Thing)pawn).Map.IsPlayerHome;
		bool drafted = pawn.Drafted;
		bool flag = ((Thing)pawn).Faction == Faction.OfPlayerSilentFail && YieldRouter.IsEligible(pawn);
		bool flag2 = HasLoadJob(pawn);
		bool flag3 = !flag2 && HasDepositableSurplus(pawn);
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : 0);
		bool flag4 = comp != null && num - comp.lastOpportunisticUnloadTick >= 250;
		if (!(settings.markForUnload && settings.autoDivertToPackAnimal && settings.enableOnNonHomeMaps && !isPlayerHome && !drafted && flag && flag3 && !flag2 && flag4))
		{
			return null;
		}
		Pawn val = FindCarrier(pawn);
		if (!PackAnimalLoadPolicy.ShouldOffloadOpportunistically(settings.autoDivertToPackAnimal, settings.enableOnNonHomeMaps, isPlayerHome, val != null, flag3, flag2, flag, drafted))
		{
			return null;
		}
		Job val2 = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_LoadPackAnimal, LocalTargetInfo.op_Implicit((Thing)(object)val));
		if (!val2.TryMakePreToilReservations(pawn, false))
		{
			return null;
		}
		OpportunisticUnload.NotifyDiverted(pawn);
		HDLog.Dbg($"{pawn} offloading scooped loot onto {val} (opportunistic caravan unload).");
		return val2;
	}

	internal static void GizmoLoadNearest(Pawn pawn, bool immediate = false)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (((pawn != null) ? ((Thing)pawn).Map : null) != null && settings != null && settings.enableOnNonHomeMaps && !pawn.Drafted && !HasLoadJob(pawn) && HasDepositableSurplus(pawn))
		{
			Pawn val = FindCarrier(pawn);
			if (val == null)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.LoadPackAnimal.NoCarrier")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
			}
			else
			{
				QueueDepositOnly(pawn, val, immediate);
			}
		}
	}

	internal static bool ShouldRedirectGiveToPackAnimal(Pawn pawn, Job vanillaJob)
	{
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Invalid comparison between Unknown and I4
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.loadPackAnimalBulk || !settings.enableOnNonHomeMaps)
		{
			return false;
		}
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || ((Thing)pawn).Map.IsPlayerHome || pawn.Drafted)
		{
			return false;
		}
		if (((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null)
		{
			return false;
		}
		Thing val = ((vanillaJob != null) ? ((LocalTargetInfo)(ref vanillaJob.targetA)).Thing : null);
		if (val == null || !val.Spawned || val.def == null || (int)val.def.category != 2)
		{
			return false;
		}
		return FindCarrier(pawn) != null;
	}

	internal static Job FindActiveLoadJob(Pawn pawn)
	{
		if (pawn?.jobs == null)
		{
			return null;
		}
		if (pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_LoadPackAnimal && pawn.CurJob != null)
		{
			return pawn.CurJob;
		}
		JobQueue jobQueue = pawn.jobs.jobQueue;
		if (jobQueue != null)
		{
			for (int i = 0; i < jobQueue.Count; i++)
			{
				if (jobQueue[i]?.job?.def == HaulersDreamDefOf.HaulersDream_LoadPackAnimal)
				{
					return jobQueue[i].job;
				}
			}
		}
		return null;
	}

	internal static void AppendToLoadJob(Job loadJob, Thing item, int count)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		if (loadJob == null || item == null)
		{
			return;
		}
		if (loadJob.targetQueueB == null)
		{
			loadJob.targetQueueB = new List<LocalTargetInfo>();
		}
		if (loadJob.countQueue == null)
		{
			loadJob.countQueue = new List<int>();
		}
		for (int i = 0; i < loadJob.targetQueueB.Count; i++)
		{
			LocalTargetInfo val = loadJob.targetQueueB[i];
			if (((LocalTargetInfo)(ref val)).Thing == item)
			{
				return;
			}
		}
		loadJob.targetQueueB.Add(LocalTargetInfo.op_Implicit(item));
		loadJob.countQueue.Add((count > 0) ? count : item.stackCount);
	}

	internal static Job BuildRedirectJob(Pawn pawn, Job vanillaJob)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		Thing val = ((vanillaJob != null) ? ((LocalTargetInfo)(ref vanillaJob.targetA)).Thing : null);
		Pawn val2 = FindCarrier(pawn);
		if (val == null || val2 == null)
		{
			return null;
		}
		int item = ((vanillaJob.count > 0) ? vanillaJob.count : val.stackCount);
		Job obj = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_LoadPackAnimal, LocalTargetInfo.op_Implicit((Thing)(object)val2));
		obj.targetQueueB = new List<LocalTargetInfo> { LocalTargetInfo.op_Implicit(val) };
		obj.countQueue = new List<int> { item };
		obj.count = 1;
		return obj;
	}

	private static void QueueDepositOnly(Pawn pawn, Pawn carrier, bool immediate = false)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Job val = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_LoadPackAnimal, LocalTargetInfo.op_Implicit((Thing)(object)carrier));
		if (pawn.jobs != null && val.TryMakePreToilReservations(pawn, false))
		{
			pawn.jobs.jobQueue.EnqueueFirst(val, (JobTag?)(JobTag)0);
			HDLog.Dbg($"{pawn} diverting to load {carrier} with carried loot.");
			if (immediate)
			{
				PawnUnloadChecker.InterruptCurrentJob(pawn);
			}
		}
	}

	internal static Job TryBuildBulkLoadJob(Pawn pawn, Thing clicked, Pawn carrier)
	{
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		Map val = ((pawn != null) ? ((Thing)pawn).Map : null);
		if (settings == null || val == null || clicked == null || !clicked.Spawned || carrier == null)
		{
			return null;
		}
		if (((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null)
		{
			return null;
		}
		float num = CeilingKg(pawn, settings);
		float running = MassUtility.GearAndInventoryMass(pawn);
		float bulkRoom = CECompat.AvailableBulk(pawn);
		List<Thing> list = new List<Thing>();
		List<int> list2 = new List<int>();
		List<Thing> pool = BulkHaul.BuildPool(pawn, clicked, val, 48f);
		HashSet<Thing> claimed = RouteSelection.ClaimedByOtherPawns(pawn);
		AddIfEligible(pawn, clicked, num, ref running, ref bulkRoom, claimed, list, list2);
		IntVec3 position = clicked.Position;
		while (list.Count < 24 && running < num - 0.0001f)
		{
			int take;
			Thing val2 = NearestEligible(pawn, pool, position, 12f, claimed, num, running, bulkRoom, out take);
			if (val2 == null)
			{
				break;
			}
			list.Add(val2);
			list2.Add(take);
			running += (float)take * StatExtension.GetStatValue(val2, StatDefOf.Mass, true, -1);
			bulkRoom -= (float)take * CECompat.BulkPerUnit(val2);
			position = val2.Position;
		}
		if (list.Count == 0)
		{
			if (!HasDepositableSurplus(pawn))
			{
				return null;
			}
			return JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_LoadPackAnimal, LocalTargetInfo.op_Implicit((Thing)(object)carrier));
		}
		Job val3 = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_LoadPackAnimal, LocalTargetInfo.op_Implicit((Thing)(object)carrier));
		val3.targetQueueB = new List<LocalTargetInfo>(list.Count);
		for (int i = 0; i < list.Count; i++)
		{
			val3.targetQueueB.Add(LocalTargetInfo.op_Implicit(list[i]));
		}
		val3.countQueue = new List<int>(list2);
		val3.count = 1;
		return val3;
	}

	private static void AddIfEligible(Pawn pawn, Thing t, float ceiling, ref float running, ref float bulkRoom, HashSet<Thing> claimed, List<Thing> things, List<int> counts)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (t != null && t.Spawned && !ForbidUtility.IsForbidden(t, pawn) && !claimed.Contains(t) && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, false) && ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(t), (PathEndMode)3))
		{
			int val = BulkHaulPolicy.CountWithinCeiling(ceiling, running, StatExtension.GetStatValue(t, StatDefOf.Mass, true, -1), t.stackCount);
			val = Math.Min(val, CECompat.MaxFitCount(pawn, t));
			float num = CECompat.BulkPerUnit(t);
			if (num > 0f && !float.IsPositiveInfinity(bulkRoom))
			{
				val = Math.Min(val, (int)Math.Floor(bulkRoom / num));
			}
			if (val > 0)
			{
				things.Add(t);
				counts.Add(val);
				running += (float)val * StatExtension.GetStatValue(t, StatDefOf.Mass, true, -1);
				bulkRoom -= (float)val * num;
			}
		}
	}

	private static Thing NearestEligible(Pawn pawn, List<Thing> pool, IntVec3 from, float radius, HashSet<Thing> claimed, float ceiling, float running, float bulkRoom, out int take)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		take = 0;
		float num = radius * radius;
		Thing val2;
		int val3;
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
			if (!ForbidUtility.IsForbidden(val2, pawn) && !claimed.Contains(val2))
			{
				val3 = BulkHaulPolicy.CountWithinCeiling(ceiling, running, StatExtension.GetStatValue(val2, StatDefOf.Mass, true, -1), val2.stackCount);
				val3 = Math.Min(val3, CECompat.MaxFitCount(pawn, val2));
				float num5 = CECompat.BulkPerUnit(val2);
				if (num5 > 0f && !float.IsPositiveInfinity(bulkRoom))
				{
					val3 = Math.Min(val3, (int)Math.Floor(bulkRoom / num5));
				}
				if (val3 > 0 && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, val2, false) && ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(val2), (PathEndMode)3))
				{
					break;
				}
			}
		}
		take = val3;
		return val2;
	}

	internal static float CeilingKg(Pawn pawn, HaulersDreamSettings s)
	{
		float baseCapKg = CarryMath.EffectiveCapacity(CarryCapacity.Of(pawn), s?.carryLimitFraction ?? 1f);
		return BulkHaulPolicy.CeilingKg(s?.overloadLevel ?? 0, OverloadGate.NoOverloadFor(pawn, s), baseCapKg, OverloadGate.MaxCeilingKg(s));
	}
}
