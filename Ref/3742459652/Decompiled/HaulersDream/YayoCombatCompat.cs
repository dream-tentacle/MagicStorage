using System;
using Verse;

namespace HaulersDream;

public static class YayoCombatCompat
{
	private static bool initialized;

	private static ThingCategoryDef ammoCategory;

	private static void Init()
	{
		initialized = true;
		ammoCategory = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("yy_ammo_category");
		if (ammoCategory != null)
		{
			HDLog.Msg("Yayo's Combat 3 detected — carried ammo excluded from surplus unloading.");
		}
	}

	public static bool IsCarriedAmmo(Thing thing)
	{
		ThingDef val = thing?.def;
		if (val == null)
		{
			return false;
		}
		if (!initialized)
		{
			Init();
		}
		if (ammoCategory != null && val.thingCategories != null && val.thingCategories.Contains(ammoCategory))
		{
			return true;
		}
		if (((Def)val).defName != null)
		{
			return ((Def)val).defName.StartsWith("yy_ammo", StringComparison.Ordinal);
		}
		return false;
	}
}
