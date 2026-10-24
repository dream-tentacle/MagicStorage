using System;
using System.Reflection;
using HarmonyLib;
using HaulersDream.Core;
using Verse;

namespace HaulersDream;

public static class CommonSenseCompat
{
	private static bool initialized;

	private static bool active;

	private static FieldInfo advCleaningField;

	private static FieldInfo advHaulAllField;

	[ThreadStatic]
	private static int ownsCacheTick;

	[ThreadStatic]
	private static bool ownsCacheValue;

	[ThreadStatic]
	private static bool ownsCacheValid;

	private const string HaulAllIngredientsLabelKey = "advanced_haul_all_ings_label";

	public const string ModName = "Common Sense";

	public static bool IsActive
	{
		get
		{
			if (!initialized)
			{
				Init();
			}
			return active;
		}
	}

	public static bool OwnsDoBillDriver
	{
		get
		{
			if (!initialized)
			{
				Init();
			}
			if (!active)
			{
				return false;
			}
			Game game = Current.Game;
			int? obj;
			if (game == null)
			{
				obj = null;
			}
			else
			{
				TickManager tickManager = game.tickManager;
				obj = ((tickManager != null) ? new int?(tickManager.TicksGame) : ((int?)null));
			}
			int num = obj ?? (-1);
			if (ownsCacheValid && ownsCacheTick == num)
			{
				return ownsCacheValue;
			}
			bool value;
			bool num2 = TryReadOption(advCleaningField, out value);
			bool value2;
			bool flag = TryReadOption(advHaulAllField, out value2);
			bool fieldsReadable = num2 && flag;
			bool result = CommonSenseCedePolicy.CommonSenseOwnsDoBillDriver(active, fieldsReadable, value, value2);
			ownsCacheTick = num;
			ownsCacheValue = result;
			ownsCacheValid = true;
			return result;
		}
	}

	public static bool GathersIngredients
	{
		get
		{
			if (!initialized)
			{
				Init();
			}
			if (!active)
			{
				return false;
			}
			bool value;
			bool fieldsReadable = TryReadOption(advHaulAllField, out value);
			return CommonSenseCedePolicy.CommonSenseGathersIngredients(active, fieldsReadable, value);
		}
	}

	public static string HaulAllIngredientsOptionLabel
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			if (!Translator.CanTranslate("advanced_haul_all_ings_label"))
			{
				return "Pawns are encouraged to pick up all ingredients before hauling them to the crafting place";
			}
			return TaggedString.op_Implicit(Translator.Translate("advanced_haul_all_ings_label"));
		}
	}

	public static bool BatchSuppressedByCommonSense
	{
		get
		{
			if (OwnsDoBillDriver)
			{
				return !(HaulersDreamMod.Settings?.allowBatchUnderCommonSense ?? true);
			}
			return false;
		}
	}

	static CommonSenseCompat()
	{
		CacheRegistry.Register(ClearTickCaches);
	}

	private static void ClearTickCaches()
	{
		ownsCacheValid = false;
		ownsCacheTick = -1;
		ownsCacheValue = false;
	}

	private static bool TryReadOption(FieldInfo field, out bool value)
	{
		if (field?.GetValue(null) is bool flag)
		{
			value = flag;
			return true;
		}
		value = false;
		return false;
	}

	private static void Init()
	{
		initialized = true;
		Type type = AccessTools.TypeByName("CommonSense.Settings");
		if (!(type == null))
		{
			active = true;
			advCleaningField = AccessTools.Field(type, "adv_cleaning");
			advHaulAllField = AccessTools.Field(type, "adv_haul_all_ings");
			bool flag = advCleaningField != null && advHaulAllField != null;
			HDLog.Msg("Common Sense detected — HD cedes the crafting-ingredient gather to it whenever its haul-all-ingredients option is on" + (flag ? "." : " (toggle fields unresolved — treating CS as gathering as a safe fallback)."));
			if (!flag)
			{
				HDLog.Warn("Common Sense present but Settings.adv_cleaning" + ((advCleaningField == null) ? " (UNRESOLVED)" : "") + " / adv_haul_all_ings" + ((advHaulAllField == null) ? " (UNRESOLVED)" : "") + " did not resolve; HD ceding the crafting-ingredient gather to CS unconditionally (its own gather conversions stay off while CS is installed).");
			}
		}
	}
}
