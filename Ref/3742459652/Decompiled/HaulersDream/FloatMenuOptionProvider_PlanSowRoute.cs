using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class FloatMenuOptionProvider_PlanSowRoute : FloatMenuOptionProvider
{
	public override bool Drafted => false;

	public override bool Undrafted => true;

	public override bool Multiselect => false;

	public override bool MechanoidCanDo => false;

	public override bool CanSelfTarget => false;

	public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
	{
		Pawn pawn = ((context != null) ? context.FirstSelectedPawn : null);
		Pawn obj = pawn;
		if (((obj != null) ? ((Thing)obj).Map : null) == null || HaulersDreamMod.Settings == null || !HaulersDreamMod.Settings.planRoutes || WorkCapabilityProbe.IsDisabled(pawn, WorkTypeDefOf.Growing) || PlannerGate.HideForUnassigned(pawn, WorkTypeDefOf.Growing))
		{
			yield break;
		}
		Pawn_Thinker thinker = pawn.thinker;
		if (((thinker != null) ? thinker.TryGetMainTreeThinkNode<JobGiver_Work>() : null) == null)
		{
			yield break;
		}
		IntVec3 clickedCell = context.ClickedCell;
		if (!((IntVec3)(ref clickedCell)).IsValid || !GenGrid.InBounds(clickedCell, ((Thing)pawn).Map) || ((Thing)pawn).Map.fogGrid.IsFogged(clickedCell))
		{
			yield break;
		}
		Zone zone = GridsUtility.GetZone(clickedCell, ((Thing)pawn).Map);
		Zone_Growing zone2 = (Zone_Growing)(object)((zone is Zone_Growing) ? zone : null);
		if (zone2 == null || !zone2.allowSow)
		{
			yield break;
		}
		ThingDef plantDefToGrow = zone2.GetPlantDefToGrow();
		if (plantDefToGrow?.plant == null)
		{
			yield break;
		}
		int sowMinSkill = plantDefToGrow.plant.sowMinSkill;
		if ((sowMinSkill > 0 && ((pawn.skills != null && pawn.skills.GetSkill(SkillDefOf.Plants).Level < sowMinSkill) || (pawn.IsColonyMech && pawn.RaceProps.mechFixedSkillLevel < sowMinSkill))) || !TryFindAnchor(pawn, clickedCell, zone2, plantDefToGrow, out var anchor))
		{
			yield break;
		}
		string text = SowGerund();
		SowRouteTemplate template = HaulersDreamMod.Settings.GetRememberedSowRoute(((Def)plantDefToGrow).defName);
		bool remember = HaulersDreamMod.Settings.rememberPlan && template != null;
		FloatMenuOption val = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(TaggedString.op_Implicit(remember ? TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.OptionRemembered", NamedArgument.op_Implicit(text)) : TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Option", NamedArgument.op_Implicit(text))), (Action)delegate
		{
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			if (remember)
			{
				Dialog_PlanSowRoute.ExecuteRemembered(pawn, anchor, template, !KeyBindingDefOf.QueueOrder.IsDownEvent);
			}
			else
			{
				Find.WindowStack.Add((Window)(object)new Dialog_PlanSowRoute(pawn, anchor, zone2));
			}
		}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0), pawn, LocalTargetInfo.op_Implicit(anchor), "ReservedBy", (ReservationLayerDef)null);
		Action<Rect> prevHover = val.mouseoverGuiAction;
		val.mouseoverGuiAction = delegate(Rect r)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			prevHover?.Invoke(r);
			UIHighlighter.HighlightTag("HaulersDream.RememberPlan");
		};
		yield return val;
	}

	private static bool TryFindAnchor(Pawn pawn, IntVec3 clicked, Zone_Growing zone, ThingDef plantDef, out IntVec3 anchor)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		anchor = clicked;
		if (SowRouteSelection.IsSowableCell(pawn, clicked, zone, plantDef))
		{
			return true;
		}
		List<IntVec3> cells = ((Zone)zone).cells;
		CellIndices cellIndices = ((Thing)pawn).Map.cellIndices;
		bool result = false;
		int num = int.MaxValue;
		for (int i = 0; i < cells.Count; i++)
		{
			IntVec3 val = cells[i];
			if (SowRouteSelection.IsSowableCell(pawn, val, zone, plantDef))
			{
				int num2 = ((CellIndices)(ref cellIndices)).CellToIndex(val);
				if (num2 < num)
				{
					num = num2;
					anchor = val;
					result = true;
				}
			}
		}
		return result;
	}

	internal static string SowGerund()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		string text = ((WorkGiver)(SowRouteExecutor.SowScanner()?)).def?.gerund;
		if (!GenText.NullOrEmpty(text))
		{
			return text;
		}
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.SowingGerundFallback"));
	}
}
