using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HaulersDream.Core;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class HaulChurnGuard
{
	private sealed class Counter
	{
		internal int loadID;

		internal int retargets;

		internal bool asideDegraded;
	}

	private static ConditionalWeakTable<Job, Counter> retargetsByJob;

	private static readonly object sync;

	private static readonly Dictionary<int, int> backoffUntil;

	private static readonly Dictionary<int, (int lastFailTick, int failCount)> thingFails;

	private static readonly List<int> expiredScratch;

	private static readonly Dictionary<int, (int lastTick, int count, int lastStackCount)> bulkAnchors;

	private static readonly HashSet<int> loopWarned;

	[ThreadStatic]
	private static int placementWrapperDepth;

	internal static bool PlacementWrapperIsAncestor => placementWrapperDepth > 0;

	static HaulChurnGuard()
	{
		retargetsByJob = new ConditionalWeakTable<Job, Counter>();
		sync = new object();
		backoffUntil = new Dictionary<int, int>();
		thingFails = new Dictionary<int, (int, int)>();
		expiredScratch = new List<int>();
		bulkAnchors = new Dictionary<int, (int, int, int)>();
		loopWarned = new HashSet<int>();
		CacheRegistry.Register(Clear);
	}

	internal static void Clear()
	{
		lock (sync)
		{
			backoffUntil.Clear();
			thingFails.Clear();
			bulkAnchors.Clear();
			loopWarned.Clear();
		}
		retargetsByJob = new ConditionalWeakTable<Job, Counter>();
	}

	internal static void EnterPlacementWrapper()
	{
		placementWrapperDepth++;
	}

	internal static void ExitPlacementWrapper()
	{
		placementWrapperDepth--;
	}

	private static Counter CounterFor(Job job)
	{
		Counter value = retargetsByJob.GetValue(job, (Job _) => new Counter
		{
			loadID = job.loadID
		});
		if (value.loadID != job.loadID)
		{
			value.loadID = job.loadID;
			value.retargets = 0;
			value.asideDegraded = false;
		}
		return value;
	}

	internal static int CountRetarget(Job job)
	{
		return ++CounterFor(job).retargets;
	}

	internal static int PeekRetargets(Job job)
	{
		if (!retargetsByJob.TryGetValue(job, out var value) || value.loadID != job.loadID)
		{
			return 0;
		}
		return value.retargets;
	}

	internal static void NoteAsideDegrade(Job job)
	{
		CounterFor(job).asideDegraded = true;
	}

	internal static void NotifyProgress(Job job)
	{
		if (retargetsByJob.TryGetValue(job, out var value) && value.loadID == job.loadID)
		{
			value.retargets = 0;
		}
	}

	internal static bool WasAsideDegraded(Job job)
	{
		if (retargetsByJob.TryGetValue(job, out var value) && value.loadID == job.loadID)
		{
			return value.asideDegraded;
		}
		return false;
	}

	internal static void NotifyPlaced(Job job)
	{
		retargetsByJob.Remove(job);
	}

	internal static void StampBackoff(Thing thing, int ticks = 600)
	{
		if (thing == null)
		{
			return;
		}
		lock (sync)
		{
			TickManager tickManager = Find.TickManager;
			StampBackoffLocked(thing, (tickManager != null) ? tickManager.TicksGame : 0, ticks);
		}
	}

	private static void StampBackoffLocked(Thing thing, int now, int ticks = 600)
	{
		expiredScratch.Clear();
		foreach (KeyValuePair<int, int> item in backoffUntil)
		{
			if (!HaulChurnPolicy.IsSuppressed(now, item.Value))
			{
				expiredScratch.Add(item.Key);
			}
		}
		for (int i = 0; i < expiredScratch.Count; i++)
		{
			backoffUntil.Remove(expiredScratch[i]);
		}
		expiredScratch.Clear();
		backoffUntil[thing.thingIDNumber] = now + ticks;
	}

	internal static void NoteThingHaulFailed(Thing thing)
	{
		if (thing == null)
		{
			return;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : 0);
		lock (sync)
		{
			thingFails.TryGetValue(thing.thingIDNumber, out (int, int) value);
			HaulChurnPolicy.RecordThingFailure(num, value.Item1, value.Item2, out var newLastFailTick, out var newCount);
			if (HaulChurnPolicy.ShouldBackOffThing(newCount))
			{
				thingFails.Remove(thing.thingIDNumber);
				StampBackoffLocked(thing, num);
				HDLog.Dbg($"{((Entity)thing).LabelShort} failed {newCount} storage hauls in quick succession; " + "backing it off from the automatic haul scan.");
			}
			else
			{
				thingFails[thing.thingIDNumber] = (newLastFailTick, newCount);
			}
		}
	}

	internal static void NoteThingHaulSucceeded(Thing thing)
	{
		if (thing == null)
		{
			return;
		}
		lock (sync)
		{
			thingFails.Remove(thing.thingIDNumber);
		}
	}

	internal static void NoteBulkAnchor(Thing thing)
	{
		if (thing?.def == null)
		{
			return;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : 0);
		string text = null;
		lock (sync)
		{
			PruneBulkAnchorsLocked(num);
			bulkAnchors.TryGetValue(thing.thingIDNumber, out (int, int, int) value);
			HaulChurnPolicy.RecordNetZeroReanchor(num, value.Item1, value.Item2, value.Item3, thing.stackCount, out var newLastTick, out var newCount);
			if (HaulChurnPolicy.ShouldBackOffReanchored(newCount))
			{
				bulkAnchors.Remove(thing.thingIDNumber);
				StampBackoffLocked(thing, num, 2500);
				if (loopWarned.Add(thing.thingIDNumber))
				{
					text = $"{((Entity)thing).LabelShort} was bulk-hauled {newCount} times in quick succession without " + "moving (net-zero). Another mod is very likely returning it to where HD keeps re-hauling it (a logistics or loadout mod, e.g. RimIOT). HD is backing it off its automatic haul scan so pawns stop looping; a forced player order still hauls it. Please report the mod combination (issue #214) if this is unexpected.";
				}
			}
			else
			{
				bulkAnchors[thing.thingIDNumber] = (newLastTick, newCount, thing.stackCount);
			}
		}
		if (text != null)
		{
			HDLog.Warn(text);
		}
	}

	internal static void NoteForeignRetarget(Pawn pawn, Thing anchor, Thing swappedTo)
	{
		if (anchor != null)
		{
			StampBackoff(anchor, 2500);
			bool flag;
			lock (sync)
			{
				flag = loopWarned.Add(anchor.thingIDNumber);
			}
			if (flag)
			{
				HDLog.Warn(((pawn != null) ? ((Entity)pawn).LabelShort : "A pawn") + "'s bulk haul of " + ((Entity)anchor).LabelShort + " was retargeted by another mod into logistics-network storage (" + (((swappedTo != null) ? ((Entity)swappedTo).LabelShort : null) ?? "a network stack") + "). HD declined to pocket the substitute and is backing the original off its automatic scan so the terminal haul/unload loop can't form; a forced player order still hauls it. If unexpected, please report the mod combination (issue #214).");
			}
		}
	}

	private static void PruneBulkAnchorsLocked(int now)
	{
		if (bulkAnchors.Count <= 64)
		{
			return;
		}
		expiredScratch.Clear();
		foreach (KeyValuePair<int, (int, int, int)> bulkAnchor in bulkAnchors)
		{
			if (now - bulkAnchor.Value.Item1 > 180)
			{
				expiredScratch.Add(bulkAnchor.Key);
			}
		}
		for (int i = 0; i < expiredScratch.Count; i++)
		{
			bulkAnchors.Remove(expiredScratch[i]);
		}
		expiredScratch.Clear();
	}

	internal static bool IsBackedOff(Thing thing)
	{
		if (thing == null || backoffUntil.Count == 0)
		{
			return false;
		}
		TickManager tickManager = Find.TickManager;
		int nowTick = ((tickManager != null) ? tickManager.TicksGame : 0);
		lock (sync)
		{
			int value;
			return backoffUntil.TryGetValue(thing.thingIDNumber, out value) && HaulChurnPolicy.IsSuppressed(nowTick, value);
		}
	}
}
