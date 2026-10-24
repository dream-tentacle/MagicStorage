using HarmonyLib;
using HaulersDream.Core;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(StrippableUtility), "CanBeStrippedByColony")]
public static class Patch_CanBeStrippedByColony_LeaveOnCorpse
{
	private static void Postfix(Thing th, ref bool __result)
	{
		if (!__result)
		{
			return;
		}
		Corpse val = (Corpse)(object)((th is Corpse) ? th : null);
		if (val != null)
		{
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings != null && StripPolicy.LeavesAnyTainted(settings.taintedSmeltablePolicy, settings.taintedNonSmeltablePolicy) && !CorpseStripper.AnythingHdWouldStrip(val, settings))
			{
				__result = false;
			}
		}
	}
}
