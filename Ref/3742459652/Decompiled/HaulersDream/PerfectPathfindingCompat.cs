using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace HaulersDream;

public static class PerfectPathfindingCompat
{
	private static bool initialized;

	private static bool active;

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
		if ((AccessTools.TypeByName("RimThreaded.PerfectPathfinding") ?? AccessTools.TypeByName("PerfectPathfinding.PerfectPathfindingMod") ?? AccessTools.TypeByName("PerfectPathfinding.Mod_PerfectPathfinding") ?? AccessTools.TypeByName("PerfectPathfinding.PerfectPathfinding")) == null)
		{
			IEnumerable<ModMetaData> allInstalledMods = ModLister.AllInstalledMods;
			if (allInstalledMods != null)
			{
				foreach (ModMetaData item in allInstalledMods)
				{
					if (item != null && item.Active && item.Name != null && item.Name.IndexOf("Perfect Pathfinding", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						active = true;
						break;
					}
				}
			}
		}
		else
		{
			active = true;
		}
		if (active)
		{
			HDLog.Msg("Perfect Pathfinding detected — en-route accurate path checks inherit its accuracy automatically (same vanilla pathfinder entry point).");
		}
	}
}
