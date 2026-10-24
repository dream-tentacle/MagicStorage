using RimWorld;
using Verse;

namespace HaulersDream;

internal static class DrugPolicyLookup
{
	internal static DrugPolicyEntry EntryFor(DrugPolicy policy, ThingDef def)
	{
		if (policy == null || def == null)
		{
			return null;
		}
		for (int i = 0; i < policy.Count; i++)
		{
			DrugPolicyEntry val = policy[i];
			if (val != null && val.drug == def)
			{
				return val;
			}
		}
		return null;
	}

	internal static int TakeToInventoryTotal(DrugPolicy policy, ThingDef def)
	{
		if (policy == null || def == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < policy.Count; i++)
		{
			DrugPolicyEntry val = policy[i];
			if (val != null && val.drug == def && val.takeToInventory > 0)
			{
				num += val.takeToInventory;
			}
		}
		return num;
	}
}
