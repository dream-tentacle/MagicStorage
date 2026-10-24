using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(JobGiver_DropUnusedInventory), "ShouldKeepDrugInInventory")]
public static class Patch_JobGiver_DropUnusedInventory_ShouldKeepDrug
{
	private const string SeamBase = "JobGiver_DropUnusedInventory.ShouldKeepDrugInInventory (HD drop protection)";

	private static void Postfix(ref bool __result, Pawn pawn, Thing drug)
	{
		if (__result || pawn == null || drug == null)
		{
			return;
		}
		ThingOwner<Thing> val = pawn.inventory?.innerContainer;
		if (val != null && ((ThingOwner)val).Contains(drug))
		{
			CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
			if (comp != null && (comp.GetHashSet().Contains(drug) || comp.IsKeptDef(drug.def)))
			{
				__result = true;
			}
		}
	}

	private static Exception Finalizer(Exception __exception, ref bool __result, Pawn pawn, Thing drug)
	{
		if (__exception == null)
		{
			return null;
		}
		string seam = "JobGiver_DropUnusedInventory.ShouldKeepDrugInInventory (HD drop protection) [drug def: " + (((Def)(drug?.def?)).defName ?? "unknown") + "]";
		if (HDFault.InvolvesHaulersDream(__exception))
		{
			return HDGuard.SeamThrew(__exception, seam, pawn, "this colonist's keep-or-drop check for that drug failed, so the drop loop and the right-click \"Pick up\" options for it are both unusable.");
		}
		__result = true;
		return HDGuard.SeamContained(__exception, seam, pawn, ContainedConsequence(__exception));
	}

	private static string ContainedConsequence(Exception ex)
	{
		string text = ((ex is ArgumentException) ? " One known source of this exception here is RimWorld's own drug-policy lookup (DrugPolicy's per-drug indexer), which raises an ArgumentException carrying no message when the colonist's drug policy holds no entry for the drug being checked - that lookup runs before any Hauler's Dream code on this path." : string.Empty);
		return "Hauler's Dream answered \"keep this drug in the pack\", which is RimWorld's own fall-through answer for that check, so nothing is dropped and the right-click menu still builds; a failed call would otherwise read as \"drop it\" and the colonist would dump that stack at their feet on every think pass." + text + " Hauler's Dream contains this, it never repairs the policy: rewriting saved drug policies is not something a hauling mod should do.";
	}
}
