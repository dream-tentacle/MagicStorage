using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using CeleTech.Base;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: AssemblyVersion("0.0.0.0")]
namespace CmcTradeShuttleExt;

[StaticConstructorOnStartup]
public static class Main
{
	static Main()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		new Harmony("cmc.tradeshuttle.ext").PatchAll(Assembly.GetExecutingAssembly());
	}
}
public class CmcTradeShuttleExtMod : Mod
{
	public static CmcTradeShuttleExtSettings Settings;

	public CmcTradeShuttleExtMod(ModContentPack content)
		: base(content)
	{
		Settings = ((Mod)this).GetSettings<CmcTradeShuttleExtSettings>();
	}

	public override string SettingsCategory()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.SettingsCategory"));
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		Settings.Draw(inRect);
	}
}
public class CmcTradeShuttleExtSettings : ModSettings
{
	public bool shipGeneral = true;

	public bool shipFoodDrug = true;

	public bool shipApparel = true;

	public bool shipRaw = true;

	public bool shipAnimals = true;

	public bool shipOrigMaterials = true;

	public bool shipOrigWeapons = true;

	public bool shipOrigSlaves = true;

	public int baseSilver = 100000;

	public float silverStep = 0.5f;

	public float goodsStep = 0.5f;

	public int upgradeCostBase = 500;

	public int maxLevel = 20;

	public const float MaxSilverAmount = 2E+09f;

	private string silverBuffer = "";

	private string silverStepBuffer = "";

	private string goodsStepBuffer = "";

	private string costBuffer = "";

	private string maxLevelBuffer = "";

	public override void ExposeData()
	{
		Scribe_Values.Look<bool>(ref shipGeneral, "shipGeneral", true, false);
		Scribe_Values.Look<bool>(ref shipFoodDrug, "shipFoodDrug", true, false);
		Scribe_Values.Look<bool>(ref shipApparel, "shipApparel", true, false);
		Scribe_Values.Look<bool>(ref shipRaw, "shipRaw", true, false);
		Scribe_Values.Look<bool>(ref shipAnimals, "shipAnimals", true, false);
		Scribe_Values.Look<bool>(ref shipOrigMaterials, "shipOrigMaterials", true, false);
		Scribe_Values.Look<bool>(ref shipOrigWeapons, "shipOrigWeapons", true, false);
		Scribe_Values.Look<bool>(ref shipOrigSlaves, "shipOrigSlaves", true, false);
		Scribe_Values.Look<int>(ref baseSilver, "baseSilver", 100000, false);
		Scribe_Values.Look<float>(ref silverStep, "silverStep", 0.5f, false);
		Scribe_Values.Look<float>(ref goodsStep, "goodsStep", 0.5f, false);
		Scribe_Values.Look<int>(ref upgradeCostBase, "upgradeCostBase", 500, false);
		Scribe_Values.Look<int>(ref maxLevel, "maxLevel", 20, false);
		Sanitize();
	}

	private bool Sanitize()
	{
		bool result = false;
		if (maxLevel < 1)
		{
			maxLevel = 1;
			result = true;
		}
		if (maxLevel > 100)
		{
			maxLevel = 100;
			result = true;
		}
		if (baseSilver < 0)
		{
			baseSilver = 0;
			result = true;
		}
		if ((float)baseSilver > 2E+09f)
		{
			baseSilver = 2000000000;
			result = true;
		}
		if (float.IsNaN(silverStep) || silverStep < 0f)
		{
			silverStep = 0f;
			result = true;
		}
		if ((double)silverStep > 10.0)
		{
			silverStep = 10f;
			result = true;
		}
		if (float.IsNaN(goodsStep) || goodsStep < 0f)
		{
			goodsStep = 0f;
			result = true;
		}
		if ((double)goodsStep > 10.0)
		{
			goodsStep = 10f;
			result = true;
		}
		if (upgradeCostBase < 0)
		{
			upgradeCostBase = 0;
			result = true;
		}
		if (upgradeCostBase > 10000000)
		{
			upgradeCostBase = 10000000;
			result = true;
		}
		return result;
	}

