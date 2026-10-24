using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class ProtectedWork
{
	public static bool IsProtected(Job job, bool nodeIsEmergency)
	{
		WorkGiverDef val = job?.workGiverDef;
		return ProtectedWorkPolicy.IsProtectedWork(nodeIsEmergency, val?.emergency ?? false, ((Def)(val?.workType?)).defName);
	}

	public static bool IsRestingPatient(Pawn pawn)
	{
		if (pawn != null)
		{
			if (!HealthAIUtility.ShouldSeekMedicalRest(pawn))
			{
				return RestUtility.InBed(pawn);
			}
			return true;
		}
		return false;
	}
}
