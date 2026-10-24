using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace CeleTech.Base;

internal class JobDriver_TradewithCMCTS : JobDriver
{
	private ThingWithComps Trader
	{
		get
		{
			Thing targetThingA = ((JobDriver)this).TargetThingA;
			return (ThingWithComps)(object)((targetThingA is ThingWithComps) ? targetThingA : null);
		}
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		return ReservationUtility.Reserve(base.pawn, LocalTargetInfo.op_Implicit((Thing)(object)Trader), base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false);
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		Comp_TraderShuttle comp = ThingCompUtility.TryGetComp<Comp_TraderShuttle>((Thing)(object)Trader);
		ToilFailConditions.FailOnDespawnedOrNull<JobDriver_TradewithCMCTS>(this, (TargetIndex)1);
		yield return ToilFailConditions.FailOn<Toil>(Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)4, false), (Func<bool>)(() => comp == null || !((TradeShip)comp.tradeShip).CanTradeNow));
		Toil trade = new Toil();
		trade.initAction = delegate
		{
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Expected O, but got Unknown
			Pawn actor = trade.actor;
			if (((TradeShip)comp.tradeShip).CanTradeNow)
			{
				Find.WindowStack.Add((Window)new Dialog_Trade(actor, (ITrader)(object)comp.tradeShip, false));
			}
		};
		yield return trade;
	}
}
