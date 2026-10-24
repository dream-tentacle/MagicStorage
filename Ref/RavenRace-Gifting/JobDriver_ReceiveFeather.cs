using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RavenRace.Features.FeatherGift;

public class JobDriver_ReceiveFeather : JobDriver
{
	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		return true;
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		ToilFailConditions.FailOn<JobDriver_ReceiveFeather>(this, (Func<bool>)(() => !FeatherGiftUtility.Available(base.pawn) || !FeatherGiftUtility.Available(((LocalTargetInfo)(ref base.job.targetA)).Pawn) || ((Thing)((LocalTargetInfo)(ref base.job.targetA)).Pawn).Map != ((Thing)base.pawn).Map || ((LocalTargetInfo)(ref base.job.targetA)).Pawn.CurJobDef != FeatherGiftDefOf.Raven_GiveFeather || ((LocalTargetInfo)(ref ((LocalTargetInfo)(ref base.job.targetA)).Pawn.CurJob.targetA)).Pawn != base.pawn));
		Toil val = Toils_General.Wait(300, (TargetIndex)0);
		val.tickAction = delegate
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			base.pawn.rotationTracker.FaceTarget(base.job.targetA);
		};
		yield return val;
	}
}
