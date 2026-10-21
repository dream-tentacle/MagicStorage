using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using InfiniteStorage.UI;
using Microsoft.CodeAnalysis;
using Mlie;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("0.0.0.0")]
[module: UnverifiableCode]
[module: RefSafetyRules(11)]
namespace Microsoft.CodeAnalysis
{
	[CompilerGenerated]
	[Embedded]
	internal sealed class EmbeddedAttribute : Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[CompilerGenerated]
	[Embedded]
	[AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
	internal sealed class RefSafetyRulesAttribute : Attribute
	{
		public readonly int Version;

		public RefSafetyRulesAttribute(int P_0)
		{
			Version = P_0;
		}
	}
}
namespace InfiniteStorage
{
	[HarmonyPatch(typeof(CaravanExitMapUtility), "ExitMapAndCreateCaravan", new Type[]
	{
		typeof(IEnumerable<Pawn>),
		typeof(Faction),
		typeof(PlanetTile),
		typeof(PlanetTile),
		typeof(PlanetTile),
		typeof(bool)
	})]
	internal static class CaravanExitMapUtility_ExitMapAndCreateCaravan
	{
		[HarmonyPriority(800)]
		private static void Prefix(IEnumerable<Pawn> pawns, Faction faction)
		{
			if (faction != Faction.OfPlayer || new List<Pawn>(pawns).Count <= 0)
			{
				return;
			}
			foreach (Building_InfiniteStorage allInfiniteStorage in WorldComp.GetAllInfiniteStorages())
			{
				allInfiniteStorage.Reclaim();
			}
		}
	}
	[HarmonyPatch(typeof(CaravanFormingUtility), "StopFormingCaravan")]
	internal static class CaravanFormingUtility_StopFormingCaravan
	{
		[HarmonyPriority(800)]
		private static void Postfix()
		{
			foreach (Building_InfiniteStorage allInfiniteStorage in WorldComp.GetAllInfiniteStorages())
			{
				allInfiniteStorage.Reclaim();
			}
		}
	}
	[HarmonyPatch(typeof(ResourceCounter), "UpdateResourceCounts")]
	internal static class ResourceCounter_UpdateResourceCounts
	{
		private static void Postfix(ResourceCounter __instance)
		{
			foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(Find.CurrentMap))
			{
				foreach (Thing storedThing in infiniteStorage.StoredThings)
				{
					if (storedThing.def.EverStorable(true) && storedThing.def.CountAsResource && !RottableUtility.IsNotFresh(storedThing))
					{
						int value = ((!__instance.countedAmounts.TryGetValue(storedThing.def, out value)) ? storedThing.stackCount : (value + storedThing.stackCount));
						__instance.countedAmounts[storedThing.def] = value;
					}
				}
			}
		}
	}
	internal class BuildingUtil
	{
		public static IEnumerable<Thing> FindThingsOfTypeNextTo(Map map, IntVec3 position, int distance)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			int num = Math.Max(0, position.x - distance);
			int num2 = Math.Min(map.info.Size.x, position.x + distance);
			int num3 = Math.Max(0, position.z - distance);
			int num4 = Math.Min(map.info.Size.z, position.z + distance);
			ReservationManager reservationManager = map.reservationManager;
			List<Thing> list = new List<Thing>();
			for (int i = num - 1; i <= num2; i++)
			{
				for (int j = num3 - 1; j <= num4; j++)
				{
					foreach (Thing item in map.thingGrid.ThingsAt(new IntVec3(i, position.y, j)))
					{
						if (reservationManager == null || !reservationManager.IsReservedByAnyoneOf(new LocalTargetInfo(item), Faction.OfPlayer))
						{
							list.Add(item);
						}
					}
				}
			}
			return list;
		}

		public static void DropThing(Thing toDrop, int amountToDrop, Building_InfiniteStorage from, Map map, List<Thing> droppedThings = null)
		{
			if (toDrop.stackCount == 0)
			{
				Log.Warning("To Drop Thing " + ((Entity)toDrop).Label + " had stack count of 0");
				return;
			}
			try
			{
				from.AllowAdds = false;
				bool flag = false;
				while (!flag)
				{
					int num = toDrop.def.stackLimit;
					if (num > amountToDrop)
					{
						num = amountToDrop;
						flag = true;
					}
					if (num >= toDrop.stackCount)
					{
						if (amountToDrop > num)
						{
							Log.Error($"        ThingStorage: Unable to drop {amountToDrop - num} of {((Def)toDrop.def).label}");
						}
						num = toDrop.stackCount;
						flag = true;
					}
					if (num > 0)
					{
						amountToDrop -= num;
						if (DropSingleThing(toDrop.SplitOff(num), from, map, out var result))
						{
							droppedThings?.Add(result);
						}
					}
				}
			}
			finally
			{
				from.AllowAdds = true;
			}
		}

		public static bool DropThing(Thing toDrop, int amountToDrop, IntVec3 from, Map map, List<Thing> droppedThings = null)
		{
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			if (toDrop.stackCount == 0)
			{
				Log.Warning("To Drop Thing " + ((Entity)toDrop).Label + " had stack count of 0");
				return false;
			}
			bool result = false;
			bool flag = false;
			while (!flag)
			{
				int num = toDrop.def.stackLimit;
				if (num > amountToDrop)
				{
					num = amountToDrop;
					flag = true;
				}
				if (num >= toDrop.stackCount)
				{
					if (amountToDrop > num)
					{
						Log.Error($"        ThingStorage: Unable to drop {amountToDrop - num} of {((Def)toDrop.def).label}");
					}
					num = toDrop.stackCount;
					flag = true;
				}
				if (num > 0)
				{
					amountToDrop -= num;
					if (dropSingleThing(toDrop.SplitOff(num), from, map, out var result2))
					{
						droppedThings?.Add(result2);
						result = true;
					}
				}
			}
			return result;
		}

