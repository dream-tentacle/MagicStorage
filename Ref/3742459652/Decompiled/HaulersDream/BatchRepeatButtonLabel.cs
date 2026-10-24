using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class BatchRepeatButtonLabel
{
	public static TaggedString BatchRepeatLabel(Bill_Production bill)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		if (bill?.repeatMode == null)
		{
			return default(TaggedString);
		}
		TaggedString labelCap = ((Def)bill.repeatMode).LabelCap;
		HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
		if (instance != null && instance.IsBatchBill((Bill)(object)bill) && CraftBatchPlanner.BatchModeAvailable((Bill)(object)bill))
		{
			return TaggedString.op_Implicit(TaggedString.op_Implicit(Translator.Translate("HaulersDream.Batch.MenuPrefix")) + ": " + ((TaggedString)(ref labelCap)).Resolve());
		}
		return labelCap;
	}

	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
	{
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Expected O, but got Unknown
		List<CodeInstruction> list = new List<CodeInstruction>(instructions);
		try
		{
			MethodInfo methodInfo = AccessTools.Method(typeof(BatchRepeatButtonLabel), "BatchRepeatLabel", (Type[])null, (Type[])null);
			List<CodeInstruction> list2 = new List<CodeInstruction>(list.Count);
			int num = 0;
			for (int i = 0; i < list.Count; i++)
			{
				if (i < list.Count - 1 && list[i].opcode == OpCodes.Ldfld && list[i].operand is FieldInfo { Name: "repeatMode" } fieldInfo && fieldInfo.DeclaringType != null && typeof(Bill).IsAssignableFrom(fieldInfo.DeclaringType) && (list[i + 1].opcode == OpCodes.Callvirt || list[i + 1].opcode == OpCodes.Call) && list[i + 1].operand is MethodInfo { Name: "get_LabelCap" })
				{
					CodeInstruction val = new CodeInstruction(OpCodes.Call, (object)methodInfo);
					val.labels.AddRange(list[i].labels);
					val.blocks.AddRange(list[i].blocks);
					val.labels.AddRange(list[i + 1].labels);
					val.blocks.AddRange(list[i + 1].blocks);
					list2.Add(val);
					i++;
					num++;
				}
				else
				{
					list2.Add(list[i]);
				}
			}
			if (num == 0)
			{
				HDLog.Warn("batch repeat-label transpiler: no repeatMode.LabelCap read found in " + method?.DeclaringType?.Name + "." + method?.Name + "; the button keeps the plain label.");
				return list;
			}
			return list2;
		}
		catch (Exception ex)
		{
			HDGuard.SeamDegraded(ex, "batch repeat-label transpiler (" + method?.DeclaringType?.Name + "." + method?.Name + ")", null, "the repeat-mode button keeps the plain vanilla label.");
			return list;
		}
	}
}
