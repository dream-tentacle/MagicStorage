using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(CompTransporter), "SubtractFromToLoadList")]
public static class Patch_SubtractFromToLoadList
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadTransporters ?? true;
	}

	private static bool Prefix(CompTransporter __instance, Thing t, int count, bool sendMessageOnFinished, ref int __result)
	{
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		if (!Global.IsExecutingManagedUnload)
		{
			return true;
		}
		List<TransferableOneWay> leftToLoad = __instance.leftToLoad;
		if (leftToLoad == null)
		{
			__result = 0;
			return false;
		}
		TransferableOneWay val = Global.FindBestMatchFor(t, count, leftToLoad);
		if (val == null)
		{
			__result = 0;
			return false;
		}
		int countToTransfer = ((Transferable)val).CountToTransfer;
		if (countToTransfer <= 0)
		{
			__result = 0;
			return false;
		}
		int num = ((count < countToTransfer) ? count : countToTransfer);
		int value = countToTransfer - num;
		if (!Global.ForceTo(val, value))
		{
			Global.ForceToViaReflection(val, value);
		}
		if (((Transferable)val).CountToTransfer <= 0)
		{
			leftToLoad.Remove(val);
		}
		if (sendMessageOnFinished && !__instance.AnyInGroupHasAnythingLeftToLoad)
		{
			CompShuttle comp = ((ThingComp)__instance).parent.GetComp<CompShuttle>();
			if (comp == null || comp.AllRequiredThingsLoaded)
			{
				if (comp != null)
				{
					Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("MessageFinishedLoadingShuttle", NamedArgumentUtility.Named((object)((ThingComp)__instance).parent, "SHUTTLE"))), LookTargets.op_Implicit((Thing)(object)((ThingComp)__instance).parent), MessageTypeDefOf.TaskCompletion, true);
				}
				else
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("MessageFinishedLoadingTransporters")), LookTargets.op_Implicit((Thing)(object)((ThingComp)__instance).parent), MessageTypeDefOf.TaskCompletion, true);
				}
			}
		}
		__result = num;
		return false;
	}
}
