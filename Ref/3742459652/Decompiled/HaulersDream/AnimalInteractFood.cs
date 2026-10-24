using System;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class AnimalInteractFood
{
	[ThreadStatic]
	private static TickKeyedMemo<float> reserveMemo;

	static AnimalInteractFood()
	{
		CacheRegistry.Register(Clear);
	}

	internal static float ReserveNutritionFor(Pawn pawn)
	{
		Pawn_JobTracker val = pawn?.jobs;
		if (val == null)
		{
			return 0f;
		}
		TickManager tickManager = Find.TickManager;
		int currentTick = ((tickManager != null) ? tickManager.TicksGame : (-1));
		int thingIDNumber = ((Thing)pawn).thingIDNumber;
		if (reserveMemo.TryGet(currentTick, thingIDNumber, out var value))
		{
			return value;
		}
		float num = ReserveOf(val.curJob, val.curDriver);
		JobQueue jobQueue = val.jobQueue;
		if (jobQueue != null)
		{
			for (int i = 0; i < jobQueue.Count; i++)
			{
				float num2 = ReserveOf(jobQueue[i]?.job, null);
				if (num2 > num)
				{
					num = num2;
				}
			}
		}
		reserveMemo.Store(currentTick, thingIDNumber, num);
		return num;
	}

	internal static bool HasAnimalInteractJob(Pawn pawn)
	{
		return ReserveNutritionFor(pawn) > 0f;
	}

	internal static bool IsInteractFood(ThingDef def)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected I4, but got Unknown
		if (def?.ingestible != null)
		{
			return AnimalInteractFoodKeepMath.IsInteractFood(def.IsIngestible, def.IsDrug, (int)def.ingestible.preferability);
		}
		return false;
	}

	internal static bool IsHeldForInteraction(Pawn pawn, Thing thing)
	{
		if (pawn != null && thing != null && IsInteractFood(thing.def))
		{
			return HasAnimalInteractJob(pawn);
		}
		return false;
	}

	private static float ReserveOf(Job job, JobDriver driver)
	{
		if (job == null)
		{
			return 0f;
		}
		Type type = job.workGiverDef?.giverClass;
		if ((!(type != null) || !typeof(WorkGiver_InteractAnimal).IsAssignableFrom(type)) && job.def != JobDefOf.Tame && job.def != JobDefOf.Train && !(driver is JobDriver_InteractAnimal))
		{
			return 0f;
		}
		Thing thing = ((LocalTargetInfo)(ref job.targetA)).Thing;
		Pawn val = (Pawn)(object)((thing is Pawn) ? thing : null);
		if (val == null)
		{
			return 2.4f;
		}
		Need_Food val2 = val.needs?.food;
		if (val2 == null)
		{
			return 0f;
		}
		return AnimalInteractFoodKeepMath.ReserveNutrition(((Need)val2).MaxLevel);
	}

	internal static void Clear()
	{
		reserveMemo.Clear();
	}
}
