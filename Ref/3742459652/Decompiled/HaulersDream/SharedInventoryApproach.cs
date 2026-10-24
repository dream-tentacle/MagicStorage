using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class SharedInventoryApproach
{
	public static void MaybeApproach(Thing carriedStack, Pawn worker)
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		if (FloatMenuMakerMap.makingFor == null)
		{
			IThingHolder obj = ((carriedStack != null) ? carriedStack.ParentHolder : null);
			Pawn val = ((Pawn_InventoryTracker)(((obj is Pawn_InventoryTracker) ? obj : null)?)).pawn;
			if (val != null && worker != null && val != worker && !val.Drafted && IsIdle(val) && ((Thing)val).Spawned && ReachabilityUtility.CanReach(val, LocalTargetInfo.op_Implicit((Thing)(object)worker), (PathEndMode)2, (Danger)2, false, false, (TraverseMode)0))
			{
				Job val2 = JobMaker.MakeJob(JobDefOf.Goto, LocalTargetInfo.op_Implicit((Thing)(object)worker));
				val2.locomotionUrgency = (LocomotionUrgency)2;
				val2.collideWithPawns = false;
				val.jobs.TryTakeOrderedJob(val2, (JobTag?)(JobTag)0, false);
			}
		}
	}

	private static bool IsIdle(Pawn p)
	{
		if (p.jobs == null)
		{
			return false;
		}
		if (p.jobs.jobQueue != null && p.jobs.jobQueue.Count > 0)
		{
			return false;
		}
		if (p.CurJob == null)
		{
			return true;
		}
		JobDef curJobDef = p.CurJobDef;
		if (curJobDef != JobDefOf.Wait && curJobDef != JobDefOf.Wait_Wander && curJobDef != JobDefOf.GotoWander)
		{
			return curJobDef == JobDefOf.Wait_MaintainPosture;
		}
		return true;
	}
}
