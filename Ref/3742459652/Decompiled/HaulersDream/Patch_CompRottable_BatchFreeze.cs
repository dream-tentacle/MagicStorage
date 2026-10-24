using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(CompRottable), "TickInterval", new Type[] { typeof(int) })]
public static class Patch_CompRottable_BatchFreeze
{
	private static bool Prefix(CompRottable __instance)
	{
		ThingWithComps val = ((ThingComp)(__instance?)).parent;
		if (val == null)
		{
			return true;
		}
		IThingHolder parentHolder = ((Thing)val).ParentHolder;
		Pawn_InventoryTracker val2 = (Pawn_InventoryTracker)(object)((parentHolder is Pawn_InventoryTracker) ? parentHolder : null);
		if (val2 != null && val2.pawn?.jobs?.curDriver is JobDriver_BatchCraft jobDriver_BatchCraft && jobDriver_BatchCraft.ShouldFreezeRot((Thing)(object)val))
		{
			return false;
		}
		return true;
	}
}
