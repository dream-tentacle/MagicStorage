using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RavenRace.Features.FeatherGift;

public class JobDriver_GiveFeather : JobDriver
{
	private Pawn Receiver => ((LocalTargetInfo)(ref base.job.targetA)).Pawn;

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		return ReservationUtility.Reserve(base.pawn, LocalTargetInfo.op_Implicit((Thing)(object)Receiver), base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false);
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		ToilFailConditions.FailOn<JobDriver_GiveFeather>(this, (Func<bool>)(() => !FeatherGiftUtility.Eligible(base.pawn, Receiver) || GenHostility.AnyHostileActiveThreatToPlayer(((Thing)base.pawn).Map, false, false)));
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate
		{
			Pawn receiver = Receiver;
			if (((receiver != null) ? receiver.CurJobDef : null) == FeatherGiftDefOf.Raven_ReceiveFeather && ((LocalTargetInfo)(ref Receiver.CurJob.targetA)).Pawn == base.pawn)
			{
				Receiver.jobs.EndCurrentJob((JobCondition)16, true, true);
			}
		});
		base.job.expiryInterval = 2500;
		base.job.checkOverrideOnExpire = false;
		yield return Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)2, false);
		yield return Toils_General.Do((Action)delegate
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			if (!FeatherGiftUtility.Idle(Receiver))
			{
				((JobDriver)this).EndJobWith((JobCondition)4);
			}
			else
			{
				Receiver.jobs.StartJob(JobMaker.MakeJob(FeatherGiftDefOf.Raven_ReceiveFeather, LocalTargetInfo.op_Implicit((Thing)(object)base.pawn)), (JobCondition)8, (ThinkNode)null, false, true, (ThinkTreeDef)null, (JobTag?)null, false, false, (bool?)null, false, true, false);
			}
		});
		Toil val = Toils_General.Wait(120, (TargetIndex)0);
		ToilFailConditions.FailOn<Toil>(val, (Func<bool>)(() => Receiver.CurJobDef != FeatherGiftDefOf.Raven_ReceiveFeather || ((LocalTargetInfo)(ref Receiver.CurJob.targetA)).Pawn != base.pawn || !GenAdj.AdjacentTo8WayOrInside(((Thing)base.pawn).Position, ((Thing)Receiver).Position)));
		val.tickAction = delegate
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			base.pawn.rotationTracker.FaceTarget(LocalTargetInfo.op_Implicit((Thing)(object)Receiver));
		};
		ToilEffects.WithProgressBarToilDelay(val, (TargetIndex)1, false, -0.5f);
		yield return val;
		yield return Toils_General.Do((Action)delegate
		{
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_010c: Expected O, but got Unknown
			if (!FeatherGiftUtility.Eligible(base.pawn, Receiver))
			{
				((JobDriver)this).EndJobWith((JobCondition)4);
			}
			else
			{
				Apparel_GiftFeather apparel_GiftFeather = (Apparel_GiftFeather)(object)ThingMaker.MakeThing(FeatherGiftDefOf.Raven_Apparel_GiftFeather, (ThingDef)null);
				apparel_GiftFeather.Bind(base.pawn, Receiver);
				Receiver.apparel.Wear((Apparel)(object)apparel_GiftFeather, true, false);
				if (((Apparel)apparel_GiftFeather).Wearer != Receiver)
				{
					((Thing)apparel_GiftFeather).Destroy((DestroyMode)0);
					((JobDriver)this).EndJobWith((JobCondition)4);
				}
				else
				{
					Current.Game.GetComponent<GameComponent_FeatherGifts>().Record(base.pawn, Receiver);
					Pawn_OutfitTracker outfits = Receiver.outfits;
					if (outfits != null)
					{
						outfits.forcedHandler.SetForced((Apparel)(object)apparel_GiftFeather, true);
					}
					Find.LetterStack.ReceiveLetter(Translator.Translate("RavenFeather_LetterTitle"), TranslatorFormattedStringExtensions.Translate("RavenFeather_LetterBody", NamedArgument.op_Implicit(((Entity)base.pawn).LabelShort), NamedArgument.op_Implicit(((Entity)Receiver).LabelShort)), LetterDefOf.PositiveEvent, new LookTargets((IEnumerable<Thing>)(object)new Thing[2]
					{
						(Thing)base.pawn,
						(Thing)Receiver
					}), (Faction)null, (Quest)null, (List<ThingDef>)null, (string)null, 0, true);
				}
			}
		});
	}
}
