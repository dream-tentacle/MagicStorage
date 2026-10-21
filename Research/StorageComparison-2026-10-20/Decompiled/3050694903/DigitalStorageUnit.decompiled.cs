#define DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using DigitalStorageUnit._harmony;
using DigitalStorageUnit._harmony.rimfeller;
using DigitalStorageUnit.compat;
using DigitalStorageUnit.def;
using DigitalStorageUnit.extensions;
using DigitalStorageUnit.map;
using DigitalStorageUnit.ui;
using DigitalStorageUnit.util;
using HarmonyLib;
using IHoldMultipleThings;
using Microsoft.CodeAnalysis;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: AssemblyTitle("DigitalStorageUnit")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("DigitalStorageUnit")]
[assembly: AssemblyCopyright("Copyright ©  2023")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("DB5A2569-1F8A-4194-BF16-124863082D2D")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8.1", FrameworkDisplayName = ".NET Framework 4.8.1")]
[assembly: AssemblyVersion("1.0.0.0")]
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
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, AllowMultiple = false, Inherited = false)]
	internal sealed class NullableAttribute : Attribute
	{
		public readonly byte[] NullableFlags;

		public NullableAttribute(byte P_0)
		{
			NullableFlags = new byte[1] { P_0 };
		}

		public NullableAttribute(byte[] P_0)
		{
			NullableFlags = P_0;
		}
	}
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
namespace IHoldMultipleThings
{
	public interface IHoldMultipleThings
	{
		bool CapacityAt(Thing thing, IntVec3 storeCell, Map map, out int capacity);

		bool StackableAt(Thing thing, IntVec3 storeCell, Map map);
	}
}
namespace DigitalStorageUnit
{
	public class DataExtenderBuilding : Building
	{
		private DigitalStorageUnitBuilding _boundStorageUnit;

		private CompPowerTrader _compPowerTrader;

		public bool Powered
		{
			get
			{
				if (_compPowerTrader == null)
				{
					_compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
				}
				return _compPowerTrader.PowerOn;
			}
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).DeSpawn(mode);
			_boundStorageUnit = null;
			_boundStorageUnit?.Extenders.Remove(this);
			_compPowerTrader = null;
		}

		protected override void Tick()
		{
			((ThingWithComps)this).Tick();
			if (Gen.IsHashIntervalTick((Thing)(object)this, 60))
			{
				List<DigitalStorageUnitBuilding> list = ((Thing)this).Map.GetDsuComponent().DsuSet.Where((DigitalStorageUnitBuilding d) => RegionAndRoomQuery.GetRoom((Thing)(object)d, (RegionType)15) == RegionAndRoomQuery.GetRoom((Thing)(object)this, (RegionType)15)).ToList();
				DigitalStorageUnitBuilding digitalStorageUnitBuilding = ((list.Count == 1) ? list.FirstOrDefault() : null);
				if (digitalStorageUnitBuilding != _boundStorageUnit)
				{
					_boundStorageUnit?.Extenders.Remove(this);
					_boundStorageUnit = digitalStorageUnitBuilding;
					_boundStorageUnit?.Extenders.Add(this);
				}
			}
		}

