using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class CompHauledToInventory : ThingComp
{
	private HashSet<Thing> takenToInventory = new HashSet<Thing>();

	private Dictionary<ThingDef, int> keptCounts = new Dictionary<ThingDef, int>();

	[NonSerialized]
	private HashSet<Thing> legacyKept;

	public int lastYieldTick = -99999;

	[NonSerialized]
	private int lastHealTick = -1;

	public bool autoHaulYields = true;

	[NonSerialized]
	private Dictionary<Thing, int> taggedTick = new Dictionary<Thing, int>();

	public int lastOpportunisticUnloadTick = -99999;

	public int lastInterceptedTick = -99999;

	public int lastSweepTick = -99999;

	public IntVec3 lastPlantHarvestCell = IntVec3.Invalid;

	public int lastPlantHarvestTick = -99999;

	public bool selfPickupDirectHarvest;

	public readonly List<Thing> pendingSelfPickups = new List<Thing>();

	[ThreadStatic]
	private static HashSet<ThingDef> tmpScoopedDefs;

	[ThreadStatic]
	private static List<Thing> tmpStaleTicks;

	[ThreadStatic]
	private static HashSet<ThingDef> tmpCarryOverDefs;

	[ThreadStatic]
	private static HashSet<object> tmpScoopedUnion;

	[ThreadStatic]
	private static List<TagHealPolicy.Stack> tmpStacks;

	[ThreadStatic]
	private static List<int> tmpTagIndices;

	[ThreadStatic]
	private static List<ThingDef> tmpKeptToPrune;

	public HashSet<Thing> GetHashSet()
	{
		TickManager tickManager = Find.TickManager;
		int num = ((tickManager != null) ? tickManager.TicksGame : (-1));
		if (!TagHealPolicy.ShouldReheal(lastHealTick, num))
		{
			return takenToInventory;
		}
		HashSet<ThingDef> hashSet = tmpCarryOverDefs ?? (tmpCarryOverDefs = new HashSet<ThingDef>());
		hashSet.Clear();
		foreach (Thing item in takenToInventory)
		{
			if ((item == null || item.Destroyed) && item?.def != null)
			{
				hashSet.Add(item.def);
			}
		}
		takenToInventory.RemoveWhere((Thing x) => x == null || x.Destroyed);
		ThingWithComps parent = base.parent;
		ThingOwner<Thing> val = ((Pawn)(((parent is Pawn) ? parent : null)?)).inventory?.innerContainer;
		if (legacyKept != null && val != null)
		{
			foreach (Thing item2 in legacyKept)
			{
				if (item2 != null && !item2.Destroyed && ((ThingOwner)val).Contains(item2) && item2.def != null)
				{
					int held = CountOfDef((ThingOwner)(object)val, item2.def);
					int taggedUnits = TaggedUnitsOfDef((ThingOwner)(object)val, item2.def);
					int num2 = KeepCountPolicy.MigratedKeep(SumLegacyKeptStackCounts(item2.def, (ThingOwner)(object)val), held, taggedUnits);
					if (num2 > 0 && (!keptCounts.TryGetValue(item2.def, out var value) || value < num2))
					{
						keptCounts[item2.def] = num2;
					}
				}
			}
			legacyKept = null;
		}
		if (keptCounts.Count > 0)
		{
			PruneEmptyKeptCounts((ThingOwner)(object)val);
		}
		if (val != null && (takenToInventory.Count > 0 || hashSet.Count > 0))
		{
			ThingWithComps parent2 = base.parent;
			Pawn pawn = (Pawn)(object)((parent2 is Pawn) ? parent2 : null);
			HashSet<ThingDef> hashSet2 = tmpScoopedDefs ?? (tmpScoopedDefs = new HashSet<ThingDef>());
			hashSet2.Clear();
			foreach (Thing item3 in takenToInventory)
			{
				hashSet2.Add(item3.def);
			}
			HashSet<object> hashSet3 = tmpScoopedUnion ?? (tmpScoopedUnion = new HashSet<object>());
			TagHealPolicy.BuildScoopedUnion(hashSet2, hashSet, hashSet3);
			List<TagHealPolicy.Stack> list = tmpStacks ?? (tmpStacks = new List<TagHealPolicy.Stack>());
			list.Clear();
			for (int num3 = 0; num3 < ((ThingOwner)val).Count; num3++)
			{
				Thing val2 = val[num3];
				ThingDef val3 = val2?.def;
				bool flag = val2 != null && takenToInventory.Contains(val2);
				bool excludeFromTag = val3 != null && !flag && hashSet3.Contains(val3) && (SimpleSidearmsCompat.IsRememberedSidearm(pawn, val2) || GrabYourToolCompat.IsCarriedTool(pawn, val2) || SurvivalToolsCompat.IsCarriedTool(pawn, val2) || AnimalInteractFood.IsHeldForInteraction(pawn, val2));
				list.Add(new TagHealPolicy.Stack(val3, flag, excludeFromTag));
			}
			List<int> list2 = tmpTagIndices ?? (tmpTagIndices = new List<int>());
			TagHealPolicy.SelectStacksToTag(hashSet3, list, list2);
			for (int num4 = 0; num4 < list2.Count; num4++)
			{
				Thing val4 = val[list2[num4]];
				if (takenToInventory.Add(val4))
				{
					StampTick(val4);
					CECompat.NotifyHeld(pawn, val4, val4.stackCount);
				}
			}
		}
		hashSet.Clear();
		if (taggedTick == null)
		{
			taggedTick = new Dictionary<Thing, int>();
		}
		if (taggedTick.Count > takenToInventory.Count)
		{
			List<Thing> list3 = tmpStaleTicks ?? (tmpStaleTicks = new List<Thing>());
			list3.Clear();
			foreach (KeyValuePair<Thing, int> item4 in taggedTick)
			{
				if (!takenToInventory.Contains(item4.Key))
				{
					list3.Add(item4.Key);
				}
			}
			for (int num5 = 0; num5 < list3.Count; num5++)
			{
				taggedTick.Remove(list3[num5]);
			}
			list3.Clear();
		}
		foreach (Thing item5 in takenToInventory)
		{
			if (!taggedTick.ContainsKey(item5))
			{
				StampTick(item5);
			}
		}
		if (num != -1)
		{
			lastHealTick = num;
		}
		return takenToInventory;
	}

	private void StampTick(Thing thing)
	{
		if (thing != null)
		{
			if (taggedTick == null)
			{
				taggedTick = new Dictionary<Thing, int>();
			}
			Dictionary<Thing, int> dictionary = taggedTick;
			TickManager tickManager = Find.TickManager;
			dictionary[thing] = ((tickManager != null) ? tickManager.TicksGame : 0);
		}
	}

	public int FirstTaggedTick(Thing thing)
	{
		if (thing != null && taggedTick != null && taggedTick.TryGetValue(thing, out var value))
		{
			return value;
		}
		TickManager tickManager = Find.TickManager;
		if (tickManager == null)
		{
			return 0;
		}
		return tickManager.TicksGame;
	}

	public void RegisterHauledItem(Thing thing, int mergedCount = 0)
	{
		if (thing != null)
		{
			lastHealTick = -1;
			if (takenToInventory.Add(thing))
			{
				StampTick(thing);
				ThingWithComps parent = base.parent;
				CECompat.NotifyHeld((Pawn)(object)((parent is Pawn) ? parent : null), thing, thing.stackCount);
			}
			else if (mergedCount > 0)
			{
				ThingWithComps parent2 = base.parent;
				CECompat.NotifyHeld((Pawn)(object)((parent2 is Pawn) ? parent2 : null), thing, mergedCount);
			}
		}
	}

	public HashSet<Thing> PeekHashSet()
	{
		return takenToInventory;
	}

	public int KeptCountOf(ThingDef def)
	{
		if (def == null || !keptCounts.TryGetValue(def, out var value) || value <= 0)
		{
			return 0;
		}
		return value;
	}

	public bool IsKeptDef(ThingDef def)
	{
		return KeptCountOf(def) > 0;
	}

	public Dictionary<ThingDef, int> PeekKeptCounts()
	{
		return keptCounts;
	}

	public void AddKeptCount(ThingDef def, int count)
	{
		if (def != null && count > 0)
		{
			keptCounts.TryGetValue(def, out var value);
			keptCounts[def] = value + count;
		}
	}

	public void SetKeptCount(ThingDef def, int count)
	{
		if (def != null)
		{
			if (count <= 0)
			{
				keptCounts.Remove(def);
			}
			else
			{
				keptCounts[def] = count;
			}
		}
	}

	private static int CountOfDef(ThingOwner owner, ThingDef def)
	{
		if (owner == null || def == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < owner.Count; i++)
		{
			Thing val = owner[i];
			if (val != null && val.def == def)
			{
				num += val.stackCount;
			}
		}
		return num;
	}

	private int SumLegacyKeptStackCounts(ThingDef def, ThingOwner owner)
	{
		if (legacyKept == null || def == null || owner == null)
		{
			return 0;
		}
		int num = 0;
		foreach (Thing item in legacyKept)
		{
			if (item != null && !item.Destroyed && item.def == def && owner.Contains(item))
			{
				num += item.stackCount;
			}
		}
		return num;
	}

	private int TaggedUnitsOfDef(ThingOwner owner, ThingDef def)
	{
		if (owner == null || def == null)
		{
			return 0;
		}
		int num = 0;
		foreach (Thing item in takenToInventory)
		{
			if (item != null && !item.Destroyed && item.def == def && owner.Contains(item))
			{
				num += item.stackCount;
			}
		}
		return num;
	}

	private void PruneEmptyKeptCounts(ThingOwner owner)
	{
		List<ThingDef> list = tmpKeptToPrune ?? (tmpKeptToPrune = new List<ThingDef>());
		list.Clear();
		foreach (KeyValuePair<ThingDef, int> keptCount in keptCounts)
		{
			if (keptCount.Value <= 0 || CountOfDef(owner, keptCount.Key) <= 0)
			{
				list.Add(keptCount.Key);
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			keptCounts.Remove(list[i]);
		}
		list.Clear();
	}

	public void Deregister(Thing thing)
	{
		lastHealTick = -1;
		takenToInventory.Remove(thing);
	}

	public Thing TakeNextValidPending()
	{
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		ThingWithComps parent = base.parent;
		Pawn val = (Pawn)(object)((parent is Pawn) ? parent : null);
		if (val == null)
		{
			pendingSelfPickups.Clear();
			return null;
		}
		int num = 0;
		Thing val2 = null;
		float num2 = float.MaxValue;
		for (int num3 = pendingSelfPickups.Count - 1; num3 >= 0; num3--)
		{
			Thing val3 = pendingSelfPickups[num3];
			bool flag = val3 != null && val3.Spawned && !val3.Destroyed && val3.MapHeld == ((Thing)val).Map && !ForbidUtility.IsForbidden(val3, val) && ExtraSweepReach.Allows(val, LocalTargetInfo.op_Implicit(val3), (PathEndMode)3);
			if (flag && !HaulAIUtility.PawnCanAutomaticallyHaulFast(val, val3, false))
			{
				flag = false;
				num++;
			}
			if (!flag)
			{
				pendingSelfPickups.RemoveAt(num3);
				SelfPickupClaims.Release(val3, val);
			}
			else
			{
				IntVec3 val4 = val3.Position - ((Thing)val).Position;
				float num4 = ((IntVec3)(ref val4)).LengthHorizontalSquared;
				if (num4 < num2)
				{
					num2 = num4;
					val2 = val3;
				}
			}
		}
		if (num > 0)
		{
			HDLog.Dbg($"{val} self-pickup: skipped {num} unreachable pending drop(s)" + ((val2 == null) ? "; queue now empty." : "."));
		}
		if (val2 == null)
		{
			return null;
		}
		pendingSelfPickups.Remove(val2);
		SelfPickupClaims.Release(val2, val);
		return val2;
	}

	public void NotifyYieldPicked()
	{
		if (Find.TickManager != null)
		{
			lastYieldTick = Find.TickManager.TicksGame;
		}
	}

	public override void PostExposeData()
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Invalid comparison between Unknown and I4
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Invalid comparison between Unknown and I4
		((ThingComp)this).PostExposeData();
		Scribe_Collections.Look<Thing>(ref takenToInventory, "haulersDreamTakenToInventory", (LookMode)3);
		Scribe_Collections.Look<ThingDef, int>(ref keptCounts, "haulersDreamKeptCounts", (LookMode)4, (LookMode)1);
		if ((int)Scribe.mode == 2 || (int)Scribe.mode == 3)
		{
			Scribe_Collections.Look<Thing>(ref legacyKept, "haulersDreamKept", (LookMode)3);
		}
		Scribe_Values.Look<int>(ref lastYieldTick, "haulersDreamLastYieldTick", -99999, false);
		Scribe_Values.Look<bool>(ref autoHaulYields, "haulersDreamAutoHaulYields", true, false);
		if (takenToInventory == null)
		{
			takenToInventory = new HashSet<Thing>();
		}
		if (keptCounts == null)
		{
			keptCounts = new Dictionary<ThingDef, int>();
		}
	}
}
