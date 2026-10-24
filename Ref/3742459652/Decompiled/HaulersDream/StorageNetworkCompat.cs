using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class StorageNetworkCompat
{
	private static bool initialized;

	private static bool active;

	private static MethodInfo getTerminalsMethod;

	private static MethodInfo canPawnUseTerminalMethod;

	private static MethodInfo getStacksByDefMethod;

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
		Type type = AccessTools.TypeByName("StorageNetwork.TerminalRegistry");
		Type type2 = AccessTools.TypeByName("StorageNetwork.StorageNetworkMod");
		Type type3 = AccessTools.TypeByName("StorageNetwork.Building_StorageTerminal");
		if (!(type == null) && !(type2 == null) && !(type3 == null))
		{
			getTerminalsMethod = AccessTools.Method(type, "GetTerminals", new Type[1] { typeof(Map) }, (Type[])null);
			canPawnUseTerminalMethod = AccessTools.Method(type2, "CanPawnUseTerminal", new Type[2]
			{
				type3,
				typeof(Pawn)
			}, (Type[])null);
			getStacksByDefMethod = AccessTools.Method(type3, "GetStacksByDef", new Type[1] { typeof(ThingDef) }, (Type[])null);
			active = getTerminalsMethod != null && canPawnUseTerminalMethod != null && getStacksByDefMethod != null;
			if (active)
			{
				Log.Message("[Hauler's Dream] Storage Network detected — opt-in bulk-load from its network servers is available (enable it under Advanced loading in the settings; default off).");
			}
			else
			{
				HDLog.Warn("Storage Network present but its terminal API did not resolve (a version/rename?); bulk-loading from the network is unavailable. Vanilla one-stack loading still works.");
			}
		}
	}

	public static List<Thing> UsableTerminals(Pawn pawn, Map map)
	{
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		List<Thing> list = new List<Thing>();
		if (!IsActive || pawn == null || map == null)
		{
			return list;
		}
		if (!(getTerminalsMethod.Invoke(null, new object[1] { map }) is IList list2))
		{
			return list;
		}
		for (int i = 0; i < list2.Count; i++)
		{
			object obj = list2[i];
			Thing val = (Thing)((obj is Thing) ? obj : null);
			if (val != null && val.Spawned && !ForbidUtility.IsForbidden(val, pawn) && (bool)canPawnUseTerminalMethod.Invoke(null, new object[2] { val, pawn }) && ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit(val), (PathEndMode)2, DangerUtility.NormalMaxDanger(pawn), false, false, (TraverseMode)0))
			{
				list.Add(val);
			}
		}
		return list;
	}

	public static List<Thing> NetworkStacksOfDef(Thing terminal, ThingDef def)
	{
		List<Thing> list = new List<Thing>();
		if (!IsActive || terminal == null || def == null)
		{
			return list;
		}
		if (getStacksByDefMethod.Invoke(terminal, new object[1] { def }) is IList list2)
		{
			for (int i = 0; i < list2.Count; i++)
			{
				object obj = list2[i];
				Thing val = (Thing)((obj is Thing) ? obj : null);
				if (val != null)
				{
					list.Add(val);
				}
			}
		}
		return list;
	}
}
