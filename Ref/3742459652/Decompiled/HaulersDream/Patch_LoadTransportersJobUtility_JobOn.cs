using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(LoadTransportersJobUtility), "JobOnTransporter")]
public static class Patch_LoadTransportersJobUtility_JobOn
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadTransporters ?? true;
	}

	private static bool Prefix(Pawn p, CompTransporter transporter, ref Job __result)
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
		Job val = TransportLoad.TryGiveBulkJob(p, loadTransportersAdapter);
		if (val == null)
		{
			HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
			if (instance != null && instance.LoadFullyClaimedByOthers(loadTransportersAdapter))
			{
				return false;
			}
			return true;
		}
		__result = val;
		return false;
	}
}
