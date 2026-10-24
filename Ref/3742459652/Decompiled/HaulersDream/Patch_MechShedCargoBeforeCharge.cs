using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_GetEnergy_Charger), "TryGiveJob")]
public static class Patch_MechShedCargoBeforeCharge
{
	private const float CriticalEnergyFloor = 15f;

	private static void Postfix(ref Job __result, Pawn pawn, JobGiver_GetEnergy_Charger __instance)
	{
		if (__result == null)
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.enableSoftlockDrop || (__instance != null && ((JobGiver_GetEnergy)__instance).forced) || pawn == null || ((Thing)pawn).Faction != Faction.OfPlayerSilentFail || !pawn.RaceProps.IsMechanoid)
		{
			return;
		}
		try
		{
			CompHauledToInventory compHauledToInventory = ThingCompUtility.TryGetComp<CompHauledToInventory>((Thing)(object)pawn);
			ThingOwner<Thing> val = pawn.inventory?.innerContainer;
			if (compHauledToInventory == null || val == null || !HasLiveTaggedCargo(compHauledToInventory, val))
			{
				return;
			}
			Need_MechEnergy val2 = pawn.needs?.energy;
			if (val2 != null && ((Need)val2).CurLevel > 15f && HasDeliverableSurplus(pawn, compHauledToInventory, val))
			{
				PawnUnloadChecker.CheckIfShouldUnload(pawn, forced: true);
				if (PawnUnloadChecker.HasQueuedUnload(pawn))
				{
					__result = null;
					return;
				}
			}
			HaulersDreamGameComponent.DropTaggedCargo(pawn);
		}
		catch (Exception ex)
		{
			HDGuard.SeamDegraded(ex, "JobGiver_GetEnergy_Charger.TryGiveJob (HD shed-cargo-before-charge)", pawn, "kept vanilla's charge job, so the mech still charges.");
		}
	}

	private static bool HasLiveTaggedCargo(CompHauledToInventory comp, ThingOwner<Thing> owner)
	{
		foreach (Thing item in comp.PeekHashSet())
		{
			if (item != null && !item.Destroyed && ((ThingOwner)owner).Contains(item))
			{
				return true;
			}
		}
		return false;
	}

	private static bool HasDeliverableSurplus(Pawn pawn, CompHauledToInventory comp, ThingOwner<Thing> owner)
	{
		foreach (Thing item in comp.PeekHashSet())
		{
			if (item != null && !item.Destroyed && ((ThingOwner)owner).Contains(item) && InventorySurplus.SurplusOf(pawn, item) > 0 && InventorySurplus.HasUnloadDestination(pawn, item))
			{
				return true;
			}
		}
		return false;
	}
}
