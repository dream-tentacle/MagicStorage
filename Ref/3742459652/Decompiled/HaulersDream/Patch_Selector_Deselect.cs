using HarmonyLib;
using RimWorld;

namespace HaulersDream;

[HarmonyPatch(typeof(Selector), "Deselect")]
public static class Patch_Selector_Deselect
{
	[HarmonyPostfix]
	private static void Postfix(Selector __instance)
	{
		if (__instance != null && __instance.NumSelected == 0)
		{
			Patch_Selector_Select.NotifyCleared();
		}
	}
}
