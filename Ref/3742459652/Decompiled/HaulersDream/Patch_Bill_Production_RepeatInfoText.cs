using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(/*Could not decode attribute arguments.*/)]
public static class Patch_Bill_Production_RepeatInfoText
{
	private static void Postfix(Bill_Production __instance, ref string __result)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
		if (instance != null && instance.IsBatchBill((Bill)(object)__instance) && CraftBatchPlanner.BatchModeAvailable((Bill)(object)__instance))
		{
			__result = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Batch.RowMarker", NamedArgument.op_Implicit(instance.BatchSizeOf((Bill)(object)__instance))) + __result);
		}
	}
}
