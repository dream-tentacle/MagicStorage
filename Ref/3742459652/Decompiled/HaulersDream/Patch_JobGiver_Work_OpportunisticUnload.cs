using System;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_Work), "TryIssueJobPackage")]
public static class Patch_JobGiver_Work_OpportunisticUnload
{
	private const string Seam = "JobGiver_Work.TryIssueJobPackage (HD opportunistic unload/load)";

	[HarmonyPriority(0)]
	private static void Postfix(ref ThinkResult __result, Pawn pawn, JobGiver_Work __instance)
	{
		try
		{
			CompHauledToInventory compHauledToInventory = ((pawn != null) ? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() : null);
			if (compHauledToInventory != null && compHauledToInventory.PeekHashSet().Count != 0)
			{
				PostfixInner(ref __result, pawn, __instance);
			}
		}
		catch (Exception ex)
		{
			HDGuard.SeamDegraded(ex, "JobGiver_Work.TryIssueJobPackage (HD opportunistic unload)", pawn, "kept vanilla's chosen work job, so the pawn keeps working.");
		}
	}

	private static void PostfixInner(ref ThinkResult __result, Pawn pawn, JobGiver_Work __instance)
	{
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		if (((ThinkResult)(ref __result)).IsValid && ((ThinkResult)(ref __result)).Job != null)
		{
			bool isProtected = ProtectedWork.IsProtected(((ThinkResult)(ref __result)).Job, __instance.emergency);
			bool isEmergency = __instance.emergency || (((ThinkResult)(ref __result)).Job.workGiverDef?.emergency ?? false);
			if (ProtectedWorkPolicy.MustHardBlockDivert(isProtected, isEmergency))
			{
				return;
			}
			bool protectedZeroDetourOnly = ProtectedWorkPolicy.MayZeroDetourUnload(isProtected, isEmergency, ((ThinkResult)(ref __result)).Job.def == JobDefOf.DoBill);
			JobDef def = ((ThinkResult)(ref __result)).Job.def;
			if (def != JobDefOf.UnloadInventory && def?.driverClass != null && def.driverClass.Assembly != typeof(Patch_JobGiver_Work_OpportunisticUnload).Assembly && def.driverClass.Name.IndexOf("Unload", StringComparison.Ordinal) >= 0)
			{
				return;
			}
			bool runOver = WorkRunPolicy.IsRunOver(OpportunisticUnload.ClassifyJobDef(((ThinkResult)(ref __result)).Job.def));
			if (OpportunisticUnload.ShouldDivert(pawn, ((ThinkResult)(ref __result)).Job, runOver, protectedZeroDetourOnly) || OpportunisticUnload.ShouldUnloadBeforeRelocation(pawn, ((ThinkResult)(ref __result)).Job))
			{
				Job val = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_UnloadInventory);
				if (val.TryMakePreToilReservations(pawn, false))
				{
					OpportunisticUnload.NotifyDiverted(pawn);
					__result = new ThinkResult(val, ((ThinkResult)(ref __result)).SourceNode, ((ThinkResult)(ref __result)).Tag, ((ThinkResult)(ref __result)).FromQueue);
				}
			}
		}
		else
		{
			Job val2 = OpportunisticUnload.TryGetEndOfRunUnloadJob(pawn);
			if (val2 != null)
			{
				__result = new ThinkResult(val2, (ThinkNode)(object)__instance, (JobTag?)(JobTag)15, false);
			}
		}
	}

	private static Exception Finalizer(Exception __exception, Pawn pawn)
	{
		Type scanContextGiver = WorkGiverBlocklist.ScanContextGiver;
		try
		{
			if (__exception == null)
			{
				return null;
			}
			if (HDFault.InvolvesHaulersDream(__exception))
			{
				return HDGuard.SeamThrew(__exception, "JobGiver_Work.TryIssueJobPackage (HD opportunistic unload/load)", pawn, "that pawn's entire work selection failed this scan (hauling/cleaning/etc. will stall).");
			}
			WorkGiverBlocklist.NoteSeamFault(__exception, scanContextGiver, pawn);
			return HDGuard.SeamContained(__exception, "JobGiver_Work.TryIssueJobPackage (HD opportunistic unload/load)", pawn, "Hauler's Dream contained it so the rest of the game keeps running - this pawn simply found no work this scan, instead of doing no work at all from now on while still eating and sleeping.");
		}
		finally
		{
			WorkGiverBlocklist.ClearScanContext();
		}
	}
}
