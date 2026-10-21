using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: AssemblyVersion("0.0.0.0")]
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
namespace QuantumStorageRedux
{
	internal class Alert_NoQuantumStorageSpace : Alert
	{
		protected override Color BGColor { get; } = new Color(1f, 0.9215686f, 0.01568628f, 0.35f);

		public Alert_NoQuantumStorageSpace()
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			base.defaultLabel = TaggedString.op_Implicit(Translator.Translate("NoQuantumStorageSpace"));
			base.defaultExplanation = TaggedString.op_Implicit(Translator.Translate("NoQuantumStorageSpaceDesc"));
			base.defaultPriority = (AlertPriority)1;
		}

		public override AlertReport GetReport()
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			if (!QNetworkManager.AnyNetworkIsFull())
			{
				return AlertReport.op_Implicit(false);
			}
			Map val = GenCollection.FirstOrDefault<Map>(Find.Maps, (Predicate<Map>)((Map map) => map.IsPlayerHome));
			if (val == null)
			{
				return AlertReport.op_Implicit(true);
			}
			Building[] array = val.listerBuildings.allBuildingsColonist.Where((Building building) => ThingCompUtility.TryGetComp<CompQSRStockpile>((Thing)(object)building) != null || ThingCompUtility.TryGetComp<CompQSRWarehouse>((Thing)(object)building) != null || ThingCompUtility.TryGetComp<CompQSRRelay>((Thing)(object)building) != null).ToArray();
			if (!array.Any())
			{
				return AlertReport.op_Implicit(true);
			}
			AlertReport val2 = new AlertReport
			{
				culpritsThings = new List<Thing>(),
				active = true
			};
			Building[] array2 = array;
			foreach (Building item in array2)
			{
				val2.culpritsThings.Add((Thing)(object)item);
			}
			return val2;
		}
	}
	internal class CompProperties_QSRRelay : CompProperties
	{
		public CompProperties_QSRRelay()
		{
			base.compClass = typeof(CompQSRRelay);
		}
	}
	internal class CompProperties_QSRStockpile : CompProperties
	{
		public CompProperties_QSRStockpile()
		{
			base.compClass = typeof(CompQSRStockpile);
		}
	}
	internal class CompProperties_QSRWarehouse : CompProperties
	{
		public CompProperties_QSRWarehouse()
		{
			base.compClass = typeof(CompQSRWarehouse);
		}
	}
	internal class CompQSRRelay : ThingComp
	{
		private QNetwork network;

		private CompPowerTrader powerTrader;

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			((ThingComp)this).PostSpawnSetup(respawningAfterLoad);
			powerTrader = ThingCompUtility.TryGetComp<CompPowerTrader>((Thing)(object)base.parent);
			network = QNetworkManager.Get(((Thing)base.parent).Map);
			network.RegisterInput(powerTrader, GenAdj.CellsAdjacent8Way((Thing)(object)base.parent));
			network.RegisterRelay(powerTrader, GenAdj.CellsOccupiedBy((Thing)(object)base.parent));
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = (DestroyMode)0)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostDeSpawn(map, mode);
			network.UnregisterInput(powerTrader);
			network.UnregisterRelay(powerTrader);
		}

		public override string CompInspectStringExtra()
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("QSRComponents_GlobalNetwork")));
			stringBuilder.Append("\n");
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("QSRComponents_MaxStacks")));
			stringBuilder.Append(" ");
			stringBuilder.Append(network.CellCapacity().ToString("F0"));
			return stringBuilder.ToString();
		}

		public override void CompTick()
		{
			relayTick(100);
		}

		private void relayTick(int tickAmount)
		{
			if (Find.TickManager.TicksGame % tickAmount == 0 && Utils.PoweredOn(powerTrader))
			{
				network.Mantain(((Thing)base.parent).Map, Find.TickManager.TicksGame);
			}
		}
	}
	internal class CompQSRStockpile : ThingComp
	{
		private QNetwork network;

		private CompPowerTrader powerTrader;

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			((ThingComp)this).PostSpawnSetup(respawningAfterLoad);
			powerTrader = ThingCompUtility.TryGetComp<CompPowerTrader>((Thing)(object)base.parent);
			network = new QNetwork();
			network.RegisterInput(powerTrader, GenAdj.CellsAdjacent8Way((Thing)(object)base.parent));
			network.RegisterStorage(powerTrader, GenAdj.CellsOccupiedBy((Thing)(object)base.parent));
			QNetworkManager.RegisterLocalNetwork(network);
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = (DestroyMode)0)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostDeSpawn(map, mode);
			QNetworkManager.UnregisterLocalNetwork(network);
		}

		public override string CompInspectStringExtra()
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("QSRComponents_LocalNetwork")));
			stringBuilder.Append("\n");
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("QSRComponents_MaxStacks")));
			stringBuilder.Append(" ");
			stringBuilder.Append(network.CellCapacity().ToString("F0"));
			return stringBuilder.ToString();
		}

		public override void CompTick()
		{
			stockpileTick(100);
		}

		private void stockpileTick(int tickAmount)
		{
			if (Find.TickManager.TicksGame % tickAmount == 0 && Utils.PoweredOn(powerTrader))
			{
				network.Mantain(((Thing)base.parent).Map, Find.TickManager.TicksGame);
			}
		}
	}
	internal class CompQSRWarehouse : ThingComp
	{
		private QNetwork network;

		private CompPowerTrader powerTrader;

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			((ThingComp)this).PostSpawnSetup(respawningAfterLoad);
			powerTrader = ThingCompUtility.TryGetComp<CompPowerTrader>((Thing)(object)base.parent);
			network = QNetworkManager.Get(((Thing)base.parent).Map);
			network.RegisterInput(powerTrader, GenAdj.CellsAdjacent8Way((Thing)(object)base.parent));
			network.RegisterStorage(powerTrader, GenAdj.CellsOccupiedBy((Thing)(object)base.parent));
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = (DestroyMode)0)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostDeSpawn(map, mode);
			network.UnregisterInput(powerTrader);
			network.UnregisterStorage(powerTrader);
		}

		public override string CompInspectStringExtra()
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("QSRComponents_GlobalNetwork")));
			stringBuilder.Append("\n");
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("QSRComponents_MaxStacks")));
			stringBuilder.Append(" ");
			stringBuilder.Append(network.CellCapacity().ToString("F0"));
			return stringBuilder.ToString();
		}

		public override void CompTick()
		{
			warehouseTick(100);
		}

		private void warehouseTick(int tickAmount)
		{
			if (Find.TickManager.TicksGame % tickAmount == 0 && Utils.PoweredOn(powerTrader))
			{
				network.Mantain(((Thing)base.parent).Map, Find.TickManager.TicksGame);
			}
		}
	}
	internal static class DeconstructExtenstions
	{
		public static void Deconstruct<T>(this IEnumerable<IGrouping<bool, T>> group, out IEnumerable<T> pos, out IEnumerable<T> neg)
		{
			ILookup<bool, IGrouping<bool, T>> lookup = group.ToLookup((IGrouping<bool, T> x) => x.Key);
			pos = lookup[true].SelectMany((IGrouping<bool, T> x) => x);
			neg = lookup[false].SelectMany((IGrouping<bool, T> x) => x);
		}
	}
	internal class DeSpawnAction(Thing thing) : IPerformable
	{
		public void Perform()
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			if (thing.Spawned)
			{
				Utils.ThrowDustPuff(thing.Map, thing.Position);
				((Entity)thing).DeSpawn((DestroyMode)0);
			}
		}

		public string Display()
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			return $"DeSpawn {LogUtils.Display(thing)} on {thing.Position}";
		}
	}
	internal class DestroyAction(Thing thing) : IPerformable
	{
		public void Perform()
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			if (!thing.Destroyed)
			{
				Utils.ThrowDustPuff(thing.Map, thing.Position);
				thing.Destroy((DestroyMode)0);
			}
		}

		public string Display()
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			return $"Destroy {LogUtils.Display(thing)} on {thing.Position}";
		}
	}
	[HarmonyPatch(typeof(GenSpawn), "Spawn", new Type[]
	{
		typeof(Thing),
		typeof(IntVec3),
		typeof(Map),
		typeof(Rot4),
		typeof(WipeMode),
		typeof(bool),
		typeof(bool)
	})]
	public static class GenSpawn_Spawn
	{
		public static bool Prefix(out Thing __result, Thing newThing, IntVec3 loc, Map map, Rot4 rot, WipeMode wipeMode = (WipeMode)0, bool respawningAfterLoad = false, bool forbidLeavings = false)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			__result = null;
			if (!respawningAfterLoad)
			{
				__result = newThing;
				return true;
			}
			__result = Utils.Spawn(newThing, loc, map, rot, wipeMode, respawningAfterLoad: true);
			return false;
		}
	}
	internal interface IPerformable
	{
		void Perform();

		string Display();
	}
	internal static class LinqExtensions
	{
		internal static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> seq)
		{
			return seq.Select((T item, int index) => (item: item, index: index));
		}

		internal static IEnumerable<(TA, TB)> Zip<TA, TB>(this IEnumerable<TA> seqA, IEnumerable<TB> seqB)
		{
			if (seqA == null)
			{
				throw new ArgumentNullException("seqA");
			}
			if (seqB == null)
			{
				throw new ArgumentNullException("seqB");
			}
			using IEnumerator<TA> iteratorA = seqA.GetEnumerator();
			using IEnumerator<TB> iteratorB = seqB.GetEnumerator();
			while (iteratorA.MoveNext() && iteratorB.MoveNext())
			{
				yield return (iteratorA.Current, iteratorB.Current);
			}
		}
	}
	public static class LogUtils
	{
		public static string Display(Thing thing)
		{
			return $"{((Def)thing.def).defName} ({thing.stackCount})";
		}

		internal static string Display(QThing thing)
		{
			return $"{((Def)thing.def).defName} ({thing.stackCount})";
		}

		internal static string Display(IEnumerable<QThing> list)
		{
			if (!list.Any())
			{
				return "Empty";
			}
			return list.Aggregate(new StringBuilder(), delegate(StringBuilder acc, QThing thing)
			{
				acc.Append(Display(thing) + " | ");
				return acc;
			}).ToString();
		}
	}
	internal class ModController : Mod
	{
		public ModController(ModContentPack content)
			: base(content)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			new Harmony("pw.cheetah.rimworld.quantumstorageredux").PatchAll(Assembly.GetExecutingAssembly());
		}
	}
	internal class PlaceWorker_QSRNoQSOverlap : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			if (GenAdj.CellsAdjacent8Way(loc, rot, checkingDef.Size).Union(GenAdj.CellsOccupiedBy(loc, rot, checkingDef.Size)).SelectMany((IntVec3 cell) => map.thingGrid.ThingsListAt(cell))
				.Any(delegate(Thing thingToCheck)
				{
					if (ThingCompUtility.TryGetComp<CompQSRStockpile>(thingToCheck) != null || ThingCompUtility.TryGetComp<CompQSRWarehouse>(thingToCheck) != null || ThingCompUtility.TryGetComp<CompQSRRelay>(thingToCheck) != null)
					{
						return true;
					}
					BuildableDef entityDefToBuild = thingToCheck.def.entityDefToBuild;
					ThingDef val = (ThingDef)(object)((entityDefToBuild is ThingDef) ? entityDefToBuild : null);
					return val != null && val.comps != null && GenCollection.Any<CompProperties>(val.comps, (Predicate<CompProperties>)((CompProperties comp) => Utils.QuantumCompClasses.Contains(comp.compClass)));
				}))
			{
				return AcceptanceReport.op_Implicit(Translator.Translate("PlaceWorker_QSRNoQSOverlap"));
			}
			return AcceptanceReport.op_Implicit(true);
		}
	}
	internal class PlaceWorker_QSRRequireWalkable : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			if (GenAdj.CellsOccupiedBy(loc, rot, checkingDef.Size).Any((IntVec3 cell) => !GenGrid.Walkable(cell, map)))
			{
				return AcceptanceReport.op_Implicit(Translator.Translate("PlaceWorker_QSRRequireWalkable"));
			}
			return AcceptanceReport.op_Implicit(true);
		}
	}
	internal class QCell(IntVec3 cell, StorageSettings storageSettings, QThing[] qthings)
	{
		public readonly QThing[] qthings = qthings;

		public readonly StorageSettings storageSettings = storageSettings;

		public IntVec3 cell = cell;
	}
	[StaticConstructorOnStartup]
	internal class QLog
	{
		public enum Ctx
		{
			Default,
			Insertion,
			Relay,
			Storage,
			Thing
		}

		private static readonly Dictionary<Ctx, bool> displaySettings = new Dictionary<Ctx, bool>
		{
			{
				Ctx.Default,
				true
			},
			{
				Ctx.Insertion,
				true
			},
			{
				Ctx.Relay,
				true
			},
			{
				Ctx.Storage,
				true
			},
			{
				Ctx.Thing,
				true
			}
		};

		[Conditional("DEBUG")]
		public static void Message(Ctx ctx, string text)
		{
			if (displaySettings[ctx])
			{
				Log.Message($"QSR.{ctx}: {text}");
				FileLog.Log($"QSR.{ctx}: {text}");
			}
		}

		[Conditional("DEBUG")]
		public static void Warning(Ctx ctx, string text)
		{
			if (displaySettings[ctx])
			{
				Log.Warning($"QSR.{ctx}: {text}");
				FileLog.Log($"QSR.{ctx}: {text}");
			}
		}

		[Conditional("DEBUG")]
		public static void Error(Ctx ctx, string text)
		{
			if (displaySettings[ctx])
			{
				Log.Error($"QSR.{ctx}: {text}");
				FileLog.Log($"QSR.{ctx}: {text}");
			}
		}

		[Conditional("DEBUG")]
		public static void Message(string text)
		{
		}

		[Conditional("DEBUG")]
		public static void Warning(string text)
		{
		}

		[Conditional("DEBUG")]
		public static void Error(string text)
		{
		}
	}
	internal class QNetwork
	{
		private const int BASE_CAPACITY = 2;

		private const int RESEARCH_STEP_INCREMENT = 2;

		private readonly Dictionary<CompPowerTrader, IEnumerable<IntVec3>> input;

		private readonly Dictionary<CompPowerTrader, IEnumerable<IntVec3>> relays;

		private readonly Dictionary<CompPowerTrader, IEnumerable<IntVec3>> storage;

		private int cellCapacity;

		private int currentTick;

		private bool isFull;

		public QNetwork()
		{
			input = new Dictionary<CompPowerTrader, IEnumerable<IntVec3>>();
			storage = new Dictionary<CompPowerTrader, IEnumerable<IntVec3>>();
			relays = new Dictionary<CompPowerTrader, IEnumerable<IntVec3>>();
			currentTick = 0;
			cellCapacity = calculateCellCapacity();
			isFull = false;
		}

		public int CellCapacity()
		{
			return cellCapacity;
		}

		public bool IsFull()
		{
			return isFull;
		}

		public void RegisterInput(CompPowerTrader powerTrader, IEnumerable<IntVec3> cells)
		{
			input.Add(powerTrader, cells);
		}

		public void UnregisterInput(CompPowerTrader powerTrader)
		{
			input.Remove(powerTrader);
		}

		public void RegisterStorage(CompPowerTrader powerTrader, IEnumerable<IntVec3> cells)
		{
			storage.Add(powerTrader, cells);
		}

		public void UnregisterStorage(CompPowerTrader powerTrader)
		{
			storage.Remove(powerTrader);
		}

		public void RegisterRelay(CompPowerTrader powerTrader, IEnumerable<IntVec3> cells)
		{
			relays.Add(powerTrader, cells);
		}

		public void UnregisterRelay(CompPowerTrader powerTrader)
		{
			relays.Remove(powerTrader);
		}

		public void Mantain(Map map, int tick)
		{
			if (currentTick == tick)
			{
				return;
			}
			currentTick = tick;
			cellCapacity = calculateCellCapacity();
			HashSet<IntVec3> intVec3S = new HashSet<IntVec3>(input.Where((KeyValuePair<CompPowerTrader, IEnumerable<IntVec3>> powerTrader) => Utils.PoweredOn(powerTrader.Key)).SelectMany((KeyValuePair<CompPowerTrader, IEnumerable<IntVec3>> x) => x.Value));
			List<IntVec3> incomingCells = storage.Where((KeyValuePair<CompPowerTrader, IEnumerable<IntVec3>> storeSpace) => Utils.PoweredOn(storeSpace.Key)).SelectMany((KeyValuePair<CompPowerTrader, IEnumerable<IntVec3>> x) => x.Value).ToList();
			List<QStorage> source = (from cells in relays.Where(delegate(KeyValuePair<CompPowerTrader, IEnumerable<IntVec3>> x)
				{
					KeyValuePair<CompPowerTrader, IEnumerable<IntVec3>> keyValuePair = x;
					return Utils.PoweredOn(keyValuePair.Key);
				}).Select(delegate(KeyValuePair<CompPowerTrader, IEnumerable<IntVec3>> x)
				{
					KeyValuePair<CompPowerTrader, IEnumerable<IntVec3>> keyValuePair = x;
					return keyValuePair.Value.ToList();
				}).ToList()
				select new QStorage(QStorage.Kind.Relay, 1).FromCells(map, cells, (StorageSettings storageSettings, Thing thing) => storageSettings.AllowedToAccept(thing) && thing.stackCount >= thing.def.stackLimit)).ToList();
			QStorage quantumStorage = new QStorage(QStorage.Kind.Storage, cellCapacity).FromCells(map, incomingCells, (StorageSettings _, Thing thing) => thing.stackCount >= thing.def.stackLimit);
			(from thing in Enumerable.Concat(second: from thing in intVec3S.Where((IntVec3 cell) => (int)Utils.Priority(map, cell) > 0).SelectMany((IntVec3 cell) => Utils.GetItemList(map, cell))
					select new QThing(thing, QThing.Source.Input), first: source.SelectMany((QStorage qrelay) => qrelay.ExcludedThings()).Concat(quantumStorage.ExcludedThings()))
				group thing by thing.stackCount < thing.def.stackLimit).Deconstruct(out var pos, out var neg);
			List<QThing> insertionQueue = Enumerable.Concat(second: mergePartialStacks(pos), first: neg).ToList();
			List<IPerformable> second = source.SelectMany(delegate(QStorage relay)
			{
				relay.InsertToRelay(insertionQueue, quantumStorage);
				return relay.Diff();
			}).ToList();
			quantumStorage.Insert(insertionQueue);
			List<IPerformable> second2 = quantumStorage.Diff();
			isFull = GenCollection.Any<QThing>(insertionQueue);
			foreach (IPerformable item in insertionQueue.SelectMany((QThing qthing) => qthing.MoveOut(map, GenCollection.RandomElement<IntVec3>(intVec3S)).actions).Concat(second).Concat(second2))
			{
				item.Perform();
			}
		}

		private static int calculateCellCapacity()
		{
			int num = 2;
			if (DefDatabase<ResearchProjectDef>.GetNamed("ResearchProject_QSRCapacityUpgrade_1", true).IsFinished)
			{
				num += 2;
			}
			if (DefDatabase<ResearchProjectDef>.GetNamed("ResearchProject_QSRCapacityUpgrade_2", true).IsFinished)
			{
				num += 2;
			}
			if (DefDatabase<ResearchProjectDef>.GetNamed("ResearchProject_QSRCapacityUpgrade_3", true).IsFinished)
			{
				num += 2;
			}
			if (DefDatabase<ResearchProjectDef>.GetNamed("ResearchProject_QSRCapacityUpgrade_4", true).IsFinished)
			{
				num += 2;
			}
			return num;
		}

		private static List<QThing> mergePartialStacks(IEnumerable<QThing> partialStacks)
		{
			return (from x in partialStacks
				group x by ((Def)x.def).shortHash).Aggregate(new List<QThing>(), delegate(List<QThing> quantumThings, IGrouping<ushort, QThing> defStacks)
			{
				QThing qThing = defStacks.Aggregate(new QThing(defStacks.First().thing, QThing.Source.Merge), delegate(QThing composite, QThing quantumThing)
				{
					int num = quantumThing.stackCount;
					int num2 = composite.stackCount + num - quantumThing.def.stackLimit;
					if (num2 > 0)
					{
						num -= num2;
					}
					composite.Absorb(quantumThing, num);
					if (composite.stackCount != composite.def.stackLimit)
					{
						return composite;
					}
					quantumThings.Add(composite);
					return new QThing(quantumThing.thing, QThing.Source.Merge).Absorb(quantumThing, num2);
				});
				if (qThing.stackCount > 0)
				{
					quantumThings.Add(qThing);
				}
				return quantumThings;
			});
		}
	}
	[StaticConstructorOnStartup]
	internal static class QNetworkManager
	{
		private static readonly HashSet<QNetwork> localNetworks = new HashSet<QNetwork>();

		private static readonly Dictionary<Map, QNetwork> globalNetworks = new Dictionary<Map, QNetwork>();

		public static QNetwork Get(Map map)
		{
			if (globalNetworks.TryGetValue(map, out var value))
			{
				return value;
			}
			QNetwork qNetwork = new QNetwork();
			globalNetworks.Add(map, qNetwork);
			return qNetwork;
		}

		public static void RegisterLocalNetwork(QNetwork network)
		{
			localNetworks.Add(network);
		}

		public static void UnregisterLocalNetwork(QNetwork network)
		{
			localNetworks.Remove(network);
		}

		public static bool AnyNetworkIsFull()
		{
			if (!globalNetworks.Any((KeyValuePair<Map, QNetwork> pair) => pair.Value.IsFull()))
			{
				return localNetworks.Any((QNetwork network) => network.IsFull());
			}
			return true;
		}
	}
	internal class QStorage
	{
		public enum Kind
		{
			Relay,
			Storage
		}

		private readonly int cellCapacity;

		private readonly Dictionary<ThingDef, List<(int cellIndex, int thingIndex)>> defsIndex;

		private readonly List<QThing> excluded;

		private readonly HashSet<ThingDef> existingDefs;

		private readonly Kind kind;

		private List<IntVec3> cells;

		private Map map;

		private List<QCell> storage;

		public QStorage(Kind kind, int cellCapacity)
		{
			this.kind = kind;
			this.cellCapacity = cellCapacity;
			excluded = new List<QThing>();
			switch (this.kind)
			{
			case Kind.Relay:
				existingDefs = new HashSet<ThingDef>();
				break;
			case Kind.Storage:
				defsIndex = new Dictionary<ThingDef, List<(int, int)>>();
				break;
			}
		}

		public QStorage FromCells(Map incomingMap, List<IntVec3> incomingCells, Func<StorageSettings, Thing, bool> isAllowed)
		{
			map = incomingMap;
			cells = incomingCells;
			storage = cells.Select(delegate(IntVec3 cell, int cellIndex)
			{
				//IL_000b: Unknown result type (might be due to invalid IL or missing references)
				//IL_002e: Unknown result type (might be due to invalid IL or missing references)
				//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
				StorageSettings storageSettings = Utils.GetStorageSettings(map, cell);
				QThing[] array = new QThing[cellCapacity];
				foreach (var (val, num) in Utils.GetItemList(map, cell).WithIndex())
				{
					if (num >= cellCapacity)
					{
						break;
					}
					if (!isAllowed(storageSettings, val))
					{
						excluded.Add(new QThing(val, QThing.Source.Storage));
					}
					else
					{
						switch (kind)
						{
						case Kind.Relay:
							existingDefs.Add(val.def);
							break;
						case Kind.Storage:
							indexDef(val.def, cellIndex, num);
							break;
						}
						array[num] = new QThing(val, QThing.Source.Storage);
					}
				}
				return new QCell(cell, storageSettings, array);
			}).ToList();
			return this;
		}

		public void InsertToRelay(List<QThing> insertionQueue, QStorage reqStorage)
		{
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			foreach (QCell qcell in storage)
			{
				if (qcell.qthings[0] != null)
				{
					continue;
				}
				foreach (ThingDef allowedThingDef in qcell.storageSettings.filter.AllowedThingDefs)
				{
					if (!existingDefs.Contains(allowedThingDef))
					{
						QThing qThing = reqStorage.requestWithDef(allowedThingDef);
						if (qThing != null && qThing.AllowedByStorage(qcell.storageSettings))
						{
							existingDefs.Add(allowedThingDef);
							qcell.qthings[0] = qThing.Insert(map, qcell.cell);
							break;
						}
					}
				}
				if (qcell.qthings[0] != null)
				{
					continue;
				}
				int num = insertionQueue.FindIndex(delegate(QThing x)
				{
					//IL_0064: Unknown result type (might be due to invalid IL or missing references)
					if (existingDefs.Contains(x.def))
					{
						return false;
					}
					if (!x.AllowedByStorage(qcell.storageSettings))
					{
						return false;
					}
					existingDefs.Add(x.def);
					qcell.qthings[0] = x.Insert(map, qcell.cell);
					return true;
				});
				if (num > -1)
				{
					insertionQueue.RemoveAt(num);
				}
			}
		}

		public void Insert(List<QThing> insertionQueue)
		{
			if (!GenCollection.Any<QThing>(insertionQueue))
			{
				return;
			}
			foreach (QCell qcell2 in storage.OrderByDescending(delegate(QCell qCell)
			{
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				StorageSettings storageSettings = qCell.storageSettings;
				return (storageSettings == null) ? ((StoragePriority)0) : storageSettings.Priority;
			}))
			{
				if (!GenCollection.Any<QThing>(insertionQueue))
				{
					break;
				}
				if (qcell2.storageSettings == null)
				{
					continue;
				}
				foreach (var item in qcell2.qthings.WithIndex())
				{
					var (qThing, qindex) = item;
					if (!GenCollection.Any<QThing>(insertionQueue))
					{
						return;
					}
					if (qcell2.qthings.Count((QThing x) => x != null) >= cellCapacity || qThing != null)
					{
						continue;
					}
					int num = insertionQueue.FindIndex(delegate(QThing x)
					{
						//IL_004c: Unknown result type (might be due to invalid IL or missing references)
						if (!x.AllowedByStorage(qcell2.storageSettings))
						{
							return false;
						}
						qcell2.qthings[qindex] = x.Insert(map, qcell2.cell);
						return true;
					});
					if (num > -1)
					{
						insertionQueue.RemoveAt(num);
					}
				}
			}
		}

		public List<IPerformable> Diff()
		{
			return cells.Zip(storage).Aggregate(new List<IPerformable>(), delegate(List<IPerformable> actions, (IntVec3, QCell) x)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				IntVec3 item = x.Item1;
				QCell item2 = x.Item2;
				List<Thing> itemList = Utils.GetItemList(map, item);
				for (int i = 0; i < cellCapacity; i++)
				{
					Thing val = itemList.ElementAtOrDefault(i);
					QThing qThing = item2.qthings[i];
					if (qThing != null && (val == null || qThing.source == QThing.Source.Composite || val.def != qThing.def || val.stackCount != qThing.stackCount || val.HitPoints != qThing.hitPoints))
					{
						actions = actions.Concat(qThing.actions).ToList();
					}
				}
				return actions;
			});
		}

		public List<QThing> ExcludedThings()
		{
			return excluded;
		}

		private QThing requestWithDef(ThingDef def)
		{
			if (defsIndex == null || !defsIndex.ContainsKey(def) || !GenCollection.Any<(int, int)>(defsIndex[def]))
			{
				return null;
			}
			(int cellIndex, int thingIndex) tuple = defsIndex[def].Last();
			int item = tuple.cellIndex;
			int item2 = tuple.thingIndex;
			QThing result = storage[item].qthings[item2];
			GenCollection.RemoveLast<(int, int)>(defsIndex[def]);
			storage[item].qthings[item2] = null;
			return result;
		}

		public string Display()
		{
			return storage.Aggregate(new StringBuilder($"{this}#{GetHashCode()}\n"), delegate(StringBuilder acc, QCell qcell)
			{
				//IL_000e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0013: Unknown result type (might be due to invalid IL or missing references)
				//IL_001f: Unknown result type (might be due to invalid IL or missing references)
				IntVec3 cell = qcell.cell;
				acc.Append($"{cell}: | ");
				qcell.qthings.Select((QThing qthing) => (qthing == null) ? "null | " : (LogUtils.Display(qthing) + " | ")).ToList().ForEach(delegate(string row)
				{
					acc.Append(row);
				});
				acc.Append("\n");
				return acc;
			}).ToString();
		}

		private void indexDef(ThingDef def, int cellIndex, int thingIndex)
		{
			if (!defsIndex.ContainsKey(def))
			{
				defsIndex.Add(def, new List<(int, int)>());
			}
			defsIndex[def].Add((cellIndex, thingIndex));
		}
	}
	internal class QThing
	{
		public enum Source
		{
			Input,
			Merge,
			Composite,
			Storage
		}

		private readonly List<QThing> absorbed;

		public readonly ThingDef def;

		private readonly GeneSet geneSet;

		private readonly ThingDef stuff;

		public readonly Thing thing;

		public List<IPerformable> actions;

		public int hitPoints;

		public Source source;

		public int stackCount;

		public QThing(Thing thing, Source source)
		{
			this.source = source;
			this.thing = thing;
			Genepack val = (Genepack)(object)((thing is Genepack) ? thing : null);
			if (val != null)
			{
				geneSet = ((GeneSetHolderBase)val).GeneSet;
			}
			def = thing.def;
			stuff = thing.Stuff;
			actions = new List<IPerformable>();
			if (this.source != Source.Merge)
			{
				stackCount = thing.stackCount;
				hitPoints = thing.HitPoints;
			}
			if (this.source == Source.Merge)
			{
				absorbed = new List<QThing>();
			}
		}

		public QThing Absorb(QThing qThing, int amount)
		{
			if (thing.def.useHitPoints)
			{
				hitPoints = Mathf.CeilToInt((float)(hitPoints * stackCount + qThing.thing.HitPoints * amount) / (float)(stackCount + amount));
			}
			stackCount += amount;
			absorbed.Add(qThing);
			source = ((absorbed.Count <= 1) ? Source.Merge : Source.Composite);
			return this;
		}

		public bool AllowedByStorage(StorageSettings storageSettings)
		{
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			if (storageSettings == null)
			{
				return false;
			}
			if (source == Source.Composite)
			{
				if (!Utils.Allows(storageSettings, this))
				{
					return false;
				}
			}
			else
			{
				if (!storageSettings.AllowedToAccept(thing))
				{
					return false;
				}
				if (storageSettings.Priority < Utils.Priority(thing.Map, thing.Position))
				{
					return false;
				}
			}
			return true;
		}

		public Thing Make()
		{
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Expected O, but got Unknown
			//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0103: Unknown result type (might be due to invalid IL or missing references)
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			Thing val = thing;
			Corpse val2 = (Corpse)(object)((val is Corpse) ? val : null);
			if (val2 == null)
			{
				UnfinishedThing val3 = (UnfinishedThing)(object)((val is UnfinishedThing) ? val : null);
				if (val3 == null)
				{
					if (val is Genepack)
					{
						Genepack val4 = (Genepack)ThingMaker.MakeThing(ThingDefOf.Genepack, (ThingDef)null);
						val4.Initialize(geneSet.GenesListForReading);
						return (Thing)val4;
					}
				}
				else
				{
					Thing obj = ThingMaker.MakeThing(((Thing)val3).def, stuff);
					UnfinishedThing val5 = (UnfinishedThing)(object)((obj is UnfinishedThing) ? obj : null);
					if (val5 != null)
					{
						val5.workLeft = val3.workLeft;
						val5.Creator = val3.Creator;
						return (Thing)(object)val5;
					}
				}
				Thing val6 = ThingMaker.MakeThing(def, stuff);
				CompQuality val7 = ThingCompUtility.TryGetComp<CompQuality>(val6);
				CompQuality val8 = ThingCompUtility.TryGetComp<CompQuality>(thing);
				if (val8 != null)
				{
					_ = val8.Quality;
					if (val7 != null)
					{
						_ = val7.Quality;
						if (val7.Quality != val8.Quality)
						{
							ThingCompUtility.TryGetComp<CompQuality>(val6).SetQuality(val8.Quality, (ArtGenerationContext?)(ArtGenerationContext)1);
						}
					}
				}
				val6.HitPoints = hitPoints;
				val6.stackCount = stackCount;
				return val6;
			}
			Thing obj2 = ThingMaker.MakeThing(val2.InnerPawn.RaceProps.corpseDef, (ThingDef)null);
			Corpse val9 = (Corpse)(object)((obj2 is Corpse) ? obj2 : null);
			val2.GetDirectlyHeldThings().TryTransferToContainer((Thing)(object)val2.InnerPawn, (val9 != null) ? val9.GetDirectlyHeldThings() : null, true);
			return (Thing)(object)val9;
		}

		public QThing Insert(Map map, IntVec3 cell)
		{
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			if (source == Source.Composite)
			{
				actions.Add(new SpawnAction(map, cell, this));
				actions = actions.Concat(((IEnumerable<QThing>)absorbed).Select((Func<QThing, IPerformable>)((QThing qThing) => new DestroyAction(qThing.thing)))).ToList();
			}
			else
			{
				actions.Add(new SpawnAction(map, cell, this));
				actions.Add(new DeSpawnAction(thing));
			}
			return this;
		}

		public QThing MoveOut(Map map, IntVec3 cell)
		{
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			int num;
			switch (source)
			{
			case Source.Merge:
				num = 0;
				goto IL_0024;
			case Source.Composite:
				num = 1;
				goto IL_0024;
			default:
				actions.Add(new SpawnNearAction(map, cell, this));
				actions.Add(new DeSpawnAction(thing));
				break;
			case Source.Input:
				break;
				IL_0024:
				if (absorbed.All((QThing qThing) => qThing.source == Source.Input))
				{
					break;
				}
				if (num != 0)
				{
					if (num == 1)
					{
						actions.Add(new SpawnNearAction(map, cell, this));
						actions = actions.Concat(((IEnumerable<QThing>)absorbed).Select((Func<QThing, IPerformable>)((QThing qThing) => new DestroyAction(qThing.thing)))).ToList();
					}
					break;
				}
				goto default;
			}
			return this;
		}
	}
	internal class SpawnAction(Map map, IntVec3 cell, QThing qthing) : IPerformable
	{
		public void Perform()
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			Utils.DropSound(map, cell, qthing.def);
			Utils.Spawn(qthing.Make(), cell, map, Rot4.North, (WipeMode)0);
		}

		public string Display()
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			string arg = LogUtils.Display(qthing);
			return $"Spawn {arg} on {cell}";
		}
	}
	internal class SpawnNearAction(Map map, IntVec3 cell, QThing qthing) : IPerformable
	{
		public void Perform()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			GenPlace.TryPlaceThing(qthing.Make(), cell, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
		}

		public string Display()
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			string arg = LogUtils.Display(qthing);
			return $"SpawnNear {arg} on {cell}";
		}
	}
	public static class Utils
	{
		public static readonly Type[] QuantumCompClasses = new Type[3]
		{
			typeof(CompQSRStockpile),
			typeof(CompQSRWarehouse),
			typeof(CompQSRRelay)
		};

		public static StorageSettings GetStorageSettings(Map map, IntVec3 cell)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			if (GridsUtility.GetZone(cell, map) != null && ((object)GridsUtility.GetZone(cell, map)).GetType().ToString().Equals("RimWorld.Zone_Stockpile"))
			{
				Zone zone = GridsUtility.GetZone(cell, map);
				Zone obj = ((zone is Zone_Stockpile) ? zone : null);
				if (obj == null)
				{
					return null;
				}
				return ((Zone_Stockpile)obj).GetStoreSettings();
			}
			Thing obj2 = GridsUtility.GetThingList(cell, map).Find((Thing x) => ((object)x).GetType().ToString().Equals("RimWorld.Building_Storage"));
			Thing obj3 = ((obj2 is Building_Storage) ? obj2 : null);
			if (obj3 == null)
			{
				return null;
			}
			return ((Building_Storage)obj3).GetStoreSettings();
		}

		public static StoragePriority Priority(Map map, IntVec3 cell)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			StorageSettings storageSettings = GetStorageSettings(map, cell);
			if (storageSettings == null)
			{
				return (StoragePriority)0;
			}
			return storageSettings.Priority;
		}

		internal static bool Allows(StorageSettings storageSettings, QThing qthing)
		{
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			if (!storageSettings.filter.Allows(qthing.def))
			{
				return false;
			}
			if (!qthing.def.useHitPoints)
			{
				return true;
			}
			float num = GenMath.RoundedHundredth((float)qthing.hitPoints / (float)qthing.thing.MaxHitPoints);
			FloatRange allowedHitPointsPercents = storageSettings.filter.AllowedHitPointsPercents;
			return ((FloatRange)(ref allowedHitPointsPercents)).IncludesEpsilon(Mathf.Clamp01(num));
		}

		public static List<Thing> GetItemList(Map map, IntVec3 cell)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			return (from x in GridsUtility.GetThingList(cell, map)
				where (int)x.def.category == 2
				select x).ToList();
		}

		public static bool PoweredOn(CompPowerTrader powerTrader)
		{
			if (powerTrader != null)
			{
				return powerTrader.PowerOn;
			}
			return false;
		}

		public static void ThrowDustPuff(Map map, IntVec3 cell)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			FleckMaker.ThrowDustPuff(((IntVec3)(ref cell)).ToVector3() + new Vector3(0.5f, 0f, 0.5f), map, 0.5f);
		}

		public static void DropSound(Map map, IntVec3 cell, ThingDef thingDef)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			SoundDef soundDrop = thingDef.soundDrop;
			if (soundDrop != null)
			{
				SoundStarter.PlayOneShot(soundDrop, SoundInfo.InMap(new TargetInfo(cell, map, false), (MaintenanceType)0));
			}
		}

		public unsafe static Thing Spawn(Thing thing, IntVec3 loc, Map map, Rot4 rot, WipeMode wipeMode = (WipeMode)0, bool respawningAfterLoad = false)
		{
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_012b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0124: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			//IL_0136: Unknown result type (might be due to invalid IL or missing references)
			//IL_0137: Unknown result type (might be due to invalid IL or missing references)
			//IL_012f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0132: Invalid comparison between Unknown and I4
			//IL_0147: Unknown result type (might be due to invalid IL or missing references)
			//IL_0148: Unknown result type (might be due to invalid IL or missing references)
			//IL_016a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0171: Unknown result type (might be due to invalid IL or missing references)
			//IL_01af: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b5: Invalid comparison between Unknown and I4
			//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
			if (thing.Spawned)
			{
				Log.Error("Tried to spawn " + Gen.ToStringSafe<Thing>(thing) + " but it's already spawned.");
				return thing;
			}
			if (map == null)
			{
				Log.Error("Tried to spawn " + Gen.ToStringSafe<Thing>(thing) + " in a null map.");
				return null;
			}
			if (!GenGrid.InBounds(loc, map))
			{
				string[] obj = new string[5]
				{
					"Tried to spawn ",
					Gen.ToStringSafe<Thing>(thing),
					" out of bounds at ",
					null,
					null
				};
				IntVec3 val = loc;
				obj[3] = ((object)(*(IntVec3*)(&val))/*cast due to constrained. prefix*/).ToString();
				obj[4] = ".";
				Log.Error(string.Concat(obj));
				return null;
			}
			CellRect val2 = GenAdj.OccupiedRect(loc, rot, ((BuildableDef)thing.def).Size);
			if (!((CellRect)(ref val2)).InBounds(map))
			{
				string[] obj2 = new string[7]
				{
					"Tried to spawn ",
					Gen.ToStringSafe<Thing>(thing),
					" out of bounds at ",
					null,
					null,
					null,
					null
				};
				IntVec3 val3 = loc;
				obj2[3] = ((object)(*(IntVec3*)(&val3))/*cast due to constrained. prefix*/).ToString();
				obj2[4] = " (out of bounds because size is ";
				obj2[5] = ((object)((BuildableDef)thing.def).Size/*cast due to constrained. prefix*/).ToString();
				obj2[6] = ").";
				Log.Error(string.Concat(obj2));
				return null;
			}
			if (thing.def.randomizeRotationOnSpawn)
			{
				rot = Rot4.Random;
			}
			if ((int)wipeMode != 0)
			{
				if ((int)wipeMode == 1)
				{
					GenSpawn.WipeAndRefundExistingThings(loc, rot, (BuildableDef)(object)thing.def, map, false);
				}
			}
			else
			{
				GenSpawn.WipeExistingThings(loc, rot, (BuildableDef)(object)thing.def, map, (DestroyMode)0);
			}
			ThingOwner holdingOwner = thing.holdingOwner;
			if (holdingOwner != null)
			{
				holdingOwner.Remove(thing);
			}
			thing.Position = loc;
			thing.Rotation = rot;
			((Entity)thing).SpawnSetup(map, respawningAfterLoad);
			if (thing.Spawned && thing.stackCount == 0)
			{
				Log.Error($"Spawned thing with 0 stackCount: {thing}");
				thing.Destroy((DestroyMode)0);
				return null;
			}
			if ((int)((BuildableDef)thing.def).passability != 2)
			{
				return thing;
			}
			Enumerator enumerator = ((CellRect)(ref val2)).GetEnumerator();
			try
			{
				while (((Enumerator)(ref enumerator)).MoveNext())
				{
					foreach (Thing item in GridsUtility.GetThingList(((Enumerator)(ref enumerator)).Current, map).ToList())
					{
						if (item != thing)
						{
							Thing obj3 = ((item is Pawn) ? item : null);
							if (obj3 != null)
							{
								((Pawn)obj3).pather.TryRecoverFromUnwalkablePosition(false);
							}
						}
					}
				}
				return thing;
			}
			finally
			{
				((IDisposable)(*(Enumerator*)(&enumerator))/*cast due to constrained. prefix*/).Dispose();
			}
		}
	}
}
