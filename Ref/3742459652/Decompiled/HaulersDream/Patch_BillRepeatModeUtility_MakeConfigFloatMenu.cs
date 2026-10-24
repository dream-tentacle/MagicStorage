using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(BillRepeatModeUtility), "MakeConfigFloatMenu")]
public static class Patch_BillRepeatModeUtility_MakeConfigFloatMenu
{
	[HarmonyPriority(800)]
	private static bool Prefix(Bill_Production bill)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Expected O, but got Unknown
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Expected O, but got Unknown
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0324: Expected O, but got Unknown
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Expected O, but got Unknown
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Expected O, but got Unknown
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Expected O, but got Unknown
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Expected O, but got Unknown
		//IL_027f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f1: Expected O, but got Unknown
		//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fd: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamGameComponent comp = HaulersDreamGameComponent.Instance;
		List<FloatMenuOption> list = new List<FloatMenuOption>();
		list.Add(new FloatMenuOption(TaggedString.op_Implicit(((Def)BillRepeatModeDefOf.RepeatCount).LabelCap), (Action)delegate
		{
			bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
			MultiplayerCompat.SetBillBatch((Bill)(object)bill, on: false, 0);
		}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
		list.Add(new FloatMenuOption(TaggedString.op_Implicit(((Def)BillRepeatModeDefOf.TargetCount).LabelCap), (Action)delegate
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			if (!((Bill)bill).recipe.WorkerCounter.CanCountProducts(bill))
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("RecipeCannotHaveTargetCount")), MessageTypeDefOf.RejectInput, false);
			}
			else
			{
				bill.repeatMode = BillRepeatModeDefOf.TargetCount;
				MultiplayerCompat.SetBillBatch((Bill)(object)bill, on: false, 0);
			}
		}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
		list.Add(new FloatMenuOption(TaggedString.op_Implicit(((Def)BillRepeatModeDefOf.Forever).LabelCap), (Action)delegate
		{
			bill.repeatMode = BillRepeatModeDefOf.Forever;
			MultiplayerCompat.SetBillBatch((Bill)(object)bill, on: false, 0);
		}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
		EverybodyGetsOneCompat.TryInsertModes(list, bill);
		CompositableLoadoutsCompat.TryInsertModes(list, bill);
		IngredientThresholdCompat.TryInsertModes(list, bill);
		if (comp != null && CraftBatchPlanner.BatchModeAvailable((Bill)(object)bill))
		{
			string text = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Batch.MenuPrefix"));
			FloatMenuOption val = new FloatMenuOption(TaggedString.op_Implicit(text + ": " + ((Def)BillRepeatModeDefOf.RepeatCount).LabelCap), (Action)delegate
			{
				bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
				EnableBatch(comp, bill);
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
			val.tooltip = TipSignal.op_Implicit(Translator.Translate("HaulersDream.Batch.TipDoX"));
			list.Add(val);
			FloatMenuOption val2 = new FloatMenuOption(TaggedString.op_Implicit(text + ": " + ((Def)BillRepeatModeDefOf.TargetCount).LabelCap), (Action)delegate
			{
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				if (!((Bill)bill).recipe.WorkerCounter.CanCountProducts(bill))
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("RecipeCannotHaveTargetCount")), MessageTypeDefOf.RejectInput, false);
				}
				else
				{
					bill.repeatMode = BillRepeatModeDefOf.TargetCount;
					EnableBatch(comp, bill);
				}
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
			val2.tooltip = TipSignal.op_Implicit(Translator.Translate("HaulersDream.Batch.TipUntilX"));
			list.Add(val2);
			FloatMenuOption val3 = new FloatMenuOption(TaggedString.op_Implicit(text + ": " + ((Def)BillRepeatModeDefOf.Forever).LabelCap), (Action)delegate
			{
				bill.repeatMode = BillRepeatModeDefOf.Forever;
				EnableBatch(comp, bill);
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
			val3.tooltip = TipSignal.op_Implicit(Translator.Translate("HaulersDream.Batch.TipForever"));
			list.Add(val3);
			if (comp.IsBatchBill((Bill)(object)bill))
			{
				FloatMenuOption val4 = new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Batch.SetSize", NamedArgument.op_Implicit(comp.BatchSizeOf((Bill)(object)bill)))), (Action)delegate
				{
					Find.WindowStack.Add((Window)(object)new Dialog_BatchSize((Bill)(object)bill));
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
				val4.tooltip = TipSignal.op_Implicit(Translator.Translate("HaulersDream.Batch.TipSize"));
				list.Add(val4);
				if (bill.repeatMode == BillRepeatModeDefOf.TargetCount)
				{
					FloatMenuOption val5 = new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Batch.SetOvershoot", NamedArgument.op_Implicit(comp.BatchOvershootOf((Bill)(object)bill)))), (Action)delegate
					{
						Find.WindowStack.Add((Window)(object)new Dialog_BatchOvershoot((Bill)(object)bill));
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
					val5.tooltip = TipSignal.op_Implicit(Translator.Translate("HaulersDream.Batch.TipOvershoot"));
					list.Add(val5);
				}
			}
		}
		Find.WindowStack.Add((Window)new FloatMenu(list));
		return false;
	}

	private static void EnableBatch(HaulersDreamGameComponent comp, Bill_Production bill)
	{
		int num = comp.BatchSizeOf((Bill)(object)bill);
		if (num < 1)
		{
			num = Mathf.Max(1, HaulersDreamMod.Settings?.defaultBatchSize ?? 10);
		}
		MultiplayerCompat.SetBillBatch((Bill)(object)bill, on: true, num);
	}
}
