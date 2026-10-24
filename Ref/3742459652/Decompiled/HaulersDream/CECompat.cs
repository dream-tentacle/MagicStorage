using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class CECompat
{
	private static bool initialized;

	private static bool active;

	private static Type compInventoryType;

	private static MethodInfo canFitInInventory;

	private static MethodInfo getAvailableBulk;

	private static MethodInfo notifyHoldTracker;

	private static MethodInfo getLoadout;

	private static MethodInfo loadoutSlotsGetter;

	private static MethodInfo slotThingDefGetter;

	private static MethodInfo slotCountGetter;

	private static MethodInfo slotGenericDefGetter;

	private static MethodInfo lambdaGetter;

	private static StatDef bulkStat;

	private static Type ammoDefType;

	[ThreadStatic]
	private static Pawn lastCompPawn;

	[ThreadStatic]
	private static ThingComp lastCompInventory;

	[ThreadStatic]
	private static object[] fitArgs;

	private static readonly object BoxedFalse = false;

	public static bool IsActive
	{
		get
		{
			if (!initialized)
			{
				Init();
			}
			return active;
		}
	}

	private static void Init()
	{
		initialized = true;
		active = false;
		compInventoryType = AccessTools.TypeByName("CombatExtended.CompInventory");
		if (compInventoryType == null)
		{
			return;
		}
		ammoDefType = AccessTools.TypeByName("CombatExtended.AmmoDef");
		canFitInInventory = AccessTools.Method(compInventoryType, "CanFitInInventory", new Type[4]
		{
			typeof(Thing),
			typeof(int).MakeByRefType(),
			typeof(bool),
			typeof(bool)
		}, (Type[])null);
		getAvailableBulk = AccessTools.Method(compInventoryType, "GetAvailableBulk", new Type[1] { typeof(bool) }, (Type[])null);
		Type type = AccessTools.TypeByName("CombatExtended.Utility_HoldTracker");
		if (type != null)
		{
			notifyHoldTracker = AccessTools.Method(type, "Notify_HoldTrackerItem", new Type[3]
			{
				typeof(Pawn),
				typeof(Thing),
				typeof(int)
			}, (Type[])null);
		}
		Type type2 = AccessTools.TypeByName("CombatExtended.Utility_Loadouts");
		if (type2 != null)
		{
			getLoadout = AccessTools.Method(type2, "GetLoadout", new Type[1] { typeof(Pawn) }, (Type[])null);
		}
		Type type3 = AccessTools.TypeByName("CombatExtended.Loadout");
		if (type3 != null)
		{
			loadoutSlotsGetter = AccessTools.PropertyGetter(type3, "Slots");
		}
		Type type4 = AccessTools.TypeByName("CombatExtended.LoadoutSlot");
		if (type4 != null)
		{
			slotThingDefGetter = AccessTools.PropertyGetter(type4, "thingDef");
			slotCountGetter = AccessTools.PropertyGetter(type4, "count");
			slotGenericDefGetter = AccessTools.PropertyGetter(type4, "genericDef");
		}
		Type type5 = AccessTools.TypeByName("CombatExtended.LoadoutGenericDef");
		if (type5 != null)
		{
			lambdaGetter = AccessTools.PropertyGetter(type5, "lambda");
		}
		bulkStat = DefDatabase<StatDef>.GetNamedSilentFail("Bulk");
		active = canFitInInventory != null;
		if (active)
		{
			HDLog.Msg("Combat Extended detected — inventory loading defers to CE's weight+bulk capacity, smart overload stands down, HoldTracker integration on.");
			if (notifyHoldTracker == null)
			{
				HDLog.Warn("Combat Extended present but Utility_HoldTracker.Notify_HoldTrackerItem(Pawn, Thing, int) did not resolve; HD cannot shield its scooped cargo from CE's loadout drop — a CE rename likely. Please report it. HD continues; CE-loadout pawns may drop scooped goods.");
			}
			if (getLoadout == null || loadoutSlotsGetter == null || slotThingDefGetter == null || slotCountGetter == null)
			{
				HDLog.Warn("Combat Extended present but its Loadout API (Utility_Loadouts.GetLoadout / Loadout.Slots / LoadoutSlot.thingDef|count) did not fully resolve; HD cannot read CE loadouts to keep loadout ammo with the pawn — a CE rename likely. Please report it. HD continues.");
			}
			if (slotGenericDefGetter == null || lambdaGetter == null)
			{
				HDLog.Warn("Combat Extended present but its generic-loadout API (LoadoutSlot.genericDef / LoadoutGenericDef.lambda) did not fully resolve; HD cannot read CE's generic loadout slots (e.g. GenericMeal) — meals and other generic-slot items may loop between inventory and storage. A CE rename likely. Please report it. HD continues.");
			}
		}
		else
		{
			HDLog.Warn("Combat Extended present but CompInventory.CanFitInInventory(Thing, out int, bool, bool) did not resolve; CE weight+bulk-aware loading is OFF (falling back to vanilla mass math).");
		}
	}

	private static ThingComp CompInventoryOf(Pawn pawn)
	{
		if (pawn == null || compInventoryType == null)
		{
			return null;
		}
		if (pawn == lastCompPawn)
		{
			return lastCompInventory;
		}
		List<ThingComp> allComps = ((ThingWithComps)pawn).AllComps;
		if (allComps == null)
		{
			return null;
		}
		ThingComp result = null;
		for (int i = 0; i < allComps.Count; i++)
		{
			if (compInventoryType.IsInstanceOfType(allComps[i]))
			{
				result = allComps[i];
				break;
			}
		}
		lastCompPawn = pawn;
		lastCompInventory = result;
		return result;
	}

	public static int MaxFitCount(Pawn pawn, Thing thing)
	{
		if (!IsActive || pawn == null || thing == null)
		{
			return int.MaxValue;
		}
		ThingComp val = CompInventoryOf(pawn);
		if (val == null)
		{
			return int.MaxValue;
		}
		object[] array = fitArgs;
		if (array == null)
		{
			array = (fitArgs = new object[4]);
			array[2] = BoxedFalse;
			array[3] = BoxedFalse;
		}
		array[0] = thing;
		array[1] = 0;
		canFitInInventory.Invoke(val, array);
		int num = (int)array[1];
		if (num >= 0)
		{
			return num;
		}
		return 0;
	}

	public static float AvailableBulk(Pawn pawn)
	{
		if (!IsActive || pawn == null || getAvailableBulk == null)
		{
			return float.PositiveInfinity;
		}
		ThingComp val = CompInventoryOf(pawn);
		if (val == null)
		{
			return float.PositiveInfinity;
		}
		return (float)getAvailableBulk.Invoke(val, new object[1] { true });
	}

	public static bool IsCarriedAmmo(Thing thing)
	{
		if (thing?.def == null)
		{
			return false;
		}
		if (!initialized)
		{
			Init();
		}
		if (ammoDefType != null)
		{
			return ammoDefType.IsInstanceOfType(thing.def);
		}
		return false;
	}

	public static float BulkPerUnit(Thing thing)
	{
		if (!IsActive || thing == null || bulkStat == null)
		{
			return 0f;
		}
		return StatExtension.GetStatValue(thing, bulkStat, true, -1);
	}

	public static float BulkPerUnitAbstract(ThingDef def)
	{
		if (!IsActive || def == null || bulkStat == null)
		{
			return 0f;
		}
		return StatExtension.GetStatValueAbstract((BuildableDef)(object)def, bulkStat, (ThingDef)null);
	}

	public static int FitUnitsByBulk(Pawn pawn, ThingDef def)
	{
		if (!IsActive || pawn == null || def == null)
		{
			return int.MaxValue;
		}
		float num = BulkPerUnitAbstract(def);
		if (num <= 0f)
		{
			return int.MaxValue;
		}
		return CarryMath.UnitsThatFitBulk(AvailableBulk(pawn), num);
	}

	public static void NotifyHeld(Pawn pawn, Thing item, int count)
	{
		if (IsActive && !(notifyHoldTracker == null) && pawn != null && item != null && count > 0)
		{
			notifyHoldTracker.Invoke(null, new object[3] { pawn, item, count });
		}
	}

	public static int LoadoutKeepCount(Pawn pawn, ThingDef def)
	{
		if (!IsActive || pawn == null || def == null || getLoadout == null || loadoutSlotsGetter == null || slotThingDefGetter == null || slotCountGetter == null)
		{
			return 0;
		}
		object obj = getLoadout.Invoke(null, new object[1] { pawn });
		if (obj == null)
		{
			return 0;
		}
		int num = 0;
		if (loadoutSlotsGetter.Invoke(obj, null) is IEnumerable enumerable)
		{
			foreach (object item in enumerable)
			{
				if (item == null)
				{
					continue;
				}
				object obj2 = slotThingDefGetter.Invoke(item, null);
				if (((obj2 is ThingDef) ? obj2 : null) == def)
				{
					num += (int)slotCountGetter.Invoke(item, null);
				}
				else
				{
					if (!(slotGenericDefGetter != null) || !(lambdaGetter != null))
					{
						continue;
					}
					object obj3 = slotGenericDefGetter.Invoke(item, null);
					if (obj3 == null)
					{
						continue;
					}
					try
					{
						if (lambdaGetter.Invoke(obj3, null) is Delegate obj4 && (bool)obj4.DynamicInvoke(def))
						{
							num += (int)slotCountGetter.Invoke(item, null);
						}
					}
					catch (Exception)
					{
						HDLog.ErrOnce("CE LoadoutGenericDef.lambda threw for def " + (((Def)(def?)).defName ?? "null") + " — generic loadout slot skipped for keep-count (non-fatal).", -830865407);
					}
				}
			}
		}
		return num;
	}
}
