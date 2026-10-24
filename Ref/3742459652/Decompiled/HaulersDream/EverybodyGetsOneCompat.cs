using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class EverybodyGetsOneCompat
{
	private static bool initialized;

	private static MethodInfo insertModeMethod;

	public static bool IsActive
	{
		get
		{
			if (!initialized)
			{
				Init();
			}
			return insertModeMethod != null;
		}
	}

	public static void TryInsertModes(List<FloatMenuOption> options, Bill_Production bill)
	{
		if (!initialized)
		{
			Init();
		}
		if (!(insertModeMethod == null) && options != null && bill != null)
		{
			insertModeMethod.Invoke(null, new object[2] { options, bill });
		}
	}

	private static void Init()
	{
		initialized = true;
		Type type = AccessTools.TypeByName("Everybody_Gets_One.MakeConfigFloatMenu_Patch");
		if (!(type == null))
		{
			insertModeMethod = AccessTools.Method(type, "InsertMode", new Type[2]
			{
				typeof(List<FloatMenuOption>),
				typeof(Bill_Production)
			}, (Type[])null);
			if (insertModeMethod != null)
			{
				Log.Message("[Hauler's Dream] Everybody Gets One detected — its bill repeat modes are surfaced in the batch-aware repeat-mode menu.");
			}
			else
			{
				HDLog.Warn("Everybody Gets One present but MakeConfigFloatMenu_Patch.InsertMode did not resolve (a version/rename?); its repeat modes will not appear in HD's repeat-mode menu.");
			}
		}
	}
}
