using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(EnterPortalUtility), "ThingsBeingHauledTo")]
public static class Patch_EnterPortalUtility_ThingsBeingHauledTo
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadPortal ?? true;
	}

	private static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result, MapPortal portal)
	{
		HashSet<Thing> seen = new HashSet<Thing>();
		foreach (Thing item in __result)
		{
			if (item != null)
			{
				seen.Add(item);
			}
			yield return item;
		}
		if (((portal == null) ? null : ((Thing)portal).Map?.mapPawns) == null)
		{
			yield break;
		}
		IReadOnlyList<Pawn> spawned = ((Thing)portal).Map.mapPawns.AllPawnsSpawned;
		for (int i = 0; i < spawned.Count; i++)
		{
			Pawn val = spawned[i];
			if (((val != null) ? val.CurJob : null) == null || val.CurJobDef != HaulersDreamDefOf.HaulersDream_LoadPortalInBulk)
			{
				continue;
			}
			LocalTargetInfo target = val.CurJob.GetTarget((TargetIndex)1);
			if ((object)((LocalTargetInfo)(ref target)).Thing != portal)
			{
				continue;
			}
			CompHauledToInventory comp = ((ThingWithComps)val).GetComp<CompHauledToInventory>();
			ThingOwner<Thing> inner = val.inventory?.innerContainer;
			if (comp == null || inner == null)
			{
				continue;
			}
			foreach (Thing item2 in comp.PeekHashSet())
			{
				if (item2 != null && !item2.Destroyed && ((ThingOwner)inner).Contains(item2) && item2.def != null && seen.Add(item2) && PortalStillWants(portal, item2.def))
				{
					yield return item2;
				}
			}
		}
	}

	private static bool PortalStillWants(MapPortal portal, ThingDef def)
	{
		List<TransferableOneWay> leftToLoad = portal.leftToLoad;
		if (leftToLoad == null)
		{
			return false;
		}
		for (int i = 0; i < leftToLoad.Count; i++)
		{
			TransferableOneWay val = leftToLoad[i];
			if (val != null && ((Transferable)val).ThingDef == def && ((Transferable)val).CountToTransfer > 0)
			{
				return true;
			}
		}
		return false;
	}
}
