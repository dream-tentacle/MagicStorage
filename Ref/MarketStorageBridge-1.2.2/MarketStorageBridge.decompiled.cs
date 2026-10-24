using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using HarmonyLib;
using MagicStorage;
using RimMarket.Buildings;
using RimMarket.Jobs;
using RimMarket.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
[assembly: AssemblyVersion("0.0.0.0")]
[module: RefSafetyRules(11)]
namespace MarketStorageBridge;

public class BridgeMod : Mod
{
	public const string Version = "1.2.2";

	public static BridgeSettings Settings;

	public BridgeMod(ModContentPack content)
		: base(content)
	{
		Settings = ((Mod)this).GetSettings<BridgeSettings>();
		Log.Message("[集市存储桥] v1.2.2 已加载 —— 摊位自动补货组件就绪");
	}

	public override string SettingsCategory()
	{
		return "集市存储桥";
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		if (Settings != null)
		{
			Settings.DoWindowContents(inRect);
		}
	}

	public override void WriteSettings()
	{
		((Mod)this).WriteSettings();
	}
}
public class BridgeSettings : ModSettings
{
	public bool enabled = true;

	public bool useStorageTradeWindow = true;

	public bool showNetworkStockInStallUi = true;

	public int restockIntervalTicks = 600;

	public int maxPerRestock = 500;

	public static bool Enabled
	{
		get
		{
			if (BridgeMod.Settings != null)
			{
				return BridgeMod.Settings.enabled;
			}
			return true;
		}
	}

	public static bool UseStorageTradeWindow
	{
		get
		{
			if (BridgeMod.Settings != null)
			{
				return BridgeMod.Settings.useStorageTradeWindow;
			}
			return true;
		}
	}

	public static bool ShowNetworkStockInStallUi
	{
		get
		{
			if (BridgeMod.Settings != null)
			{
				return BridgeMod.Settings.showNetworkStockInStallUi;
			}
			return true;
		}
	}

	public static int Interval
	{
		get
		{
			int num = ((BridgeMod.Settings == null) ? 600 : BridgeMod.Settings.restockIntervalTicks);
			if (num >= 60)
			{
				return num;
			}
			return 60;
		}
	}

	public static int MaxPerRestock
	{
		get
		{
			int num = ((BridgeMod.Settings == null) ? 500 : BridgeMod.Settings.maxPerRestock);
			if (num >= 1)
			{
				return num;
			}
			return 1;
		}
	}

	public override void ExposeData()
	{
		((ModSettings)this).ExposeData();
		Scribe_Values.Look<bool>(ref enabled, "enabled", true, false);
		Scribe_Values.Look<bool>(ref useStorageTradeWindow, "useStorageTradeWindow", true, false);
		Scribe_Values.Look<bool>(ref showNetworkStockInStallUi, "showNetworkStockInStallUi", true, false);
		Scribe_Values.Look<int>(ref restockIntervalTicks, "restockIntervalTicks", 600, false);
		Scribe_Values.Look<int>(ref maxPerRestock, "maxPerRestock", 500, false);
	}

