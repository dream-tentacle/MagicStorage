using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(Pawn), "GetGizmos")]
[StaticConstructorOnStartup]
public static class Patch_Pawn_GetGizmos
{
	private static readonly Texture2D DropIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Drop", false) ?? BaseContent.BadTex;

	private const float GizmoOrder = float.MaxValue;

	private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
	{
		foreach (Gizmo item in __result)
		{
			yield return item;
		}
		HaulersDreamSettings s = HaulersDreamMod.Settings;
		if (s == null || ((Thing)__instance).Faction != Faction.OfPlayerSilentFail)
		{
			yield break;
		}
		CompHauledToInventory comp = ((ThingWithComps)__instance).GetComp<CompHauledToInventory>();
		if (comp == null)
		{
			yield break;
		}
		if (s.showAutoHaulGizmo && YieldRouter.IsRaceEligible(__instance))
		{
			yield return (Gizmo)new Command_Toggle
			{
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Gizmo.AutoHaul")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Gizmo.AutoHaulDesc")),
				icon = (Texture)(object)DropIcon,
				Order = float.MaxValue,
				isActive = () => comp.autoHaulYields,
				toggleAction = delegate
				{
					MultiplayerCompat.SetAutoHaulYields(__instance, !comp.autoHaulYields);
				}
			};
		}
		if (s.hideGizmo)
		{
			yield break;
		}
		bool num = comp.PeekHashSet().Count > 0;
		bool flag = !CaravanFormingUtility.IsFormingCaravan(__instance) && (s.unloadAllSurplus ? SurplusCache.HasAnySurplus(__instance) : (s.HasAnySurplusProducingRule && SurplusCache.HasAnyRuledSurplus(__instance)));
		if (num || flag)
		{
			Command_Action val = new Command_Action
			{
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Gizmo.UnloadNow")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Gizmo.UnloadNowDesc")),
				icon = (Texture)(object)DropIcon,
				Order = float.MaxValue,
				action = delegate
				{
					bool isDownEvent = KeyBindingDefOf.QueueOrder.IsDownEvent;
					MultiplayerCompat.UnloadInventoryNow(__instance, isDownEvent);
				}
			};
			if (__instance.Drafted)
			{
				((Gizmo)val).Disable(TaggedString.op_Implicit(Translator.Translate("HaulersDream.Gizmo.UnloadNowDrafted")));
			}
			yield return (Gizmo)(object)val;
		}
	}
}
