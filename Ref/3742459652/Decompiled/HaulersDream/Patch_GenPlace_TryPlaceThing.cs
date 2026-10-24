using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace HaulersDream;

[HarmonyPatch]
public static class Patch_GenPlace_TryPlaceThing
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(GenPlace), "TryPlaceThing", new Type[9]
		{
			typeof(Thing),
			typeof(IntVec3),
			typeof(Map),
			typeof(ThingPlaceMode),
			typeof(Thing).MakeByRefType(),
			typeof(Action<Thing, int>),
			typeof(Predicate<IntVec3>),
			typeof(Rot4?),
			typeof(int)
		}, (Type[])null);
	}

	private static bool Prefix(Thing thing, IntVec3 center, Map map, ThingPlaceMode mode, ref Thing lastResultingThing, ref bool __result, out Pawn __state)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		return YieldRouter.OnTryPlaceThing(thing, center, map, mode, ref lastResultingThing, ref __result, out __state);
	}

	private static void Postfix(Thing lastResultingThing, Pawn __state)
	{
		YieldRouter.OnTryPlaceThingPost(lastResultingThing, __state);
	}
}
