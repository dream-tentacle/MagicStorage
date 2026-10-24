using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HaulersDream;

public class FloatMenuOptionProvider_PlanRoute : FloatMenuOptionProvider
{
	public override bool Drafted => false;

	public override bool Undrafted => true;

	public override bool Multiselect => false;

	public override bool MechanoidCanDo => false;

	public override bool CanSelfTarget => false;

	public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
	{
		Pawn pawn = ((context != null) ? context.FirstSelectedPawn : null);
		List<Thing> things = ((context != null) ? context.ClickedThings : null);
		if (pawn == null || things == null || HaulersDreamMod.Settings == null || !HaulersDreamMod.Settings.planRoutes)
		{
			yield break;
		}
		HashSet<string> seenLabels = new HashSet<string>();
		for (int i = 0; i < things.Count; i++)
		{
			Thing val = things[i];
			if (val == null)
			{
				continue;
			}
			RouteWorkKind routeWorkKind = WorkKindResolver.Resolve(pawn, val);
			if (routeWorkKind == null || PlannerGate.HideForUnassigned(pawn, ((WorkGiver)(routeWorkKind.scanner?)).def?.workType))
			{
				continue;
			}
			string text = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.Option", NamedArgument.op_Implicit(routeWorkKind.gerund)));
			if (!seenLabels.Add(text))
			{
				continue;
			}
			Thing anchor = val;
			RouteWorkKind capturedKind = routeWorkKind;
			bool remember = HaulersDreamMod.Settings.rememberPlan && HaulersDreamMod.Settings.GetRememberedRoute(((Def)(anchor.def?)).defName) != null;
			string text2 = (remember ? TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanRoute.OptionRemembered", NamedArgument.op_Implicit(routeWorkKind.gerund))) : text);
			if (HaulersDreamMod.Settings.verboseLogging && routeWorkKind.scanner is WorkGiver_ConstructDeliverResourcesToBlueprints)
			{
				bool flag = RouteExecutor.BuildJobForStop(pawn, anchor, routeWorkKind) != null;
				string[] obj = new string[6]
				{
					"construct route offered for ",
					((Entity)anchor).LabelShort,
					" (def ",
					((Def)(anchor.def?)).defName,
					", ",
					null
				};
				ThingDef def = anchor.def;
				object arg = ((def != null) ? new bool?(def.IsBlueprint) : ((bool?)null));
				ThingDef def2 = anchor.def;
				obj[5] = $"blueprint={arg}, frame={((def2 != null) ? new bool?(def2.IsFrame) : ((bool?)null))}, deliverableNow={flag}).";
				HDLog.Dbg(string.Concat(obj));
			}
			FloatMenuOption val2 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text2, (Action)delegate
			{
				if (remember)
				{
					Dialog_PlanRoute.ExecuteRemembered(pawn, anchor, capturedKind, !KeyBindingDefOf.QueueOrder.IsDownEvent);
				}
				else
				{
					Find.WindowStack.Add((Window)(object)new Dialog_PlanRoute(pawn, anchor, capturedKind));
				}
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			{
				iconThing = anchor
			}, pawn, LocalTargetInfo.op_Implicit(anchor), "ReservedBy", (ReservationLayerDef)null);
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
}
