using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class FloatMenuOptionProvider_PlanCraft : FloatMenuOptionProvider
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
		if (pawn == null || things == null || HaulersDreamMod.Settings == null || !HaulersDreamMod.Settings.planCrafting)
		{
			yield break;
		}
		Pawn_Thinker thinker = pawn.thinker;
		if (((thinker != null) ? thinker.TryGetMainTreeThinkNode<JobGiver_Work>() : null) == null || CommonSenseCompat.BatchSuppressedByCommonSense)
		{
			yield break;
		}
		HashSet<Building_WorkTable> seen = new HashSet<Building_WorkTable>();
		for (int i = 0; i < things.Count; i++)
		{
			Thing obj = things[i];
			Building_WorkTable val = (Building_WorkTable)(object)((obj is Building_WorkTable) ? obj : null);
			if (val != null && seen.Add(val) && ((Thing)val).Spawned && ((Thing)val).Map == ((Thing)pawn).Map && BillRouteGate.MayRouteToInventory((Thing)(object)val) && WorkOverride.CanDoBillsAt(pawn, val) && val.CurrentlyUsableForBills() && CraftBatchPlanner.AnyBatchableBillForPawn(pawn, val) && ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit((Thing)(object)val), (PathEndMode)4, (Danger)3, false, false, (TraverseMode)0) && !WorkOverride.HidePlanCraftForUnassigned(pawn, val))
			{
				Building_WorkTable benchLocal = val;
				FloatMenuOption val2 = new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.Option")), (Action)delegate
				{
					Find.WindowStack.Add((Window)(object)new Dialog_PlanCraft(pawn, benchLocal));
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
				{
					iconThing = (Thing)(object)val
				};
				yield return FloatMenuUtility.DecoratePrioritizedTask(val2, pawn, LocalTargetInfo.op_Implicit((Thing)(object)val), "ReservedBy", (ReservationLayerDef)null);
			}
		}
	}
}
