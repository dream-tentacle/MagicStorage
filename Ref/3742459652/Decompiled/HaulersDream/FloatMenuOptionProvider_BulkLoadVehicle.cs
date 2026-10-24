using System;
using System.Collections;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class FloatMenuOptionProvider_BulkLoadVehicle : FloatMenuOptionProvider
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
		if (settings == null || !settings.enableVehicleFramework || !settings.enableBulkLoadVehicles || !VehicleFrameworkCompat.IsActive || ((ThingWithComps)val).GetComp<CompHauledToInventory>() == null || val.inventory == null || HaulOrderGate.Blocks(val) || val.mindState?.duty?.def == DutyDefOf.LoadAndEnterTransporters)
		{
			yield break;
		}
		for (int i = 0; i < list.Count; i++)
		{
			Thing val2 = list[i];
			if (val2 == null || !VehicleFrameworkCompat.IsVehicle(val2))
			{
				continue;
			}
			IList list2 = VehicleFrameworkCompat.CargoToLoad(val2);
			if (list2 == null || list2.Count == 0 || !ReachabilityUtility.CanReach(val, LocalTargetInfo.op_Implicit(val2), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0) || !ReservationUtility.CanReserve(val, LocalTargetInfo.op_Implicit(val2), 1, -1, (ReservationLayerDef)null, false))
			{
				continue;
			}
			if (val.CurJobDef == HaulersDreamDefOf.HaulersDream_LoadVehicleInBulk)
			{
				LocalTargetInfo target = val.CurJob.GetTarget((TargetIndex)1);
				if (((LocalTargetInfo)(ref target)).Thing?.thingIDNumber == val2.thingIDNumber)
				{
					continue;
				}
			}
			VehicleLoadTarget vehicleLoadTarget = VehicleLoadTarget.TryCreate(val2);
			if (vehicleLoadTarget == null)
			{
				continue;
			}
			Pawn pawnLocal = val;
			VehicleLoadTarget adapterLocal = vehicleLoadTarget;
			Thing clickedLocal = val2;
			FloatMenuOption val3 = new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.LoadVehicle.Option", NamedArgument.op_Implicit(((Entity)val2).LabelShort))), (Action)delegate
			{
				//IL_001b: Unknown result type (might be due to invalid IL or missing references)
				Job val4 = TransportLoad.TryGiveBulkJob(pawnLocal, adapterLocal, playerOrder: true);
				if (val4 == null)
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.LoadVehicle.CouldNotStart")), LookTargets.op_Implicit(clickedLocal), MessageTypeDefOf.RejectInput, false);
				}
				else
				{
					val4.playerForced = true;
					pawnLocal.jobs.TryTakeOrderedJob(val4, (JobTag?)(JobTag)0, false);
				}
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			{
				iconThing = val2
			};
			yield return FloatMenuUtility.DecoratePrioritizedTask(val3, val, LocalTargetInfo.op_Implicit(val2), "ReservedBy", (ReservationLayerDef)null);
			break;
		}
	}
}
