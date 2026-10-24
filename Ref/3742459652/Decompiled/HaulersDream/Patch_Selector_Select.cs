using System;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(Selector), "Select")]
public static class Patch_Selector_Select
{
	private static readonly SelectionMemo memo;

	static Patch_Selector_Select()
	{
		memo = new SelectionMemo();
		CacheRegistry.Register(Reset);
	}

	internal static void Reset()
	{
		memo.Reset();
	}

	internal static void NotifyCleared()
	{
		memo.NotifyCleared(RealTime.frameCount);
	}

	[HarmonyPostfix]
	private static void Postfix(Selector __instance)
	{
		if (__instance == null || __instance.NumSelected != 1)
		{
			memo.Invalidate();
			return;
		}
		Thing singleSelectedThing = __instance.SingleSelectedThing;
		if (singleSelectedThing == null)
		{
			memo.Invalidate();
		}
		else
		{
			if (!memo.NotifySelected(singleSelectedThing.thingIDNumber, RealTime.frameCount))
			{
				return;
			}
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings == null || (!settings.autoOpenTransporterContents && !settings.autoOpenCarrierGear))
			{
				return;
			}
			if (settings.autoOpenTransporterContents && ThingCompUtility.TryGetComp<CompTransporter>(singleSelectedThing) != null)
			{
				InspectPaneUtility.OpenTab(typeof(ITab_ContentsTransporter));
			}
			else
			{
				if (!settings.autoOpenCarrierGear)
				{
					return;
				}
				Pawn val = (Pawn)(object)((singleSelectedThing is Pawn) ? singleSelectedThing : null);
				if (val == null)
				{
					return;
				}
				RaceProperties raceProps = val.RaceProps;
				bool isHumanlike = raceProps == null || raceProps.Humanlike;
				bool isPlayerFaction = ((Thing)val).Faction != null && ((Thing)val).Faction == Faction.OfPlayerSilentFail;
				Pawn_InventoryTracker inventory = val.inventory;
				int? obj;
				if (inventory == null)
				{
					obj = null;
				}
				else
				{
					ThingOwner<Thing> innerContainer = inventory.innerContainer;
					obj = ((innerContainer != null) ? new int?(((ThingOwner)innerContainer).Count) : ((int?)null));
				}
				int? num = obj;
				if (TabAutoOpenPolicy.CarrierGearApplies(isHumanlike, isPlayerFaction, num.GetValueOrDefault() > 0))
				{
					Type type = (VehicleFrameworkCompat.IsVehicle(singleSelectedThing) ? VehicleFrameworkCompat.VehicleCargoTabType : typeof(ITab_Pawn_Gear));
					if (type != null)
					{
						InspectPaneUtility.OpenTab(type);
					}
				}
			}
		}
	}
}
