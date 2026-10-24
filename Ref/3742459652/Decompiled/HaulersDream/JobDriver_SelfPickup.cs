using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_SelfPickup : JobDriver
{
	private Toil loop;

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		return true;
	}

	public override void Notify_PatherFailed()
	{
		((JobDriver)this).JumpToToil(loop);
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		CompHauledToInventory comp = ThingCompUtility.TryGetComp<CompHauledToInventory>((Thing)(object)base.pawn);
		loop = new Toil
		{
			initAction = delegate
			{
				//IL_0030: Unknown result type (might be due to invalid IL or missing references)
				Thing val = comp?.TakeNextValidPending();
				if (val == null)
				{
					((JobDriver)this).EndJobWith((JobCondition)2);
				}
				else
				{
					base.job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit(val));
				}
			},
			defaultCompleteMode = (ToilCompleteMode)1
		};
		yield return loop;
		yield return SweepWalk.MakeToil((JobDriver)(object)this, (TargetIndex)1, "HD_SelfPickup_Goto", loop, delegate
		{
		}, () => false);
		PickupDelayContext context = ((comp == null || !comp.selfPickupDirectHarvest || comp.pendingSelfPickups.Count != 1) ? PickupDelayContext.AutoHaul : PickupDelayContext.DirectHarvest);
		yield return PickupPause.MakeToil((TargetIndex)1, context);
		yield return new Toil
		{
			initAction = delegate
			{
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
				Thing thing = ((LocalTargetInfo)(ref target)).Thing;
				if (thing != null && thing.Spawned && !ForbidUtility.IsForbidden(thing, base.pawn) && !StoreUtility.IsInValidStorage(thing) && YieldRouter.HasScoopDestination(base.pawn, thing))
				{
					HaulersDreamSettings settings = HaulersDreamMod.Settings;
					int num = OverloadGate.CountToPickUp(base.pawn, thing, settings);
					if (num <= 0)
					{
						if (comp != null && thing != null && settings != null && settings.markForUnload && !settings.strictCarryWeight && !settings.keepWorkingWhenFull && !PawnUnloadChecker.HasPendingRealWork(base.pawn))
						{
							SelfPickupClaims.Claim(thing, base.pawn);
						}
						YieldRouter.MaybeUnloadBecauseFull(base.pawn, settings);
						((JobDriver)this).EndJobWith((JobCondition)2);
					}
					else
					{
						ThingOwner directlyHeldThings = base.pawn.inventory.GetDirectlyHeldThings();
						bool flag = num >= thing.stackCount;
						Thing val = thing.SplitOff(num);
						if (val != null)
						{
							int stackCount = val.stackCount;
							bool flag2 = directlyHeldThings.TryAddOrTransfer(val, true);
							if (flag2 || val.stackCount < stackCount)
							{
								int mergedCount = (flag2 ? stackCount : (stackCount - val.stackCount));
								Thing val2 = ((val.def.stackLimit == 1) ? val : (YieldRouter.InventoryStackOfDef(directlyHeldThings, val.def, base.pawn) ?? (flag2 ? val : null)));
								if (val2 != null)
								{
									comp?.RegisterHauledItem(val2, mergedCount);
								}
								comp?.NotifyYieldPicked();
							}
							if (!flag2 && !flag && val.stackCount > 0 && !thing.Destroyed)
							{
								thing.TryAbsorbStack(val, false);
							}
						}
					}
				}
			},
			defaultCompleteMode = (ToilCompleteMode)1
		};
		yield return Toils_Jump.Jump(loop);
	}
}
