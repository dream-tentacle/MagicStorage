using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class StorageEvidence
{
	internal struct PawnCargo
	{
		public ThingDef def;

		public int units;

		public Thing sample;

		public ISlotGroup knownGroup;
	}

	internal static readonly Comparison<PawnCargo> ByDefName;

	[ThreadStatic]
	private static Dictionary<ThingDef, int> scratchUnits;

	[ThreadStatic]
	private static Dictionary<ThingDef, Thing> scratchSample;

	private static int CompareByDefName(PawnCargo a, PawnCargo b)
	{
		string text = ((Def)(a.def?)).defName;
		string text2 = ((Def)(b.def?)).defName;
		if (text == null)
		{
			return (text2 != null) ? 1 : 0;
		}
		if (text2 == null)
		{
			return -1;
		}
		return string.CompareOrdinal(text, text2);
	}

	static StorageEvidence()
	{
		ByDefName = CompareByDefName;
		CacheRegistry.Register(Clear);
	}

	private static void Clear()
	{
		scratchUnits?.Clear();
		scratchSample?.Clear();
	}

	internal static void Collect(Pawn p, List<PawnCargo> into)
	{
		if (into != null)
		{
			into.Clear();
			if (p != null && ((Thing)p).Map != null)
			{
				AddPocketedSurplus(into, p);
				AddPlannedPickups(into, p);
				AddCarriedHaul(into, p);
			}
		}
	}

	internal static int UnitsOf(Pawn p, ThingDef def, List<PawnCargo> scratch)
	{
		if (p == null || def == null || scratch == null)
		{
			return 0;
		}
		Collect(p, scratch);
		int num = 0;
		for (int i = 0; i < scratch.Count; i++)
		{
			if (scratch[i].def == def)
			{
				num += scratch[i].units;
			}
		}
		if (num >= 0)
		{
			return num;
		}
		return 0;
	}

	private static void AddPocketedSurplus(List<PawnCargo> into, Pawn p)
	{
		CompHauledToInventory comp = ((ThingWithComps)p).GetComp<CompHauledToInventory>();
		HashSet<Thing> hashSet = comp?.PeekHashSet();
		if (hashSet == null || hashSet.Count == 0)
		{
			return;
		}
		ThingOwner<Thing> val = p.inventory?.innerContainer;
		if (val == null)
		{
			return;
		}
		Dictionary<ThingDef, int> dictionary = scratchUnits ?? (scratchUnits = new Dictionary<ThingDef, int>());
		Dictionary<ThingDef, Thing> dictionary2 = scratchSample ?? (scratchSample = new Dictionary<ThingDef, Thing>());
		dictionary.Clear();
		dictionary2.Clear();
		foreach (Thing item in hashSet)
		{
			if (item == null || item.Destroyed || item.def == null || !((ThingOwner)val).Contains(item))
			{
				continue;
			}
			int num = InventorySurplus.SurplusOf(p, item, comp, null);
			if (num > 0)
			{
				dictionary[item.def] = (dictionary.TryGetValue(item.def, out var value) ? (value + num) : num);
				if (!dictionary2.TryGetValue(item.def, out var value2) || item.thingIDNumber < value2.thingIDNumber)
				{
					dictionary2[item.def] = item;
				}
			}
		}
		foreach (KeyValuePair<ThingDef, int> item2 in dictionary)
		{
			Accumulate(into, item2.Key, item2.Value, dictionary2[item2.Key], null);
		}
	}

	private static void AddPlannedPickups(List<PawnCargo> into, Pawn p)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		Job curJob = p.CurJob;
		if (curJob == null || curJob.def != HaulersDreamDefOf.HaulersDream_BulkHaul)
		{
			return;
		}
		List<LocalTargetInfo> targetQueueB = curJob.targetQueueB;
		if (targetQueueB == null)
		{
			return;
		}
		List<int> countQueue = curJob.countQueue;
		for (int i = 0; i < targetQueueB.Count; i++)
		{
			LocalTargetInfo val = targetQueueB[i];
			Thing thing = ((LocalTargetInfo)(ref val)).Thing;
			if (thing != null && thing.Spawned && thing.def != null)
			{
				int num = ((countQueue != null && i < countQueue.Count) ? countQueue[i] : 0);
				int num2 = ((num > 0) ? Math.Min(num, thing.stackCount) : thing.stackCount);
				if (num2 > 0)
				{
					Accumulate(into, thing.def, num2, thing, null);
				}
			}
		}
	}

	private static void AddCarriedHaul(List<PawnCargo> into, Pawn p)
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		Job curJob = p.CurJob;
		if (curJob == null)
		{
			return;
		}
		if (curJob.def == JobDefOf.HaulToCell)
		{
			Thing thing = ((LocalTargetInfo)(ref curJob.targetA)).Thing;
			if (thing?.def != null)
			{
				int num = ((curJob.count > 0) ? Math.Min(curJob.count, thing.stackCount) : thing.stackCount);
				if (num > 0)
				{
					Accumulate(into, thing.def, num, thing, GroupAt(((Thing)p).Map, ((LocalTargetInfo)(ref curJob.targetB)).Cell));
				}
			}
		}
		else if (curJob.def == HaulersDreamDefOf.HaulersDream_UnloadInventory)
		{
			Pawn_CarryTracker carryTracker = p.carryTracker;
			Thing val = ((carryTracker != null) ? carryTracker.CarriedThing : null);
			if (val?.def != null && val.stackCount > 0)
			{
				Accumulate(into, val.def, val.stackCount, val, null);
			}
		}
	}

	private static void Accumulate(List<PawnCargo> into, ThingDef def, int units, Thing sample, ISlotGroup knownGroup)
	{
		for (int i = 0; i < into.Count; i++)
		{
			if (into[i].def == def)
			{
				PawnCargo value = into[i];
				value.units += units;
				if (value.sample == null || (sample != null && sample.thingIDNumber < value.sample.thingIDNumber))
				{
					value.sample = sample;
				}
				if (value.knownGroup == null)
				{
					value.knownGroup = knownGroup;
				}
				into[i] = value;
				return;
			}
		}
		into.Add(new PawnCargo
		{
			def = def,
			units = units,
			sample = sample,
			knownGroup = knownGroup
		});
	}

	internal static ISlotGroup DestinationGroupFor(Map map, Pawn carrier, Thing thing)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || carrier == null || thing == null)
		{
			return null;
		}
		using (StorageBuildingFilter.PushContext(StorageFilterContext.Unload))
		{
			IntVec3 cell = default(IntVec3);
			IHaulDestination val = default(IHaulDestination);
			if (!StoreUtility.TryFindBestBetterStorageFor(thing, carrier, map, (StoragePriority)0, ((Thing)carrier).Faction, ref cell, ref val, false))
			{
				return null;
			}
			return GroupAt(map, cell);
		}
	}

	private static ISlotGroup GroupAt(Map map, IntVec3 cell)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || !((IntVec3)(ref cell)).IsValid)
		{
			return null;
		}
		return BulkHaul.BudgetGroupOf(map.haulDestinationManager.SlotGroupAt(cell));
	}
}
