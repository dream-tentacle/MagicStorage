using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml;
using HaulersDream.Core;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HaulersDream;

[StaticConstructorOnStartup]
public class HaulersDreamSettings : ModSettings
{
	private enum SettingsCat
	{
		Features,
		Hauling,
		Unloading,
		BuildCraft,
		BulkLoading,
		Routing,
		Yields,
		Who,
		Planners,
		Advanced,
		Migration
	}

	private struct CatDef(SettingsCat cat, string icon, string nameKey, string helpKey)
	{
		public SettingsCat cat = cat;

		public string icon = icon;

		public string nameKey = nameKey;

		public string helpKey = helpKey;
	}

	private sealed class ResultGroup
	{
		public int CatId;

		public string Header;

		public OptionEntry Best;

		public readonly HashSet<int> Ordinals = new HashSet<int>();
	}

	private sealed class DefaultChange
	{
		public readonly string version;

		public readonly Dictionary<string, string> preChange;

		public DefaultChange(string version, Dictionary<string, string> preChange)
		{
			this.version = version;
			this.preChange = preChange;
		}
	}

	public bool masterEnabled = true;

	[NonSerialized]
	public bool drawDetourLines;

	public float carryLimitFraction = 1f;

	public float carryMassCapKg;

	public bool markForUnload = true;

	public bool unloadAllSurplus;

	public List<string> itemUnloadRules = new List<string>();

	[NonSerialized]
	private Dictionary<string, ItemUnloadRule> ruleMap;

	[NonSerialized]
	private List<string> keepDefNames;

	public bool unloadBeforeSleep = true;

	public bool unloadBeforeLeisure = true;

	public bool unloadBeforeEating = true;

	public bool shareForBuilding = true;

	public bool buildFromInventory = true;

	public bool buildFromInventoryPartial;

	public bool shareForCrafting = true;

	public bool inventoryCraftDeliver = true;

	public bool shareMeetInMiddle = true;

	public bool batchWorkDeliveries = true;

	public bool inventoryConstructDeliver = true;

	public bool multiSiteConstructDeliver = true;

	public bool shareHandHauledToStorage;

	public bool mealsOnWheels = true;

	public bool eatFromVehiclesAway;

	public bool medicineFromVehiclesAway;

	public bool buildFromVehiclesAway;

	public bool drugsForWithdrawal = true;

	public bool haulToStack = true;

	public bool orderedConstructTether = true;

	public bool haulToSiteOption = true;

	public bool planRoutes = true;

	public bool rememberPlan = true;

	public bool planCrafting = true;

	public bool planForUnassignedWork = true;

	public bool batchByDefault;

	public int defaultBatchSize = 10;

	public bool allowBatchUnderCommonSense = true;

	public bool bulkHaul = true;

	public BulkHaulTrigger bulkHaulTrigger = BulkHaulTrigger.SecondTasked;

	public bool haulNearbyOption = true;

	public bool manualPickupOption = true;

	public bool keepInInventoryOption = true;

	public bool keepInventoryGearButton = true;

	public int pickupDelayTicks = 120;

	public bool pickupDelayOnHauling;

	public bool pickupDelayOnLoading;

	public bool pickupDelayOnDirectHarvest;

	public bool haulOversizedInInventory = true;

	public bool bulkHaulCorpses = true;

	public float corpseCarryAllowance = 1f;

	public float corpseMaxHaulMassKg;

	public bool corpseHaulHumanlike = true;

	public bool corpseHaulByColonists = true;

	public bool corpseHaulByMechs = true;

	public bool corpseHaulByAnimals = true;

	public bool bulkHaulUrgent = true;

	public int bulkHaulUrgentRadius = 3;

	public bool bulkHaulUrgentIncludeNonUrgent;

	public bool sweepNearbyWhileWorking = true;

	public bool loadPackAnimalBulk = true;

	public bool autoDivertToPackAnimal = true;

	public bool enableBulkLoadTransporters = true;

	public int bulkLoadAiUpdateFrequency = 60;

	public bool enableBulkLoadPortal = true;

	public bool enableVehicleFramework = true;

	public bool enableBulkLoadVehicles = true;

	public bool enableStorageNetworkBulkLoad;

	public bool enableBulkRefuel = true;

	public bool cleanupOnSave = true;

	public bool enableSoftlockDrop = true;

	public bool enableQuestPawnDrop = true;

	public bool enableOpportunisticLoad;

	public int loadOpportunityScanRadius = 30;

	public bool enableContinuousLoading;

	public bool loadHybridPathing;

	public int loadPathfindingCandidates = 8;

	public bool autoOpenTransporterContents;

	public bool autoOpenCarrierGear;

	public bool enableBulkUnloadCarriers = true;

	public float minFreeSpaceToUnloadCarrierPct = 0.5f;

	public bool reserveCarrierOnUnload;

	public int visualUnloadDelay = 15;

	public AutoStripMode autoStripMode = AutoStripMode.AllHauls;

	public bool stripColonistCorpses;

	public bool stripBeforeCremation;

	public TaintedApparelPolicy taintedSmeltablePolicy;

	public TaintedApparelPolicy taintedNonSmeltablePolicy;

	public bool haulWildKills = true;

	public bool haulTamedSlaughter = true;

	public bool butcherSpoilingFirst = true;

	public bool cookSpoilingFirst = true;

	public bool cookMostStockFirst;

	public int overloadLevel = 5;

	public bool strictCarryWeight;

	public bool keepWorkingWhenFull;

	public int keepWorkingMarginCells = 5;

	public bool opportunisticUnload = true;

	public bool skipHaulWhileBleeding = true;

	public float bleedThresholdPerDay = 0.001f;

	public bool closestDestinationUnloadOrder = true;

	public bool enRoutePickup = true;

	public EnRoutePathChecker enRoutePathChecker;

	public OpportunisticDetour opportunisticDetour = OpportunisticDetour.Standard;

	public OpportunisticDetour unloadDetour = OpportunisticDetour.Short;

	public bool storageRouting = true;

	public bool routeSupplies = true;

	public bool routeIngredients = true;

	public bool routeToEqualPriority = true;

	public bool routeToStockpiles = true;

	public bool storageFiltersEnabled = true;

	public bool storageFilterUseDefaults = true;

	public bool storageFilterDenyLwmForOpportunistic = true;

	public StorageBuildingFilter storageBuildingFilter = new StorageBuildingFilter();

	public int routeMaxAmount = 50;

	public bool routeAllowHarvest = true;

	public int routeGrowthThreshold = 80;

	public RouteSelectionMethod routeSelectionMethod;

	public RouteDistanceBasis routeDistanceBasis;

	public int routeOrderExactMax = 12;

	public Dictionary<string, RouteDialogPrefs> routePrefsByDef = new Dictionary<string, RouteDialogPrefs>();

	public Dictionary<string, RouteDialogPrefs> rememberedRoutesByDef = new Dictionary<string, RouteDialogPrefs>();

	public Dictionary<string, SowRouteTemplate> rememberedSowRoutesByDef = new Dictionary<string, SowRouteTemplate>();

	public Dictionary<string, RemoveFloorRouteTemplate> rememberedRemoveFloorRoutesByDef = new Dictionary<string, RemoveFloorRouteTemplate>();

	public float craftBatchTimeoutHours = 2f;

	public bool allPawnsCanHaul;

	public bool allPawnsCanClean;

	public bool allPawnsCanCutPlants;

	public bool pauseWhileDrafted = true;

	public bool allowMechanoids = true;

	public float mechHaulMultiplier = 1f;

	public bool allowAnimals;

	public bool allowIncapable;

	public YieldBehavior yieldHarvest = YieldBehavior.DropThenHaul;

	public YieldBehavior yieldLogging = YieldBehavior.DropThenHaul;

	public YieldBehavior yieldMining = YieldBehavior.DropThenHaul;

	public YieldBehavior yieldChunks = YieldBehavior.DropThenHaul;

	public YieldBehavior yieldDeepDrill = YieldBehavior.DropThenHaul;

	public YieldBehavior yieldDeconstruct = YieldBehavior.DropThenHaul;

	public YieldBehavior yieldAnimals = YieldBehavior.DropThenHaul;

	public YieldBehavior yieldStrip = YieldBehavior.DropThenHaul;

	public YieldBehavior yieldUninstall = YieldBehavior.DropThenHaul;

	public YieldBehavior yieldFishing = YieldBehavior.DropThenHaul;

	[ProfileMeta]
	public int settingsSchemaVersion = 1;

	private const int CurrentSettingsSchema = 1;

	private static readonly string[] LegacyYieldNodeLabels = new string[8] { "pickupMode", "haulHarvest", "haulMining", "haulDeepDrill", "haulDeconstruct", "haulAnimals", "haulStrip", "haulUninstall" };

	public int unloadGraceTicks = 2500;

	public float intervalUnloadHours = 1f;

	public bool enableOnNonHomeMaps = true;

	public bool nonHomeMapsPlayerControlledOnly;

	public bool alertCannotUnload = true;

	public float alertStuckHours = 12f;

	public bool hideGizmo;

	public bool showAutoHaulGizmo;

	public bool showBenchGatherGizmo = true;

	public bool verboseLogging;

	public NotifyThreshold notifyThreshold;

	[ProfileMeta]
	public List<SettingsProfile> savedProfiles = new List<SettingsProfile>();

	[ProfileMeta]
	public string activeProfileName = "";

	[ProfileMeta]
	public string reporterId = "";

	[ProfileMeta]
	public Dictionary<string, long> notifySeenComment = new Dictionary<string, long>();

	[ProfileMeta]
	public Dictionary<string, long> notifyDismissed = new Dictionary<string, long>();

	public static bool SerializingSnapshot;

	[NonSerialized]
	private HaulersDreamSettings defaultsSnapshotCache;

	private static readonly CatDef[] cats = new CatDef[11]
	{
		new CatDef(SettingsCat.Features, "Features", "HaulersDream.Cat.Features", "HaulersDream.Cat.Features.Help"),
		new CatDef(SettingsCat.Hauling, "Hauling", "HaulersDream.Cat.Hauling", "HaulersDream.Cat.Hauling.Help"),
		new CatDef(SettingsCat.Unloading, "Unloading", "HaulersDream.Cat.Unloading", "HaulersDream.Cat.Unloading.Help"),
		new CatDef(SettingsCat.BuildCraft, "BuildCraft", "HaulersDream.Cat.BuildCraft", "HaulersDream.Cat.BuildCraft.Help"),
		new CatDef(SettingsCat.BulkLoading, "BulkLoading", "HaulersDream.Cat.BulkLoading", "HaulersDream.Cat.BulkLoading.Help"),
		new CatDef(SettingsCat.Routing, "Routing", "HaulersDream.Cat.Routing", "HaulersDream.Cat.Routing.Help"),
		new CatDef(SettingsCat.Yields, "Yields", "HaulersDream.Cat.Yields", "HaulersDream.Cat.Yields.Help"),
		new CatDef(SettingsCat.Who, "Who", "HaulersDream.Cat.Who", "HaulersDream.Cat.Who.Help"),
		new CatDef(SettingsCat.Planners, "Planners", "HaulersDream.Cat.Planners", "HaulersDream.Cat.Planners.Help"),
		new CatDef(SettingsCat.Advanced, "Advanced", "HaulersDream.Cat.Advanced", "HaulersDream.Cat.Advanced.Help"),
		new CatDef(SettingsCat.Migration, "Migration", "HaulersDream.Cat.Migration", "HaulersDream.Cat.Migration.Help")
	};

	private static readonly Color MigrationWarn = new Color(0.95f, 0.79f, 0.28f);

	private static SettingsCat currentCat = SettingsCat.Features;

	private static Vector2 contentScroll;

	private static Vector2 helpScroll;

	private static readonly float[] catHeight = new float[11]
	{
		1700f, 1700f, 1700f, 1700f, 1700f, 1700f, 1700f, 1700f, 1700f, 1700f,
		1700f
	};

	private static readonly Dictionary<SettingsCat, Texture2D> iconCache = new Dictionary<SettingsCat, Texture2D>();

	private static string searchQuery = "";

	private static List<OptionEntry> searchRegistry;

	private static List<OptionEntry> scoredResults;

	private static string scoredForQuery;

	private static int scoredTotal;

	private const int MaxSearchResults = 30;

	private static (int catId, int ordinal, float startY, int frames)? pendingNav;

	private static (int catId, int ordinal, float startY, float height)? highlightTarget;

	private static float highlightStart;

	private static Texture2D headerTexCache;

	private static bool headerTexResolved;

	private static Texture2D reportIconCache;

	private static bool reportIconResolved;

	private const float ContentScrollbarW = 16f;

	private const float ContentRightGutter = 12f;

	private static List<ResultGroup> groupedResults;

	private static float searchResultsHeight = 1700f;

	private const float HeaderRowH = 26f;

	private static FieldInfo[] settingFieldsCache;

	private const string LegacyConfigProbeXml = "<saveable><pickupMode>DirectToInventory</pickupMode><haulMining>False</haulMining></saveable>";

	private const char US = '\u001f';

	private const char RS = '\u001e';

	private static string currentModVersionCache;

	private static Dictionary<string, FieldInfo> settingFieldsByNameCache;

	private static readonly List<DefaultChange> DefaultChanges = new List<DefaultChange>();

	public string ReporterId
	{
		get
		{
			if (string.IsNullOrEmpty(reporterId))
			{
				reporterId = Guid.NewGuid().ToString("N");
				((ModSettings)this).Write();
			}
			return reporterId;
		}
	}

	private Dictionary<string, ItemUnloadRule> RuleMap
	{
		get
		{
			if (ruleMap == null)
			{
				ruleMap = new Dictionary<string, ItemUnloadRule>();
				if (itemUnloadRules != null)
				{
					foreach (string itemUnloadRule in itemUnloadRules)
					{
						if (TryDecodeRule(itemUnloadRule, out var defName, out var rule))
						{
							ruleMap[defName] = rule;
						}
					}
				}
			}
			return ruleMap;
		}
	}

	public int ItemRuleCount => RuleMap.Count;

	public bool HasAnySurplusProducingRule
	{
		get
		{
			foreach (KeyValuePair<string, ItemUnloadRule> item in RuleMap)
			{
				if (item.Value.mode != ItemUnloadMode.KeepAll)
				{
					return true;
				}
			}
			return false;
		}
	}

	private static Texture2D HeaderTex
	{
		get
		{
			if (!headerTexResolved)
			{
				headerTexCache = ContentFinder<Texture2D>.Get("HaulersDream/Settings/Header", false);
				headerTexResolved = true;
			}
			return headerTexCache;
		}
	}

	private static Texture2D ReportIcon
	{
		get
		{
			if (!reportIconResolved)
			{
				reportIconCache = ContentFinder<Texture2D>.Get("HaulersDream/Settings/Bug", false);
				reportIconResolved = true;
			}
			return reportIconCache;
		}
	}

	private static string OffLabel => TaggedString.op_Implicit(Translator.Translate("HaulersDream.Common.Off"));

	private static FieldInfo[] SettingFields
	{
		get
		{
			if (settingFieldsCache == null)
			{
				List<FieldInfo> list = new List<FieldInfo>();
				FieldInfo[] fields = typeof(HaulersDreamSettings).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (FieldInfo fieldInfo in fields)
				{
					if (!fieldInfo.IsNotSerialized && !fieldInfo.IsDefined(typeof(ProfileMetaAttribute), inherit: false))
					{
						list.Add(fieldInfo);
					}
				}
				settingFieldsCache = list.ToArray();
			}
			return settingFieldsCache;
		}
	}

	private HaulersDreamSettings DefaultsSnapshot
	{
		get
		{
			if (defaultsSnapshotCache == null)
			{
				HaulersDreamSettings haulersDreamSettings = new HaulersDreamSettings();
				haulersDreamSettings.ResetToDefaults();
				defaultsSnapshotCache = haulersDreamSettings;
			}
			return defaultsSnapshotCache;
		}
	}

	public SettingsProfile ActiveProfile
	{
		get
		{
			if (!string.IsNullOrEmpty(activeProfileName))
			{
				List<SettingsProfile> list = savedProfiles;
				if (list == null)
				{
					return null;
				}
				return GenCollection.FirstOrDefault<SettingsProfile>(list, (Predicate<SettingsProfile>)((SettingsProfile p) => p.name == activeProfileName));
			}
			return null;
		}
	}

	private HaulersDreamSettings BaselineState => ActiveProfile?.snapshot ?? DefaultsSnapshot;

	public bool IsDirty => !StateEquals(this, BaselineState);

