using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class RemoveFloorRouteSelection
{
	private const int ChainedWindow = 60;

	private const int FloodSlack = 50;

	public static List<IntVec3> Select(Pawn pawn, IntVec3 anchor, RemoveFloorRouteMode mode, int amount, int radius, IReadOnlyList<IntVec3> mustInclude, out bool capped, out int mustIncludeCount)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		capped = false;
		mustIncludeCount = 0;
		List<IntVec3> list = new List<IntVec3>();
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null)
		{
			return list;
		}
		Map map = ((Thing)pawn).Map;
		List<IntVec3> list2 = new List<IntVec3>();
		HashSet<IntVec3> hashSet = new HashSet<IntVec3>();
		if (IsRemovableFloorCell(pawn, anchor) && hashSet.Add(anchor))
		{
			list2.Add(anchor);
		}
		if (mustInclude != null)
		{
			for (int i = 0; i < mustInclude.Count; i++)
			{
				IntVec3 val = mustInclude[i];
				if (!hashSet.Contains(val) && IsRemovableFloorCell(pawn, val) && hashSet.Add(val))
				{
					list2.Add(val);
				}
			}
		}
		mustIncludeCount = list2.Count;
		List<IntVec3> list3 = BuildAuto(pawn, anchor, mode, radius, hashSet);
		list3.Sort((IntVec3 a, IntVec3 b) => ((CellIndices)(ref map.cellIndices)).CellToIndex(a).CompareTo(((CellIndices)(ref map.cellIndices)).CellToIndex(b)));
		int num = ((mode == RemoveFloorRouteMode.Radius) ? RouteSelection.EffectiveAmount(amount) : 100);
		int num2 = 100 - Math.Min(list2.Count, 100);
		if (num > num2)
		{
			num = num2;
		}
		if (num < 0)
		{
			num = 0;
		}
		if (list3.Count > num)
		{
			capped = true;
			list3.RemoveRange(num, list3.Count - num);
		}
		list.Capacity = list2.Count + list3.Count;
		list.AddRange(list2);
		list.AddRange(list3);
		return list;
	}

	private static List<IntVec3> BuildAuto(Pawn pawn, IntVec3 anchor, RemoveFloorRouteMode mode, int radius, HashSet<IntVec3> forcedSet)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		return mode switch
		{
			RemoveFloorRouteMode.Area => SelectArea(pawn, anchor, forcedSet), 
			RemoveFloorRouteMode.Radius => SelectRadius(pawn, anchor, radius, forcedSet), 
			_ => SelectChained(pawn, anchor, forcedSet), 
		};
	}

	private static List<IntVec3> SelectArea(Pawn pawn, IntVec3 anchor, HashSet<IntVec3> forcedSet)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		List<IntVec3> list = new List<IntVec3>();
		_ = ((Thing)pawn).Map;
		if (!IsRemovableFloorCell(pawn, anchor))
		{
			return list;
		}
		int num = 150;
		HashSet<IntVec3> hashSet = new HashSet<IntVec3> { anchor };
		Queue<IntVec3> val = new Queue<IntVec3>();
		val.Enqueue(anchor);
		IntVec3[] adjacentCells = GenAdj.AdjacentCells;
		while (val.Count > 0 && hashSet.Count <= num)
		{
			IntVec3 val2 = val.Dequeue();
			if (!forcedSet.Contains(val2))
			{
				list.Add(val2);
			}
			for (int i = 0; i < adjacentCells.Length; i++)
			{
				IntVec3 val3 = val2 + adjacentCells[i];
				if (!hashSet.Contains(val3))
				{
					hashSet.Add(val3);
					if (IsRemovableFloorCell(pawn, val3))
					{
						val.Enqueue(val3);
					}
				}
			}
		}
		return list;
	}

	private unsafe static List<IntVec3> SelectChained(Pawn pawn, IntVec3 anchor, HashSet<IntVec3> forcedSet)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		List<IntVec3> list = new List<IntVec3>();
		Map map = ((Thing)pawn).Map;
		CellRect val = CellRect.CenteredOn(anchor, 60);
		CellRect val2 = ((CellRect)(ref val)).ClipInsideMap(map);
		Enumerator enumerator = ((CellRect)(ref val2)).GetEnumerator();
		try
		{
			while (((Enumerator)(ref enumerator)).MoveNext())
			{
				IntVec3 current = ((Enumerator)(ref enumerator)).Current;
				if (!forcedSet.Contains(current) && IsRemovableFloorCell(pawn, current))
				{
					list.Add(current);
				}
			}
			return list;
		}
		finally
		{
			((IDisposable)(*(Enumerator*)(&enumerator))/*cast due to constrained. prefix*/).Dispose();
		}
	}

	private unsafe static List<IntVec3> SelectRadius(Pawn pawn, IntVec3 anchor, int radius, HashSet<IntVec3> forcedSet)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		List<IntVec3> list = new List<IntVec3>();
		Map map = ((Thing)pawn).Map;
		float num = ((radius <= 0) ? 1 : radius);
		int num2 = ((radius <= 0) ? 1 : radius);
		CellRect val = CellRect.CenteredOn(anchor, num2);
		CellRect val2 = ((CellRect)(ref val)).ClipInsideMap(map);
		Enumerator enumerator = ((CellRect)(ref val2)).GetEnumerator();
		try
		{
			while (((Enumerator)(ref enumerator)).MoveNext())
			{
				IntVec3 current = ((Enumerator)(ref enumerator)).Current;
				if (!forcedSet.Contains(current))
				{
					IntVec3 val3 = current - anchor;
					if (!(((IntVec3)(ref val3)).LengthHorizontal > num) && IsRemovableFloorCell(pawn, current))
					{
						list.Add(current);
					}
				}
			}
			return list;
		}
		finally
		{
			((IDisposable)(*(Enumerator*)(&enumerator))/*cast due to constrained. prefix*/).Dispose();
		}
	}

	public static bool IsRemovableFloorCell(Pawn pawn, IntVec3 c)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Invalid comparison between Unknown and I4
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Invalid comparison between Unknown and I4
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || !((IntVec3)(ref c)).IsValid)
		{
			return false;
		}
		Map map = ((Thing)pawn).Map;
		if (!GenGrid.InBounds(c, map) || GridsUtility.Fogged(c, map))
		{
			return false;
		}
		if (map.designationManager.DesignationAt(c, DesignationDefOf.RemoveFloor) == null)
		{
			return false;
		}
		if (!map.terrainGrid.CanRemoveTopLayerAt(c))
		{
			return false;
		}
		Building edifice = GridsUtility.GetEdifice(c, map);
		if (edifice != null && (int)((Thing)edifice).def.Fillage == 2 && (int)((BuildableDef)((Thing)edifice).def).passability == 2)
		{
			return false;
		}
		if (WorkGiver_ConstructRemoveFloor.AnyBuildingBlockingFloorRemoval(c, map))
		{
			return false;
		}
		if (ForbidUtility.IsForbidden(c, pawn))
		{
			return false;
		}
		return true;
	}
}
