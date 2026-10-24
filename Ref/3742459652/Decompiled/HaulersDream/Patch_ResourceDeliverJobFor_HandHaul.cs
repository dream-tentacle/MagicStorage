using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ResourceDeliverJobFor")]
[HarmonyPriority(200)]
public static class Patch_ResourceDeliverJobFor_HandHaul
{
	private static void Postfix(ref Job __result, Pawn pawn, IConstructible c, bool forced)
	{
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		if (__result != null)
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.shareHandHauledToStorage || ((pawn != null) ? ((Thing)pawn).Map : null) == null || c == null || c is Blueprint_Install)
		{
			return;
		}
		Thing val = (Thing)(object)((c is Thing) ? c : null);
		if (val == null || val.Destroyed || !val.Spawned)
		{
			return;
		}
		foreach (ThingDefCountClass item in c.TotalMaterialCost())
		{
			int num;
			if (!forced)
			{
				IHaulEnroute val2 = (IHaulEnroute)(object)((c is IHaulEnroute) ? c : null);
				if (val2 != null)
				{
					num = EnrouteUtility.GetSpaceRemainingWithEnroute(val2, item.thingDef, pawn);
					goto IL_008f;
				}
			}
			num = c.ThingCountNeeded(item.thingDef);
			goto IL_008f;
			IL_008f:
			int num2 = num;
			if (num2 > 0)
			{
				Pawn carrier;
				Thing val3 = CarriedHaulShare.FindCarriedStack(((Thing)pawn).Map, pawn, item.thingDef, val, num2, out carrier);
				if (val3 != null && carrier != null)
				{
					Job val4 = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_ClaimFromHauler);
					val4.targetA = LocalTargetInfo.op_Implicit((Thing)(object)carrier);
					val4.targetB = LocalTargetInfo.op_Implicit(val);
					val4.targetC = LocalTargetInfo.op_Implicit(val);
					val4.count = Mathf.Min(num2, val3.stackCount);
					__result = val4;
					HDLog.Dbg($"{pawn} will claim {val4.count}x {((Def)item.thingDef).label} from hand-hauler {((Entity)carrier).LabelShort} for {((Entity)val).LabelShort}.");
					break;
				}
			}
		}
	}
}
