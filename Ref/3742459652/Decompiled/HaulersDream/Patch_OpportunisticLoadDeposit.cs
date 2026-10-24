using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_Work), "TryIssueJobPackage")]
public static class Patch_OpportunisticLoadDeposit
{
	[HarmonyPriority(0)]
	private static void Postfix(ref ThinkResult __result, Pawn pawn, JobGiver_Work __instance)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.enableOpportunisticLoad)
		{
			return;
		}
		try
		{
			PostfixInner(ref __result, pawn, __instance, settings);
		}
		catch (Exception ex)
		{
			HDGuard.SeamDegraded(ex, "JobGiver_Work.TryIssueJobPackage (HD opportunistic load deposit)", pawn, "kept vanilla's chosen work job, so the pawn keeps working.");
		}
	}

	private static void PostfixInner(ref ThinkResult __result, Pawn pawn, JobGiver_Work __instance, HaulersDreamSettings s)
	{
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		if (!((ThinkResult)(ref __result)).IsValid || ((ThinkResult)(ref __result)).Job == null || ((pawn != null) ? ((Thing)pawn).Map : null) == null || pawn.Drafted || pawn.inventory == null)
		{
			return;
		}
		Job job = ((ThinkResult)(ref __result)).Job;
		if (job.playerForced || ProtectedWork.IsProtected(job, __instance.emergency) || (job.def?.driverClass != null && job.def.driverClass.Assembly == typeof(Patch_OpportunisticLoadDeposit).Assembly) || (pawn.CurJobDef != null && pawn.CurJobDef.driverClass != null && pawn.CurJobDef.driverClass.Assembly == typeof(Patch_OpportunisticLoadDeposit).Assembly))
		{
			return;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp == null || comp.PeekHashSet().Count == 0)
		{
			return;
		}
		float num = Mathf.Max(0f, (float)s.loadOpportunityScanRadius);
		if (num <= 0f)
		{
			return;
		}
		IManagedLoadable managedLoadable = FindBestOpportunity(pawn, num);
		if (managedLoadable != null)
		{
			Job val = BuildDepositOnlyJob(pawn, managedLoadable);
			if (val != null)
			{
				__result = new ThinkResult(val, ((ThinkResult)(ref __result)).SourceNode, ((ThinkResult)(ref __result)).Tag, ((ThinkResult)(ref __result)).FromQueue);
				string text = $"{pawn} opportunistically diverting to deposit carried cargo into ";
				Thing parentThing = managedLoadable.GetParentThing();
				HDLog.Dbg(text + $"{((parentThing != null) ? ((Entity)parentThing).LabelShort : null)} (kind {managedLoadable.Kind}).");
			}
		}
	}

	private static IManagedLoadable FindBestOpportunity(Pawn pawn, float radius)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		Map map = ((Thing)pawn).Map;
		HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
		if (map == null || instance == null)
		{
			return null;
		}
		Dictionary<ThingDef, int> dictionary = InventorySurplus.SurplusByDef(pawn);
		if (dictionary.Count == 0)
		{
			return null;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		IManagedLoadable result = null;
		float num = float.MaxValue;
		IntVec3 position = ((Thing)pawn).Position;
		foreach (Thing item in GenRadial.RadialDistinctThingsAround(position, map, radius, true))
		{
			if (item == null || (object)item == pawn)
			{
				continue;
			}
			IManagedLoadable managedLoadable = TryAdaptNeedy(item, settings);
			if (managedLoadable != null)
			{
				IntVec3 val = item.Position - position;
				float num2 = ((IntVec3)(ref val)).LengthHorizontalSquared;
				if (!(num2 >= num) && WantsAnyCarried(instance, managedLoadable, pawn, dictionary, radius, num2))
				{
					result = managedLoadable;
					num = num2;
				}
			}
		}
		return result;
	}

	private static IManagedLoadable TryAdaptNeedy(Thing thing, HaulersDreamSettings s)
	{
		if (s == null)
		{
			return null;
		}
		CompTransporter val = ThingCompUtility.TryGetComp<CompTransporter>(thing);
		if (val != null)
		{
			if (!s.enableBulkLoadTransporters || !val.AnyInGroupHasAnythingLeftToLoad)
			{
				return null;
			}
			return LoadTransportersAdapter.TryCreate(val);
		}
		MapPortal val2 = (MapPortal)(object)((thing is MapPortal) ? thing : null);
		if (val2 != null)
		{
			if (!s.enableBulkLoadPortal || !val2.LoadInProgress)
			{
				return null;
			}
			return MapPortalBulkTarget.TryCreate(val2);
		}
		if (s.enableVehicleFramework && s.enableBulkLoadVehicles && VehicleFrameworkCompat.IsActive && VehicleFrameworkCompat.IsVehicle(thing))
		{
			IList list = VehicleFrameworkCompat.CargoToLoad(thing);
			if (list == null || list.Count == 0)
			{
				return null;
			}
			return VehicleLoadTarget.TryCreate(thing);
		}
		return null;
	}

	private static bool WantsAnyCarried(HaulersDreamGameComponent ledger, IManagedLoadable loadable, Pawn pawn, Dictionary<ThingDef, int> carried, float radius, float distSq)
	{
		ledger.LoadRegisterOrUpdate(loadable);
		Dictionary<ThingDef, int> dictionary = ledger.LoadAvailableToClaim(loadable, pawn);
		if (dictionary.Count == 0)
		{
			return false;
		}
		float distanceToTarget = Mathf.Sqrt(distSq);
		bool alreadyOnHigherPriorityHdJob = false;
		foreach (KeyValuePair<ThingDef, int> item in carried)
		{
			int value;
			int targetAvailableForDef = (dictionary.TryGetValue(item.Key, out value) ? value : 0);
			if (OpportunisticLoadPolicy.ShouldDivertTo(featureEnabled: true, item.Value, targetAvailableForDef, distanceToTarget, radius, alreadyOnHigherPriorityHdJob) > 0)
			{
				return true;
			}
		}
		return false;
	}

	internal static Job BuildDepositOnlyJob(Pawn pawn, IManagedLoadable loadable)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		Thing parentThing = loadable.GetParentThing();
		if (parentThing == null)
		{
			return null;
		}
		Job val = JobMaker.MakeJob(JobDefForKind(loadable.Kind), LocalTargetInfo.op_Implicit(parentThing));
		val.targetQueueB = new List<LocalTargetInfo>();
		val.countQueue = new List<int>();
		val.count = 1;
		if (!val.TryMakePreToilReservations(pawn, false))
		{
			return null;
		}
		return val;
	}

	private static JobDef JobDefForKind(LoadableKind kind)
	{
		return (JobDef)(kind switch
		{
			LoadableKind.Portal => HaulersDreamDefOf.HaulersDream_LoadPortalInBulk, 
			LoadableKind.Vehicle => HaulersDreamDefOf.HaulersDream_LoadVehicleInBulk, 
			_ => HaulersDreamDefOf.HaulersDream_LoadTransportersInBulk, 
		});
	}
}
