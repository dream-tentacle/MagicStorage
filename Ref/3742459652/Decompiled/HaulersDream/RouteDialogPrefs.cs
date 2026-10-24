using HaulersDream.Core;
using Verse;

namespace HaulersDream;

public class RouteDialogPrefs : IExposable
{
	public RouteMode mode = RouteMode.Chained;

	public int maxTravel = 100;

	public int radius = 8;

	public int amount = -1;

	public bool smart = true;

	public bool allowHarvest = true;

	public int growthThreshold = 80;

	public RouteSelectionMethod selectionMethod;

	public RouteDistanceBasis distanceBasis;

	public void ExposeData()
	{
		Scribe_Values.Look<RouteMode>(ref mode, "mode", RouteMode.Chained, false);
		Scribe_Values.Look<int>(ref maxTravel, "maxTravel", 100, false);
		Scribe_Values.Look<int>(ref radius, "radius", 8, false);
		Scribe_Values.Look<int>(ref amount, "amount", -1, false);
		Scribe_Values.Look<bool>(ref smart, "smart", true, false);
		Scribe_Values.Look<bool>(ref allowHarvest, "allowHarvest", true, false);
		Scribe_Values.Look<int>(ref growthThreshold, "growthThreshold", 80, false);
		Scribe_Values.Look<RouteSelectionMethod>(ref selectionMethod, "selectionMethod", RouteSelectionMethod.MostStopsPerTravel, false);
		Scribe_Values.Look<RouteDistanceBasis>(ref distanceBasis, "distanceBasis", RouteDistanceBasis.StraightLine, false);
	}

	public bool ValueEquals(RouteDialogPrefs o)
	{
		if (o != null && mode == o.mode && maxTravel == o.maxTravel && radius == o.radius && amount == o.amount && smart == o.smart && allowHarvest == o.allowHarvest && growthThreshold == o.growthThreshold && selectionMethod == o.selectionMethod)
		{
			return distanceBasis == o.distanceBasis;
		}
		return false;
	}
}