		public static bool DropSingleThing(Thing toDrop, Building_InfiniteStorage from, Map map, out Thing result)
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			result = null;
			if (toDrop.stackCount == 0)
			{
				Log.Warning("To Drop Thing " + ((Entity)toDrop).Label + " had stack count of 0");
				return false;
			}
			try
			{
				from.AllowAdds = false;
				return dropSingleThing(toDrop, ((Thing)from).InteractionCell, map, out result);
			}
			finally
			{
				from.AllowAdds = true;
			}
		}

		private static bool dropSingleThing(Thing toDrop, IntVec3 from, Map map, out Thing result)
		{
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			result = null;
			if (toDrop.stackCount == 0)
			{
				Log.Warning("To Drop Thing " + ((Entity)toDrop).Label + " had stack count of 0");
				return false;
			}
			try
			{
				if (!toDrop.Spawned)
				{
					GenThing.TryDropAndSetForbidden(toDrop, from, map, (ThingPlaceMode)1, ref result, false);
					if (!result.Spawned)
					{
						if (!GenPlace.TryPlaceThing(toDrop, from, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
						{
							result = null;
							Log.Error($"Failed to spawn {((Entity)toDrop).Label} x{toDrop.stackCount}");
							return false;
						}
						result = toDrop;
					}
				}
				toDrop.Position = from;
			}
			catch (Exception ex)
			{
				Log.Error("BuildingUtil.DropApparel\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
			Thing val = result;
			if (val != null)
			{
				return val.Spawned;
			}
			return false;
		}
	}
	public class Building_InfiniteStorage : Building_Storage
	{
		private readonly SortedDictionary<string, LinkedList<Thing>> storedThings = new SortedDictionary<string, LinkedList<Thing>>();

		private CompPowerTrader compPowerTrader;

		private bool includeInTradeDeals = true;

		private long lastAutoReclaim;

		[Unsaved(false)]
		private bool preserveContentsOnMapSwap;

		[Unsaved(false)]
		private int storedCount;

		[Unsaved(false)]
		private float storedWeight;

		private List<Thing> temp;

		private List<Thing> ToDumpOnSpawn;

		public IEnumerable<Thing> StoredThings
		{
			get
			{
				foreach (LinkedList<Thing> value in storedThings.Values)
				{
					foreach (Thing item in value)
					{
						yield return item;
					}
				}
			}
		}

		public int DefsCount
		{
			get
			{
				int num = 0;
				foreach (LinkedList<Thing> value in storedThings.Values)
				{
					num += value.Count;
				}
				return num;
			}
		}

		public bool AllowAdds { get; set; } = true;

		private Map CurrentMap { get; set; }

		public bool IncludeInTradeDeals => includeInTradeDeals;

		private bool UsesPower => compPowerTrader != null;

		public bool IsOperational
		{
			get
			{
				if (compPowerTrader != null)
				{
					return compPowerTrader.PowerOn;
				}
				return true;
			}
		}

		private bool CanAutoCollect { get; set; } = true;

		public bool IncludeInWorldLookup { get; private set; }

		public void ResetAutoReclaimTime()
		{
			lastAutoReclaim = DateTime.Now.Ticks;
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Expected O, but got Unknown
			((Building_Storage)this).SpawnSetup(map, respawningAfterLoad);
			CurrentMap = map;
			if (base.settings == null)
			{
				base.settings = new StorageSettings((IStoreSettingsParent)(object)this);
				base.settings.CopyFrom(((Thing)this).def.building.defaultStorageSettings);
				base.settings.filter.SetDisallowAll((IEnumerable<ThingDef>)null, (IEnumerable<SpecialThingFilterDef>)null);
			}
			IncludeInWorldLookup = GetIncludeInWorldLookup();
			WorldComp.Add(map, this);
			compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			if (ToDumpOnSpawn == null)
			{
				return;
			}
			foreach (Thing item in ToDumpOnSpawn)
			{
				BuildingUtil.DropThing(item, item.stackCount, this, ((Thing)this).Map);
			}
			ToDumpOnSpawn.Clear();
			ToDumpOnSpawn = null;
		}

		public bool TryGetFirstFilteredItemForMending(Bill bill, ThingFilter filter, bool remove, out Thing gotten)
		{
			gotten = null;
			foreach (LinkedList<Thing> value2 in storedThings.Values)
			{
				if (value2 == null || value2.Count <= 0)
				{
					continue;
				}
				LinkedListNode<Thing> linkedListNode = value2.First;
				while (linkedListNode.Next != null)
				{
					Thing value = linkedListNode.Value;
					if (!bill.IsFixedOrAllowedIngredient(value.def) || !filter.Allows(value.def))
					{
						break;
					}
					if (bill.IsFixedOrAllowedIngredient(value) && filter.Allows(value) && value.HitPoints != value.MaxHitPoints)
					{
						value2.Remove(linkedListNode);
						BuildingUtil.DropSingleThing(value, this, ((Thing)this).Map, out gotten);
						return true;
					}
					linkedListNode = linkedListNode.Next;
				}
			}
			return gotten != null;
		}

		public override void Destroy(DestroyMode mode = (DestroyMode)0)
		{
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				storedCount = 0;
				Dispose();
				((Building_Storage)this).Destroy(mode);
			}
			catch (Exception ex)
			{
				Log.Error(((object)this).GetType().Name + ".Destroy\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (!preserveContentsOnMapSwap)
				{
					storedCount = 0;
				}
				Dispose(preserveContentsOnMapSwap);
				((Building_Storage)this).DeSpawn(mode);
			}
			catch (Exception ex)
			{
				Log.Error(((object)this).GetType().Name + ".DeSpawn\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
		}

		public override void PreSwapMap()
		{
			preserveContentsOnMapSwap = true;
			((ThingWithComps)this).PreSwapMap();
		}

		public override void PostSwapMap()
		{
			preserveContentsOnMapSwap = false;
			((ThingWithComps)this).PostSwapMap();
		}

		private void Dispose(bool preserveContents = false)
		{
			try
			{
				if (!preserveContents)
				{
					AllowAdds = false;
					foreach (LinkedList<Thing> value in storedThings.Values)
					{
						foreach (Thing item in value)
						{
							BuildingUtil.DropThing(item, item.stackCount, this, CurrentMap);
						}
					}
					storedThings.Clear();
				}
			}
			catch (Exception ex)
			{
				Log.Error(((object)this).GetType().Name + ".Dispose\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
			WorldComp.Remove(CurrentMap, this);
		}

		private void DropThing(Thing t, List<Thing> result)
		{
			BuildingUtil.DropThing(t, t.stackCount, this, CurrentMap, result);
		}

		public void Empty(List<Thing> droppedThings = null, bool force = false)
		{
			if (!force && !IsOperational && !Settings.EmptyOnPowerLoss)
			{
				return;
			}
			try
			{
				AllowAdds = false;
				foreach (LinkedList<Thing> value in storedThings.Values)
				{
					foreach (Thing item in value)
					{
						BuildingUtil.DropThing(item, item.stackCount, this, CurrentMap, droppedThings);
					}
					value.Clear();
				}
				storedCount = 0;
				storedWeight = 0f;
			}
			finally
			{
				ResetAutoReclaimTime();
				AllowAdds = true;
			}
		}

		public void Reclaim(bool respectReserved = true, List<ThingCount> chosen = null)
		{
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			if (!IsOperational || !CanAutoCollect)
			{
				return;
			}
			float num = 0f;
			if (UsesPower && Settings.EnableEnergyBuffer)
			{
				num = ((CompPower)compPowerTrader).PowerNet.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
				if (num <= (float)Settings.DesiredEnergyBuffer)
				{
					return;
				}
				num -= (float)Settings.DesiredEnergyBuffer;
			}
			foreach (Thing item in BuildingUtil.FindThingsOfTypeNextTo(((Thing)this).Map, ((Thing)this).Position, 1))
			{
				if ((chosen == null || !ChosenContains(item, chosen)) && (!UsesPower || !Settings.EnableEnergyBuffer || !((storedWeight + GetThingWeight(item, item.stackCount)) * Settings.EnergyFactor > num)))
				{
					Add(item);
				}
			}
		}

		public void ReclaimFaster(bool respectReserved = true, Dictionary<int, int> chosen = null)
		{
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			if (!IsOperational || !CanAutoCollect)
			{
				return;
			}
			float num = 0f;
			if (UsesPower && Settings.EnableEnergyBuffer)
			{
				num = ((CompPower)compPowerTrader).PowerNet.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
				if (num <= (float)Settings.DesiredEnergyBuffer)
				{
					return;
				}
				num -= (float)Settings.DesiredEnergyBuffer;
			}
			foreach (Thing item in BuildingUtil.FindThingsOfTypeNextTo(((Thing)this).Map, ((Thing)this).Position, 1))
			{
				if (chosen != null && item != null && !chosen.ContainsKey(item.thingIDNumber) && (!UsesPower || !Settings.EnableEnergyBuffer || !((storedWeight + GetThingWeight(item, item.stackCount)) * Settings.EnergyFactor > num)))
				{
					Add(item);
				}
			}
		}

		public void ForceReclaim()
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Invalid comparison between Unknown and I4
			if (((Thing)this).Map == null)
			{
				return;
			}
			foreach (Thing item in BuildingUtil.FindThingsOfTypeNextTo(((Thing)this).Map, ((Thing)this).Position, 1))
			{
				if (!(item is Building_InfiniteStorage) && (object)item != this && (int)item.def.category == 2)
				{
					Add(item, force: true);
				}
			}
		}

		private bool ChosenContains(Thing t, List<ThingCount> chosen)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			if (chosen == null)
			{
				return false;
			}
			foreach (ThingCount item in chosen)
			{
				ThingCount current = item;
				if (((ThingCount)(ref current)).Thing == t)
				{
					return true;
				}
			}
			return false;
		}

		public int StoredThingCount(ThingDef expectedDef, ThingFilter ingrediantFilter)
		{
			int num = 0;
			if (!storedThings.TryGetValue(((object)expectedDef).ToString(), out var value))
			{
				return num;
			}
			foreach (Thing item in value)
			{
				if (Allows(item, expectedDef, ingrediantFilter))
				{
					num += item.stackCount;
				}
			}
			return num;
		}

		private bool Allows(Thing t, ThingDef expectedDef, ThingFilter filter)
		{
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			if (filter == null)
			{
				return true;
			}
			if (t.def != expectedDef)
			{
				return false;
			}
			if (expectedDef.useHitPoints && filter.AllowedHitPointsPercents.min != 0f && filter.AllowedHitPointsPercents.max != 100f)
			{
				float num = (float)t.HitPoints / (float)t.MaxHitPoints;
				num = GenMath.RoundedHundredth(num);
				FloatRange allowedHitPointsPercents = filter.AllowedHitPointsPercents;
				if (!((FloatRange)(ref allowedHitPointsPercents)).IncludesEpsilon(Mathf.Clamp01(num)))
				{
					return false;
				}
			}
			if (filter.AllowedQualityLevels == QualityRange.All || !QualityUtility.FollowQualityThingFilter(t.def))
			{
				return true;
			}
			QualityCategory val = default(QualityCategory);
			if (!QualityUtility.TryGetQuality(t, ref val))
			{
				val = (QualityCategory)2;
			}
			QualityRange allowedQualityLevels = filter.AllowedQualityLevels;
			return ((QualityRange)(ref allowedQualityLevels)).Includes(val);
		}

		public override void Notify_ReceivedThing(Thing newItem)
		{
			if (!AllowAdds)
			{
				BuildingUtil.DropSingleThing(newItem, this, ((Thing)this).Map, out var _);
			}
			else if (!Add(newItem))
			{
				DropThing(newItem, null);
			}
		}

		public bool DoesAccept(Thing thing)
		{
			if (!AllowAdds || thing == null || !IsOperational || !base.settings.AllowedToAccept(thing))
			{
				return false;
			}
			if (!UsesPower)
			{
				return true;
			}
			int num = (Settings.EnableEnergyBuffer ? Settings.DesiredEnergyBuffer : 10);
			CompPowerTrader obj = compPowerTrader;
			return !(((obj != null) ? new float?(((CompPower)obj).PowerNet.CurrentEnergyGainRate()) : ((float?)null)) / CompPower.WattsToWattDaysPerTick < (float)num + GetThingWeight(thing, thing.stackCount));
		}

		public bool Add(Thing thing, bool force = false)
		{
			if (force)
			{
				if (thing == null || !base.settings.AllowedToAccept(thing))
				{
					return false;
				}
			}
			else
			{
				if (!DoesAccept(thing))
				{
					return false;
				}
				if (thing.stackCount == 0)
				{
					return true;
				}
			}
			if (thing.Spawned)
			{
				((Entity)thing).DeSpawn((DestroyMode)0);
			}
			int stackCount = thing.stackCount;
			if (storedThings.TryGetValue(((object)thing.def).ToString(), out var value))
			{
				bool flag = false;
				if (thing.def.stackLimit > 1)
				{
					foreach (Thing item in value)
					{
						if (item.TryAbsorbStack(thing, false))
						{
							flag = true;
						}
					}
				}
				if (!flag)
				{
					foreach (Thing item2 in value)
					{
						if (item2 == thing)
						{
							return true;
						}
					}
					value.AddLast(thing);
				}
			}
			else
			{
				value = new LinkedList<Thing>();
				value.AddFirst(thing);
				storedThings.Add(((object)thing.def).ToString(), value);
			}
			UpdateStoredStats(thing, stackCount, isAdding: true);
			return true;
		}

		private float GetThingWeight(Thing thing, int count)
		{
			return StatExtension.GetStatValue(thing, StatDefOf.Mass, true, -1) * (float)count;
		}

		private static bool IsBodyPart(ThingDef td)
		{
			foreach (ThingCategoryDef thingCategory in td.thingCategories)
			{
				if (((Def)thingCategory).defName.Contains("BodyPart"))
				{
					return true;
				}
			}
			return false;
		}

		public IEnumerable<Thing> GetMedicalThings(bool includeBodyParts = true, bool remove = false)
		{
			List<Thing> list = new List<Thing>();
			foreach (LinkedList<Thing> item in new List<LinkedList<Thing>>(storedThings.Values))
			{
				if (item.Count <= 0)
				{
					continue;
				}
				ThingDef def = item.First.Value.def;
				if ((def == null || !def.IsMedicine) && (!includeBodyParts || !IsBodyPart(def)))
				{
					continue;
				}
				if (remove)
				{
					if (TryRemove(def, out var _))
					{
						list.AddRange(item);
					}
				}
				else
				{
					list.AddRange(item);
				}
			}
			return list;
		}

		public bool TryGetFilteredThings(Bill bill, ThingFilter filter, out List<Thing> gotten)
		{
			gotten = null;
			foreach (LinkedList<Thing> value in storedThings.Values)
			{
				if (value.Count <= 0 || !bill.IsFixedOrAllowedIngredient(value.First.Value.def))
				{
					continue;
				}
				foreach (Thing item in value)
				{
					if (filter.Allows(item))
					{
						if (gotten == null)
						{
							gotten = new List<Thing>();
						}
						gotten.Add(item);
					}
				}
			}
			return gotten != null;
		}

		public bool TryDropThings(IngredientCount ic, out List<Thing> dropped)
		{
			dropped = null;
			foreach (LinkedList<Thing> value in storedThings.Values)
			{
				if (value.Count <= 0 || !ic.filter.Allows(value.First.Value.def))
				{
					continue;
				}
				foreach (Thing item in value)
				{
					if (ic.filter.Allows(item))
					{
						DropThing(item, null);
						if (dropped == null)
						{
							dropped = new List<Thing>();
						}
						dropped.Add(item);
					}
				}
			}
			List<Thing> list = dropped;
			if (list == null)
			{
				return false;
			}
			return list.Count > 0;
		}

		public bool TryGetValue(ThingDef thingDef, out Thing t)
		{
			if (thingDef != null && storedThings.TryGetValue(((object)thingDef).ToString(), out var value) && value.Count > 0)
			{
				t = value.First.Value;
				return true;
			}
			t = null;
			return false;
		}

		public bool TryRemove(ThingFilter filter, out List<Thing> removed)
		{
			foreach (LinkedList<Thing> value2 in storedThings.Values)
			{
				if (value2.Count > 0 && filter.Allows(value2.First.Value.def))
				{
					Thing value = value2.First.Value;
					int count = Math.Min(value.stackCount, value.def.stackLimit);
					return TryRemove(value, count, out removed);
				}
			}
			removed = null;
			return false;
		}

		public bool TryRemove(Thing thing)
		{
			if (!storedThings.TryGetValue(((object)thing.def).ToString(), out var value) || !value.Remove(thing))
			{
				return false;
			}
			UpdateStoredStats(thing, thing.stackCount, isAdding: false);
			return true;
		}

		public bool TryRemove(ThingDef thingDef, out IEnumerable<Thing> removed)
		{
			if (storedThings.TryGetValue(((object)thingDef).ToString(), out var value))
			{
				storedThings.Remove(((object)thingDef).ToString());
				removed = value;
				foreach (Thing item in value)
				{
					UpdateStoredStats(item, item.stackCount, isAdding: false);
				}
				return true;
			}
			removed = null;
			return false;
		}

		public bool TryRemove(Thing thing, int count, out List<Thing> removed)
		{
			return TryRemove(thing.def, count, out removed);
		}

		public bool TryRemove(ThingDef thingDef, int count, out List<Thing> removed)
		{
			removed = null;
			if (!storedThings.TryGetValue(((object)thingDef).ToString(), out var value))
			{
				return false;
			}
			int num = count;
			int num2 = 0;
			LinkedListNode<Thing> linkedListNode = value.First;
			while (linkedListNode != null && num > 0)
			{
				Thing value2 = linkedListNode.Value;
				LinkedListNode<Thing> next = linkedListNode.Next;
				if (removed == null)
				{
					removed = new List<Thing>();
				}
				if (value2.stackCount == 0 || value2.Destroyed)
				{
					value.Remove(linkedListNode);
				}
				else if (num >= value2.stackCount)
				{
					num -= value2.stackCount;
					num2 += value2.stackCount;
					value.Remove(linkedListNode);
					removed.Add(value2);
				}
				else
				{
					num2 += num;
					while (num > 0)
					{
						int num3 = Math.Min(num, value2.def.stackLimit);
						num -= num3;
						Thing item = value2.SplitOff(num3);
						removed.Add(item);
					}
				}
				linkedListNode = next;
			}
			List<Thing> list = removed;
			if (list == null || list.Count <= 0 || num2 <= 0)
			{
				return false;
			}
			UpdateStoredStats(removed[0], num2, isAdding: false);
			return true;
		}

		private void UpdateStoredStats(Thing thing, int count, bool isAdding)
		{
			float num = GetThingWeight(thing, count);
			if (!isAdding)
			{
				num *= -1f;
				count *= -1;
			}
			storedCount += count;
			storedWeight += num;
			if (!(storedWeight < 0f))
			{
				return;
			}
			storedCount = 0;
			storedWeight = 0f;
			foreach (LinkedList<Thing> value in storedThings.Values)
			{
				foreach (Thing item in value)
				{
					_ = item;
					UpdateStoredStats(thing, count, isAdding: true);
				}
			}
		}

		public void HandleThingsOnTop()
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			if (!((Thing)this).Spawned)
			{
				return;
			}
			foreach (Thing item in ((Thing)this).Map.thingGrid.ThingsAt(((Thing)this).Position))
			{
				if (item != null && (object)item != this && !(item is Blueprint) && !(item is Building) && !Add(item) && item.Spawned)
				{
					IntVec3 position = item.Position;
					position.x++;
					item.Position = position;
				}
			}
		}

		public override void ExposeData()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Invalid comparison between Unknown and I4
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Invalid comparison between Unknown and I4
			((Building_Storage)this).ExposeData();
			if ((int)Scribe.mode == 1)
			{
				temp = new List<Thing>();
				foreach (LinkedList<Thing> value in storedThings.Values)
				{
					foreach (Thing item in value)
					{
						temp.Add(item);
					}
				}
			}
			Scribe_Collections.Look<Thing>(ref temp, "storedThings", (LookMode)2, Array.Empty<object>());
			Scribe_Values.Look<bool>(ref includeInTradeDeals, "includeInTradeDeals", true, false);
			if ((int)Scribe.mode != 3)
			{
				return;
			}
			storedThings.Clear();
			if (temp != null)
			{
				foreach (Thing item2 in temp)
				{
					if (!Add(item2))
					{
						if (ToDumpOnSpawn == null)
						{
							ToDumpOnSpawn = new List<Thing>();
						}
						ToDumpOnSpawn.Add(item2);
					}
				}
			}
			temp?.Clear();
			temp = null;
		}

		public override string GetInspectString()
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder(((Building_Storage)this).GetInspectString());
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(Environment.NewLine);
			}
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.StoragePriority")));
			stringBuilder.Append(": ");
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("StoragePriority" + ((object)base.settings.Priority/*cast due to constrained. prefix*/).ToString())));
			stringBuilder.Append(Environment.NewLine);
			if (compPowerTrader != null)
			{
				stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.StoredWeight")));
				stringBuilder.Append(": ");
				stringBuilder.Append(storedWeight.ToString("N1"));
			}
			else
			{
				stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.Count")));
				stringBuilder.Append(": ");
				stringBuilder.Append(storedCount);
			}
			stringBuilder.Append(Environment.NewLine);
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.IncludeInTradeDeals")));
			stringBuilder.Append(": ");
			stringBuilder.Append(includeInTradeDeals.ToString());
			return stringBuilder.ToString();
		}

		public override void TickRare()
		{
			((ThingWithComps)this).TickRare();
			if (((Thing)this).Spawned && ((Thing)this).Map != null && compPowerTrader != null)
			{
				compPowerTrader.PowerOutput = -1f * Settings.EnergyFactor * storedWeight;
				((CompPower)compPowerTrader).Props.basePowerConsumption = Settings.EnergyFactor * storedWeight;
			}
			long ticks = DateTime.Now.Ticks;
			if (Settings.CollectThingsAutomatically && ticks - lastAutoReclaim > Settings.TimeBetweenAutoCollectsTicks)
			{
				Reclaim();
				lastAutoReclaim = ticks;
			}
			if (!IsOperational && Settings.EmptyOnPowerLoss && storedCount > 0 && Settings.EnergyFactor > 1E-05f)
			{
				Empty(null, force: true);
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Expected O, but got Unknown
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fb: Expected O, but got Unknown
			//IL_0108: Unknown result type (might be due to invalid IL or missing references)
			//IL_010d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0118: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			//IL_012d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			//IL_0142: Unknown result type (might be due to invalid IL or missing references)
			//IL_0152: Unknown result type (might be due to invalid IL or missing references)
			//IL_0164: Unknown result type (might be due to invalid IL or missing references)
			//IL_0170: Expected O, but got Unknown
			//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0209: Unknown result type (might be due to invalid IL or missing references)
			//IL_020f: Unknown result type (might be due to invalid IL or missing references)
			//IL_021e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0224: Unknown result type (might be due to invalid IL or missing references)
			//IL_0233: Unknown result type (might be due to invalid IL or missing references)
			//IL_0243: Unknown result type (might be due to invalid IL or missing references)
			//IL_0255: Unknown result type (might be due to invalid IL or missing references)
			//IL_0261: Expected O, but got Unknown
			//IL_017d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0182: Unknown result type (might be due to invalid IL or missing references)
			//IL_019c: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f4: Expected O, but got Unknown
			IEnumerable<Gizmo> gizmos = ((Building_Storage)this).GetGizmos();
			List<Gizmo> list = ((gizmos == null) ? new List<Gizmo>(1) : gizmos.ToList());
			int hashCode = "InfiniteStorage".GetHashCode();
			list.Add((Gizmo)new Command_Action
			{
				icon = (Texture)(object)GetGizmoViewTexture(),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.ViewDesc")),
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.View")),
				activateSound = SoundDef.Named("Click"),
				action = delegate
				{
					Find.WindowStack.Add((Window)(object)new ViewUI(this));
				},
				groupKey = hashCode
			});
			hashCode++;
			list.Add((Gizmo)new Command_Action
			{
				icon = (Texture)(object)ViewUI.emptyTexture,
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.EmptyDesc")),
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.Empty")),
				activateSound = SoundDef.Named("Click"),
				action = delegate
				{
					Empty(null, force: true);
				},
				groupKey = hashCode
			});
			hashCode++;
			if (IsOperational)
			{
				list.Add((Gizmo)new Command_Action
				{
					icon = (Texture)(object)ViewUI.collectTexture,
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.CollectDesc")),
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.Collect")),
					activateSound = SoundDef.Named("Click"),
					action = delegate
					{
						CanAutoCollect = true;
						Reclaim();
					},
					groupKey = hashCode
				});
				hashCode++;
			}
			if (IncludeInWorldLookup)
			{
				list.Add((Gizmo)new Command_Action
				{
					icon = (Texture)(object)(includeInTradeDeals ? ViewUI.yesSellTexture : ViewUI.noSellTexture),
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.IncludeInTradeDealsDesc")),
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.IncludeInTradeDeals")),
					activateSound = SoundDef.Named("Click"),
					action = delegate
					{
						includeInTradeDeals = !includeInTradeDeals;
					},
					groupKey = hashCode
				});
				hashCode++;
			}
			list.Add((Gizmo)new Command_Action
			{
				icon = (Texture)(object)ViewUI.applyFiltersTexture,
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.ApplyFiltersDesc")),
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.ApplyFilters")),
				activateSound = SoundDef.Named("Click"),
				action = ApplyFilters,
				groupKey = hashCode
			});
			return list;
		}

		private bool GetIncludeInWorldLookup()
		{
			InfiniteStorageType modExtension = ((Def)((Thing)this).def).GetModExtension<InfiniteStorageType>();
			if (modExtension == null || modExtension.IncludeInWorldLookup.Length <= 0 || modExtension.IncludeInWorldLookup.ToLower()[0] != 'f')
			{
				return true;
			}
			Log.Warning("    return false");
			return false;
		}

		private Texture2D GetGizmoViewTexture()
		{
			return ViewUI.InfiniteStorageViewTexture;
		}

		private void ApplyFilters()
		{
			Empty();
			Reclaim();
		}
	}
	public class InfiniteStorageType : DefModExtension
	{
		public string IncludeInWorldLookup;
	}
	public class InfiniteStorgeGizmoViewTexture : DefModExtension
	{
		public string GizmoViewTexture;
	}
	internal static class ReservationManagerUtil
	{
		public static bool IsInfiniteStorageAt(Map map, IntVec3 position)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			IEnumerable<Thing> enumerable = map.thingGrid.ThingsAt(position);
			if (enumerable == null)
			{
				return false;
			}
			foreach (Thing item in enumerable)
			{
				if (((object)item).GetType() == typeof(Building_InfiniteStorage))
				{
					return true;
				}
			}
			return false;
		}
	}
	public class Settings : ModSettings
	{
		private const int DefaultEnergyBuffer = 100;

		private const float DefaultEnergyFactor = 1f;

		private const long DefaultTimeBetweenCollectsTicks = 100000000L;

		private static bool enableEnergyBuffer = true;

		private static int desiredEnergyBuffer = 100;

		private static float energyFactor = 1f;

		private static bool emptyOnPowerLoss;

		private static bool collectThingsAutomatically = true;

		private static long timeBetweenAutoCollects = 100000000L;

		private static string desiredEnergyBufferUserInput = desiredEnergyBuffer.ToString();

		private static string energyFactorUserInput = energyFactor.ToString();

		private static string timeBetweenAutoCollectsUserInput = TimeBetweenAutoCollectsSeconds.ToString();

		public static bool EnableEnergyBuffer => enableEnergyBuffer;

		public static int DesiredEnergyBuffer => desiredEnergyBuffer;

		public static float EnergyFactor => energyFactor;

		public static bool EmptyOnPowerLoss => emptyOnPowerLoss;

		public static bool CollectThingsAutomatically => collectThingsAutomatically;

		public static long TimeBetweenAutoCollectsTicks => timeBetweenAutoCollects;

		private static long TimeBetweenAutoCollectsSeconds => timeBetweenAutoCollects / 10000000;

		public override void ExposeData()
		{
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Invalid comparison between Unknown and I4
			((ModSettings)this).ExposeData();
			Scribe_Values.Look<bool>(ref enableEnergyBuffer, "InfiniteStorage.EnableEnergyBuffer", true, true);
			Scribe_Values.Look<int>(ref desiredEnergyBuffer, "InfiniteStorage.DesiredEnergyBuffer", 100, true);
			Scribe_Values.Look<float>(ref energyFactor, "InfiniteStorage.EnergyFactor", 1f, true);
			Scribe_Values.Look<bool>(ref emptyOnPowerLoss, "InfiniteStorage.EmptyOnPowerLoss", false, true);
			Scribe_Values.Look<bool>(ref collectThingsAutomatically, "InfiniteStorage.CollectThingsAutomatically", true, true);
			Scribe_Values.Look<long>(ref timeBetweenAutoCollects, "InfiniteStorage.TimeBetweenAutoCollects", 100000000L, true);
			if ((int)Scribe.mode != 1)
			{
				desiredEnergyBufferUserInput = desiredEnergyBuffer.ToString();
				energyFactorUserInput = energyFactor.ToString();
				timeBetweenAutoCollectsUserInput = TimeBetweenAutoCollectsSeconds.ToString();
			}
		}

		public static void DoSettingsWindowContents(Rect rect)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_020d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0217: Unknown result type (might be due to invalid IL or missing references)
			//IL_0221: Unknown result type (might be due to invalid IL or missing references)
			//IL_0250: Unknown result type (might be due to invalid IL or missing references)
			//IL_025a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0302: Unknown result type (might be due to invalid IL or missing references)
			//IL_0305: Unknown result type (might be due to invalid IL or missing references)
			//IL_0137: Unknown result type (might be due to invalid IL or missing references)
			//IL_0141: Unknown result type (might be due to invalid IL or missing references)
			//IL_0146: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_038a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0394: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_03be: Unknown result type (might be due to invalid IL or missing references)
			//IL_0405: Unknown result type (might be due to invalid IL or missing references)
			//IL_040f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0340: Unknown result type (might be due to invalid IL or missing references)
			//IL_0345: Unknown result type (might be due to invalid IL or missing references)
			//IL_0357: Unknown result type (might be due to invalid IL or missing references)
			//IL_0293: Unknown result type (might be due to invalid IL or missing references)
			//IL_0181: Unknown result type (might be due to invalid IL or missing references)
			//IL_0186: Unknown result type (might be due to invalid IL or missing references)
			//IL_0198: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0446: Unknown result type (might be due to invalid IL or missing references)
			//IL_0450: Unknown result type (might be due to invalid IL or missing references)
			//IL_045a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0477: Unknown result type (might be due to invalid IL or missing references)
			//IL_04a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_054c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0556: Unknown result type (might be due to invalid IL or missing references)
			//IL_055b: Unknown result type (might be due to invalid IL or missing references)
			//IL_055e: Unknown result type (might be due to invalid IL or missing references)
			//IL_059e: Unknown result type (might be due to invalid IL or missing references)
			//IL_05a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_05b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_04e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_05db: Unknown result type (might be due to invalid IL or missing references)
			//IL_05f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0605: Unknown result type (might be due to invalid IL or missing references)
			//IL_060a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0614: Unknown result type (might be due to invalid IL or missing references)
			//IL_050b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0510: Unknown result type (might be due to invalid IL or missing references)
			//IL_0526: Unknown result type (might be due to invalid IL or missing references)
			int num = 60;
			Widgets.CheckboxLabeled(new Rect(0f, (float)num, 300f, 32f), TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.EnableEnergyBuffer")), ref enableEnergyBuffer, false, (Texture2D)null, (Texture2D)null, false, false);
			TaggedString val;
			if (enableEnergyBuffer)
			{
				num += 40;
				desiredEnergyBufferUserInput = Widgets.TextEntryLabeled(new Rect(0f, (float)num, 300f, 32f), TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.EnergyBuffer") + ":   "), desiredEnergyBufferUserInput);
				num += 50;
				if (Widgets.ButtonText(new Rect(50f, (float)num, 100f, 32f), TaggedString.op_Implicit(Translator.Translate("Confirm")), true, true, true, (TextAnchor?)null))
				{
					if (!int.TryParse(desiredEnergyBufferUserInput, out var result) || result < 0)
					{
						Messages.Message(TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.NumberGreaterThanZero")), MessageTypeDefOf.RejectInput, true);
					}
					else
					{
						desiredEnergyBuffer = result;
						val = Translator.Translate("InfiniteStorage.EnergyBufferSet");
						Messages.Message(TaggedString.op_Implicit(((TaggedString)(ref val)).Replace("{v}", desiredEnergyBuffer.ToString())), MessageTypeDefOf.PositiveEvent, true);
					}
				}
				Rect val2 = new Rect(175f, (float)num, 100f, 32f);
				val = Translator.Translate("default");
				if (Widgets.ButtonText(val2, TaggedString.op_Implicit(((TaggedString)(ref val)).CapitalizeFirst()), true, true, true, (TextAnchor?)null))
				{
					desiredEnergyBuffer = 100;
					desiredEnergyBufferUserInput = desiredEnergyBuffer.ToString();
					val = Translator.Translate("InfiniteStorage.EnergyBufferSet");
					Messages.Message(TaggedString.op_Implicit(((TaggedString)(ref val)).Replace("{v}", desiredEnergyBuffer.ToString())), MessageTypeDefOf.PositiveEvent, true);
				}
			}
			num += 50;
			Widgets.Label(new Rect(25f, (float)num, ((Rect)(ref rect)).width - 50f, 32f), Translator.Translate("InfiniteStorage.EnergyBufferDesc"));
			num += 29;
			Widgets.DrawLineHorizontal(0f, (float)num, ((Rect)(ref rect)).width);
			num += 29;
			energyFactorUserInput = Widgets.TextEntryLabeled(new Rect(0f, (float)num, 300f, 32f), TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.EnergyFactor") + ":   "), energyFactorUserInput);
			num += 50;
			if (Widgets.ButtonText(new Rect(50f, (float)num, 100f, 32f), TaggedString.op_Implicit(Translator.Translate("Confirm")), true, true, true, (TextAnchor?)null))
			{
				if (!float.TryParse(energyFactorUserInput, out var result2) || result2 < 0f)
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.NumberGreaterThanZero")), MessageTypeDefOf.RejectInput, true);
				}
				else
				{
					energyFactor = result2;
					val = Translator.Translate("InfiniteStorage.EnergyFactorSet");
					Messages.Message(TaggedString.op_Implicit(((TaggedString)(ref val)).Replace("{v}", energyFactor.ToString())), MessageTypeDefOf.PositiveEvent, true);
				}
			}
			Rect val3 = new Rect(175f, (float)num, 100f, 32f);
			val = Translator.Translate("default");
			if (Widgets.ButtonText(val3, TaggedString.op_Implicit(((TaggedString)(ref val)).CapitalizeFirst()), true, true, true, (TextAnchor?)null))
			{
				energyFactor = 1f;
				energyFactorUserInput = energyFactor.ToString();
				val = Translator.Translate("InfiniteStorage.EnergyFactorSet");
				Messages.Message(TaggedString.op_Implicit(((TaggedString)(ref val)).Replace("{v}", energyFactor.ToString())), MessageTypeDefOf.PositiveEvent, true);
			}
			num += 50;
			Widgets.Label(new Rect(25f, (float)num, ((Rect)(ref rect)).width - 50f, 32f), Translator.Translate("InfiniteStorage.EnergyFactorDesc"));
			num += 29;
			Widgets.CheckboxLabeled(new Rect(25f, (float)num, 300f, 32f), TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.EmptyOnPowerLoss")), ref emptyOnPowerLoss, false, (Texture2D)null, (Texture2D)null, false, false);
			num += 29;
			Widgets.DrawLineHorizontal(0f, (float)num, ((Rect)(ref rect)).width);
			num += 29;
			Widgets.CheckboxLabeled(new Rect(25f, (float)num, 200f, 32f), TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.CollectThingsAutomatically")), ref collectThingsAutomatically, false, (Texture2D)null, (Texture2D)null, false, false);
			num += 40;
			if (!collectThingsAutomatically)
			{
				return;
			}
			Widgets.Label(new Rect(25f, (float)num, 300f, 32f), Translator.Translate("InfiniteStorage.TimeBetweenAutoCollects") + ":");
			timeBetweenAutoCollectsUserInput = Widgets.TextField(new Rect(310f, (float)(num - 6), 75f, 32f), timeBetweenAutoCollectsUserInput);
			num += 40;
			if (Widgets.ButtonText(new Rect(50f, (float)num, 100f, 32f), TaggedString.op_Implicit(Translator.Translate("Confirm")), true, true, true, (TextAnchor?)null))
			{
				if (!long.TryParse(timeBetweenAutoCollectsUserInput, out var result3) || result3 < 0)
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.NumberGreaterThanZero")), MessageTypeDefOf.RejectInput, true);
				}
				else
				{
					timeBetweenAutoCollects = result3 * 10000000;
					val = Translator.Translate("InfiniteStorage.TimeBetweenAutoCollectsSet");
					Messages.Message(TaggedString.op_Implicit(((TaggedString)(ref val)).Replace("{v}", TimeBetweenAutoCollectsSeconds.ToString())), MessageTypeDefOf.PositiveEvent, true);
				}
			}
			Rect val4 = new Rect(175f, (float)num, 100f, 32f);
			val = Translator.Translate("default");
			if (Widgets.ButtonText(val4, TaggedString.op_Implicit(((TaggedString)(ref val)).CapitalizeFirst()), true, true, true, (TextAnchor?)null))
			{
				timeBetweenAutoCollects = 100000000L;
				timeBetweenAutoCollectsUserInput = TimeBetweenAutoCollectsSeconds.ToString();
				val = Translator.Translate("InfiniteStorage.TimeBetweenAutoCollectsSet");
				Messages.Message(TaggedString.op_Implicit(((TaggedString)(ref val)).Replace("{v}", TimeBetweenAutoCollectsSeconds.ToString())), MessageTypeDefOf.PositiveEvent, true);
			}
			if (SettingsController.CurrentVersion != null)
			{
				num += 40;
				GUI.contentColor = Color.gray;
				Widgets.Label(new Rect(25f, (float)num, 300f, 32f), TranslatorFormattedStringExtensions.Translate("InfiniteStorage.CurrentModVersion", NamedArgument.op_Implicit(SettingsController.CurrentVersion)));
				GUI.contentColor = Color.white;
			}
		}
	}
	public class SettingsController : Mod
	{
		public static string CurrentVersion;

		public SettingsController(ModContentPack content)
			: base(content)
		{
			((Mod)this).GetSettings<Settings>();
			CurrentVersion = VersionFromManifest.GetVersionFromModMetaData(content.ModMetaData);
		}

		public override string SettingsCategory()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("InfiniteStorage"));
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			Settings.DoSettingsWindowContents(inRect);
		}
	}
	internal static class TradeUtil
	{
		public static IEnumerable<Thing> EmptyStorages(Map map)
		{
			List<Thing> list = new List<Thing>();
			foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(map))
			{
				if (((Thing)infiniteStorage).Map == map && ((Thing)infiniteStorage).Spawned && infiniteStorage.IncludeInTradeDeals)
				{
					infiniteStorage.Empty(list);
				}
			}
			return list;
		}

		public static void ReclaimThings(bool force = false)
		{
			foreach (Building_InfiniteStorage allInfiniteStorage in WorldComp.GetAllInfiniteStorages())
			{
				if (((Thing)allInfiniteStorage).Map != null && ((Thing)allInfiniteStorage).Spawned)
				{
					if (force)
					{
						allInfiniteStorage.ForceReclaim();
					}
					else
					{
						allInfiniteStorage.Reclaim();
					}
				}
			}
		}
	}
	public class WorldComp : WorldComponent
	{
		[Unsaved(false)]
		private static readonly Dictionary<Map, LinkedList<Building_InfiniteStorage>> ifStorages = new Dictionary<Map, LinkedList<Building_InfiniteStorage>>();

		[Unsaved(false)]
		private static readonly Dictionary<Map, LinkedList<Building_InfiniteStorage>> ifNonGlobalStorages = new Dictionary<Map, LinkedList<Building_InfiniteStorage>>();

		public WorldComp(World world)
			: base(world)
		{
			foreach (LinkedList<Building_InfiniteStorage> value in ifStorages.Values)
			{
				value.Clear();
			}
			ifStorages.Clear();
		}

		public static void Add(Map map, Building_InfiniteStorage storage)
		{
			if (storage == null)
			{
				Log.Error("Tried to add a null storage");
			}
			else if (map == null || ((Thing)storage).Map == null)
			{
				Log.Error("Tried to add " + ((Entity)storage).Label + " to a null map. Please let me know if this ever happens!");
			}
			else if (!storage.IncludeInWorldLookup)
			{
				add(map, storage, ifNonGlobalStorages);
			}
			else
			{
				add(map, storage, ifStorages);
			}
		}

		private static void add(Map map, Building_InfiniteStorage storage, Dictionary<Map, LinkedList<Building_InfiniteStorage>> storages)
		{
			if (!storages.TryGetValue(map, out var value))
			{
				value = new LinkedList<Building_InfiniteStorage>();
				storages.Add(map, value);
			}
			if (!value.Contains(storage))
			{
				value.AddLast(storage);
			}
		}

		public static IEnumerable<Building_InfiniteStorage> GetAllInfiniteStorages()
		{
			if (ifStorages == null)
			{
				yield break;
			}
			foreach (LinkedList<Building_InfiniteStorage> value in ifStorages.Values)
			{
				foreach (Building_InfiniteStorage item in value)
				{
					yield return item;
				}
			}
		}

		public static IEnumerable<Building_InfiniteStorage> GetInfiniteStorages(Map map)
		{
			if (map != null && ifStorages.TryGetValue(map, out var value))
			{
				return value;
			}
			return new List<Building_InfiniteStorage>(0);
		}

		public static IEnumerable<Building_InfiniteStorage> GetInfiniteStoragesWithinRadius(Map map, IntVec3 position, float ingredientSearchRadius)
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			List<Building_InfiniteStorage> list = new List<Building_InfiniteStorage>();
			if (map == null || !ifStorages.TryGetValue(map, out var value))
			{
				return list;
			}
			float num = ingredientSearchRadius * ingredientSearchRadius;
			foreach (Building_InfiniteStorage item in value)
			{
				IntVec3 val = ((Thing)item).Position - position;
				if ((float)((IntVec3)(ref val)).LengthHorizontalSquared < num)
				{
					list.Add(item);
				}
			}
			return list;
		}

		public static bool HasInfiniteStorages(Map map)
		{
			if (map != null && ifStorages.TryGetValue(map, out var value))
			{
				return value.Count > 0;
			}
			return false;
		}

		public static bool HasNonGlobalInfiniteStorages(Map map)
		{
			if (map != null && ifNonGlobalStorages.TryGetValue(map, out var value))
			{
				return value.Count > 0;
			}
			return false;
		}

		public static void Remove(Map map)
		{
			Log.Warning($"IS Map to remove: {map.uniqueID}");
			if (ifStorages.TryGetValue(map, out var value))
			{
				if (value.Count > 0)
				{
					Log.Warning($"ifStorages map: {((Thing)value.First.Value).Map.uniqueID}");
				}
				value.Clear();
				Log.Warning("removing ifStorages");
				ifStorages.Remove(map);
			}
			if (ifNonGlobalStorages.TryGetValue(map, out value))
			{
				if (value.Count > 0)
				{
					Log.Warning($"ifNonGlobalStorages map: {((Thing)value.First.Value).Map.uniqueID}");
				}
				value.Clear();
				Log.Warning("removing ifNonGlobalStorages");
				ifNonGlobalStorages.Remove(map);
			}
		}

		public static void Remove(Map map, Building_InfiniteStorage storage)
		{
			remove(map, storage, (!storage.IncludeInWorldLookup) ? ifNonGlobalStorages : ifStorages);
		}

		private static void remove(Map map, Building_InfiniteStorage storage, Dictionary<Map, LinkedList<Building_InfiniteStorage>> storages)
		{
			if (map != null && storages.TryGetValue(map, out var value))
			{
				value.Remove(storage);
				if (value.Count == 0)
				{
					ifStorages.Remove(map);
				}
			}
		}

		internal static void ClearAll()
		{
			ifStorages.Clear();
			ifNonGlobalStorages.Clear();
		}
	}
}
namespace InfiniteStorage.UI
{
	[StaticConstructorOnStartup]
	public class ViewUI : Window
	{
		private class MatchedThings : Thing
		{
			private readonly List<Thing> Things = new List<Thing>();

