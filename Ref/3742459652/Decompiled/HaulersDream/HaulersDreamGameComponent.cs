using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class HaulersDreamGameComponent : GameComponent
{
	private Dictionary<string, int> batchBills = new Dictionary<string, int>();

	public const int BatchSizeMax = 1000;

	private Dictionary<string, int> batchOvershoots = new Dictionary<string, int>();

	private const int IdleTickInterval = 250;

	private Dictionary<int, LoadLedgerEntry> loadTasks = new Dictionary<int, LoadLedgerEntry>();

	private Dictionary<int, LoadLedgerEntry> loadVehicleTasks = new Dictionary<int, LoadLedgerEntry>();

	[ThreadStatic]
	private static Dictionary<ThingDef, int> tmpNeeded;

	private Dictionary<Pawn, List<QuestPawnSnapshotItem>> questPawnSnapshots = new Dictionary<Pawn, List<QuestPawnSnapshotItem>>();

	private List<QuestPawnSnapshotData> questPawnSnapshotScribe;

	private const int SoftlockCheckInterval = 1800;

	private readonly Queue<Pawn> softlockQueue = new Queue<Pawn>();

	private readonly List<Thing> tmpSoftlockDrop = new List<Thing>();

	internal const int StorageClaimJanitorTicks = 120;

	internal static StorageClaimRow[] storageClaims = StorageClaimLedger.Empty;

	internal static int storageClaimGeneration;

	private List<VeinRevealTracker> veinTrackers = new List<VeinRevealTracker>();

	private const int VeinTickInterval = 60;

	private const int DrillTickInterval = 120;

	private const int DrillSection = 8;

	private const int DrillInterruptCooldownTicks = 300;

	private readonly Dictionary<Pawn, int> lastDrillInterruptTick = new Dictionary<Pawn, int>();

	public static HaulersDreamGameComponent Instance
	{
		get
		{
			Game game = Current.Game;
			if (game == null)
			{
				return null;
			}
			return game.GetComponent<HaulersDreamGameComponent>();
		}
	}

	private static JobDef[] HdJobDefs => HdJobDefSets.CustomDriverJobDefs;

	private static string BatchKey(Bill bill)
	{
		if (bill?.recipe != null)
		{
			return bill.GetUniqueLoadID();
		}
		return null;
	}

	public bool IsBatchBill(Bill bill)
	{
		string text = BatchKey(bill);
		if (text != null)
		{
			return batchBills.ContainsKey(text);
		}
		return false;
	}

	public int BatchSizeOf(Bill bill)
	{
		string text = BatchKey(bill);
		if (text == null || !batchBills.TryGetValue(text, out var value))
		{
			return 0;
		}
		return value;
	}

	public void SetBatch(Bill bill, bool on, int size)
	{
		string text = BatchKey(bill);
		if (text != null)
		{
			if (on)
			{
				batchBills[text] = Mathf.Clamp(size, 1, 1000);
			}
			else
			{
				batchBills.Remove(text);
			}
		}
	}

	public int BatchOvershootOf(Bill bill)
	{
		string text = BatchKey(bill);
		if (text == null || !batchOvershoots.TryGetValue(text, out var value))
		{
			return 0;
		}
		return value;
	}

	public void SetBatchOvershoot(Bill bill, int y)
	{
		string text = BatchKey(bill);
		if (text != null)
		{
			y = Mathf.Clamp(y, 0, 1000);
			if (y > 0)
			{
				batchOvershoots[text] = y;
			}
			else
			{
				batchOvershoots.Remove(text);
			}
		}
	}

	private void ExposeBatchBills()
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Invalid comparison between Unknown and I4
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Invalid comparison between Unknown and I4
		Scribe_Collections.Look<string, int>(ref batchBills, "haulersDreamBatchBills", (LookMode)1, (LookMode)1);
		Scribe_Collections.Look<string, int>(ref batchOvershoots, "haulersDreamBatchOvershoots", (LookMode)1, (LookMode)1);
		if ((int)Scribe.mode == 2 && batchBills == null)
		{
			batchBills = new Dictionary<string, int>();
		}
		if ((int)Scribe.mode == 2 && batchOvershoots == null)
		{
			batchOvershoots = new Dictionary<string, int>();
		}
	}

	public HaulersDreamGameComponent(Game game)
	{
	}

	public override void FinalizeInit()
	{
		((GameComponent)this).FinalizeInit();
		CacheRegistry.ClearAll();
		ValidateLoadLedgerAfterLoad();
		RepairOrphanedJobsAfterLoad();
	}

	private static void RepairOrphanedJobsAfterLoad()
	{
		List<Map> maps = Find.Maps;
		if (maps == null)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < maps.Count; i++)
		{
			Map val = maps[i];
			MapPawns mapPawns = val.mapPawns;
			IReadOnlyList<Pawn> readOnlyList = ((mapPawns != null) ? mapPawns.AllPawnsSpawned : null);
			if (readOnlyList != null)
			{
				for (int j = 0; j < readOnlyList.Count; j++)
				{
					Pawn_JobTracker val2 = readOnlyList[j]?.jobs;
					if (val2 == null)
					{
						continue;
					}
					if (val2.curJob != null && val2.curJob.def == null)
					{
						readOnlyList[j].ClearReservationsForJob(val2.curJob);
						Pawn_PathFollower pather = readOnlyList[j].pather;
						if (pather != null)
						{
							pather.StopDead();
						}
						val2.curDriver = null;
						val2.curJob = null;
						num++;
					}
					JobQueue jobQueue = val2.jobQueue;
					if (jobQueue != null)
					{
						jobQueue.RemoveAll(readOnlyList[j], (Predicate<Job>)((Job val4) => val4 == null || val4.def == null));
					}
				}
			}
			ReservationManager reservationManager = val.reservationManager;
			if (reservationManager == null)
			{
				continue;
			}
			List<Reservation> reservationsReadOnly = reservationManager.ReservationsReadOnly;
			List<Reservation> list = null;
			for (int num3 = 0; num3 < reservationsReadOnly.Count; num3++)
			{
				Reservation val3 = reservationsReadOnly[num3];
				if (((val3 != null) ? val3.Job : null) != null && val3.Job.def == null && val3.Claimant != null)
				{
					(list ?? (list = new List<Reservation>())).Add(val3);
				}
			}
			if (list != null)
			{
				for (int num4 = 0; num4 < list.Count; num4++)
				{
					reservationManager.ReleaseClaimedBy(list[num4].Claimant, list[num4].Job);
					num2++;
				}
			}
		}
		if (num > 0 || num2 > 0)
		{
			HDLog.Msg($"Migration cleanup: cleared {num} orphaned job(s) and " + $"{num2} stranded reservation(s) left by a removed mod (e.g. Pick Up And " + "Haul). Affected pawns will pick new jobs normally. This is a one-time repair for this save.");
		}
	}

	public override void GameComponentTick()
	{
		int ticksGame = Find.TickManager.TicksGame;
		if (veinTrackers != null && veinTrackers.Count > 0 && ticksGame % 60 == 0)
		{
			ProcessVeinTrackers();
		}
		if (ticksGame % 250 == 0)
		{
			RunIdleBackstop();
			PruneInertLoadTasks();
		}
		if (ticksGame % 120 == 0)
		{
			List<Map> maps = Find.Maps;
			for (int i = 0; i < maps.Count; i++)
			{
				StorageCommitments.RunJanitor(maps[i]);
			}
		}
		RunSoftlockDropDriver(ticksGame);
		RunYieldLeash(ticksGame);
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.markForUnload || !SchedulePolicy.IntervalDueNow(ticksGame, settings.intervalUnloadHours))
		{
			return;
		}
		List<Map> maps2 = Find.Maps;
		for (int j = 0; j < maps2.Count; j++)
		{
			List<Pawn> list = maps2[j].mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
			for (int k = 0; k < list.Count; k++)
			{
				PawnUnloadChecker.CheckIfShouldUnload(list[k]);
			}
		}
	}

	private static void RunIdleBackstop()
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return;
		}
		List<Map> maps = Find.Maps;
		for (int i = 0; i < maps.Count; i++)
		{
			List<Pawn> list = maps[i].mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
			for (int j = 0; j < list.Count; j++)
			{
				Pawn val = list[j];
				if (IsUnloadCheckpoint(val))
				{
					if (settings.markForUnload)
					{
						PawnUnloadChecker.CheckIfShouldUnload(val);
					}
					if (YieldRouter.IsCandidate(val))
					{
						YieldRouter.EnsureSelfPickupJob(val);
					}
				}
			}
		}
	}

	private static bool IsUnloadCheckpoint(Pawn p)
	{
		if (p?.jobs == null || p.Drafted)
		{
			return false;
		}
		if (ProtectedWork.IsRestingPatient(p))
		{
			return false;
		}
		if (p.jobs.jobQueue != null && p.jobs.jobQueue.Count > 0)
		{
			return false;
		}
		if (p.CurJob == null)
		{
			return true;
		}
		JobDef curJobDef = p.CurJobDef;
		if (curJobDef == null)
		{
			return false;
		}
		if (curJobDef == JobDefOf.Wait || curJobDef == JobDefOf.Wait_Wander || curJobDef == JobDefOf.GotoWander || curJobDef == JobDefOf.Wait_MaintainPosture)
		{
			return true;
		}
		if (curJobDef != JobDefOf.Ingest)
		{
			return curJobDef.joyKind != null;
		}
		return true;
	}

	private static bool IsRunningHdJob(Pawn pawn)
	{
		JobDef curJobDef = pawn.CurJobDef;
		if (curJobDef == null)
		{
			return false;
		}
		JobDef[] hdJobDefs = HdJobDefs;
		for (int i = 0; i < hdJobDefs.Length; i++)
		{
			if (hdJobDefs[i] == curJobDef)
			{
				return true;
			}
		}
		return false;
	}

	public override void ExposeData()
	{
		((GameComponent)this).ExposeData();
		ExposeVein();
		ExposeBatchBills();
		ExposeLedger();
		ExposeQuestPawns();
	}

	private Dictionary<int, LoadLedgerEntry> BucketFor(IManagedLoadable l)
	{
		if (l == null || l.Kind != LoadableKind.Vehicle)
		{
			return loadTasks;
		}
		return loadVehicleTasks;
	}

	public LoadLedgerEntry LoadRegisterOrUpdate(IManagedLoadable loadable)
	{
		if (loadable == null)
		{
			return null;
		}
		Dictionary<int, LoadLedgerEntry> dictionary = BucketFor(loadable);
		int uniqueLoadID = loadable.GetUniqueLoadID();
		if (!dictionary.TryGetValue(uniqueLoadID, out var value) || value == null)
		{
			value = (dictionary[uniqueLoadID] = new LoadLedgerEntry(loadable.GetMap()));
		}
		if (value.map == null)
		{
			value.map = loadable.GetMap();
		}
		if (!loadable.AnythingToLoad())
		{
			value.ClearNeeded();
			return value;
		}
		Dictionary<ThingDef, int> dictionary2 = tmpNeeded ?? (tmpNeeded = new Dictionary<ThingDef, int>());
		dictionary2.Clear();
		List<TransferableOneWay> transferables = loadable.GetTransferables();
		if (transferables != null)
		{
			for (int i = 0; i < transferables.Count; i++)
			{
				TransferableOneWay val = transferables[i];
				if (val != null && ((Transferable)val).HasAnyThing && ((Transferable)val).CountToTransfer > 0)
				{
					ThingDef thingDef = ((Transferable)val).ThingDef;
					if (thingDef != null)
					{
						dictionary2[thingDef] = (dictionary2.TryGetValue(thingDef, out var value2) ? value2 : 0) + ((Transferable)val).CountToTransfer;
					}
				}
			}
		}
		value.SetNeededFrom(dictionary2);
		dictionary2.Clear();
		return value;
	}

	public Dictionary<ThingDef, int> LoadAvailableToClaim(IManagedLoadable loadable, Pawn pawn)
	{
		if (loadable == null)
		{
			return new Dictionary<ThingDef, int>();
		}
		if (!BucketFor(loadable).TryGetValue(loadable.GetUniqueLoadID(), out var value) || value == null)
		{
			return new Dictionary<ThingDef, int>();
		}
		return value.AvailableToClaim(pawn);
	}

	public bool LoadHasWork(IManagedLoadable loadable, Pawn pawn)
	{
		if (loadable == null)
		{
			return false;
		}
		if (BucketFor(loadable).TryGetValue(loadable.GetUniqueLoadID(), out var value) && value != null)
		{
			return value.HasWork(pawn);
		}
		return false;
	}

	public void LoadClaim(Pawn pawn, Job job, IManagedLoadable loadable)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (pawn == null || job == null || loadable == null)
		{
			return;
		}
		LoadLedgerEntry loadLedgerEntry = LoadRegisterOrUpdate(loadable);
		if (loadLedgerEntry == null)
		{
			return;
		}
		Dictionary<ThingDef, int> dictionary = new Dictionary<ThingDef, int>();
		List<LocalTargetInfo> targetQueueB = job.targetQueueB;
		List<int> countQueue = job.countQueue;
		if (targetQueueB != null && countQueue != null)
		{
			for (int i = 0; i < targetQueueB.Count && i < countQueue.Count; i++)
			{
				LocalTargetInfo val = targetQueueB[i];
				Thing thing = ((LocalTargetInfo)(ref val)).Thing;
				if (thing?.def != null && countQueue[i] > 0)
				{
					dictionary[thing.def] = (dictionary.TryGetValue(thing.def, out var value) ? value : 0) + countQueue[i];
				}
			}
		}
		loadLedgerEntry.ApplyClaim(pawn, dictionary);
	}

	public void LoadClaimCarriedSurplus(Pawn pawn, IManagedLoadable loadable)
	{
		if (pawn == null || loadable == null)
		{
			return;
		}
		LoadLedgerEntry loadLedgerEntry = LoadRegisterOrUpdate(loadable);
		if (loadLedgerEntry == null)
		{
			return;
		}
		Dictionary<ThingDef, int> dictionary = loadLedgerEntry.AvailableToClaim(pawn);
		Dictionary<ThingDef, int> dictionary2 = new Dictionary<ThingDef, int>();
		if (dictionary.Count > 0)
		{
			foreach (KeyValuePair<ThingDef, int> item in InventorySurplus.SurplusByDef(pawn))
			{
				int value;
				int targetAvailableForDef = (dictionary.TryGetValue(item.Key, out value) ? value : 0);
				int num = OpportunisticLoadPolicy.DepositCount(item.Value, targetAvailableForDef);
				if (num > 0)
				{
					dictionary2[item.Key] = num;
				}
			}
		}
		loadLedgerEntry.ApplyClaim(pawn, dictionary2);
	}

	public void LoadNotifyDeposited(Pawn pawn, IManagedLoadable loadable, Thing moved)
	{
		if (pawn != null && loadable != null && moved?.def != null)
		{
			LoadNotifyDeposited(pawn, loadable, moved.def, moved.stackCount);
		}
	}

	public void LoadNotifyDeposited(Pawn pawn, IManagedLoadable loadable, ThingDef def, int count)
	{
		if (pawn != null && loadable != null && def != null && count > 0 && BucketFor(loadable).TryGetValue(loadable.GetUniqueLoadID(), out var value))
		{
			value?.Settle(pawn, def, count);
		}
	}

	public void LoadReleaseClaimsForPawn(Pawn pawn)
	{
		if (pawn == null)
		{
			return;
		}
		if (loadTasks.Count > 0)
		{
			foreach (LoadLedgerEntry value in loadTasks.Values)
			{
				value?.Release(pawn);
			}
		}
		if (loadVehicleTasks.Count <= 0)
		{
			return;
		}
		foreach (LoadLedgerEntry value2 in loadVehicleTasks.Values)
		{
			value2?.Release(pawn);
		}
	}

	public bool LoadAnyClaimsInProgress(int taskId)
	{
		if (loadTasks.TryGetValue(taskId, out var value) && value != null)
		{
			return value.AnyClaimed();
		}
		return false;
	}

	public bool LoadFullyClaimedByOthers(IManagedLoadable loadable)
	{
		if (loadable == null)
		{
			return false;
		}
		if (BucketFor(loadable).TryGetValue(loadable.GetUniqueLoadID(), out var value) && value != null)
		{
			return value.FullyClaimed();
		}
		return false;
	}

	public void Notify_LoadMapRemoved(Map map)
	{
		if (map != null)
		{
			DropFromBucket(loadTasks, (KeyValuePair<int, LoadLedgerEntry> kv) => kv.Value == null || kv.Value.map == map);
			DropFromBucket(loadVehicleTasks, (KeyValuePair<int, LoadLedgerEntry> kv) => kv.Value == null || kv.Value.map == map);
		}
	}

	internal void ValidateLoadLedgerAfterLoad()
	{
		if ((loadTasks == null || loadTasks.Count == 0) && (loadVehicleTasks == null || loadVehicleTasks.Count == 0))
		{
			return;
		}
		int num = 0;
		List<Map> maps = Find.Maps;
		for (int i = 0; i < maps.Count; i++)
		{
			Map obj = maps[i];
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				MapPawns mapPawns = obj.mapPawns;
				obj2 = ((mapPawns != null) ? mapPawns.AllPawnsSpawned : null);
			}
			IReadOnlyList<Pawn> readOnlyList = (IReadOnlyList<Pawn>)obj2;
			if (readOnlyList == null)
			{
				continue;
			}
			for (int j = 0; j < readOnlyList.Count; j++)
			{
				Pawn val = readOnlyList[j];
				if (val != null && !IsRunningHdJob(val) && LoadPawnHoldsAnyClaim(val))
				{
					LoadReleaseClaimsForPawn(val);
					num++;
				}
			}
		}
		if (num > 0)
		{
			HDLog.Warn($"Released {num} orphaned bulk-load claim(s) on load — pawns that held a claim but " + "are no longer running their load job (a save written by an older version). If hauling or cleanup had stalled colony-wide, it should now resume.");
		}
	}

	private bool LoadPawnHoldsAnyClaim(Pawn p)
	{
		if (p == null)
		{
			return false;
		}
		if (loadTasks.Count > 0)
		{
			foreach (LoadLedgerEntry value in loadTasks.Values)
			{
				if (value?.pawnClaims != null && value.pawnClaims.ContainsKey(p))
				{
					return true;
				}
			}
		}
		if (loadVehicleTasks.Count > 0)
		{
			foreach (LoadLedgerEntry value2 in loadVehicleTasks.Values)
			{
				if (value2?.pawnClaims != null && value2.pawnClaims.ContainsKey(p))
				{
					return true;
				}
			}
		}
		return false;
	}

	private void PruneInertLoadTasks()
	{
		DropFromBucket(loadTasks, (KeyValuePair<int, LoadLedgerEntry> kv) => kv.Value == null || kv.Value.IsInert);
		DropFromBucket(loadVehicleTasks, (KeyValuePair<int, LoadLedgerEntry> kv) => kv.Value == null || kv.Value.IsInert);
	}

	private static void DropFromBucket(Dictionary<int, LoadLedgerEntry> bucket, Func<KeyValuePair<int, LoadLedgerEntry>, bool> drop)
	{
		if (bucket == null || bucket.Count == 0)
		{
			return;
		}
		List<int> list = null;
		foreach (KeyValuePair<int, LoadLedgerEntry> item in bucket)
		{
			if (drop(item))
			{
				(list ?? (list = new List<int>())).Add(item.Key);
			}
		}
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				bucket.Remove(list[i]);
			}
		}
	}

	private void ExposeLedger()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Invalid comparison between Unknown and I4
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Invalid comparison between Unknown and I4
		Scribe_Collections.Look<int, LoadLedgerEntry>(ref loadTasks, "haulersDreamLoadTasks", (LookMode)1, (LookMode)2);
		if ((int)Scribe.mode == 2 && loadTasks == null)
		{
			loadTasks = new Dictionary<int, LoadLedgerEntry>();
		}
		Scribe_Collections.Look<int, LoadLedgerEntry>(ref loadVehicleTasks, "haulersDreamLoadVehicleTasks", (LookMode)1, (LookMode)2);
		if ((int)Scribe.mode == 2 && loadVehicleTasks == null)
		{
			loadVehicleTasks = new Dictionary<int, LoadLedgerEntry>();
		}
	}

	internal void RecordQuestPawnSnapshot(Pawn pawn, List<QuestPawnSnapshotItem> items)
	{
		if (pawn != null && items != null)
		{
			questPawnSnapshots[pawn] = items;
		}
	}

	internal bool TryGetQuestPawnSnapshot(Pawn pawn, out List<QuestPawnSnapshotItem> items)
	{
		items = null;
		if (pawn != null)
		{
			return questPawnSnapshots.TryGetValue(pawn, out items);
		}
		return false;
	}

	internal void ClearQuestPawnSnapshot(Pawn pawn)
	{
		if (pawn != null)
		{
			questPawnSnapshots.Remove(pawn);
		}
	}

	private void ExposeQuestPawns()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Invalid comparison between Unknown and I4
		if ((int)Scribe.mode == 1)
		{
			questPawnSnapshotScribe = new List<QuestPawnSnapshotData>();
			foreach (KeyValuePair<Pawn, List<QuestPawnSnapshotItem>> questPawnSnapshot in questPawnSnapshots)
			{
				if (questPawnSnapshot.Key != null && !((Thing)questPawnSnapshot.Key).Destroyed && !questPawnSnapshot.Key.Dead && questPawnSnapshot.Value != null)
				{
					questPawnSnapshotScribe.Add(new QuestPawnSnapshotData(questPawnSnapshot.Key, questPawnSnapshot.Value));
				}
			}
		}
		Scribe_Collections.Look<QuestPawnSnapshotData>(ref questPawnSnapshotScribe, "haulersDreamQuestPawnSnapshots", (LookMode)2, Array.Empty<object>());
		if ((int)Scribe.mode != 4)
		{
			return;
		}
		questPawnSnapshots = new Dictionary<Pawn, List<QuestPawnSnapshotItem>>();
		if (questPawnSnapshotScribe != null)
		{
			foreach (QuestPawnSnapshotData item in questPawnSnapshotScribe)
			{
				if (item?.pawn != null && !((Thing)item.pawn).Destroyed && !item.pawn.Dead && item.items != null)
				{
					item.items.RemoveAll((QuestPawnSnapshotItem it) => it == null || it.def == null || it.count <= 0);
					questPawnSnapshots[item.pawn] = item.items;
				}
			}
		}
		questPawnSnapshotScribe = null;
	}

	private static MechState MechStateOf(Pawn pawn)
	{
		if (!pawn.RaceProps.IsMechanoid)
		{
			return MechState.None;
		}
		JobDef curJobDef = pawn.CurJobDef;
		if (curJobDef == JobDefOf.SelfShutdown || RestUtility.IsSelfShutdown(pawn))
		{
			return MechState.SelfShutdown;
		}
		if (curJobDef == JobDefOf.MechCharge)
		{
			return MechState.Charging;
		}
		CompCanBeDormant comp = ((ThingWithComps)pawn).GetComp<CompCanBeDormant>();
		if (comp != null && !comp.Awake)
		{
			return MechState.Dormant;
		}
		return MechState.None;
	}

	private void RunSoftlockDropDriver(int tick)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.enableSoftlockDrop)
		{
			if (softlockQueue.Count > 0)
			{
				softlockQueue.Clear();
			}
			return;
		}
		if (tick % 1800 == 0)
		{
			softlockQueue.Clear();
			List<Map> maps = Find.Maps;
			for (int i = 0; i < maps.Count; i++)
			{
				List<Pawn> list = maps[i].mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
				for (int j = 0; j < list.Count; j++)
				{
					softlockQueue.Enqueue(list[j]);
				}
			}
		}
		if (softlockQueue.Count > 0)
		{
			TryDropSoftlockedCargo(softlockQueue.Dequeue());
		}
	}

	private void TryDropSoftlockedCargo(Pawn pawn)
	{
		if (pawn == null || pawn.Dead || !((Thing)pawn).Spawned || pawn.Drafted || ((Thing)pawn).Map == null)
		{
			return;
		}
		CompHauledToInventory compHauledToInventory = ThingCompUtility.TryGetComp<CompHauledToInventory>((Thing)(object)pawn);
		if (compHauledToInventory == null)
		{
			return;
		}
		ThingOwner<Thing> val = pawn.inventory?.innerContainer;
		if (val == null)
		{
			return;
		}
		int num = 0;
		foreach (Thing item in compHauledToInventory.PeekHashSet())
		{
			if (item != null && !item.Destroyed && item.stackCount > 0 && ((ThingOwner)val).Contains(item))
			{
				num++;
			}
		}
		if (SoftlockDropPolicy.ShouldDrop(WorkCapabilityProbe.IsDisabled(pawn, WorkTypeDefOf.Hauling) && !(HaulersDreamMod.Settings?.allowIncapable ?? false), pawn.RaceProps.IsMechanoid, MechStateOf(pawn), num, IsRunningHdJob(pawn)))
		{
			DropTrackedSnapshot(pawn, compHauledToInventory, val, tmpSoftlockDrop);
		}
	}

	private static void DropTrackedSnapshot(Pawn pawn, CompHauledToInventory comp, ThingOwner<Thing> owner, List<Thing> scratch)
	{
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		scratch.Clear();
		foreach (Thing item in comp.PeekHashSet())
		{
			if (item != null)
			{
				scratch.Add(item);
			}
		}
		scratch.Sort((Thing a, Thing b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
		Thing val2 = default(Thing);
		for (int num = 0; num < scratch.Count; num++)
		{
			Thing val = scratch[num];
			if (val != null && !val.Destroyed && ((ThingOwner)owner).Contains(val))
			{
				if (!owner.TryDrop(val, ((Thing)pawn).Position, ((Thing)pawn).Map, (ThingPlaceMode)1, ref val2, (Action<Thing, int>)null, (Predicate<IntVec3>)null))
				{
					break;
				}
				comp.Deregister(val);
			}
		}
		scratch.Clear();
	}

	internal static void DropTaggedCargo(Pawn pawn)
	{
		if (((pawn != null) ? ((Thing)pawn).Map : null) != null)
		{
			CompHauledToInventory compHauledToInventory = ThingCompUtility.TryGetComp<CompHauledToInventory>((Thing)(object)pawn);
			ThingOwner<Thing> val = pawn.inventory?.innerContainer;
			if (compHauledToInventory != null && val != null)
			{
				DropTrackedSnapshot(pawn, compHauledToInventory, val, new List<Thing>(compHauledToInventory.PeekHashSet().Count));
			}
		}
	}

	internal static void SetStorageClaims(StorageClaimRow[] rows)
	{
		storageClaims = rows ?? StorageClaimLedger.Empty;
		storageClaimGeneration++;
	}

	internal static void ClearStorageClaims()
	{
		SetStorageClaims(StorageClaimLedger.Empty);
	}

	public void RegisterVeinTracker(VeinRevealTracker tracker)
	{
		if (tracker?.pawn != null)
		{
			tracker.mapId = ((Thing)tracker.pawn).Map?.uniqueID ?? (-1);
			if (veinTrackers == null)
			{
				veinTrackers = new List<VeinRevealTracker>();
			}
			veinTrackers.RemoveAll((VeinRevealTracker t) => t.pawn == tracker.pawn);
			veinTrackers.Add(tracker);
		}
	}

	private void ProcessVeinTrackers()
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.planRoutes)
		{
			return;
		}
		for (int num = veinTrackers.Count - 1; num >= 0; num--)
		{
			if (!TryExtend(veinTrackers[num]))
			{
				veinTrackers.RemoveAt(num);
			}
		}
	}

	private bool TryExtend(VeinRevealTracker tr)
	{
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		Pawn val = tr?.pawn;
		if (val == null || val.Dead || !((Thing)val).Spawned || ((Thing)val).Map == null || tr.veinDef == null || tr.included == null)
		{
			return false;
		}
		if (((Thing)val).Map.uniqueID != tr.mapId)
		{
			return false;
		}
		if (VeinExtendPolicy.AtCap(tr.included.Count, tr.cap))
		{
			return false;
		}
		bool finalAttempt = false;
		if (!RouteExecutor.TryGetQueueTailCell(val, out var cell, out var tailJob) || !(cell == tr.lastCell) || tailJob.def != JobDefOf.Mine)
		{
			bool lastCellMined = !AnyVeinThingAt(((Thing)val).Map, tr.veinDef, tr.lastCell);
			JobQueue val2 = val.jobs?.jobQueue;
			bool nothingElseQueued = true;
			if (val2 != null)
			{
				JobDef haulersDream_SelfPickup = HaulersDreamDefOf.HaulersDream_SelfPickup;
				JobDef haulersDream_UnloadInventory = HaulersDreamDefOf.HaulersDream_UnloadInventory;
				for (int i = 0; i < val2.Count; i++)
				{
					if (UnloadPolicy.IsPendingRealWork<JobDef>(val2[i]?.job?.def, haulersDream_SelfPickup, haulersDream_UnloadInventory))
					{
						nothingElseQueued = false;
						break;
					}
				}
			}
			ExtendOutcome extendOutcome = VeinExtendPolicy.DecideSupersession(tailStillMatchesRoute: false, lastCellMined, nothingElseQueued);
			if (extendOutcome == ExtendOutcome.Drop)
			{
				return false;
			}
			finalAttempt = extendOutcome == ExtendOutcome.FinalAttempt;
		}
		RouteWorkKind routeWorkKind = WorkKindResolver.MiningKind(tr.veinDef);
		if (routeWorkKind?.scanner == null)
		{
			return false;
		}
		bool fogCaution;
		List<Thing> list = RouteSelection.ReFloodVisibleVein(((Thing)val).Map, val, tr.veinDef, tr.seed, tr.cap, tr.included, out fogCaution);
		List<Thing> list2 = new List<Thing>();
		for (int j = 0; j < list.Count; j++)
		{
			Thing val3 = list[j];
			if (val3 != null && !tr.included.Contains(val3.Position))
			{
				if (!VeinExtendPolicy.CanAddStop(tr.included.Count, list2.Count, tr.cap))
				{
					break;
				}
				list2.Add(val3);
			}
		}
		if (list2.Count == 0)
		{
			return VeinExtendPolicy.KeepAfterNoNewStops(finalAttempt, fogCaution);
		}
		list2.Sort(delegate(Thing a, Thing b)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 val4 = a.Position - tr.lastCell;
			int lengthHorizontalSquared = ((IntVec3)(ref val4)).LengthHorizontalSquared;
			val4 = b.Position - tr.lastCell;
			int num3 = lengthHorizontalSquared.CompareTo(((IntVec3)(ref val4)).LengthHorizontalSquared);
			return (num3 == 0) ? a.thingIDNumber.CompareTo(b.thingIDNumber) : num3;
		});
		IntVec3 lastCell;
		int num = RouteExecutor.AppendStops(val, routeWorkKind, list2, out lastCell);
		for (int num2 = 0; num2 < list2.Count; num2++)
		{
			tr.included.Add(list2[num2].Position);
		}
		if (num > 0)
		{
			tr.lastCell = lastCell;
		}
		return true;
	}

	private static bool AnyVeinThingAt(Map map, ThingDef def, IntVec3 cell)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || def == null || !GenGrid.InBounds(cell, map))
		{
			return false;
		}
		List<Thing> list = map.thingGrid.ThingsListAtFast(cell);
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i]?.def == def && list[i].Spawned)
			{
				return true;
			}
		}
		return false;
	}

	private void ExposeVein()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Invalid comparison between Unknown and I4
		Scribe_Collections.Look<VeinRevealTracker>(ref veinTrackers, "haulersDreamVeinTrackers", (LookMode)2, Array.Empty<object>());
		if ((int)Scribe.mode == 2 && veinTrackers == null)
		{
			veinTrackers = new List<VeinRevealTracker>();
		}
	}

	private void RunYieldLeash(int tick)
	{
		if (tick % 120 != 0)
		{
			return;
		}
		List<Map> maps = Find.Maps;
		for (int i = 0; i < maps.Count; i++)
		{
			List<Pawn> list = maps[i].mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
			for (int j = 0; j < list.Count; j++)
			{
				MaybeInterruptDrillForCollection(list[j], tick);
			}
		}
	}

	private void MaybeInterruptDrillForCollection(Pawn pawn, int tick)
	{
		if (pawn?.jobs == null || !(pawn.jobs.curDriver is JobDriver_OperateDeepDrill) || !YieldRouter.IsCandidate(pawn))
		{
			return;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp != null)
		{
			int value;
			int ticksSinceLastInterrupt = (lastDrillInterruptTick.TryGetValue(pawn, out value) ? (tick - value) : int.MaxValue);
			if (YieldLeashPolicy.ShouldCollectNow(comp.pendingSelfPickups.Count, 8, ticksSinceLastInterrupt, 300))
			{
				int count = comp.pendingSelfPickups.Count;
				YieldRouter.EnsureSelfPickupJob(pawn, comp);
				lastDrillInterruptTick[pawn] = tick;
				pawn.jobs.EndCurrentJob((JobCondition)16, true, true);
				HDLog.Dbg($"{pawn} drill-leash: interrupted drilling to collect {count} pending drop(s).");
			}
		}
	}
}
