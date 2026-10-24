using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(TransporterUtility))]
public static class Patch_TransporterUtility_AllSendables
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadTransporters ?? true;
	}

	[HarmonyPatch("AllSendableItems")]
	[HarmonyPostfix]
	private static IEnumerable<Thing> AllSendableItems_Postfix(IEnumerable<Thing> __result, List<CompTransporter> transporters, Map map)
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
		foreach (Thing item2 in EnumerateInFlightTagged(transporters, map, seen, wantPawns: false))
		{
			yield return item2;
		}
	}

	[HarmonyPatch("AllSendablePawns")]
	[HarmonyPostfix]
	private static IEnumerable<Pawn> AllSendablePawns_Postfix(IEnumerable<Pawn> __result, List<CompTransporter> transporters, Map map)
	{
		HashSet<Thing> seen = new HashSet<Thing>();
		foreach (Pawn item in __result)
		{
			if (item != null)
			{
				seen.Add((Thing)(object)item);
			}
			yield return item;
		}
		foreach (Thing item2 in EnumerateInFlightTagged(transporters, map, seen, wantPawns: true))
		{
			yield return (Pawn)item2;
		}
	}

	private static IEnumerable<Thing> EnumerateInFlightTagged(List<CompTransporter> transporters, Map map, HashSet<Thing> seen, bool wantPawns)
	{
		if (map?.mapPawns == null || transporters == null || transporters.Count == 0)
		{
			yield break;
		}
		CompTransporter val = transporters[0];
		if (val == null)
		{
			yield break;
		}
		int groupID = val.groupID;
		if (groupID < 0)
		{
			yield break;
		}
		ThingWithComps parent = ((ThingComp)val).parent;
		CompShuttle shuttle = ((parent != null) ? ThingCompUtility.TryGetComp<CompShuttle>((Thing)(object)parent) : null);
		IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
		for (int i = 0; i < spawned.Count; i++)
		{
			Pawn val2 = spawned[i];
			if (((val2 != null) ? val2.CurJob : null) == null || val2.CurJobDef != HaulersDreamDefOf.HaulersDream_LoadTransportersInBulk)
			{
				continue;
			}
			LocalTargetInfo target = val2.CurJob.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			CompTransporter val3 = ((thing != null) ? ThingCompUtility.TryGetComp<CompTransporter>(thing) : null);
			if (val3 == null || val3.groupID != groupID)
			{
				continue;
			}
			CompHauledToInventory comp = ((ThingWithComps)val2).GetComp<CompHauledToInventory>();
			ThingOwner<Thing> inner = val2.inventory?.innerContainer;
			if (comp == null || inner == null)
			{
				continue;
			}
			foreach (Thing item in comp.PeekHashSet())
			{
				if (item != null && !item.Destroyed && item.def != null && ((ThingOwner)inner).Contains(item) && item is Pawn == wantPawns && seen.Add(item) && (shuttle == null || shuttle.IsRequired(item) || shuttle.IsAllowed(item)))
				{
					yield return item;
				}
			}
		}
	}
}
