using System;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace HaulersDream;

[StaticConstructorOnStartup]
public static class MultiplayerCompat
{
	private static class MpHooks
	{
		internal static void Register()
		{
			MP.RegisterSyncMethod(typeof(MultiplayerCompat), "SetAutoHaulYields", (SyncType[])null);
			MP.RegisterSyncMethod(typeof(MultiplayerCompat), "SetBenchGather", (SyncType[])null);
			MP.RegisterSyncMethod(typeof(MultiplayerCompat), "UnloadInventoryNow", (SyncType[])null);
			MP.RegisterSyncMethod(typeof(MultiplayerCompat), "SetBillBatch", (SyncType[])null);
			MP.RegisterSyncMethod(typeof(MultiplayerCompat), "SetBillBatchOvershoot", (SyncType[])null);
			MP.RegisterSyncMethod(typeof(MultiplayerCompat), "SetKeptCount", (SyncType[])null);
			MP.RegisterSyncMethod(typeof(JobDriver_BatchCraft), "StartBatchCraftSynced", (SyncType[])null);
			MP.RegisterSyncMethod(typeof(RouteExecutor), "ExecuteRouteSynced", (SyncType[])null);
			MP.RegisterSyncMethod(typeof(SowRouteExecutor), "ExecuteSowRouteSynced", (SyncType[])null);
			MP.RegisterSyncMethod(typeof(RemoveFloorRouteExecutor), "ExecuteRemoveFloorRouteSynced", (SyncType[])null);
		}

		internal static bool InMpGame()
		{
			return MP.IsInMultiplayer;
		}

		internal static bool IssuedBySelf()
		{
			return MP.IsExecutingSyncCommandIssuedBySelf;
		}
	}

	public static readonly bool Active;

	public static bool InMultiplayerGame
	{
		get
		{
			if (Active)
			{
				return MpHooks.InMpGame();
			}
			return false;
		}
	}

	public static bool ShouldShowLocalFeedback
	{
		get
		{
			if (Active)
			{
				return MpHooks.IssuedBySelf();
			}
			return true;
		}
	}

	static MultiplayerCompat()
	{
		Active = ModLister.GetActiveModWithIdentifier("rwmt.multiplayer", true) != null;
		if (!Active)
		{
			return;
		}
		try
		{
			MpHooks.Register();
			HDLog.Msg("Multiplayer detected — sync handlers registered; settings are host-authoritative at join (see CONTRIBUTING / mod description).");
		}
		catch (Exception ex)
		{
			Log.Warning("[Hauler's Dream] Multiplayer sync registration failed; player-initiated toggles may desync in MP. " + ex);
		}
	}

	public static void SetAutoHaulYields(Pawn pawn, bool value)
	{
		CompHauledToInventory compHauledToInventory = ((pawn != null) ? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() : null);
		if (compHauledToInventory != null)
		{
			compHauledToInventory.autoHaulYields = value;
		}
	}

	public static void SetBenchGather(Thing bench, bool value)
	{
		CompBenchGather compBenchGather = ((bench != null) ? ThingCompUtility.TryGetComp<CompBenchGather>(bench) : null);
		if (compBenchGather != null)
		{
			compBenchGather.gatherIngredients = value;
		}
	}

	public static void UnloadInventoryNow(Pawn pawn, bool queue)
	{
		if (pawn != null)
		{
			bool immediate = !queue;
			if (((Thing)pawn).Map != null && !MapGate.ShouldUnloadToStorage(((Thing)pawn).Map))
			{
				PackAnimalLoad.GizmoLoadNearest(pawn, immediate);
			}
			else
			{
				PawnUnloadChecker.CheckIfShouldUnload(pawn, forced: true, behindQueuedWork: false, immediate);
			}
		}
	}

	public static void SetBillBatch(Bill bill, bool on, int size)
	{
		HaulersDreamGameComponent.Instance?.SetBatch(bill, on, size);
	}

	public static void SetBillBatchOvershoot(Bill bill, int y)
	{
		HaulersDreamGameComponent.Instance?.SetBatchOvershoot(bill, y);
	}

	public static void SetKeptCount(Pawn pawn, ThingDef def, int count)
	{
		((pawn != null) ? ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() : null)?.SetKeptCount(def, count);
	}
}
