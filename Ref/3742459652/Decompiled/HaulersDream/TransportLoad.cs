using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class TransportLoad
{
	private sealed class VariantBudget
	{
		private readonly List<TransferableOneWay> wanted;

		private readonly Dictionary<TransferableOneWay, int> left;

		public VariantBudget(List<TransferableOneWay> wanted)
		{
			this.wanted = wanted ?? new List<TransferableOneWay>();
			left = new Dictionary<TransferableOneWay, int>(this.wanted.Count);
			for (int i = 0; i < this.wanted.Count; i++)
			{
				TransferableOneWay val = this.wanted[i];
				if (val != null && ((Transferable)val).HasAnyThing && ((Transferable)val).CountToTransfer > 0)
				{
					left[val] = ((Transferable)val).CountToTransfer;
				}
			}
		}

		private TransferableOneWay Match(Thing thing)
		{
			if (thing == null)
			{
				return null;
			}
			for (int i = 0; i < wanted.Count; i++)
			{
				TransferableOneWay val = wanted[i];
				if (val != null && left.TryGetValue(val, out var value) && value > 0 && TransferableUtility.TransferAsOne(thing, ((Transferable)val).AnyThing, (TransferAsOneMode)1))
				{
					return val;
				}
			}
			return null;
		}

		public int Cap(Thing thing, int desired)
		{
			if (desired <= 0)
			{
				return 0;
			}
			TransferableOneWay val = Match(thing);
			if (val == null)
			{
				return 0;
			}
			int num = left[val];
			if (desired >= num)
			{
				return num;
			}
			return desired;
		}

		public void Commit(Thing thing, int take)
		{
			if (take > 0)
			{
				TransferableOneWay val = Match(thing);
				if (val != null)
				{
					left[val] = Math.Max(0, left[val] - take);
				}
			}
		}
	}

	private const float MinSearchRadius = 12f;

	private const int MaxStacks = 24;

	private const float PoolRadiusHops = 4f;

	[ThreadStatic]
	private static List<Thing> scratchThings;

	[ThreadStatic]
	private static List<int> scratchCounts;

	[ThreadStatic]
	private static List<ThingDef> scratchNetworkDefs;

	[ThreadStatic]
	private static int workCacheTick;

	[ThreadStatic]
	private static Dictionary<long, bool> workCache;

	[ThreadStatic]
	private static int menuProbeTick;

	[ThreadStatic]
	private static Dictionary<long, bool> menuProbeCache;

	private static readonly Comparison<Thing> ByThingId;

	[ThreadStatic]
	private static Dictionary<ThingDef, int> scratchShareLeft;

	[ThreadStatic]
	private static List<Thing> hybridCands;

	[ThreadStatic]
	private static List<int> hybridTakes;

	static TransportLoad()
	{
		ByThingId = (Thing a, Thing b) => (a?.thingIDNumber ?? int.MinValue).CompareTo(b?.thingIDNumber ?? int.MinValue);
		CacheRegistry.Register(ClearLoadWorkCache);
	}

	public static void ClearLoadWorkCache()
	{
		workCache?.Clear();
		workCacheTick = -1;
		menuProbeCache?.Clear();
		menuProbeTick = -1;
	}

	public static bool WouldGiveBulkJobForMenu(Pawn pawn, IManagedLoadable loadable)
	{
		if (pawn == null || loadable == null)
		{
			return false;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : (-1));
		if (num == -1)
		{
			return TryGiveBulkJob(pawn, loadable, playerOrder: true) != null;
		}
		Dictionary<long, bool> dictionary = menuProbeCache ?? (menuProbeCache = new Dictionary<long, bool>());
		if (num != menuProbeTick)
		{
			dictionary.Clear();
			menuProbeTick = num;
		}
		long key = ((long)((Thing)pawn).thingIDNumber << 32) | (uint)loadable.GetUniqueLoadID();
		if (dictionary.TryGetValue(key, out var value))
		{
			return value;
		}
		return dictionary[key] = TryGiveBulkJob(pawn, loadable, playerOrder: true) != null;
	}

	public static bool HasPotentialBulkWork(Pawn pawn, IManagedLoadable loadable)
	{
		return HasPotentialBulkWork(pawn, loadable, FeatureEnabled(loadable));
	}

	private static bool IsBoardingPassengerFor(Pawn pawn, IManagedLoadable loadable)
	{
		PawnDuty val = pawn?.mindState?.duty;
		if (val == null || loadable == null)
		{
			return false;
		}
		if (loadable.Kind == LoadableKind.Transporter && val.def == DutyDefOf.LoadAndEnterTransporters)
		{
			if (val.transportersGroup >= 0)
			{
				return val.transportersGroup == loadable.GetUniqueLoadID();
			}
			return false;
		}
		if (loadable.Kind == LoadableKind.Portal && val.def == DutyDefOf.LoadAndEnterPortal)
		{
			if (((LocalTargetInfo)(ref val.focus)).Thing != null)
			{
				return ((LocalTargetInfo)(ref val.focus)).Thing == loadable.GetParentThing();
			}
			return false;
		}
		return false;
	}

	private static bool HoldsCargoLoadableWants(Pawn pawn, IManagedLoadable loadable)
	{
		CompHauledToInventory compHauledToInventory = ((pawn != null) ? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() : null);
		ThingOwner<Thing> val = pawn?.inventory?.innerContainer;
		if (compHauledToInventory == null || val == null || loadable == null)
		{
			return false;
		}
		List<TransferableOneWay> transferables = loadable.GetTransferables();
		if (transferables == null || transferables.Count == 0)
		{
			return false;
		}
		foreach (Thing item in compHauledToInventory.PeekHashSet())
		{
			if (item != null && !item.Destroyed && item.def != null && ((ThingOwner)val).Contains(item) && InventorySurplus.SurplusOf(pawn, item) > 0)
			{
				TransferableOneWay val2 = TransferableUtility.TransferableMatchingDesperate(item, transferables, (TransferAsOneMode)1);
				if (val2 != null && ((Transferable)val2).CountToTransfer > 0)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static float StrandedSurplusMass(Pawn pawn, IManagedLoadable loadable)
	{
		CompHauledToInventory compHauledToInventory = ((pawn != null) ? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() : null);
		ThingOwner<Thing> val = pawn?.inventory?.innerContainer;
		if (compHauledToInventory == null || val == null || loadable == null)
		{
			return 0f;
		}
		List<TransferableOneWay> transferables = loadable.GetTransferables();
		if (transferables == null || transferables.Count == 0)
		{
			return 0f;
		}
		float num = 0f;
		foreach (Thing item in compHauledToInventory.PeekHashSet())
		{
			if (item == null || item.Destroyed || item.def == null || !((ThingOwner)val).Contains(item))
			{
				continue;
			}
			int num2 = InventorySurplus.SurplusOf(pawn, item);
			if (num2 > 0)
			{
				TransferableOneWay val2 = TransferableUtility.TransferableMatchingDesperate(item, transferables, (TransferAsOneMode)1);
				if (val2 == null || ((Transferable)val2).CountToTransfer <= 0)
				{
					num += (float)num2 * StatExtension.GetStatValue(item, StatDefOf.Mass, true, -1);
				}
			}
		}
		return num;
	}

	private static bool HasPotentialBulkWork(Pawn pawn, IManagedLoadable loadable, bool featureEnabled)
	{
		if (!featureEnabled || loadable == null)
		{
			return false;
		}
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || pawn.Drafted)
		{
			return false;
		}
		if (((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null)
		{
			return false;
		}
		if (!IsBoardingPassengerFor(pawn, loadable) && !YieldRouter.IsEligible(pawn))
		{
			return false;
		}
		HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
		if (instance == null)
		{
			return false;
		}
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : (-1));
		Dictionary<long, bool> dictionary = workCache ?? (workCache = new Dictionary<long, bool>());
		if (num != workCacheTick)
		{
			dictionary.Clear();
			workCacheTick = num;
		}
		long key = ((long)((Thing)pawn).thingIDNumber << 32) | (uint)loadable.GetUniqueLoadID();
		if (num != -1 && dictionary.TryGetValue(key, out var value))
		{
			return value;
		}
		if (((Thing)pawn).Faction != Faction.OfPlayerSilentFail || QuestUtility.IsQuestLodger(pawn))
		{
			if (num != -1)
			{
				dictionary[key] = false;
			}
			return false;
		}
		instance.LoadRegisterOrUpdate(loadable);
		bool flag = instance.LoadHasWork(loadable, pawn);
		if (num != -1)
		{
			dictionary[key] = flag;
		}
		return flag;
	}

	public static bool HasPotentialBulkWorkPortal(Pawn pawn, IManagedLoadable loadable)
	{
		return HasPotentialBulkWork(pawn, loadable, HaulersDreamMod.Settings?.enableBulkLoadPortal ?? false);
	}

	private static bool FeatureEnabled(IManagedLoadable loadable)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || loadable == null)
		{
			return false;
		}
		switch (loadable.Kind)
		{
		case LoadableKind.Portal:
			return settings.enableBulkLoadPortal;
		case LoadableKind.Vehicle:
			if (settings.enableVehicleFramework)
			{
				return settings.enableBulkLoadVehicles;
			}
			return false;
		default:
			return settings.enableBulkLoadTransporters;
		}
	}

	public static Job TryGiveBulkJob(Pawn pawn, IManagedLoadable loadable, bool playerOrder = false)
	{
		return TryGiveBulkJob(pawn, loadable, JobDefFor(loadable), FeatureEnabled(loadable), playerOrder);
	}

	private static JobDef JobDefFor(IManagedLoadable loadable)
	{
		if (loadable != null)
		{
			switch (loadable.Kind)
			{
			case LoadableKind.Portal:
				return HaulersDreamDefOf.HaulersDream_LoadPortalInBulk;
			case LoadableKind.Vehicle:
				return HaulersDreamDefOf.HaulersDream_LoadVehicleInBulk;
			}
		}
		return HaulersDreamDefOf.HaulersDream_LoadTransportersInBulk;
	}

	private static Job TryGiveBulkJob(Pawn pawn, IManagedLoadable loadable, JobDef jobDef, bool featureEnabled, bool playerOrder)
	{
		//IL_0203: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_0337: Unknown result type (might be due to invalid IL or missing references)
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		Map val = ((pawn != null) ? ((Thing)pawn).Map : null);
		if (settings == null || !featureEnabled || val == null || loadable == null)
		{
			return null;
		}
		if (((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null)
		{
			return null;
		}
		if (!playerOrder && !IsBoardingPassengerFor(pawn, loadable) && !YieldRouter.IsEligible(pawn))
		{
			return null;
		}
		if (((Thing)pawn).Faction != Faction.OfPlayerSilentFail || QuestUtility.IsQuestLodger(pawn))
		{
			return null;
		}
		HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
		if (instance == null)
		{
			return null;
		}
		LoadLedgerEntry entry = instance.LoadRegisterOrUpdate(loadable);
		Dictionary<ThingDef, int> dictionary = instance.LoadAvailableToClaim(loadable, pawn);
		if (dictionary.Count == 0)
		{
			return null;
		}
		Dictionary<ThingDef, int> dictionary2 = new Dictionary<ThingDef, int>(dictionary);
		VariantBudget variantBudget = new VariantBudget(loadable.GetTransferables());
		float num = CarryMath.EffectiveCapacity(CarryCapacity.Of(pawn), settings.carryLimitFraction);
		float num2 = BulkHaulPolicy.CeilingKg(settings.overloadLevel, OverloadGate.NoOverloadFor(pawn, settings), num, OverloadGate.MaxCeilingKg(settings));
		float num3 = MassUtility.GearAndInventoryMass(pawn);
		float num4 = StrandedSurplusMass(pawn, loadable);
		float running = Math.Max(0f, num3 - num4);
		float bulkRoom = CECompat.AvailableBulk(pawn);
		float num5;
		if (float.IsPositiveInfinity(num2))
		{
			num5 = float.MaxValue;
		}
		else
		{
			num5 = Math.Max(0f, num2 - running);
			float num6 = Math.Max(0f, num2 - num3);
			if (num5 > num6)
			{
				num5 = num6;
			}
		}
		float massLeft = TransportLoadPlan.TripMassBudget(num5, loadable.GetMassCapacity(), loadable.GetMassUsage(), loadable.HasMassCap);
		List<Thing> list = BulkHaul.BuildPool(pawn, loadable.GetParentThing(), val, 48f);
		AddStoredClaimables(list, dictionary, val);
		HashSet<Thing> claimedByOthers = RouteSelection.ClaimedByOtherPawns(pawn);
		if (!playerOrder)
		{
			int num7 = CountClaimlessCoLoaders(pawn, loadable, entry);
			if (num7 > 0)
			{
				list.Sort(ByThingId);
				float heaviestUnitMass;
				float num8 = LoadFairShare.ShareMassBudget(ClaimablePoolMass(pawn, list, dictionary, claimedByOthers, out heaviestUnitMass), askerTripBudgetKg: LoadFairShare.AskerTripBudgetKg(massLeft, num), heaviestUnitMassKg: heaviestUnitMass, loaderCount: 1 + num7);
				if (num8 < massLeft)
				{
					massLeft = num8;
				}
			}
		}
		List<Thing> list2 = scratchThings ?? (scratchThings = new List<Thing>());
		List<int> list3 = scratchCounts ?? (scratchCounts = new List<int>());
		list2.Clear();
		list3.Clear();
		IntVec3 position = ((Thing)pawn).Position;
		while (list2.Count < 24 && running < num2 - 0.0001f && massLeft > 0.0001f)
		{
			int take;
			Thing val2 = NearestEligible(pawn, list, position, claimedByOthers, dictionary2, variantBudget, num2, running, bulkRoom, massLeft, out take);
			if (val2 == null)
			{
				break;
			}
			list2.Add(val2);
			list3.Add(take);
			float statValue = StatExtension.GetStatValue(val2, StatDefOf.Mass, true, -1);
			running += (float)take * statValue;
			bulkRoom -= (float)take * CECompat.BulkPerUnit(val2);
			massLeft -= (float)take * statValue;
			dictionary2[val2.def] = Math.Max(0, (dictionary2.TryGetValue(val2.def, out var value) ? value : 0) - take);
			variantBudget.Commit(val2, take);
			position = val2.Position;
		}
		if (settings.enableStorageNetworkBulkLoad && StorageNetworkCompat.IsActive)
		{
			AppendNetworkClaimables(pawn, val, dictionary2, variantBudget, claimedByOthers, list2, list3, ref running, num2, ref bulkRoom, ref massLeft);
		}
		if (list2.Count == 0)
		{
			if (!playerOrder && IsBoardingPassengerFor(pawn, loadable) && HoldsCargoLoadableWants(pawn, loadable))
			{
				return Patch_OpportunisticLoadDeposit.BuildDepositOnlyJob(pawn, loadable);
			}
			return null;
		}
		Job val3 = JobMaker.MakeJob(jobDef, LocalTargetInfo.op_Implicit(loadable.GetParentThing()));
		val3.targetQueueB = new List<LocalTargetInfo>(list2.Count);
		for (int i = 0; i < list2.Count; i++)
		{
			val3.targetQueueB.Add(LocalTargetInfo.op_Implicit(list2[i]));
		}
		val3.countQueue = new List<int>(list3);
		val3.count = 1;
		HDLog.Dbg($"TransportLoad: {pawn} sweeping {list2.Count} stacks for group {loadable.GetUniqueLoadID()} (~{running:0.#}kg).");
		return val3;
	}

	private static int CountClaimlessCoLoaders(Pawn asker, IManagedLoadable loadable, LoadLedgerEntry entry)
	{
		Map val = ((asker != null) ? ((Thing)asker).Map : null);
		if (val?.mapPawns == null || loadable == null)
		{
			return 0;
		}
		Dictionary<Pawn, Dictionary<ThingDef, int>> dictionary = entry?.pawnClaims;
		IReadOnlyList<Pawn> allPawnsSpawned = val.mapPawns.AllPawnsSpawned;
		int num = 0;
		for (int i = 0; i < allPawnsSpawned.Count; i++)
		{
			Pawn val2 = allPawnsSpawned[i];
			if (val2 != null && val2 != asker && (dictionary == null || !dictionary.ContainsKey(val2)) && IsBoardingPassengerFor(val2, loadable) && LoadFairShare.CountsAsCoLoader(isBoardingPassengerOfThisLoadable: true, !WorkCapabilityProbe.IsDisabled(val2, WorkTypeDefOf.Hauling), HasPotentialBulkWork(val2, loadable), val2.Downed, val2.Drafted, val2.InMentalState, val2.health?.capacities != null && val2.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation), ((ThingWithComps)val2).GetComp<CompHauledToInventory>() != null && val2.inventory != null))
			{
				num++;
			}
		}
		return num;
	}

	private static float ClaimablePoolMass(Pawn pawn, List<Thing> pool, Dictionary<ThingDef, int> claimable, HashSet<Thing> claimedByOthers, out float heaviestUnitMass)
	{
		heaviestUnitMass = 0f;
		Dictionary<ThingDef, int> dictionary = scratchShareLeft ?? (scratchShareLeft = new Dictionary<ThingDef, int>());
		dictionary.Clear();
		foreach (KeyValuePair<ThingDef, int> item in claimable)
		{
			if (item.Key != null && item.Value > 0)
			{
				dictionary[item.Key] = item.Value;
			}
		}
		if (dictionary.Count == 0)
		{
			return 0f;
		}
		float num = 0f;
		for (int i = 0; i < pool.Count; i++)
		{
			Thing val = pool[i];
			if (val?.def == null || !dictionary.TryGetValue(val.def, out var value) || value <= 0 || claimedByOthers.Contains(val) || ForbidUtility.IsForbidden(val, pawn))
			{
				continue;
			}
			int num2 = Math.Min(val.stackCount, value);
			if (num2 > 0)
			{
				float statValue = StatExtension.GetStatValue(val, StatDefOf.Mass, true, -1);
				num += (float)num2 * statValue;
				dictionary[val.def] = value - num2;
				if (statValue > heaviestUnitMass)
				{
					heaviestUnitMass = statValue;
				}
			}
		}
		dictionary.Clear();
		return num;
	}

	private static void AddStoredClaimables(List<Thing> pool, Dictionary<ThingDef, int> claimable, Map map)
	{
		if (pool == null || claimable == null || map == null)
		{
			return;
		}
		HashSet<Thing> hashSet = new HashSet<Thing>(pool);
		foreach (KeyValuePair<ThingDef, int> item in claimable)
		{
			ThingDef key = item.Key;
			if (key == null || item.Value <= 0)
			{
				continue;
			}
			List<Thing> list = map.listerThings.ThingsOfDef(key);
			for (int i = 0; i < list.Count; i++)
			{
				Thing val = list[i];
				if (val != null && val.Spawned && val.Map == map && !(val is Corpse) && val.def.EverHaulable && !(val.ParentHolder is Pawn_InventoryTracker) && hashSet.Add(val))
				{
					pool.Add(val);
				}
			}
		}
	}

	private static void AppendNetworkClaimables(Pawn pawn, Map map, Dictionary<ThingDef, int> claimLeft, VariantBudget variants, HashSet<Thing> claimedByOthers, List<Thing> things, List<int> counts, ref float running, float ceiling, ref float bulkRoom, ref float massLeft)
	{
		List<Thing> list = StorageNetworkCompat.UsableTerminals(pawn, map);
		if (list.Count == 0)
		{
			return;
		}
		List<ThingDef> list2 = scratchNetworkDefs ?? (scratchNetworkDefs = new List<ThingDef>());
		list2.Clear();
		foreach (KeyValuePair<ThingDef, int> item in claimLeft)
		{
			if (item.Key != null && item.Value > 0)
			{
				list2.Add(item.Key);
			}
		}
		if (list2.Count == 0)
		{
			return;
		}
		HashSet<Thing> hashSet = new HashSet<Thing>(things);
		for (int i = 0; i < list2.Count; i++)
		{
			if (things.Count >= 24)
			{
				break;
			}
			if (running >= ceiling - 0.0001f)
			{
				break;
			}
			if (massLeft <= 0.0001f)
			{
				break;
			}
			ThingDef val = list2[i];
			for (int j = 0; j < list.Count; j++)
			{
				if (things.Count >= 24)
				{
					break;
				}
				if (running >= ceiling - 0.0001f)
				{
					break;
				}
				if (massLeft <= 0.0001f)
				{
					break;
				}
				List<Thing> list3 = StorageNetworkCompat.NetworkStacksOfDef(list[j], val);
				for (int k = 0; k < list3.Count; k++)
				{
					int value;
					int num = (claimLeft.TryGetValue(val, out value) ? value : 0);
					if (num <= 0 || things.Count >= 24 || running >= ceiling - 0.0001f || massLeft <= 0.0001f)
					{
						break;
					}
					Thing val2 = list3[k];
					if (val2 != null && val2.def == val && !val2.Destroyed && hashSet.Add(val2) && !claimedByOthers.Contains(val2))
					{
						int num2 = ClampNetworkTake(pawn, val2, num, variants, ceiling, running, bulkRoom, massLeft);
						if (num2 > 0)
						{
							things.Add(val2);
							counts.Add(num2);
							float statValue = StatExtension.GetStatValue(val2, StatDefOf.Mass, true, -1);
							running += (float)num2 * statValue;
							bulkRoom -= (float)num2 * CECompat.BulkPerUnit(val2);
							massLeft -= (float)num2 * statValue;
							claimLeft[val] = Math.Max(0, num - num2);
							variants.Commit(val2, num2);
						}
					}
				}
			}
		}
	}

	private static int ClampNetworkTake(Pawn pawn, Thing chosen, int claimAvail, VariantBudget variants, float ceiling, float running, float bulkRoom, float massLeft)
	{
		float statValue = StatExtension.GetStatValue(chosen, StatDefOf.Mass, true, -1);
		int val = BulkHaulPolicy.CountWithinCeiling(ceiling, running, statValue, chosen.stackCount);
		val = Math.Min(val, CECompat.MaxFitCount(pawn, chosen));
		float num = CECompat.BulkPerUnit(chosen);
		if (num > 0f && !float.IsPositiveInfinity(bulkRoom))
		{
			val = Math.Min(val, (int)Math.Floor(bulkRoom / num));
		}
		int val2 = TransportLoadPlan.UnitsWithinMassBudget(massLeft, statValue, chosen.stackCount);
		int desired = TransportLoadPlan.DeliverableUnits(chosen.stackCount, claimAvail, claimAvail, Math.Min(val, val2));
		return variants.Cap(chosen, desired);
	}

	private static Thing NearestEligible(Pawn pawn, List<Thing> pool, IntVec3 from, HashSet<Thing> claimedByOthers, Dictionary<ThingDef, int> claimLeft, VariantBudget variants, float ceiling, float running, float bulkRoom, float massLeft, out int take)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && settings.loadHybridPathing)
		{
			return NearestEligibleHybrid(pawn, pool, from, claimedByOthers, claimLeft, variants, ceiling, running, bulkRoom, massLeft, out take);
		}
		take = 0;
		Thing val3;
		int num4;
		do
		{
			int num = -1;
			float num2 = float.MaxValue;
			for (int i = 0; i < pool.Count; i++)
			{
				Thing val = pool[i];
				if (val?.def != null && claimLeft.TryGetValue(val.def, out var value) && value > 0)
				{
					IntVec3 val2 = val.Position - from;
					float num3 = ((IntVec3)(ref val2)).LengthHorizontalSquared;
					if (num3 < num2 || (num3 == num2 && num >= 0 && val.thingIDNumber < pool[num].thingIDNumber))
					{
						num2 = num3;
						num = i;
					}
				}
			}
			if (num < 0)
			{
				return null;
			}
			val3 = pool[num];
			pool.RemoveAt(num);
			num4 = TryQualify(pawn, val3, claimedByOthers, claimLeft, variants, ceiling, running, bulkRoom, massLeft);
		}
		while (num4 <= 0);
		take = num4;
		return val3;
	}

	private static int HybridCandidateBudget()
	{
		int num = HaulersDreamMod.Settings?.loadPathfindingCandidates ?? 8;
		if (num >= 2)
		{
			if (num <= 24)
			{
				return num;
			}
			return 24;
		}
		return 2;
	}

	private static Thing NearestEligibleHybrid(Pawn pawn, List<Thing> pool, IntVec3 from, HashSet<Thing> claimedByOthers, Dictionary<ThingDef, int> claimLeft, VariantBudget variants, float ceiling, float running, float bulkRoom, float massLeft, out int take)
	{
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		take = 0;
		int num = HybridCandidateBudget();
		List<Thing> list = hybridCands ?? (hybridCands = new List<Thing>());
		List<int> list2 = hybridTakes ?? (hybridTakes = new List<int>());
		list.Clear();
		list2.Clear();
		IntVec3 val2;
		while (list.Count < num)
		{
			int num2 = -1;
			float num3 = float.MaxValue;
			for (int i = 0; i < pool.Count; i++)
			{
				Thing val = pool[i];
				if (val?.def != null && claimLeft.TryGetValue(val.def, out var value) && value > 0)
				{
					val2 = val.Position - from;
					float num4 = ((IntVec3)(ref val2)).LengthHorizontalSquared;
					if (num4 < num3 || (num4 == num3 && num2 >= 0 && val.thingIDNumber < pool[num2].thingIDNumber))
					{
						num3 = num4;
						num2 = i;
					}
				}
			}
			if (num2 < 0)
			{
				break;
			}
			Thing val3 = pool[num2];
			pool.RemoveAt(num2);
			int num5 = TryQualify(pawn, val3, claimedByOthers, claimLeft, variants, ceiling, running, bulkRoom, massLeft);
			if (num5 > 0)
			{
				list.Add(val3);
				list2.Add(num5);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		if (list.Count == 1)
		{
			take = list2[0];
			return list[0];
		}
		Map map = ((Thing)pawn).Map;
		int num6 = 0;
		float num7 = float.MaxValue;
		for (int j = 0; j < list.Count; j++)
		{
			Thing val4 = list[j];
			float num8 = PathCostTo(map, pawn, from, val4);
			if (num8 < 0f)
			{
				val2 = val4.Position - from;
				num8 = float.MaxValue - (float)((IntVec3)(ref val2)).LengthHorizontalSquared;
			}
			if (num8 < num7 || (num8 == num7 && val4.thingIDNumber < list[num6].thingIDNumber))
			{
				num7 = num8;
				num6 = j;
			}
		}
		Thing result = list[num6];
		take = list2[num6];
		for (int k = 0; k < list.Count; k++)
		{
			if (k != num6)
			{
				pool.Add(list[k]);
			}
		}
		list.Clear();
		list2.Clear();
		return result;
	}

	private static int TryQualify(Pawn pawn, Thing chosen, HashSet<Thing> claimedByOthers, Dictionary<ThingDef, int> claimLeft, VariantBudget variants, float ceiling, float running, float bulkRoom, float massLeft)
	{
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		if (ForbidUtility.IsForbidden(chosen, pawn) || claimedByOthers.Contains(chosen))
		{
			return 0;
		}
		float statValue = StatExtension.GetStatValue(chosen, StatDefOf.Mass, true, -1);
		int val = BulkHaulPolicy.CountWithinCeiling(ceiling, running, statValue, chosen.stackCount);
		val = Math.Min(val, CECompat.MaxFitCount(pawn, chosen));
		float num = CECompat.BulkPerUnit(chosen);
		if (num > 0f && !float.IsPositiveInfinity(bulkRoom))
		{
			val = Math.Min(val, (int)Math.Floor(bulkRoom / num));
		}
		int val2 = TransportLoadPlan.UnitsWithinMassBudget(massLeft, statValue, chosen.stackCount);
		int value;
		int num2 = (claimLeft.TryGetValue(chosen.def, out value) ? value : 0);
		int num3 = TransportLoadPlan.DeliverableUnits(chosen.stackCount, num2, num2, Math.Min(val, val2));
		if (num3 <= 0)
		{
			return 0;
		}
		num3 = variants.Cap(chosen, num3);
		if (num3 <= 0)
		{
			return 0;
		}
		if (!HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, chosen, false))
		{
			return 0;
		}
		if (!ExtraSweepReach.Allows(pawn, LocalTargetInfo.op_Implicit(chosen), (PathEndMode)3))
		{
			return 0;
		}
		return num3;
	}

	private static float PathCostTo(Map map, Pawn pawn, IntVec3 start, Thing destThing)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (map?.pathFinder == null)
		{
			return -1f;
		}
		PawnPath val = map.pathFinder.FindPathNow(start, LocalTargetInfo.op_Implicit(destThing), pawn, (PathFinderCostTuning?)null, (PathEndMode)3);
		try
		{
			return (val != null && val.Found) ? val.TotalCost : (-1f);
		}
		finally
		{
			if (val != null)
			{
				val.ReleaseToPool();
			}
		}
	}
}
