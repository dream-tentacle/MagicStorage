using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class RouteSelection
{
	private struct ByDistanceToComparer(IntVec3 anchor) : IComparer<Thing>
	{
		private readonly IntVec3 anchor = anchor;

		public int Compare(Thing a, Thing b)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 val = a.Position - anchor;
			long aDistSq = ((IntVec3)(ref val)).LengthHorizontalSquared;
			val = b.Position - anchor;
			long bDistSq = ((IntVec3)(ref val)).LengthHorizontalSquared;
			return RouteOrdering.CompareMarkedFirst(aMarked: false, aDistSq, a.thingIDNumber, bMarked: false, bDistSq, b.thingIDNumber);
		}
	}

	public const int HardCap = 100;

	public const int AllAmount = int.MaxValue;

	private const int VeinWindow = 60;

	[ThreadStatic]
	private static Pawn claimedCachePawn;

	[ThreadStatic]
	private static int claimedCacheTick;

	[ThreadStatic]
	private static HashSet<Thing> claimedCacheSet;

	private static readonly HashSet<Thing> EmptyClaimSet;

	public static List<RouteMode> AllowedModes(RouteWorkKind kind, Thing clicked, Map map)
	{
		List<RouteMode> list = new List<RouteMode>(4);
		switch (kind?.scope ?? RouteTargetScope.SameDef)
		{
		case RouteTargetScope.AnyFilth:
			list.Add(RouteMode.Rooms);
			list.Add(RouteMode.Radius);
			return list;
		case RouteTargetScope.DesignatedOnly:
		case RouteTargetScope.Constructible:
			list.Add(RouteMode.Chained);
			list.Add(RouteMode.Radius);
			return list;
		default:
			list.Add(RouteMode.Chained);
			list.Add(RouteMode.Vein);
			list.Add(RouteMode.Radius);
			if (GrowingZoneFor(clicked, map) != null)
			{
				list.Add(RouteMode.Zone);
			}
			return list;
		}
	}

	public static Zone_Growing GrowingZoneFor(Thing clicked, Map map)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || !(clicked is Plant) || !clicked.Spawned)
		{
			return null;
		}
		Zone obj = map.zoneManager.ZoneAt(clicked.Position);
		return (Zone_Growing)(object)((obj is Zone_Growing) ? obj : null);
	}

	public static List<Thing> Select(Pawn pawn, Thing clicked, RouteWorkKind kind, RouteMode mode, int amount, int radius, bool allowHarvest, int growthThreshold, IReadOnlyList<Thing> mustInclude, out bool capped, out bool fogCaution, out int mustIncludeCount, IReadOnlyList<IntVec3> roomAnchors = null, IReadOnlyList<ThingDef> extraDefs = null)
	{
		capped = false;
		fogCaution = false;
		mustIncludeCount = 0;
		List<Thing> list = new List<Thing>();
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || clicked == null || !clicked.Spawned || kind == null)
		{
			return list;
		}
		List<RouteMode> list2 = AllowedModes(kind, clicked, ((Thing)pawn).Map);
		if (!list2.Contains(mode))
		{
			mode = list2[0];
		}
		List<Thing> list3 = BuildForced(pawn, clicked, kind, mustInclude, extraDefs);
		mustIncludeCount = list3.Count;
		HashSet<Thing> forcedSet = new HashSet<Thing>(list3);
		List<Thing> list4 = mode switch
		{
			RouteMode.Radius => SelectRadius(pawn, clicked, kind, allowHarvest, growthThreshold, radius, extraDefs), 
			RouteMode.Chained => SelectChained(pawn, clicked, kind, allowHarvest, growthThreshold, extraDefs), 
			RouteMode.Vein => SelectVein(pawn, clicked, kind, allowHarvest, growthThreshold, out fogCaution), 
			RouteMode.Rooms => SelectRooms(pawn, clicked, kind, allowHarvest, growthThreshold, roomAnchors, extraDefs), 
			RouteMode.Zone => SelectZone(pawn, clicked, kind, allowHarvest, growthThreshold, extraDefs), 
			_ => new List<Thing>(), 
		};
		list4.RemoveAll((Thing t) => forcedSet.Contains(t));
		HashSet<Thing> claimedByOthers = ClaimedByOtherPawns(pawn);
		if (claimedByOthers.Count > 0)
		{
			list4.RemoveAll((Thing t) => claimedByOthers.Contains(t));
		}
		int num = ((mode == RouteMode.Chained) ? 100 : EffectiveAmount(amount));
		int num2 = 100 - Math.Min(list3.Count, 100);
		if (num > num2)
		{
			num = num2;
		}
		if (num < 0)
		{
			num = 0;
		}
		if (list4.Count > num)
		{
			capped = true;
			list4.RemoveRange(num, list4.Count - num);
		}
		list.Capacity = list3.Count + list4.Count;
		list.AddRange(list3);
		list.AddRange(list4);
		return list;
	}

	private static List<Thing> BuildForced(Pawn pawn, Thing clicked, RouteWorkKind kind, IReadOnlyList<Thing> mustInclude, IReadOnlyList<ThingDef> extraDefs = null)
	{
		List<Thing> list = new List<Thing>();
		HashSet<Thing> hashSet = new HashSet<Thing>();
		if (clicked != null && clicked.Spawned)
		{
			list.Add(clicked);
			hashSet.Add(clicked);
		}
		if (mustInclude != null)
		{
			for (int i = 0; i < mustInclude.Count; i++)
			{
				Thing val = mustInclude[i];
				if (val != null && !hashSet.Contains(val) && IsValidRouteTarget(pawn, clicked, kind, val, extraDefs))
				{
					list.Add(val);
					hashSet.Add(val);
				}
			}
		}
		return list;
	}

	public static bool IsValidRouteTarget(Pawn pawn, Thing clicked, RouteWorkKind kind, Thing t, IReadOnlyList<ThingDef> extraDefs = null)
	{
		if (t == null || !t.Spawned || clicked == null)
		{
			return false;
		}
		if (!MatchesScope(((pawn != null) ? ((Thing)pawn).Map : null) ?? clicked.Map, clicked, kind, t, extraDefs))
		{
			return false;
		}
		if (pawn != null && ForbidUtility.IsForbidden(t, pawn))
		{
			return false;
		}
		if (kind?.designation == DesignationDefOf.HarvestPlant)
		{
			Plant val = (Plant)(object)((t is Plant) ? t : null);
			if (val != null && !val.HarvestableNow)
			{
				return false;
			}
		}
		if (pawn != null && IsClaimedByOtherPawn(pawn, t))
		{
			return false;
		}
		return true;
	}

	private static bool DefAccepted(ThingDef def, Thing clicked, IReadOnlyList<ThingDef> extraDefs)
	{
		if (def == clicked.def)
		{
			return true;
		}
		if (extraDefs != null)
		{
			for (int i = 0; i < extraDefs.Count; i++)
			{
				if (extraDefs[i] == def)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool MatchesScope(Map map, Thing clicked, RouteWorkKind kind, Thing t, IReadOnlyList<ThingDef> extraDefs = null)
	{
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		switch (kind?.scope ?? RouteTargetScope.SameDef)
		{
		case RouteTargetScope.AnyFilth:
			return t is Filth;
		case RouteTargetScope.Constructible:
			if (t is Blueprint || t is Frame)
			{
				return t.Faction == clicked.Faction;
			}
			return false;
		case RouteTargetScope.DesignatedOnly:
			if (kind.designation != null && map != null)
			{
				return map.designationManager.DesignationOn(t, kind.designation) != null;
			}
			return false;
		case RouteTargetScope.SameDefOrDesignated:
			if (DefAccepted(t.def, clicked, extraDefs))
			{
				return true;
			}
			if (kind.designation == null || map == null)
			{
				return false;
			}
			if (kind.designation == DesignationDefOf.Mine)
			{
				if (map.designationManager.DesignationAt(t.Position, DesignationDefOf.Mine) == null)
				{
					return map.designationManager.DesignationAt(t.Position, DesignationDefOf.MineVein) != null;
				}
				return true;
			}
			return map.designationManager.DesignationOn(t, kind.designation) != null;
		default:
			return DefAccepted(t.def, clicked, extraDefs);
		}
	}

	static RouteSelection()
	{
		EmptyClaimSet = new HashSet<Thing>();
		CacheRegistry.Register(ClearClaimedCache);
	}

	internal static void ClearClaimedCache()
	{
		claimedCachePawn = null;
		claimedCacheSet = null;
		claimedCacheTick = -1;
	}

	internal static HashSet<Thing> ClaimedByOtherPawns(Pawn pawn)
	{
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || ((Thing)pawn).Faction == null)
		{
			return EmptyClaimSet;
		}
		TickManager tickManager = Find.TickManager;
		bool flag = tickManager != null && tickManager.Paused;
		TickManager tickManager2 = Find.TickManager;
		int num = ((tickManager2 != null) ? tickManager2.TicksGame : (-1));
		if (!flag && claimedCacheSet != null && claimedCachePawn == pawn && claimedCacheTick == num)
		{
			return claimedCacheSet;
		}
		HashSet<Thing> hashSet = new HashSet<Thing>();
		List<Pawn> list = ((Thing)pawn).Map.mapPawns.SpawnedPawnsInFaction(((Thing)pawn).Faction);
		for (int i = 0; i < list.Count; i++)
		{
			Pawn val = list[i];
			if (val == pawn || val.jobs == null || (val.RaceProps != null && val.RaceProps.Animal))
			{
				continue;
			}
			CollectJobTargets(hashSet, val.CurJob);
			JobQueue jobQueue = val.jobs.jobQueue;
			if (jobQueue != null)
			{
				for (int j = 0; j < jobQueue.Count; j++)
				{
					CollectJobTargets(hashSet, jobQueue[j]?.job);
				}
			}
		}
		if (!flag)
		{
			claimedCachePawn = pawn;
			claimedCacheTick = num;
			claimedCacheSet = hashSet;
		}
		return hashSet;
	}

	private static bool IsClaimedByOtherPawn(Pawn pawn, Thing t)
	{
		if (t != null)
		{
			return ClaimedByOtherPawns(pawn).Contains(t);
		}
		return false;
	}

	private static void CollectJobTargets(HashSet<Thing> set, Job job)
	{
		if (job != null)
		{
			if (((LocalTargetInfo)(ref job.targetA)).Thing != null)
			{
				set.Add(((LocalTargetInfo)(ref job.targetA)).Thing);
			}
			if (((LocalTargetInfo)(ref job.targetB)).Thing != null)
			{
				set.Add(((LocalTargetInfo)(ref job.targetB)).Thing);
			}
			if (((LocalTargetInfo)(ref job.targetC)).Thing != null)
			{
				set.Add(((LocalTargetInfo)(ref job.targetC)).Thing);
			}
			AddQueueTargets(set, job.targetQueueA);
			AddQueueTargets(set, job.targetQueueB);
		}
	}

	private static void AddQueueTargets(HashSet<Thing> set, List<LocalTargetInfo> q)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (q == null)
		{
			return;
		}
		for (int i = 0; i < q.Count; i++)
		{
			LocalTargetInfo val = q[i];
			if (((LocalTargetInfo)(ref val)).Thing != null)
			{
				val = q[i];
				set.Add(((LocalTargetInfo)(ref val)).Thing);
			}
		}
	}

	public static int EffectiveAmount(int amount)
	{
		if (amount != int.MaxValue && amount > 0)
		{
			return Math.Min(amount, 100);
		}
		return 100;
	}

	private static List<Thing> SelectRadius(Pawn pawn, Thing clicked, RouteWorkKind kind, bool allowHarvest, int threshold, int radius, IReadOnlyList<ThingDef> extraDefs = null)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		float num = ((radius <= 0) ? 1 : radius);
		List<Thing> list = new List<Thing>();
		foreach (Thing item in SameKind(pawn, clicked, kind, allowHarvest, threshold, extraDefs))
		{
			IntVec3 val = item.Position - clicked.Position;
			if (((IntVec3)(ref val)).LengthHorizontal <= num)
			{
				list.Add(item);
			}
		}
		SortByDistanceTo(list, clicked.Position);
		EnsureAnchorFirst(list, clicked);
		return list;
	}

	private static List<Thing> SelectChained(Pawn pawn, Thing clicked, RouteWorkKind kind, bool allowHarvest, int threshold, IReadOnlyList<ThingDef> extraDefs = null)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		List<Thing> list = new List<Thing>(SameKind(pawn, clicked, kind, allowHarvest, threshold, extraDefs));
		SortByDistanceTo(list, clicked.Position);
		EnsureAnchorFirst(list, clicked);
		return list;
	}

	private static List<Thing> SelectVein(Pawn pawn, Thing clicked, RouteWorkKind kind, bool allowHarvest, int threshold, out bool fogCaution)
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		List<Thing> list = FloodVisibleVein(((Thing)pawn).Map, pawn, clicked.def, clicked.Position, 101, (Thing t) => t == clicked || IncludeForHarvest(((Thing)pawn).Map, t, kind, allowHarvest, threshold), out fogCaution);
		EnsureAnchorFirst(list, clicked);
		return list;
	}

	private static List<Thing> SelectRooms(Pawn pawn, Thing clicked, RouteWorkKind kind, bool allowHarvest, int threshold, IReadOnlyList<IntVec3> roomAnchors, IReadOnlyList<ThingDef> extraDefs = null)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		Map map = ((Thing)pawn).Map;
		HashSet<int> hashSet = new HashSet<int>();
		Room room = GridsUtility.GetRoom(clicked.Position, map);
		if (room != null)
		{
			hashSet.Add(room.ID);
		}
		if (roomAnchors != null)
		{
			for (int i = 0; i < roomAnchors.Count; i++)
			{
				IntVec3 val = roomAnchors[i];
				if (((IntVec3)(ref val)).IsValid && GenGrid.InBounds(val, map))
				{
					Room room2 = GridsUtility.GetRoom(val, map);
					if (room2 != null)
					{
						hashSet.Add(room2.ID);
					}
				}
			}
		}
		List<Thing> list = new List<Thing>();
		if (hashSet.Count == 0)
		{
			return list;
		}
		foreach (Thing item in SameKind(pawn, clicked, kind, allowHarvest, threshold, extraDefs))
		{
			Room room3 = GridsUtility.GetRoom(item.Position, map);
			if (room3 != null && hashSet.Contains(room3.ID))
			{
				list.Add(item);
			}
		}
		SortByDistanceTo(list, clicked.Position);
		EnsureAnchorFirst(list, clicked);
		return list;
	}

	private static List<Thing> SelectZone(Pawn pawn, Thing clicked, RouteWorkKind kind, bool allowHarvest, int threshold, IReadOnlyList<ThingDef> extraDefs = null)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		List<Thing> list = new List<Thing>();
		Zone_Growing val = GrowingZoneFor(clicked, ((Thing)pawn).Map);
		if (val == null)
		{
			return list;
		}
		ZoneManager zoneManager = ((Thing)pawn).Map.zoneManager;
		foreach (Thing item in SameKind(pawn, clicked, kind, allowHarvest, threshold, extraDefs))
		{
			if ((object)zoneManager.ZoneAt(item.Position) == val)
			{
				list.Add(item);
			}
		}
		SortByDistanceTo(list, clicked.Position);
		EnsureAnchorFirst(list, clicked);
		return list;
	}

	public static List<Thing> ReFloodVisibleVein(Map map, Pawn pawn, ThingDef def, IntVec3 seed, int cap, IEnumerable<IntVec3> routeFootprint, out bool fogCaution)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		fogCaution = false;
		if (map == null || def == null)
		{
			return new List<Thing>();
		}
		HashSet<RouteCell> hashSet = new HashSet<RouteCell>
		{
			new RouteCell(seed.x, seed.z)
		};
		if (routeFootprint != null)
		{
			foreach (IntVec3 item in routeFootprint)
			{
				hashSet.Add(new RouteCell(item.x, item.z));
			}
		}
		return FloodVisibleVein(map, pawn, def, seed, (cap <= 0) ? 100 : cap, (Thing _) => true, out fogCaution, hashSet);
	}

	private static List<Thing> FloodVisibleVein(Map map, Pawn pawn, ThingDef def, IntVec3 seed, int cap, Func<Thing, bool> candidateGate, out bool fogCaution, HashSet<RouteCell> connectors = null)
	{
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		fogCaution = false;
		Dictionary<RouteCell, Thing> dictionary = new Dictionary<RouteCell, Thing>();
		HashSet<RouteCell> hashSet = new HashSet<RouteCell>();
		HashSet<RouteCell> hashSet2 = new HashSet<RouteCell>();
		List<Thing> list = map.listerThings.ThingsOfDef(def);
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if (val == null || !val.Spawned)
			{
				continue;
			}
			IntVec3 val2 = val.Position - seed;
			if (!(((IntVec3)(ref val2)).LengthHorizontal > 60f))
			{
				RouteCell routeCell = new RouteCell(val.Position.x, val.Position.z);
				if (map.fogGrid.IsFogged(val.Position))
				{
					hashSet2.Add(routeCell);
				}
				else if (!ForbidUtility.IsForbidden(val, pawn) && candidateGate(val) && !dictionary.ContainsKey(routeCell))
				{
					dictionary[routeCell] = val;
					hashSet.Add(routeCell);
				}
			}
		}
		RouteCell seed2 = new RouteCell(seed.x, seed.z);
		HashSet<RouteCell> hashSet3 = hashSet;
		int cap2 = cap;
		if (connectors != null && connectors.Count > 0)
		{
			hashSet3 = new HashSet<RouteCell>(hashSet);
			hashSet3.UnionWith(connectors);
			cap2 = ((cap > 0) ? (cap + connectors.Count) : cap);
		}
		List<RouteCell> list2 = VeinFloodMath.FloodOrder(hashSet3, seed2, cap2);
		fogCaution = VeinFloodMath.AnyAdjacent(list2, hashSet2);
		List<Thing> list3 = new List<Thing>(list2.Count);
		foreach (RouteCell item in list2)
		{
			if (dictionary.TryGetValue(item, out var value))
			{
				list3.Add(value);
			}
			if (cap > 0 && list3.Count >= cap)
			{
				break;
			}
		}
		return list3;
	}

	private static IEnumerable<Thing> SameKind(Pawn pawn, Thing clicked, RouteWorkKind kind, bool allowHarvest, int threshold, IReadOnlyList<ThingDef> extraDefs = null)
	{
		HashSet<Thing> seen = new HashSet<Thing>();
		foreach (Thing item in ScopeCandidates(((Thing)pawn).Map, clicked, kind, extraDefs))
		{
			if (item != null && item.Spawned && !ForbidUtility.IsForbidden(item, pawn) && seen.Add(item) && (item == clicked || IncludeForHarvest(((Thing)pawn).Map, item, kind, allowHarvest, threshold)))
			{
				yield return item;
			}
		}
	}

	private static IEnumerable<Thing> ScopeCandidates(Map map, Thing clicked, RouteWorkKind kind, IReadOnlyList<ThingDef> extraDefs = null)
	{
		switch (kind.scope)
		{
		case RouteTargetScope.AnyFilth:
			foreach (Thing item in map.listerThings.ThingsInGroup((ThingRequestGroup)15))
			{
				yield return item;
			}
			yield break;
		case RouteTargetScope.Constructible:
			foreach (Thing item2 in map.listerThings.ThingsInGroup((ThingRequestGroup)9))
			{
				if (item2.Faction == clicked.Faction)
				{
					yield return item2;
				}
			}
			foreach (Thing item3 in map.listerThings.ThingsInGroup((ThingRequestGroup)11))
			{
				if (item3.Faction == clicked.Faction)
				{
					yield return item3;
				}
			}
			yield break;
		case RouteTargetScope.DesignatedOnly:
			if (kind.designation == null)
			{
				yield break;
			}
			foreach (Designation item4 in map.designationManager.SpawnedDesignationsOfDef(kind.designation))
			{
				if (((LocalTargetInfo)(ref item4.target)).HasThing)
				{
					yield return ((LocalTargetInfo)(ref item4.target)).Thing;
				}
			}
			yield break;
		case RouteTargetScope.SameDefOrDesignated:
			foreach (Thing item5 in map.listerThings.ThingsOfDef(clicked.def))
			{
				yield return item5;
			}
			if (extraDefs != null)
			{
				for (int i = 0; i < extraDefs.Count; i++)
				{
					ThingDef val = extraDefs[i];
					if (val == null || val == clicked.def)
					{
						continue;
					}
					foreach (Thing item6 in map.listerThings.ThingsOfDef(val))
					{
						yield return item6;
					}
				}
			}
			if (kind.designation == DesignationDefOf.Mine)
			{
				foreach (Designation item7 in map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Mine))
				{
					Mineable firstMineable = GridsUtility.GetFirstMineable(((LocalTargetInfo)(ref item7.target)).Cell, map);
					if (firstMineable != null)
					{
						yield return (Thing)(object)firstMineable;
					}
				}
				foreach (Designation item8 in map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.MineVein))
				{
					Mineable firstMineable2 = GridsUtility.GetFirstMineable(((LocalTargetInfo)(ref item8.target)).Cell, map);
					if (firstMineable2 != null)
					{
						yield return (Thing)(object)firstMineable2;
					}
				}
			}
			else
			{
				if (kind.designation == null)
				{
					yield break;
				}
				foreach (Designation item9 in map.designationManager.SpawnedDesignationsOfDef(kind.designation))
				{
					if (((LocalTargetInfo)(ref item9.target)).HasThing)
					{
						yield return ((LocalTargetInfo)(ref item9.target)).Thing;
					}
				}
			}
			yield break;
		}
		foreach (Thing item10 in map.listerThings.ThingsOfDef(clicked.def))
		{
			yield return item10;
		}
		if (extraDefs == null)
		{
			yield break;
		}
		for (int i = 0; i < extraDefs.Count; i++)
		{
			ThingDef val2 = extraDefs[i];
			if (val2 == null || val2 == clicked.def)
			{
				continue;
			}
			foreach (Thing item11 in map.listerThings.ThingsOfDef(val2))
			{
				yield return item11;
			}
		}
	}

	private static bool IncludeForHarvest(Map map, Thing t, RouteWorkKind kind, bool allowHarvest, int threshold)
	{
		if (kind.designation == DesignationDefOf.HarvestPlant)
		{
			Plant val = (Plant)(object)((t is Plant) ? t : null);
			if (val != null)
			{
				if (!val.HarvestableNow)
				{
					return false;
				}
				if (map.designationManager.DesignationOn(t, DesignationDefOf.HarvestPlant) != null)
				{
					return true;
				}
				if (allowHarvest)
				{
					return val.Growth >= (float)threshold / 100f;
				}
				return false;
			}
		}
		return true;
	}

	private static void SortByDistanceTo(List<Thing> things, IntVec3 anchor)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		things.Sort(new ByDistanceToComparer(anchor));
	}

	private static void EnsureAnchorFirst(List<Thing> things, Thing anchor)
	{
		int num = things.IndexOf(anchor);
		if (num > 0)
		{
			things.RemoveAt(num);
			things.Insert(0, anchor);
		}
		else if (num < 0)
		{
			things.Insert(0, anchor);
		}
	}
}
