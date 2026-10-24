using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class BulkRefuel
{
	public static bool FeatureEnabled
	{
		get
		{
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings != null && settings.enableBulkRefuel)
			{
				return MasterEnable.Active;
			}
			return false;
		}
	}

	public static Job TryGiveBulkRefuelJob(Pawn pawn, Thing refuelable, bool playerOrder)
	{
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		if (!FeatureEnabled)
		{
			return null;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return null;
		}
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || refuelable == null || !refuelable.Spawned)
		{
			return null;
		}
		if (!MapGate.HdActiveOnMap(((Thing)pawn).Map))
		{
			return null;
		}
		if (((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null)
		{
			return null;
		}
		if (!playerOrder && !YieldRouter.IsEligible(pawn))
		{
			return null;
		}
		CompRefuelable val = ThingCompUtility.TryGetComp<CompRefuelable>(refuelable);
		if (val == null || val.IsFull)
		{
			return null;
		}
		if (val.Props != null && val.Props.atomicFueling)
		{
			return null;
		}
		if (GridsUtility.Fogged(refuelable))
		{
			return null;
		}
		if (!playerOrder && !val.allowAutoRefuel)
		{
			return null;
		}
		if (val.Props != null && val.FuelPercentOfMax > 0f && !val.Props.allowRefuelIfNotEmpty)
		{
			return null;
		}
		if (!playerOrder && !val.ShouldAutoRefuelNow)
		{
			return null;
		}
		if (refuelable.Faction != ((Thing)pawn).Faction)
		{
			return null;
		}
		CompInteractable val2 = default(CompInteractable);
		if (ThingCompUtility.TryGetComp<CompInteractable>(refuelable, ref val2) && val2.Props.cooldownPreventsRefuel && val2.OnCooldown)
		{
			return null;
		}
		if (!ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(refuelable), 1, -1, (ReservationLayerDef)null, playerOrder))
		{
			return null;
		}
		int fuelCountToFullyRefuel = val.GetFuelCountToFullyRefuel();
		if (fuelCountToFullyRefuel <= 0)
		{
			return null;
		}
		ThingFilter filter = val.Props?.fuelFilter;
		if (filter == null)
		{
			return null;
		}
		IntVec3 position = ((Thing)pawn).Position;
		if (!GenGrid.InBounds(position, ((Thing)pawn).Map) || GridsUtility.GetRegion(position, ((Thing)pawn).Map, (RegionType)14) == null)
		{
			return null;
		}
		List<Thing> list = RefuelWorkGiverUtility.FindEnoughReservableThings(pawn, position, new IntRange(1, fuelCountToFullyRefuel), (Predicate<Thing>)((Thing t) => filter.Allows(t)));
		if (list == null || list.Count < 2)
		{
			return null;
		}
		float baseCapKg = CarryMath.EffectiveCapacity(CarryCapacity.Of(pawn), settings.carryLimitFraction);
		float ceilingKg = BulkHaulPolicy.CeilingKg(settings.overloadLevel, OverloadGate.NoOverloadFor(pawn, settings), baseCapKg, OverloadGate.MaxCeilingKg(settings));
		float num = MassUtility.GearAndInventoryMass(pawn);
		Job val3 = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_BulkRefuel, LocalTargetInfo.op_Implicit(refuelable));
		val3.targetQueueB = new List<LocalTargetInfo>(list.Count);
		val3.countQueue = new List<int>(list.Count);
		int num2 = 0;
		for (int num3 = 0; num3 < list.Count; num3++)
		{
			if (num2 >= fuelCountToFullyRefuel)
			{
				break;
			}
			Thing val4 = list[num3];
			if (val4 != null && val4.stackCount > 0)
			{
				float statValue = StatExtension.GetStatValue(val4, StatDefOf.Mass, true, -1);
				int num4 = RefuelPlan.TakeFromStack(fuelCountToFullyRefuel - num2, ceilingKg, num, statValue, val4.stackCount);
				if (num4 > 0)
				{
					val3.targetQueueB.Add(new LocalTargetInfo(val4));
					val3.countQueue.Add(num4);
					num2 += num4;
					num += (float)num4 * statValue;
				}
			}
		}
		if (val3.targetQueueB.Count < 2)
		{
			return null;
		}
		val3.count = 1;
		HDLog.Dbg($"BulkRefuel: {pawn} sweeping {val3.targetQueueB.Count} fuel stack(s) for {((Entity)refuelable).LabelShort} (deficit {fuelCountToFullyRefuel}, queued ~{num2}).");
		return val3;
	}

	public static bool HasPotentialBulkRefuel(Pawn pawn, Thing refuelable)
	{
		if (!FeatureEnabled)
		{
			return false;
		}
		if (refuelable == null)
		{
			return false;
		}
		CompRefuelable val = ThingCompUtility.TryGetComp<CompRefuelable>(refuelable);
		if (val == null || val.IsFull)
		{
			return false;
		}
		if (val.Props != null && val.Props.atomicFueling)
		{
			return false;
		}
		return val.GetFuelCountToFullyRefuel() > 0;
	}
}
