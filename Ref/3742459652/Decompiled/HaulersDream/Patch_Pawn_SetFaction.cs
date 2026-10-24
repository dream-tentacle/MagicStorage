using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(Pawn), "SetFaction")]
public static class Patch_Pawn_SetFaction
{
	private static void Prefix(Pawn __instance, out Faction __state)
	{
		__state = ((Thing)__instance).Faction;
	}

	private static void Postfix(Pawn __instance, Faction newFaction, Faction __state)
	{
		QuestPawnReversion.OnFactionChanged(__instance, __state, newFaction);
	}
}
