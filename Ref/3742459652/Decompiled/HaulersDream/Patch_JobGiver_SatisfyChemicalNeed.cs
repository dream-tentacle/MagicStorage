using System;
using System.Collections.Generic;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_SatisfyChemicalNeed), "TryGiveJob")]
public static class Patch_JobGiver_SatisfyChemicalNeed
{
	[HarmonyPriority(200)]
	public static void Postfix(ref Job __result, Pawn pawn)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.drugsForWithdrawal || __result != null || pawn == null || !pawn.IsColonist || ((Thing)pawn).Map == null || pawn.needs == null || pawn.inventory == null)
		{
			return;
		}
		Job val = __result;
		try
		{
			Job val2 = TryBuildTakeFromHolderJob(pawn, settings);
			if (val2 != null)
			{
				__result = val2;
			}
		}
		catch (Exception ex)
		{
			__result = val;
			HDGuard.SeamDegraded(ex, "JobGiver_SatisfyChemicalNeed.TryGiveJob (HD kept-drug withdrawal access)", pawn, "kept vanilla's result (no drug found by HD), so drug selection itself keeps working.");
		}
	}

	private static Exception Finalizer(Exception __exception, Pawn pawn)
	{
		return HDGuard.SeamThrew(__exception, "JobGiver_SatisfyChemicalNeed.TryGiveJob (HD kept-drug withdrawal access)", pawn, "this pawn's drug search failed this scan.");
	}

	private static Job TryBuildTakeFromHolderJob(Pawn pawn, HaulersDreamSettings s)
	{
		List<Need> allNeeds = pawn.needs.AllNeeds;
		if (allNeeds == null || allNeeds.Count == 0)
		{
			return null;
		}
		float afterLevel = float.NegativeInfinity;
		int afterIndex = -1;
		for (int i = 0; i < allNeeds.Count; i++)
		{
			int index;
			Need_Chemical val = NextMostUrgentNeed(allNeeds, afterLevel, afterIndex, out index);
			if (val == null)
			{
				return null;
			}
			afterLevel = ((Need)val).CurLevel;
			afterIndex = index;
			Job val2 = TryBuildForNeed(pawn, val, s);
			if (val2 != null)
			{
				return val2;
			}
		}
		return null;
	}

	private static Need_Chemical NextMostUrgentNeed(List<Need> needs, float afterLevel, int afterIndex, out int index)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Invalid comparison between Unknown and I4
		Need_Chemical val = null;
		index = -1;
		for (int i = 0; i < needs.Count; i++)
		{
			Need obj = needs[i];
			Need_Chemical val2 = (Need_Chemical)(object)((obj is Need_Chemical) ? obj : null);
			if (val2 != null && (int)val2.CurCategory <= 1 && val2.AddictionHediff != null)
			{
				float curLevel = ((Need)val2).CurLevel;
				if (!(curLevel < afterLevel) && (curLevel != afterLevel || i > afterIndex) && (val == null || curLevel < ((Need)val).CurLevel || (curLevel == ((Need)val).CurLevel && i < index)))
				{
					val = val2;
					index = i;
				}
			}
		}
		return val;
	}

	private static Job TryBuildForNeed(Pawn pawn, Need_Chemical need, HaulersDreamSettings s)
	{
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		Hediff_Addiction addictionHediff = need.AddictionHediff;
		Map map = ((Thing)pawn).Map;
		bool drugsForWithdrawal = s.drugsForWithdrawal;
		bool vanillaFoundDrug = false;
		bool seekerHasChemicalNeed = addictionHediff != null;
		Thing val = null;
		int num = int.MaxValue;
		List<Pawn> list = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
		for (int i = 0; i < list.Count; i++)
		{
			Pawn val2 = list[i];
			bool flag = IsEligibleHolder(val2, pawn);
			if (!flag)
			{
				continue;
			}
			CompHauledToInventory comp = ((ThingWithComps)val2).GetComp<CompHauledToInventory>();
			ThingOwner<Thing> val3 = val2.inventory?.innerContainer;
			if (comp == null || val3 == null || ((ThingOwner)val3).Count == 0)
			{
				continue;
			}
			Thing val4 = null;
			for (int j = 0; j < ((ThingOwner)val3).Count; j++)
			{
				Thing val5 = val3[j];
				if (IsDrugForAddiction(pawn, addictionHediff, val5) && DrugSharePolicy.Evaluate(drugsForWithdrawal, vanillaFoundDrug, seekerHasChemicalNeed, flag, IsPinnedByHd(val2, comp, val5), val5.stackCount) == DrugShareVerdict.Allow && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(val5), 1, -1, (ReservationLayerDef)null, false) && (val4 == null || val5.thingIDNumber < val4.thingIDNumber))
				{
					val4 = val5;
				}
			}
			if (val4 != null && ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit((Thing)(object)val2), (PathEndMode)2, (Danger)2, false, false, (TraverseMode)0))
			{
				int num2 = IntVec3Utility.ManhattanDistanceFlat(((Thing)pawn).Position, ((Thing)val2).Position);
				if (val == null || num2 < num || (num2 == num && val4.thingIDNumber < val.thingIDNumber))
				{
					val = val4;
					num = num2;
				}
			}
		}
		if (val == null)
		{
			return null;
		}
		IThingHolder parentHolder = val.ParentHolder;
		Pawn val6 = ((Pawn_InventoryTracker)(((parentHolder is Pawn_InventoryTracker) ? parentHolder : null)?)).pawn;
		if (val6 == null || val6 == pawn)
		{
			return null;
		}
		Job val7 = JobMaker.MakeJob(JobDefOf.TakeFromOtherInventory, LocalTargetInfo.op_Implicit(val), LocalTargetInfo.op_Implicit((Thing)(object)val6));
		val7.count = DrugSharePolicy.UnitsToTake(val.stackCount);
		if (s.shareMeetInMiddle)
		{
			SharedInventoryApproach.MaybeApproach(val, pawn);
		}
		HDLog.Dbg(string.Format("KeptDrugShare: {0} -> {1} x{2} carried by {3} ", pawn, ((Def)(val.def?)).defName ?? "?", val7.count, val6) + "(addiction " + (((Def)(((Hediff)addictionHediff).def?)).defName ?? "?") + ").");
		return val7;
	}

	private static bool IsEligibleHolder(Pawn holder, Pawn seeker)
	{
		if (((holder != null) ? holder.RaceProps : null) == null || holder.RaceProps.Animal)
		{
			return false;
		}
		if (!InventoryShare.IsEligibleCarrier(holder, seeker))
		{
			return false;
		}
		if (VehicleFrameworkCompat.IsVehicle((Thing)(object)holder) || VehicleFrameworkCompat.InVehicle((Thing)(object)holder))
		{
			return false;
		}
		if (CaravanFormingUtility.IsFormingCaravan(holder))
		{
			return false;
		}
		if (ForbidUtility.IsForbidden((Thing)(object)holder, seeker))
		{
			return false;
		}
		return true;
	}

	private static bool IsPinnedByHd(Pawn holder, CompHauledToInventory comp, Thing stack)
	{
		if (comp != null && stack?.def != null)
		{
			if (!comp.IsKeptDef(stack.def))
			{
				if (comp.GetHashSet().Contains(stack))
				{
					return InventorySurplus.SurplusOf(holder, stack) > 0;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private static bool IsDrugForAddiction(Pawn pawn, Hediff_Addiction addiction, Thing drug)
	{
		if (drug?.def == null || !drug.def.IsDrug)
		{
			return false;
		}
		CompDrug val = ThingCompUtility.TryGetComp<CompDrug>(drug);
		if (((val == null) ? null : val.Props?.chemical) == null)
		{
			return false;
		}
		if (val.Props.chemical.addictionHediff != ((Hediff)addiction).def)
		{
			return false;
		}
		Pawn_DrugPolicyTracker drugs = pawn.drugs;
		DrugPolicy obj = ((drugs != null) ? drugs.CurrentPolicy : null);
		DrugPolicyEntry val2 = DrugPolicyLookup.EntryFor(obj, drug.def);
		bool hasPolicy = obj != null;
		bool entryPresent = val2 != null;
		bool entryAllowedForAddiction = val2?.allowedForAddiction ?? false;
		bool hasStory = pawn.story != null;
		Pawn_StoryTracker story = pawn.story;
		int? obj2;
		if (story == null)
		{
			obj2 = null;
		}
		else
		{
			TraitSet traits = story.traits;
			obj2 = ((traits != null) ? new int?(traits.DegreeOfTrait(TraitDefOf.DrugDesire)) : ((int?)null));
		}
		int? num = obj2;
		bool policyBlocks = DrugAllowancePolicy.BlocksAddictionUse(hasPolicy, entryPresent, entryAllowedForAddiction, hasStory, num.GetValueOrDefault(), pawn.InMentalState && (pawn.MentalStateDef?.ignoreDrugPolicy ?? false));
		return DrugAllowancePolicy.MayRouteToDrug(val2 != null, policyBlocks);
	}
}
