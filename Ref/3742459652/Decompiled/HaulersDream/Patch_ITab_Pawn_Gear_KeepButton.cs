using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(ITab_Pawn_Gear), "DrawThingRow")]
public static class Patch_ITab_Pawn_Gear_KeepButton
{
	private const float RowHeight = 28f;

	private const float ChipW = 40f;

	private const float ChipH = 20f;

	private static readonly MethodInfo SelPawnForGearGetter = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), "SelPawnForGear");

	private static ITab_Pawn_Gear cachedInstance;

	private static int cachedFrame = -1;

	private static Pawn cachedPawn;

	private static CompHauledToInventory cachedComp;

	private static bool Prepare()
	{
		return SelPawnForGearGetter != null;
	}

	private static bool TryGetPawnComp(ITab_Pawn_Gear inst, out Pawn pawn, out CompHauledToInventory comp)
	{
		int frameCount = Time.frameCount;
		if (inst != cachedInstance || frameCount != cachedFrame)
		{
			cachedInstance = inst;
			cachedFrame = frameCount;
			object obj = SelPawnForGearGetter.Invoke(inst, null);
			cachedPawn = (Pawn)((obj is Pawn) ? obj : null);
			cachedComp = ((cachedPawn != null && cachedPawn.IsColonistPlayerControlled) ? ((ThingWithComps)cachedPawn).GetComp<CompHauledToInventory>() : null);
		}
		pawn = cachedPawn;
		comp = cachedComp;
		if (pawn != null)
		{
			return comp != null;
		}
		return false;
	}

	private static void Postfix(ITab_Pawn_Gear __instance, float y, float width, Thing thing, bool inventory)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Invalid comparison between Unknown and I4
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (!inventory || thing?.def == null || settings == null || !settings.keepInventoryGearButton || (int)thing.def.category != 2 || !thing.def.EverHaulable || thing.def.stackLimit <= 1 || !TryGetPawnComp(__instance, out var pawn, out var comp))
		{
			return;
		}
		int num = comp.KeptCountOf(thing.def);
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, y - 28f, width, 28f);
		bool flag = Mouse.IsOver(val);
		if (num > 0 || flag)
		{
			float num2 = width - 132f - 4f - 40f;
			if (num2 < 34f)
			{
				num2 = 34f;
			}
			Rect val2 = default(Rect);
			((Rect)(ref val2))._002Ector(num2, ((Rect)(ref val)).y + 4f, 40f, 20f);
			DrawChip(val2, num, flag);
			if (Widgets.ButtonInvisible(val2, true))
			{
				OpenAmountDialog(pawn, comp, thing.def);
			}
			if (Mouse.IsOver(val2))
			{
				string text = ((num > 0) ? ((object)TranslatorFormattedStringExtensions.Translate("HaulersDream.Keep.GearTooltipKept", NamedArgument.op_Implicit(num), NamedArgument.op_Implicit(((Def)thing.def).label))/*cast due to constrained. prefix*/).ToString() : ((object)TranslatorFormattedStringExtensions.Translate("HaulersDream.Keep.GearTooltipOff", NamedArgument.op_Implicit(((Def)thing.def).label))/*cast due to constrained. prefix*/).ToString());
				TooltipHandler.TipRegion(val2, TipSignal.op_Implicit(text));
			}
		}
	}

	private static void DrawChip(Rect chip, int keptN, bool rowHovered)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		Color color = GUI.color;
		Text.Font = (GameFont)0;
		Text.Anchor = (TextAnchor)4;
		bool flag = Mouse.IsOver(chip);
		if (keptN > 0)
		{
			Widgets.DrawBoxSolid(chip, new Color(0.85f, 0.66f, 0.2f, flag ? 0.55f : 0.38f));
			GUI.color = new Color(1f, 0.95f, 0.75f);
			Widgets.Label(chip, keptN.ToString());
			GUI.color = new Color(0.85f, 0.66f, 0.2f, 0.9f);
			Widgets.DrawBox(chip, 1, (Texture2D)null);
		}
		else
		{
			Widgets.DrawBoxSolid(chip, new Color(1f, 1f, 1f, flag ? 0.16f : 0.07f));
			GUI.color = new Color(0.85f, 0.86f, 0.9f, flag ? 0.95f : 0.6f);
			Widgets.Label(chip, Translator.Translate("HaulersDream.Keep.GearChip"));
			Widgets.DrawBox(chip, 1, (Texture2D)null);
		}
		GUI.color = color;
		Text.Anchor = anchor;
		Text.Font = font;
	}

	private static void OpenAmountDialog(Pawn pawn, CompHauledToInventory comp, ThingDef def)
	{
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected O, but got Unknown
		int num = HeldOfDef(pawn, def);
		int num2 = comp.KeptCountOf(def);
		int num3 = Mathf.Max(new int[3] { num, num2, 1 });
		Pawn pawnLocal = pawn;
		ThingDef defLocal = def;
		Find.WindowStack.Add((Window)new Dialog_Slider((Func<int, string>)((int n) => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Keep.GearSliderLabel", NamedArgument.op_Implicit(n), NamedArgument.op_Implicit(((Def)defLocal).label)))), 0, num3, (Action<int>)delegate(int n)
		{
			MultiplayerCompat.SetKeptCount(pawnLocal, defLocal, n);
		}, (num2 > 0) ? num2 : num, 1f));
	}

	private static int HeldOfDef(Pawn pawn, ThingDef def)
	{
		ThingOwner<Thing> val = pawn?.inventory?.innerContainer;
		if (val == null || def == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < ((ThingOwner)val).Count; i++)
		{
			Thing val2 = val[i];
			if (val2 != null && val2.def == def)
			{
				num += val2.stackCount;
			}
		}
		return num;
	}
}
