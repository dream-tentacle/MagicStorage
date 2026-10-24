using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName = ".NET Framework 4.7.2")]
[assembly: AssemblyVersion("0.0.0.0")]
namespace TradeHelper;

internal class MatchedItem
{
	public string ItemName { get; set; }

	public int Position { get; set; }

	public MatchedItem()
	{
	}

	public MatchedItem(string itemName, int position)
	{
		ItemName = itemName;
		Position = position;
	}

	public static string GetMatchedStrings(List<MatchedItem> matchedItems)
	{
		string text = "";
		for (int i = 0; i < matchedItems.Count; i++)
		{
			text = text + matchedItems[i].ItemName + "\n";
		}
		return text;
	}

	public static int GetMatchedItemPositionIndex(List<MatchedItem> matchedItems, float position)
	{
		for (int i = 0; i < matchedItems.Count; i++)
		{
			if ((float)matchedItems[i].Position * 30f == position)
			{
				return i;
			}
		}
		return -1;
	}
}
public class TradeHelperSettings : ModSettings
{
	public string wishList = "";

	public override void ExposeData()
	{
		Scribe_Values.Look<string>(ref wishList, "TradeHelperWishList", (string)null, false);
		((ModSettings)this).ExposeData();
	}
}
public class TradeHelperMod : Mod
{
	private TradeHelperSettings settings;

	public TradeHelperMod(ModContentPack content)
		: base(content)
	{
		settings = ((Mod)this).GetSettings<TradeHelperSettings>();
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		Listing_Standard val = new Listing_Standard();
		((Listing)val).Begin(inRect);
		val.Label(Translator.Translate("TradeHelperSettingDescription"), -1f, (string)null);
		settings.wishList = val.TextEntry(settings.wishList, 1);
		((Listing)val).End();
		((Mod)this).DoSettingsWindowContents(inRect);
	}
}
public class Dialog_TradeHelperSettings : Window
{
	private Mod selMod;

	public override Vector2 InitialSize => new Vector2(800f, 400f);

	public Dialog_TradeHelperSettings()
		: base((IWindowDrawing)null)
	{
		base.forcePause = true;
		base.closeOnCancel = true;
		base.doCloseX = true;
		base.doCloseButton = true;
		base.closeOnClickedOutside = false;
		base.absorbInputAroundWindow = true;
	}

	public override void PreClose()
	{
		((Window)this).PreClose();
		if (selMod != null)
		{
			selMod.WriteSettings();
		}
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		selMod = (Mod)(object)LoadedModManager.GetMod<TradeHelperMod>();
		if (selMod != null)
		{
			Text.Font = (GameFont)1;
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(0f, 10f, ((Rect)(ref inRect)).width, ((Rect)(ref inRect)).height - 20f);
			selMod.DoSettingsWindowContents(val);
		}
	}

	public static bool HasSettings()
	{
		return LoadedModManager.ModHandles.Any((Mod mod) => !GenText.NullOrEmpty(mod.SettingsCategory()));
	}
}
[StaticConstructorOnStartup]
public static class Main
{
	[HarmonyPatch(typeof(Dialog_Trade), "DoWindowContents")]
	public class TradePatch
	{
		public static bool Prefix(Dialog_Trade __instance, ref Rect inRect)
		{
			((Rect)(ref ((Window)__instance).windowRect)).width = m_windowNewWidth;
			if (m_windowOldLeft <= -1000f)
			{
				m_windowOldLeft = ((Rect)(ref ((Window)__instance).windowRect)).x;
				m_regionHeight = UI.screenHeight - 186;
			}
			((Rect)(ref ((Window)__instance).windowRect)).x = m_windowOldLeft - (m_windowNewWidth - 1000f) / 2f;
			((Rect)(ref inRect)).width = 988f;
			if (((Rect)(ref ((Window)__instance).windowRect)).x < 0f)
			{
				((Rect)(ref ((Window)__instance).windowRect)).x = ((float)UI.screenWidth - m_windowNewWidth) / 2f;
			}
			return true;
		}

