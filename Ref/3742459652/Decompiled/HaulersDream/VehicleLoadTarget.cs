using System.Collections;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HaulersDream;

public class VehicleLoadTarget : IManagedLoadable
{
	private readonly Thing vehicle;

	private readonly Map map;

	public Thing Vehicle => vehicle;

	public bool HasMassCap => true;

	public bool HandlesAbstractDemands => false;

	public LoadableKind Kind => LoadableKind.Vehicle;

	private VehicleLoadTarget(Thing vehicle, Map map)
	{
		this.vehicle = vehicle;
		this.map = map;
	}

	public static VehicleLoadTarget TryCreate(Thing vehicle)
	{
		if (vehicle == null || !VehicleFrameworkCompat.IsVehicle(vehicle) || !vehicle.Spawned)
		{
			return null;
		}
		Map val = vehicle.Map;
		if (val == null)
		{
			return null;
		}
		return new VehicleLoadTarget(vehicle, val);
	}

	public int GetUniqueLoadID()
	{
		return vehicle.thingIDNumber;
	}

	public Map GetMap()
	{
		return map;
	}

	public Thing GetParentThing()
	{
		return vehicle;
	}

	public List<TransferableOneWay> GetTransferables()
	{
		List<TransferableOneWay> list = new List<TransferableOneWay>();
		IList list2 = VehicleFrameworkCompat.CargoToLoad(vehicle);
		if (list2 == null)
		{
			return list;
		}
		for (int i = 0; i < list2.Count; i++)
		{
			object obj = list2[i];
			TransferableOneWay val = (TransferableOneWay)((obj is TransferableOneWay) ? obj : null);
			if (val != null && ((Transferable)val).HasAnyThing && ((Transferable)val).CountToTransfer > 0)
			{
				list.Add(val);
			}
		}
		return list;
	}

	public bool AnythingToLoad()
	{
		IList list = VehicleFrameworkCompat.CargoToLoad(vehicle);
		if (list == null)
		{
			return false;
		}
		for (int i = 0; i < list.Count; i++)
		{
			object obj = list[i];
			TransferableOneWay val = (TransferableOneWay)((obj is TransferableOneWay) ? obj : null);
			if (val != null && ((Transferable)val).HasAnyThing && ((Transferable)val).CountToTransfer > 0)
			{
				return true;
			}
		}
		return false;
	}

	public ThingOwner GetInnerContainerFor(Thing depositTarget)
	{
		Thing obj = vehicle;
		return (ThingOwner)(object)((Pawn)(((obj is Pawn) ? obj : null)?)).inventory?.innerContainer;
	}

	public float GetMassCapacity()
	{
		return VehicleFrameworkCompat.CargoCapacity(vehicle);
	}

	public float GetMassUsage()
	{
		Thing obj = vehicle;
		Pawn val = (Pawn)(object)((obj is Pawn) ? obj : null);
		if (val == null)
		{
			return 0f;
		}
		return MassUtility.GearAndInventoryMass(val);
	}
}
