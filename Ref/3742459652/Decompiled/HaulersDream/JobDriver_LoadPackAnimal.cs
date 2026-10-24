using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_LoadPackAnimal : JobDriver
{
	private const TargetIndex CarrierInd = (TargetIndex)1;

	private const TargetIndex StackInd = (TargetIndex)2;

	private int loadIndex;

	private int depositLoops;

	private int passes;

	private const int MaxDepositLoops = 64;

	private const int MaxPasses = 64;

	[ThreadStatic]
	private static List<Thing> scratchTagged;

	private Toil sweepGotoToil;

	private Toil sweepDecideToil;

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

	private Pawn Carrier
	{
		get
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			return (Pawn)(object)((thing is Pawn) ? thing : null);
		}
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

	public override void ExposeData()
	{
		((JobDriver)this).ExposeData();
		Scribe_Values.Look<int>(ref loadIndex, "hdLpaLoadIndex", 0, false);
		Scribe_Values.Look<int>(ref depositLoops, "hdLpaDepositLoops", 0, false);
		Scribe_Values.Look<int>(ref passes, "hdLpaPasses", 0, false);
	}

	public override string GetReport()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.LoadPackAnimal.Report"));
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
		Toil fillStart = Toils_General.Label();
		Toil depositStart = Toils_General.Label();
		Toil loopCheck = ToilMaker.MakeToil("HD_Lpa_LoopCheck");
		yield return fillStart;
		Toil sweepDecide = ToilMaker.MakeToil("HD_Lpa_SweepDecide");
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
				float num = PackAnimalLoad.CeilingKg(base.pawn, HaulersDreamMod.Settings);
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
		yield return sweepGotoToil = SweepWalk.MakeToil((JobDriver)(object)this, (TargetIndex)2, "HD_Lpa_SweepGoto", sweepDecide, delegate
		{
			loadIndex++;
		}, () => false);
		yield return PickupPause.MakeToil((TargetIndex)2, PickupDelayContext.Loading);
		Toil val = ToilMaker.MakeToil("HD_Lpa_SweepTake");
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
				int val4 = BulkHaulPolicy.CountWithinCeiling(PackAnimalLoad.CeilingKg(base.pawn, HaulersDreamMod.Settings), MassUtility.GearAndInventoryMass(base.pawn), StatExtension.GetStatValue(thing, StatDefOf.Mass, true, -1), Math.Min(num, thing.stackCount));
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
		Toil findCarrier = ToilMaker.MakeToil("HD_Lpa_FindCarrier");
		findCarrier.initAction = delegate
		{
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			if (!PackAnimalLoad.HasDepositableSurplus(base.pawn))
			{
				((JobDriver)this).JumpToToil(loopCheck);
			}
			else if (++depositLoops > 64)
			{
				((JobDriver)this).EndJobWith((JobCondition)4);
			}
			else
			{
				Pawn val4 = PackAnimalLoad.FindCarrier(base.pawn);
				if (val4 == null || MassUtility.FreeSpace(val4) <= 0f)
				{
					((JobDriver)this).EndJobWith((JobCondition)2);
				}
				else
				{
					base.job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit((Thing)(object)val4));
				}
			}
		};
		findCarrier.defaultCompleteMode = (ToilCompleteMode)1;
		yield return findCarrier;
		Toil val2 = Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)2, false);
		ToilFailConditions.FailOnDespawnedOrNull<Toil>(val2, (TargetIndex)1);
		ToilJumpConditions.JumpIf(val2, (Func<bool>)delegate
		{
			Pawn carrier = Carrier;
			return carrier == null || carrier.Dead || MassUtility.FreeSpace(carrier) <= 0f;
		}, findCarrier);
		yield return val2;
		Toil val3 = ToilMaker.MakeToil("HD_Lpa_Deposit");
		val3.initAction = delegate
		{
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
			Pawn carrier = Carrier;
			ThingOwner<Thing> val4 = base.pawn.inventory?.innerContainer;
			CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
			if (carrier == null || !((Thing)carrier).Spawned || carrier.Dead)
			{
				((JobDriver)this).JumpToToil(findCarrier);
			}
			else if (comp == null || val4 == null)
			{
				((JobDriver)this).EndJobWith((JobCondition)2);
			}
			else
			{
				ThingOwner<Thing> innerContainer = carrier.inventory.innerContainer;
				bool flag = false;
				List<Thing> list = scratchTagged ?? (scratchTagged = new List<Thing>());
				list.Clear();
				list.AddRange(comp.GetHashSet());
				list.Sort((Thing a, Thing b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
				Thing val7 = default(Thing);
				for (int num = 0; num < list.Count; num++)
				{
					Thing val5 = list[num];
					if (val5 != null && !val5.Destroyed && ((ThingOwner)val4).Contains(val5) && ReservationUtility.CanReserve(base.pawn, LocalTargetInfo.op_Implicit(val5), 1, -1, (ReservationLayerDef)null, false))
					{
						int num2 = InventorySurplus.SurplusOf(base.pawn, val5);
						if (num2 > 0)
						{
							int num3 = PackAnimalLoadPolicy.DepositCountWithinFreeSpace(MassUtility.FreeSpace(carrier), StatExtension.GetStatValue(val5, StatDefOf.Mass, true, -1), num2);
							if (num3 > 0)
							{
								int num4;
								if (HaulersDreamMod.Settings != null && HaulersDreamMod.Settings.enableVehicleFramework && VehicleFrameworkCompat.IsVehicle((Thing)(object)carrier))
								{
									Thing val6 = val4.Take(val5, num3);
									num4 = VehicleFrameworkCompat.AddOrTransfer((Thing)(object)carrier, val6, num3);
									if (num4 <= 0)
									{
										int stackCount = val6.stackCount;
										if (((ThingOwner)innerContainer).TryAdd(val6, true))
										{
											num4 = stackCount;
										}
										else if (!((ThingOwner)val4).TryAdd(val6, true))
										{
											GenPlace.TryPlaceThing(val6, ((Thing)base.pawn).Position, ((Thing)base.pawn).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
										}
									}
								}
								else
								{
									num4 = val4.TryTransferToContainer(val5, (ThingOwner)(object)innerContainer, num3, ref val7, true);
								}
								if (num4 > 0)
								{
									flag = true;
									if (!((ThingOwner)val4).Contains(val5))
									{
										comp.Deregister(val5);
									}
								}
							}
						}
					}
				}
				if (!flag)
				{
					((JobDriver)this).EndJobWith((JobCondition)4);
				}
				else
				{
					((JobDriver)this).JumpToToil(findCarrier);
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
	}
}
