using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class EnrouteSafety
{
	public static int SpaceRemainingSafe(Thing needer, ThingDef def, Pawn pawn)
	{
		if (!ConstructDeliveryPlan.ShouldQueryNeederSpace(needer != null && needer.Spawned, def != null))
		{
			return 0;
		}
		IConstructible val = (IConstructible)(object)((needer is IConstructible) ? needer : null);
		if (val == null)
		{
			return 0;
		}
		IHaulEnroute val2 = (IHaulEnroute)(object)((needer is IHaulEnroute) ? needer : null);
		if (val2 != null)
		{
			return Mathf.Max(0, EnrouteUtility.GetSpaceRemainingWithEnroute(val2, def, pawn));
		}
		return Mathf.Max(0, val.ThingCountNeeded(def));
	}
}