	public bool IsShipEnabled(string defName)
	{
		return defName switch
		{
			"CMC_OrbitalTrader_General" => shipGeneral, 
			"CMC_OrbitalTrader_FoodDrug" => shipFoodDrug, 
			"CMC_OrbitalTrader_Apparel" => shipApparel, 
			"CMC_OrbitalTrader_Raw" => shipRaw, 
			"CMC_OrbitalTrader_Animals" => shipAnimals, 
			"CMC_OrbitalTrader_Materials" => shipOrigMaterials, 
			"CMC_OrbitalTrader_Weapons" => shipOrigWeapons, 
			"CMC_OrbitalTrader_Slaves" => shipOrigSlaves, 
			_ => true, 
		};
	}

	public float SilverMult(int level)
	{
		return 1f + silverStep * (float)level;
	}

	public float GoodsMult(int level)
	{
		return 1f + goodsStep * (float)level;
	}

	public int CostFor(int level)
	{
		return upgradeCostBase * (level + 1);
	}

	public void Reset()
	{
		shipGeneral = true;
		shipFoodDrug = true;
		shipApparel = true;
		shipRaw = true;
		shipAnimals = true;
		shipOrigMaterials = true;
		shipOrigWeapons = true;
		shipOrigSlaves = true;
		baseSilver = 100000;
		silverStep = 0.5f;
		goodsStep = 0.5f;
		upgradeCostBase = 500;
		maxLevel = 20;
		silverBuffer = "";
		silverStepBuffer = "";
		goodsStepBuffer = "";
		costBuffer = "";
		maxLevelBuffer = "";
	}

