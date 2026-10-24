using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(Toils_Recipe), "CalculateIngredients")]
public static class Patch_Toils_Recipe_CalculateIngredients_CremationStrip
{
	private static bool Prepare()
	{
		return AccessTools.Method(typeof(Toils_Recipe), "CalculateIngredients", (Type[])null, (Type[])null) != null;
	}

	private static void Postfix(List<Thing> __result, Job job, Pawn actor)
	{
		if (__result != null && job != null)
		{
			RecipeDef recipeDef = job.RecipeDef;
			for (int i = 0; i < __result.Count; i++)
			{
				CorpseStripper.MaybeStripForCremation(actor, __result[i], recipeDef);
			}
		}
	}
}
