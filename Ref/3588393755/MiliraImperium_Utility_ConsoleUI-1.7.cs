using System;
using AriandelLibrary;
using UnityEngine;
using Verse;

namespace MiliraImperium;

public static class MiliraImperium_Utility_ConsoleUI
{
	private const float DefaultMaximumTooltipWidth = 400f;

	private const float MinimumTooltipWidth = 120f;

	private const float TooltipHorizontalPadding = 12f;

	private const float TooltipVerticalPadding = 10f;

	private const float TooltipMouseOffsetX = 16f;

	private const float TooltipMouseOffsetY = 18f;

	private const float TooltipHoverDelay = 0.25f;

	private const float RectComparisonTolerance = 0.01f;

	private const int DialogueShadowTextureHeight = 64;

	private const float DialogueShadowMaximumAlpha = 0.72f;

	public static readonly Color LineColor = Color32.op_Implicit(new Color32((byte)78, (byte)117, (byte)98, byte.MaxValue));

	public static readonly Color TintedTextColor = Color32.op_Implicit(new Color32((byte)135, byte.MaxValue, (byte)207, byte.MaxValue));

	public static readonly Color DetailTextColor = Color.white;

	public static readonly Color PermitCooldownTextColor = Color.white;

	public static readonly Color PermitUnavailableTextColor = Color32.op_Implicit(new Color32((byte)232, (byte)64, (byte)51, byte.MaxValue));

	private static readonly Color DisabledLineColor = new Color(26f / 85f, 39f / 85f, 0.38431373f, 0.38f);

	private static readonly Color TooltipBackgroundColor = new Color(10f / 51f, 0.16862746f, 0.1254902f, 0.98f);

	private static readonly Color PermitStateOverlayColor = new Color(0f, 0f, 0f, 0.55f);

	private static readonly AriandelLibrary_WindowThemeStyle ConsoleThemeStyle = CreateThemeStyle(fadingSeparators: false);

	private static readonly AriandelLibrary_WindowThemeStyle FadingSeparatorThemeStyle = CreateThemeStyle(fadingSeparators: true);

	private static GUIStyle permitCooldownStyle;

	private static Texture2D dialogueShadowTexture;

	public static AriandelLibrary_WindowThemeStyle ThemeStyle => ConsoleThemeStyle;

