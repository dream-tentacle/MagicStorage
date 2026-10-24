using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HaulersDream;

[StaticConstructorOnStartup]
public class MapComponent_RoutePreview : MapComponent
{
	private Pawn pawn;

	private List<IntVec3> stops;

	private IntVec3? storageCell;

	private IntVec3? circleCenter;

	private float circleRadius;

	private static Material purpleLineMat;

	private static Material returnLineMat;

	private static readonly Color PurpleRing = new Color(0.72f, 0.3f, 1f, 0.65f);

	private const float LineWidth = 0.45f;

	private const float ReturnLineWidth = 0.38f;

	private static Material PurpleLineMat
	{
		get
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)purpleLineMat == (Object)null)
			{
				purpleLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(0.72f, 0.22f, 1f, 0.9f));
			}
			return purpleLineMat;
		}
	}

	private static Material ReturnLineMat
	{
		get
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)returnLineMat == (Object)null)
			{
				returnLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(0.25f, 0.85f, 1f, 0.7f));
			}
			return returnLineMat;
		}
	}

	public MapComponent_RoutePreview(Map map)
		: base(map)
	{
	}

	public void SetPreview(Pawn p, List<IntVec3> stopCells, IntVec3? ringCenter = null, float ringRadius = 0f, IntVec3? storage = null)
	{
		pawn = p;
		stops = stopCells;
		storageCell = storage;
		circleCenter = ringCenter;
		circleRadius = ringRadius;
	}

	public void ClearPreview()
	{
		pawn = null;
		stops = null;
		storageCell = null;
		circleCenter = null;
		circleRadius = 0f;
	}

	public override void MapComponentUpdate()
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		if (base.map != Find.CurrentMap)
		{
			return;
		}
		if (circleCenter.HasValue && circleRadius > 0f)
		{
			GenDraw.DrawRadiusRing(circleCenter.Value, circleRadius, PurpleRing, (Func<IntVec3, bool>)null);
		}
		List<IntVec3> list = stops;
		Pawn val = pawn;
		if (list != null && list.Count != 0 && val != null && ((Thing)val).Spawned && ((Thing)val).Map == base.map)
		{
			float y = Altitudes.AltitudeFor((AltitudeLayer)39);
			Material val2 = PurpleLineMat;
			Vector3 val3 = ((Thing)val).DrawPos;
			val3.y = y;
			IntVec3 val4;
			for (int i = 0; i < list.Count; i++)
			{
				val4 = list[i];
				Vector3 val5 = ((IntVec3)(ref val4)).ToVector3Shifted();
				val5.y = y;
				GenDraw.DrawLineBetween(val3, val5, val2, 0.45f);
				val3 = val5;
			}
			if (storageCell.HasValue)
			{
				val4 = storageCell.Value;
				Vector3 val6 = ((IntVec3)(ref val4)).ToVector3Shifted();
				val6.y = y;
				GenDraw.DrawLineBetween(val3, val6, ReturnLineMat, 0.38f);
			}
		}
	}

	public override void MapRemoved()
	{
		ClearPreview();
	}
}
