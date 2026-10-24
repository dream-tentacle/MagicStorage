using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class GrabYourToolCompat
{
	private static bool initialized;

	private static bool active;

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
		active = AccessTools.TypeByName("CM_Grab_Your_Tool.GrabYourToolMod") != null;
		if (active)
		{
			HDLog.Msg("Grab Your Tool detected — carried tools are excluded from surplus unloading.");
		}
	}

	public static bool IsCarriedTool(Pawn pawn, Thing thing)
	{
		if (!IsActive || ((Thing)(pawn?)).def == null || thing?.def == null)
		{
			return false;
		}
		if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
		{
			return false;
		}
		ThingDef def = thing.def;
		if (!def.IsWeapon)
		{
			return false;
		}
		List<StatModifier> equippedStatOffsets = def.equippedStatOffsets;
		if (equippedStatOffsets != null)
		{
			return equippedStatOffsets.Count > 0;
		}
		return false;
	}
}
