using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class BulkUnloadGate
{
	internal static bool PlayerMayUnload(Pawn hauler, Pawn carrier)
	{
		if (hauler == null || carrier == null)
		{
			return false;
		}
		Faction faction = ((Thing)hauler).Faction;
		if (faction == null)
		{
			return false;
		}
		bool isPrisonerOfHaulerFaction = carrier.IsPrisoner && carrier.HostFaction == faction;
		return BulkUnloadPermissionPolicy.MayBulkUnload(((Thing)carrier).Faction == faction, isPrisonerOfHaulerFaction, QuestUtility.IsQuestLodger(carrier));
	}

	internal static bool ShouldHandle(Pawn pawn, Thing t)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.enableBulkUnloadCarriers)
		{
			return false;
		}
		if (pawn != null)
		{
			Pawn val = (Pawn)(object)((t is Pawn) ? t : null);
			if (val != null)
			{
				if (((ThingWithComps)val).GetComp<CompMechCarrier>() != null)
				{
					return false;
				}
				if (VehicleFrameworkCompat.IsVehicle((Thing)(object)val))
				{
					return false;
				}
				if (!PlayerMayUnload(pawn, val))
				{
					return false;
				}
				return true;
			}
		}
		return false;
	}

	internal static bool CanDoBulkUnload(Pawn pawn, Pawn carrier, bool forced)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || carrier?.inventory == null || pawn?.carryTracker == null)
		{
			return false;
		}
		if (!UnloadCarriersJobGiverUtility.HasJobOnThing(pawn, (Thing)(object)carrier, forced))
		{
			return false;
		}
		if (CarrierIsMidLoadOrHaul(carrier))
		{
			return false;
		}
		if (!forced && PawnUnloadChecker.InDirectedActivity(carrier))
		{
			return false;
		}
		if (pawn.carryTracker.innerContainer != null && ((ThingOwner)pawn.carryTracker.innerContainer).Count > 0)
		{
			return false;
		}
		if (!BulkUnloadCarrierPolicy.HasEnoughBackpackRoom(MassUtility.EncumbrancePercent(pawn), settings.minFreeSpaceToUnloadCarrierPct))
		{
			return false;
		}
		if (carrier.inventory.innerContainer == null || ((ThingOwner)carrier.inventory.innerContainer).Count == 0)
		{
			return false;
		}
		if (AnotherPawnUnloading(pawn, carrier))
		{
			return false;
		}
		return true;
	}

	private static bool CarrierIsMidLoadOrHaul(Pawn carrier)
	{
		JobDef curJobDef = carrier.CurJobDef;
		if (curJobDef == null)
		{
			return false;
		}
		if (curJobDef != HaulersDreamDefOf.HaulersDream_LoadPackAnimal && curJobDef != HaulersDreamDefOf.HaulersDream_UnloadCarrierInBulk && curJobDef != JobDefOf.GiveToPackAnimal && curJobDef != JobDefOf.PrepareCaravan_GatherItems && curJobDef != JobDefOf.PrepareCaravan_GatherAnimals && curJobDef != JobDefOf.PrepareCaravan_GatherDownedPawns && curJobDef != JobDefOf.EnterTransporter)
		{
			return curJobDef == JobDefOf.CarryDownedPawnToExit;
		}
		return true;
	}

	private static bool AnotherPawnUnloading(Pawn pawn, Pawn carrier)
	{
		Map map = ((Thing)carrier).Map;
		if (map?.mapPawns == null)
		{
			return false;
		}
		IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
		for (int i = 0; i < allPawnsSpawned.Count; i++)
		{
			Pawn val = allPawnsSpawned[i];
			if (val != null && val != pawn && val != carrier)
			{
				Job curJob = val.CurJob;
				if (curJob != null && (curJob.def == JobDefOf.UnloadInventory || curJob.def == HaulersDreamDefOf.HaulersDream_UnloadCarrierInBulk) && (object)((LocalTargetInfo)(ref curJob.targetA)).Thing == carrier)
				{
					return true;
				}
			}
		}
		return false;
	}
}
