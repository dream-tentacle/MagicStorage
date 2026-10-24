using System.Collections.Generic;
using HaulersDream.Core;
using Verse;

namespace HaulersDream;

internal static class HaulAsideGuard
{
	private static readonly object sync;

	private static readonly Dictionary<int, int> suppressedUntil;

	private static readonly List<int> expiredScratch;

	static HaulAsideGuard()
	{
		sync = new object();
		suppressedUntil = new Dictionary<int, int>();
		expiredScratch = new List<int>();
		CacheRegistry.Register(Clear);
	}

	internal static void Clear()
	{
		lock (sync)
		{
			suppressedUntil.Clear();
		}
	}

	internal static void NoteAsideRun(Thing thing)
	{
		if (thing == null)
		{
			return;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : 0);
		int thingIDNumber = thing.thingIDNumber;
		bool flag;
		lock (sync)
		{
			flag = suppressedUntil.TryGetValue(thingIDNumber, out var value) && HaulChurnPolicy.IsSuppressed(num, value);
		}
		if (flag)
		{
			return;
		}
		lock (sync)
		{
			PruneExpiredLocked(num);
			suppressedUntil[thingIDNumber] = HaulChurnPolicy.AsideSuppressUntil(num);
		}
	}

	internal static bool ShouldDenyAside(Thing thing)
	{
		if (thing == null || suppressedUntil.Count == 0)
		{
			return false;
		}
		TickManager tickManager = Find.TickManager;
		int nowTick = ((tickManager != null) ? tickManager.TicksGame : 0);
		int thingIDNumber = thing.thingIDNumber;
		lock (sync)
		{
			if (!suppressedUntil.TryGetValue(thingIDNumber, out var value) || !HaulChurnPolicy.IsSuppressed(nowTick, value))
			{
				return false;
			}
			suppressedUntil[thingIDNumber] = HaulChurnPolicy.AsideSuppressUntil(nowTick);
		}
		return true;
	}

	private static void PruneExpiredLocked(int now)
	{
		expiredScratch.Clear();
		foreach (KeyValuePair<int, int> item in suppressedUntil)
		{
			if (!HaulChurnPolicy.IsSuppressed(now, item.Value))
			{
				expiredScratch.Add(item.Key);
			}
		}
		for (int i = 0; i < expiredScratch.Count; i++)
		{
			suppressedUntil.Remove(expiredScratch[i]);
		}
		expiredScratch.Clear();
	}
}
