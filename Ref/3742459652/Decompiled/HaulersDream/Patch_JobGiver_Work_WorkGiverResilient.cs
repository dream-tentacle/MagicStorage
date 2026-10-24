using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver", new Type[]
{
	typeof(Pawn),
	typeof(WorkGiver)
})]
public static class Patch_JobGiver_Work_WorkGiverResilient
{
	private static bool Prefix(WorkGiver giver, ref bool __result)
	{
		if (!WorkGiverBlocklist.IsQuarantined(giver))
		{
			return true;
		}
		__result = false;
		return false;
	}

	private static void Postfix(bool __result, WorkGiver giver)
	{
		if (__result)
		{
			WorkGiverBlocklist.NoteGiverPassedGate(giver);
		}
	}

	private static Exception Finalizer(Exception __exception, ref bool __result, Pawn pawn, WorkGiver giver)
	{
		if (__exception == null)
		{
			return null;
		}
		Type type = ((object)giver)?.GetType();
		if ((type != null && HDFault.IsHaulersDream(type.Assembly)) || HDFault.InvolvesHaulersDream(__exception))
		{
			return __exception;
		}
		string text = type?.FullName ?? "an unknown WorkGiver";
		string text2 = HDFault.DescribeOrigin(__exception);
		HDLog.ErrOnce("the work giver '" + text + "' threw while RimWorld evaluated whether " + (((pawn != null) ? ((Entity)pawn).LabelShort : null) ?? "a pawn") + " can use it. " + ((type != null) ? "This is NOT a Hauler's Dream work giver. " : "") + ((text2 != null) ? ("The innermost frame belonging to another mod is " + text2 + ", so that is the most likely source; please report it there. ") : "No frame in this stack belongs to a mod, so the mod at fault could not be identified here — the stack below is what was available. ") + "Vanilla has no guard here, so this throw would otherwise abort the pawn's ENTIRE work selection every scan (all hauling/cleaning/etc. would stall). Hauler's Dream is skipping just that one work giver for this pawn this scan so the rest of its work keeps running.\n" + HDFault.Render(__exception), ("HD.wgResilient." + text).GetHashCode());
		__result = false;
		return null;
	}
}
