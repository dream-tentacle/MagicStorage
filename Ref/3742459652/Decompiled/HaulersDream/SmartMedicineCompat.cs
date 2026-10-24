using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace HaulersDream;

public static class SmartMedicineCompat
{
	private static bool initialized;

	private static bool active;

	private static MethodInfo stockingUpOn;

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
		Type type = AccessTools.TypeByName("SmartMedicine.StockUpUtility");
		if (!(type == null))
		{
			stockingUpOn = AccessTools.Method(type, "StockingUpOn", new Type[2]
			{
				typeof(Pawn),
				typeof(ThingDef)
			}, (Type[])null);
			active = stockingUpOn != null;
			if (active)
			{
				HDLog.Msg("Smart Medicine detected — stocked-up medicine is excluded from surplus unloading.");
			}
			else
			{
				HDLog.Warn("Smart Medicine present but StockUpUtility.StockingUpOn(Pawn, ThingDef) did not resolve; stocked medicine is NOT specially protected (turn off 'unload foreign surplus' if it loops).");
			}
		}
	}

	public static bool IsStockedUp(Pawn pawn, ThingDef def)
	{
		if (!IsActive || pawn == null || def == null)
		{
			return false;
		}
		return (bool)stockingUpOn.Invoke(null, new object[2] { pawn, def });
	}
}
