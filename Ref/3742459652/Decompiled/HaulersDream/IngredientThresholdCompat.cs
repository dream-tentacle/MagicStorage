using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HaulersDream;

public static class IngredientThresholdCompat
{
	private static bool initialized;

	private static FieldInfo thresholdModeField;

	public static bool IsActive
	{
		get
		{
			if (!initialized)
			{
				Init();
			}
			return thresholdModeField != null;
		}
	}

	public static void TryInsertModes(List<FloatMenuOption> options, Bill_Production bill)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Expected O, but got Unknown
		if (!initialized)
		{
			Init();
		}
		if (thresholdModeField == null || options == null || bill == null)
		{
			return;
		}
		object value = thresholdModeField.GetValue(null);
		BillRepeatModeDef mode = (BillRepeatModeDef)((value is BillRepeatModeDef) ? value : null);
		if (mode != null)
		{
			options.Add(new FloatMenuOption(TaggedString.op_Implicit(((Def)mode).LabelCap), (Action)delegate
			{
				bill.repeatMode = mode;
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
		}
	}

	private static void Init()
	{
		initialized = true;
		Type type = AccessTools.TypeByName("IngredientThreshold.ThresholdRepeatModeDef");
		if (!(type == null))
		{
			thresholdModeField = AccessTools.Field(type, "IngredientThreshold");
			if (thresholdModeField != null)
			{
				Log.Message("[Hauler's Dream] Ingredient Threshold detected, its repeat mode is surfaced in the batch-aware repeat-mode menu.");
			}
			else
			{
				HDLog.Warn("Ingredient Threshold present but ThresholdRepeatModeDef.IngredientThreshold did not resolve (a version/rename?); its repeat mode will not appear in HD's repeat-mode menu.");
			}
		}
	}
}
