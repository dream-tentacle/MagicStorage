using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(StoreUtility), "TryFindBestBetterStoreCellFor")]
public static class Patch_TryFindBestBetterStoreCellFor_HaulToStack
{
	private static void Postfix(Thing t, Pawn carrier, Map map, Faction faction, ref IntVec3 foundCell, bool needAccurateResult, bool __result)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		if (!__result || !needAccurateResult)
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && settings.haulToStack && carrier != null && map != null && t != null && faction == Faction.OfPlayerSilentFail && HaulToStackPolicy.CanTopUp(t.def.stackLimit))
		{
			IntVec3 val = HaulToStack.FindStackCell(t, carrier, map, faction, foundCell);
			if (((IntVec3)(ref val)).IsValid)
			{
				foundCell = val;
			}
		}
	}
}
