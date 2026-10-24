using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls")]
[StaticConstructorOnStartup]
public static class Patch_PlaySettings
{
	public const string RememberPlanHighlightTag = "HaulersDream.RememberPlan";

	private static readonly Texture2D RememberPlanIcon = ContentFinder<Texture2D>.Get("HaulersDream/Interface/RememberPlan", false) ?? BaseContent.BadTex;

	public static Rect? RememberToggleScreenRect;

	private static void Postfix(WidgetRow row, bool worldView)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		if (worldView)
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.planRoutes)
		{
			return;
		}
		float finalX = row.FinalX;
		float finalY = row.FinalY;
		bool rememberPlan = settings.rememberPlan;
		row.ToggleableIcon(ref rememberPlan, RememberPlanIcon, TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.RememberToggleTip")), SoundDefOf.Mouseover_ButtonToggle, "HaulersDream.RememberPlan");
		float num = ((row.FinalX > finalX) ? finalX : (finalX - 24f));
		RememberToggleScreenRect = new Rect(num, finalY, 24f, 24f);
		if (rememberPlan != settings.rememberPlan)
		{
			settings.rememberPlan = rememberPlan;
			HaulersDreamMod instance = HaulersDreamMod.Instance;
			if (instance != null)
			{
				((Mod)instance).WriteSettings();
			}
		}
	}

	public static void DrawRememberButton(Rect row, bool alreadyRemembered, Action onRemember)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if (Mouse.IsOver(row))
		{
			UIHighlighter.HighlightTag("HaulersDream.RememberPlan");
		}
		if (alreadyRemembered)
		{
			DrawFaintDisabledButton(row, TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.RememberButtonSaved")));
			TooltipHandler.TipRegion(row, TipSignal.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.RememberButtonSavedDesc")));
			return;
		}
		if (Widgets.ButtonText(row, TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.RememberButton")), true, true, true, (TextAnchor?)null))
		{
			onRemember?.Invoke();
		}
		TooltipHandler.TipRegion(row, TipSignal.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.RememberButtonDesc")));
	}

	private static void DrawFaintDisabledButton(Rect rect, string label)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		Color color = GUI.color;
		GUI.color = new Color(1f, 1f, 1f, 0.3f);
		Widgets.DrawBox(rect, 1, (Texture2D)null);
		TextAnchor anchor = Text.Anchor;
		Text.Anchor = (TextAnchor)4;
		Widgets.Label(rect, label);
		Text.Anchor = anchor;
		GUI.color = color;
	}

	public static void DrawRememberArrow(Rect windowRect, Rect localButtonRect)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		if (RememberToggleScreenRect.HasValue)
		{
			Rect value = RememberToggleScreenRect.Value;
			Rect val = default(Rect);
			((Rect)(ref val))._002Ector(((Rect)(ref localButtonRect)).x + ((Rect)(ref windowRect)).x + 18f, ((Rect)(ref localButtonRect)).y + ((Rect)(ref windowRect)).y + 18f, ((Rect)(ref localButtonRect)).width, ((Rect)(ref localButtonRect)).height);
			Vector2 val2 = default(Vector2);
			((Vector2)(ref val2))._002Ector(((Rect)(ref val)).xMax, ((Rect)(ref val)).center.y);
			Vector2 val3 = ((Rect)(ref value)).center - val2;
			Vector2 normalized = ((Vector2)(ref val3)).normalized;
			if (!(normalized == Vector2.zero))
			{
				Vector2 val4 = ((Rect)(ref value)).center - normalized * (((Rect)(ref value)).width * 0.6f);
				Color val5 = default(Color);
				((Color)(ref val5))._002Ector(1f, 0.85f, 0.2f, 0.95f);
				Widgets.DrawLine(val2, val4, val5, 2.5f);
				float num = 0.4537856f;
				Vector2 val6 = -normalized;
				Vector2 val7 = default(Vector2);
				((Vector2)(ref val7))._002Ector(val6.x * Mathf.Cos(num) - val6.y * Mathf.Sin(num), val6.x * Mathf.Sin(num) + val6.y * Mathf.Cos(num));
				Vector2 val8 = default(Vector2);
				((Vector2)(ref val8))._002Ector(val6.x * Mathf.Cos(0f - num) - val6.y * Mathf.Sin(0f - num), val6.x * Mathf.Sin(0f - num) + val6.y * Mathf.Cos(0f - num));
				Widgets.DrawLine(val4, val4 + val7 * 13f, val5, 2.5f);
				Widgets.DrawLine(val4, val4 + val8 * 13f, val5, 2.5f);
			}
		}
	}
}
