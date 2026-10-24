using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RavenRace.Features.CommunityEvents;

public class JobDriver_DeliverCoupleMeals : JobDriver
{
	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		if (ReservationUtility.Reserve(base.pawn, base.job.targetA, base.job, 1, base.job.count, (ReservationLayerDef)null, errorOnFailed, false))
		{
			return ReservationUtility.Reserve(base.pawn, base.job.targetB, base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false);
		}
		return false;
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		ToilFailConditions.FailOnDestroyedNullOrForbidden<JobDriver_DeliverCoupleMeals>(this, (TargetIndex)1);
		ToilFailConditions.FailOnDespawnedNullOrForbidden<JobDriver_DeliverCoupleMeals>(this, (TargetIndex)2);
		ToilFailConditions.FailOn<JobDriver_DeliverCoupleMeals>(this, (Func<bool>)(() => QuestPart_RavenHungryCouple.For(((LocalTargetInfo)(ref base.job.targetB)).Pawn) == null));
		ToilFailConditions.FailOn<JobDriver_DeliverCoupleMeals>(this, (Func<bool>)(() => ((LocalTargetInfo)(ref base.job.targetB)).Pawn.Downed || ((LocalTargetInfo)(ref base.job.targetB)).Pawn.InMentalState));
		yield return Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)3, false);
		yield return Toils_Haul.StartCarryThing((TargetIndex)1, false, false, false, true, false);
		yield return Toils_Goto.GotoThing((TargetIndex)2, (PathEndMode)2, false);
		yield return Toils_Interpersonal.GotoInteractablePosition((TargetIndex)2);
		yield return Toils_General.Do((Action)delegate
		{
			QuestPart_RavenHungryCouple.For(((LocalTargetInfo)(ref base.job.targetB)).Pawn)?.Receive(base.pawn.carryTracker.CarriedThing);
		});
	}
}
