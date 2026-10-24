using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch]
public static class Patch_HaulUrgently_BulkHaul
{
	private static readonly string[] UrgentWorkGiverTypes = new string[2] { "KeyzAllowUtilities.WorkGiver_HaulUrgently", "AllowTool.WorkGiver_HaulUrgently" };

	private static bool Prepare()
	{
		string[] urgentWorkGiverTypes = UrgentWorkGiverTypes;
		for (int i = 0; i < urgentWorkGiverTypes.Length; i++)
		{
			if (AccessTools.TypeByName(urgentWorkGiverTypes[i]) != null)
			{
				return true;
			}
		}
		return false;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		string[] urgentWorkGiverTypes = UrgentWorkGiverTypes;
		for (int i = 0; i < urgentWorkGiverTypes.Length; i++)
		{
			Type type = AccessTools.TypeByName(urgentWorkGiverTypes[i]);
			MethodInfo methodInfo = ((type != null) ? AccessTools.Method(type, "JobOnThing", new Type[3]
			{
				typeof(Pawn),
				typeof(Thing),
				typeof(bool)
			}, (Type[])null) : null);
			if (methodInfo != null)
			{
				yield return methodInfo;
			}
		}
	}

	private static void Postfix(ref Job __result, Pawn __0, Thing __1, bool __2)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return;
		}
		if (settings.bulkHaulUrgent)
		{
			Job val = UrgentHaulBulk.TryBuild(__0, __1, __result, __2, settings.bulkHaulUrgentIncludeNonUrgent);
			if (val != null)
			{
				__result = val;
				return;
			}
		}
		if (settings.bulkHaulUrgentIncludeNonUrgent)
		{
			Job val2 = BulkHaul.TryBuildBulkJob(__0, __1, __result, __2, forceSweep: true);
			if (val2 != null)
			{
				__result = val2;
			}
		}
	}
}
