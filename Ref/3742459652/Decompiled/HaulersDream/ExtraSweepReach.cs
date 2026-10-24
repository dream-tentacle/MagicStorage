using System;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class ExtraSweepReach
{
	public static Danger Ceiling(Pawn pawn)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected I4, but got Unknown
		return (Danger)(byte)Math.Min((int)DangerUtility.NormalMaxDanger(pawn), 2);
	}

	public static bool Allows(Pawn pawn, LocalTargetInfo target, PathEndMode peMode = (PathEndMode)3)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		return ReachabilityUtility.CanReach(pawn, target, peMode, Ceiling(pawn), false, false, (TraverseMode)0);
	}
}
