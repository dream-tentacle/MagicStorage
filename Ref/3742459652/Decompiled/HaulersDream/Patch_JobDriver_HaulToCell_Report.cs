using System.Collections.Generic;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

[HarmonyPatch(typeof(JobDriver), "GetReport")]
public static class Patch_JobDriver_HaulToCell_Report
{
	private static void Postfix(JobDriver __instance, ref string __result)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		if (GenText.NullOrEmpty(__result))
		{
			return;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return;
		}
		Job val = __instance?.job;
		if (val?.def == null)
		{
			return;
		}
		JobReportKind kind;
		IntVec3 cell;
		if (settings.storageRouting && val.def == JobDefOf.HaulToCell)
		{
			StorageRouting.RelocationInfo relocationInfo = StorageRouting.RelocationData(val);
			if (relocationInfo == null)
			{
				return;
			}
			kind = JobReportKind.CloserTo;
			cell = relocationInfo.consumeCell;
		}
		else
		{
			if (val.def != HaulersDreamDefOf.HaulersDream_BulkHaul)
			{
				return;
			}
			Patch_Pawn_JobTracker_EnRoutePickup.EnRouteInfo enRouteInfo = Patch_Pawn_JobTracker_EnRoutePickup.EnRouteData(val);
			if (enRouteInfo == null || !settings.enRoutePickup)
			{
				return;
			}
			kind = JobReportKind.EnRoute;
			cell = enRouteInfo.jobCell;
		}
		if (!JobReportPolicy.RewritesReport(kind))
		{
			return;
		}
		string text = JobReportPolicy.ReportKeyFor(kind, isLoad: true);
		if (text != null)
		{
			string text2 = __result.TrimEnd().TrimEnd('.');
			if (JobReportPolicy.UsesDestination(kind))
			{
				string text3 = DestinationLabel(__instance, cell);
				__result = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate(text, NamedArgumentUtility.Named((object)text2, "ORIGINAL"), NamedArgumentUtility.Named((object)text3, "DESTINATION")));
			}
			else
			{
				__result = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate(text, NamedArgumentUtility.Named((object)text2, "ORIGINAL")));
			}
		}
	}

	private unsafe static string DestinationLabel(JobDriver driver, IntVec3 cell)
	{
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Invalid comparison between Unknown and I4
		object obj;
		if (driver == null)
		{
			obj = null;
		}
		else
		{
			Pawn pawn = driver.pawn;
			obj = ((pawn != null) ? ((Thing)pawn).MapHeld : null);
		}
		Map val = (Map)obj;
		if (val != null && ((IntVec3)(ref cell)).IsValid && GenGrid.InBounds(cell, val))
		{
			Building edifice = GridsUtility.GetEdifice(cell, val);
			if (edifice != null && !GenText.NullOrEmpty(((Entity)edifice).LabelShortCap))
			{
				return ((Entity)edifice).LabelShortCap;
			}
			List<Thing> thingList = GridsUtility.GetThingList(cell, val);
			for (int i = 0; i < thingList.Count; i++)
			{
				Thing val2 = thingList[i];
				if (val2 != null && val2.def != null && (int)val2.def.category != 1 && !GenText.NullOrEmpty(((Entity)val2).LabelShortCap))
				{
					return ((Entity)val2).LabelShortCap;
				}
			}
			ZoneManager zoneManager = val.zoneManager;
			Zone val3 = ((zoneManager != null) ? zoneManager.ZoneAt(cell) : null);
			if (val3 != null && !GenText.NullOrEmpty(val3.label))
			{
				return val3.label;
			}
		}
		if (((IntVec3)(ref cell)).IsValid)
		{
			return ((object)(*(IntVec3*)(&cell))/*cast due to constrained. prefix*/).ToString();
		}
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.JobReport.DefaultDestination"));
	}
}
