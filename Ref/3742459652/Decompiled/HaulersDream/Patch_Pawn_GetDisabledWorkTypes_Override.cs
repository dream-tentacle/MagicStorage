using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(Pawn), "GetDisabledWorkTypes")]
public static class Patch_Pawn_GetDisabledWorkTypes_Override
{
	private static void Postfix(Pawn __instance, ref List<WorkTypeDef> __result)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || __result == null || __result.Count == 0 || (!settings.allPawnsCanHaul && !settings.allPawnsCanClean && !settings.allPawnsCanCutPlants) || ((__instance != null) ? __instance.RaceProps : null) == null || !__instance.RaceProps.Humanlike)
		{
			return;
		}
		List<WorkTypeDef> list = null;
		for (int i = 0; i < __result.Count; i++)
		{
			if (WorkOverride.IsForceEnabled(__result[i], settings))
			{
				if (list == null)
				{
					list = new List<WorkTypeDef>(__result);
				}
				list.Remove(__result[i]);
			}
		}
		if (list != null)
		{
			__result = list;
		}
	}
}
