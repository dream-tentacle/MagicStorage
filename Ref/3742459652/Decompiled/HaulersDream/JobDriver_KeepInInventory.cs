using System;
using System.Collections.Generic;
using HaulersDream.Core;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_KeepInInventory : JobDriver
{
	private const TargetIndex ItemInd = (TargetIndex)1;

	private const TargetIndex ContainerInd = (TargetIndex)2;

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return ReservationUtility.Reserve(base.pawn, base.job.GetTarget((TargetIndex)1), base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false);
	}

	public override string GetReport()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.Keep.Report"));
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate
		{
			CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
			if (comp != null && PawnUnloadChecker.AnyUnloadable(base.pawn, comp.GetHashSet()))
			{
				PawnUnloadChecker.CheckIfShouldUnload(base.pawn, forced: true, behindQueuedWork: true);
			}
		});
		LocalTargetInfo target = base.job.GetTarget((TargetIndex)2);
		if (((LocalTargetInfo)(ref target)).HasThing)
		{
			yield return ToilFailConditions.FailOnDespawnedOrNull<Toil>(Toils_Goto.GotoThing((TargetIndex)2, (PathEndMode)2, false), (TargetIndex)2);
			yield return ToilFailConditions.FailOnDespawnedOrNull<Toil>(PickupPause.MakeToil((TargetIndex)2, PickupDelayContext.ManualCarry), (TargetIndex)2);
			Toil val = ToilMaker.MakeToil("HD_Keep_TakeFromContainer");
			val.initAction = delegate
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_001d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target2 = base.job.GetTarget((TargetIndex)1);
				Thing thing = ((LocalTargetInfo)(ref target2)).Thing;
				target2 = base.job.GetTarget((TargetIndex)2);
				Thing thing2 = ((LocalTargetInfo)(ref target2)).Thing;
				if (thing != null && !thing.Destroyed && thing2 != null && thing2.Spawned)
				{
					HaulersDreamSettings settings = HaulersDreamMod.Settings;
					CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
					ThingOwner<Thing> val3 = base.pawn.inventory?.innerContainer;
					if (settings != null && comp != null && val3 != null)
					{
						ThingOwner val4 = ThingOwnerUtility.TryGetInnerInteractableThingOwner(thing2);
						if (val4 != null && val4.Contains(thing))
						{
							int planned = ((base.job.count > 0) ? base.job.count : thing.stackCount);
							int num = BulkHaul.MassClampedTake(base.pawn, thing, planned, settings);
							if (num > 0)
							{
								ThingDef def = thing.def;
								Thing val5 = default(Thing);
								int num2 = val4.TryTransferToContainer(thing, (ThingOwner)(object)val3, num, ref val5, true);
								if (num2 > 0)
								{
									comp.AddKeptCount(def, num2);
								}
							}
						}
					}
				}
			};
			val.defaultCompleteMode = (ToilCompleteMode)1;
			yield return val;
			yield break;
		}
		yield return ToilFailConditions.FailOnDespawnedOrNull<Toil>(Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)3, false), (TargetIndex)1);
		yield return ToilFailConditions.FailOnDespawnedOrNull<Toil>(PickupPause.MakeToil((TargetIndex)1, PickupDelayContext.ManualCarry), (TargetIndex)1);
		Toil val2 = ToilMaker.MakeToil("HD_Keep_Take");
		val2.initAction = delegate
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target2 = base.job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target2)).Thing;
			if (thing != null && thing.Spawned)
			{
				HaulersDreamSettings settings = HaulersDreamMod.Settings;
				CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
				ThingOwner<Thing> val3 = base.pawn.inventory?.innerContainer;
				if (settings != null && comp != null && val3 != null)
				{
					int planned = ((base.job.count > 0) ? base.job.count : thing.stackCount);
					int num = BulkHaul.MassClampedTake(base.pawn, thing, planned, settings);
					if (num > 0)
					{
						ThingDef def = thing.def;
						Thing val4 = thing.SplitOff(num);
						if (val4 != null && !val4.Destroyed && val4.stackCount > 0)
						{
							int stackCount = val4.stackCount;
							if (((ThingOwner)val3).TryAdd(val4, true))
							{
								comp.AddKeptCount(def, stackCount);
							}
							else if (!val4.Destroyed)
							{
								GenPlace.TryPlaceThing(val4, ((Thing)base.pawn).Position, ((Thing)base.pawn).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
							}
						}
					}
				}
			}
		};
		val2.defaultCompleteMode = (ToilCompleteMode)1;
		yield return val2;
	}
}
