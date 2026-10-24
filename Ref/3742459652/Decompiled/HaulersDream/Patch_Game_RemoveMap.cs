using HarmonyLib;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(Game), "DeinitAndRemoveMap")]
public static class Patch_Game_RemoveMap
{
	private static void Prefix(Map map)
	{
		HaulersDreamGameComponent.Instance?.Notify_LoadMapRemoved(map);
	}
}