		public override void DrawExtraSelectionOverlays()
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).DrawExtraSelectionOverlays();
			if (_boundStorageUnit != null)
			{
				GenDraw.DrawCircleOutline(GenThing.TrueCenter((Thing)(object)this), 0.4f, (SimpleColor)6);
				GenDraw.DrawLineBetween(GenThing.TrueCenter((Thing)(object)this), GenThing.TrueCenter((Thing)(object)_boundStorageUnit), (SimpleColor)6, 0.1f);
			}
		}
	}
	public class AccessPointPortBuilding : ABasePortDsuBuilding, IRemoveStorageInspectionTab
	{
		public override StorageIOMode IOMode => StorageIOMode.Output;

		public override Graphic Graphic => ((Thing)this).Graphic.GetColoredVersion(((Thing)this).Graphic.Shader, ((Def)((Thing)this).def).GetModExtension<ModExtensionPortColor>().outColor, Color.white);

		public override bool ForbidPawnInput => true;

		public void ProvideItem(Thing thing)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			if (base.BoundStorageUnit != null && base.Powered)
			{
				thing.Position = base.WorkPosition;
				IntVec3 workPosition = base.WorkPosition;
				FleckMaker.ThrowLightningGlow(((IntVec3)(ref workPosition)).ToVector3(), ((Thing)this).Map, 0.8f);
			}
		}

		protected override void Tick()
		{
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			if (!Gen.IsHashIntervalTick((Thing)(object)this, 10) || !base.Powered || base.BoundStorageUnit == null || !base.BoundStorageUnit.CanWork)
			{
				return;
			}
			foreach (Thing item in ((Thing)this).Map.thingGrid.ThingsListAt(base.WorkPosition).ToList())
			{
				if (item.def.EverStorable(false) && base.BoundStorageUnit.CanReceiveThing(item) && !((Thing)this).Map.reservationManager.AllReservedThings().Contains(item))
				{
					base.BoundStorageUnit.HandleNewItem(item);
				}
			}
		}
	}
	[StaticConstructorOnStartup]
	public class InputPortDsuBuilding : ABasePortDsuBuilding, IRemoveStorageInspectionTab
	{
		private PortPositionComp _portPosition;

		private readonly Dictionary<IntVec3, int> _tickCooldown = new Dictionary<IntVec3, int>();

		public override StorageIOMode IOMode => StorageIOMode.Input;

		public override Graphic Graphic => ((Thing)this).Graphic.GetColoredVersion(((Thing)this).Graphic.Shader, ((Def)((Thing)this).def).GetModExtension<ModExtensionPortColor>().inColor, Color.white);

		public override void Notify_ReceivedThing(Thing newItem)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			((Building_Storage)this).Notify_ReceivedThing(newItem);
			SuckFirstItem(newItem.Position);
		}

		public override void PostMake()
		{
			base.PostMake();
			if (_portPosition == null)
			{
				_portPosition = ((ThingWithComps)this).GetComp<PortPositionComp>();
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if (_portPosition == null)
			{
				_portPosition = ((ThingWithComps)this).GetComp<PortPositionComp>();
			}
		}

		protected override void Tick()
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).Tick();
			IntVec3 val = default(IntVec3);
			int num2 = default(int);
			for (int num = _tickCooldown.Count - 1; num >= 0; num--)
			{
				_tickCooldown.ElementAt(num).Deconstruct(ref val, ref num2);
				IntVec3 key = val;
				int num3 = num2;
				if (num3 == 1)
				{
					_tickCooldown.Remove(key);
				}
				else
				{
					_tickCooldown[key] = num3 - 1;
				}
			}
			if (!base.Powered || base.BoundStorageUnit == null || !base.BoundStorageUnit.CanWork)
			{
				return;
			}
			((Building_Storage)this).settings = (StorageSettings)(((object)((Building_Storage)base.BoundStorageUnit).settings) ?? ((object)new StorageSettings((IStoreSettingsParent)(object)this)));
			foreach (IntVec3 availablePosition in _portPosition.GetAvailablePositions())
			{
				if (!_tickCooldown.ContainsKey(availablePosition))
				{
					SuckFirstItem(availablePosition);
				}
			}
		}

		private void SuckFirstItem(IntVec3 position)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			foreach (Thing item in ((Thing)this).Map.thingGrid.ThingsListAt(position).ToList())
			{
				if (!item.def.EverStorable(false) || !base.BoundStorageUnit.CanReceiveThing(item) || ((Thing)this).Map.reservationManager.AllReservedThings().Contains(item))
				{
					continue;
				}
				base.BoundStorageUnit.HandleNewItem(item);
				FleckMaker.ThrowLightningGlow(((IntVec3)(ref position)).ToVector3(), ((Thing)this).Map, 0.8f);
				break;
			}
		}
	}
	public class OutputPortDsuBuilding : ABasePortDsuBuilding, IRemoveStorageInspectionTab
	{
		private OutputSettings _outputSettings;

		private PortPositionComp _portPosition;

		private readonly Dictionary<IntVec3, int> _tickCooldown = new Dictionary<IntVec3, int>();

		public override StorageIOMode IOMode => StorageIOMode.Output;

		private OutputSettings OutputSettings => _outputSettings ?? (_outputSettings = new OutputSettings());

		public override bool ForbidPawnInput => true;

		public override Graphic Graphic => ((Thing)this).Graphic.GetColoredVersion(((Thing)this).Graphic.Shader, ((Def)((Thing)this).def).GetModExtension<ModExtensionPortColor>().outColor, Color.white);

		public override void PostMake()
		{
			base.PostMake();
			if (_portPosition == null)
			{
				_portPosition = ((ThingWithComps)this).GetComp<PortPositionComp>();
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if (_portPosition == null)
			{
				_portPosition = ((ThingWithComps)this).GetComp<PortPositionComp>();
			}
		}

		private void OnOutputSettingsAction()
		{
			Find.WindowStack.Add((Window)(object)new OutputMinMaxDialog(OutputSettings, OnOutputSettingsClosed));
		}

		private void OnOutputSettingsClosed()
		{
			SelectedPorts().ToList().ForEach(delegate(OutputPortDsuBuilding p)
			{
				OutputSettings.Copy(p.OutputSettings);
			});
		}

		protected override void Tick()
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_0138: Unknown result type (might be due to invalid IL or missing references)
			//IL_019b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01af: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).Tick();
			IntVec3 val = default(IntVec3);
			int num2 = default(int);
			for (int num = _tickCooldown.Count - 1; num >= 0; num--)
			{
				_tickCooldown.ElementAt(num).Deconstruct(ref val, ref num2);
				IntVec3 key = val;
				int num3 = num2;
				if (num3 == 1)
				{
					_tickCooldown.Remove(key);
				}
				else
				{
					_tickCooldown[key] = num3 - 1;
				}
			}
			if (!base.Powered || base.BoundStorageUnit == null || !base.BoundStorageUnit.CanWork)
			{
				return;
			}
			foreach (IntVec3 availablePosition in _portPosition.GetAvailablePositions())
			{
				if (_tickCooldown.ContainsKey(availablePosition))
				{
					continue;
				}
				List<Thing> list = (from t in GridsUtility.GetThingList(availablePosition, ((Thing)this).Map)
					where t.def.EverStorable(false)
					select t).ToList();
				if (list.Count == 0)
				{
					RefillEmptySlot(availablePosition);
					continue;
				}
				for (int num4 = list.Count - 1; num4 >= 0; num4--)
				{
					Thing val2 = list[num4];
					if (!val2.def.EverStorable(false) || ((Thing)this).Map.reservationManager.AllReservedThings().Contains(val2) || SuckDisalowed(val2, availablePosition) || SuckMoreThanFirst(val2, num4, availablePosition) || SuckMoreThanMax(val2, availablePosition) || SuckLessThanMin(val2, availablePosition))
					{
					}
				}
			}
		}

		private void RefillEmptySlot(IntVec3 position)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0178: Unknown result type (might be due to invalid IL or missing references)
			//IL_017f: Unknown result type (might be due to invalid IL or missing references)
			//IL_018f: Unknown result type (might be due to invalid IL or missing references)
			if (!GenGrid.Walkable(position, ((Thing)this).Map) || !GenGrid.InBounds(position, ((Thing)this).Map))
			{
				return;
			}
			foreach (Thing item in ((Thing)this).Map.thingGrid.ThingsListAt(position).ToList())
			{
				if (item is DigitalStorageUnitBuilding || item is InputPortDsuBuilding)
				{
					return;
				}
			}
			foreach (Thing item2 in from t in base.BoundStorageUnit.GetStoredThings()
				where ((Building_Storage)this).settings.AllowedToAccept(t)
				select t)
			{
				int num = item2.stackCount;
				if (OutputSettings.UseMax)
				{
					num = Math.Min(num, OutputSettings.Max);
				}
				if (OutputSettings.UseMin && num < OutputSettings.Min)
				{
					continue;
				}
				Thing val = ThingMaker.MakeThing(item2.def, item2.Stuff);
				val.stackCount = num;
				item2.stackCount -= num;
				if (item2.def.useHitPoints)
				{
					val.HitPoints = item2.HitPoints;
				}
				GenSpawn.Spawn(val, position, ((Thing)this).Map, Rot4.East, (WipeMode)0, false, false);
				FleckMaker.ThrowLightningGlow(((IntVec3)(ref position)).ToVector3(), ((Thing)this).Map, 0.8f);
				if (item2.stackCount <= 0)
				{
					ThingOwner holdingOwner = item2.holdingOwner;
					if (holdingOwner != null)
					{
						holdingOwner.Remove(item2);
					}
					item2.DirtyMapMesh(item2.Map);
					((Thing)this).Map.listerMergeables.Notify_ThingStackChanged(item2);
					item2.Destroy((DestroyMode)0);
				}
				break;
			}
		}

		private bool SuckDisalowed(Thing thing, IntVec3 position)
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			if (((Building_Storage)this).settings.AllowedToAccept(thing))
			{
				return false;
			}
			_tickCooldown[position] = 20;
			if (!base.BoundStorageUnit.CanReceiveThing(thing))
			{
				_tickCooldown[position] = 20;
				return true;
			}
			base.BoundStorageUnit.HandleNewItem(thing);
			return true;
		}

		private bool SuckMoreThanFirst(Thing thing, int idx, IntVec3 position)
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			if (idx == 0)
			{
				return false;
			}
			if (!base.BoundStorageUnit.CanReceiveThing(thing))
			{
				_tickCooldown[position] = 20;
				return true;
			}
			base.BoundStorageUnit.HandleNewItem(thing);
			return true;
		}

		private bool SuckMoreThanMax(Thing thing, IntVec3 position)
		{
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			if (!OutputSettings.UseMax)
			{
				return false;
			}
			int num = Math.Min(OutputSettings.Max, thing.def.stackLimit);
			if (thing.stackCount <= num)
			{
				return false;
			}
			if (!base.BoundStorageUnit.CanReceiveThing(thing))
			{
				_tickCooldown[position] = 20;
				return true;
			}
			int num2 = thing.stackCount - num;
			Thing val = ThingMaker.MakeThing(thing.def, thing.Stuff);
			val.stackCount = num2;
			val.Position = thing.Position;
			thing.stackCount -= num2;
			thing.DirtyMapMesh(thing.Map);
			if (thing.def.useHitPoints)
			{
				val.HitPoints = thing.HitPoints;
			}
			((Entity)val).SpawnSetup(thing.Map, false);
			base.BoundStorageUnit.HandleNewItem(val);
			return true;
		}

		private bool SuckLessThanMin(Thing thing, IntVec3 position)
		{
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			if (!OutputSettings.UseMin)
			{
				return false;
			}
			int num = Math.Min(OutputSettings.Min, thing.def.stackLimit);
			if (thing.stackCount >= num)
			{
				return false;
			}
			if (!base.BoundStorageUnit.CanReceiveThing(thing))
			{
				_tickCooldown[position] = 20;
				return true;
			}
			base.BoundStorageUnit.HandleNewItem(thing);
			return true;
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			yield return (Gizmo)new Command_Action
			{
				icon = (Texture)(object)TextureHolder.SetTargetFuelLevel,
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("DSU.Output.Settings")),
				action = OnOutputSettingsAction
			};
		}

		public override string GetInspectString()
		{
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			string text = ((Building_Storage)this).GetInspectString();
			if (OutputSettings.UseMin || OutputSettings.UseMax)
			{
				string text2 = (OutputSettings.UseMin ? OutputSettings.Min.ToString("0") : "1");
				string text3 = (OutputSettings.UseMax ? OutputSettings.Max.ToString("0") : "∞");
				text += "\n";
				text = TaggedString.op_Implicit(text + TranslatorFormattedStringExtensions.Translate("DSU.Limit.GetInspectString", NamedArgument.op_Implicit(text2), NamedArgument.op_Implicit(text3)));
			}
			return text;
		}

		private IEnumerable<OutputPortDsuBuilding> SelectedPorts()
		{
			List<OutputPortDsuBuilding> list = Find.Selector.SelectedObjects.OfType<OutputPortDsuBuilding>().ToList();
			if (!list.Contains(this))
			{
				list.Add(this);
			}
			return list;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Deep.Look<OutputSettings>(ref _outputSettings, "outputSettings", Array.Empty<object>());
		}
	}
	public class DsuDropAdjustComp : ThingComp
	{
		private int _index;

		private List<IntVec3> _possibleOutputs = new List<IntVec3>();

		public IntVec3 CurrentCell => _possibleOutputs[_index %= _possibleOutputs.Count];

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			_possibleOutputs = new List<IntVec3>(GenAdj.CellsAdjacent8Way((Thing)(object)base.parent));
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			GenDraw.DrawFieldEdges(new List<IntVec3> { CurrentCell }, Color.yellow, (float?)null, (HashSet<IntVec3>)null, 2900);
		}

		private void RotateIndex(int step)
		{
			_index += step;
			if (_index < 0)
			{
				_index = _possibleOutputs.Count - 1;
			}
			if (_index >= _possibleOutputs.Count)
			{
				_index = 0;
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			yield return (Gizmo)(object)new ActionRightLeftCommand
			{
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("DSU.AdjustOutputDirection")),
				ActionL = delegate
				{
					RotateIndex(1);
				},
				ActionR = delegate
				{
					RotateIndex(-1);
				},
				icon = (Texture)(object)TexUI.RotRightTex,
				defaultIconColor = Color.green
			};
		}

		public override void PostExposeData()
		{
			Scribe_Values.Look<int>(ref _index, "outputSlotIndex", 0, false);
		}
	}
	public class DsuHeaterComp : ThingComp
	{
		private float _curTickTemperature;

		private bool _criticalDamageMessage = false;

		private DsuHeaterCompProperties Props => base.props as DsuHeaterCompProperties;

		private DigitalStorageUnitBuilding Dsu => base.parent as DigitalStorageUnitBuilding;

		public override void CompTick()
		{
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_0156: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01db: Unknown result type (might be due to invalid IL or missing references)
			//IL_01df: Unknown result type (might be due to invalid IL or missing references)
			//IL_026d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0277: Unknown result type (might be due to invalid IL or missing references)
			//IL_017e: Unknown result type (might be due to invalid IL or missing references)
			if (!DigitalStorageUnit.Config.HeaterEnabled || Dsu == null || !Gen.IsHashIntervalTick((Thing)(object)Dsu, 60) || !Dsu.Powered)
			{
				return;
			}
			_curTickTemperature = GenTemperature.GetTemperatureForCell(((Thing)Dsu).PositionHeld, ((Thing)Dsu).Map);
			if (_curTickTemperature >= (float)Props.MaxHeat)
			{
				return;
			}
			float num = (float)Props.HeatPerSecond + _curTickTemperature;
			GenTemperature.PushHeat(((Thing)Dsu).PositionHeld, ((Thing)Dsu).Map, num);
			bool flag = Gen.IsHashIntervalTick((Thing)(object)Dsu, 60);
			if (_curTickTemperature > (float)Props.DamageAtHeat)
			{
				if (!flag)
				{
					return;
				}
				float num2 = Props.DamageMultiplier * (_curTickTemperature - (float)Props.DamageAtHeat);
				DamageInfo val = default(DamageInfo);
				((DamageInfo)(ref val))..ctor(DamageDefOf.Burn, num2, 0f, -1f, (Thing)(object)Dsu, (BodyPartRecord)null, (ThingDef)null, (SourceCategory)0, (Thing)null, true, true, (QualityCategory)2, true, false);
				((Thing)Dsu).TakeDamage(val);
				{
					foreach (Thing storedThing in Dsu.GetStoredThings())
					{
						storedThing.TakeDamage(val);
					}
					return;
				}
			}
			if (_curTickTemperature > (float)Props.DamageAtHeat / 2f)
			{
				if (flag)
				{
					IntVec3 position = ((Thing)Dsu).Position;
					FleckMaker.ThrowSmoke(((IntVec3)(ref position)).ToVector3Shifted(), ((Thing)Dsu).Map, (float)Math.Round((double)(((Thing)base.parent).def.size.x * ((Thing)base.parent).def.size.z) / 4.0, 1));
				}
			}
			else if (_curTickTemperature > (float)Props.DamageAtHeat / 1.15f && !_criticalDamageMessage)
			{
				Find.LetterStack.ReceiveLetter(Translator.Translate("DSU.Alert.DsuOverheat"), Translator.Translate("DSU.Alert.DsuOverheat.Desc"), LetterDefOf.ThreatBig, LookTargets.op_Implicit((Thing)(object)base.parent), (Faction)null, (Quest)null, (List<ThingDef>)null, (string)null, 0, true);
				_criticalDamageMessage = true;
			}
			else
			{
				_criticalDamageMessage = false;
			}
		}

		public bool IsRoomHermetic()
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			Room room = GridsUtility.GetRoom(((Thing)Dsu).PositionHeld, ((Thing)Dsu).Map);
			if (room == null)
			{
				return false;
			}
			return room.OpenRoofCount <= 0 && !room.TouchesMapEdge;
		}

		public bool IsOverheat()
		{
			return _curTickTemperature > (float)Props.DamageAtHeat / 1.5f;
		}
	}
	public class DsuHeaterCompProperties : CompProperties
	{
		public int HeatPerSecond = 10;

		public int MaxHeat = 1000;

		public int DamageAtHeat = 200;

		public float DamageMultiplier = 0.1f;

		public DsuHeaterCompProperties()
		{
			base.compClass = typeof(DsuHeaterComp);
		}
	}
	public class DsuHeaterPlaceWorker : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			Room room = GridsUtility.GetRoom(loc, map);
			if (room == null || room.TouchesMapEdge)
			{
				return AcceptanceReport.op_Implicit(Translator.Translate("MustBePlacedIndoors"));
			}
			if (room.OpenRoofCount > 0)
			{
				return AcceptanceReport.op_Implicit(Translator.Translate("DSU.PlaceWorker.ProperRoomNeeded"));
			}
			return AcceptanceReport.WasAccepted;
		}

		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			if (DigitalStorageUnit.Config.HeaterEnabled)
			{
				Room room = GridsUtility.GetRoom(center, Find.CurrentMap);
				if (room != null && !room.UsesOutdoorTemperature)
				{
					GenDraw.DrawFieldEdges(room.Cells.ToList(), GenTemperature.ColorRoomHot, (float?)null, (HashSet<IntVec3>)null, 2900);
				}
			}
		}
	}
	public class ExtenderPlaceWorker : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			Room room = GridsUtility.GetRoom(loc, map);
			if (room == null || room.TouchesMapEdge)
			{
				return AcceptanceReport.op_Implicit(Translator.Translate("MustBePlacedIndoors"));
			}
			if (room.OpenRoofCount > 0)
			{
				return AcceptanceReport.op_Implicit(Translator.Translate("DSU.PlaceWorker.ProperRoomNeeded"));
			}
			int num = GenCollection.Count<Thing>(room.ContainedAndAdjacentThings, (Predicate<Thing>)((Thing t) => t is DigitalStorageUnitBuilding));
			if (num <= 0)
			{
				return AcceptanceReport.op_Implicit(Translator.Translate("DSU.PlaceWorker.DsuNeeded"));
			}
			if (num > 1)
			{
				return AcceptanceReport.op_Implicit(Translator.Translate("DSU.PlaceWorker.OnlyOneDsuNeeded"));
			}
			return AcceptanceReport.WasAccepted;
		}

		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			Room room = GridsUtility.GetRoom(center, Find.CurrentMap);
			if (room == null || room.UsesOutdoorTemperature)
			{
				return;
			}
			List<Thing> list = room.ContainedAndAdjacentThings.Where((Thing t) => t is DigitalStorageUnitBuilding).ToList();
			foreach (Thing item in list)
			{
				GenDraw.DrawCircleOutline(GenThing.TrueCenter(item), 0.8f, (SimpleColor)5);
				GenDraw.DrawLineBetween(GenThing.TrueCenter(item), GenThing.TrueCenter(center, rot, def.size, ((BuildableDef)def).Altitude), (SimpleColor)5, 0.1f);
			}
		}
	}
	public class RenderLinksComp : ThingComp
	{
		private static readonly float BaseAlt = Altitudes.AltitudeFor((AltitudeLayer)39);

		public override void PostDraw()
		{
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostDraw();
			if (base.parent is ABasePortDsuBuilding { Powered: not false, BoundStorageUnit: null })
			{
				Vector3 val = GenThing.TrueCenter((Thing)(object)base.parent);
				val.y = BaseAlt + 15f / 64f;
				float num = (float)(0.30000001192092896 + (Math.Sin(((double)Time.realtimeSinceStartup + 397.0 * (double)(((Thing)base.parent).thingIDNumber % 571)) * 4.0) + 1.0) * 0.5 * 0.699999988079071);
				Material val2 = FadedMaterialPool.FadedVersionOf(TextureHolder.MatLostLink, num);
				Graphics.DrawMesh(MeshPool.plane08, val, Quaternion.identity, val2, 0);
			}
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0106: Unknown result type (might be due to invalid IL or missing references)
			//IL_0118: Unknown result type (might be due to invalid IL or missing references)
			//IL_011f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0155: Unknown result type (might be due to invalid IL or missing references)
			if (base.parent is ABasePortDsuBuilding { BoundStorageUnit: not null } aBasePortDsuBuilding)
			{
				GenDraw.DrawCircleOutline(GenThing.TrueCenter((Thing)(object)aBasePortDsuBuilding), 0.8f, (SimpleColor)5);
				GenDraw.DrawCircleOutline(GenThing.TrueCenter((Thing)(object)aBasePortDsuBuilding.BoundStorageUnit), 0.8f, (SimpleColor)5);
				GenDraw.DrawLineBetween(GenThing.TrueCenter((Thing)(object)aBasePortDsuBuilding), GenThing.TrueCenter((Thing)(object)aBasePortDsuBuilding.BoundStorageUnit), (SimpleColor)5, 0.1f);
			}
			if (!(base.parent is DigitalStorageUnitBuilding digitalStorageUnitBuilding))
			{
				return;
			}
			bool flag = false;
			foreach (ABasePortDsuBuilding port in digitalStorageUnitBuilding.Ports)
			{
				flag = true;
				GenDraw.DrawCircleOutline(GenThing.TrueCenter((Thing)(object)port), 0.8f, (SimpleColor)5);
				GenDraw.DrawLineBetween(GenThing.TrueCenter((Thing)(object)digitalStorageUnitBuilding), GenThing.TrueCenter((Thing)(object)port), (SimpleColor)5, 0.1f);
			}
			foreach (DataExtenderBuilding extender in digitalStorageUnitBuilding.Extenders)
			{
				flag = true;
				GenDraw.DrawCircleOutline(GenThing.TrueCenter((Thing)(object)extender), 0.8f, (SimpleColor)6);
				GenDraw.DrawLineBetween(GenThing.TrueCenter((Thing)(object)digitalStorageUnitBuilding), GenThing.TrueCenter((Thing)(object)extender), (SimpleColor)6, 0.1f);
			}
			if (flag)
			{
				GenDraw.DrawCircleOutline(GenThing.TrueCenter((Thing)(object)digitalStorageUnitBuilding), 0.8f, (SimpleColor)5);
			}
		}
	}
	public class ModExtensionPortColor : DefModExtension
	{
		public Color inColor;

		public Color outColor;
	}
	public class PortPositionComp : ThingComp
	{
		private int _positionIdx = -1;

		private List<IntVec3> _roundAround;

		private List<IntVec3> _every;

		private List<IntVec3> _parent;

		private List<IntVec3> _directional;

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			_roundAround = GenAdj.CellsAdjacent8Way((Thing)(object)base.parent).ToList();
			_every = new List<IntVec3>(_roundAround) { ((Thing)base.parent).Position };
			_parent = new List<IntVec3> { ((Thing)base.parent).Position };
			RecacheDirectional();
		}

		public List<IntVec3> GetAvailablePositions()
		{
			int positionIdx = _positionIdx;
			if (1 == 0)
			{
			}
			List<IntVec3> result = positionIdx switch
			{
				-2 => _every, 
				-1 => _parent, 
				_ => _directional, 
			};
			if (1 == 0)
			{
			}
			return result;
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			GenDraw.DrawFieldEdges(GetAvailablePositions(), Color.yellow, (float?)null, (HashSet<IntVec3>)null, 2900);
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			yield return (Gizmo)(object)new ActionRightLeftCommand
			{
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("DSU.AdjustOutputDirection")),
				ActionL = RotateL,
				ActionR = RotateR,
				icon = (Texture)(object)TexUI.RotRightTex,
				defaultIconColor = Color.green
			};
		}

		private void RotateL()
		{
			_positionIdx--;
			if (_positionIdx < -2)
			{
				_positionIdx = _roundAround.Count - 1;
			}
			RecacheDirectional();
		}

		private void RotateR()
		{
			_positionIdx++;
			if (_positionIdx > _roundAround.Count - 1)
			{
				_positionIdx = -2;
			}
			RecacheDirectional();
		}

		private void RecacheDirectional()
		{
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			_directional = ((_positionIdx >= 0) ? new List<IntVec3> { _roundAround[_positionIdx] } : new List<IntVec3>());
		}

		public override void PostExposeData()
		{
			Scribe_Values.Look<int>(ref _positionIdx, "positionIdx", -1, true);
		}
	}
	[StaticConstructorOnStartup]
	public class DigitalStorageUnit : Mod
	{
		private const string ModID = "Relvl.DigitalStorageUnit";

		public static DigitalStorageUnitConfig Config { get; private set; }

		static DigitalStorageUnit()
		{
			Config = new DigitalStorageUnitConfig();
		}

		public DigitalStorageUnit(ModContentPack content)
			: base(content)
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Expected O, but got Unknown
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Expected O, but got Unknown
			try
			{
				Config = ((Mod)this).GetSettings<DigitalStorageUnitConfig>();
				Harmony val = new Harmony("Relvl.DigitalStorageUnit");
				val.PatchAll(Assembly.GetExecutingAssembly());
				if (ModsConfig.ActiveModsInLoadOrder.Any((ModMetaData m) => GenText.EqualsIgnoreCase("LWM.DeepStorage", m.packageIdLowerCase)))
				{
					try
					{
						MethodInfo methodInfo = AccessTools.Method("LWM.DeepStorage.Open_DS_Tab_On_Select:Postfix", (Type[])null, (Type[])null);
						MethodInfo methodInfo2 = AccessTools.Method("SubPatch_Open_DS_Tab_On_Select_Postfix:Prefix", (Type[])null, (Type[])null);
						val.Patch((MethodBase)methodInfo, new HarmonyMethod(methodInfo2), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Log.Message("DigitalStorageUnit: LWM.DeepStorage.Open_DS_Tab_On_Select:Postfix patched");
					}
					catch (Exception ex)
					{
						Log.Error(ex.ToString());
					}
				}
				if (ModsConfig.ActiveModsInLoadOrder.Any((ModMetaData m) => GenText.EqualsIgnoreCase("adaptive.storage.framework", m.packageIdLowerCase)))
				{
					try
					{
						val.Patch((MethodBase)AccessTools.Method("AdaptiveStorage.HarmonyPatches.AddITabToLegacyStorages:Postfix", (Type[])null, (Type[])null), HarmonyMethod.op_Implicit(AccessTools.Method(typeof(SubPatch_AdaptiveStorage_AddITabToLegacyStorages_Postfix), "Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					}
					catch (Exception ex2)
					{
						Log.Error(ex2.ToString());
					}
				}
				if (ModsConfig.ActiveModsInLoadOrder.Any((ModMetaData m) => GenText.EqualsIgnoreCase("Dubwise.Rimefeller", m.packageIdLowerCase)))
				{
					try
					{
						val.Patch((MethodBase)AccessTools.Method("Rimefeller.CompRefinery:get_AdjacentHoppers", (Type[])null, (Type[])null), HarmonyMethod.op_Implicit(AccessTools.Method(typeof(SubParch_Rimfeller_CompRefinery), "Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					}
					catch (Exception ex3)
					{
						Log.Error(ex3.ToString());
					}
				}
				Log.Message($"DigitalStorageUnit {typeof(DigitalStorageUnit).Assembly.GetName().Version} - Harmony patches successful");
			}
			catch (Exception ex4)
			{
				Log.Error("DigitalStorageUnit :: Caught exception: " + ex4);
			}
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			Config.DoSettingsWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("DSU"));
		}
	}
	public class DigitalStorageUnitConfig : ModSettings
	{
		private const int Version = 1;

		public float DsuPathingMultiplier = 1f;

		public bool CheapPathfinding = true;

		public bool HalfPathfinding = true;

		public float EnergyPerStack = 10f;

		public bool HeaterEnabled;

		public bool WorkGiverDoBillUnnecessaryFix = true;

		public bool BillSearchRadiusFix = true;

		public bool AutoBoundDsu = true;

		public bool CleanCellItemList = true;

		public int BaseDsuStackSize = 256;

		public int ExtenderStackIncrease = 128;

		public override void ExposeData()
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Invalid comparison between Unknown and I4
			int num = 1;
			Scribe_Values.Look<int>(ref num, "Version", 1, true);
			if (num == 1 || (int)Scribe.mode == 2)
			{
				Scribe_Values.Look<float>(ref DsuPathingMultiplier, "DsuPathingMultiplier", 1f, true);
				Scribe_Values.Look<bool>(ref CheapPathfinding, "CheapPathfinding", true, true);
				Scribe_Values.Look<bool>(ref HalfPathfinding, "HalfPathfinding", true, true);
				Scribe_Values.Look<float>(ref EnergyPerStack, "EnergyPerStack", 10f, true);
				Scribe_Values.Look<bool>(ref HeaterEnabled, "HeaterEnabled", false, true);
				Scribe_Values.Look<bool>(ref WorkGiverDoBillUnnecessaryFix, "WorkGiverDoBillUnnecessaryFix", true, true);
				Scribe_Values.Look<bool>(ref BillSearchRadiusFix, "BillSearchRadiusFix", true, true);
				Scribe_Values.Look<bool>(ref AutoBoundDsu, "AutoBoundDsu", true, true);
				Scribe_Values.Look<bool>(ref CleanCellItemList, "CleanCellItemList", true, true);
				Scribe_Values.Look<int>(ref BaseDsuStackSize, "BaseDsuStackSize", 256, true);
				Scribe_Values.Look<int>(ref ExtenderStackIncrease, "ExtenderStackIncrease", 128, true);
			}
			else
			{
				Log.Warning("DSU: version changed, config reset");
			}
			EnergyPerStack = Mathf.Clamp(EnergyPerStack, 0f, 100000f);
		}

		public void DoSettingsWindowContents(Rect inRect)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Expected O, but got Unknown
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_013f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0161: Unknown result type (might be due to invalid IL or missing references)
			//IL_0176: Unknown result type (might be due to invalid IL or missing references)
			//IL_0196: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0200: Unknown result type (might be due to invalid IL or missing references)
			//IL_0215: Unknown result type (might be due to invalid IL or missing references)
			//IL_0235: Unknown result type (might be due to invalid IL or missing references)
			//IL_024a: Unknown result type (might be due to invalid IL or missing references)
			//IL_027b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0280: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0302: Unknown result type (might be due to invalid IL or missing references)
			Listing_Standard val = new Listing_Standard((GameFont)1);
			((Listing)val).Begin(inRect);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DSU.Config.UseCheapPathfinding")), ref CheapPathfinding, TaggedString.op_Implicit(Translator.Translate("DSU.Config.UseCheapPathfinding.Desc") + ColoredText.Colorize(Translator.Translate("DSU.Config.UseCheapPathfinding.Desc.Caution"), Color.red)), 0f, 1f);
			if (CheapPathfinding)
			{
				DsuPathingMultiplier = (float)Math.Round(val.SliderLabeled(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DSU.Config.DsuPathingMultiplier", NamedArgument.op_Implicit(DsuPathingMultiplier.ToString("0.0")))), DsuPathingMultiplier, 0.8f, 5f, 0.5f, TaggedString.op_Implicit(Translator.Translate("DSU.Config.DsuPathingMultiplier.Desc"))), 1);
			}
			else
			{
				val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DSU.Config.HalfPathfinding")), ref HalfPathfinding, TaggedString.op_Implicit(Translator.Translate("DSU.Config.HalfPathfinding.Desc")), 0f, 1f);
			}
			EnergyPerStack = (float)Math.Round(val.SliderLabeled(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DSU.Config.EnergyPerStack", NamedArgument.op_Implicit(EnergyPerStack.ToString("0")))), EnergyPerStack, 1f, 50f, 0.5f, TaggedString.op_Implicit(Translator.Translate("DSU.Config.EnergyPerStack.Desc"))), 0);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DSU.Config.HeaterEnabled")), ref HeaterEnabled, TaggedString.op_Implicit(Translator.Translate("DSU.Config.HeaterEnabled.Desc")), 0f, 1f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DSU.Config.WorkGiverDoBillUnnecessaryFix")), ref WorkGiverDoBillUnnecessaryFix, TaggedString.op_Implicit(Translator.Translate("DSU.Config.WorkGiverDoBillUnnecessaryFix.Desc")), 0f, 1f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DSU.Config.BillSearchRadiusFix")), ref BillSearchRadiusFix, TaggedString.op_Implicit(Translator.Translate("DSU.Config.BillSearchRadiusFix.Desc")), 0f, 1f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DSU.Config.AutoBoundDsu")), ref AutoBoundDsu, TaggedString.op_Implicit(Translator.Translate("DSU.Config.AutoBoundDsu.Desc")), 0f, 1f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DSU.Config.CleanCellItemList")), ref CleanCellItemList, TaggedString.op_Implicit(Translator.Translate("DSU.Config.CleanCellItemList.Desc")), 0f, 1f);
			BaseDsuStackSize = (int)Math.Round(val.SliderLabeled(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DSU.Config.BaseDsuStackSize", NamedArgument.op_Implicit(BaseDsuStackSize.ToString("0")))), (float)BaseDsuStackSize, 256f, 9999f, 0.5f, TaggedString.op_Implicit(Translator.Translate("DSU.Config.BaseDsuStackSize.Desc"))), 0);
			ExtenderStackIncrease = (int)Math.Round(val.SliderLabeled(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DSU.Config.ExtenderStackIncrease", NamedArgument.op_Implicit(ExtenderStackIncrease.ToString("0")))), (float)ExtenderStackIncrease, 128f, 9999f, 0.5f, TaggedString.op_Implicit(Translator.Translate("DSU.Config.ExtenderStackIncrease.Desc"))), 0);
			((Listing)val).End();
		}
	}
	public class OutputSettings : IExposable
	{
		public readonly string MinTooltip = "DSU.Min.Desc";

		public readonly string MaxTooltip = "DSU.Max.Desc";

		public bool UseMin;

		public bool UseMax;

		public int Min;

		public int Max;

		public OutputSettings()
		{
			UseMin = false;
			UseMax = false;
			Min = 0;
			Max = 75;
		}

		public void ExposeData()
		{
			Scribe_Values.Look<bool>(ref UseMin, "useMin", false, false);
			Scribe_Values.Look<bool>(ref UseMax, "useMax", false, false);
			Scribe_Values.Look<int>(ref Min, "min", 0, false);
			Scribe_Values.Look<int>(ref Max, "max", 0, false);
		}

		public bool SatisfiesMax(int stackCount, int stackLimit)
		{
			return CountNeededToReachMax(stackCount, stackLimit) > 0;
		}

		public bool SatisfiesMin(int stackCount)
		{
			return !UseMin || stackCount >= Min;
		}

		public int CountNeededToReachMax(int currentCount, int limit)
		{
			return UseMax ? (Math.Min(limit, Max) - currentCount) : (limit - currentCount);
		}

		public void Copy(OutputSettings other)
		{
			other.UseMin = UseMin;
			other.UseMax = UseMax;
			other.Min = Min;
			other.Max = Max;
		}
	}
	[StaticConstructorOnStartup]
	public class ModInit
	{
		static ModInit()
		{
			string[] source = new string[2] { "ITab_Storage", "ITab_DeepStorage_Inventory" };
			foreach (ThingDef item in DefDatabase<ThingDef>.AllDefsListForReading)
			{
				if ((object)item.thingClass == null || !typeof(IRemoveStorageInspectionTab).IsAssignableFrom(item.thingClass))
				{
					continue;
				}
				if (item.inspectorTabs != null)
				{
					for (int num = item.inspectorTabs.Count - 1; num >= 0; num--)
					{
						if (source.Contains(item.inspectorTabs[num].Name))
						{
							item.inspectorTabs.RemoveAt(num);
						}
					}
				}
				if (item.inspectorTabsResolved == null)
				{
					continue;
				}
				for (int num2 = item.inspectorTabsResolved.Count - 1; num2 >= 0; num2--)
				{
					if (source.Contains(((object)item.inspectorTabsResolved[num2]).GetType().Name))
					{
						item.inspectorTabsResolved.RemoveAt(num2);
					}
				}
			}
		}
	}
	public static class Debug
	{
		[Flags]
		public enum Flag
		{
			PlaceThing = 1,
			Conveyors = 2,
			ConveyorGraphics = 4,
			Benchmark = 8,
			ExtModifyProduct = 0x10,
			AssemblerQueue = 0x20
		}

		public static Flag activeFlags;

		[Conditional("DEBUG")]
		public static void Message(Flag flag, string text, bool ignoreStopLoggingLimit = false)
		{
			if ((activeFlags & flag) > (Flag)0)
			{
				Log.Message(text);
			}
		}

		[Conditional("DEBUG")]
		public static void Warning(Flag flag, string text, bool ignoreStopLoggingLimit = false)
		{
			if ((activeFlags & flag) > (Flag)0)
			{
				Log.Warning(text);
			}
		}

		[Conditional("DEBUG")]
		public static void Error(Flag flag, string text, bool ignoreStopLoggingLimit = false)
		{
			if ((activeFlags & flag) > (Flag)0)
			{
				Log.Error(text);
			}
		}
	}
	public class DsuRoomRoleWorker : RoomRoleWorker
	{
		public override float GetScore(Room room)
		{
			if (!DigitalStorageUnit.Config.HeaterEnabled)
			{
				return 0f;
			}
			List<Thing> containedAndAdjacentThings = room.ContainedAndAdjacentThings;
			int num = GenCollection.Count<Thing>(containedAndAdjacentThings, (Predicate<Thing>)((Thing t) => t is DigitalStorageUnitBuilding));
			return 10000 * num;
		}
	}
	internal class ITab_Filter : ITab_Storage
	{
		public ITab_Filter()
		{
			((InspectTabBase)this).labelKey = "Filter";
		}
	}
	[StaticConstructorOnStartup]
	public class ITab_Items : ITab
	{
		private struct RowRectSet
		{
			public Rect LabelRect;

			public Rect LabelRectInner;

			public Rect InfoRect;

			public Rect IconRect;

			public Rect RotableInfoRect;

			public Rect ForbidButtonRect;

			public Rect DropButtonRect;

			public void Shift(float height = 28f)
			{
				ref Rect labelRect = ref LabelRect;
				((Rect)(ref labelRect)).y = ((Rect)(ref labelRect)).y + height;
				ref Rect labelRectInner = ref LabelRectInner;
				((Rect)(ref labelRectInner)).y = ((Rect)(ref labelRectInner)).y + height;
				ref Rect infoRect = ref InfoRect;
				((Rect)(ref infoRect)).y = ((Rect)(ref infoRect)).y + height;
				ref Rect iconRect = ref IconRect;
				((Rect)(ref iconRect)).y = ((Rect)(ref iconRect)).y + height;
				ref Rect forbidButtonRect = ref ForbidButtonRect;
				((Rect)(ref forbidButtonRect)).y = ((Rect)(ref forbidButtonRect)).y + height;
				ref Rect dropButtonRect = ref DropButtonRect;
				((Rect)(ref dropButtonRect)).y = ((Rect)(ref dropButtonRect)).y + height;
				ref Rect rotableInfoRect = ref RotableInfoRect;
				((Rect)(ref rotableInfoRect)).y = ((Rect)(ref rotableInfoRect)).y + height;
			}

			public void Reset(float initialY, float width)
			{
				//IL_0012: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Unknown result type (might be due to invalid IL or missing references)
				//IL_0033: Unknown result type (might be due to invalid IL or missing references)
				//IL_0038: Unknown result type (might be due to invalid IL or missing references)
				//IL_006c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0071: Unknown result type (might be due to invalid IL or missing references)
				//IL_0093: Unknown result type (might be due to invalid IL or missing references)
				//IL_0098: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
				//IL_0117: Unknown result type (might be due to invalid IL or missing references)
				//IL_011c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0123: Unknown result type (might be due to invalid IL or missing references)
				//IL_0128: Unknown result type (might be due to invalid IL or missing references)
				LabelRect = new Rect(0f, 0f, width, 28f);
				InfoRect = new Rect(((Rect)(ref LabelRect)).x, initialY, 24f, 24f);
				ref Rect labelRect = ref LabelRect;
				((Rect)(ref labelRect)).xMin = ((Rect)(ref labelRect)).xMin + 26f;
				IconRect = new Rect(((Rect)(ref LabelRect)).x, initialY, 24f, 24f);
				DropButtonRect = new Rect(((Rect)(ref LabelRect)).xMax - 24f, initialY, 24f, 24f);
				ref Rect labelRect2 = ref LabelRect;
				((Rect)(ref labelRect2)).width = ((Rect)(ref labelRect2)).width - 26f;
				ForbidButtonRect = new Rect(((Rect)(ref LabelRect)).xMax - 24f, initialY, 24f, 24f);
				ref Rect labelRect3 = ref LabelRect;
				((Rect)(ref labelRect3)).width = ((Rect)(ref labelRect3)).width - 26f;
				RotableInfoRect = new Rect(((Rect)(ref LabelRect)).xMax - 30f - 10f, initialY, 30f, 24f);
				LabelRectInner = LabelRect;
				ref Rect labelRectInner = ref LabelRectInner;
				((Rect)(ref labelRectInner)).xMin = ((Rect)(ref labelRectInner)).xMin + 28f;
			}

			public bool IsWithinViewbox(float scrollPosition, float viewBoxHeight)
			{
				return ((Rect)(ref LabelRect)).y + 28f - scrollPosition >= 0f && ((Rect)(ref LabelRect)).y - scrollPosition < viewBoxHeight;
			}
		}

		private const float RowHight = 28f;

		private const int FramePadding = 10;

		private const float ClearSearchButtonSize = 18f;

		private const float ButtonSize = 24f;

		private static readonly Dictionary<Thing, int> ThingMaxHitPoints = new Dictionary<Thing, int>();

		private static readonly Dictionary<Thing, ThingIconTextureData> ThingIconCache = new Dictionary<Thing, ThingIconTextureData>();

		private static DigitalStorageUnitBuilding _oldSelected;

		private Vector2 _scrollPos;

		private string _searchQuery = "";

		private List<Thing> _itemsToShow;

		private TaggedString _windowCaption = TaggedString.op_Implicit("");

		private float _windowCaptionSize = 30f;

		private RowRectSet _rowRectSet;

		private DigitalStorageUnitBuilding Selected => ((ITab)this).SelThing as DigitalStorageUnitBuilding;

		public ITab_Items()
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).size = new Vector2(480f, 480f);
			((InspectTabBase)this).labelKey = "DSU.IItemsTab";
		}

		public override void OnOpen()
		{
			RecalculateList();
		}

		public void OnClose()
		{
			ThingMaxHitPoints.Clear();
			ThingIconCache.Clear();
		}

		private void RenderHeader(Rect frame, ref float curY)
		{
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0135: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
			Text.Anchor = (TextAnchor)0;
			Text.Font = (GameFont)2;
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(0f, curY, ((Rect)(ref frame)).width, _windowCaptionSize);
			Widgets.Label(val, _windowCaption);
			curY += ((Rect)(ref val)).height + 5f;
			Text.Font = (GameFont)1;
			GUI.color = Color.white;
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(0f, curY, ((Rect)(ref frame)).width - 10f - 18f, 25f);
			string text = Widgets.TextField(val2, _searchQuery);
			if (text != _searchQuery)
			{
				_searchQuery = text;
				RecalculateList();
			}
			if (!string.IsNullOrEmpty(_searchQuery))
			{
				Rect val3 = default(Rect);
				((Rect)(ref val3))..ctor(((Rect)(ref frame)).width - 18f, curY + 3f, 18f, 18f);
				if (Widgets.ButtonImage(val3, TexButton.CloseXSmall, Color.red, true, (string)null))
				{
					SoundStarter.PlayOneShotOnCamera(SoundDefOf.Click, (Map)null);
					_searchQuery = "";
					RecalculateList();
				}
			}
			curY += ((Rect)(ref val2)).height + 10f;
			GUI.color = Color.white;
		}

		private bool ThingFilterPredicate(Thing thing)
		{
			if (string.IsNullOrEmpty(_searchQuery))
			{
				return true;
			}
			return ((Entity)MinifyUtility.GetInnerIfMinified(thing)).Label.ToLower().NormalizedFuzzyStrength(_searchQuery.ToLower()) < 0.25;
		}

		public void RecalculateList()
		{
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			//IL_012e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0138: Unknown result type (might be due to invalid IL or missing references)
			//IL_013f: Unknown result type (might be due to invalid IL or missing references)
			if (Selected != null)
			{
				List<Thing> storedThings = Selected.GetStoredThings();
				_itemsToShow = storedThings.Where(ThingFilterPredicate).OrderByDescending(ThingSortLabelPredicate).ThenByDescending(ThingSortQualityPredicate)
					.ThenByDescending(ThingSortHitPointsPredicate)
					.ToList();
				_oldSelected = Selected;
				Text.Font = (GameFont)2;
				_windowCaption = (string.IsNullOrEmpty(_searchQuery) ? TranslatorFormattedStringExtensions.Translate("DSU.IItemsTab.Caption", NamedArgument.op_Implicit(((Entity)Selected).LabelCap), NamedArgument.op_Implicit(storedThings.Count), NamedArgument.op_Implicit(Selected.GetSlotLimit)) : TranslatorFormattedStringExtensions.Translate("DSU.IItemsTab.Caption.Filtered", NamedArgument.op_Implicit(((Entity)Selected).LabelCap), NamedArgument.op_Implicit(storedThings.Count), NamedArgument.op_Implicit(Selected.GetSlotLimit), NamedArgument.op_Implicit(_itemsToShow.Count)));
				_windowCaptionSize = Text.CalcHeight(TaggedString.op_Implicit(_windowCaption), ((InspectTabBase)this).size.x - 20f);
			}
		}

		protected override void FillTab()
		{
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00de: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
			if (_itemsToShow == null || _oldSelected == null || _oldSelected != Selected)
			{
				RecalculateList();
			}
			Text.Font = (GameFont)1;
			Text.Anchor = (TextAnchor)0;
			float curY = 0f;
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(10f, 10f, ((InspectTabBase)this).size.x - 20f, ((InspectTabBase)this).size.y - 20f);
			GUI.BeginGroup(val);
			RenderHeader(val, ref curY);
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(0f, curY, ((Rect)(ref val)).width, ((Rect)(ref val)).height - curY);
			Rect val3 = default(Rect);
			((Rect)(ref val3))..ctor(0f, 0f, ((Rect)(ref val2)).width - 16f, (float)_itemsToShow.Count * 28f);
			Widgets.BeginScrollView(val2, ref _scrollPos, val3, true);
			if (_itemsToShow.Count < 1)
			{
				Widgets.Label(val3, Translator.Translate("DSU.IItemsTab.Label.Empty"));
			}
			_rowRectSet.Reset(0f, ((Rect)(ref val3)).width);
			for (int num = _itemsToShow.Count - 1; num >= 0; num--)
			{
				if (_rowRectSet.IsWithinViewbox(_scrollPos.y, ((Rect)(ref val2)).height))
				{
					Thing thing = _itemsToShow[num];
					DrawThingRow(thing);
				}
				_rowRectSet.Shift();
			}
			Widgets.EndScrollView();
			GUI.EndGroup();
			GUI.color = Color.white;
			Text.Anchor = (TextAnchor)0;
		}

		private void DrawThingRow(Thing thing)
		{
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			//IL_0176: Unknown result type (might be due to invalid IL or missing references)
			//IL_0235: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0202: Unknown result type (might be due to invalid IL or missing references)
			//IL_020c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0211: Unknown result type (might be due to invalid IL or missing references)
			//IL_024e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0242: Unknown result type (might be due to invalid IL or missing references)
			//IL_0253: Unknown result type (might be due to invalid IL or missing references)
			//IL_02af: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02df: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
			string text = ((thing is Corpse) ? ((Entity)thing).Label : GenLabel.ThingLabel(MinifyUtility.GetInnerIfMinified(thing), thing.stackCount, false, true));
			string text2 = GenText.CapitalizeFirst(text, (Def)(object)thing.def);
			RenderRowInfoButton(thing);
			RenderRowIcon(thing);
			if (Mouse.IsOver(_rowRectSet.LabelRect))
			{
				GUI.color = ITab_Pawn_Gear.HighlightColor;
				GUI.DrawTexture(_rowRectSet.LabelRect, (Texture)(object)TexUI.HighlightTex);
			}
			if (Widgets.ButtonInvisible(_rowRectSet.LabelRect, true))
			{
				Find.Selector.ClearSelection();
				Find.Selector.Select((object)thing, true, true);
			}
			Text.Anchor = (TextAnchor)3;
			GUI.color = ITab_Pawn_Gear.ThingLabelColor;
			Text.WordWrap = false;
			Widgets.Label(_rowRectSet.LabelRectInner, GenText.Truncate(text2, ((Rect)(ref _rowRectSet.LabelRectInner)).width, (Dictionary<string, string>)null));
			Text.WordWrap = true;
			if (!ThingMaxHitPoints.ContainsKey(thing))
			{
				ThingMaxHitPoints.Add(thing, thing.MaxHitPoints);
			}
			TooltipHandler.TipRegion(_rowRectSet.LabelRect, TipSignal.op_Implicit(thing.def.useHitPoints ? (text2 + "\n" + thing.HitPoints + " / " + ThingMaxHitPoints[thing]) : text2));
			CompRottable obj = ThingCompUtility.TryGetComp<CompRottable>(thing);
			int num = Math.Min(int.MaxValue, (obj != null) ? obj.TicksUntilRotAtCurrentTemp : int.MaxValue);
			if (num < 36000000)
			{
				GUI.color = Color.yellow;
				Text.Anchor = (TextAnchor)5;
				Widgets.Label(_rowRectSet.RotableInfoRect, ((float)num / 60000f).ToString("0.#"));
				Text.Anchor = (TextAnchor)3;
				GUI.color = Color.white;
				TooltipHandler.TipRegion(_rowRectSet.RotableInfoRect, TipSignal.op_Implicit(Translator.Translate("DaysUntilRotTip")));
			}
			bool flag = !ForbidUtility.IsForbidden(thing, Faction.OfPlayer);
			bool flag2 = flag;
			TooltipHandler.TipRegion(_rowRectSet.ForbidButtonRect, TipSignal.op_Implicit(flag ? Translator.Translate("CommandNotForbiddenDesc") : Translator.Translate("CommandForbiddenDesc")));
			Widgets.Checkbox(((Rect)(ref _rowRectSet.ForbidButtonRect)).x, ((Rect)(ref _rowRectSet.ForbidButtonRect)).y, ref flag, 24f, false, false, (Texture2D)null, (Texture2D)null);
			if (flag != flag2)
			{
				ForbidUtility.SetForbidden(thing, !flag, false);
			}
			TooltipHandler.TipRegion(_rowRectSet.DropButtonRect, TipSignal.op_Implicit(TranslatorFormattedStringExtensions.Translate("DSU.Drop", NamedArgument.op_Implicit(text))));
			if (Widgets.ButtonImage(_rowRectSet.DropButtonRect, TexButton.Drop, Color.gray, Color.white, false, (string)null))
			{
				DropThing(thing);
			}
		}

		private void RenderRowInfoButton(Thing thing)
		{
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
			Func<Dialog_InfoCard> func = () => new Dialog_InfoCard(thing, (Precept_ThingStyle)null);
			Thing obj = thing;
			IConstructible constructible = (IConstructible)(object)((obj is IConstructible) ? obj : null);
			if (constructible != null)
			{
				BuildableDef entityDefToBuild = thing.def.entityDefToBuild;
				ThingDef entityDefToBuild2 = (ThingDef)(object)((entityDefToBuild is ThingDef) ? entityDefToBuild : null);
				func = ((entityDefToBuild2 == null) ? ((Func<Dialog_InfoCard>)(() => new Dialog_InfoCard((Def)(object)thing.def.entityDefToBuild, (Precept_ThingStyle)null))) : ((Func<Dialog_InfoCard>)(() => new Dialog_InfoCard(entityDefToBuild2, constructible.EntityToBuildStuff(), (Precept_ThingStyle)null))));
			}
			TooltipHandler.TipRegionByKey(_rowRectSet.InfoRect, "DefInfoTip");
			UIHighlighter.HighlightOpportunity(_rowRectSet.InfoRect, "InfoCard");
			if (Widgets.ButtonImage(_rowRectSet.InfoRect, TexButton.Info, GUI.color, true, (string)null))
			{
				SoundStarter.PlayOneShotOnCamera(SoundDefOf.Click, (Map)null);
				Find.WindowStack.Add((Window)(object)func());
			}
		}

		private void RenderRowIcon(Thing thing)
		{
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Unknown result type (might be due to invalid IL or missing references)
			//IL_0106: Unknown result type (might be due to invalid IL or missing references)
			//IL_0246: Unknown result type (might be due to invalid IL or missing references)
			//IL_0258: Unknown result type (might be due to invalid IL or missing references)
			//IL_0259: Unknown result type (might be due to invalid IL or missing references)
			//IL_0268: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0217: Unknown result type (might be due to invalid IL or missing references)
			//IL_021d: Invalid comparison between Unknown and I4
			//IL_0142: Unknown result type (might be due to invalid IL or missing references)
			//IL_0161: Unknown result type (might be due to invalid IL or missing references)
			//IL_0167: Unknown result type (might be due to invalid IL or missing references)
			//IL_016c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0181: Unknown result type (might be due to invalid IL or missing references)
			//IL_0182: Unknown result type (might be due to invalid IL or missing references)
			//IL_0187: Unknown result type (might be due to invalid IL or missing references)
			thing = MinifyUtility.GetInnerIfMinified(thing);
			if ((Object)(object)((BuildableDef)thing.def).DrawMatSingle == (Object)null || (Object)(object)((BuildableDef)thing.def).DrawMatSingle.mainTexture == (Object)null)
			{
				return;
			}
			if (!ThingIconCache.ContainsKey(thing))
			{
				ThingIconCache.Add(thing, new ThingIconTextureData(thing.GetThingTexture(out var color), color));
			}
			ThingIconTextureData thingIconTextureData = ThingIconCache[thing];
			GUI.color = thingIconTextureData.Color;
			float num = 0f;
			Rect val = _rowRectSet.IconRect;
			if (!GenText.NullOrEmpty(((BuildableDef)thing.def).uiIconPath))
			{
				num = ((BuildableDef)thing.def).uiIconAngle;
				((Rect)(ref val)).position = ((Rect)(ref val)).position + new Vector2(((BuildableDef)thing.def).uiIconOffset.x * ((Rect)(ref val)).size.x, ((BuildableDef)thing.def).uiIconOffset.y * ((Rect)(ref val)).size.y);
			}
			else if ((thing is Pawn || thing is Corpse) ? true : false)
			{
				Pawn val2 = (Pawn)(((object)((thing is Pawn) ? thing : null)) ?? ((object)((Corpse)thing).InnerPawn));
				if (val2.RaceProps.Humanlike)
				{
					val = GenUI.ScaledBy(val, 1.8f);
					((Rect)(ref val)).y = ((Rect)(ref val)).y + 3f;
					val = GenUI.Rounded(val);
				}
			}
			Vector2 val3 = default(Vector2);
			((Vector2)(ref val3))..ctor((float)thingIconTextureData.Texture.width, (float)thingIconTextureData.Texture.height);
			Rect val4 = default(Rect);
			((Rect)(ref val4))..ctor(0f, 0f, 1f, 1f);
			if (thing.def.graphicData != null)
			{
				val3 = Vector2Utility.RotatedBy(thing.def.graphicData.drawSize, ((BuildableDef)thing.def).defaultPlacingRot);
				if (GenText.NullOrEmpty(((BuildableDef)thing.def).uiIconPath) && (int)thing.def.graphicData.linkFlags > 0)
				{
					((Rect)(ref val4))..ctor(0f, 0.5f, 0.25f, 0.25f);
				}
			}
			Widgets.DrawTextureFitted(val, thingIconTextureData.Texture, GenUI.IconDrawScale(thing.def), val3, val4, num, (Material)null, 1f);
			GUI.color = Color.white;
		}

		private static string ThingSortLabelPredicate(Thing t)
		{
			return ((Entity)t).Label;
		}

		private static QualityCategory ThingSortQualityPredicate(Thing t)
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			QualityCategory result = default(QualityCategory);
			QualityUtility.TryGetQuality(t, ref result);
			return result;
		}

		private static int ThingSortHitPointsPredicate(Thing t)
		{
			return t.HitPoints / t.MaxHitPoints;
		}

		private void DropThing(Thing thing)
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 val = ((ThingWithComps)Selected).GetComp<DsuDropAdjustComp>()?.CurrentCell ?? (((Thing)Selected).Position + new IntVec3(0, 0, -2));
			if (GenPlace.TryPlaceThing(thing.SplitOff(thing.stackCount), val, ((Thing)Selected).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)((IntVec3 vec) => !GenCollection.Any<Thing>(GridsUtility.GetThingList(vec, ((Thing)Selected).Map), (Predicate<Thing>)((Thing e) => e is DigitalStorageUnitBuilding))), (Rot4?)null, 1))
			{
				RecalculateList();
			}
		}
	}
	[StaticConstructorOnStartup]
	public class DigitalStorageUnitBuilding : Building_Storage, IForbidPawnInputItem, ISlotGroupParent, IStoreSettingsParent, IHaulDestination, global::IHoldMultipleThings.IHoldMultipleThings, ILwmDsLeaveMeAlonePlease, IRenameable
	{
		public readonly HashSet<DataExtenderBuilding> Extenders = new HashSet<DataExtenderBuilding>();

		private CompPowerTrader _compPowerTrader;

		private DsuHeaterComp _heaterComp;

		private bool _pawnAccess = true;

		private int _storedItemsCount;

		public HashSet<ABasePortDsuBuilding> Ports = new HashSet<ABasePortDsuBuilding>();

		public string UniqueName;

		private float Efficiency => DigitalStorageUnit.Config.HeaterEnabled ? 1.4f : 1f;

		public int GetSlotLimit => (int)((float)(DigitalStorageUnit.Config.BaseDsuStackSize + Extenders.Count((DataExtenderBuilding e) => e.Powered) * DigitalStorageUnit.Config.ExtenderStackIncrease) * Efficiency);

		public bool Powered
		{
			get
			{
				CompPowerTrader compPowerTrader = _compPowerTrader;
				return compPowerTrader != null && compPowerTrader.PowerOn;
			}
		}

		public bool CanWork => Powered && ((Thing)this).Spawned && _heaterComp.IsRoomHermetic();

		public override string LabelNoCount => UniqueName ?? ((ThingWithComps)this).LabelNoCount;

		public override string LabelCap => UniqueName ?? ((Thing)this).LabelCap;

		public bool ForbidPawnInput => !_pawnAccess || GetSlotLimit <= _storedItemsCount;

		public string RenamableLabel
		{
			get
			{
				return UniqueName ?? ((Thing)this).LabelCapNoCount;
			}
			set
			{
				UniqueName = value;
			}
		}

		public string BaseLabel => ((Thing)this).LabelCapNoCount;

		public string InspectLabel => ((Entity)this).LabelCap;

		public override void Notify_ReceivedThing(Thing newItem)
		{
			((Building_Storage)this).Notify_ReceivedThing(newItem);
			RearrangeItems();
			UpdatePowerConsumption();
		}

		public override void Notify_LostThing(Thing newItem)
		{
			((Building_Storage)this).Notify_LostThing(newItem);
			RearrangeItems();
			UpdatePowerConsumption();
		}

		public bool CapacityAt(Thing thing, IntVec3 cell, Map map, out int capacity)
		{
			capacity = 0;
			if (thing == null || map == null || map != ((Thing)this).Map || !CanWork)
			{
				return false;
			}
			thing = MinifyUtility.GetInnerIfMinified(thing);
			if (!((Building_Storage)this).Accepts(thing))
			{
				return false;
			}
			foreach (Thing storedThing in GetStoredThings())
			{
				if (storedThing.def == thing.def && (!((BuildableDef)thing.def).MadeFromStuff || storedThing.Stuff == thing.Stuff))
				{
					capacity += thing.def.stackLimit - storedThing.stackCount;
				}
			}
			capacity += (GetSlotLimit - _storedItemsCount) * thing.def.stackLimit;
			return capacity > 0;
		}

		public bool StackableAt(Thing thing, IntVec3 cell, Map map)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			int capacity;
			return CapacityAt(thing, cell, map, out capacity);
		}

		public bool CanReceiveThing(Thing item)
		{
			return CanWork && GetSlotLimit > _storedItemsCount && ((Building_Storage)this).Accepts(item);
		}

		public List<Thing> GetStoredThings()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			return (from t in ((Thing)this).Map.thingGrid.ThingsListAtFast(((Thing)this).Position)
				where t.def.EverStorable(false) && (object)t != this
				select t).ToList();
		}

		public override void ExposeData()
		{
			((Building_Storage)this).ExposeData();
			Scribe_Collections.Look<ABasePortDsuBuilding>(ref Ports, "ports", (LookMode)3);
			Scribe_Values.Look<string>(ref UniqueName, "uniqueName", (string)null, false);
			Scribe_Values.Look<bool>(ref _pawnAccess, "pawnAccess", true, false);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building_Storage)this).SpawnSetup(map, respawningAfterLoad);
			if (_compPowerTrader == null)
			{
				_compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			}
			if (_heaterComp == null)
			{
				_heaterComp = ((ThingWithComps)this).GetComp<DsuHeaterComp>();
			}
			map.GetDsuComponent().RegisterBuilding((Building)(object)this);
			foreach (ABasePortDsuBuilding port in Ports)
			{
				if (port != null && ((Thing)port).Spawned && ((Thing)port).Map != map)
				{
					port.BoundStorageUnit = null;
				}
			}
			RearrangeItems();
		}

		public override void PostMapInit()
		{
			((ThingWithComps)this).PostMapInit();
			if (_compPowerTrader == null)
			{
				_compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			}
			if (_heaterComp == null)
			{
				_heaterComp = ((ThingWithComps)this).GetComp<DsuHeaterComp>();
			}
			RearrangeItems();
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Invalid comparison between Unknown and I4
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			foreach (Thing storedThing in GetStoredThings())
			{
				if ((int)mode == 4)
				{
					storedThing.Destroy((DestroyMode)0);
					continue;
				}
				Log.Warning($"DSU despawn mode: {mode}");
				((Entity)storedThing).DeSpawn((DestroyMode)0);
				GenPlace.TryPlaceThing(storedThing, ((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			}
			((Thing)this).Map.GetDsuComponent().DeregisterBuilding((Building)(object)this);
			((Building_Storage)this).DeSpawn(mode);
		}

		private void RearrangeItems()
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			foreach (IntVec3 item in ((Building_Storage)this).AllSlotCells())
			{
				if (item == ((Thing)this).Position)
				{
					continue;
				}
				foreach (Thing item2 in ((Thing)this).Map.thingGrid.ThingsListAtFast(item).ToList())
				{
					if (item2.def.EverStorable(false) && (object)item2 != this && item2.Position != ((Thing)this).Position)
					{
						HandleNewItem(item2);
					}
				}
				foreach (ITab_Items item3 in ((Thing)this).GetInspectTabs().OfType<ITab_Items>())
				{
					item3.RecalculateList();
				}
			}
		}

		protected override void Tick()
		{
			((ThingWithComps)this).Tick();
			_storedItemsCount = GetStoredThings().Count;
		}

		public void HandleNewItem(Thing item, bool tryAbsorb = true)
		{
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ba: Invalid comparison between Unknown and I4
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Invalid comparison between Unknown and I4
			if (item.Destroyed)
			{
				return;
			}
			if (tryAbsorb)
			{
				foreach (Thing storedThing in GetStoredThings())
				{
					if (storedThing != item)
					{
						storedThing.TryAbsorbStack(item, true);
						if (item.Destroyed)
						{
							return;
						}
					}
				}
			}
			if (CanReceiveThing(item))
			{
				item.Position = ((Thing)this).Position;
				if (!item.Spawned)
				{
					((Entity)item).SpawnSetup(((Thing)this).Map, false);
				}
				DrawerType drawerType = item.def.drawerType;
				if (((int)drawerType == 1 || (int)drawerType == 3) ? true : false)
				{
					((Thing)this).Map.dynamicDrawManager.DeRegisterDrawable(item);
				}
			}
		}

		private void UpdatePowerConsumption()
		{
			_compPowerTrader.powerOutputInt = (float)(-1 * _storedItemsCount) * DigitalStorageUnit.Config.EnergyPerStack * (DigitalStorageUnit.Config.HeaterEnabled ? 0.9f : 1f);
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			yield return (Gizmo)new Command_Action
			{
				icon = (Texture)(object)TextureHolder.Rename,
				action = delegate
				{
					Find.WindowStack.Add((Window)(object)new RenameDsuDialog(this));
				},
				hotKey = KeyBindingDefOf.Misc1,
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("DSU.Rename")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("DSU.Rename.Desc"))
			};
			yield return (Gizmo)new Command_Toggle
			{
				isActive = () => _pawnAccess,
				toggleAction = delegate
				{
					_pawnAccess = !_pawnAccess;
				},
				icon = (Texture)(object)TextureHolder.StoragePawnAccessSwitchIcon,
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("DSU.PawnAccess")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("DSU.PawnAccess.Desc"))
			};
		}

		public override string GetInspectString()
		{
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			string inspectString = ((Building_Storage)this).GetInspectString();
			StringBuilder stringBuilder = new StringBuilder();
			if (!string.IsNullOrEmpty(inspectString))
			{
				stringBuilder.AppendLine(inspectString);
			}
			stringBuilder.Append(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DSU.TotalStackNum", NamedArgument.op_Implicit(_storedItemsCount))));
			return stringBuilder.ToString();
		}

		public override void DrawGUIOverlay()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Invalid comparison between Unknown and I4
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).DrawGUIOverlay();
			if ((int)Current.CameraDriver.CurrentZoom <= 1)
			{
				GenMapUI.DrawThingLabel((Thing)(object)this, TaggedString.op_Implicit(((Entity)this).LabelCap + "\n\r" + TranslatorFormattedStringExtensions.Translate("DSU.StacksCount.Detailed", NamedArgument.op_Implicit(_storedItemsCount), NamedArgument.op_Implicit(GetSlotLimit))));
			}
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building_Storage)this).GetGizmos();
		}
	}
	[StaticConstructorOnStartup]
	public abstract class ABasePortDsuBuilding : Building_Storage, IForbidPawnInputItem, ISlotGroupParent, IStoreSettingsParent, IHaulDestination, ILwmDsLeaveMeAlonePlease
	{
		private CompPowerTrader _powerTrader;

		private DigitalStorageUnitBuilding _boundStorageUnit;

		protected IntVec3 WorkPosition => ((Thing)this).Position;

		public bool Powered
		{
			get
			{
				CompPowerTrader powerTrader = _powerTrader;
				return powerTrader != null && powerTrader.PowerOn;
			}
		}

		public virtual StorageIOMode IOMode => StorageIOMode.Input;

		public DigitalStorageUnitBuilding BoundStorageUnit
		{
			get
			{
				return _boundStorageUnit;
			}
			set
			{
				_boundStorageUnit?.Ports.Remove(this);
				_boundStorageUnit = value;
				value?.Ports.Add(this);
			}
		}

		public virtual bool ForbidPawnInput => !Powered || BoundStorageUnit == null;

		public override void ExposeData()
		{
			((Building_Storage)this).ExposeData();
			Scribe_References.Look<DigitalStorageUnitBuilding>(ref _boundStorageUnit, "boundStorageUnit", false);
		}

		public override void PostMake()
		{
			((Building_Storage)this).PostMake();
			if (_powerTrader == null)
			{
				_powerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			((Building_Storage)this).SpawnSetup(map, respawningAfterLoad);
			if (_powerTrader == null)
			{
				_powerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			}
			DigitalStorageUnitBuilding boundStorageUnit = BoundStorageUnit;
			if (((boundStorageUnit != null) ? ((Thing)boundStorageUnit).Map : null) != map)
			{
				DigitalStorageUnitBuilding boundStorageUnit2 = BoundStorageUnit;
				if (boundStorageUnit2 != null && ((Thing)boundStorageUnit2).Spawned)
				{
					BoundStorageUnit = null;
				}
			}
			if (DigitalStorageUnit.Config.AutoBoundDsu && BoundStorageUnit == null && !respawningAfterLoad)
			{
				HashSet<DigitalStorageUnitBuilding> hashSet = ((Thing)this).Map.listerBuildings.AllBuildingsColonistOfClass<DigitalStorageUnitBuilding>().ToHashSet();
				if (hashSet.Count == 1)
				{
					BoundStorageUnit = hashSet.FirstOrDefault();
					Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DSU.Message.AutoBoundTo", NamedArgument.op_Implicit(((Entity)BoundStorageUnit).LabelCap))), LookTargets.op_Implicit((Thing)(object)this), MessageTypeDefOf.PositiveEvent, true);
				}
			}
			map.GetDsuComponent().RegisterBuilding((Building)(object)this);
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			BoundStorageUnit?.Ports.Remove(this);
			((Thing)this).Map.GetDsuComponent().DeregisterBuilding((Building)(object)this);
			((Building_Storage)this).DeSpawn(mode);
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			Command_Action val = new Command_Action();
			TaggedString val2 = Translator.Translate("DSU.ItemSource") + ": ";
			DigitalStorageUnitBuilding boundStorageUnit = BoundStorageUnit;
			((Command)val).defaultLabel = TaggedString.op_Implicit(val2 + (((boundStorageUnit != null) ? ((Entity)boundStorageUnit).LabelCap : null) ?? TaggedString.op_Implicit(Translator.Translate("NoneBrackets"))));
			val.action = delegate
			{
				//IL_0087: Unknown result type (might be due to invalid IL or missing references)
				//IL_0091: Expected O, but got Unknown
				//IL_005f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0076: Unknown result type (might be due to invalid IL or missing references)
				//IL_0080: Expected O, but got Unknown
				List<FloatMenuOption> list = ((Thing)this).Map.listerBuildings.allBuildingsColonist.Where((Building b) => b is DigitalStorageUnitBuilding).Select((Func<Building, FloatMenuOption>)((Building b) => new FloatMenuOption(((Entity)b).LabelCap, (Action)delegate
				{
					SelectedPorts().ToList().ForEach(delegate(ABasePortDsuBuilding p)
					{
						p.BoundStorageUnit = b as DigitalStorageUnitBuilding;
					});
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0))).ToList();
				if (list.Count == 0)
				{
					list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("NoneBrackets")), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
				}
				Find.WindowStack.Add((Window)new FloatMenu(list));
			};
			((Command)val).icon = (Texture)(object)TextureHolder.CargoPlatform;
			yield return (Gizmo)val;
		}

		private IEnumerable<ABasePortDsuBuilding> SelectedPorts()
		{
			List<ABasePortDsuBuilding> list = Find.Selector.SelectedObjects.OfType<ABasePortDsuBuilding>().ToList();
			if (!list.Contains(this))
			{
				list.Add(this);
			}
			return list;
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building_Storage)this).GetGizmos();
		}
	}
}
namespace DigitalStorageUnit._harmony
{
	public static class SubPatch_AdaptiveStorage_AddITabToLegacyStorages_Postfix
	{
		public static bool Prefix(object[] __args, ref IEnumerable<InspectTabBase>? __result)
		{
			object obj = __args[1];
			Thing val = (Thing)((obj is Thing) ? obj : null);
			if ((val != null && (val is IRemoveStorageInspectionTab || val is DigitalStorageUnitBuilding)) ? true : false)
			{
				__result = __args[0] as IEnumerable<InspectTabBase>;
				return false;
			}
			return true;
		}
	}
	public static class SubPatch_Open_DS_Tab_On_Select_Postfix
	{
		[HarmonyPrefix]
		public static bool Prefix()
		{
			Thing singleSelectedThing = Find.Selector.SingleSelectedThing;
			if (!(singleSelectedThing is ThingWithComps))
			{
				return true;
			}
			if (!singleSelectedThing.def.thingClass.GetInterfaces().Contains(typeof(ILwmDsLeaveMeAlonePlease)))
			{
				return true;
			}
			MainTabWindow tabWindow = MainButtonDefOf.Inspect.TabWindow;
			MainTabWindow_Inspect tabWindowInspect = (MainTabWindow_Inspect)(object)((tabWindow is MainTabWindow_Inspect) ? tabWindow : null);
			if (tabWindowInspect != null && (object)tabWindowInspect.OpenTabType != null && singleSelectedThing.GetInspectTabs().Any((InspectTabBase t) => ((object)t).GetType() == tabWindowInspect.OpenTabType))
			{
				return true;
			}
			if (singleSelectedThing.def.thingClass == typeof(DigitalStorageUnitBuilding))
			{
			}
			return false;
		}
	}
	public static class Disabled_Patch_Building_Storage_Accepts
	{
		private static bool Prefix(Building_Storage __instance, Thing t, out bool __result)
		{
			__result = false;
			if (__instance is IForbidPawnInputItem { ForbidPawnInput: not false })
			{
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(Building_Storage))]
	public static class Patch_Building_Storage
	{
		[HarmonyPatch(typeof(Building_Storage))]
		public static class InnerPatch_GetGizmos
		{
			[HarmonyTargetMethod]
			public static MethodBase TargetMethod()
			{
				Type type = typeof(Building_Storage).GetNestedTypes(AccessTools.all).FirstOrDefault((Type t) => t.FullName.Contains("d__54"));
				if (type == null)
				{
					Log.Error("DSU Harmony Error - predicateClass == null for Patch_Building_Storage_GetGizmos.TargetMethod()");
					return null;
				}
				MethodInfo methodInfo = type.GetMethods(AccessTools.all).FirstOrDefault((MethodInfo t) => t.Name.Contains("MoveNext"));
				if (methodInfo == null)
				{
					Log.Error("DSU Harmony Error - methodInfo == null for Patch_Building_Storage_GetGizmos.TargetMethod()");
				}
				return methodInfo;
			}

