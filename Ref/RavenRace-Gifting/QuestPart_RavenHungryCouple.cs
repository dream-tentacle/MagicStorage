using System;
using System.Collections.Generic;
using System.Linq;
using ChezhouLib.ClThingComp;
using ChezhouLib.Utils;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RavenRace.Features.CommunityEvents;

public class QuestPart_RavenHungryCouple : QuestPartActivable
{
	public Map map;

	public IntVec3 entry;

	public List<Pawn> spouses = new List<Pawn>();

	public int meals;

	private int phase;

	private int dueTick;

	private int nextTick;

	private bool settled;

	private bool initialized;

	private bool landingRequested;

	private int visitDeadline;

	private bool visitLordChecked;

	private bool walkingStateRestored;

	public bool AcceptsFood
	{
		get
		{
			if (initialized && phase <= 1 && meals < 6)
			{
				return !settled;
			}
			return false;
		}
	}

	public Pawn Receiver
	{
		get
		{
			if (!AcceptsFood)
			{
				return null;
			}
			foreach (Pawn spouse in spouses)
			{
				if (spouse != null && ((Thing)spouse).Spawned && ((Thing)spouse).Map == map && !spouse.Dead && !spouse.Downed && !spouse.IsPrisoner && !spouse.IsSlave && !spouse.InMentalState && ((Thing)spouse).Faction != Faction.OfPlayer && !GenHostility.HostileTo((Thing)(object)spouse, Faction.OfPlayer))
				{
					return spouse;
				}
			}
			return null;
		}
	}

	private Lord VisitLord => spouses.Select((Pawn p) => (p == null) ? null : LordUtility.GetLord(p)).FirstOrDefault((Lord l) => ((l != null) ? l.LordJob : null) is LordJob_RavenCoupleVisit lordJob_RavenCoupleVisit && lordJob_RavenCoupleVisit.couple == this);

