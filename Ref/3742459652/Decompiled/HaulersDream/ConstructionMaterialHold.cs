using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class ConstructionMaterialHold
{
	[ThreadStatic]
	private static TickKeyedMemo<bool> memo;

	[ThreadStatic]
	private static List<Thing> scratchScan;

	[ThreadStatic]
	private static HashSet<ThingDef> scratchDefs;

	internal static bool HoldsMaterialForActiveConstruction(Pawn pawn, CompHauledToInventory comp, bool forced = false)
	{
		ThingOwner<Thing> val = pawn?.inventory?.innerContainer;
		if (val == null || comp == null || ((Thing)pawn).Map == null)
		{
			return false;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : 0);
		if (!ConstructionHoldPolicy.MayHoldAtAll(forced, num - comp.lastYieldTick))
		{
			return false;
		}
		HashSet<Thing> hashSet = comp.PeekHashSet();
		if (hashSet.Count == 0)
		{
			return false;
		}
		return ConstructionHoldPolicy.ShouldHoldMaterial(forced, ConstructionWantsHeldMaterial(pawn, hashSet, val), num - comp.lastYieldTick);
	}

	internal static bool MoreConstructWorkQueued(Pawn pawn)
	{
		JobQueue val = pawn?.jobs?.jobQueue;
		if (val == null)
		{
			return false;
		}
		for (int i = 0; i < val.Count; i++)
		{
			JobDef val2 = val[i]?.job?.def;
			if (val2 != null && (HdJobDefSets.ConstructDeliverJobs.Contains(val2) || val2 == JobDefOf.FinishFrame))
			{
				return true;
			}
		}
		return false;
	}

	internal static void Clear()
	{
		memo.Clear();
	}

	private static bool ConstructionWantsHeldMaterial(Pawn pawn, HashSet<Thing> tagged, ThingOwner<Thing> inv)
	{
		TickManager tickManager = Find.TickManager;
		int currentTick = ((tickManager != null) ? tickManager.TicksGame : (-1));
		if (memo.TryGet(currentTick, ((Thing)pawn).thingIDNumber, out var value))
		{
			return value;
		}
		bool flag = OnConstructionJobNow(pawn) || MoreConstructWorkQueued(pawn) || NearbySiteWantsHeldMaterial(pawn, tagged, inv);
		memo.Store(currentTick, ((Thing)pawn).thingIDNumber, flag);
		return flag;
	}

	private static bool OnConstructionJobNow(Pawn pawn)
	{
		return OpportunisticUnload.ClassifyJobDef(pawn.CurJobDef) == WorkRunKind.Construction;
	}

	private static bool NearbySiteWantsHeldMaterial(Pawn pawn, HashSet<Thing> tagged, ThingOwner<Thing> inv)
	{
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		Map map = ((Thing)pawn).Map;
		if (map.listerThings.ThingsInGroup((ThingRequestGroup)9).Count == 0 && map.listerThings.ThingsInGroup((ThingRequestGroup)11).Count == 0)
		{
			return false;
		}
		List<Thing> list = scratchScan ?? (scratchScan = new List<Thing>());
		HashSet<ThingDef> hashSet = scratchDefs ?? (scratchDefs = new HashSet<ThingDef>());
		hashSet.Clear();
		foreach (Thing item in tagged)
		{
			if (item == null || item.def == null || !((ThingOwner)inv).Contains(item) || !hashSet.Add(item.def))
			{
				continue;
			}
			list.Clear();
			InventoryConstructDelivery.CollectNeedersNear(map, item.def, pawn, ((Thing)pawn).Position, list);
			for (int i = 0; i < list.Count; i++)
			{
				if (ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit(list[i]), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0))
				{
					list.Clear();
					return true;
				}
			}
		}
		list.Clear();
		return false;
	}

	static ConstructionMaterialHold()
	{
		CacheRegistry.Register(Clear);
	}
}