			public int Count;

			public Thing First => Things[0];

			public MatchedThings()
			{
			}

			public MatchedThings(Thing t)
			{
				Add(t);
			}

			public void Add(Thing t)
			{
				Things.Add(t);
				Count++;
			}
		}

		private enum Tabs
		{
			Unknown,
			InfiniteStorage_Misc,
			InfiniteStorage_Minified,
			InfiniteStorage_Apparel,
			InfiniteStorage_Weapons,
			InfiniteStorage_Chunks
		}

		private const int HEIGHT = 30;

		private const int BUFFER = 2;

		public static Texture2D BodyPartViewTexture;

		public static Texture2D TextileViewTexture;

		public static readonly Texture2D InfiniteStorageViewTexture;

		public static Texture2D TroughViewTexture;

		private static readonly Texture2D DropTexture;

		public static readonly Texture2D emptyTexture;

		public static readonly Texture2D collectTexture;

		public static readonly Texture2D yesSellTexture;

		public static readonly Texture2D noSellTexture;

		public static readonly Texture2D applyFiltersTexture;

		private static ThingCategoryDef WeaponsMeleeCategoryDef;

		private static ThingCategoryDef WeaponsRangedCategoryDef;

		private readonly List<Thing> Apparel = new List<Thing>();

		private readonly Dictionary<string, Thing> Chunks = new Dictionary<string, Thing>();

