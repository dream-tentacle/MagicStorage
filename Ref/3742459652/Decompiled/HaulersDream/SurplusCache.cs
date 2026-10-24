using System;
using HaulersDream.Core;
using Verse;

namespace HaulersDream;

internal static class SurplusCache
{
	[ThreadStatic]
	private static TickKeyedMemo<bool> anyMemo;

	[ThreadStatic]
	private static TickKeyedMemo<bool> ruledMemo;

	static SurplusCache()
	{
		CacheRegistry.Register(Clear);
	}

	internal static bool HasAnySurplus(Pawn pawn)
	{
		if (pawn == null)
		{
			return false;
		}
		TickManager tickManager = Find.TickManager;
		int currentTick = ((tickManager != null) ? tickManager.TicksGame : (-1));
		int thingIDNumber = ((Thing)pawn).thingIDNumber;
		if (anyMemo.TryGet(currentTick, thingIDNumber, out var value))
		{
			return value;
		}
		bool flag = InventorySurplus.HasAnySurplus(pawn);
		anyMemo.Store(currentTick, thingIDNumber, flag);
		return flag;
	}

	internal static bool HasAnyRuledSurplus(Pawn pawn)
	{
		if (pawn == null)
		{
			return false;
		}
		TickManager tickManager = Find.TickManager;
		int currentTick = ((tickManager != null) ? tickManager.TicksGame : (-1));
		int thingIDNumber = ((Thing)pawn).thingIDNumber;
		if (ruledMemo.TryGet(currentTick, thingIDNumber, out var value))
		{
			return value;
		}
		bool flag = InventorySurplus.HasAnyRuledSurplus(pawn);
		ruledMemo.Store(currentTick, thingIDNumber, flag);
		return flag;
	}

	internal static void Clear()
	{
		anyMemo.Clear();
		ruledMemo.Clear();
	}
}
