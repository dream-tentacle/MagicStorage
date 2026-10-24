using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

internal static class HaulOrderGate
{
	internal static HaulOrderBlock BlockFor(Pawn pawn)
	{
		if (pawn == null)
		{
			return HaulOrderBlock.Manipulation;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		return HaulOrderPolicy.BlockFor(pawn.health?.capacities != null && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation), WorkCapabilityProbe.IsDisabled(pawn, WorkTypeDefOf.Hauling), settings?.allowIncapable ?? false);
	}

	internal static bool Blocks(Pawn pawn)
	{
		return BlockFor(pawn) != HaulOrderBlock.None;
	}
}