	public string CurrentProfileLabel
	{
		get
		{
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			SettingsProfile activeProfile = ActiveProfile;
			if (activeProfile == null)
			{
				return TaggedString.op_Implicit(Translator.Translate(IsDirty ? "HaulersDream.Profile.Custom" : "HaulersDream.Profile.Default"));
			}
			return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate(IsDirty ? "HaulersDream.Profile.NamedDirty" : "HaulersDream.Profile.Named", NamedArgument.op_Implicit(activeProfile.name)));
		}
	}

	public static string CurrentModVersion
	{
		get
		{
			if (currentModVersionCache == null)
			{
				Version version = typeof(HaulersDreamSettings).Assembly.GetName().Version;
				currentModVersionCache = ((version == null) ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}");
			}
			return currentModVersionCache;
		}
	}

	private static Dictionary<string, FieldInfo> SettingFieldsByName
	{
		get
		{
			if (settingFieldsByNameCache == null)
			{
				settingFieldsByNameCache = new Dictionary<string, FieldInfo>();
				FieldInfo[] settingFields = SettingFields;
				foreach (FieldInfo fieldInfo in settingFields)
				{
					settingFieldsByNameCache[fieldInfo.Name] = fieldInfo;
				}
			}
			return settingFieldsByNameCache;
		}
	}

	public RouteDialogPrefs GetRoutePrefs(string defName)
	{
		if (defName == null || routePrefsByDef == null)
		{
			return null;
		}
		if (!routePrefsByDef.TryGetValue(defName, out var value))
		{
			return null;
		}
		return value;
	}

	public void SetRoutePrefs(string defName, RouteDialogPrefs prefs)
	{
		if (defName != null && prefs != null)
		{
			if (routePrefsByDef == null)
			{
				routePrefsByDef = new Dictionary<string, RouteDialogPrefs>();
			}
			routePrefsByDef[defName] = prefs;
		}
	}

	public RouteDialogPrefs GetRememberedRoute(string defName)
	{
		if (defName == null || rememberedRoutesByDef == null || !rememberedRoutesByDef.TryGetValue(defName, out var value))
		{
			return null;
		}
		return value;
	}

	public void SetRememberedRoute(string defName, RouteDialogPrefs template)
	{
		if (defName != null && template != null)
		{
			if (rememberedRoutesByDef == null)
			{
				rememberedRoutesByDef = new Dictionary<string, RouteDialogPrefs>();
			}
			rememberedRoutesByDef[defName] = template;
		}
	}

	public SowRouteTemplate GetRememberedSowRoute(string defName)
	{
		if (defName == null || rememberedSowRoutesByDef == null || !rememberedSowRoutesByDef.TryGetValue(defName, out var value))
		{
			return null;
		}
		return value;
	}

	public void SetRememberedSowRoute(string defName, SowRouteTemplate template)
	{
		if (defName != null && template != null)
		{
			if (rememberedSowRoutesByDef == null)
			{
				rememberedSowRoutesByDef = new Dictionary<string, SowRouteTemplate>();
			}
			rememberedSowRoutesByDef[defName] = template;
		}
	}

	public RemoveFloorRouteTemplate GetRememberedRemoveFloorRoute(string defName)
	{
		if (defName == null || rememberedRemoveFloorRoutesByDef == null || !rememberedRemoveFloorRoutesByDef.TryGetValue(defName, out var value))
		{
			return null;
		}
		return value;
	}

	public void SetRememberedRemoveFloorRoute(string defName, RemoveFloorRouteTemplate template)
	{
		if (defName != null && template != null)
		{
			if (rememberedRemoveFloorRoutesByDef == null)
			{
				rememberedRemoveFloorRoutesByDef = new Dictionary<string, RemoveFloorRouteTemplate>();
			}
			rememberedRemoveFloorRoutesByDef[defName] = template;
		}
	}

	public YieldBehavior BehaviorFor(HaulSourceType type)
	{
		return WorkTypePolicy.BehaviorFor(type, yieldHarvest, yieldLogging, yieldMining, yieldChunks, yieldDeepDrill, yieldDeconstruct, yieldAnimals, yieldStrip, yieldUninstall, yieldFishing);
	}

	public bool IsTypeEnabled(HaulSourceType type)
	{
		return BehaviorFor(type) != YieldBehavior.Disabled;
	}

	public float EffectiveCapacity(float maxCapacityKg)
	{
		return CarryMath.EffectiveCapacity(maxCapacityKg, carryLimitFraction);
	}

	public static string SelectionMethodLabel(RouteSelectionMethod m)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate((m == RouteSelectionMethod.NearestToTarget) ? "HaulersDream.PlanRoute.SelNearest" : "HaulersDream.PlanRoute.SelMostStops"));
	}

	public static string DistanceBasisLabel(RouteDistanceBasis b)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate((b == RouteDistanceBasis.WalkingPath) ? "HaulersDream.PlanRoute.DistWalking" : "HaulersDream.PlanRoute.DistStraight"));
	}

	public override void ExposeData()
	{
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Invalid comparison between Unknown and I4
		//IL_082b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0831: Invalid comparison between Unknown and I4
		//IL_08f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_08f7: Invalid comparison between Unknown and I4
		//IL_0942: Unknown result type (might be due to invalid IL or missing references)
		//IL_0948: Invalid comparison between Unknown and I4
		//IL_0aa7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0aad: Invalid comparison between Unknown and I4
		//IL_0c47: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c4d: Invalid comparison between Unknown and I4
		((ModSettings)this).ExposeData();
		Scribe_Values.Look<bool>(ref masterEnabled, "masterEnabled", true, false);
		Scribe_Values.Look<float>(ref carryLimitFraction, "carryLimitFraction", 1f, false);
		Scribe_Values.Look<float>(ref carryMassCapKg, "carryMassCapKg", 0f, false);
		Scribe_Values.Look<bool>(ref markForUnload, "markForUnload", true, false);
		Scribe_Values.Look<bool>(ref unloadBeforeSleep, "unloadBeforeSleep", true, false);
		Scribe_Values.Look<bool>(ref unloadBeforeLeisure, "unloadBeforeLeisure", true, false);
		Scribe_Values.Look<bool>(ref unloadBeforeEating, "unloadBeforeEating", true, false);
		Scribe_Values.Look<bool>(ref unloadAllSurplus, "unloadAllSurplus", false, false);
		Scribe_Collections.Look<string>(ref itemUnloadRules, "itemUnloadRules", (LookMode)1, Array.Empty<object>());
		if ((int)Scribe.mode == 2)
		{
			if (itemUnloadRules == null)
			{
				itemUnloadRules = new List<string>();
			}
			keepDefNames = null;
			Scribe_Collections.Look<string>(ref keepDefNames, "keepDefNames", (LookMode)1, Array.Empty<object>());
			MigrateLegacyKeepDefNames();
			ruleMap = null;
		}
		Scribe_Values.Look<bool>(ref shareForBuilding, "shareForBuilding", true, false);
		Scribe_Values.Look<bool>(ref buildFromInventory, "buildFromInventory", true, false);
		Scribe_Values.Look<bool>(ref buildFromInventoryPartial, "buildFromInventoryPartial", false, false);
		Scribe_Values.Look<bool>(ref shareForCrafting, "shareForCrafting", true, false);
		Scribe_Values.Look<bool>(ref inventoryCraftDeliver, "inventoryCraftDeliver", true, false);
		Scribe_Values.Look<bool>(ref shareMeetInMiddle, "shareMeetInMiddle", true, false);
		Scribe_Values.Look<bool>(ref batchWorkDeliveries, "batchWorkDeliveries", true, false);
		Scribe_Values.Look<bool>(ref inventoryConstructDeliver, "inventoryConstructDeliver", true, false);
		Scribe_Values.Look<bool>(ref multiSiteConstructDeliver, "multiSiteConstructDeliver", true, false);
		Scribe_Values.Look<bool>(ref shareHandHauledToStorage, "shareHandHauledToStorage", false, false);
		Scribe_Values.Look<bool>(ref mealsOnWheels, "mealsOnWheels", true, false);
		Scribe_Values.Look<bool>(ref eatFromVehiclesAway, "eatFromVehiclesAway", false, false);
		Scribe_Values.Look<bool>(ref medicineFromVehiclesAway, "medicineFromVehiclesAway", false, false);
		Scribe_Values.Look<bool>(ref drugsForWithdrawal, "drugsForWithdrawal", true, false);
		Scribe_Values.Look<bool>(ref buildFromVehiclesAway, "buildFromVehiclesAway", false, false);
		Scribe_Values.Look<bool>(ref bulkHaul, "bulkHaul", true, false);
		Scribe_Values.Look<BulkHaulTrigger>(ref bulkHaulTrigger, "bulkHaulTrigger", BulkHaulTrigger.SecondTasked, false);
		Scribe_Values.Look<bool>(ref haulNearbyOption, "haulNearbyOption", true, false);
		Scribe_Values.Look<bool>(ref manualPickupOption, "manualPickupOption", true, false);
		Scribe_Values.Look<bool>(ref keepInInventoryOption, "keepInInventoryOption", true, false);
		Scribe_Values.Look<bool>(ref keepInventoryGearButton, "keepInventoryGearButton", true, false);
		Scribe_Values.Look<int>(ref pickupDelayTicks, "pickupDelayTicks", 120, false);
		Scribe_Values.Look<bool>(ref pickupDelayOnHauling, "pickupDelayOnHauling", false, false);
		Scribe_Values.Look<bool>(ref pickupDelayOnLoading, "pickupDelayOnLoading", false, false);
		Scribe_Values.Look<bool>(ref pickupDelayOnDirectHarvest, "pickupDelayOnDirectHarvest", false, false);
		Scribe_Values.Look<bool>(ref haulOversizedInInventory, "haulOversizedInInventory", true, false);
		Scribe_Values.Look<bool>(ref bulkHaulCorpses, "bulkHaulCorpses", true, false);
		Scribe_Values.Look<float>(ref corpseCarryAllowance, "corpseCarryAllowance", 1f, false);
		Scribe_Values.Look<float>(ref corpseMaxHaulMassKg, "corpseMaxHaulMassKg", 0f, false);
		Scribe_Values.Look<bool>(ref corpseHaulHumanlike, "corpseHaulHumanlike", true, false);
		Scribe_Values.Look<bool>(ref corpseHaulByColonists, "corpseHaulByColonists", true, false);
		Scribe_Values.Look<bool>(ref corpseHaulByMechs, "corpseHaulByMechs", true, false);
		Scribe_Values.Look<bool>(ref corpseHaulByAnimals, "corpseHaulByAnimals", true, false);
		Scribe_Values.Look<bool>(ref bulkHaulUrgent, "bulkHaulUrgent", true, false);
		Scribe_Values.Look<int>(ref bulkHaulUrgentRadius, "bulkHaulUrgentRadius", 3, false);
		Scribe_Values.Look<bool>(ref bulkHaulUrgentIncludeNonUrgent, "bulkHaulUrgentIncludeNonUrgent", false, false);
		Scribe_Values.Look<bool>(ref sweepNearbyWhileWorking, "sweepNearbyWhileWorking", true, false);
		Scribe_Values.Look<bool>(ref loadPackAnimalBulk, "loadPackAnimalBulk", true, false);
		Scribe_Values.Look<bool>(ref autoDivertToPackAnimal, "autoDivertToPackAnimal", true, false);
		Scribe_Values.Look<bool>(ref enableBulkLoadTransporters, "enableBulkLoadTransporters", true, false);
		Scribe_Values.Look<int>(ref bulkLoadAiUpdateFrequency, "bulkLoadAiUpdateFrequency", 60, false);
		Scribe_Values.Look<bool>(ref enableBulkLoadPortal, "enableBulkLoadPortal", true, false);
		Scribe_Values.Look<bool>(ref enableVehicleFramework, "enableVehicleFramework", true, false);
		Scribe_Values.Look<bool>(ref enableBulkLoadVehicles, "enableBulkLoadVehicles", true, false);
		Scribe_Values.Look<bool>(ref enableStorageNetworkBulkLoad, "enableStorageNetworkBulkLoad", false, false);
		Scribe_Values.Look<bool>(ref enableBulkRefuel, "enableBulkRefuel", true, false);
		Scribe_Values.Look<bool>(ref enableBulkUnloadCarriers, "enableBulkUnloadCarriers", true, false);
		Scribe_Values.Look<bool>(ref cleanupOnSave, "cleanupOnSave", true, false);
		Scribe_Values.Look<bool>(ref enableSoftlockDrop, "enableSoftlockDrop", true, false);
		Scribe_Values.Look<bool>(ref enableQuestPawnDrop, "enableQuestPawnDrop", true, false);
		Scribe_Values.Look<bool>(ref enableOpportunisticLoad, "enableOpportunisticLoad", false, false);
		Scribe_Values.Look<int>(ref loadOpportunityScanRadius, "loadOpportunityScanRadius", 30, false);
		Scribe_Values.Look<bool>(ref enableContinuousLoading, "enableContinuousLoading", false, false);
		Scribe_Values.Look<bool>(ref loadHybridPathing, "loadHybridPathing", false, false);
		Scribe_Values.Look<int>(ref loadPathfindingCandidates, "loadPathfindingCandidates", 8, false);
		Scribe_Values.Look<bool>(ref autoOpenTransporterContents, "autoOpenTransporterContents", false, false);
		Scribe_Values.Look<bool>(ref autoOpenCarrierGear, "autoOpenCarrierGear", false, false);
		Scribe_Values.Look<float>(ref minFreeSpaceToUnloadCarrierPct, "minFreeSpaceToUnloadCarrierPct", 0.5f, false);
		Scribe_Values.Look<bool>(ref reserveCarrierOnUnload, "reserveCarrierOnUnload", false, false);
		Scribe_Values.Look<int>(ref visualUnloadDelay, "visualUnloadDelay", 15, false);
		Scribe_Values.Look<bool>(ref haulToStack, "haulToStack", true, false);
		Scribe_Values.Look<bool>(ref orderedConstructTether, "orderedConstructTether", true, false);
		Scribe_Values.Look<bool>(ref haulToSiteOption, "haulToSiteOption", true, false);
		Scribe_Values.Look<bool>(ref planRoutes, "planRoutes", true, false);
		Scribe_Values.Look<bool>(ref rememberPlan, "rememberPlan", true, false);
		Scribe_Values.Look<bool>(ref planCrafting, "planCrafting", true, false);
		Scribe_Values.Look<bool>(ref planForUnassignedWork, "planForUnassignedWork", true, false);
		Scribe_Values.Look<bool>(ref batchByDefault, "batchByDefault", false, false);
		Scribe_Values.Look<int>(ref defaultBatchSize, "defaultBatchSize", 10, false);
		Scribe_Values.Look<bool>(ref allowBatchUnderCommonSense, "allowBatchUnderCommonSense", true, false);
		Scribe_Values.Look<AutoStripMode>(ref autoStripMode, "autoStripMode", AutoStripMode.AllHauls, false);
		Scribe_Values.Look<bool>(ref stripColonistCorpses, "stripColonistCorpses", false, false);
		Scribe_Values.Look<bool>(ref stripBeforeCremation, "stripBeforeCremation", false, false);
		Scribe_Values.Look<TaintedApparelPolicy>(ref taintedSmeltablePolicy, "taintedSmeltablePolicy", TaintedApparelPolicy.Take, false);
		Scribe_Values.Look<TaintedApparelPolicy>(ref taintedNonSmeltablePolicy, "taintedNonSmeltablePolicy", TaintedApparelPolicy.Take, false);
		Scribe_Values.Look<bool>(ref haulWildKills, "haulWildKills", true, false);
		Scribe_Values.Look<bool>(ref haulTamedSlaughter, "haulTamedSlaughter", true, false);
		Scribe_Values.Look<bool>(ref butcherSpoilingFirst, "butcherSpoilingFirst", true, false);
		Scribe_Values.Look<bool>(ref cookSpoilingFirst, "cookSpoilingFirst", true, false);
		Scribe_Values.Look<bool>(ref cookMostStockFirst, "cookMostStockFirst", false, false);
		Scribe_Values.Look<int>(ref overloadLevel, "overloadLevel", 5, false);
		Scribe_Values.Look<bool>(ref strictCarryWeight, "strictCarryWeight", false, false);
		Scribe_Values.Look<bool>(ref keepWorkingWhenFull, "keepWorkingWhenFull", false, false);
		Scribe_Values.Look<int>(ref keepWorkingMarginCells, "keepWorkingMarginCells", 5, false);
		Scribe_Values.Look<bool>(ref opportunisticUnload, "opportunisticUnload", true, false);
		Scribe_Values.Look<bool>(ref skipHaulWhileBleeding, "skipHaulWhileBleeding", true, false);
		Scribe_Values.Look<float>(ref bleedThresholdPerDay, "bleedThresholdPerDay", 0.001f, false);
		Scribe_Values.Look<bool>(ref closestDestinationUnloadOrder, "closestDestinationUnloadOrder", true, false);
		Scribe_Values.Look<bool>(ref enRoutePickup, "enRoutePickup", true, false);
		Scribe_Values.Look<EnRoutePathChecker>(ref enRoutePathChecker, "enRoutePathChecker", EnRoutePathChecker.Vanilla, false);
		Scribe_Values.Look<OpportunisticDetour>(ref opportunisticDetour, "opportunisticDetour", OpportunisticDetour.Standard, false);
		Scribe_Values.Look<OpportunisticDetour>(ref unloadDetour, "unloadDetour", OpportunisticDetour.Short, false);
		Scribe_Values.Look<bool>(ref storageRouting, "storageRouting", true, false);
		Scribe_Values.Look<bool>(ref routeSupplies, "routeSupplies", true, false);
		Scribe_Values.Look<bool>(ref routeIngredients, "routeIngredients", true, false);
		Scribe_Values.Look<bool>(ref routeToEqualPriority, "routeToEqualPriority", true, false);
		Scribe_Values.Look<bool>(ref routeToStockpiles, "routeToStockpiles", true, false);
		Scribe_Values.Look<bool>(ref storageFiltersEnabled, "storageFiltersEnabled", true, false);
		Scribe_Values.Look<bool>(ref storageFilterUseDefaults, "storageFilterUseDefaults", true, false);
		Scribe_Values.Look<bool>(ref storageFilterDenyLwmForOpportunistic, "storageFilterDenyLwmForOpportunistic", true, false);
		Scribe_Deep.Look<StorageBuildingFilter>(ref storageBuildingFilter, "storageBuildingFilter", Array.Empty<object>());
		if ((int)Scribe.mode == 2 && storageBuildingFilter == null)
		{
			storageBuildingFilter = new StorageBuildingFilter();
		}
		Scribe_Values.Look<bool>(ref routeAllowHarvest, "routeAllowHarvest", true, false);
		Scribe_Values.Look<int>(ref routeGrowthThreshold, "routeGrowthThreshold", 80, false);
		Scribe_Values.Look<int>(ref routeMaxAmount, "routeMaxAmount", 50, false);
		Scribe_Values.Look<RouteSelectionMethod>(ref routeSelectionMethod, "routeSelectionMethod", RouteSelectionMethod.MostStopsPerTravel, false);
		Scribe_Values.Look<RouteDistanceBasis>(ref routeDistanceBasis, "routeDistanceBasis", RouteDistanceBasis.StraightLine, false);
		Scribe_Values.Look<int>(ref routeOrderExactMax, "routeOrderExactMax", 12, false);
		routeOrderExactMax = Mathf.Clamp(routeOrderExactMax, 1, 16);
		Scribe_Values.Look<float>(ref craftBatchTimeoutHours, "craftBatchTimeoutHours", 2f, false);
		Scribe_Collections.Look<string, RouteDialogPrefs>(ref routePrefsByDef, "routePrefsByDef", (LookMode)1, (LookMode)2);
		if ((int)Scribe.mode == 2 && routePrefsByDef == null)
		{
			routePrefsByDef = new Dictionary<string, RouteDialogPrefs>();
		}
		Scribe_Collections.Look<string, RouteDialogPrefs>(ref rememberedRoutesByDef, "rememberedRoutesByDef", (LookMode)1, (LookMode)2);
		Scribe_Collections.Look<string, SowRouteTemplate>(ref rememberedSowRoutesByDef, "rememberedSowRoutesByDef", (LookMode)1, (LookMode)2);
		Scribe_Collections.Look<string, RemoveFloorRouteTemplate>(ref rememberedRemoveFloorRoutesByDef, "rememberedRemoveFloorRoutesByDef", (LookMode)1, (LookMode)2);
		if ((int)Scribe.mode == 2)
		{
			if (rememberedRoutesByDef == null)
			{
				rememberedRoutesByDef = new Dictionary<string, RouteDialogPrefs>();
			}
			if (rememberedSowRoutesByDef == null)
			{
				rememberedSowRoutesByDef = new Dictionary<string, SowRouteTemplate>();
			}
			if (rememberedRemoveFloorRoutesByDef == null)
			{
				rememberedRemoveFloorRoutesByDef = new Dictionary<string, RemoveFloorRouteTemplate>();
			}
		}
		Scribe_Values.Look<bool>(ref pauseWhileDrafted, "pauseWhileDrafted", true, false);
		Scribe_Values.Look<bool>(ref allowMechanoids, "allowMechanoids", true, false);
		Scribe_Values.Look<float>(ref mechHaulMultiplier, "mechHaulMultiplier", 1f, false);
		Scribe_Values.Look<bool>(ref allowAnimals, "allowAnimals", false, false);
		Scribe_Values.Look<bool>(ref allowIncapable, "allowIncapable", false, false);
		Scribe_Values.Look<YieldBehavior>(ref yieldHarvest, "yieldHarvest", YieldBehavior.DropThenHaul, false);
		Scribe_Values.Look<YieldBehavior>(ref yieldLogging, "yieldLogging", YieldBehavior.DropThenHaul, false);
		Scribe_Values.Look<YieldBehavior>(ref yieldMining, "yieldMining", YieldBehavior.DropThenHaul, false);
		Scribe_Values.Look<YieldBehavior>(ref yieldChunks, "yieldChunks", YieldBehavior.DropThenHaul, false);
		Scribe_Values.Look<YieldBehavior>(ref yieldDeepDrill, "yieldDeepDrill", YieldBehavior.DropThenHaul, false);
		Scribe_Values.Look<YieldBehavior>(ref yieldDeconstruct, "yieldDeconstruct", YieldBehavior.DropThenHaul, false);
		Scribe_Values.Look<YieldBehavior>(ref yieldAnimals, "yieldAnimals", YieldBehavior.DropThenHaul, false);
		Scribe_Values.Look<YieldBehavior>(ref yieldStrip, "yieldStrip", YieldBehavior.DropThenHaul, false);
		Scribe_Values.Look<YieldBehavior>(ref yieldUninstall, "yieldUninstall", YieldBehavior.DropThenHaul, false);
		Scribe_Values.Look<YieldBehavior>(ref yieldFishing, "yieldFishing", YieldBehavior.DropThenHaul, false);
		Scribe_Values.Look<int>(ref settingsSchemaVersion, "settingsSchemaVersion", 0, false);
		if ((int)Scribe.mode == 2)
		{
			if (SettingsSchemaPolicy.ShouldMigrateLegacyYields(settingsSchemaVersion, 1, HasLegacyYieldNodes()))
			{
				MigrateLegacyYieldSettings();
			}
			settingsSchemaVersion = 1;
		}
		Scribe_Values.Look<bool>(ref allPawnsCanHaul, "allPawnsCanHaul", false, false);
		Scribe_Values.Look<bool>(ref allPawnsCanClean, "allPawnsCanClean", false, false);
		Scribe_Values.Look<bool>(ref allPawnsCanCutPlants, "allPawnsCanCutPlants", false, false);
		Scribe_Values.Look<int>(ref unloadGraceTicks, "unloadGraceTicks", 2500, false);
		Scribe_Values.Look<float>(ref intervalUnloadHours, "intervalUnloadHours", 1f, false);
		Scribe_Values.Look<bool>(ref alertCannotUnload, "alertCannotUnload", true, false);
		Scribe_Values.Look<float>(ref alertStuckHours, "alertStuckHours", 12f, false);
		Scribe_Values.Look<bool>(ref enableOnNonHomeMaps, "enableOnNonHomeMaps", true, false);
		Scribe_Values.Look<bool>(ref nonHomeMapsPlayerControlledOnly, "nonHomeMapsPlayerControlledOnly", false, false);
		Scribe_Values.Look<bool>(ref hideGizmo, "hideGizmo", false, false);
		Scribe_Values.Look<bool>(ref showAutoHaulGizmo, "showAutoHaulGizmo", false, false);
		Scribe_Values.Look<bool>(ref showBenchGatherGizmo, "showBenchGatherGizmo", true, false);
		Scribe_Values.Look<bool>(ref verboseLogging, "verboseLogging", false, false);
		Scribe_Values.Look<NotifyThreshold>(ref notifyThreshold, "notifyThreshold", NotifyThreshold.All, false);
		if (SerializingSnapshot)
		{
			return;
		}
		Scribe_Collections.Look<SettingsProfile>(ref savedProfiles, "savedProfiles", (LookMode)2, Array.Empty<object>());
		Scribe_Values.Look<string>(ref activeProfileName, "activeProfileName", "", false);
		Scribe_Values.Look<string>(ref reporterId, "reporterId", "", false);
		Scribe_Collections.Look<string, long>(ref notifySeenComment, "notifySeenComment", (LookMode)1, (LookMode)1);
		Scribe_Collections.Look<string, long>(ref notifyDismissed, "notifyDismissed", (LookMode)1, (LookMode)1);
		if ((int)Scribe.mode == 2)
		{
			if (savedProfiles == null)
			{
				savedProfiles = new List<SettingsProfile>();
			}
			if (activeProfileName == null)
			{
				activeProfileName = "";
			}
			if (reporterId == null)
			{
				reporterId = "";
			}
			if (notifySeenComment == null)
			{
				notifySeenComment = new Dictionary<string, long>();
			}
			if (notifyDismissed == null)
			{
				notifyDismissed = new Dictionary<string, long>();
			}
		}
	}

	public void ResetToDefaults()
	{
		masterEnabled = true;
		carryLimitFraction = 1f;
		carryMassCapKg = 0f;
		markForUnload = true;
		unloadBeforeSleep = true;
		unloadBeforeLeisure = true;
		unloadBeforeEating = true;
		unloadAllSurplus = false;
		itemUnloadRules = new List<string>();
		ruleMap = null;
		shareForBuilding = true;
		buildFromInventory = true;
		buildFromInventoryPartial = false;
		shareForCrafting = true;
		inventoryCraftDeliver = true;
		shareMeetInMiddle = true;
		batchWorkDeliveries = true;
		inventoryConstructDeliver = true;
		multiSiteConstructDeliver = true;
		shareHandHauledToStorage = false;
		mealsOnWheels = true;
		eatFromVehiclesAway = false;
		medicineFromVehiclesAway = false;
		drugsForWithdrawal = true;
		buildFromVehiclesAway = false;
		bulkHaul = true;
		bulkHaulTrigger = BulkHaulTrigger.SecondTasked;
		haulNearbyOption = true;
		manualPickupOption = true;
		keepInInventoryOption = true;
		keepInventoryGearButton = true;
		pickupDelayTicks = 120;
		pickupDelayOnHauling = false;
		pickupDelayOnLoading = false;
		pickupDelayOnDirectHarvest = false;
		haulOversizedInInventory = true;
		bulkHaulCorpses = true;
		corpseCarryAllowance = 1f;
		corpseMaxHaulMassKg = 0f;
		corpseHaulHumanlike = true;
		corpseHaulByColonists = true;
		corpseHaulByMechs = true;
		corpseHaulByAnimals = true;
		bulkHaulUrgent = true;
		bulkHaulUrgentRadius = 3;
		bulkHaulUrgentIncludeNonUrgent = false;
		sweepNearbyWhileWorking = true;
		loadPackAnimalBulk = true;
		autoDivertToPackAnimal = true;
		enableBulkLoadTransporters = true;
		bulkLoadAiUpdateFrequency = 60;
		enableBulkLoadPortal = true;
		enableVehicleFramework = true;
		enableBulkLoadVehicles = true;
		enableStorageNetworkBulkLoad = false;
		enableBulkRefuel = true;
		enableBulkUnloadCarriers = true;
		cleanupOnSave = true;
		enableSoftlockDrop = true;
		enableQuestPawnDrop = true;
		enableOpportunisticLoad = false;
		loadOpportunityScanRadius = 30;
		enableContinuousLoading = false;
		loadHybridPathing = false;
		loadPathfindingCandidates = 8;
		autoOpenTransporterContents = false;
		autoOpenCarrierGear = false;
		minFreeSpaceToUnloadCarrierPct = 0.5f;
		reserveCarrierOnUnload = false;
		visualUnloadDelay = 15;
		haulToStack = true;
		orderedConstructTether = true;
		haulToSiteOption = true;
		planRoutes = true;
		rememberPlan = true;
		planCrafting = true;
		planForUnassignedWork = true;
		batchByDefault = false;
		defaultBatchSize = 10;
		allowBatchUnderCommonSense = true;
		autoStripMode = AutoStripMode.AllHauls;
		stripColonistCorpses = false;
		stripBeforeCremation = false;
		taintedSmeltablePolicy = TaintedApparelPolicy.Take;
		taintedNonSmeltablePolicy = TaintedApparelPolicy.Take;
		haulWildKills = true;
		haulTamedSlaughter = true;
		butcherSpoilingFirst = true;
		cookSpoilingFirst = true;
		cookMostStockFirst = false;
		overloadLevel = 5;
		strictCarryWeight = false;
		keepWorkingWhenFull = false;
		keepWorkingMarginCells = 5;
		opportunisticUnload = true;
		skipHaulWhileBleeding = true;
		bleedThresholdPerDay = 0.001f;
		closestDestinationUnloadOrder = true;
		enRoutePickup = true;
		enRoutePathChecker = EnRoutePathChecker.Vanilla;
		opportunisticDetour = OpportunisticDetour.Standard;
		unloadDetour = OpportunisticDetour.Short;
		storageRouting = true;
		routeSupplies = true;
		routeIngredients = true;
		routeToEqualPriority = true;
		routeToStockpiles = true;
		storageFiltersEnabled = true;
		storageFilterUseDefaults = true;
		storageFilterDenyLwmForOpportunistic = true;
		storageBuildingFilter = new StorageBuildingFilter();
		routeAllowHarvest = true;
		routeGrowthThreshold = 80;
		routeMaxAmount = 50;
		routeSelectionMethod = RouteSelectionMethod.MostStopsPerTravel;
		routeDistanceBasis = RouteDistanceBasis.StraightLine;
		routeOrderExactMax = 12;
		craftBatchTimeoutHours = 2f;
		routePrefsByDef = new Dictionary<string, RouteDialogPrefs>();
		rememberedRoutesByDef = new Dictionary<string, RouteDialogPrefs>();
		rememberedSowRoutesByDef = new Dictionary<string, SowRouteTemplate>();
		rememberedRemoveFloorRoutesByDef = new Dictionary<string, RemoveFloorRouteTemplate>();
		pauseWhileDrafted = true;
		allowMechanoids = true;
		mechHaulMultiplier = 1f;
		allowAnimals = false;
		allowIncapable = false;
		yieldHarvest = YieldBehavior.DropThenHaul;
		yieldLogging = YieldBehavior.DropThenHaul;
		yieldMining = YieldBehavior.DropThenHaul;
		yieldChunks = YieldBehavior.DropThenHaul;
		yieldDeepDrill = YieldBehavior.DropThenHaul;
		yieldDeconstruct = YieldBehavior.DropThenHaul;
		yieldAnimals = YieldBehavior.DropThenHaul;
		yieldStrip = YieldBehavior.DropThenHaul;
		yieldUninstall = YieldBehavior.DropThenHaul;
		yieldFishing = YieldBehavior.DropThenHaul;
		settingsSchemaVersion = 1;
		allPawnsCanHaul = false;
		allPawnsCanClean = false;
		allPawnsCanCutPlants = false;
		unloadGraceTicks = 2500;
		intervalUnloadHours = 1f;
		alertCannotUnload = true;
		alertStuckHours = 12f;
		enableOnNonHomeMaps = true;
		nonHomeMapsPlayerControlledOnly = false;
		hideGizmo = false;
		showAutoHaulGizmo = false;
		showBenchGatherGizmo = true;
		verboseLogging = false;
		notifyThreshold = NotifyThreshold.All;
	}

	public bool TryGetItemRule(ThingDef def, out ItemUnloadRule rule)
	{
		rule = default(ItemUnloadRule);
		if (def != null)
		{
			return RuleMap.TryGetValue(((Def)def).defName, out rule);
		}
		return false;
	}

	public bool RuleProducesSurplus(ThingDef def)
	{
		if (def != null && RuleMap.TryGetValue(((Def)def).defName, out var value))
		{
			return value.mode != ItemUnloadMode.KeepAll;
		}
		return false;
	}

	public Dictionary<string, ItemUnloadRule> GetItemRulesCopy()
	{
		return new Dictionary<string, ItemUnloadRule>(RuleMap);
	}

	public void SetItemRules(Dictionary<string, ItemUnloadRule> map)
	{
		itemUnloadRules = new List<string>();
		if (map != null)
		{
			foreach (KeyValuePair<string, ItemUnloadRule> item in map)
			{
				itemUnloadRules.Add(EncodeRule(item.Key, item.Value));
			}
		}
		ruleMap = null;
	}

	private static string EncodeRule(string defName, ItemUnloadRule rule)
	{
		string[] obj = new string[5] { defName, "|", null, null, null };
		int mode = (int)rule.mode;
		obj[2] = mode.ToString();
		obj[3] = "|";
		obj[4] = rule.amount.ToString();
		return string.Concat(obj);
	}

	private static bool TryDecodeRule(string entry, out string defName, out ItemUnloadRule rule)
	{
		defName = null;
		rule = default(ItemUnloadRule);
		if (string.IsNullOrEmpty(entry))
		{
			return false;
		}
		string[] array = entry.Split('|', StringSplitOptions.None);
		if (array.Length < 1 || string.IsNullOrEmpty(array[0]))
		{
			return false;
		}
		defName = array[0];
		int result = 0;
		int result2 = 0;
		if (array.Length >= 2)
		{
			int.TryParse(array[1], out result);
		}
		if (array.Length >= 3)
		{
			int.TryParse(array[2], out result2);
		}
		rule = new ItemUnloadRule((ItemUnloadMode)Mathf.Clamp(result, 0, 2), Mathf.Max(0, result2));
		return true;
	}

	private void MigrateLegacyKeepDefNames()
	{
		if (keepDefNames == null || keepDefNames.Count == 0)
		{
			return;
		}
		HashSet<string> hashSet = new HashSet<string>();
		foreach (string itemUnloadRule in itemUnloadRules)
		{
			if (TryDecodeRule(itemUnloadRule, out var defName, out var _))
			{
				hashSet.Add(defName);
			}
		}
		foreach (string keepDefName in keepDefNames)
		{
			if (!string.IsNullOrEmpty(keepDefName) && hashSet.Add(keepDefName))
			{
				itemUnloadRules.Add(EncodeRule(keepDefName, new ItemUnloadRule(ItemUnloadMode.KeepAll)));
			}
		}
		keepDefNames = null;
	}

	private static bool HasLegacyYieldNodes()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		if ((int)Scribe.mode != 2)
		{
			return false;
		}
		XmlNode xmlNode = Scribe.loader?.curXmlParent;
		if (xmlNode == null)
		{
			return false;
		}
		for (int i = 0; i < LegacyYieldNodeLabels.Length; i++)
		{
			if (xmlNode[LegacyYieldNodeLabels[i]] != null)
			{
				return true;
			}
		}
		return false;
	}

	private void MigrateLegacyYieldSettings()
	{
		PickupMode legacyPickupMode = PickupMode.DropThenHaul;
		bool enabled = true;
		bool enabled2 = true;
		bool enabled3 = true;
		bool enabled4 = true;
		bool enabled5 = true;
		bool enabled6 = true;
		bool enabled7 = true;
		Scribe_Values.Look<PickupMode>(ref legacyPickupMode, "pickupMode", PickupMode.DropThenHaul, false);
		Scribe_Values.Look<bool>(ref enabled, "haulHarvest", true, false);
		Scribe_Values.Look<bool>(ref enabled2, "haulMining", true, false);
		Scribe_Values.Look<bool>(ref enabled3, "haulDeepDrill", true, false);
		Scribe_Values.Look<bool>(ref enabled4, "haulDeconstruct", true, false);
		Scribe_Values.Look<bool>(ref enabled5, "haulAnimals", true, false);
		Scribe_Values.Look<bool>(ref enabled6, "haulStrip", true, false);
		Scribe_Values.Look<bool>(ref enabled7, "haulUninstall", true, false);
		yieldHarvest = WorkTypePolicy.MapLegacyYield(enabled, legacyPickupMode, forceDropOnly: false);
		yieldLogging = WorkTypePolicy.MapLegacyYield(enabled, legacyPickupMode, forceDropOnly: false);
		yieldMining = WorkTypePolicy.MapLegacyYield(enabled2, legacyPickupMode, forceDropOnly: false);
		yieldChunks = WorkTypePolicy.MapLegacyYield(enabled2, legacyPickupMode, forceDropOnly: false);
		yieldDeepDrill = WorkTypePolicy.MapLegacyYield(enabled3, legacyPickupMode, forceDropOnly: false);
		yieldDeconstruct = WorkTypePolicy.MapLegacyYield(enabled4, legacyPickupMode, forceDropOnly: false);
		yieldAnimals = WorkTypePolicy.MapLegacyYield(enabled5, legacyPickupMode, forceDropOnly: false);
		yieldStrip = WorkTypePolicy.MapLegacyYield(enabled6, legacyPickupMode, forceDropOnly: true);
		yieldUninstall = WorkTypePolicy.MapLegacyYield(enabled7, legacyPickupMode, forceDropOnly: false);
	}

	private static string OverloadLevelLabel(int lv)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		lv = OverloadTuning.ClampLevel(lv);
		if (lv == 0)
		{
			return TaggedString.op_Implicit(Translator.Translate("HaulersDream.Overload.Free"));
		}
		if (lv >= 10)
		{
			return TaggedString.op_Implicit(Translator.Translate("HaulersDream.Overload.Off"));
		}
		if (lv == 5)
		{
			return TaggedString.op_Implicit(Translator.Translate("HaulersDream.Overload.Fair"));
		}
		return TaggedString.op_Implicit((lv < 5) ? Translator.Translate("HaulersDream.Overload.Eager") : Translator.Translate("HaulersDream.Overload.Cautious"));
	}

	private static string[] DetourSegmentLabels()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		return new string[4]
		{
			((object)Translator.Translate("HaulersDream.Setting.Detour.Off.S")/*cast due to constrained. prefix*/).ToString(),
			Numbered("HaulersDream.Setting.Detour.Short.S", 4),
			Numbered("HaulersDream.Setting.Detour.Standard.S", 10),
			Numbered("HaulersDream.Setting.Detour.Long.S", 20)
		};
		static string Numbered(string key, int tiles)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return $"{Translator.Translate(key)} ({tiles})";
		}
	}

	private static string[] DetourSegmentHelp()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		return new string[4]
		{
			((object)Translator.Translate("HaulersDream.Setting.Detour.Off.H")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.Detour.Short.H")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.Detour.Standard.H")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.Detour.Long.H")/*cast due to constrained. prefix*/).ToString()
		};
	}

	private static Texture2D IconFor(CatDef cd)
	{
		if (!iconCache.TryGetValue(cd.cat, out var value))
		{
			value = ContentFinder<Texture2D>.Get("HaulersDream/Settings/" + cd.icon, false);
			iconCache[cd.cat] = value;
		}
		return value;
	}

	public void DoWindowContents(Rect rect)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0305: Unknown result type (might be due to invalid IL or missing references)
		//IL_030d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0315: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		HDSettingsUI.ResetHover();
		Window val = Find.WindowStack?.currentlyDrawnWindow;
		if (val != null)
		{
			val.draggable = true;
			val.doCloseButton = false;
			val.doCloseX = false;
		}
		float width = ((Rect)(ref rect)).width;
		Widgets.DrawBoxSolid(new Rect(0f, 0f, width, 60f), Widgets.WindowBGFillColor);
		Texture2D headerTex = HeaderTex;
		float num = 48f;
		float num2 = (((Object)(object)headerTex != (Object)null) ? (num * ((float)((Texture)headerTex).width / (float)((Texture)headerTex).height)) : 280f);
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(2f, (60f - num) / 2f, num2, num);
		if ((Object)(object)headerTex != (Object)null)
		{
			GUI.DrawTexture(val2, (Texture)(object)headerTex, (ScaleMode)2);
		}
		else
		{
			GameFont font = Text.Font;
			Text.Font = (GameFont)2;
			Widgets.Label(new Rect(4f, 8f, 360f, 40f), Translator.Translate("HaulersDream.SettingsHeaderFallback"));
			Text.Font = font;
		}
		if (Widgets.CloseButtonFor(new Rect(0f, 6f, width - 6f, 40f)) && val != null)
		{
			val.Close(true);
		}
		if (MultiplayerCompat.InMultiplayerGame)
		{
			Rect val3 = default(Rect);
			((Rect)(ref val3))._002Ector(40f, 100f, width - 80f, ((Rect)(ref rect)).height - 60f - 80f);
			GameFont font2 = Text.Font;
			TextAnchor anchor = Text.Anchor;
			Text.Font = (GameFont)2;
			Text.Anchor = (TextAnchor)4;
			Widgets.Label(val3, Translator.Translate("HaulersDream.Settings.MultiplayerLocked"));
			Text.Anchor = anchor;
			Text.Font = font2;
			return;
		}
		float num3 = ((Rect)(ref val2)).xMax + 16f;
		float num4 = width - 40f;
		float num5 = Mathf.Min(300f, num4 - num3);
		if (num5 >= 120f)
		{
			Rect val4 = new Rect(num3, 15f, num5, 30f);
			if (Widgets.ButtonText(val4, CurrentProfileLabel, true, true, true, (TextAnchor?)null))
			{
				OpenProfileMenu();
			}
			TooltipHandler.TipRegion(val4, TipSignal.op_Implicit(Translator.Translate("HaulersDream.Profile.SelectorTip")));
		}
		float num6 = 66f;
		float num7 = ((Rect)(ref rect)).yMax + 32f;
		Rect val5 = default(Rect);
		((Rect)(ref val5))._002Ector(0f, num6, width, num7 - num6);
		float num8 = Mathf.Clamp(((Rect)(ref val5)).width * 0.2f, 150f, 192f);
		float num9 = Mathf.Clamp(((Rect)(ref val5)).width * 0.26f, 200f, 290f);
		Rect rect2 = default(Rect);
		((Rect)(ref rect2))._002Ector(((Rect)(ref val5)).x, ((Rect)(ref val5)).y, num8, 28f);
		Rect rect3 = default(Rect);
		((Rect)(ref rect3))._002Ector(((Rect)(ref val5)).x, ((Rect)(ref val5)).y + 32f, num8, ((Rect)(ref val5)).height - 32f);
		Rect rect4 = default(Rect);
		((Rect)(ref rect4))._002Ector(((Rect)(ref rect3)).xMax + 12f, ((Rect)(ref val5)).y, ((Rect)(ref val5)).width - num8 - num9 - 24f, ((Rect)(ref val5)).height);
		Rect rect5 = default(Rect);
		((Rect)(ref rect5))._002Ector(((Rect)(ref rect4)).xMax + 12f, ((Rect)(ref val5)).y, num9, ((Rect)(ref val5)).height);
		DrawSearchBox(rect2);
		DrawNav(rect3);
		DrawContent(rect4);
		DrawHelp(rect5);
	}

	private void OpenProfileMenu()
	{
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Expected O, but got Unknown
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Expected O, but got Unknown
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Expected O, but got Unknown
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Expected O, but got Unknown
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Expected O, but got Unknown
		//IL_023d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Expected O, but got Unknown
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Expected O, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
		List<FloatMenuOption> list = new List<FloatMenuOption>();
		if (savedProfiles != null)
		{
			foreach (SettingsProfile savedProfile in savedProfiles)
			{
				SettingsProfile p = savedProfile;
				string text = ((activeProfileName == p.name) ? "  ✓" : "");
				list.Add(new FloatMenuOption(p.name + text, (Action)delegate
				{
					ApplyProfile(p);
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
		}
		list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.Profile.Default")), (Action)ApplyDefaultProfile, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
		SettingsProfile activeProfile = ActiveProfile;
		if (activeProfile != null && IsDirty)
		{
			list.Add(new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Profile.SaveChanges", NamedArgument.op_Implicit(activeProfile.name))), (Action)SaveActiveProfile, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
		}
		list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate((activeProfile == null) ? "HaulersDream.Profile.CreateNew" : "HaulersDream.Profile.SaveAsNew")), (Action)delegate
		{
			Find.WindowStack.Add((Window)(object)new Dialog_NameProfile(delegate(string name)
			{
				SaveAsNewProfile(name);
			}));
		}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
		list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.Profile.Copy")), (Action)delegate
		{
			Find.WindowStack.Add((Window)(object)new Dialog_CopyProfile(ExportProfileToString(this, activeProfileName ?? "")));
		}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
		list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.Profile.Paste")), (Action)delegate
		{
			Find.WindowStack.Add((Window)(object)new Dialog_PasteProfile(this));
		}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
		if (activeProfile != null)
		{
			SettingsProfile ap = activeProfile;
			list.Add(new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Profile.Delete", NamedArgument.op_Implicit(ap.name))), (Action)delegate
			{
				//IL_0015: Unknown result type (might be due to invalid IL or missing references)
				//IL_001a: Unknown result type (might be due to invalid IL or missing references)
				Find.WindowStack.Add((Window)(object)Dialog_MessageBox.CreateConfirmation(TranslatorFormattedStringExtensions.Translate("HaulersDream.Profile.DeleteConfirm", NamedArgument.op_Implicit(ap.name)), (Action)delegate
				{
					DeleteProfile(ap);
				}, true, (string)null, (WindowLayer)1));
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
		}
		Find.WindowStack.Add((Window)new FloatMenu(list));
	}

	private void DrawSearchBox(Rect rect)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		if (MultiplayerCompat.InMultiplayerGame)
		{
			return;
		}
		float num = ((!GenText.NullOrEmpty(searchQuery)) ? (((Rect)(ref rect)).width - 24f) : ((Rect)(ref rect)).width);
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, num, ((Rect)(ref rect)).height);
		searchQuery = Widgets.TextField(val, searchQuery ?? "");
		if (GenText.NullOrEmpty(searchQuery))
		{
			GameFont font = Text.Font;
			TextAnchor anchor = Text.Anchor;
			Color color = GUI.color;
			Text.Font = (GameFont)1;
			Text.Anchor = (TextAnchor)3;
			GUI.color = new Color(0.7f, 0.7f, 0.74f, 0.6f);
			Rect val2 = val;
			((Rect)(ref val2)).xMin = ((Rect)(ref val2)).xMin + 6f;
			Widgets.Label(val2, Translator.Translate("HaulersDream.Search.Placeholder"));
			GUI.color = color;
			Text.Anchor = anchor;
			Text.Font = font;
		}
		else
		{
			Rect val3 = new Rect(((Rect)(ref rect)).xMax - 22f, ((Rect)(ref rect)).y, 22f, ((Rect)(ref rect)).height);
			TooltipHandler.TipRegion(val3, TipSignal.op_Implicit(Translator.Translate("HaulersDream.Search.Clear")));
			if (Widgets.ButtonText(val3, "×", true, true, true, (TextAnchor?)null))
			{
				searchQuery = "";
			}
		}
		if (GenText.NullOrEmpty(searchQuery))
		{
			searchRegistry = null;
			scoredResults = null;
			scoredForQuery = null;
			groupedResults = null;
		}
	}

	private void DrawNav(Rect rect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		Rect val = GenUI.ContractedBy(rect, 4f);
		float num = ((Rect)(ref val)).y;
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		CatDef[] array = cats;
		for (int i = 0; i < array.Length; i++)
		{
			CatDef cd = array[i];
			if (cd.cat != SettingsCat.Migration)
			{
				DrawNavRow(new Rect(((Rect)(ref val)).x, num, ((Rect)(ref val)).width, 40f), cd, 40f, null);
				num += 42f;
			}
		}
		float yMax = ((Rect)(ref val)).yMax;
		if (ModReplacements.AnyActive)
		{
			CatDef cd2 = cats[10];
			float num2 = Mathf.Max(num + 40f + 2f, yMax - 40f);
			float num3 = Mathf.Max(num, num2 - 40f - 2f);
			DrawReportRow(new Rect(((Rect)(ref val)).x, num3, ((Rect)(ref val)).width, 40f), 40f);
			DrawNavRow(new Rect(((Rect)(ref val)).x, num2, ((Rect)(ref val)).width, 40f), cd2, 40f, MigrationWarn);
		}
		else
		{
			float num4 = Mathf.Max(num, yMax - 40f);
			DrawReportRow(new Rect(((Rect)(ref val)).x, num4, ((Rect)(ref val)).width, 40f), 40f);
		}
		Text.Font = font;
		Text.Anchor = anchor;
	}

	private void DrawNavRow(Rect row, CatDef cd, float rowH, Color? tint)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		bool flag = currentCat == cd.cat;
		if (flag)
		{
			Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.13f));
		}
		else if (Mouse.IsOver(row))
		{
			Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.06f));
		}
		Texture2D val = IconFor(cd);
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(((Rect)(ref row)).x + 9f, ((Rect)(ref row)).y + (rowH - 22f) / 2f, 22f, 22f);
		if ((Object)(object)val != (Object)null)
		{
			Color color = GUI.color;
			GUI.color = (Color)(((_003F?)tint) ?? (flag ? Color.white : new Color(1f, 1f, 1f, 0.75f)));
			GUI.DrawTexture(val2, (Texture)(object)val, (ScaleMode)2);
			GUI.color = color;
		}
		Rect val3 = default(Rect);
		((Rect)(ref val3))._002Ector(((Rect)(ref val2)).xMax + 8f, ((Rect)(ref row)).y, ((Rect)(ref row)).xMax - ((Rect)(ref val2)).xMax - 12f, rowH);
		Text.Font = (GameFont)1;
		Text.Anchor = (TextAnchor)3;
		Color color2 = GUI.color;
		if (tint.HasValue)
		{
			GUI.color = (Color)(flag ? tint.Value : new Color(tint.Value.r, tint.Value.g, tint.Value.b, 0.8f));
		}
		else if (flag)
		{
			GUI.color = new Color(0.92f, 0.94f, 1f);
		}
		Widgets.Label(val3, Translator.Translate(cd.nameKey));
		GUI.color = color2;
		bool flag2 = !GenText.NullOrEmpty(searchQuery);
		if (flag2 && !Mouse.IsOver(row))
		{
			Widgets.DrawBoxSolid(row, new Color(0.1f, 0.1f, 0.12f, 0.45f));
		}
		if (Mouse.IsOver(row))
		{
			HDSettingsUI.SetHelp(TaggedString.op_Implicit(Translator.Translate(cd.nameKey)), TaggedString.op_Implicit(Translator.Translate(cd.helpKey)));
		}
		if (Widgets.ButtonInvisible(row, true))
		{
			currentCat = cd.cat;
			contentScroll = Vector2.zero;
			pendingNav = null;
			highlightTarget = null;
			if (flag2)
			{
				searchQuery = "";
				searchRegistry = null;
				scoredResults = null;
				scoredForQuery = null;
				groupedResults = null;
			}
		}
	}

	private void DrawReportRow(Rect row, float rowH)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		bool num = Mouse.IsOver(row);
		if (num)
		{
			Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.06f));
		}
		float num2 = (num ? 0.72f : 0.5f);
		Texture2D reportIcon = ReportIcon;
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(((Rect)(ref row)).x + 10f, ((Rect)(ref row)).y + (rowH - 20f) / 2f, 20f, 20f);
		if ((Object)(object)reportIcon != (Object)null)
		{
			Color color = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, num2);
			GUI.DrawTexture(val, (Texture)(object)reportIcon, (ScaleMode)2);
			GUI.color = color;
		}
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(((Rect)(ref val)).xMax + 8f, ((Rect)(ref row)).y, ((Rect)(ref row)).xMax - ((Rect)(ref val)).xMax - 12f, rowH);
		Text.Font = (GameFont)1;
		Text.Anchor = (TextAnchor)3;
		Color color2 = GUI.color;
		GUI.color = new Color(1f, 1f, 1f, num2);
		Widgets.Label(val2, Translator.Translate("HaulersDream.Report.NavLabel"));
		GUI.color = color2;
		if (num)
		{
			HDSettingsUI.SetHelp(TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.NavLabel")), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Report.NavLabel.Help")));
		}
		if (Widgets.ButtonInvisible(row, true))
		{
			Find.WindowStack.Add((Window)(object)new Dialog_MyReports());
		}
	}

	private void DrawCat(SettingsCtx c, SettingsCat cat)
	{
		switch (cat)
		{
		case SettingsCat.Features:
			DrawFeaturesCat(c);
			break;
		case SettingsCat.Hauling:
			DrawHaulingCat(c);
			break;
		case SettingsCat.Unloading:
			DrawUnloadingCat(c);
			break;
		case SettingsCat.BuildCraft:
			DrawBuildCraftCat(c);
			break;
		case SettingsCat.BulkLoading:
			DrawBulkLoadingCat(c);
			break;
		case SettingsCat.Routing:
			DrawRoutingCat(c);
			break;
		case SettingsCat.Yields:
			DrawYieldsCat(c);
			break;
		case SettingsCat.Who:
			DrawWhoCat(c);
			break;
		case SettingsCat.Planners:
			DrawPlannersCat(c);
			break;
		case SettingsCat.Advanced:
			DrawAdvancedCat(c);
			break;
		case SettingsCat.Migration:
			DrawMigrationCat(c);
			break;
		}
	}

	private void DrawContent(Rect rect)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		if (!GenText.NullOrEmpty(searchQuery))
		{
			EnsureSearchRegistry(rect);
			EnsureScored();
			DrawSearchResults(rect);
			return;
		}
		if (currentCat == SettingsCat.Migration && !ModReplacements.AnyActive)
		{
			currentCat = SettingsCat.Features;
		}
		int num = (int)currentCat;
		if (pendingNav.HasValue && pendingNav.Value.catId == num)
		{
			(int, int, float, int) value = pendingNav.Value;
			contentScroll.y = Mathf.Clamp(value.Item3 - 12f, 0f, Mathf.Max(0f, catHeight[num] - ((Rect)(ref rect)).height));
			int num2 = value.Item4 - 1;
			pendingNav = ((num2 > 0) ? new(int, int, float, int)?((value.Item1, value.Item2, value.Item3, num2)) : (((int, int, float, int)?)null));
		}
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, 0f, ((Rect)(ref rect)).width - 16f, Mathf.Max(catHeight[num], ((Rect)(ref rect)).height));
		Widgets.BeginScrollView(rect, ref contentScroll, val, true);
		SettingsCtx settingsCtx = new SettingsCtx(((Rect)(ref val)).width - 12f);
		settingsCtx.Gap(2f);
		DrawCat(settingsCtx, currentCat);
		catHeight[num] = settingsCtx.CurY + 8f;
		DrawNavHighlight(val, num);
		Widgets.EndScrollView();
	}

	private void EnsureSearchRegistry(Rect rect)
	{
		if (searchRegistry != null)
		{
			return;
		}
		SettingsCtx settingsCtx = new SettingsCtx(((Rect)(ref rect)).width - 16f - 12f)
		{
			Collecting = true,
			Sink = new List<OptionEntry>()
		};
		CatDef[] array = cats;
		for (int i = 0; i < array.Length; i++)
		{
			CatDef catDef = array[i];
			if (catDef.cat != SettingsCat.Migration)
			{
				settingsCtx.CurY = 0f;
				settingsCtx.Gap(2f);
				settingsCtx.CurrentCatId = (int)catDef.cat;
				settingsCtx.CurrentHeader = null;
				settingsCtx.Ordinal = 0;
				DrawCat(settingsCtx, catDef.cat);
			}
		}
		searchRegistry = settingsCtx.Sink;
		for (int j = 0; j < searchRegistry.Count; j++)
		{
			OptionEntry optionEntry = searchRegistry[j];
			optionEntry.NameLower = optionEntry.Name?.ToLowerInvariant();
			optionEntry.DescLower = optionEntry.Desc?.ToLowerInvariant();
		}
		scoredForQuery = null;
		groupedResults = null;
	}

	private void EnsureScored()
	{
		if (searchRegistry == null)
		{
			scoredResults = null;
			groupedResults = null;
		}
		else
		{
			if (searchQuery == scoredForQuery && scoredResults != null)
			{
				return;
			}
			string queryLower = searchQuery?.ToLowerInvariant();
			List<(OptionEntry, float)> list = new List<(OptionEntry, float)>(searchRegistry.Count);
			foreach (OptionEntry item in searchRegistry)
			{
				float num = SettingsSearch.OptionScoreLower(queryLower, item.NameLower, item.DescLower, CategoryAndHeaderText(item).ToLowerInvariant());
				if (num > 0f)
				{
					list.Add((item, num));
				}
			}
			list.Sort(((OptionEntry e, float s) a, (OptionEntry e, float s) b) => b.s.CompareTo(a.s));
			scoredTotal = list.Count;
			int num2 = Mathf.Min(list.Count, 30);
			scoredResults = new List<OptionEntry>(num2);
			for (int num3 = 0; num3 < num2; num3++)
			{
				scoredResults.Add(list[num3].Item1);
			}
			scoredForQuery = searchQuery;
			BuildResultGroups();
		}
	}

	private void BuildResultGroups()
	{
		if (scoredResults == null || scoredResults.Count == 0)
		{
			groupedResults = null;
			return;
		}
		List<ResultGroup> list = new List<ResultGroup>();
		Dictionary<(int, string), ResultGroup> dictionary = new Dictionary<(int, string), ResultGroup>();
		foreach (OptionEntry scoredResult in scoredResults)
		{
			(int, string) key = (scoredResult.CatId, scoredResult.Header ?? "");
			if (!dictionary.TryGetValue(key, out var value))
			{
				ResultGroup obj = new ResultGroup
				{
					CatId = scoredResult.CatId,
					Header = scoredResult.Header,
					Best = scoredResult
				};
				value = (dictionary[key] = obj);
				list.Add(value);
			}
			value.Ordinals.Add(scoredResult.Ordinal);
		}
		groupedResults = list;
	}

	private static string CategoryAndHeaderText(OptionEntry e)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		string text = TaggedString.op_Implicit(Translator.Translate(cats[e.CatId].nameKey));
		if (!GenText.NullOrEmpty(e.Header))
		{
			return text + " " + e.Header;
		}
		return text;
	}

	private void DrawSearchResults(Rect rect)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		List<OptionEntry> list = scoredResults;
		float num = 6f;
		float num2 = ((Rect)(ref rect)).width - 16f - 12f;
		if (list == null || list.Count == 0)
		{
			Rect val = default(Rect);
			((Rect)(ref val))._002Ector(0f, 0f, ((Rect)(ref rect)).width - 16f, Mathf.Max(num + 40f, ((Rect)(ref rect)).height));
			Widgets.BeginScrollView(rect, ref contentScroll, val, true);
			GameFont font = Text.Font;
			TextAnchor anchor = Text.Anchor;
			Color color = GUI.color;
			Text.Font = (GameFont)1;
			Text.Anchor = (TextAnchor)4;
			GUI.color = new Color(0.72f, 0.72f, 0.76f);
			Widgets.Label(new Rect(0f, num, num2, 40f), Translator.Translate("HaulersDream.Search.NoResults"));
			GUI.color = color;
			Text.Anchor = anchor;
			Text.Font = font;
			Widgets.EndScrollView();
			return;
		}
		if (groupedResults == null)
		{
			BuildResultGroups();
		}
		List<ResultGroup> list2 = groupedResults;
		if (list2 != null && list2.Count != 0)
		{
			Rect val2 = default(Rect);
			((Rect)(ref val2))._002Ector(0f, 0f, ((Rect)(ref rect)).width - 16f, Mathf.Max(searchResultsHeight, ((Rect)(ref rect)).height));
			Widgets.BeginScrollView(rect, ref contentScroll, val2, true);
			SettingsCtx settingsCtx = new SettingsCtx(((Rect)(ref val2)).width - 12f);
			settingsCtx.Gap(num);
			for (int i = 0; i < list2.Count; i++)
			{
				ResultGroup resultGroup = list2[i];
				settingsCtx.Gap((i == 0) ? 22f : 40f);
				DrawResultSectionHeader(settingsCtx, resultGroup);
				settingsCtx.Gap(10f);
				settingsCtx.RenderOrdinals = resultGroup.Ordinals;
				settingsCtx.Ordinal = 0;
				settingsCtx.CurrentCatId = resultGroup.CatId;
				DrawCat(settingsCtx, (SettingsCat)resultGroup.CatId);
				settingsCtx.RenderOrdinals = null;
			}
			if (scoredTotal > scoredResults.Count)
			{
				settingsCtx.Gap(24f);
				string text = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Search.More", NamedArgument.op_Implicit(scoredResults.Count)));
				GameFont font2 = Text.Font;
				TextAnchor anchor2 = Text.Anchor;
				Color color2 = GUI.color;
				Text.Font = (GameFont)1;
				Rect val3 = settingsCtx.Row(Mathf.Max(Text.LineHeightOf((GameFont)1), Text.CalcHeight(text, settingsCtx.Width)));
				Text.Anchor = (TextAnchor)4;
				GUI.color = new Color(0.72f, 0.72f, 0.76f);
				Widgets.Label(val3, text);
				Text.Font = font2;
				Text.Anchor = anchor2;
				GUI.color = color2;
			}
			searchResultsHeight = settingsCtx.CurY + num;
			Widgets.EndScrollView();
		}
	}

	private void DrawResultSectionHeader(SettingsCtx c, ResultGroup g)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		Rect val = c.Row(26f);
		Widgets.DrawBoxSolid(val, new Color(1f, 1f, 1f, 0.09f));
		if (Mouse.IsOver(val))
		{
			Widgets.DrawBoxSolid(val, new Color(1f, 1f, 1f, 0.06f));
		}
		CatDef cd = cats[g.CatId];
		Texture2D val2 = IconFor(cd);
		Rect val3 = default(Rect);
		((Rect)(ref val3))._002Ector(((Rect)(ref val)).x + 6f, ((Rect)(ref val)).y + (((Rect)(ref val)).height - 18f) / 2f, 18f, 18f);
		if ((Object)(object)val2 != (Object)null)
		{
			Color color = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, 0.85f);
			GUI.DrawTexture(val3, (Texture)(object)val2, (ScaleMode)2);
			GUI.color = color;
		}
		string text = (GenText.NullOrEmpty(g.Header) ? ((object)Translator.Translate(cd.nameKey)/*cast due to constrained. prefix*/).ToString() : g.Header);
		Rect val4 = default(Rect);
		((Rect)(ref val4))._002Ector(((Rect)(ref val3)).xMax + 8f, ((Rect)(ref val)).y, ((Rect)(ref val)).xMax - ((Rect)(ref val3)).xMax - 10f, ((Rect)(ref val)).height);
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		Color color2 = GUI.color;
		Text.Font = (GameFont)1;
		Text.Anchor = (TextAnchor)3;
		GUI.color = new Color(0.86f, 0.88f, 0.95f);
		Widgets.Label(val4, text);
		GUI.color = color2;
		Text.Anchor = anchor;
		Text.Font = font;
		if (Mouse.IsOver(val))
		{
			HDSettingsUI.SetHelp(TaggedString.op_Implicit(Translator.Translate(cd.nameKey)), GenText.NullOrEmpty(g.Header) ? ((object)Translator.Translate(cd.helpKey)/*cast due to constrained. prefix*/).ToString() : g.Header);
		}
		if (Widgets.ButtonInvisible(val, true))
		{
			Navigate(g.Best);
		}
	}

	private void Navigate(OptionEntry e)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		currentCat = (SettingsCat)e.CatId;
		searchQuery = "";
		searchRegistry = null;
		scoredResults = null;
		scoredForQuery = null;
		groupedResults = null;
		contentScroll = Vector2.zero;
		pendingNav = (e.CatId, e.Ordinal, e.StartY, 2);
		highlightTarget = (e.CatId, e.Ordinal, e.StartY, e.Height);
		highlightStart = Time.realtimeSinceStartup;
	}

	private void DrawNavHighlight(Rect view, int currentIdx)
	{
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		if (highlightTarget.HasValue && highlightTarget.Value.catId == currentIdx)
		{
			float num = (Time.realtimeSinceStartup - highlightStart) / 1.5f;
			if (num >= 1f)
			{
				highlightTarget = null;
				return;
			}
			(int, int, float, float) value = highlightTarget.Value;
			float num2 = (1f - num) * (0.35f + 0.65f * Mathf.Abs(Mathf.Sin(num * (float)Math.PI * 5f)));
			Rect val = new Rect(0f, value.Item3 - 4f, ((Rect)(ref view)).width, value.Item4 + 8f);
			Color color = GUI.color;
			Widgets.DrawBoxSolid(val, new Color(1f, 0.92f, 0.4f, num2 * 0.35f));
			GUI.color = new Color(1f, 0.92f, 0.4f, Mathf.Min(1f, num2));
			Widgets.DrawBox(val, 2, (Texture2D)null);
			GUI.color = color;
		}
	}

	private void DrawHelp(Rect rect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		Rect val = GenUI.ContractedBy(rect, 12f);
		string text = HDSettingsUI.HoverTitle ?? TaggedString.op_Implicit(Translator.Translate(cats[(int)currentCat].nameKey));
		string text2 = HDSettingsUI.HoverBody ?? TaggedString.op_Implicit(Translator.Translate(cats[(int)currentCat].helpKey));
		string hoverStatus = HDSettingsUI.HoverStatus;
		Action<Rect> hoverExtra = HDSettingsUI.HoverExtra;
		GameFont font = Text.Font;
		Color color = GUI.color;
		float num = ((Rect)(ref val)).width - 16f;
		Text.Font = (GameFont)1;
		float num2 = Text.CalcHeight(text, num);
		Text.Font = (GameFont)0;
		float num3 = (GenText.NullOrEmpty(hoverStatus) ? 0f : Text.CalcHeight(hoverStatus, num));
		Text.Font = (GameFont)1;
		float num4 = (GenText.NullOrEmpty(text2) ? 0f : Text.CalcHeight(text2, num));
		float num5 = ((hoverExtra != null) ? 132f : 0f);
		float num6 = num2 + ((num3 > 0f) ? (num3 + 2f) : 0f) + 8f + ((num5 > 0f) ? (num5 + 10f) : 0f) + num4;
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(0f, 0f, num, Mathf.Max(num6, ((Rect)(ref val)).height));
		Widgets.BeginScrollView(val, ref helpScroll, val2, true);
		float num7 = 0f;
		Text.Font = (GameFont)1;
		GUI.color = new Color(0.95f, 0.86f, 0.62f);
		Widgets.Label(new Rect(0f, num7, num, num2), text);
		num7 += num2;
		GUI.color = color;
		if (num3 > 0f)
		{
			num7 += 2f;
			Text.Font = (GameFont)0;
			GUI.color = HDSettingsUI.HoverStatusColor;
			Widgets.Label(new Rect(0f, num7, num, num3), hoverStatus);
			num7 += num3;
			GUI.color = color;
		}
		num7 += 8f;
		if (num5 > 0f)
		{
			hoverExtra(new Rect(0f, num7, num, 132f));
			num7 += 142f;
		}
		if (num4 > 0f)
		{
			Text.Font = (GameFont)1;
			Widgets.Label(new Rect(0f, num7, num, num4), text2);
		}
		Widgets.EndScrollView();
		GUI.color = color;
		Text.Font = font;
	}

	private static void DrawOverloadGraph(Rect rect, int level)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_027f: Unknown result type (might be due to invalid IL or missing references)
		//IL_026d: Unknown result type (might be due to invalid IL or missing references)
		//IL_026f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_0347: Unknown result type (might be due to invalid IL or missing references)
		//IL_034d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0353: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0333: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.05f));
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		Color color = GUI.color;
		Text.Font = (GameFont)0;
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(((Rect)(ref rect)).x + 36f, ((Rect)(ref rect)).y + 6f, ((Rect)(ref rect)).width - 36f - 6f, ((Rect)(ref rect)).height - 15f - 6f);
		GUI.color = new Color(1f, 1f, 1f, 0.12f);
		Widgets.DrawLineHorizontal(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width);
		float num = ((Rect)(ref val)).yMax - 0.25f * ((Rect)(ref val)).height;
		Widgets.DrawLineHorizontal(((Rect)(ref val)).x, num, ((Rect)(ref val)).width);
		Widgets.DrawLineVertical(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).height);
		GUI.color = new Color(0.72f, 0.72f, 0.76f);
		Text.Anchor = (TextAnchor)5;
		Widgets.Label(new Rect(((Rect)(ref rect)).x, ((Rect)(ref val)).y - 6f, 32f, 14f), "100%");
		Widgets.Label(new Rect(((Rect)(ref rect)).x, num - 7f, 32f, 14f), GenText.ToStringPercent(0.25f));
		Text.Anchor = (TextAnchor)1;
		for (int i = 100; i <= 300; i += 100)
		{
			Widgets.Label(new Rect(((Rect)(ref val)).x + ((float)i / 100f - 1f) / 2f * ((Rect)(ref val)).width - 20f, ((Rect)(ref val)).yMax + 1f, 40f, 14f), i + "%");
		}
		Vector2 val2 = Vector2.zero;
		Color val3 = default(Color);
		((Color)(ref val3))._002Ector(0.55f, 0.8f, 1f);
		Vector2 val4 = default(Vector2);
		for (int j = 0; j <= 48; j++)
		{
			float num2 = 1f + 2f * (float)j / 48f;
			float num3 = OverloadTuning.SpeedFactor(level, num2);
			((Vector2)(ref val4))._002Ector(((Rect)(ref val)).x + (num2 - 1f) / 2f * ((Rect)(ref val)).width, ((Rect)(ref val)).yMax - num3 * ((Rect)(ref val)).height);
			if (j > 0)
			{
				Widgets.DrawLine(val2, val4, val3, 2f);
			}
			val2 = val4;
		}
		float num4 = OverloadTuning.MaxOverloadRatio(level);
		if (!float.IsInfinity(num4) && num4 > 1f && num4 <= 3f)
		{
			float num5 = ((Rect)(ref val)).x + (num4 - 1f) / 2f * ((Rect)(ref val)).width;
			GUI.color = new Color(0.95f, 0.86f, 0.62f);
			Widgets.DrawLineVertical(num5, ((Rect)(ref val)).y, ((Rect)(ref val)).height);
			Text.Anchor = (TextAnchor)0;
			Widgets.Label(new Rect(Mathf.Min(num5 + 3f, ((Rect)(ref val)).xMax - 56f), ((Rect)(ref val)).y, 60f, 14f), Translator.Translate("HaulersDream.Setting.Overload.Ceiling"));
		}
		GUI.color = color;
		Text.Anchor = anchor;
		Text.Font = font;
	}

	private static string Tiles(int n)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Unit.Tiles", NamedArgument.op_Implicit(n)));
	}

	private static string Hours(float h)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Unit.Hours", NamedArgument.op_Implicit(h.ToString("0.#"))));
	}

	private static string PickupDelayLabel(int ticks)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		if (ticks <= 0)
		{
			return TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PickupDelay.Instant"));
		}
		string text = $"~{(float)ticks / 60f:0.0}s";
		if (ticks == 120)
		{
			return TaggedString.op_Implicit(text + " (" + Translator.Translate("HaulersDream.Setting.PickupDelay.Vanilla") + ")");
		}
		return text;
	}

	private void DrawMigrationCat(SettingsCtx c)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		List<string> names = ModReplacements.ActiveNames;
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Migration.DetectedHead")));
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Migration.Intro")), 0f, MigrationWarn);
		c.Gap(4f);
		for (int i = 0; i < names.Count; i++)
		{
			HDSettingsUI.Note(c, "•  " + names[i], 12f, MigrationWarn);
		}
		c.Gap(6f);
		HDSettingsUI.Button(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Migration.DisableButton")), delegate
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			string text = GenText.ToCommaList((IEnumerable<string>)names, true, false);
			Find.WindowStack.Add((Window)(object)Dialog_MessageBox.CreateConfirmation(TranslatorFormattedStringExtensions.Translate("HaulersDream.Migration.DisableConfirm", NamedArgument.op_Implicit(text)), (Action)ModReplacements.DisableAllAndRestart, true, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Migration.DisableTitle")), (WindowLayer)1));
		}, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Migration.DisableButton.Help")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Migration.StepsHead")));
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Migration.Step1")));
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Migration.Step2")));
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Migration.Step3")));
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Migration.Step4")));
	}

	private void DrawFeaturesCat(SettingsCtx c)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Cat.Features.Intro")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.FeatGroup.Core")));
		masterEnabled = Card(c, SettingsCat.Features, "HaulersDream.Feat.Master", masterEnabled, "HaulersDream.Setting.MasterEnabledDesc");
		markForUnload = Card(c, SettingsCat.Unloading, "HaulersDream.Feat.AutoUnload", markForUnload, "HaulersDream.Feat.AutoUnload.Blurb");
		bulkHaul = Card(c, SettingsCat.Hauling, "HaulersDream.Feat.BulkHaul", bulkHaul, "HaulersDream.Setting.BulkHaulDesc");
		sweepNearbyWhileWorking = Card(c, SettingsCat.Hauling, "HaulersDream.Feat.Sweep", sweepNearbyWhileWorking, "HaulersDream.Setting.SweepNearbyWhileWorkingDesc");
		bulkHaulUrgent = Card(c, SettingsCat.Hauling, "HaulersDream.Feat.BulkHaulUrgent", bulkHaulUrgent, "HaulersDream.Setting.BulkHaulUrgentDesc");
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.FeatGroup.Loading")));
		enableBulkUnloadCarriers = Card(c, SettingsCat.BulkLoading, "HaulersDream.Feat.UnloadCarriers", enableBulkUnloadCarriers, "HaulersDream.Setting.EnableBulkUnloadCarriersDesc");
		enableBulkLoadTransporters = Card(c, SettingsCat.BulkLoading, "HaulersDream.Feat.LoadTransporters", enableBulkLoadTransporters, "HaulersDream.Setting.EnableBulkLoadTransportersDesc");
		enableBulkLoadPortal = Card(c, SettingsCat.BulkLoading, "HaulersDream.Feat.LoadPortal", enableBulkLoadPortal, "HaulersDream.Setting.EnableBulkLoadPortalDesc");
		enableBulkRefuel = Card(c, SettingsCat.BulkLoading, "HaulersDream.Feat.Refuel", enableBulkRefuel, "HaulersDream.Setting.EnableBulkRefuelDesc");
		if (VehicleFrameworkCompat.IsActive)
		{
			enableVehicleFramework = Card(c, SettingsCat.BulkLoading, "HaulersDream.Feat.Vehicles", enableVehicleFramework, "HaulersDream.Setting.EnableVehicleFrameworkDesc");
		}
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.FeatGroup.BuildCraft")));
		buildFromInventory = Card(c, SettingsCat.BuildCraft, "HaulersDream.Feat.BuildFromInv", buildFromInventory, "HaulersDream.Setting.BuildFromInventoryDesc");
		shareForCrafting = Card(c, SettingsCat.BuildCraft, "HaulersDream.Feat.CraftFromInv", shareForCrafting, "HaulersDream.Setting.ShareForCraftingDesc");
		inventoryConstructDeliver = Card(c, SettingsCat.BuildCraft, "HaulersDream.Feat.ConstructDeliver", inventoryConstructDeliver, "HaulersDream.Setting.InventoryConstructDeliverDesc");
		mealsOnWheels = Card(c, SettingsCat.BuildCraft, "HaulersDream.Feat.Meals", mealsOnWheels, "HaulersDream.Setting.MealsOnWheelsDesc");
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.FeatGroup.Planning")));
		planRoutes = Card(c, SettingsCat.Planners, "HaulersDream.Feat.PlanRoutes", planRoutes, "HaulersDream.Setting.PlanRoutesDesc");
		planCrafting = Card(c, SettingsCat.Planners, "HaulersDream.Feat.PlanCrafting", planCrafting, "HaulersDream.Setting.PlanCraftingDesc");
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.FeatGroup.Wyu")));
		enRoutePickup = Card(c, SettingsCat.Routing, "HaulersDream.Feat.EnRoute", enRoutePickup, "HaulersDream.Setting.EnRoutePickupDesc");
		storageRouting = Card(c, SettingsCat.Routing, "HaulersDream.Feat.StorageRouting", storageRouting, "HaulersDream.Setting.StorageRoutingDesc");
		storageFiltersEnabled = Card(c, SettingsCat.Routing, "HaulersDream.Feat.StorageFilters", storageFiltersEnabled, "HaulersDream.Setting.StorageFiltersDesc");
	}

	private bool Card(SettingsCtx c, SettingsCat iconCat, string prefix, bool value, string helpKey)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		Texture2D icon = IconFor(cats[(int)iconCat]);
		return HDSettingsUI.FeatureCard(c, icon, TaggedString.op_Implicit(Translator.Translate(prefix + ".Name")), TaggedString.op_Implicit(Translator.Translate(prefix + ".Blurb")), value, TaggedString.op_Implicit(Translator.Translate(helpKey)));
	}

	private void DrawHaulingCat(SettingsCtx c)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_020d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_026a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_030f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0335: Unknown result type (might be due to invalid IL or missing references)
		//IL_034a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Unknown result type (might be due to invalid IL or missing references)
		//IL_0385: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0413: Unknown result type (might be due to invalid IL or missing references)
		//IL_0482: Unknown result type (might be due to invalid IL or missing references)
		//IL_0471: Unknown result type (might be due to invalid IL or missing references)
		//IL_0476: Unknown result type (might be due to invalid IL or missing references)
		//IL_0487: Unknown result type (might be due to invalid IL or missing references)
		//IL_049e: Unknown result type (might be due to invalid IL or missing references)
		//IL_04be: Unknown result type (might be due to invalid IL or missing references)
		//IL_0501: Unknown result type (might be due to invalid IL or missing references)
		//IL_0516: Unknown result type (might be due to invalid IL or missing references)
		//IL_0547: Unknown result type (might be due to invalid IL or missing references)
		//IL_055c: Unknown result type (might be due to invalid IL or missing references)
		//IL_058d: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0619: Unknown result type (might be due to invalid IL or missing references)
		//IL_062e: Unknown result type (might be due to invalid IL or missing references)
		//IL_064f: Unknown result type (might be due to invalid IL or missing references)
		//IL_067a: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_06bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_06e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0717: Unknown result type (might be due to invalid IL or missing references)
		//IL_072c: Unknown result type (might be due to invalid IL or missing references)
		//IL_074d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0762: Unknown result type (might be due to invalid IL or missing references)
		//IL_0783: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_07de: Unknown result type (might be due to invalid IL or missing references)
		//IL_07f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0814: Unknown result type (might be due to invalid IL or missing references)
		//IL_0829: Unknown result type (might be due to invalid IL or missing references)
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Cat.Hauling.Intro")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.CarryWeight")));
		carryLimitFraction = HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CarryLimit.Lab")), carryLimitFraction, 0.05f, 1f, GenText.ToStringPercent(carryLimitFraction), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CarryLimitDesc")));
		string readout = ((object)((carryMassCapKg <= 0f) ? Translator.Translate("HaulersDream.Setting.CarryMassCap.Off") : TranslatorFormattedStringExtensions.Translate("HaulersDream.Setting.CarryMassCap.Value", NamedArgument.op_Implicit(carryMassCapKg.ToString("F0"))))/*cast due to constrained. prefix*/).ToString();
		carryMassCapKg = Mathf.Round(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CarryMassCap.Lab")), carryMassCapKg, 0f, 200f, readout, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CarryMassCapDesc"))) / 5f) * 5f;
		overloadLevel = Mathf.RoundToInt(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Overload.Lab")), overloadLevel, 0f, 10f, OverloadLevelLabel(overloadLevel), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.OverloadDesc")), !strictCarryWeight, 0f, delegate(Rect r)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			DrawOverloadGraph(r, overloadLevel);
		}));
		strictCarryWeight = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StrictCarryWeight")), strictCarryWeight, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StrictCarryWeightDesc")));
		skipHaulWhileBleeding = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.SkipHaulWhileBleeding")), skipHaulWhileBleeding, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.SkipHaulWhileBleedingDesc")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.BulkHaul")));
		bulkHaul = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Feat.BulkHaul.Name")), bulkHaul, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BulkHaulDesc")));
		int num = HDSettingsUI.Segmented(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BulkHaulTrigger.Lab")), (bulkHaulTrigger != BulkHaulTrigger.SecondTasked) ? 1 : 0, new string[2]
		{
			((object)Translator.Translate("HaulersDream.Setting.BulkHaulSecond.S")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.BulkHaulAlways.S")/*cast due to constrained. prefix*/).ToString()
		}, new string[2]
		{
			((object)Translator.Translate("HaulersDream.Setting.BulkHaulSecondDesc")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.BulkHaulAlwaysDesc")/*cast due to constrained. prefix*/).ToString()
		}, null, bulkHaul, 24f);
		bulkHaulTrigger = ((num == 0) ? BulkHaulTrigger.SecondTasked : BulkHaulTrigger.Always);
		haulNearbyOption = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HaulNearbyOption")), haulNearbyOption, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HaulNearbyOptionDesc")), bulkHaul, 24f);
		haulOversizedInInventory = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HaulOversized")), haulOversizedInInventory, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HaulOversizedDesc")), bulkHaul, 24f);
		bulkHaulCorpses = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BulkHaulCorpses")), bulkHaulCorpses, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BulkHaulCorpsesDesc")), bulkHaul, 24f);
		string readout2 = ((object)((corpseCarryAllowance <= 1f) ? Translator.Translate("HaulersDream.Setting.CorpseCarryAllowance.Off") : TranslatorFormattedStringExtensions.Translate("HaulersDream.Setting.CorpseCarryAllowance.Value", NamedArgument.op_Implicit(corpseCarryAllowance.ToString("0.0"))))/*cast due to constrained. prefix*/).ToString();
		corpseCarryAllowance = Mathf.Round(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CorpseCarryAllowance.Lab")), corpseCarryAllowance, 1f, 3f, readout2, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CorpseCarryAllowanceDesc")), bulkHaul && bulkHaulCorpses, 24f) * 10f) / 10f;
		string readout3 = ((object)((corpseMaxHaulMassKg <= 0f) ? Translator.Translate("HaulersDream.Setting.CorpseMaxHaulMass.Off") : TranslatorFormattedStringExtensions.Translate("HaulersDream.Setting.CorpseMaxHaulMass.Value", NamedArgument.op_Implicit(corpseMaxHaulMassKg.ToString("F0"))))/*cast due to constrained. prefix*/).ToString();
		corpseMaxHaulMassKg = Mathf.Round(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CorpseMaxHaulMass.Lab")), corpseMaxHaulMassKg, 0f, 200f, readout3, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CorpseMaxHaulMassDesc")), bulkHaul && bulkHaulCorpses, 24f) / 5f) * 5f;
		corpseHaulHumanlike = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CorpseHaulHumanlike")), corpseHaulHumanlike, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CorpseHaulHumanlikeDesc")), bulkHaul && bulkHaulCorpses, 24f);
		corpseHaulByColonists = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CorpseHaulByColonists")), corpseHaulByColonists, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CorpseHaulByColonistsDesc")), bulkHaul && bulkHaulCorpses, 24f);
		corpseHaulByMechs = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CorpseHaulByMechs")), corpseHaulByMechs, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CorpseHaulByMechsDesc")), bulkHaul && bulkHaulCorpses, 24f);
		corpseHaulByAnimals = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CorpseHaulByAnimals")), corpseHaulByAnimals, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CorpseHaulByAnimalsDesc")), bulkHaul && bulkHaulCorpses, 24f);
		bulkHaulUrgent = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BulkHaulUrgent")), bulkHaulUrgent, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BulkHaulUrgentDesc")));
		bulkHaulUrgentRadius = Mathf.RoundToInt(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BulkHaulUrgentRadius.Lab")), bulkHaulUrgentRadius, 1f, 12f, Tiles(bulkHaulUrgentRadius), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BulkHaulUrgentRadius.Help")), bulkHaulUrgent, 24f));
		bulkHaulUrgentIncludeNonUrgent = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BulkHaulUrgentIncludeNonUrgent")), bulkHaulUrgentIncludeNonUrgent, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BulkHaulUrgentIncludeNonUrgentDesc")), bulkHaulUrgent, 24f);
		manualPickupOption = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ManualPickup")), manualPickupOption, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ManualPickupDesc")));
		keepInInventoryOption = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.KeepInInventory")), keepInInventoryOption, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.KeepInInventoryDesc")));
		keepInventoryGearButton = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.KeepGearButton")), keepInventoryGearButton, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.KeepGearButtonDesc")));
		pickupDelayTicks = Mathf.RoundToInt(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PickupDelay.Lab")), pickupDelayTicks, 0f, 240f, PickupDelayLabel(pickupDelayTicks), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PickupDelay.Help"))) / 10f) * 10;
		pickupDelayOnHauling = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PickupDelayOnHauling")), pickupDelayOnHauling, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PickupDelayOnHaulingDesc")));
		pickupDelayOnLoading = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PickupDelayOnLoading")), pickupDelayOnLoading, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PickupDelayOnLoadingDesc")));
	}

	private void DrawUnloadingCat(SettingsCtx c)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_027f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_0307: Unknown result type (might be due to invalid IL or missing references)
		//IL_030c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_032a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0345: Unknown result type (might be due to invalid IL or missing references)
		//IL_0369: Unknown result type (might be due to invalid IL or missing references)
		//IL_0381: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Cat.Unloading.Intro")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.AutoUnloadTrips")));
		unloadGraceTicks = Mathf.RoundToInt(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.UnloadGrace.Lab")), unloadGraceTicks, 0f, 7500f, $"~{(float)unloadGraceTicks / 2500f:0.0} h", TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.UnloadGrace.Help"))) / 50f) * 50;
		markForUnload = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.MarkForUnload")), markForUnload, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Feat.AutoUnload.Blurb")));
		unloadBeforeSleep = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.UnloadBeforeSleep")), unloadBeforeSleep, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.UnloadBeforeSleepDesc")), markForUnload, 24f);
		unloadBeforeLeisure = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.UnloadBeforeLeisure")), unloadBeforeLeisure, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.UnloadBeforeLeisureDesc")), markForUnload, 24f);
		unloadBeforeEating = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.UnloadBeforeEating")), unloadBeforeEating, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.UnloadBeforeEatingDesc")), markForUnload, 24f);
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.DropOff")));
		haulToStack = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HaulToStack")), haulToStack, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HaulToStackDesc")));
		opportunisticUnload = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.OpportunisticUnload")), opportunisticUnload, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.OpportunisticUnloadDesc")));
		closestDestinationUnloadOrder = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ClosestDestUnloadOrder")), closestDestinationUnloadOrder, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ClosestDestUnloadOrderDesc")));
		unloadAllSurplus = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.UnloadAllSurplus")), unloadAllSurplus, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.UnloadAllSurplusDesc")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.UnloadDetour")));
		int num = HDSettingsUI.Segmented(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.UnloadDetour.Lab")), (int)unloadDetour, DetourSegmentLabels(), DetourSegmentHelp(), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.UnloadDetour.Desc")), markForUnload);
		unloadDetour = (OpportunisticDetour)num;
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.ItemRulesDisplay")));
		int itemRuleCount = ItemRuleCount;
		string label = TaggedString.op_Implicit((itemRuleCount > 0) ? TranslatorFormattedStringExtensions.Translate("HaulersDream.Setting.ItemUnloadButtonN", NamedArgument.op_Implicit(itemRuleCount)) : Translator.Translate("HaulersDream.Setting.ItemUnloadButton"));
		HDSettingsUI.Button(c, label, delegate
		{
			Find.WindowStack.Add((Window)(object)new Dialog_ItemUnloadSettings(this));
		}, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ItemUnload.Help")));
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PerPawnButtonsNote")));
		bool flag = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HideGizmo")), !hideGizmo, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HideGizmo.Help")));
		hideGizmo = !flag;
		showAutoHaulGizmo = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShowAutoHaulGizmo")), showAutoHaulGizmo, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShowAutoHaulGizmo.Help")));
	}

	private void DrawBuildCraftCat(SettingsCtx c)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0319: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Unknown result type (might be due to invalid IL or missing references)
		//IL_034f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0364: Unknown result type (might be due to invalid IL or missing references)
		//IL_0385: Unknown result type (might be due to invalid IL or missing references)
		//IL_039a: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0406: Unknown result type (might be due to invalid IL or missing references)
		//IL_0426: Unknown result type (might be due to invalid IL or missing references)
		//IL_043c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0451: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0479: Unknown result type (might be due to invalid IL or missing references)
		//IL_048e: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c9: Unknown result type (might be due to invalid IL or missing references)
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Cat.BuildCraft.Intro")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.Construction")));
		shareForBuilding = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShareForBuilding")), shareForBuilding, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShareForBuilding.Help")));
		buildFromInventory = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BuildFromInventory")), buildFromInventory, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BuildFromInventoryDesc")));
		buildFromInventoryPartial = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BuildFromInventoryPartial")), buildFromInventoryPartial, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BuildFromInventoryPartialDesc")), buildFromInventory, 24f);
		if (VehicleFrameworkCompat.IsActive)
		{
			buildFromVehiclesAway = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BuildFromVehiclesAway")), buildFromVehiclesAway, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BuildFromVehiclesAwayDesc")), buildFromInventory, 24f);
		}
		inventoryConstructDeliver = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.InventoryConstructDeliver")), inventoryConstructDeliver, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.InventoryConstructDeliverDesc")));
		multiSiteConstructDeliver = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.MultiSiteConstructDeliver")), multiSiteConstructDeliver, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.MultiSiteConstructDeliverDesc")), inventoryConstructDeliver, 24f);
		batchWorkDeliveries = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BatchWorkDeliveries")), batchWorkDeliveries, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BatchWorkDeliveriesDesc")));
		orderedConstructTether = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ConstructTether")), orderedConstructTether, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ConstructTetherDesc")));
		haulToSiteOption = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HaulToSiteOption")), haulToSiteOption, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HaulToSiteOptionDesc")));
		shareHandHauledToStorage = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShareHandHauled")), shareHandHauledToStorage, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShareHandHauledDesc")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.Crafting")));
		shareForCrafting = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShareForCrafting")), shareForCrafting, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShareForCraftingDesc")));
		inventoryCraftDeliver = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.InventoryCraftDeliver")), inventoryCraftDeliver, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.InventoryCraftDeliverDesc")), shareForCrafting, 24f);
		if (GatherNotice.Current == BenchGatherNotice.ForeignModGathers)
		{
			HDSettingsUI.Note(c, GatherNotice.Text(BenchGatherNotice.ForeignModGathers), 24f);
		}
		showBenchGatherGizmo = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShowBenchGatherGizmo")), showBenchGatherGizmo, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShowBenchGatherGizmoDesc")));
		shareMeetInMiddle = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShareMeetInMiddle")), shareMeetInMiddle, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShareMeetInMiddle.Help")));
		butcherSpoilingFirst = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ButcherSpoilingFirst")), butcherSpoilingFirst, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ButcherSpoilingFirstDesc")));
		cookSpoilingFirst = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CookSpoilingFirst")), cookSpoilingFirst, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CookSpoilingFirstDesc")));
		cookMostStockFirst = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CookMostStockFirst")), cookMostStockFirst, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CookMostStockFirstDesc")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.Food")));
		mealsOnWheels = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.MealsOnWheels")), mealsOnWheels, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.MealsOnWheelsDesc")));
		if (VehicleFrameworkCompat.IsActive)
		{
			eatFromVehiclesAway = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EatFromVehiclesAway")), eatFromVehiclesAway, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EatFromVehiclesAwayDesc")), mealsOnWheels, 24f);
			medicineFromVehiclesAway = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.MedicineFromVehiclesAway")), medicineFromVehiclesAway, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.MedicineFromVehiclesAwayDesc")));
		}
		drugsForWithdrawal = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.DrugsForWithdrawal")), drugsForWithdrawal, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.DrugsForWithdrawalDesc")));
	}

	private void DrawBulkLoadingCat(SettingsCtx c)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_026d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_039d: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_041e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0434: Unknown result type (might be due to invalid IL or missing references)
		//IL_0449: Unknown result type (might be due to invalid IL or missing references)
		//IL_046a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0495: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_050c: Unknown result type (might be due to invalid IL or missing references)
		//IL_052d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0558: Unknown result type (might be due to invalid IL or missing references)
		//IL_0317: Unknown result type (might be due to invalid IL or missing references)
		//IL_032d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0342: Unknown result type (might be due to invalid IL or missing references)
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_0378: Unknown result type (might be due to invalid IL or missing references)
		//IL_058b: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a0: Unknown result type (might be due to invalid IL or missing references)
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Cat.BulkLoading.Intro")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.PackAnimals")));
		enableBulkUnloadCarriers = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableBulkUnloadCarriers")), enableBulkUnloadCarriers, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableBulkUnloadCarriersDesc")));
		minFreeSpaceToUnloadCarrierPct = Mathf.Round(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.MinFreeSpaceToUnloadCarrier.Lab")), minFreeSpaceToUnloadCarrierPct, 0.1f, 0.9f, GenText.ToStringPercent(minFreeSpaceToUnloadCarrierPct), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.MinFreeSpaceToUnloadCarrier.Help")), enableBulkUnloadCarriers, 24f) * 20f) / 20f;
		reserveCarrierOnUnload = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ReserveCarrierOnUnload")), reserveCarrierOnUnload, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ReserveCarrierOnUnloadDesc")), enableBulkUnloadCarriers, 24f);
		visualUnloadDelay = Mathf.RoundToInt(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.VisualUnloadDelay.Lab")), visualUnloadDelay, 0f, 30f, $"~{(float)visualUnloadDelay / 60f:0.0}s", TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.VisualUnloadDelay.Help")), enableBulkUnloadCarriers, 24f));
		loadPackAnimalBulk = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.LoadPackAnimalBulk")), loadPackAnimalBulk, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.LoadPackAnimalBulkDesc")));
		autoDivertToPackAnimal = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AutoDivertToPackAnimal")), autoDivertToPackAnimal, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AutoDivertToPackAnimalDesc")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.Transporters")));
		enableBulkLoadTransporters = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableBulkLoadTransporters")), enableBulkLoadTransporters, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableBulkLoadTransportersDesc")));
		bulkLoadAiUpdateFrequency = Mathf.RoundToInt(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BulkLoadAiUpdateFrequency.Lab")), bulkLoadAiUpdateFrequency, 10f, 240f, $"~{(float)bulkLoadAiUpdateFrequency / 60f:0.0}s", TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BulkLoadAiUpdateFrequency.Help")), enableBulkLoadTransporters, 24f) / 10f) * 10;
		enableBulkLoadPortal = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableBulkLoadPortal")), enableBulkLoadPortal, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableBulkLoadPortalDesc")));
		enableBulkRefuel = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableBulkRefuel")), enableBulkRefuel, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableBulkRefuelDesc")));
		if (VehicleFrameworkCompat.IsActive)
		{
			HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.Vehicles")));
			enableVehicleFramework = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableVehicleFramework")), enableVehicleFramework, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableVehicleFrameworkDesc")));
			enableBulkLoadVehicles = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableBulkLoadVehicles")), enableBulkLoadVehicles, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableBulkLoadVehiclesDesc")), enableVehicleFramework, 24f);
		}
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.AutoOpenTabs")));
		autoOpenTransporterContents = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AutoOpenTransporterContents")), autoOpenTransporterContents, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AutoOpenTransporterContentsDesc")));
		autoOpenCarrierGear = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AutoOpenCarrierGear")), autoOpenCarrierGear, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AutoOpenCarrierGearDesc")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.AdvancedLoading")));
		enableOpportunisticLoad = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.OpportunisticLoad")), enableOpportunisticLoad, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.OpportunisticLoadDesc")));
		loadOpportunityScanRadius = Mathf.RoundToInt(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.LoadOpportunityRadius.Lab")), loadOpportunityScanRadius, 5f, 100f, Tiles(loadOpportunityScanRadius), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.LoadOpportunityRadius.Help")), enableOpportunisticLoad, 24f));
		enableContinuousLoading = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ContinuousLoading")), enableContinuousLoading, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ContinuousLoadingDesc")));
		loadHybridPathing = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.LoadHybridPathing")), loadHybridPathing, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.LoadHybridPathingDesc")));
		loadPathfindingCandidates = Mathf.RoundToInt(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.LoadPathfindingCandidates.Lab")), loadPathfindingCandidates, 2f, 24f, loadPathfindingCandidates.ToString(), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.LoadPathfindingCandidates.Help")), loadHybridPathing, 24f));
		if (StorageNetworkCompat.IsActive)
		{
			enableStorageNetworkBulkLoad = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableStorageNetworkBulkLoad")), enableStorageNetworkBulkLoad, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableStorageNetworkBulkLoadDesc")));
		}
	}

	private void DrawRoutingCat(SettingsCtx c)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0261: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_0327: Unknown result type (might be due to invalid IL or missing references)
		//IL_033c: Unknown result type (might be due to invalid IL or missing references)
		//IL_035d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0372: Unknown result type (might be due to invalid IL or missing references)
		//IL_0398: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ed: Unknown result type (might be due to invalid IL or missing references)
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Cat.Routing.Intro")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.EnRoute")));
		enRoutePickup = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnRoutePickup")), enRoutePickup, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnRoutePickupDesc")));
		enRoutePathChecker = HDSettingsUI.Segmented(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnRoutePathChecker.Lab")), (enRoutePathChecker != EnRoutePathChecker.Vanilla) ? ((enRoutePathChecker == EnRoutePathChecker.Default) ? 1 : 2) : 0, new string[3]
		{
			((object)Translator.Translate("HaulersDream.Setting.EnRoutePathVanilla.S")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.EnRoutePathDefault.S")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.EnRoutePathPathfinding.S")/*cast due to constrained. prefix*/).ToString()
		}, new string[3]
		{
			((object)Translator.Translate("HaulersDream.Setting.EnRoutePathVanilla.H")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.EnRoutePathDefault.H")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.EnRoutePathPathfinding.H")/*cast due to constrained. prefix*/).ToString()
		}, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnRoutePathCheckerDesc")), enRoutePickup, 24f) switch
		{
			1 => EnRoutePathChecker.Default, 
			0 => EnRoutePathChecker.Vanilla, 
			_ => EnRoutePathChecker.Pathfinding, 
		};
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.OpportunisticDetour")));
		int num = HDSettingsUI.Segmented(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.OpportunisticDetour.Lab")), (int)opportunisticDetour, DetourSegmentLabels(), DetourSegmentHelp(), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.OpportunisticDetour.Desc")), enRoutePickup);
		opportunisticDetour = (OpportunisticDetour)num;
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.StorageRouting")));
		storageRouting = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StorageRouting")), storageRouting, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StorageRoutingDesc")));
		routeSupplies = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteSupplies")), routeSupplies, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteSuppliesDesc")), storageRouting, 24f);
		routeIngredients = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteIngredients")), routeIngredients, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteIngredientsDesc")), storageRouting, 24f);
		routeToEqualPriority = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteToEqualPriority")), routeToEqualPriority, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteToEqualPriorityDesc")), storageRouting, 24f);
		routeToStockpiles = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteToStockpiles")), routeToStockpiles, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteToStockpilesDesc")), storageRouting, 24f);
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.StorageFilters")));
		storageFiltersEnabled = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StorageFilters")), storageFiltersEnabled, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StorageFiltersDesc")));
		storageFilterUseDefaults = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StorageFilterUseDefaults")), storageFilterUseDefaults, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StorageFilterUseDefaultsDesc")), storageFiltersEnabled, 24f);
		storageFilterDenyLwmForOpportunistic = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StorageFilterDenyLwm")), storageFilterDenyLwmForOpportunistic, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StorageFilterDenyLwmDesc")), storageFiltersEnabled, 24f);
		HDSettingsUI.Button(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StorageFilterButton")), delegate
		{
			Find.WindowStack.Add((Window)(object)new Dialog_StorageBuildingFilter(storageBuildingFilter));
		}, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StorageFilterButton.Help")), storageFiltersEnabled, 24f);
	}

	private static string[] YieldLabels3()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		return new string[3]
		{
			((object)Translator.Translate("HaulersDream.Setting.Yield.Off")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.Yield.Drop")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.Yield.Direct")/*cast due to constrained. prefix*/).ToString()
		};
	}

	private static string[] YieldHelps3()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		return new string[3]
		{
			((object)Translator.Translate("HaulersDream.Setting.Yield.OffDesc")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.Yield.DropDesc")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.Yield.DirectDesc")/*cast due to constrained. prefix*/).ToString()
		};
	}

	private void DrawYieldsCat(SettingsCtx c)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0419: Unknown result type (might be due to invalid IL or missing references)
		//IL_042e: Unknown result type (might be due to invalid IL or missing references)
		//IL_044f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0464: Unknown result type (might be due to invalid IL or missing references)
		//IL_0485: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04db: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0506: Unknown result type (might be due to invalid IL or missing references)
		//IL_0527: Unknown result type (might be due to invalid IL or missing references)
		//IL_053c: Unknown result type (might be due to invalid IL or missing references)
		//IL_055c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0571: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_05bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_05de: Unknown result type (might be due to invalid IL or missing references)
		//IL_05fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0600: Unknown result type (might be due to invalid IL or missing references)
		//IL_0617: Unknown result type (might be due to invalid IL or missing references)
		//IL_061c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0633: Unknown result type (might be due to invalid IL or missing references)
		//IL_0638: Unknown result type (might be due to invalid IL or missing references)
		//IL_064d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0681: Unknown result type (might be due to invalid IL or missing references)
		//IL_0696: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_06dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0717: Unknown result type (might be due to invalid IL or missing references)
		//IL_0736: Unknown result type (might be due to invalid IL or missing references)
		//IL_075c: Unknown result type (might be due to invalid IL or missing references)
		//IL_077b: Unknown result type (might be due to invalid IL or missing references)
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Cat.Yields.Intro")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.Header")));
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.Intro")));
		string[] colLabels = YieldLabels3();
		string[] colHelps = YieldHelps3();
		YieldMatrixRow yieldMatrixRow = new YieldMatrixRow
		{
			Label = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.Harvest")),
			Help = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.HarvestHelp")),
			Value = (int)yieldHarvest
		};
		YieldMatrixRow yieldMatrixRow2 = new YieldMatrixRow
		{
			Label = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.Logging")),
			Help = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.LoggingHelp")),
			Value = (int)yieldLogging
		};
		YieldMatrixRow yieldMatrixRow3 = new YieldMatrixRow
		{
			Label = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.Mining")),
			Help = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.MiningHelp")),
			Value = (int)yieldMining
		};
		YieldMatrixRow yieldMatrixRow4 = new YieldMatrixRow
		{
			Label = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.Chunks")),
			Help = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.ChunksHelp")),
			Value = (int)yieldChunks
		};
		YieldMatrixRow yieldMatrixRow5 = new YieldMatrixRow
		{
			Label = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.DeepDrill")),
			Help = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.DeepDrillHelp")),
			Value = (int)yieldDeepDrill
		};
		YieldMatrixRow yieldMatrixRow6 = new YieldMatrixRow
		{
			Label = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.Deconstruct")),
			Help = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.DeconstructHelp")),
			Value = (int)yieldDeconstruct
		};
		YieldMatrixRow yieldMatrixRow7 = new YieldMatrixRow
		{
			Label = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.Animals")),
			Help = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.AnimalsHelp")),
			Value = (int)yieldAnimals
		};
		YieldMatrixRow yieldMatrixRow8 = new YieldMatrixRow
		{
			Label = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.Strip")),
			Help = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.StripHelp")),
			Value = ((yieldStrip != YieldBehavior.Disabled) ? 1 : 0),
			AllowDirect = false
		};
		YieldMatrixRow yieldMatrixRow9 = new YieldMatrixRow
		{
			Label = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.Uninstall")),
			Help = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.UninstallHelp")),
			Value = (int)yieldUninstall
		};
		YieldMatrixRow yieldMatrixRow10 = new YieldMatrixRow
		{
			Label = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.Fishing")),
			Help = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Yield.FishingHelp")),
			Value = (int)yieldFishing
		};
		List<YieldMatrixRow> list = new List<YieldMatrixRow> { yieldMatrixRow, yieldMatrixRow2, yieldMatrixRow3, yieldMatrixRow4, yieldMatrixRow5, yieldMatrixRow6, yieldMatrixRow7, yieldMatrixRow8, yieldMatrixRow9 };
		if (ModsConfig.OdysseyActive)
		{
			list.Add(yieldMatrixRow10);
		}
		HDSettingsUI.YieldMatrix(c, colLabels, colHelps, list);
		yieldHarvest = (YieldBehavior)yieldMatrixRow.Value;
		yieldLogging = (YieldBehavior)yieldMatrixRow2.Value;
		yieldMining = (YieldBehavior)yieldMatrixRow3.Value;
		yieldChunks = (YieldBehavior)yieldMatrixRow4.Value;
		yieldDeepDrill = (YieldBehavior)yieldMatrixRow5.Value;
		yieldDeconstruct = (YieldBehavior)yieldMatrixRow6.Value;
		yieldAnimals = (YieldBehavior)yieldMatrixRow7.Value;
		yieldStrip = ((yieldMatrixRow8.Value != 0) ? YieldBehavior.DropThenHaul : YieldBehavior.Disabled);
		yieldUninstall = (YieldBehavior)yieldMatrixRow9.Value;
		if (ModsConfig.OdysseyActive)
		{
			yieldFishing = (YieldBehavior)yieldMatrixRow10.Value;
		}
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.WhileWorking")));
		sweepNearbyWhileWorking = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.SweepNearbyWhileWorking")), sweepNearbyWhileWorking, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.SweepNearbyWhileWorkingDesc")));
		pickupDelayOnDirectHarvest = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PickupDelayOnDirectHarvest")), pickupDelayOnDirectHarvest, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PickupDelayOnDirectHarvestDesc")));
		keepWorkingWhenFull = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.KeepWorkingWhenFull")), keepWorkingWhenFull, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.KeepWorkingWhenFullDesc")));
		keepWorkingMarginCells = Mathf.RoundToInt(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.KeepWorkingMargin.Lab")), keepWorkingMarginCells, 0f, 30f, Tiles(keepWorkingMarginCells), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.KeepWorkingMarginDesc")), keepWorkingWhenFull, 24f));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.AfterKill")));
		haulTamedSlaughter = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HaulTamedSlaughter")), haulTamedSlaughter, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HaulTamedSlaughterDesc")));
		haulWildKills = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HaulWildKills")), haulWildKills, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.HaulWildKillsDesc")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.Stripping")));
		autoStripMode = HDSettingsUI.Segmented(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AutoStripMode.Lab")), (autoStripMode != AutoStripMode.AllHauls) ? ((autoStripMode == AutoStripMode.DisposalOnly) ? 1 : 2) : 0, new string[3]
		{
			((object)Translator.Translate("HaulersDream.Setting.AutoStripAll.S")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.AutoStripDisposal.S")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.AutoStripOff.S")/*cast due to constrained. prefix*/).ToString()
		}, new string[3]
		{
			((object)Translator.Translate("HaulersDream.Setting.AutoStripAll")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.AutoStripDisposal")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.AutoStripOff")/*cast due to constrained. prefix*/).ToString()
		}, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AutoStrip.Help"))) switch
		{
			1 => AutoStripMode.DisposalOnly, 
			0 => AutoStripMode.AllHauls, 
			_ => AutoStripMode.Off, 
		};
		stripColonistCorpses = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StripColonists")), stripColonistCorpses, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StripColonistsDesc")), autoStripMode != AutoStripMode.Off || stripBeforeCremation, 24f);
		stripBeforeCremation = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StripBeforeCremation")), stripBeforeCremation, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.StripBeforeCremationDesc")));
		bool enabled = autoStripMode != AutoStripMode.Off || yieldStrip != YieldBehavior.Disabled || stripBeforeCremation;
		int num = HDSettingsUI.Segmented(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.TaintedSmeltable.Lab")), (int)taintedSmeltablePolicy, TaintedOptionLabels(), TaintedOptionHelps(), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Tainted.Help")), enabled, 24f);
		taintedSmeltablePolicy = (TaintedApparelPolicy)num;
		int num2 = HDSettingsUI.Segmented(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.TaintedNonSmeltable.Lab")), (int)taintedNonSmeltablePolicy, TaintedOptionLabels(), TaintedOptionHelps(), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Tainted.Help")), enabled, 24f);
		taintedNonSmeltablePolicy = (TaintedApparelPolicy)num2;
	}

	private static string[] TaintedOptionLabels()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		return new string[4]
		{
			((object)Translator.Translate("HaulersDream.Setting.TaintedTake.S")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.TaintedLeave.S")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.TaintedForbid.S")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.TaintedDestroy.S")/*cast due to constrained. prefix*/).ToString()
		};
	}

	private static string[] TaintedOptionHelps()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		return new string[4]
		{
			((object)Translator.Translate("HaulersDream.Setting.TaintedTake")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.TaintedLeave")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.TaintedForbid")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.TaintedDestroy")/*cast due to constrained. prefix*/).ToString()
		};
	}

	private void DrawWhoCat(SettingsCtx c)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_028f: Unknown result type (might be due to invalid IL or missing references)
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Cat.Who.Intro")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.WhoCanHaul")));
		pauseWhileDrafted = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PauseWhileDrafted")), pauseWhileDrafted, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PauseWhileDrafted.Help")));
		allowMechanoids = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AllowMechanoids")), allowMechanoids, TaggedString.op_Implicit(Translator.Translate("HaulersDream.AllowMechanoidsDesc")));
		mechHaulMultiplier = Mathf.Round(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.MechHaulMultiplier.Lab")), mechHaulMultiplier, 1f, 5f, "×" + mechHaulMultiplier.ToString("0.0"), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.MechHaulMultiplierDesc")), allowMechanoids, 24f) * 10f) / 10f;
		allowAnimals = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.AllowAnimals")), allowAnimals, TaggedString.op_Implicit(Translator.Translate("HaulersDream.AllowAnimalsDesc")));
		allowIncapable = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AllowIncapable")), allowIncapable, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AllowIncapable.Help")));
		enableOnNonHomeMaps = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableOnNonHomeMaps")), enableOnNonHomeMaps, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.EnableOnNonHomeMaps.Help")));
		nonHomeMapsPlayerControlledOnly = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.NonHomeMapsPlayerControlledOnly")), nonHomeMapsPlayerControlledOnly, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.NonHomeMapsPlayerControlledOnly.Help")), enableOnNonHomeMaps, 24f);
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.WorkOverrideHeader")));
		allPawnsCanHaul = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AllCanHaul")), allPawnsCanHaul, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AllCanHaulDesc")));
		allPawnsCanClean = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AllCanClean")), allPawnsCanClean, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AllCanCleanDesc")));
		allPawnsCanCutPlants = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AllCanCutPlants")), allPawnsCanCutPlants, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AllCanCutPlantsDesc")));
	}

	private void DrawPlannersCat(SettingsCtx c)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0309: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Unknown result type (might be due to invalid IL or missing references)
		//IL_0386: Unknown result type (might be due to invalid IL or missing references)
		//IL_038b: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03db: Unknown result type (might be due to invalid IL or missing references)
		//IL_0405: Unknown result type (might be due to invalid IL or missing references)
		//IL_0430: Unknown result type (might be due to invalid IL or missing references)
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Cat.Planners.Intro")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.PlanningTools")));
		planRoutes = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PlanRoutes")), planRoutes, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PlanRoutesDesc")));
		planCrafting = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PlanCrafting")), planCrafting, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PlanCraftingDesc")));
		planForUnassignedWork = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PlanForUnassignedWork")), planForUnassignedWork, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.PlanForUnassignedWorkDesc")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.Batches")));
		batchByDefault = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BatchByDefault")), batchByDefault, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.BatchByDefaultDesc")));
		defaultBatchSize = Mathf.RoundToInt(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.DefaultBatchSize.Lab")), defaultBatchSize, 1f, 200f, defaultBatchSize.ToString(), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.DefaultBatchSize.Help"))));
		craftBatchTimeoutHours = Mathf.Round(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CraftBatchTimeout.Lab")), craftBatchTimeoutHours, 0f, 8f, (craftBatchTimeoutHours <= 0f) ? OffLabel : Hours(craftBatchTimeoutHours), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CraftBatchTimeout.Help"))) * 2f) / 2f;
		if (CommonSenseCompat.IsActive)
		{
			allowBatchUnderCommonSense = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AllowBatchUnderCommonSense")), allowBatchUnderCommonSense, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AllowBatchUnderCommonSenseDesc")));
		}
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.RoutePlanning")));
		routeMaxAmount = Mathf.RoundToInt(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteMaxAmount.Lab")), routeMaxAmount, 5f, 100f, routeMaxAmount.ToString(), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteMaxAmount.Help"))));
		int num = HDSettingsUI.Segmented(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteSelection.Lab")), (routeSelectionMethod != RouteSelectionMethod.MostStopsPerTravel) ? 1 : 0, new string[2]
		{
			((object)Translator.Translate("HaulersDream.PlanRoute.SelMostStops.S")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.PlanRoute.SelNearest.S")/*cast due to constrained. prefix*/).ToString()
		}, new string[2]
		{
			((object)Translator.Translate("HaulersDream.PlanRoute.SelMostStops.H")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.PlanRoute.SelNearest.H")/*cast due to constrained. prefix*/).ToString()
		}, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteSelection.Help")));
		routeSelectionMethod = ((num != 0) ? RouteSelectionMethod.NearestToTarget : RouteSelectionMethod.MostStopsPerTravel);
		int num2 = HDSettingsUI.Segmented(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteDistance.Lab")), (routeDistanceBasis != RouteDistanceBasis.StraightLine) ? 1 : 0, new string[2]
		{
			((object)Translator.Translate("HaulersDream.PlanRoute.DistStraight.S")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.PlanRoute.DistWalking.S")/*cast due to constrained. prefix*/).ToString()
		}, new string[2]
		{
			((object)Translator.Translate("HaulersDream.PlanRoute.DistStraight.H")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.PlanRoute.DistWalking.H")/*cast due to constrained. prefix*/).ToString()
		}, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteDistance.Help")));
		routeDistanceBasis = ((num2 != 0) ? RouteDistanceBasis.WalkingPath : RouteDistanceBasis.StraightLine);
		routeOrderExactMax = Mathf.RoundToInt(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteOrderEffort.Lab")), routeOrderExactMax, 8f, 14f, routeOrderExactMax.ToString(), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.RouteOrderEffort.Help"))));
	}

	private void DrawAdvancedCat(SettingsCtx c)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_0258: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0273: Unknown result type (might be due to invalid IL or missing references)
		//IL_0289: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0300: Unknown result type (might be due to invalid IL or missing references)
		//IL_0316: Unknown result type (might be due to invalid IL or missing references)
		//IL_031b: Unknown result type (might be due to invalid IL or missing references)
		//IL_032f: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_0379: Unknown result type (might be due to invalid IL or missing references)
		//IL_038e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03af: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c4: Unknown result type (might be due to invalid IL or missing references)
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Cat.Advanced.Intro")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.UnloadTiming")));
		intervalUnloadHours = Mathf.Round(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.IntervalUnload.Lab")), intervalUnloadHours, 0f, 24f, (intervalUnloadHours <= 0f) ? OffLabel : Hours(intervalUnloadHours), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.IntervalUnload.Help"))) * 2f) / 2f;
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.SafetyNet")));
		alertCannotUnload = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AlertCannotUnload")), alertCannotUnload, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AlertCannotUnloadDesc")));
		alertStuckHours = Mathf.Round(HDSettingsUI.Slider(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AlertStuckHours.Lab")), alertStuckHours, 1f, 72f, Hours(alertStuckHours), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.AlertStuckHours.Help")), alertCannotUnload, 24f) * 2f) / 2f;
		enableSoftlockDrop = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.SoftlockDrop")), enableSoftlockDrop, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.SoftlockDropDesc")));
		enableQuestPawnDrop = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.QuestPawnDrop")), enableQuestPawnDrop, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.QuestPawnDropDesc")));
		cleanupOnSave = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CleanupOnSave")), cleanupOnSave, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.CleanupOnSaveDesc")));
		HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.Notifications")));
		HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.Notify.Intro")));
		int num = HDSettingsUI.Segmented(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.NotifyThreshold.Lab")), (int)notifyThreshold, new string[4]
		{
			((object)Translator.Translate("HaulersDream.Setting.NotifyThreshold.All")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.NotifyThreshold.Comments")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.NotifyThreshold.Fixed")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.NotifyThreshold.Never")/*cast due to constrained. prefix*/).ToString()
		}, new string[4]
		{
			((object)Translator.Translate("HaulersDream.Setting.NotifyThreshold.All.Desc")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.NotifyThreshold.Comments.Desc")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.NotifyThreshold.Fixed.Desc")/*cast due to constrained. prefix*/).ToString(),
			((object)Translator.Translate("HaulersDream.Setting.NotifyThreshold.Never.Desc")/*cast due to constrained. prefix*/).ToString()
		}, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.NotifyThreshold.Help")));
		notifyThreshold = (NotifyThreshold)Mathf.Clamp(num, 0, 3);
		if (Prefs.DevMode)
		{
			HDSettingsUI.Header(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Head.Developer")));
			verboseLogging = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.VerboseLogging")), verboseLogging, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.VerboseLogging.Help")));
			drawDetourLines = HDSettingsUI.Checkbox(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.DrawDetourLines")), drawDetourLines, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.DrawDetourLinesDesc")));
		}
		else
		{
			HDSettingsUI.Note(c, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Advanced.DevHint")));
		}
	}

	private static object CloneValue(object v)
	{
		if (v != null)
		{
			if (!(v is List<string> collection))
			{
				if (!(v is StorageBuildingFilter storageBuildingFilter))
				{
					if (!(v is Dictionary<string, RouteDialogPrefs> dictionary))
					{
						if (!(v is Dictionary<string, SowRouteTemplate> dictionary2))
						{
							if (!(v is Dictionary<string, RemoveFloorRouteTemplate> dictionary3))
							{
								if (v is string)
								{
									return v;
								}
								if (!v.GetType().IsValueType)
								{
									throw new InvalidOperationException($"HaulersDream profile capture: reference-type setting field of type {v.GetType()} has no " + "deep-clone case in CloneValue — add one (and a matching ValuesEqual case).");
								}
								return v;
							}
							Dictionary<string, RemoveFloorRouteTemplate> dictionary4 = new Dictionary<string, RemoveFloorRouteTemplate>(dictionary3.Count);
							{
								foreach (KeyValuePair<string, RemoveFloorRouteTemplate> item in dictionary3)
								{
									dictionary4[item.Key] = CloneRemoveFloorTemplate(item.Value);
								}
								return dictionary4;
							}
						}
						Dictionary<string, SowRouteTemplate> dictionary5 = new Dictionary<string, SowRouteTemplate>(dictionary2.Count);
						{
							foreach (KeyValuePair<string, SowRouteTemplate> item2 in dictionary2)
							{
								dictionary5[item2.Key] = CloneSowTemplate(item2.Value);
							}
							return dictionary5;
						}
					}
					Dictionary<string, RouteDialogPrefs> dictionary6 = new Dictionary<string, RouteDialogPrefs>(dictionary.Count);
					{
						foreach (KeyValuePair<string, RouteDialogPrefs> item3 in dictionary)
						{
							dictionary6[item3.Key] = CloneRoutePrefs(item3.Value);
						}
						return dictionary6;
					}
				}
				StorageBuildingFilter obj = new StorageBuildingFilter();
				IEnumerable<string> denied = storageBuildingFilter.denied;
				obj.denied = new HashSet<string>(denied ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
				denied = storageBuildingFilter.allowed;
				obj.allowed = new HashSet<string>(denied ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
				return obj;
			}
			return new List<string>(collection);
		}
		return null;
	}

	private static RouteDialogPrefs CloneRoutePrefs(RouteDialogPrefs s)
	{
		if (s == null)
		{
			return null;
		}
		return new RouteDialogPrefs
		{
			mode = s.mode,
			maxTravel = s.maxTravel,
			radius = s.radius,
			amount = s.amount,
			smart = s.smart,
			allowHarvest = s.allowHarvest,
			growthThreshold = s.growthThreshold,
			selectionMethod = s.selectionMethod,
			distanceBasis = s.distanceBasis
		};
	}

	private static SowRouteTemplate CloneSowTemplate(SowRouteTemplate s)
	{
		if (s == null)
		{
			return null;
		}
		return new SowRouteTemplate
		{
			mode = s.mode,
			maxTravel = s.maxTravel,
			radius = s.radius,
			amount = s.amount,
			smart = s.smart,
			selectionMethod = s.selectionMethod
		};
	}

	private static RemoveFloorRouteTemplate CloneRemoveFloorTemplate(RemoveFloorRouteTemplate s)
	{
		if (s == null)
		{
			return null;
		}
		return new RemoveFloorRouteTemplate
		{
			mode = s.mode,
			maxTravel = s.maxTravel,
			radius = s.radius,
			amount = s.amount,
			selectionMethod = s.selectionMethod
		};
	}

	public static void CopySettings(HaulersDreamSettings from, HaulersDreamSettings to)
	{
		FieldInfo[] settingFields = SettingFields;
		foreach (FieldInfo fieldInfo in settingFields)
		{
			fieldInfo.SetValue(to, CloneValue(fieldInfo.GetValue(from)));
		}
	}

	private static bool SetEq(HashSet<string> a, HashSet<string> b)
	{
		int num = a?.Count ?? 0;
		int num2 = b?.Count ?? 0;
		if (num != num2)
		{
			return false;
		}
		if (num != 0)
		{
			return a.SetEquals(b);
		}
		return true;
	}

	private static bool RoutePrefsEqual(RouteDialogPrefs a, RouteDialogPrefs b)
	{
		if (a == null || b == null)
		{
			if (a == null)
			{
				return b == null;
			}
			return false;
		}
		if (a.mode == b.mode && a.maxTravel == b.maxTravel && a.radius == b.radius && a.amount == b.amount && a.smart == b.smart && a.allowHarvest == b.allowHarvest && a.growthThreshold == b.growthThreshold && a.selectionMethod == b.selectionMethod)
		{
			return a.distanceBasis == b.distanceBasis;
		}
		return false;
	}

	private static bool SowTemplateEqual(SowRouteTemplate a, SowRouteTemplate b)
	{
		if (a == null || b == null)
		{
			if (a == null)
			{
				return b == null;
			}
			return false;
		}
		return a.ValueEquals(b);
	}

	private static bool RemoveFloorTemplateEqual(RemoveFloorRouteTemplate a, RemoveFloorRouteTemplate b)
	{
		if (a == null || b == null)
		{
			if (a == null)
			{
				return b == null;
			}
			return false;
		}
		return a.ValueEquals(b);
	}

	private static bool ValuesEqual(object x, object y)
	{
		if (x == null || y == null)
		{
			if (x == null)
			{
				return y == null;
			}
			return false;
		}
		if (!(x is List<string> list))
		{
			if (!(x is StorageBuildingFilter storageBuildingFilter))
			{
				if (!(x is Dictionary<string, RouteDialogPrefs> dictionary))
				{
					if (!(x is Dictionary<string, SowRouteTemplate> dictionary2))
					{
						if (x is Dictionary<string, RemoveFloorRouteTemplate> dictionary3)
						{
							if (!(y is Dictionary<string, RemoveFloorRouteTemplate> dictionary4) || dictionary3.Count != dictionary4.Count)
							{
								return false;
							}
							foreach (KeyValuePair<string, RemoveFloorRouteTemplate> item in dictionary3)
							{
								if (!dictionary4.TryGetValue(item.Key, out var value) || !RemoveFloorTemplateEqual(item.Value, value))
								{
									return false;
								}
							}
							return true;
						}
						if (x is float num && y is float num2)
						{
							if (num != num2)
							{
								return Math.Abs(num - num2) <= 0.0001f;
							}
							return true;
						}
						return x.Equals(y);
					}
					if (!(y is Dictionary<string, SowRouteTemplate> dictionary5) || dictionary2.Count != dictionary5.Count)
					{
						return false;
					}
					foreach (KeyValuePair<string, SowRouteTemplate> item2 in dictionary2)
					{
						if (!dictionary5.TryGetValue(item2.Key, out var value2) || !SowTemplateEqual(item2.Value, value2))
						{
							return false;
						}
					}
					return true;
				}
				if (!(y is Dictionary<string, RouteDialogPrefs> dictionary6) || dictionary.Count != dictionary6.Count)
				{
					return false;
				}
				foreach (KeyValuePair<string, RouteDialogPrefs> item3 in dictionary)
				{
					if (!dictionary6.TryGetValue(item3.Key, out var value3) || !RoutePrefsEqual(item3.Value, value3))
					{
						return false;
					}
				}
				return true;
			}
			if (y is StorageBuildingFilter storageBuildingFilter2 && SetEq(storageBuildingFilter.denied, storageBuildingFilter2.denied))
			{
				return SetEq(storageBuildingFilter.allowed, storageBuildingFilter2.allowed);
			}
			return false;
		}
		if (y is List<string> list2 && list.Count == list2.Count)
		{
			return new HashSet<string>(list).SetEquals(list2);
		}
		return false;
	}

	public static bool StateEquals(HaulersDreamSettings a, HaulersDreamSettings b)
	{
		FieldInfo[] settingFields = SettingFields;
		foreach (FieldInfo fieldInfo in settingFields)
		{
			if (!ValuesEqual(fieldInfo.GetValue(a), fieldInfo.GetValue(b)))
			{
				return false;
			}
		}
		return true;
	}

	private HaulersDreamSettings CaptureSnapshot()
	{
		HaulersDreamSettings haulersDreamSettings = new HaulersDreamSettings();
		CopySettings(this, haulersDreamSettings);
		return haulersDreamSettings;
	}

	private void ApplyState(HaulersDreamSettings snap)
	{
		if (snap != null)
		{
			CopySettings(snap, this);
			ResetTransientCaches();
		}
	}

	public void ApplyProfile(SettingsProfile p)
	{
		if (p?.snapshot != null)
		{
			ApplyState(p.snapshot);
			activeProfileName = p.name;
		}
	}

	public void ApplyDefaultProfile()
	{
		ResetToDefaults();
		activeProfileName = "";
	}

	public static void VerifyProfileIntegrity()
	{
		try
		{
			HaulersDreamSettings haulersDreamSettings = new HaulersDreamSettings();
			haulersDreamSettings.ResetToDefaults();
			HaulersDreamSettings haulersDreamSettings2 = new HaulersDreamSettings();
			haulersDreamSettings2.ResetToDefaults();
			if (!StateEquals(haulersDreamSettings, haulersDreamSettings2))
			{
				HDLog.Err("PROFILE TRIPWIRE: two freshly-reset default configurations do not compare equal — a serialized reference-type setting is missing a ValuesEqual case in SettingsProfiles, so the profile selector shows \"Custom (unsaved)\" on a pristine or just-reset config. Please report this with the log attached.");
			}
			HaulersDreamSettings haulersDreamSettings3 = haulersDreamSettings.CaptureSnapshot();
			if (!StateEquals(haulersDreamSettings, haulersDreamSettings3))
			{
				HDLog.Err("PROFILE TRIPWIRE: a captured profile snapshot does not equal its source — a serialized reference-type setting is cloned or compared incorrectly in SettingsProfiles. Please report this with the log attached.");
			}
			if (haulersDreamSettings3.settingsSchemaVersion != 1)
			{
				HDLog.Err("PROFILE TRIPWIRE: a captured profile snapshot carries settings schema " + $"{haulersDreamSettings3.settingsSchemaVersion}, not {1} — its saved copy will re-run the " + "one-time yield migration on every launch and reset the \"Collect work results\" settings. Please report this with the log attached.");
			}
			HaulersDreamSettings haulersDreamSettings4 = haulersDreamSettings.CaptureSnapshot();
			haulersDreamSettings4.rememberedSowRoutesByDef["HaulersDreamProfileSelfTest"] = new SowRouteTemplate
			{
				maxTravel = 42
			};
			haulersDreamSettings4.rememberedRemoveFloorRoutesByDef["HaulersDreamProfileSelfTest"] = new RemoveFloorRouteTemplate
			{
				radius = 3
			};
			string token = haulersDreamSettings.ExportProfileToString(haulersDreamSettings4, "self-test");
			if (!haulersDreamSettings.TryImportProfileFromString(token, out var _, out var snapshot) || !StateEquals(haulersDreamSettings4, snapshot))
			{
				HDLog.Err("PROFILE TRIPWIRE: a profile that uses remembered sow / remove-floor routes does not survive copy -> paste — the copy/paste codec is missing an encode/parse case in SettingsProfiles. Please report this with the log attached.");
			}
		}
		catch (Exception ex)
		{
			HDLog.Err("PROFILE TRIPWIRE: capturing or comparing a default profile threw " + ex.GetType().Name + ": " + ex.Message + " — a serialized reference-type setting is missing a CloneValue / codec case in SettingsProfiles, so \"Create new profile\" / \"Copy profile\" will fail. Please report this with the log attached.");
		}
		LongEventHandler.ExecuteWhenFinished((Action)VerifySerializationIntegrity);
	}

	private static void VerifySerializationIntegrity()
	{
		try
		{
			HaulersDreamSettings haulersDreamSettings = new HaulersDreamSettings();
			haulersDreamSettings.ResetToDefaults();
			VerifyScribeRoundTrip(haulersDreamSettings.CaptureSnapshot());
			HaulersDreamSettings haulersDreamSettings2 = haulersDreamSettings.CaptureSnapshot();
			haulersDreamSettings2.yieldMining = YieldBehavior.Disabled;
			haulersDreamSettings2.yieldStrip = YieldBehavior.Disabled;
			haulersDreamSettings2.yieldHarvest = YieldBehavior.DirectToInventory;
			haulersDreamSettings2.unloadGraceTicks = 1234;
			VerifyScribeRoundTrip(haulersDreamSettings2);
			VerifyLegacyYieldMigration();
		}
		catch (Exception ex)
		{
			HDLog.Err("PROFILE TRIPWIRE: the settings serializer self-check could not run: " + ex);
		}
	}

	private static void VerifyScribeRoundTrip(HaulersDreamSettings source)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		if ((int)Scribe.mode != 0)
		{
			return;
		}
		bool serializingSnapshot = SerializingSnapshot;
		SerializingSnapshot = true;
		try
		{
			string text = Scribe.saver.DebugOutputFor((IExposable)(object)source);
			if (!string.IsNullOrEmpty(text))
			{
				HaulersDreamSettings haulersDreamSettings = LoadFromSaveableXml(text);
				if (haulersDreamSettings == null || !StateEquals(source, haulersDreamSettings))
				{
					HDLog.Err("PROFILE TRIPWIRE: a settings snapshot does not survive a save -> load round-trip through the game's own settings serializer — HaulersDreamSettings.ExposeData is CHANGING a value while loading it (a migration or a clamp firing when it should not). Saved profiles will silently lose those settings on every launch. Please report this with the log attached.");
				}
			}
		}
		catch (Exception ex)
		{
			HDLog.Err("PROFILE TRIPWIRE round-trip self-check could not run: " + HDFault.Render(ex));
		}
		finally
		{
			SerializingSnapshot = serializingSnapshot;
			if ((int)Scribe.mode != 0)
			{
				Scribe.ForceStop();
			}
		}
	}

	private static void VerifyLegacyYieldMigration()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		if ((int)Scribe.mode != 0)
		{
			return;
		}
		bool serializingSnapshot = SerializingSnapshot;
		SerializingSnapshot = false;
		try
		{
			HaulersDreamSettings haulersDreamSettings = LoadFromSaveableXml("<saveable><pickupMode>DirectToInventory</pickupMode><haulMining>False</haulMining></saveable>");
			if (haulersDreamSettings == null)
			{
				HDLog.Err("PROFILE TRIPWIRE: a pre-#80 legacy settings node could not be loaded at all. Upgrading from an old Hauler's Dream may lose the \"Collect work results\" options. Please report this with the log attached.");
				return;
			}
			List<string> list = new List<string>();
			ExpectYield(list, "yieldHarvest", haulersDreamSettings.yieldHarvest, YieldBehavior.DirectToInventory);
			ExpectYield(list, "yieldLogging", haulersDreamSettings.yieldLogging, YieldBehavior.DirectToInventory);
			ExpectYield(list, "yieldMining", haulersDreamSettings.yieldMining, YieldBehavior.Disabled);
			ExpectYield(list, "yieldChunks", haulersDreamSettings.yieldChunks, YieldBehavior.Disabled);
			ExpectYield(list, "yieldDeepDrill", haulersDreamSettings.yieldDeepDrill, YieldBehavior.DirectToInventory);
			ExpectYield(list, "yieldDeconstruct", haulersDreamSettings.yieldDeconstruct, YieldBehavior.DirectToInventory);
			ExpectYield(list, "yieldAnimals", haulersDreamSettings.yieldAnimals, YieldBehavior.DirectToInventory);
			ExpectYield(list, "yieldStrip", haulersDreamSettings.yieldStrip, YieldBehavior.DropThenHaul);
			ExpectYield(list, "yieldUninstall", haulersDreamSettings.yieldUninstall, YieldBehavior.DirectToInventory);
			ExpectYield(list, "yieldFishing", haulersDreamSettings.yieldFishing, YieldBehavior.DropThenHaul);
			if (haulersDreamSettings.settingsSchemaVersion != 1)
			{
				list.Add($"settingsSchemaVersion={haulersDreamSettings.settingsSchemaVersion} " + $"(expected {1})");
			}
			if (list.Count > 0)
			{
				HDLog.Err("PROFILE TRIPWIRE: the one-time upgrade of a pre-#80 configuration no longer produces the right \"Collect work results\" values — " + string.Join(", ", list) + ". Anyone upgrading from an old Hauler's Dream will silently get the wrong settings. Please report this with the log attached.");
			}
		}
		catch (Exception ex)
		{
			HDLog.Err("PROFILE TRIPWIRE legacy-migration self-check could not run: " + HDFault.Render(ex));
		}
		finally
		{
			SerializingSnapshot = serializingSnapshot;
			if ((int)Scribe.mode != 0)
			{
				Scribe.ForceStop();
			}
		}
	}

	private static void ExpectYield(List<string> problems, string name, YieldBehavior actual, YieldBehavior want)
	{
		if (actual != want)
		{
			problems.Add($"{name}={actual} (expected {want})");
		}
	}

	private static HaulersDreamSettings LoadFromSaveableXml(string saveableXml)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.LoadXml("<HaulersDreamSelfTest>" + saveableXml + "</HaulersDreamSelfTest>");
		HaulersDreamSettings result = null;
		try
		{
			Scribe.mode = (LoadSaveMode)2;
			Scribe.loader.curXmlParent = xmlDocument.DocumentElement;
			Scribe_Deep.Look<HaulersDreamSettings>(ref result, "saveable", Array.Empty<object>());
			return result;
		}
		finally
		{
			Scribe.ForceStop();
		}
	}

	public void SaveActiveProfile()
	{
		SettingsProfile activeProfile = ActiveProfile;
		if (activeProfile != null)
		{
			activeProfile.snapshot = CaptureSnapshot();
		}
	}

	public SettingsProfile SaveAsNewProfile(string profileName)
	{
		profileName = profileName?.Trim();
		if (string.IsNullOrEmpty(profileName))
		{
			return null;
		}
		if (savedProfiles == null)
		{
			savedProfiles = new List<SettingsProfile>();
		}
		SettingsProfile settingsProfile = GenCollection.FirstOrDefault<SettingsProfile>(savedProfiles, (Predicate<SettingsProfile>)((SettingsProfile p) => string.Equals(p.name, profileName, StringComparison.OrdinalIgnoreCase)));
		if (settingsProfile != null)
		{
			settingsProfile.snapshot = CaptureSnapshot();
			activeProfileName = settingsProfile.name;
			return settingsProfile;
		}
		SettingsProfile settingsProfile2 = new SettingsProfile(profileName, CaptureSnapshot());
		savedProfiles.Add(settingsProfile2);
		activeProfileName = profileName;
		return settingsProfile2;
	}

	public void DeleteProfile(SettingsProfile p)
	{
		if (p != null && savedProfiles != null)
		{
			savedProfiles.Remove(p);
			if (activeProfileName == p.name)
			{
				activeProfileName = "";
			}
		}
	}

	private void ResetTransientCaches()
	{
		ruleMap = null;
	}

	private static bool TryParseModVersion(string s, out Version v)
	{
		v = null;
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		if (!s.Contains("."))
		{
			s += ".0";
		}
		return Version.TryParse(s, out v);
	}

	public HaulersDreamSettings DefaultsForVersion(string version)
	{
		HaulersDreamSettings haulersDreamSettings = new HaulersDreamSettings();
		haulersDreamSettings.ResetToDefaults();
		if (DefaultChanges.Count > 0 && TryParseModVersion(version, out var asked))
		{
			foreach (DefaultChange item in from c in DefaultChanges
				where TryParseModVersion(c.version, out var v) && v > asked
				orderby (!TryParseModVersion(c.version, out var v)) ? new Version(0, 0) : v descending
				select c)
			{
				foreach (KeyValuePair<string, string> item2 in item.preChange)
				{
					if (SettingFieldsByName.TryGetValue(item2.Key, out var value))
					{
						DecodeFieldValue(haulersDreamSettings, value, item2.Value);
					}
				}
			}
		}
		return haulersDreamSettings;
	}

	public string ExportProfileToString(HaulersDreamSettings snapshot, string name)
	{
		HaulersDreamSettings obj = DefaultsForVersion(CurrentModVersion);
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		FieldInfo[] settingFields = SettingFields;
		foreach (FieldInfo fieldInfo in settingFields)
		{
			object value = fieldInfo.GetValue(snapshot);
			if (!ValuesEqual(value, fieldInfo.GetValue(obj)))
			{
				dictionary[fieldInfo.Name] = EncodeFieldValue(value);
			}
		}
		return ProfileCodec.Encode(CurrentModVersion, name ?? "", dictionary);
	}

	public bool TryImportProfileFromString(string token, out string name, out HaulersDreamSettings snapshot)
	{
		name = null;
		snapshot = null;
		if (!ProfileCodec.TryDecode(token, out var data))
		{
			return false;
		}
		HaulersDreamSettings haulersDreamSettings = DefaultsForVersion(data.Version);
		foreach (KeyValuePair<string, string> item in data.Changed)
		{
			if (SettingFieldsByName.TryGetValue(item.Key, out var value))
			{
				DecodeFieldValue(haulersDreamSettings, value, item.Value);
			}
		}
		name = data.Name ?? "";
		snapshot = haulersDreamSettings;
		return true;
	}

	public SettingsProfile ImportAsProfile(string name, HaulersDreamSettings snapshot)
	{
		name = name?.Trim();
		if (string.IsNullOrEmpty(name) || snapshot == null)
		{
			return null;
		}
		if (savedProfiles == null)
		{
			savedProfiles = new List<SettingsProfile>();
		}
		SettingsProfile settingsProfile = GenCollection.FirstOrDefault<SettingsProfile>(savedProfiles, (Predicate<SettingsProfile>)((SettingsProfile p) => string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)));
		if (settingsProfile != null)
		{
			settingsProfile.snapshot = snapshot;
		}
		else
		{
			settingsProfile = new SettingsProfile(name, snapshot);
			savedProfiles.Add(settingsProfile);
		}
		ApplyProfile(settingsProfile);
		return settingsProfile;
	}

	private static string EncodeFieldValue(object v)
	{
		if (v != null)
		{
			if (!(v is bool))
			{
				if (!(v is List<string> values))
				{
					if (!(v is StorageBuildingFilter storageBuildingFilter))
					{
						if (!(v is Dictionary<string, RouteDialogPrefs> source))
						{
							if (!(v is Dictionary<string, SowRouteTemplate> source2))
							{
								if (!(v is Dictionary<string, RemoveFloorRouteTemplate> source3))
								{
									if (!(v is Enum obj))
									{
										if (!(v is float num))
										{
											if (v is int num2)
											{
												return num2.ToString(CultureInfo.InvariantCulture);
											}
											return Convert.ToString(v, CultureInfo.InvariantCulture);
										}
										return num.ToString("R", CultureInfo.InvariantCulture);
									}
									return obj.ToString();
								}
								return string.Join('\u001e'.ToString(), source3.Select((KeyValuePair<string, RemoveFloorRouteTemplate> kv) => kv.Key + "\u001f" + EncodeRemoveFloorTemplate(kv.Value)));
							}
							return string.Join('\u001e'.ToString(), source2.Select((KeyValuePair<string, SowRouteTemplate> kv) => kv.Key + "\u001f" + EncodeSowTemplate(kv.Value)));
						}
						return string.Join('\u001e'.ToString(), source.Select((KeyValuePair<string, RouteDialogPrefs> kv) => kv.Key + "\u001f" + EncodeRoutePrefs(kv.Value)));
					}
					string separator = '\u001f'.ToString();
					IEnumerable<string> denied = storageBuildingFilter.denied;
					string text = string.Join(separator, denied ?? Enumerable.Empty<string>());
					string separator2 = '\u001f'.ToString();
					denied = storageBuildingFilter.allowed;
					return text + "\u001e" + string.Join(separator2, denied ?? Enumerable.Empty<string>());
				}
				return string.Join('\u001f'.ToString(), values);
			}
			if (!(bool)v)
			{
				return "0";
			}
			return "1";
		}
		return "";
	}

	private static string EncodeRoutePrefs(RouteDialogPrefs p)
	{
		if (p == null)
		{
			p = new RouteDialogPrefs();
		}
		return string.Join('\u001f'.ToString(), p.mode.ToString(), p.maxTravel.ToString(CultureInfo.InvariantCulture), p.radius.ToString(CultureInfo.InvariantCulture), p.amount.ToString(CultureInfo.InvariantCulture), p.smart ? "1" : "0", p.allowHarvest ? "1" : "0", p.growthThreshold.ToString(CultureInfo.InvariantCulture), p.selectionMethod.ToString(), p.distanceBasis.ToString());
	}

	private static string EncodeSowTemplate(SowRouteTemplate p)
	{
		if (p == null)
		{
			p = new SowRouteTemplate();
		}
		return string.Join('\u001f'.ToString(), p.mode.ToString(), p.maxTravel.ToString(CultureInfo.InvariantCulture), p.radius.ToString(CultureInfo.InvariantCulture), p.amount.ToString(CultureInfo.InvariantCulture), p.smart ? "1" : "0", p.selectionMethod.ToString());
	}

	private static string EncodeRemoveFloorTemplate(RemoveFloorRouteTemplate p)
	{
		if (p == null)
		{
			p = new RemoveFloorRouteTemplate();
		}
		return string.Join('\u001f'.ToString(), p.mode.ToString(), p.maxTravel.ToString(CultureInfo.InvariantCulture), p.radius.ToString(CultureInfo.InvariantCulture), p.amount.ToString(CultureInfo.InvariantCulture), p.selectionMethod.ToString());
	}

	private void DecodeFieldValue(HaulersDreamSettings target, FieldInfo fi, string str)
	{
		try
		{
			fi.SetValue(target, ParseFieldValue(fi.FieldType, str));
		}
		catch (Exception ex)
		{
			HDLog.Warn("profile import: could not decode setting '" + fi.Name + "' from \"" + str + "\" — keeping default. " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private static object ParseFieldValue(Type t, string s)
	{
		if (t == typeof(bool))
		{
			return s == "1" || s == "true" || s == "True";
		}
		if (t == typeof(int))
		{
			return int.Parse(s, CultureInfo.InvariantCulture);
		}
		if (t == typeof(float))
		{
			return float.Parse(s, CultureInfo.InvariantCulture);
		}
		if (t.IsEnum)
		{
			return Enum.Parse(t, s);
		}
		if (t == typeof(List<string>))
		{
			if (s.Length != 0)
			{
				return new List<string>(from x in s.Split('\u001f', StringSplitOptions.None)
					where x.Length > 0
					select x);
			}
			return new List<string>();
		}
		if (t == typeof(StorageBuildingFilter))
		{
			return ParseFilter(s);
		}
		if (t == typeof(Dictionary<string, RouteDialogPrefs>))
		{
			return ParseRoutePrefsDict(s);
		}
		if (t == typeof(Dictionary<string, SowRouteTemplate>))
		{
			return ParseSowTemplateDict(s);
		}
		if (t == typeof(Dictionary<string, RemoveFloorRouteTemplate>))
		{
			return ParseRemoveFloorTemplateDict(s);
		}
		return Convert.ChangeType(s, t, CultureInfo.InvariantCulture);
	}

	private static HashSet<string> ParseIdSet(string s)
	{
		return new HashSet<string>(string.IsNullOrEmpty(s) ? Enumerable.Empty<string>() : (from x in s.Split('\u001f', StringSplitOptions.None)
			where x.Length > 0
			select x), StringComparer.OrdinalIgnoreCase);
	}

	private static StorageBuildingFilter ParseFilter(string s)
	{
		string[] array = s.Split('\u001e', StringSplitOptions.None);
		return new StorageBuildingFilter
		{
			denied = ParseIdSet((array.Length != 0) ? array[0] : ""),
			allowed = ParseIdSet((array.Length > 1) ? array[1] : "")
		};
	}

	private static Dictionary<string, RouteDialogPrefs> ParseRoutePrefsDict(string s)
	{
		Dictionary<string, RouteDialogPrefs> dictionary = new Dictionary<string, RouteDialogPrefs>();
		if (s.Length == 0)
		{
			return dictionary;
		}
		string[] array = s.Split('\u001e', StringSplitOptions.None);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split('\u001f', StringSplitOptions.None);
			if (array2.Length >= 10)
			{
				dictionary[array2[0]] = new RouteDialogPrefs
				{
					mode = (RouteMode)Enum.Parse(typeof(RouteMode), array2[1]),
					maxTravel = int.Parse(array2[2], CultureInfo.InvariantCulture),
					radius = int.Parse(array2[3], CultureInfo.InvariantCulture),
					amount = int.Parse(array2[4], CultureInfo.InvariantCulture),
					smart = (array2[5] == "1"),
					allowHarvest = (array2[6] == "1"),
					growthThreshold = int.Parse(array2[7], CultureInfo.InvariantCulture),
					selectionMethod = (RouteSelectionMethod)Enum.Parse(typeof(RouteSelectionMethod), array2[8]),
					distanceBasis = (RouteDistanceBasis)Enum.Parse(typeof(RouteDistanceBasis), array2[9])
				};
			}
		}
		return dictionary;
	}

	private static Dictionary<string, SowRouteTemplate> ParseSowTemplateDict(string s)
	{
		Dictionary<string, SowRouteTemplate> dictionary = new Dictionary<string, SowRouteTemplate>();
		if (s.Length == 0)
		{
			return dictionary;
		}
		string[] array = s.Split('\u001e', StringSplitOptions.None);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split('\u001f', StringSplitOptions.None);
			if (array2.Length >= 7)
			{
				dictionary[array2[0]] = new SowRouteTemplate
				{
					mode = (SowRouteMode)Enum.Parse(typeof(SowRouteMode), array2[1]),
					maxTravel = int.Parse(array2[2], CultureInfo.InvariantCulture),
					radius = int.Parse(array2[3], CultureInfo.InvariantCulture),
					amount = int.Parse(array2[4], CultureInfo.InvariantCulture),
					smart = (array2[5] == "1"),
					selectionMethod = (RouteSelectionMethod)Enum.Parse(typeof(RouteSelectionMethod), array2[6])
				};
			}
		}
		return dictionary;
	}

	private static Dictionary<string, RemoveFloorRouteTemplate> ParseRemoveFloorTemplateDict(string s)
	{
		Dictionary<string, RemoveFloorRouteTemplate> dictionary = new Dictionary<string, RemoveFloorRouteTemplate>();
		if (s.Length == 0)
		{
			return dictionary;
		}
		string[] array = s.Split('\u001e', StringSplitOptions.None);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split('\u001f', StringSplitOptions.None);
			if (array2.Length >= 6)
			{
				dictionary[array2[0]] = new RemoveFloorRouteTemplate
				{
					mode = (RemoveFloorRouteMode)Enum.Parse(typeof(RemoveFloorRouteMode), array2[1]),
					maxTravel = int.Parse(array2[2], CultureInfo.InvariantCulture),
					radius = int.Parse(array2[3], CultureInfo.InvariantCulture),
					amount = int.Parse(array2[4], CultureInfo.InvariantCulture),
					selectionMethod = (RouteSelectionMethod)Enum.Parse(typeof(RouteSelectionMethod), array2[5])
				};
			}
		}
		return dictionary;
	}
}
