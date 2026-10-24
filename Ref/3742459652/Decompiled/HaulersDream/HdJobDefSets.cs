using System.Collections.Generic;
using Verse;

namespace HaulersDream;

public static class HdJobDefSets
{
	private static JobDef[] _customDriverJobDefs;

	private static HashSet<JobDef> _inTransitLoadJobs;

	private static HashSet<JobDef> _holdsUnshareablePreloadedStock;

	private static HashSet<JobDef> _noRecursionHaulJobs;

	private static HashSet<JobDef> _constructDeliverJobs;

	public static JobDef[] CustomDriverJobDefs
	{
		get
		{
			object obj = _customDriverJobDefs;
			if (obj == null)
			{
				obj = new JobDef[13]
				{
					HaulersDreamDefOf.HaulersDream_LoadTransportersInBulk,
					HaulersDreamDefOf.HaulersDream_LoadPortalInBulk,
					HaulersDreamDefOf.HaulersDream_LoadVehicleInBulk,
					HaulersDreamDefOf.HaulersDream_LoadPackAnimal,
					HaulersDreamDefOf.HaulersDream_UnloadCarrierInBulk,
					HaulersDreamDefOf.HaulersDream_OverloadConstructDeliver,
					HaulersDreamDefOf.HaulersDream_ConstructDeliverBuild,
					HaulersDreamDefOf.HaulersDream_ClaimFromHauler,
					HaulersDreamDefOf.HaulersDream_BatchCraft,
					HaulersDreamDefOf.HaulersDream_BillPrepGather,
					HaulersDreamDefOf.HaulersDream_BulkHaul,
					HaulersDreamDefOf.HaulersDream_SelfPickup,
					HaulersDreamDefOf.HaulersDream_UnloadInventory
				};
				_customDriverJobDefs = (JobDef[])obj;
			}
			return (JobDef[])obj;
		}
	}

	public static HashSet<JobDef> InTransitLoadJobs
	{
		get
		{
			object obj = _inTransitLoadJobs;
			if (obj == null)
			{
				obj = new HashSet<JobDef>
				{
					HaulersDreamDefOf.HaulersDream_UnloadInventory,
					HaulersDreamDefOf.HaulersDream_BillPrepGather,
					HaulersDreamDefOf.HaulersDream_BatchCraft,
					HaulersDreamDefOf.HaulersDream_OverloadConstructDeliver,
					HaulersDreamDefOf.HaulersDream_ConstructDeliverBuild,
					HaulersDreamDefOf.HaulersDream_BulkHaul,
					HaulersDreamDefOf.HaulersDream_SelfPickup
				};
				_inTransitLoadJobs = (HashSet<JobDef>)obj;
			}
			return (HashSet<JobDef>)obj;
		}
	}

	public static HashSet<JobDef> HoldsUnshareablePreloadedStock
	{
		get
		{
			object obj = _holdsUnshareablePreloadedStock;
			if (obj == null)
			{
				obj = new HashSet<JobDef>
				{
					HaulersDreamDefOf.HaulersDream_BatchCraft,
					HaulersDreamDefOf.HaulersDream_InventoryDoBill,
					HaulersDreamDefOf.HaulersDream_BillPrepGather,
					HaulersDreamDefOf.HaulersDream_OverloadConstructDeliver,
					HaulersDreamDefOf.HaulersDream_ConstructDeliverBuild
				};
				_holdsUnshareablePreloadedStock = (HashSet<JobDef>)obj;
			}
			return (HashSet<JobDef>)obj;
		}
	}

	public static HashSet<JobDef> NoRecursionHaulJobs
	{
		get
		{
			object obj = _noRecursionHaulJobs;
			if (obj == null)
			{
				obj = new HashSet<JobDef>
				{
					HaulersDreamDefOf.HaulersDream_BulkHaul,
					HaulersDreamDefOf.HaulersDream_SelfPickup
				};
				_noRecursionHaulJobs = (HashSet<JobDef>)obj;
			}
			return (HashSet<JobDef>)obj;
		}
	}

	public static HashSet<JobDef> ConstructDeliverJobs
	{
		get
		{
			object obj = _constructDeliverJobs;
			if (obj == null)
			{
				obj = new HashSet<JobDef>
				{
					HaulersDreamDefOf.HaulersDream_OverloadConstructDeliver,
					HaulersDreamDefOf.HaulersDream_ConstructDeliverBuild
				};
				_constructDeliverJobs = (HashSet<JobDef>)obj;
			}
			return (HashSet<JobDef>)obj;
		}
	}
}
