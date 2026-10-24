using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HaulersDream;

public sealed class CraftBatchPlan
{
	public Bill bill;

	public RecipeDef recipe;

	public readonly List<ThingDef> ingredientDefs = new List<ThingDef>();

	public readonly List<int> perRepCounts = new List<int>();

	public bool mixingRecipe;

	public int requestedReps;

	public int availabilityReps;

	public int massReps;

	public int timeoutReps;

	public int billReps = int.MaxValue;

	public int resolvedReps;

	public float massPerRepKg;

	public int ticksPerRep;

	public int timeoutTicks;

	public bool feasible;

	public string blockReason;

	public CraftBatchLimit BindingLimit
	{
		get
		{
			int num = resolvedReps;
			if (num >= requestedReps)
			{
				return CraftBatchLimit.None;
			}
			if (num == billReps)
			{
				return CraftBatchLimit.BillRepeat;
			}
			if (num == availabilityReps)
			{
				return CraftBatchLimit.Resources;
			}
			if (num == timeoutReps)
			{
				return CraftBatchLimit.Timeout;
			}
			if (num == massReps && OverloadGate.NoOverload(HaulersDreamMod.Settings))
			{
				return CraftBatchLimit.Mass;
			}
			return CraftBatchLimit.None;
		}
	}
}
