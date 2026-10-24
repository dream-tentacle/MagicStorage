using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_BillPrepGather : JobDriver
{
	private const TargetIndex BenchInd = (TargetIndex)1;

	private const TargetIndex StackInd = (TargetIndex)2;

	private int loadIndex;

	private bool loadedAnything;

	private ThingDef loadDef;

	private int plannedTake;

	private int invCountBeforeTake;

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
		Scribe_Values.Look<int>(ref loadIndex, "hdPrepLoadIndex", 0, false);
		Scribe_Values.Look<bool>(ref loadedAnything, "hdPrepLoadedAnything", false, false);
		Scribe_Defs.Look<ThingDef>(ref loadDef, "hdPrepLoadDef");
	}

	public override string GetReport()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.PrepGather.Report"));
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		if (!ReservationUtility.Reserve(base.pawn, base.job.GetTarget((TargetIndex)1), base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false))
		{
			return false;
		}
		ReservationUtility.ReserveAsManyAsPossible(base.pawn, base.job.GetTargetQueue((TargetIndex)2), base.job, 1, -1, (ReservationLayerDef)null);
		return true;
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		ToilFailConditions.FailOnDespawnedNullOrForbidden<JobDriver_BillPrepGather>(this, (TargetIndex)1);
		ToilFailConditions.FailOn<JobDriver_BillPrepGather>(this, (Func<bool>)(() => base.job.bill == null || base.job.bill.DeletedOrDereferenced || base.job.bill.suspended));
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate
		{
			if (!loadedAnything)
			{
				BillPrepTracker.NoteEmptyRun(base.pawn);
			}
		});
		Toil gotoBench = Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)4, false);
		Toil loadDecide = ToilMaker.MakeToil("HD_Prep_LoadDecide");
		loadDecide.initAction = delegate
		{
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
			List<LocalTargetInfo> targetQueueB = base.job.targetQueueB;
			List<int> countQueue = base.job.countQueue;
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			bool flag = settings != null && OverloadGate.NoOverload(settings);
			LocalTargetInfo val2;
			while (targetQueueB != null && loadIndex < targetQueueB.Count)
			{
				val2 = targetQueueB[loadIndex];
				Thing thing = ((LocalTargetInfo)(ref val2)).Thing;
				if (thing != null && thing.Spawned && !ForbidUtility.IsForbidden(thing, base.pawn) && !(thing.ParentHolder is Pawn_InventoryTracker) && countQueue != null && loadIndex < countQueue.Count && countQueue[loadIndex] > 0 && (ReservationUtility.CanReserve(base.pawn, LocalTargetInfo.op_Implicit(thing), 1, -1, (ReservationLayerDef)null, false) || ((Thing)base.pawn).Map.reservationManager.ReservedBy(LocalTargetInfo.op_Implicit(thing), base.pawn, base.job)) && (!flag || OverloadGate.CountToPickUp(base.pawn, thing, settings) > 0))
				{
					break;
				}
				loadIndex++;
			}
			if (targetQueueB == null || loadIndex >= targetQueueB.Count)
			{
				((JobDriver)this).JumpToToil(gotoBench);
			}
			else
			{
				JobDriver_BillPrepGather jobDriver_BillPrepGather = this;
				val2 = targetQueueB[loadIndex];
				jobDriver_BillPrepGather.loadDef = ((LocalTargetInfo)(ref val2)).Thing.def;
				Job job = base.job;
				val2 = targetQueueB[loadIndex];
				job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(((LocalTargetInfo)(ref val2)).Thing));
			}
		};
		loadDecide.defaultCompleteMode = (ToilCompleteMode)1;
		yield return loadDecide;
		yield return SweepWalk.MakeToil((JobDriver)(object)this, (TargetIndex)2, "HD_Prep_LoadGoto", loadDecide, delegate
		{
			loadIndex++;
		}, () => false);
		yield return Toils_Haul.TakeToInventory((TargetIndex)2, (Func<int>)delegate
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			plannedTake = 0;
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)2);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing == null || !thing.Spawned)
			{
				return 0;
			}
			if (ForbidUtility.IsForbidden(thing, base.pawn) && !SweepForbidPolicy.MayTakeWhileForbidden(base.job.playerForced, isOrderedAnchor: false))
			{
				return 0;
			}
			List<int> countQueue = base.job.countQueue;
			int num = ((countQueue != null && loadIndex < countQueue.Count) ? countQueue[loadIndex] : 0);
			if (num <= 0)
			{
				return 0;
			}
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings != null && OverloadGate.NoOverload(settings))
			{
				num = Mathf.Min(num, OverloadGate.CountToPickUp(base.pawn, thing, settings));
			}
			plannedTake = Mathf.Min(num, thing.stackCount);
			invCountBeforeTake = YieldRouter.InventoryCountOfDef(Inv, thing.def);
			return plannedTake;
		});
		Toil val = ToilMaker.MakeToil("HD_Prep_Tag");
		val.initAction = delegate
		{
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			if (plannedTake > 0 && loadDef != null)
			{
				Thing val2 = YieldRouter.InventoryStackOfDef(Inv, loadDef);
				if (val2 != null)
				{
					CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
					if (comp != null)
					{
						int num = YieldRouter.InventoryCountOfDef(Inv, loadDef) - invCountBeforeTake;
						comp.RegisterHauledItem(val2, (num > 0) ? num : 0);
						comp.NotifyYieldPicked();
					}
					if (!val2.Spawned)
					{
						IntVec3 position = val2.Position;
						if (!((IntVec3)(ref position)).IsValid)
						{
							val2.Position = ((Thing)base.pawn).Position;
						}
					}
					loadedAnything = true;
				}
			}
			loadIndex++;
			((JobDriver)this).JumpToToil(loadDecide);
		};
		val.defaultCompleteMode = (ToilCompleteMode)1;
		yield return val;
		yield return gotoBench;
	}
}
