using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class VehicleFrameworkCompat
{
	private static bool initialized;

	private static bool active;

	private static Type vehiclePawnType;

	private static Type vehicleDefType;

	private static Type vehicleCargoTabType;

	private static MethodInfo inVehicleThing;

	private static Func<Thing, bool> inVehicleDelegate;

	private static MethodInfo addOrTransfer;

	private static FieldInfo cargoToLoadField;

	private static MethodInfo getStatValue;

	private static FieldInfo cargoCapacityField;

	private static object cargoCapacityStatDef;

	private static Type vrmType;

	private static MethodInfo reserveVehicleGeneric;

	private static MethodInfo releaseAllClaimedBy;

	public static bool IsActive
	{
		get
		{
			if (!initialized)
			{
				Init();
			}
			return active;
		}
	}

	public static Type VehicleCargoTabType
	{
		get
		{
			if (!IsActive)
			{
				return null;
			}
			return vehicleCargoTabType;
		}
	}

	private static void Init()
	{
		initialized = true;
		active = false;
		vehiclePawnType = AccessTools.TypeByName("Vehicles.VehiclePawn");
		if (vehiclePawnType == null)
		{
			return;
		}
		vehicleDefType = AccessTools.TypeByName("Vehicles.VehicleDef");
		vehicleCargoTabType = AccessTools.TypeByName("Vehicles.ITab_Vehicle_Cargo");
		Type type = AccessTools.TypeByName("Vehicles.Ext_Vehicles");
		if (type != null)
		{
			inVehicleThing = AccessTools.Method(type, "InVehicle", new Type[1] { typeof(Thing) }, (Type[])null);
			if (inVehicleThing != null)
			{
				try
				{
					inVehicleDelegate = (Func<Thing, bool>)Delegate.CreateDelegate(typeof(Func<Thing, bool>), inVehicleThing);
				}
				catch (Exception)
				{
					inVehicleDelegate = null;
				}
			}
		}
		addOrTransfer = AccessTools.Method(vehiclePawnType, "AddOrTransfer", new Type[2]
		{
			typeof(Thing),
			typeof(int)
		}, (Type[])null);
		cargoToLoadField = AccessTools.Field(vehiclePawnType, "cargoToLoad");
		Type type2 = AccessTools.TypeByName("Vehicles.VehicleStatDef");
		if (type2 != null)
		{
			getStatValue = AccessTools.Method(vehiclePawnType, "GetStatValue", new Type[1] { type2 }, (Type[])null);
			Type type3 = AccessTools.TypeByName("Vehicles.VehicleStatDefOf");
			cargoCapacityField = ((type3 != null) ? AccessTools.Field(type3, "CargoCapacity") : null);
		}
		vrmType = AccessTools.TypeByName("Vehicles.VehicleReservationManager");
		Type type4 = AccessTools.TypeByName("Vehicles.VehicleTargetReservation");
		if (vrmType != null)
		{
			releaseAllClaimedBy = AccessTools.Method(vrmType, "ReleaseAllClaimedBy", new Type[1] { typeof(Pawn) }, (Type[])null);
			if (type4 != null)
			{
				MethodInfo methodInfo = AccessTools.Method(vrmType, "Reserve", (Type[])null, (Type[])null);
				if (methodInfo != null && methodInfo.IsGenericMethodDefinition)
				{
					reserveVehicleGeneric = methodInfo.MakeGenericMethod(typeof(LocalTargetInfo), type4);
				}
			}
		}
		active = addOrTransfer != null && getStatValue != null && cargoCapacityField != null && cargoToLoadField != null;
		if (active)
		{
			HDLog.Msg("Vehicle Framework detected — bulk-load-into-vehicle + event-correct pack-animal deposit on, eat-from / build-from a parked vehicle's cargo enabled (auto-open cargo tab: " + ((vehicleCargoTabType != null) ? "on" : "unavailable — Vehicles.ITab_Vehicle_Cargo did not resolve") + ").");
			return;
		}
		HDLog.Warn("Vehicle Framework present but a load-bearing member did not resolve (VehiclePawn.AddOrTransfer(Thing,int)=" + (addOrTransfer != null) + ", VehiclePawn.GetStatValue(VehicleStatDef)=" + (getStatValue != null) + ", VehicleStatDefOf.CargoCapacity=" + (cargoCapacityField != null) + ", VehiclePawn.cargoToLoad=" + (cargoToLoadField != null) + "); vehicle bulk-load / cargo integration is OFF (HD behaves as without VF).");
	}

	public static bool IsVehicle(Thing t)
	{
		if (!IsActive || t == null)
		{
			return false;
		}
		return vehiclePawnType.IsInstanceOfType(t);
	}

	public static bool IsVehicleDef(ThingDef d)
	{
		if (!IsActive || d == null || vehicleDefType == null)
		{
			return false;
		}
		return vehicleDefType.IsInstanceOfType(d);
	}

	public static bool InVehicle(Thing t)
	{
		if (!IsActive || t == null || inVehicleThing == null)
		{
			return false;
		}
		return inVehicleDelegate?.Invoke(t) ?? ((bool)inVehicleThing.Invoke(null, new object[1] { t }));
	}

	public static int AddOrTransfer(Thing vehicle, Thing item, int count)
	{
		if (!IsActive || addOrTransfer == null || vehicle == null || item == null || count <= 0)
		{
			return -1;
		}
		return (int)addOrTransfer.Invoke(vehicle, new object[2] { item, count });
	}

	public static IList CargoToLoad(Thing vehicle)
	{
		if (!IsActive || cargoToLoadField == null || vehicle == null)
		{
			return null;
		}
		return cargoToLoadField.GetValue(vehicle) as IList;
	}

	public static int RemainingDemandForThing(Thing vehicle, Thing item)
	{
		if (!IsActive || vehicle == null || item == null)
		{
			return 0;
		}
		IList list = CargoToLoad(vehicle);
		if (list == null || list.Count == 0)
		{
			return 0;
		}
		List<TransferableOneWay> list2 = new List<TransferableOneWay>(list.Count);
		for (int i = 0; i < list.Count; i++)
		{
			object obj = list[i];
			TransferableOneWay val = (TransferableOneWay)((obj is TransferableOneWay) ? obj : null);
			if (val != null)
			{
				list2.Add(val);
			}
		}
		TransferableOneWay obj2 = TransferableUtility.TransferableMatchingDesperate(item, list2, (TransferAsOneMode)1);
		if (obj2 == null)
		{
			return 0;
		}
		return ((Transferable)obj2).CountToTransfer;
	}

	public static float CargoCapacity(Thing vehicle)
	{
		if (!IsActive || getStatValue == null || vehicle == null)
		{
			return 0f;
		}
		if (cargoCapacityStatDef == null)
		{
			cargoCapacityStatDef = cargoCapacityField?.GetValue(null);
		}
		if (cargoCapacityStatDef == null)
		{
			return 0f;
		}
		return (float)getStatValue.Invoke(vehicle, new object[1] { cargoCapacityStatDef });
	}

	public static bool ReserveVehicle(Thing vehicle, Pawn pawn, Job job)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		if (!IsActive || reserveVehicleGeneric == null || vrmType == null || vehicle == null || pawn == null || job == null)
		{
			return true;
		}
		Map mapHeld = vehicle.MapHeld;
		if (mapHeld == null)
		{
			return true;
		}
		MapComponent component = mapHeld.GetComponent(vrmType);
		if (component == null)
		{
			return true;
		}
		try
		{
			return (bool)reserveVehicleGeneric.Invoke(component, new object[4]
			{
				vehicle,
				pawn,
				job,
				LocalTargetInfo.op_Implicit(vehicle)
			});
		}
		catch (Exception ex)
		{
			HDLog.WarnOnce("VehicleFramework VRM.ReserveVehicle reflection threw; falling back to the authoritative WorkGiver-suppress stand-down (claim treated as held). Report if vehicle loading misbehaves:\n" + ex, 1212437078);
			return true;
		}
	}

	public static void ReleaseVehicleClaims(Pawn pawn)
	{
		if (!IsActive || releaseAllClaimedBy == null || vrmType == null || pawn == null)
		{
			return;
		}
		Map mapHeld = ((Thing)pawn).MapHeld;
		if (mapHeld != null)
		{
			MapComponent component = mapHeld.GetComponent(vrmType);
			if (component != null)
			{
				releaseAllClaimedBy.Invoke(component, new object[1] { pawn });
			}
		}
	}
}