		private readonly Building_InfiniteStorage InfiniteStorage;

		private readonly List<Thing> Minified = new List<Thing>();

		private readonly Dictionary<string, Thing> Misc = new Dictionary<string, Thing>();

		private readonly List<TabRecord> tabs = new List<TabRecord>();

		private readonly List<Thing> Weapons = new List<Thing>();

		private bool itemsDropped;

		private Vector2 scrollPosition = new Vector2(0f, 0f);

		private string searchText = "";

		private Tabs selectedTab = Tabs.InfiniteStorage_Misc;

		public override Vector2 InitialSize => new Vector2(550f, 650f);

		static ViewUI()
		{
			DropTexture = ContentFinder<Texture2D>.Get("InfiniteStorage/drop", true);
			InfiniteStorageViewTexture = ContentFinder<Texture2D>.Get("InfiniteStorage/view", true);
			emptyTexture = ContentFinder<Texture2D>.Get("InfiniteStorage/empty", true);
			collectTexture = ContentFinder<Texture2D>.Get("InfiniteStorage/collect", true);
			yesSellTexture = ContentFinder<Texture2D>.Get("InfiniteStorage/yessell", true);
			noSellTexture = ContentFinder<Texture2D>.Get("InfiniteStorage/nosell", true);
			applyFiltersTexture = ContentFinder<Texture2D>.Get("InfiniteStorage/filter", true);
		}

