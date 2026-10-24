using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RavenRace.Features.Operator.UI;

public sealed class RadioGiftTrader : ITrader
{
	private readonly Map map;

	public TraderKindDef TraderKind => DefDatabase<TraderKindDef>.GetNamed("Base_Player", true);

	public IEnumerable<Thing> Goods => Enumerable.Empty<Thing>();

	public int RandomPriceFactorSeed => 0;

	public string TraderName => "ZuoYao";

	public bool CanTradeNow => true;

	public float TradePriceImprovementOffsetForPlayer => 0f;

	public Faction Faction => Faction.OfPlayer;

	public TradeCurrency TradeCurrency => (TradeCurrency)0;

	public RadioGiftTrader(Map map)
	{
		this.map = map;
	}

	public void GiveSoldThingToPlayer(Thing thing, int count, Pawn negotiator)
	{
	}

	public void GiveSoldThingToTrader(Thing thing, int count, Pawn negotiator)
	{
	}

	public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn negotiator)
	{
		if (map != null)
		{
			return TradeUtility.AllLaunchableThingsForTrade(map, (ITrader)(object)this);
		}
		return Enumerable.Empty<Thing>();
	}
}
