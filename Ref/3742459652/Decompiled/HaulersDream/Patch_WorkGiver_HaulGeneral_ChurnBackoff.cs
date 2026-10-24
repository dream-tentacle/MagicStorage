using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_HaulGeneral), "JobOnThing")]
public static class Patch_WorkGiver_HaulGeneral_ChurnBackoff
{
	[HarmonyPriority(800)]
	private static void Postfix(ref Job __result, Thing t, bool forced)
	{
		if (!(__result == null || forced) && HaulChurnGuard.IsBackedOff(t))
		{
			__result = null;
		}
	}
}
