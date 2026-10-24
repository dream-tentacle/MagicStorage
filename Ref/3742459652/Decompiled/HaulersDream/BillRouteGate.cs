using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class BillRouteGate
{
	public static bool IsRoutableBenchType(Thing billGiver)
	{
		if (billGiver is Building_WorkTable)
		{
			return !(billGiver is Building_WorkTableAutonomous);
		}
		return false;
	}

	public static bool MayRouteToInventory(Thing billGiver)
	{
		if (IsRoutableBenchType(billGiver))
		{
			return CompBenchGather.Allows(billGiver);
		}
		return false;
	}

	public static bool BatchSuppressedByBench(Bill bill)
	{
		IBillGiver obj = bill?.billStack?.billGiver;
		Thing val = (Thing)(object)((obj is Thing) ? obj : null);
		if (val != null)
		{
			return !CompBenchGather.Allows(val);
		}
		return false;
	}

	public static bool WorkerMayShareCraft(Pawn worker)
	{
		if (((worker != null) ? worker.RaceProps : null) != null)
		{
			return !worker.RaceProps.IsMechanoid;
		}
		return false;
	}

	public static bool ChosenIngredientsUnstackable(Job job)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		List<LocalTargetInfo> list = job?.targetQueueB;
		if (list == null)
		{
			return false;
		}
		for (int i = 0; i < list.Count; i++)
		{
			LocalTargetInfo val = list[i];
			ThingDef val2 = ((LocalTargetInfo)(ref val)).Thing?.def;
			if (val2 != null && val2.stackLimit == 1)
			{
				return true;
			}
		}
		return false;
	}
}
