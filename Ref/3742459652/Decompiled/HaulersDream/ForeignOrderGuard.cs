using System.Collections.Generic;
using Verse;

namespace HaulersDream;

internal static class ForeignOrderGuard
{
	private static bool haulDefsResolved;

	private static DesignationDef haulDef;

	internal static bool ClaimedByForeignOrder(Thing thing)
	{
		Map val = ((thing != null) ? thing.Map : null);
		if (val == null)
		{
			return false;
		}
		DesignationManager designationManager = val.designationManager;
		if (!designationManager.HasMapDesignationOn(thing))
		{
			return false;
		}
		EnsureHaulDefsResolved();
		List<Designation> list = designationManager.AllDesignationsOn(thing);
		for (int i = 0; i < list.Count; i++)
		{
			DesignationDef def = list[i].def;
			if (def != null && def != haulDef && !UrgentHaulCompat.IsUrgentDesignationDef(def))
			{
				return true;
			}
		}
		return false;
	}

	private static void EnsureHaulDefsResolved()
	{
		if (!haulDefsResolved)
		{
			haulDefsResolved = true;
			haulDef = DefDatabase<DesignationDef>.GetNamedSilentFail("Haul");
		}
	}
}
