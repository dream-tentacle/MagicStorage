using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class InventoryShare
{
	[ThreadStatic]
	private static int countCacheTick;

	[ThreadStatic]
	private static Dictionary<long, int> countCache;

	public static Thing FindSharableStack(Map map, Pawn worker, ThingDef def)
	{
		if (map == null || worker == null || def == null)
		{
			return null;
		}
		if (CountSharable(map, worker, def) <= 0)
		{
			return null;
		}
		Thing best = null;
		int bestDist = int.MaxValue;
		ConsiderCarrierStack(worker, worker, def, ref best, ref bestDist);
		List<Pawn> list = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
		for (int i = 0; i < list.Count; i++)
		{
			Pawn carrier = list[i];
			if (IsEligibleCarrier(carrier, worker))
			{
				ConsiderCarrierStack(carrier, worker, def, ref best, ref bestDist);
			}
		}
		return best;
	}

	private static void ConsiderCarrierStack(Pawn carrier, Pawn worker, ThingDef def, ref Thing best, ref int bestDist)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		CompHauledToInventory comp = ((ThingWithComps)carrier).GetComp<CompHauledToInventory>();
		ThingOwner<Thing> val = carrier.inventory?.innerContainer;
		if (comp == null || val == null)
		{
			return;
		}
		bool flag = carrier == worker;
		bool flag2 = flag;
		bool flag3 = flag;
		foreach (Thing item in comp.GetHashSet())
		{
			if (item == null || item.def != def || !((ThingOwner)val).Contains(item))
			{
				continue;
			}
			bool flag4 = ReservationUtility.CanReserve(worker, LocalTargetInfo.op_Implicit(item), 1, -1, (ReservationLayerDef)null, false);
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
				int num = ((!flag) ? IntVec3Utility.ManhattanDistanceFlat(((Thing)worker).Position, ((Thing)carrier).Position) : 0);
				if (num < bestDist || (num == bestDist && best != null && item.thingIDNumber < best.thingIDNumber))
				{
					bestDist = num;
					best = item;
				}
			}
		}
	}

	public static void AddSharableStacksForBill(Pawn worker, Bill bill, List<Thing> outList)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		Map val = ((worker != null) ? ((Thing)worker).Map : null);
		if (val == null || bill?.recipe == null || outList == null)
		{
			return;
		}
		float ingredientSearchRadius = bill.ingredientSearchRadius;
		bool flag = ingredientSearchRadius < 999f;
		IntVec3 val2 = IntVec3.Invalid;
		if (flag)
		{
			IBillGiver obj = bill.billStack?.billGiver;
			Thing val3 = (Thing)(object)((obj is Thing) ? obj : null);
			if (val3 != null && val3.Spawned)
			{
				val2 = val3.Position;
			}
			else
			{
				flag = false;
			}
		}
		float num = ingredientSearchRadius * ingredientSearchRadius;
		int count = outList.Count;
		AddCarrierStacks(worker, worker, bill, outList);
		int num2 = outList.Count - count;
		if (num2 > 0)
		{
			CompHauledToInventory comp = ((ThingWithComps)worker).GetComp<CompHauledToInventory>();
			HDLog.Dbg(string.Format("DoBill ingredient-share for {0} / {1}: ", worker, ((Def)(bill.recipe?)).defName ?? "?") + $"worker tagged={comp?.PeekHashSet().Count ?? 0}, self-pass added {num2} stack(s).");
		}
		List<Pawn> list = val.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
		for (int i = 0; i < list.Count; i++)
		{
			Pawn val4 = list[i];
			if (!IsEligibleCarrier(val4, worker))
			{
				continue;
			}
			if (flag)
			{
				IntVec3 val5 = ((Thing)val4).Position - val2;
				if ((float)((IntVec3)(ref val5)).LengthHorizontalSquared >= num)
				{
					continue;
				}
			}
			AddCarrierStacks(val4, worker, bill, outList);
		}
	}

	private static void AddCarrierStacks(Pawn carrier, Pawn worker, Bill bill, List<Thing> outList)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		CompHauledToInventory comp = ((ThingWithComps)carrier).GetComp<CompHauledToInventory>();
		ThingOwner<Thing> val = carrier.inventory?.innerContainer;
		if (comp == null || val == null)
		{
			return;
		}
		bool flag = carrier == worker;
		bool flag2 = flag;
		bool flag3 = flag;
		foreach (Thing item in comp.GetHashSet())
		{
			if (item != null && ((ThingOwner)val).Contains(item) && IsUsableForBill(item, bill))
			{
				bool flag4 = ReservationUtility.CanReserve(worker, LocalTargetInfo.op_Implicit(item), 1, -1, (ReservationLayerDef)null, false);
				if (!flag && flag4 && !flag3)
				{
					flag2 = ReachabilityUtility.CanReach(worker, LocalTargetInfo.op_Implicit((Thing)(object)carrier), (PathEndMode)2, (Danger)2, false, false, (TraverseMode)0);
					flag3 = true;
				}
				if (!flag && flag3 && !flag2)
				{
					break;
				}
				if (SharePolicy.ShouldIncludeStack(flag, flag2, flag4, isUsable: true, withinRadius: true) && !outList.Contains(item))
				{
					outList.Add(item);
				}
			}
		}
	}

	internal static bool IsUsableForBill(Thing t, Bill bill)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		if (!bill.IsFixedOrAllowedIngredient(t))
		{
			return false;
		}
		if (t.def.IsMedicine)
		{
			IBillGiver obj = bill.billStack?.billGiver;
			Pawn val = (Pawn)(object)((obj is Pawn) ? obj : null);
			if (val != null && !MedicalCareUtility.AllowsMedicine(WorkGiver_DoBill.GetMedicalCareCategory((Thing)(object)val), t.def))
			{
				return false;
			}
		}
		List<IngredientCount> ingredients = bill.recipe.ingredients;
		for (int i = 0; i < ingredients.Count; i++)
		{
			if (ingredients[i].filter.Allows(t))
			{
				return true;
			}
		}
		return false;
	}

	static InventoryShare()
	{
		CacheRegistry.Register(Clear);
	}

	public static int CountSharable(Map map, Pawn worker, ThingDef def)
	{
		if (map == null || def == null || worker == null)
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
		int num2 = CountTaggedOfDef(worker, def);
		List<Pawn> list = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
		for (int i = 0; i < list.Count; i++)
		{
			Pawn carrier = list[i];
			if (IsEligibleCarrier(carrier, worker))
			{
				num2 += CountTaggedOfDef(carrier, def);
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

	private static int CountTaggedOfDef(Pawn carrier, ThingDef def)
	{
		CompHauledToInventory compHauledToInventory = ((carrier != null) ? ((ThingWithComps)carrier).GetComp<CompHauledToInventory>() : null);
		ThingOwner<Thing> val = carrier?.inventory?.innerContainer;
		if (compHauledToInventory == null || val == null)
		{
			return 0;
		}
		int num = 0;
		foreach (Thing item in compHauledToInventory.GetHashSet())
		{
			if (item != null && item.def == def && ((ThingOwner)val).Contains(item))
			{
				num += item.stackCount;
			}
		}
		return num;
	}

	internal static bool IsLiveShareCarrier(Pawn carrier, Pawn worker)
	{
		if (carrier == null || carrier == worker)
		{
			return false;
		}
		if (!((Thing)carrier).Spawned || carrier.Dead || carrier.Downed)
		{
			return false;
		}
		if (carrier.Drafted)
		{
			return false;
		}
		if (carrier.InMentalState)
		{
			return false;
		}
		return true;
	}

	internal static bool IsEligibleCarrier(Pawn carrier, Pawn worker)
	{
		if (!IsLiveShareCarrier(carrier, worker))
		{
			return false;
		}
		JobDef curJobDef = carrier.CurJobDef;
		if (curJobDef != null && HdJobDefSets.HoldsUnshareablePreloadedStock.Contains(curJobDef))
		{
			return false;
		}
		return true;
	}
}
