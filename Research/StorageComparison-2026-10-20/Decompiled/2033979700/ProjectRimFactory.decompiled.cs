using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using HarmonyLib;
using LudeonTK;
using Microsoft.CodeAnalysis;
using ProjectRimFactory.Archo.Things;
using ProjectRimFactory.AutoMachineTool;
using ProjectRimFactory.Common;
using ProjectRimFactory.Common.BackCompatibility;
using ProjectRimFactory.Common.HarmonyPatches;
using ProjectRimFactory.Drones;
using ProjectRimFactory.Industry;
using ProjectRimFactory.SAL3.Exposables;
using ProjectRimFactory.SAL3.Things;
using ProjectRimFactory.SAL3.Things.Assemblers;
using ProjectRimFactory.SAL3.Tools;
using ProjectRimFactory.Storage;
using ProjectRimFactory.Storage.Editables;
using ProjectRimFactory.Storage.UI;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName = ".NET Framework 4.7.2")]
[assembly: AssemblyCompany("ProjectRimFactory")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyFileVersion("2.9.5")]
[assembly: AssemblyInformationalVersion("2.9.5+34d93b2e03")]
[assembly: AssemblyProduct("ProjectRimFactory")]
[assembly: AssemblyTitle("ProjectRimFactory")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("2.9.5.0")]
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
namespace ProjectRimFactory
{
	public class GraphicExtraData
	{
		public Vector3? arrowDrawOffset;

		public Vector3? arrowEastDrawOffset;

		public Vector3? arrowWestDrawOffset;

		public Vector3? arrowNorthDrawOffset;

		public Vector3? arrowSouthDrawOffset;

		public string texPath = null;

		public string texPath2 = null;

		public string arrowTexPath1 = null;

		public string arrowTexPath2 = null;

		public GraphicData graphicData1 = null;

		public List<string> specialLinkDefs;

		public string inputString = null;

		public static GraphicExtraData Extract(GraphicRequest req, out GraphicRequest outReq, bool removeExtraFromReq = false)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
			outReq = CopyGraphicRequest(req);
			if (req.graphicData.texPath[0] != '[')
			{
				return null;
			}
			GraphicExtraData graphicExtraData = null;
			try
			{
				XmlDocument xmlDocument = new XmlDocument();
				xmlDocument.LoadXml(req.graphicData.texPath.Replace('[', '<').Replace(']', '>'));
				graphicExtraData = DirectXmlToObject.ObjectFromXml<GraphicExtraData>((XmlNode)xmlDocument.DocumentElement, false);
			}
			catch (Exception ex)
			{
				Log.Error("GraphicExtraData was unable to extract XML from \"" + req.graphicData.texPath + "\"; Exception: " + ex);
				return null;
			}
			graphicExtraData.inputString = req.graphicData.texPath;
			if (removeExtraFromReq)
			{
				outReq.graphicData.texPath = graphicExtraData.texPath;
				outReq.path = graphicExtraData.texPath;
			}
			return graphicExtraData;
		}

		public static GraphicRequest CopyGraphicRequest(GraphicRequest req, string newTexPath = null)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Expected O, but got Unknown
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			GraphicData val = new GraphicData();
			val.CopyFrom(req.graphicData);
			GraphicRequest val2 = default(GraphicRequest);
			((GraphicRequest)(ref val2))..ctor(val.graphicClass, val.texPath, req.shader, req.drawSize, req.color, req.colorTwo, val, req.renderQueue, req.shaderParameters, req.maskPath);
			if (newTexPath == null)
			{
				return val2;
			}
			val2.path = newTexPath;
			val2.graphicData.texPath = newTexPath;
			return val2;
		}
	}
	public interface IHaveGraphicExtraData
	{
		void ExtraInit(GraphicRequest req, GraphicExtraData extraData);
	}
	public class ThingOwner_Conveyor : ThingOwner<Thing>
	{
		public ThingOwner_Conveyor(IThingHolder owner)
			: base(owner)
		{
		}

		public override bool Remove(Thing item)
		{
			if ((object)item.holdingOwner == this)
			{
				Building_BeltConveyor building_BeltConveyor = ((ThingOwner)this).owner as Building_BeltConveyor;
				bool flag = base.Remove(item);
				if (flag)
				{
					building_BeltConveyor.Notify_LostItem(item);
				}
				return flag;
			}
			return base.Remove(item);
		}
	}
	public class CompTransmitsPower : ThingComp
	{
		private static ThingDef undergroundCable;

		private bool hasConduitInt = true;

		private ThingDef TransmitterDef
		{
			get
			{
				if (!(base.props is CompProperties_TransmitsPower compProperties_TransmitsPower))
				{
					Log.Error("PRF CompTransmitsPower is unexpectedly null");
				}
				else if (!(base.parent is IBeltConveyorLinkable { IsUnderground: not false }))
				{
					return compProperties_TransmitsPower.transmitter ?? ThingDefOf.HiddenConduit;
				}
				if (undergroundCable != null)
				{
					return undergroundCable;
				}
				if (GenList.NullOrEmpty<string>((IList<string>)CompProperties_TransmitsPower.possibleUndergroundTransmitters))
				{
					return undergroundCable ?? (undergroundCable = ThingDefOf.HiddenConduit);
				}
				foreach (string possibleUndergroundTransmitter in CompProperties_TransmitsPower.possibleUndergroundTransmitters)
				{
					ThingDef namedSilentFail = DefDatabase<ThingDef>.GetNamedSilentFail(possibleUndergroundTransmitter);
					if (namedSilentFail == null)
					{
						continue;
					}
					undergroundCable = namedSilentFail;
					break;
				}
				return undergroundCable ?? (undergroundCable = ThingDefOf.HiddenConduit);
			}
		}

		public override void PostExposeData()
		{
			((ThingComp)this).PostExposeData();
			Scribe_Values.Look<bool>(ref hasConduitInt, "PRF_CTP_hasConduit", false, false);
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostSpawnSetup(respawningAfterLoad);
			if (respawningAfterLoad || !hasConduitInt)
			{
				return;
			}
			bool flag = false;
			foreach (Thing item in ((Thing)base.parent).Map.thingGrid.ThingsListAt(((Thing)base.parent).Position))
			{
				Thing obj = ((item is Building) ? item : null);
				if (obj != null && ((Building)obj).TransmitsPowerNow)
				{
					flag = true;
					break;
				}
			}
			if (flag || !ProjectRimFactory_ModSettings.PRF_PlaceConveyorCable)
			{
				return;
			}
			Thing val = GenSpawn.Spawn(TransmitterDef, ((Thing)base.parent).Position, ((Thing)base.parent).Map, (WipeMode)0);
			val.SetFaction(Faction.OfPlayer, (Pawn)null);
			hasConduitInt = false;
			List<ThingComp> allComps = base.parent.AllComps;
			for (int i = 0; i < allComps.Count; i++)
			{
				ThingComp obj2 = allComps[i];
				CompPower val2 = (CompPower)(object)((obj2 is CompPower) ? obj2 : null);
				if (val2 != null)
				{
					if (!GravshipPlacementUtility.placingGravship)
					{
						val2.ConnectToTransmitter(ThingCompUtility.TryGetComp<CompPower>(val), false);
					}
					break;
				}
				if ((object)allComps[i] == this)
				{
					Log.Warning("PRF Warning: " + ((Def)((Thing)base.parent).def).defName + " has " + ((object)this).GetType()?.ToString() + " before CompPower!\n  This will make connecting to power grid difficult");
				}
			}
		}
	}
	public class CompProperties_TransmitsPower : CompProperties
	{
		public ThingDef transmitter = null;

		public static List<string> possibleUndergroundTransmitters;

		public CompProperties_TransmitsPower()
		{
			base.compClass = typeof(CompTransmitsPower);
		}
	}
	[StaticConstructorOnStartup]
	public class PlaceWorker_Arrow : PlaceWorker
	{
		public static readonly Material arrow;

		static PlaceWorker_Arrow()
		{
			arrow = FadedMaterialPool.FadedVersionOf(MaterialPool.MatFrom(RS.Arrow), 0.6f);
		}

		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			((PlaceWorker)this).DrawGhost(def, center, rot, ghostCol, thing);
			Vector3 val = ((IntVec3)(ref center)).ToVector3Shifted();
			val.y = Altitudes.AltitudeFor((AltitudeLayer)32);
			Graphics.DrawMesh(MeshPool.plane10, val, ((Rot4)(ref rot)).AsQuat, arrow, 0);
		}
	}
	public class PRFGameComponent : GameComponent
	{
		public List<SpecialSculpture> specialScupltures;

		public List<IAssemblerQueue> AssemblerQueue = new List<IAssemblerQueue>();

		public static Pawn PRF_StaticPawn;

		public static Job PRF_StaticJob;

		public static void GenStaticPawn()
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Expected O, but got Unknown
			PRF_StaticPawn = PawnGenerator.GeneratePawn(PRFDefOf.PRFSlavePawn, Faction.OfPlayer, (PlanetTile?)null);
			PRF_StaticPawn.Name = (Name)new NameTriple("...", "PRF_Static", "...");
		}

		public PRFGameComponent(Game game)
		{
			SpecialSculpture.PreStartGame();
			List<BackCompatibilityConverter> list = (List<BackCompatibilityConverter>)ReflectionUtility.BackCompatibilityConversionChain.GetValue(null);
			list.Add((BackCompatibilityConverter)(object)new PRF_BackCompatibilityConverter());
			ReflectionUtility.BackCompatibilityConversionChain.SetValue(null, list);
			PRFDefOf.PRFDrone.race.mechEnabledWorkTypes.AddRange(DefDatabase<WorkTypeDef>.AllDefs);
		}

		public override void ExposeData()
		{
			((GameComponent)this).ExposeData();
			Scribe_Collections.Look<SpecialSculpture>(ref specialScupltures, "specialSculptures", (LookMode)0, Array.Empty<object>());
			Scribe_Deep.Look<Pawn>(ref PRF_StaticPawn, "PRF_StaticPawn", Array.Empty<object>());
			Scribe_Deep.Look<Job>(ref PRF_StaticJob, "PRF_StaticJob", Array.Empty<object>());
		}

		public void RegisterAssemblerQueue(IAssemblerQueue queue)
		{
			AssemblerQueue.Add(queue);
		}

		public void DeRegisterAssemblerQueue(IAssemblerQueue queue)
		{
			AssemblerQueue.RemoveAll((IAssemblerQueue q) => queue.ToString() == q.ToString());
		}

		public bool TryAddSpecialSculpture(Thing item, SpecialSculpture specialSculpture = null, bool verifyProvidedSculpture = true)
		{
			if (specialSculpture != null)
			{
				if (verifyProvidedSculpture)
				{
					List<ThingDef> limitToDefs = specialSculpture.limitToDefs;
					if (limitToDefs != null && !limitToDefs.Contains(item.def))
					{
						return false;
					}
				}
			}
			else
			{
				foreach (SpecialSculpture ss in GenCollection.InRandomOrder<SpecialSculpture>(ProjectRimFactory_ModComponent.availableSpecialSculptures.Where((SpecialSculpture s) => s.limitToDefs?.Contains(item.def) ?? true), (IList<SpecialSculpture>)null))
				{
					if (specialScupltures == null)
					{
						specialSculpture = ss;
						break;
					}
					SpecialSculpture specialSculpture2 = GenCollection.FirstOrDefault<SpecialSculpture>(specialScupltures, (Predicate<SpecialSculpture>)((SpecialSculpture s) => s.id == ss.id));
					if (specialSculpture2 == null)
					{
						specialSculpture = ss;
						break;
					}
					if (specialSculpture2.currentInstances == null || GenCollection.Count<Thing>(specialSculpture2.currentInstances, (Predicate<Thing>)((Thing a) => a != item)) < specialSculpture2.maxNumberCopies)
					{
						specialSculpture = ss;
						break;
					}
				}
				if (specialSculpture == null)
				{
					return false;
				}
			}
			specialSculpture.MakeItemSpecial(item);
			if (specialScupltures == null)
			{
				specialScupltures = new List<SpecialSculpture>();
			}
			SpecialSculpture specialSculpture3 = GenCollection.FirstOrDefault<SpecialSculpture>(specialScupltures, (Predicate<SpecialSculpture>)((SpecialSculpture s) => s.id == specialSculpture.id));
			if (specialSculpture3 == null)
			{
				specialScupltures.Add(specialSculpture);
				specialSculpture3 = specialSculpture;
			}
			if (specialSculpture3.currentInstances == null)
			{
				specialSculpture3.currentInstances = new List<Thing>();
			}
			if (!specialSculpture3.currentInstances.Contains(item))
			{
				specialSculpture3.currentInstances.Add(item);
			}
			return true;
		}

		public static bool TryMakeProductSpecialScupture(List<Thing> products, ModExtension_ModifyProduct modifyYieldExt, IBillGiver billGiver, Thing productMaker, RecipeDef recipeDef, Pawn worker, List<Thing> ingredients, Thing dominantIngredient)
		{
			foreach (Thing product in products)
			{
				if (Current.Game.GetComponent<PRFGameComponent>().TryAddSpecialSculpture(product))
				{
					return true;
				}
			}
			return false;
		}

		public override void LoadedGame()
		{
			((GameComponent)this).LoadedGame();
			UpdateThingDescriptions.Update();
			if (ProjectRimFactory_ModSettings.PRF_LiteMode)
			{
				PRF_CustomizeDefs.ToggleLiteMode();
			}
			TraderMinifyCheck();
		}

		public override void StartedNewGame()
		{
			((GameComponent)this).StartedNewGame();
			UpdateThingDescriptions.Update();
			if (ProjectRimFactory_ModSettings.PRF_LiteMode)
			{
				PRF_CustomizeDefs.ToggleLiteMode();
			}
			TraderMinifyCheck();
		}

		private void TraderMinifyCheck()
		{
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Expected O, but got Unknown
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Invalid comparison between Unknown and I4
			List<StockGenerator> stockGenerators = PRFDefOf.PRF_Factory_Supplier.stockGenerators;
			for (int num = stockGenerators.Count - 1; num >= 0; num--)
			{
				StockGenerator obj = stockGenerators[num];
				StockGenerator_SingleDef val = (StockGenerator_SingleDef)(object)((obj is StockGenerator_SingleDef) ? obj : null);
				if (val != null)
				{
					ThingDef val2 = (ThingDef)ReflectionUtility.StockGeneratorSingleDefThingDef.GetValue(val);
					if (!val2.Minifiable && (int)val2.category == 3)
					{
						stockGenerators.RemoveAt(num);
					}
				}
			}
		}
	}
	[StaticConstructorOnStartup]
	public static class RS
	{
		public static readonly Texture2D SplitterArrowUp;

		public static readonly Texture2D SplitterArrowRight;

		public static readonly Texture2D SplitterArrowLeft;

		public static readonly Texture2D SplitterArrowDown;

		public static readonly Texture2D SplitterDisabled;

		public static readonly Texture2D ForbidOn;

		public static readonly Texture2D ForbidOff;

		public static readonly Texture2D PregnantIcon;

		public static readonly Texture2D BondIcon;

		public static readonly Texture2D MaleIcon;

		public static readonly Texture2D FemaleIcon;

		public static readonly Texture2D SlaughterIcon;

		public static readonly Texture2D TrainedIcon;

		public static readonly Texture2D YoungIcon;

		public static readonly Texture2D AdultIcon;

		public static readonly Texture2D OutputDirectionIcon;

		public static readonly Texture2D PlayIcon;

		public static readonly Texture2D Arrow;

		static RS()
		{
			PregnantIcon = ContentFinder<Texture2D>.Get("UI/Icons/Animal/Pregnant", true);
			BondIcon = ContentFinder<Texture2D>.Get("UI/Icons/Animal/Bond", true);
			MaleIcon = GenderUtility.GetIcon((Gender)1);
			FemaleIcon = GenderUtility.GetIcon((Gender)2);
			SlaughterIcon = ContentFinder<Texture2D>.Get("UI/Icons/Animal/Slaughter", true);
			TrainedIcon = ContentFinder<Texture2D>.Get("UI/Icons/Trainables/Obedience", true);
			YoungIcon = ContentFinder<Texture2D>.Get("UI/Icons/LifeStage/Young", true);
			AdultIcon = ContentFinder<Texture2D>.Get("UI/Icons/LifeStage/Adult", true);
			ForbidOn = ContentFinder<Texture2D>.Get("UI/Designators/ForbidOn", true);
			ForbidOff = ContentFinder<Texture2D>.Get("UI/Designators/ForbidOff", true);
			OutputDirectionIcon = ContentFinder<Texture2D>.Get("PRFUi/OutputDirection", true);
			PlayIcon = ContentFinder<Texture2D>.Get("PRFUi/Play", true);
			SplitterDisabled = ContentFinder<Texture2D>.Get("PRFUi/ForbidIcon", true);
			SplitterArrowUp = ContentFinder<Texture2D>.Get("PRFUi/UpArrow", true);
			SplitterArrowRight = ContentFinder<Texture2D>.Get("PRFUi/RightArrow", true);
			SplitterArrowLeft = ContentFinder<Texture2D>.Get("PRFUi/LeftArrow", true);
			SplitterArrowDown = ContentFinder<Texture2D>.Get("PRFUi/DownArrow", true);
			Arrow = ContentFinder<Texture2D>.Get("UI/Overlays/Arrow", true);
			foreach (SpecialSculpture availableSpecialSculpture in ProjectRimFactory_ModComponent.availableSpecialSculptures)
			{
				availableSpecialSculpture.Init();
			}
		}
	}
	public class SpecialSculpture : IExposable
	{
		public string id;

		public int maxNumberCopies = 1;

		public List<ThingDef> limitToDefs;

		public GraphicData graphicData;

		public string titleKey;

		public string descKey;

		public string authorKey;

		public Graphic graphic;

		public List<Thing> currentInstances = null;

		public void ExposeData()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Invalid comparison between Unknown and I4
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Invalid comparison between Unknown and I4
			if ((int)Scribe.mode == 1)
			{
				bool flag = false;
				foreach (Thing currentInstance in currentInstances)
				{
					if (!currentInstance.Destroyed)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					return;
				}
			}
			Scribe_Values.Look<string>(ref id, "id", (string)null, true);
			Scribe_Values.Look<string>(ref titleKey, "title", (string)null, false);
			Scribe_Values.Look<string>(ref descKey, "desc", (string)null, false);
			Scribe_Values.Look<string>(ref authorKey, "author", (string)null, false);
			Scribe_Collections.Look<Thing>(ref currentInstances, "item", (LookMode)3, Array.Empty<object>());
			if ((int)Scribe.mode != 4)
			{
				return;
			}
			List<SpecialSculpture> availableSpecialSculptures = ProjectRimFactory_ModComponent.availableSpecialSculptures;
			SpecialSculpture specialSculpture = ((availableSpecialSculptures != null) ? GenCollection.FirstOrDefault<SpecialSculpture>(availableSpecialSculptures, (Predicate<SpecialSculpture>)((SpecialSculpture x) => x.id == id)) : null);
			if (specialSculpture != null)
			{
				graphicData = specialSculpture.graphicData;
				graphic = specialSculpture.graphic;
				maxNumberCopies = specialSculpture.maxNumberCopies;
			}
			foreach (Thing currentInstance2 in currentInstances)
			{
				MakeItemSpecial(currentInstance2);
			}
		}

		public void Init()
		{
			if (graphicData != null)
			{
				graphic = graphicData.Graphic;
			}
		}

		public void MakeItemSpecial(Thing art)
		{
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			CompArt val = ThingCompUtility.TryGetComp<CompArt>(art);
			if (val == null)
			{
				Log.Error("PRF could not make special sculpture from " + (object)art);
				return;
			}
			AccessTools.Field(typeof(CompArt), "titleInt").SetValue(val, Translator.Translate(titleKey));
			AccessTools.Field(typeof(CompArt), "authorNameInt").SetValue(val, Translator.Translate(authorKey));
			if (graphicData != null)
			{
				AccessTools.Field(typeof(Thing), "graphicInt").SetValue(((ThingComp)val).parent, graphicData.Graphic);
			}
		}

		public static void PreStartGame()
		{
			List<SpecialSculpture> availableSpecialSculptures = ProjectRimFactory_ModComponent.availableSpecialSculptures;
			if (availableSpecialSculptures == null)
			{
				return;
			}
			foreach (SpecialSculpture item in availableSpecialSculptures)
			{
				item.currentInstances = null;
			}
		}

		public static List<SpecialSculpture> LoadAvailableSpecialSculptures(ModContentPack content)
		{
			try
			{
				LoadableXmlAsset val = DirectXmlLoader.XmlAssetsInModFolder(content, "Settings", (List<string>)null)?.Where((LoadableXmlAsset x) => x.name == "SpecialSculpture.xml")?.FirstOrDefault();
				if (val == null || val.xmlDoc == null)
				{
					Log.Warning("PRF could not load special sculpture data");
					return null;
				}
				return DirectXmlToObject.ObjectFromXml<List<SpecialSculpture>>((XmlNode)val.xmlDoc.DocumentElement, false);
			}
			catch (Exception ex)
			{
				Log.Error("PRF was unable to extract Special Sculpture data from XML; \n  Exception: " + ex);
				return null;
			}
		}
	}
	[StaticConstructorOnStartup]
	public class PickUpAndHaulCompatiblity
	{
		static PickUpAndHaulCompatiblity()
		{
			if (!GenCollection.Any<ModContentPack>(LoadedModManager.RunningModsListForReading, (Predicate<ModContentPack>)((ModContentPack mcp) => mcp.PackageId == "Mehni.PickUpAndHaul")))
			{
				return;
			}
			AssemblyName assemblyName = new AssemblyName("PRF_PUAH_Compatiblity_Assembly");
			AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
			ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
			Type type = AppDomain.CurrentDomain.GetAssemblies().SelectMany((Assembly a) => a.GetTypes()).FirstOrDefault((Type t) => t.FullName == "IHoldMultipleThings.IHoldMultipleThings");
			if (type == null)
			{
				Log.Error("PRF detected Pick Up And Haul loaded, but could not find IHoldMultipleThings\nHauling is likely to fail.");
				return;
			}
			foreach (Type item in from t in AppDomain.CurrentDomain.GetAssemblies().SelectMany((Assembly a) => a.GetExportedTypes())
				where typeof(Building_MassStorageUnit).IsAssignableFrom(t)
				where !t.IsAbstract
				select t)
			{
				TypeBuilder typeBuilder = moduleBuilder.DefineType("PRF_PUAH_" + item.Name, TypeAttributes.Public, item, new Type[1] { type });
				typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
				Type thingClass = typeBuilder.CreateType();
				foreach (ThingDef item2 in DefDatabase<ThingDef>.AllDefsListForReading)
				{
					if (item2.thingClass == item)
					{
						item2.thingClass = thingClass;
					}
				}
			}
		}
	}
}
namespace ProjectRimFactory.Storage
{
	public class Building_AdvancedStorageUnitIOPort : Building_StorageUnitIOBase
	{
		private readonly List<Thing> placementQueue = new List<Thing>();

		protected override bool ShowLimitGizmo => false;

		public override StorageIOMode IOMode
		{
			get
			{
				return StorageIOMode.Output;
			}
			set
			{
			}
		}

		public override bool ForbidPawnInput => true;

		public bool CanGetNewItem
		{
			get
			{
				int result;
				if (GetStoredItem() == null)
				{
					CompPowerTrader powerTrader = PowerTrader;
					result = ((powerTrader != null && powerTrader.PowerOn) ? 1 : 0);
				}
				else
				{
					result = 0;
				}
				return (byte)result != 0;
			}
		}

		protected override bool IsAdvancedPort => true;

		public void AddItemToQueue(Thing thing)
		{
			placementQueue.Add(thing);
		}

		public override void DeSpawn(DestroyMode destroyMode = (DestroyMode)0)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			((Thing)this).Map.GetComponent<PRFMapComponent>().DeRegisteradvancedIOLocations(((Thing)this).Position);
			base.DeSpawn(destroyMode);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			base.SpawnSetup(map, respawningAfterLoad);
			map.GetComponent<PRFMapComponent>().RegisteradvancedIOLocations(((Thing)this).Position, this);
		}

		private Thing GetStoredItem()
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			Map map = ((Thing)this).Map;
			if (map == null)
			{
				Log.Error($"PRF GetStoredItem @{((Thing)this).Position} map is null");
				return null;
			}
			return GridsUtility.GetFirstItem(WorkPosition, map);
		}

		public void UpdateQueue()
		{
			if (CanGetNewItem && placementQueue.Count > 0)
			{
				Thing thing = placementQueue[0];
				PlaceThingNow(thing);
				placementQueue.RemoveAt(0);
			}
		}

		public void PlaceThingNow(Thing thing)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			if (thing != null)
			{
				thing.Position = ((Thing)this).Position;
			}
		}

		protected override void Tick()
		{
			if (!((Thing)this).Spawned)
			{
				return;
			}
			UpdateQueue();
			if (Gen.IsHashIntervalTick((Thing)(object)this, 10))
			{
				Thing storedItem = GetStoredItem();
				if (storedItem != null && !((Thing)this).Map.reservationManager.AllReservedThings().Contains(storedItem))
				{
					RefreshInput();
				}
			}
		}
	}
	[StaticConstructorOnStartup]
	public abstract class Building_ColdStorage : Building, IRenameable, IHaulDestination, IStoreSettingsParent, ILinkableStorageParent, IThingHolder
	{
		private static readonly Texture2D RenameTex = ContentFinder<Texture2D>.Get("UI/Buttons/Rename", true);

		protected ThingOwner<Thing> thingOwner;

		private List<Building_StorageUnitIOBase> ports = new List<Building_StorageUnitIOBase>();

		private string uniqueName;

		private StorageSettings settings;

		protected DefModExtension_Crate ModExtensionCrate;

		private StorageOutputUtil outputUtil;

		private List<Thing> Items => thingOwner.InnerListForReading;

		public string RenamableLabel
		{
			get
			{
				return uniqueName ?? ((Thing)this).LabelCapNoCount;
			}
			set
			{
				uniqueName = value;
			}
		}

		public string BaseLabel => ((Thing)this).LabelCapNoCount;

		public string InspectLabel => ((Entity)this).LabelCap;

		protected abstract bool CanStoreMoreItems { get; }

		protected int MaxNumberItemsInternal
		{
			get
			{
				//IL_001c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0021: Unknown result type (might be due to invalid IL or missing references)
				int num = ModExtensionCrate?.limit ?? int.MaxValue;
				IntVec2 size = ((BuildableDef)((Thing)this).def).Size;
				return num - ((IntVec2)(ref size)).Area + 1;
			}
		}

		public List<Thing> StoredItems => Items;

		public int StoredItemsCount => Items.Count;

		public virtual bool CanReceiveIO => true;

		public virtual bool Powered => true;

		public bool ForbidPawnAccess => ModExtensionCrate?.forbidPawnAccess ?? false;

		IntVec3 IHaulDestination.Position => ((Thing)this).Position;

		Map IHaulDestination.Map => ((Thing)this).Map;

		public bool HaulDestinationEnabled => true;

		bool IStoreSettingsParent.StorageTabVisible => true;

		public bool AdvancedIOAllowed => false;

		public IntVec3 GetPosition => ((Thing)this).Position;

		public StorageSettings GetSettings => settings;

		public LocalTargetInfo GetTargetInfo => LocalTargetInfo.op_Implicit((Thing)(object)this);

		public bool CanUseIOPort => true;

		public void DeregisterPort(Building_StorageUnitIOBase port)
		{
			ports.Remove(port);
		}

		public void RegisterPort(Building_StorageUnitIOBase port)
		{
			ports.Add(port);
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			foreach (Gizmo item2 in StorageSettingsClipboard.CopyPasteGizmosFor(settings))
			{
				yield return item2;
			}
			yield return (Gizmo)new Command_Action
			{
				icon = (Texture)(object)RenameTex,
				action = delegate
				{
					Find.WindowStack.Add((Window)(object)new Dialog_RenameColdStorage(this));
				},
				hotKey = KeyBindingDefOf.Misc1,
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRFRenameMassStorageUnitLabel")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRFRenameMassStorageUnitDesc"))
			};
		}

		protected virtual string GetUIThingLabel()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFMassStorageUIThingLabel", NamedArgument.op_Implicit(StoredItemsCount)));
		}

		public virtual string GetITabString(int itemsSelected)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFItemsTabLabel", NamedArgument.op_Implicit(StoredItemsCount), NamedArgument.op_Implicit(itemsSelected)));
		}

		private void RegisterNewItem(Thing newItem)
		{
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Invalid comparison between Unknown and I4
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			if (Items.Contains(newItem))
			{
				Log.Message($"dup: {newItem}");
				return;
			}
			Thing[] array = Items.ToArray();
			foreach (Thing val in array)
			{
				if ((int)val.def.category == 2)
				{
					val.TryAbsorbStack(newItem, true);
				}
				if (newItem.Destroyed)
				{
					break;
				}
			}
			if (!newItem.Destroyed)
			{
				ThingOwner holdingOwner = newItem.holdingOwner;
				if (holdingOwner != null)
				{
					holdingOwner.Remove(newItem);
				}
				((ThingOwner)thingOwner).TryAdd(newItem, false);
				if (CanStoreMoreItems)
				{
					newItem.Position = ((Thing)this).Position;
				}
				if (newItem.Spawned)
				{
					((Entity)newItem).DeSpawn((DestroyMode)0);
				}
			}
		}

		public override void ExposeData()
		{
			((Building)this).ExposeData();
			Scribe_Collections.Look<Building_StorageUnitIOBase>(ref ports, "ports", (LookMode)3, Array.Empty<object>());
			Scribe_Deep.Look<ThingOwner<Thing>>(ref thingOwner, "thingowner", new object[1] { this });
			Scribe_Values.Look<string>(ref uniqueName, "uniqueName", (string)null, false);
			Scribe_Deep.Look<StorageSettings>(ref settings, "settings", new object[1] { this });
			if (ModExtensionCrate == null)
			{
				ModExtensionCrate = ((Def)((Thing)this).def).GetModExtension<DefModExtension_Crate>();
			}
		}

		public override string GetInspectString()
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			string inspectString = ((ThingWithComps)this).GetInspectString();
			StringBuilder stringBuilder = new StringBuilder();
			if (!string.IsNullOrEmpty(inspectString))
			{
				stringBuilder.AppendLine(inspectString);
			}
			stringBuilder.Append(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF_TotalStacksNum", NamedArgument.op_Implicit(Items.Count))));
			return stringBuilder.ToString();
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Invalid comparison between Unknown and I4
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			if (!ProjectRimFactory_ModComponent.ModSupport_SOS2_MoveShip && !GravshipUtility.generatingGravship)
			{
				List<Thing> items = Items;
				for (int i = 0; i < items.Count; i++)
				{
					if ((int)items[i].def.category == 2)
					{
						GenPlace.TryPlaceThing(items[i], ((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					}
				}
				PatchStorageUtil.GetPRFMapComponent(((Thing)this).Map).DeRegisterColdStorageBuilding(this);
			}
			((Building)this).DeSpawn(mode);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			PatchStorageUtil.GetPRFMapComponent(((Thing)this).Map).RegisterColdStorageBuilding(this);
			if (ModExtensionCrate == null)
			{
				ModExtensionCrate = ((Def)((Thing)this).def).GetModExtension<DefModExtension_Crate>();
			}
			outputUtil = new StorageOutputUtil((Building)(object)this);
			foreach (Building_StorageUnitIOBase port in ports)
			{
				if (port != null && ((Thing)port).Spawned && ((Thing)port).Map != map)
				{
					port.BoundStorageUnit = null;
				}
			}
		}

		public float GetItemWealth()
		{
			float num = 0f;
			int count = Items.Count;
			for (int i = 0; i < count; i++)
			{
				Thing val = Items[i];
				num += val.MarketValue * (float)val.stackCount;
			}
			return num;
		}

		public override void DrawGUIOverlay()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Invalid comparison between Unknown and I4
			((ThingWithComps)this).DrawGUIOverlay();
			if ((int)Current.CameraDriver.CurrentZoom <= 1)
			{
				GenMapUI.DrawThingLabel((Thing)(object)this, RenamableLabel + "\n\r" + GetUIThingLabel());
			}
		}

		public bool OutputItem(Thing item)
		{
			return outputUtil.OutputItem(item);
		}

		public bool CapacityAt(Thing thing, IntVec3 cell, Map map, out int capacity)
		{
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_0147: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_0165: Unknown result type (might be due to invalid IL or missing references)
			capacity = 0;
			if (thing == null || map == null || map != ((Thing)this).Map || !((Thing)this).Spawned)
			{
				Log.Error("PRF DSU CapacityAt Sanity Check Error");
				return false;
			}
			thing = MinifyUtility.GetInnerIfMinified(thing);
			if (!Accepts(thing))
			{
				return false;
			}
			IEnumerable<Thing> source = from t in GridsUtility.GetThingList(((Thing)this).Position, ((Thing)this).Map)
				where (int)t.def.category == 2
				select t;
			int maxstacksize = thing.def.stackLimit;
			foreach (Thing item in source.Where((Thing t) => t.def == thing.def && t.stackCount < maxstacksize))
			{
				capacity += maxstacksize - item.stackCount;
			}
			capacity += (MaxNumberItemsInternal - source.Count()) * maxstacksize;
			if (cell != ((Thing)this).Position)
			{
				Thing val = ((Thing)this).Map.thingGrid.ThingAt(cell, (ThingCategory)2);
				if (val != null)
				{
					if (val.def == thing.def)
					{
						capacity += thing.def.stackLimit - val.stackCount;
					}
				}
				else
				{
					capacity += thing.def.stackLimit;
				}
			}
			return capacity > 0;
		}

		public bool StackableAt(Thing thing, IntVec3 cell, Map map)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			int capacity;
			return CapacityAt(thing, cell, map, out capacity);
		}

		public bool Accepts(Thing t)
		{
			return settings.AllowedToAccept(t);
		}

		StorageSettings IStoreSettingsParent.GetStoreSettings()
		{
			return settings;
		}

		StorageSettings IStoreSettingsParent.GetParentStoreSettings()
		{
			StorageSettings fixedStorageSettings = ((Thing)this).def.building.fixedStorageSettings;
			if (fixedStorageSettings != null)
			{
				return fixedStorageSettings;
			}
			return StorageSettings.EverStorableFixedSettings();
		}

		public override void PostMake()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Expected O, but got Unknown
			((ThingWithComps)this).PostMake();
			settings = new StorageSettings((IStoreSettingsParent)(object)this);
			if (((Thing)this).def.building.defaultStorageSettings != null)
			{
				settings.CopyFrom(((Thing)this).def.building.defaultStorageSettings);
			}
		}

		public void HandleNewItem(Thing item)
		{
			RegisterNewItem(item);
		}

		public void HandleMoveItem(Thing item)
		{
			if (Items.Contains(item))
			{
				Items.Remove(item);
			}
		}

		public bool CanReceiveThing(Thing item)
		{
			return settings.AllowedToAccept(item) && CanReceiveIO && CanStoreMoreItems;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return (ThingOwner)(object)thingOwner;
		}

		public bool HoldsPos(IntVec3 pos)
		{
			return false;
		}

		public void Notify_SettingsChanged()
		{
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
	[StaticConstructorOnStartup]
	public class Building_ColdStoragePowered : Building_ColdStorage
	{
		private static readonly Texture2D StoragePawnAccessSwitchIcon = ContentFinder<Texture2D>.Get("PRFUi/dsu", true);

		private CompPowerTrader compPowerTrader;

		private bool pawnAccess = true;

		public override bool Powered
		{
			get
			{
				CompPowerTrader obj = compPowerTrader;
				return obj != null && obj.PowerOn;
			}
		}

		protected override bool CanStoreMoreItems => Powered && ((Thing)this).Spawned && (ModExtensionCrate == null || base.StoredItemsCount < base.MaxNumberItemsInternal);

		public override bool CanReceiveIO
		{
			get
			{
				int result;
				if (base.CanReceiveIO)
				{
					CompPowerTrader obj = compPowerTrader;
					if (obj != null && obj.PowerOn)
					{
						result = (((Thing)this).Spawned ? 1 : 0);
						goto IL_0025;
					}
				}
				result = 0;
				goto IL_0025;
				IL_0025:
				return (byte)result != 0;
			}
		}

		private void UpdatePowerConsumption()
		{
			if (compPowerTrader == null)
			{
				compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			}
			compPowerTrader.PowerOutput = -10 * base.StoredItemsCount;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look<bool>(ref pawnAccess, "pawnAccess", true, false);
			if (compPowerTrader == null)
			{
				compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			}
		}

		protected override void Tick()
		{
			((ThingWithComps)this).Tick();
			if (((Thing)this).Spawned)
			{
				if (Gen.IsHashIntervalTick((Thing)(object)this, 60))
				{
					UpdatePowerConsumption();
				}
				((ThingOwner)thingOwner).DoTick();
			}
		}

		public override void PostMapInit()
		{
			((ThingWithComps)this).PostMapInit();
			if (compPowerTrader == null)
			{
				compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			if (Prefs.DevMode)
			{
				yield return (Gizmo)new Command_Action
				{
					defaultLabel = "DEBUG: Debug actions",
					action = delegate
					{
						//IL_0011: Unknown result type (might be due to invalid IL or missing references)
						//IL_001b: Expected O, but got Unknown
						Find.WindowStack.Add((Window)new FloatMenu(new List<FloatMenuOption>(DebugActions())));
					}
				};
			}
			if (!base.ForbidPawnAccess)
			{
				yield return (Gizmo)new Command_Toggle
				{
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRFPawnAccessLabel")),
					isActive = () => pawnAccess,
					toggleAction = delegate
					{
						pawnAccess = !pawnAccess;
					},
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRFPawnAccessDesc")),
					icon = (Texture)(object)StoragePawnAccessSwitchIcon
				};
			}
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Invalid comparison between Unknown and I4
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			if ((int)mode == 4)
			{
				DefModExtension_Crate modExtension = ((Def)((Thing)this).def).GetModExtension<DefModExtension_Crate>();
				if (modExtension != null && modExtension.destroyContainsItems)
				{
					base.StoredItems.Where((Thing t) => !t.Destroyed).ToList().ForEach(delegate(Thing x)
					{
						x.Destroy((DestroyMode)0);
					});
				}
			}
			base.DeSpawn(mode);
		}

		protected virtual IEnumerable<FloatMenuOption> DebugActions()
		{
			yield return new FloatMenuOption("Update power consumption", (Action)UpdatePowerConsumption, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
			yield return new FloatMenuOption("Log item count", (Action)delegate
			{
				Log.Message(base.StoredItemsCount.ToString());
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
		}

		protected override string GetUIThingLabel()
		{
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			if ((((Def)((Thing)this).def).GetModExtension<DefModExtension_Crate>()?.limit).HasValue)
			{
				return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFCrateUIThingLabel", NamedArgument.op_Implicit(base.StoredItemsCount), NamedArgument.op_Implicit(((Def)((Thing)this).def).GetModExtension<DefModExtension_Crate>().limit)));
			}
			return base.GetUIThingLabel();
		}

		public override string GetITabString(int itemsSelected)
		{
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			if ((((Def)((Thing)this).def).GetModExtension<DefModExtension_Crate>()?.limit).HasValue)
			{
				return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFItemsTabLabel_Crate", NamedArgument.op_Implicit(base.StoredItemsCount), NamedArgument.op_Implicit(((Def)((Thing)this).def).GetModExtension<DefModExtension_Crate>().limit), NamedArgument.op_Implicit(itemsSelected)));
			}
			return base.GetITabString(itemsSelected);
		}

		public override void PostMake()
		{
			base.PostMake();
			if (thingOwner == null)
			{
				thingOwner = new ThingOwner<Thing>((IThingHolder)(object)this);
			}
		}
	}
	public class Building_Crate : Building_MassStorageUnit
	{
		protected override bool CanStoreMoreItems => GenCollection.Count<Thing>(GridsUtility.GetThingList(((Thing)this).Position, ((Thing)this).Map), (Predicate<Thing>)((Thing t) => (int)t.def.category == 2)) < MaxNumberItemsInternal;

		public override string GetITabString(int itemsSelected)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFItemsTabLabel_Crate", NamedArgument.op_Implicit(base.StoredItemsCount), NamedArgument.op_Implicit(ModExtensionCrate.limit), NamedArgument.op_Implicit(itemsSelected)));
		}

		protected override string GetUIThingLabel()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFCrateUIThingLabel", NamedArgument.op_Implicit(base.StoredItemsCount), NamedArgument.op_Implicit(ModExtensionCrate.limit)));
		}
	}
	public class Building_IOPusher : Building_StorageUnitIOBase
	{
		protected override IntVec3 WorkPosition
		{
			get
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_000f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				IntVec3 position = ((Thing)this).Position;
				Rot4 rotation = ((Thing)this).Rotation;
				return position + ((Rot4)(ref rotation)).FacingCell;
			}
		}

		public override StorageIOMode IOMode
		{
			get
			{
				return StorageIOMode.Output;
			}
			set
			{
			}
		}

		protected override bool IsAdvancedPort => false;

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			Mode = IOMode;
		}
	}
	internal class PlaceWorker_IOPusherHilight : PlaceWorker
	{
		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 item = center + ((Rot4)(ref rot)).FacingCell;
			GenDraw.DrawFieldEdges(new List<IntVec3>(1) { item }, CommonColors.OutputCell, (float?)null, (HashSet<IntVec3>)null, 2900);
		}
	}
	internal class Building_ItemSlide : Building_Crate, IPRF_Building
	{
		public IEnumerable<Thing> AvailableThings
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public virtual bool ForbidOnPlacingDefault { get; set; }

		public bool ObeysStorageFilters => false;

		public bool OutputToEntireStockpile => false;

		private IntVec3 OutputCell
		{
			get
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_000f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				IntVec3 position = ((Thing)this).Position;
				Rot4 rotation = ((Thing)this).Rotation;
				return position + ((Rot4)(ref rotation)).FacingCell;
			}
		}

		public bool AcceptsThing(Thing newThing, IPRF_Building giver = null)
		{
			if (!((Building_Storage)this).Accepts(newThing) || !CanStoreMoreItems)
			{
				return false;
			}
			((Building_Storage)this).Notify_ReceivedThing(newThing);
			return true;
		}

		public void EffectOnAcceptThing(Thing t)
		{
		}

		public void EffectOnPlaceThing(Thing t)
		{
		}

		public bool ForbidOnPlacing(Thing t)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			if (GenCollection.Any<Thing>(GridsUtility.GetThingList(OutputCell, ((Thing)this).Map), (Predicate<Thing>)((Thing type) => (type is IPRF_Building || type is Building_StorageUnitIOBase || type is Building_MassStorageUnit) ? true : false)))
			{
				return ForbidOnPlacingDefault;
			}
			return true;
		}

		public Thing GetThingBy(Func<Thing, bool> optionalValidator = null)
		{
			throw new NotImplementedException();
		}

		private void TrySlideItem()
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			IPRF_Building iPRF_Building = (IPRF_Building)GenCollection.FirstOrDefault<Thing>(GridsUtility.GetThingList(OutputCell, ((Thing)this).Map), (Predicate<Thing>)((Thing type) => type is IPRF_Building));
			if (iPRF_Building != null)
			{
				iPRF_Building.AcceptsThing(base.StoredItems[0], this);
			}
			else
			{
				this.PRFTryPlaceThing(base.StoredItems[0], OutputCell, ((Thing)this).Map);
			}
		}

		protected override void Tick()
		{
			((ThingWithComps)this).Tick();
			if (((Thing)this).Spawned && base.StoredItemsCount > 0)
			{
				TrySlideItem();
			}
		}
	}
	internal class PlaceWorker_ItemSlide : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			AcceptanceReport result = ((PlaceWorker)this).AllowsPlacing(checkingDef, loc, rot, map, thingToIgnore, thing);
			if (!((AcceptanceReport)(ref result)).Accepted)
			{
				return result;
			}
			IntVec3 val = loc + ((Rot4)(ref rot)).FacingCell;
			List<Thing> list = map.thingGrid.ThingsListAt(val);
			if (GenCollection.Any<Thing>(list, (Predicate<Thing>)((Thing t) => t is Building_ItemSlide || t.def.entityDefToBuild == checkingDef)))
			{
				return new AcceptanceReport(TaggedString.op_Implicit(Translator.Translate("PRF_PlaceWorker_ItemSlide_Denied")));
			}
			if (GenAdj.CellsAdjacentCardinal(loc, rot, checkingDef.Size).Any((IntVec3 c) => GenCollection.Any<Thing>(map.thingGrid.ThingsListAt(c), (Predicate<Thing>)delegate(Thing t)
			{
				//IL_001c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				//IL_002a: Unknown result type (might be due to invalid IL or missing references)
				//IL_002f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0035: Unknown result type (might be due to invalid IL or missing references)
				int result2;
				if (t is Building_ItemSlide || t.def.entityDefToBuild == checkingDef)
				{
					IntVec3 position = t.Position;
					Rot4 rotation = t.Rotation;
					result2 = ((position + ((Rot4)(ref rotation)).FacingCell == loc) ? 1 : 0);
				}
				else
				{
					result2 = 0;
				}
				return (byte)result2 != 0;
			})))
			{
				return new AcceptanceReport(TaggedString.op_Implicit(Translator.Translate("PRF_PlaceWorker_ItemSlide_Denied")));
			}
			return result;
		}

		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 item = center + ((Rot4)(ref rot)).FacingCell;
			GenDraw.DrawFieldEdges(new List<IntVec3>(1) { item }, CommonColors.OutputCell, (float?)null, (HashSet<IntVec3>)null, 2900);
		}
	}
	[StaticConstructorOnStartup]
	public abstract class Building_MassStorageUnit : Building_Storage, IHideItem, IHideRightClickMenu, IForbidPawnOutputItem, IForbidPawnInputItem, ISlotGroupParent, IStoreSettingsParent, IHaulDestination, IRenameable, ILinkableStorageParent, ILimitWatcher
	{
		private static readonly Texture2D RenameTex = ContentFinder<Texture2D>.Get("UI/Buttons/Rename", true);

		private List<Building_StorageUnitIOBase> ports = new List<Building_StorageUnitIOBase>();

		public DefModExtension_Crate ModExtensionCrate;

		private StorageOutputUtil outputUtil;

		private string uniqueName;

		private readonly Dictionary<ThingDef, int> itemCounts = new Dictionary<ThingDef, int>();

		public IntVec3 GetPosition => ((Thing)this).Position;

		public StorageSettings GetSettings => base.settings;

		public bool CanUseIOPort => ((Def)((Thing)this).def).GetModExtension<DefModExtension_CanUseStorageIOPorts>() != null;

		public LocalTargetInfo GetTargetInfo => LocalTargetInfo.op_Implicit((Thing)(object)this);

		protected abstract bool CanStoreMoreItems { get; }

		protected virtual int MaxNumberItemsInternal
		{
			get
			{
				//IL_001c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0021: Unknown result type (might be due to invalid IL or missing references)
				int num = ModExtensionCrate?.limit ?? int.MaxValue;
				IntVec2 size = ((BuildableDef)((Thing)this).def).Size;
				return num - ((IntVec2)(ref size)).Area + 1;
			}
		}

		public List<Thing> StoredItems { get; } = new List<Thing>();

		public int StoredItemsCount => StoredItems.Count;

		public virtual bool CanReceiveIO => true;

		public virtual bool Powered => true;

		protected bool ForbidPawnAccess => ModExtensionCrate?.forbidPawnAccess ?? false;

		public virtual bool ForbidPawnInput => ForbidPawnAccess;

		public string RenamableLabel
		{
			get
			{
				return uniqueName ?? ((Thing)this).LabelCapNoCount;
			}
			set
			{
				uniqueName = value;
			}
		}

		public string BaseLabel => ((Thing)this).LabelCapNoCount;

		public string InspectLabel => ((Entity)this).LabelCap;

		public virtual bool ForbidPawnOutput => ForbidPawnAccess;

		public virtual bool HideItems => ModExtensionCrate?.hideItems ?? false;

		public virtual bool HideRightClickMenus => ModExtensionCrate?.hideRightClickMenus ?? false;

		public bool AdvancedIOAllowed => true;

		public override void Notify_ReceivedThing(Thing newItem)
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			((Building_Storage)this).Notify_ReceivedThing(newItem);
			if (newItem.Position != ((Thing)this).Position && !GravshipPlacementUtility.placingGravship)
			{
				HandleNewItem(newItem);
			}
			RefreshStorage();
		}

		public override void Notify_LostThing(Thing newItem)
		{
			((Building_Storage)this).Notify_LostThing(newItem);
			StoredItems.Remove(newItem);
			ItemCountsRemoved(newItem.def, newItem.stackCount);
			RefreshStorage();
		}

		public void DeregisterPort(Building_StorageUnitIOBase port)
		{
			ports.Remove(port);
		}

		public void RegisterPort(Building_StorageUnitIOBase port)
		{
			ports.Add(port);
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			yield return (Gizmo)new Command_Action
			{
				icon = (Texture)(object)RenameTex,
				action = delegate
				{
					Find.WindowStack.Add((Window)(object)new Dialog_RenameMassStorageUnit(this));
				},
				hotKey = KeyBindingDefOf.Misc1,
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRFRenameMassStorageUnitLabel")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRFRenameMassStorageUnitDesc"))
			};
			yield return (Gizmo)new Command_Action
			{
				icon = (Texture)(object)TexUI.RotRightTex,
				action = delegate
				{
					//IL_000e: Unknown result type (might be due to invalid IL or missing references)
					RefreshStorage(fullRefresh: true);
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("PRFReorganize_Message")), MessageTypeDefOf.NeutralEvent, true);
				},
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRFReorganize")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRFReorganize_Desc"))
			};
		}

		protected virtual string GetUIThingLabel()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFMassStorageUIThingLabel", NamedArgument.op_Implicit(StoredItemsCount)));
		}

		public virtual string GetITabString(int itemsSelected)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFItemsTabLabel", NamedArgument.op_Implicit(StoredItemsCount), NamedArgument.op_Implicit(itemsSelected)));
		}

		private void RegisterNewItem(Thing newItem)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Invalid comparison between Unknown and I4
			//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
			ItemCountsAdded(newItem.def, newItem.stackCount);
			List<Thing> thingList = GridsUtility.GetThingList(((Thing)this).Position, ((Thing)this).Map);
			for (int i = 0; i < thingList.Count; i++)
			{
				Thing val = thingList[i];
				if (val != newItem)
				{
					if ((int)val.def.category == 2 && val.CanStackWith(newItem))
					{
						val.TryAbsorbStack(newItem, true);
					}
					if (newItem.Destroyed)
					{
						return;
					}
				}
			}
			if (!newItem.Destroyed)
			{
				if (!StoredItems.Contains(newItem))
				{
					StoredItems.Add(newItem);
				}
				if (CanStoreMoreItems)
				{
					newItem.Position = ((Thing)this).Position;
				}
				if (!newItem.Spawned)
				{
					((Entity)newItem).SpawnSetup(((Thing)this).Map, false);
				}
			}
		}

		public override void ExposeData()
		{
			((Building_Storage)this).ExposeData();
			Scribe_Collections.Look<Building_StorageUnitIOBase>(ref ports, "ports", (LookMode)3, Array.Empty<object>());
			Scribe_Values.Look<string>(ref uniqueName, "uniqueName", (string)null, false);
			if (ModExtensionCrate == null)
			{
				ModExtensionCrate = ((Def)((Thing)this).def).GetModExtension<DefModExtension_Crate>();
			}
		}

		public override string GetInspectString()
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			string inspectString = ((Building_Storage)this).GetInspectString();
			StringBuilder stringBuilder = new StringBuilder();
			if (!string.IsNullOrEmpty(inspectString))
			{
				stringBuilder.AppendLine(inspectString);
			}
			stringBuilder.Append(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF_TotalStacksNum", NamedArgument.op_Implicit(StoredItems.Count))));
			return stringBuilder.ToString();
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Invalid comparison between Unknown and I4
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
			List<Thing> list = new List<Thing>(GridsUtility.GetThingList(((Thing)this).Position, ((Thing)this).Map));
			for (int i = 0; i < list.Count; i++)
			{
				if ((int)list[i].def.category == 2)
				{
					((Entity)list[i]).DeSpawn((DestroyMode)0);
					GenPlace.TryPlaceThing(list[i], ((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
				}
			}
			((Thing)this).Map.GetComponent<PRFMapComponent>().RemoveIHideRightClickMenu((Building)(object)this);
			CellRect val = GenAdj.OccupiedRect((Thing)(object)this);
			foreach (IntVec3 cell in ((CellRect)(ref val)).Cells)
			{
				((Thing)this).Map.GetComponent<PRFMapComponent>().DeRegisterIHideItemPos(cell, this);
				((Thing)this).Map.GetComponent<PRFMapComponent>().DeRegisterIForbidPawnOutputItem(cell, this);
			}
			((Building_Storage)this).DeSpawn(mode);
			ConditionalPatchHelper.Deregister(this);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			ConditionalPatchHelper.Register(this);
			((Building_Storage)this).SpawnSetup(map, respawningAfterLoad);
			((Thing)this).Map.GetComponent<PRFMapComponent>().AddIHideRightClickMenu((Building)(object)this);
			outputUtil = new StorageOutputUtil((Building)(object)this);
			CellRect val = GenAdj.OccupiedRect((Thing)(object)this);
			foreach (IntVec3 cell in ((CellRect)(ref val)).Cells)
			{
				map.GetComponent<PRFMapComponent>().RegisterIHideItemPos(cell, this);
				map.GetComponent<PRFMapComponent>().RegisterIForbidPawnOutputItem(cell, this);
			}
			if (ModExtensionCrate == null)
			{
				ModExtensionCrate = ((Def)((Thing)this).def).GetModExtension<DefModExtension_Crate>();
			}
			RefreshStorage();
			foreach (Building_StorageUnitIOBase item in ports.ToList())
			{
				if (item != null && ((Thing)item).Spawned && ((Thing)item).Map != map)
				{
					item.BoundStorageUnit = null;
				}
			}
			if (GravshipPlacementUtility.placingGravship)
			{
				ports.RemoveAll((Building_StorageUnitIOBase port) => port == null || ((Thing)port).Destroyed);
			}
			((Thing)this).def.building.groupingLabel = ((Thing)this).LabelCapNoCount;
		}

		public override void DrawGUIOverlay()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Invalid comparison between Unknown and I4
			((ThingWithComps)this).DrawGUIOverlay();
			if ((int)Current.CameraDriver.CurrentZoom <= 1)
			{
				GenMapUI.DrawThingLabel((Thing)(object)this, RenamableLabel + "\n\r" + GetUIThingLabel());
			}
		}

		public bool OutputItem(Thing item)
		{
			return outputUtil.OutputItem(item);
		}

		public void RefreshStorage(bool fullRefresh = false)
		{
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b9: Invalid comparison between Unknown and I4
			//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			if (fullRefresh)
			{
				StoredItems.Clear();
			}
			if (StoredItems.Count > 0)
			{
				return;
			}
			StoredItems.Clear();
			itemCounts.Clear();
			IntVec3 position = ((Thing)this).Position;
			Map map = ((Thing)this).Map;
			if (!((Thing)this).Spawned)
			{
				return;
			}
			foreach (IntVec3 item in ((Building_Storage)this).AllSlotCells())
			{
				List<Thing> list = new List<Thing>(GridsUtility.GetThingList(item, map));
				int count = list.Count;
				for (int i = 0; i < count; i++)
				{
					Thing val = list[i];
					if ((int)val.def.category == 2)
					{
						if (item != position && !GravshipPlacementUtility.placingGravship)
						{
							HandleNewItem(val);
						}
						else if (!StoredItems.Contains(val))
						{
							StoredItems.Add(val);
							ItemCountsAdded(val.def, val.stackCount);
							DeregisterDrawItem(val);
						}
					}
				}
			}
		}

		public bool CapacityAt(Thing thing, IntVec3 cell, Map map, out int capacity)
		{
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_0147: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_0165: Unknown result type (might be due to invalid IL or missing references)
			capacity = 0;
			if (thing == null || map == null || map != ((Thing)this).Map || !((Thing)this).Spawned)
			{
				Log.Error("PRF DSU CapacityAt Sanity Check Error");
				return false;
			}
			thing = MinifyUtility.GetInnerIfMinified(thing);
			if (!((Building_Storage)this).Accepts(thing))
			{
				return false;
			}
			IEnumerable<Thing> source = from t in GridsUtility.GetThingList(((Thing)this).Position, ((Thing)this).Map)
				where (int)t.def.category == 2
				select t;
			int maxstacksize = thing.def.stackLimit;
			foreach (Thing item in source.Where((Thing t) => t.def == thing.def && t.stackCount < maxstacksize))
			{
				capacity += maxstacksize - item.stackCount;
			}
			capacity += (MaxNumberItemsInternal - source.Count()) * maxstacksize;
			if (cell != ((Thing)this).Position)
			{
				Thing val = ((Thing)this).Map.thingGrid.ThingAt(cell, (ThingCategory)2);
				if (val != null)
				{
					if (val.def == thing.def)
					{
						capacity += thing.def.stackLimit - val.stackCount;
					}
				}
				else
				{
					capacity += thing.def.stackLimit;
				}
			}
			return capacity > 0;
		}

		public bool StackableAt(Thing thing, IntVec3 cell, Map map)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			int capacity;
			return CapacityAt(thing, cell, map, out capacity);
		}

		public void HandleNewItem(Thing item)
		{
			RegisterNewItem(item);
			DeregisterDrawItem(item);
		}

		private void DeregisterDrawItem(Thing item)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Invalid comparison between Unknown and I4
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Invalid comparison between Unknown and I4
			DrawerType drawerType = item.def.drawerType;
			if (((int)drawerType == 1 || (int)drawerType == 3) ? true : false)
			{
				((Thing)this).Map.dynamicDrawManager.DeRegisterDrawable(item);
			}
		}

		public void HandleMoveItem(Thing item)
		{
		}

		public bool CanReceiveThing(Thing item)
		{
			return base.settings.AllowedToAccept(item) && CanReceiveIO && CanStoreMoreItems;
		}

		public bool HoldsPos(IntVec3 pos)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			return ((Building_Storage)this).AllSlotCells()?.Contains(pos) ?? false;
		}

		private void ItemCountsRemoved(ThingDef removedDef, int cnt)
		{
			if (itemCounts.TryGetValue(removedDef, out var value))
			{
				if (cnt > value)
				{
					Log.Error($"ItemCountsRemoved attempted to remove {cnt}/{value} Items of {removedDef}");
					itemCounts[removedDef] = 0;
				}
				itemCounts[removedDef] -= cnt;
			}
			else
			{
				Log.Error($"ItemCountsRemoved attempted to remove nonexistent def {removedDef}");
			}
		}

		public void ItemCountsAdded(ThingDef addedDef, int cnt)
		{
			if (!itemCounts.TryAdd(addedDef, cnt))
			{
				itemCounts[addedDef] += cnt;
			}
		}

		public bool ItemIsLimit(ThingDef thing, bool cntStacks, int limit)
		{
			if (limit < 0)
			{
				return true;
			}
			itemCounts.TryGetValue(thing, out var value);
			if (cntStacks)
			{
				value = Mathf.CeilToInt((float)value / (float)thing.stackLimit);
			}
			return value >= limit;
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building_Storage)this).GetGizmos();
		}
	}
	[StaticConstructorOnStartup]
	public class Building_MassStorageUnitPowered : Building_MassStorageUnit
	{
		private static readonly Texture2D StoragePawnAccessSwitchIcon = ContentFinder<Texture2D>.Get("PRFUi/dsu", true);

		private CompPowerTrader compPowerTrader;

		private bool pawnAccess = true;

		public override bool Powered
		{
			get
			{
				CompPowerTrader obj = compPowerTrader;
				return obj != null && obj.PowerOn;
			}
		}

		protected override int MaxNumberItemsInternal => ModExtensionCrate?.limit ?? int.MaxValue;

		protected override bool CanStoreMoreItems => Powered && ((Thing)this).Spawned && (ModExtensionCrate == null || base.StoredItemsCount < MaxNumberItemsInternal);

		public override bool CanReceiveIO => base.CanReceiveIO && Powered && ((Thing)this).Spawned;

		public override bool ForbidPawnInput => base.ForbidPawnAccess || !pawnAccess || !CanStoreMoreItems;

		public override bool ForbidPawnOutput => base.ForbidPawnAccess || !pawnAccess;

		public float ExtraPowerDraw => (float)base.StoredItems.Count * 10f;

		public override void Notify_ReceivedThing(Thing newItem)
		{
			base.Notify_ReceivedThing(newItem);
			UpdatePowerConsumption();
		}

		public override void Notify_LostThing(Thing newItem)
		{
			base.Notify_LostThing(newItem);
			UpdatePowerConsumption();
		}

		private void UpdatePowerConsumption()
		{
			if (compPowerTrader == null)
			{
				compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			}
			FridgePowerPatchUtil.UpdatePowerDraw(this, compPowerTrader);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look<bool>(ref pawnAccess, "pawnAccess", true, false);
			if (compPowerTrader == null)
			{
				compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			}
		}

		protected override void ReceiveCompSignal(string signal)
		{
			((ThingWithComps)this).ReceiveCompSignal(signal);
			if (signal == "PowerTurnedOn")
			{
				RefreshStorage();
			}
		}

		protected override void Tick()
		{
			((ThingWithComps)this).Tick();
			if (((Thing)this).Spawned && Gen.IsHashIntervalTick((Thing)(object)this, 60))
			{
				UpdatePowerConsumption();
			}
		}

		public override void PostMapInit()
		{
			((ThingWithComps)this).PostMapInit();
			if (compPowerTrader == null)
			{
				compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			}
			RefreshStorage();
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			if (Prefs.DevMode)
			{
				yield return (Gizmo)new Command_Action
				{
					defaultLabel = "DEBUG: Debug actions",
					action = delegate
					{
						//IL_0011: Unknown result type (might be due to invalid IL or missing references)
						//IL_001b: Expected O, but got Unknown
						Find.WindowStack.Add((Window)new FloatMenu(DebugActions().ToList()));
					}
				};
			}
			if (!base.ForbidPawnAccess)
			{
				yield return (Gizmo)new Command_Toggle
				{
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRFPawnAccessLabel")),
					isActive = () => pawnAccess,
					toggleAction = delegate
					{
						pawnAccess = !pawnAccess;
					},
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRFPawnAccessDesc")),
					icon = (Texture)(object)StoragePawnAccessSwitchIcon
				};
			}
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Invalid comparison between Unknown and I4
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			if ((int)mode == 4)
			{
				DefModExtension_Crate modExtensionCrate = ModExtensionCrate;
				if (modExtensionCrate != null && modExtensionCrate.destroyContainsItems)
				{
					base.StoredItems.Where((Thing t) => !t.Destroyed).ToList().ForEach(delegate(Thing x)
					{
						x.Destroy((DestroyMode)0);
					});
				}
			}
			base.DeSpawn(mode);
		}

		protected virtual IEnumerable<FloatMenuOption> DebugActions()
		{
			yield return new FloatMenuOption("Update power consumption", (Action)UpdatePowerConsumption, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
			yield return new FloatMenuOption("Log item count", (Action)delegate
			{
				Log.Message(base.StoredItemsCount.ToString());
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
		}

		protected override string GetUIThingLabel()
		{
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			if ((((Def)((Thing)this).def).GetModExtension<DefModExtension_Crate>()?.limit).HasValue)
			{
				return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFCrateUIThingLabel", NamedArgument.op_Implicit(base.StoredItemsCount), NamedArgument.op_Implicit(ModExtensionCrate.limit)));
			}
			return base.GetUIThingLabel();
		}

		public override string GetITabString(int itemsSelected)
		{
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			if ((ModExtensionCrate?.limit).HasValue)
			{
				return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFItemsTabLabel_Crate", NamedArgument.op_Implicit(base.StoredItemsCount), NamedArgument.op_Implicit(ModExtensionCrate.limit), NamedArgument.op_Implicit(itemsSelected)));
			}
			return base.GetITabString(itemsSelected);
		}
	}
	[StaticConstructorOnStartup]
	public abstract class Building_StorageUnitIOBase : Building_Storage, IForbidPawnInputItem, ISlotGroupParent, IStoreSettingsParent, IHaulDestination, IRenameable
	{
		private static readonly Texture2D CargoPlatformTex = ContentFinder<Texture2D>.Get("Storage/CargoPlatform", true);

		protected static readonly Texture2D IOModeTex = ContentFinder<Texture2D>.Get("PRFUi/IoIcon", true);

		public StorageIOMode Mode;

		private Building linkedStorageParentBuilding;

		private StorageSettings outputStoreSettings;

		private OutputSettings outputSettings;

		protected CompPowerTrader PowerTrader;

		private string uniqueName;

		private static readonly Texture2D RenameTex = ContentFinder<Texture2D>.Get("UI/Buttons/Rename", true);

		private bool forbidOnPlacement;

		public ILinkableStorageParent boundStorageUnit => linkedStorageParentBuilding as ILinkableStorageParent;

		protected virtual IntVec3 WorkPosition => ((Thing)this).Position;

		protected abstract bool IsAdvancedPort { get; }

		protected virtual bool ShowLimitGizmo => true;

		public string RenamableLabel
		{
			get
			{
				return uniqueName ?? ((Thing)this).LabelCapNoCount;
			}
			set
			{
				uniqueName = value;
			}
		}

		public string BaseLabel => ((Thing)this).LabelCapNoCount;

		public string InspectLabel => ((Entity)this).LabelCap;

		protected bool ForbidOnPlacement => forbidOnPlacement;

		public override Graphic Graphic => (IOMode == StorageIOMode.Input) ? ((Thing)this).Graphic.GetColoredVersion(((Thing)this).Graphic.Shader, ((Def)((Thing)this).def).GetModExtension<DefModExtension_StorageUnitIOPortColor>().inColor, Color.white) : ((Thing)this).Graphic.GetColoredVersion(((Thing)this).Graphic.Shader, ((Def)((Thing)this).def).GetModExtension<DefModExtension_StorageUnitIOPortColor>().outColor, Color.white);

		public virtual StorageIOMode IOMode
		{
			get
			{
				return Mode;
			}
			set
			{
				if (Mode != value)
				{
					Mode = value;
					Notify_NeedRefresh();
				}
			}
		}

		public ILinkableStorageParent BoundStorageUnit
		{
			get
			{
				return boundStorageUnit;
			}
			set
			{
				//IL_0016: Unknown result type (might be due to invalid IL or missing references)
				//IL_0020: Expected O, but got Unknown
				boundStorageUnit?.DeregisterPort(this);
				linkedStorageParentBuilding = (Building)value;
				value?.RegisterPort(this);
				Notify_NeedRefresh();
			}
		}

		protected OutputSettings OutputSettings => outputSettings ?? (outputSettings = new OutputSettings("IOPort_Minimum_UseTooltip", "IOPort_Maximum_UseTooltip"));

		public virtual bool ForbidPawnInput
		{
			get
			{
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				if (IOMode != StorageIOMode.Output || !OutputSettings.UseMax)
				{
					return false;
				}
				Thing firstItem = GridsUtility.GetFirstItem(WorkPosition, ((Thing)this).Map);
				if (firstItem != null)
				{
					return OutputSettings.CountNeededToReachMax(firstItem.stackCount, firstItem.def.stackLimit) <= 0;
				}
				return false;
			}
		}

		public override void ExposeData()
		{
			((Building_Storage)this).ExposeData();
			Scribe_Values.Look<StorageIOMode>(ref Mode, "mode", StorageIOMode.Input, false);
			Scribe_References.Look<Building>(ref linkedStorageParentBuilding, "boundStorageUnit", false);
			Scribe_Deep.Look<StorageSettings>(ref outputStoreSettings, "outputStoreSettings", new object[1] { this });
			Scribe_Deep.Look<OutputSettings>(ref outputSettings, "outputSettings", new object[2] { "IOPort_Minimum_UseTooltip", "IOPort_Maximum_UseTooltip" });
			Scribe_Values.Look<string>(ref uniqueName, "uniqueName", (string)null, false);
			Scribe_Values.Look<bool>(ref forbidOnPlacement, "forbidOnPlacement", false, false);
		}

		public override string GetInspectString()
		{
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0120: Unknown result type (might be due to invalid IL or missing references)
			//IL_0125: Unknown result type (might be due to invalid IL or missing references)
			if (OutputSettings.UseMin && OutputSettings.UseMax)
			{
				return TaggedString.op_Implicit(((Building_Storage)this).GetInspectString() + "\n" + TranslatorFormattedStringExtensions.Translate("IOPort_Minimum", NamedArgument.op_Implicit(OutputSettings.Min)) + "\n" + TranslatorFormattedStringExtensions.Translate("IOPort_Maximum", NamedArgument.op_Implicit(OutputSettings.Max)));
			}
			if (OutputSettings.UseMin && !OutputSettings.UseMax)
			{
				return TaggedString.op_Implicit(((Building_Storage)this).GetInspectString() + "\n" + TranslatorFormattedStringExtensions.Translate("IOPort_Minimum", NamedArgument.op_Implicit(OutputSettings.Min)));
			}
			if (!OutputSettings.UseMin && OutputSettings.UseMax)
			{
				return TaggedString.op_Implicit(((Building_Storage)this).GetInspectString() + "\n" + TranslatorFormattedStringExtensions.Translate("IOPort_Maximum", NamedArgument.op_Implicit(OutputSettings.Max)));
			}
			return ((Building_Storage)this).GetInspectString();
		}

		public override void PostMake()
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Expected O, but got Unknown
			((Building_Storage)this).PostMake();
			PowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			outputStoreSettings = new StorageSettings((IStoreSettingsParent)(object)this);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building_Storage)this).SpawnSetup(map, respawningAfterLoad);
			PowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			if (boundStorageUnit?.Map != map)
			{
				Building obj = linkedStorageParentBuilding;
				if (obj != null && ((Thing)obj).Spawned)
				{
					BoundStorageUnit = null;
				}
			}
			Building obj2 = linkedStorageParentBuilding;
			if (obj2 != null && ((Thing)obj2).Destroyed && GravshipPlacementUtility.placingGravship)
			{
				BoundStorageUnit = null;
			}
			((Thing)this).def.building.groupingLabel = ((Thing)this).LabelCapNoCount;
		}

		protected override void ReceiveCompSignal(string signal)
		{
			((ThingWithComps)this).ReceiveCompSignal(signal);
			if (signal == "PowerTurnedOn")
			{
				Notify_NeedRefresh();
			}
		}

		public override void DeSpawn(DestroyMode destroyMode = (DestroyMode)0)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			((Building_Storage)this).DeSpawn(destroyMode);
			boundStorageUnit?.DeregisterPort(this);
		}

		protected void Notify_NeedRefresh()
		{
			RefreshStoreSettings();
			switch (IOMode)
			{
			case StorageIOMode.Input:
				RefreshInput();
				break;
			case StorageIOMode.Output:
				RefreshOutput();
				break;
			}
		}

		public override void Notify_ReceivedThing(Thing newItem)
		{
			((Building_Storage)this).Notify_ReceivedThing(newItem);
			if (Mode == StorageIOMode.Input)
			{
				RefreshInput();
			}
		}

		public override void Notify_LostThing(Thing newItem)
		{
			((Building_Storage)this).Notify_LostThing(newItem);
			if (Mode == StorageIOMode.Output)
			{
				RefreshOutput();
			}
		}

		protected override void Tick()
		{
			((ThingWithComps)this).Tick();
			if (((Thing)this).Spawned && Gen.IsHashIntervalTick((Thing)(object)this, 10))
			{
				Notify_NeedRefresh();
			}
		}

		private void RefreshStoreSettings()
		{
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_009a: Expected O, but got Unknown
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			if (IOMode == StorageIOMode.Output)
			{
				base.settings = outputStoreSettings;
				if (boundStorageUnit != null && base.settings.Priority != boundStorageUnit.GetSettings.Priority)
				{
					base.settings.Priority = boundStorageUnit.GetSettings.Priority;
				}
			}
			else if (boundStorageUnit != null)
			{
				base.settings = boundStorageUnit.GetSettings;
			}
			else
			{
				base.settings = new StorageSettings((IStoreSettingsParent)(object)this);
			}
		}

		protected virtual void RefreshInput()
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			if (!PowerTrader.PowerOn)
			{
				return;
			}
			Thing firstItem = GridsUtility.GetFirstItem(WorkPosition, ((Thing)this).Map);
			if (Mode == StorageIOMode.Input && firstItem != null)
			{
				ILinkableStorageParent linkableStorageParent = boundStorageUnit;
				if (linkableStorageParent != null && linkableStorageParent.CanReceiveThing(firstItem))
				{
					boundStorageUnit.HandleNewItem(firstItem);
				}
			}
		}

		protected bool ItemsThatSatisfyMin(ref List<Thing> itemCandidates, Thing currentItem)
		{
			if (currentItem != null)
			{
				itemCandidates = ((IEnumerable<Thing>)itemCandidates).Where((Func<Thing, bool>)currentItem.CanStackWith).ToList();
				int num = (OutputSettings.UseMin ? outputSettings.Min : 0);
				int num2 = currentItem.stackCount;
				for (int i = 0; i < itemCandidates.Count; i++)
				{
					if (num2 >= num)
					{
						break;
					}
					num2 += itemCandidates[i].stackCount;
				}
				return OutputSettings.SatisfiesMin(num2);
			}
			return (from t in itemCandidates
				group t by t.def).FirstOrDefault((IGrouping<ThingDef, Thing> g) => OutputSettings.SatisfiesMin(g.Sum((Thing t) => t.stackCount)))?.Any() ?? false;
		}

		protected virtual void RefreshOutput()
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_01be: Unknown result type (might be due to invalid IL or missing references)
			if (!PowerTrader.PowerOn)
			{
				return;
			}
			Thing val = GridsUtility.GetFirstItem(WorkPosition, ((Thing)this).Map);
			bool flag = val == null || (base.settings.AllowedToAccept(val) && OutputSettings.SatisfiesMax(val.stackCount, val.def.stackLimit));
			ILinkableStorageParent linkableStorageParent = boundStorageUnit;
			if (linkableStorageParent == null || !linkableStorageParent.CanReceiveIO)
			{
				return;
			}
			if (flag)
			{
				List<Thing> itemCandidates = new List<Thing>(from Thing t in boundStorageUnit.StoredItems
					where base.settings.AllowedToAccept(t)
					select t);
				if (ItemsThatSatisfyMin(ref itemCandidates, val))
				{
					foreach (Thing item in itemCandidates)
					{
						if (val != null)
						{
							if (val.CanStackWith(item))
							{
								int num = Math.Min(item.stackCount, OutputSettings.CountNeededToReachMax(val.stackCount, val.def.stackLimit));
								if (num > 0)
								{
									Thing val2 = item.SplitOff(num);
									if (item.stackCount <= 0)
									{
										boundStorageUnit.HandleMoveItem(item);
									}
									val.TryAbsorbStack(val2, true);
								}
							}
						}
						else
						{
							int num2 = OutputSettings.CountNeededToReachMax(0, item.stackCount);
							if (num2 > 0)
							{
								Thing val3 = item.SplitOff(num2);
								if (item.stackCount <= 0)
								{
									boundStorageUnit.HandleMoveItem(item);
								}
								val = GenSpawn.Spawn(val3, WorkPosition, ((Thing)this).Map, (WipeMode)0);
							}
						}
						if (val != null && !OutputSettings.SatisfiesMax(val.stackCount, val.def.stackLimit))
						{
							break;
						}
					}
				}
			}
			if (val != null && (!base.settings.AllowedToAccept(val) || !OutputSettings.SatisfiesMin(val.stackCount)) && boundStorageUnit.GetSettings.AllowedToAccept(val))
			{
				ForbidUtility.SetForbidden(val, false, false);
				boundStorageUnit.HandleNewItem(val);
			}
			if (val != null && !OutputSettings.SatisfiesMax(val.stackCount, val.def.stackLimit) && boundStorageUnit.GetSettings.AllowedToAccept(val))
			{
				int num3 = -OutputSettings.CountNeededToReachMax(val.stackCount, val.def.stackLimit);
				if (num3 > 0)
				{
					Thing val4 = val.SplitOff(num3);
					ForbidUtility.SetForbidden(val4, false, false);
					boundStorageUnit.HandleNewItem(val4);
				}
			}
			if (val != null)
			{
				ForbidUtility.SetForbidden(val, ForbidOnPlacement, false);
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			Command_Action val = new Command_Action();
			TaggedString val2 = Translator.Translate("PRFBoundStorageBuilding") + ": ";
			IRenameable val3 = (IRenameable)boundStorageUnit;
			((Command)val).defaultLabel = TaggedString.op_Implicit(val2 + ((((int)val3 != 0) ? val3.RenamableLabel : null) ?? TaggedString.op_Implicit(Translator.Translate("NoneBrackets"))));
			val.action = delegate
			{
				//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ca: Expected O, but got Unknown
				//IL_0097: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b8: Expected O, but got Unknown
				List<Building> list = ((Thing)this).Map.listerBuildings.allBuildingsColonist.Where((Building b) => b is ILinkableStorageParent linkableStorageParent && linkableStorageParent.CanUseIOPort).ToList();
				if (IsAdvancedPort)
				{
					list.RemoveAll((Building b) => !(b as ILinkableStorageParent).AdvancedIOAllowed);
				}
				List<FloatMenuOption> list2 = new List<FloatMenuOption>(((IEnumerable<Building>)list).Select((Func<Building, FloatMenuOption>)((Building b) => new FloatMenuOption(((IRenameable)b).RenamableLabel, (Action)delegate
				{
					SelectedPorts().ToList().ForEach(delegate(Building_StorageUnitIOBase p)
					{
						p.BoundStorageUnit = b as ILinkableStorageParent;
					});
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0))));
				if (list2.Count == 0)
				{
					list2.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("NoneBrackets")), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
				}
				Find.WindowStack.Add((Window)new FloatMenu(list2));
			};
			((Command)val).icon = (Texture)(object)CargoPlatformTex;
			yield return (Gizmo)val;
			yield return (Gizmo)new Command_Action
			{
				icon = (Texture)(object)RenameTex,
				action = delegate
				{
					Find.WindowStack.Add((Window)(object)new Dialog_RenameStorageUnitIOBase(this));
				},
				hotKey = KeyBindingDefOf.Misc1,
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRFRenameMassStorageUnitLabel")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRFRenameMassStorageUnitDesc"))
			};
			if (IOMode == StorageIOMode.Output && ShowLimitGizmo)
			{
				yield return (Gizmo)new Command_Action
				{
					icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel", true),
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRFIOOutputSettings")),
					action = delegate
					{
						Find.WindowStack.Add((Window)(object)new Dialog_OutputMinMax(OutputSettings, delegate
						{
							(from p in SelectedPorts()
								where p.IOMode == StorageIOMode.Output
								select p).ToList().ForEach(delegate(Building_StorageUnitIOBase p)
							{
								OutputSettings.Copy(p.OutputSettings);
							});
						}));
					}
				};
			}
			if (Mode == StorageIOMode.Output)
			{
				yield return (Gizmo)new Command_Toggle
				{
					isActive = () => forbidOnPlacement,
					toggleAction = delegate
					{
						forbidOnPlacement = !forbidOnPlacement;
					},
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRF_Toggle_ForbidOnPlacement")),
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRF_Toggle_ForbidOnPlacementDesc")),
					icon = (Texture)(object)(forbidOnPlacement ? RS.ForbidOn : RS.ForbidOff)
				};
			}
		}

		private IEnumerable<Building_StorageUnitIOBase> SelectedPorts()
		{
			List<Building_StorageUnitIOBase> list = Find.Selector.SelectedObjects.OfType<Building_StorageUnitIOBase>().ToList();
			if (!list.Contains(this))
			{
				list.Add(this);
			}
			return list;
		}

		public virtual bool OutputItem(Thing thing)
		{
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			ILinkableStorageParent linkableStorageParent = boundStorageUnit;
			if (linkableStorageParent != null && linkableStorageParent.CanReceiveIO)
			{
				return GenPlace.TryPlaceThing(thing.SplitOff(thing.stackCount), WorkPosition, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)delegate(IntVec3 pos)
				{
					//IL_0036: Unknown result type (might be due to invalid IL or missing references)
					//IL_003d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0064: Unknown result type (might be due to invalid IL or missing references)
					if (base.settings.AllowedToAccept(thing) && OutputSettings.SatisfiesMin(thing.stackCount) && pos == WorkPosition)
					{
						return true;
					}
					foreach (Thing item in ((Thing)this).Map.thingGrid.ThingsListAt(pos))
					{
						if (item is Building_StorageUnitIOPort)
						{
							return false;
						}
					}
					return true;
				}, (Rot4?)null, 1);
			}
			return false;
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building_Storage)this).GetGizmos();
		}
	}
	[StaticConstructorOnStartup]
	public class Building_StorageUnitIOPort : Building_StorageUnitIOBase
	{
		public override Graphic Graphic => (IOMode == StorageIOMode.Input) ? base.Graphic.GetColoredVersion(base.Graphic.Shader, ((Def)((Thing)this).def).GetModExtension<DefModExtension_StorageUnitIOPortColor>().inColor, Color.white) : base.Graphic.GetColoredVersion(base.Graphic.Shader, ((Def)((Thing)this).def).GetModExtension<DefModExtension_StorageUnitIOPortColor>().outColor, Color.white);

		public override StorageIOMode IOMode
		{
			get
			{
				return Mode;
			}
			set
			{
				if (Mode != value)
				{
					Mode = value;
					Notify_NeedRefresh();
				}
			}
		}

		protected override bool IsAdvancedPort => false;

		protected override void RefreshInput()
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			if (!PowerTrader.PowerOn)
			{
				return;
			}
			Thing firstItem = GridsUtility.GetFirstItem(((Thing)this).Position, ((Thing)this).Map);
			if (Mode == StorageIOMode.Input && firstItem != null)
			{
				ILinkableStorageParent linkableStorageParent = base.boundStorageUnit;
				if (linkableStorageParent != null && linkableStorageParent.CanReceiveThing(firstItem))
				{
					base.boundStorageUnit.HandleNewItem(firstItem);
				}
			}
		}

		private static void AbsorbAmount(ref Thing baseThing, ref Thing toBeAbsorbed, int count)
		{
			if (baseThing.CanStackWith(toBeAbsorbed))
			{
				if (baseThing.def.useHitPoints)
				{
					baseThing.HitPoints = Mathf.CeilToInt((float)(baseThing.HitPoints * baseThing.stackCount + toBeAbsorbed.HitPoints * count) / (float)(baseThing.stackCount + count));
				}
				Thing obj = baseThing;
				obj.stackCount += count;
				Thing obj2 = toBeAbsorbed;
				obj2.stackCount -= count;
				if (baseThing.Map != null)
				{
					baseThing.DirtyMapMesh(baseThing.Map);
				}
				StealAIDebugDrawer.Notify_ThingChanged(baseThing);
				if (baseThing.Spawned)
				{
					baseThing.Map.listerMergeables.Notify_ThingStackChanged(baseThing);
				}
				if (toBeAbsorbed.stackCount <= 0)
				{
					toBeAbsorbed.Destroy((DestroyMode)0);
				}
			}
		}

		protected override void RefreshOutput()
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
			if (!PowerTrader.PowerOn || GravshipPlacementUtility.placingGravship || GravshipUtility.generatingGravship)
			{
				return;
			}
			Thing baseThing = GridsUtility.GetFirstItem(((Thing)this).Position, ((Thing)this).Map);
			bool flag = baseThing == null || (((Building_Storage)this).settings.AllowedToAccept(baseThing) && base.OutputSettings.SatisfiesMax(baseThing.stackCount, baseThing.def.stackLimit));
			ILinkableStorageParent linkableStorageParent = base.boundStorageUnit;
			if (linkableStorageParent == null || !linkableStorageParent.CanReceiveIO)
			{
				return;
			}
			if (flag)
			{
				List<Thing> itemCandidates = new List<Thing>(from Thing t in base.boundStorageUnit.StoredItems
					where ((Building_Storage)this).settings.AllowedToAccept(t)
					select t);
				if (ItemsThatSatisfyMin(ref itemCandidates, baseThing))
				{
					foreach (Thing item in itemCandidates)
					{
						if (baseThing != null)
						{
							if (baseThing.CanStackWith(item))
							{
								int num = Math.Min(item.stackCount, base.OutputSettings.CountNeededToReachMax(baseThing.stackCount, baseThing.def.stackLimit));
								if (num > 0)
								{
									Thing toBeAbsorbed = item;
									AbsorbAmount(ref baseThing, ref toBeAbsorbed, num);
									if (toBeAbsorbed.stackCount <= 0)
									{
										base.boundStorageUnit.HandleMoveItem(toBeAbsorbed);
									}
								}
							}
						}
						else
						{
							int num2 = base.OutputSettings.CountNeededToReachMax(0, item.stackCount);
							if (num2 > 0)
							{
								Thing val = item.SplitOff(num2);
								if (item.stackCount <= 0 || val == item)
								{
									base.boundStorageUnit.HandleMoveItem(item);
								}
								baseThing = GenSpawn.Spawn(val, ((Thing)this).Position, ((Thing)this).Map, (WipeMode)0);
							}
						}
						if (baseThing != null && !base.OutputSettings.SatisfiesMax(baseThing.stackCount, baseThing.def.stackLimit))
						{
							break;
						}
					}
				}
			}
			if (baseThing != null && (!((Building_Storage)this).settings.AllowedToAccept(baseThing) || !base.OutputSettings.SatisfiesMin(baseThing.stackCount)) && base.boundStorageUnit.GetSettings.AllowedToAccept(baseThing))
			{
				ForbidUtility.SetForbidden(baseThing, false, false);
				base.boundStorageUnit.HandleNewItem(baseThing);
			}
			if (baseThing != null && !base.OutputSettings.SatisfiesMax(baseThing.stackCount, baseThing.def.stackLimit) && base.boundStorageUnit.GetSettings.AllowedToAccept(baseThing))
			{
				int num3 = -base.OutputSettings.CountNeededToReachMax(baseThing.stackCount, baseThing.def.stackLimit);
				if (num3 > 0)
				{
					Thing val2 = baseThing.SplitOff(num3);
					ForbidUtility.SetForbidden(val2, false, false);
					base.boundStorageUnit.HandleNewItem(val2);
				}
			}
			if (baseThing != null)
			{
				ForbidUtility.SetForbidden(baseThing, base.ForbidOnPlacement, false);
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			yield return (Gizmo)new Command_Action
			{
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRFIOMode") + ": " + ((IOMode == StorageIOMode.Input) ? Translator.Translate("PRFIOInput") : Translator.Translate("PRFIOOutput"))),
				action = delegate
				{
					//IL_0012: Unknown result type (might be due to invalid IL or missing references)
					//IL_0034: Unknown result type (might be due to invalid IL or missing references)
					//IL_003e: Expected O, but got Unknown
					//IL_0045: Unknown result type (might be due to invalid IL or missing references)
					//IL_0067: Unknown result type (might be due to invalid IL or missing references)
					//IL_0071: Expected O, but got Unknown
					//IL_0072: Unknown result type (might be due to invalid IL or missing references)
					//IL_007c: Expected O, but got Unknown
					Find.WindowStack.Add((Window)new FloatMenu(new List<FloatMenuOption>(2)
					{
						new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("PRFIOInput")), (Action)delegate
						{
							SelectedPorts().ToList().ForEach(delegate(Building_StorageUnitIOPort p)
							{
								p.IOMode = StorageIOMode.Input;
							});
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
						new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("PRFIOOutput")), (Action)delegate
						{
							SelectedPorts().ToList().ForEach(delegate(Building_StorageUnitIOPort p)
							{
								p.IOMode = StorageIOMode.Output;
							});
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
					}));
				},
				icon = (Texture)(object)Building_StorageUnitIOBase.IOModeTex
			};
		}

		private IEnumerable<Building_StorageUnitIOPort> SelectedPorts()
		{
			List<Building_StorageUnitIOPort> list = Find.Selector.SelectedObjects.OfType<Building_StorageUnitIOPort>().ToList();
			if (!list.Contains(this))
			{
				list.Add(this);
			}
			return list;
		}

		public override bool OutputItem(Thing thing)
		{
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			ILinkableStorageParent linkableStorageParent = base.boundStorageUnit;
			if (linkableStorageParent == null || !linkableStorageParent.CanReceiveIO)
			{
				return false;
			}
			return GenPlace.TryPlaceThing(thing.SplitOff(thing.stackCount), ((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)delegate(IntVec3 pos)
			{
				//IL_0065: Unknown result type (might be due to invalid IL or missing references)
				//IL_003b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0042: Unknown result type (might be due to invalid IL or missing references)
				if (((Building_Storage)this).settings.AllowedToAccept(thing) && base.OutputSettings.SatisfiesMin(thing.stackCount) && pos == ((Thing)this).Position)
				{
					return true;
				}
				foreach (Thing item in ((Thing)this).Map.thingGrid.ThingsListAt(pos))
				{
					if (item is Building_StorageUnitIOPort)
					{
						return false;
					}
				}
				return true;
			}, (Rot4?)null, 1);
		}
	}
	public class DefModExtension_StorageUnitIOPortColor : DefModExtension
	{
		public Color inColor;

		public Color outColor;
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
			int num = 0;
			if (source.Length != target.Length)
			{
				return 99999;
			}
			for (int i = 0; i < source.Length; i++)
			{
				if (!source[i].Equals(target[i]))
				{
					num++;
				}
			}
			return num;
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
	public interface ILinkableStorageParent
	{
		List<Thing> StoredItems { get; }

		bool AdvancedIOAllowed { get; }

		StorageSettings GetSettings { get; }

		IntVec3 GetPosition { get; }

		bool CanReceiveIO { get; }

		Map Map { get; }

		int StoredItemsCount { get; }

		LocalTargetInfo GetTargetInfo { get; }

		bool Powered { get; }

		bool CanUseIOPort { get; }

		void HandleNewItem(Thing item);

		void HandleMoveItem(Thing item);

		bool CanReceiveThing(Thing item);

		bool HoldsPos(IntVec3 pos);

		void DeregisterPort(Building_StorageUnitIOBase port);

		void RegisterPort(Building_StorageUnitIOBase port);

		string GetITabString(int itemsSelected);

		bool OutputItem(Thing item);
	}
	public class OutputSettings(string minTooltip, string maxTooltip) : IExposable
	{
		public string MinTooltip = minTooltip;

		public string MaxTooltip = maxTooltip;

		public bool UseMin;

		public bool UseMax;

		private int min;

		private int max = 75;

		public int Min
		{
			get
			{
				return min;
			}
			set
			{
				min = value;
				if (min > max)
				{
					max = value;
				}
			}
		}

		public int Max
		{
			get
			{
				return max;
			}
			set
			{
				max = value;
				if (min > max)
				{
					min = value;
				}
			}
		}

		public void ExposeData()
		{
			Scribe_Values.Look<string>(ref MinTooltip, "minTooltip", (string)null, false);
			Scribe_Values.Look<string>(ref MaxTooltip, "maxTooltip", (string)null, false);
			Scribe_Values.Look<bool>(ref UseMin, "useMin", false, false);
			Scribe_Values.Look<bool>(ref UseMax, "useMax", false, false);
			Scribe_Values.Look<int>(ref min, "min", 0, false);
			Scribe_Values.Look<int>(ref max, "max", 0, false);
			if (min > max)
			{
				max = min;
			}
		}

		public bool SatisfiesMax(int stackCount, int stackLimit)
		{
			return CountNeededToReachMax(stackCount, stackLimit) > 0;
		}

		public bool SatisfiesMin(int stackCount)
		{
			return !UseMin || stackCount >= min;
		}

		public int CountNeededToReachMax(int currentCount, int limit)
		{
			if (UseMax)
			{
				return Math.Min(limit, max) - currentCount;
			}
			return limit - currentCount;
		}

		public void Copy(OutputSettings other)
		{
			other.MinTooltip = MinTooltip;
			other.MaxTooltip = MaxTooltip;
			other.UseMin = UseMin;
			other.UseMax = UseMax;
			other.min = min;
			other.max = max;
		}
	}
	public enum StorageIOMode
	{
		Input,
		Output
	}
	public class StorageOutputUtil(Building building)
	{
		private readonly CompOutputAdjustable compOutputAdjustable = ((ThingWithComps)building).GetComp<CompOutputAdjustable>();

		private readonly Map map = ((Thing)building).Map;

		private IntVec3 OutputCell => compOutputAdjustable?.CurrentCell ?? (((Thing)building).Position + new IntVec3(0, 0, -2));

		private bool ValidatePos(IntVec3 position)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return !GenCollection.Any<Thing>(GridsUtility.GetThingList(position, map), (Predicate<Thing>)((Thing e) => e is ILinkableStorageParent));
		}

		public bool OutputItem(Thing item)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			return GenPlace.TryPlaceThing(item.SplitOff(item.stackCount), OutputCell, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)ValidatePos, (Rot4?)null, 1);
		}
	}
}
namespace ProjectRimFactory.Storage.UI
{
	public class Dialog_OutputMinMax : Window
	{
		private class GracefulInput(Action<int> applyChangeAction)
		{
			private const int GracePeriod = 75;

			private int remainingGracePeriod = -1;

			private int oldValue = -1;

			public int Value = -1;

			private bool ValueChanged => Value != oldValue;

			public void UpdateValue(int value)
			{
				Value = value;
				oldValue = Value;
			}

			private void ApplyChange()
			{
				remainingGracePeriod = -1;
				applyChangeAction(Value);
				Value = -1;
			}

			public void LostFocus()
			{
				if (ValueChanged)
				{
					if (remainingGracePeriod > -1)
					{
						remainingGracePeriod = 0;
					}
					if (remainingGracePeriod == 0)
					{
						ApplyChange();
					}
				}
			}

			public void Tick()
			{
				if (ValueChanged)
				{
					if (remainingGracePeriod == -1)
					{
						remainingGracePeriod = 75;
					}
					if (remainingGracePeriod > 0)
					{
						remainingGracePeriod--;
					}
					if (remainingGracePeriod == 0)
					{
						ApplyChange();
					}
				}
			}
		}

		private static string minBufferString;

		private static string maxBufferString;

		private string controlIdMinInput = null;

		private string controlIdMaxInput = null;

		private static OutputSettings outputSettings;

		private const float TitleLabelHeight = 32f;

		private readonly Action postClose;

		private readonly GracefulInput inputMaximum = new GracefulInput(delegate(int i)
		{
			if (i >= 0)
			{
				outputSettings.Max = i;
				maxBufferString = null;
				minBufferString = null;
			}
		});

		private readonly GracefulInput inputMinimum = new GracefulInput(delegate(int i)
		{
			if (i >= 0)
			{
				outputSettings.Min = i;
				maxBufferString = null;
				minBufferString = null;
			}
		});

		public override Vector2 InitialSize => new Vector2(500f, 250f);

		public Dialog_OutputMinMax(OutputSettings settings, Action postClose = null)
			: base((IWindowDrawing)null)
		{
			outputSettings = settings;
			base.doCloseX = true;
			base.doCloseButton = true;
			base.closeOnClickedOutside = true;
			base.absorbInputAroundWindow = true;
			base.draggable = true;
			base.drawShadow = true;
			base.focusWhenOpened = true;
			base.forcePause = true;
			this.postClose = postClose;
		}

		public override void DoWindowContents(Rect rect)
		{
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_009b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Expected O, but got Unknown
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_013a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_017c: Unknown result type (might be due to invalid IL or missing references)
			//IL_019a: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01da: Unknown result type (might be due to invalid IL or missing references)
			//IL_01df: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0205: Unknown result type (might be due to invalid IL or missing references)
			//IL_020c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0217: Unknown result type (might be due to invalid IL or missing references)
			//IL_021f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0296: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0302: Unknown result type (might be due to invalid IL or missing references)
			//IL_0307: Unknown result type (might be due to invalid IL or missing references)
			//IL_0309: Unknown result type (might be due to invalid IL or missing references)
			//IL_030e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0317: Unknown result type (might be due to invalid IL or missing references)
			//IL_031f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0326: Unknown result type (might be due to invalid IL or missing references)
			//IL_0331: Unknown result type (might be due to invalid IL or missing references)
			//IL_0339: Unknown result type (might be due to invalid IL or missing references)
			if (maxBufferString == null)
			{
				maxBufferString = outputSettings.Max.ToString();
			}
			if (minBufferString == null)
			{
				minBufferString = outputSettings.Min.ToString();
			}
			if (inputMaximum.Value == -1)
			{
				inputMaximum.UpdateValue(outputSettings.Max);
			}
			if (inputMinimum.Value == -1)
			{
				inputMinimum.UpdateValue(outputSettings.Min);
			}
			Listing_Standard val = new Listing_Standard((GameFont)1)
			{
				ColumnWidth = ((Rect)(ref rect)).width
			};
			string nameOfFocusedControl = GUI.GetNameOfFocusedControl();
			if (nameOfFocusedControl == controlIdMaxInput)
			{
				inputMinimum.LostFocus();
			}
			else if (nameOfFocusedControl == controlIdMinInput)
			{
				inputMaximum.LostFocus();
			}
			else
			{
				inputMaximum.LostFocus();
				inputMinimum.LostFocus();
			}
			((Listing)val).Begin(rect);
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(0f, 0f, ((Rect)(ref rect)).width, 32f);
			Text.Font = (GameFont)2;
			Widgets.Label(val2, Translator.Translate("SmartHopper_SetTargetAmount"));
			Text.Font = (GameFont)1;
			((Listing)val).Gap(12f);
			((Listing)val).Gap(12f);
			((Listing)val).Gap(12f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("SmartHopper_Minimum_Label")), ref outputSettings.UseMin, TaggedString.op_Implicit(Translator.Translate(outputSettings.MinTooltip)), 0f, 1f);
			((Listing)val).Gap(12f);
			Rect rect2 = ((Listing)val).GetRect(Text.LineHeight, 1f);
			Rect val3 = GenUI.Rounded(GenUI.LeftHalf(rect2));
			Rect val4 = GenUI.Rounded(GenUI.RightHalf(rect2));
			TextAnchor anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)3;
			Widgets.DrawHighlightIfMouseover(rect2);
			Widgets.Label(val3, Translator.Translate("SmartHopper_MinimumKeyword"));
			Text.Anchor = anchor;
			Widgets.TextFieldNumeric<int>(val4, ref inputMinimum.Value, ref minBufferString, 0f, 1E+09f);
			if (controlIdMinInput == null)
			{
				controlIdMinInput = "TextField" + ((Rect)(ref val4)).y.ToString("F0") + ((Rect)(ref val4)).x.ToString("F0");
			}
			((Listing)val).Gap(12f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("SmartHopper_Maximum_Label")), ref outputSettings.UseMax, TaggedString.op_Implicit(Translator.Translate(outputSettings.MaxTooltip)), 0f, 1f);
			((Listing)val).Gap(12f);
			Rect rect3 = ((Listing)val).GetRect(Text.LineHeight, 1f);
			Rect val5 = GenUI.Rounded(GenUI.LeftHalf(rect3));
			Rect val6 = GenUI.Rounded(GenUI.RightHalf(rect3));
			TextAnchor anchor2 = Text.Anchor;
			Text.Anchor = (TextAnchor)3;
			Widgets.DrawHighlightIfMouseover(rect3);
			Widgets.Label(val5, Translator.Translate("SmartHopper_MaximumKeyword"));
			Text.Anchor = anchor2;
			Widgets.TextFieldNumeric<int>(val6, ref inputMaximum.Value, ref maxBufferString, 0f, 1E+09f);
			if (controlIdMaxInput == null)
			{
				controlIdMaxInput = "TextField" + ((Rect)(ref val6)).y.ToString("F0") + ((Rect)(ref val6)).x.ToString("F0");
			}
			inputMaximum.Tick();
			inputMinimum.Tick();
			((Listing)val).End();
		}

		public override void PostClose()
		{
			inputMaximum.LostFocus();
			inputMinimum.LostFocus();
			((Window)this).PostClose();
			postClose?.Invoke();
		}
	}
	public class Dialog_RenameColdStorage : Dialog_Rename<Building_ColdStorage>
	{
		public Dialog_RenameColdStorage(Building_ColdStorage renaming)
			: base(renaming)
		{
			base.curName = ((IRenameable)renaming).RenamableLabel;
		}

		protected override void OnRenamed(string name)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			base.OnRenamed(name);
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFStorageBuildingGainsName", NamedArgument.op_Implicit(base.curName))), MessageTypeDefOf.TaskCompletion, true);
		}
	}
	public class Dialog_RenameMassStorageUnit : Dialog_Rename<Building_MassStorageUnit>
	{
		public Dialog_RenameMassStorageUnit(Building_MassStorageUnit renaming)
			: base(renaming)
		{
			base.curName = ((IRenameable)renaming).RenamableLabel;
		}

		protected override void OnRenamed(string name)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			base.OnRenamed(name);
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFStorageBuildingGainsName", NamedArgument.op_Implicit(base.curName))), MessageTypeDefOf.TaskCompletion, true);
		}
	}
	public class Dialog_RenameStorageUnitIOBase : Dialog_Rename<Building_StorageUnitIOBase>
	{
		public Dialog_RenameStorageUnitIOBase(Building_StorageUnitIOBase renaming)
			: base(renaming)
		{
			base.curName = ((IRenameable)renaming).RenamableLabel;
		}

		protected override void OnRenamed(string name)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			base.OnRenamed(name);
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFStorageBuildingGainsName", NamedArgument.op_Implicit(base.curName))), MessageTypeDefOf.TaskCompletion, true);
		}
	}
	public class ITab_IOPortStorage : ITab_Storage
	{
		private Building_StorageUnitIOBase SelBuilding => (Building_StorageUnitIOBase)(object)((ITab)this).SelThing;

		public override bool IsVisible
		{
			get
			{
				Building_StorageUnitIOBase selBuilding = SelBuilding;
				return selBuilding != null && selBuilding.Mode == StorageIOMode.Output;
			}
		}

		public ITab_IOPortStorage()
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).size = new Vector2(300f, 480f);
			((InspectTabBase)this).labelKey = "TabStorage";
		}
	}
	[StaticConstructorOnStartup]
	public class ITab_Items : ITab
	{
		private static readonly Texture2D MenuUI;

		private static readonly Texture2D DropUI;

		private Vector2 scrollPos;

		private float scrollViewHeight;

		private string oldSearchQuery;

		private string searchQuery;

		private List<Thing> itemsToShow;

		private static ILinkableStorageParent oldSelectedMassStorageUnit;

		private static readonly Dictionary<Thing, List<Pawn>> CanBeConsumedBy;

		private static List<Pawn> pawnCanReachTouchDeadly;

		private static List<Pawn> pawnCanReachOnCellDeadly;

		private static readonly Dictionary<Thing, int> ThingMaxHitPoints;

		private ILinkableStorageParent SelectedMassStorageUnit => (((ITab)this).SelThing is Building_StorageUnitIOBase building_StorageUnitIOBase) ? building_StorageUnitIOBase.BoundStorageUnit : ((ILinkableStorageParent)((ITab)this).SelThing);

		public override bool IsVisible
		{
			get
			{
				if (((ITab)this).SelThing is Building_StorageUnitIOBase building_StorageUnitIOBase)
				{
					return building_StorageUnitIOBase.BoundStorageUnit?.CanReceiveIO ?? false;
				}
				return ((ITab)this).SelThing is ILinkableStorageParent;
			}
		}

		static ITab_Items()
		{
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Expected O, but got Unknown
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Expected O, but got Unknown
			CanBeConsumedBy = new Dictionary<Thing, List<Pawn>>();
			pawnCanReachTouchDeadly = new List<Pawn>();
			pawnCanReachOnCellDeadly = new List<Pawn>();
			ThingMaxHitPoints = new Dictionary<Thing, int>();
			DropUI = (Texture2D)AccessTools.Field(AccessTools.TypeByName("Verse.TexButton"), "Drop").GetValue(null);
			MenuUI = (Texture2D)AccessTools.Field(AccessTools.TypeByName("Verse.TexButton"), "ToggleLog").GetValue(null);
		}

		public ITab_Items()
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).size = new Vector2(480f, 480f);
			((InspectTabBase)this).labelKey = "PRFItemsTab";
		}

		private static bool ItemIsVisible(float curY, float viewRectHeight, float scrollY, float rowHeight = 28f)
		{
			if (curY + rowHeight - scrollY < 0f)
			{
				return false;
			}
			if (curY - rowHeight - scrollY - viewRectHeight < 0f)
			{
				return true;
			}
			return false;
		}

		private static bool Search(string source, string target)
		{
			if (ProjectRimFactory_ModSettings.PRF_UseFuzzySearch)
			{
				return source.NormalizedFuzzyStrength(target) < 0.25;
			}
			return source.Contains(target);
		}

		protected override void FillTab()
		{
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_016f: Unknown result type (might be due to invalid IL or missing references)
			//IL_019c: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0205: Unknown result type (might be due to invalid IL or missing references)
			//IL_020d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0231: Unknown result type (might be due to invalid IL or missing references)
			//IL_0238: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_03be: Invalid comparison between Unknown and I4
			//IL_03df: Unknown result type (might be due to invalid IL or missing references)
			Text.Font = (GameFont)1;
			float num = 0f;
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(10f, 10f, ((InspectTabBase)this).size.x - 10f, ((InspectTabBase)this).size.y - 10f);
			GUI.BeginGroup(val);
			Widgets.ListSeparator(ref num, ((Rect)(ref val)).width, TaggedString.op_Implicit(Translator.Translate(((InspectTabBase)this).labelKey)));
			num += 5f;
			if (itemsToShow == null || searchQuery != oldSearchQuery || SelectedMassStorageUnit.StoredItemsCount != itemsToShow.Count || oldSelectedMassStorageUnit == null || oldSelectedMassStorageUnit != SelectedMassStorageUnit)
			{
				itemsToShow = new List<Thing>(from Thing t in SelectedMassStorageUnit.StoredItems
					where string.IsNullOrEmpty(searchQuery) || Search(((Entity)MinifyUtility.GetInnerIfMinified(t)).Label.ToLower(), searchQuery.ToLower())
					orderby ((Entity)t).Label descending
					select t);
				oldSearchQuery = searchQuery;
			}
			oldSelectedMassStorageUnit = SelectedMassStorageUnit;
			string iTabString = SelectedMassStorageUnit.GetITabString(itemsToShow.Count);
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(8f, num, ((Rect)(ref val)).width - 16f, Text.CalcHeight(iTabString, ((Rect)(ref val)).width - 16f));
			Widgets.Label(val2, iTabString);
			num += ((Rect)(ref val2)).height;
			searchQuery = Widgets.TextField(new Rect(((Rect)(ref val)).x, num, ((Rect)(ref val2)).width - 16f, 25f), oldSearchQuery);
			num += 28f;
			GUI.color = Color.white;
			Rect val3 = default(Rect);
			((Rect)(ref val3))..ctor(0f, num, ((Rect)(ref val)).width, ((Rect)(ref val)).height - num);
			Rect val4 = default(Rect);
			((Rect)(ref val4))..ctor(0f, 0f, ((Rect)(ref val3)).width - 16f, scrollViewHeight);
			Widgets.BeginScrollView(val3, ref scrollPos, val4, true);
			num = 0f;
			if (itemsToShow.Count < 1)
			{
				Widgets.Label(val4, Translator.Translate("PRFItemsTabLabel_Empty"));
				num += 22f;
			}
			List<Pawn> freeColonists = SelectedMassStorageUnit.Map.mapPawns.FreeColonists;
			pawnCanReachTouchDeadly = freeColonists.Where((Pawn p) => ReachabilityUtility.CanReach(p, SelectedMassStorageUnit.GetTargetInfo, (PathEndMode)3, (Danger)3, false, false, (TraverseMode)0)).ToList();
			pawnCanReachOnCellDeadly = freeColonists.Where((Pawn p) => ReachabilityUtility.CanReach(p, SelectedMassStorageUnit.GetTargetInfo, (PathEndMode)1, (Danger)3, false, false, (TraverseMode)0)).ToList();
			for (int num2 = itemsToShow.Count - 1; num2 >= 0; num2--)
			{
				if (!ItemIsVisible(num, ((Rect)(ref val3)).height, scrollPos.y))
				{
					num += 28f;
				}
				else
				{
					Thing thing = itemsToShow[num2];
					if (!CanBeConsumedBy.ContainsKey(thing))
					{
						CanBeConsumedBy.Add(thing, freeColonists.Where((Pawn p) => p.RaceProps.CanEverEat(thing)).ToList());
					}
					if (!ThingMaxHitPoints.ContainsKey(thing))
					{
						ThingMaxHitPoints.Add(thing, thing.MaxHitPoints);
					}
					DrawThingRow(ref num, ((Rect)(ref val4)).width, thing, freeColonists);
				}
			}
			if ((int)Event.current.type == 8)
			{
				scrollViewHeight = num + 30f;
			}
			Widgets.EndScrollView();
			GUI.EndGroup();
			GUI.color = Color.white;
			Text.Anchor = (TextAnchor)0;
		}

		private void DrawThingRow(ref float y, float width, Thing thing, List<Pawn> colonists)
		{
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0114: Unknown result type (might be due to invalid IL or missing references)
			//IL_011f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0126: Unknown result type (might be due to invalid IL or missing references)
			//IL_012b: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_035c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0390: Unknown result type (might be due to invalid IL or missing references)
			//IL_0332: Unknown result type (might be due to invalid IL or missing references)
			//IL_0403: Unknown result type (might be due to invalid IL or missing references)
			//IL_0407: Unknown result type (might be due to invalid IL or missing references)
			//IL_027b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0285: Expected O, but got Unknown
			//IL_02a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b0: Expected O, but got Unknown
			Corpse val = (Corpse)(object)((thing is Corpse) ? thing : null);
			string text = ((val == null) ? GenLabel.ThingLabel(MinifyUtility.GetInnerIfMinified(thing), thing.stackCount, false, true) : ((Entity)val).Label);
			string text2 = GenText.CapitalizeFirst(text, (Def)(object)thing.def);
			width -= 24f;
			Widgets.InfoCardButton(width, y, thing);
			width -= 24f;
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(width, y, 24f, 24f);
			bool flag = !ForbidUtility.IsForbidden(thing, Faction.OfPlayer);
			bool flag2 = flag;
			TooltipHandler.TipRegion(val2, TipSignal.op_Implicit(flag ? Translator.Translate("CommandNotForbiddenDesc") : Translator.Translate("CommandForbiddenDesc")));
			Widgets.Checkbox(((Rect)(ref val2)).x, ((Rect)(ref val2)).y, ref flag, 24f, false, false, (Texture2D)null, (Texture2D)null);
			if (flag != flag2)
			{
				ForbidUtility.SetForbidden(thing, !flag, false);
			}
			width -= 24f;
			Rect val3 = default(Rect);
			((Rect)(ref val3))..ctor(width, y, 24f, 24f);
			TooltipHandler.TipRegion(val3, TipSignal.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF_DropThing", NamedArgument.op_Implicit(text))));
			if (Widgets.ButtonImage(val3, DropUI, Color.gray, Color.white, false, (string)null))
			{
				DropThing(thing);
			}
			Pawn val4 = ((colonists != null) ? GenCollection.FirstOrFallback<Pawn>((IEnumerable<Pawn>)colonists.Where((Pawn col) => col.IsColonistPlayerControlled && !col.Dead && ((Thing)col).Spawned && !col.Downed).ToArray(), (Pawn)null) : null);
			if (val4 != null && ChoicesForThing(thing, val4, text).Count > 0)
			{
				width -= 24f;
				Rect val5 = default(Rect);
				((Rect)(ref val5))..ctor(width, y, 24f, 24f);
				if (Widgets.ButtonImage(val5, MenuUI, Color.gray, Color.white, false, (string)null))
				{
					List<FloatMenuOption> list = new List<FloatMenuOption>();
					foreach (Pawn item in from Pawn col in colonists
						where col.IsColonistPlayerControlled && !col.Dead && ((Thing)col).Spawned && !col.Downed
						select col)
					{
						List<FloatMenuOption> choices = ChoicesForThing(thing, item, text);
						if (choices.Count > 0)
						{
							list.Add(new FloatMenuOption(item.Name.ToStringFull, (Action)delegate
							{
								//IL_000c: Unknown result type (might be due to invalid IL or missing references)
								//IL_0016: Expected O, but got Unknown
								Find.WindowStack.Add((Window)new FloatMenu(choices));
							}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
						}
					}
					Find.WindowStack.Add((Window)new FloatMenu(list));
				}
			}
			Rect val6 = default(Rect);
			((Rect)(ref val6))..ctor(0f, y, width, 28f);
			if (Mouse.IsOver(val6))
			{
				GUI.color = ITab_Pawn_Gear.HighlightColor;
				GUI.DrawTexture(val6, (Texture)(object)TexUI.HighlightTex);
			}
			if ((Object)(object)((BuildableDef)thing.def).DrawMatSingle != (Object)null && (Object)(object)((BuildableDef)thing.def).DrawMatSingle.mainTexture != (Object)null)
			{
				Widgets.ThingIcon(new Rect(4f, y, 28f, 28f), thing, 1f, (Rot4?)null, false, 1f, false);
			}
			Text.Anchor = (TextAnchor)3;
			GUI.color = ITab_Pawn_Gear.ThingLabelColor;
			Rect val7 = default(Rect);
			((Rect)(ref val7))..ctor(36f, y, ((Rect)(ref val6)).width - 36f, ((Rect)(ref val6)).height);
			Text.WordWrap = false;
			Widgets.Label(val7, GenText.Truncate(text2, ((Rect)(ref val7)).width, (Dictionary<string, string>)null));
			Text.WordWrap = true;
			string text3 = text2;
			if (thing.def.useHitPoints)
			{
				text3 = text2 + "\n" + thing.HitPoints + " / " + ThingMaxHitPoints[thing];
			}
			TooltipHandler.TipRegion(val6, TipSignal.op_Implicit(text3));
			y += 28f;
		}

		private void DropThing(Thing thing)
		{
			List<Thing> list = SelectedMassStorageUnit.StoredItems.Where((Thing i) => i == thing).ToList();
			if (((ITab)this).SelThing is Building_StorageUnitIOBase building_StorageUnitIOBase && building_StorageUnitIOBase.OutputItem(list[0]))
			{
				itemsToShow.Remove(thing);
			}
			else if (SelectedMassStorageUnit.OutputItem(list[0]))
			{
				itemsToShow.Remove(thing);
			}
		}

		public static List<FloatMenuOption> ChoicesForThing(Thing thing, Pawn pawn, string thingLabelShort)
		{
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_0148: Unknown result type (might be due to invalid IL or missing references)
			//IL_014d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0157: Unknown result type (might be due to invalid IL or missing references)
			//IL_05f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0603: Unknown result type (might be due to invalid IL or missing references)
			//IL_0608: Unknown result type (might be due to invalid IL or missing references)
			//IL_0612: Unknown result type (might be due to invalid IL or missing references)
			//IL_061c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0621: Unknown result type (might be due to invalid IL or missing references)
			//IL_062b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0642: Unknown result type (might be due to invalid IL or missing references)
			//IL_0649: Expected O, but got Unknown
			//IL_06cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_06d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_06dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_06fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_070f: Unknown result type (might be due to invalid IL or missing references)
			//IL_071f: Expected O, but got Unknown
			//IL_0673: Unknown result type (might be due to invalid IL or missing references)
			//IL_067e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0683: Unknown result type (might be due to invalid IL or missing references)
			//IL_068d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0697: Unknown result type (might be due to invalid IL or missing references)
			//IL_069c: Unknown result type (might be due to invalid IL or missing references)
			//IL_06a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_06bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_06c4: Expected O, but got Unknown
			//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bf: Expected O, but got Unknown
			//IL_0340: Unknown result type (might be due to invalid IL or missing references)
			//IL_0345: Unknown result type (might be due to invalid IL or missing references)
			//IL_034f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0364: Unknown result type (might be due to invalid IL or missing references)
			//IL_036f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0374: Unknown result type (might be due to invalid IL or missing references)
			//IL_0379: Unknown result type (might be due to invalid IL or missing references)
			//IL_0383: Unknown result type (might be due to invalid IL or missing references)
			//IL_039a: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a1: Expected O, but got Unknown
			//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0216: Unknown result type (might be due to invalid IL or missing references)
			//IL_021d: Expected O, but got Unknown
			//IL_03c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_03cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_03d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_03ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_0406: Unknown result type (might be due to invalid IL or missing references)
			//IL_040d: Expected O, but got Unknown
			//IL_043d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0442: Unknown result type (might be due to invalid IL or missing references)
			//IL_044c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0456: Unknown result type (might be due to invalid IL or missing references)
			//IL_045b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0465: Unknown result type (might be due to invalid IL or missing references)
			//IL_047c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0483: Expected O, but got Unknown
			//IL_0232: Unknown result type (might be due to invalid IL or missing references)
			//IL_0242: Unknown result type (might be due to invalid IL or missing references)
			//IL_024f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0260: Unknown result type (might be due to invalid IL or missing references)
			//IL_0270: Expected O, but got Unknown
			//IL_04f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_04fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_04b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_04bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_04cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_04e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_04eb: Expected O, but got Unknown
			//IL_07e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_07f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_07f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0803: Unknown result type (might be due to invalid IL or missing references)
			//IL_080d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0812: Unknown result type (might be due to invalid IL or missing references)
			//IL_081c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0833: Unknown result type (might be due to invalid IL or missing references)
			//IL_083d: Expected O, but got Unknown
			//IL_08f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_08ff: Expected O, but got Unknown
			//IL_086b: Unknown result type (might be due to invalid IL or missing references)
			//IL_087c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0881: Unknown result type (might be due to invalid IL or missing references)
			//IL_088b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0895: Unknown result type (might be due to invalid IL or missing references)
			//IL_089a: Unknown result type (might be due to invalid IL or missing references)
			//IL_08a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_08bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_08c5: Expected O, but got Unknown
			//IL_095e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0974: Unknown result type (might be due to invalid IL or missing references)
			//IL_0979: Unknown result type (might be due to invalid IL or missing references)
			//IL_09a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_09cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_09df: Expected O, but got Unknown
			//IL_0587: Unknown result type (might be due to invalid IL or missing references)
			//IL_0598: Unknown result type (might be due to invalid IL or missing references)
			//IL_05a8: Expected O, but got Unknown
			//IL_055c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0561: Unknown result type (might be due to invalid IL or missing references)
			//IL_0adf: Unknown result type (might be due to invalid IL or missing references)
			//IL_0af5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0afa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b29: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b50: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b60: Expected O, but got Unknown
			//IL_0a32: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a48: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a4d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a57: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a61: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a66: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a70: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a87: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a91: Expected O, but got Unknown
			//IL_0b7e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b94: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b99: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bbc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0be3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bf3: Expected O, but got Unknown
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			Thing t = thing;
			string text = string.Empty;
			string text2 = thingLabelShort;
			if (thing.stackCount > 1)
			{
				text2 = text2 + " x" + GenString.ToStringCached(thing.stackCount);
			}
			if (t.def.ingestible != null && CanBeConsumedBy[t].Contains(pawn) && t.IngestibleNow)
			{
				string text3 = ((!GenText.NullOrEmpty(t.def.ingestible.ingestCommandString)) ? string.Format(t.def.ingestible.ingestCommandString, thingLabelShort) : TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("ConsumeThing", NamedArgument.op_Implicit(thingLabelShort), NamedArgument.op_Implicit(t))));
				if (!SocialProperness.IsSociallyProper(t, pawn))
				{
					text3 = TaggedString.op_Implicit(text3 + " (" + Translator.Translate("ReservedForPrisoners") + ")");
				}
				FloatMenuOption item;
				if (t.def.IsNonMedicalDrug && PawnUtility.IsTeetotaler(pawn))
				{
					item = new FloatMenuOption(text3 + " (" + TraitDefOf.DrugDesire.DataAtDegree(-1).label + ")", (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
				}
				else if (!pawnCanReachOnCellDeadly.Contains(pawn))
				{
					item = new FloatMenuOption(TaggedString.op_Implicit(text3 + " (" + Translator.Translate("NoPath") + ")"), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
				}
				else
				{
					MenuOptionPriority val = (MenuOptionPriority)((!(t is Corpse)) ? 4 : 3);
					item = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text3, (Action)delegate
					{
						//IL_001a: Unknown result type (might be due to invalid IL or missing references)
						//IL_001f: Unknown result type (might be due to invalid IL or missing references)
						//IL_0024: Unknown result type (might be due to invalid IL or missing references)
						//IL_0053: Expected O, but got Unknown
						ForbidUtility.SetForbidden(t, false, true);
						Job val3 = new Job(JobDefOf.Ingest, LocalTargetInfo.op_Implicit(t))
						{
							count = FoodUtility.WillIngestStackCountOf(pawn, t.def, StatExtension.GetStatValue(t, StatDefOf.Nutrition, true, -1))
						};
						pawn.jobs.TryTakeOrderedJob(val3, (JobTag?)(JobTag)0, false);
					}, val, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0), pawn, LocalTargetInfo.op_Implicit(t), "ReservedBy", (ReservationLayerDef)null);
				}
				list.Add(item);
			}
			Thing obj = thing;
			ThingWithComps equipment = (ThingWithComps)(object)((obj is ThingWithComps) ? obj : null);
			if (equipment != null && equipment.GetComp<CompEquippable>() != null)
			{
				text = thingLabelShort;
				if (equipment.AllComps != null)
				{
					int num = 0;
					for (int count = equipment.AllComps.Count; num < count; num++)
					{
						text = equipment.AllComps[num].TransformLabel(text);
					}
				}
				FloatMenuOption item2;
				string text4 = default(string);
				if (((Thing)equipment).def.IsWeapon && pawn.WorkTagIsDisabled((WorkTags)8))
				{
					item2 = new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CannotEquip", NamedArgument.op_Implicit(text)) + " (" + TranslatorFormattedStringExtensions.Translate("IsIncapableOfViolenceLower", NamedArgument.op_Implicit(((Entity)pawn).LabelShort), NamedArgument.op_Implicit((Thing)(object)pawn)) + ")"), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
				}
				else if (!pawnCanReachTouchDeadly.Contains(pawn))
				{
					item2 = new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CannotEquip", NamedArgument.op_Implicit(text)) + " (" + Translator.Translate("NoPath") + ")"), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
				}
				else if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
				{
					item2 = new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CannotEquip", NamedArgument.op_Implicit(text)) + " (" + Translator.Translate("Incapable") + ")"), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
				}
				else if (!EquipmentUtility.CanEquip(thing, pawn, ref text4, true))
				{
					item2 = new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CannotEquip", NamedArgument.op_Implicit(text)) + " (" + text4 + ")"), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
				}
				else
				{
					string text5 = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("Equip", NamedArgument.op_Implicit(text)));
					if (((Thing)equipment).def.IsRangedWeapon && pawn.story != null && pawn.story.traits.HasTrait(TraitDefOf.Brawler))
					{
						text5 = TaggedString.op_Implicit(text5 + " " + Translator.Translate("EquipWarningBrawler"));
					}
					item2 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text5, (Action)delegate
					{
						//IL_0025: Unknown result type (might be due to invalid IL or missing references)
						//IL_002a: Unknown result type (might be due to invalid IL or missing references)
						//IL_003b: Expected O, but got Unknown
						//IL_0042: Unknown result type (might be due to invalid IL or missing references)
						ForbidUtility.SetForbidden((Thing)(object)equipment, false, true);
						pawn.jobs.TryTakeOrderedJob(new Job(JobDefOf.Equip, LocalTargetInfo.op_Implicit((Thing)(object)equipment)), (JobTag?)(JobTag)0, false);
						FleckMaker.Static(((Thing)equipment).DrawPos, ((Thing)equipment).Map, FleckDefOf.FeedbackEquip, 1f);
						PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.EquippingWeapons, (KnowledgeAmount)6);
					}, (MenuOptionPriority)5, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0), pawn, LocalTargetInfo.op_Implicit((Thing)(object)equipment), "ReservedBy", (ReservationLayerDef)null);
				}
				list.Add(item2);
			}
			Thing obj2 = thing;
			Apparel apparel = (Apparel)(object)((obj2 is Apparel) ? obj2 : null);
			if (apparel != null)
			{
				FloatMenuOption item3 = (FloatMenuOption)((!pawnCanReachTouchDeadly.Contains(pawn)) ? ((object)new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CannotWear", NamedArgument.op_Implicit(text2), NamedArgument.op_Implicit((Thing)(object)apparel)) + " (" + Translator.Translate("NoPath") + ")"), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)) : (ApparelUtility.HasPartsToWear(pawn, ((Thing)apparel).def) ? ((object)FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("ForceWear", NamedArgument.op_Implicit(text), NamedArgument.op_Implicit((Thing)(object)apparel))), (Action)delegate
				{
					//IL_001a: Unknown result type (might be due to invalid IL or missing references)
					//IL_001f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0025: Expected O, but got Unknown
					ForbidUtility.SetForbidden((Thing)(object)apparel, false, true);
					Job val3 = new Job(JobDefOf.Wear, LocalTargetInfo.op_Implicit((Thing)(object)apparel));
					pawn.jobs.TryTakeOrderedJob(val3, (JobTag?)(JobTag)0, false);
				}, (MenuOptionPriority)5, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0), pawn, LocalTargetInfo.op_Implicit((Thing)(object)apparel), "ReservedBy", (ReservationLayerDef)null)) : ((object)new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CannotWear", NamedArgument.op_Implicit(text2), NamedArgument.op_Implicit((Thing)(object)apparel)) + " (" + Translator.Translate("CannotWearBecauseOfMissingBodyParts") + ")"), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0))));
				list.Add(item3);
			}
			if (!CaravanFormingUtility.IsFormingCaravan(pawn) || !thing.def.EverHaulable)
			{
				return list;
			}
			Pawn packTarget = GiveToPackAnimalUtility.UsablePackAnimalWithTheMostFreeSpace(pawn) ?? pawn;
			JobDef jobDef = ((packTarget != pawn) ? JobDefOf.GiveToPackAnimal : JobDefOf.TakeInventory);
			if (!pawnCanReachTouchDeadly.Contains(pawn))
			{
				list.Add(new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CannotLoadIntoCaravan", NamedArgument.op_Implicit(text2), NamedArgument.op_Implicit(thing)) + " (" + Translator.Translate("NoPath") + ")"), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
			else if (MassUtility.WillBeOverEncumberedAfterPickingUp(packTarget, thing, 1))
			{
				list.Add(new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CannotLoadIntoCaravan", NamedArgument.op_Implicit(text2), NamedArgument.op_Implicit(thing)) + " (" + Translator.Translate("TooHeavy") + ")"), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
			else
			{
				LordJob_FormAndSendCaravan val2 = (LordJob_FormAndSendCaravan)LordUtility.GetLord(pawn).LordJob;
				float capacityLeft = CaravanFormingUtility.CapacityLeft(val2);
				if (thing.stackCount == 1)
				{
					float num2 = capacityLeft - StatExtension.GetStatValue(thing, StatDefOf.Mass, true, -1);
					list.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(CaravanFormingUtility.AppendOverweightInfo(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("LoadIntoCaravan", NamedArgument.op_Implicit(text2), NamedArgument.op_Implicit(thing))), num2), (Action)delegate
					{
						//IL_0025: Unknown result type (might be due to invalid IL or missing references)
						//IL_002a: Unknown result type (might be due to invalid IL or missing references)
						//IL_002f: Unknown result type (might be due to invalid IL or missing references)
						//IL_0036: Unknown result type (might be due to invalid IL or missing references)
						//IL_0050: Expected O, but got Unknown
						ForbidUtility.SetForbidden(thing, false, false);
						Job val3 = new Job(jobDef, LocalTargetInfo.op_Implicit(thing))
						{
							count = 1,
							checkEncumbrance = (packTarget == pawn)
						};
						pawn.jobs.TryTakeOrderedJob(val3, (JobTag?)(JobTag)0, false);
					}, (MenuOptionPriority)5, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0), pawn, LocalTargetInfo.op_Implicit(thing), "ReservedBy", (ReservationLayerDef)null));
				}
				else
				{
					if (MassUtility.WillBeOverEncumberedAfterPickingUp(packTarget, thing, thing.stackCount))
					{
						list.Add(new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("CannotLoadIntoCaravanAll", NamedArgument.op_Implicit(text2), NamedArgument.op_Implicit(thing)) + " (" + Translator.Translate("TooHeavy") + ")"), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					}
					else
					{
						float num3 = capacityLeft - (float)thing.stackCount * StatExtension.GetStatValue(thing, StatDefOf.Mass, true, -1);
						list.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(CaravanFormingUtility.AppendOverweightInfo(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("LoadIntoCaravanAll", NamedArgument.op_Implicit(text2), NamedArgument.op_Implicit(thing))), num3), (Action)delegate
						{
							//IL_0025: Unknown result type (might be due to invalid IL or missing references)
							//IL_002a: Unknown result type (might be due to invalid IL or missing references)
							//IL_0030: Expected O, but got Unknown
							ForbidUtility.SetForbidden(thing, false, false);
							Job val3 = new Job(jobDef, LocalTargetInfo.op_Implicit(thing));
							val3.count = thing.stackCount;
							val3.checkEncumbrance = packTarget == pawn;
							pawn.jobs.TryTakeOrderedJob(val3, (JobTag?)(JobTag)0, false);
						}, (MenuOptionPriority)5, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0), pawn, LocalTargetInfo.op_Implicit(thing), "ReservedBy", (ReservationLayerDef)null));
					}
					list.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("LoadIntoCaravanSome", NamedArgument.op_Implicit(thingLabelShort), NamedArgument.op_Implicit(thing))), (Action)delegate
					{
						//IL_0065: Unknown result type (might be due to invalid IL or missing references)
						//IL_006b: Expected O, but got Unknown
						int num4 = Mathf.Min(MassUtility.CountToPickUpUntilOverEncumbered(packTarget, thing), thing.stackCount);
						Dialog_Slider val3 = new Dialog_Slider((Func<int, string>)delegate(int num6)
						{
							//IL_003d: Unknown result type (might be due to invalid IL or missing references)
							//IL_0052: Unknown result type (might be due to invalid IL or missing references)
							//IL_0057: Unknown result type (might be due to invalid IL or missing references)
							float num5 = capacityLeft - (float)num6 * StatExtension.GetStatValue(thing, StatDefOf.Mass, true, -1);
							return CaravanFormingUtility.AppendOverweightInfo(string.Format(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("LoadIntoCaravanCount", NamedArgument.op_Implicit(thingLabelShort), NamedArgument.op_Implicit(thing))), num6), num5);
						}, 1, num4, (Action<int>)delegate(int count2)
						{
							//IL_0025: Unknown result type (might be due to invalid IL or missing references)
							//IL_002a: Unknown result type (might be due to invalid IL or missing references)
							//IL_002f: Unknown result type (might be due to invalid IL or missing references)
							//IL_0036: Unknown result type (might be due to invalid IL or missing references)
							//IL_0050: Expected O, but got Unknown
							ForbidUtility.SetForbidden(thing, false, false);
							Job val4 = new Job(jobDef, LocalTargetInfo.op_Implicit(thing))
							{
								count = count2,
								checkEncumbrance = (packTarget == pawn)
							};
							pawn.jobs.TryTakeOrderedJob(val4, (JobTag?)(JobTag)0, false);
						}, int.MinValue, 1f);
						Find.WindowStack.Add((Window)(object)val3);
					}, (MenuOptionPriority)5, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0), pawn, LocalTargetInfo.op_Implicit(thing), "ReservedBy", (ReservationLayerDef)null));
				}
			}
			return list;
		}

		public override void OnOpen()
		{
			((InspectTabBase)this).OnOpen();
			CanBeConsumedBy.Clear();
			pawnCanReachTouchDeadly.Clear();
			pawnCanReachOnCellDeadly.Clear();
			ThingMaxHitPoints.Clear();
		}
	}
}
namespace ProjectRimFactory.Storage.Editables
{
	public class DefModExtension_CanUseStorageIOPorts : DefModExtension
	{
	}
	public class DefModExtension_Crate : DefModExtension
	{
		public int limit = 10;

		public bool destroyContainsItems = false;

		public bool hideItems = false;

		public bool forbidPawnAccess = false;

		public bool hideRightClickMenus = false;
	}
}
namespace ProjectRimFactory.SAL3.Tools
{
	public class ColorGenerator_RangeHSV : ColorGenerator
	{
		public FloatRange rangeH;

		public FloatRange rangeS;

		public FloatRange rangeV;

		public override Color NewRandomizedColor()
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			return Color.HSVToRGB(((FloatRange)(ref rangeH)).RandomInRange, ((FloatRange)(ref rangeS)).RandomInRange, ((FloatRange)(ref rangeV)).RandomInRange);
		}
	}
	internal static class ProjectSal_Utilities
	{
		public static Thing CalculateDominantIngredient(RecipeDef recipeDef, List<Thing> ingredients)
		{
			List<Thing> list = ingredients.Where((Thing thing) => thing.def.IsStuff).ToList();
			if (GenList.NullOrEmpty<Thing>((IList<Thing>)ingredients) || !GenCollection.Any<Thing>(list))
			{
				return ThingMaker.MakeThing(ThingDefOf.Steel, (ThingDef)null);
			}
			if (recipeDef.productHasIngredientStuff)
			{
				return list[0];
			}
			if (GenCollection.Any<ThingDefCountClass>(recipeDef.products, (Predicate<ThingDefCountClass>)((ThingDefCountClass thingDefCountClass) => ((BuildableDef)thingDefCountClass.thingDef).MadeFromStuff)))
			{
				return GenCollection.RandomElementByWeight<Thing>(list.Where((Thing thing) => thing.def.IsStuff), (Func<Thing, float>)((Thing thing) => thing.stackCount));
			}
			return GenCollection.RandomElementByWeight<Thing>((IEnumerable<Thing>)list, (Func<Thing, float>)((Thing thing) => thing.stackCount));
		}
	}
	public static class ReflectionUtility
	{
		public static readonly FieldInfo MapIndexOrState = typeof(Thing).GetField("mapIndexOrState", BindingFlags.Instance | BindingFlags.NonPublic);

		public static readonly FieldInfo CachedTotallyDisabled = typeof(SkillRecord).GetField("cachedTotallyDisabled", BindingFlags.Instance | BindingFlags.NonPublic);

		public static readonly MethodInfo TryFindBestBillIngredientsInSet = typeof(WorkGiver_DoBill).GetMethod("TryFindBestBillIngredientsInSet", BindingFlags.Static | BindingFlags.NonPublic);

		public static readonly FieldInfo DrillPortionProgress = typeof(CompDeepDrill).GetField("portionProgress", BindingFlags.Instance | BindingFlags.NonPublic);

		public static readonly FieldInfo DrillPortionYieldPct = typeof(CompDeepDrill).GetField("portionYieldPct", BindingFlags.Instance | BindingFlags.NonPublic);

		public static readonly FieldInfo DrillLastUsedTick = typeof(CompDeepDrill).GetField("lastUsedTick", BindingFlags.Instance | BindingFlags.NonPublic);

		public static readonly MethodInfo DrillTryProducePortion = typeof(CompDeepDrill).GetMethod("TryProducePortion", BindingFlags.Instance | BindingFlags.NonPublic);

		public static readonly FieldInfo BackCompatibilityConversionChain = typeof(BackCompatibility).GetField("conversionChain", BindingFlags.Static | BindingFlags.NonPublic);

		public static readonly FieldInfo ResearchManagerProgress = typeof(ResearchManager).GetField("progress", BindingFlags.Instance | BindingFlags.NonPublic);

		public static readonly FieldInfo AllRecipesCached = typeof(ThingDef).GetField("allRecipesCached", BindingFlags.Instance | BindingFlags.NonPublic);

		public static readonly FieldInfo SalReservations = typeof(ReservationManager).GetField("reservations", BindingFlags.Instance | BindingFlags.NonPublic);

		public static readonly FieldInfo CompPropertiesPowerBasePowerConsumption = typeof(CompProperties_Power).GetField("basePowerConsumption", BindingFlags.Instance | BindingFlags.NonPublic);

		public static readonly FieldInfo StockGeneratorSingleDefThingDef = typeof(StockGenerator_SingleDef).GetField("thingDef", BindingFlags.Instance | BindingFlags.NonPublic);
	}
	[StaticConstructorOnStartup]
	public static class Textures
	{
		public static readonly Texture2D Paste = ContentFinder<Texture2D>.Get("UI/Buttons/Paste", true);
	}
}
namespace ProjectRimFactory.SAL3.Things
{
	public class Building_RecipeHolder : Building, IRecipeHolderInterface
	{
		private RecipeDef workingRecipe;

		private float workAmount;

		public List<RecipeDef> Recipes = new List<RecipeDef>();

		private readonly List<IRecipeSubscriber> recipeSubscribers = new List<IRecipeSubscriber>();

		private List<RecipeDef> queuedRecipes;

		private IEnumerable<Building_WorkTable> Tables => from <>h__TransparentIdentifier1 in (from IntVec3 cell in ((ThingWithComps)this).GetComp<CompRecipeImportRange>()?.RangeCells() ?? GenAdj.CellsAdjacent8Way((Thing)(object)this)
				where GenGrid.InBounds(cell, ((Thing)this).Map)
				from t in GridsUtility.GetThingList(cell, ((Thing)this).Map).Cast<Thing>()
				select new { cell, t }).Select(<>h__TransparentIdentifier0 =>
			{
				Thing t = <>h__TransparentIdentifier0.t;
				return new
				{
					<>h__TransparentIdentifier0 = <>h__TransparentIdentifier0,
					building = (Building_WorkTable)(object)((t is Building_WorkTable) ? t : null)
				};
			})
			where <>h__TransparentIdentifier1.building != null
			select <>h__TransparentIdentifier1.building;

		public List<RecipeDef> QueuedRecipes
		{
			get
			{
				return queuedRecipes;
			}
			set
			{
				queuedRecipes = value;
			}
		}

		public List<RecipeDef> LearnableRecipes => GetAllProvidedRecipeDefs().ToList();

		public float ProgressLearning => workAmount;

		RecipeDef IRecipeHolderInterface.RecipeLearning
		{
			get
			{
				return workingRecipe;
			}
			set
			{
				workingRecipe = value;
			}
		}

		List<RecipeDef> IRecipeHolderInterface.SavedRecipes
		{
			get
			{
				return Recipes;
			}
			set
			{
				Recipes = value;
			}
		}

		public void RegisterRecipeSubscriber(IRecipeSubscriber subscriber)
		{
			recipeSubscribers.Add(subscriber);
			subscriber.RecipesChanged(this);
		}

		public void DeregisterRecipeSubscriber(IRecipeSubscriber subscriber)
		{
			recipeSubscribers.Remove(subscriber);
			subscriber.RecipesChanged(this);
		}

		private static bool IsBill_Mech(RecipeDef recipeDef)
		{
			return recipeDef.mechResurrection || recipeDef.gestationCycles > 0;
		}

		protected virtual IEnumerable<RecipeDef> GetAllProvidedRecipeDefs()
		{
			HashSet<RecipeDef> hashSet = new HashSet<RecipeDef>();
			foreach (Building_WorkTable table in Tables)
			{
				foreach (RecipeDef allRecipe in ((Thing)table).def.AllRecipes)
				{
					if (allRecipe.AvailableNow && !Recipes.Contains(allRecipe) && !hashSet.Contains(allRecipe) && !IsBill_Mech(allRecipe))
					{
						hashSet.Add(allRecipe);
					}
				}
			}
			return hashSet;
		}

		protected virtual float GetLearnRecipeWorkAmount(RecipeDef recipe)
		{
			return recipe.WorkAmountForStuff(ThingDefOf.Steel);
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			if (Prefs.DevMode)
			{
				yield return (Gizmo)new Command_Action
				{
					defaultLabel = "Debug actions",
					action = delegate
					{
						//IL_000b: Unknown result type (might be due to invalid IL or missing references)
						//IL_0015: Expected O, but got Unknown
						Find.WindowStack.Add((Window)new FloatMenu(GetDebugOptions()));
					}
				};
			}
		}

		private List<FloatMenuOption> GetDebugOptions()
		{
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Expected O, but got Unknown
			return new List<FloatMenuOption>(1)
			{
				new FloatMenuOption("Insta-finish", (Action)delegate
				{
					workAmount = 0f;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			};
		}

		private void ResetProgress()
		{
			workAmount = 0f;
			workingRecipe = null;
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			ResetProgress();
			((Building)this).DeSpawn(mode);
			foreach (IRecipeSubscriber recipeSubscriber in recipeSubscribers)
			{
				recipeSubscriber.RecipeProviderRemoved(this);
			}
		}

		protected override void Tick()
		{
			if (!((Thing)this).Spawned)
			{
				return;
			}
			if (Gen.IsHashIntervalTick((Thing)(object)this, 60))
			{
				CompPowerTrader comp = ((ThingWithComps)this).GetComp<CompPowerTrader>();
				if (comp == null || comp.PowerOn)
				{
					if (workingRecipe != null)
					{
						workAmount -= 60f;
						if (workAmount < 0f)
						{
							Recipes.Add(workingRecipe);
							foreach (IRecipeSubscriber recipeSubscriber in recipeSubscribers)
							{
								recipeSubscriber.RecipesChanged(this);
							}
							ResetProgress();
						}
					}
					else if (QueuedRecipes.Count >= 1)
					{
						workingRecipe = QueuedRecipes[0];
						workAmount = GetLearnRecipeWorkAmount(workingRecipe);
						QueuedRecipes.RemoveAt(0);
					}
				}
			}
			((ThingWithComps)this).Tick();
		}

		public override void ExposeData()
		{
			((Building)this).ExposeData();
			Scribe_Defs.Look<RecipeDef>(ref workingRecipe, "workingRecipe");
			Scribe_Collections.Look<RecipeDef>(ref Recipes, "recipes", (LookMode)4, Array.Empty<object>());
			Scribe_Values.Look<float>(ref workAmount, "workAmount", 0f, false);
			Scribe_Collections.Look<RecipeDef>(ref queuedRecipes, "quered_recipes", (LookMode)0, Array.Empty<object>());
			if (queuedRecipes == null)
			{
				queuedRecipes = new List<RecipeDef>();
			}
		}

		public override string GetInspectString()
		{
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			string inspectString = ((ThingWithComps)this).GetInspectString();
			if (inspectString.Length > 0)
			{
				stringBuilder.AppendLine(inspectString);
			}
			if (workingRecipe != null)
			{
				stringBuilder.AppendLine(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SALInspect_RecipeReport", NamedArgument.op_Implicit(((Def)workingRecipe).label), NamedArgument.op_Implicit(GenText.ToStringWorkAmount(workAmount)))));
			}
			stringBuilder.AppendLine(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SAL3_StoredRecipes", NamedArgument.op_Implicit(string.Join(", ", Recipes.Select((RecipeDef r) => ((Def)r).label).ToArray())))));
			return GenText.TrimEndNewlines(stringBuilder.ToString());
		}

		public override void PostMake()
		{
			((ThingWithComps)this).PostMake();
			if (queuedRecipes == null)
			{
				queuedRecipes = new List<RecipeDef>();
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			Recipes.RemoveAll((RecipeDef recipeDef) => recipeDef == null);
			queuedRecipes.RemoveAll((RecipeDef recipeDef) => recipeDef == null);
			((Thing)this).Map.GetComponent<PRFMapComponent>().NotifyRecipeSubscriberOfProvider(((Thing)this).Position, this);
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
	public class Building_SmartHopper : Building, IStoreSettingsParent, IPowerSupplyMachineHolder, IPickupSettings
	{
		private OutputSettings outputSettings;

		private readonly List<IntVec3> cachedDetectorCells = new List<IntVec3>();

		private bool cachedDetectorCellsDirty = true;

		private StorageSettings settings;

		private CompPowerWorkSetting compPowerWorkSetting;

		private bool allowGroundPickup;

		private bool allowStockpilePickup;

		private bool allowStoragePickup;

		private bool allowForbiddenPickup;

		private bool allowBeltPickup;

		private Thing StoredThing => GridsUtility.GetFirstItem(((Thing)this).Position, ((Thing)this).Map);

		public IPowerSupplyMachine RangePowerSupplyMachine => compPowerWorkSetting ?? ((ThingWithComps)this).GetComp<CompPowerWorkSetting>();

		private IEnumerable<IntVec3> CellsToTarget
		{
			get
			{
				//IL_0018: Unknown result type (might be due to invalid IL or missing references)
				IEnumerable<IntVec3> source = compPowerWorkSetting?.GetRangeCells() ?? GenRadial.RadialCellsAround(((Thing)this).Position, ((BuildableDef)((Thing)this).def).specialDisplayRadius, false);
				return source.Where((IntVec3 c) => c.GetFirst<Building_SmartHopper>(((Thing)this).Map) == null);
			}
		}

		private List<IntVec3> CellsToSelect
		{
			get
			{
				//IL_0053: Unknown result type (might be due to invalid IL or missing references)
				//IL_0058: Unknown result type (might be due to invalid IL or missing references)
				//IL_005a: Unknown result type (might be due to invalid IL or missing references)
				//IL_007a: Unknown result type (might be due to invalid IL or missing references)
				//IL_008b: Unknown result type (might be due to invalid IL or missing references)
				//IL_009c: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
				//IL_00db: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
				//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
				if (Find.TickManager.TicksGame % 50 != 0 && !cachedDetectorCellsDirty)
				{
					return cachedDetectorCells;
				}
				cachedDetectorCellsDirty = false;
				cachedDetectorCells.Clear();
				foreach (IntVec3 item in CellsToTarget)
				{
					if (GenGrid.InBounds(item, ((Thing)this).Map) && ((allowStockpilePickup && cellIsZone_Stockpile(item)) || (allowStoragePickup && cellIsBuilding_Storage(item)) || (allowBeltPickup && cellIsBuilding_BeltConveyor(item)) || (allowGroundPickup && !cellIsZone_Stockpile(item) && !cellIsBuilding_Storage(item) && !cellIsBuilding_BeltConveyor(item))))
					{
						cachedDetectorCells.Add(item);
					}
				}
				return cachedDetectorCells;
			}
		}

		private IEnumerable<Thing> ThingsToSelect
		{
			get
			{
				foreach (IntVec3 cell in CellsToSelect)
				{
					foreach (Thing t in cell.AllThingsInCellForUse(((Thing)this).Map, AllowStockpilePickup))
					{
						if (allowForbiddenPickup || !ForbidUtility.IsForbidden(t, Faction.OfPlayer))
						{
							yield return t;
						}
					}
				}
			}
		}

		public bool StorageTabVisible => true;

		private OutputSettings OutputSettings => outputSettings ?? (outputSettings = new OutputSettings("SmartHopper_Minimum_UseTooltip", "SmartHopper_Maximum_UseTooltip"));

		public bool AllowGroundPickup
		{
			get
			{
				return allowGroundPickup;
			}
			set
			{
				if (value != allowGroundPickup)
				{
					cachedDetectorCellsDirty = true;
				}
				allowGroundPickup = value;
			}
		}

		public bool AllowStockpilePickup
		{
			get
			{
				return allowStockpilePickup;
			}
			set
			{
				if (value != allowStockpilePickup)
				{
					cachedDetectorCellsDirty = true;
				}
				allowStockpilePickup = value;
			}
		}

		public bool AllowStoragePickup
		{
			get
			{
				return allowStoragePickup;
			}
			set
			{
				if (value != allowStoragePickup)
				{
					cachedDetectorCellsDirty = true;
				}
				allowStoragePickup = value;
			}
		}

		public bool AllowForbiddenPickup
		{
			get
			{
				return allowForbiddenPickup;
			}
			set
			{
				allowForbiddenPickup = value;
			}
		}

		public bool AllowBeltPickup
		{
			get
			{
				return allowBeltPickup;
			}
			set
			{
				if (value != allowBeltPickup)
				{
					cachedDetectorCellsDirty = true;
				}
				allowBeltPickup = value;
			}
		}

		private bool cellIsZone_Stockpile(IntVec3 cell)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return GridsUtility.GetZone(cell, ((Thing)this).Map) is Zone_Stockpile;
		}

		private bool cellIsBuilding_Storage(IntVec3 cell)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return GenCollection.FirstOrDefault<Thing>(GridsUtility.GetThingList(cell, ((Thing)this).Map), (Predicate<Thing>)((Thing t) => t is ISlotGroupParent)) is Building_Storage;
		}

		private bool cellIsBuilding_BeltConveyor(IntVec3 cell)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return GenCollection.Any<Thing>(GridsUtility.GetThingList(cell, ((Thing)this).Map), (Predicate<Thing>)((Thing t) => t is Building_BeltConveyor));
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Expected O, but got Unknown
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			compPowerWorkSetting = ((ThingWithComps)this).GetComp<CompPowerWorkSetting>();
			if (settings == null)
			{
				settings = new StorageSettings();
				settings.CopyFrom(GetParentStoreSettings());
			}
			if (!respawningAfterLoad)
			{
				allowGroundPickup = true;
			}
		}

		public override void ExposeData()
		{
			((Building)this).ExposeData();
			Scribe_Deep.Look<OutputSettings>(ref outputSettings, "outputSettings", new object[2] { "SmartHopper_Minimum_UseTooltip", "SmartHopper_Maximum_UseTooltip" });
			Scribe_Deep.Look<StorageSettings>(ref settings, "settings", new object[1] { this });
			Scribe_Values.Look<bool>(ref allowGroundPickup, "allowGroundPickup", false, false);
			Scribe_Values.Look<bool>(ref allowStockpilePickup, "allowStockpilePickup", false, false);
			Scribe_Values.Look<bool>(ref allowStoragePickup, "allowStoragePickup", false, false);
			Scribe_Values.Look<bool>(ref allowForbiddenPickup, "allowForbiddenPickup", false, false);
			Scribe_Values.Look<bool>(ref allowBeltPickup, "allowBeltPickup", false, false);
		}

		public override string GetInspectString()
		{
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0120: Unknown result type (might be due to invalid IL or missing references)
			//IL_0125: Unknown result type (might be due to invalid IL or missing references)
			if (OutputSettings.UseMin && OutputSettings.UseMax)
			{
				return TaggedString.op_Implicit(((ThingWithComps)this).GetInspectString() + "\n" + TranslatorFormattedStringExtensions.Translate("SmartHopper_Minimum", NamedArgument.op_Implicit(OutputSettings.Min)) + "\n" + TranslatorFormattedStringExtensions.Translate("SmartHopper_Maximum", NamedArgument.op_Implicit(OutputSettings.Max)));
			}
			if (OutputSettings.UseMin && !OutputSettings.UseMax)
			{
				return TaggedString.op_Implicit(((ThingWithComps)this).GetInspectString() + "\n" + TranslatorFormattedStringExtensions.Translate("SmartHopper_Minimum", NamedArgument.op_Implicit(OutputSettings.Min)));
			}
			if (!OutputSettings.UseMin && OutputSettings.UseMax)
			{
				return TaggedString.op_Implicit(((ThingWithComps)this).GetInspectString() + "\n" + TranslatorFormattedStringExtensions.Translate("SmartHopper_Maximum", NamedArgument.op_Implicit(OutputSettings.Max)));
			}
			return ((ThingWithComps)this).GetInspectString();
		}

		protected override void Tick()
		{
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Invalid comparison between Unknown and I4
			((ThingWithComps)this).Tick();
			if (!((Thing)this).Spawned || Find.TickManager.TicksGame % 35 != 0 || !((ThingWithComps)this).GetComp<CompPowerTrader>().PowerOn)
			{
				return;
			}
			foreach (Thing item in ThingsToSelect)
			{
				bool flag = true;
				if (OutputSettings.UseMin)
				{
					flag = item.stackCount >= OutputSettings.Min;
				}
				if ((int)item.def.category == 2 && settings.AllowedToAccept(item) && flag)
				{
					TryStoreThing(item);
					break;
				}
			}
			if (StoredThing == null)
			{
				return;
			}
			if (settings.AllowedToAccept(StoredThing))
			{
				bool flag2 = true;
				if (OutputSettings.UseMin || OutputSettings.UseMax)
				{
					if (OutputSettings.UseMin && StoredThing.stackCount < OutputSettings.Min)
					{
						flag2 = false;
					}
					else if (OutputSettings.UseMax && StoredThing.stackCount > OutputSettings.Max)
					{
						flag2 = false;
					}
				}
				if (flag2)
				{
					ForbidUtility.SetForbidden(StoredThing, true, false);
					return;
				}
			}
			ForbidUtility.SetForbidden(StoredThing, false, false);
		}

		protected virtual void TryStoreThing(Thing element)
		{
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			if (StoredThing != null)
			{
				if (StoredThing.CanStackWith(element))
				{
					int num = Mathf.Min(element.stackCount, StoredThing.def.stackLimit - StoredThing.stackCount);
					if (OutputSettings.UseMax)
					{
						num = Mathf.Min(element.stackCount, Mathf.Min(StoredThing.def.stackLimit - StoredThing.stackCount, OutputSettings.Max - StoredThing.stackCount));
					}
					if (num > 0)
					{
						Thing val = element.SplitOff(num);
						StoredThing.TryAbsorbStack(val, true);
					}
				}
			}
			else
			{
				int num2 = element.stackCount;
				if (OutputSettings.UseMax)
				{
					num2 = Mathf.Min(element.stackCount, OutputSettings.Max);
				}
				if (num2 > 0)
				{
					Thing val2 = element.SplitOff(num2);
					GenPlace.TryPlaceThing(val2, ((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)0, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
				}
			}
		}

		public override void DrawExtraSelectionOverlays()
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).DrawExtraSelectionOverlays();
			GenDraw.DrawFieldEdges(CellsToSelect, Color.green, (float?)null, (HashSet<IntVec3>)null, 2900);
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			foreach (Gizmo item2 in StorageSettingsClipboard.CopyPasteGizmosFor(settings))
			{
				yield return item2;
			}
			yield return (Gizmo)new Command_Action
			{
				icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel", true),
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("SmartHopper_SetTargetAmount")),
				action = delegate
				{
					Find.WindowStack.Add((Window)(object)new Dialog_OutputMinMax(OutputSettings));
				}
			};
		}

		public StorageSettings GetStoreSettings()
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Expected O, but got Unknown
			if (settings != null)
			{
				return settings;
			}
			settings = new StorageSettings();
			settings.CopyFrom(GetParentStoreSettings());
			return settings;
		}

		public StorageSettings GetParentStoreSettings()
		{
			return ((Thing)this).def.building.fixedStorageSettings;
		}

		public void Notify_SettingsChanged()
		{
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
	public class CompRecipeImportRange : ThingComp
	{
		public CompProperties_RecipeImportRange Props => (CompProperties_RecipeImportRange)(object)base.props;

		public IEnumerable<IntVec3> RangeCells()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			return Props.CellsWithinRange(((Thing)base.parent).Position);
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostDrawExtraSelectionOverlays();
			GenDraw.DrawFieldEdges(Props.CellsWithinRange(((Thing)base.parent).Position).ToList(), Props.ghostColor, (float?)null, (HashSet<IntVec3>)null, 2900);
		}
	}
	public class CompProperties_RecipeImportRange : CompProperties, IXMLThingDescription
	{
		public float range = 5f;

		public Color ghostColor = Color.blue;

		public CompProperties_RecipeImportRange()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			base.compClass = typeof(CompRecipeImportRange);
		}

		public override void DrawGhost(IntVec3 center, Rot4 rot, ThingDef thingDef, Color ghostCol, AltitudeLayer drawAltitude, Thing thing = null)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			((CompProperties)this).DrawGhost(center, rot, thingDef, ghostCol, drawAltitude, thing);
			List<IntVec3> list = CellsWithinRange(center).ToList();
			GenDraw.DrawFieldEdges(list, ghostColor, (float?)null, (HashSet<IntVec3>)null, 2900);
		}

		public IEnumerable<IntVec3> CellsWithinRange(IntVec3 center)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return GenRadial.RadialCellsAround(center, range, true);
		}

		public string GetDescription(ThingDef def)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF_UTD_CompProperties_RecipeImportRange_Range", NamedArgument.op_Implicit(range)) + "\r\n");
		}
	}
	public interface IRecipeSubscriber
	{
		void RecipesChanged(Building_RecipeHolder buildingRecipeHolder);

		void RecipeProviderRemoved(Building_RecipeHolder buildingRecipeHolder);

		void RecipeProviderSpawnedAt(IntVec3 providerLocation, Building_RecipeHolder buildingRecipeHolder);
	}
	internal interface IRecipeHolderInterface
	{
		List<RecipeDef> SavedRecipes { get; set; }

		List<RecipeDef> QueuedRecipes { get; set; }

		List<RecipeDef> LearnableRecipes { get; }

		RecipeDef RecipeLearning { get; set; }

		float ProgressLearning { get; }
	}
	internal class ITab_RecipeHolder : ITab
	{
		private enum Enum_RecipeStatus
		{
			Saved,
			Queued,
			Learnable,
			InProgress
		}

		private const int RowHeight = 30;

		private float scrollViewHeight;

		private static readonly Vector2 WinSize = new Vector2(520f, 500f);

		private Dictionary<RecipeDef, Enum_RecipeStatus> recipes;

		private Vector2 scrollPos;

		private bool showSaved = true;

		private bool showLearnable = true;

		private bool showQueued = true;

		private string searchText = string.Empty;

		private IRecipeHolderInterface ParrentDB => ((ITab)this).SelThing as IRecipeHolderInterface;

		private void RefreshRecipeList()
		{
			recipes = new Dictionary<RecipeDef, Enum_RecipeStatus>();
			foreach (RecipeDef learnableRecipe in ParrentDB.LearnableRecipes)
			{
				if (ParrentDB.QueuedRecipes.Contains(learnableRecipe))
				{
					recipes[learnableRecipe] = Enum_RecipeStatus.Queued;
				}
				else if (ParrentDB.RecipeLearning == learnableRecipe)
				{
					recipes[learnableRecipe] = Enum_RecipeStatus.InProgress;
				}
				else
				{
					recipes[learnableRecipe] = Enum_RecipeStatus.Learnable;
				}
			}
			foreach (RecipeDef savedRecipe in ParrentDB.SavedRecipes)
			{
				recipes[savedRecipe] = Enum_RecipeStatus.Saved;
			}
		}

		public ITab_RecipeHolder()
		{
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).size = WinSize;
			((InspectTabBase)this).labelKey = TaggedString.op_Implicit(Translator.Translate("PRF_RecipeTab_TabName"));
		}

		private bool ShouldDrawRow(RecipeDef recipe, ref float curY, float viewRectHeight, float scrollY, string search)
		{
			if (!showLearnable && recipes[recipe] == Enum_RecipeStatus.Learnable)
			{
				return false;
			}
			if (!showQueued && recipes[recipe] == Enum_RecipeStatus.Queued)
			{
				return false;
			}
			if (!showSaved && recipes[recipe] == Enum_RecipeStatus.Saved)
			{
				return false;
			}
			if (search != string.Empty && !((Def)recipe).label.ToLower().Contains(search.ToLower()))
			{
				return false;
			}
			if (curY + 30f - scrollY < 0f)
			{
				curY += 30f;
				return false;
			}
			if (curY - 30f - scrollY - viewRectHeight < 0f)
			{
				return true;
			}
			curY += 30f;
			return false;
		}

		protected override void FillTab()
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Expected O, but got Unknown
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0105: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			//IL_0138: Unknown result type (might be due to invalid IL or missing references)
			//IL_0165: Unknown result type (might be due to invalid IL or missing references)
			//IL_016a: Unknown result type (might be due to invalid IL or missing references)
			//IL_019c: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_0233: Unknown result type (might be due to invalid IL or missing references)
			//IL_023b: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ce: Invalid comparison between Unknown and I4
			RefreshRecipeList();
			Listing_Standard val = new Listing_Standard();
			Rect val2 = GenUI.ContractedBy(new Rect(0f, 0f, WinSize.x, WinSize.y), 10f);
			float num = 0f;
			((Listing)val).Begin(val2);
			((Listing)val).Gap(12f);
			Rect rect = ((Listing)val).GetRect(30f, 1f);
			((Rect)(ref rect)).width = WinSize.x / 3f - 20f;
			Widgets.CheckboxLabeled(rect, TaggedString.op_Implicit(Translator.Translate("PRF_RecipeTab_FilterSaved")), ref showSaved, false, (Texture2D)null, (Texture2D)null, false, false);
			((Rect)(ref rect)).x = ((Rect)(ref rect)).x + (((Rect)(ref rect)).width + 10f);
			Widgets.CheckboxLabeled(rect, TaggedString.op_Implicit(Translator.Translate("PRF_RecipeTab_FilterLearnable")), ref showLearnable, false, (Texture2D)null, (Texture2D)null, false, false);
			((Rect)(ref rect)).x = ((Rect)(ref rect)).x + (((Rect)(ref rect)).width + 10f);
			Widgets.CheckboxLabeled(rect, TaggedString.op_Implicit(Translator.Translate("PRF_RecipeTab_FilterQueue")), ref showQueued, false, (Texture2D)null, (Texture2D)null, false, false);
			num += 40f;
			rect = ((Listing)val).GetRect(10f, 1f);
			num += 10f;
			Widgets.DrawLineHorizontal(0f, ((Rect)(ref rect)).y, ((Rect)(ref rect)).width);
			rect = ((Listing)val).GetRect(20f, 1f);
			num += 20f;
			((Rect)(ref rect)).width = ((Rect)(ref rect)).width - 166f;
			((Rect)(ref rect)).x = ((Rect)(ref rect)).x + 20f;
			searchText = Widgets.TextField(rect, searchText);
			((Rect)(ref rect)).x = ((Rect)(ref rect)).x - 20f;
			((Rect)(ref rect)).width = 20f;
			GUI.DrawTexture(rect, (Texture)(object)TexButton.Search);
			Rect val3 = default(Rect);
			((Rect)(ref val3))..ctor(5f, num + 5f, WinSize.x - 30f, WinSize.y - num - 30f);
			Rect val4 = default(Rect);
			((Rect)(ref val4))..ctor(0f, 0f, ((Rect)(ref val3)).width - 16f, scrollViewHeight);
			Widgets.BeginScrollView(val3, ref scrollPos, val4, true);
			num = 0f;
			foreach (RecipeDef key in recipes.Keys)
			{
				if (ShouldDrawRow(key, ref num, ((Rect)(ref val3)).height, scrollPos.y, searchText))
				{
					DrawRecipeRow(key, ref num, ((Rect)(ref val4)).width);
				}
			}
			if ((int)Event.current.type == 8)
			{
				scrollViewHeight = num + 30f;
			}
			Widgets.EndScrollView();
			((Listing)val).End();
		}

		private void DrawRecipeRow(RecipeDef recipe, ref float currY, float viewRectWidth)
		{
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0134: Unknown result type (might be due to invalid IL or missing references)
			//IL_013a: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(0f, currY, viewRectWidth, 30f);
			currY += 30f;
			if (recipe.products.Count > 0)
			{
				Rect val2 = val;
				((Rect)(ref val2)).width = 30f;
				((Rect)(ref val2)).height = 30f;
				Widgets.DefIcon(val2, (Def)(object)recipe.products[0].thingDef, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
			}
			((Rect)(ref val)).x = 60f;
			Widgets.Label(val, string.Empty + ((Def)recipe).label);
			((Rect)(ref val)).width = 100f;
			((Rect)(ref val)).x = 350f;
			if (recipes[recipe] == Enum_RecipeStatus.Learnable)
			{
				if (Widgets.ButtonText(val, TaggedString.op_Implicit(Translator.Translate("PRF_RecipeTab_Button_Learn")), true, true, true, (TextAnchor?)null))
				{
					ParrentDB.QueuedRecipes.Add(recipe);
				}
			}
			else if (recipes[recipe] == Enum_RecipeStatus.Queued)
			{
				if (Widgets.ButtonText(val, TaggedString.op_Implicit(Translator.Translate("PRF_RecipeTab_Button_Cancel")), true, true, true, (TextAnchor?)null) && ParrentDB.QueuedRecipes.Contains(recipe))
				{
					ParrentDB.QueuedRecipes.Remove(recipe);
				}
			}
			else if (recipes[recipe] == Enum_RecipeStatus.InProgress && Widgets.ButtonText(val, TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF_RecipeTab_Button_Abort", NamedArgument.op_Implicit(GenText.ToStringWorkAmount(ParrentDB.ProgressLearning)))), true, true, true, (TextAnchor?)null))
			{
				ParrentDB.RecipeLearning = null;
			}
		}
	}
	public class ITab_SmartHopper : ITab
	{
		private static readonly Vector2 WinSize = new Vector2(200f, 200f);

		private bool groundPickup;

		private bool stockpile;

		private bool buildingStorage;

		private bool allowForbidden;

		private bool allowBelt;

		private IPickupSettings Hopper => ((ITab)this).SelThing as IPickupSettings;

		public ITab_SmartHopper()
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).size = WinSize;
			((InspectTabBase)this).labelKey = "PRF.SmartHopper.ITab.Name";
		}

		protected override void FillTab()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Expected O, but got Unknown
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0123: Unknown result type (might be due to invalid IL or missing references)
			//IL_0124: Unknown result type (might be due to invalid IL or missing references)
			//IL_012a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0150: Unknown result type (might be due to invalid IL or missing references)
			//IL_0155: Unknown result type (might be due to invalid IL or missing references)
			//IL_0156: Unknown result type (might be due to invalid IL or missing references)
			//IL_015c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0182: Unknown result type (might be due to invalid IL or missing references)
			//IL_0187: Unknown result type (might be due to invalid IL or missing references)
			//IL_0188: Unknown result type (might be due to invalid IL or missing references)
			//IL_018e: Unknown result type (might be due to invalid IL or missing references)
			Listing_Standard val = new Listing_Standard();
			Rect val2 = GenUI.ContractedBy(new Rect(0f, 0f, WinSize.x - 20f, WinSize.y), 10f);
			((Listing)val).Begin(val2);
			groundPickup = Hopper.AllowGroundPickup;
			stockpile = Hopper.AllowStockpilePickup;
			buildingStorage = Hopper.AllowStoragePickup;
			allowForbidden = Hopper.AllowForbiddenPickup;
			allowBelt = Hopper.AllowBeltPickup;
			val.Label(Translator.Translate("PRF.SmartHopper.ITab.Description"), -1f, (string)null);
			val2 = ((Listing)val).GetRect(30f, 1f);
			Widgets.CheckboxLabeled(val2, TaggedString.op_Implicit(Translator.Translate("PRF.SmartHopper.ITab.AllowGround")), ref groundPickup, false, (Texture2D)null, (Texture2D)null, false, false);
			val2 = ((Listing)val).GetRect(30f, 1f);
			Widgets.CheckboxLabeled(val2, TaggedString.op_Implicit(Translator.Translate("PRF.SmartHopper.ITab.AllowStockpile")), ref stockpile, false, (Texture2D)null, (Texture2D)null, false, false);
			val2 = ((Listing)val).GetRect(30f, 1f);
			Widgets.CheckboxLabeled(val2, TaggedString.op_Implicit(Translator.Translate("PRF.SmartHopper.ITab.AllowStorage")), ref buildingStorage, false, (Texture2D)null, (Texture2D)null, false, false);
			val2 = ((Listing)val).GetRect(30f, 1f);
			Widgets.CheckboxLabeled(val2, TaggedString.op_Implicit(Translator.Translate("PRF.SmartHopper.ITab.AllowForbidden")), ref allowForbidden, false, (Texture2D)null, (Texture2D)null, false, false);
			val2 = ((Listing)val).GetRect(30f, 1f);
			Widgets.CheckboxLabeled(val2, TaggedString.op_Implicit(Translator.Translate("PRF.SmartHopper.ITab.AllowBelt")), ref allowBelt, false, (Texture2D)null, (Texture2D)null, false, false);
			Hopper.AllowGroundPickup = groundPickup;
			Hopper.AllowStockpilePickup = stockpile;
			Hopper.AllowStoragePickup = buildingStorage;
			Hopper.AllowForbiddenPickup = allowForbidden;
			Hopper.AllowBeltPickup = allowBelt;
			((Listing)val).End();
		}
	}
}
namespace ProjectRimFactory.SAL3.Things.Assemblers
{
	public abstract class Building_DynamicBillGiver : PRF_BuildingBill
	{
		public virtual IEnumerable<IntVec3> IngredientStackCells => (from c in ((ThingWithComps)this).GetComp<CompPowerWorkSetting>()?.GetRangeCells()
			where GenGrid.InBounds(c, ((Thing)this).Map)
			select c) ?? (from c in GenAdj.CellsAdjacent8Way((Thing)(object)this)
			where GenGrid.InBounds(c, ((Thing)this).Map)
			select c);

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building_WorkTable)this).SpawnSetup(map, respawningAfterLoad);
			RegisterRecipes();
		}

		private void RegisterRecipes()
		{
			ThingDef def = ((Thing)this).def;
			if (def.recipes == null)
			{
				def.recipes = new List<RecipeDef>();
			}
			IEnumerable<RecipeDef> importedRecipes = GetImportedRecipes();
			foreach (RecipeDef item in importedRecipes)
			{
				if (!((Thing)this).def.recipes.Contains(item))
				{
					((Thing)this).def.recipes.Add(item);
				}
			}
			ReflectionUtility.AllRecipesCached.SetValue(((Thing)this).def, null);
		}

		public bool CurrentlyUsableForBills()
		{
			return false;
		}

		protected virtual IEnumerable<RecipeDef> GetImportedRecipes()
		{
			return Array.Empty<RecipeDef>();
		}

		public bool UsableForBillsAfterFueling()
		{
			return false;
		}
	}
	public class Building_Miner : Building_ProgrammableAssembler
	{
		public override void ExposeData()
		{
			base.ExposeData();
			((Building_WorkTable)this).billStack.Bills.RemoveAll((Bill b) => ((Def)((Thing)this).def).GetModExtension<ModExtension_Miner>()?.IsExcluded(b.recipe.ProducedThingDef) ?? false);
		}
	}
	public abstract class Building_ProgrammableAssembler : Building_DynamicBillGiver, IPowerSupplyMachineHolder, IAssemblerQueue
	{
		protected class BillReport : IExposable
		{
			public Bill Bill;

			public List<Thing> Selected;

			public float WorkLeft;

			public BillReport()
			{
			}

			public BillReport(Bill b, List<Thing> list)
			{
				Bill = b;
				Selected = list;
				WorkLeft = b.recipe.WorkAmountTotal(ProjectSal_Utilities.CalculateDominantIngredient(b.recipe, list));
			}

			public void ExposeData()
			{
				Scribe_References.Look<Bill>(ref Bill, "bill", false);
				Scribe_Collections.Look<Thing>(ref Selected, "selected", (LookMode)2, Array.Empty<object>());
				Scribe_Values.Look<float>(ref WorkLeft, "workLeft", 0f, false);
			}
		}

		private CompPowerTrader compPowerTrader;

		private CompRefuelable compRefuelable;

		private CompFlickable compFlick;

		private CompOutputAdjustable compOutputAdjustable;

		protected AssemblerDefModExtension AssemblerDefModExtension;

		private Pawn buildingPawn;

		private PRFGameComponent prfGameComp = Current.Game.GetComponent<PRFGameComponent>();

		private bool allowForbidden;

		protected BillReport CurrentBillReport;

		protected List<Thing> ThingQueue = new List<Thing>();

		private const int MaxThingQueueCount = 100;

		[Unsaved(false)]
		private Effecter effecter;

		[Unsaved(false)]
		private Sustainer sound;

		public override Graphic Graphic => (!Active) ? (AssemblerDefModExtension?.PowerOffGrahic ?? ((Thing)this).Graphic) : ((CurrentBillReport == null) ? ((Thing)this).Graphic : (AssemblerDefModExtension?.WorkingGrahic ?? ((Thing)this).Graphic));

		private bool DrawStatus => AssemblerDefModExtension?.drawStatus ?? true;

		private MapTickManager MapManager { get; set; }

		protected bool Active
		{
			get
			{
				CompPowerTrader obj = compPowerTrader;
				int result;
				if (obj == null || obj.PowerOn)
				{
					CompRefuelable obj2 = compRefuelable;
					if (obj2 == null || obj2.HasFuel)
					{
						CompFlickable obj3 = compFlick;
						result = ((obj3 == null || obj3.SwitchIsOn) ? 1 : 0);
						goto IL_003d;
					}
				}
				result = 0;
				goto IL_003d;
				IL_003d:
				return (byte)result != 0;
			}
		}

		private IEnumerable<Thing> AllAccessibleThings => from t in AllThingsInArea
			where AllowForbidden || !ForbidUtility.IsForbidden(t, ((Thing)this).Faction)
			where !((Thing)this).Map.reservationManager.AllReservedThings().Contains(t)
			select t;

		private IEnumerable<Thing> AllThingsInArea
		{
			get
			{
				IThingHolder holder = default(IThingHolder);
				foreach (IntVec3 cell in IngredientStackCells)
				{
					foreach (Thing thing in ((Thing)this).Map.thingGrid.ThingsListAt(cell))
					{
						Building val = (Building)(object)((thing is Building) ? thing : null);
						int num;
						if (val != null)
						{
							holder = (IThingHolder)(object)((val is IThingHolder) ? val : null);
							num = ((holder != null) ? 1 : 0);
						}
						else
						{
							num = 0;
						}
						if (num != 0)
						{
							ThingOwner directlyHeldThings = holder.GetDirectlyHeldThings();
							if (!(directlyHeldThings is ThingOwner<Thing> owner))
							{
								continue;
							}
							foreach (Thing item in owner.InnerListForReading)
							{
								yield return item;
							}
						}
						else if ((int)thing.def.category == 2)
						{
							yield return thing;
						}
						holder = null;
					}
				}
			}
		}

		private IEnumerable<Bill> AllBillsShouldDoNow => ((Building_WorkTable)this).billStack.Bills.Where((Bill b) => b.ShouldDoNow());

		private ModExtension_Skills ExtensionSkills => ((Def)((Thing)this).def).GetModExtension<ModExtension_Skills>();

		private bool AllowForbidden => allowForbidden;

		protected virtual float ProductionSpeedFactor => AssemblerDefModExtension?.workSpeedBaseFactor ?? 1f;

		public IPowerSupplyMachine RangePowerSupplyMachine => ((ThingWithComps)this).GetComp<CompPowerWorkSetting>();

		private bool AllowProductionThingQueue => ThingQueue.Count < 100;

		Map IAssemblerQueue.Map => ((Thing)this).Map;

		private void DoPawn()
		{
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Expected O, but got Unknown
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Pawn val = PawnGenerator.GeneratePawn(PRFDefOf.PRFSlavePawn, Faction.OfPlayer, (PlanetTile?)null);
				val.Name = (Name)new NameTriple("...", ((Entity)this).Label ?? TaggedString.op_Implicit(Translator.Translate("SAL_Name")), "...");
				foreach (SkillRecord skill in val.skills.skills)
				{
					skill.Level = ExtensionSkills.GetExtendedSkillLevel(skill.def, typeof(Building_ProgrammableAssembler));
				}
				val.ideo = null;
				val.genes = null;
				val.royalty = null;
				val.guest = null;
				ReflectionUtility.MapIndexOrState.SetValue(val, ReflectionUtility.MapIndexOrState.GetValue(this));
				((Thing)val).SetPositionDirect(((Thing)this).PositionHeld);
				val.relations.ClearAllRelations();
				SetBackstoryAndSkills(val);
				val.Notify_DisabledWorkTypesChanged();
				for (int i = 0; i < 24; i++)
				{
					val.timetable.SetAssignment(i, TimeAssignmentDefOf.Work);
				}
				buildingPawn = val;
			}
			catch (Exception ex)
			{
				Log.Error("ERROR=: " + ex);
			}
		}

		private static void SetBackstoryAndSkills(Pawn p)
		{
			p.story.Childhood = PRFDefOf.ChildSpy47;
			p.story.Adulthood = PRFDefOf.ColonySettler53;
			p.story.traits.allTraits = new List<Trait>();
			for (int i = 0; i < p.skills.skills.Count; i++)
			{
				ReflectionUtility.CachedTotallyDisabled.SetValue(p.skills.skills[i], (object)(BoolUnknown)2);
			}
		}

		public override void ExposeData()
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Invalid comparison between Unknown and I4
			base.ExposeData();
			if (((Building_WorkTable)this).BillStack == null && (int)Scribe.mode == 2)
			{
				Scribe_Deep.Look<BillStack>(ref ((Building_WorkTable)this).billStack, "bills", new object[1] { this });
			}
			Scribe_Deep.Look<BillReport>(ref CurrentBillReport, "currentBillReport", Array.Empty<object>());
			Scribe_Collections.Look<Thing>(ref ThingQueue, "thingQueue", (LookMode)2, Array.Empty<object>());
			Scribe_Values.Look<bool>(ref allowForbidden, "allowForbidden", false, false);
			Scribe_Deep.Look<Pawn>(ref buildingPawn, "buildingPawn", Array.Empty<object>());
			if (ThingQueue == null)
			{
				ThingQueue = new List<Thing>();
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			yield return (Gizmo)new Command_Toggle
			{
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("SALToggleForbidden")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("SALToggleForbidden_Desc")),
				isActive = () => allowForbidden,
				toggleAction = delegate
				{
					allowForbidden = !allowForbidden;
				},
				icon = (Texture)(object)TexCommand.ForbidOff
			};
			if (Prefs.DevMode)
			{
				yield return (Gizmo)new Command_Action
				{
					defaultLabel = "DEBUG: Debug actions",
					action = delegate
					{
						//IL_0010: Unknown result type (might be due to invalid IL or missing references)
						//IL_001a: Expected O, but got Unknown
						Find.WindowStack.Add((Window)new FloatMenu(GetDebugOptions().ToList()));
					}
				};
			}
			yield return (Gizmo)new Command_Action
			{
				action = MakeMatchingStockpileZone,
				hotKey = KeyBindingDefOf.Misc1,
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("DesignatorZoneCreateStorageResourcesDesc")),
				icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile", true),
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRF_MakeStockpileZoneLabel"))
			};
		}

		private void MakeMatchingStockpileZone()
		{
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Expected O, but got Unknown
			IEnumerable<IntVec3> enumerable = IngredientStackCells.Where((IntVec3 c) => c != compOutputAdjustable.CurrentCell && !GenCollection.Any<Thing>(((Thing)this).Map.thingGrid.ThingsListAt(c), (Predicate<Thing>)((Thing t) => !t.def.CanOverlapZones)));
			if (enumerable.Any())
			{
				Designator_ZoneAddStockpile_Resources val = new Designator_ZoneAddStockpile_Resources();
				((Designator)val).DesignateMultiCell(enumerable);
			}
			else
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("PRF_CantCreateStockpile")), LookTargets.op_Implicit((Thing)(object)this), MessageTypeDefOf.CautionInput, true);
			}
		}

		protected virtual IEnumerable<FloatMenuOption> GetDebugOptions()
		{
			yield return new FloatMenuOption("View selected things", (Action)delegate
			{
				if (CurrentBillReport != null)
				{
					Log.Message("Selected things: " + string.Join(", ", CurrentBillReport.Selected.Select(StringConverter).ToArray()));
				}
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
			yield return new FloatMenuOption("View all items available for input", (Action)delegate
			{
				Log.Message(string.Join(", ", AllAccessibleThings.Select(StringConverter).ToArray()));
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
			static string StringConverter(Thing t)
			{
				return t.GetUniqueLoadID();
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			AssemblerDefModExtension = ((Def)((Thing)this).def).GetModExtension<AssemblerDefModExtension>();
			base.SpawnSetup(map, respawningAfterLoad);
			compOutputAdjustable = ((ThingWithComps)this).GetComp<CompOutputAdjustable>();
			MapManager = map.GetComponent<MapTickManager>();
			if (buildingPawn == null)
			{
				DoPawn();
			}
			buildingPawn?.skills.skills.ForEach(delegate(SkillRecord s)
			{
				s.Level = ExtensionSkills.GetExtendedSkillLevel(s.def, typeof(Building_ProgrammableAssembler));
				ReflectionUtility.CachedTotallyDisabled.SetValue(s, (object)(BoolUnknown)2);
			});
			compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			compRefuelable = ((ThingWithComps)this).GetComp<CompRefuelable>();
			compFlick = ((ThingWithComps)this).GetComp<CompFlickable>();
			prfGameComp = Current.Game.GetComponent<PRFGameComponent>();
			ReflectionUtility.MapIndexOrState.SetValue(buildingPawn, ReflectionUtility.MapIndexOrState.GetValue(this));
			((Thing)buildingPawn).SetPositionDirect(((Thing)this).Position);
			IPowerSupplyMachine rangePowerSupplyMachine = RangePowerSupplyMachine;
			if (rangePowerSupplyMachine != null)
			{
				MapManager.NextAction(rangePowerSupplyMachine.RefreshPowerStatus);
				MapManager.AfterAction(5, rangePowerSupplyMachine.RefreshPowerStatus);
			}
			prfGameComp.RegisterAssemblerQueue(this);
			if (CurrentBillReport?.Bill == null)
			{
				CurrentBillReport = null;
			}
		}

		public override Thing GetThingBy(Func<Thing, bool> optionalValidator = null)
		{
			foreach (Thing item in ThingQueue)
			{
				if (optionalValidator != null && !optionalValidator(item))
				{
					continue;
				}
				ThingQueue.Remove(item);
				return item;
			}
			return null;
		}

		protected override void Tick()
		{
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_0288: Unknown result type (might be due to invalid IL or missing references)
			//IL_028e: Unknown result type (might be due to invalid IL or missing references)
			//IL_026c: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).Tick();
			if (!((Thing)this).Spawned)
			{
				return;
			}
			if (Gen.IsHashIntervalTick((Thing)(object)this, 10) && Active)
			{
				if (ThingQueue.Count > 0 && this.PRFTryPlaceThing(ThingQueue[0], compOutputAdjustable.CurrentCell, ((Thing)this).Map))
				{
					ThingQueue.RemoveAt(0);
				}
				if (CurrentBillReport != null)
				{
					CurrentBillReport.WorkLeft -= 10f * ProductionSpeedFactor * (ThingCompUtility.TryGetComp<CompPowerWorkSetting>((Thing)(object)this)?.GetSpeedFactor() ?? 1f);
					if (CurrentBillReport.WorkLeft <= 0f)
					{
						try
						{
							ProduceItems();
							CurrentBillReport.Bill.Notify_IterationCompleted(buildingPawn, CurrentBillReport.Selected);
							Notify_RecipeCompleted(CurrentBillReport.Bill.recipe);
						}
						catch (Exception ex)
						{
							Log.Error("Error producing items for " + ((Thing)this).GetUniqueLoadID() + ": " + ex);
						}
						finally
						{
							CurrentBillReport = null;
						}
					}
				}
				else if (Gen.IsHashIntervalTick((Thing)(object)this, 60) && AllowProductionThingQueue && (CurrentBillReport = TryGetNextBill()) != null)
				{
					Notify_BillStarted();
				}
			}
			if (CurrentBillReport != null && Active)
			{
				if (effecter == null)
				{
					EffecterDef obj = ((AssemblerDefModExtension != null) ? AssemblerDefModExtension.GetEffecter(CurrentBillReport.Bill.recipe) : CurrentBillReport.Bill.recipe?.effectWorking);
					effecter = ((obj != null) ? obj.Spawn() : null);
				}
				if (sound == null)
				{
					SoundDef obj2 = ((AssemblerDefModExtension != null) ? AssemblerDefModExtension.GetSound(CurrentBillReport.Bill.recipe) : CurrentBillReport.Bill.recipe?.soundWorking);
					sound = ((obj2 != null) ? SoundStarter.TrySpawnSustainer(obj2, SoundInfo.op_Implicit((Thing)(object)this)) : null);
				}
				Effecter obj3 = effecter;
				if (obj3 != null)
				{
					obj3.EffectTick(TargetInfo.op_Implicit((Thing)(object)this), TargetInfo.op_Implicit((Thing)(object)this));
				}
				Sustainer obj4 = sound;
				if (obj4 != null)
				{
					obj4.SustainerUpdate();
				}
				if (((ThingWithComps)this).GetComp<CompGlowerPulse>() != null)
				{
					((ThingWithComps)this).GetComp<CompGlowerPulse>().Glows = true;
				}
			}
			else
			{
				if (effecter != null)
				{
					effecter.Cleanup();
					effecter = null;
				}
				if (sound != null)
				{
					sound.End();
					sound = null;
				}
				if (((ThingWithComps)this).GetComp<CompGlowerPulse>() != null)
				{
					((ThingWithComps)this).GetComp<CompGlowerPulse>().Glows = false;
				}
			}
			if (compRefuelable != null && Active && CurrentBillReport != null)
			{
				compRefuelable.Notify_UsedThisTick();
			}
		}

		protected BillReport TryGetNextBill()
		{
			IEnumerable<Thing> allAccessibleThings = AllAccessibleThings;
			IEnumerable<Bill> allBillsShouldDoNow = AllBillsShouldDoNow;
			foreach (Bill bill in allBillsShouldDoNow)
			{
				if (bill.recipe.ingredients.Count == 0)
				{
					return new BillReport(bill, new List<Thing>());
				}
				List<Thing> list = allAccessibleThings.Where((Thing x) => bill.IsFixedOrAllowedIngredient(x)).ToList();
				if ((list.Count > 0 || !bill.ingredientFilter.AllowedThingDefs.Any()) && TryFindBestBillIngredientsInSet(list, bill, out var chosen))
				{
					return new BillReport(bill, chosen.Select(delegate(ThingCount ta)
					{
						//IL_0000: Unknown result type (might be due to invalid IL or missing references)
						//IL_0001: Unknown result type (might be due to invalid IL or missing references)
						//IL_0009: Unknown result type (might be due to invalid IL or missing references)
						//IL_000a: Unknown result type (might be due to invalid IL or missing references)
						ThingCount val = ta;
						Thing thing = ((ThingCount)(ref val)).Thing;
						val = ta;
						return thing.SplitOff(((ThingCount)(ref val)).Count);
					}).ToList());
				}
			}
			return null;
		}

		private static bool TryFindBestBillIngredientsInSet(List<Thing> accessibleThings, Bill b, out List<ThingCount> chosen)
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			chosen = new List<ThingCount>();
			List<IngredientCount> list = new List<IngredientCount>();
			return (bool)ReflectionUtility.TryFindBestBillIngredientsInSet.Invoke(null, new object[6]
			{
				accessibleThings,
				b,
				chosen,
				(object)default(IntVec3),
				false,
				list
			});
		}

		private void ProduceItems()
		{
			if (CurrentBillReport == null)
			{
				Log.Error("Project RimFactory :: Tried to make products when assembler isn't engaged in a bill.");
				return;
			}
			if (((Thing)this).def == PRFDefOf.PRF_Recycler)
			{
				Patch_Thing_SmeltProducts.RecyclerProducingItems = true;
			}
			Patch_CompFoodPoisonable_Notify_RecipeProduced.AssemblerRefrence = this;
			List<Thing> list = GenRecipe.MakeRecipeProducts(CurrentBillReport.Bill.recipe, buildingPawn, CurrentBillReport.Selected, ProjectSal_Utilities.CalculateDominantIngredient(CurrentBillReport.Bill.recipe, CurrentBillReport.Selected), (IBillGiver)(object)this, (Precept_ThingStyle)null, (ThingStyleDef)null, (int?)null).ToList();
			((Def)((Thing)this).def).GetModExtension<ModExtension_ModifyProduct>()?.ProcessProducts(list, (IBillGiver)(object)this, (Thing)(object)this, CurrentBillReport.Bill.recipe);
			foreach (Thing item in list)
			{
				PostProcessRecipeProduct(item);
				ThingQueue.Add(item);
			}
			Patch_CompFoodPoisonable_Notify_RecipeProduced.AssemblerRefrence = null;
			Patch_Thing_SmeltProducts.RecyclerProducingItems = false;
			for (int i = 0; i < CurrentBillReport.Selected.Count; i++)
			{
				Thing val = CurrentBillReport.Selected[i];
				TryGetCorpseItems(val);
				CurrentBillReport.Bill.recipe.Worker.ConsumeIngredient(val, CurrentBillReport.Bill.recipe, ((Thing)this).Map);
			}
			ThingQueue.AddRange(from Thing t in CurrentBillReport.Selected
				where t.Spawned
				select t);
		}

		private void TryGetCorpseItems(Thing selected)
		{
			Corpse val = (Corpse)(object)((selected is Corpse) ? selected : null);
			if (val == null)
			{
				return;
			}
			Pawn innerPawn = val.InnerPawn;
			if (innerPawn == null)
			{
				return;
			}
			if (innerPawn.apparel != null)
			{
				List<Apparel> list = innerPawn.apparel.WornApparel.ToList();
				for (int i = 0; i < list.Count; i++)
				{
					ThingQueue.Add((Thing)(object)list[i]);
					innerPawn.apparel.Remove(list[i]);
				}
			}
			if (innerPawn.inventory?.innerContainer != null)
			{
				List<Thing> list2 = ((IEnumerable<Thing>)innerPawn.inventory.innerContainer).ToList();
				for (int j = 0; j < list2.Count; j++)
				{
					ThingQueue.Add(list2[j]);
					((ThingOwner)innerPawn.inventory.innerContainer).Remove(list2[j]);
				}
			}
		}

		public override string GetInspectString()
		{
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(((ThingWithComps)this).GetInspectString());
			if (Active)
			{
				if (CurrentBillReport == null)
				{
					stringBuilder.AppendLine(TaggedString.op_Implicit(((Building_WorkTable)this).BillStack.AnyShouldDoNow ? Translator.Translate("SearchingForIngredients") : Translator.Translate("AssemblerNoBills")));
				}
				else
				{
					stringBuilder.AppendLine(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SAL3_BillReport", NamedArgument.op_Implicit(CurrentBillReport.Bill.Label), NamedArgument.op_Implicit(GenText.ToStringWorkAmount(CurrentBillReport.WorkLeft)))));
				}
			}
			stringBuilder.AppendLine(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SAL3_Products", NamedArgument.op_Implicit(ThingQueue.Count), NamedArgument.op_Implicit(100))));
			return GenText.TrimEndNewlines(stringBuilder.ToString());
		}

		protected bool SatisfiesSkillRequirements(RecipeDef recipe)
		{
			Pawn val = buildingPawn;
			if (val != null && val.skills != null)
			{
				return recipe.PawnSatisfiesSkillRequirements(buildingPawn);
			}
			return recipe.skillRequirements?.All((SkillRequirement s) => s.minLevel <= ExtensionSkills.GetExtendedSkillLevel(s.skill, typeof(Building_ProgrammableAssembler))) ?? true;
		}

		public override void DrawExtraSelectionOverlays()
		{
			base.DrawExtraSelectionOverlays();
			if (((ThingWithComps)this).GetComp<CompPowerWorkSetting>() == null)
			{
				GenDraw.DrawFieldEdges(IngredientStackCells.ToList(), 2900);
			}
		}

		public override void DrawGUIOverlay()
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Invalid comparison between Unknown and I4
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_012a: Unknown result type (might be due to invalid IL or missing references)
			//IL_012f: Unknown result type (might be due to invalid IL or missing references)
			//IL_010c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_0118: Unknown result type (might be due to invalid IL or missing references)
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0142: Unknown result type (might be due to invalid IL or missing references)
			//IL_0144: Unknown result type (might be due to invalid IL or missing references)
			//IL_0178: Unknown result type (might be due to invalid IL or missing references)
			//IL_017a: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).DrawGUIOverlay();
			if (!DrawStatus || (int)Find.CameraDriver.CurrentZoom >= 2)
			{
				return;
			}
			string text = string.Empty;
			string text2 = string.Empty;
			if (Active)
			{
				text = ((CurrentBillReport == null) ? TaggedString.op_Implicit(((Building_WorkTable)this).BillStack.AnyShouldDoNow ? Translator.Translate("SearchingForIngredients") : Translator.Translate("AssemblerNoBills")) : CurrentBillReport.Bill.LabelCap);
			}
			else
			{
				CompFlickable obj = compFlick;
				if (obj != null && !obj.SwitchIsOn)
				{
					text = TaggedString.op_Implicit(Translator.Translate("SwitchedOff"));
				}
			}
			if (!AllowProductionThingQueue)
			{
				text2 = TaggedString.op_Implicit(Translator.Translate("PRF_OutputBufferWarning"));
			}
			else if (ThingQueue.Count > 1)
			{
				text2 = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SAL3_Products", NamedArgument.op_Implicit(ThingQueue.Count), NamedArgument.op_Implicit(100)));
			}
			Vector2 val = GenMapUI.LabelDrawPosFor((Thing)(object)this, 0f);
			if (text != string.Empty)
			{
				GenMapUI.DrawThingLabel(val, text, Color.white);
			}
			val.y += Text.CalcSize(text).y;
			if (text2 != string.Empty)
			{
				GenMapUI.DrawThingLabel(val, text2, Color.yellow);
			}
		}

		private void Notify_RecipeCompleted(RecipeDef _)
		{
		}

		protected virtual void PostProcessRecipeProduct(Thing thing)
		{
		}

		protected virtual void Notify_BillStarted()
		{
		}

		public List<Thing> GetThingQueue()
		{
			return ThingQueue;
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			for (int num = ThingQueue.Count - 1; num >= 0; num--)
			{
				if (ThingQueue[num].Destroyed)
				{
					ThingQueue[num].ForceSetStateToUnspawned();
				}
				this.PRFTryPlaceThing(ThingQueue[num], ((Thing)this).Position, ((Thing)this).Map, forcePlace: true);
			}
			((Building)this).DeSpawn(mode);
			prfGameComp.DeRegisterAssemblerQueue(this);
		}

		public override void Destroy(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			for (int num = ThingQueue.Count - 1; num >= 0; num--)
			{
				this.PRFTryPlaceThing(ThingQueue[num], ((Thing)this).Position, ((Thing)this).Map, forcePlace: true);
			}
			((Building)this).Destroy(mode);
			prfGameComp.DeRegisterAssemblerQueue(this);
		}

		public override IntVec3 OutputCell()
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			return compOutputAdjustable.CurrentCell;
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
	public class Building_SimpleAssembler : Building_ProgrammableAssembler
	{
		protected override IEnumerable<RecipeDef> GetImportedRecipes()
		{
			HashSet<RecipeDef> recipes = new HashSet<RecipeDef>();
			if ((AssemblerDefModExtension?.importRecipesFrom?.Count).GetValueOrDefault() <= 0)
			{
				yield break;
			}
			foreach (RecipeDef r in AssemblerDefModExtension.importRecipesFrom.SelectMany((ThingDef t) => t.AllRecipes))
			{
				if (!recipes.Contains(r) && SatisfiesSkillRequirements(r))
				{
					recipes.Add(r);
					yield return r;
				}
			}
		}
	}
	public class Building_SmartAssembler : Building_ProgrammableAssembler, IRecipeSubscriber
	{
		private CompRecipeImportRange compRecipeImportRange;

		private List<IntVec3> importCells;

		private readonly Dictionary<Building_RecipeHolder, List<RecipeDef>> subscribedBills = new Dictionary<Building_RecipeHolder, List<RecipeDef>>();

		private HashSet<RecipeDef> effectiveRecipes = new HashSet<RecipeDef>();

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			compRecipeImportRange = ((ThingWithComps)this).GetComp<CompRecipeImportRange>();
			RefreshRecipeImportRange();
			((Thing)this).Map.GetComponent<PRFMapComponent>().RegisterRecipeSubscriber(this);
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			Building_RecipeHolder[] array = subscribedBills.Keys.ToArray();
			Building_RecipeHolder[] array2 = array;
			foreach (Building_RecipeHolder building_RecipeHolder in array2)
			{
				building_RecipeHolder.DeregisterRecipeSubscriber(this);
			}
			((Thing)this).Map.GetComponent<PRFMapComponent>().DeregisterRecipeSubscriber(this);
			base.DeSpawn(mode);
		}

		private void RefreshRecipeImportRange()
		{
			importCells = (compRecipeImportRange?.RangeCells() ?? GenAdj.CellsAdjacent8Way((Thing)(object)this)).Where((IntVec3 cell) => GenGrid.InBounds(cell, ((Thing)this).Map)).ToList();
			List<Building_RecipeHolder> holders = (from cell in importCells
				select GridsUtility.GetFirstThing<Building_RecipeHolder>(cell, ((Thing)this).Map) into holder
				where holder != null
				select holder).ToList();
			List<Building_RecipeHolder> list = subscribedBills.Keys.Where((Building_RecipeHolder holder) => !holders.Contains(holder)).ToList();
			foreach (Building_RecipeHolder item in list)
			{
				item.DeregisterRecipeSubscriber(this);
			}
			foreach (Building_RecipeHolder item2 in holders)
			{
				item2.RegisterRecipeSubscriber(this);
			}
		}

		public void RecipesChanged(Building_RecipeHolder buildingRecipeHolder)
		{
			if (subscribedBills.Keys.Contains(buildingRecipeHolder))
			{
				subscribedBills[buildingRecipeHolder] = buildingRecipeHolder.Recipes;
			}
			else
			{
				subscribedBills.Add(buildingRecipeHolder, buildingRecipeHolder.Recipes);
			}
			UpdateBills();
		}

		private void UpdateBills()
		{
			//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0198: Unknown result type (might be due to invalid IL or missing references)
			HashSet<RecipeDef> newRecipes = new HashSet<RecipeDef>();
			List<RecipeDef> list = new List<RecipeDef>();
			foreach (List<RecipeDef> value in subscribedBills.Values)
			{
				foreach (RecipeDef item in value)
				{
					if (!newRecipes.Contains(item) && SatisfiesSkillRequirements(item))
					{
						if (!effectiveRecipes.Contains(item))
						{
							list.Add(item);
						}
						newRecipes.Add(item);
					}
				}
			}
			List<RecipeDef> removed = effectiveRecipes.Where((RecipeDef recipe) => !newRecipes.Contains(recipe)).ToList();
			if (removed.Count > 0)
			{
				((Thing)this).def.recipes.RemoveAll((RecipeDef recipe) => removed.Contains(recipe));
				((Building_WorkTable)this).BillStack.Bills.RemoveAll((Bill b) => removed.Contains(b.recipe));
				if (CurrentBillReport != null && !removed.Contains(CurrentBillReport.Bill.recipe))
				{
					for (int num = 0; num < CurrentBillReport.Selected.Count; num++)
					{
						GenPlace.TryPlaceThing(CurrentBillReport.Selected[num], ((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					}
					CurrentBillReport = null;
				}
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("SAL3Alert_SomeBillsRemoved")), LookTargets.op_Implicit((Thing)(object)this), MessageTypeDefOf.NegativeEvent, true);
			}
			if (list.Count > 0)
			{
				((Thing)this).def.recipes.AddRange(list);
			}
			if (list.Count > 0 || removed.Count > 0)
			{
				ReflectionUtility.AllRecipesCached.SetValue(((Thing)this).def, null);
			}
			effectiveRecipes = newRecipes;
		}

		public void RecipeProviderRemoved(Building_RecipeHolder buildingRecipeHolder)
		{
			if (!GravshipPlacementUtility.placingGravship && !GravshipUtility.generatingGravship)
			{
				subscribedBills.Remove(buildingRecipeHolder);
				UpdateBills();
			}
		}

		public void RecipeProviderSpawnedAt(IntVec3 providerLocation, Building_RecipeHolder buildingRecipeHolder)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			if (importCells.Contains(providerLocation))
			{
				RecipesChanged(buildingRecipeHolder);
			}
		}
	}
}
namespace ProjectRimFactory.SAL3.Things.Assemblers.Special
{
	public class Building_Assembler_Learning : Building_SmartAssembler, ISetQualityDirectly
	{
		private WorkSpeedFactorManager manager;

		private float FactorOffset => ModExtensionLearningAssembler?.MinSpeed ?? 0.5f;

		private float MaxSpeed
		{
			get
			{
				float num = ModExtensionLearningAssembler?.MaxSpeed ?? float.PositiveInfinity;
				return num - FactorOffset;
			}
		}

		private float Progress => Mathf.Clamp01(manager.GetFactorFor(CurrentBillReport.Bill.recipe) / MaxSpeed);

		private float NormalizedProgress => Progress * 1.2f - 0.1f;

		protected override float ProductionSpeedFactor => (CurrentBillReport == null) ? FactorOffset : (manager.GetFactorFor(CurrentBillReport.Bill.recipe) + FactorOffset);

		private ModExtension_LearningAssembler ModExtensionLearningAssembler => ((Def)((Thing)this).def).GetModExtension<ModExtension_LearningAssembler>();

		QualityCategory ISetQualityDirectly.GetQuality(SkillDef relevantSkill)
		{
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			if (ModExtensionLearningAssembler == null)
			{
				Log.Error("Got a Building_Assembler_Learning without a modExtension_LearningAssembler, please report this error!");
				return (QualityCategory)2;
			}
			float num = (float)ModExtensionLearningAssembler.MaxQuality;
			float num2 = NormalizedProgress * num;
			float num3 = Rand.Gaussian(num2, 1.25f);
			num3 = Mathf.Clamp(num3, (float)ModExtensionLearningAssembler.MinQuality, (float)ModExtensionLearningAssembler.MaxQuality);
			return (QualityCategory)(byte)(int)num3;
		}

		protected override void Tick()
		{
			base.Tick();
			if (((Thing)this).Spawned && CurrentBillReport != null && Gen.IsHashIntervalTick((Thing)(object)this, 60) && base.Active && (ModExtensionLearningAssembler == null || !(MaxSpeed <= manager.GetFactorFor(CurrentBillReport.Bill.recipe))))
			{
				manager.IncreaseWeight(CurrentBillReport.Bill.recipe, 0.001f * CurrentBillReport.Bill.recipe.workSkillLearnFactor);
			}
		}

		public override string GetInspectString()
		{
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(base.GetInspectString());
			if (CurrentBillReport != null)
			{
				stringBuilder.AppendLine(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SALCurrentProductionSpeed", NamedArgument.op_Implicit(((Def)CurrentBillReport.Bill.recipe).label), NamedArgument.op_Implicit(GenText.ToStringPercent(ProductionSpeedFactor)))));
			}
			return GenText.TrimEndNewlines(stringBuilder.ToString());
		}

		protected override void PostProcessRecipeProduct(Thing thing)
		{
		}

		protected override IEnumerable<FloatMenuOption> GetDebugOptions()
		{
			foreach (FloatMenuOption debugOption in base.GetDebugOptions())
			{
				yield return debugOption;
			}
			yield return new FloatMenuOption("View active skills", (Action)delegate
			{
				//IL_0065: Unknown result type (might be due to invalid IL or missing references)
				manager.TrimUnnecessaryFactors();
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("Active skills ranked descending:");
				RecipeDef[] array = manager.factors.Keys.ToArray();
				WorkSpeedFactorEntry[] array2 = manager.factors.Values.ToArray();
				Array.Sort(array2, array);
				for (int num = array.Length - 1; num >= 0; num--)
				{
					stringBuilder.AppendLine($"{((Def)array[num]).LabelCap}: {array2[num].FactorFinal + FactorOffset}");
				}
				Log.Message(stringBuilder.ToString());
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
		}

		public override void ExposeData()
		{
			Scribe_Deep.Look<WorkSpeedFactorManager>(ref manager, "manager", Array.Empty<object>());
			base.ExposeData();
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if (manager == null)
			{
				manager = new WorkSpeedFactorManager();
			}
		}
	}
	internal class ModExtension_LearningAssembler : DefModExtension, IXMLThingDescription
	{
		public float MinSpeed = 0.5f;

		public float MaxSpeed = float.PositiveInfinity;

		public QualityCategory MinQuality = (QualityCategory)0;

		public QualityCategory MaxQuality = (QualityCategory)6;

		public string GetDescription(ThingDef def)
		{
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			//IL_0130: Unknown result type (might be due to invalid IL or missing references)
			//IL_0135: Unknown result type (might be due to invalid IL or missing references)
			//IL_013a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
			string empty = string.Empty;
			if (MinSpeed == MaxSpeed)
			{
				empty = TaggedString.op_Implicit(empty + TranslatorFormattedStringExtensions.Translate("PRF_UTD_ModExtension_LearningAssembler_Speed", NamedArgument.op_Implicit(MinSpeed * 100f)));
			}
			else
			{
				TaggedString val = Translator.Translate("PRF_UTD_ModExtension_LearningAssembler_PositiveInfinity");
				if (MaxSpeed < float.PositiveInfinity)
				{
					val = TaggedString.op_Implicit((MaxSpeed * 100f).ToString());
				}
				empty = TaggedString.op_Implicit(empty + TranslatorFormattedStringExtensions.Translate("PRF_UTD_ModExtension_LearningAssembler_Speed", NamedArgument.op_Implicit($"{MinSpeed * 100f}% - {val}")));
			}
			empty += "\r\n";
			empty = ((MinQuality != MaxQuality) ? TaggedString.op_Implicit(empty + TranslatorFormattedStringExtensions.Translate("PRF_UTD_ModExtension_LearningAssembler_Quality", NamedArgument.op_Implicit($"{MinQuality} - {MaxQuality}"))) : TaggedString.op_Implicit(empty + TranslatorFormattedStringExtensions.Translate("PRF_UTD_ModExtension_LearningAssembler_Quality", NamedArgument.op_Implicit(((object)System.Runtime.CompilerServices.Unsafe.As<QualityCategory, QualityCategory>(ref MinQuality)/*cast due to constrained. prefix*/).ToString()))));
			return empty + "\r\n";
		}
	}
	public sealed class WorkSpeedFactorEntry : IExposable, IComparable<WorkSpeedFactorEntry>
	{
		private int lastTick = 0;

		private float factorCached = 0f;

		private float learningRateCached = 3.3333333E-06f;

		public float LearningRate
		{
			get
			{
				return learningRateCached;
			}
			set
			{
				UpdateFactorCache();
				learningRateCached = value;
			}
		}

		public int DeltaTicks => Find.TickManager.TicksAbs - lastTick;

		public float FactorFinal
		{
			get
			{
				return factorCached * Mathf.Pow(2f, 0f - (float)DeltaTicks * LearningRate);
			}
			set
			{
				factorCached = value;
				lastTick = Find.TickManager.TicksAbs;
			}
		}

		public int CompareTo(WorkSpeedFactorEntry other)
		{
			return FactorFinal.CompareTo(other.FactorFinal);
		}

		public void ExposeData()
		{
			Scribe_Values.Look<int>(ref lastTick, "lastTick", 0, false);
			Scribe_Values.Look<float>(ref factorCached, "factorCached", 0f, false);
			Scribe_Values.Look<float>(ref learningRateCached, "learningRateCached", 0f, true);
		}

		private void UpdateFactorCache()
		{
			if (factorCached != 0f)
			{
				factorCached *= Mathf.Pow(2f, 0f - (float)DeltaTicks * LearningRate);
			}
			lastTick = Find.TickManager.TicksAbs;
		}
	}
	public class WorkSpeedFactorManager : IExposable
	{
		public const float LearningRateCachedDefault = 3.3333333E-06f;

		public Dictionary<RecipeDef, WorkSpeedFactorEntry> factors = new Dictionary<RecipeDef, WorkSpeedFactorEntry>();

		private float learningRateCached = 3.3333333E-06f;

		public virtual float LearningRate
		{
			get
			{
				return learningRateCached;
			}
			set
			{
				foreach (RecipeDef key in factors.Keys)
				{
					factors[key].LearningRate = value;
				}
				learningRateCached = value;
			}
		}

		public virtual void IncreaseWeight(RecipeDef recipe, float factor)
		{
			if (factors.TryGetValue(recipe, out var value))
			{
				value.FactorFinal += factor;
				return;
			}
			factors.Add(recipe, new WorkSpeedFactorEntry
			{
				FactorFinal = factor
			});
		}

		public virtual float GetFactorFor(RecipeDef recipe)
		{
			WorkSpeedFactorEntry value;
			return factors.TryGetValue(recipe, out value) ? value.FactorFinal : 0f;
		}

		public void TrimUnnecessaryFactors()
		{
			List<RecipeDef> list = factors.Keys.ToList();
			for (int i = 0; i < list.Count; i++)
			{
				RecipeDef key = list[i];
				if (factors[key].FactorFinal < 0.005f)
				{
					factors.Remove(key);
				}
			}
		}

		public virtual void ExposeData()
		{
			Scribe_Collections.Look<RecipeDef, WorkSpeedFactorEntry>(ref factors, "factors", (LookMode)4, (LookMode)2);
			Scribe_Values.Look<float>(ref learningRateCached, "learningRateCached", 3.3333333E-06f, false);
		}
	}
}
namespace ProjectRimFactory.SAL3.Exposables
{
	public class AssemblerDefModExtension : DefModExtension
	{
		public float workSpeedBaseFactor = 1f;

		public List<ThingDef> importRecipesFrom;

		public bool drawStatus = false;

		public bool doEffect = false;

		public List<RecipeEffecter> overrideRecipeEffecter = new List<RecipeEffecter>();

		public EffecterDef defaultEffecter;

		public SoundDef defaultSound;

		public GraphicData workingGraphicData;

		public GraphicData powerOffGraphicData;

		public Graphic WorkingGrahic
		{
			get
			{
				GraphicData obj = workingGraphicData;
				return (obj != null) ? obj.Graphic : null;
			}
		}

		public Graphic PowerOffGrahic
		{
			get
			{
				GraphicData obj = powerOffGraphicData;
				return (obj != null) ? obj.Graphic : null;
			}
		}

		private bool IsNothingEffectAndSound(RecipeDef recipe)
		{
			return recipe.effectWorking == null && recipe.soundWorking == null;
		}

		public EffecterDef GetEffecter(RecipeDef recipe)
		{
			if (!doEffect)
			{
				return null;
			}
			RecipeEffecter recipeEffecter = GenCollection.FirstOrDefault<RecipeEffecter>(overrideRecipeEffecter, (Predicate<RecipeEffecter>)((RecipeEffecter e) => e.recipe == recipe));
			return (recipeEffecter != null) ? recipeEffecter.effecter : (IsNothingEffectAndSound(recipe) ? defaultEffecter : recipe.effectWorking);
		}

		public SoundDef GetSound(RecipeDef recipe)
		{
			if (!doEffect)
			{
				return null;
			}
			RecipeEffecter recipeEffecter = GenCollection.FirstOrDefault<RecipeEffecter>(overrideRecipeEffecter, (Predicate<RecipeEffecter>)((RecipeEffecter e) => e.recipe == recipe));
			return (recipeEffecter != null) ? recipeEffecter.sound : (IsNothingEffectAndSound(recipe) ? defaultSound : recipe.soundWorking);
		}
	}
	public class RecipeEffecter
	{
		public RecipeDef recipe;

		public EffecterDef effecter;

		public SoundDef sound;

		public void LoadDataFromXmlCustom(XmlNode xmlRoot)
		{
			DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef((object)this, "recipe", xmlRoot.Name, (string)null, (string)null, (Type)null);
			if (xmlRoot.Attributes["Effecter"] != null)
			{
				DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef((object)this, "effecter", xmlRoot.Attributes["Effecter"].Value, (string)null, (string)null, (Type)null);
			}
			if (xmlRoot.Attributes["Sound"] != null)
			{
				DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef((object)this, "sound", xmlRoot.Attributes["Sound"].Value, (string)null, (string)null, (Type)null);
			}
		}
	}
}
namespace ProjectRimFactory.Misc
{
	public class CompProperties_Glower_ColorPick : CompProperties_Glower
	{
		public string key = "default";

		public List<KeyedColor> moreColors;

		public List<CompProperties_Glower_ColorPick> colorComps;

		public List<ResearchProjectDef> prerequisites = null;

		public int groupId = 711712;

		public CompProperties_Glower_ColorPick()
		{
			((CompProperties)this).compClass = typeof(CompGlower_ColorPick);
		}

		public override void ResolveReferences(ThingDef parentDef)
		{
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			((CompProperties)this).ResolveReferences(parentDef);
			colorComps = new List<CompProperties_Glower_ColorPick>(1) { this };
			if (GenList.NullOrEmpty<KeyedColor>((IList<KeyedColor>)moreColors))
			{
				return;
			}
			foreach (KeyedColor moreColor in moreColors)
			{
				CompProperties_Glower_ColorPick item = new CompProperties_Glower_ColorPick
				{
					overlightRadius = base.overlightRadius,
					glowRadius = base.glowRadius,
					glowColor = moreColor.color,
					key = moreColor.key,
					colorComps = colorComps
				};
				colorComps.Add(item);
			}
		}
	}
	public class KeyedColor
	{
		public string key;

		public ColorInt color;
	}
	public class CompGlower_ColorPick : CompGlower
	{
		public CompProperties_Glower_ColorPick Props => ((ThingComp)this).props as CompProperties_Glower_ColorPick;

		public override void PostExposeData()
		{
			((CompGlower)this).PostExposeData();
			string key = Props.key;
			string key2 = Props.colorComps[0].key;
			string text = key;
			Scribe_Values.Look<string>(ref text, "glower_color", key2, false);
			if (text != key)
			{
				ChangeColor(text);
			}
		}

		private void ChangeColor(string key)
		{
			if (key == Props.key)
			{
				return;
			}
			if (Props.prerequisites != null)
			{
				for (int i = 0; i < Props.prerequisites.Count; i++)
				{
					if (!Props.prerequisites[i].IsFinished)
					{
						return;
					}
				}
			}
			bool flag = false;
			foreach (CompProperties_Glower_ColorPick colorComp in Props.colorComps)
			{
				if (colorComp.key == key)
				{
					((ThingComp)this).props = (CompProperties)(object)colorComp;
					flag = true;
					break;
				}
			}
			if (!flag || !((Thing)((ThingComp)this).parent).Spawned)
			{
				return;
			}
			GlowGrid val = ((Thing)((ThingComp)this).parent).Map?.glowGrid;
			if (val != null)
			{
				FieldInfo field = typeof(GlowGrid).GetField("litGlowers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
				HashSet<CompGlower> hashSet = (HashSet<CompGlower>)field.GetValue(val);
				if (hashSet.Contains((CompGlower)(object)this))
				{
					((Thing)((ThingComp)this).parent).Map.glowGrid.DeRegisterGlower((CompGlower)(object)this);
					((Thing)((ThingComp)this).parent).Map.glowGrid.RegisterGlower((CompGlower)(object)this);
				}
			}
		}

		private static void ChangeColorAllSelected(string key)
		{
			List<object> selectedObjects = Find.Selector.SelectedObjects;
			if (GenList.NullOrEmpty<object>((IList<object>)selectedObjects) || selectedObjects.Count < 2)
			{
				return;
			}
			foreach (object item in selectedObjects)
			{
				object obj = ((item is ThingWithComps) ? item : null);
				CompGlower_ColorPick compGlower_ColorPick = ((obj != null) ? ((ThingWithComps)obj).GetComp<CompGlower_ColorPick>() : null);
				if (compGlower_ColorPick == null)
				{
					continue;
				}
				foreach (CompProperties_Glower_ColorPick colorComp in compGlower_ColorPick.Props.colorComps)
				{
					if (colorComp.key == key)
					{
						compGlower_ColorPick.ChangeColor(key);
						break;
					}
				}
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			if (Props.colorComps == null || Props.colorComps.Count < 2)
			{
				yield break;
			}
			if (Props.prerequisites != null)
			{
				for (int i = 0; i < Props.prerequisites.Count; i++)
				{
					if (!Props.prerequisites[i].IsFinished)
					{
						yield break;
					}
				}
			}
			Color tmpColor = ((ColorInt)(ref ((CompProperties_Glower)Props).glowColor)).ToColor;
			tmpColor.a = 0.75f;
			yield return (Gizmo)new Command_Action
			{
				defaultLabel = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF_ChangeColorGizmo", NamedArgument.op_Implicit(Translator.Translate(Props.key)))),
				defaultIconColor = tmpColor,
				groupKey = Props.groupId,
				icon = (Texture)(object)ContentFinder<Texture2D>.Get("PRFUi/Lamp", true),
				action = delegate
				{
					//IL_0042: Unknown result type (might be due to invalid IL or missing references)
					//IL_0064: Unknown result type (might be due to invalid IL or missing references)
					//IL_006e: Expected O, but got Unknown
					//IL_0090: Unknown result type (might be due to invalid IL or missing references)
					//IL_009a: Expected O, but got Unknown
					List<FloatMenuOption> list = new List<FloatMenuOption>();
					foreach (CompProperties_Glower_ColorPick c in Props.colorComps)
					{
						list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate(c.key)), (Action)delegate
						{
							ChangeColor(c.key);
							ChangeColorAllSelected(c.key);
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					}
					Find.WindowStack.Add((Window)new FloatMenu(list));
				}
			};
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((CompGlower)this).CompGetGizmosExtra();
		}
	}
}
namespace ProjectRimFactory.Industry
{
	public class Building_AtomicReconstructor : Building
	{
		private ThingDef thingToGenerate;

		private int progressTicks;

		public int speedFactor = 1;

		private CompPowerTrader powerComp;

		private CompRefuelable refuelableComp;

		private CompOutputAdjustable outputComp;

		public int PaperclipConsumptionFactor => speedFactor * speedFactor;

		private int TotalWorkRequired
		{
			get
			{
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				if (ThingToGenerate == null)
				{
					return 0;
				}
				return Mathf.RoundToInt(StatDefOf.MarketValue.Worker.GetValue(StatRequest.For((BuildableDef)(object)ThingToGenerate, (ThingDef)null, (QualityCategory)2), true) * 100f * 2f);
			}
		}

		public float ItemBaseCost
		{
			get
			{
				if (ThingToGenerate == null)
				{
					return 0f;
				}
				return ThingToGenerate.PaperclipAmount() * (float)PaperclipConsumptionFactor;
			}
		}

		public float FuelConsumptionPerTick
		{
			get
			{
				if (ThingToGenerate == null)
				{
					return 0f;
				}
				return ItemBaseCost * (float)speedFactor / (float)TotalWorkRequired;
			}
		}

		public string ProgressToStringPercent => (ThingToGenerate == null) ? GenText.ToStringPercent(0f) : GenText.ToStringPercent((float)progressTicks / (float)TotalWorkRequired);

		public string EstimatedProductionTimeLeftPeriod => GenDate.ToStringTicksToPeriod((TotalWorkRequired - progressTicks) / speedFactor, true, false, true, true, false);

		public ThingDef ThingToGenerate
		{
			get
			{
				return thingToGenerate;
			}
			set
			{
				thingToGenerate = value;
				progressTicks = 0;
			}
		}

		public override void PostMake()
		{
			((ThingWithComps)this).PostMake();
			powerComp = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			refuelableComp = ((ThingWithComps)this).GetComp<CompRefuelable>();
			outputComp = ((ThingWithComps)this).GetComp<CompOutputAdjustable>();
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			powerComp = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			refuelableComp = ((ThingWithComps)this).GetComp<CompRefuelable>();
			outputComp = ((ThingWithComps)this).GetComp<CompOutputAdjustable>();
		}

		protected override void Tick()
		{
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).Tick();
			if (!((Thing)this).Spawned || !powerComp.PowerOn || ThingToGenerate == null)
			{
				return;
			}
			float fuel = refuelableComp.Fuel;
			if (fuel >= FuelConsumptionPerTick)
			{
				refuelableComp.ConsumeFuel(FuelConsumptionPerTick);
				progressTicks += speedFactor;
				if (progressTicks >= TotalWorkRequired)
				{
					Thing val = ThingMaker.MakeThing(ThingToGenerate, (ThingDef)null);
					GenPlace.TryPlaceThing(val, outputComp.CurrentCell, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					progressTicks = 0;
				}
			}
		}

		public override string GetInspectString()
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			string inspectString = ((ThingWithComps)this).GetInspectString();
			if (!string.IsNullOrEmpty(inspectString))
			{
				stringBuilder.AppendLine(inspectString);
			}
			stringBuilder.Append(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("AtomicReconstructorProgress", NamedArgument.op_Implicit(ProgressToStringPercent))));
			return GenText.TrimEndNewlines(stringBuilder.ToString());
		}

		public override void ExposeData()
		{
			((Building)this).ExposeData();
			Scribe_Defs.Look<ThingDef>(ref thingToGenerate, "thingToGenerate");
			Scribe_Values.Look<int>(ref progressTicks, "progressTicks", 0, false);
			Scribe_Values.Look<int>(ref speedFactor, "speedFactor", 1, false);
		}

		public override void DrawGUIOverlay()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Invalid comparison between Unknown and I4
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).DrawGUIOverlay();
			if ((int)Find.CameraDriver.CurrentZoom < 2)
			{
				Vector2 val = GenMapUI.LabelDrawPosFor((Thing)(object)this, 0f);
				ThingDef obj = thingToGenerate;
				GenMapUI.DrawThingLabel(val, TaggedString.op_Implicit((obj != null) ? ((Def)obj).LabelCap : Translator.Translate("AssemblerIdle")), Color.white);
			}
		}
	}
	[StaticConstructorOnStartup]
	public class Building_CustomBattery : Building
	{
		private CompPowerBattery compPowerBattery;

		private int ticksToExplode;

		private Sustainer wickSustainer;

		private static readonly Vector2 BarSize = new Vector2(1.25f, 0.35f);

		private static readonly Material BatteryBarFilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(1f, 1f, 1f), false);

		private static readonly Material BatteryBarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0f, 0f, 0f, 0f), false);

		public override void ExposeData()
		{
			((Building)this).ExposeData();
			Scribe_Values.Look<int>(ref ticksToExplode, "ticksToExplode", 0, false);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			compPowerBattery = ((ThingWithComps)this).GetComp<CompPowerBattery>();
		}

		public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Invalid comparison between Unknown and I4
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			((Thing)this).DynamicDrawPhaseAt(phase, drawLoc, flip);
			if ((int)phase == 2)
			{
				GenDraw.DrawFillableBar(new FillableBarRequest
				{
					center = ((Thing)this).DrawPos + Vector3.up * 0.1f + Vector3.forward * 0.25f,
					size = BarSize,
					fillPercent = compPowerBattery.StoredEnergy / compPowerBattery.Props.storedEnergyMax,
					filledMat = BatteryBarFilledMat,
					unfilledMat = BatteryBarUnfilledMat,
					margin = 0.15f,
					rotation = ((Thing)this).Rotation
				});
				if (ticksToExplode > 0 && ((Thing)this).Spawned)
				{
					((Thing)this).Map.overlayDrawer.DrawOverlay((Thing)(object)this, (OverlayTypes)4);
				}
			}
		}

		protected override void Tick()
		{
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).Tick();
			if (((Thing)this).Spawned && ticksToExplode > 0)
			{
				if (wickSustainer == null)
				{
					StartWickSustainer();
				}
				else
				{
					wickSustainer.Maintain();
				}
				ticksToExplode--;
				if (ticksToExplode == 0)
				{
					CellRect val = GenAdj.OccupiedRect((Thing)(object)this);
					IntVec3 randomCell = ((CellRect)(ref val)).RandomCell;
					float num = Rand.Range(0.5f, 1f) * 3f;
					GenExplosion.DoExplosion(randomCell, ((Thing)this).Map, num, DamageDefOf.Flame, (Thing)null, -1, -1f, (SoundDef)null, (ThingDef)null, (ThingDef)null, (Thing)null, (ThingDef)null, 0f, 1, (GasType?)null, (float?)null, 255, false, (ThingDef)null, 0f, 1, 0f, false, (float?)null, (List<Thing>)null, (FloatRange?)null, true, 1f, 0f, true, (ThingDef)null, 1f, (SimpleCurve)null, (List<IntVec3>)null, (ThingDef)null, (ThingDef)null);
					compPowerBattery.DrawPower(400f);
				}
			}
		}

		public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).PostApplyDamage(dinfo, totalDamageDealt);
			if (!((Thing)this).Destroyed && ticksToExplode == 0 && ((DamageInfo)(ref dinfo)).Def == DamageDefOf.Flame && Rand.Value < 0.05f && compPowerBattery.StoredEnergy > 500f)
			{
				ticksToExplode = Rand.Range(70, 150);
				StartWickSustainer();
			}
		}

		private void StartWickSustainer()
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			SoundInfo val = SoundInfo.InMap(TargetInfo.op_Implicit((Thing)(object)this), (MaintenanceType)1);
			wickSustainer = SoundStarter.TrySpawnSustainer(SoundDefOf.HissSmall, val);
		}
	}
	public class Building_DeepQuarry : Building, IXMLThingDescription
	{
		private static IEnumerable<ThingDef> cachedPossibleRockDefCandidates;

		private int productionTime;

		private CompFlickable flick;

		private CompPowerTrader power;

		private CompRefuelable fuel;

		private int producedChunksTotal;

		private float GetProduceMtbHours => ((Def)((Thing)this).def).GetModExtension<DeepQuarryDefModExtension>().TickCount;

		private static IEnumerable<ThingDef> PossibleRockDefCandidates
		{
			get
			{
				if (cachedPossibleRockDefCandidates != null)
				{
					return cachedPossibleRockDefCandidates;
				}
				return cachedPossibleRockDefCandidates = DefDatabase<ThingDef>.AllDefs.Where((ThingDef def) => def.building != null && def.building.isNaturalRock && def.building.mineableThing != null);
			}
		}

		private ThingDef getRock => GenCollection.RandomElementByWeight<ThingDef>(GetPossibleRockDefCandidatesFilterd(((Thing)this).def), (Func<ThingDef, float>)((ThingDef d) => d.building.isResourceRock ? (d.building.mineableScatterCommonality * ((IntRange)(ref d.building.mineableScatterLumpSizeRange)).Average * d.building.mineableDropChance) : 3f));

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			flick = ((ThingWithComps)this).GetComp<CompFlickable>();
			power = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			fuel = ((ThingWithComps)this).GetComp<CompRefuelable>();
		}

		private IEnumerable<ThingDef> GetPossibleRockDefCandidatesFilterd(ThingDef thingDef)
		{
			return PossibleRockDefCandidates.Where(delegate(ThingDef d)
			{
				ModExtension_Miner modExtension = ((Def)thingDef).GetModExtension<ModExtension_Miner>();
				return modExtension == null || !modExtension.IsExcluded(d.building.mineableThing);
			});
		}

		private void HandelTick(int numTicks, bool consumeFuelWhileRunning)
		{
			CompFlickable val = flick;
			if (val != null && !val.SwitchIsOn)
			{
				return;
			}
			CompPowerTrader val2 = power;
			if (val2 != null && !val2.PowerOn)
			{
				return;
			}
			if (fuel != null)
			{
				if (consumeFuelWhileRunning && !fuel.Props.consumeFuelOnlyWhenUsed)
				{
					fuel.ConsumeFuel(fuel.Props.fuelConsumptionRate / 6000f * (float)numTicks);
				}
				if (fuel.HasFuel)
				{
					TryGenerateResource(numTicks);
				}
			}
			else
			{
				TryGenerateResource(numTicks);
			}
		}

		protected override void Tick()
		{
			((ThingWithComps)this).Tick();
			if (((Thing)this).Spawned)
			{
				HandelTick(1, consumeFuelWhileRunning: false);
			}
		}

		public override void TickRare()
		{
			((ThingWithComps)this).TickRare();
			if (((Thing)this).Spawned)
			{
				HandelTick(250, consumeFuelWhileRunning: true);
			}
		}

		public override void TickLong()
		{
			((ThingWithComps)this).TickLong();
			if (((Thing)this).Spawned)
			{
				HandelTick(2000, consumeFuelWhileRunning: true);
			}
		}

		private void TryGenerateResource(int ticksInInterval)
		{
			productionTime += ticksInInterval;
			if (productionTime >= 2000)
			{
				productionTime = 0;
				if (Rand.MTBEventOccurs(GetProduceMtbHours, 2500f, 2000f))
				{
					GenerateChunk();
				}
			}
		}

		public override void ExposeData()
		{
			Scribe_Values.Look<int>(ref producedChunksTotal, "producedTotal", 0, false);
			Scribe_Values.Look<int>(ref productionTime, "PRF_productionTime", 0, false);
			((Building)this).ExposeData();
		}

		protected virtual void GenerateChunk()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			GenPlace.TryPlaceThing(GetChunkThingToPlace(), ((ThingWithComps)this).GetComp<CompOutputAdjustable>().CurrentCell, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			producedChunksTotal++;
		}

		protected virtual Thing GetChunkThingToPlace()
		{
			ThingDef val = getRock;
			List<Thing> list = new List<Thing>();
			((Def)((Thing)this).def).GetModExtension<ModExtension_ModifyProduct>()?.ProcessProducts(list, (IBillGiver)(object)((this is IBillGiver) ? this : null), (Thing)(object)this);
			if (list.Count > 0)
			{
				return list[0];
			}
			Thing val2 = ThingMaker.MakeThing(val.building.mineableThing, (ThingDef)null);
			val2.stackCount = val.building.mineableYield;
			return val2;
		}

		public override string GetInspectString()
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			string inspectString = ((ThingWithComps)this).GetInspectString();
			if (!string.IsNullOrEmpty(inspectString))
			{
				stringBuilder.AppendLine(inspectString);
			}
			stringBuilder.Append(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DeepQuarry_TotalSoFar", NamedArgument.op_Implicit(producedChunksTotal))));
			return GenText.TrimEndNewlines(stringBuilder.ToString());
		}

		public string GetDescription(ThingDef def)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			List<ThingDef> list = GetPossibleRockDefCandidatesFilterd(def).ToList();
			TaggedString val = Translator.Translate("PRF_DescriptionUpdate_CanMine");
			foreach (ThingDef item in list)
			{
				val += $"    - {((Def)item).LabelCap} x{item.building.mineableYield}\r\n";
			}
			val += "\r\n";
			return TaggedString.op_Implicit(val);
		}
	}
	public class Building_FuelingMachine : Building
	{
		protected class RegisteredRefuelable
		{
			public readonly Type buildingType;

			public readonly Func<Building, object> objectThatNeedsFueling;

			public readonly Func<object, Thing, int> fuelTest;

			public readonly Action<object, Thing> refuelAction;

			public RegisteredRefuelable(Type b, Func<Building, object> nf, Func<object, Thing, int> test, Action<object, Thing> a)
			{
				buildingType = b;
				objectThatNeedsFueling = nf;
				fuelTest = test;
				refuelAction = a;
			}
		}

		protected static List<RegisteredRefuelable> registeredRefuelables = new List<RegisteredRefuelable>();

		private IntVec3 FuelableCell
		{
			get
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_0009: Unknown result type (might be due to invalid IL or missing references)
				//IL_000f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				Rot4 rotation = ((Thing)this).Rotation;
				return ((Rot4)(ref rotation)).FacingCell + ((Thing)this).Position;
			}
		}

		protected IEnumerable<Thing> AllThingsForFueling
		{
			get
			{
				foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way((Thing)(object)this))
				{
					foreach (Thing item in cell.AllThingsInCellForUse(((Thing)this).Map, allowStorageZones: false))
					{
						yield return item;
					}
				}
			}
		}

		protected override void Tick()
		{
			((ThingWithComps)this).Tick();
			if (((Thing)this).Spawned && Gen.IsHashIntervalTick((Thing)(object)this, 10))
			{
				Refuel();
			}
		}

		public override void TickRare()
		{
			((ThingWithComps)this).TickRare();
			if (((Thing)this).Spawned)
			{
				Refuel();
			}
		}

		private void Refuel()
		{
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			if (!((ThingWithComps)this).GetComp<CompPowerTrader>().PowerOn)
			{
				return;
			}
			CompRefuelable val = null;
			List<Thing> collection = ((Thing)this).Map.thingGrid.ThingsListAt(FuelableCell);
			List<Thing> list = new List<Thing>(collection);
			foreach (Thing item in list)
			{
				Building val2 = (Building)(object)((item is Building) ? item : null);
				if (val2 != null)
				{
					val = ((ThingWithComps)val2).GetComp<CompRefuelable>();
					if (val != null && val.Fuel + 0.9999f < val.TargetFuelLevel)
					{
						foreach (Thing item2 in AllThingsForFueling)
						{
							if (val.Props.fuelFilter.Allows(item2))
							{
								int num = Mathf.Min(item2.stackCount, Mathf.FloorToInt(val.TargetFuelLevel - val.Fuel));
								if (num > 0)
								{
									val.Refuel((float)num);
									item2.SplitOff(num).Destroy((DestroyMode)0);
								}
								else
								{
									val.Refuel(1f);
									item2.SplitOff(1).Destroy((DestroyMode)0);
								}
								if (val.Fuel + 0.9999f >= val.TargetFuelLevel)
								{
									break;
								}
							}
						}
					}
				}
				while (true)
				{
					IL_0174:
					foreach (RegisteredRefuelable registeredRefuelable in registeredRefuelables)
					{
						if (!registeredRefuelable.buildingType.IsAssignableFrom(((object)item).GetType()))
						{
							continue;
						}
						object obj = registeredRefuelable.objectThatNeedsFueling((Building)(object)((item is Building) ? item : null));
						if (obj == null)
						{
							continue;
						}
						foreach (Thing item3 in AllThingsForFueling)
						{
							int num2 = registeredRefuelable.fuelTest(obj, item3);
							if (num2 <= 0)
							{
								continue;
							}
							Thing arg = item3.SplitOff(num2);
							registeredRefuelable.refuelAction(obj, arg);
							goto IL_0174;
						}
					}
					break;
				}
			}
		}

		public override void DrawExtraSelectionOverlays()
		{
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).DrawExtraSelectionOverlays();
			GenDraw.DrawFieldEdges(new List<IntVec3>(GenAdj.CellsAdjacent8Way((Thing)(object)this)), 2900);
			GenDraw.DrawFieldEdges(new List<IntVec3> { FuelableCell }, Color.yellow, (float?)null, (HashSet<IntVec3>)null, 2900);
		}

		public static void RegisterRefuelable(Type buildingType, Func<Building, object> objectThatNeedsFueling, Func<object, Thing, int> fuelTest, Action<object, Thing> refuelAction)
		{
			registeredRefuelables.Add(new RegisteredRefuelable(buildingType, objectThatNeedsFueling, fuelTest, refuelAction));
		}
	}
	[StaticConstructorOnStartup]
	internal static class RegisterVanillaMortarsAsRefuelable
	{
		static RegisterVanillaMortarsAsRefuelable()
		{
			if (!ModLister.HasActiveModWithName("Project RimFactory Revived") && !ModLister.HasActiveModWithName("Project RimFactory Lite"))
			{
				return;
			}
			Type type = Type.GetType("ProjectRimFactory.Industry.Building_FuelingMachine, ProjectRimFactory", throwOnError: false);
			if (type == null)
			{
				Log.Warning("PRF failed to load compatibility for PRF; auto loading mortars won't work");
				return;
			}
			type.GetMethod("RegisterRefuelable", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[4]
			{
				typeof(Building_TurretGun),
				new Func<Building, object>(FindCompNeedsShells),
				(Func<object, Thing, int>)delegate(object c, Thing t)
				{
					CompChangeableProjectile val = (CompChangeableProjectile)((c is CompChangeableProjectile) ? c : null);
					return val.allowedShellsSettings.filter.Allows(t) ? 1 : 0;
				},
				(Action<object, Thing>)delegate(object c, Thing t)
				{
					((CompChangeableProjectile)((c is CompChangeableProjectile) ? c : null)).LoadShell(t.def, 1);
					t.Destroy((DestroyMode)0);
				}
			});
		}

		private static object FindCompNeedsShells(Building b)
		{
			Building obj = ((b is Building_TurretGun) ? b : null);
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				Thing gun = ((Building_TurretGun)obj).gun;
				obj2 = ((gun != null) ? ThingCompUtility.TryGetComp<CompChangeableProjectile>(gun) : null);
			}
			CompChangeableProjectile val = (CompChangeableProjectile)obj2;
			return (val != null && !val.Loaded) ? val : null;
		}
	}
	public class Building_PaperclipDuplicator : Building
	{
		private long paperclipCount;

		private int lastTick = Find.TickManager.TicksGame;

		public Building_MassStorageUnit BoundStorageUnit;

		public long PaperclipsActual
		{
			get
			{
				//IL_005f: Unknown result type (might be due to invalid IL or missing references)
				//IL_006d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0077: Expected O, but got Unknown
				long result = 0L;
				if (paperclipCount == long.MaxValue)
				{
					return result;
				}
				try
				{
					result = checked((long)((double)paperclipCount * Math.Pow(1.05, GenDate.TicksToDays(Find.TickManager.TicksGame - lastTick))));
				}
				catch (OverflowException)
				{
					Find.WindowStack.Add((Window)new Dialog_MessageBox(Translator.Translate("PRF_ArchoCipher_BankOverflow"), (string)null, (Action)null, (string)null, (Action)null, (string)null, false, (Action)null, (Action)null, (WindowLayer)1));
					PaperclipsActual = long.MaxValue;
					result = long.MaxValue;
				}
				return result;
			}
			private set
			{
				paperclipCount = value;
				lastTick = Find.TickManager.TicksGame;
			}
		}

		public virtual void DepositPaperclips(int count)
		{
			PaperclipsActual += count;
		}

		public virtual void WithdrawPaperclips(int count)
		{
			PaperclipsActual -= count;
		}

		public override string GetInspectString()
		{
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			string inspectString = ((ThingWithComps)this).GetInspectString();
			if (!string.IsNullOrEmpty(inspectString))
			{
				stringBuilder.AppendLine(inspectString);
			}
			stringBuilder.AppendLine(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PaperclipsInDuplicator", NamedArgument.op_Implicit(PaperclipsActual.ToString()))));
			if (BoundStorageUnit != null)
			{
				stringBuilder.AppendLine(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PaperclipsInStorageUnit", NamedArgument.op_Implicit(BoundStorageUnit.StoredItems.Where((Thing t) => t.def == PRFDefOf.Paperclip).Sum((Thing t) => t.stackCount)))));
			}
			else
			{
				stringBuilder.AppendLine(TaggedString.op_Implicit(Translator.Translate("PRFNoBoundStorageUnit")));
			}
			return GenText.TrimEndNewlines(stringBuilder.ToString());
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			if (!Prefs.DevMode)
			{
				yield break;
			}
			yield return (Gizmo)new Command_Action
			{
				defaultLabel = "DEBUG: Double paperclip amount",
				action = checked(delegate
				{
					//IL_003d: Unknown result type (might be due to invalid IL or missing references)
					//IL_004b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0055: Expected O, but got Unknown
					try
					{
						if (PaperclipsActual != long.MaxValue)
						{
							PaperclipsActual *= 2L;
						}
					}
					catch (OverflowException)
					{
						Find.WindowStack.Add((Window)new Dialog_MessageBox(Translator.Translate("PRF_ArchoCipher_BankOverflow"), (string)null, (Action)null, (string)null, (Action)null, (string)null, false, (Action)null, (Action)null, (WindowLayer)1));
						PaperclipsActual = long.MaxValue;
					}
				})
			};
		}

		public override void ExposeData()
		{
			((Building)this).ExposeData();
			Scribe_Values.Look<long>(ref paperclipCount, "paperclipCount", 0L, false);
			Scribe_Values.Look<int>(ref lastTick, "lastTick", 0, false);
			Scribe_References.Look<Building_MassStorageUnit>(ref BoundStorageUnit, "boundStorageUnit", false);
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
	public class Building_PaperclipFactory : Building_SimpleAssembler
	{
		private const float PaperclipsPerKilogramModifier = 0.25f;

		protected override void PostProcessRecipeProduct(Thing thing)
		{
			int stackLimit = thing.def.stackLimit;
			int num = Mathf.RoundToInt(CurrentBillReport.Selected.Sum((Thing t) => t.PaperclipAmount() * 0.25f));
			if (num <= stackLimit)
			{
				thing.stackCount = num;
				return;
			}
			thing.stackCount = stackLimit;
			num -= stackLimit;
			while (num > 0)
			{
				int num2 = Math.Min(num, stackLimit);
				Thing val = ThingMaker.MakeThing(thing.def, (ThingDef)null);
				val.stackCount = num2;
				ThingQueue.Add(val);
				num -= num2;
			}
		}
	}
	public class Building_RepairingMachine : Building_SimpleAssembler
	{
		public class Recipe_Repair : RecipeWorker
		{
			public override void ConsumeIngredient(Thing ingredient, RecipeDef _, Map map)
			{
				if (ingredient.def == PRFDefOf.Paperclip)
				{
					ingredient.Destroy((DestroyMode)0);
				}
				else
				{
					ingredient.HitPoints = ingredient.MaxHitPoints;
				}
			}
		}

		private const int TicksPerHitPoint = 60;

		protected override void Notify_BillStarted()
		{
			base.Notify_BillStarted();
			Thing val = CurrentBillReport.Selected.Find((Thing t) => t.def != PRFDefOf.Paperclip);
			CurrentBillReport.WorkLeft = (val.MaxHitPoints - val.HitPoints) * 60;
		}
	}
	[StaticConstructorOnStartup]
	public class CompPaperclipPowerPlant : CompPowerPlant
	{
		private static readonly Texture2D SetTargetFuelLevelCommand = ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel", true);

		private int fuelPerSecond = 1;

		private int currentPowerModifierPct = 100;

		private int maxPowerModifierPct = 100;

		protected float PowerProductionModifier => currentPowerModifierPct * fuelPerSecond / 10;

		protected override float DesiredPowerOutput => (0f - (float)ReflectionUtility.CompPropertiesPowerBasePowerConsumption.GetValue(((CompPower)this).Props)) * PowerProductionModifier;

		public override void ReceiveCompSignal(string signal)
		{
			((CompPowerTrader)this).ReceiveCompSignal(signal);
			if ((signal == "RanOutOfFuel" || signal == "FlickedOff") ? true : false)
			{
				currentPowerModifierPct = 100;
			}
		}

		public override void CompTick()
		{
			((CompPowerPlant)this).CompTick();
			if (Gen.IsHashIntervalTick((Thing)(object)((ThingComp)this).parent, 60) && ((ThingComp)this).parent.GetComp<CompRefuelable>().HasFuel && ((ThingComp)this).parent.GetComp<CompFlickable>().SwitchIsOn)
			{
				if (PRFDefOf.PaperclipGeneratorQuantumFoamManipulation.IsFinished)
				{
					maxPowerModifierPct = 2500;
				}
				else if (PRFDefOf.PaperclipGeneratorKugelblitz.IsFinished)
				{
					maxPowerModifierPct = 1000;
				}
				else if (PRFDefOf.PaperclipGeneratorSelfImprovement.IsFinished)
				{
					maxPowerModifierPct = 500;
				}
				else
				{
					maxPowerModifierPct = 100;
				}
				if (maxPowerModifierPct < currentPowerModifierPct)
				{
					currentPowerModifierPct = maxPowerModifierPct;
				}
				else if (currentPowerModifierPct < maxPowerModifierPct)
				{
					currentPowerModifierPct++;
				}
				((ThingComp)this).parent.GetComp<CompRefuelable>().ConsumeFuel((float)fuelPerSecond);
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			yield return (Gizmo)new Command_Action
			{
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("SetPaperclipConsumptionPerSecond")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("SetPaperclipConsumptionPerSecond_Desc")),
				icon = (Texture)(object)SetTargetFuelLevelCommand,
				action = delegate
				{
					//IL_003e: Unknown result type (might be due to invalid IL or missing references)
					//IL_0048: Expected O, but got Unknown
					Find.WindowStack.Add((Window)new Dialog_Slider((Func<int, string>)((int j) => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PaperclipFuelConsumption", NamedArgument.op_Implicit(j)))), 1, 100, (Action<int>)delegate(int i)
					{
						fuelPerSecond = i;
					}, fuelPerSecond, 1f));
				}
			};
		}

		public override string CompInspectStringExtra()
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(((CompPowerTrader)this).CompInspectStringExtra());
			stringBuilder.AppendLine(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PaperclipGeneratorEfficiency", NamedArgument.op_Implicit(currentPowerModifierPct), NamedArgument.op_Implicit(maxPowerModifierPct))));
			int num = (int)(((ThingComp)this).parent.GetComp<CompRefuelable>().Fuel / (float)fuelPerSecond * 60f);
			stringBuilder.Append(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PaperclipsRunOutIn", NamedArgument.op_Implicit(GenDate.ToStringTicksToPeriod(num, true, false, true, true, false)))));
			return stringBuilder.ToString();
		}

		public override void PostExposeData()
		{
			((CompPowerTrader)this).PostExposeData();
			Scribe_Values.Look<int>(ref fuelPerSecond, "fuelPerSecond", 1, false);
			Scribe_Values.Look<int>(ref currentPowerModifierPct, "currentPowerModifier", 100, false);
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((CompPowerTrader)this).CompGetGizmosExtra();
		}
	}
	public class DefModExtension_AtomicReconstructorDisallow : DefModExtension
	{
	}
	public class DefModExtension_AtomicReconstructorResearchPrerequisite : DefModExtension
	{
		public bool ignoreMainPrerequisites;

		public List<ResearchProjectDef> prerequisites;
	}
	public static class PaperclipsUtility
	{
		public static float PaperclipAmount(this Thing t)
		{
			return StatExtension.GetStatValue(t, StatDefOf.Mass, true, -1) * (float)t.stackCount * 1000f;
		}

		public static float PaperclipAmount(this ThingDef tDef, ThingDef stuff = null)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			return StatDefOf.Mass.Worker.GetValue(StatRequest.For((BuildableDef)(object)tDef, stuff, (QualityCategory)2), true) * 1000f;
		}
	}
	public class PlaceWorker_FuelingMachine : PlaceWorker
	{
		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			((PlaceWorker)this).DrawGhost(def, center, rot, ghostCol, thing);
			GenDraw.DrawFieldEdges(new List<IntVec3>(1) { ((Rot4)(ref rot)).FacingCell + center }, Color.yellow, (float?)null, (HashSet<IntVec3>)null, 2900);
		}
	}
	public class DeepQuarryDefModExtension : DefModExtension
	{
		public float TickCount = 6f;
	}
}
namespace ProjectRimFactory.Industry.UI
{
	public class ITab_AtomicReconstruction : ITab
	{
		private readonly Comparer<ThingDef> thingDefComparer = Comparer<ThingDef>.Create(delegate(ThingDef first, ThingDef second)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			TaggedString labelCap = ((Def)first).LabelCap;
			string rawText = ((TaggedString)(ref labelCap)).RawText;
			labelCap = ((Def)second).LabelCap;
			return rawText.CompareTo(((TaggedString)(ref labelCap)).RawText);
		});

		private readonly string nowProducing;

		private readonly string paperclipCost;

		private readonly string consumptionPerSecond;

		private readonly string progress;

		private readonly string noneBrackets;

		private Vector2 scrollPos;

		private float scrollViewHeight;

		private string searchQuery;

		private Building_AtomicReconstructor SelBuilding => (Building_AtomicReconstructor)(object)((ITab)this).SelThing;

		public ITab_AtomicReconstruction()
		{
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
			TaggedString val = Translator.Translate("AtomicReconstructionTab_NowProducing");
			nowProducing = ((TaggedString)(ref val)).RawText;
			val = Translator.Translate("AtomicReconstructionTab_PaperclipCost");
			paperclipCost = ((TaggedString)(ref val)).RawText;
			val = Translator.Translate("AtomicReconstructionTab_ConsumptionPerSecond");
			consumptionPerSecond = ((TaggedString)(ref val)).RawText;
			val = Translator.Translate("AtomicReconstructionTab_Progress");
			progress = ((TaggedString)(ref val)).RawText;
			val = Translator.Translate("NoneBrackets");
			noneBrackets = ((TaggedString)(ref val)).RawText;
			((ITab)this)..ctor();
			((InspectTabBase)this).size = new Vector2(400f, 400f);
			((InspectTabBase)this).labelKey = "PRFAtomicReconstructionTab";
		}

		protected override void FillTab()
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Expected O, but got Unknown
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_012b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0130: Unknown result type (might be due to invalid IL or missing references)
			//IL_013b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_0217: Unknown result type (might be due to invalid IL or missing references)
			//IL_0228: Unknown result type (might be due to invalid IL or missing references)
			//IL_0230: Unknown result type (might be due to invalid IL or missing references)
			//IL_0271: Unknown result type (might be due to invalid IL or missing references)
			//IL_0342: Unknown result type (might be due to invalid IL or missing references)
			//IL_0348: Invalid comparison between Unknown and I4
			Rect val = GenUI.ContractedBy(new Rect(0f, 0f, ((InspectTabBase)this).size.x, ((InspectTabBase)this).size.y), 10f);
			Listing_Standard val2 = new Listing_Standard((GameFont)1);
			((Listing)val2).Begin(val);
			val2.Label(((ITab)this).SelThing.LabelCapNoCount, -1f, (TipSignal?)null);
			string text = nowProducing;
			ThingDef thingToGenerate = SelBuilding.ThingToGenerate;
			val2.LabelDouble(text, TaggedString.op_Implicit((thingToGenerate != null) ? ((Def)thingToGenerate).LabelCap : TaggedString.op_Implicit(noneBrackets)), (string)null);
			val2.LabelDouble(paperclipCost, GenText.ToStringDecimalIfSmall(SelBuilding.ItemBaseCost), (string)null);
			val2.LabelDouble(consumptionPerSecond, GenText.ToStringDecimalIfSmall(SelBuilding.FuelConsumptionPerTick * 60f), (string)null);
			val2.LabelDouble(progress, SelBuilding.ProgressToStringPercent + " (" + SelBuilding.EstimatedProductionTimeLeftPeriod + ")", (string)null);
			val2.Label(TranslatorFormattedStringExtensions.Translate("AtomicReconstructionTab_Speed", NamedArgument.op_Implicit(SelBuilding.speedFactor), NamedArgument.op_Implicit(SelBuilding.PaperclipConsumptionFactor)), -1f, (string)null);
			SelBuilding.speedFactor = (int)val2.Slider((float)SelBuilding.speedFactor, 1f, 20f);
			searchQuery = val2.TextEntry(searchQuery, 1);
			Rect val3 = default(Rect);
			((Rect)(ref val3))..ctor(0f, ((Listing)val2).CurHeight, ((Rect)(ref val)).width, ((Rect)(ref val)).height - ((Listing)val2).CurHeight);
			Rect val4 = default(Rect);
			((Rect)(ref val4))..ctor(0f, 0f, ((Rect)(ref val3)).width - 16f, scrollViewHeight);
			Widgets.BeginScrollView(val3, ref scrollPos, val4, true);
			float num = 0f;
			Rect val5 = default(Rect);
			((Rect)(ref val5))..ctor(0f, num, ((Rect)(ref val4)).width, 28f);
			Widgets.Label(new Rect(36f, ((Rect)(ref val5)).y, ((Rect)(ref val5)).width - 36f, ((Rect)(ref val5)).height), noneBrackets);
			Widgets.DrawHighlightIfMouseover(val5);
			if (GUI.Button(val5, string.Empty, Widgets.EmptyStyle))
			{
				if (SelBuilding.ThingToGenerate != null)
				{
					SelBuilding.ThingToGenerate = null;
				}
				SoundStarter.PlayOneShot(SoundDefOf.Click, SoundInfo.OnCamera((MaintenanceType)0));
			}
			num += 28f;
			foreach (ThingDef item in AllAllowedThingDefsColonyCanProduce().OrderBy((ThingDef d) => d, thingDefComparer))
			{
				if (searchQuery == null || ((Def)item).label.ToLower().Contains(searchQuery))
				{
					try
					{
						DrawThingDefRow(ref num, ((Rect)(ref val4)).width, item);
					}
					catch (Exception arg)
					{
						Log.Error($"Project RimFactory :: Exception displaying row for {item}:{arg}");
					}
				}
			}
			if ((int)Event.current.type == 8)
			{
				scrollViewHeight = num + 30f;
			}
			Widgets.EndScrollView();
			((Listing)val2).End();
		}

		private void DrawThingDefRow(ref float y, float width, ThingDef thingDef)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0125: Unknown result type (might be due to invalid IL or missing references)
			//IL_0127: Unknown result type (might be due to invalid IL or missing references)
			//IL_0134: Unknown result type (might be due to invalid IL or missing references)
			//IL_0188: Unknown result type (might be due to invalid IL or missing references)
			//IL_015d: Unknown result type (might be due to invalid IL or missing references)
			//IL_016e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0173: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_017c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(0f, y, width, 28f);
			if (Mouse.IsOver(val))
			{
				GUI.color = new Color(0.5f, 0.5f, 0.5f, 1f);
				GUI.DrawTexture(val, (Texture)(object)TexUI.HighlightTex);
			}
			if ((Object)(object)((BuildableDef)thingDef).DrawMatSingle != (Object)null && (Object)(object)((BuildableDef)thingDef).DrawMatSingle.mainTexture != (Object)null && thingDef.graphicData != null && GenUI.IconDrawScale(thingDef) <= 1.5f)
			{
				Widgets.ThingIcon(new Rect(4f, y, 28f, 28f), thingDef, (ThingDef)null, (ThingStyleDef)null, 1f, (Color?)null, (int?)null, 1f);
			}
			Text.Anchor = (TextAnchor)3;
			GUI.color = new Color(0.9f, 0.9f, 0.9f, 1f);
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(36f, y, ((Rect)(ref val)).width - 36f, ((Rect)(ref val)).height);
			Text.WordWrap = false;
			Widgets.Label(val2, GenText.Truncate(((Def)thingDef).LabelCap, ((Rect)(ref val2)).width, (Dictionary<string, TaggedString>)null));
			Text.WordWrap = true;
			string description = ((Def)thingDef).description;
			if (y > -28f)
			{
				Rect val3 = val;
				string text;
				if (!string.IsNullOrEmpty(description))
				{
					text = description;
				}
				else
				{
					TaggedString val4 = Translator.Translate("PRFNoDesc");
					text = ((TaggedString)(ref val4)).RawText;
				}
				TooltipHandler.TipRegion(val3, TipSignal.op_Implicit(text));
			}
			if (GUI.Button(val, "", Widgets.EmptyStyle))
			{
				if (SelBuilding.ThingToGenerate != thingDef)
				{
					SelBuilding.ThingToGenerate = thingDef;
				}
				SoundStarter.PlayOneShot(SoundDefOf.Click, SoundInfo.OnCamera((MaintenanceType)0));
			}
			Text.Anchor = (TextAnchor)0;
			y += 28f;
		}

		private static IEnumerable<ThingDef> AllAllowedThingDefsColonyCanProduce()
		{
			foreach (ThingDef tDef in DefDatabase<ThingDef>.AllDefs)
			{
				if (((BuildableDef)tDef).MadeFromStuff || ((Def)tDef).GetModExtension<DefModExtension_AtomicReconstructorDisallow>() != null)
				{
					continue;
				}
				DefModExtension_AtomicReconstructorResearchPrerequisite ex = ((Def)tDef).GetModExtension<DefModExtension_AtomicReconstructorResearchPrerequisite>();
				if (ex != null)
				{
					List<ResearchProjectDef> prerequisites = ex.prerequisites;
					if (prerequisites != null && !prerequisites.All((ResearchProjectDef r) => r.IsFinished))
					{
						continue;
					}
					if (ex.ignoreMainPrerequisites)
					{
						yield return tDef;
						continue;
					}
				}
				if (tDef.thingCategories == null)
				{
					continue;
				}
				foreach (ThingCategoryDef cat in from tcd in tDef.thingCategories
					from child in tcd.ThisAndParents()
					select child)
				{
					switch (((Def)cat).defName)
					{
					case "ResourcesRaw":
						if (!PRFDefOf.PRFAtomicReconstruction.IsFinished)
						{
							continue;
						}
						break;
					case "Foods":
						if (!PRFDefOf.PRFEdiblesSynthesis.IsFinished)
						{
							continue;
						}
						break;
					case "Manufactured":
						if (!PRFDefOf.PRFManufacturablesProduction.IsFinished)
						{
							continue;
						}
						break;
					default:
						continue;
					}
					yield return tDef;
					break;
				}
			}
		}
	}
	public class ITab_PaperclipDuplicator : ITab
	{
		private bool deposit;

		private string amountTextArea;

		private Building_PaperclipDuplicator SelBuilding => (Building_PaperclipDuplicator)(object)((ITab)this).SelThing;

		public ITab_PaperclipDuplicator()
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).size = new Vector2(400f, 250f);
			((InspectTabBase)this).labelKey = "PRFPaperclipDuplicatorTab";
		}

		protected override void FillTab()
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Expected O, but got Unknown
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			//IL_013e: Unknown result type (might be due to invalid IL or missing references)
			//IL_014a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0115: Unknown result type (might be due to invalid IL or missing references)
			//IL_011f: Expected O, but got Unknown
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_010c: Expected O, but got Unknown
			//IL_0168: Unknown result type (might be due to invalid IL or missing references)
			//IL_0205: Unknown result type (might be due to invalid IL or missing references)
			//IL_0226: Unknown result type (might be due to invalid IL or missing references)
			//IL_0192: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01be: Expected O, but got Unknown
			//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Expected O, but got Unknown
			//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fc: Expected O, but got Unknown
			Rect val = GenUI.ContractedBy(new Rect(0f, 0f, ((InspectTabBase)this).size.x, ((InspectTabBase)this).size.y), 10f);
			Listing_Standard val2 = new Listing_Standard((GameFont)1);
			((Listing)val2).Begin(val);
			val2.Label(((Entity)SelBuilding).LabelCap, -1f, (TipSignal?)null);
			string text = TaggedString.op_Implicit(Translator.Translate("PRFBoundStorageBuilding"));
			Building_MassStorageUnit boundStorageUnit = SelBuilding.BoundStorageUnit;
			if (val2.ButtonTextLabeled(text, ((boundStorageUnit != null) ? ((Entity)boundStorageUnit).LabelCap : null) ?? TaggedString.op_Implicit(Translator.Translate("NoneBrackets")), (TextAnchor)0, (string)null, (string)null))
			{
				List<FloatMenuOption> list = Find.CurrentMap.listerBuildings.AllBuildingsColonistOfClass<Building_MassStorageUnit>().Cast<Building_MassStorageUnit>().Select((Func<Building_MassStorageUnit, FloatMenuOption>)((Building_MassStorageUnit b) => new FloatMenuOption(((Entity)b).LabelCap, (Action)delegate
				{
					SelBuilding.BoundStorageUnit = b;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)))
					.ToList();
				if (list.Count == 0)
				{
					list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("NoneBrackets")), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
				}
				Find.WindowStack.Add((Window)new FloatMenu(list));
			}
			((Listing)val2).GapLine(12f);
			val2.Label(Translator.Translate("PRFDepositWithdraw"), -1f, (string)null);
			if (val2.ButtonTextLabeled(TaggedString.op_Implicit(Translator.Translate("PRFDepositWithdrawMode")), TaggedString.op_Implicit(Translator.Translate(deposit ? "PRFDeposit" : "PRFWithdraw")), (TextAnchor)0, (string)null, (string)null))
			{
				Find.WindowStack.Add((Window)new FloatMenu(new List<FloatMenuOption>(2)
				{
					new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("PRFDeposit")), (Action)delegate
					{
						deposit = true;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
					new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("PRFWithdraw")), (Action)delegate
					{
						deposit = false;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
				}));
			}
			amountTextArea = val2.TextEntryLabeled(TaggedString.op_Implicit(Translator.Translate("PRFAmount")), amountTextArea, 1);
			if (val2.ButtonText(TaggedString.op_Implicit(Translator.Translate("PRFDepositWithdraw")), (string)null, 1f))
			{
				HandleDepositWithdrawRequest();
			}
			((Listing)val2).End();
		}

		private void HandleDepositWithdrawRequest()
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_0215: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Unknown result type (might be due to invalid IL or missing references)
			//IL_029a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0303: Unknown result type (might be due to invalid IL or missing references)
			//IL_0308: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0171: Unknown result type (might be due to invalid IL or missing references)
			//IL_0176: Unknown result type (might be due to invalid IL or missing references)
			//IL_0183: Unknown result type (might be due to invalid IL or missing references)
			//IL_0188: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
			int result;
			if (SelBuilding.BoundStorageUnit == null)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("PRFNoBoundStorageUnit")), MessageTypeDefOf.RejectInput, true);
			}
			else if (!SelBuilding.BoundStorageUnit.CanReceiveIO)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("PRFBoundStorageUnitNotAvailableForIO")), MessageTypeDefOf.RejectInput, true);
			}
			else if (!int.TryParse(amountTextArea, out result))
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("PRFInputInvalid")), MessageTypeDefOf.RejectInput, true);
			}
			else if (deposit)
			{
				List<ThingCount> list = new List<ThingCount>();
				int num = 0;
				foreach (Thing item in SelBuilding.BoundStorageUnit.StoredItems.ToList())
				{
					if (item.def == PRFDefOf.Paperclip)
					{
						int num2 = Math.Min(result - num, item.stackCount);
						list.Add(new ThingCount(item, num2, false));
						num += num2;
					}
					if (num == result)
					{
						break;
					}
				}
				if (num == result)
				{
					SelBuilding.DepositPaperclips(result);
					for (int i = 0; i < list.Count; i++)
					{
						ThingCount val = list[i];
						Thing thing = ((ThingCount)(ref val)).Thing;
						val = list[i];
						thing.SplitOff(((ThingCount)(ref val)).Count);
					}
					Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SuccessfullyDepositedPaperclips", NamedArgument.op_Implicit(result))), MessageTypeDefOf.PositiveEvent, true);
				}
				else
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("PRFNotEnoughPaperclips")), MessageTypeDefOf.RejectInput, true);
				}
			}
			else if (result >= SelBuilding.PaperclipsActual)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("PRFNotEnoughPaperclips")), MessageTypeDefOf.RejectInput, true);
			}
			else
			{
				List<Thing> list2 = new List<Thing>();
				int num3;
				for (int j = 0; j < result; j += num3)
				{
					num3 = Math.Min(result - j, PRFDefOf.Paperclip.stackLimit);
					Thing val2 = ThingMaker.MakeThing(PRFDefOf.Paperclip, (ThingDef)null);
					val2.stackCount = num3;
					list2.Add(val2);
				}
				for (int k = 0; k < list2.Count; k++)
				{
					GenPlace.TryPlaceThing(list2[k], ((Thing)SelBuilding).Position, ((Thing)SelBuilding).Map, (ThingPlaceMode)0, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					((Building_Storage)SelBuilding.BoundStorageUnit).Notify_ReceivedThing(list2[k]);
				}
				SelBuilding.WithdrawPaperclips(result);
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SuccessfullyWithdrawnPaperclips", NamedArgument.op_Implicit(result))), MessageTypeDefOf.PositiveEvent, true);
			}
		}
	}
}
namespace ProjectRimFactory.Industry.HarmonyPatches
{
	[HarmonyPatch(typeof(CompCreatesInfestations), "get_CanCreateInfestationNow")]
	internal static class Patch_CanCreateInfestationNow
	{
		private static bool Prefix(CompCreatesInfestations __instance, ref bool __result)
		{
			if (!(((ThingComp)__instance).parent is Building_DeepQuarry))
			{
				return true;
			}
			CompPowerTrader comp = ((ThingComp)__instance).parent.GetComp<CompPowerTrader>();
			if (comp != null && comp.PowerOn && !__instance.CantFireBecauseCreatedInfestationRecently && !__instance.CantFireBecauseSomethingElseCreatedInfestationRecently)
			{
				__result = true;
			}
			else
			{
				__result = false;
			}
			return false;
		}
	}
}
namespace ProjectRimFactory.Drones
{
	public class Building_DroneCultivator : Building_WorkGiverDroneStation
	{
		private int dronesLeft;

		private int TotalDroneCount => SpawnedDrones.Count + dronesLeft;

		protected override int DronesLeft => dronesLeft - SpawnedDrones.Count;

		protected override void Notify_DroneLost()
		{
			dronesLeft--;
		}

		public override void Notify_DroneGained()
		{
			dronesLeft++;
		}

		public override void PostMake()
		{
			base.PostMake();
			dronesLeft = DefModExtensionDroneStation.GetDronesOnSpawn();
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if (TotalDroneCount < DefModExtensionDroneStation.GetDronesOnSpawn())
			{
				dronesLeft = DefModExtensionDroneStation.GetDronesOnSpawn();
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			yield return (Gizmo)new Command_Action
			{
				action = MakeMatchingGrowZone,
				hotKey = KeyBindingDefOf.Misc2,
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("CommandSunLampMakeGrowingZoneDesc")),
				icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Growing", true),
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("CommandSunLampMakeGrowingZoneLabel"))
			};
		}

		private void MakeMatchingGrowZone()
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Expected O, but got Unknown
			Designator_ZoneAdd_Growing designator = new Designator_ZoneAdd_Growing();
			((Designator)designator).DesignateMultiCell(CashedGetCoverageCells.Where(delegate(IntVec3 tempCell)
			{
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				AcceptanceReport val = ((Designator)designator).CanDesignateCell(tempCell);
				return ((AcceptanceReport)(ref val)).Accepted;
			}));
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look<int>(ref dronesLeft, "dronesLeft", 0, false);
		}
	}
	[StaticConstructorOnStartup]
	public abstract class Building_DroneStation : Building, IPowerSupplyMachineHolder, IDroneSeetingsITab, IPRF_SettingsContentLink
	{
		protected int[] CachedSleepTimeList;

		protected CompRefuelable RefuelableComp;

		private CompPowerWorkSetting compPowerWorkSetting;

		internal ModExtension_Skills ModExtensionDroneSkills;

		private IEnumerable<IntVec3> stationRangeCellsOld;

		protected List<IntVec3> CashedGetCoverageCells;

		public Area DroneAllowedArea;

		private static readonly Texture2D Cancel = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true);

		private bool lockdown;

		private string droneAreaSelectorLabel = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFDroneStationSelectArea", NamedArgument.op_Implicit(Translator.Translate("PRFDroneStationUnrestricted"))));

		protected DefModExtension_DroneStation DefModExtensionDroneStation;

		protected List<Pawn_Drone> SpawnedDrones = new List<Pawn_Drone>();

		protected Dictionary<WorkTypeDef, bool> WorkSettings = new Dictionary<WorkTypeDef, bool>();

		private CompPowerTrader compPowerTrader;

		private int additionJobSearchTickDelay;

		private static List<Hediff> droneDiffs;

		public List<SkillRecord> GetDroneSkillsRecord { get; set; } = new List<SkillRecord>();

		private int DroneRange
		{
			get
			{
				if (compPowerWorkSetting != null)
				{
					return (int)Math.Ceiling(compPowerWorkSetting.GetRange());
				}
				if (DefModExtensionDroneStation == null)
				{
					DefModExtensionDroneStation = ((Def)((Thing)this).def).GetModExtension<DefModExtension_DroneStation>();
				}
				return DefModExtensionDroneStation.SquareJobRadius;
			}
		}

		private IEnumerable<IntVec3> StationRangeCells
		{
			get
			{
				//IL_0028: Unknown result type (might be due to invalid IL or missing references)
				//IL_002d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0036: Unknown result type (might be due to invalid IL or missing references)
				//IL_003b: Unknown result type (might be due to invalid IL or missing references)
				CompPowerWorkSetting compPowerWorkSetting = this.compPowerWorkSetting;
				if (compPowerWorkSetting != null && compPowerWorkSetting.RangeSetting)
				{
					return this.compPowerWorkSetting.GetRangeCells();
				}
				CellRect val = GenAdj.OccupiedRect((Thing)(object)this);
				val = ((CellRect)(ref val)).ExpandedBy(DroneRange);
				return ((CellRect)(ref val)).Cells;
			}
		}

		private DroneArea GetDroneAllowedArea
		{
			get
			{
				//IL_003a: Unknown result type (might be due to invalid IL or missing references)
				//IL_003f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0042: Unknown result type (might be due to invalid IL or missing references)
				//IL_0056: Unknown result type (might be due to invalid IL or missing references)
				//IL_0077: Unknown result type (might be due to invalid IL or missing references)
				if (DroneRange <= 0)
				{
					return null;
				}
				DroneArea droneArea = new DroneArea(((Thing)this).Map.areaManager);
				foreach (IntVec3 stationRangeCell in StationRangeCells)
				{
					if (GenGrid.InBounds(stationRangeCell, ((Thing)this).Map))
					{
						((Area)droneArea)[stationRangeCell] = true;
					}
				}
				((Area)droneArea)[((Thing)this).Position] = true;
				((Thing)this).Map.areaManager.AllAreas.Add((Area)(object)droneArea);
				return droneArea;
			}
		}

		protected abstract int DronesLeft { get; }

		public IPowerSupplyMachine RangePowerSupplyMachine => compPowerWorkSetting;

		public Dictionary<WorkTypeDef, bool> GetWorkSettings
		{
			get
			{
				return WorkSettings;
			}
			set
			{
				WorkSettings = value;
			}
		}

		public List<SkillRecord> DroneSettingsSkillDefs => GetDroneSkillsRecord;

		public string[] GetSleepTimeList { get; private set; }

		public CompRefuelable CompRefuelable => RefuelableComp;

		private MapTickManager MapManager { get; set; }

		IPRF_SettingsContent IPRF_SettingsContentLink.PRF_SettingsContentOb => new ITab_DroneStation_Def(this);

		private void Update_droneAllowedArea_forDrones(Area dr)
		{
			DroneAllowedArea = (Area)(((object)dr) ?? ((object)GetDroneAllowedArea));
			if (!stationRangeCellsOld.SequenceEqual(StationRangeCells))
			{
				((DroneArea)(object)DroneAllowedArea).SetMutable(val: true);
				DroneAllowedArea.Delete();
				DroneAllowedArea = (Area)(object)GetDroneAllowedArea;
				stationRangeCellsOld = StationRangeCells;
			}
			for (int i = 0; i < SpawnedDrones.Count; i++)
			{
				((Pawn)SpawnedDrones[i]).playerSettings.AreaRestrictionInPawnCurrentMap = DroneAllowedArea;
			}
		}

		protected abstract void Notify_DroneLost();

		public abstract void Notify_DroneGained();

		public override void PostMake()
		{
			((ThingWithComps)this).PostMake();
			DefModExtensionDroneStation = ((Def)((Thing)this).def).GetModExtension<DefModExtension_DroneStation>();
			RefuelableComp = ((ThingWithComps)this).GetComp<CompRefuelable>();
			compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			compPowerWorkSetting = ((ThingWithComps)this).GetComp<CompPowerWorkSetting>();
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_0191: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			MapManager = map.GetComponent<MapTickManager>();
			RefuelableComp = ((ThingWithComps)this).GetComp<CompRefuelable>();
			compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			DefModExtensionDroneStation = ((Def)((Thing)this).def).GetModExtension<DefModExtension_DroneStation>();
			ModExtensionDroneSkills = ((Def)((Thing)this).def).GetModExtension<ModExtension_Skills>();
			compPowerWorkSetting = ((ThingWithComps)this).GetComp<CompPowerWorkSetting>();
			stationRangeCellsOld = StationRangeCells;
			if (DroneAllowedArea == null)
			{
				Update_droneAllowedArea_forDrones(DroneAllowedArea);
			}
			CachedSleepTimeList = DefModExtensionDroneStation.GetSleepTimesInt();
			GetSleepTimeList = DefModExtensionDroneStation.GetSleepTimesString();
			CashedGetCoverageCells = StationRangeCells.ToList();
			foreach (WorkTypeDef item in DefModExtensionDroneStation.workTypes.Except(WorkSettings.Keys).ToList())
			{
				WorkSettings.Add(item, value: true);
			}
			foreach (WorkTypeDef item2 in WorkSettings.Keys.Except(DefModExtensionDroneStation.workTypes).ToList())
			{
				WorkSettings.Remove(item2);
			}
			if (GetDroneSkillsRecord.Count == 0)
			{
				Pawn_Drone pawn_Drone = MakeDrone();
				GenSpawn.Spawn((Thing)(object)pawn_Drone, ((Thing)this).Position, ((Thing)this).Map, (WipeMode)0);
				((Thing)pawn_Drone).Destroy((DestroyMode)0);
				CompRefuelable refuelableComp = RefuelableComp;
				if (refuelableComp != null)
				{
					refuelableComp.Refuel(1f);
				}
			}
			update_droneAreaSelectorLabel(DroneAllowedArea);
			IPowerSupplyMachine rangePowerSupplyMachine = RangePowerSupplyMachine;
			if (rangePowerSupplyMachine != null)
			{
				MapManager.NextAction(rangePowerSupplyMachine.RefreshPowerStatus);
				MapManager.AfterAction(5, rangePowerSupplyMachine.RefreshPowerStatus);
			}
		}

		private void update_droneAreaSelectorLabel(Area area)
		{
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			if (area == null)
			{
				droneAreaSelectorLabel = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFDroneStationSelectArea", NamedArgument.op_Implicit(Translator.Translate("PRFDroneStationUnrestricted"))));
			}
			else
			{
				droneAreaSelectorLabel = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFDroneStationSelectArea", NamedArgument.op_Implicit(area.Label)));
			}
		}

		public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Invalid comparison between Unknown and I4
			((Thing)this).DynamicDrawPhaseAt(phase, drawLoc, flip);
			if ((int)phase == 2 && DefModExtensionDroneStation.displayDormantDrones)
			{
				DrawDormantDrones();
			}
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).DeSpawn(mode);
			List<Pawn_Drone> list = SpawnedDrones.ToList();
			for (int i = 0; i < list.Count; i++)
			{
				((Thing)list[i]).Destroy((DestroyMode)0);
			}
			if (DroneAllowedArea is DroneArea droneArea)
			{
				droneArea.SetMutable(val: true);
				((Area)droneArea).Delete();
			}
			DroneAllowedArea = null;
		}

		protected virtual void DrawDormantDrones()
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			foreach (IntVec3 item in GenAdj.CellsOccupiedBy((Thing)(object)this).Take(DronesLeft))
			{
				IntVec3 current = item;
				((BuildableDef)PRFDefOf.PRFDrone).graphic.DrawFromDef(((IntVec3)(ref current)).ToVector3ShiftedWithAltitude((AltitudeLayer)20), default(Rot4), PRFDefOf.PRFDrone, 0f);
			}
		}

		public override void DrawGUIOverlay()
		{
			((ThingWithComps)this).DrawGUIOverlay();
			if (lockdown)
			{
				((Thing)this).Map.overlayDrawer.DrawOverlay((Thing)(object)this, (OverlayTypes)16);
			}
		}

		public abstract Job TryGiveJob(Pawn pawn);

		protected override void Tick()
		{
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Expected O, but got Unknown
			//IL_0134: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).Tick();
			if (!((Thing)this).Spawned)
			{
				return;
			}
			if (SpawnedDrones.Count > 0)
			{
				CompPowerTrader obj = compPowerTrader;
				if (obj != null && !obj.PowerOn)
				{
					for (int num = SpawnedDrones.Count - 1; num >= 0; num--)
					{
						((Pawn)SpawnedDrones[num]).jobs.StartJob(new Job(PRFDefOf.PRFDrone_ReturnToStation, LocalTargetInfo.op_Implicit((Thing)(object)this)), (JobCondition)16, (ThinkNode)null, false, true, (ThinkTreeDef)null, (JobTag?)null, false, false, (bool?)null, false, true, false);
					}
					return;
				}
			}
			if (Gen.IsHashIntervalTick((Thing)(object)this, 60))
			{
				Update_droneAllowedArea_forDrones(DroneAllowedArea);
				CashedGetCoverageCells = StationRangeCells.ToList();
			}
			if (!Gen.IsHashIntervalTick((Thing)(object)this, 60 + additionJobSearchTickDelay) || DronesLeft <= 0 || lockdown)
			{
				return;
			}
			CompPowerTrader obj2 = compPowerTrader;
			if (obj2 != null && !obj2.PowerOn)
			{
				return;
			}
			Pawn_Drone pawn_Drone = MakeDrone();
			GenSpawn.Spawn((Thing)(object)pawn_Drone, ((Thing)this).Position, ((Thing)this).Map, (WipeMode)0);
			Job val = TryGiveJob((Pawn)(object)pawn_Drone);
			if (val != null)
			{
				additionJobSearchTickDelay = 0;
				val.playerForced = true;
				((Pawn)pawn_Drone).jobs.StartJob(val, (JobCondition)0, (ThinkNode)null, false, true, (ThinkTreeDef)null, (JobTag?)null, false, false, (bool?)null, false, true, false);
				return;
			}
			((Thing)pawn_Drone).Destroy((DestroyMode)0);
			Notify_DroneGained();
			if (additionJobSearchTickDelay < 300)
			{
				additionJobSearchTickDelay = (additionJobSearchTickDelay + 1) * 2;
			}
		}

		public void Notify_DroneMayBeLost(Pawn_Drone drone)
		{
			if (SpawnedDrones.Contains(drone))
			{
				SpawnedDrones.Remove(drone);
				Notify_DroneLost();
			}
		}

		public override void DrawExtraSelectionOverlays()
		{
			((Building)this).DrawExtraSelectionOverlays();
			if (DefModExtensionDroneStation.SquareJobRadius > 0)
			{
				GenDraw.DrawFieldEdges(CashedGetCoverageCells, 2900);
			}
		}

		public override string GetInspectString()
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			string inspectString = ((ThingWithComps)this).GetInspectString();
			if (!string.IsNullOrEmpty(inspectString))
			{
				stringBuilder.AppendLine(inspectString);
			}
			stringBuilder.Append(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFDroneStation_NumberOfDrones", NamedArgument.op_Implicit(DronesLeft))));
			return stringBuilder.ToString();
		}

		private Pawn_Drone MakeDrone()
		{
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Expected O, but got Unknown
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cb: Expected O, but got Unknown
			Pawn_Drone pawn_Drone = (Pawn_Drone)(object)ThingMaker.MakeThing(PRFDefOf.PRFDroneKind.race, (ThingDef)null);
			((Pawn)pawn_Drone).kindDef = PRFDefOf.PRFDroneKind;
			((Thing)pawn_Drone).SetFactionDirect(((Thing)this).Faction);
			PawnComponentsUtility.CreateInitialComponents((Pawn)(object)pawn_Drone);
			((Pawn)pawn_Drone).gender = (Gender)0;
			((Pawn)pawn_Drone).ageTracker.AgeBiologicalTicks = 0L;
			((Pawn)pawn_Drone).ageTracker.AgeChronologicalTicks = 0L;
			if (droneDiffs == null)
			{
				PawnTechHediffsGenerator.GenerateTechHediffsFor((Pawn)(object)pawn_Drone);
				droneDiffs = ((Pawn)pawn_Drone).health.hediffSet.hediffs.ToList();
			}
			else
			{
				((Pawn)pawn_Drone).health.hediffSet.hediffs = droneDiffs.ToList();
			}
			((Thing)pawn_Drone).Faction.Notify_PawnJoined((Pawn)(object)pawn_Drone);
			((Pawn)pawn_Drone).relations = new Pawn_RelationsTracker((Pawn)(object)pawn_Drone);
			((Pawn)pawn_Drone).ideo = new Pawn_IdeoTracker((Pawn)(object)pawn_Drone);
			((Pawn)pawn_Drone).ideo.SetIdeo(((Thing)this).Faction.ideos.PrimaryIdeo);
			pawn_Drone.BaseStation = this;
			SpawnedDrones.Add(pawn_Drone);
			return pawn_Drone;
		}

		public override void ExposeData()
		{
			((Building)this).ExposeData();
			Scribe_Collections.Look<Pawn_Drone>(ref SpawnedDrones, "spawnedDrones", (LookMode)3, Array.Empty<object>());
			Scribe_Values.Look<bool>(ref lockdown, "lockdown", false, false);
			Scribe_References.Look<Area>(ref DroneAllowedArea, "droneAllowedArea", false);
			Scribe_Collections.Look<WorkTypeDef, bool>(ref WorkSettings, "WorkSettings", (LookMode)0, (LookMode)0);
			if (WorkSettings == null)
			{
				WorkSettings = new Dictionary<WorkTypeDef, bool>();
			}
			if (RefuelableComp == null)
			{
				RefuelableComp = ((ThingWithComps)this).GetComp<CompRefuelable>();
			}
			if (stationRangeCellsOld == null)
			{
				stationRangeCellsOld = StationRangeCells;
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			yield return (Gizmo)new Command_Toggle
			{
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRFDroneStationLockdown")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRFDroneStationLockdownDesc")),
				toggleAction = delegate
				{
					//IL_0048: Unknown result type (might be due to invalid IL or missing references)
					//IL_004d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0075: Expected O, but got Unknown
					lockdown = !lockdown;
					if (!lockdown)
					{
						return;
					}
					foreach (Pawn_Drone item2 in SpawnedDrones.ToList())
					{
						((Pawn)item2).jobs.StartJob(new Job(PRFDefOf.PRFDrone_ReturnToStation, LocalTargetInfo.op_Implicit((Thing)(object)this)), (JobCondition)16, (ThinkNode)null, false, true, (ThinkTreeDef)null, (JobTag?)null, false, false, (bool?)null, false, true, false);
					}
				},
				isActive = () => lockdown,
				icon = (Texture)(object)Cancel
			};
			yield return (Gizmo)new Command_Action
			{
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRFDroneStationLockdownAll")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRFDroneStationLockdownAllDesc")),
				action = delegate
				{
					//IL_0066: Unknown result type (might be due to invalid IL or missing references)
					//IL_006b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0094: Expected O, but got Unknown
					List<Building_DroneStation> list = ((Thing)this).Map.listerThings.AllThings.OfType<Building_DroneStation>().ToList();
					for (int i = 0; i < list.Count; i++)
					{
						list[i].lockdown = true;
						foreach (Pawn_Drone item3 in list[i].SpawnedDrones.ToList())
						{
							((Pawn)item3).jobs.StartJob(new Job(PRFDefOf.PRFDrone_ReturnToStation, LocalTargetInfo.op_Implicit((Thing)(object)list[i])), (JobCondition)16, (ThinkNode)null, false, true, (ThinkTreeDef)null, (JobTag?)null, false, false, (bool?)null, false, true, false);
						}
					}
				},
				icon = (Texture)(object)ContentFinder<Texture2D>.Get("PRFUi/deactivate", true)
			};
			yield return (Gizmo)new Command_Action
			{
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRFDroneStationLiftLockdownAll")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRFDroneStationLiftLockdownAllDesc")),
				action = delegate
				{
					List<Building_DroneStation> list = ((Thing)this).Map.listerThings.AllThings.OfType<Building_DroneStation>().ToList();
					for (int i = 0; i < list.Count; i++)
					{
						list[i].lockdown = false;
					}
				},
				icon = (Texture)(object)ContentFinder<Texture2D>.Get("PRFUi/activate", true)
			};
			if (DroneRange == 0)
			{
				yield return (Gizmo)(object)new DroneAreaSelector
				{
					icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Designators/AreaAllowedExpand", true),
					defaultLabel = droneAreaSelectorLabel,
					SelectAction = delegate(Area a)
					{
						Update_droneAllowedArea_forDrones(a);
						update_droneAreaSelectorLabel(a);
					}
				};
			}
			if (!Prefs.DevMode)
			{
				yield break;
			}
			yield return (Gizmo)new Command_Action
			{
				defaultLabel = "DEV: Respawn drones",
				defaultDesc = "Respawns all Drones",
				action = delegate
				{
					for (int num = SpawnedDrones.Count - 1; num >= 0; num--)
					{
						((Thing)SpawnedDrones[num]).Destroy((DestroyMode)0);
						Notify_DroneGained();
					}
				}
			};
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
	public class Building_DroneStationRefuelable : Building_WorkGiverDroneStation, IAdditionalPowerConsumption
	{
		private int lastFuelCnt = 0;

		protected override int DronesLeft => Mathf.RoundToInt(RefuelableComp.Fuel) - SpawnedDrones.Count;

		Dictionary<string, int> IAdditionalPowerConsumption.AdditionalPowerConsumption
		{
			get
			{
				Dictionary<string, int> dictionary = new Dictionary<string, int>();
				CompRefuelable refuelableComp = RefuelableComp;
				dictionary.Add("Drone Count", (int)((refuelableComp != null) ? refuelableComp.Fuel : 0f) * 10);
				return dictionary;
			}
		}

		public override void PostMake()
		{
			base.PostMake();
			RefuelableComp.Refuel((float)DefModExtensionDroneStation.GetDronesOnSpawn(RefuelableComp));
		}

		protected override void Notify_DroneLost()
		{
			RefuelableComp.ConsumeFuel(1f);
			base.RangePowerSupplyMachine?.RefreshPowerStatus();
		}

		public override void Notify_DroneGained()
		{
			RefuelableComp.Refuel(1f);
			base.RangePowerSupplyMachine?.RefreshPowerStatus();
		}

		protected override void Tick()
		{
			base.Tick();
			if (((Thing)this).Spawned && Gen.IsHashIntervalTick((Thing)(object)this, 60) && (float)lastFuelCnt != RefuelableComp.Fuel)
			{
				lastFuelCnt = (int)RefuelableComp.Fuel;
				base.RangePowerSupplyMachine?.RefreshPowerStatus();
			}
		}
	}
	public class Building_DroneStationSpecialGraphics : Building_DroneStationRefuelable
	{
		protected override void DrawDormantDrones()
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			if (DronesLeft > 0)
			{
				Graphic graphic = ((BuildableDef)PRFDefOf.PRFDrone).graphic;
				IntVec3 position = ((Thing)this).Position;
				graphic.DrawFromDef(((IntVec3)(ref position)).ToVector3ShiftedWithAltitude((AltitudeLayer)20), default(Rot4), PRFDefOf.PRFDrone, 0f);
			}
		}
	}
	public abstract class Building_WorkGiverDroneStation : Building_DroneStation
	{
		private Pawn_WorkSettings workSettings;

		private bool allowsEmergency;

		private bool configuredAllowsEmergency;

		public Dictionary<WorkTypeDef, bool> WorkSettingsDict => WorkSettings;

		public override Job TryGiveJob(Pawn pawn)
		{
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Expected O, but got Unknown
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0104: Expected O, but got Unknown
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0120: Unknown result type (might be due to invalid IL or missing references)
			//IL_0143: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_014b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0150: Unknown result type (might be due to invalid IL or missing references)
			if (CachedSleepTimeList.Contains(GenLocalDate.HourOfDay((Thing)(object)this)))
			{
				return null;
			}
			if (workSettings == null)
			{
				pawn.workSettings = new Pawn_WorkSettings(pawn);
				pawn.workSettings.EnableAndInitialize();
				pawn.workSettings.DisableAll();
				workSettings = pawn.workSettings;
			}
			else
			{
				pawn.workSettings = workSettings;
			}
			for (int i = 0; i < WorkSettings.Keys.Count; i++)
			{
				WorkTypeDef val = WorkSettings.Keys.ElementAt(i);
				pawn.workSettings.SetPriority(val, WorkSettings[val] ? 3 : 0);
				if (!configuredAllowsEmergency && StaticEmergencyWorkTypes.EmergencyWorkTypes.Contains(val))
				{
					allowsEmergency = true;
				}
			}
			configuredAllowsEmergency = true;
			JobGiver_Work val2 = new JobGiver_Work
			{
				emergency = true
			};
			object obj;
			ThinkResult val3;
			if (!allowsEmergency)
			{
				obj = null;
			}
			else
			{
				val3 = ((ThinkNode)val2).TryIssueJobPackage(pawn, default(JobIssueParams));
				obj = ((ThinkResult)(ref val3)).Job;
			}
			Job val4 = (Job)obj;
			if (val4 != null)
			{
				return val4;
			}
			val2.emergency = false;
			val3 = ((ThinkNode)val2).TryIssueJobPackage(pawn, default(JobIssueParams));
			return ((ThinkResult)(ref val3)).Job;
		}
	}
	public class DefModExtension_DroneStation : DefModExtension, IXMLThingDescription
	{
		public int maxNumDrones;

		public bool displayDormantDrones;

		public List<WorkTypeDef> workTypes;

		public int SquareJobRadius = 0;

		public string Sleeptimes = "";

		private int spawnWithDrones = 0;

		private bool spawnWithFullDrones = false;

		public string[] GetSleepTimesString()
		{
			return Sleeptimes.Split(',', StringSplitOptions.None);
		}

		public int[] GetSleepTimesInt()
		{
			string[] sleepTimesString = GetSleepTimesString();
			List<int> list = new List<int>();
			for (int i = 0; i < sleepTimesString.Length; i++)
			{
				if (int.TryParse(sleepTimesString[i], out var result))
				{
					list.Add(result);
				}
			}
			return list.ToArray();
		}

		public string GetDescription(ThingDef def)
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_009b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0100: Unknown result type (might be due to invalid IL or missing references)
			//IL_0105: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00db: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			//IL_010d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_011c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			//IL_013c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0153: Unknown result type (might be due to invalid IL or missing references)
			//IL_0158: Unknown result type (might be due to invalid IL or missing references)
			//IL_0174: Unknown result type (might be due to invalid IL or missing references)
			CompProperties_Refuelable compProperties = def.GetCompProperties<CompProperties_Refuelable>();
			int num = maxNumDrones;
			if (compProperties != null)
			{
				num = (int)compProperties.fuelCapacity;
			}
			TaggedString val = TranslatorFormattedStringExtensions.Translate("PRF_UTD_DefModExtension_DroneStation_MaxDrones", NamedArgument.op_Implicit(num)) + "\r\n";
			val += TranslatorFormattedStringExtensions.Translate("PRF_UTD_DefModExtension_DroneStation_IncludedDrones", NamedArgument.op_Implicit(GetDronesOnSpawn(null, compProperties))) + "\r\n";
			if (Sleeptimes != "")
			{
				val += TranslatorFormattedStringExtensions.Translate("PRF_UTD_DefModExtension_DroneStation_SleepTimes", NamedArgument.op_Implicit(Sleeptimes)) + "\r\n";
			}
			val = ((SquareJobRadius != 0) ? (val + (TranslatorFormattedStringExtensions.Translate("PRF_UTD_DefModExtension_DroneStation_Range", NamedArgument.op_Implicit(SquareJobRadius)) + "\r\n")) : (val + (TranslatorFormattedStringExtensions.Translate("PRF_UTD_DefModExtension_DroneStation_Range", NamedArgument.op_Implicit(Translator.Translate("PRF_UTD_DefModExtension_DroneStation_UnlimitedRange"))) + "\r\n")));
			val += Translator.Translate("PRF_UTD_DefModExtension_DroneStation_WorkTypes") + "\r\n";
			foreach (WorkTypeDef workType in workTypes)
			{
				val += "  - " + ((Def)workType).defName + "\r\n";
			}
			return TaggedString.op_Implicit(val);
		}

		public int GetDronesOnSpawn(CompRefuelable fuelComp = null, CompProperties_Refuelable fuelCompProperties = null)
		{
			CompProperties_Refuelable val = ((fuelComp != null) ? fuelComp.Props : null) ?? fuelCompProperties;
			if (spawnWithFullDrones)
			{
				return (int)(val?.fuelCapacity ?? ((float)maxNumDrones));
			}
			return Mathf.Clamp(spawnWithDrones, 0, (int)(val?.fuelCapacity ?? ((float)maxNumDrones)));
		}
	}
	public class DroneArea : Area
	{
		private string labelInt;

		private Color colorInt;

		private bool mutable;

		public override string Label => "DroneZone";

		public override Color Color => colorInt;

		public override int ListPriority => 3000;

		public override bool Mutable => mutable;

		public DroneArea()
		{
		}

		public DroneArea(AreaManager areaManager, string label = null)
			: base(areaManager)
		{
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			base.areaManager = areaManager;
			if (!GenText.NullOrEmpty(label))
			{
				labelInt = label;
			}
			else
			{
				int num = 1;
				while (true)
				{
					labelInt = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("AreaDefaultLabel", NamedArgument.op_Implicit(num)));
					if (areaManager.GetLabeled(labelInt) == null)
					{
						break;
					}
					num++;
				}
			}
			colorInt = new Color(Rand.Value, Rand.Value, Rand.Value);
			colorInt = Color.Lerp(colorInt, Color.gray, 0.5f);
		}

		public void SetMutable(bool val)
		{
			mutable = val;
		}

		public override string GetUniqueLoadID()
		{
			return "Area_" + base.ID + "_DroneArea";
		}

		public override void ExposeData()
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			((Area)this).ExposeData();
			Scribe_Values.Look<string>(ref labelInt, "label", (string)null, false);
			Scribe_Values.Look<Color>(ref colorInt, "color", default(Color), false);
		}
	}
	public class DroneAreaSelector : Designator
	{
		private static Area selectedArea;

		public Action<Area> SelectAction;

		public override AcceptanceReport CanDesignateCell(IntVec3 loc)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			return AcceptanceReport.op_Implicit(GenGrid.InBounds(loc, ((Designator)this).Map) && Designator_AreaAllowed.SelectedArea != null && !Designator_AreaAllowed.SelectedArea[loc]);
		}

		public override void SelectedUpdate()
		{
		}

		public override void ProcessInput(Event ev)
		{
			if (((Designator)this).CheckCanInteract())
			{
				if (selectedArea != null)
				{
				}
				AreaUtility.MakeAllowedAreaListFloatMenu((Action<Area>)delegate(Area a)
				{
					selectedArea = a;
					SelectAction(selectedArea);
				}, true, false, ((Designator)this).Map);
			}
		}
	}
	[StaticConstructorOnStartup]
	public static class DroneBackstories
	{
		public static BackstoryDef childhood;

		public static BackstoryDef adulthood;

		static DroneBackstories()
		{
			LongEventHandler.ExecuteWhenFinished((Action)delegate
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_001b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0021: Unknown result type (might be due to invalid IL or missing references)
				//IL_0030: Unknown result type (might be due to invalid IL or missing references)
				//IL_003b: Unknown result type (might be due to invalid IL or missing references)
				//IL_003e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0043: Unknown result type (might be due to invalid IL or missing references)
				//IL_0045: Unknown result type (might be due to invalid IL or missing references)
				//IL_004a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0050: Unknown result type (might be due to invalid IL or missing references)
				//IL_005f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0065: Unknown result type (might be due to invalid IL or missing references)
				//IL_0074: Unknown result type (might be due to invalid IL or missing references)
				//IL_0089: Expected O, but got Unknown
				//IL_0089: Unknown result type (might be due to invalid IL or missing references)
				//IL_008e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0094: Unknown result type (might be due to invalid IL or missing references)
				//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
				//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
				//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
				//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
				//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
				//IL_0111: Expected O, but got Unknown
				childhood = new BackstoryDef
				{
					title = TaggedString.op_Implicit(Translator.Translate("PRFDroneName")),
					titleShort = TaggedString.op_Implicit(Translator.Translate("PRFDroneName")),
					identifier = "PRFNoneBracketsC",
					workDisables = (WorkTags)32,
					slot = (BackstorySlot)0,
					baseDesc = TaggedString.op_Implicit(Translator.Translate("NoneBrackets")),
					description = TaggedString.op_Implicit(Translator.Translate("NoneBrackets")),
					modContentPack = ((Mod)LoadedModManager.GetMod<ProjectRimFactory_ModComponent>()).Content
				};
				adulthood = new BackstoryDef
				{
					title = TaggedString.op_Implicit(Translator.Translate("PRFDroneName")),
					titleShort = TaggedString.op_Implicit(Translator.Translate("PRFDroneName")),
					identifier = "PRFNoneBracketsA",
					workDisables = (WorkTags)32,
					slot = (BackstorySlot)1,
					baseDesc = TaggedString.op_Implicit(Translator.Translate("NoneBrackets")),
					description = TaggedString.op_Implicit(Translator.Translate("NoneBrackets")),
					modContentPack = ((Mod)LoadedModManager.GetMod<ProjectRimFactory_ModComponent>()).Content
				};
				List<BackstoryDef> allDefsListForReading = DefDatabase<BackstoryDef>.AllDefsListForReading;
				TryAddBackstoryDef(allDefsListForReading, childhood);
				TryAddBackstoryDef(allDefsListForReading, adulthood);
			});
		}

		private static void TryAddBackstoryDef(List<BackstoryDef> backstoryDefs, BackstoryDef backstoryDef)
		{
			if (!backstoryDefs.Contains(backstoryDef))
			{
				backstoryDefs.Add(backstoryDef);
			}
		}
	}
	internal static class DroneSkills
	{
		public static List<SkillRecord> UpdateSkills(Pawn_SkillTracker skill, List<SkillRecord> skillRecords, ModExtension_Skills modExtensionSkills = null, bool forceUpdate = false)
		{
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			if (skillRecords.Count != 0 && !forceUpdate)
			{
				skill.skills = skillRecords;
				return skill.skills;
			}
			foreach (SkillRecord skill2 in skill.skills)
			{
				skill2.passion = (Passion)0;
				if (skill2.xpSinceLastLevel > 1f)
				{
					skill2.xpSinceMidnight = 100f;
					skill2.xpSinceLastLevel = 100f;
				}
				if (modExtensionSkills != null)
				{
					SkillRecord val = skill2;
					ModExtension_Skills.enum_ModExtension_SkillsskillUsage skillUsage = modExtensionSkills.SkillUsage;
					if (1 == 0)
					{
					}
					int levelInt = skillUsage switch
					{
						ModExtension_Skills.enum_ModExtension_SkillsskillUsage.ThisOverrides => modExtensionSkills.GetSkillLevel(skill2.def), 
						ModExtension_Skills.enum_ModExtension_SkillsskillUsage.ReserchIsCapping => Mathf.Clamp(modExtensionSkills.GetSkillLevel(skill2.def), 0, ReserchSkillModifier.GetResearchSkillLevel()), 
						ModExtension_Skills.enum_ModExtension_SkillsskillUsage.ThisIsCapping => Mathf.Clamp(ReserchSkillModifier.GetResearchSkillLevel(), 0, modExtensionSkills.GetSkillLevel(skill2.def)), 
						ModExtension_Skills.enum_ModExtension_SkillsskillUsage.ReserchOverrides => ReserchSkillModifier.GetResearchSkillLevel(), 
						_ => modExtensionSkills.GetSkillLevel(skill2.def), 
					};
					if (1 == 0)
					{
					}
					val.levelInt = levelInt;
				}
				else
				{
					skill2.levelInt = ReserchSkillModifier.GetResearchSkillLevel();
				}
			}
			return skill.skills;
		}
	}
	internal interface IDroneSeetingsITab
	{
		Dictionary<WorkTypeDef, bool> GetWorkSettings { get; set; }

		List<SkillRecord> DroneSettingsSkillDefs { get; }

		string[] GetSleepTimeList { get; }

		CompRefuelable CompRefuelable { get; }
	}
	public class ITab_DroneStation_Def : IPRF_SettingsContent
	{
		private readonly object caller;

		private static GUIStyle RichTextStyle
		{
			get
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_000e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				//IL_0020: Expected O, but got Unknown
				GUIStyle val = new GUIStyle
				{
					richText = true
				};
				val.normal.textColor = Color.white;
				return val;
			}
		}

		private IDroneSeetingsITab droneStation => caller as IDroneSeetingsITab;

		public float ITab_Settings_Minimum_x => 400f;

		public float ITab_Settings_Additional_y
		{
			get
			{
				float num = 30 * droneStation.GetWorkSettings.Count + 60 + 12;
				if (droneStation.GetSleepTimeList[0] != "")
				{
					num += 70f;
				}
				if (droneStation.CompRefuelable != null)
				{
					num += 70f;
				}
				return num;
			}
		}

		public ITab_DroneStation_Def(object callerParam)
		{
			caller = callerParam;
		}

		private bool CheckboxHelper(Rect rect, Listing_Standard list, bool variable, WorkTypeDef def)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_010c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
			rect = ((Listing)list).GetRect(30f, 1f);
			bool result = variable;
			Widgets.CheckboxLabeled(rect, def.labelShort, ref result, false, (Texture2D)null, (Texture2D)null, false, false);
			Rect val = rect;
			string text = TaggedString.op_Implicit(Translator.Translate("ITab_DroneStation_averageskill"));
			((Rect)(ref val)).x = 400 - 10 * text.Length;
			if (def.relevantSkills.Count > 0)
			{
				int num = 0;
				foreach (SkillRecord droneSettingsSkillDef in droneStation.DroneSettingsSkillDefs)
				{
					if (def.relevantSkills.Contains(droneSettingsSkillDef.def))
					{
						num += droneSettingsSkillDef.levelInt;
					}
				}
				((Rect)(ref val)).y = ((Rect)(ref val)).y + 5f;
				num /= def.relevantSkills.Count;
				Widgets.Label(val, text + num);
			}
			else
			{
				Widgets.Label(val, "-");
			}
			return result;
		}

		public Listing_Standard ITab_Settings_AppendContent(Listing_Standard list, Rect parrentRect)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0103: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0232: Unknown result type (might be due to invalid IL or missing references)
			//IL_0237: Unknown result type (might be due to invalid IL or missing references)
			//IL_017b: Unknown result type (might be due to invalid IL or missing references)
			Rect rect = default(Rect);
			list.Label(Translator.Translate("ITab_DroneStation_HeaderLabel"), -1f, (string)null);
			list.Label(Translator.Translate("ITab_DroneStation_InfoLabel"), -1f, (string)null);
			((Listing)list).GapLine(12f);
			foreach (WorkTypeDef item in droneStation.GetWorkSettings.Keys.ToList())
			{
				droneStation.GetWorkSettings[item] = CheckboxHelper(rect, list, droneStation.GetWorkSettings[item], item);
			}
			if (droneStation.GetSleepTimeList[0] != "")
			{
				((Listing)list).GapLine(12f);
				list.Label(Translator.Translate("ITab_DroneStation_Sleeptimes"), -1f, (string)null);
				rect = ((Listing)list).GetRect(30f, 1f);
				string text = string.Empty;
				for (int i = 0; i < 24; i++)
				{
					text = ((!droneStation.GetSleepTimeList.Contains(i.ToString())) ? (text + $"{i} ") : (text + $"<color=red><b>{i}</b></color> "));
				}
				CommonGUIFunctions.Label(rect, text, RichTextStyle);
			}
			if (droneStation.CompRefuelable == null)
			{
				return list;
			}
			((Listing)list).GapLine(12f);
			list.Label(Translator.Translate("ITab_DroneStation_SetTargetFuel"), -1f, (string)null);
			rect = ((Listing)list).GetRect(30f, 1f);
			((Listing)list).Gap(12f);
			droneStation.CompRefuelable.TargetFuelLevel = Widgets.HorizontalSlider(rect, droneStation.CompRefuelable.TargetFuelLevel, 0f, droneStation.CompRefuelable.Props.fuelCapacity, true, TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("SetTargetFuelLevel", NamedArgument.op_Implicit(droneStation.CompRefuelable.TargetFuelLevel))), "0", droneStation.CompRefuelable.Props.fuelCapacity.ToString(), 1f);
			return list;
		}
	}
	public class Pawn_Drone : Pawn
	{
		public Building_DroneStation BaseStation;

		private ModExtension_Skills skillSettings;

		public override void Kill(DamageInfo? dinfo, Hediff exactCulprit = null)
		{
			((Thing)this).Destroy((DestroyMode)0);
		}

		public override void Destroy(DestroyMode mode = (DestroyMode)0)
		{
			if (((Thing)this).Spawned)
			{
				((Entity)this).DeSpawn((DestroyMode)0);
			}
			ReflectionUtility.MapIndexOrState.SetValue(this, (sbyte)(-2));
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Expected O, but got Unknown
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Expected O, but got Unknown
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Expected O, but got Unknown
			//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Expected O, but got Unknown
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d6: Expected O, but got Unknown
			if (BaseStation == null)
			{
				((Thing)this).Kill((DamageInfo?)null, (Hediff)null);
				return;
			}
			((Pawn)this).SpawnSetup(map, respawningAfterLoad);
			base.skills = new Pawn_SkillTracker((Pawn)(object)this);
			skillSettings = BaseStation.ModExtensionDroneSkills;
			BaseStation.GetDroneSkillsRecord = DroneSkills.UpdateSkills(base.skills, BaseStation.GetDroneSkillsRecord, skillSettings, forceUpdate: true);
			base.story = new Pawn_StoryTracker((Pawn)(object)this)
			{
				bodyType = BodyTypeDefOf.Thin,
				Childhood = DroneBackstories.childhood,
				Adulthood = DroneBackstories.adulthood
			};
			base.drafter = new Pawn_DraftController((Pawn)(object)this);
			base.relations = new Pawn_RelationsTracker((Pawn)(object)this);
			((Pawn)this).Name = (Name)new NameSingle(TaggedString.op_Implicit(Translator.Translate("PRFDroneName")), false);
			base.playerSettings.AreaRestrictionInPawnCurrentMap = BaseStation.DroneAllowedArea;
		}

		public override void TickLong()
		{
			((ThingWithComps)this).TickLong();
			if (((Thing)this).Spawned)
			{
				BaseStation.GetDroneSkillsRecord = DroneSkills.UpdateSkills(base.skills, BaseStation.GetDroneSkillsRecord, skillSettings, forceUpdate: true);
			}
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			((Pawn)this).DeSpawn(mode);
			BaseStation?.Notify_DroneMayBeLost(this);
		}

		public override void Notify_Downed()
		{
			((Pawn)this).Notify_Downed();
			((Thing)this).Kill((DamageInfo?)null, (Hediff)null);
		}

		public override void ExposeData()
		{
			((Pawn)this).ExposeData();
			Scribe_References.Look<Building_DroneStation>(ref BaseStation, "station", false);
		}
	}
	public class PlaceWorker_DroneStation : PlaceWorker
	{
		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			((PlaceWorker)this).DrawGhost(def, center, rot, ghostCol, (Thing)null);
			int squareJobRadius = ((Def)def).GetModExtension<DefModExtension_DroneStation>().SquareJobRadius;
			if (squareJobRadius <= 0)
			{
				return;
			}
			List<IntVec3> list = new List<IntVec3>((squareJobRadius * 2 + 1) * (squareJobRadius * 2 + 1));
			for (int i = -squareJobRadius; i <= squareJobRadius; i++)
			{
				for (int j = -squareJobRadius; j <= squareJobRadius; j++)
				{
					list.Add(new IntVec3(i, 0, j) + center);
				}
			}
			GenDraw.DrawFieldEdges(list, 2900);
		}
	}
}
namespace ProjectRimFactory.Drones.AI
{
	public class JobDriver_ReturnToStation : JobDriver_Goto
	{
		protected override IEnumerable<Toil> MakeNewToils()
		{
			foreach (Toil item in <>n__0())
			{
				yield return item;
			}
			yield return new Toil
			{
				initAction = delegate
				{
					//IL_0012: Unknown result type (might be due to invalid IL or missing references)
					((JobDriver)this).pawn.inventory.DropAllNearPawn(((Thing)((JobDriver)this).pawn).Position, false, false);
					((Thing)((JobDriver)this).pawn).Destroy((DestroyMode)0);
					((Pawn_Drone)(object)((JobDriver)this).pawn).BaseStation.Notify_DroneGained();
				},
				defaultCompleteMode = (ToilCompleteMode)1
			};
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Toil> <>n__0()
		{
			return ((JobDriver_Goto)this).MakeNewToils();
		}
	}
	public class JobDriver_SelfTerminate : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			yield return new Toil
			{
				initAction = delegate
				{
					((Thing)base.pawn).Destroy((DestroyMode)0);
				},
				defaultCompleteMode = (ToilCompleteMode)1
			};
		}
	}
	public class JobGiver_DroneFlee : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_013e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0143: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			Pawn_PlayerSettings playerSettings = pawn.playerSettings;
			if (playerSettings != null && playerSettings.UsesConfigurableHostilityResponse)
			{
				return null;
			}
			if (ThinkNode_ConditionalShouldFollowMaster.ShouldFollowMaster(pawn))
			{
				return null;
			}
			if (((Thing)pawn).Map == null)
			{
				return null;
			}
			IntVec3 position;
			if (((Thing)pawn).Faction == null)
			{
				List<Thing> list = ((Thing)pawn).Map.listerThings.ThingsInGroup((ThingRequestGroup)48);
				for (int i = 0; i < list.Count; i++)
				{
					position = ((Thing)pawn).Position;
					if (((IntVec3)(ref position)).InHorDistOf(list[i].Position, 18f) && FleeUtility.ShouldFleeFrom(list[i], pawn, false, false))
					{
						return ReturnToStationJob((Pawn_Drone)(object)pawn);
					}
				}
				Job val = FleeLargeFireJob(pawn);
				if (val != null)
				{
					return val;
				}
			}
			else if (LordUtility.GetLord(pawn) == null)
			{
				List<IAttackTarget> potentialTargetsFor = ((Thing)pawn).Map.attackTargetsCache.GetPotentialTargetsFor((IAttackTargetSearcher)(object)pawn);
				for (int j = 0; j < potentialTargetsFor.Count; j++)
				{
					Thing thing = potentialTargetsFor[j].Thing;
					position = ((Thing)pawn).Position;
					if (((IntVec3)(ref position)).InHorDistOf(thing.Position, 18f) && FleeUtility.ShouldFleeFrom(thing, pawn, false, true))
					{
						return ReturnToStationJob((Pawn_Drone)(object)pawn);
					}
				}
			}
			return null;
		}

		private Job FleeLargeFireJob(Pawn pawn)
		{
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Expected O, but got Unknown
			//IL_00a3: Expected O, but got Unknown
			List<Thing> list = ((Thing)pawn).Map.listerThings.ThingsInGroup((ThingRequestGroup)25);
			if (list.Count < 60)
			{
				return null;
			}
			TraverseParms tp = TraverseParms.For(pawn, (Danger)3, (TraverseMode)0, false, false, false, true);
			Fire closestFire = null;
			float closestDistSq = -1f;
			int firesCount = 0;
			RegionTraverser.BreadthFirstTraverse(((Thing)pawn).Position, ((Thing)pawn).Map, (RegionEntryPredicate)((Region from, Region to) => to.Allows(tp, false)), (RegionProcessor)delegate(Region x)
			{
				//IL_001a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_006c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0076: Expected O, but got Unknown
				List<Thing> list2 = x.ListerThings.ThingsInGroup((ThingRequestGroup)25);
				for (int i = 0; i < list2.Count; i++)
				{
					float num = IntVec3Utility.DistanceToSquared(((Thing)pawn).Position, list2[i].Position);
					if (num <= 400f)
					{
						if (closestFire == null || num < closestDistSq)
						{
							closestDistSq = num;
							closestFire = (Fire)list2[i];
						}
						firesCount++;
					}
				}
				return closestDistSq <= 100f && firesCount >= 60;
			}, 18, (RegionType)14);
			if (closestDistSq <= 100f && firesCount >= 60)
			{
				Job val = ReturnToStationJob((Pawn_Drone)(object)pawn);
				if (val != null)
				{
					return val;
				}
			}
			return null;
		}

		private static Job ReturnToStationJob(Pawn_Drone drone)
		{
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Expected O, but got Unknown
			if (drone.BaseStation != null && ((Thing)drone).Map == ((Thing)drone.BaseStation).Map)
			{
				return new Job(PRFDefOf.PRFDrone_ReturnToStation, LocalTargetInfo.op_Implicit((Thing)(object)drone.BaseStation));
			}
			return null;
		}
	}
	public class JobGiver_DroneMain : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Expected O, but got Unknown
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Expected O, but got Unknown
			//IL_0108: Unknown result type (might be due to invalid IL or missing references)
			//IL_010d: Unknown result type (might be due to invalid IL or missing references)
			Pawn_Drone pawn_Drone = (Pawn_Drone)(object)pawn;
			if (!(pawn_Drone.BaseStation is Building_WorkGiverDroneStation building_WorkGiverDroneStation))
			{
				return null;
			}
			if (!((Thing)pawn_Drone.BaseStation).Spawned || ((Thing)pawn_Drone.BaseStation).Map != ((Thing)pawn).Map)
			{
				return new Job(PRFDefOf.PRFDrone_SelfTerminate);
			}
			if (pawn.workSettings == null)
			{
				pawn.workSettings = new Pawn_WorkSettings(pawn);
				pawn.workSettings.EnableAndInitialize();
				pawn.workSettings.DisableAll();
			}
			foreach (WorkTypeDef key in building_WorkGiverDroneStation.WorkSettingsDict.Keys)
			{
				pawn.workSettings.SetPriority(key, building_WorkGiverDroneStation.WorkSettingsDict[key] ? 3 : 0);
			}
			Job val = building_WorkGiverDroneStation.TryGiveJob((Pawn)(object)pawn_Drone);
			return (Job)(((object)val) ?? ((object)new Job(PRFDefOf.PRFDrone_ReturnToStation, LocalTargetInfo.op_Implicit((Thing)(object)building_WorkGiverDroneStation))));
		}
	}
}
namespace ProjectRimFactory.CultivatorTools
{
	public abstract class Building_CellIterator : Building
	{
		protected int CurrentPosition;

		private CompRefuelable compRefuelable;

		private CompPowerTrader compPowerTrader;

		protected CultivatorDefModExtension CultivatorDefModExtension;

		private bool Fueled
		{
			get
			{
				CompRefuelable obj = compRefuelable;
				return obj == null || obj.HasFuel;
			}
		}

		private bool Powered
		{
			get
			{
				CompPowerTrader obj = compPowerTrader;
				return obj == null || obj.PowerOn;
			}
		}

		protected virtual int TickRate => 250;

		protected abstract IntVec3 Current { get; }

		protected abstract int CellCount { get; }

		protected virtual bool CellValidator(IntVec3 cell)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return GenGrid.InBounds(cell, ((Thing)this).Map);
		}

		public override void ExposeData()
		{
			((Building)this).ExposeData();
			Scribe_Values.Look<int>(ref CurrentPosition, "currentNumber", 1, false);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			CultivatorDefModExtension = ((Def)((Thing)this).def).GetModExtension<CultivatorDefModExtension>();
			compRefuelable = ((ThingWithComps)this).GetComp<CompRefuelable>();
			compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
		}

		public override void DrawExtraSelectionOverlays()
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).DrawExtraSelectionOverlays();
			GenDraw.DrawFieldEdges(new List<IntVec3>(1) { Current }, Color.yellow, (float?)null, (HashSet<IntVec3>)null, 2900);
		}

		private void DoTickerWork()
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).TickRare();
			IntVec3 current = Current;
			if (!CellValidator(current) || DoIterationWork(current))
			{
				MoveNextInternal();
			}
		}

		protected abstract bool DoIterationWork(IntVec3 cell);

		protected override void Tick()
		{
			((ThingWithComps)this).Tick();
			if (((Thing)this).Spawned && Find.TickManager.TicksGame % TickRate == 0 && Powered && Fueled)
			{
				DoTickerWork();
			}
		}

		private void MoveNextInternal()
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			for (int i = 0; i < 10; i++)
			{
				CurrentPosition++;
				if (CurrentPosition >= CellCount)
				{
					CurrentPosition = 0;
				}
				IntVec3 current = Current;
				if (CellValidator(current))
				{
					break;
				}
			}
		}
	}
	public class Building_Cultivator : Building_SquareCellIterator
	{
		private CompOutputAdjustable compOutputAdjustable;

		protected override int TickRate => CultivatorDefModExtension?.TickFrequencyDivisor ?? 200;

		private static bool SeedsPleaseSupportActive => ProjectRimFactory_ModComponent.ModSupport_SeedsPlease || ProjectRimFactory_ModComponent.ModSupport_SeedsPleaseLite;

		private static bool SeedLiteDualCropSupportActive => ProjectRimFactory_ModComponent.ModSupport_SeedsPleaseLite || ProjectRimFactory_ModComponent.ModSupport_VEF_DualCropExtension;

		public override string DescriptionDetailed => TaggedString.op_Implicit(((Thing)this).DescriptionDetailed + " " + (SeedsPleaseSupportActive ? Translator.Translate("CultivatorTools_SeedsPleaseActiveDesc") : Translator.Translate("CultivatorTools_SeedsPleaseInactiveDesc")));

		public override string DescriptionFlavor => TaggedString.op_Implicit(((ThingWithComps)this).DescriptionFlavor + " " + (SeedsPleaseSupportActive ? Translator.Translate("CultivatorTools_SeedsPleaseActiveDesc") : Translator.Translate("CultivatorTools_SeedsPleaseInactiveDesc")));

		public override void PostMake()
		{
			((ThingWithComps)this).PostMake();
			compOutputAdjustable = ((ThingWithComps)this).GetComp<CompOutputAdjustable>();
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			compOutputAdjustable = ((ThingWithComps)this).GetComp<CompOutputAdjustable>();
		}

		protected override bool CellValidator(IntVec3 cell)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			return base.CellValidator(cell) && Utilities.GetIPlantToGrowSettable(cell, ((Thing)this).Map) != null;
		}

		protected override bool DoIterationWork(IntVec3 cell)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			IPlantToGrowSettable iPlantToGrowSettable = Utilities.GetIPlantToGrowSettable(cell, ((Thing)this).Map);
			ThingDef plantDefToGrow = iPlantToGrowSettable.GetPlantDefToGrow();
			foreach (Thing thing in GridsUtility.GetThingList(cell, ((Thing)this).Map))
			{
				Plant val = (Plant)(object)((thing is Plant) ? thing : null);
				if (val == null)
				{
					continue;
				}
				if (thing.def == plantDefToGrow)
				{
					if (val.Growth + 0.001f >= 1f)
					{
						CreatePlantProducts(val);
						return false;
					}
					return true;
				}
				CreatePlantProducts(val);
				if (!((Thing)val).Destroyed)
				{
					((Thing)val).Destroy((DestroyMode)0);
				}
				return false;
			}
			if (plantDefToGrow != null && iPlantToGrowSettable.CanPlantRightNow())
			{
				TryPlantNew(cell, plantDefToGrow);
			}
			return true;
		}

		public override void DrawExtraSelectionOverlays()
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			base.DrawExtraSelectionOverlays();
			if (SeedsPleaseSupportActive)
			{
				CellRect val = SeedsPleaseSupport.InputArea((Building)(object)this);
				GenDraw.DrawFieldEdges(((CellRect)(ref val)).Cells.ToList(), CommonColors.SeedsInputZone, (float?)null, (HashSet<IntVec3>)null, 2900);
			}
		}

		private void TryPlantNew(IntVec3 c, ThingDef plantDef)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Invalid comparison between Unknown and I4
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			if ((((BuildableDef)plantDef).blueprintDef == null || !SeedsPleaseSupportActive || (int)((BuildableDef)plantDef).blueprintDef.category != 2 || SeedsPleaseSupport.TryPlantNew(plantDef, SeedsPleaseSupport.InputArea((Building)(object)this), ((Thing)this).Map)) && PlantUtility.CanEverPlantAt(plantDef, c, ((Thing)this).Map, false, false) && PlantUtility.AdjacentSowBlocker(plantDef, c, ((Thing)this).Map) == null && PlantUtility.GrowthSeasonNow(c, ((Thing)this).Map, plantDef) && (plantDef.plant.vacuumResistant || VacuumUtility.GetVacuum(c, ((Thing)this).Map) < 0.5f))
			{
				GenPlace.TryPlaceThing(ThingMaker.MakeThing(plantDef, (ThingDef)null), c, ((Thing)this).Map, (ThingPlaceMode)0, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			yield return (Gizmo)new Command_Action
			{
				action = MakeMatchingGrowZone,
				hotKey = KeyBindingDefOf.Misc2,
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("CommandSunLampMakeGrowingZoneDesc")),
				icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Growing", true),
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("CommandSunLampMakeGrowingZoneLabel"))
			};
		}

		private void MakeMatchingGrowZone()
		{
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Expected O, but got Unknown
			Designator_ZoneAdd_Growing designator = new Designator_ZoneAdd_Growing();
			((Designator)designator).DesignateMultiCell(from <>h__TransparentIdentifier0 in Iter.CellPattern.Select((IntVec3 tempCell) => new
				{
					tempCell = tempCell,
					pos = tempCell + ((Thing)this).Position
				}).Where(<>h__TransparentIdentifier0 =>
				{
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0011: Unknown result type (might be due to invalid IL or missing references)
					AcceptanceReport val = ((Designator)designator).CanDesignateCell(<>h__TransparentIdentifier0.pos);
					return ((AcceptanceReport)(ref val)).Accepted;
				})
				select <>h__TransparentIdentifier0.pos);
		}

		private bool ForbiddenCells(IntVec3 cell)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			return cell != ((Thing)this).Position;
		}

		protected virtual void CreatePlantProducts(Plant p)
		{
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00de: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			int num = p.YieldNow();
			if (num > 0)
			{
				Thing val = ThingMaker.MakeThing(((Thing)p).def.plant.harvestedThingDef, (ThingDef)null);
				val.stackCount = num;
				GenPlace.TryPlaceThing(val, compOutputAdjustable.CurrentCell, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)ForbiddenCells, (Rot4?)null, 1);
			}
			if (SeedsPleaseSupportActive && ((BuildableDef)((Thing)p).def).blueprintDef != null)
			{
				SeedsPleaseSupport.CreatePlantSeeds(p, compOutputAdjustable.CurrentCell, ((Thing)this).Map);
			}
			if (PRFGameComponent.PRF_StaticPawn == null)
			{
				PRFGameComponent.GenStaticPawn();
			}
			object value = null;
			IntVec3 position = IntVec3.Invalid;
			if (SeedLiteDualCropSupportActive)
			{
				value = ReflectionUtility.MapIndexOrState.GetValue(PRFGameComponent.PRF_StaticPawn);
				position = ((Thing)PRFGameComponent.PRF_StaticPawn).Position;
				((Thing)PRFGameComponent.PRF_StaticPawn).Position = compOutputAdjustable.CurrentCell;
				ReflectionUtility.MapIndexOrState.SetValue(PRFGameComponent.PRF_StaticPawn, ReflectionUtility.MapIndexOrState.GetValue(this));
			}
			p.PlantCollected(PRFGameComponent.PRF_StaticPawn, (PlantDestructionMode)2);
			if (SeedLiteDualCropSupportActive)
			{
				ReflectionUtility.MapIndexOrState.SetValue(PRFGameComponent.PRF_StaticPawn, value);
				((Thing)PRFGameComponent.PRF_StaticPawn).Position = position;
			}
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
	public class DefModExtension_DoneBehavior : DefModExtension
	{
		public bool hauling = false;
	}
	public abstract class Building_RadialCellIterator : Building_CellIterator
	{
		public int RadialCellCount { get; private set; }

		protected override IntVec3 Current => GenRadial.RadialPattern[CurrentPosition + 1] + ((Thing)this).Position;

		protected override int CellCount => RadialCellCount;

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			RadialCellCount = GenRadial.NumCellsInRadius(((BuildableDef)((Thing)this).def).specialDisplayRadius);
		}
	}
	public class Building_Sprinkler : Building_RadialCellIterator
	{
		protected override int TickRate => CultivatorDefModExtension?.TickFrequencyDivisor ?? 50;

		private int GrowRate => CultivatorDefModExtension?.GrowRate ?? 2500;

		protected override bool DoIterationWork(IntVec3 cell)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			Plant plant = GridsUtility.GetPlant(cell, ((Thing)this).Map);
			if (plant == null || ((Thing)this).Map.reservationManager.IsReservedByAnyoneOf(LocalTargetInfo.op_Implicit((Thing)(object)plant), ((Thing)this).Faction))
			{
				return true;
			}
			float growthRatePerTickFor = GetGrowthRatePerTickFor(plant);
			plant.Growth += growthRatePerTickFor * (float)GrowRate;
			return true;
		}

		private static float GetGrowthRatePerTickFor(Plant p)
		{
			float num = 1f / (60000f * ((Thing)p).def.plant.growDays);
			return num * p.GrowthRate;
		}
	}
	public abstract class Building_SquareCellIterator : Building_CellIterator
	{
		protected SquareCellIterator Iter;

		private Cache<List<IntVec3>> selectedCellsCache;

		protected override IntVec3 Current => Iter.CellPattern[CurrentPosition] + ((Thing)this).Position;

		private List<IntVec3> CellsInRange => selectedCellsCache.Get();

		protected override int CellCount => Iter.CellPattern.Length;

		private List<IntVec3> UpdateCellsCache()
		{
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			int squareAreaRadius = CultivatorDefModExtension.squareAreaRadius;
			List<IntVec3> list = new List<IntVec3>((squareAreaRadius * 2 + 1) * (squareAreaRadius * 2 + 1));
			for (int i = -squareAreaRadius; i <= squareAreaRadius; i++)
			{
				for (int j = -squareAreaRadius; j <= squareAreaRadius; j++)
				{
					list.Add(new IntVec3(i, 0, j) + ((Thing)this).Position);
				}
			}
			return list;
		}

		public override void DrawExtraSelectionOverlays()
		{
			base.DrawExtraSelectionOverlays();
			GenDraw.DrawFieldEdges(CellsInRange, 2900);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			Iter = new SquareCellIterator(CultivatorDefModExtension.squareAreaRadius);
			selectedCellsCache = new Cache<List<IntVec3>>(UpdateCellsCache);
		}
	}
	public class CultivatorDefModExtension : DefModExtension, IXMLThingDescription
	{
		public int TickFrequencyDivisor = 200;

		public int squareAreaRadius;

		public int GrowRate = 2500;

		public string GetDescription(ThingDef def)
		{
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			int num = 0;
			if (squareAreaRadius > 0)
			{
				num = squareAreaRadius;
			}
			else if (((BuildableDef)def).specialDisplayRadius > 0f)
			{
				num = (int)((BuildableDef)def).specialDisplayRadius;
			}
			TaggedString val = TranslatorFormattedStringExtensions.Translate("PRF_UTD_CultivatorDefModExtension_Range", NamedArgument.op_Implicit(num)) + "\r\n";
			val += TranslatorFormattedStringExtensions.Translate("PRF_UTD_CultivatorDefModExtension_Tickdev", NamedArgument.op_Implicit(TickFrequencyDivisor)) + "\r\n";
			return TaggedString.op_Implicit(val);
		}
	}
	public class PlaceWorker_HighlightPlaceableCells : PlaceWorker
	{
		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			((PlaceWorker)this).DrawGhost(def, center, rot, ghostCol, (Thing)null);
			int squareAreaRadius = ((Def)def).GetModExtension<CultivatorDefModExtension>().squareAreaRadius;
			List<IntVec3> list = new List<IntVec3>((squareAreaRadius * 2 + 1) * (squareAreaRadius * 2 + 1));
			for (int i = -squareAreaRadius; i <= squareAreaRadius; i++)
			{
				for (int j = -squareAreaRadius; j <= squareAreaRadius; j++)
				{
					list.Add(new IntVec3(i, 0, j) + center);
				}
			}
			GenDraw.DrawFieldEdges(list, 2900);
		}
	}
	public class SquareCellIterator
	{
		private int rangeInt;

		public readonly IntVec3[] CellPattern;

		public int Range => rangeInt;

		public SquareCellIterator(int range)
		{
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			rangeInt = range;
			CellPattern = (IntVec3[])(object)new IntVec3[(range * 2 + 1) * (range * 2 + 1)];
			int num = 0;
			for (int i = -range; i <= range; i++)
			{
				if ((i & 1) == 0)
				{
					int num2 = -range;
					while (num2 <= range)
					{
						CellPattern[num] = new IntVec3(i, 0, num2);
						num2++;
						num++;
					}
				}
				else
				{
					int num3 = range;
					while (num3 >= -range)
					{
						CellPattern[num] = new IntVec3(i, 0, num3);
						num3--;
						num++;
					}
				}
			}
		}
	}
	public static class Utilities
	{
		public unsafe static string AsCompassDirection(this Rot4 rot)
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			switch (((Rot4)(ref rot)).AsByte)
			{
			case 0:
				return TaggedString.op_Implicit(Translator.Translate("North"));
			case 1:
				return TaggedString.op_Implicit(Translator.Translate("East"));
			case 2:
				return TaggedString.op_Implicit(Translator.Translate("South"));
			case 3:
				return TaggedString.op_Implicit(Translator.Translate("West"));
			default:
			{
				Rot4 val = rot;
				throw new ArgumentException("Invalid rotation or rotation factor " + ((object)(*(Rot4*)(&val))/*cast due to constrained. prefix*/).ToString() + ". Valid rotations are 0, 1, 2 and 3");
			}
			}
		}

		public static IPlantToGrowSettable GetIPlantToGrowSettable(IntVec3 c, Map map)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			Zone zone = GridsUtility.GetZone(c, map);
			IPlantToGrowSettable val = (from t in GridsUtility.GetThingList(c, map)
				where (int)t.def.category == 3
				select t).OfType<IPlantToGrowSettable>().FirstOrDefault();
			if (val != null)
			{
				return val;
			}
			IPlantToGrowSettable val2 = (IPlantToGrowSettable)(object)((zone is IPlantToGrowSettable) ? zone : null);
			if (val2 != null)
			{
				return val2;
			}
			return null;
		}

		public static bool CanPlantRightNow(this IPlantToGrowSettable planter)
		{
			bool flag = planter.CanAcceptSowNow();
			bool flag2 = flag;
			if (flag2)
			{
				if (1 == 0)
				{
				}
				Zone_Growing val = (Zone_Growing)(object)((planter is Zone_Growing) ? planter : null);
				bool flag3;
				if (val != null)
				{
					flag3 = val.allowSow;
				}
				else
				{
					Thing val2 = (Thing)(object)((planter is Thing) ? planter : null);
					flag3 = val2 == null || !ForbidUtility.IsForbidden(val2, Faction.OfPlayer);
				}
				if (1 == 0)
				{
				}
				flag2 = flag3;
			}
			return flag2;
		}
	}
}
namespace ProjectRimFactory.Common
{
	public class Cache<T>
	{
		private Func<T> cacheGetter;

		private T cache;

		private int updateInterval = 10;

		private int lastTick;

		public Cache(Func<T> func)
		{
			cacheGetter = func;
			cache = cacheGetter();
		}

		public T Get()
		{
			if (lastTick + updateInterval < Find.TickManager.TicksAbs)
			{
				return UpdateCache();
			}
			return cache;
		}

		private T UpdateCache()
		{
			lastTick = Find.TickManager.TicksAbs;
			return cache = cacheGetter();
		}
	}
	internal class Command_ActionRightLeft : Command
	{
		private static bool wasRightClick;

		public Action ActionL;

		public Action ActionR;

		public override void ProcessInput(Event ev)
		{
			((Command)this).ProcessInput(ev);
			if (wasRightClick)
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
				wasRightClick = false;
			}
			if (Input.GetMouseButtonDown(1))
			{
				wasRightClick = true;
			}
		}
	}
	internal static class CommonColors
	{
		public enum CellPattern
		{
			BlueprintMin,
			BlueprintMax,
			Instance,
			OtherInstance,
			OutputCell,
			OutputZone,
			InputCell,
			InputZone
		}

		public static Color BlueprintMin = Color.white;

		public static Color BlueprintMax = Color.gray.A(0.6f);

		public static Color Instance = Color.white;

		public static Color OtherInstance = Color.white.A(0.35f);

		public static Color InputCell = Color.white;

		private static readonly Color InputZone = Color.white.A(0.5f);

		public static Color OutputCell = Color.yellow;

		public static Color OutputZone = Color.yellow.A(0.5f);

		public static Color WorkbenchAlpha = Color.blue.A(0.5f);

		public static Color SeedsInputZone = new Color(0.3f, 0.15f, 0f);

		public static Color GetCellPatternColor(CellPattern pat)
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			if (1 == 0)
			{
			}
			Color result = (Color)(pat switch
			{
				CellPattern.BlueprintMin => BlueprintMin, 
				CellPattern.BlueprintMax => BlueprintMax, 
				CellPattern.Instance => Instance, 
				CellPattern.OtherInstance => OtherInstance, 
				CellPattern.OutputCell => OutputCell, 
				CellPattern.OutputZone => OutputZone, 
				CellPattern.InputCell => InputCell, 
				CellPattern.InputZone => InputZone, 
				_ => Color.white, 
			});
			if (1 == 0)
			{
			}
			return result;
		}
	}
	internal static class CommonGUIFunctions
	{
		public static void Label(Rect rect, string label, GUIStyle gUIStyle)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			Rect val = rect;
			float num = Prefs.UIScale / 2f;
			if (Prefs.UIScale > 1f && Math.Abs(num - Mathf.Floor(num)) > float.Epsilon)
			{
				((Rect)(ref val)).xMin = UIScaling.AdjustCoordToUIScalingFloor(((Rect)(ref rect)).xMin);
				((Rect)(ref val)).xMax = UIScaling.AdjustCoordToUIScalingFloor(((Rect)(ref rect)).xMax);
				((Rect)(ref val)).yMin = UIScaling.AdjustCoordToUIScalingFloor(((Rect)(ref rect)).yMin);
				((Rect)(ref val)).yMax = UIScaling.AdjustCoordToUIScalingFloor(((Rect)(ref rect)).yMax);
			}
			GUI.Label(val, label, gUIStyle);
		}
	}
	public class CompCallTickRareFromTick : ThingComp
	{
		public override void CompTick()
		{
			((ThingComp)this).CompTick();
			if (Find.TickManager.TicksGame % 250 == 0)
			{
				ThingWithComps parent = base.parent;
				if (parent != null)
				{
					((Entity)parent).TickRare();
				}
			}
		}
	}
	public class CompEffecter : ThingComp, ITicker
	{
		private Effecter effecter;

		private Sustainer sound;

		private bool effectOnInt;

		private bool enable = true;

		private CompProperties_Effecter Props => (CompProperties_Effecter)(object)base.props;

		protected virtual bool ShouldBeEffectNow
		{
			get
			{
				if (!((Thing)base.parent).Spawned)
				{
					return false;
				}
				if (!FlickUtility.WantsToBeOn((Thing)(object)base.parent))
				{
					return false;
				}
				CompPowerTrader val = ThingCompUtility.TryGetComp<CompPowerTrader>((Thing)(object)base.parent);
				if (val != null && !val.PowerOn)
				{
					return false;
				}
				CompRefuelable val2 = ThingCompUtility.TryGetComp<CompRefuelable>((Thing)(object)base.parent);
				if (val2 != null && !val2.HasFuel)
				{
					return false;
				}
				CompSendSignalOnCountdown val3 = ThingCompUtility.TryGetComp<CompSendSignalOnCountdown>((Thing)(object)base.parent);
				if (val3 != null && val3.ticksLeft <= 0)
				{
					return false;
				}
				return true;
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			((ThingComp)this).PostSpawnSetup(respawningAfterLoad);
			InitializeEffecter();
			((Thing)base.parent).Map.GetComponent<PRFMapComponent>()?.AddTicker(this);
			UpdateEffecter();
		}

		public void Tick()
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			if (effectOnInt)
			{
				Effecter obj = effecter;
				if (obj != null)
				{
					obj.EffectTick(TargetInfo.op_Implicit((Thing)(object)base.parent), TargetInfo.op_Implicit((Thing)(object)base.parent));
				}
				Sustainer obj2 = sound;
				if (obj2 != null)
				{
					obj2.SustainerUpdate();
				}
			}
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = (DestroyMode)0)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostDeSpawn(map, mode);
			FinalizeEffecter();
			map.GetComponent<PRFMapComponent>()?.RemoveTicker(this);
		}

		private void InitializeEffecter()
		{
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			CompProperties_Effecter props = Props;
			object obj;
			if (props == null)
			{
				obj = null;
			}
			else
			{
				EffecterDef effect = props.effect;
				obj = ((effect != null) ? effect.Spawn() : null);
			}
			effecter = (Effecter)obj;
			CompProperties_Effecter props2 = Props;
			object obj2;
			if (props2 == null)
			{
				obj2 = null;
			}
			else
			{
				SoundDef obj3 = props2.sound;
				obj2 = ((obj3 != null) ? SoundStarter.TrySpawnSustainer(obj3, SoundInfo.op_Implicit((Thing)(object)base.parent)) : null);
			}
			sound = (Sustainer)obj2;
		}

		private void FinalizeEffecter()
		{
			Effecter obj = effecter;
			if (obj != null)
			{
				obj.Cleanup();
			}
			Sustainer obj2 = sound;
			if (obj2 != null)
			{
				obj2.End();
			}
			effecter = null;
			sound = null;
		}

		private void UpdateEffecter()
		{
			bool flag = ShouldBeEffectNow && enable;
			if (effectOnInt != flag)
			{
				effectOnInt = flag;
				if (effectOnInt)
				{
					InitializeEffecter();
				}
				else
				{
					FinalizeEffecter();
				}
			}
		}

		public override void ReceiveCompSignal(string signal)
		{
			((ThingComp)this).ReceiveCompSignal(signal);
			bool flag;
			switch (signal)
			{
			case "PowerTurnedOn":
			case "PowerTurnedOff":
			case "FlickedOn":
			case "FlickedOff":
			case "Refueled":
			case "RanOutOfFuel":
			case "ScheduledOn":
			case "ScheduledOff":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				UpdateEffecter();
			}
		}
	}
	public class CompProperties_Effecter : CompProperties
	{
		public EffecterDef effect;

		public SoundDef sound;

		public CompProperties_Effecter()
		{
			base.compClass = typeof(CompEffecter);
		}
	}
	public class CompGlowerPulse : CompGlower, ITicker
	{
		private bool glows = true;

		[Unsaved(false)]
		private bool needUpdate = true;

		private CompProperties_GlowerPulse Props => (CompProperties_GlowerPulse)(object)((ThingComp)this).props;

		public bool Glows
		{
			set
			{
				glows = value;
				Props.Glows = value;
				needUpdate = true;
			}
		}

		public override void PostExposeData()
		{
			((CompGlower)this).PostExposeData();
			Scribe_Values.Look<bool>(ref glows, "glows", true, false);
			Props.Glows = glows;
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			((CompGlower)this).PostSpawnSetup(respawningAfterLoad);
			Props.Glows = glows;
			((Thing)((ThingComp)this).parent).Map.GetComponent<PRFMapComponent>()?.AddTicker(this);
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = (DestroyMode)0)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			((CompGlower)this).PostDeSpawn(map, mode);
			map.GetComponent<PRFMapComponent>()?.RemoveTicker(this);
		}

		public void Tick()
		{
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			if (needUpdate || Props.pulse)
			{
				Props.Update();
				((Thing)((ThingComp)this).parent).Map.glowGrid.DirtyCell(((Thing)((ThingComp)this).parent).Position);
				needUpdate = false;
			}
		}
	}
	public class CompProperties_GlowerPulse : CompProperties_Glower
	{
		public int lastTick = 0;

		public float minGlowRadius = 10f;

		public float maxGlowRadius = 15f;

		private int lag = Rand.Int % 60;

		public ColorInt minGlowColor = new ColorInt(255, 255, 255, 0) * 0.45f;

		public ColorInt maxGlowColor = new ColorInt(255, 255, 255, 0) * 1.45f;

		public float intervalTicks = 60f;

		public Type easingType;

		private IEasing easing = null;

		public bool pulse = true;

		public bool Glows { get; set; }

		public CompProperties_GlowerPulse()
		{
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			((CompProperties)this).compClass = typeof(CompGlowerPulse);
		}

		public void Update()
		{
			//IL_0171: Unknown result type (might be due to invalid IL or missing references)
			//IL_0176: Unknown result type (might be due to invalid IL or missing references)
			//IL_017b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0180: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0134: Unknown result type (might be due to invalid IL or missing references)
			//IL_013f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0145: Unknown result type (might be due to invalid IL or missing references)
			//IL_014a: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			if (lastTick == Find.TickManager.TicksGame)
			{
				return;
			}
			lastTick = Find.TickManager.TicksGame;
			if (Glows)
			{
				if (!pulse)
				{
					base.glowColor = ColorIntUtility.AsColorInt(Color32.Lerp(((ColorInt)(ref minGlowColor)).ProjectToColor32(), ((ColorInt)(ref maxGlowColor)).ProjectToColor32(), 0.5f));
					base.glowRadius = Mathf.Lerp(minGlowRadius, maxGlowRadius, 0.5f);
					return;
				}
				if (easing == null)
				{
					if (easingType != null)
					{
						easing = (IEasing)Activator.CreateInstance(easingType);
					}
					else
					{
						easing = new EasingLinear();
					}
				}
				float num = (float)(Find.TickManager.TicksGame + lag) % (intervalTicks * 2f) / intervalTicks;
				if (num > 1f)
				{
					num = 2f - num;
				}
				float value = easing.GetValue(num);
				base.glowColor = ColorIntUtility.AsColorInt(Color32.Lerp(((ColorInt)(ref minGlowColor)).ProjectToColor32(), ((ColorInt)(ref maxGlowColor)).ProjectToColor32(), value));
				base.glowRadius = Mathf.Lerp(minGlowRadius, maxGlowRadius, value);
			}
			else
			{
				base.glowColor = ColorIntUtility.AsColorInt(Color32.op_Implicit(Color.clear));
				base.glowRadius = 0f;
			}
		}
	}
	public interface IEasing
	{
		float GetValue(float t);
	}
	public class EasingLinear : IEasing
	{
		public float GetValue(float t)
		{
			return t;
		}
	}
	public class EasingSineInOut : IEasing
	{
		public float GetValue(float t)
		{
			return (0f - (Mathf.Cos((float)Math.PI * t) - 1f)) / 2f;
		}
	}
	public class EasingQuadInOut : IEasing
	{
		public float GetValue(float t)
		{
			return (t < 0.5f) ? (2f * t * t) : (1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f);
		}
	}
	public class EasingQuadIn : IEasing
	{
		public float GetValue(float t)
		{
			return t * t;
		}
	}
	public class EasingQuadOut : IEasing
	{
		public float GetValue(float t)
		{
			return 1f - (1f - t) * (1f - t);
		}
	}
	public class EasingCubicInOut : IEasing
	{
		public float GetValue(float t)
		{
			return (t < 0.5f) ? (4f * t * t * t) : (1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f);
		}
	}
	public class EasingCubicIn : IEasing
	{
		public float GetValue(float t)
		{
			return t * t * t;
		}
	}
	public class EasingCubicOut : IEasing
	{
		public float GetValue(float t)
		{
			return 1f - Mathf.Pow(1f - t, 3f);
		}
	}
	public class EasingExpInOut : IEasing
	{
		public float GetValue(float t)
		{
			return (t == 0f) ? 0f : ((t == 1f) ? 1f : ((t < 0.5f) ? (Mathf.Pow(2f, 20f * t - 10f) / 2f) : ((2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f)));
		}
	}
	public class EasingElasticInOut : IEasing
	{
		private const float c5 = 1.3962634f;

		public float GetValue(float t)
		{
			return (t == 0f) ? 0f : ((t == 1f) ? 1f : ((t < 0.5f) ? ((0f - Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * 1.3962634f)) / 2f) : (Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * 1.3962634f) / 2f + 1f)));
		}
	}
	public class EasingBounceInOut : IEasing
	{
		private const float n1 = 7.5625f;

		private const float d1 = 2.75f;

		public float GetValue(float t)
		{
			return ((double)t < 0.5) ? ((1f - EaseBounceOut(1f - 2f * t)) / 2f) : ((1f + EaseBounceOut(2f * t - 1f)) / 2f);
		}

		private float EaseBounceOut(float t)
		{
			if (t < 0.36363637f)
			{
				return 7.5625f * t * t;
			}
			if (t < 0.72727275f)
			{
				return 7.5625f * (t -= 0.54545456f) * t + 0.75f;
			}
			if ((double)t < 0.9090909090909091)
			{
				return 7.5625f * (t -= 0.8181818f) * t + 0.9375f;
			}
			return 7.5625f * (t -= 21f / 22f) * t + 63f / 64f;
		}
	}
	public class CompOutputAdjustable : ThingComp
	{
		private int index;

		public bool Visible = true;

		private List<IntVec3> possibleOutputs = new List<IntVec3>();

		private int Index
		{
			get
			{
				return index;
			}
			set
			{
				index = value;
				if (index < 0)
				{
					index += possibleOutputs.Count;
				}
				index %= possibleOutputs.Count;
			}
		}

		private CompProperties_CompOutputAdjustable Props => (CompProperties_CompOutputAdjustable)(object)base.props;

		public IntVec3 CurrentCell => possibleOutputs[Index];

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostSpawnSetup(respawningAfterLoad);
			if (GravshipPlacementUtility.placingGravship)
			{
				Rot4 rotation = Find.CurrentGravship.Rotation;
				int asInt = ((Rot4)(ref rotation)).AsInt;
				if (1 == 0)
				{
				}
				int num = asInt switch
				{
					1 => 3, 
					3 => 1, 
					_ => asInt, 
				};
				if (1 == 0)
				{
				}
				asInt = num;
				Index += possibleOutputs.Count / 4 * asInt;
			}
			if (Props.SupportDiagonal)
			{
				possibleOutputs = GenAdj.CellsAdjacent8Way((Thing)(object)base.parent).ToList();
			}
			else
			{
				possibleOutputs = GenAdj.CellsAdjacentCardinal((Thing)(object)base.parent).ToList();
			}
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostDrawExtraSelectionOverlays();
			if (Visible)
			{
				GenDraw.DrawFieldEdges(new List<IntVec3>(1) { CurrentCell }, Color.yellow, (float?)null, (HashSet<IntVec3>)null, 2900);
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			if (Visible)
			{
				yield return (Gizmo)(object)new Command_ActionRightLeft
				{
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("AdjustDirection_Output")),
					ActionL = delegate
					{
						Index++;
					},
					ActionR = delegate
					{
						Index--;
					},
					icon = (Texture)(object)TexUI.RotRightTex,
					defaultIconColor = Color.green
				};
			}
		}

		public override void PostExposeData()
		{
			((ThingComp)this).PostExposeData();
			Scribe_Values.Look<int>(ref index, "outputSlotIndex", 0, false);
			Scribe_Values.Look<bool>(ref Visible, "Visible", true, false);
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((ThingComp)this).CompGetGizmosExtra();
		}
	}
	public class CompProperties_CompOutputAdjustable : CompProperties
	{
		public bool SupportDiagonal = false;

		public CompProperties_CompOutputAdjustable()
		{
			base.compClass = typeof(CompOutputAdjustable);
		}
	}
	public class CompPowerWorkSetting : ThingComp, IPowerSupplyMachine
	{
		private enum rangeTypeClassEnum
		{
			CircleRange,
			FacingRectRange,
			RectRange
		}

		public IRangeCells rangeCells;

		public bool SpeedSettingHide = false;

		public bool RangeSettingHide = false;

		private float powerForSpeed;

		public Rot4 RangeTypeRot = Rot4.North;

		private float powerForRange;

		private int rangeTypeSeletion = -1;

		[Unsaved(false)]
		private CompPowerTrader powerComp;

		public IRangeCells[] rangeTypes = new IRangeCells[3]
		{
			new CircleRange(),
			new FacingRectRange(),
			new RectRange()
		};

		public CompProperties_PowerWorkSetting Props => (CompProperties_PowerWorkSetting)(object)base.props;

		public int MaxPowerForSpeed => (int)(((FloatRange)(ref Props.floatrange_SpeedFactor)).Span * Props.powerPerStepSpeed);

		public int MaxPowerForRange => (int)(((FloatRange)(ref Props.floatrange_Range)).Span * Props.powerPerStepRange);

		public float SupplyPowerForSpeed
		{
			get
			{
				return powerForSpeed;
			}
			set
			{
				powerForSpeed = value;
				AdjustPower();
				RefreshPowerStatus();
			}
		}

		public float SupplyPowerForRange
		{
			get
			{
				return powerForRange;
			}
			set
			{
				powerForRange = value;
				AdjustPower();
				RefreshPowerStatus();
			}
		}

		public virtual bool Glowable => false;

		public virtual bool Glow
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public virtual bool SpeedSetting
		{
			get
			{
				if (SpeedSettingHide)
				{
					return false;
				}
				return ((FloatRange)(ref Props.floatrange_SpeedFactor)).Span > 0f;
			}
		}

		public bool RangeSetting
		{
			get
			{
				if (RangeSettingHide)
				{
					return false;
				}
				return ((FloatRange)(ref Props.floatrange_Range)).Span > 0f;
			}
		}

		private int rangeTypeSelection
		{
			get
			{
				if (rangeCells == null)
				{
					rangeCells = (IRangeCells)Activator.CreateInstance(Props.rangeType);
				}
				if (rangeCells.ToText() == new CircleRange().ToText())
				{
					return 0;
				}
				if (rangeCells.ToText() == new FacingRectRange().ToText())
				{
					return 1;
				}
				if (rangeCells.ToText() == new RectRange().ToText())
				{
					return 2;
				}
				return 2;
			}
			set
			{
				if (1 == 0)
				{
				}
				IRangeCells rangeCells = value switch
				{
					0 => new CircleRange(), 
					1 => new FacingRectRange(), 
					2 => new RectRange(), 
					_ => this.rangeCells, 
				};
				if (1 == 0)
				{
				}
				this.rangeCells = rangeCells;
			}
		}

		public float BasePowerConsumption => (float)ReflectionUtility.CompPropertiesPowerBasePowerConsumption.GetValue(((CompPower)powerComp).Props);

		public int CurrentPowerConsumption => (int)powerComp.PowerOutput;

		public Dictionary<string, int> AdditionalPowerConsumption => (base.parent as IAdditionalPowerConsumption)?.AdditionalPowerConsumption ?? null;

		private int AdditionalPowerDrain
		{
			get
			{
				Dictionary<string, int> additionalPowerConsumption = AdditionalPowerConsumption;
				if (additionalPowerConsumption != null && additionalPowerConsumption.Count > 0)
				{
					return AdditionalPowerConsumption.Values.ToList().Sum();
				}
				return 0;
			}
		}

		public float PowerPerStepSpeed => Props.powerPerStepSpeed / Props.powerStepFactor;

		public float PowerPerStepRange => Props.powerPerStepRange;

		public FloatRange FloatRangeRange => Props.floatrange_Range;

		public float CurrentRange => GetRange();

		public FloatRange FloatRangeSpeedFactor => Props.floatrange_SpeedFactor;

		public float CurrentSpeedFactor => GetSpeedFactor();

		public override void PostExposeData()
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Invalid comparison between Unknown and I4
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Invalid comparison between Unknown and I4
			((ThingComp)this).PostExposeData();
			if ((int)Scribe.mode == 1)
			{
				rangeTypeSeletion = rangeTypeSelection;
			}
			Scribe_Values.Look<bool>(ref RangeSettingHide, "RangeSettingHide", false, false);
			Scribe_Values.Look<float>(ref powerForSpeed, "powerForSpeed", 0f, false);
			Scribe_Values.Look<float>(ref powerForRange, "powerForRange", 0f, false);
			Scribe_Values.Look<int>(ref rangeTypeSeletion, "rangeType", -1, false);
			Scribe_Values.Look<Rot4>(ref RangeTypeRot, "RangeTypeRot", Rot4.North, false);
			if ((int)Scribe.mode != 1)
			{
				if (rangeTypeSeletion == -1)
				{
					rangeCells = null;
					rangeTypeSeletion = rangeTypeSelection;
				}
				rangeTypeSelection = rangeTypeSeletion;
			}
			AdjustPower();
			RefreshPowerStatus();
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			((ThingComp)this).PostSpawnSetup(respawningAfterLoad);
			powerComp = ThingCompUtility.TryGetComp<CompPowerTrader>((Thing)(object)base.parent);
			AdjustPower();
			RefreshPowerStatus();
		}

		protected virtual void AdjustPower()
		{
			powerForSpeed = Mathf.Clamp(powerForSpeed, 0f, (float)MaxPowerForSpeed);
			powerForRange = Mathf.Clamp(powerForRange, 0f, (float)MaxPowerForRange);
		}

		public void RefreshPowerStatus()
		{
			if (powerComp != null)
			{
				powerComp.PowerOutput = 0f - (float)ReflectionUtility.CompPropertiesPowerBasePowerConsumption.GetValue(((CompPower)powerComp).Props) - SupplyPowerForSpeed - SupplyPowerForRange - (float)AdditionalPowerDrain;
			}
		}

		public virtual float GetSpeedFactor()
		{
			float num = 0f;
			if (MaxPowerForSpeed != 0)
			{
				num = powerForSpeed / (float)MaxPowerForSpeed;
			}
			return Mathf.Lerp(Props.floatrange_SpeedFactor.min, Props.floatrange_SpeedFactor.max, num);
		}

		public virtual float GetRange()
		{
			if (!RangeSetting)
			{
				return 0f;
			}
			float num = powerForRange / (float)MaxPowerForRange;
			return Mathf.Lerp(Props.floatrange_Range.min, Props.floatrange_Range.max, num);
		}

		public virtual IEnumerable<IntVec3> GetRangeCells()
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			if (!RangeSetting)
			{
				return null;
			}
			return RangeCells(((Thing)base.parent).Position, RangeTypeRot, ((Thing)base.parent).def, GetRange());
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostDrawExtraSelectionOverlays();
			if (RangeSetting)
			{
				DrawRangeCells(CommonColors.Instance);
			}
		}

		public virtual void DrawRangeCells(Color color)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			float range = GetRange();
			GenDraw.DrawFieldEdges(GetRangeCells().ToList(), color, (float?)null, (HashSet<IntVec3>)null, 2900);
		}

		public IEnumerable<IntVec3> RangeCells(IntVec3 center, Rot4 rot, ThingDef thingDef, float range)
		{
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			if (rangeCells == null)
			{
				rangeCells = (IRangeCells)Activator.CreateInstance(Props.rangeType);
			}
			return rangeCells.RangeCells(center, rot, thingDef, range);
		}
	}
	public class CompProperties_PowerWorkSetting : CompProperties, IXMLThingDescription
	{
		public FloatRange floatrange_SpeedFactor;

		public float powerPerStepSpeed;

		public float powerStepFactor = 1f;

		public FloatRange floatrange_Range;

		public float powerPerStepRange;

		public bool allowManualRangeTypeChange = false;

		public Type rangeType;

		private IRangeCells propsRangeType => (IRangeCells)Activator.CreateInstance(rangeType);

		public CompProperties_PowerWorkSetting()
		{
			base.compClass = typeof(CompPowerWorkSetting);
		}

		public override void DrawGhost(IntVec3 center, Rot4 rot, ThingDef thingDef, Color ghostCol, AltitudeLayer drawAltitude, Thing thing = null)
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			if (!(((FloatRange)(ref floatrange_Range)).Span > 0f))
			{
				return;
			}
			((CompProperties)this).DrawGhost(center, rot, thingDef, ghostCol, drawAltitude, thing);
			IEnumerable<IntVec3> source = propsRangeType.RangeCells(center, rot, thingDef, floatrange_Range.min);
			IEnumerable<IntVec3> source2 = propsRangeType.RangeCells(center, rot, thingDef, floatrange_Range.max);
			(from a in source.Select((IntVec3 c) => new
				{
					Cell = c,
					Color = CommonColors.BlueprintMin
				}).Concat(source2.Select((IntVec3 c) => new
				{
					Cell = c,
					Color = CommonColors.BlueprintMax
				}))
				group a by a.Color).ToList().ForEach(g =>
			{
				//IL_002b: Unknown result type (might be due to invalid IL or missing references)
				GenDraw.DrawFieldEdges(g.Select(a => a.Cell).ToList(), g.Key, (float?)null, (HashSet<IntVec3>)null, 2900);
			});
			Map currentMap = Find.CurrentMap;
			(from t in currentMap.listerThings.ThingsOfDef(thingDef)
				select ThingCompUtility.TryGetComp<CompPowerWorkSetting>(t) into c
				where c?.RangeSetting ?? false
				select c).ToList().ForEach(delegate(CompPowerWorkSetting c)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				c.DrawRangeCells(CommonColors.OtherInstance);
			});
		}

		private static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
		{
			while (toCheck != null && toCheck != typeof(object))
			{
				Type type = (toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck);
				if (generic == type)
				{
					return true;
				}
				toCheck = toCheck.BaseType;
			}
			return false;
		}

		public string GetDescription(ThingDef def)
		{
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0141: Unknown result type (might be due to invalid IL or missing references)
			//IL_0146: Unknown result type (might be due to invalid IL or missing references)
			//IL_014b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0173: Unknown result type (might be due to invalid IL or missing references)
			//IL_0178: Unknown result type (might be due to invalid IL or missing references)
			//IL_017d: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
			string text = string.Empty;
			int num = ((!IsSubclassOfRawGeneric(typeof(Building_Base<>), def.thingClass)) ? 1 : 10);
			string text2 = ((((FloatRange)(ref floatrange_SpeedFactor)).Span > 0f) ? $"{floatrange_SpeedFactor.min * (float)num} - {floatrange_SpeedFactor.max * (float)num}" : $"{floatrange_SpeedFactor.min * (float)num}");
			if (text2 != "1")
			{
				text = TaggedString.op_Implicit(text + TranslatorFormattedStringExtensions.Translate("PRF_UTD_CompProperties_PowerWorkSetting_Speed", NamedArgument.op_Implicit(text2)));
				text += "\r\n";
			}
			text2 = ((((FloatRange)(ref floatrange_Range)).Span > 0f) ? $"{floatrange_Range.min} - {floatrange_Range.max}" : $"{floatrange_Range.min}");
			if (text2 != "0" && text2 != "1")
			{
				text = TaggedString.op_Implicit(text + TranslatorFormattedStringExtensions.Translate("PRF_UTD_CompProperties_PowerWorkSetting_Range", NamedArgument.op_Implicit(text2)));
				text += "\r\n";
				text = TaggedString.op_Implicit(text + TranslatorFormattedStringExtensions.Translate("PRF_UTD_CompProperties_PowerWorkSetting_RangeType", NamedArgument.op_Implicit(propsRangeType.ToText())));
				text += "\r\n";
			}
			if (allowManualRangeTypeChange)
			{
				text = TaggedString.op_Implicit(text + (Translator.Translate("PRF_UTD_CompProperties_PowerWorkSetting_RangeTypeChange") + "\r\n"));
			}
			return text;
		}
	}
	public interface IRangeCells
	{
		bool NeedsRotate { get; }

		IEnumerable<IntVec3> RangeCells(IntVec3 center, Rot4 rot, ThingDef thingDef, float range);

		string ToText();
	}
	public class CircleRange : IRangeCells
	{
		public bool NeedsRotate => false;

		public IEnumerable<IntVec3> RangeCells(IntVec3 center, Rot4 rot, ThingDef thingDef, float range)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return GenRadial.RadialCellsAround(center, range + (float)Mathf.Max(thingDef.size.x, thingDef.size.z) - 1f, true);
		}

		public string ToText()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("PRF_SettingsTab_RangeType_CircleRange"));
		}
	}
	public class FacingRectRange : IRangeCells
	{
		public bool NeedsRotate => true;

		public IEnumerable<IntVec3> RangeCells(IntVec3 center, Rot4 rot, ThingDef thingDef, float range)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			return Util.FacingRect(center, thingDef.size, rot, Mathf.RoundToInt(range));
		}

		public string ToText()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("PRF_SettingsTab_RangeType_FacingRectRange"));
		}
	}
	public class RectRange : IRangeCells
	{
		public bool NeedsRotate => false;

		public IEnumerable<IntVec3> RangeCells(IntVec3 center, Rot4 rot, ThingDef thingDef, float range)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			IntVec2 size = thingDef.size;
			Util.CounterAdjustForRotation(ref center, ref size, rot);
			HashSet<IntVec3> under = GenAdj.CellsOccupiedBy(center, rot, size).ToHashSet();
			return from c in GenAdj.CellsOccupiedBy(center, rot, thingDef.size + new IntVec2(Mathf.RoundToInt(range) * 2, Mathf.RoundToInt(range) * 2))
				where !under.Contains(c)
				select c;
		}

		public string ToText()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("PRF_SettingsTab_RangeType_RectRange"));
		}
	}
	[StaticConstructorOnStartup]
	public class CompPRFHelp : ThingComp
	{
		private static readonly Texture2D LaunchReportTex = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport", true);

		public string HelpText
		{
			get
			{
				//IL_002b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				TaggedString val = default(TaggedString);
				return TaggedString.op_Implicit(Translator.TryTranslate(((Def)((Thing)base.parent).def).defName + "_HelpText", ref val) ? val : TaggedString.op_Implicit((string)null));
			}
		}

		public string OrdoText
		{
			get
			{
				//IL_002b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				TaggedString val = default(TaggedString);
				return TaggedString.op_Implicit(Translator.TryTranslate(((Def)((Thing)base.parent).def).defName + "_OrdoText", ref val) ? val : TaggedString.op_Implicit((string)null));
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			string helpText = HelpText;
			if (!string.IsNullOrEmpty(helpText))
			{
				yield return (Gizmo)new Command_Action
				{
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRFHelp")),
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRFHelpDesc")),
					icon = (Texture)(object)LaunchReportTex,
					action = delegate
					{
						//IL_001e: Unknown result type (might be due to invalid IL or missing references)
						//IL_002c: Unknown result type (might be due to invalid IL or missing references)
						//IL_0036: Expected O, but got Unknown
						if (Find.WindowStack.WindowOfType<Dialog_MessageBox>() == null)
						{
							Find.WindowStack.Add((Window)new Dialog_MessageBox(TaggedString.op_Implicit(helpText), (string)null, (Action)null, (string)null, (Action)null, (string)null, false, (Action)null, (Action)null, (WindowLayer)1));
						}
					}
				};
			}
			ResearchProjectDef pRFOrdoDataRummaging = PRFDefOf.PRFOrdoDataRummaging;
			if (pRFOrdoDataRummaging == null || !pRFOrdoDataRummaging.IsFinished)
			{
				yield break;
			}
			string ordoText = OrdoText;
			if (string.IsNullOrEmpty(ordoText))
			{
				yield break;
			}
			yield return (Gizmo)new Command_Action
			{
				defaultLabel = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFViewOrdo", NamedArgument.op_Implicit(((Thing)base.parent).LabelCapNoCount))),
				icon = (Texture)(object)LaunchReportTex,
				action = delegate
				{
					//IL_001e: Unknown result type (might be due to invalid IL or missing references)
					//IL_002c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0036: Expected O, but got Unknown
					if (Find.WindowStack.WindowOfType<Dialog_MessageBox>() == null)
					{
						Find.WindowStack.Add((Window)new Dialog_MessageBox(TaggedString.op_Implicit(ordoText), (string)null, (Action)null, (string)null, (Action)null, (string)null, false, (Action)null, (Action)null, (WindowLayer)1));
					}
				}
			};
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((ThingComp)this).CompGetGizmosExtra();
		}
	}
	internal class CompSecondLayer : ThingComp
	{
		private Graphic graphicInt;

		public CompProperties_SecondLayer Props => (CompProperties_SecondLayer)(object)base.props;

		protected virtual Graphic Graphic
		{
			get
			{
				if (graphicInt != null)
				{
					return graphicInt;
				}
				if (Props.graphicData == null)
				{
					Log.ErrorOnce(((object)((Thing)base.parent).def)?.ToString() + " has no SecondLayer graphicData but we are trying to access it.", 764532);
					return BaseContent.BadGraphic;
				}
				graphicInt = Props.graphicData.GraphicColoredFor((Thing)(object)base.parent);
				return graphicInt;
			}
		}

		public override void PostDraw()
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostDraw();
			Graphic.Draw(GenThing.TrueCenter(((Thing)base.parent).Position, ((Thing)base.parent).Rotation, ((Thing)base.parent).def.size, Props.Altitude), ((Thing)base.parent).Rotation, (Thing)(object)base.parent, 0f);
		}
	}
	internal class CompProperties_SecondLayer : CompProperties
	{
		public GraphicData graphicData = null;

		public AltitudeLayer altitudeLayer = (AltitudeLayer)28;

		public float Altitude => Altitudes.AltitudeFor(altitudeLayer);

		public CompProperties_SecondLayer()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			base.compClass = typeof(CompSecondLayer);
		}
	}
	public static class ConditionalPatchHelper
	{
		public class TogglePatch
		{
			private readonly MethodInfo baseMethodInfo;

			private readonly HarmonyMethod transpilerHarmonyMethod;

			private readonly HarmonyMethod prefixHarmonyMethod;

			private readonly HarmonyMethod postfixHarmonyMethod;

			private readonly MethodInfo transpilerMethodInfo;

			private readonly MethodInfo prefixMethodInfo;

			private readonly MethodInfo postfixMethodInfo;

			public bool Status { get; private set; }

			public TogglePatch(MethodInfo baseMethod, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null)
			{
				//IL_001e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0028: Expected O, but got Unknown
				//IL_0035: Unknown result type (might be due to invalid IL or missing references)
				//IL_003f: Expected O, but got Unknown
				//IL_004c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0056: Expected O, but got Unknown
				baseMethodInfo = baseMethod;
				if (transpiler != null)
				{
					transpilerHarmonyMethod = new HarmonyMethod(transpiler);
				}
				if (prefix != null)
				{
					prefixHarmonyMethod = new HarmonyMethod(prefix);
				}
				if (postfix != null)
				{
					postfixHarmonyMethod = new HarmonyMethod(postfix);
				}
				transpilerMethodInfo = transpiler;
				prefixMethodInfo = prefix;
				postfixMethodInfo = postfix;
			}

			public void PatchHandler(bool patch)
			{
				if (patch && !Status)
				{
					harmonyInstance.Patch((MethodBase)baseMethodInfo, prefixHarmonyMethod, postfixHarmonyMethod, transpilerHarmonyMethod, (HarmonyMethod)null);
					Status = true;
				}
				else if (Status && !patch)
				{
					if (transpilerMethodInfo != null)
					{
						harmonyInstance.Unpatch((MethodBase)baseMethodInfo, transpilerMethodInfo);
					}
					if (prefixMethodInfo != null)
					{
						harmonyInstance.Unpatch((MethodBase)baseMethodInfo, prefixMethodInfo);
					}
					if (postfixMethodInfo != null)
					{
						harmonyInstance.Unpatch((MethodBase)baseMethodInfo, postfixMethodInfo);
					}
					Status = false;
				}
			}
		}

		private static Harmony harmonyInstance;

		public static readonly TogglePatch PatchReachabilityCanReach = new TogglePatch(AccessTools.Method(typeof(Reachability), "CanReach", new Type[4]
		{
			typeof(IntVec3),
			typeof(LocalTargetInfo),
			typeof(PathEndMode),
			typeof(TraverseParms)
		}, (Type[])null), null, AccessTools.Method(typeof(Patch_Reachability_CanReach), "Postfix", (Type[])null, (Type[])null));

		private static readonly TogglePatch PatchMinifiedThingPrint = new TogglePatch(AccessTools.Method(typeof(MinifiedThing), "Print", new Type[1] { typeof(SectionLayer) }, (Type[])null), AccessTools.Method(typeof(Patch_MinifiedThing_Print), "Prefix", (Type[])null, (Type[])null));

		private static readonly TogglePatch PatchThingPrint = new TogglePatch(AccessTools.Method(typeof(Thing), "Print", new Type[1] { typeof(SectionLayer) }, (Type[])null), AccessTools.Method(typeof(Patch_Thing_Print), "Prefix", (Type[])null, (Type[])null));

		private static readonly TogglePatch PatchThingWithCompsDrawGUIOverlay = new TogglePatch(AccessTools.Method(typeof(ThingWithComps), "DrawGUIOverlay", (Type[])null, (Type[])null), AccessTools.Method(typeof(Patch_ThingWithComps_DrawGUIOverlay), "Prefix", (Type[])null, (Type[])null));

		private static readonly TogglePatch PatchThingDrawGUIOverlay = new TogglePatch(AccessTools.Method(typeof(Thing), "DrawGUIOverlay", (Type[])null, (Type[])null), AccessTools.Method(typeof(Patch_Thing_DrawGUIOverlay), "Prefix", (Type[])null, (Type[])null));

		private static readonly TogglePatch PatchFloatMenuMakerMapChoicesAtFor = new TogglePatch(AccessTools.Method(typeof(FloatMenuMakerMap), "GetOptions", (Type[])null, (Type[])null), AccessTools.Method(typeof(Patch_FloatMenuMakerMap_GetOptions), "Prefix", (Type[])null, (Type[])null));

		private static readonly TogglePatch PatchBuildingStorageAccepts = new TogglePatch(AccessTools.Method(typeof(Building_Storage), "Accepts", new Type[1] { typeof(Thing) }, (Type[])null), AccessTools.Method(typeof(Patch_Building_Storage_Accepts), "Prefix", (Type[])null, (Type[])null));

		private static readonly TogglePatch PatchStorageSettingsAllowedToAccept = new TogglePatch(AccessTools.Method(typeof(StorageSettings), "AllowedToAccept", new Type[1] { typeof(Thing) }, (Type[])null), AccessTools.Method(typeof(Patch_StorageSettings_AllowedToAccept), "Prefix", (Type[])null, (Type[])null));

		private static readonly TogglePatch PatchForbidUtilityIsForbidden = new TogglePatch(AccessTools.Method(typeof(ForbidUtility), "IsForbidden", new Type[2]
		{
			typeof(Thing),
			typeof(Pawn)
		}, (Type[])null), AccessTools.Method(typeof(Patch_ForbidUtility_IsForbidden), "Prefix", (Type[])null, (Type[])null));

		private static readonly List<Building_MassStorageUnit> BuildingMassStorages = new List<Building_MassStorageUnit>();

		public static void InitHarmony(Harmony harmony)
		{
			harmonyInstance = harmony;
		}

		private static void UpdatePatchStorage()
		{
			bool patch = BuildingMassStorages.Count > 0;
			PatchMinifiedThingPrint.PatchHandler(patch);
			PatchThingPrint.PatchHandler(patch);
			PatchThingWithCompsDrawGUIOverlay.PatchHandler(patch);
			PatchThingDrawGUIOverlay.PatchHandler(patch);
			PatchFloatMenuMakerMapChoicesAtFor.PatchHandler(patch);
			PatchBuildingStorageAccepts.PatchHandler(patch);
			PatchForbidUtilityIsForbidden.PatchHandler(patch);
			PatchStorageSettingsAllowedToAccept.PatchHandler(patch);
		}

		public static void Register(Building_MassStorageUnit building)
		{
			BuildingMassStorages.Add(building);
			UpdatePatchStorage();
		}

		public static void Deregister(Building_MassStorageUnit building)
		{
			BuildingMassStorages.Remove(building);
			UpdatePatchStorage();
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
	public class DefChangeTracker
	{
		private static readonly Dictionary<string, object> DefaultDefValues = new Dictionary<string, object>();

		public void AddDefaultValue(string defName, string keylet, object defaultValue)
		{
			DefaultDefValues[defName + "_" + keylet] = defaultValue;
		}

		public T GetDefaultValue<T>(string defName, string keylet) where T : class
		{
			return GetDefaultValue<T>(defName, keylet, null);
		}

		public T GetDefaultValue<T>(string defName, string keylet, T defaultValue)
		{
			if (DefaultDefValues.ContainsKey(defName + "_" + keylet))
			{
				return (T)DefaultDefValues[defName + "_" + keylet];
			}
			return defaultValue;
		}

		public IEnumerable<T> GetAllWithKeylet<T>(string keylet)
		{
			foreach (KeyValuePair<string, object> entry in DefaultDefValues)
			{
				string[] t = entry.Key.Split('_', StringSplitOptions.None);
				if (t[t.Length - 1] == keylet)
				{
					yield return (T)entry.Value;
				}
			}
		}
	}
	public static class GatherThingsUtility
	{
		public static IEnumerable<IntVec3> InputCells(this Building building)
		{
			return ((ThingWithComps)building).GetComp<CompPowerWorkSetting>()?.GetRangeCells() ?? GenAdj.CellsAdjacent8Way((Thing)(object)building);
		}

		public static IEnumerable<Thing> AllThingsInCellForUse(this IntVec3 c, Map map, bool allowStorageZones = true, bool allowUGEntranceConnector = false, bool allowUGBelts = false)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			if (!GenGrid.InBounds(c, map))
			{
				yield break;
			}
			List<Thing> thingList = map.thingGrid.ThingsListAt(c);
			ThingOwner<Thing> owner = default(ThingOwner<Thing>);
			for (int i = thingList.Count - 1; i >= 0; i--)
			{
				Thing t = thingList[i];
				Building val = (Building)(object)((t is Building) ? t : null);
				int num;
				if (val != null)
				{
					IThingHolder holder = (IThingHolder)(object)((val is IThingHolder) ? val : null);
					if (holder != null && !(holder is Frame))
					{
						ThingOwner directlyHeldThings = holder.GetDirectlyHeldThings();
						owner = directlyHeldThings as ThingOwner<Thing>;
						num = ((owner != null) ? 1 : 0);
						goto IL_010c;
					}
				}
				num = 0;
				goto IL_010c;
				IL_010c:
				if (num != 0)
				{
					Thing val2 = t;
					if (val2 is Building_BeltConveyor building_BeltConveyor && ((building_BeltConveyor.IsUnderground && !allowUGBelts) || (val2 is Building_BeltConveyorUGConnector { ToUnderground: not false } && !allowUGEntranceConnector)))
					{
						continue;
					}
					for (int j = owner.InnerListForReading.Count - 1; j >= 0; j--)
					{
						yield return owner.InnerListForReading[j];
					}
				}
				else if ((int)t.def.category == 2)
				{
					yield return t;
				}
				else
				{
					Building_Storage storage = (Building_Storage)(object)((t is Building_Storage) ? t : null);
					if (storage != null)
					{
						foreach (Thing heldThing in storage.GetSlotGroup().HeldThings)
						{
							yield return heldThing;
						}
					}
				}
				owner = null;
			}
			Zone_Stockpile sz = default(Zone_Stockpile);
			int num2;
			if (allowStorageZones)
			{
				Zone zone = GridsUtility.GetZone(c, map);
				sz = (Zone_Stockpile)(object)((zone is Zone_Stockpile) ? zone : null);
				num2 = ((sz != null) ? 1 : 0);
			}
			else
			{
				num2 = 0;
			}
			if (num2 == 0)
			{
				yield break;
			}
			foreach (Thing item in ((Zone)sz).AllContainedThings.Where((Thing val3) => (int)val3.def.category == 2))
			{
				yield return item;
			}
		}
	}
	public static class GridsUtilityExt
	{
		public static T GetFirst<T>(this IntVec3 c, Map map) where T : class
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
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
	internal interface ILimitWatcher
	{
		bool ItemIsLimit(ThingDef thing, bool CntStacks, int limit);
	}
	internal interface IPickupSettings
	{
		bool AllowGroundPickup { get; set; }

		bool AllowStockpilePickup { get; set; }

		bool AllowStoragePickup { get; set; }

		bool AllowForbiddenPickup { get; set; }

		bool AllowBeltPickup { get; set; }
	}
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
	public interface IPRF_SettingsContent
	{
		float ITab_Settings_Minimum_x { get; }

		float ITab_Settings_Additional_y { get; }

		Listing_Standard ITab_Settings_AppendContent(Listing_Standard list, Rect parrent_rect);
	}
	public interface IPRF_SettingsContentLink
	{
		IPRF_SettingsContent PRF_SettingsContentOb { get; }
	}
	internal interface IProductionSettingsUser
	{
		PRFBSetting SettingsOptions { get; }

		bool ObeysStorageFilters { get; set; }

		bool OutputToEntireStockpile { get; set; }
	}
	internal class ITab_Filter : ITab_Storage
	{
		public ITab_Filter()
		{
			((InspectTabBase)this).labelKey = "Filter";
		}
	}
	public interface IPowerSupplyMachineHolder
	{
		IPowerSupplyMachine RangePowerSupplyMachine { get; }
	}
	public interface IAdditionalPowerConsumption
	{
		Dictionary<string, int> AdditionalPowerConsumption { get; }
	}
	public interface IPowerSupplyMachine
	{
		float BasePowerConsumption { get; }

		int CurrentPowerConsumption { get; }

		Dictionary<string, int> AdditionalPowerConsumption { get; }

		int MaxPowerForSpeed { get; }

		int MaxPowerForRange { get; }

		float PowerPerStepSpeed { get; }

		float PowerPerStepRange { get; }

		FloatRange FloatRangeRange { get; }

		float CurrentRange { get; }

		FloatRange FloatRangeSpeedFactor { get; }

		float CurrentSpeedFactor { get; }

		float SupplyPowerForSpeed { get; set; }

		float SupplyPowerForRange { get; set; }

		bool Glowable { get; }

		bool Glow { get; set; }

		bool SpeedSetting { get; }

		bool RangeSetting { get; }

		void RefreshPowerStatus();
	}
	internal class ITab_PowerSupply : ITab
	{
		private static readonly Vector2 WinSize = new Vector2(600f, 130f);

		private const float HeightSpeed = 95f;

		private const float HeightRange = 75f;

		private const float HeightGlow = 30f;

		private readonly string descriptionForSpeed;

		private readonly string descriptionForRange;

		private IPowerSupplyMachine Machine => (((ITab)this).SelThing as IPowerSupplyMachineHolder)?.RangePowerSupplyMachine;

		public override bool IsVisible => Machine != null && (Machine.SpeedSetting || Machine.RangeSetting);

		public ITab_PowerSupply()
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).size = WinSize;
			((InspectTabBase)this).labelKey = "PRF.AutoMachineTool.SupplyPower.TabName";
			descriptionForSpeed = TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.SupplyPower.Description"));
			descriptionForRange = TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.SupplyPower.DescriptionForRange"));
		}

		public override void TabUpdate()
		{
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).TabUpdate();
			float num = (Machine.SpeedSetting ? 95f : 0f) + (Machine.RangeSetting ? 75f : 0f) + (Machine.Glowable ? 30f : 0f);
			((InspectTabBase)this).size = new Vector2(WinSize.x, WinSize.y + num);
			((InspectTabBase)this).UpdateSize();
		}

		protected override void FillTab()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Expected O, but got Unknown
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_0147: Unknown result type (might be due to invalid IL or missing references)
			//IL_014c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0151: Unknown result type (might be due to invalid IL or missing references)
			//IL_015d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0172: Unknown result type (might be due to invalid IL or missing references)
			//IL_0177: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_0214: Unknown result type (might be due to invalid IL or missing references)
			//IL_0219: Unknown result type (might be due to invalid IL or missing references)
			//IL_021b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0238: Unknown result type (might be due to invalid IL or missing references)
			//IL_023d: Unknown result type (might be due to invalid IL or missing references)
			//IL_024e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0253: Unknown result type (might be due to invalid IL or missing references)
			//IL_028a: Unknown result type (might be due to invalid IL or missing references)
			//IL_028f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0291: Unknown result type (might be due to invalid IL or missing references)
			//IL_0296: Unknown result type (might be due to invalid IL or missing references)
			//IL_0297: Unknown result type (might be due to invalid IL or missing references)
			//IL_029c: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02de: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_030d: Unknown result type (might be due to invalid IL or missing references)
			//IL_031e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0323: Unknown result type (might be due to invalid IL or missing references)
			//IL_0335: Unknown result type (might be due to invalid IL or missing references)
			//IL_034d: Unknown result type (might be due to invalid IL or missing references)
			//IL_035e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0363: Unknown result type (might be due to invalid IL or missing references)
			//IL_036e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0375: Unknown result type (might be due to invalid IL or missing references)
			//IL_0401: Unknown result type (might be due to invalid IL or missing references)
			//IL_0406: Unknown result type (might be due to invalid IL or missing references)
			//IL_041d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0422: Unknown result type (might be due to invalid IL or missing references)
			//IL_0424: Unknown result type (might be due to invalid IL or missing references)
			//IL_0449: Unknown result type (might be due to invalid IL or missing references)
			//IL_044e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0450: Unknown result type (might be due to invalid IL or missing references)
			//IL_046d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0472: Unknown result type (might be due to invalid IL or missing references)
			//IL_0483: Unknown result type (might be due to invalid IL or missing references)
			//IL_0488: Unknown result type (might be due to invalid IL or missing references)
			//IL_04bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_04cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_04de: Unknown result type (might be due to invalid IL or missing references)
			//IL_04eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_04fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_050c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0519: Unknown result type (might be due to invalid IL or missing references)
			//IL_0523: Unknown result type (might be due to invalid IL or missing references)
			//IL_0528: Unknown result type (might be due to invalid IL or missing references)
			//IL_053a: Unknown result type (might be due to invalid IL or missing references)
			//IL_054c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0551: Unknown result type (might be due to invalid IL or missing references)
			//IL_055c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0563: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0594: Unknown result type (might be due to invalid IL or missing references)
			//IL_0599: Unknown result type (might be due to invalid IL or missing references)
			//IL_05a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_05af: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Unknown result type (might be due to invalid IL or missing references)
			//IL_0106: Unknown result type (might be due to invalid IL or missing references)
			//IL_010b: Unknown result type (might be due to invalid IL or missing references)
			Listing_Standard val = new Listing_Standard();
			Rect val2 = GenUI.ContractedBy(new Rect(0f, 0f, ((InspectTabBase)this).size.x, ((InspectTabBase)this).size.y), 10f);
			((Listing)val).Begin(val2);
			((Listing)val).Gap(12f);
			Rect rect = ((Listing)val).GetRect(50f, 1f);
			string text = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.BreakDownLine_Start", NamedArgument.op_Implicit(Machine.BasePowerConsumption), NamedArgument.op_Implicit(Machine.SupplyPowerForSpeed), NamedArgument.op_Implicit(Machine.SupplyPowerForRange)));
			if (Machine.AdditionalPowerConsumption != null && Machine.AdditionalPowerConsumption.Count > 0)
			{
				foreach (KeyValuePair<string, int> item in Machine.AdditionalPowerConsumption)
				{
					text = TaggedString.op_Implicit(text + TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.BreakDownLine_Append", NamedArgument.op_Implicit(item.Key), NamedArgument.op_Implicit(item.Value)));
				}
			}
			text = TaggedString.op_Implicit(text + TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.BreakDownLine_End", NamedArgument.op_Implicit(-1 * Machine.CurrentPowerConsumption)));
			Widgets.Label(rect, text);
			rect = ((Listing)val).GetRect(10f, 1f);
			Widgets.DrawLineHorizontal(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, WinSize.x);
			if (Machine.SpeedSetting)
			{
				int num = 0;
				int maxPowerForSpeed = Machine.MaxPowerForSpeed;
				string text2 = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.ValueLabelForSpeed", NamedArgument.op_Implicit(Machine.SupplyPowerForSpeed)));
				rect = ((Listing)val).GetRect(30f, 1f);
				Widgets.Label(rect, descriptionForSpeed);
				((Listing)val).Gap(12f);
				rect = ((Listing)val).GetRect(20f, 1f);
				int num2 = (int)Widgets.HorizontalSlider(rect, Machine.SupplyPowerForSpeed, (float)num, (float)maxPowerForSpeed, true, text2, TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.wdLabel", NamedArgument.op_Implicit(num))), TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.wdLabel", NamedArgument.op_Implicit(maxPowerForSpeed))), Machine.PowerPerStepSpeed);
				Machine.SupplyPowerForSpeed = num2;
				rect = ((Listing)val).GetRect(30f, 1f);
				TextAnchor anchor = Text.Anchor;
				GameFont font = Text.Font;
				Text.Font = (GameFont)0;
				Text.Anchor = (TextAnchor)0;
				Widgets.Label(rect, TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.PercentLabel", NamedArgument.op_Implicit(Machine.FloatRangeSpeedFactor.min / Machine.FloatRangeSpeedFactor.min * 100f)));
				Text.Anchor = (TextAnchor)2;
				Widgets.Label(rect, TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.PercentLabel", NamedArgument.op_Implicit(Machine.FloatRangeSpeedFactor.max / Machine.FloatRangeSpeedFactor.min * 100f)));
				Text.Anchor = (TextAnchor)1;
				Widgets.Label(rect, TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.CurrentPercent", NamedArgument.op_Implicit(Machine.CurrentSpeedFactor / Machine.FloatRangeSpeedFactor.min * 100f)));
				Text.Anchor = anchor;
				Text.Font = font;
				((Listing)val).Gap(12f);
				if (Machine.RangeSetting)
				{
					rect = ((Listing)val).GetRect(10f, 1f);
					Widgets.DrawLineHorizontal(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, WinSize.x);
				}
			}
			if (Machine.RangeSetting)
			{
				int num3 = 0;
				int maxPowerForRange = Machine.MaxPowerForRange;
				string text3 = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.ValueLabelForRange", NamedArgument.op_Implicit(Machine.SupplyPowerForRange)));
				rect = ((Listing)val).GetRect(30f, 1f);
				Widgets.Label(rect, descriptionForRange);
				((Listing)val).Gap(12f);
				rect = ((Listing)val).GetRect(20f, 1f);
				float supplyPowerForRange = Widgets.HorizontalSlider(rect, Machine.SupplyPowerForRange, (float)num3, (float)maxPowerForRange, true, text3, TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.wdLabel", NamedArgument.op_Implicit(num3))), TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.wdLabel", NamedArgument.op_Implicit(maxPowerForRange))), Machine.PowerPerStepRange);
				Machine.SupplyPowerForRange = supplyPowerForRange;
				rect = ((Listing)val).GetRect(30f, 1f);
				TextAnchor anchor = Text.Anchor;
				GameFont font = Text.Font;
				Text.Font = (GameFont)0;
				Text.Anchor = (TextAnchor)0;
				Widgets.Label(rect, TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.CellsLabel", NamedArgument.op_Implicit(Machine.FloatRangeRange.min)));
				Text.Anchor = (TextAnchor)2;
				Widgets.Label(rect, TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.CellsLabel", NamedArgument.op_Implicit(Machine.FloatRangeRange.max)));
				Text.Anchor = (TextAnchor)1;
				Widgets.Label(rect, TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.SupplyPower.CurrentCellRadius", NamedArgument.op_Implicit(Machine.CurrentRange)));
				Text.Anchor = anchor;
				Text.Font = font;
				((Listing)val).Gap(12f);
			}
			if (Machine.Glowable)
			{
				rect = ((Listing)val).GetRect(30f, 1f);
				bool glow = Machine.Glow;
				Widgets.CheckboxLabeled(rect, TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.SupplyPower.SunLampText")), ref glow, false, (Texture2D)null, (Texture2D)null, false, false);
				Machine.Glow = glow;
			}
			((Listing)val).Gap(12f);
			((Listing)val).End();
		}
	}
	internal class ITab_ProductionSettings : ITab
	{
		private static readonly List<Func<Thing, bool>> ShowITabTests = new List<Func<Thing, bool>>();

		private static readonly List<Func<Thing, float>> ExtraHeightRequests = new List<Func<Thing, float>>();

		private static readonly List<Action<Thing, Listing_Standard>> WindowContentDrawers = new List<Action<Thing, Listing_Standard>>();

		private Vector2 winSize = new Vector2(400f, 0f);

		private List<SlotGroup> groups;

		private static readonly TipSignal RotInputRangeTip = new TipSignal(Translator.Translate("PRF_SettingsITab_TipSignal_RotInputRange"));

		private IProductionSettingsUser ProductionSettingsUser => ((ITab)this).SelThing as IProductionSettingsUser;

		private IPRF_SettingsContentLink PRfSettingsContent => ((ITab)this).SelThing as IPRF_SettingsContentLink;

		private IPRF_Building PRfBuilding => ((ITab)this).SelThing as IPRF_Building;

		private CompPowerWorkSetting CompPropertiesPowerWork
		{
			get
			{
				Thing selThing = ((ITab)this).SelThing;
				Thing obj = ((selThing is ThingWithComps) ? selThing : null);
				return (obj != null) ? ((ThingWithComps)obj).GetComp<CompPowerWorkSetting>() : null;
			}
		}

		private IProductLimitation Machine => ((ITab)this).SelThing as IProductLimitation;

		private IPowerSupplyMachineHolder SupplyMachineHolder => ((ITab)this).SelThing as IPowerSupplyMachineHolder;

		public override bool IsVisible => GenCollection.FirstOrDefault<Func<Thing, bool>>(ShowITabTests, (Predicate<Func<Thing, bool>>)((Func<Thing, bool> t) => t?.Invoke(((ITab)this).SelThing) ?? false)) != null || ShowProductLimit || ShowOutputToEntireStockpile || ShowObeysStorageFilter || ShowAdditionalSettings || ShowAreaSelectButton || ShowForbidOnPlacingSetting;

		private bool ShowProductLimit
		{
			get
			{
				IProductLimitation machine = Machine;
				return machine != null && !machine.ProductLimitationDisable;
			}
		}

		private bool ShowOutputToEntireStockpile => ProductionSettingsUser != null && (ProductionSettingsUser.SettingsOptions & PRFBSetting.optionOutputToEntireStockpie) > (PRFBSetting)0 && (!(ProductionSettingsUser is IBeltConveyorLinkable beltConveyorLinkable) || beltConveyorLinkable.CanSendToLevel(ConveyorLevel.Ground));

		private bool ShowObeysStorageFilter => ProductionSettingsUser != null && (ProductionSettingsUser.SettingsOptions & PRFBSetting.optionObeysStorageFilters) > (PRFBSetting)0 && (!(ProductionSettingsUser is IBeltConveyorLinkable beltConveyorLinkable) || beltConveyorLinkable.CanSendToLevel(ConveyorLevel.Ground));

		private bool ShowForbidOnPlacingSetting => PRfBuilding != null;

		private bool ShowRangeTypeSelectorButton => ShowAreaSelectButton && CompPropertiesPowerWork != null && CompPropertiesPowerWork.Props.allowManualRangeTypeChange;

		private bool ShowAdditionalSettings => PRfSettingsContent != null;

		private bool ShowAreaSelectButton => SupplyMachineHolder != null;

		public static void RegisterSetting(Func<Thing, bool> showResultTest, Func<Thing, float> extraHeightRequest, Action<Thing, Listing_Standard> windowContents)
		{
			ShowITabTests.Add(showResultTest);
			ExtraHeightRequests.Add(extraHeightRequest);
			WindowContentDrawers.Add(windowContents);
		}

		public ITab_ProductionSettings()
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).labelKey = "PRFSettingsTab";
		}

		protected override void UpdateSize()
		{
			//IL_0210: Unknown result type (might be due to invalid IL or missing references)
			//IL_0215: Unknown result type (might be due to invalid IL or missing references)
			winSize.y = 34f;
			winSize.x = 400f;
			if (ShowOutputToEntireStockpile || ShowObeysStorageFilter || ShowForbidOnPlacingSetting)
			{
				winSize.y += 36f;
			}
			if (ShowOutputToEntireStockpile)
			{
				winSize.y += 24f;
			}
			if (ShowObeysStorageFilter)
			{
				winSize.y += 24f;
			}
			if (ShowForbidOnPlacingSetting)
			{
				winSize.y += 24f;
			}
			for (int i = 0; i < ShowITabTests.Count; i++)
			{
				Func<Thing, bool> func = ShowITabTests[i];
				if (func != null && func(((ITab)this).SelThing))
				{
					winSize.y += ExtraHeightRequests[i]?.Invoke(((ITab)this).SelThing) ?? 0f;
				}
			}
			if (ShowProductLimit)
			{
				winSize.y += 200f;
			}
			if (PRfSettingsContent != null)
			{
				winSize.y += PRfSettingsContent.PRF_SettingsContentOb.ITab_Settings_Additional_y;
				winSize.x = Mathf.Max(winSize.x, PRfSettingsContent.PRF_SettingsContentOb.ITab_Settings_Minimum_x);
			}
			if (ShowRangeTypeSelectorButton)
			{
				winSize.y += 30f;
			}
			float num = 900f;
			float num2 = 70f;
			float num3 = 268f;
			if ((float)UI.screenHeight > num2 - num3)
			{
				num = (float)UI.screenHeight - num3;
			}
			winSize.y = Mathf.Clamp(winSize.y, num2, num);
			((InspectTabBase)this).size = winSize;
			((InspectTabBase)this).UpdateSize();
		}

		public override void OnOpen()
		{
			((InspectTabBase)this).OnOpen();
			if (Machine != null)
			{
				groups = Find.CurrentMap.haulDestinationManager.AllGroups.ToList();
			}
		}

		private static string GetSlotGroupName(ISlotGroupParent slotGroup)
		{
			string text = string.Empty;
			IStorageGroupMember val = (IStorageGroupMember)(object)((slotGroup is IStorageGroupMember) ? slotGroup : null);
			if (val != null && val.Group != null && val.Group.RenamableLabel != string.Empty)
			{
				text = " (" + val.Group.RenamableLabel + ")";
			}
			IRenameable val2 = (IRenameable)(object)((slotGroup is IRenameable) ? slotGroup : null);
			if (val2 != null)
			{
				return val2.RenamableLabel + text;
			}
			return slotGroup.SlotYielderLabel() + text;
		}

		protected override void FillTab()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Expected O, but got Unknown
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_011f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0130: Unknown result type (might be due to invalid IL or missing references)
			//IL_0191: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0594: Unknown result type (might be due to invalid IL or missing references)
			//IL_02af: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02df: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0304: Unknown result type (might be due to invalid IL or missing references)
			//IL_030f: Unknown result type (might be due to invalid IL or missing references)
			//IL_032f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0338: Unknown result type (might be due to invalid IL or missing references)
			//IL_0374: Unknown result type (might be due to invalid IL or missing references)
			//IL_0379: Unknown result type (might be due to invalid IL or missing references)
			//IL_05c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_05c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_05c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_05ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_05d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_05df: Unknown result type (might be due to invalid IL or missing references)
			//IL_05e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_03d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_03d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_03dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_041a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0423: Unknown result type (might be due to invalid IL or missing references)
			//IL_045f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0464: Unknown result type (might be due to invalid IL or missing references)
			//IL_03af: Unknown result type (might be due to invalid IL or missing references)
			//IL_0634: Unknown result type (might be due to invalid IL or missing references)
			//IL_063e: Expected O, but got Unknown
			//IL_049e: Unknown result type (might be due to invalid IL or missing references)
			//IL_04a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_04aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_04b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_04b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0477: Unknown result type (might be due to invalid IL or missing references)
			//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_03c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_03ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0657: Unknown result type (might be due to invalid IL or missing references)
			//IL_0658: Unknown result type (might be due to invalid IL or missing references)
			//IL_0676: Unknown result type (might be due to invalid IL or missing references)
			//IL_0677: Unknown result type (might be due to invalid IL or missing references)
			//IL_067c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0699: Unknown result type (might be due to invalid IL or missing references)
			//IL_048e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0490: Unknown result type (might be due to invalid IL or missing references)
			//IL_0492: Unknown result type (might be due to invalid IL or missing references)
			//IL_0485: Unknown result type (might be due to invalid IL or missing references)
			//IL_06c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_06c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_04e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0531: Unknown result type (might be due to invalid IL or missing references)
			//IL_0553: Unknown result type (might be due to invalid IL or missing references)
			//IL_055d: Expected O, but got Unknown
			//IL_055d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0567: Expected O, but got Unknown
			Listing_Standard val = new Listing_Standard();
			Rect val2 = GenUI.ContractedBy(new Rect(0f, 0f, winSize.x, winSize.y), 10f);
			bool flag = false;
			((Listing)val).Begin(val2);
			if (ShowOutputToEntireStockpile || ShowObeysStorageFilter || ShowForbidOnPlacingSetting)
			{
				flag = true;
				val.Label(Translator.Translate("PRF_ITab_ProductionSettings_OutputSettings_Header"), -1f, (string)null);
				((Listing)val).Gap(12f);
			}
			if (ShowOutputToEntireStockpile)
			{
				TaggedString val3 = Translator.Translate("PRF.Common.OutputToStockpileDesc");
				TaggedString val4 = Translator.Translate("PRF.Common.OutputToStockpile");
				bool outputToEntireStockpile = ProductionSettingsUser.OutputToEntireStockpile;
				val.CheckboxLabeled(TaggedString.op_Implicit(val4), ref outputToEntireStockpile, TaggedString.op_Implicit(val3), 0f, 1f);
				if (outputToEntireStockpile != ProductionSettingsUser.OutputToEntireStockpile)
				{
					ProductionSettingsUser.OutputToEntireStockpile = outputToEntireStockpile;
				}
			}
			if (ShowObeysStorageFilter)
			{
				bool obeysStorageFilters = ProductionSettingsUser.ObeysStorageFilters;
				val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("PRF.Common.ObeysStorageFilters")), ref obeysStorageFilters, TaggedString.op_Implicit(Translator.Translate("PRF.Common.ObeysStorageFiltersDesc")), 0f, 1f);
				if (obeysStorageFilters != ProductionSettingsUser.ObeysStorageFilters)
				{
					ProductionSettingsUser.ObeysStorageFilters = obeysStorageFilters;
				}
			}
			if (ShowForbidOnPlacingSetting)
			{
				bool forbidOnPlacingDefault = PRfBuilding.ForbidOnPlacingDefault;
				val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("PRF.Common.ForbidOnPlacingDefault")), ref forbidOnPlacingDefault, TaggedString.op_Implicit(Translator.Translate("PRF.Common.ForbidOnPlacingDefaultDesc")), 0f, 1f);
				if (forbidOnPlacingDefault != PRfBuilding.ForbidOnPlacingDefault)
				{
					PRfBuilding.ForbidOnPlacingDefault = forbidOnPlacingDefault;
				}
			}
			for (int i = 0; i < ShowITabTests.Count; i++)
			{
				Func<Thing, bool> func = ShowITabTests[i];
				if (func != null && func(((ITab)this).SelThing) && WindowContentDrawers[i] != null)
				{
					if (flag)
					{
						((Listing)val).GapLine(12f);
					}
					WindowContentDrawers[i](((ITab)this).SelThing, val);
					flag = true;
				}
			}
			IProductLimitation machine = Machine;
			if (machine != null && !machine.ProductLimitationDisable)
			{
				if (flag)
				{
					((Listing)val).GapLine(12f);
				}
				flag = true;
				TaggedString val5 = Translator.Translate("PRF.AutoMachineTool.ProductLimitation.ValueLabel");
				TaggedString val6 = Translator.Translate("PRF.AutoMachineTool.ProductLimitation.ValueLabelTip");
				TaggedString val7 = Translator.Translate("PRF.AutoMachineTool.ProductLimitation.CheckBoxLabel");
				TaggedString val8 = Translator.Translate("PRF.AutoMachineTool.ProductLimitation.CheckBoxLabelTip");
				TaggedString val9 = Translator.Translate("PRF.AutoMachineTool.ProductLimitation.CountStacks");
				TaggedString val10 = Translator.Translate("PRF.AutoMachineTool.ProductLimitation.CountStacksTip");
				TaggedString val11 = Translator.Translate("PRF.AutoMachineTool.ProductLimitation.SelectAreaTip");
				val.Label(Translator.Translate("PRF_ITab_ProductionSettings_ProductLimitation_Header"), -1f, (string)null);
				((Listing)val).Gap(12f);
				bool productLimitation = Machine.ProductLimitation;
				val.CheckboxLabeled(TaggedString.op_Implicit(val7), ref productLimitation, TaggedString.op_Implicit(val8), 0f, 1f);
				Machine.ProductLimitation = productLimitation;
				((Listing)val).Gap(12f);
				Rect rect = ((Listing)val).GetRect(30f, 1f);
				string text = Machine.ProductLimitCount.ToString();
				int productLimitCount = Machine.ProductLimitCount;
				if (!((TaggedString)(ref val6)).NullOrEmpty())
				{
					if (Mouse.IsOver(rect))
					{
						Widgets.DrawHighlight(rect);
					}
					TooltipHandler.TipRegion(rect, TipSignal.op_Implicit(val6));
				}
				Widgets.Label(GenUI.LeftHalf(rect), val5);
				Widgets.TextFieldNumeric<int>(GenUI.RightHalf(rect), ref productLimitCount, ref text, 1f, 1000000f);
				((Listing)val).Gap(12f);
				bool countStacks = Machine.CountStacks;
				val.CheckboxLabeled(TaggedString.op_Implicit(val9), ref countStacks, TaggedString.op_Implicit(val10), 0f, 1f);
				Machine.CountStacks = countStacks;
				((Listing)val).Gap(12f);
				rect = ((Listing)val).GetRect(30f, 1f);
				if (!((TaggedString)(ref val11)).NullOrEmpty())
				{
					if (Mouse.IsOver(rect))
					{
						Widgets.DrawHighlight(rect);
					}
					TooltipHandler.TipRegion(rect, TipSignal.op_Implicit(val11));
				}
				Widgets.Label(GenUI.LeftHalf(rect), Translator.Translate("PRF.AutoMachineTool.CountZone"));
				Rect val12 = GenUI.RightHalf(rect);
				SlotGroup targetSlotGroup = Machine.TargetSlotGroup;
				object obj;
				if (targetSlotGroup == null)
				{
					obj = null;
				}
				else
				{
					ISlotGroupParent parent = targetSlotGroup.parent;
					obj = ((parent != null) ? parent.SlotYielderLabel() : null);
				}
				if (obj == null)
				{
					obj = TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.EntierMap"));
				}
				if (Widgets.ButtonText(val12, (string)obj, true, true, true, (TextAnchor?)null))
				{
					Find.WindowStack.Add((Window)new FloatMenu(((IEnumerable<SlotGroup>)groups).Select((Func<SlotGroup, FloatMenuOption>)((SlotGroup g) => new FloatMenuOption(GetSlotGroupName(g.parent), (Action)delegate
					{
						Machine.TargetSlotGroup = g;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0))).ToList().Head(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.EntierMap")), (Action)delegate
					{
						Machine.TargetSlotGroup = null;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0))));
				}
				Machine.ProductLimitCount = productLimitCount;
			}
			if (PRfSettingsContent != null)
			{
				val = PRfSettingsContent.PRF_SettingsContentOb.ITab_Settings_AppendContent(val, val2);
			}
			if (ShowRangeTypeSelectorButton)
			{
				((Listing)val).GapLine(12f);
				val2 = ((Listing)val).GetRect(30f, 1f);
				Widgets.Label(GenUI.LeftHalf(val2), Translator.Translate("PRF_SettingsTab_RangeType_Label"));
				if (Widgets.ButtonText(GenUI.RightHalf(val2), CompPropertiesPowerWork.rangeCells.ToText(), true, true, true, (TextAnchor?)null))
				{
					Find.WindowStack.Add((Window)new FloatMenu(((IEnumerable<IRangeCells>)CompPropertiesPowerWork.rangeTypes).Select((Func<IRangeCells, FloatMenuOption>)((IRangeCells d) => new FloatMenuOption(d.ToText(), (Action)delegate
					{
						CompPropertiesPowerWork.rangeCells = d;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0))).ToList()));
				}
				if (CompPropertiesPowerWork.rangeCells.NeedsRotate)
				{
					Rect val13 = val2;
					((Rect)(ref val13)).width = 30f;
					((Rect)(ref val13)).height = 30f;
					Rect val14 = GenUI.RightHalf(val2);
					((Rect)(ref val13)).x = ((Rect)(ref val14)).x - ((Rect)(ref val13)).width - 10f;
					if (Widgets.ButtonImage(val13, TexUI.RotRightTex, true, (string)null))
					{
						((Rot4)(ref CompPropertiesPowerWork.RangeTypeRot)).Rotate((RotationDirection)1);
					}
					TooltipHandler.TipRegion(val13, RotInputRangeTip);
				}
				((Listing)val).Gap(12f);
			}
			((Listing)val).Gap(12f);
			((Listing)val).End();
		}
	}
	public class ModExtension_Graphic : DefModExtension
	{
		[XmlInheritanceAllowDuplicateNodes]
		private List<GraphicDataListItem> graphicDataList = new List<GraphicDataListItem>();

		private Dictionary<string, int> getByNameCache = new Dictionary<string, int>();

		public IEnumerable<Graphic> Graphics => graphicDataList.Select((GraphicDataListItem i) => i.Graphic);

		public Graphic FirstGraphic => Graphics.FirstOrDefault();

		public Graphic GetByName(string name)
		{
			if (name == null)
			{
				return null;
			}
			if (!getByNameCache.TryGetValue(name, out var value))
			{
				GraphicDataListItem item = GenCollection.FirstOrDefault<GraphicDataListItem>(graphicDataList, (Predicate<GraphicDataListItem>)((GraphicDataListItem g) => g.name == name));
				value = graphicDataList.IndexOf(item);
				getByNameCache.Add(name, value);
			}
			return graphicDataList[value].Graphic;
		}
	}
	public class GraphicDataListItem
	{
		public string name;

		public GraphicData graphicData;

		public Graphic Graphic
		{
			get
			{
				GraphicData obj = graphicData;
				return ((obj != null) ? obj.Graphic : null) ?? null;
			}
		}

		public void LoadDataFromXmlCustom(XmlNode xmlRoot)
		{
			name = xmlRoot.Name;
			graphicData = DirectXmlToObject.ObjectFromXml<GraphicData>(xmlRoot.FirstChild, false);
		}
	}
	public class ModExtension_Miner : DefModExtension
	{
		public List<ThingDef> excludeOres;

		public bool IsExcluded(ThingDef def)
		{
			return excludeOres?.Contains(def) ?? false;
		}
	}
	[HarmonyPatch(typeof(GenRecipe), "MakeRecipeProducts")]
	internal static class Patch_GenRecipe_MakeRecipeProducts_BonusYield
	{
		private static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result, RecipeDef recipeDef, Pawn worker, List<Thing> ingredients, Thing dominantIngredient, IBillGiver billGiver)
		{
			List<Thing> products = new List<Thing>(__result);
			Thing t = (Thing)(object)((billGiver is Thing) ? billGiver : null);
			ModExtension_ModifyProduct productExt = default(ModExtension_ModifyProduct);
			int num;
			if (t != null)
			{
				productExt = ((Def)t.def).GetModExtension<ModExtension_ModifyProduct>();
				num = ((productExt?.autoModify ?? false) ? 1 : 0);
			}
			else
			{
				num = 0;
			}
			if (num != 0)
			{
				productExt.ProcessProducts(products, billGiver, t, recipeDef, worker, ingredients, dominantIngredient);
			}
			foreach (Thing item in products)
			{
				yield return item;
			}
		}
	}
	public delegate bool ProductChangerDel(List<Thing> products, ModExtension_ModifyProduct modifyYieldExt, IBillGiver billGiver = null, Thing productMaker = null, RecipeDef recipeDef = null, Pawn worker = null, List<Thing> ingredients = null, Thing dominantIngredient = null);
	public class ProductChanger
	{
		public ProductChangerDel del;

		public static Dictionary<string, ProductChangerDel> specialNames = new Dictionary<string, ProductChangerDel>
		{
			{
				"default",
				ModExtension_ModifyProduct.TryGetDefaultBonusYield
			},
			{
				"simplebonus",
				ModExtension_ModifyProduct.TryGetSimpleBonusYield
			},
			{
				"recipebonus",
				ModExtension_ModifyProduct.TryGetRecipeBonusYield
			}
		};

		public void LoadDataFromXmlCustom(XmlNode xmlRoot)
		{
			string innerText = xmlRoot.InnerText;
			try
			{
				string key = innerText.ToLower();
				if (specialNames.TryGetValue(key, out var value))
				{
					del = value;
					return;
				}
				MethodInfo method = AccessTools.Method(innerText, (Type[])null, (Type[])null);
				del = (ProductChangerDel)Delegate.CreateDelegate(typeof(ProductChangerDel), method);
			}
			catch (Exception ex)
			{
				Log.Error("ModExtension_ModifyProduct could not parse method \"" + innerText + "\"\nFull Exception: " + ex);
			}
		}
	}
	public class ModExtension_ModifyProduct : DefModExtension, IXMLThingDescription
	{
		public bool autoModify = true;

		public List<ModExtension_ModifyProduct> bonuses;

		public bool doAll = false;

		public bool replaceOrigProduct = false;

		public float? chance;

		public QualityCategory? requiredQuality;

		public QualityCategory? minRequiredQuality;

		public ThingDef def = null;

		public float weight = 0f;

		public int count = 1;

		public string materialdef = null;

		public QualityCategory? quality;

		public QualityCategory? minQuality;

		public QualityCategory? maxQuality;

		public ProductChanger altChanger;

		public Dictionary<string, BonusYieldList> billBonusYields;

		public BonusYieldList bonusYields;

		public string extraData;

		public string extraData2;

		public int Count => Mathf.Min(count, def.stackLimit);

		public ThingDef MaterialDef
		{
			get
			{
				if (materialdef != null)
				{
					return ThingDef.Named(materialdef);
				}
				return GenStuff.DefaultStuffFor((BuildableDef)(object)def);
			}
		}

		public bool ProcessProducts(List<Thing> products, IBillGiver billGiver = null, Thing productMaker = null, RecipeDef recipeDef = null, Pawn worker = null, List<Thing> ingredients = null, Thing dominantIngredient = null)
		{
			if (!MeetsRequirements(products))
			{
				return false;
			}
			bool result = false;
			if (altChanger != null)
			{
				bool flag = altChanger.del(products, this, billGiver, productMaker, recipeDef, worker, ingredients, dominantIngredient);
				if (!doAll)
				{
					return flag;
				}
				if (flag)
				{
					result = true;
				}
			}
			if (def != null || bonusYields != null || billBonusYields != null)
			{
				Thing val = BonusThing();
				if (val != null)
				{
					if (replaceOrigProduct)
					{
						products.Clear();
					}
					products.Add(val);
					result = true;
				}
				if (!doAll)
				{
					return val != null;
				}
			}
			if (GenList.NullOrEmpty<ModExtension_ModifyProduct>((IList<ModExtension_ModifyProduct>)bonuses))
			{
				return result;
			}
			foreach (ModExtension_ModifyProduct bonuse in bonuses)
			{
				if (bonuse.ProcessProducts(products, billGiver, productMaker, recipeDef, worker, ingredients, dominantIngredient))
				{
					if (!doAll)
					{
						return true;
					}
					result = true;
				}
			}
			return result;
		}

		public Thing TryGetBonus(RecipeDef recipe = null, QualityCategory? minQuality = null, QualityCategory? maxQuality = null)
		{
			if (!MeetsRequirements())
			{
				return null;
			}
			if (altChanger != null)
			{
				List<Thing> list = new List<Thing>();
				altChanger.del(list, this, null, null, recipe);
				if (list.Count > 0)
				{
					EnsureProperQuality(list[0], minQuality, maxQuality);
					return list[0];
				}
			}
			if (def != null)
			{
				return BonusThing(recipe, minQuality, maxQuality);
			}
			if (GenList.NullOrEmpty<ModExtension_ModifyProduct>((IList<ModExtension_ModifyProduct>)bonuses))
			{
				return null;
			}
			if (bonuses[0].weight > 0f)
			{
				return GenCollection.RandomElementByWeightWithFallback<ModExtension_ModifyProduct>((IEnumerable<ModExtension_ModifyProduct>)bonuses, (Func<ModExtension_ModifyProduct, float>)((ModExtension_ModifyProduct y) => y.weight), (ModExtension_ModifyProduct)null)?.TryGetBonus(recipe, minQuality, maxQuality);
			}
			foreach (ModExtension_ModifyProduct bonuse in bonuses)
			{
				Thing val = bonuse.TryGetBonus(recipe, minQuality, maxQuality);
				if (val != null)
				{
					return val;
				}
			}
			return null;
		}

		public bool MeetsRequirements(List<Thing> products = null)
		{
			return MeetsQualityRequirements(products) && (!chance.HasValue || Rand.Chance(chance.Value));
		}

		public bool MeetsQualityRequirements(List<Thing> products)
		{
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			bool flag = true;
			QualityCategory? val = requiredQuality;
			if (!val.HasValue)
			{
				val = minRequiredQuality;
				if (!val.HasValue)
				{
					return true;
				}
			}
			flag = false;
			if (GenList.NullOrEmpty<Thing>((IList<Thing>)products))
			{
				return false;
			}
			QualityCategory val2 = default(QualityCategory);
			foreach (Thing product in products)
			{
				if (!QualityUtility.TryGetQuality(product, ref val2))
				{
					continue;
				}
				if ((!requiredQuality.HasValue || (QualityCategory?)val2 == requiredQuality) && (!minRequiredQuality.HasValue || val2 >= requiredQuality.Value))
				{
					flag = true;
				}
				break;
			}
			return flag;
		}

		public Thing BonusThing(RecipeDef recipe = null, QualityCategory? minQuality = null, QualityCategory? maxQuality = null)
		{
			Thing val = SimpleBonusThing(minQuality, maxQuality);
			if (val == null)
			{
				return null;
			}
			Thing val2 = RecipeBonusThing(recipe, minQuality, maxQuality);
			if (val2 != null)
			{
				val = val2;
			}
			return val;
		}

		public Thing SimpleBonusThing(QualityCategory? minQuality = null, QualityCategory? maxQuality = null)
		{
			Thing val = null;
			if (def != null)
			{
				val = ThingMaker.MakeThing(def, MaterialDef);
				val.stackCount = Count;
				if (val.def.CanHaveFaction)
				{
					val.SetFaction(Faction.OfPlayer, (Pawn)null);
				}
			}
			if (val == null && bonusYields != null)
			{
				val = bonusYields.GetBonusYield();
			}
			if (val == null)
			{
				return null;
			}
			EnsureProperQuality(val, minQuality, maxQuality);
			return MinifyUtility.TryMakeMinified(val);
		}

		public Thing RecipeBonusThing(RecipeDef recipe = null, QualityCategory? minQuality = null, QualityCategory? maxQuality = null)
		{
			if (recipe == null || billBonusYields == null || !billBonusYields.TryGetValue(((Def)recipe).defName, out var value))
			{
				return null;
			}
			Thing bonusYield = value.GetBonusYield();
			if (bonusYield == null)
			{
				return null;
			}
			EnsureProperQuality(bonusYield);
			return MinifyUtility.TryMakeMinified(bonusYield);
		}

		private void EnsureProperQuality(Thing t, QualityCategory? min = null, QualityCategory? max = null)
		{
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Expected I4, but got Unknown
			//IL_0080: Expected I4, but got Unknown
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Expected I4, but got Unknown
			//IL_009e: Expected I4, but got Unknown
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			CompQuality val = ((t != null) ? ThingCompUtility.TryGetComp<CompQuality>(t) : null);
			if (val != null)
			{
				QualityCategory? val2 = min;
				if (!val2.HasValue)
				{
					min = minQuality;
				}
				val2 = max;
				if (!val2.HasValue)
				{
					max = maxQuality;
				}
				QualityCategory val3 = (QualityCategory)(((??)quality) ?? val.Quality);
				if (min.HasValue)
				{
					val3 = (QualityCategory)(byte)Math.Max((int)min.Value, (int)val3);
				}
				if (max.HasValue)
				{
					val3 = (QualityCategory)(byte)Math.Min((int)max.Value, (int)val3);
				}
				val.SetQuality(val3, (ArtGenerationContext?)(ArtGenerationContext)1);
			}
		}

		public static bool TryGetDefaultBonusYield(List<Thing> products, ModExtension_ModifyProduct modifyYieldExt, IBillGiver billGiver, Thing productMaker, RecipeDef recipeDef, Pawn worker, List<Thing> ingredients, Thing dominantIngredient)
		{
			Thing val = modifyYieldExt.TryGetBonus(recipeDef);
			if (val == null)
			{
				return false;
			}
			if (modifyYieldExt.replaceOrigProduct)
			{
				products.Clear();
			}
			products.Add(val);
			return true;
		}

		public static bool TryGetSimpleBonusYield(List<Thing> products, ModExtension_ModifyProduct modifyYieldExt, IBillGiver billGiver, Thing productMaker, RecipeDef recipeDef, Pawn worker, List<Thing> ingredients, Thing dominantIngredient)
		{
			Thing val = modifyYieldExt.SimpleBonusThing();
			if (val == null)
			{
				return false;
			}
			if (modifyYieldExt.replaceOrigProduct)
			{
				products.Clear();
			}
			products.Add(val);
			return true;
		}

		public static bool TryGetRecipeBonusYield(List<Thing> products, ModExtension_ModifyProduct modifyYieldExt, IBillGiver billGiver, Thing productMaker, RecipeDef recipeDef, Pawn worker, List<Thing> ingredients, Thing dominantIngredient)
		{
			Thing val = modifyYieldExt.RecipeBonusThing(recipeDef);
			if (val == null)
			{
				return false;
			}
			if (modifyYieldExt.replaceOrigProduct)
			{
				products.Clear();
			}
			products.Add(val);
			return true;
		}

		public string GetDescription(ThingDef _)
		{
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Unknown result type (might be due to invalid IL or missing references)
			//IL_0142: Unknown result type (might be due to invalid IL or missing references)
			//IL_014c: Unknown result type (might be due to invalid IL or missing references)
			//IL_017d: Unknown result type (might be due to invalid IL or missing references)
			string empty = string.Empty;
			if (bonusYields == null)
			{
				return empty;
			}
			empty = TaggedString.op_Implicit(replaceOrigProduct ? TranslatorFormattedStringExtensions.Translate("PRF_ModifyProduct_ReplaceChance", NamedArgument.op_Implicit(bonusYields.chance * 100f)) : TranslatorFormattedStringExtensions.Translate("PRF_ModifyProduct_AdditionalChance", NamedArgument.op_Implicit(bonusYields.chance * 100f)));
			if (GenList.NullOrEmpty<BonusYield>((IList<BonusYield>)bonusYields.bonuses))
			{
				return empty;
			}
			float num = bonusYields.bonuses.Sum((BonusYield b) => b.Weight);
			foreach (BonusYield bonuse in bonusYields.bonuses)
			{
				TaggedString? obj;
				if (bonuse == null)
				{
					obj = null;
				}
				else
				{
					ThingDef obj2 = bonuse.def;
					obj = ((obj2 != null) ? new TaggedString?(((Def)obj2).LabelCap) : ((TaggedString?)null));
				}
				TaggedString? val = obj;
				if (val.HasValue)
				{
					empty = empty + "    - " + string.Format("{0,-20} \t", TaggedString.op_Implicit(((Def)bonuse.def).LabelCap + " x") + bonuse.Count) + string.Format("{0} ", Translator.Translate("PRF_ModifyProduct_Percent")) + $"{bonuse.Weight / num * 100f:G3}%\r\n";
				}
			}
			return empty;
		}
	}
	public class BonusYieldList
	{
		public List<BonusYield> bonuses;

		public float chance;

		public void LoadDataFromXmlCustom(XmlNode xmlRoot)
		{
			if (xmlRoot.Attributes["Chance"] != null)
			{
				float.TryParse(xmlRoot.Attributes["Chance"].Value, out chance);
			}
			bonuses = (from XmlNode n in xmlRoot.ChildNodes
				where n.NodeType == XmlNodeType.Element
				select n as XmlElement into e
				where e != null
				select DirectXmlToObject.ObjectFromXml<BonusYield>((XmlNode)e, false)).ToList();
		}

		public Thing GetBonusYield()
		{
			if (!Rand.Chance(chance))
			{
				return null;
			}
			List<BonusYield> list = bonuses;
			BonusYield bonusYield = ((list != null) ? GenCollection.RandomElementByWeightWithFallback<BonusYield>((IEnumerable<BonusYield>)list, (Func<BonusYield, float>)((BonusYield y) => y.Weight), (BonusYield)null) : null);
			if (bonusYield == null)
			{
				return null;
			}
			Thing val = ThingMaker.MakeThing(bonusYield.def, bonusYield.MaterialDef);
			val.stackCount = bonusYield.Count;
			QualityCategory val2 = default(QualityCategory);
			if (QualityUtility.TryGetQuality(val, ref val2))
			{
				CompQuality obj = ThingCompUtility.TryGetComp<CompQuality>(val);
				if (obj != null)
				{
					obj.SetQuality((QualityCategory)(byte)bonusYield.Quality, (ArtGenerationContext?)(ArtGenerationContext)1);
				}
			}
			if (val.def.CanHaveFaction)
			{
				val.SetFaction(Faction.OfPlayer, (Pawn)null);
			}
			return MinifyUtility.TryMakeMinified(val);
		}
	}
	public class BonusYield
	{
		public ThingDef def;

		public float weight;

		public int count;

		public int quality;

		public string materialdef = null;

		public int Count => Mathf.Min(count, def.stackLimit);

		public float Weight => weight;

		public int Quality => quality;

		public ThingDef MaterialDef
		{
			get
			{
				if (materialdef != null)
				{
					return ThingDef.Named(materialdef);
				}
				return GenStuff.DefaultStuffFor((BuildableDef)(object)def);
			}
		}

		public void LoadDataFromXmlCustom(XmlNode xmlRoot)
		{
			DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef((object)this, "def", xmlRoot.Name, (string)null, (string)null, (Type)null);
			if (xmlRoot.Attributes["Weight"] != null)
			{
				float.TryParse(xmlRoot.Attributes["Weight"].Value, out weight);
			}
			if (xmlRoot.Attributes["Count"] != null)
			{
				int.TryParse(xmlRoot.Attributes["Count"].Value, out count);
			}
			if (xmlRoot.Attributes["Quality"] != null)
			{
				int.TryParse(xmlRoot.Attributes["Quality"].Value, out quality);
			}
			if (xmlRoot.Attributes["MaterialDef"] != null)
			{
				materialdef = xmlRoot.Attributes["MaterialDef"].Value;
			}
		}
	}
	internal class ModExtension_Skills : DefModExtension, IXMLThingDescription
	{
		public enum enum_ModExtension_SkillsskillUsage
		{
			ThisOverrides,
			ReserchIsCapping,
			ThisIsCapping,
			ReserchOverrides
		}

		public enum_ModExtension_SkillsskillUsage SkillUsage = enum_ModExtension_SkillsskillUsage.ReserchOverrides;

		public int BaseSkill = 0;

		public List<SkillRecord> skills = null;

		public int GetSkillLevel(SkillDef skillDef)
		{
			return GenCollection.FirstOrDefault<SkillRecord>(skills, (Predicate<SkillRecord>)((SkillRecord x) => x.def == skillDef))?.levelInt ?? BaseSkill;
		}

		public int GetExtendedSkillLevel(SkillDef skillDef, Type type)
		{
			return SkillUsage switch
			{
				enum_ModExtension_SkillsskillUsage.ThisOverrides => GetSkillLevel(skillDef), 
				enum_ModExtension_SkillsskillUsage.ReserchIsCapping => Mathf.Clamp(GetSkillLevel(skillDef), 0, ReserchSkillModifier.GetResearchSkillLevel(type)), 
				enum_ModExtension_SkillsskillUsage.ThisIsCapping => Mathf.Clamp(ReserchSkillModifier.GetResearchSkillLevel(type), 0, GetSkillLevel(skillDef)), 
				enum_ModExtension_SkillsskillUsage.ReserchOverrides => ReserchSkillModifier.GetResearchSkillLevel(type), 
				_ => GetSkillLevel(skillDef), 
			};
		}

		public string GetDescription(ThingDef def)
		{
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
			string text = string.Empty;
			switch (SkillUsage)
			{
			case enum_ModExtension_SkillsskillUsage.ThisOverrides:
				text = TaggedString.op_Implicit(text + Translator.Translate("PRF_UTD_ModExtension_Skills_ThisOverrides"));
				break;
			case enum_ModExtension_SkillsskillUsage.ReserchIsCapping:
				text = TaggedString.op_Implicit(text + Translator.Translate("PRF_UTD_ModExtension_Skills_ReserchIsCapping"));
				break;
			case enum_ModExtension_SkillsskillUsage.ThisIsCapping:
				text = TaggedString.op_Implicit(text + Translator.Translate("PRF_UTD_ModExtension_Skills_ThisIsCapping"));
				break;
			case enum_ModExtension_SkillsskillUsage.ReserchOverrides:
				return TaggedString.op_Implicit(text + Translator.Translate("PRF_UTD_ModExtension_Skills_ReserchOverrides"));
			}
			text = TaggedString.op_Implicit(text + TranslatorFormattedStringExtensions.Translate("PRF_UTD_ModExtension_Skills_Base", NamedArgument.op_Implicit(BaseSkill)));
			if (skills == null)
			{
				return text;
			}
			text += "\r\n";
			foreach (SkillRecord skill in skills)
			{
				text += $"  - {((Def)skill.def).LabelCap}: {skill.levelInt}";
				text += "\r\n";
			}
			return text;
		}
	}
	public class PatchOperationRTFuseInsertionHook : PatchOperationAdd
	{
		protected override bool ApplyWorker(XmlDocument xml)
		{
			return GenTypes.GetTypeInAnyAssembly("RT_Fuse.CompRTFuse", (string)null) == null || ((PatchOperationAdd)this).ApplyWorker(xml);
		}
	}
	public static class PlaceThingUtility
	{
		public static Func<IntVec3, Map, Thing, bool> CallNoStorageBlockersIn;

		public static bool PRFTryPlaceThing(this IPRF_Building placer, Thing t, IntVec3 cell, Map map, bool forcePlace = false)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fb: Invalid comparison between Unknown and I4
			//IL_0172: Unknown result type (might be due to invalid IL or missing references)
			//IL_023d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_019d: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
			if (!GenGrid.InBounds(cell, map))
			{
				Log.Error("PRF Error Attempting to place Thing out of bounds.");
				return false;
			}
			SlotGroup slotGroup = StoreUtility.GetSlotGroup(cell, map);
			if (slotGroup != null)
			{
				if (slotGroup.parent is IPRF_Building)
				{
					if (placer.PlaceThingNextBuilding(slotGroup.parent as IPRF_Building, t, cell, map))
					{
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
				bool flag = false;
				foreach (Thing item in map.thingGrid.ThingsListAt(cell))
				{
					if (item.TryAbsorbStack(t, true))
					{
						placer.EffectOnPlaceThing(item);
						return true;
					}
					if ((int)((BuildableDef)item.def).passability == 2)
					{
						flag = true;
					}
					if (item is IPRF_Building nextBuilding && placer.PlaceThingNextBuilding(nextBuilding, t, cell, map))
					{
						placer.EffectOnPlaceThing(t);
						return true;
					}
				}
				if (flag)
				{
					if (!forcePlace)
					{
						return false;
					}
				}
				else if (CallNoStorageBlockersIn(cell, map, t))
				{
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

		private static bool PlaceThingInSlotGroup(this IPRF_Building placer, Thing t, SlotGroup slotGroup, IntVec3 cell, Map map)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			if (placer != null && placer.OutputToEntireStockpile)
			{
				return placer.PlaceThingAnywhereInSlotGroup(t, slotGroup, cell);
			}
			if (placer.ObeysStorageFilters && !((IHaulDestination)slotGroup.parent).Accepts(t))
			{
				return false;
			}
			if (CallNoStorageBlockersIn(cell, map, t))
			{
				bool spawned = t.Spawned;
				if (spawned)
				{
					((Entity)t).DeSpawn((DestroyMode)0);
				}
				if (!GenPlace.TryPlaceThing(t, cell, map, (ThingPlaceMode)0, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
				{
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
			return false;
		}

		private static bool PlaceThingAnywhereInSlotGroup(this IPRF_Building placer, Thing t, SlotGroup slotGroup, IntVec3? cell = null)
		{
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00de: Unknown result type (might be due to invalid IL or missing references)
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
					IntVec3? val = cell;
					return !val.HasValue || x != val.GetValueOrDefault();
				}));
			}
			foreach (IntVec3 item in enumerable)
			{
				if (!CallNoStorageBlockersIn(item, map, t))
				{
					continue;
				}
				if (t.Spawned)
				{
					((Entity)t).DeSpawn((DestroyMode)0);
				}
				if (!GenPlace.TryPlaceThing(t, item, map, (ThingPlaceMode)0, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
				{
					continue;
				}
				placer.EffectOnPlaceThing(t);
				if (placer.ForbidOnPlacing(t))
				{
					ForbidUtility.SetForbidden(t, true, false);
				}
				return true;
			}
			return false;
		}

		public static bool CanPlaceThingInSlotGroup(Thing t, SlotGroup slotGroup, Map map)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			foreach (IntVec3 cells in slotGroup.CellsList)
			{
				if (CallNoStorageBlockersIn(cells, map, t))
				{
					return true;
				}
			}
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
	internal class PlaceWorker_GrowZonePuller : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			Rot4 opposite = ((Rot4)(ref rot)).Opposite;
			IntVec3 val = loc + ((Rot4)(ref opposite)).FacingCell;
			if (GenGrid.InBounds(val, map) && GridsUtility.GetZone(val, map) is IPlantToGrowSettable)
			{
				return AcceptanceReport.WasAccepted;
			}
			return new AcceptanceReport(TaggedString.op_Implicit(Translator.Translate("PRF_PlaceWorker_GrowZonePuller")));
		}
	}
	public class PlaceWorker_NaturalWall : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			IEnumerable<IntVec3> source = GenAdj.CellsOccupiedBy(loc, rot, checkingDef.Size);
			if (source.All((IntVec3 t) => GenCollection.Any<Thing>(GridsUtility.GetThingList(t, map), (Predicate<Thing>)((Thing val) => val.def.IsNonResourceNaturalRock || val.def.IsSmoothed)) && !GenCollection.Any<Thing>(GridsUtility.GetThingList(t, map), (Predicate<Thing>)delegate(Thing val)
			{
				ThingDef def = val.def;
				BuildableDef obj = checkingDef;
				return (object)def == ((obj is ThingDef) ? obj : null);
			})))
			{
				return AcceptanceReport.WasAccepted;
			}
			return new AcceptanceReport(TaggedString.op_Implicit(Translator.Translate("PRF_PlaceWorker_NaturalWall_denied")));
		}

		public override bool ForceAllowPlaceOver(BuildableDef other)
		{
			ThingDef blueprintDef = other.blueprintDef;
			if (blueprintDef != null && blueprintDef.IsSmoothed)
			{
				return true;
			}
			blueprintDef = (ThingDef)(object)((other is ThingDef) ? other : null);
			if (blueprintDef != null && blueprintDef.IsNonResourceNaturalRock)
			{
				return true;
			}
			return ((PlaceWorker)this).ForceAllowPlaceOver(other);
		}
	}
	[StaticConstructorOnStartup]
	public static class PRFDefOf
	{
		public static JobDef PRFStaticJob;

		public static JobDef PRFDrone_ReturnToStation;

		public static JobDef PRFDrone_SelfTerminate;

		public static PawnKindDef PRFDroneKind;

		public static PawnKindDef PRFSlavePawn;

		public static ResearchProjectDef PRFAtomicReconstruction;

		public static ResearchProjectDef PRFEdiblesSynthesis;

		public static ResearchProjectDef PRFManufacturablesProduction;

		public static ResearchProjectDef PaperclipGeneratorSelfImprovement;

		public static ResearchProjectDef PaperclipGeneratorKugelblitz;

		public static ResearchProjectDef PaperclipGeneratorQuantumFoamManipulation;

		public static ResearchProjectDef PRFOrdoDataRummaging;

		public static TerrainDef PRFFloorComputer;

		public static TerrainDef PRFZCompositeTile;

		public static TerrainDef PRFYCompositeTile;

		public static ThingDef Paperclip;

		public static ThingDef PRFDrone;

		public static ThingDef PRF_DroneModule;

		public static ThingDef Column;

		public static ThingDef PRF_MiniDroneColumn;

		public static ThingDef PRF_Recycler;

		public static BackstoryDef ChildSpy47;

		public static BackstoryDef ColonySettler53;

		public static ResearchProjectDef PRF_BasicDrones;

		public static ResearchProjectDef PRF_ImprovedDrones;

		public static ResearchProjectDef PRF_AdvancedDrones;

		public static TraderKindDef PRF_Factory_Supplier;

		static PRFDefOf()
		{
			Type typeFromHandle = typeof(PRFDefOf);
			FieldInfo[] fields = typeFromHandle.GetFields();
			for (int i = 0; i < fields.Length; i++)
			{
				object obj = GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), fields[i].FieldType, "GetNamedSilentFail", new object[1] { fields[i].Name });
				if (obj != null)
				{
					fields[i].SetValue(null, obj);
				}
			}
		}
	}
	public class PRFMapComponent : MapComponent
	{
		private List<ITicker> tickers = new List<ITicker>();

		public HashSet<IntVec3> iHideRightMenus = new HashSet<IntVec3>();

		public readonly List<Building_ColdStorage> ColdStorageBuildings = new List<Building_ColdStorage>();

		private readonly Dictionary<IntVec3, List<IHideItem>> hideItemLocations = new Dictionary<IntVec3, List<IHideItem>>();

		private readonly Dictionary<IntVec3, List<IForbidPawnOutputItem>> forbidPawnOutputItemLocations = new Dictionary<IntVec3, List<IForbidPawnOutputItem>>();

		public readonly Dictionary<Building_BeltConveyor, IBeltConveyorLinkable> NextBeltCache = new Dictionary<Building_BeltConveyor, IBeltConveyorLinkable>();

		private readonly List<IRecipeSubscriber> recipeSubscribers = new List<IRecipeSubscriber>();

		public Dictionary<IntVec3, Building_AdvancedStorageUnitIOPort> GetAdvancedIOLocations { get; } = new Dictionary<IntVec3, Building_AdvancedStorageUnitIOPort>();

		public void RegisterRecipeSubscriber(IRecipeSubscriber recipeSubscriber)
		{
			recipeSubscribers.Add(recipeSubscriber);
		}

		public void DeregisterRecipeSubscriber(IRecipeSubscriber recipeSubscriber)
		{
			recipeSubscribers.Remove(recipeSubscriber);
		}

		public void NotifyRecipeSubscriberOfProvider(IntVec3 pos, Building_RecipeHolder recipeHolder)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			foreach (IRecipeSubscriber recipeSubscriber in recipeSubscribers)
			{
				recipeSubscriber.RecipeProviderSpawnedAt(pos, recipeHolder);
			}
		}

		public void RegisterColdStorageBuilding(Building_ColdStorage port)
		{
			if (!ColdStorageBuildings.Contains(port))
			{
				ColdStorageBuildings.Add(port);
			}
		}

		public void DeRegisterColdStorageBuilding(Building_ColdStorage port)
		{
			if (ColdStorageBuildings.Contains(port))
			{
				ColdStorageBuildings.Remove(port);
			}
		}

		public void RegisteradvancedIOLocations(IntVec3 pos, Building_AdvancedStorageUnitIOPort port)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			GetAdvancedIOLocations.TryAdd(pos, port);
		}

		public void DeRegisteradvancedIOLocations(IntVec3 pos)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			if (GetAdvancedIOLocations.ContainsKey(pos))
			{
				GetAdvancedIOLocations.Remove(pos);
			}
		}

		public void RegisterIHideItemPos(IntVec3 pos, IHideItem hideItem)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			if (hideItemLocations.ContainsKey(pos))
			{
				hideItemLocations[pos].Add(hideItem);
				return;
			}
			hideItemLocations.Add(pos, new List<IHideItem>(1) { hideItem });
		}

		public void DeRegisterIHideItemPos(IntVec3 pos, IHideItem hideItem)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			if (hideItemLocations[pos].Count <= 1)
			{
				hideItemLocations.Remove(pos);
			}
			else
			{
				hideItemLocations[pos].Remove(hideItem);
			}
		}

		public void RegisterIForbidPawnOutputItem(IntVec3 pos, IForbidPawnOutputItem forbidPawnOutput)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			if (forbidPawnOutputItemLocations.ContainsKey(pos))
			{
				forbidPawnOutputItemLocations[pos].Add(forbidPawnOutput);
				return;
			}
			forbidPawnOutputItemLocations.Add(pos, new List<IForbidPawnOutputItem>(1) { forbidPawnOutput });
		}

		public void DeRegisterIForbidPawnOutputItem(IntVec3 pos, IForbidPawnOutputItem forbidPawnOutput)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			if (forbidPawnOutputItemLocations[pos].Count <= 1)
			{
				forbidPawnOutputItemLocations.Remove(pos);
			}
			else
			{
				forbidPawnOutputItemLocations[pos].Remove(forbidPawnOutput);
			}
		}

		private List<IHideItem> CheckIHideItemPos(IntVec3 pos)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			return CollectionExtensions.GetValueOrDefault<IntVec3, List<IHideItem>>((IReadOnlyDictionary<IntVec3, List<IHideItem>>)hideItemLocations, pos);
		}

		public bool ShouldHideItemsAtPos(IntVec3 pos)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			List<IHideItem> list = CheckIHideItemPos(pos);
			return list != null && GenCollection.Any<IHideItem>(list, (Predicate<IHideItem>)((IHideItem t) => t.HideItems));
		}

		private List<IForbidPawnOutputItem> CheckIForbidPawnOutputItem(IntVec3 pos)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			return CollectionExtensions.GetValueOrDefault<IntVec3, List<IForbidPawnOutputItem>>((IReadOnlyDictionary<IntVec3, List<IForbidPawnOutputItem>>)forbidPawnOutputItemLocations, pos);
		}

		public bool ShouldForbidPawnOutputAtPos(IntVec3 pos)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			List<IForbidPawnOutputItem> list = CheckIForbidPawnOutputItem(pos);
			return list != null && GenCollection.Any<IForbidPawnOutputItem>(list, (Predicate<IForbidPawnOutputItem>)((IForbidPawnOutputItem t) => t.ForbidPawnOutput));
		}

		public PRFMapComponent(Map map)
			: base(map)
		{
		}

		public override void MapComponentTick()
		{
			((MapComponent)this).MapComponentTick();
			tickers.ForEach(delegate(ITicker t)
			{
				t.Tick();
			});
		}

		public void AddTicker(ITicker ticker)
		{
			tickers.Add(ticker);
		}

		public void RemoveTicker(ITicker ticker)
		{
			tickers.Remove(ticker);
		}

		public unsafe void AddIHideRightClickMenu(Building b)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			CellRect val = GenAdj.OccupiedRect((Thing)(object)b);
			Enumerator enumerator = ((CellRect)(ref val)).GetEnumerator();
			try
			{
				while (((Enumerator)(ref enumerator)).MoveNext())
				{
					IntVec3 current = ((Enumerator)(ref enumerator)).Current;
					iHideRightMenus.Add(current);
				}
			}
			finally
			{
				((IDisposable)(*(Enumerator*)(&enumerator))/*cast due to constrained. prefix*/).Dispose();
			}
		}

		public unsafe void RemoveIHideRightClickMenu(Building b)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			CellRect val = GenAdj.OccupiedRect((Thing)(object)b);
			Enumerator enumerator = ((CellRect)(ref val)).GetEnumerator();
			try
			{
				while (((Enumerator)(ref enumerator)).MoveNext())
				{
					IntVec3 current = ((Enumerator)(ref enumerator)).Current;
					iHideRightMenus.Remove(current);
					foreach (Thing item in base.map.thingGrid.ThingsListAt(current))
					{
						if ((object)item != b && item is IHideRightClickMenu)
						{
							iHideRightMenus.Add(current);
						}
					}
				}
			}
			finally
			{
				((IDisposable)(*(Enumerator*)(&enumerator))/*cast due to constrained. prefix*/).Dispose();
			}
		}
	}
	public interface ITicker
	{
		void Tick();
	}
	public interface IHideRightClickMenu
	{
		bool HideRightClickMenus { get; }
	}
	public abstract class PRF_Building : Building, IPRF_Building, IProductionSettingsUser
	{
		protected bool outputToEntireStockpile;

		protected bool obeysStorageFilters = true;

		private bool forbidOnPlacingDefault;

		public virtual IEnumerable<Thing> AvailableThings => Array.Empty<Thing>();

		public virtual bool ForbidOnPlacingDefault
		{
			get
			{
				return forbidOnPlacingDefault;
			}
			set
			{
				forbidOnPlacingDefault = value;
			}
		}

		public virtual bool ObeysStorageFilters
		{
			get
			{
				return obeysStorageFilters;
			}
			set
			{
				obeysStorageFilters = value;
			}
		}

		public virtual bool OutputToEntireStockpile
		{
			get
			{
				return outputToEntireStockpile;
			}
			set
			{
				outputToEntireStockpile = value;
			}
		}

		public virtual PRFBSetting SettingsOptions => PRFBSetting.optionOutputToEntireStockpie | PRFBSetting.optionObeysStorageFilters;

		public virtual bool AcceptsThing(Thing newThing, IPRF_Building giver)
		{
			return false;
		}

		public abstract Thing GetThingBy(Func<Thing, bool> optionalValidator = null);

		public virtual void EffectOnPlaceThing(Thing t)
		{
		}

		public virtual void EffectOnAcceptThing(Thing t)
		{
		}

		public virtual bool ForbidOnPlacing(Thing t)
		{
			return ForbidOnPlacingDefault;
		}

		public virtual IntVec3 OutputCell()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			return IntVec3.Invalid;
		}

		public override void ExposeData()
		{
			((Building)this).ExposeData();
			Scribe_Values.Look<bool>(ref outputToEntireStockpile, "PRFOutputToEntireStockpile", false, false);
			Scribe_Values.Look<bool>(ref obeysStorageFilters, "PRFObeysStorageFilters", true, false);
			Scribe_Values.Look<bool>(ref forbidOnPlacingDefault, "PRFForbidOnPlacingDefault", false, false);
		}

		public override void DrawExtraSelectionOverlays()
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).DrawExtraSelectionOverlays();
			if (OutputToEntireStockpile && OutputCell() != IntVec3.Invalid)
			{
				SlotGroup slotGroup = StoreUtility.GetSlotGroup(OutputCell(), ((Thing)this).Map);
				if (slotGroup != null)
				{
					GenDraw.DrawFieldEdges(slotGroup.CellsList, CommonColors.OutputZone, (float?)null, (HashSet<IntVec3>)null, 2900);
				}
			}
		}
	}
	[Flags]
	public enum PRFBSetting
	{
		optionOutputToEntireStockpie = 1,
		optionObeysStorageFilters = 2
	}
	public abstract class PRF_BuildingBill : Building_WorkTable, IPRF_Building, IProductionSettingsUser
	{
		protected bool outputToEntireStockpile;

		protected bool obeysStorageFilters = true;

		private bool forbidOnPlacingDefault;

		public virtual IEnumerable<Thing> AvailableThings => Array.Empty<Thing>();

		public virtual bool ForbidOnPlacingDefault
		{
			get
			{
				return forbidOnPlacingDefault;
			}
			set
			{
				forbidOnPlacingDefault = value;
			}
		}

		public virtual bool ObeysStorageFilters
		{
			get
			{
				return obeysStorageFilters;
			}
			set
			{
				obeysStorageFilters = value;
			}
		}

		public virtual bool OutputToEntireStockpile
		{
			get
			{
				return outputToEntireStockpile;
			}
			set
			{
				outputToEntireStockpile = value;
			}
		}

		public virtual PRFBSetting SettingsOptions => PRFBSetting.optionOutputToEntireStockpie | PRFBSetting.optionObeysStorageFilters;

		public virtual bool AcceptsThing(Thing newThing, IPRF_Building giver)
		{
			return false;
		}

		public abstract Thing GetThingBy(Func<Thing, bool> optionalValidator = null);

		public virtual void EffectOnPlaceThing(Thing t)
		{
		}

		public virtual void EffectOnAcceptThing(Thing t)
		{
		}

		public virtual bool ForbidOnPlacing(Thing t)
		{
			return ForbidOnPlacingDefault;
		}

		public virtual IntVec3 OutputCell()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			return IntVec3.Invalid;
		}

		public override void ExposeData()
		{
			((Building_WorkTable)this).ExposeData();
			Scribe_Values.Look<bool>(ref outputToEntireStockpile, "PRFOutputToEntireStockpile", false, false);
			Scribe_Values.Look<bool>(ref obeysStorageFilters, "PRFObeysStorageFilters", true, false);
			Scribe_Values.Look<bool>(ref forbidOnPlacingDefault, "PRFForbidOnPlacingDefault", false, false);
		}

		public override void DrawExtraSelectionOverlays()
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).DrawExtraSelectionOverlays();
			if (OutputToEntireStockpile && OutputCell() != IntVec3.Invalid)
			{
				SlotGroup slotGroup = StoreUtility.GetSlotGroup(OutputCell(), ((Thing)this).Map);
				if (slotGroup != null)
				{
					GenDraw.DrawFieldEdges(slotGroup.CellsList, CommonColors.OutputZone, (float?)null, (HashSet<IntVec3>)null, 2900);
				}
			}
		}
	}
	public static class PRF_ChangedDefTracker
	{
		public static List<ThingDef> OriginalThingDefs = new List<ThingDef>();

		public static List<ResearchProjectDef> OriginalResearchProjectDefs = new List<ResearchProjectDef>();
	}
	internal class PRF_CustomizeDefs
	{
		public static readonly string[] ExcludeScenario = new string[1] { "PRF_FactoryEntrepreneur" };

		public static void ToggleLiteMode(bool remove = true)
		{
			string[] source = new string[57]
			{
				"PRF_DeepQuarry_mkI", "PRF_DeepQuarry_mkII", "PRF_DeepQuarry_mkIII", "PRF_BillTypeMiner_I", "PRF_AutoCrafterSimple", "PRF_AutoCrafter", "PRF_TheArtMachine", "PRF_TheArtMaster", "PRF_OverclockedAutoAssembler", "PRF_OverclockedAutoAssemblerII",
				"PRF_SALAutoCooker", "PRF_SALAutoMinerI", "PRF_GodlyCrafter", "PRF_StoneWorks", "PRF_Recycler", "PRF_MrTsArtMachine", "PRF_MetalRefinery", "PRF_PartAssembler", "PRF_DroneCultivator_I", "PRF_DroneCultivator_II",
				"PRF_DroneCultivator_II_sun", "PRF_DroneCultivator_III", "PRF_OldTypeCultivator_I", "PRF_OldTypeCultivator_Sun", "PRF_OldTypeCultivator_Xl", "PRF_SelfCookerI", "PRF_SelfCookerII", "PRF_SelfCookerIII", "PRF_MeatGrinder", "PRF_FermentingBarrel",
				"PRF_Slaughterhouse", "PRF_AssemblerGroup", "PRF_RecipeDatabase", "PRF_Shearer", "PRF_Milker", "PRF_GenericAnimalHarvester", "PRF_GenericAnimalHarvester_II", "PRF_Sprinkler_I", "PRF_Sprinkler_II", "PRF_MineShaft",
				"PRF_MiniHelper", "PRF_MiniDroneColumn", "PRF_TypeOneAssembler_I", "PRF_TypeTwoAssembler_I", "PRF_TypeTwoAssembler_II", "PRF_TypeTwoAssembler_III", "PRF_FurnaceI", "PRF_FurnaceII", "PRF_FurnaceIII", "PRF_SelfPrepper",
				"PRF_Factory_Supplier", "PRF_ResearchTerminal", "TableRoboticMachining", "PRF_4k_Battery", "PRF_16k_Battery", "PRF_64k_Battery", "PRF_256k_Battery"
			};
			string[] source2 = new string[21]
			{
				"PRF_FloorLampArrow", "PRF_RedFloorLampArrow", "PRF_GreenFloorLampArrow", "PRF_FloorLampX", "PRF_FloorInput", "PRF_FloorOutput", "PRF_IconClothes", "PRF_IconSkull", "PRF_IconToxic", "PRF_IconPower",
				"PRF_IconGears", "PRF_IconGun", "PRF_IconGasmask", "PRF_IconFire", "PRF_IconCold", "PRF_IconDanger", "PRF_IconExit", "PRF_IconPrison", "PRF_IconResearch", "PRF_IconHospital",
				"PRF_IconBarbedWire"
			};
			string[] source3 = new string[5] { "PRF_MiniHelperAtomic", "PRF_MiniHelperFishing", "PRF_T1_Aquaculture", "PRF_T2_Aquaculture", "PRF_FishFood" };
			string[] source4 = new string[1] { "Make_PRF_FishFood" };
			string[] source5 = new string[37]
			{
				"PRF_AutomaticFarmingI", "PRF_AutomaticFarmingII", "PRF_AutomaticFarmingIII", "PRF_BasicDrones", "PRF_ImprovedDrones", "PRF_AdvancedDrones", "PRF_AutonomousMining", "PRF_AutonomousMiningII", "PRF_AutonomousMiningIII", "PRF_SALResearchI",
				"PRF_SALResearchII", "PRF_SALResearchIII", "PRF_SALResearchIV", "PRF_SALResearchV", "PRF_SALResearchVII", "PRF_SALResearchVI", "PRF_SALResearchVIII", "PRF_SALGodlyCrafting", "PRF_EnhancedBatteries", "PRF_LargeBatteries",
				"PRF_VeryLargeBatteries", "PRF_UniversalAutocrafting", "PRF_SelfCorrectingAssemblers", "PRF_SelfCorrectingAssemblersII", "PRF_MetalRefining", "PRF_AnimalStations", "PRF_AnimalStationsII", "PRF_AnimalStationsIII", "PRF_SelfCooking", "PRF_SelfCookingII",
				"PRF_MachineLearning", "PRF_MagneticTape", "PRF_CoreTierO", "PRF_CoreTierI", "PRF_CoreTierII", "PRF_CoreTierIII", "PRF_CoreTierIV"
			};
			string[] source6 = new string[1] { "PRF_Factory_Supplier" };
			string[] source7 = new string[9] { "PRF_RoboticArm", "PRF_ElectronicChip_I", "PRF_ElectronicChip_II", "PRF_ElectronicChip_III", "PRF_DroneModule", "PRF_DataDisk", "PRF_MachineFrame_I", "PRF_MachineFrame_II", "PRF_MachineFrame_III" };
			if (remove)
			{
				List<string> list = source.ToList();
				list.AddRange(source2.ToList());
				list.AddRange(source3.ToList());
				RemoveBuildableThingDefs(list);
				RemoveResearch(source5.ToList());
				RemoveComponents(source7.ToList());
				RemoveTrader(source6.ToList());
				RemoveScenario(ExcludeScenario.ToList());
				RemoveRecipe(source4.ToList());
				return;
			}
			foreach (ResearchProjectDef item in ProjectRimFactory_ModSettings.defTracker.GetAllWithKeylet<ResearchProjectDef>("res"))
			{
				DefDatabase<ResearchProjectDef>.AllDefsListForReading.Add(item);
			}
			foreach (ResearchProjectDef def in ProjectRimFactory_ModSettings.defTracker.GetAllWithKeylet<ResearchProjectDef>("prerequisitesBase"))
			{
				DefDatabase<ResearchProjectDef>.AllDefsListForReading[GenCollection.FirstIndexOf<ResearchProjectDef>((IEnumerable<ResearchProjectDef>)DefDatabase<ResearchProjectDef>.AllDefsListForReading, (Func<ResearchProjectDef, bool>)((ResearchProjectDef c) => ((Def)c).defName == ((Def)def).defName))].prerequisites = ProjectRimFactory_ModSettings.defTracker.GetDefaultValue<List<ResearchProjectDef>>(((Def)def).defName, "prerequisites");
			}
			foreach (ResearchProjectDef def2 in ProjectRimFactory_ModSettings.defTracker.GetAllWithKeylet<ResearchProjectDef>("requiredResearchFacilitiesBase"))
			{
				DefDatabase<ResearchProjectDef>.AllDefsListForReading[GenCollection.FirstIndexOf<ResearchProjectDef>((IEnumerable<ResearchProjectDef>)DefDatabase<ResearchProjectDef>.AllDefsListForReading, (Func<ResearchProjectDef, bool>)((ResearchProjectDef c) => ((Def)c).defName == ((Def)def2).defName))].requiredResearchFacilities = ProjectRimFactory_ModSettings.defTracker.GetDefaultValue<List<ThingDef>>(((Def)def2).defName, "requiredResearchFacilities");
			}
			foreach (ThingDef item2 in ProjectRimFactory_ModSettings.defTracker.GetAllWithKeylet<ThingDef>("def"))
			{
				DefDatabase<ThingDef>.AllDefsListForReading.Add(item2);
			}
			foreach (ThingDef def3 in ProjectRimFactory_ModSettings.defTracker.GetAllWithKeylet<ThingDef>("alterdDef"))
			{
				((BuildableDef)DefDatabase<ThingDef>.AllDefsListForReading[GenCollection.FirstIndexOf<ThingDef>((IEnumerable<ThingDef>)DefDatabase<ThingDef>.AllDefsListForReading, (Func<ThingDef, bool>)((ThingDef c) => ((Def)c).defName == ((Def)def3).defName))]).costList = ProjectRimFactory_ModSettings.defTracker.GetDefaultValue<List<ThingDefCountClass>>(((Def)def3).defName, "costList");
			}
			foreach (TraderKindDef item3 in ProjectRimFactory_ModSettings.defTracker.GetAllWithKeylet<TraderKindDef>("trader"))
			{
				DefDatabase<TraderKindDef>.AllDefsListForReading.Add(item3);
			}
			foreach (ScenarioDef item4 in ProjectRimFactory_ModSettings.defTracker.GetAllWithKeylet<ScenarioDef>("scenario"))
			{
				DefDatabase<ScenarioDef>.AllDefsListForReading.Add(item4);
			}
			foreach (RecipeDef item5 in ProjectRimFactory_ModSettings.defTracker.GetAllWithKeylet<RecipeDef>("RecipeDef"))
			{
				DefDatabase<RecipeDef>.AllDefsListForReading.Add(item5);
			}
			ClearRecipesCache();
			ArchitectMenu_ClearCache();
			ModSupportResetLayout();
		}

		private static void ModSupportResetLayout()
		{
			if (ProjectRimFactory_ModComponent.ModSupport_ReserchPal)
			{
				ProjectRimFactory_ModComponent.ModSupport_ReserchPal_ResetLayout.Invoke(null, null);
			}
			else if (ProjectRimFactory_ModComponent.ModSupport_ReserchPowl)
			{
				ProjectRimFactory_ModComponent.ModSupport_ReserchPowl_ResetLayout.Invoke(null, null);
			}
			else if (ProjectRimFactory_ModComponent.ModSupport_ResearchTreeContinued)
			{
				ProjectRimFactory_ModComponent.ModSupport_ResearchTreeContinued_ResetLayout.Invoke(null, new object[1] { true });
			}
		}

		private static void ClearRecipesCache()
		{
			List<ThingDef> list = DefDatabase<ThingDef>.AllDefsListForReading.Where((ThingDef d) => d.IsWorkTable).ToList();
			foreach (ThingDef item in list)
			{
				ReflectionUtility.AllRecipesCached.SetValue(item, null);
			}
		}

		private static void RemoveRecipe(List<string> defNames)
		{
			List<RecipeDef> list = DefDatabase<RecipeDef>.AllDefsListForReading.Where((RecipeDef d) => defNames.Contains(((Def)d).defName)).ToList();
			foreach (RecipeDef item in list)
			{
				ProjectRimFactory_ModSettings.defTracker.AddDefaultValue(((Def)item).defName, "RecipeDef", item);
			}
			DefDatabase<RecipeDef>.AllDefsListForReading.RemoveAll((RecipeDef d) => defNames.Contains(((Def)d).defName));
			ClearRecipesCache();
		}

		private static void RemoveTrader(List<string> defNames)
		{
			List<TraderKindDef> list = DefDatabase<TraderKindDef>.AllDefsListForReading.Where((TraderKindDef d) => defNames.Contains(((Def)d).defName)).ToList();
			foreach (TraderKindDef item in list)
			{
				ProjectRimFactory_ModSettings.defTracker.AddDefaultValue(((Def)item).defName, "trader", item);
			}
			DefDatabase<TraderKindDef>.AllDefsListForReading.RemoveAll((TraderKindDef d) => defNames.Contains(((Def)d).defName));
		}

		public static void RemoveScenario(List<string> defNames)
		{
			List<ScenarioDef> list = DefDatabase<ScenarioDef>.AllDefsListForReading.Where((ScenarioDef d) => defNames.Contains(((Def)d).defName)).ToList();
			foreach (ScenarioDef item in list)
			{
				ProjectRimFactory_ModSettings.defTracker.AddDefaultValue(((Def)item).defName, "scenario", item);
			}
			DefDatabase<ScenarioDef>.AllDefsListForReading.RemoveAll((ScenarioDef d) => defNames.Contains(((Def)d).defName));
		}

		private static void RemoveResearch(List<string> defNames)
		{
			List<ResearchProjectDef> list = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where((ResearchProjectDef d) => d?.prerequisites?.Where((ResearchProjectDef c) => defNames.Contains(((Def)c).defName)).Any() == true).ToList();
			foreach (ResearchProjectDef item in list)
			{
				ProjectRimFactory_ModSettings.defTracker.AddDefaultValue(((Def)item).defName, "prerequisitesBase", item);
				ProjectRimFactory_ModSettings.defTracker.AddDefaultValue(((Def)item).defName, "prerequisites", item.prerequisites.ToList());
				item.prerequisites.RemoveAll((ResearchProjectDef c) => defNames.Contains(((Def)c).defName));
			}
			List<ResearchProjectDef> list2 = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where((ResearchProjectDef d) => defNames.Contains(((Def)d).defName)).ToList();
			foreach (ResearchProjectDef item2 in list2)
			{
				ProjectRimFactory_ModSettings.defTracker.AddDefaultValue(((Def)item2).defName, "res", item2);
				RemoveDefFromUse(item2);
			}
			List<ResearchProjectDef> list3 = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(delegate(ResearchProjectDef d)
			{
				List<ThingDef> requiredResearchFacilities = d.requiredResearchFacilities;
				return requiredResearchFacilities != null && GenCollection.Any<ThingDef>(requiredResearchFacilities, (Predicate<ThingDef>)((ThingDef f) => ((Def)f).defName == "PRF_ResearchTerminal"));
			}).ToList();
			foreach (ResearchProjectDef item3 in list3)
			{
				ProjectRimFactory_ModSettings.defTracker.AddDefaultValue(((Def)item3).defName, "requiredResearchFacilities", item3.requiredResearchFacilities.ToList());
				ProjectRimFactory_ModSettings.defTracker.AddDefaultValue(((Def)item3).defName, "requiredResearchFacilitiesBase", item3);
				item3.requiredResearchFacilities.RemoveAll((ThingDef d) => ((Def)d).defName == "PRF_ResearchTerminal");
			}
			resetResearchManager_progress();
		}

		private static void resetResearchManager_progress()
		{
			Dictionary<ResearchProjectDef, float> dictionary = new Dictionary<ResearchProjectDef, float>();
			if (Current.Game?.researchManager != null)
			{
				dictionary = (Dictionary<ResearchProjectDef, float>)ReflectionUtility.ResearchManagerProgress.GetValue(Current.Game.researchManager);
			}
			List<ResearchProjectDef> list = ProjectRimFactory_ModSettings.defTracker.GetAllWithKeylet<ResearchProjectDef>("res").ToList();
			foreach (ResearchProjectDef item in list)
			{
				ProjectRimFactory_ModSettings.defTracker.AddDefaultValue(((Def)item).defName, "res", item);
				if (Current.Game?.researchManager != null)
				{
					dictionary.Remove(item);
				}
			}
			if (Current.Game?.researchManager != null)
			{
				ReflectionUtility.ResearchManagerProgress.SetValue(Current.Game.researchManager, dictionary);
			}
			ModSupportResetLayout();
		}

		private static void RemoveBuildableThingDefs(List<string> defNames)
		{
			List<ThingDef> list = DefDatabase<ThingDef>.AllDefsListForReading.Where((ThingDef d) => defNames.Contains(((Def)d).defName)).ToList();
			foreach (ThingDef item in list)
			{
				ProjectRimFactory_ModSettings.defTracker.AddDefaultValue(((Def)item).defName, "def", item);
				RemoveDefFromUse(item);
			}
			ArchitectMenu_ClearCache();
		}

		private static List<ThingDefCountClass> CostListCopy(List<ThingDefCountClass> datain)
		{
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Expected O, but got Unknown
			List<ThingDefCountClass> list = new List<ThingDefCountClass>();
			for (int i = 0; i < datain.Count; i++)
			{
				list.Add(new ThingDefCountClass(datain[i].thingDef, datain[i].count));
			}
			return list;
		}

		private static void RemoveComponents(List<string> defNames)
		{
			//IL_0266: Unknown result type (might be due to invalid IL or missing references)
			//IL_0270: Expected O, but got Unknown
			Regex regex = new Regex("^.+? x\\d+$", RegexOptions.Compiled);
			Dictionary<ThingDef, List<IngredientCount>> dictionary = new Dictionary<ThingDef, List<IngredientCount>>();
			List<RecipeDef> list = DefDatabase<RecipeDef>.AllDefsListForReading.Where((RecipeDef d) => (from c in d.products.Select((ThingDefCountClass s) => s.thingDef).ToList()
				where defNames.Contains(((Def)c).defName)
				select c).Any()).ToList();
			foreach (RecipeDef item in list)
			{
				if (!regex.IsMatch(((Def)item).label))
				{
					dictionary.Add(item.ProducedThingDef, item.ingredients);
				}
			}
			List<ThingDef> list2 = DefDatabase<ThingDef>.AllDefsListForReading.Where(delegate(ThingDef d)
			{
				bool? obj2;
				if (d == null)
				{
					obj2 = null;
				}
				else
				{
					List<ThingDefCountClass> costList = ((BuildableDef)d).costList;
					obj2 = ((costList != null) ? new bool?(GenCollection.Any<ThingDefCountClass>(costList, (Predicate<ThingDefCountClass>)((ThingDefCountClass c) => defNames.Contains(((Def)c.thingDef).defName)))) : ((bool?)null));
				}
				bool? flag = obj2;
				return flag == true;
			}).ToList();
			foreach (ThingDef item2 in list2)
			{
				ProjectRimFactory_ModSettings.defTracker.AddDefaultValue(((Def)item2).defName, "alterdDef", item2);
				ProjectRimFactory_ModSettings.defTracker.AddDefaultValue(((Def)item2).defName, "costList", CostListCopy(((BuildableDef)item2).costList));
				int num = 10;
				while (true)
				{
					num--;
					List<ThingDefCountClass> toRemove = ((BuildableDef)item2).costList.Where((ThingDefCountClass c) => defNames.Contains(((Def)c.thingDef).defName)).ToList();
					if (toRemove.Count <= 0 || num <= 0)
					{
						break;
					}
					foreach (ThingDefCountClass item3 in toRemove)
					{
						List<IngredientCount> list3 = dictionary[item3.thingDef];
						if (list3 == null)
						{
							continue;
						}
						foreach (IngredientCount ingredientCount in list3)
						{
							if (GenCollection.Any<ThingDefCountClass>(((BuildableDef)item2).costList, (Predicate<ThingDefCountClass>)((ThingDefCountClass d) => d.thingDef == ingredientCount.FixedIngredient)))
							{
								ThingDefCountClass obj = ((BuildableDef)item2).costList.First((ThingDefCountClass d) => d.thingDef == ingredientCount.FixedIngredient);
								obj.count += (int)ingredientCount.GetBaseCount() * item3.count;
							}
							else
							{
								((BuildableDef)item2).costList.Add(new ThingDefCountClass(ingredientCount.FixedIngredient, (int)ingredientCount.GetBaseCount() * item3.count));
							}
						}
					}
					((BuildableDef)item2).costList.RemoveAll((ThingDefCountClass d) => toRemove.Contains(d));
				}
			}
			ArchitectMenu_ClearCache();
		}

		public static void ArchitectMenu_ClearCache()
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Invalid comparison between Unknown and O
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c9: Expected O, but got Unknown
			string[] panels = new string[2] { "Industrial", "Power" };
			MainButtonDef architect = MainButtonDefOf.Architect;
			if ((object)(MainTabWindow_Architect)((architect != null) ? architect.TabWindow : null) == null)
			{
				return;
			}
			foreach (DesignationCategoryDef item in DefDatabase<DesignationCategoryDef>.AllDefs.Where((DesignationCategoryDef d) => d != null && panels.Contains(((Def)d).defName)))
			{
				((Editable)item).ResolveReferences();
			}
			typeof(MainTabWindow_Architect).GetMethod("CacheDesPanels", BindingFlags.Instance | BindingFlags.NonPublic).Invoke((object)(MainTabWindow_Architect)MainButtonDefOf.Architect.TabWindow, null);
			if (!ModLister.HasActiveModWithName("Architect Icons"))
			{
				return;
			}
			Type type = Type.GetType("ArchitectIcons.MainTabWindow_Architect, ArchitectIcons, Version=1.2.0.0, Culture=en, PublicKeyToken=null", throwOnError: false);
			if (type == null)
			{
				Log.Warning("Could not get type ArchitectIcons.MainTabWindow_Architect");
				return;
			}
			type.GetMethod("CacheDesPanels", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(DefDatabase<MainButtonDef>.AllDefs.Where((MainButtonDef i) => ((Def)i).defName == "Architect" && i.order == 1).First().TabWindow, null);
		}

		private static void RemoveDefFromUse(ThingDef def)
		{
			typeof(DefDatabase<>).MakeGenericType(typeof(ThingDef)).GetMethod("Remove", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, new object[1] { def });
			DefDatabase<ThingDef>.AllDefsListForReading.Remove(def);
			ThingDef named = DefDatabase<ThingDef>.GetNamed(((Def)def).defName, false);
			if (named != null)
			{
				Log.Error("Tried to remove " + ((Def)def).defName + " from DefDatabase, but it's still there???");
			}
			DesignationCategoryDef designationCategory = ((BuildableDef)def).designationCategory;
			if (designationCategory != null)
			{
				designationCategory.AllResolvedDesignators.RemoveAll(delegate(Designator x)
				{
					Designator_Build val = (Designator_Build)(object)((x is Designator_Build) ? x : null);
					return val != null && (object)((Designator_Place)val).PlacingDef == def;
				});
			}
		}

		private static void RemoveDefFromUse(ResearchProjectDef def)
		{
			typeof(DefDatabase<>).MakeGenericType(typeof(ResearchProjectDef)).GetMethod("Remove", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, new object[1] { def });
			DefDatabase<ResearchProjectDef>.AllDefsListForReading.Remove(def);
			ResearchProjectDef named = DefDatabase<ResearchProjectDef>.GetNamed(((Def)def).defName, false);
			if (named != null)
			{
				Log.Error("Tried to remove " + ((Def)def).defName + " from DefDatabase, but it's stil there???");
			}
		}
	}
	public class ProjectRimFactory_ModComponent : Mod
	{
		public static MethodInfo ModSupport_RrimFridge_GetFridgeCache;

		public static MethodInfo ModSupport_RrimFridge_HasFridgeAt;

		public static bool ModSupport_RrimFrige_Dispenser;

		public static MethodInfo ModSupport_ReserchPal_ResetLayout;

		public static MethodInfo ModSupport_ReserchPowl_ResetLayout;

		public static MethodInfo ModSupport_ResearchTreeContinued_ResetLayout;

		public static bool ModSupport_ReserchPal;

		public static bool ModSupport_ReserchPowl;

		public static bool ModSupport_ResearchTreeContinued;

		public static bool ModSupport_SeedsPlease;

		public static bool ModSupport_SeedsPleaseLite;

		public static bool ModSupport_VEF_DualCropExtension;

		private static MethodInfo ModSupport_SOS2_MoveShipFlagGetter;

		public static List<SpecialSculpture> availableSpecialSculptures;

		public static bool ModSupport_SOS2_MoveShip
		{
			get
			{
				if ((object)ModSupport_SOS2_MoveShipFlagGetter == null)
				{
					return false;
				}
				return (bool)ModSupport_SOS2_MoveShipFlagGetter.Invoke(null, new object[0]);
			}
		}

		public Harmony HarmonyInstance { get; private set; }

		public ProjectRimFactory_ModSettings Settings { get; private set; }

		public ProjectRimFactory_ModComponent(ModContentPack content)
			: base(content)
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Expected O, but got Unknown
			try
			{
				ProjectRimFactory_ModSettings.LoadXml(content);
				Settings = ((Mod)this).GetSettings<ProjectRimFactory_ModSettings>();
				HarmonyInstance = new Harmony("com.spdskatr.projectrimfactory");
				HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
				Log.Message("Project RimFactory Core " + typeof(ProjectRimFactory_ModComponent).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion + " - Harmony patches successful");
				availableSpecialSculptures = SpecialSculpture.LoadAvailableSpecialSculptures(content);
				ConditionalPatchHelper.InitHarmony(HarmonyInstance);
				ConditionalPatchHelper.PatchReachabilityCanReach.PatchHandler(ProjectRimFactory_ModSettings.PRF_Patch_Reachability_CanReach);
			}
			catch (Exception ex)
			{
				Log.Error("Project RimFactory Core :: Caught exception: " + ex);
			}
			try
			{
				LoadModSupport();
			}
			catch (Exception ex2)
			{
				Log.Error("Project RimFactory Core :: LoadModSupport Caught exception: " + ex2);
			}
		}

		private void LoadModSupport()
		{
			//IL_0161: Unknown result type (might be due to invalid IL or missing references)
			//IL_016d: Expected O, but got Unknown
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Expected O, but got Unknown
			//IL_0369: Unknown result type (might be due to invalid IL or missing references)
			//IL_0375: Expected O, but got Unknown
			//IL_0380: Unknown result type (might be due to invalid IL or missing references)
			//IL_038d: Expected O, but got Unknown
			if (ModLister.HasActiveModWithName("RimFridge: Now with Shelves!"))
			{
				ModSupport_RrimFridge_GetFridgeCache = AccessTools.Method("RimFridge.FridgeCache:GetFridgeCache", (Type[])null, (Type[])null);
				ModSupport_RrimFridge_HasFridgeAt = AccessTools.Method("RimFridge.FridgeCache:HasFridgeAt", (Type[])null, (Type[])null);
				if (ModSupport_RrimFridge_GetFridgeCache != null && ModSupport_RrimFridge_HasFridgeAt != null)
				{
					Log.Message("Project Rimfactory - added Support for shared Nutrient Dispenser with RimFridge Updated");
					ModSupport_RrimFrige_Dispenser = true;
				}
				else
				{
					Log.Warning("Project Rimfactory - Failed to add Support for shared Nutrient Dispenser with RimFridge Updated");
				}
				if (!ModLister.HasActiveModWithName("Simple Utilities: Fridge"))
				{
					MethodBase methodBase = AccessTools.Method("RimFridge.CompRefrigerator:CompTickRare", (Type[])null, (Type[])null);
					if (methodBase != null)
					{
						MethodInfo method = typeof(Patch_CompRefrigerator_CompTickRare).GetMethod("Postfix");
						HarmonyInstance.Patch(methodBase, (HarmonyMethod)null, new HarmonyMethod(method), (HarmonyMethod)null, (HarmonyMethod)null);
						Log.Message("Project Rimfactory - added Support for Fridge DSU Power using RimFridge");
					}
					else
					{
						Log.Warning("Project Rimfactory - Failed to add Support for Fridge DSU Power using RimFridge");
					}
				}
			}
			if (ModLister.HasActiveModWithName("Simple Utilities: Fridge"))
			{
				MethodBase methodBase2 = null;
				Type type = Type.GetType("SimpleFridge.FridgeUtility, SimpleUtilitiesFridge", throwOnError: false);
				if (type != null)
				{
					methodBase2 = AccessTools.Method(type, "Tick", (Type[])null, (Type[])null);
				}
				if (methodBase2 != null)
				{
					MethodInfo method2 = typeof(Patch_FridgeUtility_Tick).GetMethod("Postfix");
					HarmonyInstance.Patch(methodBase2, (HarmonyMethod)null, new HarmonyMethod(method2), (HarmonyMethod)null, (HarmonyMethod)null);
					Log.Message("Project Rimfactory - added Support for Fridge DSU Power using Simple Utilities: Fridge");
				}
				else
				{
					Log.Warning("Project Rimfactory - Failed to add Support for Fridge DSU Power using Simple Utilities: Fridge");
				}
			}
			if (ModLister.HasActiveModWithName("ResearchPal - Forked"))
			{
				ModSupport_ReserchPal_ResetLayout = AccessTools.Method("ResearchPal.Tree:ResetLayout", (Type[])null, (Type[])null);
				if (ModSupport_ReserchPal_ResetLayout != null)
				{
					Log.Message("Project Rimfactory - added Support for ResearchPal when using Lite Mode");
					ModSupport_ReserchPal = true;
				}
				else
				{
					Log.Warning("Project Rimfactory - Failed to added Support for ResearchPal when using Lite Mode");
				}
			}
			else if (ModLister.HasActiveModWithName("ResearchPowl"))
			{
				ModSupport_ReserchPowl_ResetLayout = AccessTools.Method("ResearchPowl.Tree:ResetLayout", (Type[])null, (Type[])null);
				if (ModSupport_ReserchPowl_ResetLayout != null)
				{
					Log.Message("Project Rimfactory - added Support for ResearchPowl when using Lite Mode");
					ModSupport_ReserchPowl = true;
				}
				else
				{
					Log.Warning("Project Rimfactory - Failed to added Support for ResearchPowl when using Lite Mode");
				}
			}
			else if (ModLister.HasActiveModWithName("Research Tree (Continued)"))
			{
				ModSupport_ResearchTreeContinued_ResetLayout = AccessTools.Method("FluffyResearchTree.Tree:Reset", (Type[])null, (Type[])null);
				if (ModSupport_ResearchTreeContinued_ResetLayout != null)
				{
					Log.Message("Project Rimfactory - added Support for Research Tree (Continued) when using Lite Mode");
					ModSupport_ResearchTreeContinued = true;
				}
				else
				{
					Log.Warning("Project Rimfactory - Failed to added Support for Research Tree (Continued) when using Lite Mode");
				}
			}
			if (ModLister.HasActiveModWithName("SeedsPlease"))
			{
				Log.Warning("PRF - SeedsPlease Detected - Compatibility probably requires an Update. Please Notify the PRF Team");
				ModSupport_SeedsPlease = true;
			}
			if (ModLister.HasActiveModWithName("SeedsPlease: Lite"))
			{
				ModSupport_SeedsPleaseLite = true;
			}
			if (ModLister.HasActiveModWithName("Vanilla Expanded Framework"))
			{
				ModSupport_VEF_DualCropExtension = true;
			}
			if (ModLister.HasActiveModWithName("QualityBuilder"))
			{
				MethodBase methodBase3 = AccessTools.Method("QualityBuilder.QualityBuilder:pawnCanConstruct", (Type[])null, (Type[])null);
				MethodBase methodBase4 = AccessTools.Method("QualityBuilder.QualityBuilder:getPawnConstructionSkill", (Type[])null, (Type[])null);
				if (methodBase3 != null && methodBase4 != null)
				{
					MethodInfo method3 = typeof(Patch_QualityBuilder_PawnCanConstruct).GetMethod("Postfix");
					MethodInfo method4 = typeof(Patch_QualityBuilder_GetPawnConstructionSkill).GetMethod("Prefix");
					HarmonyInstance.Patch(methodBase3, (HarmonyMethod)null, new HarmonyMethod(method3), (HarmonyMethod)null, (HarmonyMethod)null);
					HarmonyInstance.Patch(methodBase4, new HarmonyMethod(method4), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Log.Message("Project Rimfactory - Added Support for QualityBuilder");
				}
				else
				{
					Log.Warning("Project Rimfactory - Failed to add Support for QualityBuilder");
				}
			}
			if (ModLister.HasActiveModWithName("Save Our Ship 2"))
			{
				try
				{
					Type type2 = Type.GetType("SaveOurShip2.ShipInteriorMod2, ShipsHaveInsides", throwOnError: true);
					ModSupport_SOS2_MoveShipFlagGetter = type2.GetMethod("get_MoveShipFlag", BindingFlags.Static | BindingFlags.Public);
					Log.Message("Project Rimfactory - Added Support for Save Our Ship 2");
				}
				catch (Exception ex)
				{
					Log.Warning("Project Rimfactory - ailed to add Support for Save Our Ship 2");
					Log.Error(ex.Message);
				}
			}
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			Settings.DoWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("ProjectRimFactoryModName"));
		}

		public override void WriteSettings()
		{
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			Settings.Apply();
			((ModSettings)Settings).Write();
			if (Settings.RequireReboot)
			{
				Find.WindowStack.Add((Window)(object)Dialog_MessageBox.CreateConfirmation(Translator.Translate("PRF.Settings.RequireReboot"), (Action)delegate
				{
					GenCommandLine.Restart();
				}, false, (string)null, (WindowLayer)1));
			}
		}
	}
	public class ProjectRimFactory_ModSettings : ModSettings
	{
		public static bool allowAllMultipleSpecialSculptures;

		public static Dictionary<string, int> maxNumbersSpecialSculptures;

		public static DefChangeTracker defTracker = new DefChangeTracker();

		private static ContainerRow root;

		public static bool PRF_LiteMode = false;

		private static bool PRF_LiteMode_last = false;

		public static bool PRF_Patch_Reachability_CanReach = false;

		public static bool PRF_UseFuzzySearch = true;

		public static bool PRF_PlaceConveyorCable = true;

		private Vector2 scrollPosition;

		private float lastHeight = 1000f;

		public bool RequireReboot => root.RequireReboot;

		public virtual IEnumerable<PatchOperation> Patches => root.GetValidPatches();

		public static void LoadXml(ModContentPack content)
		{
			root = ParseSettingRows(content);
			root.Initialize();
		}

		private static void AddHeader(Listing_Standard list, string header)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			Rect rect = ((Listing)list).GetRect(30f, 1f);
			Widgets.DrawRectFast(rect, Color.gray, (GUIContent)null);
			GameFont font = Text.Font;
			TextAnchor anchor = Text.Anchor;
			Text.Font = (GameFont)2;
			Text.Anchor = (TextAnchor)4;
			Widgets.Label(rect, header);
			Text.Font = font;
			Text.Anchor = anchor;
			((Listing)list).Gap(12f);
		}

		private static void CSharpSettings(Listing_Standard list)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0139: Unknown result type (might be due to invalid IL or missing references)
			//IL_0154: Unknown result type (might be due to invalid IL or missing references)
			//IL_0159: Unknown result type (might be due to invalid IL or missing references)
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_016f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0175: Unknown result type (might be due to invalid IL or missing references)
			//IL_017a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0185: Unknown result type (might be due to invalid IL or missing references)
			//IL_018b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0167: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
			AddHeader(list, TaggedString.op_Implicit(Translator.Translate("PRF_Settings_C_Lite_Header")));
			Rect rect = ((Listing)list).GetRect(20f, 1f);
			if (Mouse.IsOver(rect))
			{
				Widgets.DrawHighlight(rect);
			}
			TooltipHandler.TipRegion(rect, TipSignal.op_Implicit(Translator.Translate("PRF_Settings_C_Lite_ToolTip")));
			Widgets.CheckboxLabeled(rect, TaggedString.op_Implicit(Translator.Translate("PRF_Settings_C_Lite_Label")), ref PRF_LiteMode, false, (Texture2D)null, (Texture2D)null, false, false);
			((Listing)list).Gap(12f);
			if (PRF_LiteMode != PRF_LiteMode_last)
			{
				PRF_CustomizeDefs.ToggleLiteMode(PRF_LiteMode);
			}
			PRF_LiteMode_last = PRF_LiteMode;
			AddHeader(list, TaggedString.op_Implicit(Translator.Translate("PRF_Settings_C_Patches_Header")));
			rect = ((Listing)list).GetRect(20f, 1f);
			if (Mouse.IsOver(rect))
			{
				Widgets.DrawHighlight(rect);
			}
			TooltipHandler.TipRegion(rect, TipSignal.op_Implicit(Translator.Translate("PRF_Settings_C_Patches_Reachability_CanReach_ToolTip")));
			Widgets.CheckboxLabeled(rect, TaggedString.op_Implicit(Translator.Translate("PRF_Settings_C_Patches_Reachability_CanReach")), ref PRF_Patch_Reachability_CanReach, false, (Texture2D)null, (Texture2D)null, false, false);
			((Listing)list).Gap(12f);
			ConditionalPatchHelper.PatchReachabilityCanReach.PatchHandler(PRF_Patch_Reachability_CanReach);
			AddHeader(list, TaggedString.op_Implicit(Translator.Translate("PRF_Settings_GeneralOptions_Header")));
			rect = ((Listing)list).GetRect(20f, 1f);
			if (Mouse.IsOver(rect))
			{
				Widgets.DrawHighlight(rect);
			}
			TooltipHandler.TipRegion(rect, TipSignal.op_Implicit(Translator.Translate("PRF_Settings_GeneralOptions_DSU_UseFuzzySearch_ToolTip")));
			Widgets.CheckboxLabeled(rect, TaggedString.op_Implicit(Translator.Translate("PRF_Settings_GeneralOptions_DSU_UseFuzzySearch")), ref PRF_UseFuzzySearch, false, (Texture2D)null, (Texture2D)null, false, false);
			((Listing)list).Gap(12f);
			rect = ((Listing)list).GetRect(20f, 1f);
			if (Mouse.IsOver(rect))
			{
				Widgets.DrawHighlight(rect);
			}
			TooltipHandler.TipRegion(rect, TipSignal.op_Implicit(Translator.Translate("PRF_Settings_GeneralOptions_PlaceConveyorCable_ToolTip")));
			Widgets.CheckboxLabeled(rect, TaggedString.op_Implicit(Translator.Translate("PRF_Settings_GeneralOptions_PlaceConveyorCable")), ref PRF_PlaceConveyorCable, false, (Texture2D)null, (Texture2D)null, false, false);
			((Listing)list).Gap(12f);
		}

		private static ContainerRow ParseSettingRows(ModContentPack content)
		{
			ContainerRow containerRow = new ContainerRow();
			LoadableXmlAsset val = DirectXmlLoader.XmlAssetsInModFolder(content, "Settings", (List<string>)null)?.Where((LoadableXmlAsset x) => x.name == "Settings.xml")?.ToList().FirstOrDefault();
			if (val == null || val.xmlDoc == null)
			{
				Log.Error("Settings/Settings.xml not found or invalid xml.");
				return containerRow;
			}
			XmlElement documentElement = val.xmlDoc.DocumentElement;
			if (documentElement.Name != "SettingRows")
			{
				Log.Error("SettingRows not found. name=" + documentElement.Name);
				return containerRow;
			}
			containerRow.Rows.LoadDataFromXmlCustom(documentElement);
			return containerRow;
		}

		public override void ExposeData()
		{
			((ModSettings)this).ExposeData();
			root.ExposeData();
			Scribe_Values.Look<Debug.Flag>(ref Debug.activeFlags, "debugFlags", (Debug.Flag)0, false);
			Scribe_Values.Look<bool>(ref PRF_LiteMode, "PRF_LiteMode", false, false);
			Scribe_Values.Look<bool>(ref PRF_Patch_Reachability_CanReach, "PRF_Patch_Reachability_CanReach", false, false);
			Scribe_Values.Look<bool>(ref PRF_UseFuzzySearch, "PRF_UseFuzzySearch", true, false);
			Scribe_Values.Look<bool>(ref PRF_PlaceConveyorCable, "PRF_PlaceConveyorCable", true, false);
			PRF_LiteMode_last = PRF_LiteMode;
		}

		public void DoWindowContents(Rect inRect)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Expected O, but got Unknown
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(inRect);
			((Rect)(ref val)).yMin = ((Rect)(ref val)).yMin + 20f;
			((Rect)(ref val)).yMax = ((Rect)(ref val)).yMax - 20f;
			((Rect)(ref val)).xMin = ((Rect)(ref val)).xMin + 20f;
			((Rect)(ref val)).xMax = ((Rect)(ref val)).xMax - 20f;
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(0f, 0f, ((Rect)(ref val)).width - 16f, lastHeight);
			Widgets.BeginScrollView(val, ref scrollPosition, val2, true);
			Listing_Standard val3 = new Listing_Standard();
			((Listing)val3).Begin(val2);
			CSharpSettings(val3);
			root.Draw(val3);
			((Listing)val3).End();
			Widgets.EndScrollView();
			lastHeight = ((Listing)val3).CurHeight + 16f;
		}

		public void Apply()
		{
			root.Apply();
		}
	}
	public interface ISettingRow
	{
		bool RequireReboot { get; }

		void Draw(Listing_Standard list);

		void ExposeData();

		void Apply();

		bool Initialize();

		IEnumerable<PatchOperation> GetValidPatches();
	}
	public abstract class SettingRow : ISettingRow
	{
		public bool RequireReboot => false;

		public void Apply()
		{
		}

		public abstract void Draw(Listing_Standard list);

		public void ExposeData()
		{
		}

		public IEnumerable<PatchOperation> GetValidPatches()
		{
			return Enumerable.Empty<PatchOperation>();
		}

		public bool Initialize()
		{
			return true;
		}
	}
	public abstract class SettingItemBase : ISettingRow
	{
		public string key;

		public string label;

		public string description;

		public virtual bool RequireReboot { get; protected set; } = false;

		public abstract void Draw(Listing_Standard list);

		public abstract void ExposeData();

		public abstract void Apply();

		public virtual bool Initialize()
		{
			return true;
		}

		public abstract IEnumerable<PatchOperation> GetValidPatches();
	}
	public abstract class SettingItem : SettingItemBase
	{
		public override IEnumerable<PatchOperation> GetValidPatches()
		{
			return Enumerable.Empty<PatchOperation>();
		}
	}
	public abstract class PatchSettingItem : SettingItemBase
	{
		public PatchElement Patch;

		protected IEnumerable<PatchOperation> Patches
		{
			get
			{
				IEnumerable<PatchOperation> enumerable = Patch?.Patches;
				return enumerable ?? Enumerable.Empty<PatchOperation>();
			}
		}
	}
	public class PatchElement
	{
		public XmlNode rootNode;

		public List<PatchOperation> Patches { get; private set; }

		public void LoadDataFromXmlCustom(XmlNode xmlRoot)
		{
			rootNode = xmlRoot;
			Patches = LoadDataFromXml(xmlRoot);
		}

		public static List<PatchOperation> LoadDataFromXml(XmlNode xmlRoot)
		{
			return (from XmlNode n in xmlRoot.ChildNodes
				where n.NodeType == XmlNodeType.Element
				select n as XmlElement into e
				where e != null
				where e.Name == "Operation"
				select DirectXmlToObject.ObjectFromXml<PatchOperation>((XmlNode)e, false)).ToList();
		}
	}
	public abstract class ContainerRowBase : ISettingRow
	{
		protected List<ISettingRow> rows = new List<ISettingRow>();

		public bool RequireReboot => GenCollection.Any<ISettingRow>(rows, (Predicate<ISettingRow>)((ISettingRow r) => r.RequireReboot));

		public void Apply()
		{
			rows.ForEach(delegate(ISettingRow r)
			{
				r.Apply();
			});
		}

		public abstract void Draw(Listing_Standard list);

		public void ExposeData()
		{
			rows.ForEach(delegate(ISettingRow r)
			{
				r.ExposeData();
			});
		}

		public IEnumerable<PatchOperation> GetValidPatches()
		{
			return rows.SelectMany((ISettingRow r) => r.GetValidPatches());
		}

		public abstract bool Initialize();
	}
	public class RowsElement
	{
		public List<ISettingRow> rows;

		public void LoadDataFromXmlCustom(XmlNode xmlRoot)
		{
			rows = LoadDataFromXml(xmlRoot);
		}

		public static List<ISettingRow> LoadDataFromXml(XmlNode xmlRoot)
		{
			return (from XmlNode n in xmlRoot.ChildNodes
				where n.NodeType == XmlNodeType.Element
				select n as XmlElement into e
				where e != null
				where e.Name == "Row"
				select DirectXmlToObject.ObjectFromXml<ISettingRow>((XmlNode)e, false)).ToList();
		}
	}
	public class ContainerRow : ContainerRowBase
	{
		public RowsElement Rows = new RowsElement();

		public Color backgroundColor = Color.clear;

		private float lastHeight = 100000f;

		public override void Draw(Listing_Standard list)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Expected O, but got Unknown
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			Rect rect = ((Listing)list).GetRect(lastHeight, 1f);
			if (backgroundColor != Color.clear)
			{
				Widgets.DrawRectFast(rect, backgroundColor, (GUIContent)null);
			}
			Listing_Standard child = new Listing_Standard();
			((Listing)child).Begin(rect);
			rows.ForEach(delegate(ISettingRow r)
			{
				r.Draw(child);
			});
			((Listing)child).End();
			lastHeight = ((Listing)child).CurHeight;
			((Listing)list).Gap(((Listing)list).verticalSpacing);
		}

		public override bool Initialize()
		{
			if (Rows == null || Rows.rows == null)
			{
				return false;
			}
			rows = Rows.rows.Where((ISettingRow r) => r.Initialize()).ToList();
			return rows.Count > 0;
		}
	}
	public class SplitRow : ContainerRowBase
	{
		public float rate = 0.5f;

		public ISettingRow LeftRow;

		public ISettingRow RightRow;

		private float lastHeight = 300f;

		public Color leftBackgroundColor = Color.clear;

		public Color rightBackgroundColor = Color.clear;

		public override void Draw(Listing_Standard list)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Expected O, but got Unknown
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Expected O, but got Unknown
			Rect rect = ((Listing)list).GetRect(lastHeight, 1f);
			var list2 = new[]
			{
				new
				{
					Row = LeftRow,
					List = new Listing_Standard(),
					Rect = GenUI.LeftPart(rect, rate),
					BGColor = leftBackgroundColor
				},
				new
				{
					Row = RightRow,
					List = new Listing_Standard(),
					Rect = GenUI.RightPart(rect, 1f - rate),
					BGColor = rightBackgroundColor
				}
			}.ToList();
			list2.ForEach(s =>
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Unknown result type (might be due to invalid IL or missing references)
				//IL_001d: Unknown result type (might be due to invalid IL or missing references)
				if (s.BGColor != Color.clear)
				{
					Widgets.DrawRectFast(s.Rect, s.BGColor, (GUIContent)null);
				}
				((Listing)s.List).Begin(s.Rect);
				s.Row.Draw(s.List);
				((Listing)s.List).End();
			});
			lastHeight = list2.Select(s => ((Listing)s.List).CurHeight).Max();
			((Listing)list).Gap(((Listing)list).verticalSpacing);
		}

		public override bool Initialize()
		{
			rows = new ISettingRow[2] { LeftRow, RightRow }.Where((ISettingRow r) => r.Initialize()).ToList();
			return rows.Count > 0;
		}
	}
	public class TextRow : SettingRow
	{
		public GameFont font = (GameFont)1;

		public TextAnchor anchor = (TextAnchor)3;

		public string text = "";

		public float height;

		public Color backgroundColor = Color.clear;

		public bool noTranslate = false;

		public override void Draw(Listing_Standard list)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_009b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			GameFont val = Text.Font;
			TextAnchor val2 = Text.Anchor;
			try
			{
				Text.Font = font;
				Text.Anchor = anchor;
				float num = height;
				TaggedString val3 = Translator.Translate(text);
				if (num == 0f)
				{
					num = Text.CalcHeight(TaggedString.op_Implicit(val3), ((Listing)list).ColumnWidth);
				}
				Rect rect = ((Listing)list).GetRect(num, 1f);
				if (backgroundColor != Color.clear)
				{
					Widgets.DrawRectFast(rect, backgroundColor, (GUIContent)null);
				}
				TaggedString val4 = Translator.Translate(text);
				if (noTranslate)
				{
					val4 = TaggedString.op_Implicit(text);
				}
				Widgets.Label(rect, val4);
				((Listing)list).Gap(((Listing)list).verticalSpacing);
			}
			finally
			{
				Text.Font = val;
				Text.Anchor = val2;
			}
		}
	}
	public class ImageRow : SettingRow
	{
		public string texPath;

		public float height;

		public Color backgroundColor = Color.clear;

		public override void Draw(Listing_Standard list)
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			Texture2D val = ContentFinder<Texture2D>.Get(texPath, true);
			float num = height;
			if (num == 0f)
			{
				num = ((Texture)val).height;
			}
			Rect rect = ((Listing)list).GetRect(num, 1f);
			if (backgroundColor != Color.clear)
			{
				Widgets.DrawRectFast(rect, backgroundColor, (GUIContent)null);
			}
			Widgets.DrawTextureFitted(rect, (Texture)(object)val, 1f, 1f);
			((Listing)list).Gap(((Listing)list).verticalSpacing);
		}
	}
	public class GapLineRow : SettingRow
	{
		public float height = 12f;

		public Color color = Color.clear;

		public override void Draw(Listing_Standard list)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			Color val = GUI.color;
			try
			{
				if (color != Color.clear)
				{
					GUI.color = color;
				}
				if (height != 0f)
				{
					((Listing)list).GapLine(height);
				}
			}
			finally
			{
				GUI.color = val;
			}
		}
	}
	public class GapRow : SettingRow
	{
		public float height = 12f;

		public override void Draw(Listing_Standard list)
		{
			if (height != 0f)
			{
				((Listing)list).Gap(height);
			}
		}
	}
	public class PatchItem : PatchSettingItem
	{
		private bool checkOn = false;

		private bool currentCheckOn;

		public override IEnumerable<PatchOperation> GetValidPatches()
		{
			return checkOn ? base.Patches : Enumerable.Empty<PatchOperation>();
		}

		public override void Apply()
		{
			if (currentCheckOn != checkOn)
			{
				RequireReboot = true;
				checkOn = currentCheckOn;
			}
		}

		public override void Draw(Listing_Standard list)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			list.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate(label)), ref currentCheckOn, TaggedString.op_Implicit(Translator.Translate(description)), 0f, 1f);
		}

		public override void ExposeData()
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Invalid comparison between Unknown and I4
			Scribe_Values.Look<bool>(ref checkOn, key, false, false);
			if ((int)Scribe.mode != 1)
			{
				currentCheckOn = checkOn;
			}
		}
	}
	public abstract class PatchValueItem : PatchSettingItem
	{
		public bool checkOn = false;

		protected bool currentCheckOn;

		protected List<PatchOperation> replaced;

		protected abstract string ReplaceText { get; }

		protected IEnumerable<PatchOperation> ReplacedPatch
		{
			get
			{
				if (replaced == null)
				{
					string xml = Patch.rootNode.OuterXml.Replace("${value}", SecurityElement.Escape(ReplaceText));
					XmlDocument xmlDocument = new XmlDocument();
					xmlDocument.LoadXml(xml);
					List<PatchOperation> list = PatchElement.LoadDataFromXml(xmlDocument.FirstChild);
					replaced = PatchElement.LoadDataFromXml(xmlDocument.FirstChild);
				}
				return replaced;
			}
		}

		public override void Apply()
		{
			if (checkOn != currentCheckOn)
			{
				RequireReboot = true;
				checkOn = currentCheckOn;
			}
		}

		public override void ExposeData()
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Invalid comparison between Unknown and I4
			Scribe_Values.Look<bool>(ref checkOn, key + "__check", false, false);
			if ((int)Scribe.mode != 1)
			{
				currentCheckOn = checkOn;
			}
		}

		public override IEnumerable<PatchOperation> GetValidPatches()
		{
			return checkOn ? ReplacedPatch : Enumerable.Empty<PatchOperation>();
		}
	}
	public abstract class PatchValueItem<T> : PatchValueItem
	{
		public T value;

		protected T currentValue;

		protected override string ReplaceText => value.ToString();

		public override void Apply()
		{
			base.Apply();
			if (!object.Equals(value, currentValue))
			{
				RequireReboot = true;
				value = currentValue;
			}
		}

		public override void ExposeData()
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Invalid comparison between Unknown and I4
			base.ExposeData();
			Scribe_Values.Look<T>(ref value, key, default(T), false);
			if ((int)Scribe.mode != 1)
			{
				currentValue = value;
			}
		}

		public override bool Initialize()
		{
			currentValue = value;
			return base.Initialize();
		}
	}
	public class PatchTextValueItem : PatchValueItem<string>
	{
		public override void Draw(Listing_Standard list)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Expected O, but got Unknown
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			Rect rect = ((Listing)list).GetRect(Text.LineHeight, 1f);
			Rect val = GenUI.LeftHalf(rect);
			Widgets.Label(GenUI.LeftHalf(val), new GUIContent(TaggedString.op_Implicit(Translator.Translate(label)), TaggedString.op_Implicit(Translator.Translate(description))));
			Rect val2 = GenUI.RightHalf(val);
			Widgets.Checkbox(((Rect)(ref val2)).position, ref currentCheckOn, 24f, false, false, (Texture2D)null, (Texture2D)null);
			currentValue = Widgets.TextField(GenUI.RightHalf(rect), currentValue);
			((Listing)list).Gap(((Listing)list).verticalSpacing);
		}
	}
	public class PatchFloatValueItem : PatchValueItem<float>
	{
		public float minValue = 0f;

		public float maxValue = 100000f;

		public float roundTo = -1f;

		public override void Draw(Listing_Standard list)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Expected O, but got Unknown
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			Rect rect = ((Listing)list).GetRect(Text.LineHeight * 2f, 1f);
			Rect val = GenUI.LeftHalf(rect);
			Widgets.Label(GenUI.LeftHalf(val), new GUIContent(TaggedString.op_Implicit(Translator.Translate(label)), TaggedString.op_Implicit(Translator.Translate(description))));
			Rect val2 = GenUI.RightHalf(val);
			Widgets.Checkbox(((Rect)(ref val2)).position, ref currentCheckOn, 24f, false, false, (Texture2D)null, (Texture2D)null);
			Rect val3 = GenUI.RightHalf(rect);
			((Rect)(ref val3)).xMin = ((Rect)(ref val3)).xMin + 20f;
			((Rect)(ref val3)).xMax = ((Rect)(ref val3)).xMax - 20f;
			currentValue = Widgets.HorizontalSlider(val3, currentValue, minValue, maxValue, true, currentValue.ToString(), minValue.ToString(), maxValue.ToString(), roundTo);
			((Listing)list).Gap(((Listing)list).verticalSpacing);
		}
	}
	public class PatchIntValueItem : PatchValueItem<int>
	{
		public int minValue = 0;

		public int maxValue = 100000;

		public int roundTo = 1;

		public override void Draw(Listing_Standard list)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Expected O, but got Unknown
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			Rect rect = ((Listing)list).GetRect(Text.LineHeight * 2f, 1f);
			Rect val = GenUI.LeftHalf(rect);
			Widgets.Label(GenUI.LeftHalf(val), new GUIContent(TaggedString.op_Implicit(Translator.Translate(label)), TaggedString.op_Implicit(Translator.Translate(description))));
			Rect val2 = GenUI.RightHalf(val);
			Widgets.Checkbox(((Rect)(ref val2)).position, ref currentCheckOn, 24f, false, false, (Texture2D)null, (Texture2D)null);
			Rect val3 = GenUI.RightHalf(rect);
			((Rect)(ref val3)).xMin = ((Rect)(ref val3)).xMin + 20f;
			((Rect)(ref val3)).xMax = ((Rect)(ref val3)).xMax - 20f;
			currentValue = (int)Widgets.HorizontalSlider(val3, (float)currentValue, (float)minValue, (float)maxValue, true, currentValue.ToString(), minValue.ToString(), maxValue.ToString(), (float)roundTo);
			((Listing)list).Gap(((Listing)list).verticalSpacing);
		}
	}
	public class PatchBoolValueItem : PatchValueItem<bool>
	{
		public override void Draw(Listing_Standard list)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Expected O, but got Unknown
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			Rect rect = ((Listing)list).GetRect(Text.LineHeight, 1f);
			Rect val = GenUI.LeftHalf(rect);
			Widgets.Label(GenUI.LeftHalf(val), new GUIContent(TaggedString.op_Implicit(Translator.Translate(label)), TaggedString.op_Implicit(Translator.Translate(description))));
			Rect val2 = GenUI.RightHalf(val);
			Widgets.Checkbox(((Rect)(ref val2)).position, ref currentCheckOn, 24f, false, false, (Texture2D)null, (Texture2D)null);
			val2 = GenUI.RightHalf(rect);
			Widgets.Checkbox(((Rect)(ref val2)).position, ref currentValue, 24f, false, false, (Texture2D)null, (Texture2D)null);
			((Listing)list).Gap(((Listing)list).verticalSpacing);
		}
	}
	public class PatchEnumValueItem : PatchValueItem<int>
	{
		public Type enumType;

		public List<object> EnumValues => enumType.GetEnumValues().Cast<object>().ToList();

		protected override string ReplaceText => EnumValues[value].ToString();

		public override void Draw(Listing_Standard list)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Expected O, but got Unknown
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Expected O, but got Unknown
			Rect rect = ((Listing)list).GetRect(Text.LineHeight, 1f);
			Rect val = GenUI.LeftHalf(rect);
			Widgets.Label(GenUI.LeftHalf(val), new GUIContent(TaggedString.op_Implicit(Translator.Translate(label)), TaggedString.op_Implicit(Translator.Translate(description))));
			Rect val2 = GenUI.RightHalf(val);
			Widgets.Checkbox(((Rect)(ref val2)).position, ref currentCheckOn, 24f, false, false, (Texture2D)null, (Texture2D)null);
			if (Widgets.ButtonText(GenUI.RightHalf(rect), TaggedString.op_Implicit(Translator.Translate("PRF.Settings.Select") + " (") + EnumValues[currentValue]?.ToString() + ")", true, true, true, (TextAnchor?)null))
			{
				Find.WindowStack.Add((Window)new FloatMenu(enumType.GetEnumValues().Cast<object>().Select((Func<object, int, FloatMenuOption>)((object o, int idx) => new FloatMenuOption(o.ToString(), (Action)delegate
				{
					currentValue = idx;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)))
					.ToList()));
			}
			((Listing)list).Gap(((Listing)list).verticalSpacing);
		}

		public override bool Initialize()
		{
			if (!base.Initialize())
			{
				return false;
			}
			if (enumType == null || enumType.GetEnumValues().Cast<object>().Count() == 0)
			{
				Log.Error("invalid enumType on Settings.xml");
				return false;
			}
			return true;
		}
	}
	public class PatchSelectValueItem : PatchValueItem<int>
	{
		public List<string> options;

		protected override string ReplaceText => options[value].ToString();

		public override void Draw(Listing_Standard list)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Expected O, but got Unknown
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ee: Expected O, but got Unknown
			Rect rect = ((Listing)list).GetRect(Text.LineHeight, 1f);
			Rect val = GenUI.LeftHalf(rect);
			Widgets.Label(GenUI.LeftHalf(val), new GUIContent(TaggedString.op_Implicit(Translator.Translate(label)), TaggedString.op_Implicit(Translator.Translate(description))));
			Rect val2 = GenUI.RightHalf(val);
			Widgets.Checkbox(((Rect)(ref val2)).position, ref currentCheckOn, 24f, false, false, (Texture2D)null, (Texture2D)null);
			if (Widgets.ButtonText(GenUI.RightHalf(rect), TaggedString.op_Implicit(Translator.Translate("PRF.Settings.Select") + " (" + options[currentValue] + ")"), true, true, true, (TextAnchor?)null))
			{
				Find.WindowStack.Add((Window)new FloatMenu(((IEnumerable<string>)options).Select((Func<string, int, FloatMenuOption>)((string o, int idx) => new FloatMenuOption(o.ToString(), (Action)delegate
				{
					currentValue = idx;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0))).ToList()));
			}
			((Listing)list).Gap(((Listing)list).verticalSpacing);
		}

		public override bool Initialize()
		{
			if (!base.Initialize())
			{
				return false;
			}
			if (options == null || options.Count == 0)
			{
				Log.Error("invalid selectionList on Settings.xml");
				return false;
			}
			return true;
		}
	}
	[StaticConstructorOnStartup]
	public static class RecipeRegister
	{
		private static readonly List<RecipeDef> RecipeDefs;

		private static void PrepareMineables()
		{
			List<ThingDefCountClass> list = (from d in DefDatabase<ThingDef>.AllDefs.Where(delegate(ThingDef d)
				{
					int result;
					if (d.mineable)
					{
						BuildingProperties building = d.building;
						result = ((building != null && building.mineableThing != null && building.mineableYield > 0) ? 1 : 0);
					}
					else
					{
						result = 0;
					}
					return (byte)result != 0;
				})
				where d.building.isResourceRock || d.building.isNaturalRock
				select d).Select((Func<ThingDef, ThingDefCountClass>)((ThingDef d) => new ThingDefCountClass(d.building.mineableThing, d.building.mineableYield))).ToList();
			HashSet<ThingDef> mineablesSet = new HashSet<ThingDef>();
			GenCollection.AddRange<ThingDef>(mineablesSet, list.Select((ThingDefCountClass d) => d.thingDef));
			list.AddRange((from d in DefDatabase<ThingDef>.AllDefs
				where d.deepCommonality > 0f && d.deepCountPerPortion > 0
				where !mineablesSet.Contains(d)
				select d).Select((Func<ThingDef, ThingDefCountClass>)((ThingDef d) => new ThingDefCountClass(d, d.deepCountPerPortion))));
			List<RecipeDef> list2 = list.Select((ThingDefCountClass m) => CreateMiningRecipe(m, null)).ToList();
			List<string> list3 = new List<string>();
			for (int num = 0; num < list2.Count; num++)
			{
				if (!list3.Contains(((Def)list2[num]).defName))
				{
					RecipeDefs.Add(list2[num]);
					list3.Add(((Def)list2[num]).defName);
				}
			}
			DefDatabase<RecipeDef>.Add((IEnumerable<RecipeDef>)RecipeDefs);
		}

		static RecipeRegister()
		{
			RecipeDefs = new List<RecipeDef>();
			PrepareMineables();
			IEnumerable<ThingDef> enumerable = DefDatabase<ThingDef>.AllDefs.Where((ThingDef def) => def.thingClass == typeof(Building_Miner));
			foreach (ThingDef item in enumerable)
			{
				HandleMinerDef(item);
			}
		}

		private static void HandleMinerDef(ThingDef minerDef)
		{
			if (minerDef != null)
			{
				ModExtension_Miner modExt = ((Def)minerDef).GetModExtension<ModExtension_Miner>();
				List<RecipeDef> recipes = RecipeDefs.FindAll(delegate(RecipeDef r)
				{
					ModExtension_Miner modExtension_Miner = modExt;
					return modExtension_Miner == null || !modExtension_Miner.IsExcluded(r.products[0].thingDef);
				});
				minerDef.recipes = recipes;
				ReflectionUtility.AllRecipesCached.SetValue(minerDef, null);
			}
		}

		private static RecipeDef CreateMiningRecipe(ThingDefCountClass defCount, EffecterDef effecter)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ee: Expected O, but got Unknown
			//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Expected O, but got Unknown
			RecipeDef result = new RecipeDef
			{
				defName = "Recipe_AutoMachineTool_Mine_" + ((Def)defCount.thingDef).defName,
				label = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.AutoMiner.MineOre", NamedArgument.op_Implicit(((Def)defCount.thingDef).label))),
				jobString = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.AutoMiner.MineOre", NamedArgument.op_Implicit(((Def)defCount.thingDef).label))),
				workAmount = Mathf.Max(10000f, StatDefOf.MarketValue.Worker.GetValue(StatRequest.For((BuildableDef)(object)defCount.thingDef, (ThingDef)null, (QualityCategory)2), true) * (float)defCount.count * 1000f),
				workSpeedStat = StatDefOf.WorkToMake,
				efficiencyStat = StatDefOf.WorkToMake,
				workSkill = SkillDefOf.Mining,
				workSkillLearnFactor = 0f,
				products = new List<ThingDefCountClass>().Append(defCount),
				defaultIngredientFilter = new ThingFilter(),
				effectWorking = effecter,
				ingredients = new List<IngredientCount>()
			};
			if (!StatUtility.StatListContains(((BuildableDef)defCount.thingDef).statBases, StatDefOf.MarketValue) && defCount.count == 1)
			{
				defCount.thingDef.BaseMarketValue = 0f;
			}
			return result;
		}
	}
	internal class ReserchSkillModifier
	{
		private const int ResearchSkillLevelDefault = 10;

		public static int GetResearchSkillLevel(SkillDef _ = null)
		{
			if (PRFDefOf.PRF_AdvancedDrones.IsFinished)
			{
				return 20;
			}
			if (PRFDefOf.PRF_ImprovedDrones.IsFinished)
			{
				return 15;
			}
			if (PRFDefOf.PRF_BasicDrones.IsFinished)
			{
				return 10;
			}
			return 10;
		}

		public static int GetResearchSkillLevel(Type callerType, SkillDef _ = null)
		{
			if (callerType == typeof(Pawn_Drone) || callerType == typeof(Building_DroneStation))
			{
				if (PRFDefOf.PRF_AdvancedDrones.IsFinished)
				{
					return 20;
				}
				if (PRFDefOf.PRF_ImprovedDrones.IsFinished)
				{
					return 15;
				}
				if (PRFDefOf.PRF_BasicDrones.IsFinished)
				{
					return 10;
				}
			}
			return 10;
		}
	}
	internal static class SeedsPleaseSupport
	{
		public static CellRect InputArea(Building building)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			CellRect val = GenAdj.OccupiedRect((Thing)(object)building);
			return ((CellRect)(ref val)).ExpandedBy(1);
		}

		public static bool TryPlantNew(ThingDef plantDef, CellRect seedInputArea, Map map)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			if (ProjectRimFactory_ModComponent.ModSupport_SeedsPlease || ProjectRimFactory_ModComponent.ModSupport_SeedsPleaseLite)
			{
				return tryPlantNew(plantDef, seedInputArea, map);
			}
			return true;
		}

		private unsafe static bool tryPlantNew(ThingDef plantDef, CellRect seedInputArea, Map map)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			Thing val = null;
			Enumerator enumerator = ((CellRect)(ref seedInputArea)).GetEnumerator();
			try
			{
				while (((Enumerator)(ref enumerator)).MoveNext())
				{
					IntVec3 current = ((Enumerator)(ref enumerator)).Current;
					val = GridsUtility.GetThingList(current, map).Find((Thing t) => t.def == ((BuildableDef)plantDef).blueprintDef);
					if (val != null)
					{
						break;
					}
				}
			}
			finally
			{
				((IDisposable)(*(Enumerator*)(&enumerator))/*cast due to constrained. prefix*/).Dispose();
			}
			if (val == null)
			{
				return false;
			}
			Thing obj = val;
			obj.stackCount--;
			if (val.stackCount <= 0)
			{
				val.Destroy((DestroyMode)0);
			}
			return true;
		}

		public static void CreatePlantSeeds(Plant p, IntVec3 outputCell, Map map)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			if (ProjectRimFactory_ModComponent.ModSupport_SeedsPlease)
			{
				CreatePlantSeeds_SeedsPlease(p, outputCell, map);
			}
		}

		private static void CreatePlantSeeds_SeedsPlease(Plant p, IntVec3 outputCell, Map map)
		{
			//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
			ThingDef blueprintDef = ((BuildableDef)((Thing)p).def).blueprintDef;
			Type type = ((object)blueprintDef).GetType();
			object value = type.GetField("seed").GetValue(blueprintDef);
			Type type2 = value.GetType();
			float num = Mathf.Max(Mathf.InverseLerp(((Thing)p).def.plant.harvestMinGrowth, 1.2f, p.Growth), 1f);
			if ((float)type2.GetField("seedFactor").GetValue(value) > 0f && Rand.Value < (float)type2.GetField("baseChance").GetValue(value) * num)
			{
				int stackCount = ((!(Rand.Value < (float)type2.GetField("extraChance").GetValue(value))) ? 1 : 2);
				Thing val = ThingMaker.MakeThing(blueprintDef, (ThingDef)null);
				val.stackCount = stackCount;
				GenSpawn.Spawn(val, outputCell, map, (WipeMode)0);
			}
		}
	}
	[StaticConstructorOnStartup]
	public static class StaticEmergencyWorkTypes
	{
		public static readonly WorkTypeDef[] EmergencyWorkTypes;

		static StaticEmergencyWorkTypes()
		{
			EmergencyWorkTypes = (from def in DefDatabase<WorkGiverDef>.AllDefsListForReading
				where def.emergency
				select def.workType).Distinct().ToArray();
		}
	}
	public static class ThingCategoryDefUtility
	{
		public static IEnumerable<ThingCategoryDef> ThisAndParents(this ThingCategoryDef cat)
		{
			yield return cat;
			foreach (ThingCategoryDef parent in cat.Parents)
			{
				yield return parent;
			}
		}
	}
	[StaticConstructorOnStartup]
	public static class ThingFilterHelper
	{
		public static readonly ThingFilter FilterConfigParentNoAnimalNoPlants;

		static ThingFilterHelper()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Expected O, but got Unknown
			FilterConfigParentNoAnimalNoPlants = new ThingFilter();
			FilterConfigParentNoAnimalNoPlants.SetAllowAll((ThingFilter)null, false);
			List<string> list = new List<string>(2) { "Animals", "Plants" };
			foreach (string item in list)
			{
				ThingCategoryDef named = DefDatabase<ThingCategoryDef>.GetNamed(item, false);
				if (named != null)
				{
					FilterConfigParentNoAnimalNoPlants.SetAllow(named, false, (IEnumerable<ThingDef>)null, (IEnumerable<SpecialThingFilterDef>)null);
				}
			}
		}
	}
	public interface IXMLThingDescription
	{
		string GetDescription(ThingDef def);
	}
	public static class UpdateThingDescriptions
	{
		private static bool updatedThingDescriptions;

		private static void UpdateMines()
		{
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			List<ThingDef> list = DefDatabase<ThingDef>.AllDefs.Where((ThingDef d) => d.thingClass == typeof(Building_WorkTable) && ((Def)d).HasModExtension<ModExtension_ModifyProduct>()).ToList();
			foreach (ThingDef item in list)
			{
				if (item?.recipes == null)
				{
					continue;
				}
				string text = "\r\n\r\n";
				text = TaggedString.op_Implicit(text + Translator.Translate("PRF_DescriptionUpdate_CanMine"));
				foreach (RecipeDef recipe in item.recipes)
				{
					ThingDefCountClass val = recipe.products?[0];
					text += $"    - {((val != null) ? val.Label : null)}\r\n";
				}
				((Def)item).description = ((Def)item).description + text;
			}
		}

		private static void UpdateInterface()
		{
			List<ThingDef> list = DefDatabase<ThingDef>.AllDefs.ToList();
			foreach (ThingDef item in list)
			{
				if (item == null || ((Def)item).description == null)
				{
					continue;
				}
				List<CompProperties> list2 = item.comps?.Where((CompProperties c) => c is IXMLThingDescription).ToList();
				List<DefModExtension> list3 = ((Def)item).modExtensions?.Where((DefModExtension m) => m is IXMLThingDescription).ToList();
				Type type = item.thingClass?.GetInterface("IXMLThingDescription");
				string text = "\r\n\r\n";
				if (type != null)
				{
					text += ((IXMLThingDescription)Activator.CreateInstance(item.thingClass)).GetDescription(item);
				}
				if (list2 != null)
				{
					foreach (CompProperties item2 in list2)
					{
						text += (item2 as IXMLThingDescription).GetDescription(item);
					}
				}
				if (list3 != null)
				{
					foreach (DefModExtension item3 in list3)
					{
						text += (item3 as IXMLThingDescription).GetDescription(item);
					}
				}
				if (text != "\r\n\r\n")
				{
					((Def)item).description = ((Def)item).description + text;
				}
			}
		}

		public static void Update()
		{
			if (!updatedThingDescriptions)
			{
				updatedThingDescriptions = true;
				UpdateMines();
				UpdateInterface();
			}
		}
	}
	public static class Util
	{
		public static Color A(this Color color, float a)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return Ops.A(color, a);
		}

		public static IntVec3 FacingCell(IntVec3 center, IntVec2 size, Rot4 rot)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			return Ops.FacingCell(center, size, rot);
		}

		public static IEnumerable<IntVec3> FacingRect(IntVec3 pos, Rot4 dir, int range)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			return Ops.FacingRect(pos, dir, range);
		}

		public static IEnumerable<IntVec3> FacingRect(Thing thing, Rot4 dir, int range)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			return Ops.FacingRect(thing, dir, range);
		}

		public static IEnumerable<IntVec3> FacingRect(IntVec3 center, IntVec2 size, Rot4 dir, int range)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			return Ops.FacingRect(center, size, dir, range);
		}

		public static void CounterAdjustForRotation(ref IntVec3 center, ref IntVec2 size, Rot4 rot)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			IntVec2 val = size;
			if (val.x == 1 && val.z == 1)
			{
				return;
			}
			if (((Rot4)(ref rot)).IsHorizontal)
			{
				ref int x = ref size.x;
				ref int z = ref size.z;
				int z2 = size.z;
				int x2 = size.x;
				x = z2;
				z = x2;
			}
			switch (((Rot4)(ref rot)).AsInt)
			{
			case 0:
				break;
			case 1:
				if (size.z % 2 == 0)
				{
					center.z++;
				}
				break;
			case 2:
				if (size.x % 2 == 0)
				{
					center.x++;
				}
				if (size.z % 2 == 0)
				{
					center.z++;
				}
				break;
			case 3:
				if (size.x % 2 == 0)
				{
					center.x++;
				}
				break;
			}
		}
	}
}
namespace ProjectRimFactory.Common.HarmonyPatches
{
	public static class AdvancedIO_PatchHelper
	{
		private static IEnumerable<KeyValuePair<IntVec3, Building_AdvancedStorageUnitIOPort>> GetAdvancedIOPorts(Map map)
		{
			return PatchStorageUtil.GetPRFMapComponent(map).GetAdvancedIOLocations.Where(delegate(KeyValuePair<IntVec3, Building_AdvancedStorageUnitIOPort> l)
			{
				ILinkableStorageParent boundStorageUnit = l.Value.boundStorageUnit;
				return boundStorageUnit != null && boundStorageUnit.Powered && l.Value.CanGetNewItem;
			});
		}

		private static List<KeyValuePair<float, Building_AdvancedStorageUnitIOPort>> GetOrderedAdvancedIOPorts(Map map, IntVec3 referencePos)
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			IEnumerable<KeyValuePair<IntVec3, Building_AdvancedStorageUnitIOPort>> advancedIOPorts = GetAdvancedIOPorts(map);
			List<KeyValuePair<float, Building_AdvancedStorageUnitIOPort>> list = new List<KeyValuePair<float, Building_AdvancedStorageUnitIOPort>>();
			foreach (KeyValuePair<IntVec3, Building_AdvancedStorageUnitIOPort> item in advancedIOPorts)
			{
				float key = IntVec3Utility.DistanceTo(item.Key, referencePos);
				list.Add(new KeyValuePair<float, Building_AdvancedStorageUnitIOPort>(key, item.Value));
			}
			return list.OrderBy((KeyValuePair<float, Building_AdvancedStorageUnitIOPort> i) => i.Key).ToList();
		}

		public static List<KeyValuePair<float, Building_AdvancedStorageUnitIOPort>> GetOrderedAdvancedIOPorts(Map map, IntVec3 pawnPos, IntVec3 targetPos)
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			IEnumerable<KeyValuePair<IntVec3, Building_AdvancedStorageUnitIOPort>> advancedIOPorts = GetAdvancedIOPorts(map);
			List<KeyValuePair<float, Building_AdvancedStorageUnitIOPort>> list = new List<KeyValuePair<float, Building_AdvancedStorageUnitIOPort>>();
			foreach (KeyValuePair<IntVec3, Building_AdvancedStorageUnitIOPort> item in advancedIOPorts)
			{
				float key = CalculatePath(pawnPos, item.Key, targetPos);
				list.Add(new KeyValuePair<float, Building_AdvancedStorageUnitIOPort>(key, item.Value));
			}
			return list.OrderBy((KeyValuePair<float, Building_AdvancedStorageUnitIOPort> i) => i.Key).ToList();
		}

		public static KeyValuePair<float, Building_AdvancedStorageUnitIOPort> GetClosestPort(Map map, IntVec3 referencePos)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			return GetOrderedAdvancedIOPorts(map, referencePos).FirstOrDefault();
		}

		public static KeyValuePair<float, Building_AdvancedStorageUnitIOPort> GetClosestPort(Map map, IntVec3 pawnPos, IntVec3 targetPos, Thing thing, float maxDistance)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			return GenCollection.FirstOrDefault<KeyValuePair<float, Building_AdvancedStorageUnitIOPort>>(GetOrderedAdvancedIOPorts(map, pawnPos, targetPos), (Predicate<KeyValuePair<float, Building_AdvancedStorageUnitIOPort>>)((KeyValuePair<float, Building_AdvancedStorageUnitIOPort> p) => p.Key < maxDistance && CanMoveItem(p.Value, thing)));
		}

		public static bool CanMoveItem(Building_AdvancedStorageUnitIOPort port, Thing thing)
		{
			return port.boundStorageUnit?.StoredItems?.Contains(thing) == true;
		}

		public static bool CanMoveItem(Building_AdvancedStorageUnitIOPort port, IntVec3 thingPos)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			return port.boundStorageUnit?.HoldsPos(thingPos) ?? false;
		}

		public static float CalculatePath(IntVec3 pawnPos, IntVec3 thingPos, IntVec3 targetPos)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			return IntVec3Utility.DistanceTo(pawnPos, thingPos) + IntVec3Utility.DistanceTo(thingPos, targetPos);
		}
	}
	[HarmonyPatch(typeof(CompArt), "GenerateImageDescription")]
	public class CompArt_GenerateImageDescription
	{
		public static bool Prefix(out TaggedString __result, CompArt __instance)
		{
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			Thing t = (Thing)(object)((ThingComp)__instance).parent;
			List<SpecialSculpture> specialScupltures = Current.Game.GetComponent<PRFGameComponent>().specialScupltures;
			SpecialSculpture specialSculpture = ((specialScupltures != null) ? GenCollection.FirstOrDefault<SpecialSculpture>(specialScupltures, (Predicate<SpecialSculpture>)((SpecialSculpture x) => x.currentInstances != null && x.currentInstances.Contains(t))) : null);
			if (specialSculpture == null)
			{
				return true;
			}
			__result = new TaggedString(TaggedString.op_Implicit(Translator.Translate(specialSculpture.descKey)));
			return false;
		}
	}
	[HarmonyPatch(typeof(Pawn_JobTracker), "EndCurrentJob")]
	internal class Patch_EndCurrentJob_DroneJobs
	{
		private static bool Prefix(Pawn_JobTracker __instance, Pawn ___pawn, JobCondition condition, bool startNewJob = true, bool canReturnToPool = true)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Invalid comparison between Unknown and I4
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00de: Unknown result type (might be due to invalid IL or missing references)
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			//IL_0126: Unknown result type (might be due to invalid IL or missing references)
			//IL_014e: Expected O, but got Unknown
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Invalid comparison between Unknown and I4
			bool flag = ___pawn.kindDef == PRFDefOf.PRFDroneKind;
			bool flag2 = flag;
			if (flag2)
			{
				bool flag3 = (((int)condition == 64 || (int)condition == 128) ? true : false);
				flag2 = flag3;
			}
			if (flag2)
			{
				if (__instance.curJob.def == PRFDefOf.PRFDrone_ReturnToStation)
				{
					IntVec3 position = ((Thing)___pawn).Position;
					Map map = ((Thing)___pawn).Map;
					((Thing)___pawn).Destroy((DestroyMode)0);
					GenSpawn.Spawn(PRFDefOf.PRF_DroneModule, position, map, (WipeMode)0);
					Log.Warning($"PRF - Drone could not return home from {position} - Powering down");
					return false;
				}
				object arg = ((Thing)___pawn).Position;
				LocalTargetInfo target = __instance.curJob.GetTarget((TargetIndex)1);
				Log.Warning($"Vanilla Pathing Failed - Drone Returning to Station - {arg}->{((LocalTargetInfo)(ref target)).Cell}");
				Traverse.Create((object)__instance).Method("CleanupCurrentJob", new object[4] { condition, true, true, canReturnToPool }).GetValue();
				Pawn_Drone pawn_Drone = (Pawn_Drone)(object)___pawn;
				__instance.StartJob(new Job(PRFDefOf.PRFDrone_ReturnToStation, LocalTargetInfo.op_Implicit((Thing)(object)pawn_Drone.BaseStation)), (JobCondition)0, (ThinkNode)null, false, true, (ThinkTreeDef)null, (JobTag?)null, false, false, (bool?)null, false, true, false);
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(WorkGiver_RemoveBuilding), "PotentialWorkThingsGlobal")]
	internal class Patch_PotentialWorkThingsGlobal_DronesRenoveOwnBase
	{
		private static void Postfix(Pawn pawn, ref IEnumerable<Thing> __result)
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			if (pawn.kindDef == PRFDefOf.PRFDroneKind)
			{
				Pawn_Drone pawn_Drone = (Pawn_Drone)(object)pawn;
				IntVec3 droneStationPos = ((Thing)pawn_Drone.BaseStation).Position;
				__result = __result.Where((Thing u) => u.Position != droneStationPos).ToList();
			}
		}
	}
	public static class FridgePowerPatchUtil
	{
		public static readonly Dictionary<Building_MassStorageUnitPowered, float> FridgePowerDrawPerUnit = new Dictionary<Building_MassStorageUnitPowered, float>();

		public static void UpdatePowerDraw(Building_MassStorageUnitPowered dsu, CompPowerTrader powerTrader)
		{
			if (!FridgePowerDrawPerUnit.TryGetValue(dsu, out var value))
			{
				value = -1f * (float)ReflectionUtility.CompPropertiesPowerBasePowerConsumption.GetValue(((CompPower)powerTrader).Props);
			}
			powerTrader.powerOutputInt = value - dsu.ExtraPowerDraw;
		}
	}
	[HarmonyPatch(typeof(Alert_NeedBatteries), "NeedBatteries")]
	internal class Alert_NeedBatteries_NeedBatteries_Patch
	{
		private static void Postfix(Alert_NeedBatteries __instance, Map map, ref bool __result)
		{
			if (__result)
			{
				if (map.listerBuildings.ColonistsHaveBuilding((Func<Thing, bool>)((Thing building) => building is Building_HexCapacitor)))
				{
					__result = false;
				}
				else if (map.listerBuildings.ColonistsHaveBuilding((Func<Thing, bool>)((Thing building) => building is Building_CustomBattery)))
				{
					__result = false;
				}
			}
		}
	}
	[HarmonyPatch(typeof(ModContentPack), "get_Patches")]
	internal class Patch_ModContentPack_Pathes
	{
		private static void Postfix(ModContentPack __instance, ref IEnumerable<PatchOperation> __result)
		{
			if (!(__instance.PackageId.ToLower() == ((Mod)LoadedModManager.GetMod<ProjectRimFactory_ModComponent>()).Content.PackageId.ToLower()))
			{
				return;
			}
			ProjectRimFactory_ModSettings settings = LoadedModManager.GetMod<ProjectRimFactory_ModComponent>().Settings;
			List<PatchOperation> list = settings.Patches.ToList();
			int num = 0;
			foreach (PatchOperation item in list)
			{
				num++;
				item.sourceFile = "PRF_SettingsPatch_" + num + "_";
			}
			__result = __result.Concat(list);
		}
	}
	[HarmonyPatch(typeof(ReservationManager), "Reserve")]
	internal class Patch_Reservation_Reservation_IO
	{
		private static bool Prefix(Pawn claimant, Job job, LocalTargetInfo target, ref bool __result, Map ___map)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			if (((LocalTargetInfo)(ref target)).HasThing || ___map == null || !GenGrid.InBounds(((LocalTargetInfo)(ref target)).Cell, ___map))
			{
				return true;
			}
			Building_StorageUnitIOBase building_StorageUnitIOBase = (Building_StorageUnitIOBase)(object)GenCollection.FirstOrDefault<Thing>(GridsUtility.GetThingList(((LocalTargetInfo)(ref target)).Cell, ___map), (Predicate<Thing>)((Thing t) => t is Building_StorageUnitIOBase));
			if (building_StorageUnitIOBase != null && building_StorageUnitIOBase.Mode == StorageIOMode.Input)
			{
				__result = true;
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(Page_SelectScenario), "PreOpen")]
	internal class PatchPage_SelectScenarioPreOpen
	{
		private static bool Prefix()
		{
			if (ProjectRimFactory_ModSettings.PRF_LiteMode)
			{
				PRF_CustomizeDefs.RemoveScenario(PRF_CustomizeDefs.ExcludeScenario.ToList());
			}
			return true;
		}
	}
	internal class Patch_ForbidUtility_IsForbidden
	{
		private static bool Prefix(Thing t, Pawn pawn, out bool __result)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Invalid comparison between Unknown and I4
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			__result = true;
			Map val = ((t != null) ? t.Map : null);
			if (val != null && (int)t.def.category == 2)
			{
				PRFMapComponent pRFMapComponent = PatchStorageUtil.GetPRFMapComponent(val);
				if (pRFMapComponent != null && pRFMapComponent.ShouldForbidPawnOutputAtPos(t.Position))
				{
					return false;
				}
			}
			return true;
		}
	}
	internal class Patch_Building_Storage_Accepts
	{
		private static bool Prefix(Building_Storage __instance, Thing t, out bool __result)
		{
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			__result = false;
			if (!PatchStorageUtil.SkippAcceptsPatch)
			{
				IForbidPawnInputItem obj = __instance as IForbidPawnInputItem;
				if (obj != null && obj.ForbidPawnInput && ((Thing)__instance).Position != t.Position)
				{
					return false;
				}
			}
			return true;
		}
	}
	internal class Patch_StorageSettings_AllowedToAccept
	{
		private static bool Prefix(IStoreSettingsParent ___owner, Thing t, out bool __result)
		{
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			__result = false;
			Building_Storage val = (Building_Storage)(object)((___owner is Building_Storage) ? ___owner : null);
			if (val != null && !PatchStorageUtil.SkippAcceptsPatch)
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
	internal class Patch_FloatMenuMakerMap_GetOptions
	{
		private static bool Prefix(List<Pawn> selectedPawns, Vector3 clickPos, out List<FloatMenuOption> __result, out FloatMenuContext context)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			context = null;
			if (Find.CurrentMap.GetComponent<PRFMapComponent>().iHideRightMenus.Contains(IntVec3Utility.ToIntVec3(clickPos)))
			{
				__result = new List<FloatMenuOption>();
				return false;
			}
			__result = null;
			return true;
		}
	}
	internal class Patch_Thing_DrawGUIOverlay
	{
		private static bool Prefix(Thing __instance)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Invalid comparison between Unknown and I4
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			if ((int)__instance.def.category == 2)
			{
				PRFMapComponent pRFMapComponent = PatchStorageUtil.GetPRFMapComponent(__instance.Map);
				if (pRFMapComponent != null && pRFMapComponent.ShouldHideItemsAtPos(__instance.Position))
				{
					return false;
				}
			}
			return true;
		}
	}
	internal class Patch_ThingWithComps_DrawGUIOverlay
	{
		private static bool Prefix(Thing __instance)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Invalid comparison between Unknown and I4
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			if ((int)__instance.def.category == 2)
			{
				PRFMapComponent pRFMapComponent = PatchStorageUtil.GetPRFMapComponent(__instance.Map);
				if (pRFMapComponent != null && pRFMapComponent.ShouldHideItemsAtPos(__instance.Position))
				{
					return false;
				}
			}
			return true;
		}
	}
	internal class Patch_Thing_Print
	{
		private static bool Prefix(Thing __instance, SectionLayer layer)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Invalid comparison between Unknown and I4
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			if ((int)__instance.def.category == 2)
			{
				PRFMapComponent pRFMapComponent = PatchStorageUtil.GetPRFMapComponent(__instance.Map);
				if (pRFMapComponent != null && pRFMapComponent.ShouldHideItemsAtPos(__instance.Position))
				{
					return false;
				}
			}
			return true;
		}
	}
	internal class Patch_MinifiedThing_Print
	{
		private static bool Prefix(Thing __instance, SectionLayer layer)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Invalid comparison between Unknown and I4
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			if ((int)__instance.def.category == 2)
			{
				PRFMapComponent pRFMapComponent = PatchStorageUtil.GetPRFMapComponent(__instance.Map);
				if (pRFMapComponent != null && pRFMapComponent.ShouldHideItemsAtPos(__instance.Position))
				{
					return false;
				}
			}
			return true;
		}
	}
	internal static class PatchStorageUtil
	{
		private static readonly Dictionary<Map, PRFMapComponent> MapComps;

		public static bool SkippAcceptsPatch;

		static PatchStorageUtil()
		{
			SkippAcceptsPatch = false;
			MapComps = new Dictionary<Map, PRFMapComponent>();
		}

		public static PRFMapComponent GetPRFMapComponent(Map map)
		{
			PRFMapComponent value = null;
			if (map != null && !MapComps.TryGetValue(map, out value))
			{
				value = map.GetComponent<PRFMapComponent>();
				MapComps.Add(map, value);
			}
			return value;
		}

		public static T Get<T>(Map map, IntVec3 pos) where T : class
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			return ((IntVec3)(ref pos)).IsValid ? pos.GetFirst<T>(map) : null;
		}
	}
	public interface IHideItem
	{
		bool HideItems { get; }
	}
	public interface IForbidPawnOutputItem
	{
		bool ForbidPawnOutput { get; }
	}
	public interface IForbidPawnInputItem : ISlotGroupParent, IStoreSettingsParent, IHaulDestination
	{
		bool ForbidPawnInput { get; }
	}
	[HarmonyPatch]
	internal class Patch_Building_Storage_GetGizmos
	{
		private static MethodBase TargetMethod()
		{
			Type type = typeof(Building_Storage).GetNestedTypes(AccessTools.all).FirstOrDefault((Type t) => t.FullName.Contains("d__54"));
			if (type == null)
			{
				Log.Error("PRF Harmony Error - predicateClass == null for Patch_Building_Storage_GetGizmos.TargetMethod()");
				return null;
			}
			MethodInfo methodInfo = type.GetMethods(AccessTools.all).FirstOrDefault((MethodInfo t) => t.Name.Contains("MoveNext"));
			if (methodInfo == null)
			{
				Log.Error("PRF Harmony Error - m == null for Patch_Building_Storage_GetGizmos.TargetMethod()");
			}
			return methodInfo;
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			bool foundGetNumSelected = false;
			object endJumpMarker = null;
			bool addedJump = false;
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Callvirt && instruction.operand.ToString().Contains("get_NumSelected()"))
				{
					foundGetNumSelected = true;
				}
				if (foundGetNumSelected && endJumpMarker == null && instruction.opcode == OpCodes.Bne_Un)
				{
					endJumpMarker = instruction.operand;
				}
				if (!addedJump && foundGetNumSelected && instruction.opcode == OpCodes.Ldarg_0)
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null);
					yield return new CodeInstruction(OpCodes.Ldloc_2, (object)null);
					yield return new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Patch_Building_Storage_GetGizmos), "IsPRF_StorageBuilding", (Type[])null, (Type[])null));
					yield return new CodeInstruction(OpCodes.Brtrue_S, endJumpMarker);
					addedJump = true;
				}
				yield return instruction;
			}
		}

		public static bool IsPRF_StorageBuilding(Building_Storage building)
		{
			return building is Building_MassStorageUnit;
		}
	}
	[HarmonyPatch(typeof(CompFoodPoisonable), "Notify_RecipeProduced")]
	internal class Patch_CompFoodPoisonable_Notify_RecipeProduced
	{
		public static Building_ProgrammableAssembler AssemblerRefrence;

		public static bool Prefix(CompFoodPoisonable __instance, Pawn pawn)
		{
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			if (AssemblerRefrence == null)
			{
				return true;
			}
			if (((Def)((Thing)AssemblerRefrence).def).defName == "PRF_SelfCookerIII")
			{
				return false;
			}
			if (!(AssemblerRefrence is Building_SimpleAssembler))
			{
				return true;
			}
			Room obj = RegionAndRoomQuery.RoomAt(AssemblerRefrence.OutputCell(), ((Thing)AssemblerRefrence).Map, (RegionType)14);
			if (Rand.Chance((obj != null) ? obj.GetStat(RoomStatDefOf.FoodPoisonChance) : RoomStatDefOf.FoodPoisonChance.roomlessScore))
			{
				__instance.SetPoisoned((FoodPoisonCause)2);
			}
			return false;
		}
	}
	public static class Patch_CompRefrigerator_CompTickRare
	{
		public static void Postfix(ThingComp __instance)
		{
			ThingWithComps parent = __instance.parent;
			if (parent is Building_MassStorageUnitPowered building_MassStorageUnitPowered)
			{
				CompPowerTrader comp = parent.GetComp<CompPowerTrader>();
				GenCollection.SetOrAdd<Building_MassStorageUnitPowered, float>(FridgePowerPatchUtil.FridgePowerDrawPerUnit, building_MassStorageUnitPowered, comp.powerOutputInt);
				comp.powerOutputInt -= building_MassStorageUnitPowered.ExtraPowerDraw;
			}
		}
	}
	[HarmonyPatch(typeof(RoomRequirement_ThingCount), "Count")]
	internal class Patch_DroneColumn_Royalty
	{
		private static void Postfix(Room r, ref int __result, RoomRequirement_ThingCount __instance)
		{
			if (__result < __instance.count && ((RoomRequirement_Thing)__instance).thingDef == PRFDefOf.Column)
			{
				__result += r.ThingCount(PRFDefOf.PRF_MiniDroneColumn);
			}
		}
	}
	[HarmonyPatch(typeof(TradeUtility), "AllLaunchableThingsForTrade")]
	internal class Patch_TradeUtility_AllLaunchableThingsForTrade
	{
		private static void Postfix(Map map, ref IEnumerable<Thing> __result)
		{
			HashSet<Thing> hashSet = new HashSet<Thing>();
			GenCollection.AddRange<Thing>(hashSet, __result);
			foreach (ILinkableStorageParent item in TradePatchHelper.AllPowered(map))
			{
				GenCollection.AddRange<Thing>(hashSet, item.StoredItems);
			}
			__result = hashSet;
		}
	}
	[HarmonyPatch]
	public static class Patch_PassingShip_c__DisplayClass24_0
	{
		private static Type predicateClass;

		private static MethodBase TargetMethod()
		{
			predicateClass = typeof(PassingShip).GetNestedTypes(AccessTools.all).FirstOrDefault((Type t) => t.FullName.Contains("c__DisplayClass23_0"));
			if (predicateClass == null)
			{
				Log.Error("PRF Harmony Error - predicateClass == null for Patch_PassingShip_DSUisTradebeacon.TargetMethod()");
				return null;
			}
			MethodInfo methodInfo = predicateClass.GetMethods(AccessTools.all).FirstOrDefault((MethodInfo t) => t.Name.Contains("b__1"));
			if (methodInfo == null)
			{
				Log.Error("PRF Harmony Error - m == null for Patch_PassingShip_DSUisTradebeacon.TargetMethod()");
			}
			return methodInfo;
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			Type hiddenClass = typeof(PassingShip).GetNestedTypes(AccessTools.all).FirstOrDefault((Type t) => t.FullName.Contains("c__DisplayClass23_0"));
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
					yield return new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(TradePatchHelper), "AnyPowered", new Type[1] { typeof(Map) }, (Type[])null));
					yield return new CodeInstruction(OpCodes.Brtrue_S, instruction.operand);
				}
				else
				{
					yield return instruction;
				}
			}
		}
	}
	public static class TradePatchHelper
	{
		public static bool AnyPowered(Map map)
		{
			return AllPowered(map, any: true).Any();
		}

		public static IEnumerable<ILinkableStorageParent> AllPowered(Map map, bool any = false)
		{
			foreach (Building_MassStorageUnitPowered item in map.listerBuildings.AllBuildingsColonistOfClass<Building_MassStorageUnitPowered>())
			{
				if (((ILinkableStorageParent)item).Powered)
				{
					yield return item;
					if (any)
					{
						break;
					}
				}
			}
			IEnumerable<ILinkableStorageParent> cs = ((IEnumerable<Building_ColdStorage>)PatchStorageUtil.GetPRFMapComponent(map).ColdStorageBuildings).Select((Func<Building_ColdStorage, ILinkableStorageParent>)((Building_ColdStorage b) => b));
			foreach (ILinkableStorageParent item2 in cs)
			{
				yield return item2;
			}
		}
	}
	[HarmonyPatch(typeof(FoodUtility), "SpawnedFoodSearchInnerScan")]
	internal class Patch_FoodUtility_SpawnedFoodSearchInnerScan
	{
		private static object thingarg;

		private static bool afterflaotMin;

		private static float mindist = float.MaxValue;

		private static Building_AdvancedStorageUnitIOPort closestPort;

		private static bool ioPortSelected;

		private static Thing ioPortSelectedFor;

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			thingarg = null;
			afterflaotMin = false;
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand.ToString() == "-3.402823E+38")
				{
					yield return instruction;
					afterflaotMin = true;
					continue;
				}
				if (afterflaotMin && instruction.opcode == OpCodes.Stloc_S)
				{
					afterflaotMin = false;
					yield return instruction;
					yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null);
					yield return new CodeInstruction(OpCodes.Ldarg_1, (object)null);
					yield return new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Patch_FoodUtility_SpawnedFoodSearchInnerScan), "FindClosestPort", new Type[2]
					{
						typeof(Pawn),
						typeof(IntVec3)
					}, (Type[])null));
					continue;
				}
				if (instruction.opcode == OpCodes.Stloc_S && instruction.operand.ToString() == "Verse.Thing (7)" && thingarg == null)
				{
					thingarg = instruction.operand;
				}
				if (instruction.opcode == OpCodes.Stloc_S && instruction.operand.ToString() == "System.Single (8)")
				{
					yield return instruction;
					yield return new CodeInstruction(OpCodes.Ldloca_S, instruction.operand);
					yield return new CodeInstruction(OpCodes.Ldloc_S, thingarg);
					yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null);
					yield return new CodeInstruction(OpCodes.Ldarg_1, (object)null);
					yield return new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Patch_FoodUtility_SpawnedFoodSearchInnerScan), "IsIOPortBetter", new Type[4]
					{
						typeof(float).MakeByRefType(),
						typeof(Thing),
						typeof(Pawn),
						typeof(IntVec3)
					}, (Type[])null));
				}
				else
				{
					if (instruction.opcode == OpCodes.Ret && thingarg != null)
					{
						yield return new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Patch_FoodUtility_SpawnedFoodSearchInnerScan), "MoveItemIfNeeded", new Type[1] { typeof(Thing) }, (Type[])null));
						yield return new CodeInstruction(OpCodes.Ldloc_3, (object)null);
					}
					yield return instruction;
				}
			}
		}

		public static void FindClosestPort(Pawn pawn, IntVec3 _)
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			mindist = float.MaxValue;
			closestPort = null;
			Faction faction = ((Thing)pawn).Faction;
			if (faction != null && faction.IsPlayer)
			{
				KeyValuePair<float, Building_AdvancedStorageUnitIOPort> keyValuePair = AdvancedIO_PatchHelper.GetClosestPort(((Thing)pawn).Map, ((Thing)pawn).Position);
				mindist = keyValuePair.Key;
				closestPort = keyValuePair.Value;
			}
		}

		public static void IsIOPortBetter(ref float distance, Thing thing, Pawn pawn, IntVec3 start)
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			ioPortSelected = false;
			if ((mindist < distance || (ConditionalPatchHelper.PatchReachabilityCanReach.Status && ((Thing)pawn).Map.reachability.CanReach(start, LocalTargetInfo.op_Implicit(thing), (PathEndMode)2, TraverseParms.For(pawn, (Danger)3, (TraverseMode)0, false, false, false, true)) && Patch_Reachability_CanReach.CanReachThing(thing))) && closestPort != null && AdvancedIO_PatchHelper.CanMoveItem(closestPort, thing))
			{
				distance = mindist;
				ioPortSelected = true;
				ioPortSelectedFor = thing;
			}
		}

		public static void MoveItemIfNeeded(Thing thing)
		{
			if (thing == ioPortSelectedFor && ioPortSelected && thing != null)
			{
				ioPortSelected = false;
				closestPort.PlaceThingNow(thing);
			}
		}
	}
	[HarmonyPatch(typeof(QualityUtility), "GenerateQualityCreatedByPawn", new Type[]
	{
		typeof(Pawn),
		typeof(SkillDef),
		typeof(bool)
	})]
	internal class Patch_GenRecipe_GenerateQualityCreatedByPawn
	{
		private static bool Prefix(ref QualityCategory __result, Pawn pawn, SkillDef relevantSkill, bool consumeInspiration)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Expected I4, but got Unknown
			ISetQualityDirectly setQualityDirectly = PatchStorageUtil.Get<ISetQualityDirectly>(((Thing)pawn).Map, ((Thing)pawn).Position);
			if (setQualityDirectly == null)
			{
				return true;
			}
			__result = (QualityCategory)(int)setQualityDirectly.GetQuality(relevantSkill);
			return false;
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			bool foundReplaceMarker = false;
			bool repacedCheck = false;
			int cnt = 0;
			foreach (CodeInstruction instruction in instructions)
			{
				cnt++;
				if (instruction.opcode == OpCodes.Ldarg_0 && !repacedCheck)
				{
					repacedCheck = true;
					foundReplaceMarker = true;
					cnt = 0;
				}
				if (!(instruction.opcode == OpCodes.Callvirt && foundReplaceMarker) || cnt != 1)
				{
					if (instruction.opcode == OpCodes.Callvirt && foundReplaceMarker && cnt == 2)
					{
						instruction.operand = AccessTools.Method(typeof(Patch_GenRecipe_GenerateQualityCreatedByPawn), "IsMechanoid", new Type[1] { typeof(Pawn) }, (Type[])null);
						foundReplaceMarker = false;
					}
					yield return instruction;
				}
			}
		}

		public static bool IsMechanoid(Pawn pawn)
		{
			return !(pawn is Pawn_Drone) && pawn.RaceProps.IsMechanoid;
		}
	}
	internal interface ISetQualityDirectly
	{
		QualityCategory GetQuality(SkillDef relevantSkill);
	}
	[HarmonyPatch(typeof(GravshipPlacementUtility), "SpawnNonPawnThings")]
	public class Patch_GravshipPlacementUtility_SpawnNonPawnThings
	{
		private static List<Building_MassStorageUnit> storedBuildings = new List<Building_MassStorageUnit>();

		public static bool Prefix(Gravship gravship, Map map, List<Thing> gravshipThings, IntVec3 root)
		{
			storedBuildings = gravshipThings.OfType<Building_MassStorageUnit>().ToList();
			foreach (Building_MassStorageUnit item in storedBuildings.Where((Building_MassStorageUnit thing) => ((Thing)thing).def.building.maxItemsInCell == 1))
			{
				((Thing)item).def.building.maxItemsInCell = item.ModExtensionCrate?.limit ?? int.MaxValue;
			}
			return true;
		}

		public static void Postfix(Gravship gravship, Map map, List<Thing> gravshipThings, IntVec3 root)
		{
			foreach (Building_MassStorageUnit storedBuilding in storedBuildings)
			{
				((Thing)storedBuilding).def.building.maxItemsInCell = 1;
				storedBuilding.RefreshStorage(fullRefresh: true);
			}
		}
	}
	[HarmonyPatch(typeof(JobDriver_HaulToTransporter), "Notify_Starting")]
	internal class Patch_JobDriver_HaulToTransporter_Notify_Starting
	{
		public static void Postfix(JobDriver_HaulToTransporter __instance)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 position = ((Thing)((JobDriver)__instance).pawn).Position;
			IntVec3 cell = ((LocalTargetInfo)(ref ((JobDriver)__instance).job.targetA)).Cell;
			IntVec3 cell2 = ((LocalTargetInfo)(ref ((JobDriver)__instance).job.targetB)).Cell;
			float maxDistance = AdvancedIO_PatchHelper.CalculatePath(position, cell, cell2);
			Building_AdvancedStorageUnitIOPort value = AdvancedIO_PatchHelper.GetClosestPort(((Thing)((JobDriver)__instance).pawn).Map, position, cell2, ((LocalTargetInfo)(ref ((JobDriver)__instance).job.targetA)).Thing, maxDistance).Value;
			if (value != null)
			{
				((LocalTargetInfo)(ref ((JobDriver)__instance).job.targetA)).Thing.Position = ((Thing)value).Position;
			}
		}
	}
	[HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
	public class Patch_JobGiver_Work_PawnCanUseWorkGiver
	{
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			bool foundMissingRequiredCapacity = false;
			bool foundColonySubhuman = false;
			int foundColonySubhumanCnt = 0;
			int counter = 0;
			object retPointer = null;
			object initialSkipPointer = null;
			foreach (CodeInstruction instruction in instructions)
			{
				if (initialSkipPointer == null && instruction.opcode == OpCodes.Brtrue_S)
				{
					initialSkipPointer = instruction.operand;
				}
				if (!foundColonySubhuman && instruction.opcode == OpCodes.Callvirt && instruction.operand.ToString() == "Boolean get_IsColonySubhuman()")
				{
					foundColonySubhuman = true;
				}
				if (foundColonySubhuman)
				{
					foundColonySubhumanCnt++;
				}
				if (foundColonySubhumanCnt == 3)
				{
					yield return new CodeInstruction(OpCodes.Ldarg_1, (object)null);
					yield return new CodeInstruction(OpCodes.Isinst, (object)typeof(Pawn_Drone));
					yield return new CodeInstruction(OpCodes.Ldnull, (object)null);
					yield return new CodeInstruction(OpCodes.Cgt_Un, (object)null);
					yield return new CodeInstruction(OpCodes.Brtrue_S, initialSkipPointer);
				}
				if (!foundMissingRequiredCapacity && instruction.opcode == OpCodes.Callvirt && instruction.operand.ToString() == "Verse.PawnCapacityDef MissingRequiredCapacity(Verse.Pawn)")
				{
					foundMissingRequiredCapacity = true;
				}
				if (foundMissingRequiredCapacity)
				{
					counter++;
				}
				if (counter == 8)
				{
					retPointer = instruction.operand;
				}
				if (counter == 9)
				{
					yield return new CodeInstruction(OpCodes.Ldarg_1, (object)null);
					yield return new CodeInstruction(OpCodes.Isinst, (object)typeof(Pawn_Drone));
					yield return new CodeInstruction(OpCodes.Ldnull, (object)null);
					yield return new CodeInstruction(OpCodes.Cgt_Un, (object)null);
					yield return new CodeInstruction(OpCodes.Brfalse_S, retPointer);
				}
				yield return instruction;
			}
		}
	}
	public static class Patch_FridgeUtility_Tick
	{
		public static void Postfix(List<CompPowerTrader> ___fridgeCache)
		{
			foreach (CompPowerTrader item in ___fridgeCache)
			{
				if (((ThingComp)item).parent is Building_MassStorageUnitPowered building_MassStorageUnitPowered)
				{
					GenCollection.SetOrAdd<Building_MassStorageUnitPowered, float>(FridgePowerPatchUtil.FridgePowerDrawPerUnit, building_MassStorageUnitPowered, item.powerOutputInt);
					item.powerOutputInt -= building_MassStorageUnitPowered.ExtraPowerDraw;
				}
			}
		}
	}
	[HarmonyPatch(typeof(Pawn), "get_IsColonyMech")]
	public class Patch_Pawn_get_IsColonyMech
	{
		private static bool foundReturnFalsePointer;

		private static object returnFalsePointer;

		private static int referencePointCnt;

		private static bool foundReferencePoint;

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (CodeInstruction instruction in instructions)
			{
				if (!foundReturnFalsePointer && instruction.opcode == OpCodes.Brfalse_S)
				{
					foundReturnFalsePointer = true;
					returnFalsePointer = instruction.operand;
				}
				if (!foundReferencePoint && foundReturnFalsePointer && instruction.operand?.ToString() == "Verse.MentalStateDef get_MentalStateDef()")
				{
					foundReferencePoint = true;
				}
				if (foundReferencePoint)
				{
					referencePointCnt++;
				}
				if (foundReferencePoint && referencePointCnt == 3)
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null);
					yield return new CodeInstruction(OpCodes.Isinst, (object)typeof(Pawn_Drone));
					yield return new CodeInstruction(OpCodes.Ldnull, (object)null);
					yield return new CodeInstruction(OpCodes.Cgt_Un, (object)null);
					yield return new CodeInstruction(OpCodes.Brtrue_S, returnFalsePointer);
				}
				yield return instruction;
			}
		}
	}
	[HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
	internal class Patch_Pawn_JobTracker_StartJob
	{
		private static bool TryGetTargetPos(out IntVec3 targetPos, bool isHaulJobType, Job newJob, IntVec3 pawnPosition)
		{
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			if (isHaulJobType)
			{
				Thing thing = ((LocalTargetInfo)(ref newJob.targetB)).Thing;
				targetPos = ((thing != null) ? thing.Position : ((LocalTargetInfo)(ref newJob.targetB)).Cell);
				if (targetPos == IntVec3.Invalid)
				{
					targetPos = pawnPosition;
				}
				if (newJob.targetA == LocalTargetInfo.op_Implicit((Thing)null))
				{
					return false;
				}
			}
			else
			{
				Thing thing2 = ((LocalTargetInfo)(ref newJob.targetA)).Thing;
				targetPos = ((thing2 != null) ? thing2.Position : ((LocalTargetInfo)(ref newJob.targetA)).Cell);
				if (newJob.targetB == LocalTargetInfo.op_Implicit(IntVec3.Invalid) && (newJob.targetQueueB == null || newJob.targetQueueB.Count == 0))
				{
					return false;
				}
			}
			return true;
		}

		private static void GetTargetItems(out List<LocalTargetInfo> targetItems, bool isHaulJobType, Job newJob)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			if (isHaulJobType)
			{
				targetItems = new List<LocalTargetInfo>(1) { newJob.targetA };
			}
			else if (newJob.targetQueueB == null || newJob.targetQueueB.Count == 0)
			{
				targetItems = new List<LocalTargetInfo>(1) { newJob.targetB };
			}
			else
			{
				targetItems = newJob.targetQueueB;
			}
		}

		public static bool Prefix(Job newJob, ref Pawn ___pawn, JobCondition lastJobEndCondition = (JobCondition)0, ThinkNode jobGiver = null, bool resumeCurJobAfterwards = false, bool cancelBusyStances = true, ThinkTreeDef thinkTree = null, JobTag? tag = null, bool fromQueue = false, bool canReturnCurJobToPool = false)
		{
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0127: Unknown result type (might be due to invalid IL or missing references)
			//IL_017b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0183: Unknown result type (might be due to invalid IL or missing references)
			//IL_0191: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
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
			Map map = ((Thing)___pawn).Map;
			IntVec3 position = ((Thing)___pawn).Position;
			PRFMapComponent pRFMapComponent = PatchStorageUtil.GetPRFMapComponent(map);
			Thing thing = ((LocalTargetInfo)(ref newJob.targetA)).Thing;
			bool isHaulJobType = thing != null && thing.def?.category == (ThingCategory?)2;
			if (!TryGetTargetPos(out var targetPos, isHaulJobType, newJob, position))
			{
				return true;
			}
			List<KeyValuePair<float, Building_AdvancedStorageUnitIOPort>> orderedAdvancedIOPorts = AdvancedIO_PatchHelper.GetOrderedAdvancedIOPorts(map, position, targetPos);
			GetTargetItems(out var targetItems, isHaulJobType, newJob);
			foreach (LocalTargetInfo item in targetItems)
			{
				LocalTargetInfo current = item;
				if (((LocalTargetInfo)(ref current)).Thing == null)
				{
					continue;
				}
				float num = AdvancedIO_PatchHelper.CalculatePath(position, ((LocalTargetInfo)(ref current)).Cell, targetPos);
				if (!pRFMapComponent.ShouldHideItemsAtPos(((LocalTargetInfo)(ref current)).Cell))
				{
					continue;
				}
				foreach (KeyValuePair<float, Building_AdvancedStorageUnitIOPort> item2 in orderedAdvancedIOPorts)
				{
					if (item2.Key < num || (ConditionalPatchHelper.PatchReachabilityCanReach.Status && map.reachability.CanReach(position, LocalTargetInfo.op_Implicit(((LocalTargetInfo)(ref current)).Thing), (PathEndMode)2, TraverseParms.For(___pawn, (Danger)3, (TraverseMode)0, false, false, false, true)) && Patch_Reachability_CanReach.CanReachThing(((LocalTargetInfo)(ref current)).Thing)))
					{
						if (AdvancedIO_PatchHelper.CanMoveItem(item2.Value, ((LocalTargetInfo)(ref current)).Cell))
						{
							item2.Value.AddItemToQueue(((LocalTargetInfo)(ref current)).Thing);
							item2.Value.UpdateQueue();
							break;
						}
						continue;
					}
					break;
				}
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(Pawn_TraderTracker), "ColonyThingsWillingToBuy")]
	internal class Patch_Pawn_TraderTracker_ColonyThingsWillingToBuy
	{
		private static void Postfix(Pawn playerNegotiator, ref IEnumerable<Thing> __result)
		{
			Map map = ((Thing)playerNegotiator).Map;
			if (map == null)
			{
				return;
			}
			HashSet<Thing> hashSet = new HashSet<Thing>();
			GenCollection.AddRange<Thing>(hashSet, __result);
			foreach (ILinkableStorageParent item in TradePatchHelper.AllPowered(map))
			{
				if (!item.AdvancedIOAllowed)
				{
					GenCollection.AddRange<Thing>(hashSet, item.StoredItems);
				}
			}
			__result = hashSet;
		}
	}
	internal class Patch_QualityBuilder_GetPawnConstructionSkill
	{
		public static bool Prefix(out int __result, Pawn pawn)
		{
			__result = 0;
			if (pawn is Pawn_Drone)
			{
				__result = pawn.skills.GetSkill(SkillDefOf.Construction).Level;
				return false;
			}
			return true;
		}
	}
	internal class Patch_QualityBuilder_PawnCanConstruct
	{
		public static void Postfix(Pawn pawn, ref bool __result)
		{
			if (!__result && pawn is Pawn_Drone)
			{
				Pawn_WorkSettings workSettings = pawn.workSettings;
				if (workSettings != null && workSettings.WorkIsActive(WorkTypeDefOf.Construction))
				{
					__result = true;
				}
			}
		}
	}
	internal class Patch_Reachability_CanReach
	{
		private static Thing canReachThing;

		public static bool CanReachThing(Thing thing)
		{
			bool result = thing == canReachThing;
			canReachThing = null;
			return result;
		}

		public static void Postfix(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams, ref bool __result, Map ___map, Reachability __instance)
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Invalid comparison between Unknown and I4
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			if (__result)
			{
				return;
			}
			Thing thing = ((LocalTargetInfo)(ref dest)).Thing;
			if (thing != null && (int)thing.def.category == 2)
			{
				PRFMapComponent pRFMapComponent = PatchStorageUtil.GetPRFMapComponent(___map);
				if (pRFMapComponent != null && HasPathToItem(thing, pRFMapComponent, __instance, start, traverseParams))
				{
					canReachThing = thing;
					__result = true;
				}
			}
		}

		private static bool HasPathToItem(Thing thing, PRFMapComponent mapComp, Reachability reachability, IntVec3 start, TraverseParms traverseParams)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 position = thing.Position;
			if (!mapComp.ShouldHideItemsAtPos(position))
			{
				return false;
			}
			Dictionary<IntVec3, Building_AdvancedStorageUnitIOPort> getAdvancedIOLocations = mapComp.GetAdvancedIOLocations;
			int count = getAdvancedIOLocations.Count;
			for (int i = 0; i < count; i++)
			{
				KeyValuePair<IntVec3, Building_AdvancedStorageUnitIOPort> keyValuePair = getAdvancedIOLocations.ElementAt(i);
				ILinkableStorageParent boundStorageUnit = keyValuePair.Value.boundStorageUnit;
				if (boundStorageUnit != null && boundStorageUnit.GetPosition == position && reachability.CanReach(start, LocalTargetInfo.op_Implicit(keyValuePair.Key), (PathEndMode)2, traverseParams))
				{
					return true;
				}
			}
			return false;
		}
	}
	[HarmonyPatch(typeof(RecipeWorkerCounter), "CountProducts")]
	internal class Patch_RecipeWorkerCounter_CountProducts
	{
		private static void Postfix(RecipeWorkerCounter __instance, ref int __result, Bill_Production bill)
		{
			if (bill.GetIncludeSlotGroup() != null)
			{
				return;
			}
			Map map = ((Bill)bill).Map;
			ThingDef thingDef = __instance.recipe.products[0].thingDef;
			PRFGameComponent component = Current.Game.GetComponent<PRFGameComponent>();
			for (int i = 0; i < component.AssemblerQueue.Count; i++)
			{
				if (map != component.AssemblerQueue[i].Map)
				{
					continue;
				}
				foreach (Thing item in component.AssemblerQueue[i].GetThingQueue())
				{
					TryUpdateResult(ref __result, thingDef, item);
				}
			}
			List<ILinkableStorageParent> list = ((IEnumerable<Building_ColdStorage>)PatchStorageUtil.GetPRFMapComponent(map).ColdStorageBuildings).Select((Func<Building_ColdStorage, ILinkableStorageParent>)((Building_ColdStorage b) => b)).ToList();
			foreach (ILinkableStorageParent item2 in list)
			{
				foreach (Thing storedItem in item2.StoredItems)
				{
					TryUpdateResult(ref __result, thingDef, storedItem);
				}
			}
		}

		private static void TryUpdateResult(ref int __result, ThingDef targetDef, Thing heldThing)
		{
			Thing innerIfMinified = MinifyUtility.GetInnerIfMinified(heldThing);
			if (innerIfMinified.def == targetDef)
			{
				__result += innerIfMinified.stackCount;
			}
		}
	}
	[HarmonyPatch]
	public class Patch_Thing_SmeltProducts
	{
		private static readonly Type HiddenClass = AccessTools.FirstInner(typeof(Thing), (Func<Type, bool>)((Type type) => GenAttribute.HasAttribute<CompilerGeneratedAttribute>((MemberInfo)type) && type.Name.Contains("SmeltProducts")));

		private static bool smeltableFound;

		public static bool RecyclerProducingItems = false;

		public static MethodBase TargetMethod()
		{
			if (HiddenClass == null)
			{
				Log.Error("Couldn't find iterator class -- This should never be reached.");
				return null;
			}
			MethodBase methodBase = AccessTools.Method(HiddenClass, "MoveNext", (Type[])null, (Type[])null);
			if (methodBase == null)
			{
				Log.Error("Couldn't find MoveNext");
			}
			return methodBase;
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (CodeInstruction instruction in instructions)
			{
				if (!smeltableFound)
				{
					if (instruction.opcode == OpCodes.Ldfld && instruction.operand.ToString().Contains("smeltable"))
					{
						smeltableFound = true;
						yield return new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Patch_Thing_SmeltProducts), "Smeltable", (Type[])null, (Type[])null));
					}
					else
					{
						yield return instruction;
					}
				}
				else
				{
					yield return instruction;
				}
			}
		}

		public static bool Smeltable(ThingDef def)
		{
			return RecyclerProducingItems || def.smeltable;
		}
	}
	[HarmonyPatch(typeof(Thing), "TryAbsorbStack")]
	public class Patch_Thing_TryAbsorbStack
	{
		private static PRFMapComponent prfMapComponent;

		private static bool relevant;

		private static int baseCount;

		public static bool Prefix(Thing __instance, Thing other)
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			prfMapComponent = PatchStorageUtil.GetPRFMapComponent(__instance.Map);
			relevant = prfMapComponent?.ShouldHideItemsAtPos(__instance.Position) ?? false;
			if (relevant)
			{
				baseCount = other.stackCount;
			}
			return true;
		}

		public static void Postfix(Thing other, bool respectStackLimit, Thing __instance)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			if (relevant)
			{
				__instance.Position.GetFirst<Building_MassStorageUnit>(__instance.Map)?.ItemCountsAdded(other.def, baseCount - other.stackCount);
			}
		}
	}
	[HarmonyPatch(typeof(TickManager), "DebugSetTicksGame")]
	public class Patch_TickManager_DebugSetTicksGame
	{
		public static bool Prefix(int newTicksGame)
		{
			List<Map> maps = Current.Game.Maps;
			foreach (Map item in maps)
			{
				item.GetComponent<MapTickManager>().HandleTimeSkip(newTicksGame);
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(TradeDeal), "InSellablePosition")]
	internal class Patch_TradeDeal_InSellablePosition
	{
		public static bool Prefix(Thing t, out string reason, ref bool __result)
		{
			if (!t.Spawned && t.MapHeld != null)
			{
				List<Building_ColdStorage> coldStorageBuildings = PatchStorageUtil.GetPRFMapComponent(t.MapHeld).ColdStorageBuildings;
				foreach (Building_ColdStorage item in coldStorageBuildings)
				{
					if (item.StoredItems.Contains(t))
					{
						reason = null;
						__result = true;
						return false;
					}
				}
			}
			else if (t.MapHeld == null)
			{
				Log.Warning($"Report to Rimfactory with HugsLog(CTRL & F12) - TradeDeal InSellablePosition {t}.MapHeld is Null");
			}
			reason = null;
			return true;
		}
	}
	internal class Patch_WealthWatcher_CalculateWealthItems
	{
		public static void Postfix(Map ___map, ref float __result)
		{
			List<Building_ColdStorage> coldStorageBuildings = PatchStorageUtil.GetPRFMapComponent(___map).ColdStorageBuildings;
			int count = coldStorageBuildings.Count;
			for (int i = 0; i < count; i++)
			{
				Building_ColdStorage building_ColdStorage = coldStorageBuildings[i];
				__result += building_ColdStorage.GetItemWealth();
			}
		}
	}
	[HarmonyPatch(typeof(Mineable), "TrySpawnYield", new Type[]
	{
		typeof(Map),
		typeof(bool),
		typeof(Pawn)
	})]
	internal static class Patch_Mineable_TrySpawnYield
	{
		private static void Prefix(Mineable __instance, Map map, bool moteOnWaste, Pawn pawn)
		{
			if (pawn is Pawn_Drone)
			{
				Patch_Pawn_IsColonist.OverrideIsColonist = true;
			}
		}

		private static void Postfix(Mineable __instance, Map map, bool moteOnWaste, Pawn pawn)
		{
			if (pawn is Pawn_Drone)
			{
				Patch_Pawn_IsColonist.OverrideIsColonist = false;
			}
		}
	}
	[HarmonyPatch(typeof(Pawn), "get_IsColonist")]
	internal static class Patch_Pawn_IsColonist
	{
		public static bool OverrideIsColonist;

		private static void Postfix(Pawn __instance, ref bool __result)
		{
			if (OverrideIsColonist && __instance is Pawn_Drone && !__result)
			{
				Faction faction = ((Thing)__instance).Faction;
				if (faction != null && faction.IsPlayer)
				{
					__result = true;
				}
			}
		}
	}
	[HarmonyPatch(typeof(ReservationUtility), "CanReserve")]
	internal class Patch_CanReserve_SAL
	{
		private static void Postfix(Pawn p, LocalTargetInfo target, bool ignoreOtherReservations, ref bool __result)
		{
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			if (!(__result && ignoreOtherReservations) || !((LocalTargetInfo)(ref target)).HasThing || ((LocalTargetInfo)(ref target)).Thing == null || p == null || p == PRFGameComponent.PRF_StaticPawn)
			{
				return;
			}
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (!(thing is Pawn))
			{
				IntVec3 interactionCell = thing.InteractionCell;
				List<Thing> thingList = GridsUtility.GetThingList(interactionCell, ((Thing)p).Map);
				Building_AutoMachineTool building_AutoMachineTool = (Building_AutoMachineTool)(object)GenCollection.FirstOrDefault<Thing>(thingList, (Predicate<Thing>)((Thing t) => t is Building_AutoMachineTool));
				if (building_AutoMachineTool != null)
				{
					__result = false;
				}
			}
		}
	}
	[HarmonyPatch(typeof(CompressibilityDeciderUtility), "IsSaveCompressible")]
	public static class SaveCompressiblePatch
	{
		public static void Postfix(Thing t, ref bool __result)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			if (__result && t.Map != null)
			{
				IntVec3 position = t.Position;
				if (((IntVec3)(ref position)).IsValid && t.Position.GetFirst<Building_MassStorageUnit>(t.Map) != null)
				{
					__result = false;
				}
			}
		}
	}
	[HarmonyPatch(typeof(Thing), "set_Position")]
	public static class SetPositionPatch
	{
		public static void Prefix(IntVec3 value, Thing __instance, out Building_MassStorageUnit __state)
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Invalid comparison between Unknown and I4
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			__state = null;
			IntVec3 position = __instance.Position;
			if ((int)__instance.def.category != 2 || !((IntVec3)(ref position)).IsValid)
			{
				return;
			}
			Map map = __instance.Map;
			if (map != null)
			{
				__state = position.GetFirst<Building_MassStorageUnit>(map);
				Building_MassStorageUnit obj = __state;
				if (obj != null && ((Thing)obj).Position == value)
				{
					__state = null;
				}
			}
		}

		public static void Postfix(Thing __instance, Building_MassStorageUnit __state)
		{
			if (__state != null)
			{
				((Building_Storage)__state).Notify_LostThing(__instance);
			}
		}
	}
	public interface IAssemblerQueue
	{
		Map Map { get; }

		List<Thing> GetThingQueue();
	}
	[HarmonyPatch(typeof(ResourceCounter), "UpdateResourceCounts")]
	internal class Patch_UpdateResourceCounts_AssemblerQueue
	{
		private static void Postfix(ResourceCounter __instance, Dictionary<ThingDef, int> ___countedAmounts, Map ___map)
		{
			PRFGameComponent component = Current.Game.GetComponent<PRFGameComponent>();
			for (int i = 0; i < component.AssemblerQueue.Count; i++)
			{
				IAssemblerQueue assemblerQueue = component.AssemblerQueue[i];
				if (assemblerQueue.Map != ___map)
				{
					continue;
				}
				List<Thing> thingQueue = assemblerQueue.GetThingQueue();
				foreach (Thing item in thingQueue)
				{
					Thing innerIfMinified = MinifyUtility.GetInnerIfMinified(item);
					if (innerIfMinified.def.EverStorable(false) && !RottableUtility.IsNotFresh(innerIfMinified) && ___countedAmounts.ContainsKey(innerIfMinified.def))
					{
						___countedAmounts[innerIfMinified.def] += innerIfMinified.stackCount;
					}
				}
			}
		}
	}
	[HarmonyPatch(typeof(ThingDef), "get_VolumePerUnit")]
	public class VolumePerUnitPatch
	{
		public static void Postfix(ThingDef __instance, ref float __result)
		{
			if (__instance == PRFDefOf.Paperclip)
			{
				__result = 1E-05f;
			}
		}
	}
}
namespace ProjectRimFactory.Common.BackCompatibility
{
	public class PRF_BackCompatibilityConverter : BackCompatibilityConverter
	{
		public override bool AppliesToVersion(int majorVer, int minorVer)
		{
			return true;
		}

		public override string BackCompatibleDefName(Type defType, string defName, bool forDefInjections = false, XmlNode node = null)
		{
			if (defType == typeof(ResearchProjectDef))
			{
				switch (defName)
				{
				case "PRF_StainlessSteelAlloying":
					return "PRF_Research_StainlessSteelAlloying";
				case "PRF_Carbon":
					return "PRF_Research_Carbon";
				case "PRF_CarbonNanotubes":
					return "PRF_Research_CarbonNanotubes";
				case "PRF_CarbonFiber":
					return "PRF_Research_CarbonFiber";
				case "PRF_CarbonKevlar":
					return "PRF_Research_CarbonKevlar";
				case "PRF_CarbonPlating":
					return "PRF_Research_CarbonPlating";
				case "PRF_SyntheticDiamond":
					return "PRF_Research_SyntheticDiamond";
				case "PRF_AdvancedFlooring":
					return "PRF_Research_AdvancedFlooring";
				case "PRF_CokeProduction":
					return "PRF_Research_CokeProduction";
				case "PRF_Brick":
					return "PRF_Research_Brick";
				case "PRF_BasicGlass":
					return "PRF_Research_BasicGlass";
				case "PRF_ReinforcedGlass":
					return "PRF_Research_ReinforcedGlass";
				case "PRF_Cement":
					return "PRF_Research_Cement";
				case "PRF_BasicConcrete":
					return "PRF_Research_BasicConcrete";
				case "PRF_BulkConcrete":
					return "PRF_Research_BulkConcrete";
				case "PRF_AdvancedConcrete":
					return "PRF_Research_AdvancedConcrete";
				case "PRF_BasicPlastics":
					return "PRF_Research_BasicPlastics";
				case "PRF_AdvPlastics":
					return "PRF_Research_AdvPlastics";
				case "PRF_BasicPlasticTextiles":
					return "PRF_Research_BasicPlasticTextiles";
				case "PRF_AdvPlasticTextiles":
					return "PRF_Research_AdvPlasticTextiles";
				case "PRF_GlitterPlasticTextiles":
					return "PRF_Research_GlitterPlasticTextiles";
				}
			}
			else if (defType == typeof(RecipeDef))
			{
				switch (defName)
				{
				case "K_DarkAndesite":
					return "PRF_Excavate_DarkAndesite";
				case "K_Anorthosite":
					return "PRF_Excavate_Anorthosite";
				case "K_Basalt":
					return "PRF_Excavate_Basalt";
				case "K_Blueschist":
					return "PRF_Excavate_Blueschist";
				case "K_Chalk":
					return "PRF_Excavate_Chalk";
				case "K_Charnockite":
					return "PRF_Excavate_Charnockite";
				case "K_CreoleMarble":
					return "PRF_Excavate_CreoleMarble";
				case "K_Dacite":
					return "PRF_Excavate_Dacite";
				case "K_VibrantDunite":
					return "PRF_Excavate_VibrantDunite";
				case "K_Emperadordark":
					return "PRF_Excavate_Emperadordark";
				case "K_EtowahMarble":
					return "PRF_Excavate_EtowahMarble";
				case "K_GreenGabbro":
					return "PRF_Excavate_GreenGabbro";
				case "K_GreenSchist":
					return "PRF_Excavate_GreenSchist";
				case "K_Jaspillite":
					return "PRF_Excavate_Jaspillite";
				case "K_Lepidolite":
					return "PRF_Excavate_Lepidolite";
				case "K_Lherzolite":
					return "PRF_Excavate_Lherzolite";
				case "K_Lignite":
					return "PRF_Excavate_Lignite";
				case "K_Migmatite":
					return "PRF_Excavate_Migmatite";
				case "K_Monzonite":
					return "PRF_Excavate_Monzonite";
				case "K_Obsidian":
					return "PRF_Excavate_Obsidian";
				case "K_Rhyolite":
					return "PRF_Excavate_Rhyolite";
				case "K_Scoria":
					return "PRF_Excavate_Scoria";
				case "K_Serpentinite":
					return "PRF_Excavate_Serpentinite";
				case "K_Siltstone":
					return "PRF_Excavate_Siltstone";
				case "K_Sovite":
					return "PRF_Excavate_Sovite";
				case "K_Thometzekite":
					return "PRF_Excavate_Thometzekite";
				}
			}
			return null;
		}

		public override Type GetBackCompatibleType(Type baseType, string providedClassName, XmlNode node)
		{
			return providedClassName switch
			{
				"ProjectRimFactory.AutoMachineTool.PRF_SAL_Trarget+Bill_ProductionWithUftPawnForbidded" => typeof(SAL_TargetWorktable.Bill_ProductionWithUftPawnForbidded), 
				"ProjectRimFactory.AutoMachineTool.PRF_SAL_Trarget+Bill_ProductionPawnForbidded" => typeof(SAL_TargetWorktable.Bill_ProductionPawnForbidded), 
				"ProjectRimFactory.AutoMachineTool.Building_Miner" => typeof(Building_Miner), 
				_ => null, 
			};
		}

		public override void PostExposeData(object obj)
		{
		}
	}
}
namespace ProjectRimFactory.AutoMachineTool
{
	public class Building_AutoMachineTool : Building_BaseRange<Building_AutoMachineTool>, IRecipeProductWorker
	{
		private bool forbidItem = false;

		private SAL_TargetBench salTarget = null;

		private ModExtension_Skills extension_Skills;

		public override bool ProductLimitationDisable => true;

		public ModExtension_ModifyProduct ModifyProductExt => ((Def)((Thing)this).def).GetModExtension<ModExtension_ModifyProduct>();

		protected override int? SkillLevel => ((Def)((Thing)this).def).GetModExtension<ModExtension_Tier>()?.skillLevel;

		public override bool Glowable => false;

		public Building_AutoMachineTool()
		{
			ForcePlace = false;
			targetEnumrationCount = 0;
		}

		public int GetSkillLevel(SkillDef skillDef)
		{
			return extension_Skills?.GetExtendedSkillLevel(skillDef, typeof(Building_AutoMachineTool)) ?? SkillLevel.GetValueOrDefault();
		}

		public override void ExposeData()
		{
			Scribe_Deep.Look<SAL_TargetBench>(ref salTarget, "salTarget", Array.Empty<object>());
			base.ExposeData();
			Scribe_Values.Look<bool>(ref forbidItem, "forbidItem", false, false);
		}

		private void UpdateVisuals()
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			if (GetTarget(((Thing)this).Position, ((Thing)this).Rotation, ((Thing)this).Map, spawned: true))
			{
				if (!(salTarget is SAL_TargetWorktable))
				{
					CompOutputAdjustable.Visible = false;
					PowerWorkSetting.RangeSettingHide = true;
				}
				else
				{
					CompOutputAdjustable.Visible = true;
					PowerWorkSetting.RangeSettingHide = false;
				}
			}
		}

		public bool GetTarget(IntVec3 pos, Rot4 rot, Map map, bool spawned = false)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Expected O, but got Unknown
			//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Expected O, but got Unknown
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e3: Expected O, but got Unknown
			//IL_0147: Unknown result type (might be due to invalid IL or missing references)
			//IL_0153: Unknown result type (might be due to invalid IL or missing references)
			//IL_0174: Unknown result type (might be due to invalid IL or missing references)
			//IL_0180: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
			List<Thing> list = (from t in GridsUtility.GetThingList(pos + ((Rot4)(ref rot)).FacingCell, map)
				where (int)t.def.category == 3
				where t.InteractionCell == pos
				select t).ToList();
			Building_WorkTable val = (Building_WorkTable)GenCollection.FirstOrDefault<Thing>(list, (Predicate<Thing>)((Thing t) => t is Building_WorkTable));
			Building val2 = (Building)GenCollection.FirstOrDefault<Thing>(list, (Predicate<Thing>)((Thing t) => t is Building && ThingCompUtility.TryGetComp<CompDeepDrill>(t) != null));
			Building_ResearchBench val3 = (Building_ResearchBench)GenCollection.FirstOrDefault<Thing>(list, (Predicate<Thing>)((Thing t) => t is Building_ResearchBench));
			if (spawned && ((salTarget is SAL_TargetWorktable && val == null) || (salTarget is SAL_TargetResearch && val3 == null) || (salTarget is SAL_TargetDeepDrill && val2 == null)))
			{
				salTarget.Free();
			}
			if (val != null)
			{
				salTarget = new SAL_TargetWorktable(this, ((Thing)this).Position, ((Thing)this).Map, ((Thing)this).Rotation, val);
			}
			else if (val2 != null)
			{
				salTarget = new SAL_TargetDeepDrill(this, ((Thing)this).Position, ((Thing)this).Map, ((Thing)this).Rotation, val2);
			}
			else if (val3 != null)
			{
				salTarget = new SAL_TargetResearch(this, ((Thing)this).Position, ((Thing)this).Map, ((Thing)this).Rotation, val3);
			}
			else
			{
				salTarget = null;
			}
			if (spawned && salTarget != null)
			{
				salTarget.Reserve();
			}
			return salTarget != null;
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			if (salTarget == null && base.State != WorkingState.Ready)
			{
				base.State = WorkingState.Ready;
			}
			base.SpawnSetup(map, respawningAfterLoad);
			if (salTarget == null)
			{
				UpdateVisuals();
			}
			extension_Skills = ((Def)((Thing)this).def).GetModExtension<ModExtension_Skills>();
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			salTarget?.Free();
			base.DeSpawn(mode);
			salTarget = null;
		}

		public override void PostMapInit()
		{
			base.PostMapInit();
			WorkTableSetting();
		}

		protected override void Reset()
		{
			salTarget?.Reset(base.State);
			base.Reset();
		}

		protected override void CleanupWorkingEffect()
		{
			base.CleanupWorkingEffect();
			salTarget?.CleanupWorkingEffect(base.MapManager);
		}

		protected override void CreateWorkingEffect()
		{
			base.CreateWorkingEffect();
			salTarget?.CreateWorkingEffect(base.MapManager);
		}

		protected override TargetInfo ProgressBarTarget()
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			return salTarget?.TargetInfo() ?? TargetInfo.Invalid;
		}

		private void WorkTableSetting()
		{
			if (salTarget == null)
			{
				UpdateVisuals();
				Reset();
			}
		}

		protected override void Ready()
		{
			WorkTableSetting();
			base.Ready();
		}

		private IntVec3 FacingCell()
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 position = ((Thing)this).Position;
			Rot4 rotation = ((Thing)this).Rotation;
			return position + ((Rot4)(ref rotation)).FacingCell;
		}

		protected override bool TryStartWorking(out Building_AutoMachineTool target, out float workAmount)
		{
			target = this;
			workAmount = 0f;
			if (salTarget == null)
			{
				UpdateVisuals();
			}
			if (salTarget == null || !salTarget.Ready())
			{
				return false;
			}
			return salTarget.TryStartWork(out workAmount);
		}

		protected override bool FinishWorking(Building_AutoMachineTool _, out List<Thing> outputProducts)
		{
			salTarget.WorkDone(out outputProducts);
			return true;
		}

		public override IntVec3 OutputCell()
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			return CompOutputAdjustable.CurrentCell;
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
		}

		public Room GetRoom(RegionType type)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			return RegionAndRoomQuery.GetRoom((Thing)(object)this, type);
		}

		private bool HasTargetWorktable()
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			return (from t in GridsUtility.GetThingList(FacingCell(), ((Thing)this).Map)
				where (int)t.def.category == 3 && t is Building_WorkTable
				select t).Any((Thing t) => t.InteractionCell == ((Thing)this).Position);
		}

		protected override bool WorkInterruption(Building_AutoMachineTool _)
		{
			if (salTarget == null || (salTarget is SAL_TargetWorktable && !HasTargetWorktable()))
			{
				return true;
			}
			return !salTarget.Ready();
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
	public enum WorkingState
	{
		Ready,
		Working,
		Placing
	}
	public interface IProductOutput
	{
		IntVec3 OutputCell();
	}
	public abstract class Building_Base<T> : PRF_Building, IProductOutput where T : Thing
	{
		private WorkingState state;

		protected T working;

		protected List<Thing> Products = new List<Thing>();

		private float totalWorkAmount;

		private int workStartTick;

		[Unsaved(false)]
		private Effecter progressBar;

		[Unsaved(false)]
		protected bool ShowProgressBar = true;

		[Unsaved(false)]
		protected bool ReadyOnStart = false;

		[Unsaved(false)]
		private const int StartCheckIntervalTicks = 30;

		protected CompOutputAdjustable CompOutputAdjustable;

		protected bool ForcePlace = true;

		protected MapTickManager MapManager { get; private set; }

		protected WorkingState State
		{
			get
			{
				return state;
			}
			set
			{
				state = value;
			}
		}

		protected T Working => working;

		protected virtual LookMode WorkingLookMode => (LookMode)3;

		protected virtual LookMode ProductsLookMode => (LookMode)2;

		private float CurrentWorkAmount => (float)(Find.TickManager.TicksAbs - workStartTick) * WorkAmountPerTick;

		protected float WorkLeft => totalWorkAmount - CurrentWorkAmount;

		protected abstract float WorkAmountPerTick { get; }

		public override void PostMake()
		{
			((ThingWithComps)this).PostMake();
			CompOutputAdjustable = ((ThingWithComps)this).GetComp<CompOutputAdjustable>();
		}

		public override Thing GetThingBy(Func<Thing, bool> optionalValidator = null)
		{
			foreach (Thing product in Products)
			{
				if (optionalValidator == null || optionalValidator(product))
				{
					Products.Remove(product);
					return product;
				}
			}
			return null;
		}

		protected virtual void ClearActions()
		{
			MapManager.RemoveAfterAction(Ready);
			MapManager.RemoveAfterAction(Placing);
			MapManager.RemoveAfterAction(CheckWork);
			MapManager.RemoveAfterAction(StartWork);
			MapManager.RemoveAfterAction(FinishWork);
		}

		public override void ExposeData()
		{
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Invalid comparison between Unknown and I4
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Invalid comparison between Unknown and I4
			base.ExposeData();
			Scribe_Values.Look<WorkingState>(ref state, "workingState", WorkingState.Ready, false);
			Scribe_Values.Look<float>(ref totalWorkAmount, "totalWorkAmount", 0f, false);
			Scribe_Values.Look<int>(ref workStartTick, "workStartTick", 0, false);
			Scribe_Collections.Look<Thing>(ref Products, "products", ProductsLookMode, Array.Empty<object>());
			if ((int)WorkingLookMode == 2)
			{
				Scribe_Deep.Look<T>(ref working, "working", Array.Empty<object>());
			}
			else if ((int)WorkingLookMode == 3)
			{
				Scribe_References.Look<T>(ref working, "working", false);
			}
		}

		public override void PostMapInit()
		{
			((ThingWithComps)this).PostMapInit();
			if (Products == null)
			{
				Products = new List<Thing>();
			}
			if (working == null && State == WorkingState.Working)
			{
				ForceReady();
			}
			if (Products.Count == 0 && State == WorkingState.Placing)
			{
				ForceReady();
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			MapManager = map.GetComponent<MapTickManager>();
			CompOutputAdjustable = ((ThingWithComps)this).GetComp<CompOutputAdjustable>();
			if (ReadyOnStart)
			{
				State = WorkingState.Ready;
				Reset();
				MapManager.AfterAction(Rand.Range(0, 30), Ready);
				return;
			}
			switch (State)
			{
			case WorkingState.Ready:
				MapManager.AfterAction(Rand.Range(0, 30), Ready);
				break;
			case WorkingState.Working:
				MapManager.NextAction(StartWork);
				break;
			case WorkingState.Placing:
				MapManager.NextAction(Placing);
				break;
			}
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			if (!GravshipUtility.generatingGravship)
			{
				Reset();
				ClearActions();
			}
			((Building)this).DeSpawn(mode);
		}

		protected virtual bool IsActive()
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			if (((Thing)this).Destroyed || !((Thing)this).Spawned)
			{
				return false;
			}
			return GenGrid.InBounds(OutputCell(), ((Thing)this).Map);
		}

		protected virtual void Reset()
		{
			if (State != WorkingState.Ready)
			{
				Products.ForEach(delegate(Thing t)
				{
					//IL_0029: Unknown result type (might be due to invalid IL or missing references)
					if (t != null && !t.Destroyed)
					{
						if (t.Spawned)
						{
							((Entity)t).DeSpawn((DestroyMode)0);
						}
						GenPlace.TryPlaceThing(t, ((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					}
				});
			}
			CleanupWorkingEffect();
			State = WorkingState.Ready;
			totalWorkAmount = 0f;
			workStartTick = 0;
			working = default(T);
			Products.Clear();
		}

		protected void ForceReady()
		{
			Reset();
			ClearActions();
			MapManager.NextAction(Ready);
		}

		protected virtual void CleanupWorkingEffect()
		{
			Ops.Option<Effecter>(progressBar).ForEach(delegate(Effecter e)
			{
				e.Cleanup();
			});
			progressBar = null;
		}

		protected virtual void CreateWorkingEffect()
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			CleanupWorkingEffect();
			if (!ShowProgressBar)
			{
				return;
			}
			Ops.Option<TargetInfo>(ProgressBarTarget()).ForEach(delegate
			{
				//IL_001e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0023: Unknown result type (might be due to invalid IL or missing references)
				//IL_003f: Unknown result type (might be due to invalid IL or missing references)
				progressBar = DefDatabase<EffecterDef>.GetNamed("AutoMachineTool_Effect_ProgressBar", true).Spawn();
				progressBar.EffectTick(ProgressBarTarget(), TargetInfo.Invalid);
				((MoteProgressBar2)(object)((SubEffecter_ProgressBar)progressBar.children[0]).mote).progressGetter = () => CurrentWorkAmount / totalWorkAmount;
			});
		}

		protected virtual TargetInfo ProgressBarTarget()
		{
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			if (((Thing)working).Spawned)
			{
				return TargetInfo.op_Implicit((Thing)(object)working);
			}
			return TargetInfo.op_Implicit((Thing)(object)this);
		}

		protected virtual void Ready()
		{
			if (State == WorkingState.Ready && ((Thing)this).Spawned)
			{
				if (!IsActive())
				{
					Reset();
					MapManager.AfterAction(30, Ready);
				}
				else if (TryStartWorking(out working, out totalWorkAmount))
				{
					State = WorkingState.Working;
					workStartTick = Find.TickManager.TicksAbs;
					MapManager.NextAction(StartWork);
				}
				else
				{
					MapManager.AfterAction(30, Ready);
				}
			}
		}

		private int CalcRemainTick()
		{
			if (float.IsInfinity(totalWorkAmount))
			{
				return int.MaxValue;
			}
			return Mathf.Max(1, Mathf.CeilToInt((totalWorkAmount - CurrentWorkAmount) / WorkAmountPerTick));
		}

		protected virtual void StartWork()
		{
			if (State != WorkingState.Working || !((Thing)this).Spawned)
			{
				return;
			}
			if (!IsActive())
			{
				ForceReady();
				return;
			}
			CreateWorkingEffect();
			MapManager.AfterAction(30, CheckWork);
			if (!float.IsInfinity(totalWorkAmount))
			{
				MapManager.AfterAction(CalcRemainTick(), FinishWork);
			}
		}

		protected virtual void ForceStartWork(T working, float workAmount)
		{
			Reset();
			ClearActions();
			State = WorkingState.Working;
			this.working = working;
			totalWorkAmount = workAmount;
			workStartTick = Find.TickManager.TicksAbs;
			MapManager.NextAction(StartWork);
		}

		protected virtual void CheckWork()
		{
			if (State == WorkingState.Working && ((Thing)this).Spawned)
			{
				if (!IsActive())
				{
					ForceReady();
				}
				else if (WorkInterruption(working))
				{
					ForceReady();
				}
				else if (CurrentWorkAmount >= totalWorkAmount)
				{
					MapManager.NextAction(FinishWork);
				}
				else
				{
					MapManager.AfterAction(30, CheckWork);
				}
			}
		}

		protected virtual void FinishWork()
		{
			if (State != WorkingState.Working || !((Thing)this).Spawned)
			{
				return;
			}
			MapManager.RemoveAfterAction(CheckWork);
			MapManager.RemoveAfterAction(FinishWork);
			if (!IsActive())
			{
				ForceReady();
			}
			else if (WorkInterruption(working))
			{
				ForceReady();
			}
			else if (FinishWorking(working, out Products))
			{
				State = WorkingState.Placing;
				CleanupWorkingEffect();
				working = default(T);
				if (Products == null || Products.Count == 0)
				{
					Reset();
					MapManager.NextAction(Ready);
				}
				else
				{
					MapManager.NextAction(Placing);
				}
			}
			else
			{
				Reset();
				MapManager.NextAction(Ready);
			}
		}

		protected virtual void Placing()
		{
			if (State == WorkingState.Placing && ((Thing)this).Spawned)
			{
				if (!IsActive())
				{
					ForceReady();
				}
				else if (PlaceProduct(ref Products))
				{
					State = WorkingState.Ready;
					Reset();
					MapManager.NextAction(Ready);
				}
				else if (State == WorkingState.Placing)
				{
					MapManager.AfterAction(30, Placing);
				}
			}
		}

		protected abstract bool WorkInterruption(T working);

		protected abstract bool TryStartWorking(out T target, out float workAmount);

		protected abstract bool FinishWorking(T working, out List<Thing> outputProducts);

		protected virtual bool PlaceProduct(ref List<Thing> things)
		{
			things = things.Aggregate(new List<Thing>(), delegate(List<Thing> total, Thing target)
			{
				//IL_0016: Unknown result type (might be due to invalid IL or missing references)
				if (target.Spawned)
				{
					((Entity)target).DeSpawn((DestroyMode)0);
				}
				return this.PRFTryPlaceThing(target, OutputCell(), ((Thing)this).Map, ForcePlace) ? total : total.Append(target);
			});
			return !GenCollection.Any<Thing>(Products);
		}

		public override IntVec3 OutputCell()
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 position = ((Thing)this).Position;
			IntVec2 size = ((BuildableDef)((Thing)this).def).Size;
			Rot4 rotation = ((Thing)this).Rotation;
			return Ops.FacingCell(position, size, ((Rot4)(ref rotation)).Opposite);
		}

		public override string GetInspectString()
		{
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0138: Unknown result type (might be due to invalid IL or missing references)
			//IL_013d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0142: Unknown result type (might be due to invalid IL or missing references)
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0114: Unknown result type (might be due to invalid IL or missing references)
			string inspectString = ((ThingWithComps)this).GetInspectString();
			inspectString += "\n";
			switch (State)
			{
			case WorkingState.Working:
				if (float.IsInfinity(totalWorkAmount))
				{
					return TaggedString.op_Implicit(inspectString + Translator.Translate("PRF.AutoMachineTool.StatWorkingNotParam"));
				}
				return TaggedString.op_Implicit(inspectString + TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.StatWorking", NamedArgument.op_Implicit(Mathf.RoundToInt(Math.Min(CurrentWorkAmount, totalWorkAmount))), NamedArgument.op_Implicit(Mathf.RoundToInt(totalWorkAmount)), NamedArgument.op_Implicit(Mathf.RoundToInt(Mathf.Clamp01(CurrentWorkAmount / totalWorkAmount) * 100f))));
			case WorkingState.Ready:
				return TaggedString.op_Implicit(inspectString + Translator.Translate("PRF.AutoMachineTool.StatReady"));
			case WorkingState.Placing:
				if (Products.Count != 1)
				{
					return TaggedString.op_Implicit(inspectString + TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.StatPlacing", NamedArgument.op_Implicit(Products.Count)));
				}
				return TaggedString.op_Implicit(inspectString + TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.PlacingSingle", NamedArgument.op_Implicit(((Entity)Products[0]).Label)));
			default:
				return inspectString + State;
			}
		}

		public void NortifyReceivable()
		{
			if (State == WorkingState.Placing && ((Thing)this).Spawned && !MapManager.IsExecutingThisTick(Placing))
			{
				MapManager.RemoveAfterAction(Placing);
				MapManager.NextAction(Placing);
			}
		}
	}
	public abstract class Building_BaseLimitation<T> : Building_BaseMachine<T>, IProductLimitation where T : Thing
	{
		private SlotGroup targetSlotGroup = null;

		private int productLimitCount = 100;

		private bool productLimitation = false;

		private bool countStacks = false;

		private ILoadReferenceable slotGroupParent = null;

		private string slotGroupParentLabel = null;

		public int ProductLimitCount
		{
			get
			{
				return productLimitCount;
			}
			set
			{
				productLimitCount = value;
			}
		}

		public bool ProductLimitation
		{
			get
			{
				return productLimitation;
			}
			set
			{
				productLimitation = value;
			}
		}

		public SlotGroup TargetSlotGroup
		{
			get
			{
				return targetSlotGroup;
			}
			set
			{
				targetSlotGroup = value;
			}
		}

		public bool CountStacks
		{
			get
			{
				return countStacks;
			}
			set
			{
				countStacks = value;
			}
		}

		public virtual bool ProductLimitationDisable => false;

		public override void ExposeData()
		{
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Invalid comparison between Unknown and I4
			base.ExposeData();
			Scribe_Values.Look<int>(ref productLimitCount, "productLimitCount", 100, false);
			Scribe_Values.Look<bool>(ref productLimitation, "productLimitation", false, false);
			Scribe_Values.Look<bool>(ref countStacks, "countStacks", false, false);
			if ((int)Scribe.mode == 1)
			{
				SlotGroup obj = targetSlotGroup;
				object obj2;
				if (obj == null)
				{
					obj2 = null;
				}
				else
				{
					ISlotGroupParent parent = obj.parent;
					obj2 = ((parent != null) ? parent.SlotYielderLabel() : null);
				}
				slotGroupParentLabel = (string)obj2;
				ISlotGroupParent obj3 = targetSlotGroup?.parent;
				slotGroupParent = (ILoadReferenceable)(object)((obj3 is ILoadReferenceable) ? obj3 : null);
			}
			Scribe_References.Look<ILoadReferenceable>(ref slotGroupParent, "slotGroupParent", false);
			Scribe_Values.Look<string>(ref slotGroupParentLabel, "slotGroupParentLabel", (string)null, false);
		}

		public override void PostMapInit()
		{
			targetSlotGroup = (from g in ((Thing)this).Map.haulDestinationManager.AllGroups
				where g.parent.SlotYielderLabel() == slotGroupParentLabel
				where Ops.Option<ILoadReferenceable>(slotGroupParent).Fold(defaultValue: true)((ILoadReferenceable p) => (object)p == g.parent)
				select g).FirstOption().Value;
			base.PostMapInit();
		}

		public bool IsLimit(Thing thing)
		{
			if (!productLimitation)
			{
				return false;
			}
			if (targetSlotGroup == null)
			{
				return CountFromMap(thing.def) >= productLimitCount;
			}
			if (targetSlotGroup.parent is ILimitWatcher limitWatcher)
			{
				if (limitWatcher.ItemIsLimit(thing.def, countStacks, productLimitCount))
				{
					return true;
				}
			}
			else if (CheckSlotGroup(targetSlotGroup, thing.def, productLimitCount))
			{
				return true;
			}
			PatchStorageUtil.SkippAcceptsPatch = true;
			bool result = !GenCollection.Any<IntVec3>(targetSlotGroup.CellsList, (Predicate<IntVec3>)((IntVec3 c) => StoreUtility.IsValidStorageFor(c, ((Thing)this).Map, thing)));
			PatchStorageUtil.SkippAcceptsPatch = false;
			return result;
		}

		private int CountFromMap(ThingDef thingDef)
		{
			return countStacks ? ((Thing)this).Map.listerThings.ThingsOfDef(thingDef).Count : ((Thing)this).Map.resourceCounter.GetCount(thingDef);
		}

		private bool CheckSlotGroup(SlotGroup slotGroup, ThingDef thingDef, int limit = int.MaxValue)
		{
			int num = 0;
			foreach (Thing heldThing in slotGroup.HeldThings)
			{
				if (heldThing.def == thingDef)
				{
					num = ((!countStacks) ? (num + heldThing.stackCount) : (num + 1));
					if (num >= limit)
					{
						return true;
					}
				}
			}
			return false;
		}
	}
	public abstract class Building_BaseMachine<T> : Building_Base<T>, IPowerSupplyMachineHolder, IBeltConveyorSender where T : Thing
	{
		public CompPowerWorkSetting PowerWorkSetting;

		private CompPowerTrader powerComp;

		public IPowerSupplyMachine RangePowerSupplyMachine => PowerWorkSetting;

		protected virtual int? SkillLevel => null;

		protected override float WorkAmountPerTick => 10f * PowerWorkSetting.GetSpeedFactor();

		public virtual bool Glowable => false;

		public virtual bool Glow
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			PowerWorkSetting = ((ThingWithComps)this).GetComp<CompPowerWorkSetting>();
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			powerComp = ThingCompUtility.TryGetComp<CompPowerTrader>((Thing)(object)this);
			PowerWorkSetting = ((ThingWithComps)this).GetComp<CompPowerWorkSetting>();
			base.MapManager.NextAction(PowerWorkSetting.RefreshPowerStatus);
			base.MapManager.AfterAction(5, PowerWorkSetting.RefreshPowerStatus);
		}

		protected override bool IsActive()
		{
			CompPowerTrader val = powerComp;
			return val != null && val.PowerOn && base.IsActive();
		}

		protected override void ReceiveCompSignal(string signal)
		{
			if ((signal == "PowerTurnedOff" || signal == "PowerTurnedOn") ? true : false)
			{
				PowerWorkSetting.RefreshPowerStatus();
			}
		}
	}
	public interface IRange
	{
		IntVec3 Position { get; }

		IntVec2 Size { get; }

		Rot4 Rotation { get; }

		int GetRange();

		IEnumerable<IntVec3> GetAllTargetCells();
	}
	public abstract class Building_BaseRange<T> : Building_BaseLimitation<T>, IRange, IPowerSupplyMachineHolder where T : Thing
	{
		private bool glow = false;

		private float supplyPowerForRange;

		[Unsaved(false)]
		protected int targetEnumrationCount = 100;

		[Unsaved(false)]
		private bool nextTargetCells = false;

		[Unsaved(false)]
		private HashSet<IntVec3> allTargetCellsCache;

		[Unsaved(false)]
		private List<List<IntVec3>> splittedTargetCells;

		[Unsaved(false)]
		private int splittedTargetCellsIndex = 0;

		public override bool Glowable => false;

		public override bool Glow
		{
			get
			{
				return glow;
			}
			set
			{
				if (glow != value)
				{
					glow = value;
					ChangeGlow();
				}
			}
		}

		public IntVec2 Size => ((BuildableDef)((Thing)this).def).Size;

		private bool SplitTargetCells => targetEnumrationCount > 0 && GetAllTargetCells().Count() > targetEnumrationCount;

		public IEnumerable<IntVec3> GetAllTargetCells()
		{
			CacheTargetCells();
			IEnumerable<IntVec3> rangeCells = PowerWorkSetting.GetRangeCells();
			return ((rangeCells != null) ? rangeCells.ToHashSet() : null) ?? new HashSet<IntVec3>();
		}

		private void CacheTargetCells()
		{
			if (allTargetCellsCache == null)
			{
				IEnumerable<IntVec3> rangeCells = PowerWorkSetting.GetRangeCells();
				allTargetCellsCache = ((rangeCells != null) ? rangeCells.ToHashSet() : null) ?? new HashSet<IntVec3>();
				if (targetEnumrationCount > 0)
				{
					splittedTargetCells = allTargetCellsCache.ToList().Grouped(targetEnumrationCount);
				}
			}
		}

		private List<IntVec3> GetCurrentSplittedTargetCells()
		{
			CacheTargetCells();
			if (splittedTargetCellsIndex >= splittedTargetCells.Count)
			{
				splittedTargetCellsIndex = 0;
			}
			return splittedTargetCells[splittedTargetCellsIndex];
		}

		private void NextSplittedTargetCells()
		{
			splittedTargetCellsIndex++;
			if (splittedTargetCellsIndex >= splittedTargetCells.Count)
			{
				splittedTargetCellsIndex = 0;
			}
		}

		private void ClearAllTargetCellCache()
		{
			if (IsActive())
			{
				allTargetCellsCache = null;
			}
			if (!((Thing)this).Spawned)
			{
			}
		}

		protected override void ClearActions()
		{
			base.ClearActions();
			base.MapManager.RemoveAfterAction(ClearAllTargetCellCache);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look<float>(ref supplyPowerForRange, "supplyPowerForRange", 0f, false);
			Scribe_Values.Look<bool>(ref glow, "glow", false, false);
		}

		private void ChangeGlow()
		{
			Ops.Option<CompGlower>(ThingCompUtility.TryGetComp<CompGlower>((Thing)(object)this)).ForEach(delegate(CompGlower glower)
			{
				bool switchIsOn = ThingCompUtility.TryGetComp<CompFlickable>((Thing)(object)this).SwitchIsOn;
				glower.Props.glowRadius = (Glow ? (((float)GetRange() + 2f) * 2f) : 0f);
				glower.Props.overlightRadius = (Glow ? ((float)GetRange() + 2.1f) : 0f);
				ThingCompUtility.TryGetComp<CompFlickable>((Thing)(object)this).SwitchIsOn = !switchIsOn;
				glower.UpdateLit(((Thing)this).Map);
				ThingCompUtility.TryGetComp<CompFlickable>((Thing)(object)this).SwitchIsOn = switchIsOn;
				glower.UpdateLit(((Thing)this).Map);
			});
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			Ops.Option<CompGlower>(ThingCompUtility.TryGetComp<CompGlower>((Thing)(object)this)).ForEach(delegate(CompGlower g)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Unknown result type (might be due to invalid IL or missing references)
				//IL_001e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0023: Unknown result type (might be due to invalid IL or missing references)
				//IL_0028: Unknown result type (might be due to invalid IL or missing references)
				//IL_0039: Unknown result type (might be due to invalid IL or missing references)
				//IL_004b: Expected O, but got Unknown
				CompProperties_Glower props = new CompProperties_Glower
				{
					compClass = ((CompProperties)g.Props).compClass,
					glowColor = g.Props.glowColor,
					glowRadius = g.Props.glowRadius,
					overlightRadius = g.Props.overlightRadius
				};
				((ThingComp)g).props = (CompProperties)(object)props;
			});
			allTargetCellsCache = null;
			ChangeGlow();
		}

		public int GetRange()
		{
			Log.ErrorOnce("ERROR Sniper needs to fix C# Building_BaseRange GetRange()", 684546864);
			return 0;
		}

		protected virtual IEnumerable<IntVec3> GetTargetCells()
		{
			if (!SplitTargetCells)
			{
				return GetAllTargetCells();
			}
			nextTargetCells = true;
			return GetCurrentSplittedTargetCells();
		}

		protected override void Ready()
		{
			base.Ready();
			if (base.State == WorkingState.Ready && SplitTargetCells && nextTargetCells)
			{
				NextSplittedTargetCells();
				nextTargetCells = false;
			}
		}
	}
	public enum DirectionPriority
	{
		VeryHigh = 4,
		High = 3,
		Normal = 2,
		Low = 1
	}
	public static class DirectionPriorityExtension
	{
		public static string ToText(this DirectionPriority pri)
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Conveyor.DirectionPriority." + pri));
		}
	}
	[StaticConstructorOnStartup]
	public class Building_BeltConveyor : Building_BaseMachine<Thing>, IBeltConveyorLinkable, IPRF_Building, ILoadReferenceable, IThingHolder, IHideItem, IHideRightClickMenu
	{
		protected ThingOwner_Conveyor thingOwnerInt;

		protected static bool usingThingOwnerInt;

		public static float supplyPower;

		protected bool stuck = false;

		private bool isEndOfLine = false;

		protected readonly float stuckDrawPercent = 0.3f;

		protected const float defaultCarriedItemScale = 0.75f;

		protected const float defaultCarriedItemDrawHeight = 0.15f;

		protected float carriedItemDrawHeight = 0.15f;

		protected ModExtension_Conveyor modExtension_Conveyor = null;

		private Graphic drawCarried_GraphicBase = null;

		private Graphic drawCarried_GraphicDB = null;

		protected virtual Rot4 OutputDirection => ((Thing)this).Rotation;

		public bool IsStuck => stuck;

		public bool IsUnderground => modExtension_Conveyor?.underground ?? false;

		public virtual bool IsEndOfLine
		{
			get
			{
				return isEndOfLine;
			}
			set
			{
				//IL_006a: Unknown result type (might be due to invalid IL or missing references)
				if (isEndOfLine && !value)
				{
					isEndOfLine = false;
					if (working != null)
					{
						ForceStartWork(working, 1f);
					}
					else
					{
						Reset();
					}
				}
				else
				{
					isEndOfLine = value;
				}
				if (((Thing)this).Spawned)
				{
					((Thing)this).Map.mapDrawer.MapMeshDirty(((Thing)this).Position, MapMeshFlagDef.op_Implicit(MapMeshFlagDefOf.Buildings) | MapMeshFlagDef.op_Implicit(MapMeshFlagDefOf.Things));
				}
			}
		}

		public bool HideRightClickMenus => !IsUnderground && base.State != WorkingState.Ready;

		protected override LookMode WorkingLookMode => (LookMode)3;

		protected override LookMode ProductsLookMode => (LookMode)3;

		public bool HideItems => !IsUnderground && base.State != WorkingState.Ready;

		public virtual IEnumerable<Rot4> ActiveOutputDirections
		{
			get
			{
				yield return ((Thing)this).Rotation;
			}
		}

		public float CarriedItemDrawHeight => GenThing.TrueCenter((Thing)(object)this).y + 0.15f;

		public override PRFBSetting SettingsOptions => base.SettingsOptions & ~PRFBSetting.optionObeysStorageFilters;

		public Building_BeltConveyor()
		{
			thingOwnerInt = new ThingOwner_Conveyor((IThingHolder)(object)this);
			Products = ((ThingOwner<Thing>)thingOwnerInt).InnerListForReading;
			obeysStorageFilters = false;
		}

		public override IntVec3 OutputCell()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 position = ((Thing)this).Position;
			Rot4 outputDirection = OutputDirection;
			return position + ((Rot4)(ref outputDirection)).FacingCell;
		}

		public override Thing GetThingBy(Func<Thing, bool> optionalValidator = null)
		{
			if (IsUnderground || base.State == WorkingState.Ready)
			{
				return null;
			}
			if (optionalValidator == null || optionalValidator(Products[0]))
			{
				base.State = WorkingState.Placing;
				Thing result = ((ThingOwner<Thing>)thingOwnerInt).Take(Products[0]);
				working = null;
				ForceReady();
				return result;
			}
			return null;
		}

		public virtual void Link(IBeltConveyorLinkable link)
		{
		}

		public virtual void Unlink(IBeltConveyorLinkable unlink)
		{
		}

		public virtual bool CanSendToLevel(ConveyorLevel level)
		{
			if (IsUnderground)
			{
				if (level == ConveyorLevel.Underground)
				{
					return true;
				}
			}
			else if (level == ConveyorLevel.Ground)
			{
				return true;
			}
			return false;
		}

		public virtual bool CanReceiveFromLevel(ConveyorLevel level)
		{
			return CanSendToLevel(level);
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			Enumerable.Empty<IThingHolder>();
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return (ThingOwner)(object)thingOwnerInt;
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			if (!GenList.NullOrEmpty<Thing>((IList<Thing>)Products))
			{
				Thing dropThing = Products[0];
				yield return (Gizmo)new Command_Action
				{
					defaultLabel = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF_DropThing", NamedArgument.op_Implicit(((Entity)dropThing).Label))),
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRF_DropFromConveyorDesc")),
					icon = (Texture)(Texture2D)dropThing.Graphic.MatSingleFor(dropThing).mainTexture,
					action = delegate
					{
						DropThing(dropThing);
						Find.Selector.ClearSelection();
						Find.Selector.Select((object)dropThing, true, true);
					}
				};
			}
		}

		private void DropThing(Thing t)
		{
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			if (Products == null || !Products.Contains(t))
			{
				Messages.Message("Error: Conveyor " + ((object)this)?.ToString() + " does not have " + ((Entity)t).Label, LookTargets.op_Implicit((Thing)(object)this), MessageTypeDefOf.RejectInput, true);
				return;
			}
			((ThingOwner<Thing>)thingOwnerInt).Take(t);
			if (GenPlace.TryPlaceThing(t, ((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)delegate(IntVec3 c)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0016: Unknown result type (might be due to invalid IL or missing references)
				if (GenGrid.Impassable(c, ((Thing)this).Map))
				{
					return false;
				}
				foreach (IBeltConveyorLinkable item in GridsUtility.GetThingList(c, ((Thing)this).Map).OfType<IBeltConveyorLinkable>())
				{
					if (!item.IsUnderground)
					{
						return false;
					}
				}
				return true;
			}, (Rot4?)null, 1))
			{
				working = null;
				Products.Remove(t);
				ForceReady();
			}
			else
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF_CouldNotPlaceThing", NamedArgument.op_Implicit(((Entity)t).Label))), LookTargets.op_Implicit((Thing)(object)this), MessageTypeDefOf.NegativeEvent, true);
				((ThingOwner)thingOwnerInt).TryAdd(t, true);
			}
		}

		public void Notify_LostItem(Thing item)
		{
			if ((object)item.holdingOwner != thingOwnerInt && !((ThingOwner)thingOwnerInt).Any && base.State != WorkingState.Ready && !usingThingOwnerInt)
			{
				Reset();
			}
		}

		public override void ExposeData()
		{
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Invalid comparison between Unknown and I4
			base.ExposeData();
			Scribe_Values.Look<bool>(ref stuck, "isStuck", false, false);
			Scribe_Values.Look<float>(ref supplyPower, "supplyPower", 10f, false);
			Scribe_Values.Look<bool>(ref isEndOfLine, "isEOL", false, false);
			Scribe_Deep.Look<ThingOwner_Conveyor>(ref thingOwnerInt, "thingOwner", new object[1] { this });
			if ((int)Scribe.mode == 4)
			{
				if (thingOwnerInt == null)
				{
					thingOwnerInt = new ThingOwner_Conveyor((IThingHolder)(object)this);
				}
				Products = ((ThingOwner<Thing>)thingOwnerInt).InnerListForReading;
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			modExtension_Conveyor = ((Def)((Thing)this).def).GetModExtension<ModExtension_Conveyor>();
			ShowProgressBar = false;
			foreach (IBeltConveyorLinkable item in AllNearbyLinkables())
			{
				item.Link(this);
				Link(item);
			}
			Products = ((ThingOwner<Thing>)thingOwnerInt).InnerListForReading;
			PatchStorageUtil.GetPRFMapComponent(((Thing)this).Map).NextBeltCache.Clear();
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			List<IBeltConveyorLinkable> list = AllNearbyLinkables().ToList();
			PatchStorageUtil.GetPRFMapComponent(((Thing)this).Map).NextBeltCache.Clear();
			base.DeSpawn(mode);
			if (!GravshipUtility.generatingGravship)
			{
				list.ForEach(delegate(IBeltConveyorLinkable x)
				{
					x.Unlink(this);
				});
			}
		}

		public override bool CanStackWith(Thing other)
		{
			return ((ThingWithComps)this).CanStackWith(other) && base.State == WorkingState.Ready;
		}

		public override void DrawGUIOverlay()
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Invalid comparison between Unknown and I4
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).DrawGUIOverlay();
			if (!IsUnderground || OverlayDrawHandler_UGConveyor.ShouldDraw)
			{
				Thing val = CarryingThing();
				if (val != null && (int)Find.CameraDriver.CurrentZoom <= 0)
				{
					Vector2 val2 = Vector2.op_Implicit(Find.Camera.WorldToScreenPoint(CarryPosition() + new Vector3(0f, 0f, -0.4f)) / Prefs.UIScale);
					val2.y = (float)UI.screenHeight - val2.y;
					GenMapUI.DrawThingLabel(val2, GenString.ToStringCached(val.stackCount), GenMapUI.DefaultThingLabelColor);
				}
			}
		}

		public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Invalid comparison between Unknown and I4
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			if ((int)phase == 2 && (!IsUnderground || OverlayDrawHandler_UGConveyor.ShouldDraw))
			{
				((Thing)this).DynamicDrawPhaseAt(phase, drawLoc, flip);
				if (base.State != WorkingState.Ready)
				{
					DrawCarried();
				}
			}
		}

		public virtual void DrawCarried()
		{
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00db: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			Thing val = CarryingThing();
			if (val == null)
			{
				return;
			}
			float num = modExtension_Conveyor?.carriedItemScale ?? 0.75f;
			if (drawCarried_GraphicBase == null)
			{
				drawCarried_GraphicBase = GraphicUtility.ExtractInnerGraphicFor(val.Graphic, val, (int?)null);
			}
			GraphicData val2 = val.def.graphicData ?? drawCarried_GraphicBase.data;
			if (val2 == null)
			{
				val.DrawNowAt(CarryPosition(), false);
				return;
			}
			if (drawCarried_GraphicDB == null)
			{
				drawCarried_GraphicDB = GraphicDatabase.Get(val2.graphicClass, val2.texPath, drawCarried_GraphicBase.Shader, new Vector2(num, num), drawCarried_GraphicBase.color, drawCarried_GraphicBase.colorTwo, (string)null);
			}
			drawCarried_GraphicDB.Draw(CarryPosition(), val.Rotation, val, 0f);
		}

		protected Vector3 CarryPosition()
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			Vector3 val = GenThing.TrueCenter((Thing)(object)this) + new Vector3(0f, carriedItemDrawHeight, 0f);
			if (IsEndOfLine)
			{
				return val;
			}
			Rot4 outputDirection = OutputDirection;
			IntVec3 facingCell = ((Rot4)(ref outputDirection)).FacingCell;
			return val + ((IntVec3)(ref facingCell)).ToVector3() * (1f - Mathf.Clamp01(base.WorkLeft));
		}

		protected void CalculateCarriedItemDrawHeight()
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			IBeltConveyorLinkable beltConveyorLinkable = OutputBelt();
			if (beltConveyorLinkable != null)
			{
				float num = beltConveyorLinkable.CarriedItemDrawHeight;
				float num2 = CarriedItemDrawHeight;
				if (num2 < num)
				{
					carriedItemDrawHeight = num - GenThing.TrueCenter((Thing)(object)this).y;
					return;
				}
			}
			carriedItemDrawHeight = 0.15f;
		}

		public override string GetInspectString()
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			if (IsEndOfLine)
			{
				return TaggedString.op_Implicit(base.GetInspectString() + "\n" + Translator.Translate("PRF.Conveyor.IsAtEndOfLine"));
			}
			return base.GetInspectString();
		}

		public override bool AcceptsThing(Thing newThing, IPRF_Building giver = null)
		{
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			if (!IsActive())
			{
				return false;
			}
			if (giver is IBeltConveyorLinkable otherBeltLinkable)
			{
				if (!CanLinkFrom(otherBeltLinkable, checkPosition: false))
				{
					return false;
				}
			}
			else if (IsUnderground)
			{
				return false;
			}
			if (base.State == WorkingState.Ready)
			{
				if (newThing.Spawned)
				{
					((Entity)newThing).DeSpawn((DestroyMode)0);
				}
				newThing.Position = ((Thing)this).Position;
				stuck = false;
				ForceStartWork(newThing, 1f);
				return true;
			}
			Thing val;
			if (base.State == WorkingState.Working)
			{
				val = base.Working;
			}
			else
			{
				if (Products.Count == 0)
				{
					Log.Error("PRF Belt - products List is Empty");
					Reset();
				}
				val = Products[0];
			}
			return val.TryAbsorbStack(newThing, true);
		}

		public bool CanAcceptNow(Thing thing)
		{
			if (!IsActive())
			{
				return false;
			}
			return base.State switch
			{
				WorkingState.Ready => true, 
				WorkingState.Working => ThisCanAcceptThat(base.Working, thing), 
				WorkingState.Placing => Products.Count == 0 || ThisCanAcceptThat(Products[0], thing), 
				_ => false, 
			};
		}

		private static bool ThisCanAcceptThat(Thing thing1, Thing thing2)
		{
			if (thing1 == null || thing2 == null)
			{
				Log.Error($"PRF ThisCanAcceptThat On thing is Null! t1: {thing1} t2: {thing2}");
				return false;
			}
			return thing1.CanStackWith(thing2) && thing1.stackCount < thing1.def.stackLimit;
		}

		protected override bool TryStartWorking(out Thing target, out float workAmount)
		{
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			CalculateCarriedItemDrawHeight();
			workAmount = 1f;
			if (IsUnderground)
			{
				target = null;
				return false;
			}
			target = GenCollection.FirstOrDefault<Thing>(GridsUtility.GetThingList(((Thing)this).Position, ((Thing)this).Map), (Predicate<Thing>)((Thing t) => t.def.EverHaulable));
			if (target != null)
			{
				if (target.Spawned)
				{
					((Entity)target).DeSpawn((DestroyMode)0);
				}
				target.Position = ((Thing)this).Position;
			}
			if (target != null)
			{
				((ThingOwner)thingOwnerInt).TryAdd(target, true);
			}
			return target != null;
		}

		protected override void ForceStartWork(Thing working, float workAmount)
		{
			CalculateCarriedItemDrawHeight();
			if (((ThingOwner)thingOwnerInt).Contains(working))
			{
				((ThingOwner)thingOwnerInt).Remove(working);
			}
			base.ForceStartWork(working, workAmount);
			((ThingOwner)thingOwnerInt).TryAdd(working, true);
		}

		protected override void StartWork()
		{
			base.StartWork();
			base.MapManager.RemoveAfterAction(CheckWork);
			CheckWork();
		}

		protected override void Reset()
		{
			if (((ThingOwner)thingOwnerInt).Any)
			{
				Products = ((ThingOwner<Thing>)thingOwnerInt).InnerListForReading.ToList();
				((ThingOwner)thingOwnerInt).Clear();
			}
			usingThingOwnerInt = false;
			base.Reset();
			Products = ((ThingOwner<Thing>)thingOwnerInt).InnerListForReading;
			base.MapManager.RemoveAfterAction(CheckWork);
			base.MapManager.RemoveAfterAction(Placing);
			base.MapManager.RemoveAfterAction(StartWork);
			base.MapManager.RemoveAfterAction(FinishWork);
		}

		protected sealed override bool PlaceProduct(ref List<Thing> things)
		{
			drawCarried_GraphicBase = null;
			drawCarried_GraphicDB = null;
			if (((ThingOwner)thingOwnerInt).Count == 0)
			{
				return true;
			}
			if (WorkInterruption(things[0]))
			{
				return true;
			}
			if (IsEndOfLine)
			{
				return false;
			}
			usingThingOwnerInt = true;
			Thing val = ((ThingOwner<Thing>)thingOwnerInt).Take(((ThingOwner<Thing>)thingOwnerInt)[0]);
			usingThingOwnerInt = false;
			if (val == null)
			{
				return true;
			}
			if (stuck)
			{
				((ThingOwner)thingOwnerInt).TryAdd(val, true);
				TryUnstick(val);
				return false;
			}
			if (ConveyorPlaceItem(val))
			{
				return true;
			}
			((ThingOwner)thingOwnerInt).TryAdd(val, true);
			ChangeStuckStatus(val, true);
			return false;
		}

		protected virtual bool ConveyorPlaceItem(Thing thing)
		{
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			IBeltConveyorLinkable beltConveyorLinkable = OutputBelt();
			if (beltConveyorLinkable != null)
			{
				if (beltConveyorLinkable.AcceptsThing(thing, this))
				{
					NotifyAroundSender();
					stuck = false;
					return true;
				}
			}
			else if (CanSendToLevel(ConveyorLevel.Ground) && this.PRFTryPlaceThing(thing, OutputCell(), ((Thing)this).Map))
			{
				NotifyAroundSender();
				stuck = false;
				return true;
			}
			return false;
		}

		protected Thing CarryingThing()
		{
			if (base.State == WorkingState.Working)
			{
				return base.Working;
			}
			if (base.State == WorkingState.Placing)
			{
				if (Products == null || Products.Count == 0)
				{
					return null;
				}
				return Products[0];
			}
			return null;
		}

		protected virtual IBeltConveyorLinkable OutputBelt()
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			Dictionary<Building_BeltConveyor, IBeltConveyorLinkable> nextBeltCache = PatchStorageUtil.GetPRFMapComponent(((Thing)this).Map).NextBeltCache;
			if (nextBeltCache.TryGetValue(this, out var value))
			{
				return value;
			}
			value = (from b in GridsUtility.GetThingList(OutputCell(), ((Thing)this).Map).OfType<IBeltConveyorLinkable>()
				where CanLinkTo(b, checkPosition: false)
				select b).FirstOrDefault((IBeltConveyorLinkable b) => b.CanLinkFrom(this));
			nextBeltCache.Add(this, value);
			return value;
		}

		protected IEnumerable<IBeltConveyorLinkable> AllNearbyLinkables()
		{
			return (from c in Enumerable.Range(0, 4).Select(delegate(int i)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000c: Unknown result type (might be due to invalid IL or missing references)
					//IL_000f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0014: Unknown result type (might be due to invalid IL or missing references)
					IntVec3 position = ((Thing)this).Position;
					Rot4 val = new Rot4(i);
					return position + ((Rot4)(ref val)).FacingCell;
				})
				where GenGrid.InBounds(c, ((Thing)this).Map)
				select c).SelectMany((IntVec3 c) => GridsUtility.GetThingList(c, ((Thing)this).Map)).OfType<IBeltConveyorLinkable>();
		}

		private void NotifyAroundSender()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			Rot4[] array = new Rot4[3];
			Rot4 rotation = ((Thing)this).Rotation;
			array[0] = ((Rot4)(ref rotation)).Opposite;
			rotation = ((Thing)this).Rotation;
			array[1] = ((Rot4)(ref rotation)).Opposite.RotateAsNew((RotationDirection)1);
			rotation = ((Thing)this).Rotation;
			array[2] = ((Rot4)(ref rotation)).Opposite.RotateAsNew((RotationDirection)3);
			(from t in ((IEnumerable<Rot4>)(object)array).Select((Rot4 r) => ((Thing)this).Position + ((Rot4)(ref r)).FacingCell).SelectMany((IntVec3 p) => GridsUtility.GetThingList(p, ((Thing)this).Map).ToList())
				where (int)t.def.category == 3
				select t).SelectMany((Thing t) => Ops.Option(t as IBeltConveyorSender)).ForEach(delegate(IBeltConveyorSender s)
			{
				s.NortifyReceivable();
			});
		}

		protected override bool WorkInterruption(Thing working)
		{
			if (working == null)
			{
				Log.Error("PRF - WorkInterruption Null Thing");
				return true;
			}
			return (object)working.holdingOwner != thingOwnerInt;
		}

		protected override bool FinishWorking(Thing working, out List<Thing> outputProducts)
		{
			outputProducts = Products;
			return outputProducts.Count > 0;
		}

		protected override void CheckWork()
		{
			if (!((Thing)this).Spawned || ((ThingOwner)thingOwnerInt).Count == 0 || ((ThingOwner<Thing>)thingOwnerInt)[0] == null || IsEndOfLine)
			{
				return;
			}
			if (stuck)
			{
				if (CanOutput(working))
				{
					ChangeStuckStatus(working);
				}
			}
			else
			{
				CalculateCarriedItemDrawHeight();
				if (base.WorkLeft < 1f - stuckDrawPercent && !CanOutput(working))
				{
					ChangeStuckStatus(working);
					return;
				}
			}
			base.CheckWork();
		}

		protected virtual bool CanOutput(Thing thing)
		{
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			if (thing == null)
			{
				return true;
			}
			IBeltConveyorLinkable beltConveyorLinkable = OutputBelt();
			if (beltConveyorLinkable != null)
			{
				return beltConveyorLinkable.CanAcceptNow(thing);
			}
			if (!CanSendToLevel(ConveyorLevel.Ground))
			{
				return false;
			}
			if (OutputToEntireStockpile)
			{
				SlotGroup slotGroup = StoreUtility.GetSlotGroup(OutputCell(), ((Thing)this).Map);
				if (slotGroup != null)
				{
					return PlaceThingUtility.CanPlaceThingInSlotGroup(thing, slotGroup, ((Thing)this).Map);
				}
			}
			return PlaceThingUtility.CallNoStorageBlockersIn(OutputCell(), ((Thing)this).Map, thing);
		}

		protected virtual bool TryUnstick(Thing t)
		{
			if (!stuck)
			{
				return true;
			}
			if (!CanOutput(t))
			{
				return false;
			}
			ChangeStuckStatus(t, false);
			return true;
		}

		protected void ChangeStuckStatus(Thing t, bool? willBeStuck = null)
		{
			bool valueOrDefault = willBeStuck == true;
			if (!willBeStuck.HasValue)
			{
				valueOrDefault = !stuck;
				willBeStuck = valueOrDefault;
			}
			usingThingOwnerInt = true;
			((ThingOwner<Thing>)thingOwnerInt).RemoveAll((Predicate<Thing>)((Thing thing) => true));
			usingThingOwnerInt = false;
			working = null;
			if (willBeStuck.Value)
			{
				ForceStartWork(t, 1f - stuckDrawPercent - Mathf.Clamp01(base.WorkLeft));
				stuck = true;
			}
			else
			{
				ForceStartWork(t, 1f - stuckDrawPercent - Mathf.Clamp(base.WorkLeft, 0f, 1f - stuckDrawPercent));
				stuck = false;
			}
		}

		public virtual bool CanLinkTo(IBeltConveyorLinkable otherBeltLinkable, bool checkPosition = true)
		{
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			bool flag = false;
			ConveyorLevel[] array = (ConveyorLevel[])Enum.GetValues(typeof(ConveyorLevel));
			foreach (ConveyorLevel level in array)
			{
				if (CanSendToLevel(level) && otherBeltLinkable.CanReceiveFromLevel(level))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return false;
			}
			if (!checkPosition)
			{
				return true;
			}
			return OutputCell() == otherBeltLinkable.Position;
		}

		public virtual bool CanLinkFrom(IBeltConveyorLinkable otherBeltLinkable, bool checkPosition = true)
		{
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			bool flag = false;
			ConveyorLevel[] array = (ConveyorLevel[])Enum.GetValues(typeof(ConveyorLevel));
			foreach (ConveyorLevel level in array)
			{
				if (CanReceiveFromLevel(level) && otherBeltLinkable.CanSendToLevel(level))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return false;
			}
			if (!checkPosition)
			{
				return true;
			}
			if (OutputCell() == otherBeltLinkable.Position)
			{
				return false;
			}
			IntVec3 position = ((Thing)this).Position;
			return ((IntVec3)(ref position)).AdjacentToCardinal(otherBeltLinkable.Position);
		}

		public virtual bool HasLinkWith(IBeltConveyorLinkable otherBelt)
		{
			return (CanLinkTo(otherBelt) && otherBelt.CanLinkFrom(this)) || (otherBelt.CanLinkTo(this) && CanLinkFrom(otherBelt));
		}

		public Thing Carrying()
		{
			if (base.State == WorkingState.Working)
			{
				return base.Working;
			}
			if (base.State == WorkingState.Placing)
			{
				return Products.FirstOption().GetOrDefault(null);
			}
			return null;
		}

		static Building_BeltConveyor()
		{
			usingThingOwnerInt = false;
			supplyPower = 10f;
			ITab_ProductionSettings.RegisterSetting(ShowEndOfLineSetting, HeightForEolSetting, DoEOLSettingsWindowContents);
		}

		private static bool ShowEndOfLineSetting(Thing t)
		{
			return t is IBeltConveyorLinkable;
		}

		private static float HeightForEolSetting(Thing t)
		{
			return 23f;
		}

		private static void DoEOLSettingsWindowContents(Thing t, Listing_Standard list)
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			if (t is IBeltConveyorLinkable { IsEndOfLine: var flag } beltConveyorLinkable)
			{
				list.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("PRF.Conveyor.IsEndOfLineSetting")), ref flag, TaggedString.op_Implicit(Translator.Translate("PRF.Conveyor.IsEndOfLineSetting.Desc")), 0f, 1f);
				if (flag != beltConveyorLinkable.IsEndOfLine)
				{
					beltConveyorLinkable.IsEndOfLine = flag;
				}
			}
		}

		public static bool CanDefSendToRot4AtLevel(ThingDef def, Rot4 defRotation, Rot4 queryRotation, ConveyorLevel queryLevel)
		{
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			if (queryLevel == ConveyorLevel.Underground)
			{
				ModExtension_Conveyor modExtension = ((Def)def).GetModExtension<ModExtension_Conveyor>();
				if (modExtension == null || !modExtension.underground)
				{
					return false;
				}
			}
			else
			{
				ModExtension_Conveyor modExtension2 = ((Def)def).GetModExtension<ModExtension_Conveyor>();
				if (modExtension2 != null && modExtension2.underground)
				{
					return false;
				}
			}
			return defRotation == queryRotation;
		}

		public static bool CanDefReceiveFromRot4AtLevel(ThingDef def, Rot4 defRotation, Rot4 queryRotation, ConveyorLevel queryLevel)
		{
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			if (queryLevel == ConveyorLevel.Ground)
			{
				ModExtension_Conveyor modExtension = ((Def)def).GetModExtension<ModExtension_Conveyor>();
				if (modExtension == null || !modExtension.underground)
				{
					goto IL_003b;
				}
			}
			if (queryLevel == ConveyorLevel.Underground)
			{
				ModExtension_Conveyor modExtension2 = ((Def)def).GetModExtension<ModExtension_Conveyor>();
				if (modExtension2 != null && modExtension2.underground)
				{
					goto IL_003b;
				}
			}
			return false;
			IL_003b:
			return defRotation != queryRotation;
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
	internal class Building_BeltConveyorUGConnector : Building_BeltConveyor, IBeltConveyorLinkable, IPRF_Building, ILoadReferenceable
	{
		public bool ToUnderground => modExtension_Conveyor.toUnderground;

		public override void DrawGUIOverlay()
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Invalid comparison between Unknown and I4
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			base.DrawGUIOverlay();
			if (base.State != WorkingState.Ready && (int)Find.CameraDriver.CurrentZoom == 0)
			{
				Vector3 val = CarryPosition();
				if (!ToUnderground || base.WorkLeft > 0.7f)
				{
					Vector2 val2 = Vector2.op_Implicit(Find.Camera.WorldToScreenPoint(val + new Vector3(0f, 0f, -0.4f)) / Prefs.UIScale);
					val2.y = (float)UI.screenHeight - val2.y;
					GenMapUI.DrawThingLabel(val2, GenString.ToStringCached(CarryingThing().stackCount), GenMapUI.DefaultThingLabelColor);
				}
			}
		}

		public override void DrawCarried()
		{
			if (!ToUnderground || (base.IsStuck && base.WorkLeft < 0.05f) || (!base.IsStuck && base.WorkLeft > 0.85f))
			{
				base.DrawCarried();
			}
		}

		protected override bool TryStartWorking(out Thing target, out float workAmount)
		{
			if (ToUnderground)
			{
				return base.TryStartWorking(out target, out workAmount);
			}
			target = null;
			workAmount = 1f;
			return false;
		}

		public override bool CanSendToLevel(ConveyorLevel level)
		{
			if (ToUnderground)
			{
				if (level == ConveyorLevel.Underground)
				{
					return true;
				}
			}
			else if (level == ConveyorLevel.Ground)
			{
				return true;
			}
			return false;
		}

		public override bool CanReceiveFromLevel(ConveyorLevel level)
		{
			if (ToUnderground)
			{
				if (level == ConveyorLevel.Ground)
				{
					return true;
				}
			}
			else if (level == ConveyorLevel.Underground)
			{
				return true;
			}
			return false;
		}

		public override bool CanLinkTo(IBeltConveyorLinkable otherBeltLinkable, bool checkPosition = true)
		{
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			if (ToUnderground)
			{
				if (!otherBeltLinkable.CanReceiveFromLevel(ConveyorLevel.Underground))
				{
					return false;
				}
			}
			else if (!otherBeltLinkable.CanReceiveFromLevel(ConveyorLevel.Ground))
			{
				return false;
			}
			if (!checkPosition)
			{
				return true;
			}
			IntVec3 position = ((Thing)this).Position;
			Rot4 rotation = ((Thing)this).Rotation;
			return position + ((Rot4)(ref rotation)).FacingCell == otherBeltLinkable.Position;
		}

		public new static bool CanDefSendToRot4AtLevel(ThingDef def, Rot4 defRotation, Rot4 queryRotation, ConveyorLevel queryLevel)
		{
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			if (queryLevel == ConveyorLevel.Underground)
			{
				ModExtension_Conveyor modExtension = ((Def)def).GetModExtension<ModExtension_Conveyor>();
				if (modExtension == null || !modExtension.toUnderground)
				{
					return false;
				}
			}
			else
			{
				ModExtension_Conveyor modExtension2 = ((Def)def).GetModExtension<ModExtension_Conveyor>();
				if (modExtension2 != null && modExtension2.toUnderground)
				{
					return false;
				}
			}
			return defRotation == queryRotation;
		}

		public new static bool CanDefReceiveFromRot4AtLevel(ThingDef def, Rot4 defRotation, Rot4 queryRotation, ConveyorLevel queryLevel)
		{
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			if (queryLevel == ConveyorLevel.Ground)
			{
				ModExtension_Conveyor modExtension = ((Def)def).GetModExtension<ModExtension_Conveyor>();
				if (modExtension != null && modExtension.toUnderground)
				{
					goto IL_003b;
				}
			}
			if (queryLevel == ConveyorLevel.Underground)
			{
				ModExtension_Conveyor modExtension2 = ((Def)def).GetModExtension<ModExtension_Conveyor>();
				if (modExtension2 == null || !modExtension2.toUnderground)
				{
					goto IL_003b;
				}
			}
			return false;
			IL_003b:
			return defRotation != queryRotation;
		}
	}
	public class Building_BeltSplitter : Building_BeltConveyor
	{
		public class OutputLink : IExposable
		{
			public IBeltConveyorLinkable link;

			public DirectionPriority Priority = DirectionPriority.Normal;

			private ThingFilter filter;

			private bool active = true;

			private IntVec3 position = IntVec3.Invalid;

			private Building_BeltSplitter parent;

			public bool Active
			{
				get
				{
					return active;
				}
				set
				{
					if (active != value)
					{
						active = value;
						parent.UpdateGraphic();
					}
				}
			}

			public OutputLink(Building_BeltSplitter parent, IBeltConveyorLinkable link)
				: this(parent, link.Position)
			{
				//IL_0003: Unknown result type (might be due to invalid IL or missing references)
				this.link = link;
			}

			public OutputLink(Building_BeltSplitter parent, IntVec3 pos)
			{
				//IL_000f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				//IL_0030: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Unknown result type (might be due to invalid IL or missing references)
				//IL_0037: Unknown result type (might be due to invalid IL or missing references)
				//IL_0041: Expected O, but got Unknown
				this.parent = parent;
				link = null;
				position = pos;
				filter = new ThingFilter();
				filter.SetAllowAll(ThingFilterHelper.FilterConfigParentNoAnimalNoPlants, false);
			}

			public OutputLink()
			{
			}//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)


			public void ExposeData()
			{
				//IL_005c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0062: Unknown result type (might be due to invalid IL or missing references)
				Scribe_Values.Look<DirectionPriority>(ref Priority, "PRFB_priority", DirectionPriority.Normal, false);
				Scribe_Deep.Look<ThingFilter>(ref filter, "PRFBSL_filter", Array.Empty<object>());
				Scribe_References.Look<IBeltConveyorLinkable>(ref link, "PRFBSL_link", false);
				Scribe_Values.Look<bool>(ref active, "PRFBSL_active", false, false);
				Scribe_Values.Look<IntVec3>(ref position, "PRFBSL_position", default(IntVec3), false);
				Scribe_References.Look<Building_BeltSplitter>(ref parent, "PRFBSL_parent", false);
			}

			public bool Allows(Thing t)
			{
				return Active && (filter == null || filter.Allows(t));
			}

			public bool IsValidOutputLinkFor(Thing t)
			{
				//IL_0036: Unknown result type (might be due to invalid IL or missing references)
				if (!Allows(t))
				{
					return false;
				}
				if (link != null)
				{
					return link.CanAcceptNow(t);
				}
				return PlaceThingUtility.CallNoStorageBlockersIn(position, ((Thing)parent).Map, t);
			}

			public bool TryPlace(Thing thing)
			{
				//IL_0043: Unknown result type (might be due to invalid IL or missing references)
				if (link != null)
				{
					if (link.AcceptsThing(thing, parent))
					{
						return true;
					}
				}
				else if (!parent.IsUnderground && parent.PRFTryPlaceThing(thing, position, ((Thing)parent).Map))
				{
					return true;
				}
				return false;
			}

			public void CopyAllowancesFrom(ThingFilter o)
			{
				//IL_000a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Expected O, but got Unknown
				if (filter == null)
				{
					filter = new ThingFilter();
				}
				filter.CopyAllowancesFrom(o);
			}

			public void DoThingFilterConfigWindow(Rect r, UIState uIState)
			{
				//IL_002d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0010: Unknown result type (might be due to invalid IL or missing references)
				//IL_001a: Expected O, but got Unknown
				if (filter == null)
				{
					filter = new ThingFilter();
					filter.SetAllowAll(ThingFilterHelper.FilterConfigParentNoAnimalNoPlants, false);
				}
				ThingFilterUI.DoThingFilterConfigWindow(r, uIState, filter, ThingFilterHelper.FilterConfigParentNoAnimalNoPlants, 1, (IEnumerable<ThingDef>)null, (IEnumerable<SpecialThingFilterDef>)null, false, false, false, (List<ThingDef>)null, (Map)null);
			}
		}

		private Rot4 dest = Rot4.Random;

		private Dictionary<Rot4, OutputLink> outputLinks = new Dictionary<Rot4, OutputLink>();

		[Unsaved(false)]
		private readonly HashSet<IBeltConveyorLinkable> incomingLinks = new HashSet<IBeltConveyorLinkable>();

		public Dictionary<Rot4, OutputLink> OutputLinks => outputLinks;

		public IEnumerable<IBeltConveyorLinkable> IncomingLinks => incomingLinks;

		public override PRFBSetting SettingsOptions => base.SettingsOptions | PRFBSetting.optionObeysStorageFilters;

		protected override Rot4 OutputDirection => dest;

		public override IEnumerable<Rot4> ActiveOutputDirections => from kvp in outputLinks
			where kvp.Value.Active
			select kvp.Key;

		public override bool IsEndOfLine
		{
			get
			{
				return outputLinks.Any((KeyValuePair<Rot4, OutputLink> kvp) => kvp.Value.Active) && base.IsEndOfLine;
			}
			set
			{
				//IL_003e: Unknown result type (might be due to invalid IL or missing references)
				if (value && !outputLinks.Any((KeyValuePair<Rot4, OutputLink> kvp) => kvp.Value.Active))
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("PRF.Conveyor.Splitter.MustHaveActiveOutput")), LookTargets.op_Implicit((Thing)(object)this), MessageTypeDefOf.CautionInput, true);
				}
				else
				{
					base.IsEndOfLine = value;
				}
			}
		}

		public Building_BeltSplitter()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			obeysStorageFilters = true;
		}

		public override void ExposeData()
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			base.ExposeData();
			Scribe_Values.Look<Rot4>(ref dest, "dest", default(Rot4), false);
			Scribe_Collections.Look<Rot4, OutputLink>(ref outputLinks, "outputLinks", (LookMode)1, (LookMode)2);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			base.SpawnSetup(map, respawningAfterLoad);
			ShowProgressBar = false;
			if (GravshipPlacementUtility.placingGravship)
			{
				Rot4 rotation = Find.CurrentGravship.Rotation;
				OutputLink[] array = new OutputLink[4]
				{
					CollectionExtensions.GetValueOrDefault<Rot4, OutputLink>((IReadOnlyDictionary<Rot4, OutputLink>)outputLinks, Rot4.North, (OutputLink)null),
					CollectionExtensions.GetValueOrDefault<Rot4, OutputLink>((IReadOnlyDictionary<Rot4, OutputLink>)outputLinks, Rot4.East, (OutputLink)null),
					CollectionExtensions.GetValueOrDefault<Rot4, OutputLink>((IReadOnlyDictionary<Rot4, OutputLink>)outputLinks, Rot4.South, (OutputLink)null),
					CollectionExtensions.GetValueOrDefault<Rot4, OutputLink>((IReadOnlyDictionary<Rot4, OutputLink>)outputLinks, Rot4.West, (OutputLink)null)
				};
				outputLinks.Clear();
				Rot4 key = default(Rot4);
				for (int i = 0; i < array.Length; i++)
				{
					OutputLink outputLink = array[i];
					if (outputLink != null)
					{
						((Rot4)(ref key))..ctor((i + ((Rot4)(ref rotation)).AsInt) % 4);
						outputLinks.Add(key, outputLink);
					}
				}
			}
			incomingLinks.Clear();
			List<IBeltConveyorLinkable> list = AllNearbyLinks().ToList();
			foreach (IBeltConveyorLinkable item in list)
			{
				item.Link(this);
				Link(item);
			}
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			foreach (IBeltConveyorLinkable item in AllNearbyLinkables())
			{
				item.Unlink(this);
			}
			base.DeSpawn(mode);
			if (!GravshipUtility.generatingGravship)
			{
				outputLinks.Clear();
			}
			incomingLinks.Clear();
		}

		private void UpdateGraphic()
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			if (((Thing)this).Spawned)
			{
				((Thing)this).Map.mapDrawer.MapMeshDirty(((Thing)this).Position, MapMeshFlagDef.op_Implicit(MapMeshFlagDefOf.Buildings) | MapMeshFlagDef.op_Implicit(MapMeshFlagDefOf.Things));
			}
		}

		public override bool CanStackWith(Thing other)
		{
			return base.CanStackWith(other) && base.State == WorkingState.Ready;
		}

		public override bool AcceptsThing(Thing newThing, IPRF_Building giver = null)
		{
			WorkingState workingState = base.State;
			bool flag = base.AcceptsThing(newThing, giver);
			if (flag && workingState == WorkingState.Ready)
			{
				NextDirection(newThing, out dest);
				return true;
			}
			return flag;
		}

		private bool NextDirection(Thing t, out Rot4 newDir)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			foreach (Rot4 item in NextDirectionByPriority(t))
			{
				OutputLink outputLink = GenCollection.TryGetValue<Rot4, OutputLink>((IReadOnlyDictionary<Rot4, OutputLink>)outputLinks, item, (OutputLink)null);
				if (outputLink == null || !outputLink.IsValidOutputLinkFor(t))
				{
					continue;
				}
				newDir = item;
				return true;
			}
			newDir = ((Thing)this).Rotation;
			return false;
		}

		private IEnumerable<Rot4> NextDirectionByPriority(Thing t)
		{
			Rot4 previousDir = new Rot4(((Rot4)(ref dest)).AsInt);
			DirectionPriority? prevPriority = GenCollection.TryGetValue<Rot4, OutputLink>((IReadOnlyDictionary<Rot4, OutputLink>)outputLinks, previousDir, (OutputLink)null)?.Priority;
			foreach (DirectionPriority priority in ((DirectionPriority[])Enum.GetValues(typeof(DirectionPriority))).Reverse())
			{
				Rot4 nextDir = previousDir.RotateAsNew((RotationDirection)3);
				int i = 0;
				while (i < 4)
				{
					if (nextDir != previousDir && outputLinks.ContainsKey(nextDir) && outputLinks[nextDir].Priority == priority && outputLinks[nextDir].Allows(t))
					{
						yield return nextDir;
					}
					i++;
					((Rot4)(ref nextDir)).Rotate((RotationDirection)3);
				}
				if (priority == prevPriority)
				{
					yield return previousDir;
				}
			}
		}

		protected override IBeltConveyorLinkable OutputBelt()
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			foreach (KeyValuePair<Rot4, OutputLink> outputLink in outputLinks)
			{
				Rot4 key = outputLink.Key;
				if (((Rot4)(ref key)).FacingCell + ((Thing)this).Position == OutputCell())
				{
					if (!outputLink.Value.Active)
					{
						return null;
					}
					return outputLink.Value.link;
				}
			}
			return null;
		}

		protected override bool TryUnstick(Thing t)
		{
			if (base.TryUnstick(t))
			{
				return true;
			}
			if (!NextDirection(t, out var _))
			{
				return false;
			}
			if (((ThingOwner)thingOwnerInt).Contains(t))
			{
				((ThingOwner<Thing>)thingOwnerInt).Take(t);
			}
			else
			{
				Reset();
			}
			AcceptsThing(t, this);
			return true;
		}

		protected override bool CanOutput(Thing thing)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			if (thing == null)
			{
				return true;
			}
			if (outputLinks.ContainsKey(dest))
			{
				return outputLinks[dest].IsValidOutputLinkFor(thing);
			}
			return false;
		}

		protected override bool ConveyorPlaceItem(Thing thing)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			OutputLink outputLink = GenCollection.TryGetValue<Rot4, OutputLink>((IReadOnlyDictionary<Rot4, OutputLink>)outputLinks, dest, (OutputLink)null);
			if (outputLink != null && outputLink.Allows(thing) && outputLink.TryPlace(thing))
			{
				NotifyAroundSender();
				stuck = false;
				return true;
			}
			if (NextDirection(thing, out var _))
			{
				Reset();
				AcceptsThing(thing, this);
				return false;
			}
			return false;
		}

		public override void Link(IBeltConveyorLinkable link)
		{
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			if (CanLinkTo(link) && link.CanLinkFrom(this) && PositionToRot4(link, out var r))
			{
				UpdateGraphic();
				if (outputLinks.TryGetValue(r, out var value))
				{
					value.link = link;
				}
				else
				{
					outputLinks[r] = new OutputLink(this, link);
				}
			}
			if (CanLinkFrom(link) && link.CanLinkTo(this))
			{
				UpdateGraphic();
				incomingLinks.Add(link);
				if (PositionToRot4(link, out var r2) && outputLinks.TryGetValue(r2, out var value2) && IsInputtingIntoThis(link, r2))
				{
					value2.Active = false;
				}
			}
		}

		public static bool IsInputtingIntoThis(IBeltConveyorLinkable link, Rot4 r)
		{
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			if (link is Building_BeltSplitter building_BeltSplitter)
			{
				building_BeltSplitter.outputLinks.TryGetValue(((Rot4)(ref r)).Opposite, out var value);
				return value?.Active ?? false;
			}
			Rot4 rotation = link.Rotation;
			return ((Rot4)(ref rotation)).Opposite == r;
		}

		private bool PositionToRot4(IBeltConveyorLinkable link, out Rot4 r)
		{
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			foreach (Rot4 item in Enumerable.Range(0, 4).Select((Func<int, Rot4>)((int i) => new Rot4(i))))
			{
				Rot4 current = item;
				if (((Thing)this).Position + ((Rot4)(ref current)).FacingCell == link.Position)
				{
					r = current;
					return true;
				}
			}
			r = Rot4.Random;
			return false;
		}

		public override void Unlink(IBeltConveyorLinkable unlink)
		{
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			incomingLinks.Remove(unlink);
			List<Rot4> list = (from kvp in outputLinks
				where kvp.Value.link == unlink
				select kvp.Key).ToList();
			foreach (Rot4 item in list)
			{
				outputLinks.Remove(item);
			}
			UpdateGraphic();
		}

		public void AddOutgoingLink(Rot4 dir)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			if (outputLinks.ContainsKey(dir))
			{
				outputLinks[dir].Active = true;
				return;
			}
			outputLinks[dir] = new OutputLink(this, ((Thing)this).Position + ((Rot4)(ref dir)).FacingCell);
			UpdateGraphic();
		}

		protected IEnumerable<IBeltConveyorLinkable> AllNearbyLinks()
		{
			return from b in AllNearbyLinkables()
				where (CanLinkTo(b) && b.CanLinkFrom(this)) || (CanLinkFrom(b) && b.CanLinkTo(this))
				select b;
		}

		protected IBeltConveyorLinkable BeltLinkableAt(IntVec3 location)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return (from b in GridsUtility.GetThingList(location, ((Thing)this).Map).OfType<IBeltConveyorLinkable>()
				where CanLinkTo(b)
				select b).FirstOrDefault((IBeltConveyorLinkable b) => b.CanLinkFrom(this));
		}

		private void NotifyAroundSender()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			Rot4[] array = new Rot4[3];
			Rot4 rotation = ((Thing)this).Rotation;
			array[0] = ((Rot4)(ref rotation)).Opposite;
			rotation = ((Thing)this).Rotation;
			array[1] = ((Rot4)(ref rotation)).Opposite.RotateAsNew((RotationDirection)1);
			rotation = ((Thing)this).Rotation;
			array[2] = ((Rot4)(ref rotation)).Opposite.RotateAsNew((RotationDirection)3);
			(from t in ((IEnumerable<Rot4>)(object)array).Select((Rot4 r) => ((Thing)this).Position + ((Rot4)(ref r)).FacingCell).SelectMany((IntVec3 p) => GridsUtility.GetThingList(p, ((Thing)this).Map).ToList())
				where (int)t.def.category == 3
				select t).SelectMany((Thing t) => Ops.Option(t as IBeltConveyorSender)).ForEach(delegate(IBeltConveyorSender s)
			{
				s.NortifyReceivable();
			});
		}

		protected override bool WorkInterruption(Thing _)
		{
			return false;
		}

		protected override bool TryStartWorking(out Thing target, out float workAmount)
		{
			target = null;
			workAmount = 0f;
			return false;
		}

		public override bool CanLinkTo(IBeltConveyorLinkable otherBeltLinkable, bool checkPosition = true)
		{
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			bool flag = false;
			ConveyorLevel[] array = (ConveyorLevel[])Enum.GetValues(typeof(ConveyorLevel));
			foreach (ConveyorLevel level in array)
			{
				if (CanSendToLevel(level) && otherBeltLinkable.CanReceiveFromLevel(level))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return false;
			}
			if (!checkPosition)
			{
				return true;
			}
			return ((Thing)this).Position.IsNextTo(otherBeltLinkable.Position);
		}

		public override bool CanLinkFrom(IBeltConveyorLinkable otherBeltLinkable, bool checkPosition = true)
		{
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			bool flag = false;
			ConveyorLevel[] array = (ConveyorLevel[])Enum.GetValues(typeof(ConveyorLevel));
			foreach (ConveyorLevel level in array)
			{
				if (CanReceiveFromLevel(level) && otherBeltLinkable.CanSendToLevel(level))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return false;
			}
			if (!checkPosition)
			{
				return true;
			}
			return ((Thing)this).Position.IsNextTo(otherBeltLinkable.Position);
		}

		public override bool HasLinkWith(IBeltConveyorLinkable otherBelt)
		{
			return incomingLinks.Contains(otherBelt) || outputLinks.Any((KeyValuePair<Rot4, OutputLink> kvp) => kvp.Value.link == otherBelt);
		}

		public new static bool CanDefSendToRot4AtLevel(ThingDef def, Rot4 defRotation, Rot4 queryRotation, ConveyorLevel queryLevel)
		{
			if (queryLevel == ConveyorLevel.Underground)
			{
				ModExtension_Conveyor modExtension = ((Def)def).GetModExtension<ModExtension_Conveyor>();
				if (modExtension == null || !modExtension.underground)
				{
					return false;
				}
			}
			else
			{
				ModExtension_Conveyor modExtension2 = ((Def)def).GetModExtension<ModExtension_Conveyor>();
				if (modExtension2 != null && modExtension2.underground)
				{
					return false;
				}
			}
			return true;
		}

		public new static bool CanDefReceiveFromRot4AtLevel(ThingDef def, Rot4 defRotation, Rot4 queryRotation, ConveyorLevel queryLevel)
		{
			if (queryLevel == ConveyorLevel.Ground)
			{
				ModExtension_Conveyor modExtension = ((Def)def).GetModExtension<ModExtension_Conveyor>();
				if (modExtension == null || !modExtension.underground)
				{
					goto IL_003b;
				}
			}
			if (queryLevel == ConveyorLevel.Underground)
			{
				ModExtension_Conveyor modExtension2 = ((Def)def).GetModExtension<ModExtension_Conveyor>();
				if (modExtension2 != null && modExtension2.underground)
				{
					goto IL_003b;
				}
			}
			return false;
			IL_003b:
			return true;
		}
	}
	public static class StupidCSharpStaticClassRequirements
	{
		public static bool IsNextTo(this IntVec3 one, IntVec3 two)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			if (one.x == two.x)
			{
				int num = one.z - two.z;
				return (num == -1 || num == 1) ? true : false;
			}
			if (one.z == two.z)
			{
				int num = one.x - two.x;
				return (num == -1 || num == 1) ? true : false;
			}
			return false;
		}
	}
	public class Building_HarvestPuller : Building_ItemPuller
	{
		protected override Thing TargetThing()
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 position = ((Thing)this).Position;
			Rot4 val = ((Thing)this).Rotation;
			val = ((Rot4)(ref val)).Opposite;
			Zone zone = GridsUtility.GetZone(position + ((Rot4)(ref val)).FacingCell, ((Thing)this).Map);
			if (!(zone is IPlantToGrowSettable))
			{
				return null;
			}
			Thing val2 = (from t in zone.AllContainedThings
				where (int)t.def.category == 2
				where !ForbidUtility.IsForbidden(t, Faction.OfPlayer) || TakeForbiddenItems
				where settings.AllowedToAccept(t)
				select t).FirstOrDefault((Thing t) => !IsLimit(t));
			if (val2 == null)
			{
				return null;
			}
			if (takeSingleItems)
			{
				return val2.SplitOff(1);
			}
			return val2.SplitOff(val2.stackCount);
		}
	}
	[StaticConstructorOnStartup]
	public class Building_ItemPuller : Building_BaseLimitation<Thing>, IStorageSetting, IStoreSettingsParent
	{
		private bool active = false;

		protected bool TakeForbiddenItems = true;

		protected bool takeSingleItems = false;

		private const float DefaultWorkAmount = 120f;

		public StorageSettings settings;

		protected StorageSettings storageSettings;

		private bool right = false;

		public override Graphic Graphic => ((Def)((Thing)this).def).GetModExtension<ModExtension_Graphic>()?.GetByName(GetGraphicName()) ?? ((Thing)this).Graphic;

		public bool StorageTabVisible => true;

		public StorageSettings StorageSettings => storageSettings;

		public bool GetRight => right;

		private bool OutputSides => ((Def)((Thing)this).def).GetModExtension<ModExtension_Puller>()?.outputSides ?? false;

		protected override LookMode WorkingLookMode => (LookMode)2;

		public bool TakeSingleItems
		{
			get
			{
				return takeSingleItems;
			}
			set
			{
				takeSingleItems = value;
			}
		}

		public Building_ItemPuller()
		{
			outputToEntireStockpile = true;
		}

		private string GetGraphicName()
		{
			string text = null;
			if (OutputSides)
			{
				text += (right ? "Right" : "Left");
			}
			if (active)
			{
				text += "Working";
			}
			return text;
		}

		public StorageSettings GetStoreSettings()
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Expected O, but got Unknown
			if (settings != null)
			{
				return settings;
			}
			settings = new StorageSettings();
			if (GetParentStoreSettings() != null)
			{
				settings.CopyFrom(GetParentStoreSettings());
			}
			return settings;
		}

		public StorageSettings GetParentStoreSettings()
		{
			return ((Thing)this).def.building?.fixedStorageSettings;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look<bool>(ref active, "active", false, false);
			Scribe_Values.Look<bool>(ref right, "right", false, false);
			Scribe_Deep.Look<StorageSettings>(ref settings, "settings", new object[1] { this });
			Scribe_Values.Look<bool>(ref TakeForbiddenItems, "takeForbidden", true, false);
			Scribe_Values.Look<bool>(ref takeSingleItems, "takeSingleItems", false, false);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			base.SpawnSetup(map, respawningAfterLoad);
			settings = GetStoreSettings();
			ForcePlace = false;
			if (!respawningAfterLoad)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("PRF.NeedToTurnOnPuller")), LookTargets.op_Implicit((Thing)(object)this), MessageTypeDefOf.CautionInput, true);
			}
		}

		protected override void Reset()
		{
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			if (working != null && !working.Spawned)
			{
				GenPlace.TryPlaceThing(working, ((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)((IntVec3 p) => p != ((Thing)this).Position), (Rot4?)null, 1);
			}
			base.Reset();
		}

		protected override TargetInfo ProgressBarTarget()
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			return TargetInfo.op_Implicit((Thing)(object)this);
		}

		protected virtual Thing TargetThing()
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			Thing val = null;
			IntVec3 position = ((Thing)this).Position;
			Rot4 val2 = ((Thing)this).Rotation;
			val2 = ((Rot4)(ref val2)).Opposite;
			Thing[] array = (position + ((Rot4)(ref val2)).FacingCell).AllThingsInCellForUse(((Thing)this).Map, allowStorageZones: true, allowUGEntranceConnector: true).ToArray();
			HashSet<Thing> hashSet = ((Thing)this).Map.reservationManager.AllReservedThings().ToHashSet();
			foreach (Thing val3 in array)
			{
				if ((TakeForbiddenItems || !ForbidUtility.IsForbidden(val3, Faction.OfPlayer)) && settings.AllowedToAccept(val3) && !hashSet.Contains(val3) && !IsLimit(val3))
				{
					val = val3;
					break;
				}
			}
			if (val == null)
			{
				return null;
			}
			if (takeSingleItems)
			{
				return val.SplitOff(1);
			}
			return val.SplitOff(val.stackCount);
		}

		public override IntVec3 OutputCell()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			return ((Def)((Thing)this).def).GetModExtension<ModExtension_Puller>().GetOutputCell(((Thing)this).Position, ((Thing)this).Rotation, right);
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			foreach (Gizmo item2 in StorageSettingsClipboard.CopyPasteGizmosFor(settings))
			{
				yield return item2;
			}
			yield return (Gizmo)new Command_Toggle
			{
				isActive = () => active,
				toggleAction = delegate
				{
					active = !active;
				},
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Puller.SwitchActiveLabel")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Puller.SwitchActiveDesc")),
				icon = (Texture)(object)RS.PlayIcon
			};
			yield return (Gizmo)new Command_Toggle
			{
				isActive = () => TakeForbiddenItems,
				toggleAction = delegate
				{
					TakeForbiddenItems = !TakeForbiddenItems;
				},
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRF.Puller.TakeForbiddenItems")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRF.Puller.TakeForbiddenItemsDesc")),
				icon = (Texture)(object)TexCommand.ForbidOff
			};
			if (OutputSides)
			{
				yield return (Gizmo)new Command_Action
				{
					action = delegate
					{
						right = !right;
					},
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Puller.SwitchOutputSideLabel")),
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Puller.SwitchOutputSideDesc")),
					icon = (Texture)(object)RS.OutputDirectionIcon
				};
			}
		}

		protected override bool IsActive()
		{
			return base.IsActive() && active;
		}

		protected override bool WorkInterruption(Thing _)
		{
			return false;
		}

		protected override bool TryStartWorking(out Thing target, out float workAmount)
		{
			workAmount = 120f;
			target = TargetThing();
			Thing obj = target;
			if (obj != null && obj.Spawned)
			{
				((Entity)target).DeSpawn((DestroyMode)0);
			}
			return target != null;
		}

		protected override bool FinishWorking(Thing working, out List<Thing> outputProducts)
		{
			Products.Append(working);
			outputProducts = Products;
			return true;
		}

		protected override void Placing()
		{
			if (!GenList.NullOrEmpty<Thing>((IList<Thing>)Products))
			{
				foreach (Thing product in Products)
				{
					if (ForbidUtility.IsForbidden(product, Faction.OfPlayer))
					{
						ForbidUtility.SetForbidden(product, false, true);
					}
				}
			}
			base.Placing();
		}

		public override bool AcceptsThing(Thing newThing, IPRF_Building giver)
		{
			if (base.State != WorkingState.Ready)
			{
				return false;
			}
			ClearActions();
			if (newThing.Spawned)
			{
				((Entity)newThing).DeSpawn((DestroyMode)0);
			}
			ForceStartWork(newThing, 120f);
			return true;
		}

		static Building_ItemPuller()
		{
			ITab_ProductionSettings.RegisterSetting(ShouldShowSingleVsStackSetting, ExtraHeightNeeded, DoSettingsWindowContents);
		}

		private static bool ShouldShowSingleVsStackSetting(Thing thing)
		{
			return thing is Building_ItemPuller;
		}

		private static float ExtraHeightNeeded(Thing t)
		{
			return 21f;
		}

		private static void DoSettingsWindowContents(Thing t, Listing_Standard ls)
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			if (t is Building_ItemPuller { takeSingleItems: var flag } building_ItemPuller)
			{
				ls.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("PRF.Puller.takeSingleItemsHmm")), ref flag, TaggedString.op_Implicit(Translator.Translate("PRF.Puller.takeSingleItemsDesc")), 0f, 1f);
				if (flag != building_ItemPuller.takeSingleItems)
				{
					building_ItemPuller.TakeSingleItems = flag;
				}
			}
		}

		public void Notify_SettingsChanged()
		{
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
	public class Building_Slaughterhouse : Building_BaseRange<Pawn>, ISlaughterhouse
	{
		private Dictionary<ThingDef, SlaughterSettings> slaughterSettings = new Dictionary<ThingDef, SlaughterSettings>();

		public Dictionary<ThingDef, SlaughterSettings> Settings => slaughterSettings;

		public override bool ProductLimitationDisable => true;

		public override IntVec3 OutputCell()
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			return CompOutputAdjustable.CurrentCell;
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			base.SpawnSetup(map, respawningAfterLoad);
			if (!PowerWorkSetting.Props.allowManualRangeTypeChange)
			{
				PowerWorkSetting.RangeTypeRot = ((Thing)this).Rotation;
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look<ThingDef, SlaughterSettings>(ref slaughterSettings, "slaughterSettings", (LookMode)4, (LookMode)2);
		}

		protected override void Reset()
		{
			if (base.Working != null && base.Working.jobs != null && base.Working.jobs.curJob != null && base.Working.jobs.curJob.def == JobDefOf.Wait_MaintainPosture)
			{
				base.Working.jobs.EndCurrentJob((JobCondition)16, true, true);
			}
			base.Reset();
		}

		private HashSet<Pawn> ShouldSlaughterPawns()
		{
			List<Pawn> mapPawns = ((Thing)this).Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
			return slaughterSettings.Values.Where((SlaughterSettings s) => s.doSlaughter).SelectMany(delegate(SlaughterSettings s)
			{
				IEnumerable<Pawn> pawns = mapPawns.Where((Pawn p) => ((Thing)p).def == s.def);
				Func<Pawn, bool> where = delegate(Pawn p)
				{
					bool flag = true;
					if (!s.hasBonds)
					{
						flag = p.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond, (Predicate<Pawn>)null) == null;
					}
					if (flag && !s.pregnancy)
					{
						flag = p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant, true) == null;
					}
					if (flag && !s.trained)
					{
						flag = !p.training.HasLearned(TrainableDefOf.Obedience);
					}
					return flag;
				};
				Func<IEnumerable<Pawn>, bool, IOrderedEnumerable<Pawn>> orderBy = (IEnumerable<Pawn> e, bool adult) => adult ? e.OrderByDescending((Pawn p) => p.ageTracker.AgeChronologicalTicks) : e.OrderBy((Pawn p) => p.ageTracker.AgeChronologicalTicks);
				return (from a in new[]
					{
						new
						{
							Gender = (Gender)1,
							Adult = true
						},
						new
						{
							Gender = (Gender)2,
							Adult = true
						},
						new
						{
							Gender = (Gender)1,
							Adult = false
						},
						new
						{
							Gender = (Gender)2,
							Adult = false
						},
						new
						{
							Gender = (Gender)0,
							Adult = false
						},
						new
						{
							Gender = (Gender)0,
							Adult = true
						}
					}
					select new
					{
						Group = a,
						Pawns = pawns.Where((Pawn p) => p.gender == a.Gender && p.IsAdult() == a.Adult)
					} into g
					select new
					{
						Group = g.Group,
						Pawns = g.Pawns,
						SlaughterCount = g.Pawns.Count() - s.KeepCount(g.Group.Gender, g.Group.Adult)
					} into g
					where g.SlaughterCount > 0
					select g).SelectMany(g => orderBy(g.Pawns.Where(where), g.Group.Adult).Take(g.SlaughterCount));
			}).ToHashSet();
		}

		protected override bool WorkInterruption(Pawn working)
		{
			return working.Dead || !((Thing)working).Spawned;
		}

		protected override bool TryStartWorking(out Pawn target, out float workAmount)
		{
			workAmount = 400f;
			target = null;
			IEnumerable<Pawn> enumerable = from p in (from t in GetTargetCells().SelectMany((IntVec3 c) => GridsUtility.GetThingList(c, ((Thing)this).Map))
					where (int)t.def.category == 1
					select t).SelectMany((Thing t) => Ops.Option<Pawn>((Pawn)(object)((t is Pawn) ? t : null)))
				where slaughterSettings.ContainsKey(((Thing)p).def)
				select p;
			if (!enumerable.FirstOption().HasValue)
			{
				return false;
			}
			HashSet<Pawn> targets = ShouldSlaughterPawns();
			target = enumerable.Where((Pawn p) => targets.Contains(p)).FirstOption().GetOrDefault(null);
			if (target != null)
			{
				PawnUtility.ForceWait(target, 15000, (Thing)null, true, false);
			}
			return target != null;
		}

		protected override bool FinishWorking(Pawn working, out List<Thing> outputProducts)
		{
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Expected O, but got Unknown
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			if (working.jobs.curJob.def == JobDefOf.Wait_MaintainPosture)
			{
				working.jobs.EndCurrentJob((JobCondition)16, true, true);
			}
			int num = Mathf.Max(GenMath.RoundRandom(working.BodySize * 8f), 1);
			for (int i = 0; i < num; i++)
			{
				working.health.DropBloodFilth();
			}
			((Thing)this).Map.designationManager.AddDesignation(new Designation(LocalTargetInfo.op_Implicit((Thing)(object)working), DesignationDefOf.Slaughter, (ColorDef)null));
			((Thing)working).Kill((DamageInfo?)new DamageInfo(DamageDefOf.ExecutionCut, 0f, 0f, -1f, (Thing)null, (BodyPartRecord)null, (ThingDef)null, (SourceCategory)0, (Thing)null, true, true, (QualityCategory)2, true, false), (Hediff)null);
			outputProducts = new List<Thing>().Append((Thing)(object)working.Corpse);
			((Entity)working.Corpse).DeSpawn((DestroyMode)0);
			ForbidUtility.SetForbidden((Thing)(object)working.Corpse, false, true);
			return true;
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
	public class Comp_CopyAndPaste : ThingComp
	{
		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			return ((ThingComp)this).CompGetGizmosExtra().Concat((from s in Ops.Option(base.parent as IStorageSetting)
				select s.StorageSettings into x
				select StorageSettingsClipboard.CopyPasteGizmosFor(x)).GetOrDefault(Enumerable.Empty<Gizmo>()));
		}
	}
	public interface IStorageSetting
	{
		StorageSettings StorageSettings { get; }
	}
	public interface IRecipeProductWorker
	{
		Map Map { get; }

		IntVec3 Position { get; }

		Room GetRoom(RegionType type);

		int GetSkillLevel(SkillDef skillDef);
	}
	public static class IRecipeProductWorkerExtension
	{
		public static float GetStatValue(this IRecipeProductWorker maker, StatDef stat, bool applyPostProcess = true)
		{
			if (stat == StatDefOf.FoodPoisonChance)
			{
				return 0.0005f;
			}
			return 1f;
		}
	}
	internal static class GenRecipe2
	{
		public static IEnumerable<Thing> MakeRecipeProducts(RecipeDef recipeDef, IRecipeProductWorker worker, List<Thing> ingredients, Thing dominantIngredient, IBillGiver billGiver, Precept_ThingStyle precept = null)
		{
			return MakeRecipeProductsInt(recipeDef, worker, ingredients, dominantIngredient, billGiver, precept);
		}

		public static IEnumerable<Thing> MakeRecipeProductsInt(RecipeDef recipeDef, IRecipeProductWorker worker, List<Thing> ingredients, Thing dominantIngredient, IBillGiver billGiver, Precept_ThingStyle precept = null)
		{
			float efficiency = ((recipeDef.efficiencyStat != null) ? worker.GetStatValue(recipeDef.efficiencyStat) : 1f);
			if (recipeDef.workTableEfficiencyStat != null)
			{
				Building_WorkTable building_WorkTable = (Building_WorkTable)(object)((billGiver is Building_WorkTable) ? billGiver : null);
				if (building_WorkTable != null)
				{
					efficiency *= StatExtension.GetStatValue((Thing)(object)building_WorkTable, recipeDef.workTableEfficiencyStat, true, -1);
				}
			}
			if (recipeDef.products != null)
			{
				for (int i = 0; i < recipeDef.products.Count; i++)
				{
					ThingDefCountClass prod = recipeDef.products[i];
					ThingDef stuffDef = ((!((BuildableDef)prod.thingDef).MadeFromStuff) ? null : dominantIngredient.def);
					Thing product = ThingMaker.MakeThing(prod.thingDef, stuffDef);
					product.stackCount = Mathf.CeilToInt((float)prod.count * efficiency);
					if (dominantIngredient != null)
					{
						CompColorableUtility.SetColor(product, dominantIngredient.DrawColor, false);
					}
					CompIngredients ingredientsComp = ThingCompUtility.TryGetComp<CompIngredients>(product);
					if (ingredientsComp != null)
					{
						for (int l = 0; l < ingredients.Count; l++)
						{
							ingredientsComp.RegisterIngredient(ingredients[l].def);
						}
					}
					CompFoodPoisonable foodPoisonable = ThingCompUtility.TryGetComp<CompFoodPoisonable>(product);
					if (foodPoisonable != null)
					{
						Room room = worker.GetRoom((RegionType)14);
						float chance = ((room == null) ? RoomStatDefOf.FoodPoisonChance.roomlessScore : room.GetStat(RoomStatDefOf.FoodPoisonChance));
						if (Rand.Chance(chance))
						{
							foodPoisonable.SetPoisoned((FoodPoisonCause)2);
						}
						else
						{
							float statValue = worker.GetStatValue(StatDefOf.FoodPoisonChance);
							if (Rand.Chance(statValue))
							{
								foodPoisonable.SetPoisoned((FoodPoisonCause)1);
							}
						}
					}
					yield return PostProcessProduct(product, recipeDef, worker, precept);
				}
			}
			if (recipeDef.specialProducts == null)
			{
				yield break;
			}
			for (int j = 0; j < recipeDef.specialProducts.Count; j++)
			{
				for (int k = 0; k < ingredients.Count; k++)
				{
					Thing ing = ingredients[k];
					SpecialProductType specialProductType = recipeDef.specialProducts[j];
					if ((int)specialProductType > 0)
					{
						if ((int)specialProductType != 1)
						{
							continue;
						}
						foreach (Thing product2 in ing.SmeltProducts(efficiency))
						{
							yield return PostProcessProduct(product2, recipeDef, worker, precept);
						}
						continue;
					}
					foreach (Thing product3 in ButcherProducts(ing, efficiency, worker))
					{
						yield return PostProcessProduct(product3, recipeDef, worker, precept);
					}
				}
			}
		}

		private static Thing PostProcessProduct(Thing product, RecipeDef recipeDef, IRecipeProductWorker worker, Precept_ThingStyle precept = null)
		{
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			CompQuality val = ThingCompUtility.TryGetComp<CompQuality>(product);
			if (val != null)
			{
				if (recipeDef.workSkill == null)
				{
					Log.Error(((object)recipeDef)?.ToString() + " needs workSkill because it creates a product with a quality.");
				}
				int skillLevel = worker.GetSkillLevel(recipeDef.workSkill);
				QualityCategory val2 = QualityUtility.GenerateQualityCreatedByPawn(skillLevel, false);
				val.SetQuality(val2, (ArtGenerationContext?)(ArtGenerationContext)1);
			}
			if (precept != null)
			{
				product.StyleSourcePrecept = precept;
			}
			if (product.def.Minifiable)
			{
				product = (Thing)(object)MinifyUtility.MakeMinified(product, (DestroyMode)0);
			}
			return product;
		}

		private static IEnumerable<Thing> ButcherProducts(Thing thing, float efficiency, IRecipeProductWorker worker)
		{
			Corpse val = (Corpse)(object)((thing is Corpse) ? thing : null);
			if (val != null)
			{
				return ButcherProducts(val, efficiency, worker);
			}
			return thing.ButcherProducts((Pawn)null, efficiency);
		}

		public static IEnumerable<Thing> ButcherProducts(Corpse corpse, float efficiency, IRecipeProductWorker worker)
		{
			foreach (Thing item in ((Thing)corpse.InnerPawn).ButcherProducts((Pawn)null, efficiency))
			{
				yield return item;
			}
			if (corpse.InnerPawn.RaceProps.BloodDef != null)
			{
				FilthMaker.TryMakeFilth(worker.Position, worker.Map, corpse.InnerPawn.RaceProps.BloodDef, GenText.LabelIndefinite(corpse.InnerPawn), 1, (FilthSourceFlags)0);
			}
		}
	}
	public abstract class Graphic_Linked2 : Graphic
	{
		public Graphic subGraphic;

		protected Material[] subMats = (Material[])(object)new Material[16];

		public override Material MatSingle => subMats[0];

		public Graphic_Linked2()
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Expected O, but got Unknown
			subGraphic = (Graphic)new Graphic_Single();
		}

		public override Material MatSingleFor(Thing thing)
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			return LinkedDrawMatFrom(thing, thing.Position);
		}

		public override void Init(GraphicRequest req)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			base.data = req.graphicData;
			base.path = req.path;
			base.color = req.color;
			base.colorTwo = req.colorTwo;
			base.drawSize = req.drawSize;
			subGraphic.Init(req);
			CreateSubMats();
		}

		public void CreateSubMats()
		{
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Expected O, but got Unknown
			Vector2 mainTextureScale = default(Vector2);
			((Vector2)(ref mainTextureScale))..ctor(0.25f, 0.25f);
			Vector2 mainTextureOffset = default(Vector2);
			for (int i = 0; i < 16; i++)
			{
				float num = (float)(i % 4) * 0.25f;
				float num2 = (float)(i / 4) * 0.25f;
				((Vector2)(ref mainTextureOffset))..ctor(num, num2);
				Material val = new Material(subGraphic.MatSingle)
				{
					name = ((Object)subGraphic.MatSingle).name + "_ASM" + i,
					mainTextureScale = mainTextureScale,
					mainTextureOffset = mainTextureOffset
				};
				subMats[i] = val;
			}
		}

		private Material LinkedDrawMatFrom(Thing parent, IntVec3 cell)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			int num = 0;
			int num2 = 1;
			for (int i = 0; i < 4; i++)
			{
				if (ShouldLinkWith(new Rot4(i), parent))
				{
					num += num2;
				}
				num2 *= 2;
			}
			LinkDirections linkSet = (LinkDirections)(byte)num;
			return LinkedMaterial(parent, linkSet);
		}

		private Material LinkedMaterial(Thing _, LinkDirections linkSet)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			return subMats[linkSet];
		}

		protected abstract bool ShouldLinkWith(Rot4 dir, Thing parent);

		public override void Print(SectionLayer layer, Thing thing, float extraRotation)
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			Material val = LinkedDrawMatFrom(thing, thing.Position);
			Printer_Plane.PrintPlane((MapDrawLayer)(object)layer, GenThing.TrueCenter(thing), base.drawSize, val, 0f, false, (Vector2[])null, (Color32[])null, 0.01f, 0f);
		}

		public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
		{
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			if (!(thing is MinifiedThing))
			{
				GraphicDatabase.Get<Graphic_Single>(((BuildableDef)thingDef).uiIconPath, ShaderTypeDefOf.EdgeDetect.Shader, thingDef.graphicData.drawSize, base.color, base.colorTwo).DrawWorker(loc, rot, thingDef, thing, extraRotation);
			}
			else
			{
				((Graphic)this).DrawWorker(loc, rot, thingDef, thing, extraRotation);
			}
		}
	}
	public abstract class Graphic_Link2<T> : Graphic_Linked2 where T : Graphic_Linked2, new()
	{
		public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			T val = new T
			{
				subGraphic = subGraphic.GetColoredVersion(newShader, newColor, newColorTwo),
				data = ((Graphic)this).data
			};
			val.CreateSubMats();
			return (Graphic)(object)val;
		}
	}
	[StaticConstructorOnStartup]
	public class Graphic_LinkedConveyorV2 : Graphic_Linked, IHaveGraphicExtraData
	{
		public static Material arrow00;

		public Material arrow;

		public Vector3[] arrowOffsetsByRot4 = (Vector3[])(object)new Vector3[4]
		{
			new Vector3(0f, 0.1f, 0f),
			new Vector3(0f, 0.1f, 0f),
			new Vector3(0f, 0.1f, 0f),
			new Vector3(0f, 0.1f, 0f)
		};

		private static readonly Dictionary<Type, Func<ThingDef, Rot4, Rot4, ConveyorLevel, bool>> canSendTos;

		private static readonly Dictionary<Type, Func<ThingDef, Rot4, Rot4, ConveyorLevel, bool>> canGetFroms;

		public Graphic_LinkedConveyorV2()
		{
		}//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)


		public Graphic_LinkedConveyorV2(Graphic subGraphic)
			: base(subGraphic)
		{
		}//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)


		public override void Init(GraphicRequest req)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			GraphicExtraData extraData = GraphicExtraData.Extract(req, out req);
			ExtraInit(req, extraData);
		}

		public virtual void ExtraInit(GraphicRequest req, GraphicExtraData extraData)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Expected O, but got Unknown
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			//IL_010b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			//IL_0138: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_015b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0160: Unknown result type (might be due to invalid IL or missing references)
			//IL_0183: Unknown result type (might be due to invalid IL or missing references)
			//IL_0188: Unknown result type (might be due to invalid IL or missing references)
			arrow = arrow00;
			((Graphic)this).data = req.graphicData;
			((Graphic)this).color = req.color;
			((Graphic)this).colorTwo = req.colorTwo;
			((Graphic)this).drawSize = req.drawSize;
			base.subGraphic = (Graphic)new Graphic_Single();
			if (extraData == null)
			{
				base.subGraphic.Init(req);
				((Graphic)this).path = req.path;
				return;
			}
			GraphicRequest val = GraphicExtraData.CopyGraphicRequest(req, extraData.texPath);
			((Graphic)this).path = extraData.texPath;
			base.subGraphic.Init(val);
			if (extraData.arrowDrawOffset.HasValue)
			{
				Vector3 value = extraData.arrowDrawOffset.Value;
				for (int i = 0; i < 4; i++)
				{
					arrowOffsetsByRot4[i] = new Vector3(value.x, value.y, value.z);
				}
			}
			if (extraData.arrowEastDrawOffset.HasValue)
			{
				arrowOffsetsByRot4[1] = extraData.arrowEastDrawOffset.Value;
			}
			if (extraData.arrowWestDrawOffset.HasValue)
			{
				arrowOffsetsByRot4[3] = extraData.arrowWestDrawOffset.Value;
			}
			if (extraData.arrowNorthDrawOffset.HasValue)
			{
				arrowOffsetsByRot4[0] = extraData.arrowNorthDrawOffset.Value;
			}
			if (extraData.arrowSouthDrawOffset.HasValue)
			{
				arrowOffsetsByRot4[2] = extraData.arrowSouthDrawOffset.Value;
			}
			if (extraData.arrowTexPath1 != null)
			{
				arrow = MaterialPool.MatFrom(extraData.arrowTexPath1);
			}
		}

		public override void Print(SectionLayer layer, Thing thing, float extraRotation)
		{
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			if (thing is Building_BeltConveyorUGConnector || !(thing is IBeltConveyorLinkable { IsUnderground: not false }) || layer is SectionLayer_UGConveyor)
			{
				((Graphic_Linked)this).Print(layer, thing, extraRotation);
				Vector3 val = GenThing.TrueCenter(thing);
				Vector3[] array = arrowOffsetsByRot4;
				Rot4 rotation = thing.Rotation;
				Vector3 val2 = val + array[((Rot4)(ref rotation)).AsInt];
				Vector2 drawSize = ((Graphic)this).drawSize;
				Material obj = arrow;
				rotation = thing.Rotation;
				Printer_Plane.PrintPlane((MapDrawLayer)(object)layer, val2, drawSize, obj, ((Rot4)(ref rotation)).AsAngle, false, (Vector2[])null, (Color32[])null, 0.01f, 0f);
			}
		}

		public override bool ShouldLinkWith(IntVec3 c, Thing parent)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Expected O, but got Unknown
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0138: Unknown result type (might be due to invalid IL or missing references)
			//IL_013d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0273: Unknown result type (might be due to invalid IL or missing references)
			//IL_0278: Unknown result type (might be due to invalid IL or missing references)
			//IL_0150: Unknown result type (might be due to invalid IL or missing references)
			//IL_028f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0221: Unknown result type (might be due to invalid IL or missing references)
			//IL_0228: Unknown result type (might be due to invalid IL or missing references)
			//IL_0360: Unknown result type (might be due to invalid IL or missing references)
			//IL_0367: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0311: Unknown result type (might be due to invalid IL or missing references)
			//IL_0318: Unknown result type (might be due to invalid IL or missing references)
			if (!GenGrid.InBounds(c, parent.Map))
			{
				return false;
			}
			if (!(parent is Blueprint))
			{
				IBeltConveyorLinkable belt = parent as IBeltConveyorLinkable;
				return GridsUtility.GetThingList(c, parent.Map).OfType<IBeltConveyorLinkable>().Any((IBeltConveyorLinkable other) => other.CanLinkTo(belt) || (!belt.IsEndOfLine && belt.CanLinkTo(other)));
			}
			ThingDef val = (ThingDef)parent.def.entityDefToBuild;
			Rot4 arg = Rot4.North;
			foreach (Rot4 item in Enumerable.Range(0, 4).Select((Func<int, Rot4>)((int n) => new Rot4(n))))
			{
				Rot4 current = item;
				if (parent.Position + ((Rot4)(ref current)).FacingCell == c)
				{
					arg = current;
					break;
				}
			}
			ConveyorLevel[] array = (ConveyorLevel[])Enum.GetValues(typeof(ConveyorLevel));
			foreach (ConveyorLevel arg2 in array)
			{
				if (canSendTos[val.thingClass](val, parent.Rotation, arg, arg2))
				{
					foreach (Thing thing in GridsUtility.GetThingList(c, parent.Map))
					{
						Blueprint val2 = (Blueprint)(object)((thing is Blueprint) ? thing : null);
						if (val2 != null)
						{
							BuildableDef entityDefToBuild = ((Thing)val2).def.entityDefToBuild;
							ThingDef val3 = (ThingDef)(object)((entityDefToBuild is ThingDef) ? entityDefToBuild : null);
							Type type = val3?.thingClass;
							if (typeof(Building_BeltConveyor).IsAssignableFrom(type) && canGetFroms[type](val3, ((Thing)val2).Rotation, ((Rot4)(ref arg)).Opposite, arg2))
							{
								return true;
							}
						}
						else if (thing is IBeltConveyorLinkable && canGetFroms[((object)thing).GetType()](thing.def, thing.Rotation, ((Rot4)(ref arg)).Opposite, arg2))
						{
							return true;
						}
					}
				}
				if (!canGetFroms[val.thingClass](val, parent.Rotation, arg, arg2))
				{
					continue;
				}
				foreach (Thing thing2 in GridsUtility.GetThingList(c, parent.Map))
				{
					Blueprint val4 = (Blueprint)(object)((thing2 is Blueprint) ? thing2 : null);
					if (val4 != null)
					{
						BuildableDef entityDefToBuild2 = ((Thing)val4).def.entityDefToBuild;
						ThingDef val5 = (ThingDef)(object)((entityDefToBuild2 is ThingDef) ? entityDefToBuild2 : null);
						Type type2 = val5?.thingClass;
						if (typeof(Building_BeltConveyor).IsAssignableFrom(type2) && canSendTos[type2](val5, ((Thing)val4).Rotation, ((Rot4)(ref arg)).Opposite, arg2))
						{
							return true;
						}
					}
					else if (thing2 is IBeltConveyorLinkable && canSendTos[((object)thing2).GetType()](thing2.def, thing2.Rotation, ((Rot4)(ref arg)).Opposite, arg2))
					{
						return true;
					}
				}
			}
			return false;
		}

		static Graphic_LinkedConveyorV2()
		{
			canSendTos = new Dictionary<Type, Func<ThingDef, Rot4, Rot4, ConveyorLevel, bool>>();
			canGetFroms = new Dictionary<Type, Func<ThingDef, Rot4, Rot4, ConveyorLevel, bool>>();
			arrow00 = MaterialPool.MatFrom("Belts/SmallArrow00");
			canSendTos[typeof(Building_BeltConveyor)] = Building_BeltConveyor.CanDefSendToRot4AtLevel;
			canGetFroms[typeof(Building_BeltConveyor)] = Building_BeltConveyor.CanDefReceiveFromRot4AtLevel;
			canSendTos[typeof(Building_BeltConveyorUGConnector)] = Building_BeltConveyorUGConnector.CanDefSendToRot4AtLevel;
			canGetFroms[typeof(Building_BeltConveyorUGConnector)] = Building_BeltConveyorUGConnector.CanDefReceiveFromRot4AtLevel;
			canSendTos[typeof(Building_BeltSplitter)] = Building_BeltSplitter.CanDefSendToRot4AtLevel;
			canGetFroms[typeof(Building_BeltSplitter)] = Building_BeltSplitter.CanDefReceiveFromRot4AtLevel;
		}
	}
	public class Graphic_LinkedConveyorWall : Graphic_LinkedConveyorV2, IHaveGraphicExtraData
	{
		private bool showW;

		private bool showS;

		private bool showE;

		private GraphicData transitionGData;

		private List<ThingDef> sameLinkDefs;

		public override void ExtraInit(GraphicRequest req, GraphicExtraData extraData)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Expected O, but got Unknown
			base.ExtraInit(req, extraData);
			if (extraData == null)
			{
				Log.Error("PRF's Wall Conveyor graphic requires GraphicExtraData");
				return;
			}
			if (extraData.graphicData1 == null)
			{
				Log.Error("PRF's Wall Conveyor grahpic requires GraphicExtraData's graphicData1");
				return;
			}
			transitionGData = new GraphicData();
			transitionGData.CopyFrom(extraData.graphicData1);
			sameLinkDefs = extraData.specialLinkDefs.Select((string s) => DefDatabase<ThingDef>.GetNamed(s, true)).ToList();
		}

		public override void Print(SectionLayer layer, Thing thing, float extraRotation)
		{
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0108: Unknown result type (might be due to invalid IL or missing references)
			//IL_010d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Unknown result type (might be due to invalid IL or missing references)
			showW = false;
			showS = false;
			showE = false;
			base.Print(layer, thing, extraRotation);
			if (showW)
			{
				Material matWest = transitionGData.Graphic.MatWest;
				Printer_Plane.PrintPlane((MapDrawLayer)(object)layer, GenThing.TrueCenter(thing) + new Vector3(0f, 0.1f, 0f), Vector2.one, matWest, 0f, false, (Vector2[])null, (Color32[])null, 0.01f, 0f);
			}
			if (showS)
			{
				Material matWest = transitionGData.Graphic.MatSouth;
				Printer_Plane.PrintPlane((MapDrawLayer)(object)layer, GenThing.TrueCenter(thing) + new Vector3(0f, 0.1f, 0f), Vector2.one, matWest, 0f, false, (Vector2[])null, (Color32[])null, 0.01f, 0f);
			}
			if (showE)
			{
				Material matWest = transitionGData.Graphic.MatEast;
				Printer_Plane.PrintPlane((MapDrawLayer)(object)layer, GenThing.TrueCenter(thing) + new Vector3(0f, 0.1f, 0f), Vector2.one, matWest, 0f, false, (Vector2[])null, (Color32[])null, 0.01f, 0f);
			}
		}

		public override bool ShouldLinkWith(IntVec3 c, Thing parent)
		{
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00db: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			if (parent is Blueprint)
			{
				return base.ShouldLinkWith(c, parent);
			}
			IntVec3 val = c - parent.Position;
			if (val == IntVec3.North)
			{
				return base.ShouldLinkWith(c, parent);
			}
			if (!GenGrid.InBounds(c, parent.Map))
			{
				return false;
			}
			IBeltConveyorLinkable beltConveyorLinkable = parent as IBeltConveyorLinkable;
			IBeltConveyorLinkable beltConveyorLinkable2 = GridsUtility.GetThingList(c, parent.Map).OfType<IBeltConveyorLinkable>().FirstOrDefault(beltConveyorLinkable.HasLinkWith);
			if (beltConveyorLinkable2 == null)
			{
				return false;
			}
			if (!sameLinkDefs.Contains(((Thing)((beltConveyorLinkable2 is Thing) ? beltConveyorLinkable2 : null)).def))
			{
				if (val == IntVec3.East)
				{
					showE = true;
				}
				else if (val == IntVec3.South)
				{
					showS = true;
				}
				else
				{
					showW = true;
				}
			}
			return true;
		}
	}
	[StaticConstructorOnStartup]
	public class Graphic_LinkedSplitter : Graphic_LinkedConveyorV2, IHaveGraphicExtraData
	{
		public static Material arrow00b;

		public static Material arrow01;

		public GraphicData splitterBuildingDoorOpen;

		public GraphicData splitterBuildingDoorClosed;

		public GraphicData splitterBuildingBlueprint;

		public Material arrowInput;

		public Material arrowOutput;

		public Vector3 BuildingOffset => new Vector3(0f, 0.3f, 0f);

		static Graphic_LinkedSplitter()
		{
			arrow01 = MaterialPool.MatFrom("Belts/SmallArrow01");
			arrow00b = MaterialPool.MatFrom("Belts/SmallArrow00b");
		}

		public override void Init(GraphicRequest req)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			GraphicRequest outReq;
			GraphicExtraData extraData = GraphicExtraData.Extract(req, out outReq);
			ExtraInit(outReq, extraData);
		}

		public override void ExtraInit(GraphicRequest req, GraphicExtraData extraData)
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Expected O, but got Unknown
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Expected O, but got Unknown
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Expected O, but got Unknown
			//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			arrowInput = arrow00b;
			arrowOutput = arrow01;
			base.ExtraInit(req, extraData);
			if (extraData == null)
			{
				Log.Error("PRF: invalid XML for conveyor Splitter's graphic:\n   it must have <texPath>[extraData]...[texPath2]path/to/building[/texPath2]</texPath>\n   but has string: " + req.path);
				return;
			}
			string texPath = extraData.texPath2;
			GraphicData val = new GraphicData();
			val.graphicClass = typeof(Graphic_Single);
			val.texPath = texPath + "_Open";
			val.drawSize = Vector2.one;
			splitterBuildingDoorOpen = val;
			val = new GraphicData();
			val.graphicClass = typeof(Graphic_Single);
			val.texPath = texPath + "_Closed";
			val.drawSize = Vector2.one;
			splitterBuildingDoorClosed = val;
			val = new GraphicData();
			val.graphicClass = typeof(Graphic_Single);
			val.texPath = texPath + "_Blueprint";
			val.drawSize = Vector2.one;
			splitterBuildingBlueprint = val;
			if (extraData.arrowTexPath1 != null)
			{
				arrowOutput = MaterialPool.MatFrom(extraData.arrowTexPath1);
			}
			if (extraData.arrowTexPath2 != null)
			{
				arrowInput = MaterialPool.MatFrom(extraData.arrowTexPath2);
			}
		}

		public Vector3 ArrowOffset(Rot4 rot)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			Vector3 val = arrowOffsetsByRot4[((Rot4)(ref rot)).AsInt];
			Vector3 buildingOffset = BuildingOffset;
			Vector3 result = default(Vector3);
			((Vector3)(ref result))..ctor(val.x, Mathf.Max(val.y, buildingOffset.y + 0.01f), val.z);
			return result;
		}

		public override void Print(SectionLayer layer, Thing thing, float extraRotation)
		{
			//IL_025d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0271: Unknown result type (might be due to invalid IL or missing references)
			//IL_0276: Unknown result type (might be due to invalid IL or missing references)
			//IL_027b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_014b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0156: Unknown result type (might be due to invalid IL or missing references)
			//IL_0169: Unknown result type (might be due to invalid IL or missing references)
			//IL_0174: Unknown result type (might be due to invalid IL or missing references)
			//IL_0179: Unknown result type (might be due to invalid IL or missing references)
			//IL_017e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0183: Unknown result type (might be due to invalid IL or missing references)
			//IL_0187: Unknown result type (might be due to invalid IL or missing references)
			//IL_018d: Unknown result type (might be due to invalid IL or missing references)
			//IL_018f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0194: Unknown result type (might be due to invalid IL or missing references)
			//IL_019a: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0201: Unknown result type (might be due to invalid IL or missing references)
			//IL_0206: Unknown result type (might be due to invalid IL or missing references)
			//IL_020c: Unknown result type (might be due to invalid IL or missing references)
			//IL_020e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0213: Unknown result type (might be due to invalid IL or missing references)
			//IL_0219: Unknown result type (might be due to invalid IL or missing references)
			base.Print(layer, thing, extraRotation);
			Building_BeltSplitter splitter = thing as Building_BeltSplitter;
			if (splitter != null)
			{
				if (splitter.IsUnderground && !(layer is SectionLayer_UGConveyor))
				{
					return;
				}
				if ((splitter.OutputLinks.TryGetValue(Rot4.South, out var value) && value.Active) || splitter.IncomingLinks.Any(delegate(IBeltConveyorLinkable o)
				{
					//IL_0006: Unknown result type (might be due to invalid IL or missing references)
					//IL_000b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0010: Unknown result type (might be due to invalid IL or missing references)
					//IL_0013: Unknown result type (might be due to invalid IL or missing references)
					//IL_0018: Unknown result type (might be due to invalid IL or missing references)
					//IL_001e: Unknown result type (might be due to invalid IL or missing references)
					IntVec3 position = ((Thing)splitter).Position;
					Rot4 south = Rot4.South;
					return position + ((Rot4)(ref south)).FacingCell == o.Position;
				}))
				{
					Material val = splitterBuildingDoorOpen.Graphic.MatSingleFor(thing);
					Printer_Plane.PrintPlane((MapDrawLayer)(object)layer, GenThing.TrueCenter(thing) + BuildingOffset, Vector2.one, val, 0f, false, (Vector2[])null, (Color32[])null, 0.01f, 0f);
				}
				else
				{
					Material val2 = splitterBuildingDoorClosed.Graphic.MatSingleFor(thing);
					Printer_Plane.PrintPlane((MapDrawLayer)(object)layer, GenThing.TrueCenter(thing) + BuildingOffset, Vector2.one, val2, 0f, false, (Vector2[])null, (Color32[])null, 0.01f, 0f);
				}
				foreach (IBeltConveyorLinkable incomingLink in splitter.IncomingLinks)
				{
					if (incomingLink.Position.IsNextTo(((Thing)splitter).Position))
					{
						Rot4 rot = Rot4.FromIntVec3(incomingLink.Position - ((Thing)splitter).Position);
						Vector3 val3 = GenThing.TrueCenter(thing) + ArrowOffset(rot);
						Vector2 drawSize = ((Graphic)this).drawSize;
						Material obj = arrowInput;
						Rot4 opposite = ((Rot4)(ref rot)).Opposite;
						Printer_Plane.PrintPlane((MapDrawLayer)(object)layer, val3, drawSize, obj, ((Rot4)(ref opposite)).AsAngle, false, (Vector2[])null, (Color32[])null, 0.01f, 0f);
					}
				}
				{
					foreach (Rot4 activeOutputDirection in splitter.ActiveOutputDirections)
					{
						Rot4 current2 = activeOutputDirection;
						Printer_Plane.PrintPlane((MapDrawLayer)(object)layer, GenThing.TrueCenter(thing) + ArrowOffset(current2), ((Graphic)this).drawSize, arrowOutput, ((Rot4)(ref current2)).AsAngle, false, (Vector2[])null, (Color32[])null, 0.01f, 0f);
					}
					return;
				}
			}
			Printer_Plane.PrintPlane((MapDrawLayer)(object)layer, GenThing.TrueCenter(thing) + new Vector3(0f, 0.3f, 0f), Vector2.one, splitterBuildingBlueprint.Graphic.MatSingleFor(thing), 0f, false, (Vector2[])null, (Color32[])null, 0.01f, 0f);
		}
	}
	internal class Graphic_Selectable : Graphic_Collection
	{
		private Dictionary<string, Graphic> pathDic = new Dictionary<string, Graphic>();

		public override Material MatSingle => base.subGraphics[0].MatSingle;

		public override bool ShouldDrawRotated => true;

		public Graphic Get(string path)
		{
			if (path == null)
			{
				Ops.Option<GraphicData>(base.subGraphics[0].data).ForEach(delegate(GraphicData d)
				{
					d.drawRotated = true;
				});
				return base.subGraphics[0];
			}
			if (!pathDic.ContainsKey(path))
			{
				pathDic[path] = base.subGraphics.Where((Graphic x) => x.path == path).First();
				Ops.Option<GraphicData>(pathDic[path].data).ForEach(delegate(GraphicData d)
				{
					d.drawRotated = true;
				});
			}
			return pathDic[path];
		}

		public override void Init(GraphicRequest req)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			((Graphic_Collection)this).Init(req);
			base.subGraphics.ForEach(delegate(Graphic g)
			{
				Ops.Option<GraphicData>(g.data).ForEach(delegate(GraphicData d)
				{
					d.drawRotated = true;
				});
			});
		}
	}
	public class Graphic_LinkedConveyorTwo : Graphic_Two<Graphic_LinkedConveyorV2>
	{
	}
	public class Graphic_LinkedSplitterTwo : Graphic_Two<Graphic_LinkedSplitter>
	{
	}
	public class Graphic_LinkedConveyorWallTwo : Graphic_Two<Graphic_LinkedConveyorWall>
	{
	}
	public class Graphic_Two<T> : Graphic, IHaveGraphicExtraData where T : Graphic, new()
	{
		protected Graphic NS;

		protected Graphic EW;

		public override Material MatSingle => NS.MatSingle;

		public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
		{
			return NS;
		}

		public override void Init(GraphicRequest req)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			GraphicRequest outReq;
			GraphicExtraData extraData = GraphicExtraData.Extract(req, out outReq);
			ExtraInit(outReq, extraData);
		}

		public virtual void ExtraInit(GraphicRequest req, GraphicExtraData extraData)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			base.data = req.graphicData;
			base.color = req.color;
			base.colorTwo = req.colorTwo;
			base.drawSize = req.drawSize;
			base.path = extraData?.texPath ?? req.path;
			EW = MakeSubgraphic(req, extraData, "_East");
			NS = MakeSubgraphic(req, extraData, "_North");
		}

		protected Graphic MakeSubgraphic(GraphicRequest req, GraphicExtraData extraData, string texSuffix)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			GraphicRequest val = GraphicExtraData.CopyGraphicRequest(req);
			val.graphicClass = typeof(T);
			T val2 = new T();
			if (val2 is IHaveGraphicExtraData haveGraphicExtraData && extraData != null)
			{
				string texPath = extraData.texPath;
				extraData.texPath += texSuffix;
				haveGraphicExtraData.ExtraInit(val, extraData);
				extraData.texPath = texPath;
			}
			else
			{
				GraphicData graphicData = val.graphicData;
				graphicData.texPath += texSuffix;
				val.path = val.graphicData.texPath;
				((Graphic)val2).Init(val);
			}
			return (Graphic)(object)val2;
		}

		public override Material MatSingleFor(Thing thing)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			if (thing.Rotation == Rot4.North || thing.Rotation == Rot4.South)
			{
				return NS.MatSingleFor(thing);
			}
			return EW.MatSingleFor(thing);
		}

		public override void Print(SectionLayer layer, Thing thing, float extraRotation)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			if (thing.Rotation == Rot4.East || thing.Rotation == Rot4.West)
			{
				EW.Print(layer, thing, extraRotation);
			}
			else
			{
				NS.Print(layer, thing, extraRotation);
			}
		}

		public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			if (rot == Rot4.North || rot == Rot4.South)
			{
				NS.DrawWorker(loc, rot, thingDef, thing, extraRotation);
			}
			else
			{
				EW.DrawWorker(loc, rot, thingDef, thing, extraRotation);
			}
		}

		public override Material MatAt(Rot4 rot, Thing thing = null)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			if (rot == Rot4.East || rot == Rot4.West)
			{
				return EW.MatAt(rot, thing);
			}
			return NS.MatAt(rot, thing);
		}
	}
	public class Graphic_WallLight : Graphic_Link2<Graphic_WallLight>
	{
		public override bool ShouldDrawRotated => ((Graphic)this).data == null || ((Graphic)this).data.drawRotated;

		protected override bool ShouldLinkWith(Rot4 dir, Thing parent)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			IntVec3 val = parent.Position + ((Rot4)(ref dir)).FacingCell;
			return GenGrid.InBounds(val, parent.Map) && IsWall(val, parent.Map);
		}

		private static bool IsWall(IntVec3 pos, Map map)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return !(from t in GridsUtility.GetThingList(pos, map)
				where (int)t.def.category == 3
				where t.def.building != null
				select t).Any((Thing t) => (int)((BuildableDef)t.def).passability == 2 || t is Building_Door);
		}

		public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00df: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_0122: Unknown result type (might be due to invalid IL or missing references)
			//IL_0128: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			int num = 0;
			List<PlaceWorker> placeWorkers = ((BuildableDef)thingDef).PlaceWorkers;
			if (placeWorkers != null && placeWorkers.All(delegate(PlaceWorker p)
			{
				//IL_0008: Unknown result type (might be due to invalid IL or missing references)
				//IL_000d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0013: Unknown result type (might be due to invalid IL or missing references)
				//IL_001f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				AcceptanceReport val3 = p.AllowsPlacing((BuildableDef)(object)thingDef, IntVec3Utility.ToIntVec3(loc), rot, Find.CurrentMap, (Thing)null, (Thing)null);
				return ((AcceptanceReport)(ref val3)).Accepted;
			}))
			{
				int num2 = 1;
				for (int num3 = 0; num3 < 4; num3++)
				{
					IntVec3 pos = IntVec3Utility.ToIntVec3(loc) + GenAdj.CardinalDirections[num3];
					if (IsWall(pos, Find.CurrentMap))
					{
						num += num2;
					}
					num2 *= 2;
				}
			}
			Material val = subMats[(byte)num];
			val.shader = ShaderDatabase.Transparent;
			Quaternion val2 = ((Graphic)this).QuatFromRot(rot);
			if (extraRotation != 0f)
			{
				val2 *= Quaternion.Euler(Vector3.up * extraRotation);
			}
			Graphics.DrawMesh(((Graphic)this).MeshAt(rot), loc, val2, val, 0);
			if (((Graphic)this).ShadowGraphic != null)
			{
				((Graphic)((Graphic)this).ShadowGraphic).DrawWorker(loc, rot, thingDef, thing, extraRotation);
			}
		}
	}
	public interface IBeltConveyorLinkable : IPRF_Building, ILoadReferenceable
	{
		bool IsStuck { get; }

		IEnumerable<Rot4> ActiveOutputDirections { get; }

		bool IsUnderground { get; }

		float CarriedItemDrawHeight { get; }

		bool IsEndOfLine { get; set; }

		Rot4 Rotation { get; }

		IntVec3 Position { get; }

		bool Spawned { get; }

		void Link(IBeltConveyorLinkable linkable);

		void Unlink(IBeltConveyorLinkable linkable);

		bool CanSendToLevel(ConveyorLevel level);

		bool CanReceiveFromLevel(ConveyorLevel level);

		bool CanAcceptNow(Thing t);

		bool CanLinkTo(IBeltConveyorLinkable otherBeltLinkable, bool checkPosition = true);

		bool CanLinkFrom(IBeltConveyorLinkable otherBeltLinkable, bool checkPosition = true);

		bool HasLinkWith(IBeltConveyorLinkable otherBelt);
	}
	public enum ConveyorLevel
	{
		Underground = -1,
		Ground
	}
	internal interface IBeltConveyorSender
	{
		void NortifyReceivable();
	}
	internal interface IProductLimitation
	{
		int ProductLimitCount { get; set; }

		bool ProductLimitation { get; set; }

		bool CountStacks { get; set; }

		SlotGroup TargetSlotGroup { get; set; }

		bool ProductLimitationDisable { get; }
	}
	internal class ITab_ConveyorFilter : ITab
	{
		private static readonly Vector2 WinSize = new Vector2(300f, 500f);

		private static readonly Dictionary<Rot4, string> RotStrings = new Dictionary<Rot4, string>
		{
			{
				Rot4.North,
				"N"
			},
			{
				Rot4.East,
				"E"
			},
			{
				Rot4.South,
				"S"
			},
			{
				Rot4.West,
				"W"
			}
		};

		private string description;

		private Rot4? selectedDir;

		private List<SlotGroup> groups;

		private UIState uIState = new UIState();

		private Building_BeltSplitter Splitter => (Building_BeltSplitter)(object)((ITab)this).SelThing;

		public override bool IsVisible => true;

		public ITab_ConveyorFilter()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Expected O, but got Unknown
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).size = WinSize;
			((InspectTabBase)this).labelKey = "PRF.AutoMachineTool.Conveyor.OutputItemFilter.TabName";
			description = TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Conveyor.OutputItemFilter.Description"));
		}

		public override void OnOpen()
		{
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).OnOpen();
			groups = ((Thing)Splitter).Map.haulDestinationManager.AllGroups.ToList();
			selectedDir = Splitter.OutputLinks.Keys.FirstOrDefault();
		}

		private void directionUI(Rect rect, Rot4 rot, ref bool selected, ref bool disabeld, bool isInput)
		{
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00db: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_010d: Unknown result type (might be due to invalid IL or missing references)
			//IL_013a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0122: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			//IL_0148: Unknown result type (might be due to invalid IL or missing references)
			Texture2D val = RS.SplitterArrowUp;
			CommonColors.CellPattern pat = CommonColors.CellPattern.InputCell;
			if (isInput)
			{
				if (rot == Rot4.North)
				{
					val = RS.SplitterArrowDown;
				}
				else if (rot == Rot4.South)
				{
					val = RS.SplitterArrowUp;
				}
				else if (rot == Rot4.East)
				{
					val = RS.SplitterArrowLeft;
				}
				else if (rot == Rot4.West)
				{
					val = RS.SplitterArrowRight;
				}
			}
			else
			{
				if (rot == Rot4.North)
				{
					val = RS.SplitterArrowUp;
				}
				else if (rot == Rot4.South)
				{
					val = RS.SplitterArrowDown;
				}
				else if (rot == Rot4.East)
				{
					val = RS.SplitterArrowRight;
				}
				else if (rot == Rot4.West)
				{
					val = RS.SplitterArrowLeft;
				}
				pat = CommonColors.CellPattern.OutputCell;
			}
			GUI.DrawTexture(rect, (Texture)(object)val, (ScaleMode)0, true, 0f, CommonColors.GetCellPatternColor(pat), 0f, 0f);
			if (!disabeld && !isInput)
			{
				GUI.DrawTexture(rect, (Texture)(object)RS.SplitterDisabled);
			}
			if (selected)
			{
				Widgets.DrawHighlight(rect);
				Widgets.DrawBox(rect, 1, (Texture2D)null);
			}
			Event current = Event.current;
			if ((int)current.type == 0 && ((Rect)(ref rect)).Contains(Event.current.mousePosition))
			{
				if (Input.GetMouseButton(0) && !isInput)
				{
					selected = true;
				}
				if (Input.GetMouseButton(1) && !isInput)
				{
					disabeld = !disabeld;
				}
			}
		}

		private Rot4 OppositeRot(Rot4 rot)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			if (rot == Rot4.North)
			{
				return Rot4.South;
			}
			if (rot == Rot4.South)
			{
				return Rot4.North;
			}
			if (rot == Rot4.East)
			{
				return Rot4.West;
			}
			if (rot == Rot4.West)
			{
				return Rot4.East;
			}
			return Rot4.North;
		}

		protected override void FillTab()
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Expected O, but got Unknown
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_011d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0141: Unknown result type (might be due to invalid IL or missing references)
			//IL_014e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0172: Unknown result type (might be due to invalid IL or missing references)
			//IL_017f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0214: Unknown result type (might be due to invalid IL or missing references)
			//IL_024e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0253: Unknown result type (might be due to invalid IL or missing references)
			//IL_0232: Unknown result type (might be due to invalid IL or missing references)
			//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_03c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0263: Unknown result type (might be due to invalid IL or missing references)
			//IL_0268: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_040e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0413: Unknown result type (might be due to invalid IL or missing references)
			//IL_0447: Unknown result type (might be due to invalid IL or missing references)
			//IL_044c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0455: Unknown result type (might be due to invalid IL or missing references)
			//IL_045b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0465: Unknown result type (might be due to invalid IL or missing references)
			//IL_0476: Unknown result type (might be due to invalid IL or missing references)
			//IL_048a: Unknown result type (might be due to invalid IL or missing references)
			//IL_04fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_051c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0522: Unknown result type (might be due to invalid IL or missing references)
			//IL_05ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_05b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_05c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_04fb: Expected O, but got Unknown
			//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_057a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0584: Expected O, but got Unknown
			//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0300: Unknown result type (might be due to invalid IL or missing references)
			//IL_0338: Unknown result type (might be due to invalid IL or missing references)
			//IL_031c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0356: Unknown result type (might be due to invalid IL or missing references)
			//IL_037d: Unknown result type (might be due to invalid IL or missing references)
			Rot4 valueOrDefault = selectedDir.GetValueOrDefault();
			if (!selectedDir.HasValue)
			{
				valueOrDefault = Splitter.OutputLinks.Keys.FirstOrDefault(null);
				selectedDir = valueOrDefault;
			}
			Listing_Standard val = new Listing_Standard();
			Rect val2 = GenUI.ContractedBy(new Rect(0f, 0f, WinSize.x, WinSize.y), 10f);
			((Listing)val).Begin(val2);
			((Listing)val).Gap(12f);
			Rect rect = ((Listing)val).GetRect(40f, 1f);
			Widgets.Label(rect, description);
			((Listing)val).Gap(12f);
			Dictionary<Rot4, Rect> dictionary = new Dictionary<Rot4, Rect>();
			rect = ((Listing)val).GetRect(120f, 1f);
			float num = ((Rect)(ref rect)).x + ((Rect)(ref rect)).width / 2f;
			dictionary[Rot4.North] = new Rect(num - 20f, ((Rect)(ref rect)).y, 40f, 40f);
			dictionary[Rot4.West] = new Rect(num - 60f, ((Rect)(ref rect)).y + 40f, 40f, 40f);
			dictionary[Rot4.East] = new Rect(num + 20f, ((Rect)(ref rect)).y + 40f, 40f, 40f);
			dictionary[Rot4.South] = new Rect(num - 20f, ((Rect)(ref rect)).y + 80f, 40f, 40f);
			foreach (Rot4 item in Enumerable.Range(0, 4).Select((Func<int, Rot4>)((int n) => new Rot4(n))))
			{
				Rot4 dir = item;
				bool disabeld = false;
				if (Splitter.OutputLinks.ContainsKey(dir))
				{
					disabeld = Splitter.OutputLinks[dir].Active;
				}
				Rot4? val3 = selectedDir;
				valueOrDefault = dir;
				bool selected = val3.HasValue && val3.GetValueOrDefault() == valueOrDefault;
				bool flag = false;
				foreach (IBeltConveyorLinkable item2 in Splitter.IncomingLinks.Where((IBeltConveyorLinkable l) => l.Position == ((Thing)Splitter).Position + ((Rot4)(ref dir)).FacingCell))
				{
					flag = Building_BeltSplitter.IsInputtingIntoThis(item2, dir);
					if (flag)
					{
						break;
					}
				}
				directionUI(dictionary[dir], dir, ref selected, ref disabeld, flag);
				if (selected)
				{
					selectedDir = dir;
				}
				if (Splitter.OutputLinks.ContainsKey(dir))
				{
					Splitter.OutputLinks[dir].Active = disabeld;
				}
				else if (disabeld)
				{
					Splitter.AddOutgoingLink(dir);
				}
			}
			((Listing)val).Gap(12f);
			Rot4 selectedRot = selectedDir.Value;
			if (!selectedDir.HasValue || !Splitter.OutputLinks.ContainsKey(selectedRot))
			{
				((Listing)val).End();
				return;
			}
			rect = ((Listing)val).GetRect(29f, 1f);
			float width = ((Rect)(ref rect)).width;
			float width2 = width / 2f - 1f;
			float num2 = width / 2f - 1f;
			((Rect)(ref rect)).width = width2;
			GameFont font = Text.Font;
			Text.Font = (GameFont)1;
			if (Widgets.ButtonText(rect, TaggedString.op_Implicit(Translator.Translate("Priority") + ": " + Splitter.OutputLinks[selectedRot].Priority.ToText()), true, true, true, (TextAnchor?)null))
			{
				Find.WindowStack.Add((Window)new FloatMenu(((IEnumerable<DirectionPriority>)(from k in Ops.GetEnumValues<DirectionPriority>()
					orderby (int)k descending
					select k)).Select((Func<DirectionPriority, FloatMenuOption>)((DirectionPriority d) => new FloatMenuOption(d.ToText(), (Action)delegate
				{
					//IL_001b: Unknown result type (might be due to invalid IL or missing references)
					Splitter.OutputLinks[selectedRot].Priority = d;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0))).ToList()));
			}
			Text.Font = font;
			((Rect)(ref rect)).x = width - num2;
			((Rect)(ref rect)).width = num2;
			if (Widgets.ButtonText(rect, TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Conveyor.FilterCopyFrom")), true, true, true, (TextAnchor?)null))
			{
				List<FloatMenuOption> list = ((IEnumerable<SlotGroup>)groups).Select((Func<SlotGroup, FloatMenuOption>)((SlotGroup g) => new FloatMenuOption(g.parent.SlotYielderLabel(), (Action)delegate
				{
					//IL_001b: Unknown result type (might be due to invalid IL or missing references)
					Splitter.OutputLinks[selectedRot].CopyAllowancesFrom(g.Settings.filter);
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0))).ToList();
				if (list.Count > 0)
				{
					Find.WindowStack.Add((Window)new FloatMenu(list));
				}
			}
			((Listing)val).Gap(12f);
			((Listing)val).End();
			float curHeight = ((Listing)val).CurHeight;
			Splitter.OutputLinks[selectedRot].DoThingFilterConfigWindow(GenUI.BottomPartPixels(val2, ((Rect)(ref val2)).height - curHeight), uIState);
		}
	}
	public class SlaughterSettings : IExposable
	{
		public ThingDef def;

		public bool doSlaughter;

		public bool hasBonds;

		public bool pregnancy;

		public bool trained;

		public int keepNoneYoungCount;

		public int keepNoneAdultCount;

		public int keepMaleAdultCount;

		public int keepMaleYoungCount;

		public int keepFemaleAdultCount;

		public int keepFemaleYoungCount;

		public SlaughterSettings()
		{
			doSlaughter = false;
			hasBonds = false;
			pregnancy = false;
			trained = false;
			keepNoneAdultCount = 10;
			keepNoneYoungCount = 10;
			keepMaleAdultCount = 10;
			keepMaleYoungCount = 10;
			keepFemaleAdultCount = 10;
			keepFemaleYoungCount = 10;
		}

		public void ExposeData()
		{
			Scribe_Values.Look<bool>(ref doSlaughter, "doSlaughter", false, false);
			Scribe_Values.Look<bool>(ref hasBonds, "hasBonds", false, false);
			Scribe_Values.Look<bool>(ref pregnancy, "pregnancy", false, false);
			Scribe_Values.Look<bool>(ref trained, "trained", false, false);
			Scribe_Values.Look<int>(ref keepMaleAdultCount, "keepMaleAdultCount", 10, false);
			Scribe_Values.Look<int>(ref keepMaleYoungCount, "keepMaleYoungCount", 10, false);
			Scribe_Values.Look<int>(ref keepFemaleAdultCount, "keepFemaleAdultCount", 10, false);
			Scribe_Values.Look<int>(ref keepFemaleYoungCount, "keepFemaleYoungCount", 10, false);
			Scribe_Values.Look<int>(ref keepNoneYoungCount, "keepNoneYoungCount", 10, false);
			Scribe_Values.Look<int>(ref keepNoneAdultCount, "keepNoneAdultCount", 10, false);
			Scribe_Defs.Look<ThingDef>(ref def, "def");
		}

		public int KeepCount(Gender gender, bool adult)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Invalid comparison between Unknown and I4
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Invalid comparison between Unknown and I4
			if ((int)gender != 1)
			{
				if ((int)gender == 2)
				{
					if (adult)
					{
						return keepFemaleAdultCount;
					}
					return keepFemaleYoungCount;
				}
				return adult ? keepNoneAdultCount : keepNoneYoungCount;
			}
			if (adult)
			{
				return keepMaleAdultCount;
			}
			return keepMaleYoungCount;
		}
	}
	internal interface ISlaughterhouse
	{
		Dictionary<ThingDef, SlaughterSettings> Settings { get; }
	}
	public class ITab_Slaughterhouse : ITab
	{
		private static readonly float[] ColumnWidth = new float[9] { 0.28f, 0.07f, 0.07f, 0.07f, 0.07f, 0.11f, 0.11f, 0.11f, 0.11f };

		private static readonly TipSignal SlaughterTip = new TipSignal(Translator.Translate("PRF.AutoMachineTool.Slaughterhouse.Setting.DoSlaughterTip"));

		private static readonly TipSignal HasBondsTip = new TipSignal(Translator.Translate("PRF.AutoMachineTool.Slaughterhouse.Setting.BondsTip"));

		private static readonly TipSignal PregnancyTip = new TipSignal(Translator.Translate("PRF.AutoMachineTool.Slaughterhouse.Setting.PregnancyTip"));

		private static readonly TipSignal TrainedTip = new TipSignal(Translator.Translate("PRF.AutoMachineTool.Slaughterhouse.Setting.TrainedTip"));

		private static readonly TipSignal KeepMaleChildCountTip = new TipSignal(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.Slaughterhouse.Setting.KeepCountTip", NamedArgument.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Male")), NamedArgument.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Young"))));

		private static readonly TipSignal KeepFemaleChildCountTip = new TipSignal(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.Slaughterhouse.Setting.KeepCountTip", NamedArgument.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Female")), NamedArgument.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Young"))));

		private static readonly TipSignal KeepMaleAdultCountTip = new TipSignal(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.Slaughterhouse.Setting.KeepCountTip", NamedArgument.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Male")), NamedArgument.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Adult"))));

		private static readonly TipSignal KeepFemaleAdultCountTip = new TipSignal(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.Slaughterhouse.Setting.KeepCountTip", NamedArgument.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Female")), NamedArgument.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Adult"))));

		private static readonly TipSignal KeepNoneAdultCountTip = new TipSignal(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.Slaughterhouse.Setting.KeepCountTip", NamedArgument.op_Implicit(Translator.Translate("PRF.AutoMachineTool.None")), NamedArgument.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Adult"))));

		private static readonly TipSignal KeepNoneChildCountTip = new TipSignal(TranslatorFormattedStringExtensions.Translate("PRF.AutoMachineTool.Slaughterhouse.Setting.KeepCountTip", NamedArgument.op_Implicit(Translator.Translate("PRF.AutoMachineTool.None")), NamedArgument.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Young"))));

		private Vector2 scrollPosition;

		private static Vector2 sscrollPosition;

		private readonly string description = TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.Slaughterhouse.Setting.Description"));

		private Vector2 winSize = new Vector2(560f, 400f);

		private ISlaughterhouse slaughterhouse => ((ITab)this).SelThing as ISlaughterhouse;

		public Dictionary<ThingDef, SlaughterSettings> Settings => slaughterhouse.Settings;

		private static List<ThingDef> Defs => (from p in Find.CurrentMap.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)
			where p.RaceProps.Animal && p.RaceProps.IsFlesh && ((Thing)p).SpawnedOrAnyParentSpawned
			select ((Thing)p).def).Distinct().ToList();

		public ITab_Slaughterhouse()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).labelKey = "PRFSlaughterhouseTab";
		}

		private Func<float, Rect> CutLeftFunc(Rect rect)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			float curX = 0f;
			return delegate(float pct)
			{
				//IL_004b: Unknown result type (might be due to invalid IL or missing references)
				//IL_004c: Unknown result type (might be due to invalid IL or missing references)
				//IL_004f: Unknown result type (might be due to invalid IL or missing references)
				Rect result = default(Rect);
				((Rect)(ref result))..ctor(curX, ((Rect)(ref rect)).y, ((Rect)(ref rect)).width * pct, ((Rect)(ref rect)).height);
				curX += ((Rect)(ref rect)).width * pct;
				return result;
			};
		}

		protected override void UpdateSize()
		{
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			winSize.y = 124 + Defs.Count() * 36;
			winSize.y = Mathf.Min(winSize.y, 400f);
			((InspectTabBase)this).size = winSize;
			((InspectTabBase)this).UpdateSize();
		}

		protected override void FillTab()
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Expected O, but got Unknown
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0123: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			//IL_015b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0160: Unknown result type (might be due to invalid IL or missing references)
			//IL_0166: Unknown result type (might be due to invalid IL or missing references)
			//IL_0170: Unknown result type (might be due to invalid IL or missing references)
			//IL_0181: Unknown result type (might be due to invalid IL or missing references)
			//IL_0186: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0201: Unknown result type (might be due to invalid IL or missing references)
			//IL_0206: Unknown result type (might be due to invalid IL or missing references)
			//IL_020c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0216: Unknown result type (might be due to invalid IL or missing references)
			//IL_0227: Unknown result type (might be due to invalid IL or missing references)
			//IL_022c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0254: Unknown result type (might be due to invalid IL or missing references)
			//IL_0259: Unknown result type (might be due to invalid IL or missing references)
			//IL_025f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0269: Unknown result type (might be due to invalid IL or missing references)
			//IL_027a: Unknown result type (might be due to invalid IL or missing references)
			//IL_027f: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_031f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0324: Unknown result type (might be due to invalid IL or missing references)
			//IL_032a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0334: Unknown result type (might be due to invalid IL or missing references)
			//IL_0345: Unknown result type (might be due to invalid IL or missing references)
			//IL_034f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0359: Unknown result type (might be due to invalid IL or missing references)
			//IL_036a: Unknown result type (might be due to invalid IL or missing references)
			//IL_036f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0397: Unknown result type (might be due to invalid IL or missing references)
			//IL_039c: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_03ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_03c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_03d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_040f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0414: Unknown result type (might be due to invalid IL or missing references)
			//IL_041a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0424: Unknown result type (might be due to invalid IL or missing references)
			//IL_0435: Unknown result type (might be due to invalid IL or missing references)
			//IL_043f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0449: Unknown result type (might be due to invalid IL or missing references)
			//IL_045a: Unknown result type (might be due to invalid IL or missing references)
			//IL_045f: Unknown result type (might be due to invalid IL or missing references)
			//IL_047e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0483: Unknown result type (might be due to invalid IL or missing references)
			//IL_04b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c1: Expected O, but got Unknown
			//IL_04c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_04d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_050a: Unknown result type (might be due to invalid IL or missing references)
			//IL_050f: Unknown result type (might be due to invalid IL or missing references)
			Listing_Standard val = new Listing_Standard();
			Rect val2 = GenUI.ContractedBy(new Rect(0f, 0f, winSize.x, winSize.y), 10f);
			((Listing)val).Begin(val2);
			scrollPosition = sscrollPosition;
			Rect rect = ((Listing)val).GetRect(40f, 1f);
			Widgets.Label(rect, description);
			float num = winSize.y + (float)(int)((Listing)val).CurHeight + 20f;
			num = Mathf.Min(num, ((Rect)(ref val2)).height);
			Rect val3 = GenUI.ContractedBy(new Rect(0f, ((Listing)val).CurHeight, winSize.x, num), 10f);
			Rect rect2 = ((Listing)val).GetRect(24f, 1f);
			((Rect)(ref rect2)).width = ((Rect)(ref rect2)).width - 30f;
			Func<float, Rect> func = CutLeftFunc(rect2);
			int colIndex = 0;
			Rect col = func(ColumnWidth[colIndex++]);
			Widgets.Label(col, Translator.Translate("PRF.AutoMachineTool.RaceName"));
			col = func(ColumnWidth[colIndex++]);
			GUI.DrawTexture(GenUI.LeftPartPixels(col, 24f), (Texture)(object)RS.SlaughterIcon);
			TooltipHandler.TipRegion(col, SlaughterTip);
			col = func(ColumnWidth[colIndex++]);
			GUI.DrawTexture(GenUI.LeftPartPixels(col, 24f), (Texture)(object)RS.BondIcon);
			TooltipHandler.TipRegion(col, HasBondsTip);
			col = func(ColumnWidth[colIndex++]);
			GUI.DrawTexture(GenUI.LeftPartPixels(col, 24f), (Texture)(object)RS.PregnantIcon);
			TooltipHandler.TipRegion(col, PregnancyTip);
			col = func(ColumnWidth[colIndex++]);
			GUI.DrawTexture(GenUI.LeftPartPixels(col, 24f), (Texture)(object)RS.TrainedIcon);
			TooltipHandler.TipRegion(col, TrainedTip);
			col = func(ColumnWidth[colIndex++]);
			GUI.DrawTexture(GenUI.LeftPartPixels(col, 24f), (Texture)(object)RS.MaleIcon);
			GUI.DrawTexture(GenUI.RightPartPixels(GenUI.LeftPartPixels(col, 48f), 24f), (Texture)(object)RS.YoungIcon);
			TooltipHandler.TipRegion(col, KeepMaleChildCountTip);
			col = func(ColumnWidth[colIndex++]);
			GUI.DrawTexture(GenUI.LeftPartPixels(col, 24f), (Texture)(object)RS.FemaleIcon);
			GUI.DrawTexture(GenUI.RightPartPixels(GenUI.LeftPartPixels(col, 48f), 24f), (Texture)(object)RS.YoungIcon);
			TooltipHandler.TipRegion(col, KeepFemaleChildCountTip);
			col = func(ColumnWidth[colIndex++]);
			GUI.DrawTexture(GenUI.LeftPartPixels(col, 24f), (Texture)(object)RS.MaleIcon);
			GUI.DrawTexture(GenUI.RightPartPixels(GenUI.LeftPartPixels(col, 48f), 24f), (Texture)(object)RS.AdultIcon);
			TooltipHandler.TipRegion(col, KeepMaleAdultCountTip);
			col = func(ColumnWidth[colIndex++]);
			GUI.DrawTexture(GenUI.LeftPartPixels(col, 24f), (Texture)(object)RS.FemaleIcon);
			GUI.DrawTexture(GenUI.RightPartPixels(GenUI.LeftPartPixels(col, 48f), 24f), (Texture)(object)RS.AdultIcon);
			TooltipHandler.TipRegion(col, KeepFemaleAdultCountTip);
			Rect rect3 = ((Listing)val).GetRect(((Rect)(ref val3)).height - ((Listing)val).CurHeight, 1f);
			Rect val4 = default(Rect);
			((Rect)(ref val4))..ctor(((Rect)(ref rect3)).x, 0f, ((Rect)(ref rect3)).width - 30f, (float)Defs.Count() * 36f);
			Listing_Standard innerlist = new Listing_Standard();
			Widgets.BeginScrollView(rect3, ref scrollPosition, val4, true);
			((Listing)innerlist).Begin(val4);
			Defs.ForEach(delegate(ThingDef d)
			{
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				//IL_006e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0098: Unknown result type (might be due to invalid IL or missing references)
				//IL_009d: Unknown result type (might be due to invalid IL or missing references)
				//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
				//IL_00da: Unknown result type (might be due to invalid IL or missing references)
				//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
				//IL_0100: Unknown result type (might be due to invalid IL or missing references)
				//IL_0105: Unknown result type (might be due to invalid IL or missing references)
				//IL_012c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0131: Unknown result type (might be due to invalid IL or missing references)
				//IL_013c: Unknown result type (might be due to invalid IL or missing references)
				//IL_015f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0164: Unknown result type (might be due to invalid IL or missing references)
				//IL_018b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0190: Unknown result type (might be due to invalid IL or missing references)
				//IL_019b: Unknown result type (might be due to invalid IL or missing references)
				//IL_01be: Unknown result type (might be due to invalid IL or missing references)
				//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
				//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
				//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
				//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
				//IL_021d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0222: Unknown result type (might be due to invalid IL or missing references)
				//IL_03f8: Unknown result type (might be due to invalid IL or missing references)
				//IL_03fd: Unknown result type (might be due to invalid IL or missing references)
				//IL_0410: Unknown result type (might be due to invalid IL or missing references)
				//IL_042e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0433: Unknown result type (might be due to invalid IL or missing references)
				//IL_0460: Unknown result type (might be due to invalid IL or missing references)
				//IL_0465: Unknown result type (might be due to invalid IL or missing references)
				//IL_0478: Unknown result type (might be due to invalid IL or missing references)
				//IL_0496: Unknown result type (might be due to invalid IL or missing references)
				//IL_049b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0263: Unknown result type (might be due to invalid IL or missing references)
				//IL_0268: Unknown result type (might be due to invalid IL or missing references)
				//IL_027b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0299: Unknown result type (might be due to invalid IL or missing references)
				//IL_029e: Unknown result type (might be due to invalid IL or missing references)
				//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
				//IL_02ca: Unknown result type (might be due to invalid IL or missing references)
				//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
				//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
				//IL_0300: Unknown result type (might be due to invalid IL or missing references)
				//IL_0327: Unknown result type (might be due to invalid IL or missing references)
				//IL_032c: Unknown result type (might be due to invalid IL or missing references)
				//IL_033f: Unknown result type (might be due to invalid IL or missing references)
				//IL_035d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0362: Unknown result type (might be due to invalid IL or missing references)
				//IL_0389: Unknown result type (might be due to invalid IL or missing references)
				//IL_038e: Unknown result type (might be due to invalid IL or missing references)
				//IL_03a1: Unknown result type (might be due to invalid IL or missing references)
				//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
				//IL_03c4: Unknown result type (might be due to invalid IL or missing references)
				((Listing)innerlist).GapLine(12f);
				Rect rect4 = ((Listing)innerlist).GetRect(24f, 1f);
				SlaughterSettings value = null;
				Settings.TryGetValue(d, out value);
				if (value == null)
				{
					value = new SlaughterSettings
					{
						def = d
					};
					Settings[d] = value;
				}
				Func<float, Rect> func2 = CutLeftFunc(rect4);
				colIndex = 0;
				col = func2(ColumnWidth[colIndex++]);
				Widgets.Label(col, ((Def)value.def).label);
				col = func2(ColumnWidth[colIndex++]);
				Widgets.Checkbox(((Rect)(ref col)).position, ref value.doSlaughter, 24f, false, false, (Texture2D)null, (Texture2D)null);
				TooltipHandler.TipRegion(col, SlaughterTip);
				col = func2(ColumnWidth[colIndex++]);
				Widgets.Checkbox(((Rect)(ref col)).position, ref value.hasBonds, 24f, !value.doSlaughter, false, (Texture2D)null, (Texture2D)null);
				TooltipHandler.TipRegion(col, HasBondsTip);
				col = func2(ColumnWidth[colIndex++]);
				Widgets.Checkbox(((Rect)(ref col)).position, ref value.pregnancy, 24f, !value.doSlaughter, false, (Texture2D)null, (Texture2D)null);
				TooltipHandler.TipRegion(col, PregnancyTip);
				col = func2(ColumnWidth[colIndex++]);
				Widgets.Checkbox(((Rect)(ref col)).position, ref value.trained, 24f, !value.doSlaughter, false, (Texture2D)null, (Texture2D)null);
				TooltipHandler.TipRegion(col, TrainedTip);
				if (value.def.race.hasGenders)
				{
					col = func2(ColumnWidth[colIndex++]);
					string text = value.keepMaleYoungCount.ToString();
					Widgets.TextFieldNumeric<int>(col, ref value.keepMaleYoungCount, ref text, 0f, 1000f);
					TooltipHandler.TipRegion(col, KeepMaleChildCountTip);
					col = func2(ColumnWidth[colIndex++]);
					string text2 = value.keepFemaleYoungCount.ToString();
					Widgets.TextFieldNumeric<int>(col, ref value.keepFemaleYoungCount, ref text2, 0f, 1000f);
					TooltipHandler.TipRegion(col, KeepFemaleChildCountTip);
					col = func2(ColumnWidth[colIndex++]);
					string text3 = value.keepMaleAdultCount.ToString();
					Widgets.TextFieldNumeric<int>(col, ref value.keepMaleAdultCount, ref text3, 0f, 1000f);
					TooltipHandler.TipRegion(col, KeepMaleAdultCountTip);
					col = func2(ColumnWidth[colIndex++]);
					string text4 = value.keepFemaleAdultCount.ToString();
					Widgets.TextFieldNumeric<int>(col, ref value.keepFemaleAdultCount, ref text4, 0f, 1000f);
					TooltipHandler.TipRegion(col, KeepFemaleAdultCountTip);
				}
				else
				{
					col = func2(ColumnWidth[colIndex++] * 2f);
					string text5 = value.keepNoneYoungCount.ToString();
					Widgets.TextFieldNumeric<int>(col, ref value.keepNoneYoungCount, ref text5, 0f, 1000f);
					TooltipHandler.TipRegion(col, KeepNoneChildCountTip);
					col = func2(ColumnWidth[colIndex++] * 2f);
					string text6 = value.keepNoneAdultCount.ToString();
					Widgets.TextFieldNumeric<int>(col, ref value.keepNoneAdultCount, ref text6, 0f, 1000f);
					TooltipHandler.TipRegion(col, KeepNoneAdultCountTip);
					colIndex += 2;
				}
			});
			Widgets.EndScrollView();
			((Listing)innerlist).End();
			sscrollPosition = scrollPosition;
			((Listing)val).End();
		}
	}
	public class JobDriver_StaticJob : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			yield break;
		}
	}
	public class MapTickManager(Map map) : MapComponent(map)
	{
		private readonly Dictionary<int, HashSet<Action>> tickActionsDict = new Dictionary<int, HashSet<Action>>();

		private readonly HashSet<Func<bool>> eachTickActions = new HashSet<Func<bool>>();

		public override void MapComponentTick()
		{
			((MapComponent)this).MapComponentTick();
			HashSet<Func<bool>> sequence = eachTickActions.ToList().Where(Exec).ToHashSet();
			sequence.ForEach(delegate(Func<bool> r)
			{
				eachTickActions.Remove(r);
			});
			tickActionsDict.GetOption(Find.TickManager.TicksGame).ForEach(delegate(HashSet<Action> s)
			{
				s.ToList().ForEach(Exec);
			});
			tickActionsDict.Remove(Find.TickManager.TicksGame);
		}

		public void HandleTimeSkip(int newCurrentTick)
		{
			int ticksGame = Find.TickManager.TicksGame;
			int num = newCurrentTick - ticksGame;
			Dictionary<int, HashSet<Action>> dictionary = tickActionsDict.ToDictionary((KeyValuePair<int, HashSet<Action>> entry) => entry.Key, (KeyValuePair<int, HashSet<Action>> entry) => entry.Value);
			tickActionsDict.Clear();
			foreach (KeyValuePair<int, HashSet<Action>> item in dictionary)
			{
				tickActionsDict[item.Key + num] = item.Value;
			}
		}

		private static void Exec(Action act)
		{
			try
			{
				act();
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
			}
		}

		private static T Exec<T>(Func<T> func)
		{
			try
			{
				return func();
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
				return default(T);
			}
		}

		public override void MapComponentUpdate()
		{
			((MapComponent)this).MapComponentUpdate();
			MainButtonDef openTab = Find.MainTabsRoot.OpenTab;
			MainTabWindow obj = ((openTab != null) ? openTab.TabWindow : null);
			if (((Def)((MainTabWindow_Architect)(((obj is MainTabWindow_Architect) ? obj : null)?)).selectedDesPanel?.def).defName == "Industrial")
			{
				OverlayDrawHandler_UGConveyor.DrawOverlayThisFrame();
			}
			if (Find.Selector.FirstSelectedObject is IBeltConveyorLinkable)
			{
				OverlayDrawHandler_UGConveyor.DrawOverlayThisFrame();
			}
		}

		public void AfterAction(int ticks, Action act)
		{
			if (ticks < 1)
			{
				ticks = 1;
			}
			int key = Find.TickManager.TicksGame + ticks;
			if (!tickActionsDict.TryGetValue(key, out var value))
			{
				value = new HashSet<Action>();
				tickActionsDict[key] = value;
			}
			value.Add(act);
		}

		public void NextAction(Action act)
		{
			AfterAction(1, act);
		}

		public void EachTickAction(Func<bool> act)
		{
			eachTickActions.Add(act);
		}

		public void RemoveAfterAction(Action act)
		{
			tickActionsDict.ForEach(delegate(KeyValuePair<int, HashSet<Action>> kv)
			{
				kv.Value.Remove(act);
			});
		}

		public void RemoveEachTickAction(Func<bool> act)
		{
			eachTickActions.Remove(act);
		}

		public bool IsExecutingThisTick(Action act)
		{
			return (from l in tickActionsDict.GetOption(Find.TickManager.TicksGame)
				select l.Contains(act)).GetOrDefault(defaultValue: false);
		}
	}
	public class ThingLister
	{
		private readonly Map map;

		private readonly Dictionary<Type, List<ThingDef>> typeDic = new Dictionary<Type, List<ThingDef>>();

		public ThingLister(Map map)
		{
			this.map = map;
		}

		public IEnumerable<T> ForAssignableFrom<T>() where T : Thing
		{
			if (!typeDic.TryGetValue(typeof(T), out var value))
			{
				value = DefDatabase<ThingDef>.AllDefs.Where((ThingDef d) => typeof(T).IsAssignableFrom(d.thingClass)).ToList();
				typeDic[typeof(T)] = value;
			}
			return value.SelectMany((ThingDef d) => map.listerThings.ThingsOfDef(d)).SelectMany((Thing t) => Ops.Option((T)(object)((t is T) ? t : null)));
		}
	}
	public class ModExtension_Conveyor : DefModExtension
	{
		public bool underground = false;

		public bool toUnderground = false;

		public float carriedItemScale = 0.75f;

		public Vector2 arrowDrawOffset;

		public Vector2 arrowEastDrawOffset;

		public Vector2 arrowWestDrawOffset;

		public Vector2 arrowNorthDrawOffset;

		public Vector2 arrowSouthDrawOffset;

		public string texPath2 = null;

		public List<ThingDef> specialLinkDefs;
	}
	public class ModExtension_EffectWorking : DefModExtension
	{
		public EffecterDef effectWorking;
	}
	public class ModExtension_Puller : DefModExtension
	{
		public bool outputSides = false;

		public IntVec3 GetOutputCell(IntVec3 pos, Rot4 rot, bool isRight = false)
		{
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			if (outputSides)
			{
				RotationDirection dir = (RotationDirection)1;
				if (!isRight)
				{
					dir = (RotationDirection)3;
				}
				Rot4 val = rot.RotateAsNew(dir);
				return pos + ((Rot4)(ref val)).FacingCell;
			}
			return pos + ((Rot4)(ref rot)).FacingCell;
		}
	}
	public class ModExtension_Tier : DefModExtension
	{
		public int tier = 1;

		public int skillLevel = 0;
	}
	public class MoteProgressBar2 : MoteProgressBar
	{
		public Func<float> progressGetter;

		public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Invalid comparison between Unknown and I4
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			if ((int)phase == 2)
			{
				if (progressGetter != null)
				{
					base.progress = Mathf.Clamp01(progressGetter());
				}
				((Thing)this).DynamicDrawPhaseAt(phase, drawLoc, flip);
			}
		}
	}
	public static class Ops
	{
		public static Option<T> Option<T>(T value)
		{
			return new Option<T>(value);
		}

		public static Nothing<T> Nothing<T>()
		{
			return new Nothing<T>();
		}

		private static Just<T> Just<T>(T value)
		{
			return new Just<T>(value);
		}

		public static Option<T> FirstOption<T>(this IEnumerable<T> e)
		{
			IEnumerator<T> enumerator = e.GetEnumerator();
			if (enumerator.MoveNext())
			{
				return new Just<T>(enumerator.Current);
			}
			return new Nothing<T>();
		}

		public static IEnumerable<TResult> SelectMany<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, Option<TResult>> selector)
		{
			return source.SelectMany((TSource e) => selector(e).ToList());
		}

		public static List<T> Append<T>(this List<T> lhs, T rhs)
		{
			lhs.Add(rhs);
			return lhs;
		}

		public static List<T> Head<T>(this List<T> lhs, T rhs)
		{
			lhs.Insert(0, rhs);
			return lhs;
		}

		public static void ForEach<T>(this IEnumerable<T> sequence, Action<T> action)
		{
			foreach (T item in sequence)
			{
				action(item);
			}
		}

		public static Option<V> GetOption<K, V>(this Dictionary<K, V> dict, K key)
		{
			if (dict.TryGetValue(key, out var value))
			{
				return Just(value);
			}
			return Nothing<V>();
		}

		public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer = null)
		{
			return new HashSet<T>(source, comparer);
		}

		public static List<List<T>> Grouped<T>(this List<T> source, int size)
		{
			List<List<T>> list = new List<List<T>>();
			int num;
			for (int i = 0; i < source.Count; i += num)
			{
				num = Mathf.Min(size, source.Count - i);
				list.Add(source.GetRange(i, num));
			}
			return list;
		}

		public static IEnumerable<T> GetEnumValues<T>() where T : struct
		{
			return Enum.GetValues(typeof(T)).Cast<T>();
		}

		public static TO CopyTo<FROM, TO>(this FROM bill, TO copy) where FROM : Bill_Production where TO : Bill_Production
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_012d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0132: Unknown result type (might be due to invalid IL or missing references)
			((Bill)(object)copy).allowedSkillRange = ((Bill)(object)bill).allowedSkillRange;
			((Bill)(object)copy).billStack = ((Bill)(object)bill).billStack;
			((Bill)(object)copy).deleted = ((Bill)(object)bill).deleted;
			((Bill_Production)copy).hpRange = ((Bill_Production)bill).hpRange;
			((Bill_Production)copy).includeEquipped = ((Bill_Production)bill).includeEquipped;
			((Bill_Production)copy).SetIncludeGroup(((Bill_Production)bill).GetIncludeSlotGroup());
			((Bill_Production)copy).includeTainted = ((Bill_Production)bill).includeTainted;
			((Bill)(object)copy).ingredientFilter = ((Bill)(object)bill).ingredientFilter;
			((Bill)(object)copy).ingredientSearchRadius = ((Bill)(object)bill).ingredientSearchRadius;
			((Bill_Production)copy).limitToAllowedStuff = ((Bill_Production)bill).limitToAllowedStuff;
			((Bill_Production)copy).paused = ((Bill_Production)bill).paused;
			((Bill_Production)copy).pauseWhenSatisfied = ((Bill_Production)bill).pauseWhenSatisfied;
			((Bill)(object)copy).SetPawnRestriction(((Bill)(object)bill).PawnRestriction);
			((Bill_Production)copy).qualityRange = ((Bill_Production)bill).qualityRange;
			((Bill)(object)copy).recipe = ((Bill)(object)bill).recipe;
			((Bill_Production)copy).repeatCount = ((Bill_Production)bill).repeatCount;
			((Bill_Production)copy).repeatMode = ((Bill_Production)bill).repeatMode;
			((Bill)(object)copy).SetStoreMode(((Bill)(object)bill).GetStoreMode(), (ISlotGroup)null);
			((Bill)(object)copy).suspended = ((Bill)(object)bill).suspended;
			((Bill_Production)copy).targetCount = ((Bill_Production)bill).targetCount;
			((Bill_Production)copy).unpauseWhenYouHave = ((Bill_Production)bill).unpauseWhenYouHave;
			((Bill)(object)copy).precept = ((Bill)(object)bill).precept;
			return copy;
		}

		public static IntVec3 FacingCell(IntVec3 center, IntVec2 size, Rot4 rot)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0109: Unknown result type (might be due to invalid IL or missing references)
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_011a: Unknown result type (might be due to invalid IL or missing references)
			List<IntVec3> source = GenAdj.CellsOccupiedBy(center, rot, size).ToList();
			int num = source.Min((IntVec3 c) => c.x);
			int num2 = source.Max((IntVec3 c) => c.x);
			int num3 = source.Min((IntVec3 c) => c.z);
			int num4 = source.Max((IntVec3 c) => c.z);
			int num5 = ((((Rot4)(ref rot)).FacingCell.x == 0) ? center.x : ((((Rot4)(ref rot)).FacingCell.x > 0) ? (num2 + 1) : (num - 1)));
			int num6 = ((((Rot4)(ref rot)).FacingCell.z == 0) ? center.z : ((((Rot4)(ref rot)).FacingCell.z > 0) ? (num4 + 1) : (num3 - 1)));
			return new IntVec3(num5, center.y, num6);
		}

		public static IEnumerable<IntVec3> FacingRect(IntVec3 pos, Rot4 dir, int range)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			return FacingRect(pos, new IntVec2(1, 1), dir, range);
		}

		public static IEnumerable<IntVec3> FacingRect(Thing thing, Rot4 dir, int range)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			return FacingRect(thing.Position, thing.RotatedSize, dir, range);
		}

		public static IEnumerable<IntVec3> FacingRect(IntVec3 center, IntVec2 size, Rot4 dir, int range)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			Util.CounterAdjustForRotation(ref center, ref size, dir);
			IntVec3 facing = FacingCell(center, size, dir);
			int xoffset = ((Rot4)(ref dir)).FacingCell.x * range + facing.x;
			int zoffset = ((Rot4)(ref dir)).FacingCell.z * range + facing.z;
			for (int x = -range; x <= range; x++)
			{
				for (int z = -range; z <= range; z++)
				{
					yield return new IntVec3(x + xoffset, center.y, z + zoffset);
				}
			}
		}

		public static Rot4 RotateAsNew(this Rot4 rot, RotationDirection dir)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			Rot4 result = rot;
			((Rot4)(ref result)).Rotate(dir);
			return result;
		}

		public static bool IsAdult(this Pawn p)
		{
			return p.ageTracker.CurLifeStageIndex >= 2;
		}

		public static Color A(this Color color, float a)
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
	public class Option<T>
	{
		private readonly T value;

		public T Value => value;

		public bool HasValue { get; } = false;

		public Option(T val)
		{
			if (val != null)
			{
				value = val;
				HasValue = true;
			}
		}

		public Option()
		{
		}

		public Option<TO> Select<TO>(Func<T, TO> func)
		{
			return HasValue ? new Option<TO>(func(value)) : new Nothing<TO>();
		}

		public void ForEach(Action<T> act)
		{
			if (HasValue)
			{
				act(value);
			}
		}

		public List<T> ToList()
		{
			return HasValue ? new List<T>(new T[1] { value }) : new List<T>();
		}

		public T GetOrDefault(T defaultValue)
		{
			return HasValue ? value : defaultValue;
		}

		public Func<Func<T, R>, R> Fold<R>(R defaultValue)
		{
			return HasValue ? ((Func<Func<T, R>, R>)((Func<T, R> f) => f(value))) : ((Func<Func<T, R>, R>)((Func<T, R> _) => defaultValue));
		}

		public Func<Func<T, R>, R> Fold<R>(Func<R> craetor)
		{
			return HasValue ? ((Func<Func<T, R>, R>)((Func<T, R> f) => f(value))) : ((Func<Func<T, R>, R>)((Func<T, R> _) => craetor()));
		}

		public override bool Equals(object obj)
		{
			return obj != null && obj is Option<T> && HasValue == ((Option<T>)obj).HasValue && (!HasValue || value.Equals(((Option<T>)obj).value));
		}

		public override int GetHashCode()
		{
			return HasValue ? value.GetHashCode() : HasValue.GetHashCode();
		}

		public override string ToString()
		{
			return Fold("Option<Nothing>")((T v) => "Option<" + v.ToString() + ">");
		}
	}
	public class Nothing<T> : Option<T>
	{
	}
	public class Just<T> : Option<T>
	{
		public Just(T value)
			: base(value)
		{
		}
	}
	public class PlaceWorker_AutoMachineTool : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			AcceptanceReport result = ((PlaceWorker)this).AllowsPlacing(checkingDef, loc, rot, map, thingToIgnore, thing);
			if (((AcceptanceReport)(ref result)).Accepted && !new Building_AutoMachineTool().GetTarget(loc, rot, map))
			{
				return new AcceptanceReport(TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.PlaceNotAllowed")));
			}
			return result;
		}
	}
	public class PlaceWorker_Conveyor : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0305: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_01af: Unknown result type (might be due to invalid IL or missing references)
			//IL_019c: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0302: Unknown result type (might be due to invalid IL or missing references)
			//IL_013e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0143: Unknown result type (might be due to invalid IL or missing references)
			//IL_014d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0152: Unknown result type (might be due to invalid IL or missing references)
			//IL_0233: Unknown result type (might be due to invalid IL or missing references)
			//IL_0238: Unknown result type (might be due to invalid IL or missing references)
			//IL_0242: Unknown result type (might be due to invalid IL or missing references)
			//IL_0247: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_02db: Unknown result type (might be due to invalid IL or missing references)
			ThingDef val = (ThingDef)(object)((checkingDef is ThingDef) ? checkingDef : null);
			if (val == null)
			{
				return AcceptanceReport.WasAccepted;
			}
			ModExtension_Conveyor modExtension = ((Def)checkingDef).GetModExtension<ModExtension_Conveyor>();
			if (typeof(Building_BeltConveyorUGConnector).IsAssignableFrom(val.thingClass) || (modExtension != null && modExtension.underground))
			{
				foreach (Thing item in map.thingGrid.ThingsListAt(loc))
				{
					if (item == thingToIgnore || item == thing)
					{
						continue;
					}
					if (!(item is Building_BeltConveyorUGConnector))
					{
						Building_BeltConveyor obj = item as Building_BeltConveyor;
						if (obj == null || !obj.IsUnderground)
						{
							Blueprint val2 = (Blueprint)(object)((item is Blueprint) ? item : null);
							if (val2 == null)
							{
								continue;
							}
							BuildableDef entityDefToBuild = ((Thing)val2).def.entityDefToBuild;
							ThingDef val3 = (ThingDef)(object)((entityDefToBuild is ThingDef) ? entityDefToBuild : null);
							if (val3 == null)
							{
								continue;
							}
							if (!typeof(Building_BeltConveyorUGConnector).IsAssignableFrom(val3.thingClass))
							{
								List<DefModExtension> modExtensions = ((Def)val3).modExtensions;
								if (modExtensions == null || !GenCollection.Any<DefModExtension>(modExtensions, (Predicate<DefModExtension>)((DefModExtension dme) => dme is ModExtension_Conveyor modExtension_Conveyor && modExtension_Conveyor.underground)))
								{
									continue;
								}
							}
						}
					}
					return new AcceptanceReport(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFBlockedBy", NamedArgument.op_Implicit(((Entity)item).Label))));
				}
			}
			List<Type> placeWorkers = checkingDef.placeWorkers;
			if (placeWorkers == null || !placeWorkers.Contains(typeof(PlaceWorker_WallEmbedded)))
			{
				return AcceptanceReport.WasAccepted;
			}
			foreach (Thing item2 in map.thingGrid.ThingsListAt(loc))
			{
				if (item2 == thingToIgnore || item2 == thing)
				{
					continue;
				}
				if (item2 is IBeltConveyorLinkable beltConveyorLinkable && beltConveyorLinkable is Thing)
				{
					List<Type> placeWorkers2 = ((BuildableDef)item2.def).placeWorkers;
					if (placeWorkers2 != null && placeWorkers2.Contains(typeof(PlaceWorker_WallEmbedded)))
					{
						return new AcceptanceReport(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFBlockedBy", NamedArgument.op_Implicit(((Entity)item2).Label))));
					}
				}
				Blueprint val4 = (Blueprint)(object)((item2 is Blueprint) ? item2 : null);
				if (val4 == null)
				{
					continue;
				}
				BuildableDef entityDefToBuild2 = ((Thing)val4).def.entityDefToBuild;
				ThingDef val5 = (ThingDef)(object)((entityDefToBuild2 is ThingDef) ? entityDefToBuild2 : null);
				if (val5 != null && typeof(Building_BeltConveyor).IsAssignableFrom(val5.thingClass))
				{
					List<Type> placeWorkers3 = ((BuildableDef)val5).placeWorkers;
					if (placeWorkers3 != null && placeWorkers3.Contains(typeof(PlaceWorker_WallEmbedded)))
					{
						return new AcceptanceReport(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRFBlockedBy", NamedArgument.op_Implicit(((Entity)item2).Label))));
					}
				}
			}
			return AcceptanceReport.WasAccepted;
		}
	}
	internal class PlaceWorker_PullerCellsHilight : PlaceWorker
	{
		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			Rot4 opposite = ((Rot4)(ref rot)).Opposite;
			IntVec3 item = center + ((Rot4)(ref opposite)).FacingCell;
			bool isRight = false;
			if (thing != null && !thing.def.IsBlueprint)
			{
				isRight = (thing as Building_ItemPuller)?.GetRight ?? false;
			}
			IntVec3 item2 = ((!def.IsBlueprint && !def.IsFrame) ? ((Def)def).GetModExtension<ModExtension_Puller>().GetOutputCell(center, rot, isRight) : ((Def)def.entityDefToBuild).GetModExtension<ModExtension_Puller>().GetOutputCell(center, rot, isRight));
			GenDraw.DrawFieldEdges(new List<IntVec3>(1) { item }, CommonColors.InputCell, (float?)null, (HashSet<IntVec3>)null, 2900);
			GenDraw.DrawFieldEdges(new List<IntVec3>(1) { item2 }, CommonColors.OutputCell, (float?)null, (HashSet<IntVec3>)null, 2900);
		}
	}
	internal class PlaceWorker_SALTargetCellHilight : PlaceWorker
	{
		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			GenDraw.DrawFieldEdges(new List<IntVec3>(1) { center + ((Rot4)(ref rot)).FacingCell }, CommonColors.WorkbenchAlpha, (float?)null, (HashSet<IntVec3>)null, 2900);
		}
	}
	public class PlaceWorker_WallEmbedded : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00de: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			if ((from t in GridsUtility.GetThingList(loc, map)
				where (int)t.def.category == 3
				where t.def.building != null
				where !t.def.building.isNaturalRock
				where (int)((BuildableDef)t.def).passability == 2
				select t).Any((Thing t) => (t.def.graphicData.linkFlags & 4) > 0 || (t.def.graphicData.linkFlags & 2) > 0))
			{
				return AcceptanceReport.WasAccepted;
			}
			return new AcceptanceReport(TaggedString.op_Implicit(Translator.Translate("PRF.AutoMachineTool.MustInWall")));
		}
	}
	public abstract class SAL_TargetBench : IExposable
	{
		protected Building_AutoMachineTool mySAL;

		protected IntVec3 Position;

		protected Map map;

		protected Rot4 Rotation;

		public SAL_TargetBench()
		{
		}

		public SAL_TargetBench(Building_AutoMachineTool mySAL, IntVec3 position, Map map, Rot4 rotation)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			this.mySAL = mySAL;
			Position = position;
			this.map = map;
			Rotation = rotation;
		}

		public abstract bool Ready();

		public abstract void Reserve();

		public abstract void Free();

		public abstract void WorkDone(out List<Thing> products);

		public abstract bool TryStartWork(out float workAmount);

		public abstract TargetInfo TargetInfo();

		public virtual void Reset(WorkingState workingState)
		{
		}

		public virtual void CreateWorkingEffect(MapTickManager mapTickManager)
		{
		}

		public virtual void CleanupWorkingEffect(MapTickManager mapTickManager)
		{
		}

		protected void GeneralReserve(Building tb)
		{
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Expected O, but got Unknown
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Expected O, but got Unknown
			if (PRFGameComponent.PRF_StaticPawn == null)
			{
				PRFGameComponent.GenStaticPawn();
			}
			if (PRFGameComponent.PRF_StaticJob == null)
			{
				PRFGameComponent.PRF_StaticJob = new Job(PRFDefOf.PRFStaticJob);
			}
			List<Reservation> list = (List<Reservation>)ReflectionUtility.SalReservations.GetValue(map.reservationManager);
			Reservation item = new Reservation(PRFGameComponent.PRF_StaticPawn, PRFGameComponent.PRF_StaticJob, 1, -1, LocalTargetInfo.op_Implicit((Thing)(object)tb), (ReservationLayerDef)null);
			if (!GenCollection.Any<Reservation>(list, (Predicate<Reservation>)((Reservation r) => r.Claimant == PRFGameComponent.PRF_StaticPawn && r.Job == PRFGameComponent.PRF_StaticJob && r.Target == LocalTargetInfo.op_Implicit((Thing)(object)tb))))
			{
				list.Add(item);
			}
			ReflectionUtility.SalReservations.SetValue(map.reservationManager, list);
		}

		protected void GeneralRelease(Building tb)
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Expected O, but got Unknown
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			if (PRFGameComponent.PRF_StaticPawn == null)
			{
				PRFGameComponent.GenStaticPawn();
			}
			if (PRFGameComponent.PRF_StaticJob == null)
			{
				PRFGameComponent.PRF_StaticJob = new Job(PRFDefOf.PRFStaticJob);
			}
			if (tb == null || ((Thing)tb).Destroyed)
			{
				Log.Warning("PRF GeneralRelease target null or destroyed");
				return;
			}
			if (map == null)
			{
				Log.Warning("PRF SAL_TargetBench:generalRelease NULL map");
				if (mySAL == null)
				{
					Log.Error("PRF SAL is NULL, Can't release Reservation. Skipping");
					return;
				}
				map = ((Thing)mySAL).Map;
				if (map == null)
				{
					Log.Error("PRF SAL map is also NULL, Can't release Reservation. Skipping");
					return;
				}
			}
			map.reservationManager.Release(LocalTargetInfo.op_Implicit((Thing)(object)tb), PRFGameComponent.PRF_StaticPawn, PRFGameComponent.PRF_StaticJob);
		}

		public virtual void ExposeData()
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			Scribe_References.Look<Map>(ref map, "map", false);
			Scribe_References.Look<Building_AutoMachineTool>(ref mySAL, "mySAL", false);
			Scribe_Values.Look<IntVec3>(ref Position, "Position", default(IntVec3), false);
			Scribe_Values.Look<Rot4>(ref Rotation, "Rotation", default(Rot4), false);
		}
	}
	public class SAL_TargetWorktable : SAL_TargetBench
	{
		public interface IBill_PawnForbidded
		{
			Bill Original { get; set; }
		}

		private struct ThingDefGroup
		{
			public ThingDef def;

			public List<ThingAmount> consumable;
		}

		public class Bill_ProductionPawnForbidded : Bill_Production, IBill_PawnForbidded
		{
			public Bill original;

			public Bill Original
			{
				get
				{
					return original;
				}
				set
				{
					original = value;
				}
			}

			public Bill_ProductionPawnForbidded()
			{
			}

			public Bill_ProductionPawnForbidded(RecipeDef recipe)
				: base(recipe, (Precept_ThingStyle)null)
			{
			}

			public override bool PawnAllowedToStartAnew(Pawn p)
			{
				return false;
			}

			public override void ExposeData()
			{
				//IL_001e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0024: Invalid comparison between Unknown and I4
				((Bill_Production)this).ExposeData();
				Scribe_Deep.Look<Bill>(ref original, "original", Array.Empty<object>());
				if ((int)Scribe.mode == 4)
				{
					original.billStack = ((Bill)this).billStack;
				}
			}

			public override Bill Clone()
			{
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0012: Expected O, but got Unknown
				Bill_Production copy = (Bill_Production)original.Clone();
				return (Bill)(object)this.CopyTo<Bill_ProductionPawnForbidded, Bill_Production>(copy);
			}

			public override void Notify_DoBillStarted(Pawn billDoer)
			{
				((Bill)this).Notify_DoBillStarted(billDoer);
				Ops.Option<Bill>(original).ForEach(delegate(Bill o)
				{
					o.Notify_DoBillStarted(billDoer);
				});
			}

			public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
			{
				((Bill_Production)this).Notify_IterationCompleted(billDoer, ingredients);
				Ops.Option<Bill>(original).ForEach(delegate(Bill o)
				{
					o.Notify_IterationCompleted(billDoer, ingredients);
				});
			}

			public override void Notify_PawnDidWork(Pawn p)
			{
				((Bill)this).Notify_PawnDidWork(p);
				Ops.Option<Bill>(original).ForEach(delegate(Bill o)
				{
					o.Notify_PawnDidWork(p);
				});
			}
		}

		public class Bill_ProductionWithUftPawnForbidded : Bill_ProductionWithUft, IBill_PawnForbidded
		{
			public Bill original;

			public Bill Original
			{
				get
				{
					return original;
				}
				set
				{
					original = value;
				}
			}

			public Bill_ProductionWithUftPawnForbidded()
			{
			}

			public Bill_ProductionWithUftPawnForbidded(RecipeDef recipe)
				: base(recipe, (Precept_ThingStyle)null)
			{
			}

			public override bool PawnAllowedToStartAnew(Pawn p)
			{
				return false;
			}

			public override void ExposeData()
			{
				//IL_001e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0024: Invalid comparison between Unknown and I4
				((Bill_ProductionWithUft)this).ExposeData();
				Scribe_Deep.Look<Bill>(ref original, "original", Array.Empty<object>());
				if ((int)Scribe.mode == 4)
				{
					original.billStack = ((Bill)this).billStack;
				}
			}

			public override Bill Clone()
			{
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0012: Expected O, but got Unknown
				Bill_ProductionWithUft copy = (Bill_ProductionWithUft)original.Clone();
				return (Bill)(object)this.CopyTo<Bill_ProductionWithUftPawnForbidded, Bill_ProductionWithUft>(copy);
			}

			public override void Notify_DoBillStarted(Pawn billDoer)
			{
				((Bill)this).Notify_DoBillStarted(billDoer);
				Ops.Option<Bill>(original).ForEach(delegate(Bill o)
				{
					o.Notify_DoBillStarted(billDoer);
				});
			}

			public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
			{
				((Bill_ProductionWithUft)this).Notify_IterationCompleted(billDoer, ingredients);
				Ops.Option<Bill>(original).ForEach(delegate(Bill o)
				{
					o.Notify_IterationCompleted(billDoer, ingredients);
				});
			}

			public override void Notify_PawnDidWork(Pawn p)
			{
				((Bill)this).Notify_PawnDidWork(p);
				Ops.Option<Bill>(original).ForEach(delegate(Bill o)
				{
					o.Notify_PawnDidWork(p);
				});
			}
		}

		private class ThingAmount
		{
			public Thing thing;

			public int count;

			public ThingAmount(Thing thing, int count)
			{
				this.thing = thing;
				this.count = count;
			}
		}

		public Building_WorkTable my_workTable;

		private Bill bill;

		private List<Thing> ingredients;

		private Thing dominant;

		private UnfinishedThing unfinished;

		[Unsaved(false)]
		private Effecter workingEffect = null;

		[Unsaved(false)]
		private Sustainer workingSound = null;

		public SAL_TargetWorktable()
		{
		}

		public SAL_TargetWorktable(Building_AutoMachineTool mySAL, IntVec3 position, Map map, Rot4 rotation, Building_WorkTable my_workTable)
			: base(mySAL, position, map, rotation)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			this.my_workTable = my_workTable;
		}

		public override bool Ready()
		{
			if (my_workTable == null)
			{
				return false;
			}
			return my_workTable.CurrentlyUsableForBills() && my_workTable.billStack.AnyShouldDoNow;
		}

		public override void Free()
		{
			AllowBills();
			GeneralRelease((Building)(object)my_workTable);
		}

		public override void Reserve()
		{
			GeneralReserve((Building)(object)my_workTable);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Deep.Look<UnfinishedThing>(ref unfinished, "unfinished", Array.Empty<object>());
			Scribe_References.Look<Bill>(ref bill, "bill", false);
			Scribe_References.Look<Thing>(ref dominant, "dominant", false);
			Scribe_References.Look<Building_WorkTable>(ref my_workTable, "my_workTable", false);
			if (unfinished == null)
			{
				Scribe_Collections.Look<Thing>(ref ingredients, "ingredients", (LookMode)2, Array.Empty<object>());
			}
			else
			{
				Scribe_Collections.Look<Thing>(ref ingredients, "ingredients", (LookMode)3, Array.Empty<object>());
			}
		}

		private void AllowBills()
		{
			if (my_workTable == null || !GenCollection.Any<Bill>(my_workTable.BillStack.Bills, (Predicate<Bill>)((Bill b) => b is IBill_PawnForbidded)))
			{
				return;
			}
			List<Bill> source = my_workTable.BillStack.Bills.ToList();
			my_workTable.BillStack.Clear();
			my_workTable.BillStack.Bills.AddRange(source.SelectMany(delegate(Bill b)
			{
				//IL_006f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0079: Expected O, but got Unknown
				//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
				//IL_00dc: Expected O, but got Unknown
				IBill_PawnForbidded bill_PawnForbidded = b as IBill_PawnForbidded;
				Bill value = b;
				if (bill_PawnForbidded == null)
				{
					return Ops.Option<Bill>(value);
				}
				Bill_ProductionWithUft val = (Bill_ProductionWithUft)(object)((b is Bill_ProductionWithUft) ? b : null);
				if (val != null)
				{
					value = (Bill)(object)val.CopyTo<Bill_ProductionWithUft, Bill_ProductionWithUft>((Bill_ProductionWithUft)Activator.CreateInstance(((object)bill_PawnForbidded.Original)?.GetType() ?? typeof(Bill_ProductionWithUft), ((Bill)val).recipe, ((Bill)val).precept));
				}
				else
				{
					Bill_Production val2 = (Bill_Production)(object)((b is Bill_Production) ? b : null);
					if (val2 != null)
					{
						value = (Bill)(object)val2.CopyTo<Bill_Production, Bill_Production>((Bill_Production)Activator.CreateInstance(((object)bill_PawnForbidded.Original)?.GetType() ?? typeof(Bill_Production), ((Bill)val2).recipe, ((Bill)val2).precept));
					}
				}
				return Ops.Option<Bill>(value);
			}));
		}

		private List<Thing> Consumable()
		{
			return mySAL.GetAllTargetCells().SelectMany((IntVec3 c) => c.AllThingsInCellForUse(map, allowStorageZones: false)).Distinct()
				.ToList();
		}

		public override void Reset(WorkingState workingState)
		{
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			if (workingState == WorkingState.Working)
			{
				if (unfinished == null)
				{
					ingredients.ForEach(delegate(Thing t)
					{
						//IL_0002: Unknown result type (might be due to invalid IL or missing references)
						GenPlace.TryPlaceThing(t, Position, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					});
				}
				else
				{
					GenPlace.TryPlaceThing((Thing)(object)unfinished, Position, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					((Thing)unfinished).Destroy((DestroyMode)6);
				}
			}
			bill = null;
			dominant = null;
			unfinished = null;
			ingredients = null;
			base.Reset(workingState);
		}

		public override void CreateWorkingEffect(MapTickManager mapTickManager)
		{
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			EffecterDef effectWorking = bill.recipe.effectWorking;
			workingEffect = ((effectWorking != null) ? effectWorking.Spawn() : null);
			SoundDef soundWorking = bill.recipe.soundWorking;
			if (soundWorking != null && soundWorking.sustain)
			{
				workingSound = ((soundWorking != null) ? SoundStarter.TrySpawnSustainer(soundWorking, SoundInfo.op_Implicit((Thing)(object)my_workTable)) : null);
				Sustainer obj = workingSound;
				if (obj != null)
				{
					obj.Maintain();
				}
			}
			else if (soundWorking != null)
			{
				SoundStarter.PlayOneShot(soundWorking, SoundInfo.op_Implicit((Thing)(object)my_workTable));
			}
			mapTickManager.EachTickAction(EffectTick);
			base.CreateWorkingEffect(mapTickManager);
		}

		public override void CleanupWorkingEffect(MapTickManager mapTickManager)
		{
			Effecter obj = workingEffect;
			if (obj != null)
			{
				obj.Cleanup();
			}
			workingEffect = null;
			Sustainer obj2 = workingSound;
			if (obj2 != null)
			{
				obj2.End();
			}
			workingSound = null;
			mapTickManager.RemoveEachTickAction(EffectTick);
			base.CleanupWorkingEffect(mapTickManager);
		}

		protected bool EffectTick()
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			Effecter obj = workingEffect;
			if (obj != null)
			{
				obj.EffectTick(new TargetInfo((Thing)(object)mySAL), new TargetInfo((Thing)(object)my_workTable));
			}
			return workingEffect == null;
		}

		private List<ThingAmount> Ingredients(Bill bill, List<Thing> consumable)
		{
			List<ThingAmount> arg = consumable.Select((Thing x) => new ThingAmount(x, x.stackCount)).ToList();
			Func<List<ThingAmount>, List<ThingDefGroup>> grouping = (List<ThingAmount> consumableAmounts) => (from c in consumableAmounts
				group c by c.thing.def into c
				select new
				{
					Def = c.Key,
					Count = c.Sum((ThingAmount t) => t.count),
					Amounts = c.Select((ThingAmount t) => t)
				} into g
				orderby g.Def.IsStuff descending, (float)g.Count * bill.recipe.IngredientValueGetter.ValuePerUnitOf(g.Def) descending
				select new ThingDefGroup
				{
					def = g.Def,
					consumable = g.Amounts.ToList()
				}).ToList();
			List<ThingDefGroup> grouped = grouping(arg);
			List<List<ThingAmount>> source = bill.recipe.ingredients.Select(delegate(IngredientCount i)
			{
				List<ThingAmount> list = new List<ThingAmount>();
				float num = i.GetBaseCount();
				foreach (ThingDefGroup item in grouped)
				{
					foreach (ThingAmount item2 in item.consumable)
					{
						Thing thing = item2.thing;
						if (i.filter.Allows(thing) && (bill.ingredientFilter.Allows(thing) || i.IsFixedIngredient) && !map.reservationManager.AllReservedThings().Contains(thing))
						{
							num -= bill.recipe.IngredientValueGetter.ValuePerUnitOf(thing.def) * (float)item2.count;
							int num2 = item2.count;
							if (num <= 0f)
							{
								num2 -= Mathf.RoundToInt((0f - num) / bill.recipe.IngredientValueGetter.ValuePerUnitOf(thing.def));
								num = 0f;
							}
							list.Add(new ThingAmount(thing, num2));
						}
						if (num <= 0f)
						{
							break;
						}
					}
					if (num <= 0f)
					{
						break;
					}
					if ((item.def.IsStuff && bill.recipe.productHasIngredientStuff) || !bill.recipe.allowMixingIngredients)
					{
						num = i.GetBaseCount();
						list.Clear();
					}
				}
				if (num <= 0f)
				{
					list.ForEach(delegate(ThingAmount r)
					{
						List<ThingAmount> consumable2 = grouped.Find((ThingDefGroup x) => x.def == r.thing.def).consumable;
						ThingAmount thingAmount = consumable2.Find((ThingAmount x) => x.thing == r.thing);
						consumable2.Remove(thingAmount);
						thingAmount.count -= r.count;
						consumable2.Add(thingAmount);
					});
					grouped = grouping(grouped.SelectMany((ThingDefGroup x) => x.consumable).ToList());
					return list;
				}
				return new List<ThingAmount>();
			}).ToList();
			if (source.All((List<ThingAmount> x) => x.Count > 0))
			{
				return source.SelectMany((List<ThingAmount> c) => c).ToList();
			}
			return new List<ThingAmount>();
		}

		private Bill GetNextBill(List<Thing> consumable, out List<ThingAmount> billIngredients)
		{
			billIngredients = new List<ThingAmount>();
			if (!my_workTable.CurrentlyUsableForBills())
			{
				return null;
			}
			foreach (Bill item in my_workTable.billStack)
			{
				if (!item.ShouldDoNow() || !item.recipe.AvailableNow)
				{
					continue;
				}
				List<SkillRequirement> skillRequirements = item.recipe.skillRequirements;
				if (skillRequirements != null && !skillRequirements.All((SkillRequirement r) => r.minLevel <= mySAL.GetSkillLevel(r.skill)))
				{
					continue;
				}
				if (item.recipe.ingredients.Count == 0)
				{
					billIngredients = null;
					return item;
				}
				if (consumable != null)
				{
					billIngredients = Ingredients(item, consumable);
					if (billIngredients.Count > 0)
					{
						return item;
					}
				}
			}
			billIngredients = new List<ThingAmount>();
			return null;
		}

		public override void WorkDone(out List<Thing> products)
		{
			products = GenRecipe2.MakeRecipeProducts(bill.recipe, mySAL, ingredients, dominant, (IBillGiver)(object)my_workTable, bill.precept).ToList();
			ingredients.ForEach(delegate(Thing i)
			{
				bill.recipe.Worker.ConsumeIngredient(i, bill.recipe, map);
			});
			Ops.Option<UnfinishedThing>(unfinished).ForEach(delegate(UnfinishedThing u)
			{
				((Thing)u).Destroy((DestroyMode)0);
			});
			bill.Notify_IterationCompleted((Pawn)null, ingredients);
			bill = null;
			dominant = null;
			unfinished = null;
			ingredients = null;
			mySAL.ModifyProductExt?.ProcessProducts(products, (IBillGiver)(object)((this is IBillGiver) ? this : null), (Thing)(object)mySAL, bill.recipe);
		}

		public override bool TryStartWork(out float workAmount)
		{
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Expected O, but got Unknown
			//IL_0127: Unknown result type (might be due to invalid IL or missing references)
			//IL_0131: Expected O, but got Unknown
			//IL_015e: Unknown result type (might be due to invalid IL or missing references)
			workAmount = 0f;
			List<Thing> consumable = Consumable();
			List<ThingAmount> billIngredients;
			Bill nextBill = GetNextBill(consumable, out billIngredients);
			if (nextBill == null)
			{
				return false;
			}
			bill = nextBill;
			ingredients = (from t in billIngredients?.Where((ThingAmount t) => t.count > 0)
				select t.thing.SplitOff(t.count))?.ToList() ?? new List<Thing>();
			dominant = ProjectSal_Utilities.CalculateDominantIngredient(bill.recipe, ingredients);
			if (bill.recipe.UsesUnfinishedThing)
			{
				ThingDef val = ((!((BuildableDef)bill.recipe.unfinishedThingDef).MadeFromStuff) ? null : dominant.def);
				unfinished = (UnfinishedThing)ThingMaker.MakeThing(bill.recipe.unfinishedThingDef, val);
				unfinished.BoundBill = (Bill_ProductionWithUft)bill;
				unfinished.ingredients = ingredients;
				CompColorable val2 = ThingCompUtility.TryGetComp<CompColorable>((Thing)(object)unfinished);
				if (val2 != null)
				{
					val2.SetColor(dominant.DrawColor);
				}
			}
			ThingDef val3 = null;
			if (bill.recipe.UsesUnfinishedThing && ((BuildableDef)bill.recipe.unfinishedThingDef).MadeFromStuff)
			{
				val3 = ((!bill.recipe.UsesUnfinishedThing) ? null : dominant?.def);
			}
			workAmount = bill.recipe.WorkAmountForStuff(val3);
			float statValue = StatExtension.GetStatValue((Thing)(object)my_workTable, StatDefOf.WorkTableWorkSpeedFactor, true, -1);
			workAmount /= statValue;
			return true;
		}

		public override TargetInfo TargetInfo()
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			return TargetInfo.op_Implicit((Thing)(object)my_workTable);
		}
	}
	public class SAL_TargetResearch : SAL_TargetBench
	{
		private Building_ResearchBench researchBench;

		public SAL_TargetResearch()
		{
		}

		public SAL_TargetResearch(Building_AutoMachineTool mySAL, IntVec3 position, Map map, Rot4 rotation, Building_ResearchBench researchBench)
			: base(mySAL, position, map, rotation)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			this.researchBench = researchBench;
		}

		public override bool Ready()
		{
			ResearchProjectDef project = Find.ResearchManager.GetProject((KnowledgeCategoryDef)null);
			return project != null && project.CanBeResearchedAt(researchBench, false);
		}

		public override void Free()
		{
			GeneralRelease((Building)(object)researchBench);
		}

		public override void Reserve()
		{
			GeneralReserve((Building)(object)researchBench);
		}

		public override void WorkDone(out List<Thing> products)
		{
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			products = new List<Thing>();
			ResearchProjectDef project = Find.ResearchManager.GetProject((KnowledgeCategoryDef)null);
			if (project != null)
			{
				float num = Mathf.Max(mySAL.PowerWorkSetting.GetSpeedFactor() * ((float)mySAL.GetSkillLevel(SkillDefOf.Intellectual) * 0.115f + 0.08f), 0.1f);
				num *= StatExtension.GetStatValue((Thing)(object)researchBench, StatDefOf.ResearchSpeedFactor, true, -1);
				num /= project.CostFactor(Faction.OfPlayer.def.techLevel);
				num *= 1000f;
				Find.ResearchManager.ResearchPerformed(num, (Pawn)null);
			}
		}

		public override bool TryStartWork(out float workAmount)
		{
			workAmount = 0f;
			if (Find.ResearchManager.GetProject((KnowledgeCategoryDef)null) == null)
			{
				return false;
			}
			workAmount = 1000f;
			return true;
		}

		public override TargetInfo TargetInfo()
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			return TargetInfo.op_Implicit((Thing)(object)researchBench);
		}

		public override void ExposeData()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Invalid comparison between Unknown and I4
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Invalid comparison between Unknown and I4
			if ((int)Scribe.mode == 1)
			{
				Free();
			}
			base.ExposeData();
			Scribe_References.Look<Building_ResearchBench>(ref researchBench, "researchBench", false);
			if ((int)Scribe.mode == 1)
			{
				Reserve();
			}
		}
	}
	public class SAL_TargetDeepDrill : SAL_TargetBench
	{
		private Building drilltypeBuilding;

		private CompDeepDrill compDeepDrill;

		private readonly float[] miningYieldFactors = new float[21]
		{
			0.6f, 0.7f, 0.8f, 0.85f, 0.9f, 0.925f, 0.95f, 0.975f, 1f, 1f,
			1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f,
			1f
		};

		private const float DeepDrillWorkAmount = 1000f;

		public SAL_TargetDeepDrill()
		{
		}

		public SAL_TargetDeepDrill(Building_AutoMachineTool mySAL, IntVec3 position, Map map, Rot4 rotation, Building drilltypeBuilding)
			: base(mySAL, position, map, rotation)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			this.drilltypeBuilding = drilltypeBuilding;
			if (compDeepDrill == null)
			{
				compDeepDrill = ThingCompUtility.TryGetComp<CompDeepDrill>((Thing)(object)drilltypeBuilding);
			}
		}

		public override bool Ready()
		{
			int result;
			if (ThingCompUtility.TryGetComp<CompDeepDrill>((Thing)(object)drilltypeBuilding).CanDrillNow())
			{
				CompForbiddable comp = ((ThingWithComps)drilltypeBuilding).GetComp<CompForbiddable>();
				result = ((comp == null || !comp.Forbidden) ? 1 : 0);
			}
			else
			{
				result = 0;
			}
			return (byte)result != 0;
		}

		public override void Free()
		{
			GeneralRelease(drilltypeBuilding);
		}

		public override void Reserve()
		{
			GeneralReserve(drilltypeBuilding);
		}

		public override void WorkDone(out List<Thing> products)
		{
			products = new List<Thing>();
			CompDeepDrill obj = ThingCompUtility.TryGetComp<CompDeepDrill>((Thing)(object)drilltypeBuilding);
			float num = 1000f * Mathf.Max(mySAL.PowerWorkSetting.GetSpeedFactor() * ((float)mySAL.GetSkillLevel(SkillDefOf.Mining) * 0.12f + 0.04f), 0.1f);
			ReflectionUtility.DrillPortionProgress.SetValue(obj, (float)ReflectionUtility.DrillPortionProgress.GetValue(obj) + num);
			ReflectionUtility.DrillPortionYieldPct.SetValue(obj, (float)ReflectionUtility.DrillPortionYieldPct.GetValue(obj) + num * miningYieldFactors[mySAL.GetSkillLevel(SkillDefOf.Mining)] / 10000f);
			ReflectionUtility.DrillLastUsedTick.SetValue(obj, Find.TickManager.TicksGame);
			if ((float)ReflectionUtility.DrillPortionProgress.GetValue(obj) > 10000f)
			{
				ReflectionUtility.DrillTryProducePortion.Invoke(obj, new object[2]
				{
					ReflectionUtility.DrillPortionYieldPct.GetValue(obj),
					null
				});
				ReflectionUtility.DrillPortionProgress.SetValue(obj, 0);
				ReflectionUtility.DrillPortionYieldPct.SetValue(obj, 0);
			}
		}

		public override bool TryStartWork(out float workAmount)
		{
			workAmount = 0f;
			if (compDeepDrill == null)
			{
				compDeepDrill = ThingCompUtility.TryGetComp<CompDeepDrill>((Thing)(object)drilltypeBuilding);
			}
			if (!compDeepDrill.CanDrillNow())
			{
				return false;
			}
			workAmount = 1000f;
			return true;
		}

		public override TargetInfo TargetInfo()
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			return TargetInfo.op_Implicit((Thing)(object)drilltypeBuilding);
		}

		public override void ExposeData()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Invalid comparison between Unknown and I4
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Invalid comparison between Unknown and I4
			if ((int)Scribe.mode == 1)
			{
				Free();
			}
			base.ExposeData();
			Scribe_References.Look<Building>(ref drilltypeBuilding, "drilltypeBuilding", false);
			if ((int)Scribe.mode == 1)
			{
				Reserve();
			}
		}
	}
	public class SectionLayer_UGConveyor : SectionLayer_Things
	{
		public SectionLayer_UGConveyor(Section section)
			: base(section)
		{
			base.requireAddToMapMesh = false;
			((MapDrawLayer)this).relevantChangeTypes = MapMeshFlagDef.op_Implicit(MapMeshFlagDefOf.Buildings);
		}

		public override void DrawLayer()
		{
			if (OverlayDrawHandler_UGConveyor.ShouldDraw)
			{
				((SectionLayer_Things)this).DrawLayer();
			}
		}

		protected override void TakePrintFrom(Thing t)
		{
			if ((t.Faction == null || t.Faction == Faction.OfPlayer) && t is Building_BeltConveyor { IsUnderground: not false })
			{
				t.Graphic.Print((SectionLayer)(object)this, t, 0f);
			}
		}
	}
	public static class OverlayDrawHandler_UGConveyor
	{
		private static int lastDrawFrame;

		public static bool ShouldDraw => lastDrawFrame + 1 >= Time.frameCount;

		public static void DrawOverlayThisFrame()
		{
			lastDrawFrame = Time.frameCount;
		}
	}
}
namespace ProjectRimFactory.Archo
{
	public static class ArchoCipher
	{
		public static string Decipher(string str)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			if (Regex.IsMatch(str, "[\"']\\)?;"))
			{
				return TaggedString.op_Implicit(Translator.Translate("PRF_ArchoCipher_InternalServerError"));
			}
			string text = "PRF_ArchoCipherKey_" + str.Replace(' ', '_');
			TaggedString val = default(TaggedString);
			return TaggedString.op_Implicit(Translator.TryTranslate(text, ref val) ? val : TaggedString.op_Implicit((string)null));
		}
	}
	public class CompProperties_Unstable : CompProperties
	{
		public int ticksToDisintegrate;

		public CompProperties_Unstable()
		{
			base.compClass = typeof(CompUnstable);
		}
	}
	public class CompUnstable : ThingComp
	{
		public int ticksLeft;

		private CompProperties_Unstable Props => (CompProperties_Unstable)(object)base.props;

		public override void CompTick()
		{
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).CompTick();
			ticksLeft--;
			if (ticksLeft <= 0)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF_DisintegrationMessage", NamedArgument.op_Implicit(((Entity)base.parent).LabelCap))), LookTargets.op_Implicit(new GlobalTargetInfo(((Thing)base.parent).Position, ((Thing)base.parent).Map, false)), MessageTypeDefOf.NegativeEvent, true);
				((Thing)base.parent).Destroy((DestroyMode)0);
			}
		}

		public override string CompInspectStringExtra()
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("PRF_TimeLeftToDisintegrate", NamedArgument.op_Implicit(GenDate.ToStringTicksToPeriod(ticksLeft, true, false, true, true, false))));
		}

		public override void Initialize(CompProperties compProperties)
		{
			((ThingComp)this).Initialize(compProperties);
			ticksLeft = Props.ticksToDisintegrate;
		}

		public override void PostExposeData()
		{
			Scribe_Values.Look<int>(ref ticksLeft, "ticksLeft", 0, false);
		}

		public override string TransformLabel(string label)
		{
			float num = (float)ticksLeft / (float)Props.ticksToDisintegrate;
			int num2 = Mathf.RoundToInt(num * 255f);
			int num3 = Mathf.RoundToInt((1f - num) * 255f);
			return $"<color=#{num3:x2}{num2:x2}00>{label}</color>";
		}
	}
	public class ITab_ArchoCipher : ITab
	{
		private string ciphertext;

		private string resultText;

		public override bool IsVisible => PRFDefOf.PRFOrdoDataRummaging.IsFinished;

		public ITab_ArchoCipher()
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).size = new Vector2(400f, 400f);
			((InspectTabBase)this).labelKey = "PRF_ITab_ArchoCipher";
		}

		protected override void FillTab()
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Expected O, but got Unknown
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0160: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Unknown result type (might be due to invalid IL or missing references)
			Rect val = GenUI.ContractedBy(new Rect(0f, 0f, ((InspectTabBase)this).size.x, ((InspectTabBase)this).size.y), 10f);
			Listing_Standard val2 = new Listing_Standard();
			((Listing)val2).Begin(val);
			val2.Label(Translator.Translate("PRFArchoCipherInterpreter"), -1f, (string)null);
			ciphertext = val2.TextEntryLabeled(TaggedString.op_Implicit(Translator.Translate("PRF_InsertCiphertext")), ciphertext, 1);
			if (val2.ButtonText(TaggedString.op_Implicit(Translator.Translate("PRF_ButtonDecipher")), (string)null, 1f))
			{
				string text = ArchoCipher.Decipher(ciphertext);
				if (text != null)
				{
					resultText = text;
				}
				else
				{
					Random random = new Random(((IEnumerable<char>)ciphertext).Sum((Func<char, int>)Convert.ToInt32));
					int num = random.Next(1, 513);
					string text2 = TaggedString.op_Implicit(Translator.Translate("PRF_GibberishAlphabet"));
					char[] array = new char[num];
					for (int i = 0; i < array.Length; i++)
					{
						array[i] = text2[random.Next(text2.Length)];
					}
					resultText = new string(array);
				}
			}
			if (val2.ButtonText(TaggedString.op_Implicit(Translator.Translate("PRF_ButtonClear")), (string)null, 1f))
			{
				resultText = null;
				ciphertext = null;
			}
			val2.Label(resultText, -1f, (TipSignal?)null);
			((Listing)val2).End();
		}
	}
	public class PlaceWorker_PortalGenerator : PlaceWorker
	{
		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			((PlaceWorker)this).DrawGhost(def, center, rot, ghostCol, (Thing)null);
			PortalGeneratorUtility.DrawBlueprintFieldEdges(center);
		}
	}
	public class PortalGeneratorUtility
	{
		public static void DrawBlueprintFieldEdges(IntVec3 center)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			List<List<IntVec3>> list = FieldEdgeCells(center);
			GenDraw.DrawFieldEdges(list[0], new Color(48f / 85f, 0.58431375f, 0.36078432f), (float?)null, (HashSet<IntVec3>)null, 2900);
			GenDraw.DrawFieldEdges(list[1], new Color(0.4392157f, 16f / 85f, 32f / 51f), (float?)null, (HashSet<IntVec3>)null, 2900);
			GenDraw.DrawFieldEdges(list[2], new Color(0.18039216f, 39f / 85f, 0.7137255f), (float?)null, (HashSet<IntVec3>)null, 2900);
		}

		public static List<List<IntVec3>> FieldEdgeCells(IntVec3 center)
		{
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_009b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0105: Unknown result type (might be due to invalid IL or missing references)
			List<List<IntVec3>> list = new List<List<IntVec3>>(3)
			{
				new List<IntVec3>(),
				new List<IntVec3>(),
				new List<IntVec3>()
			};
			for (int i = -3; i <= 3; i++)
			{
				for (int j = -3; j <= 3; j++)
				{
					bool flag = Math.Abs(i) <= 1;
					bool flag2 = Math.Abs(j) <= 1;
					bool flag3 = Math.Abs(i) <= 2 && Math.Abs(j) <= 2;
					if (flag && flag2)
					{
						list[0].Add(new IntVec3(center.x + i, center.y, center.z + j));
					}
					else if (flag3)
					{
						list[1].Add(new IntVec3(center.x + i, center.y, center.z + j));
					}
					else if (flag || flag2)
					{
						list[2].Add(new IntVec3(center.x + i, center.y, center.z + j));
					}
				}
			}
			return list;
		}
	}
	public class ResearchMod_Notify : ResearchMod
	{
		public string text;

		public override void Apply()
		{
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Expected O, but got Unknown
			WorldComponent_NotificatonManager component = Current.Game.World.GetComponent<WorldComponent_NotificatonManager>();
			if (component != null && Find.WindowStack.WindowOfType<Dialog_MessageBox>() == null && !component.notifiedMessages.Contains(text))
			{
				Find.WindowStack.Add((Window)new Dialog_MessageBox(TaggedString.op_Implicit(text), (string)null, (Action)null, (string)null, (Action)null, (string)null, false, (Action)null, (Action)null, (WindowLayer)1));
				component.notifiedMessages.Add(text);
			}
		}
	}
	public class WorldComponent_NotificatonManager(World world) : WorldComponent(world)
	{
		public List<string> notifiedMessages = new List<string>();

		public override void ExposeData()
		{
			((WorldComponent)this).ExposeData();
			Scribe_Collections.Look<string>(ref notifiedMessages, "notifiedMessages", (LookMode)1, Array.Empty<object>());
		}
	}
}
namespace ProjectRimFactory.Archo.Things
{
	public class Building_HexCapacitor : Building
	{
		private CompPowerBattery batteryComp;

		public override void DrawGUIOverlay()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Invalid comparison between Unknown and I4
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).DrawGUIOverlay();
			if ((int)Find.CameraDriver.CurrentZoom < 2)
			{
				GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor((Thing)(object)this, 0f), batteryComp.StoredEnergy.ToString("F0"), Color.white);
			}
		}

		public override void PostMake()
		{
			((ThingWithComps)this).PostMake();
			batteryComp = ((ThingWithComps)this).GetComp<CompPowerBattery>();
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			batteryComp = ((ThingWithComps)this).GetComp<CompPowerBattery>();
		}
	}
	public class Building_PaperclipSpawner : Building
	{
		private int workDone;

		private CompPowerTrader powerComp;

		private CompOutputAdjustable outputComp;

		public override void PostMake()
		{
			((ThingWithComps)this).PostMake();
			powerComp = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			outputComp = ((ThingWithComps)this).GetComp<CompOutputAdjustable>();
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			powerComp = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			outputComp = ((ThingWithComps)this).GetComp<CompOutputAdjustable>();
		}

		protected override void Tick()
		{
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			if (((Thing)this).Spawned && Gen.IsHashIntervalTick((Thing)(object)this, 10) && powerComp.PowerOn)
			{
				workDone++;
				if (workDone >= 250)
				{
					Thing val = ThingMaker.MakeThing(PRFDefOf.Paperclip, (ThingDef)null);
					val.stackCount = 10000;
					GenPlace.TryPlaceThing(val, outputComp.CurrentCell, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					workDone = 0;
				}
			}
		}

		public override string GetInspectString()
		{
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			string inspectString = ((ThingWithComps)this).GetInspectString();
			if (!string.IsNullOrEmpty(inspectString))
			{
				stringBuilder.AppendLine(inspectString);
			}
			stringBuilder.Append(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("NextSpawnedItemIn", NamedArgument.op_Implicit(GenLabel.ThingLabel((BuildableDef)(object)PRFDefOf.Paperclip, (ThingDef)null, 10000))) + ": " + GenDate.ToStringTicksToPeriod((250 - workDone) * 10, true, false, true, true, false)));
			return GenText.TrimEndNewlines(stringBuilder.ToString());
		}

		public override void ExposeData()
		{
			((Building)this).ExposeData();
			Scribe_Values.Look<int>(ref workDone, "workDone", 0, false);
		}
	}
	public class Building_PortalGenerator : Building
	{
		private static readonly FieldInfo InnerContainerField = typeof(Building_Casket).GetField("innerContainer", BindingFlags.Instance | BindingFlags.NonPublic);

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			if (Prefs.DevMode)
			{
				yield return (Gizmo)new Command_Action
				{
					defaultLabel = "DEBUG: Debug actions",
					action = delegate
					{
						//IL_000b: Unknown result type (might be due to invalid IL or missing references)
						//IL_0015: Expected O, but got Unknown
						Find.WindowStack.Add((Window)new FloatMenu(GetDebugActions()));
					}
				};
			}
		}

		public override void DrawExtraSelectionOverlays()
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).DrawExtraSelectionOverlays();
			PortalGeneratorUtility.DrawBlueprintFieldEdges(((Thing)this).Position);
		}

		private List<FloatMenuOption> GetDebugActions()
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Expected O, but got Unknown
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Expected O, but got Unknown
			return new List<FloatMenuOption>
			{
				new FloatMenuOption("Liquidate room", (Action)LiquidateRoom, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
				new FloatMenuOption("Calculate eligibility for liquidation", (Action)delegate
				{
					//IL_0002: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					AcceptanceReport val = RecalculateEligibilityForTeleport();
					if (((AcceptanceReport)(ref val)).Accepted)
					{
						Messages.Message("Eligible for liquidation.", MessageTypeDefOf.PositiveEvent, true);
					}
					else
					{
						Messages.Message(((AcceptanceReport)(ref val)).Reason, MessageTypeDefOf.RejectInput, true);
					}
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			};
		}

		private unsafe AcceptanceReport RecalculateEligibilityForTeleport()
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0286: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_027e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0283: Unknown result type (might be due to invalid IL or missing references)
			//IL_0270: Unknown result type (might be due to invalid IL or missing references)
			//IL_0275: Unknown result type (might be due to invalid IL or missing references)
			//IL_027a: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Invalid comparison between Unknown and I4
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0167: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0175: Unknown result type (might be due to invalid IL or missing references)
			//IL_0202: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0198: Unknown result type (might be due to invalid IL or missing references)
			//IL_019d: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0210: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0233: Unknown result type (might be due to invalid IL or missing references)
			//IL_0238: Unknown result type (might be due to invalid IL or missing references)
			//IL_023d: Unknown result type (might be due to invalid IL or missing references)
			int num = 0;
			Room room = GridsUtility.GetRoom(((Thing)this).Position, ((Thing)this).Map);
			if (room == null || room.OpenRoofCount != 0)
			{
				return AcceptanceReport.op_Implicit(Translator.Translate("TeleportReport_RoomOutdoorsOrUnroofed"));
			}
			List<List<IntVec3>> list = PortalGeneratorUtility.FieldEdgeCells(((Thing)this).Position);
			CellRect val = CellRect.CenteredOn(((Thing)this).Position, 7, 7);
			Enumerator enumerator = ((CellRect)(ref val)).GetEnumerator();
			try
			{
				while (((Enumerator)(ref enumerator)).MoveNext())
				{
					IntVec3 current = ((Enumerator)(ref enumerator)).Current;
					foreach (Thing thing in GridsUtility.GetThingList(current, ((Thing)this).Map))
					{
						if ((int)((BuildableDef)thing.def).passability == 2)
						{
							return AcceptanceReport.op_Implicit(Translator.Translate("TeleportReport_CellImpassable"));
						}
						Building_CryptosleepCasket val2 = (Building_CryptosleepCasket)(object)((thing is Building_CryptosleepCasket) ? thing : null);
						if (val2 == null)
						{
							continue;
						}
						foreach (Thing item in (IEnumerable<Thing>)((Building_Casket)val2).SearchableContents)
						{
							Pawn val3 = (Pawn)(object)((item is Pawn) ? item : null);
							if (val3 != null && val3.RaceProps.Humanlike)
							{
								num++;
							}
						}
					}
					if (list[0].Contains(current))
					{
						if (GridsUtility.GetTerrain(current, ((Thing)this).Map) != PRFDefOf.PRFFloorComputer)
						{
							return AcceptanceReport.op_Implicit(Translator.Translate("TeleportReport_FloorLayoutIncorrect"));
						}
					}
					else if (list[1].Contains(current))
					{
						if (GridsUtility.GetTerrain(current, ((Thing)this).Map) != PRFDefOf.PRFZCompositeTile)
						{
							return AcceptanceReport.op_Implicit(Translator.Translate("TeleportReport_FloorLayoutIncorrect"));
						}
					}
					else if (list[2].Contains(current) && GridsUtility.GetTerrain(current, ((Thing)this).Map) != PRFDefOf.PRFYCompositeTile)
					{
						return AcceptanceReport.op_Implicit(Translator.Translate("TeleportReport_FloorLayoutIncorrect"));
					}
				}
			}
			finally
			{
				((IDisposable)(*(Enumerator*)(&enumerator))/*cast due to constrained. prefix*/).Dispose();
			}
			if (num == 0)
			{
				return AcceptanceReport.op_Implicit(Translator.Translate("TeleportReport_NoHumanlikePawnsForTransportation"));
			}
			return AcceptanceReport.op_Implicit(true);
		}

		private void LiquidateRoom()
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			Room room = GridsUtility.GetRoom(((Thing)this).Position, ((Thing)this).Map);
			if (room == null || room.PsychologicallyOutdoors)
			{
				return;
			}
			float stat = room.GetStat(RoomStatDefOf.Wealth);
			float num = room.CellCount;
			float num2 = 0f;
			float num3 = 0f;
			foreach (IntVec3 cell in room.Cells)
			{
				if (((Thing)this).Map.terrainGrid.CanRemoveTopLayerAt(cell))
				{
					((Thing)this).Map.terrainGrid.RemoveTopLayer(cell, false);
					FilthMaker.RemoveAllFilth(cell, ((Thing)this).Map);
				}
				foreach (Thing item in GridsUtility.GetThingList(cell, ((Thing)this).Map).ToList())
				{
					if (item is Building_CryptosleepCasket)
					{
						foreach (Thing item2 in (IEnumerable<Thing>)InnerContainerField.GetValue(item))
						{
							Pawn val = (Pawn)(object)((item2 is Pawn) ? item2 : null);
							if (val != null)
							{
								if (val.RaceProps.Humanlike)
								{
									num2 += 1f;
								}
								else
								{
									num3 += 1f;
								}
							}
						}
					}
					if (item.def.destroyable && (object)item != this)
					{
						item.Destroy((DestroyMode)0);
					}
				}
			}
			float num4 = 0.001f * stat + num + 10f * num3 + 100f * num2;
			if (Prefs.DevMode)
			{
				Log.Message($"==SpdTec Room Liquidation Report==\nWealth: {stat}\nRoom size: {num}\nPawns: (non-human {num3}), (human {num2})\nPoints: {num4}");
			}
			((Thing)this).Destroy((DestroyMode)0);
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
}
namespace ProjectRimFactory.AnimalStation
{
	public abstract class Building_CompHarvester : Building_Storage, IPowerSupplyMachineHolder
	{
		private static readonly PropertyInfo ResourceAmount = typeof(CompHasGatherableBodyResource).GetProperty("ResourceAmount", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly PropertyInfo ResourceDef = typeof(CompHasGatherableBodyResource).GetProperty("ResourceDef", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly FieldInfo Fullness = typeof(CompHasGatherableBodyResource).GetField("fullness", BindingFlags.Instance | BindingFlags.NonPublic);

		private CompPowerTrader compPowerTrader;

		private CompOutputAdjustable compOutputAdjustable;

		private CompPowerWorkSetting compPowerWorkSetting;

		private IEnumerable<IntVec3> ScannerCells
		{
			get
			{
				//IL_0017: Unknown result type (might be due to invalid IL or missing references)
				//IL_001c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0020: Unknown result type (might be due to invalid IL or missing references)
				//IL_0025: Unknown result type (might be due to invalid IL or missing references)
				object obj = compPowerWorkSetting?.GetRangeCells();
				if (obj == null)
				{
					CellRect val = GenAdj.OccupiedRect((Thing)(object)this);
					val = ((CellRect)(ref val)).ExpandedBy(1);
					obj = ((CellRect)(ref val)).Cells;
				}
				return (IEnumerable<IntVec3>)obj;
			}
		}

		public IPowerSupplyMachine RangePowerSupplyMachine => ((ThingWithComps)this).GetComp<CompPowerWorkSetting>();

		private IntVec3 OutputCell(Pawn pawn)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			return compOutputAdjustable?.CurrentCell ?? ((Thing)pawn).Position;
		}

		protected abstract bool CompValidator(CompHasGatherableBodyResource comp);

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building_Storage)this).SpawnSetup(map, respawningAfterLoad);
			compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			compOutputAdjustable = ((ThingWithComps)this).GetComp<CompOutputAdjustable>();
			compPowerWorkSetting = ((ThingWithComps)this).GetComp<CompPowerWorkSetting>();
		}

		public override void TickRare()
		{
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_0147: Expected O, but got Unknown
			//IL_0179: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).TickRare();
			if (!((Thing)this).Spawned || !compPowerTrader.PowerOn)
			{
				return;
			}
			foreach (Pawn item in (from cell in ScannerCells
				from pawn in GridsUtility.GetThingList(cell, ((Thing)this).Map).OfType<Pawn>()
				where ((Thing)pawn).Faction == Faction.OfPlayer
				select pawn).ToList())
			{
				foreach (CompHasGatherableBodyResource item2 in (from comp in ((ThingWithComps)item).AllComps.OfType<CompHasGatherableBodyResource>()
					where CompValidator(comp)
					select comp).ToList())
				{
					int num = GenMath.RoundRandom((float)(int)ResourceAmount.GetValue(item2, null) * item2.Fullness);
					if (num != 0)
					{
						ThingDef val = (ThingDef)ResourceDef.GetValue(item2, null);
						while (num > 0)
						{
							int num2 = Mathf.Clamp(num, 1, val.stackLimit);
							num -= num2;
							Thing val2 = ThingMaker.MakeThing(val, (ThingDef)null);
							val2.stackCount = num2;
							GenPlace.TryPlaceThing(val2, OutputCell(item), ((Thing)item).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
						}
						Fullness.SetValue(item2, 0f);
					}
				}
			}
		}
	}
	public class Building_GenericBodyResourceGatherer : Building_CompHarvester
	{
		protected override bool CompValidator(CompHasGatherableBodyResource comp)
		{
			return true;
		}
	}
	public class Building_Milker : Building_CompHarvester
	{
		protected override bool CompValidator(CompHasGatherableBodyResource comp)
		{
			return comp is CompMilkable;
		}
	}
	public class Building_Shearer : Building_CompHarvester
	{
		protected override bool CompValidator(CompHasGatherableBodyResource comp)
		{
			return comp is CompShearable;
		}
	}
	internal class PlaceWorker_ShowAdjacent : PlaceWorker
	{
		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			((PlaceWorker)this).DrawGhost(def, center, rot, ghostCol, thing);
			CellRect val = GenAdj.OccupiedRect(center, rot, def.size);
			val = ((CellRect)(ref val)).ExpandedBy(1);
			GenDraw.DrawFieldEdges(((CellRect)(ref val)).Cells.ToList().FindAll((IntVec3 cell) => GenGrid.Standable(cell, Find.CurrentMap)), 2900);
		}
	}
}
