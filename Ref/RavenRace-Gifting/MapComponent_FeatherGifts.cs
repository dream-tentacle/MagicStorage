using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RavenRace.Features.FeatherGift;

public class MapComponent_FeatherGifts : MapComponent
{
	public MapComponent_FeatherGifts(Map map)
		: base(map)
	{
	}

	public override void MapComponentTick()
	{
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		RavenRaceSettings settings = RavenRaceMod.Settings;
		if ((settings != null && !settings.enableFeatherGifts) || (Find.TickManager.TicksGame + base.map.uniqueID) % 600 != 0 || !base.map.IsPlayerHome || GenHostility.AnyHostileActiveThreatToPlayer(base.map, false, false))
		{
			return;
		}
		List<Pawn> list = base.map.mapPawns.FreeColonistsSpawned.Where((Pawn p) => ((Thing)p).def == RavenDefOf.Raven_Race && FeatherGiftUtility.Idle(p)).ToList();
		GenList.Shuffle<Pawn>((IList<Pawn>)list);
		foreach (Pawn giver in list)
		{
			Pawn val = GenCollection.FirstOrDefault<Pawn>(base.map.mapPawns.FreeColonistsSpawned, (Predicate<Pawn>)((Pawn p) => FeatherGiftUtility.Idle(p) && FeatherGiftUtility.Eligible(giver, p) && ReservationUtility.CanReserveAndReach(giver, LocalTargetInfo.op_Implicit((Thing)(object)p), (PathEndMode)2, (Danger)1, 1, -1, (ReservationLayerDef)null, false)));
			if (val != null)
			{
				giver.jobs.StartJob(JobMaker.MakeJob(FeatherGiftDefOf.Raven_GiveFeather, LocalTargetInfo.op_Implicit((Thing)(object)val)), (JobCondition)8, (ThinkNode)null, false, true, (ThinkTreeDef)null, (JobTag?)null, false, false, (bool?)null, false, true, false);
				break;
			}
		}
	}
}
