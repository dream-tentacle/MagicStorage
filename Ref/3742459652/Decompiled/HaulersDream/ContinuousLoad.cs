using System.Collections;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class ContinuousLoad
{
	private const float ScanRadius = 24f;

	public static bool ShouldChain(JobCondition condition, Job finishedJob)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Invalid comparison between Unknown and I4
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && settings.enableContinuousLoading && (int)condition == 2 && finishedJob != null)
		{
			return finishedJob.playerForced;
		}
		return false;
	}

	public static bool TryChainFrom(Pawn pawn, IManagedLoadable finished)
	{
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || pawn.jobs == null || finished == null)
		{
			return false;
		}
		if (pawn.Dead || pawn.Downed || !((Thing)pawn).Spawned)
		{
			return false;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.enableContinuousLoading)
		{
			return false;
		}
		if (((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null)
		{
			return false;
		}
		Pawn_HealthTracker health = pawn.health;
		bool? obj;
		if (health == null)
		{
			obj = null;
		}
		else
		{
			PawnCapacitiesHandler capacities = health.capacities;
			obj = ((capacities != null) ? new bool?(capacities.CapableOf(PawnCapacityDefOf.Manipulation)) : ((bool?)null));
		}
		bool? flag = obj;
		if (flag != true)
		{
			return false;
		}
		int uniqueLoadID = finished.GetUniqueLoadID();
		LoadableKind kind = finished.Kind;
		Map map = ((Thing)pawn).Map;
		IntVec3 position = ((Thing)pawn).Position;
		float radiusSq = 576f;
		List<(IManagedLoadable, float)> list = new List<(IManagedLoadable, float)>();
		HashSet<int> seenKeys = new HashSet<int>();
		CollectCandidates(pawn, map, kind, position, radiusSq, uniqueLoadID, seenKeys, list);
		list.Sort(delegate((IManagedLoadable adp, float distSq) a, (IManagedLoadable adp, float distSq) b)
		{
			int num2 = a.distSq.CompareTo(b.distSq);
			return (num2 == 0) ? a.adp.GetUniqueLoadID().CompareTo(b.adp.GetUniqueLoadID()) : num2;
		});
		for (int num = 0; num < list.Count; num++)
		{
			IManagedLoadable item = list[num].Item1;
			Job val = TransportLoad.TryGiveBulkJob(pawn, item, playerOrder: true);
			if (val != null)
			{
				val.playerForced = true;
				pawn.jobs.jobQueue.EnqueueFirst(val, (JobTag?)(JobTag)0);
				Thing parentThing = item.GetParentThing();
				HDLog.Dbg($"ContinuousLoad: {pawn} chaining to {((parentThing != null) ? ((Entity)parentThing).LabelShort : null)} " + $"(group {item.GetUniqueLoadID()}) after finishing group {uniqueLoadID}.");
				return true;
			}
		}
		return false;
	}

	private static void CollectCandidates(Pawn pawn, Map map, LoadableKind kind, IntVec3 origin, float radiusSq, int finishedKey, HashSet<int> seenKeys, List<(IManagedLoadable, float)> outCandidates)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 val2;
		switch (kind)
		{
		case LoadableKind.Portal:
		{
			List<Thing> list = map.listerThings.ThingsInGroup((ThingRequestGroup)76);
			for (int i = 0; i < list.Count; i++)
			{
				Thing obj = list[i];
				MapPortal val = (MapPortal)(object)((obj is MapPortal) ? obj : null);
				if (val == null || !((Thing)val).Spawned || !val.LoadInProgress)
				{
					continue;
				}
				val2 = ((Thing)val).Position - origin;
				float num = ((IntVec3)(ref val2)).LengthHorizontalSquared;
				if (!(num > radiusSq))
				{
					MapPortalBulkTarget mapPortalBulkTarget = MapPortalBulkTarget.TryCreate(val);
					if (mapPortalBulkTarget != null && mapPortalBulkTarget.GetUniqueLoadID() != finishedKey && seenKeys.Add(mapPortalBulkTarget.GetUniqueLoadID()) && Passable(pawn, (Thing)(object)val, mapPortalBulkTarget))
					{
						outCandidates.Add((mapPortalBulkTarget, num));
					}
				}
			}
			return;
		}
		case LoadableKind.Vehicle:
		{
			if (!VehicleFrameworkCompat.IsActive)
			{
				return;
			}
			IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
			for (int j = 0; j < allPawnsSpawned.Count; j++)
			{
				Pawn val3 = allPawnsSpawned[j];
				if (val3 == null || !VehicleFrameworkCompat.IsVehicle((Thing)(object)val3))
				{
					continue;
				}
				IList list2 = VehicleFrameworkCompat.CargoToLoad((Thing)(object)val3);
				if (list2 == null || list2.Count == 0)
				{
					continue;
				}
				val2 = ((Thing)val3).Position - origin;
				float num2 = ((IntVec3)(ref val2)).LengthHorizontalSquared;
				if (!(num2 > radiusSq))
				{
					VehicleLoadTarget vehicleLoadTarget = VehicleLoadTarget.TryCreate((Thing)(object)val3);
					if (vehicleLoadTarget != null && vehicleLoadTarget.GetUniqueLoadID() != finishedKey && seenKeys.Add(vehicleLoadTarget.GetUniqueLoadID()) && Passable(pawn, (Thing)(object)val3, vehicleLoadTarget))
					{
						outCandidates.Add((vehicleLoadTarget, num2));
					}
				}
			}
			return;
		}
		}
		List<Thing> list3 = map.listerThings.ThingsInGroup((ThingRequestGroup)44);
		for (int k = 0; k < list3.Count; k++)
		{
			Thing obj2 = list3[k];
			CompTransporter val4 = ((obj2 != null) ? ThingCompUtility.TryGetComp<CompTransporter>(obj2) : null);
			if (((ThingComp)(val4?)).parent == null || !((Thing)((ThingComp)val4).parent).Spawned || !val4.AnyInGroupHasAnythingLeftToLoad || val4.groupID == finishedKey)
			{
				continue;
			}
			val2 = ((Thing)((ThingComp)val4).parent).Position - origin;
			float num3 = ((IntVec3)(ref val2)).LengthHorizontalSquared;
			if (!(num3 > radiusSq) && seenKeys.Add(val4.groupID))
			{
				LoadTransportersAdapter loadTransportersAdapter = LoadTransportersAdapter.TryCreate(val4);
				if (loadTransportersAdapter != null && Passable(pawn, (Thing)(object)((ThingComp)val4).parent, loadTransportersAdapter))
				{
					outCandidates.Add((loadTransportersAdapter, num3));
				}
			}
		}
	}

	private static bool Passable(Pawn pawn, Thing anchor, IManagedLoadable adapter)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		if (anchor == null)
		{
			return false;
		}
		if (anchor.Faction != null && anchor.Faction != ((Thing)pawn).Faction)
		{
			return false;
		}
		if (!adapter.AnythingToLoad())
		{
			return false;
		}
		if (ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit(anchor), (PathEndMode)2, ExtraSweepReach.Ceiling(pawn), false, false, (TraverseMode)0))
		{
			return ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(anchor), 1, -1, (ReservationLayerDef)null, false);
		}
		return false;
	}
}
