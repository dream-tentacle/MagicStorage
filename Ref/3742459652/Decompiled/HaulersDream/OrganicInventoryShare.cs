using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class OrganicInventoryShare
{
	[ThreadStatic]
	private static int countCacheTick;

	[ThreadStatic]
	private static Dictionary<long, int> countCache;

	private static bool OtherPawnSourcesAllowedHere(Map map, HaulersDreamSettings s)
	{
		if (!s.enableOnNonHomeMaps)
		{
			if (map != null)
			{
				return map.IsPlayerHome;
			}
			return false;
		}
		return true;
	}

	private static BuildMaterialSource ClassifyHolder(Pawn carrier, Pawn worker)
	{
		if (carrier == worker)
		{
			return BuildMaterialSource.Own;
		}
		if (carrier.RaceProps == null || !carrier.RaceProps.Animal)
		{
			return BuildMaterialSource.Other;
		}
		return BuildMaterialSource.PackAnimal;
	}

	private static bool IsEligibleOrganicHolder(Pawn carrier, Pawn worker, bool allowVehicles)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (!InventoryShare.IsEligibleCarrier(carrier, worker))
		{
			return false;
		}
		if (VehicleFrameworkCompat.IsVehicle((Thing)(object)carrier) && !allowVehicles)
		{
			return false;
		}
		if (VehicleFrameworkCompat.InVehicle((Thing)(object)carrier))
		{
			return false;
		}
		if (CaravanFormingUtility.IsFormingCaravan(carrier))
		{
			return false;
		}
		if (ForbidUtility.IsForbidden(((Thing)carrier).Position, worker))
		{
			return false;
		}
		return true;
	}

	static OrganicInventoryShare()
	{
		CacheRegistry.Register(Clear);
	}

	public static int CountOrganic(Map map, Pawn worker, ThingDef def)
	{
		if (map == null || worker == null || def == null)
		{
			return 0;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
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
		int num2 = CountOrganicOfDef(worker, def);
		if (OtherPawnSourcesAllowedHere(map, settings))
		{
			bool allowVehicles = settings.buildFromVehiclesAway && !map.IsPlayerHome;
			List<Pawn> list = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
			for (int i = 0; i < list.Count; i++)
			{
				Pawn carrier = list[i];
				if (IsEligibleOrganicHolder(carrier, worker, allowVehicles))
				{
					num2 += CountOrganicOfDef(carrier, def);
				}
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

	internal static int CountOrganicOfDef(Pawn carrier, ThingDef def)
	{
		ThingOwner<Thing> val = carrier?.inventory?.innerContainer;
		if (val == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < ((ThingOwner)val).Count; i++)
		{
			Thing val2 = val[i];
			if (val2 != null && val2.def == def)
			{
				num += val2.stackCount;
			}
		}
		return num;
	}

	public static Thing FindOrganicStack(Map map, Pawn worker, ThingDef def)
	{
		if (map == null || worker == null || def == null)
		{
			return null;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return null;
		}
		if (CountOrganic(map, worker, def) <= 0)
		{
			return null;
		}
		Thing best = null;
		BuildMaterialSource bestSource = BuildMaterialSource.PackAnimal;
		int bestDist = int.MaxValue;
		int bestIndex = int.MaxValue;
		int idx = 0;
		ConsiderHolder(worker, worker, def, BuildMaterialSource.Own, ref best, ref bestSource, ref bestDist, ref bestIndex, ref idx);
		if (OtherPawnSourcesAllowedHere(map, settings))
		{
			bool allowVehicles = settings.buildFromVehiclesAway && !map.IsPlayerHome;
			List<Pawn> list = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
			for (int i = 0; i < list.Count; i++)
			{
				Pawn carrier = list[i];
				if (IsEligibleOrganicHolder(carrier, worker, allowVehicles))
				{
					ConsiderHolder(carrier, worker, def, ClassifyHolder(carrier, worker), ref best, ref bestSource, ref bestDist, ref bestIndex, ref idx);
				}
			}
		}
		return best;
	}

	private static void ConsiderHolder(Pawn carrier, Pawn worker, ThingDef def, BuildMaterialSource source, ref Thing best, ref BuildMaterialSource bestSource, ref int bestDist, ref int bestIndex, ref int idx)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		ThingOwner<Thing> val = carrier.inventory?.innerContainer;
		if (val == null)
		{
			return;
		}
		bool flag = carrier == worker;
		bool flag2 = flag;
		bool flag3 = flag;
		int num = ((!flag) ? IntVec3Utility.ManhattanDistanceFlat(((Thing)worker).Position, ((Thing)carrier).Position) : 0);
		for (int i = 0; i < ((ThingOwner)val).Count; i++)
		{
			Thing val2 = val[i];
			if (val2 == null || val2.def != def)
			{
				continue;
			}
			bool flag4 = ReservationUtility.CanReserve(worker, LocalTargetInfo.op_Implicit(val2), 1, -1, (ReservationLayerDef)null, false);
			if (!flag && flag4 && !flag3)
			{
				flag2 = ReachabilityUtility.CanReach(worker, LocalTargetInfo.op_Implicit((Thing)(object)carrier), (PathEndMode)2, (Danger)2, false, false, (TraverseMode)0);
				flag3 = true;
			}
			if (!flag && flag3 && !flag2)
			{
				break;
			}
			if (SharePolicy.ShouldIncludeStack(flag, flag2, flag4, isUsable: true, withinRadius: true))
			{
				if (best == null || BuildFromInventorySource.Compare(source, num, idx, bestSource, bestDist, bestIndex) < 0)
				{
					best = val2;
					bestSource = source;
					bestDist = num;
					bestIndex = idx;
				}
				idx++;
			}
		}
	}
}
