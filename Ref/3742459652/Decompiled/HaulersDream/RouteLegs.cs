using System.Collections.Generic;
using Verse;

namespace HaulersDream;

public sealed class RouteLegs
{
	public Pawn pawn;

	public RouteWorkKind kind;

	public readonly List<Thing> gatherOrdered = new List<Thing>();

	public readonly List<float> legWalkTicks = new List<float>();

	public float[] prefixRouteCost;

	public int forcedCount = 1;

	public IntVec3? storage;

	public int selectedCount;

	public bool cappedByAmount;

	public bool fogCaution;

	public int Reachable => gatherOrdered.Count;
}
