using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_UnloadHauledInventory : JobDriver
{
	private int countToDrop = -1;

	private readonly HashSet<Thing> skippedThisJob = new HashSet<Thing>();

	[ThreadStatic]
	private static List<Thing> scratchOrdered;

	private readonly Dictionary<ThingDef, IntVec3> destCellByDef = new Dictionary<ThingDef, IntVec3>();

	private ThingDef lastDeliveredDef;

	private Toil loopToil;

	private int pathFailuresThisJob;

	public override void ExposeData()
	{
		((JobDriver)this).ExposeData();
		Scribe_Values.Look<int>(ref countToDrop, "countToDrop", -1, false);
	}

	public override void Notify_PatherFailed()
	{
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		Pawn_CarryTracker carryTracker = base.pawn.carryTracker;
		Thing val = ((carryTracker != null) ? carryTracker.CarriedThing : null);
		if (val == null || loopToil == null)
		{
			((JobDriver)this).Notify_PatherFailed();
			return;
		}
		pathFailuresThisJob++;
		int stackCount = val.stackCount;
		ThingOwner<Thing> val2 = base.pawn.inventory?.innerContainer;
		Thing val3 = null;
		if (val2 != null)
		{
			base.pawn.carryTracker.innerContainer.TryTransferToContainer(val, (ThingOwner)(object)val2, stackCount, ref val3, false);
		}
		if (val3 == null)
		{
			HaulChurnGuard.StampBackoff(val);
			HDLog.Dbg("unload " + ((Entity)base.pawn).LabelShort + ": could not reach the destination for " + ((Entity)val).LabelShort + " and could not put it back in the pack; ending the trip and backing it off.");
			((JobDriver)this).EndJobWith((JobCondition)2);
			return;
		}
		ThingCompUtility.TryGetComp<CompHauledToInventory>((Thing)(object)base.pawn)?.RegisterHauledItem(val3, stackCount);
		base.job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit(val3));
		skippedThisJob.Add(val3);
		HaulChurnGuard.StampBackoff(val3);
		ReleaseTargetBReservation();
		int num = RemainingCandidateCount();
		HDLog.Dbg("unload " + ((Entity)base.pawn).LabelShort + ": could not reach the destination for " + ((Entity)val3).LabelShort + " " + $"x{stackCount} (failure {pathFailuresThisJob} this trip, {num} stack(s) left " + "to try); putting it back in the pack, backing it off and moving on.");
		if (UnreachableDestinationPolicy.Choose(pathFailuresThisJob, num) == UnreachableDestinationAction.SetAsideAndContinue)
		{
			((JobDriver)this).JumpToToil(loopToil);
		}
		else
		{
			((JobDriver)this).EndJobWith((JobCondition)2);
		}
	}

	private int RemainingCandidateCount()
	{
		CompHauledToInventory compHauledToInventory = ThingCompUtility.TryGetComp<CompHauledToInventory>((Thing)(object)base.pawn);
		ThingOwner<Thing> val = base.pawn.inventory?.innerContainer;
		if (compHauledToInventory == null || val == null)
		{
			return 0;
		}
		int num = 0;
		foreach (Thing item in compHauledToInventory.GetHashSet())
		{
			if (item != null && !item.Destroyed && !skippedThisJob.Contains(item) && ((ThingOwner)val).Contains(item))
			{
				num++;
			}
		}
		return num;
	}

	private void ReleaseTargetBReservation()
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		ReservationManager val = ((Thing)base.pawn).Map?.reservationManager;
		if (val != null && val.ReservedBy(base.job.targetB, base.pawn, base.pawn.CurJob))
		{
			val.Release(base.job.targetB, base.pawn, base.pawn.CurJob);
		}
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		return true;
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		Toil begin = (loopToil = Toils_General.Wait(3, (TargetIndex)0));
		yield return begin;
		CompHauledToInventory comp = ThingCompUtility.TryGetComp<CompHauledToInventory>((Thing)(object)base.pawn);
		HashSet<Thing> carried = comp?.GetHashSet() ?? new HashSet<Thing>();
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			Pawn_CarryTracker carryTracker = base.pawn.carryTracker;
			int num;
			if (carryTracker == null)
			{
				num = 0;
			}
			else
			{
				ThingOwner<Thing> innerContainer = carryTracker.innerContainer;
				num = ((((innerContainer != null) ? new bool?(((ThingOwner)innerContainer).Contains(thing)) : ((bool?)null)) == true) ? 1 : 0);
			}
			bool flag = (byte)num != 0;
			Pawn_InventoryTracker inventory = base.pawn.inventory;
			int num2;
			if (inventory == null)
			{
				num2 = 0;
			}
			else
			{
				ThingOwner<Thing> innerContainer2 = inventory.innerContainer;
				num2 = ((((innerContainer2 != null) ? new bool?(((ThingOwner)innerContainer2).Contains(thing)) : ((bool?)null)) == true) ? 1 : 0);
			}
			bool flag2 = (byte)num2 != 0;
			if (comp != null && thing != null && !thing.Destroyed && (flag || flag2))
			{
				comp.RegisterHauledItem(thing);
			}
		});
		yield return FindTargetOrDrop(carried, begin);
		yield return PullItemFromInventory(carried, begin);
		Toil releaseReservation = ReleaseReservation();
		Toil carryToCell = Toils_Haul.CarryHauledThingToCell((TargetIndex)2, (PathEndMode)3);
		yield return Toils_Jump.JumpIf(carryToCell, (Func<bool>)delegate
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo targetB = ((JobDriver)this).TargetB;
			return !((LocalTargetInfo)(ref targetB)).HasThing;
		});
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

	private Toil ReleaseReservation()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		return new Toil
		{
			initAction = ReleaseTargetBReservation
		};
	}

	private Toil PullItemFromInventory(HashSet<Thing> carried, Toil wait)
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
				//IL_017a: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
				Thing result = ((LocalTargetInfo)(ref target)).Thing;
				if (result == null || !((ThingOwner)base.pawn.inventory.innerContainer).Contains(result))
				{
					carried.Remove(result);
					base.pawn.jobs.curDriver.JumpToToil(wait);
				}
				else if (!base.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || !result.def.EverStorable(false))
				{
					Thing item = result;
					if (InventoryDrop.TryDropPreferHome(base.pawn, result, countToDrop, "no-manipulation", out result))
					{
						carried.Remove(item);
					}
					((JobDriver)this).EndJobWith((JobCondition)2);
				}
				else
				{
					Thing val = result;
					base.pawn.inventory.innerContainer.TryTransferToContainer(result, (ThingOwner)(object)base.pawn.carryTracker.innerContainer, countToDrop, ref result, true);
					if (result == null)
					{
						if (val != null)
						{
							skippedThisJob.Add(val);
						}
						base.pawn.jobs.curDriver.JumpToToil(wait);
					}
					else
					{
						base.job.count = countToDrop;
						base.job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit(result));
						carried.Remove(result);
						ForbidUtility.SetForbidden(result, false, false);
					}
				}
			}
		};
	}

	private Toil FindTargetOrDrop(HashSet<Thing> carried, Toil begin)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		return new Toil
		{
			initAction = delegate
			{
				//IL_0041: Unknown result type (might be due to invalid IL or missing references)
				//IL_0046: Unknown result type (might be due to invalid IL or missing references)
				//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
				//IL_014a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0154: Unknown result type (might be due to invalid IL or missing references)
				//IL_0155: Unknown result type (might be due to invalid IL or missing references)
				//IL_0327: Unknown result type (might be due to invalid IL or missing references)
				//IL_0338: Unknown result type (might be due to invalid IL or missing references)
				//IL_0345: Unknown result type (might be due to invalid IL or missing references)
				//IL_034a: Unknown result type (might be due to invalid IL or missing references)
				//IL_034f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0377: Unknown result type (might be due to invalid IL or missing references)
				//IL_03a5: Unknown result type (might be due to invalid IL or missing references)
				//IL_03bb: Unknown result type (might be due to invalid IL or missing references)
				//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
				//IL_03d8: Unknown result type (might be due to invalid IL or missing references)
				//IL_03f1: Unknown result type (might be due to invalid IL or missing references)
				//IL_018b: Unknown result type (might be due to invalid IL or missing references)
				//IL_018c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0173: Unknown result type (might be due to invalid IL or missing references)
				//IL_043c: Unknown result type (might be due to invalid IL or missing references)
				//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
				//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
				//IL_020a: Unknown result type (might be due to invalid IL or missing references)
				if (lastDeliveredDef != null)
				{
					destCellByDef.Remove(lastDeliveredDef);
					lastDeliveredDef = null;
				}
				ThingCount next = FirstUnloadableThing(carried);
				if (((ThingCount)(ref next)).Count == 0)
				{
					((JobDriver)this).EndJobWith((JobCondition)((carried.Count == 0 || skippedThisJob.Count > 0) ? 2 : 4));
				}
				else
				{
					IntVec3 val = default(IntVec3);
					IHaulDestination val2 = default(IHaulDestination);
					bool num = StoreUtility.TryFindBestBetterStorageFor(((ThingCount)(ref next)).Thing, base.pawn, ((Thing)base.pawn).Map, (StoragePriority)0, ((Thing)base.pawn).Faction, ref val, ref val2, true);
					bool flag = ((Thing)base.pawn).Map != null && ((Thing)base.pawn).Map.IsPlayerHome;
					IntVec3 cell = IntVec3.Invalid;
					bool hasNearbyHomeCell = !num && flag && InventorySurplus.TryFindDesperateHomeAreaCell(base.pawn, ((ThingCount)(ref next)).Thing, out cell);
					Thing result;
					switch (UnloadFallbackPolicy.Choose(num, flag, hasNearbyHomeCell))
					{
					case UnloadPlacement.Deliver:
						base.job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit(((ThingCount)(ref next)).Thing));
						if (val == IntVec3.Invalid)
						{
							base.job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit((Thing)(object)((val2 is Thing) ? val2 : null)));
						}
						else
						{
							base.job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(val));
						}
						if (!StorageCommitments.TryCommit(base.pawn, GroupAt(val), ((ThingCount)(ref next)).Thing.def, UnitsBoundFor(next), "unload") && !((Thing)base.pawn).Map.reservationManager.Reserve(base.pawn, base.job, base.job.targetB, 1, -1, (ReservationLayerDef)null, true, false, true))
						{
							if (InventoryDrop.TryDropPreferHome(base.pawn, ((ThingCount)(ref next)).Thing, ((ThingCount)(ref next)).Count, "reserve-failed-storage", out result))
							{
								carried.Remove(((ThingCount)(ref next)).Thing);
							}
							((JobDriver)this).EndJobWith((JobCondition)4);
						}
						else
						{
							countToDrop = ((ThingCount)(ref next)).Count;
							lastDeliveredDef = ((ThingCount)(ref next)).Thing.def;
						}
						break;
					case UnloadPlacement.KeepInInventory:
						HDLog.Dbg($"unload {((Entity)base.pawn).LabelShort}: on a non-home map ({((Thing)base.pawn).Map}); keeping " + $"{((Entity)((ThingCount)(ref next)).Thing).LabelShort} x{((ThingCount)(ref next)).Count} tagged in inventory to ride home.");
						((JobDriver)this).EndJobWith((JobCondition)2);
						break;
					case UnloadPlacement.PlaceOnNearbyHomeCell:
					{
						string text = $"unload {((Entity)base.pawn).LabelShort}: no storage for {((Entity)((ThingCount)(ref next)).Thing).LabelShort} x{((ThingCount)(ref next)).Count}; ";
						string text2 = $"hauling to home-area cell {cell} ";
						IntVec3 val3 = cell - ((Thing)base.pawn).Position;
						HDLog.Dbg(text + text2 + $"(dist={((IntVec3)(ref val3)).LengthHorizontal:0.#}, " + $"home={InventoryDrop.IsInHome(((Thing)base.pawn).Map, cell)}).");
						base.job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit(((ThingCount)(ref next)).Thing));
						base.job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(cell));
						if (!StorageCommitments.TryCommit(base.pawn, GroupAt(cell), ((ThingCount)(ref next)).Thing.def, UnitsBoundFor(next), "unload-fallback") && !((Thing)base.pawn).Map.reservationManager.Reserve(base.pawn, base.job, base.job.targetB, 1, -1, (ReservationLayerDef)null, true, false, true))
						{
							if (InventoryDrop.TryDropPreferHome(base.pawn, ((ThingCount)(ref next)).Thing, ((ThingCount)(ref next)).Count, "reserve-failed-fallback", out result))
							{
								carried.Remove(((ThingCount)(ref next)).Thing);
								base.pawn.jobs.curDriver.JumpToToil(begin);
							}
							else
							{
								((JobDriver)this).EndJobWith((JobCondition)4);
							}
						}
						else
						{
							countToDrop = ((ThingCount)(ref next)).Count;
							lastDeliveredDef = ((ThingCount)(ref next)).Thing.def;
						}
						break;
					}
					default:
						HaulChurnGuard.StampBackoff(((ThingCount)(ref next)).Thing);
						HDLog.Dbg("unload " + ((Entity)base.pawn).LabelShort + ": no storage and no home-area cell within " + $"{20} radial cells for " + $"{((Entity)((ThingCount)(ref next)).Thing).LabelShort} x{((ThingCount)(ref next)).Count}; dropping here and backing it off.");
						if (InventoryDrop.TryDropPreferHome(base.pawn, ((ThingCount)(ref next)).Thing, ((ThingCount)(ref next)).Count, "nowhere", out result))
						{
							carried.Remove(((ThingCount)(ref next)).Thing);
							base.pawn.jobs.curDriver.JumpToToil(begin);
						}
						else
						{
							((JobDriver)this).EndJobWith((JobCondition)4);
						}
						break;
					}
				}
			}
		};
	}

	private ThingCount FirstUnloadableThing(HashSet<Thing> carried)
	{
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_027f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_026a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		ThingOwner<Thing> innerContainer = base.pawn.inventory.innerContainer;
		List<Thing> list = scratchOrdered ?? (scratchOrdered = new List<Thing>());
		list.Clear();
		foreach (Thing item in carried)
		{
			list.Add(item);
		}
		list.Sort((Thing a, Thing b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
		bool flag = HaulersDreamMod.Settings != null && HaulersDreamMod.Settings.closestDestinationUnloadOrder;
		int num = list.Count;
		while (num > 0)
		{
			int index = -1;
			Thing val = null;
			int catB = 0;
			string defB = null;
			int distSqB = int.MaxValue;
			for (int num2 = 0; num2 < list.Count; num2++)
			{
				Thing val2 = list[num2];
				if (val2 != null)
				{
					int num3 = ((int?)((Def)(val2.def.FirstThingCategory?)).index) ?? int.MaxValue;
					int num4 = int.MaxValue;
					bool flag2;
					if (flag)
					{
						num4 = ResolvedDestinationDistanceSq(val2);
						flag2 = val == null || UnloadDestinationOrder.Less(num4, num3, ((Def)val2.def).defName, distSqB, catB, defB);
					}
					else
					{
						flag2 = val == null || SelectFirstByCategoryThenDef.LessThan(num3, ((Def)val2.def).defName, catB, defB);
					}
					if (flag2)
					{
						index = num2;
						val = val2;
						catB = num3;
						defB = ((Def)val2.def).defName;
						distSqB = num4;
					}
				}
			}
			Thing val3 = val;
			list[index] = null;
			num--;
			if (skippedThisJob.Contains(val3))
			{
				continue;
			}
			if (!((ThingOwner)innerContainer).Contains(val3))
			{
				ThingDef def = val3.def;
				carried.Remove(val3);
				for (int num5 = 0; num5 < ((ThingOwner)innerContainer).Count; num5++)
				{
					if (innerContainer[num5].def == def)
					{
						carried.Add(innerContainer[num5]);
						int num6 = UnloadableCountOf(innerContainer[num5]);
						if (num6 <= 0)
						{
							break;
						}
						return new ThingCount(innerContainer[num5], num6, false);
					}
				}
			}
			else if (ReservationUtility.CanReserve(base.pawn, LocalTargetInfo.op_Implicit(val3), 1, -1, (ReservationLayerDef)null, false))
			{
				int num7 = UnloadableCountOf(val3);
				if (num7 > 0)
				{
					return new ThingCount(val3, num7, false);
				}
			}
		}
		return default(ThingCount);
	}

	private int UnitsBoundFor(ThingCount next)
	{
		return Math.Max(((ThingCount)(ref next)).Count, StorageCommitments.UnitsMovingOf(base.pawn, ((ThingCount)(ref next)).Thing.def));
	}

	private ISlotGroup GroupAt(IntVec3 cell)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (!((IntVec3)(ref cell)).IsValid || ((Thing)base.pawn).Map == null)
		{
			return null;
		}
		return BulkHaul.BudgetGroupOf(((Thing)base.pawn).Map.haulDestinationManager.SlotGroupAt(cell));
	}

	private int UnloadableCountOf(Thing thing)
	{
		return InventorySurplus.SurplusOf(base.pawn, thing);
	}

	private int ResolvedDestinationDistanceSq(Thing cand)
	{
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		ThingDef def = cand.def;
		if (!destCellByDef.TryGetValue(def, out var value))
		{
			IntVec3 val = default(IntVec3);
			IHaulDestination val2 = default(IHaulDestination);
			if (StoreUtility.TryFindBestBetterStorageFor(cand, base.pawn, ((Thing)base.pawn).Map, (StoragePriority)0, ((Thing)base.pawn).Faction, ref val, ref val2, true))
			{
				_003F val3;
				if (!(val != IntVec3.Invalid))
				{
					IHaulDestination obj = ((val2 is Thing) ? val2 : null);
					val3 = ((obj != null) ? ((Thing)obj).Position : IntVec3.Invalid);
				}
				else
				{
					val3 = val;
				}
				value = (IntVec3)val3;
			}
			else
			{
				value = IntVec3.Invalid;
			}
			destCellByDef[def] = value;
		}
		if (value == IntVec3.Invalid)
		{
			return int.MaxValue;
		}
		IntVec3 val4 = value - ((Thing)base.pawn).Position;
		int lengthHorizontalSquared = ((IntVec3)(ref val4)).LengthHorizontalSquared;
		if (lengthHorizontalSquared >= int.MaxValue)
		{
			return 2147483646;
		}
		return lengthHorizontalSquared;
	}
}
