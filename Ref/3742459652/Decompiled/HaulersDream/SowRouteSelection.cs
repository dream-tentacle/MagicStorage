using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class SowRouteSelection
{
	public static List<IntVec3> Select(Pawn pawn, IntVec3 anchor, Zone_Growing zone, SowRouteMode mode, int amount, int radius, IReadOnlyList<IntVec3> mustInclude, out bool capped, out int mustIncludeCount)
	{
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		capped = false;
		mustIncludeCount = 0;
		List<IntVec3> list = new List<IntVec3>();
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || zone == null || ((Zone)zone).Map != ((Thing)pawn).Map)
		{
			return list;
		}
		Map map = ((Thing)pawn).Map;
		ThingDef plantDefToGrow = zone.GetPlantDefToGrow();
		if (plantDefToGrow?.plant == null)
		{
			return list;
		}
		List<IntVec3> list2 = new List<IntVec3>();
		HashSet<IntVec3> hashSet = new HashSet<IntVec3>();
		if (IsSowableCell(pawn, anchor, zone, plantDefToGrow) && hashSet.Add(anchor))
		{
			list2.Add(anchor);
		}
		if (mustInclude != null)
		{
			for (int i = 0; i < mustInclude.Count; i++)
			{
				IntVec3 val = mustInclude[i];
				if (!hashSet.Contains(val) && IsSowableCell(pawn, val, zone, plantDefToGrow) && hashSet.Add(val))
				{
					list2.Add(val);
				}
			}
		}
		mustIncludeCount = list2.Count;
		List<IntVec3> list3 = new List<IntVec3>();
		float num = ((radius <= 0) ? 1 : radius);
		List<IntVec3> cells = ((Zone)zone).cells;
		for (int j = 0; j < cells.Count; j++)
		{
			IntVec3 val2 = cells[j];
			if (hashSet.Contains(val2))
			{
				continue;
			}
			if (mode == SowRouteMode.Radius)
			{
				IntVec3 val3 = val2 - anchor;
				if (((IntVec3)(ref val3)).LengthHorizontal > num)
				{
					continue;
				}
			}
			if (IsSowableCell(pawn, val2, zone, plantDefToGrow))
			{
				list3.Add(val2);
			}
		}
		list3.Sort((IntVec3 a, IntVec3 b) => ((CellIndices)(ref map.cellIndices)).CellToIndex(a).CompareTo(((CellIndices)(ref map.cellIndices)).CellToIndex(b)));
		int num2 = ((mode == SowRouteMode.Radius) ? RouteSelection.EffectiveAmount(amount) : 100);
		int num3 = 100 - Math.Min(list2.Count, 100);
		if (num2 > num3)
		{
			num2 = num3;
		}
		if (num2 < 0)
		{
			num2 = 0;
		}
		if (list3.Count > num2)
		{
			capped = true;
			list3.RemoveRange(num2, list3.Count - num2);
		}
		list.Capacity = list2.Count + list3.Count;
		list.AddRange(list2);
		list.AddRange(list3);
		return list;
	}

	public static bool IsSowableCell(Pawn pawn, IntVec3 c, Zone_Growing zone, ThingDef plantDef)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || plantDef?.plant == null || !((IntVec3)(ref c)).IsValid)
		{
			return false;
		}
		Map map = ((Thing)pawn).Map;
		if (!GenGrid.InBounds(c, map) || map.fogGrid.IsFogged(c))
		{
			return false;
		}
		Zone zone2 = GridsUtility.GetZone(c, map);
		if ((object)((zone2 is Zone_Growing) ? zone2 : null) != zone || !zone.allowSow)
		{
			return false;
		}
		if (ForbidUtility.IsForbidden(c, pawn))
		{
			return false;
		}
		if (!PlantUtility.GrowthSeasonNow(c, map, plantDef))
		{
			return false;
		}
		if (!PlantUtility.CanNowPlantAt(plantDef, c, map, false))
		{
			return false;
		}
		if (GridsUtility.GetPlant(c, map) != null)
		{
			return false;
		}
		List<Thing> thingList = GridsUtility.GetThingList(c, map);
		for (int i = 0; i < thingList.Count; i++)
		{
			Thing val = thingList[i];
			if (val.def == plantDef)
			{
				return false;
			}
			if (val.def.BlocksPlanting(false))
			{
				return false;
			}
		}
		if (PlantUtility.AdjacentSowBlocker(plantDef, c, map) != null)
		{
			return false;
		}
		return true;
	}
}
