using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using HarmonyLib;
using HaulersDream.Core;
using Verse;

namespace HaulersDream;

public static class HDLog
{
	public const string Tag = "[Hauler's Dream] ";

	private static readonly ConcurrentDictionary<int, int> stackLoggedCounts = new ConcurrentDictionary<int, int>();

	private static void Emit(string level, string message)
	{
		HDDebugLog.Enqueue(DateTime.Now.ToString("MM-dd HH:mm:ss") + " " + level + " " + message);
	}

	public static string GetReportLog()
	{
		return HDDebugLog.GetReportTail(524288);
	}

	public static void Dbg(string message)
	{
		Emit("DBG", message);
		if (Prefs.DevMode && HaulersDreamMod.Settings != null && HaulersDreamMod.Settings.verboseLogging)
		{
			Log.Message("[Hauler's Dream] " + message);
		}
	}

	public static void Msg(string message)
	{
		Emit("MSG", message);
		Log.Message("[Hauler's Dream] " + message);
	}

	public static void Warn(string message)
	{
		Emit("WARN", message);
		Log.Warning("[Hauler's Dream] " + message);
	}

	public static void WarnOnce(string message, int key)
	{
		Emit("WARN", message);
		Log.WarningOnce("[Hauler's Dream] " + message, key);
	}

	public static void Err(string message)
	{
		Emit("ERR", message);
		Log.Error("[Hauler's Dream] " + message);
	}

	public static void ErrOnce(string message, int key)
	{
		Emit("ERR", message);
		Log.ErrorOnce("[Hauler's Dream] " + message, key);
	}

	private static bool IsRepeatCheckpoint(int count)
	{
		if (count != 10 && count != 100 && count != 1000)
		{
			return count % 10000 == 0;
		}
		return true;
	}

	private static bool HasHdTranspiler(MethodBase method)
	{
		if (method == null)
		{
			return false;
		}
		ReadOnlyCollection<Patch> readOnlyCollection = Harmony.GetPatchInfo(method)?.Transpilers;
		if (readOnlyCollection == null)
		{
			return false;
		}
		foreach (Patch item in readOnlyCollection)
		{
			if (item != null && item.owner == "giwaffed.HaulersDream")
			{
				return true;
			}
		}
		return false;
	}

	public static void UniversalExceptionFinalizer(Exception __exception, MethodBase __originalMethod)
	{
		if (__exception != null)
		{
			string text = ((__originalMethod != null) ? (__originalMethod.DeclaringType?.FullName + "." + __originalMethod.Name) : "a patched method");
			int key = (((__originalMethod != null) ? __originalMethod.GetHashCode() : text.GetHashCode()) * 397) ^ __exception.GetType().GetHashCode();
			int num = stackLoggedCounts.AddOrUpdate(key, 1, (int _, int c) => c + 1);
			if (num == 1)
			{
				string text2 = HDFault.Render(__exception);
				HdInvolvement involvement = BlamePolicy.Classify(HDFault.InvolvesHaulersDream(__exception), HasHdTranspiler(__originalMethod), HaulChurnGuard.PlacementWrapperIsAncestor);
				ErrOnce("an exception passed through " + text + ", a method Hauler's Dream patches. " + BlamePolicy.Describe(involvement) + " Hauler's Dream neither alters nor swallows it. The stack below is the one captured when the exception reached this method; the game's own report continues from here.\n" + text2, key);
			}
			else if (IsRepeatCheckpoint(num))
			{
				Dbg("the exception through " + text + " [" + HDFault.ExceptionTypeName(__exception) + ": " + $"{HDFault.SafeMessage(__exception)}] has now recurred {num}x this session — " + "per-occurrence logging suppressed to protect the report trail.");
			}
		}
	}
}
