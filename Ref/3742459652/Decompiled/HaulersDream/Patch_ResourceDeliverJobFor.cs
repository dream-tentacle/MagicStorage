using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ResourceDeliverJobFor")]
[HarmonyPriority(600)]
public static class Patch_ResourceDeliverJobFor
{
	private static void Postfix(ref Job __result, Pawn pawn, IConstructible c, bool forced)
	{
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Expected O, but got Unknown
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Expected O, but got Unknown
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		if (__result != null)
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || (!settings.buildFromInventory && !settings.shareForBuilding) || ((pawn != null) ? ((Thing)pawn).Map : null) == null || c == null || c is Blueprint_Install)
		{
			return;
		}
		foreach (ThingDefCountClass item in c.TotalMaterialCost())
		{
			int num;
			if (!forced)
			{
				IHaulEnroute val = (IHaulEnroute)(object)((c is IHaulEnroute) ? c : null);
				if (val != null)
				{
					num = EnrouteUtility.GetSpaceRemainingWithEnroute(val, item.thingDef, pawn);
					goto IL_007c;
				}
			}
			num = c.ThingCountNeeded(item.thingDef);
			goto IL_007c;
			IL_007c:
			int num2 = num;
			if (num2 <= 0)
			{
				continue;
			}
			Thing val2 = (settings.buildFromInventory ? OrganicInventoryShare.FindOrganicStack(((Thing)pawn).Map, pawn, item.thingDef) : InventoryShare.FindSharableStack(((Thing)pawn).Map, pawn, item.thingDef));
			if (val2 != null)
			{
				Job val3 = JobMaker.MakeJob(JobDefOf.HaulToContainer);
				val3.targetA = LocalTargetInfo.op_Implicit(val2);
				val3.targetB = LocalTargetInfo.op_Implicit((Thing)c);
				val3.targetC = LocalTargetInfo.op_Implicit((Thing)c);
				val3.count = Mathf.Min(num2, val2.stackCount);
				val3.haulMode = (HaulMode)3;
				if (settings.shareMeetInMiddle)
				{
					SharedInventoryApproach.MaybeApproach(val2, pawn);
				}
				__result = val3;
				object[] obj = new object[5]
				{
					pawn,
					val3.count,
					((Def)item.thingDef).label,
					null,
					null
				};
				IThingHolder parentHolder = val2.ParentHolder;
				obj[3] = ((Pawn_InventoryTracker)(((parentHolder is Pawn_InventoryTracker) ? parentHolder : null)?)).pawn;
				obj[4] = c;
				HDLog.Dbg(string.Format("{0} will fetch {1}x {2} from {3} for {4}.", obj));
				break;
			}
		}
	}
}
