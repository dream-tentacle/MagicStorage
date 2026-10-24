using System;
using System.Collections.Generic;
using HarmonyLib;
using HaulersDream.Core;
using Verse;

namespace HaulersDream;

public static class SurvivalToolsCompat
{
	private static bool initialized;

	private static Type toolType;

	private static Type propsType;

	public static bool IsActive
	{
		get
		{
			if (!initialized)
			{
				Init();
			}
			if (toolType != null)
			{
				return propsType != null;
			}
			return false;
		}
	}

	private static void Init()
	{
		initialized = true;
		toolType = AccessTools.TypeByName("SurvivalTools.SurvivalTool");
		if (!(toolType == null))
		{
			propsType = AccessTools.TypeByName("SurvivalTools.SurvivalToolProperties");
			if (propsType != null)
			{
				HDLog.Msg("Survival Tools detected — a pawn's carried survival tools are excluded from surplus unloading.");
			}
			else
			{
				HDLog.Warn("Survival Tools present but SurvivalToolProperties did not resolve (a version/rename?). HD cannot recognise carried survival tools, so with 'unload all surplus' ON an unload↔re-fetch loop may occur; turning that option off avoids it. HD continues.");
			}
		}
	}

	public static bool IsCarriedTool(Pawn pawn, Thing thing)
	{
		bool isActive = IsActive;
		if (!isActive || ((Thing)(pawn?)).def == null || thing?.def == null)
		{
			return false;
		}
		return SurvivalToolKeepPolicy.KeepsCarriedTool(isActive, pawn.RaceProps != null && pawn.RaceProps.Humanlike, IsSurvivalToolThing(thing));
	}

	private static bool IsSurvivalToolThing(Thing thing)
	{
		if (!toolType.IsInstanceOfType(thing))
		{
			return false;
		}
		List<DefModExtension> modExtensions = ((Def)thing.def).modExtensions;
		if (modExtensions == null)
		{
			return false;
		}
		for (int i = 0; i < modExtensions.Count; i++)
		{
			if (modExtensions[i] != null && propsType.IsInstanceOfType(modExtensions[i]))
			{
				return true;
			}
		}
		return false;
	}
}
