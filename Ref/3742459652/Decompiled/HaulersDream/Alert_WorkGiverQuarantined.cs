using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

public class Alert_WorkGiverQuarantined : Alert
{
	public Alert_WorkGiverQuarantined()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		base.defaultPriority = (AlertPriority)1;
	}

	public override AlertReport GetReport()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		if (!WorkGiverBlocklist.AnyQuarantined)
		{
			return AlertReport.Inactive;
		}
		return AlertReport.Active;
	}

	public override string GetLabel()
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Alert.WorkGiverQuarantined", NamedArgument.op_Implicit(WorkGiverBlocklist.QuarantinedCount)));
	}

	public override TaggedString GetExplanation()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		return TranslatorFormattedStringExtensions.Translate("HaulersDream.Alert.WorkGiverQuarantinedDesc", NamedArgument.op_Implicit(GenText.ToLineList((IList<string>)Lines(), "  - ")));
	}

	private static List<string> Lines()
	{
		List<string> list = new List<string>();
		foreach (QuarantinedWork item in WorkGiverBlocklist.Quarantined)
		{
			list.Add(Line(item));
		}
		return list;
	}

	private static string Line(QuarantinedWork work)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		if (work.Naming == QuarantineNaming.NameTheMod && !GenText.NullOrEmpty(work.Source))
		{
			return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Alert.QuarantineLine.KnownSource", NamedArgument.op_Implicit(work.Work), NamedArgument.op_Implicit(work.Source)));
		}
		return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Alert.QuarantineLine.UnknownSource", NamedArgument.op_Implicit(work.Work)));
	}
}
