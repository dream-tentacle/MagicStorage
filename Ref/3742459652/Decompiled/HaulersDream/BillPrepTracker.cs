using System.Collections.Generic;
using Verse;

namespace HaulersDream;

internal static class BillPrepTracker
{
	private const int CooldownTicks = 2500;

	private static readonly Dictionary<int, int> skipUntilTick = new Dictionary<int, int>();

	internal static void NoteEmptyRun(Pawn pawn)
	{
		if (pawn != null && Find.TickManager != null)
		{
			skipUntilTick[((Thing)pawn).thingIDNumber] = Find.TickManager.TicksGame + 2500;
		}
	}

	internal static bool ShouldSkip(Pawn pawn)
	{
		if (pawn == null || Find.TickManager == null)
		{
			return false;
		}
		if (skipUntilTick.TryGetValue(((Thing)pawn).thingIDNumber, out var value))
		{
			return Find.TickManager.TicksGame < value;
		}
		return false;
	}
}
