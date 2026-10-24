using System;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

internal static class PawnMassCache
{
	[ThreadStatic]
	private static TickKeyedMemo<PawnMass> memo;

	static PawnMassCache()
	{
		CacheRegistry.Register(Clear);
	}

	internal static PawnMass MassInfo(Pawn pawn)
	{
		if (pawn == null)
		{
			return default(PawnMass);
		}
		TickManager tickManager = Find.TickManager;
		int currentTick = ((tickManager != null) ? tickManager.TicksGame : (-1));
		int thingIDNumber = ((Thing)pawn).thingIDNumber;
		if (memo.TryGet(currentTick, thingIDNumber, out var value))
		{
			return value;
		}
		PawnMass pawnMass = new PawnMass(CarryCapacity.Of(pawn), MassUtility.GearAndInventoryMass(pawn));
		memo.Store(currentTick, thingIDNumber, pawnMass);
		return pawnMass;
	}

	internal static float Capacity(Pawn pawn)
	{
		return MassInfo(pawn).Capacity;
	}

	internal static float CurrentMass(Pawn pawn)
	{
		return MassInfo(pawn).CurrentMass;
	}

	internal static void Clear()
	{
		memo.Clear();
	}
}
