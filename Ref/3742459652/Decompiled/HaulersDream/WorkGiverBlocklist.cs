using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using HarmonyLib;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class WorkGiverBlocklist
{
	private static readonly ConcurrentDictionary<Type, int> faultCounts = new ConcurrentDictionary<Type, int>();

	private static readonly ConcurrentDictionary<Type, QuarantinedWork> quarantined = new ConcurrentDictionary<Type, QuarantinedWork>();

	private static volatile bool anyQuarantined;

	[ThreadStatic]
	private static Type scanContextGiver;

	private static readonly ConcurrentDictionary<Type, string> giverPatchOwners = new ConcurrentDictionary<Type, string>();

	private static readonly string[] GiverJobEntryPoints = new string[3] { "JobOnThing", "JobOnCell", "HasJobOnThing" };

	private static MethodBase seamMethod;

	public static bool AnyQuarantined => anyQuarantined;

	public static IEnumerable<QuarantinedWork> Quarantined => quarantined.Values;

	public static int QuarantinedCount => quarantined.Count;

	public static Type ScanContextGiver => scanContextGiver;

	private static MethodBase SeamMethod
	{
		get
		{
			MethodBase result = seamMethod ?? AccessTools.Method(typeof(JobGiver_Work), "TryIssueJobPackage", (Type[])null, (Type[])null);
			seamMethod = result;
			return result;
		}
	}

	public static void NoteGiverPassedGate(WorkGiver giver)
	{
		if (giver != null)
		{
			scanContextGiver = ((object)giver).GetType();
		}
	}

	public static void ClearScanContext()
	{
		scanContextGiver = null;
	}

	public static bool IsQuarantined(WorkGiver giver)
	{
		if (!anyQuarantined || giver == null)
		{
			return false;
		}
		return quarantined.ContainsKey(((object)giver).GetType());
	}

	public static void NoteSeamFault(Exception ex, Type scanGiver, Pawn pawn)
	{
		try
		{
			Type type = HDFault.FirstFrameTypeAssignableTo(ex, typeof(WorkGiver));
			int attribution = ((!(type != null)) ? ((scanGiver != null) ? 1 : 2) : 0);
			type = type ?? scanGiver;
			string text = HDFault.DescribeOrigin(ex);
			string text2 = ((type == null) ? null : ModPatchOwnersOf(type));
			int faultCount = ((!(type == null)) ? faultCounts.AddOrUpdate(type, 1, (Type _, int c) => c + 1) : 0);
			if (WorkGiverQuarantinePolicy.Decide((GiverAttribution)attribution, type != null && HDFault.IsHaulersDream(type.Assembly), text2 != null, text != null, faultCount) == QuarantineVerdict.Quarantine)
			{
				QuarantineNaming quarantineNaming = WorkGiverNamingPolicy.Decide(text != null, text2 != null, HDFault.OwningModName(type.Assembly) != null);
				QuarantinedWork value = new QuarantinedWork(DescribeWork(type), quarantineNaming, (quarantineNaming == QuarantineNaming.NameTheMod) ? text : null);
				if (quarantined.TryAdd(type, value))
				{
					anyQuarantined = true;
					HDLog.Err("the work giver '" + type.FullName + "' threw " + faultCount + " times while RimWorld turned its chosen target into a job for " + (((pawn != null) ? ((Entity)pawn).LabelShort : null) ?? "a pawn") + ". " + SourceSentence(quarantineNaming, text) + " Vanilla does not guard that call, so the throw aborts the pawn's ENTIRE work selection every scan - colonists then do no work at all while still eating and sleeping, which looks like laziness rather than an error. Hauler's Dream has switched THAT work giver off for the rest of this session so every other kind of work keeps running (the giver is not one of Hauler's Dream's own - it never switches those off). That work will no longer happen automatically until the cause is fixed or removed; a player right-click 'Prioritize' still issues a single job (RimWorld builds that order without this check), but the pawn will not carry on with it. Restarting the game clears the list.\n" + HookFacts(type) + "\n" + HDFault.Render(ex));
				}
			}
		}
		catch (Exception ex2)
		{
			try
			{
				HDDebugLog.Enqueue(DateTime.Now.ToString("MM-dd HH:mm:ss") + " ERR [seam] work-giver quarantine bookkeeping threw " + ex2.GetType().Name);
			}
			catch
			{
			}
		}
	}

	private static string SourceSentence(QuarantineNaming naming, string origin)
	{
		if (naming == QuarantineNaming.NameTheMod && !GenText.NullOrEmpty(origin))
		{
			return "The exception's own frames put " + origin + " on the path to the throw, so that is the most likely source - likely, not proven: a stack records what a method called, never who called it.";
		}
		return "No frame in this exception resolves to a mod's own code, so the source could not be identified from the error itself. That is not a finding about anyone in particular: a Harmony-patched method runs as a DynamicMethod whose frame names nothing, and any Exception-returning finalizer deeper in the call chain has already reset the trace before it reaches here. The full error is below - attaching it to a bug report is what allows the source to be worked out.";
	}

	private static string DescribeWork(Type giverType)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				WorkGiverDef val = allDefsListForReading[i];
				if (val != null && val.giverClass == giverType && !GenText.NullOrEmpty(((Def)val).label))
				{
					return ((object)((Def)val).LabelCap/*cast due to constrained. prefix*/).ToString() + " (" + giverType.FullName + ")";
				}
			}
		}
		catch
		{
		}
		return giverType.FullName;
	}

	private static string ModPatchOwnersOf(Type giverType)
	{
		return giverPatchOwners.GetOrAdd(giverType, ComputeModPatchOwners);
	}

	private static string ComputeModPatchOwners(Type giverType)
	{
		try
		{
			List<string> list = null;
			for (int i = 0; i < GiverJobEntryPoints.Length; i++)
			{
				MethodInfo methodInfo = AccessTools.Method(giverType, GiverJobEntryPoints[i], (Type[])null, (Type[])null);
				if (methodInfo == null)
				{
					continue;
				}
				Patches patchInfo = Harmony.GetPatchInfo((MethodBase)methodInfo);
				ReadOnlyCollection<string> readOnlyCollection = ((patchInfo != null) ? patchInfo.Owners : null);
				if (readOnlyCollection == null)
				{
					continue;
				}
				foreach (string item in readOnlyCollection)
				{
					if (item != null && !(item == "giwaffed.HaulersDream"))
					{
						list = list ?? new List<string>();
						if (!list.Contains(item))
						{
							list.Add(item);
						}
					}
				}
			}
			return (list == null) ? null : string.Join(", ", list.ToArray());
		}
		catch
		{
			return null;
		}
	}

	private static string HookFacts(Type giverType)
	{
		try
		{
			List<string> list = new List<string>();
			for (int i = 0; i < GiverJobEntryPoints.Length; i++)
			{
				AddPatchOwners(AccessTools.Method(giverType, GiverJobEntryPoints[i], (Type[])null, (Type[])null), transpilersOnly: false, list);
			}
			List<string> list2 = new List<string>();
			AddPatchOwners(SeamMethod, transpilersOnly: true, list2);
			return "Harmony hooks on this work giver's job entry points (JobOnThing / JobOnCell / HasJobOnThing): " + Join(list) + ". Transpilers on JobGiver_Work.TryIssueJobPackage: " + Join(list2) + ". Being hooked into a method is INVOLVEMENT, not blame - it shows whose code can run inside that call, never whose code threw, and Hauler's Dream is listed above on the same terms as everyone else.";
		}
		catch
		{
			return "The Harmony hooks on this work giver could not be read.";
		}
	}

	private static void AddPatchOwners(MethodBase method, bool transpilersOnly, List<string> into)
	{
		if (method == null)
		{
			return;
		}
		Patches patchInfo = Harmony.GetPatchInfo(method);
		if (patchInfo != null)
		{
			AddPatches(patchInfo.Transpilers, into);
			if (!transpilersOnly)
			{
				AddPatches(patchInfo.Prefixes, into);
				AddPatches(patchInfo.Postfixes, into);
				AddPatches(patchInfo.Finalizers, into);
			}
		}
	}

	private static void AddPatches(IList<Patch> patches, List<string> into)
	{
		if (patches == null)
		{
			return;
		}
		for (int i = 0; i < patches.Count; i++)
		{
			Patch val = patches[i];
			if (val != null)
			{
				string text = HDFault.OwningModName(val.PatchMethod?.DeclaringType?.Assembly);
				string text2 = ((text == null) ? val.owner : (text + " (" + val.owner + ")"));
				if (!GenText.NullOrEmpty(text2) && !into.Contains(text2))
				{
					into.Add(text2);
				}
			}
		}
	}

	private static string Join(List<string> entries)
	{
		if (entries.Count != 0)
		{
			return string.Join(", ", entries.ToArray());
		}
		return "none";
	}
}
