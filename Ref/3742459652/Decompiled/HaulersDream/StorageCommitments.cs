using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

internal static class StorageCommitments
{
	internal readonly struct GroupSpace(int emptyCells, int partialSpace, int perCellCapacity, bool unbounded, bool truncated, int observedFloor)
	{
		public readonly int EmptyCells = emptyCells;

		public readonly int PartialSpace = partialSpace;

		public readonly int PerCellCapacity = perCellCapacity;

		public readonly bool Unbounded = unbounded;

		public readonly bool Truncated = truncated;

		public readonly int ObservedFloor = observedFloor;
	}

	private const int MaxSpaceScanCells = 200;

	[ThreadStatic]
	private static bool insideSpaceScan;

	[ThreadStatic]
	private static int forcedOrderDepth;

	private static bool seamDisabled;

	private static readonly List<Pawn> InterruptBuffer;

	[ThreadStatic]
	private static List<StorageEvidence.PawnCargo> janitorCargo;

	[ThreadStatic]
	private static List<Pawn> janitorPawns;

	private static readonly Comparison<Pawn> ByThingId;

	private static readonly StorageClaimEvidence Evidence;

	[ThreadStatic]
	private static Dictionary<(Pawn pawn, ThingDef def), int> evidenceMemo;

	[ThreadStatic]
	private static int evidenceMemoTick;

	[ThreadStatic]
	private static int evidenceMemoGeneration;

	[ThreadStatic]
	private static List<StorageEvidence.PawnCargo> evidenceCargo;

	[ThreadStatic]
	private static int spaceMemoTick;

	[ThreadStatic]
	private static Dictionary<(object group, Thing thing, Pawn pawn, bool filtered), GroupSpace> spaceMemo;

	[ThreadStatic]
	private static StorageGroupBudget crossDefBudget;

	internal static bool AnyClaims => StorageClaimLedger.AnyRows(HaulersDreamGameComponent.storageClaims);

	internal static bool InsideSpaceScan => insideSpaceScan;

	internal static bool InForcedOrder => forcedOrderDepth > 0;

	private static int TickNow
	{
		get
		{
			TickManager tickManager = Find.TickManager;
			if (tickManager == null)
			{
				return -1;
			}
			return tickManager.TicksGame;
		}
	}

	internal static void PushForcedOrder()
	{
		forcedOrderDepth++;
	}

	internal static void PopForcedOrder()
	{
		if (forcedOrderDepth > 0)
		{
			forcedOrderDepth--;
		}
	}

	internal static void Disable()
	{
		seamDisabled = true;
	}

	internal static bool ActiveOn(Map map)
	{
		if (map != null && !seamDisabled && MasterEnable.Active)
		{
			return MapGate.HdActiveOnMap(map);
		}
		return false;
	}

	internal static bool GatesVanillaStorage(Map map)
	{
		if (ActiveOn(map))
		{
			return HaulersDreamMod.Settings?.haulToStack ?? false;
		}
		return false;
	}

	internal static int FreeUnitsFor(Pawn asker, ISlotGroup group, ThingDef def, Thing subject)
	{
		bool observationTruncated;
		return FreeUnitsFor(asker, group, def, subject, out observationTruncated);
	}

	internal static int FreeUnitsFor(Pawn asker, ISlotGroup group, ThingDef def, Thing subject, out bool observationTruncated)
	{
		observationTruncated = false;
		Map val = ((asker != null) ? ((Thing)asker).Map : null);
		if (val == null || group == null || def == null || subject == null)
		{
			return int.MaxValue;
		}
		int freeCapacity = RawSpaceFor(asker, group, def, subject, val, out observationTruncated);
		StorageClaimRow[] storageClaims = HaulersDreamGameComponent.storageClaims;
		bool flag = IsDelivering(asker, subject);
		int unitsEnroute = StorageClaimLedger.ClaimedByOthers(storageClaims, group, def, asker, Evidence);
		int ownUnitsEnroute = ((!flag) ? StorageClaimLedger.ClaimedByPawn(storageClaims, group, def, asker, Evidence) : 0);
		return StorageCommitPolicy.Commit(new HaulSight(((Thing)asker).thingIDNumber, TickNow, freeCapacity, unitsEnroute, ownUnitsEnroute, int.MaxValue), flag);
	}

	private static bool IsDelivering(Pawn asker, Thing subject)
	{
		if (asker == null || subject == null)
		{
			return false;
		}
		Pawn_CarryTracker carryTracker = asker.carryTracker;
		if (((carryTracker != null) ? carryTracker.CarriedThing : null) == subject)
		{
			return true;
		}
		ThingOwner<Thing> val = asker.inventory?.innerContainer;
		if (val != null && !subject.Spawned)
		{
			return (object)subject.ParentHolder == val;
		}
		return false;
	}