	private static GUIStyle PermitCooldownStyle
	{
		get
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Expected O, but got Unknown
			if (permitCooldownStyle == null)
			{
				permitCooldownStyle = new GUIStyle(Text.CurFontStyle)
				{
					fontSize = 20,
					alignment = (TextAnchor)4,
					wordWrap = false,
					clipping = (TextClipping)1
				};
			}
			return permitCooldownStyle;
		}
	}

	public static void DrawPermitCooldownLabel(Rect rect, string text, AriandelLibrary_Utility_Window_TextScrollState state, int textScrollSlot)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		AriandelLibrary_Utility_Window_Text.DrawAutoScrollingLabel(rect, text ?? string.Empty, PermitCooldownTextColor, AriandelLibrary_Utility_Window_Theme.WindowAlpha, (TextAnchor)4, state, textScrollSlot, PermitCooldownStyle, 30f, 3f);
	}

	public static void DrawPermitStateOverlay(Rect rect)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		AriandelLibrary_Utility_Window_Theme.DrawSolid(rect, PermitStateOverlayColor);
	}

	public static void DrawDialogueShadow(Rect rect, float dialogueAlpha)
	{
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		if (((Rect)(ref rect)).width <= 0f || ((Rect)(ref rect)).height <= 0f || dialogueAlpha <= 0f)
		{
			return;
		}
		Texture2D val = GetDialogueShadowTexture();
		Color color = GUI.color;
		try
		{
			GUI.color = AriandelLibrary_Utility_Window_Theme.ApplyWindowAlpha(new Color(1f, 1f, 1f, Mathf.Clamp01(dialogueAlpha)));
			GUI.DrawTexture(rect, (Texture)(object)val, (ScaleMode)0, true);
		}
		finally
		{
			GUI.color = color;
		}
	}

	public static void DrawPanel(Rect rect, float borderThickness = 2f)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		AriandelLibrary_Utility_Window_Theme.DrawPanel(rect, ConsoleThemeStyle, borderThickness);
	}

	public static void DrawInteractiveBackground(Rect rect, bool hovered, bool selected, bool enabled, bool preserveFadingSeparator = false)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		AriandelLibrary_Utility_Window_Theme.DrawInteractiveBackground(rect, hovered, selected, enabled, preserveFadingSeparator ? FadingSeparatorThemeStyle : ConsoleThemeStyle);
	}

	public static void DrawActionButtonBackground(Rect rect, bool hovered, bool enabled)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		AriandelLibrary_Utility_Window_Theme.DrawActionButtonBackground(rect, hovered, enabled, ConsoleThemeStyle);
	}

	public static void DrawBorder(Rect rect, float thickness, bool enabled = true)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		AriandelLibrary_Utility_Window_Theme.DrawBorder(rect, enabled ? LineColor : DisabledLineColor, thickness, false);
	}

	public static void DrawBookmarkBackground(Rect rect, bool hovered, bool selected, bool enabled)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		DrawInteractiveBackground(rect, hovered, selected, enabled);
		DrawBorder(rect, selected ? 2f : 1f, enabled);
	}

	public static void DrawIconBacking(Rect rect, bool enabled)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		Color val = (enabled ? ConsoleThemeStyle.InteractiveNormalColor : ConsoleThemeStyle.InteractiveDisabledColor);
		AriandelLibrary_Utility_Window_Theme.DrawSolid(rect, val);
	}

	public static void TipRegion(Rect rect, string text, MiliraImperium_ConsoleTooltipState state)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		if (state != null && AriandelLibrary_Utility_Window_Theme.WindowInteractionEnabled && !string.IsNullOrWhiteSpace(text) && Mouse.IsOver(rect))
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (!string.Equals(state.hoveredText, text, StringComparison.Ordinal) || !RectsMatch(state.hoveredRect, rect))
			{
				state.hoveredText = text;
				state.hoveredRect = rect;
				state.hoverStartedAt = realtimeSinceStartup;
			}
			state.candidateText = text;
			state.candidateRect = rect;
			state.candidateSet = true;
		}
	}

	public static void DrawTooltip(MiliraImperium_ConsoleTooltipState state, Rect bounds, float maximumWidth = 400f)
	{
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		if (state == null)
		{
			return;
		}
		try
		{
			if (!state.candidateSet || string.IsNullOrWhiteSpace(state.candidateText))
			{
				ResetHover(state);
			}
			else if (!(Time.realtimeSinceStartup - state.hoverStartedAt < 0.25f))
			{
				maximumWidth = SanitizeMaximumWidth(maximumWidth);
				EnsureTooltipMeasurement(state, maximumWidth);
				Event current = Event.current;
				Vector2 val = ((current != null) ? current.mousePosition : ((Rect)(ref state.candidateRect)).center);
				Rect boxRect = default(Rect);
				((Rect)(ref boxRect))..ctor(val.x + 16f, val.y + 18f, state.cachedBoxWidth, state.cachedBoxHeight);
				ClampTooltipToBounds(ref boxRect, val, bounds);
				Color color = GUI.color;
				GameFont font = Text.Font;
				TextAnchor anchor = Text.Anchor;
				bool wordWrap = Text.WordWrap;
				try
				{
					AriandelLibrary_Utility_Window_Theme.DrawSolid(boxRect, TooltipBackgroundColor);
					AriandelLibrary_Utility_Window_Theme.DrawBorder(boxRect, LineColor, 2f, false);
					Text.Font = (GameFont)1;
					Text.Anchor = (TextAnchor)0;
					Text.WordWrap = true;
					AriandelLibrary_Utility_Window_Theme.SetGUIColor(DetailTextColor);
					Widgets.Label(new Rect(((Rect)(ref boxRect)).x + 12f, ((Rect)(ref boxRect)).y + 10f, ((Rect)(ref boxRect)).width - 24f, ((Rect)(ref boxRect)).height - 20f), state.candidateText);
					return;
				}
				finally
				{
					Text.WordWrap = wordWrap;
					Text.Anchor = anchor;
					Text.Font = font;
					GUI.color = color;
				}
			}
		}
		finally
		{
			state.candidateText = null;
			state.candidateRect = default(Rect);
			state.candidateSet = false;
		}
	}

	private static AriandelLibrary_WindowThemeStyle CreateThemeStyle(bool fadingSeparators)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Expected O, but got Unknown
		return new AriandelLibrary_WindowThemeStyle
		{
			BorderColor = LineColor,
			AccentColor = LineColor,
			SeparatorColor = LineColor,
			PrimaryTextColor = DetailTextColor,
			ScrollTrackColor = new Color(LineColor.r, LineColor.g, LineColor.b, 0.18f),
			ScrollThumbColor = new Color(LineColor.r, LineColor.g, LineColor.b, 0.72f),
			ScrollThumbHoverColor = TintedTextColor,
			ScrollThumbActiveColor = DetailTextColor,
			UseBorderGradient = false,
			UseSeparatorGradient = fadingSeparators
		};
	}

	private static void EnsureTooltipMeasurement(MiliraImperium_ConsoleTooltipState state, float maximumWidth)
	{
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		if (string.Equals(state.cachedText, state.candidateText, StringComparison.Ordinal) && Mathf.Abs(state.cachedMaximumWidth - maximumWidth) < 0.01f)
		{
			return;
		}
		GameFont font = Text.Font;
		bool wordWrap = Text.WordWrap;
		Text.Font = (GameFont)1;
		Text.WordWrap = true;
		try
		{
			float num = Mathf.Max(1f, maximumWidth - 24f);
			float x = Text.CalcSize(state.candidateText).x;
			float num2 = Mathf.Clamp(x, 96f, num);
			float num3 = Text.CalcHeight(state.candidateText, num2);
			state.cachedText = state.candidateText;
			state.cachedMaximumWidth = maximumWidth;
			state.cachedBoxWidth = num2 + 24f;
			state.cachedBoxHeight = num3 + 20f;
		}
		finally
		{
			Text.WordWrap = wordWrap;
			Text.Font = font;
		}
	}

	private static void ClampTooltipToBounds(ref Rect boxRect, Vector2 mousePosition, Rect bounds)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		if (((Rect)(ref boxRect)).xMax > ((Rect)(ref bounds)).xMax - 4f)
		{
			((Rect)(ref boxRect)).x = mousePosition.x - ((Rect)(ref boxRect)).width - 16f;
		}
		if (((Rect)(ref boxRect)).yMax > ((Rect)(ref bounds)).yMax - 4f)
		{
			((Rect)(ref boxRect)).y = mousePosition.y - ((Rect)(ref boxRect)).height - 18f;
		}
		((Rect)(ref boxRect)).x = Mathf.Clamp(((Rect)(ref boxRect)).x, ((Rect)(ref bounds)).x + 4f, Mathf.Max(((Rect)(ref bounds)).x + 4f, ((Rect)(ref bounds)).xMax - 4f - ((Rect)(ref boxRect)).width));
		((Rect)(ref boxRect)).y = Mathf.Clamp(((Rect)(ref boxRect)).y, ((Rect)(ref bounds)).y + 4f, Mathf.Max(((Rect)(ref bounds)).y + 4f, ((Rect)(ref bounds)).yMax - 4f - ((Rect)(ref boxRect)).height));
	}

	private static void ResetHover(MiliraImperium_ConsoleTooltipState state)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		state.hoveredText = null;
		state.hoveredRect = default(Rect);
		state.hoverStartedAt = 0f;
	}

	private static float SanitizeMaximumWidth(float value)
	{
		if (float.IsNaN(value) || float.IsInfinity(value) || value < 120f)
		{
			return 400f;
		}
		return value;
	}

	private static bool RectsMatch(Rect left, Rect right)
	{
		return Mathf.Abs(((Rect)(ref left)).x - ((Rect)(ref right)).x) < 0.01f && Mathf.Abs(((Rect)(ref left)).y - ((Rect)(ref right)).y) < 0.01f && Mathf.Abs(((Rect)(ref left)).width - ((Rect)(ref right)).width) < 0.01f && Mathf.Abs(((Rect)(ref left)).height - ((Rect)(ref right)).height) < 0.01f;
	}

	private static Texture2D GetDialogueShadowTexture()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)dialogueShadowTexture != (Object)null)
		{
			return dialogueShadowTexture;
		}
		dialogueShadowTexture = new Texture2D(1, 64, (TextureFormat)4, false)
		{
			name = "MiliraImperium_LumileiaDialogueShadow",
			filterMode = (FilterMode)1,
			wrapMode = (TextureWrapMode)1,
			hideFlags = (HideFlags)61
		};
		Color[] array = (Color[])(object)new Color[64];
		float num = 63f;
		for (int i = 0; i < 64; i++)
		{
			float num2 = 1f - (float)i / num;
			float num3 = Mathf.SmoothStep(0f, 0.72f, num2);
			array[i] = new Color(0f, 0f, 0f, num3);
		}
		dialogueShadowTexture.SetPixels(array);
		dialogueShadowTexture.Apply(false, true);
		return dialogueShadowTexture;
	}
}
