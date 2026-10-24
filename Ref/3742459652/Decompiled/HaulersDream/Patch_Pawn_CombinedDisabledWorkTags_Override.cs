using HarmonyLib;
using Verse;

namespace HaulersDream;

[HarmonyPatch(/*Could not decode attribute arguments.*/)]
public static class Patch_Pawn_CombinedDisabledWorkTags_Override
{
	private static void Postfix(Pawn __instance, ref WorkTags __result)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && __result && (settings.allPawnsCanHaul || settings.allPawnsCanClean) && ((__instance != null) ? __instance.RaceProps : null) != null && __instance.RaceProps.Humanlike)
		{
			if (settings.allPawnsCanHaul)
			{
				__result = (WorkTags)((uint)__result & 0xFFFFBFFFu);
			}
			if (settings.allPawnsCanClean)
			{
				__result = (WorkTags)((uint)__result & 0xFFFFDFFFu);
			}
		}
	}
}