	public void Draw(Rect rect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_0273: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_030d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0318: Unknown result type (might be due to invalid IL or missing references)
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		//IL_036d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0378: Unknown result type (might be due to invalid IL or missing references)
		//IL_039f: Unknown result type (might be due to invalid IL or missing references)
		//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b0: Unknown result type (might be due to invalid IL or missing references)
		Listing_Standard val = new Listing_Standard();
		((Listing)val).Begin(rect);
		((Listing)val).Gap(8f);
		Text.Font = (GameFont)2;
		val.Label(Translator.Translate("CmcTradeShuttleExt.SettingsShipsHeader"), -1f, (string)null);
		Text.Font = (GameFont)1;
		((Listing)val).Gap(4f);
		val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.ShipOrigMaterials")), ref shipOrigMaterials, (string)null, 0f, 1f);
		val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.ShipOrigWeapons")), ref shipOrigWeapons, (string)null, 0f, 1f);
		val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.ShipOrigSlaves")), ref shipOrigSlaves, (string)null, 0f, 1f);
		val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.ShipGeneral")), ref shipGeneral, (string)null, 0f, 1f);
		val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.ShipFoodDrug")), ref shipFoodDrug, (string)null, 0f, 1f);
		val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.ShipApparel")), ref shipApparel, (string)null, 0f, 1f);
		val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.ShipRaw")), ref shipRaw, (string)null, 0f, 1f);
		val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.ShipAnimals")), ref shipAnimals, (string)null, 0f, 1f);
		((Listing)val).Gap(12f);
		Text.Font = (GameFont)2;
		val.Label(Translator.Translate("CmcTradeShuttleExt.SettingsUpgradeHeader"), -1f, (string)null);
		Text.Font = (GameFont)1;
		((Listing)val).Gap(4f);
		val.Label(Translator.Translate("CmcTradeShuttleExt.BaseSilver"), -1f, (string)null);
		if (silverBuffer.Length == 0)
		{
			silverBuffer = baseSilver.ToString();
		}
		Widgets.TextFieldNumeric<int>(((Listing)val).GetRect(24f, 1f), ref baseSilver, ref silverBuffer, 0f, 1E+09f);
		val.Label(Translator.Translate("CmcTradeShuttleExt.SilverStep"), -1f, (string)null);
		if (silverStepBuffer.Length == 0)
		{
			silverStepBuffer = silverStep.ToString("0.##");
		}
		Widgets.TextFieldNumeric<float>(((Listing)val).GetRect(24f, 1f), ref silverStep, ref silverStepBuffer, 0f, 1E+09f);
		val.Label(Translator.Translate("CmcTradeShuttleExt.GoodsStep"), -1f, (string)null);
		if (goodsStepBuffer.Length == 0)
		{
			goodsStepBuffer = goodsStep.ToString("0.##");
		}
		Widgets.TextFieldNumeric<float>(((Listing)val).GetRect(24f, 1f), ref goodsStep, ref goodsStepBuffer, 0f, 1E+09f);
		val.Label(Translator.Translate("CmcTradeShuttleExt.UpgradeCostBase"), -1f, (string)null);
		if (costBuffer.Length == 0)
		{
			costBuffer = upgradeCostBase.ToString();
		}
		Widgets.TextFieldNumeric<int>(((Listing)val).GetRect(24f, 1f), ref upgradeCostBase, ref costBuffer, 0f, 1E+09f);
		val.Label(Translator.Translate("CmcTradeShuttleExt.MaxLevel"), -1f, (string)null);
		if (maxLevelBuffer.Length == 0)
		{
			maxLevelBuffer = maxLevel.ToString();
		}
		Widgets.TextFieldNumeric<int>(((Listing)val).GetRect(24f, 1f), ref maxLevel, ref maxLevelBuffer, 0f, 1E+09f);
		bool flag = Sanitize();
		((Listing)val).Gap(12f);
		if (val.ButtonText(TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.ResetDefaults")), (string)null, 1f))
		{
			Reset();
		}
		if (flag)
		{
			silverBuffer = baseSilver.ToString();
			silverStepBuffer = silverStep.ToString("0.##");
			goodsStepBuffer = goodsStep.ToString("0.##");
			costBuffer = upgradeCostBase.ToString();
			maxLevelBuffer = maxLevel.ToString();
		}
		((Listing)val).End();
	}
}
public static class CooldownStore
{
	public static int lastGeneralHr;

	public static int lastFoodHr;

	public static int lastApparelHr;

	public static int lastRawHr;

	public static int lastAnimalsHr;

	private static int GetLastTick(string defName)
	{
		return defName switch
		{
			"CMC_OrbitalTrader_General" => lastGeneralHr, 
			"CMC_OrbitalTrader_FoodDrug" => lastFoodHr, 
			"CMC_OrbitalTrader_Apparel" => lastApparelHr, 
			"CMC_OrbitalTrader_Raw" => lastRawHr, 
			"CMC_OrbitalTrader_Animals" => lastAnimalsHr, 
			_ => 0, 
		};
	}

	private static void SetLastTick(string defName, int tick)
	{
		switch (defName)
		{
		case "CMC_OrbitalTrader_General":
			lastGeneralHr = tick;
			break;
		case "CMC_OrbitalTrader_FoodDrug":
			lastFoodHr = tick;
			break;
		case "CMC_OrbitalTrader_Apparel":
			lastApparelHr = tick;
			break;
		case "CMC_OrbitalTrader_Raw":
			lastRawHr = tick;
			break;
		case "CMC_OrbitalTrader_Animals":
			lastAnimalsHr = tick;
			break;
		}
	}

	public static int GetCooldownHours(string defName, int duration)
	{
		int lastTick = GetLastTick(defName);
		if (lastTick == 0)
		{
			SetLastTick(defName, GenTicks.TicksGame);
			return 0;
		}
		int num = (GenTicks.TicksGame - lastTick) / 2500;
		if (num < 0)
		{
			num = 0;
		}
		if (num > duration)
		{
			num = duration;
		}
		return num;
	}

	public static void ResetCooldown(string defName)
	{
		SetLastTick(defName, GenTicks.TicksGame);
	}

	public static void ClearCooldown(string defName)
	{
		SetLastTick(defName, GenTicks.TicksGame - 21900000);
	}

	public static void ResetAll()
	{
		lastGeneralHr = 0;
		lastFoodHr = 0;
		lastApparelHr = 0;
		lastRawHr = 0;
		lastAnimalsHr = 0;
	}
}
public static class UpgradeStore
{
	public static Dictionary<string, int> silverLevelByDef = new Dictionary<string, int>();

	public static Dictionary<string, int> goodsLevelByDef = new Dictionary<string, int>();

	public static int GetSilver(string defName)
	{
		if (silverLevelByDef == null)
		{
			silverLevelByDef = new Dictionary<string, int>();
		}
		if (silverLevelByDef.TryGetValue(defName, out var value))
		{
			return value;
		}
		return 0;
	}

	public static int GetGoods(string defName)
	{
		if (goodsLevelByDef == null)
		{
			goodsLevelByDef = new Dictionary<string, int>();
		}
		if (goodsLevelByDef.TryGetValue(defName, out var value))
		{
			return value;
		}
		return 0;
	}

	public static void UpgradeSilver(string defName)
	{
		if (silverLevelByDef == null)
		{
			silverLevelByDef = new Dictionary<string, int>();
		}
		silverLevelByDef[defName] = GetSilver(defName) + 1;
	}

	public static void UpgradeGoods(string defName)
	{
		if (goodsLevelByDef == null)
		{
			goodsLevelByDef = new Dictionary<string, int>();
		}
		goodsLevelByDef[defName] = GetGoods(defName) + 1;
	}

	public static void ResetAll()
	{
		silverLevelByDef = new Dictionary<string, int>();
		goodsLevelByDef = new Dictionary<string, int>();
	}
}
public class StockGenerator_BuyCategory : StockGenerator
{
	public ThingCategoryDef categoryDef;

	public List<ThingDef> excludedThingDefs;

	public List<ThingCategoryDef> excludedCategories;

	public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
	{
		yield break;
	}

	public override bool HandlesThingDef(ThingDef thingDef)
	{
		if (thingDef.thingCategories == null)
		{
			return false;
		}
		if (excludedThingDefs != null && excludedThingDefs.Contains(thingDef))
		{
			return false;
		}
		for (int i = 0; i < thingDef.thingCategories.Count; i++)
		{
			for (ThingCategoryDef val = thingDef.thingCategories[i]; val != null; val = val.parent)
			{
				if (excludedCategories != null && excludedCategories.Contains(val))
				{
					return false;
				}
				if (val == categoryDef)
				{
					return true;
				}
			}
		}
		return false;
	}

	public override Tradeability TradeabilityFor(ThingDef thingDef)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		if ((int)thingDef.tradeability != 0)
		{
			if (!((StockGenerator)this).HandlesThingDef(thingDef))
			{
				return (Tradeability)0;
			}
			return (Tradeability)1;
		}
		return (Tradeability)0;
	}
}
public class StockGenerator_BuyAnimals : StockGenerator
{
	public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
	{
		yield break;
	}

