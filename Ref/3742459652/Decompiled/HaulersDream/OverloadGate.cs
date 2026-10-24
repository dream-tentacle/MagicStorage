using System;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

internal static class OverloadGate
{
	private const int ActiveRunDemand = 1048576;

	internal static bool NoOverload(HaulersDreamSettings s)
	{
		if (s != null && !s.strictCarryWeight && !OverloadTuning.IsOff(s.overloadLevel))
		{
			return CECompat.IsActive;
		}
		return true;
	}

	internal static int EffectiveLevel(HaulersDreamSettings s)
	{
		if (!NoOverload(s))
		{
			return s.overloadLevel;
		}
		return 10;
	}

	internal static float MaxCeilingKg(HaulersDreamSettings s)
	{
		if (s == null || !(s.carryMassCapKg > 0f))
		{
			return float.PositiveInfinity;
		}
		return s.carryMassCapKg;
	}

	internal static bool NoOverloadFor(Pawn pawn, HaulersDreamSettings s)
	{
		if (s == null)
		{
			return true;
		}
		RaceProperties val = ((pawn != null) ? pawn.RaceProps : null);
		if (val == null)
		{
			return true;
		}
		return !OverloadPolicy.ParticipatesInOverload(s.strictCarryWeight, s.overloadLevel, CECompat.IsActive, val.Humanlike, val.IsMechanoid);
	}

	internal static int CountToPickUp(Pawn pawn, Thing thing, HaulersDreamSettings s)
	{
		if (pawn == null || thing == null || s == null)
		{
			return 0;
		}
		float num = CarryCapacity.Of(pawn);
		float baseCap = CarryMath.EffectiveCapacity(num, s.carryLimitFraction);
		float cur = MassUtility.GearAndInventoryMass(pawn);
		return CountToPickUp(pawn, thing, s, num, baseCap, cur);
	}

	internal static int CountToPickUp(Pawn pawn, Thing thing, HaulersDreamSettings s, float maxCap, float baseCap, float cur)
	{
		if (pawn == null || thing == null || s == null)
		{
			return 0;
		}
		float statValue = StatExtension.GetStatValue(thing, StatDefOf.Mass, true, -1);
		return Math.Min(OverloadPolicy.UnitsToCarry(NoOverloadFor(pawn, s) ? 10 : s.overloadLevel, maxCap, baseCap, cur, statValue, 1048576, thing.stackCount, MaxCeilingKg(s)), CECompat.MaxFitCount(pawn, thing));
	}
}