	public void DoWindowContents(Rect inRect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		Listing_Standard val = new Listing_Standard();
		((Listing)val).Begin(inRect);
		val.CheckboxLabeled("启用自动补货", ref enabled, "关闭后所有殖民地摊位都不再从存储网络补货", 0f, 1f);
		((Listing)val).Gap(8f);
		val.CheckboxLabeled("摊位交易改用魔法存储窗口", ref useStorageTradeWindow, "开启后：殖民者照样走到摊位，但弹出的交易窗口是魔法存储的（能直接买卖网络里的库存）。关闭则用原版交易窗口。", 0f, 1f);
		((Listing)val).Gap(8f);
		val.CheckboxLabeled("布置摊位时显示网络库存", ref showNetworkStockInStallUi, "开启后：布置殖民地摊位时，「可卖物品」列表会把魔法存储网络里的东西也列出来，并显示数量。关闭则只显示地图上和柜台里的实体货。", 0f, 1f);
		((Listing)val).Gap(8f);
		val.Label("补货间隔：" + restockIntervalTicks + " tick（约 " + ((float)restockIntervalTicks / 60f).ToString("F1") + " 秒）", -1f, (TipSignal?)null);
		restockIntervalTicks = Mathf.RoundToInt(val.Slider((float)restockIntervalTicks, 60f, 3600f));
		((Listing)val).Gap(8f);
		val.Label("单次补货上限：" + maxPerRestock + " 个", -1f, (TipSignal?)null);
		maxPerRestock = Mathf.RoundToInt(val.Slider((float)maxPerRestock, 50f, 2000f));
		((Listing)val).Gap(12f);
		val.Label("说明：只对殖民地摊位生效，商人自己的摊位不碰；网络状态异常（无核心/多核心）时自动跳过。", -1f, (TipSignal?)null);
		((Listing)val).End();
	}
}
public class MapComponent_StallRestock : MapComponent
{
	private static readonly PropertyInfo OrdersProp = typeof(Building_MarketStall).GetProperty("Orders", BindingFlags.Instance | BindingFlags.Public);

	private static FieldInfo orderDefField;

	private static FieldInfo orderToSellField;

	private static MethodInfo orderAllowsMethod;

	private static bool orderReflectInit;

	private readonly List<Thing> stacksBuf = new List<Thing>();

	private readonly List<Building_MarketStall> stallsBuf = new List<Building_MarketStall>();

	private int tickCounter;

	private static void InitOrderReflection()
	{
		if (orderReflectInit)
		{
			return;
		}
		orderReflectInit = true;
		Type type = typeof(Building_MarketStall).Assembly.GetType("RimMarket.Buildings.StallOrder");
		if (type == null)
		{
			Log.Warning("[集市存储桥] 找不到 RimMarket.Buildings.StallOrder，自动补货将被禁用（RimMarket 可能已改版）");
			return;
		}
		orderDefField = type.GetField("def", BindingFlags.Instance | BindingFlags.Public);
		orderToSellField = type.GetField("toSell", BindingFlags.Instance | BindingFlags.Public);
		orderAllowsMethod = type.GetMethod("Allows", new Type[1] { typeof(Thing) });
		if (orderDefField == null)
		{
			Log.Warning("[集市存储桥] StallOrder.def 字段缺失，自动补货将被禁用");
		}
	}

	public MapComponent_StallRestock(Map map)
		: base(map)
	{
	}

	public override void ExposeData()
	{
		((MapComponent)this).ExposeData();
	}

	public override void FinalizeInit()
	{
		((MapComponent)this).FinalizeInit();
		RestockAll();
	}

	public override void MapGenerated()
	{
		((MapComponent)this).MapGenerated();
		RestockAll();
	}

	public override void MapComponentTick()
	{
		((MapComponent)this).MapComponentTick();
		if (BridgeSettings.Enabled)
		{
			tickCounter++;
			if (tickCounter >= BridgeSettings.Interval)
			{
				tickCounter = 0;
				RestockAll();
			}
		}
	}

	private void RestockAll()
	{
		if (!BridgeSettings.Enabled)
		{
			return;
		}
		InitOrderReflection();
		if (orderDefField == null)
		{
			return;
		}
		stallsBuf.Clear();
		try
		{
			foreach (Building_MarketStall item in base.map.listerBuildings.AllBuildingsColonistOfClass<Building_MarketStall>())
			{
				if (item != null && !((Thing)item).Destroyed && ((Thing)item).Spawned && item.ColonyStall)
				{
					stallsBuf.Add(item);
				}
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] 遍历摊位异常：" + ex, 5904130);
			return;
		}
		if (stallsBuf.Count == 0 || !NetworkFinder.MapHasReady(base.map))
		{
			return;
		}
		for (int i = 0; i < stallsBuf.Count; i++)
		{
			Building_MarketStall val = stallsBuf[i];
			try
			{
				RestockStall(val);
			}
			catch (Exception ex2)
			{
				Log.ErrorOnce("[集市存储桥] 摊位补货异常（thingID=" + ((Thing)val).thingIDNumber + "）：" + ex2, ((Thing)val).thingIDNumber ^ 0x5A17);
			}
		}
	}