		public ViewUI(Building_InfiniteStorage thingStorage)
			: base((IWindowDrawing)null)
		{
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			InfiniteStorage = thingStorage;
			base.closeOnClickedOutside = true;
			base.doCloseButton = true;
			base.doCloseX = true;
			base.absorbInputAroundWindow = true;
			base.forcePause = true;
			PopulateDisplayThings();
		}

		private void PopulateDisplayThings()
		{
			if (WeaponsMeleeCategoryDef == null)
			{
				foreach (ThingCategoryDef item in DefDatabase<ThingCategoryDef>.AllDefsListForReading)
				{
					if (WeaponsMeleeCategoryDef != null && WeaponsRangedCategoryDef != null)
					{
						break;
					}
					if (GenText.EqualsIgnoreCase(((Def)item).defName, "WeaponsMelee"))
					{
						WeaponsMeleeCategoryDef = item;
					}
					else if (GenText.EqualsIgnoreCase(((Def)item).defName, "WeaponsRanged"))
					{
						WeaponsRangedCategoryDef = item;
					}
				}
			}
			Misc.Clear();
			Minified.Clear();
			Apparel.Clear();
			Weapons.Clear();
			Chunks.Clear();
			foreach (Thing storedThing in InfiniteStorage.StoredThings)
			{
				if (storedThing.def.IsApparel)
				{
					Apparel.Add(storedThing);
				}
				else if (storedThing.def.thingCategories.Contains(WeaponsMeleeCategoryDef) || storedThing.def.thingCategories.Contains(WeaponsRangedCategoryDef))
				{
					Weapons.Add(storedThing);
				}
				else if (storedThing is MinifiedThing)
				{
					Minified.Add(storedThing);
				}
				else if (isChunk(storedThing.def))
				{
					addSpecial(storedThing, Chunks);
				}
				else
				{
					addSpecial(storedThing, Misc);
				}
			}
			if ((selectedTab == Tabs.InfiniteStorage_Misc && Misc.Count == 0) || (selectedTab == Tabs.InfiniteStorage_Minified && Minified.Count == 0) || (selectedTab == Tabs.InfiniteStorage_Apparel && Apparel.Count == 0) || (selectedTab == Tabs.InfiniteStorage_Weapons && Weapons.Count == 0) || (selectedTab == Tabs.InfiniteStorage_Chunks && Chunks.Count == 0))
			{
				selectedTab = Tabs.Unknown;
			}
			if (selectedTab == Tabs.Unknown)
			{
				if (Misc.Count > 0)
				{
					selectedTab = Tabs.InfiniteStorage_Misc;
				}
				else if (Minified.Count > 0)
				{
					selectedTab = Tabs.InfiniteStorage_Minified;
				}
				else if (Apparel.Count > 0)
				{
					selectedTab = Tabs.InfiniteStorage_Apparel;
				}
				else if (Weapons.Count > 0)
				{
					selectedTab = Tabs.InfiniteStorage_Weapons;
				}
				else if (Chunks.Count > 0)
				{
					selectedTab = Tabs.InfiniteStorage_Chunks;
				}
			}
		}

