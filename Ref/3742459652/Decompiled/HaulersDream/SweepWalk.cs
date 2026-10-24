using System;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class SweepWalk
{
	internal static Toil MakeToil(JobDriver driver, TargetIndex stackInd, string toilName, Toil loopHead, Action skipCurrent, Func<bool> isOrderedAnchor)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		Toil obj = ToilMaker.MakeToil(toilName);
		obj.initAction = delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = driver.job.GetTarget(stackInd);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing == null || !thing.Spawned)
			{
				skipCurrent();
				driver.JumpToToil(loopHead);
			}
			else
			{
				driver.pawn.pather.StartPath(LocalTargetInfo.op_Implicit(thing), (PathEndMode)3);
			}
		};
		obj.AddPreTickAction((Action)delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = driver.job.GetTarget(stackInd);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing != null && thing.Spawned && SweepForbidPolicy.AbandonWalk(ForbidUtility.IsForbidden(thing, driver.pawn), driver.job.playerForced, isOrderedAnchor()))
			{
				skipCurrent();
				driver.JumpToToil(loopHead);
			}
		});
		obj.defaultCompleteMode = (ToilCompleteMode)2;
		return obj;
	}
}
