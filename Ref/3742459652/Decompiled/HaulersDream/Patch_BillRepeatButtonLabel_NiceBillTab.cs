using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace HaulersDream;

[HarmonyPatch]
public static class Patch_BillRepeatButtonLabel_NiceBillTab
{
	private static MethodBase ResolveTarget()
	{
		Type typeInAnyAssembly = GenTypes.GetTypeInAnyAssembly("NiceBillTab.TabBillsDrawer", (string)null);
		if (!(typeInAnyAssembly == null))
		{
			return AccessTools.Method(typeInAnyAssembly, "DrawBillPreview", (Type[])null, (Type[])null);
		}
		return null;
	}

	private static bool Prepare()
	{
		return ResolveTarget() != null;
	}

	private static MethodBase TargetMethod()
	{
		return ResolveTarget();
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
	{
		return BatchRepeatButtonLabel.Transpiler(instructions, original);
	}
}
