using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;

namespace HaulersDream;

[HarmonyPatch(typeof(Bill_Production), "Clone")]
public static class Patch_Bill_Production_Clone
{
	internal sealed class CarryState
	{
		public int size;

		public int overshoot;

		public CarryState(int size, int overshoot)
		{
			this.size = size;
			this.overshoot = overshoot;
		}
	}

	internal static readonly ConditionalWeakTable<Bill, CarryState> Carry = new ConditionalWeakTable<Bill, CarryState>();

	private static void Postfix(Bill_Production __instance, ref Bill __result)
	{
		Bill obj = __result;
		Bill_Production val = (Bill_Production)(object)((obj is Bill_Production) ? obj : null);
		if (val != null)
		{
			HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
			int size = 0;
			int overshoot = 0;
			CarryState value;
			if (instance != null && instance.IsBatchBill((Bill)(object)__instance))
			{
				size = instance.BatchSizeOf((Bill)(object)__instance);
				overshoot = instance.BatchOvershootOf((Bill)(object)__instance);
			}
			else if (Carry.TryGetValue((Bill)(object)__instance, out value))
			{
				size = value.size;
				overshoot = value.overshoot;
			}
			Carry.Remove((Bill)(object)val);
			Carry.Add((Bill)(object)val, new CarryState(size, overshoot));
		}
	}
}
