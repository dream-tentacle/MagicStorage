using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HaulersDream;

public interface IManagedLoadable
{
	LoadableKind Kind { get; }

	bool HasMassCap { get; }

	bool HandlesAbstractDemands { get; }

	int GetUniqueLoadID();

	Map GetMap();

	Thing GetParentThing();

	List<TransferableOneWay> GetTransferables();

	bool AnythingToLoad();

	ThingOwner GetInnerContainerFor(Thing depositTarget);

	float GetMassCapacity();

	float GetMassUsage();
}
