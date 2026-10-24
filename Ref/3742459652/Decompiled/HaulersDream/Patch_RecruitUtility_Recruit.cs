using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(RecruitUtility), "Recruit")]
public static class Patch_RecruitUtility_Recruit
{
	private static void Postfix(Pawn pawn, Faction faction)
	{
		if (faction != null && faction == Faction.OfPlayerSilentFail)
		{
			QuestPawnReversion.NotifyRecruited(pawn);
		}
	}
}