	private void RestockStall(Building_MarketStall stall)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		StorageNetwork val = NetworkFinder.FindNearestReady(base.map, ((Thing)stall).Position);
		if (val == null || OrdersProp == null || !(OrdersProp.GetValue(stall, null) is IEnumerable enumerable))
		{
			return;
		}
		foreach (object item in enumerable)
		{
			if (item == null)
			{
				continue;
			}
			object? value = orderDefField.GetValue(item);
			ThingDef val2 = (ThingDef)((value is ThingDef) ? value : null);
			if (val2 == null)
			{
				continue;
			}
			int num = stall.RoomFor(val2);
			if (num <= 0)
			{
				continue;
			}
			int maxPerRestock = BridgeSettings.MaxPerRestock;
			if (num > maxPerRestock)
			{
				num = maxPerRestock;
			}
			stacksBuf.Clear();
			val.GetStacks(val2, stacksBuf);
			if (stacksBuf.Count == 0)
			{
				continue;
			}
			for (int i = 0; i < stacksBuf.Count && num > 0; i++)
			{
				Thing val3 = stacksBuf[i];
				if (val3 == null || val3.Destroyed)
				{
					continue;
				}
				if (orderAllowsMethod != null)
				{
					bool flag;
					try
					{
						flag = (bool)orderAllowsMethod.Invoke(item, new object[1] { val3 });
					}
					catch (Exception ex)
					{
						Log.ErrorOnce("[集市存储桥] StallOrder.Allows 调用失败：" + ex, 5904131);
						break;
					}
					if (!flag)
					{
						continue;
					}
				}
				int num2 = stall.RoomFor(val2);
				if (num2 <= 0)
				{
					break;
				}
				int num3 = num;
				if (num3 > val3.stackCount)
				{
					num3 = val3.stackCount;
				}
				if (num3 > num2)
				{
					num3 = num2;
				}
				if (num3 <= 0)
				{
					break;
				}
				StorageTransferResult val4 = val.TryTransferTo(val3, num3, stall.StockOwner, true);
				if (val4.Transferred > 0)
				{
					num -= val4.Transferred;
				}
			}
		}
	}
}
internal static class MarketReflection
{
	private static bool init;

	private static MethodInfo stockDefMethod;

	private static MethodInfo canSellMethod;

	private static MethodInfo tradeableMethod;

	private static void Init()
	{
		if (init)
		{
			return;
		}
		init = true;
		try
		{
			Assembly assembly = typeof(Building_MarketStall).Assembly;
			Type type = assembly.GetType("RimMarket.Market.ColonyStalls");
			Type type2 = assembly.GetType("RimMarket.Market.MarketUtility");
			if (type != null)
			{
				stockDefMethod = type.GetMethod("StockDef", new Type[1] { typeof(Thing) });
				canSellMethod = type.GetMethod("CanSell", new Type[1] { typeof(ThingDef) });
			}
			if (type2 != null)
			{
				tradeableMethod = type2.GetMethod("Tradeable", new Type[1] { typeof(Thing) });
			}
			if (stockDefMethod == null || canSellMethod == null || tradeableMethod == null)
			{
				Log.Warning("[集市存储桥] RimMarket 静态方法签名对不上（可能已改版）：StockDef=" + (stockDefMethod != null) + " CanSell=" + (canSellMethod != null) + " Tradeable=" + (tradeableMethod != null));
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] RimMarket 反射初始化失败：" + ex, 5904208);
		}
	}

