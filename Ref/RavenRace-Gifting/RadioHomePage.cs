using System;
using System.Collections.Generic;
using RavenRace.Features.CentralHub.UI;
using UnityEngine;
using Verse;

namespace RavenRace.Features.FusangOrganization.UI;

public sealed class RadioHomePage : RadioPage
{
	private Vector2 scroll;

	private int eggs;

	private int ready;

	public override string Title => "通讯首页";

	public override bool Pause => false;

	public RadioHomePage(RadioSession session)
		: base(session)
	{
	}

	public override void Enter()
	{
		List<RadioProgressEntry> list = RadioProgressQuery.Read(Session);
		eggs = GenCollection.Count<RadioProgressEntry>(list, (Predicate<RadioProgressEntry>)((RadioProgressEntry e) => e.Stage == 0));
		ready = GenCollection.Count<RadioProgressEntry>(list, (Predicate<RadioProgressEntry>)delegate(RadioProgressEntry e)
		{
			if (e.Stage > 0)
			{
				RadioSession session = Session;
				Thing thing = e.Thing;
				return RadioGrowthActions.Ready(session, (Pawn)(object)((thing is Pawn) ? thing : null), e.Stage);
			}
			return false;
		});
	}

	public override void Draw(Rect rect)
	{
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		RadioContent radioContent = new RadioContent();
		radioContent.Text("左爻", heading: true);
		radioContent.Add((float w) => RavenCentralUIStyle.TextHeight(Session.Dialogue.FullText, w, (GameFont)1) + 14f, delegate(Rect r)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			RavenCentralUIStyle.Label(r, Session.Dialogue.VisibleText, (GameFont)1, (TextAnchor)0, RavenCentralUIStyle.Text, wrap: true);
			if (GUI.enabled && Widgets.ButtonInvisible(r, true))
			{
				Session.Dialogue.Skip();
			}
		});
		if (Session.Dialogue.BlocksNavigation)
		{
			Session.Dialogue.Choices(radioContent);
		}
		else
		{
			radioContent.Text($"{Session.TradeStatus}\n任务快照：{eggs} 枚灵卵，{Session.World.surrogatePawns.Count} 名子嗣，其中 {ready} 名可处理考核。\n待领取奖励：{Session.Operator.unlockedRewardDefs.Count} 份。");
			radioContent.Button("商队 · 选择货品类别与贸易接入", delegate
			{
				Session.Navigate(RadioPageId.Caravan);
			});
			radioContent.Button("援军 · 选择梯队与空降地点", delegate
			{
				Session.Navigate(RadioPageId.Reinforcement);
			});
			radioContent.Button("任务 · 发起协议与追踪进度", delegate
			{
				Session.Navigate(RadioPageId.Missions);
			});
			radioContent.Button("支援 · 捐赠白银获取资源", delegate
			{
				Session.Navigate(RadioPageId.Support);
			});
		}
		radioContent.Draw(rect, ref scroll);
	}
}
