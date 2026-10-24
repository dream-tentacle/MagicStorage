using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace HaulersDream;

public static class ItemPolicyCompat
{
	private static bool initialized;

	private static bool active;

	private static FieldInfo policiesField;

	private static MethodInfo getEntryMethod;

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

	private static void Init()
	{
		initialized = true;
		Type type = AccessTools.TypeByName("_ItemPolicy.ItemPolicyUtility");
		if (!(type == null))
		{
			policiesField = AccessTools.Field(type, "policies");
			getEntryMethod = AccessTools.Method(type, "GetItemPolicyEntry", new Type[2]
			{
				typeof(Pawn),
				typeof(ThingDef)
			}, (Type[])null);
			active = policiesField != null && getEntryMethod != null;
			if (active)
			{
				Log.Message("[Hauler's Dream] Item Policy detected — its per-pawn inventory-stock counts are kept during surplus unload (so HD won't fight its re-fetch).");
			}
			else
			{
				HDLog.Warn("Item Policy present but its policy API did not resolve (a version/rename?); its kept inventory items may be unloaded as surplus. Vanilla keep counts still work.");
			}
		}
	}

	public static int KeepCount(Pawn pawn, ThingDef def)
	{
		if (!IsActive || pawn == null || def == null)
		{
			return 0;
		}
		if (!(policiesField.GetValue(null) is IDictionary dictionary) || !dictionary.Contains(pawn))
		{
			return 0;
		}
		return Math.Max(0, (int)getEntryMethod.Invoke(null, new object[2] { pawn, def }));
	}
}
