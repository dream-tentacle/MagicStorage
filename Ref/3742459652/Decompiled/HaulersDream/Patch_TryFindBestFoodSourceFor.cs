using System;
using System.Collections.Generic;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(FoodUtility), "TryFindBestFoodSourceFor")]
public static class Patch_TryFindBestFoodSourceFor
{
	[HarmonyPriority(200)]
	public static void Postfix(ref bool __result, Pawn getter, Pawn eater, ref Thing foodSource, ref ThingDef foodDef, bool canUseInventory, bool canUsePackAnimalInventory)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.mealsOnWheels || __result || !canUseInventory || !canUsePackAnimalInventory || eater == null || !eater.IsFreeColonist || getter == null || !getter.RaceProps.ToolUser || !getter.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || (getter == eater && ProtectedWork.IsRestingPatient(eater)))
		{
			return;
		}
		Map map = ((Thing)eater).Map;
		if (map == null)
		{
			return;
		}
		Thing val = foodSource;
		ThingDef val2 = foodDef;
		try
		{
			ScanAndResolve(ref __result, getter, eater, ref foodSource, ref foodDef, map, settings);
		}
		catch (Exception ex)
		{
			foodSource = val;
			foodDef = val2;
			__result = false;
			HDGuard.SeamDegraded(ex, "FoodUtility.TryFindBestFoodSourceFor (HD meals-on-wheels)", eater, "kept vanilla's result (no food found by HD), so food selection itself keeps working.");
		}
	}

	private static void ScanAndResolve(ref bool __result, Pawn getter, Pawn eater, ref Thing foodSource, ref ThingDef foodDef, Map map, HaulersDreamSettings s)
	{
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		bool flag = !PawnUtility.IsTeetotaler(eater);
		Thing val = null;
		MealCandidatePass mealCandidatePass = MealCandidatePass.None;
		int bTicks = int.MaxValue;
		int bDist = int.MaxValue;
		int bIndex = int.MaxValue;
		int num = 0;
		List<Pawn> list = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
		for (int i = 0; i < list.Count; i++)
		{
			Pawn val2 = list[i];
			if (!InventoryShare.IsEligibleCarrier(val2, getter) || (VehicleFrameworkCompat.IsVehicle((Thing)(object)val2) && (!s.eatFromVehiclesAway || map.IsPlayerHome)) || VehicleFrameworkCompat.InVehicle((Thing)(object)val2) || val2.jobs?.curDriver is JobDriver_FeedBaby)
			{
				continue;
			}
			ThingOwner<Thing> val3 = val2.inventory?.innerContainer;
			if (val3 == null || ((ThingOwner)val3).Count == 0 || ForbidUtility.IsForbidden(((Thing)val2).Position, getter))
			{
				continue;
			}
			Thing val4 = null;
			MealCandidatePass mealCandidatePass2 = MealCandidatePass.None;
			int num2 = int.MaxValue;
			Thing val5 = FoodUtility.BestFoodInInventory(val2, eater, (FoodPreferability)7, (FoodPreferability)10, 0f, false, false);
			if (val5 != null && ClassifyStack(val5, out var pass, out var ticksUntilRot) && pass == MealCandidatePass.RotRescue)
			{
				val4 = val5;
				mealCandidatePass2 = pass;
				num2 = ticksUntilRot;
			}
			else
			{
				Thing val6 = FoodUtility.BestFoodInInventory(val2, eater, (FoodPreferability)2, (FoodPreferability)10, 0f, flag, false);
				if (val6 != null && ClassifyStack(val6, out var pass2, out var ticksUntilRot2))
				{
					val4 = val6;
					mealCandidatePass2 = pass2;
					num2 = ticksUntilRot2;
				}
			}
			if (val4 != null && ReservationUtility.CanReserve(getter, LocalTargetInfo.op_Implicit(val4), 1, -1, (ReservationLayerDef)null, false) && ReachabilityUtility.CanReach(getter, LocalTargetInfo.op_Implicit((Thing)(object)val2), (PathEndMode)2, (Danger)2, false, false, (TraverseMode)0))
			{
				int num3 = IntVec3Utility.ManhattanDistanceFlat(((Thing)getter).Position, ((Thing)val2).Position);
				if (val == null || MealsOnWheelsSelection.Compare(mealCandidatePass2, num2, num3, num, mealCandidatePass, bTicks, bDist, bIndex) < 0)
				{
					val = val4;
					mealCandidatePass = mealCandidatePass2;
					bTicks = num2;
					bDist = num3;
					bIndex = num;
				}
				num++;
			}
		}
		if (val != null)
		{
			foodSource = val;
			foodDef = FoodUtility.GetFinalIngestibleDef(foodSource, false);
			__result = true;
			if (s.shareMeetInMiddle)
			{
				SharedInventoryApproach.MaybeApproach(val, getter);
			}
			string text = string.Format("MealsOnWheels: {0} -> {1} carried by ", eater, ((Def)(foodDef?)).defName ?? "?");
			IThingHolder parentHolder = val.ParentHolder;
			HDLog.Dbg(text + $"{((Pawn_InventoryTracker)(((parentHolder is Pawn_InventoryTracker) ? parentHolder : null)?)).pawn} (pass {mealCandidatePass}).");
		}
	}

	private static bool ClassifyStack(Thing stack, out MealCandidatePass pass, out int ticksUntilRot)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Invalid comparison between Unknown and I4
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected I4, but got Unknown
		IngestibleProperties val = stack.def?.ingestible;
		if (val == null)
		{
			pass = MealCandidatePass.None;
			ticksUntilRot = int.MaxValue;
			return false;
		}
		CompRottable val2 = ThingCompUtility.TryGetComp<CompRottable>(stack);
		bool isFreshActiveRottable = val2 != null && val2.Active && (int)val2.Stage == 0;
		ticksUntilRot = ((val2 != null && val2.Active) ? val2.TicksUntilRotAtCurrentTemp : int.MaxValue);
		int preferabilityRank = (int)val.preferability;
		pass = MealsOnWheelsSelection.Classify(acceptable: true, preferabilityRank, isFreshActiveRottable, ticksUntilRot);
		return MealsOnWheelsSelection.IsCandidate(pass);
	}
}
