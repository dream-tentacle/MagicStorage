using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class InventorySurplus
{
	[ThreadStatic]
	private static Dictionary<ThingDef, int> tmpInvCountByDef;

	public static int SurplusOf(Pawn pawn, Thing thing)
	{
		return SurplusOf(pawn, thing, null, null);
	}

	internal static int SurplusOf(Pawn pawn, Thing thing, CompHauledToInventory comp, Dictionary<ThingDef, int> invCountByDef)
	{
		if (pawn?.inventory?.innerContainer == null || thing?.def == null)
		{
			return 0;
		}
		ThingDef def = thing.def;
		if (comp == null)
		{
			comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		}
		bool flag = comp?.PeekHashSet().Contains(thing) ?? false;
		if (comp != null)
		{
			int num = comp.KeptCountOf(def);
			if (num > 0)
			{
				int heldOfDef = InventoryCountOfDef(pawn, def, invCountByDef);
				return KeepCountPolicy.SurplusForKeptDef(num, heldOfDef, thing.stackCount);
			}
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && settings.TryGetItemRule(def, out var rule))
		{
			switch (rule.mode)
			{
			case ItemUnloadMode.UnloadAlways:
				return thing.stackCount;
			case ItemUnloadMode.KeepAll:
				if (!flag)
				{
					return 0;
				}
				break;
			case ItemUnloadMode.KeepAtMost:
			{
				int num2 = ((rule.amount >= 0) ? rule.amount : 0);
				int num3 = InventoryCountOfDef(pawn, def, invCountByDef) - num2;
				if (num3 > 0)
				{
					return Math.Min(thing.stackCount, num3);
				}
				return 0;
			}
			}
		}
		else
		{
			if (!flag && GrabYourToolCompat.IsCarriedTool(pawn, thing) && !SimpleSidearmsCompat.IsRememberedSidearm(pawn, thing))
			{
				return 0;
			}
			if (!flag && SurvivalToolsCompat.IsCarriedTool(pawn, thing))
			{
				return 0;
			}
			if (SimpleSidearmsCompat.IsActive && (def.IsRangedWeapon || def.IsMeleeWeapon) && SimpleSidearmsCompat.MemoryApiOk)
			{
				int num4 = SimpleSidearmsCompat.RememberedCount(pawn, def, thing.Stuff);
				Pawn_EquipmentTracker equipment = pawn.equipment;
				ThingWithComps val = ((equipment != null) ? equipment.Primary : null);
				bool flag2 = val != null && ((Thing)val).def == def && ((Thing)val).Stuff == thing.Stuff;
				int num5 = YieldRouter.InventoryCountOfPair((ThingOwner)(object)pawn.inventory.innerContainer, def, thing.Stuff);
				int num6 = SidearmKeepMath.SurplusForPair(num4, flag2, num5, thing.stackCount);
				if (settings != null && settings.verboseLogging)
				{
					HDLog.Dbg("SurplusOf weapon " + ((Def)def).defName + " (stuff=" + (((Def)(thing.Stuff?)).defName ?? "none") + ") for " + ((Entity)pawn).LabelShort + ": " + $"have={num5} keep={SimpleSidearmsCompat.InventoryKeepCount(pawn, def, thing.Stuff)} " + $"(remembered={num4}, primaryMatch={flag2}) " + $"-> surplus={num6}");
				}
				return num6;
			}
			if (IsManagedKeepItem(pawn, thing, flag))
			{
				return 0;
			}
		}
		int num7 = KeepCountOf(pawn, def) + FoodKeepCountOf(pawn, thing) + AnimalInteractFoodKeepCountOf(pawn, thing);
		if (num7 <= 0)
		{
			return thing.stackCount;
		}
		int val2 = InventoryCountOfDef(pawn, def, invCountByDef) - num7;
		return Math.Min(thing.stackCount, val2);
	}

	public static Dictionary<ThingDef, int> SurplusByDef(Pawn pawn)
	{
		Dictionary<ThingDef, int> dictionary = new Dictionary<ThingDef, int>();
		ThingOwner<Thing> val = pawn?.inventory?.innerContainer;
		CompHauledToInventory compHauledToInventory = ((pawn != null) ? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() : null);
		if (val == null || compHauledToInventory == null)
		{
			return dictionary;
		}
		foreach (Thing item in compHauledToInventory.PeekHashSet())
		{
			if (item != null && !item.Destroyed && item.def != null && ((ThingOwner)val).Contains(item))
			{
				int num = SurplusOf(pawn, item);
				if (num > 0)
				{
					dictionary[item.def] = (dictionary.TryGetValue(item.def, out var value) ? value : 0) + num;
				}
			}
		}
		return dictionary;
	}

	private static int InventoryCountOfDef(Pawn pawn, ThingDef def, Dictionary<ThingDef, int> invCountByDef)
	{
		if (invCountByDef != null)
		{
			if (!invCountByDef.TryGetValue(def, out var value))
			{
				return 0;
			}
			return value;
		}
		return YieldRouter.InventoryCountOfDef((ThingOwner)(object)pawn.inventory.innerContainer, def);
	}

	public static bool HasAnySurplus(Pawn pawn)
	{
		ThingOwner<Thing> val = pawn?.inventory?.innerContainer;
		if (val == null)
		{
			return false;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		Dictionary<ThingDef, int> invCountByDef = BuildInvCountByDef((ThingOwner)(object)val);
		for (int i = 0; i < ((ThingOwner)val).Count; i++)
		{
			Thing val2 = val[i];
			if (val2 != null && !val2.Destroyed && SurplusOf(pawn, val2, comp, invCountByDef) > 0)
			{
				return true;
			}
		}
		return false;
	}

	public static bool HasAnyRuledSurplus(Pawn pawn)
	{
		ThingOwner<Thing> val = pawn?.inventory?.innerContainer;
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (val == null || settings == null)
		{
			return false;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		Dictionary<ThingDef, int> invCountByDef = BuildInvCountByDef((ThingOwner)(object)val);
		for (int i = 0; i < ((ThingOwner)val).Count; i++)
		{
			Thing val2 = val[i];
			if (val2 != null && !val2.Destroyed && settings.RuleProducesSurplus(val2.def) && SurplusOf(pawn, val2, comp, invCountByDef) > 0)
			{
				return true;
			}
		}
		return false;
	}

	private static Dictionary<ThingDef, int> BuildInvCountByDef(ThingOwner inner)
	{
		Dictionary<ThingDef, int> dictionary = tmpInvCountByDef ?? (tmpInvCountByDef = new Dictionary<ThingDef, int>());
		dictionary.Clear();
		for (int i = 0; i < inner.Count; i++)
		{
			Thing val = inner[i];
			if (val?.def != null)
			{
				dictionary.TryGetValue(val.def, out var value);
				dictionary[val.def] = value + val.stackCount;
			}
		}
		return dictionary;
	}

	public static bool HasUnloadDestination(Pawn pawn, Thing thing)
	{
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || thing == null || thing.Destroyed || thing.def == null)
		{
			return false;
		}
		Rand.PushState();
		try
		{
			using (StorageBuildingFilter.PushContext(StorageFilterContext.Unload))
			{
				IntVec3 cell = default(IntVec3);
				IHaulDestination val = default(IHaulDestination);
				return StoreUtility.TryFindBestBetterStorageFor(thing, pawn, ((Thing)pawn).Map, (StoragePriority)0, ((Thing)pawn).Faction, ref cell, ref val, true) || TryFindDesperateHomeAreaCell(pawn, thing, out cell);
			}
		}
		finally
		{
			Rand.PopState();
		}
	}

	internal static bool TryFindDesperateHomeAreaCell(Pawn carrier, Thing item, out IntVec3 cell)
	{
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		Map map = ((Thing)carrier).Map;
		for (int i = -4; i < 20; i++)
		{
			int num = ((i < 0) ? Rand.RangeInclusive(0, 4) : i);
			IntVec3 val = ((Thing)carrier).Position + GenRadial.RadialPattern[num];
			if (GenGrid.InBounds(val, map) && ((Area)map.areaManager.Home)[val] && ReachabilityUtility.CanReach(carrier, LocalTargetInfo.op_Implicit(val), (PathEndMode)3, (Danger)3, false, false, (TraverseMode)0) && StoreUtility.GetSlotGroup(val, map) == null && StoreUtility.IsGoodStoreCell(val, map, item, carrier, ((Thing)carrier).Faction))
			{
				cell = val;
				return true;
			}
		}
		cell = IntVec3.Invalid;
		return false;
	}

	internal static bool IsManagedKeepItem(Pawn pawn, Thing thing, bool hdSwept)
	{
		ThingDef def = thing.def;
		if (!hdSwept)
		{
			if (def.IsDrug && pawn.RaceProps != null && pawn.RaceProps.IsFlesh && (AddictionUtility.IsAddicted(pawn, thing) || AddictionUtility.HasChemicalDependency(pawn, thing)))
			{
				return true;
			}
			if (SmartMedicineCompat.IsStockedUp(pawn, def))
			{
				return true;
			}
			if (DbhCompat.IsKeptDrink(thing))
			{
				return true;
			}
			if (CECompat.IsCarriedAmmo(thing))
			{
				return true;
			}
			if (YayoCombatCompat.IsCarriedAmmo(thing))
			{
				return true;
			}
		}
		if (!SimpleSidearmsCompat.MemoryApiOk && SimpleSidearmsCompat.IsKeptWeapon(pawn, thing))
		{
			return true;
		}
		return false;
	}

	public static int KeepCountOf(Pawn pawn, ThingDef def)
	{
		int num = 0;
		int num2 = num;
		Pawn_DrugPolicyTracker drugs = pawn.drugs;
		num = num2 + DrugPolicyLookup.TakeToInventoryTotal((drugs != null) ? drugs.CurrentPolicy : null, def);
		Dictionary<InventoryStockGroupDef, InventoryStockEntry> dictionary = pawn.inventoryStock?.stockEntries;
		if (dictionary != null)
		{
			foreach (InventoryStockEntry value in dictionary.Values)
			{
				if (value != null && value.thingDef == def)
				{
					num += value.count;
				}
			}
		}
		num += CECompat.LoadoutKeepCount(pawn, def);
		num += ItemPolicyCompat.KeepCount(pawn, def);
		return num + CompositableLoadoutsCompat.KeepCount(pawn, def);
	}

	public static int FoodKeepCountOf(Pawn pawn, Thing thing)
	{
		if (!pawn.IsColonist || pawn.needs?.food == null)
		{
			return 0;
		}
		ThingDef def = thing.def;
		if (!def.IsNutritionGivingIngestible || def.IsDrug || !JobGiver_PackFood.IsGoodPackableFoodFor(thing, pawn, false))
		{
			return 0;
		}
		float inventoryPackableFoodNutrition = JobGiver_PackFood.GetInventoryPackableFoodNutrition(pawn);
		float maxLevel = ((Need)pawn.needs.food).MaxLevel;
		float statValue = StatExtension.GetStatValue(thing, StatDefOf.Nutrition, true, -1);
		return FoodKeepMath.KeepCount(inventoryPackableFoodNutrition, maxLevel, statValue, thing.stackCount);
	}

	public static int AnimalInteractFoodKeepCountOf(Pawn pawn, Thing thing)
	{
		if (!AnimalInteractFood.IsInteractFood(thing.def))
		{
			return 0;
		}
		float num = AnimalInteractFood.ReserveNutritionFor(pawn);
		if (num <= 0f)
		{
			return 0;
		}
		float statValue = StatExtension.GetStatValue(thing, StatDefOf.Nutrition, true, -1);
		return AnimalInteractFoodKeepMath.KeepCount(num, statValue, thing.stackCount);
	}
}
