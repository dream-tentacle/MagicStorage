using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HaulersDream.Core;
using Verse;

namespace HaulersDream;

public static class SimpleSidearmsCompat
{
	private static bool initialized;

	private static bool active;

	private static bool memoryApiOk;

	private static bool warned;

	private static Type compType;

	private static FieldInfo rememberedField;

	private static FieldInfo pairThingField;

	private static FieldInfo pairStuffField;

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

	public static bool MemoryApiOk
	{
		get
		{
			if (!initialized)
			{
				Init();
			}
			return memoryApiOk;
		}
	}

	private static void Init()
	{
		initialized = true;
		compType = AccessTools.TypeByName("SimpleSidearms.rimworld.CompSidearmMemory");
		if (!(compType == null))
		{
			active = true;
			rememberedField = AccessTools.Field(compType, "rememberedWeapons");
			Type type = AccessTools.TypeByName("SimpleSidearms.rimworld.ThingDefStuffDefPair");
			if (type != null)
			{
				pairThingField = AccessTools.Field(type, "thing");
				pairStuffField = AccessTools.Field(type, "stuff");
			}
			memoryApiOk = rememberedField != null && pairThingField != null && pairStuffField != null;
			HDLog.Msg("Simple Sidearms detected — carried sidearms are excluded from surplus unloading" + (memoryApiOk ? "." : " (memory API unresolved — keeping all carried weapons as a safe fallback)."));
		}
	}

	private static ThingComp MemoryOf(Pawn pawn)
	{
		List<ThingComp> list = ((pawn != null) ? ((ThingWithComps)pawn).AllComps : null);
		if (list == null || compType == null)
		{
			return null;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (compType.IsInstanceOfType(list[i]))
			{
				return list[i];
			}
		}
		return null;
	}

	public static bool IsKeptWeapon(Pawn pawn, Thing weapon)
	{
		if (!IsActive || pawn == null || weapon?.def == null)
		{
			return false;
		}
		if (!weapon.def.IsRangedWeapon && !weapon.def.IsMeleeWeapon)
		{
			return false;
		}
		if (IsRememberedSidearm(pawn, weapon))
		{
			return true;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp != null && comp.PeekHashSet().Contains(weapon))
		{
			return false;
		}
		if (memoryApiOk)
		{
			return false;
		}
		if (!warned)
		{
			warned = true;
			HDLog.Warn("Simple Sidearms present but CompSidearmMemory.rememberedWeapons did not resolve; keeping all carried weapons out of surplus unloading as a safe fallback.");
		}
		return true;
	}

	public static bool IsRememberedSidearm(Pawn pawn, Thing weapon)
	{
		if (!IsActive || !memoryApiOk || pawn == null || weapon?.def == null)
		{
			return false;
		}
		if (!weapon.def.IsRangedWeapon && !weapon.def.IsMeleeWeapon)
		{
			return false;
		}
		ThingComp val = MemoryOf(pawn);
		if (val != null && rememberedField.GetValue(val) is IEnumerable enumerable)
		{
			foreach (object item in enumerable)
			{
				object value = pairThingField.GetValue(item);
				if (((value is ThingDef) ? value : null) == weapon.def)
				{
					object value2 = pairStuffField.GetValue(item);
					if (((value2 is ThingDef) ? value2 : null) == weapon.Stuff)
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	public static int RememberedCount(Pawn pawn, ThingDef def, ThingDef stuff)
	{
		if (!IsActive || !memoryApiOk || pawn == null || def == null)
		{
			return 0;
		}
		if (!def.IsRangedWeapon && !def.IsMeleeWeapon)
		{
			return 0;
		}
		ThingComp val = MemoryOf(pawn);
		int num = 0;
		if (val != null && rememberedField.GetValue(val) is IEnumerable enumerable)
		{
			foreach (object item in enumerable)
			{
				object value = pairThingField.GetValue(item);
				if (((value is ThingDef) ? value : null) == def)
				{
					object value2 = pairStuffField.GetValue(item);
					if (((value2 is ThingDef) ? value2 : null) == stuff)
					{
						num++;
					}
				}
			}
		}
		return num;
	}

	public static int InventoryKeepCount(Pawn pawn, ThingDef def, ThingDef stuff)
	{
		int rememberedCount = RememberedCount(pawn, def, stuff);
		object obj;
		if (pawn == null)
		{
			obj = null;
		}
		else
		{
			Pawn_EquipmentTracker equipment = pawn.equipment;
			obj = ((equipment != null) ? equipment.Primary : null);
		}
		ThingWithComps val = (ThingWithComps)obj;
		bool primaryMatchesPair = val != null && ((Thing)val).def == def && ((Thing)val).Stuff == stuff;
		return SidearmKeepMath.KeepForPair(rememberedCount, primaryMatchesPair);
	}
}
