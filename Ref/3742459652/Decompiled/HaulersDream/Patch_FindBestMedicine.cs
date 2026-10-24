using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(HealthAIUtility), "FindBestMedicine")]
public static class Patch_FindBestMedicine
{
	[HarmonyPriority(200)]
	public static void Postfix(ref Thing __result, Pawn healer, Pawn patient, bool onlyUseInventory)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Invalid comparison between Unknown and I4
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.medicineFromVehiclesAway || __result != null || onlyUseInventory || healer == null || patient == null || !healer.IsColonist || !((Thing)patient).Spawned)
		{
			return;
		}
		Map map = ((Thing)healer).Map;
		if (map == null || map.IsPlayerHome || (patient.playerSettings != null && (int)patient.playerSettings.medCare <= 1) || Medicine.GetMedicineCountToFullyHeal(patient) <= 0)
		{
			return;
		}
		Thing val = __result;
		try
		{
			Thing val2 = FindCarriedMedicine(healer, patient, map);
			if (val2 != null)
			{
				__result = val2;
			}
		}
		catch (Exception ex)
		{
			__result = val;
			HDGuard.SeamDegraded(ex, "HealthAIUtility.FindBestMedicine (HD medicine-on-wheels)", healer, "kept vanilla's result (no carried medicine found by HD), so tending still works.");
		}
	}

	private static Thing FindCarriedMedicine(Pawn healer, Pawn patient, Map map)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		MedicalCareCategory care = (MedicalCareCategory)((patient.playerSettings == null) ? 1 : ((int)patient.playerSettings.medCare));
		Thing val = null;
		float num = -1f;
		int num2 = int.MaxValue;
		List<Pawn> list = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
		for (int i = 0; i < list.Count; i++)
		{
			Pawn val2 = list[i];
			if (!InventoryShare.IsEligibleCarrier(val2, healer) || VehicleFrameworkCompat.InVehicle((Thing)(object)val2))
			{
				continue;
			}
			ThingOwner<Thing> val3 = val2.inventory?.innerContainer;
			if (val3 == null || ((ThingOwner)val3).Count == 0 || ForbidUtility.IsForbidden(((Thing)val2).Position, healer))
			{
				continue;
			}
			Thing val4 = BestMedicineIn((ThingOwner)(object)val3, healer, care);
			if (val4 != null && ReachabilityUtility.CanReach(healer, LocalTargetInfo.op_Implicit((Thing)(object)val2), (PathEndMode)2, (Danger)2, false, false, (TraverseMode)0))
			{
				float statValueAbstract = StatExtension.GetStatValueAbstract((BuildableDef)(object)val4.def, StatDefOf.MedicalPotency, (ThingDef)null);
				int num3 = IntVec3Utility.ManhattanDistanceFlat(((Thing)healer).Position, ((Thing)val2).Position);
				if (val == null || statValueAbstract > num || (statValueAbstract == num && num3 < num2))
				{
					val = val4;
					num = statValueAbstract;
					num2 = num3;
				}
			}
		}
		return val;
	}

	private static Thing BestMedicineIn(ThingOwner inv, Pawn healer, MedicalCareCategory care)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		Thing val = null;
		float num = -1f;
		for (int i = 0; i < inv.Count; i++)
		{
			Thing val2 = inv[i];
			if (val2 != null && val2.def.IsMedicine && MedicalCareUtility.AllowsMedicine(care, val2.def) && !ForbidUtility.IsForbidden(val2, healer) && ReservationUtility.CanReserve(healer, LocalTargetInfo.op_Implicit(val2), 10, 1, (ReservationLayerDef)null, false))
			{
				float statValueAbstract = StatExtension.GetStatValueAbstract((BuildableDef)(object)val2.def, StatDefOf.MedicalPotency, (ThingDef)null);
				if (val == null || statValueAbstract > num)
				{
					val = val2;
					num = statValueAbstract;
				}
			}
		}
		return val;
	}
}