	public override bool HandlesThingDef(ThingDef thingDef)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		if ((int)thingDef.category == 1 && thingDef.race != null)
		{
			return thingDef.race.Animal;
		}
		return false;
	}

	public override Tradeability TradeabilityFor(ThingDef thingDef)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		if ((int)thingDef.tradeability != 0)
		{
			if (!((StockGenerator)this).HandlesThingDef(thingDef))
			{
				return (Tradeability)0;
			}
			return (Tradeability)1;
		}
		return (Tradeability)0;
	}
}
[HarmonyPatch(typeof(Building_TradeConsole), "GetGizmos")]
public static class Patch_TradeConsole_GetGizmos
{
	private static void Postfix(Building_TradeConsole __instance, ref IEnumerable<Gizmo> __result)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Expected O, but got Unknown
		List<Gizmo> list = ((__result == null) ? new List<Gizmo>() : __result.ToList());
		for (int i = 0; i < list.Count; i++)
		{
			Gizmo obj = list[i];
			Command_Action val = (Command_Action)(object)((obj is Command_Action) ? obj : null);
			if (val == null || (!string.Equals(((Command)val).defaultLabel, "DEV: Reset CD", StringComparison.Ordinal) && (((Command)val).defaultDesc == null || !((Command)val).defaultDesc.Contains("Reset cooldowns for all trade shuttles"))))
			{
				continue;
			}
			Action original = val.action;
			val.action = delegate
			{
				if (original != null)
				{
					original();
				}
				ResetAllExt(showMsg: false);
			};
			break;
		}
		if (Prefs.DevMode)
		{
			list.Add((Gizmo)new Command_Action
			{
				defaultLabel = "DEV: Reset Ext CDs",
				defaultDesc = "Debug: Reset cooldowns for the 5 extended trade shuttles.",
				action = delegate
				{
					ResetAllExt(showMsg: true);
				}
			});
		}
		__result = list;
	}

	private static void ResetAllExt(bool showMsg)
	{
		CooldownStore.ClearCooldown("CMC_OrbitalTrader_General");
		CooldownStore.ClearCooldown("CMC_OrbitalTrader_FoodDrug");
		CooldownStore.ClearCooldown("CMC_OrbitalTrader_Apparel");
		CooldownStore.ClearCooldown("CMC_OrbitalTrader_Raw");
		CooldownStore.ClearCooldown("CMC_OrbitalTrader_Animals");
		if (showMsg)
		{
			Messages.Message("Extended shuttle cooldowns reset", MessageTypeDefOf.TaskCompletion, false);
		}
	}
}
[HarmonyPatch(typeof(GameComponent_CeleTech), "ExposeData")]
public static class Patch_ExposeData
{
	public static void Postfix()
	{
		Scribe_Values.Look<int>(ref CooldownStore.lastGeneralHr, "lastGeneralHr", 0, false);
		Scribe_Values.Look<int>(ref CooldownStore.lastFoodHr, "lastFoodHr", 0, false);
		Scribe_Values.Look<int>(ref CooldownStore.lastApparelHr, "lastApparelHr", 0, false);
		Scribe_Values.Look<int>(ref CooldownStore.lastRawHr, "lastRawHr", 0, false);
		Scribe_Values.Look<int>(ref CooldownStore.lastAnimalsHr, "lastAnimalsHr", 0, false);
		Scribe_Collections.Look<string, int>(ref UpgradeStore.silverLevelByDef, "CmcSilverUpgrades", (LookMode)1, (LookMode)1);
		Scribe_Collections.Look<string, int>(ref UpgradeStore.goodsLevelByDef, "CmcGoodsUpgrades", (LookMode)1, (LookMode)1);
		if (UpgradeStore.silverLevelByDef == null)
		{
			UpgradeStore.silverLevelByDef = new Dictionary<string, int>();
		}
		if (UpgradeStore.goodsLevelByDef == null)
		{
			UpgradeStore.goodsLevelByDef = new Dictionary<string, int>();
		}
	}
}
[HarmonyPatch(typeof(GameComponent_CeleTech), "StartedNewGame")]
public static class Patch_StartedNewGame
{
	public static void Postfix()
	{
		CooldownStore.ResetAll();
		UpgradeStore.ResetAll();
	}
}
[HarmonyPatch(typeof(Window_TradeShuttle), "InitializeShuttles")]
public static class Patch_InitializeShuttles
{
	private static readonly string[] NewKinds = new string[5] { "CMC_OrbitalTrader_General", "CMC_OrbitalTrader_FoodDrug", "CMC_OrbitalTrader_Apparel", "CMC_OrbitalTrader_Raw", "CMC_OrbitalTrader_Animals" };

