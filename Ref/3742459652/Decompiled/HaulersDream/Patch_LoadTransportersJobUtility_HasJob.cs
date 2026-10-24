using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(LoadTransportersJobUtility), "HasJobOnTransporter")]
public static class Patch_LoadTransportersJobUtility_HasJob
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadTransporters ?? true;
	}

	private static bool Prefix(Pawn pawn, CompTransporter transporter, ref bool __result)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.enableBulkLoadTransporters)
		{
			return true;
		}
		LoadTransportersAdapter loadTransportersAdapter = LoadTransportersAdapter.TryCreate(transporter);
		if (loadTransportersAdapter == null)
		{
			return true;
		}
		if (!TransportLoad.HasPotentialBulkWork(pawn, loadTransportersAdapter))
		{
			HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
			if (instance != null && instance.LoadFullyClaimedByOthers(loadTransportersAdapter))
			{
				__result = false;
				return false;
			}
			return true;
		}
		if (TransportLoad.TryGiveBulkJob(pawn, loadTransportersAdapter) == null)
		{
			return true;
		}
		__result = true;
		return false;
	}
}
