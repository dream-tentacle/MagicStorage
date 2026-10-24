using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_ClaimFromHauler : JobDriver
{
	private const TargetIndex HaulerInd = (TargetIndex)1;

	private const TargetIndex NeederInd = (TargetIndex)2;

	private const TargetIndex PrimaryNeederInd = (TargetIndex)3;

	private ThingDef resourceDef;

	private Pawn Hauler
	{
		get
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			return (Pawn)(object)((thing is Pawn) ? thing : null);
		}
	}

	private Thing Needer
	{
		get
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)2);
			return ((LocalTargetInfo)(ref target)).Thing;
		}
	}

	public override void ExposeData()
	{
		((JobDriver)this).ExposeData();
		Scribe_Defs.Look<ThingDef>(ref resourceDef, "hdResourceDef");
	}

	public override string GetReport()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		Thing needer = Needer;
		if (resourceDef == null || needer == null)
		{
			return TaggedString.op_Implicit(Translator.Translate("ReportHaulingUnknown"));
		}
		return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("ReportHaulingTo", NamedArgument.op_Implicit(((Def)resourceDef).label), NamedArgumentUtility.Named((object)((Entity)needer).LabelShort, "DESTINATION"), NamedArgumentUtility.Named((object)resourceDef, "THING")));
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		if (resourceDef == null)
		{
			Pawn hauler = Hauler;
			object obj;
			if (hauler == null)
			{
				obj = null;
			}
			else
			{
				Pawn_CarryTracker carryTracker = hauler.carryTracker;
				obj = ((carryTracker == null) ? null : carryTracker.CarriedThing?.def);
			}
			resourceDef = (ThingDef)obj;
		}
		if (((LocalTargetInfo)(ref base.job.targetA)).HasThing && !ReservationUtility.Reserve(base.pawn, base.job.targetA, base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false))
		{
			return false;
		}
		Thing needer = Needer;
		IHaulEnroute val = (IHaulEnroute)(object)((needer is IHaulEnroute) ? needer : null);
		if (val != null && resourceDef != null && Needer.Spawned && ((Thing)base.pawn).Map != null)
		{
			int num = Mathf.Min(base.job.count, EnrouteSafety.SpaceRemainingSafe(Needer, resourceDef, base.pawn));
			if (num > 0)
			{
				((Thing)base.pawn).Map.enrouteManager.AddEnroute(val, base.pawn, resourceDef, num);
			}
		}
		return true;
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate
		{
			Thing needer = Needer;
			IHaulEnroute val3 = (IHaulEnroute)(object)((needer is IHaulEnroute) ? needer : null);
			if (val3 != null)
			{
				Map map = ((Thing)base.pawn).Map;
				if (map != null)
				{
					EnrouteManager enrouteManager = map.enrouteManager;
					if (enrouteManager != null)
					{
						enrouteManager.ReleaseFor(val3, base.pawn);
					}
				}
			}
		});
		ToilFailConditions.FailOn<JobDriver_ClaimFromHauler>(this, (Func<bool>)delegate
		{
			Thing needer = Needer;
			return needer == null || needer.Destroyed || !needer.Spawned;
		});
		ToilFailConditions.FailOnForbidden<JobDriver_ClaimFromHauler>(this, (TargetIndex)2);
		Toil val = Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)2, false);
		ToilFailConditions.FailOn<Toil>(val, (Func<bool>)(() => CarriedHaulShare.StorageBoundCarried(Hauler, base.pawn) == null));
		yield return val;
		Toil val2 = ToilMaker.MakeToil("HD_ClaimHandoff");
		val2.initAction = delegate
		{
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_014a: Unknown result type (might be due to invalid IL or missing references)
			//IL_012b: Unknown result type (might be due to invalid IL or missing references)
			Pawn hauler = Hauler;
			Thing val3 = CarriedHaulShare.StorageBoundCarried(hauler, base.pawn);
			if (val3 == null || (resourceDef != null && val3.def != resourceDef))
			{
				((JobDriver)this).EndJobWith((JobCondition)4);
			}
			else
			{
				int num = base.pawn.carryTracker.AvailableStackSpace(val3.def);
				int num2 = Mathf.Min(Mathf.Min(val3.stackCount, base.job.count), num);
				if (num2 <= 0)
				{
					((JobDriver)this).EndJobWith((JobCondition)4);
				}
				else
				{
					bool flag = num2 >= val3.stackCount;
					Thing val4 = default(Thing);
					hauler.carryTracker.innerContainer.TryTransferToContainer(val3, (ThingOwner)(object)base.pawn.carryTracker.innerContainer, num2, ref val4, true);
					if (val4 == null)
					{
						((JobDriver)this).EndJobWith((JobCondition)4);
					}
					else
					{
						if (flag)
						{
							hauler.jobs.EndCurrentJob((JobCondition)16, true, true);
						}
						CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
						if (comp != null)
						{
							TickManager tickManager = Find.TickManager;
							comp.lastInterceptedTick = ((tickManager != null) ? tickManager.TicksGame : 0);
						}
						if (((Thing)base.pawn).Map.reservationManager.ReservedBy(LocalTargetInfo.op_Implicit((Thing)(object)hauler), base.pawn, base.job))
						{
							((Thing)base.pawn).Map.reservationManager.Release(LocalTargetInfo.op_Implicit((Thing)(object)hauler), base.pawn, base.job);
						}
						base.job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit(val4));
					}
				}
			}
		};
		val2.defaultCompleteMode = (ToilCompleteMode)1;
		yield return val2;
		yield return (Needer is Blueprint || Needer is Frame) ? Toils_Goto.GotoBuild((TargetIndex)2) : Toils_Goto.GotoThing((TargetIndex)2, (PathEndMode)2, false);
		yield return Toils_Construct.MakeSolidThingFromBlueprintIfNecessary((TargetIndex)2, (TargetIndex)3);
		yield return Toils_Haul.DepositHauledThingInContainer((TargetIndex)2, (TargetIndex)3, (Action)null);
	}
}
