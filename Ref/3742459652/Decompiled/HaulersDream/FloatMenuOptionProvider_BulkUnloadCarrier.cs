using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class FloatMenuOptionProvider_BulkUnloadCarrier : FloatMenuOptionProvider
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
		if (settings == null || !settings.enableBulkUnloadCarriers || ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null || pawn.carryTracker?.innerContainer == null || ((ThingOwner)pawn.carryTracker.innerContainer).Count > 0 || HaulOrderGate.Blocks(pawn))
		{
			yield break;
		}
		for (int i = 0; i < list.Count; i++)
		{
			Thing obj = list[i];
			Pawn val = (Pawn)(object)((obj is Pawn) ? obj : null);
			if (val == null || VehicleFrameworkCompat.IsVehicle((Thing)(object)val) || val == pawn || val.inventory?.innerContainer == null || ((ThingOwner)val.inventory.innerContainer).Count == 0 || ((ThingWithComps)val).GetComp<CompMechCarrier>() != null || val.IsFreeColonist || !BulkUnloadGate.PlayerMayUnload(pawn, val) || !ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit((Thing)(object)val), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0) || !ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit((Thing)(object)val), 1, -1, (ReservationLayerDef)null, false))
			{
				continue;
			}
			Pawn carrierLocal = val;
			FloatMenuOption val2 = new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.UnloadCarrier.Option", NamedArgument.op_Implicit(((Entity)val).LabelShort))), (Action)delegate
			{
				//IL_000b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0041: Unknown result type (might be due to invalid IL or missing references)
				Job val3 = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_UnloadCarrierInBulk, LocalTargetInfo.op_Implicit((Thing)(object)carrierLocal));
				val3.playerForced = true;
				if (!pawn.jobs.TryTakeOrderedJob(val3, (JobTag?)(JobTag)0, false))
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.UnloadCarrier.CouldNotStart")), LookTargets.op_Implicit((Thing)(object)carrierLocal), MessageTypeDefOf.RejectInput, false);
				}
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			{
				iconThing = (Thing)(object)val
			};
			yield return FloatMenuUtility.DecoratePrioritizedTask(val2, pawn, LocalTargetInfo.op_Implicit((Thing)(object)val), "ReservedBy", (ReservationLayerDef)null);
			break;
		}
	}
}
