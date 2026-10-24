using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class FloatMenuOptionProvider_PickUpIntoInventory : FloatMenuOptionProvider
{
	public override bool Drafted => true;

	public override bool Undrafted => true;

	public override bool Multiselect => false;

	public override bool MechanoidCanDo => false;

	public override bool CanSelfTarget => false;

	public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
	{
		Pawn pawn = ((context != null) ? context.FirstSelectedPawn : null);
		List<Thing> things = ((context != null) ? context.ClickedThings : null);
		if (pawn == null || things == null || ((Thing)pawn).Map == null)
		{
			yield break;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.manualPickupOption || !MapGate.HdActiveOnMap(((Thing)pawn).Map) || ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null || HaulOrderGate.Blocks(pawn))
		{
			yield break;
		}
		for (int i = 0; i < things.Count; i++)
		{
			Thing val = things[i];
			if (val == null || !val.Spawned || val.def == null || (int)val.def.category != 2 || !val.def.EverHaulable || VehicleFrameworkCompat.IsVehicle(val) || StoreUtility.IsInValidBestStorage(val) || GridsUtility.Fogged(val.Position, ((Thing)pawn).Map) || FireUtility.IsBurning(val) || !ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(val), 1, -1, (ReservationLayerDef)null, true) || !ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit(val), (PathEndMode)3, (Danger)3, false, false, (TraverseMode)0))
			{
				continue;
			}
			Thing clickedLocal = val;
			Pawn pawnLocal = pawn;
			FloatMenuOption val2 = new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PickUp.Option", NamedArgument.op_Implicit(((Entity)val).LabelCap))), (Action)delegate
			{
				//IL_003b: Unknown result type (might be due to invalid IL or missing references)
				//IL_004b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0050: Unknown result type (might be due to invalid IL or missing references)
				Job val3 = BulkHaul.BuildPickUpJob(pawnLocal, clickedLocal) ?? HaulAIUtility.HaulToStorageJob(pawnLocal, clickedLocal, true);
				if (val3 == null)
				{
					Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PickUp.CouldNotStart", NamedArgument.op_Implicit(((Entity)pawnLocal).LabelShort), NamedArgument.op_Implicit(((Entity)clickedLocal).LabelCap))), LookTargets.op_Implicit(clickedLocal), MessageTypeDefOf.RejectInput, false);
				}
				else
				{
					val3.playerForced = true;
					pawnLocal.jobs.TryTakeOrderedJob(val3, (JobTag?)(JobTag)0, false);
				}
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			{
				iconThing = val
			};
			yield return FloatMenuUtility.DecoratePrioritizedTask(val2, pawn, LocalTargetInfo.op_Implicit(val), "ReservedBy", (ReservationLayerDef)null);
		}
	}
}
