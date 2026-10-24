using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HaulersDream;

public class Dialog_StorageBuildingFilter : Window
{
	private sealed class ModGroup
	{
		public string packageId;

		public string name;

		public List<ThingDef> buildings;
	}

	private readonly StorageBuildingFilter filter;

	private readonly HashSet<string> denied;

	private readonly HashSet<string> allowed;

	private readonly QuickSearchWidget search = new QuickSearchWidget();

	private readonly HashSet<string> expanded = new HashSet<string>();

	private Vector2 scroll;

	private float viewHeight = 500f;

	private static List<ModGroup> modGroups;

	private const float RowHeight = 28f;

	private const float SectionHeight = 28f;

	private const float Indent = 16f;

	private const float StateBtnWidth = 120f;

	public override Vector2 InitialSize => new Vector2(640f, 720f);

	public Dialog_StorageBuildingFilter(StorageBuildingFilter filter)
		: base((IWindowDrawing)null)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		this.filter = filter;
		denied = new HashSet<string>(filter?.denied ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);
		allowed = new HashSet<string>(filter?.allowed ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);
		base.doCloseX = true;
		base.doCloseButton = true;
		base.closeOnClickedOutside = true;
		base.absorbInputAroundWindow = true;
		base.forcePause = true;
		EnsureGroups();
	}

	private static void EnsureGroups()
	{
		if (modGroups != null)
		{
			return;
		}
		Dictionary<string, ModGroup> dictionary = new Dictionary<string, ModGroup>(StringComparer.OrdinalIgnoreCase);
		List<ThingDef> allDefsListForReading = DefDatabase<ThingDef>.AllDefsListForReading;
		for (int i = 0; i < allDefsListForReading.Count; i++)
		{
			ThingDef val = allDefsListForReading[i];
			if (StorageBuildingFilter.IsStorageBuildingDef(val))
			{
				ModContentPack modContentPack = ((Def)val).modContentPack;
				string text = ((modContentPack != null) ? modContentPack.PackageId : null) ?? string.Empty;
				if (!dictionary.TryGetValue(text, out var value))
				{
					value = (dictionary[text] = new ModGroup
					{
						packageId = text,
						name = (((modContentPack != null) ? modContentPack.Name : null) ?? text),
						buildings = new List<ThingDef>()
					});
				}
				value.buildings.Add(val);
			}
		}
		modGroups = new List<ModGroup>(dictionary.Values);
		foreach (ModGroup modGroup2 in modGroups)
		{
			modGroup2.buildings.Sort((ThingDef a, ThingDef b) => string.Compare(((Def)a).label ?? ((Def)a).defName, ((Def)b).label ?? ((Def)b).defName, StringComparison.OrdinalIgnoreCase));
		}
		modGroups.Sort(delegate(ModGroup a, ModGroup b)
		{
			bool flag = a.packageId == "ludeon.rimworld";
			bool flag2 = b.packageId == "ludeon.rimworld";
			return (flag != flag2) ? ((!flag) ? 1 : (-1)) : string.Compare(a.name ?? a.packageId, b.name ?? b.packageId, StringComparison.OrdinalIgnoreCase);
		});
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
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		float y = ((Rect)(ref inRect)).y;
		Text.Font = (GameFont)2;
		Widgets.Label(new Rect(((Rect)(ref inRect)).x, y, ((Rect)(ref inRect)).width, 34f), Translator.Translate("HaulersDream.StorageFilter.Title"));
		y += 38f;
		Text.Font = (GameFont)1;
		string text = TaggedString.op_Implicit(Translator.Translate("HaulersDream.StorageFilter.Desc"));
		float num = Text.CalcHeight(text, ((Rect)(ref inRect)).width);
		Widgets.Label(new Rect(((Rect)(ref inRect)).x, y, ((Rect)(ref inRect)).width, num), text);
		y += num + 6f;
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(((Rect)(ref inRect)).x, y, ((Rect)(ref inRect)).width - 130f, 26f);
		search.OnGUI(val, (Action)null, (Action)null);
		if (Widgets.ButtonText(new Rect(((Rect)(ref val)).xMax + 6f, y, 124f, 26f), TaggedString.op_Implicit(Translator.Translate("HaulersDream.StorageFilter.ResetAll")), true, true, true, (TextAnchor?)null))
		{
			denied.Clear();
			allowed.Clear();
		}
		y += 30f;
		float num2 = Window.CloseButSize.y + 10f;
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(((Rect)(ref inRect)).x, y, ((Rect)(ref inRect)).width, ((Rect)(ref inRect)).yMax - y - num2);
		Rect val3 = default(Rect);
		((Rect)(ref val3))._002Ector(0f, 0f, ((Rect)(ref val2)).width - 16f, viewHeight);
		Widgets.BeginScrollView(val2, ref scroll, val3, true);
		float curY = 0f;
		for (int i = 0; i < modGroups.Count; i++)
		{
			DrawModGroup(modGroups[i], ((Rect)(ref val3)).width, ref curY);
		}
		Widgets.EndScrollView();
		viewHeight = curY;
	}

	private void DrawModGroup(ModGroup grp, float width, ref float curY)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		bool active = search.filter.Active;
		if (active && !SectionMatches(grp))
		{
			return;
		}
		bool flag = active || expanded.Contains(grp.packageId);
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, curY, width, 28f);
		if (Mouse.IsOver(val))
		{
			Widgets.DrawHighlight(val);
		}
		if (!active && Widgets.ButtonImage(new Rect(0f, curY + 5f, 18f, 18f), flag ? TexButton.Collapse : TexButton.Reveal, true, (string)null))
		{
			ToggleSection(grp.packageId, flag);
		}
		Rect rect = default(Rect);
		((Rect)(ref rect))._002Ector(width - 120f - 2f, curY + 2f, 120f, 24f);
		DrawTriState(rect, grp.packageId, isMod: true);
		Text.Anchor = (TextAnchor)3;
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(22f, curY, ((Rect)(ref rect)).x - 22f - 6f, 28f);
		Widgets.Label(val2, grp.name);
		Text.Anchor = (TextAnchor)0;
		if (!active && Widgets.ButtonInvisible(val2, true))
		{
			ToggleSection(grp.packageId, flag);
		}
		curY += 28f;
		if (!flag)
		{
			return;
		}
		for (int i = 0; i < grp.buildings.Count; i++)
		{
			ThingDef val3 = grp.buildings[i];
			if (!active || search.filter.Matches(((Def)val3).label ?? ((Def)val3).defName))
			{
				DrawBuildingRow(val3, width, ref curY);
			}
		}
	}

	private void DrawBuildingRow(ThingDef def, float width, ref float curY)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, curY, width, 28f);
		if (Mouse.IsOver(val))
		{
			Widgets.DrawHighlight(val);
		}
		float num = 20f;
		Widgets.DefIcon(new Rect(num, curY + 2f, 24f, 24f), (Def)(object)def, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
		Rect rect = default(Rect);
		((Rect)(ref rect))._002Ector(width - 120f - 2f, curY + 2f, 120f, 24f);
		DrawTriState(rect, ((Def)def).defName, isMod: false);
		Text.Anchor = (TextAnchor)3;
		Widgets.Label(new Rect(num + 28f, curY, Math.Max(40f, ((Rect)(ref rect)).x - (num + 28f) - 6f), 28f), ((Def)def).LabelCap);
		Text.Anchor = (TextAnchor)0;
		if (Mouse.IsOver(val) && !GenText.NullOrEmpty(((Def)def).description))
		{
			TooltipHandler.TipRegion(val, TipSignal.op_Implicit(((Def)def).description));
		}
		curY += 28f;
	}

	private void DrawTriState(Rect rect, string key, bool isMod)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		bool flag = denied.Contains(key);
		bool flag2 = allowed.Contains(key);
		string text = TaggedString.op_Implicit(flag ? Translator.Translate("HaulersDream.StorageFilter.StateDeny") : (flag2 ? Translator.Translate("HaulersDream.StorageFilter.StateAllow") : Translator.Translate("HaulersDream.StorageFilter.StateDefault")));
		if (Widgets.ButtonText(rect, text, true, true, true, (TextAnchor?)null))
		{
			SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_Tiny, (Map)null);
			if (!flag2 && !flag)
			{
				allowed.Add(key);
			}
			else if (flag2)
			{
				allowed.Remove(key);
				denied.Add(key);
			}
			else
			{
				denied.Remove(key);
			}
		}
	}

	private void ToggleSection(string packageId, bool currentlyOpen)
	{
		if (currentlyOpen)
		{
			expanded.Remove(packageId);
		}
		else
		{
			expanded.Add(packageId);
		}
		SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, (Map)null);
	}

	private bool SectionMatches(ModGroup grp)
	{
		for (int i = 0; i < grp.buildings.Count; i++)
		{
			if (search.filter.Matches(((Def)grp.buildings[i]).label ?? ((Def)grp.buildings[i]).defName))
			{
				return true;
			}
		}
		return false;
	}

	public override void PreClose()
	{
		((Window)this).PreClose();
		if (filter != null)
		{
			filter.denied.Clear();
			foreach (string item in denied)
			{
				filter.denied.Add(item);
			}
			filter.allowed.Clear();
			foreach (string item2 in allowed)
			{
				filter.allowed.Add(item2);
			}
		}
		HaulersDreamMod instance = HaulersDreamMod.Instance;
		if (instance != null)
		{
			((Mod)instance).WriteSettings();
		}
	}
}
