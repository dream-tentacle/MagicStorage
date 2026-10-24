using System;
using HarmonyLib;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(Pawn_CarryTracker), "TryStartCarry", new Type[]
{
	typeof(Thing),
	typeof(int),
	typeof(bool)
})]
public static class Patch_TryStartCarry_AutoStrip
{
	private static void Prefix(Pawn_CarryTracker __instance, Thing item)
	{
		Corpse val = (Corpse)(object)((item is Corpse) ? item : null);
		if (val != null)
		{
			CorpseStripper.MaybeStripForHaul(__instance.pawn, val);
		}
	}

	private static Exception Finalizer(Exception __exception, Pawn_CarryTracker __instance)
	{
		return HDGuard.SeamThrew(__exception, "Pawn_CarryTracker.TryStartCarry (HD auto-strip-on-haul)", __instance?.pawn, "the haul that triggered the auto-strip failed.");
	}
}
