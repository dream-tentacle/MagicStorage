using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class Dialog_PlanRoute : Window
{
	private readonly Pawn pawn;

	private readonly Thing clicked;

	private readonly RouteWorkKind kind;

	private readonly MapComponent_RoutePreview preview;

	private RouteMode mode = RouteMode.Chained;

	private int maxTravel = 100;

	private int radius = 8;

	private int amount;

	private bool smart = true;

	private bool allowHarvest;

	private int growthThreshold;

	private readonly bool isHarvest;

	private readonly int amountMax;

	private readonly int harvestMinPct;

	private const int MaxTravelMin = 20;

	private const int NoLimitStep = 500;

	private const int RadiusMax = 30;

	public const string BuildTag = "F12t";

	private RouteLegs cachedLegs;

	private string legsSig;

	private int lastBudget = int.MinValue;

	private RoutePlan cachedPlan;

	private string pendingSelSig;

	private int pendingSelSigFrames;

	private const int SelSigDebounceFrames = 9;

	private readonly List<Thing> picked = new List<Thing>();

	private bool pickingMode;

	private bool rearmRequested;

	private Vector2 pickedScroll;

	private const float PickedRowH = 26f;

	private readonly List<ThingDef> secondaryDefs = new List<ThingDef>();

	private bool pickingSecondary;

	private Vector2 secondaryScroll;

	private readonly List<RouteMode> allowedModes;

	private readonly List<IntVec3> roomAnchors = new List<IntVec3>();

	private bool pickingRoom;

	private Vector2 roomsScroll;

	private Thing startNode;

	private Thing endNode;

	private string lastPinSig;

	private static readonly Color RoleActive = new Color(0.5f, 1f, 0.5f);

	private RouteSelectionMethod selectionMethod;

	private RouteDistanceBasis distanceBasis;

	private bool alsoBuild = true;

	private Rect rememberBtnLocalRect;

	private bool rememberBtnHovered;

	private bool SupportsMultiTarget
	{
		get
		{
			if (kind != null)
			{
				if (kind.scope != RouteTargetScope.SameDef)
				{
					return kind.scope == RouteTargetScope.SameDefOrDesignated;
				}
				return true;
			}
			return false;
		}
	}

	public override Vector2 InitialSize => new Vector2(460f, (isHarvest || allowedModes.Contains(RouteMode.Rooms)) ? 860f : 772f);

	private bool IsConstruction => kind?.scanner is WorkGiver_ConstructDeliverResourcesToBlueprints;

	public Dialog_PlanRoute(Pawn pawn, Thing clicked, RouteWorkKind kind)
		: base((IWindowDrawing)null)
	{
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		this.pawn = pawn;
		this.clicked = clicked;
		this.kind = kind;
		if (clicked != null)
		{
			picked.Add(clicked);
		}
		allowedModes = RouteSelection.AllowedModes(kind, clicked, (pawn != null) ? ((Thing)pawn).Map : null);
		if (clicked != null)
		{
			roomAnchors.Add(clicked.Position);
		}
		object obj;
		if (pawn == null)
		{
			obj = null;
		}
		else
		{
			Map map = ((Thing)pawn).Map;
			obj = ((map != null) ? map.GetComponent<MapComponent_RoutePreview>() : null);
		}
		preview = (MapComponent_RoutePreview)obj;
		isHarvest = kind?.designation == DesignationDefOf.HarvestPlant;
		harvestMinPct = (isHarvest ? Mathf.Clamp(Mathf.RoundToInt((clicked?.def?.plant?.harvestMinGrowth).GetValueOrDefault() * 100f), 0, 99) : 0);
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		amountMax = Mathf.Clamp(settings?.routeMaxAmount ?? 50, 5, 100);
		RouteDialogPrefs routeDialogPrefs = settings?.GetRoutePrefs(((Def)(clicked?.def?)).defName);
		if (routeDialogPrefs != null)
		{
			mode = routeDialogPrefs.mode;
			maxTravel = ((routeDialogPrefs.maxTravel < 20) ? 500 : Mathf.Clamp(routeDialogPrefs.maxTravel, 20, 500));
			radius = Mathf.Clamp(routeDialogPrefs.radius, 2, 30);
			amount = ((routeDialogPrefs.amount < 1 || routeDialogPrefs.amount > amountMax) ? (amountMax + 1) : routeDialogPrefs.amount);
			smart = routeDialogPrefs.smart;
			allowHarvest = routeDialogPrefs.allowHarvest;
			growthThreshold = Mathf.Clamp(routeDialogPrefs.growthThreshold, harvestMinPct, 100);
			selectionMethod = routeDialogPrefs.selectionMethod;
			distanceBasis = routeDialogPrefs.distanceBasis;
		}
		else
		{
			amount = amountMax + 1;
			allowHarvest = settings?.routeAllowHarvest ?? true;
			growthThreshold = Mathf.Clamp(settings?.routeGrowthThreshold ?? 80, harvestMinPct, 100);
			selectionMethod = settings?.routeSelectionMethod ?? RouteSelectionMethod.MostStopsPerTravel;
			distanceBasis = settings?.routeDistanceBasis ?? RouteDistanceBasis.StraightLine;
		}
		if (!allowedModes.Contains(mode))
		{
			mode = allowedModes[0];
		}
		base.forcePause = false;
		base.doCloseX = true;
		base.closeOnClickedOutside = false;
		base.absorbInputAroundWindow = false;
		base.draggable = true;
		base.preventCameraMotion = false;
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Expected O, but got Unknown
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_0273: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_031c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0331: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0417: Unknown result type (might be due to invalid IL or missing references)
		//IL_041c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0366: Unknown result type (might be due to invalid IL or missing references)
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		//IL_04aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_04af: Unknown result type (might be due to invalid IL or missing references)
		//IL_045c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0466: Expected O, but got Unknown
		//IL_0485: Unknown result type (might be due to invalid IL or missing references)
		//IL_048f: Expected O, but got Unknown
		//IL_048f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0499: Expected O, but got Unknown
		//IL_03c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f9: Expected O, but got Unknown
		//IL_0518: Unknown result type (might be due to invalid IL or missing references)
		//IL_0522: Expected O, but got Unknown
		//IL_0522: Unknown result type (might be due to invalid IL or missing references)
		//IL_052c: Expected O, but got Unknown
		//IL_057f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0610: Unknown result type (might be due to invalid IL or missing references)
		//IL_064a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0654: Unknown result type (might be due to invalid IL or missing references)
		//IL_068f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0699: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_06dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d3: Unknown result type (might be due to invalid IL or missing references)
		if (pickingMode && !Find.Targeter.IsTargeting)
		{
			if (rearmRequested)
			{
				rearmRequested = false;
				BeginPicking();
			}
			else
			{
				pickingMode = false;
			}
		}
		if (pickingRoom && !Find.Targeter.IsTargeting)
		{
			if (rearmRequested)
			{
				rearmRequested = false;
				BeginRoomPicking();
			}
			else
			{
				pickingRoom = false;
			}
		}
		if (pickingSecondary && !Find.Targeter.IsTargeting)
		{
			if (rearmRequested)
			{
				rearmRequested = false;
				BeginSecondaryPicking();
			}
			else
			{
				pickingSecondary = false;
			}
		}
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(((Rect)(ref inRect)).x, ((Rect)(ref inRect)).y, ((Rect)(ref inRect)).width, ((Rect)(ref inRect)).height - 36f - 30f - 6f - 8f);
		Listing_Standard val2 = new Listing_Standard();
		((Listing)val2).Begin(val);
		Text.Font = (GameFont)2;
		val2.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Title", NamedArgument.op_Implicit(kind.gerund)), -1f, (string)null);
		Text.Font = (GameFont)1;
		((Listing)val2).GapLine(12f);
		for (int i = 0; i < allowedModes.Count; i++)
		{
			RouteMode routeMode = allowedModes[i];
			if (val2.RadioButton(ModeLabel(routeMode), mode == routeMode, 0f, ModeDesc(routeMode), (float?)null))
			{
				if (mode != routeMode && pickingRoom)
				{
					StopRoomPicking();
				}
				mode = routeMode;
			}
		}
		((Listing)val2).Gap(6f);
		switch (mode)
		{
		case RouteMode.Chained:
		{
			string text = ((maxTravel >= 500) ? ((object)Translator.Translate("HaulersDream.PlanRoute.MaxTravelNoLimit")/*cast due to constrained. prefix*/).ToString() : ((object)TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Cells", NamedArgument.op_Implicit(maxTravel))/*cast due to constrained. prefix*/).ToString());
			val2.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.MaxTravel", NamedArgument.op_Implicit(text)), -1f, (string)null);
			maxTravel = Mathf.RoundToInt(val2.Slider((float)maxTravel, 20f, 500f));
			break;
		}
		case RouteMode.Radius:
			val2.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.RadiusLabel", NamedArgument.op_Implicit(radius)), -1f, (string)null);
			radius = Mathf.RoundToInt(val2.Slider((float)radius, 2f, 30f));
			DoAmountSlider(val2);
			break;
		case RouteMode.Vein:
			DoAmountSlider(val2);
			break;
		case RouteMode.Rooms:
			DoRoomsSection(val2);
			DoAmountSlider(val2);
			break;
		case RouteMode.Zone:
			DoAmountSlider(val2);
			break;
		}
		((Listing)val2).Gap(6f);
		val2.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.Smart")), ref smart, TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.SmartDesc")), 0f, 1f);
		if (IsConstruction)
		{
			((Listing)val2).Gap(6f);
			val2.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.AlsoBuild")), ref alsoBuild, TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.AlsoBuildDesc")), 0f, 1f);
		}
		if (isHarvest)
		{
			((Listing)val2).Gap(6f);
			val2.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.AllowHarvest")), ref allowHarvest, TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.AllowHarvestDesc")), 0f, 1f);
			if (allowHarvest)
			{
				growthThreshold = Mathf.Clamp(growthThreshold, harvestMinPct, 100);
				val2.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.GrowthThreshold", NamedArgument.op_Implicit(growthThreshold)), -1f, (string)null);
				growthThreshold = Mathf.RoundToInt(val2.Slider((float)growthThreshold, (float)harvestMinPct, 100f));
			}
		}
		((Listing)val2).Gap(8f);
		if (val2.ButtonText(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Selection", NamedArgument.op_Implicit(HaulersDreamSettings.SelectionMethodLabel(selectionMethod)))), (string)null, 1f))
		{
			Find.WindowStack.Add((Window)new FloatMenu(new List<FloatMenuOption>
			{
				new FloatMenuOption(HaulersDreamSettings.SelectionMethodLabel(RouteSelectionMethod.MostStopsPerTravel), (Action)delegate
				{
					selectionMethod = RouteSelectionMethod.MostStopsPerTravel;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
				new FloatMenuOption(HaulersDreamSettings.SelectionMethodLabel(RouteSelectionMethod.NearestToTarget), (Action)delegate
				{
					selectionMethod = RouteSelectionMethod.NearestToTarget;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			}));
		}
		if (val2.ButtonText(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.DistanceBasis", NamedArgument.op_Implicit(HaulersDreamSettings.DistanceBasisLabel(distanceBasis)))), (string)null, 1f))
		{
			Find.WindowStack.Add((Window)new FloatMenu(new List<FloatMenuOption>
			{
				new FloatMenuOption(HaulersDreamSettings.DistanceBasisLabel(RouteDistanceBasis.StraightLine), (Action)delegate
				{
					distanceBasis = RouteDistanceBasis.StraightLine;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
				new FloatMenuOption(HaulersDreamSettings.DistanceBasisLabel(RouteDistanceBasis.WalkingPath), (Action)delegate
				{
					distanceBasis = RouteDistanceBasis.WalkingPath;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			}));
		}
		if (SupportsMultiTarget)
		{
			((Listing)val2).Gap(8f);
			DoSecondarySection(val2);
		}
		((Listing)val2).Gap(8f);
		DoPickerSection(val2);
		RefreshPlanIfNeeded();
		((Listing)val2).Gap(4f);
		val2.Label(EstimateText(), -1f, (TipSignal?)null);
		if (mode == RouteMode.Vein && cachedPlan != null && cachedPlan.fogCaution)
		{
			GUI.color = new Color(1f, 0.85f, 0.4f);
			val2.Label(Translator.Translate("HaulersDream.PlanRoute.FogCaution"), -1f, (string)null);
			GUI.color = Color.white;
		}
		((Listing)val2).End();
		DoRememberButton(new Rect(((Rect)(ref inRect)).x, ((Rect)(ref inRect)).yMax - 36f - 6f - 30f, ((Rect)(ref inRect)).width, 30f));
		float num = (((Rect)(ref inRect)).width - 16f) / 3f;
		float num2 = ((Rect)(ref inRect)).yMax - 36f;
		if (Widgets.ButtonText(new Rect(((Rect)(ref inRect)).x, num2, num, 36f), TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.Cancel")), true, true, true, (TextAnchor?)null))
		{
			((Window)this).Close(true);
		}
		bool flag = Widgets.ButtonText(new Rect(((Rect)(ref inRect)).x + num + 8f, num2, num, 36f), TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.Append")), true, true, true, (TextAnchor?)null);
		bool num3 = Widgets.ButtonText(new Rect(((Rect)(ref inRect)).x + 2f * (num + 8f), num2, num, 36f), TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.Replace")), true, true, true, (TextAnchor?)null);
		if (flag)
		{
			Execute(replace: false);
			((Window)this).Close(true);
		}
		if (num3)
		{
			Execute(replace: true);
			((Window)this).Close(true);
		}
	}

	private string EstimateText()
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		if (cachedPlan == null || cachedPlan.Empty)
		{
			return TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.EstimateNone"));
		}
		string text = RouteEstimate.HoursFromTicks(cachedPlan.totalTicks).ToString("0.0");
		string text2 = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Estimate", NamedArgument.op_Implicit(text), NamedArgument.op_Implicit(cachedPlan.stops.Count), NamedArgument.op_Implicit(Mathf.RoundToInt(cachedPlan.travelCells))));
		if (cachedPlan.cappedByReach)
		{
			text2 = TaggedString.op_Implicit(text2 + (" " + Translator.Translate("HaulersDream.PlanRoute.EstimateUnreachable")));
		}
		return text2;
	}

	private static string ModeLabel(RouteMode m)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		return m switch
		{
			RouteMode.Chained => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeChained")), 
			RouteMode.Vein => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeVein")), 
			RouteMode.Rooms => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeRooms")), 
			RouteMode.Zone => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeZone")), 
			_ => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeRadius")), 
		};
	}

	private static string ModeDesc(RouteMode m)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		return m switch
		{
			RouteMode.Chained => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeChainedDesc")), 
			RouteMode.Vein => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeVeinDesc")), 
			RouteMode.Rooms => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeRoomsDesc")), 
			RouteMode.Zone => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeZoneDesc")), 
			_ => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeRadiusDesc")), 
		};
	}

	private void DoRoomsSection(Listing_Standard l)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		if (l.ButtonText(TaggedString.op_Implicit(pickingRoom ? Translator.Translate("HaulersDream.PlanRoute.PickRoomsStop") : Translator.Translate("HaulersDream.PlanRoute.PickRooms")), (string)null, 1f))
		{
			ToggleRoomPicking();
		}
		l.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.RoomsHeader", NamedArgument.op_Implicit(roomAnchors.Count)), -1f, (string)null);
		DrawRoomsList(((Listing)l).GetRect(78f, 1f));
		((Listing)l).Gap(4f);
	}

	private void DrawRoomsList(Rect outRect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		Widgets.DrawMenuSection(outRect);
		Rect val = GenUI.ContractedBy(outRect, 2f);
		float num = (float)roomAnchors.Count * 26f;
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(0f, 0f, ((Rect)(ref val)).width - 16f, Mathf.Max(num, ((Rect)(ref val)).height));
		Widgets.BeginScrollView(val, ref roomsScroll, val2, true);
		float num2 = 0f;
		int num3 = -1;
		Pawn obj = pawn;
		Map val3 = ((obj != null) ? ((Thing)obj).Map : null);
		Rect val5 = default(Rect);
		Rect val6 = default(Rect);
		Rect val8 = default(Rect);
		for (int i = 0; i < roomAnchors.Count; i++)
		{
			IntVec3 val4 = roomAnchors[i];
			((Rect)(ref val5))._002Ector(0f, num2, ((Rect)(ref val2)).width, 26f);
			if (i % 2 == 1)
			{
				Widgets.DrawLightHighlight(val5);
			}
			((Rect)(ref val6))._002Ector(((Rect)(ref val5)).xMax - 22f, num2 + 2f, 22f, 22f);
			if (i > 0 && Widgets.ButtonText(val6, "✕", true, true, true, (TextAnchor?)null))
			{
				num3 = i;
			}
			Room val7 = ((val3 != null && GenGrid.InBounds(val4, val3)) ? GridsUtility.GetRoom(val4, val3) : null);
			string arg = ((val7 == null) ? ((object)Translator.Translate("HaulersDream.PlanRoute.RoomGone")/*cast due to constrained. prefix*/).ToString() : GenText.CapitalizeFirst(val7.GetRoomRoleLabel()));
			((Rect)(ref val8))._002Ector(((Rect)(ref val5)).x + 4f, num2, ((Rect)(ref val6)).x - ((Rect)(ref val5)).x - 6f, 26f);
			TextAnchor anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)3;
			Widgets.Label(val8, GenText.Truncate($"{arg} ({val4.x}, {val4.z})", ((Rect)(ref val8)).width, (Dictionary<string, string>)null));
			Text.Anchor = anchor;
			num2 += 26f;
		}
		Widgets.EndScrollView();
		if (num3 > 0)
		{
			roomAnchors.RemoveAt(num3);
		}
	}

	private void ToggleRoomPicking()
	{
		if (pickingRoom)
		{
			StopRoomPicking();
			return;
		}
		if (pickingMode)
		{
			TogglePicking();
		}
		if (pickingSecondary)
		{
			StopSecondaryPicking();
		}
		pickingRoom = true;
		rearmRequested = true;
	}

	private void StopRoomPicking()
	{
		pickingRoom = false;
		rearmRequested = false;
		if (Find.Targeter.IsTargeting)
		{
			Find.Targeter.StopTargeting();
		}
	}

	private void BeginRoomPicking()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Expected O, but got Unknown
		Pawn obj = pawn;
		Map map = ((obj != null) ? ((Thing)obj).Map : null);
		TargetingParameters val = new TargetingParameters
		{
			canTargetPawns = false,
			canTargetBuildings = false,
			canTargetItems = false,
			canTargetLocations = true,
			validator = delegate(TargetInfo ti)
			{
				//IL_000a: Unknown result type (might be due to invalid IL or missing references)
				//IL_000f: Unknown result type (might be due to invalid IL or missing references)
				//IL_001b: Unknown result type (might be due to invalid IL or missing references)
				//IL_003a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0048: Unknown result type (might be due to invalid IL or missing references)
				if (map != null)
				{
					IntVec3 cell = ((TargetInfo)(ref ti)).Cell;
					if (((IntVec3)(ref cell)).IsValid && GenGrid.InBounds(((TargetInfo)(ref ti)).Cell, map) && !map.fogGrid.IsFogged(((TargetInfo)(ref ti)).Cell))
					{
						return GridsUtility.GetRoom(((TargetInfo)(ref ti)).Cell, map) != null;
					}
				}
				return false;
			}
		};
		Find.Targeter.BeginTargeting(val, (Action<LocalTargetInfo>)OnRoomPicked, (Pawn)null, (Action)null, (Texture2D)null, true);
	}

	private void OnRoomPicked(LocalTargetInfo target)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		rearmRequested = true;
		Pawn obj = pawn;
		Map val = ((obj != null) ? ((Thing)obj).Map : null);
		if (val == null)
		{
			return;
		}
		IntVec3 cell = ((LocalTargetInfo)(ref target)).Cell;
		if (!((IntVec3)(ref cell)).IsValid || !GenGrid.InBounds(((LocalTargetInfo)(ref target)).Cell, val))
		{
			return;
		}
		Room room = GridsUtility.GetRoom(((LocalTargetInfo)(ref target)).Cell, val);
		if (room == null)
		{
			return;
		}
		for (int i = 0; i < roomAnchors.Count; i++)
		{
			Room val2 = (GenGrid.InBounds(roomAnchors[i], val) ? GridsUtility.GetRoom(roomAnchors[i], val) : null);
			if (val2 != null && val2.ID == room.ID)
			{
				return;
			}
		}
		roomAnchors.Add(((LocalTargetInfo)(ref target)).Cell);
	}

	private void DoAmountSlider(Listing_Standard l)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		string text = ((amount > amountMax) ? ((object)Translator.Translate("HaulersDream.PlanRoute.VeinAll")/*cast due to constrained. prefix*/).ToString() : amount.ToString());
		l.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Amount", NamedArgument.op_Implicit(text)), -1f, (string)null);
		amount = Mathf.RoundToInt(l.Slider((float)amount, 1f, (float)(amountMax + 1)));
	}

	private void RefreshPlanIfNeeded()
	{
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		int num = picked.Count;
		for (int i = 0; i < picked.Count; i++)
		{
			num = num * 31 + (picked[i]?.thingIDNumber ?? 0);
		}
		int num2 = roomAnchors.Count;
		for (int j = 0; j < roomAnchors.Count; j++)
		{
			num2 = num2 * 31 + roomAnchors[j].x * 4099 + roomAnchors[j].z;
		}
		int num3 = secondaryDefs.Count;
		for (int k = 0; k < secondaryDefs.Count; k++)
		{
			num3 = num3 * 31 + (((Def)(secondaryDefs[k]?)).shortHash ?? 0);
		}
		string text = $"{(int)mode}|{EffectiveAmount()}|{radius}|{(smart ? 1 : 0)}|{(allowHarvest ? 1 : 0)}|{growthThreshold}|pk{num}|rm{num2}|sd{num3}|sm{(int)selectionMethod}|db{(int)distanceBasis}";
		bool flag = false;
		if (text != legsSig)
		{
			if (text == pendingSelSig)
			{
				pendingSelSigFrames++;
			}
			else
			{
				pendingSelSig = text;
				pendingSelSigFrames = 0;
			}
			if (pendingSelSigFrames >= 9 || cachedLegs == null)
			{
				legsSig = text;
				cachedLegs = RoutePlanner.ComputeLegs(pawn, clicked, kind, mode, EffectiveAmount(), radius, smart, allowHarvest, growthThreshold, picked, selectionMethod, distanceBasis, roomAnchors, secondaryDefs);
				flag = true;
			}
		}
		int num4 = ((mode == RouteMode.Chained) ? maxTravel : int.MaxValue);
		string text2 = $"{startNode?.thingIDNumber ?? (-1)}|{endNode?.thingIDNumber ?? (-1)}";
		if (flag || num4 != lastBudget || text2 != lastPinSig || cachedPlan == null)
		{
			lastBudget = num4;
			lastPinSig = text2;
			cachedPlan = RoutePlanner.Truncate(cachedLegs, MaxDistance(), startNode, endNode, HaulersDreamMod.Settings?.routeOrderExactMax ?? 12);
			UpdatePreview();
			HDLog.Dbg(string.Format("PlanRoute[{0}] {1}: mode={2} selected={3} ", "F12t", kind?.gerund, mode, cachedPlan.selectedCount) + $"reachable={cachedLegs?.Reachable ?? 0} kept={cachedPlan.stops.Count} budget={MaxDistance():0} " + $"cappedDistance={cachedPlan.cappedByDistance} cappedReach={cachedPlan.cappedByReach}");
		}
	}

	private void UpdatePreview()
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		if (preview == null)
		{
			return;
		}
		IntVec3? ringCenter = ((mode == RouteMode.Radius) ? new IntVec3?(clicked.Position) : ((IntVec3?)null));
		if (cachedPlan == null || cachedPlan.stops.Count == 0)
		{
			preview.SetPreview(pawn, null, ringCenter, radius);
			return;
		}
		List<IntVec3> list = new List<IntVec3>(cachedPlan.stops.Count);
		for (int i = 0; i < cachedPlan.stops.Count; i++)
		{
			if (cachedPlan.stops[i] != null)
			{
				list.Add(cachedPlan.stops[i].Position);
			}
		}
		preview.SetPreview(pawn, list, ringCenter, radius, cachedPlan.storageCell);
	}

	private int EffectiveAmount()
	{
		if (amount <= amountMax)
		{
			return amount;
		}
		return int.MaxValue;
	}

	private float MaxDistance()
	{
		if (mode != RouteMode.Chained)
		{
			return float.PositiveInfinity;
		}
		if (maxTravel < 500)
		{
			return maxTravel;
		}
		return float.PositiveInfinity;
	}

	private void DoPickerSection(Listing_Standard l)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		if (l.ButtonText(TaggedString.op_Implicit(pickingMode ? Translator.Translate("HaulersDream.PlanRoute.PickStop") : Translator.Translate("HaulersDream.PlanRoute.PickStart")), (string)null, 1f))
		{
			TogglePicking();
		}
		l.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.PickHeader", NamedArgument.op_Implicit(picked.Count)), -1f, (string)null);
		GUI.color = new Color(0.7f, 0.7f, 0.7f);
		Text.Font = (GameFont)0;
		l.Label(Translator.Translate("HaulersDream.PlanRoute.PickRoleHint"), -1f, (string)null);
		Text.Font = (GameFont)1;
		GUI.color = Color.white;
		DrawPickedList(((Listing)l).GetRect(78f, 1f));
	}

	private void DrawPickedList(Rect outRect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0261: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0308: Unknown result type (might be due to invalid IL or missing references)
		//IL_031c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_0343: Unknown result type (might be due to invalid IL or missing references)
		Widgets.DrawMenuSection(outRect);
		Rect val = GenUI.ContractedBy(outRect, 2f);
		float num = (float)picked.Count * 26f;
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(0f, 0f, ((Rect)(ref val)).width - 16f, Mathf.Max(num, ((Rect)(ref val)).height));
		Widgets.BeginScrollView(val, ref pickedScroll, val2, true);
		float num2 = 0f;
		int num3 = -1;
		Rect val4 = default(Rect);
		Rect val5 = default(Rect);
		Rect val6 = default(Rect);
		Rect val7 = default(Rect);
		Rect val8 = default(Rect);
		for (int i = 0; i < picked.Count; i++)
		{
			Thing val3 = picked[i];
			((Rect)(ref val4))._002Ector(0f, num2, ((Rect)(ref val2)).width, 26f);
			if (i % 2 == 1)
			{
				Widgets.DrawLightHighlight(val4);
			}
			bool num4 = val3 == clicked;
			bool flag = val3 != null && val3 == startNode;
			bool flag2 = val3 != null && val3 == endNode;
			((Rect)(ref val5))._002Ector(((Rect)(ref val4)).xMax - 22f, num2 + 2f, 22f, 22f);
			if (!num4 && Widgets.ButtonText(val5, "✕", true, true, true, (TextAnchor?)null))
			{
				num3 = i;
			}
			((Rect)(ref val6))._002Ector(((Rect)(ref val5)).x - 22f - 2f, num2 + 2f, 22f, 22f);
			((Rect)(ref val7))._002Ector(((Rect)(ref val6)).x - 22f - 2f, num2 + 2f, 22f, 22f);
			GUI.color = (flag ? RoleActive : Color.white);
			if (Widgets.ButtonText(val7, TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.PickStartBtn")), true, true, true, (TextAnchor?)null))
			{
				startNode = (flag ? null : val3);
				if (startNode != null && endNode == val3)
				{
					endNode = null;
				}
			}
			GUI.color = (flag2 ? RoleActive : Color.white);
			if (Widgets.ButtonText(val6, TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.PickEndBtn")), true, true, true, (TextAnchor?)null))
			{
				endNode = (flag2 ? null : val3);
				if (endNode != null && startNode == val3)
				{
					startNode = null;
				}
			}
			GUI.color = Color.white;
			TooltipHandler.TipRegion(val7, TipSignal.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.PickStartTip")));
			TooltipHandler.TipRegion(val6, TipSignal.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.PickEndTip")));
			((Rect)(ref val8))._002Ector(((Rect)(ref val4)).x + 4f, num2, ((Rect)(ref val7)).x - ((Rect)(ref val4)).x - 6f, 26f);
			string text = TaggedString.op_Implicit(flag ? ("  " + Translator.Translate("HaulersDream.PlanRoute.PickStartBadge")) : (flag2 ? ("  " + Translator.Translate("HaulersDream.PlanRoute.PickEndBadge")) : TaggedString.op_Implicit("")));
			string text2 = ((val3 != null) ? $"{((Entity)val3).LabelCap.ToString()} ({val3.Position.x}, {val3.Position.z}){text}" : "?");
			TextAnchor anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)3;
			Widgets.Label(val8, GenText.Truncate(text2, ((Rect)(ref val8)).width, (Dictionary<string, string>)null));
			Text.Anchor = anchor;
			num2 += 26f;
		}
		Widgets.EndScrollView();
		if (num3 >= 0)
		{
			Thing obj = picked[num3];
			if (obj == startNode)
			{
				startNode = null;
			}
			if (obj == endNode)
			{
				endNode = null;
			}
			picked.RemoveAt(num3);
		}
	}

	private void TogglePicking()
	{
		if (pickingMode)
		{
			pickingMode = false;
			rearmRequested = false;
			if (Find.Targeter.IsTargeting)
			{
				Find.Targeter.StopTargeting();
			}
			return;
		}
		if (pickingRoom)
		{
			StopRoomPicking();
		}
		if (pickingSecondary)
		{
			StopSecondaryPicking();
		}
		pickingMode = true;
		rearmRequested = true;
	}

	private void BeginPicking()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		TargetingParameters val = new TargetingParameters
		{
			canTargetPawns = false,
			canTargetBuildings = true,
			canTargetPlants = true,
			canTargetItems = true,
			canTargetLocations = false,
			mapObjectTargetsMustBeAutoAttackable = false,
			validator = (TargetInfo ti) => ((TargetInfo)(ref ti)).Thing != null && RouteSelection.IsValidRouteTarget(pawn, clicked, kind, ((TargetInfo)(ref ti)).Thing, secondaryDefs) && ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit(((TargetInfo)(ref ti)).Thing), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0)
		};
		Find.Targeter.BeginTargeting(val, (Action<LocalTargetInfo>)OnPicked, (Pawn)null, (Action)null, (Texture2D)null, true);
	}

	private void OnPicked(LocalTargetInfo target)
	{
		Thing thing = ((LocalTargetInfo)(ref target)).Thing;
		if (thing != null && !picked.Contains(thing) && RouteSelection.IsValidRouteTarget(pawn, clicked, kind, thing, secondaryDefs))
		{
			picked.Add(thing);
		}
		rearmRequested = true;
	}

	private void DoSecondarySection(Listing_Standard l)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		if (l.ButtonText(TaggedString.op_Implicit(pickingSecondary ? Translator.Translate("HaulersDream.PlanRoute.AddTargetStop") : Translator.Translate("HaulersDream.PlanRoute.AddTarget")), (string)null, 1f))
		{
			ToggleSecondaryPicking();
		}
		l.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.TargetTypesHeader", NamedArgument.op_Implicit(secondaryDefs.Count + 1)), -1f, (string)null);
		GUI.color = new Color(0.7f, 0.7f, 0.7f);
		Text.Font = (GameFont)0;
		l.Label(Translator.Translate("HaulersDream.PlanRoute.AddTargetHint"), -1f, (string)null);
		Text.Font = (GameFont)1;
		GUI.color = Color.white;
		DrawSecondaryList(((Listing)l).GetRect(78f, 1f));
	}

	private void DrawSecondaryList(Rect outRect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		Widgets.DrawMenuSection(outRect);
		Rect val = GenUI.ContractedBy(outRect, 2f);
		int num = secondaryDefs.Count + 1;
		float num2 = (float)num * 26f;
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(0f, 0f, ((Rect)(ref val)).width - 16f, Mathf.Max(num2, ((Rect)(ref val)).height));
		Widgets.BeginScrollView(val, ref secondaryScroll, val2, true);
		float num3 = 0f;
		int num4 = -1;
		Rect val3 = default(Rect);
		Rect val4 = default(Rect);
		Rect val5 = default(Rect);
		for (int i = 0; i < num; i++)
		{
			ThingDef obj = ((i != 0) ? secondaryDefs[i - 1] : clicked?.def);
			((Rect)(ref val3))._002Ector(0f, num3, ((Rect)(ref val2)).width, 26f);
			if (i % 2 == 1)
			{
				Widgets.DrawLightHighlight(val3);
			}
			((Rect)(ref val4))._002Ector(((Rect)(ref val3)).xMax - 22f, num3 + 2f, 22f, 22f);
			if (i > 0 && Widgets.ButtonText(val4, "✕", true, true, true, (TextAnchor?)null))
			{
				num4 = i - 1;
			}
			string text = ((obj != null) ? ((object)((Def)obj).LabelCap/*cast due to constrained. prefix*/).ToString() : null) ?? "?";
			if (i == 0)
			{
				text = TaggedString.op_Implicit(text + ("  " + Translator.Translate("HaulersDream.PlanRoute.PrimaryBadge")));
			}
			((Rect)(ref val5))._002Ector(((Rect)(ref val3)).x + 4f, num3, ((Rect)(ref val4)).x - ((Rect)(ref val3)).x - 6f, 26f);
			TextAnchor anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)3;
			Widgets.Label(val5, GenText.Truncate(text, ((Rect)(ref val5)).width, (Dictionary<string, string>)null));
			Text.Anchor = anchor;
			num3 += 26f;
		}
		Widgets.EndScrollView();
		if (num4 >= 0)
		{
			secondaryDefs.RemoveAt(num4);
		}
	}

	private void ToggleSecondaryPicking()
	{
		if (pickingSecondary)
		{
			StopSecondaryPicking();
			return;
		}
		if (pickingMode)
		{
			TogglePicking();
		}
		if (pickingRoom)
		{
			StopRoomPicking();
		}
		pickingSecondary = true;
		rearmRequested = true;
	}

	private void StopSecondaryPicking()
	{
		pickingSecondary = false;
		rearmRequested = false;
		if (Find.Targeter.IsTargeting)
		{
			Find.Targeter.StopTargeting();
		}
	}

	private void BeginSecondaryPicking()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		TargetingParameters val = new TargetingParameters
		{
			canTargetPawns = false,
			canTargetBuildings = true,
			canTargetPlants = true,
			canTargetItems = true,
			canTargetLocations = false,
			mapObjectTargetsMustBeAutoAttackable = false,
			validator = (TargetInfo ti) => ((TargetInfo)(ref ti)).Thing != null && IsSecondaryPick(((TargetInfo)(ref ti)).Thing)
		};
		Find.Targeter.BeginTargeting(val, (Action<LocalTargetInfo>)OnSecondaryPicked, (Pawn)null, (Action)null, (Texture2D)null, true);
	}

	private bool IsSecondaryPick(Thing t)
	{
		if (t?.def == null || t.def == clicked?.def || secondaryDefs.Contains(t.def))
		{
			return false;
		}
		RouteWorkKind routeWorkKind = WorkKindResolver.Resolve(pawn, t);
		if (routeWorkKind != null && kind != null && routeWorkKind.designation == kind.designation)
		{
			return routeWorkKind.scope == kind.scope;
		}
		return false;
	}

	private void OnSecondaryPicked(LocalTargetInfo target)
	{
		Thing thing = ((LocalTargetInfo)(ref target)).Thing;
		if (thing?.def != null && IsSecondaryPick(thing))
		{
			secondaryDefs.Add(thing.def);
		}
		rearmRequested = true;
	}

	public static bool ExecuteRemembered(Pawn pawn, Thing clicked, RouteWorkKind kind, bool replace)
	{
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		if (((pawn != null) ? ((Thing)pawn).Map : null) == null || clicked?.def == null || kind == null)
		{
			return false;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		RouteDialogPrefs routeDialogPrefs = settings?.GetRememberedRoute(((Def)clicked.def).defName);
		if (routeDialogPrefs == null)
		{
			return false;
		}
		List<RouteMode> list = RouteSelection.AllowedModes(kind, clicked, ((Thing)pawn).Map);
		RouteMode routeMode = (list.Contains(routeDialogPrefs.mode) ? routeDialogPrefs.mode : list[0]);
		int num = Mathf.Clamp(settings.routeMaxAmount, 5, 100);
		int num2 = ((routeDialogPrefs.amount < 1 || routeDialogPrefs.amount > num) ? int.MaxValue : routeDialogPrefs.amount);
		int num3 = ((routeDialogPrefs.maxTravel < 20) ? 500 : Mathf.Clamp(routeDialogPrefs.maxTravel, 20, 500));
		float maxDistance = ((routeMode != RouteMode.Chained) ? float.PositiveInfinity : ((num3 >= 500) ? float.PositiveInfinity : ((float)num3)));
		int num4 = Mathf.Clamp(routeDialogPrefs.radius, 2, 30);
		int num5 = ((kind.designation == DesignationDefOf.HarvestPlant) ? Mathf.Clamp(Mathf.RoundToInt((clicked.def?.plant?.harvestMinGrowth).GetValueOrDefault() * 100f), 0, 99) : 0);
		int num6 = Mathf.Clamp(routeDialogPrefs.growthThreshold, num5, 100);
		List<Thing> mustInclude = new List<Thing> { clicked };
		List<IntVec3> list2 = new List<IntVec3> { clicked.Position };
		bool flag = kind.scanner is WorkGiver_ConstructDeliverResourcesToBlueprints;
		RouteExecutor.ExecuteRouteSynced(pawn, clicked, WorkKindResolver.WorkKindId(kind), routeMode, num2, num4, maxDistance, routeDialogPrefs.smart, routeDialogPrefs.allowHarvest, num6, replace, mustInclude, routeDialogPrefs.selectionMethod, routeDialogPrefs.distanceBasis, settings.routeOrderExactMax, null, null, flag, list2, null);
		return true;
	}

	private void Execute(bool replace)
	{
		if (cachedPlan == null)
		{
			RefreshPlanIfNeeded();
		}
		RouteExecutor.ExecuteRouteSynced(pawn, clicked, WorkKindResolver.WorkKindId(kind), mode, EffectiveAmount(), radius, MaxDistance(), smart, allowHarvest, growthThreshold, replace, picked, selectionMethod, distanceBasis, HaulersDreamMod.Settings?.routeOrderExactMax ?? 12, startNode, endNode, IsConstruction && alsoBuild, roomAnchors, secondaryDefs);
	}

	private void DoRememberButton(Rect row)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings s = HaulersDreamMod.Settings;
		bool alreadyRemembered = (s?.GetRememberedRoute(((Def)(clicked?.def?)).defName))?.ValueEquals(CurrentPrefs()) ?? false;
		Patch_PlaySettings.DrawRememberButton(row, alreadyRemembered, delegate
		{
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			if (s != null && clicked?.def != null)
			{
				s.SetRememberedRoute(((Def)clicked.def).defName, CurrentPrefs());
				HaulersDreamMod instance = HaulersDreamMod.Instance;
				if (instance != null)
				{
					((Mod)instance).WriteSettings();
				}
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.RememberedConfirm", NamedArgument.op_Implicit(((Def)clicked.def).LabelCap))), MessageTypeDefOf.TaskCompletion, false);
			}
		});
		rememberBtnLocalRect = row;
		rememberBtnHovered = Mouse.IsOver(row);
	}

	public override void ExtraOnGUI()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		((Window)this).ExtraOnGUI();
		if (rememberBtnHovered)
		{
			Patch_PlaySettings.DrawRememberArrow(base.windowRect, rememberBtnLocalRect);
		}
	}

	private RouteDialogPrefs CurrentPrefs()
	{
		return new RouteDialogPrefs
		{
			mode = mode,
			maxTravel = ((maxTravel >= 500) ? (-1) : maxTravel),
			radius = radius,
			amount = ((amount > amountMax) ? (-1) : amount),
			smart = smart,
			allowHarvest = allowHarvest,
			growthThreshold = growthThreshold,
			selectionMethod = selectionMethod,
			distanceBasis = distanceBasis
		};
	}

	public override void PostClose()
	{
		((Window)this).PostClose();
		if ((pickingMode || pickingRoom || pickingSecondary) && Find.Targeter.IsTargeting)
		{
			Find.Targeter.StopTargeting();
		}
		preview?.ClearPreview();
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && clicked?.def != null)
		{
			settings.SetRoutePrefs(((Def)clicked.def).defName, CurrentPrefs());
			HaulersDreamMod instance = HaulersDreamMod.Instance;
			if (instance != null)
			{
				((Mod)instance).WriteSettings();
			}
		}
	}
}