	public static void Postfix(Window_TradeShuttle __instance)
	{
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Expected O, but got Unknown
		//IL_027e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Expected O, but got Unknown
		//IL_0304: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Expected O, but got Unknown
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Expected O, but got Unknown
		//IL_0353: Unknown result type (might be due to invalid IL or missing references)
		//IL_035d: Expected O, but got Unknown
		if (!(AccessTools.Field(typeof(Window_TradeShuttle), "shuttleEntries").GetValue(__instance) is List<ShuttleEntry> list))
		{
			return;
		}
		list.RemoveAll((ShuttleEntry e) => e != null && e.TraderKindDef != null && !CmcTradeShuttleExtMod.Settings.IsShipEnabled(((Def)e.TraderKindDef).defName));
		if (GenCollection.Any<ShuttleEntry>(list, (Predicate<ShuttleEntry>)((ShuttleEntry e) => e.TraderKindDef != null && NewKinds.Contains(((Def)e.TraderKindDef).defName))))
		{
			AccessTools.Field(typeof(Window_TradeShuttle), "totalViewHeight").SetValue(__instance, 138f * (float)list.Count);
			return;
		}
		ResearchProjectDef namedSilentFail = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("CMC_ShuttleTradeExtension");
		bool flag = namedSilentFail == null || namedSilentFail.IsFinished;
		CmcTradeShuttleExtSettings settings = CmcTradeShuttleExtMod.Settings;
		if (settings.shipGeneral && flag)
		{
			list.Add(new ShuttleEntry("CMC_TraderShuttle", "CMC_OrbitalTrader_General", "Things/Skyfaller/TradeShuttle", new Color(0.1f, 0.1f, 0.1f, 0.5f), (Func<int>)(() => CooldownStore.GetCooldownHours("CMC_OrbitalTrader_General", 24)), (Action)delegate
			{
				CooldownStore.ResetCooldown("CMC_OrbitalTrader_General");
			}, 24, 2500, "CmcTradeShuttleExt.GeneralShuttleDesc"));
		}
		if (settings.shipFoodDrug && flag)
		{
			list.Add(new ShuttleEntry("CMC_TraderShuttle", "CMC_OrbitalTrader_FoodDrug", "Things/Skyfaller/TradeShuttle", new Color(0.1f, 0.1f, 0.1f, 0.5f), (Func<int>)(() => CooldownStore.GetCooldownHours("CMC_OrbitalTrader_FoodDrug", 24)), (Action)delegate
			{
				CooldownStore.ResetCooldown("CMC_OrbitalTrader_FoodDrug");
			}, 24, 2500, "CmcTradeShuttleExt.FoodDrugShuttleDesc"));
		}
		if (settings.shipApparel && flag)
		{
			list.Add(new ShuttleEntry("CMC_TraderShuttle", "CMC_OrbitalTrader_Apparel", "Things/Skyfaller/TradeShuttle", new Color(0.1f, 0.1f, 0.1f, 0.5f), (Func<int>)(() => CooldownStore.GetCooldownHours("CMC_OrbitalTrader_Apparel", 36)), (Action)delegate
			{
				CooldownStore.ResetCooldown("CMC_OrbitalTrader_Apparel");
			}, 36, 3000, "CmcTradeShuttleExt.ApparelShuttleDesc"));
		}
		if (settings.shipRaw && flag)
		{
			list.Add(new ShuttleEntry("CMC_TraderShuttle", "CMC_OrbitalTrader_Raw", "Things/Skyfaller/TradeShuttle", new Color(0.1f, 0.1f, 0.1f, 0.5f), (Func<int>)(() => CooldownStore.GetCooldownHours("CMC_OrbitalTrader_Raw", 48)), (Action)delegate
			{
				CooldownStore.ResetCooldown("CMC_OrbitalTrader_Raw");
			}, 48, 3500, "CmcTradeShuttleExt.RawShuttleDesc"));
		}
		if (settings.shipAnimals && flag)
		{
			list.Add(new ShuttleEntry("CMC_TraderShuttle", "CMC_OrbitalTrader_Animals", "Things/Skyfaller/TradeShuttle", new Color(0.1f, 0.1f, 0.1f, 0.5f), (Func<int>)(() => CooldownStore.GetCooldownHours("CMC_OrbitalTrader_Animals", 48)), (Action)delegate
			{
				CooldownStore.ResetCooldown("CMC_OrbitalTrader_Animals");
			}, 48, 3500, "CmcTradeShuttleExt.AnimalsShuttleDesc"));
		}
		AccessTools.Field(typeof(Window_TradeShuttle), "totalViewHeight").SetValue(__instance, 138f * (float)list.Count);
	}
}
[HarmonyPatch(typeof(TradeShip), "GenerateThings")]
public static class Patch_TradeShip_GenerateThings
{
	private static readonly Dictionary<StockGenerator, IntRange> countBaseline = new Dictionary<StockGenerator, IntRange>();

