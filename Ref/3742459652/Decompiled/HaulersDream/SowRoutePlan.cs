using System.Collections.Generic;
using Verse;

namespace HaulersDream;

public sealed class SowRoutePlan
{
	public readonly List<IntVec3> cells = new List<IntVec3>();

	public float totalTicks;

	public float travelCells;

	public int selectedCount;

	public bool cappedByAmount;

	public bool cappedByDistance;

	public IntVec3? storageCell;

	public ThingDef plantDef;

	public bool Empty => cells.Count == 0;
}
