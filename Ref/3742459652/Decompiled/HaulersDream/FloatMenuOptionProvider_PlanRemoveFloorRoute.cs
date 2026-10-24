using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class FloatMenuOptionProvider_PlanRemoveFloorRoute : FloatMenuOptionProvider
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
		if (((obj != null) ? ((Thing)obj).Map : null) == null || HaulersDreamMod.Settings == null || !HaulersDreamMod.Settings.planRoutes || WorkCapabilityProbe.IsDisabled(pawn, WorkTypeDefOf.Construction) || PlannerGate.HideForUnassigned(pawn, WorkTypeDefOf.Construction))
		{
			yield break;
		}
		Pawn_Thinker thinker = pawn.thinker;
		if (((thinker != null) ? thinker.TryGetMainTreeThinkNode<JobGiver_Work>() : null) == null)
		{
			yield break;
		}
		IntVec3 clickedCell = context.ClickedCell;
		if (!((IntVec3)(ref clickedCell)).IsValid || !GenGrid.InBounds(clickedCell, ((Thing)pawn).Map) || ((Thing)pawn).Map.fogGrid.IsFogged(clickedCell) || !RemoveFloorRouteSelection.IsRemovableFloorCell(pawn, clickedCell))
		{
			yield break;
		}
		IntVec3 anchor = clickedCell;
		string text = TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanRoute.RemoveFloorGerund"));
		TerrainDef val = ((Thing)pawn).Map.terrainGrid.TerrainAt(anchor);
		RemoveFloorRouteTemplate template = HaulersDreamMod.Settings.GetRememberedRemoveFloorRoute(((Def)(val?)).defName);
		bool remember = HaulersDreamMod.Settings.rememberPlan && template != null;
		FloatMenuOption val2 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(TaggedString.op_Implicit(remember ? TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.OptionRemembered", NamedArgument.op_Implicit(text)) : TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Option", NamedArgument.op_Implicit(text))), (Action)delegate
		{
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			if (remember)
			{
				Dialog_PlanRemoveFloorRoute.ExecuteRemembered(pawn, anchor, template, !KeyBindingDefOf.QueueOrder.IsDownEvent);
			}
			else
			{
				Find.WindowStack.Add((Window)(object)new Dialog_PlanRemoveFloorRoute(pawn, anchor));
			}
		}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0), pawn, LocalTargetInfo.op_Implicit(anchor), "ReservedBy", (ReservationLayerDef)null);
		Action<Rect> prevHover = val2.mouseoverGuiAction;
		val2.mouseoverGuiAction = delegate(Rect r)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			prevHover?.Invoke(r);
			UIHighlighter.HighlightTag("HaulersDream.RememberPlan");
		};
		yield return val2;
	}
}
