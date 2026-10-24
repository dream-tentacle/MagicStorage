using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class ConstructTether
{
	private static List<WorkGiver_Scanner> deliverScanners;

	public static bool QueueNext(Pawn pawn, IntVec3 cell, bool allowDeliverTether = true)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || pawn.Dead || !((Thing)pawn).Spawned || pawn.Drafted || pawn.jobs == null)
		{
			return false;
		}
		Thing val = FindConstructibleAt(((Thing)pawn).Map, cell, ((Thing)pawn).Faction);
		if (val == null)
		{
			return false;
		}
		Frame val2 = (Frame)(object)((val is Frame) ? val : null);
		if (val2 != null && val2.IsCompleted())
		{
			if (WorkCapabilityProbe.IsDisabled(pawn, WorkTypeDefOf.Construction))
			{
				return false;
			}
			if (!GenConstruct.CanConstruct((Thing)(object)val2, pawn, true, true, (JobDef)null))
			{
				return false;
			}
			Job val3 = JobMaker.MakeJob(JobDefOf.FinishFrame, LocalTargetInfo.op_Implicit((Thing)(object)val2));
			val3.playerForced = true;
			if (val3.TryMakePreToilReservations(pawn, false))
			{
				pawn.jobs.jobQueue.EnqueueFirst(val3, (JobTag?)(JobTag)0);
				return true;
			}
			return false;
		}
		if (!allowDeliverTether)
		{
			return false;
		}
		ConstructRouteIntent routeIntent = InventoryConstructDelivery.RouteIntent;
		Dictionary<ThingDef, int> routeDemandByDef = InventoryConstructDelivery.RouteDemandByDef;
		try
		{
			InventoryConstructDelivery.RouteIntent = ConstructRouteIntent.HaulBuild;
			InventoryConstructDelivery.RouteDemandByDef = RemainingRouteDemand(pawn, val);
			foreach (WorkGiver_Scanner item in DeliverScanners())
			{
				Job val4 = (item.HasJobOnThing(pawn, val, true) ? item.JobOnThing(pawn, val, true) : null);
				if (val4 != null)
				{
					val4.playerForced = true;
					if (val4.TryMakePreToilReservations(pawn, false))
					{
						pawn.jobs.jobQueue.EnqueueFirst(val4, (JobTag?)(JobTag)0);
						return true;
					}
				}
			}
		}
		finally
		{
			InventoryConstructDelivery.RouteIntent = routeIntent;
			InventoryConstructDelivery.RouteDemandByDef = routeDemandByDef;
		}
		return false;
	}

	private static Dictionary<ThingDef, int> RemainingRouteDemand(Pawn pawn, Thing site)
	{
		Dictionary<ThingDef, int> dictionary = new Dictionary<ThingDef, int>();
		HashSet<Thing> seen = new HashSet<Thing>();
		AccumulateDemand(site, dictionary, seen);
		JobQueue val = pawn.jobs?.jobQueue;
		if (val != null)
		{
			for (int i = 0; i < val.Count; i++)
			{
				Job val2 = val[i]?.job;
				if (val2 != null && val2.def != null && HdJobDefSets.ConstructDeliverJobs.Contains(val2.def))
				{
					AccumulateDemand(((LocalTargetInfo)(ref val2.targetB)).Thing ?? ((LocalTargetInfo)(ref val2.targetC)).Thing, dictionary, seen);
				}
			}
		}
		return dictionary;
	}

	private static void AccumulateDemand(Thing t, Dictionary<ThingDef, int> demand, HashSet<Thing> seen)
	{
		if (t == null || t.Destroyed || !t.Spawned)
		{
			return;
		}
		IConstructible val = (IConstructible)(object)((t is IConstructible) ? t : null);
		if (val == null || !seen.Add(t))
		{
			return;
		}
		List<ThingDefCountClass> list = val.TotalMaterialCost();
		if (list == null)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			ThingDef val2 = list[i]?.thingDef;
			if (val2 != null)
			{
				int num = val.ThingCountNeeded(val2);
				if (num > 0)
				{
					demand.TryGetValue(val2, out var value);
					demand[val2] = value + num;
				}
			}
		}
	}

	public static Thing FindConstructibleAt(Map map, IntVec3 cell, Faction faction)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || !GenGrid.InBounds(cell, map))
		{
			return null;
		}
		List<Thing> list = map.thingGrid.ThingsListAtFast(cell);
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if (val is IConstructible && val.Faction == faction && val.Spawned && !val.Destroyed && (val is Blueprint || val is Frame))
			{
				return val;
			}
		}
		return null;
	}

	internal static List<WorkGiver_Scanner> DeliverScanners()
	{
		if (deliverScanners != null)
		{
			return deliverScanners;
		}
		deliverScanners = new List<WorkGiver_Scanner>(2);
		List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			if (typeof(WorkGiver_ConstructDeliverResourcesToBlueprints).IsAssignableFrom(allDefsListForReading[i].giverClass) || typeof(WorkGiver_ConstructDeliverResourcesToFrames).IsAssignableFrom(allDefsListForReading[i].giverClass))
			{
				WorkGiver worker = allDefsListForReading[i].Worker;
				WorkGiver_Scanner val = (WorkGiver_Scanner)(object)((worker is WorkGiver_Scanner) ? worker : null);
				if (val != null)
				{
					deliverScanners.Add(val);
				}
			}
		}
		return deliverScanners;
	}
}
