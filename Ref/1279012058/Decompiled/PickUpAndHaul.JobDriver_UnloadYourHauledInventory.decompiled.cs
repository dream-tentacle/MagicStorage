using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace PickUpAndHaul;

public class JobDriver_UnloadYourHauledInventory : JobDriver
{
	private int _countToDrop = -1;

	private int _unloadDuration = 3;

	public override void ExposeData()
	{
		((JobDriver)this).ExposeData();
		Scribe_Values.Look<int>(ref _countToDrop, "countToDrop", -1, false);
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		return true;
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		if (ModCompatibilityCheck.ExtendedStorageIsActive)
		{
			_unloadDuration = 20;
		}
		Toil begin = Toils_General.Wait(_unloadDuration, (TargetIndex)0);
		yield return begin;
		HashSet<Thing> carriedThings = ThingCompUtility.TryGetComp<CompHauledToInventory>((Thing)(object)base.pawn).GetHashSet();
		yield return FindTargetOrDrop(carriedThings);
		yield return PullItemFromInventory(carriedThings, begin);
		Toil releaseReservation = ReleaseReservation();
		Toil carryToCell = Toils_Haul.CarryHauledThingToCell((TargetIndex)2, (PathEndMode)3);
		yield return Toils_Jump.JumpIf(carryToCell, (Func<bool>)TargetIsCell);
		Toil carryToContainer = Toils_Haul.CarryHauledThingToContainer();
		yield return carryToContainer;
		yield return Toils_Haul.DepositHauledThingInContainer((TargetIndex)2, (TargetIndex)0, (Action)null);
		yield return Toils_Haul.JumpToCarryToNextContainerIfPossible(carryToContainer, (TargetIndex)2);
		yield return Toils_Jump.Jump(releaseReservation);
		yield return carryToCell;
		yield return Toils_Haul.PlaceHauledThingInCell((TargetIndex)2, carryToCell, true, false);
		yield return releaseReservation;
		yield return Toils_Jump.Jump(begin);
	}

