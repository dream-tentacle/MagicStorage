using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using ChangeDresser.UI;
using ChangeDresser.UI.DTO.SelectionWidgetDTOs;
using ChangeDresser.UI.Util;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyTitle("ChangeDresser")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Travis Offtermatt")]
[assembly: AssemblyProduct("ChangeDresser")]
[assembly: AssemblyCopyright("Copyright ©  2017")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("0fe64e9a-217a-4cee-8c5e-ce6cacb933ec")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.0.0.0")]
[module: UnverifiableCode]
[module: RefSafetyRules(11)]
namespace ChangeDresser
{
	internal class BuildingUtil
	{
		public static List<Thing> FindThingsNextTo(Map map, IntVec3 position, int distance)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			int num = Math.Max(0, position.x - distance);
			int num2 = Math.Min(map.info.Size.x, position.x + distance);
			int num3 = Math.Max(0, position.z - distance);
			int num4 = Math.Min(map.info.Size.z, position.z + distance);
			List<Thing> list = new List<Thing>();
			for (int i = num - 1; i <= num2; i++)
			{
				for (int j = num3 - 1; j <= num4; j++)
				{
					foreach (Thing item in map.thingGrid.ThingsAt(new IntVec3(i, position.y, j)))
					{
						list.Add(item);
					}
				}
			}
			return list;
		}

		public static List<T> FindThingsOfTypeNextTo<T>(Map map, IntVec3 position, int distance) where T : Thing
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			int num = Math.Max(0, position.x - distance);
			int num2 = Math.Min(map.info.Size.x, position.x + distance);
			int num3 = Math.Max(0, position.z - distance);
			int num4 = Math.Min(map.info.Size.z, position.z + distance);
			List<T> list = new List<T>();
			for (int i = num - 1; i <= num2; i++)
			{
				for (int j = num3 - 1; j <= num4; j++)
				{
					foreach (Thing item in map.thingGrid.ThingsAt(new IntVec3(i, position.y, j)))
					{
						if (((object)item).GetType() == typeof(T))
						{
							list.Add((T)(object)item);
						}
					}
				}
			}
			return list;
		}

		public static bool DropThing(Thing toDrop, Building from, Map map, bool makeForbidden = true)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				return DropThing(toDrop, ((Thing)from).InteractionCell, map, makeForbidden);
			}
			catch (Exception ex)
			{
				Log.Warning("ChangeDresser:BuildingUtil.DropApparel\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
			return false;
		}

		public static bool DropThing(Thing toDrop, IntVec3 from, Map map, bool makeForbidden = true)
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (!toDrop.Spawned)
				{
					Thing val = default(Thing);
					GenThing.TryDropAndSetForbidden(toDrop, from, map, (ThingPlaceMode)1, ref val, makeForbidden);
					if (!toDrop.Spawned)
					{
						GenPlace.TryPlaceThing(toDrop, from, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					}
				}
				toDrop.Position = from;
				return toDrop.Spawned;
			}
			catch (Exception ex)
			{
				Log.Warning("ChangeDresser:BuildingUtil.DropApparel\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
			return false;
		}
	}
	public class Building_Dresser : Building_Storage
	{
		public const long THIRTY_SECONDS = 300000000L;

		public readonly JobDef storeApparelJobDef = DefDatabase<JobDef>.GetNamed("StoreApparel", true);

		public const StoragePriority DefaultStoragePriority = (StoragePriority)1;

		private bool includeInTradeDeals = true;

		private List<Thing> forceAddedApparel;

		public bool UseDresserToDressFrom = true;

		private StoragePriority storagePriority = (StoragePriority)1;

		public string Name = "";

		private List<Apparel> tempApparelList;

		public static JobDef WEAR_APPAREL_FROM_DRESSER_JOB_DEF { get; private set; }

		public bool AllowAdds { get; set; }

		private Map CurrentMap { get; set; }

		public bool IncludeInTradeDeals => includeInTradeDeals;

		public override string Label
		{
			get
			{
				if (!(Name == ""))
				{
					return Name;
				}
				return ((Thing)this).Label;
			}
		}

		public List<Apparel> StoredApparel => base.slotGroup.HeldThings.OfType<Apparel>().ToList();

		public List<Apparel> Apparel
		{
			get
			{
				List<Thing> source = new List<Thing>();
				((Thing)this).Map.listerThings.GetAllThings(ref source, (ThingRequestGroup)18, (Predicate<Thing>)((Thing thing) => StoreUtility.IsInAnyStorage(thing)), true);
				return source.OfType<Apparel>().ToList();
			}
		}

		public int Count => Apparel.Count;

		public Building_Dresser()
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			AllowAdds = true;
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Expected O, but got Unknown
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			((Building_Storage)this).SpawnSetup(map, respawningAfterLoad);
			CurrentMap = map;
			WorldComp.AddDresser(this);
			if (base.settings == null)
			{
				base.settings = new StorageSettings((IStoreSettingsParent)(object)this);
				base.settings.CopyFrom(((Thing)this).def.building.defaultStorageSettings);
				base.settings.filter.SetDisallowAll((IEnumerable<ThingDef>)null, (IEnumerable<SpecialThingFilterDef>)null);
			}
			foreach (Building_RepairChangeDresser item in BuildingUtil.FindThingsOfTypeNextTo<Building_RepairChangeDresser>(((Thing)this).Map, ((Thing)this).Position, 6))
			{
				item.AddDresser(this);
			}
			storagePriority = base.settings.Priority;
		}

		public override void Destroy(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Dispose();
				((Building_Storage)this).Destroy(mode);
			}
			catch (Exception ex)
			{
				Log.Error("ChangeDresser:Building_Dresser.Destroy\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Dispose();
				((Building_Storage)this).DeSpawn(mode);
			}
			catch (Exception ex)
			{
				Log.Error("ChangeDresser:Building_Dresser.DeSpawn\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
		}

		private void Dispose()
		{
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				this.Empty<Apparel>((List<Apparel>)null);
			}
			catch (Exception ex)
			{
				Log.Error("ChangeDresser:Building_Dresser.Dispose\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
			WorldComp.RemoveDesser(this);
			foreach (Building_RepairChangeDresser item in BuildingUtil.FindThingsOfTypeNextTo<Building_RepairChangeDresser>(CurrentMap, ((Thing)this).Position, 6))
			{
				item.RemoveDresser(this);
			}
		}

		private bool DropThing(Thing t, bool makeForbidden = true)
		{
			return BuildingUtil.DropThing(t, (Building)(object)this, CurrentMap, makeForbidden);
		}

		private void DropApparel<T>(IEnumerable<T> things, bool makeForbidden = true) where T : Thing
		{
			try
			{
				if (things == null)
				{
					return;
				}
				foreach (T thing in things)
				{
					DropThing((Thing)(object)thing, makeForbidden);
				}
			}
			catch (Exception ex)
			{
				Log.Error("ChangeDresser:Building_Dresser.DropApparel\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
		}

		public void Empty<T>(List<T> removed = null) where T : Thing
		{
			try
			{
				AllowAdds = false;
			}
			finally
			{
				AllowAdds = true;
			}
		}

		public void EmptyApparel()
		{
			foreach (Apparel item in StoredApparel)
			{
				BuildingUtil.DropThing((Thing)(object)item, (Building)(object)this, ((Thing)this).Map, makeForbidden: false);
			}
		}

		internal void ReclaimApparel(bool force = false)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Expected O, but got Unknown
			if (((Thing)this).Map == null)
			{
				return;
			}
			try
			{
				List<Thing> list = BuildingUtil.FindThingsNextTo(((Thing)this).Map, ((Thing)this).Position, 1);
				if (list.Count <= 0)
				{
					return;
				}
				foreach (Thing item in list)
				{
					try
					{
						if (item is Apparel && !WorldComp.StoreApparel((Apparel)item, ((Thing)this).Map) && force && item.Spawned)
						{
							Messages.Message("Unable to store apparel: " + ((Entity)item).Label + ". No available storage space.", MessageTypeDefOf.CautionInput, false);
						}
					}
					catch
					{
					}
				}
				list.Clear();
				list = null;
			}
			catch
			{
			}
		}

		public override void ExposeData()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Invalid comparison between Unknown and I4
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Invalid comparison between Unknown and I4
			//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0104: Invalid comparison between Unknown and I4
			//IL_0106: Unknown result type (might be due to invalid IL or missing references)
			//IL_010c: Invalid comparison between Unknown and I4
			((Building_Storage)this).ExposeData();
			if ((int)Scribe.mode == 1 && forceAddedApparel == null)
			{
				forceAddedApparel = new List<Thing>();
			}
			Scribe_Collections.Look<Apparel>(ref tempApparelList, "apparel", false, (LookMode)2, new object[0]);
			Scribe_Values.Look<bool>(ref includeInTradeDeals, "includeInTradeDeals", true, false);
			Scribe_Collections.Look<Thing>(ref forceAddedApparel, "forceAddedApparel", false, (LookMode)2, new object[0]);
			Scribe_Values.Look<bool>(ref UseDresserToDressFrom, "useDresserToDressFrom", true, false);
			Scribe_Values.Look<string>(ref Name, "name", "", false);
			if (tempApparelList != null && (int)Scribe.mode == 4)
			{
				foreach (Apparel tempApparel in tempApparelList)
				{
					if (tempApparel != null && !((Thing)tempApparel).Destroyed && (double)((Thing)tempApparel).HitPoints > 0.01)
					{
						BuildingUtil.DropThing((Thing)(object)tempApparel, (Building)(object)this, ((Thing)this).Map, makeForbidden: false);
					}
				}
			}
			if (tempApparelList != null && ((int)Scribe.mode == 1 || (int)Scribe.mode == 4))
			{
				tempApparelList.Clear();
				tempApparelList = null;
				if (forceAddedApparel != null && forceAddedApparel.Count == 0)
				{
					forceAddedApparel = null;
				}
			}
		}

		public override string GetInspectString()
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder(((Building_Storage)this).GetInspectString());
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(Environment.NewLine);
			}
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.StoragePriority")));
			stringBuilder.Append(": ");
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("StoragePriority" + ((object)base.settings.Priority/*cast due to constrained. prefix*/).ToString())));
			stringBuilder.Append(Environment.NewLine);
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ApparelCount")));
			stringBuilder.Append(": ");
			stringBuilder.Append(Count);
			stringBuilder.Append(Environment.NewLine);
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.IncludeInTradeDeals")));
			stringBuilder.Append(": ");
			stringBuilder.Append(includeInTradeDeals.ToString());
			return stringBuilder.ToString();
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		public void Remove(Apparel a, bool forbidden = true)
		{
		}

		public override void TickLong()
		{
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			if (((Thing)this).Spawned)
			{
				_ = ((Thing)this).Map;
			}
			if (forceAddedApparel != null && forceAddedApparel.Count > 0)
			{
				foreach (Thing item in forceAddedApparel)
				{
					try
					{
						DropThing(item, makeForbidden: false);
					}
					catch
					{
					}
				}
				forceAddedApparel.Clear();
				forceAddedApparel = null;
			}
			if (storagePriority != base.settings.Priority)
			{
				storagePriority = base.settings.Priority;
				WorldComp.SortDressersToUse();
			}
		}

		public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
		{
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Expected O, but got Unknown
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			Pawn_ApparelTracker apparel = pawn.apparel;
			if (apparel != null && apparel.LockedApparel?.Count == 0)
			{
				list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.StoreApparel")), (Action)delegate
				{
					//IL_0011: Unknown result type (might be due to invalid IL or missing references)
					//IL_0016: Unknown result type (might be due to invalid IL or missing references)
					//IL_001c: Expected O, but got Unknown
					Job val = new Job(storeApparelJobDef, LocalTargetInfo.op_Implicit((Thing)(object)this));
					pawn.jobs.TryTakeOrderedJob(val, (JobTag?)(JobTag)0, false);
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
			return list;
		}

		public IEnumerable<Gizmo> getBaseGizmo()
		{
			foreach (Gizmo item in <>n__0())
			{
				if (!(item is Command_SelectStorage))
				{
					yield return item;
				}
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Expected O, but got Unknown
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Expected O, but got Unknown
			//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0109: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Expected O, but got Unknown
			//IL_0120: Unknown result type (might be due to invalid IL or missing references)
			//IL_0135: Unknown result type (might be due to invalid IL or missing references)
			//IL_0178: Unknown result type (might be due to invalid IL or missing references)
			//IL_017e: Expected O, but got Unknown
			//IL_018f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ed: Expected O, but got Unknown
			//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0213: Unknown result type (might be due to invalid IL or missing references)
			IEnumerable<Gizmo> baseGizmo = getBaseGizmo();
			List<Gizmo> list = ((baseGizmo == null) ? new List<Gizmo>(1) : new List<Gizmo>(baseGizmo));
			int hashCode = ((object)this).GetType().Name.GetHashCode();
			Command_Action val = new Command_Action();
			((Command)val).icon = (Texture)(object)WidgetUtil.manageapparelTexture;
			((Command)val).defaultDesc = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ManageApparelDesc"));
			((Command)val).defaultLabel = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ManageApparel"));
			((Command)val).activateSound = SoundDef.Named("Click");
			val.action = delegate
			{
				Find.WindowStack.Add((Window)(object)new StorageUI(this, null));
			};
			((Command)val).groupKey = hashCode;
			hashCode++;
			list.Add((Gizmo)(object)val);
			val = new Command_Action();
			((Command)val).icon = (Texture)(object)WidgetUtil.assignweaponsTexture;
			((Command)val).defaultDesc = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.AssignOutfitsDesc"));
			((Command)val).defaultLabel = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.AssignOutfits"));
			((Command)val).activateSound = SoundDef.Named("Click");
			val.action = delegate
			{
				Find.WindowStack.Add((Window)(object)new AssignOutfitUI(this));
			};
			((Command)val).groupKey = hashCode;
			hashCode++;
			list.Add((Gizmo)(object)val);
			val = new Command_Action();
			((Command)val).icon = (Texture)(object)WidgetUtil.customapparelTexture;
			((Command)val).defaultDesc = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.CustomOutfitsDesc"));
			((Command)val).defaultLabel = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.CustomOutfits"));
			((Command)val).activateSound = SoundDef.Named("Click");
			val.action = delegate
			{
				Find.WindowStack.Add((Window)(object)new CustomOutfitUI(this));
			};
			((Command)val).groupKey = hashCode;
			hashCode++;
			list.Add((Gizmo)(object)val);
			val = new Command_Action();
			((Command)val).icon = (Texture)(object)WidgetUtil.emptyTexture;
			((Command)val).defaultDesc = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.EmptyDesc"));
			((Command)val).defaultLabel = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.Empty"));
			((Command)val).activateSound = SoundDef.Named("Click");
			val.action = delegate
			{
				EmptyApparel();
			};
			((Command)val).groupKey = hashCode;
			hashCode++;
			list.Add((Gizmo)(object)val);
			val = new Command_Action();
			((Command)val).icon = (Texture)(object)WidgetUtil.collectTexture;
			((Command)val).defaultDesc = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.CollectDesc"));
			((Command)val).defaultLabel = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.Collect"));
			((Command)val).activateSound = SoundDef.Named("Click");
			val.action = delegate
			{
				ReclaimApparel();
			};
			((Command)val).groupKey = hashCode;
			hashCode++;
			list.Add((Gizmo)(object)val);
			return list;
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building_Storage)this).GetGizmos();
		}
	}
	internal class Building_RepairChangeDresser : Building
	{
		private const int LOW_POWER_COST = 10;

		private static LinkedList<Apparel> AllApparelBeingRepaired = new LinkedList<Apparel>();

		private LinkedList<Building_Dresser> AttachedDressers = new LinkedList<Building_Dresser>();

		public CompPowerTrader compPowerTrader;

		private Apparel BeingRepaird;

		private Map CurrentMap;

		private const long THIRTY_SECONDS = 300000000L;

		private long lastTick = DateTime.Now.Ticks;

		private long lastSearch = DateTime.Now.Ticks;

		public override string GetInspectString()
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder(((ThingWithComps)this).GetInspectString());
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(Environment.NewLine);
			}
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.AttachedDressers")));
			stringBuilder.Append(": ");
			stringBuilder.Append(AttachedDressers.Count);
			stringBuilder.Append(Environment.NewLine);
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.IsMending")));
			stringBuilder.Append(": ");
			if (BeingRepaird == null)
			{
				stringBuilder.Append(bool.FalseString);
			}
			else
			{
				stringBuilder.Append(((Entity)BeingRepaird).Label);
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("    ");
				stringBuilder.Append(((Thing)BeingRepaird).HitPoints.ToString());
				stringBuilder.Append("/");
				stringBuilder.Append(((Thing)BeingRepaird).MaxHitPoints);
			}
			return stringBuilder.ToString();
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			compPowerTrader.PowerOutput = -10f;
			CurrentMap = map;
			foreach (Building_Dresser item in BuildingUtil.FindThingsOfTypeNextTo<Building_Dresser>(((Thing)this).Map, ((Thing)this).Position, 6))
			{
				AddDresser(item);
			}
			compPowerTrader.powerStartedAction = delegate
			{
				compPowerTrader.PowerOutput = 10f;
			};
			compPowerTrader.powerStoppedAction = delegate
			{
				StopRepairing();
				compPowerTrader.PowerOutput = 0f;
			};
		}

		public override void Destroy(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).Destroy(mode);
			StopRepairing();
			AttachedDressers.Clear();
		}

		public override void Discard(bool silentlyRemoveReferences = false)
		{
			((Thing)this).Discard(silentlyRemoveReferences);
			StopRepairing();
			AttachedDressers.Clear();
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).DeSpawn(mode);
			StopRepairing();
			AttachedDressers.Clear();
		}

		protected override void Tick()
		{
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).TickRare();
			long ticks = DateTime.Now.Ticks;
			if (ticks - lastTick <= Settings.MendingAttachmentUpdateIntervalTicks)
			{
				return;
			}
			lastTick = DateTime.Now.Ticks;
			if (!compPowerTrader.PowerOn)
			{
				if (BeingRepaird != null)
				{
					StopRepairing();
				}
			}
			else if (BeingRepaird == null)
			{
				if (ticks - lastSearch > 300000000)
				{
					lastSearch = ticks;
					StartRepairing();
				}
			}
			else if (BeingRepaird != null && ((Thing)BeingRepaird).HitPoints >= ((Thing)BeingRepaird).MaxHitPoints)
			{
				((Thing)BeingRepaird).HitPoints = ((Thing)BeingRepaird).MaxHitPoints;
				StopRepairing();
				lastSearch = ticks;
				StartRepairing();
			}
			if (BeingRepaird != null)
			{
				Apparel beingRepaird = BeingRepaird;
				((Thing)beingRepaird).HitPoints = ((Thing)beingRepaird).HitPoints + Settings.MendingAttachmentMendingSpeed;
				if (((Thing)BeingRepaird).HitPoints > ((Thing)BeingRepaird).MaxHitPoints)
				{
					((Thing)BeingRepaird).HitPoints = ((Thing)BeingRepaird).MaxHitPoints;
				}
				float num = GenTemperature.ControlTemperatureTempChange(((Thing)this).Position, ((Thing)this).Map, 10f, float.MaxValue);
				GenTemperature.PushHeat((Thing)(object)this, num);
				compPowerTrader.PowerOutput = 0f - ((CompPower)compPowerTrader).Props.PowerConsumption;
			}
			else
			{
				compPowerTrader.PowerOutput = 10f;
			}
		}

		private void OrderAttachedDressers()
		{
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			bool flag = true;
			LinkedListNode<Building_Dresser> linkedListNode = AttachedDressers.First;
			while (linkedListNode != null)
			{
				LinkedListNode<Building_Dresser>? next = linkedListNode.Next;
				if (!((Thing)linkedListNode.Value).Spawned)
				{
					AttachedDressers.Remove(linkedListNode);
				}
				else if (linkedListNode.Next != null && ((Building_Storage)linkedListNode.Value).settings.Priority < ((Building_Storage)linkedListNode.Next.Value).settings.Priority)
				{
					flag = false;
				}
				linkedListNode = next;
			}
			if (flag)
			{
				return;
			}
			LinkedList<Building_Dresser> linkedList = new LinkedList<Building_Dresser>();
			for (linkedListNode = AttachedDressers.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
			{
				Building_Dresser value = linkedListNode.Value;
				bool flag2 = false;
				for (LinkedListNode<Building_Dresser> linkedListNode2 = linkedList.First; linkedListNode2 != null; linkedListNode2 = linkedListNode2.Next)
				{
					if (((Building_Storage)value).settings.Priority > ((Building_Storage)linkedListNode2.Value).settings.Priority)
					{
						linkedList.AddBefore(linkedListNode2, value);
						flag2 = true;
						break;
					}
				}
				if (!flag2)
				{
					linkedList.AddLast(value);
				}
			}
			AttachedDressers.Clear();
			AttachedDressers = linkedList;
		}

		private void StartRepairing()
		{
			OrderAttachedDressers();
			foreach (PawnOutfitTracker value in WorldComp.PawnOutfits.Values)
			{
				foreach (Apparel item in value.CustomApparel)
				{
					if (((Thing)item).HitPoints < ((Thing)item).MaxHitPoints && !AllApparelBeingRepaired.Contains(item))
					{
						BeingRepaird = item;
						AllApparelBeingRepaired.AddLast(item);
						return;
					}
				}
			}
			for (LinkedListNode<Building_Dresser> linkedListNode = AttachedDressers.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
			{
				foreach (Apparel item2 in linkedListNode.Value.StoredApparel)
				{
					if (((Thing)item2).HitPoints < ((Thing)item2).MaxHitPoints && !AllApparelBeingRepaired.Contains(item2))
					{
						BeingRepaird = item2;
						AllApparelBeingRepaired.AddLast(item2);
						return;
					}
				}
			}
		}

		private void StopRepairing()
		{
			if (BeingRepaird != null)
			{
				AllApparelBeingRepaired.Remove(BeingRepaird);
				BeingRepaird = null;
			}
		}

		public void AddDresser(Building_Dresser dresser)
		{
			if (!AttachedDressers.Contains(dresser))
			{
				AttachedDressers.AddLast(dresser);
			}
		}

		public void RemoveDresser(Building_Dresser dresser)
		{
			AttachedDressers.Remove(dresser);
		}
	}
	public class WorldComp : WorldComponent
	{
		private static HashSet<Apparel> _cachedCustomApparel;

		public static ApparelMapTracker ApparelMapTracker;

		private static int nextDresserOutfitId;

		private List<PawnOutfitTracker> tempPawnOutfits;

		public static HashSet<Apparel> CachedCustomApparel
		{
			get
			{
				if (_cachedCustomApparel == null)
				{
					_cachedCustomApparel = new HashSet<Apparel>();
					foreach (PawnOutfitTracker value in PawnOutfits.Values)
					{
						foreach (CustomOutfit customOutfit in value.CustomOutfits)
						{
							foreach (Apparel item in customOutfit.Apparel)
							{
								_cachedCustomApparel.Add(item);
							}
						}
					}
				}
				return _cachedCustomApparel;
			}
		}

		public static LinkedList<Building_Dresser> DressersToUse { get; private set; }

		public static Dictionary<Pawn, PawnOutfitTracker> PawnOutfits { get; private set; }

		public static Dictionary<Pawn, PawnOutfitTracker> PlayFunctionPawnOutfits
		{
			get
			{
				return PawnOutfits.Where((KeyValuePair<Pawn, PawnOutfitTracker> p) => ((Thing)p.Key).Faction == Faction.OfPlayer && !p.Key.Dead && !((Thing)p.Key).Destroyed && !KidnapUtility.IsKidnapped(p.Key)).ToDictionary((KeyValuePair<Pawn, PawnOutfitTracker> p) => p.Key, (KeyValuePair<Pawn, PawnOutfitTracker> p) => p.Value);
			}
			private set
			{
				PlayFunctionPawnOutfits = value;
			}
		}

		public static Dictionary<Pawn, PawnOutfitTracker> DisfunctionPawnOutfits
		{
			get
			{
				return PawnOutfits.Where((KeyValuePair<Pawn, PawnOutfitTracker> p) => ((Thing)p.Key).Faction != Faction.OfPlayer || p.Key.Dead || ((Thing)p.Key).Destroyed || KidnapUtility.IsKidnapped(p.Key)).ToDictionary((KeyValuePair<Pawn, PawnOutfitTracker> p) => p.Key, (KeyValuePair<Pawn, PawnOutfitTracker> p) => p.Value);
			}
			private set
			{
				PlayFunctionPawnOutfits = value;
			}
		}

		public static PawnTableDef AssginOutfit
		{
			get
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Expected O, but got Unknown
				//IL_003b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0041: Expected O, but got Unknown
				//IL_0052: Unknown result type (might be due to invalid IL or missing references)
				PawnTableDef val = new PawnTableDef();
				val.minWidth = (int)AssignOutfitUI.WindowsWidth - 36;
				val.workerClass = typeof(PawnTable_PlayerPawns);
				((Def)val).defName = "AssignOutfit";
				val.columns = new List<PawnColumnDef>();
				PawnColumnDef val2 = new PawnColumnDef();
				((Def)val2).defName = "LabelShortWithIcon";
				((Def)val2).label = TaggedString.op_Implicit(Translator.Translate("Name"));
				val2.workerClass = typeof(PawnColumnWorker_LabelWithCustomHead);
				val2.sortable = true;
				val2.useLabelShort = true;
				val2.showIcon = true;
				val.columns.Add(val2);
				bool flag = false;
				foreach (ApparelPolicy allOutfit in Current.Game.outfitDatabase.AllOutfits)
				{
					flag = !flag;
					PawnColumnDef val3 = (PawnColumnDef)(object)new PawnColumnDef_AssignOutfit();
					((Def)val3).defName = "Outfit_" + ((Policy)allOutfit).label;
					val3.workerClass = typeof(PawnColumnWorker_AssignOutfit);
					val3.sortable = true;
					val3.paintable = true;
					val3.moveWorkTypeLabelDown = flag;
					((PawnColumnDef_AssignOutfit)(object)val3).apparelPolicy = allOutfit;
					val.columns.Add(val3);
				}
				return val;
			}
		}

		public static List<ApparelPolicy> OutfitsForBattle { get; private set; }

		public static int NextDresserOutfitId
		{
			get
			{
				int result = nextDresserOutfitId;
				nextDresserOutfitId++;
				return result;
			}
		}

		public static void InvalidateCachedCustomApparel()
		{
			_cachedCustomApparel = null;
		}

		public static OutfitType GetOutfitType(ApparelPolicy outfit)
		{
			if (!OutfitsForBattle.Contains(outfit))
			{
				return OutfitType.Civilian;
			}
			return OutfitType.Battle;
		}

		static WorldComp()
		{
			_cachedCustomApparel = null;
			ApparelMapTracker = new ApparelMapTracker();
			nextDresserOutfitId = 0;
			DressersToUse = new LinkedList<Building_Dresser>();
		}

		public WorldComp(World world)
			: base(world)
		{
			if (DressersToUse != null)
			{
				DressersToUse.Clear();
			}
			else
			{
				DressersToUse = new LinkedList<Building_Dresser>();
			}
			if (PawnOutfits != null)
			{
				PawnOutfits.Clear();
			}
			else
			{
				PawnOutfits = new Dictionary<Pawn, PawnOutfitTracker>();
			}
			if (OutfitsForBattle != null)
			{
				OutfitsForBattle.Clear();
			}
			else
			{
				OutfitsForBattle = new List<ApparelPolicy>();
			}
		}

		public static bool TrySpawn(Thing toSpawn, IntVec3 dest, Map map, bool makeForbidden = false)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (toSpawn.Spawned && toSpawn.Map != map)
				{
					((Entity)toSpawn).DeSpawn((DestroyMode)0);
				}
				if (!toSpawn.Spawned)
				{
					Thing val = default(Thing);
					GenThing.TryDropAndSetForbidden(toSpawn, dest, map, (ThingPlaceMode)0, ref val, makeForbidden);
				}
				if (!toSpawn.Spawned)
				{
					GenPlace.TryPlaceThing(toSpawn, dest, map, (ThingPlaceMode)0, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
				}
				if (!toSpawn.Spawned)
				{
					GenPlace.TryPlaceThing(toSpawn, dest, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
				}
				toSpawn.Position = dest;
				return toSpawn.Spawned;
			}
			catch (Exception ex)
			{
				Log.Warning("ChangeDresser:BuildingUtil.DropApparel\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
			return false;
		}

		public static bool TryFindBestStorageFor(Thing t, Map map, StoragePriority currentPriority, Faction faction, out IntVec3 foundCell, out IHaulDestination haulDestination)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0189: Unknown result type (might be due to invalid IL or missing references)
			//IL_018e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0190: Unknown result type (might be due to invalid IL or missing references)
			//IL_0192: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0229: Unknown result type (might be due to invalid IL or missing references)
			//IL_022e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0233: Unknown result type (might be due to invalid IL or missing references)
			//IL_0235: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0104: Unknown result type (might be due to invalid IL or missing references)
			//IL_0109: Unknown result type (might be due to invalid IL or missing references)
			//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0215: Unknown result type (might be due to invalid IL or missing references)
			//IL_0203: Unknown result type (might be due to invalid IL or missing references)
			foundCell = IntVec3.Invalid;
			haulDestination = null;
			StoragePriority val = currentPriority;
			foreach (SlotGroup item in map.haulDestinationManager.AllGroupsListInPriorityOrder)
			{
				ISlotGroupParent parent = item.parent;
				Thing val2 = (Thing)(object)((parent is Thing) ? parent : null);
				if ((val2 != null && val2.Faction != faction) || !((IHaulDestination)item.parent).HaulDestinationEnabled || item.Settings.Priority <= val || !item.Settings.AllowedToAccept(t))
				{
					continue;
				}
				foreach (IntVec3 cells in item.CellsList)
				{
					if (StoreUtility.IsGoodStoreCell(cells, map, t, (Pawn)null, faction))
					{
						foundCell = cells;
						haulDestination = (IHaulDestination)(object)item.parent;
						val = item.Settings.Priority;
						return true;
					}
					ISlotGroupParent parent2 = item.parent;
					Building_Storage val3 = (Building_Storage)(object)((parent2 is Building_Storage) ? parent2 : null);
					if (val3 != null && val3.SpaceRemainingFor(t.def) < -1)
					{
						foundCell = cells;
						haulDestination = (IHaulDestination)(object)item.parent;
						val = item.Settings.Priority;
						return true;
					}
				}
			}
			Pawn val6 = default(Pawn);
			foreach (IHaulDestination item2 in map.haulDestinationManager.AllHaulDestinationsListInPriorityOrder)
			{
				if (item2 is ISlotGroupParent || !item2.HaulDestinationEnabled)
				{
					continue;
				}
				StoragePriority priority = ((IStoreSettingsParent)item2).GetStoreSettings().Priority;
				if (priority <= val || !item2.Accepts(t))
				{
					continue;
				}
				Thing val4 = (Thing)(object)((item2 is Thing) ? item2 : null);
				if (val4 != null)
				{
					if (val4.Faction != null && val4.Faction != faction)
					{
						continue;
					}
					IHaulEnroute val5 = (IHaulEnroute)(object)((val4 is IHaulEnroute) ? val4 : null);
					if (val5 != null && EnrouteUtility.GetSpaceRemainingWithEnroute(val5, t.def, (Pawn)null) <= 0)
					{
						continue;
					}
					if (map.reservationManager.TryGetReserver(LocalTargetInfo.op_Implicit(val4), faction, ref val6))
					{
						val6.jobs.ReleaseReservations(LocalTargetInfo.op_Implicit(val4));
					}
					if (map.reservationManager.IsReservedByAnyoneOf(LocalTargetInfo.op_Implicit(val4), faction))
					{
						continue;
					}
				}
				haulDestination = item2;
				foundCell = IntVec3.Invalid;
				val = priority;
				return true;
			}
			return false;
		}

		public static bool StoreApparel(Apparel apparel, Map map = null)
		{
			if (apparel == null)
			{
				return true;
			}
			if (map == null)
			{
				object obj = (((Thing)apparel).Spawned ? ((Thing)apparel).Map : null);
				if (obj == null)
				{
					Building_Dresser? building_Dresser = DressersToUse.FirstOrDefault();
					obj = ((building_Dresser != null) ? ((Thing)building_Dresser).Map : null);
				}
				map = (Map)obj;
			}
			if (TryStoreApparelOnMap(apparel, map))
			{
				return true;
			}
			HashSet<Map> hashSet = new HashSet<Map> { map };
			foreach (Building_Dresser item in DressersToUse)
			{
				Map map2 = ((Thing)item).Map;
				if (map2 != null && hashSet.Add(map2) && TryStoreApparelOnMap(apparel, map2))
				{
					return true;
				}
			}
			Messages.Message("Unable to store apparel: " + ((Entity)apparel).Label + ". No available storage space, will be dropped on the ground.", MessageTypeDefOf.CautionInput, false);
			return false;
		}

		private static bool TryStoreApparelOnMap(Apparel apparel, Map map)
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			if (map == null)
			{
				return false;
			}
			if (TryFindBestStorageFor((Thing)(object)apparel, map, (StoragePriority)0, Faction.OfPlayer, out var foundCell, out var haulDestination))
			{
				if (!(haulDestination is ISlotGroupParent))
				{
					Thing val = (Thing)(object)((haulDestination is Thing) ? haulDestination : null);
					if (val != null)
					{
						TrySpawn((Thing)(object)apparel, val.Position, map);
						return true;
					}
					TrySpawn((Thing)(object)apparel, foundCell, map);
					return true;
				}
				TrySpawn((Thing)(object)apparel, foundCell, map);
				return true;
			}
			return false;
		}

		public static void AddDresser(Building_Dresser dresser)
		{
			if (dresser == null || ((Thing)dresser).Map == null)
			{
				Log.Error("Cannot add ChangeDresser that is either null or has a null map.");
			}
			else if (!DressersToUse.Contains(dresser))
			{
				DressersToUse.AddFirst(dresser);
				SortDressersToUse();
			}
		}

		public static IEnumerable<Building_Dresser> GetDressers(Map map)
		{
			if (DressersToUse == null)
			{
				yield break;
			}
			foreach (Building_Dresser item in DressersToUse)
			{
				if (map == null || (((Thing)item).Spawned && ((Thing)item).Map == map))
				{
					yield return item;
				}
			}
		}

		public static void CleanupCustomOutfits()
		{
			foreach (PawnOutfitTracker value in PawnOutfits.Values)
			{
				value.Clean();
			}
		}

		public static bool HasDressers()
		{
			return DressersToUse.Count > 0;
		}

		public static bool HasDressers(Map map)
		{
			foreach (Building_Dresser item in DressersToUse)
			{
				if (((Thing)item).Spawned && ((Thing)item).Map == map)
				{
					return true;
				}
			}
			return false;
		}

		public static void RemoveDressers(Map map)
		{
			LinkedListNode<Building_Dresser> linkedListNode = DressersToUse.First;
			while (linkedListNode != null)
			{
				LinkedListNode<Building_Dresser>? next = linkedListNode.Next;
				if (((Thing)linkedListNode.Value).Map == null)
				{
					DressersToUse.Remove(linkedListNode);
				}
				linkedListNode = next;
			}
		}

		public static bool RemoveDesser(Building_Dresser dresser)
		{
			if (DressersToUse.Remove(dresser))
			{
				return true;
			}
			return false;
		}

		public static void SortDressersToUse()
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			LinkedList<Building_Dresser> linkedList = new LinkedList<Building_Dresser>();
			foreach (Building_Dresser item in DressersToUse)
			{
				bool flag = false;
				for (LinkedListNode<Building_Dresser> linkedListNode = linkedList.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
				{
					if (((Building_Storage)item).settings.Priority > ((Building_Storage)linkedListNode.Value).settings.Priority)
					{
						flag = true;
						linkedList.AddBefore(linkedListNode, item);
						break;
					}
				}
				if (!flag)
				{
					linkedList.AddLast(item);
				}
			}
			DressersToUse.Clear();
			DressersToUse = linkedList;
		}

		public override void ExposeData()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Invalid comparison between Unknown and I4
			//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c0: Invalid comparison between Unknown and I4
			//IL_01af: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b5: Invalid comparison between Unknown and I4
			//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bd: Invalid comparison between Unknown and I4
			if ((int)Scribe.mode == 1)
			{
				tempPawnOutfits = new List<PawnOutfitTracker>(PawnOutfits.Count);
				foreach (PawnOutfitTracker value in PawnOutfits.Values)
				{
					if (value != null)
					{
						tempPawnOutfits.Add(value);
					}
				}
			}
			Scribe_Values.Look<int>(ref nextDresserOutfitId, "nextDresserOutfitId", 0, false);
			Scribe_Collections.Look<PawnOutfitTracker>(ref tempPawnOutfits, "pawnOutfits", (LookMode)2, new object[0]);
			Scribe_Deep.Look<ApparelMapTracker>(ref ApparelMapTracker, "apparelColorTrack", Array.Empty<object>());
			List<ApparelPolicy> outfitsForBattle = OutfitsForBattle;
			Scribe_Collections.Look<ApparelPolicy>(ref outfitsForBattle, "outfitsForBattle", (LookMode)3, new object[0]);
			OutfitsForBattle = outfitsForBattle;
			if ((int)Scribe.mode == 4)
			{
				if (PawnOutfits == null)
				{
					PawnOutfits = new Dictionary<Pawn, PawnOutfitTracker>();
				}
				if (OutfitsForBattle == null)
				{
					OutfitsForBattle = new List<ApparelPolicy>();
				}
				PawnOutfits.Clear();
				if (tempPawnOutfits != null)
				{
					foreach (PawnOutfitTracker tempPawnOutfit in tempPawnOutfits)
					{
						if (tempPawnOutfit != null && tempPawnOutfit.Pawn != null && !tempPawnOutfit.Pawn.Dead)
						{
							PawnOutfits.Add(tempPawnOutfit.Pawn, tempPawnOutfit);
						}
					}
				}
				for (int num = OutfitsForBattle.Count - 1; num >= 0; num--)
				{
					if (OutfitsForBattle[num] == null)
					{
						OutfitsForBattle.RemoveAt(num);
					}
				}
				if (ApparelMapTracker == null)
				{
					ApparelMapTracker = new ApparelMapTracker();
				}
				ApparelMapTracker.Clean();
			}
			if (tempPawnOutfits != null && ((int)Scribe.mode == 1 || (int)Scribe.mode == 4))
			{
				tempPawnOutfits.Clear();
				tempPawnOutfits = null;
			}
		}
	}
	[StaticConstructorOnStartup]
	internal class HarmonyPatches
	{
		private struct StoredApparel(Building_Dresser dresser, Apparel apparel)
		{
			public readonly Building_Dresser Dresser = dresser;

			public readonly Apparel Apparel = apparel;
		}

		private struct ApparelToUse(List<StoredApparel> apparel, int count)
		{
			public readonly List<StoredApparel> Apparel = apparel;

			public readonly int Count = count;
		}

		private class NeededIngrediants
		{
			public readonly ThingFilter Filter;

			public int Count;

			public readonly Dictionary<Def, List<StoredApparel>> FoundThings;

			public NeededIngrediants(ThingFilter filter, int count)
			{
				Filter = filter;
				Count = count;
				FoundThings = new Dictionary<Def, List<StoredApparel>>();
			}

			public void Add(StoredApparel things)
			{
				if (!FoundThings.TryGetValue((Def)(object)((Thing)things.Apparel).def, out var value))
				{
					value = new List<StoredApparel>();
					FoundThings.Add((Def)(object)((Thing)things.Apparel).def, value);
				}
				value.Add(things);
			}

			public void Clear()
			{
				FoundThings.Clear();
			}

			public bool CountReached()
			{
				foreach (List<StoredApparel> value in FoundThings.Values)
				{
					if (CountReached(value))
					{
						return true;
					}
				}
				return false;
			}

			private bool CountReached(List<StoredApparel> l)
			{
				int num = Count;
				foreach (StoredApparel item in l)
				{
					num -= ((Thing)item.Apparel).stackCount;
				}
				return num <= 0;
			}

			public List<StoredApparel> GetFoundThings()
			{
				foreach (List<StoredApparel> value in FoundThings.Values)
				{
					if (CountReached(value))
					{
						return value;
					}
				}
				return null;
			}
		}

		static HarmonyPatches()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			new Harmony("com.changedresser.rimworld.mod").PatchAll(Assembly.GetExecutingAssembly());
		}

		public static Texture2D GetIcon(ThingDef td)
		{
			Texture2D val = null;
			val = (((Texture)(object)((BuildableDef)td).uiIcon != (Texture)null) ? ((BuildableDef)td).uiIcon : ((td == null || td.graphicData == null || td.graphicData.texPath == null) ? null : ContentFinder<Texture2D>.Get(td.graphicData.texPath, true)));
			if ((Texture)(object)val == (Texture)null)
			{
				val = WidgetUtil.noneTexture;
			}
			return val;
		}
	}
	[HarmonyPatch(typeof(Thing), "get_Label")]
	internal static class Apparel_Label_Patch
	{
		private static void Postfix(Thing __instance, ref string __result)
		{
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			if (!((IEnumerable<Thing>)WorldComp.CachedCustomApparel).Contains(__instance))
			{
				return;
			}
			foreach (PawnOutfitTracker value in WorldComp.PawnOutfits.Values)
			{
				foreach (CustomOutfit customOutfit in value.CustomOutfits)
				{
					if (((IEnumerable<Thing>)customOutfit.Apparel).Contains(__instance))
					{
						__result = $"[{value.Pawn.NameShortColored}] " + __result;
					}
				}
			}
		}
	}
	[HarmonyPatch(typeof(FloatMenuOptionProvider_Wear), "GetSingleOptionFor")]
	internal static class FloatMenuOptionProvider_Wear_GetSingleOptionFor_Patch
	{
		public static bool Prefix(Thing clickedThing, ref FloatMenuOption __result)
		{
			if (StoreUtility.StoringThing(clickedThing) is Building_Dresser building_Dresser && (object)clickedThing != building_Dresser)
			{
				__result = null;
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(FloatMenuOptionProvider_DressOtherPawn), "GetSingleOptionFor")]
	internal static class FloatMenuOptionProvider_DressOtherPawn_GetSingleOptionFor_Patch
	{
		public static bool Prefix(Thing clickedThing, ref FloatMenuOption __result)
		{
			if (StoreUtility.StoringThing(clickedThing) is Building_Dresser building_Dresser && (object)clickedThing != building_Dresser)
			{
				__result = null;
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(Thing), "DrawGUIOverlay")]
	internal static class Thing_DrawGUIOverlay_Patch
	{
		private static bool Prefix(Thing __instance)
		{
			if (StoreUtility.StoringThing(__instance) is Building_Dresser building_Dresser && (object)__instance != building_Dresser)
			{
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch]
	[HarmonyPriority(600)]
	public static class HideStoredThingsFromSectionLayerAndOverlayDrawer
	{
		private static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(SectionLayer_ThingsGeneral), "TakePrintFrom", (Type[])null, (Type[])null);
			yield return AccessTools.Method(typeof(OverlayDrawer), "RenderForbiddenOverlay", (Type[])null, (Type[])null);
		}

		[HarmonyPrefix]
		public static bool Prefix(Thing t)
		{
			if (StoreUtility.StoringThing(t) is Building_Dresser building_Dresser && (object)t != building_Dresser)
			{
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(ThingSelectionUtility), "MultiSelectableThingsInScreenRectDistinct")]
	public static class PreventSelectionInRect
	{
		[HarmonyPostfix]
		public static void Postfix(ref IEnumerable<Thing> __result)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Invalid comparison between Unknown and I4
			Event current = Event.current;
			if ((int)current.rawType == 1 && current.button == 0)
			{
				__result = __result.Where(HideStoredThingsFromSectionLayerAndOverlayDrawer.Prefix);
			}
		}
	}
	[HarmonyPatch(typeof(Selector), "SelectableObjectsUnderMouse")]
	public static class PreventSelectionUnderMouse
	{
		[HarmonyPostfix]
		public static IEnumerable<object> Postfix(IEnumerable<object> __result)
		{
			foreach (object item in __result)
			{
				Thing val = (Thing)((item is Thing) ? item : null);
				if (val == null || HideStoredThingsFromSectionLayerAndOverlayDrawer.Prefix(val))
				{
					yield return item;
				}
			}
		}
	}
	[HarmonyPatch(typeof(Pawn), "GetGizmos")]
	internal static class Patch_Pawn_GetGizmos
	{
		private static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
		{
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a7: Expected O, but got Unknown
			//IL_015e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0165: Expected O, but got Unknown
			//IL_01de: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01af: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
			List<Gizmo> list = new List<Gizmo>();
			if ((__instance.IsPrisoner || (Settings.ShowDresserButtonForPawns && ((Thing)__instance).Faction == Faction.OfPlayer && ((Thing)__instance).def.race.Humanlike)) && WorldComp.HasDressers())
			{
				list.Add((Gizmo)new Command_Action
				{
					icon = (Texture)(object)WidgetUtil.yesDressFromTexture,
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.UseDresser")),
					activateSound = SoundDef.Named("Click"),
					action = delegate
					{
						//IL_000c: Unknown result type (might be due to invalid IL or missing references)
						//IL_002e: Unknown result type (might be due to invalid IL or missing references)
						//IL_0038: Expected O, but got Unknown
						//IL_003f: Unknown result type (might be due to invalid IL or missing references)
						//IL_0049: Expected O, but got Unknown
						List<FloatMenuOption> list2 = new List<FloatMenuOption>(6)
						{
							new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.Wearing")), (Action)delegate
							{
								Find.WindowStack.Add((Window)(object)new StorageUI(__instance));
							}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
						};
						Find.WindowStack.Add((Window)new FloatMenu(list2));
					}
				});
				list.AddRange(__result);
			}
			if (!__instance.Drafted && ((Thing)__instance).Faction == Faction.OfPlayer && WorldComp.HasDressers())
			{
				if (WorldComp.PawnOutfits.TryGetValue(__instance, out var outfits))
				{
					if (list.Count == 0)
					{
						list.AddRange(__result);
					}
					foreach (IDresserOutfit o in outfits.CivilianOutfits)
					{
						if (o != null && o.IsValid())
						{
							Command_Action val = new Command_Action();
							ThingDef icon = o.Icon;
							if (icon != null)
							{
								((Command)val).icon = (Texture)(object)HarmonyPatches.GetIcon(icon);
							}
							else
							{
								((Command)val).icon = (Texture)(object)WidgetUtil.noneTexture;
							}
							StringBuilder stringBuilder = new StringBuilder();
							if (!o.IsBeingWorn)
							{
								stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ChangeTo")));
								((Command)val).defaultDesc = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ChangeToDesc"));
							}
							else
							{
								stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.Wearing")));
								((Command)val).defaultDesc = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.WearingDesc"));
							}
							stringBuilder.Append(" ");
							stringBuilder.Append(o.Label);
							((Command)val).defaultLabel = stringBuilder.ToString();
							((Command)val).activateSound = SoundDef.Named("Click");
							val.action = delegate
							{
								outfits.ChangeTo(o);
							};
							list.Add((Gizmo)(object)val);
						}
					}
				}
			}
			if (list.Count > 0)
			{
				__result = list;
			}
		}
	}
	[HarmonyPatch(typeof(Pawn_DraftController), "GetGizmos")]
	internal static class Patch_Pawn_DraftController_GetGizmos
	{
		private static void Postfix(Pawn_DraftController __instance, ref IEnumerable<Gizmo> __result)
		{
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Expected O, but got Unknown
			//IL_011a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0131: Unknown result type (might be due to invalid IL or missing references)
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			Pawn pawn = __instance.pawn;
			if (!pawn.Drafted || !WorldComp.HasDressers())
			{
				return;
			}
			if (!WorldComp.PawnOutfits.TryGetValue(pawn, out var outfits))
			{
				return;
			}
			List<Gizmo> list = new List<Gizmo>(__result);
			foreach (IDresserOutfit o in outfits.BattleOutfits)
			{
				if (o != null && o.IsValid())
				{
					Command_Action val = new Command_Action();
					ThingDef icon = o.Icon;
					if (icon != null)
					{
						((Command)val).icon = (Texture)(object)HarmonyPatches.GetIcon(icon);
					}
					else
					{
						((Command)val).icon = (Texture)(object)WidgetUtil.noneTexture;
					}
					StringBuilder stringBuilder = new StringBuilder();
					if (!((object)pawn.outfits.CurrentApparelPolicy).Equals((object?)o))
					{
						stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ChangeTo")));
						((Command)val).defaultDesc = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ChangeToDesc"));
					}
					else
					{
						stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.Wearing")));
						((Command)val).defaultDesc = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.WearingDesc"));
					}
					stringBuilder.Append(" ");
					stringBuilder.Append(o.Label);
					((Command)val).defaultLabel = stringBuilder.ToString();
					((Command)val).activateSound = SoundDef.Named("Click");
					val.action = delegate
					{
						outfits.ChangeTo(o);
					};
					list.Add((Gizmo)(object)val);
				}
			}
			__result = list;
		}
	}
	[HarmonyPatch(typeof(Pawn_DraftController), "set_Drafted")]
	internal static class Patch_Pawn_DraftController
	{
		private static void Postfix(Pawn_DraftController __instance)
		{
			if (!WorldComp.HasDressers())
			{
				return;
			}
			Pawn pawn = __instance.pawn;
			if (!pawn.Downed && WorldComp.PawnOutfits.TryGetValue(pawn, out var value))
			{
				if (pawn.Drafted)
				{
					value.ChangeToBattleOutfit();
				}
				else
				{
					value.ChangeToCivilianOutfit();
				}
			}
		}
	}
	[HarmonyPatch(typeof(JobGiver_OptimizeApparel), "ApparelScoreGain")]
	internal static class Patch_JobGiver_OptimizeApparel_ApparelScoreGain
	{
		private static bool Prefix(Pawn pawn, Apparel ap, List<float> wornScoresCache, ref float __result)
		{
			if (WorldComp.CachedCustomApparel.Contains(ap))
			{
				__result = -1000f;
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(Pawn), "Destroy")]
	internal static class Patch_Pawn_Destroy
	{
		private static Map map;

		[HarmonyPriority(800)]
		private static void Prefix(Pawn __instance)
		{
			map = ((Thing)__instance).Map;
		}

		[HarmonyPriority(800)]
		private static void Postfix(Pawn __instance, DestroyMode mode)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			if ((int)mode != 0 || !WorldComp.PawnOutfits.TryGetValue(__instance, out var value))
			{
				return;
			}
			WorldComp.PawnOutfits.Remove(__instance);
			foreach (Apparel item in value.CustomApparel)
			{
				if (!WorldComp.StoreApparel(item, map))
				{
					BuildingUtil.DropThing((Thing)(object)item, ((Thing)__instance).Position, map);
				}
			}
		}
	}
	[HarmonyPatch(typeof(OutfitDatabase), "TryDelete")]
	internal static class Patch_OutfitDatabase_TryDelete
	{
		private static void Postfix(AcceptanceReport __result, ApparelPolicy apparelPolicy)
		{
			if (!((AcceptanceReport)(ref __result)).Accepted)
			{
				return;
			}
			WorldComp.OutfitsForBattle.Remove(apparelPolicy);
			foreach (PawnOutfitTracker value in WorldComp.PawnOutfits.Values)
			{
				value.Remove(apparelPolicy);
			}
		}
	}
	[HarmonyPatch(typeof(SettlementAbandonUtility), "Abandon")]
	internal static class Patch_SettlementAbandonUtility_Abandon
	{
		[HarmonyPriority(800)]
		private static void Prefix(MapParent settlement)
		{
			WorldComp.RemoveDressers(settlement.Map);
		}
	}
	[HarmonyPatch(typeof(Caravan), "AddPawn")]
	internal static class Patch_Caravan_AddPawn
	{
		[HarmonyPriority(800)]
		private static void Prefix(Pawn p, bool addCarriedPawnToWorldPawnsIfAny)
		{
			try
			{
				if (p != null && p.Drafted)
				{
					p.drafter.Drafted = false;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Exception thrown from ChangeDresser Patch_Caravan_AddPawn - " + ex.GetType().Name + " " + ex.Message);
			}
		}
	}
	public class ApparelMapTracker : IExposable
	{
		private class ApparelMap : IExposable
		{
			public Apparel Apparel;

			public Map Map;

			public ApparelMap()
			{
			}

			public ApparelMap(Apparel a, Map m)
			{
				Apparel = a;
				Map = m;
			}

			public void ExposeData()
			{
				Scribe_References.Look<Map>(ref Map, "map", false);
				Scribe_References.Look<Apparel>(ref Apparel, "apparel", false);
			}
		}

		private Dictionary<Apparel, ApparelMap> mapLookup;

		private List<ApparelMap> l;

		public void Clear()
		{
			if (mapLookup != null)
			{
				mapLookup.Clear();
				mapLookup = null;
			}
		}

		public void Clean()
		{
			HashSet<Apparel> hashSet = new HashSet<Apparel>();
			if (mapLookup == null)
			{
				mapLookup = new Dictionary<Apparel, ApparelMap>();
			}
			foreach (Pawn allMapsCaravansAndTravellingTransporters_Alive_Colonist in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists)
			{
				if (((Thing)allMapsCaravansAndTravellingTransporters_Alive_Colonist).Faction != Faction.OfPlayer || !((Thing)allMapsCaravansAndTravellingTransporters_Alive_Colonist).def.race.Humanlike)
				{
					continue;
				}
				foreach (Apparel item in allMapsCaravansAndTravellingTransporters_Alive_Colonist.apparel.WornApparel)
				{
					hashSet.Add(item);
				}
				if (WorldComp.PawnOutfits.TryGetValue(allMapsCaravansAndTravellingTransporters_Alive_Colonist, out var value))
				{
					GenCollection.AddRange<Apparel>(hashSet, value.CustomApparel);
				}
			}
			List<Apparel> list = new List<Apparel>();
			foreach (Apparel key in mapLookup.Keys)
			{
				if (!hashSet.Contains(key))
				{
					list.Add(key);
				}
			}
			foreach (Apparel item2 in list)
			{
				mapLookup.Remove(item2);
			}
		}

		public void AddApparel(Apparel a)
		{
			if (mapLookup == null)
			{
				mapLookup = new Dictionary<Apparel, ApparelMap>();
			}
			if (a != null && ((Thing)a).Map != null && ((Thing)a).Map.IsPlayerHome)
			{
				mapLookup[a] = new ApparelMap(a, ((Thing)a).Map);
			}
		}

		public Map GetMap(Apparel a)
		{
			Map obj;
			if (a != null)
			{
				Dictionary<Apparel, ApparelMap> dictionary = mapLookup;
				if (dictionary != null && dictionary.TryGetValue(a, out var value) && !value.Map.Disposed && value.Map.IsPlayerHome)
				{
					obj = value.Map;
					goto IL_0049;
				}
			}
			obj = Find.AnyPlayerHomeMap;
			if (obj == null)
			{
				return Find.CurrentMap;
			}
			goto IL_0049;
			IL_0049:
			return obj;
		}

		public void RemoveApparel(Apparel a)
		{
			if (mapLookup != null && a != null)
			{
				mapLookup.Remove(a);
			}
		}

		public void ExposeData()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Invalid comparison between Unknown and I4
			//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f4: Invalid comparison between Unknown and I4
			//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d8: Invalid comparison between Unknown and I4
			//IL_01da: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e0: Invalid comparison between Unknown and I4
			if ((int)Scribe.mode == 1)
			{
				if (mapLookup != null)
				{
					l = new List<ApparelMap>(mapLookup.Count);
					foreach (ApparelMap value in mapLookup.Values)
					{
						if (value.Apparel != null && !((Thing)value.Apparel).Destroyed)
						{
							l.Add(value);
						}
					}
				}
				else
				{
					l = new List<ApparelMap>(0);
				}
			}
			try
			{
				Scribe_Collections.Look<ApparelMap>(ref l, "ApparelMaps", (LookMode)2, new object[0]);
			}
			catch (Exception ex)
			{
				Log.Warning("Unable to persist original apparel maps." + Environment.NewLine + ex.GetType().Name + " " + ex.Message);
				l.Clear();
			}
			if ((int)Scribe.mode == 4)
			{
				Clear();
				if (l != null)
				{
					if (mapLookup == null)
					{
						mapLookup = new Dictionary<Apparel, ApparelMap>();
					}
					foreach (ApparelMap item in l)
					{
						try
						{
							if (item != null && item.Apparel != null && item.Map != null && !item.Map.Disposed)
							{
								mapLookup.Add(item.Apparel, item);
							}
						}
						catch (Exception ex2)
						{
							Log.Warning("Unable to persist an original apparel's map." + Environment.NewLine + ex2.GetType().Name + " " + ex2.Message);
						}
					}
				}
			}
			if (((int)Scribe.mode == 1 || (int)Scribe.mode == 4) && l != null)
			{
				l.Clear();
				l = null;
			}
		}
	}
	public static class ApparelUtil
	{
		public static List<Apparel> RemoveApparel(Pawn pawn)
		{
			List<Apparel> list = new List<Apparel>(pawn.apparel.WornApparel);
			foreach (Apparel item in list)
			{
				pawn.apparel.Remove(item);
			}
			pawn.outfits.forcedHandler.ForcedApparel.Clear();
			return list;
		}

		public static void StoreApparelInWorld(List<Apparel> apparel, Pawn pawn)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			foreach (Apparel item in apparel)
			{
				if (!WorldComp.StoreApparel(item, ((Thing)pawn).Map))
				{
					BuildingUtil.DropThing((Thing)(object)item, ((Thing)pawn).Position, ((Thing)pawn).Map, makeForbidden: false);
				}
			}
		}

		public static float ApparelScoreGainAvoidingAutomaticallyDrop(Pawn pawn, Apparel ap, List<float> wornScoresCache)
		{
			if (WorldComp.CachedCustomApparel.Contains(ap))
			{
				return -1000f;
			}
			if ((((Thing)ap).def == ThingDefOf.Apparel_ShieldBelt && pawn.equipment.Primary != null && ((Thing)pawn.equipment.Primary).def.IsWeaponUsingProjectiles) || (((Thing)ap).def.apparel.ignoredByNonViolent && pawn.WorkTagIsDisabled((WorkTags)8)))
			{
				return -1000f;
			}
			float result = JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, ap);
			List<Apparel> wornApparel = pawn.apparel.WornApparel;
			for (int i = 0; i < wornApparel.Count; i++)
			{
				if (!ApparelUtility.CanWearTogether(((Thing)wornApparel[i]).def, ((Thing)ap).def, pawn.RaceProps.body))
				{
					return -1000f;
				}
			}
			return result;
		}

		public static void GetApparelsAfterNude(Pawn pawn)
		{
			//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d9: Expected O, but got Unknown
			//IL_022d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0240: Unknown result type (might be due to invalid IL or missing references)
			//IL_0246: Unknown result type (might be due to invalid IL or missing references)
			//IL_028e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0294: Unknown result type (might be due to invalid IL or missing references)
			if (pawn.outfits == null || ((Thing)pawn).Faction != Faction.OfPlayer || (pawn.IsMutant && pawn.mutant.Def.disableApparel) || QuestUtility.IsQuestLodger(pawn))
			{
				return;
			}
			List<Thing> list = new List<Thing>();
			List<float> list2 = new List<float>();
			ApparelPolicy currentApparelPolicy = pawn.outfits.CurrentApparelPolicy;
			foreach (Map map in Find.Maps)
			{
				map.listerThings.GetAllThings(ref list, (ThingRequestGroup)18, (Predicate<Thing>)null, true);
			}
			foreach (IHaulSource item in ((Thing)pawn).Map.haulDestinationManager.AllHaulSourcesListForReading)
			{
				foreach (Thing item2 in (IEnumerable<Thing>)((IThingHolder)item).GetDirectlyHeldThings())
				{
					Apparel val = (Apparel)(object)((item2 is Apparel) ? item2 : null);
					if (val != null)
					{
						list.Add((Thing)(object)val);
					}
				}
			}
			HashSet<Apparel> hashSet = new HashSet<Apparel>();
			foreach (PawnOutfitTracker value in WorldComp.PawnOutfits.Values)
			{
				foreach (CustomOutfit customOutfit in value.CustomOutfits)
				{
					foreach (Apparel item3 in customOutfit.Apparel)
					{
						hashSet.Add(item3);
					}
				}
			}
			if (list.Count == 0)
			{
				return;
			}
			int num = 0;
			bool flag;
			do
			{
				Apparel val2 = null;
				float num2 = 0f;
				flag = false;
				for (int i = 0; i < list.Count; i++)
				{
					Apparel val3 = (Apparel)list[i];
					if (currentApparelPolicy.filter.Allows((Thing)(object)val3) && StoreUtility.IsInAnyStorage((Thing)(object)val3) && !ForbidUtility.IsForbidden((Thing)(object)val3, Faction.OfPlayer) && !FireUtility.IsBurning((Thing)(object)val3) && !hashSet.Contains(val3) && ((int)((Thing)val3).def.apparel.gender == 0 || ((Thing)val3).def.apparel.gender == pawn.gender))
					{
						float num3 = ApparelScoreGainAvoidingAutomaticallyDrop(pawn, val3, list2);
						if ((double)num3 >= (double)num2 && (!CompBiocodable.IsBiocoded((Thing)(object)val3) || CompBiocodable.IsBiocodedFor((Thing)(object)val3, pawn)) && ApparelUtility.HasPartsToWear(pawn, ((Thing)val3).def) && DevelopmentalStageExtensions.Has(((Thing)val3).def.apparel.developmentalStageFilter, pawn.DevelopmentalStage))
						{
							val2 = val3;
							num2 = num3;
						}
					}
				}
				if (val2 != null)
				{
					pawn.apparel.Wear(val2, true, false);
					list2.Add(JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, val2));
					list.Remove((Thing)(object)val2);
					flag = true;
				}
				num++;
			}
			while (num < 30 && flag);
			list.Clear();
			list2.Clear();
		}
	}
	public enum OutfitType
	{
		Battle,
		Civilian
	}
	public interface IDresserOutfit : IExposable
	{
		OutfitType OutfitType { get; set; }

		string UniqueId { get; set; }

		string Label { get; }

		ThingDef Icon { get; }

		bool IsBeingWorn { get; }

		void Dress(Pawn pawn);

		void Undress(Pawn pawn, List<Apparel> customApparel);

		bool IsValid();
	}
	public class CustomOutfit : IDresserOutfit, IExposable
	{
		private OutfitType outfitType = OutfitType.Civilian;

		private bool isBeingWorn;

		private string uniqueId;

		public List<Apparel> Apparel = new List<Apparel>();

		public string Name = "";

		public ApparelPolicy Outfit;

		public OutfitType OutfitType
		{
			get
			{
				return outfitType;
			}
			set
			{
				outfitType = value;
			}
		}

		public string UniqueId
		{
			get
			{
				return uniqueId;
			}
			set
			{
				uniqueId = value;
			}
		}

		public string Label => Name;

		public ThingDef Icon
		{
			get
			{
				if (Apparel != null && Apparel.Count > 0 && Apparel[0] != null)
				{
					return ((Thing)Apparel[0]).def;
				}
				return null;
			}
		}

		public bool IsBeingWorn => isBeingWorn;

		public void Clean()
		{
			for (int num = Apparel.Count - 1; num >= 0; num--)
			{
				Apparel val = Apparel[num];
				if (val == null || ((Thing)val).Destroyed || ((Thing)val).HitPoints <= 0)
				{
					Apparel.RemoveAt(num);
				}
			}
		}

		public void Dress(Pawn pawn)
		{
			List<Apparel> list = ApparelUtil.RemoveApparel(pawn);
			foreach (Apparel item in Apparel)
			{
				list.Remove(item);
				pawn.apparel.Wear(item, true, false);
				pawn.outfits.forcedHandler.ForcedApparel.Add(item);
			}
			if (Outfit != null)
			{
				pawn.outfits.CurrentApparelPolicy = Outfit;
				ApparelUtil.GetApparelsAfterNude(pawn);
			}
			ApparelUtil.StoreApparelInWorld(list, pawn);
			isBeingWorn = true;
		}

		public void Undress(Pawn pawn, List<Apparel> customApparel)
		{
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			List<Apparel> list = new List<Apparel>(pawn.apparel.WornApparel);
			pawn.outfits.forcedHandler.ForcedApparel.Clear();
			foreach (Apparel item in list)
			{
				pawn.apparel.Remove(item);
				if (!customApparel.Contains(item) && !WorldComp.StoreApparel(item, ((Thing)pawn).Map))
				{
					BuildingUtil.DropThing((Thing)(object)item, ((Thing)pawn).Position, ((Thing)pawn).Map, makeForbidden: false);
				}
			}
			isBeingWorn = false;
		}

		public void ExposeData()
		{
			Scribe_Values.Look<string>(ref Name, "name", (string)null, false);
			Scribe_Values.Look<OutfitType>(ref outfitType, "type", OutfitType.Battle, false);
			Scribe_Values.Look<bool>(ref isBeingWorn, "isBeingWorn", false, false);
			Scribe_Values.Look<string>(ref uniqueId, "uniqueId", (string)null, false);
			Scribe_References.Look<ApparelPolicy>(ref Outfit, "outfit", false);
			Scribe_Collections.Look<Apparel>(ref Apparel, "apparel", false, (LookMode)3, new object[0]);
		}

		public bool IsValid()
		{
			return true;
		}

		public string GetUniqueLoadID()
		{
			return uniqueId;
		}
	}
	public class DefinedOutfit : IDresserOutfit, IExposable
	{
		private bool isBeingWorn;

		private string uniqueId;

		private OutfitType outfitType = OutfitType.Civilian;

		public ApparelPolicy Outfit;

		public OutfitType OutfitType
		{
			get
			{
				return outfitType;
			}
			set
			{
				outfitType = value;
			}
		}

		public string UniqueId
		{
			get
			{
				return uniqueId;
			}
			set
			{
				uniqueId = value;
			}
		}

		public string Label
		{
			get
			{
				if (Outfit != null)
				{
					return ((Policy)Outfit).label;
				}
				return "[Deleted Outfit]";
			}
		}

		public ThingDef Icon
		{
			get
			{
				if (Outfit != null)
				{
					List<ThingDef> list = new List<ThingDef>(Outfit.filter.AllowedThingDefs);
					if (list.Count > 0)
					{
						return list[0];
					}
				}
				return null;
			}
		}

		public bool IsBeingWorn => isBeingWorn;

		public DefinedOutfit()
		{
		}

		public DefinedOutfit(ApparelPolicy outfit, OutfitType outfitType)
		{
			Outfit = outfit;
			this.outfitType = outfitType;
		}

		public void Dress(Pawn pawn)
		{
			ApparelUtil.StoreApparelInWorld(ApparelUtil.RemoveApparel(pawn), pawn);
			pawn.outfits.CurrentApparelPolicy = Outfit;
			ApparelUtil.GetApparelsAfterNude(pawn);
			isBeingWorn = true;
		}

		public void Undress(Pawn pawn, List<Apparel> customApparel)
		{
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			List<Apparel> list = new List<Apparel>(pawn.apparel.WornApparel);
			pawn.outfits.forcedHandler.ForcedApparel.Clear();
			foreach (Apparel item in list)
			{
				pawn.apparel.Remove(item);
				if (!customApparel.Contains(item) && !WorldComp.StoreApparel(item, ((Thing)pawn).Map))
				{
					BuildingUtil.DropThing((Thing)(object)item, ((Thing)pawn).Position, ((Thing)pawn).Map, makeForbidden: false);
				}
			}
			isBeingWorn = false;
		}

		public void ExposeData()
		{
			Scribe_Values.Look<bool>(ref isBeingWorn, "isBeingWorn", false, false);
			Scribe_Values.Look<OutfitType>(ref outfitType, "type", OutfitType.Battle, false);
			Scribe_References.Look<ApparelPolicy>(ref Outfit, "outfit", false);
			Scribe_Values.Look<string>(ref uniqueId, "uniqueId", (string)null, false);
		}

		public bool IsValid()
		{
			return Outfit != null;
		}

		public string GetUniqueLoadID()
		{
			return uniqueId;
		}
	}
	public class PawnOutfitTracker : IExposable
	{
		public Pawn Pawn;

		private List<Apparel> customApparel = new List<Apparel>();

		public List<DefinedOutfit> DefinedOutfits = new List<DefinedOutfit>();

		public List<CustomOutfit> CustomOutfits = new List<CustomOutfit>();

		private string currentlyWorn;

		private string lastBattleOutfit;

		private string lastCivilianOutfit;

		private List<SlotColor> ApparelColors;

		public IEnumerable<Apparel> CustomApparel => customApparel;

		public IEnumerable<IDresserOutfit> AllOutfits
		{
			get
			{
				foreach (DefinedOutfit definedOutfit in DefinedOutfits)
				{
					yield return definedOutfit;
				}
				foreach (CustomOutfit customOutfit in CustomOutfits)
				{
					yield return customOutfit;
				}
			}
		}

		public IEnumerable<IDresserOutfit> BattleOutfits
		{
			get
			{
				foreach (DefinedOutfit definedOutfit in DefinedOutfits)
				{
					if (((IDresserOutfit)definedOutfit).OutfitType == OutfitType.Battle)
					{
						yield return definedOutfit;
					}
				}
				foreach (CustomOutfit customOutfit in CustomOutfits)
				{
					if (((IDresserOutfit)customOutfit).OutfitType == OutfitType.Battle)
					{
						yield return customOutfit;
					}
				}
			}
		}

		public IEnumerable<IDresserOutfit> CivilianOutfits
		{
			get
			{
				foreach (DefinedOutfit definedOutfit in DefinedOutfits)
				{
					if (((IDresserOutfit)definedOutfit).OutfitType == OutfitType.Civilian)
					{
						yield return definedOutfit;
					}
				}
				foreach (CustomOutfit customOutfit in CustomOutfits)
				{
					if (((IDresserOutfit)customOutfit).OutfitType == OutfitType.Civilian)
					{
						yield return customOutfit;
					}
				}
			}
		}

		public IDresserOutfit CurrentOutfit => GetOutfit(currentlyWorn);

		public PawnOutfitTracker()
		{
		}

		public PawnOutfitTracker(Pawn pawn)
		{
			Pawn = pawn;
		}

		public void AddOutfit(IDresserOutfit outfit)
		{
			ApplyUniqueId(outfit);
			if (outfit is DefinedOutfit)
			{
				bool flag = false;
				foreach (DefinedOutfit definedOutfit in DefinedOutfits)
				{
					if (definedOutfit.UniqueId.Equals(outfit.UniqueId))
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					DefinedOutfits.Add(outfit as DefinedOutfit);
				}
				return;
			}
			bool flag2 = false;
			foreach (CustomOutfit customOutfit in CustomOutfits)
			{
				if (customOutfit.UniqueId.Equals(outfit.UniqueId))
				{
					flag2 = true;
					break;
				}
			}
			if (!flag2)
			{
				CustomOutfits.Add(outfit as CustomOutfit);
			}
		}

		public void Clean()
		{
			bool flag = false;
			for (int num = customApparel.Count - 1; num >= 0; num--)
			{
				Apparel val = customApparel[num];
				if (val == null || ((Thing)val).Destroyed || ((Thing)val).HitPoints <= 0)
				{
					flag = true;
					customApparel.RemoveAt(num);
				}
			}
			if (!flag)
			{
				return;
			}
			foreach (CustomOutfit customOutfit in CustomOutfits)
			{
				customOutfit.Clean();
			}
		}

		public Color GetLayerColor(ApparelLayerDef layer, bool getFromWorn = false)
		{
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			int num = Util.ToInt(layer);
			if (ApparelColors != null && ApparelColors.Count > num)
			{
				SlotColor slotColor = ApparelColors[num];
				if (slotColor.IsAssigned)
				{
					return slotColor.Color;
				}
			}
			if (getFromWorn)
			{
				foreach (Apparel item in Pawn.apparel.WornApparel)
				{
					if (((Thing)item).def.apparel.LastLayer == layer)
					{
						return ((Thing)item).DrawColor;
					}
				}
			}
			return Color.white;
		}

		public void SetLayerColor(ApparelLayerDef layer, Color c)
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			int num = Util.ToInt(layer);
			InitApparelColor();
			if (ApparelColors != null && ApparelColors.Count > num)
			{
				SlotColor slotColor = ApparelColors[num];
				slotColor.Color = c;
				slotColor.IsAssigned = true;
				ApparelColors[num] = slotColor;
			}
		}

		private void ApplyUniqueId(IDresserOutfit outfit)
		{
			if (string.IsNullOrEmpty(outfit.UniqueId))
			{
				outfit.UniqueId = ((outfit is DefinedOutfit) ? "d" : "c") + WorldComp.NextDresserOutfitId;
			}
		}

		public void ChangeTo(IDresserOutfit outfit)
		{
			if (string.IsNullOrEmpty(outfit.UniqueId))
			{
				ApplyUniqueId(outfit);
			}
			if (currentlyWorn != null)
			{
				GetOutfit(currentlyWorn)?.Undress(Pawn, customApparel);
			}
			outfit.Dress(Pawn);
			if (outfit.OutfitType == OutfitType.Battle)
			{
				lastBattleOutfit = outfit.UniqueId;
			}
			else
			{
				lastCivilianOutfit = outfit.UniqueId;
			}
			currentlyWorn = outfit.UniqueId;
		}

		public bool ChangeToBattleOutfit()
		{
			if (lastBattleOutfit != null)
			{
				IDresserOutfit outfit = GetOutfit(lastBattleOutfit);
				if (outfit != null && outfit.OutfitType == OutfitType.Battle)
				{
					ChangeTo(outfit);
					return true;
				}
			}
			return ChangeToOutfitType(OutfitType.Battle);
		}

		public bool ChangeToCivilianOutfit()
		{
			if (lastCivilianOutfit != null)
			{
				IDresserOutfit outfit = GetOutfit(lastCivilianOutfit);
				if (outfit != null && outfit.OutfitType == OutfitType.Civilian)
				{
					ChangeTo(outfit);
					return true;
				}
			}
			return ChangeToOutfitType(OutfitType.Civilian);
		}

		private IDresserOutfit GetOutfit(string uniqueId)
		{
			if (!string.IsNullOrEmpty(uniqueId))
			{
				if (uniqueId[0] == 'c')
				{
					foreach (CustomOutfit customOutfit in CustomOutfits)
					{
						if (uniqueId.Equals(((IDresserOutfit)customOutfit).UniqueId))
						{
							return customOutfit;
						}
					}
				}
				else
				{
					foreach (DefinedOutfit definedOutfit in DefinedOutfits)
					{
						if (uniqueId.Equals(((IDresserOutfit)definedOutfit).UniqueId))
						{
							return definedOutfit;
						}
					}
				}
			}
			return null;
		}

		private bool ChangeToOutfitType(OutfitType type)
		{
			foreach (CustomOutfit customOutfit in CustomOutfits)
			{
				if (((IDresserOutfit)customOutfit).OutfitType == type)
				{
					ChangeTo(customOutfit);
					return true;
				}
			}
			foreach (DefinedOutfit definedOutfit in DefinedOutfits)
			{
				if (((IDresserOutfit)definedOutfit).OutfitType == type)
				{
					ChangeTo(definedOutfit);
					return true;
				}
			}
			return false;
		}

		public void InitApparelColor()
		{
			if (ApparelColors == null || ApparelColors.Count == 0)
			{
				int layerCount = Util.LayerCount;
				ApparelColors = new List<SlotColor>(layerCount);
				for (int i = 0; i < layerCount; i++)
				{
					ApparelColors.Add(new SlotColor());
				}
			}
		}

		public ApparelLayerDef GetOuterMostLayer(Apparel a)
		{
			int num = Util.ToInt(ApparelLayerDefOf.OnSkin);
			foreach (ApparelLayerDef layer in ((Thing)a).def.apparel.layers)
			{
				int num2 = Util.ToInt(layer);
				if (num2 >= num)
				{
					num = num2;
				}
			}
			return Util.ToLayer(num);
		}

		public void ApplyApparelColor(Apparel a)
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			int num = Util.ToInt(GetOuterMostLayer(a));
			if (ApparelColors != null && ApparelColors.Count > num)
			{
				SlotColor slotColor = ApparelColors[num];
				CompColorableUtility.SetColor((Thing)(object)a, slotColor.Color, true);
			}
		}

		public void SetApparelColor(Apparel a, Color color)
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			InitApparelColor();
			int index = Util.ToInt(GetOuterMostLayer(a));
			SlotColor slotColor = ApparelColors[index];
			slotColor.IsAssigned = true;
			slotColor.Color = color;
			ApparelColors[index] = slotColor;
		}

		public bool Contains(ApparelPolicy outfit)
		{
			foreach (DefinedOutfit definedOutfit in DefinedOutfits)
			{
				if (definedOutfit.Outfit == outfit)
				{
					return true;
				}
			}
			return false;
		}

		public void Remove(ApparelPolicy outfit)
		{
			for (int i = 0; i < DefinedOutfits.Count; i++)
			{
				if (DefinedOutfits[i].Outfit == outfit)
				{
					DefinedOutfits.RemoveAt(i);
					break;
				}
			}
		}

		public bool Remove(IDresserOutfit outfit)
		{
			if (outfit is DefinedOutfit)
			{
				foreach (DefinedOutfit definedOutfit in DefinedOutfits)
				{
					if (definedOutfit.UniqueId.Equals(outfit.UniqueId))
					{
						DefinedOutfits.Remove(definedOutfit);
						return true;
					}
				}
			}
			else
			{
				foreach (CustomOutfit customOutfit in CustomOutfits)
				{
					if (customOutfit.UniqueId.Equals(outfit.UniqueId))
					{
						ApparelUtil.StoreApparelInWorld(customOutfit.Apparel, Pawn);
						CustomOutfits.Remove(customOutfit);
						return true;
					}
				}
			}
			return false;
		}

		public void AddCustomApparel(Apparel apparel)
		{
			if (!customApparel.Contains(apparel))
			{
				customApparel.Add(apparel);
			}
		}

		public bool ContainsCustomApparel(Apparel apparel)
		{
			return customApparel.Contains(apparel);
		}

		public void RemoveCustomApparel(Apparel apparel)
		{
			customApparel.Remove(apparel);
		}

		public void UpdateOutfitType(ApparelPolicy outfit, OutfitType outfitType)
		{
			foreach (DefinedOutfit definedOutfit in DefinedOutfits)
			{
				if (definedOutfit.Outfit == outfit)
				{
					definedOutfit.OutfitType = outfitType;
					break;
				}
			}
		}

		[Obsolete("Should saved in CustomOutfit class.", false)]
		public void UpdateCustomApparel(Building_Dresser dresser)
		{
		}

		public void ExposeData()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Invalid comparison between Unknown and I4
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fe: Invalid comparison between Unknown and I4
			if ((int)Scribe.mode == 1)
			{
				foreach (Apparel item in Pawn.apparel.WornApparel)
				{
					customApparel.Remove(item);
				}
				Clean();
			}
			Scribe_References.Look<Pawn>(ref Pawn, "pawn", false);
			Scribe_Collections.Look<DefinedOutfit>(ref DefinedOutfits, "definedOutfits", (LookMode)2, new object[0]);
			Scribe_Collections.Look<CustomOutfit>(ref CustomOutfits, "customOutfits", (LookMode)2, new object[0]);
			Scribe_Collections.Look<SlotColor>(ref ApparelColors, "apparelColors", (LookMode)2, new object[0]);
			Scribe_Collections.Look<Apparel>(ref customApparel, "customApparel", false, (LookMode)2, new object[0]);
			Scribe_Values.Look<string>(ref currentlyWorn, "currentlyWorn", (string)null, false);
			Scribe_Values.Look<string>(ref lastBattleOutfit, "lastBattleOutfit", (string)null, false);
			Scribe_Values.Look<string>(ref lastCivilianOutfit, "lastCivilianOutfit", (string)null, false);
			if ((int)Scribe.mode == 4)
			{
				Clean();
			}
		}

		public void ClearApparelColors()
		{
			if (ApparelColors != null)
			{
				ApparelColors.Clear();
				ApparelColors = null;
			}
		}

		public bool HasApparelColors()
		{
			if (ApparelColors != null)
			{
				return ApparelColors.Count > 0;
			}
			return false;
		}
	}
	internal class SlotColor : IExposable
	{
		public bool IsAssigned;

		public Color Color = Color.white;

		public void ExposeData()
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			Scribe_Values.Look<bool>(ref IsAssigned, "isAssigned", false, false);
			Scribe_Values.Look<Color>(ref Color, "color", default(Color), false);
		}
	}
	public class SettingsController : Mod
	{
		public SettingsController(ModContentPack content)
			: base(content)
		{
			((Mod)this).GetSettings<Settings>();
		}

		public override string SettingsCategory()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("ChangeDresser"));
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			Settings.DoSettingsWindowContents(inRect);
		}
	}
	public class Settings : ModSettings
	{
		private const int DEFAULT_MENDING_SPEED = 1;

		private const float DEFAULT_MENDING_UPDATE_INTERVAL = 5f;

		public static bool ShowDresserButtonForPawns = true;

		public static bool ShowGenderAgeChange = true;

		public static bool ShowBodyChange = true;

		public static bool IncludeColorByLayer = true;

		public static bool PersistApparelOriginalColor = true;

		public static bool ShareHairAcrossGenders = false;

		public static int MendingAttachmentMendingSpeed = 1;

		public static string MendingAttachmentMendingSpeedBuffer = 1.ToString();

		public static float MendingAttachmentUpdateInterval = 5f;

		public static string MendingAttachmentUpdateIntervalBuffer = 5f.ToString();

		public const bool KeepForcedApparel = true;

		public const int RepairAttachmentDistance = 6;

		public static long MendingAttachmentUpdateIntervalTicks => (long)(MendingAttachmentUpdateInterval * 10000000f);

		public override void ExposeData()
		{
			((ModSettings)this).ExposeData();
			Scribe_Values.Look<bool>(ref ShowDresserButtonForPawns, "ChangeDresser.ShowDresserButtonForPawns", false, false);
			Scribe_Values.Look<bool>(ref ShowGenderAgeChange, "ChangeDresser.ShowGenderAgeChange", true, false);
			Scribe_Values.Look<bool>(ref ShowBodyChange, "ChangeDresser.ShowBodyChange", true, false);
			Scribe_Values.Look<bool>(ref IncludeColorByLayer, "ChangeDresser.IncludeColorByLayer", true, false);
			Scribe_Values.Look<bool>(ref PersistApparelOriginalColor, "ChangeDresser.PersistApparelOriginalColor", false, false);
			Scribe_Values.Look<bool>(ref ShareHairAcrossGenders, "ChangeDresser.ShareHairAcrossGenders", false, false);
			Scribe_Values.Look<int>(ref MendingAttachmentMendingSpeed, "ChangeDresser.MendingAttachmentHpPerTick", 1, false);
			MendingAttachmentMendingSpeedBuffer = MendingAttachmentMendingSpeed.ToString();
			Scribe_Values.Look<float>(ref MendingAttachmentUpdateInterval, "ChangeDresser.MendingAttachmentUpdateInterval", 5f, false);
			MendingAttachmentUpdateIntervalBuffer = $"{MendingAttachmentUpdateInterval:0.0###}";
		}

		public static void DoSettingsWindowContents(Rect rect)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Expected O, but got Unknown
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0135: Unknown result type (might be due to invalid IL or missing references)
			//IL_017d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0188: Unknown result type (might be due to invalid IL or missing references)
			_ = PersistApparelOriginalColor;
			Listing_Standard val = new Listing_Standard((GameFont)1);
			_ = ((Listing)val).ColumnWidth;
			((Listing)val).ColumnWidth = Math.Min(400f, ((Rect)(ref rect)).width / 2f);
			((Listing)val).Begin(rect);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ShowDresserButtonForPawns")), ref ShowDresserButtonForPawns, (string)null, 0f, 1f);
			((Listing)val).Gap(4f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.IncludeColorByLayer")), ref IncludeColorByLayer, (string)null, 0f, 1f);
			((Listing)val).Gap(4f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.PersistApparelOriginalColor")), ref PersistApparelOriginalColor, (string)null, 0f, 1f);
			((Listing)val).Gap(4f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ShareHairAcrossGenders")), ref ShareHairAcrossGenders, (string)null, 0f, 1f);
			((Listing)val).Gap(4f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ShowBodyChange")), ref ShowBodyChange, (string)null, 0f, 1f);
			if (ShowBodyChange)
			{
				((Listing)val).Gap(4f);
				val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ShowGenderAgeChange")), ref ShowGenderAgeChange, (string)null, 0f, 1f);
				((Listing)val).Gap(20f);
			}
			else
			{
				((Listing)val).Gap(48f);
			}
			((Listing)val).Gap(10f);
			val.Label(Translator.Translate("ChangeDresser.MendingAttachmentSettings"), -1f, (string)null);
			((Listing)val).Gap(4f);
			NumberInput(val, "ChangeDresser.SecondsBetweenTicks", ref MendingAttachmentUpdateInterval, ref MendingAttachmentUpdateIntervalBuffer, 5f, 0.25f, 120f);
			((Listing)val).Gap(4f);
			NumberInput(val, "ChangeDresser.HPPerTick", ref MendingAttachmentMendingSpeed, ref MendingAttachmentMendingSpeedBuffer, 1, 1, 60);
			((Listing)val).End();
		}

		private static void NumberInput(Listing_Standard l, string label, ref float val, ref string buffer, float defaultVal, float min, float max)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				l.TextFieldNumericLabeled<float>(TaggedString.op_Implicit(Translator.Translate(label)), ref val, ref buffer, min, max);
				if (l.ButtonText(TaggedString.op_Implicit(Translator.Translate("ResetButton")), (string)null, 1f))
				{
					val = defaultVal;
					buffer = $"{defaultVal:0.0###}";
				}
			}
			catch
			{
				val = min;
				buffer = $"{min:0.0###}";
			}
		}

		private static void NumberInput(Listing_Standard l, string label, ref int val, ref string buffer, int defaultVal, int min, int max)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				l.TextFieldNumericLabeled<int>(TaggedString.op_Implicit(Translator.Translate(label)), ref val, ref buffer, (float)min, (float)max);
				if (l.ButtonText(TaggedString.op_Implicit(Translator.Translate("ResetButton")), (string)null, 1f))
				{
					val = defaultVal;
					buffer = defaultVal.ToString();
				}
			}
			catch
			{
				val = min;
				buffer = min.ToString();
			}
		}
	}
	public class StoredApparel
	{
		internal Dictionary<ThingDef, LinkedList<Apparel>> StoredApparelLookup = new Dictionary<ThingDef, LinkedList<Apparel>>();

		private static readonly SimpleCurve HitPointsPercentScoreFactorCurve;

		public int Count
		{
			get
			{
				int num = 0;
				foreach (LinkedList<Apparel> value in StoredApparelLookup.Values)
				{
					num += value.Count;
				}
				return num;
			}
		}

		public IEnumerable<Apparel> Apparel
		{
			get
			{
				List<Apparel> list = new List<Apparel>(Count);
				foreach (LinkedList<Apparel> value in StoredApparelLookup.Values)
				{
					foreach (Apparel item in value)
					{
						list.Add(item);
					}
				}
				return list;
			}
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		public void AddApparel(Apparel apparel)
		{
			if (apparel != null)
			{
				if (!StoredApparelLookup.TryGetValue(((Thing)apparel).def, out var value))
				{
					value = new LinkedList<Apparel>();
					StoredApparelLookup.Add(((Thing)apparel).def, value);
				}
				AddApparelToLinkedList(apparel, value);
			}
			else
			{
				Log.Error("Tried to add null apparel");
			}
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		private static float ApparelScoreRaw(Apparel ap)
		{
			if (((Thing)ap).def.apparel.blocksVision || ((Thing)ap).def.apparel.slaveApparel)
			{
				return -10f;
			}
			float num = 0.1f + ((Thing)ap).def.apparel.scoreOffset + (StatExtension.GetStatValue((Thing)(object)ap, StatDefOf.ArmorRating_Sharp, true, -1) + StatExtension.GetStatValue((Thing)(object)ap, StatDefOf.ArmorRating_Blunt, true, -1));
			if (((Thing)ap).def.useHitPoints)
			{
				float num2 = (float)((Thing)ap).HitPoints / (float)((Thing)ap).MaxHitPoints;
				num *= HitPointsPercentScoreFactorCurve.Evaluate(num2);
			}
			float num3 = num + ap.GetSpecialApparelScoreOffset();
			float num4 = 1f;
			float num5 = num3 * num4;
			if (ap.WornByCorpse)
			{
				num5 -= 0.5f;
				if ((double)num5 > 0.0)
				{
					num5 *= 0.1f;
				}
			}
			if (((Thing)ap).Stuff == ThingDefOf.Human.race.leatherDef)
			{
				num5 -= 0.5f;
			}
			return num5;
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		private void AddApparelToLinkedList(Apparel apparel, LinkedList<Apparel> l)
		{
			float num = ApparelScoreRaw(apparel);
			for (LinkedListNode<Apparel> linkedListNode = l.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
			{
				float num2 = ApparelScoreRaw(apparel);
				if (num >= num2)
				{
					l.AddBefore(linkedListNode, apparel);
					return;
				}
				if (num < num2)
				{
					l.AddAfter(linkedListNode, apparel);
					return;
				}
			}
			l.AddLast(apparel);
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		internal int GetApparelCount(ThingDef expectedDef, QualityRange qualityRange, FloatRange hpRange, ThingFilter filter)
		{
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			if (StoredApparelLookup.TryGetValue(expectedDef, out var value))
			{
				int num = 0;
				{
					foreach (Apparel item in value)
					{
						if (Allows((Thing)(object)item, expectedDef, qualityRange, hpRange, filter))
						{
							num++;
						}
					}
					return num;
				}
			}
			return 0;
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		private bool Allows(Thing t, ThingDef expectedDef, QualityRange qualityRange, FloatRange hpRange, ThingFilter filter)
		{
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			if (t.def != expectedDef)
			{
				return false;
			}
			if (expectedDef.useHitPoints && hpRange.min != 0f && hpRange.max != 100f)
			{
				float num = (float)t.HitPoints / (float)t.MaxHitPoints;
				num = GenMath.RoundedHundredth(num);
				if (!((FloatRange)(ref hpRange)).IncludesEpsilon(Mathf.Clamp01(num)))
				{
					return false;
				}
			}
			if (qualityRange != QualityRange.All && QualityUtility.FollowQualityThingFilter(t.def))
			{
				QualityCategory val = default(QualityCategory);
				if (!QualityUtility.TryGetQuality(t, ref val))
				{
					val = (QualityCategory)2;
				}
				if (!((QualityRange)(ref qualityRange)).Includes(val))
				{
					return false;
				}
			}
			if (filter != null && !filter.Allows(t.Stuff))
			{
				return false;
			}
			return true;
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		public bool Contains(Apparel apparel)
		{
			if (StoredApparelLookup.TryGetValue(((Thing)apparel).def, out var value))
			{
				return value.Contains(apparel);
			}
			return false;
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		public void Clear()
		{
			foreach (LinkedList<Apparel> value in StoredApparelLookup.Values)
			{
				value.Clear();
			}
			StoredApparelLookup.Clear();
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		public bool TryRemoveApparel(ThingDef def, out Apparel apparel)
		{
			if (StoredApparelLookup.TryGetValue(def, out var value) && value.Count > 0)
			{
				apparel = value.First.Value;
				value.RemoveFirst();
				return true;
			}
			apparel = null;
			return false;
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		public bool TryRemoveBestApparel(ThingDef def, out Apparel apparel)
		{
			if (StoredApparelLookup.TryGetValue(def, out var value) && value.Count > 0)
			{
				apparel = value.First.Value;
				value.RemoveFirst();
				return true;
			}
			apparel = null;
			return false;
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		public bool RemoveApparel(Apparel apparel)
		{
			if (StoredApparelLookup.TryGetValue(((Thing)apparel).def, out var value))
			{
				return value.Remove(apparel);
			}
			return false;
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		internal bool TryRemoveBestApparel(ThingDef def, Pawn pawn, out Apparel apparel)
		{
			ThingFilter filter = pawn.outfits.CurrentApparelPolicy.filter;
			if (StoredApparelLookup.TryGetValue(def, out var value))
			{
				LinkedListNode<Apparel> linkedListNode = value.First;
				while (linkedListNode != null)
				{
					try
					{
						LinkedListNode<Apparel> next = linkedListNode.Next;
						if (filter.Allows((Thing)(object)linkedListNode.Value) && linkedListNode.Value.PawnCanWear(pawn, false))
						{
							value.Remove(linkedListNode);
							apparel = linkedListNode.Value;
							return true;
						}
						linkedListNode = next;
					}
					catch
					{
						Log.Error("catch");
					}
				}
			}
			apparel = null;
			return false;
		}

		[Obsolete("No StoredApparel Allowed.", true)]
		public List<Apparel> RemoveFilteredApparel(StorageSettings settings)
		{
			List<Apparel> list = new List<Apparel>(0);
			foreach (LinkedList<Apparel> value in StoredApparelLookup.Values)
			{
				for (LinkedListNode<Apparel> linkedListNode = value.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
				{
					_ = linkedListNode.Next;
					if (!settings.AllowedToAccept((Thing)(object)linkedListNode.Value))
					{
						value.Remove(linkedListNode);
						list.Add(linkedListNode.Value);
					}
				}
			}
			return list;
		}

		static StoredApparel()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Expected O, but got Unknown
			SimpleCurve val = new SimpleCurve();
			val.Add(new CurvePoint(0f, 0f), true);
			val.Add(new CurvePoint(0.2f, 0.2f), true);
			val.Add(new CurvePoint(0.22f, 0.3f), true);
			val.Add(new CurvePoint(0.5f, 0.3f), true);
			val.Add(new CurvePoint(0.52f, 1f), true);
			HitPointsPercentScoreFactorCurve = val;
		}
	}
	public enum ColorPresetType
	{
		Apparel,
		Hair,
		Skin
	}
	internal static class IOUtil
	{
		private static bool TryGetFileName(ColorPresetType type, out string fileName)
		{
			if (TryGetDirectoryPath(type, out fileName))
			{
				fileName = Path.Combine(fileName, type.ToString() + ".xml");
				return true;
			}
			return false;
		}

		private static bool TryGetDirectoryPath(ColorPresetType type, out string path)
		{
			if (TryGetDirectoryName(out path))
			{
				DirectoryInfo directoryInfo = new DirectoryInfo(path);
				if (!directoryInfo.Exists)
				{
					directoryInfo.Create();
				}
				return true;
			}
			return false;
		}

		private static bool TryGetDirectoryName(out string path)
		{
			try
			{
				path = (string)typeof(GenFilePaths).GetMethod("FolderUnderSaveData", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[1] { "ChangeDresser" });
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("ChangeDresser: Failed to get folder name - " + ex);
				path = null;
				return false;
			}
		}
	}
	internal static class Util
	{
		private static readonly Dictionary<ApparelLayerDef, int> layerDictionary = new Dictionary<ApparelLayerDef, int>();

		public static int LayerCount
		{
			get
			{
				InitDic();
				return layerDictionary.Count;
			}
		}

		public static IEnumerable<ApparelLayerDef> Layers
		{
			get
			{
				InitDic();
				return layerDictionary.Keys;
			}
		}

		private static void InitDic()
		{
			if (layerDictionary.Count != 0)
			{
				return;
			}
			int num = 0;
			foreach (ApparelLayerDef allDef in DefDatabase<ApparelLayerDef>.AllDefs)
			{
				layerDictionary.Add(allDef, num);
				num++;
			}
		}

		public static int ToInt(ApparelLayerDef layer)
		{
			InitDic();
			return layerDictionary[layer];
		}

		public static ApparelLayerDef ToLayer(int layer)
		{
			foreach (KeyValuePair<ApparelLayerDef, int> item in layerDictionary)
			{
				if (item.Value == layer)
				{
					return item.Key;
				}
			}
			Log.Warning("Unable to find layer matching " + layer + ". Using OnSkin.");
			return ApparelLayerDefOf.OnSkin;
		}
	}
}
namespace ChangeDresser.UI
{
	internal class AssignOutfitUI : Window
	{
		private PawnTable table;

		private readonly Building_Dresser Dresser;

		private Vector2 scrollPosition = new Vector2(0f, 0f);

		private float PreviousY;

		private const int NAME_WIDTH = 100;

		private const int CHECKBOX_WIDTH = 100;

		private const int HEIGHT = 35;

		private const int X_BUFFER = 10;

		private const int Y_BUFFER = 5;

		private float ExtraBottomSpace = 28f;

		private float ExtraTopSpace;

		public static float WindowsHeight = 600f;

		public static float WindowsWidth = 1000f;

		private PawnTableDef PawnTableDef => WorldComp.AssginOutfit;

		protected IEnumerable<Pawn> Pawns => PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.Where((Pawn pawn) => !DevelopmentalStageExtensions.Baby(pawn.DevelopmentalStage));

		public override Vector2 InitialSize => new Vector2(WindowsWidth, WindowsHeight);

		public AssignOutfitUI(Building_Dresser dresser)
			: base((IWindowDrawing)null)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			Dresser = dresser;
			base.closeOnClickedOutside = true;
			base.doCloseButton = false;
			base.doCloseX = true;
			base.absorbInputAroundWindow = true;
			base.forcePause = true;
			base.closeOnClickedOutside = false;
			foreach (Pawn item in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.Where((Pawn pawn) => !DevelopmentalStageExtensions.Baby(pawn.DevelopmentalStage)))
			{
				if (((Thing)item).Faction != Faction.OfPlayer || !((Thing)item).def.race.Humanlike)
				{
					continue;
				}
				Pawn_ApparelTracker apparel = item.apparel;
				if (apparel != null && apparel.LockedApparel?.Count == 0 && !WorldComp.PawnOutfits.ContainsKey(item))
				{
					PawnOutfitTracker pawnOutfitTracker = new PawnOutfitTracker
					{
						Pawn = item
					};
					ApparelPolicy currentApparelPolicy = item.outfits.CurrentApparelPolicy;
					if (currentApparelPolicy != null)
					{
						pawnOutfitTracker.AddOutfit(new DefinedOutfit(currentApparelPolicy, WorldComp.GetOutfitType(currentApparelPolicy)));
					}
					WorldComp.PawnOutfits.Add(item, pawnOutfitTracker);
				}
			}
			if (table == null)
			{
				table = CreateTable();
			}
			CreateTable();
			table.SetDirty();
		}

		public void RefreshTable()
		{
			table = CreateTable();
			table.SetDirty();
		}

		private PawnTable CreateTable()
		{
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Expected O, but got Unknown
			return (PawnTable)Activator.CreateInstance(PawnTableDef.workerClass, PawnTableDef, (Func<IEnumerable<Pawn>>)(() => Pawns), 99999, (int)((double)WindowsHeight - 35.0 - (double)ExtraTopSpace - (double)ExtraBottomSpace - (double)((Window)this).Margin * 2.0));
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_013e: Unknown result type (might be due to invalid IL or missing references)
			//IL_014e: Unknown result type (might be due to invalid IL or missing references)
			//IL_015e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0160: Unknown result type (might be due to invalid IL or missing references)
			//IL_0165: Unknown result type (might be due to invalid IL or missing references)
			//IL_0168: Unknown result type (might be due to invalid IL or missing references)
			//IL_016d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0186: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (Widgets.ButtonText(new Rect(0f, 0f, 150f, 35f), TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ManageOutfits")), true, true, true, (TextAnchor?)null))
				{
					Find.WindowStack.Add((Window)(object)new Dialog_ManageApparelPolicies_NotifyOnClose(delegate
					{
						RefreshTable();
					}));
				}
				if (Widgets.ButtonText(new Rect(170f, 0f, 150f, 35f), TaggedString.op_Implicit(Translator.Translate("ChangeDresser.FindMissingApparel")), true, true, true, (TextAnchor?)null))
				{
					foreach (PawnOutfitTracker value in WorldComp.DisfunctionPawnOutfits.Values)
					{
						Pawn pawn = value.Pawn;
						ApparelUtil.StoreApparelInWorld(value.CustomApparel.ToList(), pawn);
						WorldComp.PawnOutfits.Remove(pawn);
					}
				}
				Rect val = default(Rect);
				((Rect)(ref val))..ctor(((Rect)(ref inRect)).x, ((Rect)(ref inRect)).y + ExtraTopSpace + 35f, (float)((int)WindowsWidth - 36), table.Size.y + ExtraBottomSpace);
				Rect val2 = default(Rect);
				((Rect)(ref val2))..ctor(0f, 0f, table.Size.x, table.Size.y);
				scrollPosition = GUI.BeginScrollView(val, scrollPosition, val2, false, false);
				table.PawnTableOnGUI(new Vector2(((Rect)(ref val2)).x, ((Rect)(ref val2)).y));
				GUI.EndScrollView();
			}
			catch (Exception ex)
			{
				Log.Error(ex.GetType()?.ToString() + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace);
			}
		}

		public void DoWindowContentsOld(Rect inRect)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Expected O, but got Unknown
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0120: Unknown result type (might be due to invalid IL or missing references)
			//IL_012a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0241: Unknown result type (might be due to invalid IL or missing references)
			//IL_0256: Unknown result type (might be due to invalid IL or missing references)
			//IL_0270: Unknown result type (might be due to invalid IL or missing references)
			//IL_027f: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_033d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0343: Unknown result type (might be due to invalid IL or missing references)
			//IL_0368: Unknown result type (might be due to invalid IL or missing references)
			//IL_036d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0372: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				base.windowRect = inRect;
				if (Widgets.ButtonText(new Rect(0f, 0f, 150f, 35f), TaggedString.op_Implicit(Translator.Translate("ChangeDresser.ManageOutfits")), true, true, true, (TextAnchor?)null))
				{
					Find.WindowStack.Add((Window)new Dialog_ManageApparelPolicies((ApparelPolicy)null));
				}
				List<ApparelPolicy> allOutfits = Current.Game.outfitDatabase.AllOutfits;
				int num = 90;
				GUI.BeginScrollView(new Rect(0f, (float)num, ((Rect)(ref inRect)).width - 32f, 85f), scrollPosition, new Rect(0f, (float)num, (float)(110 + 110 * allOutfits.Count), 85f), GUIStyle.none, GUIStyle.none);
				int num2 = 110;
				foreach (ApparelPolicy item in allOutfits)
				{
					Widgets.Label(new Rect((float)num2, (float)num, 100f, 35f), ((Policy)item).label);
					num2 += 110;
				}
				num += 40;
				num2 = 0;
				Widgets.Label(new Rect((float)num2, (float)num, 100f, 35f), Translator.Translate("ChangeDresser.UseForBattle"));
				num2 += 110;
				foreach (ApparelPolicy item2 in allOutfits)
				{
					bool flag = WorldComp.OutfitsForBattle.Contains(item2);
					bool flag2 = flag;
					Widgets.Checkbox((float)(num2 + 10), (float)num, ref flag, 24f, false, false, (Texture2D)null, (Texture2D)null);
					num2 += 110;
					if (flag == flag2)
					{
						continue;
					}
					if (flag)
					{
						WorldComp.OutfitsForBattle.Add(item2);
					}
					else
					{
						WorldComp.OutfitsForBattle.Remove(item2);
					}
					foreach (PawnOutfitTracker value in WorldComp.PlayFunctionPawnOutfits.Values)
					{
						value.UpdateOutfitType(item2, (!flag) ? OutfitType.Civilian : OutfitType.Battle);
					}
				}
				num += 45;
				Widgets.DrawLineHorizontal(110f, (float)(num - 4), (float)(110 + 110 * allOutfits.Count));
				GUI.EndScrollView();
				GUI.BeginScrollView(new Rect(0f, (float)num, 100f, ((Rect)(ref inRect)).height - (float)num - 90f), new Vector2(0f, scrollPosition.y), new Rect(0f, 0f, 100f, PreviousY), GUIStyle.none, GUIStyle.none);
				num2 = 0;
				int num3 = 0;
				foreach (PawnOutfitTracker value2 in WorldComp.PlayFunctionPawnOutfits.Values)
				{
					Widgets.Label(new Rect((float)num2, (float)num3, 100f, 35f), value2.Pawn.Name.ToStringShort);
					num3 += 40;
				}
				PreviousY = num3;
				Widgets.DrawLineVertical(102f, (float)num, (float)(40 * WorldComp.PlayFunctionPawnOutfits.Values.Count));
				GUI.EndScrollView();
				int num4 = 114;
				scrollPosition = GUI.BeginScrollView(new Rect((float)num4, (float)num, ((Rect)(ref inRect)).width - (float)num4, ((Rect)(ref inRect)).height - (float)num - 90f), scrollPosition, new Rect(0f, 0f, (float)(110 + 110 * allOutfits.Count - num4), PreviousY));
				num3 = 0;
				foreach (PawnOutfitTracker value3 in WorldComp.PlayFunctionPawnOutfits.Values)
				{
					num2 = 0;
					foreach (ApparelPolicy item3 in allOutfits)
					{
						bool flag3 = value3.Contains(item3);
						bool flag4 = flag3;
						Widgets.Checkbox((float)(num2 + 10), (float)num3, ref flag3, 24f, false, false, (Texture2D)null, (Texture2D)null);
						num2 += 110;
						if (flag3 != flag4)
						{
							HandleOutfitAssign(flag3, item3, value3);
						}
					}
					num3 += 40;
				}
				GUI.EndScrollView();
			}
			catch (Exception ex)
			{
				Log.Error(ex.GetType()?.ToString() + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace);
			}
		}

		private void HandleOutfitAssign(bool assign, ApparelPolicy outfit, PawnOutfitTracker po)
		{
			Pawn pawn = po.Pawn;
			if (assign)
			{
				po.DefinedOutfits.Add(new DefinedOutfit(outfit, WorldComp.GetOutfitType(outfit)));
				return;
			}
			po.Remove(outfit);
			if (!((object)pawn.outfits.CurrentApparelPolicy).Equals((object?)outfit))
			{
				return;
			}
			if (!((!pawn.Drafted) ? (!po.ChangeToCivilianOutfit()) : (!po.ChangeToBattleOutfit())))
			{
				Messages.Message(pawn.Name.ToStringShort + " will no longer wear " + ((Policy)outfit).label + ". Could not find another Outfit for them to wear. Please fix this manually.", MessageTypeDefOf.CautionInput, true);
				return;
			}
			IDresserOutfit currentOutfit = po.CurrentOutfit;
			if (currentOutfit != null)
			{
				Messages.Message(pawn.Name.ToStringShort + " will no longer wear " + ((Policy)outfit).label + " and will instead be assigned to wear " + currentOutfit.Label, MessageTypeDefOf.CautionInput, true);
			}
			else
			{
				Messages.Message(pawn.Name.ToStringShort + " will no longer wear " + ((Policy)outfit).label + " but could not be assigned anything else to wear.", MessageTypeDefOf.CautionInput, true);
			}
		}
	}
	public class Dialog_ManageApparelPolicies_NotifyOnClose : Dialog_ManageApparelPolicies
	{
		private readonly Action onClose;

		public Dialog_ManageApparelPolicies_NotifyOnClose(Action onClose)
			: base((ApparelPolicy)null)
		{
			this.onClose = onClose;
		}

		public override void PostClose()
		{
			((Window)this).PostClose();
			onClose?.Invoke();
		}
	}
	public class CustomOutfitUI : Window
	{
		private readonly Building_Dresser Dresser;

		private Pawn pawn;

		private PawnOutfitTracker outfitTracker;

		private CustomOutfit customOutfit;

		private Vector2 scrollPosLeft = new Vector2(0f, 0f);

		private Vector2 scrollPosRight = new Vector2(0f, 0f);

		private List<Pawn> selectablePawns = new List<Pawn>();

		private List<ApparelPolicy> selectableOutfits = new List<ApparelPolicy>();

		private List<Apparel> availableApparel = new List<Apparel>();

		private const int HEIGHT = 35;

		private const int X_BUFFER = 10;

		private const int Y_BUFFER = 5;

		private const float CELL_HEIGHT = 40f;

		private CDApparelFilters apparelFilter = new CDApparelFilters();

		private Vector2 filterScrollPosition = new Vector2(0f, 0f);

		public override Vector2 InitialSize => new Vector2(1100f, 600f);

		public CustomOutfitUI(Building_Dresser dresser)
			: base((IWindowDrawing)null)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			Dresser = dresser;
			base.closeOnClickedOutside = false;
			base.doCloseButton = false;
			base.doCloseX = false;
			base.absorbInputAroundWindow = true;
			base.forcePause = true;
			WorldComp.CleanupCustomOutfits();
			foreach (Pawn allMapsCaravansAndTravellingTransporters_Alive_Colonist in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists)
			{
				if (((Thing)allMapsCaravansAndTravellingTransporters_Alive_Colonist).def.race.Humanlike)
				{
					Pawn_ApparelTracker apparel = allMapsCaravansAndTravellingTransporters_Alive_Colonist.apparel;
					if (apparel != null && apparel.LockedApparel?.Count == 0)
					{
						selectablePawns.Add(allMapsCaravansAndTravellingTransporters_Alive_Colonist);
					}
				}
			}
			UpdateAvailableApparel();
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0200: Unknown result type (might be due to invalid IL or missing references)
			//IL_0223: Unknown result type (might be due to invalid IL or missing references)
			//IL_0253: Unknown result type (might be due to invalid IL or missing references)
			//IL_0109: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_011d: Unknown result type (might be due to invalid IL or missing references)
			//IL_013e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0363: Unknown result type (might be due to invalid IL or missing references)
			//IL_028e: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bf: Expected O, but got Unknown
			//IL_0476: Unknown result type (might be due to invalid IL or missing references)
			//IL_047c: Expected O, but got Unknown
			//IL_04c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_04cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0538: Unknown result type (might be due to invalid IL or missing references)
			//IL_039e: Unknown result type (might be due to invalid IL or missing references)
			//IL_03c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_03cf: Expected O, but got Unknown
			//IL_05b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_031b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0325: Expected O, but got Unknown
			//IL_03ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0404: Unknown result type (might be due to invalid IL or missing references)
			//IL_0436: Unknown result type (might be due to invalid IL or missing references)
			//IL_0440: Expected O, but got Unknown
			//IL_0343: Unknown result type (might be due to invalid IL or missing references)
			//IL_034d: Expected O, but got Unknown
			//IL_0467: Unknown result type (might be due to invalid IL or missing references)
			//IL_0471: Expected O, but got Unknown
			try
			{
				Text.Font = (GameFont)1;
				GUI.color = Color.white;
				Widgets.Label(new Rect(0f, 0f, 200f, 50f), Translator.Translate("ChangeDresser.CustomOutfits"));
				int num = 40;
				int x = 0;
				x = DrawPawnSelection(x, num);
				x = DrawOutfitSelection(x, num);
				x = DrawOutfitDelete(x, num);
				if (outfitTracker != null && Widgets.ButtonText(new Rect((float)x, (float)num, 75f, 30f), TaggedString.op_Implicit(Translator.Translate("ChangeDresser.New")), true, true, true, (TextAnchor?)null))
				{
					customOutfit = new CustomOutfit();
					customOutfit.Name = "Custom Outfit " + (outfitTracker.CustomOutfits.Count + 1);
					outfitTracker.AddOutfit(customOutfit);
					UpdateAvailableApparel();
				}
				num += 40;
				if (customOutfit != null)
				{
					Widgets.Label(new Rect(0f, (float)num, 60f, 30f), Translator.Translate("ChangeDresser.OutfitName") + ":");
					customOutfit.Name = Widgets.TextField(new Rect(70f, (float)num, 120f, 30f), customOutfit.Name);
					x = DrawUseInBattle(200, num);
					x = DrawBaseOutfit(x, num);
					num += 40;
				}
				float num2 = ((Rect)(ref inRect)).height - (float)num;
				num2 -= 105f;
				num2 -= 10f;
				DrawAvailableApparel(0f, num, 350f, num2);
				DrawOutfitApparel(360f, num, 710f, num2);
				num += (int)num2;
				num = 50;
				Widgets.Label(new Rect(750f, (float)num, 100f, 32f), Translator.Translate("Filter"));
				num += 40;
				Widgets.Label(new Rect(775f, (float)num, 75f, 32f), Translator.Translate("ChangeDresser.Name"));
				apparelFilter.Name = Widgets.TextField(new Rect(855f, (float)(num - 3), 120f, 32f), apparelFilter.Name);
				num += 35;
				if (Widgets.ButtonText(new Rect(775f, (float)num, 200f, 32f), apparelFilter.LayerString, true, true, true, (TextAnchor?)null))
				{
					List<FloatMenuOption> list = new List<FloatMenuOption>();
					list.Add(new FloatMenuOption(apparelFilter.GetLayerLabel(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.All"))), (Action)delegate
					{
						//IL_001d: Unknown result type (might be due to invalid IL or missing references)
						apparelFilter.Layer = null;
						apparelFilter.LayerString = apparelFilter.GetLayerLabel(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.All")));
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					foreach (ApparelLayerDef d in DefDatabase<ApparelLayerDef>.AllDefs)
					{
						list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate(((Def)d).defName)), (Action)delegate
						{
							//IL_0037: Unknown result type (might be due to invalid IL or missing references)
							apparelFilter.Layer = d;
							apparelFilter.LayerString = apparelFilter.GetLayerLabel(TaggedString.op_Implicit(Translator.Translate(((Def)d).defName)));
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					}
					Find.WindowStack.Add((Window)new FloatMenu(list));
				}
				num += 35;
				if (Widgets.ButtonText(new Rect(775f, (float)num, 200f, 32f), apparelFilter.QualityString, true, true, true, (TextAnchor?)null))
				{
					List<FloatMenuOption> list2 = new List<FloatMenuOption>();
					list2.Add(new FloatMenuOption(apparelFilter.GetQualityLabel(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.All"))), (Action)delegate
					{
						//IL_0006: Unknown result type (might be due to invalid IL or missing references)
						//IL_000b: Unknown result type (might be due to invalid IL or missing references)
						//IL_0021: Unknown result type (might be due to invalid IL or missing references)
						apparelFilter.Quality = QualityRange.All;
						apparelFilter.QualityString = apparelFilter.GetQualityLabel(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.All")));
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					foreach (QualityCategory q in Enum.GetValues(typeof(QualityCategory)))
					{
						list2.Add(new FloatMenuOption(((object)q/*cast due to constrained. prefix*/).ToString(), (Action)delegate
						{
							//IL_0011: Unknown result type (might be due to invalid IL or missing references)
							//IL_0016: Unknown result type (might be due to invalid IL or missing references)
							//IL_0032: Unknown result type (might be due to invalid IL or missing references)
							apparelFilter.Quality.min = q;
							apparelFilter.QualityString = apparelFilter.GetQualityLabel(QualityUtility.GetLabel(q));
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					}
					Find.WindowStack.Add((Window)new FloatMenu(list2));
				}
				num += 35;
				Listing_Standard val = new Listing_Standard();
				Rect val2 = default(Rect);
				((Rect)(ref val2))..ctor(775f, (float)num, 216f, ((Rect)(ref inRect)).height - (float)num);
				Rect val3 = default(Rect);
				((Rect)(ref val3))..ctor(0f, 0f, 200f, (float)(64 * apparelFilter.StatDefs.Count));
				Widgets.BeginScrollView(val2, ref filterScrollPosition, val3, true);
				((Rect)(ref val2)).width = ((Rect)(ref val2)).width - 20f;
				((Rect)(ref val2)).height = 100000f;
				((Listing)val).Begin(GenUI.AtZero(val2));
				val.Label("HitPointsBasic".ToString() + " " + (int)apparelFilter.HP, -1f, (TipSignal?)null);
				apparelFilter.HP = val.Slider(apparelFilter.HP, 0f, 100f);
				for (int num3 = 0; num3 < apparelFilter.StatDefs.Count; num3++)
				{
					StatDefValue value = apparelFilter.StatDefs[num3];
					val.Label(((Def)value.Def).label + " " + value.Value.ToString("n2"), 28f, (TipSignal?)null);
					float value2 = value.Value;
					value2 = val.Slider(value2, 0f, value.Max);
					if (value.Value != value2)
					{
						value.Value = value2;
						apparelFilter.StatDefs[num3] = value;
					}
				}
				((Rect)(ref val3))..ctor(0f, 0f, ((Rect)(ref val2)).width, ((Listing)val).CurHeight);
				Widgets.EndScrollView();
				((Listing)val).End();
			}
			catch (Exception ex)
			{
				Log.Error(((object)this).GetType().Name + " closed due to: " + ex.GetType().Name + " " + ex.Message);
				Messages.Message(((object)this).GetType().Name + " closed due to: " + ex.GetType().Name + " " + ex.Message, MessageTypeDefOf.NegativeEvent, true);
				((Window)this).Close(true);
			}
		}

		private void DrawCloseButton(int x, int y)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			if (Widgets.ButtonText(new Rect((float)x, (float)y, 100f, 30f), TaggedString.op_Implicit(Translator.Translate("ChangeDresser.Close")), true, true, true, (TextAnchor?)null))
			{
				((Window)this).Close(true);
			}
		}

		private void DrawAvailableApparel(float x, float y, float width, float height)
		{
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_013c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_011a: Expected O, but got Unknown
			//IL_019c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0182: Unknown result type (might be due to invalid IL or missing references)
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(x, y, width - 10f, height);
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(0f, 0f, ((Rect)(ref val)).width - 16f, (float)availableApparel.Count * 40f);
			GUI.BeginGroup(val);
			scrollPosLeft = GUI.BeginScrollView(new Rect(GenUI.AtZero(val)), scrollPosLeft, val2);
			int i = 0;
			int num = 0;
			Rect val4 = default(Rect);
			Rect val5 = default(Rect);
			for (; i < availableApparel.Count; i++)
			{
				Apparel val3 = availableApparel[i];
				if (!apparelFilter.IncludeAppareL(val3))
				{
					continue;
				}
				((Rect)(ref val4))..ctor(0f, 2f + (float)num * 40f, ((Rect)(ref val)).width, 40f);
				num++;
				GUI.BeginGroup(val4);
				Widgets.ThingIcon(new Rect(0f, 0f, 40f, 40f), (Thing)(object)val3, 1f, (Rot4?)null, false, 1f, false);
				if (Widgets.InfoCardButton(40f, 0f, (Thing)(object)val3))
				{
					Find.WindowStack.Add((Window)new Dialog_InfoCard((Thing)(object)val3, (Precept_ThingStyle)null));
				}
				Widgets.Label(new Rect(75f, 0f, ((Rect)(ref val4)).width - 40f - 40f, 40f), ((Entity)val3).Label);
				if (customOutfit != null)
				{
					((Rect)(ref val5))..ctor(((Rect)(ref val4)).width - 35f, 10f, 20f, 20f);
					if (CanWear(val3))
					{
						if (Widgets.ButtonImage(val5, WidgetUtil.nextTexture, true, (string)null))
						{
							AddApparelToOutfit(val3);
							break;
						}
					}
					else
					{
						Widgets.ButtonImage(val5, WidgetUtil.cantTexture, true, (string)null);
					}
				}
				GUI.EndGroup();
			}
			GUI.EndScrollView();
			GUI.EndGroup();
		}

		private void DrawOutfitApparel(float x, int y, float width, float height)
		{
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_010d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_017a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0184: Expected O, but got Unknown
			if (pawn == null || outfitTracker == null || customOutfit == null)
			{
				return;
			}
			Widgets.Label(new Rect(x, (float)y, 150f, 30f), Translator.Translate("ChangeDresser.CustomOutfitApparel"));
			y += 40;
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(x, (float)y, width - 10f, height);
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(0f, 0f, ((Rect)(ref val)).width - 16f, (float)customOutfit.Apparel.Count * 40f);
			GUI.BeginGroup(val);
			scrollPosRight = GUI.BeginScrollView(new Rect(GenUI.AtZero(val)), scrollPosRight, val2);
			Rect val4 = default(Rect);
			for (int i = 0; i < customOutfit.Apparel.Count; i++)
			{
				Apparel val3 = customOutfit.Apparel[i];
				((Rect)(ref val4))..ctor(0f, 2f + (float)i * 40f, ((Rect)(ref val)).width, 40f);
				GUI.BeginGroup(val4);
				if (Widgets.ButtonImage(new Rect(5f, 10f, 20f, 20f), WidgetUtil.previousTexture, true, (string)null))
				{
					RemoveApparelFromOutfit(val3);
					break;
				}
				Widgets.ThingIcon(new Rect(35f, 0f, 40f, 40f), (Thing)(object)val3, 1f, (Rot4?)null, false, 1f, false);
				if (Widgets.InfoCardButton(75f, 0f, (Thing)(object)val3))
				{
					Find.WindowStack.Add((Window)new Dialog_InfoCard((Thing)(object)val3, (Precept_ThingStyle)null));
				}
				Widgets.Label(new Rect(115f, 0f, ((Rect)(ref val4)).width - 40f - 45f, 40f), ((Entity)val3).Label);
				GUI.EndGroup();
			}
			GUI.EndScrollView();
			GUI.EndGroup();
			UpdateAvailableApparel();
		}

		private int DrawBaseOutfit(int x, int y)
		{
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Expected O, but got Unknown
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Expected O, but got Unknown
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			//IL_012b: Expected O, but got Unknown
			if (pawn != null && outfitTracker != null && customOutfit != null)
			{
				string text = ((customOutfit.Outfit == null) ? "No Base Outfit" : ((Policy)customOutfit.Outfit).label);
				if (Widgets.ButtonText(new Rect((float)x, (float)y, 100f, 30f), text, true, true, true, (TextAnchor?)null))
				{
					List<FloatMenuOption> list = new List<FloatMenuOption>();
					list.Add(new FloatMenuOption("No Base Outfit", (Action)delegate
					{
						customOutfit.Outfit = null;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					foreach (ApparelPolicy o in Current.Game.outfitDatabase.AllOutfits)
					{
						list.Add(new FloatMenuOption(((Policy)o).label, (Action)delegate
						{
							customOutfit.Outfit = o;
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					}
					Find.WindowStack.Add((Window)new FloatMenu(list));
				}
				return x + 110;
			}
			return x;
		}

		private int DrawPawnSelection(int x, int y)
		{
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b0: Expected O, but got Unknown
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Expected O, but got Unknown
			string text = ((pawn != null) ? pawn.Name.ToStringShort : "Select Pawn");
			if (Widgets.ButtonText(new Rect((float)x, (float)y, 100f, 30f), text, true, true, true, (TextAnchor?)null))
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				foreach (Pawn p in selectablePawns)
				{
					list.Add(new FloatMenuOption(p.Name.ToStringShort, (Action)delegate
					{
						if (customOutfit != null)
						{
							customOutfit = null;
						}
						pawn = p;
						if (!WorldComp.PawnOutfits.TryGetValue(pawn, out outfitTracker))
						{
							outfitTracker = new PawnOutfitTracker(pawn);
							WorldComp.PawnOutfits.Add(pawn, outfitTracker);
						}
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
				}
				Find.WindowStack.Add((Window)new FloatMenu(list));
			}
			return x + 110;
		}

		private int DrawOutfitSelection(int x, int y)
		{
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Expected O, but got Unknown
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f5: Expected O, but got Unknown
			if (outfitTracker != null && outfitTracker.CustomOutfits.Count > 0)
			{
				string text = ((customOutfit != null) ? customOutfit.Label : "Select Outfit");
				if (Widgets.ButtonText(new Rect((float)x, (float)y, 150f, 30f), text, true, true, true, (TextAnchor?)null))
				{
					List<FloatMenuOption> list = new List<FloatMenuOption>();
					foreach (CustomOutfit o in outfitTracker.CustomOutfits)
					{
						list.Add(new FloatMenuOption(o.Label, (Action)delegate
						{
							customOutfit = o;
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					}
					Find.WindowStack.Add((Window)new FloatMenu(list));
				}
				return x + 160;
			}
			return x;
		}

		private int DrawOutfitDelete(int x, int y)
		{
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Expected O, but got Unknown
			//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e0: Expected O, but got Unknown
			if (outfitTracker != null && outfitTracker.CustomOutfits.Count > 0)
			{
				string text = "Delete Outfit";
				if (Widgets.ButtonText(new Rect((float)x, (float)y, 150f, 30f), text, true, true, true, (TextAnchor?)null))
				{
					List<FloatMenuOption> list = new List<FloatMenuOption>();
					foreach (CustomOutfit o in outfitTracker.CustomOutfits)
					{
						list.Add(new FloatMenuOption(o.Label, (Action)delegate
						{
							if (o != null)
							{
								outfitTracker.Remove((IDresserOutfit)o);
								customOutfit = null;
								UpdateAvailableApparel();
							}
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					}
					Find.WindowStack.Add((Window)new FloatMenu(list));
				}
				return x + 160;
			}
			return x;
		}

		private int DrawUseInBattle(int x, int y)
		{
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			if (pawn != null && outfitTracker != null && customOutfit != null)
			{
				Widgets.Label(new Rect((float)x, (float)y, 100f, 30f), Translator.Translate("ChangeDresser.UseForBattle"));
				bool flag = customOutfit.OutfitType == OutfitType.Battle;
				bool flag2 = flag;
				Widgets.Checkbox((float)(x + 110), (float)y, ref flag, 24f, false, false, (Texture2D)null, (Texture2D)null);
				if (flag != flag2)
				{
					customOutfit.OutfitType = ((!flag) ? OutfitType.Civilian : OutfitType.Battle);
				}
				return x + 150;
			}
			return x;
		}

		public void UpdateAvailableApparel()
		{
			availableApparel.Clear();
			WorldComp.InvalidateCachedCustomApparel();
			HashSet<Apparel> cachedCustomApparel = WorldComp.CachedCustomApparel;
			foreach (Apparel item in Dresser.Apparel)
			{
				if (!cachedCustomApparel.Contains(item))
				{
					availableApparel.Add(item);
				}
			}
		}

		private void AddApparelToOutfit(Apparel apparel)
		{
			customOutfit.Apparel.Add(apparel);
		}

		private void RemoveApparelFromOutfit(Apparel apparel)
		{
			customOutfit.Apparel.Remove(apparel);
		}

		private bool CanWear(Apparel apparel)
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			if ((CompBiocodable.IsBiocoded((Thing)(object)apparel) && !CompBiocodable.IsBiocodedFor((Thing)(object)apparel, pawn)) || !ApparelUtility.HasPartsToWear(pawn, ((Thing)apparel).def) || !DevelopmentalStageExtensions.Has(((Thing)apparel).def.apparel.developmentalStageFilter, pawn.DevelopmentalStage))
			{
				return false;
			}
			foreach (Apparel item in customOutfit.Apparel)
			{
				if (!ApparelUtility.CanWearTogether(((Thing)item).def, ((Thing)apparel).def, pawn.RaceProps.body))
				{
					return false;
				}
			}
			return true;
		}
	}
	public struct StatDefValue(StatDef def, float max, float value = 0f)
	{
		public readonly StatDef Def = def;

		public float Value = value;

		public readonly float Max = max;
	}
	public class CDApparelFilters
	{
		public string Name = "";

		public ApparelLayerDef Layer;

		public string LayerString;

		public QualityRange Quality = QualityRange.All;

		public string QualityString;

		public float HP;

		public readonly List<StatDefValue> StatDefs = new List<StatDefValue>();

		public CDApparelFilters()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			LayerString = GetLayerLabel(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.All")));
			QualityString = GetQualityLabel(TaggedString.op_Implicit(Translator.Translate("ChangeDresser.All")));
			StatDefs.Add(new StatDefValue(StatDefOf.ArmorRating_Blunt, 2f));
			StatDefs.Add(new StatDefValue(StatDefOf.ArmorRating_Sharp, 2f));
			StatDefs.Add(new StatDefValue(StatDefOf.ArmorRating_Heat, 2f));
			StatDefs.Add(new StatDefValue(StatDefOf.Insulation_Cold, 20f));
			StatDefs.Add(new StatDefValue(StatDefOf.Insulation_Heat, 20f));
			StatDefs.Add(new StatDefValue(StatDefOf.CarryingCapacity, 20f));
			StatDefs.Add(new StatDefValue(StatDefOf.GlobalLearningFactor, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.MeleeDodgeChance, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.MeleeHitChance, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.MoveSpeed, 10f));
			StatDefs.Add(new StatDefValue(StatDefOf.WorkSpeedGlobal, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.AimingDelayFactor, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.AnimalGatherSpeed, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.AnimalGatherYield, 2f));
			StatDefs.Add(new StatDefValue(StatDefOf.MiningSpeed, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.MiningYield, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.PlantWorkSpeed, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.PlantHarvestYield, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.PsychicSensitivity, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.SocialImpact, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.TameAnimalChance, 1f));
			StatDefs.Add(new StatDefValue(StatDefOf.TrainAnimalChance, 1f));
		}

		public bool IncludeAppareL(Apparel a)
		{
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			if (Name.Length > 0 && ((Entity)a).Label.ToLower().IndexOf(Name) == -1 && ((Def)((Thing)a).def).defName.ToLower().IndexOf(Name) == -1)
			{
				return false;
			}
			if (Layer != null && !((Thing)a).def.apparel.layers.Contains(Layer))
			{
				return false;
			}
			QualityCategory val = default(QualityCategory);
			if (Quality != QualityRange.All && QualityUtility.TryGetQuality((Thing)(object)a, ref val) && (Quality.min > val || Quality.max < val))
			{
				return false;
			}
			if (HP > 0f)
			{
				float num = (float)((Thing)a).HitPoints / (float)((Thing)a).MaxHitPoints;
				float num2 = HP * 0.01f;
				if (num < num2)
				{
					return false;
				}
			}
			foreach (StatDefValue statDef in StatDefs)
			{
				if (statDef.Value > 0f)
				{
					float num3 = Math.Abs(StatExtension.GetStatValue((Thing)(object)a, statDef.Def, true, -1));
					if (statDef.Value > num3)
					{
						return false;
					}
				}
			}
			return true;
		}

		public string GetLayerLabel(string layer)
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("Layer") + ": " + layer);
		}

		public string GetQualityLabel(string q)
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("Quality") + ": " + q);
		}
	}
	public class PawnColumnDef_AssignOutfit : PawnColumnDef
	{
		public ApparelPolicy apparelPolicy;
	}
	[StaticConstructorOnStartup]
	public class PawnColumnWorker_AssignOutfit : PawnColumnWorker_Checkbox
	{
		private Vector2 cachedOutfitLabelSize;

		private static readonly Texture2D SortingIcon = ContentFinder<Texture2D>.Get("UI/Icons/Sorting", true);

		private static readonly Texture2D SortingDescendingIcon = ContentFinder<Texture2D>.Get("UI/Icons/SortingDescending", true);

		public ApparelPolicy ApparelPolicy => ((PawnColumnDef_AssignOutfit)(object)((PawnColumnWorker)this).def).apparelPolicy;

		public void DoHeaderCheckon(Rect rect, PawnTable table)
		{
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			bool headValue = GetHeadValue();
			bool flag = headValue;
			float num = 24f;
			float num2 = ((Rect)(ref rect)).x + ((Rect)(ref rect)).width / 2f - num / 2f;
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(num2, ((Rect)(ref rect)).y, num, num);
			Widgets.Checkbox(num2, ((Rect)(ref rect)).y, ref headValue, 24f, false, false, (Texture2D)null, (Texture2D)null);
			if (Mouse.IsOver(val))
			{
				string text = TaggedString.op_Implicit(Translator.Translate("ChangeDresser.UseForBattle"));
				if (!GenText.NullOrEmpty(text))
				{
					TooltipHandler.TipRegion(val, TipSignal.op_Implicit(text));
				}
			}
			if (headValue != flag)
			{
				SetHeadValue(headValue, table);
			}
		}

		public override void DoHeader(Rect rect, PawnTable table)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0131: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			((PawnColumnWorker_Checkbox)this).DoHeader(rect, table);
			DoHeaderCheckon(rect, table);
			Text.Font = (GameFont)1;
			if (cachedOutfitLabelSize == default(Vector2))
			{
				cachedOutfitLabelSize = Text.CalcSize(GenText.CapitalizeFirst(((Policy)((PawnColumnDef_AssignOutfit)(object)((PawnColumnWorker)this).def).apparelPolicy).label));
			}
			Rect labelRect = GetLabelRect(rect);
			MouseoverSounds.DoRegion(labelRect);
			Text.Anchor = (TextAnchor)4;
			Widgets.Label(labelRect, GenText.CapitalizeFirst(((Policy)((PawnColumnDef_AssignOutfit)(object)((PawnColumnWorker)this).def).apparelPolicy).label));
			GUI.color = new Color(1f, 1f, 1f, 0.3f);
			Widgets.DrawLineVertical(((Rect)(ref labelRect)).center.x, ((Rect)(ref labelRect)).yMax - 3f, (float)((double)((Rect)(ref rect)).y + 50.0 - (double)((Rect)(ref labelRect)).yMax + 3.0));
			Widgets.DrawLineVertical(((Rect)(ref labelRect)).center.x + 1f, ((Rect)(ref labelRect)).yMax - 3f, (float)((double)((Rect)(ref rect)).y + 50.0 - (double)((Rect)(ref labelRect)).yMax + 3.0));
			GUI.color = Color.white;
			Text.Anchor = (TextAnchor)0;
		}

		private Rect GetLabelRect(Rect headerRect)
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			Rect result = default(Rect);
			((Rect)(ref result))..ctor(((Rect)(ref headerRect)).center.x - cachedOutfitLabelSize.x / 2f, ((Rect)(ref headerRect)).y, cachedOutfitLabelSize.x, cachedOutfitLabelSize.y);
			((Rect)(ref result)).y = ((Rect)(ref result)).y + 28f;
			if (((PawnColumnWorker)this).def.moveWorkTypeLabelDown)
			{
				((Rect)(ref result)).y = ((Rect)(ref result)).y + 20f;
			}
			return result;
		}

		protected override Rect GetInteractableHeaderRect(Rect headerRect, PawnTable table)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			return GetLabelRect(headerRect);
		}

		public override int GetMinHeaderHeight(PawnTable table)
		{
			return 78;
		}

		public override int GetMinWidth(PawnTable table)
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			Text.Font = (GameFont)1;
			return Mathf.Max(Mathf.CeilToInt(Text.CalcSize(GenText.CapitalizeFirst(((Policy)((PawnColumnDef_AssignOutfit)(object)((PawnColumnWorker)this).def).apparelPolicy).label)).x) / 2 + 10, ((PawnColumnWorker_Checkbox)this).GetMinWidth(table));
		}

		public override int GetOptimalWidth(PawnTable table)
		{
			return Mathf.Clamp(39, ((PawnColumnWorker)this).GetMinWidth(table), ((PawnColumnWorker)this).GetMaxWidth(table));
		}

		public override int GetMaxWidth(PawnTable table)
		{
			return Mathf.Min(((PawnColumnWorker_Checkbox)this).GetMaxWidth(table), 80);
		}

		protected override bool HasCheckbox(Pawn pawn)
		{
			return true;
		}

		protected override bool GetValue(Pawn pawn)
		{
			if (WorldComp.PlayFunctionPawnOutfits.TryGetValue(pawn, out var value))
			{
				return value.Contains(ApparelPolicy);
			}
			return false;
		}

		protected override void SetValue(Pawn pawn, bool value, PawnTable table)
		{
			if (WorldComp.PlayFunctionPawnOutfits.TryGetValue(pawn, out var value2))
			{
				HandleOutfitAssign(value, ApparelPolicy, value2);
			}
		}

		protected bool GetHeadValue()
		{
			ApparelPolicy apparelPolicy = ((PawnColumnDef_AssignOutfit)(object)((PawnColumnWorker)this).def).apparelPolicy;
			return WorldComp.OutfitsForBattle.Contains(apparelPolicy);
		}

		protected void SetHeadValue(bool value, PawnTable table)
		{
			ApparelPolicy apparelPolicy = ((PawnColumnDef_AssignOutfit)(object)((PawnColumnWorker)this).def).apparelPolicy;
			if (value)
			{
				WorldComp.OutfitsForBattle.Add(apparelPolicy);
			}
			else
			{
				WorldComp.OutfitsForBattle.Remove(apparelPolicy);
			}
			foreach (PawnOutfitTracker value2 in WorldComp.PawnOutfits.Values)
			{
				value2.UpdateOutfitType(apparelPolicy, (!value) ? OutfitType.Civilian : OutfitType.Battle);
			}
		}

		private void HandleOutfitAssign(bool assign, ApparelPolicy outfit, PawnOutfitTracker po)
		{
			Pawn pawn = po.Pawn;
			if (assign)
			{
				po.DefinedOutfits.Add(new DefinedOutfit(outfit, WorldComp.GetOutfitType(outfit)));
				return;
			}
			po.Remove(outfit);
			if (!((object)pawn.outfits.CurrentApparelPolicy).Equals((object?)outfit))
			{
				return;
			}
			if (!((!pawn.Drafted) ? (!po.ChangeToCivilianOutfit()) : (!po.ChangeToBattleOutfit())))
			{
				Messages.Message(pawn.Name.ToStringShort + " will no longer wear " + ((Policy)outfit).label + ". Could not find another Outfit for them to wear. Please fix this manually.", MessageTypeDefOf.CautionInput, true);
				return;
			}
			IDresserOutfit currentOutfit = po.CurrentOutfit;
			if (currentOutfit != null)
			{
				Messages.Message(pawn.Name.ToStringShort + " will no longer wear " + ((Policy)outfit).label + " and will instead be assigned to wear " + currentOutfit.Label, MessageTypeDefOf.CautionInput, true);
			}
			else
			{
				Messages.Message(pawn.Name.ToStringShort + " will no longer wear " + ((Policy)outfit).label + " but could not be assigned anything else to wear.", MessageTypeDefOf.CautionInput, true);
			}
		}
	}
	public class PawnColumnWorker_LabelWithCustomHead : PawnColumnWorker_Label
	{
		public override void DoHeader(Rect rect, PawnTable table)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			Text.Anchor = (TextAnchor)5;
			Widgets.Label(new Rect(0f, 0f, ((Rect)(ref rect)).width, 28f), Translator.Translate("ChangeDresser.UseForBattle"));
			((PawnColumnWorker)this).DoHeader(new Rect(0f, 28f, ((Rect)(ref rect)).width, ((Rect)(ref rect)).height - 28f), table);
		}
	}
	public class PawnTable_AssignOutfit : PawnTable
	{
		protected override IEnumerable<Pawn> LabelSortFunction(IEnumerable<Pawn> input)
		{
			return PlayerPawnsDisplayOrderUtility.InOrder(input);
		}

		public PawnTable_AssignOutfit(PawnTableDef def, Func<IEnumerable<Pawn>> pawnsGetter, int uiWidth, int uiHeight)
			: base(def, pawnsGetter, uiWidth, uiHeight)
		{
		}
	}
	internal class StorageUI : Window
	{
		private Building_Dresser Dresser;

		private Pawn Pawn;

		private string Filter = "";

		private float height = 2f;

		private Vector2 scrollPosLeft = new Vector2(0f, 0f);

		private Vector2 scrollPosRight = new Vector2(0f, 0f);

		private string SelectionLabel = "";

		private PawnSelectionWidgetDTO pawnSelections;

		private List<Apparel> cachedApparel;

		private bool showMapApparels = true;

		private List<Apparel> CachedApparel
		{
			get
			{
				if (cachedApparel == null)
				{
					if (showMapApparels)
					{
						cachedApparel = new List<Apparel>(Dresser.Apparel);
					}
					else
					{
						cachedApparel = new List<Apparel>(Dresser.StoredApparel);
					}
				}
				return cachedApparel;
			}
		}

		public override Vector2 InitialSize => new Vector2(750f, 700f);

		public StorageUI(Pawn pawn)
			: base((IWindowDrawing)null)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			Dresser = null;
			Pawn = pawn;
			base.closeOnClickedOutside = true;
			base.doCloseButton = true;
			base.doCloseX = true;
			base.absorbInputAroundWindow = true;
			base.forcePause = true;
			CreatePawnSelection();
		}

		public StorageUI(Building_Dresser dresser, Pawn pawn)
			: base((IWindowDrawing)null)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			Dresser = dresser;
			Pawn = pawn;
			base.closeOnClickedOutside = true;
			base.doCloseButton = true;
			base.doCloseX = true;
			base.absorbInputAroundWindow = true;
			base.forcePause = true;
			CreatePawnSelection();
		}

		private void CreatePawnSelection()
		{
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			if (Pawn == null)
			{
				pawnSelections = null;
				return;
			}
			if (Pawn.IsPrisoner)
			{
				SelectionLabel = TaggedString.op_Implicit(Translator.Translate("TabPrisoner"));
				pawnSelections = new PawnSelectionWidgetDTO(new List<Pawn>(PawnsFinder.AllMaps_PrisonersOfColony));
				if (!pawnSelections.SetSelectedPawn(Pawn))
				{
					Log.Error("unable to find prisoner " + Pawn.Name.ToStringShort);
				}
			}
			else
			{
				SelectionLabel = "";
				List<Pawn> list = new List<Pawn>();
				foreach (Pawn item in PawnsFinder.AllMaps_FreeColonistsSpawned)
				{
					if (((Thing)item).Faction == Faction.OfPlayer && item.RaceProps.Humanlike)
					{
						list.Add(item);
					}
				}
				pawnSelections = new PawnSelectionWidgetDTO(list);
				if (!pawnSelections.SetSelectedPawn(Pawn))
				{
					Log.Error("unable to find colonist " + Pawn.Name.ToStringShort);
				}
			}
			pawnSelections.SelectionChangeListener += OnPawnChange;
		}

		private void OnPawnChange(object sender)
		{
			object selectedItem = pawnSelections.SelectedItem;
			Pawn = (Pawn)((selectedItem is Pawn) ? selectedItem : null);
		}

		public override void PreClose()
		{
			((Window)this).PreClose();
			if (pawnSelections != null)
			{
				pawnSelections.SelectionChangeListener -= OnPawnChange;
			}
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_0969: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_012c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0178: Unknown result type (might be due to invalid IL or missing references)
			//IL_0182: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01db: Unknown result type (might be due to invalid IL or missing references)
			//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0241: Unknown result type (might be due to invalid IL or missing references)
			//IL_025c: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0299: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a3: Expected O, but got Unknown
			//IL_0419: Unknown result type (might be due to invalid IL or missing references)
			//IL_0423: Unknown result type (might be due to invalid IL or missing references)
			//IL_030b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0549: Unknown result type (might be due to invalid IL or missing references)
			//IL_0553: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_036b: Unknown result type (might be due to invalid IL or missing references)
			//IL_04a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_05d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_05db: Unknown result type (might be due to invalid IL or missing references)
			//IL_05dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_05e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_05e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_05ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_05ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_05f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_060a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0688: Unknown result type (might be due to invalid IL or missing references)
			//IL_079f: Unknown result type (might be due to invalid IL or missing references)
			//IL_080e: Unknown result type (might be due to invalid IL or missing references)
			//IL_083b: Unknown result type (might be due to invalid IL or missing references)
			//IL_07dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_07e6: Expected O, but got Unknown
			//IL_0704: Unknown result type (might be due to invalid IL or missing references)
			//IL_070f: Unknown result type (might be due to invalid IL or missing references)
			//IL_077c: Unknown result type (might be due to invalid IL or missing references)
			//IL_071f: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Text.Font = (GameFont)2;
				Text.Anchor = (TextAnchor)4;
				Widgets.Label(new Rect(0f, 0f, 200f, 50f), Translator.Translate("ChangeDresser.ApparelStorageLabel"));
				Text.Font = (GameFont)1;
				Text.Anchor = (TextAnchor)3;
				Filter = Widgets.TextArea(new Rect(250f, 2f, 150f, 32f), Filter, false);
				Text.Anchor = (TextAnchor)4;
				if (Widgets.ButtonText(new Rect(425f, 2f, 250f, 32f), TaggedString.op_Implicit(showMapApparels ? Translator.Translate("ShowThisDresserApparel") : Translator.Translate("ShowMapApparel")), true, true, true, (TextAnchor?)null))
				{
					showMapApparels = !showMapApparels;
					cachedApparel?.Clear();
					cachedApparel = null;
				}
				float num = 70f;
				if (pawnSelections != null)
				{
					WidgetUtil.AddSelectorWidget(150f, 60f, ((Rect)(ref inRect)).width - 300f, SelectionLabel, pawnSelections);
					num += 30f;
				}
				Text.Font = (GameFont)1;
				GUI.color = Color.white;
				Pawn pawn = Pawn;
				List<Apparel> list = ((pawn != null) ? pawn.apparel.WornApparel : null);
				float num2 = ((Rect)(ref inRect)).width * 0.5f - 10f;
				Rect val = default(Rect);
				Rect val2 = default(Rect);
				if (list != null)
				{
					GUI.Label(new Rect(0f, num, 100f, 30f), TaggedString.op_Implicit(Translator.Translate("ChangeDresser.Worn")), WidgetUtil.MiddleCenter);
					((Rect)(ref val))..ctor(0f, num + 30f, num2, 400f);
					((Rect)(ref val2))..ctor(0f, 0f, num2 - 16f, (float)list.Count * 40f);
					GUI.BeginGroup(val);
					scrollPosLeft = GUI.BeginScrollView(new Rect(GenUI.AtZero(val)), scrollPosLeft, val2);
					GUI.color = Color.white;
					Text.Font = (GameFont)2;
					Rect val4 = default(Rect);
					Rect val5 = default(Rect);
					for (int i = 0; i < list.Count; i++)
					{
						Apparel val3 = list[i];
						((Rect)(ref val4))..ctor(0f, 2f + (float)i * 40f, ((Rect)(ref val)).width, 40f);
						GUI.BeginGroup(val4);
						Widgets.ThingIcon(new Rect(0f, 0f, 40f, 40f), (Thing)(object)val3, 1f, (Rot4?)null, false, 1f, false);
						if (Widgets.InfoCardButton(40f, 0f, (Thing)(object)val3))
						{
							Find.WindowStack.Add((Window)new Dialog_InfoCard((Thing)(object)val3, (Precept_ThingStyle)null));
						}
						Text.Font = (GameFont)1;
						Widgets.Label(new Rect(75f, 0f, ((Rect)(ref val4)).width - 110f, 40f), ((Entity)val3).Label);
						GUI.color = Color.white;
						((Rect)(ref val5))..ctor(((Rect)(ref val4)).width - 35f, 10f, 20f, 20f);
						if (Dresser == null)
						{
							if (Widgets.ButtonImage(val5, WidgetUtil.dropTexture, true, (string)null))
							{
								if (!Pawn.apparel.TryDrop(val3))
								{
									Log.Error("Problem dropping " + ((Entity)val3).Label);
								}
								GUI.EndGroup();
								break;
							}
						}
						else if (((Building_Storage)Dresser).settings.filter.Allows((Thing)(object)val3))
						{
							if (Widgets.ButtonImage(val5, WidgetUtil.nextTexture, true, (string)null))
							{
								Pawn.apparel.Remove(val3);
								WorldComp.StoreApparel(val3, ((Thing)Pawn).Map);
								cachedApparel.Clear();
								cachedApparel = null;
								GUI.EndGroup();
								break;
							}
						}
						else
						{
							Widgets.ButtonImage(val5, WidgetUtil.cantTexture, true, (string)null);
						}
						GUI.EndGroup();
					}
					GUI.EndScrollView();
					GUI.EndGroup();
					if (Pawn.apparel.WornApparel.Count > 0)
					{
						if (Widgets.ButtonText(new Rect(0f, 450f, 200f, 30f), TaggedString.op_Implicit(Translator.Translate("DesignatorStrip")), true, true, true, (TextAnchor?)null))
						{
							Stack<Apparel> stack = new Stack<Apparel>(Pawn.apparel.WornApparel);
							Thing val7 = default(Thing);
							while (stack.Count > 0)
							{
								Apparel val6 = stack.Pop();
								Pawn.apparel.Remove(val6);
								if (!WorldComp.StoreApparel(val6, ((Thing)Pawn).Map) && (((Thing)Pawn).Map == null || !GenDrop.TryDropSpawn((Thing)(object)val6, ((Thing)Pawn).Position, ((Thing)Pawn).Map, (ThingPlaceMode)1, ref val7, (Action<Thing, int>)null, (Predicate<IntVec3>)null, true)))
								{
									Log.Error("failed to drop apparel " + ((Entity)val6).Label);
									Pawn.apparel.Wear(val6, true, false);
								}
							}
						}
						cachedApparel?.Clear();
						cachedApparel = null;
					}
				}
				if (Dresser == null)
				{
					return;
				}
				GUI.Label(new Rect((list == null) ? 0f : (((Rect)(ref inRect)).width * 0.5f), num, (list == null) ? ((Rect)(ref inRect)).width : 100f, 30f), TaggedString.op_Implicit(Translator.Translate("ChangeDresser.Storage")), WidgetUtil.MiddleCenter);
				float num3 = ((list == null) ? 0f : (((Rect)(ref inRect)).width - num2));
				float num4 = ((list == null) ? ((Rect)(ref inRect)).width : num2);
				((Rect)(ref val))..ctor(num3, num + 30f, num4, ((Rect)(ref inRect)).height - 160f);
				((Rect)(ref val2))..ctor(0f, 0f, num4 - 16f, height);
				height = 2f;
				GUI.BeginGroup(val);
				scrollPosRight = GUI.BeginScrollView(new Rect(GenUI.AtZero(val)), scrollPosRight, val2);
				string text = Filter.Trim().ToLower();
				GUI.color = Color.white;
				Text.Font = (GameFont)2;
				Rect val9 = default(Rect);
				Rect val10 = default(Rect);
				for (int j = 0; j < CachedApparel.Count; j++)
				{
					Apparel val8 = cachedApparel[j];
					if (text != "" && ((Entity)val8).Label.ToLower().IndexOf(text) == -1)
					{
						continue;
					}
					((Rect)(ref val9))..ctor(0f, height, ((Rect)(ref val2)).width, 40f);
					height += 40f;
					GUI.BeginGroup(val9);
					if (Pawn != null)
					{
						((Rect)(ref val10))..ctor(5f, 10f, 20f, 20f);
						if (Pawn.apparel.CanWearWithoutDroppingAnything(((Thing)val8).def) & ((!CompBiocodable.IsBiocoded((Thing)(object)val8) || CompBiocodable.IsBiocodedFor((Thing)(object)val8, Pawn)) && ApparelUtility.HasPartsToWear(Pawn, ((Thing)val8).def) && DevelopmentalStageExtensions.Has(((Thing)val8).def.apparel.developmentalStageFilter, Pawn.DevelopmentalStage)))
						{
							if (Widgets.ButtonImage(val10, WidgetUtil.previousTexture, true, (string)null))
							{
								cachedApparel.Clear();
								cachedApparel = null;
								if (WorldComp.PawnOutfits.TryGetValue(Pawn, out var value))
								{
									value.ApplyApparelColor(val8);
								}
								Pawn.apparel.Wear(val8, true, false);
								GUI.EndGroup();
								break;
							}
						}
						else
						{
							Widgets.ButtonImage(val10, WidgetUtil.cantTexture, true, (string)null);
						}
					}
					Widgets.ThingIcon(new Rect(35f, 0f, 40f, 40f), (Thing)(object)val8, 1f, (Rot4?)null, false, 1f, false);
					if (Widgets.InfoCardButton(75f, 0f, (Thing)(object)val8))
					{
						Find.WindowStack.Add((Window)new Dialog_InfoCard((Thing)(object)val8, (Precept_ThingStyle)null));
					}
					Text.Font = (GameFont)1;
					Text.Anchor = (TextAnchor)4;
					Widgets.Label(new Rect(85f, 0f, ((Rect)(ref val9)).width - 110f, 40f), ((Entity)val8).Label);
					if (Widgets.ButtonImage(new Rect(((Rect)(ref val9)).width - 45f, 0f, 20f, 20f), WidgetUtil.dropTexture, true, (string)null))
					{
						if (BuildingUtil.DropThing((Thing)(object)val8, (Building)(object)Dresser, ((Thing)Dresser).Map, makeForbidden: false))
						{
							cachedApparel.Clear();
							cachedApparel = null;
							GUI.EndGroup();
							break;
						}
						Log.Error("Problem dropping " + ((Entity)val8).Label);
					}
					GUI.EndGroup();
				}
				GUI.EndScrollView();
				GUI.EndGroup();
			}
			catch (Exception ex)
			{
				Log.Error(((object)this).GetType().Name + " closed due to: " + ex.GetType().Name + " " + ex.Message);
				Messages.Message(((object)this).GetType().Name + " closed due to: " + ex.GetType().Name + " " + ex.Message, MessageTypeDefOf.RejectInput, true);
				((Window)this).Close(true);
			}
			finally
			{
				Text.Anchor = (TextAnchor)0;
				GUI.color = Color.white;
			}
		}
	}
}
namespace ChangeDresser.UI.Util
{
	internal struct HSL
	{
		public float h;

		public float s;

		public float l;

		public HSL(HSL hsl)
		{
			h = hsl.h;
			s = hsl.s;
			l = hsl.l;
		}

		public HSL(float h = 0f, float s = 0f, float l = 0f)
		{
			this.h = h;
			this.s = s;
			this.l = l;
		}

		public override bool Equals(object obj)
		{
			if (obj != null && GetType() == obj.GetType())
			{
				HSL hSL = (HSL)obj;
				if (h == hSL.h && s == hSL.s)
				{
					return l == hSL.l;
				}
				return false;
			}
			return false;
		}

		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder("[");
			stringBuilder.Append(h);
			stringBuilder.Append(",");
			stringBuilder.Append(s);
			stringBuilder.Append(",");
			stringBuilder.Append(l);
			stringBuilder.Append("]");
			return stringBuilder.ToString();
		}

		public override int GetHashCode()
		{
			return ToString().GetHashCode();
		}

		public static bool operator ==(HSL l, HSL r)
		{
			if (l.h == r.h && l.s == r.s)
			{
				return l.l == r.l;
			}
			return false;
		}

		public static bool operator !=(HSL l, HSL r)
		{
			if (l.h == r.h && l.s == r.s)
			{
				return l.l != r.l;
			}
			return true;
		}
	}
	internal delegate void SelectionChangeListener(object sender);
	internal delegate void UpdatePawnListener(object sender, object value);
	internal delegate void ClearColorLayers();
	[StaticConstructorOnStartup]
	internal static class WidgetUtil
	{
		public static Texture2D nextTexture;

		public static Texture2D previousTexture;

		public static Texture2D cantTexture;

		public static Texture2D colorPickerTexture;

		public static Texture2D copyIconTexture;

		public static Texture2D pasteIconTexture;

		public static Texture2D dropTexture;

		public static Texture2D colorFinder;

		public static Texture2D noneTexture;

		public static Texture2D yesSellTexture;

		public static Texture2D noSellTexture;

		public static Texture2D yesDressFromTexture;

		public static Texture2D noDressFromTexture;

		public static Texture2D manageapparelTexture;

		public static Texture2D assignweaponsTexture;

		public static Texture2D customapparelTexture;

		public static Texture2D emptyTexture;

		public static Texture2D collectTexture;

		public static readonly Vector2 NavButtonSize;

		public static readonly Vector2 ButtonSize;

		public static readonly Vector2 PortraitSize;

		private static Vector2 scrollPos;

		private static Vector2 hairScrollPos;

		private static readonly Texture2D ColorPreset;

		private static readonly Texture2D LayerColor;

		private static GUIStyle middleCenterGuiStyle;

		public static float SelectionRowHeight => NavButtonSize.y;

		public static GUIStyle MiddleCenter
		{
			get
			{
				if (middleCenterGuiStyle == null)
				{
					middleCenterGuiStyle = GUI.skin.label;
					middleCenterGuiStyle.alignment = (TextAnchor)4;
				}
				return middleCenterGuiStyle;
			}
		}

		static WidgetUtil()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Expected O, but got Unknown
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Expected O, but got Unknown
			//IL_01db: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e5: Expected O, but got Unknown
			NavButtonSize = new Vector2(30f, 30f);
			ButtonSize = new Vector2(150f, 30f);
			PortraitSize = new Vector2(192f, 192f);
			scrollPos = new Vector2(0f, 0f);
			hairScrollPos = new Vector2(0f, 0f);
			ColorPreset = new Texture2D(20, 20);
			LayerColor = new Texture2D(50, 30);
			middleCenterGuiStyle = null;
			nextTexture = ContentFinder<Texture2D>.Get("UI/next", true);
			previousTexture = ContentFinder<Texture2D>.Get("UI/previous", true);
			cantTexture = ContentFinder<Texture2D>.Get("UI/x", true);
			colorPickerTexture = ContentFinder<Texture2D>.Get("UI/colorpicker", true);
			copyIconTexture = ContentFinder<Texture2D>.Get("UI/copy", true);
			pasteIconTexture = ContentFinder<Texture2D>.Get("UI/paste", true);
			dropTexture = ContentFinder<Texture2D>.Get("UI/drop", true);
			noneTexture = ContentFinder<Texture2D>.Get("UI/none", true);
			yesSellTexture = ContentFinder<Texture2D>.Get("UI/yessell", true);
			noSellTexture = ContentFinder<Texture2D>.Get("UI/nosell", true);
			yesDressFromTexture = ContentFinder<Texture2D>.Get("UI/dressfrom", true);
			noDressFromTexture = ContentFinder<Texture2D>.Get("UI/nodressfrom", true);
			manageapparelTexture = ContentFinder<Texture2D>.Get("UI/manageapparel", true);
			assignweaponsTexture = ContentFinder<Texture2D>.Get("UI/assignweapons", true);
			customapparelTexture = ContentFinder<Texture2D>.Get("UI/customapparel", true);
			emptyTexture = ContentFinder<Texture2D>.Get("UI/empty", true);
			collectTexture = ContentFinder<Texture2D>.Get("UI/collect", true);
			foreach (ModContentPack runningMod in LoadedModManager.RunningMods)
			{
				if (Object.op_Implicit((Object)(object)runningMod.GetContentHolder<Texture2D>().Get("UI/colorpicker")))
				{
					byte[] array = File.ReadAllBytes(runningMod.RootDir + "/Textures/UI/colorpicker.png");
					colorFinder = new Texture2D(2, 2, (TextureFormat)1, true);
					ImageConversion.LoadImage(colorFinder, array, false);
					break;
				}
			}
		}

		public static string AddNumberTextInput(float labelLeft, float top, float inputLeft, float inputWidth, string label, string value)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			GUI.color = Color.white;
			GUI.BeginGroup(new Rect(labelLeft, top, inputLeft + inputWidth, SelectionRowHeight));
			GUI.Label(new Rect(0f, 0f, inputLeft - 5f, SelectionRowHeight), label, MiddleCenter);
			Rect val = new Rect(inputLeft, 0f, inputWidth, SelectionRowHeight);
			string text = GUI.TextField(val, value, MiddleCenter).Trim();
			GUI.color = Color.grey;
			Widgets.DrawBox(val, 1, (Texture2D)null);
			GUI.EndGroup();
			if (text.Length > 0 && !Regex.IsMatch(text, "^[0-9]*$"))
			{
				text = value;
			}
			return text;
		}

		public static void AddSelectorWidget(float left, float top, float width, string label, ASelectionWidgetDTO selectionWidgetDto)
		{
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_0126: Unknown result type (might be due to invalid IL or missing references)
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(left, top, width, SelectionRowHeight);
			GUI.BeginGroup(val);
			GUI.color = Color.white;
			Text.Font = (GameFont)2;
			left = 0f;
			if (label != null)
			{
				GUI.Label(new Rect(0f, 0f, 75f, SelectionRowHeight), label, MiddleCenter);
				left = 80f;
			}
			Text.Anchor = (TextAnchor)4;
			if (GUI.Button(new Rect(left, 0f, NavButtonSize.x, NavButtonSize.y), (Texture)(object)previousTexture))
			{
				selectionWidgetDto.DecreaseIndex();
			}
			Rect val2 = new Rect(NavButtonSize.x + 5f + left, 0f, ((Rect)(ref val)).width - 2f * NavButtonSize.x - 10f - left, NavButtonSize.y);
			GUI.Label(val2, selectionWidgetDto.SelectedItemLabel, MiddleCenter);
			GUI.color = Color.grey;
			Widgets.DrawBox(val2, 1, (Texture2D)null);
			if (GUI.Button(new Rect(((Rect)(ref val)).width - NavButtonSize.x, 0f, NavButtonSize.x, NavButtonSize.y), (Texture)(object)nextTexture))
			{
				selectionWidgetDto.IncreaseIndex();
			}
			GUI.EndGroup();
			Text.Anchor = (TextAnchor)0;
		}

		private static Color GetColorFromTexture(Vector2 mousePosition, Rect rect, Texture2D texture)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			float num = mousePosition.x - ((Rect)(ref rect)).x;
			float num2 = mousePosition.y - ((Rect)(ref rect)).y;
			int num3 = (int)(num * ((float)((Texture)colorPickerTexture).width / (((Rect)(ref rect)).width + 0f)));
			int num4 = (int)((((Rect)(ref rect)).height - num2) * ((float)((Texture)colorPickerTexture).height / (((Rect)(ref rect)).height + 0f)));
			return texture.GetPixel(num3, num4);
		}

		private static string ColorConvert(float f)
		{
			try
			{
				int num = (int)(f * 255f);
				if (num > 255)
				{
					num = 255;
				}
				else if (num < 0)
				{
					num = 0;
				}
				return num.ToString();
			}
			catch
			{
				return "0";
			}
		}

		private static float ColorConvert(string intText)
		{
			try
			{
				float num = (float)int.Parse(intText) / 255f;
				if (num > 1f)
				{
					num = 1f;
				}
				else if (num < 0f)
				{
					num = 0f;
				}
				return num;
			}
			catch
			{
				return 0f;
			}
		}
	}
}
namespace ChangeDresser.UI.DTO.SelectionWidgetDTOs
{
	internal abstract class ASelectionWidgetDTO
	{
		protected int index;

		public abstract int Count { get; }

		public abstract string SelectedItemLabel { get; }

		public abstract object SelectedItem { get; }

		public event SelectionChangeListener SelectionChangeListener;

		public event UpdatePawnListener UpdatePawnListener;

		public void IncreaseIndex()
		{
			index++;
			if (index >= Count)
			{
				index = 0;
			}
			IndexChanged();
		}

		public void DecreaseIndex()
		{
			index--;
			if (index < 0)
			{
				index = Count - 1;
			}
			IndexChanged();
		}

		protected void IndexChanged()
		{
			if (this.SelectionChangeListener != null)
			{
				this.SelectionChangeListener(this);
			}
			UpdatePawn(SelectedItem);
		}

		protected void UpdatePawn(object item)
		{
			if (this.UpdatePawnListener != null)
			{
				this.UpdatePawnListener(this, item);
			}
		}

		public abstract void ResetToDefault();
	}
	internal class PawnSelectionWidgetDTO : ASelectionWidgetDTO
	{
		private readonly List<Pawn> Pawns;

		public override int Count => Pawns.Count;

		public override string SelectedItemLabel => ((object)Pawns[index]).ToString();

		public override object SelectedItem => Pawns[index];

		public PawnSelectionWidgetDTO(List<Pawn> pawns)
		{
			Pawns = pawns;
		}

		public bool SetSelectedPawn(Pawn pawn)
		{
			int num = 0;
			while (SelectedItem != pawn && num < Count)
			{
				IncreaseIndex();
				num++;
			}
			return SelectedItem == pawn;
		}

		public override void ResetToDefault()
		{
		}
	}
}
namespace ChangeDresser.DresserJobDriver
{
	internal class JobDriver_StoreApparel : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			yield return ToilFailConditions.FailOnDespawnedOrNull<Toil>(Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)2, false), (TargetIndex)1);
			yield return new Toil
			{
				initAction = delegate
				{
					//IL_000b: Unknown result type (might be due to invalid IL or missing references)
					Find.WindowStack.Add((Window)(object)new StorageUI((Building_Dresser)(object)(Thing)base.job.targetA, ((JobDriver)this).GetActor()));
				}
			};
		}
	}
}
