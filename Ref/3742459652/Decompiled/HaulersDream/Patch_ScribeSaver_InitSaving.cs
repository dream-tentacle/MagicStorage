using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(ScribeSaver), "InitSaving")]
public static class Patch_ScribeSaver_InitSaving
{
	private static HashSet<JobDef> _stripSet;

	private static HashSet<JobDef> StripSet => _stripSet ?? (_stripSet = new HashSet<JobDef>(HdJobDefSets.CustomDriverJobDefs));

	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.cleanupOnSave ?? true;
	}

	private static void Prefix()
	{
		if (!UnityData.IsInMainThread)
		{
			HDLog.Dbg("ScribeSaver.InitSaving reached off the main thread — skipping pre-save HD job cleanup.");
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.cleanupOnSave || Current.Game == null || Find.Maps == null)
		{
			return;
		}
		HashSet<JobDef> stripSet = StripSet;
		List<Map> maps = Find.Maps;
		for (int i = 0; i < maps.Count; i++)
		{
			Map obj = maps[i];
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				MapPawns mapPawns = obj.mapPawns;
				obj2 = ((mapPawns != null) ? mapPawns.AllPawnsSpawned : null);
			}
			IReadOnlyList<Pawn> readOnlyList = (IReadOnlyList<Pawn>)obj2;
			if (readOnlyList != null)
			{
				for (int j = 0; j < readOnlyList.Count; j++)
				{
					CleanupPawn(readOnlyList[j], stripSet);
				}
			}
		}
	}

	private static void CleanupPawn(Pawn pawn, HashSet<JobDef> strip)
	{
		if (pawn == null || ((Thing)pawn).Destroyed || pawn.jobs == null)
		{
			return;
		}
		JobQueue jobQueue = pawn.jobs.jobQueue;
		if (jobQueue != null && jobQueue.Count > 0)
		{
			jobQueue.RemoveAll(pawn, (Predicate<Job>)((Job job) => job != null && job.def != null && strip.Contains(job.def)));
		}
	}
}
