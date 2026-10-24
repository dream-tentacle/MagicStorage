using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

public class StorageBuildingFilter : IExposable
{
	private sealed class ContextScope : IDisposable
	{
		public void Dispose()
		{
			List<StorageFilterContext> contextStack = StorageBuildingFilter.contextStack;
			if (contextStack != null && contextStack.Count > 0)
			{
				contextStack.RemoveAt(contextStack.Count - 1);
			}
		}
	}

	public HashSet<string> denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	[ThreadStatic]
	private static List<StorageFilterContext> contextStack;

	[NonSerialized]
	private static bool? anyStorageModPresentCached;

	public static StorageFilterContext CurrentContext
	{
		get
		{
			List<StorageFilterContext> list = contextStack;
			if (list == null || list.Count <= 0)
			{
				return StorageFilterContext.Unload;
			}
			return list[list.Count - 1];
		}
	}

	public static bool Enabled
	{
		get
		{
			if (HaulersDreamMod.Settings != null)
			{
				return HaulersDreamMod.Settings.storageFiltersEnabled;
			}
			return false;
		}
	}

	public static bool AnyStorageModPresent
	{
		get
		{
			if (anyStorageModPresentCached.HasValue)
			{
				return anyStorageModPresentCached.Value;
			}
			bool flag = false;
			List<ThingDef> allDefsListForReading = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				ThingDef val = allDefsListForReading[i];
				if (IsStorageBuildingDef(val))
				{
					ModContentPack modContentPack = ((Def)val).modContentPack;
					if (modContentPack != null && !modContentPack.IsCoreMod)
					{
						flag = true;
						break;
					}
				}
			}
			anyStorageModPresentCached = flag;
			return flag;
		}
	}

	public static IDisposable PushContext(StorageFilterContext ctx)
	{
		(contextStack ?? (contextStack = new List<StorageFilterContext>())).Add(ctx);
		return new ContextScope();
	}

	public bool IsCellAllowed(IntVec3 cell, Map map)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (!Enabled)
		{
			return true;
		}
		if (map == null || !((IntVec3)(ref cell)).IsValid)
		{
			return true;
		}
		return IsGroupAllowed(StoreUtility.GetSlotGroup(cell, map));
	}

	public bool IsGroupAllowed(SlotGroup group)
	{
		if (!Enabled)
		{
			return true;
		}
		if (group == null)
		{
			return true;
		}
		ISlotGroupParent parent = group.parent;
		Building_Storage val = (Building_Storage)(object)((parent is Building_Storage) ? parent : null);
		if (val == null)
		{
			return true;
		}
		ThingDef def = ((Thing)val).def;
		if (def == null)
		{
			return true;
		}
		return IsBuildingDefAllowed(def);
	}

	public bool IsHaulDestinationAllowed(IHaulDestination dest)
	{
		if (!Enabled)
		{
			return true;
		}
		Building val = (Building)(object)((dest is Building) ? dest : null);
		if (val == null)
		{
			return true;
		}
		ThingDef def = ((Thing)val).def;
		if (def == null)
		{
			return true;
		}
		return IsBuildingDefAllowed(def);
	}

	private bool IsBuildingDefAllowed(ThingDef def)
	{
		string defName = ((Def)def).defName;
		ModContentPack modContentPack = ((Def)def).modContentPack;
		string packageId = ((modContentPack != null) ? modContentPack.PackageId : null);
		return StorageFilterPolicy.IsAllowed(defName, packageId, CurrentContext, HaulersDreamMod.Settings.storageFilterUseDefaults, HaulersDreamMod.Settings.storageFilterDenyLwmForOpportunistic, denied, allowed);
	}

	public static bool IsStorageBuildingDef(ThingDef def)
	{
		if (def?.thingClass == null)
		{
			return false;
		}
		return typeof(Building_Storage).IsAssignableFrom(def.thingClass);
	}

	public void ExposeData()
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Invalid comparison between Unknown and I4
		Scribe_Collections.Look<string>(ref denied, "denied", (LookMode)1);
		Scribe_Collections.Look<string>(ref allowed, "allowed", (LookMode)1);
		if ((int)Scribe.mode == 2)
		{
			denied = ToCaseInsensitive(denied);
			allowed = ToCaseInsensitive(allowed);
		}
	}

	private static HashSet<string> ToCaseInsensitive(HashSet<string> loaded)
	{
		if (loaded != null)
		{
			return new HashSet<string>(loaded, StringComparer.OrdinalIgnoreCase);
		}
		return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	}
}
