using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class SlaughterHaul
{
	internal static void TryAppendHuntKillHaul(JobDriver_Hunt driver)
	{
		if (driver != null)
		{
			Pawn pawn = ((JobDriver)driver).pawn;
			Job job = ((JobDriver)driver).job;
			Thing victimThing = ((job != null) ? ((LocalTargetInfo)(ref job.targetA)).Thing : null);
			TryAppendHaul(pawn, victimThing, HaulKillSource.Hunt);
		}
	}

	internal static bool TryAppendHaul(Pawn killer, Thing victimThing, HaulKillSource kind)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return false;
		}
		if (!HaulAfterKillPolicy.ShouldHaul(kind, settings.haulWildKills, settings.haulTamedSlaughter))
		{
			return false;
		}
		Corpse val = ResolveCorpse(victimThing);
		if (val == null || !((Thing)val).Spawned || ((Thing)val).Map == null)
		{
			return false;
		}
		Pawn innerPawn = val.InnerPawn;
		if (((innerPawn != null) ? innerPawn.RaceProps : null) == null || !innerPawn.RaceProps.Animal)
		{
			return false;
		}
		if (killer?.jobs?.jobQueue == null || ((Thing)killer).Map != ((Thing)val).Map)
		{
			return false;
		}
		if (((Thing)killer).Faction != Faction.OfPlayerSilentFail || QuestUtility.IsQuestLodger(killer))
		{
			return false;
		}
		if (!MapGate.HdActiveOnMap(((Thing)val).Map))
		{
			return false;
		}
		if (!YieldRouter.IsEligible(killer))
		{
			return false;
		}
		if (ForbidUtility.IsForbidden((Thing)(object)val, killer))
		{
			return false;
		}
		if (StoreUtility.IsInValidStorage((Thing)(object)val))
		{
			return false;
		}
		Job val2 = HaulAIUtility.HaulToStorageJob(killer, (Thing)(object)val, false);
		if (val2 == null)
		{
			return false;
		}
		if (killer.CurJob?.def == JobDefOf.HaulToCell && (object)((LocalTargetInfo)(ref killer.CurJob.targetA)).Thing == val)
		{
			return false;
		}
		JobQueue jobQueue = killer.jobs.jobQueue;
		for (int i = 0; i < jobQueue.Count; i++)
		{
			QueuedJob obj = jobQueue[i];
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				Job job = obj.job;
				obj2 = ((job != null) ? ((LocalTargetInfo)(ref job.targetA)).Thing : null);
			}
			if (obj2 == val)
			{
				return false;
			}
		}
		jobQueue.EnqueueLast(val2, (JobTag?)(JobTag)0);
		HDLog.Dbg($"{killer} queued haul-after-{kind} for {val}.");
		return true;
	}

	private static Corpse ResolveCorpse(Thing victimThing)
	{
		Corpse val = (Corpse)(object)((victimThing is Corpse) ? victimThing : null);
		if (val == null)
		{
			Pawn val2 = (Pawn)(object)((victimThing is Pawn) ? victimThing : null);
			if (val2 != null)
			{
				return val2.Corpse;
			}
			return null;
		}
		return val;
	}
}
