using UnityEngine;
using Verse;

namespace HaulersDream;

internal static class WidgetsCompat
{
	public static string TextAreaScrollable(Rect rect, string text, ref Vector2 scrollbarPosition)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		float num = ((Rect)(ref rect)).width - 16f;
		float num2 = Mathf.Max(((Rect)(ref rect)).height, Text.CalcHeight(text + "\n", num));
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, 0f, num, num2);
		Widgets.BeginScrollView(rect, ref scrollbarPosition, val, true);
		string result = Widgets.TextArea(val, text, false);
		Widgets.EndScrollView();
		return result;
	}
}
