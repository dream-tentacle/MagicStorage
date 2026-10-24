using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HaulersDream;

public class Dialog_ItemUnloadSettings : Window
{
	private enum CatAgg
	{
		Default,
		KeepAll,
		KeepAtMost,
		UnloadAlways,
		Mixed
	}

	private readonly HaulersDreamSettings settings;

	private readonly Dictionary<string, ItemUnloadRule> working;

	private readonly Dictionary<string, string> amountBuffers = new Dictionary<string, string>();

	private readonly HashSet<ThingCategoryDef> expanded = new HashSet<ThingCategoryDef>();

	private readonly QuickSearchWidget search = new QuickSearchWidget();

	private Vector2 scroll;

	private float viewHeight = 500f;

	private readonly Dictionary<ThingCategoryDef, CatAgg> catAggCache = new Dictionary<ThingCategoryDef, CatAgg>();

	private bool catAggDirty = true;

	private static Dictionary<ThingCategoryDef, List<ThingDef>> eligibleDirect;

	private static HashSet<ThingCategoryDef> hasEligibleDescendant;

	private static Dictionary<ThingCategoryDef, List<ThingDef>> subtreeDefs;

	private const float RowHeight = 28f;

	private const float CatHeight = 26f;

	private const float Indent = 14f;

	private const float ModeBtnWidth = 132f;

	private const float AmountWidth = 58f;

	private const int DefaultKeepAtMost = 25;

	public override Vector2 InitialSize => new Vector2(640f, 720f);

