using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RavenRace.Features.FeatherGift;

public static class FeatherGiftUtility
{
	public static bool Member(Pawn p)
	{
		if (p != null && !p.Dead && !((Thing)p).Destroyed && p.IsFreeColonist && ((Thing)p).Faction == Faction.OfPlayer && !QuestUtility.IsQuestLodger(p))
		{
			return p.apparel != null;
		}
		return false;
	}

	public static bool Available(Pawn p)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Invalid comparison between Unknown and I4
		if (Member(p) && ((Thing)p).Spawned && !p.Downed && !p.Drafted && !p.InMentalState && RestUtility.Awake(p) && (int)PawnUtility.GetPosture(p) == 0 && (int)p.DevelopmentalStage != 2)
		{
			return p.jobs != null;
		}
		return false;
	}

	public static bool Idle(Pawn p)
	{
		if (Available(p) && (p.CurJobDef == JobDefOf.GotoWander || p.CurJobDef == JobDefOf.Wait_Wander))
		{
			Job curJob = p.CurJob;
			if (curJob == null)
			{
				return true;
			}
			return !curJob.playerForced;
		}
		return false;
	}

	public static bool Eligible(Pawn giver, Pawn receiver)
	{
		RavenRaceSettings settings = RavenRaceMod.Settings;
		if ((settings == null || settings.enableFeatherGifts) && Available(giver) && Available(receiver) && giver != receiver && ((Thing)giver).def == RavenDefOf.Raven_Race && giver.ageTracker.AgeBiologicalYears >= 13 && ((Thing)giver).Map == ((Thing)receiver).Map && giver.relations.OpinionOf(receiver) >= 60 && !Current.Game.GetComponent<GameComponent_FeatherGifts>().Given(giver, receiver) && ApparelUtility.HasPartsToWear(receiver, FeatherGiftDefOf.Raven_Apparel_GiftFeather))
		{
			return FeatherGiftDefOf.Raven_Apparel_GiftFeather.apparel.PawnCanWear(receiver, false);
		}
		return false;
	}

	public static int BondCount(Pawn a, Pawn b)
	{
		if (a == null || b == null || a.Dead || b.Dead || ((Thing)a).Destroyed || ((Thing)b).Destroyed || ((Thing)a).Faction != Faction.OfPlayer || ((Thing)b).Faction != Faction.OfPlayer)
		{
			return 0;
		}
		return WornFrom(a, b) + WornFrom(b, a);
	}

	private static int WornFrom(Pawn wearer, Pawn giver)
	{
		Pawn_ApparelTracker apparel = wearer.apparel;
		if (apparel == null)
		{
			return 0;
		}
		return apparel.WornApparel.OfType<Apparel_GiftFeather>().Count((Apparel_GiftFeather f) => f.Giver == giver && f.BoundTo(wearer));
	}

	[DebugAction(/*Could not decode attribute arguments.*/)]
	public static void DebugOpinion()
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected O, but got Unknown
		List<Pawn> pawns = Find.CurrentMap.mapPawns.AllPawnsSpawned.Where((Pawn p) => p.RaceProps.Humanlike && p.needs?.mood != null).ToList();
		Find.WindowStack.Add((Window)new FloatMenu(((IEnumerable<Pawn>)pawns).Select((Func<Pawn, FloatMenuOption>)((Pawn a) => new FloatMenuOption("看法持有者：" + ((Entity)a).LabelShort, (Action)delegate
		{
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Expected O, but got Unknown
			Find.WindowStack.Add((Window)new FloatMenu(pawns.Where((Pawn b) => b != a).Select((Func<Pawn, FloatMenuOption>)((Pawn b) => new FloatMenuOption("好感对象：" + ((Entity)b).LabelShort, (Action)delegate
			{
				a.needs.mood.thoughts.memories.TryGainMemory(FeatherGiftDefOf.Raven_DebugFeatherOpinion, b, (Precept)null);
				Messages.Message(((Entity)a).LabelShort + " 对 " + ((Entity)b).LabelShort + " 获得了 +100 调试看法。", LookTargets.op_Implicit((Thing)(object)a), MessageTypeDefOf.TaskCompletion, true);
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0))).ToList()));
		}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0))).ToList()));
	}
}
