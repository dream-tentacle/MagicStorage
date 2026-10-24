using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace HaulersDream;

public class Dialog_MyReports : Window
{
	private enum View
	{
		Loading,
		List,
		Detail,
		Error
	}

	private enum Pending
	{
		None,
		List,
		Thread,
		Comment
	}

	private View view;

	private Pending pending;

	private string statusMsg = "";

	private UnityWebRequest req;

	private List<ReportSummary> reports;

	private ReportThread thread;

	private string detailReportId;

	private string commentDraft = "";

	private string commentMsg = "";

	private bool resetScrollOnThread;

	private bool autoOpenedCreate;

	private string deepLinkReportId;

	private Vector2 listScroll;

	private Vector2 detailScroll;

	public override Vector2 InitialSize => new Vector2(640f, 640f);

	public Dialog_MyReports()
		: base((IWindowDrawing)null)
	{
		base.forcePause = true;
		base.doCloseX = true;
		base.absorbInputAroundWindow = true;
		base.closeOnClickedOutside = false;
		base.draggable = true;
		base.closeOnAccept = false;
	}

	public Dialog_MyReports(string openReportId)
		: this()
	{
		deepLinkReportId = openReportId;
	}

	public override void PostOpen()
	{
		((Window)this).PostOpen();
		if (!GenText.NullOrEmpty(deepLinkReportId))
		{
			LoadThreadById(deepLinkReportId);
		}
		else
		{
			LoadList();
		}
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
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		PollRequest();
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		Color color = GUI.color;
		Text.Font = (GameFont)2;
		Widgets.Label(new Rect(0f, 0f, ((Rect)(ref inRect)).width, 34f), Translator.Translate("HaulersDream.Report.MyReports.Title"));
		Text.Font = (GameFont)1;
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, 42f, ((Rect)(ref inRect)).width, ((Rect)(ref inRect)).height - 42f);
		switch (view)
		{
		case View.Loading:
			DrawCentered(val, GenText.NullOrEmpty(statusMsg) ? TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.MyReports.Loading")) : statusMsg);
			break;
		case View.Error:
			DrawCentered(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width, ((Rect)(ref val)).height - 44f), statusMsg);
			DrawListButtons(inRect);
			break;
		case View.List:
			DrawList(val, inRect);
			break;
		case View.Detail:
			DrawDetail(val, inRect);
			break;
		}
		Text.Font = font;
		Text.Anchor = anchor;
		GUI.color = color;
	}

	private void DrawList(Rect body, Rect inRect)
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		float num = ((Rect)(ref inRect)).height - 36f;
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, ((Rect)(ref body)).y, ((Rect)(ref inRect)).width, num - ((Rect)(ref body)).y - 8f);
		if (reports == null || reports.Count == 0)
		{
			DrawCentered(val, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.MyReports.Empty")));
		}
		else
		{
			Rect val2 = default(Rect);
			((Rect)(ref val2))._002Ector(0f, 0f, ((Rect)(ref val)).width - 16f, (float)reports.Count * 54f);
			Widgets.BeginScrollView(val, ref listScroll, val2, true);
			float num2 = 0f;
			Rect val3 = default(Rect);
			foreach (ReportSummary report in reports)
			{
				((Rect)(ref val3))._002Ector(0f, num2, ((Rect)(ref val2)).width, 50f);
				if (Mouse.IsOver(val3))
				{
					Widgets.DrawHighlight(val3);
				}
				DrawReportRow(val3, report);
				if (Widgets.ButtonInvisible(val3, true))
				{
					LoadThread(report);
				}
				num2 += 54f;
			}
			Widgets.EndScrollView();
		}
		DrawListButtons(inRect);
	}

	private void DrawListButtons(Rect inRect)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		float num = ((Rect)(ref inRect)).height - 36f;
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, num, 200f, 32f);
		if (Widgets.ButtonText(val, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.MyReports.CreateNew")), true, true, true, (TextAnchor?)null))
		{
			OpenCreate();
		}
		if (Widgets.ButtonText(new Rect(((Rect)(ref val)).xMax + 8f, num, 120f, 32f), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.MyReports.Refresh")), true, true, true, (TextAnchor?)null))
		{
			LoadList();
		}
	}

	private void DrawReportRow(Rect row, ReportSummary r)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		TextAnchor anchor = Text.Anchor;
		Rect val = GenUI.ContractedBy(row, 6f);
		DrawStatusPill(new Rect(((Rect)(ref val)).xMax - 96f, ((Rect)(ref val)).y + 2f, 96f, 22f), r.status);
		Text.Anchor = (TextAnchor)0;
		string text = (GenText.NullOrEmpty(r.title) ? ("(" + TypeLabel(r.type) + ")") : r.title);
		Widgets.Label(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width - 96f - 8f, 24f), text);
		Text.Font = (GameFont)0;
		GUI.color = new Color(1f, 1f, 1f, 0.6f);
		Widgets.Label(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).y + 24f, ((Rect)(ref val)).width, 18f), SubLine(r));
		GUI.color = Color.white;
		Text.Font = (GameFont)1;
		Text.Anchor = anchor;
	}

	private void DrawDetail(Rect body, Rect inRect)
	{
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Unknown result type (might be due to invalid IL or missing references)
		//IL_032c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0584: Unknown result type (might be due to invalid IL or missing references)
		//IL_058e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0498: Unknown result type (might be due to invalid IL or missing references)
		//IL_0376: Unknown result type (might be due to invalid IL or missing references)
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0383: Unknown result type (might be due to invalid IL or missing references)
		//IL_0392: Unknown result type (might be due to invalid IL or missing references)
		//IL_0397: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0436: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_05bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0630: Unknown result type (might be due to invalid IL or missing references)
		//IL_0637: Unknown result type (might be due to invalid IL or missing references)
		//IL_0545: Unknown result type (might be due to invalid IL or missing references)
		//IL_054c: Unknown result type (might be due to invalid IL or missing references)
		//IL_052a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0534: Unknown result type (might be due to invalid IL or missing references)
		//IL_0714: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_06b2: Unknown result type (might be due to invalid IL or missing references)
		float num = ((Rect)(ref inRect)).height - 36f;
		bool flag = thread != null && thread.issueNumber > 0.0;
		float num2 = (flag ? 76f : 24f);
		float num3 = num - 8f - num2;
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, ((Rect)(ref body)).y, ((Rect)(ref inRect)).width, num3 - ((Rect)(ref body)).y - 8f);
		float num4 = ((Rect)(ref val)).width - 16f;
		int valueOrDefault = (thread?.comments?.Count).GetValueOrDefault();
		float num5 = 26f;
		if (thread != null && !GenText.NullOrEmpty(thread.comment))
		{
			num5 += Text.CalcHeight(thread.comment, num4) + 14f;
		}
		num5 += 26f;
		if (thread != null && thread.commentsUnavailable)
		{
			num5 += 24f;
		}
		else if (valueOrDefault == 0)
		{
			num5 += 24f;
		}
		else if (thread != null)
		{
			foreach (IssueComment comment in thread.comments)
			{
				num5 += CommentHeight(comment, num4) + 8f;
			}
		}
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(0f, 0f, num4, Mathf.Max(num5, ((Rect)(ref val)).height));
		Widgets.BeginScrollView(val, ref detailScroll, val2, true);
		float num6 = 0f;
		string text = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.Detail.Status", NamedArgument.op_Implicit(StatusLabel(thread?.status))));
		if (flag)
		{
			text = text + "  ·  #" + ((long)thread.issueNumber).ToString(CultureInfo.InvariantCulture);
		}
		Widgets.Label(new Rect(0f, num6, num4, 24f), text);
		num6 += 26f;
		if (thread != null && !GenText.NullOrEmpty(thread.comment))
		{
			float num7 = Text.CalcHeight(thread.comment, num4);
			Widgets.Label(new Rect(0f, num6, num4, num7), thread.comment);
			num6 += num7 + 14f;
		}
		Widgets.Label(new Rect(0f, num6, num4, 24f), TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.Detail.CommentsHeader", NamedArgument.op_Implicit(valueOrDefault)));
		num6 += 26f;
		if (thread != null && thread.commentsUnavailable)
		{
			DrawDim(new Rect(0f, num6, num4, 24f), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Detail.Unavailable")));
		}
		else if (valueOrDefault == 0)
		{
			DrawDim(new Rect(0f, num6, num4, 24f), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Detail.NoComments")));
		}
		else
		{
			foreach (IssueComment comment2 in thread.comments)
			{
				float num8 = CommentHeight(comment2, num4);
				Rect val3 = new Rect(0f, num6, num4, num8);
				Widgets.DrawBoxSolid(val3, RoleTint(comment2.role));
				Rect val4 = GenUI.ContractedBy(val3, 6f);
				Text.Font = (GameFont)0;
				GUI.color = new Color(1f, 1f, 1f, 0.6f);
				Widgets.Label(new Rect(((Rect)(ref val4)).x, ((Rect)(ref val4)).y, ((Rect)(ref val4)).width, 16f), CommentAuthor(comment2) + " · " + FmtIso(comment2.createdAt));
				GUI.color = Color.white;
				Text.Font = (GameFont)1;
				Widgets.Label(new Rect(((Rect)(ref val4)).x, ((Rect)(ref val4)).y + 18f, ((Rect)(ref val4)).width, ((Rect)(ref val4)).height - 18f), comment2.body ?? "");
				num6 += num8 + 8f;
			}
		}
		Widgets.EndScrollView();
		if (flag)
		{
			string text2 = Widgets.TextArea(new Rect(0f, num3, ((Rect)(ref inRect)).width - 96f, num2), commentDraft ?? "", false);
			if (text2 != commentDraft)
			{
				commentDraft = ((text2.Length > 4000) ? text2.Substring(0, 4000) : text2);
			}
			Rect val5 = default(Rect);
			((Rect)(ref val5))._002Ector(((Rect)(ref inRect)).width - 88f, num3, 88f, 30f);
			if (pending == Pending.Comment)
			{
				DrawDim(new Rect(((Rect)(ref val5)).x, num3 + 34f, 88f, 40f), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Detail.Sending")));
			}
			else if (Widgets.ButtonText(val5, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Detail.Send")), true, true, true, (TextAnchor?)null))
			{
				SendComment();
			}
		}
		else
		{
			DrawDim(new Rect(0f, num3, ((Rect)(ref inRect)).width, 24f), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Detail.NoIssue")));
		}
		Rect val6 = default(Rect);
		((Rect)(ref val6))._002Ector(0f, num, 100f, 32f);
		if (Widgets.ButtonText(val6, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Detail.Back")), true, true, true, (TextAnchor?)null))
		{
			thread = null;
			commentDraft = "";
			commentMsg = "";
			if (reports == null)
			{
				LoadList();
			}
			else
			{
				view = View.List;
			}
		}
		float num9 = ((Rect)(ref val6)).xMax + 8f;
		Rect val7 = default(Rect);
		((Rect)(ref val7))._002Ector(num9, num, 110f, 32f);
		if (Widgets.ButtonText(val7, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.MyReports.Refresh")), true, true, true, (TextAnchor?)null) && pending == Pending.None)
		{
			commentMsg = "";
			RefreshThreadSilent();
		}
		num9 = ((Rect)(ref val7)).xMax + 8f;
		if (thread != null && !GenText.NullOrEmpty(thread.url))
		{
			Rect val8 = default(Rect);
			((Rect)(ref val8))._002Ector(num9, num, 180f, 32f);
			if (Widgets.ButtonText(val8, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Detail.OpenOnGitHub")), true, true, true, (TextAnchor?)null))
			{
				Application.OpenURL(thread.url);
			}
			num9 = ((Rect)(ref val8)).xMax + 8f;
		}
		if (!GenText.NullOrEmpty(commentMsg))
		{
			DrawDim(new Rect(num9, num + 6f, ((Rect)(ref inRect)).width - num9, 24f), commentMsg);
		}
	}

	private static float CommentHeight(IssueComment c, float width)
	{
		float num = Text.CalcHeight(GenText.NullOrEmpty(c.body) ? " " : c.body, width - 12f);
		return 18f + num + 12f;
	}

	private void LoadList()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		AbortReq();
		reports = null;
		thread = null;
		view = View.Loading;
		statusMsg = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.MyReports.Loading"));
		pending = Pending.List;
		StartGet(ReportApi.MyReportsUrl());
	}

	private void LoadThread(ReportSummary r)
	{
		LoadThreadById(r.id);
	}

	private void LoadThreadById(string reportId)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		AbortReq();
		thread = null;
		detailReportId = reportId;
		commentDraft = "";
		commentMsg = "";
		view = View.Loading;
		statusMsg = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Detail.Loading"));
		pending = Pending.Thread;
		resetScrollOnThread = true;
		StartGet(ReportApi.ThreadUrl(reportId));
	}

	private void RefreshThreadSilent()
	{
		if (!GenText.NullOrEmpty(detailReportId))
		{
			AbortReq();
			pending = Pending.Thread;
			resetScrollOnThread = false;
			StartGet(ReportApi.ThreadUrl(detailReportId));
		}
	}

	private void SendComment()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		if (GenText.NullOrEmpty(commentDraft) || commentDraft.Trim().Length == 0)
		{
			commentMsg = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Detail.CommentEmpty"));
		}
		else if (pending == Pending.None && !GenText.NullOrEmpty(detailReportId))
		{
			commentMsg = "";
			pending = Pending.Comment;
			StartPost(ReportApi.CommentUrl(detailReportId), ReportApi.BuildCommentJson(commentDraft));
		}
	}

	private void StartGet(string url)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		req = ReportApi.NewRequest(url, "GET");
		req.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
		req.timeout = 30;
		req.SetRequestHeader("X-Reporter-Id", ReportApi.ReporterId());
		req.SendWebRequest();
	}

	private void StartPost(string url, string json)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected O, but got Unknown
		byte[] bytes = Encoding.UTF8.GetBytes(json ?? "{}");
		req = ReportApi.NewRequest(url, "POST");
		req.uploadHandler = (UploadHandler)new UploadHandlerRaw(bytes);
		req.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
		req.timeout = 30;
		req.SetRequestHeader("Content-Type", "application/json");
		req.SetRequestHeader("X-Reporter-Id", ReportApi.ReporterId());
		req.SendWebRequest();
	}

	private void PollRequest()
	{
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		if (this.pending == Pending.None || req == null || !req.isDone)
		{
			return;
		}
		long responseCode = req.responseCode;
		string error = req.error;
		string text = ((req.downloadHandler != null) ? req.downloadHandler.text : null);
		Pending pending = this.pending;
		req.Dispose();
		req = null;
		this.pending = Pending.None;
		bool flag = responseCode >= 200 && responseCode < 300;
		switch (pending)
		{
		case Pending.List:
			if (!flag)
			{
				ShowError(responseCode, error, text);
				break;
			}
			reports = ReportApi.ParseMyReports(text) ?? new List<ReportSummary>();
			view = View.List;
			if (reports.Count == 0 && !autoOpenedCreate)
			{
				autoOpenedCreate = true;
				OpenCreate();
			}
			break;
		case Pending.Thread:
			if (!flag)
			{
				if (!resetScrollOnThread && thread != null)
				{
					HDLog.Warn("thread refresh failed (HTTP " + responseCode + ")");
				}
				else
				{
					ShowError(responseCode, error, text);
				}
				break;
			}
			thread = ReportApi.ParseThread(text);
			if (thread == null)
			{
				ShowError(responseCode, error, text);
				break;
			}
			view = View.Detail;
			if (resetScrollOnThread)
			{
				detailScroll = Vector2.zero;
			}
			break;
		default:
			if (!flag)
			{
				commentMsg = TaggedString.op_Implicit((responseCode == 0L) ? TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.MyReports.NetworkError", NamedArgument.op_Implicit(error ?? "")) : TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.Detail.CommentFailed", NamedArgument.op_Implicit(responseCode.ToString())));
				HDLog.Warn("comment post failed (HTTP " + responseCode + "): " + (error ?? text));
			}
			else
			{
				commentDraft = "";
				commentMsg = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Detail.CommentPosted"));
				RefreshThreadSilent();
			}
			break;
		}
	}

	private void ShowError(long code, string err, string respText)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		view = View.Error;
		statusMsg = TaggedString.op_Implicit((code == 0L) ? TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.MyReports.NetworkError", NamedArgument.op_Implicit(err ?? "")) : TranslatorFormattedStringExtensions.Translate("HaulersDream.Report.MyReports.LoadError", NamedArgument.op_Implicit(code.ToString())));
		HDLog.Warn("my-reports load failed (HTTP " + code + "): " + (err ?? respText));
	}

	private void OpenCreate()
	{
		Find.WindowStack.Add((Window)(object)new Dialog_ReportIssue(LoadList));
	}

	private void AbortReq()
	{
		if (req != null)
		{
			req.Abort();
			req.Dispose();
			req = null;
		}
		pending = Pending.None;
	}

	public override void PostClose()
	{
		((Window)this).PostClose();
		AbortReq();
	}

	private static void DrawCentered(Rect rect, string text)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		TextAnchor anchor = Text.Anchor;
		Color color = GUI.color;
		Text.Anchor = (TextAnchor)4;
		GUI.color = new Color(1f, 1f, 1f, 0.7f);
		Widgets.Label(rect, text);
		Text.Anchor = anchor;
		GUI.color = color;
	}

	private static void DrawDim(Rect rect, string text)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		Color color = GUI.color;
		GameFont font = Text.Font;
		Text.Font = (GameFont)0;
		GUI.color = new Color(1f, 1f, 1f, 0.6f);
		Widgets.Label(rect, text);
		GUI.color = color;
		Text.Font = font;
	}

	private static void DrawStatusPill(Rect rect, string status)
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		Widgets.DrawBoxSolid(rect, (Color)(status switch
		{
			"closed" => new Color(0.5f, 0.22f, 0.22f, 0.5f), 
			"solved" => new Color(0.4f, 0.3f, 0.62f, 0.55f), 
			"open" => new Color(0.16f, 0.5f, 0.28f, 0.5f), 
			_ => new Color(1f, 1f, 1f, 0.12f), 
		}));
		TextAnchor anchor = Text.Anchor;
		GameFont font = Text.Font;
		Text.Font = (GameFont)0;
		Text.Anchor = (TextAnchor)4;
		Widgets.Label(rect, StatusSymbol(status) + " " + StatusLabel(status));
		Text.Anchor = anchor;
		Text.Font = font;
	}

	private static string StatusSymbol(string status)
	{
		return status switch
		{
			"open" => "●", 
			"solved" => "✓", 
			"closed" => "✕", 
			_ => "○", 
		};
	}

	private static string StatusLabel(string status)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		return status switch
		{
			"open" => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Status.Open")), 
			"solved" => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Status.Solved")), 
			"closed" => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Status.Closed")), 
			_ => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Status.Submitted")), 
		};
	}

	private static Color RoleTint(string role)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		if (!(role == "reporter"))
		{
			if (role == "maintainer")
			{
				return new Color(0.16f, 0.5f, 0.28f, 0.12f);
			}
			return new Color(1f, 1f, 1f, 0.04f);
		}
		return new Color(0.3f, 0.5f, 0.85f, 0.12f);
	}

	private static string CommentAuthor(IssueComment c)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		string role = c.role;
		if (!(role == "reporter"))
		{
			if (role == "maintainer")
			{
				return TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Role.Developer"));
			}
			if (!GenText.NullOrEmpty(c.author))
			{
				return c.author;
			}
			return "GitHub";
		}
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Role.You"));
	}

	private static string TypeLabel(string type)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		return type switch
		{
			"bug" => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Type.Bug")), 
			"feature" => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Type.Feature")), 
			"compatibility" => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Type.Compatibility")), 
			_ => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.Type.Other")), 
		};
	}

	private static string SubLine(ReportSummary r)
	{
		string text = FmtTime(r.createdAt);
		if (!GenText.NullOrEmpty(r.type))
		{
			return TypeLabel(r.type) + " · " + text;
		}
		return text;
	}

	private static string FmtTime(double ms)
	{
		if (ms <= 0.0 || ms > 253402300799999.0)
		{
			return "";
		}
		return DateTimeOffset.FromUnixTimeMilliseconds((long)ms).LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
	}

	private static string FmtIso(string iso)
	{
		if (!GenText.NullOrEmpty(iso) && DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
		{
			return result.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
		}
		return iso ?? "";
	}
}
