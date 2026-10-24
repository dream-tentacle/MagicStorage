using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(MapPortal), "SubtractFromToLoadList")]
public static class Patch_MapPortal_SubtractFromToLoadList
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadPortal ?? true;
	}

	private static bool Prefix(MapPortal __instance, Thing t, int count, ref int __result)
	{
		if (!Global.IsExecutingManagedPortalUnload)
		{
			return true;
		}
		List<TransferableOneWay> leftToLoad = __instance.leftToLoad;
		if (leftToLoad == null)
		{
			__result = 0;
			return false;
		}
		TransferableOneWay val = Global.FindBestMatchFor(t, count, leftToLoad);
		if (val == null)
		{
			__result = 0;
			return false;
		}
		int countToTransfer = ((Transferable)val).CountToTransfer;
		if (countToTransfer <= 0)
		{
			__result = 0;
			return false;
		}
		int num = ((count < countToTransfer) ? count : countToTransfer);
		int value = countToTransfer - num;
		if (!Global.ForceTo(val, value))
		{
			Global.ForceToViaReflection(val, value);
		}
		val.things?.Remove(t);
		if (((Transferable)val).CountToTransfer <= 0)
		{
			leftToLoad.Remove(val);
		}
		__result = num;
		return false;
	}
}
