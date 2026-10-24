using System;
using System.Collections.Generic;
using System.Linq;
using RavenRace.Features.FusangOrganization.UI;
using RavenRace.Features.Operator.Gifting;
using RimWorld;
using UnityEngine;
using Verse;

namespace RavenRace.Features.Operator.UI;

public static class RadioGiftActions
{
	public static string Key(Thing thing)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		QualityCategory value = default(QualityCategory);
		QualityUtility.TryGetQuality(thing, ref value);
		return $"{((Def)thing.def).defName}_{((Def)(thing.Stuff?)).defName ?? "NoStuff"}_{value}";
	}

	public static int Estimate(List<TransferableOneWay> groups)
	{
		float factor = DefDatabase<GiftReactionDef>.GetNamed("Raven_GiftReaction_Default", true).favorChangePerValue;
		return groups.Where((TransferableOneWay g) => ((Transferable)g).CountToTransfer > 0 && ((Transferable)g).AnyThing != null).Sum((TransferableOneWay g) => (int)(((Transferable)g).AnyThing.MarketValue * (float)((Transferable)g).CountToTransfer * factor));
	}

	public static bool Submit(RadioSession session, List<TransferableOneWay> groups)
	{
		if (session.Map == null)
		{
			session.Report(session.MapReason);
			return false;
		}
		List<TransferableOneWay> list = groups.Where((TransferableOneWay g) => ((Transferable)g).CountToTransfer > 0).ToList();
		if (list.Count == 0)
		{
			session.Report("请先选择赠礼数量。");
			return false;
		}
		HashSet<Thing> valid = new HashSet<Thing>(new RadioGiftTrader(session.Map).ColonyThingsWillingToBuy(null));
		if (GenCollection.Any<TransferableOneWay>(list, (Predicate<TransferableOneWay>)((TransferableOneWay g) => g.things.Where((Thing t) => !t.Destroyed && valid.Contains(t)).Sum((Thing t) => t.stackCount) < ((Transferable)g).CountToTransfer)))
		{
			session.Report("赠礼物品或数量已变化，请刷新后重新确认。");
			return false;
		}
		int num = 0;
		foreach (TransferableOneWay item in list)
		{
			int num2 = ((Transferable)item).CountToTransfer;
			foreach (Thing item2 in item.things.ToList())
			{
				if (num2 > 0 && !item2.Destroyed && valid.Contains(item2))
				{
					int num3 = Mathf.Min(num2, item2.stackCount);
					GiftingManager.HandleGift(item2, num3, out var favorChange);
					num += favorChange;
					item2.SplitOff(num3).Destroy((DestroyMode)0);
					num2 -= num3;
				}
			}
			((Transferable)item).AdjustTo(0);
		}
		string text = WorldComponent_OperatorManager.PostGiftMessage + "\n\n（好感度变化：" + GenText.ToStringWithSign(num) + "）";
		WorldComponent_OperatorManager.PostGiftMessage = null;
		session.GiftReceipt(text);
		return true;
	}
}
