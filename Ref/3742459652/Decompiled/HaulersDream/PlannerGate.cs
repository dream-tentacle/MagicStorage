using Verse;

namespace HaulersDream;

internal static class PlannerGate
{
	internal static bool HideForUnassigned(Pawn pawn, WorkTypeDef wt)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || settings.planForUnassignedWork || wt == null)
		{
			return false;
		}
		if (pawn?.workSettings != null)
		{
			return pawn.workSettings.GetPriority(wt) == 0;
		}
		return false;
	}
}
