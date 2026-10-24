using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class BulkLoadAntiConflict
{
	private static readonly List<CompTransporter> tmpTransporters = new List<CompTransporter>();

	internal static bool GroupHasGoodsLeftToLoad(Map map, int groupID)
	{
		if (map == null || groupID < 0)
		{
			return false;
		}
		TransporterUtility.GetTransportersInGroup(groupID, map, tmpTransporters);
		bool flag = false;
		for (int i = 0; i < tmpTransporters.Count; i++)
		{
			if (flag)
			{
				break;
			}
			flag = HasGoodsLeftToLoad(tmpTransporters[i]?.leftToLoad);
		}
		tmpTransporters.Clear();
		return flag;
	}

	internal static bool HasGoodsLeftToLoad(List<TransferableOneWay> leftToLoad)
	{
		if (leftToLoad == null)
		{
			return false;
		}
		for (int i = 0; i < leftToLoad.Count; i++)
		{
			TransferableOneWay val = leftToLoad[i];
			if (val != null && ((Transferable)val).CountToTransfer > 0 && !(((Transferable)val).AnyThing is Pawn))
			{
				return true;
			}
		}
		return false;
	}

	internal static bool AnyHdLoaderForGroup(CompTransporter transporter)
	{
		object obj;
		if (transporter == null)
		{
			obj = null;
		}
		else
		{
			ThingWithComps parent = ((ThingComp)transporter).parent;
			obj = ((parent != null) ? ((Thing)parent).Map : null);
		}
		if (obj == null)
		{
			return false;
		}
		return AnyHdLoaderForGroupId(((Thing)((ThingComp)transporter).parent).Map, transporter.groupID);
	}

	internal static bool AnyHdLoaderForGroupId(Map map, int groupID)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		if (map?.mapPawns == null || groupID < 0)
		{
			return false;
		}
		IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
		for (int i = 0; i < allPawnsSpawned.Count; i++)
		{
			Pawn val = allPawnsSpawned[i];
			if (((val != null) ? val.CurJob : null) != null && val.CurJobDef == HaulersDreamDefOf.HaulersDream_LoadTransportersInBulk)
			{
				LocalTargetInfo target = val.CurJob.GetTarget((TargetIndex)1);
				Thing thing = ((LocalTargetInfo)(ref target)).Thing;
				CompTransporter val2 = ((thing != null) ? ThingCompUtility.TryGetComp<CompTransporter>(thing) : null);
				if (val2 != null && val2.groupID == groupID)
				{
					return true;
				}
			}
		}
		return false;
	}

	internal static bool AnyHdLoaderForPortal(MapPortal portal)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		if (((portal == null) ? null : ((Thing)portal).Map?.mapPawns) == null)
		{
			return false;
		}
		IReadOnlyList<Pawn> allPawnsSpawned = ((Thing)portal).Map.mapPawns.AllPawnsSpawned;
		for (int i = 0; i < allPawnsSpawned.Count; i++)
		{
			Pawn val = allPawnsSpawned[i];
			if (((val != null) ? val.CurJob : null) != null && val.CurJobDef == HaulersDreamDefOf.HaulersDream_LoadPortalInBulk)
			{
				LocalTargetInfo target = val.CurJob.GetTarget((TargetIndex)1);
				if ((object)((LocalTargetInfo)(ref target)).Thing == portal)
				{
					return true;
				}
			}
		}
		return false;
	}
}