		public static void Postfix(Dialog_Trade __instance, ref Rect inRect, ref Vector2 ___scrollPosition, ref List<Tradeable> ___cachedTradeables)
		{
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_0100: Unknown result type (might be due to invalid IL or missing references)
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0148: Unknown result type (might be due to invalid IL or missing references)
			//IL_0159: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_021a: Unknown result type (might be due to invalid IL or missing references)
			//IL_023e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0262: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02de: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e4: Invalid comparison between Unknown and I4
			if (!((Window)__instance).IsOpen)
			{
				return;
			}
			try
			{
				float y = 0f;
				Rect regionBuy = default(Rect);
				((Rect)(ref regionBuy))..ctor(990f, 96f, 300f, m_regionHeight);
				Text.Font = (GameFont)1;
				Text.Anchor = (TextAnchor)0;
				GUI.color = Color.white;
				if (TestIfReset() || m_trads == null)
				{
					m_trads = new List<Tradeable>();
				}
				string matchedStrings = "";
				char[] separator = new char[2] { ',', '，' };
				wishListItems = GetWishListSetting().Split(separator, StringSplitOptions.RemoveEmptyEntries).ToList();
				wishListItems = CleanWishlist(wishListItems);
				Rect val = default(Rect);
				((Rect)(ref val))..ctor(((Rect)(ref regionBuy)).x + 2f, 20f, ((Rect)(ref regionBuy)).width - 28f, 75f);
				GUI.color = Color.white;
				Text.Font = (GameFont)1;
				Text.Anchor = (TextAnchor)4;
				if (Widgets.ButtonText(new Rect(((Rect)(ref val)).x + 90f, 0f, 80f, 19f), TaggedString.op_Implicit(Translator.Translate("TradeHelperTitleWishlist")), true, true, true, (TextAnchor?)null))
				{
					Dialog_TradeHelperSettings dialog_TradeHelperSettings = new Dialog_TradeHelperSettings();
					Find.WindowStack.Add((Window)(object)dialog_TradeHelperSettings);
				}
				List<Tradeable> tradsTemp = GenList.ListFullCopy<Tradeable>(___cachedTradeables);
				Widgets.DrawBoxSolid(val, new Color(0.19f, 0.2f, 0.21f));
				if (wishListItems.Count() > 0)
				{
					List<MatchedItem> matchedItems;
					Rect boxRect;
					GenUI.DrawElementStack<string>(new Rect(((Rect)(ref val)).x + 2f, ((Rect)(ref val)).y + 2f, ((Rect)(ref val)).width - 2f, ((Rect)(ref val)).height - 2f), 20f, wishListItems, (StackElementDrawer<string>)delegate(Rect r, string item)
					{
						//IL_014a: Unknown result type (might be due to invalid IL or missing references)
						//IL_0154: Unknown result type (might be due to invalid IL or missing references)
						//IL_004c: Unknown result type (might be due to invalid IL or missing references)
						//IL_0051: Unknown result type (might be due to invalid IL or missing references)
						//IL_0056: Unknown result type (might be due to invalid IL or missing references)
						//IL_005b: Unknown result type (might be due to invalid IL or missing references)
						//IL_0066: Unknown result type (might be due to invalid IL or missing references)
						//IL_0082: Unknown result type (might be due to invalid IL or missing references)
						//IL_0089: Unknown result type (might be due to invalid IL or missing references)
						//IL_0094: Unknown result type (might be due to invalid IL or missing references)
						//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
						//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
						matchedItems = GetMatchedItems(item, tradsTemp);
						if (matchedItems.Count > 0)
						{
							boxRect = new Rect(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, ((Rect)(ref r)).width + 5f, ((Rect)(ref r)).height + 2f));
							GUI.color = Color.green;
							GUI.Box(boxRect, item);
							matchedStrings = MatchedItem.GetMatchedStrings(matchedItems);
							TooltipHandler.TipRegion(r, TipSignal.op_Implicit(matchedStrings));
							if (Mouse.IsOver(boxRect))
							{
								Widgets.DrawLightHighlight(boxRect);
							}
							if (Widgets.ButtonInvisible(r, true))
							{
								int matchedItemPositionIndex = MatchedItem.GetMatchedItemPositionIndex(matchedItems, m_pos);
								if (matchedItemPositionIndex < 0)
								{
									m_pos = (float)matchedItems[0].Position * 30f;
									m_posChanged = true;
								}
								else if (matchedItemPositionIndex + 1 < matchedItems.Count)
								{
									m_pos = (float)matchedItems[matchedItemPositionIndex + 1].Position * 30f;
									m_posChanged = true;
								}
								else
								{
									m_pos = (float)matchedItems[0].Position * 30f;
									m_posChanged = true;
								}
							}
						}
						else
						{
							GUI.color = Color.white;
							GUI.Label(r, item);
						}
					}, (StackElementWidthGetter<string>)((string item) => Text.CalcSize(item).x + 0f), 4f, 8f, false);
					if (m_posChanged)
					{
						___scrollPosition.y = m_pos;
						m_posChanged = false;
					}
				}
				if (m_trads.Count > 0)
				{
					GUI.color = Color.white;
					Widgets.BeginScrollView(new Rect(0f, 96f, m_windowNewWidth - 35f, m_regionHeight), ref scrollPos, new Rect(0f, 96f, m_windowNewWidth - 55f, scrollViewHeight), true);
					IEnumerable<Tradeable> trads = m_trads.Where((Tradeable val2) => ((Transferable)val2).CountToTransfer > 0);
					IEnumerable<Tradeable> trads2 = m_trads.Where((Tradeable val2) => ((Transferable)val2).CountToTransfer < 0);
					Utils.DrawTradingItems(ref y, regionBuy, trads, Utils.TradingType.Buying);
					Utils.DrawTradingItems(ref y, regionBuy, trads2, Utils.TradingType.Selling);
					if ((int)Event.current.type == 8)
					{
						scrollViewHeight = y + 30f;
					}
					Widgets.EndScrollView();
				}
				Text.Anchor = (TextAnchor)0;
			}
			catch (Exception ex)
			{
				Log.Message("[TradeHelper] error:" + ex.Message);
			}
		}