	internal static void Commit(Pawn pawn, ISlotGroup group, ThingDef def, int units, string path)
	{
		StorageClaimRow[] storageClaims = HaulersDreamGameComponent.storageClaims;
		StorageClaimRow[] array = StorageClaimLedger.Add(storageClaims, pawn, group, def, units);
		if (array != storageClaims)
		{
			HaulersDreamGameComponent.SetStorageClaims(array);
		}
		Trace(path, pawn, group, def, units);
	}

	internal static bool TryCommit(Pawn pawn, ISlotGroup group, ThingDef def, int units, string path)
	{
		if (pawn == null || group == null || def == null || units <= 0 || !ActiveOn(((Thing)pawn).Map))
		{
			return false;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.haulToStack)
		{
			return false;
		}
		Commit(pawn, group, def, units, path);
		return true;
	}

	internal static void DropClaim(Pawn pawn, ThingDef def)
	{
		StorageClaimRow[] storageClaims = HaulersDreamGameComponent.storageClaims;
		StorageClaimRow[] array = StorageClaimLedger.Add(storageClaims, pawn, null, def, 0);
		if (array != storageClaims)
		{
			HaulersDreamGameComponent.SetStorageClaims(array);
		}
	}

	internal static void InterruptCommittersTo(ISlotGroup group, ThingDef def, Pawn exclude)
	{
		if (group == null || def == null)
		{
			return;
		}
		StorageClaimRow[] storageClaims = HaulersDreamGameComponent.storageClaims;
		if (storageClaims.Length == 0)
		{
			return;
		}
		List<Pawn> interruptBuffer = InterruptBuffer;
		interruptBuffer.Clear();
		for (int i = 0; i < storageClaims.Length; i++)
		{
			StorageClaimRow storageClaimRow = storageClaims[i];
			if (storageClaimRow.Group == group && storageClaimRow.Def == def)
			{
				object pawn = storageClaimRow.Pawn;
				Pawn val = (Pawn)((pawn is Pawn) ? pawn : null);
				if (val != null && val != exclude)
				{
					interruptBuffer.Add(val);
				}
			}
		}
		if (interruptBuffer.Count == 0)
		{
			return;
		}
		interruptBuffer.Sort((Pawn a, Pawn b) => ((Thing)a).thingIDNumber.CompareTo(((Thing)b).thingIDNumber));
		for (int num = 0; num < interruptBuffer.Count; num++)
		{
			Pawn val2 = interruptBuffer[num];
			Job curJob = val2.CurJob;
			if (curJob != null && (curJob.def == JobDefOf.HaulToCell || curJob.def == HaulersDreamDefOf.HaulersDream_BulkHaul))
			{
				DropClaim(val2, def);
				HDLog.Dbg("storage-commit [forced] " + ((exclude != null) ? ((Entity)exclude).LabelShort : null) + " took " + ((Def)(def?)).defName + " at " + GroupLabel(group) + "; interrupting " + ((Entity)val2).LabelShort + "'s " + ((Def)curJob.def).defName + ".");
				Pawn_JobTracker jobs = val2.jobs;
				if (jobs != null)
				{
					jobs.EndCurrentJob((JobCondition)16, false, true);
				}
			}
		}
		interruptBuffer.Clear();
	}

	internal static void RunJanitor(Map map)
	{
		if (!ActiveOn(map))
		{
			return;
		}
		Faction ofPlayerSilentFail = Faction.OfPlayerSilentFail;
		if (ofPlayerSilentFail == null)
		{
			return;
		}
		StorageClaimRow[] array = StorageClaimLedger.Reconcile(HaulersDreamGameComponent.storageClaims, Evidence, GroupIsLive);
		if (array != HaulersDreamGameComponent.storageClaims)
		{
			HaulersDreamGameComponent.SetStorageClaims(array);
		}
		List<Pawn> list = janitorPawns ?? (janitorPawns = new List<Pawn>());
		list.Clear();
		List<Pawn> list2 = map.mapPawns.SpawnedPawnsInFaction(ofPlayerSilentFail);
		for (int i = 0; i < list2.Count; i++)
		{
			if (list2[i] != null)
			{
				list.Add(list2[i]);
			}
		}
		list.Sort(ByThingId);
		List<StorageEvidence.PawnCargo> list3 = janitorCargo ?? (janitorCargo = new List<StorageEvidence.PawnCargo>());
		for (int j = 0; j < list.Count; j++)
		{
			Pawn val = list[j];
			StorageEvidence.Collect(val, list3);
			if (list3.Count > 1)
			{
				list3.Sort(StorageEvidence.ByDefName);
			}
			for (int k = 0; k < list3.Count; k++)
			{
				StorageEvidence.PawnCargo pawnCargo = list3[k];
				if (pawnCargo.def != null && pawnCargo.units > 0 && !HasRowFor(val, pawnCargo.def))
				{
					ISlotGroup val2 = pawnCargo.knownGroup ?? StorageEvidence.DestinationGroupFor(map, val, pawnCargo.sample);
					if (val2 != null)
					{
						Commit(val, val2, pawnCargo.def, pawnCargo.units, "adopt");
					}
				}
			}
			list3.Clear();
		}
		list.Clear();
	}

