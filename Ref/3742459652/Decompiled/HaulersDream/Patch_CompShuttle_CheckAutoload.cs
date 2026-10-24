using HarmonyLib;
using RimWorld;

namespace HaulersDream;

[HarmonyPatch(typeof(CompShuttle), "CheckAutoload")]
public static class Patch_CompShuttle_CheckAutoload
{
	private static bool Prepare()
	{
		return HaulersDreamMod.Settings?.enableBulkLoadTransporters ?? true;
	}

	private static void Prefix(CompShuttle __instance, out bool __state)
	{
		__state = false;
		CompTransporter transporter = __instance.Transporter;
		if (transporter == null)
		{
			return;
		}
		HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
		if (instance != null && instance.LoadAnyClaimsInProgress(transporter.groupID))
		{
			Traverse val = Traverse.Create((object)__instance).Field("autoload");
			if (val.GetValue<bool>())
			{
				__state = true;
				val.SetValue((object)false);
			}
		}
	}

	private static void Postfix(CompShuttle __instance, bool __state)
	{
		if (__state)
		{
			Traverse.Create((object)__instance).Field("autoload").SetValue((object)true);
		}
	}
}
