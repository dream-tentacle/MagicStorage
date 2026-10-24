using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_AllowMix")]
public static class Patch_WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix
{
	private static readonly MethodInfo SortBy3 = AccessTools.GetDeclaredMethods(typeof(GenCollection)).Find((MethodInfo m) => m.Name == "SortBy" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 3 && m.GetParameters().Length == 3);

	private static readonly MethodInfo SortAllowMix = AccessTools.Method(typeof(SpoilingFirst), "SortAllowMix", (Type[])null, (Type[])null);

	private static readonly MethodInfo GetSettings = AccessTools.PropertyGetter(typeof(HaulersDreamMod), "Settings");

	private static bool Prepare()
	{
		return !CommonSenseCompat.IsActive;
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		if (SortBy3 == null || SortAllowMix == null || GetSettings == null)
		{
			HDLog.Err("cook spoiling-first transpiler: could not resolve target/replacement methods; leaving TryFindBestBillIngredientsInSet_AllowMix unpatched (vanilla order).");
			foreach (CodeInstruction instruction in instructions)
			{
				yield return instruction;
			}
			yield break;
		}
		bool patched = false;
		foreach (CodeInstruction instruction2 in instructions)
		{
			if (!patched && instruction2.opcode == OpCodes.Call && instruction2.operand is MethodInfo { IsGenericMethod: not false } methodInfo && methodInfo.DeclaringType == typeof(GenCollection) && methodInfo.Name == "SortBy" && methodInfo.GetGenericArguments().Length == 3 && methodInfo.GetParameters().Length == 3)
			{
				yield return CodeInstructionExtensions.MoveBlocksFrom(CodeInstructionExtensions.MoveLabelsFrom(new CodeInstruction(OpCodes.Ldarg_1, (object)null), instruction2), instruction2);
				yield return new CodeInstruction(OpCodes.Call, (object)GetSettings);
				yield return new CodeInstruction(OpCodes.Call, (object)SortAllowMix);
				patched = true;
			}
			else
			{
				yield return instruction2;
			}
		}
		if (!patched)
		{
			HDLog.Err("cook spoiling-first transpiler: SortBy call not found in TryFindBestBillIngredientsInSet_AllowMix; cook spoiling-first is inactive (vanilla order). RimWorld may have changed the method.");
		}
	}
}
