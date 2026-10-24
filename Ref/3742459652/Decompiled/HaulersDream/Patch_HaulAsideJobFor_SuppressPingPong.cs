using System;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(HaulAIUtility), "HaulAsideJobFor")]
public static class Patch_HaulAsideJobFor_SuppressPingPong
{
	private static bool Prefix(Thing t, ref Job __result)
	{
		if (t != null && HaulAsideGuard.ShouldDenyAside(t))
		{
			__result = null;
			return false;
		}
		return true;
	}

	private static Exception Finalizer(Exception __exception)
	{
		return HDGuard.SeamThrew(__exception, "HaulAIUtility.HaulAsideJobFor (HD ping-pong break)", null, "haul-aside suppression skipped; vanilla's aside job stands for this call.");
	}
}
