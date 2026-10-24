using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

internal static class QuestPawnReversion
{
	internal static void OnFactionChanged(Pawn pawn, Faction oldFaction, Faction newFaction)
	{
		if (pawn == null || oldFaction == newFaction || pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
		{
			return;
		}
		Faction ofPlayerSilentFail = Faction.OfPlayerSilentFail;
		if (ofPlayerSilentFail == null)
		{
			return;
		}
		HaulersDreamGameComponent instance = HaulersDreamGameComponent.Instance;
		if (instance == null)
		{
			return;
		}
		if (newFaction == ofPlayerSilentFail && oldFaction != ofPlayerSilentFail)
		{
			if (QuestUtility.IsQuestLodger(pawn))
			{
				RecordGainSnapshot(pawn, instance);
			}
			else
			{
				instance.ClearQuestPawnSnapshot(pawn);
			}
		}
		else
		{
			if (oldFaction != ofPlayerSilentFail || newFaction == ofPlayerSilentFail || !instance.TryGetQuestPawnSnapshot(pawn, out var items))
			{
				return;
			}
			instance.ClearQuestPawnSnapshot(pawn);
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings != null && settings.enableQuestPawnDrop && !pawn.Dead)
			{
				Map mapHeld = ((Thing)pawn).MapHeld;
				if (mapHeld != null)
				{
					DropExcessAtRevert(pawn, items, mapHeld);
				}
			}
		}
	}

	internal static void NotifyRecruited(Pawn pawn)
	{
		if (pawn != null)
		{
			HaulersDreamGameComponent.Instance?.ClearQuestPawnSnapshot(pawn);
		}
	}

	private static void RecordGainSnapshot(Pawn pawn, HaulersDreamGameComponent component)
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Expected O, but got Unknown
		//IL_00b4: Expected O, but got Unknown
		List<QuestPawnDropPolicy.InventoryStack> list = new List<QuestPawnDropPolicy.InventoryStack>();
		ThingOwner<Thing> val = pawn.inventory?.innerContainer;
		if (val != null)
		{
			HashSet<Thing> tagged = ThingCompUtility.TryGetComp<CompHauledToInventory>((Thing)(object)pawn)?.GetHashSet();
			CollectStacks(val, tagged, list, null);
			list.Sort((QuestPawnDropPolicy.InventoryStack a, QuestPawnDropPolicy.InventoryStack b) => a.Id.CompareTo(b.Id));
		}
		List<QuestPawnDropPolicy.SnapshotEntry> list2 = new List<QuestPawnDropPolicy.SnapshotEntry>();
		QuestPawnDropPolicy.BuildSnapshot(list, list2);
		List<QuestPawnSnapshotItem> list3 = new List<QuestPawnSnapshotItem>(list2.Count);
		for (int num = 0; num < list2.Count; num++)
		{
			list3.Add(new QuestPawnSnapshotItem((ThingDef)list2[num].Def, (ThingDef)list2[num].Stuff, list2[num].Count));
		}
		component.RecordQuestPawnSnapshot(pawn, list3);
	}

	private static void DropExcessAtRevert(Pawn pawn, List<QuestPawnSnapshotItem> snapshotItems, Map map)
	{
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		ThingOwner<Thing> val = pawn.inventory?.innerContainer;
		if (val == null || ((ThingOwner)val).Count == 0)
		{
			return;
		}
		CompHauledToInventory compHauledToInventory = ThingCompUtility.TryGetComp<CompHauledToInventory>((Thing)(object)pawn);
		HashSet<Thing> tagged = compHauledToInventory?.GetHashSet();
		List<QuestPawnDropPolicy.InventoryStack> list = new List<QuestPawnDropPolicy.InventoryStack>(((ThingOwner)val).Count);
		Dictionary<int, Thing> dictionary = new Dictionary<int, Thing>(((ThingOwner)val).Count);
		CollectStacks(val, tagged, list, dictionary);
		if (list.Count == 0)
		{
			return;
		}
		list.Sort((QuestPawnDropPolicy.InventoryStack a, QuestPawnDropPolicy.InventoryStack b) => a.Id.CompareTo(b.Id));
		List<QuestPawnDropPolicy.SnapshotEntry> list2 = new List<QuestPawnDropPolicy.SnapshotEntry>(snapshotItems.Count);
		for (int num = 0; num < snapshotItems.Count; num++)
		{
			QuestPawnSnapshotItem questPawnSnapshotItem = snapshotItems[num];
			if (questPawnSnapshotItem?.def != null && questPawnSnapshotItem.count > 0)
			{
				list2.Add(new QuestPawnDropPolicy.SnapshotEntry(questPawnSnapshotItem.def, questPawnSnapshotItem.stuff, questPawnSnapshotItem.count));
			}
		}
		List<QuestPawnDropPolicy.DropOrder> list3 = new List<QuestPawnDropPolicy.DropOrder>();
		QuestPawnDropPolicy.SelectDrops(list2, list, list3);
		IntVec3 positionHeld = ((Thing)pawn).PositionHeld;
		Thing val2 = default(Thing);
		for (int num2 = 0; num2 < list3.Count; num2++)
		{
			QuestPawnDropPolicy.DropOrder dropOrder = list3[num2];
			if (!dictionary.TryGetValue(dropOrder.Id, out var value) || value == null || value.Destroyed || !((ThingOwner)val).Contains(value))
			{
				continue;
			}
			int num3 = ((dropOrder.Count < value.stackCount) ? dropOrder.Count : value.stackCount);
			if (num3 > 0 && val.TryDrop(value, positionHeld, map, (ThingPlaceMode)1, num3, ref val2, (Action<Thing, int>)null, (Predicate<IntVec3>)null))
			{
				if (val2 != null)
				{
					ForbidUtility.SetForbidden(val2, false, false);
				}
				if (!((ThingOwner)val).Contains(value))
				{
					compHauledToInventory?.Deregister(value);
				}
			}
		}
	}

	private static void CollectStacks(ThingOwner<Thing> owner, HashSet<Thing> tagged, List<QuestPawnDropPolicy.InventoryStack> outStacks, Dictionary<int, Thing> outById)
	{
		for (int i = 0; i < ((ThingOwner)owner).Count; i++)
		{
			Thing val = owner[i];
			if (val != null && !val.Destroyed && val.stackCount > 0 && GenList.NullOrEmpty<string>((IList<string>)val.questTags))
			{
				bool tagged2 = tagged?.Contains(val) ?? false;
				outStacks.Add(new QuestPawnDropPolicy.InventoryStack(val.thingIDNumber, val.def, val.Stuff, val.stackCount, tagged2));
				if (outById != null)
				{
					outById[val.thingIDNumber] = val;
				}
			}
		}
	}
}
