using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CeleTech.Base;

public class Window_TradeShuttle : Window_NavigationBase
{
	private Vector2 scrollPosition = Vector2.zero;

	private const float ShuttleEntryHeight = 100f;

	private const int SupplyShuttleCooldownHours = 24;

	private const int ArsenalShuttleCooldownHours = 48;

	private const int SlaveShuttleCooldownHours = 144;

	private List<ShuttleEntry> shuttleEntries;

	private float totalViewHeight;

	private void InitializeShuttles()
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		if (shuttleEntries == null)
		{
			shuttleEntries = new List<ShuttleEntry>
			{
				new ShuttleEntry("CMC_TraderShuttle", "CMC_OrbitalTrader_Materials", "Things/Skyfaller/TradeShuttle", new Color(0.1f, 0.1f, 0.1f, 0.5f), () => GameComponent_CeleTech.Instance.LastAuxHr, delegate
				{
					GameComponent_CeleTech.Instance.LastAuxHr = 0;
				}, 24, 2500, "CMC_SupplyShuttleDesc"),
				new ShuttleEntry("CMC_TraderShuttle_A", "CMC_OrbitalTrader_Weapons", "Things/Skyfaller/TradeShuttle_A", new Color(0.33f, 0.33f, 0.33f, 0.5f), () => GameComponent_CeleTech.Instance.LastArsHr, delegate
				{
					GameComponent_CeleTech.Instance.LastArsHr = 0;
				}, 48, 3000, "CMC_ArsenalShuttleDesc"),
				new ShuttleEntry("CMC_TraderShuttle_S", "CMC_OrbitalTrader_Slaves", "Things/Skyfaller/TradeShuttle_S", new Color(0.1f, 0.1f, 0.1f, 0.5f), () => GameComponent_CeleTech.Instance.LastSSHr, delegate
				{
					GameComponent_CeleTech.Instance.LastSSHr = 0;
				}, 144, 4500, "CMC_SlaveShuttleDesc")
			};
			totalViewHeight = 110f * (float)shuttleEntries.Count;
		}
	}

	public override void DoNavWindowContents(Rect contentRect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Expected O, but got Unknown
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		Widgets.DrawMenuSection(contentRect);
		Rect val = GenUI.ContractedBy(contentRect, 10f);
		if (GameComponent_CeleTech.Instance == null)
		{
			return;
		}
		int num = ((Find.CurrentMap != null) ? Find.CurrentMap.resourceCounter.GetCount(ThingDefOf.Silver) : 0);
		Rect val2 = default(Rect);
		((Rect)(ref val2))..ctor(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width, 30f);
		Text.Anchor = (TextAnchor)5;
		Text.Font = (GameFont)1;
		Widgets.Label(val2, TranslatorFormattedStringExtensions.Translate("CMC_ColonySilver", NamedArgument.op_Implicit(num)));
		Text.Font = (GameFont)1;
		Text.Anchor = (TextAnchor)0;
		InitializeShuttles();
		Rect val3 = default(Rect);
		((Rect)(ref val3))..ctor(((Rect)(ref val)).x, ((Rect)(ref val2)).yMax + 10f, ((Rect)(ref val)).width, ((Rect)(ref val)).height - ((Rect)(ref val2)).height - 10f);
		Rect val4 = default(Rect);
		((Rect)(ref val4))..ctor(0f, 0f, ((Rect)(ref val3)).width - 16f, totalViewHeight);
		Widgets.BeginScrollView(val3, ref scrollPosition, val4, true);
		Listing_Standard val5 = new Listing_Standard();
		((Listing)val5).Begin(val4);
		if (shuttleEntries != null)
		{
			for (int i = 0; i < shuttleEntries.Count; i++)
			{
				DrawGenericShuttleEntry(val5, shuttleEntries[i], i);
			}
		}
		((Listing)val5).End();
		Widgets.EndScrollView();
		Text.Anchor = (TextAnchor)0;
		Text.Font = (GameFont)1;
	}

	private void DrawGenericShuttleEntry(Listing_Standard listing, ShuttleEntry entry, int index)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		if (entry.TraderKindDef == null)
		{
			return;
		}
		int num = entry.GetCurrentCooldownHours();
		bool flag = num < entry.CooldownDurationHours;
		int num2 = entry.CooldownDurationHours - num;
		if (num2 < 0)
		{
			num2 = 0;
		}
		Rect rect = ((Listing)listing).GetRect(100f, 1f);
		if (index % 2 == 0)
		{
			Widgets.DrawHighlight(rect);
		}
		Widgets.DrawBoxSolid(rect, entry.BackgroundColor);
		Widgets.DrawHighlightIfMouseover(rect);
		Rect val = default(Rect);
		((Rect)(ref val))..ctor(((Rect)(ref rect)).x + 10f, ((Rect)(ref rect)).y + 10f, 80f, 80f);
		GUI.DrawTexture(val, (Texture)(object)entry.Icon, (ScaleMode)2);
		Rect rect2 = default(Rect);
		((Rect)(ref rect2))..ctor(((Rect)(ref rect)).xMax - 160f, ((Rect)(ref rect)).y + (((Rect)(ref rect)).height - 40f) / 2f, 150f, 40f);
		float num3 = ((Rect)(ref rect2)).x - ((Rect)(ref val)).xMax - 20f;
		Rect val2 = default(Rect);
		((Rect)(ref val2))..ctor(((Rect)(ref val)).xMax + 10f, ((Rect)(ref rect)).y + 10f, num3, 80f);
		Text.Font = (GameFont)2;
		Rect val3 = default(Rect);
		((Rect)(ref val3))..ctor(((Rect)(ref val2)).x, ((Rect)(ref val2)).y, ((Rect)(ref val2)).width, 30f);
		Widgets.Label(val3, GenText.CapitalizeFirst(((Def)entry.TraderKindDef).label));
		Text.Font = (GameFont)1;
		Rect val4 = default(Rect);
		((Rect)(ref val4))..ctor(((Rect)(ref val2)).x, ((Rect)(ref val3)).yMax, ((Rect)(ref val2)).width, 25f);
		Widgets.Label(val4, Translator.Translate(entry.DescriptionKey));
		Rect val5 = default(Rect);
		((Rect)(ref val5))..ctor(((Rect)(ref val2)).x, ((Rect)(ref val4)).yMax, ((Rect)(ref val2)).width, 25f);
		string text = "";
		bool active = false;
		int num4 = 0;
		if (Find.CurrentMap != null)
		{
			num4 = Find.CurrentMap.resourceCounter.Silver;
		}
		if (flag)
		{
			Widgets.Label(val5, TranslatorFormattedStringExtensions.Translate("CMC_CooldownHours", NamedArgument.op_Implicit(num2)));
			text = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CMC_CallEarlyCost", NamedArgument.op_Implicit(entry.EarlyCallCost)));
			if (num4 >= entry.EarlyCallCost)
			{
				active = true;
			}
		}
		else
		{
			Widgets.Label(val5, Translator.Translate("CMC_StatusReady"));
			text = TaggedString.op_Implicit(Translator.Translate("CMC_CallShuttle"));
			active = true;
		}
		if (TradeWindowDrawer.DrawCustomButton(rect2, text, active))
		{
			HandleShuttleCall(flag, entry);
		}
	}

	private void HandleShuttleCall(bool wasOnCooldown, ShuttleEntry entry)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (new SpawnTradeShip().SpawnShip(((Def)entry.TraderKindDef).defName))
			{
				if (wasOnCooldown)
				{
					Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CMC_PaidEarlyCall", NamedArgument.op_Implicit(entry.EarlyCallCost))), MessageTypeDefOf.PositiveEvent, true);
					TradeUtility.LaunchSilver(Find.CurrentMap, entry.EarlyCallCost);
				}
				entry.ResetCooldown();
			}
		}
		catch (Exception ex)
		{
			Log.Error("Error calling shuttle " + ((Def)entry.TraderKindDef).defName + ": " + ex.Message);
		}
	}
}
