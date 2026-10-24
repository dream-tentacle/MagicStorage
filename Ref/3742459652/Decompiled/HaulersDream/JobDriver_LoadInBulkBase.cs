using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public abstract class JobDriver_LoadInBulkBase : JobDriver
{
	protected const TargetIndex TargetInd = (TargetIndex)1;

	protected const TargetIndex StackInd = (TargetIndex)2;

	protected int loadIndex;

	protected int depositLoops;

	protected int passes;

	protected const int MaxDepositLoops = 64;

	protected const int MaxPasses = 64;

	[ThreadStatic]
	protected static List<Thing> scratchTagged;

	[NonSerialized]
	protected IManagedLoadable adapter;

	[NonSerialized]
	protected bool retainClaimOnEnd;

	private Toil sweepGotoToil;

	private Toil sweepDecideToil;

	protected ThingOwner Inv
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

	protected static HaulersDreamSettings Settings => HaulersDreamMod.Settings;

	protected abstract string ToilPrefix { get; }

	public override void Notify_PatherFailed()
	{
		if (((JobDriver)this).CurToil != sweepGotoToil)
		{
			((JobDriver)this).Notify_PatherFailed();
			return;
		}
		loadIndex++;
		((JobDriver)this).JumpToToil(sweepDecideToil);
	}

	public override void ExposeData()
	{
		((JobDriver)this).ExposeData();
	}

	public override void Notify_Starting()
	{
		((JobDriver)this).Notify_Starting();
		adapter = BuildLoadable();
		if (adapter != null)
		{
			if (base.job.targetQueueB != null && base.job.targetQueueB.Count > 0)
			{
				HaulersDreamGameComponent.Instance?.LoadClaim(base.pawn, base.job, adapter);
			}
			else
			{
				HaulersDreamGameComponent.Instance?.LoadClaimCarriedSurplus(base.pawn, adapter);
			}
			OnExtraClaim();
		}
	}

	protected IManagedLoadable EnsureAdapter()
	{
		if (adapter != null)
		{
			return adapter;
		}
		adapter = BuildLoadable();
		return adapter;
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		List<LocalTargetInfo> targetQueue = base.job.GetTargetQueue((TargetIndex)2);
		if (targetQueue == null || targetQueue.Count == 0)
		{
			return true;
		}
		if (!ReservationUtility.Reserve(base.pawn, targetQueue[0], base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false))
		{
			return false;
		}
		ReservationUtility.ReserveAsManyAsPossible(base.pawn, targetQueue, base.job, 1, -1, (ReservationLayerDef)null);
		return true;
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		AddTopLevelFailConditions();
		Toil fillStart = Toils_General.Label();
		Toil depositStart = Toils_General.Label();
		Toil loopCheck = ToilMaker.MakeToil(ToilPrefix + "_LoopCheck");
		yield return fillStart;
		Toil sweepDecide = ToilMaker.MakeToil(ToilPrefix + "_SweepDecide");
		sweepDecideToil = sweepDecide;
		sweepDecide.initAction = delegate
		{
			//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_023a: Unknown result type (might be due to invalid IL or missing references)
			//IL_023f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0248: Unknown result type (might be due to invalid IL or missing references)
			//IL_0160: Unknown result type (might be due to invalid IL or missing references)
			//IL_018f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
			List<LocalTargetInfo> targetQueueB = base.job.targetQueueB;
			List<int> countQueue = base.job.countQueue;
			if (targetQueueB == null || targetQueueB.Count == 0 || loadIndex >= targetQueueB.Count)
			{
				((JobDriver)this).JumpToToil(depositStart);
			}
			else
			{
				float num = PackAnimalLoad.CeilingKg(base.pawn, Settings);
				bool flag = float.IsPositiveInfinity(num) || MassUtility.GearAndInventoryMass(base.pawn) < num - 0.0001f;
				if (flag && CECompat.IsActive && CECompat.AvailableBulk(base.pawn) <= 0f)
				{
					flag = false;
				}
				if (!flag)
				{
					((JobDriver)this).JumpToToil(depositStart);
				}
				else
				{
					LocalTargetInfo val4;
					while (loadIndex < targetQueueB.Count)
					{
						val4 = targetQueueB[loadIndex];
						Thing thing = ((LocalTargetInfo)(ref val4)).Thing;
						bool flag2 = thing != null && thing.Spawned && !ForbidUtility.IsForbidden(thing, base.pawn) && !(thing.ParentHolder is Pawn_InventoryTracker) && countQueue != null && loadIndex < countQueue.Count && countQueue[loadIndex] > 0;
						if (flag2 && !((Thing)base.pawn).Map.reservationManager.ReservedBy(LocalTargetInfo.op_Implicit(thing), base.pawn, base.job) && (!ReservationUtility.CanReserve(base.pawn, LocalTargetInfo.op_Implicit(thing), 1, -1, (ReservationLayerDef)null, false) || !ReservationUtility.Reserve(base.pawn, LocalTargetInfo.op_Implicit(thing), base.job, 1, -1, (ReservationLayerDef)null, false, false)))
						{
							flag2 = false;
						}
						if (flag2)
						{
							break;
						}
						loadIndex++;
					}
					if (loadIndex >= targetQueueB.Count)
					{
						((JobDriver)this).JumpToToil(depositStart);
					}
					else
					{
						Job job = base.job;
						val4 = targetQueueB[loadIndex];
						job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(((LocalTargetInfo)(ref val4)).Thing));
					}
				}
			}
		};
		sweepDecide.defaultCompleteMode = (ToilCompleteMode)1;
		yield return sweepDecide;
		yield return sweepGotoToil = SweepWalk.MakeToil((JobDriver)(object)this, (TargetIndex)2, ToilPrefix + "_SweepGoto", sweepDecide, delegate
		{
			loadIndex++;
		}, () => false);
		yield return PickupPause.MakeToil((TargetIndex)2, PickupDelayContext.Loading);
		Toil val = ToilMaker.MakeToil(ToilPrefix + "_SweepTake");
		val.initAction = delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_022e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0186: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)2);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			List<int> countQueue = base.job.countQueue;
			int num = ((countQueue != null && loadIndex < countQueue.Count) ? countQueue[loadIndex] : 0);
			if (thing == null || !thing.Spawned || num <= 0 || ForbidUtility.IsForbidden(thing, base.pawn))
			{
				loadIndex++;
				((JobDriver)this).JumpToToil(sweepDecide);
			}
			else
			{
				int val4 = BulkHaulPolicy.CountWithinCeiling(PackAnimalLoad.CeilingKg(base.pawn, Settings), MassUtility.GearAndInventoryMass(base.pawn), StatExtension.GetStatValue(thing, StatDefOf.Mass, true, -1), Math.Min(num, thing.stackCount));
				val4 = Math.Min(val4, CECompat.MaxFitCount(base.pawn, thing));
				if (val4 <= 0)
				{
					((JobDriver)this).JumpToToil(depositStart);
				}
				else
				{
					int stackCount = thing.stackCount;
					Thing val5 = thing.SplitOff(val4);
					ThingOwner inv = Inv;
					if (inv != null && inv.TryAdd(val5, false))
					{
						CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
						if (comp != null)
						{
							comp.RegisterHauledItem(val5);
							comp.NotifyYieldPicked();
						}
						if (!val5.Spawned)
						{
							val5.Position = ((Thing)base.pawn).Position;
						}
						if (countQueue != null && loadIndex < countQueue.Count)
						{
							countQueue[loadIndex] = num - val4;
						}
						if (countQueue == null || loadIndex >= countQueue.Count || countQueue[loadIndex] <= 0 || val4 >= stackCount)
						{
							loadIndex++;
						}
					}
					else if (val5 != null && !val5.Destroyed && !val5.Spawned)
					{
						GenPlace.TryPlaceThing(val5, ((Thing)base.pawn).Position, ((Thing)base.pawn).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
						loadIndex++;
					}
					((JobDriver)this).JumpToToil(sweepDecide);
				}
			}
		};
		val.defaultCompleteMode = (ToilCompleteMode)1;
		yield return val;
		yield return depositStart;
		Toil findTarget = ToilMaker.MakeToil(ToilPrefix + "_FindTarget");
		findTarget.initAction = delegate
		{
			if (++depositLoops > 64)
			{
				((JobDriver)this).JumpToToil(loopCheck);
			}
			else if (!FindTargetStillValid())
			{
				((JobDriver)this).JumpToToil(loopCheck);
			}
			else if (!HasDepositable())
			{
				((JobDriver)this).JumpToToil(loopCheck);
			}
		};
		findTarget.defaultCompleteMode = (ToilCompleteMode)1;
		yield return findTarget;
		Toil val2 = Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)2, false);
		ToilFailConditions.FailOnDespawnedOrNull<Toil>(val2, (TargetIndex)1);
		yield return val2;
		Toil val3 = ToilMaker.MakeToil(ToilPrefix + "_Deposit");
		val3.initAction = delegate
		{
			//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
			ThingOwner<Thing> val4 = base.pawn.inventory?.innerContainer;
			CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
			IManagedLoadable managedLoadable = EnsureAdapter();
			if (val4 == null || comp == null || managedLoadable == null || !FindTargetStillValid())
			{
				((JobDriver)this).JumpToToil(loopCheck);
			}
			else
			{
				OnPreDepositLoop(managedLoadable);
				bool movedAny = false;
				List<Thing> list = scratchTagged ?? (scratchTagged = new List<Thing>());
				list.Clear();
				list.AddRange(comp.GetHashSet());
				list.Sort((Thing a, Thing b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
				for (int num = 0; num < list.Count; num++)
				{
					Thing val5 = list[num];
					if (val5 != null && !val5.Destroyed && ((ThingOwner)val4).Contains(val5) && ReservationUtility.CanReserve(base.pawn, LocalTargetInfo.op_Implicit(val5), 1, -1, (ReservationLayerDef)null, false) && InventorySurplus.SurplusOf(base.pawn, val5) > 0)
					{
						DepositOne(val5, (ThingOwner)(object)val4, comp, managedLoadable, ref movedAny);
					}
				}
				if (!movedAny)
				{
					((JobDriver)this).JumpToToil(loopCheck);
				}
				else
				{
					((JobDriver)this).JumpToToil(findTarget);
				}
			}
		};
		val3.defaultCompleteMode = (ToilCompleteMode)1;
		yield return val3;
		loopCheck.initAction = delegate
		{
			if (++passes > 64)
			{
				((JobDriver)this).EndJobWith((JobCondition)4);
			}
			else
			{
				depositLoops = 0;
				List<LocalTargetInfo> targetQueueB = base.job.targetQueueB;
				if (targetQueueB != null && loadIndex < targetQueueB.Count)
				{
					((JobDriver)this).JumpToToil(fillStart);
				}
				else
				{
					((JobDriver)this).EndJobWith((JobCondition)2);
				}
			}
		};
		loopCheck.defaultCompleteMode = (ToilCompleteMode)1;
		yield return loopCheck;
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate(JobCondition condition)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			if (ContinuousLoad.ShouldChain(condition, base.job))
			{
				ContinuousLoad.TryChainFrom(base.pawn, EnsureAdapter());
			}
			if (!retainClaimOnEnd)
			{
				HaulersDreamGameComponent.Instance?.LoadReleaseClaimsForPawn(base.pawn);
				OnReleaseExtraClaims();
			}
			CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
			ThingOwner<Thing> val4 = base.pawn.inventory?.innerContainer;
			if (comp != null && val4 != null)
			{
				List<Thing> list = scratchTagged ?? (scratchTagged = new List<Thing>());
				list.Clear();
				list.AddRange(comp.GetHashSet());
				for (int i = 0; i < list.Count; i++)
				{
					Thing val5 = list[i];
					if (val5 != null && !val5.Destroyed && ((ThingOwner)val4).Contains(val5))
					{
						comp.RegisterHauledItem(val5);
					}
				}
			}
		});
	}

	protected abstract IManagedLoadable BuildLoadable();

	protected abstract bool HasDepositable();

	protected abstract bool FindTargetStillValid();

	protected abstract void DepositOne(Thing thing, ThingOwner inner, CompHauledToInventory hcomp, IManagedLoadable adp, ref bool movedAny);

	protected virtual void OnPreDepositLoop(IManagedLoadable adp)
	{
	}

	protected virtual void OnExtraClaim()
	{
	}

	protected virtual void OnReleaseExtraClaims()
	{
	}

	protected virtual void AddTopLevelFailConditions()
	{
		ToilFailConditions.FailOnDespawnedOrNull<JobDriver_LoadInBulkBase>(this, (TargetIndex)1);
	}
}
