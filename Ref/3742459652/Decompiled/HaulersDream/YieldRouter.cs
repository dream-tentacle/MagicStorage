using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class YieldRouter
{
	[ThreadStatic]
	private static bool routing;

	[ThreadStatic]
	private static List<Thing> sweepScratch;

	private const float SweepRadius = 12f;

	private const int SweepMaxStacks = 24;

	private const int SweepCooldownTicks = 250;

	[ThreadStatic]
	private static Pawn deconstructCapturePawn;

	[ThreadStatic]
	private static List<Thing> deconstructCapturedLeavings;

	private static TrainableDef haulTrainable;

	private static bool haulTrainableResolved;

	public static bool OnTryPlaceThing(Thing thing, IntVec3 center, Map map, ThingPlaceMode mode, ref Thing lastResultingThing, ref bool result, out Pawn dropPawn)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Invalid comparison between Unknown and I4
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Invalid comparison between Unknown and I4
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		dropPawn = null;
		if (map == null || thing == null || thing.Destroyed || thing.def == null)
		{
			return true;
		}
		if (!InferencePolicy.IsRoutablePlacement(routing, (int)mode == 1, (int)thing.def.category == 2))
		{
			return true;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return true;
		}
		HaulSourceType type;
		Pawn val = FindWorker(center, map, out type);
		if (type == HaulSourceType.Harvest && IsLoggingYield(val))
		{
			type = HaulSourceType.Logging;
		}
		else if (type == HaulSourceType.Mining && IsChunkYield(thing))
		{
			type = HaulSourceType.Chunks;
		}
		if (val == null || !settings.IsTypeEnabled(type))
		{
			return true;
		}
		if (type == HaulSourceType.Uninstall && map.IsPlayerHome)
		{
			return true;
		}
		if (settings.BehaviorFor(type) == YieldBehavior.DropThenHaul || type == HaulSourceType.Strip)
		{
			dropPawn = val;
			return true;
		}
		if (RouteIntoInventory(val, thing, type, out var taken, out var fullyConsumed) && fullyConsumed)
		{
			lastResultingThing = taken;
			result = true;
			return false;
		}
		return true;
	}

	public static void OnTryPlaceThingPost(Thing lastResultingThing, Pawn dropPawn)
	{
		if (dropPawn == null)
		{
			if (deconstructCapturePawn != null)
			{
				CaptureDeconstructLeaving(lastResultingThing);
			}
		}
		else if (lastResultingThing != null && lastResultingThing.Spawned && !lastResultingThing.Destroyed && !StoreUtility.IsInValidStorage(lastResultingThing))
		{
			RecordSelfPickup(dropPawn, lastResultingThing);
		}
	}

	private static void RecordSelfPickup(Pawn pawn, Thing thing)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		CompHauledToInventory compHauledToInventory = ((pawn != null) ? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() : null);
		if (compHauledToInventory != null && pawn.jobs != null)
		{
			if (HasScoopDestination(pawn, thing))
			{
				SelfPickupClaims.Claim(thing, pawn);
			}
			MaybeSweepNearbyIntoPending(pawn, thing.Position);
			bool num = pawn.jobs.curDriver is JobDriver_PlantWork;
			bool flag = false;
			if (num)
			{
				TickManager tickManager = Find.TickManager;
				int num2 = ((tickManager != null) ? tickManager.TicksGame : 0);
				IntVec3 lastPlantHarvestCell = compHauledToInventory.lastPlantHarvestCell;
				flag = HarvestSectionPolicy.IsClustered(((IntVec3)(ref lastPlantHarvestCell)).IsValid, Math.Abs(thing.Position.x - lastPlantHarvestCell.x), Math.Abs(thing.Position.z - lastPlantHarvestCell.z), num2 - compHauledToInventory.lastPlantHarvestTick, 2, 600);
				compHauledToInventory.lastPlantHarvestCell = thing.Position;
				compHauledToInventory.lastPlantHarvestTick = num2;
			}
			bool flag2 = HarvestSectionPolicy.ShouldCollectNow(num, flag, compHauledToInventory.pendingSelfPickups.Count, 8);
			bool directHarvest = num && !flag;
			if (MasterEnable.Active && FitToStartHaul(pawn) && flag2)
			{
				EnsureSelfPickupJob(pawn, compHauledToInventory, directHarvest);
			}
		}
	}

	internal static void EnsureSelfPickupJob(Pawn pawn, CompHauledToInventory comp = null, bool directHarvest = false)
	{
		if (pawn?.jobs == null)
		{
			return;
		}
		comp = comp ?? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp != null && comp.pendingSelfPickups.Count != 0 && !HasSelfPickupJob(pawn))
		{
			comp.selfPickupDirectHarvest = directHarvest;
			Job val = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_SelfPickup);
			if (val.TryMakePreToilReservations(pawn, false))
			{
				pawn.jobs.jobQueue.EnqueueFirst(val, (JobTag?)(JobTag)0);
				HDLog.Dbg($"{pawn} queued self-pickup ({comp.pendingSelfPickups.Count} pending drops).");
			}
		}
	}

	internal static void MaybeSweepNearbyIntoPending(Pawn pawn, IntVec3 anchor)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings s = HaulersDreamMod.Settings;
		if (s == null || !s.sweepNearbyWhileWorking)
		{
			return;
		}
		Pawn obj = pawn;
		Map map = ((obj != null) ? ((Thing)obj).Map : null);
		if (map == null || pawn.jobs == null || !((IntVec3)(ref anchor)).IsValid || !IsEligible(pawn) || !MasterEnable.Active || !FitToStartHaul(pawn))
		{
			return;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp == null || !comp.autoHaulYields)
		{
			return;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : 0);
		if (num - comp.lastSweepTick < 250)
		{
			return;
		}
		comp.lastSweepTick = num;
		HashSet<Thing> claimed = RouteSelection.ClaimedByOtherPawns(pawn);
		float radiusSq = 144f;
		int added = 0;
		int claimedForMe = 0;
		PawnMass pawnMass = PawnMassCache.MassInfo(pawn);
		float sweepMaxCap = pawnMass.Capacity;
		float sweepBaseCap = CarryMath.EffectiveCapacity(sweepMaxCap, s.carryLimitFraction);
		float sweepCur = pawnMass.CurrentMass;
		List<Thing> list = sweepScratch ?? (sweepScratch = new List<Thing>());
		list.Clear();
		ICollection<Thing> collection = map.listerHaulables.ThingsPotentiallyNeedingHauling();
		IntVec3 val;
		if (collection is HashSet<Thing> hashSet)
		{
			foreach (Thing item in hashSet)
			{
				if (item != null)
				{
					val = item.Position - anchor;
					if ((float)((IntVec3)(ref val)).LengthHorizontalSquared <= radiusSq)
					{
						list.Add(item);
					}
				}
			}
		}
		else
		{
			foreach (Thing item2 in collection)
			{
				if (item2 != null)
				{
					val = item2.Position - anchor;
					if ((float)((IntVec3)(ref val)).LengthHorizontalSquared <= radiusSq)
					{
						list.Add(item2);
					}
				}
			}
		}
		list.Sort(delegate(Thing a, Thing b)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 val2 = a.Position - anchor;
			int lengthHorizontalSquared = ((IntVec3)(ref val2)).LengthHorizontalSquared;
			val2 = b.Position - anchor;
			int lengthHorizontalSquared2 = ((IntVec3)(ref val2)).LengthHorizontalSquared;
			int num3 = lengthHorizontalSquared.CompareTo(lengthHorizontalSquared2);
			return (num3 == 0) ? a.thingIDNumber.CompareTo(b.thingIDNumber) : num3;
		});
		for (int num2 = 0; num2 < list.Count && TryQueue(list[num2]); num2++)
		{
		}
		list.Clear();
		if (claimedForMe > 0)
		{
			HDLog.Dbg($"{pawn} area-sweep: queued {claimedForMe} nearby loose stack(s) into self-pickup" + ((claimedForMe < added) ? $" ({added - claimedForMe} left with a closer colonist)." : "."));
			EnsureSelfPickupJob(pawn, comp);
		}
		bool TryQueue(Thing t)
		{
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0124: Unknown result type (might be due to invalid IL or missing references)
			//IL_0142: Unknown result type (might be due to invalid IL or missing references)
			if (added >= 24)
			{
				return false;
			}
			if (t == null || !t.Spawned || t.Map != map || t is Corpse)
			{
				return true;
			}
			if (t.def == null || !t.def.EverHaulable)
			{
				return true;
			}
			IntVec3 val2 = t.Position - anchor;
			if ((float)((IntVec3)(ref val2)).LengthHorizontalSquared > radiusSq)
			{
				return true;
			}
			if (comp.pendingSelfPickups.Contains(t) || ForbidUtility.IsForbidden(t, pawn) || claimed.Contains(t) || StoreUtility.IsInValidStorage(t))
			{
				return true;
			}
			if (HaulChurnGuard.IsBackedOff(t))
			{
				return true;
			}
			if (RimIOTCompat.IsPresent && RimIOTCompat.IsRimIOTHandledCell(map, t.Position))
			{
				return true;
			}
			if (CorpseStripper.ShouldLeaveTaintedApparel(t, s))
			{
				return true;
			}
			if (ForeignOrderGuard.ClaimedByForeignOrder(t))
			{
				return true;
			}
			if (OverloadGate.CountToPickUp(pawn, t, s, sweepMaxCap, sweepBaseCap, sweepCur) <= 0)
			{
				return true;
			}
			if (!HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, false))
			{
				return true;
			}
			if (!ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(t), (PathEndMode)3))
			{
				return true;
			}
			IHaulDestination val3 = default(IHaulDestination);
			if (!StoreUtility.TryFindBestBetterStorageFor(t, pawn, map, StoreUtility.CurrentStoragePriorityOf(t, false), ((Thing)pawn).Faction, ref val2, ref val3, false))
			{
				return true;
			}
			added++;
			if (SelfPickupClaims.Claim(t, pawn))
			{
				claimedForMe++;
			}
			return true;
		}
	}

	internal static void MaybeUnloadBecauseFull(Pawn pawn, HaulersDreamSettings s)
	{
		if (s == null || !UnloadPolicy.FullTriggerAllowed(s.strictCarryWeight, s.markForUnload))
		{
			return;
		}
		if (((pawn != null) ? ((Thing)pawn).Map : null) != null && !MapGate.ShouldUnloadToStorage(((Thing)pawn).Map))
		{
			PackAnimalLoad.MaybeAutoDivert(pawn, s);
		}
		else if (!s.keepWorkingWhenFull)
		{
			CompHauledToInventory compHauledToInventory = ((pawn != null) ? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() : null);
			TickManager tickManager = Find.TickManager;
			int num = ((tickManager != null) ? tickManager.TicksGame : 0);
			bool divertCooldownElapsed = compHauledToInventory == null || num - compHauledToInventory.lastOpportunisticUnloadTick >= 250;
			if (UnloadPolicy.FullTriggerReady(s.strictCarryWeight, s.markForUnload, divertCooldownElapsed))
			{
				OpportunisticUnload.NotifyDiverted(pawn);
				PawnUnloadChecker.CheckIfShouldUnload(pawn, forced: true, behindQueuedWork: true);
			}
		}
	}

	private static bool HasSelfPickupJob(Pawn pawn)
	{
		if (pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_SelfPickup)
		{
			return true;
		}
		JobQueue jobQueue = pawn.jobs.jobQueue;
		if (jobQueue != null)
		{
			foreach (QueuedJob item in jobQueue)
			{
				if (item?.job?.def == HaulersDreamDefOf.HaulersDream_SelfPickup)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static void BeginDeconstructCapture(CellRect area, Map map, Thing diedThing)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		deconstructCapturePawn = null;
		deconstructCapturedLeavings = null;
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && settings.IsTypeEnabled(HaulSourceType.Deconstruct) && map != null)
		{
			Pawn val = FindDeconstructor(area, map, diedThing);
			if (val != null)
			{
				deconstructCapturePawn = val;
				deconstructCapturedLeavings = new List<Thing>();
			}
		}
	}

	internal static void CaptureDeconstructLeaving(Thing placed)
	{
		List<Thing> list = deconstructCapturedLeavings;
		if (list != null && placed != null && !list.Contains(placed))
		{
			list.Add(placed);
		}
	}

	public static void EndDeconstructCapture()
	{
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Invalid comparison between Unknown and I4
		Pawn val = deconstructCapturePawn;
		List<Thing> list = deconstructCapturedLeavings;
		deconstructCapturePawn = null;
		deconstructCapturedLeavings = null;
		if (val == null || list == null || list.Count == 0)
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return;
		}
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			Thing val2 = list[i];
			if (val2 != null && val2.Spawned && !val2.Destroyed && val2.def != null && (int)val2.def.category == 2 && !ForbidUtility.IsForbidden(val2, val) && !StoreUtility.IsInValidStorage(val2))
			{
				if (settings.BehaviorFor(HaulSourceType.Deconstruct) == YieldBehavior.DropThenHaul)
				{
					RecordSelfPickup(val, val2);
				}
				else
				{
					RouteIntoInventory(val, val2, HaulSourceType.Deconstruct, out var _, out var _);
				}
				num++;
			}
		}
		if (num > 0)
		{
			HDLog.Dbg($"{val} deconstruct: captured {num} leaving stack(s) for pickup.");
		}
	}

	internal static bool FitToStartHaul(Pawn pawn)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.skipHaulWhileBleeding)
		{
			return true;
		}
		float? obj;
		if (pawn == null)
		{
			obj = null;
		}
		else
		{
			Pawn_HealthTracker health = pawn.health;
			if (health == null)
			{
				obj = null;
			}
			else
			{
				HediffSet hediffSet = health.hediffSet;
				obj = ((hediffSet != null) ? new float?(hediffSet.BleedRateTotal) : ((float?)null));
			}
		}
		float? num = obj;
		float valueOrDefault = num.GetValueOrDefault();
		return FitToHaulPolicy.FitToStartHaul(settings.skipHaulWhileBleeding, valueOrDefault, settings.bleedThresholdPerDay);
	}

	internal static bool HasScoopDestination(Pawn pawn, Thing thing)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		Map val = ((pawn != null) ? ((Thing)pawn).Map : null);
		if (val == null || thing?.def == null)
		{
			return false;
		}
		IntVec3 val2 = default(IntVec3);
		IHaulDestination val3 = default(IHaulDestination);
		return StoreUtility.TryFindBestBetterStorageFor(thing, pawn, val, StoreUtility.CurrentStoragePriorityOf(thing, false), ((Thing)pawn).Faction, ref val2, ref val3, false);
	}

	private static bool RouteIntoInventory(Pawn pawn, Thing thing, HaulSourceType type, out Thing taken, out bool fullyConsumed)
	{
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		taken = null;
		fullyConsumed = false;
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp == null)
		{
			return false;
		}
		if (!HasScoopDestination(pawn, thing))
		{
			return false;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		int num = OverloadGate.CountToPickUp(pawn, thing, settings);
		if (num <= 0)
		{
			MaybeUnloadBecauseFull(pawn, settings);
			return false;
		}
		routing = true;
		try
		{
			bool flag = num >= thing.stackCount;
			Thing val = thing.SplitOff(num);
			if (val == null)
			{
				return false;
			}
			ThingOwner directlyHeldThings = pawn.inventory.GetDirectlyHeldThings();
			int stackCount = val.stackCount;
			bool flag2 = directlyHeldThings.TryAddOrTransfer(val, true);
			if (flag2 || val.stackCount < stackCount)
			{
				int mergedCount = (flag2 ? stackCount : (stackCount - val.stackCount));
				Thing val2 = ((val.def.stackLimit == 1) ? val : (InventoryStackOfDef(directlyHeldThings, val.def, pawn) ?? (flag2 ? val : null)));
				if (val2 != null)
				{
					comp.RegisterHauledItem(val2, mergedCount);
				}
				comp.NotifyYieldPicked();
				HDLog.Dbg($"{pawn} scooped x{stackCount - val.stackCount} {((Def)(val.def?)).label} ({type}).");
				MaybeSweepNearbyIntoPending(pawn, thing.Position);
			}
			if (flag2)
			{
				taken = val;
				fullyConsumed = flag;
				return true;
			}
			if (!flag && val.stackCount > 0 && !thing.Destroyed)
			{
				thing.TryAbsorbStack(val, false);
			}
			return false;
		}
		finally
		{
			routing = false;
		}
	}

	internal static Thing InventoryStackOfDef(ThingOwner owner, ThingDef def, Pawn pawn = null)
	{
		if (owner == null || def == null)
		{
			return null;
		}
		Thing val = null;
		for (int i = 0; i < owner.Count; i++)
		{
			Thing val2 = owner[i];
			if (val2?.def == def)
			{
				if (pawn == null || (!SimpleSidearmsCompat.IsRememberedSidearm(pawn, val2) && !GrabYourToolCompat.IsCarriedTool(pawn, val2) && !SurvivalToolsCompat.IsCarriedTool(pawn, val2)))
				{
					return val2;
				}
				if (val == null)
				{
					val = val2;
				}
			}
		}
		return val;
	}

	internal static int InventoryCountOfDef(ThingOwner owner, ThingDef def)
	{
		if (owner == null || def == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < owner.Count; i++)
		{
			if (owner[i]?.def == def)
			{
				num += owner[i].stackCount;
			}
		}
		return num;
	}

	internal static int InventoryCountOfPair(ThingOwner owner, ThingDef def, ThingDef stuff)
	{
		if (owner == null || def == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < owner.Count; i++)
		{
			Thing val = owner[i];
			if (val?.def == def && val.Stuff == stuff)
			{
				num += val.stackCount;
			}
		}
		return num;
	}

	private static bool IsLoggingYield(Pawn producer)
	{
		object obj;
		if (producer == null)
		{
			obj = null;
		}
		else
		{
			Job curJob = producer.CurJob;
			obj = ((curJob != null) ? ((LocalTargetInfo)(ref curJob.targetA)).Thing : null);
		}
		Plant val = (Plant)((obj is Plant) ? obj : null);
		if (val != null)
		{
			return ((Thing)val).def.plant.IsTree;
		}
		return false;
	}

	private static bool IsChunkYield(Thing yielded)
	{
		if (yielded?.def != null)
		{
			return yielded.def.IsWithinCategory(ThingCategoryDefOf.Chunks);
		}
		return false;
	}

	private static Pawn FindWorker(IntVec3 center, Map map, out HaulSourceType type)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		type = HaulSourceType.Harvest;
		IntVec3[] adjacentCellsAndInside = GenAdj.AdjacentCellsAndInside;
		for (int i = -1; i < adjacentCellsAndInside.Length - 1; i++)
		{
			IntVec3 val = ((i < 0) ? center : (center + adjacentCellsAndInside[i]));
			if (!GenGrid.InBounds(val, map))
			{
				continue;
			}
			List<Thing> list = map.thingGrid.ThingsListAtFast(val);
			for (int j = 0; j < list.Count; j++)
			{
				Thing obj = list[j];
				Pawn val2 = (Pawn)(object)((obj is Pawn) ? obj : null);
				if (val2 != null && TryGetWorkType(val2.jobs?.curDriver, out var type2) && IsCandidate(val2, OptOutOverridePolicy.ExplicitOrderOverridesOptOut(type2)))
				{
					IntVec3 position = ((Thing)val2).Position;
					bool flag = val2.CurJob != null;
					IntVec3 val3 = (IntVec3)(flag ? ((LocalTargetInfo)(ref val2.CurJob.targetA)).Cell : default(IntVec3));
					if (InferencePolicy.IsTrueProducer(position.x, position.z, flag, val3.x, val3.z, center.x, center.z))
					{
						type = type2;
						return val2;
					}
				}
			}
		}
		return null;
	}

	private static Pawn FindDeconstructor(CellRect area, Map map, Thing diedThing)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		Pawn val = null;
		CellRect val2 = ((CellRect)(ref area)).ExpandedBy(1);
		foreach (IntVec3 cell in ((CellRect)(ref val2)).Cells)
		{
			if (!GenGrid.InBounds(cell, map))
			{
				continue;
			}
			List<Thing> list = map.thingGrid.ThingsListAtFast(cell);
			for (int i = 0; i < list.Count; i++)
			{
				Thing obj = list[i];
				Pawn val3 = (Pawn)(object)((obj is Pawn) ? obj : null);
				if (val3 == null || !IsCandidate(val3) || !(val3.jobs?.curDriver is JobDriver_Deconstruct))
				{
					continue;
				}
				if (diedThing != null)
				{
					Job curJob = val3.CurJob;
					if (((curJob != null) ? ((LocalTargetInfo)(ref curJob.targetA)).Thing : null) == diedThing)
					{
						return val3;
					}
				}
				if (diedThing == null && val == null)
				{
					val = val3;
				}
			}
		}
		return val;
	}

	private static bool TryGetWorkType(JobDriver driver, out HaulSourceType type)
	{
		if (!(driver is JobDriver_PlantWork))
		{
			if (!(driver is JobDriver_Mine))
			{
				if (!(driver is JobDriver_OperateDeepDrill))
				{
					if (!(driver is JobDriver_GatherAnimalBodyResources))
					{
						if (!(driver is JobDriver_Strip))
						{
							if (!(driver is JobDriver_Uninstall))
							{
								if (driver is JobDriver_Fish)
								{
									type = HaulSourceType.Fishing;
									return true;
								}
								type = HaulSourceType.Harvest;
								return false;
							}
							type = HaulSourceType.Uninstall;
							return true;
						}
						type = HaulSourceType.Strip;
						return true;
					}
					type = HaulSourceType.Animal;
					return true;
				}
				type = HaulSourceType.DeepDrill;
				return true;
			}
			type = HaulSourceType.Mining;
			return true;
		}
		type = HaulSourceType.Harvest;
		return true;
	}

	public static bool IsCandidate(Pawn p, bool overrideOptOut = false)
	{
		if (p == null || ((Thing)p).Faction != Faction.OfPlayerSilentFail)
		{
			return false;
		}
		if (!IsEligible(p))
		{
			return false;
		}
		if (HaulersDreamMod.Settings == null)
		{
			return false;
		}
		if (!MapGate.HdActiveOnMap(((Thing)p).Map))
		{
			return false;
		}
		CompHauledToInventory comp = ((ThingWithComps)p).GetComp<CompHauledToInventory>();
		if (comp == null)
		{
			return false;
		}
		if (!overrideOptOut && !MasterEnable.Active)
		{
			return false;
		}
		if (!overrideOptOut && !FitToStartHaul(p))
		{
			return false;
		}
		if (!overrideOptOut)
		{
			return comp.autoHaulYields;
		}
		return true;
	}

	public static bool IsRaceEligible(Pawn p)
	{
		if (((p != null) ? p.RaceProps : null) == null)
		{
			return false;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return false;
		}
		if (EligibilityPolicy.IsEligible(p.RaceProps.IsMechanoid, p.RaceProps.Humanlike, isDrafted: false, incapableOfHauling: false, settings.allowMechanoids, settings.pauseWhileDrafted, settings.allowIncapable, settings.allowAnimals))
		{
			return CanScoopAsAnimal(p);
		}
		return false;
	}

	public static bool IsEligible(Pawn p)
	{
		if (((p != null) ? p.RaceProps : null) == null)
		{
			return false;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return false;
		}
		if (PawnUnloadChecker.InDirectedActivity(p))
		{
			return false;
		}
		if (EligibilityPolicy.IsEligible(p.RaceProps.IsMechanoid, p.RaceProps.Humanlike, p.Drafted, WorkCapabilityProbe.IsDisabled(p, WorkTypeDefOf.Hauling), settings.allowMechanoids, settings.pauseWhileDrafted, settings.allowIncapable, settings.allowAnimals))
		{
			return CanScoopAsAnimal(p);
		}
		return false;
	}

	private static TrainableDef HaulTrainable()
	{
		if (!haulTrainableResolved)
		{
			haulTrainable = DefDatabase<TrainableDef>.GetNamedSilentFail("Haul");
			haulTrainableResolved = true;
		}
		return haulTrainable;
	}

	private static bool CanScoopAsAnimal(Pawn p)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (p.RaceProps.IsMechanoid || p.RaceProps.Humanlike)
		{
			return true;
		}
		Pawn_TrainingTracker training = p.training;
		TrainableDef val = HaulTrainable();
		if (training == null || val == null)
		{
			return true;
		}
		AcceptanceReport val2 = training.CanAssignToTrain(val);
		if (!((AcceptanceReport)(ref val2)).Accepted)
		{
			return training.HasLearned(val);
		}
		return true;
	}
}