	private bool TargetIsCell()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		LocalTargetInfo targetB = ((JobDriver)this).TargetB;
		return !((LocalTargetInfo)(ref targetB)).HasThing;
	}

	private Toil ReleaseReservation()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		return new Toil
		{
			initAction = delegate
			{
				//IL_0016: Unknown result type (might be due to invalid IL or missing references)
				//IL_0049: Unknown result type (might be due to invalid IL or missing references)
				if (((Thing)base.pawn).Map.reservationManager.ReservedBy(base.job.targetB, base.pawn, base.pawn.CurJob))
				{
					((Thing)base.pawn).Map.reservationManager.Release(base.job.targetB, base.pawn, base.pawn.CurJob);
				}
			}
		};
	}

	private Toil PullItemFromInventory(HashSet<Thing> carriedThings, Toil wait)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		return new Toil
		{
			initAction = delegate
			{
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				//IL_0145: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
				Thing thing = ((LocalTargetInfo)(ref target)).Thing;
				if (thing == null || !((ThingOwner)base.pawn.inventory.innerContainer).Contains(thing))
				{
					carriedThings.Remove(thing);
					base.pawn.jobs.curDriver.JumpToToil(wait);
				}
				else
				{
					if (!base.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || !thing.def.EverStorable(false))
					{
						((ThingOwner)base.pawn.inventory.innerContainer).TryDrop(thing, (ThingPlaceMode)1, _countToDrop, ref thing, (Action<Thing, int>)null, (Predicate<IntVec3>)null);
						((JobDriver)this).EndJobWith((JobCondition)2);
						carriedThings.Remove(thing);
					}
					else
					{
						base.pawn.inventory.innerContainer.TryTransferToContainer(thing, (ThingOwner)(object)base.pawn.carryTracker.innerContainer, _countToDrop, ref thing, true);
						base.job.count = _countToDrop;
						base.job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit(thing));
						carriedThings.Remove(thing);
					}
					if (ModCompatibilityCheck.CombatExtendedIsActive)
					{
						CompatHelper.UpdateInventory(base.pawn);
					}
					ForbidUtility.SetForbidden(thing, false, false);
				}
			}
		};
	}

	private Toil FindTargetOrDrop(HashSet<Thing> carriedThings)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		return new Toil
		{
			initAction = delegate
			{
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				//IL_0016: Unknown result type (might be due to invalid IL or missing references)
				//IL_003b: Unknown result type (might be due to invalid IL or missing references)
				//IL_005e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0091: Unknown result type (might be due to invalid IL or missing references)
				//IL_009b: Unknown result type (might be due to invalid IL or missing references)
				//IL_009c: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
				//IL_0113: Unknown result type (might be due to invalid IL or missing references)
				ThingCount val = FirstUnloadableThing(base.pawn, carriedThings);
				if (((ThingCount)(ref val)).Count == 0)
				{
					if (carriedThings.Count == 0)
					{
						((JobDriver)this).EndJobWith((JobCondition)2);
					}
				}
				else
				{
					StoragePriority val2 = (StoragePriority)0;
					IntVec3 val3 = default(IntVec3);
					IHaulDestination val4 = default(IHaulDestination);
					Thing val5 = default(Thing);
					if (StoreUtility.TryFindBestBetterStorageFor(((ThingCount)(ref val)).Thing, base.pawn, ((Thing)base.pawn).Map, val2, ((Thing)base.pawn).Faction, ref val3, ref val4, true))
					{
						base.job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit(((ThingCount)(ref val)).Thing));
						if (val3 == IntVec3.Invalid)
						{
							base.job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit((Thing)(object)((val4 is Thing) ? val4 : null)));
						}
						else
						{
							base.job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(val3));
						}
						if (!((Thing)base.pawn).Map.reservationManager.Reserve(base.pawn, base.job, base.job.targetB, 1, -1, (ReservationLayerDef)null, true, false, true))
						{
							((ThingOwner)base.pawn.inventory.innerContainer).TryDrop(((ThingCount)(ref val)).Thing, (ThingPlaceMode)1, ((ThingCount)(ref val)).Thing.stackCount, ref val5, (Action<Thing, int>)null, (Predicate<IntVec3>)null);
							((JobDriver)this).EndJobWith((JobCondition)4);
						}
						else
						{
							_countToDrop = ((ThingCount)(ref val)).Thing.stackCount;
						}
					}
					else
					{
						((ThingOwner)base.pawn.inventory.innerContainer).TryDrop(((ThingCount)(ref val)).Thing, (ThingPlaceMode)1, ((ThingCount)(ref val)).Thing.stackCount, ref val5, (Action<Thing, int>)null, (Predicate<IntVec3>)null);
						((JobDriver)this).EndJobWith((JobCondition)2);
					}
				}
			}
		};
	}

	private static ThingCount FirstUnloadableThing(Pawn pawn, HashSet<Thing> carriedThings)
	{
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		ThingOwner<Thing> innerContainer = pawn.inventory.innerContainer;
		using (IEnumerator<Thing> enumerator = carriedThings.OrderBy((Thing t) => ((Def)(t.def.FirstThingCategory?)).index).ThenBy((Thing x) => ((Def)x.def).defName).GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				Thing current = enumerator.Current;
				if (!((ThingOwner)innerContainer).Contains(current))
				{
					ThingDef def = current.def;
					carriedThings.Remove(current);
					for (int num = 0; num < ((ThingOwner)innerContainer).Count; num++)
					{
						Thing val = innerContainer[num];
						if (val.def == def)
						{
							return new ThingCount(val, val.stackCount, false);
						}
					}
				}
				return new ThingCount(current, current.stackCount, false);
			}
		}
		return default(ThingCount);
	}
}
