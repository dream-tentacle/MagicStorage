using HarmonyLib;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(Pawn), "DeSpawn")]
public static class Patch_Pawn_DeSpawn_ReleaseClaim
{
	private static bool Prepare()
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return true;
		}
		if (!settings.enableBulkLoadTransporters && !settings.enableBulkLoadPortal)
		{
			if (settings.enableVehicleFramework)
			{
				return settings.enableBulkLoadVehicles;
			}
			return false;
		}
		return true;
	}

	private static void Prefix(Pawn __instance)
	{
		HaulersDreamGameComponent.Instance?.LoadReleaseClaimsForPawn(__instance);
	}
}
