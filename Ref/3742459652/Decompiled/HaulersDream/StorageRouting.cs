using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HaulersDream.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class StorageRouting
{
	internal sealed class RelocationInfo
	{
		internal readonly IntVec3 consumeCell;

		internal RelocationInfo(IntVec3 consumeCell)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			this.consumeCell = consumeCell;
		}
	}

	private static readonly ConditionalWeakTable<Job, RelocationInfo> relocationJobs = new ConditionalWeakTable<Job, RelocationInfo>();

	internal static void MarkRelocation(Job job, IntVec3 consumeCell)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		if (job != null)
		{
			relocationJobs.GetValue(job, (Job _) => new RelocationInfo(consumeCell));
		}
	}

	internal static bool IsRelocation(Job job)
	{
		RelocationInfo value;
		if (job != null)
		{
			return relocationJobs.TryGetValue(job, out value);
		}
		return false;
	}

	internal static RelocationInfo RelocationData(Job job)
	{
		if (job == null || !relocationJobs.TryGetValue(job, out var value))
		{
			return null;
		}
		return value;
	}

	internal static Job TryRouteToConsumer(Pawn pawn, ThingDef material, IntVec3 consumeCell, IReadOnlyList<LocalTargetInfo> plannedStacks, bool allowEqualPriority, bool allowStockpiles)
	{
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.storageRouting)
		{
			return null;
		}
		if (!MasterEnable.Active)
		{
			return null;
		}
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || !((Thing)pawn).Spawned || !((IntVec3)(ref consumeCell)).IsValid)
		{
			return null;
		}
		Thing val = PickLargestRoutableStack(pawn, plannedStacks);
		if (val == null)
		{
			return null;
		}
		if (val.ParentHolder is Pawn_InventoryTracker)
		{
			return null;
		}
		if (MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, val, 2))
		{
			return null;
		}
		IntVec3 val2 = FindBeforeCarryStoreCell(pawn, val, consumeCell, allowEqualPriority, allowStockpiles);
		if (!((IntVec3)(ref val2)).IsValid)
		{
			return null;
		}
		IntVec3 val3 = val.Position - consumeCell;
		int lengthHorizontalSquared = ((IntVec3)(ref val3)).LengthHorizontalSquared;
		val3 = val2 - consumeCell;
		int lengthHorizontalSquared2 = ((IntVec3)(ref val3)).LengthHorizontalSquared;
		if (!StorageRoutingPolicy.WorthRelocating(lengthHorizontalSquared, lengthHorizontalSquared2))
		{
			return null;
		}
		Job val4 = HaulAIUtility.HaulToCellStorageJob(pawn, val, val2, false);
		if (val4 == null)
		{
			return null;
		}
		MarkRelocation(val4, consumeCell);
		HDLog.Dbg($"Routing: {pawn} relocates {((Def)(val.def?)).label} (x{val.stackCount}) closer to {consumeCell} " + $"before carrying (from {lengthHorizontalSquared} -> {lengthHorizontalSquared2} sq to target).");
		return val4;
	}

	private static Thing PickLargestRoutableStack(Pawn pawn, IReadOnlyList<LocalTargetInfo> planned)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		if (planned == null || planned.Count == 0)
		{
			return null;
		}
		HashSet<Thing> hashSet = RouteSelection.ClaimedByOtherPawns(pawn);
		Thing result = null;
		int num = 0;
		for (int i = 0; i < planned.Count; i++)
		{
			LocalTargetInfo val = planned[i];
			Thing thing = ((LocalTargetInfo)(ref val)).Thing;
			if (thing != null && thing.Spawned && thing.Map == ((Thing)pawn).Map && thing.def != null && thing.def.EverHaulable && !(thing is Corpse) && thing.stackCount > num && !ForbidUtility.IsForbidden(thing, pawn) && !hashSet.Contains(thing) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(thing), 1, -1, (ReservationLayerDef)null, false) && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, false) && ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(thing), (PathEndMode)3))
			{
				result = thing;
				num = thing.stackCount;
			}
		}
		return result;
	}

	private static IntVec3 FindBeforeCarryStoreCell(Pawn pawn, Thing thing, IntVec3 consumeCell, bool allowEqualPriority, bool allowStockpiles)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Expected I4, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Expected I4, but got Unknown
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		Map map = ((Thing)pawn).Map;
		int num = (int)StoreUtility.CurrentStoragePriorityOf(thing, false);
		IntVec3 val = (thing.SpawnedOrAnyParentSpawned ? thing.PositionHeld : ((Thing)pawn).PositionHeld);
		EnRoutePickupPolicy.Midway(val.x, val.y, val.z, consumeCell.x, consumeCell.y, consumeCell.z, out var midX, out var _, out var midZ);
		SlotGroup val2 = (((IntVec3)(ref val)).IsValid ? map.haulDestinationManager.SlotGroupAt(val) : null);
		using (StorageBuildingFilter.PushContext(StorageFilterContext.BeforeCarry))
		{
			StorageBuildingFilter storageBuildingFilter = ((!StorageBuildingFilter.Enabled || StorageBuildingFilter.CurrentContext == StorageFilterContext.Unload) ? null : HaulersDreamMod.Settings?.storageBuildingFilter);
			IntVec3 result = IntVec3.Invalid;
			int bestMidwayDistSquared = int.MaxValue;
			List<SlotGroup> allGroupsListInPriorityOrder = map.haulDestinationManager.AllGroupsListInPriorityOrder;
			for (int i = 0; i < allGroupsListInPriorityOrder.Count; i++)
			{
				SlotGroup val3 = allGroupsListInPriorityOrder[i];
				StoragePriority priority = val3.Settings.Priority;
				if ((int)priority == 0)
				{
					break;
				}
				int num2 = (int)priority;
				if (num2 < num || (num2 == num && !allowEqualPriority))
				{
					break;
				}
				bool isOwnGroup = val2 != null && val3 == val2;
				if (!StorageRoutingPolicy.PriorityEligibleForRoute(num2, num, beforeCarryActive: true, allowEqualPriority, isOwnGroup))
				{
					continue;
				}
				ISlotGroupParent parent = val3.parent;
				ISlotGroupParent obj = ((parent is Zone_Stockpile) ? parent : null);
				ISlotGroupParent parent2 = val3.parent;
				Building_Storage val4 = (Building_Storage)(object)((parent2 is Building_Storage) ? parent2 : null);
				if ((obj != null && !allowStockpiles) || (val4 != null && storageBuildingFilter != null && !storageBuildingFilter.IsGroupAllowed(val3)) || !((IHaulDestination)val3.parent).Accepts(thing))
				{
					continue;
				}
				List<IntVec3> cellsList = val3.CellsList;
				if (cellsList == null)
				{
					continue;
				}
				for (int j = 0; j < cellsList.Count; j++)
				{
					IntVec3 val5 = cellsList[j];
					int num3 = EnRoutePickupPolicy.MidwayDistanceSquared(val5.x, val5.z, midX, midZ);
					if (StorageRoutingPolicy.MidwayBetter(num3, bestMidwayDistSquared) && StoreUtility.IsGoodStoreCell(val5, map, thing, pawn, ((Thing)pawn).Faction) && (storageBuildingFilter == null || storageBuildingFilter.IsCellAllowed(val5, map)))
					{
						result = val5;
						bestMidwayDistSquared = num3;
					}
				}
			}
			return result;
		}
	}

	internal static bool MayRoute(Pawn pawn)
	{
		if (pawn == null || ((Thing)pawn).Faction != Faction.OfPlayerSilentFail || QuestUtility.IsQuestLodger(pawn))
		{
			return false;
		}
		if (!YieldRouter.IsEligible(pawn))
		{
			return false;
		}
		if (CaravanFormingUtility.IsFormingCaravan(pawn))
		{
			return false;
		}
		return ((ThingWithComps)pawn).GetComp<CompHauledToInventory>()?.autoHaulYields ?? false;
	}
}
