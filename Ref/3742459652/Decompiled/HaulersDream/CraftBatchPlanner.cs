using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class CraftBatchPlanner
{
	public static bool CanBatch(Bill bill)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		if (bill == null || ((object)bill).GetType() != typeof(Bill_Production) || bill.recipe == null)
		{
			return false;
		}
		BillRepeatModeDef repeatMode = ((Bill_Production)bill).repeatMode;
		if (repeatMode != BillRepeatModeDefOf.RepeatCount && repeatMode != BillRepeatModeDefOf.TargetCount && repeatMode != BillRepeatModeDefOf.Forever)
		{
			return false;
		}
		RecipeDef recipe = bill.recipe;
		if (recipe.UsesUnfinishedThing)
		{
			return false;
		}
		if (recipe.ingredients == null || recipe.ingredients.Count == 0)
		{
			return false;
		}
		if (recipe.ignoreIngredientCountTakeEntireStacks)
		{
			return false;
		}
		if (GenList.NullOrEmpty<ThingDefCountClass>((IList<ThingDefCountClass>)recipe.products))
		{
			return !GenList.NullOrEmpty<SpecialProductType>((IList<SpecialProductType>)recipe.specialProducts);
		}
		return true;
	}

	public static bool BatchModeAvailable(Bill bill)
	{
		if (CanBatch(bill) && !CommonSenseCompat.BatchSuppressedByCommonSense)
		{
			return !BillRouteGate.BatchSuppressedByBench(bill);
		}
		return false;
	}

	public static bool AnyBatchableBill(Building_WorkTable bench)
	{
		object obj;
		if (bench == null)
		{
			obj = null;
		}
		else
		{
			BillStack billStack = bench.BillStack;
			obj = ((billStack != null) ? billStack.Bills : null);
		}
		List<Bill> list = (List<Bill>)obj;
		if (list == null)
		{
			return false;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (CanBatch(list[i]))
			{
				return true;
			}
		}
		return false;
	}

	public static bool CanPawnBatch(Pawn pawn, Bill bill)
	{
		if (pawn == null || !CanBatch(bill))
		{
			return false;
		}
		if (((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null)
		{
			return false;
		}
		if (!bill.suspended && bill.ShouldDoNow() && bill.PawnAllowedToStartAnew(pawn) && bill.recipe.PawnSatisfiesSkillRequirements(pawn))
		{
			if (bill.recipe.requiredGiverWorkType != null)
			{
				return !WorkCapabilityProbe.IsDisabled(pawn, bill.recipe.requiredGiverWorkType);
			}
			return true;
		}
		return false;
	}

	public static bool AnyBatchableBillForPawn(Pawn pawn, Building_WorkTable bench)
	{
		object obj;
		if (bench == null)
		{
			obj = null;
		}
		else
		{
			BillStack billStack = bench.BillStack;
			obj = ((billStack != null) ? billStack.Bills : null);
		}
		List<Bill> list = (List<Bill>)obj;
		if (list == null)
		{
			return false;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (CanPawnBatch(pawn, list[i]))
			{
				return true;
			}
		}
		return false;
	}

	public static int EffectiveProductCount(Bill_Production bp)
	{
		object obj;
		if (bp == null)
		{
			obj = null;
		}
		else
		{
			RecipeDef recipe = ((Bill)bp).recipe;
			obj = ((recipe != null) ? recipe.WorkerCounter : null);
		}
		RecipeWorkerCounter val = (RecipeWorkerCounter)obj;
		if (((bp != null) ? ((Bill)bp).Map : null) == null || val == null)
		{
			return 0;
		}
		return val.CountProducts(bp);
	}

	public static int BankedInFlightProductCount(Bill_Production bp)
	{
		Map val = ((bp != null) ? ((Bill)bp).Map : null);
		object obj;
		if (bp == null)
		{
			obj = null;
		}
		else
		{
			RecipeDef recipe = ((Bill)bp).recipe;
			obj = ((recipe != null) ? recipe.WorkerCounter : null);
		}
		RecipeWorkerCounter val2 = (RecipeWorkerCounter)obj;
		if (val == null || val2 == null || !val2.CanCountProducts(bp))
		{
			return 0;
		}
		ThingDef thingDef = ((Bill)bp).recipe.products[0].thingDef;
		if (thingDef == null)
		{
			return 0;
		}
		int num = 0;
		foreach (Pawn item in val.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
		{
			HashSet<Thing> hashSet = ((ThingWithComps)item).GetComp<CompHauledToInventory>()?.PeekHashSet();
			if (hashSet == null)
			{
				continue;
			}
			foreach (Thing item2 in hashSet)
			{
				if (item2 != null && !item2.Destroyed && item2.ParentHolder is Pawn_InventoryTracker && val2.CountValidThing(item2, bp, thingDef))
				{
					int num2 = InventorySurplus.SurplusOf(item, item2);
					if (num2 > 0)
					{
						num += num2;
					}
				}
			}
		}
		return num;
	}

	public static CraftBatchPlan Resolve(Pawn pawn, Building_WorkTable bench, Bill bill, int requestedReps, int timeoutTicks)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_027f: Unknown result type (might be due to invalid IL or missing references)
		CraftBatchPlan craftBatchPlan = new CraftBatchPlan
		{
			bill = bill,
			recipe = bill?.recipe,
			requestedReps = Mathf.Max(0, requestedReps),
			timeoutTicks = Mathf.Max(0, timeoutTicks),
			resolvedReps = 0
		};
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || bench == null || bill == null || !CanBatch(bill))
		{
			craftBatchPlan.blockReason = TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.BlockUnsupported"));
			return craftBatchPlan;
		}
		if (bill.suspended || !bill.ShouldDoNow())
		{
			craftBatchPlan.blockReason = TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.BlockBillNotActive"));
			return craftBatchPlan;
		}
		Bill_Production val = (Bill_Production)(object)((bill is Bill_Production) ? bill : null);
		if (val != null)
		{
			if (val.repeatMode == BillRepeatModeDefOf.RepeatCount)
			{
				craftBatchPlan.billReps = val.repeatCount;
			}
			else if (val.repeatMode == BillRepeatModeDefOf.TargetCount && !GenList.NullOrEmpty<ThingDefCountClass>((IList<ThingDefCountClass>)bill.recipe.products) && bill.recipe.products[0].count > 0)
			{
				int num = HaulersDreamGameComponent.Instance?.BatchOvershootOf((Bill)(object)val) ?? 0;
				int num2 = val.targetCount + num - EffectiveProductCount(val);
				craftBatchPlan.billReps = ((num2 > 0) ? Mathf.CeilToInt((float)num2 / (float)bill.recipe.products[0].count) : 0);
			}
		}
		RecipeDef recipe = bill.recipe;
		if (recipe.allowMixingIngredients)
		{
			return ResolveMixing(craftBatchPlan, pawn, bench, bill, recipe);
		}
		float num3 = 0f;
		for (int i = 0; i < recipe.ingredients.Count; i++)
		{
			IngredientCount val2 = recipe.ingredients[i];
			ThingDef val3 = null;
			int num4 = -1;
			int num5 = 0;
			int num6 = 0;
			foreach (ThingDef allowedThingDef in val2.filter.AllowedThingDefs)
			{
				if (allowedThingDef == null || (!val2.IsFixedIngredient && bill.ingredientFilter != null && !bill.ingredientFilter.Allows(allowedThingDef)))
				{
					continue;
				}
				int num7 = val2.CountRequiredOfFor(allowedThingDef, recipe, bill);
				if (num7 <= 0)
				{
					continue;
				}
				int num8 = AvailableUnits(pawn, allowedThingDef, bill, bench);
				if (num8 >= num7)
				{
					int num9 = num8 / num7;
					if (num9 > num4 || (num9 == num4 && num8 > num5) || (num9 == num4 && num8 == num5 && val3 != null && ((Def)allowedThingDef).shortHash < ((Def)val3).shortHash))
					{
						num4 = num9;
						val3 = allowedThingDef;
						num5 = num8;
						num6 = num7;
					}
				}
			}
			if (val3 == null)
			{
				craftBatchPlan.blockReason = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.BlockNoIngredient", NamedArgument.op_Implicit(val2.filter.Summary)));
				return craftBatchPlan;
			}
			craftBatchPlan.ingredientDefs.Add(val3);
			craftBatchPlan.perRepCounts.Add(num6);
			num3 += (float)num6 * StatExtension.GetStatValueAbstract((BuildableDef)(object)val3, StatDefOf.Mass, (ThingDef)null);
		}
		Dictionary<ThingDef, int> dictionary = new Dictionary<ThingDef, int>();
		List<int> list = new List<int>();
		List<int> list2 = new List<int>(craftBatchPlan.ingredientDefs.Count);
		for (int j = 0; j < craftBatchPlan.ingredientDefs.Count; j++)
		{
			ThingDef val4 = craftBatchPlan.ingredientDefs[j];
			if (!dictionary.TryGetValue(val4, out var value))
			{
				value = (dictionary[val4] = list.Count);
				list.Add(AvailableUnits(pawn, val4, bill, bench));
			}
			list2.Add(value);
		}
		craftBatchPlan.availabilityReps = CraftBatchMath.ScarcestDefReps(list2, craftBatchPlan.perRepCounts, list);
		float num10 = 0f;
		if (CECompat.IsActive)
		{
			for (int k = 0; k < craftBatchPlan.ingredientDefs.Count; k++)
			{
				num10 += (float)craftBatchPlan.perRepCounts[k] * CECompat.BulkPerUnitAbstract(craftBatchPlan.ingredientDefs[k]);
			}
		}
		FinalizeCaps(craftBatchPlan, pawn, bench, recipe, num3, num10);
		return craftBatchPlan;
	}

	private static void FinalizeCaps(CraftBatchPlan plan, Pawn pawn, Building_WorkTable bench, RecipeDef recipe, float massPerRep, float bulkPerRep)
	{
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		plan.massPerRepKg = massPerRep;
		plan.ticksPerRep = EstimateTicksPerRep(pawn, bench, recipe);
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		float maxCapacityKg = CarryCapacity.Of(pawn);
		float baseCapKg = CarryMath.EffectiveCapacity(maxCapacityKg, settings?.carryLimitFraction ?? 1f);
		float currentMassKg = MassUtility.GearAndInventoryMass(pawn);
		int overloadLevel = ((settings != null) ? OverloadGate.EffectiveLevel(settings) : 10);
		plan.massReps = CraftBatchMath.RepsByMass(overloadLevel, maxCapacityKg, baseCapKg, currentMassKg, massPerRep, plan.requestedReps);
		if (CECompat.IsActive && bulkPerRep > 0f)
		{
			int num = (int)Math.Floor(CECompat.AvailableBulk(pawn) / bulkPerRep);
			if (num < plan.massReps)
			{
				plan.massReps = ((num >= 0) ? num : 0);
			}
		}
		plan.timeoutReps = CraftBatchMath.RepsByTimeout(plan.ticksPerRep, plan.timeoutTicks);
		plan.resolvedReps = CraftBatchMath.Resolve(plan.requestedReps, plan.availabilityReps, int.MaxValue, plan.timeoutReps);
		if (plan.billReps < plan.resolvedReps)
		{
			plan.resolvedReps = plan.billReps;
		}
		if (settings != null && OverloadGate.NoOverload(settings) && plan.resolvedReps >= 1 && plan.massReps < 1)
		{
			plan.resolvedReps = 0;
		}
		plan.feasible = plan.resolvedReps >= 1;
		if (!plan.feasible && plan.blockReason == null)
		{
			plan.blockReason = TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.BlockNoReps"));
		}
	}

	private static CraftBatchPlan ResolveMixing(CraftBatchPlan plan, Pawn pawn, Building_WorkTable bench, Bill bill, RecipeDef recipe)
	{
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		plan.mixingRecipe = true;
		int num = int.MaxValue;
		float num2 = 0f;
		float num3 = 0f;
		IngredientValueGetter ingredientValueGetter = recipe.IngredientValueGetter;
		for (int i = 0; i < recipe.ingredients.Count; i++)
		{
			IngredientCount val = recipe.ingredients[i];
			double num4 = val.GetBaseCount();
			double num5 = 0.0;
			float num6 = 0f;
			float num7 = 0f;
			foreach (ThingDef allowedThingDef in val.filter.AllowedThingDefs)
			{
				if (allowedThingDef == null || (!val.IsFixedIngredient && bill.ingredientFilter != null && !bill.ingredientFilter.Allows(allowedThingDef)))
				{
					continue;
				}
				float num8 = ingredientValueGetter.ValuePerUnitOf(allowedThingDef);
				if (num8 <= 0f)
				{
					continue;
				}
				int num9 = AvailableUnits(pawn, allowedThingDef, bill, bench);
				if (num9 > 0)
				{
					num5 += (double)num9 * (double)num8;
				}
				float num10 = StatExtension.GetStatValueAbstract((BuildableDef)(object)allowedThingDef, StatDefOf.Mass, (ThingDef)null) / num8;
				if (num10 > num6)
				{
					num6 = num10;
				}
				if (CECompat.IsActive)
				{
					float num11 = CECompat.BulkPerUnitAbstract(allowedThingDef) / num8;
					if (num11 > num7)
					{
						num7 = num11;
					}
				}
			}
			int num12 = CraftBatchMath.MixAvailableReps(num4, num5);
			if (num12 <= 0)
			{
				plan.blockReason = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.BlockNoIngredient", NamedArgument.op_Implicit(val.filter.Summary)));
				return plan;
			}
			if (num12 < num)
			{
				num = num12;
			}
			if (num4 > 0.0)
			{
				num2 += (float)num4 * num6;
				num3 += (float)num4 * num7;
			}
		}
		plan.availabilityReps = num;
		FinalizeCaps(plan, pawn, bench, recipe, num2, num3);
		return plan;
	}

	public static int MaxAvailableReps(Pawn pawn, Building_WorkTable bench, Bill bill)
	{
		CraftBatchPlan craftBatchPlan = Resolve(pawn, bench, bill, 9999, 0);
		int num = Mathf.Min(craftBatchPlan.availabilityReps, craftBatchPlan.billReps);
		if (num == int.MaxValue)
		{
			return 9999;
		}
		return Mathf.Max(0, num);
	}

	public static int AvailableUnits(Pawn pawn, ThingDef def, Bill bill, Building_WorkTable bench)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		Map val = ((pawn != null) ? ((Thing)pawn).Map : null);
		if (val == null || def == null)
		{
			return 0;
		}
		float num = ((bill != null) ? (bill.ingredientSearchRadius * bill.ingredientSearchRadius) : float.MaxValue);
		IntVec3 val2 = ((bench != null) ? ((Thing)bench).Position : ((Thing)pawn).Position);
		int num2 = 0;
		List<Thing> list = val.listerThings.ThingsOfDef(def);
		for (int i = 0; i < list.Count; i++)
		{
			Thing val3 = list[i];
			if (val3 != null && val3.Spawned && !ForbidUtility.IsForbidden(val3, pawn) && (bill == null || InventoryShare.IsUsableForBill(val3, bill)))
			{
				IntVec3 val4 = val3.Position - val2;
				if (!((float)((IntVec3)(ref val4)).LengthHorizontalSquared >= num) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(val3), 1, -1, (ReservationLayerDef)null, false))
				{
					num2 += val3.stackCount;
				}
			}
		}
		ThingOwner<Thing> val5 = pawn.inventory?.innerContainer;
		if (val5 != null)
		{
			for (int j = 0; j < ((ThingOwner)val5).Count; j++)
			{
				if (val5[j]?.def == def && (bill == null || InventoryShare.IsUsableForBill(val5[j], bill)))
				{
					num2 += val5[j].stackCount;
				}
			}
		}
		return num2;
	}

	public static int EstimateTicksPerRep(Pawn pawn, Building_WorkTable bench, RecipeDef recipe)
	{
		float num = recipe.WorkAmountTotal((Thing)null);
		float num2 = ((recipe.workSpeedStat != null) ? Mathf.Max(0.01f, StatExtension.GetStatValue((Thing)(object)pawn, recipe.workSpeedStat, true, -1)) : 1f);
		if (recipe.workTableSpeedStat != null && bench != null)
		{
			num2 *= Mathf.Max(0.01f, StatExtension.GetStatValue((Thing)(object)bench, recipe.workTableSpeedStat, true, -1));
		}
		return Mathf.Max(1, Mathf.RoundToInt(num / Mathf.Max(0.0001f, num2)));
	}
}