			[HarmonyTranspiler]
			public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				bool Found_get_NumSelected = false;
				object endJumpMarker = null;
				bool patched = false;
				foreach (CodeInstruction instruction in instructions)
				{
					if (instruction.opcode == OpCodes.Callvirt && instruction.operand.ToString().Contains("get_NumSelected()"))
					{
						Found_get_NumSelected = true;
					}
					if (Found_get_NumSelected && endJumpMarker == null && instruction.opcode == OpCodes.Bne_Un)
					{
						endJumpMarker = instruction.operand;
					}
					if (!patched && Found_get_NumSelected && instruction.opcode == OpCodes.Ldarg_0)
					{
						yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null);
						yield return new CodeInstruction(OpCodes.Ldloc_2, (object)null);
						yield return new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Patch_Building_Storage), "IsDsuBuilding", (Type[])null, (Type[])null));
						yield return new CodeInstruction(OpCodes.Brtrue_S, endJumpMarker);
						patched = true;
						Log.Message("DSU: Patch_Building_Storage_GetGizmos OK");
					}
					yield return instruction;
				}
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch("GetInspectString")]
		public static IEnumerable<CodeInstruction> GetInspectString(IEnumerable<CodeInstruction> instructions)
		{
			int brFalseCount = 0;
			bool patched = false;
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Brfalse)
				{
					brFalseCount++;
				}
				if (brFalseCount == 2 && !patched)
				{
					yield return instruction;
					yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null);
					yield return new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Patch_Building_Storage), "IsDsuBuilding", (Type[])null, (Type[])null));
					yield return new CodeInstruction(OpCodes.Brtrue_S, instruction.operand);
					patched = true;
					Log.Message("DSU: Patch_Buildin_Storage_GetInspectString OK");
				}
				else
				{
					yield return instruction;
				}
			}
		}

		private static bool IsDsuBuilding(Building_Storage building)
		{
			return building is DigitalStorageUnitBuilding;
		}

		[HarmonyPrefix]
		[HarmonyPatch("get_GroupingLabel")]
		public static bool get_GroupingLabel(Building_Storage __instance, ref string __result)
		{
			if (__instance is DigitalStorageUnitBuilding digitalStorageUnitBuilding)
			{
				__result = ((Thing)digitalStorageUnitBuilding).LabelNoCount;
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(JobDriver_HaulToContainer))]
	public static class Patch_JobDriver_HaulToContainer
	{
		[HarmonyPostfix]
		[HarmonyPatch("MakeNewToils")]
		public static void MakeNewToils(JobDriver_HaulToContainer __instance, ref IEnumerable<Toil> __result)
		{
			List<Toil> list = new List<Toil>();
			foreach (Toil item in __result)
			{
				if (item.debugName == "GotoThing")
				{
					Toil val = Toils_General.Do((Action)delegate
					{
						//IL_000d: Unknown result type (might be due to invalid IL or missing references)
						//IL_0012: Unknown result type (might be due to invalid IL or missing references)
						//IL_0027: Unknown result type (might be due to invalid IL or missing references)
						//IL_002c: Unknown result type (might be due to invalid IL or missing references)
						//IL_0041: Unknown result type (might be due to invalid IL or missing references)
						//IL_0047: Unknown result type (might be due to invalid IL or missing references)
						//IL_005e: Unknown result type (might be due to invalid IL or missing references)
						LocalTargetInfo target = ((JobDriver)__instance).job.GetTarget((TargetIndex)1);
						Thing thing = ((LocalTargetInfo)(ref target)).Thing;
						target = ((JobDriver)__instance).job.GetTarget((TargetIndex)2);
						Thing thing2 = ((LocalTargetInfo)(ref target)).Thing;
						ReachabilityPatchResult reachabilityPatchResult = Patch_Reachability.CanReachAndFindAccessPoint(((JobDriver)__instance).pawn, LocalTargetInfo.op_Implicit(thing), thing2.Position, (PathEndMode)3, TraverseParms.For(((JobDriver)__instance).pawn, (Danger)3, (TraverseMode)0, false, false, false, true));
						if (reachabilityPatchResult.Dsu != null && reachabilityPatchResult.AccessPoint != null)
						{
							reachabilityPatchResult.AccessPoint.ProvideItem(((LocalTargetInfo)(ref ((JobDriver)__instance).job.targetA)).Thing);
						}
					});
					val.debugName = "DSU: MakeNewToils";
					list.Add(val);
				}
				list.Add(item);
			}
			__result = list;
		}
	}
	[HarmonyPatch(typeof(MouseoverReadout))]
	public static class Patch_MouseoverReadout
	{
		[HarmonyTranspiler]
		[HarmonyPatch("MouseoverReadoutOnGUI")]
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Expected O, but got Unknown
			//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c6: Expected O, but got Unknown
			CodeMatcher val = new CodeMatcher(instructions, (ILGenerator)null);
			val.MatchEndForward((CodeMatch[])(object)new CodeMatch[4]
			{
				CodeMatch.IsLdloc((LocalBuilder)null),
				CodeMatch.Calls(AccessTools.PropertyGetter(typeof(Find), "CurrentMap")),
				CodeMatch.Calls(AccessTools.Method(typeof(GridsUtility), "GetThingList", (Type[])null, (Type[])null)),
				CodeMatch.IsStloc((LocalBuilder)null)
			});
			if (!val.IsValid)
			{
				Log.Error("DSU: Patch_MouseoverReadout: Failed to find matching load local cell");
				return val.InstructionEnumeration();
			}
			val.Advance(1);
			val.Insert((CodeInstruction[])(object)new CodeInstruction[3]
			{
				CodeInstruction.LoadLocal(4, false),
				(CodeInstruction)new CodeMatch((OpCode?)OpCodes.Call, (object)AccessTools.Method(typeof(Patch_MouseoverReadout), "Instrumented_FixList", (Type[])null, (Type[])null), (string)null),
				CodeInstruction.StoreLocal(4)
			});
			return val.InstructionEnumeration();
		}

		public static List<Thing> Instrumented_FixList(List<Thing> list)
		{
			if (DigitalStorageUnit.Config.CleanCellItemList && GenCollection.Any<Thing>(list, (Predicate<Thing>)((Thing t) => t is DigitalStorageUnitBuilding)))
			{
				return list.Where((Thing t) => ((object)t).GetType() == typeof(DigitalStorageUnitBuilding)).ToList();
			}
			return list;
		}
	}
	[HarmonyPatch(typeof(CompressibilityDeciderUtility))]
	public static class Patch_CompressibilityDeciderUtility
	{
		[HarmonyPrefix]
		[HarmonyPatch("IsSaveCompressible")]
		public static bool IsSaveCompressible(Thing t, ref bool __result)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			if (t.Map == null)
			{
				return true;
			}
			IntVec3 position = t.Position;
			if (!((IntVec3)(ref position)).IsValid)
			{
				return true;
			}
			if (t.Position.GetFirst<DigitalStorageUnitBuilding>(t.Map) == null)
			{
				return true;
			}
			__result = false;
			return false;
		}
	}
	[HarmonyPatch(typeof(FloatMenuMakerMap))]
	public static class Patch_FloatMenuMakerMap
	{
		[HarmonyPrefix]
		[HarmonyPatch("GetOptions")]
		private static bool GetOptions(Vector3 clickPos, ref List<FloatMenuOption> __result, out FloatMenuContext context)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			if (Find.CurrentMap.IsDSUOnPoint(IntVec3Utility.ToIntVec3(clickPos)))
			{
				__result = new List<FloatMenuOption>();
				context = null;
				return false;
			}
			__result = null;
			context = null;
			return true;
		}
	}
	[HarmonyPatch(typeof(FoodUtility))]
	public class Patch_FoodUtility
	{
		[HarmonyPrefix]
		[HarmonyPatch("SpawnedFoodSearchInnerScan")]
		public static bool SpawnedFoodSearchInnerScan(Pawn eater, IntVec3 root, IReadOnlyList<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, out Thing __result)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
			__result = null;
			if (searchSet == null)
			{
				return true;
			}
			Pawn val = traverseParams.pawn ?? eater;
			float num = float.MinValue;
			DsuMapComponent dsuComponent = ((Thing)val).Map.GetDsuComponent();
			if (dsuComponent == null)
			{
				return true;
			}
			Dictionary<IntVec3, ReachabilityPatchResult> dictionary = new Dictionary<IntVec3, ReachabilityPatchResult>();
			foreach (Thing item in searchSet)
			{
				if (!item.Spawned || (validator != null && !validator(item)))
				{
					continue;
				}
				ReachabilityPatchResult reachabilityPatchResult = (dictionary.ContainsKey(item.Position) ? GenCollection.TryGetValue<IntVec3, ReachabilityPatchResult>((IReadOnlyDictionary<IntVec3, ReachabilityPatchResult>)dictionary, item.Position, (ReachabilityPatchResult)null) : Patch_Reachability.CanReachAndFindAccessPoint(val, LocalTargetInfo.op_Implicit(item.Position), ((Thing)eater).Position, peMode, traverseParams));
				dictionary[item.Position] = reachabilityPatchResult;
				if (reachabilityPatchResult.Dsu != null && reachabilityPatchResult.AccessPoint != null)
				{
					if (reachabilityPatchResult.DirectDistanceToAccessPoint > maxDistance)
					{
						continue;
					}
					float num2 = FoodUtility.FoodOptimality(eater, item, FoodUtility.GetFinalIngestibleDef(item, false), reachabilityPatchResult.DirectDistanceToAccessPoint, false);
					if (num2 < num || (validator != null && !validator(item)))
					{
						continue;
					}
					num = num2;
				}
				else
				{
					if (reachabilityPatchResult.DirectDistanceToTarget > maxDistance)
					{
						continue;
					}
					float num3 = FoodUtility.FoodOptimality(eater, item, FoodUtility.GetFinalIngestibleDef(item, false), reachabilityPatchResult.DirectDistanceToTarget, false);
					if (num3 < num || !reachabilityPatchResult.OriginalCanReach)
					{
						continue;
					}
					num = num3;
				}
				__result = item;
			}
			return false;
		}
	}
	[HarmonyPatch(typeof(ForbidUtility))]
	public static class Patch_ForbidUtility
	{
		[HarmonyPrefix]
		[HarmonyPatch("IsForbidden", new Type[]
		{
			typeof(Thing),
			typeof(Pawn)
		})]
		private static bool IsForbidden(Thing t, Pawn pawn, out bool __result)
		{
			__result = true;
			return true;
		}
	}
	[HarmonyPatch(typeof(MainTabWindow_Inspect))]
	public static class Patch_MainTabWindow_Inspect
	{
		private static ITab_Items cachedITabItems;

		[HarmonyPrefix]
		[HarmonyPatch("set_OpenTabType")]
		public static void OpenTabType_Setter(Type value)
		{
			if (value == typeof(ITab_Items))
			{
				cachedITabItems = Find.Selector.SingleSelectedThing.GetInspectTabs().OfType<ITab_Items>().FirstOrDefault();
			}
			if (cachedITabItems != null && (object)value == null)
			{
				cachedITabItems.OnClose();
				cachedITabItems = null;
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch("CloseOpenTab")]
		public static void CloseOpenTab()
		{
			cachedITabItems?.OnClose();
			cachedITabItems = null;
		}
	}
	[HarmonyPatch(typeof(MinifiedThing))]
	public class Patch_MinifiedThing
	{
		[HarmonyPrefix]
		[HarmonyPatch("Print", new Type[] { typeof(SectionLayer) })]
		private static bool Prefix(Thing __instance)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			return !__instance.def.EverStorable(false) || !__instance.Map.IsDSUOnPoint(__instance.Position);
		}
	}
	[HarmonyPatch]
	public static class Patch_PassingShip
	{
		private static Type hiddenClass;

		[HarmonyTargetMethod]
		public static MethodBase TargetMethod()
		{
			hiddenClass = typeof(PassingShip).GetNestedTypes(AccessTools.all).FirstOrDefault((Type t) => t.FullName.Contains("c__DisplayClass23_0"));
			if (hiddenClass == null)
			{
				Log.Error("DSU Harmony Error - predicateClass == null for Patch_PassingShip_DSUisTradebeacon.TargetMethod()");
				return null;
			}
			MethodInfo methodInfo = hiddenClass.GetMethods(AccessTools.all).FirstOrDefault((MethodInfo t) => t.Name.Contains("b__1"));
			if (methodInfo == null)
			{
				Log.Error("DSU Harmony Error - methodInfo == null for Patch_PassingShip_DSUisTradebeacon.TargetMethod()");
			}
			return methodInfo;
		}

		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			bool foundLocaterString = false;
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Call && instruction.operand as MethodInfo == AccessTools.Method(typeof(Building_OrbitalTradeBeacon), "AllPowered", (Type[])null, (Type[])null))
				{
					foundLocaterString = true;
				}
				if (instruction.opcode == OpCodes.Brtrue_S && foundLocaterString)
				{
					foundLocaterString = false;
					yield return instruction;
					yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null);
					yield return new CodeInstruction(OpCodes.Ldfld, (object)AccessTools.Field(hiddenClass, "<>4__this"));
					yield return new CodeInstruction(OpCodes.Call, (object)AccessTools.PropertyGetter(typeof(PassingShip), "Map"));
					yield return new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Patch_PassingShip), "AnyPowered", new Type[1] { typeof(Map) }, (Type[])null));
					yield return new CodeInstruction(OpCodes.Brtrue_S, instruction.operand);
					Log.Message("DSU: Patch_PassingShip_c__DisplayClass24_0 OK");
				}
				else
				{
					yield return instruction;
				}
			}
		}

		public static bool AnyPowered(Map map)
		{
			return map.GetAllPoweredDSU(any: true).Any();
		}
	}
	[HarmonyPatch(typeof(Pawn_JobTracker))]
	public class Patch_Pawn_JobTracker
	{
		[HarmonyPrefix]
		[HarmonyPatch("StartJob")]
		public static bool StartJob(Job newJob, ref Pawn ___pawn, JobCondition lastJobEndCondition = (JobCondition)0, ThinkNode jobGiver = null, bool resumeCurJobAfterwards = false, bool cancelBusyStances = true, ThinkTreeDef thinkTree = null, JobTag? tag = null, bool fromQueue = false, bool canReturnCurJobToPool = false)
		{
			//IL_011a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_011f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			//IL_0126: Unknown result type (might be due to invalid IL or missing references)
			//IL_012b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_0172: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_020e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0213: Unknown result type (might be due to invalid IL or missing references)
			//IL_021d: Unknown result type (might be due to invalid IL or missing references)
			Pawn obj = ___pawn;
			Faction val = ((obj != null) ? ((Thing)obj).Faction : null);
			if (val == null || !val.IsPlayer)
			{
				return true;
			}
			if (((Def)newJob.def).defName == "HaulToInventory")
			{
				return true;
			}
			Thing thing = ((LocalTargetInfo)(ref newJob.targetA)).Thing;
			bool? obj2;
			if (thing == null)
			{
				obj2 = null;
			}
			else
			{
				ThingDef def = thing.def;
				obj2 = ((def != null) ? new bool?(def.EverStorable(false)) : ((bool?)null));
			}
			bool? flag = obj2;
			bool valueOrDefault = flag == true;
			IntVec3 val2;
			if (valueOrDefault)
			{
				Thing thing2 = ((LocalTargetInfo)(ref newJob.targetB)).Thing;
				val2 = ((thing2 != null) ? thing2.Position : ((LocalTargetInfo)(ref newJob.targetB)).Cell);
				if (val2 == IntVec3.Invalid)
				{
					val2 = ((Thing)___pawn).Position;
				}
				if (newJob.targetA == LocalTargetInfo.op_Implicit((Thing)null))
				{
					return true;
				}
			}
			else
			{
				Thing thing3 = ((LocalTargetInfo)(ref newJob.targetA)).Thing;
				val2 = ((thing3 != null) ? thing3.Position : ((LocalTargetInfo)(ref newJob.targetA)).Cell);
				if (newJob.targetB == LocalTargetInfo.op_Implicit(IntVec3.Invalid) && (newJob.targetQueueB == null || newJob.targetQueueB.Count == 0))
				{
					return true;
				}
			}
			List<LocalTargetInfo> list = (valueOrDefault ? new List<LocalTargetInfo>(1) { newJob.targetA } : ((newJob.targetQueueB != null && newJob.targetQueueB.Count != 0) ? newJob.targetQueueB : new List<LocalTargetInfo>(1) { newJob.targetB }));
			DsuMapComponent dsuComponent = ((Thing)___pawn).Map.GetDsuComponent();
			if (dsuComponent == null)
			{
				return true;
			}
			foreach (LocalTargetInfo item in list)
			{
				LocalTargetInfo current = item;
				if (((LocalTargetInfo)(ref current)).HasThing)
				{
					ReachabilityPatchResult reachabilityPatchResult = Patch_Reachability.CanReachAndFindAccessPoint(___pawn, LocalTargetInfo.op_Implicit(((LocalTargetInfo)(ref current)).Thing), val2, (PathEndMode)2, TraverseParms.For(___pawn, (Danger)3, (TraverseMode)0, false, false, false, true));
					if (reachabilityPatchResult.Dsu != null && reachabilityPatchResult.AccessPoint != null)
					{
						reachabilityPatchResult.AccessPoint.ProvideItem(((LocalTargetInfo)(ref current)).Thing);
					}
				}
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(Reachability))]
	public static class Patch_Reachability
	{
		private static ReachabilityPatchResult _cachedResult;

		public static ReachabilityPatchResult CanReachAndFindAccessPoint(Pawn pawn, LocalTargetInfo middlePoint, IntVec3 jobDestination, PathEndMode peMode, TraverseParms traverseParams)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			_cachedResult = new ReachabilityPatchResult
			{
				JobDestination = jobDestination,
				JobPawn = pawn
			};
			ReachabilityPatchResult cachedResult = _cachedResult;
			IntVec3 val = ((Thing)_cachedResult.JobPawn).Position - ((LocalTargetInfo)(ref middlePoint)).Cell;
			cachedResult.DirectDistanceToTarget = ((IntVec3)(ref val)).LengthManhattan;
			((Thing)pawn).Map.reachability.CanReach(((Thing)pawn).Position, middlePoint, peMode, traverseParams);
			try
			{
				return _cachedResult;
			}
			finally
			{
				_cachedResult = null;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch("CanReach", new Type[]
		{
			typeof(IntVec3),
			typeof(LocalTargetInfo),
			typeof(PathEndMode),
			typeof(TraverseParms)
		})]
		public static void CanReach(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams, ref bool __result, Map ___map, Reachability __instance)
		{
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_011a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0124: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_0143: Unknown result type (might be due to invalid IL or missing references)
			//IL_0148: Unknown result type (might be due to invalid IL or missing references)
			//IL_014e: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
			if (_cachedResult == null)
			{
				return;
			}
			_cachedResult.OriginalCanReach = __result;
			if (_cachedResult.JobDestination == IntVec3.Invalid || _cachedResult.JobPawn == null)
			{
				return;
			}
			Thing thing = ((LocalTargetInfo)(ref dest)).Thing;
			if (thing == null || !thing.def.EverStorable(false))
			{
				return;
			}
			DsuMapComponent dsuComponent = ___map.GetDsuComponent();
			DigitalStorageUnitBuilding digitalStorageUnitBuilding = dsuComponent?.GetDsuHoldingItem(((LocalTargetInfo)(ref dest)).Thing);
			if (digitalStorageUnitBuilding == null || !digitalStorageUnitBuilding.CanWork)
			{
				return;
			}
			float num = (__result ? dsuComponent.GetTotalDistance(_cachedResult.JobPawn, ((LocalTargetInfo)(ref dest)).Cell, _cachedResult.JobDestination, isDsu: true) : float.MaxValue);
			_cachedResult.DirectPathingWeight = num;
			AccessPointPortBuilding accessPointPortBuilding = null;
			foreach (AccessPointPortBuilding accessPoint in dsuComponent.GetAccessPoints(digitalStorageUnitBuilding))
			{
				float totalDistance = dsuComponent.GetTotalDistance(_cachedResult.JobPawn, ((Thing)accessPoint).Position, _cachedResult.JobDestination);
				if (!(num < totalDistance) && __instance.CanReach(start, LocalTargetInfo.op_Implicit(((Thing)accessPoint).Position), (PathEndMode)2, traverseParams))
				{
					num = totalDistance;
					accessPointPortBuilding = accessPoint;
				}
			}
			_cachedResult.Dsu = digitalStorageUnitBuilding;
			_cachedResult.AccessPoint = accessPointPortBuilding;
			_cachedResult.BestPathingWeight = num;
			ReachabilityPatchResult cachedResult = _cachedResult;
			int num2;
			if (accessPointPortBuilding != null)
			{
				IntVec3 val = ((Thing)_cachedResult.JobPawn).Position - ((Thing)accessPointPortBuilding).Position;
				num2 = ((IntVec3)(ref val)).LengthManhattan;
			}
			else
			{
				num2 = 0;
			}
			cachedResult.DirectDistanceToAccessPoint = num2;
			__result = __result || _cachedResult.AccessPoint != null;
		}
	}
	public class ReachabilityPatchResult
	{
		public IntVec3 JobDestination;

		public Pawn JobPawn;

		public DigitalStorageUnitBuilding Dsu;

		public AccessPointPortBuilding AccessPoint;

		public float BestPathingWeight;

		public float DirectPathingWeight;

		public float DirectDistanceToTarget;

		public float DirectDistanceToAccessPoint;

		public bool OriginalCanReach;
	}
	[HarmonyPatch(typeof(ReservationManager))]
	public static class Patch_Reservation
	{
		[HarmonyPrefix]
		[HarmonyPatch("Reserve")]
		public static bool Reserve(Pawn claimant, Job job, LocalTargetInfo target, ref bool __result, Map ___map)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			if (((LocalTargetInfo)(ref target)).HasThing || ___map == null || !GenGrid.InBounds(((LocalTargetInfo)(ref target)).Cell, ___map))
			{
				return true;
			}
			Thing val = GenCollection.FirstOrDefault<Thing>(GridsUtility.GetThingList(((LocalTargetInfo)(ref target)).Cell, ___map), (Predicate<Thing>)((Thing t) => t is ABasePortDsuBuilding));
			if (val is ABasePortDsuBuilding { IOMode: StorageIOMode.Input })
			{
				__result = true;
				return false;
			}
			return true;
		}
	}
	public static class Disabled_Patch_StorageSettings
	{
		public static bool Prefix(IStoreSettingsParent ___owner, Thing t, out bool __result)
		{
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			__result = false;
			Building_Storage val = (Building_Storage)(object)((___owner is Building_Storage) ? ___owner : null);
			if (val != null)
			{
				IForbidPawnInputItem obj = val as IForbidPawnInputItem;
				if (obj != null && obj.ForbidPawnInput && ((Thing)val).Position != t.Position)
				{
					return false;
				}
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(StoreUtility))]
	public static class Patch_StoreUtility
	{
		[HarmonyPrefix]
		[HarmonyPatch("TryFindBestBetterStoreCellForWorker")]
		public static bool Prefix(Thing t, Pawn carrier, Map map, Faction faction, ISlotGroup slotGroup, bool needAccurateResult, ref IntVec3 closestSlot, ref float closestDistSquared, ref StoragePriority foundPriority)
		{
			SlotGroup val = (SlotGroup)(object)((slotGroup is SlotGroup) ? slotGroup : null);
			if (val == null)
			{
				return true;
			}
			ISlotGroupParent parent = val.parent;
			Building_Storage val2 = (Building_Storage)(object)((parent is Building_Storage) ? parent : null);
			if (val2 == null)
			{
				return true;
			}
			if (!(val2 is IForbidPawnInputItem forbidPawnInputItem))
			{
				return true;
			}
			return !forbidPawnInputItem.ForbidPawnInput;
		}
	}
	[HarmonyPatch(typeof(Thing))]
	public static class Patch_Thing
	{
		[HarmonyPrefix]
		[HarmonyPatch("DrawGUIOverlay")]
		public static bool DrawGUIOverlay(Thing __instance)
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			if (!__instance.def.EverStorable(false))
			{
				return true;
			}
			return !__instance.Map.IsDSUOnPoint(__instance.Position);
		}

		[HarmonyPrefix]
		[HarmonyPatch("Print", new Type[] { typeof(SectionLayer) })]
		public static bool Print(Thing __instance)
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			if (!__instance.def.EverStorable(false))
			{
				return true;
			}
			return !__instance.Map.IsDSUOnPoint(__instance.Position);
		}

		[HarmonyPrefix]
		[HarmonyPatch("set_Position")]
		public static void Prefix(Thing __instance, out IntVec3 __state)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			__state = __instance.Position;
		}

		[HarmonyPostfix]
		[HarmonyPatch("set_Position")]
		public static void Postfix(Thing __instance, IntVec3 __state)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			if (__instance.Map == null || __instance.Position == __state || !__instance.def.EverStorable(false))
			{
				return;
			}
			DigitalStorageUnitBuilding digitalStorageUnitBuilding = (((IntVec3)(ref __state)).IsValid ? __state.GetFirst<DigitalStorageUnitBuilding>(__instance.Map) : null);
			IntVec3 position = __instance.Position;
			DigitalStorageUnitBuilding digitalStorageUnitBuilding2 = (((IntVec3)(ref position)).IsValid ? __instance.Position.GetFirst<DigitalStorageUnitBuilding>(__instance.Map) : null);
			if (digitalStorageUnitBuilding2 != digitalStorageUnitBuilding)
			{
				if (digitalStorageUnitBuilding2 != null)
				{
					((Building_Storage)digitalStorageUnitBuilding2).Notify_ReceivedThing(__instance);
				}
				if (digitalStorageUnitBuilding != null)
				{
					((Building_Storage)digitalStorageUnitBuilding).Notify_LostThing(__instance);
				}
			}
		}
	}
	[HarmonyPatch(typeof(ThingWithComps))]
	public class Patch_ThingWithComps
	{
		[HarmonyPrefix]
		[HarmonyPatch("DrawGUIOverlay")]
		public static bool DrawGUIOverlay(Thing __instance)
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			if (!__instance.def.EverStorable(false))
			{
				return true;
			}
			return !__instance.Map.IsDSUOnPoint(__instance.Position);
		}
	}
	[HarmonyPatch(typeof(TradeUtility))]
	public static class Patch_TradeUtility
	{
		[HarmonyPostfix]
		[HarmonyPatch("AllLaunchableThingsForTrade")]
		public static void AllLaunchableThingsForTrade(Map map, ref IEnumerable<Thing> __result)
		{
			HashSet<Thing> hashSet = new HashSet<Thing>();
			GenCollection.AddRange<Thing>(hashSet, __result);
			foreach (DigitalStorageUnitBuilding item in map.GetAllPoweredDSU())
			{
				GenCollection.AddRange<Thing>(hashSet, item.GetStoredThings());
			}
			__result = hashSet;
		}
	}
	[HarmonyPatch]
	public static class Patch_WorkGiver_DoBill
	{
		[HarmonyPatch(typeof(WorkGiver_DoBill))]
		public static class InnerPatch_AddItemsFromFoundDsu
		{
			[HarmonyTargetMethod]
			public static MethodBase TargetMethod()
			{
				_hiddenClass = typeof(WorkGiver_DoBill).GetNestedTypes(AccessTools.all).FirstOrDefault((Type t) => t.FullName.Contains("c__DisplayClass24_0"));
				if ((object)_hiddenClass == null)
				{
					Log.Error("DSU: Can't find WorkGiver_DoBill.c__DisplayClass24_0 class");
					return null;
				}
				_regionsProcessed = _hiddenClass.GetFields(AccessTools.all).FirstOrDefault((FieldInfo t) => t.Name.EndsWith("regionsProcessed"));
				MethodInfo methodInfo = _hiddenClass.GetMethods(AccessTools.all).FirstOrDefault((MethodInfo t) => t.Name.Contains("b__4"));
				if ((object)methodInfo == null)
				{
					Log.Error("DSU: Can't find WorkGiver_DoBill.<>c__DisplayClass24_0.<TryFindBestIngredientsHelper>b__4 method");
					return null;
				}
				return methodInfo;
			}

			[HarmonyTranspiler]
			public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				bool regionsProcessedFound = false;
				bool patched = false;
				foreach (CodeInstruction instruction in instructions)
				{
					if (!regionsProcessedFound && instruction.opcode == OpCodes.Stfld && instruction.operand.ToString() == "System.Int32 regionsProcessed")
					{
						regionsProcessedFound = true;
						yield return instruction;
					}
					else if (regionsProcessedFound && !patched)
					{
						yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null);
						yield return new CodeInstruction(OpCodes.Ldfld, (object)_regionsProcessed);
						yield return new CodeInstruction(OpCodes.Ldsfld, (object)AccessTools.Field(typeof(WorkGiver_DoBill), "newRelevantThings"));
						yield return new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(InnerPatch_AddItemsFromFoundDsu), "Injected_AddThingsFromDSU", (Type[])null, (Type[])null));
						Log.Message("DSU: Patch_WorkGiver_DoBill_DisplayClass_24_0_HiddenMethod OK");
						patched = true;
						yield return instruction;
					}
					else
					{
						yield return instruction;
					}
				}
			}

			public static void Injected_AddThingsFromDSU(int regionsProcessed, List<Thing> relevantThings)
			{
				//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
				if (!DigitalStorageUnit.Config.BillSearchRadiusFix || _pawn == null || _thingValidator == null || regionsProcessed != 1)
				{
					return;
				}
				foreach (DigitalStorageUnitBuilding item in FoundDsuWithPortsInRadius)
				{
					foreach (Thing storedThing in item.GetStoredThings())
					{
						if (!relevantThings.Contains(storedThing) && !ForbidUtility.IsForbidden(storedThing, _pawn) && ReservationUtility.CanReserve(_pawn, LocalTargetInfo.op_Implicit(storedThing), 1, -1, (ReservationLayerDef)null, false) && _thingValidator(storedThing))
						{
							relevantThings.Add(storedThing);
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(WorkGiver_DoBill))]
		public static class InnerPatch_TryFindBestIngredientsHelper
		{
			[HarmonyPrefix]
			[HarmonyPatch("TryFindBestIngredientsHelper")]
			public static void TryFindBestIngredientsHelper_Prefix(Predicate<Thing> thingValidator, Pawn pawn, Thing billGiver, float searchRadius)
			{
				_thingValidator = thingValidator;
				_pawn = pawn;
				if (billGiver != null && DigitalStorageUnit.Config.BillSearchRadiusFix)
				{
					float radiusSq = searchRadius * searchRadius;
					GenCollection.AddRange<DigitalStorageUnitBuilding>(FoundDsuWithPortsInRadius, from p in billGiver.Map.listerBuildings.AllBuildingsColonistOfClass<AccessPointPortBuilding>()
						where ((Thing)p).Spawned && p.Powered && p.BoundStorageUnit != null && p.BoundStorageUnit.CanWork && (float)IntVec3Utility.DistanceToSquared(((Thing)p).Position, billGiver.Position) <= radiusSq
						select p.BoundStorageUnit);
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch("TryFindBestIngredientsHelper")]
			public static void TryFindBestIngredientsHelper_Postfix()
			{
				_thingValidator = null;
				_pawn = null;
				FoundDsuWithPortsInRadius.Clear();
			}
		}

		[HarmonyPatch(typeof(WorkGiver_DoBill))]
		public static class InnerPatch_TryStartNewDoBillJob
		{
			[HarmonyPrefix]
			[HarmonyPatch("TryStartNewDoBillJob")]
			public static void Postfix(ref Job __result)
			{
				//IL_005d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0062: Unknown result type (might be due to invalid IL or missing references)
				//IL_010e: Unknown result type (might be due to invalid IL or missing references)
				if (__result?.targetQueueB == null)
				{
					return;
				}
				Thing thing = ((LocalTargetInfo)(ref __result.targetA)).Thing;
				DsuMapComponent dsuMapComponent = ((thing == null) ? null : thing.Map?.GetDsuComponent());
				if (dsuMapComponent == null)
				{
					return;
				}
				for (int i = 0; i < __result.targetQueueB.Count; i++)
				{
					LocalTargetInfo val = __result.targetQueueB[i];
					if (!((LocalTargetInfo)(ref val)).HasThing)
					{
						continue;
					}
					DigitalStorageUnitBuilding dsuHoldingItem = dsuMapComponent.GetDsuHoldingItem(((LocalTargetInfo)(ref val)).Thing);
					if (dsuHoldingItem == null)
					{
						break;
					}
					int num = __result.countQueue[i];
					if (num > 0 && num < ((LocalTargetInfo)(ref val)).Thing.stackCount && dsuHoldingItem.CanWork)
					{
						Thing val2 = ThingMaker.MakeThing(((LocalTargetInfo)(ref val)).Thing.def, ((LocalTargetInfo)(ref val)).Thing.Stuff);
						val2.stackCount = ((LocalTargetInfo)(ref val)).Thing.stackCount - num;
						val2.Position = ((LocalTargetInfo)(ref val)).Thing.Position;
						((LocalTargetInfo)(ref val)).Thing.stackCount = num;
						((LocalTargetInfo)(ref val)).Thing.DirtyMapMesh(((LocalTargetInfo)(ref val)).Thing.Map);
						if (((LocalTargetInfo)(ref val)).Thing.def.useHitPoints)
						{
							val2.HitPoints = ((LocalTargetInfo)(ref val)).Thing.HitPoints;
						}
						((Entity)val2).SpawnSetup(((LocalTargetInfo)(ref val)).Thing.Map, false);
						dsuHoldingItem.HandleNewItem(val2, tryAbsorb: false);
					}
				}
			}
		}

		private static Type _hiddenClass;

		private static FieldInfo _regionsProcessed;

		private static FieldInfo _relevantThings;

		private static Predicate<Thing> _thingValidator;

		private static Pawn _pawn;

		private static readonly HashSet<DigitalStorageUnitBuilding> FoundDsuWithPortsInRadius = new HashSet<DigitalStorageUnitBuilding>();
	}
}
namespace DigitalStorageUnit._harmony.rimfeller
{
	public static class SubParch_Rimfeller_CompRefinery
	{
		public static bool Prefix(Thing ___parent, ref List<Building_Storage> __result)
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			Map map = ___parent.Map;
			if (map == null)
			{
				return true;
			}
			CellRect val = GenAdj.OccupiedRect(___parent);
			foreach (IntVec3 item in ((CellRect)(ref val)).AdjacentCellsCardinal)
			{
				Thing firstThing = GridsUtility.GetFirstThing(item, map, DsuDefOf.DSU_InputBus_Building);
				if (!(firstThing is InputPortDsuBuilding { BoundStorageUnit: not null } inputPortDsuBuilding) || !inputPortDsuBuilding.BoundStorageUnit.CanWork)
				{
					continue;
				}
				__result = new List<Building_Storage>(1) { (Building_Storage)(object)inputPortDsuBuilding };
				return false;
			}
			return true;
		}
	}
}
namespace DigitalStorageUnit.util
{
	public static class ColorExtensions
	{
		public static Color WithAlpha(this Color color, float a)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			Color result = color;
			result.a = a;
			return result;
		}
	}
	public interface IForbidPawnInputItem : ISlotGroupParent, IStoreSettingsParent, IHaulDestination
	{
		bool ForbidPawnInput { get; }
	}
	public static class IntVec3Extensions
	{
		public static T GetFirst<T>(this IntVec3 c, Map map) where T : class
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			if (map == null || !GenGrid.InBounds(c, map))
			{
				return null;
			}
			foreach (Thing item in map.thingGrid.ThingsListAt(c))
			{
				if (item is T result)
				{
					return result;
				}
			}
			return null;
		}
	}
	public static class ThingExtensions
	{
		private static readonly Rect RenderRect = GenUI.Rounded(GenUI.ScaledBy(new Rect(0f, 0f, 24f, 24f), 1.8f));

		public static Texture GetThingTexture(this Thing thing, out Color color)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_0145: Unknown result type (might be due to invalid IL or missing references)
			//IL_0153: Unknown result type (might be due to invalid IL or missing references)
			//IL_0158: Unknown result type (might be due to invalid IL or missing references)
			color = thing.DrawColor;
			thing = MinifyUtility.GetInnerIfMinified(thing);
			if (!GenText.NullOrEmpty(((BuildableDef)thing.def).uiIconPath))
			{
				return (Texture)(object)((BuildableDef)thing.def).uiIcon;
			}
			if (!(thing is Pawn) && !(thing is Corpse))
			{
				return GraphicUtility.ExtractInnerGraphicFor(thing.Graphic, thing, (int?)null).MatAt(((BuildableDef)thing.def).defaultPlacingRot, (Thing)null).mainTexture;
			}
			Pawn val = (Pawn)(object)((thing is Pawn) ? thing : null);
			if (val == null)
			{
				val = ((Corpse)thing).InnerPawn;
			}
			if (val.RaceProps.Humanlike)
			{
				Pawn obj = val;
				Rect renderRect = RenderRect;
				float width = ((Rect)(ref renderRect)).width;
				renderRect = RenderRect;
				return (Texture)(object)PortraitsCache.Get(obj, new Vector2(width, ((Rect)(ref renderRect)).height), Rot4.North, default(Vector3), 1f, true, true, true, true, (IReadOnlyDictionary<Apparel, Color>)null, (Color?)null, false, (PawnHealthState?)null);
			}
			Graphic bodyGraphic = val.Drawer.renderer.BodyGraphic;
			if (bodyGraphic == null)
			{
				Log.Error($"PRF Can't get the Body Graphic for {val}");
			}
			Material val2 = bodyGraphic.MatAt(Rot4.East, (Thing)null);
			color = val2.color;
			return val2.mainTexture;
		}
	}
	public struct ThingIconTextureData(Texture texture, Color color)
	{
		public readonly Texture Texture = texture;

		public readonly Color Color = color;
	}
	public static class FuzzySearch
	{
		[StructLayout(LayoutKind.Sequential, Size = 1)]
		public struct Strength
		{
			public const double Strong = 0.25;

			public const double Normal = 0.5;

			public const double Manual = 0.6;

			public const double Weak = 0.75;
		}

		private static int HammingDistance(this string source, string target)
		{
			if (source.Length == target.Length)
			{
				int num = 0;
				for (int i = 0; i < source.Length; i++)
				{
					if (!source[i].Equals(target[i]))
					{
						num++;
					}
				}
				return num;
			}
			return 99999;
		}

		private static int LevenshteinDistance(this string source, string target)
		{
			int length = source.Length;
			int length2 = target.Length;
			int[,] array = new int[length + 1, length2 + 1];
			if (length == 0)
			{
				return length2;
			}
			if (length2 == 0)
			{
				return length;
			}
			int num = 0;
			while (num <= length)
			{
				array[num, 0] = num++;
			}
			int num2 = 0;
			while (num2 <= length2)
			{
				array[0, num2] = num2++;
			}
			for (int i = 1; i <= length; i++)
			{
				for (int j = 1; j <= length2; j++)
				{
					int num3 = ((target[j - 1] != source[i - 1]) ? 1 : 0);
					array[i, j] = Math.Min(Math.Min(array[i - 1, j] + 1, array[i, j - 1] + 1), array[i - 1, j - 1] + num3);
				}
			}
			return array[length, length2];
		}

		private static double NormalizedLevenshteinDistance(this string source, string target)
		{
			int num = source.LevenshteinDistance(target);
			return num - source.LevenshteinDistanceLowerBounds(target);
		}

		private static int LevenshteinDistanceUpperBounds(this string source, string target)
		{
			if (source.Length == target.Length)
			{
				return source.HammingDistance(target);
			}
			if (source.Length > target.Length)
			{
				return source.Length;
			}
			return (target.Length > source.Length) ? target.Length : 9999;
		}

		private static int LevenshteinDistanceLowerBounds(this string source, string target)
		{
			return Math.Abs(source.Length - target.Length);
		}

		public static double NormalizedFuzzyStrength(this string source, string target)
		{
			return Convert.ToDouble(source.NormalizedLevenshteinDistance(target)) / Convert.ToDouble(Math.Max(source.Length, target.Length) - source.LevenshteinDistanceLowerBounds(target));
		}

		public static double FuzzyStrength(this string source, string target)
		{
			return Convert.ToDouble(source.LevenshteinDistance(target)) / Convert.ToDouble(source.LevenshteinDistanceUpperBounds(target));
		}
	}
	public enum StorageIOMode
	{
		Input,
		Output
	}
	[StaticConstructorOnStartup]
	public static class TextureHolder
	{
		public static readonly Texture2D StoragePawnAccessSwitchIcon = ContentFinder<Texture2D>.Get("UI/dsu", true);

		public static readonly Texture2D CargoPlatform = ContentFinder<Texture2D>.Get("Storage/CargoPlatform", true);

		public static readonly Texture2D IoIcon = ContentFinder<Texture2D>.Get("UI/IoIcon", true);

		public static readonly Texture2D ForbidOn = ContentFinder<Texture2D>.Get("UI/Designators/ForbidOn", true);

		public static readonly Texture2D ForbidOff = ContentFinder<Texture2D>.Get("UI/Designators/ForbidOff", true);

		public static readonly Texture2D Arrow = ContentFinder<Texture2D>.Get("UI/Overlays/Arrow", true);

		public static readonly Texture2D Rename = ContentFinder<Texture2D>.Get("UI/Buttons/Rename", true);

		public static readonly Texture2D SetTargetFuelLevel = ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel", true);

		public static readonly Material MatLostLink = MaterialPool.MatFrom("UI/too-far-from-roboport-icon", ShaderDatabase.MetaOverlay);

		public static readonly Color Yellow25 = Color.yellow.WithAlpha(0.25f);

		public static readonly Color Red25 = Color.red.WithAlpha(0.25f);

		public const float LineWidth = 0.1f;

		public const float CircleRadius = 0.8f;
	}
}
namespace DigitalStorageUnit.ui
{
	internal class ActionRightLeftCommand : Command
	{
		private static bool _wasRightClick;

