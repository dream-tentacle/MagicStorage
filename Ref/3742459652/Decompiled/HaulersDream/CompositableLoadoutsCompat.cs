using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class CompositableLoadoutsCompat
{
	private static bool initialized;

	private static MethodInfo getOptionsMethod;

	private static bool keepApiInitialized;

	private static bool keepApiOk;

	private static Type loadoutComponentType;

	private static MethodInfo loadoutGetter;

	private static MethodInfo itemsGetter;

	private static MethodInfo itemDefGetter;

	private static MethodInfo itemQtyGetter;

	public static bool IsActive
	{
		get
		{
			if (!initialized)
			{
				Init();
			}
			return getOptionsMethod != null;
		}
	}

	public static void TryInsertModes(List<FloatMenuOption> options, Bill_Production bill)
	{
		if (!initialized)
		{
			Init();
		}
		if (!(getOptionsMethod == null) && options != null && bill != null)
		{
			getOptionsMethod.Invoke(null, new object[2] { options, bill });
		}
	}

	private static void Init()
	{
		initialized = true;
		Type type = AccessTools.TypeByName("Inventory.MakeConfigFloatMenu_Patch");
		if (!(type == null))
		{
			getOptionsMethod = AccessTools.Method(type, "GetOptions", new Type[2]
			{
				typeof(List<FloatMenuOption>),
				typeof(Bill_Production)
			}, (Type[])null);
			if (getOptionsMethod != null)
			{
				HDLog.Msg("Compositable Loadouts detected — its 'X per Tag' bill repeat mode is surfaced in the batch-aware repeat-mode menu.");
			}
			else
			{
				HDLog.Warn("Compositable Loadouts present but MakeConfigFloatMenu_Patch.GetOptions did not resolve (a version/rename?); its repeat mode will not appear in HD's repeat-mode menu.");
			}
		}
	}

	public static int KeepCount(Pawn pawn, ThingDef def)
	{
		if (pawn == null || def == null)
		{
			return 0;
		}
		if (!CompositableLoadoutKeepPolicy.ShieldsDef(def.IsApparel))
		{
			return 0;
		}
		if (!keepApiInitialized)
		{
			InitKeepApi();
		}
		if (!keepApiOk)
		{
			return 0;
		}
		ThingComp val = FindLoadoutComp(pawn);
		if (val == null)
		{
			return 0;
		}
		int wantedUnits;
		try
		{
			wantedUnits = LoadoutWantedUnits(val, def);
		}
		catch (Exception ex)
		{
			keepApiOk = false;
			HDLog.ErrOnce("Compositable Loadouts keep-count read threw for " + (((Def)(((Thing)pawn).def?)).defName ?? "a pawn") + "; HD is standing down its CL loadout shield (an unload↔re-fetch loop may recur). Please report it (issue #200).\n" + ex, 549522135);
			return 0;
		}
		return CompositableLoadoutKeepPolicy.ContributedKeep(wantedUnits, EquippedCountOfDef(pawn, def));
	}

	private static int LoadoutWantedUnits(ThingComp comp, ThingDef def)
	{
		object obj = loadoutGetter.Invoke(comp, null);
		if (obj == null)
		{
			return 0;
		}
		if (!(itemsGetter.Invoke(obj, null) is IEnumerable enumerable))
		{
			return 0;
		}
		int num = 0;
		foreach (object item in enumerable)
		{
			if (item != null)
			{
				object obj2 = itemDefGetter.Invoke(item, null);
				if (((obj2 is ThingDef) ? obj2 : null) == def)
				{
					num += CompositableLoadoutKeepPolicy.EntryUnits((int)itemQtyGetter.Invoke(item, null));
				}
			}
		}
		return num;
	}

	private static int EquippedCountOfDef(Pawn pawn, ThingDef def)
	{
		Pawn_EquipmentTracker equipment = pawn.equipment;
		List<ThingWithComps> list = ((equipment != null) ? equipment.AllEquipmentListForReading : null);
		if (list == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			ThingWithComps val = list[i];
			if (val != null && ((Thing)val).def == def)
			{
				num += ((Thing)val).stackCount;
			}
		}
		return num;
	}

	private static void InitKeepApi()
	{
		keepApiInitialized = true;
		loadoutComponentType = AccessTools.TypeByName("Inventory.LoadoutComponent");
		if (!(loadoutComponentType == null))
		{
			loadoutGetter = AccessTools.PropertyGetter(loadoutComponentType, "Loadout");
			Type type = AccessTools.TypeByName("Inventory.Loadout");
			itemsGetter = ((type != null) ? AccessTools.PropertyGetter(type, "Items") : null);
			Type type2 = AccessTools.TypeByName("Inventory.Item");
			itemDefGetter = ((type2 != null) ? AccessTools.PropertyGetter(type2, "Def") : null);
			itemQtyGetter = ((type2 != null) ? AccessTools.PropertyGetter(type2, "Quantity") : null);
			keepApiOk = loadoutGetter != null && itemsGetter != null && itemDefGetter != null && itemQtyGetter != null;
			if (keepApiOk)
			{
				HDLog.Msg("Compositable Loadouts loadout keep API resolved — a pawn's loadout items are kept during surplus unload (so HD won't fight CL's re-fetch). Apparel is deliberately not kept.");
			}
			else
			{
				HDLog.Warn("Compositable Loadouts present but its per-pawn loadout keep API (LoadoutComponent.Loadout / Loadout.Items / Item.Def|Quantity) did not fully resolve — a CL version/rename likely. HD cannot shield CL-loadout items from 'adopt all surplus', so an unload↔re-fetch loop may recur. Please report it (issue #200). HD continues.");
			}
		}
	}

	private static ThingComp FindLoadoutComp(Pawn pawn)
	{
		List<ThingComp> allComps = ((ThingWithComps)pawn).AllComps;
		if (allComps == null)
		{
			return null;
		}
		for (int i = 0; i < allComps.Count; i++)
		{
			if (allComps[i] != null && loadoutComponentType.IsInstanceOfType(allComps[i]))
			{
				return allComps[i];
			}
		}
		return null;
	}
}
