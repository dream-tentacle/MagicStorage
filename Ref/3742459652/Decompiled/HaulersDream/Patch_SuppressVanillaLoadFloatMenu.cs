using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(FloatMenuOptionProvider_WorkGivers), "GetWorkGiverOption")]
public static class Patch_SuppressVanillaLoadFloatMenu
{
	private static bool Prepare()
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return true;
		}
		if (!settings.enableBulkLoadTransporters)
		{
			return settings.enableBulkLoadPortal;
		}
		return true;
	}

	private static bool Prefix(Pawn pawn, WorkGiverDef workGiver, LocalTargetInfo target, ref FloatMenuOption __result)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || workGiver?.giverClass == null || pawn == null)
		{
			return true;
		}
		if (HaulOrderGate.Blocks(pawn))
		{
			return true;
		}
		Thing thing = ((LocalTargetInfo)(ref target)).Thing;
		if (thing == null)
		{
			return true;
		}
		if (settings.enableBulkLoadTransporters && workGiver.giverClass == typeof(WorkGiver_LoadTransporters))
		{
			if (pawn.mindState?.duty?.def == DutyDefOf.LoadAndEnterTransporters)
			{
				return true;
			}
			LoadTransportersAdapter loadTransportersAdapter = LoadTransportersAdapter.TryCreate(ThingCompUtility.TryGetComp<CompTransporter>(thing));
			if (loadTransportersAdapter != null && TransportLoad.WouldGiveBulkJobForMenu(pawn, loadTransportersAdapter))
			{
				__result = null;
				return false;
			}
			return true;
		}
		if (settings.enableBulkLoadPortal && workGiver.giverClass == typeof(WorkGiver_HaulToPortal))
		{
			if (pawn.mindState?.duty?.def == DutyDefOf.LoadAndEnterPortal)
			{
				return true;
			}
			MapPortalBulkTarget mapPortalBulkTarget = MapPortalBulkTarget.TryCreate((MapPortal)(object)((thing is MapPortal) ? thing : null));
			if (mapPortalBulkTarget != null && TransportLoad.WouldGiveBulkJobForMenu(pawn, mapPortalBulkTarget))
			{
				__result = null;
				return false;
			}
			return true;
		}
		return true;
	}
}
