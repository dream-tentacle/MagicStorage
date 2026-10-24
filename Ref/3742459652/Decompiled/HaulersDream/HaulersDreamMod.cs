using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class HaulersDreamMod : Mod
{
	public const string HarmonyId = "giwaffed.HaulersDream";

	public const string PackageId = "giwaffed.HaulersDream";

	private static readonly (Type type, string method)[] DropProtectionTargets = new(Type, string)[3]
	{
		(typeof(JobGiver_DropUnusedInventory), "TryGiveJob"),
		(typeof(JobGiver_DropUnusedInventory), "Drop"),
		(typeof(JobGiver_DropUnusedInventory), "ShouldKeepDrugInInventory")
	};

	private static readonly (Type type, string method, string patchClass)[] StorageSeamTargets = new(Type, string, string)[3]
	{
		(typeof(HaulAIUtility), "HaulToCellStorageJob", "Patch_HaulToCellStorageJob_ClampToCommitments"),
		(typeof(StoreUtility), "IsGoodStoreCell", "Patch_IsGoodStoreCell_HonourCommitments"),
		(typeof(JobDriver_HaulToCell), "TryMakePreToilReservations", "Patch_JobDriver_HaulToCell_NoCellReservation")
	};

	private static readonly HarmonyMethod UniversalTagger = new HarmonyMethod(AccessTools.Method(typeof(HDLog), "UniversalExceptionFinalizer", (Type[])null, (Type[])null))
	{
		priority = 0
	};

	private static readonly HashSet<(Type type, string name)> TaggerExcludedSafeGetters = new HashSet<(Type, string)>
	{
		(typeof(Pawn), "GetDisabledWorkTypes"),
		(typeof(Pawn), "get_CombinedDisabledWorkTags")
	};

	private static bool syncedAllPawnsCanHaul;

	private static bool syncedAllPawnsCanClean;

	private static bool syncedAllPawnsCanCutPlants;

	public static HaulersDreamMod Instance { get; private set; }

	public static HaulersDreamSettings Settings { get; private set; }

	public HaulersDreamMod(ModContentPack content)
		: base(content)
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected O, but got Unknown
		//IL_008d: Expected O, but got Unknown
		Instance = this;
		Settings = ((Mod)this).GetSettings<HaulersDreamSettings>();
		syncedAllPawnsCanHaul = Settings.allPawnsCanHaul;
		syncedAllPawnsCanClean = Settings.allPawnsCanClean;
		syncedAllPawnsCanCutPlants = Settings.allPawnsCanCutPlants;
		HDDebugLog.ConfigureDirectory(Application.consoleLogPath);
		Application.quitting += delegate
		{
			HDDebugLog.FlushAndClose();
		};
		Harmony val = new Harmony("giwaffed.HaulersDream");
		ApplyPatchesResilient(val, Assembly.GetExecutingAssembly());
		VerifyDropProtection(val);
		VerifyStorageSeam();
		HaulersDreamSettings.VerifyProfileIntegrity();
		HDLog.Msg("initialised — carry limit defaults to each pawn's max carrying capacity.");
	}

	private static void ApplyPatchesResilient(Harmony harmony, Assembly assembly)
	{
		int num = 0;
		int num2 = 0;
		Type[] typesFromAssembly = AccessTools.GetTypesFromAssembly(assembly);
		foreach (Type type in typesFromAssembly)
		{
			if (IsHarmonyPatchContainer(type))
			{
				try
				{
					harmony.CreateClassProcessor(type).Patch();
					num++;
				}
				catch (Exception ex)
				{
					num2++;
					HDLog.Err("patch class '" + type.Name + "' could not be applied on this RimWorld build (a hooked vanilla target is likely missing or renamed) — that feature is disabled; the rest of the mod continues. " + ex.GetType().Name + ": " + ex.Message);
				}
			}
		}
		if (num2 > 0)
		{
			HDLog.Warn($"{num} patch class(es) applied, {num2} skipped due to missing targets (see errors above).");
		}
		AttachUniversalExceptionTagger(harmony);
	}

	private static void VerifyDropProtection(Harmony harmony)
	{
		(Type, string)[] dropProtectionTargets = DropProtectionTargets;
		for (int i = 0; i < dropProtectionTargets.Length; i++)
		{
			(Type, string) tuple = dropProtectionTargets[i];
			Type item = tuple.Item1;
			string item2 = tuple.Item2;
			MethodInfo methodInfo = AccessTools.Method(item, item2, (Type[])null, (Type[])null);
			if (methodInfo == null)
			{
				HDLog.Err("DROP-PROTECTION TRIPWIRE: vanilla " + item.Name + "." + item2 + " was not found on this RimWorld build (renamed or removed). Hauler's Dream can no longer stop pawns from dropping the crops / milk / drugs it scooped into their inventory — this needs a code update. Please report it with this log attached.");
			}
			else if (!HasHdPatch(Harmony.GetPatchInfo((MethodBase)methodInfo)))
			{
				HDLog.Err("DROP-PROTECTION TRIPWIRE: vanilla " + item.Name + "." + item2 + " exists but Hauler's Dream's guard did not attach to it, so pawns may drop scooped inventory cargo. Another mod may have replaced the method, or HD's guard patch is missing. Please report it with this log attached.");
			}
		}
	}

	private static void VerifyStorageSeam()
	{
		(Type, string, string)[] storageSeamTargets = StorageSeamTargets;
		for (int i = 0; i < storageSeamTargets.Length; i++)
		{
			(Type, string, string) tuple = storageSeamTargets[i];
			Type item = tuple.Item1;
			string item2 = tuple.Item2;
			string item3 = tuple.Item3;
			MethodInfo methodInfo = AccessTools.DeclaredMethod(item, item2, (Type[])null, (Type[])null);
			if (methodInfo == null)
			{
				StorageCommitments.Disable();
				HDLog.Err("STORAGE-SEAM TRIPWIRE: vanilla " + item.Name + "." + item2 + " was not found on this RimWorld build (renamed or removed). Hauler's Dream cannot arbitrate how much each hauler may take to a stockpile without it, so the whole storage-sharing feature is now OFF and hauling falls back to the base game's own behaviour — this needs a code update. Please report it with this log attached.");
				continue;
			}
			Patches patchInfo = Harmony.GetPatchInfo((MethodBase)methodInfo);
			if (!DeclaredIn(patchInfo?.Prefixes, item3) && !DeclaredIn(patchInfo?.Postfixes, item3) && !DeclaredIn(patchInfo?.Transpilers, item3))
			{
				StorageCommitments.Disable();
				HDLog.Err("STORAGE-SEAM TRIPWIRE: vanilla " + item.Name + "." + item2 + " exists but Hauler's Dream's " + item3 + " did not attach to it. Another mod may have replaced the method, or HD's patch is missing. Leaving the rest of the seam running would let Hauler's Dream strip the destination reservation with nothing in its place, so the whole storage-sharing feature is now OFF and hauling falls back to the base game's own behaviour. Please report it with this log attached.");
			}
		}
	}

	private static bool DeclaredIn(IEnumerable<Patch> patches, string patchClass)
	{
		if (patches == null)
		{
			return false;
		}
		foreach (Patch patch in patches)
		{
			if (patch != null && patch.owner == "giwaffed.HaulersDream" && patch.PatchMethod?.DeclaringType?.Name == patchClass)
			{
				return true;
			}
		}
		return false;
	}

	private static bool HasHdPatch(Patches info)
	{
		if (info != null)
		{
			if (!OwnedByHd(info.Prefixes) && !OwnedByHd(info.Postfixes))
			{
				return OwnedByHd(info.Transpilers);
			}
			return true;
		}
		return false;
	}

	private static bool OwnedByHd(IEnumerable<Patch> patches)
	{
		if (patches == null)
		{
			return false;
		}
		foreach (Patch patch in patches)
		{
			if (patch != null && patch.owner == "giwaffed.HaulersDream")
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsSafePostfixOnlyBlameMagnet(MethodBase method, Patches info)
	{
		if (method?.DeclaringType == null || info == null)
		{
			return false;
		}
		if (!TaggerExcludedSafeGetters.Contains((method.DeclaringType, method.Name)))
		{
			return false;
		}
		if (OwnedByHd(info.Postfixes) && !OwnedByHd(info.Prefixes))
		{
			return !OwnedByHd(info.Transpilers);
		}
		return false;
	}

	private static void AttachUniversalExceptionTagger(Harmony harmony)
	{
		List<MethodBase> list = new List<MethodBase>(harmony.GetPatchedMethods());
		HashSet<MethodBase> hashSet = new HashSet<MethodBase>();
		int num = 0;
		foreach (MethodBase item in list)
		{
			if (item == null)
			{
				continue;
			}
			MethodBase methodBase = NormalizeToDeclared(item);
			if ((methodBase is MethodInfo methodInfo && (methodInfo.IsAbstract || methodInfo.ContainsGenericParameters)) || !hashSet.Add(methodBase))
			{
				continue;
			}
			Patches patchInfo = Harmony.GetPatchInfo(methodBase);
			if (!IsSafePostfixOnlyBlameMagnet(methodBase, patchInfo) && (patchInfo?.Finalizers == null || !AlreadyHasHandlingFinalizer(patchInfo.Finalizers)))
			{
				try
				{
					harmony.Patch(methodBase, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, UniversalTagger);
					num++;
				}
				catch (Exception ex)
				{
					HDLog.Warn("could not attach the exception tagger to " + methodBase.DeclaringType?.FullName + "." + methodBase.Name + " — exceptions through it won't carry the Hauler's Dream breadcrumb. " + ex.GetType().Name + ": " + ex.Message);
				}
			}
		}
		HDLog.Dbg($"exception tagger attached to {num} of {list.Count} patched method(s).");
	}

	internal static MethodBase NormalizeToDeclared(MethodBase m)
	{
		if (!(m is MethodInfo methodInfo) || methodInfo.DeclaringType == null || methodInfo.ReflectedType == methodInfo.DeclaringType)
		{
			return m;
		}
		Type[] array = Array.ConvertAll(methodInfo.GetParameters(), (ParameterInfo p) => p.ParameterType);
		return AccessTools.DeclaredMethod(methodInfo.DeclaringType, methodInfo.Name, array, (Type[])null) ?? m;
	}

	private static bool AlreadyHasHandlingFinalizer(IEnumerable<Patch> finalizers)
	{
		foreach (Patch finalizer in finalizers)
		{
			if (finalizer?.owner != "giwaffed.HaulersDream" || finalizer.PatchMethod == null)
			{
				continue;
			}
			if (finalizer.PatchMethod.ReturnType == typeof(Exception))
			{
				return true;
			}
			ParameterInfo[] parameters = finalizer.PatchMethod.GetParameters();
			foreach (ParameterInfo parameterInfo in parameters)
			{
				if (parameterInfo.Name == "__exception" || parameterInfo.ParameterType == typeof(Exception))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool IsHarmonyPatchContainer(Type type)
	{
		if (HasDirectHarmonyAttribute(type))
		{
			return true;
		}
		MethodInfo[] methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		for (int i = 0; i < methods.Length; i++)
		{
			if (HasDirectHarmonyAttribute(methods[i]))
			{
				return true;
			}
		}
		return false;
	}

	private static bool HasDirectHarmonyAttribute(MemberInfo member)
	{
		try
		{
			return member.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length != 0 || member.GetCustomAttributes(typeof(HarmonyPrefix), inherit: false).Length != 0 || member.GetCustomAttributes(typeof(HarmonyPostfix), inherit: false).Length != 0 || member.GetCustomAttributes(typeof(HarmonyTranspiler), inherit: false).Length != 0 || member.GetCustomAttributes(typeof(HarmonyFinalizer), inherit: false).Length != 0 || member.GetCustomAttributes(typeof(HarmonyReversePatch), inherit: false).Length != 0;
		}
		catch (Exception ex)
		{
			string text = (member.DeclaringType?.FullName ?? "?") + "." + member.Name;
			HDLog.WarnOnce("could not read attributes on " + text + " (likely an optional-mod attribute whose assembly isn't loaded) — treating it as a non-patch member so startup isn't bricked. " + ex.GetType().Name, ("HD.attrProbe." + text).GetHashCode());
			return false;
		}
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		Settings.DoWindowContents(inRect);
	}

	public override string SettingsCategory()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.SettingsCategory"));
	}

	public override void WriteSettings()
	{
		((Mod)this).WriteSettings();
		HaulersDreamSettings settings = Settings;
		if (Current.Game == null || settings == null || (settings.allPawnsCanHaul == syncedAllPawnsCanHaul && settings.allPawnsCanClean == syncedAllPawnsCanClean && settings.allPawnsCanCutPlants == syncedAllPawnsCanCutPlants))
		{
			return;
		}
		syncedAllPawnsCanHaul = settings.allPawnsCanHaul;
		syncedAllPawnsCanClean = settings.allPawnsCanClean;
		syncedAllPawnsCanCutPlants = settings.allPawnsCanCutPlants;
		List<Pawn> allMapsCaravansAndTravellingTransporters_Alive = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive;
		for (int i = 0; i < allMapsCaravansAndTravellingTransporters_Alive.Count; i++)
		{
			Pawn val = allMapsCaravansAndTravellingTransporters_Alive[i];
			if (((val != null) ? ((Thing)val).Faction : null) != null && ((Thing)val).Faction.IsPlayer)
			{
				val.Notify_DisabledWorkTypesChanged();
			}
		}
	}
}
