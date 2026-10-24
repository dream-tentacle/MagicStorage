using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class ConstructionBatch
{
	private const int MaxBatchSpan = 40;

	private const int MaxBatchSpanSq = 1600;

	internal static void Expand(Pawn pawn, Job job, ThingDef def, bool forced)
	{
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || job == null || def == null || pawn.carryTracker == null || (!(((LocalTargetInfo)(ref job.targetB)).Thing is IHaulEnroute) && !(((LocalTargetInfo)(ref job.targetC)).Thing is IHaulEnroute)))
		{
			return;
		}
		int num = pawn.carryTracker.MaxStackSpaceEver(def);
		if (num <= job.count)
		{
			return;
		}
		HashSet<Thing> hashSet = new HashSet<Thing>();
		AddThing(hashSet, ((LocalTargetInfo)(ref job.targetB)).Thing);
		AddThing(hashSet, ((LocalTargetInfo)(ref job.targetC)).Thing);
		if (job.targetQueueB != null)
		{
			foreach (LocalTargetInfo item in job.targetQueueB)
			{
				LocalTargetInfo current = item;
				AddThing(hashSet, ((LocalTargetInfo)(ref current)).Thing);
			}
		}
		HashSet<Thing> hashSet2 = new HashSet<Thing>();
		int num2 = AddResource(hashSet2, ((LocalTargetInfo)(ref job.targetA)).Thing);
		if (job.targetQueueA != null)
		{
			foreach (LocalTargetInfo item2 in job.targetQueueA)
			{
				LocalTargetInfo current2 = item2;
				num2 += AddResource(hashSet2, ((LocalTargetInfo)(ref current2)).Thing);
			}
		}
		Thing thing = ((LocalTargetInfo)(ref job.targetB)).Thing;
		_003F val;
		if (thing == null)
		{
			Thing thing2 = ((LocalTargetInfo)(ref job.targetC)).Thing;
			val = ((thing2 != null) ? thing2.Position : ((Thing)pawn).Position);
		}
		else
		{
			val = thing.Position;
		}
		IntVec3 anchor = (IntVec3)val;
		Thing thing3 = ((LocalTargetInfo)(ref job.targetA)).Thing;
		IntVec3 anchor2 = ((thing3 != null) ? thing3.Position : ((Thing)pawn).Position);
		List<Thing> list = new List<Thing>();
		List<int> list2 = new List<int>();
		GatherNeeders(pawn, def, forced, hashSet, num - job.count, anchor, list, list2);
		if (list.Count == 0)
		{
			return;
		}
		List<Thing> list3 = new List<Thing>();
		List<int> list4 = new List<int>();
		GatherResources(pawn, def, hashSet2, num, anchor2, list3, list4);
		ConstructionBatchMath.Plan(num, job.count, num2, list2, list4, out var finalCount, out var neederTake, out var resourceTake);
		if (finalCount <= job.count || neederTake <= 0)
		{
			return;
		}
		int count = job.count;
		if (job.targetQueueB == null)
		{
			job.targetQueueB = new List<LocalTargetInfo>();
		}
		for (int i = 0; i < neederTake && i < list.Count; i++)
		{
			job.targetQueueB.Add(LocalTargetInfo.op_Implicit(list[i]));
		}
		if (resourceTake > 0)
		{
			if (job.targetQueueA == null)
			{
				job.targetQueueA = new List<LocalTargetInfo>();
			}
			for (int j = 0; j < resourceTake && j < list3.Count; j++)
			{
				job.targetQueueA.Add(LocalTargetInfo.op_Implicit(list3[j]));
			}
		}
		job.count = finalCount;
		HDLog.Dbg($"{pawn} batched {((Def)def).label} construction haul x{finalCount} (was x{count}) across {hashSet.Count + neederTake} needers.");
	}

	private static void AddThing(HashSet<Thing> set, Thing t)
	{
		if (t != null)
		{
			set.Add(t);
		}
	}

	private static int AddResource(HashSet<Thing> set, Thing t)
	{
		if (t == null || !set.Add(t))
		{
			return 0;
		}
		return t.stackCount;
	}

	private static void GatherNeeders(Pawn pawn, ThingDef def, bool forced, HashSet<Thing> exclude, int spaceWanted, IntVec3 anchor, List<Thing> outNeeders, List<int> outSpaces)
	{
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Expected O, but got Unknown
		List<Thing> list = ((Thing)pawn).Map.listerThings.ThingsInGroup((ThingRequestGroup)37);
		List<Thing> list2 = new List<Thing>();
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if (val != null && !exclude.Contains(val) && val is IConstructible && val is IHaulEnroute && val.Faction == ((Thing)pawn).Faction && !ForbidUtility.IsForbidden(val, pawn))
			{
				IntVec3 val2 = val.Position - anchor;
				if (((IntVec3)(ref val2)).LengthHorizontalSquared <= 1600)
				{
					list2.Add(val);
				}
			}
		}
		SortByDistance(list2, ((Thing)pawn).Position);
		int num = 0;
		for (int j = 0; j < list2.Count; j++)
		{
			if (num >= spaceWanted)
			{
				break;
			}
			Thing val3 = list2[j];
			if (GenConstruct.CanConstruct(val3, pawn, false, forced, JobDefOf.HaulToContainer) && ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(val3), (PathEndMode)2))
			{
				int num2 = (forced ? ((IConstructible)val3).ThingCountNeeded(def) : EnrouteUtility.GetSpaceRemainingWithEnroute((IHaulEnroute)val3, def, pawn));
				if (num2 > 0)
				{
					outNeeders.Add(val3);
					outSpaces.Add(num2);
					num += num2;
				}
			}
		}
	}

	private static void GatherResources(Pawn pawn, ThingDef def, HashSet<Thing> exclude, int handCap, IntVec3 anchor, List<Thing> outResources, List<int> outCounts)
	{
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		List<Thing> list = ((Thing)pawn).Map.listerThings.ThingsOfDef(def);
		List<Thing> list2 = new List<Thing>();
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if (val != null && !exclude.Contains(val) && !ForbidUtility.IsForbidden(val, pawn))
			{
				IntVec3 val2 = val.Position - anchor;
				if (((IntVec3)(ref val2)).LengthHorizontalSquared <= 1600)
				{
					list2.Add(val);
				}
			}
		}
		SortByDistance(list2, ((Thing)pawn).Position);
		int num = 0;
		for (int j = 0; j < list2.Count; j++)
		{
			if (num >= handCap)
			{
				break;
			}
			Thing val3 = list2[j];
			if (HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, val3, false) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(val3), 1, -1, (ReservationLayerDef)null, false) && ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(val3), (PathEndMode)3))
			{
				outResources.Add(val3);
				outCounts.Add(val3.stackCount);
				num += val3.stackCount;
			}
		}
	}

	private static void SortByDistance(List<Thing> things, IntVec3 from)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		things.Sort(delegate(Thing a, Thing b)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 val = a.Position - from;
			int lengthHorizontalSquared = ((IntVec3)(ref val)).LengthHorizontalSquared;
			val = b.Position - from;
			int num = lengthHorizontalSquared.CompareTo(((IntVec3)(ref val)).LengthHorizontalSquared);
			return (num == 0) ? a.thingIDNumber.CompareTo(b.thingIDNumber) : num;
		});
	}
}
