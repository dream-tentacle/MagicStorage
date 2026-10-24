using System;
using System.Collections.Generic;

namespace HaulersDream;

internal static class CacheRegistry
{
	private static readonly object sync = new object();

	private static readonly List<Action> clears = new List<Action>();

	internal static void Register(Action clear)
	{
		if (clear == null)
		{
			return;
		}
		lock (sync)
		{
			clears.Add(clear);
		}
	}

	internal static void ClearAll()
	{
		Action[] array;
		lock (sync)
		{
			array = clears.ToArray();
		}
		for (int i = 0; i < array.Length; i++)
		{
			array[i]();
		}
	}
}
