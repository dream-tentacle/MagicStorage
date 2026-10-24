using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class HaulToStack
{
	[ThreadStatic]
	private static int cacheTick;

	[ThreadStatic]
	private static Dictionary<(int thingId, int carrierId, int cellIdx), IntVec3> cellCache;

	static HaulToStack()
	{
		CacheRegistry.Register(Clear);
	}

	internal static void Clear()
	{
		cacheTick = -1;
		cellCache?.Clear();
	}

	internal static IntVec3 FindStackCell(Thing t, Pawn carrier, Map map, Faction faction, IntVec3 vanillaCell)
	{
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : (-1));
		if (num != -1)
		{
			if (cellCache == null)
			{
				cellCache = new Dictionary<(int, int, int), IntVec3>();
			}
			if (num != cacheTick)
			{
				cellCache.Clear();
				cacheTick = num;
			}
		}
		(int, int, int) key = (t.thingIDNumber, ((Thing)carrier).thingIDNumber, ((CellIndices)(ref map.cellIndices)).CellToIndex(vanillaCell));
		if (num != -1 && cellCache.TryGetValue(key, out var value) && (!((IntVec3)(ref value)).IsValid || StoreUtility.IsGoodStoreCell(value, map, t, carrier, faction)))
		{
			return value;
		}
		IntVec3 val = FindStackCellUncached(t, carrier, map, faction, vanillaCell);
		if (num != -1)
		{
			cellCache[key] = val;
		}
		return val;
	}

	private static IntVec3 FindStackCellUncached(Thing t, Pawn carrier, Map map, Faction faction, IntVec3 vanillaCell)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		if (CellHasPartialStackOf(vanillaCell, map, t))
		{
			return IntVec3.Invalid;
		}
		SlotGroup slotGroup = StoreUtility.GetSlotGroup(vanillaCell, map);
		if (((slotGroup != null) ? slotGroup.Settings : null) == null)
		{
			return IntVec3.Invalid;
		}
		if (RimIOTCompat.IsActive && RimIOTCompat.IsNetworkManagedGroup(map, slotGroup))
		{
			return IntVec3.Invalid;
		}
		StoragePriority priority = slotGroup.Settings.Priority;
		Room room = GridsUtility.GetRoom(vanillaCell, map);
		bool hasRoom = room != null;
		Room obj = room;
		bool radiusScan = HaulToStackPolicy.UseRadiusScan(hasRoom, obj == null || obj.TouchesMapEdge);
		IntVec3 origin = (t.SpawnedOrAnyParentSpawned ? t.PositionHeld : ((Thing)carrier).PositionHeld);
		IntVec3 best = IntVec3.Invalid;
		float bestDistSq = float.MaxValue;
		int scanned = 0;
		if (ScanGroup(slotGroup))
		{
			List<SlotGroup> allGroupsListInPriorityOrder = map.haulDestinationManager.AllGroupsListInPriorityOrder;
			for (int i = 0; i < allGroupsListInPriorityOrder.Count; i++)
			{
				SlotGroup val = allGroupsListInPriorityOrder[i];
				if (val == slotGroup || ((val != null) ? val.Settings : null) == null)
				{
					continue;
				}
				StoragePriority priority2 = val.Settings.Priority;
				if (priority2 <= priority)
				{
					if (priority2 < priority)
					{
						break;
					}
					ISlotGroupParent parent = val.parent;
					Thing val2 = (Thing)(object)((parent is Thing) ? parent : null);
					if ((val2 == null || val2.Faction == faction) && ((IHaulDestination)val.parent).HaulDestinationEnabled && val.Settings.AllowedToAccept(t) && !ScanGroup(val))
					{
						break;
					}
				}
			}
		}
		return best;
		bool ScanGroup(SlotGroup group)
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
			if (RimIOTCompat.IsActive && RimIOTCompat.IsNetworkManagedGroup(map, group))
			{
				return true;
			}
			List<IntVec3> cellsList = group.CellsList;
			for (int j = 0; j < cellsList.Count; j++)
			{
				IntVec3 val3 = cellsList[j];
				IntVec3 val4;
				if (radiusScan)
				{
					val4 = val3 - vanillaCell;
					if ((float)((IntVec3)(ref val4)).LengthHorizontalSquared > 400f)
					{
						continue;
					}
				}
				else if (GridsUtility.GetRoom(val3, map) != room)
				{
					continue;
				}
				if (++scanned > 600)
				{
					return false;
				}
				val4 = val3 - origin;
				float num = ((IntVec3)(ref val4)).LengthHorizontalSquared;
				if ((!((IntVec3)(ref best)).IsValid || HaulToStackPolicy.IsBetter(candidateIsStack: true, num, bestIsStack: true, bestDistSq)) && CellHasPartialStackOf(val3, map, t) && StoreUtility.IsGoodStoreCell(val3, map, t, carrier, faction))
				{
					best = val3;
					bestDistSq = num;
				}
			}
			return true;
		}
	}

	private static bool CellHasPartialStackOf(IntVec3 cell, Map map, Thing t)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Invalid comparison between Unknown and I4
		if (!GenGrid.InBounds(cell, map))
		{
			return false;
		}
		List<Thing> list = map.thingGrid.ThingsListAtFast(cell);
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if ((int)val.def.category == 2 && val.stackCount < val.def.stackLimit && val != t && t.CanStackWith(val))
			{
				return true;
			}
		}
		return false;
	}
}
