using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_BulkHaul : JobDriver
{
	private struct PlannedCargo
	{
		public ThingDef def;

		public int units;

		public Thing sample;
	}

	private const TargetIndex PrimaryInd = (TargetIndex)1;

	private const TargetIndex StackInd = (TargetIndex)2;

	private int loadIndex;

	private bool loadedAnything;

	private Toil loadDecideToil;

	[ThreadStatic]
	private static List<Thing> foldScratch;

	[ThreadStatic]
	private static List<PlannedCargo> plannedScratch;

	private ThingOwner Inv
	{
		get
		{
			Pawn_InventoryTracker inventory = base.pawn.inventory;
			if (inventory == null)
			{
				return null;
			}
			return inventory.GetDirectlyHeldThings();
		}
	}

	internal bool IsStillLoading
	{
		get
		{
			if (base.job?.targetQueueB != null)
			{
				return loadIndex < base.job.targetQueueB.Count;
			}
			return false;
		}
	}

	public override void Notify_PatherFailed()
	{
		loadIndex++;
		((JobDriver)this).JumpToToil(loadDecideToil);
	}

	public override void ExposeData()
	{
		((JobDriver)this).ExposeData();
		Scribe_Values.Look<int>(ref loadIndex, "hdBulkLoadIndex", 0, false);
		Scribe_Values.Look<bool>(ref loadedAnything, "hdBulkLoadedAnything", false, false);
	}

	public override string GetReport()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.BulkHaul.Report"));
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		List<LocalTargetInfo> targetQueue = base.job.GetTargetQueue((TargetIndex)2);
		if (targetQueue == null || targetQueue.Count == 0)
		{
			return false;
		}
		if (!ReservationUtility.Reserve(base.pawn, targetQueue[0], base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false))
		{
			return false;
		}
		ReservationUtility.ReserveAsManyAsPossible(base.pawn, targetQueue, base.job, 1, -1, (ReservationLayerDef)null);
		CommitPlannedDestinations();
		return true;
	}

	private void CommitPlannedDestinations()
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		Pawn pawn = base.pawn;
		Map val = ((pawn != null) ? ((Thing)pawn).Map : null);
		if (val == null || !StorageCommitments.ActiveOn(val))
		{
			return;
		}
		List<LocalTargetInfo> targetQueueB = base.job.targetQueueB;
		List<int> countQueue = base.job.countQueue;
		if (targetQueueB == null)
		{
			return;
		}
		List<PlannedCargo> list = plannedScratch ?? (plannedScratch = new List<PlannedCargo>());
		list.Clear();
		for (int i = 0; i < targetQueueB.Count; i++)
		{
			LocalTargetInfo val2 = targetQueueB[i];
			Thing thing = ((LocalTargetInfo)(ref val2)).Thing;
			if (thing?.def == null)
			{
				continue;
			}
			int num = ((countQueue != null && i < countQueue.Count) ? countQueue[i] : 0);
			if (num > 0)
			{
				num = Math.Min(num, thing.stackCount);
				if (num > 0)
				{
					Fold(list, thing, num);
				}
			}
		}
		for (int j = 0; j < list.Count; j++)
		{
			PlannedCargo plannedCargo = list[j];
			ISlotGroup val3 = StorageEvidence.DestinationGroupFor(val, base.pawn, plannedCargo.sample);
			if (val3 != null)
			{
				if (base.job.playerForced && StorageCommitments.FreeUnitsFor(base.pawn, val3, plannedCargo.def, plannedCargo.sample) <= 0)
				{
					StorageCommitments.InterruptCommittersTo(val3, plannedCargo.def, base.pawn);
				}
				int units = Math.Max(plannedCargo.units, StorageCommitments.UnitsMovingOf(base.pawn, plannedCargo.def));
				StorageCommitments.Commit(base.pawn, val3, plannedCargo.def, units, "bulk-sweep");
			}
		}
		list.Clear();
	}

	private static void Fold(List<PlannedCargo> planned, Thing t, int units)
	{
		for (int i = 0; i < planned.Count; i++)
		{
			if (planned[i].def == t.def)
			{
				PlannedCargo value = planned[i];
				value.units += units;
				if (t.thingIDNumber < value.sample.thingIDNumber)
				{
					value.sample = t;
				}
				planned[i] = value;
				return;
			}
		}
		planned.Add(new PlannedCargo
		{
			def = t.def,
			units = units,
			sample = t
		});
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate
		{
			if (loadedAnything)
			{
				PawnUnloadChecker.CheckIfShouldUnload(base.pawn, forced: true, behindQueuedWork: true);
			}
		});
		Toil end = Toils_General.Label();
		Toil loadDecide = ToilMaker.MakeToil("HD_Bulk_LoadDecide");
		loadDecideToil = loadDecide;
		loadDecide.initAction = delegate
		{
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0284: Unknown result type (might be due to invalid IL or missing references)
			//IL_0289: Unknown result type (might be due to invalid IL or missing references)
			//IL_0292: Unknown result type (might be due to invalid IL or missing references)
			//IL_019c: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
			List<LocalTargetInfo> targetQueueB = base.job.targetQueueB;
			List<int> countQueue = base.job.countQueue;
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			float num = CeilingKgLive(settings);
			bool flag = float.IsPositiveInfinity(num) || BulkHaul.BudgetedCarriedMassKg(base.pawn, CorpseAllowance(settings)) < num - 0.0001f;
			if (flag && CECompat.IsActive && CECompat.AvailableBulk(base.pawn) <= 0f)
			{
				flag = false;
			}
			LocalTargetInfo val2;
			while (flag && targetQueueB != null && loadIndex < targetQueueB.Count)
			{
				val2 = targetQueueB[loadIndex];
				Thing thing = ((LocalTargetInfo)(ref val2)).Thing;
				bool flag2 = thing != null && (!ForbidUtility.IsForbidden(thing, base.pawn) || SweepForbidPolicy.MayTakeWhileForbidden(base.job.playerForced, loadIndex == 0));
				bool flag3 = thing != null && thing.Spawned && flag2 && !(thing.ParentHolder is Pawn_InventoryTracker) && countQueue != null && loadIndex < countQueue.Count && countQueue[loadIndex] > 0 && (loadIndex == 0 || !StoreUtility.IsInValidBestStorage(thing));
				if (flag3 && ShouldSkipRimIOTRetarget(thing, loadIndex))
				{
					flag3 = false;
				}
				if (flag3 && !((Thing)base.pawn).Map.reservationManager.ReservedBy(LocalTargetInfo.op_Implicit(thing), base.pawn, base.job) && (!ReservationUtility.CanReserve(base.pawn, LocalTargetInfo.op_Implicit(thing), 1, -1, (ReservationLayerDef)null, false) || !ReservationUtility.Reserve(base.pawn, LocalTargetInfo.op_Implicit(thing), base.job, 1, -1, (ReservationLayerDef)null, false, false)))
				{
					flag3 = false;
				}
				if (flag3)
				{
					break;
				}
				loadIndex++;
			}
			if (!flag || targetQueueB == null || loadIndex >= targetQueueB.Count)
			{
				((JobDriver)this).JumpToToil(end);
			}
			else
			{
				Job job = base.job;
				val2 = targetQueueB[loadIndex];
				job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(((LocalTargetInfo)(ref val2)).Thing));
			}
		};
		loadDecide.defaultCompleteMode = (ToilCompleteMode)1;
		yield return loadDecide;
		yield return SweepWalk.MakeToil((JobDriver)(object)this, (TargetIndex)2, "HD_Bulk_LoadGoto", loadDecide, delegate
		{
			loadIndex++;
		}, () => loadIndex == 0);
		yield return PickupPause.MakeToil((TargetIndex)2, (base.job.takeInventoryDelay <= 0) ? PickupDelayContext.AutoHaul : PickupDelayContext.ManualCarry);
		Toil val = ToilMaker.MakeToil("HD_Bulk_Take");
		val.initAction = delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)2);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			List<int> countQueue = base.job.countQueue;
			int num = ((countQueue != null && loadIndex < countQueue.Count) ? countQueue[loadIndex] : 0);
			if (thing == null || !thing.Spawned || num <= 0)
			{
				loadIndex++;
				((JobDriver)this).JumpToToil(loadDecide);
			}
			else if (ForbidUtility.IsForbidden(thing, base.pawn) && !SweepForbidPolicy.MayTakeWhileForbidden(base.job.playerForced, loadIndex == 0))
			{
				loadIndex++;
				((JobDriver)this).JumpToToil(loadDecide);
			}
			else if (loadIndex != 0 && StoreUtility.IsInValidBestStorage(thing))
			{
				loadIndex++;
				((JobDriver)this).JumpToToil(loadDecide);
			}
			else if (ShouldSkipRimIOTRetarget(thing, loadIndex))
			{
				loadIndex++;
				((JobDriver)this).JumpToToil(loadDecide);
			}
			else
			{
				Corpse val2 = (Corpse)(object)((thing is Corpse) ? thing : null);
				if (val2 != null)
				{
					CorpseStripper.MaybeStripForHaul(base.pawn, val2);
				}
				HaulersDreamSettings settings = HaulersDreamMod.Settings;
				float corpseCarryAllowance = CorpseAllowance(settings);
				int val3 = BulkHaulPolicy.CountWithinCeiling(CeilingKgLive(settings), BulkHaul.BudgetedCarriedMassKg(base.pawn, corpseCarryAllowance), CorpseHaulPolicy.BudgetMassKg(StatExtension.GetStatValue(thing, StatDefOf.Mass, true, -1), thing is Corpse, corpseCarryAllowance), Math.Min(num, thing.stackCount));
				val3 = Math.Min(val3, CECompat.MaxFitCount(base.pawn, thing));
				if (val3 <= 0)
				{
					loadIndex++;
					((JobDriver)this).JumpToToil(loadDecide);
				}
				else
				{
					Thing split = thing.SplitOff(val3);
					if (DepositSwept(split))
					{
						loadedAnything = true;
					}
					loadIndex++;
					((JobDriver)this).JumpToToil(loadDecide);
				}
			}
		};
		val.defaultCompleteMode = (ToilCompleteMode)1;
		yield return val;
		yield return end;
	}

	private bool ShouldSkipRimIOTRetarget(Thing t, int loadIndex)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (t == null || base.job.playerForced || !RimIOTCompat.IsPresent || !RimIOTCompat.IsRimIOTHandledCell(((Thing)base.pawn).Map, t.Position))
		{
			return false;
		}
		if (loadIndex == 0)
		{
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing != null && thing != t)
			{
				HaulChurnGuard.NoteForeignRetarget(base.pawn, thing, t);
			}
		}
		return true;
	}

	private bool DepositSwept(Thing split)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		if (split == null || split.Destroyed || split.stackCount <= 0)
		{
			return false;
		}
		ThingOwner inv = Inv;
		if (inv == null)
		{
			if (!split.Spawned)
			{
				GenPlace.TryPlaceThing(split, ((Thing)base.pawn).Position, ((Thing)base.pawn).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			}
			return false;
		}
		CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
		bool flag = false;
		if (comp != null)
		{
			List<Thing> list = foldScratch ?? (foldScratch = new List<Thing>());
			list.Clear();
			foreach (Thing item in comp.PeekHashSet())
			{
				if (item != null)
				{
					list.Add(item);
				}
			}
			list.Sort((Thing a, Thing b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
			for (int num = 0; num < list.Count; num++)
			{
				Thing val = list[num];
				if (split.Destroyed || split.stackCount <= 0)
				{
					break;
				}
				if (val != split && !val.Destroyed && val.def == split.def && inv.Contains(val) && val.stackCount < val.def.stackLimit && val.CanStackWith(split))
				{
					int stackCount = split.stackCount;
					val.TryAbsorbStack(split, true);
					int num2 = stackCount - split.stackCount;
					if (num2 > 0)
					{
						comp.RegisterHauledItem(val, num2);
						flag = true;
					}
				}
			}
			list.Clear();
		}
		if (!split.Destroyed && split.stackCount > 0)
		{
			if (inv.TryAdd(split, false))
			{
				comp?.RegisterHauledItem(split);
				if (!split.Spawned)
				{
					split.Position = ((Thing)base.pawn).Position;
				}
				flag = true;
			}
			else if (!split.Destroyed && !split.Spawned)
			{
				GenPlace.TryPlaceThing(split, ((Thing)base.pawn).Position, ((Thing)base.pawn).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			}
		}
		if (flag)
		{
			comp?.NotifyYieldPicked();
		}
		return flag;
	}

	private static float CorpseAllowance(HaulersDreamSettings s)
	{
		return s?.corpseCarryAllowance ?? 1f;
	}

	private float CeilingKgLive(HaulersDreamSettings s)
	{
		if (s == null)
		{
			return CarryMath.EffectiveCapacity(CarryCapacity.Of(base.pawn), 1f);
		}
		float baseCapKg = CarryMath.EffectiveCapacity(CarryCapacity.Of(base.pawn), s.carryLimitFraction);
		return BulkHaulPolicy.CeilingKg(s.overloadLevel, OverloadGate.NoOverloadFor(base.pawn, s), baseCapKg, OverloadGate.MaxCeilingKg(s));
	}
}
