using System;
using System.Collections.Generic;
using RavenRace.Features.Operator;
using RavenRace.Features.Operator.UI;
using RimWorld;
using Verse;

namespace RavenRace.Features.FusangOrganization.UI;

public sealed class RadioSession
{
	public readonly Dialog_FusangComm Host;

	public readonly Thing Radio;

	public readonly Faction Faction;

	public readonly WorldComponent_Fusang World;

	public readonly WorldComponent_OperatorManager Operator;

	public readonly RadioDialogue Dialogue;

	private readonly Dictionary<RadioPageId, RadioPage> pages = new Dictionary<RadioPageId, RadioPage>();

	public RadioPageId CurrentId { get; private set; }

	public RadioPage Current => pages[CurrentId];

	public string Receipt { get; private set; } = string.Empty;

	public bool CanBack => CurrentId != RadioPageId.Home;

	public Map Map
	{
		get
		{
			if (Radio == null || Radio.Destroyed)
			{
				return null;
			}
			return Radio.Map;
		}
	}

	public string MapReason
	{
		get
		{
			if (Map != null)
			{
				return string.Empty;
			}
			return "电台已失效或已离开地图。";
		}
	}

	public string TradeStatus
	{
		get
		{
			if (!World.CanTradeNow())
			{
				return $"贸易冷却：{(float)World.GetTicksUntilTrade() / 60000f:F1} 天";
			}
			return "贸易链路就绪";
		}
	}

	public RadioSession(Dialog_FusangComm host, Thing radio)
	{
		Host = host;
		Radio = radio;
		Faction = Find.FactionManager.FirstFactionOfDef(RavenDefOf.Fusang_Hidden);
		World = Find.World.GetComponent<WorldComponent_Fusang>();
		Operator = Find.World.GetComponent<WorldComponent_OperatorManager>();
		Dialogue = new RadioDialogue();
		pages.Add(RadioPageId.Home, new RadioHomePage(this));
		Current.Enter();
	}

	public void Report(string text)
	{
		Receipt = text;
	}

	public void Navigate(RadioPageId id)
	{
		if (id == CurrentId)
		{
			return;
		}
		if (Dialogue.BlocksNavigation)
		{
			Report("请先完成左爻的对话选择。");
			return;
		}
		Host.Transition(delegate
		{
			Show(id);
		});
	}

	public void Back()
	{
		if (CurrentId == RadioPageId.Home)
		{
			((Window)Host).Close(true);
		}
		else if (Dialogue.BlocksNavigation)
		{
			Report("请先完成左爻的对话选择。");
		}
		else if (!Current.TryBack())
		{
			RadioPageId parent = ((CurrentId == RadioPageId.Surrogate || CurrentId == RadioPageId.Ember || CurrentId == RadioPageId.Progress || CurrentId == RadioPageId.Offspring) ? RadioPageId.Missions : RadioPageId.Home);
			Host.Transition(delegate
			{
				Show(parent);
			});
		}
	}

	public void GiftReceipt(string text)
	{
		Dialogue.ShowGiftReceipt(text);
		Receipt = "赠礼已送达。";
		Navigate(RadioPageId.Home);
	}

	private void Show(RadioPageId id)
	{
		if (!pages.ContainsKey(id))
		{
			pages.Add(id, Create(id));
		}
		CurrentId = id;
		((Window)Host).forcePause = Current.Pause;
		Current.Enter();
	}

	private RadioPage Create(RadioPageId id)
	{
		return id switch
		{
			RadioPageId.Caravan => new RadioCaravanPage(this), 
			RadioPageId.Reinforcement => new RadioReinforcementPage(this), 
			RadioPageId.Missions => new RadioMissionsPage(this), 
			RadioPageId.Support => new RadioSupportPage(this), 
			RadioPageId.Surrogate => new RadioSurrogatePage(this), 
			RadioPageId.Ember => new RadioEmberPage(this), 
			RadioPageId.Progress => new RadioProgressPage(this), 
			RadioPageId.Offspring => new RadioOffspringPage(this), 
			RadioPageId.Gifts => new RadioGiftPage(this), 
			RadioPageId.Rewards => new RadioRewardsPage(this), 
			RadioPageId.Collection => new RadioCollectionPage(this), 
			_ => throw new ArgumentOutOfRangeException("id"), 
		};
	}
}
