using System.Collections.Generic;
using Verse;

namespace HaulersDream;

public class VeinRevealTracker : IExposable
{
	public Pawn pawn;

	public ThingDef veinDef;

	public int mapId = -1;

	public IntVec3 seed;

	public int cap;

	public IntVec3 lastCell;

	public HashSet<IntVec3> included = new HashSet<IntVec3>();

	public void ExposeData()
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Invalid comparison between Unknown and I4
		Scribe_References.Look<Pawn>(ref pawn, "pawn", false);
		Scribe_Defs.Look<ThingDef>(ref veinDef, "veinDef");
		Scribe_Values.Look<int>(ref mapId, "mapId", -1, false);
		Scribe_Values.Look<IntVec3>(ref seed, "seed", default(IntVec3), false);
		Scribe_Values.Look<int>(ref cap, "cap", 0, false);
		Scribe_Values.Look<IntVec3>(ref lastCell, "lastCell", default(IntVec3), false);
		Scribe_Collections.Look<IntVec3>(ref included, "included", (LookMode)1);
		if ((int)Scribe.mode == 2 && included == null)
		{
			included = new HashSet<IntVec3>();
		}
	}
}