		private static List<string> CleanWishlist(List<string> wishListItems)
		{
			List<string> list = new List<string>();
			for (int i = 0; i < wishListItems.Count(); i++)
			{
				if (wishListItems[i].Trim().Length > 0)
				{
					list.Add(wishListItems[i].Trim());
				}
			}
			return list;
		}

		private static bool TestIfReset()
		{
			foreach (Tradeable allTradeable in TradeSession.deal.AllTradeables)
			{
				if (((Transferable)allTradeable).CountToTransfer != 0)
				{
					return false;
				}
			}
			return true;
		}

		private static string GetWishListSetting()
		{
			return ((Mod)LoadedModManager.GetMod<TradeHelperMod>()).GetSettings<TradeHelperSettings>().wishList;
		}

		private static List<MatchedItem> GetMatchedItems(string wishListItem, List<Tradeable> trads)
		{
			List<MatchedItem> list = new List<MatchedItem>();
			for (int i = 0; i < trads.Count(); i++)
			{
				Tradeable val = trads[i];
				if (val.CountHeldBy((Transactor)1) > 0 && Regex.IsMatch(((Transferable)val).AnyThing.LabelCapNoCount, WildCardToRegular(wishListItem), RegexOptions.IgnoreCase))
				{
					list.Add(new MatchedItem(((Transferable)val).AnyThing.LabelCapNoCount, i));
				}
			}
			return list;
		}

		public static bool GetMatchedStatus(string wishListItem, string tradItem)
		{
			if (Regex.IsMatch(tradItem, WildCardToRegular(wishListItem), RegexOptions.IgnoreCase))
			{
				return true;
			}
			return false;
		}

