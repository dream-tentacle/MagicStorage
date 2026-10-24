using HarmonyLib;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(HaulAIUtility), "HaulToStorageJob")]
public static class Patch_HaulToStorageJob_ForcedScope
{
	private static void Prefix(bool __2)
	{
		if (__2)
		{
			StorageCommitments.PushForcedOrder();
		}
	}

	private static void Finalizer(bool __2)
	{
		if (__2)
		{
			StorageCommitments.PopForcedOrder();
		}
	}
}
