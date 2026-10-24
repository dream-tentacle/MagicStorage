using HaulersDream.Core;
using Verse;

namespace HaulersDream;

public class RemoveFloorRouteTemplate : IExposable
{
	public RemoveFloorRouteMode mode;

	public int maxTravel = 100;

	public int radius = 8;

	public int amount = -1;

	public RouteSelectionMethod selectionMethod;

	public void ExposeData()
	{
		Scribe_Values.Look<RemoveFloorRouteMode>(ref mode, "mode", RemoveFloorRouteMode.Area, false);
		Scribe_Values.Look<int>(ref maxTravel, "maxTravel", 100, false);
		Scribe_Values.Look<int>(ref radius, "radius", 8, false);
		Scribe_Values.Look<int>(ref amount, "amount", -1, false);
		Scribe_Values.Look<RouteSelectionMethod>(ref selectionMethod, "selectionMethod", RouteSelectionMethod.MostStopsPerTravel, false);
	}

	public bool ValueEquals(RemoveFloorRouteTemplate o)
	{
		if (o != null && mode == o.mode && maxTravel == o.maxTravel && radius == o.radius && amount == o.amount)
		{
			return selectionMethod == o.selectionMethod;
		}
		return false;
	}
}
