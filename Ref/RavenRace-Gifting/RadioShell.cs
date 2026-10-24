using RavenRace.Features.CentralHub.UI;
using UnityEngine;
using Verse;

namespace RavenRace.Features.FusangOrganization.UI;

public sealed class RadioShell
{
	private Vector2 statusScroll;

	private Vector2 receiptScroll;

	private readonly RadioPageViewport viewport = new RadioPageViewport();

	private static readonly RadioPageId[] Tabs = new RadioPageId[5]
	{
		RadioPageId.Home,
		RadioPageId.Caravan,
		RadioPageId.Reinforcement,
		RadioPageId.Missions,
		RadioPageId.Support
	};

	private static readonly string[] Names = new string[5] { "首页", "商队", "援军", "任务", "支援" };

	public void Draw(Rect rect, RadioSession session, RadioPortrait portrait)
	{
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0375: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0474: Unknown result type (might be due to invalid IL or missing references)
		//IL_044e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0495: Unknown result type (might be due to invalid IL or missing references)
		//IL_049c: Unknown result type (might be due to invalid IL or missing references)
		float num = Mathf.Max(42f, RavenCentralUIStyle.LineHeight((GameFont)1) + 16f);
		string text = "扶桑电台 / " + session.Current.Title;
		float num2 = Mathf.Max(num, RavenCentralUIStyle.TextHeight(text, ((Rect)(ref rect)).width - 160f, (GameFont)2));
		RavenCentralUIStyle.Label(new Rect(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, ((Rect)(ref rect)).width - 160f, num2), text, (GameFont)2, (TextAnchor)3, RavenCentralUIStyle.Gold, wrap: true);
		if (RadioWidgets.Button(new Rect(((Rect)(ref rect)).xMax - 152f, ((Rect)(ref rect)).y, 88f, num), "返回", session.CanBack && !session.Dialogue.BlocksNavigation))
		{
			session.Back();
		}
		if (RadioWidgets.Button(new Rect(((Rect)(ref rect)).xMax - 56f, ((Rect)(ref rect)).y, 56f, num), "关闭"))
		{
			((Window)session.Host).Close(true);
		}
		((Rect)(ref rect)).yMin = ((Rect)(ref rect)).yMin + (num2 + 10f);
		float num3 = RadioResourceBar.Height(((Rect)(ref rect)).width);
		RadioResourceBar.Draw(new Rect(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, ((Rect)(ref rect)).width, num3));
		((Rect)(ref rect)).yMin = ((Rect)(ref rect)).yMin + (num3 + 8f);
		Rect val = default(Rect);
		((Rect)(ref val))..ctor(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, ((Rect)(ref rect)).width * 0.32f, ((Rect)(ref rect)).height);
		float num4 = Mathf.Min(((Rect)(ref val)).height * 0.52f, StatusHeight(((Rect)(ref val)).width, session));
		portrait.Draw(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width, Mathf.Max(20f, ((Rect)(ref val)).height - num4 - 8f)), session);
		DrawStatus(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).yMax - num4, ((Rect)(ref val)).width, num4), session);
		Rect val2 = default(Rect);
		((Rect)(ref val2))..ctor(((Rect)(ref val)).xMax + 14f, ((Rect)(ref rect)).y, ((Rect)(ref rect)).xMax - ((Rect)(ref val)).xMax - 14f, ((Rect)(ref rect)).height);
		int num5 = ((((Rect)(ref val2)).width < 420f) ? 3 : 5);
		int num6 = ((num5 == 5) ? 1 : 2);
		float num7 = (((Rect)(ref val2)).width - (float)(num5 - 1) * 6f) / (float)num5;
		for (int i = 0; i < Tabs.Length; i++)
		{
			RadioPageId radioPageId = Tabs[i];
			bool selected = radioPageId == session.CurrentId || (radioPageId == RadioPageId.Missions && IsMission(session.CurrentId));
			if (RadioWidgets.Button(new Rect(((Rect)(ref val2)).x + (float)(i % num5) * (num7 + 6f), ((Rect)(ref val2)).y + (float)(i / num5) * (num + 6f), num7, num), Names[i], !session.Dialogue.BlocksNavigation || radioPageId == RadioPageId.Home, selected))
			{
				session.Navigate(radioPageId);
			}
		}
		((Rect)(ref val2)).yMin = ((Rect)(ref val2)).yMin + ((float)num6 * (num + 6f) + 4f);
		if (session.CurrentId == RadioPageId.Missions || IsMission(session.CurrentId))
		{
			float num8 = (((Rect)(ref val2)).width - 6f) / 2f;
			if (RadioWidgets.Button(new Rect(((Rect)(ref val2)).x, ((Rect)(ref val2)).y, num8, num), "任务发起", enabled: true, session.CurrentId == RadioPageId.Missions))
			{
				session.Navigate(RadioPageId.Missions);
			}
			if (RadioWidgets.Button(new Rect(((Rect)(ref val2)).x + num8 + 6f, ((Rect)(ref val2)).y, num8, num), "进度追踪", enabled: true, session.CurrentId == RadioPageId.Progress))
			{
				session.Navigate(RadioPageId.Progress);
			}
			((Rect)(ref val2)).yMin = ((Rect)(ref val2)).yMin + (num + 10f);
		}
		if (!GenText.NullOrEmpty(session.Receipt))
		{
			RadioContent radioContent = new RadioContent();
			radioContent.Text(session.Receipt);
			float num9 = Mathf.Min(((Rect)(ref val2)).height * 0.2f, radioContent.Height(((Rect)(ref val2)).width - 18f));
			radioContent.Draw(new Rect(((Rect)(ref val2)).x, ((Rect)(ref val2)).yMax - num9, ((Rect)(ref val2)).width, num9), ref receiptScroll);
			((Rect)(ref val2)).yMax = ((Rect)(ref val2)).yMax - (num9 + 8f);
		}
		RavenCentralUIStyle.DrawPanel(val2, 1f);
		if (((Rect)(ref val2)).height > 40f)
		{
			viewport.Draw(GenUI.ContractedBy(val2, 12f), session);
		}
	}

	private static bool IsMission(RadioPageId id)
	{
		if (id != RadioPageId.Surrogate && id != RadioPageId.Ember && id != RadioPageId.Progress)
		{
			return id == RadioPageId.Offspring;
		}
		return true;
	}

	private static float ButtonHeight(string label, float width)
	{
		return Mathf.Max(42f, RavenCentralUIStyle.TextHeight(label, Mathf.Max(1f, width - 20f), (GameFont)1) + 16f);
	}

	private static float StatusHeight(float width, RadioSession session)
	{
		float width2 = Mathf.Max(1f, width - 20f);
		string text = $"好感度：{session.Operator.zuoYaoFavorability:N0}";
		return 20f + RavenCentralUIStyle.TextHeight("左爻 · 通讯接线员", width2, (GameFont)2) + RavenCentralUIStyle.TextHeight(text, width2, (GameFont)1) + ButtonHeight("赠礼", width2) + ButtonHeight($"奖励（{session.Operator.unlockedRewardDefs.Count}）", width2) + ButtonHeight("收藏", width2) + 24f;
	}

	private void DrawStatus(Rect rect, RadioSession session)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0343: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		RavenCentralUIStyle.DrawPanel(rect, 1f);
		if (((Rect)(ref rect)).height >= StatusHeight(((Rect)(ref rect)).width, session) && ((Rect)(ref rect)).width > 60f)
		{
			Rect val = GenUI.ContractedBy(rect, 10f);
			float y = ((Rect)(ref val)).y;
			float num = RavenCentralUIStyle.TextHeight("左爻 · 通讯接线员", ((Rect)(ref val)).width, (GameFont)2);
			RavenCentralUIStyle.Label(new Rect(((Rect)(ref val)).x, y, ((Rect)(ref val)).width, num), "左爻 · 通讯接线员", (GameFont)2, (TextAnchor)0, RavenCentralUIStyle.Gold, wrap: true);
			y += num + 6f;
			string text = $"好感度：{session.Operator.zuoYaoFavorability:N0}";
			float num2 = RavenCentralUIStyle.TextHeight(text, ((Rect)(ref val)).width, (GameFont)1);
			RavenCentralUIStyle.Label(new Rect(((Rect)(ref val)).x, y, ((Rect)(ref val)).width, num2), text, (GameFont)1, (TextAnchor)0, RavenCentralUIStyle.Text, wrap: true);
			y += num2 + 6f;
			bool enabled = !session.Dialogue.BlocksNavigation;
			float num3 = ButtonHeight("赠礼", ((Rect)(ref val)).width);
			if (RadioWidgets.Button(new Rect(((Rect)(ref val)).x, y, ((Rect)(ref val)).width, num3), "赠礼", enabled))
			{
				session.Navigate(RadioPageId.Gifts);
			}
			y += num3 + 6f;
			string label = $"奖励（{session.Operator.unlockedRewardDefs.Count}）";
			float num4 = ButtonHeight(label, ((Rect)(ref val)).width);
			if (RadioWidgets.Button(new Rect(((Rect)(ref val)).x, y, ((Rect)(ref val)).width, num4), label, enabled))
			{
				session.Navigate(RadioPageId.Rewards);
			}
			y += num4 + 6f;
			if (RadioWidgets.Button(new Rect(((Rect)(ref val)).x, y, ((Rect)(ref val)).width, ButtonHeight("收藏", ((Rect)(ref val)).width)), "收藏", enabled))
			{
				session.Navigate(RadioPageId.Collection);
			}
		}
		else
		{
			RadioContent radioContent = new RadioContent();
			radioContent.Text("左爻 · 通讯接线员", heading: true);
			radioContent.Text($"好感度：{session.Operator.zuoYaoFavorability:N0}");
			bool enabled2 = !session.Dialogue.BlocksNavigation;
			radioContent.Button("赠礼", delegate
			{
				session.Navigate(RadioPageId.Gifts);
			}, enabled2);
			radioContent.Button($"奖励（{session.Operator.unlockedRewardDefs.Count}）", delegate
			{
				session.Navigate(RadioPageId.Rewards);
			}, enabled2);
			radioContent.Button("收藏", delegate
			{
				session.Navigate(RadioPageId.Collection);
			}, enabled2);
			radioContent.Draw(GenUI.ContractedBy(rect, 10f), ref statusScroll);
		}
	}
}