	public override string DescriptionPart
	{
		get
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			if (phase <= 1)
			{
				return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("RavenCommunity_CoupleProgress", NamedArgument.op_Implicit(meals)));
			}
			if (phase == 2)
			{
				return TaggedString.op_Implicit(Translator.Translate((meals >= 6) ? "RavenCommunity_CoupleFedDepartureProgress" : ((meals > 0) ? "RavenCommunity_CouplePartialDepartureProgress" : "RavenCommunity_CoupleUnfedDepartureProgress")));
			}
			if (phase == 3)
			{
				return TaggedString.op_Implicit(Translator.Translate("RavenCommunity_CoupleAwaitReturn"));
			}
			return TaggedString.op_Implicit(Translator.Translate("RavenCommunity_CoupleReturnedProgress"));
		}
	}

	public override IEnumerable<GlobalTargetInfo> QuestLookTargets
	{
		get
		{
			foreach (Pawn spouse in spouses)
			{
				if (spouse != null)
				{
					yield return GlobalTargetInfo.op_Implicit((Thing)(object)spouse);
				}
			}
		}
	}

	public override bool QuestPartReserves(Pawn p)
	{
		if (!settled)
		{
			return spouses.Contains(p);
		}
		return false;
	}

	protected override void Enable(SignalArgs args)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		((QuestPartActivable)this).Enable(args);
		if (!initialized)
		{
			Faction friendlyFusang = CommunityUtility.FriendlyFusang;
			if (friendlyFusang == null || map == null)
			{
				Finish(success: false);
				return;
			}
			initialized = true;
			spouses.Add(CommunityUtility.Generate(friendlyFusang, "Raven_Colonist", (Gender)1));
			spouses.Add(CommunityUtility.Generate(friendlyFusang, "Raven_Colonist", (Gender)2));
			spouses[0].relations.AddDirectRelation(PawnRelationDefOf.Spouse, spouses[1]);
			SpawnVisit();
		}
	}

	private void SpawnVisit()
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		phase = ((phase == 3) ? 4 : 0);
		landingRequested = false;
		visitLordChecked = false;
		walkingStateRestored = false;
		if ((!GenGrid.InBounds(entry, map) || !GenGrid.Standable(entry, map) || !map.reachability.CanReachColony(entry)) && !CommunityUtility.Entry(map, out entry))
		{
			Finish(success: false);
			return;
		}
		foreach (Pawn spouse in spouses)
		{
			if (Find.WorldPawns.Contains(spouse))
			{
				Find.WorldPawns.RemovePawn(spouse);
			}
			GenSpawn.Spawn((Thing)(object)spouse, entry, map, (WipeMode)0);
		}
		RestoreWalkingState();
		if (!EnsureVisitLord())
		{
			Finish(success: false);
			return;
		}
		dueTick = Find.TickManager.TicksGame;
		visitDeadline = Find.TickManager.TicksGame + 60000;
	}

	private void RestoreWalkingState()
	{
		if (walkingStateRestored)
		{
			return;
		}
		foreach (Pawn spouse in spouses)
		{
			if (spouse != null && ((Thing)spouse).Spawned && ((Thing)spouse).Map == map && !spouse.Dead && !spouse.IsPrisoner && !spouse.IsSlave && ((Thing)spouse).Faction != Faction.OfPlayer)
			{
				ThingComp_RaceFly val = ThingCompUtility.TryGetComp<ThingComp_RaceFly>((Thing)(object)spouse);
				if (spouse.CurJob != null && ((val != null && val.isActionFiy) || spouse.CurJobDef == ClDefof.Job_TakeOff || spouse.CurJobDef == ClDefof.Job_Landing))
				{
					spouse.jobs.EndCurrentJob((JobCondition)16, false, true);
				}
				if (val != null)
				{
					val.SetFlightActiveState(false);
					val.curVisualHeight = 0f;
					val.RemoveFlyingHediff();
					val.RemoveTakeOffHediff();
					val.RemoveLandingHediff();
				}
			}
		}
		walkingStateRestored = true;
	}

	private bool EnsureVisitLord()
	{
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		if (visitLordChecked)
		{
			return true;
		}
		Lord visitLord = VisitLord;
		if (visitLord != null)
		{
			visitLord.CurLordToil.UpdateAllDuties();
			if (!(visitLord.CurLordToil is LordToil_ExitMap))
			{
				foreach (Pawn spouse in spouses)
				{
					if (((Thing)spouse).Spawned && ((Thing)spouse).Map == map)
					{
						Job curJob = spouse.CurJob;
						if (curJob != null && curJob.exitMapOnArrival)
						{
							spouse.jobs.EndCurrentJob((JobCondition)16, false, true);
						}
					}
				}
			}
			visitLordChecked = true;
			return true;
		}
		if (GenCollection.Any<Pawn>(spouses, (Predicate<Pawn>)((Pawn p) => !((Thing)p).Spawned || ((Thing)p).Map != map)))
		{
			return false;
		}
		Pawn val = GenCollection.FirstOrDefault<Pawn>(spouses, (Predicate<Pawn>)((Pawn p) => !p.Downed));
		if (val == null)
		{
			return false;
		}
		IntVec3 spot = default(IntVec3);
		if (GenCollection.Any<Pawn>(spouses, (Predicate<Pawn>)((Pawn p) => !ReachabilityUtility.CanReachMapEdge(p))) || !RCellFinder.TryFindRandomSpotJustOutsideColony(val, ref spot) || !GenGrid.InBounds(spot, map) || !GenGrid.Standable(spot, map) || GenCollection.Any<Pawn>(spouses, (Predicate<Pawn>)((Pawn p) => !ReachabilityUtility.CanReach(p, LocalTargetInfo.op_Implicit(spot), (PathEndMode)1, (Danger)2, false, false, (TraverseMode)0))))
		{
			return false;
		}
		foreach (Pawn spouse2 in spouses)
		{
			Lord lord = LordUtility.GetLord(spouse2);
			if (lord != null)
			{
				lord.Notify_PawnLost(spouse2, (PawnLostCondition)7, (DamageInfo?)null);
			}
		}
		LordMaker.MakeNewLord(((Thing)val).Faction, (LordJob)(object)new LordJob_RavenCoupleVisit(this, spot), map, (IEnumerable<Pawn>)spouses);
		visitLordChecked = true;
		foreach (Pawn spouse3 in spouses)
		{
			if (spouse3.CurJob != null)
			{
				spouse3.jobs.EndCurrentJob((JobCondition)16, false, true);
			}
		}
		return true;
	}

	public override void QuestPartTick()
	{
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0387: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03be: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c3: Unknown result type (might be due to invalid IL or missing references)
		if (settled || !initialized)
		{
			return;
		}
		int ticksGame = Find.TickManager.TicksGame;
		if (ticksGame < nextTick)
		{
			return;
		}
		nextTick = ticksGame + 120;
		if (spouses.Count != 2 || GenCollection.Any<Pawn>(spouses, (Predicate<Pawn>)((Pawn p) => p == null || p.Dead || ((Thing)p).Destroyed || p.IsPrisoner || p.IsSlave || ((Thing)p).Faction == Faction.OfPlayer || GenHostility.HostileTo((Thing)(object)p, Faction.OfPlayer))))
		{
			Finish(success: false);
			return;
		}
		if (phase == 3)
		{
			if (ticksGame >= dueTick && !GenCollection.Any<Pawn>(spouses, (Predicate<Pawn>)((Pawn p) => ((Thing)p).Spawned || p.Downed)))
			{
				if (!CommunityUtility.Safe(map))
				{
					map = GenCollection.FirstOrDefault<Map>(Find.Maps, (Predicate<Map>)CommunityUtility.Safe);
				}
				if (!CommunityUtility.Safe(map) || !CommunityUtility.Entry(map, out entry))
				{
					dueTick = ticksGame + 2500;
				}
				else
				{
					SpawnVisit();
				}
			}
			return;
		}
		if (map == null || !Find.Maps.Contains(map))
		{
			Finish(success: false);
			return;
		}
		RestoreWalkingState();
		if (phase == 0 && meals < 6 && visitDeadline > 0 && ticksGame >= visitDeadline)
		{
			BeginDeparture(returning: false);
			return;
		}
		if (phase == 4 && visitDeadline > 0 && ticksGame >= visitDeadline)
		{
			Finish(success: false);
			return;
		}
		if (phase == 0 || phase == 1 || phase == 4 || phase == 5)
		{
			if (GenCollection.Any<Pawn>(spouses, (Predicate<Pawn>)((Pawn p) => !((Thing)p).Spawned || ((Thing)p).Map != map)))
			{
				Finish(success: false);
				return;
			}
			if (!visitLordChecked && spouses.All((Pawn p) => p.Downed))
			{
				return;
			}
			if (!EnsureVisitLord())
			{
				Finish(success: false);
				return;
			}
		}
		if (phase <= 1 || phase == 4 || phase == 5)
		{
			Lord visitLord = VisitLord;
			if (((visitLord != null) ? visitLord.CurLordToil : null) is LordToil_ExitMap)
			{
				Finish(success: false);
				return;
			}
		}
		if (phase <= 1 && meals >= 6)
		{
			BeginDeparture(returning: false);
		}
		else
		{
			if (ticksGame < dueTick)
			{
				return;
			}
			if (phase == 0 || phase == 4)
			{
				if (GenCollection.Any<Pawn>(spouses, (Predicate<Pawn>)((Pawn p) => !((Thing)p).Spawned || ((Thing)p).Map != map)))
				{
					Finish(success: false);
				}
				else
				{
					if (GenCollection.Any<Pawn>(spouses, (Predicate<Pawn>)((Pawn p) => p.Downed)))
					{
						return;
					}
					if (phase == 4)
					{
						Lord visitLord2 = VisitLord;
						if (!(((visitLord2 != null) ? visitLord2.CurLordToil : null) is LordToil_RavenCoupleWait))
						{
							return;
						}
						phase = 5;
						List<Thing> list = CommunityUtility.CoupleGift();
						string text = GenLabel.ThingsLabel((IEnumerable<Thing>)list, "  - ");
						foreach (Thing item in list)
						{
							if (!GenPlace.TryPlaceThing(item, ((Thing)spouses[0]).Position, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
							{
								item.Destroy((DestroyMode)0);
							}
						}
						Find.LetterStack.ReceiveLetter(Translator.Translate("RavenCommunity_CoupleReturnTitle"), TranslatorFormattedStringExtensions.Translate("RavenCommunity_CoupleReturn", NamedArgument.op_Implicit(((Entity)spouses[0]).LabelShort), NamedArgument.op_Implicit(((Entity)spouses[1]).LabelShort), NamedArgument.op_Implicit(text)), LetterDefOf.PositiveEvent, LookTargets.op_Implicit((Thing)(object)spouses[0]), (Faction)null, (Quest)null, (List<ThingDef>)null, (string)null, 0, true);
						dueTick = ticksGame + 2500;
					}
					else
					{
						phase = 1;
						dueTick = ((visitDeadline > 0) ? visitDeadline : (ticksGame + 60000));
					}
				}
			}
			else if (phase == 1 || phase == 5)
			{
				BeginDeparture(phase == 5);
			}
			else
			{
				if (phase != 2 && phase != 6)
				{
					return;
				}
				if (GenCollection.Any<Pawn>(spouses, (Predicate<Pawn>)((Pawn p) => ((Thing)p).Spawned)))
				{
					Lord visitLord3 = VisitLord;
					if (visitLord3 != null)
					{
						visitLord3.ReceiveMemo("RavenCoupleLeave");
					}
					else
					{
						CommunityUtility.Leave(spouses.Where(delegate(Pawn p)
						{
							if (((Thing)p).Spawned && !p.Downed)
							{
								Job curJob = p.CurJob;
								if (curJob == null || !curJob.exitMapOnArrival)
								{
									Lord lord = LordUtility.GetLord(p);
									return !(((lord != null) ? lord.LordJob : null) is LordJob_ExitMapBest);
								}
							}
							return false;
						}), map);
					}
					dueTick = ticksGame + 600;
				}
				else if (phase == 6 || meals < 6)
				{
					Finish(meals >= 6);
				}
				else
				{
					phase = 3;
					dueTick = ticksGame + Rand.RangeInclusive(5, 10) * 60000;
				}
			}
		}
	}

	private void BeginDeparture(bool returning)
	{
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		phase = (returning ? 6 : 2);
		dueTick = Find.TickManager.TicksGame;
		if (!returning)
		{
			string text = ((meals >= 6) ? "RavenCommunity_CoupleFedDepartureTitle" : ((meals > 0) ? "RavenCommunity_CouplePartialDepartureTitle" : "RavenCommunity_CoupleUnfedDepartureTitle"));
			string text2 = ((meals >= 6) ? "RavenCommunity_CoupleFedDepartureText" : ((meals > 0) ? "RavenCommunity_CouplePartialDepartureText" : "RavenCommunity_CoupleUnfedDepartureText"));
			Find.LetterStack.ReceiveLetter(Translator.Translate(text), TranslatorFormattedStringExtensions.Translate(text2, NamedArgument.op_Implicit(((Entity)spouses[0]).LabelShort), NamedArgument.op_Implicit(((Entity)spouses[1]).LabelShort)), LetterDefOf.NeutralEvent, LookTargets.op_Implicit((Thing)(object)spouses[0]), (Faction)null, (Quest)null, (List<ThingDef>)null, (string)null, 0, true);
		}
		Lord visitLord = VisitLord;
		if (visitLord != null)
		{
			visitLord.ReceiveMemo("RavenCoupleLeave");
		}
		else
		{
			CommunityUtility.Leave(spouses, map);
		}
	}

	private void Finish(bool success)
	{
		if (!settled)
		{
			settled = true;
			((QuestPart)this).quest.End((QuestEndOutcome)(success ? 1 : 0), success, true);
		}
	}

	public override void Cleanup()
	{
		settled = true;
		foreach (Map map in Find.Maps)
		{
			CommunityUtility.Leave(spouses, map);
		}
	}

	public static QuestPart_RavenHungryCouple For(Pawn receiver)
	{
		object obj;
		if (receiver == null)
		{
			obj = null;
		}
		else
		{
			Lord lord = LordUtility.GetLord(receiver);
			obj = ((lord != null) ? lord.LordJob : null);
		}
		if (obj is LordJob_RavenCoupleVisit lordJob_RavenCoupleVisit)
		{
			QuestPart_RavenHungryCouple couple = lordJob_RavenCoupleVisit.couple;
			if (couple == null || !couple.AcceptsFood)
			{
				return null;
			}
			return lordJob_RavenCoupleVisit.couple;
		}
		foreach (Quest item in Find.QuestManager.QuestsListForReading)
		{
			if (item.Historical)
			{
				continue;
			}
			foreach (QuestPart item2 in item.PartsListForReading)
			{
				if (item2 is QuestPart_RavenHungryCouple { AcceptsFood: not false } questPart_RavenHungryCouple && questPart_RavenHungryCouple.spouses.Contains(receiver))
				{
					return questPart_RavenHungryCouple;
				}
			}
		}
		return null;
	}

	public bool Edible(Thing food)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		if (food != null && food.IngestibleNow && food.def.ingestible != null && (food.def.ingestible.foodType & 0x100) != 0)
		{
			return spouses.All((Pawn p) => p.RaceProps.CanEverEat(food));
		}
		return false;
	}

	public IEnumerable<FloatMenuOption> DeliveryOptions(Pawn receiver, Pawn worker)
	{
		if (receiver != Receiver || worker == null || !worker.IsColonistPlayerControlled || ((Thing)worker).Map != map)
		{
			yield break;
		}
		string text = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("RavenCommunity_GiveMeals", NamedArgument.op_Implicit(6 - meals)));
		if (worker.Downed || !worker.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || !ReservationUtility.CanReserveAndReach(worker, LocalTargetInfo.op_Implicit((Thing)(object)receiver), (PathEndMode)2, (Danger)2, 1, -1, (ReservationLayerDef)null, false))
		{
			yield return new FloatMenuOption(TaggedString.op_Implicit(text + ": " + Translator.Translate("RavenCommunity_CannotDeliver")), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
			yield break;
		}
		if (FindMeal(worker) == null)
		{
			yield return new FloatMenuOption(TaggedString.op_Implicit(text + ": " + Translator.Translate("RavenCommunity_NoMeals")), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
			yield break;
		}
		yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, (Action)delegate
		{
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			if (receiver == Receiver && ((Thing)worker).Map == map && ReservationUtility.CanReserveAndReach(worker, LocalTargetInfo.op_Implicit((Thing)(object)receiver), (PathEndMode)2, (Danger)2, 1, -1, (ReservationLayerDef)null, false))
			{
				Thing val = FindMeal(worker);
				if (val == null)
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("RavenCommunity_NoMeals")), MessageTypeDefOf.RejectInput, false);
				}
				else
				{
					Job val2 = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("Raven_DeliverCoupleMeals", true), LocalTargetInfo.op_Implicit(val), LocalTargetInfo.op_Implicit((Thing)(object)receiver));
					val2.count = Math.Min(6 - meals, val.stackCount);
					worker.jobs.TryTakeOrderedJob(val2, (JobTag?)(JobTag)0, false);
				}
			}
		}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0), worker, LocalTargetInfo.op_Implicit((Thing)(object)receiver), "ReservedBy", (ReservationLayerDef)null);
	}

	private Thing FindMeal(Pawn worker)
	{
		return (from t in map.listerThings.ThingsInGroup((ThingRequestGroup)7)
			where Edible(t) && !ForbidUtility.IsForbidden(t, worker) && ReservationUtility.CanReserveAndReach(worker, LocalTargetInfo.op_Implicit(t), (PathEndMode)3, (Danger)2, 1, -1, (ReservationLayerDef)null, false)
			orderby IntVec3Utility.DistanceToSquared(t.Position, ((Thing)worker).Position)
			select t).FirstOrDefault();
	}

	public void Receive(Thing carried)
	{
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		if (!AcceptsFood || !Edible(carried))
		{
			return;
		}
		int num = Math.Min(carried.stackCount, 6 - meals);
		carried.SplitOff(num).Destroy((DestroyMode)0);
		meals += num;
		foreach (Pawn spouse in spouses)
		{
			if (spouse.needs?.food != null)
			{
				((Need)spouse.needs.food).CurLevelPercentage = 1f;
			}
		}
		Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("RavenCommunity_CoupleProgress", NamedArgument.op_Implicit(meals))), LookTargets.op_Implicit((Thing)(object)spouses[0]), MessageTypeDefOf.PositiveEvent, true);
		if (meals >= 6)
		{
			BeginDeparture(returning: false);
		}
	}

	public override void ExposeData()
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Invalid comparison between Unknown and I4
		((QuestPartActivable)this).ExposeData();
		Scribe_References.Look<Map>(ref map, "map", false);
		Scribe_Values.Look<IntVec3>(ref entry, "entry", default(IntVec3), false);
		Scribe_Collections.Look<Pawn>(ref spouses, "spouses", (LookMode)3, Array.Empty<object>());
		Scribe_Values.Look<int>(ref meals, "meals", 0, false);
		Scribe_Values.Look<int>(ref phase, "phase", 0, false);
		Scribe_Values.Look<int>(ref dueTick, "dueTick", 0, false);
		Scribe_Values.Look<int>(ref nextTick, "nextTick", 0, false);
		Scribe_Values.Look<bool>(ref settled, "settled", false, false);
		Scribe_Values.Look<bool>(ref initialized, "initialized", false, false);
		Scribe_Values.Look<bool>(ref landingRequested, "landingRequested", false, false);
		Scribe_Values.Look<int>(ref visitDeadline, "visitDeadline", 0, false);
		if (spouses == null)
		{
			spouses = new List<Pawn>();
		}
		if ((int)Scribe.mode == 4)
		{
			visitLordChecked = false;
			walkingStateRestored = false;
			if (visitDeadline == 0 && (phase == 0 || phase == 1 || phase == 4))
			{
				visitDeadline = ((phase == 1) ? dueTick : (Find.TickManager.TicksGame + 60000));
			}
		}
	}
}
