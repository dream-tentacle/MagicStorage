#define DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text;
using System.Xml;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
[assembly: AssemblyVersion("0.0.0.0")]
[module: RefSafetyRules(11)]
namespace SuperDimensionalStorage;

internal static class SDS_WorkSupply
{
	public static bool TryFindKey(Predicate<ThingDef> predicate, out SDS_StockKey key, out int available)
	{
		key = null;
		available = 0;
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || predicate == null)
		{
			return false;
		}
		List<SDS_StockEntry> list = instance.Snapshot(0);
		long num = 0L;
		for (int i = 0; i < list.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = list[i];
			if (sDS_StockEntry != null && sDS_StockEntry.key != null && sDS_StockEntry.key.thingDef != null && sDS_StockEntry.count > 0 && predicate(sDS_StockEntry.key.thingDef))
			{
				long availableCount = SDS_Availability.GetAvailableCount(instance, sDS_StockEntry.key);
				if (availableCount > num)
				{
					num = availableCount;
					key = sDS_StockEntry.key;
				}
			}
		}
		if (key == null || num <= 0)
		{
			return false;
		}
		available = (int)Math.Min(num, 2147483647L);
		return true;
	}
}
[HarmonyPatch(typeof(ResourceCounter), "UpdateResourceCounts")]
[HarmonyPriority(200)]
internal static class Patch_SDS_ResourceCounterWithGrid
{
	private static FieldInfo countedAmountsField;

	private static FieldInfo resourceMapField;

	private static readonly List<Thing> preservedBuffer = new List<Thing>();

	private static readonly List<SDS_StockKey> mirrorKeyBuffer = new List<SDS_StockKey>();

	private static readonly HashSet<ThingDef> defBuffer = new HashSet<ThingDef>();

	public static void Postfix(ResourceCounter __instance)
	{
		try
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || __instance == null)
			{
				return;
			}
			if (countedAmountsField == null)
			{
				countedAmountsField = AccessTools.Field(typeof(ResourceCounter), "countedAmounts");
			}
			if (resourceMapField == null)
			{
				resourceMapField = AccessTools.Field(typeof(ResourceCounter), "map");
			}
			object obj;
			if (!(resourceMapField != null))
			{
				obj = null;
			}
			else
			{
				object? value = resourceMapField.GetValue(__instance);
				obj = ((value is Map) ? value : null);
			}
			Map val = (Map)obj;
			if (val == null)
			{
				return;
			}
			bool flag = SDS_OutputUtility.MapExposesGrid(val);
			Dictionary<ThingDef, int> dictionary = ((countedAmountsField != null) ? (countedAmountsField.GetValue(__instance) as Dictionary<ThingDef, int>) : null);
			if (dictionary == null)
			{
				return;
			}
			List<SDS_StockEntry> list = instance.Snapshot(0);
			defBuffer.Clear();
			for (int i = 0; i < list.Count; i++)
			{
				SDS_StockEntry sDS_StockEntry = list[i];
				if (sDS_StockEntry != null && sDS_StockEntry.key != null && sDS_StockEntry.key.thingDef != null && sDS_StockEntry.count > 0 && sDS_StockEntry.key.thingDef.CountAsResource)
				{
					defBuffer.Add(sDS_StockEntry.key.thingDef);
				}
			}
			mirrorKeyBuffer.Clear();
			SDS_MirrorService.CopyMirrorKeys(val, mirrorKeyBuffer);
			for (int j = 0; j < mirrorKeyBuffer.Count; j++)
			{
				ThingDef val2 = ((mirrorKeyBuffer[j] != null) ? mirrorKeyBuffer[j].thingDef : null);
				if (val2 != null && val2.CountAsResource)
				{
					defBuffer.Add(val2);
				}
			}
			if (flag)
			{
				preservedBuffer.Clear();
				instance.AppendPreservedThings(preservedBuffer);
				for (int k = 0; k < preservedBuffer.Count; k++)
				{
					Thing val3 = preservedBuffer[k];
					if (val3 != null && !val3.Destroyed && val3.def != null && val3.def.CountAsResource)
					{
						defBuffer.Add(val3.def);
					}
				}
			}
			foreach (ThingDef item in defBuffer)
			{
				long num = SDS_Exposure.InjectedOnCounter(val, item, flag);
				if (num != 0)
				{
					int value2;
					int num2 = (dictionary.TryGetValue(item, out value2) ? value2 : 0);
					long num3 = num2 + num;
					dictionary[item] = (int)((num3 > 0) ? Math.Min(num3, 2147483647L) : 0);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("资源汇总注入超维空间库存失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(ResourceCounter), "ResourceCounterTick")]
internal static class Patch_SDS_ResourceCounterFastRefresh
{
	private static FieldInfo mapField;

	public static void Prefix(ResourceCounter __instance)
	{
		try
		{
			if (__instance != null && SDS_GameComponent_Grid.Instance != null)
			{
				Map val = MapOf(__instance);
				if (val != null && SDS_Exposure.TryBeginCounterRefresh(val))
				{
					__instance.UpdateResourceCounts();
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("资源汇总快速刷新失败（已忽略，原版 204 tick 周期不受影响）", exception);
		}
	}

	private static Map MapOf(ResourceCounter counter)
	{
		if (mapField == null)
		{
			mapField = AccessTools.Field(typeof(ResourceCounter), "map");
		}
		object result;
		if (!(mapField != null))
		{
			result = null;
		}
		else
		{
			object? value = mapField.GetValue(counter);
			result = ((value is Map) ? value : null);
		}
		return (Map)result;
	}
}
[HarmonyPatch(typeof(RecipeDef), "PotentiallyMissingIngredients")]
internal static class Patch_SDS_PotentiallyMissingIngredients
{
	public static void Postfix(RecipeDef __instance, Pawn billDoer, Map map, ref IEnumerable<ThingDef> __result)
	{
		try
		{
			if (__result == null || __instance == null)
			{
				return;
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || !SDS_OutputUtility.MapExposesGrid(map))
			{
				return;
			}
			List<ThingDef> list = null;
			bool flag = false;
			foreach (ThingDef item2 in __result)
			{
				if (item2 == null)
				{
					flag = true;
				}
				else
				{
					(list ?? (list = new List<ThingDef>())).Add(item2);
				}
			}
			HashSet<ThingDef> hashSet = ((list != null) ? SDS_BillIngredientQuery.CollectSatisfied(instance, __instance, list, billDoer, map) : null);
			List<ThingDef> list2 = null;
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					ThingDef item = list[i];
					if (hashSet != null && hashSet.Contains(item))
					{
						flag = true;
					}
					else
					{
						(list2 ?? (list2 = new List<ThingDef>())).Add(item);
					}
				}
			}
			if (flag)
			{
				__result = list2 ?? new List<ThingDef>();
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("配方缺料判定注入超维空间失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(Map), "GetChildHolders")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MapChildHoldersExposure
{
	public static void Postfix(Map __instance, List<IThingHolder> outChildren)
	{
		try
		{
			if (__instance == null || outChildren == null || SDS_GameComponent_Grid.Instance == null)
			{
				return;
			}
			for (int num = outChildren.Count - 1; num >= 0; num--)
			{
				if (outChildren[num] is ISDS_StorageEndpoint || outChildren[num] is Building_SDS_Vault)
				{
					outChildren.RemoveAt(num);
				}
			}
			if (!SDS_MirrorService.HasAnyMirrorOn(__instance))
			{
				return;
			}
			for (int i = 0; i < outChildren.Count; i++)
			{
				IThingHolder obj = outChildren[i];
				Pawn val = (Pawn)(object)((obj is Pawn) ? obj : null);
				if (val != null && SDS_ImplantUtility.IsPortOperational(val))
				{
					outChildren[i] = (IThingHolder)(object)SDS_ProjectionService.MapLevelHolderFor(val);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("共享库存向 Map 级枚举暴露失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(WealthWatcher), "CalculateWealthItems")]
[HarmonyPriority(200)]
internal static class Patch_SDS_WealthNormalize
{
	public static void Postfix(WealthWatcher __instance, Map ___map, ref float __result)
	{
		try
		{
			if (__instance == null || ___map == null)
			{
				return;
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance != null)
			{
				float num = SDS_Exposure.WealthAdjust(___map);
				if (num != 0f)
				{
					__result = ((__result + num > 0f) ? (__result + num) : 0f);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("殖民地财富归一化失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(WorkGiver_Haul), "JobOnThing")]
[HarmonyPriority(200)]
internal static class Patch_SDS_HaulGeneralToGrid
{
	public static void Postfix(WorkGiver_Haul __instance, Pawn pawn, Thing t, bool forced, ref Job __result)
	{
		try
		{
			if (pawn != null && t != null && __result != null && __result.def == SDS_JobDefOf.DepositToGrid && __result.workGiverDef == null && __instance != null)
			{
				__result.workGiverDef = ((WorkGiver)__instance).def;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("超维空间「搬运即存入」派活失败（已忽略，原版搬运行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(JobGiver_Haul), "TryGiveJob")]
[HarmonyPriority(200)]
internal static class Patch_SDS_JobGiverHaul
{
	public static void Postfix(Pawn pawn, ref Job __result)
	{
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__result != null || pawn == null || !((Thing)pawn).Spawned || ((Thing)pawn).Map == null || ((Thing)pawn).Faction != Faction.OfPlayer)
			{
				return;
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || !instance.Available)
			{
				return;
			}
			Thing val = GenClosest.ClosestThing_Global_Reachable(((Thing)pawn).Position, ((Thing)pawn).Map, (IEnumerable<Thing>)((Thing)pawn).Map.listerHaulables.ThingsPotentiallyNeedingHauling(), (PathEndMode)1, TraverseParms.For(pawn, (Danger)3, (TraverseMode)0, false, false, false, true), 9999f, (Predicate<Thing>)Validator, (Func<Thing, float>)null, false);
			if (val != null)
			{
				Job val2 = SDS_DepositService.TryMake(pawn, val, forced: false, null, multi: true);
				if (val2 != null)
				{
					__result = val2;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("超维空间「JobGiver 搬运」补派失败（已忽略，原版行为不受影响）", exception);
		}
		bool Validator(Thing t)
		{
			return t != null && !ForbidUtility.IsForbidden(t, pawn) && !SDS_MirrorRegistry.IsMirror(t) && SDS_DepositService.CanCarryCandidate(pawn, t, forced: false);
		}
	}
}
[HarmonyPatch(typeof(HaulAIUtility), "HaulToStorageJob")]
[HarmonyPriority(200)]
internal static class Patch_SDS_HaulToStorageJob
{
	public static void Postfix(Pawn p, Thing t, bool forced, ref Job __result)
	{
		try
		{
			if (p != null && t != null && (__result == null || __result.def != SDS_JobDefOf.DepositToGrid))
			{
				Job val = SDS_DepositService.TryMake(p, t, forced, __result, multi: true);
				if (val != null)
				{
					__result = val;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("超维空间「存储投递」派活失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(HaulAIUtility), "HaulToCellStorageJob")]
[HarmonyPriority(200)]
internal static class Patch_SDS_CarriedProductToGrid
{
	public static void Postfix(Pawn p, Thing t, ref Job __result)
	{
		try
		{
			if (p != null && t != null && __result != null && __result.def != null && __result.def != SDS_JobDefOf.DepositToGrid)
			{
				Job val = SDS_DepositService.TryMakeCarried(p, t, __result);
				if (val == null)
				{
					val = SDS_DepositService.TryMake(p, t, forced: false, __result, multi: true);
				}
				if (val != null)
				{
					__result = val;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("超维空间「格搬运」派活失败（已忽略，原版搬运行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(HaulAIUtility), "HaulToContainerJob")]
[HarmonyPriority(200)]
internal static class Patch_SDS_HaulToContainerJob
{
	public static void Postfix(Pawn p, Thing t, Thing container, ref Job __result)
	{
		try
		{
			if (p != null && t != null && container != null && __result != null && __result.def != null && __result.def != SDS_JobDefOf.DepositToGrid)
			{
				Job val = SDS_DepositService.TryMake(p, t, forced: false, __result, multi: true);
				if (val != null)
				{
					__result = val;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("超维空间「容器搬运」派活失败（已忽略，原版搬运行为不受影响）", exception);
		}
	}
}
public sealed class SDS_AccessPoint
{
	public Thing thing;

	public IntVec3 cell;

	public PathEndMode endMode;
}
public static class SDS_AccessPointResolver
{
	private sealed class MapEntry
	{
		public readonly List<SDS_AccessPoint> points = new List<SDS_AccessPoint>();

		public int builtTick = -1;

		public int builtVersion = -1;
	}

	private struct ReachKey : IEquatable<ReachKey>
	{
		public Map map;

		public IntVec3 root;

		public TraverseParms parms;

		public bool Equals(ReachKey other)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			return map == other.map && root == other.root && ((TraverseParms)(ref parms)).Equals(other.parms);
		}

		public override bool Equals(object obj)
		{
			return obj is ReachKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			int num = ((map != null) ? ((object)map).GetHashCode() : 0);
			num = (num * 397) ^ ((object)Unsafe.As<IntVec3, IntVec3>(ref root)/*cast due to constrained. prefix*/).GetHashCode();
			return (num * 397) ^ ((object)Unsafe.As<TraverseParms, TraverseParms>(ref parms)/*cast due to constrained. prefix*/).GetHashCode();
		}
	}

	private sealed class ReachEntry
	{
		public int tick;

		public int version;

		public SDS_AccessPoint point;
	}

	private const int CacheTicks = 60;

	private static readonly Dictionary<Map, MapEntry> cache = new Dictionary<Map, MapEntry>();

	private static readonly Dictionary<ReachKey, ReachEntry> reachCache = new Dictionary<ReachKey, ReachEntry>();

	private static readonly List<ReachKey> staleKeys = new List<ReachKey>();

	private const int ReachCacheTicks = 30;

	private const int ReachCacheMaxEntries = 128;

	public static void Invalidate()
	{
		cache.Clear();
		reachCache.Clear();
		staleKeys.Clear();
	}

	public static List<SDS_AccessPoint> GetPoints(Map map)
	{
		if (map == null)
		{
			return null;
		}
		if (!cache.TryGetValue(map, out var value) || value == null)
		{
			value = new MapEntry();
			cache[map] = value;
		}
		int num = ((Find.TickManager != null) ? Find.TickManager.TicksGame : 0);
		int endpoint = SDS_ReadVersions.Capture().endpoint;
		if (value.builtTick < 0 || value.builtVersion != endpoint || num - value.builtTick >= 60)
		{
			BuildPoints(map, value.points);
			value.builtTick = num;
			value.builtVersion = endpoint;
		}
		return value.points;
	}

	private static void BuildPoints(Map map, List<SDS_AccessPoint> points)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		points.Clear();
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			return;
		}
		List<SDS_EndpointHandle> list = new List<SDS_EndpointHandle>();
		SDS_EndpointResolver.Collect(map, list);
		for (int i = 0; i < list.Count; i++)
		{
			SDS_EndpointHandle sDS_EndpointHandle = list[i];
			if (sDS_EndpointHandle != null && sDS_EndpointHandle.Thing != null)
			{
				IntVec3 accessCell = sDS_EndpointHandle.AccessCell;
				if (((IntVec3)(ref accessCell)).IsValid && GenGrid.InBounds(accessCell, map))
				{
					points.Add(new SDS_AccessPoint
					{
						thing = sDS_EndpointHandle.Thing,
						cell = accessCell,
						endMode = sDS_EndpointHandle.AccessMode
					});
				}
			}
		}
	}

	public static float EffectiveDistanceSquared(IntVec3 from, Thing thing)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 val;
		if (thing == null || !SDS_MirrorRegistry.IsMirror(thing))
		{
			float result;
			if (thing == null)
			{
				result = float.MaxValue;
			}
			else
			{
				val = from - thing.PositionHeld;
				result = ((IntVec3)(ref val)).LengthHorizontalSquared;
			}
			return result;
		}
		if (!TryGetNearestAccess(from, thing.Map, out var point))
		{
			val = from - thing.PositionHeld;
			return ((IntVec3)(ref val)).LengthHorizontalSquared;
		}
		val = from - point.cell;
		return ((IntVec3)(ref val)).LengthHorizontalSquared;
	}

	public static bool TryGetNearestAccess(IntVec3 from, Map map, out SDS_AccessPoint point)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		point = FindNearestAccess(from, map, requireReach: false, default(TraverseParms));
		return point != null;
	}

	private static SDS_AccessPoint FindNearestAccess(IntVec3 from, Map map, bool requireReach, TraverseParms traverseParams)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		List<SDS_AccessPoint> points = GetPoints(map);
		if (points == null || points.Count == 0)
		{
			return null;
		}
		SDS_AccessPoint result = null;
		float num = float.MaxValue;
		for (int i = 0; i < points.Count; i++)
		{
			SDS_AccessPoint sDS_AccessPoint = points[i];
			if (sDS_AccessPoint != null)
			{
				IntVec3 val = from - sDS_AccessPoint.cell;
				float num2 = ((IntVec3)(ref val)).LengthHorizontalSquared;
				if (!(num2 >= num) && (!requireReach || map.reachability.CanReach(from, new LocalTargetInfo(sDS_AccessPoint.thing), sDS_AccessPoint.endMode, traverseParams)))
				{
					result = sDS_AccessPoint;
					num = num2;
				}
			}
		}
		return result;
	}

	public static bool TryGetNearestReachableAccess(IntVec3 from, Map map, TraverseParms traverseParams, out SDS_AccessPoint point)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		point = null;
		if (map == null)
		{
			return false;
		}
		try
		{
			int ticksGame = GenTicks.TicksGame;
			int endpoint = SDS_ReadVersions.Capture().endpoint;
			ReachKey key = new ReachKey
			{
				map = map,
				root = from,
				parms = traverseParams
			};
			if (reachCache.TryGetValue(key, out var value) && value != null && value.version == endpoint && value.tick <= ticksGame && ticksGame - value.tick <= 30)
			{
				point = value.point;
				return point != null;
			}
			if (value == null)
			{
				if (reachCache.Count >= 128)
				{
					PruneReachCache(ticksGame, endpoint);
				}
				value = new ReachEntry();
				reachCache[key] = value;
			}
			value.tick = ticksGame;
			value.version = endpoint;
			value.point = FindNearestReachable(from, map, traverseParams);
			point = value.point;
			if (reachCache.Count > 128)
			{
				PruneReachCache(ticksGame, endpoint);
			}
			return point != null;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("最近可达访问点查询失败（本次按不可达处理）", exception);
			point = null;
			return false;
		}
	}

	private static SDS_AccessPoint FindNearestReachable(IntVec3 from, Map map, TraverseParms traverseParams)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		return FindNearestAccess(from, map, requireReach: true, traverseParams);
	}

	private static void PruneReachCache(int now, int version)
	{
		staleKeys.Clear();
		foreach (KeyValuePair<ReachKey, ReachEntry> item in reachCache)
		{
			ReachEntry value = item.Value;
			if (value == null || value.version != version || now - value.tick > 120)
			{
				staleKeys.Add(item.Key);
			}
		}
		for (int i = 0; i < staleKeys.Count; i++)
		{
			reachCache.Remove(staleKeys[i]);
		}
		staleKeys.Clear();
	}

	public static bool CanReachViaAccess(IntVec3 from, Map map, TraverseParms traverseParams)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		SDS_AccessPoint point;
		return TryGetNearestReachableAccess(from, map, traverseParams, out point);
	}

	public static bool TryGetNearestReachable(Pawn pawn, out SDS_AccessPoint point)
	{
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		point = null;
		if (pawn == null || !((Thing)pawn).Spawned || ((Thing)pawn).Map == null)
		{
			return false;
		}
		if (SDS_ImplantUtility.IsPortOperational(pawn))
		{
			SDS_EndpointHandle sDS_EndpointHandle = SDS_EndpointHandle.ForPawn(pawn, self: true);
			point = new SDS_AccessPoint
			{
				thing = (Thing)(object)pawn,
				cell = sDS_EndpointHandle.AccessCell,
				endMode = sDS_EndpointHandle.AccessMode
			};
			return true;
		}
		return TryGetNearestReachableAccess(((Thing)pawn).Position, ((Thing)pawn).Map, TraverseParms.For(pawn, (Danger)3, (TraverseMode)0, false, false, false, true), out point);
	}
}
public static class SDS_API
{
	public const int Version = 2;

	public static IReadOnlyList<Thing> VisibleThingsOfDef(Map map, ThingDef def)
	{
		List<Thing> list = new List<Thing>();
		try
		{
			if (map == null || def == null || map.listerThings == null)
			{
				return list;
			}
			List<Thing> list2 = map.listerThings.ThingsOfDef(def);
			if (list2 == null)
			{
				return list;
			}
			for (int i = 0; i < list2.Count; i++)
			{
				Thing val = list2[i];
				if (val != null && !val.Destroyed && val.Spawned && val.Map == map)
				{
					list.Add(val);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/看库存异常", exception);
			list.Clear();
		}
		return list;
	}

	public static SDS_ApiStockInfo ReadStock(SDS_StockKey key)
	{
		SDS_ApiStockInfo result = new SDS_ApiStockInfo
		{
			key = key,
			result = SDS_ApiResultCode.Ok
		};
		try
		{
			result.versions = SDS_ReadVersions.Capture();
			result.resolved = key?.Resolved ?? false;
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null)
			{
				result.result = SDS_ApiResultCode.GridUnavailable;
				result.failure = SDS_SupplyFailure.Insufficient;
				return result;
			}
			if (key != null)
			{
				result.forbidden = instance.IsForbidden(key);
				if (key.preserved)
				{
					if (!instance.TryGetPreservedTotals(key, out result.total, out var _))
					{
						result.total = 0L;
					}
				}
				else
				{
					result.total = instance.GetCount(key);
				}
				if (!instance.Available)
				{
					result.result = SDS_ApiResultCode.GridUnavailable;
					result.available = 0L;
					result.failure = SDS_SupplyFailure.Insufficient;
				}
				else
				{
					result.available = SDS_Availability.GetAvailableCount(instance, key);
					result.failure = SDS_Availability.GetFailure(key);
				}
			}
			else
			{
				result.failure = SDS_SupplyFailure.UnresolvedDef;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/读库存异常", exception);
			result.result = SDS_ApiResultCode.InternalError;
			result.failure = SDS_SupplyFailure.Insufficient;
		}
		return result;
	}

	public static SDS_ApiStockInfo ReadStockFor(Thing thing)
	{
		try
		{
			if (thing == null)
			{
				return ReadStock(null);
			}
			SDS_StockKey key = (SDS_DepositRules.RequiresPreservedStorage(thing) ? SDS_StockKey.FromPreservedThing(thing) : SDS_StockKey.FromThing(thing));
			return ReadStock(key);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/读实物库存异常", exception);
			return new SDS_ApiStockInfo
			{
				result = SDS_ApiResultCode.InternalError,
				failure = SDS_SupplyFailure.Insufficient
			};
		}
	}

	public static SDS_ReadVersions CaptureReadVersions()
	{
		try
		{
			return SDS_ReadVersions.Capture();
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/读版本异常", exception);
			return default(SDS_ReadVersions);
		}
	}

	public static bool TryFindDepositEndpoint(Thing thing, Map map, IntVec3 reference, SDS_ApiCaller caller, out Thing endpoint, out SDS_ApiResultCode code)
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		endpoint = null;
		try
		{
			if (!ValidateDepositCall(thing, map, caller, out code))
			{
				return false;
			}
			Map val = map ?? thing.MapHeld;
			if (val == null)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			IntVec3 reference2 = (((IntVec3)(ref reference)).IsValid ? reference : (thing.SpawnedOrAnyParentSpawned ? thing.PositionHeld : val.Center));
			SDS_EndpointQuery query = SDS_EndpointQuery.DepositForNonPawn(thing, val, reference2);
			Predicate<SDS_EndpointHandle> filter = (SDS_EndpointHandle handle) => handle != null && handle.Thing != null && (handle.Thing.Faction == null || handle.Thing.Faction == caller.faction);
			if (!SDS_EndpointResolver.TryFindBestForNonPawn(in query, filter, out var best, out var _) || best == null || best.Thing == null)
			{
				code = SDS_ApiResultCode.NoEndpoint;
				return false;
			}
			endpoint = best.Thing;
			code = SDS_ApiResultCode.Ok;
			return true;
		}
		catch (Exception e)
		{
			endpoint = null;
			return Fail("存/找端点异常", e, out code);
		}
	}

	public static bool TryFindWithdrawPoint(Pawn actor, out Thing endpoint, out bool self, out SDS_ApiResultCode code)
	{
		endpoint = null;
		self = false;
		try
		{
			if (actor == null || !((Thing)actor).Spawned || ((Thing)actor).Map == null || actor.Dead || ((Thing)actor).Faction != Faction.OfPlayer)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			SDS_WithdrawPoint sDS_WithdrawPoint = SDS_OutputUtility.FindWithdrawPoint(actor, ((Thing)actor).Map, forced: false);
			if (sDS_WithdrawPoint == null || sDS_WithdrawPoint.Endpoint == null)
			{
				code = SDS_ApiResultCode.NoEndpoint;
				return false;
			}
			endpoint = sDS_WithdrawPoint.Endpoint;
			self = sDS_WithdrawPoint.IsSelf;
			code = SDS_ApiResultCode.Ok;
			return true;
		}
		catch (Exception e)
		{
			endpoint = null;
			self = false;
			return Fail("取/找取物点异常", e, out code);
		}
	}

	public static bool TryDepositNow(Thing thing, Map map, IntVec3 reference, SDS_ApiCaller caller, out int accepted, out SDS_ApiResultCode code)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		accepted = 0;
		try
		{
			if (!TryFindDepositEndpoint(thing, map, reference, caller, out var endpoint, out code))
			{
				return false;
			}
			return TryDepositAt(thing, endpoint, caller, out accepted, out code);
		}
		catch (Exception e)
		{
			accepted = 0;
			return Fail("存/立即入账异常", e, out code);
		}
	}

	public static bool TryDepositAt(Thing thing, Thing endpoint, SDS_ApiCaller caller, out int accepted, out SDS_ApiResultCode code)
	{
		accepted = 0;
		try
		{
			if (!ValidateDepositCall(thing, (endpoint != null) ? endpoint.MapHeld : null, caller, out code))
			{
				return false;
			}
			if (endpoint == null || endpoint.Destroyed || !endpoint.Spawned || !(endpoint is ISDS_StorageEndpoint endpoint2))
			{
				code = SDS_ApiResultCode.EndpointUnavailable;
				return false;
			}
			if (endpoint.Faction != null && endpoint.Faction != caller.faction)
			{
				code = SDS_ApiResultCode.NotOwnedByCaller;
				return false;
			}
			SDS_EndpointHandle sDS_EndpointHandle = SDS_EndpointHandle.ForBuilding(endpoint2);
			if (sDS_EndpointHandle == null || !sDS_EndpointHandle.Operational)
			{
				code = SDS_ApiResultCode.EndpointUnavailable;
				return false;
			}
			if (!sDS_EndpointHandle.Accepts(thing, allowProducedMarked: false))
			{
				code = SDS_ApiResultCode.NotDepositable;
				return false;
			}
			if (!sDS_EndpointHandle.TryDeposit(thing, out accepted, allowProducedMarked: false) || accepted <= 0)
			{
				accepted = 0;
				code = SDS_ApiResultCode.DepositFailed;
				return false;
			}
			code = SDS_ApiResultCode.Ok;
			return true;
		}
		catch (Exception e)
		{
			accepted = 0;
			return Fail("存/端点入账异常", e, out code);
		}
	}

	public static bool TryWithdraw(SDS_StockKey key, int quantity, Pawn actor, SDS_WithdrawMode mode, out List<Thing> made, out SDS_ApiResultCode code)
	{
		return TryWithdraw(key, quantity, mode, SDS_ApiCaller.FromPawn(actor), out made, out code);
	}

	public static bool TryWithdrawFromMirror(Thing mirror, int quantity, SDS_WithdrawMode mode, SDS_ApiCaller caller, out Thing taken, out SDS_ApiResultCode code)
	{
		taken = null;
		try
		{
			if (!ValidateWithdrawCall(caller, out code))
			{
				return false;
			}
			if (mirror == null || quantity <= 0 || !SDS_MirrorRegistry.IsMirror(mirror) || mirror.Map == null)
			{
				code = SDS_ApiResultCode.NotWithdrawable;
				return false;
			}
			if (mode == SDS_WithdrawMode.VirtualHandoff)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			if (caller.pawn != null)
			{
				if (!((Thing)caller.pawn).Spawned || caller.pawn.Dead || ((Thing)caller.pawn).Map != mirror.Map)
				{
					code = SDS_ApiResultCode.InvalidArgument;
					return false;
				}
			}
			else if (mirror.Map != Find.CurrentMap)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			if (!SDS_WithdrawExecutor.TryTakeMirror(mirror, quantity, out taken, mode, caller.pawn) || taken == null)
			{
				code = SDS_ApiResultCode.TransactionFailed;
				return false;
			}
			code = SDS_ApiResultCode.Ok;
			return true;
		}
		catch (Exception e)
		{
			taken = null;
			return Fail("取/镜像取出异常", e, out code);
		}
	}

	public static bool TryWithdrawPreservedExact(List<ThingCount> plan, SDS_WithdrawMode mode, SDS_ApiCaller caller, out List<Thing> made, out SDS_ApiResultCode code)
	{
		made = null;
		try
		{
			if (!ValidateWithdrawCall(caller, out code))
			{
				return false;
			}
			if (plan == null || plan.Count == 0)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			if (mode == SDS_WithdrawMode.VirtualHandoff)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			if (caller.pawn == null && Find.CurrentMap == null)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			if (!SDS_WithdrawExecutor.TryWithdrawPreservedExact(plan, out made, mode, caller.pawn) || made == null || made.Count == 0)
			{
				made = null;
				code = SDS_ApiResultCode.TransactionFailed;
				return false;
			}
			code = SDS_ApiResultCode.Ok;
			return true;
		}
		catch (Exception e)
		{
			made = null;
			return Fail("取/保真精确取出异常", e, out code);
		}
	}

	private static bool ValidateDepositCall(Thing thing, Map map, SDS_ApiCaller caller, out SDS_ApiResultCode code)
	{
		code = SDS_ApiResultCode.Ok;
		if (thing == null || thing.Destroyed || thing.stackCount <= 0)
		{
			code = SDS_ApiResultCode.InvalidArgument;
			return false;
		}
		if (caller.faction != Faction.OfPlayer)
		{
			code = SDS_ApiResultCode.NotPlayerFaction;
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			code = SDS_ApiResultCode.GridUnavailable;
			return false;
		}
		if (SDS_MirrorRegistry.IsMirror(thing))
		{
			code = SDS_ApiResultCode.NotDepositable;
			return false;
		}
		if (!IsOwnedByCaller(thing, caller.faction))
		{
			code = SDS_ApiResultCode.NotOwnedByCaller;
			return false;
		}
		if (ForbidUtility.IsForbidden(thing, caller.faction))
		{
			code = SDS_ApiResultCode.Forbidden;
			return false;
		}
		if (!instance.CanDepositThing(thing))
		{
			code = SDS_ApiResultCode.NotDepositable;
			return false;
		}
		Map mapHeld = thing.MapHeld;
		if (mapHeld == null || (map != null && map != mapHeld))
		{
			code = SDS_ApiResultCode.InvalidArgument;
			return false;
		}
		return true;
	}

	private static bool ValidateWithdrawCall(SDS_ApiCaller caller, out SDS_ApiResultCode code)
	{
		code = SDS_ApiResultCode.Ok;
		if (caller.faction != Faction.OfPlayer)
		{
			code = SDS_ApiResultCode.NotPlayerFaction;
			return false;
		}
		return true;
	}

	private static bool IsOwnedByCaller(Thing thing, Faction faction)
	{
		if (thing == null || faction == null)
		{
			return false;
		}
		if (thing.Faction != null && thing.Faction != faction)
		{
			return false;
		}
		Thing val = (thing.SpawnedOrAnyParentSpawned ? thing.SpawnedParentOrMe : thing);
		Pawn val2 = (Pawn)(object)((val is Pawn) ? val : null);
		if (val2 != null && ((Thing)val2).Faction != faction)
		{
			return false;
		}
		if (val != null && val.Faction != null && val.Faction != faction)
		{
			return false;
		}
		return true;
	}

	private static SDS_ApiResultCode MapSupplyFailure(SDS_SupplyFailure failure)
	{
		return failure switch
		{
			SDS_SupplyFailure.UnresolvedDef => SDS_ApiResultCode.UnresolvedDef, 
			SDS_SupplyFailure.Forbidden => SDS_ApiResultCode.Forbidden, 
			SDS_SupplyFailure.Insufficient => SDS_ApiResultCode.NoStock, 
			SDS_SupplyFailure.RebuildFailed => SDS_ApiResultCode.RebuildFailed, 
			_ => SDS_ApiResultCode.TransactionFailed, 
		};
	}

	private static bool Fail(string where, Exception e, out SDS_ApiResultCode code)
	{
		SDS_Diag.ErrorOnce("公共API/" + where, e);
		code = SDS_ApiResultCode.InternalError;
		return false;
	}

	internal static bool IsProducedPendingConsumption(Thing thing)
	{
		try
		{
			return thing != null && SDS_ProducedThingMarker.IsMarked(thing);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/来源排除查询异常", exception);
			return false;
		}
	}

	internal static void MarkProducedPendingConsumption(IList<Thing> things)
	{
		if (things == null)
		{
			return;
		}
		try
		{
			for (int i = 0; i < things.Count; i++)
			{
				SDS_ProducedThingMarker.Mark(things[i]);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/来源排除标记异常", exception);
		}
	}

	internal static bool IsPawnPortOperational(Pawn pawn)
	{
		try
		{
			return pawn != null && SDS_ImplantUtility.IsPortOperational(pawn);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/端口可用性查询异常", exception);
			return false;
		}
	}

	internal static bool TryTakeDepositCandidate(Pawn pawn, Thing thing, bool forced, out SDS_ApiResultCode code)
	{
		code = SDS_ApiResultCode.Ok;
		try
		{
			if (!SDS_DepositService.CanTakeCandidate(pawn, thing, forced, out var endpoint) || endpoint == null)
			{
				code = SDS_ApiResultCode.NotDepositable;
				return false;
			}
			SDS_HaulJobDecisionCache.Remember(pawn, thing, endpoint);
			return true;
		}
		catch (Exception e)
		{
			return Fail("存/候选闸门异常", e, out code);
		}
	}

	internal static bool TryMakeDepositJob(Pawn pawn, Thing thing, bool forced, Job vanillaJob, bool multi, out Job job, out SDS_ApiResultCode code)
	{
		job = null;
		code = SDS_ApiResultCode.Ok;
		try
		{
			if (pawn == null || thing == null)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			if (SDS_HaulJobDecisionCache.TryGet(pawn, thing, out var endpoint))
			{
				job = SDS_DepositService.TryBuildFromEndpoint(pawn, thing, forced, endpoint, multi);
			}
			else
			{
				job = SDS_DepositService.TryMake(pawn, thing, forced, vanillaJob, multi);
			}
			if (job == null)
			{
				code = SDS_ApiResultCode.NotDepositable;
				return false;
			}
			return true;
		}
		catch (Exception e)
		{
			job = null;
			return Fail("存/派活异常", e, out code);
		}
	}

	internal static bool TryMakeDepositJobFromInventory(Pawn pawn, IList<Thing> items, out Job job, out SDS_ApiResultCode code)
	{
		job = null;
		code = SDS_ApiResultCode.Ok;
		try
		{
			if (pawn == null || items == null || items.Count == 0)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			if (!IsPawnPortOperational(pawn))
			{
				code = SDS_ApiResultCode.NoEndpoint;
				return false;
			}
			job = SDS_DepositService.TryMakeFromInventory(pawn, items);
			if (job == null)
			{
				code = SDS_ApiResultCode.NotDepositable;
				return false;
			}
			return true;
		}
		catch (Exception e)
		{
			job = null;
			return Fail("存/背包派活异常", e, out code);
		}
	}

	public static bool TryWithdraw(SDS_StockKey key, int quantity, SDS_WithdrawMode mode, SDS_ApiCaller caller, out List<Thing> made, out SDS_ApiResultCode code)
	{
		made = null;
		try
		{
			if (!ValidateWithdrawCall(caller, out code))
			{
				return false;
			}
			if (key == null || quantity <= 0)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			if (mode == SDS_WithdrawMode.VirtualHandoff)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			if (caller.pawn != null)
			{
				if (!((Thing)caller.pawn).Spawned || caller.pawn.Dead || ((Thing)caller.pawn).Map == null)
				{
					code = SDS_ApiResultCode.InvalidArgument;
					return false;
				}
			}
			else if (Find.CurrentMap == null)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			SDS_SupplyFailure failure = SDS_Availability.GetFailure(key);
			if (failure != SDS_SupplyFailure.None)
			{
				code = MapSupplyFailure(failure);
				return false;
			}
			if (!(key.preserved ? SDS_WithdrawExecutor.TryWithdrawPreserved(key, quantity, null, out made, mode, caller.pawn) : SDS_WithdrawExecutor.TryWithdrawVirtual(key, quantity, out made, mode, caller.pawn, requireFullQuantity: true)) || made == null || made.Count == 0)
			{
				made = null;
				code = SDS_ApiResultCode.TransactionFailed;
				return false;
			}
			code = SDS_ApiResultCode.Ok;
			return true;
		}
		catch (Exception e)
		{
			made = null;
			return Fail("取/事务异常", e, out code);
		}
	}

	public static long GetAvailableCount(SDS_StockKey key)
	{
		try
		{
			return SDS_Availability.GetAvailableCount(key);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/可用量查询异常", exception);
			return 0L;
		}
	}

	internal static long GetAvailableCountForDef(ThingDef def)
	{
		try
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (def == null || instance == null || !instance.Available)
			{
				return 0L;
			}
			return SDS_Availability.GetAvailableCount(instance, def);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/可用数量异常", exception);
			return 0L;
		}
	}

	internal static bool TryFindWithdrawableKey(ThingDef def, out SDS_StockKey key, out int available)
	{
		key = null;
		available = 0;
		try
		{
			if (def == null)
			{
				return false;
			}
			return SDS_WorkSupply.TryFindKey((ThingDef d) => d == def, out key, out available);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/找可取出键异常", exception);
			key = null;
			available = 0;
			return false;
		}
	}

	internal static void AppendPreservedThings(List<Thing> result)
	{
		if (result == null)
		{
			return;
		}
		try
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance != null && instance.Available)
			{
				instance.AppendPreservedThings(result);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/保真真身枚举异常", exception);
		}
	}

	internal static bool IsLivePreservedThing(Thing thing)
	{
		try
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			return instance != null && thing != null && instance.IsLivePreservedThing(thing);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/保真存活查询异常", exception);
			return false;
		}
	}

	internal static bool IsPreservedThing(Thing thing)
	{
		try
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			return instance != null && thing != null && instance.IsPreserved(thing);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/保真归属查询异常", exception);
			return false;
		}
	}

	internal static bool IsStockForbidden(SDS_StockKey key)
	{
		try
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			return instance != null && key != null && instance.IsForbidden(key);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/禁用查询异常", exception);
			return true;
		}
	}

	public static long GetPreservedAvailableCount(SDS_StockKey key)
	{
		try
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			return (instance != null && key != null) ? instance.GetPreservedAvailableCount(key) : 0;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/保真键可用量异常", exception);
			return 0L;
		}
	}

	public static long GetPreservedAvailableCount(ThingDef def)
	{
		try
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || def == null || !instance.Available)
			{
				return 0L;
			}
			List<Thing> list = new List<Thing>();
			instance.AppendPreservedThings(list);
			HashSet<SDS_StockKey> hashSet = new HashSet<SDS_StockKey>();
			long num = 0L;
			for (int i = 0; i < list.Count; i++)
			{
				Thing val = list[i];
				if (val != null && val.def == def)
				{
					SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(val);
					if (sDS_StockKey != null && hashSet.Add(sDS_StockKey) && !instance.IsForbidden(sDS_StockKey))
					{
						num += instance.GetPreservedAvailableCount(sDS_StockKey);
					}
				}
			}
			return num;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/保真可用量查询异常", exception);
			return 0L;
		}
	}

	internal static bool TryWithdrawVirtualByDef(ThingDef def, int count, Map map, SDS_ApiCaller caller, out List<Thing> made, out SDS_ApiResultCode code)
	{
		made = null;
		code = SDS_ApiResultCode.Ok;
		try
		{
			if (!ValidateWithdrawCall(caller, out code))
			{
				return false;
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || !instance.Available)
			{
				code = SDS_ApiResultCode.GridUnavailable;
				return false;
			}
			if (def == null || count <= 0)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			List<SDS_StockKey> list = new List<SDS_StockKey>();
			instance.CopyKeysForDef(def, list);
			if (list == null || list.Count == 0)
			{
				code = SDS_ApiResultCode.NoStock;
				return false;
			}
			List<Thing> list2 = new List<Thing>();
			int num = count;
			for (int i = 0; i < list.Count; i++)
			{
				if (num <= 0)
				{
					break;
				}
				SDS_StockKey sDS_StockKey = list[i];
				if (sDS_StockKey == null || instance.IsForbidden(sDS_StockKey))
				{
					continue;
				}
				long availableCount = instance.GetAvailableCount(sDS_StockKey);
				if (availableCount <= 0)
				{
					continue;
				}
				int num2 = (int)Math.Min(num, availableCount);
				if (num2 <= 0 || !SDS_WithdrawExecutor.TryWithdrawVirtual(sDS_StockKey, num2, out var made2, SDS_WithdrawMode.Container, caller.pawn, requireFullQuantity: true) || made2 == null || made2.Count == 0)
				{
					continue;
				}
				int num3 = 0;
				for (int j = 0; j < made2.Count; j++)
				{
					Thing val = made2[j];
					if (val != null && !val.Destroyed)
					{
						list2.Add(val);
						num3 += val.stackCount;
					}
				}
				num -= num3;
			}
			if (list2.Count == 0 || num > 0)
			{
				ReturnVirtualList(list2, map);
				code = SDS_ApiResultCode.TransactionFailed;
				return false;
			}
			made = list2;
			return true;
		}
		catch (Exception e)
		{
			made = null;
			return Fail("取/按def虚拟取出异常", e, out code);
		}
	}

	internal static bool TryWithdrawPreservedByDef(ThingDef def, int count, Map map, SDS_ApiCaller caller, out List<Thing> made, out SDS_ApiResultCode code)
	{
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		made = null;
		code = SDS_ApiResultCode.Ok;
		try
		{
			if (!ValidateWithdrawCall(caller, out code))
			{
				return false;
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || !instance.Available)
			{
				code = SDS_ApiResultCode.GridUnavailable;
				return false;
			}
			if (def == null || count <= 0)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			List<Thing> list = new List<Thing>();
			instance.AppendPreservedThings(list);
			List<ThingCount> list2 = new List<ThingCount>();
			int num = count;
			for (int i = 0; i < list.Count; i++)
			{
				if (num <= 0)
				{
					break;
				}
				Thing val = list[i];
				if (val != null && val.def == def && val.stackCount == 1 && instance.IsLivePreservedThing(val))
				{
					SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(val);
					if (sDS_StockKey != null && !instance.IsForbidden(sDS_StockKey))
					{
						list2.Add(new ThingCount(val, 1, false));
						num--;
					}
				}
			}
			if (num > 0 || list2.Count == 0)
			{
				code = SDS_ApiResultCode.NoStock;
				return false;
			}
			if (!SDS_WithdrawExecutor.TryWithdrawPreservedExact(list2, out var made2, SDS_WithdrawMode.Container, caller.pawn) || made2 == null || made2.Count == 0)
			{
				code = SDS_ApiResultCode.TransactionFailed;
				return false;
			}
			if (SumUnits(made2) < count)
			{
				ReturnPreservedList(made2, map);
				code = SDS_ApiResultCode.TransactionFailed;
				return false;
			}
			made = made2;
			return true;
		}
		catch (Exception e)
		{
			made = null;
			return Fail("取/按def保真取出异常", e, out code);
		}
	}

	internal static bool TryReturnVirtual(Thing thing, SDS_StockKey key, Map map, IntVec3 cell)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			SDS_StockKey key2 = key ?? SDS_StockKey.FromThing(thing);
			return SDS_ReturnUtility.ReturnVirtual(thing, key2, map, cell) == SDS_ReclaimOutcome.Returned;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/虚拟回滚异常", exception);
			return false;
		}
	}

	internal static bool TryReturnPreserved(Thing thing, Map map, IntVec3 cell)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			return SDS_ReturnUtility.ReturnPreserved(thing, map, cell) == SDS_ReclaimOutcome.Returned;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/保真回滚异常", exception);
			return false;
		}
	}

	internal static bool TryReturnToGrid(Thing thing, Map map, IntVec3 cell)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			return SDS_ReturnUtility.Return(thing, map, cell, SDS_ReturnFallback.None) == SDS_ReclaimOutcome.Returned;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/落地回滚异常", exception);
			return false;
		}
	}

	public static bool IsMirror(Thing thing)
	{
		try
		{
			return thing != null && SDS_MirrorRegistry.IsMirror(thing);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/镜像判定异常", exception);
			return false;
		}
	}

	public static bool TryGetDisplayKey(Thing thing, out SDS_StockKey key)
	{
		key = null;
		try
		{
			return thing != null && SDS_OutputUtility.TryGetDisplayKey(thing, out key) && key != null;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/展示键解析异常", exception);
			key = null;
			return false;
		}
	}

	public static bool TryGetWithdrawCell(Pawn actor, out IntVec3 cell, out SDS_ApiResultCode code)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		cell = IntVec3.Invalid;
		code = SDS_ApiResultCode.Ok;
		try
		{
			if (actor == null || !((Thing)actor).Spawned || ((Thing)actor).Map == null || actor.Dead || ((Thing)actor).Faction != Faction.OfPlayer)
			{
				code = SDS_ApiResultCode.InvalidArgument;
				return false;
			}
			SDS_WithdrawPoint sDS_WithdrawPoint = SDS_OutputUtility.FindWithdrawPoint(actor, ((Thing)actor).Map, forced: false);
			if (sDS_WithdrawPoint == null || !((IntVec3)(ref sDS_WithdrawPoint.Cell)).IsValid)
			{
				code = SDS_ApiResultCode.NoEndpoint;
				return false;
			}
			cell = sDS_WithdrawPoint.Cell;
			return true;
		}
		catch (Exception e)
		{
			cell = IntVec3.Invalid;
			return Fail("取/取物格解析异常", e, out code);
		}
	}

	internal static bool HasAnyEndpoint(Map map)
	{
		try
		{
			if (map == null)
			{
				return false;
			}
			List<SDS_EndpointHandle> list = new List<SDS_EndpointHandle>();
			SDS_EndpointResolver.Collect(map, list);
			for (int i = 0; i < list.Count; i++)
			{
				Thing val = ((list[i] != null) ? list[i].Thing : null);
				if (val != null && val.Spawned && val.Map == map && (val is ISDS_StorageEndpoint || val is Pawn))
				{
					return true;
				}
			}
			return false;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/端点存在性查询异常", exception);
			return false;
		}
	}

	internal static bool HasUsableBuildingEndpointWithin(Map map, IntVec3 root, float radius)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (map == null || !((IntVec3)(ref root)).IsValid || !GenGrid.InBounds(root, map))
			{
				return false;
			}
			List<SDS_EndpointHandle> list = new List<SDS_EndpointHandle>();
			SDS_EndpointResolver.Collect(map, list, includePawns: false);
			float num = Math.Max(0f, radius) * Math.Max(0f, radius);
			TraverseParms val = TraverseParms.For((TraverseMode)1, (Danger)3, false, false, false, true, false);
			for (int i = 0; i < list.Count; i++)
			{
				SDS_EndpointHandle sDS_EndpointHandle = list[i];
				Thing val2 = sDS_EndpointHandle?.Thing;
				if (val2 == null || val2.Destroyed || !val2.Spawned || val2.Map != map || !(val2 is ISDS_StorageEndpoint) || sDS_EndpointHandle.DistanceSquared(root) > num)
				{
					continue;
				}
				try
				{
					if (!map.reachability.CanReach(root, LocalTargetInfo.op_Implicit(val2), sDS_EndpointHandle.AccessMode, val))
					{
						continue;
					}
				}
				catch
				{
					continue;
				}
				return true;
			}
			return false;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/端点半径可达查询异常", exception);
			return false;
		}
	}

	internal static bool IsGridAvailable()
	{
		try
		{
			return SDS_GameComponent_Grid.Instance?.Available ?? false;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/账本可用性查询异常", exception);
			return false;
		}
	}

	internal static bool IsGridUsable(Map map)
	{
		try
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || !instance.Available || map == null)
			{
				return false;
			}
			return SDS_OutputUtility.MapExposesGrid(map);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("公共API/网格可用性查询异常", exception);
			return false;
		}
	}

	private static void ReturnVirtualList(List<Thing> things, Map map)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		if (things == null)
		{
			return;
		}
		for (int i = 0; i < things.Count; i++)
		{
			Thing val = things[i];
			if (val != null && !val.Destroyed)
			{
				TryReturnVirtual(val, SDS_StockKey.FromThing(val), map, IntVec3.Invalid);
			}
		}
	}

	private static void ReturnPreservedList(List<Thing> things, Map map)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		if (things == null)
		{
			return;
		}
		for (int i = 0; i < things.Count; i++)
		{
			Thing val = things[i];
			if (val != null && !val.Destroyed)
			{
				TryReturnPreserved(val, map, IntVec3.Invalid);
			}
		}
	}

	private static int SumUnits(List<Thing> things)
	{
		int num = 0;
		if (things == null)
		{
			return num;
		}
		for (int i = 0; i < things.Count; i++)
		{
			Thing val = things[i];
			if (val != null && !val.Destroyed)
			{
				num += val.stackCount;
			}
		}
		return num;
	}
}
public struct SDS_ApiCaller
{
	public Faction faction;

	public Pawn pawn;

	public Thing claimOwner;

	public bool IsNonPawn => pawn == null;

	public static SDS_ApiCaller FromPawn(Pawn pawn)
	{
		return new SDS_ApiCaller
		{
			faction = ((pawn != null) ? ((Thing)pawn).Faction : null),
			pawn = pawn,
			claimOwner = (Thing)(object)pawn
		};
	}

	public static SDS_ApiCaller FromFaction(Faction faction, Thing claimOwner = null)
	{
		return new SDS_ApiCaller
		{
			faction = faction,
			pawn = null,
			claimOwner = claimOwner
		};
	}
}
public enum SDS_ApiResultCode
{
	Ok,
	InvalidArgument,
	GridUnavailable,
	NoEndpoint,
	EndpointUnavailable,
	NotDepositable,
	Forbidden,
	NotOwnedByCaller,
	NotPlayerFaction,
	NoStock,
	UnresolvedDef,
	RebuildFailed,
	NotWithdrawable,
	DepositFailed,
	TransactionFailed,
	InternalError
}
public struct SDS_ApiStockInfo
{
	public SDS_StockKey key;

	public long total;

	public long available;

	public bool forbidden;

	public bool resolved;

	public SDS_SupplyFailure failure;

	public SDS_ReadVersions versions;

	public SDS_ApiResultCode result;
}
internal static class SDS_BillIngredientQuery
{
	internal static HashSet<ThingDef> CollectSatisfied(SDS_GameComponent_Grid grid, RecipeDef recipe, List<ThingDef> missing, Pawn billDoer, Map map)
	{
		List<IngredientCount> ingredients = recipe.ingredients;
		bool[] array = new bool[ingredients.Count];
		int num = ingredients.Count;
		List<SDS_StockEntry> list = grid.Snapshot(0);
		for (int i = 0; i < list.Count; i++)
		{
			if (num <= 0)
			{
				break;
			}
			SDS_StockEntry sDS_StockEntry = list[i];
			if (sDS_StockEntry != null && sDS_StockEntry.key != null && sDS_StockEntry.count > 0 && !sDS_StockEntry.key.preserved && sDS_StockEntry.key.thingDef != null && !grid.IsForbidden(sDS_StockEntry.key) && DefMayMatchAnySlot(recipe, ingredients, sDS_StockEntry.key.thingDef))
			{
				if (!SDS_MirrorService.TryGetMirror(map, sDS_StockEntry.key, out var mirror) || mirror == null)
				{
					mirror = SDS_OutputUtility.CachedDisplayThing(sDS_StockEntry.key);
				}
				if (mirror != null && !mirror.Destroyed)
				{
					num = MarkSatisfiedSlots(recipe, ingredients, array, num, mirror, billDoer, map);
				}
			}
		}
		if (num > 0)
		{
			List<Thing> list2 = new List<Thing>();
			grid.AppendPreservedThings(list2);
			for (int j = 0; j < list2.Count; j++)
			{
				if (num <= 0)
				{
					break;
				}
				Thing val = list2[j];
				if (val == null || val.Destroyed)
				{
					continue;
				}
				SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(val);
				if (sDS_StockKey != null && !grid.IsForbidden(sDS_StockKey))
				{
					Thing innerIfMinified = MinifyUtility.GetInnerIfMinified(val);
					if (innerIfMinified != null && innerIfMinified.def != null && DefMayMatchAnySlot(recipe, ingredients, innerIfMinified.def))
					{
						num = MarkSatisfiedSlots(recipe, ingredients, array, num, innerIfMinified, billDoer, map);
					}
				}
			}
		}
		HashSet<ThingDef> hashSet = new HashSet<ThingDef>();
		for (int k = 0; k < missing.Count; k++)
		{
			ThingDef val2 = missing[k];
			if (val2 == null)
			{
				continue;
			}
			for (int l = 0; l < ingredients.Count; l++)
			{
				IngredientCount val3 = ingredients[l];
				if (val3 != null && array[l] && val3.filter != null && val3.filter.Allows(val2) && (val3.IsFixedIngredient || recipe.fixedIngredientFilter.Allows(val2)))
				{
					hashSet.Add(val2);
					break;
				}
			}
		}
		return hashSet;
	}

	private static bool DefMayMatchAnySlot(RecipeDef recipe, List<IngredientCount> slots, ThingDef def)
	{
		if (def == null)
		{
			return false;
		}
		for (int i = 0; i < slots.Count; i++)
		{
			IngredientCount val = slots[i];
			if (val != null && val.filter != null && val.filter.Allows(def) && (val.IsFixedIngredient || recipe.fixedIngredientFilter.Allows(def)))
			{
				return true;
			}
		}
		return false;
	}

	private static int MarkSatisfiedSlots(RecipeDef recipe, List<IngredientCount> slots, bool[] satisfiedSlots, int remaining, Thing thing, Pawn billDoer, Map map)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (billDoer != null && ForbidUtility.IsForbidden(thing, billDoer))
		{
			return remaining;
		}
		if (thing.Spawned && GridsUtility.Fogged(thing.Position, map))
		{
			return remaining;
		}
		for (int i = 0; i < slots.Count; i++)
		{
			IngredientCount val = slots[i];
			if (!satisfiedSlots[i] && val != null && val.filter != null && (val.IsFixedIngredient || recipe.fixedIngredientFilter.Allows(thing)) && val.filter.Allows(thing))
			{
				satisfiedSlots[i] = true;
				remaining--;
			}
		}
		return remaining;
	}
}
public interface ISDS_StorageEndpoint : IHaulDestination, IStoreSettingsParent, IThingHolder, IHaulEnroute, ILoadReferenceable
{
	bool Operational { get; }

	ThingOwner InputBuffer { get; }
}
public static class SDS_EndpointUtility
{
	public const int EnrouteSpace = 99999;

	public static bool CanReceive(ISDS_StorageEndpoint endpoint)
	{
		if (endpoint == null)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			return false;
		}
		if (!BuildingEndpointUsable(endpoint))
		{
			return false;
		}
		return instance.UsedCapacity <= instance.TypeCapacity;
	}

	public static bool Accepts(ISDS_StorageEndpoint endpoint, Thing thing)
	{
		if (endpoint == null || thing == null)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available || !instance.CanDepositThing(thing))
		{
			return false;
		}
		if (SDS_ProducedThingMarker.IsMarked(thing))
		{
			return false;
		}
		return BuildingEndpointUsable(endpoint);
	}

	private static bool BuildingEndpointUsable(ISDS_StorageEndpoint endpoint)
	{
		if (endpoint is Building_SDS_Core core)
		{
			return SDS_EndpointContract.IsCoreUsable(core);
		}
		if (endpoint is Building_SDS_Port anchor)
		{
			return SDS_EndpointContract.IsAnchorUsable(anchor);
		}
		return endpoint.Operational;
	}

	public static int SpaceRemainingFor(ISDS_StorageEndpoint endpoint)
	{
		return CanReceive(endpoint) ? 99999 : 0;
	}

	public static void FlushInputBuffer(ISDS_StorageEndpoint endpoint)
	{
		ThingOwner val = endpoint?.InputBuffer;
		if (val != null && val.Count != 0)
		{
			List<Thing> list = new List<Thing>(val.Count);
			for (int i = 0; i < val.Count; i++)
			{
				list.Add(val[i]);
			}
			for (int num = list.Count - 1; num >= 0; num--)
			{
				FlushInputBufferItem(endpoint, list[num]);
			}
		}
	}

	internal static void FlushInputBufferItem(ISDS_StorageEndpoint endpoint, Thing thing)
	{
		ThingOwner val = endpoint?.InputBuffer;
		if (val == null || thing == null || thing.Destroyed || !val.Contains(thing))
		{
			return;
		}
		try
		{
			SDS_EndpointHandle sDS_EndpointHandle = SDS_EndpointHandle.ForBuilding(endpoint);
			if (sDS_EndpointHandle == null || !sDS_EndpointHandle.TryDeposit(thing, out var accepted) || accepted <= 0)
			{
				EjectToMap(endpoint, thing);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("端点输入缓冲入账失败（已退回地图）", exception);
			EjectToMap(endpoint, thing);
		}
	}

	public static void DrainInputBufferSafely(ISDS_StorageEndpoint endpoint, ThingOwner buffer)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		if (endpoint == null || buffer == null || buffer.Count == 0)
		{
			return;
		}
		FlushInputBuffer(endpoint);
		if (buffer.Count == 0)
		{
			return;
		}
		Map val = EndpointMap(endpoint);
		Thing val2 = (Thing)((endpoint is Thing) ? endpoint : null);
		if (val != null && val2 != null)
		{
			buffer.TryDropAll(val2.Position, val, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, true);
		}
		if (buffer.Count == 0)
		{
			return;
		}
		List<Thing> list = new List<Thing>((IEnumerable<Thing>)buffer);
		for (int i = 0; i < list.Count; i++)
		{
			if (SDS_ReturnUtility.Return(list[i], val, IntVec3.Invalid, SDS_ReturnFallback.None) != SDS_ReclaimOutcome.Returned)
			{
				SDS_Diag.Fail("端点拆除/缓冲残留无法回收", SDS_Diag.Ctx(val, null, list[i], null, "endpoint=" + endpoint.GetType().Name));
			}
		}
	}

	private static void EjectToMap(ISDS_StorageEndpoint endpoint, Thing thing)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if (thing == null || thing.Destroyed)
		{
			return;
		}
		Map val = EndpointMap(endpoint);
		if (val != null)
		{
			Thing val2 = thing.SplitOff(thing.stackCount);
			if (val2 != null)
			{
				SDS_PlacementUtility.TryPlaceOrSpawn(val2, ((IHaulDestination)endpoint).Position, val);
			}
		}
	}

	private static Map EndpointMap(ISDS_StorageEndpoint endpoint)
	{
		Thing val = (Thing)((endpoint is Thing) ? endpoint : null);
		return (val != null) ? val.Map : null;
	}
}
public class SDS_EndpointBuffer : ThingOwner<Thing>
{
	private readonly ISDS_StorageEndpoint endpoint;

	public SDS_EndpointBuffer(IThingHolder owner)
		: base(owner, false, (LookMode)2, true)
	{
		endpoint = owner as ISDS_StorageEndpoint;
	}

	public override bool TryAdd(Thing item, bool canMergeWithExistingStacks)
	{
		if (!CanAcceptNow(item))
		{
			return false;
		}
		bool flag = base.TryAdd(item, false);
		if (flag)
		{
			TryFlushItem(item);
		}
		return flag;
	}

	public override int TryAdd(Thing item, int count, bool canMergeWithExistingStacks)
	{
		if (!CanAcceptNow(item))
		{
			return 0;
		}
		List<Thing> before = ((((ThingOwner)this).Count > 0) ? new List<Thing>((IEnumerable<Thing>)this) : null);
		int num = base.TryAdd(item, count, false);
		if (num > 0)
		{
			FlushNewItems(before);
		}
		return num;
	}

	public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true)
	{
		if (!CanAcceptNow(item))
		{
			return 0;
		}
		return base.GetCountCanAccept(item, canMergeWithExistingStacks);
	}

	private bool CanAcceptNow(Thing item)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Invalid comparison between Unknown and I4
		if (item == null)
		{
			return false;
		}
		if ((int)Current.ProgramState != 2 || (int)Scribe.mode > 0)
		{
			return true;
		}
		return SDS_EndpointUtility.Accepts(endpoint, item);
	}

	private void FlushNewItems(List<Thing> before)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Invalid comparison between Unknown and I4
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Invalid comparison between Unknown and I4
		if (endpoint == null || (int)Current.ProgramState != 2 || (int)Scribe.mode > 0)
		{
			return;
		}
		List<Thing> list = new List<Thing>((IEnumerable<Thing>)this);
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if (val != null && (before == null || !before.Contains(val)))
			{
				SDS_EndpointUtility.FlushInputBufferItem(endpoint, val);
			}
		}
	}

	private void TryFlushItem(Thing item)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I4
		if (endpoint != null && item != null && (int)Current.ProgramState == 2 && (int)Scribe.mode <= 0)
		{
			SDS_EndpointUtility.FlushInputBufferItem(endpoint, item);
		}
	}
}
public sealed class SDS_EndpointHandle
{
	public Thing Thing { get; private set; }

	public Pawn Pawn { get; private set; }

	public bool IsSelf { get; private set; }

	public bool IsPawnPort => Pawn != null;

	private ISDS_StorageEndpoint BuildingPort => Thing as ISDS_StorageEndpoint;

	public Map Map
	{
		get
		{
			if (Pawn != null)
			{
				return ((Thing)Pawn).Spawned ? ((Thing)Pawn).Map : null;
			}
			return (Thing != null && Thing.Spawned) ? Thing.Map : null;
		}
	}

	public bool Operational
	{
		get
		{
			if (Pawn != null)
			{
				return ((Thing)Pawn).Spawned && !Pawn.Dead && SDS_ImplantUtility.IsPortOperational(Pawn);
			}
			return BuildingPort?.Operational ?? false;
		}
	}

	public IntVec3 AccessCell => AccessCellOf(Thing);

	public PathEndMode AccessMode
	{
		get
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			if (Pawn != null)
			{
				return (PathEndMode)2;
			}
			Thing thing = Thing;
			Building val = (Building)(object)((thing is Building) ? thing : null);
			if (val != null && ((Thing)val).Spawned && ((Thing)val).Map != null && ((Thing)val).def != null && ((Thing)val).def.hasInteractionCell)
			{
				IntVec3 interactionCell = ((Thing)val).InteractionCell;
				if (((IntVec3)(ref interactionCell)).IsValid && GenGrid.InBounds(interactionCell, ((Thing)val).Map) && GenGrid.Walkable(interactionCell, ((Thing)val).Map))
				{
					return (PathEndMode)4;
				}
			}
			return (PathEndMode)2;
		}
	}

	public int Priority => SDS_EndpointContract.PriorityOf(this);

	public static IntVec3 AccessCellOf(Thing thing)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		if (thing == null)
		{
			return IntVec3.Invalid;
		}
		Pawn val = (Pawn)(object)((thing is Pawn) ? thing : null);
		if (val != null)
		{
			return ((Thing)val).Spawned ? ((Thing)val).Position : IntVec3.Invalid;
		}
		Building val2 = (Building)(object)((thing is Building) ? thing : null);
		if (val2 != null && ((Thing)val2).Spawned && ((Thing)val2).Map != null)
		{
			IntVec3 interactionCell = ((Thing)val2).InteractionCell;
			if (((IntVec3)(ref interactionCell)).IsValid && GenGrid.InBounds(interactionCell, ((Thing)val2).Map) && GenGrid.Walkable(interactionCell, ((Thing)val2).Map))
			{
				return interactionCell;
			}
			return ((Thing)val2).Position;
		}
		return thing.PositionHeld;
	}

	public static float DistanceSquared(IntVec3 reference, Thing thing)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		return DistanceSquared(reference, AccessCellOf(thing));
	}

	public static float DistanceSquared(IntVec3 reference, IntVec3 cell)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		float result;
		if (!((IntVec3)(ref reference)).IsValid || !((IntVec3)(ref cell)).IsValid)
		{
			result = float.MaxValue;
		}
		else
		{
			IntVec3 val = reference - cell;
			result = ((IntVec3)(ref val)).LengthHorizontalSquared;
		}
		return result;
	}

	public float RankDistanceSquared(IntVec3 reference)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		return IsSelf ? 0f : DistanceSquared(reference);
	}

	public static SDS_EndpointHandle ForBuilding(ISDS_StorageEndpoint endpoint)
	{
		Thing val = (Thing)((endpoint is Thing) ? endpoint : null);
		if (val == null)
		{
			return null;
		}
		return new SDS_EndpointHandle
		{
			Thing = val
		};
	}

	public static SDS_EndpointHandle ForPawn(Pawn pawn, bool self)
	{
		if (pawn == null)
		{
			return null;
		}
		return new SDS_EndpointHandle
		{
			Thing = (Thing)(object)pawn,
			Pawn = pawn,
			IsSelf = self
		};
	}

	public static bool TryFromThing(Thing thing, out SDS_EndpointHandle handle)
	{
		handle = null;
		if (thing == null)
		{
			return false;
		}
		if (thing is ISDS_StorageEndpoint endpoint)
		{
			handle = ForBuilding(endpoint);
			return handle != null;
		}
		Pawn val = (Pawn)(object)((thing is Pawn) ? thing : null);
		if (val != null && SDS_ImplantUtility.IsPortOperational(val))
		{
			handle = ForPawn(val, self: false);
			return true;
		}
		return false;
	}

	public bool Accepts(Thing thing)
	{
		return Accepts(thing, allowProducedMarked: false);
	}

	public bool Accepts(Thing thing, bool allowProducedMarked)
	{
		if (thing == null || thing.Destroyed)
		{
			return false;
		}
		SDS_EndpointRejectReason reason;
		return SDS_EndpointContract.TryEvaluateGate(this, SDS_EndpointQuery.Deposit(thing, null, explicitUi: false, allowProducedMarked), out reason);
	}

	public bool TryDeposit(Thing thing, out int accepted)
	{
		return TryDeposit(thing, out accepted, allowProducedMarked: false);
	}

	public bool TryDeposit(Thing thing, out int accepted, bool allowProducedMarked)
	{
		accepted = 0;
		if (!Accepts(thing, allowProducedMarked))
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		return instance != null && instance.TryDeposit(thing, out accepted) && accepted > 0;
	}

	public bool CanReach(Pawn actor, Danger danger)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		if (actor == null || Thing == null)
		{
			return false;
		}
		if (IsSelf)
		{
			return true;
		}
		if (Pawn != null)
		{
			return ReachabilityUtility.CanReach(actor, LocalTargetInfo.op_Implicit((Thing)(object)Pawn), (PathEndMode)2, danger, false, false, (TraverseMode)0);
		}
		return ReachabilityUtility.CanReach(actor, LocalTargetInfo.op_Implicit(Thing), AccessMode, danger, false, false, (TraverseMode)0);
	}

	public float DistanceSquared(IntVec3 reference)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return DistanceSquared(reference, Thing);
	}
}
public static class SDS_EndpointResolver
{
	private sealed class MapEntry
	{
		public readonly List<Building_SDS_Core> cores = new List<Building_SDS_Core>();

		public readonly List<Building_SDS_Port> ports = new List<Building_SDS_Port>();

		public readonly List<Pawn> pawns = new List<Pawn>();

		public bool built;
	}

	private sealed class ReachEntry
	{
		public int tick;

		public int version;

		public Danger danger;

		public readonly List<SDS_EndpointHandle> handles = new List<SDS_EndpointHandle>();
	}

	private sealed class DistanceEntry
	{
		public int tick;

		public int invalidationVersion;

		public SDS_EndpointOp op;

		public Danger danger;

		public readonly List<SDS_EndpointHandle> handles = new List<SDS_EndpointHandle>();

		public readonly List<float> distances = new List<float>();
	}

	private static readonly Dictionary<Map, MapEntry> registry = new Dictionary<Map, MapEntry>();

	private static readonly List<Map> staleMaps = new List<Map>();

	private static bool registryDirty = true;

	private static readonly Dictionary<Pawn, ReachEntry> reachCache = new Dictionary<Pawn, ReachEntry>();

	private static readonly List<Pawn> stalePawns = new List<Pawn>();

	private const int ReachCacheTicks = 30;

	private const int ReachCacheMaxEntries = 256;

	private static readonly Dictionary<Pawn, DistanceEntry> distanceCache = new Dictionary<Pawn, DistanceEntry>();

	private static readonly List<Pawn> staleDistancePawns = new List<Pawn>();

	private const int DistanceCacheMaxEntries = 256;

	private const float DistanceCacheReferenceRadiusSquared = 100f;

	public static int Version { get; private set; }

	public static void MarkDirty()
	{
		registryDirty = true;
		Version++;
		SDS_EndpointContractCache.NotifyEndpointsChanged();
	}

	public static void Reset()
	{
		registry.Clear();
		reachCache.Clear();
		distanceCache.Clear();
		staleMaps.Clear();
		stalePawns.Clear();
		staleDistancePawns.Clear();
		registryDirty = true;
		Version++;
		SDS_EndpointContractCache.Reset();
	}

	private static MapEntry EntryFor(Map map)
	{
		if (registryDirty)
		{
			registryDirty = false;
			staleMaps.Clear();
			foreach (KeyValuePair<Map, MapEntry> item in registry)
			{
				if (item.Key == null || item.Key.Disposed)
				{
					staleMaps.Add(item.Key);
				}
			}
			for (int i = 0; i < staleMaps.Count; i++)
			{
				registry.Remove(staleMaps[i]);
			}
			staleMaps.Clear();
			foreach (KeyValuePair<Map, MapEntry> item2 in registry)
			{
				Rebuild(item2.Key, item2.Value);
			}
		}
		if (!registry.TryGetValue(map, out var value) || value == null)
		{
			value = new MapEntry();
			registry[map] = value;
		}
		if (!value.built)
		{
			Rebuild(map, value);
		}
		return value;
	}

	private static void Rebuild(Map map, MapEntry entry)
	{
		entry.cores.Clear();
		entry.ports.Clear();
		entry.pawns.Clear();
		entry.built = true;
		if (map == null || map.Disposed)
		{
			return;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			return;
		}
		List<Building_SDS_Core> list = instance.AllCoresOn(map);
		for (int i = 0; i < list.Count; i++)
		{
			Building_SDS_Core building_SDS_Core = list[i];
			if (building_SDS_Core != null && ((Thing)building_SDS_Core).Spawned && ((Thing)building_SDS_Core).Map == map)
			{
				entry.cores.Add(building_SDS_Core);
			}
		}
		List<Building_SDS_Port> list2 = instance.AllPortsOn(map);
		for (int j = 0; j < list2.Count; j++)
		{
			Building_SDS_Port building_SDS_Port = list2[j];
			if (building_SDS_Port != null && ((Thing)building_SDS_Port).Spawned && ((Thing)building_SDS_Port).Map == map)
			{
				entry.ports.Add(building_SDS_Port);
			}
		}
		if (map.mapPawns == null)
		{
			return;
		}
		IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
		for (int k = 0; k < allPawnsSpawned.Count; k++)
		{
			Pawn val = allPawnsSpawned[k];
			if (val != null && ((Thing)val).Spawned && ((Thing)val).Map == map && SDS_ImplantUtility.EffectiveChipDef(val) != null)
			{
				entry.pawns.Add(val);
			}
		}
	}

	private static ReachEntry GetReachEntry(Pawn actor, Danger danger)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		int ticksGame = GenTicks.TicksGame;
		if (reachCache.TryGetValue(actor, out var value) && value != null && value.version == Version && value.danger == danger && value.tick <= ticksGame && ticksGame - value.tick <= 30)
		{
			return value;
		}
		if (value == null)
		{
			if (reachCache.Count >= 256)
			{
				PruneReachCache(ticksGame);
			}
			value = new ReachEntry();
			reachCache[actor] = value;
		}
		value.tick = ticksGame;
		value.version = Version;
		value.danger = danger;
		value.handles.Clear();
		Map map = ((Thing)actor).Map;
		MapEntry mapEntry = EntryFor(map);
		for (int i = 0; i < mapEntry.cores.Count; i++)
		{
			Building_SDS_Core building_SDS_Core = mapEntry.cores[i];
			if (building_SDS_Core != null && ((Thing)building_SDS_Core).Spawned && ((Thing)building_SDS_Core).Map == map)
			{
				value.handles.Add(SDS_EndpointHandle.ForBuilding(building_SDS_Core));
			}
		}
		for (int j = 0; j < mapEntry.ports.Count; j++)
		{
			Building_SDS_Port building_SDS_Port = mapEntry.ports[j];
			if (building_SDS_Port != null && ((Thing)building_SDS_Port).Spawned && ((Thing)building_SDS_Port).Map == map)
			{
				value.handles.Add(SDS_EndpointHandle.ForBuilding(building_SDS_Port));
			}
		}
		for (int k = 0; k < mapEntry.pawns.Count; k++)
		{
			Pawn val = mapEntry.pawns[k];
			if (val != null && val != actor && ((Thing)val).Spawned && ((Thing)val).Map == map && !val.Dead)
			{
				value.handles.Add(SDS_EndpointHandle.ForPawn(val, self: false));
			}
		}
		if (reachCache.Count > 256)
		{
			PruneReachCache(ticksGame);
		}
		return value;
	}

	private static void PruneReachCache(int now)
	{
		stalePawns.Clear();
		foreach (KeyValuePair<Pawn, ReachEntry> item in reachCache)
		{
			Pawn key = item.Key;
			ReachEntry value = item.Value;
			if (key == null || ((Thing)key).Destroyed || !((Thing)key).Spawned || value == null || value.version != Version || now - value.tick > 120)
			{
				stalePawns.Add(key);
			}
		}
		for (int i = 0; i < stalePawns.Count; i++)
		{
			reachCache.Remove(stalePawns[i]);
		}
		stalePawns.Clear();
	}

	public static bool TryGetSelf(Pawn pawn, out SDS_EndpointHandle handle)
	{
		handle = null;
		if (pawn == null || !((Thing)pawn).Spawned || ((Thing)pawn).Map == null || pawn.Dead)
		{
			return false;
		}
		if (!SDS_ImplantUtility.IsPortOperational(pawn))
		{
			return false;
		}
		handle = SDS_EndpointHandle.ForPawn(pawn, self: true);
		return true;
	}

	public static void Collect(Map map, List<SDS_EndpointHandle> outHandles, bool includePawns = true)
	{
		if (outHandles == null)
		{
			return;
		}
		outHandles.Clear();
		if (map == null)
		{
			return;
		}
		MapEntry mapEntry = EntryFor(map);
		for (int i = 0; i < mapEntry.cores.Count; i++)
		{
			Building_SDS_Core building_SDS_Core = mapEntry.cores[i];
			if (building_SDS_Core != null && ((Thing)building_SDS_Core).Spawned && ((Thing)building_SDS_Core).Map == map && building_SDS_Core.Operational)
			{
				outHandles.Add(SDS_EndpointHandle.ForBuilding(building_SDS_Core));
			}
		}
		for (int j = 0; j < mapEntry.ports.Count; j++)
		{
			Building_SDS_Port building_SDS_Port = mapEntry.ports[j];
			if (building_SDS_Port != null && ((Thing)building_SDS_Port).Spawned && ((Thing)building_SDS_Port).Map == map && building_SDS_Port.Operational)
			{
				outHandles.Add(SDS_EndpointHandle.ForBuilding(building_SDS_Port));
			}
		}
		if (!includePawns)
		{
			return;
		}
		for (int k = 0; k < mapEntry.pawns.Count; k++)
		{
			Pawn val = mapEntry.pawns[k];
			if (val != null && ((Thing)val).Spawned && ((Thing)val).Map == map && !val.Dead && SDS_ImplantUtility.IsPortOperational(val))
			{
				outHandles.Add(SDS_EndpointHandle.ForPawn(val, self: false));
			}
		}
	}

	private static void PruneDistanceCache(int now)
	{
		staleDistancePawns.Clear();
		foreach (KeyValuePair<Pawn, DistanceEntry> item in distanceCache)
		{
			Pawn key = item.Key;
			DistanceEntry value = item.Value;
			if (key == null || ((Thing)key).Destroyed || !((Thing)key).Spawned || value == null || value.invalidationVersion != SDS_EndpointContractCache.Version || now - value.tick > 120)
			{
				staleDistancePawns.Add(key);
			}
		}
		for (int i = 0; i < staleDistancePawns.Count; i++)
		{
			distanceCache.Remove(staleDistancePawns[i]);
		}
		staleDistancePawns.Clear();
	}

	private static DistanceEntry GetDistanceEntry(Pawn actor, SDS_EndpointOp op, Danger danger, IntVec3 reference, out bool useCache)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		useCache = false;
		if (actor == null || !((Thing)actor).Spawned || ((Thing)actor).Map == null)
		{
			return null;
		}
		IntVec3 val = reference - ((Thing)actor).Position;
		if ((float)((IntVec3)(ref val)).LengthHorizontalSquared > 100f)
		{
			return null;
		}
		int ticksGame = GenTicks.TicksGame;
		if (distanceCache.TryGetValue(actor, out var value) && value != null && value.invalidationVersion == SDS_EndpointContractCache.Version && value.op == op && value.danger == danger && value.tick <= ticksGame && ticksGame - value.tick <= 30)
		{
			useCache = true;
			return value;
		}
		if (value == null)
		{
			if (distanceCache.Count >= 256)
			{
				PruneDistanceCache(ticksGame);
			}
			value = new DistanceEntry();
			distanceCache[actor] = value;
		}
		value.tick = ticksGame;
		value.invalidationVersion = SDS_EndpointContractCache.Version;
		value.op = op;
		value.danger = danger;
		value.handles.Clear();
		value.distances.Clear();
		ReachEntry reachEntry = GetReachEntry(actor, danger);
		for (int i = 0; i < reachEntry.handles.Count; i++)
		{
			SDS_EndpointHandle sDS_EndpointHandle = reachEntry.handles[i];
			if (sDS_EndpointHandle != null && !sDS_EndpointHandle.IsSelf)
			{
				value.handles.Add(sDS_EndpointHandle);
				value.distances.Add(sDS_EndpointHandle.DistanceSquared(((Thing)actor).Position));
			}
		}
		if (distanceCache.Count > 256)
		{
			PruneDistanceCache(ticksGame);
		}
		useCache = true;
		return value;
	}

	public static bool TryFindBest(in SDS_EndpointQuery query, out SDS_EndpointHandle best, out SDS_EndpointDecision bestDecision)
	{
		return TryFindBestCore(in query, null, out best, out bestDecision);
	}

	public static bool TryFindBest(in SDS_EndpointQuery query, Predicate<SDS_EndpointHandle> filter, out SDS_EndpointHandle best, out SDS_EndpointDecision bestDecision)
	{
		return TryFindBestCore(in query, filter, out best, out bestDecision);
	}

	public static bool TryFindBestForNonPawn(in SDS_EndpointQuery query, out SDS_EndpointHandle best, out SDS_EndpointDecision bestDecision)
	{
		return TryFindBestForNonPawn(in query, null, out best, out bestDecision);
	}

	public static bool TryFindBestForNonPawn(in SDS_EndpointQuery query, Predicate<SDS_EndpointHandle> filter, out SDS_EndpointHandle best, out SDS_EndpointDecision bestDecision)
	{
		return TryFindBestCore(in query, filter, out best, out bestDecision);
	}

	private static bool TryFindBestCore(in SDS_EndpointQuery query, Predicate<SDS_EndpointHandle> filter, out SDS_EndpointHandle best, out SDS_EndpointDecision bestDecision)
	{
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f7: Unknown result type (might be due to invalid IL or missing references)
		best = null;
		bestDecision = default(SDS_EndpointDecision);
		bestDecision.Reset();
		try
		{
			bool ignorePriority = query.op == SDS_EndpointOp.Withdraw || SDS_EndpointContract.BypassPriority(in query);
			if (query.actor == null)
			{
				if (query.op != SDS_EndpointOp.Deposit || query.map == null || query.thing == null)
				{
					return false;
				}
				List<SDS_EndpointHandle> list = new List<SDS_EndpointHandle>(8);
				Collect(query.map, list, includePawns: false);
				for (int i = 0; i < list.Count; i++)
				{
					SDS_EndpointHandle sDS_EndpointHandle = list[i];
					if (sDS_EndpointHandle != null && !sDS_EndpointHandle.IsSelf && (filter == null || filter(sDS_EndpointHandle)) && SDS_EndpointContract.TryEvaluateCore(sDS_EndpointHandle, in query, out var decision) && (best == null || SDS_EndpointContract.IsBetter(in decision, in bestDecision, ignorePriority)))
					{
						best = sDS_EndpointHandle;
						bestDecision = decision;
					}
				}
				return best != null;
			}
			if (!((Thing)query.actor).Spawned || ((Thing)query.actor).Map == null)
			{
				return false;
			}
			List<SDS_EndpointDecision> list2 = new List<SDS_EndpointDecision>(8);
			IntVec3 reference = query.reference;
			bool useCache;
			DistanceEntry distanceEntry = ((((IntVec3)(ref reference)).IsValid && query.reference == ((Thing)query.actor).Position) ? GetDistanceEntry(query.actor, query.op, query.danger, query.reference, out useCache) : null);
			if (distanceEntry != null && distanceEntry != null)
			{
				for (int j = 0; j < distanceEntry.handles.Count; j++)
				{
					SDS_EndpointHandle sDS_EndpointHandle2 = distanceEntry.handles[j];
					if (sDS_EndpointHandle2 != null && !sDS_EndpointHandle2.IsSelf && (filter == null || filter(sDS_EndpointHandle2)) && SDS_EndpointContract.TryEvaluateCore(sDS_EndpointHandle2, in query, out var decision2))
					{
						decision2.distanceSquared = distanceEntry.distances[j];
						list2.Add(decision2);
					}
				}
			}
			else
			{
				ReachEntry reachEntry = GetReachEntry(query.actor, query.danger);
				for (int k = 0; k < reachEntry.handles.Count; k++)
				{
					SDS_EndpointHandle sDS_EndpointHandle3 = reachEntry.handles[k];
					if (sDS_EndpointHandle3 != null && !sDS_EndpointHandle3.IsSelf && (filter == null || filter(sDS_EndpointHandle3)) && SDS_EndpointContract.TryEvaluateCore(sDS_EndpointHandle3, in query, out var decision3))
					{
						list2.Add(decision3);
					}
				}
			}
			if (query.allowSelf && TryGetSelf(query.actor, out var handle) && (filter == null || filter(handle)) && SDS_EndpointContract.TryEvaluateCore(handle, in query, out var decision4))
			{
				list2.Add(decision4);
			}
			if (list2.Count == 0)
			{
				return false;
			}
			bool[] array = new bool[list2.Count];
			SDS_EndpointDecision sDS_EndpointDecision;
			while (true)
			{
				int num = -1;
				for (int l = 0; l < list2.Count; l++)
				{
					if (!array[l] && (num < 0 || SDS_EndpointContract.IsBetter(list2[l], list2[num], ignorePriority)))
					{
						num = l;
					}
				}
				if (num < 0)
				{
					return false;
				}
				sDS_EndpointDecision = list2[num];
				if (!query.requireReach || sDS_EndpointDecision.handle == null || sDS_EndpointDecision.handle.IsSelf || sDS_EndpointDecision.handle.CanReach(query.actor, query.danger))
				{
					break;
				}
				array[num] = true;
			}
			best = sDS_EndpointDecision.handle;
			bestDecision = sDS_EndpointDecision;
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("端点统一选择失败（本次按未找到处理）", exception);
			return false;
		}
	}
}
public static class SDS_BuildingUtility
{
	public static bool IsOperational(Building building, float requiredWatts)
	{
		CompBreakdownable breakdown = ((building != null) ? ((ThingWithComps)building).GetComp<CompBreakdownable>() : null);
		CompPowerTrader power = ((building != null) ? ((ThingWithComps)building).GetComp<CompPowerTrader>() : null);
		return IsOperational(building, requiredWatts, power, breakdown);
	}

	public static bool IsOperational(Building building, float requiredWatts, CompPowerTrader power, CompBreakdownable breakdown)
	{
		if (building == null || !((Thing)building).Spawned || ((Thing)building).Destroyed || ((Thing)building).Map == null)
		{
			return false;
		}
		if (((Thing)building).Faction != Faction.OfPlayer)
		{
			return false;
		}
		if (((Thing)building).HitPoints <= 0)
		{
			return false;
		}
		if (breakdown != null && breakdown.BrokenDown)
		{
			return false;
		}
		if (requiredWatts > 0f && power != null && !power.PowerOn)
		{
			return false;
		}
		return true;
	}
}
public class Building_SDS_Core : Building_WorkTable, ISDS_StorageEndpoint, IHaulDestination, IStoreSettingsParent, IThingHolder, IHaulEnroute, ILoadReferenceable, IBillGiver
{
	private const int OperationalPollIntervalTicks = 30;

	private int nextOperationalPollTick;

	private bool lastOperational;

	private CompPowerTrader cachedPowerComp;

	private CompBreakdownable cachedBreakdownComp;

	private SDS_EndpointBuffer inputBuffer;

	private StorageSettings settings;

	private int syncedFilterVersion = -1;

	public bool Operational => SDS_BuildingUtility.IsOperational((Building)(object)this, SDS_Config.CorePower, cachedPowerComp, cachedBreakdownComp);

	public ThingOwner InputBuffer => (ThingOwner)(object)inputBuffer;

	public bool HaulDestinationEnabled => SDS_EndpointUtility.CanReceive(this);

	public bool StorageTabVisible => false;

	public Building_SDS_Core()
	{
		inputBuffer = new SDS_EndpointBuffer((IThingHolder)(object)this);
	}

	bool IBillGiver.CurrentlyUsableForBills()
	{
		return Operational;
	}

	bool IBillGiver.UsableForBillsAfterFueling()
	{
		return Operational;
	}

	public bool Accepts(Thing thing)
	{
		return SDS_EndpointUtility.Accepts(this, thing);
	}

	public int SpaceRemainingFor(ThingDef stuff)
	{
		return SDS_EndpointUtility.SpaceRemainingFor(this);
	}

	public StorageSettings GetStoreSettings()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		if (settings == null)
		{
			settings = new StorageSettings((IStoreSettingsParent)(object)this);
		}
		SyncGlobalFilter();
		return settings;
	}

	public bool SyncStoragePriority()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		if (settings == null)
		{
			settings = new StorageSettings((IStoreSettingsParent)(object)this);
		}
		StoragePriority value = SDS_EndpointPriorityState.Value;
		if (settings.Priority == value)
		{
			return false;
		}
		settings.Priority = value;
		return true;
	}

	private void SyncGlobalFilter()
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance != null && settings != null && syncedFilterVersion != instance.FilterVersion)
		{
			syncedFilterVersion = instance.FilterVersion;
			settings.filter.CopyAllowancesFrom(instance.DepositFilter);
		}
	}

	public StorageSettings GetParentStoreSettings()
	{
		return null;
	}

	public void Notify_SettingsChanged()
	{
	}

	public ThingOwner GetDirectlyHeldThings()
	{
		return (ThingOwner)(object)inputBuffer;
	}

	public void GetChildHolders(List<IThingHolder> outChildren)
	{
		ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, (IList<Thing>)inputBuffer);
		if (Operational)
		{
			IThingHolder val = (IThingHolder)(object)SDS_ProjectionService.ProjectionFor((Thing)(object)this);
			if (val != null)
			{
				outChildren.Add(val);
			}
		}
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		if (base.billStack == null)
		{
			base.billStack = new BillStack((IBillGiver)(object)this);
		}
		((Building_WorkTable)this).SpawnSetup(map, respawningAfterLoad);
		cachedPowerComp = ((ThingWithComps)this).GetComp<CompPowerTrader>();
		cachedBreakdownComp = ((ThingWithComps)this).GetComp<CompBreakdownable>();
		if (inputBuffer == null)
		{
			inputBuffer = new SDS_EndpointBuffer((IThingHolder)(object)this);
		}
		SDS_GameComponent_Grid.Instance?.RegisterCore(this);
		SyncStoragePriority();
		lastOperational = Operational;
	}

	public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid.Instance?.UnregisterCore(this);
		((Building)this).DeSpawn(mode);
	}

	public override void Destroy(DestroyMode mode = (DestroyMode)0)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		Map map = ((Thing)this).Map;
		IntVec3 position = ((Thing)this).Position;
		SDS_GameComponent_Grid.Instance?.UnregisterCore(this);
		if (inputBuffer != null && ((ThingOwner)inputBuffer).Count > 0)
		{
			SDS_EndpointUtility.DrainInputBufferSafely(this, (ThingOwner)(object)inputBuffer);
			((ThingOwner)inputBuffer).ClearAndDestroyContents((DestroyMode)0);
		}
		((Building)this).Destroy(mode);
	}

	protected override void Tick()
	{
		((ThingWithComps)this).Tick();
		SDS_EndpointUtility.FlushInputBuffer(this);
		int ticksGame = GenTicks.TicksGame;
		if (ticksGame >= nextOperationalPollTick)
		{
			nextOperationalPollTick = ticksGame + 30;
			bool operational = Operational;
			if (operational != lastOperational)
			{
				lastOperational = operational;
				SDS_GameComponent_Grid.Instance?.NotifyStructureChanged();
			}
		}
	}

	public override void ExposeData()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Invalid comparison between Unknown and I4
		((Building_WorkTable)this).ExposeData();
		Scribe_Deep.Look<SDS_EndpointBuffer>(ref inputBuffer, "inputBuffer", new object[1] { this });
		if ((int)Scribe.mode == 4 && inputBuffer == null)
		{
			inputBuffer = new SDS_EndpointBuffer((IThingHolder)(object)this);
		}
	}

	public override string GetInspectString()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		string inspectString = ((ThingWithComps)this).GetInspectString();
		string text = ((object)Translator.Translate(Operational ? "SDS.Status.Online" : "SDS.Status.Offline")/*cast due to constrained. prefix*/).ToString();
		return GenText.NullOrEmpty(inspectString) ? text : (inspectString + "\n" + text);
	}
}
public class Building_SDS_Port : Building, ISDS_StorageEndpoint, IHaulDestination, IStoreSettingsParent, IThingHolder, IHaulEnroute, ILoadReferenceable
{
	private SDS_EndpointBuffer inputBuffer;

	private CompPowerTrader cachedPowerComp;

	private CompBreakdownable cachedBreakdownComp;

	private StorageSettings settings;

	private int syncedFilterVersion = -1;

	public bool Operational
	{
		get
		{
			if (!SDS_BuildingUtility.IsOperational((Building)(object)this, SDS_Config.PortPower, cachedPowerComp, cachedBreakdownComp))
			{
				return false;
			}
			return SDS_GameComponent_Grid.Instance?.MapHasOperationalCore(((Thing)this).Map) ?? false;
		}
	}

	public ThingOwner InputBuffer => (ThingOwner)(object)inputBuffer;

	public bool HaulDestinationEnabled => SDS_EndpointUtility.CanReceive(this);

	public bool StorageTabVisible => false;

	public Building_SDS_Port()
	{
		inputBuffer = new SDS_EndpointBuffer((IThingHolder)(object)this);
	}

	public bool Accepts(Thing thing)
	{
		return SDS_EndpointUtility.Accepts(this, thing);
	}

	public int SpaceRemainingFor(ThingDef stuff)
	{
		return SDS_EndpointUtility.SpaceRemainingFor(this);
	}

	public StorageSettings GetStoreSettings()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		if (settings == null)
		{
			settings = new StorageSettings((IStoreSettingsParent)(object)this);
		}
		SyncGlobalFilter();
		return settings;
	}

	public bool SyncStoragePriority()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		if (settings == null)
		{
			settings = new StorageSettings((IStoreSettingsParent)(object)this);
		}
		StoragePriority value = SDS_EndpointPriorityState.Value;
		if (settings.Priority == value)
		{
			return false;
		}
		settings.Priority = value;
		return true;
	}

	private void SyncGlobalFilter()
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance != null && settings != null && syncedFilterVersion != instance.FilterVersion)
		{
			syncedFilterVersion = instance.FilterVersion;
			settings.filter.CopyAllowancesFrom(instance.DepositFilter);
		}
	}

	public StorageSettings GetParentStoreSettings()
	{
		return null;
	}

	public void Notify_SettingsChanged()
	{
	}

	public ThingOwner GetDirectlyHeldThings()
	{
		return (ThingOwner)(object)inputBuffer;
	}

	public void GetChildHolders(List<IThingHolder> outChildren)
	{
		ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, (IList<Thing>)inputBuffer);
		if (Operational)
		{
			IThingHolder val = (IThingHolder)(object)SDS_ProjectionService.ProjectionFor((Thing)(object)this);
			if (val != null)
			{
				outChildren.Add(val);
			}
		}
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		((Building)this).SpawnSetup(map, respawningAfterLoad);
		cachedPowerComp = ((ThingWithComps)this).GetComp<CompPowerTrader>();
		cachedBreakdownComp = ((ThingWithComps)this).GetComp<CompBreakdownable>();
		if (inputBuffer == null)
		{
			inputBuffer = new SDS_EndpointBuffer((IThingHolder)(object)this);
		}
		SDS_GameComponent_Grid.Instance?.RegisterPort(this);
		SyncStoragePriority();
	}

	public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid.Instance?.UnregisterPort(this);
		((Building)this).DeSpawn(mode);
	}

	public override void Destroy(DestroyMode mode = (DestroyMode)0)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		Map map = ((Thing)this).Map;
		IntVec3 position = ((Thing)this).Position;
		SDS_GameComponent_Grid.Instance?.UnregisterPort(this);
		if (inputBuffer != null && ((ThingOwner)inputBuffer).Count > 0)
		{
			SDS_EndpointUtility.DrainInputBufferSafely(this, (ThingOwner)(object)inputBuffer);
			((ThingOwner)inputBuffer).ClearAndDestroyContents((DestroyMode)0);
		}
		((Building)this).Destroy(mode);
	}

	public override void ExposeData()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Invalid comparison between Unknown and I4
		((Building)this).ExposeData();
		Scribe_Deep.Look<SDS_EndpointBuffer>(ref inputBuffer, "inputBuffer", new object[1] { this });
		if ((int)Scribe.mode == 4 && inputBuffer == null)
		{
			inputBuffer = new SDS_EndpointBuffer((IThingHolder)(object)this);
		}
	}

	protected override void Tick()
	{
		((ThingWithComps)this).Tick();
		SDS_EndpointUtility.FlushInputBuffer(this);
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		foreach (Gizmo item in <>n__0())
		{
			yield return item;
		}
	}

	public override string GetInspectString()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		string inspectString = ((ThingWithComps)this).GetInspectString();
		string text = ((object)Translator.Translate(Operational ? "SDS.Status.Online" : "SDS.Status.Offline")/*cast due to constrained. prefix*/).ToString();
		return GenText.NullOrEmpty(inspectString) ? text : (inspectString + "\n" + text);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerable<Gizmo> <>n__0()
	{
		return ((Building)this).GetGizmos();
	}
}
public class Building_SDS_CapacityArray : Building
{
	private const int OperationalPollIntervalTicks = 30;

	private int nextOperationalPollTick;

	private bool lastOperational;

	private CompPowerTrader cachedPowerComp;

	private CompBreakdownable cachedBreakdownComp;

	public bool Operational => SDS_BuildingUtility.IsOperational((Building)(object)this, SDS_Config.ArrayPower, cachedPowerComp, cachedBreakdownComp);

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		((Building)this).SpawnSetup(map, respawningAfterLoad);
		cachedPowerComp = ((ThingWithComps)this).GetComp<CompPowerTrader>();
		cachedBreakdownComp = ((ThingWithComps)this).GetComp<CompBreakdownable>();
		SDS_GameComponent_Grid.Instance?.RegisterCapacityArray(this);
		lastOperational = Operational;
	}

	public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid.Instance?.UnregisterCapacityArray(this);
		((Building)this).DeSpawn(mode);
	}

	public override void Destroy(DestroyMode mode = (DestroyMode)0)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid.Instance?.UnregisterCapacityArray(this);
		((Building)this).Destroy(mode);
	}

	protected override void Tick()
	{
		((ThingWithComps)this).Tick();
		int ticksGame = GenTicks.TicksGame;
		if (ticksGame >= nextOperationalPollTick)
		{
			nextOperationalPollTick = ticksGame + 30;
			bool operational = Operational;
			if (operational != lastOperational)
			{
				lastOperational = operational;
				SDS_GameComponent_Grid.Instance?.NotifyStructureChanged();
			}
		}
	}

	public override string GetInspectString()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		string inspectString = ((ThingWithComps)this).GetInspectString();
		string text = ((object)Translator.Translate(Operational ? "SDS.Status.Online" : "SDS.Status.Offline")/*cast due to constrained. prefix*/).ToString();
		return GenText.NullOrEmpty(inspectString) ? text : (inspectString + "\n" + text);
	}
}
public class Building_SDS_Vault : Building_Storage, IHaulDestination, IStoreSettingsParent, IHaulSource, IThingHolder
{
	public const string DefName = "SDS_HiddenVault";

	private static readonly Dictionary<Map, Building_SDS_Vault> vaults = new Dictionary<Map, Building_SDS_Vault>();

	private static ThingDef vaultDef;

	private static bool vaultDefWarned;

	private ThingOwner<Thing> emptyContents;

	bool IHaulDestination.HaulDestinationEnabled => false;

	bool IStoreSettingsParent.StorageTabVisible => false;

	private static ThingDef VaultDef
	{
		get
		{
			if (vaultDef == null && !vaultDefWarned)
			{
				vaultDef = DefDatabase<ThingDef>.GetNamedSilentFail("SDS_HiddenVault");
				if (vaultDef == null)
				{
					vaultDefWarned = true;
					Log.Error("[超维存储] 找不到隐藏金库 ThingDef（SDS_HiddenVault）：镜像不会生成（Game 可正常运行）。请检查 Defs/SDS_Buildings.xml。");
				}
			}
			return vaultDef;
		}
	}

	public bool HaulSourceEnabled => true;

	public static bool IsVault(Thing thing)
	{
		return thing is Building_SDS_Vault;
	}

	public static void ResetStatics()
	{
		vaults.Clear();
	}

	public static Building_SDS_Vault GetOn(Map map)
	{
		if (map == null || map.Disposed)
		{
			return null;
		}
		if (vaults.TryGetValue(map, out var value) && value != null && !((Thing)value).Destroyed && ((Thing)value).Spawned && ((Thing)value).Map == map)
		{
			return value;
		}
		vaults.Remove(map);
		ThingDef val = VaultDef;
		if (val != null && map.listerThings != null)
		{
			List<Thing> list = map.listerThings.ThingsOfDef(val);
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] is Building_SDS_Vault building_SDS_Vault && ((Thing)building_SDS_Vault).Spawned && !((Thing)building_SDS_Vault).Destroyed && ((Thing)building_SDS_Vault).Map == map)
				{
					vaults[map] = building_SDS_Vault;
					return building_SDS_Vault;
				}
			}
		}
		return null;
	}

	public unsafe static Building_SDS_Vault EnsureOn(Map map, IntVec3 preferredCell, out IntVec3 cell)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		cell = IntVec3.Invalid;
		if (map == null || map.Disposed)
		{
			return null;
		}
		Building_SDS_Vault building_SDS_Vault = GetOn(map);
		if (building_SDS_Vault != null)
		{
			if (CellOwnedByForeignGroup(map, ((Thing)building_SDS_Vault).Position, ((Building_Storage)building_SDS_Vault).GetSlotGroup()))
			{
				TryRelocate(map, building_SDS_Vault, preferredCell);
				SDS_MirrorService.NotifyVaultRelocated(map);
			}
			else
			{
				RepairEdificeRegistration(map, building_SDS_Vault);
			}
			cell = ((Thing)building_SDS_Vault).Position;
			return building_SDS_Vault;
		}
		ThingDef val = VaultDef;
		if (val == null)
		{
			SDS_Diag.Fail("金库/Def缺失", SDS_Diag.Ctx(map, null, null, null, "defName=SDS_HiddenVault"));
			return null;
		}
		IntVec3 val2 = FindVaultCell(map, preferredCell);
		if (!((IntVec3)(ref val2)).IsValid)
		{
			SDS_Diag.Fail("金库/全图无空格", SDS_Diag.Ctx(map, null, null, null, "preferred=" + (((IntVec3)(ref preferredCell)).IsValid ? ((object)(*(IntVec3*)(&preferredCell))/*cast due to constrained. prefix*/).ToString() : "无效")));
			return null;
		}
		if (!(ThingMaker.MakeThing(val, (ThingDef)null) is Building_SDS_Vault building_SDS_Vault2))
		{
			SDS_Diag.Fail("金库/创建失败", SDS_Diag.Ctx(map, null, null, null, "defName=SDS_HiddenVault"));
			return null;
		}
		GenSpawn.Spawn((Thing)(object)building_SDS_Vault2, val2, map, (WipeMode)0);
		if (((Thing)building_SDS_Vault2).Destroyed || !((Thing)building_SDS_Vault2).Spawned)
		{
			IntVec3 val3 = val2;
			SDS_Diag.Fail("金库/落子失败", SDS_Diag.Ctx(map, null, (Thing)(object)building_SDS_Vault2, null, "target=" + ((object)(*(IntVec3*)(&val3))/*cast due to constrained. prefix*/).ToString()));
			return null;
		}
		cell = ((Thing)building_SDS_Vault2).Position;
		return building_SDS_Vault2;
	}

	private unsafe static void RepairEdificeRegistration(Map map, Building_SDS_Vault vault)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || map.edificeGrid == null || vault == null || !((Thing)vault).Spawned)
		{
			return;
		}
		IntVec3 position = ((Thing)vault).Position;
		if ((object)map.edificeGrid[position] != vault)
		{
			Building firstBuilding = GridsUtility.GetFirstBuilding(position, map);
			if (firstBuilding != null && (object)firstBuilding != vault)
			{
				IntVec3 val = position;
				SDS_Diag.Warn("金库/登记跳过", SDS_Diag.Ctx(map, null, (Thing)(object)vault, null, "cell=" + ((object)(*(IntVec3*)(&val))/*cast due to constrained. prefix*/).ToString() + " 本格建筑=" + ((((Thing)firstBuilding).def != null) ? ((Def)((Thing)firstBuilding).def).defName : "?")));
			}
			else
			{
				map.edificeGrid.Register((Building)(object)vault);
				IntVec3 val = position;
				SDS_Diag.Warn("金库/登记自愈", SDS_Diag.Ctx(map, null, (Thing)(object)vault, null, "cell=" + ((object)(*(IntVec3*)(&val))/*cast due to constrained. prefix*/).ToString()));
			}
		}
	}

	private static bool CellOwnedByForeignGroup(Map map, IntVec3 cell, SlotGroup own)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		HaulDestinationManager val = map?.haulDestinationManager;
		if (val == null)
		{
			return false;
		}
		List<SlotGroup> allGroupsListForReading = val.AllGroupsListForReading;
		for (int i = 0; i < allGroupsListForReading.Count; i++)
		{
			SlotGroup val2 = allGroupsListForReading[i];
			if (val2 != null && val2 != own && val2.CellsList.Contains(cell))
			{
				return true;
			}
		}
		return false;
	}

	private unsafe static void TryRelocate(Map map, Building_SDS_Vault vault, IntVec3 preferredCell)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 val = FindVaultCell(map, preferredCell);
		if (!((IntVec3)(ref val)).IsValid)
		{
			SDS_Diag.Warn("金库/搬迁无空位", SDS_Diag.Ctx(map, null, (Thing)(object)vault, null, "cell=" + ((object)((Thing)vault).Position/*cast due to constrained. prefix*/).ToString()));
			return;
		}
		try
		{
			((Entity)vault).DeSpawn((DestroyMode)0);
			GenSpawn.Spawn((Thing)(object)vault, val, map, (WipeMode)0);
		}
		catch (Exception exception)
		{
			string? text = ((object)((Thing)vault).Position/*cast due to constrained. prefix*/).ToString();
			IntVec3 val2 = val;
			SDS_Diag.Fail("金库/搬迁失败", SDS_Diag.Ctx(map, null, (Thing)(object)vault, null, "from=" + text + " to=" + ((object)(*(IntVec3*)(&val2))/*cast due to constrained. prefix*/).ToString()), exception);
		}
	}

	private static IntVec3 FindVaultCell(Map map, IntVec3 preferred)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 val = ((((IntVec3)(ref preferred)).IsValid && GenGrid.InBounds(preferred, map)) ? preferred : map.Center);
		if (IsFreeCell(map, val))
		{
			return val;
		}
		foreach (IntVec3 item in GenRadial.RadialCellsAround(val, 12f, true))
		{
			if (IsFreeCell(map, item))
			{
				return item;
			}
		}
		IntVec3 result = IntVec3.Invalid;
		float num = float.MaxValue;
		foreach (IntVec3 allCell in map.AllCells)
		{
			if (IsFreeCell(map, allCell))
			{
				IntVec3 val2 = allCell - val;
				float num2 = ((IntVec3)(ref val2)).LengthHorizontalSquared;
				if (num2 < num)
				{
					result = allCell;
					num = num2;
				}
			}
		}
		return result;
	}

	private static bool IsFreeCell(Map map, IntVec3 cell)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		return ((IntVec3)(ref cell)).IsValid && GenGrid.InBounds(cell, map) && !GridsUtility.Fogged(cell, map) && GenGrid.Standable(cell, map) && GridsUtility.GetFirstBuilding(cell, map) == null && GridsUtility.GetZone(cell, map) == null && (map.haulDestinationManager == null || map.haulDestinationManager.SlotGroupAt(cell) == null);
	}

	private void EnsureStorageSettings()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		if (base.settings == null)
		{
			base.settings = new StorageSettings((IStoreSettingsParent)(object)this);
			SDS_Diag.Warn("存档/金库筛选补齐", SDS_Diag.Ctx(((Thing)this).Map, null, (Thing)(object)this, null, "旧存档缺少 settings"));
		}
	}

	public override void ExposeData()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Invalid comparison between Unknown and I4
		((Building_Storage)this).ExposeData();
		if ((int)Scribe.mode == 4)
		{
			EnsureStorageSettings();
		}
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		EnsureStorageSettings();
		((Building_Storage)this).SpawnSetup(map, respawningAfterLoad);
		if (map != null)
		{
			vaults[map] = this;
		}
	}

	public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		Map map = ((Thing)this).Map;
		if (map != null && vaults.TryGetValue(map, out var value) && value == this)
		{
			vaults.Remove(map);
		}
		if (map != null)
		{
			try
			{
				SDS_MirrorService.EvacuateForStructureChange(map, "vault-despawn");
			}
			catch (Exception exception)
			{
				SDS_Diag.ErrorOnce("镜像/结构撤离异常（已忽略，继续移除金库）", exception);
			}
		}
		((Building_Storage)this).DeSpawn(mode);
	}

	public ThingOwner GetDirectlyHeldThings()
	{
		if (emptyContents == null)
		{
			emptyContents = new ThingOwner<Thing>((IThingHolder)(object)this, false, (LookMode)2, true);
		}
		return (ThingOwner)(object)emptyContents;
	}

	public void GetChildHolders(List<IThingHolder> outChildren)
	{
		IThingHolder val = (IThingHolder)(object)SDS_ProjectionService.ProjectionFor((Thing)(object)this);
		if (val != null)
		{
			outChildren.Add(val);
		}
	}
}
[HarmonyPatch(typeof(Pawn_HealthTracker), "AddHediff", new Type[]
{
	typeof(Hediff),
	typeof(BodyPartRecord),
	typeof(DamageInfo?),
	typeof(DamageResult)
})]
internal static class Patch_SDS_EndpointChipAdded
{
	public static void Postfix(Hediff hediff)
	{
		try
		{
			if (hediff != null && (hediff.def == SDS_HediffDefOf.ChipA || hediff.def == SDS_HediffDefOf.ChipB))
			{
				SDS_EndpointResolver.MarkDirty();
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("端点注册表：芯片植入事件处理失败（已忽略）", exception);
		}
	}
}
[HarmonyPatch(typeof(Pawn), "SpawnSetup", new Type[]
{
	typeof(Map),
	typeof(bool)
})]
internal static class Patch_SDS_EndpointPawnSpawned
{
	public static void Postfix(Pawn __instance)
	{
		try
		{
			if (__instance != null)
			{
				if (SDS_ImplantUtility.EffectiveChipDef(__instance) != null)
				{
					SDS_EndpointResolver.MarkDirty();
				}
				if (((Thing)__instance).Faction == Faction.OfPlayer)
				{
					SDS_GameComponent_Grid.Instance?.NotifyPlayerPresenceChanged();
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("端点注册表：小人进图事件处理失败（已忽略）", exception);
		}
	}
}
[HarmonyPatch(typeof(Pawn), "DeSpawn", new Type[] { typeof(DestroyMode) })]
internal static class Patch_SDS_PlayerPresencePawnDespawned
{
	public static void Prefix(Pawn __instance, out bool __state)
	{
		__state = __instance != null && ((Thing)__instance).Faction == Faction.OfPlayer;
	}

	public static void Postfix(bool __state)
	{
		if (!__state)
		{
			return;
		}
		try
		{
			SDS_GameComponent_Grid.Instance?.NotifyPlayerPresenceChanged();
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("扫描地图集：小人离图事件处理失败（已忽略）", exception);
		}
	}
}
[HarmonyPatch(typeof(Pawn), "SetFaction", new Type[]
{
	typeof(Faction),
	typeof(Pawn)
})]
internal static class Patch_SDS_PlayerPresenceFactionChanged
{
	public static void Prefix(Pawn __instance, out bool __state)
	{
		__state = __instance != null && ((Thing)__instance).Faction == Faction.OfPlayer;
	}

	public static void Postfix(Pawn __instance, bool __state)
	{
		try
		{
			bool flag = __instance != null && ((Thing)__instance).Faction == Faction.OfPlayer;
			if (__state || flag)
			{
				SDS_GameComponent_Grid.Instance?.NotifyPlayerPresenceChanged();
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("扫描地图集：阵营变化事件处理失败（已忽略）", exception);
		}
	}
}
[HarmonyPatch]
internal static class Patch_SDS_EndpointTransferReturn
{
	public static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(ThingOwner), "TryTransferToContainer", new Type[5]
		{
			typeof(Thing),
			typeof(ThingOwner),
			typeof(int),
			typeof(Thing).MakeByRefType(),
			typeof(bool)
		}, (Type[])null);
	}

	public static void Prefix(Thing item, int count, out int __state)
	{
		__state = ((item != null) ? Math.Min(item.stackCount, count) : 0);
	}

	public static void Postfix(ThingOwner otherContainer, int __state, ref Thing resultingTransferredItem, ref int __result)
	{
		if (__result == 0 && __state > 0 && otherContainer is SDS_EndpointBuffer && resultingTransferredItem != null && resultingTransferredItem.Destroyed)
		{
			__result = __state;
		}
	}
}
internal static class SDS_CaravanVirtual
{
	private static readonly Dictionary<Thing, List<Thing>> preservedBuckets = new Dictionary<Thing, List<Thing>>();

	private static bool TryGetPreservedBucket(Thing display, out List<Thing> bucket)
	{
		bucket = null;
		return display != null && preservedBuckets.TryGetValue(display, out bucket) && bucket != null;
	}

	public static void Reset()
	{
		preservedBuckets.Clear();
	}

	public static void CleanupFakes(List<TransferableOneWay> transferables)
	{
		if (transferables == null)
		{
			return;
		}
		for (int i = 0; i < transferables.Count; i++)
		{
			TransferableOneWay val = transferables[i];
			if (val == null || val.things == null)
			{
				continue;
			}
			for (int num = val.things.Count - 1; num >= 0; num--)
			{
				Thing val2 = val.things[num];
				if (val2 != null && SDS_VirtualDisplayRegistry.IsFake(val2))
				{
					val.things.RemoveAt(num);
					SDS_VirtualDisplayRegistry.Forget(val2, SDS_VirtualDisplayRegistry.CaravanOwner);
				}
			}
		}
	}

	public static bool AnyVirtualNeeded(List<TransferableOneWay> transferables)
	{
		if (transferables == null)
		{
			return false;
		}
		for (int i = 0; i < transferables.Count; i++)
		{
			TransferableOneWay val = transferables[i];
			if (TryGetVirtualRowKeys(val, out var _, out var _) && ((Transferable)val).CountToTransfer > RealCount(val))
			{
				return true;
			}
		}
		return false;
	}

	public static bool ValidateQuantities(List<TransferableOneWay> transferables, out string failLabel)
	{
		failLabel = null;
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || transferables == null)
		{
			return true;
		}
		for (int i = 0; i < transferables.Count; i++)
		{
			TransferableOneWay val = transferables[i];
			if (val == null || ((Transferable)val).CountToTransfer <= 0 || !TryGetVirtualRowKeys(val, out var virtualKey, out var preservedKey))
			{
				continue;
			}
			int num = ((Transferable)val).CountToTransfer - RealCount(val);
			if (num > 0)
			{
				long num2 = 0L;
				if (virtualKey != null)
				{
					num2 += SDS_Availability.GetAvailableCount(instance, virtualKey);
				}
				if (preservedKey != null)
				{
					num2 += SDS_Availability.GetAvailableCount(instance, preservedKey);
				}
				if (num2 < num)
				{
					failLabel = ((Transferable)val).Label;
					return false;
				}
			}
		}
		return true;
	}

	private static bool TryGetVirtualRowKeys(TransferableOneWay t, out SDS_StockKey virtualKey, out SDS_StockKey preservedKey)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		virtualKey = null;
		preservedKey = null;
		if (t == null || t.things == null || ((Transferable)t).ThingDef == null || (int)((Transferable)t).ThingDef.category != 2)
		{
			return false;
		}
		for (int i = 0; i < t.things.Count; i++)
		{
			Thing val = t.things[i];
			if (val == null || (!SDS_VirtualDisplayRegistry.IsFake(val) && !SDS_OutputUtility.TryGetDisplayKey(val, out var _)))
			{
				continue;
			}
			if (TryGetPreservedBucket(val, out var bucket))
			{
				if (preservedKey == null)
				{
					preservedKey = SDS_OutputUtility.KeyOfDisplay(val, bucket);
				}
			}
			else if (virtualKey == null)
			{
				virtualKey = SDS_OutputUtility.KeyOfDisplay(val, null);
			}
		}
		return virtualKey != null || preservedKey != null;
	}

	public static int RealCount(TransferableOneWay t)
	{
		if (t == null || t.things == null)
		{
			return 0;
		}
		long num = 0L;
		for (int i = 0; i < t.things.Count; i++)
		{
			Thing val = t.things[i];
			if (val != null && val.holdingOwner == null && !SDS_VirtualDisplayRegistry.IsFake(val))
			{
				num += val.stackCount;
				if (num >= int.MaxValue)
				{
					return int.MaxValue;
				}
			}
		}
		return (int)num;
	}

	public static void Inject(List<TransferableOneWay> transferables)
	{
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Invalid comparison between Unknown and I4
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Expected O, but got Unknown
		if (transferables == null)
		{
			return;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			return;
		}
		List<SDS_StockEntry> list = instance.Snapshot(0);
		for (int i = 0; i < list.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = list[i];
			if (sDS_StockEntry == null || sDS_StockEntry.key == null || sDS_StockEntry.count <= 0)
			{
				continue;
			}
			ThingDef thingDef = sDS_StockEntry.key.thingDef;
			if (thingDef == null || (int)thingDef.category != 2 || !thingDef.canLoadIntoCaravan)
			{
				continue;
			}
			long availableCount = SDS_Availability.GetAvailableCount(instance, sDS_StockEntry.key);
			if (availableCount <= 0)
			{
				continue;
			}
			Thing val = SDS_OutputUtility.CachedDisplayThing(sDS_StockEntry.key);
			if (val == null)
			{
				continue;
			}
			TransferableOneWay val2 = TransferableUtility.TransferableMatching<TransferableOneWay>(val, transferables, (TransferAsOneMode)1);
			long val3 = Math.Max(0L, 2147483647L - (long)((val2 != null) ? val2.MaxCount : 0));
			int num = (int)Math.Min(availableCount, val3);
			if (num > 0)
			{
				val.stackCount = num;
				if (val2 == null)
				{
					val2 = new TransferableOneWay();
					transferables.Add(val2);
				}
				val2.things.Add(val);
				SDS_VirtualDisplayRegistry.Register(val, SDS_VirtualDisplayRegistry.CaravanOwner);
			}
		}
		InjectPreserved(transferables, instance);
	}

	private static void InjectPreserved(List<TransferableOneWay> transferables, SDS_GameComponent_Grid grid)
	{
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Expected O, but got Unknown
		preservedBuckets.Clear();
		List<SDS_PreservedBucket> list = SDS_PreservedBuckets.Build(grid, delegate(Thing thing)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Invalid comparison between Unknown and I4
			Thing innerIfMinified = MinifyUtility.GetInnerIfMinified(thing);
			return thing.def != null && (int)thing.def.category == 2 && innerIfMinified != null && innerIfMinified.def != null && innerIfMinified.def.canLoadIntoCaravan;
		}, tradeGrouping: false, null, tradableBudget: false);
		for (int num = 0; num < list.Count; num++)
		{
			SDS_PreservedBucket sDS_PreservedBucket = list[num];
			if (sDS_PreservedBucket == null || sDS_PreservedBucket.display == null || sDS_PreservedBucket.units <= 0)
			{
				continue;
			}
			TransferableOneWay val = TransferableUtility.TransferableMatching<TransferableOneWay>(sDS_PreservedBucket.display, transferables, (TransferAsOneMode)1);
			long val2 = Math.Max(0L, 2147483647L - (long)((val != null) ? val.MaxCount : 0));
			int num2 = (int)Math.Min(sDS_PreservedBucket.units, val2);
			if (num2 > 0)
			{
				sDS_PreservedBucket.display.stackCount = num2;
				if (val == null)
				{
					val = new TransferableOneWay();
					transferables.Add(val);
				}
				val.things.Add(sDS_PreservedBucket.display);
				SDS_VirtualDisplayRegistry.Register(sDS_PreservedBucket.display, SDS_VirtualDisplayRegistry.CaravanOwner);
				preservedBuckets[sDS_PreservedBucket.display] = sDS_PreservedBucket.things;
			}
		}
	}

	public static void MaterializeForCaravan(Lord lord, LordJob_FormAndSendCaravan lordJob, Map map, IEnumerable<Pawn> startingPawns)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Invalid comparison between Unknown and I4
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0382: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (lord == null || lordJob == null || lordJob.transferables == null || map == null || instance == null)
		{
			return;
		}
		IntVec3 reference = CaravanReferenceCell(lord, startingPawns);
		IntVec3 cell;
		bool flag = SDS_OutputUtility.TryFindEndpointOutputCell(map, reference, out cell);
		bool flag2 = SDS_OutputUtility.MapHasOperationalEndpoint(map);
		List<Pawn> list = CollectMembers(lord, startingPawns);
		List<TransferableOneWay> transferables = lordJob.transferables;
		for (int i = 0; i < transferables.Count; i++)
		{
			TransferableOneWay val = transferables[i];
			if (val == null || ((Transferable)val).ThingDef == null || (int)((Transferable)val).ThingDef.category != 2)
			{
				continue;
			}
			SDS_StockKey key = null;
			List<Thing> bucket = null;
			for (int num = val.things.Count - 1; num >= 0; num--)
			{
				Thing val2 = val.things[num];
				if (val2 != null && SDS_VirtualDisplayRegistry.IsFake(val2))
				{
					if (TryGetPreservedBucket(val2, out var bucket2))
					{
						if (bucket == null)
						{
							bucket = bucket2;
						}
					}
					else if (key == null)
					{
						key = SDS_OutputUtility.KeyOfDisplay(val2, null);
					}
					val.things.RemoveAt(num);
					SDS_VirtualDisplayRegistry.Forget(val2, SDS_VirtualDisplayRegistry.CaravanOwner);
				}
			}
			if (bucket == null && key == null && ((Transferable)val).AnyThing != null && !TryGetPreservedBucket(((Transferable)val).AnyThing, out bucket) && !SDS_OutputUtility.TryGetDisplayKey(((Transferable)val).AnyThing, out key))
			{
				key = SDS_StockKey.FromAnyThing(((Transferable)val).AnyThing);
			}
			if (bucket == null && key == null && ((Transferable)val).ThingDef != null)
			{
				key = instance.FindWithdrawableKey(((Transferable)val).ThingDef);
			}
			if (bucket == null && key != null && key.preserved)
			{
				List<Thing> list2 = new List<Thing>();
				instance.AppendPreservedThings(list2);
				List<Thing> list3 = new List<Thing>();
				for (int j = 0; j < list2.Count; j++)
				{
					Thing val3 = list2[j];
					if (val3 != null && !val3.Destroyed)
					{
						SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(val3);
						if (sDS_StockKey != null && sDS_StockKey.SameAs(key))
						{
							list3.Add(val3);
						}
					}
				}
				if (list3.Count > 0)
				{
					bucket = list3;
				}
			}
			int num2 = ((Transferable)val).CountToTransfer - RealCount(val);
			if (num2 > 0 && key != null && !key.preserved)
			{
				int num3 = ((flag2 && list.Count > 0) ? MaterializeIntoMembers(instance, key, num2, list) : 0);
				if (num3 < num2 && flag)
				{
					MaterializeInto(map, cell, instance, key, num2 - num3, val, lord);
				}
				if (num3 > 0)
				{
					((Transferable)val).AdjustTo(Math.Max(0, ((Transferable)val).CountToTransfer - num3));
				}
			}
			num2 = ((Transferable)val).CountToTransfer - RealCount(val);
			if (num2 > 0 && bucket != null)
			{
				int num4 = ((flag2 && list.Count > 0) ? MaterializePreservedBucketIntoMembers(instance, bucket, num2, list) : 0);
				if (num4 < num2 && flag)
				{
					MaterializePreservedBucketInto(map, cell, instance, bucket, num2 - num4, val, lord);
				}
				if (num4 > 0)
				{
					((Transferable)val).AdjustTo(Math.Max(0, ((Transferable)val).CountToTransfer - num4));
				}
			}
			ClampToReal(val);
		}
	}

	private static int MaterializeInto(Map map, IntVec3 center, SDS_GameComponent_Grid grid, SDS_StockKey key, int deficit, TransferableOneWay t, Lord lord)
	{
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		deficit = ClampDeficitToAvailable(grid, key, deficit);
		if (deficit <= 0)
		{
			return 0;
		}
		if (!SDS_WithdrawExecutor.TryWithdrawVirtual(key, deficit, out var made) || made == null || made.Count == 0)
		{
			SDS_Diag.Fail("编成/取货物化失败", SDS_Diag.Ctx(map, null, null, null, "def=" + ((key.thingDef != null) ? ((Def)key.thingDef).defName : "?") + " deficit=" + deficit));
			return 0;
		}
		int num = 0;
		for (int i = 0; i < made.Count; i++)
		{
			Thing val = made[i];
			if (val != null && !val.Destroyed && SDS_PlacementUtility.TryPlace(val, center, map, SDS_PlacementPolicy.Exact, out var placed) && placed != null && !placed.Destroyed)
			{
				CompForbiddable val2 = ThingCompUtility.TryGetComp<CompForbiddable>(placed);
				if (val2 != null)
				{
					val2.Forbidden = true;
				}
				if (!t.things.Contains(placed))
				{
					t.things.Add(placed);
				}
				SDS_ReclaimRegistry.RegisterProtectedOutput(placed, lord);
				num += placed.stackCount;
			}
		}
		return num;
	}

	private static List<Pawn> CollectMembers(Lord lord, IEnumerable<Pawn> startingPawns)
	{
		List<Pawn> list = new List<Pawn>();
		object obj = startingPawns;
		if (obj == null)
		{
			if (lord == null)
			{
				obj = null;
			}
			else
			{
				IEnumerable<Pawn> ownedPawns = lord.ownedPawns;
				obj = ownedPawns;
			}
		}
		IEnumerable<Pawn> enumerable = (IEnumerable<Pawn>)obj;
		if (enumerable == null)
		{
			return list;
		}
		foreach (Pawn item in enumerable)
		{
			if (item != null && !((Thing)item).Destroyed && item.inventory != null && ((Thing)item).Spawned && ((Thing)item).Map != null && SDS_ImplantUtility.IsPortOperational(item))
			{
				list.Add(item);
			}
		}
		return list;
	}

	private static IntVec3 CaravanReferenceCell(Lord lord, IEnumerable<Pawn> startingPawns)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		object obj = startingPawns;
		if (obj == null)
		{
			if (lord == null)
			{
				obj = null;
			}
			else
			{
				IEnumerable<Pawn> ownedPawns = lord.ownedPawns;
				obj = ownedPawns;
			}
		}
		IEnumerable<Pawn> enumerable = (IEnumerable<Pawn>)obj;
		if (enumerable == null)
		{
			return IntVec3.Invalid;
		}
		int num = 0;
		float num2 = 0f;
		float num3 = 0f;
		bool flag = false;
		IntVec3 result = IntVec3.Invalid;
		foreach (Pawn item in enumerable)
		{
			if (item != null && ((Thing)item).Spawned && ((Thing)item).Map != null)
			{
				if (!flag)
				{
					result = ((Thing)item).Position;
					flag = true;
				}
				num2 += (float)((Thing)item).Position.x;
				num3 += (float)((Thing)item).Position.z;
				num++;
			}
		}
		if (num == 0)
		{
			return result;
		}
		return new IntVec3((int)Math.Round(num2 / (float)num), 0, (int)Math.Round(num3 / (float)num));
	}

	private static int MaterializeIntoMembers(SDS_GameComponent_Grid grid, SDS_StockKey key, int deficit, List<Pawn> members)
	{
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		if (grid == null || key == null || deficit <= 0 || members == null || members.Count == 0)
		{
			return 0;
		}
		deficit = ClampDeficitToAvailable(grid, key, deficit);
		if (deficit <= 0)
		{
			return 0;
		}
		if (!SDS_WithdrawExecutor.TryWithdrawVirtual(key, deficit, out var made) || made == null || made.Count == 0)
		{
			SDS_Diag.Fail("编成/取货物化失败", SDS_Diag.Ctx(null, (members != null && members.Count > 0) ? members[0] : null, null, null, "def=" + ((key.thingDef != null) ? ((Def)key.thingDef).defName : "?") + " deficit=" + deficit));
			return 0;
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < made.Count; i++)
		{
			Thing val = made[i];
			if (val == null || val.Destroyed)
			{
				continue;
			}
			int stackCount = val.stackCount;
			bool flag = false;
			for (int j = 0; j < members.Count; j++)
			{
				Pawn val2 = members[num2];
				num2 = (num2 + 1) % members.Count;
				if (val2 != null && !((Thing)val2).Destroyed && val2.inventory != null && ((ThingOwner)val2.inventory.innerContainer).TryAdd(val, true))
				{
					num += stackCount;
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				Pawn val3 = FirstSpawned(members);
				Map map = ((val3 != null) ? ((Thing)val3).Map : Find.AnyPlayerHomeMap);
				IntVec3 cell = ((val3 != null) ? ((Thing)val3).Position : IntVec3.Invalid);
				if (SDS_ReturnUtility.ReturnVirtual(val, key, map, cell) != SDS_ReclaimOutcome.Returned)
				{
					SDS_Diag.Fail("编成/虚拟回滚失败", SDS_Diag.Ctx(map, val3, val, null, "def=" + ((key.thingDef != null) ? ((Def)key.thingDef).defName : "?") + " amount=" + stackCount));
				}
			}
		}
		return num;
	}

	private static int MaterializePreservedBucketInto(Map map, IntVec3 center, SDS_GameComponent_Grid grid, List<Thing> bucket, int deficit, TransferableOneWay t, Lord lord)
	{
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		int preservedAvailableCount = grid.GetPreservedAvailableCount(BucketKey(bucket));
		if (preservedAvailableCount <= 0)
		{
			return 0;
		}
		deficit = Math.Min(deficit, preservedAvailableCount);
		int plannedUnits = 0;
		List<ThingCount> list = SDS_PreservedBuckets.TakePlan(grid, bucket, deficit, out plannedUnits);
		if (plannedUnits <= 0 || list.Count == 0)
		{
			SDS_Diag.Warn("编成/保真无可用计划", SDS_Diag.Ctx(map, null, null, null, "deficit=" + deficit + " available=" + preservedAvailableCount));
			return 0;
		}
		if (!SDS_WithdrawExecutor.TryWithdrawPreservedExact(list, out var made) || made == null || made.Count == 0)
		{
			SDS_Diag.Fail("编成/保真物化失败", SDS_Diag.Ctx(map, null, null, null, "planned=" + plannedUnits + " plan=" + list.Count));
			return 0;
		}
		int num = 0;
		for (int i = 0; i < made.Count; i++)
		{
			Thing val = made[i];
			if (val != null && !val.Destroyed && SDS_PlacementUtility.TryPlace(val, center, map, SDS_PlacementPolicy.Exact, out var placed) && placed != null && !placed.Destroyed)
			{
				CompForbiddable val2 = ThingCompUtility.TryGetComp<CompForbiddable>(placed);
				if (val2 != null)
				{
					val2.Forbidden = true;
				}
				if (!t.things.Contains(placed))
				{
					t.things.Add(placed);
				}
				SDS_ReclaimRegistry.RegisterProtectedOutput(placed, lord);
				num += placed.stackCount;
			}
		}
		return num;
	}

	private static int MaterializePreservedBucketIntoMembers(SDS_GameComponent_Grid grid, List<Thing> bucket, int deficit, List<Pawn> members)
	{
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		if (grid == null || bucket == null || deficit <= 0 || members == null || members.Count == 0)
		{
			return 0;
		}
		int preservedAvailableCount = grid.GetPreservedAvailableCount(BucketKey(bucket));
		if (preservedAvailableCount <= 0)
		{
			return 0;
		}
		deficit = Math.Min(deficit, preservedAvailableCount);
		int plannedUnits = 0;
		List<ThingCount> list = SDS_PreservedBuckets.TakePlan(grid, bucket, deficit, out plannedUnits);
		if (plannedUnits <= 0 || list.Count == 0)
		{
			SDS_Diag.Warn("编成/保真无可用计划", SDS_Diag.Ctx(null, (members != null && members.Count > 0) ? members[0] : null, null, null, "deficit=" + deficit + " available=" + preservedAvailableCount));
			return 0;
		}
		if (!SDS_WithdrawExecutor.TryWithdrawPreservedExact(list, out var made) || made == null || made.Count == 0)
		{
			SDS_Diag.Fail("编成/保真物化失败", SDS_Diag.Ctx(null, (members != null && members.Count > 0) ? members[0] : null, null, null, "planned=" + plannedUnits + " plan=" + list.Count));
			return 0;
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < made.Count; i++)
		{
			Thing val = made[i];
			if (val == null || val.Destroyed)
			{
				continue;
			}
			int stackCount = val.stackCount;
			bool flag = false;
			for (int j = 0; j < members.Count; j++)
			{
				Pawn val2 = members[num2];
				num2 = (num2 + 1) % members.Count;
				if (val2 != null && !((Thing)val2).Destroyed && val2.inventory != null && ((ThingOwner)val2.inventory.innerContainer).TryAdd(val, true))
				{
					num += stackCount;
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				Pawn val3 = FirstSpawned(members);
				SDS_ReclaimOutcome sDS_ReclaimOutcome = SDS_ReturnUtility.ReturnPreserved(val, (val3 != null) ? ((Thing)val3).Map : null, (val3 != null) ? ((Thing)val3).Position : IntVec3.Invalid);
				if (sDS_ReclaimOutcome != SDS_ReclaimOutcome.Returned)
				{
					SDS_Diag.Fail("编成/保真回滚失败", SDS_Diag.Ctx((val3 != null) ? ((Thing)val3).Map : null, val3, val, null, "def=" + ((val.def != null) ? ((Def)val.def).defName : "?")));
				}
			}
		}
		return num;
	}

	private static Pawn FirstSpawned(List<Pawn> members)
	{
		for (int i = 0; i < members.Count; i++)
		{
			Pawn val = members[i];
			if (val != null && !((Thing)val).Destroyed && ((Thing)val).Spawned && ((Thing)val).Map != null)
			{
				return val;
			}
		}
		return null;
	}

	private static void ClampToReal(TransferableOneWay t)
	{
		int maxCount = t.MaxCount;
		if (((Transferable)t).CountToTransfer > maxCount)
		{
			((Transferable)t).AdjustTo(maxCount);
		}
	}

	private static int ClampDeficitToAvailable(SDS_GameComponent_Grid grid, SDS_StockKey key, int deficit)
	{
		if (grid == null || key == null || deficit <= 0)
		{
			return 0;
		}
		long availableCount = SDS_Availability.GetAvailableCount(grid, key);
		if (availableCount <= 0)
		{
			return 0;
		}
		return (int)Math.Min(deficit, Math.Min(availableCount, 2147483647L));
	}

	private static SDS_StockKey BucketKey(List<Thing> bucket)
	{
		if (bucket == null)
		{
			return null;
		}
		for (int i = 0; i < bucket.Count; i++)
		{
			Thing val = bucket[i];
			if (val != null && !val.Destroyed)
			{
				return SDS_StockKey.FromPreservedThing(val);
			}
		}
		return null;
	}

	public static void RemoveFromCaravan(Lord lord, Thing thing)
	{
		if (lord == null || thing == null)
		{
			return;
		}
		LordJob lordJob = lord.LordJob;
		LordJob_FormAndSendCaravan val = (LordJob_FormAndSendCaravan)(object)((lordJob is LordJob_FormAndSendCaravan) ? lordJob : null);
		if (val == null || val.transferables == null)
		{
			return;
		}
		List<TransferableOneWay> transferables = val.transferables;
		for (int i = 0; i < transferables.Count; i++)
		{
			TransferableOneWay val2 = transferables[i];
			if (val2 != null && val2.things != null && val2.things.Remove(thing))
			{
				ClampToReal(val2);
			}
		}
	}
}
[HarmonyPatch(typeof(Dialog_FormCaravan), "CalculateAndRecacheTransferables")]
internal static class Patch_SDS_CaravanRecacheReset
{
	public static void Prefix(Dialog_FormCaravan __instance)
	{
		try
		{
			SDS_CaravanVirtual.CleanupFakes(__instance?.transferables);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("远征队展示物清理失败（已忽略）", exception);
		}
	}
}
[HarmonyPatch(typeof(Dialog_FormCaravan), "AddItemsToTransferables")]
internal static class Patch_SDS_CaravanAddVirtual
{
	public static void Postfix(Dialog_FormCaravan __instance, Map ___map, bool ___reform)
	{
		try
		{
			if (!(__instance == null || ___reform) && SDS_OutputUtility.MapHasOperationalEndpoint(___map))
			{
				SDS_CaravanVirtual.Inject(__instance.transferables);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("远征队超维空间品类注入失败（已忽略，原版编成不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(Dialog_FormCaravan), "TryFormAndSendCaravan")]
internal static class Patch_SDS_CaravanValidate
{
	public static bool Prefix(Dialog_FormCaravan __instance, Map ___map, ref bool __result)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__instance == null || !SDS_CaravanVirtual.AnyVirtualNeeded(__instance.transferables))
			{
				return true;
			}
			if (!SDS_OutputUtility.MapHasOperationalEndpoint(___map))
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("SDS.Caravan.NeedsOutputPoint")), MessageTypeDefOf.RejectInput, false);
				__result = false;
				return false;
			}
			if (!SDS_CaravanVirtual.ValidateQuantities(__instance.transferables, out var failLabel))
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SDS.Caravan.NotEnoughStock", NamedArgument.op_Implicit(failLabel ?? "?"))), MessageTypeDefOf.RejectInput, false);
				__result = false;
				return false;
			}
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("远征队端点校验失败（已忽略，放行原版出发）", exception);
			return true;
		}
	}
}
[HarmonyPatch(typeof(Dialog_FormCaravan), "DebugTryFormCaravanInstantly")]
internal static class Patch_SDS_CaravanBlockInstant
{
	public static bool Prefix(Dialog_FormCaravan __instance, ref bool __result)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__instance == null || !SDS_CaravanVirtual.AnyVirtualNeeded(__instance.transferables))
			{
				return true;
			}
			Messages.Message(TaggedString.op_Implicit(Translator.Translate("SDS.Caravan.InstantUnsupported")), MessageTypeDefOf.RejectInput, false);
			__result = false;
			return false;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("远征队瞬间编成拦截失败（已忽略）", exception);
			return true;
		}
	}
}
[HarmonyPatch(typeof(Dialog_FormCaravan), "PostClose")]
internal static class Patch_SDS_CaravanPostCloseCleanup
{
	public static void Postfix(Dialog_FormCaravan __instance)
	{
		try
		{
			if (__instance != null && !__instance.choosingRoute)
			{
				SDS_CaravanVirtual.CleanupFakes(__instance.transferables);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("远征队收尾清理失败（已忽略）", exception);
		}
	}
}
[HarmonyPatch(typeof(LordMaker), "MakeNewLord")]
internal static class Patch_SDS_CaravanMaterialize
{
	public static void Postfix(LordJob lordJob, Map map, IEnumerable<Pawn> startingPawns, Lord __result)
	{
		try
		{
			LordJob_FormAndSendCaravan val = (LordJob_FormAndSendCaravan)(object)((lordJob is LordJob_FormAndSendCaravan) ? lordJob : null);
			if (val != null)
			{
				SDS_CaravanVirtual.MaterializeForCaravan(__result, val, map, startingPawns);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("远征队超维空间物资物化失败（已忽略）", exception);
		}
	}
}
internal static class SDS_TransportVirtual
{
	private static readonly Dictionary<Thing, List<Thing>> preservedBuckets = new Dictionary<Thing, List<Thing>>();

	private static readonly Dictionary<int, List<ThingCount>> pendingLoadPlans = new Dictionary<int, List<ThingCount>>();

	private static readonly Dictionary<int, SDS_StockKey> pendingLoadKeys = new Dictionary<int, SDS_StockKey>();

	private const int LoadFailureCooldownTicks = 250;

	private static readonly Dictionary<long, int> loadFailureUntil = new Dictionary<long, int>();

	internal static void RegisterPreservedBucket(Thing display, List<Thing> bucket)
	{
		if (display != null && bucket != null)
		{
			preservedBuckets[display] = bucket;
		}
	}

	internal static bool TryGetPreservedBucket(Thing display, out List<Thing> bucket)
	{
		bucket = null;
		return display != null && preservedBuckets.TryGetValue(display, out bucket) && bucket != null;
	}

	public static void Reset()
	{
		pendingLoadKeys.Clear();
		pendingLoadPlans.Clear();
		SDS_VirtualDisplayRegistry.Cleanup(SDS_VirtualDisplayRegistry.TransportOwner);
		preservedBuckets.Clear();
	}

	public static bool IsVirtualDisplay(Thing thing)
	{
		if (thing == null || thing.Destroyed)
		{
			return false;
		}
		if (SDS_VirtualDisplayRegistry.IsFake(thing))
		{
			return true;
		}
		return !thing.Spawned && thing.ParentHolder == null && !(thing is Pawn);
	}

	public static int UnloadedRealUnits(TransferableOneWay t, Thing container)
	{
		if (t == null || t.things == null)
		{
			return 0;
		}
		ThingOwner val = HeldBy(container);
		long num = 0L;
		for (int i = 0; i < t.things.Count; i++)
		{
			Thing val2 = t.things[i];
			if (val2 != null && !val2.Destroyed && !IsVirtualDisplay(val2) && (val == null || val2.holdingOwner != val))
			{
				num += val2.stackCount;
				if (num >= int.MaxValue)
				{
					return int.MaxValue;
				}
			}
		}
		return (int)num;
	}

	private static ThingOwner HeldBy(Thing container)
	{
		MapPortal val = (MapPortal)(object)((container is MapPortal) ? container : null);
		if (val != null)
		{
			return (ThingOwner)(object)val.containerProxy;
		}
		return ((container != null) ? ThingCompUtility.TryGetComp<CompTransporter>(container) : null)?.innerContainer;
	}

	private static Dictionary<TransferableOneWay, int> BuildInFlight(Thing container, List<TransferableOneWay> leftToLoad)
	{
		if (container == null || leftToLoad == null || container.Map == null)
		{
			return null;
		}
		Dictionary<TransferableOneWay, int> dictionary = null;
		IReadOnlyList<Pawn> allPawnsSpawned = container.Map.mapPawns.AllPawnsSpawned;
		for (int i = 0; i < allPawnsSpawned.Count; i++)
		{
			Pawn val = allPawnsSpawned[i];
			if (val == null || val.jobs == null)
			{
				continue;
			}
			JobDef curJobDef = val.CurJobDef;
			if (curJobDef != JobDefOf.HaulToTransporter && curJobDef != JobDefOf.HaulToPortal)
			{
				continue;
			}
			JobDriver curDriver = val.jobs.curDriver;
			JobDriver_HaulToContainer val2 = (JobDriver_HaulToContainer)(object)((curDriver is JobDriver_HaulToContainer) ? curDriver : null);
			if (val2 == null || val2.Container != container)
			{
				continue;
			}
			Thing thingToCarry = val2.ThingToCarry;
			if (thingToCarry == null)
			{
				continue;
			}
			TransferableOneWay val3 = TransferableUtility.TransferableMatchingDesperate(thingToCarry, leftToLoad, (TransferAsOneMode)1);
			if (val3 == null)
			{
				continue;
			}
			int num = 0;
			JobDriver_HaulToTransporter val4 = (JobDriver_HaulToTransporter)(object)((val2 is JobDriver_HaulToTransporter) ? val2 : null);
			if (val4 != null)
			{
				num = val4.initialCount;
			}
			else
			{
				JobDriver_HaulToPortal val5 = (JobDriver_HaulToPortal)(object)((val2 is JobDriver_HaulToPortal) ? val2 : null);
				if (val5 != null)
				{
					num = val5.initialCount;
				}
			}
			if (num > 0)
			{
				if (dictionary == null)
				{
					dictionary = new Dictionary<TransferableOneWay, int>();
				}
				dictionary.TryGetValue(val3, out var value);
				dictionary[val3] = value + num;
			}
		}
		return dictionary;
	}

	public static bool LeftToLoadHasFake(List<TransferableOneWay> leftToLoad)
	{
		if (leftToLoad == null)
		{
			return false;
		}
		for (int i = 0; i < leftToLoad.Count; i++)
		{
			TransferableOneWay val = leftToLoad[i];
			if (val == null || val.things == null)
			{
				continue;
			}
			for (int j = 0; j < val.things.Count; j++)
			{
				if (IsVirtualDisplay(val.things[j]))
				{
					return true;
				}
			}
		}
		return false;
	}

	public static void InjectIntoTransporters(List<TransferableOneWay> transferables, List<CompTransporter> transporters, Map map)
	{
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		if (transferables == null || transporters == null || transporters.Count == 0 || map == null)
		{
			return;
		}
		bool flag = false;
		for (int i = 0; i < transporters.Count; i++)
		{
			CompTransporter val = transporters[i];
			if (val != null && LeftToLoadHasFake(val.leftToLoad))
			{
				flag = true;
				break;
			}
		}
		if (flag)
		{
			Thing val2 = (Thing)(object)((transporters[0] != null) ? ((ThingComp)transporters[0]).parent : null);
			IntVec3 displayCell = IntVec3.Invalid;
			if (val2 != null)
			{
				displayCell = (GenGrid.InBounds(val2.InteractionCell, map) ? val2.InteractionCell : val2.Position);
			}
			for (int j = 0; j < transporters.Count; j++)
			{
				CompTransporter val3 = transporters[j];
				if (val3 != null)
				{
					InjectExistingRows(transferables, val3.leftToLoad, displayCell);
				}
			}
		}
		else
		{
			Reset();
			Thing val4 = (Thing)(object)((transporters[0] != null) ? ((ThingComp)transporters[0]).parent : null);
			IntVec3 displayCell2 = IntVec3.Invalid;
			if (val4 != null)
			{
				displayCell2 = (GenGrid.InBounds(val4.InteractionCell, map) ? val4.InteractionCell : val4.Position);
			}
			Inject(transferables, map, displayCell2);
		}
	}

	public static void InjectIntoPortal(List<TransferableOneWay> transferables, MapPortal portal)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		if (transferables != null && portal != null && ((Thing)portal).Map != null)
		{
			if (LeftToLoadHasFake(portal.leftToLoad))
			{
				InjectExistingRows(transferables, portal.leftToLoad, IntVec3.Invalid);
				return;
			}
			Reset();
			Inject(transferables, ((Thing)portal).Map, IntVec3.Invalid);
		}
	}

	private static void InjectExistingRows(List<TransferableOneWay> transferables, List<TransferableOneWay> rows, IntVec3 displayCell)
	{
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Expected O, but got Unknown
		if (transferables == null || rows == null)
		{
			return;
		}
		for (int i = 0; i < rows.Count; i++)
		{
			TransferableOneWay val = rows[i];
			if (val == null || ((Transferable)val).CountToTransfer <= 0 || val.things == null)
			{
				continue;
			}
			Thing val2 = null;
			for (int j = 0; j < val.things.Count; j++)
			{
				if (IsVirtualDisplay(val.things[j]))
				{
					val2 = val.things[j];
					break;
				}
			}
			if (val2 != null)
			{
				if (((IntVec3)(ref displayCell)).IsValid)
				{
					val2.Position = displayCell;
				}
				TransferableOneWay val3 = TransferableUtility.TransferableMatching<TransferableOneWay>(val2, transferables, (TransferAsOneMode)1);
				if (val3 == null)
				{
					val3 = new TransferableOneWay();
					transferables.Add(val3);
				}
				if (!val3.things.Contains(val2))
				{
					val3.things.Add(val2);
				}
			}
		}
	}

	private static void Inject(List<TransferableOneWay> transferables, Map map, IntVec3 displayCell)
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Invalid comparison between Unknown and I4
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d8: Expected O, but got Unknown
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Expected O, but got Unknown
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available || !SDS_OutputUtility.MapHasOperationalEndpoint(map))
		{
			return;
		}
		List<SDS_StockEntry> list = instance.Snapshot(0);
		for (int i = 0; i < list.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = list[i];
			if (sDS_StockEntry == null || sDS_StockEntry.key == null || sDS_StockEntry.count <= 0)
			{
				continue;
			}
			ThingDef thingDef = sDS_StockEntry.key.thingDef;
			if (thingDef == null || (int)thingDef.category != 2 || !thingDef.canLoadIntoCaravan)
			{
				continue;
			}
			long availableCount = SDS_Availability.GetAvailableCount(instance, sDS_StockEntry.key);
			if (availableCount <= 0)
			{
				continue;
			}
			Thing val = SDS_OutputUtility.CachedDisplayThing(sDS_StockEntry.key);
			if (val == null)
			{
				continue;
			}
			TransferableOneWay val2 = TransferableUtility.TransferableMatching<TransferableOneWay>(val, transferables, (TransferAsOneMode)1);
			long val3 = Math.Max(0L, 2147483647L - (long)((val2 != null) ? val2.MaxCount : 0));
			long num = Math.Min(availableCount, val3);
			if (num > 0)
			{
				val.stackCount = (int)((num > int.MaxValue) ? int.MaxValue : num);
				if (((IntVec3)(ref displayCell)).IsValid)
				{
					val.Position = displayCell;
				}
				if (val2 == null)
				{
					val2 = new TransferableOneWay();
					transferables.Add(val2);
				}
				if (!val2.things.Contains(val))
				{
					val2.things.Add(val);
				}
				SDS_VirtualDisplayRegistry.Register(val, SDS_VirtualDisplayRegistry.TransportOwner);
			}
		}
		List<SDS_PreservedBucket> list2 = SDS_PreservedBuckets.Build(instance, delegate(Thing thing)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Invalid comparison between Unknown and I4
			Thing innerIfMinified = MinifyUtility.GetInnerIfMinified(thing);
			return thing.def != null && (int)thing.def.category == 2 && innerIfMinified != null && innerIfMinified.def != null && innerIfMinified.def.canLoadIntoCaravan;
		}, tradeGrouping: false, null, tradableBudget: false);
		for (int num2 = 0; num2 < list2.Count; num2++)
		{
			SDS_PreservedBucket sDS_PreservedBucket = list2[num2];
			if (sDS_PreservedBucket == null || sDS_PreservedBucket.display == null || sDS_PreservedBucket.units <= 0)
			{
				continue;
			}
			TransferableOneWay val4 = TransferableUtility.TransferableMatching<TransferableOneWay>(sDS_PreservedBucket.display, transferables, (TransferAsOneMode)1);
			long val5 = Math.Max(0L, 2147483647L - (long)((val4 != null) ? val4.MaxCount : 0));
			long num3 = Math.Min(sDS_PreservedBucket.units, val5);
			if (num3 > 0)
			{
				sDS_PreservedBucket.display.stackCount = (int)((num3 > int.MaxValue) ? int.MaxValue : num3);
				if (((IntVec3)(ref displayCell)).IsValid)
				{
					sDS_PreservedBucket.display.Position = displayCell;
				}
				if (val4 == null)
				{
					val4 = new TransferableOneWay();
					transferables.Add(val4);
				}
				if (!val4.things.Contains(sDS_PreservedBucket.display))
				{
					val4.things.Add(sDS_PreservedBucket.display);
				}
				SDS_VirtualDisplayRegistry.Register(sDS_PreservedBucket.display, SDS_VirtualDisplayRegistry.TransportOwner);
				RegisterPreservedBucket(sDS_PreservedBucket.display, sDS_PreservedBucket.things);
			}
		}
	}

	public static bool TryFindVirtualRequest(Thing container, List<TransferableOneWay> leftToLoad, out ThingDef def, out SDS_StockKey key, out int count, out List<Thing> preservedBucket)
	{
		def = null;
		key = null;
		count = 0;
		preservedBucket = null;
		if (leftToLoad == null)
		{
			return false;
		}
		Dictionary<TransferableOneWay, int> dictionary = BuildInFlight(container, leftToLoad);
		for (int i = 0; i < leftToLoad.Count; i++)
		{
			TransferableOneWay val = leftToLoad[i];
			if (val == null || ((Transferable)val).CountToTransfer <= 0)
			{
				continue;
			}
			SDS_StockKey sDS_StockKey = null;
			SDS_StockKey sDS_StockKey2 = null;
			List<Thing> list = null;
			for (int j = 0; j < val.things.Count; j++)
			{
				Thing val2 = val.things[j];
				if (!IsVirtualDisplay(val2))
				{
					continue;
				}
				if (TryGetPreservedBucket(val2, out var bucket))
				{
					if (list == null)
					{
						list = bucket;
						sDS_StockKey2 = SDS_OutputUtility.KeyOfDisplay(val2, bucket);
					}
				}
				else if (sDS_StockKey == null)
				{
					sDS_StockKey = SDS_OutputUtility.KeyOfDisplay(val2, null);
				}
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null)
			{
				continue;
			}
			bool flag = false;
			SDS_StockKey sDS_StockKey3 = null;
			if (sDS_StockKey != null && sDS_StockKey.thingDef != null && SDS_Availability.GetAvailableCount(instance, sDS_StockKey) > 0)
			{
				sDS_StockKey3 = sDS_StockKey;
			}
			else if (list != null && sDS_StockKey2 != null && sDS_StockKey2.thingDef != null && SDS_Availability.GetAvailableCount(instance, sDS_StockKey2) > 0)
			{
				sDS_StockKey3 = sDS_StockKey2;
				flag = true;
			}
			if (sDS_StockKey3 != null)
			{
				int value = 0;
				dictionary?.TryGetValue(val, out value);
				int num = ((Transferable)val).CountToTransfer - value;
				if (num > 0)
				{
					def = sDS_StockKey3.thingDef;
					key = sDS_StockKey3;
					count = num;
					preservedBucket = (flag ? list : null);
					return true;
				}
			}
		}
		return false;
	}

	public static Job MakeLoadJob(Pawn pawn, Thing container, SDS_StockKey key, int count, List<Thing> preservedBucket)
	{
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		JobDef withdrawForTransportLoad = SDS_JobDefOf.WithdrawForTransportLoad;
		ThingDef val = key?.thingDef;
		if (withdrawForTransportLoad == null || pawn == null || ((Thing)pawn).Map == null || container == null || val == null || count <= 0)
		{
			return null;
		}
		int num = ((pawn.carryTracker != null) ? pawn.carryTracker.MaxStackSpaceEver(val) : 0);
		int num2 = ((num > 0) ? Math.Min(count, num) : count);
		if (num2 <= 0)
		{
			return null;
		}
		SDS_WithdrawQuery query = SDS_WithdrawResolver.MakeQuery(key, num2, key.preserved, ((Thing)pawn).Position, SDS_WithdrawMode.Hand);
		SDS_EndpointHandle sDS_EndpointHandle = SDS_WithdrawResolver.Decide(query, pawn)?.endpoint;
		if (sDS_EndpointHandle == null || sDS_EndpointHandle.Thing == null)
		{
			return null;
		}
		List<ThingCount> list = null;
		if (preservedBucket != null)
		{
			HashSet<Thing> hashSet = null;
			foreach (KeyValuePair<int, List<ThingCount>> pendingLoadPlan in pendingLoadPlans)
			{
				List<ThingCount> value = pendingLoadPlan.Value;
				if (value == null)
				{
					continue;
				}
				for (int i = 0; i < value.Count; i++)
				{
					ThingCount val2 = value[i];
					if (((ThingCount)(ref val2)).Thing != null)
					{
						if (hashSet == null)
						{
							hashSet = new HashSet<Thing>();
						}
						HashSet<Thing> hashSet2 = hashSet;
						val2 = value[i];
						hashSet2.Add(((ThingCount)(ref val2)).Thing);
					}
				}
			}
			int plannedUnits = 0;
			list = SDS_PreservedBuckets.TakePlan(SDS_GameComponent_Grid.Instance, preservedBucket, num2, out plannedUnits, hashSet);
			if (plannedUnits <= 0 || list.Count == 0)
			{
				return null;
			}
			num2 = plannedUnits;
		}
		Job val3 = JobMaker.MakeJob(withdrawForTransportLoad, new LocalTargetInfo(sDS_EndpointHandle.Thing), new LocalTargetInfo(container));
		val3.thingDefToCarry = val;
		val3.count = num2;
		pendingLoadKeys[val3.loadID] = key;
		if (list != null)
		{
			pendingLoadPlans[val3.loadID] = list;
		}
		return val3;
	}

	public static SDS_StockKey TakePendingLoadKey(int jobLoadID)
	{
		if (!pendingLoadKeys.TryGetValue(jobLoadID, out var value))
		{
			return null;
		}
		pendingLoadKeys.Remove(jobLoadID);
		return value;
	}

	public static List<ThingCount> PeekPendingLoadPlan(int jobLoadID)
	{
		List<ThingCount> value;
		return pendingLoadPlans.TryGetValue(jobLoadID, out value) ? value : null;
	}

	public static List<ThingCount> TakePendingLoadPlan(int jobLoadID)
	{
		if (!pendingLoadPlans.TryGetValue(jobLoadID, out var value))
		{
			return null;
		}
		pendingLoadPlans.Remove(jobLoadID);
		return value;
	}

	public static void ForgetPendingLoadPlan(int jobLoadID)
	{
		pendingLoadPlans.Remove(jobLoadID);
		pendingLoadKeys.Remove(jobLoadID);
	}

	public static void ExportLoadRequests(List<SDS_TransportLoadSave> into)
	{
		if (into == null)
		{
			return;
		}
		into.Clear();
		List<Map> maps = Find.Maps;
		for (int i = 0; i < maps.Count; i++)
		{
			Map val = maps[i];
			if (val != null)
			{
				List<Thing> list = val.listerThings.ThingsInGroup((ThingRequestGroup)44);
				for (int j = 0; j < list.Count; j++)
				{
					ExportContainer(list[j], into);
				}
			}
		}
	}

	private static void ExportContainer(Thing container, List<SDS_TransportLoadSave> into)
	{
		List<TransferableOneWay> list = ((container != null) ? ThingCompUtility.TryGetComp<CompTransporter>(container) : null)?.leftToLoad;
		if (list == null || list.Count == 0)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			TransferableOneWay val = list[i];
			if (val == null || ((Transferable)val).CountToTransfer <= 0 || val.things == null)
			{
				continue;
			}
			Thing val2 = null;
			for (int j = 0; j < val.things.Count; j++)
			{
				if (IsVirtualDisplay(val.things[j]))
				{
					val2 = val.things[j];
					break;
				}
			}
			if (val2 == null)
			{
				continue;
			}
			TryGetPreservedBucket(val2, out var bucket);
			SDS_StockKey sDS_StockKey = SDS_OutputUtility.KeyOfDisplay(val2, bucket);
			bool preserved = sDS_StockKey?.preserved ?? false;
			if (sDS_StockKey != null && sDS_StockKey.thingDef != null && !string.IsNullOrEmpty(sDS_StockKey.defName))
			{
				int num = ((Transferable)val).CountToTransfer - UnloadedRealUnits(val, container);
				if (num > 0)
				{
					into.Add(new SDS_TransportLoadSave
					{
						containerThingID = container.thingIDNumber,
						rowIndex = i,
						defName = sDS_StockKey.defName,
						stuffName = sDS_StockKey.stuffName,
						qualityInt = sDS_StockKey.qualityInt,
						preserved = preserved,
						units = Math.Min(num, Math.Max(1, val2.stackCount))
					});
				}
			}
		}
	}

	public static void RestoreLoadRequests(List<SDS_TransportLoadSave> list)
	{
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		if (list == null || list.Count == 0)
		{
			return;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			return;
		}
		Dictionary<int, Thing> dictionary = CollectLoadContainers();
		List<SDS_PreservedBucket> list2 = null;
		for (int i = 0; i < list.Count; i++)
		{
			SDS_TransportLoadSave sDS_TransportLoadSave = list[i];
			if (sDS_TransportLoadSave == null || sDS_TransportLoadSave.units <= 0 || string.IsNullOrEmpty(sDS_TransportLoadSave.defName) || !dictionary.TryGetValue(sDS_TransportLoadSave.containerThingID, out var value) || value == null)
			{
				continue;
			}
			List<TransferableOneWay> list3 = ThingCompUtility.TryGetComp<CompTransporter>(value)?.leftToLoad;
			if (list3 == null || sDS_TransportLoadSave.rowIndex < 0 || sDS_TransportLoadSave.rowIndex >= list3.Count)
			{
				continue;
			}
			TransferableOneWay val = list3[sDS_TransportLoadSave.rowIndex];
			if (val == null || ((Transferable)val).CountToTransfer <= 0 || val.things == null)
			{
				continue;
			}
			bool flag = false;
			for (int j = 0; j < val.things.Count; j++)
			{
				if (IsVirtualDisplay(val.things[j]))
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				continue;
			}
			SDS_StockKey sDS_StockKey = BuildLoadKey(sDS_TransportLoadSave);
			if (sDS_StockKey == null || sDS_StockKey.thingDef == null)
			{
				continue;
			}
			List<Thing> list4 = null;
			Thing val2;
			if (sDS_TransportLoadSave.preserved)
			{
				if (list2 == null)
				{
					list2 = SDS_PreservedBuckets.Build(instance, delegate(Thing thing)
					{
						//IL_0016: Unknown result type (might be due to invalid IL or missing references)
						//IL_001c: Invalid comparison between Unknown and I4
						Thing innerIfMinified = MinifyUtility.GetInnerIfMinified(thing);
						return thing.def != null && (int)thing.def.category == 2 && innerIfMinified != null && innerIfMinified.def != null && innerIfMinified.def.canLoadIntoCaravan;
					}, tradeGrouping: false, null, tradableBudget: false);
				}
				list4 = FindPreservedBucket(list2, sDS_StockKey);
				val2 = ((list4 != null && list4.Count > 0) ? SDS_OutputUtility.CachedPreservedBucketDisplay(list4[0]) : null);
			}
			else
			{
				val2 = SDS_OutputUtility.CachedDisplayThing(sDS_StockKey);
			}
			if (val2 != null)
			{
				val2.stackCount = sDS_TransportLoadSave.units;
				IntVec3 val3 = value.InteractionCell;
				if (!((IntVec3)(ref val3)).IsValid || value.Map == null || !GenGrid.InBounds(val3, value.Map))
				{
					val3 = value.Position;
				}
				val2.Position = val3;
				val.things.Add(val2);
				SDS_VirtualDisplayRegistry.Register(val2, SDS_VirtualDisplayRegistry.TransportOwner);
				if (list4 != null)
				{
					RegisterPreservedBucket(val2, list4);
				}
			}
		}
		list.Clear();
	}

	private static SDS_StockKey BuildLoadKey(SDS_TransportLoadSave dto)
	{
		ThingDef val = SDS_DefResolve.ResolveThingDef(dto.defName);
		if (val == null)
		{
			return null;
		}
		ThingDef stuff = (string.IsNullOrEmpty(dto.stuffName) ? null : SDS_DefResolve.ResolveThingDef(dto.stuffName));
		return new SDS_StockKey(val, stuff, dto.qualityInt, dto.preserved);
	}

	private static Dictionary<int, Thing> CollectLoadContainers()
	{
		Dictionary<int, Thing> dictionary = new Dictionary<int, Thing>();
		List<Map> maps = Find.Maps;
		for (int i = 0; i < maps.Count; i++)
		{
			Map val = maps[i];
			if (val == null)
			{
				continue;
			}
			List<Thing> list = val.listerThings.ThingsInGroup((ThingRequestGroup)44);
			for (int j = 0; j < list.Count; j++)
			{
				Thing val2 = list[j];
				if (val2 != null && val2.Spawned)
				{
					dictionary[val2.thingIDNumber] = val2;
				}
			}
		}
		return dictionary;
	}

	private static List<Thing> FindPreservedBucket(List<SDS_PreservedBucket> buckets, SDS_StockKey key)
	{
		if (buckets == null || key == null)
		{
			return null;
		}
		for (int i = 0; i < buckets.Count; i++)
		{
			SDS_PreservedBucket sDS_PreservedBucket = buckets[i];
			if (sDS_PreservedBucket != null && sDS_PreservedBucket.things != null && sDS_PreservedBucket.things.Count != 0)
			{
				SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(sDS_PreservedBucket.things[0]);
				if (sDS_StockKey != null && sDS_StockKey.SameAs(key))
				{
					return sDS_PreservedBucket.things;
				}
			}
		}
		return null;
	}

	private static long LoadFailureKey(Pawn pawn, Thing container)
	{
		if (pawn == null || container == null)
		{
			return 0L;
		}
		return ((long)((Thing)pawn).thingIDNumber << 32) | (uint)container.thingIDNumber;
	}

	public static bool IsLoadCoolingDown(Pawn pawn, Thing container)
	{
		long num = LoadFailureKey(pawn, container);
		if (num == 0L || !loadFailureUntil.TryGetValue(num, out var value))
		{
			return false;
		}
		int num2 = ((Find.TickManager != null) ? Find.TickManager.TicksGame : 0);
		if (num2 >= value)
		{
			loadFailureUntil.Remove(num);
			return false;
		}
		return true;
	}

	public static void NotifyLoadFailed(Pawn pawn, Thing container)
	{
		long num = LoadFailureKey(pawn, container);
		if (num != 0)
		{
			int num2 = ((Find.TickManager != null) ? Find.TickManager.TicksGame : 0);
			loadFailureUntil[num] = num2 + 250;
		}
	}
}
[HarmonyPatch(typeof(TransporterUtility), "AllSendableItems")]
[HarmonyPriority(200)]
internal static class Patch_SDS_ShuttleRequiredItemsFromGrid
{
	public static void Postfix(List<CompTransporter> transporters, Map map, ref IEnumerable<Thing> __result)
	{
		try
		{
			if (transporters == null || transporters.Count == 0 || map == null)
			{
				return;
			}
			Thing val = (Thing)(object)((transporters[0] != null) ? ((ThingComp)transporters[0]).parent : null);
			CompShuttle val2 = ((val != null) ? ThingCompUtility.TryGetComp<CompShuttle>(val) : null);
			if (val2 != null && !GenList.NullOrEmpty<ThingDefCount>((IList<ThingDefCount>)val2.requiredItems))
			{
				SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
				if (instance != null && instance.Available && SDS_OutputUtility.MapExposesGrid(map))
				{
					__result = AppendRequiredItems(__result, val2, val, map, instance);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("穿梭机必需物品注入超维空间失败（已忽略，原版装载不受影响）", exception);
		}
	}

	private static IEnumerable<Thing> AppendRequiredItems(IEnumerable<Thing> source, CompShuttle shuttle, Thing transporter, Map map, SDS_GameComponent_Grid grid)
	{
		if (source != null)
		{
			foreach (Thing item in source)
			{
				yield return item;
			}
		}
		IntVec3 displayCell = (GenGrid.InBounds(transporter.InteractionCell, map) ? transporter.InteractionCell : transporter.Position);
		for (int i = 0; i < shuttle.requiredItems.Count; i++)
		{
			ThingDefCount required = shuttle.requiredItems[i];
			ThingDef def = ((required != ThingDefCount.op_Implicit((ThingDefCountClass)null)) ? ((ThingDefCount)(ref required)).ThingDef : null);
			if (def == null || (int)def.category != 2 || !def.canLoadIntoCaravan)
			{
				continue;
			}
			SDS_StockKey key = grid.FindWithdrawableKey(def);
			if (key == null)
			{
				continue;
			}
			long available = SDS_Availability.GetAvailableCount(grid, key);
			if (available > 0)
			{
				Thing fake = SDS_OutputUtility.CachedDisplayThing(key);
				if (fake != null)
				{
					fake.stackCount = (int)((available > int.MaxValue) ? int.MaxValue : available);
					fake.Position = displayCell;
					yield return fake;
					required = default(ThingDefCount);
				}
			}
		}
	}
}
[HarmonyPatch(typeof(Dialog_LoadTransporters), "AddItemsToTransferables")]
internal static class Patch_SDS_TransporterAddVirtual
{
	public static void Postfix(Dialog_LoadTransporters __instance)
	{
		try
		{
			if (__instance != null)
			{
				Traverse val = Traverse.Create((object)__instance);
				List<TransferableOneWay> value = val.Field("transferables").GetValue<List<TransferableOneWay>>();
				List<CompTransporter> value2 = val.Field("transporters").GetValue<List<CompTransporter>>();
				Map value3 = val.Field("map").GetValue<Map>();
				if (SDS_ModCompat_EquivalentExchange.AllowInjection(__instance))
				{
					SDS_TransportVirtual.InjectIntoTransporters(value, value2, value3);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("运输舱超维空间品类注入失败（已忽略，原版装载不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(LoadTransportersJobUtility), "HasJobOnTransporter")]
internal static class Patch_SDS_TransporterLoadHasJob
{
	public static void Postfix(Pawn pawn, CompTransporter transporter, ref bool __result)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!__result && pawn != null && ((Thing)pawn).Map != null && transporter != null && ((ThingComp)transporter).parent != null)
			{
				ThingCount val = LoadTransportersJobUtility.FindThingToLoad(pawn, transporter);
				if (((ThingCount)(ref val)).Thing == null && pawn.health != null && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit((Thing)(object)((ThingComp)transporter).parent), (PathEndMode)2, DangerUtility.NormalMaxDanger(pawn), false, false, (TraverseMode)0) && !SDS_TransportVirtual.IsLoadCoolingDown(pawn, (Thing)(object)((ThingComp)transporter).parent) && SDS_TransportVirtual.TryFindVirtualRequest((Thing)(object)((ThingComp)transporter).parent, transporter.leftToLoad, out var _, out var _, out var count, out var _) && count > 0 && SDS_OutputUtility.FindWithdrawPoint(pawn, ((Thing)pawn).Map, forced: false) != null)
				{
					__result = true;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("运输舱超维空间可装载判定失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(LoadTransportersJobUtility), "JobOnTransporter")]
[HarmonyPriority(200)]
internal static class Patch_SDS_TransporterLoadJob
{
	public static void Postfix(Pawn p, CompTransporter transporter, ref Job __result)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (p == null || ((Thing)p).Map == null || transporter == null || ((ThingComp)transporter).parent == null)
			{
				return;
			}
			ThingCount val = LoadTransportersJobUtility.FindThingToLoad(p, transporter);
			if (((ThingCount)(ref val)).Thing == null)
			{
				if (SDS_TransportVirtual.IsLoadCoolingDown(p, (Thing)(object)((ThingComp)transporter).parent) || !SDS_TransportVirtual.TryFindVirtualRequest((Thing)(object)((ThingComp)transporter).parent, transporter.leftToLoad, out var _, out var key, out var count, out var preservedBucket))
				{
					__result = null;
				}
				else
				{
					__result = SDS_TransportVirtual.MakeLoadJob(p, (Thing)(object)((ThingComp)transporter).parent, key, count, preservedBucket);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("运输舱超维空间取货派活失败（已忽略，交回原版）", exception);
		}
	}
}
[HarmonyPatch(typeof(Dialog_EnterPortal), "AddItemsToTransferables")]
internal static class Patch_SDS_PortalAddVirtual
{
	public static void Postfix(Dialog_EnterPortal __instance)
	{
		try
		{
			if (__instance != null)
			{
				Traverse val = Traverse.Create((object)__instance);
				List<TransferableOneWay> value = val.Field("transferables").GetValue<List<TransferableOneWay>>();
				MapPortal value2 = val.Field("portal").GetValue<MapPortal>();
				SDS_TransportVirtual.InjectIntoPortal(value, value2);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("传送门超维空间品类注入失败（已忽略，原版进入不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(EnterPortalUtility), "HasJobOnPortal")]
[HarmonyPriority(200)]
internal static class Patch_SDS_PortalLoadHasJob
{
	public static void Postfix(Pawn pawn, MapPortal portal, ref bool __result)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!__result && pawn != null && ((Thing)pawn).Map != null && portal != null)
			{
				ThingCount val = EnterPortalUtility.FindThingToLoad(pawn, portal);
				if (((ThingCount)(ref val)).Thing == null && pawn.health != null && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit((Thing)(object)portal), (PathEndMode)2, DangerUtility.NormalMaxDanger(pawn), false, false, (TraverseMode)0) && !SDS_TransportVirtual.IsLoadCoolingDown(pawn, (Thing)(object)portal) && SDS_TransportVirtual.TryFindVirtualRequest((Thing)(object)portal, portal.leftToLoad, out var _, out var _, out var count, out var _) && count > 0 && SDS_OutputUtility.FindWithdrawPoint(pawn, ((Thing)pawn).Map, forced: false) != null)
				{
					__result = true;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("传送门超维空间可装载判定失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(EnterPortalUtility), "JobOnPortal")]
[HarmonyPriority(200)]
internal static class Patch_SDS_PortalLoadJob
{
	public static void Postfix(Pawn p, MapPortal portal, ref Job __result)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (p == null || ((Thing)p).Map == null || portal == null)
			{
				return;
			}
			ThingCount val = EnterPortalUtility.FindThingToLoad(p, portal);
			if (((ThingCount)(ref val)).Thing == null)
			{
				if (SDS_TransportVirtual.IsLoadCoolingDown(p, (Thing)(object)portal) || !SDS_TransportVirtual.TryFindVirtualRequest((Thing)(object)portal, portal.leftToLoad, out var _, out var key, out var count, out var preservedBucket))
				{
					__result = null;
				}
				else
				{
					__result = SDS_TransportVirtual.MakeLoadJob(p, (Thing)(object)portal, key, count, preservedBucket);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("传送门超维空间取货派活失败（已忽略，交回原版）", exception);
		}
	}
}
public class JobDriver_SDS_WithdrawFromGrid : JobDriver
{
	internal const TargetIndex WithdrawPointInd = (TargetIndex)1;

	internal const TargetIndex WorkTargetInd = (TargetIndex)2;

	private bool IsSelfWithdrawPoint()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		if (base.job == null || base.pawn == null)
		{
			return false;
		}
		LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
		Thing thing = ((LocalTargetInfo)(ref target)).Thing;
		return thing != null && (object)thing == base.pawn;
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		if (base.pawn.carryTracker != null && base.pawn.carryTracker.CarriedThing != null)
		{
			Thing val = default(Thing);
			base.pawn.carryTracker.TryDropCarriedThing(((Thing)base.pawn).Position, (ThingPlaceMode)1, ref val, (Action<Thing, int>)null);
		}
		Thing thing = ((LocalTargetInfo)(ref base.job.targetA)).Thing;
		if (!IsSelfWithdrawPoint() && !(thing is Pawn) && !ReservationUtility.Reserve(base.pawn, base.job.targetA, base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false))
		{
			SDS_Diag.Fail("取货/取物点不可预留", SDS_Diag.Ctx(((Thing)base.pawn).Map, base.pawn, thing, base.job));
			return false;
		}
		return true;
	}

	private void ReleaseEnrouteClaim()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (base.pawn == null || ((Thing)base.pawn).Map == null)
		{
			return;
		}
		LocalTargetInfo target = base.job.GetTarget((TargetIndex)2);
		Thing thing = ((LocalTargetInfo)(ref target)).Thing;
		if (thing != null && !thing.Destroyed)
		{
			IHaulEnroute val = (IHaulEnroute)(object)((thing is IHaulEnroute) ? thing : null);
			if (val != null)
			{
				((Thing)base.pawn).Map.enrouteManager.ReleaseFor(val, base.pawn);
			}
		}
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate
		{
			SDS_GameComponent_Grid.Instance?.ReleaseReservation(base.job.loadID);
			SDS_TransportVirtual.ForgetPendingLoadPlan(base.job.loadID);
			ReleaseEnrouteClaim();
		});
		ToilFailConditions.FailOnIncapable<JobDriver_SDS_WithdrawFromGrid>(this, PawnCapacityDefOf.Manipulation);
		if (!IsSelfWithdrawPoint())
		{
			if (((LocalTargetInfo)(ref base.job.targetA)).Thing is Pawn)
			{
				ToilFailConditions.FailOn<JobDriver_SDS_WithdrawFromGrid>(this, (Func<bool>)delegate
				{
					//IL_0008: Unknown result type (might be due to invalid IL or missing references)
					//IL_000d: Unknown result type (might be due to invalid IL or missing references)
					LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
					Thing thing = ((LocalTargetInfo)(ref target)).Thing;
					return thing == null || thing.Destroyed || !thing.Spawned;
				});
			}
			else
			{
				ToilFailConditions.FailOnDespawnedNullOrForbidden<JobDriver_SDS_WithdrawFromGrid>(this, (TargetIndex)1);
			}
		}
		yield return Toils_General.Do((Action)delegate
		{
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_011d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0122: Unknown result type (might be due to invalid IL or missing references)
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			List<ThingCount> list = SDS_TransportVirtual.PeekPendingLoadPlan(base.job.loadID);
			LocalTargetInfo target;
			if (instance != null && list != null && list.Count > 0)
			{
				if (!instance.TryReservePreservedPlan(base.job, base.pawn, list))
				{
					Pawn pawn = base.pawn;
					target = base.job.GetTarget((TargetIndex)2);
					SDS_TransportVirtual.NotifyLoadFailed(pawn, ((LocalTargetInfo)(ref target)).Thing);
					((JobDriver)this).EndJobWith((JobCondition)4);
				}
			}
			else
			{
				int num = ((base.pawn.carryTracker != null && base.job.thingDefToCarry != null) ? base.pawn.carryTracker.MaxStackSpaceEver(base.job.thingDefToCarry) : 0);
				int maxStackSpace = Math.Min((num > 0) ? num : base.job.count, base.job.count);
				SDS_StockKey preferredKey = SDS_TransportVirtual.TakePendingLoadKey(base.job.loadID);
				if (instance == null || !instance.TryReserveBillWithdrawal(base.job, base.pawn, maxStackSpace, preferredKey))
				{
					Pawn pawn2 = base.pawn;
					target = base.job.GetTarget((TargetIndex)2);
					SDS_TransportVirtual.NotifyLoadFailed(pawn2, ((LocalTargetInfo)(ref target)).Thing);
					((JobDriver)this).EndJobWith((JobCondition)4);
				}
			}
		});
		if (!IsSelfWithdrawPoint())
		{
			bool pawnPoint = ((LocalTargetInfo)(ref base.job.targetA)).Thing is Pawn;
			PathEndMode endMode = (PathEndMode)(pawnPoint ? 2 : 4);
			yield return Toils_General.Do((Action)delegate
			{
				//IL_000d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0012: Unknown result type (might be due to invalid IL or missing references)
				//IL_003a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0040: Unknown result type (might be due to invalid IL or missing references)
				//IL_0050: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
				Thing thing = ((LocalTargetInfo)(ref target)).Thing;
				if (thing == null || !thing.Spawned || thing.Destroyed || !ReachabilityUtility.CanReach(base.pawn, LocalTargetInfo.op_Implicit(thing), endMode, DangerUtility.NormalMaxDanger(base.pawn), false, false, (TraverseMode)0))
				{
					((JobDriver)this).EndJobWith((JobCondition)4);
				}
			});
			yield return Toils_Goto.GotoThing((TargetIndex)1, endMode, false);
		}
		Toil materialize = ToilMaker.MakeToil("SDS_MaterializeTransportLoad");
		materialize.initAction = MaterializeAndHandoff;
		materialize.defaultCompleteMode = (ToilCompleteMode)1;
		yield return materialize;
	}

	private void MaterializeAndHandoff()
	{
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0304: Unknown result type (might be due to invalid IL or missing references)
		//IL_0309: Unknown result type (might be due to invalid IL or missing references)
		//IL_0312: Unknown result type (might be due to invalid IL or missing references)
		//IL_0317: Unknown result type (might be due to invalid IL or missing references)
		//IL_0334: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		List<ThingCount> list = SDS_TransportVirtual.TakePendingLoadPlan(base.job.loadID);
		bool flag;
		if (list != null && list.Count > 0)
		{
			flag = false;
			if (instance != null && SDS_WithdrawExecutor.TryWithdrawPreservedExactAdopted(base.job.loadID, list, out var made) && made != null && made.Count > 0)
			{
				SDS_ProducedThingMarker.MarkAll(made);
				for (int i = 0; i < made.Count; i++)
				{
					Thing val = made[i];
					if (val != null && !val.Destroyed && (base.pawn.carryTracker == null || base.pawn.carryTracker.CarriedThing != null || !base.pawn.carryTracker.TryStartCarry(val)))
					{
						SDS_ReturnUtility.ReturnPreserved(val, ((Thing)base.pawn).Map, ((Thing)base.pawn).Position);
					}
				}
				flag = base.pawn.carryTracker != null && base.pawn.carryTracker.CarriedThing != null;
			}
		}
		else
		{
			flag = instance != null && SDS_WithdrawExecutor.TryWithdrawReservedForJob(base.job.loadID, base.pawn);
		}
		LocalTargetInfo target;
		if (!flag)
		{
			Map map = ((base.pawn != null) ? ((Thing)base.pawn).Map : null);
			Pawn pawn = base.pawn;
			object thing;
			if (base.job == null)
			{
				thing = null;
			}
			else
			{
				target = base.job.GetTarget((TargetIndex)2);
				thing = ((LocalTargetInfo)(ref target)).Thing;
			}
			SDS_Diag.Fail("装载/取货物化失败", SDS_Diag.Ctx(map, pawn, (Thing)thing, base.job, "reservation=" + ((base.job != null) ? base.job.loadID : 0)));
			Pawn pawn2 = base.pawn;
			target = base.job.GetTarget((TargetIndex)2);
			SDS_TransportVirtual.NotifyLoadFailed(pawn2, ((LocalTargetInfo)(ref target)).Thing);
			((JobDriver)this).EndJobWith((JobCondition)4);
			return;
		}
		Thing val2 = ((base.pawn.carryTracker != null) ? base.pawn.carryTracker.CarriedThing : null);
		target = base.job.GetTarget((TargetIndex)2);
		Thing thing2 = ((LocalTargetInfo)(ref target)).Thing;
		if (val2 == null || thing2 == null || thing2.Destroyed || !thing2.Spawned)
		{
			SDS_Diag.Fail("装载/交付目标失效", SDS_Diag.Ctx((base.pawn != null) ? ((Thing)base.pawn).Map : null, base.pawn, thing2, base.job, "carried=" + ((val2 != null) ? ((Def)val2.def).defName : "无")));
			ReturnCarriedThingToGridOrDrop();
			SDS_TransportVirtual.NotifyLoadFailed(base.pawn, thing2);
			((JobDriver)this).EndJobWith((JobCondition)4);
			return;
		}
		JobDef val3 = ((thing2 is MapPortal) ? JobDefOf.HaulToPortal : JobDefOf.HaulToTransporter);
		Job val4 = JobMaker.MakeJob(val3);
		val4.targetA = LocalTargetInfo.op_Implicit(val2);
		val4.targetB = LocalTargetInfo.op_Implicit(thing2);
		val4.count = val2.stackCount;
		val4.ignoreForbidden = true;
		val4.haulMode = (HaulMode)3;
		val4.playerForced = base.job.playerForced;
		val4.workGiverDef = base.job.workGiverDef;
		base.pawn.jobs.StartJob(val4, (JobCondition)2, (ThinkNode)null, false, true, (ThinkTreeDef)null, (JobTag?)null, false, false, (bool?)true, false, true, true);
	}

	private void ReturnCarriedThingToGridOrDrop()
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		Thing val = default(Thing);
		if (base.pawn != null && base.pawn.carryTracker != null && base.pawn.carryTracker.CarriedThing != null && base.pawn.carryTracker.TryDropCarriedThing(((Thing)base.pawn).Position, (ThingPlaceMode)1, ref val, (Action<Thing, int>)null) && val != null)
		{
			SDS_ReturnUtility.Return(val, ((Thing)base.pawn).Map, ((Thing)base.pawn).Position, SDS_ReturnFallback.None);
		}
	}
}
public static class SDS_JobDefOf
{
	public const string WithdrawForTransportLoadName = "SDS_WithdrawForTransportLoad";

	public const string DepositToGridName = "SDS_DepositToGrid";

	public const string WithdrawPreservedForBillName = "SDS_WithdrawPreservedForBill";

	private static JobDef withdrawForTransportLoad;

	private static JobDef depositToGrid;

	private static JobDef withdrawPreservedForBill;

	private static bool lookedUp;

	public static JobDef WithdrawForTransportLoad
	{
		get
		{
			EnsureLookup();
			return withdrawForTransportLoad;
		}
	}

	public static JobDef DepositToGrid
	{
		get
		{
			EnsureLookup();
			return depositToGrid;
		}
	}

	public static JobDef WithdrawPreservedForBill
	{
		get
		{
			EnsureLookup();
			return withdrawPreservedForBill;
		}
	}

	private static void EnsureLookup()
	{
		if (!lookedUp)
		{
			lookedUp = true;
			withdrawForTransportLoad = DefDatabase<JobDef>.GetNamedSilentFail("SDS_WithdrawForTransportLoad");
			depositToGrid = DefDatabase<JobDef>.GetNamedSilentFail("SDS_DepositToGrid");
			withdrawPreservedForBill = DefDatabase<JobDef>.GetNamedSilentFail("SDS_WithdrawPreservedForBill");
			if (withdrawForTransportLoad == null)
			{
				Log.Error("[超维存储] 找不到 JobDef SDS_WithdrawForTransportLoad：运输舱 / 传送门装载的超维取货不会启用。请检查 Defs/SDS_JobDefs.xml。");
			}
			if (depositToGrid == null)
			{
				Log.Error("[超维存储] 找不到 JobDef SDS_DepositToGrid：手动「存入超维空间」不会启用。请检查 Defs/SDS_JobDefs.xml。");
			}
			if (withdrawPreservedForBill == null)
			{
				Log.Error("[超维存储] 找不到 JobDef SDS_WithdrawPreservedForBill：保真原料（尸体等）无法从端点派活取用。请检查 Defs/SDS_JobDefs.xml。");
			}
		}
	}
}
public sealed class SDS_TransportLoadSave : IExposable
{
	public int containerThingID;

	public int rowIndex = -1;

	public string defName;

	public string stuffName;

	public int qualityInt = -1;

	public bool preserved;

	public int units;

	public void ExposeData()
	{
		Scribe_Values.Look<int>(ref containerThingID, "containerThingID", 0, false);
		Scribe_Values.Look<int>(ref rowIndex, "rowIndex", -1, false);
		Scribe_Values.Look<string>(ref defName, "defName", (string)null, false);
		Scribe_Values.Look<string>(ref stuffName, "stuffName", (string)null, false);
		Scribe_Values.Look<int>(ref qualityInt, "qualityInt", -1, false);
		Scribe_Values.Look<bool>(ref preserved, "preserved", false, false);
		Scribe_Values.Look<int>(ref units, "units", 0, false);
	}
}
internal sealed class SDS_CompatPatchReport
{
	private readonly string modName;

	private readonly List<string> applied = new List<string>();

	private readonly List<string> failed = new List<string>();

	public int AppliedCount => applied.Count;

	public int DeclaredCount => applied.Count + failed.Count;

	public bool HasFailures => failed.Count > 0;

	public SDS_CompatPatchReport(string modName)
	{
		this.modName = modName;
	}

	public void Applied(string hook)
	{
		if (!string.IsNullOrEmpty(hook))
		{
			applied.Add(hook);
		}
	}

	public void Missing(string hook, string reason)
	{
		if (!string.IsNullOrEmpty(hook))
		{
			failed.Add(string.IsNullOrEmpty(reason) ? hook : (hook + " → " + reason));
		}
	}

	public void Flush(string remark = null)
	{
		int num = applied.Count + failed.Count;
		if (num != 0)
		{
			string text = "[超维存储] 【第三方 Mod 兼容补丁】" + modName + "：A/B = " + applied.Count + "/" + num + " 个挂点";
			string text2 = (string.IsNullOrEmpty(remark) ? string.Empty : ("（" + remark + "）"));
			if (failed.Count == 0)
			{
				Log.Message(text + text2 + "。");
				return;
			}
			Log.Warning("[超维存储] 【第三方 Mod 兼容补丁】" + modName + "：A/B = " + applied.Count + "/" + num + " 个挂点；失败 " + failed.Count + " 个：" + Join(failed) + ((applied.Count > 0) ? ("；已生效：" + Join(applied)) : string.Empty) + text2 + "。");
		}
	}

	private static string Join(List<string> items)
	{
		return string.Join("，", items.ToArray());
	}
}
internal sealed class SDS_CompatAdapterReport
{
	public string name;

	public string packageId;

	public bool detected;

	public SDS_CompatPatchReport report;

	public string remark;

	public string detectedButBroken;
}
internal static class SDS_CompatReportHub
{
	private static readonly List<SDS_CompatAdapterReport> reports = new List<SDS_CompatAdapterReport>();

	public static void Reset()
	{
		reports.Clear();
	}

	public static bool Has(string name)
	{
		for (int i = 0; i < reports.Count; i++)
		{
			if (reports[i] != null && reports[i].name == name)
			{
				return true;
			}
		}
		return false;
	}

	public static void Register(string name, string packageId, bool detected, SDS_CompatPatchReport report, string remark = null, string detectedButBroken = null)
	{
		reports.Add(new SDS_CompatAdapterReport
		{
			name = name,
			packageId = packageId,
			detected = detected,
			report = report,
			remark = remark,
			detectedButBroken = detectedButBroken
		});
	}

	public static void LogAll()
	{
		for (int i = 0; i < reports.Count; i++)
		{
			SDS_CompatAdapterReport sDS_CompatAdapterReport = reports[i];
			if (sDS_CompatAdapterReport.detected)
			{
				string text = (string.IsNullOrEmpty(sDS_CompatAdapterReport.remark) ? string.Empty : ("（" + sDS_CompatAdapterReport.remark + "）"));
				if (sDS_CompatAdapterReport.report == null)
				{
					string text2 = (string.IsNullOrEmpty(sDS_CompatAdapterReport.detectedButBroken) ? "关键成员缺失" : sDS_CompatAdapterReport.detectedButBroken);
					Log.Warning("[超维存储] 适配器 · " + sDS_CompatAdapterReport.name + "：已检测到，A/B = 0/0（" + text2 + "）" + text + "。");
				}
				else
				{
					Log.Message("[超维存储] 适配器 · " + sDS_CompatAdapterReport.name + "：已检测到，A/B = " + sDS_CompatAdapterReport.report.AppliedCount + "/" + sDS_CompatAdapterReport.report.DeclaredCount + " 个挂点" + text + "。");
				}
			}
		}
		for (int j = 0; j < reports.Count; j++)
		{
			SDS_CompatAdapterReport sDS_CompatAdapterReport2 = reports[j];
			if (sDS_CompatAdapterReport2.detected && sDS_CompatAdapterReport2.report != null && sDS_CompatAdapterReport2.report.HasFailures)
			{
				sDS_CompatAdapterReport2.report.Flush(sDS_CompatAdapterReport2.remark);
			}
		}
	}
}
internal static class SDS_CompatTools
{
	public static MethodInfo Method(Type type, string name, params Type[] args)
	{
		if (type == null || string.IsNullOrEmpty(name))
		{
			return null;
		}
		try
		{
			return (args == null || args.Length == 0) ? AccessTools.Method(type, name, (Type[])null, (Type[])null) : AccessTools.Method(type, name, args, (Type[])null);
		}
		catch
		{
			return null;
		}
	}

	public static PropertyInfo Property(Type type, string name)
	{
		if (type == null || string.IsNullOrEmpty(name))
		{
			return null;
		}
		try
		{
			return AccessTools.Property(type, name);
		}
		catch
		{
			return null;
		}
	}

	public static MethodInfo PropertyGetter(Type type, string name)
	{
		if (type == null || string.IsNullOrEmpty(name))
		{
			return null;
		}
		try
		{
			return AccessTools.PropertyGetter(type, name);
		}
		catch
		{
			return null;
		}
	}

	public static FieldInfo Field(Type type, string name)
	{
		if (type == null || string.IsNullOrEmpty(name))
		{
			return null;
		}
		try
		{
			return AccessTools.Field(type, name);
		}
		catch
		{
			return null;
		}
	}

	public static bool TryPatch(Harmony harmony, SDS_CompatPatchReport report, Type ownerType, string hook, MethodInfo target, string prefixName, string postfixName, string missingReason, string finalizerName = null, int priority = 200, int finalizerPriority = 200)
	{
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		if (target == null)
		{
			report?.Missing(hook, missingReason);
			return false;
		}
		if (harmony == null || ownerType == null)
		{
			report?.Missing(hook, "Harmony / 适配器类型为空");
			return false;
		}
		try
		{
			HarmonyMethod val = (string.IsNullOrEmpty(prefixName) ? ((HarmonyMethod)null) : new HarmonyMethod(AccessTools.Method(ownerType, prefixName, (Type[])null, (Type[])null))
			{
				priority = priority
			});
			HarmonyMethod val2 = (string.IsNullOrEmpty(postfixName) ? ((HarmonyMethod)null) : new HarmonyMethod(AccessTools.Method(ownerType, postfixName, (Type[])null, (Type[])null))
			{
				priority = priority
			});
			HarmonyMethod val3 = (string.IsNullOrEmpty(finalizerName) ? ((HarmonyMethod)null) : new HarmonyMethod(AccessTools.Method(ownerType, finalizerName, (Type[])null, (Type[])null))
			{
				priority = finalizerPriority
			});
			if (val == null && val2 == null && val3 == null)
			{
				report?.Missing(hook, "未指定 Prefix/Postfix/Finalizer");
				return false;
			}
			harmony.Patch((MethodBase)target, val, val2, (HarmonyMethod)null, val3);
			report?.Applied(hook);
			return true;
		}
		catch (Exception ex)
		{
			report?.Missing(hook, "应用异常 " + ex.GetType().Name);
			SDS_Diag.ErrorOnce("第三方兼容挂点应用失败（已跳过）：" + hook, ex);
			return false;
		}
	}

	public static bool GetBool(PropertyInfo prop, object instance)
	{
		if (prop == null || instance == null)
		{
			return false;
		}
		try
		{
			object value = prop.GetValue(instance, null);
			bool flag = default(bool);
			int num;
			if (value is bool)
			{
				flag = (bool)value;
				num = 1;
			}
			else
			{
				num = 0;
			}
			return (byte)((uint)num & (flag ? 1u : 0u)) != 0;
		}
		catch
		{
			return false;
		}
	}
}
internal static class SDS_CompatEndpoint
{
	public static bool HasAnyEndpoint(Map map)
	{
		return SDS_API.HasAnyEndpoint(map);
	}

	public static bool HasUsableBuildingEndpointWithin(Map map, IntVec3 root, float radius)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		return SDS_API.HasUsableBuildingEndpointWithin(map, root, radius);
	}
}
internal static class SDS_CompatStock
{
	public static long CountAvailableVirtual(ThingDef def)
	{
		return SDS_API.GetAvailableCountForDef(def);
	}

	public static long CountAvailablePreserved(ThingDef def)
	{
		return SDS_API.GetPreservedAvailableCount(def);
	}

	public static bool TryWithdrawVirtualByDef(ThingDef def, int count, Map map, out List<Thing> made)
	{
		SDS_ApiResultCode code;
		return SDS_API.TryWithdrawVirtualByDef(def, count, map, SDS_ApiCaller.FromFaction(Faction.OfPlayer), out made, out code);
	}

	public static bool TryWithdrawPreservedByDef(ThingDef def, int count, Map map, out List<Thing> made)
	{
		SDS_ApiResultCode code;
		return SDS_API.TryWithdrawPreservedByDef(def, count, map, SDS_ApiCaller.FromFaction(Faction.OfPlayer), out made, out code);
	}

	public static void ReturnPreserved(List<Thing> things, Map map, IntVec3 cell)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if (things == null)
		{
			return;
		}
		for (int i = 0; i < things.Count; i++)
		{
			Thing val = things[i];
			if (val != null && !val.Destroyed)
			{
				SDS_API.TryReturnPreserved(val, map, cell);
			}
		}
	}
}
internal static class SDS_CompatIngredientRule
{
	public static bool AllowsDef(IngredientCount slot, Thing thing, ThingDef def, Bill bill, RecipeDef recipe, List<ThingDef> allowedIngredients, ThingFilter corpseFilter)
	{
		if (def == null)
		{
			return false;
		}
		if (bill != null && thing != null)
		{
			return bill.IsFixedOrAllowedIngredient(thing);
		}
		if (slot != null && slot.filter != null && !slot.filter.Allows(def))
		{
			return false;
		}
		if (bill != null && slot != null && !slot.IsFixedIngredient && (bill.ingredientFilter == null || !bill.ingredientFilter.Allows(def)))
		{
			return false;
		}
		if (allowedIngredients != null && allowedIngredients.Count > 0 && !allowedIngredients.Contains(def))
		{
			return false;
		}
		if (slot != null && !slot.IsFixedIngredient && recipe?.fixedIngredientFilter != null && !recipe.fixedIngredientFilter.Allows(def))
		{
			return false;
		}
		if (corpseFilter != null && def.IsCorpse && !corpseFilter.Allows(def))
		{
			return false;
		}
		return true;
	}

	public static ThingDef ChooseDef(IngredientCount slot, Bill bill, RecipeDef recipe, List<ThingDef> allowedIngredients, ThingFilter corpseFilter, Func<ThingDef, int> ownCount, Func<ThingDef, long> externalCount, int need, Func<ThingDef, bool> extraAllows, out int chosenOwn)
	{
		chosenOwn = 0;
		if (slot?.filter == null)
		{
			return null;
		}
		IEnumerable<ThingDef> allowedThingDefs = slot.filter.AllowedThingDefs;
		if (allowedThingDefs == null)
		{
			return null;
		}
		ThingDef val = null;
		int num = 0;
		foreach (ThingDef item in allowedThingDefs)
		{
			if (item != null && AllowsDef(slot, null, item, bill, recipe, allowedIngredients, corpseFilter) && (extraAllows == null || extraAllows(item)))
			{
				int num2 = ((ownCount != null) ? Math.Max(0, ownCount(item)) : 0);
				long num3 = ((externalCount != null) ? Math.Max(0L, externalCount(item)) : 0);
				if (num2 >= need)
				{
					chosenOwn = num2;
					return item;
				}
				if (val == null && num2 + num3 >= need)
				{
					val = item;
					num = num2;
				}
			}
		}
		if (val != null)
		{
			chosenOwn = num;
		}
		return val;
	}
}
internal static class SDS_CompatBillSupply
{
	private static MethodInfo findInSet;

	private static bool findInSetResolved;

	private static MethodInfo FindInSet
	{
		get
		{
			if (!findInSetResolved)
			{
				findInSetResolved = true;
				findInSet = AccessTools.Method(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet", new Type[6]
				{
					typeof(List<Thing>),
					typeof(Bill),
					typeof(List<ThingCount>),
					typeof(IntVec3),
					typeof(bool),
					typeof(List<IngredientCount>)
				}, (Type[])null);
			}
			return findInSet;
		}
	}

	public static bool TrySupplyPreservedBill(Bill bill, Map map, IntVec3 rootCell, ThingOwner destination, out List<Thing> made)
	{
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_0337: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a2: Unknown result type (might be due to invalid IL or missing references)
		made = null;
		if (bill == null || bill.recipe == null || bill.recipe.ingredients == null || bill.recipe.ingredients.Count == 0 || map == null)
		{
			return false;
		}
		if (!SDS_API.IsGridAvailable())
		{
			return false;
		}
		MethodInfo methodInfo = FindInSet;
		if (methodInfo == null)
		{
			return false;
		}
		List<Thing> list = new List<Thing>();
		HashSet<Thing> hashSet = new HashSet<Thing>();
		if (destination != null)
		{
			foreach (Thing item2 in (IEnumerable<Thing>)destination)
			{
				if (item2 != null && !item2.Destroyed && hashSet.Add(item2))
				{
					list.Add(item2);
				}
			}
		}
		List<Thing> list2 = new List<Thing>();
		SDS_API.AppendPreservedThings(list2);
		for (int i = 0; i < list2.Count; i++)
		{
			Thing val = list2[i];
			if (val != null && !val.Destroyed && val.stackCount == 1 && SDS_API.IsLivePreservedThing(val) && hashSet.Add(val) && SDS_CompatIngredientRule.AllowsDef(null, val, val.def, bill, null, null, null))
			{
				SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(val);
				if (sDS_StockKey != null && !SDS_API.IsStockForbidden(sDS_StockKey) && SDS_API.GetPreservedAvailableCount(sDS_StockKey) > 0)
				{
					list.Add(val);
				}
			}
		}
		if (list.Count == 0)
		{
			return false;
		}
		List<ThingCount> list3 = new List<ThingCount>();
		bool flag;
		try
		{
			flag = (bool)methodInfo.Invoke(null, new object[6] { list, bill, list3, rootCell, false, null });
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("第三方配料选择调用原版算法失败（已忽略）", exception);
			return false;
		}
		if (!flag || list3.Count == 0)
		{
			return false;
		}
		List<ThingCount> list4 = new List<ThingCount>();
		for (int j = 0; j < list3.Count; j++)
		{
			ThingCount item = list3[j];
			Thing thing = ((ThingCount)(ref item)).Thing;
			if (thing != null && !thing.Destroyed && (destination == null || !destination.Contains(thing)) && SDS_API.IsPreservedThing(thing))
			{
				list4.Add(item);
			}
		}
		if (list4.Count == 0)
		{
			return false;
		}
		int num = SumCounts(list4);
		if (!SDS_API.TryWithdrawPreservedExact(list4, SDS_WithdrawMode.Container, SDS_ApiCaller.FromFaction(Faction.OfPlayer), out var made2, out var _) || made2 == null || made2.Count == 0)
		{
			return false;
		}
		List<Thing> list5 = new List<Thing>(made2);
		if (SumUnits(made2) < num)
		{
			ReturnPreserved(list5, map, rootCell);
			return false;
		}
		if (destination != null)
		{
			for (int k = 0; k < list5.Count; k++)
			{
				Thing val2 = list5[k];
				if (val2 != null && !val2.Destroyed && !destination.Contains(val2) && !destination.TryAddOrTransfer(val2, false))
				{
					RemoveFromDestination(destination, list5);
					ReturnPreserved(list5, map, rootCell);
					return false;
				}
			}
		}
		made = list5;
		return made.Count > 0;
	}

	private static int SumCounts(List<ThingCount> counts)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		for (int i = 0; i < counts.Count; i++)
		{
			int num2 = num;
			ThingCount val = counts[i];
			num = num2 + Math.Max(0, ((ThingCount)(ref val)).Count);
		}
		return num;
	}

	private static int SumUnits(List<Thing> things)
	{
		int num = 0;
		if (things == null)
		{
			return num;
		}
		for (int i = 0; i < things.Count; i++)
		{
			Thing val = things[i];
			if (val != null && !val.Destroyed)
			{
				num += val.stackCount;
			}
		}
		return num;
	}

	private static void RemoveFromDestination(ThingOwner destination, List<Thing> things)
	{
		if (destination == null || things == null)
		{
			return;
		}
		for (int num = things.Count - 1; num >= 0; num--)
		{
			Thing val = things[num];
			if (val != null && !val.Destroyed && destination.Contains(val))
			{
				try
				{
					destination.Remove(val);
				}
				catch
				{
				}
			}
		}
	}

	private static void ReturnPreserved(List<Thing> preserved, Map map, IntVec3 cell)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		SDS_CompatStock.ReturnPreserved(preserved, map, cell);
	}
}
internal enum SDS_DestinationKind
{
	None,
	Endpoint,
	WarehouseSlotGroup,
	Functional,
	Unknown
}
internal static class SDS_DestinationClassifier
{
	private static readonly HashSet<string> functionalDefNames = new HashSet<string> { "GeneBank", "SDS_HiddenVault" };

	public static SDS_DestinationKind ClassifyThing(Thing thing)
	{
		if (thing == null)
		{
			return SDS_DestinationKind.None;
		}
		if (thing is ISDS_StorageEndpoint)
		{
			return SDS_DestinationKind.Endpoint;
		}
		if (IsFunctionalContainer(thing))
		{
			return SDS_DestinationKind.Functional;
		}
		if (thing is ISlotGroupParent || thing is Building_Storage)
		{
			return SDS_DestinationKind.WarehouseSlotGroup;
		}
		if (thing is IHaulDestination && thing.def != null)
		{
			Log.WarningOnce("[SDS] 未接管未知目的地类型：" + ((Def)thing.def).defName + "（不参与目的地竞争）", ((Def)thing.def).shortHash ^ 0x5D5D);
		}
		return SDS_DestinationKind.Unknown;
	}

	public static SDS_DestinationKind ClassifyCell(Map map, IntVec3 cell)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || !((IntVec3)(ref cell)).IsValid || !GenGrid.InBounds(cell, map))
		{
			return SDS_DestinationKind.None;
		}
		List<Thing> list = map.thingGrid.ThingsListAt(cell);
		for (int i = 0; i < list.Count; i++)
		{
			if (IsFunctionalContainer(list[i]))
			{
				return SDS_DestinationKind.Functional;
			}
		}
		if (map.haulDestinationManager != null && map.haulDestinationManager.SlotGroupAt(cell) != null)
		{
			return SDS_DestinationKind.WarehouseSlotGroup;
		}
		return SDS_DestinationKind.Unknown;
	}

	public static bool IsFunctionalContainer(Thing container)
	{
		if (container == null)
		{
			return false;
		}
		if (container.def != null && functionalDefNames.Contains(((Def)container.def).defName))
		{
			return true;
		}
		if (container is IConstructible)
		{
			return true;
		}
		if (container is IBillGiver)
		{
			return true;
		}
		if (container is Building_Enterable)
		{
			return true;
		}
		if (container is Building_Casket)
		{
			return true;
		}
		if (container is Building_NutrientPasteDispenser || container is Building_FermentingBarrel)
		{
			return true;
		}
		if (container.def != null && ((Def)container.def).defName == "Hopper")
		{
			return true;
		}
		if (container is Building_GeneAssembler)
		{
			return true;
		}
		if (ThingCompUtility.TryGetComp<CompTransporter>(container) != null)
		{
			return true;
		}
		if (ThingCompUtility.TryGetComp<CompRefuelable>(container) != null)
		{
			return true;
		}
		return false;
	}
}
internal static class SDS_DestinationResolver
{
	public static bool TryDecideDeposit(Pawn pawn, Thing thing, Job vanillaJob, bool allowProducedMarked, out SDS_EndpointHandle endpoint)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		endpoint = null;
		if (pawn == null || thing == null || ((Thing)pawn).Map == null)
		{
			return false;
		}
		if (vanillaJob != null && vanillaJob.def == SDS_JobDefOf.DepositToGrid)
		{
			return false;
		}
		if (!TryParseVanillaDestination(pawn, vanillaJob, out var vanillaThing, out var vanillaCell, out var priority))
		{
			return false;
		}
		return TryDecideCore(pawn, thing, vanillaThing, vanillaCell, priority, allowProducedMarked, out endpoint);
	}

	public static bool TryDecideAgainstVanillaBest(Pawn pawn, Thing thing, bool allowProducedMarked, out SDS_EndpointHandle endpoint)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		endpoint = null;
		if (pawn == null || thing == null || ((Thing)pawn).Map == null)
		{
			return false;
		}
		Thing val = null;
		IntVec3 invalid = IntVec3.Invalid;
		StoragePriority vanillaPriority = (StoragePriority)0;
		StoragePriority val2 = StoreUtility.CurrentStoragePriorityOf(thing, false);
		IHaulDestination val3 = default(IHaulDestination);
		if (StoreUtility.TryFindBestBetterStorageFor(thing, pawn, ((Thing)pawn).Map, val2, ((Thing)pawn).Faction, ref invalid, ref val3, false))
		{
			val = (Thing)(object)((val3 is Thing) ? val3 : null);
			if (val == null && !((IntVec3)(ref invalid)).IsValid)
			{
				return false;
			}
			IStoreSettingsParent val4 = (IStoreSettingsParent)(object)val3;
			if (val4 != null)
			{
				vanillaPriority = val4.GetStoreSettings().Priority;
			}
		}
		return TryDecideCore(pawn, thing, val, invalid, vanillaPriority, allowProducedMarked, out endpoint);
	}

	private static bool TryDecideCore(Pawn pawn, Thing thing, Thing vanillaThing, IntVec3 vanillaCell, StoragePriority vanillaPriority, bool allowProducedMarked, out SDS_EndpointHandle endpoint)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Expected I4, but got Unknown
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Expected I4, but got Unknown
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		endpoint = null;
		SDS_DestinationKind sDS_DestinationKind = ((vanillaThing != null) ? SDS_DestinationClassifier.ClassifyThing(vanillaThing) : SDS_DestinationClassifier.ClassifyCell(((Thing)pawn).Map, vanillaCell));
		if (sDS_DestinationKind == SDS_DestinationKind.Functional || sDS_DestinationKind == SDS_DestinationKind.Unknown)
		{
			return false;
		}
		if (!SDS_EndpointResolver.TryFindBest(SDS_EndpointQuery.Deposit(thing, pawn, explicitUi: false, allowProducedMarked), out endpoint, out var _) || endpoint == null)
		{
			return false;
		}
		if (vanillaThing == null && !((IntVec3)(ref vanillaCell)).IsValid)
		{
			return true;
		}
		if (SDS_EndpointPriorityState.IgnorePriority)
		{
			return true;
		}
		int candidatePrio = (int)SDS_EndpointPriorityState.Value;
		int currentPrio = (int)vanillaPriority;
		IntVec3 reference = (thing.SpawnedOrAnyParentSpawned ? thing.PositionHeld : ((Thing)pawn).PositionHeld);
		IntVec3 cell = (((IntVec3)(ref vanillaCell)).IsValid ? vanillaCell : SDS_EndpointHandle.AccessCellOf(vanillaThing));
		float candidateDist = endpoint.RankDistanceSquared(reference);
		float currentDist = SDS_EndpointHandle.DistanceSquared(reference, cell);
		int candidateId = ((endpoint.Thing != null) ? endpoint.Thing.thingIDNumber : int.MaxValue);
		int currentId = vanillaThing?.thingIDNumber ?? int.MaxValue;
		return SDS_SearchRanker.PreferDestination(candidatePrio, candidateDist, candidateId, candidateIsEndpoint: true, currentPrio, currentDist, currentId, currentIsEndpoint: false, hasCurrent: true);
	}

	private static bool TryParseVanillaDestination(Pawn pawn, Job job, out Thing vanillaThing, out IntVec3 vanillaCell, out StoragePriority priority)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Invalid comparison between Unknown and I4
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Expected I4, but got Unknown
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Expected I4, but got Unknown
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Expected I4, but got Unknown
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Expected I4, but got Unknown
		vanillaThing = null;
		vanillaCell = IntVec3.Invalid;
		priority = (StoragePriority)0;
		if (job == null)
		{
			return true;
		}
		if (job.def == JobDefOf.HaulToCell)
		{
			if ((int)job.haulMode != 1)
			{
				return false;
			}
			vanillaCell = ((LocalTargetInfo)(ref job.targetB)).Cell;
			if (!((IntVec3)(ref vanillaCell)).IsValid)
			{
				return true;
			}
			SlotGroup val = ((((Thing)pawn).Map.haulDestinationManager != null) ? ((Thing)pawn).Map.haulDestinationManager.SlotGroupAt(vanillaCell) : null);
			if (val != null)
			{
				priority = (StoragePriority)(int)val.Settings.Priority;
			}
			return true;
		}
		if (job.def == JobDefOf.HaulToContainer)
		{
			vanillaThing = ((LocalTargetInfo)(ref job.targetB)).Thing;
			if (vanillaThing == null)
			{
				return true;
			}
			Thing obj = vanillaThing;
			IStoreSettingsParent val2 = (IStoreSettingsParent)(object)((obj is IStoreSettingsParent) ? obj : null);
			if (val2 != null)
			{
				priority = (StoragePriority)(int)val2.GetStoreSettings().Priority;
			}
			return true;
		}
		if (((Def)job.def).defName == "HaulToInventory")
		{
			vanillaThing = ((LocalTargetInfo)(ref job.targetB)).Thing;
			if (vanillaThing != null)
			{
				Thing obj2 = vanillaThing;
				IStoreSettingsParent val3 = (IStoreSettingsParent)(object)((obj2 is IStoreSettingsParent) ? obj2 : null);
				if (val3 != null)
				{
					priority = (StoragePriority)(int)val3.GetStoreSettings().Priority;
				}
				return true;
			}
			vanillaCell = ((LocalTargetInfo)(ref job.targetB)).Cell;
			if (!((IntVec3)(ref vanillaCell)).IsValid)
			{
				return true;
			}
			SlotGroup val4 = ((((Thing)pawn).Map.haulDestinationManager != null) ? ((Thing)pawn).Map.haulDestinationManager.SlotGroupAt(vanillaCell) : null);
			if (val4 != null)
			{
				priority = (StoragePriority)(int)val4.Settings.Priority;
			}
			return true;
		}
		return false;
	}
}
internal static class SDS_DestinationCommit
{
	public static void Apply(Job job, Thing endpointThing, Thing firstItem)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		if (job != null && endpointThing != null)
		{
			job.targetB = new LocalTargetInfo(endpointThing);
			string text = ((firstItem != null && !firstItem.Destroyed) ? ((Entity)firstItem).LabelShort : string.Empty);
			job.reportStringOverride = ((object)TranslatorFormattedStringExtensions.Translate("SDS.JobReport.Deposit", NamedArgument.op_Implicit(text))/*cast due to constrained. prefix*/).ToString();
		}
	}
}
internal static class SDS_DepositService
{
	private enum CandidateVerdict
	{
		Skip,
		Stop,
		Take
	}

	private const float CandidateRadius = 12f;

	private const int MaxItems = 8;

	public static Job Decide(Pawn pawn, Thing firstThing, bool forced, Job vanillaJob, bool multi)
	{
		if (!CommonGates(pawn, firstThing, forced))
		{
			return null;
		}
		if (!SDS_DestinationResolver.TryDecideDeposit(pawn, firstThing, vanillaJob, forced, out var endpoint))
		{
			return null;
		}
		if (endpoint == null)
		{
			return null;
		}
		return BuildJob(pawn, firstThing, forced, endpoint, multi);
	}

	public static Job TryMakeManual(Pawn pawn, Thing thing)
	{
		if (!ManualGates(pawn, thing))
		{
			return null;
		}
		SDS_EndpointHandle sDS_EndpointHandle = SDS_EndpointHandle.ForPawn(pawn, self: true);
		return (sDS_EndpointHandle != null) ? BuildJob(pawn, thing, forced: true, sDS_EndpointHandle, multi: false) : null;
	}

	public static int DepositNow(Pawn pawn, IEnumerable<Thing> items)
	{
		if (pawn == null || items == null || !SDS_ImplantUtility.IsPortOperational(pawn))
		{
			return 0;
		}
		SDS_EndpointHandle sDS_EndpointHandle = SDS_EndpointHandle.ForPawn(pawn, self: true);
		if (sDS_EndpointHandle == null)
		{
			return 0;
		}
		List<Thing> list = new List<Thing>(items);
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			Thing thing = list[i];
			if (ManualGates(pawn, thing) && sDS_EndpointHandle.TryDeposit(thing, out var accepted) && accepted > 0)
			{
				num += accepted;
			}
		}
		return num;
	}

	private static bool ManualGates(Pawn pawn, Thing thing)
	{
		return pawn != null && thing != null && !thing.Destroyed && SDS_ImplantUtility.IsPortOperational(pawn) && SDS_ChipManualDeposit.CanDepositThing(thing);
	}

	public static bool CanTakeCandidate(Pawn pawn, Thing thing, bool forced, out SDS_EndpointHandle endpoint)
	{
		endpoint = null;
		if (!CommonGates(pawn, thing, forced))
		{
			return false;
		}
		return SDS_DestinationResolver.TryDecideAgainstVanillaBest(pawn, thing, forced, out endpoint);
	}

	public static bool CanCarryCandidate(Pawn pawn, Thing thing, bool forced)
	{
		return CommonGates(pawn, thing, forced);
	}

	public static Job TryMake(Pawn pawn, Thing firstThing, bool forced, Job vanillaJob, bool multi)
	{
		return Decide(pawn, firstThing, forced, vanillaJob, multi);
	}

	public static Job TryMakeCarried(Pawn pawn, Thing carriedThing, Job vanillaJob)
	{
		if (pawn == null || carriedThing == null || ((Thing)pawn).Map == null)
		{
			return null;
		}
		if (((Thing)pawn).Faction != Faction.OfPlayer || pawn.carryTracker == null || pawn.carryTracker.CarriedThing != carriedThing)
		{
			return null;
		}
		if (!SDS_DestinationResolver.TryDecideDeposit(pawn, carriedThing, vanillaJob, allowProducedMarked: false, out var endpoint))
		{
			return null;
		}
		if (endpoint == null)
		{
			return null;
		}
		return BuildJob(pawn, carriedThing, forced: false, endpoint, multi: false);
	}

	public static Job TryMakeFromInventory(Pawn pawn, IList<Thing> items)
	{
		if (pawn == null || items == null || items.Count == 0)
		{
			return null;
		}
		if (((Thing)pawn).Map == null || !((Thing)pawn).Spawned || pawn.inventory == null || pawn.inventory.innerContainer == null)
		{
			return null;
		}
		if (((Thing)pawn).Faction != Faction.OfPlayer || !SDS_ImplantUtility.IsPortOperational(pawn))
		{
			return null;
		}
		Thing val = null;
		SDS_EndpointHandle endpoint = null;
		for (int i = 0; i < items.Count; i++)
		{
			Thing val2 = items[i];
			if (IsInventoryDepositable(pawn, val2))
			{
				if (!SDS_DestinationResolver.TryDecideAgainstVanillaBest(pawn, val2, allowProducedMarked: false, out endpoint) || endpoint == null)
				{
					return null;
				}
				val = val2;
				break;
			}
		}
		if (val == null || endpoint == null)
		{
			return null;
		}
		Job val3 = BuildJob(pawn, val, forced: false, endpoint, multi: false);
		if (val3 == null)
		{
			return null;
		}
		List<Thing> list = new List<Thing>(items.Count);
		for (int j = 0; j < items.Count; j++)
		{
			Thing val4 = items[j];
			if (val4 != null && val4 != val && IsInventoryDepositable(pawn, val4))
			{
				list.Add(val4);
			}
		}
		CollectCompatible(pawn, endpoint, forced: false, val3, list, (Thing _) => CandidateVerdict.Take);
		return val3;
	}

	private static bool IsInventoryDepositable(Pawn pawn, Thing item)
	{
		return item != null && !item.Destroyed && (object)item.holdingOwner == pawn.inventory.innerContainer && SDS_ChipManualDeposit.CanDepositThing(item);
	}

	public static Job TryBuildFromEndpoint(Pawn pawn, Thing firstThing, bool forced, SDS_EndpointHandle endpoint, bool multi)
	{
		if (endpoint == null || !CommonGates(pawn, firstThing, forced) || !endpoint.Accepts(firstThing, forced))
		{
			return null;
		}
		return BuildJob(pawn, firstThing, forced, endpoint, multi);
	}

	private static Job BuildJob(Pawn pawn, Thing firstThing, bool forced, SDS_EndpointHandle endpoint, bool multi)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		if (multi && endpoint.IsPawnPort && !endpoint.IsSelf)
		{
			return null;
		}
		if (SDS_JobDefOf.DepositToGrid == null)
		{
			SDS_Diag.Fail("存入/缺JobDef", SDS_Diag.Ctx(((Thing)pawn).Map, pawn, firstThing));
			return null;
		}
		Job val = JobMaker.MakeJob(SDS_JobDefOf.DepositToGrid, LocalTargetInfo.op_Implicit(firstThing));
		val.count = Math.Max(1, firstThing.stackCount);
		val.playerForced = forced;
		val.targetQueueA = new List<LocalTargetInfo>();
		val.countQueue = new List<int>();
		val.targetQueueA.Add(new LocalTargetInfo(firstThing));
		val.countQueue.Add(Math.Max(1, firstThing.stackCount));
		if (!QueueFitsCapacity(val))
		{
			return null;
		}
		SDS_DestinationCommit.Apply(val, endpoint.Thing, firstThing);
		if (multi)
		{
			CollectCompatibleItems(pawn, firstThing, endpoint, forced, val);
		}
		return val;
	}

	private static void CollectCompatible(Pawn pawn, SDS_EndpointHandle committed, bool forced, Job job, IList<Thing> candidates, Func<Thing, CandidateVerdict> admit)
	{
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		if (candidates == null || admit == null || job == null || job.targetQueueA == null)
		{
			return;
		}
		for (int i = 0; i < candidates.Count; i++)
		{
			if (job.targetQueueA.Count >= 8)
			{
				break;
			}
			Thing val = candidates[i];
			if (val == null)
			{
				continue;
			}
			switch (admit(val))
			{
			case CandidateVerdict.Stop:
				return;
			case CandidateVerdict.Take:
			{
				if (SDS_DestinationResolver.TryDecideAgainstVanillaBest(pawn, val, forced, out var endpoint) && endpoint != null && endpoint.Thing == committed.Thing)
				{
					job.targetQueueA.Add(new LocalTargetInfo(val));
					job.countQueue.Add(Math.Max(1, val.stackCount));
					if (!QueueFitsCapacity(job))
					{
						job.targetQueueA.RemoveAt(job.targetQueueA.Count - 1);
						job.countQueue.RemoveAt(job.countQueue.Count - 1);
					}
				}
				break;
			}
			}
		}
	}

	private static void CollectCompatibleItems(Pawn pawn, Thing firstThing, SDS_EndpointHandle committed, bool forced, Job job)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		List<Thing> list = new List<Thing>(((Thing)pawn).Map.listerHaulables.ThingsPotentiallyNeedingHauling());
		IntVec3 origin = firstThing.PositionHeld;
		list.RemoveAll((Thing t) => t == null || t == firstThing || t.Destroyed || !t.Spawned || t.Map != ((Thing)pawn).Map);
		list.Sort(delegate(Thing a, Thing b)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 val = a.PositionHeld - origin;
			float num = ((IntVec3)(ref val)).LengthHorizontalSquared;
			val = b.PositionHeld - origin;
			float value = ((IntVec3)(ref val)).LengthHorizontalSquared;
			return num.CompareTo(value);
		});
		float radiusSquared = 144f;
		CollectCompatible(pawn, committed, forced, job, list, delegate(Thing candidate)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 val = candidate.PositionHeld - origin;
			if ((float)((IntVec3)(ref val)).LengthHorizontalSquared > radiusSquared)
			{
				return CandidateVerdict.Stop;
			}
			if (!CommonGates(pawn, candidate, forced))
			{
				return CandidateVerdict.Skip;
			}
			if (SDS_ProducedThingMarker.IsMarked(candidate) && !forced)
			{
				return CandidateVerdict.Skip;
			}
			if (StoreUtility.IsInValidBestStorage(candidate))
			{
				return CandidateVerdict.Skip;
			}
			return MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, candidate, candidate.stackCount) ? CandidateVerdict.Stop : CandidateVerdict.Take;
		});
	}

	private static bool QueueFitsCapacity(Job job)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || job == null || job.targetQueueA == null || job.targetQueueA.Count == 0)
		{
			return false;
		}
		List<Thing> list = new List<Thing>(job.targetQueueA.Count);
		for (int i = 0; i < job.targetQueueA.Count; i++)
		{
			LocalTargetInfo val = job.targetQueueA[i];
			Thing thing = ((LocalTargetInfo)(ref val)).Thing;
			if (thing != null && !thing.Destroyed)
			{
				list.Add(thing);
			}
		}
		return list.Count > 0 && instance.CanAcceptAll(list);
	}

	private static bool CommonGates(Pawn pawn, Thing thing, bool forced)
	{
		return HardGates(pawn, thing, forced) && AutoOnlyGates(pawn, thing, forced);
	}

	private static bool HardGates(Pawn pawn, Thing thing, bool forced)
	{
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null || thing == null || ((Thing)pawn).Map == null || !((Thing)pawn).Spawned)
		{
			return false;
		}
		if (((Thing)pawn).Faction != Faction.OfPlayer)
		{
			return false;
		}
		if (pawn.inventory == null || pawn.inventory.innerContainer == null)
		{
			return false;
		}
		if (!thing.Spawned || thing.Map != ((Thing)pawn).Map || thing.Destroyed)
		{
			return false;
		}
		if (!SDS_ChipManualDeposit.CanDepositThing(thing))
		{
			return false;
		}
		if (SDS_ModCompat_CE.CountCanFitInInventory(pawn, thing) <= 0)
		{
			return false;
		}
		Danger val = (Danger)(forced ? 3 : ((int)DangerUtility.NormalMaxDanger(pawn)));
		return ReservationUtility.CanReserveAndReach(pawn, LocalTargetInfo.op_Implicit(thing), (PathEndMode)3, val, 1, Math.Max(1, thing.stackCount), (ReservationLayerDef)null, false);
	}

	private static bool AutoOnlyGates(Pawn pawn, Thing thing, bool forced)
	{
		if (forced)
		{
			return true;
		}
		if (ForbidUtility.IsForbidden(thing, pawn) || SDS_ProducedThingMarker.IsMarked(thing) || StoreUtility.IsInValidBestStorage(thing))
		{
			return false;
		}
		if (!HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, false))
		{
			return false;
		}
		return !SDS_DepositDispatchLedger.IsPending(thing) && !SDS_DepositDispatchLedger.IsFailureCoolingDown(pawn, thing);
	}
}
internal static class SDS_HaulJobDecisionCache
{
	private sealed class Entry
	{
		public int tick;

		public SDS_EndpointHandle endpoint;
	}

	private static readonly Dictionary<Pawn, Dictionary<Thing, Entry>> cache = new Dictionary<Pawn, Dictionary<Thing, Entry>>();

	private static int cacheTick = -1;

	public static void Remember(Pawn pawn, Thing thing, SDS_EndpointHandle endpoint)
	{
		if (pawn != null && thing != null && endpoint != null)
		{
			EnsureTick();
			if (!cache.TryGetValue(pawn, out var value) || value == null)
			{
				value = new Dictionary<Thing, Entry>();
				cache[pawn] = value;
			}
			value[thing] = new Entry
			{
				tick = cacheTick,
				endpoint = endpoint
			};
		}
	}

	public static bool TryGet(Pawn pawn, Thing thing, out SDS_EndpointHandle endpoint)
	{
		endpoint = null;
		if (pawn == null || thing == null)
		{
			return false;
		}
		EnsureTick();
		if (!cache.TryGetValue(pawn, out var value) || value == null)
		{
			return false;
		}
		if (!value.TryGetValue(thing, out var value2) || value2 == null || value2.tick != cacheTick)
		{
			value.Remove(thing);
			return false;
		}
		if (value2.endpoint == null || !SDS_EndpointContract.IsEndpointUsable(value2.endpoint, out var _) || !value2.endpoint.Accepts(thing))
		{
			value.Remove(thing);
			return false;
		}
		endpoint = value2.endpoint;
		return true;
	}

	private static void EnsureTick()
	{
		int ticksGame = GenTicks.TicksGame;
		if (ticksGame != cacheTick)
		{
			cacheTick = ticksGame;
			cache.Clear();
		}
	}
}
internal static class SDS_DepositJobCleanup
{
	public static void OnCleanup(Pawn pawn, Job job, JobCondition condition, bool releaseReservations)
	{
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Invalid comparison between Unknown and I4
		if (pawn == null || job == null || !releaseReservations || job.def != SDS_JobDefOf.DepositToGrid || pawn.inventory == null || pawn.inventory.innerContainer == null || ((Thing)pawn).Map == null)
		{
			return;
		}
		List<Thing> list = new List<Thing>();
		if (job.targetQueueA != null)
		{
			for (int i = 0; i < job.targetQueueA.Count; i++)
			{
				LocalTargetInfo val = job.targetQueueA[i];
				Thing thing = ((LocalTargetInfo)(ref val)).Thing;
				if (thing != null && !thing.Destroyed)
				{
					list.Add(thing);
				}
			}
		}
		Thing thing2 = ((LocalTargetInfo)(ref job.targetA)).Thing;
		if (thing2 != null && !thing2.Destroyed)
		{
			list.Add(thing2);
		}
		for (int j = 0; j < list.Count; j++)
		{
			Thing val2 = list[j];
			if ((object)val2.holdingOwner == pawn.inventory.innerContainer && ((ThingOwner)pawn.inventory.innerContainer).Remove(val2))
			{
				DropAtPawn(pawn, val2);
				if ((int)condition != 2)
				{
					SDS_DepositDispatchLedger.MarkFailure(pawn, val2);
				}
				else
				{
					SDS_DepositDispatchLedger.Clear(val2);
				}
			}
		}
		SDS_ModCompat_CE.NotifyInventoryChanged(pawn);
	}

	private static void DropAtPawn(Pawn pawn, Thing item)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		if (item != null && !item.Destroyed && !item.Spawned)
		{
			SDS_PlacementUtility.TryPlace(item, ((Thing)pawn).Position, ((Thing)pawn).Map, SDS_PlacementPolicy.RandomNear, out var _);
		}
	}
}
public enum SDS_EndpointOp
{
	Deposit,
	Withdraw
}
public enum SDS_EndpointRejectReason
{
	None,
	GridUnavailable,
	NotDepositable,
	NotWithdrawable,
	EndpointUnavailable,
	NoCandidate
}
public struct SDS_EndpointQuery
{
	public SDS_EndpointOp op;

	public Map map;

	public Pawn actor;

	public IntVec3 reference;

	public Thing thing;

	public SDS_StockKey key;

	public Danger danger;

	public bool requireReach;

	public bool allowSelf;

	public bool requireStock;

	public bool bypassPriority;

	public bool allowProducedMarked;

	public static SDS_EndpointQuery Deposit(Thing thing, Pawn actor, bool explicitUi = false, bool allowProducedMarked = false)
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		return new SDS_EndpointQuery
		{
			op = SDS_EndpointOp.Deposit,
			thing = thing,
			actor = actor,
			map = ((actor != null) ? ((Thing)actor).Map : ((thing != null) ? thing.Map : null)),
			reference = ((thing != null && thing.SpawnedOrAnyParentSpawned) ? thing.PositionHeld : ((actor != null) ? ((Thing)actor).Position : IntVec3.Invalid)),
			danger = (Danger)((actor == null) ? 3 : ((int)DangerUtility.NormalMaxDanger(actor))),
			requireReach = (actor != null),
			allowSelf = true,
			bypassPriority = explicitUi,
			allowProducedMarked = allowProducedMarked
		};
	}

	public static SDS_EndpointQuery DepositForNonPawn(Thing thing, Map map, IntVec3 reference)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		SDS_EndpointQuery result = Deposit(thing, null);
		result.map = map;
		result.reference = reference;
		result.requireReach = false;
		result.allowSelf = false;
		return result;
	}

	public static SDS_EndpointQuery WithdrawPoint(Pawn actor)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		return new SDS_EndpointQuery
		{
			op = SDS_EndpointOp.Withdraw,
			actor = actor,
			map = ((actor != null) ? ((Thing)actor).Map : null),
			reference = ((actor != null) ? ((Thing)actor).Position : IntVec3.Invalid),
			danger = (Danger)((actor == null) ? 3 : ((int)DangerUtility.NormalMaxDanger(actor))),
			requireReach = (actor != null),
			allowSelf = true,
			requireStock = false
		};
	}
}
public struct SDS_EndpointDecision
{
	public bool allowed;

	public SDS_EndpointHandle handle;

	public int priority;

	public float distanceSquared;

	public void Reset()
	{
		allowed = false;
		handle = null;
		priority = 0;
		distanceSquared = float.MaxValue;
	}
}
public static class SDS_EndpointPriorityState
{
	public static int Version { get; private set; }

	public static StoragePriority Value
	{
		get
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance != null && instance.HasCoreStoragePriority)
			{
				return instance.CoreStoragePriority;
			}
			return SDS_Config.CoreStoragePriority;
		}
	}

	public static bool IgnorePriority => SDS_Config.IgnorePriorityMode;

	public static void SetValue(StoragePriority value)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance != null)
		{
			instance.SetCoreStoragePriority(value);
		}
		else
		{
			SDS_Config.SetCoreStoragePriority(value);
		}
	}

	public static void NotifyChanged()
	{
		Version++;
		SDS_EndpointContractCache.NotifyPriorityChanged();
	}
}
public static class SDS_EndpointContractCache
{
	public const int DistanceCacheTicks = 30;

	public static int Version { get; private set; }

	public static void Reset()
	{
		Version++;
	}

	public static void NotifyMapChanged()
	{
		Reset();
	}

	public static void NotifyPriorityChanged()
	{
		Reset();
	}

	public static void NotifyEndpointsChanged()
	{
		Reset();
	}
}
public static class SDS_EndpointContract
{
	public static bool BypassPriority(in SDS_EndpointQuery query)
	{
		return query.bypassPriority || SDS_EndpointPriorityState.IgnorePriority;
	}

	public static bool IsCoreUsable(Building_SDS_Core core)
	{
		if (core == null)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			return false;
		}
		return core.Operational && instance.MapHasOperationalCore(((Thing)core).Map);
	}

	public static bool IsAnchorUsable(Building_SDS_Port anchor)
	{
		if (anchor == null)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			return false;
		}
		return anchor.Operational;
	}

	public static bool IsBUsable(Pawn pawn)
	{
		if (pawn == null)
		{
			return false;
		}
		return SDS_ImplantUtility.EffectiveChipDef(pawn) == SDS_HediffDefOf.ChipB && SDS_ImplantUtility.IsPortOperational(pawn);
	}

	public static bool HasUsableBEndpointOn(Map map)
	{
		if (map == null || map.mapPawns == null)
		{
			return false;
		}
		IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
		for (int i = 0; i < allPawnsSpawned.Count; i++)
		{
			Pawn val = allPawnsSpawned[i];
			if (val != null && ((Thing)val).Spawned && ((Thing)val).Map == map && IsBUsable(val))
			{
				return true;
			}
		}
		return false;
	}

	public static bool LocalUsable(Map map)
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available || map == null)
		{
			return false;
		}
		return instance.MapHasOperationalCore(map) || HasUsableBEndpointOn(map);
	}

	public static bool TryGetExposureAnchor(Map map, out ISDS_StorageEndpoint endpoint, out Pawn portPawn)
	{
		endpoint = null;
		portPawn = null;
		if (!LocalUsable(map))
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			return false;
		}
		endpoint = instance.CanonicalEndpointOn(map);
		if (endpoint != null)
		{
			return true;
		}
		portPawn = FirstOperationalPortPawnOn(map);
		return portPawn != null;
	}

	public static Pawn FirstOperationalPortPawnOn(Map map)
	{
		if (map == null || map.mapPawns == null)
		{
			return null;
		}
		IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
		for (int i = 0; i < allPawnsSpawned.Count; i++)
		{
			Pawn val = allPawnsSpawned[i];
			if (val != null && ((Thing)val).Spawned && SDS_ImplantUtility.IsPortOperational(val))
			{
				return val;
			}
		}
		return null;
	}

	public static bool IsRemoteTradeUsable(Pawn negotiator)
	{
		if (negotiator == null || ((Thing)negotiator).Faction != Faction.OfPlayer)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			return false;
		}
		return SDS_ImplantUtility.EffectiveChipDef(negotiator) == SDS_HediffDefOf.ChipB;
	}

	public static bool IsEndpointUsable(SDS_EndpointHandle handle, out SDS_EndpointRejectReason reason)
	{
		if (handle == null)
		{
			reason = SDS_EndpointRejectReason.NoCandidate;
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			reason = SDS_EndpointRejectReason.GridUnavailable;
			return false;
		}
		if (handle.Thing is Building_SDS_Core core)
		{
			if (!IsCoreUsable(core))
			{
				reason = SDS_EndpointRejectReason.EndpointUnavailable;
				return false;
			}
		}
		else if (handle.Thing is Building_SDS_Port anchor)
		{
			if (!IsAnchorUsable(anchor))
			{
				reason = SDS_EndpointRejectReason.EndpointUnavailable;
				return false;
			}
		}
		else
		{
			Thing thing = handle.Thing;
			Pawn val = (Pawn)(object)((thing is Pawn) ? thing : null);
			if (val != null)
			{
				if (!SDS_ImplantUtility.IsPortOperational(val))
				{
					reason = SDS_EndpointRejectReason.EndpointUnavailable;
					return false;
				}
			}
			else if (!handle.Operational)
			{
				reason = SDS_EndpointRejectReason.EndpointUnavailable;
				return false;
			}
		}
		reason = SDS_EndpointRejectReason.None;
		return true;
	}

	public static bool TryEvaluateGate(SDS_EndpointHandle handle, in SDS_EndpointQuery query, out SDS_EndpointRejectReason reason)
	{
		if (handle == null)
		{
			reason = SDS_EndpointRejectReason.NoCandidate;
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			reason = SDS_EndpointRejectReason.GridUnavailable;
			return false;
		}
		if (query.op == SDS_EndpointOp.Deposit)
		{
			if (query.thing == null || query.thing.Destroyed || !instance.CanDepositThing(query.thing))
			{
				reason = SDS_EndpointRejectReason.NotDepositable;
				return false;
			}
			if (!query.allowProducedMarked && SDS_ProducedThingMarker.IsMarked(query.thing))
			{
				reason = SDS_EndpointRejectReason.NotDepositable;
				return false;
			}
		}
		else if (query.op == SDS_EndpointOp.Withdraw && query.requireStock && (query.key == null || SDS_Availability.GetAvailableCount(instance, query.key) <= 0))
		{
			reason = SDS_EndpointRejectReason.NotWithdrawable;
			return false;
		}
		if (!IsEndpointUsable(handle, out reason))
		{
			return false;
		}
		reason = SDS_EndpointRejectReason.None;
		return true;
	}

	public static bool TryEvaluateCore(SDS_EndpointHandle handle, in SDS_EndpointQuery query, out SDS_EndpointDecision decision)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		decision = default(SDS_EndpointDecision);
		decision.Reset();
		decision.handle = handle;
		if (!TryEvaluateGate(handle, in query, out var _))
		{
			return false;
		}
		decision.allowed = true;
		decision.priority = PriorityOf(handle);
		decision.distanceSquared = handle.RankDistanceSquared(query.reference);
		return true;
	}

	public static int PriorityOf(SDS_EndpointHandle handle)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		return (handle != null) ? ((int)SDS_EndpointPriorityState.Value) : 0;
	}

	public static bool IsBetter(in SDS_EndpointDecision candidate, in SDS_EndpointDecision current, bool ignorePriority)
	{
		if (!candidate.allowed)
		{
			return false;
		}
		if (!current.allowed || current.handle == null)
		{
			return true;
		}
		int candidateId = ((candidate.handle == null) ? int.MaxValue : (candidate.handle.IsSelf ? int.MinValue : (candidate.handle.Thing?.thingIDNumber ?? int.MaxValue)));
		int currentId = (current.handle.IsSelf ? int.MinValue : (current.handle.Thing?.thingIDNumber ?? int.MaxValue));
		return SDS_SearchRanker.PreferEndpoint(candidate.priority, candidate.distanceSquared, candidateId, candidate.handle?.IsSelf ?? false, current.priority, current.distanceSquared, currentId, current.handle.IsSelf, hasCurrent: true, ignorePriority);
	}

	public static bool ProbeMap(Map map, out SDS_EndpointRejectReason reason)
	{
		if (map == null)
		{
			reason = SDS_EndpointRejectReason.NoCandidate;
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			reason = SDS_EndpointRejectReason.GridUnavailable;
			return false;
		}
		if (!LocalUsable(map))
		{
			reason = SDS_EndpointRejectReason.NoCandidate;
			return false;
		}
		reason = SDS_EndpointRejectReason.None;
		return true;
	}
}
public class SDS_GameComponent_Grid : GameComponent, IThingHolder
{
	public const int CurrentDataVersion = 1;

	private readonly Dictionary<SDS_StockKey, long> stock = new Dictionary<SDS_StockKey, long>();

	private readonly Dictionary<SDS_StockKey, SDS_StockVitals> stockVitals = new Dictionary<SDS_StockKey, SDS_StockVitals>();

	private List<SDS_StockEntry> stockEntries = new List<SDS_StockEntry>();

	private readonly ThingOwner<Thing> preservedOwner;

	private readonly Dictionary<SDS_StockKey, List<Thing>> preservedByKey = new Dictionary<SDS_StockKey, List<Thing>>();

	private readonly List<SDS_StockKey> preservedKeyOrder = new List<SDS_StockKey>();

	private readonly Dictionary<ThingDef, List<SDS_StockKey>> preservedKeysByInnerDef = new Dictionary<ThingDef, List<SDS_StockKey>>();

	private readonly Dictionary<ThingDef, long> preservedCountByShellDef = new Dictionary<ThingDef, long>();

	private readonly HashSet<Thing> preservedMembers = new HashSet<Thing>();

	private readonly HashSet<Thing> preservedTotalsScratch = new HashSet<Thing>();

	private float preservedWealthCache;

	private bool preservedWealthDirty = true;

	private bool preservedIndexDirty = true;

	private List<Thing> preservedScratch = new List<Thing>();

	private List<Thing> mirrorScratch = new List<Thing>();

	private readonly List<Building_SDS_Core> cores = new List<Building_SDS_Core>();

	private readonly List<Building_SDS_CapacityArray> capacityArrays = new List<Building_SDS_CapacityArray>();

	private readonly List<Building_SDS_Port> ports = new List<Building_SDS_Port>();

	private List<SDS_Reservation> reservations = new List<SDS_Reservation>();

	private List<SDS_ReclaimSave> reclaimScratch = new List<SDS_ReclaimSave>();

	private bool reclaimAuditPending;

	private List<SDS_TransportLoadSave> transportLoadScratch = new List<SDS_TransportLoadSave>();

	private ThingFilter depositFilter;

	private readonly HashSet<SDS_StockKey> forbiddenKeys = new HashSet<SDS_StockKey>();

	private List<SDS_StockKey> forbiddenScratch = new List<SDS_StockKey>();

	private int coreStoragePriority = -1;

	private SDS_SidebarUIState sidebarUI = new SDS_SidebarUIState();

	private int filterVersion;

	private int dataVersion = 1;

	private bool dirty = true;

	private bool cachedAvailable;

	private int cachedTypeCapacity = 30;

	private readonly HashSet<SDS_StockKey> categoryKeys = new HashSet<SDS_StockKey>();

	private bool categoryKeysBuilt;

	private int categoryKeysSignature = int.MinValue;

	private readonly Dictionary<SDS_StockKey, int> categoryGroupRefs = new Dictionary<SDS_StockKey, int>();

	private readonly Dictionary<ThingDef, List<SDS_StockKey>> keysByDef = new Dictionary<ThingDef, List<SDS_StockKey>>();

	private bool keysByDefBuilt;

	private int reservationVersion;

	private int reservedCountsVersion = -1;

	private readonly Dictionary<SDS_StockKey, long> reservedCounts = new Dictionary<SDS_StockKey, long>();

	private readonly HashSet<Map> scanMaps = new HashSet<Map>();

	private readonly HashSet<Map> mapsWithOperationalCore = new HashSet<Map>();

	private bool scanDirty = true;

	private bool loadReportPending;

	private int nextPreservedOfferSweepTick;

	private bool wealthRecountPending;

	private int nextWealthRecountTick;

	private bool apparelNudgePending;

	private int nextApparelNudgeTick;

	private int lastMapSignature = int.MinValue;

	private const int RegistrySweepIntervalTicks = 250;

	private int nextRegistrySweepTick;

	private const int MirrorSyncIntervalTicks = 30;

	private int nextMirrorSyncTick;

	private static readonly List<Building_SDS_Core> sweepCores = new List<Building_SDS_Core>();

	private static readonly List<Building_SDS_Port> sweepPorts = new List<Building_SDS_Port>();

	private static readonly List<Building_SDS_CapacityArray> sweepArrays = new List<Building_SDS_CapacityArray>();

	public static SDS_GameComponent_Grid Instance { get; private set; }

	public int StockVersion { get; private set; }

	public int ReservationVersion => reservationVersion;

	public IThingHolder ParentHolder => null;

	public int FilterVersion => filterVersion;

	public ThingFilter DepositFilter
	{
		get
		{
			if (depositFilter == null)
			{
				depositFilter = BuildDefaultDepositFilter();
			}
			return depositFilter;
		}
	}

	public bool HasCoreStoragePriority => coreStoragePriority >= 0;

	public SDS_SidebarUIState SidebarUI => sidebarUI ?? (sidebarUI = new SDS_SidebarUIState());

	public StoragePriority CoreStoragePriority
	{
		get
		{
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			if (coreStoragePriority < 0)
			{
				return SDS_Config.CoreStoragePriority;
			}
			int num = Math.Min(Math.Max(coreStoragePriority, 1), 5);
			return (StoragePriority)(byte)num;
		}
	}

	public bool Available
	{
		get
		{
			RecomputeIfDirty();
			return cachedAvailable;
		}
	}

	public int TypeCapacity
	{
		get
		{
			RecomputeIfDirty();
			return cachedTypeCapacity;
		}
	}

	public int CategoryCount
	{
		get
		{
			EnsureCategoryKeys();
			return categoryKeys.Count;
		}
	}

	public int PreservedCount => (preservedOwner != null) ? ((ThingOwner)preservedOwner).Count : 0;

	public int UsedCapacity
	{
		get
		{
			RecomputeIfDirty();
			return CategoryCount;
		}
	}

	public int UnresolvedCount
	{
		get
		{
			int num = 0;
			foreach (KeyValuePair<SDS_StockKey, long> item in stock)
			{
				if (item.Value > 0 && item.Key != null && !item.Key.Resolved && item.Key.HasIdentity)
				{
					num++;
				}
			}
			return num;
		}
	}

	public SDS_GameComponent_Grid(Game game)
	{
		Instance = this;
		SDS_StaticState.ResetAll();
		preservedOwner = new ThingOwner<Thing>((IThingHolder)(object)this, false, (LookMode)2, true);
	}

	public void GetChildHolders(List<IThingHolder> outChildren)
	{
	}

	public ThingOwner GetDirectlyHeldThings()
	{
		return (ThingOwner)(object)preservedOwner;
	}

	public void NotifyFilterChanged()
	{
		filterVersion++;
	}

	private ThingFilter BuildDefaultDepositFilter()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected O, but got Unknown
		ThingFilter val = new ThingFilter((Action)NotifyFilterChanged);
		val.SetDisallowAll((IEnumerable<ThingDef>)null, (IEnumerable<SpecialThingFilterDef>)null);
		foreach (ThingDef item in DefDatabase<ThingDef>.AllDefsListForReading)
		{
			if (SDS_DepositRules.AllowedByDefault(item))
			{
				val.SetAllow(item, true);
			}
		}
		return val;
	}

	public bool IsForbidden(SDS_StockKey key)
	{
		return key != null && forbiddenKeys.Count > 0 && forbiddenKeys.Contains(key);
	}

	public void SetForbidden(SDS_StockKey key, bool forbidden)
	{
		if (key != null && key.thingDef != null && (forbidden ? forbiddenKeys.Add(key) : forbiddenKeys.Remove(key)))
		{
			ApplyForbiddenToPreserved(key, forbidden);
			if (key.preserved)
			{
				NotifyPreservedStockChanged();
			}
			else
			{
				NotifyVirtualStockChanged(key);
			}
		}
	}

	private void ApplyForbiddenToPreserved(SDS_StockKey key, bool forbidden)
	{
		if (!key.preserved || preservedOwner == null)
		{
			return;
		}
		EnsurePreservedIndex();
		if (!preservedByKey.TryGetValue(key, out var value) || value == null)
		{
			return;
		}
		for (int i = 0; i < value.Count; i++)
		{
			Thing val = value[i];
			if (val != null && !val.Destroyed)
			{
				ForbidUtility.SetForbidden(val, forbidden, false);
			}
		}
	}

	private void PruneForbiddenKeys()
	{
		if (forbiddenKeys.Count == 0)
		{
			return;
		}
		List<SDS_StockKey> list = null;
		foreach (SDS_StockKey forbiddenKey in forbiddenKeys)
		{
			if (forbiddenKey == null || !forbiddenKey.HasIdentity || (GetCount(forbiddenKey) <= 0 && (!forbiddenKey.preserved || GetPreservedTotalCount(forbiddenKey) <= 0)))
			{
				if (list == null)
				{
					list = new List<SDS_StockKey>();
				}
				list.Add(forbiddenKey);
			}
		}
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				forbiddenKeys.Remove(list[i]);
			}
		}
	}

	public void RegisterPort(Building_SDS_Port building)
	{
		if (building != null && !ports.Contains(building))
		{
			ports.Add(building);
			SDS_EndpointResolver.MarkDirty();
			SDS_MirrorService.NotifyStructureChanged();
		}
	}

	public void UnregisterPort(Building_SDS_Port building)
	{
		if (building != null && ports.Remove(building))
		{
			SDS_EndpointResolver.MarkDirty();
			SDS_MirrorService.NotifyStructureChanged();
		}
	}

	public void RegisterCore(Building_SDS_Core building)
	{
		if (building != null && !cores.Contains(building))
		{
			cores.Add(building);
			dirty = true;
			SDS_EndpointResolver.MarkDirty();
			SDS_MirrorService.NotifyStructureChanged();
		}
	}

	public void UnregisterCore(Building_SDS_Core building)
	{
		if (building != null && cores.Remove(building))
		{
			dirty = true;
			SDS_EndpointResolver.MarkDirty();
			SDS_MirrorService.NotifyStructureChanged();
		}
	}

	public void RegisterCapacityArray(Building_SDS_CapacityArray building)
	{
		if (building != null && !capacityArrays.Contains(building))
		{
			capacityArrays.Add(building);
			dirty = true;
			SDS_MirrorService.NotifyStructureChanged();
		}
	}

	public void UnregisterCapacityArray(Building_SDS_CapacityArray building)
	{
		if (building != null && capacityArrays.Remove(building))
		{
			dirty = true;
			SDS_MirrorService.NotifyStructureChanged();
		}
	}

	public void NotifyStructureChanged()
	{
		dirty = true;
		SDS_EndpointResolver.MarkDirty();
		SDS_MirrorService.NotifyStructureChanged();
	}

	public void NotifyPlayerPresenceChanged()
	{
		scanDirty = true;
		SDS_EndpointResolver.MarkDirty();
		SDS_MirrorService.NotifyStructureChanged();
	}

	public void SetCoreStoragePriority(StoragePriority value)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected I4, but got Unknown
		int num = Math.Min(Math.Max((int)value, 1), 5);
		if (coreStoragePriority != num)
		{
			coreStoragePriority = num;
			SDS_EndpointPriorityState.NotifyChanged();
			ApplyStoragePriorityToEndpoints();
		}
	}

	public void ApplyStoragePriorityToEndpoints()
	{
		bool flag = false;
		for (int i = 0; i < cores.Count; i++)
		{
			flag |= cores[i] != null && cores[i].SyncStoragePriority();
		}
		for (int j = 0; j < ports.Count; j++)
		{
			flag |= ports[j] != null && ports[j].SyncStoragePriority();
		}
		if (!flag || Find.Maps == null)
		{
			return;
		}
		for (int k = 0; k < Find.Maps.Count; k++)
		{
			Map val = Find.Maps[k];
			if (val != null && val.haulDestinationManager != null)
			{
				val.haulDestinationManager.Notify_HaulDestinationChangedPriority();
			}
		}
	}

	private void RebuildScanMaps()
	{
		scanMaps.Clear();
		if (Find.Maps == null)
		{
			return;
		}
		for (int i = 0; i < Find.Maps.Count; i++)
		{
			Map val = Find.Maps[i];
			if (val != null && (val.IsPlayerHome || MapHasPlayerSpawnedPawn(val)))
			{
				scanMaps.Add(val);
			}
		}
	}

	private static bool MapHasPlayerSpawnedPawn(Map map)
	{
		if (map == null || map.mapPawns == null)
		{
			return false;
		}
		foreach (Pawn item in map.mapPawns.AllPawnsSpawned)
		{
			if (item != null && ((Thing)item).Spawned && !item.Dead && ((Thing)item).Faction == Faction.OfPlayer)
			{
				return true;
			}
		}
		return false;
	}

	public override void GameComponentTick()
	{
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Invalid comparison between Unknown and I4
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Invalid comparison between Unknown and I4
		((GameComponent)this).GameComponentTick();
		try
		{
			CheckMapSignature();
			if (reclaimAuditPending)
			{
				reclaimAuditPending = false;
				SDS_WithdrawSave.AuditRestored(this);
			}
			if (loadReportPending && Find.Maps != null && Find.Maps.Count > 0)
			{
				loadReportPending = false;
				LogLoadDiagnostics();
				if (Prefs.DevMode)
				{
					SDS_SelfCheck.Run(verbose: false);
				}
			}
			int num = ((Find.TickManager != null) ? Find.TickManager.TicksGame : 0);
			if (num >= nextRegistrySweepTick)
			{
				nextRegistrySweepTick = num + 250;
				SweepRegistries();
				SDS_ReclaimRegistry.Sweep(num);
			}
			if (num >= nextMirrorSyncTick)
			{
				nextMirrorSyncTick = num + 30;
				SDS_MirrorService.SyncIfDirty();
			}
			if (num >= nextPreservedOfferSweepTick)
			{
				nextPreservedOfferSweepTick = num + 60;
				SDS_PreservedOffer.Tick();
			}
			if (apparelNudgePending && num >= nextApparelNudgeTick && (int)Current.ProgramState == 2)
			{
				apparelNudgePending = false;
				nextApparelNudgeTick = num + 600;
				Map anyPlayerHomeMap = Find.AnyPlayerHomeMap;
				if (anyPlayerHomeMap != null && anyPlayerHomeMap.mapPawns != null)
				{
					List<Pawn> list = anyPlayerHomeMap.mapPawns.PawnsInFaction(Faction.OfPlayer);
					for (int i = 0; i < list.Count; i++)
					{
						Pawn val = list[i];
						if (val != null && ((Thing)val).Spawned && !val.Dead && val.RaceProps != null && val.RaceProps.Humanlike && val.mindState != null)
						{
							val.mindState.nextApparelOptimizeTick = num;
						}
					}
				}
			}
			if (wealthRecountPending && num >= nextWealthRecountTick && (int)Current.ProgramState == 2)
			{
				wealthRecountPending = false;
				nextWealthRecountTick = num + 300;
				Map anyPlayerHomeMap2 = Find.AnyPlayerHomeMap;
				if (anyPlayerHomeMap2 != null && anyPlayerHomeMap2.wealthWatcher != null)
				{
					anyPlayerHomeMap2.wealthWatcher.ForceRecount(false);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("超维存储 GameComponentTick 失败（本 tick 的端点 / 镜像刷新已跳过）", exception);
		}
	}

	private static int ComputeMapSignature()
	{
		List<Map> maps = Find.Maps;
		if (maps == null)
		{
			return 0;
		}
		int num = 17;
		num = num * 31 + maps.Count;
		for (int i = 0; i < maps.Count; i++)
		{
			num = num * 31 + (maps[i]?.uniqueID ?? 0);
		}
		return num;
	}

	private void CheckMapSignature()
	{
		int num = ComputeMapSignature();
		if (num != lastMapSignature)
		{
			lastMapSignature = num;
			SDS_EndpointContractCache.NotifyMapChanged();
			SDS_EndpointResolver.MarkDirty();
			SDS_MirrorService.NotifyStructureChanged();
			scanDirty = true;
			dirty = true;
		}
	}

	private void SweepRegistries()
	{
		if (Find.Maps == null)
		{
			return;
		}
		sweepCores.Clear();
		sweepPorts.Clear();
		sweepArrays.Clear();
		List<Building_SDS_Core> list = sweepCores;
		List<Building_SDS_Port> list2 = sweepPorts;
		List<Building_SDS_CapacityArray> list3 = sweepArrays;
		for (int i = 0; i < Find.Maps.Count; i++)
		{
			Map val = Find.Maps[i];
			if (val == null || val.listerThings == null)
			{
				continue;
			}
			List<Thing> list4 = val.listerThings.ThingsInGroup((ThingRequestGroup)10);
			for (int j = 0; j < list4.Count; j++)
			{
				Thing val2 = list4[j];
				if (val2 != null && !val2.Destroyed)
				{
					if (val2 is Building_SDS_Core item)
					{
						list.Add(item);
					}
					else if (val2 is Building_SDS_Port item2)
					{
						list2.Add(item2);
					}
					else if (val2 is Building_SDS_CapacityArray item3)
					{
						list3.Add(item3);
					}
				}
			}
		}
		bool flag = PruneStale<Building_SDS_Core>(cores, list);
		flag |= PruneStale(ports, list2);
		if (flag | PruneStale(capacityArrays, list3))
		{
			dirty = true;
		}
		scanDirty = true;
		SDS_MirrorService.NotifyStructureChanged();
		SDS_EndpointResolver.MarkDirty();
		ApplyStoragePriorityToEndpoints();
	}

	private static bool PruneStale<T>(List<T> registered, List<T> live) where T : Building
	{
		if (registered == null || live == null)
		{
			return false;
		}
		HashSet<T> hashSet = new HashSet<T>(live);
		HashSet<T> hashSet2 = new HashSet<T>(registered);
		bool result = false;
		for (int num = registered.Count - 1; num >= 0; num--)
		{
			T val = registered[num];
			if (val == null || ((Thing)(object)val).Destroyed || ((Thing)(object)val).Map == null || !hashSet.Contains(val))
			{
				hashSet2.Remove(val);
				registered.RemoveAt(num);
				result = true;
			}
		}
		for (int i = 0; i < live.Count; i++)
		{
			T val2 = live[i];
			if (val2 != null && hashSet2.Add(val2))
			{
				registered.Add(val2);
				result = true;
			}
		}
		return result;
	}

	private void RecomputeIfDirty()
	{
		if (!dirty && !scanDirty)
		{
			return;
		}
		bool flag = scanDirty || scanMaps.Count == 0;
		dirty = false;
		scanDirty = false;
		if (flag)
		{
			RebuildScanMaps();
		}
		mapsWithOperationalCore.Clear();
		for (int num = ports.Count - 1; num >= 0; num--)
		{
			Building_SDS_Port building_SDS_Port = ports[num];
			if (building_SDS_Port == null || ((Thing)building_SDS_Port).Destroyed)
			{
				ports.RemoveAt(num);
			}
		}
		int num2 = 0;
		for (int num3 = capacityArrays.Count - 1; num3 >= 0; num3--)
		{
			Building_SDS_CapacityArray building_SDS_CapacityArray = capacityArrays[num3];
			if (building_SDS_CapacityArray == null || ((Thing)building_SDS_CapacityArray).Destroyed)
			{
				capacityArrays.RemoveAt(num3);
			}
			else if (building_SDS_CapacityArray.Operational && ((Thing)building_SDS_CapacityArray).Map != null && scanMaps.Contains(((Thing)building_SDS_CapacityArray).Map))
			{
				num2++;
			}
		}
		int num4 = 0;
		for (int num5 = cores.Count - 1; num5 >= 0; num5--)
		{
			Building_SDS_Core building_SDS_Core = cores[num5];
			if (building_SDS_Core == null || ((Thing)building_SDS_Core).Destroyed)
			{
				cores.RemoveAt(num5);
			}
			else if (building_SDS_Core.Operational && ((Thing)building_SDS_Core).Map != null && scanMaps.Contains(((Thing)building_SDS_Core).Map))
			{
				num4++;
				mapsWithOperationalCore.Add(((Thing)building_SDS_Core).Map);
			}
		}
		cachedAvailable = num4 > 0;
		cachedTypeCapacity = SDS_Config.GridCapacity + num2 * SDS_Config.CapacityPerArray;
	}

	public bool MapHasOperationalCore(Map map)
	{
		if (map == null)
		{
			return false;
		}
		RecomputeIfDirty();
		return mapsWithOperationalCore.Contains(map);
	}

	public List<Building_SDS_Core> AllCoresOn(Map map)
	{
		List<Building_SDS_Core> list = new List<Building_SDS_Core>();
		if (map == null)
		{
			return list;
		}
		for (int i = 0; i < cores.Count; i++)
		{
			Building_SDS_Core building_SDS_Core = cores[i];
			if (building_SDS_Core != null && ((Thing)building_SDS_Core).Spawned && ((Thing)building_SDS_Core).Map == map)
			{
				list.Add(building_SDS_Core);
			}
		}
		return list;
	}

	public List<Building_SDS_Port> AllPortsOn(Map map)
	{
		List<Building_SDS_Port> list = new List<Building_SDS_Port>();
		if (map == null)
		{
			return list;
		}
		for (int i = 0; i < ports.Count; i++)
		{
			Building_SDS_Port building_SDS_Port = ports[i];
			if (building_SDS_Port != null && ((Thing)building_SDS_Port).Spawned && ((Thing)building_SDS_Port).Map == map)
			{
				list.Add(building_SDS_Port);
			}
		}
		return list;
	}

	public ISDS_StorageEndpoint CanonicalEndpointOn(Map map)
	{
		if (map == null || !Available)
		{
			return null;
		}
		Thing val = null;
		for (int i = 0; i < cores.Count; i++)
		{
			Building_SDS_Core building_SDS_Core = cores[i];
			if (building_SDS_Core != null && ((Thing)building_SDS_Core).Spawned && ((Thing)building_SDS_Core).Map == map && SDS_EndpointContract.IsCoreUsable(building_SDS_Core))
			{
				val = Earlier(val, (Thing)(object)building_SDS_Core);
			}
		}
		for (int j = 0; j < ports.Count; j++)
		{
			Building_SDS_Port building_SDS_Port = ports[j];
			if (building_SDS_Port != null && ((Thing)building_SDS_Port).Spawned && ((Thing)building_SDS_Port).Map == map && SDS_EndpointContract.IsAnchorUsable(building_SDS_Port))
			{
				val = Earlier(val, (Thing)(object)building_SDS_Port);
			}
		}
		return val as ISDS_StorageEndpoint;
	}

	private static Thing Earlier(Thing a, Thing b)
	{
		return (a == null || b.thingIDNumber < a.thingIDNumber) ? b : a;
	}

	public long GetCount(SDS_StockKey key)
	{
		if (key == null)
		{
			return 0L;
		}
		long value;
		return stock.TryGetValue(key, out value) ? value : 0;
	}

	public static SDS_StockKey CategoryKeyOf(SDS_StockKey key)
	{
		if (key == null)
		{
			return null;
		}
		bool separateByQuality = SDS_Config.SeparateByQuality;
		bool separateByStuff = SDS_Config.SeparateByStuff;
		if (separateByQuality && separateByStuff)
		{
			return key;
		}
		return new SDS_StockKey(key.thingDef, separateByStuff ? key.stuff : null, separateByQuality ? key.qualityInt : (-1), key.preserved);
	}

	public bool ContainsKnownKey(SDS_StockKey key)
	{
		if (key == null || key.thingDef == null)
		{
			return false;
		}
		if (!key.preserved)
		{
			return stock.ContainsKey(key) && GetCount(key) > 0;
		}
		if (preservedOwner == null)
		{
			return false;
		}
		EnsurePreservedIndex();
		long total;
		long available;
		return preservedByKey.ContainsKey(key) && TryGetPreservedTotals(key, out total, out available) && total > 0;
	}

	public bool TryNormalizeRowKey(SDS_StockKey key, out SDS_StockKey realKey)
	{
		realKey = null;
		if (key == null || key.thingDef == null)
		{
			return false;
		}
		if (ContainsKnownKey(key))
		{
			realKey = key;
			return true;
		}
		if (key.preserved)
		{
			if (preservedOwner == null)
			{
				return false;
			}
			EnsurePreservedIndex();
			if (!preservedKeysByInnerDef.TryGetValue(key.thingDef, out var value) || value == null)
			{
				return false;
			}
			SDS_StockKey sDS_StockKey = null;
			for (int i = 0; i < value.Count; i++)
			{
				SDS_StockKey sDS_StockKey2 = value[i];
				if (sDS_StockKey2 != null && TryGetPreservedTotals(sDS_StockKey2, out var total, out var _) && total > 0 && (sDS_StockKey == null || SDS_StockKey.CompareIdentity(sDS_StockKey2, sDS_StockKey) < 0))
				{
					sDS_StockKey = sDS_StockKey2;
				}
			}
			if (sDS_StockKey == null)
			{
				return false;
			}
			realKey = sDS_StockKey;
			return true;
		}
		List<SDS_StockKey> list = new List<SDS_StockKey>();
		CopyKeysForDef(key.thingDef, list);
		SDS_StockKey sDS_StockKey3 = null;
		for (int j = 0; j < list.Count; j++)
		{
			SDS_StockKey sDS_StockKey4 = list[j];
			if (sDS_StockKey4 != null && !sDS_StockKey4.preserved && GetCount(sDS_StockKey4) > 0 && (sDS_StockKey3 == null || SDS_StockKey.CompareIdentity(sDS_StockKey4, sDS_StockKey3) < 0))
			{
				sDS_StockKey3 = sDS_StockKey4;
			}
		}
		if (sDS_StockKey3 == null)
		{
			return false;
		}
		realKey = sDS_StockKey3;
		return true;
	}

	private void EnsureCategoryKeys()
	{
		int num = (SDS_Config.SeparateByQuality ? 1 : 0) | (SDS_Config.SeparateByStuff ? 2 : 0);
		if (categoryKeysBuilt && num == categoryKeysSignature)
		{
			return;
		}
		categoryKeysBuilt = true;
		categoryKeysSignature = num;
		categoryKeys.Clear();
		categoryGroupRefs.Clear();
		foreach (KeyValuePair<SDS_StockKey, long> item in stock)
		{
			SDS_StockKey key = item.Key;
			if (key != null && key.thingDef != null && item.Value > 0)
			{
				AddCategoryRef(CategoryKeyOf(key));
			}
		}
		if (preservedOwner == null)
		{
			return;
		}
		EnsurePreservedIndex();
		for (int i = 0; i < preservedKeyOrder.Count; i++)
		{
			SDS_StockKey sDS_StockKey = preservedKeyOrder[i];
			if (sDS_StockKey != null && sDS_StockKey.thingDef != null)
			{
				AddCategoryRef(CategoryKeyOf(sDS_StockKey));
			}
		}
	}

	private void AddCategoryRef(SDS_StockKey groupKey)
	{
		if (groupKey != null)
		{
			categoryGroupRefs.TryGetValue(groupKey, out var value);
			categoryGroupRefs[groupKey] = value + 1;
			if (value == 0)
			{
				categoryKeys.Add(groupKey);
			}
		}
	}

	private void RemoveCategoryRef(SDS_StockKey groupKey)
	{
		if (groupKey != null && categoryGroupRefs.TryGetValue(groupKey, out var value) && value > 0)
		{
			if (value <= 1)
			{
				categoryGroupRefs.Remove(groupKey);
				categoryKeys.Remove(groupKey);
			}
			else
			{
				categoryGroupRefs[groupKey] = value - 1;
			}
		}
	}

	private bool CategoryExists(SDS_StockKey key)
	{
		EnsureCategoryKeys();
		return categoryKeys.Contains(CategoryKeyOf(key));
	}

	private void NotifyStockChanged()
	{
		StockVersion++;
		categoryKeysBuilt = false;
		wealthRecountPending = true;
		apparelNudgePending = true;
		SDS_MirrorService.NotifyStockChanged();
	}

	private void NotifyVirtualStockChanged(SDS_StockKey key)
	{
		StockVersion++;
		wealthRecountPending = true;
		if (key != null && key.thingDef != null && key.thingDef.IsApparel)
		{
			apparelNudgePending = true;
		}
		SDS_MirrorService.NotifyStockChanged(key);
	}

	private void NotifyPreservedStockChanged()
	{
		StockVersion++;
		categoryKeysBuilt = false;
		wealthRecountPending = true;
		apparelNudgePending = true;
	}

	public bool CanDeposit(SDS_StockKey key, int count)
	{
		if (key == null || key.thingDef == null || count <= 0)
		{
			return false;
		}
		if (!SDS_DepositRules.CanStoreDef(key.thingDef))
		{
			return false;
		}
		if (key.preserved || SDS_DepositRules.RequiresPreservedStorage(key.thingDef))
		{
			return false;
		}
		if (!key.thingDef.EverHaulable)
		{
			return false;
		}
		RecomputeIfDirty();
		int usedCapacity = UsedCapacity;
		if (usedCapacity > cachedTypeCapacity)
		{
			return false;
		}
		if (CategoryExists(key))
		{
			return true;
		}
		return usedCapacity < cachedTypeCapacity;
	}

	public bool TryAddCount(SDS_StockKey key, int count)
	{
		return TryAddCount(key, count, -1f, -1f);
	}

	public bool TryAddCount(SDS_StockKey key, int count, float addHitPoints, float addRotProgress)
	{
		if (!CanDeposit(key, count))
		{
			return false;
		}
		long count2 = GetCount(key);
		stock[key] = count2 + count;
		if (count2 <= 0)
		{
			IndexKey(key);
			if (categoryKeysBuilt)
			{
				AddCategoryRef(CategoryKeyOf(key));
			}
		}
		MergeVitals(key, count2, count, addHitPoints, addRotProgress);
		NotifyVirtualStockChanged(key);
		return true;
	}

	private void MergeVitals(SDS_StockKey key, long oldCount, long addCount, float addHitPoints, float addRotProgress)
	{
		if (key == null || key.thingDef == null || (addHitPoints < 0f && addRotProgress < 0f))
		{
			return;
		}
		long num = oldCount + addCount;
		if (num > 0)
		{
			if (!stockVitals.TryGetValue(key, out var value) || value == null)
			{
				value = new SDS_StockVitals();
				stockVitals[key] = value;
			}
			if (addHitPoints >= 0f && key.thingDef.useHitPoints)
			{
				float num2 = ((value.hitPoints >= 0f) ? value.hitPoints : DefaultHitPoints(key));
				value.hitPoints = ((oldCount <= 0) ? addHitPoints : ((num2 * (float)oldCount + addHitPoints * (float)addCount) / (float)num));
			}
			if (addRotProgress >= 0f)
			{
				float num3 = ((value.rotProgress >= 0f) ? value.rotProgress : 0f);
				value.rotProgress = ((oldCount <= 0) ? addRotProgress : ((num3 * (float)oldCount + addRotProgress * (float)addCount) / (float)num));
			}
		}
	}

	private static float DefaultHitPoints(SDS_StockKey key)
	{
		if (key == null || key.thingDef == null || !key.thingDef.useHitPoints)
		{
			return -1f;
		}
		return StatExtension.GetStatValueAbstract((BuildableDef)(object)key.thingDef, StatDefOf.MaxHitPoints, key.stuff);
	}

	public void ApplyStoredVitals(SDS_StockKey key, Thing thing)
	{
		if (key == null || key.preserved || thing == null || thing.def == null || !stockVitals.TryGetValue(key, out var value) || value == null)
		{
			return;
		}
		if (value.hitPoints >= 0f && thing.def.useHitPoints)
		{
			int maxHitPoints = thing.MaxHitPoints;
			int num = (int)Math.Ceiling(value.hitPoints);
			if (num < 1)
			{
				num = 1;
			}
			if (maxHitPoints > 0 && num > maxHitPoints)
			{
				num = maxHitPoints;
			}
			thing.HitPoints = num;
		}
		if (value.rotProgress >= 0f)
		{
			CompRottable val = ThingCompUtility.TryGetComp<CompRottable>(thing);
			if (val != null)
			{
				val.RotProgress = value.rotProgress;
			}
		}
	}

	private void RemoveStock(SDS_StockKey key)
	{
		UnindexKey(key);
		if (categoryKeysBuilt && key != null)
		{
			RemoveCategoryRef(CategoryKeyOf(key));
		}
		if (key != null)
		{
			forbiddenKeys.Remove(key);
		}
		stock.Remove(key);
		stockVitals.Remove(key);
	}

	public bool TryDebitCount(SDS_StockKey key, int count)
	{
		if (!Available)
		{
			return false;
		}
		if (key == null || key.thingDef == null || count <= 0)
		{
			return false;
		}
		long count2 = GetCount(key);
		if (count2 <= 0)
		{
			return false;
		}
		int num = (int)Math.Min(count, count2);
		if (num <= 0)
		{
			return false;
		}
		stock[key] = count2 - num;
		NotifyVirtualStockChanged(key);
		if (stock[key] <= 0)
		{
			RemoveStock(key);
		}
		return true;
	}

	private bool PassesFilter(Thing thing)
	{
		if (thing == null || thing.def == null)
		{
			return false;
		}
		MinifiedThing val = (MinifiedThing)(object)((thing is MinifiedThing) ? thing : null);
		if (val != null)
		{
			return val.InnerThing != null && DepositFilter.Allows(val.InnerThing);
		}
		return DepositFilter.Allows(thing);
	}

	public bool CanDepositThing(Thing thing)
	{
		if (SDS_MirrorRegistry.IsMirror(thing))
		{
			return false;
		}
		if (!SDS_DepositRules.CanStore(thing))
		{
			return false;
		}
		if (!PassesFilter(thing))
		{
			return false;
		}
		if (SDS_DepositRules.RequiresPreservedStorage(thing))
		{
			return CanPreserve(SDS_StockKey.FromPreservedThing(thing));
		}
		return CanDeposit(SDS_StockKey.FromThing(thing), thing.stackCount);
	}

	public bool CanAcceptAll(IEnumerable<Thing> things)
	{
		if (things == null || !Available)
		{
			return false;
		}
		RecomputeIfDirty();
		HashSet<SDS_StockKey> hashSet = new HashSet<SDS_StockKey>();
		bool flag = false;
		foreach (Thing thing in things)
		{
			if (thing != null && !thing.Destroyed)
			{
				flag = true;
				if (!CanDepositThing(thing))
				{
					return false;
				}
				SDS_StockKey sDS_StockKey = (SDS_DepositRules.RequiresPreservedStorage(thing) ? SDS_StockKey.FromPreservedThing(thing) : SDS_StockKey.FromThing(thing));
				if (sDS_StockKey != null && !CategoryExists(sDS_StockKey))
				{
					hashSet.Add(sDS_StockKey);
				}
			}
		}
		if (!flag)
		{
			return false;
		}
		return UsedCapacity + hashSet.Count <= cachedTypeCapacity;
	}

	public bool TryDeposit(Thing thing, out int accepted)
	{
		accepted = 0;
		if (SDS_MirrorRegistry.IsMirror(thing))
		{
			return false;
		}
		if (!Available)
		{
			return false;
		}
		if (!CanDepositThing(thing))
		{
			return false;
		}
		if (SDS_DepositRules.RequiresPreservedStorage(thing))
		{
			return TryDepositPreserved(thing, out accepted);
		}
		SDS_StockKey key = SDS_StockKey.FromThing(thing);
		int stackCount = thing.stackCount;
		float addHitPoints = SDS_StockVitals.CaptureHitPoints(thing);
		float addRotProgress = SDS_StockVitals.CaptureRotProgress(thing);
		if (!TryAddCount(key, stackCount, addHitPoints, addRotProgress))
		{
			return false;
		}
		accepted = stackCount;
		thing.Destroy((DestroyMode)0);
		return true;
	}

	public bool TryWithdraw(SDS_StockKey key, int requested, out List<Thing> result)
	{
		result = null;
		if (!Available)
		{
			return false;
		}
		if (key == null || key.thingDef == null || requested <= 0)
		{
			return false;
		}
		long count = GetCount(key);
		if (count <= 0)
		{
			return false;
		}
		int num = (int)Math.Min(requested, count);
		if (num <= 0)
		{
			return false;
		}
		int val = Math.Max(1, key.thingDef.stackLimit);
		List<Thing> list = new List<Thing>();
		int num2 = num;
		int num3 = 0;
		while (num2 > 0)
		{
			int num4 = Math.Min(num2, val);
			Thing val2 = ThingMaker.MakeThing(key.thingDef, key.stuff);
			if (val2 == null)
			{
				break;
			}
			val2.stackCount = num4;
			if (key.qualityInt >= 0)
			{
				CompQuality val3 = ThingCompUtility.TryGetComp<CompQuality>(val2);
				if (val3 != null)
				{
					val3.SetQuality((QualityCategory)(byte)key.qualityInt, (ArtGenerationContext?)null);
				}
			}
			ApplyStoredVitals(key, val2);
			list.Add(val2);
			num3 += num4;
			num2 -= num4;
		}
		if (num3 <= 0)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] != null && !list[i].Destroyed)
				{
					list[i].Destroy((DestroyMode)0);
				}
			}
			return false;
		}
		stock[key] = count - num3;
		NotifyVirtualStockChanged(key);
		if (stock[key] <= 0)
		{
			RemoveStock(key);
		}
		result = list;
		return true;
	}

	private void CommitReservations(List<SDS_Reservation> additions)
	{
		if (additions == null || additions.Count == 0)
		{
			return;
		}
		for (int i = 0; i < additions.Count; i++)
		{
			if (additions[i] != null)
			{
				reservations.Add(additions[i]);
			}
		}
		reservationVersion++;
		reservedCountsVersion = -1;
	}

	public long GetReservedCount(SDS_StockKey key)
	{
		if (key == null)
		{
			return 0L;
		}
		if (reservedCountsVersion != reservationVersion)
		{
			reservedCounts.Clear();
			for (int i = 0; i < reservations.Count; i++)
			{
				SDS_Reservation sDS_Reservation = reservations[i];
				if (sDS_Reservation != null && sDS_Reservation.count > 0 && sDS_Reservation.key != null)
				{
					reservedCounts.TryGetValue(sDS_Reservation.key, out var value);
					reservedCounts[sDS_Reservation.key] = value + sDS_Reservation.count;
				}
			}
			reservedCountsVersion = reservationVersion;
		}
		long value2;
		return reservedCounts.TryGetValue(key, out value2) ? value2 : 0;
	}

	public long GetReservedCount(int transactionId, SDS_StockKey key)
	{
		if (transactionId == 0 || key == null)
		{
			return 0L;
		}
		long num = 0L;
		for (int i = 0; i < reservations.Count; i++)
		{
			SDS_Reservation sDS_Reservation = reservations[i];
			if (sDS_Reservation != null && sDS_Reservation.jobLoadID == transactionId && sDS_Reservation.key != null && sDS_Reservation.count > 0 && sDS_Reservation.key.SameAs(key))
			{
				num += sDS_Reservation.count;
			}
		}
		return num;
	}

	public long GetAvailableCount(SDS_StockKey key)
	{
		if (key == null || !key.Resolved)
		{
			return 0L;
		}
		if (key.preserved)
		{
			long total;
			long available;
			long num = (TryGetPreservedTotals(key, out total, out available) ? available : 0);
			SDS_Invariants.AvailableWithinTotal(key, total, num);
			return num;
		}
		long count = GetCount(key);
		long num2 = count - GetReservedCount(key);
		if (num2 < 0)
		{
			num2 = 0L;
		}
		SDS_Invariants.AvailableWithinTotal(key, count, num2);
		return num2;
	}

	public long GetAvailableCount(ThingDef def)
	{
		if (def == null)
		{
			return 0L;
		}
		EnsureKeysByDef();
		long num = 0L;
		if (keysByDef.TryGetValue(def, out var value) && value != null)
		{
			for (int i = 0; i < value.Count; i++)
			{
				if (!IsForbidden(value[i]))
				{
					num += GetAvailableCount(value[i]);
				}
			}
		}
		if (preservedOwner != null && ((ThingOwner)preservedOwner).Count > 0)
		{
			EnsurePreservedIndex();
			if (preservedKeysByInnerDef.TryGetValue(def, out var value2) && value2 != null)
			{
				for (int j = 0; j < value2.Count; j++)
				{
					SDS_StockKey sDS_StockKey = value2[j];
					if (sDS_StockKey != null && !IsForbidden(sDS_StockKey) && TryGetPreservedTotals(sDS_StockKey, out var _, out var available) && available > 0)
					{
						num += available;
					}
				}
			}
		}
		return num;
	}

	public bool TryReserveWithdrawCounts(int transactionId, List<SDS_StockKey> keys, List<int> counts, Pawn pawn)
	{
		if (transactionId == 0 || keys == null || counts == null || keys.Count != counts.Count)
		{
			return false;
		}
		if (!Available)
		{
			return false;
		}
		Dictionary<SDS_StockKey, int> dictionary = new Dictionary<SDS_StockKey, int>();
		for (int i = 0; i < keys.Count; i++)
		{
			SDS_StockKey sDS_StockKey = keys[i];
			int num = counts[i];
			if (sDS_StockKey != null && num > 0)
			{
				dictionary.TryGetValue(sDS_StockKey, out var value);
				dictionary[sDS_StockKey] = value + num;
			}
		}
		if (dictionary.Count == 0)
		{
			return false;
		}
		foreach (KeyValuePair<SDS_StockKey, int> item in dictionary)
		{
			if (GetAvailableCount(item.Key) < item.Value)
			{
				return false;
			}
		}
		List<SDS_Reservation> list = new List<SDS_Reservation>(dictionary.Count);
		foreach (KeyValuePair<SDS_StockKey, int> item2 in dictionary)
		{
			list.Add(new SDS_Reservation
			{
				jobLoadID = transactionId,
				key = item2.Key,
				count = item2.Value,
				pawn = pawn
			});
		}
		CommitReservations(list);
		return true;
	}

	public long GetTotalCountForDef(ThingDef def)
	{
		if (def == null)
		{
			return 0L;
		}
		EnsureKeysByDef();
		if (!keysByDef.TryGetValue(def, out var value) || value == null)
		{
			return 0L;
		}
		long num = 0L;
		for (int i = 0; i < value.Count; i++)
		{
			num += GetCount(value[i]);
		}
		return num;
	}

	public void CopyKeysForDef(ThingDef def, List<SDS_StockKey> into)
	{
		if (into == null)
		{
			return;
		}
		into.Clear();
		if (def != null)
		{
			EnsureKeysByDef();
			if (keysByDef.TryGetValue(def, out var value) && value != null)
			{
				into.AddRange(value);
			}
		}
	}

	public long GetPreservedCountOfDef(ThingDef def)
	{
		if (def == null || preservedOwner == null)
		{
			return 0L;
		}
		EnsurePreservedIndex();
		long value;
		return preservedCountByShellDef.TryGetValue(def, out value) ? value : 0;
	}

	private void MarkPreservedIndexDirty()
	{
		preservedIndexDirty = true;
		preservedWealthDirty = true;
	}

	private void EnsurePreservedIndex()
	{
		if (!preservedIndexDirty)
		{
			return;
		}
		preservedIndexDirty = false;
		preservedByKey.Clear();
		preservedKeyOrder.Clear();
		preservedKeysByInnerDef.Clear();
		preservedCountByShellDef.Clear();
		preservedMembers.Clear();
		if (preservedOwner != null)
		{
			for (int i = 0; i < ((ThingOwner)preservedOwner).Count; i++)
			{
				IndexPreservedThing(preservedOwner[i]);
			}
		}
	}

	private void IndexPreservedThing(Thing thing)
	{
		preservedWealthDirty = true;
		if (!IsLivePreservedContent(thing) || !preservedMembers.Add(thing))
		{
			return;
		}
		if (thing.def != null)
		{
			preservedCountByShellDef.TryGetValue(thing.def, out var value);
			preservedCountByShellDef[thing.def] = value + thing.stackCount;
		}
		SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(thing);
		if (sDS_StockKey == null || sDS_StockKey.thingDef == null)
		{
			return;
		}
		if (!preservedByKey.TryGetValue(sDS_StockKey, out var value2))
		{
			value2 = new List<Thing>();
			preservedByKey[sDS_StockKey] = value2;
			preservedKeyOrder.Add(sDS_StockKey);
			if (!preservedKeysByInnerDef.TryGetValue(sDS_StockKey.thingDef, out var value3))
			{
				value3 = new List<SDS_StockKey>();
				preservedKeysByInnerDef[sDS_StockKey.thingDef] = value3;
			}
			value3.Add(sDS_StockKey);
		}
		value2.Add(thing);
	}

	private void IndexPreservedAbsorbed(Thing existing, int units)
	{
		preservedWealthDirty = true;
		if (!preservedIndexDirty && existing != null && units > 0 && existing.def != null)
		{
			preservedCountByShellDef.TryGetValue(existing.def, out var value);
			preservedCountByShellDef[existing.def] = value + units;
		}
	}

	private void EnsureKeysByDef()
	{
		if (keysByDefBuilt)
		{
			return;
		}
		keysByDefBuilt = true;
		keysByDef.Clear();
		foreach (KeyValuePair<SDS_StockKey, long> item in stock)
		{
			SDS_StockKey key = item.Key;
			if (key != null && key.thingDef != null && item.Value > 0)
			{
				if (!keysByDef.TryGetValue(key.thingDef, out var value) || value == null)
				{
					value = new List<SDS_StockKey>();
					keysByDef[key.thingDef] = value;
				}
				value.Add(key);
			}
		}
	}

	private void IndexKey(SDS_StockKey key)
	{
		if (keysByDefBuilt && key != null && key.thingDef != null)
		{
			if (!keysByDef.TryGetValue(key.thingDef, out var value) || value == null)
			{
				value = new List<SDS_StockKey>();
				keysByDef[key.thingDef] = value;
			}
			if (!value.Contains(key))
			{
				value.Add(key);
			}
		}
	}

	private void UnindexKey(SDS_StockKey key)
	{
		if (keysByDefBuilt && key != null && key.thingDef != null && keysByDef.TryGetValue(key.thingDef, out var value) && value != null)
		{
			value.Remove(key);
			if (value.Count == 0)
			{
				keysByDef.Remove(key.thingDef);
			}
		}
	}

	public SDS_StockKey FindWithdrawableKey(ThingDef def)
	{
		if (def == null)
		{
			return null;
		}
		EnsureKeysByDef();
		if (keysByDef.TryGetValue(def, out var value) && value != null && value.Count > 0)
		{
			for (int i = 0; i < value.Count; i++)
			{
				SDS_StockKey sDS_StockKey = value[i];
				if (sDS_StockKey != null && sDS_StockKey.stuff == null && sDS_StockKey.qualityInt < 0 && GetAvailableCount(sDS_StockKey) > 0)
				{
					return sDS_StockKey;
				}
			}
			SDS_StockKey sDS_StockKey2 = null;
			long num = 0L;
			for (int j = 0; j < value.Count; j++)
			{
				SDS_StockKey sDS_StockKey3 = value[j];
				if (sDS_StockKey3 != null)
				{
					long availableCount = GetAvailableCount(sDS_StockKey3);
					if (availableCount > num)
					{
						num = availableCount;
						sDS_StockKey2 = sDS_StockKey3;
					}
				}
			}
			if (sDS_StockKey2 != null)
			{
				return sDS_StockKey2;
			}
		}
		return FindPreservedKeyForDef(def);
	}

	private SDS_StockKey FindPreservedKeyForDef(ThingDef def)
	{
		if (def == null || preservedOwner == null || ((ThingOwner)preservedOwner).Count == 0)
		{
			return null;
		}
		EnsurePreservedIndex();
		if (!preservedKeysByInnerDef.TryGetValue(def, out var value) || value.Count == 0)
		{
			return null;
		}
		SDS_StockKey sDS_StockKey = null;
		SDS_StockKey sDS_StockKey2 = null;
		long num = -1L;
		for (int i = 0; i < value.Count; i++)
		{
			SDS_StockKey sDS_StockKey3 = value[i];
			if (sDS_StockKey3 != null && TryGetPreservedTotals(sDS_StockKey3, out var total, out var _))
			{
				if (sDS_StockKey == null && sDS_StockKey3.stuff == null && sDS_StockKey3.qualityInt < 0)
				{
					sDS_StockKey = sDS_StockKey3;
				}
				if (total > num)
				{
					num = total;
					sDS_StockKey2 = sDS_StockKey3;
				}
			}
		}
		return sDS_StockKey ?? sDS_StockKey2;
	}

	public bool TryReserveBillWithdrawal(Job job, Pawn pawn, int maxStackSpace, SDS_StockKey preferredKey = null)
	{
		if (job == null || job.thingDefToCarry == null || pawn == null || maxStackSpace <= 0)
		{
			return false;
		}
		for (int i = 0; i < reservations.Count; i++)
		{
			SDS_Reservation sDS_Reservation = reservations[i];
			if (sDS_Reservation != null && sDS_Reservation.jobLoadID == job.loadID && sDS_Reservation.count > 0)
			{
				job.count = SumReservationCount(job.loadID);
				return true;
			}
		}
		SDS_StockKey sDS_StockKey = preferredKey;
		if (sDS_StockKey == null || sDS_StockKey.thingDef != job.thingDefToCarry || GetAvailableCount(sDS_StockKey) <= 0)
		{
			sDS_StockKey = FindWithdrawableKey(job.thingDefToCarry);
		}
		if (sDS_StockKey == null)
		{
			return false;
		}
		long availableCount = GetAvailableCount(sDS_StockKey);
		int num = (int)Math.Min(maxStackSpace, availableCount);
		if (num <= 0)
		{
			return false;
		}
		CommitReservations(new List<SDS_Reservation>
		{
			new SDS_Reservation
			{
				jobLoadID = job.loadID,
				key = sDS_StockKey,
				count = num,
				pawn = pawn
			}
		});
		job.count = num;
		return true;
	}

	public bool TryGetJobReservation(int jobLoadID, out SDS_StockKey key, out int total)
	{
		key = null;
		total = 0;
		for (int i = 0; i < reservations.Count; i++)
		{
			SDS_Reservation sDS_Reservation = reservations[i];
			if (sDS_Reservation != null && sDS_Reservation.jobLoadID == jobLoadID && sDS_Reservation.key != null && sDS_Reservation.count > 0)
			{
				if (key == null)
				{
					key = sDS_Reservation.key;
				}
				else if (!key.SameAs(sDS_Reservation.key))
				{
					key = null;
					total = 0;
					return false;
				}
				total += sDS_Reservation.count;
			}
		}
		return key != null && total > 0;
	}

	private int SumReservationCount(int jobLoadID)
	{
		int num = 0;
		for (int i = 0; i < reservations.Count; i++)
		{
			SDS_Reservation sDS_Reservation = reservations[i];
			if (sDS_Reservation != null && sDS_Reservation.jobLoadID == jobLoadID && sDS_Reservation.count > 0)
			{
				num += sDS_Reservation.count;
			}
		}
		return num;
	}

	public void ReleaseReservation(int jobLoadID)
	{
		bool flag = false;
		for (int num = reservations.Count - 1; num >= 0; num--)
		{
			SDS_Reservation sDS_Reservation = reservations[num];
			if (sDS_Reservation != null && sDS_Reservation.jobLoadID == jobLoadID)
			{
				reservations.RemoveAt(num);
				flag = true;
			}
		}
		if (flag)
		{
			reservationVersion++;
		}
	}

	public bool ConsumeReservation(int transactionId, SDS_StockKey key, int count)
	{
		if (key == null || count <= 0)
		{
			return count <= 0;
		}
		int num = 0;
		for (int i = 0; i < reservations.Count; i++)
		{
			SDS_Reservation sDS_Reservation = reservations[i];
			if (sDS_Reservation != null && sDS_Reservation.jobLoadID == transactionId && sDS_Reservation.key != null && sDS_Reservation.count > 0 && sDS_Reservation.key.SameAs(key))
			{
				num += sDS_Reservation.count;
				if (num >= count)
				{
					break;
				}
			}
		}
		if (num < count)
		{
			return false;
		}
		int num2 = count;
		for (int j = 0; j < reservations.Count; j++)
		{
			if (num2 <= 0)
			{
				break;
			}
			SDS_Reservation sDS_Reservation2 = reservations[j];
			if (sDS_Reservation2 != null && sDS_Reservation2.jobLoadID == transactionId && sDS_Reservation2.key != null && sDS_Reservation2.count > 0 && sDS_Reservation2.key.SameAs(key))
			{
				int num3 = Math.Min(sDS_Reservation2.count, num2);
				sDS_Reservation2.count -= num3;
				num2 -= num3;
				if (sDS_Reservation2.count <= 0)
				{
					reservations.RemoveAt(j);
					j--;
				}
			}
		}
		reservationVersion++;
		return num2 <= 0;
	}

	internal void AuditReservationsAfterLoad()
	{
		bool flag = false;
		for (int num = reservations.Count - 1; num >= 0; num--)
		{
			SDS_Reservation sDS_Reservation = reservations[num];
			if (sDS_Reservation == null || sDS_Reservation.key == null || sDS_Reservation.count <= 0)
			{
				reservations.RemoveAt(num);
				flag = true;
			}
			else if (sDS_Reservation.jobLoadID < 0)
			{
				reservations.RemoveAt(num);
				flag = true;
			}
			else if (sDS_Reservation.pawn != null && (((Thing)sDS_Reservation.pawn).Destroyed || ((Thing)sDS_Reservation.pawn).Map == null))
			{
				reservations.RemoveAt(num);
				flag = true;
			}
		}
		if (flag)
		{
			reservationVersion++;
		}
	}

	public bool CanPreserve(SDS_StockKey key)
	{
		if (key == null || !key.preserved || key.thingDef == null)
		{
			return false;
		}
		RecomputeIfDirty();
		int usedCapacity = UsedCapacity;
		if (usedCapacity > cachedTypeCapacity)
		{
			return false;
		}
		if (CategoryExists(key))
		{
			return true;
		}
		return usedCapacity < cachedTypeCapacity;
	}

	public bool TryAddPreserved(Thing thing)
	{
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		if (thing == null || thing.Destroyed || thing.stackCount <= 0)
		{
			return false;
		}
		if (SDS_MirrorRegistry.IsMirror(thing))
		{
			return false;
		}
		if (!SDS_DepositRules.RequiresPreservedStorage(thing) || !CanPreserve(SDS_StockKey.FromPreservedThing(thing)))
		{
			return false;
		}
		if (preservedOwner == null || ((ThingOwner)preservedOwner).Contains(thing))
		{
			return false;
		}
		ThingOwner holdingOwner = thing.holdingOwner;
		Thing val = thing.SplitOff(thing.stackCount);
		if (val == null)
		{
			return false;
		}
		if (val.Spawned)
		{
			((Entity)val).DeSpawn((DestroyMode)0);
		}
		if (holdingOwner != null && holdingOwner.Contains(val) && !holdingOwner.Remove(val))
		{
			if (!val.Spawned)
			{
				holdingOwner.TryAdd(val, false);
			}
			return false;
		}
		val.holdingOwner = null;
		if (TryMergePreserved(val))
		{
			NotifyPreservedStockChanged();
			return true;
		}
		if (!((ThingOwner)preservedOwner).TryAdd(val, false))
		{
			Log.Error("[超维存储] 保真通道容器拒收实体，已退回来源 / 地图保数量守恒：" + Gen.ToStringSafe<Thing>(val));
			if (holdingOwner != null && holdingOwner.TryAdd(val, false))
			{
				return false;
			}
			Map anyPlayerHomeMap = Find.AnyPlayerHomeMap;
			Thing val2 = default(Thing);
			if (anyPlayerHomeMap != null && !GenPlace.TryPlaceThing(val, anyPlayerHomeMap.Center, anyPlayerHomeMap, (ThingPlaceMode)1, ref val2, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
			{
				GenSpawn.Spawn(val, anyPlayerHomeMap.Center, anyPlayerHomeMap, (WipeMode)0);
			}
			return false;
		}
		IndexPreservedThing(val);
		NotifyPreservedStockChanged();
		return true;
	}

	private bool TryMergePreserved(Thing incoming)
	{
		if (incoming == null || incoming.def == null || incoming.def.stackLimit <= 1)
		{
			return false;
		}
		SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(incoming);
		if (sDS_StockKey == null || !sDS_StockKey.preserved)
		{
			return false;
		}
		EnsurePreservedIndex();
		if (!preservedByKey.TryGetValue(sDS_StockKey, out var value) || value == null)
		{
			return false;
		}
		for (int i = 0; i < value.Count; i++)
		{
			Thing val = value[i];
			if (val != null && val != incoming && IsLivePreservedContent(val) && val.stackCount + incoming.stackCount <= val.def.stackLimit && SameStoredState(val, incoming) && val.CanStackWith(incoming))
			{
				int stackCount = incoming.stackCount;
				val.TryAbsorbStack(incoming, true);
				if (incoming.Destroyed || incoming.stackCount <= 0)
				{
					IndexPreservedAbsorbed(val, stackCount);
					return true;
				}
				if (incoming.stackCount != stackCount)
				{
					IndexPreservedAbsorbed(val, stackCount - incoming.stackCount);
					return false;
				}
			}
		}
		return false;
	}

	private static bool SameStoredState(Thing a, Thing b)
	{
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		if (a == null || b == null || a.def != b.def || a.Stuff != b.Stuff)
		{
			return false;
		}
		if (a.def.useHitPoints && a.HitPoints != b.HitPoints)
		{
			return false;
		}
		CompRottable val = ThingCompUtility.TryGetComp<CompRottable>(a);
		CompRottable val2 = ThingCompUtility.TryGetComp<CompRottable>(b);
		if (val == null != (val2 == null))
		{
			return false;
		}
		if (val != null && Math.Abs(val.RotProgress - val2.RotProgress) > 0.01f)
		{
			return false;
		}
		CompColorable val3 = ThingCompUtility.TryGetComp<CompColorable>(a);
		CompColorable val4 = ThingCompUtility.TryGetComp<CompColorable>(b);
		if ((val3 != null && val3.Active) || (val4 != null && val4.Active))
		{
			if (val3 == null || val4 == null || !val3.Active || !val4.Active)
			{
				return false;
			}
			Color color = val3.Color;
			Color color2 = val4.Color;
			if (Math.Abs(color.r - color2.r) > 0.004f || Math.Abs(color.g - color2.g) > 0.004f || Math.Abs(color.b - color2.b) > 0.004f || Math.Abs(color.a - color2.a) > 0.004f)
			{
				return false;
			}
		}
		CompRefuelable val5 = ThingCompUtility.TryGetComp<CompRefuelable>(a);
		CompRefuelable val6 = ThingCompUtility.TryGetComp<CompRefuelable>(b);
		if ((val5 != null || val6 != null) && (val5 == null || val6 == null || Math.Abs(val5.Fuel - val6.Fuel) > 0.01f))
		{
			return false;
		}
		CompApparelVerbOwner_Charged val7 = ThingCompUtility.TryGetComp<CompApparelVerbOwner_Charged>(a);
		CompApparelVerbOwner_Charged val8 = ThingCompUtility.TryGetComp<CompApparelVerbOwner_Charged>(b);
		if ((val7 != null || val8 != null) && (val7 == null || val8 == null || val7.RemainingCharges != val8.RemainingCharges))
		{
			return false;
		}
		CompIngredients val9 = ThingCompUtility.TryGetComp<CompIngredients>(a);
		CompIngredients val10 = ThingCompUtility.TryGetComp<CompIngredients>(b);
		if (val9 != null || val10 != null)
		{
			if (val9 == null || val10 == null || val9.ingredients == null || val10.ingredients == null || val9.ingredients.Count != val10.ingredients.Count)
			{
				return false;
			}
			for (int i = 0; i < val9.ingredients.Count; i++)
			{
				if (val9.ingredients[i] != val10.ingredients[i])
				{
					return false;
				}
			}
		}
		return true;
	}

	private bool TryDepositPreserved(Thing thing, out int accepted)
	{
		accepted = 0;
		int stackCount = thing.stackCount;
		if (!TryAddPreserved(thing))
		{
			return false;
		}
		accepted = stackCount;
		return true;
	}

	public bool TryWithdrawPreserved(SDS_StockKey key, int requested, out List<Thing> result)
	{
		return TryWithdrawPreservedWhere(key, requested, null, out result);
	}

	public bool TryWithdrawPreservedWhere(SDS_StockKey key, int requested, Predicate<Thing> validator, out List<Thing> result)
	{
		result = null;
		if (!Available)
		{
			return false;
		}
		if (key == null || !key.preserved || requested <= 0 || preservedOwner == null || ((ThingOwner)preservedOwner).Count == 0)
		{
			return false;
		}
		List<Thing> list = new List<Thing>();
		HashSet<Thing> hashSet = new HashSet<Thing>();
		int num = requested;
		EnsurePreservedIndex();
		if (!preservedByKey.TryGetValue(key, out var value) || value.Count == 0)
		{
			return false;
		}
		for (int i = 0; i < value.Count && num > 0; i++)
		{
			Thing val = value[i];
			if (!IsLivePreservedContent(val) || !hashSet.Add(val) || (validator != null && !validator(val)))
			{
				continue;
			}
			int num2 = Math.Min(num, val.stackCount);
			if (num2 <= 0)
			{
				continue;
			}
			if (num2 >= val.stackCount)
			{
				if (!((ThingOwner)preservedOwner).Remove(val))
				{
					continue;
				}
				list.Add(val);
			}
			else
			{
				Thing val2 = val.SplitOff(num2);
				if (val2 == null)
				{
					continue;
				}
				list.Add(val2);
			}
			num -= num2;
		}
		if (list.Count == 0)
		{
			return false;
		}
		MarkPreservedIndexDirty();
		NotifyPreservedStockChanged();
		result = list;
		return true;
	}

	public bool IsPreserved(Thing thing)
	{
		if (thing == null || preservedOwner == null)
		{
			return false;
		}
		EnsurePreservedIndex();
		return preservedMembers.Contains(thing);
	}

	public bool IsLivePreservedThing(Thing thing)
	{
		return thing != null && IsPreserved(thing) && IsLivePreservedContent(thing);
	}

	private bool IsLivePreservedContent(Thing thing)
	{
		return thing != null && !thing.Destroyed && thing.stackCount > 0 && !thing.Spawned && (object)thing.holdingOwner == preservedOwner;
	}

	public bool AnyPreservedOfDef(ThingDef def)
	{
		if (def == null || preservedOwner == null)
		{
			return false;
		}
		EnsurePreservedIndex();
		long value;
		return preservedCountByShellDef.TryGetValue(def, out value) && value > 0;
	}

	public void AppendPreservedThings(List<Thing> outList)
	{
		if (outList == null || preservedOwner == null)
		{
			return;
		}
		for (int i = 0; i < ((ThingOwner)preservedOwner).Count; i++)
		{
			Thing val = preservedOwner[i];
			if (IsLivePreservedContent(val))
			{
				outList.Add(val);
			}
		}
	}

	public bool TryReservePreservedPlan(Job job, Pawn pawn, List<ThingCount> plan)
	{
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		if (job == null || pawn == null || plan == null || plan.Count == 0)
		{
			return false;
		}
		for (int i = 0; i < reservations.Count; i++)
		{
			SDS_Reservation sDS_Reservation = reservations[i];
			if (sDS_Reservation != null && sDS_Reservation.jobLoadID == job.loadID && sDS_Reservation.count > 0)
			{
				return true;
			}
		}
		Dictionary<SDS_StockKey, int> dictionary = new Dictionary<SDS_StockKey, int>();
		for (int j = 0; j < plan.Count; j++)
		{
			ThingCount val = plan[j];
			Thing thing = ((ThingCount)(ref val)).Thing;
			if (!IsLivePreservedThing(thing))
			{
				return false;
			}
			SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(thing);
			if (sDS_StockKey == null || !sDS_StockKey.preserved)
			{
				return false;
			}
			val = plan[j];
			int num = Math.Min(Math.Max(1, ((ThingCount)(ref val)).Count), Math.Max(1, thing.stackCount));
			dictionary[sDS_StockKey] = (dictionary.TryGetValue(sDS_StockKey, out var value) ? value : 0) + num;
		}
		foreach (KeyValuePair<SDS_StockKey, int> item in dictionary)
		{
			if (GetAvailableCount(item.Key) < item.Value)
			{
				return false;
			}
		}
		List<SDS_Reservation> list = new List<SDS_Reservation>(dictionary.Count);
		foreach (KeyValuePair<SDS_StockKey, int> item2 in dictionary)
		{
			list.Add(new SDS_Reservation
			{
				jobLoadID = job.loadID,
				key = item2.Key,
				count = item2.Value,
				pawn = pawn
			});
		}
		CommitReservations(list);
		return true;
	}

	public bool TryWithdrawPreservedPlan(List<ThingCount> plan, out List<Thing> result)
	{
		return TryWithdrawPreservedPlan(0, plan, out result);
	}

	public bool TryWithdrawPreservedPlan(int transactionId, List<ThingCount> plan, out List<Thing> result)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		result = null;
		if (!Available)
		{
			return false;
		}
		if (plan == null || plan.Count == 0 || preservedOwner == null)
		{
			return false;
		}
		ThingCount val;
		for (int i = 0; i < plan.Count; i++)
		{
			val = plan[i];
			Thing thing = ((ThingCount)(ref val)).Thing;
			val = plan[i];
			int count = ((ThingCount)(ref val)).Count;
			if (!IsLivePreservedThing(thing) || count <= 0 || count > thing.stackCount)
			{
				return false;
			}
		}
		List<Thing> list = new List<Thing>();
		for (int j = 0; j < plan.Count; j++)
		{
			val = plan[j];
			Thing thing2 = ((ThingCount)(ref val)).Thing;
			val = plan[j];
			int count2 = ((ThingCount)(ref val)).Count;
			if (count2 < thing2.stackCount)
			{
				Thing val2 = thing2.SplitOff(count2);
				if (val2 == null)
				{
					for (int num = list.Count - 1; num >= 0; num--)
					{
						Thing val3 = list[num];
						if (val3 != null && !val3.Destroyed)
						{
							((ThingOwner)preservedOwner).TryAdd(val3, false);
						}
					}
					MarkPreservedIndexDirty();
					return false;
				}
				list.Add(val2);
			}
			else
			{
				((ThingOwner)preservedOwner).Remove(thing2);
				list.Add(thing2);
			}
		}
		if (transactionId != 0)
		{
			for (int k = 0; k < plan.Count; k++)
			{
				val = plan[k];
				SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(((ThingCount)(ref val)).Thing);
				if (sDS_StockKey != null)
				{
					val = plan[k];
					ConsumeReservation(transactionId, sDS_StockKey, ((ThingCount)(ref val)).Count);
				}
			}
		}
		NotifyPreservedStockChanged();
		MarkPreservedIndexDirty();
		result = list;
		return true;
	}

	public bool TryGetPreservedTotals(SDS_StockKey key, out long total, out long available)
	{
		total = 0L;
		available = 0L;
		if (key == null || !key.preserved || preservedOwner == null)
		{
			return false;
		}
		EnsurePreservedIndex();
		if (!preservedByKey.TryGetValue(key, out var value) || value == null || value.Count == 0)
		{
			return false;
		}
		preservedTotalsScratch.Clear();
		for (int i = 0; i < value.Count; i++)
		{
			Thing val = value[i];
			if (IsLivePreservedContent(val) && preservedTotalsScratch.Add(val))
			{
				total += val.stackCount;
			}
		}
		if (total <= 0)
		{
			return false;
		}
		long reservedCount = GetReservedCount(key);
		available = ((total > reservedCount) ? (total - reservedCount) : 0);
		return true;
	}

	public int GetPreservedTotalCount(SDS_StockKey key)
	{
		long total;
		long available;
		return (int)(TryGetPreservedTotals(key, out total, out available) ? Math.Min(total, 2147483647L) : 0);
	}

	public int GetPreservedAvailableCount(SDS_StockKey key)
	{
		long total;
		long available;
		return (int)(TryGetPreservedTotals(key, out total, out available) ? Math.Min(available, 2147483647L) : 0);
	}

	public List<SDS_StockEntry> PreservedSnapshot()
	{
		List<SDS_StockEntry> list = new List<SDS_StockEntry>();
		if (preservedOwner == null || ((ThingOwner)preservedOwner).Count == 0)
		{
			return list;
		}
		EnsurePreservedIndex();
		for (int i = 0; i < preservedKeyOrder.Count; i++)
		{
			SDS_StockKey sDS_StockKey = preservedKeyOrder[i];
			if (sDS_StockKey != null && sDS_StockKey.thingDef != null && TryGetPreservedTotals(sDS_StockKey, out var total, out var _))
			{
				list.Add(new SDS_StockEntry(sDS_StockKey, total, preserved: true));
			}
		}
		return list;
	}

	private void PrunePreserved()
	{
		if (preservedOwner != null)
		{
			int num = preservedOwner.RemoveAll((Predicate<Thing>)((Thing t) => t == null || t.Destroyed || t.stackCount <= 0));
			if (num > 0)
			{
				MarkPreservedIndexDirty();
				NotifyPreservedStockChanged();
				Log.Message("[超维存储] 保真通道清理了 " + num + " 件已失效实体（拆过 / 已取出）。");
			}
		}
	}

	public float PreservedWealth()
	{
		if (!preservedWealthDirty)
		{
			return preservedWealthCache;
		}
		preservedWealthDirty = false;
		float num = 0f;
		if (preservedOwner != null)
		{
			for (int i = 0; i < ((ThingOwner)preservedOwner).Count; i++)
			{
				Thing val = preservedOwner[i];
				if (IsLivePreservedContent(val) && val.def != null && val.def.EverHaulable)
				{
					num += val.MarketValue * (float)val.stackCount;
				}
			}
		}
		preservedWealthCache = num;
		return num;
	}

	public List<SDS_StockEntry> Snapshot(int max)
	{
		List<SDS_StockEntry> list = new List<SDS_StockEntry>();
		foreach (KeyValuePair<SDS_StockKey, long> item in stock)
		{
			if (item.Value > 0 && item.Key != null && item.Key.thingDef != null)
			{
				list.Add(new SDS_StockEntry(item.Key, item.Value));
				if (max > 0 && list.Count >= max)
				{
					break;
				}
			}
		}
		return list;
	}

	public List<SDS_StockEntry> UnresolvedSnapshot()
	{
		List<SDS_StockEntry> list = new List<SDS_StockEntry>();
		foreach (KeyValuePair<SDS_StockKey, long> item in stock)
		{
			if (item.Value > 0 && item.Key != null && !item.Key.Resolved && item.Key.HasIdentity)
			{
				list.Add(new SDS_StockEntry(item.Key, item.Value));
			}
		}
		return list;
	}

	public int RetryResolveUnresolved()
	{
		bool flag = false;
		int num = 0;
		List<SDS_StockKey> list = new List<SDS_StockKey>(stock.Keys);
		for (int i = 0; i < list.Count; i++)
		{
			SDS_StockKey sDS_StockKey = list[i];
			if (sDS_StockKey != null && !sDS_StockKey.Resolved)
			{
				if (sDS_StockKey.TryResolve())
				{
					flag = true;
				}
				else
				{
					num++;
				}
			}
		}
		if (flag)
		{
			keysByDefBuilt = false;
			categoryKeysBuilt = false;
			NotifyStockChanged();
		}
		return num;
	}

	private void LogUnresolvedEntries()
	{
		int num = 0;
		long num2 = 0L;
		List<string> list = null;
		foreach (KeyValuePair<SDS_StockKey, long> item in stock)
		{
			if (item.Key != null && !item.Key.Resolved)
			{
				num++;
				num2 += item.Value;
				if (list == null)
				{
					list = new List<string>();
				}
				list.Add(item.Key.EffectiveDefName + "×" + item.Value);
			}
		}
		if (num > 0)
		{
			SDS_Diag.Warn("存档/未解析 def（账目已保留，修复 def 后自动恢复）", "条目=" + num + " 件数=" + num2 + " 明细=" + string.Join("，", list.ToArray()));
		}
	}

	private void PurgeUnrebuildableStock()
	{
		if (stock.Count == 0)
		{
			return;
		}
		List<SDS_StockKey> list = new List<SDS_StockKey>();
		foreach (KeyValuePair<SDS_StockKey, long> item in stock)
		{
			if (item.Key != null && item.Key.Resolved && (SDS_DepositRules.IsLegacyUnrebuildableDef(item.Key.thingDef) || (!item.Key.preserved && (!SDS_DepositRules.CanStoreDef(item.Key.thingDef) || !item.Key.thingDef.EverHaulable))))
			{
				list.Add(item.Key);
			}
		}
		if (list.Count != 0)
		{
			for (int i = 0; i < list.Count; i++)
			{
				RemoveStock(list[i]);
			}
			NotifyStockChanged();
		}
	}

	public override void ExposeData()
	{
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Invalid comparison between Unknown and I4
		//IL_04eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f1: Invalid comparison between Unknown and I4
		((GameComponent)this).ExposeData();
		string text = null;
		if (stockEntries == null)
		{
			stockEntries = new List<SDS_StockEntry>();
			text = "stockEntries";
		}
		if (reservations == null)
		{
			reservations = new List<SDS_Reservation>();
			text = ((text == null) ? string.Empty : (text + ",")) + "reservations";
		}
		if (preservedScratch == null)
		{
			preservedScratch = new List<Thing>();
			text = ((text == null) ? string.Empty : (text + ",")) + "preservedScratch";
		}
		if (mirrorScratch == null)
		{
			mirrorScratch = new List<Thing>();
			text = ((text == null) ? string.Empty : (text + ",")) + "mirrorScratch";
		}
		if (reclaimScratch == null)
		{
			reclaimScratch = new List<SDS_ReclaimSave>();
			text = ((text == null) ? string.Empty : (text + ",")) + "reclaimScratch";
		}
		if (transportLoadScratch == null)
		{
			transportLoadScratch = new List<SDS_TransportLoadSave>();
			text = ((text == null) ? string.Empty : (text + ",")) + "transportLoadScratch";
		}
		if (forbiddenScratch == null)
		{
			forbiddenScratch = new List<SDS_StockKey>();
			text = ((text == null) ? string.Empty : (text + ",")) + "forbiddenKeys";
		}
		if (sidebarUI == null)
		{
			sidebarUI = new SDS_SidebarUIState();
			text = ((text == null) ? string.Empty : (text + ",")) + "sidebarUI";
		}
		if (text != null)
		{
			SDS_Diag.Warn("存档/旧档字段补齐", "字段=" + text);
		}
		if ((int)Scribe.mode == 1)
		{
			stockEntries.Clear();
			foreach (KeyValuePair<SDS_StockKey, long> item in stock)
			{
				if (item.Key != null && item.Key.HasIdentity && item.Value > 0)
				{
					item.Key.EnsureNames();
					SDS_StockEntry sDS_StockEntry = new SDS_StockEntry(item.Key, item.Value);
					if (stockVitals.TryGetValue(item.Key, out var value) && value != null)
					{
						sDS_StockEntry.avgHitPoints = value.hitPoints;
						sDS_StockEntry.avgRotProgress = value.rotProgress;
					}
					stockEntries.Add(sDS_StockEntry);
				}
			}
			PrunePreserved();
			preservedScratch.Clear();
			for (int i = 0; i < ((ThingOwner)preservedOwner).Count; i++)
			{
				Thing val = preservedOwner[i];
				if (val != null && !val.Destroyed)
				{
					preservedScratch.Add(val);
				}
			}
			mirrorScratch.Clear();
			SDS_MirrorService.CollectMirrors(mirrorScratch);
			reclaimScratch.Clear();
			SDS_ReclaimRegistry.ExportTo(reclaimScratch);
			SDS_TransportVirtual.ExportLoadRequests(transportLoadScratch);
			forbiddenScratch.Clear();
			foreach (SDS_StockKey forbiddenKey in forbiddenKeys)
			{
				forbiddenScratch.Add(forbiddenKey);
			}
		}
		Scribe_Values.Look<int>(ref dataVersion, "dataVersion", 1, false);
		Scribe_Collections.Look<SDS_StockEntry>(ref stockEntries, "stockEntries", (LookMode)2, Array.Empty<object>());
		Scribe_Deep.Look<ThingFilter>(ref depositFilter, "depositFilter", Array.Empty<object>());
		Scribe_Collections.Look<SDS_StockKey>(ref forbiddenScratch, "forbiddenKeys", (LookMode)2, Array.Empty<object>());
		Scribe_Values.Look<int>(ref coreStoragePriority, "coreStoragePriority", -1, false);
		Scribe_Deep.Look<SDS_SidebarUIState>(ref sidebarUI, "sidebarUI", Array.Empty<object>());
		if (sidebarUI == null)
		{
			sidebarUI = new SDS_SidebarUIState();
		}
		Scribe_Collections.Look<SDS_Reservation>(ref reservations, "reservations", (LookMode)2, Array.Empty<object>());
		Scribe_Collections.Look<Thing>(ref preservedScratch, "preservedThings", (LookMode)2, Array.Empty<object>());
		Scribe_Collections.Look<Thing>(ref mirrorScratch, "sdsMirrors", (LookMode)3, Array.Empty<object>());
		Scribe_Collections.Look<SDS_ReclaimSave>(ref reclaimScratch, "sdsReclaim", (LookMode)2, Array.Empty<object>());
		Scribe_Collections.Look<SDS_TransportLoadSave>(ref transportLoadScratch, "transportLoadRequests", (LookMode)2, Array.Empty<object>());
		if ((int)Scribe.mode != 4)
		{
			return;
		}
		stock.Clear();
		stockVitals.Clear();
		keysByDefBuilt = false;
		if (stockEntries != null)
		{
			for (int j = 0; j < stockEntries.Count; j++)
			{
				SDS_StockEntry sDS_StockEntry2 = stockEntries[j];
				if (sDS_StockEntry2 != null && sDS_StockEntry2.key != null && sDS_StockEntry2.count > 0 && sDS_StockEntry2.key.HasIdentity)
				{
					sDS_StockEntry2.key.TryResolve();
					long count = GetCount(sDS_StockEntry2.key);
					stock[sDS_StockEntry2.key] = count + sDS_StockEntry2.count;
					MergeVitals(sDS_StockEntry2.key, count, sDS_StockEntry2.count, sDS_StockEntry2.avgHitPoints, sDS_StockEntry2.avgRotProgress);
				}
			}
			stockEntries.Clear();
		}
		NotifyStockChanged();
		LogUnresolvedEntries();
		reservationVersion++;
		if (depositFilter == null)
		{
			depositFilter = BuildDefaultDepositFilter();
		}
		if (reservations == null)
		{
			reservations = new List<SDS_Reservation>();
		}
		if (preservedScratch != null)
		{
			for (int k = 0; k < preservedScratch.Count; k++)
			{
				Thing val2 = preservedScratch[k];
				if (val2 != null && !val2.Destroyed)
				{
					if (val2.Spawned)
					{
						((Entity)val2).DeSpawn((DestroyMode)0);
					}
					val2.holdingOwner = null;
					if (!((ThingOwner)preservedOwner).TryAdd(val2, false))
					{
						Log.Error("[超维存储] 读档时保真实体入容器失败：" + Gen.ToStringSafe<Thing>(val2));
					}
				}
			}
			preservedScratch.Clear();
		}
		MarkPreservedIndexDirty();
		PrunePreserved();
		PurgeUnrebuildableStock();
		forbiddenKeys.Clear();
		if (forbiddenScratch != null)
		{
			for (int l = 0; l < forbiddenScratch.Count; l++)
			{
				SDS_StockKey sDS_StockKey = forbiddenScratch[l];
				if (sDS_StockKey != null && sDS_StockKey.HasIdentity)
				{
					sDS_StockKey.TryResolve();
					forbiddenKeys.Add(sDS_StockKey);
				}
			}
			forbiddenScratch.Clear();
		}
		PruneForbiddenKeys();
		if (forbiddenKeys.Count > 0)
		{
			List<SDS_StockKey> list = new List<SDS_StockKey>(forbiddenKeys);
			for (int m = 0; m < list.Count; m++)
			{
				ApplyForbiddenToPreserved(list[m], forbidden: true);
			}
		}
		if (reclaimScratch != null)
		{
			SDS_ReclaimRegistry.ImportFrom(reclaimScratch);
			reclaimScratch.Clear();
		}
		reclaimAuditPending = true;
		SidebarUI.Normalize();
		SidebarUI.NormalizeAgainst(this);
		Instance = this;
		dirty = true;
	}

	public override void FinalizeInit()
	{
		((GameComponent)this).FinalizeInit();
		Instance = this;
		if (reservations == null)
		{
			reservations = new List<SDS_Reservation>();
		}
		PrunePreserved();
		if (mirrorScratch == null)
		{
			mirrorScratch = new List<Thing>();
		}
		SDS_MirrorService.CleanupLoadedMirrors(mirrorScratch);
		mirrorScratch.Clear();
		if (reclaimScratch != null && reclaimScratch.Count > 0)
		{
			SDS_ReclaimRegistry.ImportFrom(reclaimScratch);
			reclaimScratch.Clear();
		}
		reclaimAuditPending = true;
		RetryResolveUnresolved();
		PurgeUnrebuildableStock();
		RebuildRegistries();
		if (transportLoadScratch == null)
		{
			transportLoadScratch = new List<SDS_TransportLoadSave>();
		}
		SDS_TransportVirtual.RestoreLoadRequests(transportLoadScratch);
		transportLoadScratch.Clear();
		ApplyStoragePriorityToEndpoints();
		dirty = true;
		scanDirty = true;
		SDS_ImplantUtility.NotifyChipStateChanged();
		SDS_EndpointContractCache.Reset();
		lastMapSignature = ComputeMapSignature();
		LogLoadDiagnostics();
	}

	private void LogLoadDiagnostics()
	{
		if (Find.Maps == null || Find.Maps.Count == 0)
		{
			loadReportPending = true;
			return;
		}
		dirty = true;
		scanDirty = true;
		RecomputeIfDirty();
		int total = 0;
		int ok = 0;
		for (int i = 0; i < cores.Count; i++)
		{
			AddBuildingEndpoint((Building)(object)cores[i], ref total, ref ok);
		}
		for (int j = 0; j < capacityArrays.Count; j++)
		{
			AddBuildingEndpoint((Building)(object)capacityArrays[j], ref total, ref ok);
		}
		for (int k = 0; k < ports.Count; k++)
		{
			AddBuildingEndpoint((Building)(object)ports[k], ref total, ref ok);
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		foreach (Map scanMap in scanMaps)
		{
			if (scanMap == null || scanMap.mapPawns == null)
			{
				continue;
			}
			foreach (Pawn item in scanMap.mapPawns.AllPawnsSpawned)
			{
				if (item == null || !((Thing)item).Spawned || ((Thing)item).Faction != Faction.OfPlayer || item.RaceProps == null)
				{
					continue;
				}
				bool flag = SDS_ImplantUtility.IsPortOperational(item);
				if (item.RaceProps.IsMechanoid)
				{
					num3++;
					if (flag)
					{
						num4++;
					}
				}
				else if (item.RaceProps.Humanlike)
				{
					num++;
					if (flag)
					{
						num2++;
					}
				}
			}
		}
		int num5 = total + num + num3;
		int num6 = ok + num2 + num4;
		Log.Message("[超维存储] 超维空间可用性：" + (cachedAvailable ? "可用" : "不可用") + "（扫描地图 " + scanMaps.Count + " 张，可用核心 " + mapsWithOperationalCore.Count + " 张图有核心）");
		Log.Message("[超维存储] 端点总计：总 " + num5 + "，可用 " + num6 + "，不可用 " + (num5 - num6) + "（建筑 + 殖民者 + 机械体）");
		Log.Message("[超维存储] 建筑端点：总 " + total + "，可用 " + ok + "，不可用 " + (total - ok));
		Log.Message("[超维存储] 殖民者端点：总 " + num + "，可用 " + num2 + "，不可用 " + (num - num2));
		Log.Message("[超维存储] 机械体端点：总 " + num3 + "，可用 " + num4 + "，不可用 " + (num3 - num4));
	}

	private void AddBuildingEndpoint(Building b, ref int total, ref int ok)
	{
		if (b == null || ((Thing)b).Destroyed || ((Thing)b).Map == null || !scanMaps.Contains(((Thing)b).Map))
		{
			return;
		}
		total++;
		if (b is Building_SDS_Core building_SDS_Core)
		{
			if (building_SDS_Core.Operational)
			{
				ok++;
			}
		}
		else if (b is Building_SDS_Port building_SDS_Port)
		{
			if (building_SDS_Port.Operational)
			{
				ok++;
			}
		}
		else if (b is Building_SDS_CapacityArray { Operational: not false })
		{
			ok++;
		}
	}

	private void RebuildRegistries()
	{
		cores.Clear();
		capacityArrays.Clear();
		ports.Clear();
		List<Map> maps = Find.Maps;
		if (maps == null)
		{
			return;
		}
		foreach (Map item in maps)
		{
			if (item == null || item.listerThings == null)
			{
				continue;
			}
			List<Thing> allThings = item.listerThings.AllThings;
			if (allThings == null)
			{
				continue;
			}
			for (int i = 0; i < allThings.Count; i++)
			{
				if (allThings[i] is Building_SDS_Core building)
				{
					RegisterCore(building);
				}
				else if (allThings[i] is Building_SDS_CapacityArray building2)
				{
					RegisterCapacityArray(building2);
				}
				else if (allThings[i] is Building_SDS_Port building3)
				{
					RegisterPort(building3);
				}
			}
		}
	}
}
internal enum SDS_PlacementPolicy
{
	Exact,
	NearPawn,
	RandomNear
}
internal static class SDS_PlacementUtility
{
	public static bool TryPlaceOrSpawn(Thing thing, IntVec3 cell, Map map)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		Thing placed;
		return TryPlace(thing, cell, map, SDS_PlacementPolicy.Exact, out placed);
	}

	public static bool TryPlace(Thing thing, IntVec3 cell, Map map, SDS_PlacementPolicy policy, out Thing placed, float randomRadius = 3f)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		placed = thing;
		if (thing == null || thing.Destroyed || thing.Spawned || map == null)
		{
			return false;
		}
		IntVec3 val = ((((IntVec3)(ref cell)).IsValid && GenGrid.InBounds(cell, map)) ? cell : map.Center);
		Thing val2 = default(Thing);
		if (GenPlace.TryPlaceThing(thing, val, map, (ThingPlaceMode)1, ref val2, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1) && val2 != null && !val2.Destroyed)
		{
			placed = val2;
			return true;
		}
		IntVec3 val3 = default(IntVec3);
		if (policy == SDS_PlacementPolicy.RandomNear && CellFinder.TryFindRandomCellNear(val, map, Math.Max(1, (int)randomRadius), (Predicate<IntVec3>)((IntVec3 c) => GenGrid.Standable(c, map) && !GridsUtility.Fogged(c, map)), ref val3, -1))
		{
			val = val3;
		}
		if (!thing.Destroyed)
		{
			GenSpawn.Spawn(thing, val, map, (WipeMode)0);
			placed = thing;
			return true;
		}
		return false;
	}
}
[DefOf]
public static class SDS_KeyBindingDefOf
{
	public static KeyBindingDef SDS_OpenSuperSpace;

	public static KeyBindingDef SDS_OutputFromSuperSpace;

	public static KeyBindingDef SDS_UseFromSuperSpace;
}
internal static class SDS_Hotkeys
{
	private static bool bindingsEnsured;

	private static int lastSettingsFrame = -1000;

	public static bool Capturing { get; private set; }

	public static KeyBindingDef CaptureDef { get; private set; }

	public static string CaptureNote { get; private set; }

	public static KeyBindingDef OpenDef => SDS_KeyBindingDefOf.SDS_OpenSuperSpace;

	public static KeyBindingDef OutputDef => SDS_KeyBindingDefOf.SDS_OutputFromSuperSpace;

	public static KeyBindingDef UseDef => SDS_KeyBindingDefOf.SDS_UseFromSuperSpace;

	public static bool InBuildMode
	{
		get
		{
			DesignatorManager designatorManager = Find.DesignatorManager;
			if (designatorManager != null && designatorManager.SelectedDesignator != null)
			{
				return true;
			}
			MainButtonDef architect = MainButtonDefOf.Architect;
			return Find.MainTabsRoot != null && architect != null && Find.MainTabsRoot.OpenTab == architect;
		}
	}

	public static bool CanUseGlobalHotkeys()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		EnsureBindings();
		if ((int)Current.ProgramState != 2 || Find.CurrentMap == null)
		{
			return false;
		}
		if (WorldRendererUtility.WorldRendered || Find.WindowStack == null)
		{
			return false;
		}
		if (KeyPrefs.KeyPrefsData == null)
		{
			return false;
		}
		if (Find.WindowStack.AnySearchWidgetFocused || Find.WindowStack.AnyWindowAbsorbingAllInput)
		{
			return false;
		}
		return true;
	}

	public static void ToggleSidebar()
	{
		if (Find.WindowStack != null)
		{
			Window_SuperSpace window_SuperSpace = Find.WindowStack.WindowOfType<Window_SuperSpace>();
			if (window_SuperSpace != null)
			{
				((Window)window_SuperSpace).Close(true);
			}
			else
			{
				Find.WindowStack.Add((Window)(object)new Window_SuperSpace());
			}
		}
	}

	public static bool MustYieldHotkey(KeyCode code)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		if (InBuildMode)
		{
			return true;
		}
		if ((int)code != 0 && GizmoGridDrawer.drawnHotKeys.Contains(code))
		{
			return true;
		}
		Targeter targeter = Find.Targeter;
		if (targeter != null && targeter.IsTargeting)
		{
			return true;
		}
		return !string.IsNullOrEmpty(GUI.GetNameOfFocusedControl());
	}

	public static bool TryOutputFromSidebar()
	{
		if (Find.WindowStack == null)
		{
			return false;
		}
		Window_SuperSpace window_SuperSpace = Find.WindowStack.WindowOfType<Window_SuperSpace>();
		if (window_SuperSpace == null || !window_SuperSpace.HasSelectedRow)
		{
			return false;
		}
		if (!Find.WindowStack.GetsInput((Window)(object)window_SuperSpace))
		{
			return false;
		}
		return window_SuperSpace.TryOpenOutputFromHotkey();
	}

	public static bool TryUseFromSidebar()
	{
		if (Find.WindowStack == null)
		{
			return false;
		}
		Window_SuperSpace window_SuperSpace = Find.WindowStack.WindowOfType<Window_SuperSpace>();
		if (window_SuperSpace == null || !window_SuperSpace.HasSelectedRow)
		{
			return false;
		}
		if (!Find.WindowStack.GetsInput((Window)(object)window_SuperSpace))
		{
			return false;
		}
		return window_SuperSpace.TryOpenUseFromHotkey();
	}

	public static void NotifySettingsPageDrawn()
	{
		lastSettingsFrame = Time.frameCount;
	}

	public static void BeginCapture(KeyBindingDef def)
	{
		if (def != null)
		{
			EnsureBindings();
			Capturing = true;
			CaptureDef = def;
			CaptureNote = null;
			lastSettingsFrame = Time.frameCount;
		}
	}

	public static void CancelCapture()
	{
		Capturing = false;
		CaptureDef = null;
		CaptureNote = null;
	}

	public unsafe static string GetKeyLabel(KeyBindingDef def)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		if (def == null)
		{
			return "?";
		}
		EnsureBindings();
		if (KeyPrefs.KeyPrefsData == null)
		{
			return "?";
		}
		KeyCode mainKey = def.MainKey;
		return ((int)mainKey == 0) ? ((object)Translator.Translate("SDS.Settings.HotkeyNone")/*cast due to constrained. prefix*/).ToString() : ((object)(*(KeyCode*)(&mainKey))/*cast due to constrained. prefix*/).ToString();
	}

	public static string HotkeyHintText()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		return ((object)TranslatorFormattedStringExtensions.Translate("SDS.Window.HotkeyHint", NamedArgument.op_Implicit(GetKeyLabel(OpenDef)), NamedArgument.op_Implicit(GetKeyLabel(OutputDef)), NamedArgument.op_Implicit(GetKeyLabel(UseDef)))/*cast due to constrained. prefix*/).ToString();
	}

	public static void ClearBinding(KeyBindingDef def)
	{
		if (def != null)
		{
			EnsureBindings();
			SetBindingRaw(def, (KeyCode)0);
			SaveBindings();
		}
	}

	public static void ResetDefaults()
	{
		EnsureBindings();
		ResetOne(OpenDef);
		ResetOne(OutputDef);
		ResetOne(UseDef);
		SaveBindings();
		CancelCapture();
	}

	private static void ResetOne(KeyBindingDef def)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (def != null)
		{
			SetBindingRaw(def, def.GetDefaultKeyCode((BindingSlot)0));
		}
	}

	public static bool TryCaptureKey(Event current)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Invalid comparison between Unknown and I4
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Invalid comparison between Unknown and I4
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Invalid comparison between Unknown and I4
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Invalid comparison between Unknown and I4
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Invalid comparison between Unknown and I4
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Invalid comparison between Unknown and I4
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		if (!Capturing || current == null || (int)current.type != 4)
		{
			return false;
		}
		KeyCode keyCode = current.keyCode;
		if ((int)keyCode == 0)
		{
			return false;
		}
		if ((int)keyCode == 27)
		{
			ClearBinding(CaptureDef);
			CancelCapture();
			current.Use();
			return true;
		}
		if ((int)keyCode == 13 || (int)keyCode == 271)
		{
			CaptureNote = ((object)Translator.Translate("SDS.Settings.HotkeyReserved")/*cast due to constrained. prefix*/).ToString();
			current.Use();
			return true;
		}
		if (IsModifierOnly(keyCode))
		{
			CaptureNote = ((object)Translator.Translate("SDS.Settings.HotkeyModifierOnly")/*cast due to constrained. prefix*/).ToString();
			current.Use();
			return true;
		}
		if (current.control || current.alt || current.shift || current.command)
		{
			CaptureNote = ((object)Translator.Translate("SDS.Settings.HotkeyNoModifiers")/*cast due to constrained. prefix*/).ToString();
			current.Use();
			return true;
		}
		foreach (KeyBindingDef item in OwnDefs())
		{
			if (item != null && item != CaptureDef && KeyPrefs.KeyPrefsData != null && item.MainKey == keyCode)
			{
				CaptureNote = ((object)Translator.Translate("SDS.Settings.HotkeyDuplicate")/*cast due to constrained. prefix*/).ToString();
				current.Use();
				return true;
			}
		}
		KeyBindingDef captureDef = CaptureDef;
		SetKey(captureDef, keyCode);
		string text = FindConflictLabel(keyCode, captureDef);
		CancelCapture();
		current.Use();
		if (text != null && (int)keyCode != 99 && (int)keyCode != 113)
		{
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SDS.Settings.HotkeyConflict", NamedArgument.op_Implicit(text))), MessageTypeDefOf.RejectInput, false);
		}
		return true;
	}

	private static void SetKey(KeyBindingDef def, KeyCode code)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		if (def != null)
		{
			SetBindingRaw(def, code);
			SaveBindings();
		}
	}

	private static void SetBindingRaw(KeyBindingDef def, KeyCode code)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		EnsureBindings();
		if (KeyPrefs.KeyPrefsData != null)
		{
			if (!KeyPrefs.KeyPrefsData.SetBinding(def, (BindingSlot)0, code))
			{
				KeyPrefs.KeyPrefsData.AddMissingDefaultBindings();
				KeyPrefs.KeyPrefsData.SetBinding(def, (BindingSlot)0, code);
			}
			KeyPrefs.KeyPrefsData.SetBinding(def, (BindingSlot)1, (KeyCode)0);
		}
	}

	private static void SaveBindings()
	{
		if (KeyPrefs.KeyPrefsData != null)
		{
			KeyPrefs.Save();
		}
	}

	private static void EnsureBindings()
	{
		if (!bindingsEnsured && KeyPrefs.KeyPrefsData != null)
		{
			KeyPrefs.KeyPrefsData.AddMissingDefaultBindings();
			bindingsEnsured = true;
		}
	}

	private static bool IsModifierOnly(KeyCode code)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		if (code - 303 <= 7)
		{
			return true;
		}
		return false;
	}

	private static IEnumerable<KeyBindingDef> OwnDefs()
	{
		if (OpenDef != null)
		{
			yield return OpenDef;
		}
		if (OutputDef != null)
		{
			yield return OutputDef;
		}
		if (UseDef != null)
		{
			yield return UseDef;
		}
	}

	private static bool IsOwnHotkey(KeyBindingDef def)
	{
		foreach (KeyBindingDef item in OwnDefs())
		{
			if (item == def)
			{
				return true;
			}
		}
		return false;
	}

	private static string FindConflictLabel(KeyCode code, KeyBindingDef except)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		if (KeyPrefs.KeyPrefsData == null)
		{
			return null;
		}
		foreach (KeyBindingDef allDef in DefDatabase<KeyBindingDef>.AllDefs)
		{
			if (allDef == null || allDef == except || IsOwnHotkey(allDef) || allDef.MainKey != code)
			{
				continue;
			}
			return ((object)((Def)allDef).LabelCap/*cast due to constrained. prefix*/).ToString();
		}
		return null;
	}

	internal static bool CaptureIsStale()
	{
		if (!Capturing)
		{
			return false;
		}
		if (Time.frameCount - lastSettingsFrame > 2)
		{
			return true;
		}
		return Find.WindowStack == null || Find.WindowStack.WindowOfType<Dialog_ModSettings>() == null;
	}
}
[HarmonyPatch(typeof(WindowStack), "HandleEventsHighPriority")]
internal static class Patch_SDS_Hotkeys
{
	private static bool Prefix()
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		if (SDS_Hotkeys.Capturing)
		{
			if (!SDS_Hotkeys.CaptureIsStale())
			{
				return !SDS_Hotkeys.TryCaptureKey(Event.current);
			}
			SDS_Hotkeys.CancelCapture();
		}
		if (Event.current != null && (int)Event.current.type == 0 && Event.current.button == 2 && Find.WindowStack != null)
		{
			Window_SDS_OutputDialog window_SDS_OutputDialog = Find.WindowStack.WindowOfType<Window_SDS_OutputDialog>();
			if (window_SDS_OutputDialog != null && Find.WindowStack.GetsInput((Window)(object)window_SDS_OutputDialog))
			{
				Event.current.Use();
				window_SDS_OutputDialog.ConfirmFromMiddleClick();
				return false;
			}
		}
		if (!SDS_Hotkeys.CanUseGlobalHotkeys())
		{
			return true;
		}
		KeyBindingDef openDef = SDS_Hotkeys.OpenDef;
		if (openDef != null && openDef.KeyDownEvent && !SDS_Hotkeys.MustYieldHotkey(openDef.MainKey))
		{
			Event.current.Use();
			SDS_Hotkeys.ToggleSidebar();
			return false;
		}
		KeyBindingDef outputDef = SDS_Hotkeys.OutputDef;
		if (outputDef != null && outputDef.KeyDownEvent && !SDS_Hotkeys.MustYieldHotkey(outputDef.MainKey) && SDS_Hotkeys.TryOutputFromSidebar())
		{
			Event.current.Use();
			return false;
		}
		KeyBindingDef useDef = SDS_Hotkeys.UseDef;
		if (useDef != null && useDef.KeyDownEvent && !SDS_Hotkeys.MustYieldHotkey(useDef.MainKey) && SDS_Hotkeys.TryUseFromSidebar())
		{
			Event.current.Use();
			return false;
		}
		return true;
	}
}
public static class SDS_HediffDefOf
{
	public const string ChipAName = "SDS_ImplantHediffA";

	public const string ChipBName = "SDS_ImplantHediffB";

	private static bool lookedUp;

	private static HediffDef chipA;

	private static HediffDef chipB;

	public static HediffDef ChipA
	{
		get
		{
			EnsureLookup();
			return chipA;
		}
	}

	public static HediffDef ChipB
	{
		get
		{
			EnsureLookup();
			return chipB;
		}
	}

	private static void EnsureLookup()
	{
		if (!lookedUp)
		{
			lookedUp = true;
			chipA = DefDatabase<HediffDef>.GetNamedSilentFail("SDS_ImplantHediffA");
			chipB = DefDatabase<HediffDef>.GetNamedSilentFail("SDS_ImplantHediffB");
			if (chipA == null || chipB == null)
			{
				Log.Error("[超维存储] 找不到植入体芯片 HediffDef（SDS_ImplantHediffA / SDS_ImplantHediffB）：植入体端口不会启用（Game 可正常运行）。请检查 Defs/SDS_Implants.xml 是否随 Mod 一起安装。");
			}
		}
	}
}
public static class SDS_ImplantUtility
{
	private sealed class ChipCacheEntry
	{
		public HediffDef chip;

		public long sig;
	}

	private static readonly Dictionary<Pawn, ChipCacheEntry> chipCache = new Dictionary<Pawn, ChipCacheEntry>();

	private const int ChipCachePruneThreshold = 512;

	private static bool brainDefsResolved;

	private static BodyPartDef normalBrain;

	private static BodyPartDef mechBrain;

	public static void NotifyChipStateChanged()
	{
		ResetChipCache();
		SDS_AccessPointResolver.Invalidate();
	}

	internal static void ResetChipCache()
	{
		chipCache.Clear();
	}

	private static void PruneChipCacheIfNeeded()
	{
		if (chipCache.Count < 512)
		{
			return;
		}
		List<Pawn> list = new List<Pawn>();
		foreach (KeyValuePair<Pawn, ChipCacheEntry> item in chipCache)
		{
			Pawn key = item.Key;
			if (((Thing)key).Destroyed || !((Thing)key).Spawned)
			{
				list.Add(key);
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			chipCache.Remove(list[i]);
		}
	}

	private static void ResolveBrainDefs()
	{
		if (!brainDefsResolved)
		{
			brainDefsResolved = true;
			normalBrain = DefDatabase<BodyPartDef>.GetNamedSilentFail("Brain");
			mechBrain = DefDatabase<BodyPartDef>.GetNamedSilentFail("ArtificialBrain");
		}
	}

	public static BodyPartRecord ResolveBrainPart(Pawn p)
	{
		if (p == null || p.RaceProps == null || p.RaceProps.body == null)
		{
			return null;
		}
		ResolveBrainDefs();
		if (normalBrain != null)
		{
			BodyPartRecord val = GenCollection.FirstOrFallback<BodyPartRecord>((IEnumerable<BodyPartRecord>)p.RaceProps.body.GetPartsWithDef(normalBrain), (BodyPartRecord)null);
			if (val != null)
			{
				return val;
			}
		}
		if (mechBrain != null)
		{
			BodyPartRecord val2 = GenCollection.FirstOrFallback<BodyPartRecord>((IEnumerable<BodyPartRecord>)p.RaceProps.body.GetPartsWithDef(mechBrain), (BodyPartRecord)null);
			if (val2 != null)
			{
				return val2;
			}
		}
		return null;
	}

	public static HediffDef CurrentChipHediffDef(Pawn p)
	{
		if (p == null || p.health == null || p.health.hediffSet == null)
		{
			return null;
		}
		HediffDef chipA = SDS_HediffDefOf.ChipA;
		HediffDef chipB = SDS_HediffDefOf.ChipB;
		if (chipA == null && chipB == null)
		{
			return null;
		}
		HediffDef val = null;
		HediffDef val2 = null;
		List<Hediff> hediffs = p.health.hediffSet.hediffs;
		for (int i = 0; i < hediffs.Count; i++)
		{
			Hediff val3 = hediffs[i];
			if (val3 != null && val3.def != null)
			{
				if (chipA != null && val3.def == chipA)
				{
					val = chipA;
				}
				else if (chipB != null && val3.def == chipB)
				{
					val2 = chipB;
				}
			}
		}
		return val2 ?? val;
	}

	public static bool IsChipOperational(Pawn p, HediffDef chip)
	{
		if (p == null || chip == null || p.Dead || p.InMentalState)
		{
			return false;
		}
		if (!((Thing)p).Spawned || ((Thing)p).Map == null)
		{
			return false;
		}
		if (((Thing)p).Faction != Faction.OfPlayer)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			return false;
		}
		if (chip == SDS_HediffDefOf.ChipB)
		{
			return instance.Available;
		}
		return instance.MapHasOperationalCore(((Thing)p).Map);
	}

	private static Pawn OverseerOf(Pawn p)
	{
		if (p == null || !ModsConfig.BiotechActive || p.relations == null || p.RaceProps == null || !p.RaceProps.IsMechanoid)
		{
			return null;
		}
		return p.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Overseer, (Predicate<Pawn>)null);
	}

	public static HediffDef EffectiveChipDef(Pawn p)
	{
		if (p == null || p.health == null || p.health.hediffSet == null)
		{
			return null;
		}
		PruneChipCacheIfNeeded();
		Pawn val = OverseerOf(p);
		long num = ChipSignature(p, val);
		if (chipCache.TryGetValue(p, out var value) && value != null && value.sig == num)
		{
			return value.chip;
		}
		HediffDef val2 = CurrentChipHediffDef(p);
		if (val2 == null && val != null && !((Thing)val).Destroyed && !val.Dead && val.health != null)
		{
			val2 = CurrentChipHediffDef(val);
		}
		if (value == null)
		{
			value = new ChipCacheEntry();
			chipCache[p] = value;
		}
		value.chip = val2;
		value.sig = num;
		return val2;
	}

	private static long ChipSignature(Pawn p, Pawn overseer)
	{
		long num = ((p.health != null && p.health.hediffSet != null) ? p.health.hediffSet.hediffs.Count : 0);
		num = num * 1000003 + (((Thing)(overseer?)).thingIDNumber ?? (-1));
		long num2 = ((overseer != null && overseer.health != null && overseer.health.hediffSet != null) ? overseer.health.hediffSet.hediffs.Count : 0);
		num = num * 1000003 + num2;
		num = num * 31 + ((overseer != null && overseer.Dead) ? 1 : 0);
		return num * 31 + ((overseer != null && ((Thing)overseer).Destroyed) ? 2 : 0);
	}

	public static bool IsPortOperational(Pawn p)
	{
		HediffDef val = EffectiveChipDef(p);
		return val != null && IsChipOperational(p, val);
	}
}
public class SDS_CompUsable : CompUsable
{
	protected override string FloatMenuOptionLabel(Pawn pawn)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		return ((object)TranslatorFormattedStringExtensions.Translate("SDS.Chip.UseLabel", NamedArgument.op_Implicit(((Entity)((ThingComp)this).parent).LabelShort))/*cast due to constrained. prefix*/).ToString();
	}
}
public class SDS_CompProperties_InstallChip : CompProperties_UseEffectInstallImplant
{
	public HediffDef replacedHediffDef;

	public SDS_CompProperties_InstallChip()
	{
		((CompProperties)this).compClass = typeof(SDS_CompInstallChip);
	}
}
public class SDS_CompInstallChip : CompUseEffect_InstallImplant
{
	public SDS_CompProperties_InstallChip ChipProps => (SDS_CompProperties_InstallChip)(object)((ThingComp)this).props;

	public override AcceptanceReport CanBeUsedBy(Pawn p)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		if (p == null || p.health == null || p.RaceProps == null || p.RaceProps.body == null)
		{
			return AcceptanceReport.op_Implicit(false);
		}
		if (p.RaceProps.IsMechanoid)
		{
			return AcceptanceReport.op_Implicit(Translator.Translate("SDS.Chip.MechCannotInstall"));
		}
		if ((!p.IsFreeColonist || QuestUtility.HasExtraHomeFaction(p, (Quest)null)) && !((CompUseEffect_InstallImplant)this).Props.allowNonColonists)
		{
			return AcceptanceReport.op_Implicit(Translator.Translate("InstallImplantNotAllowedForNonColonists"));
		}
		if (SDS_ImplantUtility.ResolveBrainPart(p) == null)
		{
			return AcceptanceReport.op_Implicit(Translator.Translate("InstallImplantNoBodyPart") + ": " + ((CompUseEffect_InstallImplant)this).Props.bodyPart.LabelShort);
		}
		HediffDef hediffDef = ((CompUseEffect_InstallImplant)this).Props.hediffDef;
		HediffDef val = SDS_ImplantUtility.CurrentChipHediffDef(p);
		if (hediffDef == SDS_HediffDefOf.ChipB)
		{
			return (val == SDS_HediffDefOf.ChipB) ? AcceptanceReport.op_Implicit(Translator.Translate("InstallImplantAlreadyInstalled")) : AcceptanceReport.op_Implicit(true);
		}
		if (val == SDS_HediffDefOf.ChipB)
		{
			return AcceptanceReport.op_Implicit(Translator.Translate("SDS.Chip.HigherAlreadyInstalled"));
		}
		return (val == null) ? AcceptanceReport.op_Implicit(true) : AcceptanceReport.op_Implicit(Translator.Translate("InstallImplantAlreadyInstalled"));
	}

	public override void DoEffect(Pawn user)
	{
		if (user == null || user.health == null)
		{
			return;
		}
		if (ChipProps.replacedHediffDef != null)
		{
			Hediff firstHediffOfDef = user.health.hediffSet.GetFirstHediffOfDef(ChipProps.replacedHediffDef, false);
			if (firstHediffOfDef != null)
			{
				user.health.RemoveHediff(firstHediffOfDef);
			}
		}
		HediffDef hediffDef = ((CompUseEffect_InstallImplant)this).Props.hediffDef;
		if (hediffDef != null && user.health.hediffSet.GetFirstHediffOfDef(hediffDef, false) == null)
		{
			BodyPartRecord val = SDS_ImplantUtility.ResolveBrainPart(user);
			if (val != null)
			{
				user.health.AddHediff(hediffDef, val, (DamageInfo?)null, (DamageResult)null);
			}
			else
			{
				Log.Error("[超维存储] 未能为 " + ((Entity)user).LabelShort + " 找到脑部零件，芯片未植入。");
			}
		}
		SDS_ImplantUtility.NotifyChipStateChanged();
		ConsumeChip();
	}

	private void ConsumeChip()
	{
		Thing parent = (Thing)(object)((ThingComp)this).parent;
		if (parent != null && !parent.Destroyed && parent.stackCount > 0)
		{
			Thing val = parent.SplitOff(1);
			if (val != null && !val.Destroyed)
			{
				val.Destroy((DestroyMode)0);
			}
		}
	}
}
internal static class SDS_ProducedThingMarker
{
	private const int LifetimeTicks = 60000;

	private const int MaxEntries = 8192;

	private const int PruneIntervalTicks = 500;

	private static readonly Dictionary<Thing, int> marked = new Dictionary<Thing, int>();

	private static int nextPruneTick;

	internal static void Reset()
	{
		marked.Clear();
		nextPruneTick = 0;
	}

	public static void Mark(Thing thing)
	{
		if (thing != null && !thing.Destroyed)
		{
			int num = ((Find.TickManager != null) ? Find.TickManager.TicksGame : 0);
			PruneIfNeeded(num);
			if (marked.Count < 8192)
			{
				marked[thing] = num + 60000;
			}
		}
	}

	public static void MarkAll(List<Thing> things)
	{
		if (things != null)
		{
			for (int i = 0; i < things.Count; i++)
			{
				Mark(things[i]);
			}
		}
	}

	public static bool IsMarked(Thing thing)
	{
		if (thing == null || marked.Count == 0)
		{
			return false;
		}
		int num = ((Find.TickManager != null) ? Find.TickManager.TicksGame : 0);
		PruneIfNeeded(num);
		if (!marked.TryGetValue(thing, out var value))
		{
			return false;
		}
		if (num >= value)
		{
			marked.Remove(thing);
			return false;
		}
		return true;
	}

	private static void PruneIfNeeded(int now)
	{
		if (now < nextPruneTick && marked.Count < 8192)
		{
			return;
		}
		nextPruneTick = now + 500;
		if (marked.Count == 0)
		{
			return;
		}
		List<Thing> list = null;
		foreach (KeyValuePair<Thing, int> item in marked)
		{
			Thing key = item.Key;
			if (key == null || key.Destroyed || now >= item.Value)
			{
				if (list == null)
				{
					list = new List<Thing>();
				}
				list.Add(key);
			}
		}
		if (list == null)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] != null)
			{
				marked.Remove(list[i]);
			}
		}
	}
}
internal static class SDS_DepositDispatchLedger
{
	private const int PendingTicks = 300;

	private const int FailureCooldownTicks = 120;

	private const int PruneIntervalTicks = 500;

	private const int MaxEntries = 4096;

	private static readonly Dictionary<Thing, int> pendingUntil = new Dictionary<Thing, int>();

	private static readonly Dictionary<long, int> failureUntil = new Dictionary<long, int>();

	private static int nextPruneTick;

	private static int Now => (Find.TickManager != null) ? Find.TickManager.TicksGame : 0;

	internal static void Reset()
	{
		pendingUntil.Clear();
		failureUntil.Clear();
		nextPruneTick = 0;
	}

	public static bool IsPending(Thing thing)
	{
		if (thing == null || thing.Destroyed)
		{
			return false;
		}
		int now = Now;
		PruneIfNeeded(now);
		if (!pendingUntil.TryGetValue(thing, out var value))
		{
			return false;
		}
		if (now >= value)
		{
			pendingUntil.Remove(thing);
			return false;
		}
		return true;
	}

	public static void MarkPending(Thing thing)
	{
		if (thing != null && !thing.Destroyed)
		{
			int now = Now;
			PruneIfNeeded(now);
			int num = now + 300;
			if (!pendingUntil.TryGetValue(thing, out var value) || value < num)
			{
				pendingUntil[thing] = num;
			}
		}
	}

	public static void Clear(Thing thing)
	{
		if (thing != null)
		{
			pendingUntil.Remove(thing);
		}
	}

	public static bool IsFailureCoolingDown(Pawn pawn, Thing thing)
	{
		if (pawn == null || thing == null)
		{
			return false;
		}
		int now = Now;
		PruneIfNeeded(now);
		long key = MakeKey(pawn, thing);
		if (!failureUntil.TryGetValue(key, out var value))
		{
			return false;
		}
		if (now >= value)
		{
			failureUntil.Remove(key);
			return false;
		}
		return true;
	}

	public static void MarkFailure(Pawn pawn, Thing thing)
	{
		if (pawn != null && thing != null)
		{
			int now = Now;
			PruneIfNeeded(now);
			failureUntil[MakeKey(pawn, thing)] = now + 120;
			pendingUntil[thing] = now + 120;
		}
	}

	private static long MakeKey(Pawn pawn, Thing thing)
	{
		return (long)(((ulong)(uint)((Thing)pawn).thingIDNumber << 32) | (uint)thing.thingIDNumber);
	}

	private static void PruneIfNeeded(int now)
	{
		if (now < nextPruneTick && pendingUntil.Count + failureUntil.Count < 4096)
		{
			return;
		}
		nextPruneTick = now + 500;
		if (pendingUntil.Count > 0)
		{
			List<Thing> list = null;
			foreach (KeyValuePair<Thing, int> item in pendingUntil)
			{
				Thing key = item.Key;
				if (key == null || key.Destroyed || now >= item.Value)
				{
					if (list == null)
					{
						list = new List<Thing>();
					}
					list.Add(key);
				}
			}
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i] != null)
					{
						pendingUntil.Remove(list[i]);
					}
				}
			}
		}
		if (failureUntil.Count <= 0)
		{
			return;
		}
		List<long> list2 = null;
		foreach (KeyValuePair<long, int> item2 in failureUntil)
		{
			if (now >= item2.Value)
			{
				if (list2 == null)
				{
					list2 = new List<long>();
				}
				list2.Add(item2.Key);
			}
		}
		if (list2 != null)
		{
			for (int j = 0; j < list2.Count; j++)
			{
				failureUntil.Remove(list2[j]);
			}
		}
	}
}
internal static class SDS_ChipManualDeposit
{
	public static bool PawnCanDeposit(Pawn pawn)
	{
		return pawn != null && SDS_ImplantUtility.IsPortOperational(pawn);
	}

	public static bool CanDepositThing(Thing thing)
	{
		if (thing == null || thing.Destroyed)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		return instance != null && instance.Available && instance.CanDepositThing(thing);
	}

	public static Job MakeDepositJob(Pawn pawn, Thing thing)
	{
		if (pawn == null || thing == null || SDS_JobDefOf.DepositToGrid == null)
		{
			return null;
		}
		Job val = SDS_DepositService.TryMakeManual(pawn, thing);
		if (val == null)
		{
			return null;
		}
		return val;
	}

	public static bool ShouldReportToPlayer(Job job)
	{
		return job != null && job.playerForced && job.workGiverDef == null;
	}

	public static int DepositInventory(Pawn pawn)
	{
		if (pawn == null || pawn.inventory == null)
		{
			return 0;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available || !SDS_ImplantUtility.IsPortOperational(pawn))
		{
			return 0;
		}
		ThingOwner innerContainer = (ThingOwner)(object)pawn.inventory.innerContainer;
		return SDS_DepositService.DepositNow(pawn, (IEnumerable<Thing>)innerContainer);
	}

	public static Gizmo MakeInventoryDepositGizmo(Pawn pawn)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		Texture2D icon = ContentFinder<Texture2D>.Get("UI/Commands/Install", false);
		return (Gizmo)new Command_Action
		{
			defaultLabel = ((object)Translator.Translate("SDS.Chip.DepositInventory")/*cast due to constrained. prefix*/).ToString(),
			defaultDesc = ((object)Translator.Translate("SDS.Chip.DepositInventoryDesc")/*cast due to constrained. prefix*/).ToString(),
			icon = (Texture)(object)icon,
			action = delegate
			{
				//IL_0047: Unknown result type (might be due to invalid IL or missing references)
				//IL_004c: Unknown result type (might be due to invalid IL or missing references)
				//IL_001e: Unknown result type (might be due to invalid IL or missing references)
				int num = DepositInventory(pawn);
				if (num <= 0)
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("SDS.Chip.NothingToDeposit")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
				}
				else
				{
					Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SDS.Chip.Deposited", NamedArgument.op_Implicit(num))), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.TaskCompletion, false);
				}
			}
		};
	}

	public static IEnumerable<Gizmo> AppendInventoryDepositGizmo(IEnumerable<Gizmo> original, Pawn pawn)
	{
		if (original != null)
		{
			foreach (Gizmo gizmo in original)
			{
				if (gizmo != null)
				{
					yield return gizmo;
				}
			}
		}
		yield return MakeInventoryDepositGizmo(pawn);
	}
}
[HarmonyPatch(typeof(FloatMenuMakerMap), "GetOptions")]
internal static class Patch_SDS_FloatMenuDeposit
{
	public static void Postfix(Vector3 clickPos, List<Pawn> selectedPawns, List<FloatMenuOption> __result)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__result == null || selectedPawns == null || selectedPawns.Count != 1)
			{
				return;
			}
			Pawn pawn = selectedPawns[0];
			if (!SDS_ChipManualDeposit.PawnCanDeposit(pawn))
			{
				return;
			}
			Thing val = FirstDepositableThingNear(clickPos, pawn);
			if (val != null)
			{
				FloatMenuOption val2 = MakeDepositOption(pawn, val);
				if (val2 != null)
				{
					__result.Add(val2);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("超维空间右键「存入超维空间」入口生成失败（已忽略，不影响原版菜单）", exception);
		}
	}

	private static Thing FirstDepositableThingNear(Vector3 clickPos, Pawn pawn)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		Map val = ((pawn != null) ? ((Thing)pawn).MapHeld : null);
		if (val == null)
		{
			return null;
		}
		IntVec3 val2 = IntVec3Utility.ToIntVec3(clickPos);
		if (!GenGrid.InBounds(val2, val))
		{
			return null;
		}
		for (int i = 0; i < 5; i++)
		{
			IntVec3 val3 = ((i == 0) ? val2 : (val2 + GenAdj.AdjacentCells[i - 1]));
			if (!GenGrid.InBounds(val3, val))
			{
				continue;
			}
			List<Thing> list = val.thingGrid.ThingsListAt(val3);
			for (int j = 0; j < list.Count; j++)
			{
				Thing val4 = list[j];
				if (IsDepositableMapItem(val4) && SDS_ChipManualDeposit.CanDepositThing(val4))
				{
					return val4;
				}
			}
		}
		return null;
	}

	private static bool IsDepositableMapItem(Thing thing)
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Invalid comparison between Unknown and I4
		if (thing == null || thing.def == null || thing.Destroyed || !thing.Spawned)
		{
			return false;
		}
		if (thing is Pawn || thing is Building)
		{
			return false;
		}
		return (int)thing.def.category == 2;
	}

	private static FloatMenuOption MakeDepositOption(Pawn pawn, Thing thing)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Expected O, but got Unknown
		if (pawn == null || thing == null || SDS_JobDefOf.DepositToGrid == null)
		{
			return null;
		}
		Action action = delegate
		{
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			string text = ((thing != null && !thing.Destroyed) ? ((Entity)thing).LabelShort : string.Empty);
			if (thing == null || thing.Destroyed || !thing.Spawned || !ReservationUtility.CanReserveAndReach(pawn, LocalTargetInfo.op_Implicit(thing), (PathEndMode)3, (Danger)3, 1, Math.Max(1, thing.stackCount), (ReservationLayerDef)null, false))
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SDS.Chip.DepositItemFailed", NamedArgument.op_Implicit(text))), MessageTypeDefOf.RejectInput, false);
			}
			else
			{
				Job val = SDS_ChipManualDeposit.MakeDepositJob(pawn, thing);
				if (val != null)
				{
					pawn.jobs.TryTakeOrderedJob(val, (JobTag?)(JobTag)0, false);
				}
			}
		};
		return new FloatMenuOption(((object)TranslatorFormattedStringExtensions.Translate("SDS.Chip.DepositItem", NamedArgument.op_Implicit(((Entity)thing).LabelShort))/*cast due to constrained. prefix*/).ToString(), action, (MenuOptionPriority)4, (Action<Rect>)null, thing, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
	}
}
[HarmonyPatch(typeof(Pawn), "GetGizmos")]
internal static class Patch_SDS_PawnDepositGizmo
{
	public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
	{
		try
		{
			if (__instance != null && SDS_ChipManualDeposit.PawnCanDeposit(__instance))
			{
				__result = SDS_ChipManualDeposit.AppendInventoryDepositGizmo(__result, __instance);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("超维空间「存入背包」gizmo 安装失败（已忽略，原版 gizmo 不受影响）", exception);
		}
	}
}
public class JobDriver_SDS_DepositToGrid : JobDriver
{
	internal const TargetIndex ItemInd = (TargetIndex)1;

	private int nextIndex;

	private int stuckTicks;

	private bool partialPickup;

	private bool pickupBlocked;

	private int partialRemaining;

	private List<LocalTargetInfo> Queue => (base.job != null) ? base.job.targetQueueA : null;

	private int QueueCount
	{
		get
		{
			List<LocalTargetInfo> queue = Queue;
			if (queue != null && queue.Count > 0)
			{
				return queue.Count;
			}
			return (base.job != null && ((LocalTargetInfo)(ref base.job.targetA)).IsValid) ? 1 : 0;
		}
	}

	private Thing EndpointThing => (base.job != null) ? ((LocalTargetInfo)(ref base.job.targetB)).Thing : null;

	private bool SelfEndpoint
	{
		get
		{
			Thing endpointThing = EndpointThing;
			return endpointThing == null || (object)endpointThing == base.pawn;
		}
	}

	private Thing ItemAt(int index)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		List<LocalTargetInfo> queue = Queue;
		if (queue != null && queue.Count > 0)
		{
			object result;
			if (index < 0 || index >= queue.Count)
			{
				result = null;
			}
			else
			{
				LocalTargetInfo val = queue[index];
				result = ((LocalTargetInfo)(ref val)).Thing;
			}
			return (Thing)result;
		}
		return (index == 0 && base.job != null) ? ((LocalTargetInfo)(ref base.job.targetA)).Thing : null;
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		if (base.pawn == null || base.job == null)
		{
			return false;
		}
		if (base.pawn.inventory == null || base.pawn.inventory.innerContainer == null)
		{
			return false;
		}
		List<LocalTargetInfo> list = base.job.targetQueueA;
		if (list == null || list.Count == 0)
		{
			list = new List<LocalTargetInfo>();
			if (((LocalTargetInfo)(ref base.job.targetA)).IsValid)
			{
				list.Add(base.job.targetA);
			}
			base.job.targetQueueA = list;
		}
		if (base.job.countQueue == null)
		{
			base.job.countQueue = new List<int>();
		}
		List<LocalTargetInfo> list2 = new List<LocalTargetInfo>();
		List<int> list3 = new List<int>();
		for (int i = 0; i < list.Count; i++)
		{
			LocalTargetInfo val = list[i];
			Thing thing = ((LocalTargetInfo)(ref val)).Thing;
			if (thing == null || thing.Destroyed)
			{
				continue;
			}
			if ((object)thing.holdingOwner == base.pawn.inventory.innerContainer)
			{
				list2.Add(list[i]);
				list3.Add(Math.Max(1, thing.stackCount));
			}
			else if (base.pawn.carryTracker != null && base.pawn.carryTracker.CarriedThing == thing)
			{
				list2.Add(list[i]);
				list3.Add(Math.Max(1, thing.stackCount));
			}
			else if (thing.Spawned)
			{
				int num = Math.Max(1, thing.stackCount);
				if (ReservationUtility.Reserve(base.pawn, new LocalTargetInfo(thing), base.job, 1, num, (ReservationLayerDef)null, errorOnFailed, false))
				{
					list2.Add(list[i]);
					list3.Add(num);
					SDS_DepositDispatchLedger.MarkPending(thing);
				}
			}
		}
		list.Clear();
		list.AddRange(list2);
		base.job.countQueue = list3;
		if (list.Count == 0)
		{
			ClearPending(list);
			return false;
		}
		base.job.SetTarget((TargetIndex)1, list[0]);
		base.job.count = list3[0];
		Thing endpointThing = EndpointThing;
		if (endpointThing != null && (object)endpointThing != base.pawn && (endpointThing.Destroyed || !endpointThing.Spawned))
		{
			ClearPending(list);
			return false;
		}
		return true;
	}

	private void ClearPending(List<LocalTargetInfo> queue)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (queue == null)
		{
			return;
		}
		for (int i = 0; i < queue.Count; i++)
		{
			LocalTargetInfo val = queue[i];
			Thing thing = ((LocalTargetInfo)(ref val)).Thing;
			if (thing != null)
			{
				SDS_DepositDispatchLedger.Clear(thing);
			}
		}
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		if (base.job == null)
		{
			yield break;
		}
		if (base.job.targetQueueA == null || base.job.targetQueueA.Count == 0)
		{
			base.job.targetQueueA = new List<LocalTargetInfo>();
			if (((LocalTargetInfo)(ref base.job.targetA)).IsValid)
			{
				base.job.targetQueueA.Add(base.job.targetA);
			}
		}
		Thing endpoint = EndpointThing;
		PathEndMode endpointMode = EndpointPathMode(endpoint);
		if (endpoint != null && (object)endpoint != base.pawn)
		{
			yield return Toils_General.Do((Action)delegate
			{
				//IL_002c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0032: Unknown result type (might be due to invalid IL or missing references)
				//IL_0042: Unknown result type (might be due to invalid IL or missing references)
				Thing endpointThing = EndpointThing;
				if (endpointThing == null || endpointThing.Destroyed || !endpointThing.Spawned || !ReachabilityUtility.CanReach(base.pawn, LocalTargetInfo.op_Implicit(endpointThing), endpointMode, DangerUtility.NormalMaxDanger(base.pawn), false, false, (TraverseMode)0))
				{
					FailWithMessage(null);
				}
			});
		}
		nextIndex = 0;
		stuckTicks = 0;
		Toil deposit = ToilMaker.MakeToil("SDS_DepositAll");
		deposit.defaultCompleteMode = (ToilCompleteMode)1;
		deposit.initAction = DepositAll;
		Toil gotoEndpoint = null;
		if (!SelfEndpoint)
		{
			gotoEndpoint = ToilMaker.MakeToil("SDS_GotoEndpoint");
			gotoEndpoint.initAction = delegate
			{
				//IL_0049: Unknown result type (might be due to invalid IL or missing references)
				//IL_004f: Unknown result type (might be due to invalid IL or missing references)
				Thing endpointThing = EndpointThing;
				if (endpointThing == null || endpointThing.Destroyed || !endpointThing.Spawned)
				{
					FailWithMessage(null);
				}
				else
				{
					base.pawn.pather.StartPath(LocalTargetInfo.op_Implicit(endpointThing), endpointMode);
				}
			};
			gotoEndpoint.defaultCompleteMode = (ToilCompleteMode)2;
			gotoEndpoint.AddFailCondition((Func<bool>)delegate
			{
				//IL_002c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0032: Unknown result type (might be due to invalid IL or missing references)
				//IL_0042: Unknown result type (might be due to invalid IL or missing references)
				Thing endpointThing = EndpointThing;
				return endpointThing == null || endpointThing.Destroyed || !endpointThing.Spawned || !ReachabilityUtility.CanReach(base.pawn, LocalTargetInfo.op_Implicit(endpointThing), endpointMode, DangerUtility.NormalMaxDanger(base.pawn), false, false, (TraverseMode)0);
			});
		}
		Toil next = gotoEndpoint ?? deposit;
		Toil pickup = ToilMaker.MakeToil("SDS_PickupLoop");
		pickup.defaultCompleteMode = (ToilCompleteMode)5;
		pickup.initAction = delegate
		{
			StartNextPickup();
		};
		pickup.tickAction = delegate
		{
			TickPickup();
			if (nextIndex >= QueueCount && base.pawn != null && base.pawn.jobs != null)
			{
				base.pawn.jobs.curDriver.JumpToToil(next);
			}
		};
		pickup.AddFailCondition((Func<bool>)(() => base.pawn == null || base.pawn.Dead || base.pawn.Downed || !((Thing)base.pawn).Spawned || ((Thing)base.pawn).Map == null));
		yield return pickup;
		if (gotoEndpoint != null)
		{
			yield return gotoEndpoint;
		}
		yield return deposit;
	}

	private static PathEndMode EndpointPathMode(Thing endpoint)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		if (endpoint == null || endpoint is Pawn)
		{
			return (PathEndMode)2;
		}
		if (SDS_EndpointHandle.TryFromThing(endpoint, out var handle))
		{
			return handle.AccessMode;
		}
		return (PathEndMode)2;
	}

	private void StartNextPickup()
	{
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		stuckTicks = 0;
		int queueCount = QueueCount;
		while (nextIndex < queueCount)
		{
			Thing val = ItemAt(nextIndex);
			if (val == null || val.Destroyed || !val.Spawned || val.Map != ((Thing)base.pawn).Map || (object)val.holdingOwner == base.pawn.inventory.innerContainer)
			{
				nextIndex++;
				continue;
			}
			if (!ReachabilityUtility.CanReach(base.pawn, LocalTargetInfo.op_Implicit(val), (PathEndMode)3, DangerUtility.NormalMaxDanger(base.pawn), false, false, (TraverseMode)0))
			{
				SDS_DepositDispatchLedger.MarkFailure(base.pawn, val);
				nextIndex++;
				continue;
			}
			if (!ReservationUtility.CanReserve(base.pawn, LocalTargetInfo.op_Implicit(val), 1, Math.Max(1, val.stackCount), (ReservationLayerDef)null, false))
			{
				nextIndex++;
				continue;
			}
			base.pawn.pather.StartPath(LocalTargetInfo.op_Implicit(val), (PathEndMode)3);
			break;
		}
	}

	private void TickPickup()
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		if (nextIndex >= QueueCount)
		{
			return;
		}
		Thing val = ItemAt(nextIndex);
		if (val == null || val.Destroyed || !val.Spawned)
		{
			nextIndex++;
			StartNextPickup();
			return;
		}
		IntVec3 val2 = ((Thing)base.pawn).Position - val.Position;
		if (((IntVec3)(ref val2)).LengthHorizontalSquared <= 2)
		{
			Pickup(val, nextIndex);
			nextIndex++;
			StartNextPickup();
		}
		else if (!base.pawn.pather.Moving)
		{
			stuckTicks++;
			if (stuckTicks > 120)
			{
				SDS_DepositDispatchLedger.MarkFailure(base.pawn, val);
				nextIndex++;
				StartNextPickup();
			}
			else
			{
				StartNextPickup();
			}
		}
	}

	private void Pickup(Thing item, int index)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_031b: Unknown result type (might be due to invalid IL or missing references)
		if (item == null || item.Destroyed || !item.Spawned)
		{
			return;
		}
		if (SDS_MirrorRegistry.IsMirror(item))
		{
			Log.WarningOnce("[SDS] 存入执行器收到镜像，已拒绝；这属于逻辑错误，不应发生。", 1566375937);
			pickupBlocked = true;
			SDS_DepositDispatchLedger.MarkFailure(base.pawn, item);
			return;
		}
		IntVec3 positionHeld = item.PositionHeld;
		Map mapHeld = item.MapHeld;
		int num = SDS_ModCompat_CE.CountCanFitInInventory(base.pawn, item);
		int countCanAccept = ((ThingOwner)base.pawn.inventory.innerContainer).GetCountCanAccept(item, false);
		if (countCanAccept < num)
		{
			num = countCanAccept;
		}
		if (num <= 0 || item.stackCount <= 0)
		{
			pickupBlocked = true;
			partialRemaining += Math.Max(0, item.stackCount);
			if (base.job != null)
			{
				base.job.reportStringOverride = ((object)TranslatorFormattedStringExtensions.Translate("SDS.JobReport.DepositBlocked", NamedArgument.op_Implicit(((Entity)item).LabelShort))/*cast due to constrained. prefix*/).ToString();
			}
			SDS_DepositDispatchLedger.MarkFailure(base.pawn, item);
			return;
		}
		Thing val;
		if (num < item.stackCount)
		{
			partialPickup = true;
			partialRemaining += item.stackCount - num;
			if (base.job != null)
			{
				base.job.reportStringOverride = ((object)TranslatorFormattedStringExtensions.Translate("SDS.JobReport.DepositPartial", NamedArgument.op_Implicit(((Entity)item).LabelShort))/*cast due to constrained. prefix*/).ToString();
			}
			val = item.SplitOff(num);
			if (val == null)
			{
				SDS_DepositDispatchLedger.MarkFailure(base.pawn, item);
				return;
			}
		}
		else
		{
			((Entity)item).DeSpawn((DestroyMode)0);
			val = item;
		}
		if (!((ThingOwner)base.pawn.inventory.innerContainer).TryAdd(val, false))
		{
			SDS_Diag.Fail("存入/入包失败", SDS_Diag.Ctx(((Thing)base.pawn).Map, base.pawn, val, base.job, "count=" + (val?.stackCount ?? 0)));
			int num2 = ((val != null && !val.Destroyed) ? val.stackCount : 0);
			if (val != item && item != null && !item.Destroyed && item.Spawned && val != null && !val.Destroyed && val.Spawned && item.def == val.def && item.Stuff == val.Stuff && item.CanStackWith(val) && !SDS_MirrorRegistry.IsMirror(item) && !SDS_MirrorRegistry.IsMirror(val) && item.TryAbsorbStack(val, true))
			{
				partialRemaining += num2;
				pickupBlocked = true;
				SDS_DepositDispatchLedger.MarkFailure(base.pawn, item);
				return;
			}
			if (val != null && !val.Destroyed && !val.Spawned)
			{
				SDS_PlacementUtility.TryPlace(val, positionHeld, mapHeld, SDS_PlacementPolicy.RandomNear, out var _);
			}
			partialRemaining += num2;
			pickupBlocked = true;
			SDS_DepositDispatchLedger.MarkFailure(base.pawn, item);
			if (val != null && !val.Destroyed)
			{
				SDS_DepositDispatchLedger.MarkFailure(base.pawn, val);
			}
			return;
		}
		if (base.job.targetQueueA != null && index >= 0 && index < base.job.targetQueueA.Count)
		{
			base.job.targetQueueA[index] = new LocalTargetInfo(val);
			if (base.job.countQueue != null && index < base.job.countQueue.Count)
			{
				base.job.countQueue[index] = Math.Max(1, val.stackCount);
			}
		}
		if (val != item && item != null && !item.Destroyed)
		{
			SDS_DepositDispatchLedger.Clear(item);
		}
		SDS_ModCompat_CE.NotifyInventoryChanged(base.pawn);
	}

	private void DepositAll()
	{
		//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_034a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0351: Unknown result type (might be due to invalid IL or missing references)
		//IL_0356: Unknown result type (might be due to invalid IL or missing references)
		TakeCarriedIntoInventory();
		Thing endpointThing = EndpointThing;
		bool flag = endpointThing == null || (object)endpointThing == base.pawn;
		SDS_EndpointHandle handle = null;
		if (flag)
		{
			handle = SDS_EndpointHandle.ForPawn(base.pawn, self: true);
		}
		else
		{
			SDS_EndpointHandle.TryFromThing(endpointThing, out handle);
		}
		bool flag2 = handle == null;
		int num = 0;
		int queueCount = QueueCount;
		for (int i = 0; i < queueCount; i++)
		{
			Thing val = ItemAt(i);
			if (val == null || val.Destroyed || (object)val.holdingOwner != base.pawn.inventory.innerContainer)
			{
				continue;
			}
			if (handle == null || !handle.Accepts(val, base.job.playerForced))
			{
				flag2 = true;
				SDS_DepositDispatchLedger.MarkFailure(base.pawn, val);
				continue;
			}
			int stackCount = val.stackCount;
			if (!((ThingOwner)base.pawn.inventory.innerContainer).Remove(val))
			{
				SDS_Diag.Fail("存入/背包移除失败", SDS_Diag.Ctx(((Thing)base.pawn).Map, base.pawn, val, base.job));
				flag2 = true;
				continue;
			}
			int accepted = 0;
			if (handle != null && handle.TryDeposit(val, out accepted, base.job.playerForced) && accepted > 0)
			{
				num += stackCount;
				SDS_DepositDispatchLedger.Clear(val);
				continue;
			}
			SDS_Diag.Fail("存入/入账失败", SDS_Diag.Ctx(((Thing)base.pawn).Map, base.pawn, val, base.job, "accepted=" + accepted));
			flag2 = true;
			SDS_DepositDispatchLedger.MarkFailure(base.pawn, val);
			DropOrRespawn(val);
		}
		SDS_ModCompat_CE.NotifyInventoryChanged(base.pawn);
		if (flag2)
		{
			FailWithMessage(null);
			return;
		}
		if (num <= 0 && pickupBlocked)
		{
			FailWithMessage(null);
			return;
		}
		if (num > 0)
		{
			bool flag3 = SDS_ChipManualDeposit.ShouldReportToPlayer(base.job);
			if ((flag3 || (base.job != null && base.job.playerForced)) && (partialPickup || pickupBlocked))
			{
				Thing val2 = ((base.job != null) ? ((LocalTargetInfo)(ref base.job.targetA)).Thing : null);
				string text = ((val2 != null && !val2.Destroyed) ? ((Entity)val2).LabelShort : "?");
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SDS.Chip.DepositPartial", NamedArgument.op_Implicit(text), NamedArgument.op_Implicit(num), NamedArgument.op_Implicit(partialRemaining))), LookTargets.op_Implicit((Thing)(object)base.pawn), MessageTypeDefOf.TaskCompletion, false);
			}
			else if (flag3)
			{
				Thing val3 = ((base.job != null) ? ((LocalTargetInfo)(ref base.job.targetA)).Thing : null);
				string text2 = ((val3 != null && !val3.Destroyed) ? ((Entity)val3).LabelShort : "?");
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SDS.Chip.DepositItemDone", NamedArgument.op_Implicit(text2), NamedArgument.op_Implicit(num))), LookTargets.op_Implicit((Thing)(object)base.pawn), MessageTypeDefOf.TaskCompletion, false);
			}
		}
		((JobDriver)this).EndJobWith((JobCondition)2);
	}

	private void TakeCarriedIntoInventory()
	{
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		Pawn_CarryTracker val = ((base.pawn != null) ? base.pawn.carryTracker : null);
		if (val == null || base.pawn.inventory == null || base.pawn.inventory.innerContainer == null)
		{
			return;
		}
		Thing carriedThing = val.CarriedThing;
		if (carriedThing != null && !carriedThing.Destroyed && QueueContains(carriedThing))
		{
			Thing val2 = default(Thing);
			val.innerContainer.TryTransferToContainer(carriedThing, (ThingOwner)(object)base.pawn.inventory.innerContainer, carriedThing.stackCount, ref val2, false);
			Thing carriedThing2 = val.CarriedThing;
			if (carriedThing2 != null && !carriedThing2.Destroyed && QueueContains(carriedThing2))
			{
				SDS_Diag.Warn("存入/手上货物落地", SDS_Diag.Ctx(((Thing)base.pawn).Map, base.pawn, carriedThing2, base.job, "count=" + carriedThing2.stackCount));
				val.TryDropCarriedThing(((Thing)base.pawn).Position, (ThingPlaceMode)1, ref val2, (Action<Thing, int>)null);
			}
		}
	}

	private bool QueueContains(Thing item)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		List<LocalTargetInfo> queue = Queue;
		if (queue == null || item == null)
		{
			return false;
		}
		for (int i = 0; i < queue.Count; i++)
		{
			LocalTargetInfo val = queue[i];
			if (((LocalTargetInfo)(ref val)).Thing == item)
			{
				return true;
			}
		}
		return false;
	}

	private void DropOrRespawn(Thing item)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (item != null && !item.Destroyed)
		{
			SDS_PlacementUtility.TryPlace(item, ((Thing)base.pawn).Position, ((Thing)base.pawn).Map, SDS_PlacementPolicy.NearPawn, out var _);
		}
	}

	private void FailWithMessage(Thing item)
	{
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		Thing val = item;
		if (val == null && base.job != null)
		{
			val = ((LocalTargetInfo)(ref base.job.targetA)).Thing;
		}
		if (val != null && !val.Destroyed)
		{
			SDS_DepositDispatchLedger.MarkFailure(base.pawn, val);
		}
		if (SDS_ChipManualDeposit.ShouldReportToPlayer(base.job) || (base.job != null && base.job.playerForced))
		{
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SDS.Chip.DepositItemFailed", NamedArgument.op_Implicit((val != null && !val.Destroyed) ? ((Entity)val).LabelShort : "?"))), LookTargets.op_Implicit((Thing)(object)base.pawn), MessageTypeDefOf.RejectInput, false);
		}
		((JobDriver)this).EndJobWith((JobCondition)4);
	}
}
internal static class SDS_Invariants
{
	[Conditional("DEBUG")]
	public static void AvailableWithinTotal(SDS_StockKey key, long total, long available)
	{
		if (Prefs.DevMode && key != null && (available < 0 || available > total))
		{
			Log.Error("[超维存储][不变量] 可用量越界：def=" + key.EffectiveDefName + " preserved=" + key.preserved + " total=" + total + " available=" + available);
		}
	}

	[Conditional("DEBUG")]
	public static void TransactionStage(SDS_WithdrawTransaction transaction, SDS_WithdrawEndState expected, string stage)
	{
		if (Prefs.DevMode && transaction != null && transaction.state != expected)
		{
			Log.Error("[超维存储][不变量] 事务阶段错误：" + stage + " id=" + transaction.id + " 期望=" + expected.ToString() + " 实际=" + transaction.state);
		}
	}

	[Conditional("DEBUG")]
	public static void TransactionTerminal(SDS_WithdrawTransaction transaction, string stage)
	{
		if (Prefs.DevMode && transaction != null && (transaction.state == SDS_WithdrawEndState.Delivered || transaction.state == SDS_WithdrawEndState.RolledBack))
		{
			Log.Error("[超维存储][不变量] 事务已终结仍被推进：" + stage + " id=" + transaction.id + " state=" + transaction.state);
		}
	}
}
internal static class SDS_JobTargetAdapter
{
	public static void Rewrite(Pawn pawn, Job job)
	{
		if (pawn != null && job != null && ((Thing)pawn).Spawned && ((Thing)pawn).Map != null && job.def != JobDefOf.HaulToCell && job.def != JobDefOf.HaulToContainer && SDS_WithdrawDeliveryPolicy.ModeFor(job) == SDS_WithdrawMode.SpawnedTarget)
		{
			Dictionary<Thing, Thing> replaced = null;
			bool flag = RewriteSlot(pawn, ref job.targetA, TakeCountForSlot(job, (TargetIndex)1), job, ref replaced);
			RewriteSlot(pawn, ref job.targetB, TakeCountForSlot(job, (TargetIndex)2), job, ref replaced);
			RewriteSlot(pawn, ref job.targetC, TakeCountForSlot(job, (TargetIndex)3), job, ref replaced);
			if (flag && job.count > 0 && ((LocalTargetInfo)(ref job.targetA)).HasThing && ((LocalTargetInfo)(ref job.targetA)).Thing != null && ((LocalTargetInfo)(ref job.targetA)).Thing.stackCount < job.count)
			{
				job.count = ((LocalTargetInfo)(ref job.targetA)).Thing.stackCount;
			}
		}
	}

	public static void ReleaseMirrorReservations(Pawn pawn, Job job)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null || job == null || ((Thing)pawn).Map == null)
		{
			return;
		}
		Release(job.targetA);
		Release(job.targetB);
		Release(job.targetC);
		if (job.targetQueueA != null)
		{
			for (int i = 0; i < job.targetQueueA.Count; i++)
			{
				Release(job.targetQueueA[i]);
			}
		}
		if (job.targetQueueB != null)
		{
			for (int j = 0; j < job.targetQueueB.Count; j++)
			{
				Release(job.targetQueueB[j]);
			}
		}
		void Release(LocalTargetInfo target)
		{
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing != null && thing.Spawned && SDS_MirrorRegistry.IsMirror(thing) && ((Thing)pawn).Map.reservationManager.ReservedBy(LocalTargetInfo.op_Implicit(thing), pawn, job))
			{
				((Thing)pawn).Map.reservationManager.Release(LocalTargetInfo.op_Implicit(thing), pawn, job);
			}
		}
	}

	private static int TakeCountForSlot(Job job, TargetIndex ind)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Invalid comparison between Unknown and I4
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Invalid comparison between Unknown and I4
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Invalid comparison between Unknown and I4
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		int result = ((job == null || job.count <= 0) ? 1 : job.count);
		if (job == null)
		{
			return result;
		}
		LocalTargetInfo target;
		if (job.def == JobDefOf.BottleFeedBaby && (int)ind == 2)
		{
			target = job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			Pawn val = (Pawn)(object)((thing is Pawn) ? thing : null);
			target = job.GetTarget((TargetIndex)2);
			Thing thing2 = ((LocalTargetInfo)(ref target)).Thing;
			if (val != null && thing2 != null && val.needs != null && val.needs.food != null)
			{
				float num = FoodUtility.NutritionForEater(val, thing2);
				if (num > 0f)
				{
					return Math.Max(1, FoodUtility.WillIngestStackCountOf(val, thing2.def, num));
				}
			}
			return result;
		}
		if ((job.def == JobDefOf.Refuel || job.def == JobDefOf.RefuelAtomic) && (int)ind == 2)
		{
			target = job.GetTarget((TargetIndex)1);
			Thing thing3 = ((LocalTargetInfo)(ref target)).Thing;
			CompRefuelable val2 = ((thing3 != null) ? ThingCompUtility.TryGetComp<CompRefuelable>(thing3) : null);
			if (val2 != null)
			{
				return Math.Max(1, val2.GetFuelCountToFullyRefuel());
			}
			return result;
		}
		if (job.def == JobDefOf.FillFermentingBarrel && (int)ind == 2)
		{
			target = job.GetTarget((TargetIndex)1);
			Thing thing4 = ((LocalTargetInfo)(ref target)).Thing;
			Building_FermentingBarrel val3 = (Building_FermentingBarrel)(object)((thing4 is Building_FermentingBarrel) ? thing4 : null);
			if (val3 != null)
			{
				return Math.Max(1, val3.SpaceLeftForWort);
			}
			return result;
		}
		return result;
	}

	private static bool RewriteSlot(Pawn pawn, ref LocalTargetInfo target, int count, Job job, ref Dictionary<Thing, Thing> replaced)
	{
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		Thing thing = ((LocalTargetInfo)(ref target)).Thing;
		if (thing == null || !SDS_MirrorRegistry.IsMirror(thing))
		{
			return false;
		}
		if (replaced != null && replaced.TryGetValue(thing, out var value) && value != null && !value.Destroyed)
		{
			target = new LocalTargetInfo(value);
			return true;
		}
		int quantity = Math.Min(Math.Max(1, count), thing.stackCount);
		SDS_WithdrawQuery query = SDS_WithdrawResolver.MakeQuery(SDS_StockKey.FromThing(thing), quantity, preserved: false, ((Thing)pawn).Position);
		SDS_WithdrawPlan sDS_WithdrawPlan = SDS_WithdrawResolver.DecideEndpointPlan(query, pawn, job);
		if (sDS_WithdrawPlan?.endpoint == null)
		{
			return false;
		}
		if (!SDS_WithdrawExecutor.TryTakeMirror(thing, quantity, out var taken, sDS_WithdrawPlan.mode))
		{
			return false;
		}
		int stackCount = taken.stackCount;
		Map map = ((Thing)pawn).Map;
		IntVec3 accessCell = sDS_WithdrawPlan.endpoint.AccessCell;
		Thing val = default(Thing);
		if (!GenPlace.TryPlaceThing(taken, accessCell, map, (ThingPlaceMode)1, ref val, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1) || val == null)
		{
			SDS_TakeService.ReturnToGridOrDrop(taken, map, accessCell);
			return false;
		}
		SDS_ReclaimRegistry.TrackJobTarget(job, val, stackCount);
		if (thing.Spawned && job != null && map.reservationManager.ReservedBy(LocalTargetInfo.op_Implicit(thing), pawn, job))
		{
			map.reservationManager.Release(LocalTargetInfo.op_Implicit(thing), pawn, job);
		}
		if (replaced == null)
		{
			replaced = new Dictionary<Thing, Thing>();
		}
		replaced[thing] = val;
		target = new LocalTargetInfo(val);
		return true;
	}
}
[HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
[HarmonyPriority(200)]
internal static class Patch_SDS_JobMirrorTargets
{
	public static void Prefix(Job newJob, Pawn ___pawn)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Invalid comparison between Unknown and I4
		try
		{
			if (newJob != null && (int)Current.ProgramState == 2 && SDS_MirrorService.AnyMirrorInWorld)
			{
				SDS_JobTargetAdapter.Rewrite(___pawn, newJob);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("Job 镜像目标改写失败（已忽略，原版行为不受影响）", exception);
		}
	}

	public static void Postfix(Job newJob, Pawn ___pawn)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Invalid comparison between Unknown and I4
		try
		{
			if (newJob != null && (int)Current.ProgramState == 2 && SDS_MirrorService.AnyMirrorInWorld)
			{
				SDS_JobTargetAdapter.ReleaseMirrorReservations(___pawn, newJob);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("释放镜像预留失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(Pawn_ApparelTracker), "Wear")]
[HarmonyPriority(200)]
internal static class Patch_SDS_NoWearMirror
{
	public static bool Prefix(Apparel newApparel, bool dropReplacedApparel, bool locked)
	{
		return newApparel == null || !SDS_MirrorRegistry.Any || !SDS_MirrorRegistry.IsMirror((Thing)(object)newApparel);
	}
}
[HarmonyPatch(typeof(Toils_Goto), "GotoThing", new Type[]
{
	typeof(TargetIndex),
	typeof(PathEndMode),
	typeof(bool)
})]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorTargetGoto
{
	public static void Postfix(TargetIndex ind, PathEndMode peMode, ref Toil __result)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (__result == null || __result.initAction == null || !SDS_MirrorRegistry.Any)
		{
			return;
		}
		Toil toil = __result;
		Action original = toil.initAction;
		toil.initAction = delegate
		{
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Pawn actor = toil.actor;
				Job val = ((actor != null && actor.jobs != null) ? actor.jobs.curJob : null);
				object obj;
				if (val == null)
				{
					obj = null;
				}
				else
				{
					LocalTargetInfo target = val.GetTarget(ind);
					obj = ((LocalTargetInfo)(ref target)).Thing;
				}
				Thing val2 = (Thing)obj;
				if (val2 != null && SDS_MirrorRegistry.IsMirror(val2) && SDS_AccessPointResolver.TryGetNearestReachable(actor, out var point))
				{
					actor.pather.StartPath(new LocalTargetInfo(point.cell), peMode);
					return;
				}
			}
			catch (Exception exception)
			{
				SDS_Diag.ErrorOnce("Toils_Goto.GotoThing 镜像寻路改写失败（已回落到原版寻路）", exception);
			}
			original();
		};
	}
}
[HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
[HarmonyPriority(200)]
internal static class Patch_SDS_ReclaimJobTargets
{
	public static void Prefix(Pawn ___pawn, Job ___curJob, JobCondition condition, bool releaseReservations, out int __state)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Invalid comparison between Unknown and I4
		__state = 0;
		SDS_DepositJobCleanup.OnCleanup(___pawn, ___curJob, condition, releaseReservations);
		if (___curJob != null && releaseReservations)
		{
			SDS_ReclaimRegistry.ReclaimHand(___pawn, ___curJob.loadID);
			if ((int)condition == 2)
			{
				SDS_ReclaimRegistry.ForgetJobTarget(___curJob.loadID);
			}
			else
			{
				__state = ___curJob.loadID;
			}
		}
	}

	public static void Postfix(int __state)
	{
		if (__state != 0)
		{
			SDS_ReclaimRegistry.ReclaimJobTarget(__state);
		}
	}
}
[HarmonyPatch(typeof(StoreUtility), "IsInAnyStorage")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorIsInStorage
{
	public static void Postfix(Thing t, ref bool __result)
	{
		if (!__result && SDS_MirrorRegistry.Any && SDS_MirrorRegistry.IsMirror(t))
		{
			__result = true;
		}
	}
}
[HarmonyPatch(typeof(StoreUtility), "IsInValidStorage")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorIsInValidStorage
{
	public static bool Prefix(Thing t, ref bool __result)
	{
		if (t == null || !SDS_MirrorRegistry.Any || !SDS_MirrorRegistry.IsMirror(t))
		{
			return true;
		}
		__result = true;
		return false;
	}
}
[HarmonyPatch(typeof(StoreUtility), "CurrentStoragePriorityOf")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorStoragePriority
{
	public static bool Prefix(Thing t, bool forced, ref StoragePriority __result)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected I4, but got Unknown
		if (t == null || !SDS_MirrorRegistry.Any || !SDS_MirrorRegistry.IsMirror(t))
		{
			return true;
		}
		__result = (StoragePriority)(int)SDS_EndpointPriorityState.Value;
		return false;
	}
}
internal sealed class SDS_MirrorHaulDestination : IHaulDestination, IStoreSettingsParent
{
	private static readonly Dictionary<Map, SDS_MirrorHaulDestination> cache = new Dictionary<Map, SDS_MirrorHaulDestination>();

	private readonly StorageSettings settings;

	public IntVec3 Position => Map.Center;

	public Map Map { get; }

	public bool HaulDestinationEnabled => false;

	public bool StorageTabVisible => false;

	internal static void Reset()
	{
		cache.Clear();
	}

	private SDS_MirrorHaulDestination(Map map)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		Map = map;
		settings = new StorageSettings((IStoreSettingsParent)(object)this);
		settings.Priority = SDS_EndpointPriorityState.Value;
	}

	public static SDS_MirrorHaulDestination For(Map map)
	{
		if (map == null)
		{
			return null;
		}
		if (cache.TryGetValue(map, out var value) && value != null)
		{
			return value;
		}
		if (cache.Count > 32)
		{
			cache.Clear();
		}
		value = new SDS_MirrorHaulDestination(map);
		cache[map] = value;
		return value;
	}

	public bool Accepts(Thing t)
	{
		return t != null && !t.Destroyed;
	}

	public StorageSettings GetStoreSettings()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		StoragePriority value = SDS_EndpointPriorityState.Value;
		if (settings.Priority != value)
		{
			settings.Priority = value;
		}
		return settings;
	}

	public StorageSettings GetParentStoreSettings()
	{
		return null;
	}

	public void Notify_SettingsChanged()
	{
	}
}
[HarmonyPatch(typeof(StoreUtility), "CurrentHaulDestinationOf")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorHaulDestination
{
	public static void Postfix(Thing t, ref IHaulDestination __result)
	{
		try
		{
			if (t != null && SDS_MirrorRegistry.Any && SDS_MirrorRegistry.IsMirror(t))
			{
				SDS_MirrorHaulDestination sDS_MirrorHaulDestination = SDS_MirrorHaulDestination.For(t.MapHeld);
				if (sDS_MirrorHaulDestination != null)
				{
					__result = (IHaulDestination)(object)sDS_MirrorHaulDestination;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("镜像仓库代理解析失败（已忽略，保留原版目的地）", exception);
		}
	}
}
[HarmonyPatch(typeof(TradeUtility), "PlayerSellableNow")]
internal static class Patch_SDS_MirrorNotSellable
{
	public static void Postfix(Thing t, ref bool __result)
	{
		if (__result && SDS_MirrorRegistry.Any && SDS_MirrorRegistry.IsMirror(t))
		{
			__result = false;
		}
	}
}
[HarmonyPatch(typeof(TradeUtility), "AllLaunchableThingsForTrade")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNotLaunchable
{
	public static void Postfix(ref IEnumerable<Thing> __result)
	{
		if (__result != null && SDS_MirrorRegistry.Any)
		{
			__result = WithoutMirrors(__result);
		}
	}

	private static IEnumerable<Thing> WithoutMirrors(IEnumerable<Thing> source)
	{
		foreach (Thing thing in source)
		{
			if (!SDS_MirrorRegistry.IsMirror(thing))
			{
				yield return thing;
			}
		}
	}
}
[HarmonyPatch(typeof(Toils_Haul), "StartCarryThing", new Type[]
{
	typeof(TargetIndex),
	typeof(bool),
	typeof(bool),
	typeof(bool),
	typeof(bool),
	typeof(bool)
})]
[HarmonyPriority(200)]
internal static class Patch_SDS_StartCarryAlreadyCarried
{
	public static void Postfix(TargetIndex haulableInd, ref Toil __result)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		if (__result == null || __result.initAction == null)
		{
			return;
		}
		Toil toil = __result;
		Action original = toil.initAction;
		toil.initAction = delegate
		{
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			Pawn actor = toil.actor;
			Job val = ((actor != null && actor.jobs != null) ? actor.jobs.curJob : null);
			object obj;
			if (val == null)
			{
				obj = null;
			}
			else
			{
				LocalTargetInfo target = val.GetTarget(haulableInd);
				obj = ((LocalTargetInfo)(ref target)).Thing;
			}
			Thing val2 = (Thing)obj;
			if (val2 == null || actor.carryTracker == null || actor.carryTracker.CarriedThing != val2)
			{
				original();
			}
		};
	}
}
[HarmonyPatch(typeof(CaravanFormingUtility), "AllReachableColonyItems")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNotCaravanItem
{
	public static void Postfix(List<Thing> __result)
	{
		if (__result == null || !SDS_MirrorRegistry.Any)
		{
			return;
		}
		for (int num = __result.Count - 1; num >= 0; num--)
		{
			if (SDS_MirrorRegistry.IsMirror(__result[num]))
			{
				__result.RemoveAt(num);
			}
		}
	}
}
public static class SDS_MirrorRegistry
{
	private static readonly HashSet<Thing> mirrors = new HashSet<Thing>();

	public static bool AllowDeSpawn;

	public static bool Any => mirrors.Count > 0;

	public static bool IsMirror(Thing thing)
	{
		return thing != null && mirrors.Contains(thing);
	}

	public static void Mark(Thing thing)
	{
		if (thing != null)
		{
			mirrors.Add(thing);
		}
	}

	public static void Unmark(Thing thing)
	{
		if (thing != null)
		{
			mirrors.Remove(thing);
		}
	}

	public static void CollectOn(Map map, List<Thing> outList)
	{
		if (map == null || outList == null)
		{
			return;
		}
		foreach (Thing mirror in mirrors)
		{
			if (mirror != null && !mirror.Destroyed && mirror.Spawned && mirror.Map == map)
			{
				outList.Add(mirror);
			}
		}
	}

	public static void Clear()
	{
		mirrors.Clear();
	}
}
public static class SDS_MirrorService
{
	private static readonly Dictionary<Map, Dictionary<SDS_StockKey, Thing>> mirrors = new Dictionary<Map, Dictionary<SDS_StockKey, Thing>>();

	private static readonly List<Map> tmpMaps = new List<Map>();

	private static readonly List<SDS_StockKey> tmpKeys = new List<SDS_StockKey>();

	private static readonly HashSet<SDS_StockKey> tmpKeySet = new HashSet<SDS_StockKey>();

	private static readonly HashSet<SDS_StockKey> dirtyKeys = new HashSet<SDS_StockKey>();

	private static bool fullSync = true;

	private static bool dirty = true;

	private const int RetryIntervalTicks = 250;

	private static int retryAtTick = -1;

	public static int Version { get; private set; }

	public static bool AnyMirrorInWorld
	{
		get
		{
			foreach (KeyValuePair<Map, Dictionary<SDS_StockKey, Thing>> mirror in mirrors)
			{
				if (mirror.Value != null && mirror.Value.Count > 0)
				{
					return true;
				}
			}
			return false;
		}
	}

	internal static void MarkChanged()
	{
		Version++;
	}

	public static void Reset()
	{
		mirrors.Clear();
		tmpMaps.Clear();
		tmpKeys.Clear();
		dirtyKeys.Clear();
		fullSync = true;
		SDS_MirrorRegistry.Clear();
		SDS_MirrorRegistry.AllowDeSpawn = false;
		SDS_AccessPointResolver.Invalidate();
		Building_SDS_Vault.ResetStatics();
		retryAtTick = -1;
		MarkChanged();
		dirty = true;
	}

	public static void NotifyStockChanged()
	{
		fullSync = true;
		dirtyKeys.Clear();
		retryAtTick = -1;
		dirty = true;
	}

	public static void NotifyStockChanged(SDS_StockKey key)
	{
		if (key == null || fullSync)
		{
			dirty = true;
			return;
		}
		dirtyKeys.Add(key);
		dirty = true;
	}

	public static void NotifyStructureChanged()
	{
		SDS_AccessPointResolver.Invalidate();
		fullSync = true;
		dirtyKeys.Clear();
		retryAtTick = -1;
		dirty = true;
	}

	public static void CollectMirrors(List<Thing> outList)
	{
		if (outList == null)
		{
			return;
		}
		outList.Clear();
		foreach (Dictionary<SDS_StockKey, Thing> value in mirrors.Values)
		{
			if (value == null)
			{
				continue;
			}
			foreach (Thing value2 in value.Values)
			{
				if (value2 == null || value2.Destroyed)
				{
					continue;
				}
				int num = ((value2.def == null) ? 1 : Math.Max(1, value2.def.stackLimit));
				if (value2.stackCount > num)
				{
					value2.stackCount = num;
					if (value2.Map != null)
					{
						value2.DirtyMapMesh(value2.Map);
					}
					dirty = true;
					MarkChanged();
				}
				outList.Add(value2);
			}
		}
	}

	public static void CleanupLoadedMirrors(List<Thing> loaded)
	{
		if (loaded != null)
		{
			for (int i = 0; i < loaded.Count; i++)
			{
				Thing val = loaded[i];
				if (val != null && !val.Destroyed)
				{
					SDS_MirrorRegistry.Unmark(val);
					val.Destroy((DestroyMode)0);
				}
			}
		}
		dirty = true;
		MarkChanged();
	}

	internal static void ForgetMirror(Thing mirror)
	{
		if (mirror == null)
		{
			return;
		}
		Map map = mirror.Map;
		if (map == null || !mirrors.TryGetValue(map, out var value) || value == null)
		{
			return;
		}
		SDS_StockKey sDS_StockKey = null;
		foreach (KeyValuePair<SDS_StockKey, Thing> item in value)
		{
			if (item.Value == mirror)
			{
				sDS_StockKey = item.Key;
				break;
			}
		}
		if (sDS_StockKey != null)
		{
			value.Remove(sDS_StockKey);
			MarkChanged();
		}
	}

	public static bool HasAnyMirrorOn(Map map)
	{
		Dictionary<SDS_StockKey, Thing> value;
		return map != null && mirrors.TryGetValue(map, out value) && value != null && value.Count > 0;
	}

	public static void CopyMirrorsOn(Map map, List<Thing> outList)
	{
		if (outList == null)
		{
			return;
		}
		outList.Clear();
		if (map == null || !mirrors.TryGetValue(map, out var value) || value == null)
		{
			return;
		}
		foreach (Thing value2 in value.Values)
		{
			if (value2 != null && !value2.Destroyed && value2.Spawned && value2.Map == map)
			{
				outList.Add(value2);
			}
		}
	}

	public static void CopyMirrorKeys(Map map, List<SDS_StockKey> outList)
	{
		if (outList == null)
		{
			return;
		}
		outList.Clear();
		if (map == null || !mirrors.TryGetValue(map, out var value) || value == null)
		{
			return;
		}
		foreach (KeyValuePair<SDS_StockKey, Thing> item in value)
		{
			Thing value2 = item.Value;
			if (item.Key != null && value2 != null && !value2.Destroyed && value2.Spawned && value2.Map == map)
			{
				outList.Add(item.Key);
			}
		}
	}

	public static void CopyMirrorsForDef(Map map, ThingDef def, List<Thing> outList)
	{
		if (outList == null)
		{
			return;
		}
		outList.Clear();
		if (map == null || def == null || !mirrors.TryGetValue(map, out var value) || value == null)
		{
			return;
		}
		foreach (KeyValuePair<SDS_StockKey, Thing> item in value)
		{
			Thing value2 = item.Value;
			if (value2 != null && !value2.Destroyed && value2.Spawned && value2.Map == map && item.Key != null && item.Key.thingDef == def)
			{
				outList.Add(value2);
			}
		}
	}

	public static bool TryGetMirror(Map map, SDS_StockKey key, out Thing mirror)
	{
		mirror = null;
		if (map == null || key == null || !mirrors.TryGetValue(map, out var value) || value == null)
		{
			return false;
		}
		if (!value.TryGetValue(key, out mirror) || mirror == null || mirror.Destroyed || !mirror.Spawned || mirror.Map != map)
		{
			mirror = null;
			return false;
		}
		return true;
	}

	public static long MirrorExposureOnResourceCounter(Map map, SDS_StockKey key)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || key == null || !TryGetMirror(map, key, out var mirror))
		{
			return 0L;
		}
		if (mirror.def == null || !mirror.def.EverStorable(false) || RottableUtility.IsNotFresh(mirror) || GridsUtility.Fogged(mirror.PositionHeld, map))
		{
			return 0L;
		}
		HaulDestinationManager haulDestinationManager = map.haulDestinationManager;
		if (haulDestinationManager == null)
		{
			return 0L;
		}
		IntVec3 positionHeld = mirror.PositionHeld;
		List<SlotGroup> allGroupsListForReading = haulDestinationManager.AllGroupsListForReading;
		int num = 0;
		for (int i = 0; i < allGroupsListForReading.Count; i++)
		{
			SlotGroup val = allGroupsListForReading[i];
			if (val != null && val.CellsList.Contains(positionHeld))
			{
				num++;
			}
		}
		return (num <= 0) ? 0 : ((long)num * (long)mirror.stackCount);
	}

	public static void NotifyVaultRelocated(Map map)
	{
		if (map != null)
		{
			RemoveMap(map);
			MarkChanged();
		}
	}

	public static float MirrorWealthOn(Map map)
	{
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || !mirrors.TryGetValue(map, out var value) || value == null)
		{
			return 0f;
		}
		float num = 0f;
		foreach (Thing value2 in value.Values)
		{
			if (value2 != null && !value2.Destroyed && value2.Spawned && value2.Map == map && value2.def != null && value2.def.EverHaulable && !GridsUtility.Fogged(value2.PositionHeld, map))
			{
				num += value2.MarketValue * (float)value2.stackCount;
			}
		}
		return num;
	}

	private static void ScheduleRetry()
	{
		int num = ((Find.TickManager != null) ? Find.TickManager.TicksGame : 0);
		int num2 = num + 250;
		if (retryAtTick < 0 || num2 < retryAtTick)
		{
			retryAtTick = num2;
		}
	}

	public static void SyncIfDirty()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		if ((int)Current.ProgramState != 2)
		{
			return;
		}
		if (!dirty)
		{
			if (retryAtTick < 0 || Find.TickManager == null || Find.TickManager.TicksGame < retryAtTick)
			{
				return;
			}
			retryAtTick = -1;
			fullSync = true;
		}
		dirty = false;
		try
		{
			if (fullSync)
			{
				fullSync = false;
				dirtyKeys.Clear();
				SyncAll();
			}
			else
			{
				SyncDirtyKeys();
			}
		}
		catch (Exception exception)
		{
			fullSync = true;
			dirty = true;
			SDS_Diag.ErrorOnce("物理镜像同步失败（已忽略，账本不受影响）", exception);
		}
	}

	public unsafe static void SyncAll()
	{
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_036f: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		List<Map> maps = Find.Maps;
		if (instance == null || maps == null)
		{
			return;
		}
		tmpMaps.Clear();
		foreach (KeyValuePair<Map, Dictionary<SDS_StockKey, Thing>> mirror in mirrors)
		{
			if (mirror.Key == null || !maps.Contains(mirror.Key))
			{
				tmpMaps.Add(mirror.Key);
			}
		}
		for (int i = 0; i < tmpMaps.Count; i++)
		{
			RemoveMap(tmpMaps[i]);
		}
		if (!instance.Available)
		{
			RemoveAll();
			return;
		}
		List<SDS_StockEntry> list = instance.Snapshot(0);
		for (int j = 0; j < maps.Count; j++)
		{
			Map val = maps[j];
			if (val == null || val.Disposed || val.listerThings == null)
			{
				continue;
			}
			if (!SDS_EndpointContract.TryGetExposureAnchor(val, out var endpoint, out var portPawn))
			{
				RemoveMap(val);
				continue;
			}
			IntVec3 preferredCell = ((endpoint != null) ? MirrorCellFor(val, endpoint) : MirrorCellFor(val, ((Thing)portPawn).Position));
			if (!((IntVec3)(ref preferredCell)).IsValid)
			{
				preferredCell = val.Center;
			}
			IntVec3 cell;
			Building_SDS_Vault building_SDS_Vault = Building_SDS_Vault.EnsureOn(val, preferredCell, out cell);
			if (building_SDS_Vault == null || !((IntVec3)(ref cell)).IsValid)
			{
				SDS_Diag.Fail("镜像/金库落点不可用", SDS_Diag.Ctx(val, null, null, null, "cell=" + (((IntVec3)(ref cell)).IsValid ? ((object)(*(IntVec3*)(&cell))/*cast due to constrained. prefix*/).ToString() : "无效")));
				RemoveMap(val);
				ScheduleRetry();
				continue;
			}
			Dictionary<SDS_StockKey, Thing> orCreateMap = GetOrCreateMap(val);
			tmpKeys.Clear();
			tmpKeySet.Clear();
			for (int k = 0; k < list.Count; k++)
			{
				SDS_StockEntry sDS_StockEntry = list[k];
				if (sDS_StockEntry != null && sDS_StockEntry.key != null && sDS_StockEntry.key.thingDef != null && !sDS_StockEntry.key.preserved && sDS_StockEntry.count > 0)
				{
					tmpKeys.Add(sDS_StockEntry.key);
					tmpKeySet.Add(sDS_StockEntry.key);
				}
			}
			List<SDS_StockKey> list2 = null;
			foreach (KeyValuePair<SDS_StockKey, Thing> item in orCreateMap)
			{
				if (!tmpKeySet.Contains(item.Key))
				{
					(list2 ?? (list2 = new List<SDS_StockKey>())).Add(item.Key);
				}
			}
			if (list2 != null)
			{
				for (int l = 0; l < list2.Count; l++)
				{
					RemoveMirror(orCreateMap, list2[l]);
				}
			}
			for (int m = 0; m < tmpKeys.Count; m++)
			{
				SDS_StockKey key = tmpKeys[m];
				EnsureMirrorFor(val, orCreateMap, key, instance.GetCount(key), cell, requireVault: false);
			}
		}
	}

	private static bool EnsureMirrorFor(Map map, Dictionary<SDS_StockKey, Thing> dict, SDS_StockKey key, long countLong, IntVec3 cell, bool requireVault)
	{
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (key != null && instance != null && instance.IsForbidden(key))
		{
			RemoveMirror(dict, key);
			return true;
		}
		if (countLong <= 0)
		{
			RemoveMirror(dict, key);
			return true;
		}
		int count = (int)Math.Min(countLong, 2147483647L);
		if (dict.TryGetValue(key, out var value) && value != null && !value.Destroyed && value.Spawned && value.Map == map)
		{
			UpdateMirror(value, key, count, map);
			return true;
		}
		dict.Remove(key);
		RemoveMirror(value);
		if (!((IntVec3)(ref cell)).IsValid && requireVault)
		{
			Building_SDS_Vault building_SDS_Vault = Building_SDS_Vault.GetOn(map);
			cell = ((building_SDS_Vault != null) ? ((Thing)building_SDS_Vault).Position : IntVec3.Invalid);
		}
		if (!((IntVec3)(ref cell)).IsValid)
		{
			return false;
		}
		Thing val = SpawnMirror(map, key, count, cell);
		if (val != null)
		{
			dict[key] = val;
		}
		return true;
	}

	private static void SyncDirtyKeys()
	{
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		List<Map> maps = Find.Maps;
		if (instance == null || maps == null)
		{
			fullSync = true;
			dirty = true;
			return;
		}
		if (!instance.Available)
		{
			RemoveAll();
			fullSync = true;
			return;
		}
		foreach (SDS_StockKey dirtyKey in dirtyKeys)
		{
			if (dirtyKey == null || dirtyKey.preserved || !dirtyKey.Resolved)
			{
				continue;
			}
			long count = instance.GetCount(dirtyKey);
			for (int i = 0; i < maps.Count; i++)
			{
				Map val = maps[i];
				if (val != null && !val.Disposed && val.listerThings != null && mirrors.TryGetValue(val, out var value) && value != null && !EnsureMirrorFor(val, value, dirtyKey, count, IntVec3.Invalid, requireVault: true))
				{
					SDS_Diag.Fail("镜像/金库缺失", SDS_Diag.Ctx(val, null, null, null, "def=" + ((dirtyKey.thingDef != null) ? ((Def)dirtyKey.thingDef).defName : "?")));
					ScheduleRetry();
				}
			}
		}
		dirtyKeys.Clear();
	}

	public static void OnMirrorSpawned(Thing thing)
	{
		if (thing != null && SDS_MirrorRegistry.IsMirror(thing))
		{
			if (Find.TickManager != null)
			{
				Find.TickManager.DeRegisterAllTickabilityFor(thing);
			}
			CompRottable val = ThingCompUtility.TryGetComp<CompRottable>(thing);
			if (val != null)
			{
				val.disabled = true;
			}
		}
	}

	private static Dictionary<SDS_StockKey, Thing> GetOrCreateMap(Map map)
	{
		if (!mirrors.TryGetValue(map, out var value) || value == null)
		{
			value = new Dictionary<SDS_StockKey, Thing>();
			mirrors[map] = value;
		}
		return value;
	}

	private unsafe static Thing SpawnMirror(Map map, SDS_StockKey key, int count, IntVec3 cell)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		if (GridsUtility.GetMaxItemsAllowedInCell(cell, map) <= GridsUtility.GetItemCount(cell, map))
		{
			string[] obj = new string[8] { "cell=", null, null, null, null, null, null, null };
			IntVec3 val = cell;
			obj[1] = ((object)(*(IntVec3*)(&val))/*cast due to constrained. prefix*/).ToString();
			obj[2] = " max=";
			obj[3] = GridsUtility.GetMaxItemsAllowedInCell(cell, map).ToString();
			obj[4] = " items=";
			obj[5] = GridsUtility.GetItemCount(cell, map).ToString();
			obj[6] = " def=";
			obj[7] = ((key.thingDef != null) ? ((Def)key.thingDef).defName : "?");
			SDS_Diag.Fail("镜像/落点已满", SDS_Diag.Ctx(map, null, null, null, string.Concat(obj)));
			ScheduleRetry();
			return null;
		}
		Thing val2 = SDS_ProxyFactory.CreateMirrorThing(key);
		if (val2 == null)
		{
			SDS_Diag.Fail("镜像/品类不可重建", SDS_Diag.Ctx(map, null, null, null, "def=" + ((key.thingDef != null) ? ((Def)key.thingDef).defName : "?")));
			return null;
		}
		int num = ((val2.def != null) ? val2.def.stackLimit : 0);
		if (num <= 0)
		{
			SDS_Diag.Fail("镜像/堆上限非法", SDS_Diag.Ctx(map, null, val2, null, "stackLimit=" + num));
			SDS_MirrorRegistry.Unmark(val2);
			val2.Destroy((DestroyMode)0);
			return null;
		}
		val2.stackCount = Math.Min(count, num);
		SDS_MirrorRegistry.Mark(val2);
		try
		{
			GenSpawn.Spawn(val2, cell, map, (WipeMode)0);
			if (val2.Destroyed || !val2.Spawned)
			{
				SDS_MirrorRegistry.Unmark(val2);
				return null;
			}
			if (val2.stackCount != count)
			{
				val2.stackCount = count;
				val2.DirtyMapMesh(map);
				MarkChanged();
			}
			return val2;
		}
		catch (Exception exception)
		{
			SDS_MirrorRegistry.Unmark(val2);
			if (!val2.Destroyed)
			{
				val2.Destroy((DestroyMode)0);
			}
			SDS_Diag.ErrorOnce("物理镜像 spawn 失败（已忽略，账本不受影响）", exception);
			return null;
		}
	}

	private static void UpdateMirror(Thing thing, SDS_StockKey key, int count, Map map)
	{
		if (thing == null || thing.Destroyed)
		{
			return;
		}
		SDS_MirrorRegistry.Mark(thing);
		SDS_GameComponent_Grid.Instance?.ApplyStoredVitals(key, thing);
		if (thing.stackCount != count)
		{
			thing.stackCount = count;
			if (thing.def != null)
			{
				thing.DirtyMapMesh(map);
			}
			MarkChanged();
		}
	}

	private static void RemoveMirror(Dictionary<SDS_StockKey, Thing> dict, SDS_StockKey key)
	{
		if (dict != null && key != null && dict.TryGetValue(key, out var value))
		{
			dict.Remove(key);
			RemoveMirror(value);
		}
	}

	private static void RemoveMirror(Thing thing)
	{
		if (thing == null)
		{
			return;
		}
		SDS_MirrorRegistry.Unmark(thing);
		if (!thing.Destroyed)
		{
			if (thing.Spawned)
			{
				thing.DeSpawnOrDeselect((DestroyMode)0);
			}
			else
			{
				thing.Destroy((DestroyMode)0);
			}
			MarkChanged();
		}
	}

	private static void RemoveMap(Map map)
	{
		if (map == null || !mirrors.TryGetValue(map, out var value) || value == null)
		{
			return;
		}
		mirrors.Remove(map);
		foreach (Thing value2 in value.Values)
		{
			RemoveMirror(value2);
		}
		value.Clear();
	}

	private static void RemoveAll()
	{
		tmpMaps.Clear();
		foreach (Map key in mirrors.Keys)
		{
			tmpMaps.Add(key);
		}
		for (int i = 0; i < tmpMaps.Count; i++)
		{
			RemoveMap(tmpMaps[i]);
		}
	}

	public static void EvacuateForStructureChange(Map map, string reason)
	{
		if (map == null || map.Disposed)
		{
			return;
		}
		List<Thing> list = new List<Thing>();
		SDS_MirrorRegistry.CollectOn(map, list);
		mirrors.TryGetValue(map, out var value);
		if (value != null)
		{
			foreach (Thing value2 in value.Values)
			{
				if (value2 != null && !list.Contains(value2))
				{
					list.Add(value2);
				}
			}
		}
		mirrors.Remove(map);
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if (val != null && !val.Destroyed)
			{
				try
				{
					SDS_MirrorRegistry.Unmark(val);
					val.Destroy((DestroyMode)0);
					num++;
				}
				catch (Exception exception)
				{
					SDS_Diag.Fail("镜像/撤离失败", SDS_Diag.Ctx(map, null, val, null, "reason=" + reason), exception);
				}
			}
		}
		value?.Clear();
		if (num > 0)
		{
			SDS_Diag.Warn("镜像/结构撤离", SDS_Diag.Ctx(map, null, null, null, "reason=" + reason + " count=" + num));
		}
		fullSync = true;
		dirtyKeys.Clear();
		retryAtTick = -1;
		dirty = true;
		MarkChanged();
	}

	private static IntVec3 MirrorCellFor(Map map, ISDS_StorageEndpoint endpoint)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		Thing val = (Thing)((endpoint is Thing) ? endpoint : null);
		IntVec3 baseCell = ((val != null) ? val.Position : map.Center);
		Building val2 = (Building)(object)((val is Building) ? val : null);
		if (val2 != null && ((Thing)val2).def != null && ((Thing)val2).def.hasInteractionCell)
		{
			baseCell = ((Thing)val2).InteractionCell;
		}
		IntVec3 val3 = FindUsableCellNear(map, baseCell);
		return ((IntVec3)(ref val3)).IsValid ? val3 : map.Center;
	}

	private static IntVec3 MirrorCellFor(Map map, IntVec3 baseCell)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		return FindUsableCellNear(map, baseCell);
	}

	private static IntVec3 FindUsableCellNear(Map map, IntVec3 baseCell)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		if (IsUsableCell(baseCell, map))
		{
			return baseCell;
		}
		foreach (IntVec3 item in GenRadial.RadialCellsAround(baseCell, 6f, true))
		{
			if (IsUsableCell(item, map))
			{
				return item;
			}
		}
		return IntVec3.Invalid;
	}

	private static bool IsUsableCell(IntVec3 cell, Map map)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		return ((IntVec3)(ref cell)).IsValid && GenGrid.InBounds(cell, map) && GenGrid.Standable(cell, map) && !GridsUtility.Fogged(cell, map) && GridsUtility.GetFirstBuilding(cell, map) == null && GridsUtility.GetZone(cell, map) == null && (map.haulDestinationManager == null || map.haulDestinationManager.SlotGroupAt(cell) == null);
	}
}
public sealed class SDS_ProjectionOwner : ThingOwner<Thing>, IEnumerable<Thing>, IEnumerable
{
	private static readonly List<Thing> tmpMirrors = new List<Thing>();

	private readonly SDS_ProjectionHolder holder;

	private int builtVersion = -1;

	private Map builtMap;

	private int viewOpDepth;

	public override int Count
	{
		get
		{
			EnsureFresh();
			return base.Count;
		}
	}

	public SDS_ProjectionOwner(SDS_ProjectionHolder holder)
		: base((IThingHolder)(object)holder)
	{
		this.holder = holder;
		((ThingOwner)this).dontTickContents = true;
		((ThingOwner)this).removeContentsIfDestroyed = false;
	}

	public List<Thing>.Enumerator GetEnumerator()
	{
		EnsureFresh();
		return base.InnerListForReading.GetEnumerator();
	}

	IEnumerator<Thing> IEnumerable<Thing>.GetEnumerator()
	{
		EnsureFresh();
		return base.InnerListForReading.GetEnumerator();
	}

	public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true)
	{
		return 0;
	}

	public override int TryAdd(Thing item, int count, bool canMergeWithExistingStacks = true)
	{
		return 0;
	}

	public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
	{
		return false;
	}

	public override int IndexOf(Thing item)
	{
		EnsureFresh();
		return base.IndexOf(item);
	}

	public override bool Remove(Thing item)
	{
		EnsureFresh();
		List<Thing> innerListForReading = base.InnerListForReading;
		int num = innerListForReading.LastIndexOf(item);
		if (num < 0)
		{
			return false;
		}
		if (viewOpDepth > 0)
		{
			innerListForReading.RemoveAt(num);
			return true;
		}
		int num2 = ((item == null || item.def == null) ? 1 : Math.Max(1, item.def.stackLimit));
		if (item == null || item.stackCount > num2)
		{
			return false;
		}
		if (item.stackCount > 0)
		{
			SDS_TakeService.TryTake(item, item.stackCount, out var _);
		}
		num = innerListForReading.LastIndexOf(item);
		if (num >= 0)
		{
			innerListForReading.RemoveAt(num);
		}
		return true;
	}

	public bool ContainsReference(Thing thing)
	{
		EnsureFresh();
		return thing != null && base.InnerListForReading.Contains(thing);
	}

	public Thing TakeFromProjection(Thing thing, int count)
	{
		Thing taken;
		return TryTakeForTransfer(thing, count, out taken) ? taken : null;
	}

	public bool TryTakeForTransfer(Thing thing, int count, out Thing taken)
	{
		taken = null;
		if (thing == null || count <= 0)
		{
			return false;
		}
		EnsureFresh();
		List<Thing> innerListForReading = base.InnerListForReading;
		if (!innerListForReading.Contains(thing))
		{
			return false;
		}
		int num = Math.Min(count, thing.stackCount);
		if (num <= 0 || !SDS_TakeService.TryTake(thing, num, out taken))
		{
			return false;
		}
		if (taken == thing || thing.Destroyed || thing.stackCount <= 0)
		{
			innerListForReading.Remove(thing);
		}
		return true;
	}

	public void BeginViewOp()
	{
		viewOpDepth++;
	}

	public void EndViewOp()
	{
		if (viewOpDepth > 0)
		{
			viewOpDepth--;
		}
		if (viewOpDepth == 0)
		{
			builtVersion = -1;
		}
	}

	public override void ExposeData()
	{
	}

	private void EnsureFresh()
	{
		Map map = holder.Map;
		int version = SDS_MirrorService.Version;
		if (builtVersion == version && builtMap == map)
		{
			return;
		}
		builtVersion = version;
		builtMap = map;
		List<Thing> innerListForReading = base.InnerListForReading;
		innerListForReading.Clear();
		if (map == null)
		{
			return;
		}
		SDS_MirrorService.CopyMirrorsOn(map, tmpMirrors);
		for (int i = 0; i < tmpMirrors.Count; i++)
		{
			Thing val = tmpMirrors[i];
			if (val != null)
			{
				innerListForReading.Add(val);
			}
		}
	}
}
public sealed class SDS_ProjectionHolder : IThingHolder
{
	private readonly Pawn pawn;

	private readonly Thing endpoint;

	private readonly SDS_ProjectionOwner contents;

	public Map Map => (pawn != null) ? ((Thing)pawn).Map : ((endpoint != null) ? endpoint.Map : null);

	public IThingHolder ParentHolder
	{
		get
		{
			if (pawn != null)
			{
				return (IThingHolder)(object)pawn;
			}
			Thing obj = endpoint;
			return (IThingHolder)(object)((obj is IThingHolder) ? obj : null);
		}
	}

	public bool Alive => (pawn == null) ? (endpoint != null && !endpoint.Destroyed && endpoint.Spawned) : (!((Thing)pawn).Destroyed && ((Thing)pawn).Spawned);

	public int CachedCount => ((ThingOwner)contents).Count;

	public SDS_ProjectionHolder(Pawn pawn)
	{
		this.pawn = pawn;
		contents = new SDS_ProjectionOwner(this);
	}

	public SDS_ProjectionHolder(Thing endpoint)
	{
		this.endpoint = endpoint;
		contents = new SDS_ProjectionOwner(this);
	}

	public ThingOwner GetDirectlyHeldThings()
	{
		return (ThingOwner)(object)contents;
	}

	public void GetChildHolders(List<IThingHolder> outChildren)
	{
	}
}
public sealed class SDS_MapLevelHolder : IThingHolder
{
	public readonly Pawn pawn;

	public IThingHolder ParentHolder => (IThingHolder)(object)pawn;

	public bool PassesWealthFilter => ((Thing)pawn).Faction == Faction.OfPlayer && !QuestUtility.IsQuestLodger(pawn);

	public SDS_MapLevelHolder(Pawn pawn)
	{
		this.pawn = pawn;
	}

	public ThingOwner GetDirectlyHeldThings()
	{
		return pawn.GetDirectlyHeldThings();
	}

	public void GetChildHolders(List<IThingHolder> outChildren)
	{
		int count = outChildren.Count;
		pawn.GetChildHolders(outChildren);
		for (int num = outChildren.Count - 1; num >= count; num--)
		{
			if (outChildren[num] is SDS_ProjectionHolder)
			{
				outChildren.RemoveAt(num);
			}
		}
	}
}
public static class SDS_ProjectionService
{
	private const int PruneThreshold = 256;

	private static readonly Dictionary<Pawn, SDS_ProjectionHolder> pawnProjections = new Dictionary<Pawn, SDS_ProjectionHolder>();

	private static readonly Dictionary<Thing, SDS_ProjectionHolder> endpointProjections = new Dictionary<Thing, SDS_ProjectionHolder>();

	private static readonly Dictionary<Pawn, SDS_MapLevelHolder> mapLevelHolders = new Dictionary<Pawn, SDS_MapLevelHolder>();

	public static void Reset()
	{
		pawnProjections.Clear();
		endpointProjections.Clear();
		mapLevelHolders.Clear();
	}

	public static SDS_ProjectionHolder ProjectionFor(Pawn pawn)
	{
		if (pawn == null)
		{
			return null;
		}
		PruneIfNeeded();
		if (!pawnProjections.TryGetValue(pawn, out var value) || value == null)
		{
			value = new SDS_ProjectionHolder(pawn);
			pawnProjections[pawn] = value;
		}
		return value;
	}

	public static SDS_ProjectionHolder ProjectionFor(Thing endpoint)
	{
		if (endpoint == null)
		{
			return null;
		}
		PruneIfNeeded();
		if (!endpointProjections.TryGetValue(endpoint, out var value) || value == null)
		{
			value = new SDS_ProjectionHolder(endpoint);
			endpointProjections[endpoint] = value;
		}
		return value;
	}

	public static SDS_MapLevelHolder MapLevelHolderFor(Pawn pawn)
	{
		if (pawn == null)
		{
			return null;
		}
		PruneIfNeeded();
		if (!mapLevelHolders.TryGetValue(pawn, out var value) || value == null)
		{
			value = new SDS_MapLevelHolder(pawn);
			mapLevelHolders[pawn] = value;
		}
		return value;
	}

	private static void PruneIfNeeded()
	{
		if (pawnProjections.Count + mapLevelHolders.Count > 256)
		{
			PrunePawnDict(pawnProjections);
			PrunePawnDict(mapLevelHolders);
		}
		if (endpointProjections.Count <= 256)
		{
			return;
		}
		List<Thing> list = null;
		foreach (KeyValuePair<Thing, SDS_ProjectionHolder> endpointProjection in endpointProjections)
		{
			if (endpointProjection.Key == null || endpointProjection.Key.Destroyed)
			{
				(list ?? (list = new List<Thing>())).Add(endpointProjection.Key);
			}
		}
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				endpointProjections.Remove(list[i]);
			}
		}
	}

	private static void PrunePawnDict<T>(Dictionary<Pawn, T> dict)
	{
		List<Pawn> list = null;
		foreach (KeyValuePair<Pawn, T> item in dict)
		{
			if (item.Key == null || ((Thing)item.Key).Destroyed)
			{
				(list ?? (list = new List<Pawn>())).Add(item.Key);
			}
		}
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				dict.Remove(list[i]);
			}
		}
	}
}
[HarmonyPatch(typeof(Pawn), "GetChildHolders")]
[HarmonyPriority(200)]
internal static class Patch_SDS_PawnProjectionExposure
{
	public static void Postfix(Pawn __instance, List<IThingHolder> outChildren)
	{
		try
		{
			if (__instance != null && outChildren != null && ((Thing)__instance).Spawned && SDS_GameComponent_Grid.Instance != null && SDS_ImplantUtility.IsPortOperational(__instance))
			{
				SDS_ProjectionHolder sDS_ProjectionHolder = SDS_ProjectionService.ProjectionFor(__instance);
				if (sDS_ProjectionHolder != null)
				{
					outChildren.Add((IThingHolder)(object)sDS_ProjectionHolder);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("小人投影挂载失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(WealthWatcher), "WealthItemsFilter")]
[HarmonyPriority(200)]
internal static class Patch_SDS_WealthItemsFilterProjection
{
	public static bool Prefix(IThingHolder x, ref bool __result)
	{
		if (x is SDS_MapLevelHolder sDS_MapLevelHolder)
		{
			__result = sDS_MapLevelHolder.PassesWealthFilter;
			return false;
		}
		if (x is SDS_ProjectionHolder)
		{
			__result = true;
			return false;
		}
		return true;
	}
}
public static class SDS_TakeService
{
	public static bool TryTake(Thing mirror, int count, out Thing taken)
	{
		return SDS_WithdrawExecutor.TryTakeMirror(mirror, count, out taken);
	}

	internal static bool TryTakeInternal(Thing mirror, int count, out Thing taken)
	{
		taken = null;
		if (mirror == null || count <= 0 || mirror.Destroyed || !mirror.Spawned || mirror.Map == null || !SDS_MirrorRegistry.IsMirror(mirror))
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			return false;
		}
		SDS_StockKey sDS_StockKey = SDS_StockKey.FromThing(mirror);
		if (sDS_StockKey == null || sDS_StockKey.thingDef == null)
		{
			return false;
		}
		long count2 = instance.GetCount(sDS_StockKey);
		if (count2 <= 0)
		{
			return false;
		}
		int num = mirror.stackCount;
		if (num <= 0)
		{
			return false;
		}
		if (num > count2)
		{
			num = (mirror.stackCount = (int)Math.Min(count2, 2147483647L));
			if (mirror.Map != null)
			{
				mirror.DirtyMapMesh(mirror.Map);
			}
			SDS_MirrorService.MarkChanged();
		}
		int val = Math.Max(1, sDS_StockKey.thingDef.stackLimit);
		int val2 = Math.Min(num, val);
		int num2 = Math.Min(count, val2);
		if (num2 <= 0)
		{
			return false;
		}
		if (!instance.TryDebitCount(sDS_StockKey, num2))
		{
			return false;
		}
		Map map = mirror.Map;
		if (num2 >= num)
		{
			SDS_MirrorService.ForgetMirror(mirror);
			SDS_MirrorRegistry.Unmark(mirror);
			CompRottable val3 = ThingCompUtility.TryGetComp<CompRottable>(mirror);
			if (val3 != null)
			{
				val3.disabled = false;
			}
			taken = mirror;
			if (mirror.Spawned)
			{
				mirror.DeSpawnOrDeselect((DestroyMode)0);
			}
			return true;
		}
		Thing val4 = SDS_OutputUtility.MakeDisplayThing(sDS_StockKey);
		if (val4 == null)
		{
			SDS_ReturnUtility.ReturnVirtualCount(sDS_StockKey, num2);
			return false;
		}
		val4.stackCount = num2;
		mirror.stackCount = num - num2;
		if (map != null)
		{
			mirror.DirtyMapMesh(map);
		}
		SDS_MirrorService.MarkChanged();
		taken = val4;
		return true;
	}

	internal static bool TryConsumeInternal(Thing mirror, out SDS_StockKey key, out int consumed)
	{
		key = null;
		consumed = 0;
		if (mirror == null || mirror.Destroyed || !mirror.Spawned || mirror.Map == null || !SDS_MirrorRegistry.IsMirror(mirror))
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			return false;
		}
		key = SDS_StockKey.FromThing(mirror);
		if (key == null || key.thingDef == null || key.preserved)
		{
			key = null;
			return false;
		}
		int stackCount = mirror.stackCount;
		long count = instance.GetCount(key);
		if (stackCount > 0 && count > 0)
		{
			int num = (int)Math.Min(stackCount, count);
			if (!instance.TryDebitCount(key, num))
			{
				return false;
			}
			consumed = num;
		}
		SDS_MirrorService.ForgetMirror(mirror);
		SDS_MirrorRegistry.Unmark(mirror);
		SDS_MirrorService.MarkChanged();
		return true;
	}

	public static void ReturnToGridOrDrop(Thing thing, Map map, IntVec3 cell)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		SDS_ReturnUtility.Return(thing, map, cell, SDS_ReturnFallback.Ground);
	}
}
[HarmonyPatch(typeof(ThingOwner), "Take", new Type[]
{
	typeof(Thing),
	typeof(int)
})]
[HarmonyPriority(200)]
internal static class Patch_SDS_ProjectionTake
{
	public static bool Prefix(ThingOwner __instance, Thing thing, int count, ref Thing __result)
	{
		if (!(__instance is SDS_ProjectionOwner sDS_ProjectionOwner))
		{
			return true;
		}
		__result = sDS_ProjectionOwner.TakeFromProjection(thing, count);
		if (__result == null)
		{
			SDS_Diag.Fail("投影/取出失败", SDS_Diag.Ctx((thing != null) ? thing.MapHeld : null, null, thing, null, "count=" + count));
		}
		return false;
	}
}
[HarmonyPatch]
internal static class Patch_SDS_ProjectionTryDropCounted
{
	public static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(ThingOwner), "TryDrop", new Type[8]
		{
			typeof(Thing),
			typeof(IntVec3),
			typeof(Map),
			typeof(ThingPlaceMode),
			typeof(int),
			typeof(Thing).MakeByRefType(),
			typeof(Action<Thing, int>),
			typeof(Predicate<IntVec3>)
		}, (Type[])null);
	}

	public unsafe static bool Prefix(ThingOwner __instance, Thing thing, IntVec3 dropLoc, Map map, ThingPlaceMode mode, int count, ref Thing resultingThing, Action<Thing, int> placedAction, Predicate<IntVec3> nearPlaceValidator, ref bool __result)
	{
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		if (!(__instance is SDS_ProjectionOwner sDS_ProjectionOwner))
		{
			return true;
		}
		__result = false;
		resultingThing = null;
		if (thing == null || !sDS_ProjectionOwner.ContainsReference(thing) || count <= 0)
		{
			SDS_Diag.Fail("投影/落地参数无效", SDS_Diag.Ctx(map, null, thing, null, "count=" + count));
			return false;
		}
		if (count > thing.stackCount)
		{
			count = thing.stackCount;
		}
		if (!sDS_ProjectionOwner.TryTakeForTransfer(thing, count, out var taken))
		{
			SDS_Diag.Fail("投影/落地扣账失败", SDS_Diag.Ctx(map, null, thing, null, "count=" + count));
			return false;
		}
		Thing val = default(Thing);
		if (GenDrop.TryDropSpawn(taken, dropLoc, map, mode, ref val, placedAction, nearPlaceValidator, true))
		{
			resultingThing = val;
			__result = true;
			return false;
		}
		Thing thing2 = taken;
		IntVec3 val2 = dropLoc;
		SDS_Diag.Fail("投影/落地失败", SDS_Diag.Ctx(map, null, thing2, null, "cell=" + ((object)(*(IntVec3*)(&val2))/*cast due to constrained. prefix*/).ToString()));
		SDS_TakeService.ReturnToGridOrDrop(taken, map, dropLoc);
		return false;
	}
}
[HarmonyPatch]
[HarmonyPriority(200)]
internal static class Patch_SDS_ProjectionTryDropAll
{
	public static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(ThingOwner), "TryDrop", new Type[8]
		{
			typeof(Thing),
			typeof(IntVec3),
			typeof(Map),
			typeof(ThingPlaceMode),
			typeof(Thing).MakeByRefType(),
			typeof(Action<Thing, int>),
			typeof(Predicate<IntVec3>),
			typeof(bool)
		}, (Type[])null);
	}

	public unsafe static bool Prefix(ThingOwner __instance, Thing thing, IntVec3 dropLoc, Map map, ThingPlaceMode mode, ref Thing lastResultingThing, Action<Thing, int> placedAction, Predicate<IntVec3> nearPlaceValidator, bool playDropSound, ref bool __result)
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		if (!(__instance is SDS_ProjectionOwner sDS_ProjectionOwner))
		{
			return true;
		}
		__result = false;
		lastResultingThing = null;
		if (thing == null || !sDS_ProjectionOwner.ContainsReference(thing))
		{
			SDS_Diag.Fail("投影/整堆落地参数无效", SDS_Diag.Ctx(map, null, thing));
			return false;
		}
		if (!sDS_ProjectionOwner.TryTakeForTransfer(thing, thing.stackCount, out var taken))
		{
			SDS_Diag.Fail("投影/整堆落地扣账失败", SDS_Diag.Ctx(map, null, thing));
			return false;
		}
		Thing val = default(Thing);
		if (GenDrop.TryDropSpawn(taken, dropLoc, map, mode, ref val, placedAction, nearPlaceValidator, playDropSound))
		{
			lastResultingThing = val;
			__result = true;
			return false;
		}
		Thing thing2 = taken;
		IntVec3 val2 = dropLoc;
		SDS_Diag.Fail("投影/整堆落地失败", SDS_Diag.Ctx(map, null, thing2, null, "cell=" + ((object)(*(IntVec3*)(&val2))/*cast due to constrained. prefix*/).ToString()));
		SDS_TakeService.ReturnToGridOrDrop(taken, map, dropLoc);
		return false;
	}
}
[HarmonyPatch]
[HarmonyPriority(200)]
internal static class Patch_SDS_ProjectionTryTransfer
{
	public static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(ThingOwner), "TryTransferToContainer", new Type[5]
		{
			typeof(Thing),
			typeof(ThingOwner),
			typeof(int),
			typeof(Thing).MakeByRefType(),
			typeof(bool)
		}, (Type[])null);
	}

	public static bool Prefix(ThingOwner __instance, Thing item, ThingOwner otherContainer, int count, ref Thing resultingTransferredItem, bool canMergeWithExistingStacks, ref int __result)
	{
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		if (!(__instance is SDS_ProjectionOwner sDS_ProjectionOwner))
		{
			return true;
		}
		__result = 0;
		resultingTransferredItem = null;
		if (item == null || otherContainer == null || count <= 0 || !sDS_ProjectionOwner.ContainsReference(item))
		{
			SDS_Diag.Fail("投影/转移参数无效", SDS_Diag.Ctx((item != null) ? item.MapHeld : null, null, item, null, "count=" + count));
			return false;
		}
		if (otherContainer == __instance)
		{
			resultingTransferredItem = item;
			__result = item.stackCount;
			return false;
		}
		int num = Math.Min(count, item.stackCount);
		if (num <= 0 || otherContainer.GetCountCanAccept(item, canMergeWithExistingStacks) < num)
		{
			SDS_Diag.Fail("投影/目标容量不足", SDS_Diag.Ctx(item.MapHeld, null, item, null, "take=" + num + " canAccept=" + ((otherContainer != null) ? otherContainer.GetCountCanAccept(item, canMergeWithExistingStacks) : (-1))));
			return false;
		}
		if (!sDS_ProjectionOwner.TryTakeForTransfer(item, num, out var taken))
		{
			SDS_Diag.Fail("投影/转移扣账失败", SDS_Diag.Ctx(item.MapHeld, null, item, null, "take=" + num));
			return false;
		}
		if (otherContainer.TryAdd(taken, canMergeWithExistingStacks))
		{
			resultingTransferredItem = taken;
			__result = ((taken != null && !taken.Destroyed) ? taken.stackCount : 0);
			return false;
		}
		IThingHolder owner = __instance.Owner;
		Map rootMap = ThingOwnerUtility.GetRootMap(owner);
		IntVec3 rootPosition = ThingOwnerUtility.GetRootPosition(owner);
		SDS_Diag.Fail("投影/目标拒收", SDS_Diag.Ctx(rootMap, null, taken, null, "take=" + num));
		SDS_TakeService.ReturnToGridOrDrop(taken, rootMap, rootPosition);
		return false;
	}
}
internal static class SDS_ProjectionViewOps
{
	public static void Begin(ThingOwner owner)
	{
		(owner as SDS_ProjectionOwner)?.BeginViewOp();
	}

	public static Exception End(ThingOwner owner, Exception exception)
	{
		(owner as SDS_ProjectionOwner)?.EndViewOp();
		return exception;
	}
}
[HarmonyPatch(typeof(ThingOwner), "Clear")]
internal static class Patch_SDS_ProjectionClear
{
	public static void Prefix(ThingOwner __instance)
	{
		SDS_ProjectionViewOps.Begin(__instance);
	}

	public static Exception Finalizer(ThingOwner __instance, Exception __exception)
	{
		return SDS_ProjectionViewOps.End(__instance, __exception);
	}
}
[HarmonyPatch(typeof(ThingOwner), "ClearAndDestroyContents")]
internal static class Patch_SDS_ProjectionClearAndDestroy
{
	public static void Prefix(ThingOwner __instance)
	{
		SDS_ProjectionViewOps.Begin(__instance);
	}

	public static Exception Finalizer(ThingOwner __instance, Exception __exception)
	{
		return SDS_ProjectionViewOps.End(__instance, __exception);
	}
}
[HarmonyPatch(typeof(ThingOwner), "ClearAndDestroyContentsOrPassToWorld")]
internal static class Patch_SDS_ProjectionClearAndDestroyOrPassToWorld
{
	public static void Prefix(ThingOwner __instance)
	{
		SDS_ProjectionViewOps.Begin(__instance);
	}

	public static Exception Finalizer(ThingOwner __instance, Exception __exception)
	{
		return SDS_ProjectionViewOps.End(__instance, __exception);
	}
}
[HarmonyPatch(typeof(ThingOwner), "RemoveAll", new Type[] { typeof(Predicate<Thing>) })]
internal static class Patch_SDS_ProjectionRemoveAll
{
	public static void Prefix(ThingOwner __instance)
	{
		SDS_ProjectionViewOps.Begin(__instance);
	}

	public static Exception Finalizer(ThingOwner __instance, Exception __exception)
	{
		return SDS_ProjectionViewOps.End(__instance, __exception);
	}
}
[HarmonyPatch(typeof(ThingOwner<Thing>), "RemoveAll", new Type[] { typeof(Predicate<Thing>) })]
internal static class Patch_SDS_ProjectionRemoveAllGeneric
{
	public static void Prefix(ThingOwner<Thing> __instance)
	{
		SDS_ProjectionViewOps.Begin((ThingOwner)(object)__instance);
	}

	public static Exception Finalizer(ThingOwner<Thing> __instance, Exception __exception)
	{
		return SDS_ProjectionViewOps.End((ThingOwner)(object)__instance, __exception);
	}
}
[HarmonyPatch(typeof(ListerHaulables), "ShouldBeHaulable")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNotHaulable
{
	public static void Postfix(Thing t, ref bool __result)
	{
		if (__result && SDS_MirrorRegistry.IsMirror(t))
		{
			__result = false;
		}
	}
}
[HarmonyPatch(typeof(ListerMergeables), "ShouldBeMergeable")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNotMergeable
{
	public static void Postfix(Thing t, ref bool __result)
	{
		if (__result && SDS_MirrorRegistry.IsMirror(t))
		{
			__result = false;
		}
	}
}
internal static class SDS_MirrorAbsorb
{
	private const int MaxRounds = 256;

	public static bool TryHandle(Thing receiver, Thing other, bool respectStackLimit, out bool result)
	{
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		result = false;
		bool flag = SDS_MirrorRegistry.IsMirror(receiver);
		bool flag2 = SDS_MirrorRegistry.IsMirror(other);
		if (!flag && !flag2)
		{
			return false;
		}
		if (flag)
		{
			return true;
		}
		try
		{
			if (receiver == null || receiver.Destroyed || other == null || other.Destroyed || other.stackCount <= 0 || !receiver.CanStackWith(other))
			{
				return true;
			}
			int num = ThingUtility.TryAbsorbStackNumToTake(receiver, other, respectStackLimit);
			int val = ((other.def != null) ? Math.Max(1, other.def.stackLimit) : num);
			for (int i = 0; i < 256; i++)
			{
				if (num <= 0)
				{
					break;
				}
				if (receiver.Destroyed)
				{
					break;
				}
				if (other.Destroyed)
				{
					break;
				}
				if (other.stackCount <= 0)
				{
					break;
				}
				int count = Math.Min(num, val);
				if (!SDS_TakeService.TryTake(other, count, out var taken) || taken == null || taken.Destroyed)
				{
					break;
				}
				int stackCount = taken.stackCount;
				if (!receiver.TryAbsorbStack(taken, respectStackLimit))
				{
					SDS_ReturnUtility.ReturnVirtual(taken, SDS_StockKey.FromThing(taken), receiver.MapHeld, receiver.PositionHeld);
					break;
				}
				num -= Math.Max(0, stackCount);
			}
			result = other.Destroyed || !SDS_MirrorRegistry.IsMirror(other);
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/合并取出异常", SDS_Diag.Ctx((receiver != null) ? receiver.Map : null, null, other), exception);
			result = false;
			return true;
		}
	}
}
[HarmonyPatch(typeof(Thing), "TryAbsorbStack")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorAbsorb
{
	public static bool Prefix(Thing __instance, Thing other, bool respectStackLimit, ref bool __result)
	{
		if (SDS_MirrorAbsorb.TryHandle(__instance, other, respectStackLimit, out var result))
		{
			__result = result;
			return false;
		}
		return true;
	}
}
[HarmonyPatch(typeof(ThingWithComps), "TryAbsorbStack")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorAbsorbWithComps
{
	public static bool Prefix(ThingWithComps __instance, Thing other, bool respectStackLimit, ref bool __result)
	{
		if (SDS_MirrorAbsorb.TryHandle((Thing)(object)__instance, other, respectStackLimit, out var result))
		{
			__result = result;
			return false;
		}
		return true;
	}
}
[HarmonyPatch(typeof(Thing), "SplitOff")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorTakeOnSplitOff
{
	public static bool Prefix(Thing __instance, int count, ref Thing __result)
	{
		if (!SDS_MirrorRegistry.IsMirror(__instance))
		{
			return true;
		}
		try
		{
			if (SDS_TakeService.TryTake(__instance, count, out var taken))
			{
				__result = taken;
				return false;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/SplitOff异常", SDS_Diag.Ctx(__instance.Map, null, __instance, null, "count=" + count), exception);
		}
		__result = null;
		return false;
	}
}
internal static class SDS_MirrorCarry
{
	public static int TakeIntoCarry(Pawn_CarryTracker tracker, Thing mirror, int count, bool reserve)
	{
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		if (tracker == null || mirror == null)
		{
			return 0;
		}
		Pawn pawn = tracker.pawn;
		if (pawn == null || ((Thing)pawn).Map == null || pawn.Dead || pawn.Downed)
		{
			return 0;
		}
		int val = tracker.AvailableStackSpace(mirror.def);
		int num = Math.Min(count, Math.Min(val, mirror.stackCount));
		if (num <= 0)
		{
			return 0;
		}
		if (!SDS_TakeService.TryTake(mirror, num, out var taken) || taken == null || taken.Destroyed)
		{
			return 0;
		}
		int num2 = tracker.TryStartCarry(taken, taken.stackCount, reserve);
		Thing carriedThing = tracker.CarriedThing;
		Job val2 = ((pawn.jobs != null) ? pawn.jobs.curJob : null);
		if (num2 > 0 && carriedThing != null && val2 != null)
		{
			SDS_ReclaimRegistry.TrackHand(val2, carriedThing);
			if (ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(carriedThing), 1, -1, (ReservationLayerDef)null, false))
			{
				ReservationUtility.Reserve(pawn, LocalTargetInfo.op_Implicit(carriedThing), val2, 1, -1, (ReservationLayerDef)null, true, false);
			}
			if (mirror.Spawned && !mirror.Destroyed && ((Thing)pawn).Map.reservationManager.ReservedBy(LocalTargetInfo.op_Implicit(mirror), pawn, val2))
			{
				((Thing)pawn).Map.reservationManager.Release(LocalTargetInfo.op_Implicit(mirror), pawn, val2);
			}
		}
		if (taken != null && !taken.Destroyed && taken.stackCount > 0 && taken.holdingOwner == null)
		{
			SDS_ReturnUtility.Return(taken, ((Thing)pawn).Map, ((Thing)pawn).Position, SDS_ReturnFallback.PawnCarry, pawn);
		}
		return num2;
	}
}
[HarmonyPatch(typeof(Pawn_CarryTracker), "TryStartCarry", new Type[] { typeof(Thing) })]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorCarry
{
	public static bool Prefix(Pawn_CarryTracker __instance, Thing item, ref bool __result)
	{
		if (!SDS_MirrorRegistry.IsMirror(item))
		{
			return true;
		}
		try
		{
			__result = SDS_MirrorCarry.TakeIntoCarry(__instance, item, item.stackCount, reserve: true) > 0;
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/入手异常", SDS_Diag.Ctx((__instance != null && __instance.pawn != null) ? ((Thing)__instance.pawn).Map : null, __instance?.pawn, item), exception);
			__result = false;
		}
		return false;
	}
}
[HarmonyPatch(typeof(Pawn_CarryTracker), "TryStartCarry", new Type[]
{
	typeof(Thing),
	typeof(int),
	typeof(bool)
})]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorCarryCounted
{
	public static bool Prefix(Pawn_CarryTracker __instance, Thing item, int count, bool reserve, ref int __result)
	{
		if (!SDS_MirrorRegistry.IsMirror(item))
		{
			return true;
		}
		try
		{
			__result = SDS_MirrorCarry.TakeIntoCarry(__instance, item, count, reserve);
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/入手异常", SDS_Diag.Ctx((__instance != null && __instance.pawn != null) ? ((Thing)__instance.pawn).Map : null, __instance?.pawn, item, null, "count=" + count), exception);
			__result = 0;
		}
		return false;
	}
}
[HarmonyPatch(typeof(Thing), "TakeDamage")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNoDamage
{
	public static bool Prefix(Thing __instance, ref DamageResult __result)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected O, but got Unknown
		if (SDS_MirrorRegistry.IsMirror(__instance) || Building_SDS_Vault.IsVault(__instance))
		{
			__result = new DamageResult();
			return false;
		}
		return true;
	}
}
[HarmonyPatch(typeof(Thing), "PreApplyDamage")]
internal static class Patch_SDS_VaultPreApplyDamage
{
	public static bool Prefix(Thing __instance, ref bool absorbed)
	{
		if (!Building_SDS_Vault.IsVault(__instance))
		{
			return true;
		}
		absorbed = true;
		return false;
	}
}
[HarmonyPatch(typeof(FireUtility), "TryAttachFire")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNoFire
{
	public static bool Prefix(Thing t)
	{
		return !SDS_MirrorRegistry.IsMirror(t) && !Building_SDS_Vault.IsVault(t);
	}
}
[HarmonyPatch(typeof(Thing), "DeSpawn")]
internal static class Patch_SDS_MirrorNoDeSpawn
{
	public static bool Prefix(Thing __instance)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Invalid comparison between Unknown and I4
		return !SDS_MirrorRegistry.IsMirror(__instance) || SDS_MirrorRegistry.AllowDeSpawn || (int)Current.ProgramState != 2;
	}
}
[HarmonyPatch(typeof(ThingWithComps), "DeSpawn")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNoDeSpawnWithComps
{
	public static bool Prefix(ThingWithComps __instance)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Invalid comparison between Unknown and I4
		return !SDS_MirrorRegistry.IsMirror((Thing)(object)__instance) || SDS_MirrorRegistry.AllowDeSpawn || (int)Current.ProgramState != 2;
	}
}
[HarmonyPatch(typeof(MapDeiniter), "Deinit")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MapDeinitAllowMirrorDespawn
{
	public static void Prefix(Map map)
	{
		SDS_MirrorRegistry.AllowDeSpawn = true;
		try
		{
			SDS_MirrorService.EvacuateForStructureChange(map, "map-deinit");
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("镜像/结构撤离异常（已忽略，继续卸载地图）", exception);
		}
	}

	public static Exception Finalizer(Exception __exception)
	{
		SDS_MirrorRegistry.AllowDeSpawn = false;
		return __exception;
	}
}
[HarmonyPatch(typeof(Thing), "SpawnSetup")]
internal static class Patch_SDS_MirrorSpawnSetup
{
	public static void Postfix(Thing __instance)
	{
		if (!SDS_MirrorRegistry.IsMirror(__instance))
		{
			return;
		}
		try
		{
			SDS_MirrorService.OnMirrorSpawned(__instance);
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("镜像/落点初始化异常", SDS_Diag.Ctx(__instance.Map, null, __instance), exception);
		}
	}
}
internal sealed class SDS_MirrorConsumeState
{
	public SDS_StockKey key;

	public int count;

	public Map map;
}
internal static class SDS_MirrorConsume
{
	public static bool Prefix(Thing thing, out SDS_MirrorConsumeState state)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		state = null;
		if (!SDS_MirrorRegistry.IsMirror(thing))
		{
			return true;
		}
		if (SDS_MirrorRegistry.AllowDeSpawn || (int)Current.ProgramState != 2)
		{
			return true;
		}
		if (thing.Destroyed || thing.def == null || !thing.def.destroyable)
		{
			return true;
		}
		if (!thing.Spawned)
		{
			SDS_MirrorRegistry.Unmark(thing);
			return true;
		}
		try
		{
			if (SDS_WithdrawExecutor.TryConsumeMirror(thing, out var key, out var consumed))
			{
				state = new SDS_MirrorConsumeState
				{
					key = key,
					count = consumed,
					map = thing.MapHeld
				};
				return true;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/销毁扣账异常", SDS_Diag.Ctx(thing.Map, null, thing), exception);
			return false;
		}
		SDS_Diag.Fail("取货/销毁扣账失败（已拒绝销毁，保护实物）", SDS_Diag.Ctx(thing.Map, null, thing, null, "stackCount=" + thing.stackCount));
		return false;
	}

	public static Exception Finalizer(Thing thing, SDS_MirrorConsumeState state, Exception exception)
	{
		if (state == null || thing == null || thing.Destroyed)
		{
			return exception;
		}
		if (state.count > 0 && !SDS_ReturnUtility.ReturnVirtualCount(state.key, state.count))
		{
			SDS_Diag.Fail("取货/销毁未完成且回充失败", SDS_Diag.Ctx(state.map, null, thing, null, "count=" + state.count));
		}
		try
		{
			SDS_MirrorRegistry.Unmark(thing);
			thing.Destroy((DestroyMode)0);
		}
		catch (Exception exception2)
		{
			SDS_Diag.ErrorOnce("取货/销毁未完成（镜像已回充账本，对象清理失败）", exception2);
		}
		SDS_Diag.Warn("取货/销毁未完成", SDS_Diag.Ctx(state.map, null, thing, null, "count=" + state.count));
		return exception;
	}
}
[HarmonyPatch(typeof(Thing), "Destroy")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorDestroyConsume
{
	public static bool Prefix(Thing __instance, out SDS_MirrorConsumeState __state)
	{
		return SDS_MirrorConsume.Prefix(__instance, out __state);
	}

	public static Exception Finalizer(Thing __instance, SDS_MirrorConsumeState __state, Exception __exception)
	{
		return SDS_MirrorConsume.Finalizer(__instance, __state, __exception);
	}
}
[HarmonyPatch(typeof(ThingWithComps), "Destroy")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorDestroyConsumeWithComps
{
	public static bool Prefix(ThingWithComps __instance, out SDS_MirrorConsumeState __state)
	{
		return SDS_MirrorConsume.Prefix((Thing)(object)__instance, out __state);
	}

	public static Exception Finalizer(ThingWithComps __instance, SDS_MirrorConsumeState __state, Exception __exception)
	{
		return SDS_MirrorConsume.Finalizer((Thing)(object)__instance, __state, __exception);
	}
}
[HarmonyPatch(typeof(Thing), "DynamicDrawPhaseAt", new Type[]
{
	typeof(DrawPhase),
	typeof(Vector3),
	typeof(bool)
})]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNoRealtimeDraw
{
	public static bool Prefix(Thing __instance)
	{
		return !SDS_MirrorRegistry.Any || !SDS_MirrorRegistry.IsMirror(__instance);
	}
}
[HarmonyPatch(typeof(Thing), "Print")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNoMeshPrint
{
	public static bool Prefix(Thing __instance)
	{
		return !SDS_MirrorRegistry.Any || !SDS_MirrorRegistry.IsMirror(__instance);
	}
}
[HarmonyPatch(typeof(ThingWithComps), "Print")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNoMeshPrintWithComps
{
	public static bool Prefix(ThingWithComps __instance)
	{
		return !SDS_MirrorRegistry.Any || !SDS_MirrorRegistry.IsMirror((Thing)(object)__instance);
	}
}
[HarmonyPatch(typeof(Thing), "DrawGUIOverlay")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNoGuiOverlay
{
	public static bool Prefix(Thing __instance)
	{
		return !SDS_MirrorRegistry.Any || !SDS_MirrorRegistry.IsMirror(__instance);
	}
}
[HarmonyPatch(typeof(ThingWithComps), "DrawGUIOverlay")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNoGuiOverlayWithComps
{
	public static bool Prefix(ThingWithComps __instance)
	{
		return !SDS_MirrorRegistry.Any || !SDS_MirrorRegistry.IsMirror((Thing)(object)__instance);
	}
}
[HarmonyPatch(typeof(ThingSelectionUtility), "SelectableByMapClick")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNotSelectable
{
	public static void Postfix(Thing t, ref bool __result)
	{
		if (__result && SDS_MirrorRegistry.Any && SDS_MirrorRegistry.IsMirror(t))
		{
			__result = false;
		}
	}
}
[HarmonyPatch(typeof(Selector), "Select")]
[HarmonyPriority(200)]
internal static class Patch_SDS_MirrorNotSelectableDirect
{
	public static bool Prefix(object obj)
	{
		Thing val = (Thing)((obj is Thing) ? obj : null);
		return val == null || !SDS_MirrorRegistry.Any || !SDS_MirrorRegistry.IsMirror(val);
	}
}
[HarmonyPatch]
[HarmonyPriority(200)]
internal static class Patch_SDS_RoomIgnoresHiddenGrid
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.PropertyGetter(typeof(Room), "ContainedAndAdjacentThings");
	}

	public static void Postfix(List<Thing> __result)
	{
		if (__result == null || __result.Count == 0)
		{
			return;
		}
		for (int num = __result.Count - 1; num >= 0; num--)
		{
			Thing val = __result[num];
			if (val != null && (SDS_MirrorRegistry.IsMirror(val) || Building_SDS_Vault.IsVault(val)))
			{
				__result.RemoveAt(num);
			}
		}
	}
}
[HarmonyPatch(typeof(Room), "ContainsThing")]
[HarmonyPriority(200)]
internal static class Patch_SDS_RoomContainsThingIgnoresHiddenGrid
{
	public static void Postfix(Room __instance, ThingDef def, ref bool __result)
	{
		if (!__result || __instance == null || def == null || !SDS_MirrorRegistry.Any)
		{
			return;
		}
		try
		{
			List<Region> regions = __instance.Regions;
			for (int i = 0; i < regions.Count; i++)
			{
				List<Thing> list = regions[i].ListerThings.ThingsOfDef(def);
				for (int j = 0; j < list.Count; j++)
				{
					Thing val = list[j];
					if (val != null && !SDS_MirrorRegistry.IsMirror(val) && !Building_SDS_Vault.IsVault(val))
					{
						return;
					}
				}
			}
			__result = false;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("房间包含判定过滤隐藏库存失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(Room), "ContainedThings", new Type[] { typeof(ThingDef) })]
[HarmonyPriority(200)]
internal static class Patch_SDS_RoomContainedThingsIgnoresHiddenGrid
{
	public static void Postfix(ref IEnumerable<Thing> __result)
	{
		if (__result == null || !SDS_MirrorRegistry.Any)
		{
			return;
		}
		try
		{
			List<Thing> list = new List<Thing>();
			foreach (Thing item in __result)
			{
				if (item != null && !SDS_MirrorRegistry.IsMirror(item) && !Building_SDS_Vault.IsVault(item))
				{
					list.Add(item);
				}
			}
			__result = list;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("房间内容枚举过滤隐藏库存失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(Room), "ThingCount")]
[HarmonyPriority(200)]
internal static class Patch_SDS_RoomThingCountIgnoresHiddenGrid
{
	private static readonly HashSet<Thing> seen = new HashSet<Thing>();

	public static bool Prefix(Room __instance, ThingDef def, ref int __result)
	{
		if (__instance == null || def == null || !SDS_MirrorRegistry.Any)
		{
			return true;
		}
		try
		{
			int num = 0;
			seen.Clear();
			List<Region> regions = __instance.Regions;
			for (int i = 0; i < regions.Count; i++)
			{
				List<Thing> list = regions[i].ListerThings.ThingsOfDef(def);
				for (int j = 0; j < list.Count; j++)
				{
					Thing val = list[j];
					if (val != null && !SDS_MirrorRegistry.IsMirror(val) && !Building_SDS_Vault.IsVault(val) && seen.Add(val))
					{
						num += val.stackCount;
					}
				}
			}
			seen.Clear();
			__result = num;
			return false;
		}
		catch (Exception exception)
		{
			seen.Clear();
			SDS_Diag.ErrorOnce("房间数量统计过滤隐藏库存失败（已忽略，原版行为不受影响）", exception);
			return true;
		}
	}
}
[HarmonyPatch]
[HarmonyPriority(200)]
internal static class Patch_SDS_ObserverIgnoresHiddenGrid
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(PawnObserver), "PossibleToObserve", new Type[1] { typeof(Thing) }, (Type[])null);
	}

	public static bool Prefix(Thing thing, ref bool __result)
	{
		if (thing != null && (SDS_MirrorRegistry.IsMirror(thing) || Building_SDS_Vault.IsVault(thing)))
		{
			__result = false;
			return false;
		}
		return true;
	}
}
[HarmonyPatch(typeof(TantrumMentalStateUtility), "CanSmash")]
[HarmonyPriority(200)]
internal static class Patch_SDS_TantrumIgnoresHiddenGrid
{
	public static bool Prefix(Thing thing, ref bool __result)
	{
		if (thing != null && (SDS_MirrorRegistry.IsMirror(thing) || Building_SDS_Vault.IsVault(thing)))
		{
			__result = false;
			return false;
		}
		return true;
	}
}
[HarmonyPatch]
[HarmonyPriority(200)]
internal static class Patch_SDS_FireSpreeIgnoresHiddenGrid
{
	private static MethodBase TargetMethod()
	{
		Type type = AccessTools.TypeByName("RimWorld.JobGiver_FireStartingSpree");
		return (type != null) ? AccessTools.Method(type, "TryFindRandomIgniteTarget", (Type[])null, (Type[])null) : null;
	}

	public static void Postfix(ref Thing __result)
	{
		if (__result != null && (SDS_MirrorRegistry.IsMirror(__result) || Building_SDS_Vault.IsVault(__result)))
		{
			__result = null;
		}
	}
}
internal static class SDS_Exposure
{
	private static readonly List<SDS_StockKey> injectedKeys = new List<SDS_StockKey>();

	private static readonly List<SDS_StockKey> injectedMirrorKeys = new List<SDS_StockKey>();

	private const int CounterMinIntervalTicks = 30;

	private static readonly Dictionary<Map, int> counterRefreshTick = new Dictionary<Map, int>();

	private static readonly Dictionary<Map, SDS_ReadVersions> counterVersions = new Dictionary<Map, SDS_ReadVersions>();

	public static long CounterAdjust(Map map, SDS_StockKey key)
	{
		return CounterAdjust(map, key, SDS_OutputUtility.MapExposesGrid(map));
	}

	public static long CounterAdjust(Map map, SDS_StockKey key, bool exposed)
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (map == null || key == null || instance == null)
		{
			return 0L;
		}
		long num = (exposed ? instance.GetCount(key) : 0);
		long num2 = SDS_MirrorService.MirrorExposureOnResourceCounter(map, key);
		if (num2 == 0L && SDS_MirrorService.TryGetMirror(map, key, out var _))
		{
			return 0L;
		}
		return num - num2;
	}

	public static long InjectedOnCounter(Map map, ThingDef def, bool exposed)
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (map == null || def == null || instance == null)
		{
			return 0L;
		}
		long num = 0L;
		instance.CopyKeysForDef(def, injectedKeys);
		for (int i = 0; i < injectedKeys.Count; i++)
		{
			num += CounterAdjust(map, injectedKeys[i], exposed);
		}
		SDS_MirrorService.CopyMirrorKeys(map, injectedMirrorKeys);
		for (int j = 0; j < injectedMirrorKeys.Count; j++)
		{
			SDS_StockKey sDS_StockKey = injectedMirrorKeys[j];
			if (sDS_StockKey != null && sDS_StockKey.thingDef == def && !ContainsKey(injectedKeys, sDS_StockKey))
			{
				num += CounterAdjust(map, sDS_StockKey, exposed);
			}
		}
		if (!exposed)
		{
			return num;
		}
		return num + instance.GetPreservedCountOfDef(def);
	}

	private static bool ContainsKey(List<SDS_StockKey> keys, SDS_StockKey key)
	{
		if (keys == null || key == null)
		{
			return false;
		}
		for (int i = 0; i < keys.Count; i++)
		{
			SDS_StockKey sDS_StockKey = keys[i];
			if (sDS_StockKey == key || (sDS_StockKey != null && sDS_StockKey.SameAs(key)))
			{
				return true;
			}
		}
		return false;
	}

	internal static void Reset()
	{
		counterRefreshTick.Clear();
		counterVersions.Clear();
		injectedKeys.Clear();
		injectedMirrorKeys.Clear();
	}

	public static bool TryBeginCounterRefresh(Map map)
	{
		if (map == null || map.resourceCounter == null)
		{
			return false;
		}
		int num = ((Find.TickManager != null) ? Find.TickManager.TicksGame : 0);
		if (num % 204 == 0)
		{
			return false;
		}
		SDS_ReadVersions value = SDS_ReadVersions.Capture();
		if (counterVersions.TryGetValue(map, out var value2) && value.SameExposureAs(in value2))
		{
			return false;
		}
		if (counterRefreshTick.TryGetValue(map, out var value3) && num - value3 < 30)
		{
			return false;
		}
		if (!SDS_OutputUtility.MapExposesGrid(map))
		{
			return false;
		}
		if (counterVersions.Count > 32)
		{
			counterVersions.Clear();
			counterRefreshTick.Clear();
		}
		counterVersions[map] = value;
		counterRefreshTick[map] = num;
		return true;
	}

	public static void ForceCounterRefresh(Map map)
	{
		if (map == null || map.resourceCounter == null)
		{
			return;
		}
		int num = ((Find.TickManager != null) ? Find.TickManager.TicksGame : 0);
		if (!counterRefreshTick.TryGetValue(map, out var value) || value != num)
		{
			if (counterRefreshTick.Count > 32)
			{
				counterRefreshTick.Clear();
				counterVersions.Clear();
			}
			counterRefreshTick[map] = num;
			counterVersions[map] = SDS_ReadVersions.Capture();
			map.resourceCounter.UpdateResourceCounts();
		}
	}

	public static long PhysicalMirrorUnitOffset(Map map, SDS_StockKey key)
	{
		return PhysicalMirrorUnitOffset(map, key, mirrorCounts: true);
	}

	public static long PhysicalMirrorUnitOffset(Map map, SDS_StockKey key, bool mirrorCounts)
	{
		Thing mirror;
		return (mirrorCounts && SDS_MirrorService.TryGetMirror(map, key, out mirror)) ? (-1) : 0;
	}

	public static long LedgerTotalForDef(ThingDef def)
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || def == null)
		{
			return 0L;
		}
		return instance.GetTotalCountForDef(def) + instance.GetPreservedCountOfDef(def);
	}

	public static long AccessibleTotal(ThingDef def)
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || def == null)
		{
			return 0L;
		}
		return SDS_Selection.NonMirrorAccessibleCount(def) + LedgerTotalForDef(def);
	}

	public static float WealthAdjust(Map map)
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (map == null || instance == null)
		{
			return 0f;
		}
		float num = SDS_MirrorService.MirrorWealthOn(map);
		if (SDS_Config.ExcludeGridFromWealth)
		{
			return 0f - num;
		}
		if (!SDS_OutputUtility.MapExposesGrid(map) || SDS_Selection.CanonicalExposureMap() != map)
		{
			return 0f - num;
		}
		return LedgerVirtualWealth(map) + instance.PreservedWealth() - num;
	}

	private static float LedgerVirtualWealth(Map map)
	{
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (map == null || instance == null)
		{
			return 0f;
		}
		float num = 0f;
		List<SDS_StockEntry> list = instance.Snapshot(0);
		for (int i = 0; i < list.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = list[i];
			if (sDS_StockEntry == null || sDS_StockEntry.key == null || sDS_StockEntry.key.preserved || sDS_StockEntry.key.thingDef == null || sDS_StockEntry.count <= 0 || !sDS_StockEntry.key.thingDef.EverHaulable)
			{
				continue;
			}
			if (SDS_MirrorService.TryGetMirror(map, sDS_StockEntry.key, out var mirror))
			{
				if (mirror != null && mirror.Spawned && !GridsUtility.Fogged(mirror.PositionHeld, map))
				{
					num += mirror.MarketValue * (float)mirror.stackCount;
				}
				continue;
			}
			Thing val = SDS_OutputUtility.CachedDisplayThing(sDS_StockEntry.key);
			if (val != null)
			{
				num += val.MarketValue * (float)sDS_StockEntry.count;
			}
		}
		return num;
	}
}
public class SDS_Mod : Mod
{
	public static SDS_Settings Settings;

	private Vector2 settingsScroll = Vector2.zero;

	private const float SettingsContentHeight = 1000f;

	private static readonly FieldInfo basePowerField = AccessTools.Field(typeof(CompProperties_Power), "basePowerConsumption");

	private static readonly FieldInfo showPowerNeededField = AccessTools.Field(typeof(CompProperties_Power), "showPowerNeededIfOff");

	private static bool harmonyPatchesApplied;

	private static readonly HashSet<string> sharedPowerPropsChecked = new HashSet<string>();

	public SDS_Mod(ModContentPack content)
		: base(content)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Settings = ((Mod)this).GetSettings<SDS_Settings>();
		Settings.ClampAll();
		ApplyHarmonyPatches();
		LongEventHandler.ExecuteWhenFinished((Action)ApplySettings);
	}

	private static void ApplyHarmonyPatches()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		if (harmonyPatchesApplied)
		{
			return;
		}
		harmonyPatchesApplied = true;
		Harmony val = new Harmony("cagier.superdimensionalstorage");
		Type[] types = Assembly.GetExecutingAssembly().GetTypes();
		int num = 0;
		int num2 = 0;
		List<string> list = new List<string>();
		List<string> list2 = null;
		foreach (Type type in types)
		{
			object[] customAttributes = type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false);
			if (customAttributes != null && customAttributes.Length != 0)
			{
				num2++;
				try
				{
					val.CreateClassProcessor(type).Patch();
					num++;
					list.Add(type.Name);
				}
				catch (Exception ex)
				{
					(list2 ?? (list2 = new List<string>())).Add(type.Name);
					Log.Error("[超维存储] 补丁类 " + type.Name + " 应用失败，已跳过（其余补丁与 Mod 设置照常生效）：" + ex);
				}
			}
		}
		Log.Message("[超维存储] Harmony 补丁类应用完成：A/B = " + num + "/" + num2 + " 个。");
		if (list2 != null)
		{
			Log.Warning("[超维存储][补丁/应用失败] 失败 " + list2.Count + " 个：" + string.Join("，", list2.ToArray()) + "；已生效 " + num + " 个：" + string.Join("，", list.ToArray()));
		}
		SDS_ModCompat.TryApplyAll(val);
		SDS_UseCommand.ReportProvidersOnce();
	}

	public override string SettingsCategory()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		return ((object)Translator.Translate("SDS.Settings.Category")/*cast due to constrained. prefix*/).ToString();
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0332: Unknown result type (might be due to invalid IL or missing references)
		//IL_0337: Unknown result type (might be due to invalid IL or missing references)
		//IL_0367: Unknown result type (might be due to invalid IL or missing references)
		//IL_036c: Unknown result type (might be due to invalid IL or missing references)
		//IL_039c: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0423: Unknown result type (might be due to invalid IL or missing references)
		//IL_0428: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0487: Unknown result type (might be due to invalid IL or missing references)
		//IL_048c: Unknown result type (might be due to invalid IL or missing references)
		//IL_04aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ce: Unknown result type (might be due to invalid IL or missing references)
		SDS_Hotkeys.NotifySettingsPageDrawn();
		if (SDS_Hotkeys.Capturing)
		{
			DrawHotkeyCaptureOverlay(inRect);
			return;
		}
		Listing_Standard val = new Listing_Standard();
		Rect val2 = default(Rect);
		((Rect)(ref val2))..ctor(0f, 0f, Mathf.Max(0f, ((Rect)(ref inRect)).width - 18f), 1000f);
		Widgets.BeginScrollView(inRect, ref settingsScroll, val2, true);
		((Listing)val).Begin(val2);
		val.Label(((object)TranslatorFormattedStringExtensions.Translate("SDS.Settings.GridCapacity", NamedArgument.op_Implicit(Settings.gridCapacity), NamedArgument.op_Implicit(10), NamedArgument.op_Implicit(90))/*cast due to constrained. prefix*/).ToString(), -1f, (TipSignal?)null);
		Settings.gridCapacity = Mathf.RoundToInt(val.Slider((float)Settings.gridCapacity, 10f, 90f));
		((Listing)val).Gap(12f);
		val.Label(((object)TranslatorFormattedStringExtensions.Translate("SDS.Settings.CapacityPerArray", NamedArgument.op_Implicit(Settings.capacityPerArray), NamedArgument.op_Implicit(10), NamedArgument.op_Implicit(90))/*cast due to constrained. prefix*/).ToString(), -1f, (TipSignal?)null);
		Settings.capacityPerArray = Mathf.RoundToInt(val.Slider((float)Settings.capacityPerArray, 10f, 90f));
		((Listing)val).Gap(12f);
		val.Label(((object)TranslatorFormattedStringExtensions.Translate("SDS.Settings.CorePower", NamedArgument.op_Implicit(Mathf.RoundToInt(Settings.corePower)), NamedArgument.op_Implicit(0), NamedArgument.op_Implicit(2000))/*cast due to constrained. prefix*/).ToString(), -1f, (TipSignal?)null);
		Settings.corePower = val.Slider(Settings.corePower, 0f, 2000f);
		((Listing)val).Gap(12f);
		val.Label(((object)TranslatorFormattedStringExtensions.Translate("SDS.Settings.ArrayPower", NamedArgument.op_Implicit(Mathf.RoundToInt(Settings.arrayPower)), NamedArgument.op_Implicit(0), NamedArgument.op_Implicit(2000))/*cast due to constrained. prefix*/).ToString(), -1f, (TipSignal?)null);
		Settings.arrayPower = val.Slider(Settings.arrayPower, 0f, 2000f);
		((Listing)val).Gap(12f);
		val.Label(((object)TranslatorFormattedStringExtensions.Translate("SDS.Settings.PortPower", NamedArgument.op_Implicit(Mathf.RoundToInt(Settings.portPower)), NamedArgument.op_Implicit(0), NamedArgument.op_Implicit(100))/*cast due to constrained. prefix*/).ToString(), -1f, (TipSignal?)null);
		Settings.portPower = val.Slider(Settings.portPower, 0f, 100f);
		((Listing)val).Gap(12f);
		val.Label(((object)Translator.Translate("SDS.Settings.ZeroMeansNoPower")/*cast due to constrained. prefix*/).ToString(), -1f, (TipSignal?)null);
		((Listing)val).Gap(12f);
		val.CheckboxLabeled(((object)Translator.Translate("SDS.Settings.SeparateQuality")/*cast due to constrained. prefix*/).ToString(), ref Settings.separateByQuality, (string)null, 0f, 1f);
		val.CheckboxLabeled(((object)Translator.Translate("SDS.Settings.SeparateStuff")/*cast due to constrained. prefix*/).ToString(), ref Settings.separateByStuff, (string)null, 0f, 1f);
		val.Label(((object)Translator.Translate("SDS.Settings.SeparateDesc")/*cast due to constrained. prefix*/).ToString(), -1f, (TipSignal?)null);
		((Listing)val).Gap(12f);
		val.CheckboxLabeled(((object)Translator.Translate("SDS.Settings.ExcludeFromWealth")/*cast due to constrained. prefix*/).ToString(), ref Settings.excludeGridFromWealth, (string)null, 0f, 1f);
		((Listing)val).Gap(12f);
		bool highestStoragePriority = Settings.highestStoragePriority;
		val.CheckboxLabeled(((object)Translator.Translate("SDS.Settings.HighestStoragePriority")/*cast due to constrained. prefix*/).ToString(), ref Settings.highestStoragePriority, (string)null, 0f, 1f);
		if (highestStoragePriority != Settings.highestStoragePriority)
		{
			SDS_EndpointPriorityState.NotifyChanged();
			SDS_GameComponent_Grid.Instance?.ApplyStoragePriorityToEndpoints();
		}
		val.Label(((object)Translator.Translate("SDS.Settings.HighestStoragePriorityDesc")/*cast due to constrained. prefix*/).ToString(), -1f, (TipSignal?)null);
		DrawHotkeySection(val);
		((Listing)val).Gap(12f);
		if (val.ButtonText(((object)Translator.Translate("SDS.Settings.ResetDefaults")/*cast due to constrained. prefix*/).ToString(), (string)null, 1f))
		{
			Settings.ResetToDefaults();
			ApplySettings();
			RefreshWealth();
		}
		((Listing)val).End();
		Widgets.EndScrollView();
		Settings.ClampAll();
	}

	private static void DrawHotkeySection(Listing_Standard listing)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		((Listing)listing).Gap(12f);
		listing.Label(((object)Translator.Translate("SDS.Settings.Hotkeys")/*cast due to constrained. prefix*/).ToString(), -1f, (TipSignal?)null);
		DrawHotkeyRow(listing, "SDS.Settings.HotkeyOpen", SDS_KeyBindingDefOf.SDS_OpenSuperSpace);
		((Listing)listing).Gap(2f);
		DrawHotkeyRow(listing, "SDS.Settings.HotkeyOutput", SDS_KeyBindingDefOf.SDS_OutputFromSuperSpace);
		((Listing)listing).Gap(2f);
		DrawHotkeyRow(listing, "SDS.Settings.HotkeyUse", SDS_KeyBindingDefOf.SDS_UseFromSuperSpace);
		((Listing)listing).Gap(4f);
		if (listing.ButtonText(((object)Translator.Translate("SDS.Settings.HotkeyReset")/*cast due to constrained. prefix*/).ToString(), (string)null, 1f))
		{
			SDS_Hotkeys.ResetDefaults();
		}
		listing.Label(((object)Translator.Translate("SDS.Settings.HotkeyNote")/*cast due to constrained. prefix*/).ToString(), -1f, (TipSignal?)null);
	}

	private static void DrawHotkeyRow(Listing_Standard listing, string labelKey, KeyBindingDef def)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		Rect rect = ((Listing)listing).GetRect(30f, 1f);
		float num = Mathf.Max(120f, ((Rect)(ref rect)).width * 0.36f);
		Rect val = default(Rect);
		((Rect)(ref val))..ctor(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, num, ((Rect)(ref rect)).height);
		Rect val2 = default(Rect);
		((Rect)(ref val2))..ctor(((Rect)(ref val)).xMax, ((Rect)(ref rect)).y, 84f, ((Rect)(ref rect)).height);
		Rect val3 = default(Rect);
		((Rect)(ref val3))..ctor(((Rect)(ref val2)).xMax + 6f, ((Rect)(ref rect)).y, 58f, ((Rect)(ref rect)).height);
		Rect val4 = default(Rect);
		((Rect)(ref val4))..ctor(((Rect)(ref val3)).xMax + 6f, ((Rect)(ref rect)).y, 58f, ((Rect)(ref rect)).height);
		Widgets.Label(val, ((object)Translator.Translate(labelKey)/*cast due to constrained. prefix*/).ToString());
		Widgets.DrawBoxSolid(val2, new Color(0.15f, 0.15f, 0.15f));
		TextAnchor anchor = Text.Anchor;
		Text.Anchor = (TextAnchor)4;
		Widgets.Label(val2, SDS_Hotkeys.GetKeyLabel(def));
		Text.Anchor = anchor;
		if (Widgets.ButtonText(val3, ((object)Translator.Translate("SDS.Settings.HotkeySet")/*cast due to constrained. prefix*/).ToString(), true, true, true, (TextAnchor?)null))
		{
			SDS_Hotkeys.BeginCapture(def);
		}
		if (Widgets.ButtonText(val4, ((object)Translator.Translate("SDS.Settings.HotkeyClear")/*cast due to constrained. prefix*/).ToString(), true, true, true, (TextAnchor?)null))
		{
			SDS_Hotkeys.ClearBinding(def);
		}
	}

	private static void DrawHotkeyCaptureOverlay(Rect inRect)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0257: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		Widgets.DrawBoxSolid(inRect, new Color(0f, 0f, 0f, 0.55f));
		Rect val = default(Rect);
		((Rect)(ref val))..ctor(((Rect)(ref inRect)).center.x - 240f, ((Rect)(ref inRect)).center.y - 70f, 480f, 150f);
		Widgets.DrawBoxSolid(val, new Color(0.14f, 0.14f, 0.14f));
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		bool wordWrap = Text.WordWrap;
		try
		{
			Text.Font = (GameFont)1;
			Text.Anchor = (TextAnchor)4;
			Text.WordWrap = true;
			KeyBindingDef captureDef = SDS_Hotkeys.CaptureDef;
			string text = ((captureDef == SDS_KeyBindingDefOf.SDS_OpenSuperSpace) ? ((object)Translator.Translate("SDS.Settings.HotkeyOpen")/*cast due to constrained. prefix*/).ToString() : ((captureDef == SDS_KeyBindingDefOf.SDS_UseFromSuperSpace) ? ((object)Translator.Translate("SDS.Settings.HotkeyUse")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("SDS.Settings.HotkeyOutput")/*cast due to constrained. prefix*/).ToString()));
			Widgets.Label(new Rect(((Rect)(ref val)).x + 12f, ((Rect)(ref val)).y + 10f, ((Rect)(ref val)).width - 24f, 26f), ((object)TranslatorFormattedStringExtensions.Translate("SDS.Settings.HotkeyCaptureTitle", NamedArgument.op_Implicit(text))/*cast due to constrained. prefix*/).ToString());
			Widgets.Label(new Rect(((Rect)(ref val)).x + 12f, ((Rect)(ref val)).y + 40f, ((Rect)(ref val)).width - 24f, 42f), ((object)Translator.Translate("SDS.Settings.HotkeyCaptureHint")/*cast due to constrained. prefix*/).ToString());
			if (!GenText.NullOrEmpty(SDS_Hotkeys.CaptureNote))
			{
				Color color = GUI.color;
				GUI.color = new Color(1f, 0.7f, 0.4f);
				Widgets.Label(new Rect(((Rect)(ref val)).x + 12f, ((Rect)(ref val)).y + 84f, ((Rect)(ref val)).width - 24f, 24f), SDS_Hotkeys.CaptureNote);
				GUI.color = color;
			}
			if (Widgets.ButtonText(new Rect(((Rect)(ref val)).center.x - 60f, ((Rect)(ref val)).yMax - 38f, 120f, 28f), ((object)Translator.Translate("SDS.Settings.HotkeyCancel")/*cast due to constrained. prefix*/).ToString(), true, true, true, (TextAnchor?)null))
			{
				SDS_Hotkeys.CancelCapture();
			}
		}
		finally
		{
			Text.WordWrap = wordWrap;
			Text.Anchor = anchor;
			Text.Font = font;
		}
	}

	public override void WriteSettings()
	{
		SDS_Hotkeys.CancelCapture();
		Settings.ClampAll();
		ApplySettings();
		RefreshWealth();
		((Mod)this).WriteSettings();
	}

	private static void RefreshWealth()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		if ((int)Current.ProgramState != 2 || Find.Maps == null)
		{
			return;
		}
		for (int i = 0; i < Find.Maps.Count; i++)
		{
			Map val = Find.Maps[i];
			if (val != null && val.wealthWatcher != null)
			{
				val.wealthWatcher.ForceRecount(false);
			}
		}
	}

	public static void ApplySettings()
	{
		if (Settings != null)
		{
			Settings.ClampAll();
			ApplyPower("SDS_BuildingCore", Settings.corePower);
			ApplyPower("SDS_BuildingCapacityArray", Settings.arrayPower);
			ApplyPower("SDS_BuildingPort", Settings.portPower);
			SDS_GameComponent_Grid.Instance?.NotifyStructureChanged();
			SDS_GameComponent_Grid.Instance?.ApplyStoragePriorityToEndpoints();
		}
	}

	private static void ApplyPower(string defName, float watts)
	{
		ThingDef namedSilentFail = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
		if (namedSilentFail == null)
		{
			return;
		}
		CompProperties_Power compProperties = namedSilentFail.GetCompProperties<CompProperties_Power>();
		if (compProperties == null || basePowerField == null)
		{
			return;
		}
		if (IsPowerPropsSharedWithOtherDefs(compProperties, namedSilentFail))
		{
			Log.ErrorOnce("[超维存储] " + defName + " 的 CompProperties_Power 与其它 Def 共用同一实例，已跳过电力写入以免影响其它建筑。", defName.GetHashCode());
			return;
		}
		float num = Mathf.Max(0f, watts);
		basePowerField.SetValue(compProperties, num);
		if (showPowerNeededField != null)
		{
			showPowerNeededField.SetValue(compProperties, num > 0f);
		}
		RefreshSpawnedPowerComps(namedSilentFail);
	}

	private static void RefreshSpawnedPowerComps(ThingDef def)
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			return;
		}
		foreach (Map map in Find.Maps)
		{
			if (map == null || map.listerThings == null)
			{
				continue;
			}
			List<Thing> list = map.listerThings.ThingsOfDef(def);
			for (int i = 0; i < list.Count; i++)
			{
				CompPowerTrader val = ThingCompUtility.TryGetComp<CompPowerTrader>(list[i]);
				if (val != null)
				{
					((CompPower)val).SetUpPowerVars();
				}
			}
		}
	}

	private static bool IsPowerPropsSharedWithOtherDefs(CompProperties_Power power, ThingDef owner)
	{
		if (sharedPowerPropsChecked.Contains(((Def)owner).defName))
		{
			return false;
		}
		sharedPowerPropsChecked.Add(((Def)owner).defName);
		List<ThingDef> allDefsListForReading = DefDatabase<ThingDef>.AllDefsListForReading;
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			ThingDef val = allDefsListForReading[i];
			if (val == null || val == owner || val.comps == null)
			{
				continue;
			}
			for (int j = 0; j < val.comps.Count; j++)
			{
				if ((object)val.comps[j] == power)
				{
					return true;
				}
			}
		}
		return false;
	}
}
public class SDS_Settings : ModSettings
{
	public int gridCapacity = 30;

	public int capacityPerArray = 30;

	public float corePower = 1000f;

	public float arrayPower = 1000f;

	public float portPower = 10f;

	public bool excludeGridFromWealth;

	public bool highestStoragePriority;

	public int endpointStoragePriority = 2;

	public bool separateByQuality = true;

	public bool separateByStuff = true;

	public override void ExposeData()
	{
		Scribe_Values.Look<int>(ref gridCapacity, "gridCapacity", 30, false);
		Scribe_Values.Look<int>(ref capacityPerArray, "capacityPerArray", 30, false);
		Scribe_Values.Look<float>(ref corePower, "corePower", 1000f, false);
		Scribe_Values.Look<float>(ref arrayPower, "arrayPower", 1000f, false);
		Scribe_Values.Look<float>(ref portPower, "portPower", 10f, false);
		Scribe_Values.Look<bool>(ref excludeGridFromWealth, "excludeGridFromWealth", false, false);
		Scribe_Values.Look<bool>(ref highestStoragePriority, "highestStoragePriority", false, false);
		Scribe_Values.Look<int>(ref endpointStoragePriority, "endpointStoragePriority", 2, false);
		Scribe_Values.Look<bool>(ref separateByQuality, "separateByQuality", true, false);
		Scribe_Values.Look<bool>(ref separateByStuff, "separateByStuff", true, false);
	}

	public void ResetToDefaults()
	{
		gridCapacity = 30;
		capacityPerArray = 30;
		corePower = 1000f;
		arrayPower = 1000f;
		portPower = 10f;
		excludeGridFromWealth = false;
		highestStoragePriority = false;
		endpointStoragePriority = 2;
		separateByQuality = true;
		separateByStuff = true;
		ClampAll();
		SDS_EndpointPriorityState.NotifyChanged();
	}

	public void ClampAll()
	{
		gridCapacity = SDS_Config.ClampGridCapacity(gridCapacity);
		capacityPerArray = SDS_Config.ClampCapacityPerArray(capacityPerArray);
		corePower = SDS_Config.Clamp(corePower, 0f, 2000f);
		arrayPower = SDS_Config.Clamp(arrayPower, 0f, 2000f);
		portPower = SDS_Config.Clamp(portPower, 0f, 100f);
		endpointStoragePriority = SDS_Config.Clamp(endpointStoragePriority, 1, 5);
	}
}
public static class SDS_Config
{
	public const int GridCapacityDefault = 30;

	public const int GridCapacityMin = 10;

	public const int GridCapacityMax = 90;

	public const int CapacityPerArrayDefault = 30;

	public const int CapacityPerArrayMin = 10;

	public const int CapacityPerArrayMax = 90;

	public const float CorePowerDefault = 1000f;

	public const float CorePowerMin = 0f;

	public const float CorePowerMax = 2000f;

	public const float ArrayPowerDefault = 1000f;

	public const float ArrayPowerMin = 0f;

	public const float ArrayPowerMax = 2000f;

	public const float PortPowerDefault = 10f;

	public const float PortPowerMin = 0f;

	public const float PortPowerMax = 100f;

	public const string CoreDefName = "SDS_BuildingCore";

	public const string ArrayDefName = "SDS_BuildingCapacityArray";

	public const string PortDefName = "SDS_BuildingPort";

	public static SDS_Settings Settings => SDS_Mod.Settings;

	public static int GridCapacity => (Settings != null) ? Settings.gridCapacity : 30;

	public static int CapacityPerArray => (Settings != null) ? Settings.capacityPerArray : 30;

	public static float CorePower => (Settings != null) ? Settings.corePower : 1000f;

	public static float ArrayPower => (Settings != null) ? Settings.arrayPower : 1000f;

	public static float PortPower => (Settings != null) ? Settings.portPower : 10f;

	public static bool ExcludeGridFromWealth => Settings != null && Settings.excludeGridFromWealth;

	public static bool SeparateByQuality => Settings == null || Settings.separateByQuality;

	public static bool SeparateByStuff => Settings == null || Settings.separateByStuff;

	public static StoragePriority CoreStoragePriority
	{
		get
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			int value = ((Settings != null) ? Settings.endpointStoragePriority : 2);
			return (StoragePriority)(byte)Clamp(value, 1, 5);
		}
	}

	public static bool IgnorePriorityMode => Settings != null && Settings.highestStoragePriority;

	public static void SetCoreStoragePriority(StoragePriority value)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected I4, but got Unknown
		if (Settings != null)
		{
			int num = Clamp((int)value, 1, 5);
			if (Settings.endpointStoragePriority != num)
			{
				Settings.endpointStoragePriority = num;
				SDS_EndpointPriorityState.NotifyChanged();
				SDS_GameComponent_Grid.Instance?.ApplyStoragePriorityToEndpoints();
			}
		}
	}

	public static int ClampGridCapacity(int value)
	{
		return Clamp(value, 10, 90);
	}

	public static int ClampCapacityPerArray(int value)
	{
		return Clamp(value, 10, 90);
	}

	public static int Clamp(int value, int min, int max)
	{
		if (value < min)
		{
			return min;
		}
		return (value > max) ? max : value;
	}

	public static float Clamp(float value, float min, float max)
	{
		if (value < min)
		{
			return min;
		}
		return (value > max) ? max : value;
	}
}
public static class SDS_ResearchUtility
{
	public const string AdvancedPrecisionName = "SDS_AdvancedPrecisionSuperStorage";

	private static bool lookedUp;

	private static ResearchProjectDef advancedPrecision;

	public static ResearchProjectDef AdvancedPrecision
	{
		get
		{
			EnsureLookup();
			return advancedPrecision;
		}
	}

	public static bool CrossMapUnlocked
	{
		get
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Invalid comparison between Unknown and I4
			ResearchProjectDef val = AdvancedPrecision;
			if (val == null)
			{
				return false;
			}
			if ((int)Current.ProgramState != 2 || Find.ResearchManager == null)
			{
				return false;
			}
			return val.IsFinished;
		}
	}

	private static void EnsureLookup()
	{
		if (!lookedUp)
		{
			lookedUp = true;
			advancedPrecision = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("SDS_AdvancedPrecisionSuperStorage");
			if (advancedPrecision == null)
			{
				Log.Error("[超维存储] 找不到 ResearchProjectDef SDS_AdvancedPrecisionSuperStorage：跨地图能力（超维空间高级芯片 的跨地图访问 / 跨地图携带物资）不会启用（Game 可正常运行）。请检查 Defs/SDS_Research.xml 是否随 Mod 一起安装。");
			}
		}
	}
}
internal static class SDS_Diag
{
	private sealed class FailState
	{
		public int count;

		public int nextTick;
	}

	private const int RepeatSuppressTicks = 600;

	private static readonly Dictionary<string, int> seen = new Dictionary<string, int>();

	private static readonly Dictionary<string, FailState> fails = new Dictionary<string, FailState>();

	private static readonly HashSet<string> warned = new HashSet<string>();

	public static void ErrorOnce(string message, Exception exception)
	{
		if (message == null)
		{
			return;
		}
		string text = CurrentCaller(2);
		string key = message + "|" + text + "|" + ((exception != null) ? exception.GetType().FullName : "none");
		if (seen.TryGetValue(key, out var value))
		{
			value++;
			seen[key] = value;
			if (value == 10 || value == 100 || value == 1000)
			{
				Log.Warning("[超维存储] 重复错误第 " + value + " 次（" + text + "）：" + message + ((exception != null) ? ("\n" + exception) : string.Empty));
			}
		}
		else
		{
			seen[key] = 1;
			Log.Error("[超维存储] " + message + "（调用点：" + text + "）" + ((exception != null) ? ("\n" + exception) : string.Empty));
		}
	}

	public static void Fail(string reason, string context = null, Exception exception = null)
	{
		if (ShouldLogFail(reason))
		{
			Write(reason, context, exception, error: true);
		}
	}

	public static void Warn(string reason, string context = null, Exception exception = null)
	{
		if (!string.IsNullOrEmpty(reason) && warned.Add(reason))
		{
			Write(reason, context, exception, error: false);
		}
	}

	private static bool ShouldLogFail(string reason)
	{
		if (string.IsNullOrEmpty(reason))
		{
			return false;
		}
		int ticksGame = GenTicks.TicksGame;
		if (!fails.TryGetValue(reason, out var value))
		{
			value = new FailState
			{
				nextTick = ticksGame
			};
			fails[reason] = value;
		}
		value.count++;
		bool flag = value.count == 10 || value.count == 100 || value.count == 1000;
		if (ticksGame < value.nextTick && !flag)
		{
			return false;
		}
		value.nextTick = ticksGame + 600;
		return true;
	}

	public static string Ctx(Map map = null, Pawn pawn = null, Thing thing = null, Job job = null, string extra = null)
	{
		StringBuilder stringBuilder = new StringBuilder(96);
		if (map != null)
		{
			stringBuilder.Append("map=").Append(map.uniqueID);
		}
		if (pawn != null)
		{
			stringBuilder.Append((stringBuilder.Length > 0) ? " " : string.Empty).Append("pawn=").Append(((Entity)pawn).LabelShort)
				.Append('#')
				.Append(((Thing)pawn).thingIDNumber);
		}
		if (thing != null)
		{
			stringBuilder.Append((stringBuilder.Length > 0) ? " " : string.Empty).Append("thing=").Append((thing.def != null) ? ((Def)thing.def).defName : "?")
				.Append('#')
				.Append(thing.thingIDNumber);
			if (thing.stackCount > 1)
			{
				stringBuilder.Append('x').Append(thing.stackCount);
			}
		}
		if (job != null)
		{
			stringBuilder.Append((stringBuilder.Length > 0) ? " " : string.Empty).Append("job=").Append((job.def != null) ? ((Def)job.def).defName : "?")
				.Append('#')
				.Append(job.loadID);
		}
		if (!string.IsNullOrEmpty(extra))
		{
			stringBuilder.Append((stringBuilder.Length > 0) ? " " : string.Empty).Append(extra);
		}
		return stringBuilder.ToString();
	}

	private static void Write(string reason, string context, Exception exception, bool error)
	{
		if (!string.IsNullOrEmpty(reason))
		{
			fails.TryGetValue(reason, out var value);
			int num = value?.count ?? 1;
			bool flag = num == 10 || num == 100 || num == 1000;
			string text = CurrentCaller(3);
			string text2 = "[超维存储][" + reason + "]" + (string.IsNullOrEmpty(context) ? string.Empty : (" " + context)) + (flag ? ("（第 " + num + " 次）") : string.Empty) + "（调用点：" + text + "）" + ((exception != null) ? ("\n" + exception) : string.Empty);
			if (error)
			{
				Log.Error(text2);
			}
			else
			{
				Log.Warning(text2);
			}
		}
	}

	private static string CurrentCaller(int skip)
	{
		try
		{
			MethodBase method = new StackFrame(skip, needFileInfo: false).GetMethod();
			if (method == null)
			{
				return "?";
			}
			Type declaringType = method.DeclaringType;
			return ((declaringType != null) ? (declaringType.FullName + ".") : string.Empty) + method.Name;
		}
		catch (Exception)
		{
			return "?";
		}
	}
}
internal static class SDS_ModCompat
{
	public static void TryApplyAll(Harmony harmony)
	{
		if (harmony != null)
		{
			SDS_CompatReportHub.Reset();
			ApplyOne("PickUp and Haul", delegate
			{
				SDS_ModCompat_PUAH.TryApply(harmony);
			});
			ApplyOne("Combat Extended", delegate
			{
				SDS_ModCompat_CE.TryApply(harmony);
			});
			ApplyOne("联合轻工（UFRecipes）", delegate
			{
				SDS_ModCompat_UFRecipes.TryApply(harmony);
			});
			ApplyOne("米莉拉未来科技（Milira Expanded）", delegate
			{
				SDS_ModCompat_MiliraExpanded.TryApply(harmony);
			});
			ApplyOne("等价交换（ProjectE）", delegate
			{
				SDS_ModCompat_EquivalentExchange.TryApply(harmony);
			});
			ApplyOne("牵引光束搬运器（ManipulatorBeam）", delegate
			{
				SDS_ModCompat_ManipulatorBeam.TryApply(harmony);
			});
			SDS_CompatReportHub.LogAll();
		}
	}

	private static void ApplyOne(string name, Action apply)
	{
		try
		{
			apply?.Invoke();
		}
		catch (Exception ex)
		{
			if (!SDS_CompatReportHub.Has(name))
			{
				SDS_CompatReportHub.Register(name, null, detected: true, null, null, "适配器异常 " + ex.GetType().Name);
			}
			SDS_Diag.ErrorOnce("第三方适配器执行失败（已跳过）：" + name, ex);
		}
	}
}
internal static class SDS_ModCompat_CE
{
	private static Type tCompAmmoUser;

	private static Type tCompInventory;

	private static Type tAmmoDef;

	private static Type tJobGiverUtilsReload;

	private static Type tBuildingAutoloaderCe;

	private static Type tTurretRegistry;

	private static MethodInfo mGetAmmo;

	private static MethodInfo mGetReloadable;

	private static MethodInfo mGetReloading;

	private static MethodInfo mFindBestAmmoTurret;

	private static MethodInfo mFindBestAmmoAutoloader;

	private static Type tCeJobDefOf;

	private static FieldInfo fReloadTurretDef;

	private static FieldInfo fReloadAutoLoaderDef;

	private static FieldInfo fReloadWeaponDef;

	private static readonly FieldInfo fJobTrackerPawn = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");

	private static Thing pendingAmmo;

	private static Thing pendingAmmoTarget;

	private static ThingDef pendingAmmoDef;

	private static readonly Dictionary<int, int> checkCooldownUntil = new Dictionary<int, int>();

	private static readonly Dictionary<int, int> priorityCacheUntil = new Dictionary<int, int>();

	private static readonly Dictionary<int, bool> priorityCacheValue = new Dictionary<int, bool>();

	private const int CheckCooldownTicks = 250;

	private const int PriorityCacheTicks = 60;

	private const int NegativeCacheTicks = 10;

	internal static void Reset()
	{
		checkCooldownUntil.Clear();
		priorityCacheUntil.Clear();
		priorityCacheValue.Clear();
	}

	public static void TryApply(Harmony harmony)
	{
		bool flag = false;
		try
		{
			if (harmony != null)
			{
				if (!Resolve())
				{
					flag = tCompAmmoUser != null || tCompInventory != null || tAmmoDef != null || tJobGiverUtilsReload != null;
					string detectedButBroken = (flag ? ("CE 关键类型解析不完整：CompAmmoUser=" + (tCompAmmoUser != null) + "，CompInventory=" + (tCompInventory != null) + "，AmmoDef=" + (tAmmoDef != null) + "，JobGiverUtils_Reload=" + (tJobGiverUtilsReload != null)) : null);
					SDS_CompatReportHub.Register("Combat Extended", "CETeam.CombatExtended", flag, null, null, detectedButBroken);
					return;
				}
				flag = true;
				SDS_CompatPatchReport report = new SDS_CompatPatchReport("Combat Extended");
				Type[] signature = new Type[3]
				{
					typeof(Pawn),
					typeof(Thing),
					typeof(bool)
				};
				Type[] signature2 = new Type[2]
				{
					typeof(Pawn),
					typeof(Thing)
				};
				Type target = AccessTools.TypeByName("CombatExtended.WorkGiver_ReloadTurret");
				Patch(harmony, report, "WorkGiver_ReloadTurret", target, "HasJobOnThing", signature, null, "ReloadTurretHasJobPostfix");
				Patch(harmony, report, "WorkGiver_ReloadTurret", target, "JobOnThing", signature, "ReloadTurretJobPrefix", "ReloadTurretJobPostfix");
				Type target2 = AccessTools.TypeByName("CombatExtended.WorkGiver_ReloadAutoLoader");
				Patch(harmony, report, "WorkGiver_ReloadAutoLoader", target2, "HasJobOnThing", signature, null, "ReloadAutoLoaderHasJobPostfix");
				Patch(harmony, report, "WorkGiver_ReloadAutoLoader", target2, "JobOnThing", signature, "ReloadAutoLoaderJobPrefix", "ReloadAutoLoaderJobPostfix");
				Type target3 = AccessTools.TypeByName("CombatExtended.JobGiver_ManTurretsNearSelfCE");
				Patch(harmony, report, "JobGiver_ManTurretsNearSelfCE", target3, "FindAmmoForTurret", signature2, null, "ManTurretsFindAmmoPostfix");
				Type target4 = AccessTools.TypeByName("CombatExtended.JobGiver_ManTurretsNearPointCE");
				Patch(harmony, report, "JobGiver_ManTurretsNearPointCE", target4, "FindAmmoForTurret", signature2, null, "ManTurretsFindAmmoPostfix");
				Type target5 = AccessTools.TypeByName("CombatExtended.JobGiver_CheckReload");
				Patch(harmony, report, "JobGiver_CheckReload", target5, "GetPriority", new Type[1] { typeof(Pawn) }, null, "CheckReloadPriorityPostfix");
				Patch(harmony, report, "JobGiver_CheckReload", target5, "TryGiveJob", new Type[1] { typeof(Pawn) }, "CheckReloadJobPrefix", "CheckReloadJobPostfix");
				Patch(harmony, report, "Pawn_JobTracker", typeof(Pawn_JobTracker), "JobTrackerTick", Type.EmptyTypes, null, "JobTrackerTickPostfix");
				Type target6 = AccessTools.TypeByName("CombatExtended.JobGiver_DefenderReloadTurret");
				Patch(harmony, report, "JobGiver_DefenderReloadTurret", target6, "TryGiveJob", new Type[1] { typeof(Pawn) }, "DefenderReloadPrefix", null);
				Patch(harmony, report, "CompAmmoUser", tCompAmmoUser, "TryPickupAmmo", Type.EmptyTypes, null, "TryPickupAmmoPostfix");
				Patch(harmony, report, "CompAmmoUser", tCompAmmoUser, "TryPrepareShot", Type.EmptyTypes, "TryPrepareShotPrefix", null);
				Patch(harmony, report, "JobGiverUtils_Reload", tJobGiverUtilsReload, "FindBestAmmo", new Type[2]
				{
					typeof(Pawn),
					tAmmoDef
				}, null, "FindBestAmmoMirrorFilterPostfix");
				Type target7 = AccessTools.TypeByName("CombatExtended.JobDriver_ReloadTurret");
				Patch(harmony, report, "JobDriver_ReloadTurret", target7, "TryMakePreToilReservations", new Type[1] { typeof(bool) }, "ReloadDriverMirrorGuardPrefix", null);
				Type target8 = AccessTools.TypeByName("CombatExtended.JobDriver_ReloadAutoLoader");
				Patch(harmony, report, "JobDriver_ReloadAutoLoader", target8, "TryMakePreToilReservations", new Type[1] { typeof(bool) }, "ReloadDriverMirrorGuardPrefix", null);
				SDS_CompatReportHub.Register("Combat Extended", "CETeam.CombatExtended", detected: true, report, "只影响 CE 自己的换弹 / 炮塔 / 拾取链路，不改变 CE 的任何表现");
			}
		}
		catch (Exception ex)
		{
			SDS_CompatReportHub.Register("Combat Extended", "CETeam.CombatExtended", flag, null, null, "应用异常 " + ex.GetType().Name);
			SDS_Diag.ErrorOnce("Combat Extended 兼容补丁应用失败（已跳过，双方原行为不受影响）", ex);
		}
	}

	private static bool Resolve()
	{
		tCompAmmoUser = AccessTools.TypeByName("CombatExtended.CompAmmoUser");
		tCompInventory = AccessTools.TypeByName("CombatExtended.CompInventory");
		tAmmoDef = AccessTools.TypeByName("CombatExtended.AmmoDef");
		tJobGiverUtilsReload = AccessTools.TypeByName("CombatExtended.CombatExtended.Jobs.Utils.JobGiverUtils_Reload");
		tBuildingAutoloaderCe = AccessTools.TypeByName("CombatExtended.Building_AutoloaderCE");
		tTurretRegistry = AccessTools.TypeByName("CombatExtended.Compatibility.TurretRegistry");
		if (tCompAmmoUser == null || tCompInventory == null || tAmmoDef == null || tJobGiverUtilsReload == null)
		{
			return false;
		}
		if (tTurretRegistry != null)
		{
			mGetAmmo = AccessTools.Method(tTurretRegistry, "GetAmmo", new Type[1] { typeof(Building_Turret) }, (Type[])null);
			mGetReloadable = AccessTools.Method(tTurretRegistry, "GetReloadable", new Type[1] { typeof(Building_Turret) }, (Type[])null);
			mGetReloading = AccessTools.Method(tTurretRegistry, "GetReloading", new Type[1] { typeof(Building_Turret) }, (Type[])null);
		}
		if (tBuildingAutoloaderCe != null)
		{
			mFindBestAmmoAutoloader = AccessTools.Method(tJobGiverUtilsReload, "FindBestAmmo", new Type[2]
			{
				typeof(Pawn),
				tBuildingAutoloaderCe
			}, (Type[])null);
		}
		mFindBestAmmoTurret = AccessTools.Method(tJobGiverUtilsReload, "FindBestAmmo", new Type[2]
		{
			typeof(Pawn),
			typeof(Building_Turret)
		}, (Type[])null);
		tCeJobDefOf = AccessTools.TypeByName("CombatExtended.CE_JobDefOf");
		if (tCeJobDefOf != null)
		{
			fReloadTurretDef = AccessTools.Field(tCeJobDefOf, "ReloadTurret");
			fReloadAutoLoaderDef = AccessTools.Field(tCeJobDefOf, "ReloadAutoLoader");
			fReloadWeaponDef = AccessTools.Field(tCeJobDefOf, "ReloadWeapon");
		}
		return true;
	}

	private static void Patch(Harmony harmony, SDS_CompatPatchReport report, string targetName, Type target, string methodName, Type[] signature, string prefixName, string postfixName)
	{
		string hook = targetName + "." + methodName;
		if (target == null)
		{
			report.Missing(hook, "找不到类型 " + targetName + "（版本差异？）");
			return;
		}
		MethodInfo target2 = ((signature != null && signature.Length != 0) ? AccessTools.Method(target, methodName, signature, (Type[])null) : AccessTools.Method(target, methodName, Type.EmptyTypes, (Type[])null));
		SDS_CompatTools.TryPatch(harmony, report, typeof(SDS_ModCompat_CE), hook, target2, prefixName, postfixName, "找不到方法（签名变化？）");
	}

	public static void ReloadTurretHasJobPostfix(Pawn pawn, Thing t, bool forced, ref bool __result)
	{
		try
		{
			if (!__result && t is Building_Turret && CanSupplyAmmoFor(pawn, t, forced) && CanReserveTurret(pawn, t))
			{
				__result = true;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（炮塔 HasJobOnThing）失败（已忽略，CE 行为不受影响）", exception);
		}
	}

	public static bool ReloadTurretJobPrefix(Pawn pawn, Thing t, bool forced)
	{
		return ReloadJobPrefix(pawn, t, autoLoader: false);
	}

	public static void ReloadTurretJobPostfix(Pawn pawn, Thing t, bool forced, ref Job __result)
	{
		ReloadJobPostfix(pawn, t, autoLoader: false, ref __result);
	}

	public static void ReloadAutoLoaderHasJobPostfix(Pawn pawn, Thing t, bool forced, ref bool __result)
	{
		try
		{
			if (!__result && !(tBuildingAutoloaderCe == null) && tBuildingAutoloaderCe.IsInstanceOfType(t) && CanSupplyAmmoFor(pawn, t, forced) && CanReserveTurret(pawn, t))
			{
				__result = true;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（装弹器 HasJobOnThing）失败（已忽略，CE 行为不受影响）", exception);
		}
	}

	public static bool ReloadAutoLoaderJobPrefix(Pawn pawn, Thing t, bool forced)
	{
		return ReloadJobPrefix(pawn, t, autoLoader: true);
	}

	public static void ReloadAutoLoaderJobPostfix(Pawn pawn, Thing t, bool forced, ref Job __result)
	{
		ReloadJobPostfix(pawn, t, autoLoader: true, ref __result);
	}

	public static void ManTurretsFindAmmoPostfix(Pawn pawn, Thing turret, ref Thing __result)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__result == null && pawn != null && turret != null && ((Thing)pawn).Map != null && ((Thing)pawn).Faction == Faction.OfPlayer && GridUsable(((Thing)pawn).Map) && NeedAmmoFor(turret, out var def, out var need))
			{
				Thing val = MaterializeToMap(def, need, turret.Map, turret.Position);
				if (val != null)
				{
					__result = val;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（派人操作炮塔取弹）失败（已忽略，CE 行为不受影响）", exception);
		}
	}

	public static bool DefenderReloadPrefix(Pawn pawn)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (pawn == null || ((Thing)pawn).Map == null || ((Thing)pawn).Faction != Faction.OfPlayer)
			{
				return true;
			}
			Thing val = FindCeTurretNeedingReload(pawn);
			if (val == null)
			{
				return true;
			}
			if (!NeedAmmoFor(val, out var def, out var need))
			{
				return true;
			}
			MaterializeToMap(def, need, ((Thing)pawn).Map, val.Position);
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（防御职责炮塔补弹）失败（已忽略，CE 行为不受影响）", exception);
			return true;
		}
	}

	private static Thing FindCeTurretNeedingReload(Pawn pawn)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Invalid comparison between Unknown and I4
		List<Thing> list = ((Thing)pawn).Map.listerThings.ThingsInGroup((ThingRequestGroup)10);
		Thing result = null;
		float num = float.MaxValue;
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if (!(val is Building_Turret) || val.Destroyed || !val.Spawned)
			{
				continue;
			}
			float num2 = IntVec3Utility.DistanceTo(val.Position, ((Thing)pawn).Position);
			if (num2 > 100f || num2 >= num || !ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(val), 1, -1, (ReservationLayerDef)null, false) || !ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit(val), (PathEndMode)3, (Danger)3, false, false, (TraverseMode)0) || ForbidUtility.IsForbidden(val, pawn) || InvokeStaticBool(mGetReloading, val))
			{
				continue;
			}
			if (((Thing)pawn).Faction != null && val.Faction != ((Thing)pawn).Faction)
			{
				Faction faction = val.Faction;
				if (faction == null || (int)faction.RelationKindWith(((Thing)pawn).Faction) != 2)
				{
					continue;
				}
			}
			if (pawn.health != null && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && ShouldReloadTarget(val) && NeedAmmoFor(val, out var _, out var _))
			{
				result = val;
				num = num2;
			}
		}
		return result;
	}

	private static bool ShouldReloadTarget(Thing turret)
	{
		object obj = AmmoCompOf(turret);
		if (obj == null)
		{
			return false;
		}
		bool flag = BoolMember(obj, "HasMagazine");
		int num = IntMember(obj, "CurMagCount");
		int num2 = IntMember(obj, "MagSize");
		object member = GetMember(obj, "CurrentAmmo");
		ThingDef val = (ThingDef)((member is ThingDef) ? member : null);
		object member2 = GetMember(obj, "SelectedAmmo");
		ThingDef val2 = (ThingDef)((member2 is ThingDef) ? member2 : null);
		if (flag && num2 > 0 && (float)num <= (float)num2 * 0.5f)
		{
			return true;
		}
		return val != val2;
	}

	public static void CheckReloadPriorityPostfix(Pawn pawn, ref float __result)
	{
		try
		{
			if (!(__result > 0f) && CanConsiderCheckReload(pawn))
			{
				__result = 9.1f;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（换弹优先级）失败（已忽略，CE 行为不受影响）", exception);
		}
	}

	public static void CheckReloadJobPostfix(Pawn pawn, ref Job __result)
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__result != null || !CanConsiderCheckReload(pawn) || !NeedAmmoForPawn(pawn, out var def, out var need))
			{
				return;
			}
			if (SDS_ChipManualDeposit.PawnCanDeposit(pawn))
			{
				Job val = InvokeTryMakeReloadJob(pawn, def);
				if (val != null && MaterializeIntoInventory(pawn, def, need) > 0)
				{
					NotifyChecked(pawn);
					__result = val;
				}
				return;
			}
			Thing val2 = MaterializeToMap(def, need, ((Thing)pawn).Map, WithdrawCellFor(pawn, null));
			if (val2 != null)
			{
				Job val3 = JobMaker.MakeJob(JobDefOf.TakeInventory, LocalTargetInfo.op_Implicit(val2));
				val3.count = Math.Min(val2.stackCount, need);
				NotifyChecked(pawn);
				__result = val3;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（手持武器换弹取货）失败（已忽略，CE 行为不受影响）", exception);
		}
	}

	public static bool CheckReloadJobPrefix(Pawn pawn, ref Job __result)
	{
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__result != null)
			{
				return true;
			}
			if (pawn == null || ((Thing)pawn).Destroyed || ((Thing)pawn).Map == null || pawn.Dead || pawn.Downed || pawn.InMentalState || ((Thing)pawn).Faction != Faction.OfPlayer || !pawn.Drafted)
			{
				return true;
			}
			if (!GridUsable(((Thing)pawn).Map) || IsOnCheckCooldown(pawn) || IsCeReloadWeaponJob(pawn.CurJobDef))
			{
				return true;
			}
			object comp = GetComp((Thing)(object)((pawn.equipment != null) ? pawn.equipment.Primary : null), tCompAmmoUser);
			if (comp == null || !BoolMember(comp, "HasMagazine") || !BoolMember(comp, "UseAmmo"))
			{
				return true;
			}
			int num = IntMember(comp, "CurMagCount");
			int num2 = IntMember(comp, "MagSize");
			if (num2 <= 0 || num >= num2 || num > IntMember(comp, "TryReloadOn"))
			{
				return true;
			}
			if (!BoolMember(comp, "IsOpportunisticReloadActive") || GenTicks.TicksGame - pawn.LastAttackTargetTick < IntMember(comp, "MinimalTicksAfterFight") || HasHostileWithin(pawn, FloatMember(comp, "SafeDistanceToReload")))
			{
				return true;
			}
			if (!TryFindSupplyAmmo(comp, allowAmmoSetFallback: false, out var supplyDef, out var need, out var _))
			{
				return true;
			}
			if (SDS_ChipManualDeposit.PawnCanDeposit(pawn))
			{
				if (MaterializeIntoInventory(pawn, supplyDef, need) > 0)
				{
					NotifyChecked(pawn);
				}
				return true;
			}
			Thing val = MaterializeToMap(supplyDef, need, ((Thing)pawn).Map, WithdrawCellFor(pawn, null));
			if (val == null)
			{
				return true;
			}
			Job val2 = JobMaker.MakeJob(JobDefOf.TakeInventory, LocalTargetInfo.op_Implicit(val));
			val2.count = Math.Min(val.stackCount, need);
			NotifyChecked(pawn);
			__result = val2;
			return false;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（征兆空匣装弹）失败（已忽略，CE 行为不受影响）", exception);
			return true;
		}
	}

	private static bool IsCeReloadWeaponJob(JobDef def)
	{
		if (def == null || fReloadWeaponDef == null || tCeJobDefOf == null)
		{
			return false;
		}
		try
		{
			object? value = fReloadWeaponDef.GetValue(null);
			return def == ((value is JobDef) ? value : null);
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool HasHostileWithin(Pawn pawn, float distance)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null || ((Thing)pawn).Map == null || ((Thing)pawn).Map.mapPawns == null || distance <= 0f)
		{
			return false;
		}
		IReadOnlyList<Pawn> allPawnsSpawned = ((Thing)pawn).Map.mapPawns.AllPawnsSpawned;
		for (int i = 0; i < allPawnsSpawned.Count; i++)
		{
			Pawn val = allPawnsSpawned[i];
			if (val != null)
			{
				IntVec3 position = ((Thing)val).Position;
				if (((IntVec3)(ref position)).InHorDistOf(((Thing)pawn).Position, distance) && !InvisibilityUtility.IsPsychologicallyInvisible(val) && GenHostility.HostileTo((Thing)(object)val, (Thing)(object)pawn))
				{
					return true;
				}
			}
		}
		return false;
	}

	public static void JobTrackerTickPostfix(Pawn_JobTracker __instance)
	{
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			object obj;
			if (__instance == null || !(fJobTrackerPawn != null))
			{
				obj = null;
			}
			else
			{
				object? value = fJobTrackerPawn.GetValue(__instance);
				obj = ((value is Pawn) ? value : null);
			}
			Pawn val = (Pawn)obj;
			if (val == null || ((Thing)val).Destroyed || ((Thing)val).Map == null || val.Dead || val.Downed || val.InMentalState || ((Thing)val).Faction != Faction.OfPlayer || !val.Drafted || val.CurJobDef != JobDefOf.AttackStatic)
			{
				return;
			}
			object comp = GetComp((Thing)(object)((val.equipment != null) ? val.equipment.Primary : null), tCompAmmoUser);
			if (comp == null || !BoolMember(comp, "HasMagazine") || !BoolMember(comp, "UseAmmo") || IntMember(comp, "CurMagCount") > 0 || !GridUsable(((Thing)val).Map) || IsOnCheckCooldown(val) || !TryFindSupplyAmmo(comp, allowAmmoSetFallback: false, out var supplyDef, out var need, out var _))
			{
				return;
			}
			if (SDS_ChipManualDeposit.PawnCanDeposit(val))
			{
				if (MaterializeIntoInventory(val, supplyDef, need) > 0)
				{
					NotifyChecked(val);
					StartCeReloadJob(val, supplyDef);
				}
				return;
			}
			Job val2 = InvokeTryMakeReloadJob(val, supplyDef);
			if (val2 != null)
			{
				Thing val3 = MaterializeToMap(supplyDef, need, ((Thing)val).Map, WithdrawCellFor(val, null));
				if (val3 != null)
				{
					Job val4 = JobMaker.MakeJob(JobDefOf.TakeInventory, LocalTargetInfo.op_Implicit(val3));
					val4.count = Math.Min(val3.stackCount, need);
					NotifyChecked(val);
					val.jobs.SuspendCurrentJob((JobCondition)16, true, (bool?)null);
					val.jobs.jobQueue.EnqueueFirst(val2, (JobTag?)null);
					val.jobs.StartJob(val4, (JobCondition)16, (ThinkNode)null, false, true, (ThinkTreeDef)null, (JobTag?)null, false, false, (bool?)null, false, true, false);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（攻击中空匣接管）失败（已忽略，CE 行为不受影响）", exception);
		}
	}

	private static void StartCeReloadJob(Pawn pawn, ThingDef def)
	{
		Job val = InvokeTryMakeReloadJob(pawn, def);
		if (val != null && pawn != null && pawn.jobs != null)
		{
			val.playerForced = true;
			pawn.jobs.StartJob(val, (JobCondition)16, (ThinkNode)null, true, true, (ThinkTreeDef)null, (JobTag?)null, false, false, (bool?)null, false, true, false);
		}
	}

	public static void TryPickupAmmoPostfix(object __instance, ref bool __result)
	{
		try
		{
			if (__result || __instance == null)
			{
				return;
			}
			object member = GetMember(__instance, "Holder");
			Pawn val = (Pawn)((member is Pawn) ? member : null);
			if (val != null && !((Thing)val).Destroyed && ((Thing)val).Map != null && ((Thing)val).Faction == Faction.OfPlayer && SDS_ChipManualDeposit.PawnCanDeposit(val) && GridUsable(((Thing)val).Map) && TryFindSupplyAmmo(__instance, allowAmmoSetFallback: true, out var supplyDef, out var need, out var _))
			{
				Job val2 = InvokeTryMakeReloadJob(val, supplyDef);
				if (val2 != null && val.jobs != null && MaterializeIntoInventory(val, supplyDef, need) > 0)
				{
					val2.playerForced = true;
					bool flag = val.CurJob == null || val.CurJob.def != val2.def;
					val.jobs.StartJob(val2, (JobCondition)16, (ThinkNode)null, flag, true, (ThinkTreeDef)null, (JobTag?)null, false, false, (bool?)null, false, true, false);
					__result = true;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（紧急捡弹）失败（已忽略，CE 行为不受影响）", exception);
		}
	}

	private static bool ReloadJobPrefix(Pawn pawn, Thing target, bool autoLoader)
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			ClearPending();
			if (pawn == null || target == null || ((Thing)pawn).Map == null || target.Map != ((Thing)pawn).Map || ((Thing)pawn).Faction != Faction.OfPlayer)
			{
				return true;
			}
			if (CeFindsAmmo(pawn, target, autoLoader))
			{
				return true;
			}
			if (!NeedAmmoFor(target, out var def, out var need))
			{
				return true;
			}
			Thing val = MaterializeToMap(def, need, ((Thing)pawn).Map, WithdrawCellFor(pawn, target));
			if (val != null)
			{
				pendingAmmo = val;
				pendingAmmoTarget = target;
				pendingAmmoDef = def;
			}
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（换弹前置补货）失败（已忽略，CE 行为不受影响）", exception);
			return true;
		}
	}

	private static void ReloadJobPostfix(Pawn pawn, Thing target, bool autoLoader, ref Job __result)
	{
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__result != null)
			{
				ClearPending();
				return;
			}
			if (pawn == null || target == null || ((Thing)pawn).Map == null || ((Thing)pawn).Faction != Faction.OfPlayer)
			{
				ClearPending();
				return;
			}
			bool num;
			if (!autoLoader)
			{
				num = !(target is Building_Turret);
			}
			else
			{
				if (tBuildingAutoloaderCe == null)
				{
					goto IL_0081;
				}
				num = !tBuildingAutoloaderCe.IsInstanceOfType(target);
			}
			if (!num)
			{
				JobDef val = (autoLoader ? ReloadAutoLoaderJobDef() : ReloadTurretJobDef());
				object obj = AmmoCompOf(target);
				if (val == null || obj == null)
				{
					ClearPending();
					return;
				}
				if (!BoolMember(obj, "UseAmmo"))
				{
					if (!autoLoader)
					{
						__result = JobMaker.MakeJob(val, new LocalTargetInfo(target), LocalTargetInfo.Invalid);
					}
					ClearPending();
					return;
				}
				Thing val2 = ((pendingAmmoTarget == target && pendingAmmo != null && !pendingAmmo.Destroyed && pendingAmmo.Spawned) ? pendingAmmo : null);
				if (val2 == null && NeedAmmoFor(target, out var def, out var need))
				{
					val2 = MaterializeToMap(def, need, ((Thing)pawn).Map, WithdrawCellFor(pawn, target));
				}
				if (val2 == null)
				{
					val2 = ((pendingAmmoDef != null) ? FindSpawnedAmmoNear(pawn, pendingAmmoDef) : null);
				}
				if (val2 == null)
				{
					ClearPending();
					return;
				}
				Job val3 = JobMaker.MakeJob(val, new LocalTargetInfo(target), new LocalTargetInfo(val2));
				int num2 = IntMember(obj, "MissingToFullMagazine");
				val3.count = Math.Min(val2.stackCount, (num2 > 0) ? num2 : val2.stackCount);
				__result = val3;
				ClearPending();
				return;
			}
			goto IL_0081;
			IL_0081:
			ClearPending();
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（换弹兜底直构）失败（已忽略，CE 行为不受影响）", exception);
			ClearPending();
		}
	}

	public static bool TryPrepareShotPrefix(object __instance)
	{
		try
		{
			if (__instance == null)
			{
				return true;
			}
			object member = GetMember(__instance, "Holder");
			Pawn val = (Pawn)((member is Pawn) ? member : null);
			if (val == null || ((Thing)val).Destroyed || ((Thing)val).Map == null || ((Thing)val).Faction != Faction.OfPlayer)
			{
				return true;
			}
			if (!SDS_ChipManualDeposit.PawnCanDeposit(val) || !GridUsable(((Thing)val).Map))
			{
				return true;
			}
			if (BoolMember(__instance, "HasAmmo"))
			{
				return true;
			}
			if (BoolMember(__instance, "HasMagazine") && IntMember(__instance, "CurMagCount") > 0)
			{
				return true;
			}
			if (!TryFindSupplyAmmo(__instance, !BoolMember(__instance, "LoadedMagazine"), out var supplyDef, out var need, out var _))
			{
				return true;
			}
			if (MaterializeIntoInventory(val, supplyDef, need) <= 0)
			{
				return true;
			}
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（打空即换弹）失败（已忽略，CE 行为不受影响）", exception);
			return true;
		}
	}

	private static bool CeFindsAmmo(Pawn pawn, Thing target, bool autoLoader)
	{
		MethodInfo methodInfo = (autoLoader ? mFindBestAmmoAutoloader : mFindBestAmmoTurret);
		if (methodInfo == null)
		{
			return false;
		}
		try
		{
			return methodInfo.Invoke(null, new object[2] { pawn, target }) != null;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容：FindBestAmmo 反射调用失败（按 CE 找不到弹药处理）", exception);
			return false;
		}
	}

	public static void FindBestAmmoMirrorFilterPostfix(Pawn pawn, ref Thing __result)
	{
		try
		{
			if (SDS_API.IsMirror(__result))
			{
				__result = FindRealAmmo(pawn, __result.def);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容（弹药搜索镜像过滤）失败（已忽略，CE 行为不受影响）", exception);
		}
	}

	private static Thing FindRealAmmo(Pawn pawn, ThingDef def)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null || ((Thing)pawn).Map == null || def == null || (int)def.category != 2)
		{
			return null;
		}
		float num = ((((Thing)pawn).Faction != Faction.OfPlayer) ? 40f : 9999f);
		try
		{
			return GenClosest.ClosestThingReachable(((Thing)pawn).Position, ((Thing)pawn).Map, ThingRequest.ForDef(def), (PathEndMode)3, TraverseParms.For(pawn, (Danger)3, (TraverseMode)0, false, false, false, true), num, (Predicate<Thing>)((Thing t) => t != null && !t.Destroyed && !SDS_API.IsMirror(t) && !ForbidUtility.IsForbidden(t, pawn) && !FireUtility.IsBurning(t) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(t), 1, -1, (ReservationLayerDef)null, false)), (IEnumerable<Thing>)null, 0, -1, false, (RegionType)14, false, false);
		}
		catch (Exception)
		{
			return null;
		}
	}

	public static bool ReloadDriverMirrorGuardPrefix(JobDriver __instance, ref bool __result)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		Job val = __instance?.job;
		if (val == null)
		{
			return true;
		}
		LocalTargetInfo target = val.GetTarget((TargetIndex)1);
		if (!SDS_API.IsMirror(((LocalTargetInfo)(ref target)).Thing))
		{
			target = val.GetTarget((TargetIndex)2);
			if (!SDS_API.IsMirror(((LocalTargetInfo)(ref target)).Thing))
			{
				return true;
			}
		}
		__result = false;
		return false;
	}

	private static IntVec3 WithdrawCellFor(Pawn pawn, Thing target)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (SDS_API.TryGetWithdrawCell(pawn, out var cell, out var _))
			{
				return cell;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容：取物点解析失败（回落到目标 / 自身位置）", exception);
		}
		return (target != null && target.Spawned) ? target.Position : ((Thing)pawn).Position;
	}

	private static JobDef ReloadTurretJobDef()
	{
		try
		{
			object result;
			if (!(fReloadTurretDef != null))
			{
				result = null;
			}
			else
			{
				object? value = fReloadTurretDef.GetValue(null);
				result = ((value is JobDef) ? value : null);
			}
			return (JobDef)result;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容：读取 ReloadTurret JobDef 失败", exception);
			return null;
		}
	}

	private static JobDef ReloadAutoLoaderJobDef()
	{
		try
		{
			object result;
			if (!(fReloadAutoLoaderDef != null))
			{
				result = null;
			}
			else
			{
				object? value = fReloadAutoLoaderDef.GetValue(null);
				result = ((value is JobDef) ? value : null);
			}
			return (JobDef)result;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容：读取 ReloadAutoLoader JobDef 失败", exception);
			return null;
		}
	}

	private static Thing FindSpawnedAmmoNear(Pawn pawn, ThingDef def)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			return GenClosest.ClosestThingReachable(((Thing)pawn).Position, ((Thing)pawn).Map, ThingRequest.ForDef(def), (PathEndMode)3, TraverseParms.For(pawn, (Danger)3, (TraverseMode)0, false, false, false, true), 9999f, (Predicate<Thing>)((Thing th) => th != null && !th.Destroyed && th.Spawned && !ForbidUtility.IsForbidden(th, pawn)), (IEnumerable<Thing>)null, 0, -1, false, (RegionType)14, false, false);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static void ClearPending()
	{
		pendingAmmo = null;
		pendingAmmoTarget = null;
		pendingAmmoDef = null;
	}

	private static bool NeedAmmoFor(Thing target, out ThingDef def, out int need)
	{
		int available;
		return TryFindSupplyAmmo(AmmoCompOf(target), allowAmmoSetFallback: false, out def, out need, out available);
	}

	private static object AmmoCompOf(Thing target)
	{
		try
		{
			if (mGetAmmo != null && target is Building_Turret)
			{
				return mGetAmmo.Invoke(null, new object[1] { target });
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容：TurretRegistry.GetAmmo 调用失败（回落到普通组件查找）", exception);
		}
		return GetComp(target, tCompAmmoUser);
	}

	private static bool TryFindSupplyAmmo(object ammoComp, bool allowAmmoSetFallback, out ThingDef supplyDef, out int need, out int available)
	{
		supplyDef = null;
		need = 0;
		available = 0;
		if (ammoComp == null || !BoolMember(ammoComp, "UseAmmo"))
		{
			return false;
		}
		int num = IntMember(ammoComp, "MissingToFullMagazine");
		if (num <= 0)
		{
			return false;
		}
		object member = GetMember(ammoComp, "SelectedAmmo");
		ThingDef val = (ThingDef)((member is ThingDef) ? member : null);
		if (val == null)
		{
			return false;
		}
		if (HasInGrid(val, out available))
		{
			supplyDef = val;
			need = ClampNeed(val, num);
			return need > 0;
		}
		if (!allowAmmoSetFallback)
		{
			return false;
		}
		foreach (ThingDef item in AmmoSetDefs(ammoComp))
		{
			if (item == null || item == val || !HasInGrid(item, out available))
			{
				continue;
			}
			supplyDef = item;
			need = ClampNeed(item, num);
			return need > 0;
		}
		return false;
	}

	private static bool HasInGrid(ThingDef def, out int available)
	{
		available = 0;
		if (def == null)
		{
			return false;
		}
		SDS_StockKey key;
		return SDS_API.TryFindWithdrawableKey(def, out key, out available) && available > 0;
	}

	private static int ClampNeed(ThingDef def, int missing)
	{
		int num = ((def == null || def.stackLimit <= 0) ? 1 : def.stackLimit);
		return (missing > num) ? num : missing;
	}

	private static IEnumerable<ThingDef> AmmoSetDefs(object ammoComp)
	{
		object set = GetMember(ammoComp, "CurAmmoSet");
		object types = ((set != null) ? GetMember(set, "ammoTypes") : null);
		if (!(types is IEnumerable list))
		{
			yield break;
		}
		foreach (object link in list)
		{
			object obj;
			if (link == null)
			{
				obj = null;
			}
			else
			{
				object member = GetMember(link, "ammo");
				obj = ((member is ThingDef) ? member : null);
			}
			ThingDef def = (ThingDef)obj;
			if (def != null)
			{
				yield return def;
			}
		}
	}

	private static bool CanSupplyAmmoFor(Pawn pawn, Thing target, bool forced)
	{
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null || target == null || ((Thing)pawn).Map == null || target.Map != ((Thing)pawn).Map)
		{
			return false;
		}
		if (!GridUsable(((Thing)pawn).Map))
		{
			return false;
		}
		Danger val = (Danger)(forced ? 3 : ((int)DangerUtility.NormalMaxDanger(pawn)));
		if (target is Building_Turret)
		{
			CompMannable val2 = ThingCompUtility.TryGetComp<CompMannable>(target);
			if ((val2 == null || val2.ManningPawn != pawn) && !ReservationUtility.CanReserveAndReach(pawn, LocalTargetInfo.op_Implicit(target), (PathEndMode)4, val, 10, -1, (ReservationLayerDef)null, false))
			{
				return false;
			}
		}
		else if (!ReservationUtility.CanReserveAndReach(pawn, LocalTargetInfo.op_Implicit(target), (PathEndMode)3, val, 1, -1, (ReservationLayerDef)null, false))
		{
			return false;
		}
		if (!NeedAmmoFor(target, out var def, out var _))
		{
			return false;
		}
		SDS_StockKey key;
		int available;
		return SDS_API.TryFindWithdrawableKey(def, out key, out available) && available > 0;
	}

	private static bool CanReserveTurret(Pawn pawn, Thing target)
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Invalid comparison between Unknown and I4
		if (pawn == null || target == null)
		{
			return false;
		}
		if (ForbidUtility.IsForbidden(target, pawn) || FireUtility.IsBurning(target))
		{
			return false;
		}
		object target2 = AmmoCompOf(target);
		if (BoolMember(target2, "FullMagazine"))
		{
			return false;
		}
		if (((Thing)pawn).Faction != null && target.Faction != ((Thing)pawn).Faction)
		{
			Faction faction = target.Faction;
			if (faction == null || (int)faction.RelationKindWith(((Thing)pawn).Faction) != 2)
			{
				return false;
			}
		}
		if (target is Building_Turret)
		{
			if (mGetReloadable != null && !InvokeStaticBool(mGetReloadable, target))
			{
				return false;
			}
			if (pawn.health == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
				return false;
			}
			if (InvokeStaticBool(mGetReloading, target))
			{
				return false;
			}
		}
		else if (tBuildingAutoloaderCe != null && tBuildingAutoloaderCe.IsInstanceOfType(target) && (BoolMember(target, "isReloading") || BoolMember(target, "isActive")))
		{
			return false;
		}
		return true;
	}

	private static bool InvokeStaticBool(MethodInfo method, Thing target)
	{
		if (method == null || target == null)
		{
			return false;
		}
		object obj = method.Invoke(null, new object[1] { target });
		bool flag = default(bool);
		int num;
		if (obj is bool)
		{
			flag = (bool)obj;
			num = 1;
		}
		else
		{
			num = 0;
		}
		return (byte)((uint)num & (flag ? 1u : 0u)) != 0;
	}

	private static bool CanConsiderCheckReload(Pawn pawn)
	{
		if (pawn == null || ((Thing)pawn).Destroyed || ((Thing)pawn).Map == null || pawn.Downed || pawn.Dead || pawn.InMentalState || ((Thing)pawn).Faction != Faction.OfPlayer)
		{
			return false;
		}
		if (pawn.Drafted)
		{
			return false;
		}
		int ticksGame = GenTicks.TicksGame;
		bool value2;
		if (priorityCacheUntil.TryGetValue(((Thing)pawn).thingIDNumber, out var value) && ticksGame < value)
		{
			return priorityCacheValue.TryGetValue(((Thing)pawn).thingIDNumber, out value2) && value2;
		}
		bool flag = CanConsiderCheckReloadUncached(pawn);
		priorityCacheUntil[((Thing)pawn).thingIDNumber] = ticksGame + (flag ? 60 : 10);
		priorityCacheValue[((Thing)pawn).thingIDNumber] = flag;
		return flag;
	}

	private static bool CanConsiderCheckReloadUncached(Pawn pawn)
	{
		if (!GridUsable(((Thing)pawn).Map))
		{
			return false;
		}
		if (IsOnCheckCooldown(pawn))
		{
			return false;
		}
		if (!NeedAmmoForPawn(pawn, out var _, out var _))
		{
			return false;
		}
		return true;
	}

	private static bool NeedAmmoForPawn(Pawn pawn, out ThingDef def, out int need)
	{
		def = null;
		need = 0;
		if (pawn == null)
		{
			return false;
		}
		object comp = GetComp((Thing)(object)((pawn.equipment != null) ? pawn.equipment.Primary : null), tCompAmmoUser);
		if (TryFindSupplyAmmo(comp, !BoolMember(comp, "LoadedMagazine"), out def, out need, out var available))
		{
			return true;
		}
		def = null;
		need = 0;
		ThingOwner val = (ThingOwner)(object)((pawn.inventory != null) ? pawn.inventory.innerContainer : null);
		if (val != null)
		{
			foreach (Thing item in (IEnumerable<Thing>)val)
			{
				object comp2 = GetComp(item, tCompAmmoUser);
				if (TryFindSupplyAmmo(comp2, !BoolMember(comp2, "LoadedMagazine"), out def, out need, out available))
				{
					return true;
				}
				def = null;
				need = 0;
			}
		}
		return false;
	}

	private static Job InvokeTryMakeReloadJob(Pawn pawn, ThingDef def)
	{
		try
		{
			if (pawn == null)
			{
				return null;
			}
			object obj = FindHoldingAmmoComp(pawn, def);
			if (obj == null)
			{
				return null;
			}
			MethodInfo methodInfo = AccessTools.Method(tCompAmmoUser, "TryMakeReloadJob", Type.EmptyTypes, (Type[])null);
			if (methodInfo == null)
			{
				return null;
			}
			object? obj2 = methodInfo.Invoke(obj, null);
			return (Job)((obj2 is Job) ? obj2 : null);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容：调用 CompAmmoUser.TryMakeReloadJob 失败（已忽略）", exception);
			return null;
		}
	}

	private static object FindHoldingAmmoComp(Pawn pawn, ThingDef def)
	{
		object comp = GetComp((Thing)(object)((pawn.equipment != null) ? pawn.equipment.Primary : null), tCompAmmoUser);
		if (CompAcceptsAmmo(comp, def))
		{
			return comp;
		}
		ThingOwner val = (ThingOwner)(object)((pawn.inventory != null) ? pawn.inventory.innerContainer : null);
		if (val != null)
		{
			foreach (Thing item in (IEnumerable<Thing>)val)
			{
				object comp2 = GetComp(item, tCompAmmoUser);
				if (CompAcceptsAmmo(comp2, def))
				{
					return comp2;
				}
			}
		}
		return null;
	}

	private static bool CompAcceptsAmmo(object comp, ThingDef def)
	{
		if (comp == null || def == null)
		{
			return false;
		}
		object member = GetMember(comp, "SelectedAmmo");
		if (((member is ThingDef) ? member : null) == def)
		{
			return true;
		}
		foreach (ThingDef item in AmmoSetDefs(comp))
		{
			if (item == def)
			{
				return true;
			}
		}
		return false;
	}

	private static bool TryTakeForCe(ThingDef def, int count, SDS_ApiCaller caller, out List<Thing> made)
	{
		made = null;
		if (!SDS_API.IsGridAvailable() || def == null || count <= 0)
		{
			return false;
		}
		if (!SDS_API.TryFindWithdrawableKey(def, out var key, out var available) || available <= 0)
		{
			return false;
		}
		int quantity = Math.Min(count, available);
		if (!SDS_API.TryWithdraw(key, quantity, SDS_WithdrawMode.Hand, caller, out made, out var _) || made == null || made.Count == 0)
		{
			made = null;
			return false;
		}
		return true;
	}

	private static Thing MaterializeToMap(ThingDef def, int count, Map map, IntVec3 cell)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || !TryTakeForCe(def, count, SDS_ApiCaller.FromFaction(Faction.OfPlayer), out var made))
		{
			return null;
		}
		IntVec3 val = (((IntVec3)(ref cell)).IsValid ? cell : map.Center);
		Thing val2 = null;
		List<Thing> list = new List<Thing>();
		Thing val4 = default(Thing);
		for (int i = 0; i < made.Count; i++)
		{
			Thing val3 = made[i];
			if (val3 == null || val3.Destroyed)
			{
				continue;
			}
			if (GenPlace.TryPlaceThing(val3, val, map, (ThingPlaceMode)1, ref val4, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1) && val4 != null)
			{
				list.Add(val4);
				if (val2 == null)
				{
					val2 = val4;
				}
			}
			else
			{
				ReturnToGrid(val3);
			}
		}
		if (list.Count > 0)
		{
			SDS_API.MarkProducedPendingConsumption(list);
		}
		return val2;
	}

	private static int MaterializeIntoInventory(Pawn pawn, ThingDef def, int count)
	{
		if (pawn == null || ((Thing)pawn).Destroyed || ((Thing)pawn).Map == null || pawn.inventory == null || pawn.inventory.innerContainer == null)
		{
			return 0;
		}
		if (!TryTakeForCe(def, count, SDS_ApiCaller.FromPawn(pawn), out var made))
		{
			return 0;
		}
		ThingOwner innerContainer = (ThingOwner)(object)pawn.inventory.innerContainer;
		int num = CountDefInOwner(innerContainer, def);
		for (int i = 0; i < made.Count; i++)
		{
			Thing val = made[i];
			if (val != null && !val.Destroyed && !innerContainer.TryAdd(val, true))
			{
				ReturnToGrid(val);
			}
		}
		int num2 = CountDefInOwner(innerContainer, def) - num;
		if (num2 > 0)
		{
			ThingComp comp = GetComp((Thing)(object)pawn, tCompInventory);
			if (comp != null)
			{
				InvokeMethod(comp, "UpdateInventory");
			}
		}
		return num2;
	}

	private static void ReturnToGrid(Thing thing)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		SDS_API.TryReturnToGrid(thing, null, IntVec3.Invalid);
	}

	private static int CountDefInOwner(ThingOwner owner, ThingDef def)
	{
		if (owner == null || def == null)
		{
			return 0;
		}
		int num = 0;
		foreach (Thing item in (IEnumerable<Thing>)owner)
		{
			if (item != null && item.def == def)
			{
				num += item.stackCount;
			}
		}
		return num;
	}

	public static void NotifyInventoryChanged(Pawn pawn)
	{
		if (pawn != null && !(tCompInventory == null))
		{
			ThingComp comp = GetComp((Thing)(object)pawn, tCompInventory);
			if (comp != null)
			{
				InvokeMethod(comp, "UpdateInventory");
			}
		}
	}

	public static int CountCanFitInInventory(Pawn pawn, Thing thing)
	{
		if (pawn == null || thing == null)
		{
			return 0;
		}
		if (tCompInventory == null)
		{
			return thing.stackCount;
		}
		ThingComp comp = GetComp((Thing)(object)pawn, tCompInventory);
		if (comp == null)
		{
			return thing.stackCount;
		}
		try
		{
			MethodInfo methodInfo = AccessTools.Method(tCompInventory, "CanFitInInventory", new Type[4]
			{
				typeof(Thing),
				typeof(int).MakeByRefType(),
				typeof(bool),
				typeof(bool)
			}, (Type[])null);
			if (methodInfo == null)
			{
				return thing.stackCount;
			}
			object[] array = new object[4] { thing, 0, false, false };
			object obj = methodInfo.Invoke(comp, array);
			bool flag = default(bool);
			int num;
			if (obj is bool)
			{
				flag = (bool)obj;
				num = 1;
			}
			else
			{
				num = 0;
			}
			bool flag2 = (byte)((uint)num & (flag ? 1u : 0u)) != 0;
			int num2 = ((array[1] is int num3) ? num3 : 0);
			if (!flag2 || num2 <= 0)
			{
				return 0;
			}
			return Math.Min(num2, thing.stackCount);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容：背包容量判定失败（按整堆放行）", exception);
			return thing.stackCount;
		}
	}

	private static bool GridUsable(Map map)
	{
		return SDS_API.IsGridUsable(map);
	}

	private static ThingComp GetComp(Thing thing, Type compType)
	{
		if (thing == null || compType == null)
		{
			return null;
		}
		ThingWithComps val = (ThingWithComps)(object)((thing is ThingWithComps) ? thing : null);
		if (val == null || val.AllComps == null)
		{
			return null;
		}
		for (int i = 0; i < val.AllComps.Count; i++)
		{
			ThingComp val2 = val.AllComps[i];
			if (val2 != null && compType.IsInstanceOfType(val2))
			{
				return val2;
			}
		}
		return null;
	}

	private static object GetMember(object target, string name)
	{
		if (target == null || name == null)
		{
			return null;
		}
		try
		{
			Type type = target.GetType();
			PropertyInfo propertyInfo = AccessTools.Property(type, name);
			if (propertyInfo != null && propertyInfo.CanRead)
			{
				return propertyInfo.GetValue(target, null);
			}
			FieldInfo fieldInfo = AccessTools.Field(type, name);
			return (fieldInfo != null) ? fieldInfo.GetValue(target) : null;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容：读取成员 " + name + " 失败（返回默认值）", exception);
			return null;
		}
	}

	private static int IntMember(object target, string name)
	{
		object member = GetMember(target, name);
		if (member is int result)
		{
			return result;
		}
		if (member is float num)
		{
			return (int)num;
		}
		return 0;
	}

	private static bool BoolMember(object target, string name)
	{
		object member = GetMember(target, name);
		bool flag = default(bool);
		int num;
		if (member is bool)
		{
			flag = (bool)member;
			num = 1;
		}
		else
		{
			num = 0;
		}
		return (byte)((uint)num & (flag ? 1u : 0u)) != 0;
	}

	private static float FloatMember(object target, string name)
	{
		object member = GetMember(target, name);
		if (member is float result)
		{
			return result;
		}
		if (member is int num)
		{
			return num;
		}
		return 0f;
	}

	private static object InvokeMethod(object target, string name)
	{
		if (target == null)
		{
			return null;
		}
		try
		{
			MethodInfo methodInfo = AccessTools.Method(target.GetType(), name, Type.EmptyTypes, (Type[])null);
			return (methodInfo != null) ? methodInfo.Invoke(target, null) : null;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("CE 兼容：调用方法 " + name + " 失败（返回 null）", exception);
			return null;
		}
	}

	private static bool IsOnCheckCooldown(Pawn pawn)
	{
		if (pawn != null && checkCooldownUntil.TryGetValue(((Thing)pawn).thingIDNumber, out var value))
		{
			return GenTicks.TicksGame < value;
		}
		return false;
	}

	private static void NotifyChecked(Pawn pawn)
	{
		if (pawn != null)
		{
			int ticksGame = GenTicks.TicksGame;
			checkCooldownUntil[((Thing)pawn).thingIDNumber] = ticksGame + 250;
			priorityCacheValue[((Thing)pawn).thingIDNumber] = false;
			priorityCacheUntil[((Thing)pawn).thingIDNumber] = ticksGame + 250;
		}
	}
}
internal static class SDS_ModCompat_EquivalentExchange
{
	private const string TableTypeName = "EquivalentExchange.Building_TransmutationTable";

	private const string InterfaceTypeName = "EquivalentExchange.Building_EmcInterface";

	private const string UtilityTypeName = "EquivalentExchange.TransmutationUtility";

	private const string TableDefName = "EE_TransmutationTable";

	private const string InterfaceDefName = "EE_EmcInterface";

	private static bool detected;

	private static bool hookReady;

	private static bool fieldsProbed;

	private static FieldInfo transferablesField;

	private static FieldInfo transportersField;

	private static FieldInfo mapField;

	public static void TryApply(Harmony harmony)
	{
		bool flag = false;
		try
		{
			if (harmony == null)
			{
				return;
			}
			flag = (detected = AccessTools.TypeByName("EquivalentExchange.Building_TransmutationTable") != null || AccessTools.TypeByName("EquivalentExchange.Building_EmcInterface") != null);
			ProbeFields();
			if (!flag)
			{
				SDS_CompatReportHub.Register("等价交换（ProjectE）", "Natsuki.ProjectE", detected: false, null);
				return;
			}
			SDS_CompatPatchReport sDS_CompatPatchReport = new SDS_CompatPatchReport("等价交换（ProjectE）");
			if (transferablesField == null || transportersField == null)
			{
				sDS_CompatPatchReport.Missing("Dialog_LoadTransporters 字段（transferables / transporters）", "字段改名或结构变化");
			}
			bool flag2 = SDS_CompatTools.TryPatch(harmony, sDS_CompatPatchReport, typeof(SDS_ModCompat_EquivalentExchange), "Dialog_LoadTransporters.TryAccept（瞬时转化：展示物换真身）", AccessTools.Method(typeof(Dialog_LoadTransporters), "TryAccept", (Type[])null, (Type[])null), "TryAcceptPrefix", null, "找不到方法 TryAccept()", "TryAcceptFinalizer", 600);
			Type type = AccessTools.TypeByName("EquivalentExchange.TransmutationUtility");
			SDS_CompatTools.TryPatch(harmony, sDS_CompatPatchReport, typeof(SDS_ModCompat_EquivalentExchange), "TransmutationUtility.GetTransmutableThingsInArea（金库投影过滤）", (type != null) ? AccessTools.Method(type, "GetTransmutableThingsInArea", new Type[3]
			{
				typeof(Map),
				typeof(IntVec3),
				typeof(int)
			}, (Type[])null) : null, null, "AreaCandidatesPostfix", (type == null) ? "找不到类型 EquivalentExchange.TransmutationUtility" : "找不到方法 GetTransmutableThingsInArea(Map,IntVec3,int)");
			hookReady = flag2 && transferablesField != null && transportersField != null;
			SDS_CompatReportHub.Register("等价交换（ProjectE）", "Natsuki.ProjectE", detected: true, sDS_CompatPatchReport, hookReady ? null : "瞬时转化未接管：EE 对话框不注入展示物");
		}
		catch (Exception ex)
		{
			SDS_CompatReportHub.Register("等价交换（ProjectE）", "Natsuki.ProjectE", flag, null, null, "应用异常 " + ex.GetType().Name);
			SDS_Diag.ErrorOnce("等价交换（ProjectE）兼容补丁应用失败（已跳过，本体行为不受影响）", ex);
		}
	}

	private static void ProbeFields()
	{
		if (!fieldsProbed)
		{
			fieldsProbed = true;
			transferablesField = AccessTools.Field(typeof(Dialog_LoadTransporters), "transferables");
			transportersField = AccessTools.Field(typeof(Dialog_LoadTransporters), "transporters");
			mapField = AccessTools.Field(typeof(Dialog_LoadTransporters), "map");
		}
	}

	public static bool AllowInjection(Dialog_LoadTransporters dialog)
	{
		if (!detected)
		{
			return true;
		}
		if (hookReady)
		{
			return true;
		}
		return !IsTransmutationDialog(GetTransporters(dialog));
	}

	private static List<CompTransporter> GetTransporters(Dialog_LoadTransporters dialog)
	{
		if (dialog == null || !fieldsProbed || transportersField == null)
		{
			return null;
		}
		try
		{
			return transportersField.GetValue(dialog) as List<CompTransporter>;
		}
		catch
		{
			return null;
		}
	}

	private static bool IsTransmutationDialog(List<CompTransporter> transporters)
	{
		if (transporters == null || transporters.Count == 0)
		{
			return false;
		}
		for (int i = 0; i < transporters.Count; i++)
		{
			ThingWithComps val = ((ThingComp)(transporters[i]?)).parent;
			string text = ((val != null && ((Thing)val).def != null) ? ((Def)((Thing)val).def).defName : null);
			if (text != "EE_TransmutationTable" && text != "EE_EmcInterface")
			{
				return false;
			}
		}
		return true;
	}

	public static bool TryAcceptPrefix(Dialog_LoadTransporters __instance, out SDS_EETransmuteState __state)
	{
		__state = null;
		try
		{
			if (__instance == null || !fieldsProbed || transferablesField == null || transportersField == null)
			{
				return true;
			}
			if (!IsTransmutationDialog(GetTransporters(__instance)))
			{
				return true;
			}
			if (!(transferablesField.GetValue(__instance) is List<TransferableOneWay> { Count: not 0 } list))
			{
				return true;
			}
			__state = ConvertRows(list);
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("等价交换兼容（瞬时转化前置）失败（已忽略，它的原流程不受影响）", exception);
			return true;
		}
	}

	public static Exception TryAcceptFinalizer(Dialog_LoadTransporters __instance, SDS_EETransmuteState __state, Exception __exception)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		if (__state == null)
		{
			return __exception;
		}
		try
		{
			Map map = null;
			if (__instance != null && mapField != null)
			{
				object? value = mapField.GetValue(__instance);
				map = (Map)((value is Map) ? value : null);
			}
			IntVec3 cell = IntVec3.Invalid;
			List<CompTransporter> transporters = GetTransporters(__instance);
			if (transporters != null && transporters.Count > 0 && transporters[0] != null && ((ThingComp)transporters[0]).parent != null)
			{
				cell = ((Thing)((ThingComp)transporters[0]).parent).PositionHeld;
			}
			for (int i = 0; i < __state.materialized.Count; i++)
			{
				Thing val = __state.materialized[i];
				if (val != null && !val.Destroyed && !val.Spawned && val.holdingOwner == null)
				{
					SDS_StockKey sDS_StockKey = SDS_StockKey.FromThing(val);
					if (sDS_StockKey != null)
					{
						SDS_API.TryReturnVirtual(val, sDS_StockKey, map, cell);
					}
				}
			}
			for (int j = 0; j < __state.rows.Count; j++)
			{
				TransferableOneWay val2 = __state.rows[j];
				if (val2 == null || val2.things == null)
				{
					continue;
				}
				for (int num = val2.things.Count - 1; num >= 0; num--)
				{
					Thing val3 = val2.things[num];
					if (val3 == null || val3.Destroyed || __state.materialized.Contains(val3))
					{
						val2.things.RemoveAt(num);
					}
				}
				if (IsRowEmpty(val2))
				{
					((Transferable)val2).AdjustTo(0);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("等价交换兼容（瞬时转化回充）失败（实物已在世界，未重复记账）", exception);
		}
		return __exception;
	}

	private static SDS_EETransmuteState ConvertRows(List<TransferableOneWay> transferables)
	{
		SDS_EETransmuteState sDS_EETransmuteState = null;
		for (int num = transferables.Count - 1; num >= 0; num--)
		{
			TransferableOneWay val = transferables[num];
			if (val != null && val.things != null && ((Transferable)val).CountToTransfer > 0)
			{
				int num2 = 0;
				bool flag = false;
				bool flag2 = false;
				int num3 = 0;
				SDS_StockKey sDS_StockKey = null;
				for (int i = 0; i < val.things.Count; i++)
				{
					Thing val2 = val.things[i];
					if (val2 == null || val2.Destroyed)
					{
						continue;
					}
					if (!SDS_VirtualDisplayRegistry.IsFake(val2))
					{
						num2 += Math.Max(0, val2.stackCount);
						continue;
					}
					if (!SDS_API.TryGetDisplayKey(val2, out var key) || key == null)
					{
						flag2 = true;
						continue;
					}
					if (key.preserved)
					{
						flag2 = true;
						continue;
					}
					flag = true;
					num3 += Math.Max(0, val2.stackCount);
					if (sDS_StockKey == null)
					{
						sDS_StockKey = key;
					}
				}
				if (flag || flag2)
				{
					if (!flag)
					{
						RemoveFakes(val);
						if (IsRowEmpty(val))
						{
							transferables.RemoveAt(num);
						}
					}
					else
					{
						int num4 = ((Transferable)val).CountToTransfer - num2;
						int num5 = ((num4 > 0) ? Math.Min(num4, num3) : 0);
						long availableCount = SDS_API.GetAvailableCount(sDS_StockKey);
						if (num5 > availableCount)
						{
							num5 = (int)Math.Min(availableCount, 2147483647L);
						}
						List<Thing> made;
						SDS_ApiResultCode code;
						if (num5 <= 0)
						{
							RemoveFakes(val);
							if (IsRowEmpty(val))
							{
								transferables.RemoveAt(num);
							}
						}
						else if (!SDS_API.TryWithdraw(sDS_StockKey, num5, SDS_WithdrawMode.Hand, SDS_ApiCaller.FromFaction(Faction.OfPlayer), out made, out code) || made == null || made.Count == 0)
						{
							RemoveFakes(val);
							if (IsRowEmpty(val))
							{
								transferables.RemoveAt(num);
							}
						}
						else
						{
							RemoveFakes(val);
							for (int j = 0; j < made.Count; j++)
							{
								Thing val3 = made[j];
								if (val3 != null && !val3.Destroyed && val3.stackCount > 0)
								{
									val.things.Add(val3);
									if (sDS_EETransmuteState == null)
									{
										sDS_EETransmuteState = new SDS_EETransmuteState();
									}
									sDS_EETransmuteState.materialized.Add(val3);
									if (!sDS_EETransmuteState.rows.Contains(val))
									{
										sDS_EETransmuteState.rows.Add(val);
									}
								}
							}
							if (IsRowEmpty(val))
							{
								transferables.RemoveAt(num);
							}
						}
					}
				}
			}
		}
		return sDS_EETransmuteState;
	}

	private static void RemoveFakes(TransferableOneWay row)
	{
		if (row == null || row.things == null)
		{
			return;
		}
		for (int num = row.things.Count - 1; num >= 0; num--)
		{
			Thing val = row.things[num];
			if (val != null && SDS_VirtualDisplayRegistry.IsFake(val))
			{
				row.things.RemoveAt(num);
			}
		}
	}

	private static bool IsRowEmpty(TransferableOneWay row)
	{
		if (row == null || row.things == null || row.things.Count == 0)
		{
			return true;
		}
		for (int i = 0; i < row.things.Count; i++)
		{
			Thing val = row.things[i];
			if (val != null && !val.Destroyed && val.stackCount > 0)
			{
				return false;
			}
		}
		return true;
	}

	public static void AreaCandidatesPostfix(ref List<Thing> __result)
	{
		try
		{
			if (__result == null || __result.Count == 0)
			{
				return;
			}
			for (int num = __result.Count - 1; num >= 0; num--)
			{
				if (__result[num] == null || SDS_API.IsMirror(__result[num]))
				{
					__result.RemoveAt(num);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("等价交换兼容（区域转化过滤）失败（已忽略）", exception);
		}
	}
}
internal sealed class SDS_EETransmuteState
{
	public readonly List<Thing> materialized = new List<Thing>();

	public readonly List<TransferableOneWay> rows = new List<TransferableOneWay>();
}
internal static class SDS_ModCompat_ManipulatorBeam
{
	private const string PackageId = "natsuki.manipulatorbeam";

	private const string SettingsTypeName = "ManipulatorBeam.BeamFeatureSettings";

	private const string ScanCacheTypeName = "ManipulatorBeam.MB_BeamScanCacheMapComponent";

	private const string TransferUtilTypeName = "ManipulatorBeam.BeamSpecialTransferUtility";

	private const string TransferTypeName = "ManipulatorBeam.BeamTransfer";

	private static ConstructorInfo transferCtor;

	public static void TryApply(Harmony harmony)
	{
		bool flag = false;
		try
		{
			if (harmony == null)
			{
				return;
			}
			Type type = AccessTools.TypeByName("ManipulatorBeam.BeamFeatureSettings");
			Type type2 = AccessTools.TypeByName("ManipulatorBeam.MB_BeamScanCacheMapComponent");
			Type type3 = AccessTools.TypeByName("ManipulatorBeam.BeamSpecialTransferUtility");
			Type type4 = AccessTools.TypeByName("ManipulatorBeam.BeamTransfer");
			flag = type != null || type2 != null || type3 != null;
			if (!flag)
			{
				SDS_CompatReportHub.Register("牵引光束搬运器（ManipulatorBeam）", "natsuki.manipulatorbeam", detected: false, null);
				return;
			}
			SDS_CompatPatchReport sDS_CompatPatchReport = new SDS_CompatPatchReport("牵引光束搬运器（ManipulatorBeam）");
			transferCtor = ((type4 != null) ? AccessTools.Constructor(type4, new Type[4]
			{
				typeof(Thing),
				typeof(IntVec3),
				typeof(Thing),
				typeof(int)
			}, false) : null);
			if (transferCtor == null)
			{
				sDS_CompatPatchReport.Missing("BeamTransfer(Thing, IntVec3, Thing, int)", "构造函数改名或结构变化");
			}
			SDS_CompatTools.TryPatch(harmony, sDS_CompatPatchReport, typeof(SDS_ModCompat_ManipulatorBeam), "BeamFeatureSettings.IsSpecialContainerTypeEnabled（容器类型闸门）", SDS_CompatTools.Method(type, "IsSpecialContainerTypeEnabled", typeof(Thing)), null, "IsSpecialContainerTypeEnabledPostfix", "找不到类型 ManipulatorBeam.BeamFeatureSettings 或方法 IsSpecialContainerTypeEnabled(Thing)");
			SDS_CompatTools.TryPatch(harmony, sDS_CompatPatchReport, typeof(SDS_ModCompat_ManipulatorBeam), "MB_BeamScanCacheMapComponent.SpecialContainers（容器扫描表）", SDS_CompatTools.PropertyGetter(type2, "SpecialContainers"), null, "SpecialContainersPostfix", "找不到类型 ManipulatorBeam.MB_BeamScanCacheMapComponent 或属性 SpecialContainers");
			SDS_CompatTools.TryPatch(harmony, sDS_CompatPatchReport, typeof(SDS_ModCompat_ManipulatorBeam), "BeamSpecialTransferUtility.TryFindSpecialContainerTransfer（目的地 → 货源）", SDS_CompatTools.Method(type3, "TryFindSpecialContainerTransfer"), "TryFindSpecialContainerTransferPrefix", null, "找不到类型 ManipulatorBeam.BeamSpecialTransferUtility 或方法 TryFindSpecialContainerTransfer");
			SDS_CompatReportHub.Register("牵引光束搬运器（ManipulatorBeam）", "natsuki.manipulatorbeam", detected: true, sDS_CompatPatchReport);
		}
		catch (Exception ex)
		{
			SDS_CompatReportHub.Register("牵引光束搬运器（ManipulatorBeam）", "natsuki.manipulatorbeam", flag, null, null, "应用异常 " + ex.GetType().Name);
			SDS_Diag.ErrorOnce("牵引光束兼容（ManipulatorBeam）应用失败（已跳过，本体行为不受影响）", ex);
		}
	}

	private static void IsSpecialContainerTypeEnabledPostfix(Thing containerThing, ref bool __result)
	{
		try
		{
			if (!__result && containerThing is ISDS_StorageEndpoint)
			{
				__result = true;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("牵引光束兼容（容器类型闸门）失败（已忽略）", exception);
		}
	}

	private static void SpecialContainersPostfix(object __instance, List<Thing> __result)
	{
		try
		{
			if (__result == null)
			{
				return;
			}
			MapComponent val = (MapComponent)((__instance is MapComponent) ? __instance : null);
			if (val == null || val.map == null)
			{
				return;
			}
			List<Building> allBuildingsColonist = val.map.listerBuildings.allBuildingsColonist;
			for (int i = 0; i < allBuildingsColonist.Count; i++)
			{
				Building val2 = allBuildingsColonist[i];
				if (val2 is ISDS_StorageEndpoint && !__result.Contains((Thing)(object)val2))
				{
					__result.Add((Thing)(object)val2);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("牵引光束兼容（容器扫描表）失败（已忽略）", exception);
		}
	}

	private static bool TryFindSpecialContainerTransferPrefix(Thing containerThing, Predicate<Thing> haulValidator, ref object transfer, ref bool __result)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		if (!(containerThing is ISDS_StorageEndpoint))
		{
			return true;
		}
		transfer = null;
		__result = false;
		try
		{
			if (transferCtor == null)
			{
				return false;
			}
			Map map = containerThing.Map;
			if (map == null || containerThing.Destroyed || !containerThing.Spawned || GridsUtility.Fogged(containerThing.Position, map) || haulValidator == null)
			{
				goto IL_007b;
			}
			IHaulDestination val = (IHaulDestination)(object)((containerThing is IHaulDestination) ? containerThing : null);
			if (val == null)
			{
				goto IL_007b;
			}
			Thing val2 = FindDepositableThing(map, containerThing, val, haulValidator);
			if (val2 != null)
			{
				transfer = transferCtor.Invoke(new object[4] { val2, val2.Position, containerThing, val2.stackCount });
				__result = transfer != null;
			}
			goto end_IL_001f;
			IL_007b:
			return false;
			end_IL_001f:;
		}
		catch (Exception exception)
		{
			transfer = null;
			__result = false;
			SDS_Diag.ErrorOnce("牵引光束兼容（端点选源）失败（已忽略）", exception);
		}
		return false;
	}

	private static Thing FindDepositableThing(Map map, Thing endpoint, IHaulDestination destination, Predicate<Thing> haulValidator)
	{
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		Thing val = null;
		ICollection<Thing> collection = map.listerHaulables.ThingsPotentiallyNeedingHauling();
		foreach (Thing item in collection)
		{
			if (item == null || item.Destroyed || !item.Spawned || item.stackCount <= 0 || !haulValidator(item) || !destination.Accepts(item))
			{
				continue;
			}
			val = item;
			break;
		}
		if (val == null)
		{
			return null;
		}
		SDS_ApiCaller caller = SDS_ApiCaller.FromFaction(Faction.OfPlayer);
		if (!SDS_API.TryFindDepositEndpoint(val, map, val.Position, caller, out var endpoint2, out var _) || endpoint2 != endpoint)
		{
			return null;
		}
		return val;
	}
}
internal static class SDS_ModCompat_MiliraExpanded
{
	private static Type networkType;

	private static Type deviceType;

	private static PropertyInfo networkInstanceProp;

	private static PropertyInfo operationalProp;

	private static PropertyInfo recipeSourceProp;

	private static MethodInfo getAvailableCountMethod;

	private static MethodInfo getPreservedAvailableCountMethod;

	private static MethodInfo tryDepositMethod;

	private static MethodInfo canStoreAnyDefMethod;

	private static MethodInfo canDepositDefMethod;

	private static Type orderType;

	private static Type decomposerCompType;

	private static Type lifeEssenceCompType;

	private static MethodInfo ordersSnapshotMethod;

	private static FieldInfo ordersField;

	private static FieldInfo orderRecipeField;

	private static FieldInfo orderKindField;

	private static FieldInfo orderRemainingField;

	private static FieldInfo orderModeField;

	private static FieldInfo orderAllowedField;

	private static FieldInfo orderDeviceNamesField;

	private static FieldInfo orderIdField;

	private static PropertyInfo orderUsesAllowedProp;

	private static FieldInfo orderCorpseFilterField;

	private static MethodInfo orderReserveMethod;

	private static MethodInfo decomposeValueMethod;

	private static FieldInfo decConversionField;

	private static FieldInfo decEfficiencyField;

	private static FieldInfo decTargetField;

	private static FieldInfo leNutritionField;

	private static FieldInfo leOutputField;

	private static MethodInfo lifeEssenceNutritionMethod;

	private static Type matterExtensionType;

	private static MethodInfo requiredMatterMethod;

	private static PropertyInfo availableStandardMatterProp;

	private static ThingDef standardMatterUnitDef;

	private static bool resolved;

	private static readonly Dictionary<int, int> nextTickByDevice = new Dictionary<int, int>();

	private static readonly Dictionary<ThingDef, bool> canStoreCache = new Dictionary<ThingDef, bool>();

	internal static void Reset()
	{
		nextTickByDevice.Clear();
	}

	public static void TryApply(Harmony harmony)
	{
		bool flag = false;
		try
		{
			flag = AccessTools.TypeByName("MiliraExpandedXY.Buildings.GameComponent_VirtualStorageNetwork") != null || AccessTools.TypeByName("MiliraExpandedXY.Buildings.CompVirtualProductionDevice") != null;
			if (!Resolve())
			{
				SDS_CompatReportHub.Register("米莉拉未来科技（Milira Expanded）", "XiyueYM.MiliraExpandedXY", flag, null, null, flag ? "关键类型 / 成员缺失（版本变化？）" : null);
				return;
			}
			flag = true;
			SDS_CompatPatchReport sDS_CompatPatchReport = new SDS_CompatPatchReport("米莉拉未来科技（Milira Expanded）");
			if (!HasOrdersAccessor())
			{
				sDS_CompatPatchReport.Missing("GameComponent_VirtualStorageNetwork.生产订单表（订单补料）", "GetProductionOrdersSnapshot / wf / vk 均不存在（版本变化？）");
			}
			MethodInfo target = SDS_CompatTools.Method(deviceType, "CompTick", Type.EmptyTypes);
			SDS_CompatTools.TryPatch(harmony, sDS_CompatPatchReport, typeof(SDS_ModCompat_MiliraExpanded), "CompVirtualProductionDevice.CompTick（自动补料）", target, "CompTickPrefix", null, "找不到 CompVirtualProductionDevice.CompTick()");
			SDS_CompatTools.TryPatch(harmony, sDS_CompatPatchReport, typeof(SDS_ModCompat_MiliraExpanded), "GameComponent_VirtualStorageNetwork.TryProcessAutomatedRecipeCycle（自动补料）", SDS_CompatTools.Method(networkType, "TryProcessAutomatedRecipeCycle", deviceType, typeof(Bill_Production), typeof(int)), "TryProcessAutomatedRecipeCyclePrefix", null, "找不到 TryProcessAutomatedRecipeCycle(CompVirtualProductionDevice,Bill_Production,int)");
			SDS_CompatTools.TryPatch(harmony, sDS_CompatPatchReport, typeof(SDS_ModCompat_MiliraExpanded), "GameComponent_VirtualStorageNetwork.TryProcessManualRecomposeBill（自动补料）", SDS_CompatTools.Method(networkType, "TryProcessManualRecomposeBill", deviceType, typeof(Bill_Production)), "TryProcessManualRecomposeBillPrefix", null, "找不到 TryProcessManualRecomposeBill(CompVirtualProductionDevice,Bill_Production)");
			SDS_CompatTools.TryPatch(harmony, sDS_CompatPatchReport, typeof(SDS_ModCompat_MiliraExpanded), "GameComponent_VirtualStorageNetwork.TryProcessRecomposeOrder（自动补料）", SDS_CompatTools.Method(networkType, "TryProcessRecomposeOrder", deviceType, typeof(int)), "TryProcessRecomposeOrderPrefix", null, "找不到 TryProcessRecomposeOrder(CompVirtualProductionDevice,int)");
			SDS_CompatTools.TryPatch(harmony, sDS_CompatPatchReport, typeof(SDS_ModCompat_MiliraExpanded), "GameComponent_VirtualStorageNetwork.TryProcessDecomposeOrder（自动补料）", (decomposerCompType == null) ? null : SDS_CompatTools.Method(networkType, "TryProcessDecomposeOrder", decomposerCompType), "TryProcessForCompPrefix", null, "找不到 TryProcessDecomposeOrder(MatterDecomposer_ThingComp)");
			SDS_CompatTools.TryPatch(harmony, sDS_CompatPatchReport, typeof(SDS_ModCompat_MiliraExpanded), "GameComponent_VirtualStorageNetwork.TryProcessLifeEssenceOrder（自动补料）", (lifeEssenceCompType == null) ? null : SDS_CompatTools.Method(networkType, "TryProcessLifeEssenceOrder", lifeEssenceCompType), "TryProcessForCompPrefix", null, "找不到 TryProcessLifeEssenceOrder(LifeEssenceExtractor_ThingComp)");
			SDS_CompatReportHub.Register("米莉拉未来科技（Milira Expanded）", "XiyueYM.MiliraExpandedXY", detected: true, sDS_CompatPatchReport, "SDS → 米莉拉：仅补自动生产 / 链接工作台 / 分解与生命精华订单缺料");
		}
		catch (Exception ex)
		{
			SDS_CompatReportHub.Register("米莉拉未来科技（Milira Expanded）", "XiyueYM.MiliraExpandedXY", flag, null, null, "应用异常 " + ex.GetType().Name);
			SDS_Diag.ErrorOnce("米莉拉未来科技适配器应用失败（已跳过，双方原行为不受影响）", ex);
		}
	}

	private static bool Resolve()
	{
		if (resolved)
		{
			return networkType != null && deviceType != null;
		}
		resolved = true;
		networkType = AccessTools.TypeByName("MiliraExpandedXY.Buildings.GameComponent_VirtualStorageNetwork");
		deviceType = AccessTools.TypeByName("MiliraExpandedXY.Buildings.CompVirtualProductionDevice");
		if (networkType == null || deviceType == null)
		{
			return false;
		}
		networkInstanceProp = SDS_CompatTools.Property(networkType, "Instance");
		operationalProp = SDS_CompatTools.Property(deviceType, "Operational");
		recipeSourceProp = SDS_CompatTools.Property(deviceType, "RecipeSource");
		getAvailableCountMethod = SDS_CompatTools.Method(networkType, "GetAvailableCount", typeof(ThingDef));
		getPreservedAvailableCountMethod = SDS_CompatTools.Method(networkType, "GetPreservedAvailableCount", typeof(ThingDef));
		tryDepositMethod = SDS_CompatTools.Method(networkType, "TryDeposit", typeof(Thing), typeof(int), typeof(int).MakeByRefType());
		Type type = AccessTools.TypeByName("MiliraExpandedXY.Buildings.FoldedStorage_ThingComp");
		canStoreAnyDefMethod = SDS_CompatTools.Method(type, "CanStoreAnyDef", typeof(ThingDef));
		canDepositDefMethod = SDS_CompatTools.Method(type, "CanDepositDef", typeof(ThingDef));
		orderType = AccessTools.TypeByName("MiliraExpandedXY.Buildings.VirtualProductionOrder");
		decomposerCompType = AccessTools.TypeByName("MiliraExpandedXY.Buildings.MatterDecomposer_ThingComp");
		lifeEssenceCompType = AccessTools.TypeByName("MiliraExpandedXY.Buildings.LifeEssenceExtractor_ThingComp");
		ordersSnapshotMethod = SDS_CompatTools.Method(networkType, "GetProductionOrdersSnapshot", Type.EmptyTypes);
		if (ordersSnapshotMethod == null)
		{
			ordersField = SDS_CompatTools.Field(networkType, "wf") ?? SDS_CompatTools.Field(networkType, "vk");
		}
		if (orderType != null)
		{
			orderRecipeField = SDS_CompatTools.Field(orderType, "recipeDef");
			orderKindField = SDS_CompatTools.Field(orderType, "kind");
			orderRemainingField = SDS_CompatTools.Field(orderType, "remainingCount");
			orderModeField = SDS_CompatTools.Field(orderType, "mode");
			orderAllowedField = SDS_CompatTools.Field(orderType, "allowedIngredients");
			orderDeviceNamesField = SDS_CompatTools.Field(orderType, "allowedDeviceDefNames");
			orderIdField = SDS_CompatTools.Field(orderType, "orderId");
			orderUsesAllowedProp = SDS_CompatTools.Property(orderType, "UsesAllowedIngredients");
			orderCorpseFilterField = SDS_CompatTools.Field(orderType, "corpseFilter");
			orderReserveMethod = SDS_CompatTools.Method(orderType, "IngredientReserveFor", typeof(ThingDef));
		}
		decomposeValueMethod = SDS_CompatTools.Method(AccessTools.TypeByName("MiliraExpandedXY.Buildings.MatterDecomposeValueResolver"), "ResolveMarketValue", typeof(ThingDef));
		Type type2 = AccessTools.TypeByName("MiliraExpandedXY.Buildings.CompProperties_MatterDecomposer");
		decConversionField = SDS_CompatTools.Field(type2, "conversionFactor");
		decEfficiencyField = SDS_CompatTools.Field(type2, "efficiencyMultiplier");
		decTargetField = SDS_CompatTools.Field(type2, "targetMatterUnitsPerCycle");
		Type type3 = AccessTools.TypeByName("MiliraExpandedXY.Buildings.CompProperties_LifeEssenceExtractor");
		leNutritionField = SDS_CompatTools.Field(type3, "nutritionPerInterval");
		leOutputField = SDS_CompatTools.Field(type3, "outputPerNutrition");
		lifeEssenceNutritionMethod = SDS_CompatTools.Method(lifeEssenceCompType, "a", typeof(ThingDef));
		matterExtensionType = AccessTools.TypeByName("MiliraExpandedXY.Buildings.MatterRecomposeRecipeExtension");
		requiredMatterMethod = SDS_CompatTools.Method(matterExtensionType, "RequiredMatterUnitsFor", typeof(Thing));
		availableStandardMatterProp = SDS_CompatTools.Property(networkType, "AvailableStandardMatterForDisplay");
		standardMatterUnitDef = DefDatabase<ThingDef>.GetNamedSilentFail("MEXY_StandardMatterUnit");
		if (networkInstanceProp == null || operationalProp == null || recipeSourceProp == null || getAvailableCountMethod == null || getPreservedAvailableCountMethod == null || tryDepositMethod == null)
		{
			networkType = null;
			deviceType = null;
			return false;
		}
		return true;
	}

	private static bool HasOrdersAccessor()
	{
		return ordersSnapshotMethod != null || ordersField != null;
	}

	private static IList GetOrders(object network)
	{
		if (network == null)
		{
			return null;
		}
		if (ordersSnapshotMethod != null)
		{
			try
			{
				return ordersSnapshotMethod.Invoke(network, null) as IList;
			}
			catch
			{
				return null;
			}
		}
		if (ordersField == null)
		{
			return null;
		}
		try
		{
			return ordersField.GetValue(network) as IList;
		}
		catch
		{
			return null;
		}
	}

	public static void TryProcessAutomatedRecipeCyclePrefix(object __instance, object[] __args)
	{
		try
		{
			object obj;
			if (__args == null || __args.Length == 0)
			{
				obj = null;
			}
			else
			{
				object obj2 = __args[0];
				obj = ((obj2 is ThingComp) ? obj2 : null);
			}
			ThingComp val = (ThingComp)obj;
			object obj3;
			if (__args == null || __args.Length <= 1)
			{
				obj3 = null;
			}
			else
			{
				object obj4 = __args[1];
				obj3 = ((obj4 is Bill_Production) ? obj4 : null);
			}
			Bill_Production val2 = (Bill_Production)obj3;
			int orderId = ((__args != null && __args.Length > 2 && __args[2] is int num) ? num : 0);
			if (val?.parent == null || !((Thing)val.parent).Spawned || ((Thing)val.parent).Map == null)
			{
				return;
			}
			object obj5 = __instance ?? networkInstanceProp?.GetValue(null, null);
			if (obj5 != null)
			{
				if (val2 != null)
				{
					EnsureBillIngredients(obj5, (Bill)(object)val2, (Thing)(object)val.parent, ((Thing)val.parent).Map);
				}
				else
				{
					EnsureOrderByIdOrScan(obj5, val, (Thing)(object)val.parent, GetRecipeSource(val, (Thing)(object)val.parent), orderId, ((Thing)val.parent).Map);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("米莉拉自动生产补料失败（已忽略，原逻辑不受影响）", exception);
		}
	}

	public static void TryProcessManualRecomposeBillPrefix(object __instance, object[] __args)
	{
		try
		{
			object obj;
			if (__args == null || __args.Length == 0)
			{
				obj = null;
			}
			else
			{
				object obj2 = __args[0];
				obj = ((obj2 is ThingComp) ? obj2 : null);
			}
			ThingComp val = (ThingComp)obj;
			object obj3;
			if (__args == null || __args.Length <= 1)
			{
				obj3 = null;
			}
			else
			{
				object obj4 = __args[1];
				obj3 = ((obj4 is Bill_Production) ? obj4 : null);
			}
			Bill_Production bill = (Bill_Production)obj3;
			if (val?.parent != null && ((Thing)val.parent).Spawned && ((Thing)val.parent).Map != null)
			{
				object obj5 = __instance ?? networkInstanceProp?.GetValue(null, null);
				if (obj5 != null)
				{
					EnsureBillIngredients(obj5, (Bill)(object)bill, (Thing)(object)val.parent, ((Thing)val.parent).Map);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("米莉拉手动重组补料失败（已忽略，原逻辑不受影响）", exception);
		}
	}

	public static void TryProcessRecomposeOrderPrefix(object __instance, object[] __args)
	{
		try
		{
			object obj;
			if (__args == null || __args.Length == 0)
			{
				obj = null;
			}
			else
			{
				object obj2 = __args[0];
				obj = ((obj2 is ThingComp) ? obj2 : null);
			}
			ThingComp val = (ThingComp)obj;
			int orderId = ((__args != null && __args.Length > 1 && __args[1] is int num) ? num : 0);
			if (val?.parent != null && ((Thing)val.parent).Spawned && ((Thing)val.parent).Map != null)
			{
				object obj3 = __instance ?? networkInstanceProp?.GetValue(null, null);
				if (obj3 != null)
				{
					EnsureOrderByIdOrScan(obj3, val, (Thing)(object)val.parent, GetRecipeSource(val, (Thing)(object)val.parent), orderId, ((Thing)val.parent).Map);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("米莉拉网络订单补料失败（已忽略，原逻辑不受影响）", exception);
		}
	}

	public static void TryProcessForCompPrefix(object __instance, object[] __args)
	{
		try
		{
			object obj;
			if (__args == null || __args.Length == 0)
			{
				obj = null;
			}
			else
			{
				object obj2 = __args[0];
				obj = ((obj2 is ThingComp) ? obj2 : null);
			}
			ThingComp val = (ThingComp)obj;
			if (val?.parent != null && ((Thing)val.parent).Spawned && ((Thing)val.parent).Map != null)
			{
				object obj3 = __instance ?? networkInstanceProp?.GetValue(null, null);
				if (obj3 != null && HasOrdersAccessor())
				{
					EnsureOrderByIdOrScan(obj3, val, (Thing)(object)val.parent, (Thing)(object)val.parent, 0, ((Thing)val.parent).Map);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("米莉拉分解 / 生命精华补料失败（已忽略，原逻辑不受影响）", exception);
		}
	}

	private static Thing GetRecipeSource(object deviceComp, Thing deviceThing)
	{
		try
		{
			object? obj = recipeSourceProp?.GetValue(deviceComp, null);
			Thing val = (Thing)((obj is Thing) ? obj : null);
			return val ?? deviceThing;
		}
		catch
		{
			return deviceThing;
		}
	}

	public static void CompTickPrefix(object __instance)
	{
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__instance == null || deviceType == null || !deviceType.IsInstanceOfType(__instance))
			{
				return;
			}
			ThingComp val = (ThingComp)((__instance is ThingComp) ? __instance : null);
			if (val == null)
			{
				return;
			}
			if (!SDS_CompatTools.GetBool(operationalProp, __instance))
			{
				return;
			}
			Thing parent = (Thing)(object)val.parent;
			if (parent == null || !parent.Spawned || parent.Map == null)
			{
				return;
			}
			int thingIDNumber = parent.thingIDNumber;
			if (nextTickByDevice.TryGetValue(thingIDNumber, out var value) && GenTicks.TicksGame < value)
			{
				return;
			}
			if (nextTickByDevice.Count > 256)
			{
				nextTickByDevice.Clear();
			}
			nextTickByDevice[thingIDNumber] = GenTicks.TicksGame + 30;
			object value2 = networkInstanceProp.GetValue(null, null);
			if (value2 != null)
			{
				object? value3 = recipeSourceProp.GetValue(__instance, null);
				Thing val2 = (Thing)((value3 is Thing) ? value3 : null);
				if (val2 == null)
				{
					val2 = parent;
				}
				if (val2 != null && val2.Spawned && val2.Map != null && !val2.Map.reservationManager.OnlyReservationsForJobDef(LocalTargetInfo.op_Implicit(val2), JobDefOf.DoBill, true) && SDS_CompatEndpoint.HasAnyEndpoint(val2.Map))
				{
					EnsureAutoDeviceStock(value2, __instance, parent, val2, val2.Map);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("米莉拉未来科技兼容（自动补料）失败（已忽略，米莉拉行为不受影响）", exception);
		}
	}

	private static bool EnsureAutoDeviceStock(object network, object deviceComp, Thing deviceThing, Thing source, Map map)
	{
		IBillGiver val = (IBillGiver)(object)((source is IBillGiver) ? source : null);
		if (((val != null) ? val.BillStack : null) != null)
		{
			List<Bill> bills = val.BillStack.Bills;
			for (int i = 0; i < bills.Count; i++)
			{
				Bill obj = bills[i];
				Bill_Production val2 = (Bill_Production)(object)((obj is Bill_Production) ? obj : null);
				if (val2 != null && !((Bill)val2).suspended && ((Bill)val2).ShouldDoNow() && !(((object)val2).GetType().Name == "Bill_VirtualProductionOrder") && EnsureBillIngredients(network, (Bill)(object)val2, deviceThing, map))
				{
					return true;
				}
			}
		}
		return EnsureOrderIngredients(network, deviceComp, deviceThing, source, map);
	}

	private static bool EnsureOrderByIdOrScan(object network, object deviceComp, Thing deviceThing, Thing source, int orderId, Map map)
	{
		if (orderId > 0 && HasOrdersAccessor() && orderType != null && orderIdField != null)
		{
			IList orders = GetOrders(network);
			if (orders != null)
			{
				for (int i = 0; i < orders.Count; i++)
				{
					object obj = orders[i];
					if (obj != null && orderType.IsInstanceOfType(obj))
					{
						int num;
						try
						{
							num = ((orderIdField.GetValue(obj) is int num2) ? num2 : (-1));
						}
						catch
						{
							continue;
						}
						if (num == orderId)
						{
							return EnsureOrder(network, deviceComp, deviceThing, source, obj, map);
						}
					}
				}
			}
		}
		return EnsureOrderIngredients(network, deviceComp, deviceThing, source, map);
	}

	private static bool EnsureOrderIngredients(object network, object deviceComp, Thing deviceThing, Thing source, Map map)
	{
		if (!HasOrdersAccessor() || orderType == null || orderRecipeField == null || orderRemainingField == null || orderModeField == null || orderAllowedField == null || orderDeviceNamesField == null)
		{
			return false;
		}
		string value = ((Def)(deviceThing?.def?)).defName;
		string value2 = ((Def)(source?.def?)).defName;
		if (string.IsNullOrEmpty(value) && string.IsNullOrEmpty(value2))
		{
			return false;
		}
		IList orders = GetOrders(network);
		if (orders == null || orders.Count == 0)
		{
			return false;
		}
		for (int i = 0; i < orders.Count; i++)
		{
			object obj = orders[i];
			if (obj != null && orderType.IsInstanceOfType(obj) && EnsureOrder(network, deviceComp, deviceThing, source, obj, map))
			{
				return true;
			}
		}
		return false;
	}

	private static bool EnsureOrder(object network, object deviceComp, Thing deviceThing, Thing source, object order, Map map)
	{
		try
		{
			string text = orderModeField.GetValue(order)?.ToString();
			int num = ((orderRemainingField.GetValue(order) is int num2) ? num2 : 0);
			if (text != "Forever" && num <= 0)
			{
				return false;
			}
			string item = ((Def)(deviceThing?.def?)).defName;
			string item2 = ((Def)(source?.def?)).defName;
			if (!(orderDeviceNamesField.GetValue(order) is List<string> { Count: not 0 } list) || (!list.Contains(item) && !list.Contains(item2)))
			{
				return false;
			}
			object? value = orderRecipeField.GetValue(order);
			RecipeDef val = (RecipeDef)((value is RecipeDef) ? value : null);
			if (val == null)
			{
				return EnsureNonRecipeOrderIngredients(network, order, deviceComp, map);
			}
			if (FindMatterExtension(val) != null)
			{
				return EnsureStandardMatterForRecipe(network, val, deviceThing, map);
			}
			List<ThingDef> list2 = orderAllowedField.GetValue(order) as List<ThingDef>;
			bool flag = list2 != null && list2.Count > 0;
			if (orderUsesAllowedProp != null)
			{
				try
				{
					flag = (bool)orderUsesAllowedProp.GetValue(order, null);
				}
				catch
				{
				}
			}
			if (flag && (list2 == null || list2.Count == 0))
			{
				return false;
			}
			if (!flag)
			{
				list2 = null;
			}
			object? obj2 = orderCorpseFilterField?.GetValue(order);
			ThingFilter corpseFilter = (ThingFilter)((obj2 is ThingFilter) ? obj2 : null);
			Func<ThingDef, int> reserveProvider = ((orderReserveMethod != null) ? ((Func<ThingDef, int>)((ThingDef def) => GetOrderReserve(order, def))) : null);
			return EnsureRecipeIngredients(network, val, list2, null, map, corpseFilter, reserveProvider);
		}
		catch
		{
			return false;
		}
	}

	private static bool EnsureNonRecipeOrderIngredients(object network, object order, object deviceComp, Map map)
	{
		if (orderKindField == null || orderAllowedField == null)
		{
			return false;
		}
		string text;
		try
		{
			text = orderKindField.GetValue(order)?.ToString();
		}
		catch
		{
			return false;
		}
		if (text != "Decompose" && text != "LifeEssenceExtraction")
		{
			return false;
		}
		object deviceComp2 = deviceComp;
		Thing thing = (Thing)(object)((ThingComp)(((deviceComp is ThingComp) ? deviceComp : null)?)).parent;
		if (text == "Decompose" && decomposerCompType != null)
		{
			deviceComp2 = FindCompOfType(thing, decomposerCompType) ?? deviceComp;
		}
		else if (text == "LifeEssenceExtraction" && lifeEssenceCompType != null)
		{
			deviceComp2 = FindCompOfType(thing, lifeEssenceCompType) ?? deviceComp;
		}
		List<ThingDef> list;
		try
		{
			list = orderAllowedField.GetValue(order) as List<ThingDef>;
		}
		catch
		{
			return false;
		}
		if (list == null || list.Count == 0)
		{
			return false;
		}
		int remaining;
		try
		{
			remaining = ((orderRemainingField?.GetValue(order) is int num) ? num : 0);
		}
		catch
		{
			remaining = 0;
		}
		ThingFilter val = null;
		try
		{
			object? obj4 = orderCorpseFilterField?.GetValue(order);
			val = (ThingFilter)((obj4 is ThingFilter) ? obj4 : null);
		}
		catch
		{
		}
		Dictionary<ThingDef, int> pool = new Dictionary<ThingDef, int>();
		for (int i = 0; i < list.Count; i++)
		{
			ThingDef val2 = list[i];
			if (val2 == null || (val2.IsCorpse && val != null && !val.Allows(val2)) || !CanStoreDef(val2))
			{
				continue;
			}
			int ownCount = GetOwnCount(network, val2, pool);
			int orderReserve = GetOrderReserve(order, val2);
			int val3 = ((text == "Decompose") ? EstimateDecomposeBatch(deviceComp2, val2, remaining) : EstimateLifeEssenceBatch(deviceComp2, val2, remaining));
			int num2 = orderReserve + Math.Max(1, val3);
			if (ownCount >= num2)
			{
				return true;
			}
			int num3 = num2 - ownCount;
			long num4 = SDS_CompatStock.CountAvailableVirtual(val2) + SDS_CompatStock.CountAvailablePreserved(val2);
			if (num4 > 0)
			{
				int count = (int)Math.Min(num3, num4);
				TryWithdrawAndDeposit(network, val2, count, map, out var deposited);
				if (deposited > 0)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static int EstimateDecomposeBatch(object deviceComp, ThingDef def, int remaining)
	{
		float num = 0f;
		if (decomposeValueMethod != null)
		{
			try
			{
				num = (float)decomposeValueMethod.Invoke(null, new object[1] { def });
			}
			catch
			{
				num = 0f;
			}
		}
		if (num <= 0f)
		{
			num = ((def != null) ? def.BaseMarketValue : 0f);
		}
		if (num <= 0f)
		{
			return 1;
		}
		CompProperties val = ((ThingComp)(((deviceComp is ThingComp) ? deviceComp : null)?)).props;
		if (val == null || decConversionField == null || decEfficiencyField == null || decTargetField == null)
		{
			return 1;
		}
		try
		{
			float num2 = (float)decConversionField.GetValue(val);
			float num3 = (float)decEfficiencyField.GetValue(val);
			float num4 = (float)decTargetField.GetValue(val);
			float num5 = num * Mathf.Max(0f, num2) * Mathf.Max(0f, num3) / 10f;
			if (num5 <= 0.0001f)
			{
				return 1;
			}
			float num6 = ((remaining <= 0) ? Mathf.Max(0.01f, num4) : Mathf.Min((float)remaining, Mathf.Max(0.01f, num4)));
			return Mathf.Clamp(Mathf.CeilToInt(num6 / num5), 1, 200);
		}
		catch
		{
			return 1;
		}
	}

	private static int EstimateLifeEssenceBatch(object deviceComp, ThingDef def, int remaining)
	{
		if (def == null || def.IsCorpse)
		{
			return 1;
		}
		CompProperties val = ((ThingComp)(((deviceComp is ThingComp) ? deviceComp : null)?)).props;
		if (val == null || leNutritionField == null || leOutputField == null || lifeEssenceNutritionMethod == null)
		{
			return 1;
		}
		try
		{
			float num = (float)lifeEssenceNutritionMethod.Invoke(null, new object[1] { def });
			if (num <= 0.0001f)
			{
				return 1;
			}
			float num2 = Mathf.Max(0.01f, (float)leNutritionField.GetValue(val));
			float num3 = Mathf.Max(0.01f, (float)leOutputField.GetValue(val));
			float num4 = num * num3;
			if (num4 <= 0.0001f)
			{
				return 1;
			}
			float num5 = num2 * num3;
			float num6 = ((remaining <= 0) ? num5 : Mathf.Min((float)remaining, num5));
			return Mathf.Clamp(Mathf.CeilToInt(num6 / num4), 1, 200);
		}
		catch
		{
			return 1;
		}
	}

	private static object FindCompOfType(Thing thing, Type compType)
	{
		if (thing == null || compType == null)
		{
			return null;
		}
		Thing obj = ((thing is ThingWithComps) ? thing : null);
		List<ThingComp> list = ((obj != null) ? ((ThingWithComps)obj).AllComps : null);
		if (list == null)
		{
			return null;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] != null && compType.IsInstanceOfType(list[i]))
			{
				return list[i];
			}
		}
		return null;
	}

	private static int GetOrderReserve(object order, ThingDef def)
	{
		if (orderReserveMethod == null || order == null || def == null)
		{
			return 0;
		}
		try
		{
			return (int)orderReserveMethod.Invoke(order, new object[1] { def });
		}
		catch
		{
			return 0;
		}
	}

	private static object FindMatterExtension(RecipeDef recipe)
	{
		if (((Def)(recipe?)).modExtensions == null || matterExtensionType == null)
		{
			return null;
		}
		for (int i = 0; i < ((Def)recipe).modExtensions.Count; i++)
		{
			object obj = ((Def)recipe).modExtensions[i];
			if (obj != null && matterExtensionType.IsInstanceOfType(obj))
			{
				return obj;
			}
		}
		return null;
	}

	private static float GetRequiredMatter(object extension, Thing deviceThing)
	{
		if (requiredMatterMethod == null || extension == null || deviceThing == null)
		{
			return 0f;
		}
		try
		{
			return (float)requiredMatterMethod.Invoke(extension, new object[1] { deviceThing });
		}
		catch
		{
			return 0f;
		}
	}

	private static double GetAvailableStandardMatter(object network)
	{
		if (availableStandardMatterProp == null || network == null)
		{
			return 0.0;
		}
		try
		{
			return (availableStandardMatterProp.GetValue(network, null) is double num) ? num : 0.0;
		}
		catch
		{
			return 0.0;
		}
	}

	private static bool EnsureStandardMatterForRecipe(object network, RecipeDef recipe, Thing deviceThing, Map map)
	{
		object obj = FindMatterExtension(recipe);
		if (obj == null)
		{
			return true;
		}
		if (requiredMatterMethod == null)
		{
			return false;
		}
		if (availableStandardMatterProp == null)
		{
			return false;
		}
		float requiredMatter = GetRequiredMatter(obj, deviceThing);
		if (requiredMatter <= 0f)
		{
			return true;
		}
		double availableStandardMatter = GetAvailableStandardMatter(network);
		if (availableStandardMatter + 0.0001 >= (double)requiredMatter)
		{
			return true;
		}
		int count = Mathf.CeilToInt((float)((double)requiredMatter - availableStandardMatter));
		ThingDef val = standardMatterUnitDef ?? DefDatabase<ThingDef>.GetNamedSilentFail("MEXY_StandardMatterUnit");
		if (val == null)
		{
			return false;
		}
		if (!TryWithdrawAndDeposit(network, val, count, map, out var _))
		{
			return false;
		}
		return GetAvailableStandardMatter(network) + 0.0001 >= (double)requiredMatter;
	}

	private static bool EnsureBillIngredients(object network, Bill bill, Thing deviceThing, Map map)
	{
		if (bill == null || bill.recipe == null)
		{
			return false;
		}
		if (FindMatterExtension(bill.recipe) != null)
		{
			return EnsureStandardMatterForRecipe(network, bill.recipe, deviceThing, map);
		}
		return EnsureRecipeIngredients(network, bill.recipe, null, bill, map);
	}

	private static bool EnsureRecipeIngredients(object network, RecipeDef recipe, List<ThingDef> allowedIngredients, Bill bill, Map map, ThingFilter corpseFilter = null, Func<ThingDef, int> reserveProvider = null)
	{
		if (recipe == null || recipe.ingredients == null || recipe.ingredients.Count == 0)
		{
			return false;
		}
		if (recipe.allowMixingIngredients)
		{
			return false;
		}
		Dictionary<ThingDef, int> pool = new Dictionary<ThingDef, int>();
		HashSet<ThingDef> hashSet = ((reserveProvider != null) ? new HashSet<ThingDef>() : null);
		for (int i = 0; i < recipe.ingredients.Count; i++)
		{
			IngredientCount val = recipe.ingredients[i];
			if (val == null || val.filter == null)
			{
				continue;
			}
			int num = Mathf.CeilToInt(val.GetBaseCount());
			if (num <= 0)
			{
				continue;
			}
			int chosenOwn;
			ThingDef val2 = SDS_CompatIngredientRule.ChooseDef(val, bill, recipe, allowedIngredients, corpseFilter, (ThingDef def) => GetOwnCount(network, def, pool), (ThingDef def) => SDS_CompatStock.CountAvailableVirtual(def) + SDS_CompatStock.CountAvailablePreserved(def), num, CanStoreDef, out chosenOwn);
			if (val2 == null)
			{
				return false;
			}
			int num2 = num;
			if (reserveProvider != null && hashSet != null && hashSet.Add(val2))
			{
				int num3 = reserveProvider(val2);
				if (num3 > 0)
				{
					num2 += num3;
				}
			}
			int num4 = Math.Min(num2, chosenOwn);
			pool[val2] = Math.Max(0, chosenOwn - num4);
			int num5 = num2 - num4;
			if (num5 > 0)
			{
				if (!TryWithdrawAndDeposit(network, val2, num5, map, out var deposited))
				{
					return false;
				}
				pool[val2] = Math.Max(0, pool[val2] + deposited);
			}
		}
		return true;
	}

	private static bool CanStoreDef(ThingDef def)
	{
		if (def == null)
		{
			return false;
		}
		if (canStoreCache.TryGetValue(def, out var value))
		{
			return value;
		}
		MethodInfo methodInfo = ((def.IsCorpse || SDS_DepositRules.RequiresPreservedStorage(def)) ? canStoreAnyDefMethod : (canDepositDefMethod ?? canStoreAnyDefMethod));
		bool flag = true;
		if (methodInfo != null)
		{
			try
			{
				flag = (bool)methodInfo.Invoke(null, new object[1] { def });
			}
			catch
			{
				flag = true;
			}
		}
		canStoreCache[def] = flag;
		return flag;
	}

	private static int GetOwnCount(object network, ThingDef def, Dictionary<ThingDef, int> pool)
	{
		if (pool.TryGetValue(def, out var value))
		{
			return value;
		}
		long num = 0L;
		try
		{
			num += (long)getAvailableCountMethod.Invoke(network, new object[1] { def });
		}
		catch
		{
		}
		try
		{
			num += (long)getPreservedAvailableCountMethod.Invoke(network, new object[1] { def });
		}
		catch
		{
		}
		return pool[def] = (int)((num > int.MaxValue) ? int.MaxValue : Math.Max(0L, num));
	}

	private static bool TryWithdrawAndDeposit(object network, ThingDef def, int count, Map map, out int deposited)
	{
		deposited = 0;
		int num = count;
		if (SDS_CompatStock.TryWithdrawVirtualByDef(def, num, map, out var made))
		{
			int num2 = DepositAll(network, made, map, preservedSource: false);
			deposited += num2;
			num -= num2;
		}
		if (num > 0 && SDS_CompatStock.TryWithdrawPreservedByDef(def, num, map, out var made2))
		{
			int num3 = DepositAll(network, made2, map, preservedSource: true);
			deposited += num3;
			num -= num3;
		}
		return num <= 0;
	}

	private static int DepositAll(object network, List<Thing> things, Map map, bool preservedSource)
	{
		if (things == null || things.Count == 0)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < things.Count; i++)
		{
			Thing val = things[i];
			if (val == null || val.Destroyed)
			{
				continue;
			}
			int stackCount = val.stackCount;
			if (stackCount > 0)
			{
				int num2 = 0;
				try
				{
					object[] array = new object[3] { val, stackCount, 0 };
					tryDepositMethod.Invoke(network, array);
					num2 = (int)array[2];
				}
				catch (Exception exception)
				{
					SDS_Diag.ErrorOnce("米莉拉未来科技兼容（存入网络）失败（已忽略）", exception);
				}
				if (num2 > 0)
				{
					num += Math.Min(num2, stackCount);
				}
				int num3 = stackCount - Math.Max(0, num2);
				if (num3 > 0 && val != null && !val.Destroyed && val.stackCount > 0)
				{
					ReturnOne(val, map, preservedSource);
				}
			}
		}
		return num;
	}

	private static void ReturnOne(Thing thing, Map map, bool preservedSource)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (thing != null && !thing.Destroyed)
		{
			if (preservedSource)
			{
				SDS_API.TryReturnPreserved(thing, map, IntVec3.Invalid);
			}
			else
			{
				SDS_API.TryReturnVirtual(thing, SDS_StockKey.FromThing(thing), map, IntVec3.Invalid);
			}
		}
	}
}
internal static class SDS_ModCompat_PUAH
{
	private const string WorkGiverTypeName = "PickUpAndHaul.WorkGiver_HaulToInventory";

	private const string HauledCompTypeName = "PickUpAndHaul.CompHauledToInventory";

	private const string UnloadCheckerTypeName = "PickUpAndHaul.PawnUnloadChecker";

	private const string UnloadCheckMethodName = "CheckIfPawnShouldUnloadInventory";

	private static bool hauledLookupDone;

	private static Type hauledCompType;

	private static MethodInfo hauledSetGetter;

	public static void TryApply(Harmony harmony)
	{
		bool flag = false;
		try
		{
			if (harmony != null)
			{
				Type type = AccessTools.TypeByName("PickUpAndHaul.WorkGiver_HaulToInventory");
				if (type == null)
				{
					flag = AccessTools.TypeByName("PickUpAndHaul.CompHauledToInventory") != null || AccessTools.TypeByName("PickUpAndHaul.PawnUnloadChecker") != null;
					SDS_CompatReportHub.Register("PickUp and Haul", "Mehni.PickUpAndHaul", flag, null, null, flag ? "找不到 WorkGiver_HaulToInventory（版本 / 命名变化？）" : null);
					return;
				}
				flag = true;
				SDS_CompatPatchReport report = new SDS_CompatPatchReport("PickUp and Haul");
				Type[] array = new Type[3]
				{
					typeof(Pawn),
					typeof(Thing),
					typeof(bool)
				};
				Patch(report, harmony, "WorkGiver_HaulToInventory.HasJobOnThing（候选闸门）", AccessTools.Method(type, "HasJobOnThing", array, (Type[])null), null, "HasJobOnThingPostfix", "找不到方法 HasJobOnThing(Pawn,Thing,bool)");
				Patch(report, harmony, "WorkGiver_HaulToInventory.JobOnThing（派活替换）", AccessTools.Method(type, "JobOnThing", array, (Type[])null), null, "JobOnThingPostfix", "找不到方法 JobOnThing(Pawn,Thing,bool)");
				Type type2 = AccessTools.TypeByName("PickUpAndHaul.PawnUnloadChecker");
				Patch(report, harmony, "PawnUnloadChecker.CheckIfPawnShouldUnloadInventory（卸载派活）", (type2 != null) ? AccessTools.Method(type2, "CheckIfPawnShouldUnloadInventory", new Type[2]
				{
					typeof(Pawn),
					typeof(bool)
				}, (Type[])null) : null, "UnloadDispatchPrefix", null, (type2 == null) ? "找不到类型 PickUpAndHaul.PawnUnloadChecker" : "找不到方法 CheckIfPawnShouldUnloadInventory(Pawn,bool)");
				SDS_CompatReportHub.Register("PickUp and Haul", "Mehni.PickUpAndHaul", detected: true, report);
			}
		}
		catch (Exception ex)
		{
			SDS_CompatReportHub.Register("PickUp and Haul", "Mehni.PickUpAndHaul", flag, null, null, "应用异常 " + ex.GetType().Name);
			SDS_Diag.ErrorOnce("PickUpAndHaul 兼容补丁应用失败（已跳过，原版行为不受影响）", ex);
		}
	}

	private static void Patch(SDS_CompatPatchReport report, Harmony harmony, string hook, MethodInfo target, string prefixName, string postfixName, string missingReason)
	{
		SDS_CompatTools.TryPatch(harmony, report, typeof(SDS_ModCompat_PUAH), hook, target, prefixName, postfixName, missingReason);
	}

	public static void HasJobOnThingPostfix(Pawn pawn, Thing thing, bool forced, ref bool __result)
	{
		try
		{
			if (thing != null)
			{
				SDS_ApiResultCode code;
				if (SDS_API.IsProducedPendingConsumption(thing))
				{
					__result = false;
				}
				else if (!__result && SDS_API.TryTakeDepositCandidate(pawn, thing, forced, out code))
				{
					__result = true;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("PickUpAndHaul 兼容（HasJobOnThing）失败（已忽略，PUAH 行为不受影响）", exception);
		}
	}

	public static void JobOnThingPostfix(Pawn pawn, Thing thing, bool forced, ref Job __result)
	{
		try
		{
			if (thing != null)
			{
				Job job;
				SDS_ApiResultCode code;
				if (SDS_API.IsProducedPendingConsumption(thing))
				{
					__result = null;
				}
				else if (!InPuahCarryLoop(pawn) && (__result == null || __result.def != SDS_JobDefOf.DepositToGrid) && SDS_API.TryMakeDepositJob(pawn, thing, forced, __result, multi: true, out job, out code))
				{
					__result = job;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("PickUpAndHaul 兼容（JobOnThing）失败（已忽略，PUAH 行为不受影响）", exception);
		}
	}

	public static bool UnloadDispatchPrefix(Pawn pawn, bool forced)
	{
		try
		{
			if (pawn == null || pawn.jobs == null || pawn.jobs.jobQueue == null)
			{
				return true;
			}
			if (!SDS_API.IsPawnPortOperational(pawn))
			{
				return true;
			}
			if (!TryGetHauledThings(pawn, out var hauled))
			{
				return true;
			}
			if (!SDS_API.TryMakeDepositJobFromInventory(pawn, hauled, out var job, out var _))
			{
				return true;
			}
			if (!job.TryMakePreToilReservations(pawn, false))
			{
				return true;
			}
			pawn.jobs.jobQueue.EnqueueFirst(job, (JobTag?)(JobTag)0);
			return false;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("PickUpAndHaul 兼容（卸载派活）失败（已忽略，PUAH 行为不受影响）", exception);
			return true;
		}
	}

	private static bool TryGetHauledThings(Pawn pawn, out List<Thing> hauled)
	{
		hauled = null;
		if (pawn == null || ((ThingWithComps)pawn).AllComps == null || ((ThingWithComps)pawn).AllComps.Count == 0)
		{
			return false;
		}
		if (!hauledLookupDone)
		{
			hauledLookupDone = true;
			hauledCompType = AccessTools.TypeByName("PickUpAndHaul.CompHauledToInventory");
			hauledSetGetter = ((hauledCompType != null) ? AccessTools.Method(hauledCompType, "GetHashSet", (Type[])null, (Type[])null) : null);
		}
		if (hauledCompType == null || hauledSetGetter == null)
		{
			return false;
		}
		for (int i = 0; i < ((ThingWithComps)pawn).AllComps.Count; i++)
		{
			ThingComp val = ((ThingWithComps)pawn).AllComps[i];
			if (val != null && hauledCompType.IsInstanceOfType(val))
			{
				if (!(hauledSetGetter.Invoke(val, null) is IEnumerable<Thing> collection))
				{
					return false;
				}
				hauled = new List<Thing>(collection);
				return hauled.Count > 0;
			}
		}
		return false;
	}

	private static bool InPuahCarryLoop(Pawn pawn)
	{
		Job val = ((pawn != null && pawn.jobs != null) ? pawn.jobs.curJob : null);
		if (val == null || val.def == null)
		{
			return false;
		}
		return ((Def)val.def).defName == "HaulToInventory";
	}
}
internal static class SDS_ModCompat_UFRecipes
{
	private static Type autoUtilType;

	private static bool resolved;

	public static void TryApply(Harmony harmony)
	{
		bool flag = false;
		try
		{
			flag = AccessTools.TypeByName("UFRecipes.Building_SuperWorkbench") != null || AccessTools.TypeByName("UFRecipes.AutoSequenceUtil") != null;
			if (!Resolve())
			{
				SDS_CompatReportHub.Register("联合轻工（UFRecipes）", "KindSeal.UFLI", flag, null, null, flag ? "关键类型缺失（版本变化？）" : null);
				return;
			}
			flag = true;
			SDS_CompatPatchReport report = new SDS_CompatPatchReport("联合轻工（UFRecipes）");
			MethodInfo target = SDS_CompatTools.Method(autoUtilType, "TryPullIngredientsFromCandidates", typeof(Bill), typeof(List<Thing>), typeof(Map), typeof(ThingOwner), typeof(bool).MakeByRefType());
			SDS_CompatTools.TryPatch(harmony, report, typeof(SDS_ModCompat_UFRecipes), "AutoSequenceUtil.TryPullIngredientsFromCandidates（全自动·保真兜底）", target, null, "PullCandidatesPostfix", "找不到 TryPullIngredientsFromCandidates(Bill,List<Thing>,Map,ThingOwner,out bool)");
			SDS_CompatReportHub.Register("联合轻工（UFRecipes）", "KindSeal.UFLI", detected: true, report);
		}
		catch (Exception ex)
		{
			SDS_CompatReportHub.Register("联合轻工（UFRecipes）", "KindSeal.UFLI", flag, null, null, "应用异常 " + ex.GetType().Name);
			SDS_Diag.ErrorOnce("联合轻工适配器应用失败（已跳过，双方原行为不受影响）", ex);
		}
	}

	private static bool Resolve()
	{
		if (resolved)
		{
			return autoUtilType != null;
		}
		resolved = true;
		autoUtilType = AccessTools.TypeByName("UFRecipes.AutoSequenceUtil");
		return autoUtilType != null;
	}

	public static void PullCandidatesPostfix(Bill bill, Map map, ThingOwner destination, ref bool __result)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__result || bill == null || destination == null)
			{
				return;
			}
			IThingHolder owner = destination.Owner;
			Thing val = (Thing)(object)((owner is Thing) ? owner : null);
			if (val != null && val.Spawned && val.Map != null)
			{
				Map map2 = map ?? val.Map;
				Building val2 = (Building)(object)((val is Building) ? val : null);
				IntVec3 val3 = ((val2 != null && val.def != null && val.def.hasInteractionCell) ? ((Thing)val2).InteractionCell : val.Position);
				if (SDS_CompatEndpoint.HasUsableBuildingEndpointWithin(map2, val3, bill.ingredientSearchRadius) && SDS_CompatBillSupply.TrySupplyPreservedBill(bill, map2, val3, destination, out var made) && made != null && made.Count != 0)
				{
					__result = true;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("联合轻工兼容（全自动保真供料）失败（已忽略，打印机行为不受影响）", exception);
		}
	}
}
internal static class SDS_PreservedBillDispatch
{
	private static MethodInfo findInSet;

	private static MethodInfo findInSetNoMix;

	private static bool TryGetFinder()
	{
		if (findInSet != null)
		{
			return true;
		}
		findInSet = AccessTools.Method(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet", new Type[6]
		{
			typeof(List<Thing>),
			typeof(Bill),
			typeof(List<ThingCount>),
			typeof(IntVec3),
			typeof(bool),
			typeof(List<IngredientCount>)
		}, (Type[])null);
		return findInSet != null;
	}

	private static bool TryGetNoMixFinder()
	{
		if (findInSetNoMix != null)
		{
			return true;
		}
		findInSetNoMix = AccessTools.Method(typeof(WorkGiver_DoBill), "TryFindBestIngredientsInSet_NoMixHelper", new Type[7]
		{
			typeof(List<Thing>),
			typeof(List<IngredientCount>),
			typeof(List<ThingCount>),
			typeof(IntVec3),
			typeof(bool),
			typeof(List<IngredientCount>),
			typeof(Bill)
		}, (Type[])null);
		return findInSetNoMix != null;
	}

	private static bool CollectIngredientMirrors(Map map, Pawn pawn, Predicate<Thing> predicate, List<Thing> candidates)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		SDS_SearchCore.CollectMirrorCandidates(map, default(ThingRequest), null, candidates);
		for (int num = candidates.Count - 1; num >= 0; num--)
		{
			Thing val = candidates[num];
			if (val == null || val.Destroyed || !val.Spawned || ForbidUtility.IsForbidden(val, pawn) || !ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(val), 1, -1, (ReservationLayerDef)null, false) || (predicate != null && !predicate(val)))
			{
				candidates.RemoveAt(num);
			}
		}
		return candidates.Count > 0;
	}

	public static bool TrySupplyVirtualIngredients(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen, List<IngredientCount> missingIngredients)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		if (!CanVirtualFallback(bill, pawn, billGiver, chosen))
		{
			return false;
		}
		Map map = billGiver.Map ?? ((Thing)pawn).Map;
		List<Thing> list = new List<Thing>();
		if (!CollectIngredientMirrors(map, pawn, (Predicate<Thing>)bill.IsFixedOrAllowedIngredient, list) || !TryGetFinder())
		{
			return false;
		}
		chosen.Clear();
		missingIngredients?.Clear();
		bool flag;
		try
		{
			flag = (bool)findInSet.Invoke(null, new object[6]
			{
				list,
				bill,
				chosen,
				RootCell(billGiver, pawn),
				false,
				missingIngredients
			});
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("虚拟账本配料调用原版算法失败（已忽略，原版行为不受影响）", exception);
			chosen.Clear();
			return false;
		}
		if (!flag || chosen.Count == 0)
		{
			chosen.Clear();
			return false;
		}
		missingIngredients?.Clear();
		return true;
	}

	public static bool TrySupplyVirtualFixedIngredients(List<IngredientCount> ingredients, Pawn pawn, Thing ingredientDestination, List<ThingCount> chosen, float searchRadius)
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		if (ingredients == null || ingredients.Count == 0 || pawn == null || ingredientDestination == null || chosen == null)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		Map val = ingredientDestination.Map ?? ((Thing)pawn).Map;
		if (instance == null || val == null || !instance.Available || !SDS_OutputUtility.MapExposesGrid(val) || !HasReachableEndpointWithin(pawn, RootCell(ingredientDestination, pawn), searchRadius))
		{
			return false;
		}
		List<Thing> list = new List<Thing>();
		if (!CollectIngredientMirrors(val, pawn, (Thing t) => AnyIngredientAllows(ingredients, t), list) || !TryGetNoMixFinder())
		{
			return false;
		}
		chosen.Clear();
		bool flag;
		try
		{
			flag = (bool)findInSetNoMix.Invoke(null, new object[7]
			{
				list,
				ingredients,
				chosen,
				RootCell(ingredientDestination, pawn),
				false,
				null,
				null
			});
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("虚拟账本固定配料调用原版算法失败（已忽略，原版行为不受影响）", exception);
			chosen.Clear();
			return false;
		}
		if (!flag || chosen.Count == 0)
		{
			chosen.Clear();
			return false;
		}
		return true;
	}

	private static bool CanVirtualFallback(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen)
	{
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		if (bill == null || pawn == null || billGiver == null || chosen == null || bill.recipe == null || bill.recipe.ingredients == null || bill.recipe.ingredients.Count == 0)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		Map val = billGiver.Map ?? ((Thing)pawn).Map;
		return instance != null && val != null && instance.Available && SDS_OutputUtility.MapExposesGrid(val) && HasReachableEndpointWithin(pawn, RootCell(billGiver, pawn), bill.ingredientSearchRadius);
	}

	private static bool HasReachableEndpointWithin(Pawn pawn, IntVec3 reference, float radius)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null)
		{
			return false;
		}
		float radiusSquared = Math.Max(0f, radius) * Math.Max(0f, radius);
		SDS_EndpointQuery query = SDS_EndpointQuery.WithdrawPoint(pawn);
		query.reference = reference;
		Predicate<SDS_EndpointHandle> filter = delegate(SDS_EndpointHandle handle)
		{
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			if (handle == null)
			{
				return false;
			}
			if (handle.IsSelf)
			{
				IntVec3 val = ((Thing)pawn).Position - reference;
				return (float)((IntVec3)(ref val)).LengthHorizontalSquared <= radiusSquared;
			}
			return handle.DistanceSquared(reference) <= radiusSquared;
		};
		SDS_EndpointHandle best;
		SDS_EndpointDecision bestDecision;
		return SDS_EndpointResolver.TryFindBest(in query, filter, out best, out bestDecision);
	}

	private static bool AnyIngredientAllows(List<IngredientCount> ingredients, Thing thing)
	{
		for (int i = 0; i < ingredients.Count; i++)
		{
			IngredientCount val = ingredients[i];
			if (val != null && val.filter != null && val.filter.Allows(thing))
			{
				return true;
			}
		}
		return false;
	}

	private static IntVec3 RootCell(Thing billGiver, Pawn pawn)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		Building val = (Building)(object)((billGiver is Building) ? billGiver : null);
		if (val != null && ((Thing)val).def != null && ((Thing)val).def.hasInteractionCell)
		{
			return ((Thing)val).InteractionCell;
		}
		if (billGiver != null)
		{
			return billGiver.Position;
		}
		return (pawn != null) ? ((Thing)pawn).Position : IntVec3.Invalid;
	}

	public static bool TrySupplyPreservedIngredients(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen, List<IngredientCount> missingIngredients)
	{
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_028f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		if (bill == null || pawn == null || billGiver == null || chosen == null || bill.recipe == null || bill.recipe.ingredients == null || bill.recipe.ingredients.Count == 0)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			return false;
		}
		if (!SDS_EndpointResolver.TryFindBest(SDS_EndpointQuery.WithdrawPoint(pawn), out var _, out var _))
		{
			return false;
		}
		if (!TryGetFinder())
		{
			return false;
		}
		List<Thing> list = new List<Thing>();
		instance.AppendPreservedThings(list);
		Dictionary<SDS_StockKey, int> dictionary = new Dictionary<SDS_StockKey, int>();
		for (int num = list.Count - 1; num >= 0; num--)
		{
			Thing val = list[num];
			if (val == null || val.Destroyed || !bill.IsFixedOrAllowedIngredient(val))
			{
				list.RemoveAt(num);
			}
			else
			{
				SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(val);
				if (sDS_StockKey == null || instance.IsForbidden(sDS_StockKey))
				{
					list.RemoveAt(num);
				}
				else
				{
					if (!dictionary.TryGetValue(sDS_StockKey, out var value))
					{
						value = (dictionary[sDS_StockKey] = instance.GetPreservedAvailableCount(sDS_StockKey));
					}
					if (value <= 0)
					{
						list.RemoveAt(num);
					}
				}
			}
		}
		if (list.Count == 0)
		{
			return false;
		}
		chosen.Clear();
		missingIngredients?.Clear();
		Building val2 = (Building)(object)((billGiver is Building) ? billGiver : null);
		IntVec3 val3 = ((val2 != null && ((Thing)val2).def != null && ((Thing)val2).def.hasInteractionCell) ? ((Thing)val2).InteractionCell : billGiver.Position);
		bool flag;
		try
		{
			flag = (bool)findInSet.Invoke(null, new object[6] { list, bill, chosen, val3, false, missingIngredients });
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真原料挑选调用原版算法失败（已忽略，原版行为不受影响）", exception);
			chosen.Clear();
			return false;
		}
		if (!flag || chosen.Count == 0)
		{
			chosen.Clear();
			return false;
		}
		float num2 = 0f;
		for (int i = 0; i < chosen.Count; i++)
		{
			ThingCount val4 = chosen[i];
			Thing thing = ((ThingCount)(ref val4)).Thing;
			val4 = chosen[i];
			int count = ((ThingCount)(ref val4)).Count;
			if (thing == null || thing.Destroyed || count <= 0)
			{
				chosen.Clear();
				return false;
			}
			num2 += (float)count * StatExtension.GetStatValue(thing, StatDefOf.Mass, true, -1);
		}
		if (chosen.Count > 1)
		{
			float statValue = StatExtension.GetStatValue((Thing)(object)pawn, StatDefOf.CarryingCapacity, true, -1);
			if (num2 > statValue + 0.01f)
			{
				chosen.Clear();
				return false;
			}
		}
		return true;
	}

	public static bool TryWrapDoBillJob(Pawn pawn, Bill bill, IBillGiver giver, ref Job result)
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		if (result != null && result.def == JobDefOf.DoBill && result.targetQueueB != null && result.targetQueueB.Count != 0 && pawn != null && bill != null)
		{
			Thing val = (Thing)(object)((giver is Thing) ? giver : null);
			if (val != null)
			{
				SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
				if (instance == null)
				{
					return false;
				}
				for (int i = 0; i < result.targetQueueB.Count; i++)
				{
					LocalTargetInfo val2 = result.targetQueueB[i];
					Thing thing = ((LocalTargetInfo)(ref val2)).Thing;
					if (thing == null || thing.Spawned || !instance.IsPreserved(thing))
					{
						return false;
					}
				}
				JobDef withdrawPreservedForBill = SDS_JobDefOf.WithdrawPreservedForBill;
				if (withdrawPreservedForBill == null)
				{
					return false;
				}
				if (!SDS_EndpointResolver.TryFindBest(SDS_EndpointQuery.WithdrawPoint(pawn), out var best, out var _))
				{
					result = null;
					return true;
				}
				Job val3 = JobMaker.MakeJob(withdrawPreservedForBill, LocalTargetInfo.op_Implicit(best.Thing), LocalTargetInfo.op_Implicit(val));
				val3.bill = bill;
				val3.count = 1;
				val3.haulMode = (HaulMode)2;
				val3.targetQueueB = new List<LocalTargetInfo>(result.targetQueueB);
				val3.countQueue = ((result.countQueue != null) ? new List<int>(result.countQueue) : new List<int>());
				val3.workGiverDef = result.workGiverDef;
				result = val3;
				return true;
			}
		}
		return false;
	}
}
[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
[HarmonyPriority(200)]
internal static class Patch_SDS_PreservedBillIngredients
{
	public static void Postfix(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen, List<IngredientCount> missingIngredients, ref bool __result)
	{
		try
		{
			if (!__result)
			{
				if (SDS_PreservedBillDispatch.TrySupplyVirtualIngredients(bill, pawn, billGiver, chosen, missingIngredients))
				{
					__result = true;
				}
				else if (SDS_PreservedBillDispatch.TrySupplyPreservedIngredients(bill, pawn, billGiver, chosen, missingIngredients))
				{
					__result = true;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真原料补漏失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestFixedIngredients")]
[HarmonyPriority(200)]
internal static class Patch_SDS_VirtualFixedIngredients
{
	public static void Postfix(List<IngredientCount> ingredients, Pawn pawn, Thing ingredientDestination, List<ThingCount> chosen, float searchRadius, ref bool __result)
	{
		try
		{
			if (!__result && SDS_PreservedBillDispatch.TrySupplyVirtualFixedIngredients(ingredients, pawn, ingredientDestination, chosen, searchRadius))
			{
				__result = true;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("虚拟账本固定配料补漏失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(WorkGiver_DoBill), "TryStartNewDoBillJob")]
[HarmonyPriority(200)]
internal static class Patch_SDS_PreservedBillJob
{
	public static void Postfix(Pawn pawn, Bill bill, IBillGiver giver, ref Job __result)
	{
		try
		{
			SDS_PreservedBillDispatch.TryWrapDoBillJob(pawn, bill, giver, ref __result);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真原料派活改写失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
public class JobDriver_SDS_WithdrawPreservedForBill : JobDriver
{
	private const TargetIndex WithdrawPointInd = (TargetIndex)1;

	private const TargetIndex GiverInd = (TargetIndex)2;

	private const int FailureBackoffTicks = 60;

	private void BackoffCurrentBill()
	{
		if (base.job != null && base.job.bill != null)
		{
			base.job.bill.nextTickToSearchForIngredients = GenTicks.TicksGame + 60;
		}
	}

	private bool IsSelfWithdrawPoint()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (base.job == null)
		{
			obj = null;
		}
		else
		{
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
			obj = ((LocalTargetInfo)(ref target)).Thing;
		}
		Thing val = (Thing)obj;
		return val != null && (object)val == base.pawn;
	}

	private List<ThingCount> CurrentPlan()
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		List<ThingCount> list = new List<ThingCount>();
		if (base.job == null || base.job.targetQueueB == null)
		{
			return list;
		}
		for (int i = 0; i < base.job.targetQueueB.Count; i++)
		{
			LocalTargetInfo val = base.job.targetQueueB[i];
			Thing thing = ((LocalTargetInfo)(ref val)).Thing;
			int val2 = ((base.job.countQueue == null || i >= base.job.countQueue.Count) ? 1 : base.job.countQueue[i]);
			if (thing != null)
			{
				list.Add(new ThingCount(thing, Math.Max(1, val2), false));
			}
		}
		return list;
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		if (base.pawn.carryTracker != null && base.pawn.carryTracker.CarriedThing != null)
		{
			Thing val = default(Thing);
			base.pawn.carryTracker.TryDropCarriedThing(((Thing)base.pawn).Position, (ThingPlaceMode)1, ref val, (Action<Thing, int>)null);
		}
		Thing thing = ((LocalTargetInfo)(ref base.job.targetA)).Thing;
		if (!IsSelfWithdrawPoint() && !(thing is Pawn) && !ReservationUtility.Reserve(base.pawn, base.job.targetA, base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false))
		{
			SDS_Diag.Fail("取货/清单取物点不可预留", SDS_Diag.Ctx(((Thing)base.pawn).Map, base.pawn, thing, base.job));
			BackoffCurrentBill();
			return false;
		}
		Thing thing2 = ((LocalTargetInfo)(ref base.job.targetB)).Thing;
		if (thing2 == null || thing2.Destroyed || !thing2.Spawned || thing2.Map != ((Thing)base.pawn).Map)
		{
			SDS_Diag.Fail("取货/清单工作台失效", SDS_Diag.Ctx(((Thing)base.pawn).Map, base.pawn, thing2, base.job));
			BackoffCurrentBill();
			return false;
		}
		if (!ReservationUtility.Reserve(base.pawn, LocalTargetInfo.op_Implicit(thing2), base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false))
		{
			SDS_Diag.Fail("取货/清单工作台不可预留", SDS_Diag.Ctx(((Thing)base.pawn).Map, base.pawn, thing2, base.job));
			BackoffCurrentBill();
			return false;
		}
		Building val2 = (Building)(object)((thing2 is Building) ? thing2 : null);
		if (val2 != null && ((Thing)val2).def != null && ((Thing)val2).def.hasInteractionCell && !ReservationUtility.ReserveSittableOrSpot(base.pawn, ((Thing)val2).InteractionCell, base.job, errorOnFailed))
		{
			SDS_Diag.Fail("取货/清单交互格不可预留", SDS_Diag.Ctx(((Thing)base.pawn).Map, base.pawn, thing2, base.job, "cell=" + ((object)((Thing)val2).InteractionCell/*cast due to constrained. prefix*/).ToString()));
			BackoffCurrentBill();
			return false;
		}
		return true;
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate
		{
			SDS_GameComponent_Grid.Instance?.ReleaseReservation(base.job.loadID);
		});
		ToilFailConditions.FailOnIncapable<JobDriver_SDS_WithdrawPreservedForBill>(this, PawnCapacityDefOf.Manipulation);
		if (!IsSelfWithdrawPoint())
		{
			if (((LocalTargetInfo)(ref base.job.targetA)).Thing is Pawn)
			{
				ToilFailConditions.FailOn<JobDriver_SDS_WithdrawPreservedForBill>(this, (Func<bool>)delegate
				{
					//IL_0008: Unknown result type (might be due to invalid IL or missing references)
					//IL_000d: Unknown result type (might be due to invalid IL or missing references)
					LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
					Thing thing = ((LocalTargetInfo)(ref target)).Thing;
					return thing == null || thing.Destroyed || !thing.Spawned;
				});
			}
			else
			{
				ToilFailConditions.FailOnDespawnedNullOrForbidden<JobDriver_SDS_WithdrawPreservedForBill>(this, (TargetIndex)1);
			}
		}
		yield return Toils_General.Do((Action)delegate
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			List<ThingCount> list = CurrentPlan();
			if (instance == null || list.Count == 0 || !instance.TryReservePreservedPlan(base.job, base.pawn, list))
			{
				SDS_Diag.Fail("取货/清单保真预留失败", SDS_Diag.Ctx(((Thing)base.pawn).Map, base.pawn, null, base.job, "plan=" + list.Count));
				BackoffCurrentBill();
				((JobDriver)this).EndJobWith((JobCondition)4);
			}
		});
		if (!IsSelfWithdrawPoint())
		{
			Thing point = ((LocalTargetInfo)(ref base.job.targetA)).Thing;
			PathEndMode mode = (PathEndMode)2;
			if (!(point is Pawn) && SDS_EndpointHandle.TryFromThing(point, out var handle))
			{
				mode = handle.AccessMode;
			}
			yield return Toils_Goto.GotoThing((TargetIndex)1, mode, false);
		}
		Toil materialize = ToilMaker.MakeToil("SDS_WithdrawPreservedForBill");
		materialize.initAction = MaterializeAndHandoff;
		materialize.defaultCompleteMode = (ToilCompleteMode)1;
		yield return materialize;
	}

	private void MaterializeAndHandoff()
	{
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_0289: Unknown result type (might be due to invalid IL or missing references)
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		List<ThingCount> list = CurrentPlan();
		if (instance == null || list.Count == 0 || !SDS_WithdrawExecutor.TryWithdrawPreservedExactAdopted(base.job.loadID, list, out var made) || made == null || made.Count == 0)
		{
			SDS_Diag.Fail("取货/清单保真取料失败", SDS_Diag.Ctx(((Thing)base.pawn).Map, base.pawn, null, base.job, "plan=" + list.Count + " reservation=" + base.job.loadID));
			BackoffCurrentBill();
			((JobDriver)this).EndJobWith((JobCondition)4);
			return;
		}
		SDS_ProducedThingMarker.MarkAll(made);
		Thing val = null;
		List<Thing> list2 = new List<Thing>();
		for (int i = 0; i < made.Count; i++)
		{
			Thing val2 = made[i];
			if (val2 == null || val2.Destroyed)
			{
				continue;
			}
			if (val == null && base.pawn.carryTracker != null && base.pawn.carryTracker.TryStartCarry(val2))
			{
				val = base.pawn.carryTracker.CarriedThing;
				if (val != null)
				{
					list2.Add(val);
					continue;
				}
			}
			if (base.pawn.inventory != null && base.pawn.inventory.innerContainer != null && ((ThingOwner)base.pawn.inventory.innerContainer).TryAdd(val2, false))
			{
				list2.Add(val2);
				continue;
			}
			ReturnAllToGrid(val, list2);
			BackoffCurrentBill();
			((JobDriver)this).EndJobWith((JobCondition)4);
			return;
		}
		LocalTargetInfo target = base.job.GetTarget((TargetIndex)2);
		Thing thing = ((LocalTargetInfo)(ref target)).Thing;
		if (list2.Count == 0 || thing == null || thing.Destroyed || !thing.Spawned)
		{
			ReturnAllToGrid(val, list2);
			BackoffCurrentBill();
			((JobDriver)this).EndJobWith((JobCondition)4);
			return;
		}
		if (base.job.bill != null)
		{
			base.job.bill.nextTickToSearchForIngredients = 0;
		}
		Job val3 = JobMaker.MakeJob(JobDefOf.DoBill, LocalTargetInfo.op_Implicit(thing));
		val3.targetQueueB = new List<LocalTargetInfo>();
		val3.countQueue = new List<int>();
		if (val != null)
		{
			val3.targetQueueB.Add(LocalTargetInfo.op_Implicit(val));
			val3.countQueue.Add(val.stackCount);
		}
		for (int j = 0; j < list2.Count; j++)
		{
			Thing val4 = list2[j];
			if (val4 != null && !val4.Destroyed && val4 != val)
			{
				val3.targetQueueB.Add(LocalTargetInfo.op_Implicit(val4));
				val3.countQueue.Add(val4.stackCount);
			}
		}
		val3.haulMode = (HaulMode)2;
		val3.bill = base.job.bill;
		val3.playerForced = base.job.playerForced;
		val3.workGiverDef = base.job.workGiverDef;
		base.pawn.jobs.StartJob(val3, (JobCondition)2, (ThinkNode)null, false, true, (ThinkTreeDef)null, (JobTag?)null, false, false, (bool?)true, false, true, true);
	}

	private void ReturnAllToGrid(Thing carried, List<Thing> queued)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		if (base.pawn == null)
		{
			return;
		}
		Thing val = default(Thing);
		if (carried != null && !carried.Destroyed && base.pawn.carryTracker != null && base.pawn.carryTracker.CarriedThing == carried && base.pawn.carryTracker.TryDropCarriedThing(((Thing)base.pawn).Position, (ThingPlaceMode)1, ref val, (Action<Thing, int>)null) && val != null)
		{
			SDS_ReturnUtility.Return(val, ((Thing)base.pawn).Map, ((Thing)base.pawn).Position, SDS_ReturnFallback.None);
		}
		if (queued == null || base.pawn.inventory == null || base.pawn.inventory.innerContainer == null)
		{
			return;
		}
		for (int i = 0; i < queued.Count; i++)
		{
			Thing val2 = queued[i];
			if (val2 != null && !val2.Destroyed && val2 != carried && ((ThingOwner)base.pawn.inventory.innerContainer).Contains(val2))
			{
				((ThingOwner)base.pawn.inventory.innerContainer).Remove(val2);
				SDS_ReturnUtility.Return(val2, ((Thing)base.pawn).Map, ((Thing)base.pawn).Position, SDS_ReturnFallback.Ground);
			}
		}
	}
}
internal static class SDS_PreservedOffer
{
	private sealed class OfferEntry
	{
		public int expireTick;

		public bool rotFrozen;

		public bool preservedOrigin;
	}

	private const int DefaultLifetimeTicks = 6000;

	private const int CooldownTicks = 600;

	private const int MaxEntries = 512;

	private static readonly Dictionary<Thing, OfferEntry> offers = new Dictionary<Thing, OfferEntry>();

	private static readonly Dictionary<(int pawnId, ThingDef def), int> cooldowns = new Dictionary<(int, ThingDef), int>();

	private static int Now => (Find.TickManager != null) ? Find.TickManager.TicksGame : 0;

	public static void Reset()
	{
		offers.Clear();
		cooldowns.Clear();
	}

	public static bool IsOffer(Thing thing)
	{
		return thing != null && offers.Count > 0 && offers.ContainsKey(thing);
	}

	public static bool TryOffer(Pawn pawn, Map map, Predicate<Thing> filter, Func<Thing, float> score, int lifetimeTicks, out Thing offered)
	{
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		offered = null;
		if (pawn == null || map == null || filter == null || !((Thing)pawn).Spawned)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available || !SDS_OutputUtility.MapExposesGrid(map))
		{
			return false;
		}
		if (!TryGetAccess(pawn, map, out var access))
		{
			return false;
		}
		Thing val = null;
		float num = float.MinValue;
		List<Thing> list = new List<Thing>();
		instance.AppendPreservedThings(list);
		Dictionary<SDS_StockKey, long> dictionary = new Dictionary<SDS_StockKey, long>();
		for (int i = 0; i < list.Count; i++)
		{
			Thing val2 = list[i];
			if (val2 == null || val2.Destroyed || val2.stackCount <= 0 || !filter(val2))
			{
				continue;
			}
			SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(val2);
			if (sDS_StockKey == null || sDS_StockKey.thingDef == null || instance.IsForbidden(sDS_StockKey))
			{
				continue;
			}
			if (!dictionary.TryGetValue(sDS_StockKey, out var value))
			{
				value = (dictionary[sDS_StockKey] = SDS_Availability.GetAvailableCount(sDS_StockKey));
			}
			if (value > 0 && instance.GetReservedCount(sDS_StockKey) <= 0)
			{
				float num2 = score?.Invoke(val2) ?? 0f;
				if (val == null || num2 > num)
				{
					val = val2;
					num = num2;
				}
			}
		}
		if (val == null)
		{
			return false;
		}
		if (InCooldown(pawn, val.def))
		{
			return false;
		}
		List<ThingCount> plan = new List<ThingCount>
		{
			new ThingCount(val, 1, false)
		};
		if (!SDS_WithdrawExecutor.TryWithdrawPreservedExact(plan, out var made, SDS_WithdrawMode.SpawnedTarget) || made == null || made.Count == 0)
		{
			return false;
		}
		if (TrackPlaced(pawn, map, made[0], access, lifetimeTicks, preservedOrigin: true, out offered))
		{
			return true;
		}
		if (SDS_ReturnUtility.ReturnPreserved(made[0], map, access.cell) == SDS_ReclaimOutcome.Abandoned)
		{
			SDS_Diag.Warn("保真桥/落点失败（实物已落地，未回库）", SDS_Diag.Ctx(map, pawn, made[0], null, "def=" + ((Def)val.def).defName));
		}
		return false;
	}

	public static bool TryOfferVirtual(Pawn pawn, Map map, Predicate<ThingDef> defFilter, Func<ThingDef, float> score, int lifetimeTicks, out Thing offered)
	{
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		offered = null;
		if (pawn == null || map == null || defFilter == null || !((Thing)pawn).Spawned)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available || !SDS_OutputUtility.MapExposesGrid(map))
		{
			return false;
		}
		if (!TryGetAccess(pawn, map, out var access))
		{
			return false;
		}
		SDS_StockKey sDS_StockKey = null;
		float num = float.MinValue;
		List<SDS_StockEntry> list = instance.Snapshot(0);
		for (int i = 0; i < list.Count; i++)
		{
			SDS_StockKey sDS_StockKey2 = ((list[i] != null) ? list[i].key : null);
			if (sDS_StockKey2 != null && !sDS_StockKey2.preserved && sDS_StockKey2.Resolved && sDS_StockKey2.thingDef != null && defFilter(sDS_StockKey2.thingDef) && !instance.IsForbidden(sDS_StockKey2) && SDS_Availability.GetAvailableCount(sDS_StockKey2) > 0 && instance.GetReservedCount(sDS_StockKey2) <= 0)
			{
				float num2 = score?.Invoke(sDS_StockKey2.thingDef) ?? 0f;
				if (sDS_StockKey == null || num2 > num)
				{
					sDS_StockKey = sDS_StockKey2;
					num = num2;
				}
			}
		}
		if (sDS_StockKey == null || InCooldown(pawn, sDS_StockKey.thingDef))
		{
			return false;
		}
		if (!SDS_WithdrawExecutor.TryWithdrawVirtual(sDS_StockKey, 1, out var made, SDS_WithdrawMode.SpawnedTarget, pawn, requireFullQuantity: true) || made == null || made.Count == 0)
		{
			return false;
		}
		if (TrackPlaced(pawn, map, made[0], access, lifetimeTicks, preservedOrigin: false, out offered))
		{
			return true;
		}
		if (SDS_ReturnUtility.Return(made[0], map, access.cell, SDS_ReturnFallback.Ground) == SDS_ReclaimOutcome.Abandoned)
		{
			SDS_Diag.Warn("保真桥/虚拟落点失败（实物已落地，未回库）", SDS_Diag.Ctx(map, pawn, made[0], null, "def=" + ((Def)sDS_StockKey.thingDef).defName));
		}
		return false;
	}

	private static bool TryGetAccess(Pawn pawn, Map map, out SDS_AccessPoint access)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return SDS_AccessPointResolver.TryGetNearestReachableAccess(((Thing)pawn).Position, map, TraverseParms.For(pawn, (Danger)3, (TraverseMode)0, false, false, false, true), out access) && access != null && ((IntVec3)(ref access.cell)).IsValid;
	}

	private static bool TrackPlaced(Pawn pawn, Map map, Thing item, SDS_AccessPoint access, int lifetimeTicks, bool preservedOrigin, out Thing offered)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		offered = null;
		if (item == null || item.Destroyed)
		{
			return false;
		}
		if (!SDS_PlacementUtility.TryPlace(item, access.cell, map, SDS_PlacementPolicy.Exact, out var placed) || placed == null || placed.Destroyed)
		{
			return false;
		}
		if (placed != item)
		{
			SetCooldown(pawn, placed.def);
			offered = placed;
			return true;
		}
		ForbidUtility.SetForbidden(placed, false, true);
		SDS_ProducedThingMarker.Mark(placed);
		SetCooldown(pawn, placed.def);
		offered = placed;
		if (offers.Count >= 512)
		{
			return true;
		}
		OfferEntry offerEntry = new OfferEntry
		{
			expireTick = Now + Math.Max(60, (lifetimeTicks <= 0) ? 6000 : lifetimeTicks),
			rotFrozen = false,
			preservedOrigin = preservedOrigin
		};
		CompRottable val = ThingCompUtility.TryGetComp<CompRottable>(placed);
		if (val != null && !val.disabled)
		{
			val.disabled = true;
			offerEntry.rotFrozen = true;
		}
		offers[placed] = offerEntry;
		return true;
	}

	public static void Tick()
	{
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		if (offers.Count == 0 && cooldowns.Count == 0)
		{
			return;
		}
		int now = Now;
		List<Thing> list = null;
		foreach (KeyValuePair<Thing, OfferEntry> offer in offers)
		{
			Thing key = offer.Key;
			OfferEntry value = offer.Value;
			if (key == null || key.Destroyed || key.stackCount <= 0)
			{
				(list ?? (list = new List<Thing>())).Add(key);
			}
			else if (key.holdingOwner != null)
			{
				RestoreRot(key, value);
				(list ?? (list = new List<Thing>())).Add(key);
			}
			else if (!key.Spawned)
			{
				RestoreRot(key, value);
				RollbackOrWarn(key, value, null, IntVec3.Invalid, SDS_ReturnFallback.None, "保真桥/失图回库失败（实物已落地）");
				(list ?? (list = new List<Thing>())).Add(key);
			}
			else if (now >= value.expireTick)
			{
				RestoreRot(key, value);
				RollbackOrWarn(key, value, key.Map, key.Position, SDS_ReturnFallback.Ground, "保真桥/超时回库失败（实物已落地）");
				(list ?? (list = new List<Thing>())).Add(key);
			}
		}
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				offers.Remove(list[i]);
			}
		}
		PruneCooldowns(now);
	}

	private static void RollbackOrWarn(Thing thing, OfferEntry entry, Map map, IntVec3 cell, SDS_ReturnFallback fallback, string warnReason)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (thing != null && entry != null)
		{
			SDS_ReclaimOutcome sDS_ReclaimOutcome = (entry.preservedOrigin ? SDS_ReturnUtility.ReturnPreserved(thing, map, cell) : SDS_ReturnUtility.Return(thing, map, cell, fallback));
			if (sDS_ReclaimOutcome == SDS_ReclaimOutcome.Abandoned)
			{
				SDS_Diag.Warn(warnReason, SDS_Diag.Ctx(map, null, thing, null, "def=" + ((Def)thing.def).defName));
			}
		}
	}

	private static void RestoreRot(Thing thing, OfferEntry entry)
	{
		if (entry != null && entry.rotFrozen && thing != null && !thing.Destroyed)
		{
			CompRottable val = ThingCompUtility.TryGetComp<CompRottable>(thing);
			if (val != null)
			{
				val.disabled = false;
			}
		}
	}

	private static (int pawnId, ThingDef def) CooldownKey(Pawn pawn, ThingDef def)
	{
		return (pawnId: ((Thing)(pawn?)).thingIDNumber ?? 0, def: def);
	}

	private static bool InCooldown(Pawn pawn, ThingDef def)
	{
		if (cooldowns.TryGetValue(CooldownKey(pawn, def), out var value) && value > Now)
		{
			return true;
		}
		return false;
	}

	private static void SetCooldown(Pawn pawn, ThingDef def)
	{
		cooldowns[CooldownKey(pawn, def)] = Now + 600;
	}

	private static void PruneCooldowns(int now)
	{
		if (cooldowns.Count == 0)
		{
			return;
		}
		List<(int, ThingDef)> list = null;
		foreach (KeyValuePair<(int, ThingDef), int> cooldown in cooldowns)
		{
			if (cooldown.Value <= now)
			{
				(list ?? (list = new List<(int, ThingDef)>())).Add(cooldown.Key);
			}
		}
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				cooldowns.Remove(list[i]);
			}
		}
	}
}
[HarmonyPatch(typeof(HealthAIUtility), "FindBestMedicine")]
[HarmonyPriority(200)]
internal static class Patch_SDS_PreservedMedicineOffer
{
	public static void Postfix(Pawn healer, Pawn patient, bool onlyUseInventory, ref Thing __result)
	{
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Invalid comparison between Unknown and I4
		try
		{
			if (!(__result != null || onlyUseInventory) && healer != null && patient != null && ((Thing)healer).Spawned && ((Thing)healer).Map != null && ((Thing)healer).Map == ((Thing)patient).MapHeld && ((Thing)healer).Faction == Faction.OfPlayer && (patient.playerSettings == null || (int)patient.playerSettings.medCare > 1) && Medicine.GetMedicineCountToFullyHeal(patient) > 0 && SDS_PreservedOffer.TryOffer(healer, ((Thing)healer).Map, (Thing t) => t.def != null && t.def.IsMedicine && ((patient.playerSettings != null) ? MedicalCareUtility.AllowsMedicine(patient.playerSettings.medCare, t.def) : MedicalCareUtility.AllowsMedicine((MedicalCareCategory)1, t.def)), (Thing t) => StatExtension.GetStatValue(t, StatDefOf.MedicalPotency, true, -1), 6000, out var offered))
			{
				__result = offered;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真桥/药品提供失败（已忽略，原版按无药处理）", exception);
		}
	}
}
[HarmonyPatch(typeof(FoodUtility), "TryFindBestFoodSourceFor")]
[HarmonyPriority(200)]
internal static class Patch_SDS_PreservedFoodOffer
{
	public static void Postfix(Pawn getter, Pawn eater, bool desperate, bool allowVenerated, ref bool __result, ref Thing foodSource, ref ThingDef foodDef)
	{
		try
		{
			if (!__result && getter != null && eater != null && ((Thing)getter).Spawned && ((Thing)getter).Map != null && ((Thing)eater).Map == ((Thing)getter).Map && eater.needs != null && eater.needs.food != null && !eater.Dead && (((Thing)getter).Faction == Faction.OfPlayer || getter.IsColonist) && SDS_PreservedOffer.TryOffer(getter, ((Thing)getter).Map, (Thing t) => t.def != null && t.def.IsNutritionGivingIngestible && !t.def.IsCorpse && FoodUtility.WillEat(eater, t, getter, true, allowVenerated), (Thing t) => StatExtension.GetStatValue(t, StatDefOf.Nutrition, true, -1), 2500, out var offered))
			{
				foodSource = offered;
				foodDef = FoodUtility.GetFinalIngestibleDef(offered, false);
				__result = true;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真桥/食物提供失败（已忽略，原版按无食物处理）", exception);
		}
	}
}
[HarmonyPatch(typeof(JobGiver_PickUpOpportunisticWeapon), "TryGiveJob")]
[HarmonyPriority(200)]
internal static class Patch_SDS_PreservedWeaponOffer
{
	public static void Postfix(Pawn pawn, ref Job __result)
	{
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__result == null && pawn != null && ((Thing)pawn).Spawned && ((Thing)pawn).Map != null && ((Thing)pawn).Faction == Faction.OfPlayer && !pawn.Downed && pawn.equipment != null && pawn.equipment.Primary == null && !pawn.WorkTagIsDisabled((WorkTags)8) && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && RegionAndRoomQuery.GetRegion((Thing)(object)pawn, (RegionType)14) != null && (SDS_PreservedOffer.TryOffer(pawn, ((Thing)pawn).Map, (Thing t) => t.def != null && t.def.HasAssignableCompFrom(typeof(CompEquippable)) && EquipmentUtility.CanEquip(t, pawn) && (!t.def.IsRangedWeapon || !pawn.WorkTagIsDisabled((WorkTags)524288)), (Thing t) => t.MarketValue, 6000, out var offered) || SDS_PreservedOffer.TryOfferVirtual(pawn, ((Thing)pawn).Map, (ThingDef def) => def.HasAssignableCompFrom(typeof(CompEquippable)) && (!def.IsRangedWeapon || !pawn.WorkTagIsDisabled((WorkTags)524288)), (ThingDef def) => StatExtension.GetStatValueAbstract((BuildableDef)(object)def, StatDefOf.MarketValue, (ThingDef)null), 6000, out offered)))
			{
				Job val = JobMaker.MakeJob(JobDefOf.Equip, LocalTargetInfo.op_Implicit(offered));
				val.expiryInterval = 60;
				__result = val;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真桥/武器提供失败（已忽略，原版按无武器处理）", exception);
		}
	}
}
[HarmonyPatch(typeof(JobGiver_OptimizeApparel), "TryGiveJob")]
[HarmonyPriority(200)]
internal static class Patch_SDS_PreservedApparelOffer
{
	public static void Postfix(Pawn pawn, ref Job __result)
	{
		try
		{
			if (__result == null && pawn != null && ((Thing)pawn).Spawned && ((Thing)pawn).Map != null && ((Thing)pawn).Faction == Faction.OfPlayer && pawn.apparel != null && pawn.outfits != null && !pawn.Downed && !QuestUtility.IsQuestLodger(pawn) && (!pawn.IsMutant || !pawn.mutant.Def.disableApparel) && Find.TickManager.TicksGame >= pawn.mindState.nextApparelOptimizeTick)
			{
				ApparelPolicy policy = pawn.outfits.CurrentApparelPolicy;
				if (policy != null && policy.filter != null && SDS_PreservedOffer.TryOffer(pawn, ((Thing)pawn).Map, delegate(Thing t)
				{
					//IL_0051: Unknown result type (might be due to invalid IL or missing references)
					//IL_0085: Unknown result type (might be due to invalid IL or missing references)
					//IL_0095: Unknown result type (might be due to invalid IL or missing references)
					//IL_0063: Unknown result type (might be due to invalid IL or missing references)
					//IL_0073: Unknown result type (might be due to invalid IL or missing references)
					Apparel val = (Apparel)(object)((t is Apparel) ? t : null);
					return val != null && ((Thing)val).def != null && policy.filter.Allows((Thing)(object)val) && ApparelUtility.HasPartsToWear(pawn, ((Thing)val).def) && ((int)((Thing)val).def.apparel.gender == 0 || ((Thing)val).def.apparel.gender == pawn.gender) && DevelopmentalStageExtensions.Has(((Thing)val).def.apparel.developmentalStageFilter, pawn.DevelopmentalStage) && !pawn.apparel.WouldReplaceLockedApparel(val) && !CompBiocodable.IsBiocoded((Thing)(object)val);
				}, null, 6000, out var _))
				{
					pawn.mindState.nextApparelOptimizeTick = Find.TickManager.TicksGame;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真桥/衣物提供失败（已忽略，原版按无衣物处理）", exception);
		}
	}
}
[HarmonyPatch(typeof(StoreUtility), "IsInAnyStorage")]
[HarmonyPriority(200)]
internal static class Patch_SDS_PreservedOfferInStorage
{
	public static void Postfix(Thing t, ref bool __result)
	{
		if (!__result && SDS_PreservedOffer.IsOffer(t))
		{
			__result = true;
		}
	}
}
internal static class SDS_PreservedUsage
{
	private static Building_GeneAssembler activeGeneDialog;

	private static bool activeGeneDialogInjected;

	public static bool HasReachableEndpoint(Pawn pawn)
	{
		if (pawn == null || !((Thing)pawn).Spawned || ((Thing)pawn).Map == null)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			return false;
		}
		SDS_EndpointHandle best;
		SDS_EndpointDecision bestDecision;
		return SDS_EndpointResolver.TryFindBest(SDS_EndpointQuery.WithdrawPoint(pawn), out best, out bestDecision);
	}

	public static bool TryWithdrawExact(Thing thing)
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || thing == null || thing.Destroyed || !instance.IsPreserved(thing))
		{
			return false;
		}
		SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(thing);
		if (sDS_StockKey == null || instance.IsForbidden(sDS_StockKey) || instance.GetPreservedAvailableCount(sDS_StockKey) <= 0)
		{
			return false;
		}
		List<ThingCount> plan = new List<ThingCount>(1)
		{
			new ThingCount(thing, thing.stackCount, false)
		};
		if (!SDS_WithdrawExecutor.TryWithdrawPreservedExact(plan, out var made) || made == null || made.Count != 1)
		{
			return false;
		}
		return true;
	}

	public static bool TryWithdrawOne(SDS_StockKey key, Predicate<Thing> validator, out Thing thing)
	{
		thing = null;
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || key == null || !key.preserved || instance.IsForbidden(key) || instance.GetPreservedAvailableCount(key) <= 0)
		{
			return false;
		}
		if (!SDS_WithdrawExecutor.TryWithdrawPreserved(key, 1, validator, out var made) || made == null || made.Count == 0)
		{
			return false;
		}
		thing = made[0];
		for (int i = 1; i < made.Count; i++)
		{
			instance.TryAddPreserved(made[i]);
		}
		return true;
	}

	public static bool TryPlaceAtAccessPoint(Thing thing, Map map, IntVec3 reference, out Thing placed)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		placed = null;
		if (thing == null || thing.Destroyed || map == null)
		{
			return false;
		}
		IntVec3 val = NearestAccessCell(map, reference);
		if (!((IntVec3)(ref val)).IsValid)
		{
			val = ((((IntVec3)(ref reference)).IsValid && GenGrid.InBounds(reference, map)) ? reference : map.Center);
		}
		if (GenPlace.TryPlaceThing(thing, val, map, (ThingPlaceMode)1, ref placed, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1) && placed != null && !placed.Destroyed)
		{
			SDS_ProducedThingMarker.Mark(placed);
			return true;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.TryAddPreserved(thing))
		{
			GenSpawn.Spawn(thing, map.Center, map, (WipeMode)0);
			placed = thing;
			SDS_ProducedThingMarker.Mark(thing);
		}
		return placed != null;
	}

	public static bool TryMaterialize(Thing thing, Map map, IntVec3 reference, out Thing placed)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		placed = null;
		if (!TryWithdrawExact(thing))
		{
			return false;
		}
		return TryPlaceAtAccessPoint(thing, map, reference, out placed);
	}

	public static bool TryMaterialize(SDS_StockKey key, Map map, IntVec3 reference, Predicate<Thing> validator, out Thing placed)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		placed = null;
		if (!TryWithdrawOne(key, validator, out var thing))
		{
			return false;
		}
		return TryPlaceAtAccessPoint(thing, map, reference, out placed);
	}

	public static IntVec3 NearestAccessCell(Map map, IntVec3 reference)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		if (map == null)
		{
			return IntVec3.Invalid;
		}
		IntVec3 val = ((((IntVec3)(ref reference)).IsValid && GenGrid.InBounds(reference, map)) ? reference : map.Center);
		SDS_AccessPoint point;
		return (SDS_AccessPointResolver.TryGetNearestAccess(val, map, out point) && point != null) ? point.cell : IntVec3.Invalid;
	}

	public static void BeginGeneDialog(Building_GeneAssembler assembler)
	{
		activeGeneDialog = assembler;
		activeGeneDialogInjected = false;
	}

	public static bool TryClaimGeneDialogInjection(Building_GeneAssembler assembler)
	{
		if (assembler == null || assembler != activeGeneDialog || activeGeneDialogInjected)
		{
			return false;
		}
		activeGeneDialogInjected = true;
		return true;
	}
}
[HarmonyPatch(typeof(Recipe_ImplantXenogerm), "AvailableOnNow")]
internal static class Patch_SDS_XenogermImplantAvailable
{
	public static void Postfix(Thing thing, ref bool __result)
	{
		try
		{
			if (__result || !ModsConfig.BiotechActive || ThingDefOf.Xenogerm == null)
			{
				return;
			}
			Pawn val = (Pawn)(object)((thing is Pawn) ? thing : null);
			if (val != null)
			{
				SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
				if (instance != null && instance.Available && instance.AnyPreservedOfDef(ThingDefOf.Xenogerm) && SDS_PreservedUsage.HasReachableEndpoint(val))
				{
					__result = true;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("异种胚芽可见性补丁失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(/*Could not decode attribute arguments.*/)]
internal static class Patch_SDS_SelectXenogermList
{
	public static void Postfix(Pawn pawn, Map map, Dialog_SelectXenogerm __instance)
	{
		try
		{
			if (__instance == null || map == null || !ModsConfig.BiotechActive || ThingDefOf.Xenogerm == null)
			{
				return;
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || !instance.Available || !instance.AnyPreservedOfDef(ThingDefOf.Xenogerm) || !SDS_PreservedUsage.HasReachableEndpoint(pawn))
			{
				return;
			}
			List<Xenogerm> value = Traverse.Create((object)__instance).Field("xenogerms").GetValue<List<Xenogerm>>();
			if (value == null)
			{
				return;
			}
			List<Thing> list = new List<Thing>();
			instance.AppendPreservedThings(list);
			for (int i = 0; i < list.Count; i++)
			{
				Thing obj = list[i];
				Xenogerm val = (Xenogerm)(object)((obj is Xenogerm) ? obj : null);
				if (val != null && !((Thing)val).Destroyed && !value.Contains(val))
				{
					value.Add(val);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真异种胚芽列表补丁失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(Xenogerm), "SetTargetPawn")]
internal static class Patch_SDS_XenogermMaterialize
{
	public static bool Prefix(Xenogerm __instance, Pawn newTarget)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__instance == null || newTarget == null)
			{
				return true;
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || !instance.IsPreserved((Thing)(object)__instance))
			{
				return true;
			}
			Map mapHeld = ((Thing)newTarget).MapHeld;
			if (mapHeld == null || !SDS_PreservedUsage.TryMaterialize((Thing)(object)__instance, mapHeld, ((Thing)newTarget).Position, out var _))
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("SDS.Preserved.NoAccess")), MessageTypeDefOf.RejectInput, false);
				return false;
			}
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真异种胚芽物化失败（已忽略，原版行为不受影响）", exception);
			return true;
		}
	}
}
[HarmonyPatch(/*Could not decode attribute arguments.*/)]
internal static class Patch_SDS_GeneDialogBegin
{
	public static void Prefix(Building_GeneAssembler geneAssembler)
	{
		SDS_PreservedUsage.BeginGeneDialog(geneAssembler);
	}
}
[HarmonyPatch(typeof(Building_GeneAssembler), "GetGenepacks")]
internal static class Patch_SDS_GeneAssemblerGenepacks
{
	public static void Postfix(Building_GeneAssembler __instance, bool includePowered, bool includeUnpowered, ref List<Genepack> __result)
	{
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!includePowered || !includeUnpowered || __result == null || __instance == null || !((Thing)__instance).Spawned || ThingDefOf.Genepack == null || !SDS_PreservedUsage.TryClaimGeneDialogInjection(__instance))
			{
				return;
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || !instance.Available || !instance.AnyPreservedOfDef(ThingDefOf.Genepack))
			{
				return;
			}
			List<Thing> connectedFacilities = __instance.ConnectedFacilities;
			if (connectedFacilities == null || connectedFacilities.Count == 0)
			{
				return;
			}
			List<CompGenepackContainer> list = new List<CompGenepackContainer>();
			for (int i = 0; i < connectedFacilities.Count; i++)
			{
				CompGenepackContainer val = ((connectedFacilities[i] != null) ? ThingCompUtility.TryGetComp<CompGenepackContainer>(connectedFacilities[i]) : null);
				if (val != null && val.CanLoadMore)
				{
					list.Add(val);
				}
			}
			if (list.Count == 0)
			{
				return;
			}
			List<Thing> list2 = new List<Thing>();
			instance.AppendPreservedThings(list2);
			int j = 0;
			for (int k = 0; k < list2.Count; k++)
			{
				Thing obj = list2[k];
				Genepack val2 = (Genepack)(object)((obj is Genepack) ? obj : null);
				if (val2 == null || ((Thing)val2).Destroyed || ((Thing)val2).def != ThingDefOf.Genepack || ((GeneSetHolderBase)val2).GeneSet == null || ((GeneSetHolderBase)val2).GeneSet.GenesListForReading.Count == 0)
				{
					continue;
				}
				for (; j < list.Count && !list[j].CanLoadMore; j++)
				{
				}
				if (j >= list.Count)
				{
					break;
				}
				if (SDS_PreservedUsage.TryWithdrawExact((Thing)(object)val2))
				{
					CompGenepackContainer val3 = list[j];
					if (val3.innerContainer.TryAdd((Thing)(object)val2, false))
					{
						__result.Add(val2);
					}
					else
					{
						SDS_ReturnUtility.ReturnPreserved((Thing)(object)val2, ((Thing)__instance).Map, ((Thing)__instance).Position);
					}
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真基因包装入基因库失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(Building_GrowthVat), "GetGizmos")]
internal static class Patch_SDS_GrowthVatEmbryoGizmo
{
	public unsafe static void Postfix(Building_GrowthVat __instance, ref IEnumerable<Gizmo> __result)
	{
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Expected O, but got Unknown
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__result == null || __instance == null || !((Thing)__instance).Spawned || ThingDefOf.HumanEmbryo == null || ((Building_Enterable)__instance).Working || __instance.selectedEmbryo != null || ((Building_Enterable)__instance).SelectedPawn != null || !Find.Storyteller.difficulty.ChildrenAllowed)
			{
				return;
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance != null && instance.Available && instance.AnyPreservedOfDef(ThingDefOf.HumanEmbryo))
			{
				Command_Action val = new Command_Action();
				TaggedString val2 = Translator.Translate("SDS.GrowthVat.ImplantEmbryo");
				((Command)val).defaultLabel = ((object)(*(TaggedString*)(&val2))/*cast due to constrained. prefix*/).ToString() + "...";
				val2 = TranslatorFormattedStringExtensions.Translate("SDS.GrowthVat.ImplantEmbryoDesc", NamedArgument.op_Implicit(GenDate.ToStringTicksToPeriod(540000, true, false, true, true, false)));
				((Command)val).defaultDesc = ((TaggedString)(ref val2)).Resolve();
				((Command)val).icon = (Texture)(object)Building_GrowthVat.InsertEmbryoIcon.Texture;
				val.action = delegate
				{
					OpenMenu(__instance);
				};
				if (!__instance.PowerOn)
				{
					val2 = Translator.Translate("NoPower");
					((Gizmo)val).Disable(TaggedString.op_Implicit(((TaggedString)(ref val2)).CapitalizeFirst()));
				}
				__result = WithExtra(__result, (Gizmo)(object)val);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("生长舱保真胚胎按钮失败（已忽略，原版行为不受影响）", exception);
		}
	}

	private static void OpenMenu(Building_GrowthVat vat)
	{
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Expected O, but got Unknown
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Expected O, but got Unknown
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || vat == null || !((Thing)vat).Spawned)
		{
			return;
		}
		List<Thing> list = new List<Thing>();
		instance.AppendPreservedThings(list);
		List<FloatMenuOption> list2 = new List<FloatMenuOption>();
		for (int i = 0; i < list.Count; i++)
		{
			Thing obj = list[i];
			HumanEmbryo val = (HumanEmbryo)(object)((obj is HumanEmbryo) ? obj : null);
			if (val != null && !((Thing)val).Destroyed)
			{
				HumanEmbryo captured = val;
				list2.Add(new FloatMenuOption(((Entity)captured).LabelCap, (Action)delegate
				{
					vat.SelectEmbryo(captured);
				}, (Thing)(object)captured, Color.white, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
		}
		if (list2.Count == 0)
		{
			Messages.Message(TaggedString.op_Implicit(Translator.Translate("SDS.GrowthVat.NoEmbryo")), MessageTypeDefOf.RejectInput, false);
		}
		else
		{
			Find.WindowStack.Add((Window)new FloatMenu(list2));
		}
	}

	private static IEnumerable<Gizmo> WithExtra(IEnumerable<Gizmo> source, Gizmo extra)
	{
		foreach (Gizmo item in source)
		{
			yield return item;
		}
		yield return extra;
	}
}
[HarmonyPatch(typeof(Building_GrowthVat), "SelectEmbryo")]
internal static class Patch_SDS_GrowthVatSelectEmbryo
{
	public static bool Prefix(Building_GrowthVat __instance, ref HumanEmbryo embryo)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__instance == null || embryo == null || ((Thing)__instance).Map == null)
			{
				return true;
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || !instance.IsPreserved((Thing)(object)embryo))
			{
				return true;
			}
			if (SDS_PreservedUsage.TryMaterialize((Thing)(object)embryo, ((Thing)__instance).Map, ((Thing)__instance).InteractionCell, out var placed))
			{
				HumanEmbryo val = (HumanEmbryo)(object)((placed is HumanEmbryo) ? placed : null);
				if (val != null)
				{
					embryo = val;
					return true;
				}
			}
			Messages.Message(TaggedString.op_Implicit(Translator.Translate("SDS.Preserved.NoAccess")), MessageTypeDefOf.RejectInput, false);
			return false;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真胚胎物化失败（已忽略，原版行为不受影响）", exception);
			return true;
		}
	}
}
[HarmonyPatch(typeof(Building_Casket), "TryAcceptThing")]
internal static class Patch_SDS_CasketAcceptPreservedCorpse
{
	public static bool Prefix(Thing thing, out bool __state)
	{
		__state = false;
		try
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || thing == null || thing.Destroyed || !instance.IsPreserved(thing))
			{
				return true;
			}
			__state = SDS_PreservedUsage.TryWithdrawExact(thing);
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真尸体入棺预处理失败（已忽略，原版行为不受影响）", exception);
			return true;
		}
	}

	public static Exception Finalizer(Thing thing, bool __state, bool __result, Exception __exception)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		if (!__state || __result)
		{
			return __exception;
		}
		try
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || thing == null || thing.Destroyed)
			{
				return __exception;
			}
			Map map = thing.MapHeld ?? Find.AnyPlayerHomeMap;
			SDS_ReturnUtility.ReturnPreserved(thing, map, IntVec3.Invalid);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真尸体入棺回滚失败（已忽略）", exception);
		}
		return __exception;
	}
}
[HarmonyPatch(typeof(GenConstruct), "PlaceBlueprintForInstall")]
internal static class Patch_SDS_PlaceBlueprintForInstall
{
	public static void Prefix(ref MinifiedThing itemToInstall, IntVec3 center, Map map)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (itemToInstall == null || map == null)
			{
				return;
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance != null && instance.IsPreserved((Thing)(object)itemToInstall) && SDS_PreservedUsage.TryMaterialize((Thing)(object)itemToInstall, map, center, out var placed))
			{
				MinifiedThing val = (MinifiedThing)(object)((placed is MinifiedThing) ? placed : null);
				if (val != null)
				{
					SDS_InstallBlueprintGuard.CancelExistingBlueprint(val);
					itemToInstall = val;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("保真缩装物建蓝图预处理失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
internal static class SDS_InstallBlueprintGuard
{
	private static readonly FieldInfo MiniToInstallField = AccessTools.Field(typeof(Blueprint_Install), "miniToInstall");

	public static void CancelExistingBlueprint(MinifiedThing mini)
	{
		if (mini != null)
		{
			Blueprint_Install val = InstallBlueprintUtility.ExistingBlueprintFor((Thing)(object)mini);
			if (val != null && !((Thing)val).Destroyed)
			{
				((Thing)val).Destroy((DestroyMode)6);
			}
		}
	}

	public static bool TryReplaceBlueprintWithRollback(MinifiedThing mini, IntVec3 cell, Map map)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		if (mini == null || map == null)
		{
			return false;
		}
		try
		{
			CancelExistingBlueprint(mini);
			return GenConstruct.PlaceBlueprintForInstall(mini, cell, map, Rot4.North, Faction.OfPlayer, true) != null;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("输出/安装失败（物资已回滚）", exception);
			DestroyBlueprintsReferencing(mini, map);
			SDS_ReturnUtility.ReturnPreserved((Thing)(object)mini, map, cell);
			return false;
		}
	}

	private static void DestroyBlueprintsReferencing(MinifiedThing mini, Map map)
	{
		if (mini == null || map == null || map.listerThings == null || MiniToInstallField == null)
		{
			return;
		}
		try
		{
			List<Thing> allThings = map.listerThings.AllThings;
			List<Blueprint_Install> list = null;
			for (int i = 0; i < allThings.Count; i++)
			{
				Thing obj = allThings[i];
				Blueprint_Install val = (Blueprint_Install)(object)((obj is Blueprint_Install) ? obj : null);
				if (val != null && MiniToInstallField.GetValue(val) == mini)
				{
					(list ?? (list = new List<Blueprint_Install>())).Add(val);
				}
			}
			if (list == null)
			{
				return;
			}
			for (int j = 0; j < list.Count; j++)
			{
				Blueprint_Install val2 = list[j];
				if (val2 != null && !((Thing)val2).Destroyed)
				{
					((Thing)val2).Destroy((DestroyMode)0);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("输出/安装半生成蓝图清理失败（已跳过）", exception);
		}
	}
}
public interface ISDS_ProxyThing
{
	SDS_StockKey Key { get; }

	bool Preserved { get; }

	Thing ResolveReal(int count);
}
internal static class SDS_ProxyUtility
{
	public static bool IsActive(Thing thing)
	{
		return thing is ISDS_ProxyThing && !thing.Destroyed && SDS_MirrorRegistry.IsMirror(thing);
	}

	public static Thing Resolve(Thing proxy, int count)
	{
		if (proxy == null || count <= 0 || !SDS_MirrorRegistry.IsMirror(proxy))
		{
			return null;
		}
		Thing taken;
		return SDS_TakeService.TryTake(proxy, count, out taken) ? taken : null;
	}
}
internal static class SDS_ProxyFactory
{
	public static Type ProxyTypeFor(ThingDef def)
	{
		if (def == null || def.thingClass == null)
		{
			return null;
		}
		Type thingClass = def.thingClass;
		if (thingClass == typeof(Thing))
		{
			return typeof(SDS_Proxy_Thing);
		}
		if (thingClass == typeof(ThingWithComps))
		{
			return typeof(SDS_Proxy_ThingWithComps);
		}
		if (thingClass == typeof(Apparel))
		{
			return typeof(SDS_Proxy_Apparel);
		}
		return null;
	}

	public static Thing CreateMirrorThing(SDS_StockKey key)
	{
		if (key == null || key.thingDef == null || key.preserved)
		{
			return null;
		}
		Thing val = null;
		Type type = ProxyTypeFor(key.thingDef);
		if (type != null)
		{
			try
			{
				val = Instantiate(type, key);
			}
			catch (Exception exception)
			{
				SDS_Diag.ErrorOnce("代理镜像创建失败（已回退为原版实物类）", exception);
				val = null;
			}
		}
		if (val == null)
		{
			val = ThingMaker.MakeThing(key.thingDef, key.stuff);
		}
		if (val != null)
		{
			SDS_OutputUtility.ApplyKeyState(key, val);
		}
		return val;
	}

	private static Thing Instantiate(Type proxyType, SDS_StockKey key)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		ThingDef thingDef = key.thingDef;
		ThingDef val = key.stuff;
		if (val != null && !val.IsStuff)
		{
			val = null;
		}
		if (((BuildableDef)thingDef).MadeFromStuff && val == null)
		{
			val = GenStuff.DefaultStuffFor((BuildableDef)(object)thingDef);
		}
		if (!((BuildableDef)thingDef).MadeFromStuff && val != null)
		{
			val = null;
		}
		Thing val2 = (Thing)Activator.CreateInstance(proxyType);
		val2.def = thingDef;
		val2.SetStuffDirect(val);
		val2.PostMake();
		val2.PostPostMake();
		return val2;
	}
}
public class SDS_Proxy_Thing : Thing, ISDS_ProxyThing
{
	public SDS_StockKey Key => SDS_StockKey.FromThing((Thing)(object)this);

	public bool Preserved => false;

	public Thing ResolveReal(int count)
	{
		return SDS_ProxyUtility.Resolve((Thing)(object)this, count);
	}

	public override Thing SplitOff(int count)
	{
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			return ((Thing)this).SplitOff(count);
		}
		return ResolveReal(count);
	}

	public override bool CanStackWith(Thing other)
	{
		if (SDS_ProxyUtility.IsActive((Thing)(object)this) || SDS_ProxyUtility.IsActive(other))
		{
			return false;
		}
		return ((Thing)this).CanStackWith(other);
	}

	public override bool TryAbsorbStack(Thing other, bool respectStackLimit)
	{
		if (SDS_MirrorAbsorb.TryHandle((Thing)(object)this, other, respectStackLimit, out var result))
		{
			return result;
		}
		return ((Thing)this).TryAbsorbStack(other, respectStackLimit);
	}

	public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
	{
		if (SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			absorbed = true;
		}
		else
		{
			((Thing)this).PreApplyDamage(ref dinfo, ref absorbed);
		}
	}

	public override void Kill(DamageInfo? dinfo = null, Hediff exactCulprit = null)
	{
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			((Thing)this).Kill(dinfo, exactCulprit);
		}
	}

	public override void Print(SectionLayer layer)
	{
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			((Thing)this).Print(layer);
		}
	}

	protected override void DrawAt(Vector3 drawLoc, bool flip = false)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			((Thing)this).DrawAt(drawLoc, flip);
		}
	}

	public override void DrawGUIOverlay()
	{
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			((Thing)this).DrawGUIOverlay();
		}
	}

	public override string GetInspectString()
	{
		return SDS_ProxyUtility.IsActive((Thing)(object)this) ? null : ((Thing)this).GetInspectString();
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		if (SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			yield break;
		}
		foreach (Gizmo item in <>n__0())
		{
			yield return item;
		}
	}

	public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
	{
		if (SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			yield break;
		}
		foreach (FloatMenuOption item in <>n__1(selPawn))
		{
			yield return item;
		}
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerable<Gizmo> <>n__0()
	{
		return ((Thing)this).GetGizmos();
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerable<FloatMenuOption> <>n__1(Pawn selPawn)
	{
		return ((Thing)this).GetFloatMenuOptions(selPawn);
	}
}
public class SDS_Proxy_ThingWithComps : ThingWithComps, ISDS_ProxyThing
{
	public SDS_StockKey Key => SDS_StockKey.FromThing((Thing)(object)this);

	public bool Preserved => false;

	public Thing ResolveReal(int count)
	{
		return SDS_ProxyUtility.Resolve((Thing)(object)this, count);
	}

	public override Thing SplitOff(int count)
	{
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			return ((ThingWithComps)this).SplitOff(count);
		}
		return ResolveReal(count);
	}

	public override bool CanStackWith(Thing other)
	{
		if (SDS_ProxyUtility.IsActive((Thing)(object)this) || SDS_ProxyUtility.IsActive(other))
		{
			return false;
		}
		return ((ThingWithComps)this).CanStackWith(other);
	}

	public override bool TryAbsorbStack(Thing other, bool respectStackLimit)
	{
		if (SDS_MirrorAbsorb.TryHandle((Thing)(object)this, other, respectStackLimit, out var result))
		{
			return result;
		}
		return ((ThingWithComps)this).TryAbsorbStack(other, respectStackLimit);
	}

	public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
	{
		if (SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			absorbed = true;
		}
		else
		{
			((ThingWithComps)this).PreApplyDamage(ref dinfo, ref absorbed);
		}
	}

	public override void Kill(DamageInfo? dinfo = null, Hediff exactCulprit = null)
	{
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			((ThingWithComps)this).Kill(dinfo, exactCulprit);
		}
	}

	public override void Print(SectionLayer layer)
	{
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			((ThingWithComps)this).Print(layer);
		}
	}

	protected override void DrawAt(Vector3 drawLoc, bool flip = false)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			((ThingWithComps)this).DrawAt(drawLoc, flip);
		}
	}

	public override void DrawGUIOverlay()
	{
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			((ThingWithComps)this).DrawGUIOverlay();
		}
	}

	public override string GetInspectString()
	{
		return SDS_ProxyUtility.IsActive((Thing)(object)this) ? null : ((ThingWithComps)this).GetInspectString();
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		if (SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			yield break;
		}
		foreach (Gizmo item in <>n__0())
		{
			yield return item;
		}
	}

	public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
	{
		if (SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			yield break;
		}
		foreach (FloatMenuOption item in <>n__1(selPawn))
		{
			yield return item;
		}
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerable<Gizmo> <>n__0()
	{
		return ((ThingWithComps)this).GetGizmos();
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerable<FloatMenuOption> <>n__1(Pawn selPawn)
	{
		return ((ThingWithComps)this).GetFloatMenuOptions(selPawn);
	}
}
public class SDS_Proxy_Apparel : Apparel, ISDS_ProxyThing
{
	public SDS_StockKey Key => SDS_StockKey.FromThing((Thing)(object)this);

	public bool Preserved => false;

	public Thing ResolveReal(int count)
	{
		return SDS_ProxyUtility.Resolve((Thing)(object)this, count);
	}

	public override Thing SplitOff(int count)
	{
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			return ((ThingWithComps)this).SplitOff(count);
		}
		return ResolveReal(count);
	}

	public override bool CanStackWith(Thing other)
	{
		if (SDS_ProxyUtility.IsActive((Thing)(object)this) || SDS_ProxyUtility.IsActive(other))
		{
			return false;
		}
		return ((ThingWithComps)this).CanStackWith(other);
	}

	public override bool TryAbsorbStack(Thing other, bool respectStackLimit)
	{
		if (SDS_MirrorAbsorb.TryHandle((Thing)(object)this, other, respectStackLimit, out var result))
		{
			return result;
		}
		return ((ThingWithComps)this).TryAbsorbStack(other, respectStackLimit);
	}

	public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
	{
		if (SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			absorbed = true;
		}
		else
		{
			((ThingWithComps)this).PreApplyDamage(ref dinfo, ref absorbed);
		}
	}

	public override void Kill(DamageInfo? dinfo = null, Hediff exactCulprit = null)
	{
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			((ThingWithComps)this).Kill(dinfo, exactCulprit);
		}
	}

	public override void Print(SectionLayer layer)
	{
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			((ThingWithComps)this).Print(layer);
		}
	}

	protected override void DrawAt(Vector3 drawLoc, bool flip = false)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			((ThingWithComps)this).DrawAt(drawLoc, flip);
		}
	}

	public override void DrawGUIOverlay()
	{
		if (!SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			((ThingWithComps)this).DrawGUIOverlay();
		}
	}

	public override string GetInspectString()
	{
		return SDS_ProxyUtility.IsActive((Thing)(object)this) ? null : ((Apparel)this).GetInspectString();
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		if (SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			yield break;
		}
		foreach (Gizmo item in <>n__0())
		{
			yield return item;
		}
	}

	public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
	{
		if (SDS_ProxyUtility.IsActive((Thing)(object)this))
		{
			yield break;
		}
		foreach (FloatMenuOption item in <>n__1(selPawn))
		{
			yield return item;
		}
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerable<Gizmo> <>n__0()
	{
		return ((ThingWithComps)this).GetGizmos();
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerable<FloatMenuOption> <>n__1(Pawn selPawn)
	{
		return ((ThingWithComps)this).GetFloatMenuOptions(selPawn);
	}
}
internal static class SDS_ReturnUtility
{
	public static SDS_ReclaimOutcome Return(Thing thing, Map map, IntVec3 cell, SDS_ReturnFallback fallback, Pawn pawn = null, bool clearForbidden = false)
	{
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		if (thing == null || thing.Destroyed)
		{
			return SDS_ReclaimOutcome.Consumed;
		}
		if (SDS_ReclaimRegistry.TryReturnToGrid(thing, clearForbidden))
		{
			return SDS_ReclaimOutcome.Returned;
		}
		switch (fallback)
		{
		case SDS_ReturnFallback.None:
			return SDS_ReclaimOutcome.Abandoned;
		case SDS_ReturnFallback.PawnInventory:
		case SDS_ReturnFallback.PawnCarry:
			if (pawn == null)
			{
				return SDS_ReclaimOutcome.Abandoned;
			}
			if (pawn.inventory != null && pawn.inventory.innerContainer != null && ((ThingOwner)pawn.inventory.innerContainer).TryAdd(thing, true))
			{
				return SDS_ReclaimOutcome.Abandoned;
			}
			if (fallback == SDS_ReturnFallback.PawnCarry && pawn.carryTracker != null && ((ThingOwner)pawn.carryTracker.innerContainer).TryAdd(thing, true))
			{
				return SDS_ReclaimOutcome.Abandoned;
			}
			if (pawn != null && ((Thing)pawn).Spawned)
			{
				PlaceTo(thing, ((Thing)pawn).Map, ((Thing)pawn).Position);
			}
			return SDS_ReclaimOutcome.Abandoned;
		default:
			PlaceTo(thing, map ?? thing.MapHeld, cell);
			return SDS_ReclaimOutcome.Abandoned;
		}
	}

	public static SDS_ReclaimOutcome ReturnPreserved(Thing thing, Map map, IntVec3 cell)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		if (thing == null || thing.Destroyed)
		{
			return SDS_ReclaimOutcome.Consumed;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance != null && instance.TryAddPreserved(thing))
		{
			return SDS_ReclaimOutcome.Returned;
		}
		PlaceTo(thing, map ?? thing.MapHeld, cell);
		return SDS_ReclaimOutcome.Abandoned;
	}

	public static bool ReturnVirtualCount(SDS_StockKey key, int count, float hitPoints = -1f, float rotProgress = -1f)
	{
		if (key == null || count <= 0)
		{
			return false;
		}
		return SDS_GameComponent_Grid.Instance?.TryAddCount(key, count, hitPoints, rotProgress) ?? false;
	}

	public static SDS_ReclaimOutcome ReturnVirtual(Thing thing, SDS_StockKey key, Map map, IntVec3 cell)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		if (thing == null || thing.Destroyed)
		{
			return SDS_ReclaimOutcome.Consumed;
		}
		int stackCount = thing.stackCount;
		if (ReturnVirtualCount(key, stackCount, SDS_StockVitals.CaptureHitPoints(thing), SDS_StockVitals.CaptureRotProgress(thing)))
		{
			thing.Destroy((DestroyMode)0);
			return SDS_ReclaimOutcome.Returned;
		}
		PlaceTo(thing, map, cell);
		return SDS_ReclaimOutcome.Abandoned;
	}

	private static void PlaceTo(Thing thing, Map map, IntVec3 cell)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (thing != null && !thing.Spawned && map != null)
		{
			SDS_PlacementUtility.TryPlaceOrSpawn(thing, cell, map);
		}
	}
}
internal static class SDS_ReclaimRegistry
{
	private sealed class Entry
	{
		public SDS_ReclaimPolicy policy;

		public int ownerLoadID;

		public int tick;

		public bool hand;

		public readonly List<Thing> things = new List<Thing>();

		public readonly List<int> counts = new List<int>();
	}

	private const int MaxAgeTicks = 60000;

	private const int MaxEntries = 4096;

	public const int ProtectedOutputRecycleTicks = 60000;

	private static readonly Dictionary<int, Entry> ledger = new Dictionary<int, Entry>();

	private static readonly List<Entry> outputLedger = new List<Entry>();

	private static readonly List<Entry> pendingOutputs = new List<Entry>();

	private static readonly Dictionary<int, Entry> handLedger = new Dictionary<int, Entry>();

	private static int Now => (Find.TickManager != null) ? Find.TickManager.TicksGame : 0;

	public static void Reset()
	{
		ledger.Clear();
		handLedger.Clear();
		outputLedger.Clear();
		pendingOutputs.Clear();
	}

	public static bool TryReturnToGrid(Thing thing, bool clearForbidden)
	{
		if (thing == null || thing.Destroyed)
		{
			return false;
		}
		if (clearForbidden)
		{
			CompForbiddable val = ThingCompUtility.TryGetComp<CompForbiddable>(thing);
			if (val != null && val.Forbidden)
			{
				val.Forbidden = false;
			}
		}
		int accepted;
		return SDS_GameComponent_Grid.Instance?.TryDeposit(thing, out accepted) ?? false;
	}

	public static void RegisterProtectedOutput(Thing thing, Lord owner)
	{
		if (thing != null && !thing.Destroyed && thing.Spawned && outputLedger.Count + pendingOutputs.Count < 4096)
		{
			Entry entry = new Entry
			{
				policy = SDS_ReclaimPolicy.ProtectedOutput,
				ownerLoadID = (owner?.loadID ?? 0),
				tick = Now
			};
			entry.things.Add(thing);
			entry.counts.Add(1);
			outputLedger.Add(entry);
		}
	}

	public static SDS_ReclaimOutcome Sweep(int now)
	{
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		SDS_ReclaimOutcome sDS_ReclaimOutcome = SDS_ReclaimOutcome.None;
		for (int num = outputLedger.Count - 1; num >= 0; num--)
		{
			Entry entry = outputLedger[num];
			Thing val = ((entry.things.Count > 0) ? entry.things[0] : null);
			if (val == null || val.Destroyed)
			{
				sDS_ReclaimOutcome = Worse(sDS_ReclaimOutcome, SDS_ReclaimOutcome.Consumed);
				outputLedger.RemoveAt(num);
			}
			else if (!val.Spawned)
			{
				sDS_ReclaimOutcome = Worse(sDS_ReclaimOutcome, SDS_ReclaimOutcome.Abandoned);
				outputLedger.RemoveAt(num);
			}
			else
			{
				Lord val2 = FindLord(entry.ownerLoadID);
				bool flag = val2 == null;
				bool flag2 = entry.tick > 0 && now - entry.tick >= 60000;
				if (flag || flag2)
				{
					SDS_CaravanVirtual.RemoveFromCaravan(val2, val);
					sDS_ReclaimOutcome = Worse(sDS_ReclaimOutcome, SDS_ReturnUtility.Return(val, val.MapHeld, val.PositionHeld, SDS_ReturnFallback.None, null, clearForbidden: true));
					outputLedger.RemoveAt(num);
				}
			}
		}
		return sDS_ReclaimOutcome;
	}

	private static Lord FindLord(int loadID)
	{
		if (loadID == 0 || Find.Maps == null)
		{
			return null;
		}
		for (int i = 0; i < Find.Maps.Count; i++)
		{
			List<Lord> list = (Find.Maps[i]?.lordManager)?.lords;
			if (list == null)
			{
				continue;
			}
			for (int j = 0; j < list.Count; j++)
			{
				Lord val = list[j];
				if (val != null && val.loadID == loadID)
				{
					return val;
				}
			}
		}
		return null;
	}

	public static void TrackJobTarget(Job job, Thing thing, int count)
	{
		if (job == null || thing == null || count <= 0)
		{
			return;
		}
		int now = Now;
		Prune(now);
		if (!ledger.TryGetValue(job.loadID, out var value) || value == null)
		{
			if (ledger.Count >= 4096)
			{
				return;
			}
			value = new Entry
			{
				policy = SDS_ReclaimPolicy.JobTarget,
				ownerLoadID = job.loadID,
				tick = now
			};
			ledger[job.loadID] = value;
		}
		value.things.Add(thing);
		value.counts.Add(count);
	}

	public static void ForgetJobTarget(int loadID)
	{
		ledger.Remove(loadID);
	}

	private static SDS_ReclaimOutcome ReclaimOne(Thing thing, int want)
	{
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		if (thing == null || thing.Destroyed)
		{
			return SDS_ReclaimOutcome.Consumed;
		}
		if (!thing.Spawned || thing.holdingOwner != null || StoreUtility.IsInAnyStorage(thing))
		{
			return SDS_ReclaimOutcome.Abandoned;
		}
		want = Math.Min(want, thing.stackCount);
		if (want <= 0)
		{
			return SDS_ReclaimOutcome.None;
		}
		Map mapHeld = thing.MapHeld;
		IntVec3 positionHeld = thing.PositionHeld;
		Thing val = ((want >= thing.stackCount) ? thing : thing.SplitOff(want));
		if (val == null)
		{
			return SDS_ReclaimOutcome.None;
		}
		return SDS_ReturnUtility.Return(val, mapHeld, positionHeld, SDS_ReturnFallback.Ground);
	}

	private static SDS_ReclaimOutcome Worse(SDS_ReclaimOutcome a, SDS_ReclaimOutcome b)
	{
		return (a >= b) ? a : b;
	}

	public static SDS_ReclaimOutcome ReclaimJobTarget(int loadID)
	{
		if (!ledger.TryGetValue(loadID, out var value) || value == null)
		{
			return SDS_ReclaimOutcome.None;
		}
		ledger.Remove(loadID);
		SDS_ReclaimOutcome sDS_ReclaimOutcome = SDS_ReclaimOutcome.None;
		for (int i = 0; i < value.things.Count; i++)
		{
			sDS_ReclaimOutcome = Worse(sDS_ReclaimOutcome, ReclaimOne(value.things[i], value.counts[i]));
		}
		if (sDS_ReclaimOutcome == SDS_ReclaimOutcome.Consumed)
		{
			SDS_Diag.Warn("回收时实物已被消耗（不回加账本）", "jobLoadID=" + loadID);
		}
		return sDS_ReclaimOutcome;
	}

	public static void TrackHand(Job job, Thing thing)
	{
		if (job == null || thing == null)
		{
			return;
		}
		if (!handLedger.TryGetValue(job.loadID, out var value) || value == null)
		{
			if (handLedger.Count >= 4096)
			{
				handLedger.Clear();
			}
			value = new Entry
			{
				policy = SDS_ReclaimPolicy.JobTarget,
				ownerLoadID = job.loadID,
				tick = Now,
				hand = true
			};
			handLedger[job.loadID] = value;
		}
		value.things.Add(thing);
		value.counts.Add(1);
	}

	public static SDS_ReclaimOutcome ReclaimHand(Pawn pawn, int loadID)
	{
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		if (!handLedger.TryGetValue(loadID, out var value) || value == null)
		{
			return SDS_ReclaimOutcome.None;
		}
		handLedger.Remove(loadID);
		if (pawn == null || pawn.carryTracker == null || ((Thing)pawn).Map == null)
		{
			return SDS_ReclaimOutcome.Abandoned;
		}
		SDS_ReclaimOutcome sDS_ReclaimOutcome = SDS_ReclaimOutcome.None;
		for (int i = 0; i < value.things.Count; i++)
		{
			Thing val = value.things[i];
			sDS_ReclaimOutcome = ((val != null && !val.Destroyed) ? (((object)val.holdingOwner == pawn.carryTracker.innerContainer) ? (((ThingOwner)pawn.carryTracker.innerContainer).Remove(val) ? Worse(sDS_ReclaimOutcome, SDS_ReturnUtility.Return(val, ((Thing)pawn).Map, ((Thing)pawn).Position, SDS_ReturnFallback.PawnInventory, pawn)) : Worse(sDS_ReclaimOutcome, SDS_ReclaimOutcome.Abandoned)) : Worse(sDS_ReclaimOutcome, SDS_ReclaimOutcome.Abandoned)) : Worse(sDS_ReclaimOutcome, SDS_ReclaimOutcome.Consumed));
		}
		return sDS_ReclaimOutcome;
	}

	internal static void ExportTo(List<SDS_ReclaimSave> into)
	{
		if (into == null)
		{
			return;
		}
		into.Clear();
		foreach (KeyValuePair<int, Entry> item in ledger)
		{
			AddSave(into, item.Key, item.Value);
		}
		foreach (KeyValuePair<int, Entry> item2 in handLedger)
		{
			AddSave(into, item2.Key, item2.Value);
		}
		for (int i = 0; i < outputLedger.Count; i++)
		{
			AddSave(into, 0, outputLedger[i]);
		}
	}

	private static void AddSave(List<SDS_ReclaimSave> into, int jobLoadID, Entry entry)
	{
		if (into == null || entry == null)
		{
			return;
		}
		SDS_ReclaimSave sDS_ReclaimSave = new SDS_ReclaimSave
		{
			policy = entry.policy,
			jobLoadID = jobLoadID,
			ownerLoadID = entry.ownerLoadID,
			tick = entry.tick,
			hand = entry.hand
		};
		for (int i = 0; i < entry.things.Count; i++)
		{
			Thing val = entry.things[i];
			if (val != null && !val.Destroyed)
			{
				sDS_ReclaimSave.things.Add(val);
				sDS_ReclaimSave.counts.Add((i >= entry.counts.Count) ? 1 : entry.counts[i]);
			}
		}
		if (sDS_ReclaimSave.things.Count > 0)
		{
			into.Add(sDS_ReclaimSave);
		}
	}

	internal static void ImportFrom(List<SDS_ReclaimSave> from)
	{
		ledger.Clear();
		handLedger.Clear();
		outputLedger.Clear();
		if (from == null)
		{
			return;
		}
		for (int i = 0; i < from.Count; i++)
		{
			SDS_ReclaimSave sDS_ReclaimSave = from[i];
			if (sDS_ReclaimSave == null || sDS_ReclaimSave.things == null || sDS_ReclaimSave.things.Count == 0)
			{
				continue;
			}
			if (sDS_ReclaimSave.policy == SDS_ReclaimPolicy.ProtectedOutput)
			{
				Thing val = FirstAlive(sDS_ReclaimSave.things);
				if (val != null)
				{
					Entry entry = new Entry
					{
						policy = SDS_ReclaimPolicy.ProtectedOutput,
						ownerLoadID = ((sDS_ReclaimSave.ownerLoadID != 0) ? sDS_ReclaimSave.ownerLoadID : sDS_ReclaimSave.jobLoadID),
						tick = sDS_ReclaimSave.tick
					};
					entry.things.Add(val);
					entry.counts.Add(1);
					outputLedger.Add(entry);
				}
			}
			else
			{
				if (sDS_ReclaimSave.jobLoadID == 0)
				{
					continue;
				}
				Entry entry2 = new Entry
				{
					policy = SDS_ReclaimPolicy.JobTarget,
					ownerLoadID = sDS_ReclaimSave.jobLoadID,
					tick = sDS_ReclaimSave.tick,
					hand = sDS_ReclaimSave.hand
				};
				for (int j = 0; j < sDS_ReclaimSave.things.Count; j++)
				{
					Thing val2 = sDS_ReclaimSave.things[j];
					if (val2 != null && !val2.Destroyed)
					{
						entry2.things.Add(val2);
						entry2.counts.Add((j >= sDS_ReclaimSave.counts.Count) ? 1 : sDS_ReclaimSave.counts[j]);
					}
				}
				if (entry2.things.Count != 0)
				{
					if (sDS_ReclaimSave.hand)
					{
						handLedger[sDS_ReclaimSave.jobLoadID] = entry2;
					}
					else
					{
						ledger[sDS_ReclaimSave.jobLoadID] = entry2;
					}
				}
			}
		}
	}

	private static Thing FirstAlive(List<Thing> things)
	{
		for (int i = 0; i < things.Count; i++)
		{
			Thing val = things[i];
			if (val != null && !val.Destroyed)
			{
				return val;
			}
		}
		return null;
	}

	internal static void AdoptLegacyProtectedOutputs(List<SDS_ProtectedOutput> legacy)
	{
		if (legacy == null)
		{
			return;
		}
		for (int i = 0; i < legacy.Count; i++)
		{
			SDS_ProtectedOutput sDS_ProtectedOutput = legacy[i];
			Thing val = sDS_ProtectedOutput?.thing;
			if (val != null && !val.Destroyed)
			{
				if (outputLedger.Count + pendingOutputs.Count >= 4096)
				{
					break;
				}
				Entry entry = new Entry
				{
					policy = SDS_ReclaimPolicy.ProtectedOutput,
					ownerLoadID = sDS_ProtectedOutput.lordLoadID,
					tick = sDS_ProtectedOutput.createdTick
				};
				entry.things.Add(val);
				entry.counts.Add(1);
				pendingOutputs.Add(entry);
			}
		}
	}

	internal static void AuditRestored()
	{
		if (pendingOutputs.Count > 0)
		{
			outputLedger.AddRange(pendingOutputs);
			pendingOutputs.Clear();
		}
		if (ledger.Count > 0)
		{
			List<int> list = null;
			foreach (KeyValuePair<int, Entry> item in ledger)
			{
				if (!JobExists(item.Key))
				{
					if (list == null)
					{
						list = new List<int>();
					}
					list.Add(item.Key);
				}
			}
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					ReclaimJobTarget(list[i]);
				}
			}
		}
		if (handLedger.Count <= 0)
		{
			return;
		}
		List<int> list2 = null;
		foreach (KeyValuePair<int, Entry> item2 in handLedger)
		{
			if (!JobExists(item2.Key))
			{
				if (list2 == null)
				{
					list2 = new List<int>();
				}
				list2.Add(item2.Key);
			}
		}
		if (list2 != null)
		{
			for (int j = 0; j < list2.Count; j++)
			{
				handLedger.Remove(list2[j]);
			}
		}
	}

	private static bool JobExists(int loadID)
	{
		if (loadID == 0 || Find.Maps == null)
		{
			return false;
		}
		for (int i = 0; i < Find.Maps.Count; i++)
		{
			IReadOnlyList<Pawn> allPawnsSpawned = Find.Maps[i].mapPawns.AllPawnsSpawned;
			if (allPawnsSpawned == null)
			{
				continue;
			}
			for (int j = 0; j < allPawnsSpawned.Count; j++)
			{
				Pawn_JobTracker val = allPawnsSpawned[j]?.jobs;
				if (val == null)
				{
					continue;
				}
				if (val.curJob != null && val.curJob.loadID == loadID)
				{
					return true;
				}
				foreach (QueuedJob item in val.jobQueue)
				{
					if (item != null && item.job != null && item.job.loadID == loadID)
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private static void Prune(int now)
	{
		if (ledger.Count == 0)
		{
			return;
		}
		List<int> list = null;
		foreach (KeyValuePair<int, Entry> item in ledger)
		{
			if (item.Value == null || now - item.Value.tick > 60000)
			{
				if (list == null)
				{
					list = new List<int>();
				}
				list.Add(item.Key);
			}
		}
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				ledger.Remove(list[i]);
			}
		}
	}
}
internal static class SDS_SearchRanker
{
	public static bool PreferSource(float candidateDist, int candidateId, float currentDist, int currentId, bool hasCurrent)
	{
		if (!hasCurrent)
		{
			return true;
		}
		if (candidateDist < currentDist)
		{
			return true;
		}
		if (candidateDist > currentDist)
		{
			return false;
		}
		return candidateId < currentId;
	}

	public static bool PreferPriority(float candidatePrio, float candidateDist, int candidateId, float currentPrio, float currentDist, int currentId, bool hasCurrent)
	{
		if (!hasCurrent)
		{
			return true;
		}
		if (candidatePrio > currentPrio)
		{
			return true;
		}
		if (candidatePrio < currentPrio)
		{
			return false;
		}
		return PreferSource(candidateDist, candidateId, currentDist, currentId, hasCurrent: true);
	}

	public static bool PreferMirrorCandidate(float candidatePrio, float candidateDist, int candidateId, bool candidateIsMirror, bool hasPriority, float currentPrio, float currentDist, int currentId, bool currentIsMirror, bool hasCurrent)
	{
		if (!hasCurrent)
		{
			return true;
		}
		if (hasPriority && candidatePrio != currentPrio)
		{
			return candidatePrio > currentPrio;
		}
		if (candidateDist != currentDist)
		{
			return candidateDist < currentDist;
		}
		if (candidateIsMirror != currentIsMirror)
		{
			return candidateIsMirror;
		}
		return candidateId < currentId;
	}

	public static bool PreferMirrorOnTie(float mirrorDist, float realDist)
	{
		return mirrorDist <= realDist;
	}

	public static bool PreferEndpoint(int candidatePrio, float candidateDist, int candidateId, bool candidateIsSelf, int currentPrio, float currentDist, int currentId, bool currentIsSelf, bool hasCurrent, bool ignorePriority)
	{
		if (!hasCurrent)
		{
			return true;
		}
		if (!ignorePriority && candidatePrio != currentPrio)
		{
			return candidatePrio > currentPrio;
		}
		if (candidateDist < currentDist)
		{
			return true;
		}
		if (candidateDist > currentDist)
		{
			return false;
		}
		if (candidateIsSelf != currentIsSelf)
		{
			return candidateIsSelf;
		}
		return candidateId < currentId;
	}

	public static bool PreferDestination(int candidatePrio, float candidateDist, int candidateId, bool candidateIsEndpoint, int currentPrio, float currentDist, int currentId, bool currentIsEndpoint, bool hasCurrent)
	{
		if (!hasCurrent)
		{
			return true;
		}
		if (candidatePrio != currentPrio)
		{
			return candidatePrio > currentPrio;
		}
		if (candidateDist < currentDist)
		{
			return true;
		}
		if (candidateDist > currentDist)
		{
			return false;
		}
		if (candidateIsEndpoint != currentIsEndpoint)
		{
			return candidateIsEndpoint;
		}
		return candidateId < currentId;
	}
}
internal static class SDS_SearchCore
{
	public static Thing FindGlobal(IntVec3 center, Map map, IEnumerable searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, Func<Thing, float> priorityGetter, bool lookInHaulSources, bool canLookInHaulableSources, bool requireReach)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Expected O, but got Unknown
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Expected O, but got Unknown
		Thing chosen = null;
		float bestPrio = float.MinValue;
		float maxDistanceSquared = maxDistance * maxDistance;
		float closestDistSquared = 2.1474836E+09f;
		bool chosenIsMirror = false;
		int chosenId = int.MaxValue;
		bool careAboutHaulSourceEnabled = canLookInHaulableSources && traverseParams.pawn != null && traverseParams.pawn.IsColonist;
		bool readHaulSources = lookInHaulSources || canLookInHaulableSources;
		if (searchSet is IList<Thing> list)
		{
			for (int i = 0; i < list.Count; i++)
			{
				Process(list[i]);
			}
		}
		else if (searchSet is IList<Pawn> list2)
		{
			for (int j = 0; j < list2.Count; j++)
			{
				Process((Thing)(object)list2[j]);
			}
		}
		else if (searchSet is IList<Building> list3)
		{
			for (int k = 0; k < list3.Count; k++)
			{
				Process((Thing)(object)list3[k]);
			}
		}
		else if (searchSet is IList<IAttackTarget> list4)
		{
			for (int l = 0; l < list4.Count; l++)
			{
				Process((Thing)list4[l]);
			}
		}
		else
		{
			foreach (Thing item in searchSet)
			{
				Thing t = item;
				Process(t);
			}
		}
		return chosen;
		void Process(Thing val)
		{
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			if (val == null)
			{
				return;
			}
			bool num;
			if (!requireReach)
			{
				if (val.Spawned)
				{
					goto IL_0042;
				}
				num = !HaulAIUtility.IsInHaulableInventory(val);
			}
			else
			{
				num = !val.Spawned;
			}
			if (num)
			{
				return;
			}
			goto IL_0042;
			IL_0042:
			bool flag = SDS_MirrorRegistry.IsMirror(val);
			float num2;
			if (!flag)
			{
				IntVec3 val2 = center - val.PositionHeld;
				num2 = ((IntVec3)(ref val2)).LengthHorizontalSquared;
			}
			else
			{
				num2 = SDS_AccessPointResolver.EffectiveDistanceSquared(center, val);
			}
			float num3 = num2;
			if (!(num3 > maxDistanceSquared) && (priorityGetter != null || !(num3 > closestDistSquared)))
			{
				Validate(val, num3, flag);
				if (readHaulSources)
				{
					IHaulSource val3 = (IHaulSource)(object)((val is IHaulSource) ? val : null);
					if (val3 != null && (!careAboutHaulSourceEnabled || val3.HaulSourceEnabled))
					{
						ThingOwner directlyHeldThings = ((IThingHolder)val3).GetDirectlyHeldThings();
						for (int m = 0; m < directlyHeldThings.Count; m++)
						{
							Validate(directlyHeldThings[m], num3, isMirror: false);
						}
					}
				}
			}
		}
		void Validate(Thing val, float dist, bool isMirror)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			if ((!requireReach || map.reachability.CanReach(center, LocalTargetInfo.op_Implicit(val.SpawnedParentOrMe), peMode, traverseParams)) && (validator == null || validator(val)))
			{
				float num = ((priorityGetter != null) ? priorityGetter(val) : 0f);
				if (SDS_SearchRanker.PreferMirrorCandidate(num, dist, val.thingIDNumber, isMirror, priorityGetter != null, bestPrio, closestDistSquared, chosenId, chosenIsMirror, chosen != null))
				{
					chosen = val;
					closestDistSquared = dist;
					bestPrio = num;
					chosenIsMirror = isMirror;
					chosenId = val.thingIDNumber;
				}
			}
		}
	}

	public static void CollectMirrorCandidates(Map map, ThingRequest thingReq, IEnumerable<Thing> customGlobalSearchSet, List<Thing> outList)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		CollectMirrorCandidatesRaw(map, thingReq, customGlobalSearchSet, outList);
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			return;
		}
		for (int num = outList.Count - 1; num >= 0; num--)
		{
			SDS_StockKey sDS_StockKey = SDS_StockKey.FromThing(outList[num]);
			if (sDS_StockKey != null && instance.IsForbidden(sDS_StockKey))
			{
				outList.RemoveAt(num);
			}
		}
	}

	private static void CollectMirrorCandidatesRaw(Map map, ThingRequest thingReq, IEnumerable<Thing> customGlobalSearchSet, List<Thing> outList)
	{
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		outList.Clear();
		if (customGlobalSearchSet != null)
		{
			foreach (Thing item in customGlobalSearchSet)
			{
				if (item != null && item.Spawned && item.Map == map && SDS_MirrorRegistry.IsMirror(item) && !outList.Contains(item))
				{
					outList.Add(item);
				}
			}
			return;
		}
		if (thingReq.singleDef != null)
		{
			SDS_MirrorService.CopyMirrorsForDef(map, thingReq.singleDef, outList);
		}
		else
		{
			SDS_MirrorService.CopyMirrorsOn(map, outList);
		}
	}

	public static Thing FindBestMirror(List<Thing> candidates, IntVec3 root, ThingRequest thingReq, Predicate<Thing> validator, Func<Thing, float> priorityGetter, out float bestPrio, out float bestDist)
	{
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		Thing val = null;
		bestPrio = float.MinValue;
		bestDist = float.MaxValue;
		int currentId = int.MaxValue;
		for (int i = 0; i < candidates.Count; i++)
		{
			Thing val2 = candidates[i];
			if (val2 != null && val2.Spawned && SDS_MirrorRegistry.IsMirror(val2) && ((ThingRequest)(ref thingReq)).Accepts(val2) && (validator == null || validator(val2)))
			{
				float num = priorityGetter?.Invoke(val2) ?? 0f;
				float num2 = SDS_AccessPointResolver.EffectiveDistanceSquared(root, val2);
				int thingIDNumber = val2.thingIDNumber;
				if ((priorityGetter != null) ? SDS_SearchRanker.PreferPriority(num, num2, thingIDNumber, bestPrio, bestDist, currentId, val != null) : SDS_SearchRanker.PreferSource(num2, thingIDNumber, bestDist, currentId, val != null))
				{
					val = val2;
					bestPrio = num;
					bestDist = num2;
					currentId = thingIDNumber;
				}
			}
		}
		return val;
	}

	public static bool TryReplaceWithMirror(IntVec3 root, Map map, ThingRequest thingReq, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, Func<Thing, float> priorityGetter, IEnumerable<Thing> customGlobalSearchSet, List<Thing> candidates, ref Thing result)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Invalid comparison between Unknown and I4
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Invalid comparison between Unknown and I4
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || !SDS_MirrorService.AnyMirrorInWorld || !SDS_MirrorService.HasAnyMirrorOn(map))
		{
			return false;
		}
		if (((ThingRequest)(ref thingReq)).IsUndefined || (int)thingReq.group == 1 || (int)thingReq.group == 2 || !GenGrid.InBounds(root, map))
		{
			return false;
		}
		CollectMirrorCandidates(map, thingReq, customGlobalSearchSet, candidates);
		float bestPrio;
		float bestDist;
		Thing val = FindBestMirror(candidates, root, thingReq, validator, priorityGetter, out bestPrio, out bestDist);
		if (val == null)
		{
			return false;
		}
		if (!SDS_AccessPointResolver.CanReachViaAccess(root, map, traverseParams))
		{
			return false;
		}
		if (bestDist > maxDistance * maxDistance)
		{
			return false;
		}
		if (result == null)
		{
			result = val;
			return true;
		}
		float num = priorityGetter?.Invoke(result) ?? 0f;
		if (bestPrio > num)
		{
			result = val;
			return true;
		}
		if (bestPrio < num)
		{
			return false;
		}
		float num2;
		if (!SDS_MirrorRegistry.IsMirror(result))
		{
			IntVec3 val2 = root - result.PositionHeld;
			num2 = ((IntVec3)(ref val2)).LengthHorizontalSquared;
		}
		else
		{
			num2 = SDS_AccessPointResolver.EffectiveDistanceSquared(root, result);
		}
		float realDist = num2;
		if (!SDS_SearchRanker.PreferMirrorOnTie(bestDist, realDist))
		{
			return false;
		}
		result = val;
		return true;
	}
}
[HarmonyPatch(typeof(GenClosest), "ClosestThing_Global")]
[HarmonyPriority(200)]
internal static class Patch_SDS_ClosestGlobal
{
	public static bool Prefix(IntVec3 center, IEnumerable searchSet, float maxDistance, Predicate<Thing> validator, Func<Thing, float> priorityGetter, bool lookInHaulSources, ref Thing __result)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (searchSet == null || !SDS_MirrorService.AnyMirrorInWorld)
		{
			return true;
		}
		try
		{
			__result = Run(center, searchSet, maxDistance, validator, priorityGetter, lookInHaulSources);
			return false;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("GenClosest.ClosestThing_Global 镜像距离重算失败（已放行原版搜索）", exception);
			return true;
		}
	}

	private static Thing Run(IntVec3 center, IEnumerable searchSet, float maxDistance, Predicate<Thing> validator, Func<Thing, float> priorityGetter, bool lookInHaulSources)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return SDS_SearchCore.FindGlobal(center, null, searchSet, (PathEndMode)2, default(TraverseParms), maxDistance, validator, priorityGetter, lookInHaulSources, canLookInHaulableSources: false, requireReach: false);
	}
}
[HarmonyPatch(typeof(GenClosest), "ClosestThing_Global_Reachable")]
[HarmonyPriority(200)]
internal static class Patch_SDS_ClosestGlobalReachable
{
	public static bool Prefix(IntVec3 center, Map map, IEnumerable<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, Func<Thing, float> priorityGetter, bool canLookInHaulableSources, ref Thing __result)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || searchSet == null || !SDS_MirrorService.AnyMirrorInWorld)
		{
			return true;
		}
		try
		{
			__result = Run(center, map, searchSet, peMode, traverseParams, maxDistance, validator, priorityGetter, canLookInHaulableSources);
			return false;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("GenClosest.ClosestThing_Global_Reachable 镜像距离重算失败（已放行原版搜索）", exception);
			return true;
		}
	}

	private static Thing Run(IntVec3 center, Map map, IEnumerable<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, Func<Thing, float> priorityGetter, bool canLookInHaulableSources)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return SDS_SearchCore.FindGlobal(center, map, searchSet, peMode, traverseParams, maxDistance, validator, priorityGetter, lookInHaulSources: false, canLookInHaulableSources, requireReach: true);
	}
}
[HarmonyPatch(typeof(GenClosest), "ClosestThingReachable")]
[HarmonyPriority(200)]
internal static class Patch_SDS_ClosestReachable
{
	private static readonly List<Thing> tmpMirrors = new List<Thing>();

	public static void Postfix(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, IEnumerable<Thing> customGlobalSearchSet, ref Thing __result)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			SDS_SearchCore.TryReplaceWithMirror(root, map, thingReq, traverseParams, maxDistance, validator, null, customGlobalSearchSet, tmpMirrors, ref __result);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("GenClosest 镜像补漏失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(GenClosest), "ClosestThing_Regionwise_ReachablePrioritized")]
[HarmonyPriority(200)]
internal static class Patch_SDS_ClosestRegionwisePrioritized
{
	private static readonly List<Thing> tmpMirrors = new List<Thing>();

	public static void Postfix(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, Func<Thing, float> priorityGetter, ref Thing __result)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			SDS_SearchCore.TryReplaceWithMirror(root, map, thingReq, traverseParams, maxDistance, validator, priorityGetter, null, tmpMirrors, ref __result);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("GenClosest 优先搜索镜像补漏失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(Reachability), "CanReach", new Type[]
{
	typeof(IntVec3),
	typeof(LocalTargetInfo),
	typeof(PathEndMode),
	typeof(TraverseParms)
})]
[HarmonyPriority(200)]
internal static class Patch_SDS_ReachabilityMirror
{
	public static bool Prefix(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams, Map ___map, ref bool __result)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (!((LocalTargetInfo)(ref dest)).HasThing || !SDS_MirrorRegistry.IsMirror(((LocalTargetInfo)(ref dest)).Thing))
		{
			return true;
		}
		try
		{
			__result = SDS_AccessPointResolver.CanReachViaAccess(start, ___map, traverseParams);
			return false;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("Reachability.CanReach 镜像可达性判定失败（已放行原版寻路）", exception);
			return true;
		}
	}
}
public enum SDS_ReclaimPolicy
{
	JobTarget,
	ProtectedOutput
}
public enum SDS_ReturnFallback
{
	None,
	Ground,
	PawnInventory,
	PawnCarry
}
internal static class SDS_Selection
{
	private static readonly Dictionary<ThingDef, long> derivedCounts = new Dictionary<ThingDef, long>();

	private static readonly HashSet<ThingDef> derivedMakeable = new HashSet<ThingDef>();

	private static int derivedCacheFrame = -1;

	private static FieldInfo accessibleThingsField;

	private static bool accessibleThingsFieldChecked;

	public static bool GridAccessibleFrom(Map map)
	{
		SDS_EndpointRejectReason reason;
		return SDS_EndpointContract.ProbeMap(map, out reason);
	}

	public static bool GridAccessibleAnywhere()
	{
		List<Map> maps = Find.Maps;
		if (maps == null)
		{
			return false;
		}
		for (int i = 0; i < maps.Count; i++)
		{
			if (SDS_EndpointContract.ProbeMap(maps[i], out var _))
			{
				return true;
			}
		}
		return false;
	}

	public static long TotalAccessibleCount(ThingDef def)
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || def == null || !instance.Available)
		{
			return 0L;
		}
		return SDS_Exposure.LedgerTotalForDef(def);
	}

	public static long AccessibleCount(ThingDef thing)
	{
		return SDS_Exposure.AccessibleTotal(thing);
	}

	private static void EnsureDerivedCounts()
	{
		int frameCount = RealTime.frameCount;
		if (derivedCacheFrame == frameCount)
		{
			return;
		}
		derivedCacheFrame = frameCount;
		derivedCounts.Clear();
		derivedMakeable.Clear();
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			return;
		}
		List<SDS_StockEntry> list = instance.Snapshot(0);
		for (int i = 0; i < list.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = list[i];
			if (sDS_StockEntry != null && sDS_StockEntry.key != null && sDS_StockEntry.count > 0 && !sDS_StockEntry.key.preserved)
			{
				Thing val = SDS_OutputUtility.CachedDisplayThing(sDS_StockEntry.key);
				if (val != null)
				{
					AppendDerivedProducts(val, sDS_StockEntry.count);
				}
			}
		}
		List<Thing> list2 = new List<Thing>();
		instance.AppendPreservedThings(list2);
		for (int j = 0; j < list2.Count; j++)
		{
			Thing val2 = list2[j];
			if (val2 != null && !val2.Destroyed)
			{
				AppendDerivedProducts(MinifyUtility.GetInnerIfMinified(val2), 1L);
			}
		}
	}

	private static void AppendDerivedProducts(Thing thing, long multiplier)
	{
		if (thing == null || thing.def == null || multiplier <= 0)
		{
			return;
		}
		if (GenLeaving.CanBuildingLeaveResources(thing, (DestroyMode)4))
		{
			List<ThingDefCountClass> list = CostListCalculator.CostListAdjusted(thing);
			for (int i = 0; i < list.Count; i++)
			{
				int num = Mathf.RoundToInt((float)list[i].count * ((BuildableDef)thing.def).resourcesFractionWhenDeconstructed);
				if (num > 0)
				{
					AddDerived(list[i].thingDef, num * multiplier);
				}
			}
		}
		Plant val = (Plant)(object)((thing is Plant) ? thing : null);
		if (val != null && ((Thing)val).def.plant != null && ((Thing)val).def.plant.Harvestable)
		{
			int num2 = Mathf.RoundToInt(((Thing)val).def.plant.harvestYield * Find.Storyteller.difficulty.cropYieldFactor);
			if (num2 > 0)
			{
				AddDerived(((Thing)val).def.plant.harvestedThingDef, num2 * multiplier);
			}
		}
		if (!GenList.NullOrEmpty<ThingDefCountClass>((IList<ThingDefCountClass>)thing.def.butcherProducts))
		{
			for (int j = 0; j < thing.def.butcherProducts.Count; j++)
			{
				ThingDefCountClass val2 = thing.def.butcherProducts[j];
				if (val2 != null)
				{
					AddDerived(val2.thingDef, val2.count * multiplier);
				}
			}
		}
		Pawn val3 = (Pawn)(object)((thing is Pawn) ? thing : null);
		if (val3 != null)
		{
			if (val3.RaceProps.meatDef != null)
			{
				int num3 = Mathf.RoundToInt(StatExtension.GetStatValue((Thing)(object)val3, StatDefOf.MeatAmount, true, -1));
				if (num3 > 0)
				{
					AddDerived(val3.RaceProps.meatDef, num3 * multiplier);
				}
			}
			if (val3.RaceProps.leatherDef != null)
			{
				int num4 = GenMath.RoundRandom(StatExtension.GetStatValue((Thing)(object)val3, StatDefOf.LeatherAmount, true, -1));
				if (num4 > 0)
				{
					AddDerived(val3.RaceProps.leatherDef, num4 * multiplier);
				}
			}
			if (!val3.RaceProps.Humanlike)
			{
				PawnKindLifeStage curKindLifeStage = val3.ageTracker.CurKindLifeStage;
				if (curKindLifeStage != null && curKindLifeStage.butcherBodyPart != null)
				{
					AddDerived(curKindLifeStage.butcherBodyPart.thing, multiplier);
				}
			}
		}
		if (!thing.def.smeltable)
		{
			return;
		}
		List<ThingDefCountClass> list2 = CostListCalculator.CostListAdjusted(thing);
		for (int k = 0; k < list2.Count; k++)
		{
			if (!list2[k].thingDef.intricate)
			{
				int num5 = Mathf.RoundToInt((float)list2[k].count * 0.25f);
				if (num5 > 0)
				{
					AddDerived(list2[k].thingDef, num5 * multiplier);
				}
			}
		}
		if (GenList.NullOrEmpty<ThingDefCountClass>((IList<ThingDefCountClass>)thing.def.smeltProducts))
		{
			return;
		}
		for (int l = 0; l < thing.def.smeltProducts.Count; l++)
		{
			ThingDefCountClass val4 = thing.def.smeltProducts[l];
			if (val4 != null)
			{
				AddDerived(val4.thingDef, val4.count * multiplier);
			}
		}
	}

	private static void AddDerived(ThingDef def, long count)
	{
		if (def != null && count > 0)
		{
			derivedCounts.TryGetValue(def, out var value);
			derivedCounts[def] = value + count;
			derivedMakeable.Add(def);
		}
	}

	public static long PossiblyAccessibleDerivedCount(ThingDef def)
	{
		if (def == null)
		{
			return 0L;
		}
		EnsureDerivedCounts();
		long value;
		return derivedCounts.TryGetValue(def, out value) ? value : 0;
	}

	public static bool CanMakeDerived(ThingDef def)
	{
		if (def == null)
		{
			return false;
		}
		EnsureDerivedCounts();
		return derivedMakeable.Contains(def);
	}

	public static Map CanonicalExposureMap()
	{
		Map anyPlayerHomeMap = Find.AnyPlayerHomeMap;
		if (anyPlayerHomeMap != null && SDS_OutputUtility.MapExposesGrid(anyPlayerHomeMap))
		{
			return anyPlayerHomeMap;
		}
		List<Map> maps = Find.Maps;
		if (maps != null)
		{
			for (int i = 0; i < maps.Count; i++)
			{
				if (maps[i] != null && SDS_OutputUtility.MapExposesGrid(maps[i]))
				{
					return maps[i];
				}
			}
		}
		return null;
	}

	private static List<Thing> GetCachedAccessibleThings()
	{
		if (!accessibleThingsFieldChecked)
		{
			accessibleThingsFieldChecked = true;
			accessibleThingsField = AccessTools.Field(typeof(PlayerItemAccessibilityUtility), "cachedAccessibleThings");
		}
		return (accessibleThingsField != null) ? (accessibleThingsField.GetValue(null) as List<Thing>) : null;
	}

	public static long NonMirrorAccessibleCount(ThingDef def)
	{
		if (def == null)
		{
			return 0L;
		}
		List<Thing> cachedAccessibleThings = GetCachedAccessibleThings();
		if (cachedAccessibleThings == null)
		{
			return 0L;
		}
		long num = 0L;
		for (int i = 0; i < cachedAccessibleThings.Count; i++)
		{
			Thing val = cachedAccessibleThings[i];
			if (val != null && !val.Destroyed && val.def == def && !SDS_MirrorRegistry.IsMirror(val))
			{
				num += val.stackCount;
			}
		}
		return num;
	}

	private static void EnsureResourceCounterFresh(Map map)
	{
		SDS_Exposure.ForceCounterRefresh(map);
	}

	public static long PlayerOwnedCount(ThingDef def)
	{
		long num = TotalAccessibleCount(def);
		long num2 = num;
		List<Map> maps = Find.Maps;
		if (maps != null)
		{
			for (int i = 0; i < maps.Count; i++)
			{
				Map val = maps[i];
				if (val == null || val.resourceCounter == null)
				{
					continue;
				}
				if (def.CountAsResource)
				{
					bool exposed = SDS_OutputUtility.MapExposesGrid(val);
					EnsureResourceCounterFresh(val);
					long num3 = val.resourceCounter.GetCount(def);
					num3 -= SDS_Exposure.InjectedOnCounter(val, def, exposed);
					num2 += ((num3 > 0) ? num3 : 0);
				}
				else
				{
					if (val.listerThings == null)
					{
						continue;
					}
					List<Thing> list = val.listerThings.ThingsOfDef(def);
					for (int j = 0; j < list.Count; j++)
					{
						Thing val2 = list[j];
						if (val2 != null && !val2.Destroyed && !SDS_MirrorRegistry.IsMirror(val2))
						{
							num2 += val2.stackCount;
						}
					}
				}
			}
		}
		List<Caravan> caravans = Find.WorldObjects.Caravans;
		for (int k = 0; k < caravans.Count; k++)
		{
			Caravan val3 = caravans[k];
			if (val3 == null || !val3.IsPlayerControlled)
			{
				continue;
			}
			List<Thing> list2 = CaravanInventoryUtility.AllInventoryItems(val3);
			for (int l = 0; l < list2.Count; l++)
			{
				Thing val4 = list2[l];
				if (val4 != null && !val4.Destroyed && val4.def == def)
				{
					num2 += val4.stackCount;
				}
			}
		}
		List<Site> sites = Find.WorldObjects.Sites;
		for (int m = 0; m < sites.Count; m++)
		{
			Site val5 = sites[m];
			if (val5 == null)
			{
				continue;
			}
			for (int n = 0; n < val5.parts.Count; n++)
			{
				SitePart val6 = val5.parts[n];
				if (val6 == null || val6.things == null)
				{
					continue;
				}
				for (int num4 = 0; num4 < val6.things.Count; num4++)
				{
					Thing val7 = val6.things[num4];
					if (val7 != null && !val7.Destroyed && val7.def == def)
					{
						num2 += val7.stackCount;
					}
				}
			}
			DefeatAllEnemiesQuestComp component = ((WorldObject)val5).GetComponent<DefeatAllEnemiesQuestComp>();
			if (component == null || component.rewards == null)
			{
				continue;
			}
			ThingOwner rewards = component.rewards;
			for (int num5 = 0; num5 < rewards.Count; num5++)
			{
				Thing val8 = rewards[num5];
				if (val8 != null && !val8.Destroyed && val8.def == def)
				{
					num2 += val8.stackCount;
				}
			}
		}
		return num2;
	}

	public static bool HasPreservedMatching(ThingFilter filter)
	{
		if (filter == null)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			return false;
		}
		List<Thing> list = new List<Thing>();
		instance.AppendPreservedThings(list);
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if (val != null && !val.Destroyed && filter.Allows(val))
			{
				return true;
			}
		}
		return false;
	}
}
[HarmonyPatch(typeof(PlayerItemAccessibilityUtility), "Accessible")]
[HarmonyPriority(200)]
internal static class Patch_SDS_Accessible
{
	public static void Postfix(ThingDef thing, int count, Map map, ref bool __result)
	{
		try
		{
			if (!__result && thing != null && count > 0 && map != null && SDS_Selection.GridAccessibleFrom(map))
			{
				long num = SDS_Selection.AccessibleCount(thing);
				__result = num >= count;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("玩家可及性补全失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(PlayerItemAccessibilityUtility), "PossiblyAccessible")]
[HarmonyPriority(200)]
internal static class Patch_SDS_PossiblyAccessible
{
	public static void Postfix(ThingDef thing, int count, Map map, ref bool __result)
	{
		try
		{
			if (!__result && thing != null && count > 0 && map != null && SDS_Selection.GridAccessibleFrom(map))
			{
				__result = SDS_Selection.AccessibleCount(thing) + SDS_Selection.PossiblyAccessibleDerivedCount(thing) >= count;
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("玩家潜在可及性补全失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(PlayerItemAccessibilityUtility), "PlayerCanMake")]
[HarmonyPriority(200)]
internal static class Patch_SDS_PlayerCanMake
{
	public static void Postfix(ThingDef thing, Map map, ref bool __result)
	{
		try
		{
			if (!__result && thing != null && map != null && SDS_Selection.GridAccessibleFrom(map))
			{
				__result = SDS_Selection.CanMakeDerived(thing);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("玩家可制造判定补全失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(PlayerItemAccessibilityUtility), "PlayerOrQuestRewardHas", new Type[]
{
	typeof(ThingDef),
	typeof(int)
})]
[HarmonyPriority(200)]
internal static class Patch_SDS_PlayerHasDef
{
	public static bool Prefix(ThingDef thingDef, int count, ref bool __result)
	{
		try
		{
			if (thingDef == null || count <= 0)
			{
				return true;
			}
			if (!SDS_Selection.GridAccessibleAnywhere())
			{
				return true;
			}
			__result = SDS_Selection.PlayerOwnedCount(thingDef) >= count;
			return false;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("玩家持有量重算失败（已放行原版）", exception);
			return true;
		}
	}
}
[HarmonyPatch(typeof(PlayerItemAccessibilityUtility), "PlayerOrQuestRewardHas", new Type[] { typeof(ThingFilter) })]
[HarmonyPriority(200)]
internal static class Patch_SDS_PlayerHasFilter
{
	public static void Postfix(ThingFilter thingFilter, ref bool __result)
	{
		try
		{
			if (!__result && thingFilter != null && SDS_Selection.GridAccessibleAnywhere())
			{
				__result = SDS_Selection.HasPreservedMatching(thingFilter);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("玩家持有筛选补全失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
internal static class SDS_SelfCheck
{
	private const int MaxReported = 20;

	[DebugAction("超维存储", "自检：账本 / 预留 / 镜像", false, false, false, false, false, 0, false)]
	public static void RunLedgerChecks()
	{
		Run(verbose: true);
	}

	internal static void Run(bool verbose)
	{
		try
		{
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null)
			{
				if (verbose)
				{
					Log.Message("[超维存储][自检] 当前没有运行期网格，跳过。");
				}
				return;
			}
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			List<SDS_StockEntry> list = instance.Snapshot(0);
			for (int i = 0; i < list.Count; i++)
			{
				SDS_StockEntry sDS_StockEntry = list[i];
				if (sDS_StockEntry == null || sDS_StockEntry.key == null || sDS_StockEntry.key.preserved)
				{
					continue;
				}
				long count = instance.GetCount(sDS_StockEntry.key);
				long reservedCount = instance.GetReservedCount(sDS_StockEntry.key);
				long num4 = count - reservedCount;
				if (num4 < 0)
				{
					num4 = 0L;
				}
				long availableCount = instance.GetAvailableCount(sDS_StockEntry.key);
				num++;
				if (availableCount != num4)
				{
					num2++;
					if (num3++ < 20)
					{
						Log.Error("[超维存储][自检] 虚拟可用量不符：def=" + sDS_StockEntry.key.EffectiveDefName + " total=" + count + " reserved=" + reservedCount + " expected=" + num4 + " actual=" + availableCount);
					}
				}
			}
			List<Thing> list2 = new List<Thing>();
			instance.AppendPreservedThings(list2);
			HashSet<SDS_StockKey> hashSet = new HashSet<SDS_StockKey>();
			for (int j = 0; j < list2.Count; j++)
			{
				SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(list2[j]);
				if (sDS_StockKey == null || !hashSet.Add(sDS_StockKey) || !instance.TryGetPreservedTotals(sDS_StockKey, out var total, out var available))
				{
					continue;
				}
				long reservedCount2 = instance.GetReservedCount(sDS_StockKey);
				long num5 = total - reservedCount2;
				if (num5 < 0)
				{
					num5 = 0L;
				}
				num++;
				if (available != num5)
				{
					num2++;
					if (num3++ < 20)
					{
						Log.Error("[超维存储][自检] 保真可用量不符：def=" + sDS_StockKey.EffectiveDefName + " total=" + total + " reserved=" + reservedCount2 + " expected=" + num5 + " actual=" + available);
					}
				}
			}
			List<Map> maps = Find.Maps;
			List<SDS_StockKey> list3 = new List<SDS_StockKey>();
			for (int k = 0; k < maps.Count; k++)
			{
				Map val = maps[k];
				if (val == null || val.Disposed)
				{
					continue;
				}
				list3.Clear();
				SDS_MirrorService.CopyMirrorKeys(val, list3);
				for (int l = 0; l < list3.Count; l++)
				{
					SDS_StockKey sDS_StockKey2 = list3[l];
					if (!SDS_MirrorService.TryGetMirror(val, sDS_StockKey2, out var mirror))
					{
						continue;
					}
					num++;
					if (mirror == null || mirror.Destroyed || !mirror.Spawned)
					{
						num2++;
						if (num3++ < 20)
						{
							Log.Error("[超维存储][自检] 镜像失效：map=" + val.uniqueID + " def=" + ((sDS_StockKey2 != null) ? sDS_StockKey2.EffectiveDefName : "?"));
						}
					}
				}
			}
			if (num2 > 0)
			{
				Log.Warning("[超维存储][自检] 完成：检查 " + num + " 项，失败 " + num2 + " 项（逐条最多列 " + 20 + " 条）。");
			}
			else if (verbose || Prefs.DevMode)
			{
				Log.Message("[超维存储][自检] 完成：检查 " + num + " 项，失败 0 项。");
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("自检执行失败（已忽略）", exception);
		}
	}
}
internal static class SDS_StaticState
{
	public static int ResetAll()
	{
		int num = 0;
		SDS_MirrorService.Reset();
		num++;
		SDS_ProjectionService.Reset();
		num++;
		SDS_EndpointResolver.Reset();
		num++;
		SDS_ReclaimRegistry.Reset();
		num++;
		SDS_VirtualDisplayRegistry.Reset();
		num++;
		SDS_OutputUtility.ResetDisplayCaches();
		num++;
		SDS_PreservedOffer.Reset();
		num++;
		SDS_TradeVirtual.Reset();
		num++;
		SDS_TransportVirtual.Reset();
		num++;
		SDS_CaravanVirtual.Reset();
		num++;
		SDS_ModCompat_CE.Reset();
		num++;
		SDS_ModCompat_MiliraExpanded.Reset();
		num++;
		SDS_ProducedThingMarker.Reset();
		num++;
		SDS_DepositDispatchLedger.Reset();
		num++;
		SDS_MirrorHaulDestination.Reset();
		num++;
		SDS_Exposure.Reset();
		num++;
		SDS_ReadOnlyUI.Reset();
		num++;
		SDS_ImplantUtility.ResetChipCache();
		num++;
		if (Prefs.DevMode)
		{
			Log.Message("[超维存储][状态] 运行期静态状态已清理：E1 登记 " + num + " 项。");
		}
		return num;
	}
}
public class SDS_StockKey : IExposable, IEquatable<SDS_StockKey>
{
	public ThingDef thingDef;

	public ThingDef stuff;

	public int qualityInt = -1;

	public string defName;

	public string stuffName;

	public bool preserved;

	public string EffectiveDefName => (thingDef != null) ? ((Def)thingDef).defName : defName;

	public string EffectiveStuffName => (stuff != null) ? ((Def)stuff).defName : stuffName;

	public bool Resolved => thingDef != null;

	public bool HasIdentity
	{
		get
		{
			string effectiveDefName = EffectiveDefName;
			return !string.IsNullOrEmpty(effectiveDefName) && effectiveDefName != "null";
		}
	}

	public QualityCategory? Quality => (qualityInt < 0) ? ((QualityCategory?)null) : new QualityCategory?((QualityCategory)(byte)qualityInt);

	public string Label
	{
		get
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			if (thingDef == null)
			{
				return "?";
			}
			string text = ((object)((Def)thingDef).LabelCap/*cast due to constrained. prefix*/).ToString();
			if (stuff != null)
			{
				text = ((object)((Def)stuff).LabelCap/*cast due to constrained. prefix*/).ToString() + " " + text;
			}
			if (qualityInt >= 0)
			{
				text = text + " (" + ((object)Translator.Translate("QualityCategory_" + ((object)(QualityCategory)(byte)qualityInt/*cast due to constrained. prefix*/).ToString())/*cast due to constrained. prefix*/).ToString() + ")";
			}
			return text;
		}
	}

	public SDS_StockKey()
	{
	}

	public SDS_StockKey(ThingDef thingDef, ThingDef stuff, int qualityInt)
		: this(thingDef, stuff, qualityInt, preserved: false)
	{
	}

	public SDS_StockKey(ThingDef thingDef, ThingDef stuff, int qualityInt, bool preserved)
	{
		this.thingDef = thingDef;
		this.stuff = stuff;
		this.qualityInt = qualityInt;
		this.preserved = preserved;
		defName = ((Def)(thingDef?)).defName;
		stuffName = ((Def)(stuff?)).defName;
	}

	public void EnsureNames()
	{
		if (thingDef != null)
		{
			defName = ((Def)thingDef).defName;
		}
		if (stuff != null)
		{
			stuffName = ((Def)stuff).defName;
		}
		if (string.IsNullOrEmpty(defName) || defName == "null")
		{
			defName = null;
		}
		if (string.IsNullOrEmpty(stuffName) || stuffName == "null")
		{
			stuffName = null;
		}
	}

	public bool TryResolve()
	{
		bool result = false;
		if (thingDef == null && HasIdentity)
		{
			ThingDef val = SDS_DefResolve.ResolveThingDef(defName);
			if (val != null)
			{
				thingDef = val;
				defName = ((Def)val).defName;
				result = true;
			}
		}
		if (stuff == null && !string.IsNullOrEmpty(stuffName) && stuffName != "null")
		{
			ThingDef val2 = SDS_DefResolve.ResolveThingDef(stuffName);
			if (val2 != null && val2.IsStuff)
			{
				stuff = val2;
				stuffName = ((Def)val2).defName;
				result = true;
			}
		}
		return result;
	}

	public static SDS_StockKey FromThing(Thing thing)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		if (thing == null)
		{
			return null;
		}
		CompQuality val = ThingCompUtility.TryGetComp<CompQuality>(thing);
		return new SDS_StockKey(thing.def, thing.Stuff, (val == null) ? (-1) : ((int)val.Quality));
	}

	public static SDS_StockKey FromPreservedThing(Thing thing)
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		if (thing == null)
		{
			return null;
		}
		MinifiedThing val = (MinifiedThing)(object)((thing is MinifiedThing) ? thing : null);
		if (val != null && val.InnerThing != null)
		{
			Thing innerThing = val.InnerThing;
			CompQuality val2 = ThingCompUtility.TryGetComp<CompQuality>(innerThing);
			return new SDS_StockKey(innerThing.def, innerThing.Stuff, (val2 == null) ? (-1) : ((int)val2.Quality), preserved: true);
		}
		CompQuality val3 = ThingCompUtility.TryGetComp<CompQuality>(thing);
		return new SDS_StockKey(thing.def, thing.Stuff, (val3 == null) ? (-1) : ((int)val3.Quality), preserved: true);
	}

	public static SDS_StockKey FromAnyThing(Thing thing)
	{
		if (thing == null)
		{
			return null;
		}
		return SDS_DepositRules.RequiresPreservedStorage(thing) ? FromPreservedThing(thing) : FromThing(thing);
	}

	public bool Equals(SDS_StockKey other)
	{
		return SameAs(other);
	}

	public override bool Equals(object obj)
	{
		return obj is SDS_StockKey other && SameAs(other);
	}

	public override int GetHashCode()
	{
		int num = GenText.StableStringHash(EffectiveDefName ?? string.Empty);
		string effectiveStuffName = EffectiveStuffName;
		if (!string.IsNullOrEmpty(effectiveStuffName) && effectiveStuffName != "null")
		{
			num = (num * 397) ^ GenText.StableStringHash(effectiveStuffName);
		}
		return (((num * 397) ^ qualityInt) * 397) ^ (preserved ? 1 : 0);
	}

	public bool SameAs(SDS_StockKey other)
	{
		if (other == null || other.qualityInt != qualityInt || other.preserved != preserved)
		{
			return false;
		}
		if (thingDef != null && other.thingDef != null)
		{
			return other.thingDef == thingDef && other.stuff == stuff;
		}
		return string.Equals(EffectiveDefName, other.EffectiveDefName, StringComparison.Ordinal) && string.Equals(EffectiveStuffName, other.EffectiveStuffName, StringComparison.Ordinal);
	}

	public static int CompareIdentity(SDS_StockKey a, SDS_StockKey b)
	{
		if (a == b)
		{
			return 0;
		}
		if (a == null)
		{
			return -1;
		}
		if (b == null)
		{
			return 1;
		}
		int num = string.CompareOrdinal(a.EffectiveDefName ?? string.Empty, b.EffectiveDefName ?? string.Empty);
		if (num != 0)
		{
			return num;
		}
		num = string.CompareOrdinal(a.EffectiveStuffName ?? string.Empty, b.EffectiveStuffName ?? string.Empty);
		if (num != 0)
		{
			return num;
		}
		num = a.qualityInt.CompareTo(b.qualityInt);
		if (num != 0)
		{
			return num;
		}
		return (a.preserved ? 1 : 0).CompareTo(b.preserved ? 1 : 0);
	}

	public void ExposeData()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		if ((int)Scribe.mode == 1)
		{
			EnsureNames();
			Scribe_Values.Look<string>(ref defName, "defName", (string)null, false);
			Scribe_Values.Look<string>(ref stuffName, "stuffName", (string)null, false);
			string text = defName ?? "null";
			string text2 = stuffName ?? "null";
			Scribe_Values.Look<string>(ref text, "thingDef", "null", false);
			Scribe_Values.Look<string>(ref text2, "stuff", "null", false);
			Scribe_Values.Look<int>(ref qualityInt, "qualityInt", -1, false);
			Scribe_Values.Look<bool>(ref preserved, "preserved", false, false);
			return;
		}
		Scribe_Values.Look<string>(ref defName, "defName", (string)null, false);
		Scribe_Values.Look<string>(ref stuffName, "stuffName", (string)null, false);
		string text3 = null;
		string text4 = null;
		Scribe_Values.Look<string>(ref text3, "thingDef", (string)null, false);
		Scribe_Values.Look<string>(ref text4, "stuff", (string)null, false);
		Scribe_Values.Look<int>(ref qualityInt, "qualityInt", -1, false);
		Scribe_Values.Look<bool>(ref preserved, "preserved", false, false);
		if (string.IsNullOrEmpty(defName) || defName == "null")
		{
			defName = ((text3 == "null") ? null : text3);
		}
		if (string.IsNullOrEmpty(stuffName) || stuffName == "null")
		{
			stuffName = ((text4 == "null") ? null : text4);
		}
		defName = defName?.Trim();
		stuffName = stuffName?.Trim();
		TryResolve();
	}
}
internal static class SDS_DefResolve
{
	public static ThingDef ResolveThingDef(string defName)
	{
		if (string.IsNullOrEmpty(defName) || defName == "null")
		{
			return null;
		}
		string text = defName;
		try
		{
			text = BackCompatibility.BackCompatibleDefName(typeof(ThingDef), defName, false, (XmlNode)null) ?? defName;
		}
		catch (Exception)
		{
		}
		return DefDatabase<ThingDef>.GetNamedSilentFail(text);
	}
}
public class SDS_StockEntry : IExposable
{
	public SDS_StockKey key;

	public long count;

	public bool preserved;

	public SDS_StockKey displayKey;

	public float avgHitPoints = -1f;

	public float avgRotProgress = -1f;

	public SDS_StockEntry()
	{
	}

	public SDS_StockEntry(SDS_StockKey key, long count)
	{
		this.key = key;
		this.count = count;
	}

	public SDS_StockEntry(SDS_StockKey key, long count, bool preserved)
	{
		this.key = key;
		this.count = count;
		this.preserved = preserved;
	}

	public void ExposeData()
	{
		Scribe_Deep.Look<SDS_StockKey>(ref key, "key", Array.Empty<object>());
		Scribe_Values.Look<long>(ref count, "count", 0L, false);
		Scribe_Values.Look<float>(ref avgHitPoints, "avgHitPoints", -1f, false);
		Scribe_Values.Look<float>(ref avgRotProgress, "avgRotProgress", -1f, false);
	}
}
public sealed class SDS_StockVitals
{
	public float hitPoints = -1f;

	public float rotProgress = -1f;

	public static float CaptureHitPoints(Thing thing)
	{
		return (thing != null && thing.def != null && thing.def.useHitPoints) ? ((float)thing.HitPoints) : (-1f);
	}

	public static float CaptureRotProgress(Thing thing)
	{
		CompRottable val = ((thing != null) ? ThingCompUtility.TryGetComp<CompRottable>(thing) : null);
		return (val != null) ? val.RotProgress : (-1f);
	}
}
public static class SDS_DepositRules
{
	private static readonly Dictionary<ThingDef, bool> rebuildCache = new Dictionary<ThingDef, bool>();

	private static readonly string[] virtualDefaultCategoryDefNames = new string[4] { "Manufactured", "ResourcesRaw", "Foods", "Chunks" };

	public static bool CanRebuildDef(ThingDef def)
	{
		if (def == null)
		{
			return false;
		}
		if (rebuildCache.TryGetValue(def, out var value))
		{
			return value;
		}
		bool flag = !IsLegacyUnrebuildableDef(def) && (IsVirtualDefaultCategory(def) || !HasDefLevelNonKeyState(def));
		rebuildCache[def] = flag;
		return flag;
	}

	public static bool IsVirtualDefaultCategory(ThingDef def)
	{
		return InAnyCategoryTree(def, virtualDefaultCategoryDefNames);
	}

	private static bool InAnyCategoryTree(ThingDef def, string[] categoryDefNames)
	{
		if (def == null || def.thingCategories == null || def.thingCategories.Count == 0)
		{
			return false;
		}
		for (int i = 0; i < def.thingCategories.Count; i++)
		{
			for (ThingCategoryDef val = def.thingCategories[i]; val != null; val = val.parent)
			{
				for (int j = 0; j < categoryDefNames.Length; j++)
				{
					if (((Def)val).defName == categoryDefNames[j])
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private static bool HasDefLevelNonKeyState(ThingDef def)
	{
		return def.HasComp<CompArt>() || def.HasComp<CompBook>() || def.HasComp<CompBladelinkWeapon>() || def.HasComp<CompGeneratedNames>() || def.HasComp<CompUniqueWeapon>();
	}

	private static bool HasInstanceNonKeyState(Thing thing)
	{
		ThingDef val = thing?.def;
		if (val == null || IsVirtualDefaultCategory(val) || IsLegacyUnrebuildableDef(val))
		{
			return false;
		}
		CompColorable val2 = ThingCompUtility.TryGetComp<CompColorable>(thing);
		if (val2 != null && val2.Active)
		{
			return true;
		}
		CompStyleable val3 = ThingCompUtility.TryGetComp<CompStyleable>(thing);
		if (val3 != null && val3.styleDef != null)
		{
			return true;
		}
		CompIngredients val4 = ThingCompUtility.TryGetComp<CompIngredients>(thing);
		if (val4 != null && val4.ingredients != null && val4.ingredients.Count > 0)
		{
			return true;
		}
		CompRefuelable val5 = ThingCompUtility.TryGetComp<CompRefuelable>(thing);
		if (val5 != null && val5.Fuel > 0f)
		{
			return true;
		}
		CompApparelVerbOwner_Charged val6 = ThingCompUtility.TryGetComp<CompApparelVerbOwner_Charged>(thing);
		if (val6 != null && val6.RemainingCharges > 0)
		{
			return true;
		}
		CompUniqueWeapon val7 = ThingCompUtility.TryGetComp<CompUniqueWeapon>(thing);
		if (val7 != null && val7.TraitsListForReading != null && val7.TraitsListForReading.Count > 0)
		{
			return true;
		}
		return false;
	}

	public static bool IsLegacyUnrebuildableDef(ThingDef def)
	{
		if (def == null)
		{
			return true;
		}
		Type thingClass = def.thingClass;
		return thingClass != null && (typeof(Corpse).IsAssignableFrom(thingClass) || typeof(MinifiedThing).IsAssignableFrom(thingClass) || typeof(UnfinishedThing).IsAssignableFrom(thingClass) || typeof(GeneSetHolderBase).IsAssignableFrom(thingClass));
	}

	public static bool AllowedByDefault(ThingDef def)
	{
		if (!CanStoreDef(def))
		{
			return false;
		}
		if (IsLegacyUnrebuildableDef(def))
		{
			Type thingClass = def.thingClass;
			return thingClass != null && typeof(GeneSetHolderBase).IsAssignableFrom(thingClass);
		}
		return def.EverHaulable;
	}

	public static bool RequiresPreservedStorage(ThingDef def)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Invalid comparison between Unknown and I4
		return def != null && (int)def.category == 2 && !IsUnfinishedDef(def) && !CanRebuildDef(def);
	}

	public static bool RequiresPreservedStorage(Thing thing)
	{
		return thing != null && (RequiresPreservedStorage(thing.def) || IsProtectedThing(thing) || HasInstanceNonKeyState(thing));
	}

	public static bool IsProtectedThing(Thing thing)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Invalid comparison between Unknown and I4
		if (thing == null || thing.def == null || (int)thing.def.category != 2)
		{
			return false;
		}
		if (thing.questTags != null && thing.questTags.Count > 0)
		{
			return true;
		}
		if (ThingCompUtility.TryGetComp<CompGeneratedNames>(thing) != null || ThingCompUtility.TryGetComp<CompBladelinkWeapon>(thing) != null)
		{
			return true;
		}
		CompArt val = ThingCompUtility.TryGetComp<CompArt>(thing);
		if (val != null && val.Active)
		{
			return true;
		}
		return ReliquaryUtility.IsRelic(thing);
	}

	public static bool IsUnfinishedDef(ThingDef def)
	{
		return def != null && (def.isUnfinishedThing || typeof(UnfinishedThing).IsAssignableFrom(def.thingClass));
	}

	public static bool CanStoreDef(ThingDef def)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Invalid comparison between Unknown and I4
		return def != null && (int)def.category == 2 && !IsUnfinishedDef(def);
	}

	public static bool CanStore(Thing thing)
	{
		return thing != null && !thing.Destroyed && thing.stackCount > 0 && CanStoreDef(thing.def);
	}
}
public class SDS_Reservation : IExposable
{
	public int jobLoadID;

	public SDS_StockKey key;

	public int count;

	public Pawn pawn;

	public void ExposeData()
	{
		Scribe_Values.Look<int>(ref jobLoadID, "jobLoadID", 0, false);
		Scribe_Deep.Look<SDS_StockKey>(ref key, "key", Array.Empty<object>());
		Scribe_Values.Look<int>(ref count, "count", 0, false);
		Scribe_References.Look<Pawn>(ref pawn, "pawn", false);
	}
}
internal static class SDS_TradeVirtual
{
	private static readonly Dictionary<Thing, List<Thing>> preservedBuckets = new Dictionary<Thing, List<Thing>>();

	private static readonly HashSet<Thing> preservedFakes = new HashSet<Thing>();

	internal static void RegisterFake(Thing fake)
	{
		SDS_VirtualDisplayRegistry.Register(fake, SDS_VirtualDisplayRegistry.TradeOwner);
	}

	private static void RegisterPreservedBucket(Thing fake, List<Thing> bucket)
	{
		if (fake != null && bucket != null)
		{
			RegisterFake(fake);
			preservedFakes.Add(fake);
			preservedBuckets[fake] = bucket;
		}
	}

	internal static IEnumerable<Thing> VirtualDisplayStream(SDS_GameComponent_Grid grid, ITrader trader, Action<Thing> onDisplay)
	{
		if (grid == null)
		{
			yield break;
		}
		List<SDS_StockEntry> snapshot = grid.Snapshot(0);
		for (int i = 0; i < snapshot.Count; i++)
		{
			SDS_StockEntry entry = snapshot[i];
			if (entry == null || entry.key == null || entry.key.thingDef == null || entry.key.preserved || entry.count <= 0)
			{
				continue;
			}
			long available = SDS_Availability.GetTradableCount(grid, entry.key);
			if (available > 0)
			{
				Thing fake = SDS_OutputUtility.CachedDisplayThing(entry.key);
				if (fake != null && TradeUtility.PlayerSellableNow(fake, trader))
				{
					fake.stackCount = (int)Math.Min(available, 2147483647L);
					onDisplay?.Invoke(fake);
					yield return fake;
				}
			}
		}
	}

	public static bool TryGetLocalTrader(out ITrader trader, out Map map)
	{
		trader = TradeSession.trader;
		map = ((TradeSession.playerNegotiator != null) ? ((Thing)TradeSession.playerNegotiator).Map : null);
		if (map == null)
		{
			return false;
		}
		ITrader obj = trader;
		Pawn val = (Pawn)(object)((obj is Pawn) ? obj : null);
		if (val != null)
		{
			return ((Thing)val).Spawned && ((Thing)val).Map == map;
		}
		ITrader obj2 = trader;
		TradeShip val2 = (TradeShip)(object)((obj2 is TradeShip) ? obj2 : null);
		if (val2 != null)
		{
			return ((PassingShip)val2).Map == map;
		}
		return false;
	}

	private static bool MapReady(Map map)
	{
		return SDS_GameComponent_Grid.Instance != null && SDS_OutputUtility.MapHasOperationalEndpoint(map);
	}

	public static void Reset()
	{
		SDS_VirtualDisplayRegistry.Cleanup(SDS_VirtualDisplayRegistry.TradeOwner);
		preservedFakes.Clear();
		preservedBuckets.Clear();
		SDS_OutputUtility.PruneBucketDisplays();
	}

	private static void InjectPreserved(SDS_GameComponent_Grid grid, ITrader trader, List<Tradeable> tradeables)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		List<Thing> list = BuildTradePreservedDisplays(grid, trader);
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			Tradeable val2 = TransferableUtility.TradeableMatching(val, tradeables);
			if (val2 == null)
			{
				val2 = new Tradeable();
				tradeables.Add(val2);
			}
			val2.AddThing(val, (Transactor)0);
		}
	}

	internal static List<Thing> BuildPreservedDisplays(SDS_GameComponent_Grid grid, ITrader trader)
	{
		List<SDS_PreservedBucket> list = BuildPreservedBuckets(grid, trader);
		List<Thing> list2 = new List<Thing>();
		for (int i = 0; i < list.Count; i++)
		{
			SDS_PreservedBucket sDS_PreservedBucket = list[i];
			if (sDS_PreservedBucket != null && sDS_PreservedBucket.display != null)
			{
				list2.Add(sDS_PreservedBucket.display);
			}
		}
		return list2;
	}

	internal static List<Thing> BuildTradePreservedDisplays(SDS_GameComponent_Grid grid, ITrader trader)
	{
		List<SDS_PreservedBucket> list = BuildPreservedBuckets(grid, trader);
		List<Thing> list2 = new List<Thing>();
		for (int i = 0; i < list.Count; i++)
		{
			SDS_PreservedBucket sDS_PreservedBucket = list[i];
			if (sDS_PreservedBucket != null && sDS_PreservedBucket.display != null)
			{
				RegisterPreservedBucket(sDS_PreservedBucket.display, sDS_PreservedBucket.things);
				list2.Add(sDS_PreservedBucket.display);
			}
		}
		return list2;
	}

	private static List<SDS_PreservedBucket> BuildPreservedBuckets(SDS_GameComponent_Grid grid, ITrader trader)
	{
		return SDS_PreservedBuckets.Build(grid, (Thing thing) => TradeUtility.PlayerSellableNow(thing, trader), tradeGrouping: true, trader, tradableBudget: true);
	}

	public static void Inject(TradeDeal deal)
	{
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Expected O, but got Unknown
		if (deal == null)
		{
			return;
		}
		ITrader trader;
		Map map;
		if (TradeSession.giftMode)
		{
			trader = TradeSession.trader;
			map = ((TradeSession.playerNegotiator != null) ? ((Thing)TradeSession.playerNegotiator).Map : null);
			if (trader == null || map == null || !MapReady(map))
			{
				return;
			}
		}
		else if (!TryGetLocalTrader(out trader, out map) || !MapReady(map))
		{
			return;
		}
		if (trader is TradeShip)
		{
			bool flag = false;
			using (IEnumerator<Building_OrbitalTradeBeacon> enumerator = Building_OrbitalTradeBeacon.AllPowered(map).GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					Building_OrbitalTradeBeacon current = enumerator.Current;
					flag = true;
				}
			}
			if (!flag)
			{
				return;
			}
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		List<Tradeable> allTradeables = deal.AllTradeables;
		foreach (Thing item in VirtualDisplayStream(instance, trader, null))
		{
			Tradeable val = TransferableUtility.TradeableMatching(item, allTradeables);
			int num = ((val != null) ? val.CountHeldBy((Transactor)0) : 0);
			int num2 = Math.Max(0, int.MaxValue - num);
			if (item.stackCount > num2)
			{
				item.stackCount = num2;
			}
			if (item.stackCount > 0)
			{
				if (val == null)
				{
					val = new Tradeable();
					allTradeables.Add(val);
				}
				val.AddThing(item, (Transactor)0);
				RegisterFake(item);
			}
		}
		InjectPreserved(instance, trader, allTradeables);
	}

	public static bool Validate(TradeDeal deal, ref bool actuallyTraded, ref bool __result)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Invalid comparison between Unknown and I4
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		if (deal == null)
		{
			return true;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			return true;
		}
		bool flag = false;
		Dictionary<SDS_StockKey, int> budget = null;
		List<Tradeable> allTradeables = deal.AllTradeables;
		for (int i = 0; i < allTradeables.Count; i++)
		{
			if (flag)
			{
				break;
			}
			Tradeable val = allTradeables[i];
			if (val == null || (int)val.ActionToDo != 2)
			{
				continue;
			}
			int num = ((Transferable)val).CountToTransferToDestination;
			for (int j = 0; j < val.thingsColony.Count; j++)
			{
				if (num <= 0)
				{
					break;
				}
				Thing val2 = val.thingsColony[j];
				int num2 = Math.Min(num, val2.stackCount);
				if (SDS_VirtualDisplayRegistry.IsFake(val2))
				{
					if (preservedFakes.Contains(val2))
					{
						preservedBuckets.TryGetValue(val2, out var value);
						SDS_StockKey sDS_StockKey = SDS_OutputUtility.KeyOfDisplay(val2, value);
						int num3 = BudgetLeft(ref budget, instance, sDS_StockKey);
						if (sDS_StockKey == null || !sDS_StockKey.preserved || num3 < num2)
						{
							flag = true;
							break;
						}
						SDS_PreservedBuckets.TakePlan(instance, value, num2, out var plannedUnits);
						if (plannedUnits < num2)
						{
							flag = true;
							break;
						}
						budget[sDS_StockKey] = num3 - num2;
					}
					else
					{
						SDS_StockKey sDS_StockKey2 = SDS_OutputUtility.KeyOfDisplay(val2, null);
						int num4 = BudgetLeft(ref budget, instance, sDS_StockKey2);
						if (sDS_StockKey2 == null || num4 < num2)
						{
							flag = true;
							break;
						}
						budget[sDS_StockKey2] = num4 - num2;
					}
				}
				num -= num2;
			}
		}
		if (!flag)
		{
			return true;
		}
		Messages.Message(TaggedString.op_Implicit(Translator.Translate("SDS.Trade.VirtualStorageChanged")), MessageTypeDefOf.RejectInput, false);
		actuallyTraded = false;
		__result = false;
		return false;
	}

	private static int BudgetLeft(ref Dictionary<SDS_StockKey, int> budget, SDS_GameComponent_Grid grid, SDS_StockKey key)
	{
		if (key == null)
		{
			return 0;
		}
		if (budget == null)
		{
			budget = new Dictionary<SDS_StockKey, int>();
		}
		if (!budget.TryGetValue(key, out var value))
		{
			value = (int)Math.Min(SDS_Availability.GetTradableCount(grid, key), 2147483647L);
			budget[key] = value;
		}
		return value;
	}

	public static bool HandoffSold(Pawn_TraderTracker trader, Thing toGive, int countToGive)
	{
		if (trader == null)
		{
			return true;
		}
		return HandoffSoldCore(toGive, countToGive, delegate(Thing t, int c)
		{
			trader.GiveSoldThingToTrader(t, c, TradeSession.playerNegotiator);
		});
	}

	public static bool HandoffSold(ITrader trader, Thing toGive, int countToGive)
	{
		if (trader == null)
		{
			return true;
		}
		return HandoffSoldCore(toGive, countToGive, delegate(Thing t, int c)
		{
			trader.GiveSoldThingToTrader(t, c, TradeSession.playerNegotiator);
		});
	}

	internal static bool HandoffSoldCore(Thing toGive, int countToGive, Action<Thing, int> giveToTrader)
	{
		if (toGive == null || countToGive <= 0 || toGive.def == null || !SDS_VirtualDisplayRegistry.IsFake(toGive))
		{
			return true;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || giveToTrader == null)
		{
			return true;
		}
		if (preservedFakes.Contains(toGive))
		{
			int plannedUnits = 0;
			List<ThingCount> list = new List<ThingCount>();
			if (preservedBuckets.TryGetValue(toGive, out var value) && value != null)
			{
				list = SDS_PreservedBuckets.TakePlan(instance, value, countToGive, out plannedUnits);
			}
			List<Thing> made = null;
			if (plannedUnits >= countToGive && list.Count > 0 && !SDS_WithdrawExecutor.TryWithdrawPreservedExact(list, out made))
			{
				made = null;
			}
			if (made == null || made.Count == 0)
			{
				SDS_Diag.ErrorOnce("交易保真物化失败（校验后桶内真身已变化），本笔交付已跳过", new InvalidOperationException("TryWithdrawPreservedExact failed for " + ((Def)toGive.def).defName));
				return false;
			}
			preservedFakes.Remove(toGive);
			SDS_VirtualDisplayRegistry.Forget(toGive, SDS_VirtualDisplayRegistry.TradeOwner);
			preservedBuckets.Remove(toGive);
			for (int i = 0; i < made.Count; i++)
			{
				Thing val = made[i];
				if (val != null && !val.Destroyed)
				{
					giveToTrader(val, val.stackCount);
				}
			}
			return false;
		}
		SDS_StockKey sDS_StockKey = SDS_OutputUtility.KeyOfDisplay(toGive, null);
		if (sDS_StockKey == null || SDS_Availability.GetTradableCount(instance, sDS_StockKey) < countToGive)
		{
			SDS_Diag.ErrorOnce("交易物化失败（超维空间可用量在校验后发生变化），本笔交付已跳过", new InvalidOperationException("available changed for " + ((Def)toGive.def).defName));
			return false;
		}
		if (!SDS_WithdrawExecutor.TryWithdrawVirtual(sDS_StockKey, countToGive, out var made2, SDS_WithdrawMode.Hand, null, requireFullQuantity: true) || made2 == null || made2.Count == 0)
		{
			SDS_Diag.ErrorOnce("交易物化失败（超维空间可用量在校验后发生变化），本笔交付已跳过", new InvalidOperationException("TryWithdraw failed for " + ((Def)toGive.def).defName));
			return false;
		}
		SDS_VirtualDisplayRegistry.Forget(toGive, SDS_VirtualDisplayRegistry.TradeOwner);
		for (int j = 0; j < made2.Count; j++)
		{
			Thing val2 = made2[j];
			if (val2 != null && !val2.Destroyed)
			{
				giveToTrader(val2, val2.stackCount);
			}
		}
		return false;
	}
}
internal sealed class SDS_PreservedBucket
{
	public Thing display;

	public List<Thing> things;

	public int units;
}
internal static class SDS_PreservedBuckets
{
	public static List<SDS_PreservedBucket> Build(SDS_GameComponent_Grid grid, Predicate<Thing> include, bool tradeGrouping, ITrader trader, bool tradableBudget)
	{
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		List<SDS_PreservedBucket> list = new List<SDS_PreservedBucket>();
		if (grid == null)
		{
			return list;
		}
		List<Thing> list2 = new List<Thing>();
		grid.AppendPreservedThings(list2);
		for (int num = list2.Count - 1; num >= 0; num--)
		{
			Thing val = list2[num];
			if (val != null && !val.Destroyed)
			{
				MinifiedThing val2 = (MinifiedThing)(object)((val is MinifiedThing) ? val : null);
				if ((val2 == null || val2.InnerThing != null) && (include == null || include(val)))
				{
					continue;
				}
			}
			list2.RemoveAt(num);
		}
		if (list2.Count == 0)
		{
			return list;
		}
		List<List<Thing>> list3 = new List<List<Thing>>();
		for (int i = 0; i < list2.Count; i++)
		{
			Thing val3 = list2[i];
			if (tradeGrouping && val3 is MinifiedThing)
			{
				list3.Add(new List<Thing> { val3 });
				continue;
			}
			Thing innerIfMinified = MinifyUtility.GetInnerIfMinified(val3);
			TransferAsOneMode val4 = (TransferAsOneMode)((!tradeGrouping) ? 1 : ((trader == null || trader.TraderKind == null || !trader.TraderKind.WillTrade(innerIfMinified.def)) ? 2 : 0));
			bool flag = false;
			for (int j = 0; j < list3.Count; j++)
			{
				if ((!tradeGrouping || !(list3[j][0] is MinifiedThing)) && TransferableUtility.TransferAsOne(val3, list3[j][0], val4))
				{
					list3[j].Add(val3);
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				list3.Add(new List<Thing> { val3 });
			}
		}
		Dictionary<SDS_StockKey, long> dictionary = new Dictionary<SDS_StockKey, long>();
		for (int k = 0; k < list3.Count; k++)
		{
			List<Thing> list4 = list3[k];
			if (list4.Count == 0)
			{
				continue;
			}
			SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(list4[0]);
			if (sDS_StockKey == null)
			{
				continue;
			}
			if (!dictionary.TryGetValue(sDS_StockKey, out var value))
			{
				value = (dictionary[sDS_StockKey] = (tradableBudget ? SDS_Availability.GetTradableCount(grid, sDS_StockKey) : SDS_Availability.GetAvailableCount(grid, sDS_StockKey)));
			}
			if (value <= 0)
			{
				continue;
			}
			long num3 = 0L;
			for (int l = 0; l < list4.Count; l++)
			{
				num3 += list4[l].stackCount;
			}
			int num4 = (int)Math.Min(num3, Math.Min(value, 2147483647L));
			if (num4 > 0)
			{
				dictionary[sDS_StockKey] = value - num4;
				Thing val5 = SDS_OutputUtility.CachedPreservedBucketDisplay(list4[0]);
				if (val5 != null)
				{
					val5.stackCount = num4;
					list.Add(new SDS_PreservedBucket
					{
						display = val5,
						things = list4,
						units = num4
					});
				}
			}
		}
		return list;
	}

	public static List<ThingCount> TakePlan(SDS_GameComponent_Grid grid, List<Thing> bucket, int wantedUnits, out int plannedUnits, HashSet<Thing> excluded = null)
	{
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		plannedUnits = 0;
		List<ThingCount> list = new List<ThingCount>();
		if (grid == null || bucket == null || wantedUnits <= 0)
		{
			return list;
		}
		int num = wantedUnits;
		for (int i = 0; i < bucket.Count; i++)
		{
			if (num <= 0)
			{
				break;
			}
			Thing val = bucket[i];
			if (val != null && !val.Destroyed && grid.IsPreserved(val) && (excluded == null || !excluded.Contains(val)))
			{
				int num2 = Math.Min(num, val.stackCount);
				if (num2 > 0)
				{
					list.Add(new ThingCount(val, num2, false));
					plannedUnits += num2;
					num -= num2;
				}
			}
		}
		return list;
	}
}
[HarmonyPatch(typeof(TradeDeal), "Reset")]
internal static class Patch_SDS_TradeDealReset
{
	public static void Prefix()
	{
		try
		{
			SDS_TradeVirtual.Reset();
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("交易展示物清理失败（已忽略）", exception);
		}
	}

	public static void Postfix(TradeDeal __instance)
	{
		try
		{
			SDS_TradeVirtual.Inject(__instance);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("交易超维空间品类注入失败（已忽略，原版交易不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(TradeDeal), "TryExecute")]
internal static class Patch_SDS_TradeDealValidate
{
	public static bool Prefix(TradeDeal __instance, ref bool actuallyTraded, ref bool __result)
	{
		try
		{
			return SDS_TradeVirtual.Validate(__instance, ref actuallyTraded, ref __result);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("交易超维空间校验失败（已忽略，放行原版交易）", exception);
			return true;
		}
	}
}
[HarmonyPatch(typeof(Pawn_TraderTracker), "GiveSoldThingToTrader")]
internal static class Patch_SDS_PawnTraderGiveSold
{
	public static bool Prefix(Pawn_TraderTracker __instance, Thing toGive, int countToGive)
	{
		try
		{
			return SDS_TradeVirtual.HandoffSold(__instance, toGive, countToGive);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("商人交收物化失败（已忽略）", exception);
			return true;
		}
	}
}
[HarmonyPatch(typeof(TradeShip), "GiveSoldThingToTrader")]
internal static class Patch_SDS_TradeShipGiveSold
{
	public static bool Prefix(TradeShip __instance, Thing toGive, int countToGive)
	{
		try
		{
			return SDS_TradeVirtual.HandoffSold((ITrader)(object)__instance, toGive, countToGive);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("贸易船交收物化失败（已忽略）", exception);
			return true;
		}
	}
}
[HarmonyPatch(typeof(Dialog_Trade), "Close")]
internal static class Patch_SDS_DialogTradeClose
{
	public static void Postfix()
	{
		try
		{
			SDS_TradeVirtual.Reset();
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("交易展示物收尾清理失败（已忽略）", exception);
		}
	}
}
internal static class SDS_BeaconDisplay
{
	public static bool HasPoweredBeacon(Map map)
	{
		if (map == null)
		{
			return false;
		}
		foreach (Building_OrbitalTradeBeacon item in Building_OrbitalTradeBeacon.AllPowered(map))
		{
			if (item != null)
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsLaunchFromColony(Map map, ITrader trader)
	{
		return trader == null && map != null && HasPoweredBeacon(map) && SDS_OutputUtility.MapHasOperationalEndpoint(map);
	}

	public static IEnumerable<Thing> Append(IEnumerable<Thing> original, Map map)
	{
		if (original != null)
		{
			foreach (Thing item in original)
			{
				yield return item;
			}
		}
		foreach (Thing item2 in Displays(map))
		{
			yield return item2;
		}
	}

	private static IEnumerable<Thing> Displays(Map map)
	{
		SDS_GameComponent_Grid grid = SDS_GameComponent_Grid.Instance;
		if (grid == null || !SDS_OutputUtility.MapHasOperationalEndpoint(map))
		{
			yield break;
		}
		foreach (Thing item in SDS_TradeVirtual.VirtualDisplayStream(grid, null, null))
		{
			yield return item;
		}
		List<Thing> preservedDisplays = SDS_TradeVirtual.BuildPreservedDisplays(grid, null);
		for (int i = 0; i < preservedDisplays.Count; i++)
		{
			Thing display = preservedDisplays[i];
			if (display != null && !(display is MinifiedThing))
			{
				yield return display;
			}
		}
	}

	public static int CountLaunchableOnMap(ThingDef resDef, Map map)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		if (resDef == null || map == null)
		{
			return 0;
		}
		int num = 0;
		HashSet<Thing> hashSet = new HashSet<Thing>();
		foreach (Building_OrbitalTradeBeacon item in Building_OrbitalTradeBeacon.AllPowered(map))
		{
			if (item == null)
			{
				continue;
			}
			foreach (IntVec3 tradeableCell in item.TradeableCells)
			{
				foreach (Thing item2 in map.thingGrid.ThingsAt(tradeableCell))
				{
					if (item2 != null && !item2.Destroyed && item2.def == resDef && hashSet.Add(item2) && TradeUtility.PlayerSellableNow(item2, (ITrader)null))
					{
						num += item2.stackCount;
					}
				}
			}
		}
		return num;
	}

	public static int ConsumeFromGrid(ThingDef resDef, int count)
	{
		if (resDef == null || count <= 0)
		{
			return 0;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			return 0;
		}
		int num = count;
		int num2 = 0;
		while (num > 0)
		{
			SDS_StockKey sDS_StockKey = instance.FindWithdrawableKey(resDef);
			if (sDS_StockKey == null)
			{
				break;
			}
			long tradableCount = SDS_Availability.GetTradableCount(instance, sDS_StockKey);
			if (tradableCount <= 0)
			{
				break;
			}
			int num3 = (int)Math.Min(num, tradableCount);
			if (num3 <= 0)
			{
				break;
			}
			if (sDS_StockKey.preserved)
			{
				List<Thing> list = new List<Thing>();
				instance.AppendPreservedThings(list);
				for (int num4 = list.Count - 1; num4 >= 0; num4--)
				{
					SDS_StockKey sDS_StockKey2 = SDS_StockKey.FromPreservedThing(list[num4]);
					if (sDS_StockKey2 == null || !sDS_StockKey2.SameAs(sDS_StockKey))
					{
						list.RemoveAt(num4);
					}
				}
				int plannedUnits = 0;
				List<ThingCount> list2 = SDS_PreservedBuckets.TakePlan(instance, list, num3, out plannedUnits);
				if (plannedUnits <= 0 || list2.Count == 0 || !SDS_WithdrawExecutor.TryWithdrawPreservedExact(list2, out var made) || made == null || made.Count == 0)
				{
					break;
				}
				int num5 = 0;
				for (int i = 0; i < made.Count; i++)
				{
					Thing val = made[i];
					if (val != null && !val.Destroyed)
					{
						num5 += val.stackCount;
						val.Destroy((DestroyMode)0);
					}
				}
				if (num5 <= 0)
				{
					break;
				}
				num2 += num5;
				num -= num5;
				continue;
			}
			if (!SDS_WithdrawExecutor.TryWithdrawVirtual(sDS_StockKey, num3, out var made2) || made2 == null || made2.Count == 0)
			{
				break;
			}
			int num6 = 0;
			for (int j = 0; j < made2.Count; j++)
			{
				Thing val2 = made2[j];
				if (val2 != null && !val2.Destroyed)
				{
					num6 += val2.stackCount;
					val2.Destroy((DestroyMode)0);
				}
			}
			if (num6 <= 0)
			{
				break;
			}
			num2 += num6;
			num -= num6;
		}
		return num2;
	}
}
[HarmonyPatch(typeof(TradeUtility), "AllLaunchableThingsForTrade")]
internal static class Patch_SDS_BeaconLaunchRead
{
	public static void Postfix(Map map, ITrader trader, ref IEnumerable<Thing> __result)
	{
		try
		{
			if (SDS_BeaconDisplay.IsLaunchFromColony(map, trader))
			{
				__result = SDS_BeaconDisplay.Append(__result, map);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("通讯信标读出口注入超维空间失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(TradeUtility), "LaunchThingsOfType")]
internal static class Patch_SDS_BeaconLaunchTake
{
	public static bool Prefix(ThingDef resDef, ref int debt, Map map, TradeShip trader)
	{
		try
		{
			if (resDef == null || debt <= 0 || !SDS_BeaconDisplay.IsLaunchFromColony(map, (ITrader)(object)trader))
			{
				return true;
			}
			int num = SDS_BeaconDisplay.CountLaunchableOnMap(resDef, map);
			if (num >= debt)
			{
				return true;
			}
			int count = debt - num;
			int num2 = SDS_BeaconDisplay.ConsumeFromGrid(resDef, count);
			if (num2 <= 0)
			{
				return true;
			}
			debt -= num2;
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("通讯信标取出口扣减超维空间失败（已忽略，原版行为不受影响）", exception);
			return true;
		}
	}
}
internal static class SDS_RemoteTrade
{
	public static bool CanUseRemote(Pawn negotiator)
	{
		if (negotiator == null || negotiator.Dead || negotiator.InMentalState)
		{
			return false;
		}
		if (((Thing)negotiator).Faction != Faction.OfPlayer)
		{
			return false;
		}
		if (SDS_ImplantUtility.EffectiveChipDef(negotiator) != SDS_HediffDefOf.ChipB)
		{
			return false;
		}
		if (!SDS_ResearchUtility.CrossMapUnlocked)
		{
			return false;
		}
		return SDS_EndpointContract.IsRemoteTradeUsable(negotiator);
	}

	public static IEnumerable<Thing> AppendVirtualDisplays(IEnumerable<Thing> original, Pawn negotiator)
	{
		if (original != null)
		{
			foreach (Thing t in original)
			{
				if (t != null)
				{
					yield return t;
				}
			}
		}
		if (!CanUseRemote(negotiator))
		{
			yield break;
		}
		SDS_GameComponent_Grid grid = SDS_GameComponent_Grid.Instance;
		if (grid == null)
		{
			yield break;
		}
		ITrader trader = TradeSession.trader;
		foreach (Thing item in SDS_TradeVirtual.VirtualDisplayStream(grid, trader, SDS_TradeVirtual.RegisterFake))
		{
			yield return item;
		}
		List<Thing> preservedDisplays = SDS_TradeVirtual.BuildTradePreservedDisplays(grid, trader);
		for (int i = 0; i < preservedDisplays.Count; i++)
		{
			yield return preservedDisplays[i];
		}
	}

	public static bool HandoffSold(Thing toGive, int countToGive, Action<Thing, int> giveToTrader)
	{
		return SDS_TradeVirtual.HandoffSoldCore(toGive, countToGive, giveToTrader);
	}

	public static bool TryTakeBoughtIntoGrid(Thing toGive, int countToGive, Pawn negotiator)
	{
		if (toGive == null || toGive.Destroyed || toGive is Pawn)
		{
			return true;
		}
		if (!CanUseRemote(negotiator))
		{
			return true;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available || !instance.CanDepositThing(toGive))
		{
			return true;
		}
		int num = Math.Min(countToGive, toGive.stackCount);
		if (num <= 0)
		{
			return true;
		}
		Thing val = toGive.SplitOff(num);
		if (val == null)
		{
			return true;
		}
		val.PreTraded((TradeAction)1, negotiator, TradeSession.trader);
		if (instance.TryDeposit(val, out var accepted) && accepted > 0)
		{
			return false;
		}
		RestoreBuySplit(toGive, val);
		return true;
	}

	private static void RestoreBuySplit(Thing toGive, Thing split)
	{
		if (split != null && !split.Destroyed)
		{
			ThingOwner val = toGive?.holdingOwner;
			if (val == null || !val.TryAdd(split, false))
			{
				split.Destroy((DestroyMode)0);
			}
		}
	}
}
[HarmonyPatch(typeof(Settlement_TraderTracker), "ColonyThingsWillingToBuy")]
internal static class Patch_SDS_SettlementBuyList
{
	public static void Postfix(Pawn playerNegotiator, ref IEnumerable<Thing> __result)
	{
		try
		{
			__result = SDS_RemoteTrade.AppendVirtualDisplays(__result, playerNegotiator);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("世界聚落交易清单注入超维空间失败（已忽略，原版交易不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(Caravan_TraderTracker), "ColonyThingsWillingToBuy")]
internal static class Patch_SDS_CaravanBuyList
{
	public static void Postfix(Pawn playerNegotiator, ref IEnumerable<Thing> __result)
	{
		try
		{
			__result = SDS_RemoteTrade.AppendVirtualDisplays(__result, playerNegotiator);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("商队交易清单注入超维空间失败（已忽略，原版交易不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(Settlement_TraderTracker), "GiveSoldThingToTrader")]
internal static class Patch_SDS_SettlementGiveSold
{
	public static bool Prefix(Settlement_TraderTracker __instance, Thing toGive, int countToGive)
	{
		try
		{
			return SDS_RemoteTrade.HandoffSold(toGive, countToGive, delegate(Thing t, int c)
			{
				__instance.GiveSoldThingToTrader(t, c, TradeSession.playerNegotiator);
			});
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("世界聚落交易物化失败（已忽略）", exception);
			return true;
		}
	}
}
[HarmonyPatch(typeof(Caravan_TraderTracker), "GiveSoldThingToTrader")]
internal static class Patch_SDS_CaravanGiveSold
{
	public static bool Prefix(Caravan_TraderTracker __instance, Thing toGive, int countToGive)
	{
		try
		{
			return SDS_RemoteTrade.HandoffSold(toGive, countToGive, delegate(Thing t, int c)
			{
				__instance.GiveSoldThingToTrader(t, c, TradeSession.playerNegotiator);
			});
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("商队交易物化失败（已忽略）", exception);
			return true;
		}
	}
}
[HarmonyPatch(typeof(Settlement_TraderTracker), "GiveSoldThingToPlayer")]
internal static class Patch_SDS_SettlementGiveBought
{
	public static bool Prefix(Thing toGive, int countToGive)
	{
		try
		{
			return SDS_RemoteTrade.TryTakeBoughtIntoGrid(toGive, countToGive, TradeSession.playerNegotiator);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("世界聚落买入并入超维空间失败（已忽略，交回原版交付）", exception);
			return true;
		}
	}
}
[HarmonyPatch(typeof(Caravan_TraderTracker), "GiveSoldThingToPlayer")]
internal static class Patch_SDS_CaravanGiveBought
{
	public static bool Prefix(Thing toGive, int countToGive)
	{
		try
		{
			return SDS_RemoteTrade.TryTakeBoughtIntoGrid(toGive, countToGive, TradeSession.playerNegotiator);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("商队买入并入超维空间失败（已忽略，交回原版交付）", exception);
			return true;
		}
	}
}
public class SDS_ProtectedOutput : IExposable
{
	public Thing thing;

	public int lordLoadID;

	public int createdTick;

	public void ExposeData()
	{
		Scribe_References.Look<Thing>(ref thing, "thing", false);
		Scribe_Values.Look<int>(ref lordLoadID, "lordLoadID", 0, false);
		Scribe_Values.Look<int>(ref createdTick, "createdTick", 0, false);
	}
}
public class MapComponent_SDS_ProtectedOutputs : MapComponent
{
	public MapComponent_SDS_ProtectedOutputs(Map map)
		: base(map)
	{
	}

	public override void ExposeData()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Invalid comparison between Unknown and I4
		((MapComponent)this).ExposeData();
		List<SDS_ProtectedOutput> list = null;
		Scribe_Collections.Look<SDS_ProtectedOutput>(ref list, "sdsProtectedOutputs", (LookMode)2, Array.Empty<object>());
		if ((int)Scribe.mode == 4 && list != null && list.Count > 0)
		{
			SDS_ReclaimRegistry.AdoptLegacyProtectedOutputs(list);
		}
	}
}
internal static class SDS_Transmission
{
	public enum TransferKind
	{
		NotMirror,
		Resolved,
		Failed
	}

	public static TransferKind ResolveForOwner(Thing item, int count, out Thing real)
	{
		real = item;
		if (item == null || item.Destroyed || !SDS_MirrorRegistry.IsMirror(item))
		{
			return TransferKind.NotMirror;
		}
		if (count <= 0)
		{
			count = item.stackCount;
		}
		if (SDS_TakeService.TryTake(item, count, out var taken) && taken != null && !taken.Destroyed)
		{
			real = taken;
			return TransferKind.Resolved;
		}
		real = null;
		return TransferKind.Failed;
	}

	public static bool IsMapOwned(ThingOwner owner)
	{
		return owner != null && owner.Owner is Map;
	}

	public static bool CanReceive(ThingOwner owner, Thing item, bool canMergeWithExistingStacks)
	{
		return owner != null && item != null && owner.CanAcceptAnyOf(item, canMergeWithExistingStacks);
	}

	public static void ReturnOrDrop(Thing thing)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		if (thing != null && !thing.Destroyed && thing.stackCount > 0)
		{
			Map val = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
			SDS_ReclaimOutcome sDS_ReclaimOutcome = SDS_ReturnUtility.Return(thing, val, IntVec3.Invalid, SDS_ReturnFallback.Ground);
			if (sDS_ReclaimOutcome == SDS_ReclaimOutcome.Abandoned && val == null && !thing.Destroyed && !thing.Spawned)
			{
				SDS_Diag.ErrorOnce("容器拒收后无处回充（已保留在世界 pawn 列表之外，请检查是否丢失）：" + Gen.ToStringSafe<Thing>(thing), new InvalidOperationException("no map for rollback"));
			}
		}
	}
}
[HarmonyPatch(typeof(ThingOwner<Thing>), "TryAdd", new Type[]
{
	typeof(Thing),
	typeof(bool)
})]
[HarmonyPriority(200)]
internal static class Patch_SDS_OwnerTryAdd
{
	public static bool Prefix(ThingOwner __instance, ref Thing item, bool canMergeWithExistingStacks, out Thing __state, ref bool __result)
	{
		__state = null;
		try
		{
			if (item == null || SDS_Transmission.IsMapOwned(__instance) || !SDS_MirrorRegistry.IsMirror(item))
			{
				return true;
			}
			if (!SDS_Transmission.CanReceive(__instance, item, canMergeWithExistingStacks))
			{
				__result = false;
				return false;
			}
			Thing real;
			SDS_Transmission.TransferKind transferKind = SDS_Transmission.ResolveForOwner(item, item.stackCount, out real);
			if (transferKind == SDS_Transmission.TransferKind.Failed)
			{
				__result = false;
				return false;
			}
			__state = real;
			item = real;
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("容器接收镜像时物化失败（已拒绝本次添加）", exception);
			__result = false;
			return false;
		}
	}

	public static Exception Finalizer(Thing __state, bool __result, Exception __exception)
	{
		if (__state != null && (__exception != null || !__result))
		{
			SDS_Transmission.ReturnOrDrop(__state);
		}
		return __exception;
	}
}
[HarmonyPatch(typeof(ThingOwner), "TryAddOrTransfer", new Type[]
{
	typeof(Thing),
	typeof(bool)
})]
[HarmonyPriority(200)]
internal static class Patch_SDS_OwnerTryAddOrTransfer
{
	public static bool Prefix(ThingOwner __instance, ref Thing item, bool canMergeWithExistingStacks, out Thing __state, ref bool __result)
	{
		__state = null;
		try
		{
			if (item == null || SDS_Transmission.IsMapOwned(__instance) || !SDS_MirrorRegistry.IsMirror(item))
			{
				return true;
			}
			if (!SDS_Transmission.CanReceive(__instance, item, canMergeWithExistingStacks))
			{
				__result = false;
				return false;
			}
			Thing real;
			SDS_Transmission.TransferKind transferKind = SDS_Transmission.ResolveForOwner(item, item.stackCount, out real);
			if (transferKind == SDS_Transmission.TransferKind.Failed)
			{
				__result = false;
				return false;
			}
			__state = real;
			item = real;
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("容器转移镜像时物化失败（已拒绝本次转移）", exception);
			__result = false;
			return false;
		}
	}

	public static Exception Finalizer(Thing __state, bool __result, Exception __exception)
	{
		if (__state != null && (__exception != null || !__result))
		{
			SDS_Transmission.ReturnOrDrop(__state);
		}
		return __exception;
	}
}
public enum SDS_OutputTargetKind
{
	Core,
	Port,
	ImplantedPawn
}
public class SDS_OutputTarget
{
	public SDS_OutputTargetKind kind;

	public Thing building;

	public Pawn pawn;
}
public sealed class SDS_WithdrawPoint
{
	public Thing Endpoint;

	public bool IsSelf;

	public bool IsPawnEndpoint;

	public IntVec3 Cell;

	public LocalTargetInfo Target => (LocalTargetInfo)((Endpoint != null) ? new LocalTargetInfo(Endpoint) : LocalTargetInfo.Invalid);

	public PathEndMode EndMode => (PathEndMode)((IsSelf || IsPawnEndpoint) ? 2 : 4);

	public static SDS_WithdrawPoint Self(Pawn pawn)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null)
		{
			return null;
		}
		return new SDS_WithdrawPoint
		{
			Endpoint = (Thing)(object)pawn,
			IsSelf = true,
			Cell = ((Thing)pawn).Position
		};
	}

	public static SDS_WithdrawPoint FromHandle(SDS_EndpointHandle handle)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if (handle == null || handle.Thing == null)
		{
			return null;
		}
		return new SDS_WithdrawPoint
		{
			Endpoint = handle.Thing,
			IsSelf = handle.IsSelf,
			IsPawnEndpoint = handle.IsPawnPort,
			Cell = handle.AccessCell
		};
	}
}
public static class SDS_OutputUtility
{
	public static readonly bool PawnPortEnabled = true;

	private static readonly Dictionary<SDS_StockKey, Thing> displayCache = new Dictionary<SDS_StockKey, Thing>();

	private static readonly Dictionary<Thing, SDS_StockKey> displayKeys = new Dictionary<Thing, SDS_StockKey>();

	private static readonly FieldInfo geneSetField = AccessTools.Field(typeof(GeneSetHolderBase), "geneSet");

	private static readonly FieldInfo bladeTraitsField = AccessTools.Field(typeof(CompBladelinkWeapon), "traits");

	private static readonly FieldInfo generatedNameField = AccessTools.Field(typeof(CompGeneratedNames), "name");

	private static readonly FieldInfo stylePreceptField = AccessTools.Field(typeof(CompStyleable), "sourcePrecept");

	private static readonly Dictionary<Thing, Thing> bucketDisplayCache = new Dictionary<Thing, Thing>();

	public static SDS_WithdrawPoint FindWithdrawPoint(Pawn pawn, Map map, bool forced)
	{
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null || map == null || !((Thing)pawn).Spawned || ((Thing)pawn).Map != map)
		{
			return null;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			return null;
		}
		if (PawnPortEnabled && SDS_ImplantUtility.IsPortOperational(pawn))
		{
			return SDS_WithdrawPoint.Self(pawn);
		}
		Danger danger = (Danger)(forced ? 3 : ((int)DangerUtility.NormalMaxDanger(pawn)));
		Predicate<SDS_EndpointHandle> filter = delegate(SDS_EndpointHandle h)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			if (h.IsSelf)
			{
				return true;
			}
			IntVec3 accessCell = h.AccessCell;
			if (!((IntVec3)(ref accessCell)).IsValid || !GenGrid.InBounds(accessCell, map))
			{
				return false;
			}
			if (h.IsPawnPort)
			{
				return true;
			}
			return (pawn.playerSettings == null || pawn.playerSettings.AreaRestrictionInPawnCurrentMap == null || pawn.playerSettings.AreaRestrictionInPawnCurrentMap[accessCell]) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(h.Thing), 1, -1, (ReservationLayerDef)null, false);
		};
		SDS_EndpointQuery query = SDS_EndpointQuery.WithdrawPoint(pawn);
		query.danger = danger;
		if (SDS_EndpointResolver.TryFindBest(in query, filter, out var best, out var _))
		{
			return SDS_WithdrawPoint.FromHandle(best);
		}
		return null;
	}

	public static bool MapHasOperationalEndpoint(Map map)
	{
		SDS_EndpointRejectReason reason;
		return SDS_EndpointContract.ProbeMap(map, out reason);
	}

	public static bool MapExposesGrid(Map map)
	{
		SDS_EndpointRejectReason reason;
		return SDS_EndpointContract.ProbeMap(map, out reason);
	}

	public static bool TryFindEndpointOutputCell(Map map, IntVec3 reference, out IntVec3 cell)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		cell = IntVec3.Invalid;
		if (map == null || !MapHasOperationalEndpoint(map))
		{
			return false;
		}
		bool haveRef = GenGrid.InBounds(reference, map);
		IntVec3 best = IntVec3.Invalid;
		float bestDist = float.MaxValue;
		List<SDS_EndpointHandle> list = new List<SDS_EndpointHandle>();
		SDS_EndpointResolver.Collect(map, list);
		for (int i = 0; i < list.Count; i++)
		{
			IntVec3 accessCell = list[i].AccessCell;
			if (((IntVec3)(ref accessCell)).IsValid && GenGrid.InBounds(accessCell, map))
			{
				ConsiderOutputCell(accessCell, reference, haveRef, ref best, ref bestDist);
			}
		}
		if (best == IntVec3.Invalid)
		{
			return false;
		}
		cell = best;
		return true;
	}

	private static void ConsiderOutputCell(IntVec3 candidate, IntVec3 reference, bool haveRef, ref IntVec3 best, ref float bestDist)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if (((IntVec3)(ref candidate)).IsValid)
		{
			float num = (haveRef ? SDS_EndpointHandle.DistanceSquared(reference, candidate) : 0f);
			if (best == IntVec3.Invalid || num <= bestDist)
			{
				best = candidate;
				bestDist = num;
			}
		}
	}

	public static Thing MakeDisplayThing(SDS_StockKey key)
	{
		if (key == null || key.thingDef == null || key.preserved)
		{
			return null;
		}
		Thing val = ThingMaker.MakeThing(key.thingDef, key.stuff);
		ApplyKeyState(key, val);
		if (val != null)
		{
			displayKeys[val] = key;
		}
		return val;
	}

	public static void ApplyKeyState(SDS_StockKey key, Thing thing)
	{
		if (key == null || thing == null)
		{
			return;
		}
		if (key.qualityInt >= 0)
		{
			CompQuality val = ThingCompUtility.TryGetComp<CompQuality>(thing);
			if (val != null)
			{
				val.SetQuality((QualityCategory)(byte)key.qualityInt, (ArtGenerationContext?)null);
			}
		}
		SDS_GameComponent_Grid.Instance?.ApplyStoredVitals(key, thing);
	}

	public static bool TryGetDisplayKey(Thing display, out SDS_StockKey key)
	{
		key = null;
		return display != null && displayKeys.TryGetValue(display, out key) && key != null;
	}

	public static SDS_StockKey KeyOfDisplay(Thing display, List<Thing> preservedBucket)
	{
		if (preservedBucket != null && preservedBucket.Count > 0 && preservedBucket[0] != null)
		{
			return SDS_StockKey.FromPreservedThing(preservedBucket[0]);
		}
		if (display == null)
		{
			return null;
		}
		if (displayKeys.TryGetValue(display, out var value) && value != null)
		{
			return value;
		}
		return SDS_StockKey.FromThing(display);
	}

	public static Thing CachedDisplayThing(SDS_StockKey key)
	{
		if (key == null || key.thingDef == null)
		{
			return null;
		}
		if (displayCache.TryGetValue(key, out var value) && value != null && !value.Destroyed)
		{
			SDS_GameComponent_Grid.Instance?.ApplyStoredVitals(key, value);
			return value;
		}
		Thing val = MakeDisplayThing(key);
		if (val != null)
		{
			displayCache[key] = val;
		}
		return val;
	}

	public static Thing CachedPreservedBucketDisplay(Thing source)
	{
		if (source == null || source.Destroyed)
		{
			return null;
		}
		if (bucketDisplayCache.TryGetValue(source, out var value) && value != null && !value.Destroyed)
		{
			return value;
		}
		Thing val = MakePreservedDisplayThingFrom(source);
		if (val != null)
		{
			bucketDisplayCache[source] = val;
		}
		return val;
	}

	public static void ResetDisplayCaches()
	{
		displayCache.Clear();
		displayKeys.Clear();
		bucketDisplayCache.Clear();
	}

	public static void PruneBucketDisplays()
	{
		if (bucketDisplayCache.Count == 0)
		{
			return;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		List<Thing> list = null;
		foreach (KeyValuePair<Thing, Thing> item in bucketDisplayCache)
		{
			Thing key = item.Key;
			if (key == null || key.Destroyed || (instance != null && !instance.IsPreserved(key)))
			{
				if (list == null)
				{
					list = new List<Thing>();
				}
				list.Add(key);
			}
		}
		if (list == null)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] != null)
			{
				bucketDisplayCache.Remove(list[i]);
			}
		}
	}

	public static Thing MakePreservedDisplayThingFrom(Thing source)
	{
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || source.Destroyed || source.def == null)
		{
			return null;
		}
		if (typeof(Corpse).IsAssignableFrom(source.def.thingClass))
		{
			return null;
		}
		MinifiedThing val = (MinifiedThing)(object)((source is MinifiedThing) ? source : null);
		if (val != null)
		{
			if (val.InnerThing == null)
			{
				return null;
			}
			Thing val2 = MakeMinifiedDisplay(val);
			RegisterPreservedDisplay(val2, source);
			return val2;
		}
		Thing val3 = ThingMaker.MakeThing(source.def, source.Stuff);
		if (val3 == null)
		{
			return null;
		}
		CompQuality val4 = ThingCompUtility.TryGetComp<CompQuality>(source);
		ApplyQuality(val3, (val4 == null) ? (-1) : ((int)val4.Quality));
		if (source.def.useHitPoints && val3.def != null && val3.def.useHitPoints)
		{
			val3.HitPoints = source.HitPoints;
		}
		CopyPreservedIdentity(source, val3);
		RegisterPreservedDisplay(val3, source);
		return val3;
	}

	private static void RegisterPreservedDisplay(Thing display, Thing source)
	{
		if (display != null && source != null)
		{
			SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(source);
			if (sDS_StockKey != null && sDS_StockKey.thingDef != null)
			{
				displayKeys[display] = sDS_StockKey;
			}
		}
	}

	private static Thing MakeMinifiedDisplay(MinifiedThing source)
	{
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		Thing val = ThingMaker.MakeThing(((Thing)source).def, (ThingDef)null);
		MinifiedThing val2 = (MinifiedThing)(object)((val is MinifiedThing) ? val : null);
		if (val2 == null)
		{
			if (val != null)
			{
				val.Destroy((DestroyMode)0);
			}
			return null;
		}
		Thing innerThing = source.InnerThing;
		Thing val3 = ThingMaker.MakeThing(innerThing.def, innerThing.Stuff);
		if (val3 == null)
		{
			((Thing)val2).Destroy((DestroyMode)0);
			return null;
		}
		CompQuality val4 = ThingCompUtility.TryGetComp<CompQuality>(innerThing);
		ApplyQuality(val3, (val4 == null) ? (-1) : ((int)val4.Quality));
		if (innerThing.def != null && innerThing.def.useHitPoints && val3.def != null && val3.def.useHitPoints)
		{
			val3.HitPoints = innerThing.HitPoints;
		}
		CopyPreservedIdentity(innerThing, val3);
		val2.InnerThing = val3;
		if (val2.InnerThing == null)
		{
			((Thing)val2).Destroy((DestroyMode)0);
			val3.Destroy((DestroyMode)0);
			return null;
		}
		return (Thing)(object)val2;
	}

	private static void CopyPreservedIdentity(Thing source, Thing display)
	{
		if (source == null || display == null)
		{
			return;
		}
		GeneSetHolderBase val = (GeneSetHolderBase)(object)((source is GeneSetHolderBase) ? source : null);
		if (val != null)
		{
			GeneSetHolderBase val2 = (GeneSetHolderBase)(object)((display is GeneSetHolderBase) ? display : null);
			if (val2 != null && val.GeneSet != null && geneSetField != null)
			{
				geneSetField.SetValue(val2, val.GeneSet.Copy());
			}
		}
		CompBladelinkWeapon val3 = ThingCompUtility.TryGetComp<CompBladelinkWeapon>(source);
		CompBladelinkWeapon val4 = ThingCompUtility.TryGetComp<CompBladelinkWeapon>(display);
		if (val3 != null && val4 != null && bladeTraitsField != null)
		{
			List<WeaponTraitDef> traitsListForReading = val3.TraitsListForReading;
			bladeTraitsField.SetValue(val4, (traitsListForReading == null) ? null : new List<WeaponTraitDef>(traitsListForReading));
		}
		CompGeneratedNames val5 = ThingCompUtility.TryGetComp<CompGeneratedNames>(source);
		CompGeneratedNames val6 = ThingCompUtility.TryGetComp<CompGeneratedNames>(display);
		if (val5 != null && val6 != null && generatedNameField != null)
		{
			generatedNameField.SetValue(val6, generatedNameField.GetValue(val5));
		}
		CompStyleable val7 = ThingCompUtility.TryGetComp<CompStyleable>(source);
		CompStyleable val8 = ThingCompUtility.TryGetComp<CompStyleable>(display);
		if (val7 != null && val8 != null && stylePreceptField != null)
		{
			stylePreceptField.SetValue(val8, stylePreceptField.GetValue(val7));
		}
	}

	private static void ApplyQuality(Thing thing, int qualityInt)
	{
		if (thing != null && qualityInt >= 0)
		{
			CompQuality val = ThingCompUtility.TryGetComp<CompQuality>(thing);
			if (val != null)
			{
				val.SetQuality((QualityCategory)(byte)qualityInt, (ArtGenerationContext?)null);
			}
		}
	}

	public static bool IsValidOutputTarget(Thing thing)
	{
		if (thing == null || !thing.Spawned || thing.Map == null || thing.Destroyed)
		{
			return false;
		}
		SDS_EndpointHandle sDS_EndpointHandle = null;
		if (thing is ISDS_StorageEndpoint endpoint)
		{
			sDS_EndpointHandle = SDS_EndpointHandle.ForBuilding(endpoint);
		}
		else if (PawnPortEnabled)
		{
			Pawn val = (Pawn)(object)((thing is Pawn) ? thing : null);
			if (val != null)
			{
				sDS_EndpointHandle = SDS_EndpointHandle.ForPawn(val, self: false);
			}
		}
		SDS_EndpointRejectReason reason;
		return sDS_EndpointHandle != null && SDS_EndpointContract.IsEndpointUsable(sDS_EndpointHandle, out reason);
	}

	private static bool TryResolveTargetKind(Thing thing, out SDS_OutputTargetKind kind)
	{
		if (thing is Building_SDS_Core)
		{
			kind = SDS_OutputTargetKind.Core;
			return true;
		}
		if (thing is Building_SDS_Port)
		{
			kind = SDS_OutputTargetKind.Port;
			return true;
		}
		if (PawnPortEnabled && thing is Pawn)
		{
			kind = SDS_OutputTargetKind.ImplantedPawn;
			return true;
		}
		kind = SDS_OutputTargetKind.Core;
		return false;
	}

	public static bool TryOutputToThing(Thing thing, SDS_StockKey key, int count, out string message)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		message = null;
		if (thing == null || !TryResolveTargetKind(thing, out var kind))
		{
			message = ((object)Translator.Translate("SDS.Output.TargetGone")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		return TryOutput(MakeTarget(kind, thing), key, count, out message);
	}

	public static bool TryOutputPreservedToThing(Thing thing, SDS_StockKey key, int count, out string message)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		message = null;
		if (thing == null || !TryResolveTargetKind(thing, out var kind))
		{
			message = ((object)Translator.Translate("SDS.Output.TargetGone")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		return TryOutputPreserved(MakeTarget(kind, thing), key, count, out message);
	}

	private static SDS_OutputTarget MakeTarget(SDS_OutputTargetKind kind, Thing thing)
	{
		SDS_OutputTarget sDS_OutputTarget = new SDS_OutputTarget
		{
			kind = kind
		};
		if (kind == SDS_OutputTargetKind.ImplantedPawn)
		{
			sDS_OutputTarget.pawn = (Pawn)(object)((thing is Pawn) ? thing : null);
		}
		else
		{
			sDS_OutputTarget.building = thing;
		}
		return sDS_OutputTarget;
	}

	private static bool TryResolvePlacement(SDS_OutputTarget target, out Map map, out IntVec3 center, out string message)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		map = null;
		center = IntVec3.Invalid;
		message = null;
		if (target == null)
		{
			message = ((object)Translator.Translate("SDS.Output.NoTarget")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		if (target.kind == SDS_OutputTargetKind.ImplantedPawn)
		{
			if (!PawnPortEnabled)
			{
				message = ((object)Translator.Translate("SDS.Output.PawnReserved")/*cast due to constrained. prefix*/).ToString();
				return false;
			}
			Pawn pawn = target.pawn;
			if (pawn == null || !((Thing)pawn).Spawned || ((Thing)pawn).Map == null || pawn.Dead || !SDS_ImplantUtility.IsPortOperational(pawn))
			{
				message = ((object)Translator.Translate("SDS.Output.TargetGone")/*cast due to constrained. prefix*/).ToString();
				return false;
			}
			map = ((Thing)pawn).Map;
			center = ((Thing)pawn).Position;
			return true;
		}
		if (target.building == null || !target.building.Spawned || target.building.Map == null)
		{
			message = ((object)Translator.Translate("SDS.Output.TargetGone")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		SDS_EndpointHandle sDS_EndpointHandle = ((target.building is ISDS_StorageEndpoint endpoint) ? SDS_EndpointHandle.ForBuilding(endpoint) : null);
		if (sDS_EndpointHandle == null || !SDS_EndpointContract.IsEndpointUsable(sDS_EndpointHandle, out var _))
		{
			message = ((object)Translator.Translate("SDS.Output.TargetGone")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		map = target.building.Map;
		center = target.building.Position;
		return true;
	}

	private static int PlaceAll(List<Thing> made, Map map, IntVec3 center)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		for (int i = 0; i < made.Count; i++)
		{
			Thing val = made[i];
			if (val != null && !val.Destroyed && SDS_PlacementUtility.TryPlaceOrSpawn(val, center, map))
			{
				num += val.stackCount;
			}
		}
		return num;
	}

	private static int ClampToAvailable(SDS_GameComponent_Grid grid, SDS_StockKey key, int count)
	{
		if (grid == null || key == null)
		{
			return 0;
		}
		long availableCount = SDS_Availability.GetAvailableCount(grid, key);
		if (availableCount <= 0)
		{
			return 0;
		}
		if (count <= 0)
		{
			count = int.MaxValue;
		}
		return (int)Math.Min(count, Math.Min(availableCount, 2147483647L));
	}

	public static bool TryOutput(SDS_OutputTarget target, SDS_StockKey key, int count, out string message)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		message = null;
		if (key == null)
		{
			message = ((object)Translator.Translate("SDS.Output.NoKey")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			message = ((object)Translator.Translate("SDS.Output.NoGrid")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		if (!instance.Available)
		{
			message = ((object)Translator.Translate("SDS.Output.NoGrid")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		SDS_SupplyFailure failure = SDS_Availability.GetFailure(key);
		if (failure == SDS_SupplyFailure.UnresolvedDef)
		{
			message = ((object)TranslatorFormattedStringExtensions.Translate("SDS.Output.Unresolved", NamedArgument.op_Implicit(key.EffectiveDefName ?? "?"))/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		if (!TryResolvePlacement(target, out var map, out var center, out message))
		{
			return false;
		}
		count = ClampToAvailable(instance, key, count);
		if (count <= 0)
		{
			failure = SDS_Availability.GetFailure(key);
			message = ((failure == SDS_SupplyFailure.Forbidden) ? ((object)Translator.Translate("SDS.Output.Forbidden")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("SDS.Output.Insufficient")/*cast due to constrained. prefix*/).ToString());
			return false;
		}
		if (!SDS_WithdrawExecutor.TryWithdrawVirtual(key, count, out var made, SDS_WithdrawMode.Hand, null, requireFullQuantity: true) || made == null || made.Count == 0)
		{
			message = ((object)TranslatorFormattedStringExtensions.Translate("SDS.Output.RebuildFailed", NamedArgument.op_Implicit(key.EffectiveDefName ?? "?"))/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		int num = PlaceAll(made, map, center);
		message = ((object)TranslatorFormattedStringExtensions.Translate("SDS.Output.Done", NamedArgument.op_Implicit(key.Label), NamedArgument.op_Implicit(num))/*cast due to constrained. prefix*/).ToString();
		return num > 0;
	}

	public static bool TryOutputPreserved(SDS_OutputTarget target, SDS_StockKey key, int count, out string message)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		message = null;
		if (key == null || !key.preserved)
		{
			message = ((object)Translator.Translate("SDS.Output.NoKey")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			message = ((object)Translator.Translate("SDS.Output.NoGrid")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		if (!instance.Available)
		{
			message = ((object)Translator.Translate("SDS.Output.NoGrid")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		if (!TryResolvePlacement(target, out var map, out var center, out message))
		{
			return false;
		}
		count = ClampToAvailable(instance, key, count);
		if (count <= 0)
		{
			message = (instance.IsForbidden(key) ? ((object)Translator.Translate("SDS.Output.Forbidden")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("SDS.Output.Insufficient")/*cast due to constrained. prefix*/).ToString());
			return false;
		}
		if (!SDS_WithdrawExecutor.TryWithdrawPreserved(key, count, null, out var made) || made == null || made.Count == 0)
		{
			message = ((object)Translator.Translate("SDS.Output.Insufficient")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		int num = PlaceAll(made, map, center);
		message = ((object)TranslatorFormattedStringExtensions.Translate("SDS.Output.Done", NamedArgument.op_Implicit(key.Label), NamedArgument.op_Implicit(num))/*cast due to constrained. prefix*/).ToString();
		return num > 0;
	}

	public static bool IsValidCasketTarget(Thing thing)
	{
		return thing != null && thing.Spawned && !thing.Destroyed && thing.Map != null && thing is Building_Casket;
	}

	public static bool TryOutputPreservedToCasket(Thing casketThing, SDS_StockKey key, int count, out string message)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		message = null;
		Building_Casket val = (Building_Casket)(object)((casketThing is Building_Casket) ? casketThing : null);
		if (val == null || key == null || !key.preserved)
		{
			message = ((object)Translator.Translate("SDS.Output.NoKey")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			message = ((object)Translator.Translate("SDS.Output.NoGrid")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		if (!instance.Available)
		{
			message = ((object)Translator.Translate("SDS.Output.NoGrid")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		count = ClampToAvailable(instance, key, count);
		if (count <= 0)
		{
			message = (instance.IsForbidden(key) ? ((object)Translator.Translate("SDS.Output.Forbidden")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("SDS.Output.Insufficient")/*cast due to constrained. prefix*/).ToString());
			return false;
		}
		if (!SDS_WithdrawExecutor.TryWithdrawPreserved(key, count, null, out var made) || made == null || made.Count == 0)
		{
			message = ((object)Translator.Translate("SDS.Output.Insufficient")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		Map map = ((Thing)val).Map;
		IntVec3 position = ((Thing)val).Position;
		int num = 0;
		for (int i = 0; i < made.Count; i++)
		{
			Thing val2 = made[i];
			if (val2 != null && !val2.Destroyed)
			{
				if (val.TryAcceptThing(val2, true))
				{
					num += val2.stackCount;
				}
				else if (SDS_PlacementUtility.TryPlaceOrSpawn(val2, position, map))
				{
					num += val2.stackCount;
				}
			}
		}
		message = ((object)TranslatorFormattedStringExtensions.Translate("SDS.Output.Done", NamedArgument.op_Implicit(key.Label), NamedArgument.op_Implicit(num))/*cast due to constrained. prefix*/).ToString();
		return num > 0;
	}

	public static bool TryOutputPreservedInstall(SDS_StockKey key, Map map, IntVec3 cell, out string message)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_0297: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		message = null;
		if (key == null || !key.preserved || key.thingDef == null || !key.thingDef.Minifiable || ((BuildableDef)key.thingDef).installBlueprintDef == null)
		{
			message = ((object)Translator.Translate("SDS.Output.NoKey")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			message = ((object)Translator.Translate("SDS.Output.NoGrid")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		if (!instance.Available)
		{
			message = ((object)Translator.Translate("SDS.Output.NoGrid")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		if (map == null || !((IntVec3)(ref cell)).IsValid || !GenGrid.InBounds(cell, map))
		{
			message = ((object)Translator.Translate("SDS.Output.TargetGone")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		if (!AcceptanceReport.op_Implicit(GenConstruct.CanPlaceBlueprintAt((BuildableDef)(object)key.thingDef, cell, Rot4.North, map, false, (Thing)null, (Thing)null, key.stuff, false, false, false)))
		{
			message = ((object)Translator.Translate("SDS.Output.CannotPlace")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		if (SDS_Availability.GetAvailableCount(instance, key) <= 0)
		{
			message = ((object)Translator.Translate("SDS.Output.Insufficient")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		if (!SDS_WithdrawExecutor.TryWithdrawPreserved(key, 1, null, out var made) || made == null || made.Count == 0)
		{
			message = ((object)Translator.Translate("SDS.Output.Insufficient")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		Thing val = made[0];
		for (int i = 1; i < made.Count; i++)
		{
			SDS_ReturnUtility.ReturnPreserved(made[i], map, cell);
		}
		MinifiedThing val2 = (MinifiedThing)(object)((val is MinifiedThing) ? val : null);
		if (val2 == null)
		{
			SDS_ReturnUtility.ReturnPreserved(val, map, cell);
			message = ((object)Translator.Translate("SDS.Output.NoKey")/*cast due to constrained. prefix*/).ToString();
			return false;
		}
		if (SDS_PreservedUsage.TryPlaceAtAccessPoint((Thing)(object)val2, map, cell, out var placed))
		{
			MinifiedThing val3 = (MinifiedThing)(object)((placed is MinifiedThing) ? placed : null);
			if (val3 != null)
			{
				if (!SDS_InstallBlueprintGuard.TryReplaceBlueprintWithRollback(val3, cell, map))
				{
					message = ((object)Translator.Translate("SDS.Output.InstallFailed")/*cast due to constrained. prefix*/).ToString();
					return false;
				}
				message = ((object)TranslatorFormattedStringExtensions.Translate("SDS.Output.InstallStarted", NamedArgument.op_Implicit(key.Label))/*cast due to constrained. prefix*/).ToString();
				return true;
			}
		}
		message = ((object)Translator.Translate("SDS.Output.NoTarget")/*cast due to constrained. prefix*/).ToString();
		return false;
	}
}
internal static class SDS_OutputTargeting
{
	private enum Mode
	{
		Endpoint,
		Casket,
		Install
	}

	private static SDS_StockKey pendingKey;

	private static SDS_StockKey pendingPreservedKey;

	private static int pendingCount;

	private static Mode pendingMode;

	public static void Begin(SDS_StockKey key, int count)
	{
		if (key != null)
		{
			pendingKey = key;
			pendingPreservedKey = null;
			pendingCount = ((count <= 0) ? int.MaxValue : count);
			pendingMode = Mode.Endpoint;
			BeginTargeting();
		}
	}

	public static void BeginPreserved(SDS_StockKey key, int count)
	{
		if (key != null && key.preserved)
		{
			pendingKey = null;
			pendingPreservedKey = key;
			pendingCount = ((count <= 0) ? int.MaxValue : count);
			pendingMode = ResolveMode(key);
			BeginTargeting();
		}
	}

	private static Mode ResolveMode(SDS_StockKey key)
	{
		ThingDef val = key?.thingDef;
		if (val == null)
		{
			return Mode.Endpoint;
		}
		if (val.Minifiable && ((BuildableDef)val).installBlueprintDef != null)
		{
			return Mode.Install;
		}
		if (typeof(Corpse).IsAssignableFrom(val.thingClass))
		{
			return Mode.Casket;
		}
		return Mode.Endpoint;
	}

	private static void BeginTargeting()
	{
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Expected O, but got Unknown
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Expected O, but got Unknown
		Find.WindowStack.TryRemove(typeof(Window_SDS_OutputDialog), false);
		Find.WindowStack.TryRemove(typeof(Window_SuperSpace), false);
		if (pendingMode == Mode.Install)
		{
			TargetingParameters val = new TargetingParameters
			{
				canTargetLocations = true,
				canTargetSelf = false,
				canTargetPawns = false,
				canTargetItems = false,
				canTargetFires = false,
				canTargetPlants = false,
				canTargetBuildings = false,
				mustBeSelectable = false,
				validator = delegate(TargetInfo t)
				{
					//IL_0003: Unknown result type (might be due to invalid IL or missing references)
					//IL_0008: Unknown result type (might be due to invalid IL or missing references)
					IntVec3 cell = ((TargetInfo)(ref t)).Cell;
					return ((IntVec3)(ref cell)).IsValid && !((TargetInfo)(ref t)).HasThing;
				}
			};
			Find.Targeter.BeginTargeting(val, (Action<LocalTargetInfo>)OnTargetChosen, (Pawn)null, (Action)null, (Texture2D)null, true);
		}
		else
		{
			bool casketMode = pendingMode == Mode.Casket;
			TargetingParameters val2 = new TargetingParameters
			{
				canTargetLocations = false,
				canTargetSelf = true,
				canTargetPawns = true,
				canTargetItems = false,
				canTargetFires = false,
				canTargetPlants = false,
				canTargetBuildings = true,
				mustBeSelectable = false,
				validator = (TargetInfo t) => SDS_OutputUtility.IsValidOutputTarget(((TargetInfo)(ref t)).Thing) || (casketMode && SDS_OutputUtility.IsValidCasketTarget(((TargetInfo)(ref t)).Thing))
			};
			Find.Targeter.BeginTargeting(val2, (Action<LocalTargetInfo>)OnTargetChosen, (Pawn)null, (Action)null, (Texture2D)null, true);
		}
	}

	private static void OnTargetChosen(LocalTargetInfo target)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		SDS_StockKey sDS_StockKey = pendingKey;
		SDS_StockKey sDS_StockKey2 = pendingPreservedKey;
		int count = pendingCount;
		Mode mode = pendingMode;
		pendingKey = null;
		pendingPreservedKey = null;
		pendingCount = 0;
		pendingMode = Mode.Endpoint;
		bool flag;
		string message;
		if (mode == Mode.Install && sDS_StockKey2 != null)
		{
			flag = SDS_OutputUtility.TryOutputPreservedInstall(sDS_StockKey2, Find.CurrentMap, ((LocalTargetInfo)(ref target)).Cell, out message);
		}
		else
		{
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing == null)
			{
				return;
			}
			if (sDS_StockKey2 != null)
			{
				flag = ((mode == Mode.Casket && SDS_OutputUtility.IsValidCasketTarget(thing)) ? SDS_OutputUtility.TryOutputPreservedToCasket(thing, sDS_StockKey2, count, out message) : SDS_OutputUtility.TryOutputPreservedToThing(thing, sDS_StockKey2, count, out message));
			}
			else
			{
				if (sDS_StockKey == null)
				{
					return;
				}
				flag = SDS_OutputUtility.TryOutputToThing(thing, sDS_StockKey, count, out message);
			}
		}
		Messages.Message(message, flag ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, false);
	}
}
internal static class SDS_UseTargeting
{
	private static SDS_StockKey pendingKey;

	private static bool pendingPreserved;

	public static void Begin(SDS_StockKey key, bool preserved)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Expected O, but got Unknown
		if (key != null)
		{
			pendingKey = key;
			pendingPreserved = preserved;
			Find.WindowStack.TryRemove(typeof(Window_SDS_OutputDialog), false);
			Find.WindowStack.TryRemove(typeof(Window_SuperSpace), false);
			TargetingParameters val = new TargetingParameters
			{
				canTargetLocations = true,
				canTargetSelf = false,
				canTargetPawns = true,
				canTargetBuildings = true,
				canTargetItems = true,
				canTargetFires = false,
				canTargetPlants = false,
				mapObjectTargetsMustBeAutoAttackable = false,
				mustBeSelectable = false,
				validator = (TargetInfo t) => true
			};
			Find.Targeter.BeginTargeting(val, (Action<LocalTargetInfo>)OnTargetChosen, (Pawn)null, (Action)null, (Texture2D)null, true);
		}
	}

	private static void OnTargetChosen(LocalTargetInfo target)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		SDS_StockKey sDS_StockKey = pendingKey;
		bool preserved = pendingPreserved;
		pendingKey = null;
		pendingPreserved = false;
		if (sDS_StockKey != null)
		{
			SDS_UseCommand.HandleTarget(target, sDS_StockKey, preserved);
		}
	}
}
public class SDS_SidebarUIState : IExposable
{
	public List<string> collapsed = new List<string>();

	public Vector2 scroll = Vector2.zero;

	public bool hasSelection;

	public SDS_StockKey selectedKey;

	public bool selectedPreserved;

	public void ExposeData()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Invalid comparison between Unknown and I4
		Scribe_Collections.Look<string>(ref collapsed, "collapsed", (LookMode)1, Array.Empty<object>());
		Scribe_Values.Look<Vector2>(ref scroll, "scroll", Vector2.zero, false);
		Scribe_Values.Look<bool>(ref hasSelection, "hasSelection", false, false);
		Scribe_Deep.Look<SDS_StockKey>(ref selectedKey, "selectedKey", Array.Empty<object>());
		Scribe_Values.Look<bool>(ref selectedPreserved, "selectedPreserved", false, false);
		if ((int)Scribe.mode == 4)
		{
			Normalize();
		}
	}

	public void Normalize()
	{
		if (collapsed == null)
		{
			collapsed = new List<string>();
		}
		collapsed.RemoveAll(string.IsNullOrEmpty);
		if (scroll.x < 0f)
		{
			scroll.x = 0f;
		}
		if (scroll.y < 0f)
		{
			scroll.y = 0f;
		}
		if (selectedKey != null)
		{
			selectedKey.TryResolve();
		}
		if (selectedKey == null || !selectedKey.HasIdentity)
		{
			selectedKey = null;
			hasSelection = false;
			selectedPreserved = false;
		}
	}

	public void NormalizeAgainst(SDS_GameComponent_Grid grid)
	{
		if (grid == null || selectedKey == null)
		{
			return;
		}
		if (!grid.TryNormalizeRowKey(selectedKey, out var realKey) || realKey == null)
		{
			selectedKey = null;
			hasSelection = false;
			selectedPreserved = false;
			return;
		}
		if (!realKey.SameAs(selectedKey))
		{
			selectedKey = realKey;
		}
		hasSelection = true;
		selectedPreserved = selectedKey.preserved;
	}
}
public class Window_SuperSpace : Window
{
	private sealed class SDS_CatNode
	{
		public string key;

		public string label;

		public long total;

		public readonly List<SDS_CatNode> children = new List<SDS_CatNode>();

		public readonly List<SDS_StockEntry> entries = new List<SDS_StockEntry>();

		public SDS_CatNode ChildFor(string childKey, string childLabel)
		{
			for (int i = 0; i < children.Count; i++)
			{
				if (children[i].key == childKey)
				{
					return children[i];
				}
			}
			SDS_CatNode sDS_CatNode = new SDS_CatNode
			{
				key = childKey,
				label = childLabel
			};
			children.Add(sDS_CatNode);
			return sDS_CatNode;
		}
	}

	private sealed class SDS_StockRow
	{
		public SDS_CatNode node;

		public SDS_StockEntry entry;

		public int depth;

		public string pathLabel;

		public float height => (node != null) ? 24f : 28f;
	}

	private static readonly Color SectionColor = new Color(0.17f, 0.17f, 0.17f);

	private static readonly Color TextColor = new Color(0.55f, 0.75f, 1f);

	private static readonly Color SubTextColor = new Color(0.42f, 0.58f, 0.78f);

	private static readonly Color ForbiddenTextColor = new Color(0.42f, 0.42f, 0.42f);

	private const float HeaderTitleH = 30f;

	private const float StatusH = 20f;

	private const float SearchY = 52f;

	private const float SearchH = 24f;

	private const float ListY = 80f;

	private const float FooterH = 76f;

	private const float RowHeight = 28f;

	private const float GroupHeaderHeight = 24f;

	private const float RowIndent = 8f;

	private const float SidebarTopMargin = 150f;

	private const float SidebarBottomMargin = 40f;

	private readonly QuickSearchWidget quickSearch = new QuickSearchWidget();

	private Vector2 stockScroll = Vector2.zero;

	private readonly HashSet<string> collapsedNodes = new HashSet<string>();

	private SDS_StockKey selectedKey;

	private bool selectedPreserved;

	private List<SDS_StockEntry> selectedMembers;

	private readonly Dictionary<SDS_StockEntry, List<SDS_StockEntry>> aggMembers = new Dictionary<SDS_StockEntry, List<SDS_StockEntry>>();

	private string message;

	private List<SDS_StockEntry> cachedEntries;

	private List<SDS_StockRow> cachedRows;

	private int cachedStockVersion = int.MinValue;

	private string cachedQuery;

	private bool cachedSeparateQuality;

	private bool cachedSeparateStuff;

	private bool rowsDirty = true;

	private static float SidebarWidth => Mathf.Clamp(Mathf.RoundToInt((float)UI.screenWidth * 0.28f), 320, 420);

	private static float SidebarHeight => Mathf.Max(320f, (float)UI.screenHeight - 150f - 40f);

	public override Vector2 InitialSize => new Vector2(SidebarWidth, SidebarHeight);

	protected override float Margin => 8f;

	private static SDS_SidebarUIState UIState => SDS_GameComponent_Grid.Instance?.SidebarUI;

	public override QuickSearchWidget CommonSearchWidget => quickSearch;

	internal bool HasSelectedRow => selectedKey != null;

	public Window_SuperSpace()
		: base((IWindowDrawing)null)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		base.doCloseX = true;
		base.draggable = false;
		base.resizeable = false;
		base.absorbInputAroundWindow = false;
		base.closeOnCancel = true;
		base.closeOnAccept = false;
		base.closeOnClickedOutside = false;
		base.preventCameraMotion = false;
		base.layer = (WindowLayer)0;
		LoadUIState();
	}

	private void LoadUIState()
	{
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		SDS_SidebarUIState uIState = UIState;
		if (uIState == null)
		{
			return;
		}
		uIState.Normalize();
		uIState.NormalizeAgainst(SDS_GameComponent_Grid.Instance);
		collapsedNodes.Clear();
		for (int i = 0; i < uIState.collapsed.Count; i++)
		{
			string text = uIState.collapsed[i];
			if (!string.IsNullOrEmpty(text))
			{
				collapsedNodes.Add(text);
			}
		}
		stockScroll = uIState.scroll;
		selectedKey = (uIState.hasSelection ? uIState.selectedKey : null);
		selectedPreserved = selectedKey != null && uIState.selectedPreserved;
	}

	private static void PersistCollapsed(HashSet<string> nodes)
	{
		SDS_SidebarUIState uIState = UIState;
		if (uIState == null)
		{
			return;
		}
		uIState.collapsed.Clear();
		foreach (string node in nodes)
		{
			uIState.collapsed.Add(node);
		}
	}

	private void PersistScroll()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		SDS_SidebarUIState uIState = UIState;
		if (uIState != null)
		{
			uIState.scroll = stockScroll;
		}
	}

	private void PersistSelection()
	{
		SDS_SidebarUIState uIState = UIState;
		if (uIState != null)
		{
			uIState.hasSelection = selectedKey != null;
			uIState.selectedKey = selectedKey;
			uIState.selectedPreserved = selectedKey != null && selectedPreserved;
		}
	}

	protected override void SetInitialSizeAndPosition()
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		float sidebarWidth = SidebarWidth;
		float sidebarHeight = SidebarHeight;
		float num = Mathf.Max(0f, Mathf.Min(150f, (float)UI.screenHeight - sidebarHeight));
		base.windowRect = GenUI.Rounded(new Rect((float)UI.screenWidth - sidebarWidth, num, sidebarWidth, sidebarHeight));
	}

	protected override Rect QuickSearchWidgetRect(Rect winRect, Rect inRect)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		return new Rect(((Rect)(ref inRect)).x, ((Rect)(ref inRect)).y + 52f, ((Rect)(ref inRect)).width, 24f);
	}

	public override void Notify_CommonSearchChanged()
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		rowsDirty = true;
		stockScroll = Vector2.zero;
		PersistScroll();
	}

	public override void OnCancelKeyPressed()
	{
		if (!SDS_Hotkeys.InBuildMode)
		{
			((Window)this).OnCancelKeyPressed();
		}
	}

	internal bool TryOpenOutputFromHotkey()
	{
		if (selectedKey == null)
		{
			return false;
		}
		OpenOutputDialog();
		return true;
	}

	internal bool TryOpenUseFromHotkey()
	{
		if (selectedKey == null)
		{
			return false;
		}
		OpenUseDialog();
		return true;
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		Rect titleRect = default(Rect);
		((Rect)(ref titleRect))..ctor(((Rect)(ref inRect)).x, ((Rect)(ref inRect)).y, Mathf.Max(0f, ((Rect)(ref inRect)).width - 30f), 30f);
		Rect statusRect = default(Rect);
		((Rect)(ref statusRect))..ctor(((Rect)(ref inRect)).x, ((Rect)(ref inRect)).y + 30f, ((Rect)(ref inRect)).width, 20f);
		float num = ((Rect)(ref inRect)).y + 80f;
		float num2 = ((Rect)(ref inRect)).yMax - 76f;
		Rect rect = default(Rect);
		((Rect)(ref rect))..ctor(((Rect)(ref inRect)).x, num, ((Rect)(ref inRect)).width, Mathf.Max(40f, num2 - num - 4f));
		float num3 = Mathf.Max(((Rect)(ref rect)).yMax + 4f, num2);
		Rect rect2 = default(Rect);
		((Rect)(ref rect2))..ctor(((Rect)(ref inRect)).x, num3, ((Rect)(ref inRect)).width, Mathf.Max(0f, ((Rect)(ref inRect)).yMax - num3));
		DrawHeader(titleRect, statusRect);
		DrawStockList(rect);
		DrawFooter(rect2);
	}

	private void DrawHeader(Rect titleRect, Rect statusRect)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		Text.Font = (GameFont)2;
		Text.Anchor = (TextAnchor)3;
		ColoredLabel(titleRect, ((object)Translator.Translate("SDS.Window.BigTitle")/*cast due to constrained. prefix*/).ToString());
		Text.Font = (GameFont)1;
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		string text = ((instance == null || !instance.Available) ? ((object)Translator.Translate("SDS.Window.GridStatusOff")/*cast due to constrained. prefix*/).ToString() : ((object)TranslatorFormattedStringExtensions.Translate("SDS.Window.GridStatusOn", NamedArgument.op_Implicit(instance.UsedCapacity), NamedArgument.op_Implicit(instance.TypeCapacity))/*cast due to constrained. prefix*/).ToString());
		ColoredLabel(statusRect, text, SubTextColor);
		Text.Anchor = anchor;
		Text.Font = font;
	}

	private void DrawFooter(Rect rect)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02be: Unknown result type (might be due to invalid IL or missing references)
		//IL_0297: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0325: Unknown result type (might be due to invalid IL or missing references)
		//IL_032a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0398: Unknown result type (might be due to invalid IL or missing references)
		//IL_039c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0381: Unknown result type (might be due to invalid IL or missing references)
		//IL_0386: Unknown result type (might be due to invalid IL or missing references)
		//IL_0362: Unknown result type (might be due to invalid IL or missing references)
		//IL_0367: Unknown result type (might be due to invalid IL or missing references)
		if (!(((Rect)(ref rect)).height <= 0f))
		{
			GameFont font = Text.Font;
			TextAnchor anchor = Text.Anchor;
			Text.Font = (GameFont)1;
			Text.Anchor = (TextAnchor)3;
			string text = ((!GenText.NullOrEmpty(message)) ? message : SDS_Hotkeys.HotkeyHintText());
			ColoredLabel(new Rect(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, ((Rect)(ref rect)).width, 18f), text, SubTextColor);
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			float num = 0f;
			string text2 = ((object)Translator.Translate("SDS.Window.GridStatusOff")/*cast due to constrained. prefix*/).ToString();
			if (instance != null && instance.TypeCapacity > 0)
			{
				num = Mathf.Clamp01((float)instance.UsedCapacity / (float)instance.TypeCapacity);
				text2 = ((object)TranslatorFormattedStringExtensions.Translate("SDS.Window.CapacityBar", NamedArgument.op_Implicit(instance.UsedCapacity), NamedArgument.op_Implicit(instance.TypeCapacity))/*cast due to constrained. prefix*/).ToString();
			}
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(((Rect)(ref rect)).x, ((Rect)(ref rect)).y + 20f, ((Rect)(ref rect)).width, 18f);
			Widgets.FillableBar(val, num);
			Text.Anchor = (TextAnchor)4;
			ColoredLabel(val, text2, Color.white);
			float num2 = Mathf.Clamp(((Rect)(ref rect)).width * 0.28f, 64f, 110f);
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(((Rect)(ref rect)).x, ((Rect)(ref rect)).y + 42f, Mathf.Max(40f, ((Rect)(ref rect)).width - num2 - 6f), 30f);
			Rect val3 = default(Rect);
			((Rect)(ref val3))..ctor(((Rect)(ref val2)).xMax + 6f, ((Rect)(ref rect)).y + 42f, num2, 30f);
			string text3 = ((selectedKey != null) ? ((object)TranslatorFormattedStringExtensions.Translate("SDS.Window.OutputButtonSelected", NamedArgument.op_Implicit(selectedKey.Label))/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("SDS.Window.OutputButton")/*cast due to constrained. prefix*/).ToString());
			if (Widgets.ButtonText(val2, GenText.Truncate(text3, ((Rect)(ref val2)).width - 8f, (Dictionary<string, string>)null), true, true, true, (TextAnchor?)null))
			{
				OpenOutputDialog();
			}
			bool flag = selectedKey != null && RowHasAvailable(instance, selectedKey, selectedMembers);
			Color color = GUI.color;
			if (!flag)
			{
				GUI.color = new Color(1f, 1f, 1f, 0.45f);
			}
			string text4 = ((object)Translator.Translate("SDS.Window.UseButton")/*cast due to constrained. prefix*/).ToString();
			if (Widgets.ButtonText(val3, GenText.Truncate(text4, ((Rect)(ref val3)).width - 8f, (Dictionary<string, string>)null), true, true, true, (TextAnchor?)null) && flag)
			{
				OpenUseDialog();
			}
			GUI.color = color;
			if (!flag)
			{
				string text5 = ((selectedKey == null) ? ((object)Translator.Translate("SDS.Window.OutputNoKey")/*cast due to constrained. prefix*/).ToString() : ((instance == null || !IsRowForbidden(instance, selectedKey, selectedMembers)) ? ((object)Translator.Translate("SDS.Use.NoStock")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("SDS.Forbidden.CannotUse")/*cast due to constrained. prefix*/).ToString()));
				TooltipHandler.TipRegion(val3, TipSignal.op_Implicit(text5));
			}
			Text.Anchor = anchor;
			Text.Font = font;
		}
	}

	private void OpenOutputDialog()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		message = null;
		if (selectedKey == null)
		{
			message = ((object)Translator.Translate("SDS.Window.OutputNoKey")/*cast due to constrained. prefix*/).ToString();
			return;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			message = ((object)Translator.Translate("SDS.Output.NoGrid")/*cast due to constrained. prefix*/).ToString();
		}
		else if (Find.WindowStack.WindowOfType<Window_SDS_OutputDialog>() == null)
		{
			Find.WindowStack.Add((Window)(object)new Window_SDS_OutputDialog(selectedKey, selectedPreserved, selectedMembers));
		}
	}

	private void OpenUseDialog()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		message = null;
		if (selectedKey == null)
		{
			message = ((object)Translator.Translate("SDS.Window.OutputNoKey")/*cast due to constrained. prefix*/).ToString();
			return;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			message = ((object)Translator.Translate("SDS.Output.NoGrid")/*cast due to constrained. prefix*/).ToString();
		}
		else if (selectedMembers != null && selectedMembers.Count > 1)
		{
			if (Find.WindowStack.WindowOfType<Window_SDS_OutputDialog>() == null)
			{
				Find.WindowStack.Add((Window)(object)new Window_SDS_OutputDialog(selectedKey, selectedPreserved, selectedMembers, Window_SDS_OutputDialog.DialogMode.Use));
			}
		}
		else
		{
			SDS_UseTargeting.Begin(selectedKey, selectedPreserved);
		}
	}

	private void DrawStockList(Rect rect)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_0291: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		//IL_035c: Unknown result type (might be due to invalid IL or missing references)
		Widgets.DrawBoxSolid(rect, SectionColor);
		Rect val = GenUI.ContractedBy(rect, 6f);
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			DrawCentered(val, ((object)Translator.Translate("SDS.Window.NoGrid")/*cast due to constrained. prefix*/).ToString());
			return;
		}
		List<SDS_StockEntry> list = instance.UnresolvedSnapshot();
		if (list.Count > 0)
		{
			float num = 24f + (float)Mathf.Min(list.Count, 3) * 18f + 4f;
			num = Mathf.Min(num, ((Rect)(ref val)).height * 0.45f);
			Rect rect2 = default(Rect);
			((Rect)(ref rect2))..ctor(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width, num);
			DrawUnresolvedSection(rect2, list);
			float num2 = ((Rect)(ref rect2)).height + 4f;
			((Rect)(ref val)).y = ((Rect)(ref val)).y + num2;
			((Rect)(ref val)).height = ((Rect)(ref val)).height - num2;
			if (((Rect)(ref val)).height < 40f)
			{
				return;
			}
		}
		string text = ((quickSearch.filter == null) ? null : quickSearch.filter.Text)?.Trim();
		if (rowsDirty || cachedRows == null || instance.StockVersion != cachedStockVersion || !string.Equals(text, cachedQuery, StringComparison.Ordinal) || SDS_Config.SeparateByQuality != cachedSeparateQuality || SDS_Config.SeparateByStuff != cachedSeparateStuff)
		{
			RebuildRows(instance, text);
		}
		List<SDS_StockRow> list2 = cachedRows;
		if (cachedEntries == null || cachedEntries.Count == 0)
		{
			DrawCentered(val, ((object)Translator.Translate("SDS.Window.StockEmpty")/*cast due to constrained. prefix*/).ToString());
			return;
		}
		if (list2 == null || list2.Count == 0)
		{
			DrawCentered(val, ((object)Translator.Translate("SDS.Window.SearchNoMatch")/*cast due to constrained. prefix*/).ToString());
			return;
		}
		float num3 = 0f;
		for (int i = 0; i < list2.Count; i++)
		{
			num3 += list2[i].height;
		}
		Rect val2 = default(Rect);
		((Rect)(ref val2))..ctor(0f, 0f, ((Rect)(ref val)).width - 20f, Mathf.Max(num3, ((Rect)(ref val)).height));
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		bool flag = false;
		try
		{
			Text.Font = (GameFont)1;
			Text.Anchor = (TextAnchor)3;
			Widgets.BeginScrollView(val, ref stockScroll, val2, true);
			flag = true;
			float num4 = Mathf.Max(0f, stockScroll.y);
			float num5 = num4 + ((Rect)(ref val)).height;
			float num6 = 0f;
			Rect val3 = default(Rect);
			for (int j = 0; j < list2.Count; j++)
			{
				SDS_StockRow sDS_StockRow = list2[j];
				float height = sDS_StockRow.height;
				if (num6 + height < num4)
				{
					num6 += height;
					continue;
				}
				if (num6 > num5)
				{
					break;
				}
				((Rect)(ref val3))..ctor(0f, num6, ((Rect)(ref val2)).width, height);
				if (sDS_StockRow.node != null)
				{
					DrawCategoryRow(val3, sDS_StockRow);
				}
				else
				{
					DrawStockRow(val3, sDS_StockRow.entry, sDS_StockRow.depth, sDS_StockRow.pathLabel);
				}
				num6 += height;
			}
		}
		finally
		{
			if (flag)
			{
				Widgets.EndScrollView();
			}
			Text.Anchor = anchor;
			Text.Font = font;
		}
		PersistScroll();
	}

	private void RebuildRows(SDS_GameComponent_Grid grid, string query)
	{
		List<SDS_StockEntry> list = grid.Snapshot(0);
		list.AddRange(grid.PreservedSnapshot());
		list = (cachedEntries = BuildDisplayEntries(list));
		cachedRows = ((list.Count == 0) ? new List<SDS_StockRow>() : (GenText.NullOrEmpty(query) ? BuildTreeRows(list) : BuildSearchRows(list, query)));
		cachedStockVersion = grid.StockVersion;
		cachedQuery = query;
		cachedSeparateQuality = SDS_Config.SeparateByQuality;
		cachedSeparateStuff = SDS_Config.SeparateByStuff;
		rowsDirty = false;
		RebuildSelectedMembers();
	}

	private void RebuildSelectedMembers()
	{
		selectedMembers = null;
		if (selectedKey == null || cachedEntries == null)
		{
			return;
		}
		for (int i = 0; i < cachedEntries.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = cachedEntries[i];
			if (sDS_StockEntry == null || sDS_StockEntry.key == null || sDS_StockEntry.preserved != selectedPreserved)
			{
				continue;
			}
			aggMembers.TryGetValue(sDS_StockEntry, out var value);
			bool flag = sDS_StockEntry.key.SameAs(selectedKey);
			if (!flag && value != null)
			{
				for (int j = 0; j < value.Count; j++)
				{
					SDS_StockEntry sDS_StockEntry2 = value[j];
					if (sDS_StockEntry2 != null && sDS_StockEntry2.key != null && sDS_StockEntry2.key.SameAs(selectedKey))
					{
						flag = true;
						break;
					}
				}
			}
			if (flag)
			{
				selectedMembers = value;
				if (!sDS_StockEntry.key.SameAs(selectedKey))
				{
					selectedKey = sDS_StockEntry.key;
					PersistSelection();
				}
				return;
			}
		}
		selectedKey = null;
		selectedPreserved = false;
		selectedMembers = null;
		PersistSelection();
	}

	private static void DrawUnresolvedSection(Rect rect, List<SDS_StockEntry> entries)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		try
		{
			Widgets.DrawBoxSolid(rect, new Color(0.3f, 0.1f, 0.1f, 0.45f));
			Rect val = GenUI.ContractedBy(rect, 4f);
			((Rect)(ref val)).height = 18f;
			Text.Font = (GameFont)0;
			Text.Anchor = (TextAnchor)3;
			Widgets.Label(val, ((object)TranslatorFormattedStringExtensions.Translate("SDS.Window.UnresolvedHeader", NamedArgument.op_Implicit(entries.Count))/*cast due to constrained. prefix*/).ToString());
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(((Rect)(ref val)).xMax - 84f, ((Rect)(ref val)).y, 84f, 18f);
			if (Widgets.ButtonText(val2, ((object)Translator.Translate("SDS.Window.RetryResolve")/*cast due to constrained. prefix*/).ToString(), true, true, true, (TextAnchor?)null))
			{
				SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
				if (instance != null)
				{
					int num = instance.RetryResolveUnresolved();
					Messages.Message(((object)TranslatorFormattedStringExtensions.Translate("SDS.Window.RetryResolveDone", NamedArgument.op_Implicit(num))/*cast due to constrained. prefix*/).ToString(), MessageTypeDefOf.NeutralEvent, false);
				}
			}
			((Rect)(ref val)).y = ((Rect)(ref val)).y + 18f;
			int num2 = Mathf.Min(entries.Count, Mathf.FloorToInt((((Rect)(ref rect)).height - 26f) / 18f));
			for (int i = 0; i < num2; i++)
			{
				SDS_StockEntry sDS_StockEntry = entries[i];
				string text = sDS_StockEntry?.key?.EffectiveDefName ?? "?";
				Widgets.Label(val, text + " ×" + (sDS_StockEntry?.count ?? 0));
				((Rect)(ref val)).y = ((Rect)(ref val)).y + 18f;
			}
		}
		finally
		{
			Text.Anchor = anchor;
			Text.Font = font;
		}
	}

	private static void DrawCentered(Rect rect, string text)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		Text.Font = (GameFont)1;
		Text.Anchor = (TextAnchor)4;
		ColoredLabel(rect, text, SubTextColor);
		Text.Anchor = anchor;
		Text.Font = font;
	}

	private void DrawCategoryRow(Rect rect, SDS_StockRow row)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		SDS_CatNode node = row.node;
		bool flag = collapsedNodes.Contains(node.key);
		float num = (float)row.depth * 8f;
		Rect val = default(Rect);
		((Rect)(ref val))..ctor(((Rect)(ref rect)).x + num, ((Rect)(ref rect)).y, ((Rect)(ref rect)).width - num, ((Rect)(ref rect)).height);
		Widgets.DrawBoxSolid(val, new Color(0.13f, 0.13f, 0.13f));
		Color color = GUI.color;
		GUI.color = SubTextColor;
		Widgets.Label(new Rect(((Rect)(ref val)).x + 6f, ((Rect)(ref val)).y, ((Rect)(ref val)).width - 12f, ((Rect)(ref val)).height), (flag ? "+ " : "- ") + node.label + "   x" + node.total);
		GUI.color = color;
		if (Widgets.ButtonInvisible(val, true))
		{
			if (flag)
			{
				collapsedNodes.Remove(node.key);
			}
			else
			{
				collapsedNodes.Add(node.key);
			}
			rowsDirty = true;
			PersistCollapsed(collapsedNodes);
		}
	}

	private void DrawStockRow(Rect row, SDS_StockEntry entry, int depth, string pathLabel)
	{
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_026d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0335: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02da: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0302: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_030d: Unknown result type (might be due to invalid IL or missing references)
		//IL_031c: Unknown result type (might be due to invalid IL or missing references)
		//IL_032c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0352: Unknown result type (might be due to invalid IL or missing references)
		//IL_0355: Unknown result type (might be due to invalid IL or missing references)
		//IL_03af: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0415: Unknown result type (might be due to invalid IL or missing references)
		//IL_041e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0427: Unknown result type (might be due to invalid IL or missing references)
		bool flag = selectedPreserved == entry.preserved && selectedKey != null && selectedKey.SameAs(entry.key);
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		aggMembers.TryGetValue(entry, out var value);
		bool flag2 = IsRowForbidden(instance, entry.key, value);
		SDS_StockKey sDS_StockKey = entry.displayKey ?? entry.key;
		float num = ((Rect)(ref row)).xMax - 2f;
		float num2 = ((Rect)(ref row)).y + (((Rect)(ref row)).height - 24f) / 2f;
		Rect val = default(Rect);
		((Rect)(ref val))..ctor(num - 24f, num2, 24f, 24f);
		num = ((Rect)(ref val)).x - 2f;
		Rect val2 = default(Rect);
		((Rect)(ref val2))..ctor(num - 24f, num2, 24f, 24f);
		num = ((Rect)(ref val2)).x - 2f;
		Rect val3 = default(Rect);
		((Rect)(ref val3))..ctor(num - 62f, ((Rect)(ref row)).y, 62f, ((Rect)(ref row)).height);
		num = ((Rect)(ref val3)).x - 2f;
		float num3 = (float)depth * 8f + 8f;
		Rect val4 = default(Rect);
		((Rect)(ref val4))..ctor(((Rect)(ref row)).x + num3, ((Rect)(ref row)).y + (((Rect)(ref row)).height - 24f) / 2f, 24f, 24f);
		if (sDS_StockKey.thingDef != null)
		{
			Widgets.ThingIcon(val4, sDS_StockKey.thingDef, sDS_StockKey.stuff, (ThingStyleDef)null, 1f, (Color?)null, (int?)null, 1f);
		}
		string text = (entry.preserved ? ("   x" + entry.count + "  " + ((object)Translator.Translate("SDS.Window.PreservedTag")/*cast due to constrained. prefix*/).ToString()) : ("   x" + entry.count));
		string text2 = (flag ? "> " : "") + (GenText.NullOrEmpty(pathLabel) ? string.Empty : pathLabel) + sDS_StockKey.Label + text;
		Rect val5 = default(Rect);
		((Rect)(ref val5))..ctor(((Rect)(ref val4)).xMax + 4f, ((Rect)(ref row)).y, Mathf.Max(0f, num - ((Rect)(ref val4)).xMax - 4f), ((Rect)(ref row)).height);
		Color color = GUI.color;
		GUI.color = (flag ? Color.white : (flag2 ? ForbiddenTextColor : TextColor));
		bool wordWrap = Text.WordWrap;
		Text.WordWrap = false;
		Widgets.Label(val5, GenText.Truncate(text2, ((Rect)(ref val5)).width, (Dictionary<string, string>)null));
		Text.WordWrap = wordWrap;
		GUI.color = color;
		if (sDS_StockKey.thingDef != null)
		{
			float num4 = RowMass(entry, value);
			TextAnchor anchor = Text.Anchor;
			bool wordWrap2 = Text.WordWrap;
			Color color2 = GUI.color;
			Text.Anchor = (TextAnchor)5;
			Text.WordWrap = false;
			GUI.color = (flag ? Color.white : TransferableOneWayWidget.ItemMassColor);
			Widgets.Label(val3, GenText.ToStringMass(num4));
			GUI.color = color2;
			Text.WordWrap = wordWrap2;
			Text.Anchor = anchor;
		}
		if (!Mouse.IsOver(val2) && !Mouse.IsOver(val))
		{
			TooltipHandler.TipRegion(row, TipSignal.op_Implicit(text2));
		}
		bool flag3 = !flag2;
		Widgets.Checkbox(((Rect)(ref val2)).x, ((Rect)(ref val2)).y, ref flag3, 24f, false, false, (Texture2D)null, (Texture2D)null);
		bool flag4 = !flag3;
		if (flag4 != flag2)
		{
			SetRowForbidden(instance, entry, value, flag4);
			message = null;
		}
		TooltipHandler.TipRegion(val2, TipSignal.op_Implicit(((object)Translator.Translate(flag4 ? "CommandForbiddenDesc" : "CommandNotForbiddenDesc")/*cast due to constrained. prefix*/).ToString()));
		if (entry.key.thingDef != null)
		{
			Widgets.InfoCardButton(((Rect)(ref val)).x, ((Rect)(ref val)).y, (Def)(object)entry.key.thingDef);
		}
		if (!Mouse.IsOver(val2) && !Mouse.IsOver(val) && Widgets.ButtonInvisible(row, true))
		{
			selectedKey = entry.key;
			selectedPreserved = entry.preserved;
			selectedMembers = value;
			message = null;
			PersistSelection();
		}
	}

	private static float RowMass(SDS_StockEntry entry, List<SDS_StockEntry> members)
	{
		if (entry == null || entry.key == null || entry.key.thingDef == null)
		{
			return 0f;
		}
		if (members != null && members.Count > 0)
		{
			float num = 0f;
			bool flag = false;
			for (int i = 0; i < members.Count; i++)
			{
				SDS_StockEntry sDS_StockEntry = members[i];
				if (sDS_StockEntry != null && sDS_StockEntry.key != null && sDS_StockEntry.key.thingDef != null)
				{
					num += StatExtension.GetStatValueAbstract((BuildableDef)(object)sDS_StockEntry.key.thingDef, StatDefOf.Mass, sDS_StockEntry.key.stuff) * (float)sDS_StockEntry.count;
					flag = true;
				}
			}
			if (flag)
			{
				return num;
			}
		}
		SDS_StockKey sDS_StockKey = entry.displayKey ?? entry.key;
		return StatExtension.GetStatValueAbstract((BuildableDef)(object)sDS_StockKey.thingDef, StatDefOf.Mass, sDS_StockKey.stuff) * (float)entry.count;
	}

	private static bool RowHasAvailable(SDS_GameComponent_Grid grid, SDS_StockKey key, List<SDS_StockEntry> members)
	{
		if (grid == null)
		{
			return false;
		}
		if (members != null && members.Count > 0)
		{
			for (int i = 0; i < members.Count; i++)
			{
				SDS_StockEntry sDS_StockEntry = members[i];
				if (sDS_StockEntry != null && sDS_StockEntry.key != null && SDS_Availability.GetAvailableCount(grid, sDS_StockEntry.key) > 0)
				{
					return true;
				}
			}
			return false;
		}
		return key != null && SDS_Availability.GetAvailableCount(grid, key) > 0;
	}

	private static bool IsRowForbidden(SDS_GameComponent_Grid grid, SDS_StockKey key, List<SDS_StockEntry> members)
	{
		if (grid == null || key == null)
		{
			return false;
		}
		if (members == null || members.Count == 0)
		{
			return grid.IsForbidden(key);
		}
		bool result = false;
		for (int i = 0; i < members.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = members[i];
			if (sDS_StockEntry != null && sDS_StockEntry.key != null)
			{
				result = true;
				if (!grid.IsForbidden(sDS_StockEntry.key))
				{
					return false;
				}
			}
		}
		return result;
	}

	private static void SetRowForbidden(SDS_GameComponent_Grid grid, SDS_StockEntry entry, List<SDS_StockEntry> members, bool forbidden)
	{
		if (grid == null || entry == null)
		{
			return;
		}
		if (members == null || members.Count == 0)
		{
			grid.SetForbidden(entry.key, forbidden);
			return;
		}
		for (int i = 0; i < members.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = members[i];
			if (sDS_StockEntry != null && sDS_StockEntry.key != null)
			{
				grid.SetForbidden(sDS_StockEntry.key, forbidden);
			}
		}
	}

	private List<SDS_StockEntry> BuildDisplayEntries(List<SDS_StockEntry> entries)
	{
		aggMembers.Clear();
		if (entries == null || entries.Count == 0)
		{
			return entries;
		}
		if (SDS_Config.SeparateByQuality && SDS_Config.SeparateByStuff)
		{
			return entries;
		}
		Dictionary<string, SDS_StockEntry> dictionary = new Dictionary<string, SDS_StockEntry>();
		Dictionary<string, List<SDS_StockEntry>> dictionary2 = new Dictionary<string, List<SDS_StockEntry>>();
		List<SDS_StockEntry> list = new List<SDS_StockEntry>(entries.Count);
		for (int i = 0; i < entries.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = entries[i];
			if (sDS_StockEntry != null && sDS_StockEntry.key != null && sDS_StockEntry.key.thingDef != null)
			{
				SDS_StockKey sDS_StockKey = SDS_GameComponent_Grid.CategoryKeyOf(sDS_StockEntry.key);
				string key = (sDS_StockEntry.preserved ? "P|" : "V|") + ((Def)sDS_StockKey.thingDef).defName + "|" + ((sDS_StockKey.stuff != null) ? ((Def)sDS_StockKey.stuff).defName : "-") + "|" + sDS_StockKey.qualityInt;
				if (!dictionary.TryGetValue(key, out var value))
				{
					value = (dictionary[key] = new SDS_StockEntry(sDS_StockEntry.key, 0L, sDS_StockEntry.preserved)
					{
						displayKey = sDS_StockKey
					});
					dictionary2[key] = new List<SDS_StockEntry>();
					list.Add(value);
				}
				else if (value.key != null && SDS_StockKey.CompareIdentity(sDS_StockEntry.key, value.key) < 0)
				{
					value.key = sDS_StockEntry.key;
				}
				value.count += sDS_StockEntry.count;
				dictionary2[key].Add(sDS_StockEntry);
			}
		}
		foreach (KeyValuePair<string, List<SDS_StockEntry>> item in dictionary2)
		{
			aggMembers[dictionary[item.Key]] = item.Value;
		}
		return list;
	}

	private List<SDS_StockRow> BuildTreeRows(List<SDS_StockEntry> entries)
	{
		SDS_CatNode node = BuildCategoryTree(entries);
		List<SDS_StockRow> list = new List<SDS_StockRow>();
		AppendNodeRows(node, 0, isOutermostLevel: true, list);
		return list;
	}

	private void AppendNodeRows(SDS_CatNode node, int depth, bool isOutermostLevel, List<SDS_StockRow> rows)
	{
		bool isOutermostLevel2 = isOutermostLevel;
		if (!GenText.NullOrEmpty(node.key))
		{
			if (isOutermostLevel && node.children.Count > 0)
			{
				isOutermostLevel2 = false;
			}
			else
			{
				rows.Add(new SDS_StockRow
				{
					node = node,
					depth = depth
				});
				if (collapsedNodes.Contains(node.key))
				{
					return;
				}
				depth++;
				isOutermostLevel2 = false;
			}
		}
		for (int i = 0; i < node.children.Count; i++)
		{
			AppendNodeRows(node.children[i], depth, isOutermostLevel2, rows);
		}
		for (int j = 0; j < node.entries.Count; j++)
		{
			rows.Add(new SDS_StockRow
			{
				entry = node.entries[j],
				depth = depth
			});
		}
	}

	private static SDS_CatNode BuildCategoryTree(List<SDS_StockEntry> entries)
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		SDS_CatNode sDS_CatNode = new SDS_CatNode
		{
			key = string.Empty,
			label = string.Empty
		};
		for (int i = 0; i < entries.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = entries[i];
			if (sDS_StockEntry == null || sDS_StockEntry.key == null || sDS_StockEntry.key.thingDef == null)
			{
				continue;
			}
			List<ThingCategoryDef> list = CategoryPath(sDS_StockEntry.key.thingDef);
			if (list.Count == 0)
			{
				SDS_CatNode sDS_CatNode2 = sDS_CatNode.ChildFor("SDS.Window.Uncategorized", ((object)Translator.Translate("SDS.Window.Uncategorized")/*cast due to constrained. prefix*/).ToString());
				sDS_CatNode2.entries.Add(sDS_StockEntry);
				sDS_CatNode2.total += sDS_StockEntry.count;
				continue;
			}
			SDS_CatNode sDS_CatNode3 = sDS_CatNode;
			string text = string.Empty;
			for (int j = 0; j < list.Count; j++)
			{
				text = ((text.Length == 0) ? ((Def)list[j]).defName : (text + "/" + ((Def)list[j]).defName));
				sDS_CatNode3 = sDS_CatNode3.ChildFor(text, ((object)((Def)list[j]).LabelCap/*cast due to constrained. prefix*/).ToString());
				sDS_CatNode3.total += sDS_StockEntry.count;
			}
			sDS_CatNode3.entries.Add(sDS_StockEntry);
		}
		SortTree(sDS_CatNode);
		return sDS_CatNode;
	}

	private static List<ThingCategoryDef> CategoryPath(ThingDef def)
	{
		List<ThingCategoryDef> list = new List<ThingCategoryDef>();
		if (def == null || GenList.NullOrEmpty<ThingCategoryDef>((IList<ThingCategoryDef>)def.thingCategories))
		{
			return list;
		}
		for (ThingCategoryDef val = def.thingCategories[0]; val != null; val = val.parent)
		{
			list.Insert(0, val);
		}
		return list;
	}

	private static string CategoryPathLabel(ThingDef def)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		List<ThingCategoryDef> list = CategoryPath(def);
		if (list.Count == 0)
		{
			return ((object)Translator.Translate("SDS.Window.Uncategorized")/*cast due to constrained. prefix*/).ToString();
		}
		string text = string.Empty;
		for (int i = 0; i < list.Count; i++)
		{
			text = ((text.Length == 0) ? ((object)((Def)list[i]).LabelCap/*cast due to constrained. prefix*/).ToString() : (text + " > " + ((object)((Def)list[i]).LabelCap/*cast due to constrained. prefix*/).ToString()));
		}
		return text;
	}

	private static void SortTree(SDS_CatNode node)
	{
		node.children.Sort((SDS_CatNode a, SDS_CatNode b) => string.CompareOrdinal(a.label, b.label));
		node.entries.Sort(delegate(SDS_StockEntry a, SDS_StockEntry b)
		{
			int num2 = string.CompareOrdinal(((Def)a.key.thingDef).defName, ((Def)b.key.thingDef).defName);
			if (num2 != 0)
			{
				return num2;
			}
			string strA = ((a.key.stuff != null) ? ((Def)a.key.stuff).defName : string.Empty);
			string strB = ((b.key.stuff != null) ? ((Def)b.key.stuff).defName : string.Empty);
			num2 = string.CompareOrdinal(strA, strB);
			return (num2 != 0) ? num2 : a.key.qualityInt.CompareTo(b.key.qualityInt);
		});
		for (int num = 0; num < node.children.Count; num++)
		{
			SortTree(node.children[num]);
		}
	}

	private static List<SDS_StockRow> BuildSearchRows(List<SDS_StockEntry> entries, string query)
	{
		List<SDS_StockRow> list = new List<SDS_StockRow>();
		for (int i = 0; i < entries.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = entries[i];
			if (sDS_StockEntry != null && sDS_StockEntry.key != null && sDS_StockEntry.key.thingDef != null)
			{
				string text = CategoryPathLabel(sDS_StockEntry.key.thingDef);
				if (sDS_StockEntry.key.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					list.Add(new SDS_StockRow
					{
						entry = sDS_StockEntry,
						pathLabel = text + "   >   "
					});
				}
			}
		}
		return list;
	}

	private static void ColoredLabel(Rect rect, string text)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		ColoredLabel(rect, text, TextColor);
	}

	private static void ColoredLabel(Rect rect, string text, Color color)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		Color color2 = GUI.color;
		GUI.color = color;
		Widgets.Label(rect, text);
		GUI.color = color2;
	}
}
public class Window_SDS_OutputDialog : Window
{
	private sealed class MemberButton
	{
		public SDS_StockEntry entry;

		public float width;
	}

	public enum DialogMode
	{
		Output,
		Use
	}

	private const int DefaultAmount = 1000;

	private const float TitleH = 26f;

	private const float RecordsLabelH = 20f;

	private const float RowH = 26f;

	private const float RowGap = 6f;

	private const float ElementGap = 4f;

	private const float ConfirmH = 30f;

	private const float ArrowW = 26f;

	private const float FieldW = 84f;

	private const float MinInnerWidth = 360f;

	private const float MaxInnerWidth = 620f;

	private const float WindowMargin = 36f;

	private static readonly string[] StepTexts = new string[4] { "-100", "-10", "+10", "+100" };

	private static readonly long[] StepDeltas = new long[4] { -100L, -10L, 10L, 100L };

	private SDS_StockKey key;

	private bool preserved;

	private List<SDS_StockEntry> members;

	private List<List<MemberButton>> memberRows;

	private float innerWidth = 360f;

	private float contentHeight = 94f;

	private float recordsLabelWidth;

	private float amountLabelWidth;

	private float allButtonWidth;

	private float confirmWidth;

	private float[] stepWidths;

	private string recordsLabelText;

	private string amountLabelText;

	private string confirmText;

	private string amountBuffer = 1000.ToString();

	private int amount = 1000;

	private bool amountInitialized;

	private GUIStyle centeredFieldStyle;

	private readonly DialogMode dialogMode;

	public override Vector2 InitialSize => new Vector2(innerWidth + 36f, contentHeight + 36f);

	public Window_SDS_OutputDialog(SDS_StockKey key)
		: this(key, preserved: false)
	{
	}

	public Window_SDS_OutputDialog(SDS_StockKey key, bool preserved)
		: this(key, preserved, null)
	{
	}

	public Window_SDS_OutputDialog(SDS_StockKey key, bool preserved, List<SDS_StockEntry> members)
		: this(key, preserved, members, DialogMode.Output)
	{
	}

	public Window_SDS_OutputDialog(SDS_StockKey key, bool preserved, List<SDS_StockEntry> members, DialogMode mode)
		: base((IWindowDrawing)null)
	{
		dialogMode = mode;
		this.key = key;
		this.preserved = preserved;
		if (members != null && members.Count > 0)
		{
			SDS_StockEntry sDS_StockEntry = members[0];
			for (int i = 1; i < members.Count; i++)
			{
				SDS_StockEntry sDS_StockEntry2 = members[i];
				if (sDS_StockEntry2 != null && sDS_StockEntry != null && sDS_StockEntry2.count > sDS_StockEntry.count)
				{
					sDS_StockEntry = sDS_StockEntry2;
				}
			}
			if (sDS_StockEntry != null && sDS_StockEntry.key != null)
			{
				this.key = sDS_StockEntry.key;
				this.preserved = sDS_StockEntry.preserved;
			}
			if (members.Count > 1)
			{
				this.members = members;
			}
		}
		BuildContentSize();
		base.doCloseX = true;
		base.draggable = true;
		base.resizeable = false;
		base.absorbInputAroundWindow = true;
		base.closeOnCancel = true;
		base.closeOnClickedOutside = false;
		base.preventCameraMotion = true;
	}

	private void BuildContentSize()
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_0261: Unknown result type (might be due to invalid IL or missing references)
		bool flag = dialogMode == DialogMode.Use;
		recordsLabelText = ((object)Translator.Translate(flag ? "SDS.Use.Records" : "SDS.Window.OutputRecords")/*cast due to constrained. prefix*/).ToString();
		amountLabelText = ((object)Translator.Translate("SDS.Window.OutputAmount")/*cast due to constrained. prefix*/).ToString();
		confirmText = ((object)Translator.Translate(flag ? "SDS.Use.Confirm" : "SDS.Window.OutputConfirm")/*cast due to constrained. prefix*/).ToString();
		recordsLabelWidth = MeasureSmall(recordsLabelText).x;
		amountLabelWidth = MeasureSmall(amountLabelText).x + 2f;
		confirmWidth = Mathf.Max(120f, MeasureSmall(confirmText).x + 24f);
		stepWidths = new float[StepTexts.Length];
		float num = 0f;
		for (int i = 0; i < StepTexts.Length; i++)
		{
			stepWidths[i] = ButtonWidthFor(StepTexts[i]);
			num += stepWidths[i] + ((i < StepTexts.Length - 1) ? 4f : 0f);
		}
		allButtonWidth = ButtonWidthFor(((object)Translator.Translate("SDS.Window.OutputAll")/*cast due to constrained. prefix*/).ToString());
		float num2 = (flag ? 0f : Mathf.Max(amountLabelWidth + 4f + 26f + 4f + 84f + 4f + 26f + 6f + allButtonWidth, num));
		long num3 = 0L;
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance != null && key != null)
		{
			num3 = SDS_Availability.GetAvailableCount(instance, key);
		}
		string text = (flag ? ((object)TranslatorFormattedStringExtensions.Translate("SDS.Use.Title", NamedArgument.op_Implicit((key != null) ? key.Label : "?"))/*cast due to constrained. prefix*/).ToString() : ((object)TranslatorFormattedStringExtensions.Translate("SDS.Window.OutputSelected", NamedArgument.op_Implicit((key != null) ? key.Label : "?"), NamedArgument.op_Implicit(num3))/*cast due to constrained. prefix*/).ToString());
		float x = MeasureSmall(text).x;
		innerWidth = Mathf.Clamp(Mathf.Max(num2, Mathf.Max(x, Mathf.Max(recordsLabelWidth, confirmWidth))), 360f, 620f);
		BuildMemberRows();
		float num4 = 26f;
		if (memberRows != null)
		{
			num4 += 32f + (float)memberRows.Count * 26f + (float)(memberRows.Count - 1) * 6f;
		}
		num4 += (flag ? 36f : 100f);
		contentHeight = num4;
	}

	private void BuildMemberRows()
	{
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		memberRows = null;
		if (members == null || members.Count == 0)
		{
			return;
		}
		List<List<MemberButton>> list = new List<List<MemberButton>>();
		List<MemberButton> list2 = new List<MemberButton>();
		float num = 0f;
		for (int i = 0; i < members.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = members[i];
			if (sDS_StockEntry != null && sDS_StockEntry.key != null)
			{
				MemberButton memberButton = new MemberButton
				{
					entry = sDS_StockEntry,
					width = Mathf.Clamp(MeasureSmall(MemberLabelText(sDS_StockEntry, selected: true)).x + 16f, 110f, innerWidth)
				};
				if (list2.Count > 0 && num + 4f + memberButton.width > innerWidth)
				{
					list.Add(list2);
					list2 = new List<MemberButton>();
					num = 0f;
				}
				if (list2.Count > 0)
				{
					num += 4f;
				}
				list2.Add(memberButton);
				num += memberButton.width;
			}
		}
		if (list2.Count > 0)
		{
			list.Add(list2);
		}
		if (list.Count > 0)
		{
			memberRows = list;
		}
	}

	private static string MemberLabelText(SDS_StockEntry member, bool selected)
	{
		return (selected ? "> " : string.Empty) + member.key.Label + " x" + member.count;
	}

	private static Vector2 MeasureSmall(string text)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		GameFont font = Text.Font;
		Text.Font = (GameFont)1;
		Vector2 result = Text.CalcSize(GenText.NullOrEmpty(text) ? string.Empty : text);
		Text.Font = font;
		return result;
	}

	private static float ButtonWidthFor(string text)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return Mathf.Max(34f, MeasureSmall(text).x + 16f);
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		bool wordWrap = Text.WordWrap;
		try
		{
			DrawDialogContents(inRect);
		}
		finally
		{
			Text.WordWrap = wordWrap;
			Text.Anchor = anchor;
			Text.Font = font;
		}
	}

	private void DrawDialogContents(Rect inRect)
	{
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_031e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0347: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0353: Unknown result type (might be due to invalid IL or missing references)
		//IL_041f: Unknown result type (might be due to invalid IL or missing references)
		//IL_044e: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e6: Expected O, but got Unknown
		//IL_0521: Unknown result type (might be due to invalid IL or missing references)
		//IL_0581: Unknown result type (might be due to invalid IL or missing references)
		//IL_058b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0590: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_072e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0656: Unknown result type (might be due to invalid IL or missing references)
		//IL_0757: Unknown result type (might be due to invalid IL or missing references)
		//IL_075e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0763: Unknown result type (might be due to invalid IL or missing references)
		Text.Font = (GameFont)1;
		Text.Anchor = (TextAnchor)4;
		Text.WordWrap = false;
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		long num = 0L;
		if (instance != null && key != null)
		{
			num = SDS_Availability.GetAvailableCount(instance, key);
		}
		float num2 = ((Rect)(ref inRect)).x + ((Rect)(ref inRect)).width * 0.5f;
		float y = ((Rect)(ref inRect)).y;
		string text = ((dialogMode == DialogMode.Use) ? ((object)TranslatorFormattedStringExtensions.Translate("SDS.Use.Title", NamedArgument.op_Implicit(key.Label))/*cast due to constrained. prefix*/).ToString() : ((object)TranslatorFormattedStringExtensions.Translate("SDS.Window.OutputSelected", NamedArgument.op_Implicit(key.Label), NamedArgument.op_Implicit(num))/*cast due to constrained. prefix*/).ToString());
		Widgets.Label(new Rect(((Rect)(ref inRect)).x, y, ((Rect)(ref inRect)).width, 26f), text);
		y += 26f;
		if (memberRows != null)
		{
			y += 6f;
			Widgets.Label(new Rect(((Rect)(ref inRect)).x, y, ((Rect)(ref inRect)).width, 20f), recordsLabelText);
			y += 26f;
			for (int i = 0; i < memberRows.Count; i++)
			{
				List<MemberButton> list = memberRows[i];
				float num3 = 0f;
				for (int j = 0; j < list.Count; j++)
				{
					num3 += list[j].width + ((j > 0) ? 4f : 0f);
				}
				float num4 = num2 - num3 * 0.5f;
				for (int k = 0; k < list.Count; k++)
				{
					MemberButton memberButton = list[k];
					SDS_StockEntry entry = memberButton.entry;
					bool selected = entry.preserved == preserved && entry.key.SameAs(key);
					if (Widgets.ButtonText(new Rect(num4, y, memberButton.width, 26f), MemberLabelText(entry, selected), true, true, true, (TextAnchor?)null))
					{
						key = entry.key;
						preserved = entry.preserved;
						amountInitialized = false;
						amountBuffer = string.Empty;
					}
					num4 += memberButton.width + 4f;
				}
				y += 26f;
				if (i < memberRows.Count - 1)
				{
					y += 6f;
				}
			}
			if (instance != null && key != null)
			{
				num = SDS_Availability.GetAvailableCount(instance, key);
			}
		}
		if (dialogMode == DialogMode.Use)
		{
			y += 6f;
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(num2 - confirmWidth * 0.5f, y, confirmWidth, 30f);
			if (Widgets.ButtonText(val, confirmText, true, true, true, (TextAnchor?)null))
			{
				ConfirmOutput();
			}
			TooltipHandler.TipRegion(val, TipSignal.op_Implicit(Translator.Translate("SDS.Use.ConfirmTip")));
			return;
		}
		if (!amountInitialized)
		{
			amountInitialized = true;
			amountBuffer = Math.Min(1000L, num).ToString();
		}
		if (GenText.NullOrEmpty(amountBuffer))
		{
			amountBuffer = Math.Min(1000L, num).ToString();
		}
		y += 6f;
		float num5 = amountLabelWidth + 4f + 26f + 4f + 84f + 4f + 26f + 6f + allButtonWidth;
		float num6 = num2 - num5 * 0.5f;
		Widgets.Label(new Rect(num6, y, amountLabelWidth, 26f), amountLabelText);
		num6 += amountLabelWidth + 4f;
		if (Widgets.ButtonText(new Rect(num6, y, 26f, 26f), "◀", true, true, true, (TextAnchor?)null))
		{
			amountBuffer = ClampAmount(ParseAmount(amountBuffer, num) - 1, num).ToString();
		}
		num6 += 30f;
		Rect val2 = default(Rect);
		((Rect)(ref val2))..ctor(num6, y, 84f, 26f);
		num6 += 88f;
		if (centeredFieldStyle == null)
		{
			centeredFieldStyle = new GUIStyle(Text.CurTextFieldStyle)
			{
				alignment = (TextAnchor)4
			};
		}
		amountBuffer = GUI.TextField(val2, GenText.NullOrEmpty(amountBuffer) ? string.Empty : amountBuffer, centeredFieldStyle);
		if (Widgets.ButtonText(new Rect(num6, y, 26f, 26f), "▶", true, true, true, (TextAnchor?)null))
		{
			amountBuffer = ClampAmount(ParseAmount(amountBuffer, num) + 1, num).ToString();
		}
		num6 += 32f;
		if (Widgets.ButtonText(new Rect(num6, y, allButtonWidth, 26f), ((object)Translator.Translate("SDS.Window.OutputAll")/*cast due to constrained. prefix*/).ToString(), true, true, true, (TextAnchor?)null))
		{
			amountBuffer = num.ToString();
		}
		y += 32f;
		float num7 = 0f;
		for (int l = 0; l < stepWidths.Length; l++)
		{
			if (l > 0)
			{
				num7 += 4f;
			}
			num7 += stepWidths[l];
		}
		float num8 = num2 - num7 * 0.5f;
		for (int m = 0; m < stepWidths.Length; m++)
		{
			if (m > 0)
			{
				num8 += 4f;
			}
			if (Widgets.ButtonText(new Rect(num8, y, stepWidths[m], 26f), StepTexts[m], true, true, true, (TextAnchor?)null))
			{
				amountBuffer = ClampAmount(ParseAmount(amountBuffer, num) + StepDeltas[m], num).ToString();
			}
			num8 += stepWidths[m];
		}
		y += 26f;
		long val3 = ParseAmount(amountBuffer, num);
		long val4 = Math.Min(val3, num);
		amount = (int)Math.Min(val4, 2147483647L);
		y += 6f;
		Rect val5 = default(Rect);
		((Rect)(ref val5))..ctor(num2 - confirmWidth * 0.5f, y, confirmWidth, 30f);
		if (Widgets.ButtonText(val5, confirmText, true, true, true, (TextAnchor?)null))
		{
			ConfirmOutput();
		}
		TooltipHandler.TipRegion(val5, TipSignal.op_Implicit(Translator.Translate("SDS.Window.OutputConfirmTip")));
	}

	public override void OnAcceptKeyPressed()
	{
		ConfirmOutput();
		if (Event.current != null)
		{
			Event.current.Use();
		}
	}

	private void ConfirmOutput()
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		if (key == null)
		{
			return;
		}
		if (dialogMode == DialogMode.Use)
		{
			SDS_UseTargeting.Begin(key, preserved);
			return;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		long num = ((instance != null) ? SDS_Availability.GetAvailableCount(instance, key) : 0);
		long val = ParseAmount(amountBuffer, num);
		long val2 = Math.Min(val, num);
		amount = (int)Math.Min(val2, 2147483647L);
		if (amount <= 0)
		{
			Messages.Message(TaggedString.op_Implicit(Translator.Translate("SDS.Output.Insufficient")), MessageTypeDefOf.RejectInput, false);
		}
		else if (preserved)
		{
			SDS_OutputTargeting.BeginPreserved(key, amount);
		}
		else
		{
			SDS_OutputTargeting.Begin(key, amount);
		}
	}

	internal void ConfirmFromMiddleClick()
	{
		ConfirmOutput();
	}

	private static long ParseAmount(string buffer, long max)
	{
		if (long.TryParse(buffer, out var result) && result > 0)
		{
			return result;
		}
		return max;
	}

	private static long ClampAmount(long value, long max)
	{
		if (max <= 0)
		{
			return 0L;
		}
		if (value < 1)
		{
			return 1L;
		}
		return (value > max) ? max : value;
	}
}
public class SDS_ITab_DepositFilter : ITab
{
	private static readonly Vector2 WinSize = new Vector2(320f, 480f);

	private readonly UIState filterState = new UIState();

	public override bool IsVisible
	{
		get
		{
			Thing selThing = ((ITab)this).SelThing;
			if (selThing == null || selThing.Faction != Faction.OfPlayer)
			{
				return false;
			}
			return selThing is Building_SDS_Core || selThing is Building_SDS_Port;
		}
	}

	public SDS_ITab_DepositFilter()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		((InspectTabBase)this).size = WinSize;
		((InspectTabBase)this).labelKey = "SDS.Tab.DepositFilter";
		((InspectTabBase)this).tutorTag = null;
	}

	public override void OnOpen()
	{
		((InspectTabBase)this).OnOpen();
		filterState.quickSearch.Reset();
	}

	public override void Notify_ClickOutsideWindow()
	{
		((InspectTabBase)this).Notify_ClickOutsideWindow();
		filterState.quickSearch.Unfocus();
	}

	protected override void FillTab()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Invalid comparison between Unknown and I4
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Expected O, but got Unknown
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Expected O, but got Unknown
		Rect val = GenUI.ContractedBy(new Rect(0f, 0f, WinSize.x, WinSize.y), 10f);
		Widgets.BeginGroup(val);
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			Widgets.Label(new Rect(0f, 0f, ((Rect)(ref val)).width, 30f), ((object)Translator.Translate("SDS.Window.NoGridHint")/*cast due to constrained. prefix*/).ToString());
		}
		else
		{
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(0f, 0f, ((Rect)(ref val)).width, ((Rect)(ref val)).height);
			if (((ITab)this).SelThing is Building_SDS_Core)
			{
				Text.Font = (GameFont)1;
				Rect val3 = default(Rect);
				((Rect)(ref val3))..ctor(0f, 0f, 160f, 29f);
				StoragePriority value = SDS_EndpointPriorityState.Value;
				if (Widgets.ButtonText(val3, TaggedString.op_Implicit(Translator.Translate("Priority") + ": " + GenText.CapitalizeFirst(StoragePriorityHelper.Label(value))), true, true, true, (TextAnchor?)null))
				{
					List<FloatMenuOption> list = new List<FloatMenuOption>();
					foreach (StoragePriority value2 in Enum.GetValues(typeof(StoragePriority)))
					{
						if ((int)value2 != 0)
						{
							StoragePriority local = value2;
							list.Add(new FloatMenuOption(GenText.CapitalizeFirst(StoragePriorityHelper.Label(local)), (Action)delegate
							{
								//IL_0002: Unknown result type (might be due to invalid IL or missing references)
								SDS_EndpointPriorityState.SetValue(local);
							}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
						}
					}
					Find.WindowStack.Add((Window)new FloatMenu(list));
				}
				UIHighlighter.HighlightOpportunity(val3, "StoragePriority");
				string text = ((object)Translator.Translate("SDS.Tab.GlobalPriority")/*cast due to constrained. prefix*/).ToString() + "\n" + ((object)Translator.Translate("SDS.Tab.PriorityHint")/*cast due to constrained. prefix*/).ToString();
				if (SDS_EndpointPriorityState.IgnorePriority)
				{
					text = text + "\n" + ((object)Translator.Translate("SDS.Tab.IgnorePriorityOn")/*cast due to constrained. prefix*/).ToString();
				}
				TooltipHandler.TipRegion(val3, TipSignal.op_Implicit(text));
				((Rect)(ref val2))..ctor(0f, 35f, ((Rect)(ref val)).width, Mathf.Max(0f, ((Rect)(ref val)).height - 35f));
			}
			ThingFilterUI.DoThingFilterConfigWindow(val2, filterState, instance.DepositFilter, (ThingFilter)null, 1, (IEnumerable<ThingDef>)null, (IEnumerable<SpecialThingFilterDef>)null, false, false, false, (List<ThingDef>)null, (Map)null);
		}
		Widgets.EndGroup();
	}
}
[HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls")]
public static class Patch_SDS_PlaySettingsButton
{
	private static void Postfix(WidgetRow row, bool worldView)
	{
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		if (worldView || row == null)
		{
			return;
		}
		GameFont font = Text.Font;
		try
		{
			Text.Font = (GameFont)1;
			string text = ((object)Translator.Translate("SDS.Button.Label")/*cast due to constrained. prefix*/).ToString();
			Rect val = row.ButtonRect(text, (float?)24f);
			((Rect)(ref val)).height = 22f;
			TooltipHandler.TipRegion(val, TipSignal.op_Implicit(((object)Translator.Translate("SDS.Window.TitleTip")/*cast due to constrained. prefix*/).ToString() + "\n" + SDS_Hotkeys.HotkeyHintText()));
			if (Widgets.ButtonText(val, GenText.Truncate(text, ((Rect)(ref val)).width - 4f, (Dictionary<string, string>)null), true, true, true, (TextAnchor?)null))
			{
				SDS_Hotkeys.ToggleSidebar();
			}
		}
		finally
		{
			Text.Font = font;
		}
	}
}
internal static class SDS_ReadOnlyUI
{
	private static SDS_GameComponent_Grid cachedProductsGrid;

	private static SDS_ReadVersions cachedProductsVersions;

	private static bool cachedProductsValid;

	private static List<SDS_StockEntry> cachedProductsSnapshot;

	private static readonly List<Thing> cachedPreservedSnapshot = new List<Thing>();

	internal static void Reset()
	{
		cachedProductsGrid = null;
		cachedProductsVersions = default(SDS_ReadVersions);
		cachedProductsValid = false;
		cachedProductsSnapshot = null;
		cachedPreservedSnapshot.Clear();
	}

	public static bool UsesResourceCounter(Bill_Production bill, ThingDef def)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Invalid comparison between Unknown and I4
		return def.CountAsResource && !bill.includeEquipped && (bill.includeTainted || !def.IsApparel || !def.apparel.careIfWornByCorpse) && bill.GetIncludeSlotGroup() == null && bill.hpRange.min == 0f && bill.hpRange.max == 1f && (int)bill.qualityRange.min == 0 && (int)bill.qualityRange.max == 6 && !bill.limitToAllowedStuff;
	}

	public static long CountGridProducts(RecipeWorkerCounter worker, Bill_Production bill, ThingDef def)
	{
		if (worker == null || bill == null || def == null)
		{
			return 0L;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !SDS_OutputUtility.MapExposesGrid(((Bill)bill).Map))
		{
			return 0L;
		}
		long num = 0L;
		SDS_ReadVersions sDS_ReadVersions = SDS_ReadVersions.Capture();
		if (!cachedProductsValid || cachedProductsGrid != instance || !sDS_ReadVersions.SameExposureAs(in cachedProductsVersions) || cachedProductsSnapshot == null)
		{
			cachedProductsGrid = instance;
			cachedProductsVersions = sDS_ReadVersions;
			cachedProductsValid = true;
			cachedProductsSnapshot = instance.Snapshot(0);
			cachedPreservedSnapshot.Clear();
			instance.AppendPreservedThings(cachedPreservedSnapshot);
		}
		List<SDS_StockEntry> list = cachedProductsSnapshot;
		for (int i = 0; i < list.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = list[i];
			if (sDS_StockEntry == null || sDS_StockEntry.key == null || sDS_StockEntry.key.thingDef != def || sDS_StockEntry.count <= 0)
			{
				continue;
			}
			bool flag;
			if (SDS_MirrorService.TryGetMirror(((Bill)bill).Map, sDS_StockEntry.key, out var mirror))
			{
				flag = worker.CountValidThing(mirror, bill, def);
			}
			else
			{
				Thing val = SDS_OutputUtility.CachedDisplayThing(sDS_StockEntry.key);
				flag = val != null && worker.CountValidThing(val, bill, def);
			}
			if (flag)
			{
				long num2 = sDS_StockEntry.count + SDS_Exposure.PhysicalMirrorUnitOffset(((Bill)bill).Map, sDS_StockEntry.key);
				if (num2 > 0)
				{
					num += num2;
				}
			}
		}
		for (int j = 0; j < cachedPreservedSnapshot.Count; j++)
		{
			Thing val2 = cachedPreservedSnapshot[j];
			if (val2 == null || val2.Destroyed)
			{
				continue;
			}
			MinifiedThing val3 = (MinifiedThing)(object)((val2 is MinifiedThing) ? val2 : null);
			if (val3 != null && val3.InnerThing != null && val3.InnerThing.def == def)
			{
				if (worker.CountValidThing(val3.InnerThing, bill, def))
				{
					num += (long)val2.stackCount * (long)val3.InnerThing.stackCount;
				}
			}
			else if (val2.def == def && worker.CountValidThing(val2, bill, def))
			{
				num += val2.stackCount;
			}
		}
		return num;
	}

	public static int GridWarmClothesSetsCount(Alert_NeedWarmClothes alert, Map map)
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !SDS_OutputUtility.MapExposesGrid(map))
		{
			return 0;
		}
		List<float> list = new List<float>();
		List<float> list2 = new List<float>();
		List<float> list3 = new List<float>();
		List<SDS_StockEntry> list4 = instance.Snapshot(0);
		for (int i = 0; i < list4.Count; i++)
		{
			SDS_StockEntry sDS_StockEntry = list4[i];
			ThingDef val = ((sDS_StockEntry != null && sDS_StockEntry.key != null) ? sDS_StockEntry.key.thingDef : null);
			if (val == null || !val.IsApparel || sDS_StockEntry.count <= 0)
			{
				continue;
			}
			Thing mirror;
			bool mirrorCounts = SDS_MirrorService.TryGetMirror(map, sDS_StockEntry.key, out mirror) && StoreUtility.IsInAnyStorage(mirror);
			Thing val2 = SDS_OutputUtility.CachedDisplayThing(sDS_StockEntry.key);
			if (val2 == null)
			{
				continue;
			}
			float statValue = StatExtension.GetStatValue(val2, StatDefOf.Insulation_Cold, true, 120);
			bool flag = val.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso);
			bool flag2 = val.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Legs);
			bool flag3 = val.apparel.layers.Contains(ApparelLayerDefOf.OnSkin);
			if (statValue <= 0f || (!flag && !flag2))
			{
				continue;
			}
			long num = sDS_StockEntry.count + SDS_Exposure.PhysicalMirrorUnitOffset(map, sDS_StockEntry.key, mirrorCounts);
			if (num <= 0)
			{
				continue;
			}
			for (long num2 = 0L; num2 < num; num2++)
			{
				if (flag)
				{
					(flag3 ? list2 : list).Add(statValue);
				}
				if (flag2)
				{
					list3.Add(statValue);
				}
			}
		}
		List<Thing> list5 = new List<Thing>();
		instance.AppendPreservedThings(list5);
		for (int j = 0; j < list5.Count; j++)
		{
			Thing obj = list5[j];
			Apparel val3 = (Apparel)(object)((obj is Apparel) ? obj : null);
			if (val3 == null || ((Thing)val3).Destroyed || ((Thing)val3).def == null)
			{
				continue;
			}
			float statValue2 = StatExtension.GetStatValue((Thing)(object)val3, StatDefOf.Insulation_Cold, true, 120);
			if (statValue2 <= 0f)
			{
				continue;
			}
			bool flag4 = ((Thing)val3).def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso);
			bool flag5 = ((Thing)val3).def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Legs);
			if (!flag4 && !flag5)
			{
				continue;
			}
			bool flag6 = ((Thing)val3).def.apparel.layers.Contains(ApparelLayerDefOf.OnSkin);
			long num3 = ((Thing)val3).stackCount;
			for (long num4 = 0L; num4 < num3; num4++)
			{
				if (flag4)
				{
					(flag6 ? list2 : list).Add(statValue2);
				}
				if (flag5)
				{
					list3.Add(statValue2);
				}
			}
		}
		if (list.Count == 0 && list2.Count == 0 && list3.Count == 0)
		{
			return 0;
		}
		list.Sort((float a, float b) => b.CompareTo(a));
		list2.Sort((float a, float b) => b.CompareTo(a));
		list3.Sort((float a, float b) => b.CompareTo(a));
		float num5 = 0f;
		if (alert != null)
		{
			num5 = Traverse.Create((object)alert).Field("lowestTemperatureComing").GetValue<float>();
		}
		float num6 = StatExtension.GetStatValueAbstract((BuildableDef)(object)ThingDefOf.Human, StatDefOf.ComfyTemperatureMin, (ThingDef)null) - num5;
		if (num6 <= 0f)
		{
			return GenMath.Max(list.Count, list2.Count, list3.Count);
		}
		int num7 = 0;
		int num8 = 0;
		int num9 = 0;
		int num10 = 0;
		while (num8 < list.Count || num9 < list2.Count || num10 < list3.Count)
		{
			float num11 = 0f;
			if (num8 < list.Count)
			{
				num11 += list[num8++];
			}
			if (num9 < list2.Count && num11 < num6)
			{
				num11 += list2[num9++];
			}
			if (num10 < list3.Count && num11 < num6)
			{
				num11 += list3[num10++];
			}
			if (num11 < num6)
			{
				break;
			}
			num7++;
		}
		return num7;
	}
}
[HarmonyPatch(typeof(RecipeWorkerCounter), "CountProducts")]
[HarmonyPriority(200)]
internal static class Patch_SDS_RecipeCounterCountProducts
{
	public static void Postfix(RecipeWorkerCounter __instance, Bill_Production bill, ref int __result)
	{
		try
		{
			if (bill == null || ((Bill)bill).Map == null || ((Bill)bill).recipe == null)
			{
				return;
			}
			List<ThingDefCountClass> products = ((Bill)bill).recipe.products;
			if (GenList.NullOrEmpty<ThingDefCountClass>((IList<ThingDefCountClass>)products) || products[0] == null || products[0].thingDef == null)
			{
				return;
			}
			ThingDef thingDef = products[0].thingDef;
			if (!SDS_ReadOnlyUI.UsesResourceCounter(bill, thingDef) && bill.GetIncludeSlotGroup() == null)
			{
				long num = SDS_ReadOnlyUI.CountGridProducts(__instance, bill, thingDef);
				if (num > 0)
				{
					__result = (int)Math.Min(__result + num, 2147483647L);
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("清单产物计数注入超维空间失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
[HarmonyPatch(typeof(Alert_NeedWarmClothes), "FreeWarmClothesSetsCount")]
[HarmonyPriority(200)]
internal static class Patch_SDS_AlertNeedWarmClothes
{
	public static void Postfix(Alert_NeedWarmClothes __instance, Map map, ref int __result)
	{
		try
		{
			if (map != null)
			{
				int num = SDS_ReadOnlyUI.GridWarmClothesSetsCount(__instance, map);
				if (num > 0)
				{
					__result += num;
				}
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("寒冷预警注入超维空间失败（已忽略，原版行为不受影响）", exception);
		}
	}
}
internal static class SDS_JobReportText
{
	public static void Rewrite(JobDriver driver, ref string result)
	{
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Job val = driver?.job;
			if (val != null && val.def != null && (val.def == SDS_JobDefOf.DepositToGrid || ((LocalTargetInfo)(ref val.targetB)).Thing is ISDS_StorageEndpoint) && string.IsNullOrEmpty(val.reportStringOverride))
			{
				LocalTargetInfo val2 = val.GetTarget((TargetIndex)1);
				Thing thing = ((LocalTargetInfo)(ref val2)).Thing;
				if (thing == null && val.targetQueueA != null)
				{
					val2 = GenCollection.FirstValid(val.targetQueueA);
					thing = ((LocalTargetInfo)(ref val2)).Thing;
				}
				string text = ((thing != null && !thing.Destroyed) ? ((Entity)thing).LabelShort : string.Empty);
				result = ((object)TranslatorFormattedStringExtensions.Translate("SDS.JobReport.Deposit", NamedArgument.op_Implicit(text))/*cast due to constrained. prefix*/).ToString();
			}
		}
		catch (Exception)
		{
		}
	}
}
[HarmonyPatch(typeof(JobDriver), "GetReport")]
[HarmonyPriority(200)]
internal static class Patch_SDS_JobReport
{
	public static void Postfix(JobDriver __instance, ref string __result)
	{
		SDS_JobReportText.Rewrite(__instance, ref __result);
	}
}
[HarmonyPatch(typeof(JobDriver_HaulToContainer), "GetReport")]
[HarmonyPriority(200)]
internal static class Patch_SDS_JobReportHaulToContainer
{
	public static void Postfix(JobDriver_HaulToContainer __instance, ref string __result)
	{
		SDS_JobReportText.Rewrite((JobDriver)(object)__instance, ref __result);
	}
}
internal static class SDS_UseCommand
{
	private static readonly HashSet<string> ExcludedProviderNames = new HashSet<string>(StringComparer.Ordinal)
	{
		"FloatMenuOptionProvider_Arrest", "FloatMenuOptionProvider_CapturePawn", "FloatMenuOptionProvider_CaptureEntity", "FloatMenuOptionProvider_DraftedAttack", "FloatMenuOptionProvider_DraftedMove", "FloatMenuOptionProvider_DraftedRepair", "FloatMenuOptionProvider_DraftedTend", "FloatMenuOptionProvider_CarryPawn", "FloatMenuOptionProvider_CarryingPawn", "FloatMenuOptionProvider_CarryPawnToExit",
		"FloatMenuOptionProvider_CarryToShuttle", "FloatMenuOptionProvider_CarryToCryptosleepCasket", "FloatMenuOptionProvider_CarryToBiosculpterPod", "FloatMenuOptionProvider_CarryDeathrestingToCasket", "FloatMenuOptionProvider_CarryMechToCharger", "FloatMenuOptionProvider_LoadCaravan", "FloatMenuOptionProvider_LoadOntoPackAnimal", "FloatMenuOptionProvider_FromZone", "FloatMenuOptionProvider_FromLord", "FloatMenuOptionProvider_PickUpItem",
		"FloatMenuOptionProvider_HandleCorpse", "FloatMenuOptionProvider_Deathrest", "FloatMenuOptionProvider_Childcare", "FloatMenuOptionProvider_OfferHelp", "FloatMenuOptionProvider_Mechanitor", "FloatMenuOptionProvider_BringBabyToSafety", "FloatMenuOptionProvider_CleanRoom", "FloatMenuOptionProvider_ExtinguishFires", "FloatMenuOptionProvider_PutOutFireOnPawn", "FloatMenuOptionProvider_PrisonerBloodfeed",
		"FloatMenuOptionProvider_DressOtherPawn", "FloatMenuOptionProvider_DropEquipment", "FloatMenuOptionProvider_WorkGivers"
	};

	private static readonly string[] TaskJobPrefixes = new string[9] { "Haul", "Goto", "Attack", "Arrest", "Capture", "Carry", "Wait", "Prioritize", "LayDown" };

	private static List<FloatMenuOptionProvider> providers;

	private static bool reported;

	public static void ReportProvidersOnce()
	{
		if (reported)
		{
			return;
		}
		reported = true;
		try
		{
			List<FloatMenuOptionProvider> list = EnsureProviders();
			List<string> list2 = MissingExclusionNames();
			Log.Message("[超维存储] 【使用命令】右键菜单项来源：纳入 " + list.Count + " 个 provider / 排除表 " + ExcludedProviderNames.Count + " 类（未列出的第三方 provider 默认纳入）" + ((list2.Count > 0) ? ("；排除表未命中 " + list2.Count + " 类：" + string.Join("、", list2)) : "；排除表全部命中"));
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("「使用命令」provider 枚举失败（已跳过，使用命令不可用）", exception);
		}
	}

	private static List<string> MissingExclusionNames()
	{
		List<string> list = null;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		IEnumerable<Type> enumerable = GenTypes.AllSubclassesNonAbstract(typeof(FloatMenuOptionProvider));
		if (enumerable != null)
		{
			foreach (Type item in enumerable)
			{
				if (item != null)
				{
					hashSet.Add(item.Name);
				}
			}
		}
		foreach (string excludedProviderName in ExcludedProviderNames)
		{
			if (!hashSet.Contains(excludedProviderName))
			{
				(list ?? (list = new List<string>())).Add(excludedProviderName);
			}
		}
		return list ?? new List<string>();
	}

	public static void HandleTarget(LocalTargetInfo target, SDS_StockKey key, bool preserved)
	{
		try
		{
			Pawn pawn = ((LocalTargetInfo)(ref target)).Pawn;
			if (pawn == null)
			{
				MsgKey("SDS.Use.NotPawn");
			}
			else
			{
				TryUse(pawn, key, preserved);
			}
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("「使用」命令处理点选目标失败（已忽略）", exception);
		}
	}

	public static bool TryUse(Pawn pawn, SDS_StockKey key, bool preserved)
	{
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!IsValidTarget(pawn))
			{
				MsgKey("SDS.Use.NotEligible");
				return false;
			}
			if (!SDS_ImplantUtility.IsPortOperational(pawn))
			{
				MsgKey("SDS.Use.NoPort");
				return false;
			}
			SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
			if (instance == null || key == null)
			{
				MsgKey("SDS.Use.NoStock");
				return false;
			}
			if (!key.Resolved)
			{
				MsgRaw(((object)TranslatorFormattedStringExtensions.Translate("SDS.Use.Unresolved", NamedArgument.op_Implicit(key.EffectiveDefName ?? "?"))/*cast due to constrained. prefix*/).ToString());
				return false;
			}
			if (instance.IsForbidden(key))
			{
				MsgKey("SDS.Forbidden.CannotUse");
				return false;
			}
			if (SDS_Availability.GetAvailableCount(instance, key) <= 0)
			{
				MsgKey("SDS.Use.NoStock");
				return false;
			}
			if (!IsUsableCategory(key.thingDef))
			{
				MsgKey("SDS.Use.NotUsable");
				return false;
			}
			if (!(preserved ? SDS_WithdrawExecutor.TryWithdrawPreserved(key, 1, null, out var made, SDS_WithdrawMode.SpawnedTarget) : SDS_WithdrawExecutor.TryWithdrawVirtual(key, 1, out made, SDS_WithdrawMode.SpawnedTarget, pawn, requireFullQuantity: true)) || made == null || made.Count == 0)
			{
				SDS_SupplyFailure failure = SDS_Availability.GetFailure(key);
				MsgKey((failure == SDS_SupplyFailure.Forbidden) ? "SDS.Forbidden.CannotUse" : "SDS.Use.NoStock");
				return false;
			}
			Thing val = made[0];
			Map map = ((Thing)pawn).Map;
			IntVec3 positionHeld = ((Thing)pawn).PositionHeld;
			Thing val2 = default(Thing);
			if (!GenPlace.TryPlaceThing(val, positionHeld, map, (ThingPlaceMode)1, ref val2, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1) || val2 == null)
			{
				Refund(val, map, positionHeld);
				MsgKey("SDS.Use.NoPlace");
				return false;
			}
			if (val2 == val)
			{
				ForbidUtility.SetForbidden(val2, false, true);
				SDS_ProducedThingMarker.Mark(val2);
			}
			return DecideAndRun(pawn, val2, map);
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("「使用」命令执行失败（已忽略，原版行为不受影响）", exception);
			return false;
		}
	}

	private static bool DecideAndRun(Pawn pawn, Thing landed, Map map)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		FloatMenuContext context = new FloatMenuContext(new List<Pawn> { pawn }, landed.DrawPos, map);
		FloatMenuOption val = null;
		string text = null;
		foreach (FloatMenuOptionProvider item in EnsureProviders())
		{
			List<FloatMenuOption> list = CollectOptions(item, landed, context);
			if (list == null || list.Count == 0)
			{
				continue;
			}
			for (int i = 0; i < list.Count; i++)
			{
				FloatMenuOption val2 = list[i];
				if (val2 == null)
				{
					continue;
				}
				if (val2.action != null)
				{
					if (val == null)
					{
						val = val2;
					}
				}
				else if (text == null && !GenText.NullOrEmpty(val2.Label))
				{
					text = val2.Label;
				}
			}
			if (val == null)
			{
				continue;
			}
			break;
		}
		if (val == null)
		{
			Refund(landed, map, ((Thing)pawn).PositionHeld);
			if (!GenText.NullOrEmpty(text))
			{
				MsgRaw(((object)TranslatorFormattedStringExtensions.Translate("SDS.Use.Blocked", NamedArgument.op_Implicit(((Entity)landed).LabelShort), NamedArgument.op_Implicit(text))/*cast due to constrained. prefix*/).ToString());
			}
			else
			{
				MsgKey("SDS.Use.NotUsable");
			}
			return false;
		}
		Job curJob = pawn.CurJob;
		int prevQueue = ((pawn.jobs != null && pawn.jobs.jobQueue != null) ? pawn.jobs.jobQueue.Count : 0);
		try
		{
			val.action();
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("「使用」命令调用原版菜单项失败（物品已在地上）", exception);
			return false;
		}
		Job val3 = ResolveCreatedJob(pawn, curJob, prevQueue);
		if (val3 == null)
		{
			return true;
		}
		if (IsTaskJob(val3.def))
		{
			if (val3 == pawn.CurJob)
			{
				pawn.jobs.EndCurrentJob((JobCondition)16, true, true);
			}
			Refund(landed, map, ((Thing)pawn).PositionHeld);
			MsgRaw(((object)TranslatorFormattedStringExtensions.Translate("SDS.Use.TaskJob", NamedArgument.op_Implicit(((Entity)landed).LabelShort), NamedArgument.op_Implicit(((Def)val3.def).label))/*cast due to constrained. prefix*/).ToString());
			return false;
		}
		if (!IsKnownUseJob(val3.def))
		{
			Log.WarningOnce("[超维存储] 【使用命令】未知使用类 JobDef：" + ((Def)val3.def).defName + "（已按使用处理；如需排除请更新《用得上.md》§五）", (int)((Def)val3.def).shortHash);
		}
		return true;
	}

	private static List<FloatMenuOption> CollectOptions(FloatMenuOptionProvider provider, Thing thing, FloatMenuContext context)
	{
		FloatMenuMakerMap.currentProvider = provider;
		try
		{
			bool flag = false;
			using (IEnumerator<Pawn> enumerator = context.ValidSelectedPawns.GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					Pawn current = enumerator.Current;
					flag = true;
				}
			}
			if (!flag || !provider.Applies(context))
			{
				return null;
			}
			if (!provider.TargetThingValid(thing, context))
			{
				return null;
			}
			return new List<FloatMenuOption>(provider.GetOptionsFor(thing, context));
		}
		catch (Exception exception)
		{
			SDS_Diag.ErrorOnce("「使用命令」provider 求值失败（已跳过）：" + ((object)provider).GetType().Name, exception);
			return null;
		}
		finally
		{
			FloatMenuMakerMap.currentProvider = null;
		}
	}

	private static Job ResolveCreatedJob(Pawn pawn, Job prevCur, int prevQueue)
	{
		if (pawn == null || pawn.jobs == null)
		{
			return null;
		}
		if (pawn.CurJob != null && pawn.CurJob != prevCur)
		{
			return pawn.CurJob;
		}
		if (pawn.jobs.jobQueue != null && pawn.jobs.jobQueue.Count > prevQueue)
		{
			return pawn.jobs.jobQueue.Peek()?.job;
		}
		return null;
	}

	private static bool IsValidTarget(Pawn pawn)
	{
		return pawn != null && ((Thing)pawn).Spawned && ((Thing)pawn).Map != null && !pawn.Dead && ((Thing)pawn).Faction == Faction.OfPlayer && pawn.RaceProps != null && pawn.RaceProps.Humanlike && !pawn.RaceProps.IsMechanoid && !pawn.InMentalState;
	}

	private static bool IsUsableCategory(ThingDef def)
	{
		if (def == null)
		{
			return false;
		}
		if (def.ingestible != null && def.ingestible.showIngestFloatOption)
		{
			return true;
		}
		if (def.IsApparel)
		{
			return true;
		}
		if (def.HasAssignableCompFrom(typeof(CompEquippable)))
		{
			return true;
		}
		if (def.HasAssignableCompFrom(typeof(CompUsable)))
		{
			return true;
		}
		return def.thingClass != null && typeof(IOpenable).IsAssignableFrom(def.thingClass);
	}

	private static bool IsTaskJob(JobDef def)
	{
		if (def == null || ((Def)def).defName == null)
		{
			return false;
		}
		if (SDS_JobDefOf.DepositToGrid != null && def == SDS_JobDefOf.DepositToGrid)
		{
			return true;
		}
		for (int i = 0; i < TaskJobPrefixes.Length; i++)
		{
			if (((Def)def).defName.StartsWith(TaskJobPrefixes[i], StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsKnownUseJob(JobDef def)
	{
		if (def == null)
		{
			return false;
		}
		return def == JobDefOf.Ingest || def == JobDefOf.Wear || def == JobDefOf.Equip || def == JobDefOf.Open || string.Equals(((Def)def).defName, "UseItem", StringComparison.Ordinal);
	}

	private static void Refund(Thing thing, Map map, IntVec3 cell)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (thing != null && !thing.Destroyed)
		{
			SDS_TakeService.ReturnToGridOrDrop(thing, map, cell);
			if (thing.Spawned)
			{
				MsgRaw(((object)TranslatorFormattedStringExtensions.Translate("SDS.Use.RefundFailed", NamedArgument.op_Implicit(((Entity)thing).LabelShort))/*cast due to constrained. prefix*/).ToString());
			}
		}
	}

	private static void MsgKey(string key)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		Messages.Message(TaggedString.op_Implicit(Translator.Translate(key)), MessageTypeDefOf.RejectInput, false);
	}

	private static void MsgRaw(string text)
	{
		Messages.Message(text, MessageTypeDefOf.RejectInput, false);
	}

	private static List<FloatMenuOptionProvider> EnsureProviders()
	{
		if (providers != null)
		{
			return providers;
		}
		providers = new List<FloatMenuOptionProvider>();
		IEnumerable<Type> enumerable = GenTypes.AllSubclassesNonAbstract(typeof(FloatMenuOptionProvider));
		if (enumerable == null)
		{
			return providers;
		}
		foreach (Type item in enumerable)
		{
			if (item == null || ExcludedProviderNames.Contains(item.Name))
			{
				continue;
			}
			try
			{
				object? obj = Activator.CreateInstance(item);
				FloatMenuOptionProvider val = (FloatMenuOptionProvider)((obj is FloatMenuOptionProvider) ? obj : null);
				if (val != null)
				{
					providers.Add(val);
				}
			}
			catch (Exception exception)
			{
				SDS_Diag.ErrorOnce("「使用命令」provider 实例化失败（已跳过）：" + item.FullName, exception);
			}
		}
		providers.Sort((FloatMenuOptionProvider a, FloatMenuOptionProvider b) => ProviderRank(a).CompareTo(ProviderRank(b)));
		return providers;
	}

	private static int ProviderRank(FloatMenuOptionProvider provider)
	{
		return ((provider != null) ? ((object)provider).GetType().Name : string.Empty) switch
		{
			"FloatMenuOptionProvider_Wear" => 0, 
			"FloatMenuOptionProvider_Equip" => 1, 
			"FloatMenuOptionProvider_Ingest" => 2, 
			"FloatMenuOptionProvider_UseItem" => 3, 
			"FloatMenuOptionProvider_Open" => 4, 
			"FloatMenuOptionProvider_FromThing" => 5, 
			_ => 10, 
		};
	}
}
internal static class SDS_VirtualDisplayRegistry
{
	public static readonly object TradeOwner = new object();

	public static readonly object CaravanOwner = new object();

	public static readonly object TransportOwner = new object();

	private static readonly Dictionary<Thing, HashSet<object>> owners = new Dictionary<Thing, HashSet<object>>();

	private static readonly List<Thing> stale = new List<Thing>();

	public static void Register(Thing display, object owner)
	{
		if (display != null && !display.Destroyed && owner != null)
		{
			if (!owners.TryGetValue(display, out var value) || value == null)
			{
				value = new HashSet<object>();
				owners[display] = value;
			}
			value.Add(owner);
		}
	}

	public static bool IsFake(Thing thing)
	{
		return thing != null && owners.ContainsKey(thing);
	}

	public static void Forget(Thing display, object owner)
	{
		if (display != null && owner != null && owners.TryGetValue(display, out var value) && value != null)
		{
			value.Remove(owner);
			if (value.Count == 0)
			{
				owners.Remove(display);
			}
		}
	}

	public static void Cleanup(object owner)
	{
		if (owner == null || owners.Count == 0)
		{
			return;
		}
		stale.Clear();
		foreach (KeyValuePair<Thing, HashSet<object>> owner2 in owners)
		{
			if (owner2.Value != null && owner2.Value.Contains(owner))
			{
				stale.Add(owner2.Key);
			}
		}
		for (int i = 0; i < stale.Count; i++)
		{
			Forget(stale[i], owner);
		}
		stale.Clear();
	}

	public static void Reset()
	{
		owners.Clear();
		stale.Clear();
	}
}
public enum SDS_WithdrawMode
{
	Hand,
	Container,
	SpawnedTarget,
	VirtualHandoff
}
public enum SDS_SupplyFailure
{
	None,
	UnresolvedDef,
	Forbidden,
	Insufficient,
	RebuildFailed
}
public sealed class SDS_ReclaimSave : IExposable
{
	public SDS_ReclaimPolicy policy;

	public int jobLoadID;

	public int ownerLoadID;

	public int tick;

	public bool hand;

	public List<Thing> things = new List<Thing>();

	public List<int> counts = new List<int>();

	public void ExposeData()
	{
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Invalid comparison between Unknown and I4
		Scribe_Values.Look<SDS_ReclaimPolicy>(ref policy, "policy", SDS_ReclaimPolicy.JobTarget, false);
		Scribe_Values.Look<int>(ref jobLoadID, "jobLoadID", 0, false);
		Scribe_Values.Look<int>(ref ownerLoadID, "ownerLoadID", 0, false);
		Scribe_Values.Look<int>(ref tick, "tick", 0, false);
		Scribe_Values.Look<bool>(ref hand, "hand", false, false);
		Scribe_Collections.Look<Thing>(ref things, "things", (LookMode)3, Array.Empty<object>());
		Scribe_Collections.Look<int>(ref counts, "counts", (LookMode)1, Array.Empty<object>());
		if ((int)Scribe.mode == 4)
		{
			if (things == null)
			{
				things = new List<Thing>();
			}
			if (counts == null)
			{
				counts = new List<int>();
			}
			while (counts.Count < things.Count)
			{
				counts.Add(1);
			}
		}
	}
}
internal static class SDS_WithdrawSave
{
	public static void AuditRestored(SDS_GameComponent_Grid grid)
	{
		if (grid != null)
		{
			grid.AuditReservationsAfterLoad();
			SDS_ReclaimRegistry.AuditRestored();
		}
	}
}
public enum SDS_WithdrawEndState
{
	None,
	Reserved,
	Materialized,
	Delivered,
	RolledBack
}
public enum SDS_ReclaimOutcome
{
	None,
	Returned,
	Consumed,
	Abandoned
}
public class SDS_WithdrawDeliveryPolicyDef : Def
{
	public string jobDefName;

	public string driverType;

	public SDS_WithdrawMode mode = SDS_WithdrawMode.SpawnedTarget;
}
public static class SDS_WithdrawDeliveryPolicy
{
	private static Dictionary<string, SDS_WithdrawMode> byJob;

	private static Dictionary<string, SDS_WithdrawMode> byDriver;

	public static SDS_WithdrawMode ModeFor(Job job)
	{
		if (job?.def == null)
		{
			return SDS_WithdrawMode.SpawnedTarget;
		}
		EnsureLoaded();
		if (byJob != null && byJob.TryGetValue(((Def)job.def).defName, out var value))
		{
			return value;
		}
		Type driverClass = job.def.driverClass;
		if (driverClass != null && byDriver != null && (byDriver.TryGetValue(driverClass.Name, out value) || byDriver.TryGetValue(driverClass.FullName ?? string.Empty, out value)))
		{
			return value;
		}
		return SDS_WithdrawMode.SpawnedTarget;
	}

	private static void EnsureLoaded()
	{
		if (byJob != null)
		{
			return;
		}
		byJob = new Dictionary<string, SDS_WithdrawMode>();
		byDriver = new Dictionary<string, SDS_WithdrawMode>();
		List<SDS_WithdrawDeliveryPolicyDef> allDefsListForReading = DefDatabase<SDS_WithdrawDeliveryPolicyDef>.AllDefsListForReading;
		if (allDefsListForReading == null)
		{
			return;
		}
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			SDS_WithdrawDeliveryPolicyDef sDS_WithdrawDeliveryPolicyDef = allDefsListForReading[i];
			if (sDS_WithdrawDeliveryPolicyDef != null && sDS_WithdrawDeliveryPolicyDef.mode != SDS_WithdrawMode.VirtualHandoff)
			{
				if (!string.IsNullOrEmpty(sDS_WithdrawDeliveryPolicyDef.jobDefName))
				{
					byJob[sDS_WithdrawDeliveryPolicyDef.jobDefName] = sDS_WithdrawDeliveryPolicyDef.mode;
				}
				if (!string.IsNullOrEmpty(sDS_WithdrawDeliveryPolicyDef.driverType))
				{
					byDriver[sDS_WithdrawDeliveryPolicyDef.driverType] = sDS_WithdrawDeliveryPolicyDef.mode;
				}
			}
		}
	}
}
public sealed class SDS_WithdrawQuery
{
	public SDS_StockKey key;

	public int quantity;

	public bool preserved;

	public IntVec3 referenceCell = IntVec3.Invalid;

	public SDS_WithdrawMode? preferredMode;
}
public sealed class SDS_WithdrawPlan
{
	public SDS_WithdrawQuery query;

	public SDS_EndpointHandle endpoint;

	public SDS_WithdrawMode mode;

	public SDS_StockKey Key => query?.key;

	public int Quantity => query?.quantity ?? 0;

	public bool Preserved => query?.preserved ?? false;
}
public struct SDS_ReadVersions
{
	public int tick;

	public int mirror;

	public int endpoint;

	public int stock;

	public int reservation;

	public int filter;

	public static SDS_ReadVersions Capture()
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		return new SDS_ReadVersions
		{
			tick = ((Find.TickManager != null) ? Find.TickManager.TicksGame : 0),
			mirror = SDS_MirrorService.Version,
			endpoint = SDS_EndpointContractCache.Version,
			stock = (instance?.StockVersion ?? 0),
			reservation = (instance?.ReservationVersion ?? 0),
			filter = (instance?.FilterVersion ?? 0)
		};
	}

	public bool SameExposureAs(in SDS_ReadVersions other)
	{
		return mirror == other.mirror && endpoint == other.endpoint && stock == other.stock && filter == other.filter;
	}
}
public static class SDS_Availability
{
	public static long GetAvailableCount(SDS_StockKey key)
	{
		return GetAvailableCount(SDS_GameComponent_Grid.Instance, key);
	}

	public static SDS_SupplyFailure GetFailure(SDS_StockKey key)
	{
		if (key == null || !key.Resolved)
		{
			return SDS_SupplyFailure.UnresolvedDef;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null)
		{
			return SDS_SupplyFailure.Insufficient;
		}
		if (!instance.Available)
		{
			return SDS_SupplyFailure.Insufficient;
		}
		if (instance.IsForbidden(key))
		{
			return SDS_SupplyFailure.Forbidden;
		}
		return (instance.GetAvailableCount(key) <= 0) ? SDS_SupplyFailure.Insufficient : SDS_SupplyFailure.None;
	}

	public static long GetAvailableCount(SDS_GameComponent_Grid grid, SDS_StockKey key)
	{
		if (grid == null || key == null)
		{
			return 0L;
		}
		if (!grid.Available)
		{
			return 0L;
		}
		if (grid.IsForbidden(key))
		{
			return 0L;
		}
		return grid.GetAvailableCount(key);
	}

	public static long GetTradableCount(SDS_GameComponent_Grid grid, SDS_StockKey key)
	{
		if (grid == null || key == null)
		{
			return 0L;
		}
		if (!grid.Available)
		{
			return 0L;
		}
		return grid.GetAvailableCount(key);
	}

	public static long GetAvailableCount(SDS_GameComponent_Grid grid, ThingDef def)
	{
		if (grid == null || def == null)
		{
			return 0L;
		}
		if (!grid.Available)
		{
			return 0L;
		}
		return grid.GetAvailableCount(def);
	}

	public static bool HasAvailable(SDS_StockKey key, int quantity)
	{
		if (key == null || quantity <= 0)
		{
			return false;
		}
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || !instance.Available)
		{
			return false;
		}
		return instance.GetAvailableCount(key) >= quantity;
	}
}
public static class SDS_WithdrawResolver
{
	public static SDS_WithdrawQuery MakeQuery(SDS_StockKey key, int quantity, bool preserved, IntVec3 referenceCell, SDS_WithdrawMode? preferredMode = null)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		if (key == null || quantity <= 0)
		{
			return null;
		}
		return new SDS_WithdrawQuery
		{
			key = key,
			quantity = quantity,
			preserved = (preserved || key.preserved),
			referenceCell = referenceCell,
			preferredMode = preferredMode
		};
	}

	public static bool HasAvailable(SDS_WithdrawQuery query)
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (query?.key == null || query.quantity <= 0 || instance == null || !instance.Available)
		{
			return false;
		}
		return SDS_Availability.HasAvailable(query.key, query.quantity);
	}

	public static SDS_WithdrawPlan Decide(SDS_WithdrawQuery query, Pawn actor, Job job = null)
	{
		if (!HasAvailable(query))
		{
			return null;
		}
		return DecideEndpointPlan(query, actor, job);
	}

	public static SDS_WithdrawPlan DecideEndpointPlan(SDS_WithdrawQuery query, Pawn actor, Job job = null)
	{
		if (query == null || !TryDecideEndpoint(actor, out var endpoint))
		{
			return null;
		}
		return new SDS_WithdrawPlan
		{
			query = query,
			endpoint = endpoint,
			mode = ResolveMode(query, job)
		};
	}

	public static bool TryDecideEndpoint(Pawn actor, out SDS_EndpointHandle endpoint)
	{
		endpoint = null;
		if (actor == null || !((Thing)actor).Spawned || ((Thing)actor).Map == null)
		{
			return false;
		}
		SDS_WithdrawPoint sDS_WithdrawPoint = SDS_OutputUtility.FindWithdrawPoint(actor, ((Thing)actor).Map, forced: false);
		if (sDS_WithdrawPoint == null || sDS_WithdrawPoint.Endpoint == null)
		{
			return false;
		}
		if (sDS_WithdrawPoint.IsSelf)
		{
			endpoint = SDS_EndpointHandle.ForPawn(actor, self: true);
			return endpoint != null;
		}
		return SDS_EndpointHandle.TryFromThing(sDS_WithdrawPoint.Endpoint, out endpoint);
	}

	public static SDS_WithdrawMode ResolveMode(SDS_WithdrawQuery query, Job job)
	{
		if (query != null && query.preferredMode.HasValue)
		{
			return query.preferredMode.Value;
		}
		return (job != null) ? SDS_WithdrawDeliveryPolicy.ModeFor(job) : SDS_WithdrawMode.SpawnedTarget;
	}
}
public sealed class SDS_WithdrawTransaction
{
	private static int nextId = -2147483647;

	public int id;

	public Pawn actor;

	public List<SDS_WithdrawPlan> plans = new List<SDS_WithdrawPlan>();

	public List<Thing> materialized = new List<Thing>();

	public SDS_WithdrawEndState state;

	private bool materializedPreserved;

	private SDS_GameComponent_Grid grid;

	public static SDS_WithdrawTransaction BeginSingle(SDS_StockKey key, int quantity, bool preserved, Pawn actor, SDS_WithdrawMode mode = SDS_WithdrawMode.Hand)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		if (key == null || quantity <= 0)
		{
			SDS_Diag.Fail("取货/参数无效", SDS_Diag.Ctx(null, actor, null, null, "quantity=" + quantity + " preserved=" + preserved));
			return null;
		}
		SDS_WithdrawQuery sDS_WithdrawQuery = SDS_WithdrawResolver.MakeQuery(key, quantity, preserved, (actor != null) ? ((Thing)actor).Position : IntVec3.Invalid, mode);
		if (sDS_WithdrawQuery == null)
		{
			SDS_Diag.Fail("取货/查询构造失败", SDS_Diag.Ctx(null, actor, null, null, "def=" + ((key.thingDef != null) ? ((Def)key.thingDef).defName : "?") + " quantity=" + quantity));
			return null;
		}
		SDS_WithdrawPlan item = new SDS_WithdrawPlan
		{
			query = sDS_WithdrawQuery,
			endpoint = null,
			mode = mode
		};
		return Begin(new List<SDS_WithdrawPlan> { item }, actor);
	}

	public static SDS_WithdrawTransaction Begin(List<SDS_WithdrawPlan> plans, Pawn actor)
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || plans == null || plans.Count == 0)
		{
			SDS_Diag.Fail("取货/事务参数无效", SDS_Diag.Ctx(null, actor, null, null, "plans=" + (plans?.Count ?? (-1))));
			return null;
		}
		List<SDS_StockKey> list = new List<SDS_StockKey>();
		List<int> list2 = new List<int>();
		for (int i = 0; i < plans.Count; i++)
		{
			SDS_WithdrawPlan sDS_WithdrawPlan = plans[i];
			if (sDS_WithdrawPlan?.query?.key == null || sDS_WithdrawPlan.Quantity <= 0)
			{
				SDS_Diag.Fail("取货/计划无效", SDS_Diag.Ctx(null, actor, null, null, "index=" + i));
				return null;
			}
			if (sDS_WithdrawPlan.mode == SDS_WithdrawMode.VirtualHandoff)
			{
				SDS_Diag.Fail("取货/交付模式不匹配", SDS_Diag.Ctx(null, actor, null, null, "index=" + i + " mode=VirtualHandoff"));
				return null;
			}
			list.Add(sDS_WithdrawPlan.query.key);
			list2.Add(sDS_WithdrawPlan.Quantity);
		}
		int transactionId = NextId();
		if (!instance.TryReserveWithdrawCounts(transactionId, list, list2, actor))
		{
			return null;
		}
		return new SDS_WithdrawTransaction
		{
			id = transactionId,
			actor = actor,
			plans = new List<SDS_WithdrawPlan>(plans),
			state = SDS_WithdrawEndState.Reserved,
			grid = instance
		};
	}

	public static SDS_WithdrawTransaction BeginJobAdopt(int reservationId, List<SDS_WithdrawPlan> plans, Pawn actor)
	{
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || reservationId == 0 || plans == null || plans.Count == 0)
		{
			SDS_Diag.Fail("取货/接管参数无效", SDS_Diag.Ctx(null, actor, null, null, "reservation=" + reservationId + " plans=" + (plans?.Count ?? (-1))));
			return null;
		}
		Dictionary<SDS_StockKey, int> dictionary = new Dictionary<SDS_StockKey, int>();
		for (int i = 0; i < plans.Count; i++)
		{
			SDS_WithdrawPlan sDS_WithdrawPlan = plans[i];
			if (sDS_WithdrawPlan?.query?.key == null || sDS_WithdrawPlan.Quantity <= 0)
			{
				SDS_Diag.Fail("取货/接管计划无效", SDS_Diag.Ctx(null, actor, null, null, "index=" + i));
				return null;
			}
			if (sDS_WithdrawPlan.mode == SDS_WithdrawMode.VirtualHandoff)
			{
				SDS_Diag.Fail("取货/接管模式不匹配", SDS_Diag.Ctx(null, actor, null, null, "index=" + i + " mode=VirtualHandoff"));
				return null;
			}
			dictionary.TryGetValue(sDS_WithdrawPlan.query.key, out var value);
			dictionary[sDS_WithdrawPlan.query.key] = value + sDS_WithdrawPlan.Quantity;
		}
		foreach (KeyValuePair<SDS_StockKey, int> item in dictionary)
		{
			long reservedCount = instance.GetReservedCount(reservationId, item.Key);
			if (reservedCount < item.Value)
			{
				return null;
			}
		}
		return new SDS_WithdrawTransaction
		{
			id = reservationId,
			actor = actor,
			plans = new List<SDS_WithdrawPlan>(plans),
			state = SDS_WithdrawEndState.Reserved,
			grid = instance
		};
	}

	public bool ExecuteVirtual(out List<Thing> made, bool requireFullQuantity = false)
	{
		made = null;
		if (RejectVirtualHandoff("ExecuteVirtual"))
		{
			return false;
		}
		if (grid == null || plans.Count == 0)
		{
			SDS_Diag.Fail("取货/执行参数无效", SDS_Diag.Ctx(null, actor, null, null, "plans=" + plans.Count));
			return false;
		}
		SDS_Invariants.TransactionStage(this, SDS_WithdrawEndState.Reserved, "ExecuteVirtual");
		materializedPreserved = false;
		List<Thing> list = new List<Thing>();
		for (int i = 0; i < plans.Count; i++)
		{
			SDS_WithdrawPlan sDS_WithdrawPlan = plans[i];
			if (sDS_WithdrawPlan.Preserved)
			{
				SDS_Diag.Fail("取货/通道不匹配", SDS_Diag.Ctx(null, actor, null, null, "index=" + i + " 期望虚拟通道"));
				RollbackMaterialized(list);
				return false;
			}
			if (!grid.TryWithdraw(sDS_WithdrawPlan.Key, sDS_WithdrawPlan.Quantity, out var result) || result == null || result.Count == 0)
			{
				RollbackMaterialized(list);
				return false;
			}
			int num = CountMaterializedUnits(result);
			grid.ConsumeReservation(id, sDS_WithdrawPlan.Key, num);
			list.AddRange(result);
			if (requireFullQuantity && num < sDS_WithdrawPlan.Quantity)
			{
				SDS_Diag.Fail("取货/足额校验失败（执行期可用量不足，整笔回滚）", SDS_Diag.Ctx((actor != null) ? ((Thing)actor).Map : null, actor, null, null, "key=" + ((sDS_WithdrawPlan.Key != null && sDS_WithdrawPlan.Key.thingDef != null) ? ((Def)sDS_WithdrawPlan.Key.thingDef).defName : "?") + " want=" + sDS_WithdrawPlan.Quantity + " got=" + num));
				RollbackMaterialized(list);
				return false;
			}
		}
		materialized = list;
		state = SDS_WithdrawEndState.Materialized;
		made = list;
		return true;
	}

	public bool ExecuteVirtualReserved(out List<Thing> made)
	{
		made = null;
		if (RejectVirtualHandoff("ExecuteVirtualReserved"))
		{
			return false;
		}
		if (grid == null || plans.Count == 0)
		{
			return false;
		}
		SDS_Invariants.TransactionStage(this, SDS_WithdrawEndState.Reserved, "ExecuteVirtualReserved");
		materializedPreserved = false;
		List<Thing> list = new List<Thing>();
		for (int i = 0; i < plans.Count; i++)
		{
			SDS_WithdrawPlan sDS_WithdrawPlan = plans[i];
			if (sDS_WithdrawPlan.Preserved)
			{
				RollbackMaterialized(list);
				return false;
			}
			if (grid.GetReservedCount(id, sDS_WithdrawPlan.Key) < sDS_WithdrawPlan.Quantity)
			{
				RollbackMaterialized(list);
				return false;
			}
			if (!grid.TryWithdraw(sDS_WithdrawPlan.Key, sDS_WithdrawPlan.Quantity, out var result) || result == null || result.Count == 0)
			{
				RollbackMaterialized(list);
				return false;
			}
			int count = CountMaterializedUnits(result);
			grid.ConsumeReservation(id, sDS_WithdrawPlan.Key, count);
			list.AddRange(result);
		}
		materialized = list;
		state = SDS_WithdrawEndState.Materialized;
		made = list;
		return true;
	}

	public bool ExecutePreserved(Predicate<Thing> validator, out List<Thing> made)
	{
		made = null;
		if (RejectVirtualHandoff("ExecutePreserved"))
		{
			return false;
		}
		if (grid == null || plans.Count == 0)
		{
			SDS_Diag.Fail("取货/执行参数无效", SDS_Diag.Ctx(null, actor, null, null, "plans=" + plans.Count + " 保真通道"));
			return false;
		}
		SDS_Invariants.TransactionStage(this, SDS_WithdrawEndState.Reserved, "ExecutePreserved");
		materializedPreserved = true;
		List<Thing> list = new List<Thing>();
		for (int i = 0; i < plans.Count; i++)
		{
			SDS_WithdrawPlan sDS_WithdrawPlan = plans[i];
			if (!sDS_WithdrawPlan.Preserved)
			{
				SDS_Diag.Fail("取货/通道不匹配", SDS_Diag.Ctx(null, actor, null, null, "index=" + i + " 期望保真通道"));
				RollbackMaterialized(list);
				return false;
			}
			if (!grid.TryWithdrawPreservedWhere(sDS_WithdrawPlan.Key, sDS_WithdrawPlan.Quantity, validator, out var result) || result == null || result.Count == 0)
			{
				RollbackMaterialized(list);
				return false;
			}
			int num = CountMaterializedUnits(result);
			if (num < sDS_WithdrawPlan.Quantity)
			{
				RollbackMaterialized(result);
				RollbackMaterialized(list);
				return false;
			}
			grid.ConsumeReservation(id, sDS_WithdrawPlan.Key, sDS_WithdrawPlan.Quantity);
			list.AddRange(result);
		}
		materialized = list;
		state = SDS_WithdrawEndState.Materialized;
		made = list;
		return true;
	}

	public bool ExecutePreservedExact(List<ThingCount> plan, out List<Thing> made)
	{
		made = null;
		if (RejectVirtualHandoff("ExecutePreservedExact"))
		{
			return false;
		}
		if (grid == null || plan == null || plan.Count == 0)
		{
			SDS_Diag.Fail("取货/精确计划无效", SDS_Diag.Ctx(null, actor, null, null, "plan=" + (plan?.Count ?? (-1))));
			return false;
		}
		SDS_Invariants.TransactionStage(this, SDS_WithdrawEndState.Reserved, "ExecutePreservedExact");
		materializedPreserved = true;
		if (!grid.TryWithdrawPreservedPlan(id, plan, out made) || made == null || made.Count == 0)
		{
			SDS_Diag.Fail("取货/精确计划扣账失败", SDS_Diag.Ctx(null, actor, null, null, "reservation=" + id + " plan=" + plan.Count));
			made = null;
			return false;
		}
		materialized = made;
		state = SDS_WithdrawEndState.Materialized;
		return true;
	}

	public bool ExecuteMirror(Thing mirror, int quantity, out Thing taken)
	{
		taken = null;
		if (RejectVirtualHandoff("ExecuteMirror"))
		{
			return false;
		}
		if (mirror == null || quantity <= 0 || plans.Count != 1)
		{
			SDS_Diag.Fail("取货/镜像参数无效", SDS_Diag.Ctx((mirror != null) ? mirror.Map : null, actor, mirror, null, "quantity=" + quantity + " plans=" + plans.Count));
			return false;
		}
		SDS_Invariants.TransactionStage(this, SDS_WithdrawEndState.Reserved, "ExecuteMirror");
		materializedPreserved = false;
		if (!SDS_TakeService.TryTakeInternal(mirror, quantity, out taken) || taken == null)
		{
			return false;
		}
		SDS_StockKey sDS_StockKey = SDS_StockKey.FromThing(mirror);
		if (grid != null && sDS_StockKey != null)
		{
			grid.ConsumeReservation(id, sDS_StockKey, taken.stackCount);
		}
		materialized = new List<Thing> { taken };
		state = SDS_WithdrawEndState.Materialized;
		return true;
	}

	private bool RejectVirtualHandoff(string stage)
	{
		for (int i = 0; i < plans.Count; i++)
		{
			if (plans[i] != null && plans[i].mode == SDS_WithdrawMode.VirtualHandoff)
			{
				SDS_Diag.Fail("取货/交付模式不允许物化", SDS_Diag.Ctx((actor != null) ? ((Thing)actor).Map : null, actor, null, null, "stage=" + stage + " index=" + i));
				return true;
			}
		}
		return false;
	}

	public void Deliver()
	{
		SDS_Invariants.TransactionTerminal(this, "Deliver");
		if (state == SDS_WithdrawEndState.Materialized)
		{
			try
			{
				ReleaseReservation();
			}
			catch (Exception exception)
			{
				SDS_Diag.Fail("取货/预留释放失败", SDS_Diag.Ctx((actor != null) ? ((Thing)actor).Map : null, actor, null, null, "reservation=" + id), exception);
			}
			state = SDS_WithdrawEndState.Delivered;
		}
	}

	public void Rollback()
	{
		SDS_Invariants.TransactionTerminal(this, "Rollback");
		RollbackMaterialized(materialized);
		materialized.Clear();
		try
		{
			ReleaseReservation();
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/回滚预留释放失败", SDS_Diag.Ctx((actor != null) ? ((Thing)actor).Map : null, actor, null, null, "reservation=" + id), exception);
		}
		state = SDS_WithdrawEndState.RolledBack;
	}

	private void RollbackMaterialized(List<Thing> things)
	{
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if (things == null)
		{
			return;
		}
		Map val = ((actor != null) ? ((Thing)actor).Map : Find.CurrentMap);
		IntVec3 cell = ((actor != null) ? ((Thing)actor).Position : ((val != null) ? val.Center : IntVec3.Invalid));
		for (int i = 0; i < things.Count; i++)
		{
			Thing val2 = things[i];
			if (val2 == null || val2.Destroyed)
			{
				continue;
			}
			try
			{
				if (materializedPreserved)
				{
					SDS_ReturnUtility.ReturnPreserved(val2, val, cell);
				}
				else
				{
					SDS_ReturnUtility.ReturnVirtual(val2, SDS_StockKey.FromThing(val2), val, cell);
				}
			}
			catch (Exception exception)
			{
				SDS_Diag.Fail("取货/回滚失败（单件，已尝试兜底落地）", SDS_Diag.Ctx(val, actor, val2, null, "index=" + i + " reservation=" + id), exception);
				try
				{
					SDS_TakeService.ReturnToGridOrDrop(val2, val, cell);
				}
				catch (Exception exception2)
				{
					SDS_Diag.Fail("取货/回滚兜底落地失败", SDS_Diag.Ctx(val, actor, val2, null, "index=" + i), exception2);
				}
			}
		}
	}

	private static int CountMaterializedUnits(List<Thing> things)
	{
		if (things == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < things.Count; i++)
		{
			Thing val = things[i];
			if (val != null && !val.Destroyed)
			{
				num += val.stackCount;
			}
		}
		return num;
	}

	private void ReleaseReservation()
	{
		if (grid != null)
		{
			grid.ReleaseReservation(id);
		}
	}

	private static int NextId()
	{
		if (nextId == int.MaxValue)
		{
			nextId = -2147483647;
		}
		return nextId++;
	}
}
public static class SDS_WithdrawExecutor
{
	public static bool TryWithdrawVirtual(SDS_StockKey key, int quantity, out List<Thing> made, SDS_WithdrawMode mode = SDS_WithdrawMode.Hand, Pawn actor = null, bool requireFullQuantity = false)
	{
		made = null;
		try
		{
			SDS_WithdrawTransaction sDS_WithdrawTransaction = SDS_WithdrawTransaction.BeginSingle(key, quantity, preserved: false, actor, mode);
			if (sDS_WithdrawTransaction == null)
			{
				return false;
			}
			if (!sDS_WithdrawTransaction.ExecuteVirtual(out made, requireFullQuantity))
			{
				sDS_WithdrawTransaction.Rollback();
				return false;
			}
			sDS_WithdrawTransaction.Deliver();
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/执行器异常", "虚拟通道", exception);
			made = null;
			return false;
		}
	}

	public static bool TryWithdrawPreserved(SDS_StockKey key, int quantity, Predicate<Thing> validator, out List<Thing> made, SDS_WithdrawMode mode = SDS_WithdrawMode.Hand, Pawn actor = null)
	{
		made = null;
		try
		{
			SDS_WithdrawTransaction sDS_WithdrawTransaction = SDS_WithdrawTransaction.BeginSingle(key, quantity, preserved: true, actor, mode);
			if (sDS_WithdrawTransaction == null)
			{
				return false;
			}
			if (!sDS_WithdrawTransaction.ExecutePreserved(validator, out made))
			{
				sDS_WithdrawTransaction.Rollback();
				return false;
			}
			sDS_WithdrawTransaction.Deliver();
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/执行器异常", "保真通道", exception);
			made = null;
			return false;
		}
	}

	public static bool TryWithdrawPreservedExact(List<ThingCount> plan, out List<Thing> made, SDS_WithdrawMode mode = SDS_WithdrawMode.Container, Pawn actor = null)
	{
		made = null;
		try
		{
			List<SDS_WithdrawPlan> list = BuildExactPreservedPlans(plan, mode);
			if (list == null)
			{
				return false;
			}
			SDS_WithdrawTransaction sDS_WithdrawTransaction = SDS_WithdrawTransaction.Begin(list, actor);
			if (sDS_WithdrawTransaction == null)
			{
				return false;
			}
			if (!sDS_WithdrawTransaction.ExecutePreservedExact(plan, out made))
			{
				sDS_WithdrawTransaction.Rollback();
				return false;
			}
			sDS_WithdrawTransaction.Deliver();
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/执行器异常", "精确保真通道", exception);
			made = null;
			return false;
		}
	}

	public static bool TryWithdrawPreservedExactAdopted(int reservationId, List<ThingCount> plan, out List<Thing> made, SDS_WithdrawMode mode = SDS_WithdrawMode.Hand)
	{
		made = null;
		try
		{
			List<SDS_WithdrawPlan> list = BuildExactPreservedPlans(plan, mode);
			if (list == null)
			{
				return false;
			}
			SDS_WithdrawTransaction sDS_WithdrawTransaction = SDS_WithdrawTransaction.BeginJobAdopt(reservationId, list, null);
			if (sDS_WithdrawTransaction == null)
			{
				return false;
			}
			if (!sDS_WithdrawTransaction.ExecutePreservedExact(plan, out made))
			{
				return false;
			}
			sDS_WithdrawTransaction.Deliver();
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/执行器异常", "接管预留守卫通道", exception);
			made = null;
			return false;
		}
	}

	public static bool TryWithdrawReservedForJob(int reservationId, Pawn pawn)
	{
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		SDS_GameComponent_Grid instance = SDS_GameComponent_Grid.Instance;
		if (instance == null || reservationId == 0 || pawn == null || ((Thing)pawn).Map == null || pawn.carryTracker == null)
		{
			SDS_Diag.Fail("取货/Job预留参数无效", SDS_Diag.Ctx((pawn != null) ? ((Thing)pawn).Map : null, pawn, null, null, "reservation=" + reservationId));
			return false;
		}
		if (!instance.TryGetJobReservation(reservationId, out var key, out var total) || key == null || key.preserved)
		{
			return false;
		}
		int num = (int)Math.Min((long)total, (long)pawn.carryTracker.MaxStackSpaceEver(key.thingDef));
		if (num <= 0)
		{
			return false;
		}
		SDS_WithdrawQuery sDS_WithdrawQuery = SDS_WithdrawResolver.MakeQuery(key, num, preserved: false, ((Thing)pawn).Position, SDS_WithdrawMode.Hand);
		if (sDS_WithdrawQuery == null)
		{
			return false;
		}
		SDS_WithdrawPlan item = new SDS_WithdrawPlan
		{
			query = sDS_WithdrawQuery,
			endpoint = null,
			mode = SDS_WithdrawMode.Hand
		};
		SDS_WithdrawTransaction sDS_WithdrawTransaction = SDS_WithdrawTransaction.BeginJobAdopt(reservationId, new List<SDS_WithdrawPlan> { item }, pawn);
		if (sDS_WithdrawTransaction == null)
		{
			return false;
		}
		try
		{
			if (!sDS_WithdrawTransaction.ExecuteVirtualReserved(out var made) || made == null || made.Count == 0)
			{
				sDS_WithdrawTransaction.Rollback();
				return false;
			}
			bool result = false;
			for (int i = 0; i < made.Count; i++)
			{
				Thing val = made[i];
				if (val != null && !val.Destroyed)
				{
					if (pawn.carryTracker.TryStartCarry(val))
					{
						result = true;
					}
					else
					{
						SDS_PlacementUtility.TryPlaceOrSpawn(val, ((Thing)pawn).Position, ((Thing)pawn).Map);
					}
				}
			}
			sDS_WithdrawTransaction.Deliver();
			return result;
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/执行器异常", SDS_Diag.Ctx(((Thing)pawn).Map, pawn, null, null, "reservation=" + reservationId), exception);
			sDS_WithdrawTransaction.Rollback();
			return false;
		}
	}

	private static List<SDS_WithdrawPlan> BuildExactPreservedPlans(List<ThingCount> plan, SDS_WithdrawMode mode)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		if (plan == null || plan.Count == 0)
		{
			SDS_Diag.Fail("取货/精确计划为空");
			return null;
		}
		Dictionary<SDS_StockKey, int> dictionary = new Dictionary<SDS_StockKey, int>();
		for (int i = 0; i < plan.Count; i++)
		{
			ThingCount val = plan[i];
			Thing thing = ((ThingCount)(ref val)).Thing;
			val = plan[i];
			int count = ((ThingCount)(ref val)).Count;
			if (thing == null || thing.Destroyed || count <= 0)
			{
				SDS_Diag.Fail("取货/精确计划条目无效", SDS_Diag.Ctx((thing != null) ? thing.MapHeld : null, null, thing, null, "index=" + i + " count=" + count));
				return null;
			}
			SDS_StockKey sDS_StockKey = SDS_StockKey.FromPreservedThing(thing);
			if (sDS_StockKey == null || !sDS_StockKey.preserved)
			{
				SDS_Diag.Fail("取货/非保真条目进入保真通道", SDS_Diag.Ctx(thing.MapHeld, null, thing, null, "index=" + i));
				return null;
			}
			dictionary.TryGetValue(sDS_StockKey, out var value);
			dictionary[sDS_StockKey] = value + count;
		}
		List<SDS_WithdrawPlan> list = new List<SDS_WithdrawPlan>();
		foreach (KeyValuePair<SDS_StockKey, int> item in dictionary)
		{
			SDS_WithdrawQuery sDS_WithdrawQuery = SDS_WithdrawResolver.MakeQuery(item.Key, item.Value, preserved: true, IntVec3.Invalid, mode);
			if (sDS_WithdrawQuery == null)
			{
				SDS_Diag.Fail("取货/查询构造失败", "def=" + ((item.Key.thingDef != null) ? ((Def)item.Key.thingDef).defName : "?") + " quantity=" + item.Value);
				return null;
			}
			list.Add(new SDS_WithdrawPlan
			{
				query = sDS_WithdrawQuery,
				endpoint = null,
				mode = mode
			});
		}
		return list;
	}

	public static bool TryTakeMirror(Thing mirror, int quantity, out Thing taken, SDS_WithdrawMode mode = SDS_WithdrawMode.Hand, Pawn actor = null)
	{
		taken = null;
		if (mirror == null || quantity <= 0)
		{
			SDS_Diag.Fail("取货/镜像参数无效", SDS_Diag.Ctx((mirror != null) ? mirror.Map : null, null, mirror, null, "quantity=" + quantity));
			return false;
		}
		SDS_StockKey sDS_StockKey = SDS_StockKey.FromThing(mirror);
		if (sDS_StockKey == null || sDS_StockKey.preserved)
		{
			SDS_Diag.Fail("取货/镜像品类非法", SDS_Diag.Ctx(mirror.Map, null, mirror, null, "quantity=" + quantity));
			return false;
		}
		try
		{
			SDS_WithdrawTransaction sDS_WithdrawTransaction = SDS_WithdrawTransaction.BeginSingle(sDS_StockKey, quantity, preserved: false, actor, mode);
			if (sDS_WithdrawTransaction == null)
			{
				return false;
			}
			if (!sDS_WithdrawTransaction.ExecuteMirror(mirror, quantity, out taken))
			{
				sDS_WithdrawTransaction.Rollback();
				return false;
			}
			sDS_WithdrawTransaction.Deliver();
			return true;
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/执行器异常", SDS_Diag.Ctx(mirror.Map, null, mirror, null, "quantity=" + quantity), exception);
			taken = null;
			return false;
		}
	}

	public static bool TryConsumeMirror(Thing mirror, out SDS_StockKey key, out int consumed)
	{
		key = null;
		consumed = 0;
		try
		{
			return SDS_TakeService.TryConsumeInternal(mirror, out key, out consumed);
		}
		catch (Exception exception)
		{
			SDS_Diag.Fail("取货/销毁扣账异常", SDS_Diag.Ctx((mirror != null) ? mirror.Map : null, null, mirror), exception);
			key = null;
			consumed = 0;
			return false;
		}
	}
}
