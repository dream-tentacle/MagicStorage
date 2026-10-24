using System.Collections.Generic;
using Verse;

namespace HaulersDream;

public sealed class RemoveFloorRoutePlan
{
	public readonly List<IntVec3> cells = new List<IntVec3>();

	public float totalTicks;

	public float travelCells;

	public int selectedCount;

	public bool cappedByAmount;

	public bool cappedByDistance;

	public bool Empty => cells.Count == 0;
}