	private static bool HasRowFor(Pawn pawn, ThingDef def)
	{
		StorageClaimRow[] storageClaims = HaulersDreamGameComponent.storageClaims;
		for (int i = 0; i < storageClaims.Length; i++)
		{
			if (storageClaims[i].Pawn == pawn && storageClaims[i].Def == def)
			{
				return true;
			}
		}
		return false;
	}

	private static bool GroupIsLive(object group)
	{
		ISlotGroup val = (ISlotGroup)((group is ISlotGroup) ? group : null);
		if (val != null && val.CellsList != null)
		{
			return val.CellsList.Count > 0;
		}
		return false;
	}

	internal static int UnitsMovingOf(Pawn pawn, ThingDef def)
	{
		return UnitsMoving(pawn, def);
	}

	private static int UnitsMoving(Pawn pawn, ThingDef def)
	{
		if (pawn == null || def == null)
		{
			return 0;
		}
		int tickNow = TickNow;
		Dictionary<(Pawn, ThingDef), int> dictionary = evidenceMemo ?? (evidenceMemo = new Dictionary<(Pawn, ThingDef), int>());
		int value;
		if (tickNow != evidenceMemoTick || evidenceMemoGeneration != HaulersDreamGameComponent.storageClaimGeneration)
		{
			dictionary.Clear();
			evidenceMemoTick = tickNow;
			evidenceMemoGeneration = HaulersDreamGameComponent.storageClaimGeneration;
		}
		else if (tickNow != -1 && dictionary.TryGetValue((pawn, def), out value))
		{
			return value;
		}
		int num = StorageEvidence.UnitsOf(pawn, def, evidenceCargo ?? (evidenceCargo = new List<StorageEvidence.PawnCargo>()));
		if (tickNow != -1)
		{
			dictionary[(pawn, def)] = num;
		}
		return num;
	}

	private static int EnoughFor(ThingDef def)
	{
		long num = 24L * (long)Math.Max(1, def?.stackLimit ?? 1);
		if (num < int.MaxValue)
		{
			return (int)num;
		}
		return 2147483646;
	}

	internal static StorageBuildingFilter ActiveFilter()
	{
		if (!StorageBuildingFilter.Enabled || StorageBuildingFilter.CurrentContext == StorageFilterContext.Unload)
		{
			return null;
		}
		return HaulersDreamMod.Settings?.storageBuildingFilter;
	}

	static StorageCommitments()
	{
		InterruptBuffer = new List<Pawn>();
		ByThingId = (Pawn a, Pawn b) => ((Thing)a).thingIDNumber.CompareTo(((Thing)b).thingIDNumber);
		Evidence = (object pawn, object def) => UnitsMoving((Pawn)((pawn is Pawn) ? pawn : null), (ThingDef)((def is ThingDef) ? def : null));
		CacheRegistry.Register(ClearCaches);
	}

	private static void ClearCaches()
	{
		spaceMemo?.Clear();
		spaceMemoTick = -1;
		evidenceMemo?.Clear();
		evidenceMemoTick = -1;
		evidenceCargo?.Clear();
		janitorCargo?.Clear();
		janitorPawns?.Clear();
		HaulersDreamGameComponent.ClearStorageClaims();
	}

	private static int RawSpaceFor(Pawn asker, ISlotGroup group, ThingDef def, Thing subject, Map map, out bool truncated)
	{
		GroupSpace groupSpace = MeasureGroup(asker, subject, group, map);
		truncated = groupSpace.Truncated;
		if (groupSpace.Unbounded)
		{
			return groupSpace.ObservedFloor;
		}
		StorageGroupBudget obj = crossDefBudget ?? (crossDefBudget = new StorageGroupBudget(0));
		obj.Reset(groupSpace.EmptyCells);
		obj.PriceDef(def, groupSpace.PartialSpace, groupSpace.PerCellCapacity);
		SpendCrossDefClaims(obj, group, def);
		return obj.AvailableFor(def);
	}

