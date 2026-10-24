using System.Collections.Generic;
using System.Text;
using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulersDream;

public class Dialog_PlanCraft : Window
{
	private readonly Pawn pawn;

	private readonly Building_WorkTable bench;

	private readonly List<Bill> bills = new List<Bill>();

	private Bill selected;

	private int reps = 4;

	private float timeoutHours;

	private int maxAvailReps = 1;

	private string repsBuf;

	private string timeoutBuf;

	private CraftBatchPlan cachedPlan;

	private string planSig;

	private Vector2 billScroll;

	public const string BuildTag = "F-Craft1";

	private const float RowH = 30f;

	private const int TicksPerHour = 2500;

	private const int RepsBoxMin = 1;

	private const int RepsBoxMax = 500;

	private const float TimeoutMin = 0f;

	private const float TimeoutMax = 8f;

	private const float TimeoutStep = 0.5f;

	private const string TimeoutFormat = "0.#";

	public override Vector2 InitialSize => new Vector2(480f, 560f);

	public Dialog_PlanCraft(Pawn pawn, Building_WorkTable bench)
		: base((IWindowDrawing)null)
	{
		this.pawn = pawn;
		this.bench = bench;
		object obj;
		if (bench == null)
		{
			obj = null;
		}
		else
		{
			BillStack billStack = bench.BillStack;
			obj = ((billStack != null) ? billStack.Bills : null);
		}
		List<Bill> list = (List<Bill>)obj;
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (CraftBatchPlanner.CanPawnBatch(pawn, list[i]))
				{
					bills.Add(list[i]);
				}
			}
		}
		timeoutHours = Mathf.Clamp(HaulersDreamMod.Settings?.craftBatchTimeoutHours ?? 2f, 0f, 8f);
		if (bills.Count > 0)
		{
			SelectBill(bills[0]);
		}
		base.forcePause = false;
		base.doCloseX = true;
		base.closeOnClickedOutside = true;
		base.absorbInputAroundWindow = true;
		base.draggable = true;
		base.closeOnAccept = false;
	}

	private void SelectBill(Bill bill)
	{
		selected = bill;
		maxAvailReps = Mathf.Clamp(CraftBatchPlanner.MaxAvailableReps(pawn, bench, bill), 1, 500);
		reps = Mathf.Clamp(reps, 1, maxAvailReps);
		repsBuf = null;
		planSig = null;
	}

	private void RefreshPlan()
	{
		Bill obj = selected;
		string text = $"{((obj != null) ? obj.GetUniqueLoadID() : null)}|{reps}|{timeoutHours:0.0}";
		if (!(text == planSig) || cachedPlan == null)
		{
			planSig = text;
			cachedPlan = ((selected == null) ? null : CraftBatchPlanner.Resolve(pawn, bench, selected, reps, Mathf.RoundToInt(timeoutHours * 2500f)));
		}
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Invalid comparison between Unknown and I4
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Invalid comparison between Unknown and I4
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Invalid comparison between Unknown and I4
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Expected O, but got Unknown
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		//IL_029f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0308: Unknown result type (might be due to invalid IL or missing references)
		//IL_032c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0331: Unknown result type (might be due to invalid IL or missing references)
		//IL_0375: Unknown result type (might be due to invalid IL or missing references)
		//IL_0364: Unknown result type (might be due to invalid IL or missing references)
		//IL_0369: Unknown result type (might be due to invalid IL or missing references)
		//IL_0380: Unknown result type (might be due to invalid IL or missing references)
		//IL_0392: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_040a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0449: Unknown result type (might be due to invalid IL or missing references)
		//IL_0461: Unknown result type (might be due to invalid IL or missing references)
		//IL_0466: Unknown result type (might be due to invalid IL or missing references)
		//IL_0491: Unknown result type (might be due to invalid IL or missing references)
		bool enterPressed = (int)Event.current.type == 4 && ((int)Event.current.keyCode == 13 || (int)Event.current.keyCode == 271);
		Text.Font = (GameFont)2;
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(((Rect)(ref inRect)).x, ((Rect)(ref inRect)).y, ((Rect)(ref inRect)).width, 32f);
		Widgets.Label(val, TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.Title", NamedArgument.op_Implicit(((Entity)bench).LabelShortCap)));
		Text.Font = (GameFont)1;
		if (bills.Count == 0)
		{
			Widgets.Label(new Rect(((Rect)(ref inRect)).x, ((Rect)(ref val)).yMax + 8f, ((Rect)(ref inRect)).width, ((Rect)(ref inRect)).height - 80f), Translator.Translate("HaulersDream.PlanCraft.NoBills"));
			DrawButtons(inRect, 36f, confirmEnabled: false, enterPressed);
			return;
		}
		float num = ((Rect)(ref val)).yMax + 6f;
		float num2 = Mathf.Min((float)bills.Count * 30f + 6f, 170f);
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(((Rect)(ref inRect)).x, num, ((Rect)(ref inRect)).width, num2);
		Widgets.DrawMenuSection(val2);
		Rect val3 = default(Rect);
		((Rect)(ref val3))._002Ector(0f, 0f, ((Rect)(ref val2)).width - 16f, (float)bills.Count * 30f);
		Widgets.BeginScrollView(GenUI.ContractedBy(val2, 2f), ref billScroll, val3, true);
		float num3 = 0f;
		Rect val4 = default(Rect);
		for (int i = 0; i < bills.Count; i++)
		{
			((Rect)(ref val4))._002Ector(0f, num3, ((Rect)(ref val3)).width, 30f);
			if (Mouse.IsOver(val4))
			{
				Widgets.DrawHighlight(val4);
			}
			bool flag = selected == bills[i];
			if (Widgets.RadioButtonLabeled(val4, bills[i].LabelCap, flag, false) && !flag)
			{
				SelectBill(bills[i]);
			}
			num3 += 30f;
		}
		Widgets.EndScrollView();
		RefreshPlan();
		Rect val5 = default(Rect);
		((Rect)(ref val5))._002Ector(((Rect)(ref inRect)).x, ((Rect)(ref val2)).yMax + 10f, ((Rect)(ref inRect)).width, ((Rect)(ref inRect)).height - ((Rect)(ref val2)).yMax - 36f - 18f);
		Listing_Standard val6 = new Listing_Standard();
		((Listing)val6).Begin(val5);
		val6.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.Repeat", NamedArgument.op_Implicit(reps)), -1f, (string)null);
		Rect rect = ((Listing)val6).GetRect(22f, 1f);
		((Listing)val6).Gap(2f);
		HDNumberField.SplitRow(rect, out var sliderRect, out var boxRect);
		int num4 = Mathf.Clamp(reps, 1, maxAvailReps);
		int num5 = Mathf.RoundToInt(Widgets.HorizontalSlider(sliderRect, (float)num4, 1f, (float)maxAvailReps, true, (string)null, (string)null, (string)null, -1f));
		if (num5 != num4)
		{
			reps = num5;
			repsBuf = null;
		}
		HDNumberField.Int(boxRect, "HD_PlanCraftReps", ref reps, ref repsBuf, 1, 500, TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.RepeatBoxTip", NamedArgument.op_Implicit(maxAvailReps))));
		val6.Label((timeoutHours <= 0f) ? Translator.Translate("HaulersDream.PlanCraft.TimeoutOff") : TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.Timeout", NamedArgument.op_Implicit(timeoutHours.ToString("0.#"))), -1f, (string)null);
		Rect rect2 = ((Listing)val6).GetRect(22f, 1f);
		((Listing)val6).Gap(2f);
		HDNumberField.SplitRow(rect2, out var sliderRect2, out var boxRect2);
		float num6 = Mathf.Clamp(timeoutHours, 0f, 8f);
		float num7 = Widgets.HorizontalSlider(sliderRect2, num6, 0f, 8f, true, (string)null, (string)null, (string)null, -1f);
		if (num7 != num6)
		{
			timeoutHours = NumberEntryPolicy.SnapFloat(num7, 0.5f, 0f, 8f);
			timeoutBuf = null;
		}
		HDNumberField.Float(boxRect2, "HD_PlanCraftTimeout", ref timeoutHours, ref timeoutBuf, 0f, 8f, 0.5f, "0.#", TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.TimeoutBoxTip", NamedArgument.op_Implicit(0f.ToString("0.#")), NamedArgument.op_Implicit(8f.ToString("0.#")))));
		((Listing)val6).Gap(6f);
		DrawSummary(val6);
		((Listing)val6).End();
		DrawButtons(inRect, 36f, cachedPlan != null && cachedPlan.feasible, enterPressed);
	}

	private void DrawSummary(Listing_Standard l)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		if (cachedPlan == null)
		{
			return;
		}
		if (!cachedPlan.feasible)
		{
			GUI.color = ColorLibrary.RedReadable;
			l.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.Infeasible", NamedArgument.op_Implicit(cachedPlan.blockReason ?? "")), -1f, (string)null);
			GUI.color = Color.white;
			return;
		}
		int resolvedReps = cachedPlan.resolvedReps;
		if (resolvedReps < cachedPlan.requestedReps)
		{
			string text = cachedPlan.BindingLimit switch
			{
				CraftBatchLimit.Resources => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.LimitResources")), 
				CraftBatchLimit.Mass => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.LimitMass")), 
				CraftBatchLimit.Timeout => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.LimitTimeout")), 
				CraftBatchLimit.BillRepeat => TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.LimitBillRepeat")), 
				_ => "", 
			};
			l.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.ResolvedTrimmed", NamedArgument.op_Implicit(resolvedReps), NamedArgument.op_Implicit(text)), -1f, (string)null);
		}
		else
		{
			l.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.Resolved", NamedArgument.op_Implicit(resolvedReps)), -1f, (string)null);
		}
		if (cachedPlan.mixingRecipe)
		{
			l.Label(Translator.Translate("HaulersDream.PlanCraft.IngredientsMixed"), -1f, (string)null);
		}
		else
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < cachedPlan.ingredientDefs.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append(", ");
				}
				stringBuilder.Append((cachedPlan.perRepCounts[i] * resolvedReps).ToString());
				stringBuilder.Append("× ");
				stringBuilder.Append(((Def)cachedPlan.ingredientDefs[i]).label);
			}
			l.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.Ingredients", NamedArgument.op_Implicit(stringBuilder.ToString())), -1f, (string)null);
		}
		l.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.EstTime", NamedArgument.op_Implicit(((float)cachedPlan.ticksPerRep * (float)resolvedReps / 2500f).ToString("0.#"))), -1f, (string)null);
	}

	private void DrawButtons(Rect inRect, float btnH, bool confirmEnabled, bool enterPressed)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		float num = (((Rect)(ref inRect)).width - 8f) / 2f;
		Rect val = new Rect(((Rect)(ref inRect)).x, ((Rect)(ref inRect)).yMax - btnH, num, btnH);
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(((Rect)(ref inRect)).x + num + 8f, ((Rect)(ref inRect)).yMax - btnH, num, btnH);
		bool enabled = GUI.enabled;
		GUI.enabled = confirmEnabled;
		if (Widgets.ButtonText(val, TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.Confirm")), true, true, true, (TextAnchor?)null) && confirmEnabled)
		{
			Confirm();
		}
		GUI.enabled = enabled;
		if (Widgets.ButtonText(val2, TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.Cancel")), true, true, true, (TextAnchor?)null))
		{
			((Window)this).Close(true);
		}
		if (enterPressed)
		{
			Event.current.Use();
			if (confirmEnabled)
			{
				Confirm();
			}
		}
	}

	private void Confirm()
	{
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		reps = NumberEntryPolicy.ClampInt(reps, 1, 500);
		timeoutHours = NumberEntryPolicy.SnapFloat(timeoutHours, 0.5f, 0f, 8f);
		repsBuf = null;
		timeoutBuf = null;
		planSig = null;
		RefreshPlan();
		if (cachedPlan == null || !cachedPlan.feasible || selected == null)
		{
			if (selected != null)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.CouldNotStart")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
			}
			return;
		}
		if (HaulersDreamMod.Settings != null)
		{
			HaulersDreamMod.Settings.craftBatchTimeoutHours = timeoutHours;
			((ModSettings)HaulersDreamMod.Settings).Write();
		}
		JobDriver_BatchCraft.StartBatchCraftSynced(pawn, bench, selected, reps, Mathf.RoundToInt(timeoutHours * 2500f));
		((Window)this).Close(true);
	}
}
