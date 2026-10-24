using System.Text;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

internal static class CarryCapacity
{
	internal static float Of(Pawn pawn)
	{
		if (pawn == null)
		{
			return 0f;
		}
		float num = MassUtility.Capacity(pawn, (StringBuilder)null);
		RaceProperties raceProps = pawn.RaceProps;
		if (raceProps == null || !raceProps.IsMechanoid || ((Thing)pawn).Faction == null || !((Thing)pawn).Faction.IsPlayer || CECompat.IsActive)
		{
			return num;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		float statValue = StatExtension.GetStatValue((Thing)(object)pawn, StatDefOf.CarryingCapacity, true, -1);
		float mechMultiplier = settings?.mechHaulMultiplier ?? 1f;
		return CarryCapacityPolicy.BaseCapacity(isPlayerMech: true, ceActive: false, statValue, num, mechMultiplier);
	}
}
