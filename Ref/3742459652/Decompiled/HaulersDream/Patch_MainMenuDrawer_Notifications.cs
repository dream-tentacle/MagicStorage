using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(MainMenuDrawer), "MainMenuOnGUI")]
internal static class Patch_MainMenuDrawer_Notifications
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		if ((int)Current.ProgramState == 0)
		{
			ReportNotifications.OnMainMenuGUI();
		}
	}
}
