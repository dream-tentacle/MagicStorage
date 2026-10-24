using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class SowRoutePlanner
{
	public static SowRoutePlan Plan(Pawn pawn, IntVec3 anchor, Zone_Growing zone, SowRouteMode mode, int amount, int radius, float maxDistance, bool smart, IReadOnlyList<IntVec3> mustInclude = null, RouteSelectionMethod selectionMethod = RouteSelectionMethod.MostStopsPerTravel, int exactMax = 12)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		SowRoutePlan sowRoutePlan = new SowRoutePlan();
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || zone == null || ((Zone)zone).Map != ((Thing)pawn).Map)
		{
			return sowRoutePlan;
		}
		ThingDef plantDef = (sowRoutePlan.plantDef = zone.GetPlantDefToGrow());
		bool capped;
		int mustIncludeCount;
		List<IntVec3> list = SowRouteSelection.Select(pawn, anchor, zone, mode, amount, radius, mustInclude, out capped, out mustIncludeCount);
		sowRoutePlan.cappedByAmount = capped;
		sowRoutePlan.selectedCount = list.Count;
		if (list.Count == 0)
		{
			return sowRoutePlan;
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
			return sowRoutePlan;
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
		sowRoutePlan.storageCell = (smart ? FindStorageCell(pawn, plantDef) : ((IntVec3?)null));
		List<IntVec3> list3 = OrderCells(pawn, list2, num, sowRoutePlan.storageCell, selectionMethod, exactMax);
		float travel;
		int num2 = ApplyBudget(((Thing)pawn).Position, list3, hashSet, maxDistance, out travel);
		sowRoutePlan.travelCells = travel;
		sowRoutePlan.cappedByDistance = num2 < list3.Count;
		for (int k = 0; k < num2; k++)
		{
			sowRoutePlan.cells.Add(list3[k]);
		}
		sowRoutePlan.totalTicks = EstimateTicks(pawn, sowRoutePlan, plantDef);
		return sowRoutePlan;
	}

	private static List<IntVec3> OrderCells(Pawn pawn, List<IntVec3> cells, int forcedCount, IntVec3? storage, RouteSelectionMethod selectionMethod, int exactMax)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
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
		bool hasValue = storage.HasValue;
		int storageX = (hasValue ? storage.Value.x : 0);
		int storageZ = (hasValue ? storage.Value.z : 0);
		int[] array3 = RouteOrderPolicy.OrderStops(((Thing)pawn).Position.x, ((Thing)pawn).Position.z, array, array2, hasValue, storageX, storageZ, -1, -1, exactMax);
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

	private static float EstimateTicks(Pawn pawn, SowRoutePlan plan, ThingDef plantDef)
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		if (plan.cells.Count == 0)
		{
			return 0f;
		}
		float statValue = StatExtension.GetStatValue((Thing)(object)pawn, StatDefOf.PlantWorkSpeed, true, -1);
		float item = RouteEstimate.SowWorkTicks((plantDef?.plant?.sowWork).GetValueOrDefault(), statValue);
		float num = TicksPerCell(pawn);
		List<float> list = new List<float>(plan.cells.Count);
		List<float> list2 = new List<float>(plan.cells.Count);
		IntVec3 val = ((Thing)pawn).Position;
		IntVec3 val2;
		for (int i = 0; i < plan.cells.Count; i++)
		{
			val2 = plan.cells[i] - val;
			list.Add(((IntVec3)(ref val2)).LengthHorizontal * num);
			list2.Add(item);
			val = plan.cells[i];
		}
		float returnLegTicks = 0f;
		if (plan.storageCell.HasValue)
		{
			val2 = plan.storageCell.Value - val;
			returnLegTicks = ((IntVec3)(ref val2)).LengthHorizontal * num;
		}
		return RouteEstimate.TotalTicks(list, list2, plan.cells.Count, returnLegTicks);
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

	internal static IntVec3? FindStorageCell(Pawn pawn, ThingDef plantDef)
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		ThingDef val = plantDef?.plant?.harvestedThingDef;
		if (val == null || ((BuildableDef)val).MadeFromStuff)
		{
			return null;
		}
		Thing val2 = ThingMaker.MakeThing(val, (ThingDef)null);
		if (val2 == null)
		{
			return null;
		}
		bool flag;
		IntVec3 value = default(IntVec3);
		using (StorageBuildingFilter.PushContext(StorageFilterContext.Unload))
		{
			flag = StoreUtility.TryFindBestBetterStoreCellFor(val2, pawn, ((Thing)pawn).Map, (StoragePriority)0, ((Thing)pawn).Faction, ref value, false);
		}
		if (!flag)
		{
			return null;
		}
		return value;
	}
}