	public Dialog_ItemUnloadSettings(HaulersDreamSettings settings)
		: base((IWindowDrawing)null)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		this.settings = settings;
		working = settings?.GetItemRulesCopy() ?? new Dictionary<string, ItemUnloadRule>();
		base.doCloseX = true;
		base.doCloseButton = true;
		base.closeOnClickedOutside = true;
		base.absorbInputAroundWindow = true;
		base.forcePause = true;
		EnsureCaches();
	}

	private static void EnsureCaches()
	{
		if (eligibleDirect == null)
		{
			eligibleDirect = new Dictionary<ThingCategoryDef, List<ThingDef>>();
			hasEligibleDescendant = new HashSet<ThingCategoryDef>();
			subtreeDefs = new Dictionary<ThingCategoryDef, List<ThingDef>>();
			TreeNode_ThingCategory rootNode = ThingCategoryNodeDatabase.RootNode;
			if (rootNode?.catDef != null)
			{
				BuildCaches(rootNode.catDef);
			}
		}
	}

	private static bool IsEligible(ThingDef def)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Invalid comparison between Unknown and I4
		if (def != null && (int)def.category == 2)
		{
			return !def.IsCorpse;
		}
		return false;
	}

	private static bool BuildCaches(ThingCategoryDef cat)
	{
		if (cat == null)
		{
			return false;
		}
		List<ThingDef> list = new List<ThingDef>();
		if (cat.childThingDefs != null)
		{
			foreach (ThingDef childThingDef in cat.childThingDefs)
			{
				if (IsEligible(childThingDef))
				{
					list.Add(childThingDef);
				}
			}
		}
		list.Sort((ThingDef a, ThingDef b) => string.Compare(((Def)a).label ?? ((Def)a).defName, ((Def)b).label ?? ((Def)b).defName, StringComparison.OrdinalIgnoreCase));
		eligibleDirect[cat] = list;
		List<ThingDef> list2 = new List<ThingDef>(list);
		bool flag = list.Count > 0;
		if (cat.childCategories != null)
		{
			foreach (ThingCategoryDef childCategory in cat.childCategories)
			{
				flag |= BuildCaches(childCategory);
				if (subtreeDefs.TryGetValue(childCategory, out var value))
				{
					list2.AddRange(value);
				}
			}
		}
		subtreeDefs[cat] = list2;
		if (flag)
		{
			hasEligibleDescendant.Add(cat);
		}
		return flag;
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		float y = ((Rect)(ref inRect)).y;
		Text.Font = (GameFont)2;
		Widgets.Label(new Rect(((Rect)(ref inRect)).x, y, ((Rect)(ref inRect)).width, 34f), Translator.Translate("HaulersDream.ItemUnload.Title"));
		y += 38f;
		Text.Font = (GameFont)1;
		string text = TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.Desc"));
		float num = Text.CalcHeight(text, ((Rect)(ref inRect)).width);
		Widgets.Label(new Rect(((Rect)(ref inRect)).x, y, ((Rect)(ref inRect)).width, num), text);
		y += num + 6f;
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(((Rect)(ref inRect)).x, y, ((Rect)(ref inRect)).width - 130f, 26f);
		search.OnGUI(val, (Action)null, (Action)null);
		if (Widgets.ButtonText(new Rect(((Rect)(ref val)).xMax + 6f, y, 124f, 26f), TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ClearAll")), true, true, true, (TextAnchor?)null))
		{
			List<string> list = new List<string>();
			foreach (string key in working.Keys)
			{
				if (DefDatabase<ThingDef>.GetNamedSilentFail(key) != null)
				{
					list.Add(key);
				}
			}
			foreach (string item in list)
			{
				working.Remove(item);
			}
			amountBuffers.Clear();
			catAggDirty = true;
		}
		y += 30f;
		float num2 = Window.CloseButSize.y + 10f;
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(((Rect)(ref inRect)).x, y, ((Rect)(ref inRect)).width, ((Rect)(ref inRect)).yMax - y - num2);
		Rect val3 = default(Rect);
		((Rect)(ref val3))._002Ector(0f, 0f, ((Rect)(ref val2)).width - 16f, viewHeight);
		Widgets.BeginScrollView(val2, ref scroll, val3, true);
		float curY = 0f;
		TreeNode_ThingCategory rootNode = ThingCategoryNodeDatabase.RootNode;
		if (rootNode != null)
		{
			DrawCategoryChildren(rootNode, 0, ((Rect)(ref val3)).width, ref curY);
		}
		Widgets.EndScrollView();
		viewHeight = curY;
	}

	private void DrawCategoryChildren(TreeNode_ThingCategory node, int depth, float width, ref float curY)
	{
		foreach (TreeNode_ThingCategory childCategoryNode in node.ChildCategoryNodes)
		{
			ThingCategoryDef catDef = childCategoryNode.catDef;
			if (catDef != null && hasEligibleDescendant != null && hasEligibleDescendant.Contains(catDef) && (!search.filter.Active || SubtreeMatches(catDef)))
			{
				DrawCategory(childCategoryNode, depth, width, ref curY);
			}
		}
		if (eligibleDirect == null || node.catDef == null || !eligibleDirect.TryGetValue(node.catDef, out var value))
		{
			return;
		}
		foreach (ThingDef item in value)
		{
			if (!search.filter.Active || search.filter.Matches(((Def)item).label))
			{
				DrawThingRow(item, depth, width, ref curY);
			}
		}
	}

	private void DrawCategory(TreeNode_ThingCategory node, int depth, float width, ref float curY)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		ThingCategoryDef catDef = node.catDef;
		bool flag = search.filter.Active || expanded.Contains(catDef);
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, curY, width, 26f);
		if (Mouse.IsOver(val))
		{
			Widgets.DrawHighlight(val);
		}
		float num = (float)depth * 14f;
		if (!search.filter.Active && Widgets.ButtonImage(new Rect(num, curY + 4f, 18f, 18f), flag ? TexButton.Collapse : TexButton.Reveal, true, (string)null))
		{
			ToggleCategory(catDef, flag);
		}
		float val2 = width - num - 22f;
		if (!search.filter.Active)
		{
			Rect val3 = default(Rect);
			((Rect)(ref val3))._002Ector(width - 132f - 2f, curY + 1f, 132f, 24f);
			TooltipHandler.TipRegion(val3, TipSignal.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.SetCategoryTip")));
			if (Widgets.ButtonText(val3, CatAggLabel(GetCatAgg(catDef)), true, true, true, (TextAnchor?)null))
			{
				OpenCategoryMenu(catDef);
			}
			val2 = ((Rect)(ref val3)).x - 6f - (num + 22f);
		}
		Text.Anchor = (TextAnchor)3;
		Rect val4 = default(Rect);
		((Rect)(ref val4))._002Ector(num + 22f, curY, Math.Max(40f, val2), 26f);
		Widgets.Label(val4, node.LabelCap);
		Text.Anchor = (TextAnchor)0;
		if (!search.filter.Active && Widgets.ButtonInvisible(val4, true))
		{
			ToggleCategory(catDef, flag);
		}
		curY += 26f;
		if (flag)
		{
			DrawCategoryChildren(node, depth + 1, width, ref curY);
		}
	}

	private void ToggleCategory(ThingCategoryDef cat, bool currentlyOpen)
	{
		if (currentlyOpen)
		{
			expanded.Remove(cat);
		}
		else
		{
			expanded.Add(cat);
		}
		SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, (Map)null);
	}

	private void DrawThingRow(ThingDef def, int depth, float width, ref float curY)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, curY, width, 28f);
		if (Mouse.IsOver(val))
		{
			Widgets.DrawHighlight(val);
		}
		float num = (float)depth * 14f + 4f;
		Widgets.DefIcon(new Rect(num, curY + 2f, 24f, 24f), (Def)(object)def, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
		ItemUnloadRule value;
		bool flag = working.TryGetValue(((Def)def).defName, out value);
		bool num2 = flag && value.mode == ItemUnloadMode.KeepAtMost;
		float num3 = width - 2f;
		Rect val2 = default(Rect);
		if (num2)
		{
			((Rect)(ref val2))._002Ector(num3 - 58f, curY + 3f, 58f, 22f);
			num3 = ((Rect)(ref val2)).x - 6f;
		}
		Rect val3 = default(Rect);
		((Rect)(ref val3))._002Ector(num3 - 132f, curY + 2f, 132f, 24f);
		float num4 = ((Rect)(ref val3)).x - 6f;
		Text.Anchor = (TextAnchor)3;
		Widgets.Label(new Rect(num + 28f, curY, Math.Max(40f, num4 - (num + 28f)), 28f), ((Def)def).LabelCap);
		Text.Anchor = (TextAnchor)0;
		if (Mouse.IsOver(val) && !GenText.NullOrEmpty(((Def)def).description))
		{
			TooltipHandler.TipRegion(val, TipSignal.op_Implicit(((Def)def).description));
		}
		if (Widgets.ButtonText(val3, ModeLabel(flag, value), true, true, true, (TextAnchor?)null))
		{
			OpenModeMenu(def);
		}
		if (num2)
		{
			int amount = value.amount;
			string value3;
			string value2 = (amountBuffers.TryGetValue(((Def)def).defName, out value3) ? value3 : amount.ToString());
			Widgets.TextFieldNumeric<int>(val2, ref amount, ref value2, 0f, 999999f);
			amountBuffers[((Def)def).defName] = value2;
			if (amount != value.amount)
			{
				value.amount = amount;
				PutRule(((Def)def).defName, value);
			}
		}
		curY += 28f;
	}

	private static string ModeLabel(bool has, ItemUnloadRule rule)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (!has)
		{
			return TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeDefault"));
		}
		return rule.mode switch
		{
			ItemUnloadMode.KeepAll => TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeKeepAll")), 
			ItemUnloadMode.KeepAtMost => TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeKeepAtMost")), 
			ItemUnloadMode.UnloadAlways => TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeUnloadAlways")), 
			_ => TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeDefault")), 
		};
	}

	private void OpenModeMenu(ThingDef def)
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
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Expected O, but got Unknown
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Expected O, but got Unknown
		string name = ((Def)def).defName;
		List<FloatMenuOption> list = new List<FloatMenuOption>
		{
			new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeDefault")), (Action)delegate
			{
				DropRule(name);
				amountBuffers.Remove(name);
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
			new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeKeepAll")), (Action)delegate
			{
				PutRule(name, new ItemUnloadRule(ItemUnloadMode.KeepAll));
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
			new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeKeepAtMost")), (Action)delegate
			{
				ItemUnloadRule value;
				int amount = ((working.TryGetValue(name, out value) && value.amount > 0) ? value.amount : 25);
				PutRule(name, new ItemUnloadRule(ItemUnloadMode.KeepAtMost, amount));
				amountBuffers.Remove(name);
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
			new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeUnloadAlways")), (Action)delegate
			{
				PutRule(name, new ItemUnloadRule(ItemUnloadMode.UnloadAlways));
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
		};
		Find.WindowStack.Add((Window)new FloatMenu(list));
	}

	private void OpenCategoryMenu(ThingCategoryDef cat)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected O, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Expected O, but got Unknown
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Expected O, but got Unknown
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Expected O, but got Unknown
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Expected O, but got Unknown
		List<FloatMenuOption> list = new List<FloatMenuOption>
		{
			new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeDefault")), (Action)delegate
			{
				ApplyToCategory(cat, isDefault: true, default(ItemUnloadRule));
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
			new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeKeepAll")), (Action)delegate
			{
				ApplyToCategory(cat, isDefault: false, new ItemUnloadRule(ItemUnloadMode.KeepAll));
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
			new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeKeepAtMost")), (Action)delegate
			{
				ApplyToCategory(cat, isDefault: false, new ItemUnloadRule(ItemUnloadMode.KeepAtMost, 25));
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
			new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeUnloadAlways")), (Action)delegate
			{
				ApplyToCategory(cat, isDefault: false, new ItemUnloadRule(ItemUnloadMode.UnloadAlways));
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
		};
		Find.WindowStack.Add((Window)new FloatMenu(list));
	}

	private void ApplyToCategory(ThingCategoryDef cat, bool isDefault, ItemUnloadRule rule)
	{
		if (cat == null || subtreeDefs == null || !subtreeDefs.TryGetValue(cat, out var value))
		{
			return;
		}
		foreach (ThingDef item in value)
		{
			string defName = ((Def)item).defName;
			if (isDefault)
			{
				working.Remove(defName);
			}
			else
			{
				working[defName] = new ItemUnloadRule(rule.mode, rule.amount);
			}
			amountBuffers.Remove(defName);
		}
		catAggDirty = true;
		SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, (Map)null);
	}

	private void PutRule(string defName, ItemUnloadRule rule)
	{
		working[defName] = rule;
		catAggDirty = true;
	}

	private void DropRule(string defName)
	{
		working.Remove(defName);
		catAggDirty = true;
	}

	private CatAgg GetCatAgg(ThingCategoryDef cat)
	{
		if (catAggDirty)
		{
			catAggCache.Clear();
			catAggDirty = false;
		}
		if (catAggCache.TryGetValue(cat, out var value))
		{
			return value;
		}
		value = ComputeCatAgg(cat);
		catAggCache[cat] = value;
		return value;
	}

	private CatAgg ComputeCatAgg(ThingCategoryDef cat)
	{
		if (subtreeDefs == null || !subtreeDefs.TryGetValue(cat, out var value) || value.Count == 0)
		{
			return CatAgg.Default;
		}
		bool flag = true;
		bool flag2 = false;
		ItemUnloadRule itemUnloadRule = default(ItemUnloadRule);
		foreach (ThingDef item in value)
		{
			ItemUnloadRule value2;
			bool flag3 = working.TryGetValue(((Def)item).defName, out value2);
			if (flag)
			{
				flag = false;
				flag2 = flag3;
				itemUnloadRule = value2;
				continue;
			}
			if (flag3 != flag2)
			{
				return CatAgg.Mixed;
			}
			if (!flag3 || (value2.mode == itemUnloadRule.mode && (value2.mode != ItemUnloadMode.KeepAtMost || value2.amount == itemUnloadRule.amount)))
			{
				continue;
			}
			return CatAgg.Mixed;
		}
		if (!flag2)
		{
			return CatAgg.Default;
		}
		return itemUnloadRule.mode switch
		{
			ItemUnloadMode.KeepAll => CatAgg.KeepAll, 
			ItemUnloadMode.KeepAtMost => CatAgg.KeepAtMost, 
			ItemUnloadMode.UnloadAlways => CatAgg.UnloadAlways, 
			_ => CatAgg.Default, 
		};
	}

	private static string CatAggLabel(CatAgg agg)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		return agg switch
		{
			CatAgg.KeepAll => TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeKeepAll")), 
			CatAgg.KeepAtMost => TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeKeepAtMost")), 
			CatAgg.UnloadAlways => TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeUnloadAlways")), 
			CatAgg.Mixed => TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeMixed")), 
			_ => TaggedString.op_Implicit(Translator.Translate("HaulersDream.ItemUnload.ModeDefault")), 
		};
	}

	private bool SubtreeMatches(ThingCategoryDef cat)
	{
		if (cat == null || hasEligibleDescendant == null || !hasEligibleDescendant.Contains(cat))
		{
			return false;
		}
		if (eligibleDirect.TryGetValue(cat, out var value))
		{
			foreach (ThingDef item in value)
			{
				if (search.filter.Matches(((Def)item).label))
				{
					return true;
				}
			}
		}
		if (cat.childCategories != null)
		{
			foreach (ThingCategoryDef childCategory in cat.childCategories)
			{
				if (SubtreeMatches(childCategory))
				{
					return true;
				}
			}
		}
		return false;
	}

	public override void PreClose()
	{
		((Window)this).PreClose();
		settings?.SetItemRules(working);
		HaulersDreamMod instance = HaulersDreamMod.Instance;
		if (instance != null)
		{
			((Mod)instance).WriteSettings();
		}
	}
}