	private static readonly Dictionary<StockGenerator, FloatRange> priceBaseline = new Dictionary<StockGenerator, FloatRange>();

	private static readonly Dictionary<StockGenerator_Category, IntRange> catBaseline = new Dictionary<StockGenerator_Category, IntRange>();

	private static readonly FieldInfo fSingleDefThingDef = AccessTools.Field(typeof(StockGenerator_SingleDef), "thingDef");

	private static readonly FieldInfo fCatCountRange = AccessTools.Field(typeof(StockGenerator_Category), "thingDefCountRange");

	private static List<StockGenerator> touched;

	private static bool IsBuy(StockGenerator sg)
	{
		if (!(sg is StockGenerator_BuySingleDef) && !(sg is StockGenerator_BuyTradeTag) && !(sg is StockGenerator_BuyExpensiveSimple) && !(sg is StockGenerator_BuyCategory))
		{
			return sg is StockGenerator_BuyAnimals;
		}
		return true;
	}

	public static void Prefix(TradeShip __instance)
	{
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Invalid comparison between Unknown and O
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		TraderKindDef def = __instance.def;
		if (def == null || ((Def)def).defName == null || !((Def)def).defName.StartsWith("CMC_OrbitalTrader_"))
		{
			return;
		}
		CmcTradeShuttleExtSettings settings = CmcTradeShuttleExtMod.Settings;
		int silver = UpgradeStore.GetSilver(((Def)def).defName);
		int goods = UpgradeStore.GetGoods(((Def)def).defName);
		float num = settings.SilverMult(silver);
		float num2 = settings.GoodsMult(goods);
		if (def.stockGenerators == null)
		{
			return;
		}
		touched = null;
		IntRange countRange = default(IntRange);
		FloatRange totalPriceRange = default(FloatRange);
		for (int i = 0; i < def.stockGenerators.Count; i++)
		{
			StockGenerator val = def.stockGenerators[i];
			if (val == null || IsBuy(val))
			{
				continue;
			}
			bool flag = false;
			StockGenerator_SingleDef val2 = (StockGenerator_SingleDef)(object)((val is StockGenerator_SingleDef) ? val : null);
			if (val2 != null)
			{
				flag = (object)(ThingDef)fSingleDefThingDef.GetValue(val2) == ThingDefOf.Silver;
			}
			if (!countBaseline.TryGetValue(val, out var value))
			{
				value = val.countRange;
				countBaseline[val] = value;
			}
			if (!priceBaseline.TryGetValue(val, out var value2))
			{
				value2 = val.totalPriceRange;
				priceBaseline[val] = value2;
			}
			if (flag)
			{
				float num3 = (float)settings.baseSilver * num;
				if (num3 < 0f)
				{
					num3 = 0f;
				}
				else if (num3 > 2E+09f)
				{
					num3 = 2E+09f;
				}
				int num4 = (int)num3;
				((IntRange)(ref countRange))..ctor(num4, num4);
				((FloatRange)(ref totalPriceRange))..ctor(value2.min, value2.max);
			}
			else
			{
				((IntRange)(ref countRange))..ctor((int)((float)value.min * num2), (int)((float)value.max * num2));
				((FloatRange)(ref totalPriceRange))..ctor(value2.min * num2, value2.max * num2);
			}
			val.countRange = countRange;
			val.totalPriceRange = totalPriceRange;
			StockGenerator_Category val3 = (StockGenerator_Category)(object)((val is StockGenerator_Category) ? val : null);
			if (val3 != null)
			{
				if (!catBaseline.TryGetValue(val3, out var value3))
				{
					value3 = (IntRange)fCatCountRange.GetValue(val3);
					catBaseline[val3] = value3;
				}
				fCatCountRange.SetValue(val3, (object)new IntRange((int)((float)value3.min * num2), (int)((float)value3.max * num2)));
			}
			if (touched == null)
			{
				touched = new List<StockGenerator>();
			}
			touched.Add(val);
		}
	}

