using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class CorpseStripper
{
	internal static bool ShouldLeaveTaintedApparel(Thing t, HaulersDreamSettings s)
	{
		if (s == null || !StripPolicy.LeavesAnyTainted(s.taintedSmeltablePolicy, s.taintedNonSmeltablePolicy))
		{
			return false;
		}
		Apparel val = (Apparel)(object)((t is Apparel) ? t : null);
		if (val == null || ((Thing)val).def.apparel == null)
		{
			return false;
		}
		if (!val.WornByCorpse || !((Thing)val).def.apparel.careIfWornByCorpse)
		{
			return false;
		}
		return StripPolicy.LeaveWhereItIs(tainted: true, ((Thing)val).Smeltable, s.taintedSmeltablePolicy, s.taintedNonSmeltablePolicy);
	}

	internal static bool StaysOnCorpse(Apparel ap, HaulersDreamSettings s)
	{
		if (ap == null || s == null)
		{
			return false;
		}
		return StripPolicy.StaysOnCorpse(ap.WornByCorpse && ((Thing)ap).def.apparel != null && ((Thing)ap).def.apparel.careIfWornByCorpse, ((Thing)ap).Smeltable, s.taintedSmeltablePolicy, s.taintedNonSmeltablePolicy);
	}

	internal static bool AnythingHdWouldStrip(Corpse corpse, HaulersDreamSettings s)
	{
		Pawn val = ((corpse != null) ? corpse.InnerPawn : null);
		if (val == null)
		{
			return false;
		}
		if (!val.kindDef.canStrip)
		{
			return false;
		}
		if (val.equipment != null && val.equipment.HasAnything())
		{
			return true;
		}
		if (val.inventory != null && ((ThingOwner)val.inventory.innerContainer).Count > 0)
		{
			return true;
		}
		Pawn_ApparelTracker apparel = val.apparel;
		if (apparel == null)
		{
			return false;
		}
		bool destroyed = ((Thing)val).Destroyed;
		List<Apparel> wornApparel = apparel.WornApparel;
		for (int i = 0; i < wornApparel.Count; i++)
		{
			Apparel val2 = wornApparel[i];
			if ((destroyed || !apparel.IsLocked(val2)) && !StaysOnCorpse(val2, s))
			{
				return true;
			}
		}
		return false;
	}

	internal static void QueueReStripIfNeeded(Pawn stripper, Thing target)
	{
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.IsTypeEnabled(HaulSourceType.Strip))
		{
			return;
		}
		Pawn val = (Pawn)(object)((target is Pawn) ? target : null);
		if (val == null || val.Dead || !((Thing)val).Spawned || stripper?.jobs == null || ((Thing)stripper).Map != ((Thing)val).Map || ((Thing)stripper).Faction != Faction.OfPlayerSilentFail)
		{
			return;
		}
		JobQueue jobQueue = stripper.jobs.jobQueue;
		if (jobQueue == null)
		{
			return;
		}
		for (int i = 0; i < jobQueue.Count; i++)
		{
			if (jobQueue[i]?.job?.def == JobDefOf.Strip && (object)((LocalTargetInfo)(ref jobQueue[i].job.targetA)).Thing == val)
			{
				return;
			}
		}
		jobQueue.EnqueueLast(JobMaker.MakeJob(JobDefOf.Strip, LocalTargetInfo.op_Implicit((Thing)(object)val)), (JobTag?)(JobTag)0);
	}

	internal static void ApplyTaintedPolicyToPending(Pawn stripper, bool corpseStrip)
	{
		if (!corpseStrip)
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || stripper == null)
		{
			return;
		}
		List<Thing> list = ((ThingWithComps)stripper).GetComp<CompHauledToInventory>()?.pendingSelfPickups;
		if (list == null || list.Count == 0)
		{
			return;
		}
		for (int num = list.Count - 1; num >= 0; num--)
		{
			Thing obj = list[num];
			Apparel val = (Apparel)(object)((obj is Apparel) ? obj : null);
			if (val != null && !((Thing)val).Destroyed && ((Thing)val).Spawned && val.WornByCorpse && ((Thing)val).def.apparel != null && ((Thing)val).def.apparel.careIfWornByCorpse)
			{
				switch (StripPolicy.ApparelAction(tainted: true, ((Thing)val).Smeltable, settings.taintedSmeltablePolicy, settings.taintedNonSmeltablePolicy))
				{
				case TaintedApparelPolicy.LeaveOnCorpse:
					ForbidUtility.SetForbidden((Thing)(object)val, true, false);
					list.RemoveAt(num);
					SelfPickupClaims.Release((Thing)(object)val, stripper);
					break;
				case TaintedApparelPolicy.DropAndForbid:
					ForbidUtility.SetForbidden((Thing)(object)val, true, false);
					list.RemoveAt(num);
					SelfPickupClaims.Release((Thing)(object)val, stripper);
					break;
				case TaintedApparelPolicy.Destroy:
					if (GenList.NullOrEmpty<string>((IList<string>)((Thing)val).questTags) && !ReliquaryUtility.IsRelic((Thing)(object)val) && ((Thing)val).stackCount == 1)
					{
						list.RemoveAt(num);
						SelfPickupClaims.Release((Thing)(object)val, stripper);
						((Thing)val).Destroy((DestroyMode)0);
					}
					break;
				}
			}
		}
	}

	internal static void MaybeStripForHaul(Pawn hauler, Corpse corpse)
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || settings.autoStripMode == AutoStripMode.Off || hauler == null || corpse == null || !((Thing)corpse).Spawned || ((Thing)hauler).Map == null || ((Thing)corpse).Map != ((Thing)hauler).Map || !MapGate.HdActiveOnMap(((Thing)corpse).Map) || ((Thing)hauler).Faction != Faction.OfPlayerSilentFail || QuestUtility.IsQuestLodger(hauler))
		{
			return;
		}
		CompHauledToInventory comp = ((ThingWithComps)hauler).GetComp<CompHauledToInventory>();
		if (comp == null || !comp.autoHaulYields || !YieldRouter.IsRaceEligible(hauler))
		{
			return;
		}
		Job curJob = hauler.CurJob;
		if (curJob != null && QualifyingHaul(curJob, settings.autoStripMode))
		{
			Pawn innerPawn = corpse.InnerPawn;
			if (innerPawn != null && (settings.stripColonistCorpses || ((Thing)innerPawn).Faction != Faction.OfPlayerSilentFail) && StrippableUtility.CanBeStrippedByColony((Thing)(object)corpse))
			{
				StripAndScoop(hauler, corpse, settings);
			}
		}
	}

	internal static void MaybeStripForCremation(Pawn worker, Thing ingredient, RecipeDef recipe)
	{
		Corpse val = (Corpse)(object)((ingredient is Corpse) ? ingredient : null);
		if (val == null)
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || recipe == null || worker == null || !((Thing)val).Spawned || ((Thing)val).Map == null)
		{
			return;
		}
		Pawn innerPawn = val.InnerPawn;
		if (innerPawn != null)
		{
			bool isPlayerFactionCorpse = ((Thing)innerPawn).Faction == Faction.OfPlayerSilentFail;
			if (CremationStripPolicy.ShouldStrip(settings.stripBeforeCremation, recipe.autoStripCorpses, val.AnythingToStrip(), isPlayerFactionCorpse, settings.stripColonistCorpses))
			{
				StripCorpseDroppingLoot(worker, val, settings);
			}
		}
	}

	private static bool QualifyingHaul(Job job, AutoStripMode mode)
	{
		if (job.def == JobDefOf.DoBill)
		{
			return true;
		}
		if (job.def == JobDefOf.HaulToContainer)
		{
			if (mode == AutoStripMode.AllHauls)
			{
				return true;
			}
			return ((LocalTargetInfo)(ref job.targetB)).Thing is Building_Casket;
		}
		if (job.def == JobDefOf.HaulToCell)
		{
			return mode == AutoStripMode.AllHauls;
		}
		if (job.def == HaulersDreamDefOf.HaulersDream_BulkHaul)
		{
			return mode == AutoStripMode.AllHauls;
		}
		return false;
	}

	internal static List<ThingCount> StripCorpseDroppingLoot(Pawn actor, Corpse corpse, HaulersDreamSettings s)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		Pawn innerPawn = corpse.InnerPawn;
		if (innerPawn == null)
		{
			return null;
		}
		Map map = ((Thing)corpse).Map;
		IntVec3 positionHeld = ((Thing)corpse).PositionHeld;
		List<ThingCount> loot = new List<ThingCount>();
		bool flag = false;
		if (innerPawn.equipment != null)
		{
			List<ThingWithComps> allEquipmentListForReading = innerPawn.equipment.AllEquipmentListForReading;
			ThingWithComps val2 = default(ThingWithComps);
			for (int num = allEquipmentListForReading.Count - 1; num >= 0; num--)
			{
				ThingWithComps val = allEquipmentListForReading[num];
				if (innerPawn.equipment.TryDropEquipment(val, ref val2, positionHeld, false) && val2 != null)
				{
					flag = true;
					if (val2 == val)
					{
						loot.Add(new ThingCount((Thing)(object)val2, ((Thing)val2).stackCount, false));
					}
				}
			}
		}
		ThingOwner<Thing> val3 = innerPawn.inventory?.innerContainer;
		if (val3 != null)
		{
			Thing val4 = default(Thing);
			for (int num2 = ((ThingOwner)val3).Count - 1; num2 >= 0; num2--)
			{
				if (val3.TryDrop(val3[num2], positionHeld, map, (ThingPlaceMode)1, ref val4, (Action<Thing, int>)delegate(Thing placed, int count)
				{
					//IL_0009: Unknown result type (might be due to invalid IL or missing references)
					loot.Add(new ThingCount(placed, count, false));
				}, (Predicate<IntVec3>)null))
				{
					flag = true;
				}
			}
		}
		if (innerPawn.apparel != null)
		{
			List<Apparel> wornApparel = innerPawn.apparel.WornApparel;
			Apparel val6 = default(Apparel);
			for (int num3 = wornApparel.Count - 1; num3 >= 0; num3--)
			{
				Apparel val5 = wornApparel[num3];
				if (((Thing)innerPawn).Destroyed || !innerPawn.apparel.IsLocked(val5))
				{
					TaintedApparelPolicy taintedApparelPolicy = StripPolicy.ApparelAction(val5.WornByCorpse && ((Thing)val5).def.apparel != null && ((Thing)val5).def.apparel.careIfWornByCorpse, ((Thing)val5).Smeltable, s.taintedSmeltablePolicy, s.taintedNonSmeltablePolicy);
					if (taintedApparelPolicy != TaintedApparelPolicy.LeaveOnCorpse && innerPawn.apparel.TryDrop(val5, ref val6, positionHeld, false) && val6 != null)
					{
						flag = true;
						if (val6 == val5)
						{
							switch (taintedApparelPolicy)
							{
							case TaintedApparelPolicy.Destroy:
								if (!GenList.NullOrEmpty<string>((IList<string>)((Thing)val6).questTags) || ReliquaryUtility.IsRelic((Thing)(object)val6))
								{
									loot.Add(new ThingCount((Thing)(object)val6, ((Thing)val6).stackCount, false));
								}
								else if (!((Thing)val6).Destroyed)
								{
									((Thing)val6).Destroy((DestroyMode)0);
								}
								break;
							case TaintedApparelPolicy.DropAndForbid:
								ForbidUtility.SetForbidden((Thing)(object)val6, true, false);
								break;
							default:
								loot.Add(new ThingCount((Thing)(object)val6, ((Thing)val6).stackCount, false));
								break;
							}
						}
					}
				}
			}
		}
		if (!flag)
		{
			return null;
		}
		Designation obj = map.designationManager.DesignationOn((Thing)(object)corpse, DesignationDefOf.Strip);
		if (obj != null)
		{
			obj.Delete();
		}
		Pawn_RecordsTracker records = actor.records;
		if (records != null)
		{
			records.Increment(RecordDefOf.BodiesStripped);
		}
		if (((Thing)innerPawn).Faction != null)
		{
			((Thing)innerPawn).Faction.Notify_MemberStripped(innerPawn, Faction.OfPlayer);
		}
		return loot;
	}

	private static void StripAndScoop(Pawn hauler, Corpse corpse, HaulersDreamSettings s)
	{
		List<ThingCount> list = StripCorpseDroppingLoot(hauler, corpse, s);
		if (list != null && YieldRouter.IsEligible(hauler))
		{
			int num = ScoopLoot(hauler, list, s);
			HDLog.Dbg($"{hauler} auto-stripped {corpse} on haul: scooped {num} of {list.Count} loot entries.");
		}
	}

	private static int ScoopLoot(Pawn hauler, List<ThingCount> loot, HaulersDreamSettings s)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		Pawn_InventoryTracker inventory = hauler.inventory;
		ThingOwner val = ((inventory != null) ? inventory.GetDirectlyHeldThings() : null);
		CompHauledToInventory comp = ((ThingWithComps)hauler).GetComp<CompHauledToInventory>();
		if (val == null || comp == null)
		{
			return 0;
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < loot.Count; i++)
		{
			ThingCount val2 = loot[i];
			Thing thing = ((ThingCount)(ref val2)).Thing;
			if (thing == null || thing.Destroyed || !thing.Spawned || StoreUtility.IsInValidStorage(thing))
			{
				continue;
			}
			int num3 = OverloadGate.CountToPickUp(hauler, thing, s);
			int val3 = num3;
			val2 = loot[i];
			num3 = Math.Min(val3, ((ThingCount)(ref val2)).Count);
			if (num3 <= 0)
			{
				continue;
			}
			if (!YieldRouter.HasScoopDestination(hauler, thing))
			{
				num2++;
				continue;
			}
			Thing val4 = thing.SplitOff(Math.Min(num3, thing.stackCount));
			if (val.TryAdd(val4, false))
			{
				comp.RegisterHauledItem(val4);
				comp.NotifyYieldPicked();
				if (!val4.Spawned)
				{
					val4.Position = ((Thing)hauler).Position;
				}
				num++;
			}
			else if (val4 != null && !val4.Destroyed && !val4.Spawned)
			{
				GenPlace.TryPlaceThing(val4, ((Thing)hauler).Position, ((Thing)hauler).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			}
		}
		if (num2 > 0)
		{
			HDLog.Dbg($"{hauler} auto-strip: left {num2} stripped loot entr(ies) at the body — nowhere better to put them.");
		}
		return num;
	}
}
