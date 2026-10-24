using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class FloatMenuOptionProvider_BulkLoadPackAnimal : FloatMenuOptionProvider
{
	public override bool Drafted => true;

	public override bool Undrafted => true;

	public override bool Multiselect => false;

	public override bool MechanoidCanDo => false;

	public override bool CanSelfTarget => false;

	public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
	{
		Pawn pawn = ((context != null) ? context.FirstSelectedPawn : null);
		List<Thing> list = ((context != null) ? context.ClickedThings : null);
		if (pawn == null || list == null || ((Thing)pawn).Map == null)
		{
			yield break;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.loadPackAnimalBulk || !settings.enableOnNonHomeMaps || CaravanFormingUtility.IsFormingCaravan(pawn) || ((Thing)pawn).Map.IsPlayerHome || ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
		{
			yield break;
		}
		Pawn val = GiveToPackAnimalUtility.UsablePackAnimalWithTheMostFreeSpace(pawn);
		if (val == null)
		{
			yield break;
		}
		for (int i = 0; i < list.Count; i++)
		{
			Thing val2 = list[i];
			if (val2 == null || val2.def == null || (int)val2.def.category != 2 || !val2.def.EverHaulable || !ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit(val2), (PathEndMode)3, (Danger)3, false, false, (TraverseMode)0))
			{
				continue;
			}
			Thing clickedLocal = val2;
			Pawn carrierLocal = val;
			FloatMenuOption val3 = new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.LoadPackAnimal.BulkOption")), (Action)delegate
			{
				//IL_0025: Unknown result type (might be due to invalid IL or missing references)
				Job val4 = PackAnimalLoad.TryBuildBulkLoadJob(pawn, clickedLocal, carrierLocal);
				if (val4 == null)
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.LoadPackAnimal.CouldNotStart")), LookTargets.op_Implicit(clickedLocal), MessageTypeDefOf.RejectInput, false);
				}
				else
				{
					val4.playerForced = true;
					pawn.jobs.TryTakeOrderedJob(val4, (JobTag?)(JobTag)0, false);
				}
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			{
				iconThing = val2
			};
			yield return FloatMenuUtility.DecoratePrioritizedTask(val3, pawn, LocalTargetInfo.op_Implicit(val2), "ReservedBy", (ReservationLayerDef)null);
			break;
		}
	}
}