	public static ThingDef StockDef(Thing t)
	{
		Init();
		if (stockDefMethod == null || t == null)
		{
			return null;
		}
		try
		{
			object? obj = stockDefMethod.Invoke(null, new object[1] { t });
			return (ThingDef)((obj is ThingDef) ? obj : null);
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] StockDef 调用失败：" + ex, 5904209);
			return null;
		}
	}

	public static bool CanSell(ThingDef def)
	{
		Init();
		if (canSellMethod == null || def == null)
		{
			return canSellMethod == null;
		}
		try
		{
			return (bool)canSellMethod.Invoke(null, new object[1] { def });
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] CanSell 调用失败：" + ex, 5904210);
			return false;
		}
	}

	public static bool Tradeable(Thing t)
	{
		Init();
		if (tradeableMethod == null)
		{
			return true;
		}
		if (t == null)
		{
			return false;
		}
		try
		{
			return (bool)tradeableMethod.Invoke(null, new object[1] { t });
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] Tradeable 调用失败：" + ex, 5904211);
			return false;
		}
	}
}
internal static class NetworkFinder
{
	public static StorageNetwork FindNearestReady(Map map, IntVec3 pos)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		if (map == null)
		{
			return null;
		}
		StorageNetwork result = null;
		int num = int.MaxValue;
		try
		{
			foreach (Building_StorageCore item in map.listerBuildings.AllBuildingsColonistOfClass<Building_StorageCore>())
			{
				if (item == null || ((Thing)item).Destroyed || !((Thing)item).Spawned)
				{
					continue;
				}
				StorageNetwork network = item.Network;
				if (network != null && network.CanWork)
				{
					IntVec3 val = ((Thing)item).Position - pos;
					int lengthHorizontalSquared = ((IntVec3)(ref val)).LengthHorizontalSquared;
					if (lengthHorizontalSquared < num)
					{
						num = lengthHorizontalSquared;
						result = network;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] 查找存储网络异常：" + ex, 5904176);
		}
		return result;
	}

	public static bool MapHasReady(Map map)
	{
		if (map == null)
		{
			return false;
		}
		try
		{
			foreach (Building_StorageCore item in map.listerBuildings.AllBuildingsColonistOfClass<Building_StorageCore>())
			{
				if (item != null && !((Thing)item).Destroyed && ((Thing)item).Spawned)
				{
					StorageNetwork network = item.Network;
					if (network != null && network.CanWork)
					{
						return true;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] 检查存储网络异常：" + ex, 5904177);
		}
		return false;
	}
}
[HarmonyPatch(typeof(Dialog_ColonyStall), "Refresh")]
internal static class Patch_ColonyStallStock
{
	private static readonly FieldInfo StallField = typeof(Dialog_ColonyStall).GetField("stall", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly List<Thing> stacksBuf = new List<Thing>();

	private static readonly HashSet<ThingDef> touchedDefs = new HashSet<ThingDef>();

	private static MethodInfo rowForMethod;

	private static MethodInfo countMethod;

	private static MethodInfo recountMethod;

	private static MethodInfo getAllStacksMethod;

	private static bool reflectTried;

	private static bool reflectOk;

	private static void EnsureReflection()
	{
		if (reflectTried)
		{
			return;
		}
		reflectTried = true;
		try
		{
			Type nestedType = typeof(Dialog_ColonyStall).GetNestedType("Row", BindingFlags.NonPublic);
			if (nestedType == null)
			{
				Log.Warning("[集市存储桥] 找不到 Dialog_ColonyStall.Row，布置摊位界面将不显示网络库存");
				return;
			}
			rowForMethod = typeof(Dialog_ColonyStall).GetMethod("RowFor", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[1] { typeof(ThingDef) }, null);
			countMethod = typeof(Dialog_ColonyStall).GetMethod("Count", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[3]
			{
				nestedType,
				typeof(Thing),
				typeof(bool)
			}, null);
			recountMethod = typeof(Dialog_ColonyStall).GetMethod("Recount", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[1] { nestedType }, null);
			getAllStacksMethod = typeof(StorageNetwork).GetMethod("GetAllStacks", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(List<Thing>) }, null);
			if (rowForMethod == null || countMethod == null || recountMethod == null || getAllStacksMethod == null)
			{
				Log.Warning("[集市存储桥] 布置摊位界面的方法签名对不上（RimMarket 或魔法存储可能改版）：RowFor=" + (rowForMethod != null) + " Count=" + (countMethod != null) + " Recount=" + (recountMethod != null) + " GetAllStacks=" + (getAllStacksMethod != null));
			}
			else
			{
				reflectOk = true;
				Log.Message("[集市存储桥] 已接上布置摊位界面（网络库存会出现在可选列表里）");
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] 布置摊位反射初始化失败：" + ex, 5904192);
		}
	}

	[HarmonyPostfix]
	private static void Postfix(Dialog_ColonyStall __instance)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		if (!BridgeSettings.ShowNetworkStockInStallUi)
		{
			return;
		}
		EnsureReflection();
		if (!reflectOk)
		{
			return;
		}
		try
		{
			object obj;
			if (!(StallField == null))
			{
				object? value = StallField.GetValue(__instance);
				obj = ((value is Building_MarketStall) ? value : null);
			}
			else
			{
				obj = null;
			}
			Building_MarketStall val = (Building_MarketStall)obj;
			if (val == null || ((Thing)val).Destroyed || !((Thing)val).Spawned)
			{
				return;
			}
			StorageNetwork val2 = NetworkFinder.FindNearestReady(((Thing)val).Map, ((Thing)val).Position);
			if (val2 == null)
			{
				return;
			}
			stacksBuf.Clear();
			getAllStacksMethod.Invoke(val2, new object[1] { stacksBuf });
			if (stacksBuf.Count == 0)
			{
				return;
			}
			touchedDefs.Clear();
			for (int i = 0; i < stacksBuf.Count; i++)
			{
				Thing val3 = stacksBuf[i];
				if (val3 == null || val3.Destroyed)
				{
					continue;
				}
				ThingDef val4 = MarketReflection.StockDef(val3);
				if (val4 != null && MarketReflection.CanSell(val4) && MarketReflection.Tradeable(val3))
				{
					object obj2 = rowForMethod.Invoke(__instance, new object[1] { val4 });
					if (obj2 != null)
					{
						countMethod.Invoke(null, new object[3] { obj2, val3, true });
						touchedDefs.Add(val4);
					}
				}
			}
			foreach (ThingDef touchedDef in touchedDefs)
			{
				object obj3 = rowForMethod.Invoke(__instance, new object[1] { touchedDef });
				if (obj3 != null)
				{
					recountMethod.Invoke(null, new object[1] { obj3 });
				}
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] 布置摊位界面补库存显示异常：" + ex, 5904193);
		}
	}
}
[HarmonyPatch(typeof(JobDriver_TradeAtStall), "MakeNewToils")]
internal static class Patch_TradeAtStall
{
	private static readonly PropertyInfo TraderProp = typeof(JobDriver_TradeAtStall).GetProperty("Trader", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly PropertyInfo StallProp = typeof(JobDriver_TradeAtStall).GetProperty("Stall", BindingFlags.Instance | BindingFlags.NonPublic);

	[HarmonyPostfix]
	private static IEnumerable<Toil> Postfix(IEnumerable<Toil> __result, JobDriver_TradeAtStall __instance)
	{
		List<Toil> list = new List<Toil>();
		if (__result == null)
		{
			return list;
		}
		foreach (Toil item in __result)
		{
			if (item != null)
			{
				list.Add(item);
			}
		}
		if (list.Count == 0 || !BridgeSettings.UseStorageTradeWindow)
		{
			return list;
		}
		for (int num = list.Count - 1; num >= 0; num--)
		{
			Toil target = list[num];
			if (target.initAction != null)
			{
				Action original = target.initAction;
				target.initAction = delegate
				{
					Run(target, __instance, original);
				};
				break;
			}
		}
		return list;
	}

	private static void Run(Toil toil, JobDriver_TradeAtStall driver, Action original)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Pawn actor = toil.actor;
			if (actor != null && BridgeSettings.UseStorageTradeWindow && StorageTradeBridge.Ready)
			{
				object obj;
				if (!(TraderProp == null))
				{
					object? value = TraderProp.GetValue(driver, null);
					obj = ((value is Pawn) ? value : null);
				}
				else
				{
					obj = null;
				}
				Pawn val = (Pawn)obj;
				if (val != null && val.CanTradeNow && StorageTradeBridge.MapHasReadyNetwork(((Thing)actor).Map) && StorageTradeBridge.TryOpen(actor, (ITrader)(object)val))
				{
					object obj2;
					if (!(StallProp == null))
					{
						object? value2 = StallProp.GetValue(driver, null);
						obj2 = ((value2 is CompMarketStall) ? value2 : null);
					}
					else
					{
						obj2 = null;
					}
					CompMarketStall val2 = (CompMarketStall)obj2;
					if (val2 != null)
					{
						actor.rotationTracker.FaceCell(((Thing)((ThingComp)val2).parent).Position);
						val2.MarkDisplayDirty();
					}
					return;
				}
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] 替换摊位交易窗口异常，回退原版：" + ex, 5904160);
		}
		original();
	}
}
[StaticConstructorOnStartup]
internal static class BridgeHarmony
{
	static BridgeHarmony()
	{
		int num = 0;
		int fail = 0;
		num += PatchOne(typeof(Patch_TradeAtStall), ref fail);
		num += PatchOne(typeof(Patch_ColonyStallStock), ref fail);
		Log.Message("[集市存储桥] 补丁挂载：" + num + " 成功 / " + fail + " 失败");
	}

