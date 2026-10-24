using System;
using System.Collections;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class FloatMenuOptionProvider_ContinuousLoad : FloatMenuOptionProvider
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
		if (settings == null || !settings.enableContinuousLoading || ((ThingWithComps)val).GetComp<CompHauledToInventory>() == null || val.inventory == null || HaulOrderGate.Blocks(val))
		{
			yield break;
		}
		DutyDef val2 = val.mindState?.duty?.def;
		if (val2 == DutyDefOf.LoadAndEnterTransporters || val2 == DutyDefOf.LoadAndEnterPortal)
		{
			yield break;
		}
		for (int i = 0; i < list.Count; i++)
		{
			Thing val3 = list[i];
			if (val3 == null)
			{
				continue;
			}
			IManagedLoadable managedLoadable = TryResolveLoadable(val3, settings);
			if (managedLoadable == null || !ReachabilityUtility.CanReach(val, LocalTargetInfo.op_Implicit(val3), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0) || !ReservationUtility.CanReserve(val, LocalTargetInfo.op_Implicit(val3), 1, -1, (ReservationLayerDef)null, false))
			{
				continue;
			}
			Pawn pawnLocal = val;
			IManagedLoadable adapterLocal = managedLoadable;
			Thing clickedLocal = val3;
			FloatMenuOption val4 = new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.ContinuousLoad.Option", NamedArgument.op_Implicit(((Entity)val3).LabelShort))), (Action)delegate
			{
				//IL_001b: Unknown result type (might be due to invalid IL or missing references)
				Job val5 = TransportLoad.TryGiveBulkJob(pawnLocal, adapterLocal, playerOrder: true);
				if (val5 == null)
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.ContinuousLoad.CouldNotStart")), LookTargets.op_Implicit(clickedLocal), MessageTypeDefOf.RejectInput, false);
				}
				else
				{
					val5.playerForced = true;
					pawnLocal.jobs.TryTakeOrderedJob(val5, (JobTag?)(JobTag)0, false);
				}
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			{
				iconThing = val3
			};
			yield return FloatMenuUtility.DecoratePrioritizedTask(val4, val, LocalTargetInfo.op_Implicit(val3), "ReservedBy", (ReservationLayerDef)null);
			break;
		}
	}

	private static IManagedLoadable TryResolveLoadable(Thing clicked, HaulersDreamSettings s)
	{
		CompTransporter val = ThingCompUtility.TryGetComp<CompTransporter>(clicked);
		if (val != null)
		{
			if (!s.enableBulkLoadTransporters || !val.AnyInGroupHasAnythingLeftToLoad)
			{
				return null;
			}
			return LoadTransportersAdapter.TryCreate(val);
		}
		MapPortal val2 = (MapPortal)(object)((clicked is MapPortal) ? clicked : null);
		if (val2 != null)
		{
			if (!s.enableBulkLoadPortal || !val2.LoadInProgress)
			{
				return null;
			}
			return MapPortalBulkTarget.TryCreate(val2);
		}
		if (s.enableVehicleFramework && s.enableBulkLoadVehicles && VehicleFrameworkCompat.IsActive && VehicleFrameworkCompat.IsVehicle(clicked))
		{
			IList list = VehicleFrameworkCompat.CargoToLoad(clicked);
			if (list == null || list.Count == 0)
			{
				return null;
			}
			return VehicleLoadTarget.TryCreate(clicked);
		}
		return null;
	}
}
