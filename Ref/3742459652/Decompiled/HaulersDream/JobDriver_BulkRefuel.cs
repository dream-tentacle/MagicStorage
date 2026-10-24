using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_BulkRefuel : JobDriver
{
	private const TargetIndex RefuelableInd = (TargetIndex)1;

	private const TargetIndex FuelInd = (TargetIndex)2;

	private int loadIndex;

	[ThreadStatic]
	private static List<Thing> scratchFuel;

	private Toil sweepGotoToil;

	private Toil sweepDecideToil;

	private Thing Refuelable
	{
		get
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
			return ((LocalTargetInfo)(ref target)).Thing;
		}
	}

	private CompRefuelable Comp
	{
		get
		{
			Thing refuelable = Refuelable;
			if (refuelable == null)
			{
				return null;
			}
			return ThingCompUtility.TryGetComp<CompRefuelable>(refuelable);
		}
	}

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

	public override void ExposeData()
	{
		((JobDriver)this).ExposeData();
		Scribe_Values.Look<int>(ref loadIndex, "hdBrefLoadIndex", 0, false);
	}

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

	public override string GetReport()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.BulkRefuel.Report"));
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		if (!ReservationUtility.Reserve(base.pawn, LocalTargetInfo.op_Implicit(Refuelable), base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false))
		{
			return false;
		}
		List<LocalTargetInfo> targetQueue = base.job.GetTargetQueue((TargetIndex)2);
		if (targetQueue != null && targetQueue.Count > 0)
		{
			ReservationUtility.ReserveAsManyAsPossible(base.pawn, targetQueue, base.job, 1, -1, (ReservationLayerDef)null);
		}
		return true;
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		ToilFailConditions.FailOnDespawnedOrNull<JobDriver_BulkRefuel>(this, (TargetIndex)1);
		ToilFailConditions.FailOn<JobDriver_BulkRefuel>(this, (Func<bool>)delegate
		{
			CompRefuelable comp = Comp;
			return comp == null || comp.IsFull;
		});
		Toil depositStart = Toils_General.Label();
		Toil sweepDecide = ToilMaker.MakeToil("HD_Bref_SweepDecide");
		sweepDecideToil = sweepDecide;
		sweepDecide.initAction = delegate
		{
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_012c: Unknown result type (might be due to invalid IL or missing references)
			List<LocalTargetInfo> targetQueueB = base.job.targetQueueB;
			List<int> countQueue = base.job.countQueue;
			if (targetQueueB == null || targetQueueB.Count == 0 || loadIndex >= targetQueueB.Count)
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
					bool flag = thing != null && thing.Spawned && !ForbidUtility.IsForbidden(thing, base.pawn) && !(thing.ParentHolder is Pawn_InventoryTracker) && countQueue != null && loadIndex < countQueue.Count && countQueue[loadIndex] > 0;
					if (flag && !((Thing)base.pawn).Map.reservationManager.ReservedBy(LocalTargetInfo.op_Implicit(thing), base.pawn, base.job) && (!ReservationUtility.CanReserve(base.pawn, LocalTargetInfo.op_Implicit(thing), 1, -1, (ReservationLayerDef)null, false) || !ReservationUtility.Reserve(base.pawn, LocalTargetInfo.op_Implicit(thing), base.job, 1, -1, (ReservationLayerDef)null, false, false)))
					{
						flag = false;
					}
					if (flag)
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
		};
		sweepDecide.defaultCompleteMode = (ToilCompleteMode)1;
		yield return sweepDecide;
		yield return sweepGotoToil = SweepWalk.MakeToil((JobDriver)(object)this, (TargetIndex)2, "HD_Bref_SweepGoto", sweepDecide, delegate
		{
			loadIndex++;
		}, () => false);
		yield return PickupPause.MakeToil((TargetIndex)2, PickupDelayContext.AutoHaul);
		Toil val = ToilMaker.MakeToil("HD_Bref_SweepTake");
		val.initAction = delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
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
				int num2 = Math.Min(num, thing.stackCount);
				if (num2 <= 0)
				{
					loadIndex++;
					((JobDriver)this).JumpToToil(sweepDecide);
				}
				else
				{
					int stackCount = thing.stackCount;
					Thing val4 = thing.SplitOff(num2);
					ThingOwner inv = Inv;
					if (inv != null && inv.TryAdd(val4, false))
					{
						CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
						if (comp != null)
						{
							comp.RegisterHauledItem(val4);
							comp.NotifyYieldPicked();
						}
						if (!val4.Spawned)
						{
							val4.Position = ((Thing)base.pawn).Position;
						}
						if (countQueue != null && loadIndex < countQueue.Count)
						{
							countQueue[loadIndex] = num - num2;
						}
						if (countQueue == null || loadIndex >= countQueue.Count || countQueue[loadIndex] <= 0 || num2 >= stackCount)
						{
							loadIndex++;
						}
					}
					else if (val4 != null && !val4.Destroyed && !val4.Spawned)
					{
						GenPlace.TryPlaceThing(val4, ((Thing)base.pawn).Position, ((Thing)base.pawn).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
						loadIndex++;
					}
					((JobDriver)this).JumpToToil(sweepDecide);
				}
			}
		};
		val.defaultCompleteMode = (ToilCompleteMode)1;
		yield return val;
		yield return depositStart;
		Toil val2 = Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)2, false);
		ToilFailConditions.FailOnDespawnedOrNull<Toil>(val2, (TargetIndex)1);
		yield return val2;
		Toil val3 = ToilMaker.MakeToil("HD_Bref_Deposit");
		val3.initAction = delegate
		{
			CompRefuelable comp = Comp;
			ThingOwner<Thing> val4 = base.pawn.inventory?.innerContainer;
			CompHauledToInventory comp2 = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
			if (comp != null && !comp.IsFull && val4 != null && comp2 != null)
			{
				ThingFilter val5 = comp.Props?.fuelFilter;
				if (val5 != null)
				{
					List<Thing> list = scratchFuel ?? (scratchFuel = new List<Thing>());
					list.Clear();
					for (int i = 0; i < ((ThingOwner)val4).Count; i++)
					{
						Thing val6 = val4[i];
						if (val6 != null && !val6.Destroyed && val6.def != null && val5.Allows(val6) && InventorySurplus.SurplusOf(base.pawn, val6) > 0)
						{
							list.Add(val6);
						}
					}
					if (list.Count != 0)
					{
						int count = list.Count;
						List<Thing> list2 = new List<Thing>(list);
						comp.Refuel(list);
						for (int j = 0; j < list2.Count; j++)
						{
							Thing val7 = list2[j];
							if (val7 != null && (val7.Destroyed || !((ThingOwner)val4).Contains(val7)))
							{
								comp2.Deregister(val7);
							}
						}
						list.Clear();
						object[] obj = new object[4] { base.pawn, null, null, null };
						Thing refuelable = Refuelable;
						obj[1] = ((refuelable != null) ? ((Entity)refuelable).LabelShort : null);
						obj[2] = count;
						obj[3] = comp.Fuel;
						HDLog.Dbg(string.Format("BulkRefuel: {0} deposited fuel into {1} (from {2} carried stack(s), fuel now {3:0.#}).", obj));
					}
				}
			}
		};
		val3.defaultCompleteMode = (ToilCompleteMode)1;
		yield return val3;
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate
		{
			CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
			ThingOwner<Thing> val4 = base.pawn.inventory?.innerContainer;
			if (comp != null && val4 != null)
			{
				List<Thing> list = scratchFuel ?? (scratchFuel = new List<Thing>());
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
				list.Clear();
			}
		});
	}
}
