using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ResourceDeliverJobFor")]
[HarmonyPriority(400)]
public static class Patch_ResourceDeliverJobFor_Inventory
{
	private static void Postfix(ref Job __result, Pawn pawn, IConstructible c, bool forced)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Invalid comparison between Unknown and I4
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.inventoryConstructDeliver || __result == null || ((pawn != null) ? ((Thing)pawn).Map : null) == null || __result.def != JobDefOf.HaulToContainer || (int)__result.haulMode != 3 || c is Blueprint_Install)
		{
			return;
		}
		Thing thing = ((LocalTargetInfo)(ref __result.targetA)).Thing;
		if (thing != null && thing.Spawned && !(thing.ParentHolder is Pawn_InventoryTracker))
		{
			ConstructRouteIntent routeIntent = InventoryConstructDelivery.RouteIntent;
			bool tetherBuild = forced && routeIntent != ConstructRouteIntent.HaulOnly && (routeIntent == ConstructRouteIntent.HaulBuild || HaulersDreamMod.Settings == null || HaulersDreamMod.Settings.orderedConstructTether);
			Job val = InventoryConstructDelivery.TryBuild(pawn, c, thing, forced, tetherBuild, __result);
			if (val != null)
			{
				__result = val;
			}
		}
	}
}
