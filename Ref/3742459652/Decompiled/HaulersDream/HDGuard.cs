using System;
using System.Collections.Concurrent;
using Verse;

namespace HaulersDream;

public static class HDGuard
{
	private static readonly ConcurrentDictionary<string, int> breadcrumbCounts = new ConcurrentDictionary<string, int>();

	private static bool IsRepeatCheckpoint(int count)
	{
		if (count != 10 && count != 100 && count != 1000)
		{
			return count % 10000 == 0;
		}
		return true;
	}

	public static Exception SeamThrew(Exception ex, string seam, Pawn pawn, string consequence)
	{
		if (ex == null)
		{
			return null;
		}
		Report(ex, seam, pawn, consequence, KeyFor(seam, "threw", ex));
		return ex;
	}

	public static Exception SeamContained(Exception ex, string seam, Pawn pawn, string consequence)
	{
		if (ex == null)
		{
			return null;
		}
		Report(ex, seam, pawn, consequence, KeyFor(seam, "contained", ex));
		return null;
	}

	private static string KeyFor(string seam, string channel, Exception ex)
	{
		return seam + "|" + channel + "|" + HDFault.ExceptionTypeName(ex);
	}

	public static void SeamDegraded(Exception ex, string seam, Pawn pawn, string kept)
	{
		if (ex == null)
		{
			return;
		}
		string text = KeyFor(seam, "degraded", ex);
		if (Breadcrumb(text) != 1)
		{
			return;
		}
		try
		{
			HDLog.ErrOnce("Hauler's Dream's enhancement at " + seam + " threw while selecting for " + (((pawn != null) ? ((Entity)pawn).LabelShort : null) ?? "a pawn") + " and stood down for this scan. " + kept + " " + OriginClause(ex) + "\n" + HDFault.Render(ex), text.GetHashCode());
		}
		catch (Exception reportFailed)
		{
			NoteReportFailure(text, reportFailed);
		}
	}

	private static void Report(Exception ex, string seam, Pawn pawn, string consequence, string key)
	{
		if (Breadcrumb(key) != 1)
		{
			return;
		}
		try
		{
			HDLog.ErrOnce("An exception surfaced at " + seam + " (a method HD patches) while selecting for " + (((pawn != null) ? ((Entity)pawn).LabelShort : null) ?? "a pawn") + " - " + consequence + " " + OriginClause(ex) + "\n" + HDFault.Render(ex), key.GetHashCode());
		}
		catch (Exception reportFailed)
		{
			NoteReportFailure(key, reportFailed);
		}
	}

	private static string OriginClause(Exception ex)
	{
		string text = HDFault.DescribeOwnFrame(ex);
		string text2 = HDFault.DescribeOrigin(ex);
		if (text != null && text2 != null)
		{
			return "Hauler's Dream's own code IS in this stack (" + text + "), and so is another mod's (" + text2 + "). Which of the two threw cannot be settled from here, so treat neither as established - reporting this to Hauler's Dream with the log attached is what lets it be traced. The full stack is below.";
		}
		if (text != null)
		{
			return "The only mod-owned frame in this stack is Hauler's Dream's own (" + text + "), so treat this as a Hauler's Dream bug and report it to Hauler's Dream, with the log attached. The full stack is below.";
		}
		if (text2 != null)
		{
			return "The innermost frame belonging to another mod is " + text2 + ", so that is the most likely source; please report it there. The full stack is below.";
		}
		if (HDFault.OriginUnknown(ex))
		{
			return "This exception carries no readable stack frames, so where it came from could not be determined here — the game's own error report shows the full call stack.";
		}
		string text3 = HDFault.DescribeInnermostFrame(ex);
		if (text3 != null)
		{
			return "No frame in this stack belongs to a mod, so the mod at fault could not be identified here; the innermost readable frame is " + text3 + ". That is not proof Hauler's Dream caused it either (a stack records what a method called, never who called it) — the game's own error report shows the full call stack.";
		}
		return "Where this exception came from could not be determined here; the game's own error report shows the full call stack.";
	}

	private static int Breadcrumb(string key)
	{
		try
		{
			int num = breadcrumbCounts.AddOrUpdate(key, 1, (string _, int c) => c + 1);
			if (num == 1)
			{
				HDDebugLog.Enqueue(DateTime.Now.ToString("MM-dd HH:mm:ss") + " ERR [seam] " + key);
			}
			else if (IsRepeatCheckpoint(num))
			{
				HDDebugLog.Enqueue(DateTime.Now.ToString("MM-dd HH:mm:ss") + " ERR [seam] " + key + " (recurred " + num + "x this session)");
			}
			return num;
		}
		catch
		{
			return 1;
		}
	}

	private static void NoteReportFailure(string key, Exception reportFailed)
	{
		try
		{
			HDDebugLog.Enqueue(DateTime.Now.ToString("MM-dd HH:mm:ss") + " ERR [seam] " + key + " - building the full report threw " + reportFailed.GetType().Name + "; the minimal breadcrumb above is all that could be captured.");
		}
		catch
		{
		}
	}
}
