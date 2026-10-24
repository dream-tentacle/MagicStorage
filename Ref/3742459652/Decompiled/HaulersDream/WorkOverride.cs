using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class WorkOverride
{
	private static readonly Dictionary<ThingDef, List<WorkTypeDef>> billWorkTypesByBench = new Dictionary<ThingDef, List<WorkTypeDef>>();

	internal static bool IsForceEnabled(WorkTypeDef def, HaulersDreamSettings s)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		if (def == null)
		{
			return false;
		}
		if (s.allPawnsCanHaul && (def.workTags & 0x4000) != 0)
		{
			return true;
		}
		if (s.allPawnsCanClean && (def.workTags & 0x2000) != 0)
		{
			return true;
		}
		if (s.allPawnsCanCutPlants && def == WorkTypeDefOf.PlantCutting)
		{
			return true;
		}
		return false;
	}

	internal static bool CanDoBillsAt(Pawn pawn, Building_WorkTable bench)
	{
		if (pawn == null || ((Thing)(bench?)).def == null)
		{
			return false;
		}
		List<WorkTypeDef> list = BillWorkTypesFor(((Thing)bench).def);
		if (list.Count == 0)
		{
			return true;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (!WorkCapabilityProbe.IsDisabled(pawn, list[i]))
			{
				return true;
			}
		}
		return false;
	}

	internal static bool HidePlanCraftForUnassigned(Pawn pawn, Building_WorkTable bench)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || settings.planForUnassignedWork)
		{
			return false;
		}
		if (pawn?.workSettings == null || ((Thing)(bench?)).def == null)
		{
			return false;
		}
		BillStack billStack = bench.BillStack;
		List<Bill> list = ((billStack != null) ? billStack.Bills : null);
		if (list == null)
		{
			return false;
		}
		List<WorkTypeDef> list2 = BillWorkTypesFor(((Thing)bench).def);
		for (int i = 0; i < list.Count; i++)
		{
			Bill val = list[i];
			if (!CraftBatchPlanner.CanPawnBatch(pawn, val))
			{
				continue;
			}
			WorkTypeDef val2 = val.recipe?.requiredGiverWorkType;
			if (val2 != null)
			{
				if (pawn.workSettings.GetPriority(val2) > 0)
				{
					return false;
				}
				continue;
			}
			if (list2.Count == 0)
			{
				return false;
			}
			for (int j = 0; j < list2.Count; j++)
			{
				if (pawn.workSettings.GetPriority(list2[j]) > 0)
				{
					return false;
				}
			}
		}
		return true;
	}

	private static List<WorkTypeDef> BillWorkTypesFor(ThingDef benchDef)
	{
		if (billWorkTypesByBench.TryGetValue(benchDef, out var value))
		{
			return value;
		}
		List<WorkTypeDef> list = new List<WorkTypeDef>();
		List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			WorkGiverDef val = allDefsListForReading[i];
			if (val.workType != null && val.fixedBillGiverDefs != null && typeof(WorkGiver_DoBill).IsAssignableFrom(val.giverClass) && val.fixedBillGiverDefs.Contains(benchDef) && !list.Contains(val.workType))
			{
				list.Add(val.workType);
			}
		}
		billWorkTypesByBench[benchDef] = list;
		return list;
	}
}
