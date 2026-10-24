using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace HaulersDream;

[HarmonyPatch(typeof(BillStack), "AddBill")]
public static class Patch_BillStack_AddBill_BatchDefault
{
	private static void Postfix(Bill bill)
	{
		HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
		if (instance == null)
		{
			return;
		}
		if (Patch_Bill_Production_Clone.Carry.TryGetValue(bill, out var value))
		{
			Patch_Bill_Production_Clone.Carry.Remove(bill);
			if (value.size > 0)
			{
				instance.SetBatch(bill, on: true, value.size);
				instance.SetBatchOvershoot(bill, value.overshoot);
			}
		}
		else
		{
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings != null && settings.batchByDefault && !instance.IsBatchBill(bill) && CraftBatchPlanner.CanBatch(bill))
			{
				instance.SetBatch(bill, on: true, Mathf.Max(1, settings.defaultBatchSize));
			}
		}
	}
}
