using HarmonyLib;
using RimWorld;

namespace HaulersDream;

[HarmonyPatch(typeof(Selector), "ClearSelection")]
public static class Patch_Selector_ClearSelection
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		Patch_Selector_Select.NotifyCleared();
	}
}