	private static int PatchOne(Type patchType, ref int fail)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			return new Harmony("runtang.marketstorage.bridge." + patchType.Name).CreateClassProcessor(patchType).Patch().Count;
		}
		catch (Exception ex)
		{
			fail++;
			Log.Error("[集市存储桥] 补丁 " + patchType.Name + " 挂载失败：" + ex);
			return 0;
		}
	}
}
internal static class StorageTradeBridge
{
	private static bool initTried;

	private static MethodInfo openMethod;

	private static Type sessionType;

	public static bool Ready
	{
		get
		{
			Init();
			return openMethod != null;
		}
	}

	private static void Init()
	{
		if (initTried)
		{
			return;
		}
		initTried = true;
		try
		{
			sessionType = typeof(StorageNetwork).Assembly.GetType("MagicStorage.StorageTradeSession");
			if (sessionType == null)
			{
				Log.Warning("[集市存储桥] 找不到 MagicStorage.StorageTradeSession，摊位交易将回退原版窗口（魔法存储可能已改版）");
				return;
			}
			openMethod = sessionType.GetMethod("Open", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[3]
			{
				typeof(Pawn),
				typeof(ITrader),
				typeof(Building_CommsConsole)
			}, null);
			if (openMethod == null)
			{
				Log.Warning("[集市存储桥] 找不到 StorageTradeSession.Open(Pawn, ITrader, Building_CommsConsole)，摊位交易将回退原版窗口");
			}
			else
			{
				Log.Message("[集市存储桥] 已接上魔法存储交易窗口（StorageTradeSession.Open）");
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] 反射初始化失败：" + ex, 5904144);
		}
	}

	public static bool TryOpen(Pawn negotiator, ITrader trader)
	{
		Init();
		if (openMethod == null || negotiator == null || trader == null)
		{
			return false;
		}
		try
		{
			openMethod.Invoke(null, new object[3] { negotiator, trader, null });
			return true;
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] 打开魔法存储交易窗口失败：" + ex, 5904145);
			return false;
		}
	}

	public static bool MapHasReadyNetwork(Map map)
	{
		if (map == null)
		{
			return false;
		}
		try
		{
			foreach (Building_StorageCore item in map.listerBuildings.AllBuildingsColonistOfClass<Building_StorageCore>())
			{
				if (item != null && !((Thing)item).Destroyed && ((Thing)item).Spawned)
				{
					StorageNetwork network = item.Network;
					if (network != null && network.CanWork)
					{
						return true;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[集市存储桥] 检查存储网络异常：" + ex, 5904146);
		}
		return false;
	}
}
