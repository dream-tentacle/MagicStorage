using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace HaulersDream;

public class Dialog_ReportIssue : Window
{
	private enum ReportKind
	{
		Bug,
		Feature,
		Compatibility,
		Other
	}

	private enum Phase
	{
		Editing,
		Sending,
		Sent,
		Failed
	}

	private enum Stage
	{
		Report,
		Attachment
	}

	private struct TypeDef(ReportKind kind, string id, string labelKey, string placeholderKey)
	{
		public ReportKind kind = kind;

		public string id = id;

		public string labelKey = labelKey;

		public string placeholderKey = placeholderKey;
	}

	private static readonly TypeDef[] Types = new TypeDef[4]
	{
		new TypeDef(ReportKind.Bug, "bug", "HaulersDream.Report.Type.Bug", "HaulersDream.Report.Placeholder.Bug"),
		new TypeDef(ReportKind.Feature, "feature", "HaulersDream.Report.Type.Feature", "HaulersDream.Report.Placeholder.Feature"),
		new TypeDef(ReportKind.Compatibility, "compatibility", "HaulersDream.Report.Type.Compatibility", "HaulersDream.Report.Placeholder.Compatibility"),
		new TypeDef(ReportKind.Other, "other", "HaulersDream.Report.Type.Other", "HaulersDream.Report.Placeholder.Other")
	};

	private ReportKind kind;

	private string description = "";

	private bool includeGameLog = true;

	private readonly List<string> attachments = new List<string>();

	private Vector2 descScroll;

	private Phase phase;

	private Stage stage;

	private string statusMsg = "";

	private string sentNote = "";

	private UnityWebRequest req;

	private string reportId;

	private int attachIndex;

	private int attachFailed;

	private readonly Action onSent;

	public override Vector2 InitialSize => new Vector2(560f, 560f);

