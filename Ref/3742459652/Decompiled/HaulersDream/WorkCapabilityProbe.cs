using System;
using Verse;

namespace HaulersDream;

internal static class WorkCapabilityProbe
{
	internal static bool IsDisabled(Pawn pawn, WorkTypeDef work)
	{
		if (pawn == null || work == null)
		{
			return true;
		}
		try
		{
			return pawn.WorkTypeIsDisabled(work);
		}
		catch (Exception ex)
		{
			HDLog.ErrOnce("vanilla's work-type query (Pawn.WorkTypeIsDisabled -> GetDisabledWorkTypes) threw for a " + (((Def)(((Thing)pawn).def?)).defName ?? "pawn") + " while Hauler's Dream checked whether it can do " + (((Def)work).defName ?? "a work type") + ". This is a defect in that pawn's setup, NOT in Hauler's Dream: vanilla reads fields like ageTracker / lifeStageWorkSettings with no null guard, and a malformed modded pawn (for example a custom 'mech') can be missing them. The stack trace below names the real source. Hauler's Dream is treating this pawn as unable to do that work so one broken pawn does not stop the whole scan.\n" + ex, ((((Def)(((Thing)pawn).def?)).shortHash ?? 0) * 397) ^ ((Def)work).shortHash);
			return true;
		}
	}
}
