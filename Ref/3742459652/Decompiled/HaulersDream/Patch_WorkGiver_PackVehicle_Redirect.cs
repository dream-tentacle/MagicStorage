using System;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch]
public static class Patch_WorkGiver_PackVehicle_Redirect
{
	private static readonly Type PackVehicleType = AccessTools.TypeByName("Vehicles.WorkGiver_PackVehicle");

	private static readonly MethodInfo TargetJobOnThing = AccessTools.Method(PackVehicleType, "JobOnThing", new Type[3]
	{
		typeof(Pawn),
		typeof(Thing),
		typeof(bool)
	}, (Type[])null);

	private static bool Prepare()
	{
		if (TargetJobOnThing == null)
		{
			return false;
		}
		if (!VehicleFrameworkCompat.IsActive)
		{
			return false;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && settings.enableVehicleFramework)
		{
			return settings.enableBulkLoadVehicles;
		}
		return false;
	}

	private static MethodBase TargetMethod()
	{
		return HaulersDreamMod.NormalizeToDeclared(TargetJobOnThing);
	}

	private static void Postfix(object __instance, Pawn pawn, Thing t, ref Job __result)
	{
		if (__result == null || PackVehicleType == null || !PackVehicleType.IsInstanceOfType(__instance))
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.enableVehicleFramework || !settings.enableBulkLoadVehicles || !VehicleFrameworkCompat.IsActive || !VehicleFrameworkCompat.IsVehicle(t))
		{
			return;
		}
		VehicleLoadTarget vehicleLoadTarget = VehicleLoadTarget.TryCreate(t);
		if (vehicleLoadTarget == null)
		{
			return;
		}
		if (!TransportLoad.HasPotentialBulkWork(pawn, vehicleLoadTarget))
		{
			HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
			if (instance != null && instance.LoadFullyClaimedByOthers(vehicleLoadTarget))
			{
				__result = null;
			}
		}
		else
		{
			Job val = TransportLoad.TryGiveBulkJob(pawn, vehicleLoadTarget);
			if (val != null)
			{
				__result = val;
			}
		}
	}
}
