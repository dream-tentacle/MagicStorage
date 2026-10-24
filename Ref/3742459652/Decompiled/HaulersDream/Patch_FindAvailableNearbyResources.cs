using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "FindAvailableNearbyResources")]
public static class Patch_FindAvailableNearbyResources
{
	private static bool _refResolved;

	private static FieldRef<List<Thing>> _resourcesAvailable;

	private static FieldRef<List<Thing>> ResourcesAvailableRef()
	{
		if (!_refResolved)
		{
			_refResolved = true;
			FieldInfo fieldInfo = AccessTools.Field(typeof(WorkGiver_ConstructDeliverResources), "resourcesAvailable");
			if (fieldInfo != null)
			{
				_resourcesAvailable = AccessTools.StaticFieldRefAccess<List<Thing>>(fieldInfo);
			}
			else
			{
				HDLog.Warn("WorkGiver_ConstructDeliverResources.resourcesAvailable not found on this RimWorld build; partial build-from-inventory delivery degrades to vanilla.");
			}
		}
		return _resourcesAvailable;
	}

	private static bool Prefix(Thing firstFoundResource, ref int resTotalAvailable)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.buildFromInventory || !settings.buildFromInventoryPartial)
		{
			return true;
		}
		if (firstFoundResource == null || firstFoundResource.Spawned)
		{
			return true;
		}
		FieldRef<List<Thing>> val = ResourcesAvailableRef();
		if (val == null)
		{
			return true;
		}
		List<Thing> list = val.Invoke();
		if (list == null)
		{
			return true;
		}
		list.Clear();
		list.Add(firstFoundResource);
		resTotalAvailable = firstFoundResource.stackCount;
		return false;
	}
}