	public static void Postfix()
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		if (touched == null)
		{
			return;
		}
		for (int i = 0; i < touched.Count; i++)
		{
			StockGenerator val = touched[i];
			if (countBaseline.TryGetValue(val, out var value))
			{
				val.countRange = value;
			}
			if (priceBaseline.TryGetValue(val, out var value2))
			{
				val.totalPriceRange = value2;
			}
			StockGenerator_Category val2 = (StockGenerator_Category)(object)((val is StockGenerator_Category) ? val : null);
			if (val2 != null && catBaseline.TryGetValue(val2, out var value3))
			{
				fCatCountRange.SetValue(val2, value3);
			}
		}
		touched = null;
	}
}
[HarmonyPatch(typeof(Window_TradeShuttle), "DrawGenericShuttleEntry")]
public static class Patch_DrawGenericShuttleEntry
{
	public static void Postfix(Listing_Standard listing, ShuttleEntry entry)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		if (entry == null || entry.TraderKindDef == null)
		{
			return;
		}
		string defName = ((Def)entry.TraderKindDef).defName;
		if (defName != null && defName.StartsWith("CMC_OrbitalTrader_"))
		{
			CmcTradeShuttleExtSettings settings = CmcTradeShuttleExtMod.Settings;
			Rect rect = ((Listing)listing).GetRect(28f, 1f);
			int silver = UpgradeStore.GetSilver(defName);
			int goods = UpgradeStore.GetGoods(defName);
			int num = settings.CostFor(silver);
			int num2 = settings.CostFor(goods);
			float num3 = (((Rect)(ref rect)).width - 6f) / 2f;
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, num3, ((Rect)(ref rect)).height);
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(((Rect)(ref rect)).x + num3 + 6f, ((Rect)(ref rect)).y, num3, ((Rect)(ref rect)).height);
			bool num4 = silver >= settings.maxLevel;
			bool flag = goods >= settings.maxLevel;
			string text = ((!num4) ? string.Format(TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.UpgradeLevelCost")), TranslatorFormattedStringExtensions.Translate("CmcTradeShuttleExt.SilverUpgradeLv", NamedArgument.op_Implicit(silver)), num) : TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.SilverUpgradeFull")));
			string text2 = ((!flag) ? string.Format(TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.UpgradeLevelCost")), TranslatorFormattedStringExtensions.Translate("CmcTradeShuttleExt.GoodsUpgradeLv", NamedArgument.op_Implicit(goods)), num2) : TaggedString.op_Implicit(Translator.Translate("CmcTradeShuttleExt.GoodsUpgradeFull")));
			Rect val3 = default(Rect);
			((Rect)(ref val3))..ctor(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width, ((Rect)(ref val)).height);
			Rect val4 = default(Rect);
			((Rect)(ref val4))..ctor(((Rect)(ref val2)).x, ((Rect)(ref val2)).y, ((Rect)(ref val2)).width, ((Rect)(ref val2)).height);
			if (!num4 && TradeWindowDrawer.DrawCustomButton(val3, text, true, (Texture2D)null, (Texture2D)null))
			{
				TryUpgrade(defName, num, Translator.Translate("CmcTradeShuttleExt.SilverUpgradeName"), isSilver: true, settings);
			}
			if (!flag && TradeWindowDrawer.DrawCustomButton(val4, text2, true, (Texture2D)null, (Texture2D)null))
			{
				TryUpgrade(defName, num2, Translator.Translate("CmcTradeShuttleExt.GoodsUpgradeName"), isSilver: false, settings);
			}
		}
	}

	private static void TryUpgrade(string defName, int cost, TaggedString what, bool isSilver, CmcTradeShuttleExtSettings s)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		GameComponent_CeleTech instance = GameComponent_CeleTech.Instance;
		if (instance == null)
		{
			return;
		}
		if (instance.CurrentPoint < cost)
		{
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CmcTradeShuttleExt.UpgradeFail", NamedArgument.op_Implicit(cost), NamedArgument.op_Implicit(instance.CurrentPoint))), MessageTypeDefOf.RejectInput, false);
			return;
		}
		instance.CurrentPoint -= cost;
		int num;
		if (isSilver)
		{
			UpgradeStore.UpgradeSilver(defName);
			num = UpgradeStore.GetSilver(defName);
		}
		else
		{
			UpgradeStore.UpgradeGoods(defName);
			num = UpgradeStore.GetGoods(defName);
		}
		string text = ((isSilver ? s.silverStep : s.goodsStep) * 100f).ToString("0.##", CultureInfo.InvariantCulture);
		Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CmcTradeShuttleExt.UpgradeDone", NamedArgument.op_Implicit(cost.ToString()), NamedArgument.op_Implicit(what), NamedArgument.op_Implicit(num.ToString()), NamedArgument.op_Implicit(text))), MessageTypeDefOf.PositiveEvent, false);
	}
}
