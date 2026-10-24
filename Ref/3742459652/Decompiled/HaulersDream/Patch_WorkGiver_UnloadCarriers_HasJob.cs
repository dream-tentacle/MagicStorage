using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_UnloadCarriers), "HasJobOnThing")]
public static class Patch_WorkGiver_UnloadCarriers_HasJob
{
	private static bool Prefix(Pawn pawn, Thing t, bool forced, ref bool __result)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		if (!BulkUnloadGate.ShouldHandle(pawn, t))
		{
			return true;
		}
		if (!BulkUnloadGate.CanDoBulkUnload(pawn, (Pawn)t, forced))
		{
			return true;
		}
		__result = true;
		return false;
	}
}
