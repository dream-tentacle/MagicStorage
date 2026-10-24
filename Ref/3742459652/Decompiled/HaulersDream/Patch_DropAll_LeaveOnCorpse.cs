using System;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(Pawn_ApparelTracker), "DropAll")]
public static class Patch_DropAll_LeaveOnCorpse
{
	private static void Prefix(Pawn_ApparelTracker __instance, ref Predicate<Apparel> selector)
	{
		Pawn pawn = __instance.pawn;
		if (pawn == null || !pawn.Dead)
		{
			return;
		}
		HaulersDreamSettings s = HaulersDreamMod.Settings;
		if (s != null && StripPolicy.LeavesAnyTainted(s.taintedSmeltablePolicy, s.taintedNonSmeltablePolicy))
		{
			Predicate<Apparel> original = selector;
			selector = (Apparel ap) => !CorpseStripper.StaysOnCorpse(ap, s) && (original == null || original(ap));
		}
	}
}
