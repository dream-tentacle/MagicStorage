using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulersDream;

public class Dialog_PickScreenshots : Window
{
	private const int ThumbMaxEdge = 256;

	private readonly List<ScreenshotEntry> entries = new List<ScreenshotEntry>();

	private readonly HashSet<string> selected;

	private readonly int maxSelectable;

	private readonly Action<List<string>> onConfirm;

	private readonly Dictionary<string, Texture2D> thumbCache = new Dictionary<string, Texture2D>();

	private Vector2 scroll;

	private string screenshotFolder;

	public override Vector2 InitialSize => new Vector2(820f, 620f);

	public Dialog_PickScreenshots(IEnumerable<string> alreadySelected, int maxSelectable, Action<List<string>> onConfirm)
		: base((IWindowDrawing)null)
	{
		selected = new HashSet<string>(alreadySelected ?? Enumerable.Empty<string>());
		this.maxSelectable = maxSelectable;
		this.onConfirm = onConfirm;
		base.forcePause = true;
		base.doCloseX = true;
		base.absorbInputAroundWindow = true;
		base.closeOnClickedOutside = false;
		base.draggable = true;
		Rescan();
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		GameFont font = Text.Font;
		Color color = GUI.color;
		Text.Font = (GameFont)2;
		Widgets.Label(new Rect(0f, 0f, ((Rect)(ref inRect)).width, 34f), Translator.Translate("HaulersDream.Report.PickTitle"));
		Text.Font = (GameFont)1;
		GUI.color = new Color(1f, 1f, 1f, 0.6f);
		Widgets.Label(new Rect(0f, 32f, ((Rect)(ref inRect)).width, 24f), TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.PickSubtitle", NamedArgument.op_Implicit(selected.Count), NamedArgument.op_Implicit(maxSelectable)));
		GUI.color = color;
		float num = ((Rect)(ref inRect)).height - 36f;
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, 62f, ((Rect)(ref inRect)).width, num - 70f);
		if (entries.Count == 0)
		{
			TextAnchor anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)4;
			GUI.color = new Color(1f, 1f, 1f, 0.6f);
			Widgets.Label(val, Translator.Translate("HaulersDream.Report.PickEmpty"));
			GUI.color = color;
			Text.Anchor = anchor;
		}
		else
		{
			DrawGrid(val);
		}
		Rect val2 = new Rect(0f, num, 250f, 32f);
		Rect val3 = default(Rect);
		((Rect)(ref val3))._002Ector(260f, num, 110f, 32f);
		Rect val4 = default(Rect);
		((Rect)(ref val4))._002Ector(((Rect)(ref inRect)).width - 290f, num, 130f, 32f);
		Rect val5 = default(Rect);
		((Rect)(ref val5))._002Ector(((Rect)(ref inRect)).width - 150f, num, 150f, 32f);
		if (Widgets.ButtonText(val2, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.PickOpenFolder")), true, true, true, (TextAnchor?)null) && !ReportScreenshots.TryOpenFolder())
		{
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.PickOpenFolderFailed", NamedArgument.op_Implicit(screenshotFolder))), MessageTypeDefOf.RejectInput, false);
		}
		TooltipHandler.TipRegion(val2, TipSignal.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.PickOpenFolder.Help", NamedArgument.op_Implicit(screenshotFolder))));
		if (Widgets.ButtonText(val3, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.PickRefresh")), true, true, true, (TextAnchor?)null))
		{
			Rescan();
		}
		if (Widgets.ButtonText(val4, TaggedString.op_Implicit(Translator.Translate("CancelButton")), true, true, true, (TextAnchor?)null))
		{
			((Window)this).Close(true);
		}
		if (Widgets.ButtonText(val5, TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.PickConfirm", NamedArgument.op_Implicit(selected.Count))), true, true, true, (TextAnchor?)null))
		{
			onConfirm?.Invoke((from e in entries
				where selected.Contains(e.fullPath)
				select e.fullPath).ToList());
			((Window)this).Close(true);
		}
		Text.Font = font;
		GUI.color = color;
	}

	private void DrawGrid(Rect rect)
	{
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		int num = Mathf.Max(1, Mathf.FloorToInt((((Rect)(ref rect)).width + 10f) / 185f));
		float num2 = (((Rect)(ref rect)).width - 16f - (float)(num - 1) * 10f) / (float)num;
		float num3 = num2 * 9f / 16f;
		float num4 = 18f;
		float num5 = num3 + num4 + 4f;
		int num6 = Mathf.CeilToInt((float)entries.Count / (float)num);
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, 0f, ((Rect)(ref rect)).width - 16f, (float)num6 * (num5 + 10f));
		Widgets.BeginScrollView(rect, ref scroll, val, true);
		float num7 = scroll.y - num5;
		float num8 = scroll.y + ((Rect)(ref rect)).height + num5;
		Rect cell = default(Rect);
		for (int i = 0; i < entries.Count; i++)
		{
			int num9 = i % num;
			float num10 = (float)(i / num) * (num5 + 10f);
			if (!(num10 < num7) && !(num10 > num8))
			{
				((Rect)(ref cell))._002Ector((float)num9 * (num2 + 10f), num10, num2, num5);
				DrawCell(cell, entries[i], num3);
			}
		}
		Widgets.EndScrollView();
	}

	private void DrawCell(Rect cell, ScreenshotEntry e, float imgH)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(((Rect)(ref cell)).x, ((Rect)(ref cell)).y, ((Rect)(ref cell)).width, imgH);
		bool flag = selected.Contains(e.fullPath);
		Widgets.DrawBoxSolid(val, new Color(0f, 0f, 0f, 0.35f));
		Texture2D val2 = Thumb(e);
		if ((Object)(object)val2 != (Object)null)
		{
			GUI.DrawTexture(val, (Texture)(object)val2, (ScaleMode)2);
		}
		else
		{
			Widgets.Label(GenUI.ContractedBy(val, 6f), e.name);
		}
		if (Mouse.IsOver(val) && !flag)
		{
			Widgets.DrawBoxSolid(val, new Color(1f, 1f, 1f, 0.08f));
		}
		if (flag)
		{
			Widgets.DrawBoxSolid(val, new Color(0.2f, 0.8f, 0.4f, 0.18f));
			Widgets.DrawBox(val, 2, (Texture2D)null);
			Rect val3 = default(Rect);
			((Rect)(ref val3))._002Ector(((Rect)(ref val)).xMax - 26f, ((Rect)(ref val)).y + 6f, 20f, 20f);
			Widgets.DrawBoxSolid(val3, new Color(0.16f, 0.6f, 0.32f, 0.95f));
			TextAnchor anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)4;
			Widgets.Label(val3, "✓");
			Text.Anchor = anchor;
		}
		GameFont font = Text.Font;
		Color color = GUI.color;
		Text.Font = (GameFont)0;
		GUI.color = new Color(1f, 1f, 1f, 0.65f);
		Widgets.Label(new Rect(((Rect)(ref cell)).x, ((Rect)(ref val)).yMax + 2f, ((Rect)(ref cell)).width, 16f), e.modified.ToString("yyyy-MM-dd HH:mm"));
		GUI.color = color;
		Text.Font = font;
		if (Widgets.ButtonInvisible(val, true))
		{
			Toggle(e);
		}
	}

	private void Toggle(ScreenshotEntry e)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if (selected.Contains(e.fullPath))
		{
			selected.Remove(e.fullPath);
		}
		else if (selected.Count >= maxSelectable)
		{
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.PickMax", NamedArgument.op_Implicit(maxSelectable))), MessageTypeDefOf.RejectInput, false);
		}
		else
		{
			selected.Add(e.fullPath);
		}
	}

	private Texture2D Thumb(ScreenshotEntry e)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		if (thumbCache.TryGetValue(e.thumbPath, out var value))
		{
			return value;
		}
		Texture2D val = null;
		if (File.Exists(e.thumbPath))
		{
			byte[] array = File.ReadAllBytes(e.thumbPath);
			Texture2D val2 = new Texture2D(2, 2, (TextureFormat)3, true);
			if (ImageConversion.LoadImage(val2, array))
			{
				val = ShrinkToThumbnail(val2);
			}
			else
			{
				Object.Destroy((Object)(object)val2);
			}
		}
		thumbCache[e.thumbPath] = val;
		return val;
	}

	private static Texture2D ShrinkToThumbnail(Texture2D full)
	{
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Expected O, but got Unknown
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		int num = Mathf.Max(((Texture)full).width, ((Texture)full).height);
		if (num <= 256)
		{
			return full;
		}
		float num2 = 256f / (float)num;
		int num3 = Mathf.Max(1, Mathf.RoundToInt((float)((Texture)full).width * num2));
		int num4 = Mathf.Max(1, Mathf.RoundToInt((float)((Texture)full).height * num2));
		RenderTexture active = RenderTexture.active;
		RenderTexture val = null;
		Texture2D val2 = null;
		try
		{
			((Texture)full).filterMode = (FilterMode)2;
			val = RenderTexture.GetTemporary(num3, num4, 0, (RenderTextureFormat)7, (RenderTextureReadWrite)0);
			Graphics.Blit((Texture)(object)full, val);
			RenderTexture.active = val;
			val2 = new Texture2D(num3, num4, (TextureFormat)3, false);
			val2.ReadPixels(new Rect(0f, 0f, (float)num3, (float)num4), 0, 0);
			val2.Apply();
			Texture2D result = val2;
			val2 = null;
			return result;
		}
		finally
		{
			RenderTexture.active = active;
			if ((Object)(object)val != (Object)null)
			{
				RenderTexture.ReleaseTemporary(val);
			}
			Object.Destroy((Object)(object)full);
			if ((Object)(object)val2 != (Object)null)
			{
				Object.Destroy((Object)(object)val2);
			}
		}
	}

	private void Rescan()
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		ReleaseThumbs();
		entries.Clear();
		entries.AddRange(ReportScreenshots.FindRecent());
		screenshotFolder = ReportScreenshots.EnsureFolder();
		scroll = Vector2.zero;
	}

	private void ReleaseThumbs()
	{
		foreach (Texture2D value in thumbCache.Values)
		{
			if ((Object)(object)value != (Object)null)
			{
				Object.Destroy((Object)(object)value);
			}
		}
		thumbCache.Clear();
	}

	public override void PostClose()
	{
		((Window)this).PostClose();
		ReleaseThumbs();
	}
}
