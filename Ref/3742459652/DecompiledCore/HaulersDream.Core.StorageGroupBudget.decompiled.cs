using System;
using System.Collections.Generic;

namespace HaulersDream.Core;

public sealed class StorageGroupBudget
{
	private int emptyCells;

	private readonly Dictionary<object, int> partialByDef = new Dictionary<object, int>();

	private readonly Dictionary<object, int> perCellByDef = new Dictionary<object, int>();

	public bool Unbounded => emptyCells == int.MaxValue;

	public StorageGroupBudget(int emptyCells)
	{
		this.emptyCells = emptyCells;
	}

	public void Reset(int emptyCells)
	{
		this.emptyCells = emptyCells;
		partialByDef.Clear();
		perCellByDef.Clear();
	}

	public bool IsPriced(object def)
	{
		return perCellByDef.ContainsKey(def);
	}

	public void PriceDef(object def, int partialSpace, int perCellCapacity)
	{
		if (!Unbounded && !perCellByDef.ContainsKey(def))
		{
			partialByDef[def] = Math.Max(0, partialSpace);
			perCellByDef[def] = Math.Max(1, perCellCapacity);
		}
	}

	public int AvailableFor(object def)
	{
		if (Unbounded || !perCellByDef.TryGetValue(def, out var value))
		{
			return int.MaxValue;
		}
		long num = partialByDef[def] + (long)emptyCells * (long)value;
		if (num < int.MaxValue)
		{
			return (int)num;
		}
		return int.MaxValue;
	}

	public void Consume(object def, int count)
	{
		if (Unbounded || count <= 0 || !perCellByDef.TryGetValue(def, out var value))
		{
			return;
		}
		int num = Math.Min(count, partialByDef[def]);
		partialByDef[def] -= num;
		int num2 = count - num;
		if (num2 > 0)
		{
			int num3 = (num2 + value - 1) / value;
			if (num3 > emptyCells)
			{
				num3 = emptyCells;
			}
			emptyCells -= num3;
			int num4 = num3 * value - num2;
			if (num4 > 0)
			{
				partialByDef[def] = (partialByDef.TryGetValue(def, out var value2) ? (value2 + num4) : num4);
			}
		}
	}
}
