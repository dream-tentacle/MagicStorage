using HaulersDream.Core;
using Verse;

namespace HaulersDream;

public class SowRouteTemplate : IExposable
{
	public SowRouteMode mode;

	public int maxTravel = 100;

	public int radius = 8;

	public int amount = -1;

	public bool smart = true;

	public RouteSelectionMethod selectionMethod;

	public void ExposeData()
	{
		Scribe_Values.Look<SowRouteMode>(ref mode, "mode", SowRouteMode.Zone, false);
		Scribe_Values.Look<int>(ref maxTravel, "maxTravel", 100, false);
		Scribe_Values.Look<int>(ref radius, "radius", 8, false);
		Scribe_Values.Look<int>(ref amount, "amount", -1, false);
		Scribe_Values.Look<bool>(ref smart, "smart", true, false);
		Scribe_Values.Look<RouteSelectionMethod>(ref selectionMethod, "selectionMethod", RouteSelectionMethod.MostStopsPerTravel, false);
	}

	public bool ValueEquals(SowRouteTemplate o)
	{
		if (o != null && mode == o.mode && maxTravel == o.maxTravel && radius == o.radius && amount == o.amount && smart == o.smart)
		{
			return selectionMethod == o.selectionMethod;
		}
		return false;
	}
}
