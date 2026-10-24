using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ResourceDeliverJobFor")]
[HarmonyPriority(200)]
public static class Patch_ResourceDeliverJobFor_Batch
{
	private static void Postfix(ref Job __result, Pawn pawn, IConstructible c, bool forced)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Invalid comparison between Unknown and I4
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && settings.batchWorkDeliveries && __result != null && ((pawn != null) ? ((Thing)pawn).Map : null) != null && !forced && __result.def == JobDefOf.HaulToContainer && (int)__result.haulMode == 3 && !(c is Blueprint_Install))
		{
			Thing thing = ((LocalTargetInfo)(ref __result.targetA)).Thing;
			if (thing != null && thing.Spawned && !(thing.ParentHolder is Pawn_InventoryTracker))
			{
				ConstructionBatch.Expand(pawn, __result, thing.def, forced);
			}
		}
	}
}
