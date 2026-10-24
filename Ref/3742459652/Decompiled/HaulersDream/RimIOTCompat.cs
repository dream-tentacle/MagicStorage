using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class RimIOTCompat
{
	private static bool initialized;

	private static bool active;

	private static bool present;

	private static MethodInfo apiGetNetwork;

	private static Type mgrType;

	private static MethodInfo mgrGetNetwork;

	[ThreadStatic]
	private static object[] apiArgs;

	private static List<ThingDef> interfaceDefs;

	private const int InterfaceApronRadius = 4;

	[ThreadStatic]
	private static TickKeyedMemo<List<IntVec3>> interfaceCellsMemo;

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

	public static bool IsPresent
	{
		get
		{
			if (!initialized)
			{
				Init();
			}
			return present;
		}
	}

	static RimIOTCompat()
	{
		CacheRegistry.Register(ClearInterfaceCellsCache);
	}

	private static void ClearInterfaceCellsCache()
	{
		interfaceCellsMemo.Clear();
	}

	private static void Init()
	{
		initialized = true;
		active = false;
		present = false;
		if (ModLister.GetActiveModWithIdentifier("CN.RimIOT", true) == null)
		{
			return;
		}
		present = true;
		interfaceDefs = new List<ThingDef>();
		Type type = AccessTools.TypeByName("RimIOT.Building_Interface");
		List<ThingDef> allDefsListForReading = DefDatabase<ThingDef>.AllDefsListForReading;
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			ThingDef val = allDefsListForReading[i];
			if (!(val?.thingClass == null) && ((type != null) ? type.IsAssignableFrom(val.thingClass) : (((Def)val).defName == "RimIOT_Interface")))
			{
				interfaceDefs.Add(val);
			}
		}
		if (interfaceDefs.Count == 0)
		{
			ThingDef namedSilentFail = DefDatabase<ThingDef>.GetNamedSilentFail("RimIOT_Interface");
			if (namedSilentFail != null)
			{
				interfaceDefs.Add(namedSilentFail);
			}
		}
		if (interfaceDefs.Count == 0)
		{
			HDLog.Warn("RimIOT present but no interface-terminal def resolved (RimIOT.Building_Interface / RimIOT_Interface); HD's interface-apron overflow-drop gate is OFF (a RimIOT rename likely). Please report it. HD continues.");
		}
		Type type2 = AccessTools.TypeByName("RimIOT.RimIOTApi");
		if (type2 != null)
		{
			apiGetNetwork = AccessTools.Method(type2, "GetNetworkForContainer", new Type[2]
			{
				typeof(ISlotGroupParent),
				typeof(Map)
			}, (Type[])null);
		}
		mgrType = AccessTools.TypeByName("RimIOT.MapComponent_NetworkManager");
		if (mgrType != null)
		{
			mgrGetNetwork = AccessTools.Method(mgrType, "GetNetworkForContainer", new Type[1] { typeof(ISlotGroupParent) }, (Type[])null);
		}
		active = apiGetNetwork != null || mgrGetNetwork != null;
		if (active)
		{
			HDLog.Msg("RimIOT (Logistic Matrix) detected: HD leaves logistic-network storage to RimIOT (no bulk-sweep of network stacks, no deposit-steering inside a network).");
		}
		else
		{
			HDLog.Warn("RimIOT (Logistic Matrix) present but neither RimIOTApi.GetNetworkForContainer(ISlotGroupParent, Map) nor MapComponent_NetworkManager.GetNetworkForContainer(ISlotGroupParent) resolved; HD's RimIOT network-cell gating is OFF (a RimIOT rename likely). Please report it. HD continues.");
		}
	}

	public static bool IsNetworkManagedCell(Map map, IntVec3 cell)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (!IsActive || map == null || !GenGrid.InBounds(cell, map))
		{
			return false;
		}
		HaulDestinationManager haulDestinationManager = map.haulDestinationManager;
		return IsNetworkManagedParent(map, (haulDestinationManager == null) ? null : haulDestinationManager.SlotGroupAt(cell)?.parent);
	}

	public static bool IsNetworkManagedGroup(Map map, SlotGroup group)
	{
		if (!IsActive || map == null || group == null)
		{
			return false;
		}
		return IsNetworkManagedParent(map, group.parent);
	}

	private static bool IsNetworkManagedParent(Map map, ISlotGroupParent parent)
	{
		if (parent == null)
		{
			return false;
		}
		if (apiGetNetwork != null)
		{
			object[] array = apiArgs ?? (apiArgs = new object[2]);
			array[0] = parent;
			array[1] = map;
			return apiGetNetwork.Invoke(null, array) != null;
		}
		if (mgrGetNetwork != null)
		{
			MapComponent component = map.GetComponent(mgrType);
			if (component == null)
			{
				return false;
			}
			return mgrGetNetwork.Invoke(component, new object[1] { parent }) != null;
		}
		return false;
	}

	public static bool IsRimIOTHandledCell(Map map, IntVec3 cell)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		if (!IsNetworkManagedCell(map, cell))
		{
			return IsNearPoweredInterface(map, cell);
		}
		return true;
	}

	public static bool IsNearPoweredInterface(Map map, IntVec3 cell)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPresent || interfaceDefs == null || interfaceDefs.Count == 0 || map == null)
		{
			return false;
		}
		TickManager tickManager = Find.TickManager;
		int currentTick = ((tickManager != null) ? tickManager.TicksGame : 0);
		if (!interfaceCellsMemo.TryGet(currentTick, map.uniqueID, out var value))
		{
			value = BuildPoweredInterfaceCells(map);
			interfaceCellsMemo.Store(currentTick, map.uniqueID, value);
		}
		for (int i = 0; i < value.Count; i++)
		{
			IntVec3 val = value[i];
			if (Math.Abs(val.x - cell.x) <= 4 && Math.Abs(val.z - cell.z) <= 4)
			{
				return true;
			}
		}
		return false;
	}

	private static List<IntVec3> BuildPoweredInterfaceCells(Map map)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		List<IntVec3> list = new List<IntVec3>();
		for (int i = 0; i < interfaceDefs.Count; i++)
		{
			List<Thing> list2 = map.listerThings.ThingsOfDef(interfaceDefs[i]);
			for (int j = 0; j < list2.Count; j++)
			{
				Thing val = list2[j];
				if (val != null && val.Spawned)
				{
					CompPowerTrader val2 = ThingCompUtility.TryGetComp<CompPowerTrader>(val);
					if (val2 == null || val2.PowerOn)
					{
						list.Add(val.Position);
					}
				}
			}
		}
		return list;
	}
}
