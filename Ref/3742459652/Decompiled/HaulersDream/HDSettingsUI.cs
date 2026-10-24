using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HaulersDream;

public static class HDSettingsUI
{
	public static string HoverTitle;

	public static string HoverBody;

	public static string HoverStatus;

	public static Color HoverStatusColor;

	public static Action<Rect> HoverExtra;

	public static readonly Color OnColor = new Color(0.5f, 0.82f, 0.5f);

	public static readonly Color OffColor = new Color(0.82f, 0.55f, 0.55f);

	public static readonly Color ValueColor = new Color(0.62f, 0.78f, 0.95f);

	private const float RowGap = 6f;

	private const float YieldLabelFrac = 0.44f;

	public static void ResetHover()
	{
		HoverTitle = null;
		HoverBody = null;
		HoverStatus = null;
		HoverExtra = null;
	}

	public static void SetHelp(string title, string body)
	{
		HoverTitle = title;
		HoverBody = body;
	}

	public static void SetStatus(string status, Color color)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		HoverStatus = status;
		HoverStatusColor = color;
	}

	private static void BoolStatus(bool value)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		SetStatus(TaggedString.op_Implicit(Translator.Translate(value ? "HaulersDream.Common.On" : "HaulersDream.Common.Off")), value ? OnColor : OffColor);
	}

	private static void Hover(Rect r, string title, string body)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		if (Mouse.IsOver(r))
		{
			Widgets.DrawBoxSolid(r, new Color(1f, 1f, 1f, 0.04f));
			if (title != null || body != null)
			{
				SetHelp(title, body);
			}
		}
	}

	private static void DrawIndentRail(Rect r, float indent)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		if (!(indent <= 0f))
		{
			Widgets.DrawBoxSolid(new Rect(indent - 10f, ((Rect)(ref r)).y + 4f, 3f, Mathf.Max(4f, ((Rect)(ref r)).height - 8f)), new Color(0.45f, 0.6f, 0.7f, 0.5f));
		}
	}

	public static void Header(SettingsCtx c, string label)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		if (c.RenderOrdinals == null)
		{
			c.Gap(32f);
			Rect val = c.Row(26f);
			if (c.Collecting)
			{
				c.CurrentHeader = label;
				c.Gap(12f);
				return;
			}
			Widgets.DrawBoxSolid(val, new Color(1f, 1f, 1f, 0.09f));
			GameFont font = Text.Font;
			Color color = GUI.color;
			TextAnchor anchor = Text.Anchor;
			Text.Font = (GameFont)1;
			Text.Anchor = (TextAnchor)3;
			GUI.color = new Color(0.86f, 0.88f, 0.95f);
			Rect val2 = val;
			((Rect)(ref val2)).xMin = ((Rect)(ref val2)).xMin + 8f;
			Widgets.Label(val2, label);
			GUI.color = color;
			Text.Anchor = anchor;
			Text.Font = font;
			c.Gap(12f);
		}
	}

	public static void GapLine(SettingsCtx c)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		c.Gap(6f);
		Rect val = c.Row(1f);
		Color color = GUI.color;
		GUI.color = new Color(1f, 1f, 1f, 0.12f);
		Widgets.DrawLineHorizontal(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width);
		GUI.color = color;
		c.Gap(6f);
	}

	public static void Note(SettingsCtx c, string text, float indent = 0f, Color? color = null)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		if (c.RenderOrdinals == null)
		{
			GameFont font = Text.Font;
			Text.Font = (GameFont)0;
			float h = Mathf.Max(18f, Text.CalcHeight(text, c.Width - indent));
			Rect val = c.Row(h, indent);
			if (!c.Collecting)
			{
				Color color2 = GUI.color;
				GUI.color = (Color)(((_003F?)color) ?? new Color(0.72f, 0.72f, 0.76f));
				Widgets.Label(val, text);
				GUI.color = color2;
			}
			Text.Font = font;
		}
	}

	public static bool Checkbox(SettingsCtx c, string label, bool value, string help = null, bool enabled = true, float indent = 0f)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		if (c.RenderOrdinals != null)
		{
			int item = c.Ordinal++;
			if (!c.RenderOrdinals.Contains(item))
			{
				return value;
			}
		}
		float curY = c.CurY;
		GameFont font = Text.Font;
		Text.Font = (GameFont)1;
		float h = Mathf.Max(26f, Text.CalcHeight(label, c.Width - indent - 28f));
		Rect val = c.Row(h, indent);
		if (!c.Collecting)
		{
			DrawIndentRail(val, indent);
			Hover(val, label, help);
			bool flag = value;
			Widgets.CheckboxLabeled(val, label, ref flag, !enabled, (Texture2D)null, (Texture2D)null, false, false);
			if (Mouse.IsOver(val))
			{
				BoolStatus(enabled ? flag : value);
			}
			Text.Font = font;
			c.Gap(6f);
			if (!enabled)
			{
				return value;
			}
			return flag;
		}
		Text.Font = font;
		c.Gap(6f);
		c.Sink.Add(new OptionEntry
		{
			CatId = c.CurrentCatId,
			Header = c.CurrentHeader,
			Name = label,
			Desc = help,
			Ordinal = c.Ordinal++,
			StartY = curY,
			Height = c.CurY - curY
		});
		return value;
	}

	public static float Slider(SettingsCtx c, string label, float value, float min, float max, string readout, string help = null, bool enabled = true, float indent = 0f, Action<Rect> graph = null)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		if (c.RenderOrdinals != null)
		{
			int item = c.Ordinal++;
			if (!c.RenderOrdinals.Contains(item))
			{
				return value;
			}
		}
		float curY = c.CurY;
		GameFont font = Text.Font;
		Text.Font = (GameFont)1;
		Rect val = c.Row(24f, indent);
		if (c.Collecting)
		{
			c.Row(26f, indent);
			Text.Font = font;
			c.Gap(6f);
			c.Sink.Add(new OptionEntry
			{
				CatId = c.CurrentCatId,
				Header = c.CurrentHeader,
				Name = label,
				Desc = help,
				Ordinal = c.Ordinal++,
				StartY = curY,
				Height = c.CurY - curY
			});
			return value;
		}
		DrawIndentRail(val, indent);
		Hover(val, label, help);
		Color color = GUI.color;
		if (!enabled)
		{
			GUI.color = new Color(color.r, color.g, color.b, 0.5f);
		}
		TextAnchor anchor = Text.Anchor;
		bool wordWrap = Text.WordWrap;
		Text.WordWrap = false;
		float num = Mathf.Min(Text.CalcSize(readout).x + 4f, ((Rect)(ref val)).width - 70f);
		Rect val2 = val;
		((Rect)(ref val2)).width = Mathf.Max(40f, ((Rect)(ref val)).width - num - 8f);
		Widgets.Label(val2, label);
		Text.Anchor = (TextAnchor)5;
		Rect val3 = new Rect(((Rect)(ref val)).xMax - num, ((Rect)(ref val)).y, num, ((Rect)(ref val)).height);
		GUI.color = (enabled ? new Color(0.8f, 0.85f, 0.95f) : new Color(0.8f, 0.85f, 0.95f, 0.5f));
		Widgets.Label(val3, readout);
		Text.Anchor = anchor;
		Text.WordWrap = wordWrap;
		GUI.color = color;
		Rect val4 = c.Row(26f, indent);
		bool enabled2 = GUI.enabled;
		GUI.enabled = enabled;
		float result = Widgets.HorizontalSlider(val4, value, min, max, true, (string)null, (string)null, (string)null, -1f);
		GUI.enabled = enabled2;
		if (Mouse.IsOver(val) || Mouse.IsOver(val4))
		{
			SetStatus(readout, ValueColor);
			if (graph != null)
			{
				HoverExtra = graph;
			}
		}
		Text.Font = font;
		c.Gap(6f);
		if (!enabled)
		{
			return value;
		}
		return result;
	}

	public static int Segmented(SettingsCtx c, string label, int selected, string[] options, string[] optionHelp = null, string help = null, bool enabled = true, float indent = 0f)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0300: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_023e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		if (c.RenderOrdinals != null)
		{
			int item = c.Ordinal++;
			if (!c.RenderOrdinals.Contains(item))
			{
				return selected;
			}
		}
		float curY = c.CurY;
		GameFont font = Text.Font;
		Text.Font = (GameFont)1;
		Rect val = c.Row(24f, indent);
		if (c.Collecting)
		{
			c.Row(30f, indent);
			Text.Font = font;
			c.Gap(6f);
			c.Sink.Add(new OptionEntry
			{
				CatId = c.CurrentCatId,
				Header = c.CurrentHeader,
				Name = label,
				Desc = help,
				Ordinal = c.Ordinal++,
				StartY = curY,
				Height = c.CurY - curY
			});
			return selected;
		}
		DrawIndentRail(val, indent);
		Hover(val, label, help);
		Color color = GUI.color;
		if (!enabled)
		{
			GUI.color = new Color(color.r, color.g, color.b, 0.5f);
		}
		Widgets.Label(val, label);
		GUI.color = color;
		Rect val2 = c.Row(30f, indent);
		int num = Mathf.Max(1, options.Length);
		float num2 = (((Rect)(ref val2)).width - 4f * (float)(num - 1)) / (float)num;
		int num3 = selected;
		TextAnchor anchor = Text.Anchor;
		bool wordWrap = Text.WordWrap;
		Text.Anchor = (TextAnchor)4;
		Text.WordWrap = true;
		Text.Font = (GameFont)0;
		Rect val3 = default(Rect);
		for (int i = 0; i < num; i++)
		{
			((Rect)(ref val3))._002Ector(((Rect)(ref val2)).x + (float)i * (num2 + 4f), ((Rect)(ref val2)).y, num2, ((Rect)(ref val2)).height);
			bool flag = i == selected;
			Widgets.DrawBoxSolid(val3, flag ? new Color(0.28f, 0.45f, 0.55f, enabled ? 0.7f : 0.35f) : new Color(1f, 1f, 1f, enabled ? 0.06f : 0.03f));
			Color color2 = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, flag ? 0.45f : 0.18f);
			Widgets.DrawBox(val3, 1, (Texture2D)null);
			GUI.color = (Color)(enabled ? Color.white : new Color(1f, 1f, 1f, 0.5f));
			Widgets.Label(val3, options[i]);
			GUI.color = color2;
			if (optionHelp != null && i < optionHelp.Length)
			{
				Hover(val3, options[i], optionHelp[i]);
			}
			if (enabled && Widgets.ButtonInvisible(val3, true))
			{
				num3 = i;
			}
		}
		int num4 = (enabled ? num3 : selected);
		if (Mouse.IsOver(val) || Mouse.IsOver(val2))
		{
			SetStatus(options[Mathf.Clamp(num4, 0, options.Length - 1)], ValueColor);
		}
		Text.Anchor = anchor;
		Text.WordWrap = wordWrap;
		Text.Font = font;
		c.Gap(6f);
		if (!enabled)
		{
			return selected;
		}
		return num3;
	}

	public static void YieldMatrix(SettingsCtx c, string[] colLabels, string[] colHelps, IList<YieldMatrixRow> rows)
	{
		if (c.RenderOrdinals != null)
		{
			int ordinal = c.Ordinal;
			c.Ordinal += rows.Count;
			bool flag = false;
			for (int i = 0; i < rows.Count; i++)
			{
				if (c.RenderOrdinals.Contains(ordinal + i))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return;
			}
			MatrixHeader(c, colLabels, colHelps);
			for (int j = 0; j < rows.Count; j++)
			{
				if (c.RenderOrdinals.Contains(ordinal + j))
				{
					MatrixRow(c, colLabels, colHelps, rows[j]);
				}
			}
			return;
		}
		MatrixHeader(c, colLabels, colHelps);
		for (int k = 0; k < rows.Count; k++)
		{
			float curY = c.CurY;
			MatrixRow(c, colLabels, colHelps, rows[k]);
			if (c.Collecting)
			{
				c.Sink.Add(new OptionEntry
				{
					CatId = c.CurrentCatId,
					Header = c.CurrentHeader,
					Name = rows[k].Label,
					Desc = rows[k].Help,
					Ordinal = c.Ordinal++,
					StartY = curY,
					Height = c.CurY - curY
				});
			}
		}
	}

	private static void MatrixHeader(SettingsCtx c, string[] colLabels, string[] colHelps)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		Rect val = c.Row(34f);
		if (c.Collecting)
		{
			return;
		}
		float num = c.Width * 0.44f;
		int num2 = Mathf.Max(1, colLabels.Length);
		float num3 = (c.Width - num) / (float)num2;
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		bool wordWrap = Text.WordWrap;
		Color color = GUI.color;
		Text.Font = (GameFont)0;
		Text.Anchor = (TextAnchor)7;
		Text.WordWrap = true;
		GUI.color = new Color(0.8f, 0.84f, 0.92f);
		Rect val2 = default(Rect);
		for (int i = 0; i < num2; i++)
		{
			((Rect)(ref val2))._002Ector(num + (float)i * num3, ((Rect)(ref val)).y, num3, ((Rect)(ref val)).height);
			Widgets.Label(val2, colLabels[i]);
			if (colHelps != null && i < colHelps.Length)
			{
				Hover(val2, colLabels[i], colHelps[i]);
			}
		}
		GUI.color = new Color(1f, 1f, 1f, 0.12f);
		Widgets.DrawLineHorizontal(((Rect)(ref val)).x, ((Rect)(ref val)).yMax - 1f, ((Rect)(ref val)).width);
		GUI.color = color;
		Text.WordWrap = wordWrap;
		Text.Anchor = anchor;
		Text.Font = font;
	}

	private static void MatrixRow(SettingsCtx c, string[] colLabels, string[] colHelps, YieldMatrixRow row)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		GameFont font = Text.Font;
		Text.Font = (GameFont)1;
		float num = c.Width * 0.44f;
		float h = Mathf.Max(32f, Text.CalcHeight(row.Label, num - 10f));
		Rect val = c.Row(h);
		if (c.Collecting)
		{
			Text.Font = font;
			return;
		}
		int num2 = Mathf.Max(1, colLabels.Length);
		float num3 = (c.Width - num) / (float)num2;
		Color color = GUI.color;
		GUI.color = new Color(1f, 1f, 1f, 0.07f);
		Widgets.DrawLineHorizontal(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width);
		GUI.color = color;
		if (Mouse.IsOver(val))
		{
			SetStatus(colLabels[Mathf.Clamp(row.Value, 0, colLabels.Length - 1)], ValueColor);
		}
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(((Rect)(ref val)).x, ((Rect)(ref val)).y, num - 6f, ((Rect)(ref val)).height);
		if (Mouse.IsOver(val2))
		{
			Widgets.DrawBoxSolid(val2, new Color(1f, 1f, 1f, 0.04f));
			SetHelp(row.Label, row.Help);
		}
		TextAnchor anchor = Text.Anchor;
		Text.Anchor = (TextAnchor)3;
		GUI.color = new Color(0.9f, 0.9f, 0.94f);
		Widgets.Label(val2, row.Label);
		GUI.color = color;
		Text.Anchor = anchor;
		Rect val3 = default(Rect);
		for (int i = 0; i < num2; i++)
		{
			((Rect)(ref val3))._002Ector(num + (float)i * num3, ((Rect)(ref val)).y, num3, ((Rect)(ref val)).height);
			if (!row.AllowDirect && i == num2 - 1)
			{
				Color color2 = GUI.color;
				TextAnchor anchor2 = Text.Anchor;
				GUI.color = new Color(1f, 1f, 1f, 0.2f);
				Text.Anchor = (TextAnchor)4;
				Widgets.Label(val3, "—");
				Text.Anchor = anchor2;
				GUI.color = color2;
				continue;
			}
			if (Mouse.IsOver(val3))
			{
				Widgets.DrawBoxSolid(val3, new Color(1f, 1f, 1f, 0.04f));
				if (colHelps != null && i < colHelps.Length)
				{
					SetHelp(colLabels[i], colHelps[i]);
				}
			}
			float num4 = ((Rect)(ref val3)).x + (((Rect)(ref val3)).width - 24f) / 2f;
			float num5 = ((Rect)(ref val3)).y + (((Rect)(ref val3)).height - 24f) / 2f;
			if (Widgets.RadioButton(num4, num5, row.Value == i, false))
			{
				row.Value = i;
			}
		}
		Text.Font = font;
	}

	public static void Button(SettingsCtx c, string label, Action onClick, string help = null, bool enabled = true, float indent = 0f)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		if (c.RenderOrdinals != null)
		{
			int item = c.Ordinal++;
			if (!c.RenderOrdinals.Contains(item))
			{
				return;
			}
		}
		float curY = c.CurY;
		Rect r = c.Row(32f, indent);
		if (c.Collecting)
		{
			c.Gap(6f);
			c.Sink.Add(new OptionEntry
			{
				CatId = c.CurrentCatId,
				Header = c.CurrentHeader,
				Name = label,
				Desc = help,
				Ordinal = c.Ordinal++,
				StartY = curY,
				Height = c.CurY - curY
			});
		}
		else
		{
			DrawIndentRail(r, indent);
			Hover(r, label, help);
			if (Widgets.ButtonText(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y + 1f, Mathf.Min(340f, ((Rect)(ref r)).width), 28f), label, true, true, enabled, (TextAnchor?)null) && enabled)
			{
				onClick();
			}
			c.Gap(6f);
		}
	}

	public static bool FeatureCard(SettingsCtx c, Texture2D icon, string name, string blurb, bool value, string help = null, bool enabled = true)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_026f: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		if (c.RenderOrdinals != null)
		{
			int item = c.Ordinal++;
			if (!c.RenderOrdinals.Contains(item))
			{
				return value;
			}
		}
		float curY = c.CurY;
		Rect val = c.Row(54f);
		c.Gap(4f);
		if (c.Collecting)
		{
			c.Sink.Add(new OptionEntry
			{
				CatId = c.CurrentCatId,
				Header = c.CurrentHeader,
				Name = name,
				Desc = (help ?? blurb),
				Ordinal = c.Ordinal++,
				StartY = curY,
				Height = c.CurY - curY
			});
			return value;
		}
		Widgets.DrawHighlightIfMouseover(val);
		if (Mouse.IsOver(val))
		{
			SetHelp(name, help ?? blurb);
			BoolStatus(value);
		}
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(((Rect)(ref val)).x + 10f, ((Rect)(ref val)).y + 15f, 24f, 24f);
		if ((Object)(object)icon != (Object)null)
		{
			Color color = GUI.color;
			GUI.color = (Color)((enabled && value) ? Color.white : new Color(1f, 1f, 1f, 0.55f));
			GUI.DrawTexture(val2, (Texture)(object)icon, (ScaleMode)2);
			GUI.color = color;
		}
		Rect val3 = default(Rect);
		((Rect)(ref val3))._002Ector(((Rect)(ref val)).xMax - 10f - 24f, ((Rect)(ref val)).y + 15f, 24f, 24f);
		Widgets.CheckboxDraw(((Rect)(ref val3)).x, ((Rect)(ref val3)).y, value, !enabled, 24f, (Texture2D)null, (Texture2D)null);
		float num = ((Rect)(ref val2)).xMax + 10f;
		float num2 = ((Rect)(ref val3)).x - num - 8f;
		GameFont font = Text.Font;
		Color color2 = GUI.color;
		Text.Font = (GameFont)1;
		GUI.color = (Color)(enabled ? Color.white : new Color(1f, 1f, 1f, 0.55f));
		Widgets.Label(new Rect(num, ((Rect)(ref val)).y + 7f, num2, 22f), name);
		Text.Font = (GameFont)0;
		GUI.color = new Color(0.74f, 0.74f, 0.78f, enabled ? 1f : 0.6f);
		Widgets.Label(new Rect(num, ((Rect)(ref val)).y + 28f, num2, 20f), blurb);
		GUI.color = color2;
		Text.Font = font;
		bool result = value;
		if (enabled && Widgets.ButtonInvisible(val, true))
		{
			result = !value;
			SoundStarter.PlayOneShotOnCamera(SoundDefOf.Checkbox_TurnedOn, (Map)null);
		}
		return result;
	}
}
