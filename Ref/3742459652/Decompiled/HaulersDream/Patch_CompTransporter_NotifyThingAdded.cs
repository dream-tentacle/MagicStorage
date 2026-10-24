using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(CompTransporter), "Notify_ThingAdded")]
public static class Patch_CompTransporter_NotifyThingAdded
{
	[ThreadStatic]
	private static List<Thing> scratchItems;

	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadTransporters ?? true;
	}

	[HarmonyPostfix]
	private static void Postfix(CompTransporter __instance, Thing t)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.enableBulkLoadTransporters)
		{
			return;
		}
		ThingWithComps parent = ((ThingComp)__instance).parent;
		if (((parent != null) ? parent.GetComp<CompShuttle>() : null) == null)
		{
			return;
		}
		Pawn val = (Pawn)(object)((t is Pawn) ? t : null);
		if (val == null)
		{
			return;
		}
		ThingOwner<Thing> val2 = val.inventory?.innerContainer;
		if (val2 == null || ((ThingOwner)val2).Count == 0)
		{
			return;
		}
		List<TransferableOneWay> leftToLoad = __instance.leftToLoad;
		if (GenList.NullOrEmpty<TransferableOneWay>((IList<TransferableOneWay>)leftToLoad))
		{
			return;
		}
		CompHauledToInventory comp = ((ThingWithComps)val).GetComp<CompHauledToInventory>();
		if (comp == null)
		{
			return;
		}
		HashSet<Thing> hashSet = comp.GetHashSet();
		if (hashSet.Count == 0)
		{
			return;
		}
		ThingOwner innerContainer = __instance.innerContainer;
		if (innerContainer == null)
		{
			return;
		}
		List<Thing> list = scratchItems ?? (scratchItems = new List<Thing>());
		list.Clear();
		for (int i = 0; i < ((ThingOwner)val2).Count; i++)
		{
			Thing val3 = val2[i];
			if (val3 != null && hashSet.Contains(val3))
			{
				list.Add(val3);
			}
		}
		Thing val6 = default(Thing);
		for (int j = 0; j < list.Count; j++)
		{
			Thing val4 = list[j];
			if (val4 == null || val4.Destroyed || !((ThingOwner)val2).Contains(val4))
			{
				continue;
			}
			TransferableOneWay val5 = Global.FindBestMatchFor(val4, leftToLoad);
			if (val5 == null)
			{
				continue;
			}
			int countToTransfer = ((Transferable)val5).CountToTransfer;
			if (countToTransfer <= 0)
			{
				continue;
			}
			int num = ((val4.stackCount < countToTransfer) ? val4.stackCount : countToTransfer);
			if (num > 0)
			{
				Global.IsExecutingManagedUnload = true;
				int num2;
				try
				{
					num2 = val2.TryTransferToContainer(val4, innerContainer, num, ref val6, false);
				}
				finally
				{
					Global.IsExecutingManagedUnload = false;
				}
				if (num2 > 0 && !((ThingOwner)val2).Contains(val4))
				{
					comp.Deregister(val4);
				}
			}
		}
		list.Clear();
	}
}
