using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_UnloadYourInventory), "TryGiveJob")]
public static class Patch_JobGiver_UnloadYourInventory
{
	private static void Postfix(ref Job __result, Pawn pawn)
	{
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		if (__result == null || pawn == null || __result.def != JobDefOf.UnloadYourInventory || ((Thing)pawn).Map == null || !MapGate.ShouldUnloadToStorage(((Thing)pawn).Map))
		{
			return;
		}
		try
		{
			HashSet<Thing> hashSet = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>()?.GetHashSet();
			if (hashSet == null || hashSet.Count == 0 || pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_UnloadInventory)
			{
				return;
			}
			ThingOwner<Thing> val = pawn.inventory?.innerContainer;
			if (val == null)
			{
				return;
			}
			bool flag = false;
			foreach (Thing item in hashSet)
			{
				if (item != null && ((ThingOwner)val).Contains(item) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(item), 1, -1, (ReservationLayerDef)null, false) && InventorySurplus.SurplusOf(pawn, item) > 0)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				Job val2 = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_UnloadInventory);
				if (val2.TryMakePreToilReservations(pawn, false))
				{
					__result = val2;
				}
			}
		}
		catch (Exception ex)
		{
			HDGuard.SeamDegraded(ex, "JobGiver_UnloadYourInventory.TryGiveJob (HD unload substitution)", pawn, "kept vanilla's own unload job, so the pawn still empties its inventory.");
		}
	}

	private static Exception Finalizer(Exception __exception, Pawn pawn)
	{
		return HDGuard.SeamThrew(__exception, "JobGiver_UnloadYourInventory.TryGiveJob (HD unload substitution)", pawn, "this pawn's inventory-unload selection failed this scan.");
	}
}
