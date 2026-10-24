using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage;

internal sealed class StorageTradeSession : IDisposable
{
	private static StorageTradeSession opening;

	private static readonly Dictionary<TradeDeal, StorageTradeSession> sessions = new Dictionary<TradeDeal, StorageTradeSession>();

	private static readonly Action<TradeDeal> limitCurrency = AccessTools.MethodDelegate<Action<TradeDeal>>(AccessTools.Method(typeof(TradeDeal), "LimitCurrencyCountToFunds", (Type[])null, (Type[])null), (object)null, true, (Type[])null);

	private readonly Dictionary<Thing, StorageNetwork> sources = new Dictionary<Thing, StorageNetwork>();

	private readonly Dictionary<Thing, Building_StorageSupplyShelf> shelfSources = new Dictionary<Thing, Building_StorageSupplyShelf>();

	private readonly Pawn negotiator;

	private readonly ITrader trader;

	private readonly Building_CommsConsole console;

	private readonly Thing groundTarget;

	private readonly Func<bool> groundTargetUsable;

	private TradeDeal deal;

	private StorageTradeTransaction transaction;

	private bool closed;

	internal bool IsOrbital => trader is TradeShip && groundTarget == null;

	private StorageTradeSession(Pawn negotiator, ITrader trader, Building_CommsConsole console, Thing groundTarget, Func<bool> groundTargetUsable)
	{
		this.negotiator = negotiator;
		this.trader = trader;
		this.console = console;
		this.groundTarget = groundTarget;
		this.groundTargetUsable = groundTargetUsable;
	}

	internal static bool TryGet(TradeDeal deal, out StorageTradeSession session)
	{
		if (sessions.TryGetValue(deal, out session))
		{
			return true;
		}
		if (opening != null && opening.deal == null && TradeSession.trader == opening.trader && TradeSession.playerNegotiator == opening.negotiator)
		{
			session = opening;
			session.deal = deal;
			sessions.Add(deal, session);
			return true;
		}
		return false;
	}

	internal static void Open(Pawn negotiator, ITrader trader, Building_CommsConsole console = null, Thing groundTarget = null, Func<bool> groundTargetUsable = null)
	{
		if (opening != null || Find.WindowStack.WindowOfType<Dialog_Trade>() != null || DynamicTradeInterfaceCompatibility.HasTradeWindow())
		{
			return;
		}
		StorageTradeSession storageTradeSession = (opening = new StorageTradeSession(negotiator, trader, console, groundTarget, groundTargetUsable));
		try
		{
			Find.WindowStack.Add((Window)(object)new Dialog_StorageTrade(negotiator, trader, storageTradeSession));
		}
		catch
		{
			storageTradeSession.Dispose();
			throw;
		}
		finally
		{
			opening = null;
		}
	}

	internal static bool HasCore(Map map)
	{
		if (map == null)
		{
			return false;
		}
		map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
		foreach (Building item in map.listerBuildings.allBuildingsColonist)
		{
			if (item is Building_StorageCore { CanWork: not false })
			{
				return true;
			}
		}
		return false;
	}

