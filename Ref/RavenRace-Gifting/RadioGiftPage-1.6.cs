using System;
using System.Collections.Generic;
using System.Linq;
using RavenRace.Features.CentralHub.UI;
using RavenRace.Features.FusangOrganization.UI;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RavenRace.Features.Operator.UI;

public sealed class RadioGiftPage : RadioPage
{
	private List<TransferableOneWay> groups = new List<TransferableOneWay>();

	private TransferableOneWayWidget widget;

	private Vector2 horizontalScroll;

	private Vector2 footerScroll;

	public override string Title => "赠礼";

	public RadioGiftPage(RadioSession session)
		: base(session)
	{
	}

	public override void Enter()
	{
		Refresh();
	}

	private void Refresh()
	{
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Expected O, but got Unknown
		RadioGiftWidgetState obj = ((widget == null) ? null : new RadioGiftWidgetState(widget));
		Dictionary<string, int> amounts = groups.Where((TransferableOneWay g) => ((Transferable)g).AnyThing != null).ToDictionary((TransferableOneWay g) => RadioGiftActions.Key(((Transferable)g).AnyThing), (TransferableOneWay g) => ((Transferable)g).CountToTransfer);
		groups = new RadioGiftTrader(Session.Map).ColonyThingsWillingToBuy(null).GroupBy(RadioGiftActions.Key).Select(delegate(IGrouping<string, Thing> group)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Expected O, but got Unknown
			TransferableOneWay val = new TransferableOneWay();
			val.things.AddRange(group);
			if (amounts.TryGetValue(group.Key, out var value))
			{
				((Transferable)val).AdjustTo(Mathf.Min(value, group.Sum((Thing t) => t.stackCount)));
			}
			return val;
		})
			.ToList();
		widget = new TransferableOneWayWidget((IEnumerable<TransferableOneWay>)groups, (string)null, (string)null, "可用数量", true, (IgnorePawnsInventoryMode)3, true, (Func<float>)null, 0f, false, (PlanetTile?)null, true, false, false, false, false, false, false, false, false, false);
		obj?.Restore(widget);
	}

	public override void Draw(Rect rect)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		float num = RavenCentralUIStyle.LineHeight((GameFont)1) + 18f;
		if (RadioWidgets.Button(new Rect(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, ((Rect)(ref rect)).width, num), "刷新可用物品"))
		{
			Refresh();
		}
		((Rect)(ref rect)).yMin = ((Rect)(ref rect)).yMin + (num + 8f);
		RadioContent radioContent = new RadioContent();
		if (groups.Count == 0)
		{
			radioContent.Text("没有可用的赠礼物品。请准备可用于交易的物品后刷新。");
		}
		radioContent.Text("预估好感度变化：" + GenText.ToStringWithSign(RadioGiftActions.Estimate(groups)) + "\n实际变化以赠礼回执为准。");
		if (Session.Map == null)
		{
			radioContent.Text(Session.MapReason);
		}
		radioContent.Button("送出所选礼物", delegate
		{
			if (RadioGiftActions.Submit(Session, groups))
			{
				Refresh();
			}
		}, Session.Map != null && GenCollection.Any<TransferableOneWay>(groups, (Predicate<TransferableOneWay>)((TransferableOneWay g) => ((Transferable)g).CountToTransfer > 0)));
		float num2 = Mathf.Min(((Rect)(ref rect)).height * 0.45f, radioContent.Height(((Rect)(ref rect)).width - 18f));
		Rect val = default(Rect);
		((Rect)(ref val))..ctor(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, ((Rect)(ref rect)).width, ((Rect)(ref rect)).height - num2 - 10f);
		float num3 = Mathf.Max(660f, ((Rect)(ref val)).width - 18f);
		float num4 = Mathf.Max(100f, ((Rect)(ref val)).height - 20f);
		Widgets.BeginScrollView(val, ref horizontalScroll, new Rect(0f, 0f, num3, num4), true);
		try
		{
			bool flag = default(bool);
			widget.OnGUI(new Rect(0f, 0f, num3, num4), ref flag);
		}
		finally
		{
			Widgets.EndScrollView();
		}
		radioContent.Draw(new Rect(((Rect)(ref rect)).x, ((Rect)(ref rect)).yMax - num2, ((Rect)(ref rect)).width, num2), ref footerScroll);
	}
}
