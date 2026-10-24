using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobDriver_HaulToCell), "TryMakePreToilReservations")]
public static class Patch_JobDriver_HaulToCell_NoCellReservation
{
	private static bool Prefix(JobDriver_HaulToCell __instance, bool errorOnFailed, ref bool __result)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.haulToStack)
		{
			return true;
		}
		Job job = ((JobDriver)__instance).job;
		if (job == null || (int)job.haulMode != 1)
		{
			return true;
		}
		Pawn pawn = ((JobDriver)__instance).pawn;
		LocalTargetInfo target = job.GetTarget((TargetIndex)1);
		Thing thing = ((LocalTargetInfo)(ref target)).Thing;
		Map val = ((pawn != null) ? ((Thing)pawn).Map : null);
		if (thing?.def == null || val == null)
		{
			return true;
		}
		HaulDestinationManager haulDestinationManager = val.haulDestinationManager;
		target = job.GetTarget((TargetIndex)2);
		ISlotGroup val2 = BulkHaul.BudgetGroupOf(haulDestinationManager.SlotGroupAt(((LocalTargetInfo)(ref target)).Cell));
		int units = Math.Max((job.count > 0) ? Math.Min(job.count, thing.stackCount) : thing.stackCount, StorageCommitments.UnitsMovingOf(pawn, thing.def));
		if (job.playerForced && val2 != null && StorageCommitments.FreeUnitsFor(pawn, val2, thing.def, thing) <= 0)
		{
			StorageCommitments.InterruptCommittersTo(val2, thing.def, pawn);
		}
		if (!StorageCommitments.TryCommit(pawn, val2, thing.def, units, "haul-to-cell"))
		{
			return true;
		}
		__result = ReservationUtility.Reserve(pawn, job.GetTarget((TargetIndex)1), job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false);
		return false;
	}
}
