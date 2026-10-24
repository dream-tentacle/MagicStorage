using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class InventoryConstructDelivery
{
	[ThreadStatic]
	internal static ConstructRouteIntent RouteIntent;

	[ThreadStatic]
	internal static Dictionary<ThingDef, int> RouteDemandByDef;

	internal const float ClusterScanRadius = 24f;

	private static readonly List<Thing> NoneQueued = new List<Thing>();

	internal static Job TryBuild(Pawn pawn, IConstructible c, Thing resource, bool forced, bool tetherBuild = false, Job vanillaJob = null)
	{
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_046e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0583: Unknown result type (might be due to invalid IL or missing references)
		//IL_0588: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_05cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_05de: Unknown result type (might be due to invalid IL or missing references)
		//IL_0649: Unknown result type (might be due to invalid IL or missing references)
		//IL_0622: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		ThingDef val = resource?.def;
		if (settings == null || val == null || pawn?.carryTracker == null || ((Thing)pawn).Map == null)
		{
			return null;
		}
		Thing val2 = (Thing)(object)((c is Thing) ? c : null);
		if (val2 == null || val2.Destroyed || !val2.Spawned)
		{
			return null;
		}
		int num = pawn.carryTracker.MaxStackSpaceEver(val);
		if (num <= 0)
		{
			HDLog.Dbg($"inv-deliver skip: handCap<=0 for {((Def)val).label} ({pawn})");
			return null;
		}
		int num2;
		if (!forced)
		{
			IHaulEnroute val3 = (IHaulEnroute)(object)((c is IHaulEnroute) ? c : null);
			if (val3 != null)
			{
				num2 = EnrouteUtility.GetSpaceRemainingWithEnroute(val3, val, pawn);
				goto IL_0097;
			}
		}
		num2 = c.ThingCountNeeded(val);
		goto IL_0097;
		IL_0097:
		int num3 = num2;
		if (num3 <= 0)
		{
			return null;
		}
		int num4 = (settings.buildFromInventory ? OrganicInventoryShare.CountOrganicOfDef(pawn, val) : 0);
		bool flag = settings.buildFromInventory && BuildFromInventorySource.OwnInventoryCoversDelivery(num4, num3, num);
		int num5 = int.MaxValue;
		if (!flag)
		{
			num5 = CECompat.FitUnitsByBulk(pawn, val);
			if (ConstructDeliveryPlan.InventoryLoadWorseThanHands(num5, num))
			{
				HDLog.Dbg($"inv-deliver skip: CE bulk fits {num5} <= handCap {num} of {((Def)val).label} " + $"for {pawn} (hand-carry beats a bulk-capped inventory trip)");
				return null;
			}
		}
		PawnMass pawnMass = PawnMassCache.MassInfo(pawn);
		float capacity = pawnMass.Capacity;
		if (capacity <= 0f)
		{
			return null;
		}
		float num6 = CarryMath.EffectiveCapacity(capacity, settings.carryLimitFraction);
		float currentMass = pawnMass.CurrentMass;
		float statValueAbstract = StatExtension.GetStatValueAbstract((BuildableDef)(object)val, StatDefOf.Mass, (ThingDef)null);
		if (statValueAbstract <= 0f)
		{
			return null;
		}
		int overloadLevel = (OverloadGate.NoOverloadFor(pawn, settings) ? 10 : settings.overloadLevel);
		int num7 = (flag ? num4 : Mathf.Min(ConstructDeliveryPlan.GatherCeiling(overloadLevel, capacity, num6, currentMass, statValueAbstract, int.MaxValue), num5));
		List<Thing> list = null;
		int clusterNeed = 0;
		bool flag2 = false;
		if (settings.multiSiteConstructDeliver && RouteIntent == ConstructRouteIntent.None && num7 > num)
		{
			list = new List<Thing>();
			AddClusterNeeder(val2, val, pawn, forced, list, ref clusterNeed);
			if (vanillaJob != null)
			{
				if (ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(((LocalTargetInfo)(ref vanillaJob.targetB)).Thing), (PathEndMode)2))
				{
					AddClusterNeeder(((LocalTargetInfo)(ref vanillaJob.targetB)).Thing, val, pawn, forced, list, ref clusterNeed);
				}
				if (vanillaJob.targetQueueB != null)
				{
					for (int i = 0; i < vanillaJob.targetQueueB.Count; i++)
					{
						LocalTargetInfo val4 = vanillaJob.targetQueueB[i];
						Thing thing = ((LocalTargetInfo)(ref val4)).Thing;
						if (ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(thing), (PathEndMode)2))
						{
							AddClusterNeeder(thing, val, pawn, forced, list, ref clusterNeed);
						}
					}
				}
			}
			ScanNearbyNeeders(pawn, val2, val, forced, num7, list, ref clusterNeed);
			flag2 = ConstructDeliveryPlan.MultiSiteWorthIt(list.Count, clusterNeed, num);
			if (!flag2)
			{
				list = null;
			}
		}
		if (!flag && !flag2 && !forced && num3 <= num)
		{
			HDLog.Dbg($"inv-deliver skip: need {num3} <= handCap {num} for {((Def)val).label} → {((Entity)val2).LabelShort} (one hand-trip suffices)");
			return null;
		}
		if (!flag && !flag2 && forced && RouteIntent == ConstructRouteIntent.None && num3 <= num && vanillaJob != null && vanillaJob.targetQueueB != null && vanillaJob.targetQueueB.Count > 0)
		{
			HDLog.Dbg("inv-deliver skip: forced one-hand order for " + ((Def)val).label + " → " + ((Entity)val2).LabelShort + ", " + $"vanilla batches {vanillaJob.targetQueueB.Count} more needer(s) (keep vanilla cluster delivery)");
			return null;
		}
		int num8 = (flag2 ? clusterNeed : num3);
		if (RouteIntent == ConstructRouteIntent.HaulBuild && RouteDemandByDef != null && RouteDemandByDef.TryGetValue(val, out var value) && value > num8)
		{
			num8 = value;
		}
		List<Thing> list2 = new List<Thing>();
		Thing val5 = null;
		int num9;
		if (flag)
		{
			Pawn_InventoryTracker inventory = pawn.inventory;
			val5 = YieldRouter.InventoryStackOfDef((inventory != null) ? inventory.GetDirectlyHeldThings() : null, val);
			if (val5 == null)
			{
				return null;
			}
			num9 = Mathf.Min(num8, num4);
		}
		else
		{
			int num10 = Mathf.Min(ConstructDeliveryPlan.GatherCeiling(overloadLevel, capacity, num6, currentMass, statValueAbstract, num8), num5);
			if (!forced && num10 <= num)
			{
				HDLog.Dbg($"inv-deliver skip: ceiling {num10} <= handCap {num} for {((Def)val).label} (cur mass {currentMass:0.0}/{num6:0.0}kg, unit {statValueAbstract:0.###})");
				return null;
			}
			if (forced && num10 < 1)
			{
				num10 = num;
			}
			int num11 = Gather(pawn, val, resource.Position, num10, list2);
			int frameNeedUnits = (flag2 ? clusterNeed : num3);
			num9 = (forced ? Mathf.Min(num8, num11) : ConstructDeliveryPlan.PlanLoad(overloadLevel, capacity, num6, currentMass, statValueAbstract, frameNeedUnits, num, num11));
			num9 = Mathf.Min(num9, num5);
			if (num9 <= 0)
			{
				HDLog.Dbg("inv-deliver skip: targetLoad 0 for " + ((Def)val).label + " → " + ((Entity)val2).LabelShort + " " + $"(available {num11} in {list2.Count} stacks near stockpile, need {num3}, handCap {num}, ceiling {num10})");
				return null;
			}
			TrimToCount(list2, num9);
			if (list2.Count == 0)
			{
				return null;
			}
		}
		Job val6 = JobMaker.MakeJob(tetherBuild ? HaulersDreamDefOf.HaulersDream_ConstructDeliverBuild : HaulersDreamDefOf.HaulersDream_OverloadConstructDeliver);
		val6.targetA = LocalTargetInfo.op_Implicit(flag ? val5 : list2[0]);
		val6.targetQueueA = new List<LocalTargetInfo>();
		for (int j = 0; j < list2.Count; j++)
		{
			val6.targetQueueA.Add(LocalTargetInfo.op_Implicit(list2[j]));
		}
		val6.targetB = LocalTargetInfo.op_Implicit(val2);
		val6.targetC = LocalTargetInfo.op_Implicit(val2);
		if (flag2 && list != null && list.Count > 1)
		{
			val6.targetQueueB = new List<LocalTargetInfo>();
			for (int k = 0; k < list.Count; k++)
			{
				if (list[k] != val2)
				{
					val6.targetQueueB.Add(LocalTargetInfo.op_Implicit(list[k]));
				}
			}
		}
		val6.count = num9;
		val6.haulMode = (HaulMode)3;
		string text = (flag ? $"own inventory, {num4} carried" : $"{list2.Count} stacks");
		HDLog.Dbg(flag2 ? ($"{pawn} inventory-delivers {num9}x {((Def)val).label} to a {list.Count}-site cluster " + $"(primary {((Entity)val2).LabelShort}, cluster need {clusterNeed}, hand cap {num}, {text}).") : ($"{pawn} inventory-delivers {num9}x {((Def)val).label} to {((Entity)val2).LabelShort} " + $"(need {num3}, hand cap {num}, {text})."));
		return val6;
	}

	private static int Gather(Pawn pawn, ThingDef def, IntVec3 anchor, int unitCap, List<Thing> outStacks)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		List<Thing> list = ((Thing)pawn).Map.listerThings.ThingsOfDef(def);
		List<Thing> list2 = new List<Thing>();
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if (val != null && !ForbidUtility.IsForbidden(val, pawn))
			{
				list2.Add(val);
			}
		}
		list2.Sort(delegate(Thing a, Thing b)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 val3 = a.Position - anchor;
			int lengthHorizontalSquared = ((IntVec3)(ref val3)).LengthHorizontalSquared;
			val3 = b.Position - anchor;
			int num3 = lengthHorizontalSquared.CompareTo(((IntVec3)(ref val3)).LengthHorizontalSquared);
			return (num3 == 0) ? a.thingIDNumber.CompareTo(b.thingIDNumber) : num3;
		});
		int num = 0;
		for (int num2 = 0; num2 < list2.Count; num2++)
		{
			if (num >= unitCap)
			{
				break;
			}
			Thing val2 = list2[num2];
			if (HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, val2, false) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(val2), 1, -1, (ReservationLayerDef)null, false) && ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(val2), (PathEndMode)3))
			{
				outStacks.Add(val2);
				num += val2.stackCount;
			}
		}
		return num;
	}

	internal static Job TryBuildHaulOnlyOrder(Pawn pawn, Thing site)
	{
		IConstructible val = (IConstructible)(object)((site is IConstructible) ? site : null);
		if (val == null || ((pawn != null) ? ((Thing)pawn).Map : null) == null)
		{
			return null;
		}
		if (FirstNeededDefWithStock(pawn, val, out var nearestStack) == null || nearestStack == null)
		{
			return null;
		}
		return TryBuild(pawn, val, nearestStack, forced: true);
	}

	internal static bool AnyNeededMaterialAvailable(Pawn pawn, Thing site)
	{
		IConstructible val = (IConstructible)(object)((site is IConstructible) ? site : null);
		Thing nearestStack;
		if (val != null)
		{
			return FirstNeededDefWithStock(pawn, val, out nearestStack) != null;
		}
		return false;
	}

	private static ThingDef FirstNeededDefWithStock(Pawn pawn, IConstructible c, out Thing nearestStack)
	{
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		nearestStack = null;
		List<ThingDefCountClass> list = c.TotalMaterialCost();
		if (list == null)
		{
			return null;
		}
		for (int i = 0; i < list.Count; i++)
		{
			ThingDef val = list[i]?.thingDef;
			if (val == null || c.ThingCountNeeded(val) <= 0)
			{
				continue;
			}
			List<Thing> list2 = ((Thing)pawn).Map.listerThings.ThingsOfDef(val);
			Thing val2 = null;
			int num = int.MaxValue;
			for (int j = 0; j < list2.Count; j++)
			{
				Thing val3 = list2[j];
				if (val3 != null && val3.Spawned && !ForbidUtility.IsForbidden(val3, pawn) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(val3), 1, -1, (ReservationLayerDef)null, false) && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, val3, false) && ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(val3), (PathEndMode)3))
				{
					IntVec3 val4 = val3.Position - ((Thing)pawn).Position;
					int lengthHorizontalSquared = ((IntVec3)(ref val4)).LengthHorizontalSquared;
					if (lengthHorizontalSquared < num || (lengthHorizontalSquared == num && val2 != null && val3.thingIDNumber < val2.thingIDNumber))
					{
						num = lengthHorizontalSquared;
						val2 = val3;
					}
				}
			}
			if (val2 != null)
			{
				nearestStack = val2;
				return val;
			}
		}
		return null;
	}

	private static void AddClusterNeeder(Thing cand, ThingDef def, Pawn pawn, bool forced, List<Thing> outNeeders, ref int clusterNeed)
	{
		if (cand == null || cand.Destroyed || !cand.Spawned)
		{
			return;
		}
		IConstructible val = (IConstructible)(object)((cand is IConstructible) ? cand : null);
		if (val == null || outNeeders.Contains(cand))
		{
			return;
		}
		int num;
		if (!forced)
		{
			IHaulEnroute val2 = (IHaulEnroute)(object)((cand is IHaulEnroute) ? cand : null);
			if (val2 != null)
			{
				num = EnrouteUtility.GetSpaceRemainingWithEnroute(val2, def, pawn);
				goto IL_0046;
			}
		}
		num = val.ThingCountNeeded(def);
		goto IL_0046;
		IL_0046:
		int num2 = num;
		if (num2 > 0)
		{
			outNeeders.Add(cand);
			clusterNeed += num2;
		}
	}

	private static void ScanNearbyNeeders(Pawn pawn, Thing primary, ThingDef def, bool forced, int maxLoadUnits, List<Thing> outNeeders, ref int clusterNeed)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		if (clusterNeed >= maxLoadUnits)
		{
			return;
		}
		Map map = ((Thing)pawn).Map;
		IntVec3 anchor = primary.Position;
		float radiusSq = 576f;
		List<Thing> list = new List<Thing>();
		CollectConstructibles(map, (ThingRequestGroup)9, def, pawn, anchor, radiusSq, outNeeders, list);
		CollectConstructibles(map, (ThingRequestGroup)11, def, pawn, anchor, radiusSq, outNeeders, list);
		list.Sort(delegate(Thing a, Thing b)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 val2 = a.Position - anchor;
			int lengthHorizontalSquared = ((IntVec3)(ref val2)).LengthHorizontalSquared;
			val2 = b.Position - anchor;
			int num2 = lengthHorizontalSquared.CompareTo(((IntVec3)(ref val2)).LengthHorizontalSquared);
			return (num2 == 0) ? a.thingIDNumber.CompareTo(b.thingIDNumber) : num2;
		});
		for (int num = 0; num < list.Count; num++)
		{
			if (clusterNeed >= maxLoadUnits)
			{
				break;
			}
			Thing val = list[num];
			if (GenConstruct.CanConstruct(val, pawn, false, false, JobDefOf.HaulToContainer) && ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(val), (PathEndMode)2))
			{
				AddClusterNeeder(val, def, pawn, forced, outNeeders, ref clusterNeed);
			}
		}
	}

	private static void CollectConstructibles(Map map, ThingRequestGroup group, ThingDef def, Pawn pawn, IntVec3 anchor, float radiusSq, List<Thing> alreadyQueued, List<Thing> outScan)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		List<Thing> list = map.listerThings.ThingsInGroup(group);
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if (val == null || !val.Spawned || val is Blueprint_Install)
			{
				continue;
			}
			IConstructible val2 = (IConstructible)(object)((val is IConstructible) ? val : null);
			if (val2 != null)
			{
				IntVec3 val3 = val.Position - anchor;
				if (!((float)((IntVec3)(ref val3)).LengthHorizontalSquared > radiusSq) && val.Faction == ((Thing)pawn).Faction && !ForbidUtility.IsForbidden(val, pawn) && val2.ThingCountNeeded(def) > 0 && !alreadyQueued.Contains(val))
				{
					outScan.Add(val);
				}
			}
		}
	}

	internal static void CollectNeedersNear(Map map, ThingDef def, Pawn pawn, IntVec3 anchor, List<Thing> outScan)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (map != null && def != null && pawn != null && outScan != null)
		{
			float radiusSq = 576f;
			CollectConstructibles(map, (ThingRequestGroup)9, def, pawn, anchor, radiusSq, NoneQueued, outScan);
			CollectConstructibles(map, (ThingRequestGroup)11, def, pawn, anchor, radiusSq, NoneQueued, outScan);
		}
	}

	private static void TrimToCount(List<Thing> stacks, int target)
	{
		int num = 0;
		int i;
		for (i = 0; i < stacks.Count; i++)
		{
			if (num >= target)
			{
				break;
			}
			num += stacks[i].stackCount;
		}
		if (i < stacks.Count)
		{
			stacks.RemoveRange(i, stacks.Count - i);
		}
	}
}
