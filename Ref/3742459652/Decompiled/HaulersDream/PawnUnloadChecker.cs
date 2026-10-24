using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HaulersDream;

public static class PawnUnloadChecker
{
	public static void CheckIfShouldUnload(Pawn pawn, bool forced = false, bool behindQueuedWork = false, bool immediate = false)
	{
		CompHauledToInventory compHauledToInventory = ((pawn != null) ? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() : null);
		if (compHauledToInventory == null)
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || pawn.Drafted || (!forced && ProtectedWork.IsRestingPatient(pawn)) || (!forced && InDirectedActivity(pawn)) || (!forced && pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_BillPrepGather) || (!forced && HoldsStockForActiveDoBill(pawn, compHauledToInventory)) || ConstructionMaterialHold.HoldsMaterialForActiveConstruction(pawn, compHauledToInventory, forced))
		{
			return;
		}
		bool flag = ((Thing)pawn).Faction == Faction.OfPlayerSilentFail && (forced || YieldRouter.IsEligible(pawn)) && pawn.inventory?.innerContainer != null;
		bool unloadAllSurplus = settings.unloadAllSurplus;
		if (flag && !CaravanFormingUtility.IsFormingCaravan(pawn) && (unloadAllSurplus || settings.HasAnySurplusProducingRule))
		{
			AdoptSurplusInventory(pawn, compHauledToInventory, unloadAllSurplus);
		}
		HashSet<Thing> hashSet = compHauledToInventory.GetHashSet();
		Pawn_InventoryTracker inventory = pawn.inventory;
		int? obj;
		if (inventory == null)
		{
			obj = null;
		}
		else
		{
			ThingOwner<Thing> innerContainer = inventory.innerContainer;
			obj = ((innerContainer != null) ? new int?(((ThingOwner)innerContainer).Count) : ((int?)null));
		}
		int? num = obj;
		int valueOrDefault = num.GetValueOrDefault();
		if (forced && !behindQueuedWork && pawn.CurJobDef != HaulersDreamDefOf.HaulersDream_UnloadInventory && TryPromoteQueuedUnloadToFront(pawn))
		{
			if (immediate)
			{
				Pawn_JobTracker jobs = pawn.jobs;
				if (jobs != null)
				{
					jobs.EndCurrentJob((JobCondition)16, true, true);
				}
			}
			return;
		}
		bool alreadyUnloading = pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_UnloadInventory || HasQueuedUnload(pawn);
		TickManager tickManager = Find.TickManager;
		int ticksSinceLastYield = ((tickManager != null) ? tickManager.TicksGame : 0) - compHauledToInventory.lastYieldTick;
		bool hasPendingWork = HasPendingRealWork(pawn);
		int graceTicks = ((!OpportunisticUnload.IsInDowntimeJob(pawn, settings)) ? settings.unloadGraceTicks : 0);
		for (int i = 0; i < 2; i++)
		{
			bool flag2 = AnyUnloadable(pawn, hashSet);
			UnloadDecision unloadDecision = UnloadPolicy.Decide(flag, hashSet.Count, valueOrDefault, alreadyUnloading, forced, hasPendingWork, ticksSinceLastYield, graceTicks, flag2);
			int? obj2;
			switch (unloadDecision)
			{
			default:
				return;
			case UnloadDecision.ClearTracker:
			{
				HDLog.Dbg($"{pawn} tracker out of sync ({valueOrDefault} < {hashSet.Count}); pruning stale tags.");
				ThingOwner<Thing> inv = pawn.inventory?.innerContainer;
				hashSet.RemoveWhere((Thing t) => t == null || t.Destroyed || inv == null || !((ThingOwner)inv).Contains(t));
				Pawn_InventoryTracker inventory2 = pawn.inventory;
				if (inventory2 == null)
				{
					obj2 = null;
					break;
				}
				ThingOwner<Thing> innerContainer2 = inventory2.innerContainer;
				obj2 = ((innerContainer2 != null) ? new int?(((ThingOwner)innerContainer2).Count) : ((int?)null));
				break;
			}
			case UnloadDecision.Queue:
			{
				if (((Thing)pawn).Map != null && !MapGate.ShouldUnloadToStorage(((Thing)pawn).Map))
				{
					Job val = PackAnimalLoad.TryGetOpportunisticLoadJob(pawn);
					if (val != null && pawn.jobs != null)
					{
						bool flag3 = UnloadPolicy.QueueBehindPendingWork(behindQueuedWork, hasPendingWork);
						if (flag3)
						{
							pawn.jobs.jobQueue.EnqueueLast(val, (JobTag?)(JobTag)0);
						}
						else
						{
							pawn.jobs.jobQueue.EnqueueFirst(val, (JobTag?)(JobTag)0);
						}
						HDLog.Dbg($"{pawn} queued caravan pack-animal load ({hashSet.Count} tracked).");
						YieldRouter.EnsureSelfPickupJob(pawn);
						if (immediate && !flag3)
						{
							InterruptCurrentJob(pawn);
						}
					}
					return;
				}
				Job val2 = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_UnloadInventory);
				if (pawn.jobs != null && val2.TryMakePreToilReservations(pawn, false))
				{
					bool flag4 = UnloadPolicy.QueueBehindPendingWork(behindQueuedWork, hasPendingWork);
					if (flag4)
					{
						pawn.jobs.jobQueue.EnqueueLast(val2, (JobTag?)(JobTag)0);
					}
					else
					{
						pawn.jobs.jobQueue.EnqueueFirst(val2, (JobTag?)(JobTag)0);
					}
					HDLog.Dbg($"{pawn} queued unload ({hashSet.Count} tracked, forced={forced}, behind={flag4}).");
					YieldRouter.EnsureSelfPickupJob(pawn);
					if (immediate && flag2 && !flag4)
					{
						InterruptCurrentJob(pawn);
					}
				}
				return;
			}
			}
			num = obj2;
			valueOrDefault = num.GetValueOrDefault();
		}
	}

	internal static void InterruptCurrentJob(Pawn pawn)
	{
		Job val = pawn?.jobs?.curJob;
		if (val != null)
		{
			JobDef def = val.def;
			if (def != HaulersDreamDefOf.HaulersDream_UnloadInventory && def != HaulersDreamDefOf.HaulersDream_SelfPickup && def != HaulersDreamDefOf.HaulersDream_LoadPackAnimal)
			{
				pawn.jobs.EndCurrentJob((JobCondition)16, true, true);
			}
		}
	}

	internal static bool InDirectedActivity(Pawn p)
	{
		if (p != null)
		{
			if (LordUtility.GetLord(p) == null)
			{
				return p.mindState?.duty != null;
			}
			return true;
		}
		return false;
	}

	internal static void AdoptSurplusInventory(Pawn pawn, CompHauledToInventory comp, bool adoptAll)
	{
		ThingOwner<Thing> val = pawn.inventory?.innerContainer;
		if (val == null || comp == null || InDirectedActivity(pawn))
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		int num = 0;
		for (int i = 0; i < ((ThingOwner)val).Count; i++)
		{
			Thing val2 = val[i];
			if (val2 != null && !val2.Destroyed && (adoptAll || (settings != null && settings.RuleProducesSurplus(val2.def))) && ((settings != null && settings.TryGetItemRule(val2.def, out var rule) && rule.mode == ItemUnloadMode.UnloadAlways) || (!SimpleSidearmsCompat.IsRememberedSidearm(pawn, val2) && !GrabYourToolCompat.IsCarriedTool(pawn, val2) && !SurvivalToolsCompat.IsCarriedTool(pawn, val2) && !AnimalInteractFood.IsHeldForInteraction(pawn, val2))) && InventorySurplus.SurplusOf(pawn, val2) > 0 && InventorySurplus.HasUnloadDestination(pawn, val2))
			{
				int count = comp.PeekHashSet().Count;
				comp.RegisterHauledItem(val2);
				if (comp.PeekHashSet().Count > count)
				{
					num++;
				}
			}
		}
		if (num > 0)
		{
			comp.NotifyYieldPicked();
			HDLog.Dbg($"{pawn} adopted {num} surplus inventory stack(s) it did not scoop (adoptAll={adoptAll}).");
		}
	}

	internal static bool AnyUnloadable(Pawn pawn, HashSet<Thing> carried)
	{
		ThingOwner<Thing> val = pawn.inventory?.innerContainer;
		if (val == null || carried == null)
		{
			return false;
		}
		foreach (Thing item in carried)
		{
			if (item != null && ((ThingOwner)val).Contains(item) && InventorySurplus.SurplusOf(pawn, item) > 0)
			{
				return true;
			}
		}
		return false;
	}

	private static bool TryPromoteQueuedUnloadToFront(Pawn pawn)
	{
		JobQueue val = pawn?.jobs?.jobQueue;
		if (val == null || val.Count == 0)
		{
			return false;
		}
		for (int i = 0; i < val.Count; i++)
		{
			Job val2 = val[i]?.job;
			if (val2?.def == HaulersDreamDefOf.HaulersDream_UnloadInventory)
			{
				if (i == 0)
				{
					return true;
				}
				val.EnqueueFirst(val.Extract(val2)?.job ?? val2, (JobTag?)(JobTag)0);
				return true;
			}
		}
		return false;
	}

	internal static bool HasQueuedUnload(Pawn pawn)
	{
		JobQueue val = pawn.jobs?.jobQueue;
		if (val != null)
		{
			foreach (QueuedJob item in val)
			{
				if (item?.job?.def == HaulersDreamDefOf.HaulersDream_UnloadInventory)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool HoldsStockForActiveDoBill(Pawn pawn, CompHauledToInventory comp)
	{
		ThingOwner<Thing> val = pawn?.inventory?.innerContainer;
		if (val == null || comp == null)
		{
			return false;
		}
		if (MatchesActiveDoBill(pawn.CurJob, comp, val))
		{
			return true;
		}
		JobQueue val2 = pawn.jobs?.jobQueue;
		if (val2 != null)
		{
			foreach (QueuedJob item in val2)
			{
				if (MatchesActiveDoBill(item?.job, comp, val))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool MatchesActiveDoBill(Job job, CompHauledToInventory comp, ThingOwner<Thing> inv)
	{
		if (job == null || job.def != JobDefOf.DoBill || job.bill?.recipe == null)
		{
			return false;
		}
		foreach (Thing item in comp.PeekHashSet())
		{
			if (item != null && ((ThingOwner)inv).Contains(item) && InventoryShare.IsUsableForBill(item, job.bill))
			{
				return true;
			}
		}
		return false;
	}

	internal static bool HasPendingRealWork(Pawn pawn)
	{
		JobQueue val = pawn.jobs?.jobQueue;
		if (val == null)
		{
			return false;
		}
		JobDef haulersDream_SelfPickup = HaulersDreamDefOf.HaulersDream_SelfPickup;
		JobDef haulersDream_UnloadInventory = HaulersDreamDefOf.HaulersDream_UnloadInventory;
		for (int i = 0; i < val.Count; i++)
		{
			if (UnloadPolicy.IsPendingRealWork<JobDef>(val[i]?.job?.def, haulersDream_SelfPickup, haulersDream_UnloadInventory))
			{
				return true;
			}
		}
		return false;
	}
}
