using System;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

internal static class TrackedMassCache
{
	[ThreadStatic]
	private static TickKeyedMemo<float> memo;

	static TrackedMassCache()
	{
		CacheRegistry.Register(Clear);
	}

	internal static float TrackedMass(Pawn pawn, CompHauledToInventory comp)
	{
		if (pawn == null || comp == null)
		{
			return 0f;
		}
		TickManager tickManager = Find.TickManager;
		int currentTick = ((tickManager != null) ? tickManager.TicksGame : (-1));
		int thingIDNumber = ((Thing)pawn).thingIDNumber;
		if (memo.TryGet(currentTick, thingIDNumber, out var value))
		{
			return value;
		}
		float num = 0f;
		foreach (Thing item in comp.PeekHashSet())
		{
			if (item != null && !item.Destroyed)
			{
				num += (float)item.stackCount * StatExtension.GetStatValue(item, StatDefOf.Mass, true, -1);
			}
		}
		memo.Store(currentTick, thingIDNumber, num);
		return num;
	}

	internal static void Clear()
	{
		memo.Clear();
	}
}
