using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace HaulersDream;

public static class DbhCompat
{
	private static bool initialized;

	private static bool active;

	private static Type waterExtType;

	public static bool IsActive
	{
		get
		{
			if (!initialized)
			{
				Init();
			}
			return active;
		}
	}

	private static void Init()
	{
		initialized = true;
		waterExtType = AccessTools.TypeByName("DubsBadHygiene.WaterExt");
		active = waterExtType != null;
		if (active)
		{
			HDLog.Msg("Dub's Bad Hygiene detected — carried water is excluded from surplus unloading.");
		}
	}

	public static bool IsKeptDrink(Thing thing)
	{
		if (!IsActive || thing?.def == null)
		{
			return false;
		}
		List<DefModExtension> modExtensions = ((Def)thing.def).modExtensions;
		if (modExtensions == null)
		{
			return false;
		}
		for (int i = 0; i < modExtensions.Count; i++)
		{
			if (modExtensions[i] != null && waterExtType.IsInstanceOfType(modExtensions[i]))
			{
				return true;
			}
		}
		return false;
	}
}
