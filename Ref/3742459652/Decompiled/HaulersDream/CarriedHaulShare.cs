using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class CarriedHaulShare
{
	private const int CooldownTicks = 250;

	[ThreadStatic]
	private static int countCacheTick;

	[ThreadStatic]
	private static Dictionary<long, int> countCache;

	internal static Thing StorageBoundCarried(Pawn carrier, Pawn worker)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Invalid comparison between Unknown and I4
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if (!InventoryShare.IsLiveShareCarrier(carrier, worker))
		{
			return null;
		}
		Pawn_CarryTracker carryTracker = carrier.carryTracker;
		Thing val = ((carryTracker != null) ? carryTracker.CarriedThing : null);
		if (val == null || val.def == null || !val.def.EverHaulable)
		{
			return null;
		}
		Job curJob = carrier.CurJob;
		if (curJob == null || curJob.playerForced)
		{
			return null;
		}
		if (curJob.def != JobDefOf.HaulToCell || (int)curJob.haulMode != 1)
		{
			return null;
		}
		LocalTargetInfo target = curJob.GetTarget((TargetIndex)1);
		if (((LocalTargetInfo)(ref target)).Thing != val)
		{
			return null;
		}
		IntVec3 cell = ((LocalTargetInfo)(ref curJob.targetB)).Cell;
		if (!((IntVec3)(ref cell)).IsValid || StoreUtility.GetSlotGroup(cell, ((Thing)carrier).Map) == null)
		{
			return null;
		}
		return val;
	}

	static CarriedHaulShare()
	{
		CacheRegistry.Register(Clear);
	}

	internal static int CountStorageBoundCarried(Map map, Pawn worker, ThingDef def)
	{
		if (map == null || worker == null || def == null)
		{
			return 0;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : (-1));
		Dictionary<long, int> dictionary = countCache ?? (countCache = new Dictionary<long, int>());
		if (num != -1 && num != countCacheTick)
		{
			countCacheTick = num;
			dictionary.Clear();
		}
		long key = ((long)((Thing)worker).thingIDNumber << 32) | ((Def)def).shortHash;
		if (num != -1 && dictionary.TryGetValue(key, out var value))
		{
			return value;
		}
		int num2 = 0;
		List<Pawn> list = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = StorageBoundCarried(list[i], worker);
			if (val != null && val.def == def)
			{
				num2 += val.stackCount;
			}
		}
		if (num != -1)
		{
			dictionary[key] = num2;
		}
		return num2;
	}

	internal static void Clear()
	{
		countCacheTick = 0;
		countCache?.Clear();
	}

	internal static Thing FindCarriedStack(Map map, Pawn worker, ThingDef def, Thing needer, int needed, out Pawn carrier)
	{
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		carrier = null;
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.shareHandHauledToStorage || map == null || worker == null || def == null || needer == null || needed <= 0)
		{
			return null;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : 0);
		CompHauledToInventory comp = ((ThingWithComps)worker).GetComp<CompHauledToInventory>();
		if (comp != null && num - comp.lastInterceptedTick < 250)
		{
			return null;
		}
		if (CountStorageBoundCarried(map, worker, def) <= 0)
		{
			return null;
		}
		List<Pawn> list = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
		Thing result = null;
		int num2 = int.MaxValue;
		for (int i = 0; i < list.Count; i++)
		{
			Pawn val = list[i];
			Thing val2 = StorageBoundCarried(val, worker);
			if (val2 != null && val2.def == def && ReservationUtility.CanReserve(worker, LocalTargetInfo.op_Implicit((Thing)(object)val), 1, -1, (ReservationLayerDef)null, false) && ReachabilityUtility.CanReach(worker, LocalTargetInfo.op_Implicit((Thing)(object)val), (PathEndMode)2, (Danger)2, false, false, (TraverseMode)0))
			{
				IntVec3 cell = ((LocalTargetInfo)(ref val.CurJob.targetB)).Cell;
				int num3 = CellDist(((Thing)worker).Position, ((Thing)val).Position);
				int haulerToStorage = CellDist(((Thing)val).Position, cell);
				int haulerToNeeder = CellDist(((Thing)val).Position, needer.Position);
				int storageToNeeder = CellDist(cell, needer.Position);
				float carriedFractionOfNeed = (float)Mathf.Min(val2.stackCount, needed) / (float)needed;
				if (CarriedInterceptPolicy.ShouldIntercept(num3, haulerToStorage, haulerToNeeder, storageToNeeder, carriedFractionOfNeed) && num3 < num2)
				{
					num2 = num3;
					result = val2;
					carrier = val;
				}
			}
		}
		return result;
	}

	private static int CellDist(IntVec3 a, IntVec3 b)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 val = a - b;
		return Mathf.RoundToInt(((IntVec3)(ref val)).LengthHorizontal);
	}
}
