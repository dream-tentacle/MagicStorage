using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class SpoilingFirst
{
	private struct Cand
	{
		public IngredientSpoilKind kind;

		public int ticks;

		public int index;

		public int stock;
	}

	private struct AllowMixComparer(bool mostStockFirst, Cand[] cands, List<Thing> things, Func<Thing, float> valueKey, Func<Thing, int> distKey) : IComparer<int>
	{
		private readonly bool mostStockFirst = mostStockFirst;

		private readonly Cand[] cands = cands;

		private readonly List<Thing> things = things;

		private readonly Func<Thing, float> valueKey = valueKey;

		private readonly Func<Thing, int> distKey = distKey;

		public int Compare(int x, int y)
		{
			int num = SpoilingFirstSelection.CompareCookRank(mostStockFirst, cands[x].stock, cands[x].kind, cands[x].ticks, cands[y].stock, cands[y].kind, cands[y].ticks);
			if (num != 0)
			{
				return num;
			}
			num = valueKey(things[x]).CompareTo(valueKey(things[y]));
			if (num != 0)
			{
				return num;
			}
			num = distKey(things[x]).CompareTo(distKey(things[y]));
			if (num != 0)
			{
				return num;
			}
			return x.CompareTo(y);
		}
	}

	private struct StockDescStableComparer(int[] stock) : IComparer<int>
	{
		private readonly int[] stock = stock;

		public int Compare(int x, int y)
		{
			int num = stock[y].CompareTo(stock[x]);
			if (num == 0)
			{
				return x.CompareTo(y);
			}
			return num;
		}
	}

	[ThreadStatic]
	private static Cand[] scratchCands;

	[ThreadStatic]
	private static int[] scratchOrder;

	[ThreadStatic]
	private static Thing[] scratchSorted;

	private static Cand Classify(Thing t, int index, HaulersDreamSettings s)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Invalid comparison between Unknown and I4
		bool isCorpse = t is Corpse;
		CompRottable val = ThingCompUtility.TryGetComp<CompRottable>(t);
		bool isRottable = val != null && val.Active && (int)val.Stage == 0;
		int ticks = ((val != null && val.Active) ? val.TicksUntilRotAtCurrentTemp : int.MaxValue);
		IngredientSpoilKind kind = SpoilingFirstSelection.Categorize(isCorpse, isRottable, s.butcherSpoilingFirst, s.cookSpoilingFirst);
		return new Cand
		{
			kind = kind,
			ticks = ticks,
			index = index
		};
	}

	public static bool AnyToggleOn(HaulersDreamSettings s)
	{
		if (s != null)
		{
			if (!s.butcherSpoilingFirst)
			{
				return s.cookSpoilingFirst;
			}
			return true;
		}
		return false;
	}

	public static bool IsCookBill(Bill bill, HaulersDreamSettings s)
	{
		if (s != null && bill?.recipe != null && bill.recipe.allowMixingIngredients)
		{
			ThingDef producedThingDef = bill.recipe.ProducedThingDef;
			if (producedThingDef == null)
			{
				return false;
			}
			return producedThingDef.IsIngestible;
		}
		return false;
	}

	public static bool IsCookSpoilingBill(Bill bill, HaulersDreamSettings s)
	{
		if (s != null && s.cookSpoilingFirst)
		{
			return IsCookBill(bill, s);
		}
		return false;
	}

	public static int StockOfDef(ThingDef def, Map map)
	{
		if (def == null || map?.resourceCounter == null)
		{
			return 0;
		}
		return map.resourceCounter.GetCount(def);
	}

	private static int StockOf(Thing t)
	{
		return StockOfDef(t?.def, (t != null) ? t.MapHeld : null);
	}

	public static void SortAllowMix(List<Thing> things, Func<Thing, float> valueKey, Func<Thing, int> distKey, Bill bill, HaulersDreamSettings s)
	{
		if (things == null)
		{
			return;
		}
		bool num = IsCookBill(bill, s);
		bool flag = num && s.cookMostStockFirst;
		bool flag2 = num && s.cookSpoilingFirst;
		if (things.Count < 2 || (!flag && !flag2))
		{
			GenCollection.SortBy<Thing, float, int>(things, valueKey, distKey);
			return;
		}
		int count = things.Count;
		Cand[] array = RentCands(count);
		bool flag3 = false;
		for (int i = 0; i < count; i++)
		{
			array[i] = Classify(things[i], i, s);
			if (flag)
			{
				array[i].stock = StockOf(things[i]);
			}
			if (SpoilingFirstSelection.IsEligible(array[i].kind))
			{
				flag3 = true;
			}
		}
		if (!flag && !flag3)
		{
			GenCollection.SortBy<Thing, float, int>(things, valueKey, distKey);
			return;
		}
		int[] array2 = RentOrder(count);
		for (int j = 0; j < count; j++)
		{
			array2[j] = j;
		}
		Array.Sort(array2, 0, count, new AllowMixComparer(flag, array, things, valueKey, distKey));
		Thing[] array3 = RentSorted(count);
		for (int k = 0; k < count; k++)
		{
			array3[k] = things[array2[k]];
		}
		for (int l = 0; l < count; l++)
		{
			things[l] = array3[l];
		}
		Array.Clear(array3, 0, count);
	}

	private static Cand[] RentCands(int n)
	{
		if (scratchCands == null || scratchCands.Length < n)
		{
			scratchCands = new Cand[n];
		}
		return scratchCands;
	}

	private static int[] RentOrder(int n)
	{
		if (scratchOrder == null || scratchOrder.Length < n)
		{
			scratchOrder = new int[n];
		}
		return scratchOrder;
	}

	private static Thing[] RentSorted(int n)
	{
		if (scratchSorted == null || scratchSorted.Length < n)
		{
			scratchSorted = (Thing[])(object)new Thing[n];
		}
		return scratchSorted;
	}

	public static bool ReorderInPlace(List<Thing> availableThings, HaulersDreamSettings s)
	{
		if (!AnyToggleOn(s) || availableThings == null || availableThings.Count < 2)
		{
			return false;
		}
		Cand[] cands = new Cand[availableThings.Count];
		bool flag = false;
		for (int i = 0; i < availableThings.Count; i++)
		{
			cands[i] = Classify(availableThings[i], i, s);
			if (SpoilingFirstSelection.IsEligible(cands[i].kind))
			{
				flag = true;
			}
		}
		if (!flag)
		{
			return false;
		}
		int[] array = new int[availableThings.Count];
		for (int j = 0; j < array.Length; j++)
		{
			array[j] = j;
		}
		Array.Sort(array, (int x, int y) => SpoilingFirstSelection.Compare(cands[x].kind, cands[x].ticks, cands[x].index, cands[y].kind, cands[y].ticks, cands[y].index));
		Thing[] array2 = (Thing[])(object)new Thing[availableThings.Count];
		for (int num = 0; num < array.Length; num++)
		{
			array2[num] = availableThings[array[num]];
		}
		for (int num2 = 0; num2 < array2.Length; num2++)
		{
			availableThings[num2] = array2[num2];
		}
		return true;
	}

	public static bool BetterThan(Thing b, int distB, Thing a, int distA, HaulersDreamSettings s)
	{
		Cand cand = Classify(b, distB, s);
		Cand cand2 = Classify(a, distA, s);
		return SpoilingFirstSelection.Compare(cand.kind, cand.ticks, cand.index, cand2.kind, cand2.ticks, cand2.index) < 0;
	}

	public static bool CookBetterThan(Thing b, int distB, Thing a, int distA, bool applyStock, HaulersDreamSettings s)
	{
		Cand cand = Classify(b, distB, s);
		Cand cand2 = Classify(a, distA, s);
		int aStock = (applyStock ? StockOf(b) : 0);
		int bStock = (applyStock ? StockOf(a) : 0);
		int num = SpoilingFirstSelection.CompareCookRank(applyStock, aStock, cand.kind, cand.ticks, bStock, cand2.kind, cand2.ticks);
		if (num == 0)
		{
			return distB < distA;
		}
		return num < 0;
	}

	public static void ApplyMostStockStable(List<Thing> things, Bill bill, HaulersDreamSettings s)
	{
		if (things != null && things.Count >= 2 && s != null && s.cookMostStockFirst && IsCookBill(bill, s))
		{
			int count = things.Count;
			int[] array = new int[count];
			int[] array2 = new int[count];
			for (int i = 0; i < count; i++)
			{
				array[i] = StockOf(things[i]);
				array2[i] = i;
			}
			Array.Sort(array2, 0, count, new StockDescStableComparer(array));
			Thing[] array3 = (Thing[])(object)new Thing[count];
			for (int j = 0; j < count; j++)
			{
				array3[j] = things[array2[j]];
			}
			for (int k = 0; k < count; k++)
			{
				things[k] = array3[k];
			}
		}
	}
}