		private static void addSpecial(Thing t, Dictionary<string, Thing> d)
		{
			if (t.def.stackLimit == 1)
			{
				if (d.TryGetValue(((Def)t.def).defName, out var value) && value is MatchedThings matchedThings)
				{
					matchedThings.Add(t);
				}
				else
				{
					d[((Def)t.def).defName] = (Thing)(object)new MatchedThings(t);
				}
			}
			else
			{
				d[((Def)t.def).defName] = t;
			}
		}

		private static bool isChunk(ThingDef def)
		{
			foreach (ThingCategoryDef thingCategory in def.thingCategories)
			{
				if (thingCategory == ThingCategoryDefOf.Chunks || thingCategory == ThingCategoryDefOf.StoneChunks)
				{
					return true;
				}
			}
			return false;
		}

		public override void PreClose()
		{
			((Window)this).PreClose();
			Misc.Clear();
			Chunks.Clear();
			Minified.Clear();
			Apparel.Clear();
			Weapons.Clear();
			if (itemsDropped && InfiniteStorage != null)
			{
				InfiniteStorage.ResetAutoReclaimTime();
			}
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_010b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_011c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_016e: Unknown result type (might be due to invalid IL or missing references)
			GUI.color = Color.white;
			Text.Font = (GameFont)1;
			try
			{
				int num = 90;
				int rows;
				IEnumerable<Thing> thingsToShow = getThingsToShow(out rows);
				if (rows == 0)
				{
					return;
				}
				searchText = Widgets.TextEntryLabeled(new Rect(20f, 15f, 300f, 32f), TaggedString.op_Implicit(Translator.Translate("InfiniteStorage.Search") + ": "), searchText).ToLower().Trim();
				searchText = Regex.Replace(searchText, "\\t|\\n|\\r", "");
				if (searchText.Length == 0)
				{
					TabDrawer.DrawTabs<TabRecord>(new Rect(0f, (float)num, ((Rect)(ref inRect)).width, ((Rect)(ref inRect)).height - (float)num), tabs, 200f);
					num += 32;
				}
				Rect val = default(Rect);
				((Rect)(ref val))..ctor(0f, (float)num, 368f, (float)((rows + 1) * 32));
				scrollPosition = GUI.BeginScrollView(new Rect(50f, (float)num, ((Rect)(ref val)).width + 18f, ((Rect)(ref inRect)).height - (float)num - 75f), scrollPosition, val);
				int num2 = 0;
				foreach (Thing item in thingsToShow)
				{
					if (item == null)
					{
						continue;
					}
					string text = formatLabel(item);
					if (searchText.Length == 0 || text.ToLower().Contains(searchText))
					{
						if (drawRow(item, text, num, num2, val))
						{
							break;
						}
						num2++;
					}
				}
				GUI.EndScrollView();
			}
			catch (Exception ex)
			{
				string text2 = ((object)this).GetType().Name + " closed due to: " + ex.GetType().Name + " " + ex.Message;
				Log.Error(text2);
				Messages.Message(text2, MessageTypeDefOf.NegativeEvent, true);
				((Window)this).Close(true);
			}
			finally
			{
				Text.Anchor = (TextAnchor)0;
				GUI.color = Color.white;
			}
		}

		private bool drawRow(Thing thing, string label, float y, int i, Rect r)
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Expected O, but got Unknown
			//IL_00de: Unknown result type (might be due to invalid IL or missing references)
			Thing val = ((thing is MatchedThings matchedThings) ? matchedThings.First : thing);
			GUI.BeginGroup(new Rect(0f, y + (float)(i * 32), ((Rect)(ref r)).width, 30f));
			Widgets.ThingIcon(new Rect(0f, 0f, 30f, 30f), val, 1f, (Rot4?)null, false, 1f, false);
			if (Widgets.InfoCardButton(40f, 0f, val))
			{
				Find.WindowStack.Add((Window)new Dialog_InfoCard(val, (Precept_ThingStyle)null));
			}
			Widgets.Label(new Rect(70f, 0f, ((Rect)(ref r)).width - 110f, 30f), label);
			if (InfiniteStorage.IsOperational && Widgets.ButtonImage(new Rect(((Rect)(ref r)).xMax - 20f, 0f, 20f, 20f), DropTexture, true, (string)null))
			{
				InfiniteStorage.AllowAdds = false;
				if (InfiniteStorage.TryRemove(val))
				{
					BuildingUtil.DropThing(val, val.stackCount, InfiniteStorage, ((Thing)InfiniteStorage).Map);
					itemsDropped = true;
				}
				PopulateDisplayThings();
				return true;
			}
			GUI.EndGroup();
			return false;
		}

		private static string formatLabel(Thing thing)
		{
			Thing val = ((thing is MatchedThings matchedThings) ? matchedThings.First : thing);
			if (val is MinifiedThing || val.def.IsApparel || val.def.thingCategories.Contains(WeaponsMeleeCategoryDef) || val.def.thingCategories.Contains(WeaponsRangedCategoryDef))
			{
				return ((Entity)val).Label;
			}
			StringBuilder stringBuilder = new StringBuilder(((Def)val.def).label);
			if (val.Stuff != null)
			{
				stringBuilder.Append(" (");
				stringBuilder.Append(val.Stuff.LabelAsStuff);
				stringBuilder.Append(")");
			}
			if (thing is MatchedThings matchedThings2)
			{
				stringBuilder.Append(" x");
				stringBuilder.Append(matchedThings2.Count);
			}
			else if (val.stackCount > 0)
			{
				stringBuilder.Append(" x");
				stringBuilder.Append(val.stackCount);
			}
			return stringBuilder.ToString();
		}

