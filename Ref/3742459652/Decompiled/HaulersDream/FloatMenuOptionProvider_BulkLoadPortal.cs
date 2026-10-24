using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class FloatMenuOptionProvider_BulkLoadPortal : FloatMenuOptionProvider
{
	public override bool Drafted => true;

	public override bool Undrafted => true;

	public override bool Multiselect => false;

	public override bool MechanoidCanDo => false;

	public override bool CanSelfTarget => false;

	public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
	{
		Pawn val = ((context != null) ? context.FirstSelectedPawn : null);
		List<Thing> list = ((context != null) ? context.ClickedThings : null);
		if (val == null || list == null || ((Thing)val).Map == null)
		{
			yield break;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.enableBulkLoadPortal || ((ThingWithComps)val).GetComp<CompHauledToInventory>() == null || val.inventory == null || HaulOrderGate.Blocks(val) || val.mindState?.duty?.def == DutyDefOf.LoadAndEnterPortal)
		{
			yield break;
		}
		for (int i = 0; i < list.Count; i++)
		{
			Thing val2 = list[i];
			MapPortal val3 = (MapPortal)(object)((val2 is MapPortal) ? val2 : null);
			if (val3 == null || !val3.LoadInProgress || !ReachabilityUtility.CanReach(val, LocalTargetInfo.op_Implicit(val2), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0) || !ReservationUtility.CanReserve(val, LocalTargetInfo.op_Implicit(val2), 1, -1, (ReservationLayerDef)null, false))
			{
				continue;
			}
			if (val.CurJobDef == HaulersDreamDefOf.HaulersDream_LoadPortalInBulk)
			{
				LocalTargetInfo target = val.CurJob.GetTarget((TargetIndex)1);
				if ((object)((LocalTargetInfo)(ref target)).Thing == val3)
				{
					continue;
				}
			}
			MapPortalBulkTarget mapPortalBulkTarget = MapPortalBulkTarget.TryCreate(val3);
			if (mapPortalBulkTarget == null)
			{
				continue;
			}
			Pawn pawnLocal = val;
			MapPortalBulkTarget adapterLocal = mapPortalBulkTarget;
			Thing clickedLocal = val2;
			FloatMenuOption val4 = new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.LoadPortal.Option", NamedArgument.op_Implicit(((Entity)val2).LabelShort))), (Action)delegate
			{
				//IL_001b: Unknown result type (might be due to invalid IL or missing references)
				Job val5 = TransportLoad.TryGiveBulkJob(pawnLocal, adapterLocal, playerOrder: true);
				if (val5 == null)
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.LoadPortal.CouldNotStart")), LookTargets.op_Implicit(clickedLocal), MessageTypeDefOf.RejectInput, false);
				}
				else
				{
					val5.playerForced = true;
					pawnLocal.jobs.TryTakeOrderedJob(val5, (JobTag?)(JobTag)0, false);
				}
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			{
				iconThing = val2
			};
			yield return FloatMenuUtility.DecoratePrioritizedTask(val4, val, LocalTargetInfo.op_Implicit(val2), "ReservedBy", (ReservationLayerDef)null);
			break;
		}
	}
}