	public Dialog_ReportIssue(Action onSent = null)
		: base((IWindowDrawing)null)
	{
		this.onSent = onSent;
		base.forcePause = true;
		base.doCloseX = true;
		base.absorbInputAroundWindow = true;
		base.closeOnClickedOutside = false;
		base.draggable = true;
		base.closeOnAccept = false;
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_041a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_0306: Unknown result type (might be due to invalid IL or missing references)
		//IL_030b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0310: Unknown result type (might be due to invalid IL or missing references)
		//IL_0335: Unknown result type (might be due to invalid IL or missing references)
		//IL_033f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0341: Unknown result type (might be due to invalid IL or missing references)
		//IL_037c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0384: Unknown result type (might be due to invalid IL or missing references)
		//IL_055a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0577: Unknown result type (might be due to invalid IL or missing references)
		//IL_0581: Unknown result type (might be due to invalid IL or missing references)
		//IL_058b: Unknown result type (might be due to invalid IL or missing references)
		//IL_048f: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_04dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0501: Unknown result type (might be due to invalid IL or missing references)
		//IL_050b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0641: Unknown result type (might be due to invalid IL or missing references)
		//IL_066b: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0601: Unknown result type (might be due to invalid IL or missing references)
		//IL_0611: Unknown result type (might be due to invalid IL or missing references)
		//IL_06da: Unknown result type (might be due to invalid IL or missing references)
		//IL_06e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0708: Unknown result type (might be due to invalid IL or missing references)
		//IL_070e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0714: Unknown result type (might be due to invalid IL or missing references)
		PollRequest();
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		Color color = GUI.color;
		Text.Font = (GameFont)2;
		Widgets.Label(new Rect(0f, 0f, ((Rect)(ref inRect)).width, 34f), Translator.Translate("HaulersDream.Report.DialogTitle"));
		Text.Font = (GameFont)1;
		if (phase == Phase.Sent)
		{
			DrawSent(inRect);
			Text.Font = font;
			Text.Anchor = anchor;
			GUI.color = color;
			return;
		}
		bool flag = phase == Phase.Editing || phase == Phase.Failed;
		float num = 40f;
		Text.Font = (GameFont)0;
		GUI.color = new Color(1f, 1f, 1f, 0.6f);
		Widgets.Label(new Rect(0f, num, ((Rect)(ref inRect)).width, 30f), Translator.Translate("HaulersDream.Report.DialogIntro"));
		GUI.color = color;
		Text.Font = (GameFont)1;
		num += 30f;
		Widgets.Label(new Rect(0f, num, ((Rect)(ref inRect)).width, 24f), Translator.Translate("HaulersDream.Report.TypeLabel"));
		num += 26f;
		float num2 = (((Rect)(ref inRect)).width - 8f) / 2f;
		for (int i = 0; i < Types.Length; i++)
		{
			int num3 = i % 2;
			int num4 = i / 2;
			if (Widgets.RadioButtonLabeled(new Rect((float)num3 * (num2 + 8f), num + (float)num4 * 30f, num2, 28f), TaggedString.op_Implicit(Translator.Translate(Types[i].labelKey)), kind == Types[i].kind, false) && flag)
			{
				kind = Types[i].kind;
			}
		}
		num += 68f;
		Widgets.Label(new Rect(0f, num, ((Rect)(ref inRect)).width, 24f), Translator.Translate("HaulersDream.Report.DescribeLabel"));
		num += 24f;
		float num5 = ((Rect)(ref inRect)).height - 36f;
		GameFont font2 = Text.Font;
		Text.Font = (GameFont)0;
		float num6 = Text.LineHeightOf((GameFont)0) * 2f + 2f;
		float num7 = (GenText.NullOrEmpty(statusMsg) ? num6 : Mathf.Max(num6, Text.CalcHeight(statusMsg, ((Rect)(ref inRect)).width)));
		Text.Font = font2;
		float num8 = 88f + num7 + 8f;
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, num, ((Rect)(ref inRect)).width, Mathf.Max(80f, num5 - num8 - num));
		description = WidgetsCompat.TextAreaScrollable(val, description, ref descScroll);
		if (description != null && description.Length > 4000)
		{
			description = description.Substring(0, 4000);
		}
		if (flag && GenText.NullOrEmpty(description))
		{
			GameFont font3 = Text.Font;
			TextAnchor anchor2 = Text.Anchor;
			Color color2 = GUI.color;
			Text.Font = (GameFont)1;
			Text.Anchor = (TextAnchor)0;
			GUI.color = new Color(0.7f, 0.7f, 0.74f, 0.6f);
			Rect val2 = val;
			((Rect)(ref val2)).xMin = ((Rect)(ref val2)).xMin + 6f;
			((Rect)(ref val2)).yMin = ((Rect)(ref val2)).yMin + 4f;
			((Rect)(ref val2)).xMax = ((Rect)(ref val2)).xMax - 16f;
			Widgets.Label(val2, Translator.Translate(PlaceholderKey()));
			GUI.color = color2;
			Text.Anchor = anchor2;
			Text.Font = font3;
		}
		num = ((Rect)(ref val)).yMax + 6f;
		Rect val3 = new Rect(0f, num, ((Rect)(ref inRect)).width, 26f);
		Widgets.CheckboxLabeled(val3, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.IncludeLog")), ref includeGameLog, false, (Texture2D)null, (Texture2D)null, false, false);
		TooltipHandler.TipRegion(val3, TipSignal.op_Implicit(Translator.Translate("HaulersDream.Report.IncludeLog.Help")));
		num += 26f;
		Rect val4 = default(Rect);
		((Rect)(ref val4))._002Ector(0f, num, 200f, 28f);
		if (Widgets.ButtonText(val4, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.AddScreenshots")), true, true, flag, (TextAnchor?)null) && flag)
		{
			Find.WindowStack.Add((Window)(object)new Dialog_PickScreenshots(attachments, 12, delegate(List<string> sel)
			{
				attachments.Clear();
				attachments.AddRange(sel);
			}));
		}
		if (attachments.Count > 0)
		{
			Text.Font = (GameFont)0;
			GUI.color = new Color(1f, 1f, 1f, 0.7f);
			Widgets.Label(new Rect(((Rect)(ref val4)).xMax + 10f, num + 6f, 130f, 22f), TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.ScreenshotsSelected", NamedArgument.op_Implicit(attachments.Count)));
			GUI.color = color;
			Text.Font = (GameFont)1;
			if (Widgets.ButtonText(new Rect(((Rect)(ref val4)).xMax + 140f, num, 80f, 28f), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.ClearScreenshots")), true, true, flag, (TextAnchor?)null) && flag)
			{
				attachments.Clear();
			}
		}
		num += 34f;
		Text.Font = (GameFont)0;
		GUI.color = new Color(1f, 1f, 1f, 0.55f);
		Widgets.Label(new Rect(0f, num, ((Rect)(ref inRect)).width, 22f), Translator.Translate("HaulersDream.Report.AutoMeta"));
		GUI.color = color;
		Text.Font = (GameFont)1;
		num += 22f;
		if (!GenText.NullOrEmpty(statusMsg))
		{
			Text.Font = (GameFont)0;
			GUI.color = ((phase == Phase.Failed) ? new Color(0.92f, 0.5f, 0.5f) : new Color(1f, 1f, 1f, 0.7f));
			Widgets.Label(new Rect(0f, num, ((Rect)(ref inRect)).width, num7), statusMsg);
			GUI.color = color;
			Text.Font = (GameFont)1;
		}
		bool flag2 = phase == Phase.Sending;
		Rect val5 = new Rect(((Rect)(ref inRect)).width - 290f, num5, 130f, 32f);
		Rect val6 = default(Rect);
		((Rect)(ref val6))._002Ector(((Rect)(ref inRect)).width - 150f, num5, 150f, 32f);
		if (Widgets.ButtonText(val5, TaggedString.op_Implicit(Translator.Translate("CancelButton")), true, true, true, (TextAnchor?)null) && !flag2)
		{
			((Window)this).Close(true);
		}
		bool flag3 = !flag2 && !GenText.NullOrEmpty(description.Trim());
		string text = TaggedString.op_Implicit(flag2 ? Translator.Translate("HaulersDream.Report.Sending") : ((phase == Phase.Failed) ? Translator.Translate("HaulersDream.Report.Retry") : Translator.Translate("HaulersDream.Report.Submit")));
		if (Widgets.ButtonText(val6, text, true, true, flag3, (TextAnchor?)null) && flag3)
		{
			Submit();
		}
		Text.Font = font;
		Text.Anchor = anchor;
		GUI.color = color;
	}

	private void DrawSent(Rect inRect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		TextAnchor anchor = Text.Anchor;
		Text.Anchor = (TextAnchor)4;
		string text = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.SentBody"));
		if (!GenText.NullOrEmpty(sentNote))
		{
			text = text + "\n\n" + sentNote;
		}
		Widgets.Label(new Rect(0f, 40f, ((Rect)(ref inRect)).width, ((Rect)(ref inRect)).height - 40f - 44f), text);
		Text.Anchor = anchor;
		if (Widgets.ButtonText(new Rect(((Rect)(ref inRect)).width / 2f - 75f, ((Rect)(ref inRect)).height - 36f, 150f, 32f), TaggedString.op_Implicit(Translator.Translate("CloseButton")), true, true, true, (TextAnchor?)null))
		{
			((Window)this).Close(true);
		}
	}

	private string PlaceholderKey()
	{
		TypeDef[] types = Types;
		for (int i = 0; i < types.Length; i++)
		{
			TypeDef typeDef = types[i];
			if (typeDef.kind == kind)
			{
				return typeDef.placeholderKey;
			}
		}
		return "HaulersDream.Report.Placeholder.Other";
	}

	private void Submit()
	{
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Expected O, but got Unknown
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		if (req != null)
		{
			req.Abort();
			req.Dispose();
			req = null;
		}
		string typeId = "other";
		TypeDef[] types = Types;
		for (int i = 0; i < types.Length; i++)
		{
			TypeDef typeDef = types[i];
			if (typeDef.kind == kind)
			{
				typeId = typeDef.id;
				break;
			}
		}
		string s = ReportApi.BuildJson(typeId, description.Trim(), includeGameLog);
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		req = ReportApi.NewRequest("https://reports.refzlund.com/report/XfgH9t8FHTV39guTPe1V86xg", "POST");
		req.uploadHandler = (UploadHandler)new UploadHandlerRaw(bytes)
		{
			contentType = "application/json; charset=utf-8"
		};
		req.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
		req.timeout = 30;
		req.SendWebRequest();
		stage = Stage.Report;
		phase = Phase.Sending;
		reportId = null;
		attachIndex = 0;
		attachFailed = 0;
		statusMsg = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Sending"));
	}

	private void StartNextAttachment()
	{
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Expected O, but got Unknown
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected O, but got Unknown
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		while (attachIndex < attachments.Count)
		{
			string text = attachments[attachIndex];
			string text2 = ReportApi.ContentTypeFor(text);
			long num = (File.Exists(text) ? new FileInfo(text).Length : 0);
			if (text2 == null || num <= 0 || num > 104857600)
			{
				HDLog.Warn("skipping report attachment (unsupported type, missing, or too large): " + text);
				attachFailed++;
				attachIndex++;
				continue;
			}
			byte[] array = File.ReadAllBytes(text);
			req = ReportApi.NewRequest(ReportApi.AttachmentUrl(reportId, Path.GetFileName(text)), "POST");
			req.uploadHandler = (UploadHandler)new UploadHandlerRaw(array)
			{
				contentType = text2
			};
			req.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
			req.timeout = 120;
			req.SendWebRequest();
			stage = Stage.Attachment;
			statusMsg = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.Uploading", NamedArgument.op_Implicit(attachIndex + 1), NamedArgument.op_Implicit(attachments.Count)));
			return;
		}
		FinishSend();
	}

	private void FinishSend()
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		phase = Phase.Sent;
		statusMsg = "";
		sentNote = ((attachFailed > 0) ? TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.SentPartial", NamedArgument.op_Implicit(attachFailed))) : "");
		HDLog.Msg("issue report submitted" + ((attachFailed > 0) ? $" ({attachFailed} attachment(s) failed)" : "") + ".");
		onSent?.Invoke();
		ReportNotifications.Refresh();
	}

	private void PollRequest()
	{
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		if (phase != Phase.Sending || req == null || !req.isDone)
		{
			return;
		}
		long responseCode = req.responseCode;
		string error = req.error;
		string text = ((req.downloadHandler != null) ? req.downloadHandler.text : null);
		req.Dispose();
		req = null;
		bool flag = responseCode >= 200 && responseCode < 300;
		if (stage == Stage.Report)
		{
			if (!flag)
			{
				Fail(responseCode, error, text);
				return;
			}
			reportId = ExtractReportId(text);
			if (reportId == null)
			{
				phase = Phase.Failed;
				statusMsg = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.ServerError", NamedArgument.op_Implicit(responseCode.ToString())));
				HDLog.Warn("issue report accepted (HTTP " + responseCode + ") but no report id was returned; nothing was persisted. Body: " + Trunc(text));
				return;
			}
			HDLog.Msg("issue report accepted (HTTP " + responseCode + "), id=" + reportId + ".");
			if (attachments.Count > 0)
			{
				attachIndex = 0;
				attachFailed = 0;
				StartNextAttachment();
			}
			else
			{
				FinishSend();
			}
		}
		else
		{
			if (!flag)
			{
				attachFailed++;
				HDLog.Warn("report attachment upload failed (HTTP " + responseCode + "): " + (error ?? text));
			}
			attachIndex++;
			StartNextAttachment();
		}
	}

	private void Fail(long code, string err, string respText)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		phase = Phase.Failed;
		switch (code)
		{
		case 429L:
			statusMsg = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.RateLimited"));
			break;
		case 0L:
			statusMsg = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.NetworkError", NamedArgument.op_Implicit(err ?? "")));
			HDLog.Warn("issue report failed (network): " + err);
			break;
		default:
			statusMsg = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.ServerError", NamedArgument.op_Implicit(code.ToString())));
			HDLog.Warn("issue report failed (HTTP " + code + "): " + respText);
			break;
		}
	}

	private static string ExtractReportId(string json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return null;
		}
		Match match = Regex.Match(json, "\"id\"\\s*:\\s*\"([^\"]+)\"");
		if (!match.Success)
		{
			return null;
		}
		return match.Groups[1].Value;
	}

	private static string Trunc(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "(empty)";
		}
		s = s.Replace("\r", " ").Replace("\n", " ");
		if (s.Length <= 300)
		{
			return s;
		}
		return s.Substring(0, 300) + "...";
	}

	public override void PostClose()
	{
		((Window)this).PostClose();
		if (req != null)
		{
			req.Abort();
			req.Dispose();
			req = null;
		}
	}
}