		public Action ActionL;

		public Action ActionR;

		public override void ProcessInput(Event ev)
		{
			((Command)this).ProcessInput(ev);
			if (_wasRightClick)
			{
				ActionR();
			}
			else
			{
				ActionL();
			}
		}

		public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			((Command)this).DrawIcon(rect, buttonMat, parms);
			if (Input.GetMouseButtonDown(0))
			{
				_wasRightClick = false;
			}
			if (Input.GetMouseButtonDown(1))
			{
				_wasRightClick = true;
			}
		}
	}
	public class DsuNonHermeticAlert : Alert
	{
		protected override Color BGColor => TextureHolder.Red25;

		public override string GetLabel()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("DSU.Alert.DsuNonHermetic"));
		}

		public override TaggedString GetExplanation()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return Translator.Translate("DSU.Alert.DsuNonHermetic.Desc");
		}

		public override AlertReport GetReport()
		{
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			List<Thing> list = Find.Maps.Where((Map m) => m.IsPlayerHome).SelectMany((Map m) => m.listerBuildings.AllBuildingsColonistOfClass<DigitalStorageUnitBuilding>()).Where(delegate(DigitalStorageUnitBuilding t)
			{
				if (t == null || !t.Powered)
				{
					return false;
				}
				DsuHeaterComp dsuHeaterComp = ThingCompUtility.TryGetComp<DsuHeaterComp>((Thing)(object)t);
				return dsuHeaterComp != null && !dsuHeaterComp.IsRoomHermetic();
			})
				.Cast<Thing>()
				.ToList();
			return GenCollection.Any<Thing>(list) ? AlertReport.CulpritsAre(list) : AlertReport.op_Implicit(false);
		}
	}
	public class DsuOverheatAlert : Alert
	{
		protected override Color BGColor => TextureHolder.Red25;

		public override string GetLabel()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("DSU.Alert.DsuOverheat"));
		}

		public override TaggedString GetExplanation()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return Translator.Translate("DSU.Alert.DsuOverheat.Desc");
		}

		public override AlertReport GetReport()
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_011a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Unknown result type (might be due to invalid IL or missing references)
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			if (!DigitalStorageUnit.Config.HeaterEnabled)
			{
				return AlertReport.op_Implicit(false);
			}
			List<Thing> list = (from d in Find.Maps.Where((Map m) => m.IsPlayerHome).SelectMany((Map m) => m.listerBuildings.allBuildingsColonist)
				where d is DigitalStorageUnitBuilding digitalStorageUnitBuilding && digitalStorageUnitBuilding.Powered
				select ThingCompUtility.TryGetComp<DsuHeaterComp>((Thing)(object)d) into c
				where c.IsOverheat()
				select (Thing)(object)((ThingComp)c).parent).ToList();
			return GenCollection.Any<Thing>(list) ? AlertReport.CulpritsAre(list) : AlertReport.op_Implicit(false);
		}
	}
	public class LostLinkAlert : Alert
	{
		protected override Color BGColor => TextureHolder.Yellow25;

		public override string GetLabel()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("DSU.Alert.LostLink.Label"));
		}

		public override TaggedString GetExplanation()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return Translator.Translate("DSU.Alert.LostLink.Desc");
		}

		public override AlertReport GetReport()
		{
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			List<Thing> list = (from t in Find.Maps.Where((Map m) => m.IsPlayerHome).SelectMany((Map m) => m.listerBuildings.allBuildingsColonist)
				where t is ABasePortDsuBuilding aBasePortDsuBuilding && aBasePortDsuBuilding.BoundStorageUnit == null
				select t).Cast<Thing>().ToList();
			if (!GenCollection.Any<Thing>(list))
			{
				return AlertReport.op_Implicit(false);
			}
			return AlertReport.CulpritsAre(list);
		}
	}
	public class OutputMinMaxDialog : Window
	{
		private enum LimitUpdateRequestFocus
		{
			Max = 1,
			Min = 0,
			Undefined = -1
		}

		private static class LimitUpdateRequest
		{
			private const int GracePeriodMax = 75;

			private static int _gracePeriod = -1;

			private static string _lastval = "";

			private static Predicate<bool> _predicate = (bool _) => false;

			private static OutputMinMaxDialog _parent;

			private static bool _minIsMax;

			public static void Init(OutputMinMaxDialog par, Predicate<bool> validator)
			{
				_predicate = validator;
				_parent = par;
			}

			public static void Update(bool dir, string buff)
			{
				if (_gracePeriod == -1 || buff != _lastval)
				{
					_gracePeriod = 75;
				}
				else if (dir != _minIsMax)
				{
					LostFocus();
				}
				_minIsMax = dir;
				_lastval = buff;
			}

			public static void CheckFocusLoss(LimitUpdateRequestFocus maxFocus)
			{
				if (_gracePeriod != -1 && (maxFocus < LimitUpdateRequestFocus.Min || ((maxFocus != LimitUpdateRequestFocus.Max || !_minIsMax) && (maxFocus != LimitUpdateRequestFocus.Min || _minIsMax))))
				{
					LostFocus();
				}
			}

			public static void LostFocus(bool force = false)
			{
				if (_gracePeriod != -1 || force)
				{
					_gracePeriod = 1;
					Tick();
				}
			}

			public static void Tick()
			{
				if (_gracePeriod >= 0)
				{
					_gracePeriod--;
				}
				if (_gracePeriod == 0 && _predicate(_minIsMax))
				{
					_parent?.OverrideBuffer(_minIsMax);
				}
			}
		}

		private string _controlIdMinInput;

		private string _controlIdMaxInput;

		private readonly OutputSettings _outputSettings;

		private readonly Action _postClose;

		private const float TitleLabelHeight = 32f;

		private string _minBufferString;

		private string _maxBufferString;

		public override Vector2 InitialSize => new Vector2(500f, 250f);

		private void OverrideBuffer(bool minIsMax)
		{
			if (minIsMax)
			{
				_minBufferString = _maxBufferString;
			}
			else
			{
				_maxBufferString = _minBufferString;
			}
		}

		public OutputMinMaxDialog(OutputSettings settings, Action postClose = null)
			: base((IWindowDrawing)null)
		{
			_outputSettings = settings;
			_postClose = postClose;
			base.doCloseX = true;
			base.doCloseButton = true;
			base.closeOnClickedOutside = true;
			base.absorbInputAroundWindow = true;
			base.draggable = true;
			base.drawShadow = true;
			base.focusWhenOpened = true;
			base.forcePause = true;
		}

		private bool Validator(bool data)
		{
			if (GenText.NullOrEmpty(_maxBufferString))
			{
				_maxBufferString = "0";
				_outputSettings.Max = 0;
			}
			if (GenText.NullOrEmpty(_minBufferString))
			{
				_minBufferString = "0";
				_outputSettings.Min = 0;
			}
			return _outputSettings.Max < _outputSettings.Min;
		}

		public override void DoWindowContents(Rect rect)
		{
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Expected O, but got Unknown
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Unknown result type (might be due to invalid IL or missing references)
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			//IL_0153: Unknown result type (might be due to invalid IL or missing references)
			//IL_0158: Unknown result type (might be due to invalid IL or missing references)
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			//IL_015c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0161: Unknown result type (might be due to invalid IL or missing references)
			//IL_0166: Unknown result type (might be due to invalid IL or missing references)
			//IL_0168: Unknown result type (might be due to invalid IL or missing references)
			//IL_016a: Unknown result type (might be due to invalid IL or missing references)
			//IL_016f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0174: Unknown result type (might be due to invalid IL or missing references)
			//IL_0176: Unknown result type (might be due to invalid IL or missing references)
			//IL_017b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0184: Unknown result type (might be due to invalid IL or missing references)
			//IL_018c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0193: Unknown result type (might be due to invalid IL or missing references)
			//IL_019e: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_025e: Unknown result type (might be due to invalid IL or missing references)
			//IL_027e: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02be: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0303: Unknown result type (might be due to invalid IL or missing references)
			if (_maxBufferString == null)
			{
				_maxBufferString = _outputSettings.Max.ToString();
			}
			if (_minBufferString == null)
			{
				_minBufferString = _outputSettings.Min.ToString();
			}
			Listing_Standard val = new Listing_Standard((GameFont)1)
			{
				ColumnWidth = ((Rect)(ref rect)).width
			};
			string nameOfFocusedControl = GUI.GetNameOfFocusedControl();
			if (nameOfFocusedControl == _controlIdMaxInput)
			{
				LimitUpdateRequest.CheckFocusLoss(LimitUpdateRequestFocus.Max);
			}
			else if (nameOfFocusedControl == _controlIdMinInput)
			{
				LimitUpdateRequest.CheckFocusLoss(LimitUpdateRequestFocus.Min);
			}
			else
			{
				LimitUpdateRequest.CheckFocusLoss(LimitUpdateRequestFocus.Undefined);
			}
			((Listing)val).Begin(rect);
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(0f, 0f, ((Rect)(ref rect)).width, 32f);
			Text.Font = (GameFont)2;
			Widgets.Label(val2, Translator.Translate("DSU.MinMaxAmounts"));
			Text.Font = (GameFont)1;
			((Listing)val).Gap(12f);
			((Listing)val).Gap(12f);
			((Listing)val).Gap(12f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DSU.UseMin")), ref _outputSettings.UseMin, TaggedString.op_Implicit(Translator.Translate(_outputSettings.MinTooltip)), 0f, 1f);
			((Listing)val).Gap(12f);
			Rect rect2 = ((Listing)val).GetRect(Text.LineHeight, 1f);
			Rect val3 = GenUI.Rounded(GenUI.LeftHalf(rect2));
			Rect val4 = GenUI.Rounded(GenUI.RightHalf(rect2));
			TextAnchor anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)3;
			Widgets.DrawHighlightIfMouseover(rect2);
			Widgets.Label(val3, Translator.Translate("DSU.Min"));
			Text.Anchor = anchor;
			Widgets.TextFieldNumeric<int>(val4, ref _outputSettings.Min, ref _minBufferString, 0f, 1E+09f);
			if (_controlIdMinInput == null)
			{
				_controlIdMinInput = "TextField" + ((Rect)(ref val4)).y.ToString("F0") + ((Rect)(ref val4)).x.ToString("F0");
			}
			if (_outputSettings.Max < _outputSettings.Min && GUI.GetNameOfFocusedControl() == _controlIdMinInput)
			{
				LimitUpdateRequest.Update(dir: false, _minBufferString);
			}
			((Listing)val).Gap(12f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DSU.UseMax")), ref _outputSettings.UseMax, TaggedString.op_Implicit(Translator.Translate(_outputSettings.MaxTooltip)), 0f, 1f);
			((Listing)val).Gap(12f);
			Rect rect3 = ((Listing)val).GetRect(Text.LineHeight, 1f);
			Rect val5 = GenUI.Rounded(GenUI.LeftHalf(rect3));
			Rect val6 = GenUI.Rounded(GenUI.RightHalf(rect3));
			TextAnchor anchor2 = Text.Anchor;
			Text.Anchor = (TextAnchor)3;
			Widgets.DrawHighlightIfMouseover(rect3);
			Widgets.Label(val5, Translator.Translate("DSU.Max"));
			Text.Anchor = anchor2;
			Widgets.TextFieldNumeric<int>(val6, ref _outputSettings.Max, ref _maxBufferString, 0f, 1E+09f);
			if (_controlIdMaxInput == null)
			{
				_controlIdMaxInput = "TextField" + ((Rect)(ref val6)).y.ToString("F0") + ((Rect)(ref val6)).x.ToString("F0");
			}
			if (_outputSettings.Min > _outputSettings.Max && GUI.GetNameOfFocusedControl() == _controlIdMaxInput)
			{
				LimitUpdateRequest.Update(dir: true, _maxBufferString);
			}
			LimitUpdateRequest.Tick();
			((Listing)val).End();
		}

		public override void PostClose()
		{
			LimitUpdateRequest.LostFocus(GenText.NullOrEmpty(_maxBufferString) || GenText.NullOrEmpty(_minBufferString));
			((Window)this).PostClose();
			_postClose?.Invoke();
		}

		public override void PreOpen()
		{
			LimitUpdateRequest.Init(this, Validator);
			((Window)this).PreOpen();
		}
	}
	public class RenameDsuDialog : Dialog_Rename<DigitalStorageUnitBuilding>
	{
		public RenameDsuDialog(DigitalStorageUnitBuilding building)
			: base(building)
		{
		}
	}
}
namespace DigitalStorageUnit.obsolete
{
	public interface IPRF_Building
	{
		IEnumerable<Thing> AvailableThings { get; }

