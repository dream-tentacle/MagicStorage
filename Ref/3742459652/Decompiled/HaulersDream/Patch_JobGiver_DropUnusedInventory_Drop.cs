using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_DropUnusedInventory), "Drop")]
public static class Patch_JobGiver_DropUnusedInventory_Drop
{
	private static bool Prefix(Pawn pawn, Thing thing)
	{
		CompHauledToInventory compHauledToInventory = ((pawn != null) ? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() : null);
		if (compHauledToInventory == null || thing == null)
		{
			return true;
		}
		if (!compHauledToInventory.GetHashSet().Contains(thing))
		{
			return !compHauledToInventory.IsKeptDef(thing.def);
		}
		return false;
	}
}
