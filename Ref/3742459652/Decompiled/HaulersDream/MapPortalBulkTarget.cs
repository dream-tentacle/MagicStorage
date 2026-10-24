using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HaulersDream;

public class MapPortalBulkTarget : IManagedLoadable
{
	private readonly MapPortal portal;

	private readonly Map map;

	public MapPortal Portal => portal;

	public bool HasMassCap => false;

	public bool HandlesAbstractDemands => true;

	public LoadableKind Kind => LoadableKind.Portal;

	private MapPortalBulkTarget(MapPortal portal, Map map)
	{
		this.portal = portal;
		this.map = map;
	}

	public static MapPortalBulkTarget TryCreate(MapPortal portal)
	{
		if (portal == null || !((Thing)portal).Spawned)
		{
			return null;
		}
		Map val = ((Thing)portal).Map;
		if (val == null)
		{
			return null;
		}
		return new MapPortalBulkTarget(portal, val);
	}

	public static int LedgerKey(MapPortal portal)
	{
		return -(((Thing)portal).thingIDNumber + 1);
	}

	public int GetUniqueLoadID()
	{
		return LedgerKey(portal);
	}

	public Map GetMap()
	{
		return map;
	}

	public Thing GetParentThing()
	{
		return (Thing)(object)portal;
	}

	public List<TransferableOneWay> GetTransferables()
	{
		List<TransferableOneWay> list = new List<TransferableOneWay>();
		List<TransferableOneWay> leftToLoad = portal.leftToLoad;
		if (leftToLoad == null)
		{
			return list;
		}
		for (int i = 0; i < leftToLoad.Count; i++)
		{
			TransferableOneWay val = leftToLoad[i];
			if (val != null && ((Transferable)val).HasAnyThing && ((Transferable)val).CountToTransfer > 0)
			{
				list.Add(val);
			}
		}
		return list;
	}

	public bool AnythingToLoad()
	{
		List<TransferableOneWay> leftToLoad = portal.leftToLoad;
		if (leftToLoad == null)
		{
			return false;
		}
		for (int i = 0; i < leftToLoad.Count; i++)
		{
			TransferableOneWay val = leftToLoad[i];
			if (val != null && ((Transferable)val).HasAnyThing && ((Transferable)val).CountToTransfer > 0)
			{
				return true;
			}
		}
		return false;
	}

	public ThingOwner GetInnerContainerFor(Thing depositTarget)
	{
		return portal.GetDirectlyHeldThings();
	}

	public float GetMassCapacity()
	{
		return float.MaxValue;
	}

	public float GetMassUsage()
	{
		return 0f;
	}
}
