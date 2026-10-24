using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class MapGate
{
	public static bool HdActiveOnMap(Map map)
	{
		if (map == null)
		{
			return true;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return true;
		}
		bool isPlayerControlled = settings.nonHomeMapsPlayerControlledOnly && IsPlayerControlled(map);
		return MapEligibilityPolicy.HdActiveOnMap(map.IsPlayerHome, isPlayerControlled, settings.enableOnNonHomeMaps, settings.nonHomeMapsPlayerControlledOnly);
	}

	public static bool IsPlayerControlled(Map map)
	{
		if (map != null)
		{
			if (!map.IsPlayerHome && map.ParentFaction != Faction.OfPlayer)
			{
				return HasPlayerStorage(map);
			}
			return true;
		}
		return false;
	}

	public static bool ShouldUnloadToStorage(Map map)
	{
		if (map == null)
		{
			return false;
		}
		if (map.IsPlayerHome)
		{
			return true;
		}
		return HasPlayerStorage(map);
	}

	public static bool HasPlayerStorage(Map map)
	{
		HaulDestinationManager obj = map?.haulDestinationManager;
		List<SlotGroup> list = ((obj != null) ? obj.AllGroupsListForReading : null);
		if (list == null)
		{
			return false;
		}
		for (int i = 0; i < list.Count; i++)
		{
			ISlotGroupParent val = list[i]?.parent;
			if (val != null)
			{
				if (val is Zone)
				{
					return true;
				}
				Thing val2 = (Thing)(object)((val is Thing) ? val : null);
				if (val2 != null && val2.Faction == Faction.OfPlayer)
				{
					return true;
				}
			}
		}
		return false;
	}
}