		private IEnumerable<Thing> getThingsToShow(out int rows)
		{
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Expected O, but got Unknown
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Expected O, but got Unknown
			//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Expected O, but got Unknown
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_0135: Unknown result type (might be due to invalid IL or missing references)
			//IL_013f: Expected O, but got Unknown
			//IL_0158: Unknown result type (might be due to invalid IL or missing references)
			//IL_0177: Unknown result type (might be due to invalid IL or missing references)
			//IL_0181: Expected O, but got Unknown
			IEnumerable<Thing> result;
			if (searchText.Length > 0)
			{
				result = InfiniteStorage.StoredThings;
				rows = InfiniteStorage.DefsCount;
			}
			else
			{
				tabs.Clear();
				if (Misc.Count > 0)
				{
					tabs.Add(new TabRecord(TaggedString.op_Implicit(Translator.Translate("InfiniteStorage_Misc")), (Action)delegate
					{
						selectedTab = Tabs.InfiniteStorage_Misc;
					}, selectedTab == Tabs.InfiniteStorage_Misc));
				}
				if (Minified.Count > 0)
				{
					tabs.Add(new TabRecord(TaggedString.op_Implicit(Translator.Translate("InfiniteStorage_Minified")), (Action)delegate
					{
						selectedTab = Tabs.InfiniteStorage_Minified;
					}, selectedTab == Tabs.InfiniteStorage_Minified));
				}
				if (Apparel.Count > 0)
				{
					tabs.Add(new TabRecord(TaggedString.op_Implicit(Translator.Translate("InfiniteStorage_Apparel")), (Action)delegate
					{
						selectedTab = Tabs.InfiniteStorage_Apparel;
					}, selectedTab == Tabs.InfiniteStorage_Apparel));
				}
				if (Weapons.Count > 0)
				{
					tabs.Add(new TabRecord(TaggedString.op_Implicit(Translator.Translate("InfiniteStorage_Weapons")), (Action)delegate
					{
						selectedTab = Tabs.InfiniteStorage_Weapons;
					}, selectedTab == Tabs.InfiniteStorage_Weapons));
				}
				if (Chunks.Count > 0)
				{
					tabs.Add(new TabRecord(TaggedString.op_Implicit(Translator.Translate("InfiniteStorage_Chunks")), (Action)delegate
					{
						selectedTab = Tabs.InfiniteStorage_Chunks;
					}, selectedTab == Tabs.InfiniteStorage_Chunks));
				}
				switch (selectedTab)
				{
				case Tabs.InfiniteStorage_Misc:
					result = Misc.Values;
					rows = Misc.Count;
					break;
				case Tabs.InfiniteStorage_Minified:
					result = Minified;
					rows = Minified.Count;
					break;
				case Tabs.InfiniteStorage_Apparel:
					result = Apparel;
					rows = Apparel.Count;
					break;
				case Tabs.InfiniteStorage_Weapons:
					result = Weapons;
					rows = Weapons.Count;
					break;
				default:
					result = Chunks.Values;
					rows = Chunks.Count;
					break;
				}
			}
			return result;
		}
	}
}
namespace InfiniteStorage.HarmonyPatches
{
	[HarmonyPatch(typeof(Building_Storage), "Accepts")]
	internal static class Building_Storage_Accepts
	{
		[HarmonyPriority(800)]
		private static bool Prefix(Building_Storage __instance, ref bool __result, Thing t)
		{
			if (!(__instance is Building_InfiniteStorage building_InfiniteStorage))
			{
				return true;
			}
			__result = building_InfiniteStorage.DoesAccept(t);
			return false;
		}
	}
	[HarmonyPatch(typeof(Designator_Build), "ProcessInput")]
	internal static class Designator_Build_ProcessInput
	{
		private static bool Prefix(Designator_Build __instance, Event ev)
		{
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0100: Expected O, but got Unknown
			//IL_0241: Unknown result type (might be due to invalid IL or missing references)
			//IL_0246: Unknown result type (might be due to invalid IL or missing references)
			//IL_024f: Expected O, but got Unknown
			//IL_0229: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01de: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e8: Expected O, but got Unknown
			Map currentMap = Find.CurrentMap;
			BuildableDef entDef = __instance.entDef;
			ThingDef val = (ThingDef)(object)((entDef is ThingDef) ? entDef : null);
			if (val == null || !entDef.MadeFromStuff || !WorldComp.HasInfiniteStorages(currentMap))
			{
				return true;
			}
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			foreach (ThingDef current2 in currentMap.resourceCounter.AllCountedAmounts.Keys)
			{
				if (current2.IsStuff && current2.stuffProps.CanMake((BuildableDef)(object)val) && (DebugSettings.godMode || currentMap.listerThings.ThingsOfDef(current2).Count > 0))
				{
					string text = TaggedString.op_Implicit(((Def)current2).LabelCap);
					list.Add(new FloatMenuOption(text, (Action)delegate
					{
						((Gizmo)__instance).ProcessInput(ev);
						Find.DesignatorManager.Select((Designator)(object)__instance);
						__instance.stuffDef = current2;
						__instance.writeStuff = true;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
				}
			}
			foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(currentMap))
			{
				if (!((Thing)infiniteStorage).Spawned)
				{
					continue;
				}
				foreach (Thing storedThing in infiniteStorage.StoredThings)
				{
					ThingDef current5 = storedThing.def;
					if (current5.IsStuff && current5.stuffProps.CanMake((BuildableDef)(object)val) && (DebugSettings.godMode || storedThing.stackCount > 0))
					{
						string text2 = TaggedString.op_Implicit(((Def)current5).LabelCap);
						list.Add(new FloatMenuOption(text2, (Action)delegate
						{
							((Gizmo)__instance).ProcessInput(ev);
							Find.DesignatorManager.Select((Designator)(object)__instance);
							__instance.stuffDef = current5;
							__instance.writeStuff = true;
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					}
				}
			}
			if (list.Count == 0)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("NoStuffsToBuildWith")), MessageTypeDefOf.RejectInput, true);
			}
			else
			{
				FloatMenu val2 = new FloatMenu(list)
				{
					vanishIfMouseDistant = true
				};
				Find.WindowStack.Add((Window)(object)val2);
				Find.DesignatorManager.Select((Designator)(object)__instance);
			}
			return false;
		}
	}
	[HarmonyPatch(typeof(Dialog_FormCaravan), "PostOpen")]
	internal static class Dialog_FormCaravan_PostOpen
	{
		private static void Prefix(Window __instance, Map ___map)
		{
			if (!(((object)__instance).GetType() == typeof(Dialog_FormCaravan)))
			{
				return;
			}
			foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(___map))
			{
				infiniteStorage.Empty();
			}
		}
	}
	[HarmonyPatch(typeof(FactionDialogMaker), "AmountSendableSilver")]
	public static class FactionDialogMaker_AmountSendableSilver
	{
		private static void Prefix(Map map)
		{
			foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(map))
			{
				if (!infiniteStorage.TryRemove(ThingDefOf.Silver, out var removed))
				{
					continue;
				}
				foreach (Thing item in removed)
				{
					BuildingUtil.DropThing(item, item.stackCount, infiniteStorage, ((Thing)infiniteStorage).Map);
				}
			}
		}
	}
	[StaticConstructorOnStartup]
	internal class HarmonyPatches
	{
		static HarmonyPatches()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			new Harmony("com.InfiniteStorage.rimworld.mod").PatchAll(Assembly.GetExecutingAssembly());
		}
	}
	[HarmonyPatch(typeof(HealthAIUtility), "FindBestMedicine")]
	public static class HealthAIUtility_FindBestMedicine
	{
		[HarmonyPriority(800)]
		private static void Prefix(Pawn patient)
		{
			foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(((Thing)patient).Map))
			{
				foreach (Thing medicalThing in infiniteStorage.GetMedicalThings(includeBodyParts: false, remove: true))
				{
					BuildingUtil.DropThing(medicalThing, medicalThing.stackCount, infiniteStorage, ((Thing)infiniteStorage).Map);
				}
			}
		}
	}
	[HarmonyPatch(typeof(HealthCardUtility), "DrawMedOperationsTab")]
	public static class HealthCardUtility_DrawMedOperationsTab
	{
		private static long lastUpdate;

		private static readonly List<Thing> cache = new List<Thing>();

		[HarmonyPriority(800)]
		private static void Prefix()
		{
			ListerThings_ThingsInGroup.AvailableMedicalThing.Clear();
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return;
			}
			long ticks = DateTime.Now.Ticks;
			if (cache == null || ticks - lastUpdate > 10000000)
			{
				cache?.Clear();
				foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(currentMap))
				{
					if (((Def)((Thing)infiniteStorage).def).defName.Equals("IS_BodyPartStorage") || ((Def)((Thing)infiniteStorage).def).defName.Equals("InfiniteStorage"))
					{
						cache?.AddRange(infiniteStorage.GetMedicalThings());
					}
				}
				lastUpdate = ticks;
			}
			if (cache != null)
			{
				ListerThings_ThingsInGroup.AvailableMedicalThing.AddRange(cache);
			}
		}

		private static void Postfix()
		{
			ListerThings_ThingsInGroup.AvailableMedicalThing.Clear();
		}
	}
	[HarmonyPatch(typeof(HistoryAutoRecorder), "ExposeData")]
	public static class HistoryAutoRecorder_ExposeData
	{
		private static void Postfix(HistoryAutoRecorder __instance)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Invalid comparison between Unknown and I4
			if ((int)Scribe.mode != 4)
			{
				return;
			}
			List<float> records = __instance.records;
			for (int i = 0; i < records.Count; i++)
			{
				if (i <= 0 || !(records[i] > 10000f))
				{
					continue;
				}
				float num = records[i - 1] * 1.5f;
				if (records[i] > num)
				{
					int j;
					for (j = i + 1; j < records.Count && !(records[j] < num); j++)
					{
					}
					float value = ((j == records.Count) ? records[i] : ((records[i - 1] + records[j]) * 0.5f));
					for (; i < j; i++)
					{
						records[i] = value;
					}
				}
			}
			__instance.records = records;
		}
	}
	[HarmonyPatch(typeof(ItemAvailability), "ThingsAvailableAnywhere")]
	internal static class ItemAvailability_ThingsAvailableAnywhere
	{
		private static void Postfix(ref bool __result, ItemAvailability __instance, ThingDef need, int amount, Pawn pawn)
		{
			if (__result || pawn == null || ((Thing)pawn).Faction != Faction.OfPlayer)
			{
				return;
			}
			foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(((Thing)pawn).Map))
			{
				if (!infiniteStorage.IsOperational || !((Thing)infiniteStorage).Spawned || need == null || !infiniteStorage.TryGetValue(need, out var t) || t.stackCount < amount)
				{
					continue;
				}
				int count = ((amount < t.def.stackLimit) ? t.def.stackLimit : amount);
				if (!infiniteStorage.TryRemove(t, count, out var removed))
				{
					break;
				}
				foreach (Thing item in removed)
				{
					BuildingUtil.DropThing(item, item.stackCount, infiniteStorage, ((Thing)infiniteStorage).Map);
				}
				__result = true;
				__instance.cachedResults[Gen.HashCombine<Faction>(((object)need).GetHashCode(), ((Thing)pawn).Faction)] = __result;
				break;
			}
		}
	}
	[HarmonyPatch(typeof(JobDriver_ManTurret), "FindAmmoForTurret")]
	internal static class JobDriver_ManTurret_FindAmmoForTurret
	{
		private static void Prefix(Pawn pawn, Building_TurretGun gun)
		{
			if (!pawn.IsColonist || ((Thing)pawn).Map != ((Thing)gun).Map)
			{
				return;
			}
			StorageSettings allowedShellsSettings = ThingCompUtility.TryGetComp<CompChangeableProjectile>(gun.gun).allowedShellsSettings;
			foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(((Thing)gun).Map))
			{
				if (!infiniteStorage.TryRemove(allowedShellsSettings.filter, out var removed))
				{
					continue;
				}
				foreach (Thing item in removed)
				{
					BuildingUtil.DropThing(item, item.stackCount, infiniteStorage, ((Thing)infiniteStorage).Map, new List<Thing>());
				}
			}
		}
	}
	[HarmonyPatch(typeof(ListerThings), "ThingsInGroup")]
	public static class ListerThings_ThingsInGroup
	{
		public static readonly List<Thing> AvailableMedicalThing = new List<Thing>();

		private static void Postfix(ref List<Thing> __result)
		{
			if (AvailableMedicalThing.Count > 0)
			{
				__result.AddRange(AvailableMedicalThing);
			}
		}
	}
	[HarmonyPatch(typeof(MoveColonyUtility), "MoveColonyAndReset")]
	internal static class MoveColonyUtility_MoveColonyAndReset
	{
		[HarmonyPriority(800)]
		private static void Prefix()
		{
			WorldComp.ClearAll();
		}
	}
	[HarmonyPatch(typeof(TradeShip), "ColonyThingsWillingToBuy")]
	internal static class PassingShip_TryOpenComms
	{
		private static void Postfix(ref IEnumerable<Thing> __result, Pawn playerNegotiator)
		{
			if (playerNegotiator != null && ((Thing)playerNegotiator).Map != null)
			{
				List<Thing> list = new List<Thing>(__result);
				list.AddRange(TradeUtil.EmptyStorages(((Thing)playerNegotiator).Map));
				__result = list;
			}
		}
	}
	[HarmonyPatch(typeof(RecipeWorkerCounter), "CountProducts")]
	internal static class RecipeWorkerCounter_CountProducts
	{
		private static void Postfix(ref int __result, RecipeWorkerCounter __instance, Bill_Production bill)
		{
			List<ThingDefCountClass> products = __instance.recipe.products;
			if (!WorldComp.HasInfiniteStorages(((Bill)bill).Map) || products == null)
			{
				return;
			}
			foreach (ThingDefCountClass item in products)
			{
				ThingDef thingDef = item.thingDef;
				foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(((Bill)bill).Map))
				{
					__result += infiniteStorage.StoredThingCount(thingDef, ((Bill)bill).ingredientFilter);
				}
			}
		}
	}
	public static class RefuelPatchUtil
	{
		private static Dictionary<Thing, Building_InfiniteStorage> droppedAndStorage;

		internal static void Prefix(Pawn pawn, Thing refuelable)
		{
			if (!WorldComp.HasInfiniteStorages(refuelable.Map))
			{
				return;
			}
			droppedAndStorage = new Dictionary<Thing, Building_InfiniteStorage>();
			ThingFilter fuelFilter = ThingCompUtility.TryGetComp<CompRefuelable>(refuelable).Props.fuelFilter;
			foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(refuelable.Map))
			{
				if (!((Thing)infiniteStorage).Spawned || ((Thing)infiniteStorage).Map != ((Thing)pawn).Map || !infiniteStorage.IsOperational || !infiniteStorage.TryRemove(fuelFilter, out var removed))
				{
					continue;
				}
				List<Thing> list = new List<Thing>();
				foreach (Thing item in removed)
				{
					BuildingUtil.DropThing(item, item.def.stackLimit, infiniteStorage, ((Thing)infiniteStorage).Map, list);
				}
				if (list.Count > 0)
				{
					droppedAndStorage.Add(list[0], infiniteStorage);
				}
			}
		}

		internal static void Postfix(Thing __result)
		{
			if (droppedAndStorage == null)
			{
				return;
			}
			foreach (KeyValuePair<Thing, Building_InfiniteStorage> item in droppedAndStorage)
			{
				if (item.Key != __result)
				{
					item.Value.Add(item.Key);
				}
			}
			droppedAndStorage.Clear();
		}

		internal static void Postfix(List<Thing> __result)
		{
			if (droppedAndStorage == null || __result == null)
			{
				return;
			}
			HashSet<Thing> hashSet = new HashSet<Thing>();
			foreach (Thing item in __result)
			{
				hashSet.Add(item);
			}
			foreach (KeyValuePair<Thing, Building_InfiniteStorage> item2 in droppedAndStorage)
			{
				if (!hashSet.Contains(item2.Key))
				{
					item2.Value.Add(item2.Key);
				}
			}
			hashSet.Clear();
			droppedAndStorage.Clear();
		}
	}
	[HarmonyPatch(typeof(RefuelWorkGiverUtility), "FindAllFuel")]
	public static class RefuelWorkGiverUtility_FindAllFuel
	{
		private static void Prefix(Pawn pawn, Thing refuelable)
		{
			RefuelPatchUtil.Prefix(pawn, refuelable);
		}

		private static void Postfix(List<Thing> __result)
		{
			RefuelPatchUtil.Postfix(__result);
		}
	}
	[HarmonyPatch(typeof(RefuelWorkGiverUtility), "FindBestFuel")]
	public static class RefuelWorkGiverUtility_FindBestFuel
	{
		private static void Prefix(Pawn pawn, Thing refuelable)
		{
			RefuelPatchUtil.Prefix(pawn, refuelable);
		}

		private static void Postfix(Thing __result)
		{
			RefuelPatchUtil.Postfix(__result);
		}
	}
	[HarmonyPatch(typeof(ReservationManager), "CanReserve")]
	internal static class ReservationManager_CanReserve
	{
		private static bool Prefix(ref bool __result, ReservationManager __instance, LocalTargetInfo target)
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			if (__result || target == LocalTargetInfo.op_Implicit((Thing)null) || !((LocalTargetInfo)(ref target)).IsValid || ((LocalTargetInfo)(ref target)).ThingDestroyed || !ReservationManagerUtil.IsInfiniteStorageAt(__instance.map, ((LocalTargetInfo)(ref target)).Cell))
			{
				return true;
			}
			__result = true;
			return false;
		}
	}
	[HarmonyPatch(typeof(ReservationManager), "Release")]
	internal static class ReservationManager_Release
	{
		private static bool Prefix(ReservationManager __instance, LocalTargetInfo target)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			if (!(target == LocalTargetInfo.op_Implicit((Thing)null)) && ((LocalTargetInfo)(ref target)).IsValid && !((LocalTargetInfo)(ref target)).ThingDestroyed)
			{
				return !ReservationManagerUtil.IsInfiniteStorageAt(__instance.map, ((LocalTargetInfo)(ref target)).Cell);
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(ReservationManager), "Reserve")]
	internal static class ReservationManager_Reserve
	{
		private static bool Prefix(ref bool __result, ReservationManager __instance, LocalTargetInfo target)
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			if (__result || target == LocalTargetInfo.op_Implicit((Thing)null) || !((LocalTargetInfo)(ref target)).IsValid || ((LocalTargetInfo)(ref target)).ThingDestroyed || !ReservationManagerUtil.IsInfiniteStorageAt(__instance.map, ((LocalTargetInfo)(ref target)).Cell))
			{
				return true;
			}
			__result = true;
			return false;
		}
	}
	[HarmonyPatch(typeof(ScribeSaver), "InitSaving")]
	internal static class ScribeSaver_InitSaving
	{
		private static void Prefix()
		{
			try
			{
				foreach (Building_InfiniteStorage allInfiniteStorage in WorldComp.GetAllInfiniteStorages())
				{
					try
					{
						allInfiniteStorage.ForceReclaim();
					}
					catch (Exception ex)
					{
						Log.Warning("Error while reclaiming apparel for infinite storage\n" + ex.Message);
					}
				}
			}
			catch (Exception ex2)
			{
				Log.Warning("Error while reclaiming items\n" + ex2.Message);
			}
		}
	}
	[HarmonyPatch(typeof(SettlementAbandonUtility), "Abandon")]
	internal static class SettlementAbandonUtility_Abandon
	{
		[HarmonyPriority(800)]
		private static void Prefix(MapParent settlement)
		{
			WorldComp.Remove(settlement.Map);
		}
	}
	[HarmonyPatch(typeof(TradeDeal), "Reset")]
	internal static class TradeDeal_Reset
	{
		private static void Prefix()
		{
			TradeUtil.ReclaimThings();
		}
	}
	[HarmonyPatch(typeof(Pawn_TraderTracker), "ColonyThingsWillingToBuy")]
	internal static class TradeShip_ColonyThingsWillingToBuy
	{
		private static void Postfix(ref IEnumerable<Thing> __result, Pawn playerNegotiator)
		{
			if (playerNegotiator != null && ((Thing)playerNegotiator).Map != null)
			{
				List<Thing> list = new List<Thing>(__result);
				list.AddRange(TradeUtil.EmptyStorages(((Thing)playerNegotiator).Map));
				__result = list;
			}
		}
	}
	[HarmonyPatch(typeof(WealthWatcher), "ForceRecount")]
	public static class WealthWatcher_ForceRecount
	{
		private static float lastItemWealth = -1f;

		[HarmonyPriority(0)]
		private static void Postfix(Map ___map, ref float ___wealthItems)
		{
			float wealthItems = ___wealthItems;
			float num = tallyWealth(WorldComp.GetInfiniteStorages(___map), wealthItems);
			if (lastItemWealth < 1f)
			{
				lastItemWealth = num;
			}
			else if (num > lastItemWealth * 5f)
			{
				float num2 = lastItemWealth;
				wealthItems = num;
				num = num2;
				lastItemWealth = wealthItems;
			}
			else
			{
				lastItemWealth = num;
			}
			___wealthItems = num;
		}

		private static float tallyWealth(IEnumerable<Building_InfiniteStorage> storages, float wealthItems)
		{
			foreach (Building_InfiniteStorage storage in storages)
			{
				foreach (Thing storedThing in storage.StoredThings)
				{
					wealthItems = ((!(storedThing is ThingWithComps)) ? (wealthItems + (float)storedThing.stackCount * storedThing.def.BaseMarketValue) : (wealthItems + (float)storedThing.stackCount * storedThing.MarketValue));
				}
			}
			return wealthItems;
		}
	}
	[HarmonyPatch(typeof(Dialog_Trade), "Close")]
	internal static class Window_PreClose
	{
		[HarmonyPriority(800)]
		private static void Postfix()
		{
			TradeUtil.ReclaimThings(force: true);
		}
	}
	[HarmonyPriority(800)]
	[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestIngredientsHelper")]
	internal static class WorkGiver_DoBill_TryFindBestIngredientsHelper
	{
		private static void Prefix(List<IngredientCount> ingredients, Pawn pawn, ref List<Pair<Building_InfiniteStorage, List<Thing>>> __state)
		{
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			if (ingredients == null || pawn == null)
			{
				return;
			}
			foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(((Thing)pawn).Map))
			{
				List<Thing> list = null;
				foreach (IngredientCount ingredient in ingredients)
				{
					if (infiniteStorage.TryDropThings(ingredient, out var dropped))
					{
						if (list == null)
						{
							list = new List<Thing>();
						}
						list.AddRange(dropped);
					}
				}
				if (list != null && list.Count > 0)
				{
					if (__state == null)
					{
						__state = new List<Pair<Building_InfiniteStorage, List<Thing>>>();
					}
					__state.Add(new Pair<Building_InfiniteStorage, List<Thing>>(infiniteStorage, list));
				}
			}
		}

		private static void Postfix(ref bool __result, Pawn pawn, List<IngredientCount> ingredients, List<ThingCount> chosen, ref List<Pair<Building_InfiniteStorage, List<Thing>>> __state)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			if (!__result)
			{
				if (__state == null)
				{
					return;
				}
				{
					foreach (Pair<Building_InfiniteStorage, List<Thing>> item in __state)
					{
						foreach (Thing item2 in item.Second)
						{
							item.First.Add(item2);
						}
					}
					return;
				}
			}
			if (pawn == null || ingredients == null || chosen == null)
			{
				return;
			}
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			foreach (ThingCount item3 in chosen)
			{
				ThingCount current3 = item3;
				dictionary.Add(((ThingCount)(ref current3)).Thing.thingIDNumber, ((ThingCount)(ref current3)).Count);
			}
			foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(((Thing)pawn).Map))
			{
				infiniteStorage.ReclaimFaster(respectReserved: true, dictionary);
			}
		}
	}
	[HarmonyPatch(typeof(WorkGiver_FixBrokenDownBuilding), "FindClosestComponent")]
	internal static class WorkGiver_FixBrokenDownBuilding_FindClosestComponent
	{
		private static void Postfix(ref Thing __result, Pawn pawn)
		{
			//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			bool flag = false;
			if (pawn == null || __result != null)
			{
				return;
			}
			foreach (Building_InfiniteStorage infiniteStorage in WorldComp.GetInfiniteStorages(((Thing)pawn).Map))
			{
				if (!infiniteStorage.TryRemove(ThingDefOf.ComponentIndustrial, 1, out var removed))
				{
					continue;
				}
				flag = true;
				foreach (Thing item in removed)
				{
					BuildingUtil.DropThing(item, 1, infiniteStorage, ((Thing)infiniteStorage).Map);
				}
			}
			if (flag)
			{
				__result = GenClosest.ClosestThingReachable(((Thing)pawn).Position, ((Thing)pawn).Map, ThingRequest.ForDef(ThingDefOf.ComponentIndustrial), (PathEndMode)4, TraverseParms.For(pawn, DangerUtility.NormalMaxDanger(pawn), (TraverseMode)0, false, false, false, true), 9999f, (Predicate<Thing>)((Thing x) => !ForbidUtility.IsForbidden(x, pawn) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(x), 1, -1, (ReservationLayerDef)null, false)), (IEnumerable<Thing>)null, 0, -1, false, (RegionType)14, false, false);
			}
		}
	}
	[HarmonyPatch]
	public static class WorkGiver_Tend_Smart_Medicine
	{
		private static MethodBase target;

		private static bool Prepare()
		{
			ModContentPack val = LoadedModManager.RunningMods.FirstOrDefault((ModContentPack m) => m.Name == "Smart Medicine");
			if (val == null)
			{
				return false;
			}
			Type type = val.assemblies.loadedAssemblies.FirstOrDefault((Assembly a) => a.GetName().Name == "SmartMedicine")?.GetType("SmartMedicine.FindBestMedicine");
			if (type == null)
			{
				Log.Warning("InfiniteStorage can't patch 'Smart Medicine'");
				return false;
			}
			target = AccessTools.DeclaredMethod(type, "Find", (Type[])null, (Type[])null);
			if (target != null)
			{
				return true;
			}
			Log.Warning("InfiniteStorage can't patch 'Smart Medicine' Find");
			return false;
		}

		private static MethodBase TargetMethod()
		{
			return target;
		}

		private static void Postfix(ref List<ThingCount> __result, Pawn healer, Pawn patient)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			if (((Thing)healer).Map != ((Thing)patient).Map)
			{
				return;
			}
			foreach (Building_InfiniteStorage item in WorldComp.GetInfiniteStoragesWithinRadius(((Thing)healer).Map, ((Thing)patient).Position, 20f))
			{
				foreach (Thing medicalThing in item.GetMedicalThings(includeBodyParts: false, remove: true))
				{
					List<Thing> list = new List<Thing>();
					BuildingUtil.DropThing(medicalThing, medicalThing.stackCount, item, ((Thing)item).Map, list);
					foreach (Thing item2 in list)
					{
						__result.Add(new ThingCount(item2, item2.stackCount, false));
						item2.Map.reservationManager.CanReserveStack(healer, LocalTargetInfo.op_Implicit(item2), 1, ReservationLayerDefOf.Floor, true);
					}
				}
			}
		}
	}
}
namespace System.Runtime.CompilerServices
{
	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
	internal sealed class IgnoresAccessChecksToAttribute : Attribute
	{
		internal IgnoresAccessChecksToAttribute(string assemblyName)
		{
		}
	}
}
