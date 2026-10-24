using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace CeleTech.Base;

public class Landed_CMCTS : TradeShip, ITrader
{
	private Map map;

	public int iniSilver;

	public float soldSilverTotal;

	public float boughtSilverTotal;

	public float creditPointRemainder;

	public Landed_CMCTS()
	{
	}

	public Landed_CMCTS(Map map, TraderKindDef def, Faction faction = null)
		: base(def, faction)
	{
		this.map = map;
		((PassingShip)this).passingShipManager = map.passingShipManager;
	}

	public override void ExposeData()
	{
		((TradeShip)this).ExposeData();
		Scribe_References.Look<Map>(ref map, "map", false);
		Scribe_Values.Look<int>(ref iniSilver, "initsilver", 0, false);
		Scribe_Values.Look<float>(ref soldSilverTotal, "soldSilverTotal", 0f, false);
		Scribe_Values.Look<float>(ref boughtSilverTotal, "boughtSilverTotal", 0f, false);
		Scribe_Values.Look<float>(ref creditPointRemainder, "creditPointRemainder", 0f, false);
	}

	public override void PassingShipTick()
	{
		((TradeShip)this).PassingShipTick();
		if (((PassingShip)this).passingShipManager == null && map != null)
		{
			((PassingShip)this).passingShipManager = map.passingShipManager;
		}
	}

	public override void Depart()
	{
	}

	void ITrader.GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
	{
		GiveSoldThingToTrader_Tracked(toGive, countToGive, playerNegotiator);
	}

	void ITrader.GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
	{
		GiveSoldThingToPlayer_Tracked(toGive, countToGive, playerNegotiator);
	}

	public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
	{
		GiveSoldThingToTrader_Tracked(toGive, countToGive, playerNegotiator);
	}

	public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
	{
		GiveSoldThingToPlayer_Tracked(toGive, countToGive, playerNegotiator);
	}

	private void GiveSoldThingToTrader_Tracked(Thing toGive, int countToGive, Pawn playerNegotiator)
	{
		if (toGive != null && toGive.def == ThingDefOf.Silver && countToGive > 0)
		{
			boughtSilverTotal += countToGive;
		}
		((TradeShip)this).GiveSoldThingToTrader(toGive, countToGive, playerNegotiator);
	}

	private void GiveSoldThingToPlayer_Tracked(Thing toGive, int countToGive, Pawn playerNegotiator)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		if (toGive != null && toGive.def == ThingDefOf.Silver && countToGive > 0)
		{
			soldSilverTotal += countToGive;
		}
		Thing val = toGive.SplitOff(countToGive);
		val.PreTraded((TradeAction)1, playerNegotiator, (ITrader)(object)this);
		if (playerNegotiator != null && ((Thing)playerNegotiator).Spawned && ((Thing)playerNegotiator).Map == ((PassingShip)this).Map)
		{
			if (!GenPlace.TryPlaceThing(val, ((Thing)playerNegotiator).Position, ((PassingShip)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
			{
				Log.Error($"Could not place bought thing {val} at {((Thing)playerNegotiator).Position}");
				val.Destroy((DestroyMode)0);
			}
		}
		else
		{
			TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(((PassingShip)this).Map), ((PassingShip)this).Map, val);
		}
	}

	public bool ReachableForTrade(Pawn pawn, Thing thing)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		if (((Thing)pawn).Map == thing.Map)
		{
			return ((Thing)pawn).Map.reachability.CanReach(((Thing)pawn).Position, LocalTargetInfo.op_Implicit(thing), (PathEndMode)2, (TraverseMode)1, (Danger)2);
		}
		return false;
	}

	public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator)
	{
		foreach (Thing item in TradeUtility.AllLaunchableThingsForTrade(((PassingShip)this).Map, (ITrader)(object)this))
		{
			yield return item;
		}
		foreach (Pawn item2 in TradeUtility.AllSellableColonyPawns(((PassingShip)this).Map, false))
		{
			yield return (Thing)(object)item2;
		}
	}
}
