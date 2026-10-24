using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch]
public static class Patch_CommonSense_IngredientPriority_DoSort
{
	private static MethodBase targetMethod;

	private static readonly string[] DoSortDeclaringTypeNames = new string[3] { "CommonSense.WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_CommonSensePatch", "CommonSense.IngredientPriority+WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_CommonSensePatch", "CommonSense.IngredientPriority" };

	private static bool Prepare()
	{
		if (targetMethod != null)
		{
			return true;
		}
		if (!CommonSenseCompat.IsActive)
		{
			return false;
		}
		Type[] array = new Type[2]
		{
			typeof(List<Thing>),
			typeof(Bill)
		};
		string[] doSortDeclaringTypeNames = DoSortDeclaringTypeNames;
		for (int i = 0; i < doSortDeclaringTypeNames.Length; i++)
		{
			Type type = AccessTools.TypeByName(doSortDeclaringTypeNames[i]);
			if (!(type == null))
			{
				targetMethod = AccessTools.Method(type, "DoSort", array, (Type[])null) ?? AccessTools.Method(type, "DoSort", (Type[])null, (Type[])null);
				if (targetMethod != null)
				{
					break;
				}
			}
		}
		if (targetMethod == null)
		{
			HDLog.Dbg("Common Sense present but its cook-ingredient sort (DoSort) did not resolve on any known fork; 'cook with the most-stocked ingredient first' falls back to HD's own batch-cook picker under this CS build. Harmless — this is an opt-in feature.");
		}
		return targetMethod != null;
	}

	private static MethodBase TargetMethod()
	{
		return targetMethod;
	}

	private static void Postfix(List<Thing> __0, Bill __1)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && settings.cookMostStockFirst)
		{
			SpoilingFirst.ApplyMostStockStable(__0, __1, settings);
		}
	}
}
