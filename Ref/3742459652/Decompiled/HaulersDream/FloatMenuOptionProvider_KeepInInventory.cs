using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class FloatMenuOptionProvider_KeepInInventory : FloatMenuOptionProvider
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
		if (settings == null || !settings.keepInInventoryOption || ((ThingWithComps)pawn).GetComp<CompHauledToInventory>() == null || pawn.inventory == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || (!pawn.Drafted && pawn.WorkTagIsDisabled((WorkTags)16384)))
		{
			yield break;
		}
		for (int i = 0; i < things.Count; i++)
		{
			Thing val = things[i];
			if (val == null)
			{
				continue;
			}
			Thing val2 = null;
			if (!val.Spawned)
			{
				Thing spawnedParentOrMe = val.SpawnedParentOrMe;
				if (spawnedParentOrMe == null || spawnedParentOrMe == val || spawnedParentOrMe is Pawn || spawnedParentOrMe.Map != ((Thing)pawn).Map)
				{
					continue;
				}
				ThingOwner val3 = ThingOwnerUtility.TryGetInnerInteractableThingOwner(spawnedParentOrMe);
				if (val3 == null || !val3.Contains(val))
				{
					continue;
				}
				val2 = spawnedParentOrMe;
			}
			if (val.def == null || (int)val.def.category != 2 || !val.def.EverHaulable || VehicleFrameworkCompat.IsVehicle(val))
			{
				continue;
			}
			Thing val4 = val2 ?? val;
			if (GridsUtility.Fogged(val4.Position, ((Thing)pawn).Map) || FireUtility.IsBurning(val4) || !ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(val), 1, -1, (ReservationLayerDef)null, true) || !ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit(val4), (PathEndMode)3, (Danger)3, false, false, (TraverseMode)0))
			{
				continue;
			}
			Thing clickedLocal = val;
			Thing containerLocal = val2;
			Pawn pawnLocal = pawn;
			FloatMenuOption val5 = new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Keep.Option", NamedArgument.op_Implicit(((Entity)val).LabelCap))), (Action)delegate
			{
				//IL_0074: Unknown result type (might be due to invalid IL or missing references)
				//IL_007e: Expected O, but got Unknown
				int stackCount = clickedLocal.stackCount;
				if (stackCount <= 1)
				{
					OrderKeep(pawnLocal, clickedLocal, containerLocal, stackCount);
				}
				else
				{
					Find.WindowStack.Add((Window)new Dialog_Slider((Func<int, string>)((int n) => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Keep.SliderLabel", NamedArgument.op_Implicit(n), NamedArgument.op_Implicit(clickedLocal.LabelNoCount)))), 1, stackCount, (Action<int>)delegate(int n)
					{
						OrderKeep(pawnLocal, clickedLocal, containerLocal, n);
					}, stackCount, 1f));
				}
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			{
				iconThing = val
			};
			yield return FloatMenuUtility.DecoratePrioritizedTask(val5, pawn, LocalTargetInfo.op_Implicit(val4), "ReservedBy", (ReservationLayerDef)null);
		}
	}

	private static void OrderKeep(Pawn pawn, Thing item, Thing container, int count)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		Job val = ((container != null) ? BulkHaul.BuildKeepFromContainerJob(pawn, item, container, count) : BulkHaul.BuildKeepJob(pawn, item, count));
		if (val == null)
		{
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Keep.CouldNotStart", NamedArgument.op_Implicit(((Entity)pawn).LabelShort), NamedArgument.op_Implicit(((Entity)item).LabelCap))), LookTargets.op_Implicit(item), MessageTypeDefOf.RejectInput, false);
			return;
		}
		val.playerForced = true;
		pawn.jobs.TryTakeOrderedJob(val, (JobTag?)(JobTag)0, false);
	}
}
