using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class FloatMenuOptionProvider_HaulNearby : FloatMenuOptionProvider
{
	public override bool Drafted => false;

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
		if (settings == null || !settings.bulkHaul || !settings.haulNearbyOption || !MapGate.HdActiveOnMap(((Thing)pawn).Map) || ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || HaulOrderGate.Blocks(pawn))
		{
			yield break;
		}
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if (val == null || !val.Spawned || val.def == null || (int)val.def.category != 2 || !val.def.EverHaulable || !HaulAIUtility.PawnCanAutomaticallyHaul(pawn, val, true))
			{
				continue;
			}
			Thing clickedLocal = val;
			FloatMenuOption val2 = new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("HaulersDream.HaulNearby.Option")), (Action)delegate
			{
				//IL_003a: Unknown result type (might be due to invalid IL or missing references)
				Job val3 = BulkHaul.BuildBulkJobForced(pawn, clickedLocal) ?? HaulAIUtility.HaulToStorageJob(pawn, clickedLocal, true);
				if (val3 == null)
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.HaulNearby.CouldNotStart")), LookTargets.op_Implicit(clickedLocal), MessageTypeDefOf.RejectInput, false);
				}
				else
				{
					val3.playerForced = true;
					pawn.jobs.TryTakeOrderedJob(val3, (JobTag?)(JobTag)0, false);
				}
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			{
				iconThing = val
			};
			yield return FloatMenuUtility.DecoratePrioritizedTask(val2, pawn, LocalTargetInfo.op_Implicit(val), "ReservedBy", (ReservationLayerDef)null);
			break;
		}
	}
}
