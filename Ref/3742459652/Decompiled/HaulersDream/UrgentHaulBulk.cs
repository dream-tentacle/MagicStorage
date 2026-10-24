using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class UrgentHaulBulk
{
	private const int MaxStacks = 24;

	[ThreadStatic]
	private static List<Thing> scratchUrgentNear;

	[ThreadStatic]
	private static List<UrgentVicinityPolicy.Candidate> scratchCandidates;

	[ThreadStatic]
	private static Dictionary<int, Thing> scratchById;

	[ThreadStatic]
	private static HashSet<Thing> scratchOwnTargets;

	internal static Job TryBuild(Pawn pawn, Thing primary, Job vanillaJob, bool forced, bool includeNonUrgent)
	{
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_0404: Unknown result type (might be due to invalid IL or missing references)
		//IL_0427: Unknown result type (might be due to invalid IL or missing references)
		//IL_0477: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null || primary == null || !primary.Spawned)
		{
			return null;
		}
		HaulersDreamSettings s = HaulersDreamMod.Settings;
		Map map = ((Thing)pawn).Map;
		if (s == null || map == null || !s.bulkHaulUrgent)
		{
			return null;
		}
		if (!UrgentHaulCompat.AnyUrgentDefResolved)
		{
			return null;
		}
		if (!MapGate.HdActiveOnMap(map))
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
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp == null || pawn.inventory == null)
		{
			return null;
		}
		if (vanillaJob == null || (vanillaJob.def != JobDefOf.HaulToCell && vanillaJob.def != JobDefOf.HaulToContainer))
		{
			return null;
		}
		if (primary is Corpse)
		{
			return null;
		}
		if (!forced)
		{
			if (!MasterEnable.Active)
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
			if (ForeignOrderGuard.ClaimedByForeignOrder(primary))
			{
				return null;
			}
			if (RimIOTCompat.IsPresent && RimIOTCompat.IsRimIOTHandledCell(map, primary.Position))
			{
				return null;
			}
		}
		int num = BulkHaul.MassClampedTake(pawn, primary, primary.stackCount, s);
		if (num <= 0)
		{
			return null;
		}
		float baseCapKg = CarryMath.EffectiveCapacity(CarryCapacity.Of(pawn), s.carryLimitFraction);
		float ceilingKg = BulkHaulPolicy.CeilingKg(s.overloadLevel, OverloadGate.NoOverloadFor(pawn, s), baseCapKg, OverloadGate.MaxCeilingKg(s));
		float runningMassKg = MassUtility.GearAndInventoryMass(pawn) + (float)num * StatExtension.GetStatValue(primary, StatDefOf.Mass, true, -1);
		float num2 = Math.Max(1, s.bulkHaulUrgentRadius);
		float radiusSq = num2 * num2;
		Dictionary<int, Thing> byId = scratchById ?? (scratchById = new Dictionary<int, Thing>());
		List<UrgentVicinityPolicy.Candidate> candidates = scratchCandidates ?? (scratchCandidates = new List<UrgentVicinityPolicy.Candidate>());
		byId.Clear();
		candidates.Clear();
		HashSet<Thing> claimed = RouteSelection.ClaimedByOtherPawns(pawn);
		HashSet<Thing> ownTargets = scratchOwnTargets ?? (scratchOwnTargets = new HashSet<Thing>());
		FillOwnJobTargets(pawn, ownTargets);
		List<Thing> list = scratchUrgentNear ?? (scratchUrgentNear = new List<Thing>());
		UrgentHaulCompat.CollectUrgentNear(map, primary.Position, radiusSq, list);
		for (int i = 0; i < list.Count; i++)
		{
			Vet(list[i], isUrgent: true, requireBetterStorage: false);
		}
		if (includeNonUrgent)
		{
			ICollection<Thing> collection = map.listerHaulables.ThingsPotentiallyNeedingHauling();
			if (collection is HashSet<Thing> hashSet)
			{
				foreach (Thing item in hashSet)
				{
					Vet(item, isUrgent: false, requireBetterStorage: true);
				}
			}
			else
			{
				foreach (Thing item2 in collection)
				{
					Vet(item2, isUrgent: false, requireBetterStorage: true);
				}
			}
		}
		List<UrgentVicinityPolicy.UrgentTake> list2 = UrgentVicinityPolicy.Select(candidates, radiusSq, includeNonUrgent, ceilingKg, runningMassKg, 23);
		if (list2.Count == 0)
		{
			Pawn_CarryTracker carryTracker = pawn.carryTracker;
			int handCap = ((carryTracker != null) ? carryTracker.MaxStackSpaceEver(primary.def) : primary.def.stackLimit);
			if (!s.haulOversizedInInventory || !BulkHaulPolicy.OversizedStackWorthInventory(primary.stackCount, handCap, num))
			{
				return null;
			}
		}
		Job val = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_BulkHaul, LocalTargetInfo.op_Implicit(primary));
		val.targetQueueB = new List<LocalTargetInfo>(list2.Count + 1) { LocalTargetInfo.op_Implicit(primary) };
		val.countQueue = new List<int>(list2.Count + 1) { num };
		for (int j = 0; j < list2.Count; j++)
		{
			val.targetQueueB.Add(LocalTargetInfo.op_Implicit(byId[list2[j].ThingId]));
			val.countQueue.Add(list2[j].Take);
		}
		val.count = 1;
		HDLog.Dbg($"UrgentHaul: {pawn} sweeping {val.targetQueueB.Count} urgent stack(s) " + $"(includeNonUrgent={includeNonUrgent}, forced={forced}).");
		return val;
		void Vet(Thing t, bool isUrgent, bool requireBetterStorage)
		{
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_0130: Unknown result type (might be due to invalid IL or missing references)
			if (t != null && t != primary && t.Spawned && t.Map == map && !(t is Corpse) && t.def != null && t.def.EverHaulable)
			{
				IntVec3 val2 = t.Position - primary.Position;
				int lengthHorizontalSquared = ((IntVec3)(ref val2)).LengthHorizontalSquared;
				if (!((float)lengthHorizontalSquared > radiusSq) && !byId.ContainsKey(t.thingIDNumber) && (!RimIOTCompat.IsPresent || !RimIOTCompat.IsRimIOTHandledCell(map, t.Position)) && !CorpseStripper.ShouldLeaveTaintedApparel(t, s) && !ForbidUtility.IsForbidden(t, pawn) && !claimed.Contains(t) && !ownTargets.Contains(t) && !ForeignOrderGuard.ClaimedByForeignOrder(t))
				{
					int num3 = Math.Min(t.stackCount, CECompat.MaxFitCount(pawn, t));
					IHaulDestination val3 = default(IHaulDestination);
					if (num3 > 0 && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, false) && ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(t), (PathEndMode)3) && (!requireBetterStorage || StoreUtility.TryFindBestBetterStorageFor(t, pawn, map, StoreUtility.CurrentStoragePriorityOf(t, false), ((Thing)pawn).Faction, ref val2, ref val3, false)))
					{
						byId[t.thingIDNumber] = t;
						candidates.Add(new UrgentVicinityPolicy.Candidate(t.thingIDNumber, lengthHorizontalSquared, isUrgent, StatExtension.GetStatValue(t, StatDefOf.Mass, true, -1), num3));
					}
				}
			}
		}
	}

	private static void FillOwnJobTargets(Pawn pawn, HashSet<Thing> set)
	{
		set.Clear();
		if (pawn?.jobs == null)
		{
			return;
		}
		AddJobTargets(set, pawn.CurJob);
		JobQueue jobQueue = pawn.jobs.jobQueue;
		if (jobQueue != null)
		{
			for (int i = 0; i < jobQueue.Count; i++)
			{
				AddJobTargets(set, jobQueue[i]?.job);
			}
		}
	}

	private static void AddJobTargets(HashSet<Thing> set, Job job)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		if (job == null)
		{
			return;
		}
		if (((LocalTargetInfo)(ref job.targetA)).Thing != null)
		{
			set.Add(((LocalTargetInfo)(ref job.targetA)).Thing);
		}
		if (((LocalTargetInfo)(ref job.targetB)).Thing != null)
		{
			set.Add(((LocalTargetInfo)(ref job.targetB)).Thing);
		}
		List<LocalTargetInfo> targetQueueB = job.targetQueueB;
		if (targetQueueB == null)
		{
			return;
		}
		for (int i = 0; i < targetQueueB.Count; i++)
		{
			LocalTargetInfo val = targetQueueB[i];
			if (((LocalTargetInfo)(ref val)).Thing != null)
			{
				val = targetQueueB[i];
				set.Add(((LocalTargetInfo)(ref val)).Thing);
			}
		}
	}
}
