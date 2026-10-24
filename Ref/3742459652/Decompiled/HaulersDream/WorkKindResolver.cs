using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class WorkKindResolver
{
	[ThreadStatic]
	private static int resolveCacheTick;

	[ThreadStatic]
	private static Dictionary<long, RouteWorkKind> resolveCache;

	private static WorkGiverDef minerDef;

	private static bool minerResolved;

	private static readonly Dictionary<Type, WorkGiverDef> wgByClass;

	private static WorkGiverDef plantsCutDef;

	private static bool plantsCutResolved;

	static WorkKindResolver()
	{
		wgByClass = new Dictionary<Type, WorkGiverDef>();
		CacheRegistry.Register(ClearResolveCache);
	}

	public static void ClearResolveCache()
	{
		resolveCache?.Clear();
		resolveCacheTick = -1;
	}

	public static RouteWorkKind Resolve(Pawn pawn, Thing clicked)
	{
		if (pawn == null || clicked == null)
		{
			return null;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : (-1));
		if (num == -1)
		{
			return ResolveUncached(pawn, clicked);
		}
		Dictionary<long, RouteWorkKind> dictionary = resolveCache ?? (resolveCache = new Dictionary<long, RouteWorkKind>());
		if (num != resolveCacheTick)
		{
			dictionary.Clear();
			resolveCacheTick = num;
		}
		long key = ((long)((Thing)pawn).thingIDNumber << 32) | (uint)clicked.thingIDNumber;
		if (dictionary.TryGetValue(key, out var value))
		{
			return value;
		}
		return dictionary[key] = ResolveUncached(pawn, clicked);
	}

	private static RouteWorkKind ResolveUncached(Pawn pawn, Thing clicked)
	{
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || clicked == null || !clicked.Spawned || clicked.Map != ((Thing)pawn).Map)
		{
			return null;
		}
		Pawn_Thinker thinker = pawn.thinker;
		if (((thinker != null) ? thinker.TryGetMainTreeThinkNode<JobGiver_Work>() : null) == null)
		{
			return null;
		}
		RouteWorkKind routeWorkKind = TryResolveConstruction(pawn, clicked);
		if (routeWorkKind != null)
		{
			return routeWorkKind;
		}
		List<WorkTypeDef> allDefsListForReading = DefDatabase<WorkTypeDef>.AllDefsListForReading;
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			List<WorkGiverDef> workGiversByPriority = allDefsListForReading[i].workGiversByPriority;
			for (int j = 0; j < workGiversByPriority.Count; j++)
			{
				WorkGiverDef val = workGiversByPriority[j];
				if (!val.directOrderable || (pawn.Drafted && !val.canBeDoneWhileDrafted) || (val.workType != null && WorkCapabilityProbe.IsDisabled(pawn, val.workType)))
				{
					continue;
				}
				WorkGiver worker = val.Worker;
				WorkGiver_Scanner val2 = (WorkGiver_Scanner)(object)((worker is WorkGiver_Scanner) ? worker : null);
				if (val2 == null || val2 is WorkGiver_DoBill || val2 is WorkGiver_Researcher)
				{
					continue;
				}
				string gerund;
				try
				{
					if (!ScannerSkips(pawn, val2, clicked) && val2.HasJobOnThing(pawn, clicked, true))
					{
						Job val3 = val2.JobOnThing(pawn, clicked, true);
						if (val3 != null)
						{
							gerund = val2.PostProcessedGerund(val3);
							goto IL_0150;
						}
					}
				}
				catch (Exception arg)
				{
					HDLog.Err("WorkGiver '" + ((Def)val).defName + "' threw while resolving a route " + $"for {clicked} — skipping it (report to that mod's author): {arg}");
				}
				continue;
				IL_0150:
				DesignationDef designation = DetermineDesignation(((Thing)pawn).Map, clicked);
				return new RouteWorkKind
				{
					scanner = val2,
					designation = designation,
					gerund = gerund,
					yieldDef = ResolveYield(clicked),
					scope = ScopeFor(val2, designation)
				};
			}
		}
		return TryResolveUnmarkedHarvest(pawn, clicked);
	}

	public static string WorkKindId(RouteWorkKind kind)
	{
		return ((Def)(((WorkGiver)(kind?.scanner?)).def?)).defName;
	}

	public static RouteWorkKind ResolveById(Pawn pawn, Thing clicked, string workGiverDefName)
	{
		if (string.IsNullOrEmpty(workGiverDefName))
		{
			return null;
		}
		RouteWorkKind routeWorkKind = Resolve(pawn, clicked);
		if (!(WorkKindId(routeWorkKind) == workGiverDefName))
		{
			return null;
		}
		return routeWorkKind;
	}

	private static RouteWorkKind TryResolveUnmarkedHarvest(Pawn pawn, Thing clicked)
	{
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		Plant val = (Plant)(object)((clicked is Plant) ? clicked : null);
		if (val == null || ((Thing)val).def.plant == null)
		{
			return null;
		}
		if (((Thing)val).def.plant.harvestedThingDef == null || !val.HarvestableNow)
		{
			return null;
		}
		WorkGiverDef val2 = PlantsCutDef();
		if (val2 != null)
		{
			WorkGiver worker = val2.Worker;
			WorkGiver_Scanner val3 = (WorkGiver_Scanner)(object)((worker is WorkGiver_Scanner) ? worker : null);
			if (val3 != null)
			{
				if (WorkCapabilityProbe.IsDisabled(pawn, val2.workType))
				{
					return null;
				}
				if (FireUtility.IsBurning((Thing)(object)val) || !PlantUtility.PawnWillingToCutPlant_Job((Thing)(object)val, pawn) || !ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit((Thing)(object)val), 1, -1, (ReservationLayerDef)null, true) || !ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit((Thing)(object)val), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0))
				{
					return null;
				}
				return new RouteWorkKind
				{
					scanner = val3,
					designation = DesignationDefOf.HarvestPlant,
					gerund = TaggedString.op_Implicit(Translator.Translate("HarvestGerund")),
					yieldDef = ((Thing)val).def.plant.harvestedThingDef
				};
			}
		}
		return null;
	}

	public static RouteWorkKind MiningKind(ThingDef veinDef)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		WorkGiverDef val = MinerDef();
		if (val != null)
		{
			WorkGiver worker = val.Worker;
			WorkGiver_Scanner val2 = (WorkGiver_Scanner)(object)((worker is WorkGiver_Scanner) ? worker : null);
			if (val2 != null)
			{
				return new RouteWorkKind
				{
					scanner = val2,
					designation = DesignationDefOf.Mine,
					gerund = (val.gerund ?? TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.MiningGerundFallback"))),
					yieldDef = veinDef?.building?.mineableThing,
					scope = RouteTargetScope.SameDefOrDesignated
				};
			}
		}
		return null;
	}

	private static WorkGiverDef MinerDef()
	{
		if (minerResolved)
		{
			return minerDef;
		}
		minerResolved = true;
		List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			if (typeof(WorkGiver_Miner).IsAssignableFrom(allDefsListForReading[i].giverClass))
			{
				minerDef = allDefsListForReading[i];
				break;
			}
		}
		return minerDef;
	}

	private static RouteWorkKind TryResolveConstruction(Pawn pawn, Thing clicked)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		if (!clicked.def.IsBlueprint && !(clicked is Frame))
		{
			return null;
		}
		if (clicked.Faction != ((Thing)pawn).Faction)
		{
			return null;
		}
		if (WorkCapabilityProbe.IsDisabled(pawn, WorkTypeDefOf.Construction))
		{
			return null;
		}
		if (!ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit(clicked), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0))
		{
			return null;
		}
		WorkGiverDef val = WorkGiverOfClass(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints));
		if (val != null)
		{
			WorkGiver worker = val.Worker;
			WorkGiver_Scanner val2 = (WorkGiver_Scanner)(object)((worker is WorkGiver_Scanner) ? worker : null);
			if (val2 != null)
			{
				return new RouteWorkKind
				{
					scanner = val2,
					designation = null,
					gerund = TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ConstructGerund")),
					yieldDef = null,
					scope = RouteTargetScope.Constructible
				};
			}
		}
		return null;
	}

	private static WorkGiverDef WorkGiverOfClass(Type workGiverClass)
	{
		if (wgByClass.TryGetValue(workGiverClass, out var value))
		{
			return value;
		}
		WorkGiverDef val = null;
		List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			if (workGiverClass.IsAssignableFrom(allDefsListForReading[i].giverClass))
			{
				val = allDefsListForReading[i];
				break;
			}
		}
		wgByClass[workGiverClass] = val;
		return val;
	}

	private static WorkGiverDef PlantsCutDef()
	{
		if (plantsCutResolved)
		{
			return plantsCutDef;
		}
		plantsCutResolved = true;
		List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			if (typeof(WorkGiver_PlantsCut).IsAssignableFrom(allDefsListForReading[i].giverClass))
			{
				plantsCutDef = allDefsListForReading[i];
				break;
			}
		}
		return plantsCutDef;
	}

	private static RouteTargetScope ScopeFor(WorkGiver_Scanner scanner, DesignationDef designation)
	{
		if (scanner is WorkGiver_CleanFilth)
		{
			return RouteTargetScope.AnyFilth;
		}
		if (designation == DesignationDefOf.Deconstruct || designation == DesignationDefOf.Uninstall)
		{
			return RouteTargetScope.DesignatedOnly;
		}
		if (designation == DesignationDefOf.Mine || designation == DesignationDefOf.CutPlant)
		{
			return RouteTargetScope.SameDefOrDesignated;
		}
		return RouteTargetScope.SameDef;
	}

	private static bool ScannerSkips(Pawn pawn, WorkGiver_Scanner s, Thing t)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		ThingRequest potentialWorkThingRequest = s.PotentialWorkThingRequest;
		if (((ThingRequest)(ref potentialWorkThingRequest)).Accepts(t))
		{
			return ((WorkGiver)s).ShouldSkip(pawn, true);
		}
		IEnumerable<Thing> enumerable = s.PotentialWorkThingsGlobal(pawn);
		if (enumerable != null)
		{
			foreach (Thing item in enumerable)
			{
				if (item == t)
				{
					return ((WorkGiver)s).ShouldSkip(pawn, true);
				}
			}
		}
		return true;
	}

	private static DesignationDef DetermineDesignation(Map map, Thing clicked)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		DesignationManager designationManager = map.designationManager;
		List<Designation> list = designationManager.AllDesignationsOn(clicked);
		for (int i = 0; i < list.Count; i++)
		{
			DesignationDef def = list[i].def;
			if (def == DesignationDefOf.HarvestPlant || def == DesignationDefOf.CutPlant || def == DesignationDefOf.Deconstruct || def == DesignationDefOf.Uninstall)
			{
				return def;
			}
		}
		if (designationManager.DesignationAt(clicked.Position, DesignationDefOf.Mine) != null || designationManager.DesignationAt(clicked.Position, DesignationDefOf.MineVein) != null)
		{
			return DesignationDefOf.Mine;
		}
		return null;
	}

	private static ThingDef ResolveYield(Thing clicked)
	{
		Plant val = (Plant)(object)((clicked is Plant) ? clicked : null);
		if (val != null)
		{
			return ((Thing)val).def.plant?.harvestedThingDef;
		}
		if (clicked.def.mineable)
		{
			return clicked.def.building?.mineableThing;
		}
		return null;
	}
}
