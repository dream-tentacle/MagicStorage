using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch]
public static class Patch_GenLeaving_DoLeavingsFor
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(GenLeaving), "DoLeavingsFor", new Type[6]
		{
			typeof(Thing),
			typeof(Map),
			typeof(DestroyMode),
			typeof(CellRect),
			typeof(Predicate<IntVec3>),
			typeof(List<Thing>)
		}, (Type[])null);
	}

	private static void Prefix(Thing __0, Map __1, DestroyMode __2, CellRect __3)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		if ((int)__2 == 4 && __1 != null)
		{
			YieldRouter.BeginDeconstructCapture(__3, __1, __0);
		}
	}

	private static void Finalizer(DestroyMode __2)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		if ((int)__2 == 4)
		{
			YieldRouter.EndDeconstructCapture();
		}
	}
}