	private static void SpendCrossDefClaims(StorageGroupBudget budget, ISlotGroup group, ThingDef skip)
	{
		StorageClaimRow[] storageClaims = HaulersDreamGameComponent.storageClaims;
		for (int i = 0; i < storageClaims.Length; i++)
		{
			object def = storageClaims[i].Def;
			ThingDef val = (ThingDef)((def is ThingDef) ? def : null);
			if (val != null && val != skip && storageClaims[i].Group == group && !budget.IsPriced(val))
			{
				int num = StorageClaimLedger.ClaimedTotal(storageClaims, group, val, Evidence);
				if (num > 0)
				{
					budget.PriceDef(val, 0, Math.Max(1, val.stackLimit));
					budget.Consume(val, num);
				}
			}
		}
	}

	internal static GroupSpace MeasureGroup(Pawn pawn, Thing thing, ISlotGroup group, Map map)
	{
		StorageBuildingFilter storageBuildingFilter = ActiveFilter();
		int tickNow = TickNow;
		Dictionary<(object, Thing, Pawn, bool), GroupSpace> dictionary = spaceMemo ?? (spaceMemo = new Dictionary<(object, Thing, Pawn, bool), GroupSpace>());
		(object, Thing, Pawn, bool) key = (group, thing, pawn, storageBuildingFilter != null);
		if (tickNow != -1)
		{
			GroupSpace value;
			if (tickNow != spaceMemoTick)
			{
				dictionary.Clear();
				spaceMemoTick = tickNow;
			}
			else if (dictionary.TryGetValue(key, out value))
			{
				return value;
			}
		}
		GroupSpace groupSpace = MeasureGroupUncached(pawn, thing, group, map, storageBuildingFilter);
		if (tickNow != -1)
		{
			dictionary[key] = groupSpace;
		}
		return groupSpace;
	}

	private static GroupSpace MeasureGroupUncached(Pawn pawn, Thing thing, ISlotGroup group, Map map, StorageBuildingFilter filter)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		ThingDef def = thing.def;
		int num = Math.Max(1, def.stackLimit);
		List<IntVec3> cellsList = group.CellsList;
		if (cellsList == null)
		{
			return new GroupSpace(0, 0, num, unbounded: true, truncated: false, int.MaxValue);
		}
		int num2 = EnoughFor(def);
		long num3 = 0L;
		long num4 = 0L;
		int num5 = 0;
		int num6 = 0;
		bool flag = insideSpaceScan;
		insideSpaceScan = true;
		try
		{
			for (int i = 0; i < cellsList.Count; i++)
			{
				if (num6 >= 200)
				{
					break;
				}
				num6++;
				IntVec3 val = cellsList[i];
				if (!StoreUtility.IsGoodStoreCell(val, map, thing, pawn, ((Thing)pawn).Faction) || (filter != null && !filter.IsCellAllowed(val, map)))
				{
					continue;
				}
				int itemStackSpaceLeftFor = GridsUtility.GetItemStackSpaceLeftFor(val, map, def);
				if (itemStackSpaceLeftFor > 0)
				{
					if (GridsUtility.GetFirstItem(val, map) == null)
					{
						num5++;
						num3 += itemStackSpaceLeftFor;
					}
					else
					{
						num4 += itemStackSpaceLeftFor;
					}
					if (num4 + num3 >= num2)
					{
						break;
					}
				}
			}
		}
		finally
		{
			insideSpaceScan = flag;
		}
		if (num4 + num3 >= num2)
		{
			return new GroupSpace(0, 0, num, unbounded: true, truncated: false, num2);
		}
		bool truncated = num6 >= 200 && num6 < cellsList.Count;
		int val2 = (int)((num5 > 0) ? (num3 / num5) : num);
		return new GroupSpace(num5, (int)num4, Math.Max(1, val2), unbounded: false, truncated, 0);
	}

	internal static void Trace(string path, Pawn pawn, ISlotGroup group, ThingDef def, int units, int free = int.MaxValue)
	{
		int num = StorageClaimLedger.ClaimedByOthers(HaulersDreamGameComponent.storageClaims, group, def, pawn, Evidence);
		HDLog.Dbg(string.Format("storage-commit [{0}] {1} {2} x{3} ", path, ((pawn != null) ? ((Entity)pawn).LabelShort : null) ?? "?", ((Def)(def?)).defName ?? "?", units) + "-> " + GroupLabel(group) + "; free " + ((free == int.MaxValue) ? "unmeasured" : free.ToString()) + ", " + $"others enroute {num}.");
	}

	internal static int ClaimedByOthersFor(Pawn asker, ISlotGroup group, ThingDef def)
	{
		return StorageClaimLedger.ClaimedByOthers(HaulersDreamGameComponent.storageClaims, group, def, asker, Evidence);
	}

	private static string GroupLabel(ISlotGroup group)
	{
		if (group == null)
		{
			return "none";
		}
		SlotGroup val = (SlotGroup)(object)((group is SlotGroup) ? group : null);
		if (val != null && val.parent != null)
		{
			return ((object)val.parent).ToString();
		}
		return ((object)group).ToString();
	}
}
