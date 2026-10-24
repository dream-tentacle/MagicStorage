using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class Dialog_PlanRemoveFloorRoute : Window
{
	private readonly Pawn pawn;

	private readonly IntVec3 anchor;

	private readonly MapComponent_RoutePreview preview;

	private readonly string gerund;

	private RemoveFloorRouteMode mode;

	private int maxTravel = 100;

	private int radius = 8;

	private int amount;

	private readonly int amountMax;

	private RouteSelectionMethod selectionMethod;

	private const int MaxTravelMin = 20;

	private const int NoLimitStep = 500;

	private const int RadiusMax = 30;

	private readonly List<IntVec3> picked = new List<IntVec3>();

	private bool pickingMode;

	private bool rearmRequested;

	private Vector2 pickedScroll;

	private const float PickedRowH = 26f;

	private readonly List<RemoveFloorRouteMode> allowedModes = new List<RemoveFloorRouteMode>
	{
		RemoveFloorRouteMode.Area,
		RemoveFloorRouteMode.Chained,
		RemoveFloorRouteMode.Radius
	};

	private string selSig;

	private RemoveFloorRoutePlan cachedPlan;

	private Rect rememberBtnLocalRect;

	private bool rememberBtnHovered;

	public override Vector2 InitialSize => new Vector2(460f, 652f);

	public Dialog_PlanRemoveFloorRoute(Pawn pawn, IntVec3 anchor)
		: base((IWindowDrawing)null)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		this.pawn = pawn;
		this.anchor = anchor;
		gerund = TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.RemoveFloorGerund"));
		picked.Add(anchor);
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
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		amountMax = Mathf.Clamp(settings?.routeMaxAmount ?? 50, 5, 100);
		amount = amountMax + 1;
		selectionMethod = settings?.routeSelectionMethod ?? RouteSelectionMethod.MostStopsPerTravel;
		base.forcePause = false;
		base.doCloseX = true;
		base.closeOnClickedOutside = false;
		base.absorbInputAroundWindow = false;
		base.draggable = true;
		base.preventCameraMotion = false;
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected O, but got Unknown
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_028b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_030a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0314: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0358: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
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
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(((Rect)(ref inRect)).x, ((Rect)(ref inRect)).y, ((Rect)(ref inRect)).width, ((Rect)(ref inRect)).height - 36f - 30f - 6f - 8f);
		Listing_Standard val2 = new Listing_Standard();
		((Listing)val2).Begin(val);
		Text.Font = (GameFont)2;
		val2.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Title", NamedArgument.op_Implicit(gerund)), -1f, (string)null);
		Text.Font = (GameFont)1;
		((Listing)val2).GapLine(12f);
		for (int i = 0; i < allowedModes.Count; i++)
		{
			RemoveFloorRouteMode removeFloorRouteMode = allowedModes[i];
			if (val2.RadioButton(ModeLabel(removeFloorRouteMode), mode == removeFloorRouteMode, 0f, ModeDesc(removeFloorRouteMode), (float?)null))
			{
				mode = removeFloorRouteMode;
			}
		}
		((Listing)val2).Gap(6f);
		switch (mode)
		{
		case RemoveFloorRouteMode.Chained:
		{
			string text = ((maxTravel >= 500) ? ((object)Translator.Translate("HaulersDream.PlanRoute.MaxTravelNoLimit")/*cast due to constrained. prefix*/).ToString() : ((object)TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Cells", NamedArgument.op_Implicit(maxTravel))/*cast due to constrained. prefix*/).ToString());
			val2.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.MaxTravel", NamedArgument.op_Implicit(text)), -1f, (string)null);
			maxTravel = Mathf.RoundToInt(val2.Slider((float)maxTravel, 20f, 500f));
			break;
		}
		case RemoveFloorRouteMode.Radius:
			val2.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.RadiusLabel", NamedArgument.op_Implicit(radius)), -1f, (string)null);
			radius = Mathf.RoundToInt(val2.Slider((float)radius, 2f, 30f));
			DoAmountSlider(val2);
			break;
		}
		((Listing)val2).Gap(8f);
		DoPickerSection(val2);
		RefreshPlanIfNeeded();
		((Listing)val2).Gap(4f);
		val2.Label(EstimateText(), -1f, (TipSignal?)null);
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
		if (cachedPlan == null || cachedPlan.Empty)
		{
			return TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.EstimateNone"));
		}
		string text = RouteEstimate.HoursFromTicks(cachedPlan.totalTicks).ToString("0.0");
		return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Estimate", NamedArgument.op_Implicit(text), NamedArgument.op_Implicit(cachedPlan.cells.Count), NamedArgument.op_Implicit(Mathf.RoundToInt(cachedPlan.travelCells))));
	}

	private static string ModeLabel(RemoveFloorRouteMode m)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		return m switch
		{
			RemoveFloorRouteMode.Chained => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeChained")), 
			RemoveFloorRouteMode.Radius => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeRadius")), 
			_ => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeFloorArea")), 
		};
	}

	private static string ModeDesc(RemoveFloorRouteMode m)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		return m switch
		{
			RemoveFloorRouteMode.Chained => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeChainedDesc")), 
			RemoveFloorRouteMode.Radius => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeRadiusDesc")), 
			_ => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.ModeFloorAreaDesc")), 
		};
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
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		int num = picked.Count;
		for (int i = 0; i < picked.Count; i++)
		{
			num = num * 31 + picked[i].x * 4099 + picked[i].z;
		}
		string text = $"{(int)mode}|{EffectiveAmount()}|{radius}|{maxTravel}|pk{num}|sm{(int)selectionMethod}";
		if (!(text == selSig) || cachedPlan == null)
		{
			selSig = text;
			cachedPlan = RemoveFloorRoutePlanner.Plan(pawn, anchor, mode, EffectiveAmount(), radius, MaxDistance(), picked, selectionMethod, HaulersDreamMod.Settings?.routeOrderExactMax ?? 12);
			UpdatePreview();
		}
	}

	private void UpdatePreview()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		if (preview != null)
		{
			IntVec3? ringCenter = ((mode == RemoveFloorRouteMode.Radius) ? new IntVec3?(anchor) : ((IntVec3?)null));
			if (cachedPlan == null || cachedPlan.cells.Count == 0)
			{
				preview.SetPreview(pawn, null, ringCenter, radius);
			}
			else
			{
				preview.SetPreview(pawn, new List<IntVec3>(cachedPlan.cells), ringCenter, radius);
			}
		}
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
		if (mode != RemoveFloorRouteMode.Chained)
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
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		if (l.ButtonText(TaggedString.op_Implicit(pickingMode ? Translator.Translate("HaulersDream.PlanRoute.PickStop") : Translator.Translate("HaulersDream.PlanRoute.PickStart")), (string)null, 1f))
		{
			TogglePicking();
		}
		l.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.PickHeader", NamedArgument.op_Implicit(picked.Count)), -1f, (string)null);
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
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
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
		for (int i = 0; i < picked.Count; i++)
		{
			IntVec3 val3 = picked[i];
			((Rect)(ref val4))._002Ector(0f, num2, ((Rect)(ref val2)).width, 26f);
			if (i % 2 == 1)
			{
				Widgets.DrawLightHighlight(val4);
			}
			bool num4 = val3 == anchor;
			((Rect)(ref val5))._002Ector(((Rect)(ref val4)).xMax - 22f, num2 + 2f, 22f, 22f);
			if (!num4 && Widgets.ButtonText(val5, "✕", true, true, true, (TextAnchor?)null))
			{
				num3 = i;
			}
			((Rect)(ref val6))._002Ector(((Rect)(ref val4)).x + 4f, num2, ((Rect)(ref val5)).x - ((Rect)(ref val4)).x - 6f, 26f);
			TextAnchor val7 = Text.Anchor;
			Text.Anchor = (TextAnchor)3;
			Widgets.Label(val6, GenText.Truncate($"({val3.x}, {val3.z})", ((Rect)(ref val6)).width, (Dictionary<string, string>)null));
			Text.Anchor = val7;
			num2 += 26f;
		}
		Widgets.EndScrollView();
		if (num3 > 0)
		{
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
		}
		else
		{
			pickingMode = true;
			rearmRequested = true;
		}
	}

	private void BeginPicking()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected O, but got Unknown
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
				//IL_0053: Unknown result type (might be due to invalid IL or missing references)
				//IL_0058: Unknown result type (might be due to invalid IL or missing references)
				if (map != null)
				{
					IntVec3 cell = ((TargetInfo)(ref ti)).Cell;
					if (((IntVec3)(ref cell)).IsValid && GenGrid.InBounds(((TargetInfo)(ref ti)).Cell, map) && RemoveFloorRouteSelection.IsRemovableFloorCell(pawn, ((TargetInfo)(ref ti)).Cell))
					{
						return ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit(((TargetInfo)(ref ti)).Cell), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0);
					}
				}
				return false;
			}
		};
		Find.Targeter.BeginTargeting(val, (Action<LocalTargetInfo>)OnPicked, (Pawn)null, (Action)null, (Texture2D)null, true);
	}

	private void OnPicked(LocalTargetInfo target)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 cell = ((LocalTargetInfo)(ref target)).Cell;
		if (((IntVec3)(ref cell)).IsValid && !picked.Contains(cell) && RemoveFloorRouteSelection.IsRemovableFloorCell(pawn, cell))
		{
			picked.Add(cell);
		}
		rearmRequested = true;
	}

	private void Execute(bool replace)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		if (cachedPlan == null)
		{
			RefreshPlanIfNeeded();
		}
		RemoveFloorRouteExecutor.ExecuteRemoveFloorRouteSynced(pawn, anchor, mode, EffectiveAmount(), radius, MaxDistance(), replace, picked, selectionMethod, HaulersDreamMod.Settings?.routeOrderExactMax ?? 12);
	}

	private RemoveFloorRouteTemplate CurrentTemplate()
	{
		return new RemoveFloorRouteTemplate
		{
			mode = mode,
			maxTravel = ((maxTravel >= 500) ? (-1) : maxTravel),
			radius = radius,
			amount = ((amount > amountMax) ? (-1) : amount),
			selectionMethod = selectionMethod
		};
	}

	private void DoRememberButton(Rect row)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings s = HaulersDreamMod.Settings;
		Pawn obj = pawn;
		object obj2;
		if (obj == null)
		{
			obj2 = null;
		}
		else
		{
			Map map = ((Thing)obj).Map;
			if (map == null)
			{
				obj2 = null;
			}
			else
			{
				TerrainGrid terrainGrid = map.terrainGrid;
				obj2 = ((terrainGrid != null) ? terrainGrid.TerrainAt(anchor) : null);
			}
		}
		TerrainDef terrain = (TerrainDef)obj2;
		bool alreadyRemembered = ((terrain == null) ? null : s?.GetRememberedRemoveFloorRoute(((Def)terrain).defName))?.ValueEquals(CurrentTemplate()) ?? false;
		Patch_PlaySettings.DrawRememberButton(row, alreadyRemembered, delegate
		{
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			if (s != null && terrain != null)
			{
				s.SetRememberedRemoveFloorRoute(((Def)terrain).defName, CurrentTemplate());
				HaulersDreamMod instance = HaulersDreamMod.Instance;
				if (instance != null)
				{
					((Mod)instance).WriteSettings();
				}
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.RememberedConfirm", NamedArgument.op_Implicit(((Def)terrain).LabelCap))), MessageTypeDefOf.TaskCompletion, false);
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

	public static void ExecuteRemembered(Pawn pawn, IntVec3 anchor, RemoveFloorRouteTemplate template, bool replace)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		if (template != null && ((pawn != null) ? ((Thing)pawn).Map : null) != null && ((IntVec3)(ref anchor)).IsValid && GenGrid.InBounds(anchor, ((Thing)pawn).Map))
		{
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			int num = Mathf.Clamp(settings?.routeMaxAmount ?? 50, 5, 100);
			int num2 = ((template.amount < 1 || template.amount > num) ? int.MaxValue : template.amount);
			int num3 = ((template.maxTravel < 20) ? 500 : Mathf.Clamp(template.maxTravel, 20, 500));
			float maxDistance = ((template.mode != RemoveFloorRouteMode.Chained) ? float.PositiveInfinity : ((num3 >= 500) ? float.PositiveInfinity : ((float)num3)));
			int num4 = Mathf.Clamp(template.radius, 2, 30);
			RemoveFloorRouteExecutor.ExecuteRemoveFloorRouteSynced(pawn, anchor, template.mode, num2, num4, maxDistance, replace, new List<IntVec3> { anchor }, template.selectionMethod, settings?.routeOrderExactMax ?? 12);
		}
	}

	public override void PostClose()
	{
		((Window)this).PostClose();
		if (pickingMode && Find.Targeter.IsTargeting)
		{
			Find.Targeter.StopTargeting();
		}
		preview?.ClearPreview();
	}
}
