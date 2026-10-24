using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class RoutePlanner
{
	public const int WalkingPathStopCap = 20;

	public static RoutePlan Plan(Pawn pawn, Thing clicked, RouteWorkKind kind, RouteMode mode, int amount, int radius, float maxDistance, bool smart, bool allowHarvest, int growthThreshold, IReadOnlyList<Thing> mustInclude = null, RouteSelectionMethod selectionMethod = RouteSelectionMethod.MostStopsPerTravel, RouteDistanceBasis distanceBasis = RouteDistanceBasis.StraightLine, int exactMax = 12, Thing startNode = null, Thing endNode = null, IReadOnlyList<IntVec3> roomAnchors = null, IReadOnlyList<ThingDef> extraDefs = null)
	{
		return Truncate(ComputeLegs(pawn, clicked, kind, mode, amount, radius, smart, allowHarvest, growthThreshold, mustInclude, selectionMethod, distanceBasis, roomAnchors, extraDefs), maxDistance, startNode, endNode, exactMax);
	}

	public static RouteLegs ComputeLegs(Pawn pawn, Thing clicked, RouteWorkKind kind, RouteMode mode, int amount, int radius, bool smart, bool allowHarvest, int growthThreshold, IReadOnlyList<Thing> mustInclude = null, RouteSelectionMethod selectionMethod = RouteSelectionMethod.MostStopsPerTravel, RouteDistanceBasis distanceBasis = RouteDistanceBasis.StraightLine, IReadOnlyList<IntVec3> roomAnchors = null, IReadOnlyList<ThingDef> extraDefs = null)
	{
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		RouteLegs routeLegs = new RouteLegs
		{
			pawn = pawn,
			kind = kind
		};
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || clicked == null || kind == null)
		{
			return routeLegs;
		}
		bool capped;
		bool fogCaution;
		int mustIncludeCount;
		List<Thing> list = RouteSelection.Select(pawn, clicked, kind, mode, amount, radius, allowHarvest, growthThreshold, mustInclude, out capped, out fogCaution, out mustIncludeCount, roomAnchors, extraDefs);
		routeLegs.cappedByAmount = capped;
		routeLegs.fogCaution = fogCaution;
		routeLegs.selectedCount = list.Count;
		if (list.Count == 0)
		{
			return routeLegs;
		}
		routeLegs.storage = (smart ? FindStorageCell(pawn, kind) : ((IntVec3?)null));
		List<Thing> list2 = new List<Thing>(list.Count);
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
			return routeLegs;
		}
		routeLegs.forcedCount = ((num < 1) ? 1 : num);
		int count = list2.Count;
		bool walking = distanceBasis == RouteDistanceBasis.WalkingPath && count <= 20;
		float[,] dist = BuildDistMatrix(pawn, list2, walking, routeLegs.storage);
		int endIdx = (routeLegs.storage.HasValue ? (count + 1) : (-1));
		int[] order;
		float[] cost;
		if (selectionMethod == RouteSelectionMethod.NearestToTarget)
		{
			order = NearestOrder(dist, routeLegs.forcedCount, count);
			RouteBudget.PrefixRouteCostMatrixWithEnd(dist, order, routeLegs.forcedCount, endIdx, out cost);
		}
		else
		{
			RouteBudget.GreedyPlanMatrixWithEnd(dist, routeLegs.forcedCount, endIdx, out order, out cost);
		}
		routeLegs.prefixRouteCost = cost;
		IntVec3 position = ((Thing)pawn).Position;
		for (int j = 0; j < order.Length; j++)
		{
			Thing val = list2[order[j]];
			TryLeg(pawn, position, LocalTargetInfo.op_Implicit(val), (PathEndMode)2, out var _, out var ticks);
			routeLegs.gatherOrdered.Add(val);
			routeLegs.legWalkTicks.Add(ticks);
			position = val.Position;
		}
		return routeLegs;
	}

	public static RoutePlan Truncate(RouteLegs legs, float maxDistance, Thing startNode = null, Thing endNode = null, int exactMax = 12)
	{
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		RoutePlan routePlan = new RoutePlan();
		if (legs == null || legs.Reachable == 0)
		{
			return routePlan;
		}
		routePlan.selectedCount = legs.selectedCount;
		routePlan.cappedByAmount = legs.cappedByAmount;
		routePlan.fogCaution = legs.fogCaution;
		routePlan.storageCell = legs.storage;
		int reachable = legs.Reachable;
		int num = RouteBudget.LargestPrefixWithin(legs.prefixRouteCost, maxDistance, legs.forcedCount);
		if (num < 1)
		{
			num = 1;
		}
		if (num > reachable)
		{
			num = reachable;
		}
		routePlan.cappedByDistance = num < reachable;
		routePlan.cappedByReach = reachable < legs.selectedCount;
		routePlan.travelCells = ((legs.prefixRouteCost != null && num >= 1 && num <= legs.prefixRouteCost.Length) ? legs.prefixRouteCost[num - 1] : 0f);
		List<Thing> list = new List<Thing>(num);
		for (int i = 0; i < num; i++)
		{
			list.Add(legs.gatherOrdered[i]);
		}
		List<Thing> collection = ((list.Count > 1) ? OrderByEuclidean(((Thing)legs.pawn).Position, list, legs.storage, startNode, endNode, exactMax) : list);
		routePlan.stops.AddRange(collection);
		List<float> list2 = new List<float>(num);
		for (int j = 0; j < num; j++)
		{
			list2.Add(WorkTicks(legs.pawn, routePlan.stops[j], legs.kind));
		}
		float returnLegTicks = 0f;
		if (legs.storage.HasValue && routePlan.stops.Count > 0 && TryLeg(legs.pawn, routePlan.stops[routePlan.stops.Count - 1].Position, LocalTargetInfo.op_Implicit(legs.storage.Value), (PathEndMode)1, out var _, out var ticks))
		{
			returnLegTicks = ticks;
		}
		routePlan.totalTicks = RouteEstimate.TotalTicks(legs.legWalkTicks, list2, num, returnLegTicks);
		return routePlan;
	}

	private static float[,] BuildDistMatrix(Pawn pawn, List<Thing> stops, bool walking, IntVec3? storage)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		int count = stops.Count;
		int num = count + 1 + (storage.HasValue ? 1 : 0);
		IntVec3[] array = (IntVec3[])(object)new IntVec3[num];
		array[0] = ((Thing)pawn).Position;
		for (int i = 0; i < count; i++)
		{
			array[i + 1] = stops[i].Position;
		}
		if (storage.HasValue)
		{
			array[count + 1] = storage.Value;
		}
		float[,] array2 = new float[num, num];
		for (int j = 0; j < num; j++)
		{
			for (int k = j + 1; k < num; k++)
			{
				IntVec3 val = array[j] - array[k];
				float num2 = ((IntVec3)(ref val)).LengthHorizontal;
				if (walking && TryLeg(pawn, array[j], LocalTargetInfo.op_Implicit(array[k]), (PathEndMode)2, out var cells, out var _))
				{
					num2 = cells;
				}
				array2[j, k] = num2;
				array2[k, j] = num2;
			}
		}
		return array2;
	}

	private static int[] NearestOrder(float[,] dist, int forcedCount, int n)
	{
		int[] array = new int[n];
		for (int i = 0; i < forcedCount && i < n; i++)
		{
			array[i] = i;
		}
		List<int> list = new List<int>(n);
		for (int j = forcedCount; j < n; j++)
		{
			list.Add(j);
		}
		list.Sort(delegate(int a, int b)
		{
			int num2 = dist[1, a + 1].CompareTo(dist[1, b + 1]);
			return (num2 == 0) ? a.CompareTo(b) : num2;
		});
		for (int num = 0; num < list.Count; num++)
		{
			array[forcedCount + num] = list[num];
		}
		return array;
	}

	private static List<Thing> OrderByEuclidean(IntVec3 pawnPos, List<Thing> things, IntVec3? storage, Thing startNode, Thing endNode, int exactMax)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		int count = things.Count;
		if (count <= 1)
		{
			return new List<Thing>(things);
		}
		int[] array = new int[count];
		int[] array2 = new int[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = things[i].Position.x;
			array2[i] = things[i].Position.z;
		}
		int startStop = ((startNode != null) ? things.IndexOf(startNode) : (-1));
		int endStop = ((endNode != null && endNode != startNode) ? things.IndexOf(endNode) : (-1));
		bool hasValue = storage.HasValue;
		int storageX = (hasValue ? storage.Value.x : 0);
		int storageZ = (hasValue ? storage.Value.z : 0);
		int[] array3 = RouteOrderPolicy.OrderStops(pawnPos.x, pawnPos.z, array, array2, hasValue, storageX, storageZ, startStop, endStop, exactMax);
		List<Thing> list = new List<Thing>(count);
		for (int j = 0; j < array3.Length; j++)
		{
			list.Add(things[array3[j]]);
		}
		return list;
	}

	private static bool TryLeg(Pawn pawn, IntVec3 from, LocalTargetInfo target, PathEndMode peMode, out float cells, out float ticks)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		cells = 0f;
		ticks = 0f;
		PawnPath val = null;
		try
		{
			val = ((Thing)pawn).Map.pathFinder.FindPathNow(from, target, pawn, (PathFinderCostTuning?)null, peMode);
			if (val == null || !val.Found)
			{
				return false;
			}
			cells = val.NodesReversed.Count;
			ticks = val.TotalCost;
			return true;
		}
		finally
		{
			if (val != null)
			{
				val.ReleaseToPool();
			}
		}
	}

	private static float WorkTicks(Pawn pawn, Thing t, RouteWorkKind kind)
	{
		Plant val = (Plant)(object)((t is Plant) ? t : null);
		if (val != null)
		{
			return RouteEstimate.PlantWorkTicks(((Thing)val).def.plant?.harvestWork ?? 0f, StatExtension.GetStatValue((Thing)(object)pawn, StatDefOf.PlantWorkSpeed, true, -1), val.Growth);
		}
		if (t.def.mineable)
		{
			bool isNaturalRock = t.def.building?.isNaturalRock ?? false;
			return RouteEstimate.MineWorkTicks(t.HitPoints, isNaturalRock, StatExtension.GetStatValue((Thing)(object)pawn, StatDefOf.MiningSpeed, true, -1));
		}
		Blueprint val2 = (Blueprint)(object)((t is Blueprint) ? t : null);
		if (val2 != null)
		{
			BuildableDef val3 = val2.EntityToBuild();
			if (val3 != null)
			{
				float statValueAbstract = StatExtension.GetStatValueAbstract(val3, StatDefOf.WorkToBuild, val2.EntityToBuildStuff());
				float statValue = StatExtension.GetStatValue((Thing)(object)pawn, StatDefOf.ConstructionSpeed, true, -1);
				if (statValue > 0f && statValueAbstract > 0f)
				{
					return statValueAbstract / statValue;
				}
			}
		}
		return 0f;
	}

	internal static IntVec3? FindStorageCell(Pawn pawn, RouteWorkKind kind)
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		ThingDef yieldDef = kind.yieldDef;
		if (yieldDef == null || ((BuildableDef)yieldDef).MadeFromStuff)
		{
			return null;
		}
		Thing val = ThingMaker.MakeThing(yieldDef, (ThingDef)null);
		if (val == null)
		{
			return null;
		}
		bool flag;
		IntVec3 value = default(IntVec3);
		using (StorageBuildingFilter.PushContext(StorageFilterContext.Unload))
		{
			flag = StoreUtility.TryFindBestBetterStoreCellFor(val, pawn, ((Thing)pawn).Map, (StoragePriority)0, ((Thing)pawn).Faction, ref value, false);
		}
		if (!flag)
		{
			return null;
		}
		return value;
	}
}
