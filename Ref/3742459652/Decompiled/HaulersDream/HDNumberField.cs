using HaulersDream.Core;
using UnityEngine;
using Verse;

namespace HaulersDream;

public static class HDNumberField
{
	public const float Width = 58f;

	public const float Gap = 6f;

	public const float SliderRowH = 22f;

	public const float SliderRowGap = 2f;

	public static void SplitRow(Rect row, out Rect sliderRect, out Rect boxRect)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		boxRect = new Rect(((Rect)(ref row)).xMax - 58f, ((Rect)(ref row)).y, 58f, ((Rect)(ref row)).height);
		sliderRect = new Rect(((Rect)(ref row)).x, ((Rect)(ref row)).y, Mathf.Max(40f, ((Rect)(ref row)).width - 58f - 6f), ((Rect)(ref row)).height);
	}

	public static void Int(Rect boxRect, string controlName, ref int value, ref string buf, int min, int max, string tooltip = null)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		if (buf == null)
		{
			buf = NumberEntryPolicy.RenderInt(value);
		}
		if (!GenText.NullOrEmpty(tooltip))
		{
			TooltipHandler.TipRegion(boxRect, TipSignal.op_Implicit(tooltip));
		}
		GUI.SetNextControlName(controlName);
		string text = Widgets.TextField(boxRect, buf);
		if (GUI.GetNameOfFocusedControl() == controlName)
		{
			if (NumberEntryPolicy.IsAcceptable(text, allowDecimal: false, 6))
			{
				buf = text;
			}
			if (NumberEntryPolicy.TryParseInt(buf, out var value2))
			{
				value = NumberEntryPolicy.ClampInt(value2, min, max);
			}
		}
		else
		{
			value = NumberEntryPolicy.ClampInt(value, min, max);
			buf = NumberEntryPolicy.RenderInt(value);
		}
	}

	public static void Float(Rect boxRect, string controlName, ref float value, ref string buf, float min, float max, float step, string format, string tooltip = null)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (buf == null)
		{
			buf = NumberEntryPolicy.RenderFloat(value, format);
		}
		if (!GenText.NullOrEmpty(tooltip))
		{
			TooltipHandler.TipRegion(boxRect, TipSignal.op_Implicit(tooltip));
		}
		GUI.SetNextControlName(controlName);
		string text = Widgets.TextField(boxRect, buf);
		if (GUI.GetNameOfFocusedControl() == controlName)
		{
			if (NumberEntryPolicy.IsAcceptable(text, allowDecimal: true, 6))
			{
				buf = text;
			}
			if (NumberEntryPolicy.TryParseFloat(buf, out var value2))
			{
				value = NumberEntryPolicy.ClampFloat(value2, min, max);
			}
		}
		else
		{
			value = NumberEntryPolicy.SnapFloat(value, step, min, max);
			buf = NumberEntryPolicy.RenderFloat(value, format);
		}
	}
}
