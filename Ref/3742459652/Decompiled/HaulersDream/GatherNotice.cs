using HaulersDream.Core;
using Verse;

namespace HaulersDream;

public static class GatherNotice
{
	private static bool PlainGatherEnabled
	{
		get
		{
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings == null)
			{
				return true;
			}
			return GatherOwnershipPolicy.PlainGatherEnabled(settings.inventoryCraftDeliver, settings.shareForCrafting, settings.markForUnload);
		}
	}

	public static BenchGatherNotice Current => GatherOwnershipPolicy.ResolveNotice(CommonSenseCompat.GathersIngredients, PlainGatherEnabled);

	public static bool BenchSwitchGovernsPlainGather => GatherOwnershipPolicy.BenchSwitchGovernsPlainGather(CommonSenseCompat.GathersIngredients, PlainGatherEnabled);

	public static string Text(BenchGatherNotice notice)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		return notice switch
		{
			BenchGatherNotice.ForeignModGathers => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Notice.ForeignModGathers", NamedArgument.op_Implicit("Common Sense"), NamedArgument.op_Implicit(CommonSenseCompat.HaulAllIngredientsOptionLabel))), 
			BenchGatherNotice.GlobalGatherOff => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Notice.GatherOffGlobally", NamedArgument.op_Implicit(OffSettingLabel()))), 
			_ => null, 
		};
	}

	private static string OffSettingLabel()
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && !settings.shareForCrafting)
		{
			return TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.ShareForCrafting"));
		}
		if (settings != null && !settings.markForUnload)
		{
			return TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.MarkForUnload"));
		}
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.Setting.InventoryCraftDeliver"));
	}
}
