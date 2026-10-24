using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;

namespace HaulersDream;

[HarmonyPatch]
public static class Patch_BillRepeatButtonLabel_Vanilla
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(Bill_Production), "DoConfigInterface", (Type[])null, (Type[])null);
		yield return AccessTools.Method(typeof(Dialog_BillConfig), "DoWindowContents", (Type[])null, (Type[])null);
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
	{
		return BatchRepeatButtonLabel.Transpiler(instructions, original);
	}
}
