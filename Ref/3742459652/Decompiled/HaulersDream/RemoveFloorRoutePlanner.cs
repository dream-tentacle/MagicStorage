using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class RemoveFloorRoutePlanner
{
	private const float RemoveFloorWorkTicksProxy = 200f;

	public static RemoveFloorRoutePlan Plan(Pawn pawn, IntVec3 anchor, RemoveFloorRouteMode mode, int amount, int radius, float maxDistance, IReadOnlyList<IntVec3> mustInclude = null, RouteSelectionMethod selectionMethod = RouteSelectionMethod.MostStopsPerTravel, int exactMax = 12)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		RemoveFloorRoutePlan removeFloorRoutePlan = new RemoveFloorRoutePlan();
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || !((IntVec3)(ref anchor)).IsValid)
		{
			return removeFloorRoutePlan;
		}
		bool capped;
		int mustIncludeCount;
		List<IntVec3> list = RemoveFloorRouteSelection.Select(pawn, anchor, mode, amount, radius, mustInclude, out capped, out mustIncludeCount);
		removeFloorRoutePlan.cappedByAmount = capped;
		removeFloorRoutePlan.selectedCount = list.Count;
		if (list.Count == 0)
		{
			return removeFloorRoutePlan;
		}
		List<IntVec3> list2 = new List<IntVec3>(list.Count);
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			if (ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit(list[i]), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0))
			{
				list2.Add(list[i]);
				if (i < mustIncludeCount)
				{
					num++;
				}
			}
		}
		if (list2.Count == 0)
		{
			return removeFloorRoutePlan;
		}
		if (num < 1)
		{
			num = 1;
		}
		HashSet<IntVec3> hashSet = new HashSet<IntVec3>();
		for (int j = 0; j < num && j < list2.Count; j++)
		{
			hashSet.Add(list2[j]);
		}
		List<IntVec3> list3 = OrderCells(pawn, list2, num, selectionMethod, exactMax);
		float travel;
		int num2 = ApplyBudget(((Thing)pawn).Position, list3, hashSet, maxDistance, out travel);
		removeFloorRoutePlan.travelCells = travel;
		removeFloorRoutePlan.cappedByDistance = num2 < list3.Count;
		for (int k = 0; k < num2; k++)
		{
			removeFloorRoutePlan.cells.Add(list3[k]);
		}
		removeFloorRoutePlan.totalTicks = EstimateTicks(pawn, removeFloorRoutePlan);
		return removeFloorRoutePlan;
	}

	private static List<IntVec3> OrderCells(Pawn pawn, List<IntVec3> cells, int forcedCount, RouteSelectionMethod selectionMethod, int exactMax)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		int count = cells.Count;
		if (count <= 1)
		{
			return new List<IntVec3>(cells);
		}
		if (selectionMethod == RouteSelectionMethod.NearestToTarget)
		{
			List<IntVec3> list = new List<IntVec3>(count);
			Map map = ((Thing)pawn).Map;
			IntVec3 anchor = cells[0];
			for (int i = 0; i < forcedCount && i < count; i++)
			{
				list.Add(cells[i]);
			}
			List<IntVec3> list2 = new List<IntVec3>(count);
			for (int j = forcedCount; j < count; j++)
			{
				list2.Add(cells[j]);
			}
			list2.Sort(delegate(IntVec3 a, IntVec3 b)
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0016: Unknown result type (might be due to invalid IL or missing references)
				//IL_0018: Unknown result type (might be due to invalid IL or missing references)
				//IL_001d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				//IL_0045: Unknown result type (might be due to invalid IL or missing references)
				//IL_005a: Unknown result type (might be due to invalid IL or missing references)
				IntVec3 val = a - anchor;
				long num3 = ((IntVec3)(ref val)).LengthHorizontalSquared;
				val = b - anchor;
				long value = ((IntVec3)(ref val)).LengthHorizontalSquared;
				int num4 = num3.CompareTo(value);
				return (num4 != 0) ? num4 : ((CellIndices)(ref map.cellIndices)).CellToIndex(a).CompareTo(((CellIndices)(ref map.cellIndices)).CellToIndex(b));
			});
			list.AddRange(list2);
			return list;
		}
		int[] array = new int[count];
		int[] array2 = new int[count];
		for (int num = 0; num < count; num++)
		{
			array[num] = cells[num].x;
			array2[num] = cells[num].z;
		}
		int[] array3 = RouteOrderPolicy.OrderStops(((Thing)pawn).Position.x, ((Thing)pawn).Position.z, array, array2, hasStorage: false, 0, 0, -1, -1, exactMax);
		List<IntVec3> list3 = new List<IntVec3>(count);
		for (int num2 = 0; num2 < array3.Length; num2++)
		{
			list3.Add(cells[array3[num2]]);
		}
		return list3;
	}

	private static int ApplyBudget(IntVec3 pawnPos, List<IntVec3> ordered, HashSet<IntVec3> forcedCells, float maxDistance, out float travel)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		travel = 0f;
		if (ordered.Count == 0)
		{
			return 0;
		}
		bool flag = maxDistance <= 0f || float.IsPositiveInfinity(maxDistance);
		IntVec3 val = pawnPos;
		int num = 0;
		IntVec3 val2;
		for (int i = 0; i < ordered.Count; i++)
		{
			val2 = ordered[i] - val;
			float lengthHorizontal = ((IntVec3)(ref val2)).LengthHorizontal;
			float num2 = travel + lengthHorizontal;
			bool flag2 = forcedCells?.Contains(ordered[i]) ?? false;
			if (!flag && !flag2 && i > 0 && num2 > maxDistance)
			{
				break;
			}
			travel = num2;
			val = ordered[i];
			num = i + 1;
		}
		if (num < 1)
		{
			num = 1;
		}
		travel = 0f;
		val = pawnPos;
		for (int j = 0; j < num; j++)
		{
			float num3 = travel;
			val2 = ordered[j] - val;
			travel = num3 + ((IntVec3)(ref val2)).LengthHorizontal;
			val = ordered[j];
		}
		return num;
	}

	private static float EstimateTicks(Pawn pawn, RemoveFloorRoutePlan plan)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		if (plan.cells.Count == 0)
		{
			return 0f;
		}
		float num = TicksPerCell(pawn);
		List<float> list = new List<float>(plan.cells.Count);
		List<float> list2 = new List<float>(plan.cells.Count);
		IntVec3 val = ((Thing)pawn).Position;
		for (int i = 0; i < plan.cells.Count; i++)
		{
			IntVec3 val2 = plan.cells[i] - val;
			list.Add(((IntVec3)(ref val2)).LengthHorizontal * num);
			list2.Add(200f);
			val = plan.cells[i];
		}
		return RouteEstimate.TotalTicks(list, list2, plan.cells.Count, 0f);
	}

	private static float TicksPerCell(Pawn pawn)
	{
		float num = StatExtension.GetStatValue((Thing)(object)pawn, StatDefOf.MoveSpeed, true, -1);
		if (num <= 0.01f)
		{
			num = 1f;
		}
		return 60f / num;
	}
}
