using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage;

[StaticConstructorOnStartup]
internal static class StorageLaunchPaymentPatches
{
	static StorageLaunchPaymentPatches()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		Harmony val = new Harmony("mjcg.magicstorage.trading.payment");
		val.Patch((MethodBase)AccessTools.Method(typeof(TradeUtility), "AllLaunchableThingsForTrade", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(StorageLaunchPaymentPatches), "LaunchableThings", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null);
		val.Patch((MethodBase)AccessTools.Method(typeof(TradeUtility), "LaunchThingsOfType", (Type[])null, (Type[])null), new HarmonyMethod(typeof(StorageLaunchPaymentPatches), "Pay", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
	}

	private static void LaunchableThings(Map map, ITrader trader, ref IEnumerable<Thing> __result)
	{
		if (map != null && trader == null)
		{
			__result = WithStorage(__result, map);
		}
	}

	private static IEnumerable<Thing> WithStorage(IEnumerable<Thing> original, Map map)
	{
		HashSet<Thing> seen = new HashSet<Thing>();
		foreach (Thing item in original)
		{
			if (seen.Add(item))
			{
				yield return item;
			}
		}
		foreach (StorageNetwork network in Networks(map))
		{
			foreach (Thing item2 in Stocks(network, null))
			{
				if (seen.Add(item2))
				{
					yield return item2;
				}
			}
		}
	}

	private static List<StorageNetwork> Networks(Map map)
	{
		map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
		List<StorageNetwork> list = new List<StorageNetwork>();
		HashSet<StorageNetwork> hashSet = new HashSet<StorageNetwork>();
		foreach (Building item in map.listerBuildings.allBuildingsColonist)
		{
			if (item is Building_StorageCore building_StorageCore && StorageTradeSession.PaymentCoreEligible(building_StorageCore, map) && hashSet.Add(building_StorageCore.Network))
			{
				list.Add(building_StorageCore.Network);
			}
		}
		return list;
	}

	private static List<Thing> Stocks(StorageNetwork network, ThingDef def)
	{
		List<Thing> list = new List<Thing>();
		if (def == null)
		{
			network.GetAllStacks(list);
		}
		else
		{
			network.GetStacks(def, list);
		}
		list.RemoveAll((Thing item) => item == null || item.Destroyed || item.stackCount <= 0 || (int)item.def.category != 2 || !TradeUtility.PlayerSellableNow(item, (ITrader)null) || network.AvailableToWithdraw(item) != item.stackCount);
		list.Sort((Thing a, Thing b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
		return list;
	}

	private static IEnumerable<Thing> Ground(Map map, ThingDef def)
	{
		HashSet<Thing> seen = new HashSet<Thing>();
		foreach (Building_OrbitalTradeBeacon beacon in Building_OrbitalTradeBeacon.AllPowered(map))
		{
			foreach (IntVec3 cell in beacon.TradeableCells)
			{
				foreach (Thing item in GridsUtility.GetThingList(cell, map))
				{
					if (item.def == def && StorageMaterialTransaction.GroundAvailable(map, item, respectReservations: false) && seen.Add(item))
					{
						yield return item;
					}
				}
			}
		}
	}

	private static bool Pay(ThingDef resDef, int debt, Map map, TradeShip trader)
	{
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || trader != null || debt <= 0 || resDef == null)
		{
			return true;
		}
		List<StorageNetwork> list = Networks(map);
		if (list.Count == 0)
		{
			long num = 0L;
			foreach (Thing item in Ground(map, resDef))
			{
				num += item.stackCount;
				if (num >= debt)
				{
					return true;
				}
			}
			return Reject();
		}
		using (StorageMaterialTransaction storageMaterialTransaction = new StorageMaterialTransaction(map, IntVec3.Invalid, respectGroundReservations: false))
		{
			int num2 = debt;
			HashSet<Thing> hashSet = new HashSet<Thing>();
			foreach (Thing item2 in Ground(map, resDef))
			{
				int num3 = Math.Min(num2, item2.stackCount);
				if (!storageMaterialTransaction.Add(item2, num3))
				{
					return Reject();
				}
				hashSet.Add(item2);
				num2 -= num3;
				if (num2 == 0)
				{
					break;
				}
			}
			foreach (StorageNetwork item3 in list)
			{
				if (num2 == 0)
				{
					break;
				}
				foreach (Thing item4 in Stocks(item3, resDef))
				{
					if (hashSet.Add(item4))
					{
						int num4 = Math.Min(num2, item4.stackCount);
						if (!storageMaterialTransaction.Add(item4, num4, item3, (Thing)(object)item3.Core))
						{
							return Reject();
						}
						num2 -= num4;
						if (num2 == 0)
						{
							break;
						}
					}
				}
			}
			if (num2 != 0 || !Eligible(storageMaterialTransaction, map) || !storageMaterialTransaction.TryStage() || !Eligible(storageMaterialTransaction, map) || !storageMaterialTransaction.ConsumeAll())
			{
				return Reject();
			}
		}
		return false;
	}

	private static bool Eligible(StorageMaterialTransaction payment, Map map)
	{
		foreach (StorageMaterialTransaction.Request request in payment.Requests)
		{
			if (request.Network != null && !StorageTradeSession.PaymentCoreEligible(request.Network.Core, map))
			{
				return false;
			}
		}
		return true;
	}

	private static bool Reject()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Messages.Message(TaggedString.op_Implicit(Translator.Translate("MS_Payment_Unavailable")), MessageTypeDefOf.RejectInput, false);
		return false;
	}
}
