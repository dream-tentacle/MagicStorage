using System.Collections.Generic;
using Verse;

namespace HaulersDream;

public sealed class RoutePlan
{
	public readonly List<Thing> stops = new List<Thing>();

	public float totalTicks;

	public float travelCells;

	public int selectedCount;

	public bool cappedByAmount;

	public bool cappedByDistance;

	public bool cappedByReach;

	public bool fogCaution;

	public IntVec3? storageCell;

	public bool Empty => stops.Count == 0;
}