		private static string WildCardToRegular(string value)
		{
			return "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";
		}
	}

	[HarmonyPatch(typeof(TradeUI), "DrawTradeableRow")]
	public class TradePatch4
	{
		public static void Postfix(Rect rect, Tradeable trad, int index)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			_ = GUI.color;
			if (trad.IsCurrency)
			{
				return;
			}
			if (m_trads.Contains(trad) && ((Transferable)trad).CountToTransfer == 0)
			{
				m_trads.Remove(trad);
			}
			else if (!m_trads.Contains(trad) && ((Transferable)trad).CountToTransfer != 0)
			{
				m_trads.Add(trad);
			}
			if (Mouse.IsOver(rect))
			{
				Color color = GUI.color;
				GUI.color = m_hoverColor;
				Widgets.DrawBox(rect, 2, (Texture2D)null);
				GUI.color = color;
			}
			if (trad.CountHeldBy((Transactor)1) <= 0 || wishListItems == null)
			{
				return;
			}
			foreach (string wishListItem in wishListItems)
			{
				if (TradePatch.GetMatchedStatus(wishListItem, ((Transferable)trad).AnyThing.LabelCapNoCount))
				{
					Color color2 = GUI.color;
					GUI.color = m_highlightColor;
					Widgets.DrawBox(rect, 1, (Texture2D)null);
					GUI.color = color2;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Dialog_FormCaravan), "DoWindowContents")]
	public class CaravanPatch
	{
		public static bool Prefix(Dialog_FormCaravan __instance, ref Rect inRect)
		{
			((Rect)(ref ((Window)__instance).windowRect)).width = m_windowNewWidth;
			if (m_windowOldLeft2 <= -1000f)
			{
				m_windowOldLeft2 = ((Rect)(ref ((Window)__instance).windowRect)).x;
				m_regionHeight = ((Rect)(ref inRect)).height - 150f;
			}
			((Rect)(ref ((Window)__instance).windowRect)).x = m_windowOldLeft2 - (m_windowNewWidth - 1000f) / 2f;
			((Rect)(ref inRect)).width = 988f;
			if (((Rect)(ref ((Window)__instance).windowRect)).x < 0f)
			{
				((Rect)(ref ((Window)__instance).windowRect)).x = ((float)UI.screenWidth - m_windowNewWidth) / 2f;
			}
			return true;
		}

		public static void Postfix(Dialog_FormCaravan __instance, ref Rect inRect, ref List<TransferableOneWay> ___transferables)
		{
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0128: Unknown result type (might be due to invalid IL or missing references)
			//IL_0139: Unknown result type (might be due to invalid IL or missing references)
			//IL_013f: Invalid comparison between Unknown and I4
			if (!((Window)__instance).IsOpen)
			{
				return;
			}
			try
			{
				float y = 0f;
				Rect regionBuy = default(Rect);
				((Rect)(ref regionBuy))..ctor(990f, 96f, 300f, m_regionHeight);
				Text.Font = (GameFont)1;
				Text.Anchor = (TextAnchor)0;
				GUI.color = Color.white;
				if (___transferables.Count == 0 || m_trads2 == null)
				{
					m_trads2 = new List<TransferableOneWay>();
				}
				IEnumerable<TransferableOneWay> source = ___transferables.Where((TransferableOneWay trads) => ((Transferable)trads).CountToTransfer > 0);
				if (source.Count() == 0)
				{
					m_trads2 = new List<TransferableOneWay>();
				}
				if (___transferables.Count > 0)
				{
					GUI.color = Color.white;
					Widgets.BeginScrollView(new Rect(0f, 96f, m_windowNewWidth - 35f, m_regionHeight), ref scrollPos, new Rect(0f, 96f, m_windowNewWidth - 55f, scrollViewHeight), true);
					source = ___transferables.Where((TransferableOneWay trads) => ((Transferable)trads).CountToTransfer >= 0);
					Utils.AppendCleanNewTransferable(ref m_trads2, source);
					Utils.DrawTradingItems(ref y, regionBuy, m_trads2, Utils.TradingType.Caravan);
					if ((int)Event.current.type == 8)
					{
						scrollViewHeight = y + 30f;
					}
					Widgets.EndScrollView();
				}
				Text.Anchor = (TextAnchor)0;
			}
			catch (Exception ex)
			{
				Log.Message("[TradeHelper] form caravan error:" + ex.Message);
			}
		}
	}

	[HarmonyPatch(typeof(Dialog_FormCaravan), "CalculateAndRecacheTransferables")]
	public class CaravanPatch2
	{
		public static void Postfix()
		{
			m_trads2 = new List<TransferableOneWay>();
		}
	}

	[HarmonyPatch(typeof(TransferableOneWayWidget), "DoRow")]
	public class CaravanPatch3
	{
		public static void Postfix(Rect rect, TransferableOneWay trad, int index, float availableMass)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			_ = GUI.color;
			if (Mouse.IsOver(rect))
			{
				Color color = GUI.color;
				GUI.color = m_hoverColor;
				Widgets.DrawBox(rect, 2, (Texture2D)null);
				GUI.color = color;
			}
		}
	}

	private static float m_windowOldLeft;

	private static float m_windowOldLeft2;

	private static float m_windowNewWidth;

	private static float m_regionHeight;

	private static Color m_highlightColor;

	private static Color m_hoverColor;

	private static List<Tradeable> m_trads;

	private static List<TransferableOneWay> m_trads2;

	private static Vector2 scrollPos;

	private static float scrollViewHeight;

	private static List<string> wishListItems;

	private static float m_pos;

	private static bool m_posChanged;

	static Main()
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		m_windowOldLeft = -1000f;
		m_windowOldLeft2 = -1000f;
		m_windowNewWidth = 1300f;
		m_highlightColor = new Color(0f, 0.75f, 0f, 0.8f);
		m_hoverColor = new Color(0.5f, 0.5f, 0.5f);
		m_trads = new List<Tradeable>();
		m_trads2 = new List<TransferableOneWay>();
		m_posChanged = false;
		new Harmony("TimmyLiang.TradeHelper").PatchAll(Assembly.GetExecutingAssembly());
		m_windowOldLeft = -1000f;
		m_windowOldLeft2 = -1000f;
		m_windowNewWidth = 1300f;
		Log.Message("[TradeHelper] Loaded.");
	}
}
public static class Utils
{
	public enum TradingType
	{
		Gift,
		Buying,
		Selling,
		Caravan
	}