	private bool Usable()
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		if (!closed && TradeSession.trader == trader && TradeSession.playerNegotiator == negotiator && ((Thing)negotiator).Spawned && trader.CanTradeNow)
		{
			AcceptanceReport val = FactionUtility.CanTradeWith(negotiator, trader.Faction, trader.TraderKind);
			if (((AcceptanceReport)(ref val)).Accepted)
			{
				if (groundTarget != null)
				{
					return !groundTarget.Destroyed && groundTarget.Spawned && groundTarget.Map == ((Thing)negotiator).Map && (groundTargetUsable?.Invoke() ?? false);
				}
				ITrader obj = trader;
				Pawn val2 = (Pawn)(object)((obj is Pawn) ? obj : null);
				if (val2 != null)
				{
					return ((Thing)val2).Spawned && ((Thing)negotiator).Map == ((Thing)val2).Map && !val2.mindState.traderDismissed;
				}
				ITrader obj2 = trader;
				TradeShip val3 = (TradeShip)(object)((obj2 is TradeShip) ? obj2 : null);
				if (val3 != null)
				{
					return ((PassingShip)val3).Map == ((Thing)negotiator).Map && ((Thing)negotiator).Map.passingShipManager.passingShips.Contains((PassingShip)(object)val3) && console != null && ((Thing)console).Spawned && ((Thing)console).Map == ((Thing)negotiator).Map && console.CanUseCommsNow && negotiator.health.capacities.CapableOf(PawnCapacityDefOf.Talking);
				}
				return false;
			}
		}
		return false;
	}

	internal static bool HasOrbitalCore(Map map)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		if (!HasCore(map))
		{
			return false;
		}
		foreach (Building item in map.listerBuildings.allBuildingsColonist)
		{
			if (item is Building_StorageCore { CanWork: not false } building_StorageCore && !GridsUtility.Fogged(((Thing)building_StorageCore).Position, map) && BeaconCovers(building_StorageCore))
			{
				return true;
			}
		}
		return false;
	}

	private static bool BeaconCovers(Building_StorageCore core)
	{
		return Building_OrbitalTradeBeacon.AllPowered(((Thing)core).Map).Any((Building_OrbitalTradeBeacon beacon) => beacon.TradeableCells.Contains(((Thing)core).Position));
	}

	private bool CoreEligible(Building_StorageCore core, out string reason)
	{
		object obj2;
		if (groundTarget == null)
		{
			ITrader obj = trader;
			obj2 = ((obj is Pawn) ? obj : null);
		}
		else
		{
			obj2 = negotiator;
		}
		Thing groundTrader = (Thing)obj2;
		return CoreEligible(core, ((Thing)negotiator).Map, IsOrbital, groundTrader, out reason);
	}

	internal static bool PaymentCoreEligible(Building_StorageCore core, Map map)
	{
		string reason;
		return CoreEligible(core, map, orbital: false, null, out reason);
	}

	private static bool CoreEligible(Building_StorageCore core, Map map, bool orbital, Thing groundTrader, out string reason)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		reason = null;
		if (core == null || !core.CanWork || ((Thing)core).Faction != Faction.OfPlayer || ((Thing)core).Map != map || GridsUtility.Fogged(((Thing)core).Position, map))
		{
			return false;
		}
		if (orbital && !BeaconCovers(core))
		{
			return false;
		}
		if (groundTrader != null && !map.reachability.CanReach(groundTrader.Position, LocalTargetInfo.op_Implicit((Thing)(object)core), (PathEndMode)2, (TraverseMode)1, (Danger)2))
		{
			return false;
		}
		Room room = RegionAndRoomQuery.GetRoom((Thing)(object)core, (RegionType)15);
		if (room == null)
		{
			return true;
		}
		int num = GenRadial.NumCellsInRadius(6.9f);
		for (int i = 0; i < num; i++)
		{
			IntVec3 val = ((Thing)core).Position + GenRadial.RadialPattern[i];
			if (!GenGrid.InBounds(val, map) || GridsUtility.GetRoom(val, map) != room)
			{
				continue;
			}
			foreach (Thing thing in GridsUtility.GetThingList(val, map))
			{
				if (thing.PreventPlayerSellingThingsNearby(ref reason))
				{
					return false;
				}
			}
		}
		return true;
	}

	internal int Available(Thing item)
	{
		if (!sources.TryGetValue(item, out var value))
		{
			return 0;
		}
		shelfSources.TryGetValue(item, out var value2);
		return transaction?.Available(value, item, value2) ?? ((value2 == null) ? value.AvailableToWithdraw(item) : StorageTradeTransaction.ShelfAvailable(value, value2, item));
	}

	internal void FillInventory()
	{
		//IL_0327: Unknown result type (might be due to invalid IL or missing references)
		//IL_032d: Invalid comparison between Unknown and I4
		//IL_0340: Unknown result type (might be due to invalid IL or missing references)
		//IL_034a: Expected O, but got Unknown
		transaction?.Dispose();
		transaction = null;
		sources.Clear();
		shelfSources.Clear();
		if (Usable())
		{
			((Thing)negotiator).Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
			HashSet<StorageNetwork> hashSet = new HashSet<StorageNetwork>();
			foreach (Building item in ((Thing)negotiator).Map.listerBuildings.allBuildingsColonist)
			{
				if (!(item is Building_StorageCore building_StorageCore))
				{
					continue;
				}
				if (!CoreEligible(building_StorageCore, out var reason))
				{
					if (reason != null && !deal.cannotSellReasons.Contains(reason))
					{
						deal.cannotSellReasons.Add(reason);
					}
				}
				else
				{
					if (!hashSet.Add(building_StorageCore.Network))
					{
						continue;
					}
					List<Thing> list = new List<Thing>();
					building_StorageCore.Network.GetAllStacks(list);
					list.Sort((Thing a, Thing b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
					foreach (Thing item2 in list)
					{
						AddColony(item2, building_StorageCore.Network);
					}
					foreach (Building_StorageSupplyShelf supplyShelf in ((Thing)negotiator).Map.GetComponent<MapComponent_StorageNetworks>().SupplyShelves)
					{
						if (!((Thing)supplyShelf).Spawned || supplyShelf.Network != building_StorageCore.Network)
						{
							continue;
						}
						foreach (Thing item3 in supplyShelf.GetSlotGroup().HeldThings.OrderBy((Thing item) => item.thingIDNumber))
						{
							AddColony(item3, building_StorageCore.Network, supplyShelf);
						}
					}
				}
			}
		}
		if (!TradeSession.giftMode)
		{
			foreach (Thing good in trader.Goods)
			{
				Add(good, (Transactor)1);
			}
			if (deal.AllTradeables.Find((Tradeable t) => t.IsCurrency) == null)
			{
				Thing val = ThingMaker.MakeThing(ThingDefOf.Silver, (ThingDef)null);
				val.stackCount = 0;
				Add(val, (Transactor)1);
			}
		}
		if ((int)TradeSession.TradeCurrency == 1)
		{
			deal.AllTradeables.Add((Tradeable)new Tradeable_RoyalFavor());
		}
	}

	private void AddColony(Thing item, StorageNetwork network, Building_StorageSupplyShelf shelf = null)
	{
		if (item != null && !item.Destroyed && !(item is Pawn) && !sources.ContainsKey(item) && ((shelf == null) ? network.AvailableToWithdraw(item) : StorageTradeTransaction.ShelfAvailable(network, shelf, item)) > 0 && TradeUtility.PlayerSellableNow(item, trader))
		{
			sources.Add(item, network);
			if (shelf != null)
			{
				shelfSources.Add(item, shelf);
			}
			Add(item, (Transactor)0);
		}
	}

	private void Add(Thing item, Transactor trans)
	{
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		List<Tradeable> list = ((MagicStorageMod.Settings.TradeItemFilters && item.def != ThingDefOf.Silver) ? deal.AllTradeables.Where((Tradeable candidate) => StorageInventoryFilters.CanGroup(item, ((Transferable)candidate).AnyThing, MagicStorageMod.Settings.InventoryFilters)).ToList() : deal.AllTradeables);
		Tradeable val = TransferableUtility.TradeableMatching(item, list);
		if (val == null)
		{
			val = (Tradeable)(object)((!(item is Pawn)) ? new Tradeable_Storage(this) : ((Tradeable_Storage)new Tradeable_Pawn()));
			deal.AllTradeables.Add(val);
		}
		val.AddThing(item, trans);
	}

	internal bool Prepare(out StorageTradeTransaction prepared)
	{
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Invalid comparison between Unknown and I4
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Invalid comparison between Unknown and I4
		prepared = null;
		transaction?.Dispose();
		transaction = null;
		if (!Usable())
		{
			return Reject();
		}
		((Thing)negotiator).Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
		deal.UpdateCurrencyCount();
		if (!TradeSession.giftMode && (deal.CurrencyTradeable == null || deal.CurrencyTradeable.CountPostDealFor((Transactor)0) < 0))
		{
			return Reject();
		}
		limitCurrency(deal);
		List<StorageTradeRequest> list = new List<StorageTradeRequest>();
		foreach (Tradeable allTradeable in deal.AllTradeables)
		{
			if ((int)allTradeable.ActionToDo == 1 && ((Transferable)allTradeable).CountToTransferToSource > allTradeable.CountHeldBy((Transactor)1))
			{
				return Reject();
			}
			if ((int)allTradeable.ActionToDo != 2)
			{
				continue;
			}
			int num = ((Transferable)allTradeable).CountToTransferToDestination;
			foreach (Thing item in allTradeable.thingsColony.OrderBy((Thing key) => shelfSources.ContainsKey(key)))
			{
				if (num == 0)
				{
					break;
				}
				if (!sources.TryGetValue(item, out var network) || !CoreEligible(network.Core, out var _) || !TradeUtility.PlayerSellableNow(item, trader))
				{
					continue;
				}
				shelfSources.TryGetValue(item, out var value);
				int num2 = Math.Min(num, Available(item));
				if (num2 > 0)
				{
					list.Add(new StorageTradeRequest
					{
						Line = allTradeable,
						Network = network,
						Shelf = value,
						Item = item,
						Count = num2,
						Usable = () => Usable() && CoreEligible(network.Core, out var _) && TradeUtility.PlayerSellableNow(item, trader)
					});
					num -= num2;
				}
			}
			if (num == 0)
			{
				continue;
			}
			return Reject();
		}
		StorageTradeTransaction storageTradeTransaction = new StorageTradeTransaction(list, Usable);
		if (!storageTradeTransaction.Reserve())
		{
			return Reject();
		}
		transaction = (prepared = storageTradeTransaction);
		return true;
	}

	private bool Reject()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		Dialog_Trade obj = Find.WindowStack.WindowOfType<Dialog_Trade>();
		if (obj != null)
		{
			obj.FlashSilver();
		}
		DynamicTradeInterfaceCompatibility.FlashSilver(this);
		Messages.Message(TaggedString.op_Implicit(Translator.Translate("MS_Trade_Unavailable")), MessageTypeDefOf.RejectInput, false);
		return false;
	}

	internal void Deliver(Tradeable_Storage line)
	{
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		if (transaction == null)
		{
			throw new InvalidOperationException("Storage trade has no prepared transaction.");
		}
		if (transaction.PendingFor(line) != ((Transferable)line).CountToTransferToDestination)
		{
			throw new InvalidOperationException("Storage trade quantity changed after reservation.");
		}
		Map map = ((Thing)negotiator).Map;
		MapComponent_StorageNetworks component = map.GetComponent<MapComponent_StorageNetworks>();
		? origin;
		if (groundTarget == null)
		{
			ITrader obj = trader;
			Pawn val = (Pawn)(object)((obj is Pawn) ? obj : null);
			origin = ((val != null) ? ((Thing)val).Position : ((Thing)console).Position);
		}
		else
		{
			origin = groundTarget.Position;
		}
		StorageRecoveryBatch storageRecoveryBatch = component.CreateRecovery((IntVec3)origin);
		List<Thing> list = new List<Thing>();
		try
		{
			if (!transaction.Collect(line, storageRecoveryBatch.Contents, list))
			{
				throw new InvalidOperationException("Storage trade reservation became unavailable during delivery.");
			}
			foreach (Thing item in list)
			{
				trader.GiveSoldThingToTrader(item, item.stackCount, negotiator);
			}
		}
		finally
		{
			foreach (Thing item2 in list)
			{
				if (storageRecoveryBatch.Contents.Contains(item2))
				{
					transaction.Restore(storageRecoveryBatch.Contents, item2);
				}
			}
			component.ReleaseRecovery(storageRecoveryBatch);
		}
	}

	internal void Finish(StorageTradeTransaction prepared)
	{
		prepared?.Dispose();
		if (transaction == prepared)
		{
			transaction = null;
		}
	}

	public void Dispose()
	{
		if (!closed)
		{
			closed = true;
			transaction?.Dispose();
			transaction = null;
			if (deal != null)
			{
				sessions.Remove(deal);
			}
			sources.Clear();
			shelfSources.Clear();
		}
	}
}