		bool ForbidOnPlacingDefault { get; set; }

		bool ObeysStorageFilters { get; }

		bool OutputToEntireStockpile { get; }

		Map Map { get; }

		bool AcceptsThing(Thing newThing, IPRF_Building giver = null);

		Thing GetThingBy(Func<Thing, bool> optionalValidator = null);

		bool ForbidOnPlacing(Thing t);

		void EffectOnPlaceThing(Thing t);

		void EffectOnAcceptThing(Thing t);
	}
	public static class PlaceThingUtility
	{
		public static Func<IntVec3, Map, Thing, bool> CallNoStorageBlockersIn;

		public unsafe static bool PRFTryPlaceThing(this IPRF_Building placer, Thing t, IntVec3 cell, Map map, bool forcePlace = false)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0181: Unknown result type (might be due to invalid IL or missing references)
			//IL_0182: Unknown result type (might be due to invalid IL or missing references)
			//IL_010d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0211: Unknown result type (might be due to invalid IL or missing references)
			//IL_0217: Invalid comparison between Unknown and I4
			//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0403: Unknown result type (might be due to invalid IL or missing references)
			//IL_023b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0331: Unknown result type (might be due to invalid IL or missing references)
			//IL_037c: Unknown result type (might be due to invalid IL or missing references)
			if (!GenGrid.InBounds(cell, map))
			{
				Log.Error("PRF Error Attempting to place Thing out of bounds.");
				return false;
			}
			SlotGroup slotGroup = StoreUtility.GetSlotGroup(cell, map);
			if (slotGroup != null)
			{
				string[] obj = new string[6]
				{
					"Placing ",
					((object)t)?.ToString(),
					" in slotGroup: ",
					((object)slotGroup.parent)?.ToString(),
					" at ",
					null
				};
				IntVec3 val = cell;
				obj[5] = ((object)(*(IntVec3*)(&val))/*cast due to constrained. prefix*/).ToString();
				Debug.Warning(Debug.Flag.PlaceThing, string.Concat(obj));
				if (slotGroup.parent is IPRF_Building)
				{
					if (placer.PlaceThingNextBuilding(slotGroup.parent as IPRF_Building, t, cell, map))
					{
						Debug.Message(Debug.Flag.PlaceThing, "  which is owned by PRF " + (object)slotGroup.parent);
						return true;
					}
					if (!forcePlace)
					{
						return false;
					}
				}
				else
				{
					if (placer.PlaceThingInSlotGroup(t, slotGroup, cell, map))
					{
						return true;
					}
					if (!forcePlace)
					{
						return false;
					}
				}
			}
			else
			{
				object text;
				if ("Place request: " + placer != null)
				{
					string[] obj2 = new string[5]
					{
						placer.ToString(),
						" is trying to place ",
						((object)t)?.ToString(),
						" at ",
						null
					};
					IntVec3 val = cell;
					obj2[4] = ((object)(*(IntVec3*)(&val))/*cast due to constrained. prefix*/).ToString();
					text = string.Concat(obj2);
				}
				else
				{
					text = "NoPlacer";
				}
				Debug.Warning(Debug.Flag.PlaceThing, (string)text);
				bool flag = false;
				foreach (Thing item in map.thingGrid.ThingsListAt(cell))
				{
					if (item.TryAbsorbStack(t, true))
					{
						Debug.Message(Debug.Flag.PlaceThing, "  absorbed by " + (object)item);
						placer.EffectOnPlaceThing(item);
						return true;
					}
					if ((int)((BuildableDef)item.def).passability == 2)
					{
						flag = true;
					}
					if (item is IPRF_Building && placer.PlaceThingNextBuilding(item as IPRF_Building, t, cell, map))
					{
						Debug.Message(Debug.Flag.PlaceThing, placer?.ToString() + " gave " + ((object)t)?.ToString() + " to " + (object)item);
						placer.EffectOnPlaceThing(t);
						return true;
					}
				}
				if (flag)
				{
					Debug.Message(Debug.Flag.PlaceThing, "  cell is impassable.");
					if (!forcePlace)
					{
						return false;
					}
				}
				else if (CallNoStorageBlockersIn(cell, map, t))
				{
					Debug.Message(Debug.Flag.PlaceThing, "  placing directly.");
					bool spawned = t.Spawned;
					if (spawned)
					{
						((Entity)t).DeSpawn((DestroyMode)0);
					}
					if (GenPlace.TryPlaceThing(t, cell, map, (ThingPlaceMode)0, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
					{
						placer.EffectOnPlaceThing(t);
						if (placer.ForbidOnPlacing(t))
						{
							ForbidUtility.SetForbidden(t, true, false);
						}
						return true;
					}
					Debug.Message(Debug.Flag.PlaceThing, "  some absorbed, still have " + t.stackCount + " left");
					if (spawned)
					{
						GenPlace.TryPlaceThing(t, t.Position, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					}
					placer.EffectOnPlaceThing(t);
					if (!forcePlace)
					{
						return false;
					}
				}
				else if (!forcePlace)
				{
					return false;
				}
			}
			Debug.Warning(Debug.Flag.PlaceThing, "  Placement is forced!");
			if (t.Spawned)
			{
				((Entity)t).DeSpawn((DestroyMode)0);
			}
			GenPlace.TryPlaceThing(t, cell, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			placer.EffectOnPlaceThing(t);
			if (placer.ForbidOnPlacing(t))
			{
				ForbidUtility.SetForbidden(t, true, false);
			}
			return true;
		}

		private static bool PlaceThingNextBuilding(this IPRF_Building placer, IPRF_Building nextBuilding, Thing t, IntVec3 cell, Map map)
		{
			if (nextBuilding.AcceptsThing(t, placer))
			{
				placer.EffectOnPlaceThing(t);
				return true;
			}
			return false;
		}

		private unsafe static bool PlaceThingInSlotGroup(this IPRF_Building placer, Thing t, SlotGroup slotGroup, IntVec3 cell, Map map)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0168: Unknown result type (might be due to invalid IL or missing references)
			//IL_0169: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_010e: Unknown result type (might be due to invalid IL or missing references)
			if (placer != null && placer.OutputToEntireStockpile)
			{
				return placer.PlaceThingAnywhereInSlotGroup(t, slotGroup, cell);
			}
			if (placer.ObeysStorageFilters && !((IHaulDestination)slotGroup.parent).Accepts(t))
			{
				return false;
			}
			IntVec3 val;
			if (CallNoStorageBlockersIn(cell, map, t))
			{
				string[] obj = new string[5] { "Found NoStorageBlockersIn(", null, null, null, null };
				val = cell;
				obj[1] = ((object)(*(IntVec3*)(&val))/*cast due to constrained. prefix*/).ToString();
				obj[2] = ", map, ";
				obj[3] = ((object)t)?.ToString();
				obj[4] = ") - Placing";
				Debug.Message(Debug.Flag.PlaceThing, string.Concat(obj));
				bool spawned = t.Spawned;
				if (spawned)
				{
					((Entity)t).DeSpawn((DestroyMode)0);
				}
				if (!GenPlace.TryPlaceThing(t, cell, map, (ThingPlaceMode)0, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
				{
					Debug.Message(Debug.Flag.PlaceThing, "  some was absorbed, but still have " + t.stackCount);
					if (spawned)
					{
						GenPlace.TryPlaceThing(t, t.Position, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					}
					placer.EffectOnPlaceThing(t);
					return false;
				}
				placer.EffectOnPlaceThing(t);
				if (placer.ForbidOnPlacing(t))
				{
					ForbidUtility.SetForbidden(t, true, false);
				}
				return true;
			}
			string[] obj2 = new string[5] { "There were StorageBlockersIn(", null, null, null, null };
			val = cell;
			obj2[1] = ((object)(*(IntVec3*)(&val))/*cast due to constrained. prefix*/).ToString();
			obj2[2] = ", map, ";
			obj2[3] = ((object)t)?.ToString();
			obj2[4] = ") - cannot place";
			Debug.Message(Debug.Flag.PlaceThing, string.Concat(obj2));
			return false;
		}

		public static bool PlaceThingAnywhereInSlotGroup(this IPRF_Building placer, Thing t, SlotGroup slotGroup, IntVec3? cell = null)
		{
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			if (placer.ObeysStorageFilters && !((IHaulDestination)slotGroup.parent).Accepts(t))
			{
				return false;
			}
			Map map = placer.Map;
			IEnumerable<IntVec3> enumerable;
			if (!cell.HasValue)
			{
				IEnumerable<IntVec3> cellsList = slotGroup.CellsList;
				enumerable = cellsList;
			}
			else
			{
				enumerable = ((IEnumerable<IntVec3>)(object)new IntVec3[1] { cell.Value }).Concat(slotGroup.CellsList.Where(delegate(IntVec3 x)
				{
					//IL_0000: Unknown result type (might be due to invalid IL or missing references)
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_0015: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					IntVec3? val2 = cell;
					return !val2.HasValue || x != val2.GetValueOrDefault();
				}));
			}
			foreach (IntVec3 item in enumerable)
			{
				if (!CallNoStorageBlockersIn(item, map, t))
				{
					continue;
				}
				string[] obj = new string[5] { "Found NoStorageBlockersIn(", null, null, null, null };
				IntVec3? val = cell;
				obj[1] = val.ToString();
				obj[2] = ", map, ";
				obj[3] = ((object)t)?.ToString();
				obj[4] = ") - Placing";
				Debug.Message(Debug.Flag.PlaceThing, string.Concat(obj));
				if (t.Spawned)
				{
					((Entity)t).DeSpawn((DestroyMode)0);
				}
				if (!GenPlace.TryPlaceThing(t, item, map, (ThingPlaceMode)0, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
				{
					Debug.Message(Debug.Flag.PlaceThing, "  some was absorbed, still have " + t.stackCount);
					continue;
				}
				placer.EffectOnPlaceThing(t);
				if (placer.ForbidOnPlacing(t))
				{
					ForbidUtility.SetForbidden(t, true, false);
				}
				return true;
			}
			Debug.Message(Debug.Flag.PlaceThing, "There were StorageBlockersIn every cell of " + ((object)slotGroup.parent)?.ToString() + " - cannot place" + (object)t);
			return false;
		}

		static PlaceThingUtility()
		{
			DynamicMethod dynamicMethod = new DynamicMethod("directly call RimWorld.StoreUtility's NoStorageBlockersIn", typeof(bool), new Type[3]
			{
				typeof(IntVec3),
				typeof(Map),
				typeof(Thing)
			}, restrictedSkipVisibility: true);
			ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Ldarg_2);
			iLGenerator.Emit(OpCodes.Call, typeof(StoreUtility).GetMethod("NoStorageBlockersIn", BindingFlags.Static | BindingFlags.NonPublic));
			iLGenerator.Emit(OpCodes.Ret);
			CallNoStorageBlockersIn = (Func<IntVec3, Map, Thing, bool>)dynamicMethod.CreateDelegate(typeof(Func<, , , >).MakeGenericType(typeof(IntVec3), typeof(Map), typeof(Thing), typeof(bool)));
		}
	}
}
namespace DigitalStorageUnit.map
{
	public class DsuMapComponent(Map map) : MapComponent(map)
	{
		public HashSet<DigitalStorageUnitBuilding> DsuSet { get; } = new HashSet<DigitalStorageUnitBuilding>();

		public HashSet<InputPortDsuBuilding> IoPortSet { get; } = new HashSet<InputPortDsuBuilding>();

		public HashSet<AccessPointPortBuilding> AccessPointSet { get; } = new HashSet<AccessPointPortBuilding>();

		public Dictionary<IntVec3, DigitalStorageUnitBuilding> DsuOccupiedPoints { get; } = new Dictionary<IntVec3, DigitalStorageUnitBuilding>();

		public bool IsDSUOnPoint(IntVec3 point)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			return DsuOccupiedPoints.ContainsKey(point);
		}

		public unsafe void RegisterBuilding(Building building)
		{
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			if (!(building is DigitalStorageUnitBuilding digitalStorageUnitBuilding))
			{
				if (!(building is AccessPointPortBuilding item))
				{
					if (building is InputPortDsuBuilding item2)
					{
						IoPortSet.Add(item2);
					}
				}
				else
				{
					AccessPointSet.Add(item);
				}
				return;
			}
			DsuSet.Add(digitalStorageUnitBuilding);
			CellRect val = GenAdj.OccupiedRect((Thing)(object)digitalStorageUnitBuilding);
			Enumerator enumerator = ((CellRect)(ref val)).GetEnumerator();
			try
			{
				while (((Enumerator)(ref enumerator)).MoveNext())
				{
					IntVec3 current = ((Enumerator)(ref enumerator)).Current;
					DsuOccupiedPoints[current] = digitalStorageUnitBuilding;
				}
			}
			finally
			{
				((IDisposable)(*(Enumerator*)(&enumerator))/*cast due to constrained. prefix*/).Dispose();
			}
		}

		public unsafe void DeregisterBuilding(Building building)
		{
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			if (!(building is DigitalStorageUnitBuilding digitalStorageUnitBuilding))
			{
				if (!(building is AccessPointPortBuilding item))
				{
					if (building is InputPortDsuBuilding item2)
					{
						IoPortSet.Remove(item2);
					}
				}
				else
				{
					AccessPointSet.Remove(item);
				}
				return;
			}
			DsuSet.Remove(digitalStorageUnitBuilding);
			CellRect val = GenAdj.OccupiedRect((Thing)(object)digitalStorageUnitBuilding);
			Enumerator enumerator = ((CellRect)(ref val)).GetEnumerator();
			try
			{
				while (((Enumerator)(ref enumerator)).MoveNext())
				{
					IntVec3 current = ((Enumerator)(ref enumerator)).Current;
					DsuOccupiedPoints.Remove(current);
				}
			}
			finally
			{
				((IDisposable)(*(Enumerator*)(&enumerator))/*cast due to constrained. prefix*/).Dispose();
			}
		}

		public float GetTotalDistance(Pawn pawn, IntVec3 middlePos, IntVec3 destinationPos, bool isDsu = false)
		{
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			if (DigitalStorageUnit.Config.CheapPathfinding)
			{
				return IntVec3Utility.DistanceTo(((Thing)pawn).Position, middlePos) + IntVec3Utility.DistanceTo(middlePos, destinationPos) * (isDsu ? DigitalStorageUnit.Config.DsuPathingMultiplier : 1f);
			}
			float totalCost = ((Thing)pawn).Map.pathFinder.FindPathNow(middlePos, LocalTargetInfo.op_Implicit(destinationPos), pawn, (PathFinderCostTuning?)null, (PathEndMode)1).TotalCost;
			if (DigitalStorageUnit.Config.HalfPathfinding)
			{
				return totalCost;
			}
			return ((Thing)pawn).Map.pathFinder.FindPathNow(((Thing)pawn).Position, LocalTargetInfo.op_Implicit(middlePos), pawn, (PathFinderCostTuning?)null, (PathEndMode)1).TotalCost + totalCost;
		}

		public DigitalStorageUnitBuilding GetDsuHoldingItem(Thing item)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			return GenCollection.TryGetValue<IntVec3, DigitalStorageUnitBuilding>((IReadOnlyDictionary<IntVec3, DigitalStorageUnitBuilding>)DsuOccupiedPoints, item.Position, (DigitalStorageUnitBuilding)null);
		}

		public override void MapRemoved()
		{
			MapExtension.OnMapRemoved(base.map);
		}

		public IEnumerable<AccessPointPortBuilding> GetAccessPoints(DigitalStorageUnitBuilding dsu = null, bool powered = true)
		{
			return AccessPointSet.Where(delegate(AccessPointPortBuilding ap)
			{
				if (dsu != null && ap.BoundStorageUnit != dsu)
				{
					return false;
				}
				return (!powered || ap.Powered) ? true : false;
			});
		}
	}
}
namespace DigitalStorageUnit.extensions
{
	public static class MapExtension
	{
		private static readonly Dictionary<Map, DsuMapComponent> MapCompsCache = new Dictionary<Map, DsuMapComponent>();

		public static DsuMapComponent GetDsuComponent(this Map map)
		{
			DsuMapComponent value = null;
			if (map != null && !MapCompsCache.TryGetValue(map, out value))
			{
				value = map.GetComponent<DsuMapComponent>();
				MapCompsCache.Add(map, value);
			}
			return value;
		}

		public static void OnMapRemoved(Map map)
		{
			MapCompsCache.Remove(map);
		}

		public static bool IsDSUOnPoint(this Map map, IntVec3 point)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			return map.GetDsuComponent()?.IsDSUOnPoint(point) ?? false;
		}

		public static IEnumerable<DigitalStorageUnitBuilding> GetAllPoweredDSU(this Map map, bool any = false)
		{
			foreach (DigitalStorageUnitBuilding building in map.listerBuildings.AllBuildingsColonistOfClass<DigitalStorageUnitBuilding>())
			{
				if (building.CanWork)
				{
					yield return building;
					if (any)
					{
						break;
					}
				}
			}
		}
	}
}
namespace DigitalStorageUnit.def
{
	[DefOf]
	public class DsuDefOf
	{
		public static ThingDef DSU_InputBus_Building;

		static DsuDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(DsuDefOf));
		}
	}
}
namespace DigitalStorageUnit.compat
{
	public interface IRemoveStorageInspectionTab
	{
	}
	public interface ILwmDsLeaveMeAlonePlease
	{
	}
}