	public static void DrawTradingItems(ref float y, Rect regionBuy, IEnumerable<Tradeable> trads, TradingType tradingType)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		if (trads.Count() <= 0)
		{
			return;
		}
		string text = "";
		if (TradeSession.giftMode)
		{
			text = TaggedString.op_Implicit(Translator.Translate("TradeHelperTitleGift"));
		}
		else
		{
			switch (tradingType)
			{
			case TradingType.Buying:
				text = TaggedString.op_Implicit(Translator.Translate("TradeHelperTitleBuying") + " (-") + GetTradeSum(trads, tradingType == TradingType.Buying) + " " + GetTradingCurrency() + ")";
				break;
			case TradingType.Selling:
				text = TaggedString.op_Implicit(Translator.Translate("TradeHelperTitleSelling") + " (+") + GetTradeSum(trads, tradingType == TradingType.Buying) + " " + GetTradingCurrency() + ")";
				break;
			default:
				return;
			}
		}
		Text.Anchor = (TextAnchor)1;
		Widgets.Label(new Rect(((Rect)(ref regionBuy)).x + 2f, ((Rect)(ref regionBuy)).y + y + 4f, 270f, 28f), text);
		Widgets.DrawHighlight(new Rect(((Rect)(ref regionBuy)).x + 2f, ((Rect)(ref regionBuy)).y + y + 4f, 300f, 22f));
		y += 28f;
		foreach (Tradeable trad in trads)
		{
			Text.Anchor = (TextAnchor)0;
			Widgets.ThingIcon(new Rect(((Rect)(ref regionBuy)).x + 2f, ((Rect)(ref regionBuy)).y + y, 28f, 28f), ((Transferable)trad).AnyThing, 1f, (Rot4?)null, false, 1f, false);
			Widgets.Label(new Rect(((Rect)(ref regionBuy)).x + 34f, ((Rect)(ref regionBuy)).y + y + 4f, 300f, 28f), GenText.Truncate(((Transferable)trad).AnyThing.LabelCapNoCount, 180f, (Dictionary<string, string>)null));
			Text.Anchor = (TextAnchor)2;
			Widgets.Label(new Rect(((Rect)(ref regionBuy)).x + 34f + 180f, ((Rect)(ref regionBuy)).y + y + 4f, 40f, 28f), Math.Abs(((Transferable)trad).CountToTransfer).ToString());
			y += 28f;
		}
	}

	public static void DrawTradingItems(ref float y, Rect regionBuy, IEnumerable<TransferableOneWay> trads, TradingType tradingType)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		if (trads.Count() <= 0)
		{
			return;
		}
		string text = "";
		if (tradingType != TradingType.Caravan)
		{
			return;
		}
		text = TaggedString.op_Implicit(Translator.Translate("Caravan"));
		Text.Anchor = (TextAnchor)1;
		Widgets.Label(new Rect(((Rect)(ref regionBuy)).x + 2f, ((Rect)(ref regionBuy)).y + y + 4f, 270f, 28f), text);
		Widgets.DrawHighlight(new Rect(((Rect)(ref regionBuy)).x + 2f, ((Rect)(ref regionBuy)).y + y + 4f, 300f, 22f));
		y += 28f;
		foreach (TransferableOneWay trad in trads)
		{
			Text.Anchor = (TextAnchor)0;
			Widgets.ThingIcon(new Rect(((Rect)(ref regionBuy)).x + 2f, ((Rect)(ref regionBuy)).y + y, 28f, 28f), ((Transferable)trad).AnyThing, 1f, (Rot4?)null, false, 1f, false);
			Widgets.Label(new Rect(((Rect)(ref regionBuy)).x + 34f, ((Rect)(ref regionBuy)).y + y + 4f, 300f, 28f), GenText.Truncate(((Transferable)trad).AnyThing.LabelCapNoCount, 180f, (Dictionary<string, string>)null));
			Text.Anchor = (TextAnchor)2;
			Widgets.Label(new Rect(((Rect)(ref regionBuy)).x + 34f + 180f, ((Rect)(ref regionBuy)).y + y + 4f, 40f, 28f), Math.Abs(((Transferable)trad).CountToTransfer).ToString());
			y += 28f;
		}
	}

	private static float GetTradeSum(IEnumerable<Tradeable> trads, bool isBuying)
	{
		float num = 0f;
		float num2 = 0f;
		foreach (Tradeable trad in trads)
		{
			num2 = ((!isBuying) ? trad.GetPriceFor((TradeAction)2) : trad.GetPriceFor((TradeAction)1));
			num += (float)((Transferable)trad).CountToTransfer * num2;
		}
		return (float)Math.Abs(Math.Round(num));
	}

	private static string GetTradingCurrency()
	{
		try
		{
			return ((Transferable)TradeSession.deal.AllTradeables.Where((Tradeable trads) => trads.IsCurrency).ToList()[0]).AnyThing.LabelCapNoCount;
		}
		catch (Exception ex)
		{
			Log.Message("[TradeHelper] error:" + ex.Message);
			return "";
		}
	}

	public static void AppendCleanNewTransferable(ref List<TransferableOneWay> rootTrans, IEnumerable<TransferableOneWay> newTrans)
	{
		foreach (TransferableOneWay newTran in newTrans)
		{
			if (rootTrans.Contains(newTran) && ((Transferable)newTran).CountToTransfer == 0)
			{
				rootTrans.Remove(newTran);
			}
			else if (!rootTrans.Contains(newTran) && ((Transferable)newTran).CountToTransfer != 0)
			{
				rootTrans.Add(newTran);
			}
		}
	}
}
