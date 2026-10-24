using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace HaulersDream;

[StaticConstructorOnStartup]
internal static class ReportNotifications
{
	private static bool started;

	private static UnityWebRequest req;

	private static List<ReportStatus> reports = new List<ReportStatus>();

	private static List<NotifyCard> visible = new List<NotifyCard>();

	private static string latestVersion;

	private static bool outdated;

	private static NotifyThreshold plannedThreshold = NotifyThreshold.All;

	private const float CardW = 300f;

	private const float CardH = 50f;

	private const float CardHWarn = 68f;

	private const float MarginRight = 24f;

	private const float MarginBottom = 22f;

	private const float CardGap = 7f;

	private const int MaxCards = 4;

	private static Texture2D dotTex;

	private static Texture2D refreshTex;

	private static Texture2D DotTex
	{
		get
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Expected O, but got Unknown
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)dotTex == (Object)null)
			{
				Texture2D val = new Texture2D(16, 16, (TextureFormat)5, false)
				{
					wrapMode = (TextureWrapMode)1
				};
				Color[] array = (Color[])(object)new Color[256];
				float num = 7.5f;
				Vector2 val2 = default(Vector2);
				((Vector2)(ref val2))._002Ector(7.5f, 7.5f);
				for (int i = 0; i < 16; i++)
				{
					for (int j = 0; j < 16; j++)
					{
						array[i * 16 + j] = new Color(1f, 1f, 1f, Mathf.Clamp01(num - Vector2.Distance(new Vector2((float)j, (float)i), val2) + 0.5f));
					}
				}
				val.SetPixels(array);
				val.Apply();
				dotTex = val;
			}
			return dotTex;
		}
	}

	private static Texture2D RefreshTex
	{
		get
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Expected O, but got Unknown
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0157: Unknown result type (might be due to invalid IL or missing references)
			//IL_0159: Unknown result type (might be due to invalid IL or missing references)
			//IL_015b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0185: Unknown result type (might be due to invalid IL or missing references)
			//IL_018a: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)refreshTex == (Object)null)
			{
				Texture2D val = new Texture2D(32, 32, (TextureFormat)5, false)
				{
					wrapMode = (TextureWrapMode)1
				};
				Color[] array = (Color[])(object)new Color[1024];
				float num = 15.5f;
				float num2 = 15.5f;
				float num3 = 0.61086524f;
				Vector2 val2 = new Vector2(num + 9.5f * Mathf.Cos(num3), num2 + 9.5f * Mathf.Sin(num3));
				Vector2 val3 = default(Vector2);
				((Vector2)(ref val3))._002Ector(Mathf.Cos(num3), Mathf.Sin(num3));
				Vector2 val4 = default(Vector2);
				((Vector2)(ref val4))._002Ector(Mathf.Sin(num3), 0f - Mathf.Cos(num3));
				Vector2 a = val2 + val4 * 6.5f;
				Vector2 b = val2 + val3 * 4.5f;
				Vector2 c = val2 - val3 * 4.5f;
				for (int i = 0; i < 32; i++)
				{
					for (int j = 0; j < 32; j++)
					{
						Vector2 val5 = new Vector2((float)j, (float)i);
						float num4 = Vector2.Distance(val5, new Vector2(num, num2));
						float num5 = Mathf.Atan2((float)i - num2, (float)j - num) * 57.29578f;
						if (num5 < 0f)
						{
							num5 += 360f;
						}
						float num6 = 0f;
						if (num5 < 35f || num5 > 95f)
						{
							num6 = Mathf.Clamp01(2f - Mathf.Abs(num4 - 9.5f) + 0.5f);
						}
						if (InTriangle(val5, a, b, c))
						{
							num6 = 1f;
						}
						array[i * 32 + j] = new Color(1f, 1f, 1f, num6);
					}
				}
				val.SetPixels(array);
				val.Apply();
				refreshTex = val;
			}
			return refreshTex;
		}
	}

	public static void OnMainMenuGUI()
	{
		try
		{
			Pump();
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings != null && reports.Count > 0 && settings.notifyThreshold != plannedThreshold)
			{
				Replan();
			}
			Draw();
		}
		catch (Exception ex)
		{
			HDLog.ErrOnce("main-menu notifications failed: " + HDFault.Render(ex), 1296977748);
		}
	}

	public static void Refresh()
	{
		if (req != null)
		{
			req.Abort();
			req.Dispose();
			req = null;
		}
		started = false;
		reports = new List<ReportStatus>();
		visible = new List<NotifyCard>();
	}

	private static void Pump()
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return;
		}
		if (!started)
		{
			if (settings.notifyThreshold == NotifyThreshold.Never)
			{
				return;
			}
			string reporterId = settings.reporterId;
			if (GenText.NullOrEmpty(reporterId))
			{
				return;
			}
			started = true;
			StartPoll(reporterId);
		}
		if (req != null && req.isDone)
		{
			long responseCode = req.responseCode;
			string error = req.error;
			string text = ((req.downloadHandler != null) ? req.downloadHandler.text : null);
			req.Dispose();
			req = null;
			if (responseCode >= 200 && responseCode < 300 && !GenText.NullOrEmpty(text))
			{
				Apply(text);
			}
			else if (responseCode != 0L)
			{
				HDLog.WarnOnce("notification poll failed (HTTP " + responseCode + "): " + error, 1313885233);
			}
		}
	}

	private static void StartPoll(string reporterId)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		req = ReportApi.NewRequest(ReportApi.StatusUrl(), "GET");
		req.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
		req.timeout = 20;
		req.SetRequestHeader("X-Reporter-Id", reporterId);
		req.SendWebRequest();
	}

	private static void Apply(string text)
	{
		StatusFeed statusFeed = ReportApi.ParseReportStatuses(text);
		reports = statusFeed.reports ?? new List<ReportStatus>();
		latestVersion = statusFeed.latestVersion;
		outdated = VersionCompare.IsOutdated(ReportApi.ModVersionString(), latestVersion);
		Replan();
	}

	private static void Replan()
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			visible = new List<NotifyCard>();
			return;
		}
		visible = ReportNotifyPlanner.Plan(reports, settings.notifySeenComment, settings.notifyDismissed, settings.notifyThreshold);
		plannedThreshold = settings.notifyThreshold;
	}

	private static void Open(NotifyCard c)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		MarkSeen(c);
		if (HaulersDreamMod.Instance != null)
		{
			Find.WindowStack.Add((Window)new Dialog_ModSettings((Mod)(object)HaulersDreamMod.Instance));
		}
		if (!GenText.NullOrEmpty(c.ReportId))
		{
			Find.WindowStack.Add((Window)(object)new Dialog_MyReports(c.ReportId));
		}
	}

	private static void MarkSeen(NotifyCard c)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && c != null && !GenText.NullOrEmpty(c.ReportId))
		{
			if (settings.notifySeenComment == null)
			{
				settings.notifySeenComment = new Dictionary<string, long>();
			}
			settings.notifySeenComment[c.ReportId] = ReportNotifyPlanner.SeenAfterClick(AsStatus(c));
			settings.notifyDismissed?.Remove(c.ReportId);
			Replan();
			((ModSettings)settings).Write();
		}
	}

	private static void Dismiss(NotifyCard c)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && c != null && !GenText.NullOrEmpty(c.ReportId))
		{
			if (settings.notifyDismissed == null)
			{
				settings.notifyDismissed = new Dictionary<string, long>();
			}
			settings.notifyDismissed[c.ReportId] = ReportNotifyPlanner.DismissedAfterX(AsStatus(c));
			Replan();
			((ModSettings)settings).Write();
		}
	}

	private static ReportStatus AsStatus(NotifyCard c)
	{
		return new ReportStatus
		{
			ReportId = c.ReportId,
			Title = c.Title,
			Url = c.Url,
			Status = c.Status,
			StatusAt = c.StatusAt,
			LastCommentAt = c.LastCommentAt
		};
	}

	private static void Draw()
	{
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && settings.notifyThreshold != NotifyThreshold.Never && visible != null && visible.Count != 0)
		{
			int num = Mathf.Min(visible.Count, 4);
			int num2 = visible.Count - num;
			float num3 = (float)UI.screenWidth - 300f - 24f;
			float num4 = (float)UI.screenHeight - 22f;
			NotifyCard toOpen = null;
			NotifyCard toDismiss = null;
			bool toRefresh = false;
			for (int i = 0; i < num; i++)
			{
				NotifyCard notifyCard = visible[i];
				bool flag = notifyCard.Kind == NotifyCardKind.Resolved && outdated;
				float num5 = (flag ? 68f : 50f);
				float num6 = num4 - num5;
				DrawCard(new Rect(num3, num6, 300f, num5), notifyCard, flag, ref toOpen, ref toDismiss, ref toRefresh);
				num4 = num6 - 7f;
			}
			if (num2 > 0)
			{
				TextAnchor anchor = Text.Anchor;
				GameFont font = Text.Font;
				Color color = GUI.color;
				Text.Font = (GameFont)0;
				Text.Anchor = (TextAnchor)5;
				GUI.color = new Color(1f, 1f, 1f, 0.6f);
				Widgets.Label(new Rect(num3, num4 - 18f, 300f, 16f), TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.Notify.MoreCount", NamedArgument.op_Implicit(num2)));
				Text.Anchor = anchor;
				Text.Font = font;
				GUI.color = color;
			}
			if (toRefresh)
			{
				Refresh();
			}
			else if (toDismiss != null)
			{
				Dismiss(toDismiss);
			}
			else if (toOpen != null)
			{
				Open(toOpen);
			}
		}
	}

	private static void DrawCard(Rect card, NotifyCard c, bool warn, ref NotifyCard toOpen, ref NotifyCard toDismiss, ref bool toRefresh)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Unknown result type (might be due to invalid IL or missing references)
		//IL_029f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_031c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0303: Unknown result type (might be due to invalid IL or missing references)
		//IL_0308: Unknown result type (might be due to invalid IL or missing references)
		//IL_0330: Unknown result type (might be due to invalid IL or missing references)
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		TextAnchor anchor = Text.Anchor;
		GameFont font = Text.Font;
		Color color = GUI.color;
		Color val = StatusColorFor(c);
		Widgets.DrawBoxSolid(card, new Color(0.05f, 0.06f, 0.08f, 0.55f));
		if (Mouse.IsOver(card))
		{
			Widgets.DrawHighlight(card);
		}
		float num = ((Rect)(ref card)).x + 9f + 7f + 7f;
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(((Rect)(ref card)).xMax - 9f - 16f, ((Rect)(ref card)).y + 7f, 16f, 16f);
		Rect val3 = default(Rect);
		((Rect)(ref val3))._002Ector(((Rect)(ref val2)).x - 5f - 16f, ((Rect)(ref card)).y + 7f, 16f, 16f);
		bool flag = Mouse.IsOver(val2);
		bool flag2 = Mouse.IsOver(val3);
		GUI.color = val;
		GUI.DrawTexture(new Rect(((Rect)(ref card)).x + 9f, ((Rect)(ref card)).y + 12f, 7f, 7f), (Texture)(object)DotTex);
		Text.Font = (GameFont)1;
		Text.Anchor = (TextAnchor)3;
		GUI.color = Lighten(val);
		Widgets.Label(new Rect(num, ((Rect)(ref card)).y + 4f, ((Rect)(ref val3)).x - num - 6f, 24f), StatusWord(c));
		GUI.color = new Color(1f, 1f, 1f, flag2 ? 0.95f : 0.5f);
		GUI.DrawTexture(GenUI.ContractedBy(val3, 1f), (Texture)(object)RefreshTex);
		if (flag2)
		{
			TooltipHandler.TipRegion(val3, TipSignal.op_Implicit(Translator.Translate("HaulersDream.Report.MyReports.Refresh")));
		}
		Text.Font = (GameFont)0;
		Text.Anchor = (TextAnchor)4;
		GUI.color = new Color(1f, 1f, 1f, flag ? 0.95f : 0.42f);
		Widgets.Label(val2, "✕");
		float num2 = ((Rect)(ref card)).y + 28f;
		Rect val4 = default(Rect);
		((Rect)(ref val4))._002Ector(((Rect)(ref card)).xMax - 9f - 92f, num2, 92f, 16f);
		Text.Font = (GameFont)0;
		Text.Anchor = (TextAnchor)0;
		GUI.color = new Color(1f, 1f, 1f, 0.6f);
		Widgets.Label(new Rect(num, num2, ((Rect)(ref val4)).x - num - 8f, 16f), c.Title ?? "");
		Text.Anchor = (TextAnchor)2;
		GUI.color = new Color(1f, 1f, 1f, 0.32f);
		Widgets.Label(val4, Translator.Translate("HaulersDream.Report.Notify.Source"));
		if (warn)
		{
			Text.Anchor = (TextAnchor)0;
			GUI.color = new Color(1f, 0.78f, 0.28f, 0.9f);
			Widgets.Label(new Rect(num, num2 + 16f, ((Rect)(ref card)).xMax - 9f - num, 16f), TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.Notify.OutOfDate", NamedArgument.op_Implicit(ReportApi.ModVersionString())));
		}
		Text.Font = font;
		Text.Anchor = anchor;
		GUI.color = color;
		if (Widgets.ButtonInvisible(val3, true))
		{
			toRefresh = true;
		}
		else if (Widgets.ButtonInvisible(val2, true))
		{
			toDismiss = c;
		}
		else if (Widgets.ButtonInvisible(card, true))
		{
			toOpen = c;
		}
	}

	private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		float num = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
		float num2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
		float num3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
		bool flag = num < 0f || num2 < 0f || num3 < 0f;
		bool flag2 = num > 0f || num2 > 0f || num3 > 0f;
		return !(flag && flag2);
	}

	private static Color Lighten(Color c, float t = 0.28f)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		return new Color(Mathf.Lerp(c.r, 1f, t), Mathf.Lerp(c.g, 1f, t), Mathf.Lerp(c.b, 1f, t), 1f);
	}

	private static Color StatusColorFor(NotifyCard c)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		return (Color)(c.Kind switch
		{
			NotifyCardKind.Comment => new Color(0.95f, 0.8f, 0.3f), 
			NotifyCardKind.Resolved => new Color(0.62f, 0.47f, 0.85f), 
			NotifyCardKind.Closed => new Color(0.82f, 0.4f, 0.4f), 
			_ => new Color(0.38f, 0.76f, 0.45f), 
		});
	}

	private static string StatusWord(NotifyCard c)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		return c.Kind switch
		{
			NotifyCardKind.Comment => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Notify.Word.Comment")), 
			NotifyCardKind.Resolved => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Notify.Word.Resolved")), 
			NotifyCardKind.Closed => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Notify.Word.Closed")), 
			_ => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Notify.Word.Open")), 
		};
	}
}
