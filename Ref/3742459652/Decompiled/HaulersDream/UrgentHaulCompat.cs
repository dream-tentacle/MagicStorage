using System.Collections.Generic;
using Verse;

namespace HaulersDream;

internal static class UrgentHaulCompat
{
	private static bool resolved;

	private static DesignationDef keyzUrgentDef;

	private static DesignationDef allowToolUrgentDef;

	internal static bool AnyUrgentDefResolved
	{
		get
		{
			EnsureResolved();
			if (keyzUrgentDef == null)
			{
				return allowToolUrgentDef != null;
			}
			return true;
		}
	}

	private static void EnsureResolved()
	{
		if (!resolved)
		{
			resolved = true;
			keyzUrgentDef = DefDatabase<DesignationDef>.GetNamedSilentFail("KAU_HaulUrgentlyDesignation");
			allowToolUrgentDef = DefDatabase<DesignationDef>.GetNamedSilentFail("HaulUrgentlyDesignation");
		}
	}

	internal static bool IsUrgentDesignationDef(DesignationDef def)
	{
		if (def == null)
		{
			return false;
		}
		EnsureResolved();
		if (def != keyzUrgentDef)
		{
			return def == allowToolUrgentDef;
		}
		return true;
	}

	internal static void CollectUrgentNear(Map map, IntVec3 anchor, float radiusSq, List<Thing> into)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		into.Clear();
		if (map != null)
		{
			EnsureResolved();
			AddUrgentOfDef(map, anchor, radiusSq, keyzUrgentDef, into);
			AddUrgentOfDef(map, anchor, radiusSq, allowToolUrgentDef, into);
		}
	}

	private static void AddUrgentOfDef(Map map, IntVec3 anchor, float radiusSq, DesignationDef def, List<Thing> into)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		if (def == null)
		{
			return;
		}
		foreach (Designation item in map.designationManager.SpawnedDesignationsOfDef(def))
		{
			if (!((LocalTargetInfo)(ref item.target)).HasThing)
			{
				continue;
			}
			Thing thing = ((LocalTargetInfo)(ref item.target)).Thing;
			if (thing != null && thing.Spawned)
			{
				IntVec3 val = thing.Position - anchor;
				if (!((float)((IntVec3)(ref val)).LengthHorizontalSquared > radiusSq))
				{
					into.Add(thing);
				}
			}
		}
	}
}
