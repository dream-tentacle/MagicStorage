using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

[StaticConstructorOnStartup]
public class DetourOverlay : MapComponent
{
	private static readonly Color EnRouteColor = new Color(0.3f, 1f, 0.45f, 0.85f);

	private static readonly Color RoutingColor = new Color(0.25f, 0.85f, 1f, 0.85f);

	private const float LineWidth = 0.4f;

	private static Material enRouteMat;

	private static Material routingMat;

	private static Material EnRouteMat
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)enRouteMat == (Object)null)
			{
				enRouteMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, EnRouteColor);
			}
			return enRouteMat;
		}
	}

	private static Material RoutingMat
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)routingMat == (Object)null)
			{
				routingMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, RoutingColor);
			}
			return routingMat;
		}
	}

	public DetourOverlay(Map map)
		: base(map)
	{
	}

	public override void MapComponentUpdate()
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.drawDetourLines || !Prefs.DevMode || base.map != Find.CurrentMap)
		{
			return;
		}
		float y = Altitudes.AltitudeFor((AltitudeLayer)39);
		MapPawns mapPawns = base.map.mapPawns;
		IReadOnlyList<Pawn> readOnlyList = ((mapPawns != null) ? mapPawns.AllPawnsSpawned : null);
		if (readOnlyList == null)
		{
			return;
		}
		for (int i = 0; i < readOnlyList.Count; i++)
		{
			Pawn val = readOnlyList[i];
			if (val != null && ((Thing)val).Spawned && ((Thing)val).Faction == Faction.OfPlayerSilentFail)
			{
				Job curJob = val.CurJob;
				if (curJob?.def != null)
				{
					DrawForJob(val, curJob, y);
				}
			}
		}
	}

	private void DrawForJob(Pawn pawn, Job job, float y)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 val2;
		if (job.def == JobDefOf.HaulToCell)
		{
			StorageRouting.RelocationInfo relocationInfo = StorageRouting.RelocationData(job);
			if (relocationInfo != null)
			{
				Vector3 drawPos = ((Thing)pawn).DrawPos;
				drawPos.y = y;
				Vector3 val = CellOrThing(job.targetA, pawn);
				val.y = y;
				val2 = ((LocalTargetInfo)(ref job.targetB)).Cell;
				Vector3 val3 = ((IntVec3)(ref val2)).ToVector3Shifted();
				val3.y = y;
				val2 = relocationInfo.consumeCell;
				Vector3 val4 = ((IntVec3)(ref val2)).ToVector3Shifted();
				val4.y = y;
				Material val5 = RoutingMat;
				GenDraw.DrawLineBetween(drawPos, val, val5, 0.4f);
				GenDraw.DrawLineBetween(val, val3, val5, 0.4f);
				val2 = relocationInfo.consumeCell;
				if (((IntVec3)(ref val2)).IsValid)
				{
					GenDraw.DrawLineBetween(val3, val4, val5, 0.4f);
				}
			}
		}
		else
		{
			if (job.def != HaulersDreamDefOf.HaulersDream_BulkHaul)
			{
				return;
			}
			Patch_Pawn_JobTracker_EnRoutePickup.EnRouteInfo enRouteInfo = Patch_Pawn_JobTracker_EnRoutePickup.EnRouteData(job);
			if (enRouteInfo != null)
			{
				Vector3 drawPos2 = ((Thing)pawn).DrawPos;
				drawPos2.y = y;
				Vector3 val6 = PickupCell(job, pawn);
				val6.y = y;
				val2 = enRouteInfo.jobCell;
				Vector3 val7 = ((IntVec3)(ref val2)).ToVector3Shifted();
				val7.y = y;
				Material val8 = EnRouteMat;
				GenDraw.DrawLineBetween(drawPos2, val6, val8, 0.4f);
				val2 = enRouteInfo.jobCell;
				if (((IntVec3)(ref val2)).IsValid)
				{
					GenDraw.DrawLineBetween(val6, val7, val8, 0.4f);
				}
			}
		}
	}

	private static Vector3 PickupCell(Job job, Pawn pawn)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		if (((LocalTargetInfo)(ref job.targetA)).IsValid)
		{
			return CellOrThing(job.targetA, pawn);
		}
		List<LocalTargetInfo> targetQueueB = job.targetQueueB;
		if (targetQueueB != null)
		{
			for (int i = 0; i < targetQueueB.Count; i++)
			{
				LocalTargetInfo val = targetQueueB[i];
				if (((LocalTargetInfo)(ref val)).IsValid)
				{
					return CellOrThing(targetQueueB[i], pawn);
				}
			}
		}
		return ((Thing)pawn).DrawPos;
	}

	private static Vector3 CellOrThing(LocalTargetInfo t, Pawn pawn)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (((LocalTargetInfo)(ref t)).HasThing && ((LocalTargetInfo)(ref t)).Thing != null && ((LocalTargetInfo)(ref t)).Thing.Spawned)
		{
			return ((LocalTargetInfo)(ref t)).Thing.DrawPos;
		}
		IntVec3 cell = ((LocalTargetInfo)(ref t)).Cell;
		if (((IntVec3)(ref cell)).IsValid)
		{
			cell = ((LocalTargetInfo)(ref t)).Cell;
			return ((IntVec3)(ref cell)).ToVector3Shifted();
		}
		return ((Thing)pawn).DrawPos;
	}
}
