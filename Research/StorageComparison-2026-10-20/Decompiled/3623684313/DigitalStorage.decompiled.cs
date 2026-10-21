using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using DigitalStorage.AI;
using DigitalStorage.Backpack;
using DigitalStorage.Compatibility;
using DigitalStorage.Components;
using DigitalStorage.Core;
using DigitalStorage.Effects;
using DigitalStorage.HarmonyPatches;
using DigitalStorage.Performance;
using DigitalStorage.Settings;
using DigitalStorage.UI;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyCompany("DigitalStorage")]
[assembly: AssemblyConfiguration("Debug")]
[assembly: AssemblyFileVersion("2.0.0.0")]
[assembly: AssemblyInformationalVersion("2.0.0")]
[assembly: AssemblyProduct("DigitalStorage")]
[assembly: AssemblyTitle("DigitalStorage")]
[assembly: TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName = ".NET Framework 4.7.2")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("2.0.0.0")]
[module: UnverifiableCode]
namespace DigitalStorage.Services
{
	public class DigitalStorageGameComponent : GameComponent
	{
		private bool shown40Letter;

		private const int ReconcileInterval = 250;

		public DigitalStorageGameComponent(Game game)
		{
		}

		public override void GameComponentTick()
		{
			((GameComponent)this).GameComponentTick();
			if (Find.TickManager == null || Find.TickManager.TicksGame % 250 != 0)
			{
				return;
			}
			List<Map> maps = Find.Maps;
			if (maps == null)
			{
				return;
			}
			for (int i = 0; i < maps.Count; i++)
			{
				Map val = maps[i];
				if (val != null && val.mapPawns != null)
				{
					IReadOnlyList<Pawn> allPawnsSpawned = val.mapPawns.AllPawnsSpawned;
					for (int j = 0; j < allPawnsSpawned.Count; j++)
					{
						BackpackImplant.Sync(allPawnsSpawned[j]);
					}
				}
			}
		}

		public override void FinalizeInit()
		{
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			((GameComponent)this).FinalizeInit();
			CompAutoIngest.ClearSessionCaches();
			BillBenchClaims.ClearAll();
			CraftIngredientClaims.Clear();
			if (!shown40Letter)
			{
				shown40Letter = true;
				Find.LetterStack.ReceiveLetter(Translator.Translate("DigitalStorage_Letter40_Label"), Translator.Translate("DigitalStorage_Letter40_Text"), LetterDefOf.NeutralEvent, (string)null, 0, true);
			}
		}

		public override void ExposeData()
		{
			((GameComponent)this).ExposeData();
			Scribe_Values.Look<bool>(ref shown40Letter, "shown40Letter", false, false);
		}
	}
	public class MapComponent_CraftRange : MapComponent
	{
		private static readonly Color RangeColor = new Color(0.788f, 0.663f, 0.38f, 0.85f);

		public MapComponent_CraftRange(Map map)
			: base(map)
		{
		}

		public override void MapComponentDraw()
		{
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			((MapComponent)this).MapComponentDraw();
			if (base.map == null)
			{
				return;
			}
			List<Building> allBuildingsColonist = base.map.listerBuildings.allBuildingsColonist;
			for (int i = 0; i < allBuildingsColonist.Count; i++)
			{
				Building val = allBuildingsColonist[i];
				if (val != null && !((Thing)val).Destroyed && ((Thing)val).Spawned)
				{
					CompBillAutomation compBillAutomation = ThingCompUtility.TryGetComp<CompBillAutomation>((Thing)(object)val);
					if (compBillAutomation != null && compBillAutomation.ShowRange)
					{
						GenDraw.DrawFieldEdges(compBillAutomation.RangeCellsForDrawing(), RangeColor, (float?)null, (HashSet<IntVec3>)null, 2900);
					}
				}
			}
		}
	}
	public class MapComponent_HopperSupply : MapComponent
	{
		private const float AlreadyFilledFraction = 0.35f;

		private static readonly List<Thing> tmpCandidates = new List<Thing>();

		public MapComponent_HopperSupply(Map map)
			: base(map)
		{
		}

		public override void MapComponentTick()
		{
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			if ((Find.TickManager.TicksGame + base.map.uniqueID) % 15 != 0)
			{
				return;
			}
			List<Thing> list;
			try
			{
				list = base.map.listerThings.ThingsOfDef(ThingDefOf.Hopper);
			}
			catch (Exception)
			{
				return;
			}
			if (list == null || list.Count == 0)
			{
				return;
			}
			tmpCandidates.Clear();
			try
			{
				CoreSupply.GatherContents(base.map, list[0].Position, tmpCandidates);
			}
			catch (Exception ex2)
			{
				Log.ErrorOnce("[DigitalStorage] 料斗候选汇总异常（本轮跳过）：" + ex2, 1146308689);
				return;
			}
			if (tmpCandidates.Count == 0)
			{
				return;
			}
			for (int i = 0; i < list.Count; i++)
			{
				try
				{
					TrySupply(list[i], tmpCandidates);
				}
				catch (Exception ex3)
				{
					Log.ErrorOnce("[DigitalStorage] 料斗投料异常（这一台跳过，其余继续）：" + ex3, 1146308688);
				}
			}
		}

		private void TrySupply(Thing thing, List<Thing> candidates)
		{
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Invalid comparison between Unknown and I4
			//IL_012c: Unknown result type (might be due to invalid IL or missing references)
			//IL_019c: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Invalid comparison between Unknown and I4
			//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f6: Invalid comparison between Unknown and I4
			if (thing == null || thing.Destroyed || !thing.Spawned)
			{
				return;
			}
			Building_Storage val = (Building_Storage)(object)((thing is Building_Storage) ? thing : null);
			if (val == null || ((Thing)val).def == null || ((Thing)val).def.building == null || !((Thing)val).def.building.isHopper)
			{
				return;
			}
			SlotGroup slotGroup = val.GetSlotGroup();
			if (slotGroup == null || slotGroup.Settings == null)
			{
				return;
			}
			IntVec3 cell = ((Thing)val).Position;
			Thing val2 = null;
			List<Thing> thingList = GridsUtility.GetThingList(cell, base.map);
			for (int i = 0; i < thingList.Count; i++)
			{
				Thing val3 = thingList[i];
				if (val3 != null && val3.def != null && (int)val3.def.category == 2 && Building_NutrientPasteDispenser.IsAcceptableFeedstock(val3.def))
				{
					val2 = val3;
				}
			}
			int num2;
			if (val2 != null)
			{
				int num = Math.Max(1, val2.def.stackLimit);
				if ((float)val2.stackCount / (float)num > 0.35f)
				{
					return;
				}
				num2 = num - val2.stackCount;
			}
			else
			{
				num2 = 0;
			}
			if (val2 != null && num2 <= 0)
			{
				return;
			}
			ReservationManager reservationManager = base.map.reservationManager;
			if (reservationManager != null && reservationManager.IsReservedByAnyoneOf(LocalTargetInfo.op_Implicit((Thing)(object)val), Faction.OfPlayer))
			{
				return;
			}
			Thing val4 = null;
			for (int j = 0; j < candidates.Count; j++)
			{
				Thing val5 = candidates[j];
				if (val5 != null && !val5.Destroyed && val5.def != null && val5.ParentHolder is Building_StorageCore && !ForbidUtility.IsForbidden(val5, Faction.OfPlayer) && (reservationManager == null || !reservationManager.IsReservedByAnyoneOf(LocalTargetInfo.op_Implicit(val5), Faction.OfPlayer)) && (val2 == null || val5.def == val2.def) && val5.def.IsNutritionGivingIngestible && val5.def.ingestible != null)
				{
					FoodPreferability preferability = val5.def.ingestible.preferability;
					if (((int)preferability == 4 || (int)preferability == 5) && Building_NutrientPasteDispenser.IsAcceptableFeedstock(val5.def) && slotGroup.Settings.AllowedToAccept(val5))
					{
						val4 = val5;
						break;
					}
				}
			}
			if (val4 == null)
			{
				return;
			}
			int num3 = ((val2 != null) ? Math.Min(val4.stackCount, num2) : Math.Min(val4.stackCount, Math.Max(1, val4.def.stackLimit)));
			if (num3 > 0)
			{
				CoreSupply.TakeAndPlace(val4, num3, (Thing taken) => GenPlace.TryPlaceThing(taken, cell, base.map, (ThingPlaceMode)0, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1));
			}
		}
	}
}
namespace DigitalStorage.Settings
{
	public class DigitalStorageMod : Mod
	{
		private DigitalStorageSettings settings;

		public DigitalStorageMod(ModContentPack content)
			: base(content)
		{
			settings = ((Mod)this).GetSettings<DigitalStorageSettings>();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			DigitalStorageSettings.DoSettingsWindowContents(inRect);
			((Mod)this).DoSettingsWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("DS_SettingsCategory"));
		}
	}
	public class DigitalStorageSettings : ModSettings
	{
		public static float costMultiplier = 1f;

		public static float powerMultiplier = 1f;

		public static float researchMultiplier = 1f;

		public static bool enableDebugLog = false;

		public static bool autoIngestEnabled = true;

		public static bool autoIngestYieldToJobs = true;

		public static bool craftAnyBench = true;

		public static int craftScanRadius = 6;

		public static int craftExpandDepth = 2;

		public static float craftExpandWorkFactor = 0.5f;

		public static bool concurrentCoreDelivery = true;

		public static bool backpackForMechanoids = true;

		public static bool perfOptimizationsEnabled = true;

		public static bool noWorkFailure = true;

		public static int workerCompletionsPerTick = 16;

		public static int fleckBudgetPerFrame = 500;

		private static readonly float[] MultiplierLadder = new float[18]
		{
			0.01f, 0.02f, 0.05f, 0.1f, 0.15f, 0.2f, 0.3f, 0.5f, 0.75f, 1f,
			1.5f, 2f, 3f, 5f, 10f, 20f, 50f, 100f
		};

		public override void ExposeData()
		{
			Scribe_Values.Look<float>(ref costMultiplier, "costMultiplier", 1f, false);
			Scribe_Values.Look<float>(ref powerMultiplier, "powerMultiplier", 1f, false);
			Scribe_Values.Look<float>(ref researchMultiplier, "researchMultiplier", 1f, false);
			Scribe_Values.Look<bool>(ref enableDebugLog, "enableDebugLog", false, false);
			Scribe_Values.Look<bool>(ref autoIngestEnabled, "autoIngestEnabled", true, false);
			Scribe_Values.Look<bool>(ref autoIngestYieldToJobs, "autoIngestYieldToJobs", true, false);
			Scribe_Values.Look<bool>(ref craftAnyBench, "craftAnyBench", true, false);
			Scribe_Values.Look<int>(ref craftScanRadius, "craftScanRadius", 6, false);
			Scribe_Values.Look<int>(ref craftExpandDepth, "craftExpandDepth", 2, false);
			Scribe_Values.Look<float>(ref craftExpandWorkFactor, "craftExpandWorkFactor", 0.5f, false);
			Scribe_Values.Look<bool>(ref concurrentCoreDelivery, "concurrentCoreDelivery", true, false);
			Scribe_Values.Look<bool>(ref backpackForMechanoids, "backpackForMechanoids", true, false);
			Scribe_Values.Look<bool>(ref perfOptimizationsEnabled, "perfOptimizationsEnabled", true, false);
			Scribe_Values.Look<bool>(ref noWorkFailure, "noWorkFailure", true, false);
			Scribe_Values.Look<int>(ref workerCompletionsPerTick, "workerCompletionsPerTick", 16, false);
			Scribe_Values.Look<int>(ref fleckBudgetPerFrame, "fleckBudgetPerFrame", 500, false);
			((ModSettings)this).ExposeData();
		}

		public static void DoSettingsWindowContents(Rect inRect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Expected O, but got Unknown
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d4: Expected O, but got Unknown
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0106: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0126: Unknown result type (might be due to invalid IL or missing references)
			//IL_012c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0139: Expected O, but got Unknown
			//IL_013e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0160: Unknown result type (might be due to invalid IL or missing references)
			//IL_0171: Unknown result type (might be due to invalid IL or missing references)
			//IL_0177: Unknown result type (might be due to invalid IL or missing references)
			//IL_0182: Unknown result type (might be due to invalid IL or missing references)
			//IL_018e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0199: Unknown result type (might be due to invalid IL or missing references)
			//IL_019f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_020a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0215: Unknown result type (might be due to invalid IL or missing references)
			//IL_021b: Unknown result type (might be due to invalid IL or missing references)
			//IL_022f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0248: Unknown result type (might be due to invalid IL or missing references)
			//IL_0253: Unknown result type (might be due to invalid IL or missing references)
			//IL_0259: Unknown result type (might be due to invalid IL or missing references)
			//IL_026d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0286: Unknown result type (might be due to invalid IL or missing references)
			//IL_0291: Unknown result type (might be due to invalid IL or missing references)
			//IL_0297: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_02da: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0305: Unknown result type (might be due to invalid IL or missing references)
			//IL_032a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0335: Unknown result type (might be due to invalid IL or missing references)
			//IL_0340: Unknown result type (might be due to invalid IL or missing references)
			//IL_0345: Unknown result type (might be due to invalid IL or missing references)
			//IL_0350: Unknown result type (might be due to invalid IL or missing references)
			//IL_035d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0381: Unknown result type (might be due to invalid IL or missing references)
			//IL_0387: Unknown result type (might be due to invalid IL or missing references)
			//IL_0392: Unknown result type (might be due to invalid IL or missing references)
			//IL_0398: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_03be: Unknown result type (might be due to invalid IL or missing references)
			//IL_03c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_03d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_03f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_03f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0403: Unknown result type (might be due to invalid IL or missing references)
			//IL_0409: Unknown result type (might be due to invalid IL or missing references)
			//IL_0414: Unknown result type (might be due to invalid IL or missing references)
			//IL_041a: Unknown result type (might be due to invalid IL or missing references)
			//IL_042e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0447: Unknown result type (might be due to invalid IL or missing references)
			//IL_0459: Unknown result type (might be due to invalid IL or missing references)
			//IL_045f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0473: Unknown result type (might be due to invalid IL or missing references)
			//IL_049a: Unknown result type (might be due to invalid IL or missing references)
			//IL_04a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_04b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_04b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0505: Unknown result type (might be due to invalid IL or missing references)
			//IL_0510: Unknown result type (might be due to invalid IL or missing references)
			//IL_051b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0520: Unknown result type (might be due to invalid IL or missing references)
			//IL_052b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0539: Unknown result type (might be due to invalid IL or missing references)
			//IL_0559: Unknown result type (might be due to invalid IL or missing references)
			//IL_055f: Unknown result type (might be due to invalid IL or missing references)
			//IL_056a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0570: Unknown result type (might be due to invalid IL or missing references)
			//IL_0581: Unknown result type (might be due to invalid IL or missing references)
			//IL_0587: Unknown result type (might be due to invalid IL or missing references)
			//IL_0592: Unknown result type (might be due to invalid IL or missing references)
			//IL_059e: Unknown result type (might be due to invalid IL or missing references)
			//IL_05a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_05af: Unknown result type (might be due to invalid IL or missing references)
			//IL_05c3: Unknown result type (might be due to invalid IL or missing references)
			Listing_Standard val = new Listing_Standard();
			((Listing)val).Begin(inRect);
			bool changed = false;
			Text.Font = (GameFont)2;
			val.Label(Translator.Translate("DS_CostSettings"), -1f, (string)null);
			Text.Font = (GameFont)1;
			((Listing)val).Gap(12f);
			val.Label(TranslatorFormattedStringExtensions.Translate("DS_CostMultiplier", NamedArgument.op_Implicit(FormatMultiplier(costMultiplier))), -1f, (string)null);
			costMultiplier = MultiplierSlider(val, costMultiplier, ref changed);
			((Listing)val).Gap(6f);
			val.Label(Translator.Translate("DS_CostMultiplierDesc"), -1f, (string)null);
			((Listing)val).Gap(18f);
			val.Label(TranslatorFormattedStringExtensions.Translate("DS_PowerMultiplier", NamedArgument.op_Implicit(FormatMultiplier(powerMultiplier))), -1f, (string)null);
			powerMultiplier = MultiplierSlider(val, powerMultiplier, ref changed);
			((Listing)val).Gap(6f);
			val.Label(Translator.Translate("DS_PowerMultiplierDesc"), -1f, (string)null);
			((Listing)val).Gap(18f);
			val.Label(TranslatorFormattedStringExtensions.Translate("DS_ResearchMultiplier", NamedArgument.op_Implicit(FormatMultiplier(researchMultiplier))), -1f, (string)null);
			researchMultiplier = MultiplierSlider(val, researchMultiplier, ref changed);
			((Listing)val).Gap(6f);
			val.Label(Translator.Translate("DS_ResearchMultiplierDesc"), -1f, (string)null);
			((Listing)val).Gap(24f);
			Text.Font = (GameFont)2;
			val.Label(Translator.Translate("DS_AccessSettings"), -1f, (string)null);
			Text.Font = (GameFont)1;
			((Listing)val).Gap(12f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DS_AutoIngestToggle")), ref autoIngestEnabled, TaggedString.op_Implicit(Translator.Translate("DS_AutoIngestToggleDesc")), 0f, 1f);
			((Listing)val).Gap(6f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DS_AutoIngestYieldToggle")), ref autoIngestYieldToJobs, TaggedString.op_Implicit(Translator.Translate("DS_AutoIngestYieldToggleDesc")), 0f, 1f);
			((Listing)val).Gap(6f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DS_BackpackMechToggle")), ref backpackForMechanoids, TaggedString.op_Implicit(Translator.Translate("DS_BackpackMechToggleDesc")), 0f, 1f);
			((Listing)val).Gap(6f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DS_ConcurrentDeliveryToggle")), ref concurrentCoreDelivery, TaggedString.op_Implicit(Translator.Translate("DS_ConcurrentDeliveryToggleDesc")), 0f, 1f);
			((Listing)val).Gap(6f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DS_CraftAnyBenchToggle")), ref craftAnyBench, TaggedString.op_Implicit(Translator.Translate("DS_CraftAnyBenchToggleDesc")), 0f, 1f);
			((Listing)val).Gap(6f);
			val.Label(TranslatorFormattedStringExtensions.Translate("DS_CraftScanRadius", NamedArgument.op_Implicit(craftScanRadius), NamedArgument.op_Implicit(craftScanRadius * 2 + 1)), -1f, (string)null);
			float num = craftScanRadius;
			num = val.Slider(num, 2f, 20f);
			craftScanRadius = Mathf.Clamp(Mathf.RoundToInt(num), 2, 20);
			((Listing)val).Gap(6f);
			val.Label(TranslatorFormattedStringExtensions.Translate("DS_CraftExpandDepth", NamedArgument.op_Implicit(craftExpandDepth)), -1f, (string)null);
			float num2 = craftExpandDepth;
			num2 = val.Slider(num2, 1f, 3f);
			craftExpandDepth = Mathf.Clamp(Mathf.RoundToInt(num2), 1, 3);
			val.Label(Translator.Translate("DS_CraftExpandDepthDesc"), -1f, (string)null);
			((Listing)val).Gap(4f);
			val.Label(TranslatorFormattedStringExtensions.Translate("DS_CraftExpandWork", NamedArgument.op_Implicit(Mathf.RoundToInt(craftExpandWorkFactor * 100f))), -1f, (string)null);
			float num3 = craftExpandWorkFactor;
			num3 = val.Slider(num3, 0f, 1f);
			craftExpandWorkFactor = Mathf.Clamp01(num3);
			val.Label(Translator.Translate("DS_CraftExpandWorkDesc"), -1f, (string)null);
			((Listing)val).Gap(6f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DS_PerfToggle")), ref perfOptimizationsEnabled, TaggedString.op_Implicit(Translator.Translate("DS_PerfToggleDesc")), 0f, 1f);
			((Listing)val).Gap(6f);
			bool flag = noWorkFailure;
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DS_NoWorkFailureToggle")), ref noWorkFailure, TaggedString.op_Implicit(Translator.Translate("DS_NoWorkFailureToggleDesc")), 0f, 1f);
			if (flag != noWorkFailure)
			{
				WorkFailureTuner.Apply();
			}
			((Listing)val).Gap(6f);
			val.Label(TranslatorFormattedStringExtensions.Translate("DS_WorkerBudget", NamedArgument.op_Implicit(workerCompletionsPerTick)), -1f, (string)null);
			float num4 = workerCompletionsPerTick;
			num4 = val.Slider(num4, 0f, 120f);
			workerCompletionsPerTick = Mathf.RoundToInt(num4);
			val.Label(Translator.Translate("DS_WorkerBudgetDesc"), -1f, (string)null);
			((Listing)val).Gap(6f);
			val.Label(TranslatorFormattedStringExtensions.Translate("DS_FleckBudget", NamedArgument.op_Implicit(fleckBudgetPerFrame)), -1f, (string)null);
			float num5 = fleckBudgetPerFrame;
			num5 = val.Slider(num5, 0f, 5000f);
			fleckBudgetPerFrame = Mathf.RoundToInt(num5);
			val.Label(Translator.Translate("DS_FleckBudgetDesc"), -1f, (string)null);
			((Listing)val).Gap(24f);
			Text.Font = (GameFont)2;
			val.Label(Translator.Translate("DS_DebugSettings"), -1f, (string)null);
			Text.Font = (GameFont)1;
			((Listing)val).Gap(12f);
			val.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("DS_EnableDebugLog")), ref enableDebugLog, TaggedString.op_Implicit(Translator.Translate("DS_EnableDebugLogDesc")), 0f, 1f);
			((Listing)val).End();
			if (changed)
			{
				DefMultipliers.ApplyAll();
			}
		}

		private static float MultiplierSlider(Listing_Standard listing, float current, ref bool changed)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			int num = NearestLadderIndex(current);
			int num2 = Mathf.Clamp(Mathf.RoundToInt(Widgets.HorizontalSlider(((Listing)listing).GetRect(22f, 1f), (float)num, 0f, (float)(MultiplierLadder.Length - 1), true, (string)null, (string)null, (string)null, 1f)), 0, MultiplierLadder.Length - 1);
			float num3 = MultiplierLadder[num2];
			if (!Mathf.Approximately(num3, current))
			{
				changed = true;
			}
			return num3;
		}

		private static int NearestLadderIndex(float value)
		{
			int result = 0;
			float num = float.MaxValue;
			for (int i = 0; i < MultiplierLadder.Length; i++)
			{
				float num2 = Mathf.Abs(MultiplierLadder[i] - value);
				if (num2 < num)
				{
					num = num2;
					result = i;
				}
			}
			return result;
		}

		private static string FormatMultiplier(float mult)
		{
			return mult.ToString("0.###", CultureInfo.InvariantCulture) + "×";
		}
	}
	[StaticConstructorOnStartup]
	internal static class DefMultipliers
	{
		private sealed class CostSnapshot
		{
			public int[] counts;

			public int stuff;

			public int[] diffCounts;

			public int diffStuff;
		}

		private sealed class ResearchSnapshot
		{
			public float baseCost;

			public float knowledgeCost;
		}

		private static readonly Dictionary<BuildableDef, CostSnapshot> costSnapshots;

		private static readonly Dictionary<ResearchProjectDef, ResearchSnapshot> researchSnapshots;

		private static bool captured;

		static DefMultipliers()
		{
			costSnapshots = new Dictionary<BuildableDef, CostSnapshot>();
			researchSnapshots = new Dictionary<ResearchProjectDef, ResearchSnapshot>();
			ApplyAll();
		}

		public static void ApplyAll()
		{
			try
			{
				Capture();
				ApplyCost(DigitalStorageSettings.costMultiplier);
				ApplyResearch(DigitalStorageSettings.researchMultiplier);
				RefreshPower();
			}
			catch (Exception ex)
			{
				Log.Error("[DigitalStorage] 倍率设置施加失败（本次改动未生效）：" + ex);
			}
		}

		private static void Capture()
		{
			if (captured)
			{
				return;
			}
			List<ThingDef> allDefsListForReading = DefDatabase<ThingDef>.AllDefsListForReading;
			if (allDefsListForReading == null || allDefsListForReading.Count == 0)
			{
				return;
			}
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				CaptureCost((BuildableDef)(object)allDefsListForReading[i]);
			}
			List<TerrainDef> allDefsListForReading2 = DefDatabase<TerrainDef>.AllDefsListForReading;
			if (allDefsListForReading2 != null)
			{
				for (int j = 0; j < allDefsListForReading2.Count; j++)
				{
					CaptureCost((BuildableDef)(object)allDefsListForReading2[j]);
				}
			}
			List<ResearchProjectDef> allDefsListForReading3 = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
			if (allDefsListForReading3 != null)
			{
				for (int k = 0; k < allDefsListForReading3.Count; k++)
				{
					ResearchProjectDef val = allDefsListForReading3[k];
					if (val != null && (!(val.baseCost <= 0f) || !(val.knowledgeCost <= 0f)))
					{
						researchSnapshots[val] = new ResearchSnapshot
						{
							baseCost = val.baseCost,
							knowledgeCost = val.knowledgeCost
						};
					}
				}
			}
			captured = true;
		}

		private static void CaptureCost(BuildableDef def)
		{
			if (def == null)
			{
				return;
			}
			CostSnapshot costSnapshot = new CostSnapshot();
			bool flag = false;
			if (def.costList != null && def.costList.Count > 0)
			{
				costSnapshot.counts = new int[def.costList.Count];
				for (int i = 0; i < def.costList.Count; i++)
				{
					costSnapshot.counts[i] = ((def.costList[i] != null) ? def.costList[i].count : 0);
				}
				flag = true;
			}
			if (def.costStuffCount > 0)
			{
				costSnapshot.stuff = def.costStuffCount;
				flag = true;
			}
			if (def.costListForDifficulty != null)
			{
				List<ThingDefCountClass> costList = def.costListForDifficulty.costList;
				if (costList != null && costList.Count > 0)
				{
					costSnapshot.diffCounts = new int[costList.Count];
					for (int j = 0; j < costList.Count; j++)
					{
						costSnapshot.diffCounts[j] = ((costList[j] != null) ? costList[j].count : 0);
					}
					flag = true;
				}
				if (def.costListForDifficulty.costStuffCount > 0)
				{
					costSnapshot.diffStuff = def.costListForDifficulty.costStuffCount;
					flag = true;
				}
			}
			if (flag)
			{
				costSnapshots[def] = costSnapshot;
			}
		}

		private static void ApplyCost(float mult)
		{
			foreach (KeyValuePair<BuildableDef, CostSnapshot> costSnapshot in costSnapshots)
			{
				BuildableDef key = costSnapshot.Key;
				CostSnapshot value = costSnapshot.Value;
				SetCounts(key.costList, value.counts, mult);
				if (value.stuff > 0)
				{
					key.costStuffCount = Scaled(value.stuff, mult);
				}
				if (key.costListForDifficulty != null)
				{
					SetCounts(key.costListForDifficulty.costList, value.diffCounts, mult);
					if (value.diffStuff > 0)
					{
						key.costListForDifficulty.costStuffCount = Scaled(value.diffStuff, mult);
					}
				}
			}
		}

		private static void SetCounts(List<ThingDefCountClass> list, int[] baseline, float mult)
		{
			if (list == null || baseline == null)
			{
				return;
			}
			int num = Mathf.Min(list.Count, baseline.Length);
			for (int i = 0; i < num; i++)
			{
				if (list[i] != null)
				{
					list[i].count = Scaled(baseline[i], mult);
				}
			}
		}

		private static void ApplyResearch(float mult)
		{
			foreach (KeyValuePair<ResearchProjectDef, ResearchSnapshot> researchSnapshot in researchSnapshots)
			{
				ResearchProjectDef key = researchSnapshot.Key;
				if (key != null)
				{
					if (researchSnapshot.Value.baseCost > 0f)
					{
						key.baseCost = ScaledFloat(researchSnapshot.Value.baseCost, mult);
					}
					if (researchSnapshot.Value.knowledgeCost > 0f)
					{
						key.knowledgeCost = ScaledFloat(researchSnapshot.Value.knowledgeCost, mult);
					}
				}
			}
		}

		private static int Scaled(int original, float mult)
		{
			if (original <= 0)
			{
				return original;
			}
			return Mathf.Max(1, Mathf.RoundToInt((float)original * mult));
		}

		private static float ScaledFloat(float original, float mult)
		{
			if (original <= 0f)
			{
				return original;
			}
			return Mathf.Max(1f, original * mult);
		}

		private static void RefreshPower()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Invalid comparison between Unknown and I4
			if ((int)Current.ProgramState != 2)
			{
				return;
			}
			List<Map> maps = Find.Maps;
			if (maps == null)
			{
				return;
			}
			for (int i = 0; i < maps.Count; i++)
			{
				Map val = maps[i];
				if (val == null || val.listerThings == null)
				{
					continue;
				}
				List<Thing> list = val.listerThings.ThingsInGroup((ThingRequestGroup)75);
				for (int j = 0; j < list.Count; j++)
				{
					Thing obj = list[j];
					CompPowerTrader val2 = ((obj != null) ? ThingCompUtility.TryGetComp<CompPowerTrader>(obj) : null);
					if (val2 != null)
					{
						((CompPower)val2).SetUpPowerVars();
					}
				}
			}
		}
	}
	[StaticConstructorOnStartup]
	internal static class WorkFailureTuner
	{
		private sealed class SkillSnapshot
		{
			public SkillNeed_Direct need;

			public float[] values;
		}

		private sealed class StatSnapshot
		{
			public readonly List<SkillSnapshot> skills = new List<SkillSnapshot>();

			public readonly List<PawnCapacityFactor> capacities = new List<PawnCapacityFactor>();

			public readonly List<float> capacityWeights = new List<float>();

			public bool IsEmpty
			{
				get
				{
					if (skills.Count == 0)
					{
						return capacities.Count == 0;
					}
					return false;
				}
			}
		}

		private static readonly Dictionary<StatDef, StatSnapshot> snapshots;

		private static bool captured;

		static WorkFailureTuner()
		{
			snapshots = new Dictionary<StatDef, StatSnapshot>();
			Apply();
		}

		public static void Apply()
		{
			try
			{
				Capture();
				if (!captured)
				{
					return;
				}
				bool noWorkFailure = DigitalStorageSettings.noWorkFailure;
				foreach (KeyValuePair<StatDef, StatSnapshot> snapshot in snapshots)
				{
					ApplyTo(snapshot.Value, noWorkFailure);
				}
				if (DigitalStorageSettings.enableDebugLog)
				{
					Log.Warning("[DigitalStorage] 工作失败率设置 = " + (noWorkFailure ? "0（不会失败）" : "原版") + "，已施加 " + snapshots.Count + " 条 stat。");
				}
			}
			catch (Exception ex)
			{
				Log.Error("[DigitalStorage] 工作失败率设置施加失败（本次改动未生效）：" + ex);
			}
		}

		private static void Capture()
		{
			if (!captured && StatDefOf.PlantHarvestYield != null)
			{
				CaptureStat(StatDefOf.PlantHarvestYield);
				CaptureStat(StatDefOf.DrugHarvestYield);
				CaptureStat(StatDefOf.ConstructSuccessChance);
				CaptureStat(StatDefOf.FixBrokenDownBuildingSuccessChance);
				captured = snapshots.Count > 0;
				if (!captured)
				{
					Log.Warning("[DigitalStorage] 找不到任何工作失败类 stat，工作失败率设置本次无效。");
				}
			}
		}

		private static void CaptureStat(StatDef def)
		{
			if (def == null || snapshots.ContainsKey(def))
			{
				return;
			}
			StatSnapshot statSnapshot = new StatSnapshot();
			if (def.skillNeedFactors != null)
			{
				for (int i = 0; i < def.skillNeedFactors.Count; i++)
				{
					SkillNeed obj = def.skillNeedFactors[i];
					SkillNeed_Direct val = (SkillNeed_Direct)(object)((obj is SkillNeed_Direct) ? obj : null);
					if (val != null && val.valuesPerLevel != null && val.valuesPerLevel.Count != 0)
					{
						float[] array = new float[val.valuesPerLevel.Count];
						for (int j = 0; j < array.Length; j++)
						{
							array[j] = val.valuesPerLevel[j];
						}
						statSnapshot.skills.Add(new SkillSnapshot
						{
							need = val,
							values = array
						});
					}
				}
			}
			if (def.capacityFactors != null)
			{
				for (int k = 0; k < def.capacityFactors.Count; k++)
				{
					PawnCapacityFactor val2 = def.capacityFactors[k];
					if (val2 != null)
					{
						statSnapshot.capacities.Add(val2);
						statSnapshot.capacityWeights.Add(val2.weight);
					}
				}
			}
			if (!statSnapshot.IsEmpty || def == StatDefOf.DrugHarvestYield)
			{
				snapshots[def] = statSnapshot;
			}
		}

		private static void ApplyTo(StatSnapshot snap, bool on)
		{
			for (int i = 0; i < snap.skills.Count; i++)
			{
				SkillSnapshot skillSnapshot = snap.skills[i];
				if (skillSnapshot.need != null && skillSnapshot.need.valuesPerLevel != null)
				{
					int num = Mathf.Min(skillSnapshot.need.valuesPerLevel.Count, skillSnapshot.values.Length);
					for (int j = 0; j < num; j++)
					{
						skillSnapshot.need.valuesPerLevel[j] = (on ? Mathf.Max(1f, skillSnapshot.values[j]) : skillSnapshot.values[j]);
					}
				}
			}
			for (int k = 0; k < snap.capacities.Count; k++)
			{
				PawnCapacityFactor val = snap.capacities[k];
				if (val != null)
				{
					val.weight = (on ? 0f : snap.capacityWeights[k]);
				}
			}
		}
	}
}
namespace DigitalStorage.Performance
{
	[HarmonyPatch(typeof(SelectionDrawer), "DrawSelectionBracketFor", new Type[]
	{
		typeof(object),
		typeof(Material)
	})]
	internal static class Patch_SelectionDrawer_BracketCull
	{
		private const int ThingMargin = 12;

		private const int CellMargin = 2;

		private static bool Prefix(object obj)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			if (!DigitalStorageSettings.perfOptimizationsEnabled)
			{
				return true;
			}
			if (Find.CurrentMap == null)
			{
				return true;
			}
			CellRect currentViewRect = Find.CameraDriver.CurrentViewRect;
			Thing val = (Thing)((obj is Thing) ? obj : null);
			if (val != null)
			{
				if (!val.Spawned)
				{
					return true;
				}
				Vector3 drawPos = (val.SpawnedParentOrMe ?? val).DrawPos;
				IntVec3 val2 = default(IntVec3);
				((IntVec3)(ref val2))..ctor(Mathf.RoundToInt(drawPos.x), 0, Mathf.RoundToInt(drawPos.z));
				CellRect val3 = ((CellRect)(ref currentViewRect)).ExpandedBy(12);
				return ((CellRect)(ref val3)).Contains(val2);
			}
			Zone val4 = (Zone)((obj is Zone) ? obj : null);
			if (val4 != null)
			{
				return AnyCellInView(val4.Cells, currentViewRect);
			}
			Plan val5 = (Plan)((obj is Plan) ? obj : null);
			if (val5 != null)
			{
				return AnyCellInView(val5.Cells, currentViewRect);
			}
			return true;
		}

		private static bool AnyCellInView(List<IntVec3> cells, CellRect view)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			CellRect val = ((CellRect)(ref view)).ExpandedBy(2);
			for (int i = 0; i < cells.Count; i++)
			{
				if (((CellRect)(ref val)).Contains(cells[i]))
				{
					return true;
				}
			}
			return false;
		}
	}
	[HarmonyPatch(typeof(DesignationManager), "DrawDesignations")]
	internal static class Patch_DesignationManager_BatchDraw
	{
		internal const int MatrixPerBatch = 1023;

		private static readonly ConditionalWeakTable<DesignationManager, MapCache> caches = new ConditionalWeakTable<DesignationManager, MapCache>();

		private static MaterialPropertyBlock block;

		internal static bool Failed;

		internal static bool Active
		{
			get
			{
				if (!Failed && DigitalStorageSettings.perfOptimizationsEnabled)
				{
					return SystemInfo.supportsInstancing;
				}
				return false;
			}
		}

		internal static MapCache CacheFor(DesignationManager mgr)
		{
			if (!caches.TryGetValue(mgr, out var value))
			{
				value = new MapCache();
				caches.Add(mgr, value);
			}
			return value;
		}

		internal static bool IsBatched(DesignationDef def)
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Invalid comparison between Unknown and I4
			if (def != null && (int)def.targetType == 1)
			{
				return def.shouldBatchDraw;
			}
			return false;
		}

		private static bool Prefix(DesignationManager __instance)
		{
			if (__instance == null)
			{
				return true;
			}
			if (!Active)
			{
				DevDrawProfiler.Bump("批绘未生效", 1);
				return true;
			}
			try
			{
				Draw(__instance);
				DevDrawProfiler.Bump("批绘接管", 1);
				return false;
			}
			catch (Exception ex)
			{
				Failed = true;
				Log.ErrorOnce("[DigitalStorage] 标记批绘优化异常，已永久交回原版路径：" + ex, 7711233);
				return true;
			}
		}

		internal static Matrix4x4 MatrixOf(Designation des)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			return Matrix4x4.TRS(des.DrawLoc(), Quaternion.identity, Vector3.one);
		}

		private static void Draw(DesignationManager mgr)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Expected O, but got Unknown
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			MapCache mapCache = CacheFor(mgr);
			CellRect currentViewRect = Find.CameraDriver.CurrentViewRect;
			CellRect val = ((CellRect)(ref currentViewRect)).ExpandedBy(3);
			if (block == null)
			{
				block = new MaterialPropertyBlock();
			}
			List<DesignationDef> allDefsListForReading = DefDatabase<DesignationDef>.AllDefsListForReading;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				DesignationDef val2 = allDefsListForReading[i];
				List<Designation> list = mgr.designationsByDef[val2];
				if (list == null || list.Count == 0)
				{
					continue;
				}
				if (!IsBatched(val2))
				{
					for (int j = 0; j < list.Count; j++)
					{
						Designation val3 = list[j];
						if ((!((LocalTargetInfo)(ref val3.target)).HasThing || ((LocalTargetInfo)(ref val3.target)).Thing.Map == mgr.map) && ((CellRect)(ref val)).Contains(((LocalTargetInfo)(ref val3.target)).Cell))
						{
							val3.DesignationDraw();
						}
					}
					continue;
				}
				BatchCache batchCache = mapCache.For(val2);
				if (batchCache.NeedResync || batchCache.Count != list.Count)
				{
					batchCache.Resync(list);
				}
				if (batchCache.Count == 0)
				{
					continue;
				}
				Material iconMat = val2.iconMat;
				if (!((Object)(object)iconMat == (Object)null))
				{
					iconMat.enableInstancing = true;
					int count = batchCache.Count;
					int num = count / 1023;
					for (int k = 0; k < num; k++)
					{
						Graphics.DrawMeshInstanced(MeshPool.plane10, 0, iconMat, batchCache.matrices[k], 1023, block, (ShadowCastingMode)0, true, 0);
					}
					int num2 = count % 1023;
					if (num2 > 0)
					{
						Graphics.DrawMeshInstanced(MeshPool.plane10, 0, iconMat, batchCache.matrices[num], num2, block, (ShadowCastingMode)0, true, 0);
					}
				}
			}
		}
	}
	internal sealed class MapCache
	{
		private readonly BatchCache[] byDefIndex = new BatchCache[DefDatabase<DesignationDef>.DefCount];

		public BatchCache For(DesignationDef def)
		{
			BatchCache batchCache = byDefIndex[((Def)def).index];
			if (batchCache == null)
			{
				batchCache = new BatchCache();
				byDefIndex[((Def)def).index] = batchCache;
			}
			return batchCache;
		}

		public BatchCache Get(DesignationDef def)
		{
			return byDefIndex[((Def)def).index];
		}
	}
	internal sealed class BatchCache
	{
		public int Count;

		public bool NeedResync;

		public readonly List<Matrix4x4[]> matrices = new List<Matrix4x4[]>();

		private readonly List<Designation> order = new List<Designation>();

		private readonly Dictionary<Designation, int> slot = new Dictionary<Designation, int>();

		public void OnAdded(DesignationManager mgr, Designation des)
		{
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			if (NeedResync)
			{
				return;
			}
			try
			{
				List<Designation> list = mgr.designationsByDef[des.def];
				if (list.Count == 0 || list[list.Count - 1] != des || Count != list.Count - 1)
				{
					NeedResync = true;
					return;
				}
				Ensure(Count + 1);
				matrices[Count / 1023][Count % 1023] = Patch_DesignationManager_BatchDraw.MatrixOf(des);
				order.Add(des);
				slot[des] = Count;
				Count++;
			}
			catch (Exception ex)
			{
				Patch_DesignationManager_BatchDraw.Failed = true;
				Log.ErrorOnce("[DigitalStorage] 标记批绘增量（增）异常，已停用优化：" + ex, 7711234);
			}
		}

		public void OnRemoved(DesignationManager mgr, Designation des)
		{
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			if (NeedResync)
			{
				return;
			}
			try
			{
				if (!slot.TryGetValue(des, out var value) || value < 0 || value >= Count || order[value] != des)
				{
					NeedResync = true;
					return;
				}
				int num = Count - 1;
				if (value != num)
				{
					Designation val = order[num];
					order[value] = val;
					slot[val] = value;
					matrices[value / 1023][value % 1023] = matrices[num / 1023][num % 1023];
				}
				order.RemoveAt(num);
				slot.Remove(des);
				Count = num;
			}
			catch (Exception ex)
			{
				Patch_DesignationManager_BatchDraw.Failed = true;
				Log.ErrorOnce("[DigitalStorage] 标记批绘增量（删）异常，已停用优化：" + ex, 7711235);
			}
		}

		public void Resync(List<Designation> list)
		{
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			DevDrawProfiler.Bump("重同步", 1);
			order.Clear();
			slot.Clear();
			Ensure(list.Count);
			for (int i = 0; i < list.Count; i++)
			{
				Designation val = list[i];
				order.Add(val);
				slot[val] = i;
				matrices[i / 1023][i % 1023] = Patch_DesignationManager_BatchDraw.MatrixOf(val);
			}
			Count = list.Count;
			NeedResync = false;
		}

		private void Ensure(int needed)
		{
			while (matrices.Count * 1023 < needed)
			{
				matrices.Add((Matrix4x4[])(object)new Matrix4x4[1023]);
			}
		}
	}
	[HarmonyPatch(typeof(DesignationManager), "IndexDesignation")]
	internal static class Patch_DesignationManager_BatchDraw_Add
	{
		private static void Postfix(DesignationManager __instance, Designation designation)
		{
			if (Patch_DesignationManager_BatchDraw.Active && designation != null && Patch_DesignationManager_BatchDraw.IsBatched(designation.def))
			{
				Patch_DesignationManager_BatchDraw.CacheFor(__instance).For(designation.def).OnAdded(__instance, designation);
			}
		}
	}
	[HarmonyPatch(typeof(DesignationManager), "RemoveDesignation")]
	internal static class Patch_DesignationManager_BatchDraw_Remove
	{
		private static void Prefix(DesignationManager __instance, Designation des)
		{
			if (Patch_DesignationManager_BatchDraw.Active && des != null && Patch_DesignationManager_BatchDraw.IsBatched(des.def))
			{
				Patch_DesignationManager_BatchDraw.CacheFor(__instance).Get(des.def)?.OnRemoved(__instance, des);
			}
		}
	}
	[HarmonyPatch(typeof(FleckMaker), "ThrowMetaPuffs", new Type[] { typeof(TargetInfo) })]
	internal static class Patch_FleckMaker_MetaPuffThrottle
	{
		private const int MaxCallsPerTick = 16;

		private static int lastTick = -1;

		private static int callsThisTick;

		private static bool Prefix()
		{
			if (!DigitalStorageSettings.perfOptimizationsEnabled)
			{
				return true;
			}
			if (Find.TickManager == null || Find.TickManager.Paused)
			{
				return true;
			}
			int ticksGame = GenTicks.TicksGame;
			if (ticksGame != lastTick)
			{
				lastTick = ticksGame;
				callsThisTick = 0;
			}
			if (callsThisTick >= 16)
			{
				DevDrawProfiler.Bump("气喷拦", 1);
				return false;
			}
			callsThisTick++;
			DevDrawProfiler.Bump("气喷过", 1);
			return true;
		}
	}
	[HarmonyPatch(typeof(FleckManager), "CreateFleck")]
	internal static class Patch_FleckManager_CreationBudget
	{
		private static int frame = -1;

		private static int createdThisFrame;

		private static bool Prefix()
		{
			int fleckBudgetPerFrame = DigitalStorageSettings.fleckBudgetPerFrame;
			if (fleckBudgetPerFrame <= 0)
			{
				return true;
			}
			if (!DigitalStorageSettings.perfOptimizationsEnabled)
			{
				return true;
			}
			int frameCount = Time.frameCount;
			if (frameCount != frame)
			{
				frame = frameCount;
				createdThisFrame = 0;
			}
			if (createdThisFrame >= fleckBudgetPerFrame)
			{
				DevDrawProfiler.Bump("造泡拦", 1);
				return false;
			}
			createdThisFrame++;
			DevDrawProfiler.Bump("造泡", 1);
			return true;
		}
	}
	internal static class DevDrawProfiler
	{
		private const int WindowFrames = 180;

		private static string buildStamp;

		private static readonly string[] Keys = new string[20]
		{
			"Ticks", "MapUpd", "DSWork", "HaulSweep", "DS-valid", "DS-scan", "DS-work", "DS-finish", "DS-visual", "DS-allocMB",
			"ScanSet", "Drops", "DropsDirect", "MapMesh", "DynThings", "Designations", "Overlays", "Motes", "Flecks", "SelDraw"
		};

		private static readonly Dictionary<string, double> frameMs = new Dictionary<string, double>();

		private static readonly Dictionary<string, double> windowMs = new Dictionary<string, double>();

		private static readonly Dictionary<string, double> windowMax = new Dictionary<string, double>();

		private static readonly Dictionary<string, int> counters = new Dictionary<string, int>();

		private static readonly Dictionary<string, int> frameCounts = new Dictionary<string, int>();

		private static readonly Dictionary<string, double> windowCounts = new Dictionary<string, double>();

		private static int frameStamp = -1;

		private static int frames;

		private static double frameTotal;

		private static double windowTotal;

		private static double windowPeak;

		private static double windowDelta;

		private static double windowDeltaPeak;

		private static long allocated;

		private static long lastHeap = -1L;

		private static long heapStart;

		private static int gc0Start;

		private static int gc1Start;

		private static int gc2Start;

		internal static bool Enabled
		{
			get
			{
				if (Prefs.DevMode)
				{
					return DigitalStorageSettings.enableDebugLog;
				}
				return false;
			}
		}

		internal static long Now => Stopwatch.GetTimestamp();

		private static string BuildStamp()
		{
			if (buildStamp != null)
			{
				return buildStamp;
			}
			try
			{
				buildStamp = File.GetLastWriteTime(typeof(DevDrawProfiler).Assembly.Location).ToString("MMdd-HHmm", CultureInfo.InvariantCulture);
			}
			catch
			{
				buildStamp = "?";
			}
			return buildStamp;
		}

		internal static double Ms(long since)
		{
			return (double)(Stopwatch.GetTimestamp() - since) * 1000.0 / (double)Stopwatch.Frequency;
		}

		internal static void Add(string key, double ms)
		{
			if (Enabled)
			{
				CheckFrame();
				frameMs.TryGetValue(key, out var value);
				frameMs[key] = value + ms;
				frameTotal += ms;
			}
		}

		internal static void Counter(string key, int value)
		{
			if (Enabled)
			{
				CheckFrame();
				counters[key] = value;
			}
		}

		internal static void CounterMax(string key, int value)
		{
			if (Enabled)
			{
				CheckFrame();
				if (!counters.TryGetValue(key, out var value2) || value2 < value)
				{
					counters[key] = value;
				}
			}
		}

		internal static long Stamp()
		{
			if (!Enabled)
			{
				return 0L;
			}
			return Stopwatch.GetTimestamp();
		}

		internal static void Mark(string key, long since)
		{
			if (since != 0L && Enabled)
			{
				Add(key, Ms(since));
			}
		}

		internal static long HeapStamp()
		{
			if (!Enabled)
			{
				return 0L;
			}
			return GC.GetTotalMemory(forceFullCollection: false);
		}

		internal static void MarkAlloc(string key, long heapSince)
		{
			if (heapSince != 0L && Enabled)
			{
				long totalMemory = GC.GetTotalMemory(forceFullCollection: false);
				if (totalMemory > heapSince)
				{
					Add(key, (double)(totalMemory - heapSince) / 1048576.0);
				}
			}
		}

		internal static void Bump(string key, int delta)
		{
			if (Enabled)
			{
				CheckFrame();
				frameCounts.TryGetValue(key, out var value);
				frameCounts[key] = value + delta;
			}
		}

		private static void CheckFrame()
		{
			if (frameStamp != Time.frameCount)
			{
				if (frameStamp < 0)
				{
					ResetWindow();
				}
				else
				{
					FlushFrame();
				}
				frameStamp = Time.frameCount;
				frameMs.Clear();
				frameCounts.Clear();
				frameTotal = 0.0;
			}
		}

		private static void FlushFrame()
		{
			frames++;
			windowTotal += frameTotal;
			if (frameTotal > windowPeak)
			{
				windowPeak = frameTotal;
			}
			double num = (double)Time.unscaledDeltaTime * 1000.0;
			windowDelta += num;
			if (num > windowDeltaPeak)
			{
				windowDeltaPeak = num;
			}
			for (int i = 0; i < Keys.Length; i++)
			{
				string key = Keys[i];
				if (frameMs.TryGetValue(key, out var value))
				{
					windowMs.TryGetValue(key, out var value2);
					windowMs[key] = value2 + value;
					windowMax.TryGetValue(key, out var value3);
					if (value > value3)
					{
						windowMax[key] = value;
					}
				}
			}
			long totalMemory = GC.GetTotalMemory(forceFullCollection: false);
			if (lastHeap >= 0 && totalMemory > lastHeap)
			{
				allocated += totalMemory - lastHeap;
			}
			lastHeap = totalMemory;
			foreach (KeyValuePair<string, int> frameCount in frameCounts)
			{
				windowCounts.TryGetValue(frameCount.Key, out var value4);
				windowCounts[frameCount.Key] = value4 + (double)frameCount.Value;
			}
			if (frames >= 180)
			{
				LogWindow();
				ResetWindow();
			}
		}

		private static void ResetWindow()
		{
			frames = 0;
			windowTotal = 0.0;
			windowPeak = 0.0;
			windowDelta = 0.0;
			windowDeltaPeak = 0.0;
			windowMs.Clear();
			windowMax.Clear();
			windowCounts.Clear();
			allocated = 0L;
			lastHeap = GC.GetTotalMemory(forceFullCollection: false);
			heapStart = lastHeap;
			gc0Start = GC.CollectionCount(0);
			gc1Start = GC.CollectionCount(1);
			gc2Start = GC.CollectionCount(2);
		}

		private static double KeyAvg(string k)
		{
			if (!windowMs.TryGetValue(k, out var value) || frames <= 0)
			{
				return 0.0;
			}
			return value / (double)frames;
		}

		private static double KeyMax(string k)
		{
			if (!windowMax.TryGetValue(k, out var value))
			{
				return 0.0;
			}
			return value;
		}

		private static void LogWindow()
		{
			double num = WindowDeltaAvg();
			double num2 = KeyAvg("Ticks");
			double num3 = KeyAvg("SelDraw");
			double num4 = KeyAvg("MapMesh") + KeyAvg("DynThings") + KeyAvg("Designations") + KeyAvg("Overlays") + KeyAvg("Motes") + KeyAvg("Flecks");
			double num5 = num - num2 - num3;
			if (num5 < 0.0)
			{
				num5 = 0.0;
			}
			double num6 = num2 - num4;
			if (num6 < 0.0)
			{
				num6 = 0.0;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("[DS-Draw ").Append(BuildStamp()).Append("] 帧=")
				.Append(frames);
			stringBuilder.Append(" 帧间隔 ").Append(F(num)).Append("(峰 ")
				.Append(F(windowDeltaPeak))
				.Append(')');
			stringBuilder.Append(" | 刻度 ").Append(F(num2)).Append("(峰 ")
				.Append(F(KeyMax("Ticks")))
				.Append(')');
			if (counters.TryGetValue("tick/帧", out var value))
			{
				stringBuilder.Append("[tick/帧=").Append(value).Append(']');
			}
			stringBuilder.Append(" | 刻度内绘制 ").Append(F(num4));
			stringBuilder.Append(" 刻度内非绘制 ").Append(F(num6));
			stringBuilder.Append(" 帧内其他 ").Append(F(num5));
			stringBuilder.Append(" ||");
			for (int i = 0; i < Keys.Length; i++)
			{
				string text = Keys[i];
				if (windowMs.TryGetValue(text, out var value2) && !(value2 <= 0.0))
				{
					stringBuilder.Append(' ').Append(text).Append(' ')
						.Append(F(value2 / (double)frames))
						.Append('/')
						.Append(F(KeyMax(text)));
				}
			}
			stringBuilder.Append(" || GC0+").Append(GC.CollectionCount(0) - gc0Start);
			stringBuilder.Append(" GC1+").Append(GC.CollectionCount(1) - gc1Start);
			stringBuilder.Append(" GC2+").Append(GC.CollectionCount(2) - gc2Start);
			stringBuilder.Append(" 分配").Append(F((double)allocated / 1048576.0)).Append("MB");
			stringBuilder.Append(" 堆Δ").Append(F((double)(GC.GetTotalMemory(forceFullCollection: false) - heapStart) / 1048576.0)).Append("MB");
			if (counters.TryGetValue("选中", out var value3))
			{
				stringBuilder.Append(" 选中=").Append(value3);
			}
			if (counters.TryGetValue("标记", out var value4))
			{
				stringBuilder.Append(" 标记=").Append(value4);
			}
			if (counters.TryGetValue("任务", out var value5))
			{
				stringBuilder.Append(" 任务=").Append(value5);
			}
			stringBuilder.Append(" 性能开关=").Append(DigitalStorageSettings.perfOptimizationsEnabled ? 1 : 0).Append(" 直塞预算=")
				.Append(DigitalStorageSettings.workerCompletionsPerTick);
			foreach (KeyValuePair<string, double> windowCount in windowCounts)
			{
				if (frames > 0)
				{
					stringBuilder.Append(' ').Append(windowCount.Key).Append("/帧=")
						.Append(F(windowCount.Value / (double)frames));
				}
			}
			Log.Warning(stringBuilder.ToString());
		}

		private static double WindowDeltaAvg()
		{
			if (frames <= 0)
			{
				return 0.0;
			}
			return windowDelta / (double)frames;
		}

		private static string F(double v)
		{
			return v.ToString("F2", CultureInfo.InvariantCulture);
		}
	}
	[HarmonyPatch(typeof(MapDrawer), "DrawMapMesh")]
	internal static class PerfProbe_MapMesh
	{
		private static long t0;

		private static void Prefix()
		{
			t0 = (DevDrawProfiler.Enabled ? DevDrawProfiler.Now : 0);
		}

		private static void Postfix()
		{
			if (t0 != 0L)
			{
				DevDrawProfiler.Add("MapMesh", DevDrawProfiler.Ms(t0));
				t0 = 0L;
			}
		}
	}
	[HarmonyPatch(typeof(DynamicDrawManager), "DrawDynamicThings")]
	internal static class PerfProbe_DynThings
	{
		private static long t0;

		private static void Prefix()
		{
			t0 = (DevDrawProfiler.Enabled ? DevDrawProfiler.Now : 0);
		}

		private static void Postfix()
		{
			if (t0 != 0L)
			{
				DevDrawProfiler.Add("DynThings", DevDrawProfiler.Ms(t0));
				t0 = 0L;
			}
		}
	}
	[HarmonyPatch(typeof(DesignationManager), "DrawDesignations")]
	internal static class PerfProbe_Designations
	{
		private static long t0;

		[HarmonyPriority(800)]
		private static void Prefix(DesignationManager __instance)
		{
			if (DevDrawProfiler.Enabled)
			{
				t0 = DevDrawProfiler.Now;
				DevDrawProfiler.Counter("标记", CountAll(__instance));
			}
		}

		private static void Postfix()
		{
			if (t0 != 0L)
			{
				DevDrawProfiler.Add("Designations", DevDrawProfiler.Ms(t0));
				t0 = 0L;
			}
		}

		private static int CountAll(DesignationManager mgr)
		{
			if (mgr == null)
			{
				return 0;
			}
			int num = 0;
			List<DesignationDef> allDefsListForReading = DefDatabase<DesignationDef>.AllDefsListForReading;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				List<Designation> list = mgr.designationsByDef[allDefsListForReading[i]];
				if (list != null)
				{
					num += list.Count;
				}
			}
			return num;
		}
	}
	[HarmonyPatch(typeof(OverlayDrawer), "DrawAllOverlays")]
	internal static class PerfProbe_Overlays
	{
		private static long t0;

		private static void Prefix()
		{
			t0 = (DevDrawProfiler.Enabled ? DevDrawProfiler.Now : 0);
		}

		private static void Postfix()
		{
			if (t0 != 0L)
			{
				DevDrawProfiler.Add("Overlays", DevDrawProfiler.Ms(t0));
				t0 = 0L;
			}
		}
	}
	[HarmonyPatch(typeof(TemporaryThingDrawer), "Draw")]
	internal static class PerfProbe_Motes
	{
		private static long t0;

		private static void Prefix()
		{
			t0 = (DevDrawProfiler.Enabled ? DevDrawProfiler.Now : 0);
		}

		private static void Postfix()
		{
			if (t0 != 0L)
			{
				DevDrawProfiler.Add("Motes", DevDrawProfiler.Ms(t0));
				t0 = 0L;
			}
		}
	}
	[HarmonyPatch(typeof(FleckManager), "FleckManagerDraw")]
	internal static class PerfProbe_Flecks
	{
		private static long t0;

		private static void Prefix()
		{
			t0 = (DevDrawProfiler.Enabled ? DevDrawProfiler.Now : 0);
		}

		private static void Postfix()
		{
			if (t0 != 0L)
			{
				DevDrawProfiler.Add("Flecks", DevDrawProfiler.Ms(t0));
				t0 = 0L;
			}
		}
	}
	[HarmonyPatch(typeof(SelectionDrawer), "DrawSelectionOverlays")]
	internal static class PerfProbe_Selection
	{
		private static long t0;

		private static void Prefix()
		{
			if (DevDrawProfiler.Enabled)
			{
				t0 = DevDrawProfiler.Now;
				DevDrawProfiler.Counter("选中", (Find.Selector != null) ? Find.Selector.NumSelected : 0);
			}
		}

		private static void Postfix()
		{
			if (t0 != 0L)
			{
				DevDrawProfiler.Add("SelDraw", DevDrawProfiler.Ms(t0));
				t0 = 0L;
			}
		}
	}
	[HarmonyPatch(typeof(TickManager), "TickManagerUpdate")]
	internal static class PerfProbe_Ticks
	{
		private static long t0;

		private static void Prefix()
		{
			t0 = (DevDrawProfiler.Enabled ? DevDrawProfiler.Now : 0);
		}

		private static void Postfix()
		{
			if (t0 != 0L)
			{
				DevDrawProfiler.Add("Ticks", DevDrawProfiler.Ms(t0));
				TickManager tickManager = Find.TickManager;
				if (tickManager != null)
				{
					DevDrawProfiler.Counter("tick/帧", tickManager.TicksThisFrame);
				}
				t0 = 0L;
			}
		}
	}
	[HarmonyPatch(typeof(Map), "MapUpdate")]
	internal static class PerfProbe_MapUpdate
	{
		private static long t0;

		private static void Prefix()
		{
			t0 = (DevDrawProfiler.Enabled ? DevDrawProfiler.Now : 0);
		}

		private static void Postfix()
		{
			if (t0 != 0L)
			{
				DevDrawProfiler.Add("MapUpd", DevDrawProfiler.Ms(t0));
				t0 = 0L;
			}
		}
	}
	[HarmonyPatch(typeof(CompDigitalWorker), "CompTick")]
	internal static class PerfProbe_DSWorker
	{
		private static long t0;

		private static void Prefix(CompDigitalWorker __instance)
		{
			if (DevDrawProfiler.Enabled)
			{
				t0 = DevDrawProfiler.Now;
				DevDrawProfiler.CounterMax("任务", __instance.ActiveCount);
			}
		}

		private static void Postfix()
		{
			if (t0 != 0L)
			{
				DevDrawProfiler.Add("DSWork", DevDrawProfiler.Ms(t0));
				t0 = 0L;
			}
		}
	}
	[HarmonyPatch(typeof(ListerHaulables), "RecalculateAllInHaulSource", new Type[] { typeof(IHaulSource) })]
	internal static class Patch_ListerHaulables_CoreSweep
	{
		private const int FallbackInterval = 250;

		private static long t0;

		private static bool Prefix(IHaulSource source, ListerHaulables __instance)
		{
			t0 = DevDrawProfiler.Stamp();
			if (!(source is Building_StorageCore building_StorageCore))
			{
				return true;
			}
			if (!DigitalStorageSettings.perfOptimizationsEnabled)
			{
				return true;
			}
			try
			{
				if (__instance == null)
				{
					return true;
				}
				if (!((Thing)building_StorageCore).Spawned || ((Thing)building_StorageCore).Map == null)
				{
					return true;
				}
				if (((Thing)building_StorageCore).Faction != Faction.OfPlayer)
				{
					return true;
				}
				bool flag = !building_StorageCore.HaulSourceEnabled;
				if (!flag && HasStrictlyBetterDestination(building_StorageCore))
				{
					int ticksGame = GenTicks.TicksGame;
					if (ticksGame - building_StorageCore.lastHaulSweepTick < 250)
					{
						return false;
					}
					building_StorageCore.lastHaulSweepTick = ticksGame;
					return true;
				}
				ThingOwner directlyHeldThings = ((IThingHolder)source).GetDirectlyHeldThings();
				if (directlyHeldThings == null || directlyHeldThings.Count == 0)
				{
					return false;
				}
				ICollection<Thing> collection = __instance.ThingsPotentiallyNeedingHauling();
				int num = 0;
				for (int i = 0; i < directlyHeldThings.Count; i++)
				{
					Thing val = directlyHeldThings[i];
					if (val != null)
					{
						if (!flag && !building_StorageCore.Accepts(val))
						{
							return true;
						}
						collection.Remove(val);
						num++;
					}
				}
				DevDrawProfiler.Bump("核心免算件", num);
				return false;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 核心搬运源重算跳过失败，本次退回原版：" + ex, 1146307157);
				return true;
			}
		}

		private static void Postfix()
		{
			DevDrawProfiler.Mark("HaulSweep", t0);
			t0 = 0L;
		}

		private static bool HasStrictlyBetterDestination(Building_StorageCore core)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Invalid comparison between I4 and Unknown
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Invalid comparison between I4 and Unknown
			HaulDestinationManager val = ((Thing)core).Map?.haulDestinationManager;
			if (val == null)
			{
				return true;
			}
			StoragePriority val2 = (StoragePriority)0;
			StorageSettings storeSettings = core.GetStoreSettings();
			if (storeSettings != null)
			{
				val2 = storeSettings.Priority;
			}
			List<SlotGroup> allGroupsListInPriorityOrder = val.AllGroupsListInPriorityOrder;
			if (allGroupsListInPriorityOrder != null)
			{
				for (int i = 0; i < allGroupsListInPriorityOrder.Count; i++)
				{
					SlotGroup val3 = allGroupsListInPriorityOrder[i];
					if (val3 != null && val3.parent != null)
					{
						StorageSettings settings = val3.Settings;
						if (((settings != null) ? ((int)settings.Priority) : 0) <= (int)val2)
						{
							break;
						}
						if (((IHaulDestination)val3.parent).HaulDestinationEnabled)
						{
							return true;
						}
					}
				}
			}
			List<IHaulDestination> allHaulDestinationsListInPriorityOrder = val.AllHaulDestinationsListInPriorityOrder;
			if (allHaulDestinationsListInPriorityOrder != null)
			{
				for (int j = 0; j < allHaulDestinationsListInPriorityOrder.Count; j++)
				{
					IHaulDestination val4 = allHaulDestinationsListInPriorityOrder[j];
					if (val4 != null && (object)val4 != core)
					{
						StorageSettings storeSettings2 = ((IStoreSettingsParent)val4).GetStoreSettings();
						if (((storeSettings2 != null) ? ((int)storeSettings2.Priority) : 0) <= (int)val2)
						{
							break;
						}
						if (val4.HaulDestinationEnabled)
						{
							return true;
						}
					}
				}
			}
			return false;
		}
	}
}
namespace DigitalStorage.HarmonyPatches
{
	[StaticConstructorOnStartup]
	public static class HarmonyInit
	{
		internal static string PatchSummary;

		static HarmonyInit()
		{
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Expected O, but got Unknown
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Expected O, but got Unknown
			//IL_0025: Expected O, but got Unknown
			PatchSummary = "";
			Harmony val = new Harmony("DigitalStorage.HarmonyPatches");
			PatchAllIsolated(val);
			ManualInstallAll(val);
			PhinixCompatPatch.Install(val);
			PickUpAndHaulCompat.LogOnce();
			string text;
			try
			{
				text = File.GetLastWriteTime(typeof(HarmonyInit).Assembly.Location).ToString("yyyy-MM-dd HH:mm");
			}
			catch
			{
				text = "?";
			}
			Log.Warning("[DigitalStorage 4.0] Harmony ready. 构建=" + text + PatchSummary);
			if (Prefs.DevMode)
			{
				Log.Warning("[DigitalStorage] 第三方兼容挂点=" + PhinixCompatPatch.InstalledCount);
			}
		}

		private static void ManualInstallAll(Harmony harmony)
		{
			int num = 0;
			List<string> list = new List<string>();
			try
			{
				Patch_GenPlace_DropRedirect.Install(harmony);
				num++;
			}
			catch (Exception ex)
			{
				list.Add("Patch_GenPlace_DropRedirect");
				Log.Error("[DigitalStorage] 手挂补丁失败（已跳过）：Patch_GenPlace_DropRedirect :: " + ex);
			}
			PatchSummary = PatchSummary + " 手挂=" + num;
			if (list.Count > 0)
			{
				PatchSummary = PatchSummary + " 手挂失败=" + list.Count + " [" + string.Join(",", list.ToArray()) + "]";
			}
		}

		private static void PatchAllIsolated(Harmony harmony)
		{
			Type[] types;
			try
			{
				types = typeof(HarmonyInit).Assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				types = ex.Types;
				Log.Error("[DigitalStorage] 程序集类型枚举部分失败，跳过为 null 的项：" + ex.Message);
			}
			int num = 0;
			List<string> list = new List<string>();
			List<string> list2 = (Prefs.DevMode ? new List<string>() : null);
			foreach (Type type in types)
			{
				if (!(type == null) && type.GetCustomAttributes(typeof(HarmonyPatch), inherit: true).Length != 0)
				{
					try
					{
						harmony.CreateClassProcessor(type).Patch();
						num++;
						list2?.Add(type.Name);
					}
					catch (Exception ex2)
					{
						list.Add(type.Name);
						Log.Error("[DigitalStorage] 补丁类挂载失败（已跳过，其余继续）：" + type.FullName + " :: " + ex2);
					}
				}
			}
			PatchSummary = ((list.Count == 0) ? (" 补丁类=" + num) : (" 补丁类=" + num + " 失败=" + list.Count + " [" + string.Join(",", list.ToArray()) + "]"));
			if (list2 != null)
			{
				Log.Warning("[DigitalStorage] 已挂补丁类：" + string.Join(",", list2.ToArray()));
			}
		}
	}
	[HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
	internal static class Patch_GetFood
	{
		private static void Postfix(Pawn pawn, ref Job __result)
		{
			if (__result != null)
			{
				if (!ConsumePatchUtil.IsUnexecutableContainerTarget(__result))
				{
					return;
				}
				__result = null;
			}
			if (ConsumePatchUtil.ShouldTry(pawn) && CanUseCoreFood(pawn))
			{
				__result = ConsumptionHelper.TryCreateJob(pawn, (Thing t) => t.def.IsNutritionGivingIngestible);
			}
		}

		private static bool CanUseCoreFood(Pawn pawn)
		{
			if (pawn.needs?.food == null)
			{
				return false;
			}
			return ConsumePatchUtil.PawnMayUseCore(pawn);
		}
	}
	[HarmonyPatch(typeof(JobGiver_TakeDrugsForDrugPolicy), "TryGiveJob")]
	internal static class Patch_TakeDrugs
	{
		private static void Postfix(Pawn pawn, ref Job __result)
		{
			if (__result != null)
			{
				if (!ConsumePatchUtil.IsUnexecutableContainerTarget(__result))
				{
					return;
				}
				__result = null;
			}
			if (!ConsumePatchUtil.ShouldTry(pawn) || !ConsumePatchUtil.PawnMayUseCore(pawn))
			{
				return;
			}
			Pawn_DrugPolicyTracker drugs = pawn.drugs;
			DrugPolicy val = ((drugs != null) ? drugs.CurrentPolicy : null);
			if (val == null)
			{
				return;
			}
			for (int i = 0; i < val.Count; i++)
			{
				ThingDef drugDef = val[i].drug;
				if (pawn.drugs.ShouldTryToTakeScheduledNow(drugDef))
				{
					Job val2 = ConsumptionHelper.TryCreateJob(pawn, (Thing t) => t.def == drugDef);
					if (val2 != null)
					{
						__result = val2;
						break;
					}
				}
			}
		}
	}
	[HarmonyPatch(typeof(JoyGiver_Ingest), "TryGiveJob")]
	internal static class Patch_JoyIngest
	{
		private static void Postfix(Pawn pawn, ref Job __result)
		{
			if (__result != null)
			{
				if (!ConsumePatchUtil.IsUnexecutableContainerTarget(__result))
				{
					return;
				}
				__result = null;
			}
			if (!ConsumePatchUtil.ShouldTry(pawn) || !ConsumePatchUtil.PawnMayUseCore(pawn))
			{
				return;
			}
			foreach (JoyGiverDef allDef in DefDatabase<JoyGiverDef>.AllDefs)
			{
				if (!(allDef.Worker is JoyGiver_Ingest) || allDef.thingDefs == null)
				{
					continue;
				}
				foreach (ThingDef td in allDef.thingDefs)
				{
					Job val = ConsumptionHelper.TryCreateJob(pawn, (Thing t) => t.def == td);
					if (val != null)
					{
						__result = val;
						return;
					}
				}
			}
		}
	}
	internal static class ConsumePatchUtil
	{
		private static readonly Dictionary<string, int> lastLogTickByKey = new Dictionary<string, int>();

		private static int lastFailTick = -1;

		private static int lastFailPawnID = -1;

		public static bool PawnMayUseCore(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}
			if (pawn.IsPrisoner)
			{
				return false;
			}
			return ((Thing)pawn).Faction == Faction.OfPlayer;
		}

		public static bool ShouldLog(string key)
		{
			int ticksGame = Find.TickManager.TicksGame;
			if (lastLogTickByKey.TryGetValue(key, out var value) && ticksGame - value < 60)
			{
				return false;
			}
			lastLogTickByKey[key] = ticksGame;
			return true;
		}

		public static bool IsUnexecutableContainerTarget(Job job)
		{
			if (!IsHaulSourceContent((job != null) ? ((LocalTargetInfo)(ref job.targetA)).Thing : null) && !IsHaulSourceContent((job != null) ? ((LocalTargetInfo)(ref job.targetB)).Thing : null))
			{
				return IsHaulSourceContent((job != null) ? ((LocalTargetInfo)(ref job.targetC)).Thing : null);
			}
			return true;
		}

		private static bool IsHaulSourceContent(Thing t)
		{
			if (t != null && !t.Spawned)
			{
				return t.ParentHolder is IHaulSource;
			}
			return false;
		}

		public static bool ShouldTry(Pawn pawn)
		{
			if (Find.TickManager.TicksGame == lastFailTick && ((Thing)pawn).thingIDNumber == lastFailPawnID)
			{
				return false;
			}
			if (pawn.CurJob?.def == DigitalStorage_JobDefOf.DigitalStorage_Consume)
			{
				return false;
			}
			for (int i = 0; i < pawn.jobs.jobQueue.Count; i++)
			{
				if (pawn.jobs.jobQueue[i].job.def == DigitalStorage_JobDefOf.DigitalStorage_Consume)
				{
					return false;
				}
			}
			return true;
		}

		public static void NotifyFail(Pawn pawn)
		{
			lastFailTick = Find.TickManager.TicksGame;
			lastFailPawnID = ((Thing)pawn).thingIDNumber;
		}
	}
	internal static class ContainerDrugFinder
	{
		public static Thing FindAndExtract(Pawn pawn, Predicate<Thing> validator, out string reason)
		{
			reason = null;
			if (((pawn != null) ? ((Thing)pawn).Map : null) == null)
			{
				reason = "pawn/map null";
				return null;
			}
			Map map = ((Thing)pawn).Map;
			ReservationManager res = map.reservationManager;
			int seen = 0;
			int rejected = 0;
			int reserved = 0;
			Thing val = HaulSourceContents.FindBestIncludingRemote(map, null, delegate(Thing t)
			{
				//IL_003f: Unknown result type (might be due to invalid IL or missing references)
				seen++;
				if (!validator(t))
				{
					rejected++;
					return false;
				}
				if (res != null && res.IsReserved(LocalTargetInfo.op_Implicit(t)))
				{
					reserved++;
					return false;
				}
				return true;
			});
			if (val == null)
			{
				reason = "no match (seen=" + seen + " rejected=" + rejected + " reserved=" + reserved + ")";
				return null;
			}
			Thing val2 = HaulSourceContents.ExtractToFeet(val, 1, pawn);
			if (val2 == null)
			{
				reason = "extract failed (容器没货/脚下放不下)";
				return null;
			}
			reason = "ok: " + ((Def)val2.def).defName;
			return val2;
		}
	}
	[HarmonyPatch(typeof(JobGiver_SatisfyChemicalNeed), "TryGiveJob")]
	internal static class Patch_SatisfyChemicalNeed
	{
		[HarmonyPostfix]
		internal static void Postfix(Pawn pawn, ref Job __result)
		{
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Invalid comparison between Unknown and I4
			bool flag = DigitalStorageSettings.enableDebugLog && ConsumePatchUtil.ShouldLog("ChemNeed");
			if (flag)
			{
				Log.Warning("[DS] ChemNeed: entered, vanilla __result=" + ((__result != null) ? "non-null" : "null"));
			}
			if (__result != null)
			{
				if (!ConsumePatchUtil.IsUnexecutableContainerTarget(__result))
				{
					return;
				}
				if (flag)
				{
					Log.Warning("[DS] ChemNeed: 原版作业目标在容器里，接管。");
				}
				__result = null;
			}
			Pawn obj = pawn;
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				Pawn_NeedsTracker needs = obj.needs;
				obj2 = ((needs != null) ? needs.AllNeeds : null);
			}
			List<Need> list = (List<Need>)obj2;
			if (list == null)
			{
				if (flag)
				{
					Log.Warning("[DS] ChemNeed: needs=null");
				}
				return;
			}
			List<Need_Chemical> list2 = new List<Need_Chemical>();
			for (int i = 0; i < list.Count; i++)
			{
				Need obj3 = list[i];
				Need_Chemical val = (Need_Chemical)(object)((obj3 is Need_Chemical) ? obj3 : null);
				if (val != null && (int)val.CurCategory <= 1)
				{
					list2.Add(val);
				}
			}
			if (list2.Count == 0)
			{
				if (flag)
				{
					Log.Warning("[DS] ChemNeed: 没有需要满足的 Need_Chemical");
				}
				return;
			}
			list2.Sort((Need_Chemical a, Need_Chemical b) => ((Need)a).CurLevel.CompareTo(((Need)b).CurLevel));
			for (int num = 0; num < list2.Count; num++)
			{
				Need_Chemical val2 = list2[num];
				Hediff_Addiction addiction = val2.AddictionHediff;
				if (addiction == null)
				{
					if (flag)
					{
						Log.Warning("[DS] ChemNeed: AddictionHediff=null");
					}
					continue;
				}
				string reason;
				Thing val3 = ContainerDrugFinder.FindAndExtract(pawn, (Thing t) => DrugValidator(pawn, addiction, t), out reason);
				if (flag)
				{
					Log.Warning("[DS] ChemNeed: " + ((Def)((Need)val2).def).defName + " -> " + reason);
				}
				if (val3 != null)
				{
					__result = DrugAIUtility.IngestAndTakeToInventoryJob(val3, pawn, 1);
					break;
				}
			}
		}

		private static bool DrugValidator(Pawn pawn, Hediff_Addiction addiction, Thing drug)
		{
			if (!drug.def.IsDrug)
			{
				return false;
			}
			CompDrug val = ThingCompUtility.TryGetComp<CompDrug>(drug);
			if (((val != null) ? val.Props.chemical : null) == null)
			{
				return false;
			}
			if (val.Props.chemical.addictionHediff != ((Hediff)addiction).def)
			{
				return false;
			}
			Pawn_DrugPolicyTracker drugs = pawn.drugs;
			DrugPolicy val2 = ((drugs != null) ? drugs.CurrentPolicy : null);
			if (val2 != null && !val2[drug.def].allowedForAddiction && pawn.story != null && pawn.story.traits.DegreeOfTrait(TraitDefOf.DrugDesire) <= 0 && (!pawn.InMentalState || !pawn.MentalStateDef.ignoreDrugPolicy))
			{
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(JobGiver_SatifyChemicalDependency), "TryGiveJob")]
	internal static class Patch_SatifyChemicalDependency
	{
		[HarmonyPostfix]
		internal static void Postfix(Pawn pawn, ref Job __result)
		{
			bool flag = DigitalStorageSettings.enableDebugLog && ConsumePatchUtil.ShouldLog("ChemDep");
			if (flag)
			{
				Log.Warning("[DS] ChemDep: entered, vanilla __result=" + ((__result != null) ? "non-null" : "null"));
			}
			if (__result != null)
			{
				if (!ConsumePatchUtil.IsUnexecutableContainerTarget(__result))
				{
					return;
				}
				if (flag)
				{
					Log.Warning("[DS] ChemDep: 原版作业目标在容器里，接管。");
				}
				__result = null;
			}
			if (!ModsConfig.BiotechActive)
			{
				if (flag)
				{
					Log.Warning("[DS] ChemDep: Biotech 未启用");
				}
				return;
			}
			List<Hediff> list = pawn?.health?.hediffSet?.hediffs;
			if (list == null)
			{
				if (flag)
				{
					Log.Warning("[DS] ChemDep: hediffs=null");
				}
				return;
			}
			List<Hediff_ChemicalDependency> list2 = new List<Hediff_ChemicalDependency>();
			for (int i = 0; i < list.Count; i++)
			{
				Hediff obj = list[i];
				Hediff_ChemicalDependency val = (Hediff_ChemicalDependency)(object)((obj is Hediff_ChemicalDependency) ? obj : null);
				if (val != null && val.ShouldSatify)
				{
					list2.Add(val);
				}
			}
			if (list2.Count == 0)
			{
				if (flag)
				{
					Log.Warning("[DS] ChemDep: 没有需要满足的化学依赖");
				}
				return;
			}
			list2.Sort((Hediff_ChemicalDependency a, Hediff_ChemicalDependency b) => ((Hediff)b).Severity.CompareTo(((Hediff)a).Severity));
			for (int num = 0; num < list2.Count; num++)
			{
				Hediff_ChemicalDependency dep = list2[num];
				string reason;
				Thing val2 = ContainerDrugFinder.FindAndExtract(pawn, (Thing t) => DrugValidator(pawn, dep, t), out reason);
				if (flag)
				{
					Log.Warning("[DS] ChemDep: " + ((Def)((Hediff)dep).def).defName + " -> " + reason);
				}
				if (val2 != null)
				{
					__result = DrugAIUtility.IngestAndTakeToInventoryJob(val2, pawn, 1);
					break;
				}
			}
		}

		private static bool DrugValidator(Pawn pawn, Hediff_ChemicalDependency dependency, Thing drug)
		{
			if (!drug.def.IsDrug)
			{
				return false;
			}
			CompDrug val = ThingCompUtility.TryGetComp<CompDrug>(drug);
			if (val == null || val.Props.chemical == null)
			{
				return false;
			}
			if (val.Props.chemical != dependency.chemical)
			{
				return false;
			}
			if (pawn.drugs != null && !pawn.drugs.CurrentPolicy[drug.def].allowedForAddiction && (!pawn.InMentalState || pawn.MentalStateDef.ignoreDrugPolicy))
			{
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(JoyGiver_TakeDrug), "BestIngestItem")]
	internal static class Patch_JoyTakeDrug
	{
		private static readonly List<ThingDef> takeableDrugs = new List<ThingDef>();

		[HarmonyPostfix]
		internal static void Postfix(Pawn pawn, Predicate<Thing> extraValidator, ref Thing __result)
		{
			bool flag = DigitalStorageSettings.enableDebugLog && ConsumePatchUtil.ShouldLog("JoyDrug");
			if (flag)
			{
				Log.Warning("[DS] JoyDrug: entered, vanilla __result=" + ((__result != null) ? "non-null" : "null"));
			}
			if (__result != null || pawn?.drugs == null || ((Thing)pawn).Map == null)
			{
				return;
			}
			bool flag2 = pawn.story != null && (pawn.story.traits.DegreeOfTrait(TraitDefOf.DrugDesire) > 0 || pawn.InMentalState);
			takeableDrugs.Clear();
			DrugPolicy currentPolicy = pawn.drugs.CurrentPolicy;
			for (int i = 0; i < currentPolicy.Count; i++)
			{
				if (flag2 || currentPolicy[i].allowedForJoy)
				{
					takeableDrugs.Add(currentPolicy[i].drug);
				}
			}
			for (int j = 0; j < takeableDrugs.Count; j++)
			{
				ThingDef def = takeableDrugs[j];
				string reason;
				Thing val = ContainerDrugFinder.FindAndExtract(pawn, (Thing t) => t.def == def && CanIngestForJoyMirror(pawn, t) && (extraValidator == null || extraValidator(t)) && t.def.ingestible != null && (int)t.def.ingestible.drugCategory > 0, out reason);
				if (flag)
				{
					Log.Warning("[DS] JoyDrug: " + ((Def)def).defName + " -> " + reason);
				}
				if (val != null)
				{
					__result = val;
					break;
				}
			}
		}

		private static bool CanIngestForJoyMirror(Pawn pawn, Thing t)
		{
			if (!t.def.IsIngestible || t.def.ingestible.joyKind == null || t.def.ingestible.joy <= 0f || !FoodUtility.WillEat(pawn, t, (Pawn)null, true, false))
			{
				return false;
			}
			if (t.def.IsDrug && pawn.drugs != null && !pawn.drugs.CurrentPolicy[t.def].allowedForJoy && pawn.story != null && pawn.story.traits.DegreeOfTrait(TraitDefOf.DrugDesire) <= 0 && !pawn.InMentalState)
			{
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(ResourceCounter), "UpdateResourceCounts")]
	public static class Patch_ResourceCounter_HaulSources
	{
		[HarmonyPostfix]
		public static void Postfix(Map ___map, Dictionary<ThingDef, int> ___countedAmounts)
		{
			if (___map == null || ___countedAmounts == null)
			{
				return;
			}
			HaulDestinationManager haulDestinationManager = ___map.haulDestinationManager;
			List<IHaulSource> list = ((haulDestinationManager != null) ? haulDestinationManager.AllHaulSourcesListForReading : null);
			if (list == null)
			{
				return;
			}
			for (int i = 0; i < list.Count; i++)
			{
				IHaulSource val = list[i];
				if (val == null || !val.HaulSourceEnabled || val is ISlotGroupParent || val is Building_StorageCore { CompatSlotGroupRegistered: not false })
				{
					continue;
				}
				ThingOwner directlyHeldThings = ((IThingHolder)val).GetDirectlyHeldThings();
				if (directlyHeldThings == null)
				{
					continue;
				}
				for (int j = 0; j < directlyHeldThings.Count; j++)
				{
					Thing val2 = directlyHeldThings[j];
					if (val2 != null && val2.def != null)
					{
						Thing innerIfMinified = MinifyUtility.GetInnerIfMinified(val2);
						if (innerIfMinified != null && innerIfMinified.def != null && innerIfMinified.def.CountAsResource && ShouldCount(innerIfMinified))
						{
							___countedAmounts.TryGetValue(innerIfMinified.def, out var value);
							___countedAmounts[innerIfMinified.def] = value + innerIfMinified.stackCount;
						}
					}
				}
			}
		}

		private static bool ShouldCount(Thing t)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			if (RottableUtility.IsNotFresh(t))
			{
				return false;
			}
			if (t.SpawnedOrAnyParentSpawned && GridsUtility.Fogged(t.PositionHeld, t.MapHeld))
			{
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(/*Could not decode attribute arguments.*/)]
	internal static class Patch_SlotGroup_HeldThings
	{
		[HarmonyPostfix]
		private static void Postfix(SlotGroup __instance, ref IEnumerable<Thing> __result)
		{
			CoreSlotGroupAdapter coreSlotGroupAdapter = AdapterOf(__instance);
			if (coreSlotGroupAdapter != null)
			{
				__result = WithContents(__result, coreSlotGroupAdapter);
			}
		}

		private static IEnumerable<Thing> WithContents(IEnumerable<Thing> original, CoreSlotGroupAdapter adapter)
		{
			if (original != null)
			{
				foreach (Thing item in original)
				{
					yield return item;
				}
			}
			Building_StorageCore core = adapter.Core;
			if (core == null)
			{
				yield break;
			}
			ThingOwner held = core.GetDirectlyHeldThings();
			if (held == null)
			{
				yield break;
			}
			for (int i = 0; i < held.Count; i++)
			{
				Thing val = held[i];
				if (val != null && !val.Destroyed && val.def != null && val.def.EverStorable(false))
				{
					yield return val;
				}
			}
		}

		internal static CoreSlotGroupAdapter AdapterOf(SlotGroup group)
		{
			if (group != null)
			{
				return group.parent as CoreSlotGroupAdapter;
			}
			return null;
		}
	}
	[HarmonyPatch(/*Could not decode attribute arguments.*/)]
	internal static class Patch_SlotGroup_HeldThingsCount
	{
		[HarmonyPostfix]
		private static void Postfix(SlotGroup __instance, ref int __result)
		{
			Building_StorageCore building_StorageCore = Patch_SlotGroup_HeldThings.AdapterOf(__instance)?.Core;
			if (building_StorageCore == null)
			{
				return;
			}
			ThingOwner directlyHeldThings = building_StorageCore.GetDirectlyHeldThings();
			if (directlyHeldThings == null)
			{
				return;
			}
			for (int i = 0; i < directlyHeldThings.Count; i++)
			{
				Thing val = directlyHeldThings[i];
				if (val != null && !val.Destroyed && val.def != null && val.def.EverStorable(false))
				{
					__result++;
				}
			}
		}
	}
	[HarmonyPatch(typeof(Bill_Production), "SaveSlotReferencable")]
	internal static class Patch_Bill_Production_SaveCoreGroup
	{
		[HarmonyPrefix]
		private static bool Prefix(ISlotGroup slot, string key)
		{
			if (!(((SlotGroup)(((slot is SlotGroup) ? slot : null)?)).parent is CoreSlotGroupAdapter coreSlotGroupAdapter))
			{
				return true;
			}
			ILoadReferenceable core = (ILoadReferenceable)(object)coreSlotGroupAdapter.Core;
			Scribe_References.Look<ILoadReferenceable>(ref core, key, false);
			return false;
		}
	}
	[HarmonyPatch(typeof(Bill_Production), "LoadSlotReferencable")]
	internal static class Patch_Bill_Production_LoadCoreGroup
	{
		[HarmonyPrefix]
		private static bool Prefix(ref ISlotGroup slot, string key)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Invalid comparison between Unknown and I4
			ILoadReferenceable val = null;
			Scribe_References.Look<ILoadReferenceable>(ref val, key, false);
			if ((int)Scribe.mode != 3)
			{
				return false;
			}
			if (val is Building_StorageCore building_StorageCore)
			{
				slot = (ISlotGroup)(object)building_StorageCore.CompatSlotGroup.GetSlotGroup();
				return false;
			}
			ISlotGroup val2 = (ISlotGroup)(object)((val is ISlotGroup) ? val : null);
			if (val2 != null)
			{
				slot = val2;
				return false;
			}
			ISlotGroupParent val3 = (ISlotGroupParent)(object)((val is ISlotGroupParent) ? val : null);
			if (val3 != null)
			{
				slot = (ISlotGroup)(object)val3.GetSlotGroup();
			}
			return false;
		}
	}
	[HarmonyPatch(typeof(Bill_Production), "ValidateGroup")]
	internal static class Patch_Bill_Production_ValidateCoreGroup
	{
		[HarmonyPrefix]
		private static bool Prefix(Bill_Production __instance, ref ISlotGroup slot)
		{
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			ISlotGroup obj = slot;
			SlotGroup val = (SlotGroup)(object)((obj is SlotGroup) ? obj : null);
			if (!(val?.parent is CoreSlotGroupAdapter coreSlotGroupAdapter))
			{
				return true;
			}
			Map map = ((Bill)__instance).Map;
			if (map != null && !map.haulDestinationManager.AllGroupsListForReading.Contains(val))
			{
				if ((object)__instance != BillUtility.Clipboard)
				{
					string text = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("MessageBillValidationIncludeBuildingDeleted", NamedArgument.op_Implicit(((Bill)__instance).LabelCap), NamedArgument.op_Implicit(GenText.CapitalizeFirst(((Bill)__instance).billStack.billGiver.LabelShort)), NamedArgument.op_Implicit(((Entity)coreSlotGroupAdapter.Core).LabelCap)));
					IBillGiver billGiver = ((Bill)__instance).billStack.billGiver;
					Messages.Message(text, LookTargets.op_Implicit((Thing)(object)((billGiver is Thing) ? billGiver : null)), MessageTypeDefOf.NegativeEvent, true);
				}
				slot = null;
			}
			return false;
		}
	}
	[HarmonyPatch(typeof(Designator_Build), "ProcessInput")]
	[HarmonyPriority(600)]
	internal static class Patch_DesignatorBuild_ProcessInput_Transpiler
	{
		public static bool Applied;

		private static readonly MethodInfo GetCount = AccessTools.PropertyGetter(typeof(List<Thing>), "Count");

		private static readonly MethodInfo GetMap = AccessTools.PropertyGetter(typeof(Designator), "Map");

		private static readonly MethodInfo ExtendList = AccessTools.Method(typeof(DSMaterialMenuUtility), "ExtendList", (Type[])null, (Type[])null);

		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bf: Expected O, but got Unknown
			//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01da: Expected O, but got Unknown
			List<CodeInstruction> list = instructions.ToList();
			if (GetCount == null || GetMap == null || ExtendList == null)
			{
				Log.Error("[DigitalStorage] ProcessInput transpiler: reflection lookup failed; fallback prefix will be used.");
				return list;
			}
			try
			{
				int num = -1;
				for (int i = 0; i < list.Count; i++)
				{
					MethodInfo methodInfo = list[i].operand as MethodInfo;
					if (list[i].opcode == OpCodes.Callvirt && methodInfo == GetCount)
					{
						num = i;
						break;
					}
				}
				if (num < 0)
				{
					Log.Error("[DigitalStorage] ProcessInput transpiler: List<Thing>.get_Count not found; fallback prefix will be used.");
					return list;
				}
				CodeInstruction val = null;
				for (int num2 = num - 1; num2 >= 0; num2--)
				{
					OpCode opcode = list[num2].opcode;
					if ((opcode == OpCodes.Ldloc || opcode == OpCodes.Ldloc_S || opcode == OpCodes.Ldloc_0 || opcode == OpCodes.Ldloc_1 || opcode == OpCodes.Ldloc_2 || opcode == OpCodes.Ldloc_3) && (!(list[num2].operand is LocalBuilder localBuilder) || !(localBuilder.LocalType != typeof(ThingDef))) && (!(list[num2].operand is LocalVariableInfo localVariableInfo) || !(localVariableInfo.LocalType != typeof(ThingDef))))
					{
						val = list[num2];
						break;
					}
				}
				if (val == null)
				{
					Log.Error("[DigitalStorage] ProcessInput transpiler: stuffDef local load not found; fallback prefix will be used.");
					return list;
				}
				list.InsertRange(num, (IEnumerable<CodeInstruction>)(object)new CodeInstruction[4]
				{
					CodeInstruction.LoadArgument(0, false),
					new CodeInstruction(OpCodes.Callvirt, (object)GetMap),
					val.Clone(),
					new CodeInstruction(OpCodes.Call, (object)ExtendList)
				});
				Applied = true;
			}
			catch (Exception ex)
			{
				Log.Error("[DigitalStorage] ProcessInput transpiler failed: " + ex);
			}
			return list;
		}
	}
	[HarmonyPatch(typeof(Designator_Build), "ProcessInput")]
	[HarmonyPriority(200)]
	internal static class Patch_DesignatorBuild_ProcessInput_Fallback
	{
		private static readonly List<Thing> tmpStuffThings = new List<Thing>();

		private static readonly FieldRef<Designator_Build, bool> WriteStuffRef = AccessTools.FieldRefAccess<Designator_Build, bool>("writeStuff");

		private static readonly Func<Designator, bool> CanInteract = AccessTools.MethodDelegate<Func<Designator, bool>>(AccessTools.Method(typeof(Designator), "CheckCanInteract", (Type[])null, (Type[])null), (object)null, true, (Type[])null);

		private static bool Prefix(Designator_Build __instance)
		{
			try
			{
				return PrefixInner(__instance);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 选材菜单兜底失败（已退回原版菜单，不影响能否建造）：" + ex, 382253);
				return true;
			}
		}

		private static bool PrefixInner(Designator_Build __instance)
		{
			//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f7: Expected O, but got Unknown
			//IL_0213: Unknown result type (might be due to invalid IL or missing references)
			//IL_021a: Expected O, but got Unknown
			if (Patch_DesignatorBuild_ProcessInput_Transpiler.Applied)
			{
				return true;
			}
			BuildableDef placingDef = ((Designator_Place)__instance).PlacingDef;
			ThingDef val = (ThingDef)(object)((placingDef is ThingDef) ? placingDef : null);
			if (val == null || !((BuildableDef)val).MadeFromStuff)
			{
				return true;
			}
			Map map = ((Designator)__instance).Map;
			if (map == null)
			{
				return true;
			}
			if (!CanInteract((Designator)(object)__instance))
			{
				return false;
			}
			if (HaulSourceContents.EnabledSources(map).Count == 0 && !HaulSourceContents.AnyRemoteContent(map))
			{
				return true;
			}
			HashSet<ThingDef> hashSet = new HashSet<ThingDef>();
			HashSet<ThingDef> hashSet2 = new HashSet<ThingDef>();
			CollectStuffDefs(map, val, hashSet2);
			foreach (ThingDef key in map.resourceCounter.AllCountedAmounts.Keys)
			{
				if (key.IsStuff && key.stuffProps.CanMake((BuildableDef)(object)val) && map.listerThings.ThingsOfDef(key).Count > 0)
				{
					hashSet.Add(key);
				}
			}
			HashSet<ThingDef> hashSet3 = new HashSet<ThingDef>(hashSet2);
			hashSet3.ExceptWith(hashSet);
			if (hashSet3.Count == 0)
			{
				return true;
			}
			HashSet<ThingDef> hashSet4 = new HashSet<ThingDef>(hashSet);
			hashSet4.UnionWith(hashSet2);
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			foreach (ThingDef item in from d in hashSet4
				orderby d.stuffProps?.commonality ?? 0f descending, d.BaseMarketValue
				select d)
			{
				ThingDef localDef = item;
				string text = GenText.CapitalizeFirst(GenLabel.ThingLabel(((Designator_Place)__instance).PlacingDef, localDef, 1));
				list.Add(new FloatMenuOption(text, (Action)delegate
				{
					__instance.SetStuffDef(localDef);
					WriteStuffRef.Invoke(__instance) = true;
					Find.DesignatorManager.Select((Designator)(object)__instance);
				}, localDef, (ThingStyleDef)null, false, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0, (int?)null));
			}
			FloatMenu val2 = new FloatMenu(list);
			val2.onCloseCallback = delegate
			{
				WriteStuffRef.Invoke(__instance) = true;
			};
			Find.WindowStack.Add((Window)(object)val2);
			Find.DesignatorManager.Select((Designator)(object)__instance);
			return false;
		}

		private static void CollectStuffDefs(Map map, ThingDef thingDef, HashSet<ThingDef> result)
		{
			HaulSourceContents.GatherAllIncludingRemote(map, tmpStuffThings);
			for (int i = 0; i < tmpStuffThings.Count; i++)
			{
				ThingDef val = tmpStuffThings[i]?.def;
				if (val != null && val.IsStuff && val.stuffProps.CanMake((BuildableDef)(object)thingDef))
				{
					result.Add(val);
				}
			}
			tmpStuffThings.Clear();
		}
	}
	internal static class DSMaterialMenuUtility
	{
		private static Map cachedMap;

		private static int cachedTick = -1;

		private static readonly HashSet<ThingDef> cachedContainerStuffDefs = new HashSet<ThingDef>();

		private static readonly List<Thing> tmpStuffThings = new List<Thing>();

		public static List<Thing> ExtendList(List<Thing> list, Map map, ThingDef stuffDef)
		{
			if (list == null || list.Count > 0 || map == null || stuffDef == null)
			{
				return list;
			}
			try
			{
				if (!GetContainerStuffDefs(map).Contains(stuffDef))
				{
					return list;
				}
				return new List<Thing>(1) { null };
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 选材菜单扩展失败（已降级为原版菜单，不影响能否建造）：" + ex, 382252);
				return list;
			}
		}

		private static HashSet<ThingDef> GetContainerStuffDefs(Map map)
		{
			int ticksGame = Find.TickManager.TicksGame;
			if (map == cachedMap && ticksGame == cachedTick)
			{
				return cachedContainerStuffDefs;
			}
			cachedMap = map;
			cachedTick = ticksGame;
			cachedContainerStuffDefs.Clear();
			HaulSourceContents.GatherAllIncludingRemote(map, tmpStuffThings);
			for (int i = 0; i < tmpStuffThings.Count; i++)
			{
				Thing val = tmpStuffThings[i];
				if (val?.def != null)
				{
					cachedContainerStuffDefs.Add(val.def);
				}
			}
			tmpStuffThings.Clear();
			return cachedContainerStuffDefs;
		}
	}
	[HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ResourceDeliverJobFor")]
	public static class Patch_ResourceDeliverJobFor
	{
		[HarmonyPostfix]
		[HarmonyPriority(200)]
		public static void Postfix(Pawn pawn, IConstructible c, bool forced, bool __runOriginal, ref Job __result)
		{
			if (__runOriginal && __result == null && DSConstructionDelivery.TryMakeJob(pawn, c, forced, out var job))
			{
				__result = job;
			}
		}
	}
	[HarmonyPatch(typeof(RecipeDef), "PotentiallyMissingIngredients")]
	[HarmonyPatch(new Type[]
	{
		typeof(Pawn),
		typeof(Map)
	})]
	internal static class Patch_RecipeDef_MissingIngredients
	{
		private static readonly List<Thing> tmpThings = new List<Thing>();

		private static void Postfix(ref IEnumerable<ThingDef> __result, Map map, RecipeDef __instance)
		{
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			if (map == null || __instance?.ingredients == null || __instance.ingredients.Count == 0)
			{
				return;
			}
			List<ThingDef> list;
			try
			{
				list = __result.ToList();
			}
			catch (Exception ex)
			{
				if (DigitalStorageSettings.enableDebugLog)
				{
					Log.Warning("[DS] PotentiallyMissingIngredients 迭代异常,按不缺料处理: " + ex.Message);
				}
				__result = new List<ThingDef>();
				return;
			}
			if (list.Count == 0)
			{
				return;
			}
			HashSet<ThingDef> hashSet = new HashSet<ThingDef>();
			HaulSourceContents.GatherAllIncludingRemote(map, tmpThings);
			for (int i = 0; i < tmpThings.Count; i++)
			{
				Thing val = tmpThings[i];
				if (val?.def != null && !map.reservationManager.IsReserved(LocalTargetInfo.op_Implicit(val)))
				{
					hashSet.Add(val.def);
				}
			}
			tmpThings.Clear();
			if (hashSet.Count == 0)
			{
				return;
			}
			ThingFilter fixedIngredientFilter = __instance.fixedIngredientFilter;
			List<ThingDef> list2 = new List<ThingDef>();
			foreach (ThingDef item in list)
			{
				bool flag = true;
				foreach (IngredientCount ingredient in __instance.ingredients)
				{
					if (!ingredient.filter.Allows(item))
					{
						continue;
					}
					foreach (ThingDef item2 in hashSet)
					{
						if (ingredient.filter.Allows(item2) && (ingredient.IsFixedIngredient || fixedIngredientFilter == null || fixedIngredientFilter.Allows(item2)))
						{
							flag = false;
							break;
						}
					}
					if (!flag)
					{
						break;
					}
				}
				if (flag)
				{
					list2.Add(item);
				}
			}
			__result = list2;
		}
	}
	[HarmonyPatch(typeof(HealthAIUtility), "FindBestMedicine")]
	internal static class Patch_FindBestMedicine
	{
		private static void Postfix(Pawn healer, Pawn patient, bool onlyUseInventory, ref Thing __result)
		{
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Invalid comparison between Unknown and I4
			if (__result != null || onlyUseInventory || ((healer != null) ? ((Thing)healer).Map : null) == null || patient == null)
			{
				return;
			}
			Pawn_PlayerSettings playerSettings = patient.playerSettings;
			MedicalCareCategory medCare = (MedicalCareCategory)((playerSettings == null) ? 1 : ((int)playerSettings.medCare));
			if ((int)medCare <= 1)
			{
				return;
			}
			int medicineCountToFullyHeal = Medicine.GetMedicineCountToFullyHeal(patient);
			if (medicineCountToFullyHeal <= 0)
			{
				return;
			}
			Thing val = HaulSourceContents.FindBestIncludingRemote(((Thing)healer).Map, (Thing t) => StatExtension.GetStatValueAbstract((BuildableDef)(object)t.def, StatDefOf.MedicalPotency, (ThingDef)null), (Thing t) => t.def.IsMedicine && MedicalCareUtility.AllowsMedicine(medCare, t.def));
			if (val != null)
			{
				Thing val2 = HaulSourceContents.ExtractToFeet(val, medicineCountToFullyHeal, healer);
				if (val2 != null)
				{
					__result = val2;
				}
			}
		}
	}
	[HarmonyPatch(typeof(RefuelWorkGiverUtility), "FindBestFuel")]
	[HarmonyPatch(new Type[]
	{
		typeof(Pawn),
		typeof(Thing)
	})]
	internal static class Patch_RefuelFromStorage
	{
		private static void Postfix(Pawn pawn, Thing refuelable, ref Thing __result)
		{
			if (__result != null || ((pawn != null) ? ((Thing)pawn).Map : null) == null || refuelable == null)
			{
				return;
			}
			CompRefuelable val = ThingCompUtility.TryGetComp<CompRefuelable>(refuelable);
			if (val == null)
			{
				return;
			}
			ThingFilter filter = val.Props.fuelFilter;
			if (filter == null)
			{
				return;
			}
			int fuelCountToFullyRefuel = val.GetFuelCountToFullyRefuel();
			if (fuelCountToFullyRefuel <= 0)
			{
				return;
			}
			Thing val2 = HaulSourceContents.FindBestIncludingRemote(((Thing)pawn).Map, null, (Thing t) => filter.Allows(t.def));
			if (val2 != null)
			{
				Thing val3 = HaulSourceContents.ExtractToFeet(val2, fuelCountToFullyRefuel, pawn);
				if (val3 != null)
				{
					__result = val3;
				}
			}
		}
	}
	[HarmonyPatch(typeof(WorkGiver_FixBrokenDownBuilding), "FindClosestComponent")]
	[HarmonyPatch(new Type[] { typeof(Pawn) })]
	internal static class Patch_FixBrokenFromStorage
	{
		private static void Postfix(Pawn pawn, ref Thing __result)
		{
			if (__result != null || ((pawn != null) ? ((Thing)pawn).Map : null) == null)
			{
				return;
			}
			Thing val = HaulSourceContents.FindBestIncludingRemote(((Thing)pawn).Map, null, (Thing t) => t.def == ThingDefOf.ComponentIndustrial);
			if (val != null)
			{
				Thing val2 = HaulSourceContents.ExtractToFeet(val, 1, pawn);
				if (val2 != null)
				{
					__result = val2;
				}
			}
		}
	}
	[HarmonyPatch(typeof(FoodUtility), "TryFindBestFoodSourceFor")]
	internal static class Patch_FeedFromStorage
	{
		private static void Postfix(Pawn getter, Pawn eater, ref Thing foodSource, ref ThingDef foodDef, ref bool __result)
		{
			if (__result || foodSource != null)
			{
				return;
			}
			Pawn obj = getter;
			if (((obj != null) ? ((Thing)obj).Map : null) == null || eater == null || !ConsumePatchUtil.PawnMayUseCore(getter))
			{
				return;
			}
			ThingFilter val = null;
			if (eater.foodRestriction != null)
			{
				FoodPolicy currentRespectedRestriction = eater.foodRestriction.GetCurrentRespectedRestriction(getter);
				if (currentRespectedRestriction != null)
				{
					val = currentRespectedRestriction.filter;
				}
			}
			ThingFilter capturedFilter = val;
			Thing val2 = HaulSourceContents.FindBestIncludingRemote(((Thing)getter).Map, (Thing t) => FoodScoring.Score(eater, t, getter), (Thing t) => t.def.IsNutritionGivingIngestible && t.def.IsIngestible && (capturedFilter == null || capturedFilter.Allows(t.def)) && eater.RaceProps.Eats(t.def.ingestible.foodType) && FoodUtility.WillEat(eater, t, getter, true, false));
			if (val2 == null || !FoodUtility.WillEat(eater, val2, getter, true, false))
			{
				return;
			}
			int num = CalculateFeedAmount(eater, val2.def, val2.stackCount);
			if (num > 0)
			{
				Thing val3 = HaulSourceContents.ExtractToFeet(val2, num, getter);
				if (val3 != null)
				{
					foodSource = val3;
					foodDef = val3.def;
					__result = true;
				}
			}
		}

		private static int CalculateFeedAmount(Pawn eater, ThingDef def, int maxAvailable)
		{
			float num = ((eater.needs?.food != null) ? eater.needs.food.NutritionWanted : 1f);
			float statValueAbstract = StatExtension.GetStatValueAbstract((BuildableDef)(object)def, StatDefOf.Nutrition, (ThingDef)null);
			if (statValueAbstract <= 0f)
			{
				return 1;
			}
			int num2 = (int)Math.Ceiling(num / statValueAbstract);
			if (num2 <= 0)
			{
				num2 = 1;
			}
			if (def.ingestible.maxNumToIngestAtOnce > 0)
			{
				num2 = Math.Min(num2, def.ingestible.maxNumToIngestAtOnce);
			}
			return Math.Min(num2, maxAvailable);
		}
	}
	[HarmonyPatch(typeof(TradeUtility), "AllLaunchableThingsForTrade")]
	internal static class Patch_AllLaunchableThingsForTrade
	{
		private static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result, Map map, ITrader trader)
		{
			HashSet<Thing> yielded = new HashSet<Thing>(ThingIdentityComparer.Instance);
			if (__result != null)
			{
				foreach (Thing item in __result)
				{
					if (yielded.Add(item))
					{
						yield return item;
					}
				}
			}
			List<Thing> additions;
			try
			{
				additions = new List<Thing>();
				foreach (Thing item2 in DigitalStorageTradeUtility.CoreContents(map, trader))
				{
					if (item2 != null && yielded.Add(item2))
					{
						additions.Add(item2);
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 追加核心内容物到交易列表时抛异常（已降级为只显示原版内容，交易列表不会因此变空）: " + ex, 382234);
				additions = null;
			}
			if (additions != null)
			{
				for (int i = 0; i < additions.Count; i++)
				{
					yield return additions[i];
				}
			}
		}
	}
	[HarmonyPatch(typeof(TradeUtility), "LaunchThingsOfType", new Type[]
	{
		typeof(ThingDef),
		typeof(int),
		typeof(Map),
		typeof(TradeShip)
	})]
	internal static class Patch_LaunchThingsOfType
	{
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
		{
			//IL_038f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0396: Expected O, but got Unknown
			//IL_0420: Unknown result type (might be due to invalid IL or missing references)
			//IL_0426: Expected O, but got Unknown
			//IL_0436: Unknown result type (might be due to invalid IL or missing references)
			//IL_043c: Expected O, but got Unknown
			List<CodeInstruction> list = new List<CodeInstruction>(instructions);
			MethodInfo methodInfo = AccessTools.Method(typeof(Math), "Min", new Type[2]
			{
				typeof(int),
				typeof(int)
			}, (Type[])null);
			MethodInfo methodInfo2 = AccessTools.Method(typeof(Log), "Error", new Type[1] { typeof(string) }, (Type[])null);
			MethodInfo methodInfo3 = AccessTools.Method(typeof(DigitalStorageTradeUtility), "PaymentThing", (Type[])null, (Type[])null);
			FieldInfo fieldInfo = AccessTools.Field(typeof(Thing), "stackCount");
			MethodBody methodBody = __originalMethod.GetMethodBody();
			if (methodInfo == null || methodInfo2 == null || methodInfo3 == null || fieldInfo == null || methodBody == null)
			{
				throw new InvalidOperationException("[DigitalStorage] LaunchThingsOfType payment hook metadata is unavailable.");
			}
			int num = -1;
			int num2 = -1;
			for (int i = 3; i < list.Count; i++)
			{
				if (!CodeInstructionExtensions.Calls(list[i], methodInfo) || list[i - 1].opcode != OpCodes.Ldfld || !fieldInfo.Equals(list[i - 1].operand) || !LoadsArgument(list[i - 3], 1))
				{
					continue;
				}
				int num3 = LocalIndex(list[i - 2], store: false);
				if (num3 >= 0 && num3 < methodBody.LocalVariables.Count && !(methodBody.LocalVariables[num3].LocalType != typeof(Thing)))
				{
					if (num >= 0)
					{
						throw new InvalidOperationException("[DigitalStorage] LaunchThingsOfType has multiple payment count sites.");
					}
					num = i - 3;
					num2 = num3;
				}
			}
			if (num < 0)
			{
				throw new InvalidOperationException("[DigitalStorage] LaunchThingsOfType payment count site was not found.");
			}
			CodeInstruction val = null;
			for (int j = 1; j < num; j++)
			{
				if (!(list[j - 1].opcode != OpCodes.Ldnull) && LocalIndex(list[j], store: true) == num2)
				{
					if (val != null)
					{
						throw new InvalidOperationException("[DigitalStorage] LaunchThingsOfType has multiple payment candidate initializers.");
					}
					val = list[j];
				}
			}
			int num4 = -1;
			for (int k = 0; k + 2 < num; k++)
			{
				if (LocalIndex(list[k], store: false) != num2 || (list[k + 1].opcode != OpCodes.Brtrue && list[k + 1].opcode != OpCodes.Brtrue_S) || !(list[k + 1].operand is Label item) || !list[num].labels.Contains(item) || list[k + 2].opcode != OpCodes.Ldstr || !object.Equals(list[k + 2].operand, "Could not find any "))
				{
					continue;
				}
				bool flag = false;
				bool flag2 = false;
				for (int l = k + 3; l < num; l++)
				{
					if (CodeInstructionExtensions.Calls(list[l], methodInfo2))
					{
						flag = true;
					}
					if (flag && list[l].opcode == OpCodes.Ret)
					{
						flag2 = true;
					}
				}
				if (flag2)
				{
					if (num4 >= 0)
					{
						throw new InvalidOperationException("[DigitalStorage] LaunchThingsOfType has multiple missing-payment checks.");
					}
					num4 = k;
				}
			}
			if (val == null || num4 < 0)
			{
				throw new InvalidOperationException("[DigitalStorage] LaunchThingsOfType missing-payment branch did not match the supported vanilla shape.");
			}
			CodeInstruction val2 = new CodeInstruction(list[num4].opcode, list[num4].operand);
			val2.labels.AddRange(list[num4].labels);
			val2.blocks.AddRange(list[num4].blocks);
			list[num4].labels.Clear();
			list[num4].blocks.Clear();
			list.InsertRange(num4, (IEnumerable<CodeInstruction>)(object)new CodeInstruction[6]
			{
				val2,
				CodeInstruction.LoadArgument(0, false),
				CodeInstruction.LoadArgument(2, false),
				CodeInstruction.LoadArgument(3, false),
				new CodeInstruction(OpCodes.Call, (object)methodInfo3),
				new CodeInstruction(val.opcode, val.operand)
			});
			return list;
		}

		private static bool LoadsArgument(CodeInstruction instruction, int index)
		{
			if (index == 1 && instruction.opcode == OpCodes.Ldarg_1)
			{
				return true;
			}
			if (instruction.opcode == OpCodes.Ldarg || instruction.opcode == OpCodes.Ldarg_S)
			{
				return Convert.ToInt32(instruction.operand) == index;
			}
			return false;
		}

		private static int LocalIndex(CodeInstruction instruction, bool store)
		{
			OpCode opcode = instruction.opcode;
			if (opcode == (store ? OpCodes.Stloc_0 : OpCodes.Ldloc_0))
			{
				return 0;
			}
			if (opcode == (store ? OpCodes.Stloc_1 : OpCodes.Ldloc_1))
			{
				return 1;
			}
			if (opcode == (store ? OpCodes.Stloc_2 : OpCodes.Ldloc_2))
			{
				return 2;
			}
			if (opcode == (store ? OpCodes.Stloc_3 : OpCodes.Ldloc_3))
			{
				return 3;
			}
			if (opcode != (store ? OpCodes.Stloc : OpCodes.Ldloc) && opcode != (store ? OpCodes.Stloc_S : OpCodes.Ldloc_S))
			{
				return -1;
			}
			if (instruction.operand is LocalBuilder localBuilder)
			{
				return localBuilder.LocalIndex;
			}
			if (instruction.operand is LocalVariableInfo localVariableInfo)
			{
				return localVariableInfo.LocalIndex;
			}
			return Convert.ToInt32(instruction.operand);
		}
	}
	internal static class DigitalStorageTradeUtility
	{
		internal static IEnumerable<Thing> CoreContents(Map map, ITrader trader)
		{
			object obj;
			if (map == null)
			{
				obj = null;
			}
			else
			{
				HaulDestinationManager haulDestinationManager = map.haulDestinationManager;
				obj = ((haulDestinationManager != null) ? haulDestinationManager.AllHaulSourcesListForReading : null);
			}
			List<IHaulSource> sources = (List<IHaulSource>)obj;
			if (sources == null)
			{
				yield break;
			}
			for (int i = 0; i < sources.Count; i++)
			{
				if (!(sources[i] is Building_StorageCore { IsUsableNow: not false, HaulSourceEnabled: not false } building_StorageCore))
				{
					continue;
				}
				ThingOwner held = building_StorageCore.GetDirectlyHeldThings();
				if (held == null)
				{
					continue;
				}
				for (int j = 0; j < held.Count; j++)
				{
					Thing val = held[j];
					if (SellableContent(val, trader))
					{
						yield return val;
					}
				}
			}
		}

		internal static Thing PaymentThing(Thing groundThing, ThingDef resDef, Map map, TradeShip trader)
		{
			if (groundThing != null)
			{
				return groundThing;
			}
			object obj;
			if (map == null)
			{
				obj = null;
			}
			else
			{
				HaulDestinationManager haulDestinationManager = map.haulDestinationManager;
				obj = ((haulDestinationManager != null) ? haulDestinationManager.AllHaulSourcesListForReading : null);
			}
			List<IHaulSource> list = (List<IHaulSource>)obj;
			if (list == null)
			{
				return null;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (!(list[i] is Building_StorageCore { IsUsableNow: not false, HaulSourceEnabled: not false } building_StorageCore))
				{
					continue;
				}
				ThingOwner directlyHeldThings = building_StorageCore.GetDirectlyHeldThings();
				if (directlyHeldThings == null)
				{
					continue;
				}
				for (int j = 0; j < directlyHeldThings.Count; j++)
				{
					Thing val = directlyHeldThings[j];
					if (val != null && val.def == resDef && SellableContent(val, (ITrader)(object)trader))
					{
						return val;
					}
				}
			}
			return null;
		}

		private static bool SellableContent(Thing thing, ITrader trader)
		{
			if (thing != null && !thing.Destroyed && thing.def != null && thing.stackCount > 0 && !(thing is Pawn))
			{
				return TradeUtility.PlayerSellableNow(thing, trader);
			}
			return false;
		}
	}
	internal sealed class ThingIdentityComparer : IEqualityComparer<Thing>
	{
		internal static readonly ThingIdentityComparer Instance = new ThingIdentityComparer();

		public bool Equals(Thing left, Thing right)
		{
			return left == right;
		}

		public int GetHashCode(Thing thing)
		{
			if (thing != null)
			{
				return RuntimeHelpers.GetHashCode(thing);
			}
			return 0;
		}
	}
	[HarmonyPatch(typeof(TradeDeal), "InSellablePosition")]
	internal static class Patch_TradeDeal_InSellablePosition
	{
		private static bool Prefix(Thing t, out string reason, ref bool __result)
		{
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			Building_StorageCore building_StorageCore = t.ParentHolder as Building_StorageCore;
			if (t.Spawned || building_StorageCore == null)
			{
				reason = null;
				return true;
			}
			if (!building_StorageCore.IsUsableNow || !building_StorageCore.HaulSourceEnabled)
			{
				reason = null;
				__result = false;
				return false;
			}
			IntVec3 positionHeld = t.PositionHeld;
			Map mapHeld = t.MapHeld;
			Room room = RegionAndRoomQuery.GetRoom(t.SpawnedParentOrMe, (RegionType)15);
			if (mapHeld == null || GridsUtility.Fogged(positionHeld, mapHeld))
			{
				reason = null;
				__result = false;
				return false;
			}
			if (room != null)
			{
				int num = GenRadial.NumCellsInRadius(6.9f);
				for (int i = 0; i < num; i++)
				{
					IntVec3 val = positionHeld + GenRadial.RadialPattern[i];
					if (!GenGrid.InBounds(val, mapHeld) || GridsUtility.GetRoom(val, mapHeld) != room)
					{
						continue;
					}
					List<Thing> thingList = GridsUtility.GetThingList(val, mapHeld);
					for (int j = 0; j < thingList.Count; j++)
					{
						if (thingList[j].PreventPlayerSellingThingsNearby(ref reason))
						{
							__result = false;
							return false;
						}
					}
				}
			}
			reason = null;
			__result = true;
			return false;
		}
	}
	[HarmonyPatch(typeof(TradeDeal), "TryExecute")]
	internal static class Patch_TradeDeal_TryExecute_Preflight
	{
		private const int MaxListedDefs = 4;

		private static void Prefix(TradeDeal __instance)
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			if (__instance == null || TradeSession.giftMode)
			{
				return;
			}
			try
			{
				string text = ClampOversold(__instance);
				if (text != null)
				{
					Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_TradePreflight_Clamped", NamedArgument.op_Implicit(text))), MessageTypeDefOf.CautionInput, false);
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 交易结算前夹卖单失败（已放行原版流程）: " + ex, 382250);
			}
		}

		private static string ClampOversold(TradeDeal deal)
		{
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Invalid comparison between Unknown and I4
			List<Tradeable> allTradeables = deal.AllTradeables;
			if (allTradeables == null)
			{
				return null;
			}
			StringBuilder stringBuilder = null;
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < allTradeables.Count; i++)
			{
				Tradeable val = allTradeables[i];
				if (val == null || val.IsCurrency || (int)val.ActionToDo != 2)
				{
					continue;
				}
				int countToTransferToDestination = ((Transferable)val).CountToTransferToDestination;
				if (countToTransferToDestination <= 0)
				{
					continue;
				}
				int num3 = CountColonyLeft(val);
				if (num3 >= countToTransferToDestination)
				{
					continue;
				}
				((Transferable)val).ForceToDestination(num3);
				num2++;
				if (num < 4)
				{
					ThingDef thingDef = ((Transferable)val).ThingDef;
					string value = ((Def)(thingDef?)).label ?? ((Def)(thingDef?)).defName ?? "?";
					if (stringBuilder == null)
					{
						stringBuilder = new StringBuilder();
					}
					else
					{
						stringBuilder.Append(", ");
					}
					stringBuilder.Append(value).Append(' ').Append(num3)
						.Append('/')
						.Append(countToTransferToDestination);
					num++;
				}
			}
			if (num2 == 0)
			{
				return null;
			}
			if (stringBuilder == null)
			{
				return "...";
			}
			if (num2 > num)
			{
				stringBuilder.Append(", ...");
			}
			return stringBuilder.ToString();
		}

		private static int CountColonyLeft(Tradeable t)
		{
			List<Thing> thingsColony = t.thingsColony;
			if (thingsColony == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < thingsColony.Count; i++)
			{
				Thing val = thingsColony[i];
				if (val != null && !val.Destroyed)
				{
					num += val.stackCount;
				}
			}
			return num;
		}
	}
	[HarmonyPatch(typeof(Settlement_TraderTracker), "ColonyThingsWillingToBuy")]
	internal static class Patch_SettlementTrade_FromCore
	{
		private static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result)
		{
			return WorldTradeFromCore.Append(__result);
		}
	}
	[HarmonyPatch(typeof(Caravan_TraderTracker), "ColonyThingsWillingToBuy")]
	internal static class Patch_CaravanTrade_FromCore
	{
		private static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result)
		{
			return WorldTradeFromCore.Append(__result);
		}
	}
	internal static class WorldTradeFromCore
	{
		internal static IEnumerable<Thing> Append(IEnumerable<Thing> original)
		{
			HashSet<Thing> yielded = new HashSet<Thing>(ThingIdentityComparer.Instance);
			if (original != null)
			{
				foreach (Thing item in original)
				{
					if (yielded.Add(item))
					{
						yield return item;
					}
				}
			}
			List<Thing> additions = Collect();
			if (additions == null)
			{
				yield break;
			}
			for (int i = 0; i < additions.Count; i++)
			{
				Thing val = additions[i];
				if (yielded.Add(val))
				{
					yield return val;
				}
			}
		}

		private static List<Thing> Collect()
		{
			try
			{
				List<Thing> list = new List<Thing>();
				List<Map> maps = Find.Maps;
				if (maps == null)
				{
					return list;
				}
				ITrader trader = TradeSession.trader;
				for (int i = 0; i < maps.Count; i++)
				{
					list.AddRange(DigitalStorageTradeUtility.CoreContents(maps[i], trader));
				}
				return list;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 追加核心库存到世界地图交易列表时抛异常（已降级为只显示原版内容，交易列表不会因此变空）: " + ex, 382251);
				return null;
			}
		}
	}
	[HarmonyPatch(typeof(HaulAIUtility), "PawnCanAutomaticallyHaul")]
	internal static class Patch_HaulAIUtility_PawnCanAutomaticallyHaul
	{
		private static bool Prefix(Pawn p, Thing t, bool forced, ref bool __result)
		{
			if (p == null || t == null || t.Map == null)
			{
				__result = false;
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(HaulAIUtility), "PawnCanAutomaticallyHaulFast_NewTemp")]
	internal static class Patch_HaulAIUtility_PawnCanAutomaticallyHaulFast
	{
		private static bool Prefix(Pawn p, Thing t, bool forced, bool checkReachability, ref bool __result)
		{
			if (p == null || t == null || t.MapHeld == null)
			{
				__result = false;
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(Toils_Haul), "StartCarryThing")]
	internal static class Patch_StartCarryThing_CorePower
	{
		private static void Postfix(TargetIndex haulableInd, Toil __result)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			__result.AddFailCondition((Func<bool>)delegate
			{
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				//IL_0016: Unknown result type (might be due to invalid IL or missing references)
				//IL_001b: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = __result.actor.CurJob.GetTarget(haulableInd);
				Thing thing = ((LocalTargetInfo)(ref target)).Thing;
				return ((thing != null) ? thing.ParentHolder : null) is Building_StorageCore building_StorageCore && !building_StorageCore.HaulSourceEnabled;
			});
		}
	}
	[HarmonyPatch(typeof(JobDriver_Wear), "MakeNewToils")]
	internal static class Patch_JobDriver_Wear_CorePower
	{
		private static IEnumerable<Toil> Postfix(IEnumerable<Toil> __result, JobDriver_Wear __instance)
		{
			foreach (Toil item in __result)
			{
				item.AddFailCondition((Func<bool>)delegate
				{
					//IL_000c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0011: Unknown result type (might be due to invalid IL or missing references)
					LocalTargetInfo target = ((JobDriver)__instance).job.GetTarget((TargetIndex)1);
					Thing thing = ((LocalTargetInfo)(ref target)).Thing;
					return ((thing != null) ? thing.ParentHolder : null) is Building_StorageCore building_StorageCore && !building_StorageCore.ApparelSourceEnabled;
				});
				yield return item;
			}
		}
	}
	internal static class EmptyWasteFallback
	{
		internal static Job TryMakeToCore(Pawn pawn, Thing container, bool forced)
		{
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0105: Unknown result type (might be due to invalid IL or missing references)
			//IL_010b: Unknown result type (might be due to invalid IL or missing references)
			if (((pawn != null) ? ((Thing)pawn).Map : null) == null || container == null)
			{
				return null;
			}
			if (container.Map != ((Thing)pawn).Map)
			{
				return null;
			}
			if (!ModLister.BiotechInstalled)
			{
				return null;
			}
			CompWasteProducer val = ThingCompUtility.TryGetComp<CompWasteProducer>(container);
			if (val == null || !val.CanEmptyNow)
			{
				return null;
			}
			Thing waste = val.Waste;
			if (waste == null || waste.stackCount <= 0)
			{
				return null;
			}
			if (!WasteUtil.IsHazardousWaste(waste))
			{
				return null;
			}
			if (!ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(container), 1, -1, ReservationLayerDefOf.Empty, forced))
			{
				return null;
			}
			IntVec3 val2 = default(IntVec3);
			IHaulDestination val3 = default(IHaulDestination);
			if (!StoreUtility.TryFindBestBetterStorageFor(waste, pawn, ((Thing)pawn).Map, (StoragePriority)0, ((Thing)pawn).Faction, ref val2, ref val3, false))
			{
				return null;
			}
			if (!(val3 is Building_StorageCore building_StorageCore) || ((Thing)building_StorageCore).Destroyed || ((Thing)building_StorageCore).Map != ((Thing)pawn).Map)
			{
				return null;
			}
			if (((Thing)building_StorageCore).Faction != null && ((Thing)building_StorageCore).Faction != ((Thing)pawn).Faction)
			{
				return null;
			}
			if (ForbidUtility.IsForbidden((Thing)(object)building_StorageCore, pawn))
			{
				return null;
			}
			if (!building_StorageCore.Accepts(waste))
			{
				return null;
			}
			if (!ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit((Thing)(object)building_StorageCore), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0))
			{
				return null;
			}
			Job obj = JobMaker.MakeJob(DigitalStorage_JobDefOf.DigitalStorage_EmptyWasteToCore, LocalTargetInfo.op_Implicit(container), LocalTargetInfo.op_Implicit((Thing)(object)building_StorageCore), LocalTargetInfo.op_Implicit(waste));
			obj.count = waste.stackCount;
			return obj;
		}

		internal static Job TryMakeCell(Pawn pawn, Thing container, bool forced)
		{
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			if (((pawn != null) ? ((Thing)pawn).Map : null) == null || container == null)
			{
				return null;
			}
			if (container.Map != ((Thing)pawn).Map)
			{
				return null;
			}
			if (!ModLister.BiotechInstalled)
			{
				return null;
			}
			CompWasteProducer val = ThingCompUtility.TryGetComp<CompWasteProducer>(container);
			if (val == null || !val.CanEmptyNow)
			{
				return null;
			}
			Thing waste = val.Waste;
			if (waste == null || waste.stackCount <= 0)
			{
				return null;
			}
			if (!ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(container), 1, -1, ReservationLayerDefOf.Empty, forced))
			{
				return null;
			}
			IntVec3 val2 = default(IntVec3);
			if (!StoreUtility.TryFindStoreCellNearColonyDesperate(waste, pawn, ref val2))
			{
				return null;
			}
			if (!((IntVec3)(ref val2)).IsValid)
			{
				return null;
			}
			Job obj = JobMaker.MakeJob(JobDefOf.EmptyWasteContainer, LocalTargetInfo.op_Implicit(container), LocalTargetInfo.op_Implicit(waste), LocalTargetInfo.op_Implicit(val2));
			obj.count = waste.stackCount;
			return obj;
		}
	}
	[HarmonyPatch(typeof(WorkGiver_EmptyWasteContainer), "HasJobOnThing")]
	internal static class Patch_EmptyWasteContainerFallback_HasJob
	{
		private static void Postfix(Pawn pawn, Thing t, bool forced, ref bool __result)
		{
			if (__result)
			{
				return;
			}
			try
			{
				if (EmptyWasteFallback.TryMakeToCore(pawn, t, forced) != null || EmptyWasteFallback.TryMakeCell(pawn, t, forced) != null)
				{
					__result = true;
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 清空废料容器兜底判定失败（本次仍按原版处理）：" + ex, 1146307601);
			}
		}
	}
	[HarmonyPatch(typeof(WorkGiver_EmptyWasteContainer), "JobOnThing")]
	internal static class Patch_EmptyWasteContainerFallback_Job
	{
		private static void Postfix(Pawn pawn, Thing t, bool forced, ref Job __result)
		{
			try
			{
				Job val = EmptyWasteFallback.TryMakeToCore(pawn, t, forced);
				if (val != null)
				{
					__result = val;
				}
				else if (__result == null || !((LocalTargetInfo)(ref __result.targetC)).IsValid)
				{
					Job val2 = EmptyWasteFallback.TryMakeCell(pawn, t, forced);
					if (val2 != null)
					{
						__result = val2;
					}
					else if (__result != null && !((LocalTargetInfo)(ref __result.targetC)).IsValid)
					{
						__result = null;
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 清空废料容器兜底建 job 失败（本次仍按原版处理）：" + ex, 1146307602);
			}
		}
	}
	[HarmonyPatch(typeof(WorkGiver_DoBill), "ThingIsUsableBillGiver")]
	internal static class Patch_BenchWhitelist_Relax
	{
		private static bool Prefix(Thing thing, ref bool __result)
		{
			if (!BenchWhitelistScope.Relaxed)
			{
				return true;
			}
			if (!(thing is Building))
			{
				return true;
			}
			__result = true;
			return false;
		}
	}
	[HarmonyPatch(typeof(JobDriver_HaulToContainer), "TryMakePreToilReservations")]
	internal static class Patch_HaulToContainer_ConcurrentCore
	{
		private static bool Prefix(JobDriver_HaulToContainer __instance, bool errorOnFailed, ref bool __result)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			if (!DigitalStorageSettings.concurrentCoreDelivery)
			{
				return true;
			}
			Job job = ((JobDriver)__instance).job;
			if (job == null)
			{
				return true;
			}
			LocalTargetInfo target = job.GetTarget((TargetIndex)2);
			if (!(((LocalTargetInfo)(ref target)).Thing is Building_StorageCore))
			{
				return true;
			}
			Pawn pawn = ((JobDriver)__instance).pawn;
			if (pawn == null)
			{
				return true;
			}
			if (!ReservationUtility.Reserve(pawn, job.GetTarget((TargetIndex)1), job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false))
			{
				__result = false;
				return false;
			}
			ReservationUtility.ReserveAsManyAsPossible(pawn, job.GetTargetQueue((TargetIndex)1), job, 1, -1, (ReservationLayerDef)null);
			__result = true;
			return false;
		}
	}
	internal static class Patch_CarryFromCore_MarkWithdrawn
	{
		[HarmonyPatch(typeof(Pawn_CarryTracker), "TryStartCarry", new Type[]
		{
			typeof(Thing),
			typeof(int),
			typeof(bool)
		})]
		internal static class ThreeArg
		{
			private static void Postfix(Pawn_CarryTracker __instance, Thing item, int count, bool reserve, ref int __result)
			{
				if (__result > 0)
				{
					MarkIfFromCore(__instance, item);
				}
			}
		}

		[HarmonyPatch(typeof(Pawn_CarryTracker), "TryStartCarry", new Type[] { typeof(Thing) })]
		internal static class OneArg
		{
			private static void Postfix(Pawn_CarryTracker __instance, Thing item, ref bool __result)
			{
				if (__result)
				{
					MarkIfFromCore(__instance, item);
				}
			}
		}

		private static void MarkIfFromCore(Pawn_CarryTracker tracker, Thing source)
		{
			if (tracker != null && source != null && source.ParentHolder is Building_StorageCore)
			{
				Thing carriedThing = tracker.CarriedThing;
				if (carriedThing != null && !carriedThing.Destroyed)
				{
					CompAutoIngest.MarkWithdrawn(carriedThing);
				}
				else if (!source.Destroyed)
				{
					CompAutoIngest.MarkWithdrawn(source);
				}
			}
		}
	}
	internal static class JobDriver_Equip_ContainerCompat
	{
		internal static Thing ContainerHolding(Job job)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			if (job == null)
			{
				return null;
			}
			LocalTargetInfo target = job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing == null || thing.Spawned)
			{
				return null;
			}
			IThingHolder parentHolder = thing.ParentHolder;
			Thing val = (Thing)(object)((parentHolder is Thing) ? parentHolder : null);
			if (val == null)
			{
				return null;
			}
			if (val is Building_OutfitStand)
			{
				return null;
			}
			if (!(val is IHaulSource))
			{
				return null;
			}
			return val;
		}
	}
	[HarmonyPatch(typeof(JobDriver_Equip), "Notify_Starting")]
	internal static class Patch_JobDriver_Equip_NotifyStarting
	{
		[HarmonyPostfix]
		internal static void Postfix(JobDriver_Equip __instance)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			Thing val = JobDriver_Equip_ContainerCompat.ContainerHolding(((JobDriver)__instance).job);
			if (val != null)
			{
				((JobDriver)__instance).job.targetB = LocalTargetInfo.op_Implicit(val);
			}
		}
	}
	[HarmonyPatch(typeof(JobDriver_Equip), "MakeNewToils")]
	internal static class Patch_JobDriver_Equip_MakeNewToils
	{
		[HarmonyPrefix]
		internal static bool Prefix(JobDriver_Equip __instance, ref IEnumerable<Toil> __result)
		{
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			Thing val = JobDriver_Equip_ContainerCompat.ContainerHolding(((JobDriver)__instance).job);
			if (val == null)
			{
				return true;
			}
			if (!((LocalTargetInfo)(ref ((JobDriver)__instance).job.targetB)).IsValid)
			{
				((JobDriver)__instance).job.targetB = LocalTargetInfo.op_Implicit(val);
			}
			__result = ToilsFromContainer(__instance, val);
			return false;
		}

		private static IEnumerable<Toil> ToilsFromContainer(JobDriver_Equip driver, Thing holder)
		{
			Job job = ((JobDriver)driver).job;
			Pawn pawn = ((JobDriver)driver).pawn;
			yield return Toils_General.Do((Action)delegate
			{
				pawn.mindState.droppedWeapon = null;
			});
			yield return ToilFailConditions.FailOn<Toil>(ToilFailConditions.FailOnDespawnedNullOrForbidden<Toil>(Toils_Goto.GotoThing((TargetIndex)2, (PathEndMode)3, false), (TargetIndex)2), (Func<bool>)(() => holder is Building_StorageCore building_StorageCore && !building_StorageCore.HaulSourceEnabled));
			Toil val = ToilMaker.MakeToil("DS_EquipFromContainer");
			val.initAction = delegate
			{
				//IL_002b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0030: Unknown result type (might be due to invalid IL or missing references)
				//IL_0104: Unknown result type (might be due to invalid IL or missing references)
				//IL_0115: Unknown result type (might be due to invalid IL or missing references)
				//IL_011a: Unknown result type (might be due to invalid IL or missing references)
				if (holder is Building_StorageCore { HaulSourceEnabled: false })
				{
					((JobDriver)driver).EndJobWith((JobCondition)4);
				}
				else
				{
					LocalTargetInfo target = job.GetTarget((TargetIndex)1);
					Thing thing = ((LocalTargetInfo)(ref target)).Thing;
					ThingWithComps val2 = (ThingWithComps)(object)((thing is ThingWithComps) ? thing : null);
					if (val2 == null || ((Thing)val2).Destroyed)
					{
						((JobDriver)driver).EndJobWith((JobCondition)64);
					}
					else
					{
						Thing obj = holder;
						Thing obj2 = ((obj is IThingHolder) ? obj : null);
						ThingOwner val3 = ((obj2 != null) ? ((IThingHolder)obj2).GetDirectlyHeldThings() : null);
						if (val3 == null || !val3.Contains((Thing)(object)val2))
						{
							((JobDriver)driver).EndJobWith((JobCondition)64);
						}
						else
						{
							ThingWithComps val4;
							if (((Thing)val2).def.stackLimit > 1 && ((Thing)val2).stackCount > 1)
							{
								Thing obj3 = val3.Take((Thing)(object)val2, 1);
								val4 = (ThingWithComps)(object)((obj3 is ThingWithComps) ? obj3 : null);
							}
							else
							{
								val3.Remove((Thing)(object)val2);
								val4 = val2;
							}
							if (val4 == null)
							{
								((JobDriver)driver).EndJobWith((JobCondition)64);
							}
							else
							{
								pawn.equipment.MakeRoomFor(val4);
								pawn.equipment.AddEquipment(val4);
								SoundDef soundInteract = ((Thing)val4).def.soundInteract;
								if (soundInteract != null)
								{
									SoundStarter.PlayOneShot(soundInteract, SoundInfo.op_Implicit(new TargetInfo(((Thing)pawn).Position, ((Thing)pawn).Map, false)));
								}
							}
						}
					}
				}
			};
			val.defaultCompleteMode = (ToilCompleteMode)1;
			yield return val;
		}
	}
	internal static class Patch_HemogenAdministerFromCore
	{
		internal static bool GatesBeforePackSearchPass(Pawn doctor, Thing target, bool forced)
		{
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			if (!ConsumePatchUtil.PawnMayUseCore(doctor))
			{
				return false;
			}
			Pawn val = (Pawn)(object)((target is Pawn) ? target : null);
			if (val == null || val == doctor)
			{
				return false;
			}
			Pawn_GeneTracker genes = val.genes;
			Gene_Hemogen val2 = ((genes != null) ? genes.GetFirstGeneOfType<Gene_Hemogen>() : null);
			if (val2 == null)
			{
				return false;
			}
			if (((Gene_Resource)val2).ValuePercent >= 0.95f)
			{
				return false;
			}
			if (!forced && ((Gene_Resource)val2).Value >= 0.25f)
			{
				return false;
			}
			if (!FeedPatientUtility.ShouldBeFed(val))
			{
				return false;
			}
			if (!val2.hemogenPacksAllowed)
			{
				return false;
			}
			if (!val2.ShouldConsumeHemogenNow())
			{
				return false;
			}
			if (!ReservationUtility.CanReserve(doctor, LocalTargetInfo.op_Implicit(target), 1, -1, (ReservationLayerDef)null, forced))
			{
				return false;
			}
			return true;
		}

		internal static bool SpawnedPackReachable(Pawn doctor)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			return GenClosest.ClosestThingReachable(((Thing)doctor).Position, ((Thing)doctor).Map, ThingRequest.ForDef(ThingDefOf.HemogenPack), (PathEndMode)1, TraverseParms.For(doctor, (Danger)3, (TraverseMode)0, false, false, false, true), 9999f, (Predicate<Thing>)((Thing pack) => !ForbidUtility.IsForbidden(pack, doctor) && ReservationUtility.CanReserve(doctor, LocalTargetInfo.op_Implicit(pack), 1, -1, (ReservationLayerDef)null, false)), (IEnumerable<Thing>)null, 0, -1, false, (RegionType)14, false, false) != null;
		}

		internal static Thing FindPackInStorage(Pawn doctor)
		{
			if (((Thing)doctor).MapHeld == null)
			{
				return null;
			}
			return HaulSourceContents.FindBestIncludingRemote(((Thing)doctor).MapHeld, null, (Thing t) => t.def == ThingDefOf.HemogenPack && !ForbidUtility.IsForbidden(t, doctor) && ReservationUtility.CanReserve(doctor, LocalTargetInfo.op_Implicit(t), 1, -1, (ReservationLayerDef)null, false));
		}
	}
	[HarmonyPatch(typeof(Workgiver_AdministerHemogen), "HasJobOnThing")]
	internal static class Patch_HemogenAdminister_HasJobOnThing
	{
		private static void Postfix(Pawn pawn, Thing t, bool forced, ref bool __result)
		{
			if (!__result && ((pawn != null) ? ((Thing)pawn).MapHeld : null) != null && Patch_HemogenAdministerFromCore.GatesBeforePackSearchPass(pawn, t, forced) && !Patch_HemogenAdministerFromCore.SpawnedPackReachable(pawn) && HaulSourceContents.CountOf(((Thing)pawn).MapHeld, ThingDefOf.HemogenPack) > 0)
			{
				__result = true;
			}
		}
	}
	[HarmonyPatch(typeof(Workgiver_AdministerHemogen), "JobOnThing")]
	internal static class Patch_HemogenAdminister_JobOnThing
	{
		private static void Postfix(Pawn pawn, Thing t, bool forced, ref Job __result)
		{
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			if (__result != null || ((pawn != null) ? ((Thing)pawn).MapHeld : null) == null || !Patch_HemogenAdministerFromCore.GatesBeforePackSearchPass(pawn, t, forced))
			{
				return;
			}
			Thing val = Patch_HemogenAdministerFromCore.FindPackInStorage(pawn);
			if (val != null)
			{
				Thing val2 = HaulSourceContents.ExtractToFeet(val, 1, pawn);
				if (val2 != null)
				{
					Job val3 = JobMaker.MakeJob(JobDefOf.FeedPatient, LocalTargetInfo.op_Implicit(val2), LocalTargetInfo.op_Implicit(t));
					val3.count = 1;
					__result = val3;
				}
			}
		}
	}
	[HarmonyPatch(typeof(WorkGiver_Warden_DeliverHemogen), "JobOnThing")]
	internal static class Patch_WardenDeliverHemogenFromCore
	{
		private struct Prepared
		{
			public Thing Pack;

			public Pawn Prisoner;

			public int Tick;
		}

		private static readonly Dictionary<Pawn, Prepared> preparedByWarden = new Dictionary<Pawn, Prepared>();

		private static void Postfix(Pawn pawn, Thing t, bool forced, ref Job __result)
		{
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
			if (__result != null || ((pawn != null) ? ((Thing)pawn).MapHeld : null) == null || !ConsumePatchUtil.PawnMayUseCore(pawn) || !ModsConfig.BiotechActive)
			{
				return;
			}
			Pawn val = (Pawn)(object)((t is Pawn) ? t : null);
			if (val == null || !ShouldTakeCareOfPrisoner(pawn, val, forced) || !val.guest.CanBeBroughtFood || !GridsUtility.IsInPrisonCell(((Thing)val).Position, ((Thing)val).Map) || WardenFeedUtility.ShouldBeFed(val))
			{
				return;
			}
			Pawn_GeneTracker genes = val.genes;
			Gene obj = ((genes != null) ? genes.GetGene(GeneDefOf.Hemogenic) : null);
			Gene_Hemogen val2 = (Gene_Hemogen)(object)((obj is Gene_Hemogen) ? obj : null);
			if (val2 != null && val2.hemogenPacksAllowed && val2.ShouldConsumeHemogenNow() && !PackAlreadyAvailableFor(val))
			{
				Room room = RegionAndRoomQuery.GetRoom((Thing)(object)val, (RegionType)15);
				Thing val3 = TakeUsablePack(pawn, val, room);
				if (val3 != null)
				{
					Job val4 = JobMaker.MakeJob(JobDefOf.DeliverFood, LocalTargetInfo.op_Implicit(val3), LocalTargetInfo.op_Implicit((Thing)(object)val));
					val4.count = 1;
					val4.targetC = LocalTargetInfo.op_Implicit(RCellFinder.SpotToChewStandingNear(val, val3, (Predicate<IntVec3>)null));
					__result = val4;
				}
			}
		}

		private static Thing TakeUsablePack(Pawn warden, Pawn prisoner, Room prisonerRoom)
		{
			int ticksGame = Find.TickManager.TicksGame;
			if (preparedByWarden.TryGetValue(warden, out var value))
			{
				preparedByWarden.Remove(warden);
				if (value.Pack != null && !value.Pack.Destroyed && value.Pack.Spawned && value.Pack.Map == ((Thing)warden).MapHeld && value.Prisoner == prisoner && ticksGame - value.Tick <= 2)
				{
					return value.Pack;
				}
			}
			Thing val = HaulSourceContents.FindBestIncludingRemote(((Thing)warden).MapHeld, null, (Thing x) => x.def == ThingDefOf.HemogenPack && !ForbidUtility.IsForbidden(x, warden) && ReservationUtility.CanReserve(warden, LocalTargetInfo.op_Implicit(x), 1, -1, (ReservationLayerDef)null, false) && RoomOfStorage(x) != prisonerRoom);
			if (val == null)
			{
				return null;
			}
			Thing val2 = HaulSourceContents.ExtractToFeet(val, 1, warden);
			if (val2 == null)
			{
				return null;
			}
			preparedByWarden[warden] = new Prepared
			{
				Pack = val2,
				Prisoner = prisoner,
				Tick = ticksGame
			};
			return val2;
		}

		private static bool ShouldTakeCareOfPrisoner(Pawn warden, Pawn prisoner, bool forced)
		{
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			if (!prisoner.IsPrisonerOfColony || !prisoner.guest.PrisonerIsSecure || !((Thing)prisoner).Spawned || prisoner.InAggroMentalState || ForbidUtility.IsForbidden((Thing)(object)prisoner, warden) || CaravanFormingUtility.IsFormingCaravan(prisoner))
			{
				return false;
			}
			return ReservationUtility.CanReserveAndReach(warden, LocalTargetInfo.op_Implicit((Thing)(object)prisoner), (PathEndMode)1, DangerUtility.NormalMaxDanger(warden), 1, -1, (ReservationLayerDef)null, forced);
		}

		private static bool PackAlreadyAvailableFor(Pawn prisoner)
		{
			if (prisoner.carryTracker.CarriedCount(ThingDefOf.HemogenPack) > 0)
			{
				return true;
			}
			if (prisoner.inventory.Count(ThingDefOf.HemogenPack) > 0)
			{
				return true;
			}
			Room room = RegionAndRoomQuery.GetRoom((Thing)(object)prisoner, (RegionType)15);
			if (room != null)
			{
				List<Region> regions = room.Regions;
				for (int i = 0; i < regions.Count; i++)
				{
					if (regions[i].ListerThings.ThingsOfDef(ThingDefOf.HemogenPack).Count > 0)
					{
						return true;
					}
				}
			}
			return false;
		}

		private static Room RoomOfStorage(Thing stored)
		{
			IThingHolder obj = ((stored != null) ? stored.ParentHolder : null);
			IThingHolder obj2 = ((obj is Thing) ? obj : null);
			if (obj2 == null)
			{
				return null;
			}
			return RegionAndRoomQuery.GetRoom((Thing)(object)obj2, (RegionType)15);
		}
	}
	[HarmonyPatch(typeof(JobGiver_GetHemogen), "GetHemogenPack")]
	internal static class Patch_GetHemogenPackFromCore
	{
		private static void Postfix(Pawn pawn, ref Thing __result)
		{
			if (__result != null)
			{
				return;
			}
			Pawn obj = pawn;
			if (((obj != null) ? ((Thing)obj).MapHeld : null) == null || !ConsumePatchUtil.PawnMayUseCore(pawn))
			{
				return;
			}
			Pawn_GeneTracker genes = pawn.genes;
			Gene_Hemogen val = ((genes != null) ? genes.GetFirstGeneOfType<Gene_Hemogen>() : null);
			if (val == null)
			{
				return;
			}
			int num = Mathf.FloorToInt((((Gene_Resource)val).Max - ((Gene_Resource)val).Value) / JobGiver_GetHemogen.HemogenPackHemogenGain);
			if (num <= 0)
			{
				return;
			}
			Thing val2 = HaulSourceContents.FindBestIncludingRemote(((Thing)pawn).MapHeld, null, (Thing t) => t.def == ThingDefOf.HemogenPack && !ForbidUtility.IsForbidden(t, pawn) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(t), 1, -1, (ReservationLayerDef)null, false));
			if (val2 != null)
			{
				Thing val3 = HaulSourceContents.ExtractToFeet(val2, Mathf.Min(num, val2.stackCount), pawn);
				if (val3 != null)
				{
					__result = val3;
				}
			}
		}
	}
	internal static class Patch_BloodTransfusionFromCore
	{
		internal static int CountPacksInStorage(Map map)
		{
			return HaulSourceContents.CountOf(map, ThingDefOf.HemogenPack);
		}
	}
	[HarmonyPatch(typeof(Recipe_BloodTransfusion), "AvailableOnNow")]
	internal static class Patch_BloodTransfusion_AvailableOnNow
	{
		private static void Postfix(Recipe_BloodTransfusion __instance, Thing thing, ref bool __result)
		{
			if (__result)
			{
				return;
			}
			Pawn val = (Pawn)(object)((thing is Pawn) ? thing : null);
			if (val == null || val.health?.hediffSet == null)
			{
				return;
			}
			Map mapHeld = thing.MapHeld;
			if (mapHeld != null && val.health.hediffSet.HasHediff(HediffDefOf.BloodLoss, false))
			{
				RecipeDef recipe = ((RecipeWorker)__instance).recipe;
				if ((recipe == null || BaseSurgeryChecksPass(recipe, val)) && mapHeld.listerThings.ThingsOfDef(ThingDefOf.HemogenPack).Count <= 0 && Patch_BloodTransfusionFromCore.CountPacksInStorage(mapHeld) > 0)
				{
					__result = true;
				}
			}
		}

		private static bool BaseSurgeryChecksPass(RecipeDef recipe, Pawn pawn)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			if ((((??)recipe.genderPrerequisite) ?? pawn.gender) != pawn.gender)
			{
				return false;
			}
			if (recipe.mustBeFertile && pawn.Sterile())
			{
				return false;
			}
			if (!recipe.allowedForQuestLodgers && QuestUtility.IsQuestLodger(pawn))
			{
				return false;
			}
			if (recipe.minAllowedAge > 0 && pawn.ageTracker.AgeBiologicalYears < recipe.minAllowedAge)
			{
				return false;
			}
			if (recipe.developmentalStageFilter.HasValue && !DevelopmentalStageExtensions.Has(recipe.developmentalStageFilter.Value, pawn.DevelopmentalStage))
			{
				return false;
			}
			if (recipe.humanlikeOnly && !pawn.RaceProps.Humanlike)
			{
				return false;
			}
			if (ModsConfig.AnomalyActive)
			{
				if (recipe.mutantBlacklist != null && pawn.IsMutant && recipe.mutantBlacklist.Contains(pawn.mutant.Def))
				{
					return false;
				}
				if (recipe.mutantPrerequisite != null && (!pawn.IsMutant || !recipe.mutantPrerequisite.Contains(pawn.mutant.Def)))
				{
					return false;
				}
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(Recipe_BloodTransfusion), "GetIngredientCount")]
	internal static class Patch_BloodTransfusion_GetIngredientCount
	{
		private static void Postfix(IngredientCount ing, Bill bill, ref float __result)
		{
			if (((bill != null) ? bill.Map : null) == null || ing?.filter == null || !ing.filter.Allows(ThingDefOf.HemogenPack))
			{
				return;
			}
			IBillGiver obj = bill.billStack?.billGiver;
			Pawn val = (Pawn)(object)((obj is Pawn) ? obj : null);
			if (val == null || val.health?.hediffSet == null)
			{
				return;
			}
			Hediff firstHediffOfDef = val.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss, false);
			if (firstHediffOfDef == null)
			{
				return;
			}
			float num = firstHediffOfDef.Severity / 0.35f;
			if (!(__result >= num))
			{
				int num2 = Patch_BloodTransfusionFromCore.CountPacksInStorage(bill.Map);
				if (num2 > 0)
				{
					__result = Mathf.Min(__result + (float)num2, num);
				}
			}
		}
	}
	internal static class Patch_TakeToInventoryFromCore
	{
		internal static Thing FindInStorage(Pawn pawn, Predicate<Thing> validator)
		{
			if (((pawn != null) ? ((Thing)pawn).MapHeld : null) == null || validator == null)
			{
				return null;
			}
			return HaulSourceContents.FindBestIncludingRemote(((Thing)pawn).MapHeld, null, validator);
		}

		internal static Thing TakeToFeet(Pawn pawn, Thing found, int count)
		{
			if (found == null || count <= 0)
			{
				return null;
			}
			return HaulSourceContents.ExtractToFeet(found, Mathf.Min(count, found.stackCount), pawn);
		}
	}
	[HarmonyPatch(typeof(JobGiver_MoveDrugsToInventory), "FindDrugFor")]
	internal static class Patch_MoveDrugsToInventoryFromCore
	{
		private static void Postfix(Pawn pawn, ThingDef drugDef, int desired, ref Thing __result)
		{
			if (__result != null || drugDef == null || desired <= 0 || !ConsumePatchUtil.PawnMayUseCore(pawn) || !drugDef.IsDrug)
			{
				return;
			}
			Thing val = Patch_TakeToInventoryFromCore.FindInStorage(pawn, (Thing x) => x.def == drugDef && !ForbidUtility.IsForbidden(x, pawn) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(x), 10, Mathf.Min(x.stackCount, desired), (ReservationLayerDef)null, false));
			if (val != null)
			{
				Thing val2 = Patch_TakeToInventoryFromCore.TakeToFeet(pawn, val, desired);
				if (val2 != null)
				{
					__result = val2;
				}
			}
		}
	}
	[HarmonyPatch(typeof(JobGiver_TakeForInventoryStock), "FindThingFor")]
	internal static class Patch_InventoryStockFromCore
	{
		private static void Postfix(Pawn pawn, ThingDef thingDef, ref Thing __result)
		{
			if (__result != null || thingDef == null || !ConsumePatchUtil.PawnMayUseCore(pawn))
			{
				return;
			}
			int num = DesiredFor(pawn, thingDef);
			if (num <= 0)
			{
				return;
			}
			Thing val = Patch_TakeToInventoryFromCore.FindInStorage(pawn, (Thing x) => x.def == thingDef && !ForbidUtility.IsForbidden(x, pawn) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(x), 10, -1, (ReservationLayerDef)null, false));
			if (val != null)
			{
				Thing val2 = Patch_TakeToInventoryFromCore.TakeToFeet(pawn, val, num);
				if (val2 != null)
				{
					__result = val2;
				}
			}
		}

		private static int DesiredFor(Pawn pawn, ThingDef thingDef)
		{
			Pawn_InventoryStockTracker inventoryStock = pawn.inventoryStock;
			if (inventoryStock?.stockEntries == null)
			{
				return 0;
			}
			foreach (InventoryStockEntry value in inventoryStock.stockEntries.Values)
			{
				if (value?.thingDef == thingDef)
				{
					return value.count - pawn.inventory.Count(thingDef);
				}
			}
			return 0;
		}
	}
	internal static class PaintFromCore
	{
		private const int MaxDyePerTrip = 10;

		private static readonly MethodInfo ShouldPaintThingMi = AccessTools.Method(typeof(WorkGiver_PaintBuilding), "ShouldPaintThing", (Type[])null, (Type[])null);

		private static readonly MethodInfo ShouldPaintCellMi = AccessTools.Method(typeof(WorkGiver_PaintFloor), "ShouldPaintCell", (Type[])null, (Type[])null);

		private static int memoTick = -1;

		private static Map memoMap;

		private static Thing memoDye;

		internal static MethodInfo BuildingGateMi => ShouldPaintThingMi;

		internal static MethodInfo FloorGateMi => ShouldPaintCellMi;

		internal static int PreferredDyePerTrip => 10;

		internal static Thing FindDyeInStorage(Pawn pawn)
		{
			Pawn obj = pawn;
			Map val = ((obj != null) ? ((Thing)obj).MapHeld : null);
			if (val == null)
			{
				return null;
			}
			int ticksGame = Find.TickManager.TicksGame;
			if (memoMap == val && memoTick == ticksGame)
			{
				if (memoDye != null && !memoDye.Destroyed && memoDye.stackCount > 0)
				{
					return memoDye;
				}
				memoDye = null;
			}
			else
			{
				memoMap = val;
				memoTick = ticksGame;
				memoDye = HaulSourceContents.FindBestIncludingRemote(val, null, (Thing t) => t.def == ThingDefOf.Dye && !ForbidUtility.IsForbidden(t, pawn));
			}
			return memoDye;
		}

		internal static bool CanPaintWithStoredDye(Pawn pawn)
		{
			return FindDyeInStorage(pawn) != null;
		}

		internal static Thing MaterializeDye(Pawn pawn, int maxAmount)
		{
			Thing val = FindDyeInStorage(pawn);
			if (val == null)
			{
				return null;
			}
			int count = Mathf.Min(Mathf.Max(1, maxAmount), val.stackCount);
			Thing val2 = HaulSourceContents.ExtractToFeet(val, count, pawn);
			if (val2 == null)
			{
				return null;
			}
			memoDye = null;
			memoTick = -1;
			if (DigitalStorageSettings.enableDebugLog && ConsumePatchUtil.ShouldLog("PaintFromCore"))
			{
				Log.Message("[DigitalStorage] 粉饰从核心取染料: " + ((Def)val2.def).defName + " x" + val2.stackCount + " → " + ((Entity)pawn).LabelShort);
			}
			return val2;
		}

		internal static bool RerunWithoutDyeGate(MethodInfo mi, object giver, Pawn pawn, object target, bool forced)
		{
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			if (mi == null || giver == null)
			{
				return false;
			}
			try
			{
				if (mi.Invoke(giver, new object[4] { pawn, target, forced, false }) is AcceptanceReport val)
				{
					return ((AcceptanceReport)(ref val)).Accepted;
				}
			}
			catch (Exception ex)
			{
				Log.WarningOnce("[DigitalStorage] 粉饰判据重跑失败: " + ex.Message, 0x5A17C0 ^ ((Thing)pawn).thingIDNumber);
			}
			return false;
		}
	}
	[HarmonyPatch(typeof(PaintUtility), "FindNearbyDyes")]
	internal static class Patch_PaintUtility_FindNearbyDyes
	{
		private static void Postfix(Pawn pawn, ref List<Thing> __result)
		{
			if ((__result == null || __result.Count <= 0) && pawn != null && ConsumePatchUtil.PawnMayUseCore(pawn))
			{
				Thing val = PaintFromCore.MaterializeDye(pawn, PaintFromCore.PreferredDyePerTrip);
				if (val != null)
				{
					__result = new List<Thing> { val };
				}
			}
		}
	}
	[HarmonyPatch(typeof(WorkGiver_PaintBuilding), "ShouldPaintThing")]
	internal static class Patch_WorkGiver_PaintBuilding_ShouldPaintThing
	{
		private static void Postfix(WorkGiver_PaintBuilding __instance, Pawn pawn, Thing t, bool forced, bool checkDye, ref AcceptanceReport __result)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			if (!AcceptanceReport.op_Implicit(__result) && checkDye && pawn != null && ConsumePatchUtil.PawnMayUseCore(pawn) && PaintFromCore.CanPaintWithStoredDye(pawn) && PaintFromCore.RerunWithoutDyeGate(PaintFromCore.BuildingGateMi, __instance, pawn, t, forced))
			{
				__result = AcceptanceReport.WasAccepted;
			}
		}
	}
	[HarmonyPatch(typeof(WorkGiver_PaintFloor), "ShouldPaintCell")]
	internal static class Patch_WorkGiver_PaintFloor_ShouldPaintCell
	{
		private static void Postfix(WorkGiver_PaintFloor __instance, Pawn pawn, IntVec3 c, bool forced, bool checkDye, ref AcceptanceReport __result)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			if (!AcceptanceReport.op_Implicit(__result) && checkDye && pawn != null && ConsumePatchUtil.PawnMayUseCore(pawn) && PaintFromCore.CanPaintWithStoredDye(pawn) && PaintFromCore.RerunWithoutDyeGate(PaintFromCore.FloorGateMi, __instance, pawn, c, forced))
			{
				__result = AcceptanceReport.WasAccepted;
			}
		}
	}
	internal static class DyeFromCore
	{
		internal static bool IdeologyDyeActive => ModsConfig.IdeologyActive;

		internal static bool HasUsableGroundDye(Pawn pawn)
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			List<Thing> list = ((Thing)pawn).Map.listerThings.ThingsOfDef(ThingDefOf.Dye);
			for (int i = 0; i < list.Count; i++)
			{
				if (!ForbidUtility.IsForbidden(list[i], pawn) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(list[i]), 1, 1, (ReservationLayerDef)null, false))
				{
					return true;
				}
			}
			return false;
		}

		internal static bool HasReachableStylingStation(Pawn pawn)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			return GenClosest.ClosestThing_Global_Reachable(((Thing)pawn).Position, ((Thing)pawn).Map, (IEnumerable<Thing>)((Thing)pawn).Map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.StylingStation), (PathEndMode)4, TraverseParms.For(pawn, (Danger)3, (TraverseMode)0, false, false, false, true), 9999f, (Predicate<Thing>)((Thing x) => !ForbidUtility.IsForbidden(x, pawn) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(x), 1, -1, (ReservationLayerDef)null, false)), (Func<Thing, float>)null, false) != null;
		}

		internal static bool HasReachableStylingStationForRecolor(Pawn pawn)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			return GenClosest.ClosestThingReachable(((Thing)pawn).Position, ((Thing)pawn).Map, ThingRequest.ForDef(ThingDefOf.StylingStation), (PathEndMode)2, TraverseParms.For(pawn, (Danger)3, (TraverseMode)0, false, false, false, true), 9999f, (Predicate<Thing>)((Thing t) => !ForbidUtility.IsForbidden(t, pawn) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(t), 1, -1, (ReservationLayerDef)null, false) && ReservationUtility.CanReserveSittableOrSpot(pawn, t.InteractionCell, false)), (IEnumerable<Thing>)null, 0, -1, false, (RegionType)14, false, false) != null;
		}
	}
	[HarmonyPatch(typeof(JobGiver_DyeHair), "TryGiveJob")]
	internal static class Patch_JobGiver_DyeHair_TryGiveJob
	{
		private static void Prefix(Pawn pawn)
		{
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			if (((pawn != null) ? ((Thing)pawn).Map : null) != null && DyeFromCore.IdeologyDyeActive && pawn.style != null && pawn.story != null && pawn.style.nextHairColor.HasValue)
			{
				Color? nextHairColor = pawn.style.nextHairColor;
				Color hairColor = pawn.story.HairColor;
				if ((!nextHairColor.HasValue || !(nextHairColor.GetValueOrDefault() == hairColor)) && ConsumePatchUtil.PawnMayUseCore(pawn) && !DyeFromCore.HasUsableGroundDye(pawn) && DyeFromCore.HasReachableStylingStation(pawn))
				{
					PaintFromCore.MaterializeDye(pawn, 1);
				}
			}
		}
	}
	[HarmonyPatch(typeof(JobGiver_OptimizeApparel), "TryCreateRecolorJob")]
	internal static class Patch_JobGiver_OptimizeApparel_TryCreateRecolorJob
	{
		private static void Prefix(Pawn pawn, bool dryRun)
		{
			if (dryRun || ((pawn != null) ? ((Thing)pawn).Map : null) == null || pawn.apparel == null || !DyeFromCore.IdeologyDyeActive || !pawn.apparel.AnyApparelNeedsRecoloring || !ConsumePatchUtil.PawnMayUseCore(pawn) || DyeFromCore.HasUsableGroundDye(pawn) || !DyeFromCore.HasReachableStylingStationForRecolor(pawn))
			{
				return;
			}
			int num = 0;
			List<Apparel> wornApparel = pawn.apparel.WornApparel;
			for (int i = 0; i < wornApparel.Count; i++)
			{
				if (wornApparel[i].DesiredColor.HasValue)
				{
					num++;
				}
			}
			if (num > 0)
			{
				PaintFromCore.MaterializeDye(pawn, num);
			}
		}
	}
	[HarmonyPatch(typeof(WorkGiver_FillFermentingBarrel), "FindWort")]
	internal static class Patch_FillFermentingBarrel_FindWort
	{
		private static void Postfix(Pawn pawn, Building_FermentingBarrel barrel, ref Thing __result)
		{
			if (__result != null)
			{
				return;
			}
			Pawn obj = pawn;
			if (((obj != null) ? ((Thing)obj).Map : null) == null || barrel == null || !ConsumePatchUtil.PawnMayUseCore(pawn))
			{
				return;
			}
			int spaceLeftForWort = barrel.SpaceLeftForWort;
			if (spaceLeftForWort <= 0)
			{
				return;
			}
			Thing val = HaulSourceContents.FindBestIncludingRemote(((Thing)pawn).Map, null, (Thing t) => t.def == ThingDefOf.Wort && !ForbidUtility.IsForbidden(t, pawn));
			if (val == null)
			{
				return;
			}
			Thing val2 = HaulSourceContents.ExtractToFeet(val, Mathf.Min(spaceLeftForWort, val.stackCount), pawn);
			if (val2 != null)
			{
				if (DigitalStorageSettings.enableDebugLog && ConsumePatchUtil.ShouldLog("WortFromCore"))
				{
					Log.Message("[DigitalStorage] 发酵桶从核心取麦芽汁: x" + val2.stackCount + " → " + ((Entity)pawn).LabelShort);
				}
				__result = val2;
			}
		}
	}
	[HarmonyPatch(typeof(WorkGiver_CreateXenogerm), "FindArchiteCapsule")]
	internal static class Patch_CreateXenogerm_FindArchiteCapsule
	{
		private static void Postfix(Pawn pawn, ref Thing __result)
		{
			if (__result != null)
			{
				return;
			}
			Pawn obj = pawn;
			if (((obj != null) ? ((Thing)obj).Map : null) != null && ModsConfig.BiotechActive && ConsumePatchUtil.PawnMayUseCore(pawn))
			{
				Thing val = HaulSourceContents.FindBestIncludingRemote(((Thing)pawn).Map, null, (Thing t) => t.def == ThingDefOf.ArchiteCapsule && !ForbidUtility.IsForbidden(t, pawn));
				if (val != null)
				{
					__result = val;
				}
			}
		}
	}
	[HarmonyPatch(typeof(WorkGiver_HaulToBiosculpterPod), "FindNutrition")]
	internal static class Patch_HaulToBiosculpterPod_FindNutrition
	{
		private static void Postfix(Pawn pawn, CompBiosculpterPod pod, ref ThingCount __result)
		{
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			if (((ThingCount)(ref __result)).Thing != null)
			{
				return;
			}
			Pawn obj = pawn;
			if (((obj != null) ? ((Thing)obj).Map : null) == null || pod == null || !ConsumePatchUtil.PawnMayUseCore(pawn))
			{
				return;
			}
			float requiredNutritionRemaining = pod.RequiredNutritionRemaining;
			if (requiredNutritionRemaining <= 0f)
			{
				return;
			}
			Thing val = HaulSourceContents.FindBestIncludingRemote(((Thing)pawn).Map, null, (Thing t) => !ForbidUtility.IsForbidden(t, pawn) && t.def.IsNutritionGivingIngestible && NutritionOf(t) > 0f && pod.CanAcceptNutrition(t) && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(t), 1, -1, (ReservationLayerDef)null, false));
			if (val != null)
			{
				float num = NutritionOf(val);
				int num2 = Mathf.Clamp(Mathf.CeilToInt(requiredNutritionRemaining / num), 1, val.stackCount);
				__result = new ThingCount(val, num2, false);
				if (DigitalStorageSettings.enableDebugLog && ConsumePatchUtil.ShouldLog("BiosculpterFromCore"))
				{
					Log.Message("[DigitalStorage] 塑形舱从核心取营养: " + ((Def)val.def).defName + " x" + num2 + " (缺 " + requiredNutritionRemaining.ToString("0.##") + " 营养) → " + ((Entity)pawn).LabelShort);
				}
			}
		}

		private static float NutritionOf(Thing t)
		{
			return StatExtension.GetStatValue(t, StatDefOf.Nutrition, true, -1);
		}
	}
	[HarmonyPatch(typeof(HaulDestinationManager), "RemoveHaulDestination")]
	internal static class Patch_HaulDestinationManager_RemoveGuard
	{
		private static bool Prefix(HaulDestinationManager __instance, IHaulDestination haulDestination)
		{
			if (__instance == null || haulDestination == null)
			{
				return true;
			}
			if (__instance.AllHaulDestinationsListForReading.Contains(haulDestination))
			{
				return true;
			}
			if (Prefs.DevMode)
			{
				Log.Message("[DigitalStorage] 跳过未登记搬运目的地的重复移除（原版此处报红字）：" + Gen.ToStringSafe<IHaulDestination>(haulDestination));
			}
			return false;
		}
	}
	[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestIngredientsHelper")]
	internal static class Patch_WorkGiver_DoBill_RemoteIngredients
	{
		private static void Prefix(Pawn pawn, Predicate<Thing> thingValidator, ref Predicate<List<Thing>> foundAllIngredientsAndChoose)
		{
			if (!Patch_WorkGiver_DoBill_BillSearchScope.InBillSearch)
			{
				return;
			}
			Predicate<List<Thing>> original = foundAllIngredientsAndChoose;
			if (original == null || pawn == null || ((Thing)pawn).Map == null)
			{
				return;
			}
			List<Map> maps = Find.Maps;
			if (maps == null || maps.Count <= 1)
			{
				return;
			}
			bool remoteTried = false;
			foundAllIngredientsAndChoose = delegate(List<Thing> found)
			{
				if (found == null)
				{
					return original(found);
				}
				if (original(found))
				{
					return true;
				}
				if (remoteTried)
				{
					return false;
				}
				remoteTried = true;
				int num = RemoteIngredientProvider.Append(found, pawn, thingValidator);
				if (num <= 0)
				{
					return false;
				}
				if (DigitalStorageSettings.enableDebugLog)
				{
					Log.Warning("[DS] 跨图取料：本图凑不齐，从其它地图的核心追加 " + num + " 件候选原料");
				}
				return original(found);
			};
		}
	}
	[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
	internal static class Patch_WorkGiver_DoBill_BillSearchScope
	{
		[ThreadStatic]
		internal static bool InBillSearch;

		private static void Prefix(out bool? __state)
		{
			__state = InBillSearch;
			InBillSearch = true;
		}

		private static Exception Finalizer(Exception __exception, bool? __state)
		{
			if (__state.HasValue)
			{
				InBillSearch = __state.Value;
			}
			return __exception;
		}
	}
	[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestFixedIngredients")]
	internal static class Patch_WorkGiver_DoBill_FixedSearchScope
	{
		private static void Prefix(out bool? __state)
		{
			__state = Patch_WorkGiver_DoBill_BillSearchScope.InBillSearch;
			Patch_WorkGiver_DoBill_BillSearchScope.InBillSearch = false;
		}

		private static Exception Finalizer(Exception __exception, bool? __state)
		{
			if (__state.HasValue)
			{
				Patch_WorkGiver_DoBill_BillSearchScope.InBillSearch = __state.Value;
			}
			return __exception;
		}
	}
	internal static class RemoteIngredientProvider
	{
		internal static int Append(List<Thing> found, Pawn pawn, Predicate<Thing> validator)
		{
			if (found == null || pawn == null || validator == null || ((Thing)pawn).Map == null)
			{
				return 0;
			}
			int num = 0;
			List<Map> maps = Find.Maps;
			if (maps == null)
			{
				return 0;
			}
			for (int i = 0; i < maps.Count; i++)
			{
				Map val = maps[i];
				if (val == null || val == ((Thing)pawn).Map)
				{
					continue;
				}
				List<Building_StorageCore> list = CoreFinder.AllUsableCores(val);
				for (int j = 0; j < list.Count; j++)
				{
					ThingOwner directlyHeldThings = list[j].GetDirectlyHeldThings();
					if (directlyHeldThings == null)
					{
						continue;
					}
					for (int k = 0; k < directlyHeldThings.Count; k++)
					{
						Thing val2 = directlyHeldThings[k];
						if (val2 != null && !val2.Destroyed && validator(val2) && !ForbidUtility.IsForbidden(val2, ((Thing)pawn).Faction))
						{
							found.Add(val2);
							num++;
						}
					}
				}
			}
			return num;
		}
	}
	[HarmonyPatch(typeof(TransporterUtility), "MakeLordsAsAppropriate")]
	internal static class Patch_TransporterUtility_MakeLordsAsAppropriate
	{
		private static void Postfix(List<CompTransporter> transporters, Map map)
		{
			ContainerAutoLoader.FillTransporters(transporters, map);
		}
	}
	[HarmonyPatch(typeof(EnterPortalUtility), "MakeLordsAsAppropriate")]
	internal static class Patch_EnterPortalUtility_MakeLordsAsAppropriate
	{
		private static void Postfix(MapPortal portal)
		{
			ContainerAutoLoader.FillPortal(portal);
		}
	}
	[HarmonyPatch(typeof(ReservationManager), "CanReserve", new Type[]
	{
		typeof(Pawn),
		typeof(LocalTargetInfo),
		typeof(int),
		typeof(int),
		typeof(ReservationLayerDef),
		typeof(bool)
	})]
	internal static class Patch_ReservationManager_CanReserve_ProxyClaims
	{
		private static void Postfix(Pawn claimant, LocalTargetInfo target, bool ignoreOtherReservations, ref bool __result)
		{
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			if (!__result || claimant == null || ignoreOtherReservations || !DigitalWorkerClaims.AnyClaims)
			{
				return;
			}
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing == null)
			{
				if (!((LocalTargetInfo)(ref target)).IsValid)
				{
					return;
				}
				Map map = ((Thing)claimant).Map;
				if (map != null)
				{
					CompDigitalWorker compDigitalWorker = DigitalWorkerClaims.CellOwnerOf(map, ((LocalTargetInfo)(ref target)).Cell);
					if (compDigitalWorker != null && compDigitalWorker.WorkerIfCreated != claimant)
					{
						__result = false;
					}
				}
				return;
			}
			Map mapHeld = thing.MapHeld;
			if (mapHeld != null)
			{
				CompDigitalWorker compDigitalWorker2 = DigitalWorkerClaims.OwnerOf(mapHeld, thing);
				if (compDigitalWorker2 != null && compDigitalWorker2.WorkerIfCreated != claimant)
				{
					__result = false;
				}
			}
		}
	}
	[HarmonyPatch(typeof(ReservationManager), "CanReserve", new Type[]
	{
		typeof(Pawn),
		typeof(LocalTargetInfo),
		typeof(int),
		typeof(int),
		typeof(ReservationLayerDef),
		typeof(bool)
	})]
	internal static class Patch_ReservationManager_CanReserve_SharedCore
	{
		private static bool Prefix(Pawn claimant, LocalTargetInfo target, ref bool __result)
		{
			if (!DigitalStorageSettings.concurrentCoreDelivery)
			{
				return true;
			}
			if (!(((LocalTargetInfo)(ref target)).Thing is Building_StorageCore building_StorageCore))
			{
				return true;
			}
			if (claimant == null || !((Thing)claimant).Spawned)
			{
				return true;
			}
			if (((Thing)claimant).Map == null || ((Thing)claimant).Map != ((Thing)building_StorageCore).Map)
			{
				return true;
			}
			if (!((Thing)building_StorageCore).Spawned || ((Thing)building_StorageCore).Destroyed)
			{
				return true;
			}
			if (((LocalTargetInfo)(ref target)).ThingDestroyed)
			{
				return true;
			}
			__result = true;
			return false;
		}
	}
	[HarmonyPatch(typeof(ReservationManager), "CanReserveStack", new Type[]
	{
		typeof(Pawn),
		typeof(LocalTargetInfo),
		typeof(int),
		typeof(ReservationLayerDef),
		typeof(bool)
	})]
	internal static class Patch_ReservationManager_CanReserveStack_SharedCore
	{
		private static bool Prefix(Pawn claimant, LocalTargetInfo target, ref int __result)
		{
			if (!DigitalStorageSettings.concurrentCoreDelivery)
			{
				return true;
			}
			if (!(((LocalTargetInfo)(ref target)).Thing is Building_StorageCore building_StorageCore))
			{
				return true;
			}
			if (claimant == null || !((Thing)claimant).Spawned)
			{
				return true;
			}
			if (((Thing)claimant).Map == null || ((Thing)claimant).Map != ((Thing)building_StorageCore).Map)
			{
				return true;
			}
			if (!((Thing)building_StorageCore).Spawned || ((Thing)building_StorageCore).Destroyed)
			{
				return true;
			}
			if (((LocalTargetInfo)(ref target)).ThingDestroyed)
			{
				return true;
			}
			__result = ((LocalTargetInfo)(ref target)).Thing.stackCount;
			return false;
		}
	}
	internal static class Patch_GenPlace_DropRedirect
	{
		private static readonly Type[] TargetSignature = new Type[9]
		{
			typeof(Thing),
			typeof(IntVec3),
			typeof(Map),
			typeof(ThingPlaceMode),
			typeof(Thing).MakeByRefType(),
			typeof(Action<Thing, int>),
			typeof(Predicate<IntVec3>),
			typeof(Rot4?),
			typeof(int)
		};

		private static bool tookOver;

		private static long stamp;

		internal static void Install(Harmony harmony)
		{
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Expected O, but got Unknown
			//IL_006e: Expected O, but got Unknown
			MethodInfo methodInfo = AccessTools.Method(typeof(GenPlace), "TryPlaceThing", TargetSignature, (Type[])null);
			if (methodInfo == null)
			{
				Log.Error("[DigitalStorage] 找不到 GenPlace.TryPlaceThing(out Thing, …) 重载 —— 掉落直塞未挂上（原版签名变了？本功能会静默退回原版落地行为）。");
			}
			else
			{
				harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(AccessTools.Method(typeof(Patch_GenPlace_DropRedirect), "Prefix", (Type[])null, (Type[])null)), new HarmonyMethod(AccessTools.Method(typeof(Patch_GenPlace_DropRedirect), "Postfix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null);
			}
		}

		private static bool Prefix(Thing thing, Map map, out Thing lastResultingThing, ref bool __result)
		{
			lastResultingThing = null;
			if (DevDrawProfiler.Enabled)
			{
				stamp = DevDrawProfiler.Stamp();
			}
			if (!DigitalDropRedirect.Active)
			{
				return true;
			}
			Building_StorageCore core = DigitalDropRedirect.Core;
			if (core == null || map == null || ((Thing)core).Map != map)
			{
				return true;
			}
			if (!DigitalDropRedirect.TryIngestUnspawned(core, thing))
			{
				return true;
			}
			tookOver = true;
			lastResultingThing = thing;
			__result = true;
			return false;
		}

		private static void Postfix()
		{
			if (DevDrawProfiler.Enabled && stamp != 0L)
			{
				DevDrawProfiler.Add(tookOver ? "DropsDirect" : "Drops", DevDrawProfiler.Ms(stamp));
				DevDrawProfiler.Bump(tookOver ? "直塞" : "落地", 1);
				stamp = 0L;
				tookOver = false;
			}
		}
	}
	[HarmonyPatch]
	internal static class Patch_Building_SubcoreScanner_SupplyFromCore
	{
		private static MethodBase TargetMethod()
		{
			return AccessTools.DeclaredMethod(typeof(Building_SubcoreScanner), "Tick", Type.EmptyTypes, (Type[])null) ?? AccessTools.Method(typeof(Building_SubcoreScanner), "Tick", Type.EmptyTypes, (Type[])null);
		}

		private static void Postfix(Building_SubcoreScanner __instance)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Invalid comparison between Unknown and I4
			if (__instance != null && ((Thing)__instance).Spawned && (int)__instance.State == 1 && (Find.TickManager.TicksGame + ((Thing)__instance).thingIDNumber) % 15 == 0)
			{
				Supply(__instance);
			}
		}

		private static void Supply(Building_SubcoreScanner scanner)
		{
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			List<IngredientCount> list = ((Thing)scanner).def?.building?.subcoreScannerFixedIngredients;
			if (list == null || list.Count == 0)
			{
				return;
			}
			Map map = ((Thing)scanner).Map;
			if (map == null)
			{
				return;
			}
			ReservationManager reservationManager = map.reservationManager;
			if (reservationManager != null && reservationManager.IsReservedByAnyoneOf(LocalTargetInfo.op_Implicit((Thing)(object)scanner), Faction.OfPlayer))
			{
				return;
			}
			for (int i = 0; i < list.Count; i++)
			{
				ThingDef fixedIngredient = list[i].FixedIngredient;
				if (fixedIngredient != null)
				{
					int requiredCountOf = scanner.GetRequiredCountOf(fixedIngredient);
					if (requiredCountOf > 0)
					{
						TakeFromCores(scanner, map, fixedIngredient, requiredCountOf);
					}
				}
			}
		}

		private static void TakeFromCores(Building_SubcoreScanner scanner, Map map, ThingDef def, int need)
		{
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			ThingOwner innerContainer = ((Building_Enterable)scanner).innerContainer;
			if (innerContainer == null)
			{
				return;
			}
			List<Building_StorageCore> list = CoreFinder.AllUsableCores(map);
			if (list.Count == 0)
			{
				return;
			}
			IntVec3 pos = ((Thing)scanner).Position;
			list.Sort(delegate(Building_StorageCore a, Building_StorageCore b)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				//IL_001d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0023: Unknown result type (might be due to invalid IL or missing references)
				//IL_0028: Unknown result type (might be due to invalid IL or missing references)
				//IL_002d: Unknown result type (might be due to invalid IL or missing references)
				IntVec3 val2 = ((Thing)a).Position - pos;
				int lengthHorizontalSquared = ((IntVec3)(ref val2)).LengthHorizontalSquared;
				val2 = ((Thing)b).Position - pos;
				return lengthHorizontalSquared.CompareTo(((IntVec3)(ref val2)).LengthHorizontalSquared);
			});
			ReservationManager reservationManager = map.reservationManager;
			for (int num = 0; num < list.Count; num++)
			{
				if (need <= 0)
				{
					break;
				}
				Building_StorageCore building_StorageCore = list[num];
				if (building_StorageCore == null || !building_StorageCore.IsUsableNow)
				{
					continue;
				}
				ThingOwner directlyHeldThings = building_StorageCore.GetDirectlyHeldThings();
				if (directlyHeldThings == null)
				{
					continue;
				}
				int num2 = directlyHeldThings.Count - 1;
				while (num2 >= 0 && need > 0)
				{
					Thing val = directlyHeldThings[num2];
					if (val != null && !val.Destroyed && val.def == def && (reservationManager == null || !reservationManager.IsReservedByAnyoneOf(LocalTargetInfo.op_Implicit(val), Faction.OfPlayer)))
					{
						int stackCount = val.stackCount;
						if (stackCount > 0)
						{
							directlyHeldThings.TryTransferToContainer(val, innerContainer, Math.Min(need, stackCount), true);
							int num3 = (directlyHeldThings.Contains(val) ? (stackCount - val.stackCount) : stackCount);
							if (num3 > 0)
							{
								need -= num3;
							}
						}
					}
					num2--;
				}
			}
		}
	}
	[HarmonyPatch]
	internal static class Patch_Building_GrowthVat_SupplyFromCore
	{
		private const float NutritionSlack = 2.5f;

		private const int MaxMovesPerPass = 20;

		private static MethodBase TargetMethod()
		{
			return AccessTools.DeclaredMethod(typeof(Building_GrowthVat), "Tick", Type.EmptyTypes, (Type[])null) ?? AccessTools.Method(typeof(Building_GrowthVat), "Tick", Type.EmptyTypes, (Type[])null);
		}

		private static void Postfix(Building_GrowthVat __instance)
		{
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			if (__instance == null || !((Thing)__instance).Spawned || (Find.TickManager.TicksGame + ((Thing)__instance).thingIDNumber) % 15 != 0)
			{
				return;
			}
			Map map = ((Thing)__instance).Map;
			if (map == null)
			{
				return;
			}
			ReservationManager reservationManager = map.reservationManager;
			if (reservationManager == null || !reservationManager.IsReservedByAnyoneOf(LocalTargetInfo.op_Implicit((Thing)(object)__instance), Faction.OfPlayer))
			{
				DeliverEmbryoFromCore(__instance);
				if (__instance.NutritionNeeded > 2.5f)
				{
					SupplyNutrition(__instance, map, reservationManager);
				}
			}
		}

		private static void DeliverEmbryoFromCore(Building_GrowthVat vat)
		{
			HumanEmbryo selectedEmbryo = vat.selectedEmbryo;
			if (selectedEmbryo != null && !((Thing)selectedEmbryo).Destroyed && !((Building_Enterable)vat).innerContainer.Contains((Thing)(object)selectedEmbryo) && ((Thing)selectedEmbryo).ParentHolder is Building_StorageCore)
			{
				CoreSupply.TakeAndPlace((Thing)(object)selectedEmbryo, 1, (Thing taken) => ((Building_Enterable)vat).innerContainer.TryAddOrTransfer(taken, true));
			}
		}

		private static void SupplyNutrition(Building_GrowthVat vat, Map map, ReservationManager resMgr)
		{
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			List<Thing> list = new List<Thing>();
			for (int i = 0; i < 20; i++)
			{
				if (!(vat.NutritionNeeded > 2.5f))
				{
					break;
				}
				float nutritionNeeded = vat.NutritionNeeded;
				if (nutritionNeeded <= 0f)
				{
					break;
				}
				list.Clear();
				CoreSupply.GatherContents(map, ((Thing)vat).Position, list);
				Thing val = null;
				int num = 0;
				for (int j = 0; j < list.Count; j++)
				{
					Thing val2 = list[j];
					if (val2 != null && !val2.Destroyed && val2.ParentHolder is Building_StorageCore && val2.def != null && !ForbidUtility.IsForbidden(val2, Faction.OfPlayer) && (resMgr == null || !resMgr.IsReservedByAnyoneOf(LocalTargetInfo.op_Implicit(val2), Faction.OfPlayer)) && vat.CanAcceptNutrition(val2))
					{
						float statValueAbstract = StatExtension.GetStatValueAbstract((BuildableDef)(object)val2.def, StatDefOf.Nutrition, (ThingDef)null);
						if (!(statValueAbstract <= 0f) && !(statValueAbstract > nutritionNeeded))
						{
							val = val2;
							num = Math.Min(val2.stackCount, Mathf.CeilToInt(nutritionNeeded / statValueAbstract));
							break;
						}
					}
				}
				if (val == null || num <= 0 || !CoreSupply.TakeAndPlace(val, num, (Thing taken) => ((Building_Enterable)vat).innerContainer.TryAddOrTransfer(taken, true)))
				{
					break;
				}
			}
		}
	}
	[HarmonyPatch(typeof(CompPowerTrader), "SetUpPowerVars")]
	internal static class Patch_PowerMultiplier
	{
		private static void Postfix(CompPowerTrader __instance)
		{
			if (__instance != null)
			{
				float powerMultiplier = DigitalStorageSettings.powerMultiplier;
				if (!Mathf.Approximately(powerMultiplier, 1f) && __instance.powerOutputInt < 0f)
				{
					__instance.powerOutputInt *= powerMultiplier;
				}
			}
		}
	}
	[HarmonyPatch(typeof(QualityUtility), "SendCraftNotification")]
	internal static class Patch_QualityUtility_SuppressCraftLetter
	{
		private static bool Prefix(Thing thing, Pawn worker)
		{
			return !BillAutomationScope.SuppressCraftLetters;
		}
	}
	[HarmonyPatch(typeof(Frame), "CompleteConstruction")]
	internal static class Patch_Frame_CompleteConstruction_Foundation
	{
		private static void Prefix(Frame __instance)
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				BuildableDef entityDefToBuild = ((Thing)__instance).def.entityDefToBuild;
				TerrainDef val = (TerrainDef)(object)((entityDefToBuild is TerrainDef) ? entityDefToBuild : null);
				if (val == null || !val.isFoundation)
				{
					return;
				}
				Map map = ((Thing)__instance).Map;
				if (map == null)
				{
					return;
				}
				TerrainGrid terrainGrid = map.terrainGrid;
				IntVec3 position = ((Thing)__instance).Position;
				if (terrainGrid.UnderTerrainAt(position) != null)
				{
					if (terrainGrid.TempTerrainAt(position) != null)
					{
						terrainGrid.RemoveTempTerrain(position, false, false);
					}
					if (terrainGrid.CanRemoveTopLayerAt(position))
					{
						terrainGrid.RemoveTopLayer(position, true);
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 地基铺设前置清理失败（本格退回原版行为，地基可能铺不上）：" + ex, 382254);
			}
		}
	}
}
namespace DigitalStorage.UI
{
	public class ITab_DigitalStorage : ITab
	{
		private struct Row
		{
			public ThingDef def;

			public ThingDef stuff;

			public int qualityOrdinal;

			public string label;

			public int count;

			public ItemGroup group;
		}

		private static readonly Vector2 WinSize = new Vector2(470f, 560f);

		private readonly bool[] expanded = new bool[6];

		private float scrollFrac;

		private string search = "";

		private const float HeaderRowH = 26f;

		private const float ItemRowH = 24f;

		private readonly List<Row> rows = new List<Row>();

		private int cachedTick = -1;

		private int cachedCount = -1;

		private Building_StorageCore Core => ((ITab)this).SelThing as Building_StorageCore;

		public override bool IsVisible => Core != null;

		public ITab_DigitalStorage()
		{
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).size = WinSize;
			((InspectTabBase)this).labelKey = "DS_TabDigitalStorage";
		}

		private static int QualityOrdinalOf(Thing t)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Expected I4, but got Unknown
			CompQuality val = ThingCompUtility.TryGetComp<CompQuality>(t);
			if (val == null)
			{
				return -1;
			}
			return (int)val.Quality;
		}

		private void RebuildRowsIfNeeded(Building_StorageCore core, bool force = false)
		{
			int ticksGame = Find.TickManager.TicksGame;
			int count = ((ThingOwner)core.innerContainer).Count;
			if (!force && ticksGame == cachedTick && count == cachedCount)
			{
				return;
			}
			cachedTick = ticksGame;
			cachedCount = count;
			List<(ThingDef, ThingDef, int)> list = new List<(ThingDef, ThingDef, int)>();
			Dictionary<(ThingDef, ThingDef, int), int> dictionary = new Dictionary<(ThingDef, ThingDef, int), int>();
			Dictionary<(ThingDef, ThingDef, int), string> dictionary2 = new Dictionary<(ThingDef, ThingDef, int), string>();
			for (int i = 0; i < ((ThingOwner)core.innerContainer).Count; i++)
			{
				Thing val = core.innerContainer[i];
				if (val != null && val.def != null && !val.Destroyed)
				{
					(ThingDef, ThingDef, int) tuple = (val.def, val.Stuff, QualityOrdinalOf(val));
					if (dictionary.TryGetValue(tuple, out var value))
					{
						dictionary[tuple] = value + val.stackCount;
						continue;
					}
					dictionary[tuple] = val.stackCount;
					dictionary2[tuple] = BuildLabel(val);
					list.Add(tuple);
				}
			}
			rows.Clear();
			for (int j = 0; j < list.Count; j++)
			{
				(ThingDef, ThingDef, int) key = list[j];
				Row item = new Row
				{
					def = key.Item1,
					stuff = key.Item2,
					qualityOrdinal = key.Item3,
					label = dictionary2[key],
					count = dictionary[key],
					group = ItemGrouping.GroupOf(key.Item1)
				};
				rows.Add(item);
			}
			rows.Sort((Row a, Row b) => string.Compare(a.label, b.label, StringComparison.Ordinal));
		}

		private static string BuildLabel(Thing t)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			string text;
			try
			{
				text = t.LabelNoCount;
			}
			catch
			{
				text = TaggedString.op_Implicit(((Def)t.def).LabelCap);
			}
			if (t.def.useHitPoints && t.MaxHitPoints > 0)
			{
				text = text + "  " + GenText.ToStringPercent((float)t.HitPoints / (float)t.MaxHitPoints);
			}
			return text;
		}

		protected override void FillTab()
		{
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_014e: Unknown result type (might be due to invalid IL or missing references)
			//IL_015d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0164: Unknown result type (might be due to invalid IL or missing references)
			//IL_016b: Unknown result type (might be due to invalid IL or missing references)
			//IL_017b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0180: Unknown result type (might be due to invalid IL or missing references)
			//IL_0185: Unknown result type (might be due to invalid IL or missing references)
			//IL_0199: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0214: Unknown result type (might be due to invalid IL or missing references)
			//IL_021f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0301: Unknown result type (might be due to invalid IL or missing references)
			//IL_033e: Unknown result type (might be due to invalid IL or missing references)
			//IL_025a: Unknown result type (might be due to invalid IL or missing references)
			//IL_025f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0272: Unknown result type (might be due to invalid IL or missing references)
			//IL_0274: Unknown result type (might be due to invalid IL or missing references)
			//IL_027b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0286: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b3: Expected O, but got Unknown
			//IL_02da: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e4: Expected O, but got Unknown
			try
			{
				Building_StorageCore core = Core;
				if (core == null)
				{
					return;
				}
				RebuildRowsIfNeeded(core);
				AE2Draw.HandlePauseHotkey();
				Rect val = GenUI.ContractedBy(new Rect(0f, 0f, WinSize.x, WinSize.y), 4f);
				AE2Draw.PanelBox(val);
				Rect val2 = GenUI.ContractedBy(val, 6f);
				float y = ((Rect)(ref val2)).y;
				Rect r = default(Rect);
				((Rect)(ref r))..ctor(((Rect)(ref val2)).x, y, ((Rect)(ref val2)).width - 100f, 22f);
				if (AE2Draw.TextButton(new Rect(((Rect)(ref val2)).xMax - 96f, y, 96f, 22f), ((object)Translator.Translate("DS_AE2_OpenCraft")/*cast due to constrained. prefix*/).ToString()))
				{
					Find.WindowStack.Add((Window)(object)new Window_AE2CraftPanel(NearestProxy(core)));
				}
				int count = ((ThingOwner)core.innerContainer).Count;
				int maxStacks = core.maxStacks;
				AE2Draw.Bar(r, (maxStacks > 0) ? Mathf.Clamp01((float)count / (float)maxStacks) : 0f, selected: false);
				AE2Draw.TextIn(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y + 3f, ((Rect)(ref r)).width, 16f), ((object)TranslatorFormattedStringExtensions.Translate("DS_CapacityBarLv", NamedArgument.op_Implicit(CoreTier.Level), NamedArgument.op_Implicit(count), NamedArgument.op_Implicit(maxStacks), NamedArgument.op_Implicit(rows.Count))/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.Hi, (TextAnchor)3);
				y += 26f;
				AE2Draw.TextIn(new Rect(((Rect)(ref val2)).x, y + 3f, 54f, 16f), ((object)Translator.Translate("DS_Priority")/*cast due to constrained. prefix*/).ToString() + ":", 1f, AE2Draw.TextCol, (TextAnchor)3);
				if (AE2Draw.TextButton(new Rect(((Rect)(ref val2)).x + 56f, y, 150f, 22f), PriorityLabel(core.storagePriority)))
				{
					List<FloatMenuOption> list = new List<FloatMenuOption>();
					foreach (StoragePriority value in Enum.GetValues(typeof(StoragePriority)))
					{
						StoragePriority priority = value;
						if ((int)priority != 0)
						{
							list.Add(new FloatMenuOption(PriorityLabel(priority), (Action)delegate
							{
								//IL_000c: Unknown result type (might be due to invalid IL or missing references)
								core.storagePriority = priority;
							}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
						}
					}
					Find.WindowStack.Add((Window)new FloatMenu(list));
				}
				y += 26f;
				search = AE2Draw.TextField(new Rect(((Rect)(ref val2)).x, y, ((Rect)(ref val2)).width, 24f), search);
				y += 28f;
				Rect list2 = default(Rect);
				((Rect)(ref list2))..ctor(((Rect)(ref val2)).x, y, ((Rect)(ref val2)).width, ((Rect)(ref val2)).yMax - y);
				DrawList(list2, core);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] AE2 界面绘制异常（只记一次，界面不会卡死）：" + ex, 771005);
			}
		}

		private void DrawList(Rect list, Building_StorageCore core)
		{
			//IL_017b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0192: Unknown result type (might be due to invalid IL or missing references)
			//IL_0197: Unknown result type (might be due to invalid IL or missing references)
			//IL_05ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_0611: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0446: Unknown result type (might be due to invalid IL or missing references)
			//IL_044f: Unknown result type (might be due to invalid IL or missing references)
			//IL_047d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0482: Unknown result type (might be due to invalid IL or missing references)
			//IL_048e: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0518: Unknown result type (might be due to invalid IL or missing references)
			//IL_0547: Unknown result type (might be due to invalid IL or missing references)
			//IL_0551: Unknown result type (might be due to invalid IL or missing references)
			//IL_0556: Unknown result type (might be due to invalid IL or missing references)
			//IL_026e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0277: Unknown result type (might be due to invalid IL or missing references)
			//IL_0284: Unknown result type (might be due to invalid IL or missing references)
			//IL_027d: Unknown result type (might be due to invalid IL or missing references)
			//IL_033d: Unknown result type (might be due to invalid IL or missing references)
			//IL_035f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0364: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_03ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_03cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_03da: Unknown result type (might be due to invalid IL or missing references)
			List<int> list2 = new List<int>();
			List<int> list3 = new List<int>();
			List<float> list4 = new List<float>();
			float num = 0f;
			for (int i = 0; i < 6; i++)
			{
				ItemGroup itemGroup = (ItemGroup)i;
				int num2 = 0;
				long num3 = 0L;
				for (int j = 0; j < rows.Count; j++)
				{
					if (rows[j].group == itemGroup && RowMatchesSearch(rows[j]))
					{
						num2++;
						num3 += rows[j].count;
					}
				}
				if (num2 == 0)
				{
					continue;
				}
				list2.Add(i);
				list3.Add(-1);
				list4.Add(num);
				num += 28f;
				if (!expanded[i])
				{
					continue;
				}
				for (int k = 0; k < rows.Count; k++)
				{
					if (rows[k].group == itemGroup && RowMatchesSearch(rows[k]))
					{
						list2.Add(-1);
						list3.Add(k);
						list4.Add(num);
						num += 25f;
					}
				}
			}
			if (num <= 0f)
			{
				AE2Draw.TextIn(new Rect(((Rect)(ref list)).x + 4f, ((Rect)(ref list)).y + 4f, ((Rect)(ref list)).width - 8f, 18f), (rows.Count == 0) ? ((object)Translator.Translate("DS_EmptyCoreText")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("DS_KindsEmpty")/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextDimCol, (TextAnchor)3);
				return;
			}
			float height = ((Rect)(ref list)).height;
			float num4 = Mathf.Max(0f, num - height);
			float num5 = Mathf.Round(scrollFrac * num4);
			Rect val = default(Rect);
			Rect val2 = default(Rect);
			for (int l = 0; l < list4.Count; l++)
			{
				float num6 = list4[l] - num5;
				if (num6 + 26f < 0f)
				{
					continue;
				}
				if (num6 > height)
				{
					break;
				}
				if (list2[l] >= 0)
				{
					int num7 = list2[l];
					((Rect)(ref val))..ctor(((Rect)(ref list)).x, ((Rect)(ref list)).y + num6, ((Rect)(ref list)).width - 14f, 26f);
					bool flag = Mouse.IsOver(val);
					AE2Draw.Sunken(val, flag ? AE2Draw.SlotHover : AE2Draw.Slot);
					int num8 = 0;
					long num9 = 0L;
					for (int m = 0; m < rows.Count; m++)
					{
						if (rows[m].group == (ItemGroup)num7 && RowMatchesSearch(rows[m]))
						{
							num8++;
							num9 += rows[m].count;
						}
					}
					string text = (expanded[num7] ? "▼" : "▶");
					AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 5f, ((Rect)(ref val)).y + 5f, ((Rect)(ref val)).width - 10f, 16f), text + " " + ((object)Translator.Translate(ItemGrouping.LabelKeyOf((ItemGroup)num7))/*cast due to constrained. prefix*/).ToString() + "   ×" + num9 + "   (" + num8 + " " + ((object)Translator.Translate("DS_Kinds")/*cast due to constrained. prefix*/).ToString() + ")", 1f, AE2Draw.Hi, (TextAnchor)3);
					if (Widgets.ButtonInvisible(val, true))
					{
						expanded[num7] = !expanded[num7];
					}
					continue;
				}
				Row row = rows[list3[l]];
				((Rect)(ref val2))..ctor(((Rect)(ref list)).x + 10f, ((Rect)(ref list)).y + num6, ((Rect)(ref list)).width - 24f, 24f);
				bool hover = Mouse.IsOver(val2);
				AE2Draw.Row(val2, hover, selected: false);
				Rect val3 = new Rect(((Rect)(ref val2)).x + 2f, ((Rect)(ref val2)).y + 1f, 22f, 22f);
				AE2Draw.SlotBox(val3, hover: false);
				Widgets.ThingIcon(GenUI.ContractedBy(val3, 1f), row.def, row.stuff, (ThingStyleDef)null, 1f, (Color?)null, (int?)null, 1f);
				AE2Draw.TextIn(new Rect(((Rect)(ref val2)).x + 28f, ((Rect)(ref val2)).y + 4f, ((Rect)(ref val2)).width - 140f, 16f), row.label + "   ×" + row.count, 1f, AE2Draw.TextCol, (TextAnchor)3);
				if (AE2Draw.TextButton(new Rect(((Rect)(ref val2)).xMax - 92f, ((Rect)(ref val2)).y + 1f, 90f, 22f), ((object)Translator.Translate("DS_WithdrawBtn")/*cast due to constrained. prefix*/).ToString()))
				{
					Row captured = row;
					Find.WindowStack.Add((Window)(object)new Dialog_WithdrawAmount(captured.label, captured.count, 0, delegate(int amount)
					{
						ExtractRow(core, captured, amount);
					}));
				}
			}
			AE2Draw.DragBar(new Rect(((Rect)(ref list)).xMax - 12f, ((Rect)(ref list)).y, 12f, ((Rect)(ref list)).height), ref scrollFrac, Mathf.Clamp01(height / Mathf.Max(1f, num)));
			AE2Draw.WheelScroll(list, ref scrollFrac);
		}

		private bool RowMatchesSearch(Row row)
		{
			if (string.IsNullOrEmpty(search))
			{
				return true;
			}
			return row.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static void ExtractRow(Building_StorageCore core, Row row, int amount)
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			Map map = ((Thing)core).Map;
			if (map != null && HaulSourceContents.ExtractMatchingTo(core, amount, ((Thing)core).Position, map, (Thing t) => t.def == row.def && t.Stuff == row.stuff && QualityOrdinalOf(t) == row.qualityOrdinal, forbid: false) <= 0)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("DS_NoSpaceNearCore")), LookTargets.op_Implicit((Thing)(object)core), MessageTypeDefOf.RejectInput, true);
			}
		}

		private static CompBillAutomation NearestProxy(Building_StorageCore core)
		{
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			Map val = ((core != null && ((Thing)core).Map != null) ? ((Thing)core).Map : Find.CurrentMap);
			if (val == null)
			{
				return null;
			}
			CompBillAutomation result = null;
			float num = float.MaxValue;
			List<Thing> allThings = val.listerThings.AllThings;
			for (int i = 0; i < allThings.Count; i++)
			{
				Thing val2 = allThings[i];
				if (val2 == null || !val2.Spawned)
				{
					continue;
				}
				CompBillAutomation compBillAutomation = ThingCompUtility.TryGetComp<CompBillAutomation>(val2);
				if (compBillAutomation != null)
				{
					float num2 = ((core != null) ? IntVec3Utility.DistanceTo(val2.Position, ((Thing)core).Position) : 0f);
					if (num2 < num)
					{
						num = num2;
						result = compBillAutomation;
					}
				}
			}
			return result;
		}

		private unsafe static string PriorityLabel(StoragePriority p)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Expected I4, but got Unknown
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			return (p - 1) switch
			{
				0 => TaggedString.op_Implicit(Translator.Translate("StoragePriorityLow")), 
				1 => TaggedString.op_Implicit(Translator.Translate("StoragePriorityNormal")), 
				2 => TaggedString.op_Implicit(Translator.Translate("StoragePriorityPreferred")), 
				3 => TaggedString.op_Implicit(Translator.Translate("StoragePriorityImportant")), 
				4 => TaggedString.op_Implicit(Translator.Translate("StoragePriorityCritical")), 
				_ => ((object)(*(StoragePriority*)(&p))/*cast due to constrained. prefix*/).ToString(), 
			};
		}
	}
	public class ITab_BillAutomation : ITab
	{
		private static readonly Vector2 WinSize = new Vector2(430f, 330f);

		private const float RowH = 22f;

		private float scroll;

		private CompBillAutomation Comp
		{
			get
			{
				if (((ITab)this).SelThing != null)
				{
					return ThingCompUtility.TryGetComp<CompBillAutomation>(((ITab)this).SelThing);
				}
				return null;
			}
		}

		public override bool IsVisible => Comp != null;

		public ITab_BillAutomation()
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			((InspectTabBase)this).size = WinSize;
			((InspectTabBase)this).labelKey = "DS_TabBillAutomation";
		}

		protected override void FillTab()
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_010d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0118: Unknown result type (might be due to invalid IL or missing references)
			//IL_011d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0122: Unknown result type (might be due to invalid IL or missing references)
			//IL_0160: Unknown result type (might be due to invalid IL or missing references)
			//IL_0170: Unknown result type (might be due to invalid IL or missing references)
			//IL_0175: Unknown result type (might be due to invalid IL or missing references)
			//IL_017a: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_021a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0234: Unknown result type (might be due to invalid IL or missing references)
			//IL_024d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0266: Unknown result type (might be due to invalid IL or missing references)
			//IL_026b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0270: Unknown result type (might be due to invalid IL or missing references)
			//IL_0284: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0304: Unknown result type (might be due to invalid IL or missing references)
			//IL_0373: Unknown result type (might be due to invalid IL or missing references)
			//IL_037d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0382: Unknown result type (might be due to invalid IL or missing references)
			//IL_0396: Unknown result type (might be due to invalid IL or missing references)
			//IL_069f: Unknown result type (might be due to invalid IL or missing references)
			//IL_044a: Unknown result type (might be due to invalid IL or missing references)
			//IL_044c: Unknown result type (might be due to invalid IL or missing references)
			//IL_048e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0493: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_05d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_05ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_05e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0591: Unknown result type (might be due to invalid IL or missing references)
			//IL_0596: Unknown result type (might be due to invalid IL or missing references)
			//IL_0619: Unknown result type (might be due to invalid IL or missing references)
			//IL_064e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0665: Unknown result type (might be due to invalid IL or missing references)
			//IL_065e: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				CompBillAutomation comp = Comp;
				if (comp == null)
				{
					return;
				}
				Rect val = GenUI.ContractedBy(new Rect(0f, 0f, WinSize.x, WinSize.y), 4f);
				AE2Draw.HandlePauseHotkey();
				AE2Draw.PanelBox(val);
				Rect val2 = GenUI.ContractedBy(val, 6f);
				float y = ((Rect)(ref val2)).y;
				if (AE2Draw.TextButton(new Rect(((Rect)(ref val2)).x, y, ((Rect)(ref val2)).width, 28f), ((object)Translator.Translate("DS_AE2_OpenPanel")/*cast due to constrained. prefix*/).ToString()))
				{
					Find.WindowStack.Add((Window)(object)new Window_AE2CraftPanel(comp));
				}
				y += 32f;
				string text = (comp.Enabled ? ((object)Translator.Translate("DS_BA_On")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("DS_BA_Off")/*cast due to constrained. prefix*/).ToString());
				float num = (((Rect)(ref val2)).width - 4f) / 2f;
				if (AE2Draw.TextButton(new Rect(((Rect)(ref val2)).x, y, num, 24f), ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_ToggleState", NamedArgument.op_Implicit(text))/*cast due to constrained. prefix*/).ToString()))
				{
					comp.Enabled = !comp.Enabled;
				}
				if (AE2Draw.TextButton(new Rect(((Rect)(ref val2)).x + num + 4f, y, num, 24f), ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_Overclock", NamedArgument.op_Implicit(comp.OverclockLabel()))/*cast due to constrained. prefix*/).ToString()))
				{
					comp.OverclockTier = (comp.OverclockTier + 1) % 4;
				}
				y += 28f;
				Rect r = default(Rect);
				((Rect)(ref r))..ctor(((Rect)(ref val2)).x, y, ((Rect)(ref val2)).width, 40f);
				AE2Draw.Sunken(r, AE2Draw.BarBg);
				AE2Draw.TextIn(new Rect(((Rect)(ref r)).x + 4f, ((Rect)(ref r)).y + 3f, ((Rect)(ref r)).width - 8f, 16f), ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_Watts", NamedArgument.op_Implicit(comp.CurrentWatts.ToString("#####0")), NamedArgument.op_Implicit(comp.Props.basePowerWatts.ToString("#####0")), NamedArgument.op_Implicit(comp.BenchWatts().ToString("#####0")), NamedArgument.op_Implicit(comp.OverclockPowerMult.ToString("0")))/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.Hi, (TextAnchor)3);
				AE2Draw.TextIn(new Rect(((Rect)(ref r)).x + 4f, ((Rect)(ref r)).y + 21f, ((Rect)(ref r)).width - 8f, 16f), ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_Stats", NamedArgument.op_Implicit(comp.CompletedCount), NamedArgument.op_Implicit(comp.DroppedCount), NamedArgument.op_Implicit(comp.PlansForReading.Count))/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.Hi, (TextAnchor)3);
				y += 44f;
				IList<CraftPlan> plansForReading = comp.PlansForReading;
				Rect val3 = default(Rect);
				((Rect)(ref val3))..ctor(((Rect)(ref val2)).x, y, ((Rect)(ref val2)).width, ((Rect)(ref val2)).yMax - y);
				if (plansForReading.Count == 0)
				{
					AE2Draw.TextIn(new Rect(((Rect)(ref val3)).x + 4f, ((Rect)(ref val3)).y + 2f, ((Rect)(ref val3)).width - 8f, 18f), ((object)Translator.Translate("DS_BA_Empty")/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextDimCol, (TextAnchor)3);
					return;
				}
				int num2 = Mathf.Max(1, Mathf.FloorToInt(((Rect)(ref val3)).height / 23f));
				int num3 = Mathf.Max(0, plansForReading.Count - num2);
				int num4 = Mathf.RoundToInt(scroll * (float)num3);
				int num5 = Mathf.Min(num2, Mathf.Max(0, plansForReading.Count - num4));
				Rect val4 = default(Rect);
				for (int i = 0; i < num5; i++)
				{
					CraftPlan craftPlan = plansForReading[num4 + i];
					if (craftPlan != null)
					{
						((Rect)(ref val4))..ctor(((Rect)(ref val3)).x, ((Rect)(ref val3)).y + (float)i * 23f, ((Rect)(ref val3)).width - 14f, 22f);
						AE2Draw.Row(val4, Mouse.IsOver(val4), i == 0);
						CraftLine craftLine = ((craftPlan.lines.Count > 0) ? craftPlan.lines[0] : null);
						string s = ((craftPlan.recipe == null) ? "?" : ((object)((Def)craftPlan.recipe).LabelCap/*cast due to constrained. prefix*/).ToString());
						float num6;
						string s2;
						if (craftPlan.Done)
						{
							num6 = 1f;
							s2 = ((object)Translator.Translate("DS_BA_PlanFinished")/*cast due to constrained. prefix*/).ToString();
						}
						else if (craftPlan.suspended)
						{
							num6 = 0f;
							s2 = ((object)Translator.Translate("DS_BA_Suspended")/*cast due to constrained. prefix*/).ToString();
						}
						else if (craftPlan.mode == 2 && craftPlan.paused)
						{
							num6 = 0f;
							s2 = craftPlan.countedCount + "/" + craftPlan.targetCount;
						}
						else if (craftLine != null && craftLine.HasWork)
						{
							num6 = craftLine.Progress01;
							s2 = (num6 * 100f).ToString("0") + "%";
						}
						else
						{
							num6 = 0f;
							s2 = ((object)Translator.Translate("DS_CA_Waiting")/*cast due to constrained. prefix*/).ToString();
						}
						AE2Draw.TextIn(new Rect(((Rect)(ref val4)).x + 4f, ((Rect)(ref val4)).y + 2f, ((Rect)(ref val4)).width - 84f, 18f), s, 1f, (i == 0) ? AE2Draw.Hi : AE2Draw.TextCol, (TextAnchor)3);
						AE2Draw.Bar(new Rect(((Rect)(ref val4)).xMax - 74f, ((Rect)(ref val4)).y + 7f, 46f, 8f), num6, i == 0);
						AE2Draw.TextIn(new Rect(((Rect)(ref val4)).xMax - 26f, ((Rect)(ref val4)).y + 3f, 24f, 16f), s2, 1f, (i == 0) ? AE2Draw.Hi : AE2Draw.TextDimCol, (TextAnchor)3);
					}
				}
				AE2Draw.DragBar(new Rect(((Rect)(ref val3)).xMax - 12f, ((Rect)(ref val3)).y, 12f, ((Rect)(ref val3)).height), ref scroll, (plansForReading.Count <= 0) ? 1f : Mathf.Clamp01((float)num2 / (float)plansForReading.Count));
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] AE2 界面绘制异常（只记一次，界面不会卡死）：" + ex, 771004);
			}
		}
	}
	public class Dialog_DS_AddCraft : Window
	{
		private readonly CompBillAutomation comp;

		private readonly List<RecipeDef> shown = new List<RecipeDef>();

		private string search = "";

		private int catIndex;

		private Vector2 scroll;

		public override Vector2 InitialSize => new Vector2(560f, 620f);

		public Dialog_DS_AddCraft(CompBillAutomation comp)
			: base((IWindowDrawing)null)
		{
			this.comp = comp;
			base.doCloseX = true;
			base.closeOnClickedOutside = true;
			base.absorbInputAroundWindow = true;
			base.onlyOneOfTypeAllowed = true;
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0201: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_0256: Unknown result type (might be due to invalid IL or missing references)
			//IL_0260: Unknown result type (might be due to invalid IL or missing references)
			//IL_0267: Unknown result type (might be due to invalid IL or missing references)
			//IL_0271: Unknown result type (might be due to invalid IL or missing references)
			//IL_012a: Unknown result type (might be due to invalid IL or missing references)
			//IL_011c: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			//IL_056d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0597: Unknown result type (might be due to invalid IL or missing references)
			//IL_05a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_05ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			//IL_0135: Unknown result type (might be due to invalid IL or missing references)
			//IL_0329: Unknown result type (might be due to invalid IL or missing references)
			//IL_0150: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_0332: Unknown result type (might be due to invalid IL or missing references)
			//IL_0334: Unknown result type (might be due to invalid IL or missing references)
			//IL_0160: Unknown result type (might be due to invalid IL or missing references)
			//IL_016f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0179: Unknown result type (might be due to invalid IL or missing references)
			//IL_0346: Unknown result type (might be due to invalid IL or missing references)
			//IL_0348: Unknown result type (might be due to invalid IL or missing references)
			//IL_018c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0191: Unknown result type (might be due to invalid IL or missing references)
			//IL_0361: Unknown result type (might be due to invalid IL or missing references)
			//IL_035a: Unknown result type (might be due to invalid IL or missing references)
			//IL_03f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_03fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0439: Unknown result type (might be due to invalid IL or missing references)
			//IL_0450: Unknown result type (might be due to invalid IL or missing references)
			//IL_039c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0464: Unknown result type (might be due to invalid IL or missing references)
			//IL_0480: Unknown result type (might be due to invalid IL or missing references)
			//IL_0474: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_04e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_04fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_04cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_0512: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(inRect, AE2Draw.Panel);
			Widgets.DrawBox(inRect, 1, (Texture2D)null);
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(((Rect)(ref inRect)).x + 8f, ((Rect)(ref inRect)).y + 6f, ((Rect)(ref inRect)).width - 16f, ((Rect)(ref inRect)).height - 12f);
			Text.Font = (GameFont)2;
			Widgets.Label(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width - 30f, 26f), Translator.Translate("DS_BA_AddTitle"));
			Text.Font = (GameFont)1;
			float num = ((Rect)(ref val)).y + 28f;
			float num2 = ((Rect)(ref val)).x;
			float num3 = num;
			Rect val2 = default(Rect);
			for (int i = 0; i < CraftCategories.Tabs.Length; i++)
			{
				string text = TaggedString.op_Implicit(Translator.Translate(CraftCategories.LabelKey(CraftCategories.Tabs[i])));
				float num4 = Mathf.Max(52f, Text.CalcSize(text).x + 16f);
				if (num2 + num4 > ((Rect)(ref val)).xMax)
				{
					num2 = ((Rect)(ref val)).x;
					num3 += 24f;
				}
				((Rect)(ref val2))..ctor(num2, num3, num4, 22f);
				if (catIndex == i)
				{
					Widgets.DrawBoxSolid(val2, AE2Draw.Slot);
				}
				else if (Mouse.IsOver(val2))
				{
					Widgets.DrawBoxSolid(val2, AE2Draw.Panel);
				}
				GUI.color = ((catIndex == i) ? AE2Draw.Accent : Color.white);
				Text.Anchor = (TextAnchor)4;
				Widgets.Label(val2, text);
				Text.Anchor = (TextAnchor)0;
				GUI.color = Color.white;
				if (Widgets.ButtonInvisible(val2, true))
				{
					catIndex = i;
					scroll = Vector2.zero;
				}
				num2 += num4 + 3f;
			}
			num = num3 + 26f;
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref val)).x, num - 2f, ((Rect)(ref val)).width, 1f), AE2Draw.LineSoft);
			search = Widgets.TextField(new Rect(((Rect)(ref val)).x, num + 2f, ((Rect)(ref val)).width, 24f), search);
			num += 30f;
			BuildList();
			Rect val3 = default(Rect);
			((Rect)(ref val3))..ctor(((Rect)(ref val)).x, num, ((Rect)(ref val)).width, ((Rect)(ref val)).yMax - num - 26f);
			if (shown.Count == 0)
			{
				GUI.color = AE2Draw.TextDimCol;
				Widgets.Label(val3, Translator.Translate("DS_BA_NoRecipeAvailable"));
				GUI.color = Color.white;
			}
			else
			{
				Rect val4 = default(Rect);
				((Rect)(ref val4))..ctor(0f, 0f, ((Rect)(ref val3)).width - 18f, Math.Max(((Rect)(ref val3)).height, (float)shown.Count * 36f + 4f));
				Widgets.BeginScrollView(val3, ref scroll, val4, true);
				float num5 = 0f;
				Rect val6 = default(Rect);
				for (int j = 0; j < shown.Count; j++)
				{
					RecipeDef val5 = shown[j];
					((Rect)(ref val6))..ctor(2f, num5, ((Rect)(ref val4)).width - 4f, 34f);
					bool flag = comp.HasPlan(val5);
					bool flag2 = SkillOk(val5);
					if (Mouse.IsOver(val6))
					{
						Widgets.DrawBoxSolid(val6, AE2Draw.Slot);
					}
					else if (j % 2 == 0)
					{
						Widgets.DrawBoxSolid(val6, AE2Draw.Panel);
					}
					GUI.color = ((flag || !flag2) ? AE2Draw.TextDimCol : Color.white);
					ThingDef val7 = CraftCategories.MainProduct(val5);
					if (val7 != null)
					{
						Widgets.DefIcon(new Rect(((Rect)(ref val6)).x + 3f, ((Rect)(ref val6)).y + 3f, 26f, 26f), (Def)(object)val7, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
					}
					Widgets.Label(new Rect(((Rect)(ref val6)).x + 34f, ((Rect)(ref val6)).y + 2f, ((Rect)(ref val6)).width - 120f, 18f), ((Def)val5).LabelCap);
					Text.Font = (GameFont)0;
					Widgets.Label(new Rect(((Rect)(ref val6)).x + 34f, ((Rect)(ref val6)).y + 19f, ((Rect)(ref val6)).width - 120f, 16f), IngredientsLine(val5));
					Text.Font = (GameFont)1;
					GUI.color = Color.white;
					Text.Anchor = (TextAnchor)5;
					if (flag)
					{
						GUI.color = AE2Draw.Accent;
					}
					else if (!flag2)
					{
						GUI.color = AE2Draw.Bad;
					}
					else
					{
						GUI.color = AE2Draw.TextDimCol;
					}
					Widgets.Label(new Rect(((Rect)(ref val6)).xMax - 250f, ((Rect)(ref val6)).y + 9f, 246f, 18f), flag ? ((object)Translator.Translate("DS_BA_AlreadyAdded")/*cast due to constrained. prefix*/).ToString() : (flag2 ? ((object)Translator.Translate("DS_BA_ClickToAdd")/*cast due to constrained. prefix*/).ToString() : NeedSkillText(val5)));
					GUI.color = Color.white;
					Text.Anchor = (TextAnchor)0;
					if (!flag && Widgets.ButtonInvisible(val6, true))
					{
						if (Event.current != null && Event.current.shift)
						{
							AddWholeTree(val5);
						}
						else
						{
							comp.AddPlan(val5);
						}
					}
					num5 += 36f;
				}
				Widgets.EndScrollView();
			}
			GUI.color = AE2Draw.TextDimCol;
			Widgets.Label(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).yMax - 22f, ((Rect)(ref val)).width, 20f), Translator.Translate("DS_CA_AddHint"));
			GUI.color = Color.white;
		}

		private void AddWholeTree(RecipeDef recipe)
		{
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			if (recipe == null)
			{
				return;
			}
			CraftJob craftJob = comp.SubmitJob(recipe, null, 1, withIntermediates: true);
			if (craftJob != null)
			{
				if (craftJob.steps.Count <= 1)
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("DS_CT_AddTreeNone")), MessageTypeDefOf.NeutralEvent, false);
				}
				else
				{
					Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_JOB_Submitted", NamedArgument.op_Implicit(((Def)recipe).LabelCap), NamedArgument.op_Implicit(craftJob.wanted), NamedArgument.op_Implicit(craftJob.steps.Count))), MessageTypeDefOf.NeutralEvent, false);
				}
			}
		}

		private void BuildList()
		{
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			shown.Clear();
			IList<RecipeDef> unlockedRecipes = comp.UnlockedRecipes;
			CraftCategory craftCategory = CraftCategories.Tabs[Mathf.Clamp(catIndex, 0, CraftCategories.Tabs.Length - 1)];
			for (int i = 0; i < unlockedRecipes.Count; i++)
			{
				RecipeDef val = unlockedRecipes[i];
				if (val != null && (craftCategory == CraftCategory.All || CraftCategories.Of(val) == craftCategory) && (string.IsNullOrEmpty(search) || ((object)((Def)val).LabelCap/*cast due to constrained. prefix*/).ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || ((Def)val).defName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
				{
					shown.Add(val);
				}
			}
		}

		private static string IngredientsLine(RecipeDef r)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			if (r.ingredients == null || r.ingredients.Count == 0)
			{
				return ((object)Translator.Translate("DS_CA_NoMaterials")/*cast due to constrained. prefix*/).ToString();
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < r.ingredients.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append(" + ");
				}
				stringBuilder.Append(r.ingredients[i].Summary);
			}
			return stringBuilder.ToString();
		}

		private string NeedSkillText(RecipeDef r)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			if (r.skillRequirements == null || r.skillRequirements.Count == 0)
			{
				return ((object)Translator.Translate("DS_BA_SkillShort")/*cast due to constrained. prefix*/).ToString();
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < r.skillRequirements.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append(", ");
				}
				stringBuilder.Append(TaggedString.op_Implicit(((Def)r.skillRequirements[i].skill).LabelCap)).Append(' ').Append(r.skillRequirements[i].minLevel);
			}
			return ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_NeedSkill", NamedArgument.op_Implicit(stringBuilder.ToString()))/*cast due to constrained. prefix*/).ToString();
		}

		private bool SkillOk(RecipeDef r)
		{
			if (r.skillRequirements == null)
			{
				return true;
			}
			int skillLevel = comp.Props.skillLevel;
			for (int i = 0; i < r.skillRequirements.Count; i++)
			{
				if (r.skillRequirements[i].minLevel > skillLevel)
				{
					return false;
				}
			}
			return true;
		}
	}
	public class Window_CraftAutomation : Window
	{
		private enum SKind
		{
			Ok,
			Warn,
			Bad,
			Idle,
			Done
		}

		private const float NavWidth = 116f;

		private const float ListWidth = 236f;

		private const float TopHeight = 58f;

		private CompBillAutomation comp;

		private readonly List<CompBillAutomation> proxies = new List<CompBillAutomation>();

		private int nextProxyScanTick;

		private int navIndex;

		private CraftPlan sel;

		private Vector2 scrollList;

		private Vector2 scrollDetail;

		private readonly Dictionary<ThingDef, int> stock = new Dictionary<ThingDef, int>();

		private int nextStockTick;

		private readonly List<CraftPlan> shown = new List<CraftPlan>();

		private const int NavCount = 6;

		private float flowX;

		private float flowY;

		private float flowStartX;

		private float flowMaxX;

		private const float FlowRowH = 26f;

		private const float FlowGap = 4f;

		private string remainingBuf;

		private string targetBuf;

		public override Vector2 InitialSize => new Vector2(960f, 640f);

		public Window_CraftAutomation(CompBillAutomation comp)
			: base((IWindowDrawing)null)
		{
			this.comp = comp;
			base.draggable = true;
			base.resizeable = true;
			base.doCloseX = true;
			base.absorbInputAroundWindow = true;
			base.closeOnClickedOutside = false;
			base.onlyOneOfTypeAllowed = true;
			base.preventCameraMotion = true;
		}

		public static void OpenOrFocus(CompBillAutomation target)
		{
			Window_CraftAutomation window_CraftAutomation = Find.WindowStack.WindowOfType<Window_CraftAutomation>();
			if (window_CraftAutomation != null)
			{
				if (target != null)
				{
					window_CraftAutomation.SetComp(target);
				}
			}
			else
			{
				Find.WindowStack.Add((Window)(object)new Window_AE2CraftPanel(target));
			}
		}

		private void SetComp(CompBillAutomation c)
		{
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			if (c != null && c != comp)
			{
				comp = c;
				sel = null;
				ClearCountBuffers();
				navIndex = 0;
				scrollList = Vector2.zero;
				scrollDetail = Vector2.zero;
			}
		}

		public override void WindowUpdate()
		{
			((Window)this).WindowUpdate();
			int ticksGame = Find.TickManager.TicksGame;
			if (ticksGame >= nextProxyScanTick)
			{
				nextProxyScanTick = ticksGame + 60;
				RescanProxies();
			}
			if (ticksGame >= nextStockTick)
			{
				nextStockTick = ticksGame + 60;
				RebuildStock();
			}
		}

		private void RescanProxies()
		{
			proxies.Clear();
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return;
			}
			List<Building> allBuildingsColonist = currentMap.listerBuildings.allBuildingsColonist;
			for (int i = 0; i < allBuildingsColonist.Count; i++)
			{
				CompBillAutomation compBillAutomation = ThingCompUtility.TryGetComp<CompBillAutomation>((Thing)(object)allBuildingsColonist[i]);
				if (compBillAutomation != null)
				{
					proxies.Add(compBillAutomation);
				}
			}
			if (comp == null || ((ThingComp)comp).parent == null || !((Thing)((ThingComp)comp).parent).Spawned)
			{
				comp = ((proxies.Count > 0) ? proxies[0] : null);
				sel = null;
			}
		}

		private void RebuildStock()
		{
			stock.Clear();
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return;
			}
			List<Building_StorageCore> list = CoreFinder.AllUsableCores(currentMap);
			for (int i = 0; i < list.Count; i++)
			{
				ThingOwner directlyHeldThings = list[i].GetDirectlyHeldThings();
				if (directlyHeldThings == null)
				{
					continue;
				}
				for (int j = 0; j < directlyHeldThings.Count; j++)
				{
					Thing val = directlyHeldThings[j];
					if (val != null && !val.Destroyed && val.def != null)
					{
						stock.TryGetValue(val.def, out var value);
						stock[val.def] = value + Math.Max(0, val.stackCount);
					}
				}
			}
		}

		private int StockOf(ThingDef def)
		{
			if (def == null || !stock.TryGetValue(def, out var value))
			{
				return 0;
			}
			return value;
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0165: Unknown result type (might be due to invalid IL or missing references)
			//IL_016c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0173: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(inRect, AE2Draw.Panel);
			Widgets.DrawBox(inRect, 1, (Texture2D)null);
			Rect rect = default(Rect);
			((Rect)(ref rect))..ctor(((Rect)(ref inRect)).x + 1f, ((Rect)(ref inRect)).y + 1f, ((Rect)(ref inRect)).width - 2f, 58f);
			DrawTop(rect);
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(((Rect)(ref inRect)).x + 1f, ((Rect)(ref rect)).yMax, ((Rect)(ref inRect)).width - 2f, ((Rect)(ref inRect)).yMax - ((Rect)(ref rect)).yMax - 1f);
			if (comp == null)
			{
				Text.Anchor = (TextAnchor)4;
				Widgets.Label(val, Translator.Translate("DS_CA_NoProxy"));
				Text.Anchor = (TextAnchor)0;
				return;
			}
			Rect rect2 = default(Rect);
			((Rect)(ref rect2))..ctor(((Rect)(ref val)).x, ((Rect)(ref val)).y, 116f, ((Rect)(ref val)).height);
			Rect rect3 = default(Rect);
			((Rect)(ref rect3))..ctor(((Rect)(ref rect2)).xMax, ((Rect)(ref val)).y, 236f, ((Rect)(ref val)).height);
			Rect rect4 = default(Rect);
			((Rect)(ref rect4))..ctor(((Rect)(ref rect3)).xMax, ((Rect)(ref val)).y, ((Rect)(ref val)).width - 116f - 236f, ((Rect)(ref val)).height);
			Map currentMap = Find.CurrentMap;
			if (currentMap != null)
			{
				IList<CraftPlan> plansForReading = comp.PlansForReading;
				for (int i = 0; i < plansForReading.Count; i++)
				{
					comp.RefreshTargetStateIfStale(plansForReading[i], currentMap);
				}
			}
			DrawNav(rect2);
			DrawList(rect3);
			DrawDetail(rect4);
		}

		private void DrawTop(Rect rect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0333: Unknown result type (might be due to invalid IL or missing references)
			//IL_035d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0367: Unknown result type (might be due to invalid IL or missing references)
			//IL_0371: Unknown result type (might be due to invalid IL or missing references)
			//IL_0222: Unknown result type (might be due to invalid IL or missing references)
			//IL_024c: Unknown result type (might be due to invalid IL or missing references)
			//IL_026f: Unknown result type (might be due to invalid IL or missing references)
			//IL_028e: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0313: Unknown result type (might be due to invalid IL or missing references)
			//IL_0318: Unknown result type (might be due to invalid IL or missing references)
			//IL_031d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0327: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0106: Unknown result type (might be due to invalid IL or missing references)
			//IL_010b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0381: Unknown result type (might be due to invalid IL or missing references)
			//IL_03ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_014a: Unknown result type (might be due to invalid IL or missing references)
			//IL_015f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0164: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(rect, AE2Draw.Panel);
			float num = ((Rect)(ref rect)).y + 4f;
			float num2 = ((Rect)(ref rect)).x + 6f;
			string label = ((comp == null || ((ThingComp)comp).parent == null) ? ((object)Translator.Translate("DS_CA_NoProxy")/*cast due to constrained. prefix*/).ToString() : (((Entity)((ThingComp)comp).parent).LabelShort + "  " + ((object)((Thing)((ThingComp)comp).parent).Position/*cast due to constrained. prefix*/).ToString()));
			if (AE2Draw.AE2TextButton(new Rect(num2, num, 250f, 24f), label))
			{
				OpenProxyMenu();
			}
			num2 += 256f;
			if (comp != null)
			{
				string text = TaggedString.op_Implicit(comp.Enabled ? Translator.Translate("DS_BA_On") : Translator.Translate("DS_BA_Off"));
				if (AE2Draw.AE2TextButton(new Rect(num2, num, 132f, 24f), TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_BA_ToggleState", NamedArgument.op_Implicit(text)))))
				{
					comp.Enabled = !comp.Enabled;
				}
				num2 += 138f;
				if (AE2Draw.AE2TextButton(new Rect(num2, num, 168f, 24f), TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_BA_Overclock", NamedArgument.op_Implicit(comp.OverclockLabel())))))
				{
					comp.OverclockTier = (comp.OverclockTier + 1) % 4;
				}
				num2 += 174f;
			}
			float num3 = Math.Min(150f, ((Rect)(ref rect)).xMax - num2 - 8f);
			if (num3 > 40f && AE2Draw.AE2TextButton(new Rect(((Rect)(ref rect)).xMax - num3 - 6f, num, num3, 24f), TaggedString.op_Implicit(Translator.Translate("DS_CA_AddRecipe"))) && comp != null)
			{
				Find.WindowStack.Add((Window)(object)new Dialog_DS_AddCraft(comp));
			}
			num += 26f;
			Text.Font = (GameFont)0;
			if (comp != null)
			{
				GUI.color = AE2Draw.TextDimCol;
				Widgets.Label(new Rect(((Rect)(ref rect)).x + 6f, num, ((Rect)(ref rect)).width - 12f, 24f), TranslatorFormattedStringExtensions.Translate("DS_BA_Watts", NamedArgument.op_Implicit(comp.CurrentWatts.ToString("#####0")), NamedArgument.op_Implicit(comp.Props.basePowerWatts.ToString("#####0")), NamedArgument.op_Implicit(comp.BenchWatts().ToString("#####0")), NamedArgument.op_Implicit(comp.OverclockPowerMult.ToString("0"))) + "   ·   " + TranslatorFormattedStringExtensions.Translate("DS_BA_Stats", NamedArgument.op_Implicit(comp.CompletedCount), NamedArgument.op_Implicit(comp.DroppedCount), NamedArgument.op_Implicit(comp.PlansForReading.Count)));
				GUI.color = Color.white;
			}
			else
			{
				GUI.color = AE2Draw.TextDimCol;
				Widgets.Label(new Rect(((Rect)(ref rect)).x + 6f, num, ((Rect)(ref rect)).width - 12f, 24f), Translator.Translate("DS_CA_NoProxyHint"));
				GUI.color = Color.white;
			}
			Text.Font = (GameFont)1;
			GUI.color = Color.white;
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref rect)).x, ((Rect)(ref rect)).yMax - 1f, ((Rect)(ref rect)).width, 1f), AE2Draw.LineSoft);
		}

		private void OpenProxyMenu()
		{
			//IL_0131: Unknown result type (might be due to invalid IL or missing references)
			//IL_013b: Expected O, but got Unknown
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			//IL_012b: Expected O, but got Unknown
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Expected O, but got Unknown
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			for (int i = 0; i < proxies.Count; i++)
			{
				CompBillAutomation compBillAutomation = proxies[i];
				if (compBillAutomation != null && ((ThingComp)compBillAutomation).parent != null)
				{
					CompBillAutomation target = compBillAutomation;
					string text = TaggedString.op_Implicit(((Entity)((ThingComp)target).parent).LabelShort + "  " + ((object)((Thing)((ThingComp)target).parent).Position/*cast due to constrained. prefix*/).ToString() + "（" + TranslatorFormattedStringExtensions.Translate("DS_BA_Stats2", NamedArgument.op_Implicit(target.CompletedCount), NamedArgument.op_Implicit(target.PlansForReading.Count)) + "）");
					list.Add(new FloatMenuOption(text, (Action)delegate
					{
						SetComp(target);
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
				}
			}
			if (list.Count == 0)
			{
				list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("DS_CA_NoProxy")), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
			Find.WindowStack.Add((Window)new FloatMenu(list));
		}

		private void DrawNav(Rect rect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00de: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_012e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0139: Unknown result type (might be due to invalid IL or missing references)
			//IL_014a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0178: Unknown result type (might be due to invalid IL or missing references)
			//IL_018f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0199: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(rect, AE2Draw.Panel);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref rect)).xMax - 1f, ((Rect)(ref rect)).y, 1f, ((Rect)(ref rect)).height), AE2Draw.LineSoft);
			float num = ((Rect)(ref rect)).y + 4f;
			Widgets.Label(new Rect(((Rect)(ref rect)).x + 4f, num, ((Rect)(ref rect)).width - 12f, 20f), Translator.Translate("DS_CA_Orders"));
			num += 22f;
			Rect val = default(Rect);
			for (int i = 0; i < 6; i++)
			{
				SKind sKind = (SKind)i;
				int num2 = CountOf(sKind);
				((Rect)(ref val))..ctor(((Rect)(ref rect)).x + 4f, num, ((Rect)(ref rect)).width - 6f, 24f);
				if (navIndex == i)
				{
					Widgets.DrawBoxSolid(val, AE2Draw.Slot);
				}
				else if (Mouse.IsOver(val))
				{
					Widgets.DrawBoxSolid(val, AE2Draw.Panel);
				}
				GUI.color = ((navIndex == i) ? Color.white : AE2Draw.TextCol);
				Widgets.Label(new Rect(((Rect)(ref val)).x + 6f, ((Rect)(ref val)).y + 2f, ((Rect)(ref val)).width - 40f, 20f), Translator.Translate(NavLabel(sKind)));
				Text.Anchor = (TextAnchor)5;
				GUI.color = CountColor(sKind);
				Widgets.Label(new Rect(((Rect)(ref val)).xMax - 34f, ((Rect)(ref val)).y + 2f, 30f, 20f), num2.ToString());
				Text.Anchor = (TextAnchor)0;
				GUI.color = Color.white;
				if (Widgets.ButtonInvisible(val, true))
				{
					navIndex = i;
					sel = null;
				}
				num += 25f;
			}
		}

		private static string NavLabel(SKind k)
		{
			return k switch
			{
				SKind.Ok => "DS_CA_NavOk", 
				SKind.Bad => "DS_CA_NavBad", 
				SKind.Warn => "DS_CA_NavWarn", 
				SKind.Idle => "DS_CA_NavIdle", 
				SKind.Done => "DS_CA_NavDone", 
				_ => "DS_CA_NavAll", 
			};
		}

		private int CountOf(SKind kind)
		{
			if (comp == null)
			{
				return 0;
			}
			IList<CraftPlan> plansForReading = comp.PlansForReading;
			int num = 0;
			for (int i = 0; i < plansForReading.Count; i++)
			{
				if (KindOf(plansForReading[i]) == kind)
				{
					num++;
				}
			}
			return num;
		}

		private static Color ColorOf(SKind k)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			return (Color)(k switch
			{
				SKind.Ok => AE2Draw.Ok, 
				SKind.Bad => AE2Draw.Bad, 
				SKind.Warn => AE2Draw.Warn, 
				SKind.Done => AE2Draw.Accent, 
				_ => AE2Draw.TextDimCol, 
			});
		}

		private static Color CountColor(SKind k)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			if (k != SKind.Idle)
			{
				return ColorOf(k);
			}
			return AE2Draw.TextDimCol;
		}

		private void DrawList(Rect rect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00db: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_0167: Unknown result type (might be due to invalid IL or missing references)
			//IL_0159: Unknown result type (might be due to invalid IL or missing references)
			//IL_015b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01af: Unknown result type (might be due to invalid IL or missing references)
			//IL_0170: Unknown result type (might be due to invalid IL or missing references)
			//IL_0172: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_022a: Unknown result type (might be due to invalid IL or missing references)
			//IL_022f: Unknown result type (might be due to invalid IL or missing references)
			//IL_025e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0294: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0257: Unknown result type (might be due to invalid IL or missing references)
			//IL_0338: Unknown result type (might be due to invalid IL or missing references)
			//IL_0344: Unknown result type (might be due to invalid IL or missing references)
			//IL_0409: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_03fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_03ff: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(rect, AE2Draw.Panel);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref rect)).xMax - 1f, ((Rect)(ref rect)).y, 1f, ((Rect)(ref rect)).height), AE2Draw.LineSoft);
			BuildShown();
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(0f, 0f, ((Rect)(ref rect)).width - 18f, Math.Max(((Rect)(ref rect)).height, (float)shown.Count * 28f + 6f));
			Widgets.BeginScrollView(new Rect(((Rect)(ref rect)).x + 2f, ((Rect)(ref rect)).y + 3f, ((Rect)(ref rect)).width - 4f, ((Rect)(ref rect)).height - 6f), ref scrollList, val, true);
			float num = 0f;
			if (shown.Count == 0)
			{
				GUI.color = AE2Draw.TextDimCol;
				Widgets.Label(new Rect(4f, num, ((Rect)(ref val)).width - 8f, 40f), Translator.Translate("DS_CA_NoOrder"));
				GUI.color = Color.white;
			}
			Rect val2 = default(Rect);
			for (int i = 0; i < shown.Count; i++)
			{
				CraftPlan craftPlan = shown[i];
				((Rect)(ref val2))..ctor(2f, num, ((Rect)(ref val)).width - 4f, 26f);
				if (craftPlan == sel)
				{
					Widgets.DrawBoxSolid(val2, AE2Draw.Slot);
				}
				else if (Mouse.IsOver(val2))
				{
					Widgets.DrawBoxSolid(val2, AE2Draw.Panel);
				}
				SKind k = KindOf(craftPlan);
				Widgets.DrawBoxSolid(new Rect(((Rect)(ref val2)).x + 5f, ((Rect)(ref val2)).y + 10f, 6f, 6f), ColorOf(k));
				ThingDef val3 = CraftCategories.MainProduct(craftPlan.recipe);
				if (val3 != null)
				{
					Widgets.DefIcon(new Rect(((Rect)(ref val2)).x + 16f, ((Rect)(ref val2)).y + 3f, 20f, 20f), (Def)(object)val3, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
				}
				string text = ((craftPlan.recipe == null) ? "?" : ((object)((Def)craftPlan.recipe).LabelCap/*cast due to constrained. prefix*/).ToString());
				GUI.color = ((craftPlan.suspended || craftPlan.Done) ? AE2Draw.TextDimCol : AE2Draw.TextCol);
				Widgets.Label(new Rect(((Rect)(ref val2)).x + 40f, ((Rect)(ref val2)).y + 4f, ((Rect)(ref val2)).width - 96f, 20f), text);
				GUI.color = Color.white;
				Text.Anchor = (TextAnchor)5;
				GUI.color = AE2Draw.TextDimCol;
				float num2 = 30f;
				string text2;
				if (craftPlan.mode == 1)
				{
					text2 = craftPlan.remaining.ToString();
				}
				else if (craftPlan.mode == 2)
				{
					text2 = craftPlan.countedCount + "/" + craftPlan.targetCount;
					num2 = 52f;
				}
				else
				{
					text2 = "∞";
				}
				Widgets.Label(new Rect(((Rect)(ref val2)).xMax - num2 - 22f, ((Rect)(ref val2)).y + 4f, num2, 20f), text2);
				GUI.color = Color.white;
				Text.Anchor = (TextAnchor)0;
				CraftLine craftLine = ((craftPlan.lines.Count > 0) ? craftPlan.lines[0] : null);
				if (craftLine != null && craftLine.HasWork)
				{
					Widgets.DrawBoxSolid(new Rect(((Rect)(ref val2)).x + 40f, ((Rect)(ref val2)).yMax - 5f, ((Rect)(ref val2)).width - 90f, 2f), AE2Draw.LineSoft);
					Widgets.DrawBoxSolid(new Rect(((Rect)(ref val2)).x + 40f, ((Rect)(ref val2)).yMax - 5f, (((Rect)(ref val2)).width - 90f) * Mathf.Clamp01(craftLine.Progress01), 2f), AE2Draw.Accent);
				}
				if (Widgets.ButtonInvisible(val2, true))
				{
					SelectPlan(craftPlan);
				}
				num += 28f;
			}
			Widgets.EndScrollView();
		}

		private void BuildShown()
		{
			shown.Clear();
			if (comp == null)
			{
				return;
			}
			IList<CraftPlan> plansForReading = comp.PlansForReading;
			SKind sKind = (SKind)navIndex;
			for (int i = 0; i < plansForReading.Count; i++)
			{
				if (KindOf(plansForReading[i]) == sKind)
				{
					shown.Add(plansForReading[i]);
				}
			}
		}

		private void DrawDetail(Rect rect)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Unknown result type (might be due to invalid IL or missing references)
			//IL_0173: Unknown result type (might be due to invalid IL or missing references)
			//IL_0125: Unknown result type (might be due to invalid IL or missing references)
			//IL_0190: Unknown result type (might be due to invalid IL or missing references)
			//IL_0195: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0200: Unknown result type (might be due to invalid IL or missing references)
			//IL_0205: Unknown result type (might be due to invalid IL or missing references)
			//IL_021a: Unknown result type (might be due to invalid IL or missing references)
			//IL_021f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0229: Unknown result type (might be due to invalid IL or missing references)
			//IL_024b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0254: Unknown result type (might be due to invalid IL or missing references)
			//IL_0269: Unknown result type (might be due to invalid IL or missing references)
			//IL_0276: Unknown result type (might be due to invalid IL or missing references)
			//IL_027f: Unknown result type (might be due to invalid IL or missing references)
			//IL_029e: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02be: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02df: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0375: Unknown result type (might be due to invalid IL or missing references)
			//IL_037f: Unknown result type (might be due to invalid IL or missing references)
			//IL_03af: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_03da: Unknown result type (might be due to invalid IL or missing references)
			//IL_03df: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_03fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0421: Unknown result type (might be due to invalid IL or missing references)
			//IL_0426: Unknown result type (might be due to invalid IL or missing references)
			//IL_042b: Unknown result type (might be due to invalid IL or missing references)
			//IL_04a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_04d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_04df: Unknown result type (might be due to invalid IL or missing references)
			//IL_04fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0538: Unknown result type (might be due to invalid IL or missing references)
			//IL_053d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0542: Unknown result type (might be due to invalid IL or missing references)
			//IL_05cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_05d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_046b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0470: Unknown result type (might be due to invalid IL or missing references)
			//IL_0475: Unknown result type (might be due to invalid IL or missing references)
			//IL_0686: Unknown result type (might be due to invalid IL or missing references)
			//IL_068b: Unknown result type (might be due to invalid IL or missing references)
			//IL_066b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0670: Unknown result type (might be due to invalid IL or missing references)
			//IL_05fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_05da: Unknown result type (might be due to invalid IL or missing references)
			//IL_06b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_06bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_06e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_06eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_071a: Unknown result type (might be due to invalid IL or missing references)
			//IL_071f: Unknown result type (might be due to invalid IL or missing references)
			//IL_07cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_07d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_079c: Unknown result type (might be due to invalid IL or missing references)
			//IL_07a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0760: Unknown result type (might be due to invalid IL or missing references)
			//IL_0765: Unknown result type (might be due to invalid IL or missing references)
			//IL_0745: Unknown result type (might be due to invalid IL or missing references)
			//IL_074a: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(rect, AE2Draw.Panel);
			if (sel == null || !comp.PlansForReading.Contains(sel))
			{
				sel = null;
				GUI.color = AE2Draw.TextDimCol;
				Text.Anchor = (TextAnchor)4;
				Widgets.Label(rect, Translator.Translate("DS_CA_PickOrder"));
				Text.Anchor = (TextAnchor)0;
				GUI.color = Color.white;
				return;
			}
			CraftPlan plan = sel;
			ThingDef val = CraftCategories.MainProduct(plan.recipe);
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(0f, 0f, ((Rect)(ref rect)).width - 20f, Math.Max(((Rect)(ref rect)).height - 12f, 580f));
			Widgets.BeginScrollView(new Rect(((Rect)(ref rect)).x + 6f, ((Rect)(ref rect)).y + 6f, ((Rect)(ref rect)).width - 10f, ((Rect)(ref rect)).height - 12f), ref scrollDetail, val2, true);
			float num = 4f;
			float num2 = 0f;
			if (val != null)
			{
				Widgets.DefIcon(new Rect(num, num2, 42f, 42f), (Def)(object)val, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
			}
			Text.Font = (GameFont)2;
			Widgets.Label(new Rect(num + 50f, num2, ((Rect)(ref val2)).width - 60f, 26f), (plan.recipe == null) ? "?" : ((object)((Def)plan.recipe).LabelCap/*cast due to constrained. prefix*/).ToString());
			Text.Font = (GameFont)1;
			GUI.color = AE2Draw.TextDimCol;
			Widgets.Label(new Rect(num + 50f, num2 + 24f, ((Rect)(ref val2)).width - 60f, 20f), TranslatorFormattedStringExtensions.Translate("DS_CA_Sub", NamedArgument.op_Implicit(Translator.Translate(CraftCategories.LabelKey(CraftCategories.Of(plan.recipe)))), NamedArgument.op_Implicit(SkillText(plan.recipe))));
			GUI.color = Color.white;
			num2 += 50f;
			SKind k = KindOf(plan);
			num2 = InfoRow(val2, num, num2, TaggedString.op_Implicit(Translator.Translate("DS_CA_Mode")), ModeText(plan), AE2Draw.TextCol);
			num2 = InfoRow(val2, num, num2, TaggedString.op_Implicit(Translator.Translate("DS_CA_Running")), ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_Lines", NamedArgument.op_Implicit(plan.lines.Count), NamedArgument.op_Implicit(comp.UsableBenchCountFor(plan.recipe)))/*cast due to constrained. prefix*/).ToString(), AE2Draw.TextCol);
			num2 = InfoRow(val2, num, num2, TaggedString.op_Implicit(Translator.Translate("DS_CA_Status")), StatusText(plan), ColorOf(k));
			num2 += 6f;
			CraftLine craftLine = ((plan.lines.Count > 0) ? plan.lines[0] : null);
			float num3 = ((craftLine != null && craftLine.HasWork) ? Mathf.Clamp01(craftLine.Progress01) : 0f);
			Widgets.Label(new Rect(num, num2, ((Rect)(ref val2)).width - 8f, 20f), Translator.Translate("DS_CA_Progress"));
			num2 += 20f;
			Rect val3 = default(Rect);
			((Rect)(ref val3))..ctor(num, num2, ((Rect)(ref val2)).width - 10f, 18f);
			Widgets.DrawBoxSolid(val3, AE2Draw.Slot);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref val3)).x, ((Rect)(ref val3)).y, ((Rect)(ref val3)).width * num3, ((Rect)(ref val3)).height), AE2Draw.Accent);
			Widgets.DrawBox(val3, 1, (Texture2D)null);
			num2 += 22f;
			GUI.color = AE2Draw.TextDimCol;
			string text = ((object)TranslatorFormattedStringExtensions.Translate("DS_CA_Percent", NamedArgument.op_Implicit((num3 * 100f).ToString("0")))/*cast due to constrained. prefix*/).ToString();
			if (craftLine != null && craftLine.HasWork)
			{
				text = text + "   ·   " + ((object)TranslatorFormattedStringExtensions.Translate("DS_CA_RemainTime", NamedArgument.op_Implicit(RemainSeconds(craftLine).ToString("0")))/*cast due to constrained. prefix*/).ToString();
			}
			Widgets.Label(new Rect(num, num2, ((Rect)(ref val2)).width - 8f, 20f), text);
			GUI.color = Color.white;
			num2 += 26f;
			Widgets.Label(new Rect(num, num2, ((Rect)(ref val2)).width - 8f, 20f), Translator.Translate("DS_CA_Needs"));
			num2 += 20f;
			num2 = DrawMaterials(plan, craftLine, val2, num, num2);
			num2 += 8f;
			FlowBegin(num, num2, ((Rect)(ref val2)).width - 8f);
			FlowButton(((object)TranslatorFormattedStringExtensions.Translate("DS_CA_ModeBtn", NamedArgument.op_Implicit(ModeText(plan)))/*cast due to constrained. prefix*/).ToString(), 132f, delegate
			{
				comp.CycleMode(plan);
				ClearCountBuffers();
			});
			bool flag = plan.mode == 1;
			bool flag2 = plan.mode == 2;
			if (flag || flag2)
			{
				FlowButton("−10", 42f, delegate
				{
					plan.AddCount(-10);
					ClearCountBuffers();
				});
				FlowButton("−1", 36f, delegate
				{
					plan.AddCount(-1);
					ClearCountBuffers();
				});
				Rect val4 = FlowRect(78f);
				if (flag)
				{
					Widgets.TextFieldNumeric<int>(val4, ref plan.remaining, ref remainingBuf, 0f, 1E+09f);
				}
				else
				{
					Widgets.TextFieldNumeric<int>(val4, ref plan.targetCount, ref targetBuf, 0f, 1E+09f);
				}
				FlowButton("+1", 36f, delegate
				{
					plan.AddCount(1);
					ClearCountBuffers();
				});
				FlowButton("+10", 42f, delegate
				{
					plan.AddCount(10);
					ClearCountBuffers();
				});
			}
			FlowButton(plan.suspended ? ((object)Translator.Translate("DS_BA_Resume")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("DS_BA_Suspend")/*cast due to constrained. prefix*/).ToString(), 60f, delegate
			{
				plan.suspended = !plan.suspended;
			});
			FlowButton(((object)Translator.Translate("DS_CA_Up")/*cast due to constrained. prefix*/).ToString(), 56f, delegate
			{
				comp.MovePlan(plan, -1);
			});
			FlowButton(((object)Translator.Translate("DS_CA_Down")/*cast due to constrained. prefix*/).ToString(), 56f, delegate
			{
				comp.MovePlan(plan, 1);
			});
			if (flag2)
			{
				FlowButton(((object)Translator.Translate("DS_CA_PauseWhenSatisfied")/*cast due to constrained. prefix*/).ToString() + "：" + (plan.pauseWhenSatisfied ? ((object)Translator.Translate("DS_BA_On")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("DS_BA_Off")/*cast due to constrained. prefix*/).ToString()), 146f, delegate
				{
					plan.pauseWhenSatisfied = !plan.pauseWhenSatisfied;
					plan.countedTick = 0;
				});
			}
			if (flag || flag2)
			{
				FlowButton(((object)Translator.Translate("DS_CA_SetForever")/*cast due to constrained. prefix*/).ToString(), 96f, delegate
				{
					plan.SetForever();
				});
			}
			FlowButton(((object)Translator.Translate("DS_CA_Delete")/*cast due to constrained. prefix*/).ToString(), 70f, delegate
			{
				comp.RemovePlan(plan);
				SelectPlan(null);
			});
			_ = sel;
			Widgets.EndScrollView();
		}

		private void FlowBegin(float x, float y, float maxX)
		{
			flowStartX = x;
			flowX = x;
			flowY = y;
			flowMaxX = maxX;
		}

		private Rect FlowRect(float w)
		{
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			if (flowX + w > flowMaxX && flowX > flowStartX)
			{
				flowX = flowStartX;
				flowY += 30f;
			}
			Rect result = new Rect(flowX, flowY, w, 26f);
			flowX += w + 4f;
			return result;
		}

		private void FlowButton(string label, float w, Action onClick)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			if (AE2Draw.AE2TextButton(FlowRect(w), label))
			{
				onClick?.Invoke();
			}
		}

		private float FlowEnd()
		{
			return flowY + 26f;
		}

		private void ClearCountBuffers()
		{
			remainingBuf = null;
			targetBuf = null;
		}

		private void SelectPlan(CraftPlan plan)
		{
			sel = plan;
			ClearCountBuffers();
		}

		private float RemainSeconds(CraftLine line)
		{
			float num = Math.Max(0.0001f, line.BaseRate);
			if (comp != null)
			{
				num *= Math.Max(0.01f, comp.Props.workSpeedMult) * comp.OverclockSpeedMult;
			}
			return Math.Max(0f, line.WorkLeft) / num / 60f;
		}

		private static string ModeText(CraftPlan plan)
		{
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			if (plan == null)
			{
				return "";
			}
			if (plan.mode == 1)
			{
				return ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_Remain", NamedArgument.op_Implicit(plan.remaining))/*cast due to constrained. prefix*/).ToString();
			}
			if (plan.mode == 2)
			{
				return ((object)TranslatorFormattedStringExtensions.Translate("DS_CA_Target", NamedArgument.op_Implicit(plan.countedCount), NamedArgument.op_Implicit(plan.targetCount))/*cast due to constrained. prefix*/).ToString();
			}
			return ((object)Translator.Translate("DS_BA_Forever")/*cast due to constrained. prefix*/).ToString();
		}

		private static string SkillText(RecipeDef r)
		{
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			if (r == null || r.skillRequirements == null || r.skillRequirements.Count == 0)
			{
				return "";
			}
			for (int i = 0; i < r.skillRequirements.Count; i++)
			{
				SkillRequirement val = r.skillRequirements[i];
				if (val != null)
				{
					return TaggedString.op_Implicit(((Def)val.skill).LabelCap + " ") + val.minLevel;
				}
			}
			return "";
		}

		private float InfoRow(Rect inner, float x, float y, string label, string value, Color valueColor)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			GUI.color = AE2Draw.TextDimCol;
			Widgets.Label(new Rect(x, y, 70f, 20f), label);
			GUI.color = valueColor;
			Widgets.Label(new Rect(x + 74f, y, ((Rect)(ref inner)).width - 84f, 20f), value);
			GUI.color = Color.white;
			return y + 21f;
		}

		private float DrawMaterials(CraftPlan plan, CraftLine line, Rect inner, float x, float y)
		{
			//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0100: Unknown result type (might be due to invalid IL or missing references)
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0114: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			bool flag = false;
			if (line != null && line.HasWork && line.Ingredients != null)
			{
				for (int i = 0; i < line.Ingredients.Length && i < line.Counts.Length; i++)
				{
					Thing val = line.Ingredients[i];
					if (val != null && val.def != null)
					{
						flag = true;
						y = MatRow(inner, x, y, val.def, line.Counts[i]);
					}
				}
			}
			if (!flag && plan.recipe != null && plan.recipe.ingredients != null)
			{
				for (int j = 0; j < plan.recipe.ingredients.Count; j++)
				{
					IngredientCount val2 = plan.recipe.ingredients[j];
					if (val2 != null)
					{
						flag = true;
						ThingDef def = (val2.IsFixedIngredient ? val2.FixedIngredient : null);
						y = MatRow(inner, x, y, def, -1, val2.Summary);
					}
				}
			}
			if (!flag)
			{
				GUI.color = AE2Draw.TextDimCol;
				Widgets.Label(new Rect(x, y, ((Rect)(ref inner)).width - 8f, 20f), Translator.Translate("DS_CA_NoMaterials"));
				GUI.color = Color.white;
				y += 20f;
			}
			return y;
		}

		private float MatRow(Rect inner, float x, float y, ThingDef def, int need, string textOverride = null)
		{
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_011c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0128: Unknown result type (might be due to invalid IL or missing references)
			//IL_012f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0134: Unknown result type (might be due to invalid IL or missing references)
			//IL_013e: Unknown result type (might be due to invalid IL or missing references)
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(x, y, ((Rect)(ref inner)).width - 10f, 22f);
			if (def != null)
			{
				Widgets.DefIcon(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).y + 2f, 18f, 18f), (Def)(object)def, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
			}
			string text = ((textOverride != null) ? textOverride : ((def == null) ? "?" : ((object)((Def)def).LabelCap/*cast due to constrained. prefix*/).ToString()));
			Widgets.Label(new Rect(((Rect)(ref val)).x + 22f, ((Rect)(ref val)).y + 2f, ((Rect)(ref val)).width - 130f, 20f), text);
			Text.Anchor = (TextAnchor)5;
			if (need >= 0)
			{
				int num = StockOf(def);
				GUI.color = ((num < need) ? AE2Draw.Bad : AE2Draw.TextDimCol);
				Widgets.Label(new Rect(((Rect)(ref val)).xMax - 120f, ((Rect)(ref val)).y + 2f, 116f, 20f), TranslatorFormattedStringExtensions.Translate("DS_CA_Have", NamedArgument.op_Implicit(need), NamedArgument.op_Implicit(num)));
				GUI.color = Color.white;
			}
			Text.Anchor = (TextAnchor)0;
			return y + 23f;
		}

		private static SKind KindOf(CraftPlan plan)
		{
			if (plan == null)
			{
				return SKind.Idle;
			}
			if (plan.Done)
			{
				return SKind.Done;
			}
			if (plan.suspended)
			{
				return SKind.Idle;
			}
			if (plan.mode == 2 && plan.paused)
			{
				return SKind.Done;
			}
			CraftLine craftLine = ((plan.lines.Count > 0) ? plan.lines[0] : null);
			if (craftLine != null && craftLine.HasWork)
			{
				return SKind.Ok;
			}
			if (plan.lines.Count == 0 && plan.mode != 2)
			{
				return SKind.Bad;
			}
			if (plan.lines.Count == 0 && plan.mode == 2 && !plan.paused)
			{
				return SKind.Warn;
			}
			string text = craftLine?.BlockKey;
			if (text == null)
			{
				return SKind.Ok;
			}
			switch (text)
			{
			case "DS_BA_Block_Material":
			case "DS_BA_Block_Research":
			case "DS_BA_NoCoreMaterial":
			case "DS_BA_Block_Skill":
			case "DS_BA_Block_Unsupported":
			case "DS_BA_Block_Restricted":
			case "DS_BA_NoBill":
				return SKind.Bad;
			default:
				return SKind.Warn;
			}
		}

		private static string StatusText(CraftPlan plan)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0114: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0154: Unknown result type (might be due to invalid IL or missing references)
			//IL_0135: Unknown result type (might be due to invalid IL or missing references)
			//IL_013a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			if (plan.Done)
			{
				return ((object)Translator.Translate("DS_BA_PlanFinished")/*cast due to constrained. prefix*/).ToString();
			}
			if (plan.suspended)
			{
				return ((object)Translator.Translate("DS_BA_Suspended")/*cast due to constrained. prefix*/).ToString();
			}
			if (plan.mode == 2 && plan.paused)
			{
				return ((object)TranslatorFormattedStringExtensions.Translate("DS_CA_TargetPaused", NamedArgument.op_Implicit(plan.countedCount), NamedArgument.op_Implicit(plan.targetCount))/*cast due to constrained. prefix*/).ToString();
			}
			CraftLine craftLine = ((plan.lines.Count > 0) ? plan.lines[0] : null);
			if (craftLine == null)
			{
				if (plan.mode != 2 || plan.countedCount >= plan.targetCount)
				{
					return ((object)TranslatorFormattedStringExtensions.Translate("DS_CA_TargetPaused", NamedArgument.op_Implicit(plan.countedCount), NamedArgument.op_Implicit(plan.targetCount))/*cast due to constrained. prefix*/).ToString();
				}
				return ((object)Translator.Translate("DS_BA_NoBench")/*cast due to constrained. prefix*/).ToString();
			}
			if (craftLine.HasWork)
			{
				return ((object)Translator.Translate("DS_CA_Working")/*cast due to constrained. prefix*/).ToString();
			}
			if (GenText.NullOrEmpty(craftLine.BlockKey))
			{
				return ((object)Translator.Translate("DS_CA_Waiting")/*cast due to constrained. prefix*/).ToString();
			}
			return ((object)Translator.Translate(craftLine.BlockKey)/*cast due to constrained. prefix*/).ToString();
		}
	}
	public class MainButtonWorker_CraftAutomation : MainButtonWorker
	{
		public override void Activate()
		{
			Window_CraftAutomation.OpenOrFocus(null);
		}
	}
	public class Dialog_WithdrawAmount : Window
	{
		private readonly string title;

		private readonly int available;

		private readonly int maxCarry;

		private readonly int upperBound;

		private readonly Action<int> onConfirm;

		private string amountStr;

		private int amount;

		public override Vector2 InitialSize => new Vector2(360f, 220f);

		public Dialog_WithdrawAmount(string title, int available, int maxCarry, Action<int> onConfirm)
			: base((IWindowDrawing)null)
		{
			this.title = title;
			this.available = Math.Max(available, 0);
			this.maxCarry = maxCarry;
			this.onConfirm = onConfirm;
			upperBound = ((maxCarry > 0) ? Math.Min(this.available, maxCarry) : this.available);
			if (upperBound <= 0)
			{
				upperBound = 1;
			}
			amount = upperBound;
			amountStr = amount.ToString();
			base.forcePause = true;
			base.doCloseX = false;
			base.absorbInputAroundWindow = false;
			base.closeOnClickedOutside = true;
			base.doWindowBackground = false;
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_013a: Unknown result type (might be due to invalid IL or missing references)
			//IL_013f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			//IL_019b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_010b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0207: Unknown result type (might be due to invalid IL or missing references)
			//IL_020e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0213: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				bool closeClicked;
				Rect val = AE2Draw.WindowFrame(GenUI.ContractedBy(inRect, 2f), ((object)TranslatorFormattedStringExtensions.Translate("DS_WithdrawTitle", NamedArgument.op_Implicit(title))/*cast due to constrained. prefix*/).ToString(), out closeClicked);
				if (closeClicked)
				{
					((Window)this).Close(true);
					return;
				}
				AE2Draw.HandlePauseHotkey();
				float y = ((Rect)(ref val)).y;
				AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 2f, y, ((Rect)(ref val)).width - 4f, 18f), ((object)TranslatorFormattedStringExtensions.Translate("DS_WithdrawAvailable", NamedArgument.op_Implicit(available))/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextCol, (TextAnchor)3);
				y += 20f;
				if (maxCarry > 0)
				{
					AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 2f, y, ((Rect)(ref val)).width - 4f, 18f), ((object)TranslatorFormattedStringExtensions.Translate("DS_WithdrawCarryLimit", NamedArgument.op_Implicit(maxCarry))/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextDimCol, (TextAnchor)3);
					y += 20f;
				}
				y += 6f;
				Rect val2 = new Rect(((Rect)(ref val)).x, y, ((Rect)(ref val)).width, 30f);
				AE2Draw.Sunken(val2, AE2Draw.Slot);
				Widgets.TextFieldNumeric<int>(GenUI.ContractedBy(val2, 4f), ref amount, ref amountStr, 1f, (float)upperBound);
				y += 38f;
				float num = ((Rect)(ref val)).width / 2f - 6f;
				Rect r = new Rect(((Rect)(ref val)).x, y, num, 28f);
				Rect r2 = default(Rect);
				((Rect)(ref r2))..ctor(((Rect)(ref val)).x + num + 12f, y, num, 28f);
				if (AE2Draw.TextButton(r, ((object)Translator.Translate("DS_Confirm")/*cast due to constrained. prefix*/).ToString()))
				{
					if (amount > 0 && onConfirm != null)
					{
						onConfirm(amount);
					}
					((Window)this).Close(true);
				}
				if (AE2Draw.TextButton(r2, ((object)Translator.Translate("DS_Cancel")/*cast due to constrained. prefix*/).ToString()))
				{
					((Window)this).Close(true);
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] AE2 界面绘制异常（只记一次，界面不会卡死）：" + ex, 771003);
			}
		}
	}
	public class Dialog_StorageFilter : Window
	{
		private readonly Building_StorageCore core;

		private UIState uiState = new UIState();

		public override Vector2 InitialSize => new Vector2(300f, 480f);

		public Dialog_StorageFilter(Building_StorageCore core)
			: base((IWindowDrawing)null)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Expected O, but got Unknown
			this.core = core;
			base.doCloseX = true;
			base.absorbInputAroundWindow = false;
			base.closeOnClickedOutside = true;
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			ThingFilterUI.DoThingFilterConfigWindow(inRect, uiState, core.StorageFilter, core.GetParentFilterPublic(), 8, (IEnumerable<ThingDef>)null, (IEnumerable<SpecialThingFilterDef>)null, false, false, false, (List<ThingDef>)null, (Map)null);
		}
	}
	internal static class AE2Draw
	{
		private static Texture2D _glowTex;

		public static Color Panel => AE2Theme.Active.Panel;

		public static Color PanelFace => AE2Theme.Active.PanelFace;

		public static Color Button => AE2Theme.Active.Button;

		public static Color ButtonHover => AE2Theme.Active.ButtonHover;

		public static Color Slot => AE2Theme.Active.Slot;

		public static Color SlotHover => AE2Theme.Active.SlotHover;

		public static Color BarBg => AE2Theme.Active.BarBg;

		public static Color Hi => AE2Theme.Active.TextHi;

		public static Color Lo => AE2Theme.Active.OutlineCol;

		public static Color OutlineCol => AE2Theme.Active.OutlineCol;

		public static Color LineSoft => AE2Theme.Active.LineSoft;

		public static Color BtnHoverFace => AE2Theme.Active.BtnHoverFace;

		public static Color BtnHoverTop => AE2Theme.Active.BtnHoverTop;

		public static Color BtnHoverShadow => AE2Theme.Active.BtnHoverShadow;

		public static Color BtnHoverEdge => AE2Theme.Active.BtnHoverEdge;

		public static Color EdgeHi => AE2Theme.Active.EdgeHi;

		public static Color EdgeLo => AE2Theme.Active.EdgeLo;

		public static Color EdgeDeep => AE2Theme.Active.EdgeDeep;

		public static Color TextCol => AE2Theme.Active.TextCol;

		public static Color TextDimCol => AE2Theme.Active.TextDimCol;

		public static Color TextHi => AE2Theme.Active.TextHi;

		public static Color ButtonText => AE2Theme.Active.ButtonText;

		public static Color ButtonTextDim => AE2Theme.Active.ButtonTextDim;

		public static Color Accent => AE2Theme.Active.Accent;

		public static Color AccentText => AE2Theme.Active.AccentText;

		public static Color AccentBright => AE2Theme.Active.AccentBright;

		public static Color AccentDim => AE2Theme.Active.AccentDim;

		public static Color Warn => AE2Theme.Active.Warn;

		public static Color Bad => AE2Theme.Active.Bad;

		public static Color Ok => AE2Theme.Active.Ok;

		public static Color Bg => AE2Theme.Active.Bg;

		public static bool IsDark => AE2Theme.IsDark;

		public static void BevelRaised(Rect r, Color face)
		{
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_010d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Unknown result type (might be due to invalid IL or missing references)
			//IL_0130: Unknown result type (might be due to invalid IL or missing references)
			//IL_0135: Unknown result type (might be due to invalid IL or missing references)
			//IL_015b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0160: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			float x = ((Rect)(ref r)).x;
			float y = ((Rect)(ref r)).y;
			float width = ((Rect)(ref r)).width;
			float height = ((Rect)(ref r)).height;
			Widgets.DrawBoxSolid(new Rect(x + 1f, y + 1f, width - 2f, height - 2f), face);
			Widgets.DrawBoxSolid(new Rect(x + 1f, y + 1f, width - 2f, 1f), Slot);
			if (height > 5f)
			{
				Widgets.DrawBoxSolid(new Rect(x + 1f, y + height - 5f, width - 2f, 1f), Slot);
				Widgets.DrawBoxSolid(new Rect(x + 1f, y + height - 4f, width - 2f, 3f), EdgeDeep);
			}
			Widgets.DrawBoxSolid(new Rect(x + 1f, y, width - 2f, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x + 1f, y + height - 1f, width - 2f, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x, y + 1f, 1f, height - 2f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x + width - 1f, y + 1f, 1f, height - 2f), OutlineCol);
		}

		public static void BevelSunken(Rect r, Color face)
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_010b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
			float x = ((Rect)(ref r)).x;
			float y = ((Rect)(ref r)).y;
			float width = ((Rect)(ref r)).width;
			float height = ((Rect)(ref r)).height;
			Widgets.DrawBoxSolid(r, face);
			Widgets.DrawBoxSolid(new Rect(x + 1f, y + 1f, width - 2f, 1f), EdgeLo);
			if (height > 5f)
			{
				Widgets.DrawBoxSolid(new Rect(x + 1f, y + height - 4f, width - 2f, 1f), EdgeHi);
				Widgets.DrawBoxSolid(new Rect(x + 1f, y + height - 3f, width - 2f, 2f), LineSoft);
			}
			Widgets.DrawBoxSolid(new Rect(x, y, width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x, y + height - 1f, width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x, y, 1f, height), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x + width - 1f, y, 1f, height), OutlineCol);
		}

		public static void Outline(Rect r)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, ((Rect)(ref r)).width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).yMax - 1f, ((Rect)(ref r)).width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).xMax - 1f, ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), OutlineCol);
		}

		public static void OutlineAccentBox(Rect r)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, ((Rect)(ref r)).width, 1f), Accent);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).yMax - 1f, ((Rect)(ref r)).width, 1f), Accent);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), Accent);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).xMax - 1f, ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), Accent);
		}

		public static void PanelBox(Rect r)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(r, Panel);
			Outline(r);
		}

		public static void PanelRaised(Rect r)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(r, PanelFace);
			Outline(r);
		}

		public static void SlotBox(Rect r, bool hover)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(r, hover ? SlotHover : EdgeHi);
			if (((Rect)(ref r)).width > 2f && ((Rect)(ref r)).height > 2f)
			{
				Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x + 1f, ((Rect)(ref r)).y + 1f, ((Rect)(ref r)).width - 2f, ((Rect)(ref r)).height - 2f), Slot);
			}
		}

		public static void Sunken(Rect r, Color fill)
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			float x = ((Rect)(ref r)).x;
			float y = ((Rect)(ref r)).y;
			float width = ((Rect)(ref r)).width;
			float height = ((Rect)(ref r)).height;
			Widgets.DrawBoxSolid(r, fill);
			Widgets.DrawBoxSolid(new Rect(x + 1f, y + 1f, width - 2f, 1f), EdgeHi);
			if (height > 4f)
			{
				Widgets.DrawBoxSolid(new Rect(x + 1f, y + height - 3f, width - 2f, 1f), LineSoft);
			}
			Widgets.DrawBoxSolid(new Rect(x, y, width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x, y + height - 1f, width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x, y, 1f, height), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x + width - 1f, y, 1f, height), OutlineCol);
		}

		public static void Divider(Rect r)
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y + Mathf.Floor(((Rect)(ref r)).height * 0.5f), ((Rect)(ref r)).width, 1f), LineSoft);
		}

		public static void DividerV(Rect r)
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x + Mathf.Floor(((Rect)(ref r)).width * 0.5f), ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), LineSoft);
		}

		public static Rect SectionHeader(Rect r, string title)
		{
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(((Rect)(ref r)).x, ((Rect)(ref r)).y, ((Rect)(ref r)).width, 17f);
			Tiny(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width, 15f), title, TextDimCol);
			Divider(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).yMax - 2f, ((Rect)(ref val)).width, 4f));
			return new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y + 17f, ((Rect)(ref r)).width, ((Rect)(ref r)).height - 17f);
		}

		public static void FlatButton(Rect r, bool active)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			if (active)
			{
				Widgets.DrawBoxSolid(r, Slot);
				Outline(r);
			}
			else
			{
				AE2Button(r, active: false, hover: false);
			}
		}

		public static void FlatButtonHover(Rect r, bool active)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			if (active)
			{
				FlatButton(r, active: true);
			}
			else
			{
				AE2Button(r, active: false, Mouse.IsOver(r));
			}
		}

		public static void Row(Rect r, bool hover, bool selected)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			if (selected)
			{
				Widgets.DrawBoxSolid(r, Slot);
				Outline(r);
			}
			else
			{
				Widgets.DrawBoxSolid(r, hover ? ButtonHover : Button);
			}
		}

		public static void Bar(Rect r, float frac, bool selected)
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0103: Unknown result type (might be due to invalid IL or missing references)
			//IL_0108: Unknown result type (might be due to invalid IL or missing references)
			float x = ((Rect)(ref r)).x;
			float y = ((Rect)(ref r)).y;
			float width = ((Rect)(ref r)).width;
			float height = ((Rect)(ref r)).height;
			Widgets.DrawBoxSolid(r, BarBg);
			Widgets.DrawBoxSolid(new Rect(x, y, width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x, y + height - 1f, width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x, y, 1f, height), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x + width - 1f, y, 1f, height), OutlineCol);
			float num = Mathf.Clamp01(frac);
			if (!(num <= 0f))
			{
				Color val = (selected ? AccentBright : ((num >= 1f) ? Accent : Warn));
				if (width > 2f && height > 2f)
				{
					Widgets.DrawBoxSolid(new Rect(x + 1f, y + 1f, Mathf.Max(1f, (width - 2f) * num), height - 2f), val);
				}
			}
		}

		private static void ScrollTrack(Rect r)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(r, EdgeDeep);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, ((Rect)(ref r)).width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).yMax - 1f, ((Rect)(ref r)).width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).xMax - 1f, ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), OutlineCol);
		}

		private static void ScrollThumb(Rect r, bool hover)
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_011c: Unknown result type (might be due to invalid IL or missing references)
			float num = ((Rect)(ref r)).height;
			if (num < 8f)
			{
				num = 8f;
			}
			float num2 = Mathf.Max(1f, num - 6f);
			Color val = (hover ? ButtonHover : Button);
			Widgets.DrawBoxSolid(r, val);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, ((Rect)(ref r)).width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y + 1f, ((Rect)(ref r)).width, 1f), Slot);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y + 1f + num2, ((Rect)(ref r)).width, 1f), Slot);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).yMax - 3f, ((Rect)(ref r)).width, 2f), EdgeDeep);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).yMax - 1f, ((Rect)(ref r)).width, 1f), OutlineCol);
		}

		public static void DragBar(Rect r, ref float frac, float viewPct)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Invalid comparison between Unknown and I4
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Invalid comparison between Unknown and I4
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			ScrollTrack(r);
			float num = Mathf.Clamp(((Rect)(ref r)).height * Mathf.Clamp01(viewPct), 22f, ((Rect)(ref r)).height);
			float num2 = Mathf.Max(1f, ((Rect)(ref r)).height - num);
			Event current = Event.current;
			if (current != null && Mouse.IsOver(r))
			{
				if (((int)current.type == 0 || (int)current.type == 3) && current.button == 0)
				{
					frac = Mathf.Clamp01((current.mousePosition.y - ((Rect)(ref r)).y - num * 0.5f) / num2);
					current.Use();
				}
				else if ((int)current.type == 6)
				{
					frac = Mathf.Clamp01(frac + current.delta.y * 0.06f);
					current.Use();
				}
			}
			Rect val = new Rect(((Rect)(ref r)).x + 1f, ((Rect)(ref r)).y + num2 * Mathf.Clamp01(frac), ((Rect)(ref r)).width - 2f, num - 2f);
			ScrollThumb(val, Mouse.IsOver(val));
		}

		public static Rect WindowFrame(Rect r, string title, out bool closeClicked)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			bool themeClicked;
			return WindowFrame(r, title, out closeClicked, out themeClicked);
		}

		public static Rect WindowFrame(Rect r, string title, out bool closeClicked, out bool themeClicked)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
			closeClicked = false;
			themeClicked = false;
			PanelBox(r);
			Rect val = GenUI.ContractedBy(r, 6f);
			Rect r2 = default(Rect);
			((Rect)(ref r2))..ctor(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width, 24f);
			BevelRaised(r2, PanelFace);
			Small(new Rect(((Rect)(ref r2)).x + 8f, ((Rect)(ref r2)).y + 2f, ((Rect)(ref r2)).width - 38f, 22f), title, TextHi);
			Rect val2 = new Rect(((Rect)(ref r2)).xMax - 20f, ((Rect)(ref r2)).y + 4f, 16f, 16f);
			Mouse.IsOver(val2);
			if (AE2TextButton(val2, "×"))
			{
				closeClicked = true;
			}
			return new Rect(((Rect)(ref val)).x, ((Rect)(ref r2)).yMax + 4f, ((Rect)(ref val)).width, ((Rect)(ref val)).yMax - ((Rect)(ref r2)).yMax - 4f);
		}

		public static string TextField(Rect r, string text)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			BevelSunken(r, Slot);
			return Widgets.TextField(GenUI.ContractedBy(r, 3f), text);
		}

		public static bool TextButton(Rect r, string label, bool active = false)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			bool flag = Mouse.IsOver(r);
			if (active)
			{
				Widgets.DrawBoxSolid(r, Slot);
				Outline(r);
			}
			else if (flag)
			{
				AE2Button(r, active: false, hover: true);
			}
			else
			{
				AE2Button(r, active: false, hover: false);
			}
			TextIn(r, label, 2f, active ? TextCol : (flag ? AccentText : ButtonText), (TextAnchor)4);
			return Widgets.ButtonInvisible(r, true);
		}

		public static void StatusDot(Rect r, Color c)
		{
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			float num = Mathf.Min(((Rect)(ref r)).width, ((Rect)(ref r)).height);
			float num2 = ((Rect)(ref r)).x + (((Rect)(ref r)).width - num) * 0.5f;
			float num3 = ((Rect)(ref r)).y + (((Rect)(ref r)).height - num) * 0.5f;
			Widgets.DrawBoxSolid(new Rect(num2 + 1f, num3, num - 2f, 1f), c);
			Widgets.DrawBoxSolid(new Rect(num2, num3 + 1f, num, num - 2f), c);
			Widgets.DrawBoxSolid(new Rect(num2 + 1f, num3 + num - 1f, num - 2f, 1f), c);
		}

		private static Texture2D GlowTex()
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Expected O, but got Unknown
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)_glowTex != (Object)null)
			{
				return _glowTex;
			}
			_glowTex = new Texture2D(64, 64, (TextureFormat)5, false);
			((Texture)_glowTex).wrapMode = (TextureWrapMode)1;
			((Texture)_glowTex).filterMode = (FilterMode)1;
			Color[] array = (Color[])(object)new Color[4096];
			float num = 32f;
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 64; j++)
				{
					float num2 = ((float)j + 0.5f - num) / num;
					float num3 = ((float)i + 0.5f - num) / num;
					float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
					float num5 = Mathf.Clamp01(1f - num4);
					num5 = num5 * num5 * (3f - 2f * num5);
					array[i * 64 + j] = new Color(1f, 1f, 1f, num5);
				}
			}
			_glowTex.SetPixels(array);
			_glowTex.Apply();
			return _glowTex;
		}

		public static void GlowDot(Rect r, int state, bool pulse)
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0109: Unknown result type (might be due to invalid IL or missing references)
			//IL_010e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_014e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0154: Unknown result type (might be due to invalid IL or missing references)
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0161: Unknown result type (might be due to invalid IL or missing references)
			//IL_0177: Unknown result type (might be due to invalid IL or missing references)
			//IL_0190: Unknown result type (might be due to invalid IL or missing references)
			//IL_0195: Unknown result type (might be due to invalid IL or missing references)
			Color val = (Color)(state switch
			{
				1 => Accent, 
				2 => AccentBright, 
				3 => Warn, 
				4 => TextDimCol, 
				5 => Bad, 
				_ => Warn, 
			});
			float num = Mathf.Min(((Rect)(ref r)).width, ((Rect)(ref r)).height);
			Rect val2 = default(Rect);
			((Rect)(ref val2))..ctor(((Rect)(ref r)).x + (((Rect)(ref r)).width - num) * 0.5f, ((Rect)(ref r)).y + (((Rect)(ref r)).height - num) * 0.5f, num, num);
			float num2 = 1f;
			if (pulse && state == 1)
			{
				num2 = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.realtimeSinceStartup * 3.6f));
			}
			Color color = GUI.color;
			Color color2 = val;
			color2.a = 0.55f * num2;
			GUI.color = color2;
			GUI.DrawTexture(val2, (Texture)(object)GlowTex(), (ScaleMode)0, true, 0f, Color.white, new Vector4(0.2f, 0.2f, 0.8f, 0.8f), Vector4.zero);
			float num3 = num * 0.42f;
			Rect val3 = new Rect(((Rect)(ref val2)).x + (num - num3) * 0.5f, ((Rect)(ref val2)).y + (num - num3) * 0.5f, num3, num3);
			GUI.color = new Color(val.r, val.g, val.b, num2);
			GUI.DrawTexture(val3, (Texture)(object)GlowTex(), (ScaleMode)0, true, 0f, Color.white, new Vector4(0.3f, 0.3f, 0.7f, 0.7f), Vector4.zero);
			GUI.color = color;
		}

		public static void IconSlot(Rect r, ThingDef def, bool hover)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			SlotBox(r, hover);
			if (def != null)
			{
				Widgets.DefIcon(GenUI.ContractedBy(r, 2f), (Def)(object)def, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
			}
		}

		public static void WheelScroll(Rect area, ref float frac, int maxStart)
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Invalid comparison between Unknown and I4
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			Event current = Event.current;
			if (current != null && Mouse.IsOver(area) && (int)current.type == 6)
			{
				frac = Mathf.Clamp01(frac + current.delta.y / Mathf.Max(1f, (float)maxStart));
				current.Use();
			}
		}

		public static void WheelScroll(Rect area, ref float frac)
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Invalid comparison between Unknown and I4
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			Event current = Event.current;
			if (current != null && Mouse.IsOver(area) && (int)current.type == 6)
			{
				frac = Mathf.Clamp01(frac + current.delta.y * 0.06f);
				current.Use();
			}
		}

		public static void HandlePauseHotkey()
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			Event current = Event.current;
			if (current != null && (int)current.type == 0 && !string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
			{
				GUI.FocusControl((string)null);
			}
		}

		public static Rect Fit(Rect r, float minH)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			if (((Rect)(ref r)).height < minH)
			{
				((Rect)(ref r)).height = minH;
			}
			return r;
		}

		public static void Tiny(Rect r, string s, Color c)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			r = Fit(r, 17f);
			GameFont font = Text.Font;
			Color color = GUI.color;
			Text.Font = (GameFont)0;
			GUI.color = c;
			Widgets.Label(r, s);
			GUI.color = color;
			Text.Font = font;
		}

		public static void Small(Rect r, string s, Color c)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			r = Fit(r, 21f);
			GameFont font = Text.Font;
			Color color = GUI.color;
			Text.Font = (GameFont)1;
			GUI.color = c;
			Widgets.Label(r, s);
			GUI.color = color;
			Text.Font = font;
		}

		public static void AE2Button(Rect r, bool active, bool hover)
		{
			//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_0137: Unknown result type (might be due to invalid IL or missing references)
			//IL_013c: Unknown result type (might be due to invalid IL or missing references)
			//IL_015c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0161: Unknown result type (might be due to invalid IL or missing references)
			//IL_0181: Unknown result type (might be due to invalid IL or missing references)
			//IL_0186: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0204: Unknown result type (might be due to invalid IL or missing references)
			//IL_033d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0251: Unknown result type (might be due to invalid IL or missing references)
			//IL_0256: Unknown result type (might be due to invalid IL or missing references)
			//IL_0276: Unknown result type (might be due to invalid IL or missing references)
			//IL_027b: Unknown result type (might be due to invalid IL or missing references)
			//IL_029b: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0313: Unknown result type (might be due to invalid IL or missing references)
			//IL_0318: Unknown result type (might be due to invalid IL or missing references)
			//IL_0400: Unknown result type (might be due to invalid IL or missing references)
			//IL_0405: Unknown result type (might be due to invalid IL or missing references)
			//IL_0422: Unknown result type (might be due to invalid IL or missing references)
			//IL_0427: Unknown result type (might be due to invalid IL or missing references)
			//IL_044a: Unknown result type (might be due to invalid IL or missing references)
			//IL_044f: Unknown result type (might be due to invalid IL or missing references)
			//IL_034c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0345: Unknown result type (might be due to invalid IL or missing references)
			//IL_036c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0371: Unknown result type (might be due to invalid IL or missing references)
			//IL_0391: Unknown result type (might be due to invalid IL or missing references)
			//IL_0396: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_03bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_03db: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e0: Unknown result type (might be due to invalid IL or missing references)
			float x = ((Rect)(ref r)).x;
			float y = ((Rect)(ref r)).y;
			float width = ((Rect)(ref r)).width;
			int num = Mathf.Max(8, Mathf.RoundToInt(((Rect)(ref r)).height));
			float num2 = (float)num / 20f;
			int num3 = Mathf.Max(1, Mathf.RoundToInt(1f * num2));
			int num4 = Mathf.Max(5, Mathf.RoundToInt(5f * num2));
			int num5 = Mathf.Max(4, Mathf.RoundToInt(4f * num2));
			if (num3 * 2 + num4 > num)
			{
				num3 = Mathf.Max(1, (num - num4) / 2);
			}
			int num6 = (int)y + num3;
			int num7 = (int)y + num3 * 2;
			int num8 = (int)y + num - num4;
			int num9 = num8 + num3;
			int num10 = (int)y + num - num3;
			int num11 = Mathf.Max(1, num8 - num7);
			int num12 = Mathf.Max(1, num10 - num9);
			if (hover && active)
			{
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num7, width - (float)num3 * 2f, (float)num11), SlotHover);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num6, width - (float)num3 * 2f, (float)num3), Slot);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num8, width - (float)num3 * 2f, (float)num3), Slot);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num9, width - (float)num3 * 2f, (float)num12), EdgeDeep);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)(int)y, width - (float)num3 * 2f, (float)num3), OutlineCol);
				Widgets.DrawBoxSolid(new Rect(x, (float)num6, (float)num3, (float)Mathf.Max(1, num10 - num6)), OutlineCol);
				Widgets.DrawBoxSolid(new Rect(x + width - (float)num3, (float)num6, (float)num3, (float)Mathf.Max(1, num10 - num6)), OutlineCol);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num10, width - (float)num3 * 2f, (float)num3), OutlineCol);
				OutlineAccentBox(new Rect(x, y, width, (float)num));
			}
			else if (hover)
			{
				int num13 = (int)y + num - num5;
				int num14 = num13 + num3;
				int num15 = (int)y + num - num3;
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num6, width - (float)num3 * 2f, (float)Mathf.Max(1, num13 - num6)), BtnHoverFace);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num6, width - (float)num3 * 2f, (float)num3), OutlineCol);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num7, width - (float)num3 * 2f, (float)num3), BtnHoverTop);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num13, width - (float)num3 * 2f, (float)num3), BtnHoverTop);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num14, width - (float)num3 * 2f, (float)Mathf.Max(1, num15 - num14)), BtnHoverShadow);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)(int)y, width - (float)num3 * 2f, (float)num3), BtnHoverEdge);
			}
			else
			{
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num7, width - (float)num3 * 2f, (float)num11), active ? SlotHover : Button);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num6, width - (float)num3 * 2f, (float)num3), Slot);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num8, width - (float)num3 * 2f, (float)num3), Slot);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num9, width - (float)num3 * 2f, (float)num12), EdgeDeep);
				Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)(int)y, width - (float)num3 * 2f, (float)num3), OutlineCol);
			}
			Widgets.DrawBoxSolid(new Rect(x + (float)num3, (float)num10, width - (float)num3 * 2f, (float)num3), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x, (float)num6, (float)num3, (float)Mathf.Max(1, num10 - num6)), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(x + width - (float)num3, (float)num6, (float)num3, (float)Mathf.Max(1, num10 - num6)), OutlineCol);
		}

		public static bool AE2TextButton(Rect r, string label, bool active = false)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			bool hover = Mouse.IsOver(r);
			AE2Button(r, active, hover);
			if (!string.IsNullOrEmpty(label))
			{
				TextIn(r, label, 2f, TextCol, (TextAnchor)4);
			}
			return Widgets.ButtonInvisible(r, true);
		}

		public static void TextIn(Rect r, string s, float pad, Color c, TextAnchor anchor)
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_0108: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
			if (string.IsNullOrEmpty(s) || ((Rect)(ref r)).width <= pad * 2f || ((Rect)(ref r)).height < 6f)
			{
				return;
			}
			GameFont font = Text.Font;
			TextAnchor anchor2 = Text.Anchor;
			Color color = GUI.color;
			bool wordWrap = Text.WordWrap;
			float num = ((Rect)(ref r)).width - pad * 2f;
			Text.Font = (GameFont)0;
			float x = Text.CalcSize(s).x;
			if (x > num && x > 1f)
			{
				int num2 = Mathf.Max(1, Mathf.FloorToInt((float)s.Length * (num / x)) - 1);
				s = ((num2 < s.Length) ? (s.Substring(0, num2) + "…") : s);
			}
			else if (((Rect)(ref r)).height >= 21f)
			{
				Text.Font = (GameFont)1;
				if (Text.CalcSize(s).x > num)
				{
					Text.Font = (GameFont)0;
				}
			}
			Text.Anchor = anchor;
			Text.WordWrap = false;
			GUI.color = c;
			Widgets.Label(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y - 1f, ((Rect)(ref r)).width, ((Rect)(ref r)).height + 2f), s);
			GUI.color = color;
			Text.WordWrap = wordWrap;
			Text.Anchor = anchor2;
			Text.Font = font;
		}

		public static void ProgressBar(Rect r, float frac, Color fill, string label, bool selected)
		{
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_014a: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0190: Unknown result type (might be due to invalid IL or missing references)
			//IL_0195: Unknown result type (might be due to invalid IL or missing references)
			//IL_0143: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			float num = 0f;
			if (!string.IsNullOrEmpty(label))
			{
				num = Mathf.Ceil(Text.CalcSize(label).x) + 6f;
				if (num > ((Rect)(ref r)).width * 0.6f)
				{
					num = 0f;
				}
			}
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(((Rect)(ref r)).x, ((Rect)(ref r)).y, Mathf.Max(6f, ((Rect)(ref r)).width - num), ((Rect)(ref r)).height);
			Widgets.DrawBoxSolid(val, BarBg);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).yMax - 1f, ((Rect)(ref val)).width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).y, 1f, ((Rect)(ref val)).height), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref val)).xMax - 1f, ((Rect)(ref val)).y, 1f, ((Rect)(ref val)).height), OutlineCol);
			float num2 = Mathf.Clamp01(frac);
			if (num2 > 0f)
			{
				Color val2 = (selected ? AccentBright : ((num2 >= 1f) ? Accent : fill));
				Widgets.DrawBoxSolid(new Rect(((Rect)(ref val)).x + 1f, ((Rect)(ref val)).y + 1f, Mathf.Max(1f, (((Rect)(ref val)).width - 2f) * num2), ((Rect)(ref val)).height - 2f), val2);
			}
			if (num > 0f)
			{
				TextIn(new Rect(((Rect)(ref val)).xMax + 4f, ((Rect)(ref r)).y, num - 4f, ((Rect)(ref r)).height), label, 0f, TextCol, (TextAnchor)3);
			}
		}

		public static int Segmented(Rect r, string[] labels, int selected, float gap)
		{
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			if (labels == null || labels.Length == 0)
			{
				return -1;
			}
			float num = (((Rect)(ref r)).width - gap * (float)(labels.Length - 1)) / (float)labels.Length;
			int result = -1;
			for (int i = 0; i < labels.Length; i++)
			{
				if (AE2TextButton(new Rect(((Rect)(ref r)).x + (float)i * (num + gap), ((Rect)(ref r)).y, num, ((Rect)(ref r)).height), labels[i], i == selected))
				{
					result = i;
				}
			}
			return result;
		}

		public static void SunkenBox(Rect r)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_0122: Unknown result type (might be due to invalid IL or missing references)
			//IL_0127: Unknown result type (might be due to invalid IL or missing references)
			//IL_015d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0162: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(r, PanelFace);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, ((Rect)(ref r)).width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).yMax - 1f, ((Rect)(ref r)).width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).xMax - 1f, ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x + 1f, ((Rect)(ref r)).y + 1f, 1f, ((Rect)(ref r)).height - 2f), EdgeHi);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x + 1f, ((Rect)(ref r)).yMax - 3f, ((Rect)(ref r)).width - 2f, 1f), EdgeHi);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x + 1f, ((Rect)(ref r)).yMax - 2f, ((Rect)(ref r)).width - 2f, 2f), LineSoft);
		}

		public static void WindowBg(Rect r)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(r, Panel);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x + 1f, ((Rect)(ref r)).y + 1f, ((Rect)(ref r)).width - 2f, 1f), EdgeHi);
			Outline(r);
		}

		public static void ColumnHead(Rect r)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(r, Panel);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, ((Rect)(ref r)).width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x + 1f, ((Rect)(ref r)).y + 1f, ((Rect)(ref r)).width - 2f, 1f), EdgeHi);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).yMax - 1f, ((Rect)(ref r)).width, 1f), LineSoft);
		}

		public static void FlatColumn(Rect r)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(r, Panel);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, ((Rect)(ref r)).width, 24f), Button);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x + 1f, ((Rect)(ref r)).y + 1f, ((Rect)(ref r)).width - 2f, 1f), EdgeHi);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y + 23f, ((Rect)(ref r)).width, 1f), EdgeLo);
			Outline(r);
		}

		public static void OutlineColor(Rect r, Color c)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, ((Rect)(ref r)).width, 1f), c);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).yMax - 1f, ((Rect)(ref r)).width, 1f), c);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), c);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).xMax - 1f, ((Rect)(ref r)).y, 1f, ((Rect)(ref r)).height), c);
		}

		public static void FlatRow(Rect r, bool hover, bool selected)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			Widgets.DrawBoxSolid(r, selected ? Slot : (hover ? ButtonHover : Panel));
			if (selected)
			{
				OutlineAccentBox(r);
			}
			else
			{
				Outline(r);
			}
		}

		public static void StateBar(Rect r, float frac, Color fill, string label)
		{
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0190: Unknown result type (might be due to invalid IL or missing references)
			//IL_0195: Unknown result type (might be due to invalid IL or missing references)
			float num = 0f;
			if (!string.IsNullOrEmpty(label))
			{
				num = Mathf.Ceil(Text.CalcSize(label).x) + 6f;
				if (num > ((Rect)(ref r)).width * 0.5f)
				{
					num = 0f;
				}
			}
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(((Rect)(ref r)).x, ((Rect)(ref r)).y, Mathf.Max(8f, ((Rect)(ref r)).width - num), ((Rect)(ref r)).height);
			Widgets.DrawBoxSolid(val, BarBg);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).yMax - 1f, ((Rect)(ref val)).width, 1f), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref val)).x, ((Rect)(ref val)).y, 1f, ((Rect)(ref val)).height), OutlineCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref val)).xMax - 1f, ((Rect)(ref val)).y, 1f, ((Rect)(ref val)).height), OutlineCol);
			float num2 = Mathf.Clamp01(frac);
			if (num2 > 0f && ((Rect)(ref val)).width > 2f && ((Rect)(ref val)).height > 2f)
			{
				Widgets.DrawBoxSolid(new Rect(((Rect)(ref val)).x + 1f, ((Rect)(ref val)).y + 1f, Mathf.Max(1f, (((Rect)(ref val)).width - 2f) * num2), ((Rect)(ref val)).height - 2f), fill);
			}
			if (num > 0f)
			{
				TextIn(new Rect(((Rect)(ref val)).xMax + 4f, ((Rect)(ref r)).y, num - 4f, ((Rect)(ref r)).height), label, 0f, TextCol, (TextAnchor)3);
			}
		}
	}
	internal sealed class AE2Palette
	{
		public Color Bg;

		public Color Panel;

		public Color PanelFace;

		public Color Button;

		public Color ButtonHover;

		public Color Slot;

		public Color SlotHover;

		public Color BarBg;

		public Color BtnHoverFace;

		public Color BtnHoverTop;

		public Color BtnHoverShadow;

		public Color BtnHoverEdge;

		public Color EdgeHi;

		public Color EdgeLo;

		public Color EdgeDeep;

		public Color OutlineCol;

		public Color LineSoft;

		public Color TextCol;

		public Color TextDimCol;

		public Color TextHi;

		public Color ButtonText;

		public Color ButtonTextDim;

		public Color Accent;

		public Color AccentBright;

		public Color AccentDim;

		public Color AccentText;

		public Color Warn;

		public Color Bad;

		public Color Ok;
	}
	internal static class AE2Theme
	{
		public static readonly AE2Palette Light = new AE2Palette
		{
			Bg = Color32.op_Implicit(new Color32((byte)203, (byte)204, (byte)212, byte.MaxValue)),
			Panel = Color32.op_Implicit(new Color32((byte)203, (byte)204, (byte)212, byte.MaxValue)),
			PanelFace = Color32.op_Implicit(new Color32((byte)203, (byte)204, (byte)212, byte.MaxValue)),
			Button = Color32.op_Implicit(new Color32((byte)154, (byte)159, (byte)180, byte.MaxValue)),
			ButtonHover = Color32.op_Implicit(new Color32((byte)173, (byte)176, (byte)196, byte.MaxValue)),
			Slot = Color32.op_Implicit(new Color32((byte)173, (byte)176, (byte)196, byte.MaxValue)),
			SlotHover = Color32.op_Implicit(new Color32((byte)203, (byte)204, (byte)212, byte.MaxValue)),
			BarBg = Color32.op_Implicit(new Color32((byte)135, (byte)143, (byte)165, byte.MaxValue)),
			BtnHoverFace = Color32.op_Implicit(new Color32((byte)156, (byte)211, byte.MaxValue, byte.MaxValue)),
			BtnHoverTop = Color32.op_Implicit(new Color32((byte)218, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BtnHoverShadow = Color32.op_Implicit(new Color32((byte)112, (byte)140, (byte)186, byte.MaxValue)),
			BtnHoverEdge = Color32.op_Implicit(new Color32((byte)156, (byte)211, byte.MaxValue, byte.MaxValue)),
			EdgeHi = Color32.op_Implicit(new Color32((byte)242, (byte)242, (byte)242, byte.MaxValue)),
			EdgeLo = Color32.op_Implicit(new Color32((byte)105, (byte)109, (byte)136, byte.MaxValue)),
			EdgeDeep = Color32.op_Implicit(new Color32((byte)105, (byte)109, (byte)136, byte.MaxValue)),
			OutlineCol = Color32.op_Implicit(new Color32((byte)65, (byte)63, (byte)84, byte.MaxValue)),
			LineSoft = Color32.op_Implicit(new Color32((byte)135, (byte)143, (byte)165, byte.MaxValue)),
			TextCol = Color32.op_Implicit(new Color32((byte)65, (byte)63, (byte)84, byte.MaxValue)),
			TextDimCol = Color32.op_Implicit(new Color32((byte)135, (byte)143, (byte)165, byte.MaxValue)),
			TextHi = Color32.op_Implicit(new Color32((byte)65, (byte)63, (byte)84, byte.MaxValue)),
			ButtonText = Color32.op_Implicit(new Color32((byte)65, (byte)63, (byte)84, byte.MaxValue)),
			ButtonTextDim = Color32.op_Implicit(new Color32((byte)89, (byte)94, (byte)109, byte.MaxValue)),
			Accent = Color32.op_Implicit(new Color32((byte)156, (byte)211, byte.MaxValue, byte.MaxValue)),
			AccentBright = Color32.op_Implicit(new Color32((byte)218, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			AccentDim = Color32.op_Implicit(new Color32((byte)156, (byte)211, byte.MaxValue, byte.MaxValue)),
			AccentText = Color32.op_Implicit(new Color32((byte)22, (byte)59, (byte)78, byte.MaxValue)),
			Warn = Color32.op_Implicit(new Color32((byte)232, (byte)180, (byte)88, byte.MaxValue)),
			Bad = Color32.op_Implicit(new Color32((byte)176, (byte)46, (byte)38, byte.MaxValue)),
			Ok = Color32.op_Implicit(new Color32((byte)40, (byte)90, (byte)58, byte.MaxValue))
		};

		public static bool IsDark => false;

		public static AE2Palette Active => Light;
	}
	public class Window_AE2CraftPanel : Window
	{
		private const int F_All = 0;

		private const int F_Running = 1;

		private const int F_Waiting = 2;

		private const int F_Blocked = 3;

		private const int F_Suspended = 4;

		private const int F_Done = 5;

		private CompBillAutomation comp;

		private int filter;

		private CraftPlan sel;

		private float scrollOrders;

		private float scrollCore;

		private string searchCore = "";

		private string countBuf;

		private CraftPlan countBufPlan;

		private int countBufShown = -1;

		private const float HeaderH = 24f;

		private const float InfoH = 18f;

		private const float FilterH = 22f;

		private const float RowH = 40f;

		private const float CoreRowH = 30f;

		public override Vector2 InitialSize => new Vector2(960f, 540f);

		public Window_AE2CraftPanel(CompBillAutomation comp)
			: base((IWindowDrawing)null)
		{
			this.comp = comp;
			base.doCloseX = false;
			base.closeOnClickedOutside = false;
			base.absorbInputAroundWindow = false;
			base.onlyOneOfTypeAllowed = true;
			base.draggable = true;
			base.resizeable = false;
			base.doWindowBackground = false;
			RefreshTargetCountsOnce(comp);
		}

		private static void RefreshTargetCountsOnce(CompBillAutomation comp)
		{
			if (comp == null || Find.CurrentMap == null)
			{
				return;
			}
			IList<CraftPlan> plansForReading = comp.PlansForReading;
			if (plansForReading == null)
			{
				return;
			}
			for (int i = 0; i < plansForReading.Count; i++)
			{
				CraftPlan craftPlan = plansForReading[i];
				if (craftPlan != null)
				{
					try
					{
						comp.RecountNow(craftPlan);
					}
					catch (Exception ex)
					{
						Log.ErrorOnce("[DigitalStorage] 面板打开时重数失败：" + ex, 771234);
					}
				}
			}
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0105: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0114: Unknown result type (might be due to invalid IL or missing references)
			//IL_0125: Unknown result type (might be due to invalid IL or missing references)
			//IL_012a: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_0168: Unknown result type (might be due to invalid IL or missing references)
			//IL_0172: Unknown result type (might be due to invalid IL or missing references)
			//IL_0177: Unknown result type (might be due to invalid IL or missing references)
			//IL_018b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0231: Unknown result type (might be due to invalid IL or missing references)
			//IL_027a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0282: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0308: Unknown result type (might be due to invalid IL or missing references)
			//IL_0370: Unknown result type (might be due to invalid IL or missing references)
			//IL_0311: Unknown result type (might be due to invalid IL or missing references)
			//IL_0313: Unknown result type (might be due to invalid IL or missing references)
			//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0329: Unknown result type (might be due to invalid IL or missing references)
			//IL_0421: Unknown result type (might be due to invalid IL or missing references)
			//IL_0423: Unknown result type (might be due to invalid IL or missing references)
			//IL_044b: Unknown result type (might be due to invalid IL or missing references)
			//IL_046a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0488: Unknown result type (might be due to invalid IL or missing references)
			//IL_04a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_04b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_04d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_04e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_04fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0500: Unknown result type (might be due to invalid IL or missing references)
			//IL_0539: Unknown result type (might be due to invalid IL or missing references)
			//IL_0551: Unknown result type (might be due to invalid IL or missing references)
			//IL_067f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0688: Unknown result type (might be due to invalid IL or missing references)
			//IL_0692: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				AE2Draw.HandlePauseHotkey();
				if (comp == null || ((ThingComp)comp).parent == null)
				{
					comp = FindAnyProxy();
					if (comp == null)
					{
						bool closeClicked;
						Rect val = AE2Draw.WindowFrame(GenUI.ContractedBy(inRect, 2f), ((object)Translator.Translate("DS_AE2_Title")/*cast due to constrained. prefix*/).ToString(), out closeClicked);
						if (closeClicked)
						{
							((Window)this).Close(true);
						}
						else
						{
							AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 6f, ((Rect)(ref val)).y + 6f, ((Rect)(ref val)).width - 12f, 20f), ((object)Translator.Translate("DS_CA_NoProxyHint")/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextCol, (TextAnchor)3);
						}
						return;
					}
				}
				Map map = ((Thing)((ThingComp)comp).parent).Map;
				if (((Rect)(ref inRect)).width < 620f || ((Rect)(ref inRect)).height < 360f)
				{
					bool closeClicked2;
					Rect val2 = AE2Draw.WindowFrame(GenUI.ContractedBy(inRect, 2f), ((object)Translator.Translate("DS_AE2_Title")/*cast due to constrained. prefix*/).ToString(), out closeClicked2);
					if (closeClicked2)
					{
						((Window)this).Close(true);
					}
					else
					{
						AE2Draw.TextIn(new Rect(((Rect)(ref val2)).x + 6f, ((Rect)(ref val2)).y + 6f, ((Rect)(ref val2)).width - 12f, 20f), ((object)Translator.Translate("DS_AE2_TooSmall")/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.Bad, (TextAnchor)3);
					}
					return;
				}
				if (map == null)
				{
					((Window)this).Close(true);
					return;
				}
				string text = ((Entity)((ThingComp)comp).parent).LabelShort.ToString() + "\u3000" + ((object)((Thing)((ThingComp)comp).parent).Position/*cast due to constrained. prefix*/).ToString();
				Rect r = GenUI.ContractedBy(inRect, 2f);
				AE2Draw.WindowBg(r);
				float num = 28f;
				Rect rail = default(Rect);
				((Rect)(ref rail))..ctor(((Rect)(ref r)).x + 4f, ((Rect)(ref r)).y + 4f, num, ((Rect)(ref r)).height - 8f);
				DrawToolRail(rail);
				float num2 = ((Rect)(ref rail)).xMax + 6f;
				float num3 = ((Rect)(ref r)).xMax - num2 - 4f;
				float num4 = ((Rect)(ref r)).y + 4f;
				Rect r2 = default(Rect);
				((Rect)(ref r2))..ctor(num2, num4, num3, 28f);
				AE2Draw.ColumnHead(r2);
				float num5 = Mathf.Ceil(Text.CalcSize(text).x) + 4f;
				AE2Draw.TextIn(new Rect(((Rect)(ref r2)).x + 6f, ((Rect)(ref r2)).y, num5 + 14f, ((Rect)(ref r2)).height), text, 1f, AE2Draw.TextHi, (TextAnchor)3);
				Rect val3 = default(Rect);
				((Rect)(ref val3))..ctor(((Rect)(ref r2)).x + 4f, ((Rect)(ref r2)).y + 2f, num5 + 12f, ((Rect)(ref r2)).height - 4f);
				if (Mouse.IsOver(val3))
				{
					Widgets.DrawBoxSolid(val3, AE2Draw.Button);
					if (Event.current != null && (int)Event.current.type == 0 && Event.current.button == 0)
					{
						Event.current.Use();
						OpenProxyMenu();
					}
				}
				if (AE2Draw.AE2TextButton(new Rect(((Rect)(ref r2)).xMax - 20f, ((Rect)(ref r2)).y + 3f, 18f, 18f), "×"))
				{
					((Window)this).Close(true);
					return;
				}
				Rect r3 = default(Rect);
				((Rect)(ref r3))..ctor(((Rect)(ref r2)).xMax - 20f - 6f - 260f, ((Rect)(ref r2)).y + 5f, 260f, 16f);
				searchCore = AE2Draw.TextField(r3, searchCore);
				num4 = ((Rect)(ref r2)).yMax + 2f;
				IList<CraftPlan> list = comp.PlansForReading;
				if (list == null)
				{
					list = new List<CraftPlan>();
				}
				num4 = DrawToolRow(num2, num4, num3, list);
				Rect r4 = default(Rect);
				((Rect)(ref r4))..ctor(num2, num4, num3, 18f);
				AE2Draw.Sunken(r4, AE2Draw.BarBg);
				string text2 = ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_Watts", NamedArgument.op_Implicit(comp.CurrentWatts.ToString("#####0")), NamedArgument.op_Implicit(comp.Props.basePowerWatts.ToString("#####0")), NamedArgument.op_Implicit(comp.BenchWatts().ToString("#####0")), NamedArgument.op_Implicit(comp.OverclockPowerMult.ToString("0")))/*cast due to constrained. prefix*/).ToString();
				string text3 = ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_Stats", NamedArgument.op_Implicit(comp.CompletedCount), NamedArgument.op_Implicit(comp.DroppedCount), NamedArgument.op_Implicit(comp.PlansForReading.Count))/*cast due to constrained. prefix*/).ToString();
				AE2Draw.TextIn(new Rect(((Rect)(ref r4)).x + 5f, ((Rect)(ref r4)).y, ((Rect)(ref r4)).width - 10f, ((Rect)(ref r4)).height), text2 + "\u3000·\u3000" + text3, 1f, AE2Draw.Hi, (TextAnchor)3);
				num4 = ((Rect)(ref r4)).yMax + 3f;
				IList<CraftPlan> list2 = comp.PlansForReading;
				if (list2 == null)
				{
					list2 = new List<CraftPlan>();
				}
				if (sel != null && !list2.Contains(sel))
				{
					sel = null;
				}
				if (sel == null && list2.Count > 0)
				{
					sel = list2[0];
				}
				float num6 = ((Rect)(ref r)).yMax - num4 - 4f;
				if (num6 < 60f)
				{
					num6 = 60f;
				}
				float num7 = ((Rect)(ref r)).xMax - 4f - num2;
				float num8 = Mathf.Clamp(num7 * 0.28f, 180f, 300f);
				float num9 = Mathf.Clamp(num7 * 0.35f, 220f, 380f);
				Rect area = default(Rect);
				((Rect)(ref area))..ctor(num2, num4, num8, num6);
				Rect area2 = default(Rect);
				((Rect)(ref area2))..ctor(((Rect)(ref area)).xMax + 5f, num4, num9, num6);
				Rect area3 = default(Rect);
				((Rect)(ref area3))..ctor(((Rect)(ref area2)).xMax + 5f, num4, ((Rect)(ref r)).xMax - 4f - ((Rect)(ref area2)).xMax - 5f, num6);
				DrawCore(area, map);
				DrawOrders(area2, list2);
				DrawDetail(area3, map);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] AE2 界面绘制异常（只记一次，界面不会卡死）：" + ex, 771001);
			}
		}

		private void DrawToolRail(Rect rail)
		{
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0108: Unknown result type (might be due to invalid IL or missing references)
			//IL_010d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0124: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
			float num = Mathf.Min(24f, ((Rect)(ref rail)).width - 4f);
			float num2 = ((Rect)(ref rail)).x + (((Rect)(ref rail)).width - num) / 2f;
			string[] array = new string[7] { "^", "II", ">", "II", "<>", "=", "[]" };
			string[] array2 = new string[7]
			{
				((object)Translator.Translate("DS_AE2_CoreItems")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_SuspendAll")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_ResumeAll")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_BA_Suspend")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_SwitchProxy")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_AddRecipe")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_BA_ShowRange")/*cast due to constrained. prefix*/).ToString()
			};
			Rect val = default(Rect);
			for (int i = 0; i < array.Length; i++)
			{
				((Rect)(ref val))..ctor(num2, ((Rect)(ref rail)).y + (float)i * (num + 3f), num, num);
				if (((Rect)(ref val)).yMax > ((Rect)(ref rail)).yMax)
				{
					break;
				}
				bool active = (i == 3 && sel != null && sel.suspended) || (i == 6 && comp != null && comp.ShowRange);
				if (AE2Draw.AE2TextButton(val, array[i], active))
				{
					switch (i)
					{
					case 1:
						SetAllSuspended(on: true);
						break;
					case 2:
						SetAllSuspended(on: false);
						break;
					case 3:
						if (sel != null)
						{
							sel.suspended = !sel.suspended;
						}
						break;
					case 4:
						OpenProxyMenu();
						break;
					case 5:
						Find.WindowStack.Add((Window)(object)new Dialog_AE2AddRecipe(comp));
						break;
					case 6:
						if (comp != null)
						{
							comp.ToggleShowRange();
						}
						break;
					case 0:
					{
						Building_StorageCore building_StorageCore = FirstCore(((Thing)((ThingComp)comp).parent).Map);
						if (building_StorageCore != null)
						{
							Find.Selector.ClearSelection();
							Find.Selector.Select((object)building_StorageCore, false, false);
							((Window)this).Close(true);
						}
						break;
					}
					}
				}
				string text = array2[i];
				if (!string.IsNullOrEmpty(text) && Mouse.IsOver(val))
				{
					TooltipHandler.TipRegion(val, TipSignal.op_Implicit(text));
				}
			}
		}

		private static void TreeGlyph(Rect r)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Unknown result type (might be due to invalid IL or missing references)
			//IL_0106: Unknown result type (might be due to invalid IL or missing references)
			//IL_0125: Unknown result type (might be due to invalid IL or missing references)
			//IL_012a: Unknown result type (might be due to invalid IL or missing references)
			//IL_013d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0142: Unknown result type (might be due to invalid IL or missing references)
			float num = ((Rect)(ref r)).width / 10f;
			Color textCol = AE2Draw.TextCol;
			float num2 = Mathf.Max(3f, num * 2.4f);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x + 4f * num, ((Rect)(ref r)).y, num2, num2), textCol);
			float num3 = ((Rect)(ref r)).y + num2;
			float num4 = ((Rect)(ref r)).yMax - num2;
			for (int i = 0; i < 3; i++)
			{
				float num5 = ((float)i + 0.5f) / 3f;
				float num6 = num3 + num5 * Mathf.Max(1f, num4 - num3 - 1f);
				float num7 = ((Rect)(ref r)).x + (4.6f - num5 * 4.2f) * num;
				float num8 = ((Rect)(ref r)).x + (5.4f + num5 * 4.2f) * num;
				Widgets.DrawBoxSolid(new Rect(num7, num6, Mathf.Max(1f, num * 0.9f), 1f), textCol);
				Widgets.DrawBoxSolid(new Rect(num8, num6, Mathf.Max(1f, num * 0.9f), 1f), textCol);
			}
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x, num4, num2, num2), textCol);
			Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).xMax - num2, num4, num2, num2), textCol);
		}

		private static void ProxyArrow(Rect r)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			Color textHi = AE2Draw.TextHi;
			float num = Mathf.Max(3f, ((Rect)(ref r)).height * 0.62f);
			float num2 = ((Rect)(ref r)).y + (((Rect)(ref r)).height - num) * 0.5f;
			int num3 = Mathf.Max(2, Mathf.RoundToInt(num));
			for (int i = 0; i < num3; i++)
			{
				float num4 = (float)i / (float)num3;
				float num5 = Mathf.Max(1f, ((Rect)(ref r)).width * (1f - num4));
				Widgets.DrawBoxSolid(new Rect(((Rect)(ref r)).x + (((Rect)(ref r)).width - num5) * 0.5f, num2 + (float)i, num5, 1f), textHi);
			}
		}

		private float DrawToolRow(float x0, float y, float w, IList<CraftPlan> plans)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0132: Unknown result type (might be due to invalid IL or missing references)
			//IL_0137: Unknown result type (might be due to invalid IL or missing references)
			//IL_0185: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0203: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0232: Unknown result type (might be due to invalid IL or missing references)
			//IL_0240: Unknown result type (might be due to invalid IL or missing references)
			//IL_024b: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0254: Unknown result type (might be due to invalid IL or missing references)
			//IL_025a: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0300: Unknown result type (might be due to invalid IL or missing references)
			string[] array = new string[6]
			{
				((object)Translator.Translate("DS_CA_Orders")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_Working")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_Waiting")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_AE2_Blocked")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_BA_Suspended")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_BA_PlanFinished")/*cast due to constrained. prefix*/).ToString()
			};
			string[] array2 = new string[3] { "+", "", "[]" };
			string[] array3 = new string[3] { "添加", "树", "删树" };
			string[] array4 = new string[3]
			{
				((object)Translator.Translate("DS_CA_AddRecipe")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_AE2_Tree_Btn")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_Delete")/*cast due to constrained. prefix*/).ToString()
			};
			float num = x0 + w - 6f - 168f - 12f;
			Rect val = default(Rect);
			for (int i = 0; i < 3; i++)
			{
				((Rect)(ref val))..ctor(num + (float)i * 62f, y, 56f, 24f);
				if (AE2Draw.AE2TextButton(val, ""))
				{
					DoAction(i);
				}
				if (i == 1)
				{
					TreeGlyph(new Rect(((Rect)(ref val)).x + 4f, ((Rect)(ref val)).y + 4f, 15f, 15f));
				}
				else
				{
					AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 3f, ((Rect)(ref val)).y + 1f, 17f, 22f), array2[i], 0f, AE2Draw.TextCol, (TextAnchor)3);
				}
				AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 21f, ((Rect)(ref val)).y + 1f, 32f, 22f), array3[i], 0f, AE2Draw.TextCol, (TextAnchor)3);
				if (Mouse.IsOver(val))
				{
					TooltipHandler.TipRegion(val, TipSignal.op_Implicit(array4[i]));
				}
			}
			float num2 = Mathf.Clamp((num - 6f - x0 - 30f) / 6f, 40f, 74f);
			float num3 = x0;
			Rect val2 = default(Rect);
			for (int j = 0; j < 6; j++)
			{
				if (num3 + num2 > num - 6f)
				{
					break;
				}
				((Rect)(ref val2))..ctor(num3, y, num2, 24f);
				if (AE2Draw.AE2TextButton(val2, array[j], filter == j))
				{
					filter = j;
				}
				if (Mouse.IsOver(val2))
				{
					TooltipHandler.TipRegion(val2, TipSignal.op_Implicit(array[j] + "：" + FilterHint(j)));
				}
				num3 += num2 + 6f;
			}
			return y + 24f + 4f;
		}

		private void DoAction(int i)
		{
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			switch (i)
			{
			case 0:
				Find.WindowStack.Add((Window)(object)new Dialog_AE2AddRecipe(comp));
				break;
			case 1:
				if (sel != null && sel.recipe != null)
				{
					CraftJob craftJob = comp.JobForRecipe(sel.recipe);
					if (craftJob != null)
					{
						Find.WindowStack.Add((Window)(object)new Window_AE2CraftTreeGraph(comp, craftJob));
						break;
					}
					int wanted = ((sel.mode == 2) ? Mathf.Max(1, sel.targetCount) : ((sel.mode != 1) ? 1 : Mathf.Max(1, sel.remaining)));
					Find.WindowStack.Add((Window)(object)new Window_AE2CraftTreeGraph(comp, sel.recipe, sel.recipe.ProducedThingDef, wanted));
				}
				break;
			case 2:
				if (sel == null)
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("DS_CA_DeleteTreeNoSel")), MessageTypeDefOf.RejectInput, false);
					break;
				}
				comp.RemoveSelectedTree(sel);
				sel = null;
				break;
			}
		}

		private static int Classify(CraftPlan p)
		{
			if (p == null)
			{
				return 2;
			}
			if (p.Done || (p.mode == 1 && p.remaining <= 0))
			{
				return 5;
			}
			if (p.suspended)
			{
				return 4;
			}
			bool flag = false;
			bool flag2 = false;
			for (int i = 0; i < p.lines.Count; i++)
			{
				CraftLine craftLine = p.lines[i];
				if (craftLine != null)
				{
					if (craftLine.HasWork)
					{
						flag = true;
					}
					if (!string.IsNullOrEmpty(craftLine.BlockKey))
					{
						flag2 = true;
					}
				}
			}
			if (flag)
			{
				return 1;
			}
			if (flag2)
			{
				return 3;
			}
			return 2;
		}

		private void DrawCore(Rect area, Map map)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0201: Unknown result type (might be due to invalid IL or missing references)
			//IL_023d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0242: Unknown result type (might be due to invalid IL or missing references)
			//IL_024e: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_030d: Unknown result type (might be due to invalid IL or missing references)
			//IL_033b: Unknown result type (might be due to invalid IL or missing references)
			//IL_011f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0124: Unknown result type (might be due to invalid IL or missing references)
			//IL_034d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0360: Unknown result type (might be due to invalid IL or missing references)
			//IL_036a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0398: Unknown result type (might be due to invalid IL or missing references)
			//IL_039d: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_03bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_047e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0488: Unknown result type (might be due to invalid IL or missing references)
			//IL_048d: Unknown result type (might be due to invalid IL or missing references)
			//IL_04a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_040b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0410: Unknown result type (might be due to invalid IL or missing references)
			//IL_0506: Unknown result type (might be due to invalid IL or missing references)
			//IL_04d6: Unknown result type (might be due to invalid IL or missing references)
			AE2Draw.FlatColumn(area);
			AE2Draw.TextIn(new Rect(((Rect)(ref area)).x + 5f, ((Rect)(ref area)).y, ((Rect)(ref area)).width - 10f, 24f), ((object)Translator.Translate("DS_AE2_CoreItems")/*cast due to constrained. prefix*/).ToString() + "\u3000·\u3000" + CoreSummary(map), 0f, AE2Draw.TextCol, (TextAnchor)3);
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(((Rect)(ref area)).x + 5f, ((Rect)(ref area)).y + 26f, ((Rect)(ref area)).width - 10f, ((Rect)(ref area)).height - 30f);
			Rect area2 = default(Rect);
			((Rect)(ref area2))..ctor(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width, ((Rect)(ref val)).yMax - ((Rect)(ref val)).y);
			List<KeyValuePair<ThingDef, int>> list = CoreItems(map);
			if (!string.IsNullOrEmpty(searchCore))
			{
				List<KeyValuePair<ThingDef, int>> list2 = new List<KeyValuePair<ThingDef, int>>();
				for (int i = 0; i < list.Count; i++)
				{
					if (((object)((Def)list[i].Key).LabelCap/*cast due to constrained. prefix*/).ToString().IndexOf(searchCore, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						list2.Add(list[i]);
					}
				}
				list = list2;
			}
			int num = Mathf.Max(1, Mathf.FloorToInt(((Rect)(ref area2)).height / 31f));
			int num2 = Mathf.Max(0, list.Count - num);
			int num3 = Mathf.RoundToInt(scrollCore * (float)num2);
			int num4 = Mathf.Min(num, Mathf.Max(0, list.Count - num3));
			Rect val2 = default(Rect);
			for (int j = 0; j < num4; j++)
			{
				int index = num3 + j;
				((Rect)(ref val2))..ctor(((Rect)(ref area2)).x, ((Rect)(ref area2)).y + (float)j * 31f, ((Rect)(ref area2)).width - 14f, 30f);
				bool flag = Mouse.IsOver(val2);
				AE2Draw.AE2Button(val2, active: false, flag);
				Rect val3 = new Rect(((Rect)(ref val2)).x + 3f, ((Rect)(ref val2)).y + (((Rect)(ref val2)).height - 18f) * 0.5f, 18f, 18f);
				AE2Draw.SlotBox(val3, hover: false);
				Widgets.DefIcon(GenUI.ContractedBy(val3, 1f), (Def)(object)list[index].Key, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
				AE2Draw.TextIn(new Rect(((Rect)(ref val2)).x + 25f, ((Rect)(ref val2)).y, ((Rect)(ref val2)).width - 64f, ((Rect)(ref val2)).height), ((object)((Def)list[index].Key).LabelCap/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextCol, (TextAnchor)3);
				AE2Draw.TextIn(new Rect(((Rect)(ref val2)).xMax - 46f, ((Rect)(ref val2)).y, 42f, ((Rect)(ref val2)).height), "×" + list[index].Value, 1f, AE2Draw.TextDimCol, (TextAnchor)5);
				if (!flag)
				{
					continue;
				}
				TooltipHandler.TipRegion(val2, TipSignal.op_Implicit(TaggedString.op_Implicit(((Def)list[index].Key).LabelCap + " ×") + list[index].Value + "\n" + ((object)Translator.Translate("DS_AE2_TakeHint")/*cast due to constrained. prefix*/).ToString()));
				if (Widgets.ButtonInvisible(val2, true))
				{
					ThingDef __def = list[index].Key;
					int value = list[index].Value;
					Find.WindowStack.Add((Window)(object)new Dialog_WithdrawAmount(((object)((Def)__def).LabelCap/*cast due to constrained. prefix*/).ToString(), value, 0, delegate(int n)
					{
						TakeFromCore(map, __def, n);
					}));
					break;
				}
			}
			if (list.Count == 0)
			{
				AE2Draw.TextIn(new Rect(((Rect)(ref area2)).x + 2f, ((Rect)(ref area2)).y + 2f, ((Rect)(ref area2)).width - 4f, 16f), ((object)Translator.Translate("DS_AE2_CoreEmpty")/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextDimCol, (TextAnchor)3);
			}
			if (list.Count > num)
			{
				AE2Draw.DragBar(new Rect(((Rect)(ref area2)).xMax - 12f, ((Rect)(ref area2)).y, 12f, ((Rect)(ref area2)).height), ref scrollCore, (list.Count <= 0) ? 1f : Mathf.Clamp01((float)num / (float)list.Count));
			}
			AE2Draw.WheelScroll(area2, ref scrollCore, num2);
		}

		private static List<KeyValuePair<ThingDef, int>> CoreItems(Map map)
		{
			Dictionary<ThingDef, int> dictionary = new Dictionary<ThingDef, int>();
			List<Building_StorageCore> list = CoreFinder.AllUsableCores(map);
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					Building_StorageCore building_StorageCore = list[i];
					if (building_StorageCore == null)
					{
						continue;
					}
					ThingOwner directlyHeldThings = building_StorageCore.GetDirectlyHeldThings();
					if (directlyHeldThings == null)
					{
						continue;
					}
					for (int j = 0; j < directlyHeldThings.Count; j++)
					{
						Thing val = directlyHeldThings[j];
						if (val != null && val.def != null)
						{
							dictionary.TryGetValue(val.def, out var value);
							dictionary[val.def] = value + val.stackCount;
						}
					}
				}
			}
			List<KeyValuePair<ThingDef, int>> list2 = new List<KeyValuePair<ThingDef, int>>(dictionary);
			list2.Sort((KeyValuePair<ThingDef, int> a, KeyValuePair<ThingDef, int> b) => (a.Value != b.Value) ? (b.Value - a.Value) : string.Compare(((Def)a.Key).label, ((Def)b.Key).label, StringComparison.Ordinal));
			return list2;
		}

		private void DrawOrders(Rect area, IList<CraftPlan> plans)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_013c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0169: Unknown result type (might be due to invalid IL or missing references)
			//IL_016e: Unknown result type (might be due to invalid IL or missing references)
			//IL_014e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0153: Unknown result type (might be due to invalid IL or missing references)
			//IL_0182: Unknown result type (might be due to invalid IL or missing references)
			//IL_06ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_069b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0244: Unknown result type (might be due to invalid IL or missing references)
			//IL_0248: Unknown result type (might be due to invalid IL or missing references)
			//IL_0286: Unknown result type (might be due to invalid IL or missing references)
			//IL_028b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0297: Unknown result type (might be due to invalid IL or missing references)
			//IL_02da: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_02df: Unknown result type (might be due to invalid IL or missing references)
			//IL_0333: Unknown result type (might be due to invalid IL or missing references)
			//IL_0342: Unknown result type (might be due to invalid IL or missing references)
			//IL_038b: Unknown result type (might be due to invalid IL or missing references)
			//IL_039c: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_03dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_03f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_03f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0404: Unknown result type (might be due to invalid IL or missing references)
			//IL_040d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0416: Unknown result type (might be due to invalid IL or missing references)
			//IL_0420: Unknown result type (might be due to invalid IL or missing references)
			//IL_042c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0436: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_064a: Unknown result type (might be due to invalid IL or missing references)
			//IL_04e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_055c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0561: Unknown result type (might be due to invalid IL or missing references)
			//IL_0541: Unknown result type (might be due to invalid IL or missing references)
			//IL_0546: Unknown result type (might be due to invalid IL or missing references)
			//IL_0589: Unknown result type (might be due to invalid IL or missing references)
			//IL_0593: Expected O, but got Unknown
			//IL_05b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_05bd: Expected O, but got Unknown
			//IL_05c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_05c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_05f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_05fb: Expected O, but got Unknown
			//IL_0602: Unknown result type (might be due to invalid IL or missing references)
			//IL_0607: Unknown result type (might be due to invalid IL or missing references)
			//IL_062f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0639: Expected O, but got Unknown
			//IL_0640: Unknown result type (might be due to invalid IL or missing references)
			//IL_064a: Expected O, but got Unknown
			AE2Draw.FlatColumn(area);
			AE2Draw.TextIn(new Rect(((Rect)(ref area)).x + 5f, ((Rect)(ref area)).y, ((Rect)(ref area)).width - 10f, 24f), ((object)Translator.Translate("DS_CA_Orders")/*cast due to constrained. prefix*/).ToString(), 0f, AE2Draw.TextCol, (TextAnchor)3);
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(((Rect)(ref area)).x + 5f, ((Rect)(ref area)).y + 26f, ((Rect)(ref area)).width - 10f, ((Rect)(ref area)).height - 30f);
			List<CraftPlan> list = new List<CraftPlan>();
			for (int i = 0; i < plans.Count; i++)
			{
				if (plans[i] != null && (filter == 0 || Classify(plans[i]) == filter))
				{
					list.Add(plans[i]);
				}
			}
			Rect area2 = default(Rect);
			((Rect)(ref area2))..ctor(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width, ((Rect)(ref val)).height);
			if (list.Count == 0)
			{
				AE2Draw.TextIn(new Rect(((Rect)(ref area2)).x + 3f, ((Rect)(ref area2)).y + 3f, ((Rect)(ref area2)).width - 6f, 16f), (plans.Count == 0) ? ((object)Translator.Translate("DS_CA_PickOrder")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("DS_CA_NoOrder")/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextDimCol, (TextAnchor)3);
				return;
			}
			int num = Mathf.Max(1, Mathf.FloorToInt(((Rect)(ref area2)).height / 41f));
			int num2 = Mathf.Max(0, list.Count - num);
			int num3 = Mathf.RoundToInt(scrollOrders * (float)num2);
			int num4 = Mathf.Min(num, Mathf.Max(0, list.Count - num3));
			Rect val2 = default(Rect);
			for (int j = 0; j < num4; j++)
			{
				CraftPlan craftPlan = list[num3 + j];
				if (craftPlan == null || craftPlan.recipe == null)
				{
					continue;
				}
				((Rect)(ref val2))..ctor(((Rect)(ref area2)).x, ((Rect)(ref area2)).y + (float)j * 41f, ((Rect)(ref area2)).width - 14f, 40f);
				bool flag = craftPlan == sel;
				AE2Draw.AE2Button(val2, flag, Mouse.IsOver(val2));
				Rect val3 = new Rect(((Rect)(ref val2)).x + 3f, ((Rect)(ref val2)).y + (((Rect)(ref val2)).height - 18f) * 0.5f, 18f, 18f);
				AE2Draw.SlotBox(val3, hover: false);
				Widgets.DefIcon(GenUI.ContractedBy(val3, 1f), (Def)(object)craftPlan.recipe.ProducedThingDef, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
				Color c = (flag ? AE2Draw.TextHi : AE2Draw.TextCol);
				CraftPlan craftPlan2 = plans[j];
				bool flag2 = craftPlan2 != null && craftPlan2.recipe != null && comp.IsSubordinateStep(craftPlan2.recipe);
				if (flag2)
				{
					AE2Draw.TextIn(new Rect(((Rect)(ref val2)).x + 23f, ((Rect)(ref val2)).y, 16f, ((Rect)(ref val2)).height), "└", 1f, AE2Draw.TextDimCol, (TextAnchor)3);
				}
				AE2Draw.TextIn(new Rect(((Rect)(ref val2)).x + (flag2 ? 41f : 25f), ((Rect)(ref val2)).y, ((Rect)(ref val2)).width - (flag2 ? 100f : 84f), ((Rect)(ref val2)).height), ((object)((Def)craftPlan.recipe.ProducedThingDef).LabelCap/*cast due to constrained. prefix*/).ToString(), 1f, c, (TextAnchor)3);
				AE2Draw.TextIn(new Rect(((Rect)(ref val2)).xMax - 108f, ((Rect)(ref val2)).y, 46f, ((Rect)(ref val2)).height), CountLabel(craftPlan), 1f, flag ? AE2Draw.TextHi : AE2Draw.TextDimCol, (TextAnchor)5);
				if (Mouse.IsOver(val2))
				{
					TooltipHandler.TipRegion(val2, TipSignal.op_Implicit(TaggedString.op_Implicit(((Def)craftPlan.recipe).LabelCap + "\n" + StatusText(craftPlan))));
				}
				CraftLine craftLine = ((craftPlan.lines.Count > 0) ? craftPlan.lines[0] : null);
				float frac = ((craftLine != null && craftLine.HasWork) ? craftLine.Progress01 : (craftPlan.Done ? 1f : 0f));
				AE2Draw.Bar(new Rect(((Rect)(ref val2)).xMax - 58f, ((Rect)(ref val2)).y + (((Rect)(ref val2)).height - 5f) * 0.5f, 46f, 5f), frac, flag);
				if (Mouse.IsOver(val2) && Event.current != null && (int)Event.current.type == 0 && Event.current.button == 1)
				{
					Event.current.Use();
					List<FloatMenuOption> list2 = new List<FloatMenuOption>();
					CraftPlan cap = craftPlan;
					list2.Add(new FloatMenuOption(cap.suspended ? ((object)Translator.Translate("DS_BA_Resume")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("DS_BA_Suspend")/*cast due to constrained. prefix*/).ToString(), (Action)delegate
					{
						cap.suspended = !cap.suspended;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					list2.Add(new FloatMenuOption("+10", (Action)delegate
					{
						cap.AddCount(10);
						comp.NotifyRootCountChanged(cap);
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					list2.Add(new FloatMenuOption(((object)Translator.Translate("DS_CA_SetForever")/*cast due to constrained. prefix*/).ToString(), (Action)delegate
					{
						cap.SetForever();
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					list2.Add(new FloatMenuOption(((object)Translator.Translate("DS_CA_Delete")/*cast due to constrained. prefix*/).ToString(), (Action)delegate
					{
						comp.RemovePlan(cap);
						sel = null;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					Find.WindowStack.Add((Window)new FloatMenu(list2));
				}
				if (Widgets.ButtonInvisible(val2, true))
				{
					sel = craftPlan;
					countBuf = null;
				}
			}
			if (list.Count > num)
			{
				AE2Draw.DragBar(new Rect(((Rect)(ref area2)).xMax - 12f, ((Rect)(ref area2)).y, 12f, ((Rect)(ref area2)).height), ref scrollOrders, (list.Count <= 0) ? 1f : Mathf.Clamp01((float)num / (float)list.Count));
			}
			AE2Draw.WheelScroll(area2, ref scrollOrders, num2);
		}

		private static string CountLabel(CraftPlan p)
		{
			if (p == null)
			{
				return "";
			}
			if (p.mode == 0)
			{
				return "∞";
			}
			if (p.mode == 1)
			{
				return "×" + Mathf.Max(0, p.remaining);
			}
			return p.countedCount + "/" + p.targetCount;
		}

		private static string StatusText(CraftPlan p)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_013b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_0115: Unknown result type (might be due to invalid IL or missing references)
			if (p == null)
			{
				return "";
			}
			if (p.Done)
			{
				return ((object)Translator.Translate("DS_BA_PlanFinished")/*cast due to constrained. prefix*/).ToString();
			}
			if (p.suspended)
			{
				return ((object)Translator.Translate("DS_BA_Suspended")/*cast due to constrained. prefix*/).ToString();
			}
			if (p.mode == 2 && !p.wantsWork)
			{
				return ((object)TranslatorFormattedStringExtensions.Translate("DS_CA_TargetPaused", NamedArgument.op_Implicit(p.countedCount), NamedArgument.op_Implicit(p.targetCount))/*cast due to constrained. prefix*/).ToString();
			}
			if (p.lines.Count == 0 && p.mode == 2 && p.countedCount < p.targetCount)
			{
				return ((object)Translator.Translate("DS_BA_NoBench")/*cast due to constrained. prefix*/).ToString();
			}
			for (int i = 0; i < p.lines.Count; i++)
			{
				CraftLine craftLine = p.lines[i];
				if (craftLine != null)
				{
					if (craftLine.HasWork)
					{
						return ((object)Translator.Translate("DS_CA_Working")/*cast due to constrained. prefix*/).ToString();
					}
					if (!string.IsNullOrEmpty(craftLine.BlockKey))
					{
						return ((object)Translator.Translate(craftLine.BlockKey)/*cast due to constrained. prefix*/).ToString();
					}
				}
			}
			return ((object)Translator.Translate("DS_CA_Waiting")/*cast due to constrained. prefix*/).ToString();
		}

		private void DrawDetail(Rect area, Map map)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00de: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_0101: Unknown result type (might be due to invalid IL or missing references)
			//IL_014d: Unknown result type (might be due to invalid IL or missing references)
			//IL_017a: Unknown result type (might be due to invalid IL or missing references)
			//IL_017f: Unknown result type (might be due to invalid IL or missing references)
			//IL_018b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0204: Unknown result type (might be due to invalid IL or missing references)
			//IL_020f: Unknown result type (might be due to invalid IL or missing references)
			//IL_021e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0249: Unknown result type (might be due to invalid IL or missing references)
			//IL_0253: Unknown result type (might be due to invalid IL or missing references)
			//IL_0258: Unknown result type (might be due to invalid IL or missing references)
			//IL_026c: Unknown result type (might be due to invalid IL or missing references)
			//IL_029d: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_0300: Unknown result type (might be due to invalid IL or missing references)
			//IL_0331: Unknown result type (might be due to invalid IL or missing references)
			//IL_0351: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_03af: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_03c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_03f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_041f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0499: Unknown result type (might be due to invalid IL or missing references)
			//IL_049e: Unknown result type (might be due to invalid IL or missing references)
			//IL_04b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_04db: Unknown result type (might be due to invalid IL or missing references)
			//IL_050c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0518: Unknown result type (might be due to invalid IL or missing references)
			//IL_0540: Unknown result type (might be due to invalid IL or missing references)
			//IL_054a: Unknown result type (might be due to invalid IL or missing references)
			//IL_054f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0563: Unknown result type (might be due to invalid IL or missing references)
			//IL_0594: Unknown result type (might be due to invalid IL or missing references)
			//IL_05a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_05ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_05bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_05c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_05d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_060b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0612: Unknown result type (might be due to invalid IL or missing references)
			//IL_0666: Unknown result type (might be due to invalid IL or missing references)
			//IL_0670: Unknown result type (might be due to invalid IL or missing references)
			//IL_0675: Unknown result type (might be due to invalid IL or missing references)
			//IL_06a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0492: Unknown result type (might be due to invalid IL or missing references)
			//IL_075b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0762: Unknown result type (might be due to invalid IL or missing references)
			//IL_048b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0484: Unknown result type (might be due to invalid IL or missing references)
			//IL_047d: Unknown result type (might be due to invalid IL or missing references)
			//IL_07c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_07c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_07e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_07e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_07fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0801: Unknown result type (might be due to invalid IL or missing references)
			//IL_082d: Unknown result type (might be due to invalid IL or missing references)
			//IL_08ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_0908: Unknown result type (might be due to invalid IL or missing references)
			//IL_0941: Unknown result type (might be due to invalid IL or missing references)
			//IL_0994: Unknown result type (might be due to invalid IL or missing references)
			//IL_0995: Unknown result type (might be due to invalid IL or missing references)
			//IL_09a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a4a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a83: Unknown result type (might be due to invalid IL or missing references)
			//IL_0adb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ae0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ac0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ac5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0afb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b3a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b3f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b56: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b5b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b72: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b77: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b94: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b99: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b9e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bbb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bc0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bc5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bdc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0be1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c00: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c8f: Unknown result type (might be due to invalid IL or missing references)
			AE2Draw.FlatColumn(area);
			AE2Draw.TextIn(new Rect(((Rect)(ref area)).x + 5f, ((Rect)(ref area)).y, ((Rect)(ref area)).width - 10f, 24f), ((object)Translator.Translate("DS_CA_Detail")/*cast due to constrained. prefix*/).ToString(), 0f, AE2Draw.TextCol, (TextAnchor)3);
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(((Rect)(ref area)).x + 5f, ((Rect)(ref area)).y + 26f, ((Rect)(ref area)).width - 10f, ((Rect)(ref area)).height - 30f);
			float y = ((Rect)(ref val)).y;
			if (sel == null || sel.recipe == null)
			{
				AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 3f, ((Rect)(ref val)).y + 3f, ((Rect)(ref val)).width - 6f, 18f), ((object)Translator.Translate("DS_CA_PickOrder")/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextDimCol, (TextAnchor)3);
				return;
			}
			CraftPlan craftPlan = sel;
			CraftLine craftLine = ((craftPlan.lines.Count > 0) ? craftPlan.lines[0] : null);
			Rect r = default(Rect);
			((Rect)(ref r))..ctor(((Rect)(ref val)).x, y, ((Rect)(ref val)).width, 26f);
			AE2Draw.AE2Button(r, active: false, hover: false);
			Rect val2 = new Rect(((Rect)(ref r)).x + 4f, ((Rect)(ref r)).y + 4f, 18f, 18f);
			AE2Draw.SlotBox(val2, hover: false);
			Widgets.DefIcon(GenUI.ContractedBy(val2, 1f), (Def)(object)craftPlan.recipe.ProducedThingDef, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
			AE2Draw.TextIn(new Rect(((Rect)(ref r)).x + 26f, ((Rect)(ref r)).y, ((Rect)(ref r)).width - 30f, ((Rect)(ref r)).height), TaggedString.op_Implicit(((Def)craftPlan.recipe.ProducedThingDef).LabelCap + "  " + CountLabel(craftPlan)), 1f, AE2Draw.TextCol, (TextAnchor)3);
			y += 30f;
			AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 3f, y, 65f, 19f), ((object)Translator.Translate("DS_CA_Skill")/*cast due to constrained. prefix*/).ToString(), 0f, AE2Draw.TextDimCol, (TextAnchor)3);
			AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 68f, y, ((Rect)(ref val)).width - 68f - 2f, 19f), SkillText(craftPlan.recipe), 0f, AE2Draw.TextCol, (TextAnchor)3);
			y += 19f;
			AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 3f, y, 65f, 19f), ((object)Translator.Translate("DS_CA_Material")/*cast due to constrained. prefix*/).ToString(), 0f, AE2Draw.TextDimCol, (TextAnchor)3);
			AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 68f, y, ((Rect)(ref val)).width - 68f - 2f, 19f), MaterialsSummary(craftPlan, ((Thing)((ThingComp)comp).parent).Map), 0f, AE2Draw.TextCol, (TextAnchor)3);
			y += 19f;
			float num = ((craftLine != null && craftLine.HasWork) ? craftLine.Progress01 : (craftPlan.Done ? 1f : 0f));
			AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 3f, y, 65f, 19f), ((object)Translator.Translate("DS_CA_Progress")/*cast due to constrained. prefix*/).ToString(), 0f, AE2Draw.TextDimCol, (TextAnchor)3);
			AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 68f, y, ((Rect)(ref val)).width - 68f - 2f, 19f), (int)(num * 100f) + "%", 0f, AE2Draw.TextCol, (TextAnchor)3);
			y += 19f;
			int num2 = Classify(craftPlan);
			string s = StatusText(craftPlan);
			if (craftPlan.lines.Count == 0 && craftPlan.mode == 2 && craftPlan.countedCount < craftPlan.targetCount)
			{
				num2 = 3;
			}
			Color c = (Color)(num2 switch
			{
				1 => AE2Draw.Accent, 
				3 => AE2Draw.Bad, 
				4 => AE2Draw.TextDimCol, 
				5 => AE2Draw.Accent, 
				_ => AE2Draw.TextCol, 
			});
			AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 3f, y, 65f, 19f), ((object)Translator.Translate("DS_CA_Status")/*cast due to constrained. prefix*/).ToString(), 0f, AE2Draw.TextDimCol, (TextAnchor)3);
			AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 68f, y, ((Rect)(ref val)).width - 68f - 2f, 19f), s, 0f, c, (TextAnchor)3);
			y += 19f;
			AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 3f, y, 65f, 19f), ((object)Translator.Translate("DS_CA_Bench")/*cast due to constrained. prefix*/).ToString(), 0f, AE2Draw.TextDimCol, (TextAnchor)3);
			AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 68f, y, ((Rect)(ref val)).width - 68f - 2f, 19f), ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_Lines", NamedArgument.op_Implicit(BusyLines(craftPlan)), NamedArgument.op_Implicit(comp.UsableBenchCountFor(craftPlan.recipe)))/*cast due to constrained. prefix*/).ToString(), 0f, AE2Draw.TextCol, (TextAnchor)3);
			y += 25f;
			AE2Draw.StateBar(new Rect(((Rect)(ref val)).x + 2f, y, ((Rect)(ref val)).width - 6f, 16f), num, AE2Draw.Accent, (int)(num * 100f) + "%");
			y += 22f;
			AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 3f, y, ((Rect)(ref val)).width - 3f - 2f, 19f), ((object)Translator.Translate("DS_CA_CoreStore")/*cast due to constrained. prefix*/).ToString() + "  " + CoreSummary(((Thing)((ThingComp)comp).parent).Map), 0f, AE2Draw.TextDimCol, (TextAnchor)3);
			y += 21f;
			float num3 = 0f;
			string label = null;
			Building_StorageCore building_StorageCore = FirstCore(((Thing)((ThingComp)comp).parent).Map);
			if (building_StorageCore != null)
			{
				int maxStacks = building_StorageCore.maxStacks;
				int num4 = ((building_StorageCore.innerContainer != null) ? ((ThingOwner)building_StorageCore.innerContainer).Count : 0);
				num3 = ((maxStacks > 0) ? Mathf.Clamp01((float)num4 / (float)maxStacks) : 0f);
				label = (int)(num3 * 100f) + "%";
			}
			AE2Draw.StateBar(new Rect(((Rect)(ref val)).x + 2f, y, ((Rect)(ref val)).width - 6f, 14f), num3, AE2Draw.Warn, label);
			y += 20f;
			float num5 = 32f;
			y += 4f;
			float x = ((Rect)(ref val)).x;
			bool flag = comp.CanTargetMode(craftPlan);
			int selected = ((craftPlan.mode != 2) ? ((craftPlan.mode == 1) ? 1 : 2) : 0);
			string[] labels = new string[3]
			{
				((object)Translator.Translate("DS_CA_ModeKeepShort")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_ModeCountShort")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_ModeForeverShort")/*cast due to constrained. prefix*/).ToString()
			};
			float num6 = Mathf.Min(((Rect)(ref val)).width, 182f);
			switch (AE2Draw.Segmented(new Rect(x, y, num6, num5), labels, selected, 4f))
			{
			case 0:
				if (flag)
				{
					craftPlan.SetTarget((craftPlan.targetCount > 0) ? craftPlan.targetCount : 10);
					if (!craftPlan.pauseWhenSatisfied)
					{
						craftPlan.pauseWhenSatisfied = true;
					}
				}
				break;
			case 1:
				if (craftPlan.mode != 1)
				{
					craftPlan.mode = 1;
					craftPlan.remaining = 0;
					craftPlan.AddCount(1);
					comp.NotifyRootCountChanged(craftPlan);
				}
				break;
			case 2:
				craftPlan.SetForever();
				break;
			}
			y += num5 + 4f;
			x = ((Rect)(ref val)).x;
			if (AE2Draw.TextButton(new Rect(x, y, 42f, num5), "−10"))
			{
				craftPlan.AddCount(-10);
				comp.NotifyRootCountChanged(craftPlan);
			}
			x += 46f;
			if (AE2Draw.TextButton(new Rect(x, y, 36f, num5), "−1"))
			{
				craftPlan.AddCount(-1);
				comp.NotifyRootCountChanged(craftPlan);
			}
			x += 40f;
			Rect val3 = new Rect(x, y, 58f, num5);
			int num7 = ((craftPlan.mode == 2) ? craftPlan.targetCount : Mathf.Max(0, craftPlan.remaining));
			if (countBufPlan != craftPlan || countBufShown != num7)
			{
				countBufPlan = craftPlan;
				countBufShown = num7;
				countBuf = num7.ToString();
			}
			AE2Draw.Sunken(val3, AE2Draw.Slot);
			string text = Widgets.TextField(GenUI.ContractedBy(val3, 3f), countBuf ?? "");
			if (text != countBuf)
			{
				countBuf = text;
				if (int.TryParse(text, out var result) && result >= 0)
				{
					if (craftPlan.mode == 2)
					{
						craftPlan.SetTarget(Mathf.Max(1, result));
					}
					else
					{
						craftPlan.remaining = result;
					}
					countBufShown = ((craftPlan.mode == 2) ? craftPlan.targetCount : Mathf.Max(0, craftPlan.remaining));
					comp.NotifyRootCountChanged(craftPlan);
				}
			}
			x += 62f;
			if (AE2Draw.TextButton(new Rect(x, y, 36f, num5), "+1"))
			{
				craftPlan.AddCount(1);
				comp.NotifyRootCountChanged(craftPlan);
			}
			x += 40f;
			if (AE2Draw.TextButton(new Rect(x, y, 42f, num5), "+10"))
			{
				craftPlan.AddCount(10);
				comp.NotifyRootCountChanged(craftPlan);
			}
			x += 46f;
			string label2 = (craftPlan.suspended ? ((object)Translator.Translate("DS_BA_Resume")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("DS_BA_Suspend")/*cast due to constrained. prefix*/).ToString());
			if (AE2Draw.TextButton(new Rect(x, y, 66f, num5), label2))
			{
				craftPlan.suspended = !craftPlan.suspended;
			}
			y += num5 + 4f;
			x = ((Rect)(ref val)).x;
			string[] array = new string[6]
			{
				((object)Translator.Translate("DS_CA_Up")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_Down")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_PauseWhenSatisfied")/*cast due to constrained. prefix*/).ToString(),
				((object)TranslatorFormattedStringExtensions.Translate("DS_AE2_Stuff", NamedArgument.op_Implicit(StuffName(craftPlan)))/*cast due to constrained. prefix*/).ToString(),
				((object)TranslatorFormattedStringExtensions.Translate("DS_AE2_Style", NamedArgument.op_Implicit(StyleName(craftPlan)))/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_Delete")/*cast due to constrained. prefix*/).ToString()
			};
			for (int i = 0; i < array.Length; i++)
			{
				float num8 = Mathf.Ceil(Text.CalcSize(array[i]).x) + 14f;
				if (num8 > ((Rect)(ref val)).width)
				{
					num8 = ((Rect)(ref val)).width;
				}
				if (x > ((Rect)(ref val)).x && x + num8 > ((Rect)(ref val)).xMax)
				{
					x = ((Rect)(ref val)).x;
					y += num5 + 4f;
				}
				bool active = i switch
				{
					4 => craftPlan.styleDef != null, 
					3 => craftPlan.allowedStuff != null, 
					2 => craftPlan.pauseWhenSatisfied, 
					_ => false, 
				};
				if (AE2Draw.TextButton(new Rect(x, y, num8, num5), array[i], active))
				{
					switch (i)
					{
					case 0:
						comp.MovePlan(craftPlan, -1);
						break;
					case 1:
						comp.MovePlan(craftPlan, 1);
						break;
					case 2:
						craftPlan.pauseWhenSatisfied = !craftPlan.pauseWhenSatisfied;
						break;
					case 3:
						OpenStuffMenu(craftPlan);
						break;
					case 4:
						OpenStyleMenu(craftPlan);
						break;
					case 5:
						comp.RemovePlan(craftPlan, singleOnly: true);
						sel = null;
						break;
					}
				}
				x += num8 + 2f;
			}
		}

		private static CompBillAutomation FindAnyProxy()
		{
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return null;
			}
			List<Thing> allThings = currentMap.listerThings.AllThings;
			for (int i = 0; i < allThings.Count; i++)
			{
				Thing val = allThings[i];
				if (val != null && val.Spawned)
				{
					CompBillAutomation compBillAutomation = ThingCompUtility.TryGetComp<CompBillAutomation>(val);
					if (compBillAutomation != null)
					{
						return compBillAutomation;
					}
				}
			}
			return null;
		}

		private float RemainSeconds(CraftLine line)
		{
			float num = Math.Max(0.0001f, line.BaseRate);
			if (comp != null)
			{
				num *= Math.Max(0.01f, comp.Props.workSpeedMult) * comp.OverclockSpeedMult;
			}
			return Math.Max(0f, line.WorkLeft) / num / 60f;
		}

		private static int BusyLines(CraftPlan p)
		{
			int num = 0;
			for (int i = 0; i < p.lines.Count; i++)
			{
				CraftLine craftLine = p.lines[i];
				if (craftLine != null && craftLine.HasWork)
				{
					num++;
				}
			}
			return num;
		}

		private static string ModeShort(CraftPlan p)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			if (p.mode == 0)
			{
				return ((object)Translator.Translate("DS_BA_Forever")/*cast due to constrained. prefix*/).ToString();
			}
			if (p.mode == 1)
			{
				return ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_Remain", NamedArgument.op_Implicit(Mathf.Max(0, p.remaining)))/*cast due to constrained. prefix*/).ToString();
			}
			return p.countedCount + "/" + p.targetCount;
		}

		private static string ModeLabel(CraftPlan p)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			if (p.mode == 0)
			{
				return ((object)Translator.Translate("DS_BA_Forever")/*cast due to constrained. prefix*/).ToString();
			}
			if (p.mode == 1)
			{
				return ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_Remain", NamedArgument.op_Implicit(Mathf.Max(0, p.remaining)))/*cast due to constrained. prefix*/).ToString();
			}
			return ((object)TranslatorFormattedStringExtensions.Translate("DS_CA_Target", NamedArgument.op_Implicit(p.countedCount), NamedArgument.op_Implicit(p.targetCount))/*cast due to constrained. prefix*/).ToString();
		}

		private static string SkillText(RecipeDef r)
		{
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			if (r == null)
			{
				return "?";
			}
			if (r.skillRequirements != null && r.skillRequirements.Count > 0)
			{
				for (int i = 0; i < r.skillRequirements.Count; i++)
				{
					SkillRequirement val = r.skillRequirements[i];
					if (val != null && val.skill != null)
					{
						return ((object)((Def)val.skill).LabelCap/*cast due to constrained. prefix*/).ToString() + " " + val.minLevel;
					}
				}
			}
			return ((object)Translator.Translate("DS_CA_SkillNone")/*cast due to constrained. prefix*/).ToString();
		}

		private static string StuffName(CraftPlan p)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			if (p == null || p.allowedStuff == null)
			{
				return ((object)Translator.Translate("DS_AE2_StuffAny")/*cast due to constrained. prefix*/).ToString();
			}
			return ((object)((Def)p.allowedStuff).LabelCap/*cast due to constrained. prefix*/).ToString();
		}

		private static string StyleName(CraftPlan p)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			if (p == null || p.styleDef == null)
			{
				return ((object)Translator.Translate("DS_AE2_StyleAny")/*cast due to constrained. prefix*/).ToString();
			}
			return ((object)((Def)p.styleDef).LabelCap/*cast due to constrained. prefix*/).ToString();
		}

		private void OpenStyleMenu(CraftPlan p)
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Expected O, but got Unknown
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d8: Expected O, but got Unknown
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Expected O, but got Unknown
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			list.Add(new FloatMenuOption(((object)Translator.Translate("DS_AE2_StyleAny")/*cast due to constrained. prefix*/).ToString(), (Action)delegate
			{
				p.styleDef = null;
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			List<ThingStyleDef> allDefsListForReading = DefDatabase<ThingStyleDef>.AllDefsListForReading;
			for (int num = 0; num < allDefsListForReading.Count; num++)
			{
				ThingStyleDef val = allDefsListForReading[num];
				if (val != null)
				{
					ThingStyleDef cap = val;
					list.Add(new FloatMenuOption(((object)((Def)val).LabelCap/*cast due to constrained. prefix*/).ToString(), (Action)delegate
					{
						p.styleDef = cap;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
				}
			}
			Find.WindowStack.Add((Window)new FloatMenu(list));
		}

		private void OpenStuffMenu(CraftPlan p)
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Expected O, but got Unknown
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00eb: Expected O, but got Unknown
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cb: Expected O, but got Unknown
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			list.Add(new FloatMenuOption(((object)Translator.Translate("DS_AE2_StuffAny")/*cast due to constrained. prefix*/).ToString(), (Action)delegate
			{
				p.allowedStuff = null;
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			List<ThingDef> list2 = BillProbe.UsableStuffs(p.recipe);
			if (list2 != null)
			{
				for (int num = 0; num < list2.Count; num++)
				{
					ThingDef val = list2[num];
					if (val != null)
					{
						ThingDef dd = val;
						list.Add(new FloatMenuOption(((object)((Def)dd).LabelCap/*cast due to constrained. prefix*/).ToString(), (Action)delegate
						{
							p.allowedStuff = dd;
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					}
				}
			}
			Find.WindowStack.Add((Window)new FloatMenu(list));
		}

		private static float DrawMaterials(CraftPlan p, CraftLine first, Rect ri, float y, Map map)
		{
			//IL_0366: Unknown result type (might be due to invalid IL or missing references)
			//IL_0372: Unknown result type (might be due to invalid IL or missing references)
			//IL_0379: Unknown result type (might be due to invalid IL or missing references)
			//IL_0380: Unknown result type (might be due to invalid IL or missing references)
			//IL_0385: Unknown result type (might be due to invalid IL or missing references)
			//IL_038a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0158: Unknown result type (might be due to invalid IL or missing references)
			//IL_0162: Unknown result type (might be due to invalid IL or missing references)
			//IL_0167: Unknown result type (might be due to invalid IL or missing references)
			//IL_017b: Unknown result type (might be due to invalid IL or missing references)
			//IL_03ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00df: Unknown result type (might be due to invalid IL or missing references)
			//IL_026c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0271: Unknown result type (might be due to invalid IL or missing references)
			//IL_0276: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ff: Unknown result type (might be due to invalid IL or missing references)
			Dictionary<ThingDef, int> dictionary = CraftTree.CoreStock(map);
			if (first != null && first.HasWork && first.Ingredients != null && first.Counts != null)
			{
				for (int i = 0; i < first.Ingredients.Length && i < first.Counts.Length; i++)
				{
					Thing val = first.Ingredients[i];
					if (val != null && val.def != null)
					{
						int num = first.Counts[i];
						dictionary.TryGetValue(val.def, out var value);
						AE2Draw.TextIn(new Rect(((Rect)(ref ri)).x + 4f, y, ((Rect)(ref ri)).width - 8f, 15f), ((object)TranslatorFormattedStringExtensions.Translate("DS_CA_Have", NamedArgument.op_Implicit(num + "×" + ((Def)val.def).LabelCap), NamedArgument.op_Implicit(value))/*cast due to constrained. prefix*/).ToString(), 1f, (value < num) ? AE2Draw.Bad : AE2Draw.TextCol, (TextAnchor)3);
						y += 19f;
					}
				}
				return y;
			}
			if (p.recipe.ingredients == null || p.recipe.ingredients.Count == 0)
			{
				AE2Draw.TextIn(new Rect(((Rect)(ref ri)).x + 4f, y, ((Rect)(ref ri)).width - 8f, 15f), ((object)Translator.Translate("DS_CA_NoMaterials")/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextDimCol, (TextAnchor)3);
				return y + 16f;
			}
			int num2 = ((p.mode == 2) ? Mathf.Max(1, p.targetCount - p.countedCount) : ((p.mode != 1) ? 1 : Mathf.Max(1, p.remaining)));
			for (int j = 0; j < p.recipe.ingredients.Count; j++)
			{
				IngredientCount val2 = p.recipe.ingredients[j];
				if (val2 == null || val2.filter == null)
				{
					continue;
				}
				string text = "";
				int num3 = 0;
				int num4 = 0;
				foreach (KeyValuePair<ThingDef, int> item in dictionary)
				{
					if (item.Key != null && item.Value > 0 && val2.filter.Allows(item.Key))
					{
						num3 += item.Value;
						if (num4 < 3)
						{
							text = TaggedString.op_Implicit(text + (((text.Length > 0) ? " / " : "") + ((Def)item.Key).LabelCap));
							num4++;
						}
					}
				}
				if (num4 == 0 && val2.filter.AllowedThingDefs != null)
				{
					foreach (ThingDef allowedThingDef in val2.filter.AllowedThingDefs)
					{
						if (allowedThingDef != null)
						{
							text = TaggedString.op_Implicit(text + (((text.Length > 0) ? " / " : "") + ((Def)allowedThingDef).LabelCap));
							if (++num4 >= 3)
							{
								break;
							}
						}
					}
				}
				int num5 = 0;
				try
				{
					num5 = (int)(val2.GetBaseCount() * (float)num2);
				}
				catch
				{
					num5 = num2;
				}
				AE2Draw.TextIn(new Rect(((Rect)(ref ri)).x + 4f, y, ((Rect)(ref ri)).width - 8f, 15f), ((object)TranslatorFormattedStringExtensions.Translate("DS_AE2_NeedWithCore", NamedArgument.op_Implicit(text), NamedArgument.op_Implicit(num5), NamedArgument.op_Implicit(num3))/*cast due to constrained. prefix*/).ToString(), 1f, (num3 >= num5) ? AE2Draw.TextCol : AE2Draw.Bad, (TextAnchor)3);
				y += 19f;
			}
			return y;
		}

		private static string MaterialsSummary(CraftPlan p, Map map)
		{
			//IL_019b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			if (p == null || p.recipe == null)
			{
				return "-";
			}
			RecipeDef recipe = p.recipe;
			string text = "-";
			int num = 0;
			if (recipe.ingredients != null && recipe.ingredients.Count > 0 && recipe.ingredients[0].filter != null)
			{
				List<ThingDef> list = ((recipe.ingredients[0].filter.AllowedThingDefs != null) ? new List<ThingDef>(recipe.ingredients[0].filter.AllowedThingDefs) : new List<ThingDef>());
				if (list.Count > 0)
				{
					text = ((object)((Def)list[0]).LabelCap/*cast due to constrained. prefix*/).ToString();
				}
				try
				{
					num = (int)recipe.ingredients[0].GetBaseCount();
				}
				catch
				{
					num = 1;
				}
			}
			int num2 = 0;
			List<KeyValuePair<ThingDef, int>> list2 = CoreItems(map);
			for (int i = 0; i < list2.Count; i++)
			{
				if (list2[i].Key != null && recipe.ingredients != null && recipe.ingredients.Count > 0 && recipe.ingredients[0].filter != null && recipe.ingredients[0].filter.Allows(list2[i].Key))
				{
					num2 += list2[i].Value;
				}
			}
			return text + " ×" + num + "  " + ((object)TranslatorFormattedStringExtensions.Translate("DS_CA_CoreHave", NamedArgument.op_Implicit(num2))/*cast due to constrained. prefix*/).ToString();
		}

		private static string CoreSummary(Map map)
		{
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			List<KeyValuePair<ThingDef, int>> list = CoreItems(map);
			int num = 0;
			for (int i = 0; i < list.Count; i++)
			{
				num += list[i].Value;
			}
			return ((object)TranslatorFormattedStringExtensions.Translate("DS_AE2_CoreSummary", NamedArgument.op_Implicit(list.Count), NamedArgument.op_Implicit(num))/*cast due to constrained. prefix*/).ToString();
		}

		private void SetAllSuspended(bool on)
		{
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			IList<CraftPlan> plansForReading = comp.PlansForReading;
			if (plansForReading == null)
			{
				return;
			}
			int num = 0;
			for (int i = 0; i < plansForReading.Count; i++)
			{
				CraftPlan craftPlan = plansForReading[i];
				if (craftPlan != null && craftPlan.suspended != on)
				{
					craftPlan.suspended = on;
					num++;
				}
			}
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_CA_SuspendAllMsg", NamedArgument.op_Implicit(num), NamedArgument.op_Implicit(on ? ((object)Translator.Translate("DS_BA_Suspend")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("DS_BA_Resume")/*cast due to constrained. prefix*/).ToString()))), MessageTypeDefOf.SilentInput, false);
		}

		private void OpenProxyMenu()
		{
			//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Expected O, but got Unknown
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ef: Expected O, but got Unknown
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Expected O, but got Unknown
			Map map = ((Thing)((ThingComp)comp).parent).Map;
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			List<Building> allBuildingsColonist = map.listerBuildings.allBuildingsColonist;
			for (int i = 0; i < allBuildingsColonist.Count; i++)
			{
				Building val = allBuildingsColonist[i];
				if (val == null)
				{
					continue;
				}
				CompBillAutomation compBillAutomation = ((ThingWithComps)val).GetComp<CompBillAutomation>();
				if (compBillAutomation != null)
				{
					CompBillAutomation cc = compBillAutomation;
					list.Add(new FloatMenuOption(((Entity)val).LabelCap.ToString() + (((object)val == ((ThingComp)comp).parent) ? "  ✓" : ""), (Action)delegate
					{
						comp = cc;
						sel = null;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
				}
			}
			if (list.Count == 0)
			{
				list.Add(new FloatMenuOption(((object)Translator.Translate("DS_AE2_NoProxyInMap")/*cast due to constrained. prefix*/).ToString(), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
			Find.WindowStack.Add((Window)new FloatMenu(list));
		}

		private static Building_StorageCore FirstCore(Map map)
		{
			if (map == null)
			{
				return null;
			}
			List<Building_StorageCore> list = CoreFinder.AllUsableCores(map);
			if (list == null || list.Count <= 0)
			{
				return null;
			}
			return list[0];
		}

		private static string FilterHint(int i)
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			return i switch
			{
				0 => ((object)Translator.Translate("DS_AE2_FilterAllHint")/*cast due to constrained. prefix*/).ToString(), 
				1 => ((object)Translator.Translate("DS_AE2_FilterRunningHint")/*cast due to constrained. prefix*/).ToString(), 
				2 => ((object)Translator.Translate("DS_AE2_FilterWaitingHint")/*cast due to constrained. prefix*/).ToString(), 
				3 => ((object)Translator.Translate("DS_AE2_FilterBlockedHint")/*cast due to constrained. prefix*/).ToString(), 
				4 => ((object)Translator.Translate("DS_AE2_FilterSuspendedHint")/*cast due to constrained. prefix*/).ToString(), 
				5 => ((object)Translator.Translate("DS_AE2_FilterDoneHint")/*cast due to constrained. prefix*/).ToString(), 
				_ => "", 
			};
		}

		private void TakeFromCore(Map map, ThingDef def, int count)
		{
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			if (map == null || def == null || count <= 0)
			{
				return;
			}
			List<Building_StorageCore> list = CoreFinder.AllUsableCores(map);
			if (list == null)
			{
				return;
			}
			int num = count;
			IntVec3 val = ((comp != null && ((ThingComp)comp).parent != null) ? ((Thing)((ThingComp)comp).parent).Position : map.Center);
			for (int i = 0; i < list.Count; i++)
			{
				if (num <= 0)
				{
					break;
				}
				Building_StorageCore building_StorageCore = list[i];
				if (building_StorageCore == null)
				{
					continue;
				}
				ThingOwner directlyHeldThings = building_StorageCore.GetDirectlyHeldThings();
				if (directlyHeldThings == null)
				{
					continue;
				}
				int num2 = directlyHeldThings.Count - 1;
				while (num2 >= 0 && num > 0)
				{
					Thing val2 = directlyHeldThings[num2];
					if (val2 != null && val2.def == def)
					{
						int num3 = Mathf.Min(num, val2.stackCount);
						Thing val3 = val2.SplitOff(num3);
						if (val3 != null)
						{
							num -= num3;
							if (!GenPlace.TryPlaceThing(val3, val, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1) && !directlyHeldThings.TryAdd(val3, false))
							{
								GenPlace.TryPlaceThing(val3, val, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
							}
						}
					}
					num2--;
				}
			}
		}
	}
	public class Dialog_AE2AddRecipe : Window
	{
		private readonly CompBillAutomation comp;

		private string search = "";

		private string catFilter;

		private float scroll;

		private RecipeDef sel;

		private int count = 1;

		private int rootMode;

		private ThingDef stuff;

		private ThingStyleDef style;

		private const float RowH = 26f;

		private const float BottomH = 158f;

		public override Vector2 InitialSize => new Vector2(560f, 520f);

		public Dialog_AE2AddRecipe(CompBillAutomation comp)
			: base((IWindowDrawing)null)
		{
			this.comp = comp;
			base.doCloseX = false;
			base.closeOnClickedOutside = false;
			base.absorbInputAroundWindow = false;
			base.onlyOneOfTypeAllowed = true;
			base.draggable = true;
			base.resizeable = true;
			base.doWindowBackground = false;
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_010c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0170: Unknown result type (might be due to invalid IL or missing references)
			//IL_0172: Unknown result type (might be due to invalid IL or missing references)
			//IL_017d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0184: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (comp == null || ((ThingComp)comp).parent == null)
				{
					((Window)this).Close(true);
					return;
				}
				AE2Draw.HandlePauseHotkey();
				bool closeClicked;
				Rect val = AE2Draw.WindowFrame(GenUI.ContractedBy(inRect, 2f), ((object)Translator.Translate("DS_AE2_AddTitle")/*cast due to constrained. prefix*/).ToString(), out closeClicked);
				if (closeClicked)
				{
					((Window)this).Close(true);
					return;
				}
				Rect val2 = default(Rect);
				((Rect)(ref val2))..ctor(((Rect)(ref val)).x, ((Rect)(ref val)).y, ((Rect)(ref val)).width, 22f);
				Rect r = new Rect(((Rect)(ref val2)).x, ((Rect)(ref val2)).y, 84f, ((Rect)(ref val2)).height);
				string label = ((catFilter == null) ? ((object)Translator.Translate("DS_AE2_CatAll")/*cast due to constrained. prefix*/).ToString() : catFilter);
				if (AE2Draw.TextButton(r, label))
				{
					OpenCategoryMenu();
				}
				string text = AE2Draw.TextField(new Rect(((Rect)(ref val2)).x + 88f, ((Rect)(ref val2)).y, Mathf.Max(60f, ((Rect)(ref val2)).width - 88f), ((Rect)(ref val2)).height), search ?? "");
				if (text != search)
				{
					search = text;
					scroll = 0f;
				}
				Rect val3 = default(Rect);
				((Rect)(ref val3))..ctor(((Rect)(ref val)).x, ((Rect)(ref val)).yMax - 158f, ((Rect)(ref val)).width, 158f);
				AE2Draw.Sunken(val3, AE2Draw.Slot);
				DrawBottom(GenUI.ContractedBy(val3, 4f));
				Rect area = default(Rect);
				((Rect)(ref area))..ctor(((Rect)(ref val)).x, ((Rect)(ref val2)).yMax + 4f, ((Rect)(ref val)).width, ((Rect)(ref val3)).y - ((Rect)(ref val2)).yMax - 8f);
				DrawList(area);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] AE2 界面绘制异常（只记一次，界面不会卡死）：" + ex, 771002);
			}
		}

		private void DrawList(Rect area)
		{
			//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0238: Unknown result type (might be due to invalid IL or missing references)
			//IL_023d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0218: Unknown result type (might be due to invalid IL or missing references)
			//IL_021d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0222: Unknown result type (might be due to invalid IL or missing references)
			//IL_0306: Unknown result type (might be due to invalid IL or missing references)
			//IL_0308: Unknown result type (might be due to invalid IL or missing references)
			//IL_033a: Unknown result type (might be due to invalid IL or missing references)
			//IL_033f: Unknown result type (might be due to invalid IL or missing references)
			//IL_034b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0251: Unknown result type (might be due to invalid IL or missing references)
			//IL_0158: Unknown result type (might be due to invalid IL or missing references)
			//IL_015d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0173: Unknown result type (might be due to invalid IL or missing references)
			//IL_0178: Unknown result type (might be due to invalid IL or missing references)
			//IL_0389: Unknown result type (might be due to invalid IL or missing references)
			//IL_0382: Unknown result type (might be due to invalid IL or missing references)
			//IL_04a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_048a: Unknown result type (might be due to invalid IL or missing references)
			//IL_038e: Unknown result type (might be due to invalid IL or missing references)
			//IL_03bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_03c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_03cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0401: Unknown result type (might be due to invalid IL or missing references)
			//IL_0409: Unknown result type (might be due to invalid IL or missing references)
			//IL_0440: Unknown result type (might be due to invalid IL or missing references)
			//IL_0412: Unknown result type (might be due to invalid IL or missing references)
			//IL_0416: Unknown result type (might be due to invalid IL or missing references)
			//IL_0420: Unknown result type (might be due to invalid IL or missing references)
			//IL_042c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0436: Unknown result type (might be due to invalid IL or missing references)
			List<RecipeDef> list = new List<RecipeDef>();
			IList<RecipeDef> unlockedRecipes = comp.UnlockedRecipes;
			if (unlockedRecipes != null)
			{
				for (int i = 0; i < unlockedRecipes.Count; i++)
				{
					RecipeDef val = unlockedRecipes[i];
					if (val != null && !list.Contains(val))
					{
						list.Add(val);
					}
				}
			}
			foreach (RecipeDef allDef in DefDatabase<RecipeDef>.AllDefs)
			{
				if (allDef == null || allDef.ProducedThingDef == null || allDef.recipeUsers == null)
				{
					continue;
				}
				bool flag = false;
				for (int j = 0; j < allDef.recipeUsers.Count; j++)
				{
					ThingDef val2 = allDef.recipeUsers[j];
					if (val2 != null && ((Def)val2).defName != null && ((Def)val2).defName.StartsWith("DigitalStorage_WorkerCraft"))
					{
						flag = true;
						break;
					}
				}
				if (flag && !list.Contains(allDef))
				{
					list.Add(allDef);
				}
			}
			List<RecipeDef> list2 = new List<RecipeDef>();
			for (int k = 0; k < list.Count; k++)
			{
				RecipeDef val3 = list[k];
				if (val3 == null || val3.ProducedThingDef == null || (catFilter != null && CatOf(val3.ProducedThingDef) != catFilter))
				{
					continue;
				}
				if (!string.IsNullOrEmpty(search))
				{
					string text = ((object)((Def)val3).LabelCap/*cast due to constrained. prefix*/).ToString();
					string text2 = ((object)((Def)val3.ProducedThingDef).LabelCap/*cast due to constrained. prefix*/).ToString();
					if (text.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 && text2.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
					{
						continue;
					}
				}
				list2.Add(val3);
			}
			if (list2.Count == 0)
			{
				AE2Draw.TextIn(new Rect(((Rect)(ref area)).x + 4f, ((Rect)(ref area)).y + 4f, ((Rect)(ref area)).width - 8f, 17f), string.IsNullOrEmpty(search) ? ((object)Translator.Translate("DS_AE2_NoRecipeInCat")/*cast due to constrained. prefix*/).ToString() : ((object)TranslatorFormattedStringExtensions.Translate("DS_AE2_NoRecipeMatch", NamedArgument.op_Implicit(search))/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextDimCol, (TextAnchor)3);
				return;
			}
			int num = Mathf.Max(1, Mathf.FloorToInt((((Rect)(ref area)).height - 2f) / 27f));
			int num2 = Mathf.Max(0, list2.Count - num);
			int num3 = Mathf.RoundToInt(scroll * (float)num2);
			int num4 = Mathf.Min(num, Mathf.Max(0, list2.Count - num3));
			Rect val5 = default(Rect);
			for (int l = 0; l < num4; l++)
			{
				RecipeDef val4 = list2[num3 + l];
				((Rect)(ref val5))..ctor(((Rect)(ref area)).x, ((Rect)(ref area)).y + (float)l * 27f, ((Rect)(ref area)).width - 14f, 26f);
				bool flag2 = val4 == sel;
				AE2Draw.Row(val5, Mouse.IsOver(val5), flag2);
				Rect val6 = new Rect(((Rect)(ref val5)).x + 3f, ((Rect)(ref val5)).y + 3f, 20f, 20f);
				AE2Draw.SlotBox(val6, hover: false);
				Widgets.DefIcon(GenUI.ContractedBy(val6, 1f), (Def)(object)val4.ProducedThingDef, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
				Color c = (flag2 ? AE2Draw.Hi : AE2Draw.TextCol);
				AE2Draw.TextIn(new Rect(((Rect)(ref val5)).x + 28f, ((Rect)(ref val5)).y + 2f, ((Rect)(ref val5)).width - 32f, 21f), ((object)((Def)val4.ProducedThingDef).LabelCap/*cast due to constrained. prefix*/).ToString() + "\u3000" + ((object)((Def)val4).LabelCap/*cast due to constrained. prefix*/).ToString(), 1f, c, (TextAnchor)3);
				if (Mouse.IsOver(val5))
				{
					TooltipHandler.TipRegion(val5, TipSignal.op_Implicit(TaggedString.op_Implicit(((Def)val4).LabelCap + "\n" + ((Def)val4).description)));
				}
				if (Widgets.ButtonInvisible(val5, true))
				{
					sel = val4;
				}
			}
			if (list2.Count > num)
			{
				AE2Draw.DragBar(new Rect(((Rect)(ref area)).xMax - 12f, ((Rect)(ref area)).y, 12f, ((Rect)(ref area)).height), ref scroll, Mathf.Clamp01((float)num / (float)list2.Count));
			}
			AE2Draw.WheelScroll(area, ref scroll, num2);
		}

		private static string CatOf(ThingDef d)
		{
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			if (d == null || d.FirstThingCategory == null)
			{
				return null;
			}
			ThingCategoryDef val = d.FirstThingCategory;
			while (val.parent != null && val.parent.parent != null)
			{
				val = val.parent;
			}
			return ((object)((Def)val).LabelCap/*cast due to constrained. prefix*/).ToString();
		}

		private void OpenCategoryMenu()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Expected O, but got Unknown
			//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bf: Expected O, but got Unknown
			//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01df: Expected O, but got Unknown
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			list.Add(new FloatMenuOption(((object)Translator.Translate("DS_AE2_CatAll")/*cast due to constrained. prefix*/).ToString(), (Action)delegate
			{
				catFilter = null;
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			List<string> list2 = new List<string>();
			foreach (RecipeDef allDef in DefDatabase<RecipeDef>.AllDefs)
			{
				if (allDef == null || allDef.ProducedThingDef == null || allDef.recipeUsers == null)
				{
					continue;
				}
				bool flag = false;
				for (int num = 0; num < allDef.recipeUsers.Count; num++)
				{
					ThingDef val = allDef.recipeUsers[num];
					if (val != null && ((Def)val).defName != null && ((Def)val).defName.StartsWith("DigitalStorage_WorkerCraft"))
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					string text = CatOf(allDef.ProducedThingDef);
					if (text != null && !list2.Contains(text))
					{
						list2.Add(text);
					}
				}
			}
			IList<RecipeDef> unlockedRecipes = comp.UnlockedRecipes;
			if (unlockedRecipes != null)
			{
				for (int num2 = 0; num2 < unlockedRecipes.Count; num2++)
				{
					RecipeDef val2 = unlockedRecipes[num2];
					string text2 = ((val2 == null) ? null : CatOf(val2.ProducedThingDef));
					if (text2 != null && !list2.Contains(text2))
					{
						list2.Add(text2);
					}
				}
			}
			list2.Sort();
			for (int num3 = 0; num3 < list2.Count; num3++)
			{
				string cap = list2[num3];
				list.Add(new FloatMenuOption(cap, (Action)delegate
				{
					catFilter = cap;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
			Find.WindowStack.Add((Window)new FloatMenu(list));
		}

		private void DrawBottom(Rect b)
		{
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_00df: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_010e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_0118: Unknown result type (might be due to invalid IL or missing references)
			//IL_0146: Unknown result type (might be due to invalid IL or missing references)
			//IL_014b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0157: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_020f: Unknown result type (might be due to invalid IL or missing references)
			//IL_024e: Unknown result type (might be due to invalid IL or missing references)
			//IL_028b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0290: Unknown result type (might be due to invalid IL or missing references)
			//IL_0291: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_030c: Unknown result type (might be due to invalid IL or missing references)
			//IL_034e: Unknown result type (might be due to invalid IL or missing references)
			//IL_038e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0393: Unknown result type (might be due to invalid IL or missing references)
			//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_03af: Unknown result type (might be due to invalid IL or missing references)
			//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_03cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0400: Unknown result type (might be due to invalid IL or missing references)
			//IL_0466: Unknown result type (might be due to invalid IL or missing references)
			//IL_0493: Unknown result type (might be due to invalid IL or missing references)
			//IL_04dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_04e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_050e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0537: Unknown result type (might be due to invalid IL or missing references)
			//IL_053c: Unknown result type (might be due to invalid IL or missing references)
			//IL_051c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0521: Unknown result type (might be due to invalid IL or missing references)
			//IL_055b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0554: Unknown result type (might be due to invalid IL or missing references)
			//IL_056a: Unknown result type (might be due to invalid IL or missing references)
			if (sel == null || sel.ProducedThingDef == null)
			{
				AE2Draw.TextIn(new Rect(((Rect)(ref b)).x + 2f, ((Rect)(ref b)).y + 4f, ((Rect)(ref b)).width - 4f, 17f), ((object)Translator.Translate("DS_AE2_PickRecipeFirst")/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextDimCol, (TextAnchor)3);
				return;
			}
			string label = ((object)TranslatorFormattedStringExtensions.Translate("DS_AE2_Stuff", NamedArgument.op_Implicit((stuff == null) ? ((object)Translator.Translate("DS_AE2_StuffAny")/*cast due to constrained. prefix*/).ToString() : ((object)((Def)stuff).LabelCap/*cast due to constrained. prefix*/).ToString()))/*cast due to constrained. prefix*/).ToString();
			string label2 = ((object)TranslatorFormattedStringExtensions.Translate("DS_AE2_Style", NamedArgument.op_Implicit((style == null) ? ((object)Translator.Translate("DS_AE2_StyleAny")/*cast due to constrained. prefix*/).ToString() : ((object)((Def)style).LabelCap/*cast due to constrained. prefix*/).ToString()))/*cast due to constrained. prefix*/).ToString();
			Rect val = new Rect(((Rect)(ref b)).x, ((Rect)(ref b)).y + 2f, 20f, 20f);
			AE2Draw.SlotBox(val, hover: false);
			Widgets.DefIcon(GenUI.ContractedBy(val, 1f), (Def)(object)sel.ProducedThingDef, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
			AE2Draw.TextIn(new Rect(((Rect)(ref b)).x + 26f, ((Rect)(ref b)).y + 2f, ((Rect)(ref b)).width - 30f, 21f), ((object)((Def)sel.ProducedThingDef).LabelCap/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.Hi, (TextAnchor)3);
			float num = ((Rect)(ref b)).y + 28f;
			if (Btn(new Rect(((Rect)(ref b)).x, num, 34f, 24f), "−10"))
			{
				count = Mathf.Max(1, count - 10);
			}
			if (Btn(new Rect(((Rect)(ref b)).x + 36f, num, 30f, 24f), "−1"))
			{
				count = Mathf.Max(1, count - 1);
			}
			Rect val2 = new Rect(((Rect)(ref b)).x + 68f, num, 76f, 24f);
			AE2Draw.Sunken(val2, AE2Draw.Slot);
			if (int.TryParse(Widgets.TextField(GenUI.ContractedBy(val2, 3f), (rootMode == 2) ? "∞" : count.ToString()), out var result) && result > 0)
			{
				count = Mathf.Min(99999, result);
				if (rootMode == 2)
				{
					rootMode = 0;
				}
			}
			if (Btn(new Rect(((Rect)(ref b)).x + 146f, num, 30f, 24f), "+1"))
			{
				count = Mathf.Min(99999, count + 1);
			}
			if (Btn(new Rect(((Rect)(ref b)).x + 178f, num, 34f, 24f), "+10"))
			{
				count = Mathf.Min(99999, count + 10);
			}
			float num2 = num + 26f;
			string[] labels = new string[3]
			{
				((object)Translator.Translate("DS_CA_ModeKeepShort")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_ModeCountShort")/*cast due to constrained. prefix*/).ToString(),
				((object)Translator.Translate("DS_CA_ModeForeverShort")/*cast due to constrained. prefix*/).ToString()
			};
			float num3 = Mathf.Min(((Rect)(ref b)).width, 182f);
			int num4 = AE2Draw.Segmented(new Rect(((Rect)(ref b)).x, num2, num3, 24f), labels, rootMode, 4f);
			if (num4 >= 0)
			{
				rootMode = num4;
			}
			float num5 = num2 + 28f;
			float num6 = Mathf.Max(60f, (((Rect)(ref b)).width - 150f - 12f) * 0.5f);
			if (Btn(new Rect(((Rect)(ref b)).x, num5, num6, 32f), label))
			{
				OpenStuffMenu();
			}
			if (Btn(new Rect(((Rect)(ref b)).x + num6 + 6f, num5, num6, 32f), label2))
			{
				OpenStyleMenu();
			}
			Rect val3 = default(Rect);
			((Rect)(ref val3))..ctor(((Rect)(ref b)).xMax - 150f, num5, 150f, 32f);
			bool flag = comp.JobForRecipe(sel) == null;
			AE2Draw.AE2Button(val3, active: false, flag && Mouse.IsOver(val3));
			AE2Draw.TextIn(new Rect(((Rect)(ref val3)).x, ((Rect)(ref val3)).y, ((Rect)(ref val3)).width, ((Rect)(ref val3)).height), flag ? ((object)Translator.Translate("DS_AE2_Submit")/*cast due to constrained. prefix*/).ToString() : ((object)Translator.Translate("DS_AE2_AlreadyInJob")/*cast due to constrained. prefix*/).ToString(), 1f, flag ? AE2Draw.TextCol : AE2Draw.TextDimCol, (TextAnchor)4);
			if (flag && Widgets.ButtonInvisible(val3, true))
			{
				Submit();
			}
		}

		private bool Btn(Rect r, string label)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			bool hover = Mouse.IsOver(r);
			AE2Draw.AE2Button(r, active: false, hover);
			AE2Draw.TextIn(new Rect(((Rect)(ref r)).x, ((Rect)(ref r)).y, ((Rect)(ref r)).width, ((Rect)(ref r)).height), label, 1f, AE2Draw.TextCol, (TextAnchor)4);
			return Widgets.ButtonInvisible(r, true);
		}

		private void OpenStuffMenu()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Expected O, but got Unknown
			//IL_0104: Unknown result type (might be due to invalid IL or missing references)
			//IL_010e: Expected O, but got Unknown
			//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fe: Expected O, but got Unknown
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Expected O, but got Unknown
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			list.Add(new FloatMenuOption(((object)Translator.Translate("DS_AE2_StuffNone")/*cast due to constrained. prefix*/).ToString(), (Action)delegate
			{
				stuff = null;
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			List<ThingDef> list2 = BillProbe.UsableStuffs(sel);
			if (list2 != null)
			{
				for (int num = 0; num < list2.Count; num++)
				{
					ThingDef d = list2[num];
					if (d != null)
					{
						list.Add(new FloatMenuOption(((object)((Def)d).LabelCap/*cast due to constrained. prefix*/).ToString(), (Action)delegate
						{
							stuff = d;
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					}
				}
			}
			if (list.Count == 1)
			{
				list.Add(new FloatMenuOption(((object)Translator.Translate("DS_AE2_NoStuff")/*cast due to constrained. prefix*/).ToString(), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
			Find.WindowStack.Add((Window)new FloatMenu(list));
		}

		private void OpenStyleMenu()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Expected O, but got Unknown
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c6: Expected O, but got Unknown
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Expected O, but got Unknown
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			list.Add(new FloatMenuOption(((object)Translator.Translate("DS_AE2_StyleAny")/*cast due to constrained. prefix*/).ToString(), (Action)delegate
			{
				style = null;
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			List<ThingStyleDef> allDefsListForReading = DefDatabase<ThingStyleDef>.AllDefsListForReading;
			for (int num = 0; num < allDefsListForReading.Count; num++)
			{
				ThingStyleDef val = allDefsListForReading[num];
				if (val != null)
				{
					ThingStyleDef cap = val;
					list.Add(new FloatMenuOption(((object)((Def)val).LabelCap/*cast due to constrained. prefix*/).ToString(), (Action)delegate
					{
						style = cap;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
				}
			}
			Find.WindowStack.Add((Window)new FloatMenu(list));
		}

		private void Submit()
		{
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
			int num = ((rootMode == 2) ? 1 : Mathf.Max(1, count));
			CraftJob craftJob = comp.SubmitJob(sel, null, num, withIntermediates: true);
			if (craftJob == null)
			{
				return;
			}
			CraftPlan craftPlan = CraftTree.FindPlan(comp, sel);
			if (craftPlan != null)
			{
				if (stuff != null)
				{
					craftPlan.allowedStuff = stuff;
				}
				if (style != null)
				{
					craftPlan.styleDef = style;
				}
				if (rootMode == 2)
				{
					craftPlan.SetForever();
				}
				else if (rootMode == 1)
				{
					craftPlan.mode = 1;
					craftPlan.remaining = Mathf.Max(1, num);
					craftPlan.suspended = false;
				}
				else
				{
					craftPlan.SetTarget(Mathf.Max(1, num));
					craftPlan.pauseWhenSatisfied = true;
				}
			}
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_JOB_Submitted", NamedArgument.op_Implicit(((Def)sel).LabelCap), NamedArgument.op_Implicit(craftJob.wanted), NamedArgument.op_Implicit(craftJob.steps.Count))), MessageTypeDefOf.NeutralEvent, false);
			((Window)this).Close(true);
		}
	}
	public class Window_AE2CraftTreeGraph : Window
	{
		private class GNode
		{
			public ThingDef Def;

			public string Label;

			public string CountText;

			public string StateText;

			public Color StateCol = Color.white;

			public float Progress;

			public int Missing;

			public bool Crafting;

			public int DotState;

			public int Depth;

			public int Parent = -1;

			public float X;

			public float Y;
		}

		private readonly CompBillAutomation comp;

		private readonly CraftJob job;

		private readonly RecipeDef rootRecipe;

		private readonly ThingDef rootProduct;

		private int wanted;

		private readonly List<GNode> nodes = new List<GNode>();

		private int builtForWanted = -1;

		private bool builtLive;

		private float scrollX;

		private float scrollY;

		private float zoom = 1f;

		private int selected = -1;

		private bool onlyMissing;

		private static readonly Color AcBlack = Color32.op_Implicit(new Color32((byte)65, (byte)63, (byte)84, byte.MaxValue));

		private static readonly Color AcFrame = Color32.op_Implicit(new Color32((byte)65, (byte)63, (byte)84, byte.MaxValue));

		private static readonly Color AcFill = Color32.op_Implicit(new Color32((byte)154, (byte)159, (byte)180, byte.MaxValue));

		private static readonly Color AcEdge = Color32.op_Implicit(new Color32((byte)173, (byte)176, (byte)196, byte.MaxValue));

		private static readonly Color AcMissing = Color32.op_Implicit(new Color32((byte)139, (byte)36, (byte)30, byte.MaxValue));

		private static readonly Color AcHighlight = Color32.op_Implicit(new Color32((byte)172, (byte)233, byte.MaxValue, byte.MaxValue));

		private static readonly Color AcViewport = Color32.op_Implicit(new Color32((byte)154, (byte)159, (byte)180, byte.MaxValue));

		private const float NodeSize = 44f;

		private const float ColGap = 96f;

		private const float RowGap = 34f;

		public override Vector2 InitialSize => new Vector2(760f, 520f);

		internal Window_AE2CraftTreeGraph(CompBillAutomation comp, CraftJob job)
			: base((IWindowDrawing)null)
		{
			this.comp = comp;
			this.job = job;
			rootRecipe = job?.rootRecipe;
			rootProduct = job?.rootProduct;
			wanted = ((job == null) ? 1 : Mathf.Max(1, job.wanted));
			Init();
		}

		public Window_AE2CraftTreeGraph(CompBillAutomation comp, RecipeDef recipe, ThingDef product, int wanted)
			: base((IWindowDrawing)null)
		{
			this.comp = comp;
			job = null;
			rootRecipe = recipe;
			rootProduct = product;
			this.wanted = Mathf.Max(1, wanted);
			Init();
		}

		private void Init()
		{
			base.doCloseX = false;
			base.closeOnClickedOutside = false;
			base.absorbInputAroundWindow = false;
			base.onlyOneOfTypeAllowed = true;
			base.draggable = true;
			base.resizeable = true;
			base.doWindowBackground = false;
		}

		private static void PipeSeg2H(Rect seg, float pw, Color edge, Color core)
		{
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			if (!(((Rect)(ref seg)).width <= 0f))
			{
				float num = Mathf.Max(1f, pw * 0.5f);
				Widgets.DrawBoxSolid(new Rect(((Rect)(ref seg)).x, ((Rect)(ref seg)).y - num, ((Rect)(ref seg)).width, num), edge);
				Widgets.DrawBoxSolid(new Rect(((Rect)(ref seg)).x, ((Rect)(ref seg)).y, ((Rect)(ref seg)).width, Mathf.Max(1f, pw)), core);
				Widgets.DrawBoxSolid(new Rect(((Rect)(ref seg)).x, ((Rect)(ref seg)).y + Mathf.Max(1f, pw), ((Rect)(ref seg)).width, num), edge);
			}
		}

		private static void PipeSeg2V(Rect seg, float pw, Color edge, Color core)
		{
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			if (!(((Rect)(ref seg)).height <= 0f))
			{
				float num = Mathf.Max(1f, pw * 0.5f);
				Widgets.DrawBoxSolid(new Rect(((Rect)(ref seg)).x - num, ((Rect)(ref seg)).y, num, ((Rect)(ref seg)).height), edge);
				Widgets.DrawBoxSolid(new Rect(((Rect)(ref seg)).x, ((Rect)(ref seg)).y, Mathf.Max(1f, pw), ((Rect)(ref seg)).height), core);
				Widgets.DrawBoxSolid(new Rect(((Rect)(ref seg)).x + Mathf.Max(1f, pw), ((Rect)(ref seg)).y, num, ((Rect)(ref seg)).height), edge);
			}
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0100: Unknown result type (might be due to invalid IL or missing references)
			//IL_0105: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_0151: Unknown result type (might be due to invalid IL or missing references)
			AE2Draw.HandlePauseHotkey();
			try
			{
				if (comp == null || ((ThingComp)comp).parent == null || rootRecipe == null)
				{
					((Window)this).Close(true);
					return;
				}
				if (((Rect)(ref inRect)).width < 380f || ((Rect)(ref inRect)).height < 220f)
				{
					((Window)this).Close(true);
					return;
				}
				if (job != null)
				{
					bool closeClicked;
					Rect ri = AE2Draw.WindowFrame(GenUI.ContractedBy(inRect, 2f), ((object)TranslatorFormattedStringExtensions.Translate("DS_AE2_Tree_Live", NamedArgument.op_Implicit(((Def)rootRecipe).LabelCap), NamedArgument.op_Implicit(job.wanted))/*cast due to constrained. prefix*/).ToString(), out closeClicked);
					if (closeClicked)
					{
						((Window)this).Close(true);
						return;
					}
					BuildLive();
					DrawGraph(ri, live: true);
					return;
				}
				bool closeClicked2;
				Rect ri2 = AE2Draw.WindowFrame(GenUI.ContractedBy(inRect, 2f), ((object)TranslatorFormattedStringExtensions.Translate("DS_AE2_Tree_Title", NamedArgument.op_Implicit(((Def)rootRecipe).LabelCap))/*cast due to constrained. prefix*/).ToString(), out closeClicked2);
				if (closeClicked2)
				{
					((Window)this).Close(true);
					return;
				}
				if (nodes.Count == 0 || builtForWanted != wanted)
				{
					BuildPreview(((Thing)((ThingComp)comp).parent).Map);
					builtForWanted = wanted;
				}
				DrawGraph(ri2, live: false);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 合成树（节点）绘制异常（只记一次）：" + ex, 771007);
			}
		}

		private void BuildPreview(Map map)
		{
			nodes.Clear();
			CraftTreeNode craftTreeNode = CraftTree.Build(comp, rootRecipe, rootProduct, wanted);
			if (craftTreeNode != null)
			{
				Dictionary<ThingDef, int> stock = CraftTree.CoreStock(map);
				AddPreviewNode(craftTreeNode, -1, stock);
				TidyLayout();
			}
		}

		private int AddPreviewNode(CraftTreeNode n, int parent, Dictionary<ThingDef, int> stock)
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_010e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			GNode gNode = new GNode();
			gNode.Def = n.Product;
			gNode.Label = ((n.Product != null) ? ((object)((Def)n.Product).LabelCap/*cast due to constrained. prefix*/).ToString() : "?");
			gNode.CountText = n.RequiredTotal + " ×" + n.Crafts;
			stock.TryGetValue(n.Product, out var value);
			if (n.CoveredByCore)
			{
				gNode.StateText = ((object)TranslatorFormattedStringExtensions.Translate("DS_AE2_Tree_StInCore", NamedArgument.op_Implicit(value))/*cast due to constrained. prefix*/).ToString();
				gNode.StateCol = AE2Draw.Accent;
				gNode.Progress = 1f;
				gNode.DotState = 2;
			}
			else if (n.NoRecipe)
			{
				gNode.StateText = null;
				gNode.StateCol = AE2Draw.Bad;
				gNode.DotState = 3;
			}
			else if (n.CycleCut)
			{
				gNode.StateText = null;
				gNode.StateCol = AE2Draw.Bad;
				gNode.DotState = 5;
			}
			else
			{
				gNode.StateText = null;
				gNode.StateCol = AE2Draw.Warn;
				gNode.DotState = 3;
				gNode.Missing = n.Required;
			}
			gNode.Depth = n.Depth;
			gNode.Parent = parent;
			int count = nodes.Count;
			nodes.Add(gNode);
			for (int i = 0; i < n.Children.Count; i++)
			{
				AddPreviewNode(n.Children[i], count, stock);
			}
			return count;
		}

		private void BuildLive()
		{
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0108: Unknown result type (might be due to invalid IL or missing references)
			//IL_010d: Unknown result type (might be due to invalid IL or missing references)
			//IL_012e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			//IL_015f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0164: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_019e: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
			nodes.Clear();
			if (job == null)
			{
				return;
			}
			for (int i = 0; i < job.steps.Count; i++)
			{
				CraftJobStep craftJobStep = job.steps[i];
				if (craftJobStep != null && craftJobStep.recipe != null)
				{
					GNode gNode = new GNode();
					gNode.Def = craftJobStep.product;
					gNode.Label = ((object)((Def)craftJobStep.recipe).LabelCap/*cast due to constrained. prefix*/).ToString();
					gNode.CountText = craftJobStep.needCount + " ×" + craftJobStep.crafts;
					gNode.Depth = Mathf.Max(0, craftJobStep.depth);
					gNode.Parent = ((craftJobStep.parent >= 0 && craftJobStep.parent < i) ? craftJobStep.parent : (-1));
					CraftPlan craftPlan = CraftTree.FindPlan(comp, craftJobStep.recipe);
					CraftLine craftLine = ((craftPlan != null && craftPlan.lines.Count > 0) ? craftPlan.lines[0] : null);
					if (craftPlan == null)
					{
						gNode.StateText = null;
						gNode.StateCol = AE2Draw.Bad;
						gNode.DotState = 5;
					}
					else if (CompBillAutomation.StepSatisfied(craftPlan))
					{
						gNode.StateText = null;
						gNode.StateCol = AE2Draw.Accent;
						gNode.Progress = 1f;
						gNode.DotState = 2;
					}
					else if (craftPlan.suspended)
					{
						gNode.StateText = null;
						gNode.StateCol = AE2Draw.TextDimCol;
						gNode.DotState = 4;
					}
					else if (craftLine != null && craftLine.HasWork)
					{
						gNode.Crafting = true;
						gNode.Progress = craftLine.Progress01;
						gNode.StateText = null;
						gNode.StateCol = AE2Draw.Accent;
						gNode.DotState = 1;
					}
					else if (craftLine != null && !string.IsNullOrEmpty(craftLine.BlockKey))
					{
						gNode.StateText = null;
						gNode.StateCol = AE2Draw.Bad;
						gNode.DotState = 5;
						gNode.Missing = 1;
					}
					else
					{
						gNode.StateText = null;
						gNode.StateCol = AE2Draw.Warn;
						gNode.DotState = 0;
					}
					nodes.Add(gNode);
				}
			}
			TidyLayout();
		}

		private void TidyLayout()
		{
			if (nodes.Count != 0)
			{
				float next = 0f;
				LayoutSubtree(0, ref next);
			}
		}

		private void LayoutSubtree(int idx, ref float next)
		{
			GNode gNode = nodes[idx];
			gNode.Y = (float)gNode.Depth * 96f;
			int num = -1;
			float num2 = 0f;
			float num3 = 0f;
			bool flag = false;
			for (int i = idx + 1; i < nodes.Count; i++)
			{
				if (nodes[i].Parent == idx)
				{
					if (num < 0)
					{
						num = i;
					}
					LayoutSubtree(i, ref next);
					float x = nodes[i].X;
					if (!flag)
					{
						num2 = x;
						flag = true;
					}
					num3 = x;
				}
			}
			if (num < 0)
			{
				gNode.X = next;
				next += 78f;
				return;
			}
			gNode.X = (num2 + num3) * 0.5f;
			if (next < gNode.X + 44f + 34f)
			{
				next = gNode.X + 44f + 34f;
			}
		}

		private void DrawGraph(Rect ri, bool live)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_025c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0266: Unknown result type (might be due to invalid IL or missing references)
			//IL_026b: Unknown result type (might be due to invalid IL or missing references)
			//IL_010e: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0310: Unknown result type (might be due to invalid IL or missing references)
			//IL_0157: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01be: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0204: Unknown result type (might be due to invalid IL or missing references)
			//IL_0454: Unknown result type (might be due to invalid IL or missing references)
			//IL_0468: Unknown result type (might be due to invalid IL or missing references)
			//IL_046e: Invalid comparison between Unknown and I4
			//IL_047c: Unknown result type (might be due to invalid IL or missing references)
			//IL_063c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0641: Unknown result type (might be due to invalid IL or missing references)
			//IL_0646: Unknown result type (might be due to invalid IL or missing references)
			//IL_065c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0661: Unknown result type (might be due to invalid IL or missing references)
			//IL_0666: Unknown result type (might be due to invalid IL or missing references)
			//IL_0707: Unknown result type (might be due to invalid IL or missing references)
			//IL_070c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0711: Unknown result type (might be due to invalid IL or missing references)
			//IL_06ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_06f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_06d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a53: Unknown result type (might be due to invalid IL or missing references)
			//IL_0744: Unknown result type (might be due to invalid IL or missing references)
			//IL_0749: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a62: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a64: Unknown result type (might be due to invalid IL or missing references)
			//IL_074e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0768: Unknown result type (might be due to invalid IL or missing references)
			//IL_076f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0771: Unknown result type (might be due to invalid IL or missing references)
			//IL_0786: Unknown result type (might be due to invalid IL or missing references)
			//IL_078b: Unknown result type (might be due to invalid IL or missing references)
			//IL_072a: Unknown result type (might be due to invalid IL or missing references)
			//IL_072f: Unknown result type (might be due to invalid IL or missing references)
			//IL_071b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a74: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a76: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b72: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bbf: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ac3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c17: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c1c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c21: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c64: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c69: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c9f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ca4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0cd4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0cd9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d0f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d14: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d68: Unknown result type (might be due to invalid IL or missing references)
			//IL_0da3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0dad: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d59: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d5e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0861: Unknown result type (might be due to invalid IL or missing references)
			//IL_0866: Unknown result type (might be due to invalid IL or missing references)
			//IL_086b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0847: Unknown result type (might be due to invalid IL or missing references)
			//IL_084c: Unknown result type (might be due to invalid IL or missing references)
			//IL_082f: Unknown result type (might be due to invalid IL or missing references)
			//IL_08a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_08ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_08b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_08dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_08e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_08e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0902: Unknown result type (might be due to invalid IL or missing references)
			//IL_0909: Unknown result type (might be due to invalid IL or missing references)
			//IL_090b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0920: Unknown result type (might be due to invalid IL or missing references)
			//IL_0925: Unknown result type (might be due to invalid IL or missing references)
			//IL_088f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0894: Unknown result type (might be due to invalid IL or missing references)
			//IL_0880: Unknown result type (might be due to invalid IL or missing references)
			float y = ((Rect)(ref ri)).y;
			if (AE2Draw.AE2TextButton(new Rect(((Rect)(ref ri)).x, y, 30f, 22f), "−"))
			{
				zoom = Mathf.Clamp(zoom - 0.05f, 0.25f, 3f);
			}
			if (AE2Draw.AE2TextButton(new Rect(((Rect)(ref ri)).x + 34f, y, 30f, 22f), "+"))
			{
				zoom = Mathf.Clamp(zoom + 0.05f, 0.25f, 3f);
			}
			AE2Draw.TextIn(new Rect(((Rect)(ref ri)).x + 70f, y, 200f, 22f), ((object)TranslatorFormattedStringExtensions.Translate("DS_AE2_Tree_Zoom", NamedArgument.op_Implicit((int)(zoom * 100f)))/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextCol, (TextAnchor)3);
			if (!live)
			{
				if (AE2Draw.AE2TextButton(new Rect(((Rect)(ref ri)).x + 180f, y, 30f, 22f), "−1"))
				{
					wanted = Mathf.Max(1, wanted - 1);
					nodes.Clear();
				}
				if (AE2Draw.AE2TextButton(new Rect(((Rect)(ref ri)).x + 214f, y, 30f, 22f), "+1"))
				{
					wanted = Mathf.Min(9999, wanted + 1);
					nodes.Clear();
				}
				AE2Draw.TextIn(new Rect(((Rect)(ref ri)).x + 250f, y, 120f, 22f), ((object)TranslatorFormattedStringExtensions.Translate("DS_AE2_Tree_Count", NamedArgument.op_Implicit(wanted))/*cast due to constrained. prefix*/).ToString(), 1f, AE2Draw.TextCol, (TextAnchor)3);
				if (AE2Draw.AE2TextButton(new Rect(((Rect)(ref ri)).xMax - 132f, y, 132f, 22f), ((object)Translator.Translate("DS_AE2_Tree_Submit")/*cast due to constrained. prefix*/).ToString()) && comp.SubmitJob(rootRecipe, rootProduct, wanted, withIntermediates: true) != null)
				{
					((Window)this).Close(true);
				}
			}
			else if (AE2Draw.AE2TextButton(new Rect(((Rect)(ref ri)).xMax - 132f, y, 132f, 22f), ((object)Translator.Translate("DS_AE2_Tree_CancelJob")/*cast due to constrained. prefix*/).ToString()))
			{
				comp.CancelJob(job);
				((Window)this).Close(true);
			}
			y += 26f;
			Rect val = default(Rect);
			((Rect)(ref val))..ctor(((Rect)(ref ri)).x, y, ((Rect)(ref ri)).width, ((Rect)(ref ri)).yMax - y);
			AE2Draw.FlatColumn(val);
			AE2Draw.TextIn(new Rect(((Rect)(ref val)).x + 5f, ((Rect)(ref val)).y, ((Rect)(ref val)).width - 10f, 24f), ((object)Translator.Translate("DS_AE2_Tree_Btn")/*cast due to constrained. prefix*/).ToString(), 0f, AE2Draw.TextCol, (TextAnchor)3);
			((Rect)(ref val))..ctor(((Rect)(ref val)).x + 5f, ((Rect)(ref val)).y + 27f, ((Rect)(ref val)).width - 10f, ((Rect)(ref val)).height - 32f);
			float num = 0f;
			float num2 = 0f;
			for (int i = 0; i < nodes.Count; i++)
			{
				if (nodes[i].X + 44f > num)
				{
					num = nodes[i].X + 44f;
				}
				if (nodes[i].Y + 44f > num2)
				{
					num2 = nodes[i].Y + 44f;
				}
			}
			num *= zoom;
			num2 *= zoom;
			scrollX = Mathf.Clamp(scrollX, 0f, Mathf.Max(0f, num - ((Rect)(ref val)).width + 20f));
			scrollY = Mathf.Clamp(scrollY, 0f, Mathf.Max(0f, num2 - ((Rect)(ref val)).height + 20f));
			if (Mouse.IsOver(val) && Event.current != null && (int)Event.current.type == 6)
			{
				zoom = Mathf.Clamp(zoom - Event.current.delta.y * 0.05f, 0.25f, 3f);
				Event.current.Use();
			}
			for (int j = 0; j < nodes.Count; j++)
			{
				GNode gNode = nodes[j];
				int num3 = -1;
				int num4 = -1;
				int num5 = 0;
				for (int k = 0; k < nodes.Count; k++)
				{
					if (nodes[k].Parent == j)
					{
						if (num3 < 0 || nodes[k].X < nodes[num3].X)
						{
							num3 = k;
						}
						if (num4 < 0 || nodes[k].X > nodes[num4].X)
						{
							num4 = k;
						}
						num5++;
					}
				}
				if (num5 == 0)
				{
					continue;
				}
				float num6 = Mathf.Max(1f, 2f * zoom);
				float num7 = Mathf.Max(1f, num6 * 0.5f);
				float num8 = Mathf.Round(((Rect)(ref val)).x + 10f - scrollX + (gNode.X + 22f) * zoom);
				float num9 = Mathf.Round(((Rect)(ref val)).y + 10f - scrollY + (gNode.Y + 44f) * zoom);
				float num10 = Mathf.Round(((Rect)(ref val)).y + 10f - scrollY + (gNode.Y + 44f + 26f) * zoom);
				Color val2 = Color32.op_Implicit(new Color32((byte)105, (byte)109, (byte)136, byte.MaxValue));
				Color val3 = Color32.op_Implicit(new Color32((byte)173, (byte)176, (byte)196, byte.MaxValue));
				bool flag = false;
				bool flag2 = false;
				for (int l = 0; l < nodes.Count; l++)
				{
					if (nodes[l].Parent == j)
					{
						if (nodes[l].Crafting)
						{
							flag = true;
						}
						if (nodes[l].Missing > 0)
						{
							flag2 = true;
						}
					}
				}
				Color val4 = (flag2 ? Color32.op_Implicit(new Color32((byte)139, (byte)36, (byte)30, byte.MaxValue)) : (flag ? Color32.op_Implicit(new Color32((byte)156, (byte)211, byte.MaxValue, byte.MaxValue)) : val3));
				Color edge = (flag2 ? Color32.op_Implicit(new Color32((byte)139, (byte)36, (byte)30, byte.MaxValue)) : (flag ? Color32.op_Implicit(new Color32((byte)22, (byte)59, (byte)78, byte.MaxValue)) : val2));
				PipeSeg2V(new Rect(num8, num9, num6, Mathf.Max(1f, num10 - num9 + num7)), num6, edge, val4);
				Widgets.DrawBoxSolid(new Rect(num8 - num7, num10 - num7, num6, num6), val4);
				for (int m = 0; m < nodes.Count; m++)
				{
					if (nodes[m].Parent == j)
					{
						GNode gNode2 = nodes[m];
						float num11 = Mathf.Round(((Rect)(ref val)).x + 10f - scrollX + (gNode2.X + 22f) * zoom);
						float num12 = Mathf.Round(((Rect)(ref val)).y + 10f - scrollY + gNode2.Y * zoom);
						Color val5 = ((gNode2.Missing > 0) ? Color32.op_Implicit(new Color32((byte)139, (byte)36, (byte)30, byte.MaxValue)) : (gNode2.Crafting ? Color32.op_Implicit(new Color32((byte)156, (byte)211, byte.MaxValue, byte.MaxValue)) : val3));
						Color edge2 = ((gNode2.Missing > 0) ? Color32.op_Implicit(new Color32((byte)139, (byte)36, (byte)30, byte.MaxValue)) : (gNode2.Crafting ? Color32.op_Implicit(new Color32((byte)22, (byte)59, (byte)78, byte.MaxValue)) : val2));
						float num13 = Mathf.Min(num8, num11);
						float num14 = Mathf.Max(num8, num11);
						PipeSeg2H(new Rect(num13, num10, Mathf.Max(num6, num14 - num13), num6), num6, edge2, val5);
						PipeSeg2V(new Rect(num11, num10, num6, Mathf.Max(num6, num12 - num10 + num7)), num6, edge2, val5);
						Widgets.DrawBoxSolid(new Rect(num11 - num7, num10 - num7, num6, num6), val5);
					}
				}
			}
			Rect val6 = default(Rect);
			for (int n = 0; n < nodes.Count; n++)
			{
				GNode gNode3 = nodes[n];
				if (gNode3 == null)
				{
					continue;
				}
				float num15 = ((Rect)(ref val)).x + gNode3.X * zoom + 10f - scrollX;
				float num16 = ((Rect)(ref val)).y + gNode3.Y * zoom + 10f - scrollY;
				float num17 = 44f * zoom;
				((Rect)(ref val6))..ctor(num15, num16, num17, num17);
				if (!(((Rect)(ref val6)).xMax < ((Rect)(ref val)).x) && !(((Rect)(ref val6)).x > ((Rect)(ref val)).xMax) && !(((Rect)(ref val6)).yMax < ((Rect)(ref val)).y) && !(((Rect)(ref val6)).y > ((Rect)(ref val)).yMax))
				{
					bool num18 = gNode3.Missing > 0;
					bool crafting = gNode3.Crafting;
					AE2Draw.AE2Button(active: crafting | (gNode3.Progress >= 1f), r: val6, hover: false);
					if (num18)
					{
						AE2Draw.OutlineColor(val6, AE2Draw.Bad);
					}
					else if (crafting)
					{
						AE2Draw.OutlineColor(val6, AE2Draw.Accent);
					}
					if (gNode3.Def != null)
					{
						float num19 = num17 * 0.2f;
						Widgets.DefIcon(new Rect(((Rect)(ref val6)).x + num19, ((Rect)(ref val6)).y + num19 * 0.8f, num17 - num19 * 2f, num17 - num19 * 2f), (Def)(object)gNode3.Def, (ThingDef)null, 1f, (ThingStyleDef)null, false, (Color?)null, (Material)null, (int?)null, 1f);
					}
					float num20 = Mathf.Max(4f, 6f * zoom);
					float num21 = ((Rect)(ref val6)).yMax - num20 - Mathf.Max(2f, 3f * zoom);
					AE2Draw.Bar(new Rect(((Rect)(ref val6)).x + Mathf.Max(3f, 5f * zoom), num21, num17 - 2f * Mathf.Max(3f, 5f * zoom), num20), gNode3.Progress, selected: false);
					float num22 = Mathf.Clamp(11f * zoom, 8f, 20f);
					AE2Draw.GlowDot(new Rect(((Rect)(ref val6)).x + 2f, ((Rect)(ref val6)).y + 2f, num22, num22), gNode3.DotState, pulse: true);
					if (crafting)
					{
						float num23 = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.realtimeSinceStartup * 3.2f));
						byte b = (byte)(110f * num23);
						Color val7 = Color32.op_Implicit(new Color32((byte)156, (byte)211, byte.MaxValue, b));
						float num24 = Mathf.Max(2f, 3f * zoom);
						Widgets.DrawBoxSolid(new Rect(((Rect)(ref val6)).x - num24, ((Rect)(ref val6)).y - num24, ((Rect)(ref val6)).width + 2f * num24, 2f), val7);
						Widgets.DrawBoxSolid(new Rect(((Rect)(ref val6)).x - num24, ((Rect)(ref val6)).yMax + num24 - 2f, ((Rect)(ref val6)).width + 2f * num24, 2f), val7);
						Widgets.DrawBoxSolid(new Rect(((Rect)(ref val6)).x - num24, ((Rect)(ref val6)).y - num24, 2f, ((Rect)(ref val6)).height + 2f * num24), val7);
						Widgets.DrawBoxSolid(new Rect(((Rect)(ref val6)).xMax + num24 - 2f, ((Rect)(ref val6)).y - num24, 2f, ((Rect)(ref val6)).height + 2f * num24), val7);
					}
					if (n == selected)
					{
						AE2Draw.OutlineColor(new Rect(((Rect)(ref val6)).x - 2f, ((Rect)(ref val6)).y - 2f, ((Rect)(ref val6)).width + 4f, ((Rect)(ref val6)).height + 4f), AE2Draw.Accent);
					}
					TooltipHandler.TipRegion(val6, TipSignal.op_Implicit(gNode3.Label + "\n" + gNode3.CountText + "  " + gNode3.StateText));
					if (Widgets.ButtonInvisible(val6, true))
					{
						selected = n;
						scrollX = Mathf.Max(0f, gNode3.X * zoom - ((Rect)(ref val)).width * 0.25f);
						scrollY = Mathf.Max(0f, gNode3.Y * zoom - ((Rect)(ref val)).height * 0.25f);
					}
				}
			}
		}
	}
	public class FloatMenuOptionProvider_DS_Withdraw : FloatMenuOptionProvider
	{
		protected override bool Drafted => false;

		protected override bool Undrafted => true;

		protected override bool Multiselect => false;

		protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
		{
			//IL_0128: Unknown result type (might be due to invalid IL or missing references)
			//IL_014a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0150: Expected O, but got Unknown
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Invalid comparison between Unknown and I4
			Pawn pawn = context.FirstSelectedPawn;
			Pawn obj = pawn;
			if (((obj != null) ? ((Thing)obj).Map : null) == null)
			{
				return null;
			}
			if (!CoreFinder.AnyUsableCoreGlobal(pawn))
			{
				return null;
			}
			Map map = ((Thing)pawn).Map;
			Dictionary<ThingDef, int> availableByDef = new Dictionary<ThingDef, int>();
			List<Thing> list = new List<Thing>();
			HaulSourceContents.GatherAllIncludingRemote(map, list);
			for (int i = 0; i < list.Count; i++)
			{
				Thing val = list[i];
				if (val?.def != null && (int)val.def.category == 2)
				{
					availableByDef.TryGetValue(val.def, out var value);
					availableByDef[val.def] = value + val.stackCount;
				}
			}
			if (availableByDef.Count == 0)
			{
				return null;
			}
			List<ThingDef> defs = new List<ThingDef>(availableByDef.Keys);
			defs.Sort((ThingDef a, ThingDef b) => string.Compare(TaggedString.op_Implicit(((Def)a).LabelCap), TaggedString.op_Implicit(((Def)b).LabelCap), StringComparison.Ordinal));
			return new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("DS_WithdrawToSpot")), (Action)delegate
			{
				//IL_004e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0053: Unknown result type (might be due to invalid IL or missing references)
				//IL_005e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0063: Unknown result type (might be due to invalid IL or missing references)
				//IL_0088: Unknown result type (might be due to invalid IL or missing references)
				//IL_0092: Expected O, but got Unknown
				//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c0: Expected O, but got Unknown
				List<FloatMenuOption> list2 = new List<FloatMenuOption>();
				for (int j = 0; j < defs.Count; j++)
				{
					ThingDef def = defs[j];
					int total = availableByDef[def];
					string text = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_WithdrawItemLabel", NamedArgument.op_Implicit(((Def)def).LabelCap), NamedArgument.op_Implicit(total)));
					list2.Add(new FloatMenuOption(text, (Action)delegate
					{
						//IL_002e: Unknown result type (might be due to invalid IL or missing references)
						Pawn_CarryTracker carryTracker = pawn.carryTracker;
						int maxCarry = ((carryTracker != null) ? carryTracker.AvailableStackSpace(def) : 0);
						Find.WindowStack.Add((Window)(object)new Dialog_WithdrawAmount(TaggedString.op_Implicit(((Def)def).LabelCap), total, maxCarry, delegate(int amount)
						{
							//IL_0063: Unknown result type (might be due to invalid IL or missing references)
							//IL_0068: Unknown result type (might be due to invalid IL or missing references)
							//IL_0076: Unknown result type (might be due to invalid IL or missing references)
							//IL_008d: Unknown result type (might be due to invalid IL or missing references)
							Thing val2 = HaulSourceContents.FindBestIncludingRemote(map, (Thing t) => t.stackCount, (Thing t) => t.def == def);
							if (val2 != null)
							{
								Job val3 = JobMaker.MakeJob(DigitalStorage_JobDefOf.DigitalStorage_WithdrawToSpot, LocalTargetInfo.op_Implicit(context.ClickedCell));
								val3.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(val2));
								IThingHolder parentHolder = val2.ParentHolder;
								val3.SetTarget((TargetIndex)3, LocalTargetInfo.op_Implicit((Thing)(object)((parentHolder is Thing) ? parentHolder : null)));
								val3.count = amount;
								pawn.jobs.TryTakeOrderedJob(val3, (JobTag?)(JobTag)1, false);
							}
						}));
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
				}
				if (list2.Count > 0)
				{
					Find.WindowStack.Add((Window)new FloatMenu(list2));
				}
			}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
		}
	}
	public class FloatMenuOptionProvider_DS_SupplyConstruct : FloatMenuOptionProvider
	{
		protected override bool Drafted => false;

		protected override bool Undrafted => true;

		protected override bool Multiselect => false;

		protected override bool RequiresManipulation => true;

		protected override bool MechanoidCanDo => true;

		protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
		{
			FloatMenuOption result = null;
			try
			{
				result = BuildOption(clickedThing, context);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] SupplyConstruct 右键项构造失败: " + ex, clickedThing.thingIDNumber ^ 0x44534D43);
			}
			return result;
		}

		private static FloatMenuOption BuildOption(Thing clickedThing, FloatMenuContext context)
		{
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_0156: Unknown result type (might be due to invalid IL or missing references)
			//IL_015b: Unknown result type (might be due to invalid IL or missing references)
			//IL_015e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0175: Unknown result type (might be due to invalid IL or missing references)
			//IL_017a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0188: Unknown result type (might be due to invalid IL or missing references)
			//IL_0198: Expected O, but got Unknown
			//IL_0105: Unknown result type (might be due to invalid IL or missing references)
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Unknown result type (might be due to invalid IL or missing references)
			//IL_0134: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_0150: Expected O, but got Unknown
			Pawn pawn = context.FirstSelectedPawn;
			if (pawn == null || !((Thing)pawn).Spawned || ((Thing)pawn).Map == null)
			{
				return null;
			}
			if (!(clickedThing is Blueprint) && !(clickedThing is Frame))
			{
				return null;
			}
			if (clickedThing is Blueprint_Install)
			{
				return null;
			}
			if (!clickedThing.Spawned || clickedThing.Map != ((Thing)pawn).Map)
			{
				return null;
			}
			if (clickedThing.Faction != ((Thing)pawn).Faction)
			{
				return null;
			}
			if (FireUtility.IsBurning(clickedThing))
			{
				return null;
			}
			if (ForbidUtility.IsForbidden(clickedThing, pawn))
			{
				return null;
			}
			IConstructible val = (IConstructible)(object)((clickedThing is IConstructible) ? clickedThing : null);
			if (val == null)
			{
				return null;
			}
			if (!NeedsMaterials(val))
			{
				return null;
			}
			if (!CoreFinder.AnyUsableCoreGlobal(pawn))
			{
				return null;
			}
			if (!ReachabilityUtility.CanReach(pawn, LocalTargetInfo.op_Implicit(clickedThing), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0))
			{
				return null;
			}
			TaggedString val2;
			if (DSConstructionDelivery.TryMakeJob(pawn, val, forced: true, out var job) && job != null)
			{
				val2 = TranslatorFormattedStringExtensions.Translate("DS_FM_SupplyConstruct", NamedArgument.op_Implicit(((Entity)clickedThing).LabelShort));
				return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(TaggedString.op_Implicit(((TaggedString)(ref val2)).CapitalizeFirst()), (Action)delegate
				{
					pawn.jobs.TryTakeOrderedJob(job, (JobTag?)(JobTag)1, false);
				}, (MenuOptionPriority)4, (Action<Rect>)null, clickedThing, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0), pawn, LocalTargetInfo.op_Implicit(clickedThing), "ReservedBy", (ReservationLayerDef)null);
			}
			val2 = Translator.Translate("DS_FM_SupplyConstructBlocked");
			return new FloatMenuOption(TaggedString.op_Implicit(((TaggedString)(ref val2)).CapitalizeFirst()), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, clickedThing, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
			{
				tooltip = new TipSignal(DescribeWhyBlocked(val, clickedThing, pawn))
			};
		}

		private static bool NeedsMaterials(IConstructible c)
		{
			try
			{
				List<ThingDefCountClass> list = c.TotalMaterialCost();
				if (list == null || list.Count == 0)
				{
					return false;
				}
				for (int i = 0; i < list.Count; i++)
				{
					ThingDefCountClass val = list[i];
					if (val?.thingDef != null && val.count > 0 && c.ThingCountNeeded(val.thingDef) > 0)
					{
						return true;
					}
				}
				return false;
			}
			catch
			{
				return false;
			}
		}

		private static string DescribeWhyBlocked(IConstructible c, Thing target, Pawn pawn)
		{
			//IL_0196: Unknown result type (might be due to invalid IL or missing references)
			//IL_0179: Unknown result type (might be due to invalid IL or missing references)
			//IL_017e: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_010c: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			List<string> list = new List<string>();
			try
			{
				List<ThingDefCountClass> list2 = c.TotalMaterialCost();
				if (list2 != null && list2.Count > 0)
				{
					Dictionary<ThingDef, int> dictionary = new Dictionary<ThingDef, int>();
					List<Thing> list3 = new List<Thing>();
					HaulSourceContents.GatherAllIncludingRemote(((Thing)pawn).Map, list3);
					for (int i = 0; i < list3.Count; i++)
					{
						Thing val = list3[i];
						if (val?.def != null && !val.Destroyed)
						{
							dictionary.TryGetValue(val.def, out var value);
							dictionary[val.def] = value + val.stackCount;
						}
					}
					for (int j = 0; j < list2.Count; j++)
					{
						ThingDefCountClass val2 = list2[j];
						if (val2?.thingDef == null || val2.count <= 0)
						{
							continue;
						}
						int num = c.ThingCountNeeded(val2.thingDef);
						if (num > 0)
						{
							dictionary.TryGetValue(val2.thingDef, out var value2);
							if (value2 < num)
							{
								list.Add(TaggedString.op_Implicit(((Def)val2.thingDef).LabelCap + " ") + (num - value2));
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.WarningOnce("[DigitalStorage] 取料诊断失败: " + ex.Message, 1146309956);
			}
			if (list.Count > 0)
			{
				stringBuilder.Append(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_FM_SupplyShort", NamedArgument.op_Implicit(GenText.ToCommaList((IEnumerable<string>)list, true, false)))));
			}
			else
			{
				stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("DS_FM_SupplyReserved")));
			}
			Map map = ((Thing)pawn).Map;
			object obj;
			if (map == null)
			{
				obj = null;
			}
			else
			{
				ReservationManager reservationManager = map.reservationManager;
				obj = ((reservationManager != null) ? reservationManager.FirstRespectedReserver(LocalTargetInfo.op_Implicit(target), pawn, (ReservationLayerDef)null) : null);
			}
			Pawn val3 = (Pawn)obj;
			if (val3 != null)
			{
				stringBuilder.Append("\n").Append(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("ReservedBy", NamedArgument.op_Implicit(((Entity)val3).LabelShort), NamedArgument.op_Implicit((Thing)(object)val3))));
			}
			return stringBuilder.ToString();
		}
	}
}
namespace DigitalStorage.Backpack
{
	public class HediffCompProperties_Backpack : HediffCompProperties
	{
		public int capacityStacks = 8;

		public HediffCompProperties_Backpack()
		{
			base.compClass = typeof(HediffComp_Backpack);
		}
	}
	public class HediffComp_Backpack : HediffComp, IThingHolder
	{
		private ThingOwner backpack;

		public HediffCompProperties_Backpack Props => (HediffCompProperties_Backpack)(object)base.props;

		public IThingHolder ParentHolder => (IThingHolder)(object)((HediffComp)this).Pawn;

		public int Count => GetDirectlyHeldThings().Count;

		public ThingOwner GetDirectlyHeldThings()
		{
			if (backpack == null)
			{
				backpack = (ThingOwner)(object)new ThingOwner<Thing>((IThingHolder)(object)this);
			}
			return backpack;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, (IList<Thing>)GetDirectlyHeldThings());
		}

		public static HediffComp_Backpack For(Pawn pawn)
		{
			if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
			{
				return null;
			}
			List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
			for (int i = 0; i < hediffs.Count; i++)
			{
				Hediff obj = hediffs[i];
				HediffWithComps val = (HediffWithComps)(object)((obj is HediffWithComps) ? obj : null);
				if (val == null || val.comps == null)
				{
					continue;
				}
				for (int j = 0; j < val.comps.Count; j++)
				{
					if (val.comps[j] is HediffComp_Backpack result)
					{
						return result;
					}
				}
			}
			return null;
		}

		public Thing TryAbsorb(Thing source, int count)
		{
			if (source == null || source.Destroyed || count <= 0)
			{
				return null;
			}
			if (((HediffComp)this).Pawn == null)
			{
				return null;
			}
			ThingOwner directlyHeldThings = GetDirectlyHeldThings();
			if ((object)source.ParentHolder == this)
			{
				return source;
			}
			ThingOwner val = source.holdingOwner;
			if (val == null || val.Owner is Map)
			{
				IThingHolder parentHolder = source.ParentHolder;
				val = ((parentHolder == null) ? null : parentHolder.GetDirectlyHeldThings());
			}
			if (val == null || val.Owner is Map || !val.Contains(source))
			{
				return null;
			}
			if (val.Owner is Building_StorageCore { IsUsableNow: false })
			{
				return null;
			}
			int num = ((count < source.stackCount) ? count : source.stackCount);
			if (num <= 0)
			{
				return null;
			}
			if (!CanFit(source))
			{
				return null;
			}
			Thing val2;
			if (num >= source.stackCount)
			{
				val.Remove(source);
				val2 = source;
			}
			else
			{
				val2 = source.SplitOff(num);
			}
			if (val2 == null)
			{
				return null;
			}
			if (directlyHeldThings.TryAdd(val2, false))
			{
				return val2;
			}
			Return(val, val2);
			return null;
		}

		private bool CanFit(Thing t)
		{
			ThingOwner directlyHeldThings = GetDirectlyHeldThings();
			if (directlyHeldThings.Contains(t))
			{
				return true;
			}
			return directlyHeldThings.Count < Props.capacityStacks;
		}

		private void Return(ThingOwner owner, Thing taken)
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			if (taken == null || taken.Destroyed || (owner != null && owner.TryAdd(taken, true)))
			{
				return;
			}
			Pawn pawn = ((HediffComp)this).Pawn;
			Map val = ((pawn == null) ? null : ((Thing)pawn).MapHeld);
			if (val != null)
			{
				IntVec3 positionHeld = ((Thing)pawn).PositionHeld;
				if (((IntVec3)(ref positionHeld)).IsValid && GenPlace.TryPlaceThing(taken, ((Thing)pawn).PositionHeld, val, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
				{
					return;
				}
			}
			if (!GetDirectlyHeldThings().TryAdd(taken, false))
			{
				Log.Error("[DigitalStorage] 背包：物品退不回原容器也放不下：" + (object)taken);
			}
		}

		public int ReturnContentsToCore()
		{
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			ThingOwner directlyHeldThings = GetDirectlyHeldThings();
			if (directlyHeldThings.Count == 0)
			{
				return 0;
			}
			Pawn pawn = ((HediffComp)this).Pawn;
			Map val = ((pawn == null) ? null : ((Thing)pawn).MapHeld);
			if (val == null)
			{
				return 0;
			}
			Building_StorageCore building_StorageCore = FindNearestCore(val, ((Thing)pawn).PositionHeld);
			if (building_StorageCore == null)
			{
				building_StorageCore = FindNearestCoreGlobal(((Thing)pawn).PositionHeld);
			}
			if (building_StorageCore == null)
			{
				return 0;
			}
			int num = 0;
			for (int num2 = directlyHeldThings.Count - 1; num2 >= 0; num2--)
			{
				Thing val2 = directlyHeldThings[num2];
				if (val2 != null)
				{
					num += building_StorageCore.TryStore(val2, val2.stackCount);
				}
			}
			return num;
		}

		private static Building_StorageCore FindNearestCore(Map map, IntVec3 from)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return Nearest(CoreFinder.AllUsableCores(map), from);
		}

		private static Building_StorageCore FindNearestCoreGlobal(IntVec3 from)
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return Nearest(CoreFinder.AllUsableCoresGlobal(), from);
		}

		private static Building_StorageCore Nearest(List<Building_StorageCore> cores, IntVec3 from)
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			Building_StorageCore building_StorageCore = null;
			int num = int.MaxValue;
			for (int i = 0; i < cores.Count; i++)
			{
				Building_StorageCore building_StorageCore2 = cores[i];
				if (building_StorageCore2 != null)
				{
					IntVec3 val = ((Thing)building_StorageCore2).Position - from;
					int lengthHorizontalSquared = ((IntVec3)(ref val)).LengthHorizontalSquared;
					if (building_StorageCore == null || lengthHorizontalSquared < num)
					{
						building_StorageCore = building_StorageCore2;
						num = lengthHorizontalSquared;
					}
				}
			}
			return building_StorageCore;
		}

		public void EjectAll()
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			ThingOwner directlyHeldThings = GetDirectlyHeldThings();
			if (directlyHeldThings.Count == 0)
			{
				return;
			}
			Pawn pawn = ((HediffComp)this).Pawn;
			Map val = ((pawn == null) ? null : ((Thing)pawn).MapHeld);
			if (val != null)
			{
				IntVec3 positionHeld = ((Thing)pawn).PositionHeld;
				if (((IntVec3)(ref positionHeld)).IsValid)
				{
					directlyHeldThings.TryDropAll(((Thing)pawn).PositionHeld, val, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, true);
				}
			}
			if (directlyHeldThings.Count == 0 || pawn == null || pawn.inventory == null || pawn.inventory.innerContainer == null)
			{
				return;
			}
			for (int num = directlyHeldThings.Count - 1; num >= 0; num--)
			{
				Thing val2 = directlyHeldThings[num];
				if (val2 != null)
				{
					Thing val3 = directlyHeldThings.Take(val2, val2.stackCount);
					if (val3 != null && !((ThingOwner)pawn.inventory.innerContainer).TryAdd(val3, true))
					{
						Return(directlyHeldThings, val3);
					}
				}
			}
		}

		public override void CompPostTickInterval(ref float severityAdjustment, int delta)
		{
			((HediffComp)this).CompPostTickInterval(ref severityAdjustment, delta);
			if (backpack == null || backpack.Count == 0)
			{
				return;
			}
			try
			{
				Pawn pawn = ((HediffComp)this).Pawn;
				if (pawn == null)
				{
					return;
				}
				if (pawn.Dead || pawn.Downed)
				{
					ReturnContentsToCore();
					if (Count > 0)
					{
						EjectAll();
					}
				}
				else
				{
					Job val = ((pawn.jobs == null) ? null : pawn.jobs.curJob);
					if (val == null || val.def != JobDefOf.DoBill)
					{
						ReturnContentsToCore();
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 背包清理失败（物品仍在背包里，不会丢）：" + ex, 1146307155);
			}
		}

		public override void CompExposeData()
		{
			((HediffComp)this).CompExposeData();
			Scribe_Deep.Look<ThingOwner>(ref backpack, "dsBackpack", new object[1] { this });
		}

		public override void CompPostMake()
		{
			((HediffComp)this).CompPostMake();
			if (backpack == null)
			{
				backpack = (ThingOwner)(object)new ThingOwner<Thing>((IThingHolder)(object)this);
			}
		}

		public override void CompPostPostAdd(DamageInfo? dinfo)
		{
			((HediffComp)this).CompPostPostAdd(dinfo);
			if (backpack == null)
			{
				backpack = (ThingOwner)(object)new ThingOwner<Thing>((IThingHolder)(object)this);
			}
		}

		public override void CompPostPostRemoved()
		{
			((HediffComp)this).CompPostPostRemoved();
			try
			{
				EjectAll();
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 背包移除时清空失败（物品仍在背包对象里）：" + ex, 1146307156);
			}
		}
	}
	internal static class BackpackImplant
	{
		private const string HediffDefName = "DS_CoreBackpack";

		private static HediffDef def;

		private static bool defResolved;

		private static HediffDef Def
		{
			get
			{
				if (!defResolved)
				{
					def = DefDatabase<HediffDef>.GetNamedSilentFail("DS_CoreBackpack");
					defResolved = true;
				}
				return def;
			}
		}

		internal static bool ShouldHave(Pawn pawn)
		{
			if (pawn == null || ((Thing)pawn).def == null || pawn.RaceProps == null)
			{
				return false;
			}
			if (pawn.health == null || pawn.health.hediffSet == null)
			{
				return false;
			}
			if (pawn.Dead)
			{
				return false;
			}
			if (((Thing)pawn).Faction != Faction.OfPlayer)
			{
				return false;
			}
			if (pawn.IsPrisoner)
			{
				return false;
			}
			if (pawn.RaceProps.Humanlike)
			{
				return true;
			}
			if (pawn.RaceProps.IsMechanoid)
			{
				return DigitalStorageSettings.backpackForMechanoids;
			}
			return false;
		}

		internal static void Sync(Pawn pawn)
		{
			try
			{
				HediffDef val = Def;
				if (val == null || pawn == null || pawn.health == null || pawn.health.hediffSet == null)
				{
					return;
				}
				Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(val, false);
				if (ShouldHave(pawn))
				{
					if (firstHediffOfDef == null)
					{
						pawn.health.AddHediff(val, (BodyPartRecord)null, (DamageInfo?)null, (DamageResult)null);
						LogChange(pawn, added: true);
					}
				}
				else if (firstHediffOfDef != null)
				{
					pawn.health.RemoveHediff(firstHediffOfDef);
					LogChange(pawn, added: false);
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 挂/摘数字存储背包失败（这台 pawn 跳过，其余照常）：" + ex, 1146307152);
			}
		}

		private static void LogChange(Pawn pawn, bool added)
		{
			if (DigitalStorageSettings.enableDebugLog && pawn != null)
			{
				string labelShortCap = ((Entity)pawn).LabelShortCap;
				string text = ((((Thing)pawn).Faction == null) ? "无派系" : ((Thing)pawn).Faction.Name);
				Log.Warning("[DS] 数字存储背包 " + (added ? "挂上" : "摘掉") + "：" + labelShortCap + "（阵营=" + text + (pawn.IsPrisoner ? "，囚犯" : (pawn.IsSlave ? "，奴隶" : "")) + "）");
			}
		}
	}
	[HarmonyPatch(typeof(Pawn), "SpawnSetup")]
	internal static class Patch_AutoImplantBackpack
	{
		[HarmonyPostfix]
		private static void Postfix(Pawn __instance)
		{
			if (__instance != null)
			{
				BackpackImplant.Sync(__instance);
			}
		}
	}
	[HarmonyPatch(typeof(Pawn_GuestTracker), "SetGuestStatus")]
	internal static class Patch_BackpackOnGuestStatusChanged
	{
		[HarmonyPostfix]
		private static void Postfix(Pawn ___pawn)
		{
			if (___pawn != null)
			{
				BackpackImplant.Sync(___pawn);
			}
		}
	}
	[HarmonyPatch(typeof(Pawn), "SetFaction")]
	internal static class Patch_BackpackOnFactionChanged
	{
		[HarmonyPostfix]
		private static void Postfix(Pawn __instance)
		{
			if (__instance != null)
			{
				BackpackImplant.Sync(__instance);
			}
		}
	}
	[HarmonyPatch(typeof(Pawn), "Kill")]
	internal static class Patch_BackpackEjectOnDeath
	{
		[HarmonyPostfix]
		private static void Postfix(Pawn __instance)
		{
			try
			{
				HediffComp_Backpack hediffComp_Backpack = HediffComp_Backpack.For(__instance);
				if (hediffComp_Backpack != null && hediffComp_Backpack.Count != 0)
				{
					hediffComp_Backpack.ReturnContentsToCore();
					if (hediffComp_Backpack.Count > 0)
					{
						hediffComp_Backpack.EjectAll();
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 死亡时清空背包失败（物品仍留在背包里，不会丢）：" + ex, 1146307153);
			}
		}
	}
	[HarmonyPatch(typeof(JobDriver_DoBill), "MakeNewToils")]
	internal static class Patch_JobDriver_DoBill_LoadIngredients
	{
		[HarmonyPostfix]
		private static void Postfix(ref IEnumerable<Toil> __result)
		{
			if (__result != null)
			{
				__result = Prepend(__result);
			}
		}

		private static IEnumerable<Toil> Prepend(IEnumerable<Toil> original)
		{
			yield return MakeLoadToil();
			foreach (Toil item in original)
			{
				yield return item;
			}
		}

		private static Toil MakeLoadToil()
		{
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			Toil toil = ToilMaker.MakeToil("DS_LoadBillIngredients");
			toil.defaultCompleteMode = (ToilCompleteMode)1;
			toil.initAction = delegate
			{
				LoadIngredients(toil);
			};
			return toil;
		}

		private static void LoadIngredients(Toil toil)
		{
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0215: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Pawn actor = toil.actor;
				if (actor == null)
				{
					return;
				}
				HediffComp_Backpack hediffComp_Backpack = HediffComp_Backpack.For(actor);
				if (hediffComp_Backpack == null)
				{
					return;
				}
				Job val = ((actor.jobs == null) ? null : actor.jobs.curJob);
				if (val == null)
				{
					return;
				}
				List<LocalTargetInfo> targetQueue = val.GetTargetQueue((TargetIndex)2);
				if (GenList.NullOrEmpty<LocalTargetInfo>((IList<LocalTargetInfo>)targetQueue))
				{
					return;
				}
				List<int> countQueue = val.countQueue;
				if (hediffComp_Backpack.Count > 0)
				{
					hediffComp_Backpack.ReturnContentsToCore();
				}
				LocalTargetInfo val2 = val.GetTarget((TargetIndex)1);
				Thing thing = ((LocalTargetInfo)(ref val2)).Thing;
				ThingOwner val3 = ((thing == null) ? null : ThingOwnerUtility.TryGetInnerInteractableThingOwner(thing));
				for (int i = 0; i < targetQueue.Count; i++)
				{
					val2 = targetQueue[i];
					Thing thing2 = ((LocalTargetInfo)(ref val2)).Thing;
					if (thing2 == null || thing2.Destroyed || thing2.Spawned || !(thing2.ParentHolder is Building_StorageCore building_StorageCore) || (val3 != null && val3.Contains(thing2)))
					{
						continue;
					}
					if (!building_StorageCore.IsUsableNow)
					{
						actor.jobs.curDriver.EndJobWith((JobCondition)4);
						break;
					}
					if (countQueue == null || i >= countQueue.Count)
					{
						continue;
					}
					Thing val4 = hediffComp_Backpack.TryAbsorb(thing2, countQueue[i]);
					if (val4 == null)
					{
						if (!building_StorageCore.IsUsableNow || ((Thing)building_StorageCore).Map != ((Thing)actor).Map)
						{
							if (DigitalStorageSettings.enableDebugLog)
							{
								Log.Warning("[DS] 跨图取料：源已被截走（" + ((Def)thing2.def).defName + " @ " + ((object)((Thing)building_StorageCore).Map)?.ToString() + "），本作业放弃，等待下轮重找");
							}
							actor.jobs.curDriver.EndJobWith((JobCondition)4);
							break;
						}
					}
					else if (val4 != thing2)
					{
						targetQueue[i] = new LocalTargetInfo(val4);
						ReservationUtility.Reserve(actor, new LocalTargetInfo(val4), val, 1, -1, (ReservationLayerDef)null, false, false);
						Map map = ((Thing)actor).Map;
						if (map != null && map.reservationManager.ReservedBy(LocalTargetInfo.op_Implicit(thing2), actor, val))
						{
							map.reservationManager.Release(LocalTargetInfo.op_Implicit(thing2), actor, val);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] bill 取料（背包）失败，本作业退回原版路径：" + ex, 1146307154);
			}
		}
	}
}
namespace DigitalStorage.Effects
{
	[StaticConstructorOnStartup]
	public class Mote_DS_IngestBeam : Mote
	{
		private const int GrowTicks = 40;

		private const int FootDuration = 41;

		private const float MaxHeight = 14f;

		private const float BeamWidth = 1.15f;

		private const float FootSize = 0.8f;

		private static readonly Material BeamMat = MaterialPool.MatFrom("Other/OrbitalBeam", ShaderDatabase.MoteGlow, MapMaterialRenderQueues.OrbitalBeam);

		private static readonly Material BeamEndMat = MaterialPool.MatFrom("Other/OrbitalBeamEnd", ShaderDatabase.MoteGlow, MapMaterialRenderQueues.OrbitalBeam);

		private readonly MaterialPropertyBlock[] blocks = (MaterialPropertyBlock[])(object)new MaterialPropertyBlock[3];

		private static readonly Color BeamColor = new Color(0.54f, 0.5f, 0.9f, 0.65f);

		private float angle;

		private MaterialPropertyBlock Block(int index)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Expected O, but got Unknown
			if (blocks[index] == null)
			{
				blocks[index] = new MaterialPropertyBlock();
			}
			return blocks[index];
		}

		public void Init(float angle)
		{
			this.angle = angle;
		}

		protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_017e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0183: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01de: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0201: Unknown result type (might be due to invalid IL or missing references)
			//IL_0210: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0100: Unknown result type (might be due to invalid IL or missing references)
			//IL_0114: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			//IL_0138: Unknown result type (might be due to invalid IL or missing references)
			if (((Thing)this).Destroyed || Find.UIRoot.HideMotes)
			{
				return;
			}
			int num = Find.TickManager.TicksGame - base.spawnTick;
			float num2 = Mathf.Clamp01((float)num / 40f);
			float num3 = 14f * num2;
			float num4 = 0.92f + Mathf.Sin((float)num * 0.35f) * 0.08f;
			float num5 = ((Mote)this).Alpha * num4;
			if (num5 <= 0.01f)
			{
				return;
			}
			float num6 = Altitudes.AltitudeFor((AltitudeLayer)39);
			if (num3 > 0.05f)
			{
				Color beamColor = BeamColor;
				beamColor.a *= num5;
				Vector3 val = default(Vector3);
				((Vector3)(ref val))..ctor(base.exactPosition.x, num6 + num3 * 0.5f, base.exactPosition.z);
				for (int i = 0; i < 2; i++)
				{
					MaterialPropertyBlock val2 = Block(i);
					val2.SetColor(ShaderPropertyIDs.Color, beamColor);
					Matrix4x4 val3 = default(Matrix4x4);
					((Matrix4x4)(ref val3)).SetTRS(val, Quaternion.Euler(0f, angle + (float)i * 90f, 0f) * Quaternion.Euler(90f, 0f, 0f), new Vector3(1.15f, 1f, num3));
					Graphics.DrawMesh(MeshPool.plane10, val3, BeamMat, 0, (Camera)null, 0, val2);
				}
			}
			float num7 = Mathf.Clamp01((float)num / 40f);
			if (num < 41)
			{
				float num8 = 1f - (float)num / 41f;
				Color beamColor2 = BeamColor;
				beamColor2.a *= ((Mote)this).Alpha * num4 * num8;
				MaterialPropertyBlock val4 = Block(2);
				val4.SetColor(ShaderPropertyIDs.Color, beamColor2);
				Matrix4x4 val5 = default(Matrix4x4);
				float num9 = 0.8f * num7;
				((Matrix4x4)(ref val5)).SetTRS(new Vector3(base.exactPosition.x, num6, base.exactPosition.z), Quaternion.Euler(0f, angle, 0f), new Vector3(num9, 1f, num9));
				Graphics.DrawMesh(MeshPool.plane10, val5, BeamEndMat, 0, (Camera)null, 0, val4);
			}
		}

		public override void ExposeData()
		{
			((Thing)this).ExposeData();
			Scribe_Values.Look<float>(ref angle, "dsBeamAngle", 0f, false);
		}
	}
	public class Mote_DS_WorkHand : Mote
	{
		private const int StrikeTicks = 12;

		private const float StrikeDip = 0.3f;

		private int lastStrikeTick = -99999;

		private float baseYOffset;

		protected override bool EndOfLife => false;

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Mote)this).SpawnSetup(map, respawningAfterLoad);
			baseYOffset = base.yOffset;
		}

		public void Strike()
		{
			lastStrikeTick = Find.TickManager.TicksGame;
		}

		protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			int ticksGame = Find.TickManager.TicksGame;
			int num = ticksGame - lastStrikeTick;
			float num2 = Mathf.Sin((float)(ticksGame + base.offsetRandom) * 0.1f) * 0.045f;
			float num3 = 0f;
			float num4 = 0f;
			if (num >= 0 && num < 12)
			{
				float num5 = (float)num / 12f;
				float num6 = Mathf.Sin(num5 * (float)Math.PI);
				num2 -= 0.3f * num6;
				num4 += 0.1f * num6;
				num3 += Mathf.Sin(num5 * (float)Math.PI * 2f) * 0.05f;
			}
			base.yOffset = baseYOffset + num2;
			base.exactPosition.x += num3;
			base.exactPosition.z += num4;
			((Mote)this).DrawAt(drawLoc, flip);
			base.exactPosition.x -= num3;
			base.exactPosition.z -= num4;
		}
	}
}
namespace DigitalStorage.AI
{
	[DefOf]
	public static class DigitalStorage_JobDefOf
	{
		public static JobDef DigitalStorage_WithdrawToConstruction;

		public static JobDef DigitalStorage_WithdrawToSpot;

		public static JobDef DigitalStorage_Consume;

		public static JobDef DigitalStorage_EmptyWasteToCore;

		static DigitalStorage_JobDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(DigitalStorage_JobDefOf));
		}
	}
	public class JobDriver_DS_Withdraw : JobDriver
	{
		private Thing SourceThing
		{
			get
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = base.job.GetTarget((TargetIndex)2);
				return ((LocalTargetInfo)(ref target)).Thing;
			}
		}

		private Building_StorageCore TargetCore
		{
			get
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = base.job.GetTarget((TargetIndex)3);
				return ((LocalTargetInfo)(ref target)).Thing as Building_StorageCore;
			}
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			EnsurePlanned();
			int num = ((base.job.count > 0) ? base.job.count : (-1));
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing != null && !thing.Destroyed)
			{
				ReservationUtility.Reserve(base.pawn, LocalTargetInfo.op_Implicit(thing), base.job, 1, -1, (ReservationLayerDef)null, false, false);
			}
			Thing sourceThing = SourceThing;
			if (sourceThing != null && !sourceThing.Destroyed && sourceThing.MapHeld == ((Thing)base.pawn).Map)
			{
				ReservationUtility.Reserve(base.pawn, LocalTargetInfo.op_Implicit(sourceThing), base.job, 1, num, (ReservationLayerDef)null, false, false);
			}
			return true;
		}

		public override void Notify_Starting()
		{
			((JobDriver)this).Notify_Starting();
			EnsurePlanned();
			if (base.job.count <= 0 || SourceThing == null)
			{
				((JobDriver)this).EndJobWith((JobCondition)4);
			}
		}

		private void EnsurePlanned()
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			if (base.job.count > 0 && SourceThing != null)
			{
				return;
			}
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			IConstructible val = (IConstructible)(object)((thing is IConstructible) ? thing : null);
			if (val != null)
			{
				(Thing, int)? tuple = ConstructMaterialPlanner.TryPlan(val, base.pawn, base.job.playerForced, ((Thing)base.pawn).Map);
				if (tuple.HasValue)
				{
					Thing item = tuple.Value.Item1;
					base.job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(item));
					Job job = base.job;
					IThingHolder parentHolder = item.ParentHolder;
					job.SetTarget((TargetIndex)3, LocalTargetInfo.op_Implicit((Thing)(object)((parentHolder is Thing) ? parentHolder : null)));
					base.job.count = tuple.Value.Item2;
				}
			}
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			ToilFailConditions.FailOn<JobDriver_DS_Withdraw>(this, (Func<bool>)delegate
			{
				Thing sourceThing = SourceThing;
				return sourceThing == null || sourceThing.Destroyed;
			});
			ToilFailConditions.FailOn<JobDriver_DS_Withdraw>(this, (Func<bool>)delegate
			{
				Building_StorageCore targetCore = TargetCore;
				if (targetCore != null)
				{
					Thing sourceThing = SourceThing;
					if ((object)((sourceThing != null) ? sourceThing.ParentHolder : null) == targetCore)
					{
						return !targetCore.IsUsableNow;
					}
				}
				return false;
			});
			yield return GuardDest();
			yield return WithdrawToHand();
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
			if (((LocalTargetInfo)(ref target)).IsValid)
			{
				yield return Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)3, false);
			}
			yield return RemoveExistingFloor();
			yield return Toils_Construct.MakeSolidThingFromBlueprintIfNecessary((TargetIndex)1, (TargetIndex)0);
			yield return PlaceHauled();
		}

		private Toil GuardDest()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			Toil obj = ToilMaker.MakeToil("DS_GuardDest");
			obj.defaultCompleteMode = (ToilCompleteMode)1;
			obj.initAction = delegate
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0040: Unknown result type (might be due to invalid IL or missing references)
				//IL_0055: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
				if (!((LocalTargetInfo)(ref target)).IsValid)
				{
					((JobDriver)this).EndJobWith((JobCondition)4);
				}
				else
				{
					Thing thing = ((LocalTargetInfo)(ref target)).Thing;
					if (thing != null)
					{
						if (thing.Destroyed)
						{
							((JobDriver)this).EndJobWith((JobCondition)4);
						}
						else if (!ReservationUtility.HasReserved(base.pawn, target, base.job) && !ReservationUtility.Reserve(base.pawn, target, base.job, 1, -1, (ReservationLayerDef)null, false, false))
						{
							((JobDriver)this).EndJobWith((JobCondition)4);
						}
					}
				}
			};
			return obj;
		}

		private Toil RemoveExistingFloor()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			Toil obj = ToilMaker.MakeToil("DS_RemoveExistingFloor");
			obj.defaultCompleteMode = (ToilCompleteMode)1;
			obj.initAction = delegate
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				try
				{
					LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
					Thing thing = ((LocalTargetInfo)(ref target)).Thing;
					IConstructible val = (IConstructible)(object)((thing is IConstructible) ? thing : null);
					if (val != null && DSConstructionDelivery.RemoveExistingFloorFirstIfNeeded(val, ((Thing)base.pawn).Map) && (thing.Destroyed || !thing.Spawned))
					{
						((JobDriver)this).EndJobWith((JobCondition)4);
					}
				}
				catch (Exception ex)
				{
					Log.ErrorOnce("[DigitalStorage] 地形蓝图旧地板清理失败（本格退回原版行为）：" + ex, 382255);
				}
			};
			return obj;
		}

		private Toil WithdrawToHand()
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			Toil toil = ToilMaker.MakeToil("DS_WithdrawToHand");
			toil.defaultCompleteMode = (ToilCompleteMode)1;
			toil.initAction = delegate
			{
				//IL_018c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0149: Unknown result type (might be due to invalid IL or missing references)
				Pawn actor = toil.actor;
				Thing sourceThing = SourceThing;
				if (sourceThing == null || sourceThing.Destroyed)
				{
					((JobDriver)this).EndJobWith((JobCondition)4);
				}
				else
				{
					IThingHolder parentHolder = sourceThing.ParentHolder;
					ThingOwner val = ((parentHolder != null) ? parentHolder.GetDirectlyHeldThings() : null);
					if (val == null || !val.Contains(sourceThing))
					{
						((JobDriver)this).EndJobWith((JobCondition)4);
					}
					else if (parentHolder is Building_StorageCore { IsUsableNow: false })
					{
						((JobDriver)this).EndJobWith((JobCondition)4);
					}
					else
					{
						int num = actor.carryTracker.AvailableStackSpace(sourceThing.def);
						if (num <= 0)
						{
							((JobDriver)this).EndJobWith((JobCondition)4);
						}
						else
						{
							int num2 = Math.Min(Math.Min(base.job.count, num), sourceThing.stackCount);
							if (num2 <= 0)
							{
								((JobDriver)this).EndJobWith((JobCondition)4);
							}
							else
							{
								Thing val2;
								if (num2 >= sourceThing.stackCount)
								{
									val.Remove(sourceThing);
									val2 = sourceThing;
								}
								else
								{
									val2 = sourceThing.SplitOff(num2);
								}
								if (val2 == null)
								{
									((JobDriver)this).EndJobWith((JobCondition)4);
								}
								else
								{
									int stackCount = val2.stackCount;
									if (actor.carryTracker.TryStartCarry(val2, stackCount, false) < stackCount)
									{
										if (!val2.Destroyed && val2.holdingOwner == null && !val.TryAdd(val2, true))
										{
											GenPlace.TryPlaceThing(val2, ((Thing)actor).Position, ((Thing)actor).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
										}
										((JobDriver)this).EndJobWith((JobCondition)4);
									}
									else
									{
										base.job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(actor.carryTracker.CarriedThing));
									}
								}
							}
						}
					}
				}
			};
			return toil;
		}

		private Toil PlaceHauled()
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			Toil toil = ToilMaker.MakeToil("DS_PlaceHauled");
			toil.defaultCompleteMode = (ToilCompleteMode)1;
			toil.initAction = delegate
			{
				//IL_0028: Unknown result type (might be due to invalid IL or missing references)
				//IL_002d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0099: Unknown result type (might be due to invalid IL or missing references)
				//IL_009e: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
				//IL_0140: Unknown result type (might be due to invalid IL or missing references)
				//IL_0134: Unknown result type (might be due to invalid IL or missing references)
				//IL_0139: Unknown result type (might be due to invalid IL or missing references)
				//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
				//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
				//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
				//IL_010d: Unknown result type (might be due to invalid IL or missing references)
				//IL_010f: Unknown result type (might be due to invalid IL or missing references)
				Pawn actor = toil.actor;
				Thing carriedThing = actor.carryTracker.CarriedThing;
				if (carriedThing != null)
				{
					LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
					Thing thing = ((LocalTargetInfo)(ref target)).Thing;
					if (thing is IConstructible)
					{
						ThingOwner val = ThingOwnerUtility.TryGetInnerInteractableThingOwner(thing);
						if (val != null && val.CanAcceptAnyOf(carriedThing, true))
						{
							Thing val2 = actor.carryTracker.innerContainer.Take(carriedThing, carriedThing.stackCount);
							if (val2 != null && val.TryAdd(val2, true))
							{
								return;
							}
							if (val2 != null)
							{
								((ThingOwner)actor.carryTracker.innerContainer).TryAdd(val2, true);
							}
						}
					}
					IntVec3 val3 = ((Thing)actor).Position;
					LocalTargetInfo target2 = base.job.GetTarget((TargetIndex)1);
					if (((LocalTargetInfo)(ref target2)).HasThing)
					{
						Thing thing2 = ((LocalTargetInfo)(ref target2)).Thing;
						val3 = thing2.Position;
						IBillGiver val4 = (IBillGiver)(object)((thing2 is IBillGiver) ? thing2 : null);
						if (val4 != null)
						{
							foreach (IntVec3 ingredientStackCell in val4.IngredientStackCells)
							{
								if (GenGrid.InBounds(ingredientStackCell, ((Thing)actor).Map) && GenPlace.HaulPlaceBlockerIn(carriedThing, ingredientStackCell, ((Thing)actor).Map, false) == null)
								{
									val3 = ingredientStackCell;
									break;
								}
							}
						}
					}
					else if (((LocalTargetInfo)(ref target2)).IsValid)
					{
						val3 = ((LocalTargetInfo)(ref target2)).Cell;
					}
					Thing val5 = default(Thing);
					if (actor.carryTracker.TryDropCarriedThing(val3, (ThingPlaceMode)1, ref val5, (Action<Thing, int>)null) && val5 != null)
					{
						CompAutoIngest.MarkWithdrawn(val5);
						if (!(thing is IConstructible))
						{
							ForbidUtility.SetForbidden(val5, true, false);
						}
					}
				}
			};
			return toil;
		}
	}
	public static class ConstructMaterialPlanner
	{
		public static bool CanReserveFor(Thing t, Pawn pawn, int count)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			if (t == null || pawn == null)
			{
				return false;
			}
			if (t.MapHeld != ((Thing)pawn).Map)
			{
				return true;
			}
			return ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(t), 1, (count > 0) ? count : (-1), (ReservationLayerDef)null, false);
		}

		public static (Thing thing, int count)? TryPlan(IConstructible c, Pawn pawn, bool forced, Map map)
		{
			if (c is Blueprint_Install)
			{
				return null;
			}
			if (map == null)
			{
				return null;
			}
			List<ThingDefCountClass> list;
			try
			{
				list = c.TotalMaterialCost();
			}
			catch (Exception ex)
			{
				Log.WarningOnce("[DigitalStorage] TotalMaterialCost failed for " + ((object)c).GetType().Name + ": " + ex.Message, ((object)c).GetType().Name.GetHashCode());
				return null;
			}
			if (list == null || list.Count == 0)
			{
				return null;
			}
			for (int i = 0; i < list.Count; i++)
			{
				ThingDefCountClass val = list[i];
				if (val == null || val.thingDef == null || val.count <= 0)
				{
					continue;
				}
				int remaining;
				if (forced)
				{
					remaining = c.ThingCountNeeded(val.thingDef);
				}
				else
				{
					IHaulEnroute val2 = (IHaulEnroute)(object)((c is IHaulEnroute) ? c : null);
					remaining = ((val2 != null) ? EnrouteUtility.GetSpaceRemainingWithEnroute(val2, val.thingDef, pawn) : c.ThingCountNeeded(val.thingDef));
				}
				if (remaining > 0)
				{
					ThingDef def = val.thingDef;
					Thing val3 = HaulSourceContents.FindBestIncludingRemote(map, (Thing t) => t.stackCount, (Thing t) => t.def == def && CanReserveFor(t, pawn, Math.Min(remaining, t.stackCount)));
					if (val3 != null)
					{
						return (val3, Math.Min(remaining, val3.stackCount));
					}
				}
			}
			return null;
		}
	}
	public class WorkGiver_DS_WithdrawForConstruct : WorkGiver_Scanner
	{
		public override PathEndMode PathEndMode => (PathEndMode)3;

		public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup((ThingRequestGroup)11);

		public override Danger MaxPathDanger(Pawn pawn)
		{
			return (Danger)3;
		}

		public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
		{
			foreach (Thing item in ((Thing)pawn).Map.listerThings.ThingsInGroup((ThingRequestGroup)11))
			{
				yield return item;
			}
			foreach (Thing item2 in ((Thing)pawn).Map.listerThings.ThingsInGroup((ThingRequestGroup)9))
			{
				yield return item2;
			}
		}

		public override bool ShouldSkip(Pawn pawn, bool forced = false)
		{
			return !CoreFinder.AnyUsableCoreGlobal(pawn);
		}

		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			if (t.Faction != ((Thing)pawn).Faction)
			{
				return false;
			}
			IConstructible val = (IConstructible)(object)((t is IConstructible) ? t : null);
			if (val == null)
			{
				return false;
			}
			if (!(t is Frame) && !(t is Blueprint))
			{
				return false;
			}
			if (t is Blueprint_Install)
			{
				return false;
			}
			if (!ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(t), 1, -1, (ReservationLayerDef)null, forced))
			{
				return false;
			}
			if (!GenConstruct.CanConstruct(t, pawn, ((WorkGiver)this).def.workType, forced, DigitalStorage_JobDefOf.DigitalStorage_WithdrawToConstruction))
			{
				return false;
			}
			return DSConstructionDelivery.CanMakeJob(pawn, val, forced);
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			IConstructible val = (IConstructible)(object)((t is IConstructible) ? t : null);
			if (val == null)
			{
				return null;
			}
			if (!DSConstructionDelivery.TryMakeJob(pawn, val, forced, out var job))
			{
				return null;
			}
			return job;
		}
	}
	public static class DSConstructionDelivery
	{
		public static bool CanMakeJob(Pawn pawn, IConstructible c, bool forced)
		{
			return PlanFor(pawn, c, forced).HasValue;
		}

		public static bool TryMakeJob(Pawn pawn, IConstructible c, bool forced, out Job job)
		{
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			job = null;
			(Thing, int)? tuple = PlanFor(pawn, c, forced);
			if (!tuple.HasValue)
			{
				return false;
			}
			Thing val = (Thing)(object)((c is Thing) ? c : null);
			if (val == null)
			{
				return false;
			}
			Thing item = tuple.Value.Item1;
			job = JobMaker.MakeJob(DigitalStorage_JobDefOf.DigitalStorage_WithdrawToConstruction, LocalTargetInfo.op_Implicit(val));
			job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(item));
			Job obj = job;
			IThingHolder parentHolder = item.ParentHolder;
			obj.SetTarget((TargetIndex)3, LocalTargetInfo.op_Implicit((Thing)(object)((parentHolder is Thing) ? parentHolder : null)));
			job.count = tuple.Value.Item2;
			return true;
		}

		private static (Thing thing, int count)? PlanFor(Pawn pawn, IConstructible c, bool forced)
		{
			if (!IsValidTarget(pawn, c))
			{
				return null;
			}
			if (!CoreFinder.AnyUsableCoreGlobal(pawn))
			{
				return null;
			}
			return ConstructMaterialPlanner.TryPlan(c, pawn, forced, ((Thing)pawn).Map);
		}

		private static bool IsValidTarget(Pawn pawn, IConstructible c)
		{
			if (pawn == null || ((Thing)pawn).Map == null || !((Thing)pawn).Spawned)
			{
				return false;
			}
			if (c == null)
			{
				return false;
			}
			if (c is Blueprint_Install)
			{
				return false;
			}
			Thing val = (Thing)(object)((c is Thing) ? c : null);
			if (val == null || val.Destroyed || !val.Spawned)
			{
				return false;
			}
			if (val.Map != ((Thing)pawn).Map)
			{
				return false;
			}
			if (val.Faction != ((Thing)pawn).Faction)
			{
				return false;
			}
			return true;
		}

		public static bool RemoveExistingFloorFirstIfNeeded(IConstructible c, Map map)
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			Blueprint val = (Blueprint)(object)((c is Blueprint) ? c : null);
			if (val == null || map == null)
			{
				return false;
			}
			if (!(((Thing)val).def.entityDefToBuild is TerrainDef))
			{
				return false;
			}
			TerrainGrid terrainGrid = map.terrainGrid;
			IntVec3 position = ((Thing)val).Position;
			if (!terrainGrid.CanRemoveTopLayerAt(position))
			{
				return false;
			}
			TerrainDef val2 = terrainGrid.TempTerrainAt(position);
			if (val2 != null && val2.Removable)
			{
				terrainGrid.RemoveTempTerrain(position, false, false);
			}
			else
			{
				terrainGrid.RemoveTopLayer(position, true);
			}
			FilthMaker.RemoveAllFilth(position, map);
			return true;
		}
	}
	public static class ConsumptionHelper
	{
		public static string LastFailReason = "not called";

		public static Job TryCreateJob(Pawn pawn, ThingDef desiredDef)
		{
			return TryCreateJob(pawn, (Thing t) => t.def == desiredDef);
		}

		public static Job TryCreateJob(Pawn pawn, Predicate<Thing> filter)
		{
			//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
			Pawn obj = pawn;
			if (((obj != null) ? ((Thing)obj).Map : null) == null)
			{
				LastFailReason = "pawn/map null";
				return null;
			}
			if (filter == null)
			{
				LastFailReason = "filter null";
				return null;
			}
			if (pawn.RaceProps.IsMechanoid)
			{
				LastFailReason = "mechanoid";
				return null;
			}
			bool allowDrug = !PawnUtility.IsTeetotaler(pawn);
			Map map = ((Thing)pawn).Map;
			ReservationManager resMgr = map.reservationManager;
			int seen = 0;
			int rejectedByFilter = 0;
			int rejectedByGate = 0;
			int rejectedByReserved = 0;
			Thing val = HaulSourceContents.FindBestIncludingRemote(map, (Thing t) => (!t.def.IsNutritionGivingIngestible) ? FoodScoring.Score(pawn, t.def) : FoodScoring.Score(pawn, t, pawn), delegate(Thing t)
			{
				//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
				seen++;
				if (!filter(t))
				{
					rejectedByFilter++;
					return false;
				}
				ThingDef def = t.def;
				if (def == null)
				{
					return false;
				}
				if (def.IsNutritionGivingIngestible)
				{
					if (!FoodUtility.WillEat(pawn, t, pawn, true, false))
					{
						rejectedByGate++;
						return false;
					}
				}
				else if (def.IsDrug)
				{
					if (!allowDrug)
					{
						rejectedByGate++;
						return false;
					}
					if (!FoodUtility.DrugIsSuitable(pawn, def))
					{
						rejectedByGate++;
						return false;
					}
				}
				if (resMgr != null && resMgr.IsReserved(LocalTargetInfo.op_Implicit(t)))
				{
					rejectedByReserved++;
					return false;
				}
				return true;
			});
			if (val == null)
			{
				LastFailReason = "no match (seen=" + seen + " filtered=" + rejectedByFilter + " gate=" + rejectedByGate + " reserved=" + rejectedByReserved + ")";
				return null;
			}
			int ingestAmount = GetIngestAmount(pawn, val.def, val.stackCount);
			if (ingestAmount <= 0)
			{
				LastFailReason = "GetIngestAmount<=0 for " + ((Def)val.def).defName;
				return null;
			}
			LastFailReason = "ok: " + ((Def)val.def).defName + " x" + ingestAmount;
			Job val2 = JobMaker.MakeJob(DigitalStorage_JobDefOf.DigitalStorage_Consume);
			val2.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit(val));
			IThingHolder parentHolder = val.ParentHolder;
			Thing val3 = (Thing)(object)((parentHolder is Thing) ? parentHolder : null);
			if (val3 != null)
			{
				val2.SetTarget((TargetIndex)3, LocalTargetInfo.op_Implicit(val3));
			}
			val2.count = ingestAmount;
			return val2;
		}

		private static int GetIngestAmount(Pawn pawn, ThingDef def, int maxAvailable)
		{
			if (def.ingestible == null)
			{
				return 0;
			}
			int num;
			if (def.IsNutritionGivingIngestible && pawn.needs?.food != null)
			{
				float statValueAbstract = StatExtension.GetStatValueAbstract((BuildableDef)(object)def, StatDefOf.Nutrition, (ThingDef)null);
				num = FoodUtility.StackCountForNutrition(pawn.needs.food.NutritionWanted, statValueAbstract);
				if (def.ingestible.maxNumToIngestAtOnce > 0)
				{
					num = Math.Min(num, def.ingestible.maxNumToIngestAtOnce);
				}
			}
			else
			{
				num = def.ingestible.defaultNumToIngestAtOnce;
				if (num <= 0)
				{
					num = 1;
				}
			}
			return Math.Min(num, maxAvailable);
		}
	}
	public static class FoodScoring
	{
		public static float Score(Pawn eater, Thing thing, Pawn getter = null)
		{
			if (eater == null || thing == null || thing.def?.ingestible == null)
			{
				return -9999f;
			}
			if (!FoodUtility.WillEat(eater, thing, getter ?? eater, true, false))
			{
				return -9999f;
			}
			return Score(eater, thing.def);
		}

		public static float Score(Pawn eater, ThingDef def)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Expected I4, but got Unknown
			if (def.ingestible == null)
			{
				return -9999f;
			}
			float num = 300f;
			FoodPreferability preferability = def.ingestible.preferability;
			switch (preferability - 1)
			{
			case 0:
				return -9999f;
			case 1:
				num -= 150f;
				break;
			case 2:
				if (eater.RaceProps.Humanlike)
				{
					num -= 150f;
				}
				break;
			}
			if (eater.RaceProps.Humanlike)
			{
				num += def.ingestible.optimalityOffsetHumanlikes;
			}
			else if (eater.RaceProps.Animal)
			{
				num += def.ingestible.optimalityOffsetFeedingAnimals;
			}
			if (eater.story?.traits != null && eater.story.traits.AnyTraitHasIngestibleOverrides)
			{
				List<Trait> allTraits = eater.story.traits.allTraits;
				for (int i = 0; i < allTraits.Count; i++)
				{
					if (allTraits[i].Suppressed)
					{
						continue;
					}
					List<IngestibleModifiers> ingestibleModifiers = allTraits[i].CurrentData.ingestibleModifiers;
					if (GenList.NullOrEmpty<IngestibleModifiers>((IList<IngestibleModifiers>)ingestibleModifiers))
					{
						continue;
					}
					for (int j = 0; j < ingestibleModifiers.Count; j++)
					{
						if (ingestibleModifiers[j].ingestible == def)
						{
							num += ingestibleModifiers[j].optimalityOffset;
						}
					}
				}
			}
			if (eater.RaceProps.Humanlike && eater.needs?.mood != null)
			{
				TraitDef namedSilentFail = DefDatabase<TraitDef>.GetNamedSilentFail("Cannibal");
				int num2;
				if (namedSilentFail != null)
				{
					Pawn_StoryTracker story = eater.story;
					if (story == null)
					{
						num2 = 0;
					}
					else
					{
						TraitSet traits = story.traits;
						num2 = ((((traits != null) ? new bool?(traits.HasTrait(namedSilentFail)) : ((bool?)null)) == true) ? 1 : 0);
					}
				}
				else
				{
					num2 = 0;
				}
				bool flag = (byte)num2 != 0;
				TraitDef namedSilentFail2 = DefDatabase<TraitDef>.GetNamedSilentFail("Ascetic");
				int num3;
				if (namedSilentFail2 != null)
				{
					Pawn_StoryTracker story2 = eater.story;
					if (story2 == null)
					{
						num3 = 0;
					}
					else
					{
						TraitSet traits2 = story2.traits;
						num3 = ((((traits2 != null) ? new bool?(traits2.HasTrait(namedSilentFail2)) : ((bool?)null)) == true) ? 1 : 0);
					}
				}
				else
				{
					num3 = 0;
				}
				bool flag2 = (byte)num3 != 0;
				if (((Def)def).defName.Contains("Human"))
				{
					num += EvalMoodCurve(flag ? 10f : (-20f));
				}
				if (FoodUtility.IsRawHumanFood(def) && !flag2 && !flag)
				{
					num += EvalMoodCurve(-7f);
				}
			}
			return num;
		}

		private static float EvalMoodCurve(float moodEffect)
		{
			if (moodEffect <= -100f)
			{
				return -600f;
			}
			if (moodEffect <= -10f)
			{
				return Lerp(-100f, -10f, -600f, -100f, moodEffect);
			}
			if (moodEffect <= -5f)
			{
				return Lerp(-10f, -5f, -100f, -70f, moodEffect);
			}
			if (moodEffect <= -1f)
			{
				return Lerp(-5f, -1f, -70f, -50f, moodEffect);
			}
			if (moodEffect <= 0f)
			{
				return Lerp(-1f, 0f, -50f, 0f, moodEffect);
			}
			if (moodEffect >= 100f)
			{
				return 800f;
			}
			return Lerp(0f, 100f, 0f, 800f, moodEffect);
		}

		private static float Lerp(float x0, float x1, float y0, float y1, float x)
		{
			return y0 + (y1 - y0) * ((x - x0) / (x1 - x0));
		}
	}
	public class JobDriver_DS_Consume : JobDriver
	{
		private Thing TargetThing
		{
			get
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
				return ((LocalTargetInfo)(ref target)).Thing;
			}
		}

		private Building_StorageCore TargetCore
		{
			get
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = base.job.GetTarget((TargetIndex)3);
				return ((LocalTargetInfo)(ref target)).Thing as Building_StorageCore;
			}
		}

		private float ChewDurationMultiplier
		{
			get
			{
				ThingDef val = TargetThing?.def;
				if (val?.ingestible != null && !val.ingestible.useEatingSpeedStat)
				{
					return 1f;
				}
				return 1f / StatExtension.GetStatValue((Thing)(object)base.pawn, StatDefOf.EatingSpeed, true, -1);
			}
		}

		public override string GetReport()
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			Thing targetThing = TargetThing;
			return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_ReportConsuming", NamedArgument.op_Implicit((targetThing != null) ? ((Entity)targetThing).Label : "?")));
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			Thing targetThing = TargetThing;
			if (targetThing == null || targetThing.Destroyed)
			{
				return true;
			}
			if (targetThing.MapHeld != ((Thing)base.pawn).Map)
			{
				return true;
			}
			ReservationUtility.Reserve(base.pawn, LocalTargetInfo.op_Implicit(targetThing), base.job, 1, -1, (ReservationLayerDef)null, false, false);
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			ToilFailConditions.FailOn<JobDriver_DS_Consume>(this, (Func<bool>)delegate
			{
				Building_StorageCore targetCore = TargetCore;
				if (targetCore != null && (!((Thing)targetCore).Spawned || !targetCore.Powered))
				{
					return true;
				}
				Thing targetThing = TargetThing;
				return targetThing == null || targetThing.Destroyed;
			});
			yield return DropIncompatibleCarryIfNeeded();
			yield return WithdrawToCarry();
			yield return CarryToChewSpot();
			yield return Toils_Ingest.FindAdjacentEatSurface((TargetIndex)2, (TargetIndex)1);
			yield return ToilFailConditions.FailOnCannotTouch<Toil>(ToilFailConditions.FailOn(Toils_Ingest.ChewIngestible(base.pawn, ChewDurationMultiplier, (TargetIndex)1, (TargetIndex)2), (Func<Toil, bool>)((Toil x) => base.pawn.carryTracker.CarriedThing == null || !base.pawn.carryTracker.CarriedThing.IngestibleNow)), (TargetIndex)1, (PathEndMode)2);
			yield return Toils_Ingest.FinalizeIngest(base.pawn, (TargetIndex)1);
		}

		private Toil DropIncompatibleCarryIfNeeded()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			Toil obj = ToilMaker.MakeToil("DS_DropIncompatible");
			obj.defaultCompleteMode = (ToilCompleteMode)1;
			obj.initAction = delegate
			{
				//IL_003d: Unknown result type (might be due to invalid IL or missing references)
				Thing carriedThing = base.pawn.carryTracker.CarriedThing;
				Thing targetThing = TargetThing;
				if (carriedThing != null && targetThing != null && carriedThing.def != targetThing.def)
				{
					Thing val = default(Thing);
					base.pawn.carryTracker.TryDropCarriedThing(((Thing)base.pawn).Position, (ThingPlaceMode)1, ref val, (Action<Thing, int>)null);
				}
			};
			return obj;
		}

		private Toil WithdrawToCarry()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			Toil obj = ToilMaker.MakeToil("DS_WithdrawToCarry");
			obj.defaultCompleteMode = (ToilCompleteMode)1;
			obj.initAction = delegate
			{
				//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
				Thing targetThing = TargetThing;
				if (targetThing == null || targetThing.Destroyed)
				{
					Fail();
				}
				else
				{
					IThingHolder parentHolder = targetThing.ParentHolder;
					ThingOwner val = ((parentHolder != null) ? parentHolder.GetDirectlyHeldThings() : null);
					if (val == null || !val.Contains(targetThing))
					{
						Fail();
					}
					else
					{
						int num = base.pawn.carryTracker.AvailableStackSpace(targetThing.def);
						if (num <= 0)
						{
							Fail();
						}
						else
						{
							int num2 = Math.Min(Math.Min(base.job.count, num), targetThing.stackCount);
							if (num2 <= 0)
							{
								Fail();
							}
							else
							{
								Thing val2;
								if (num2 >= targetThing.stackCount)
								{
									val.Remove(targetThing);
									val2 = targetThing;
								}
								else
								{
									val2 = targetThing.SplitOff(num2);
								}
								if (val2 == null)
								{
									Fail();
								}
								else if (base.pawn.carryTracker.TryStartCarry(val2, val2.stackCount, false) < val2.stackCount)
								{
									ReturnToOwner(val, val2);
									Fail();
								}
								else
								{
									base.job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit(base.pawn.carryTracker.CarriedThing));
								}
							}
						}
					}
				}
			};
			return obj;
		}

		private static void ReturnToOwner(ThingOwner owner, Thing t)
		{
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			if (t != null && !t.Destroyed && (owner == null || !owner.TryAdd(t, true)) && !t.Spawned)
			{
				Map currentMap = Find.CurrentMap;
				if (currentMap != null)
				{
					GenPlace.TryPlaceThing(t, currentMap.Center, currentMap, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
				}
			}
		}

		private Toil CarryToChewSpot()
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			Toil obj = ToilMaker.MakeToil("DS_CarryToChewSpot");
			obj.initAction = delegate
			{
				//IL_0098: Unknown result type (might be due to invalid IL or missing references)
				//IL_009d: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
				//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Unknown result type (might be due to invalid IL or missing references)
				//IL_005b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0068: Unknown result type (might be due to invalid IL or missing references)
				//IL_0069: Unknown result type (might be due to invalid IL or missing references)
				//IL_007e: Unknown result type (might be due to invalid IL or missing references)
				//IL_007f: Unknown result type (might be due to invalid IL or missing references)
				Thing carriedThing = base.pawn.carryTracker.CarriedThing;
				IntVec3 val = default(IntVec3);
				if (carriedThing == null)
				{
					Fail();
				}
				else if (Toils_Ingest.TryFindChairOrSpot(base.pawn, carriedThing, ref val))
				{
					ReservationUtility.ReserveSittableOrSpot(base.pawn, val, base.job, true);
					((Thing)base.pawn).Map.pawnDestinationReservationManager.Reserve(base.pawn, base.job, val);
					base.job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(val));
					base.pawn.pather.StartPath(LocalTargetInfo.op_Implicit(val), (PathEndMode)1);
				}
				else
				{
					base.job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(((Thing)base.pawn).Position));
					base.pawn.pather.StartPath(LocalTargetInfo.op_Implicit(((Thing)base.pawn).Position), (PathEndMode)1);
				}
			};
			obj.defaultCompleteMode = (ToilCompleteMode)2;
			return obj;
		}

		private void Fail()
		{
			ConsumePatchUtil.NotifyFail(base.pawn);
			((JobDriver)this).EndJobWith((JobCondition)4);
		}
	}
	public class JobDriver_DS_EmptyWasteToCore : JobDriver
	{
		private const TargetIndex ContainerInd = (TargetIndex)1;

		private const TargetIndex CoreInd = (TargetIndex)2;

		private const TargetIndex WasteInd = (TargetIndex)3;

		private const int OpenTicks = 120;

		private Thing Container
		{
			get
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
				return ((LocalTargetInfo)(ref target)).Thing;
			}
		}

		private Building_StorageCore TargetCore
		{
			get
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = base.job.GetTarget((TargetIndex)2);
				return ((LocalTargetInfo)(ref target)).Thing as Building_StorageCore;
			}
		}

		private Thing TargetWaste
		{
			get
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				LocalTargetInfo target = base.job.GetTarget((TargetIndex)3);
				return ((LocalTargetInfo)(ref target)).Thing;
			}
		}

		private PathEndMode ContainerPathEndMode
		{
			get
			{
				Thing container = Container;
				if (container?.def != null && container.def.hasInteractionCell)
				{
					return (PathEndMode)4;
				}
				return (PathEndMode)2;
			}
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			if (!ReservationUtility.Reserve(base.pawn, LocalTargetInfo.op_Implicit(Container), base.job, 1, -1, ReservationLayerDefOf.Empty, errorOnFailed, false))
			{
				return false;
			}
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			yield return ToilFailConditions.FailOn<Toil>(ToilFailConditions.FailOnSomeonePhysicallyInteracting<Toil>(ToilFailConditions.FailOnDespawnedNullOrForbidden<Toil>(Toils_Goto.GotoThing((TargetIndex)1, ContainerPathEndMode, false), (TargetIndex)1), (TargetIndex)1), (Func<bool>)delegate
			{
				Thing container = Container;
				CompThingContainer val = default(CompThingContainer);
				return container != null && ThingCompUtility.TryGetComp<CompThingContainer>(container, ref val) && val.Empty;
			});
			yield return Toils_General.WaitWhileExtractingContents((TargetIndex)1, (TargetIndex)3, 120);
			yield return Toils_General.Do((Action)delegate
			{
				//IL_0017: Unknown result type (might be due to invalid IL or missing references)
				Thing container = Container;
				if (container != null && ThingOwnerUtility.TryGetInnerInteractableThingOwner(container).TryDropAll(((Thing)base.pawn).Position, ((Thing)base.pawn).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, true))
				{
					CompThingContainer obj = ThingCompUtility.TryGetComp<CompThingContainer>(container);
					if (obj != null)
					{
						EffecterDef dropEffecterDef = obj.Props.dropEffecterDef;
						if (dropEffecterDef != null)
						{
							dropEffecterDef.Spawn(container, ((JobDriver)this).Map, 1f).Cleanup();
						}
					}
				}
			});
			yield return Toils_Reserve.Reserve((TargetIndex)3, 1, -1, (ReservationLayerDef)null, false);
			yield return ToilFailConditions.FailOnSomeonePhysicallyInteracting<Toil>(ToilFailConditions.FailOnDespawnedNullOrForbidden<Toil>(Toils_Goto.GotoThing((TargetIndex)3, (PathEndMode)2, false), (TargetIndex)3), (TargetIndex)3);
			yield return Toils_Haul.StartCarryThing((TargetIndex)3, false, true, false, true, false);
			yield return Toils_Haul.CarryHauledThingToContainer();
			yield return Toils_Haul.DepositHauledThingInContainer((TargetIndex)2, (TargetIndex)0, (Action)null);
			yield return Toils_General.Do((Action)delegate
			{
				//IL_0032: Unknown result type (might be due to invalid IL or missing references)
				Pawn pawn = base.pawn;
				object obj;
				if (pawn == null)
				{
					obj = null;
				}
				else
				{
					Pawn_CarryTracker carryTracker = pawn.carryTracker;
					obj = ((carryTracker != null) ? carryTracker.CarriedThing : null);
				}
				if (obj != null)
				{
					Thing val = default(Thing);
					base.pawn.carryTracker.TryDropCarriedThing(((Thing)base.pawn).Position, (ThingPlaceMode)1, ref val, (Action<Thing, int>)null);
				}
			});
		}
	}
	public static class CoreFinder
	{
		private static readonly List<(Pawn pawn, int tick, List<Building_StorageCore> result)> cache = new List<(Pawn, int, List<Building_StorageCore>)>();

		private const int CacheSize = 4;

		private static int globalTick = -1;

		private static Map cachedExclude;

		private static bool globalAny;

		public static List<Building_StorageCore> AllUsableCores(Pawn pawn)
		{
			int ticksGame = Find.TickManager.TicksGame;
			for (int i = 0; i < cache.Count; i++)
			{
				if (cache[i].pawn == pawn && cache[i].tick == ticksGame)
				{
					return cache[i].result;
				}
			}
			List<Building_StorageCore> list = AllUsableCores((pawn != null) ? ((Thing)pawn).Map : null);
			cache.Insert(0, (pawn, ticksGame, list));
			if (cache.Count > 4)
			{
				cache.RemoveAt(cache.Count - 1);
			}
			return list;
		}

		public static List<Building_StorageCore> AllUsableCores(Map map)
		{
			List<Building_StorageCore> list = new List<Building_StorageCore>();
			if (map == null)
			{
				return list;
			}
			List<IHaulSource> list2 = HaulSourceContents.EnabledSources(map);
			for (int i = 0; i < list2.Count; i++)
			{
				if (list2[i] is Building_StorageCore building_StorageCore && IsUsable(building_StorageCore))
				{
					list.Add(building_StorageCore);
				}
			}
			return list;
		}

		public static bool AnyUsableCore(Pawn pawn)
		{
			return AllUsableCores(pawn).Count > 0;
		}

		public static List<Building_StorageCore> AllUsableCoresGlobal()
		{
			List<Building_StorageCore> list = new List<Building_StorageCore>();
			List<Map> maps = Find.Maps;
			if (maps == null)
			{
				return list;
			}
			for (int i = 0; i < maps.Count; i++)
			{
				Map val = maps[i];
				if (val != null)
				{
					list.AddRange(AllUsableCores(val));
				}
			}
			return list;
		}

		public static bool AnyRemoteUsableCore(Map exclude)
		{
			int ticksGame = Find.TickManager.TicksGame;
			if (globalTick == ticksGame && cachedExclude == exclude)
			{
				return globalAny;
			}
			globalTick = ticksGame;
			cachedExclude = exclude;
			globalAny = false;
			List<Map> maps = Find.Maps;
			if (maps != null)
			{
				for (int i = 0; i < maps.Count; i++)
				{
					if (globalAny)
					{
						break;
					}
					Map val = maps[i];
					if (val != null && val != exclude && AllUsableCores(val).Count > 0)
					{
						globalAny = true;
					}
				}
			}
			return globalAny;
		}

		public static bool AnyUsableCoreGlobal(Pawn pawn)
		{
			if (AnyUsableCore(pawn))
			{
				return true;
			}
			return AnyRemoteUsableCore((pawn != null) ? ((Thing)pawn).Map : null);
		}

		public static bool IsUsable(Building_StorageCore core)
		{
			return core?.IsUsableNow ?? false;
		}
	}
	public abstract class DigitalTask
	{
		public Thing target;

		public CompDigitalWorker comp;

		public WorkTypeDef workType;

		public bool Abort;

		public int StrikeCount;

		public virtual string HandMoteDefName => "DS_WorkHand";

		public virtual EffecterDef HitEffecterDef => null;

		public abstract string Label { get; }

		public virtual bool Finished
		{
			get
			{
				if (!Abort && target != null && !target.Destroyed)
				{
					return !target.Spawned;
				}
				return true;
			}
		}

		public virtual float Progress01 => -1f;

		public virtual IntVec3 TargetCell
		{
			get
			{
				//IL_0015: Unknown result type (might be due to invalid IL or missing references)
				//IL_0021: Unknown result type (might be due to invalid IL or missing references)
				if (target == null || !target.Spawned)
				{
					return IntVec3.Invalid;
				}
				return target.Position;
			}
		}

		public virtual string TargetLabel
		{
			get
			{
				//IL_0015: Unknown result type (might be due to invalid IL or missing references)
				//IL_001a: Unknown result type (might be due to invalid IL or missing references)
				if (target != null)
				{
					return ((Entity)target).LabelShort;
				}
				IntVec3 targetCell = TargetCell;
				if (!((IntVec3)(ref targetCell)).IsValid)
				{
					return "?";
				}
				return "(" + targetCell.x + ", " + targetCell.z + ")";
			}
		}

		public abstract bool StillValid(Pawn pawn, Map map);

		public abstract void Work(Pawn pawn, Map map, float speedMult);

		public virtual void Cleanup()
		{
		}

		public virtual void Claim(Map map, CompDigitalWorker me)
		{
			DigitalWorkerClaims.TryClaim(map, target, me);
		}

		public virtual void Release(Map map, CompDigitalWorker me)
		{
			DigitalWorkerClaims.Release(map, target, me);
		}
	}
	public abstract class DigitalTaskAdapter
	{
		public abstract Type WorkGiverClass { get; }

		public virtual string[] HomeWorkTypes => null;

		public virtual bool CellBased => false;

		public virtual bool TrustWorkGiver => true;

		public bool AcceptsWorkType(WorkTypeDef workType)
		{
			string[] homeWorkTypes = HomeWorkTypes;
			if (homeWorkTypes == null)
			{
				return true;
			}
			if (workType == null || ((Def)workType).defName == null)
			{
				return false;
			}
			for (int i = 0; i < homeWorkTypes.Length; i++)
			{
				if (homeWorkTypes[i] == ((Def)workType).defName)
				{
					return true;
				}
			}
			return false;
		}

		public abstract DigitalTask MakeTask(Thing t, CompDigitalWorker comp);

		public virtual IEnumerable<IntVec3> CandidateCells(Map map, Pawn pawn, WorkGiver_Scanner scanner)
		{
			return scanner.PotentialWorkCellsGlobal(pawn);
		}

		public virtual DigitalTask MakeCellTask(IntVec3 cell, Map map, Pawn pawn, WorkGiver_Scanner scanner, CompDigitalWorker comp)
		{
			return null;
		}

		public virtual bool CanTarget(Pawn pawn, Thing t)
		{
			return true;
		}

		public virtual void SliceTick(Map map, Pawn pawn, WorkGiver_Scanner scanner)
		{
		}

		public virtual IEnumerable<Thing> CandidateSet(Map map, Pawn pawn, WorkGiver_Scanner scanner)
		{
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			IEnumerable<Thing> enumerable = null;
			try
			{
				enumerable = scanner.PotentialWorkThingsGlobal(pawn);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 取代理候选集失败：" + ((Def)((WorkGiver)scanner).def).defName + " → " + ex, ((Def)((WorkGiver)scanner).def).shortHash * 31 + 7717);
			}
			if (enumerable == null)
			{
				enumerable = map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
			}
			return enumerable;
		}
	}
	public static class DigitalTaskRegistry
	{
		private static readonly List<DigitalTaskAdapter> All = new List<DigitalTaskAdapter>
		{
			new DigitalTaskAdapter_Mine(),
			new DigitalTaskAdapter_Construct(),
			new DigitalTaskAdapter_Deconstruct(),
			new DigitalTaskAdapter_Clean(),
			new DigitalTaskAdapter_PlantCut(),
			new DigitalTaskAdapter_GrowerSow(),
			new DigitalTaskAdapter_GrowerHarvest()
		};

		private static readonly Dictionary<WorkTypeDef, List<WorkGiver>> giversCache = new Dictionary<WorkTypeDef, List<WorkGiver>>();

		public static List<WorkGiver> FindGivers(WorkTypeDef workType)
		{
			if (workType == null)
			{
				return null;
			}
			if (giversCache.TryGetValue(workType, out var value))
			{
				return value;
			}
			List<WorkGiver> list = new List<WorkGiver>();
			List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				WorkGiverDef val = allDefsListForReading[i];
				if (val == null || val.workType != workType)
				{
					continue;
				}
				WorkGiver worker = val.Worker;
				if (worker != null)
				{
					DigitalTaskAdapter digitalTaskAdapter = AdapterFor(worker);
					if (digitalTaskAdapter != null && digitalTaskAdapter.AcceptsWorkType(workType))
					{
						list.Add(worker);
					}
				}
			}
			giversCache[workType] = list;
			return list;
		}

		public static DigitalTaskAdapter AdapterFor(WorkGiver giver)
		{
			if (giver == null || giver.def == null || giver.def.giverClass == null)
			{
				return null;
			}
			for (int i = 0; i < All.Count; i++)
			{
				if (All[i].WorkGiverClass.IsAssignableFrom(giver.def.giverClass))
				{
					return All[i];
				}
			}
			return null;
		}

		public static bool PawnCanUse(WorkGiver giver, Pawn pawn, DigitalTaskAdapter adapter)
		{
			if (giver == null || giver.def == null || pawn == null)
			{
				return false;
			}
			if (!giver.def.nonColonistsCanDo && !pawn.IsColonist && (!pawn.RaceProps.IsMechanoid || ((Thing)pawn).Faction != Faction.OfPlayer))
			{
				return false;
			}
			if ((adapter == null || adapter.TrustWorkGiver) && giver.ShouldSkip(pawn, false))
			{
				return false;
			}
			if (giver.MissingRequiredCapacity(pawn) != null)
			{
				return false;
			}
			if (pawn.RaceProps.IsMechanoid && !giver.def.canBeDoneByMechs)
			{
				return false;
			}
			return true;
		}
	}
	public class DigitalTaskAdapter_Mine : DigitalTaskAdapter
	{
		private static readonly string[] Homes = new string[1] { "Mining" };

		public override Type WorkGiverClass => typeof(WorkGiver_Miner);

		public override string[] HomeWorkTypes => Homes;

		public override DigitalTask MakeTask(Thing t, CompDigitalWorker comp)
		{
			return new DigitalTask_Mine
			{
				target = t,
				comp = comp
			};
		}
	}
	public class DigitalTask_Mine : DigitalTask
	{
		private const int BaseTicksBetweenPickHits = 100;

		private const int BaseDamagePerPickHit_NaturalRock = 80;

		private const int BaseDamagePerPickHit_NotNaturalRock = 40;

		private float ticksToPickHit = -1f;

		public override string Label => ((object)Translator.Translate("DS_Task_Mine")/*cast due to constrained. prefix*/).ToString();

		public override EffecterDef HitEffecterDef => EffecterDefOf.Mine;

		public override float Progress01
		{
			get
			{
				if (target == null || target.MaxHitPoints <= 0)
				{
					return -1f;
				}
				return 1f - (float)target.HitPoints / (float)target.MaxHitPoints;
			}
		}

		public override void Cleanup()
		{
		}

		public override bool StillValid(Pawn pawn, Map map)
		{
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			if (target == null || target.Destroyed || !target.Spawned || !target.def.mineable)
			{
				return false;
			}
			DesignationManager designationManager = map.designationManager;
			if (designationManager.DesignationAt(target.Position, DesignationDefOf.Mine) == null)
			{
				return designationManager.DesignationAt(target.Position, DesignationDefOf.MineVein) != null;
			}
			return true;
		}

		public override void Work(Pawn pawn, Map map, float speedMult)
		{
			if (target == null || target.Destroyed || !target.Spawned)
			{
				return;
			}
			if (ticksToPickHit < 0f)
			{
				ticksToPickHit = Mathf.Round(100f / speedMult);
			}
			ticksToPickHit -= 1f;
			if (!(ticksToPickHit > 0f))
			{
				StrikeCount++;
				DoDamage(pawn, map);
				if (target != null && !target.Destroyed)
				{
					ticksToPickHit = Mathf.Round(100f / speedMult);
				}
			}
		}

		private void DoDamage(Pawn pawn, Map map)
		{
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
			Thing val = target;
			int num = ((val.def.building != null && val.def.building.isNaturalRock) ? 80 : 40);
			Mineable val2 = (Mineable)(object)((val is Mineable) ? val : null);
			if (val2 == null || val.HitPoints > num)
			{
				val.TakeDamage(new DamageInfo(DamageDefOf.Mining, (float)num, 0f, -1f, (Thing)(object)pawn, (BodyPartRecord)null, (ThingDef)null, (SourceCategory)0, (Thing)null, true, true, (QualityCategory)2, true, false));
			}
			else
			{
				if (!DigitalWorkBudget.AllowCompletion())
				{
					return;
				}
				bool num2 = map.designationManager.DesignationAt(((Thing)val2).Position, DesignationDefOf.MineVein) != null;
				IntVec3 position = ((Thing)val2).Position;
				val2.Notify_TookMiningDamage(val.HitPoints, pawn);
				((Thing)val2).HitPoints = 0;
				val2.DestroyMined(pawn);
				if (pawn.records != null)
				{
					pawn.records.Increment(RecordDefOf.CellsMined);
				}
				if (map.mineStrikeManager != null)
				{
					map.mineStrikeManager.CheckStruckOre(position, val.def, (Thing)(object)pawn);
				}
				if (num2)
				{
					IntVec3[] adjacentCells = GenAdj.AdjacentCells;
					for (int i = 0; i < adjacentCells.Length; i++)
					{
						Designator_MineVein.FloodFillDesignations(position + adjacentCells[i], map, val.def);
					}
				}
			}
		}
	}
	public class DigitalTaskAdapter_Construct : DigitalTaskAdapter
	{
		private static readonly string[] Homes = new string[1] { "Construction" };

		public override Type WorkGiverClass => typeof(WorkGiver_ConstructFinishFrames);

		public override string[] HomeWorkTypes => Homes;

		public override bool TrustWorkGiver => false;

		public override DigitalTask MakeTask(Thing t, CompDigitalWorker comp)
		{
			return new DigitalTask_Construct
			{
				target = t,
				comp = comp
			};
		}

		public override IEnumerable<Thing> CandidateSet(Map map, Pawn pawn, WorkGiver_Scanner scanner)
		{
			List<Thing> list = map.listerThings.ThingsInGroup((ThingRequestGroup)9);
			List<Thing> list2 = map.listerThings.ThingsInGroup((ThingRequestGroup)11);
			List<Thing> list3 = new List<Thing>(list.Count + list2.Count);
			list3.AddRange(list);
			list3.AddRange(list2);
			return list3;
		}

		public override bool CanTarget(Pawn pawn, Thing t)
		{
			if (!(t is Blueprint_Build) && !(t is Frame))
			{
				return false;
			}
			return FactionMatches(pawn, t);
		}

		internal static bool FactionMatches(Pawn pawn, Thing t)
		{
			if (t == null)
			{
				return false;
			}
			if (pawn == null || ((Thing)pawn).Faction == null)
			{
				return false;
			}
			return t.Faction == ((Thing)pawn).Faction;
		}
	}
	public class DigitalTask_Construct : DigitalTask
	{
		public override string Label => ((object)Translator.Translate("DS_Task_Build")/*cast due to constrained. prefix*/).ToString();

		private Frame FrameTarget
		{
			get
			{
				Thing obj = target;
				return (Frame)(object)((obj is Frame) ? obj : null);
			}
		}

		public override float Progress01
		{
			get
			{
				Frame frameTarget = FrameTarget;
				if (frameTarget == null || frameTarget.WorkToBuild <= 0f)
				{
					return -1f;
				}
				return Mathf.Clamp01(frameTarget.workDone / frameTarget.WorkToBuild);
			}
		}

		public override bool Finished
		{
			get
			{
				if (Abort)
				{
					return true;
				}
				Frame frameTarget = FrameTarget;
				if (frameTarget != null && !((Thing)frameTarget).Destroyed && frameTarget.IsCompleted() && frameTarget.WorkLeft <= 0f)
				{
					return true;
				}
				return base.Finished;
			}
		}

		public override bool StillValid(Pawn pawn, Map map)
		{
			if (target == null || target.Destroyed || !target.Spawned)
			{
				return false;
			}
			Thing obj = target;
			Blueprint val = (Blueprint)(object)((obj is Blueprint) ? obj : null);
			if (val != null)
			{
				if (!DigitalTaskAdapter_Construct.FactionMatches(pawn, (Thing)(object)val))
				{
					return false;
				}
				Thing val2 = GenConstruct.FirstBlockingThing((Thing)(object)val, pawn);
				if (val2 != null && !(val2 is Plant))
				{
					return false;
				}
				return true;
			}
			Frame frameTarget = FrameTarget;
			if (frameTarget == null)
			{
				return false;
			}
			if (!frameTarget.IsCompleted())
			{
				return false;
			}
			if (!DigitalTaskAdapter_Construct.FactionMatches(pawn, (Thing)(object)frameTarget))
			{
				return false;
			}
			if (FireUtility.IsBurning((Thing)(object)frameTarget))
			{
				return false;
			}
			if (((BuildableDef)((Thing)frameTarget).def).constructionSkillPrerequisite > 0 && pawn.skills != null && pawn.skills.GetSkill(SkillDefOf.Construction).Level < ((BuildableDef)((Thing)frameTarget).def).constructionSkillPrerequisite)
			{
				return false;
			}
			return GenConstruct.FirstBlockingThing((Thing)(object)frameTarget, pawn) == null;
		}

		public override void Work(Pawn pawn, Map map, float speedMult)
		{
			if (target == null || target.Destroyed || !target.Spawned)
			{
				return;
			}
			Thing obj = target;
			Blueprint val = (Blueprint)(object)((obj is Blueprint) ? obj : null);
			if (val != null)
			{
				Thing val2 = GenConstruct.FirstBlockingThing((Thing)(object)val, pawn);
				Plant val3 = (Plant)(object)((val2 is Plant) ? val2 : null);
				if (val3 != null && !((Thing)val3).Destroyed && ((Thing)val3).Spawned)
				{
					DigitalPlantWork.Harvest(pawn, val3, asHarvest: false);
				}
				else if (val2 != null)
				{
					Abort = true;
				}
				else if (!DSConstructionDelivery.RemoveExistingFloorFirstIfNeeded((IConstructible)(object)val, map) && !TrySupplyFromCores(pawn, map, val))
				{
					Abort = true;
				}
				return;
			}
			Frame frameTarget = FrameTarget;
			if (frameTarget == null || ((Thing)frameTarget).Destroyed)
			{
				return;
			}
			float num = speedMult * 1.7f;
			if (((Thing)frameTarget).Stuff != null)
			{
				num *= StatExtension.GetStatValueAbstract((BuildableDef)(object)((Thing)frameTarget).Stuff, StatDefOf.ConstructionSpeedFactor, (ThingDef)null);
			}
			float workToBuild = frameTarget.WorkToBuild;
			if (workToBuild <= 0f)
			{
				if (DigitalWorkBudget.AllowCompletion())
				{
					frameTarget.CompleteConstruction(pawn);
				}
				return;
			}
			bool flag = frameTarget.workDone + num >= workToBuild;
			if (!flag || DigitalWorkBudget.AllowCompletion())
			{
				frameTarget.workDone += num;
				StrikeCount++;
				if (flag)
				{
					frameTarget.CompleteConstruction(pawn);
				}
			}
		}

		private bool TrySupplyFromCores(Pawn pawn, Map map, Blueprint bp)
		{
			List<ThingDefCountClass> list = bp.TotalMaterialCost();
			if (list == null || list.Count == 0)
			{
				return TryReplace(pawn, bp);
			}
			for (int i = 0; i < list.Count; i++)
			{
				ThingDefCountClass val = list[i];
				if (val != null && val.thingDef != null && val.count > 0 && CountInCores(map, val.thingDef) < val.count)
				{
					return false;
				}
			}
			if (!TryReplace(pawn, bp))
			{
				return false;
			}
			Frame frameTarget = FrameTarget;
			if (frameTarget == null || ((Thing)frameTarget).Destroyed)
			{
				return true;
			}
			ThingOwner val2 = ThingOwnerUtility.TryGetInnerInteractableThingOwner((Thing)(object)frameTarget);
			if (val2 == null)
			{
				return true;
			}
			for (int j = 0; j < list.Count; j++)
			{
				ThingDefCountClass val3 = list[j];
				if (val3 != null && val3.thingDef != null && val3.count > 0)
				{
					TransferFromCores(map, val3.thingDef, val3.count, val2);
				}
			}
			return true;
		}

		private bool TryReplace(Pawn pawn, Blueprint bp)
		{
			Thing val = default(Thing);
			bool flag = default(bool);
			if (!bp.TryReplaceWithSolidThing(pawn, ref val, ref flag))
			{
				return false;
			}
			if (val != null)
			{
				target = val;
			}
			return true;
		}

		private static int CountInCores(Map map, ThingDef def)
		{
			int num = 0;
			List<IHaulSource> list = HaulSourceContents.CoreSources(map);
			for (int i = 0; i < list.Count; i++)
			{
				ThingOwner directlyHeldThings = ((IThingHolder)list[i]).GetDirectlyHeldThings();
				if (directlyHeldThings == null)
				{
					continue;
				}
				for (int j = 0; j < directlyHeldThings.Count; j++)
				{
					Thing val = directlyHeldThings[j];
					if (val != null && !val.Destroyed && val.def == def)
					{
						num += val.stackCount;
					}
				}
			}
			return num;
		}

		private static void TransferFromCores(Map map, ThingDef def, int count, ThingOwner dest)
		{
			int num = count;
			List<IHaulSource> list = HaulSourceContents.CoreSources(map);
			for (int i = 0; i < list.Count; i++)
			{
				if (num <= 0)
				{
					break;
				}
				ThingOwner directlyHeldThings = ((IThingHolder)list[i]).GetDirectlyHeldThings();
				if (directlyHeldThings == null)
				{
					continue;
				}
				int num2 = directlyHeldThings.Count - 1;
				while (num2 >= 0 && num > 0)
				{
					Thing val = directlyHeldThings[num2];
					if (val != null && !val.Destroyed && val.def == def)
					{
						int num3 = directlyHeldThings.TryTransferToContainer(val, dest, Mathf.Min(num, val.stackCount), true);
						if (num3 > 0)
						{
							num -= num3;
						}
					}
					num2--;
				}
			}
		}
	}
	public class DigitalTaskAdapter_Clean : DigitalTaskAdapter
	{
		private sealed class FilthSnapshot
		{
			private List<Thing> current = new List<Thing>();

			private List<Thing> pending = new List<Thing>();

			private int cursor;

			private int lastSliceTick = int.MinValue;

			public List<Thing> Current => current;

			public void Advance(Map map)
			{
				int ticksGame = GenTicks.TicksGame;
				if (ticksGame == lastSliceTick)
				{
					return;
				}
				lastSliceTick = ticksGame;
				List<Thing> allThings = map.listerThings.AllThings;
				if (allThings == null)
				{
					return;
				}
				long since = DevDrawProfiler.Stamp();
				int num = 0;
				while (cursor < allThings.Count && num < 3000)
				{
					Thing val = allThings[cursor];
					cursor++;
					num++;
					if (val is Filth)
					{
						pending.Add(val);
					}
				}
				DevDrawProfiler.Mark("ScanSet", since);
				DevDrawProfiler.Bump("清切片", num);
				if (cursor >= allThings.Count)
				{
					List<Thing> list = current;
					current = pending;
					pending = list;
					pending.Clear();
					cursor = 0;
					DevDrawProfiler.Bump("清池换", 1);
				}
			}
		}

		private static readonly string[] Homes = new string[1] { "Cleaning" };

		private const int SlicePerTick = 3000;

		private static readonly Dictionary<Map, FilthSnapshot> snapshots = new Dictionary<Map, FilthSnapshot>();

		public override Type WorkGiverClass => typeof(WorkGiver_CleanFilth);

		public override string[] HomeWorkTypes => Homes;

		public override bool TrustWorkGiver => false;

		public override IEnumerable<Thing> CandidateSet(Map map, Pawn pawn, WorkGiver_Scanner scanner)
		{
			return SnapshotFor(map)?.Current;
		}

		public override void SliceTick(Map map, Pawn pawn, WorkGiver_Scanner scanner)
		{
			SnapshotFor(map)?.Advance(map);
		}

		private static FilthSnapshot SnapshotFor(Map map)
		{
			if (map == null)
			{
				return null;
			}
			if (snapshots.TryGetValue(map, out var value))
			{
				return value;
			}
			PruneSnapshots();
			value = new FilthSnapshot();
			snapshots[map] = value;
			return value;
		}

		private static void PruneSnapshots()
		{
			if (snapshots.Count < 8)
			{
				return;
			}
			List<Map> list = null;
			foreach (KeyValuePair<Map, FilthSnapshot> snapshot in snapshots)
			{
				if (snapshot.Key == null || Find.Maps == null || !Find.Maps.Contains(snapshot.Key))
				{
					if (list == null)
					{
						list = new List<Map>();
					}
					list.Add(snapshot.Key);
				}
			}
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					snapshots.Remove(list[i]);
				}
			}
		}

		public override DigitalTask MakeTask(Thing t, CompDigitalWorker comp)
		{
			return new DigitalTask_Clean
			{
				target = t,
				comp = comp
			};
		}

		public override bool CanTarget(Pawn pawn, Thing t)
		{
			return t is Filth;
		}
	}
	public class DigitalTask_Clean : DigitalTask
	{
		private float cleaningWorkDone;

		private float totalWorkRequired;

		public override string Label => ((object)Translator.Translate("DS_Task_Clean")/*cast due to constrained. prefix*/).ToString();

		private Filth FilthTarget
		{
			get
			{
				Thing obj = target;
				return (Filth)(object)((obj is Filth) ? obj : null);
			}
		}

		public override float Progress01
		{
			get
			{
				Filth filthTarget = FilthTarget;
				if (filthTarget == null || ((Thing)filthTarget).Destroyed || totalWorkRequired <= 0f)
				{
					return -1f;
				}
				float num = (float)filthTarget.thickness * ((Thing)filthTarget).def.filth.cleaningWorkToReduceThickness - cleaningWorkDone;
				return Mathf.Clamp01(1f - num / totalWorkRequired);
			}
		}

		public override bool StillValid(Pawn pawn, Map map)
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			Filth filthTarget = FilthTarget;
			if (filthTarget == null || ((Thing)filthTarget).Destroyed || !((Thing)filthTarget).Spawned)
			{
				return false;
			}
			if (map.fogGrid != null && map.fogGrid.IsFogged(((Thing)filthTarget).Position))
			{
				return false;
			}
			return filthTarget.TicksSinceThickened >= 600;
		}

		public override void Work(Pawn pawn, Map map, float speedMult)
		{
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			Filth filthTarget = FilthTarget;
			if (filthTarget == null || ((Thing)filthTarget).Destroyed)
			{
				return;
			}
			if (totalWorkRequired <= 0f)
			{
				totalWorkRequired = ((Thing)filthTarget).def.filth.cleaningWorkToReduceThickness * (float)filthTarget.thickness;
			}
			float statValueAbstract = StatExtension.GetStatValueAbstract((BuildableDef)(object)GridsUtility.GetTerrain(((Thing)filthTarget).Position, ((Thing)filthTarget).Map), StatDefOf.CleaningTimeFactor, (ThingDef)null);
			float num = speedMult;
			if (statValueAbstract != 0f)
			{
				num /= statValueAbstract;
			}
			cleaningWorkDone += num;
			if (cleaningWorkDone > ((Thing)filthTarget).def.filth.cleaningWorkToReduceThickness && DigitalWorkBudget.AllowCompletion())
			{
				filthTarget.ThinFilth();
				StrikeCount++;
				cleaningWorkDone = 0f;
				if (((Thing)filthTarget).Destroyed && pawn.records != null)
				{
					pawn.records.Increment(RecordDefOf.MessesCleaned);
				}
			}
		}
	}
	public class DigitalTaskAdapter_PlantCut : DigitalTaskAdapter
	{
		private static readonly string[] Homes = new string[1] { "PlantCutting" };

		public override Type WorkGiverClass => typeof(WorkGiver_PlantsCut);

		public override string[] HomeWorkTypes => Homes;

		public override DigitalTask MakeTask(Thing t, CompDigitalWorker comp)
		{
			return new DigitalTask_PlantCut
			{
				target = t,
				comp = comp
			};
		}

		public override bool CanTarget(Pawn pawn, Thing t)
		{
			return t is Plant;
		}
	}
	public class DigitalTask_PlantCut : DigitalTask
	{
		private float workDone;

		private bool asHarvest;

		public override string Label
		{
			get
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_002b: Unknown result type (might be due to invalid IL or missing references)
				//IL_000d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0012: Unknown result type (might be due to invalid IL or missing references)
				if (!asHarvest)
				{
					return ((object)Translator.Translate("DS_Task_Cut")/*cast due to constrained. prefix*/).ToString();
				}
				return ((object)Translator.Translate("DS_Task_CutHarvest")/*cast due to constrained. prefix*/).ToString();
			}
		}

		private Plant PlantTarget
		{
			get
			{
				Thing obj = target;
				return (Plant)(object)((obj is Plant) ? obj : null);
			}
		}

		public override float Progress01
		{
			get
			{
				Plant plantTarget = PlantTarget;
				if (plantTarget == null || ((Thing)plantTarget).def.plant.harvestWork <= 0f)
				{
					return -1f;
				}
				return Mathf.Clamp01(workDone / ((Thing)plantTarget).def.plant.harvestWork);
			}
		}

		public override bool StillValid(Pawn pawn, Map map)
		{
			Plant plantTarget = PlantTarget;
			if (plantTarget == null || ((Thing)plantTarget).Destroyed || !((Thing)plantTarget).Spawned)
			{
				return false;
			}
			if (map.designationManager.DesignationOn((Thing)(object)plantTarget, DesignationDefOf.HarvestPlant) != null)
			{
				asHarvest = true;
				return true;
			}
			if (map.designationManager.DesignationOn((Thing)(object)plantTarget, DesignationDefOf.CutPlant) != null)
			{
				asHarvest = false;
				return true;
			}
			return false;
		}

		public override void Work(Pawn pawn, Map map, float speedMult)
		{
			Plant plantTarget = PlantTarget;
			if (plantTarget != null && !((Thing)plantTarget).Destroyed)
			{
				workDone += DigitalPlantWork.WorkPerTick(speedMult, plantTarget);
				StrikeCount++;
				if (!(workDone < ((Thing)plantTarget).def.plant.harvestWork) && DigitalWorkBudget.AllowCompletion())
				{
					DigitalPlantWork.Harvest(pawn, plantTarget, asHarvest);
					workDone = 0f;
				}
			}
		}
	}
	public class DigitalTaskAdapter_Deconstruct : DigitalTaskAdapter
	{
		private static readonly string[] Homes = new string[2] { "Construction", "FSFDeconstruct" };

		public override Type WorkGiverClass => typeof(WorkGiver_Deconstruct);

		public override string[] HomeWorkTypes => Homes;

		public override DigitalTask MakeTask(Thing t, CompDigitalWorker comp)
		{
			return new DigitalTask_Deconstruct
			{
				target = t,
				comp = comp
			};
		}

		public override bool CanTarget(Pawn pawn, Thing t)
		{
			if (!(t is Building))
			{
				return t is MinifiedThing;
			}
			return true;
		}
	}
	public class DigitalTask_Deconstruct : DigitalTask
	{
		private const float MaxDeconstructWork = 3000f;

		private const float MinDeconstructWork = 20f;

		private float workLeft;

		private float totalNeededWork;

		public override string Label => ((object)Translator.Translate("DS_Task_Deconstruct")/*cast due to constrained. prefix*/).ToString();

		private Building BuildingTarget
		{
			get
			{
				if (target != null)
				{
					Thing innerIfMinified = MinifyUtility.GetInnerIfMinified(target);
					return (Building)(object)((innerIfMinified is Building) ? innerIfMinified : null);
				}
				return null;
			}
		}

		public override float Progress01
		{
			get
			{
				if (!(totalNeededWork <= 0f))
				{
					return Mathf.Clamp01(1f - workLeft / totalNeededWork);
				}
				return -1f;
			}
		}

		public override bool StillValid(Pawn pawn, Map map)
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			Building buildingTarget = BuildingTarget;
			if (buildingTarget == null || ((Thing)buildingTarget).Destroyed || !((Thing)buildingTarget).Spawned)
			{
				return false;
			}
			if (((Thing)pawn).Faction != null && !AcceptanceReport.op_Implicit(buildingTarget.DeconstructibleBy(((Thing)pawn).Faction)))
			{
				return false;
			}
			if (map.designationManager.DesignationOn(target, DesignationDefOf.Deconstruct) == null)
			{
				return false;
			}
			CompExplosive val = ThingCompUtility.TryGetComp<CompExplosive>((Thing)(object)buildingTarget);
			if (val != null)
			{
				return !val.wickStarted;
			}
			return true;
		}

		public override void Work(Pawn pawn, Map map, float speedMult)
		{
			Building buildingTarget = BuildingTarget;
			if (buildingTarget == null || ((Thing)buildingTarget).Destroyed)
			{
				return;
			}
			if (totalNeededWork <= 0f)
			{
				totalNeededWork = Mathf.Clamp(StatExtension.GetStatValue((Thing)(object)buildingTarget, StatDefOf.WorkToBuild, true, -1), 20f, 3000f);
				workLeft = totalNeededWork;
			}
			workLeft -= speedMult * 1.7f;
			StrikeCount++;
			if (!(workLeft > 0f) && DigitalWorkBudget.AllowCompletion())
			{
				Thing val = target;
				if (val.Faction != null)
				{
					val.Faction.Notify_BuildingRemoved(buildingTarget, pawn);
				}
				val.Destroy((DestroyMode)4);
				if (pawn.records != null)
				{
					pawn.records.Increment(RecordDefOf.ThingsDeconstructed);
				}
				map.designationManager.RemoveAllDesignationsOn(val, false);
			}
		}
	}
	internal static class DigitalPlantWork
	{
		public static void ResetWantedPlantDef()
		{
			GrowerStaticAccess.SetWantedPlantDef(null);
		}

		public static float WorkPerTick(float speedMult, Plant plant)
		{
			return speedMult * Mathf.Lerp(3.3f, 1f, plant.Growth);
		}

		public static void Harvest(Pawn pawn, Plant plant, bool asHarvest)
		{
			//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			if (pawn == null || plant == null || ((Thing)plant).Destroyed || !((Thing)plant).Spawned)
			{
				return;
			}
			Map map = ((Thing)plant).Map;
			if (map == null)
			{
				return;
			}
			if (((Thing)plant).def.plant.harvestedThingDef != null)
			{
				StatDef val = ((((Thing)plant).def.plant.harvestedThingDef.IsDrug || ((Thing)plant).def.plant.drugForHarvestPurposes) ? StatDefOf.DrugHarvestYield : StatDefOf.PlantHarvestYield);
				float statValue = StatExtension.GetStatValue((Thing)(object)pawn, val, true, -1);
				int num = plant.YieldNow();
				if (statValue > 1f)
				{
					num = GenMath.RoundRandom((float)num * statValue);
				}
				if (num > 0)
				{
					Thing val2 = ThingMaker.MakeThing(((Thing)plant).def.plant.harvestedThingDef, (ThingDef)null);
					val2.stackCount = num;
					Find.QuestManager.Notify_PlantHarvested(pawn, val2);
					GenPlace.TryPlaceThing(val2, ((Thing)plant).Position, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					if (pawn.records != null)
					{
						pawn.records.Increment(RecordDefOf.PlantsHarvested);
					}
				}
				if (plant.HarvestableNow)
				{
					List<ThingComp> allComps = ((ThingWithComps)plant).AllComps;
					for (int i = 0; i < allComps.Count; i++)
					{
						foreach (ThingDefCountClass item in allComps[i].GetAdditionalHarvestYield())
						{
							Thing obj = ThingMaker.MakeThing(item.thingDef, (ThingDef)null);
							obj.stackCount = item.count;
							GenPlace.TryPlaceThing(obj, ((Thing)plant).Position, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
						}
					}
				}
			}
			if (((Thing)plant).def.plant.soundHarvestFinish != null)
			{
				SoundStarter.PlayOneShot(((Thing)plant).def.plant.soundHarvestFinish, SoundInfo.op_Implicit((Thing)(object)plant));
			}
			plant.PlantCollected(pawn, (PlantDestructionMode)(asHarvest ? 2 : 3));
			if (asHarvest)
			{
				Designation val3 = map.designationManager.DesignationOn((Thing)(object)plant, DesignationDefOf.HarvestPlant);
				if (val3 != null)
				{
					map.designationManager.RemoveDesignation(val3);
				}
			}
			else if (!((Thing)plant).Destroyed && ((Thing)plant).Spawned)
			{
				((Thing)plant).Destroy((DestroyMode)0);
			}
		}
	}
	internal class GrowerStaticAccess : WorkGiver_Grower
	{
		public static void SetWantedPlantDef(ThingDef def)
		{
			WorkGiver_Grower.wantedPlantDef = def;
		}

		public static ThingDef GetWantedPlantDef()
		{
			return WorkGiver_Grower.wantedPlantDef;
		}
	}
	public abstract class DigitalTaskAdapter_CellOnly : DigitalTaskAdapter
	{
		public override bool CellBased => true;

		public override bool TrustWorkGiver => false;

		public override DigitalTask MakeTask(Thing t, CompDigitalWorker comp)
		{
			return null;
		}
	}
	public class DigitalTaskAdapter_GrowerSow : DigitalTaskAdapter_CellOnly
	{
		private static readonly string[] Homes = new string[1] { "Growing" };

		public override Type WorkGiverClass => typeof(WorkGiver_GrowerSow);

		public override string[] HomeWorkTypes => Homes;

		public override DigitalTask MakeCellTask(IntVec3 cell, Map map, Pawn pawn, WorkGiver_Scanner scanner, CompDigitalWorker comp)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			DigitalPlantWork.ResetWantedPlantDef();
			Job val;
			try
			{
				val = scanner.JobOnCell(pawn, cell, false);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 问播种 WorkGiver 时出错：" + ex, 7731);
				return null;
			}
			if (val == null || val.def != JobDefOf.Sow || val.plantDefToSow == null)
			{
				return null;
			}
			return new DigitalTask_PlantSow
			{
				comp = comp,
				cell = cell,
				plantDef = val.plantDefToSow
			};
		}
	}
	public class DigitalTask_PlantSow : DigitalTask
	{
		public IntVec3 cell;

		public ThingDef plantDef;

		private float workDone;

		private bool done;

		public override string Label => ((object)Translator.Translate("DS_Task_Sow")/*cast due to constrained. prefix*/).ToString();

		public override IntVec3 TargetCell => cell;

		public override bool Finished
		{
			get
			{
				if (!done)
				{
					return Abort;
				}
				return true;
			}
		}

		public override string TargetLabel
		{
			get
			{
				//IL_000e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0013: Unknown result type (might be due to invalid IL or missing references)
				string text = ((plantDef == null) ? "?" : ((object)((Def)plantDef).LabelCap/*cast due to constrained. prefix*/).ToString());
				return text + " (" + cell.x + ", " + cell.z + ")";
			}
		}

		public override float Progress01
		{
			get
			{
				if (plantDef == null || plantDef.plant == null || plantDef.plant.sowWork <= 0f)
				{
					return -1f;
				}
				return Mathf.Clamp01(workDone / plantDef.plant.sowWork);
			}
		}

		public override void Claim(Map map, CompDigitalWorker me)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			DigitalWorkerClaims.TryClaimCell(map, cell, me);
		}

		public override void Release(Map map, CompDigitalWorker me)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			DigitalWorkerClaims.ReleaseCell(map, cell, me);
		}

		public override bool StillValid(Pawn pawn, Map map)
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			if (Abort || done)
			{
				return false;
			}
			if (plantDef == null || !((IntVec3)(ref cell)).IsValid || !GenGrid.InBounds(cell, map))
			{
				return false;
			}
			List<Thing> thingList = GridsUtility.GetThingList(cell, map);
			for (int i = 0; i < thingList.Count; i++)
			{
				if (thingList[i].def == plantDef)
				{
					return false;
				}
			}
			IPlantToGrowSettable plantToGrowSettable = GridsUtility.GetPlantToGrowSettable(cell, map);
			if (plantToGrowSettable != null)
			{
				return plantToGrowSettable.GetPlantDefToGrow() == plantDef;
			}
			return false;
		}

		public override void Work(Pawn pawn, Map map, float speedMult)
		{
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
			if (Abort || done || plantDef == null)
			{
				return;
			}
			workDone += speedMult;
			StrikeCount++;
			if (workDone < plantDef.plant.sowWork || !DigitalWorkBudget.AllowCompletion())
			{
				return;
			}
			if (PlantUtility.AdjacentSowBlocker(plantDef, cell, map) != null || !PlantUtility.CanNowPlantAt(plantDef, cell, map, false) || !PlantUtility.GrowthSeasonNow(cell, map, plantDef))
			{
				Abort = true;
				return;
			}
			Thing obj = GenSpawn.Spawn(plantDef, cell, map, (WipeMode)0);
			Plant val = (Plant)(object)((obj is Plant) ? obj : null);
			if (val == null)
			{
				Abort = true;
				return;
			}
			val.Growth = 0f;
			val.sown = true;
			val.Growth = 0.0001f;
			map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDef.op_Implicit(MapMeshFlagDefOf.Things));
			if (pawn.records != null)
			{
				pawn.records.Increment(RecordDefOf.PlantsSown);
			}
			done = true;
		}
	}
	public class DigitalTaskAdapter_GrowerHarvest : DigitalTaskAdapter_CellOnly
	{
		private static readonly string[] Homes = new string[2] { "Growing", "FSFHarvesting" };

		public override Type WorkGiverClass => typeof(WorkGiver_GrowerHarvest);

		public override string[] HomeWorkTypes => Homes;

		public override DigitalTask MakeCellTask(IntVec3 cell, Map map, Pawn pawn, WorkGiver_Scanner scanner, CompDigitalWorker comp)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			DigitalPlantWork.ResetWantedPlantDef();
			bool flag;
			try
			{
				flag = scanner.HasJobOnCell(pawn, cell, false);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 问收割 WorkGiver 时出错：" + ex, 7733);
				return null;
			}
			if (!flag)
			{
				return null;
			}
			Plant plant = GridsUtility.GetPlant(cell, map);
			if (plant == null || ((Thing)plant).Destroyed || !((Thing)plant).Spawned)
			{
				return null;
			}
			return new DigitalTask_PlantHarvest
			{
				target = (Thing)(object)plant,
				comp = comp
			};
		}
	}
	public class DigitalTask_PlantHarvest : DigitalTask
	{
		private float workDone;

		public override string Label => ((object)Translator.Translate("DS_Task_Reap")/*cast due to constrained. prefix*/).ToString();

		public override float Progress01
		{
			get
			{
				Thing obj = target;
				Plant val = (Plant)(object)((obj is Plant) ? obj : null);
				if (val == null || ((Thing)val).def.plant.harvestWork <= 0f)
				{
					return -1f;
				}
				return Mathf.Clamp01(workDone / ((Thing)val).def.plant.harvestWork);
			}
		}

		public override bool StillValid(Pawn pawn, Map map)
		{
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Invalid comparison between Unknown and I4
			Thing obj = target;
			Plant val = (Plant)(object)((obj is Plant) ? obj : null);
			if (val == null || ((Thing)val).Destroyed || !((Thing)val).Spawned)
			{
				return false;
			}
			if (val.HarvestableNow)
			{
				return (int)val.LifeStage == 2;
			}
			return false;
		}

		public override void Work(Pawn pawn, Map map, float speedMult)
		{
			Thing obj = target;
			Plant val = (Plant)(object)((obj is Plant) ? obj : null);
			if (val != null && !((Thing)val).Destroyed)
			{
				workDone += DigitalPlantWork.WorkPerTick(speedMult, val);
				StrikeCount++;
				if (!(workDone < ((Thing)val).def.plant.harvestWork) && DigitalWorkBudget.AllowCompletion())
				{
					DigitalPlantWork.Harvest(pawn, val, asHarvest: true);
					workDone = 0f;
				}
			}
		}
	}
	internal static class DigitalWorkBudget
	{
		private static int tick = -1;

		private static int left;

		public static bool AllowCompletion()
		{
			int workerCompletionsPerTick = DigitalStorageSettings.workerCompletionsPerTick;
			if (workerCompletionsPerTick <= 0)
			{
				return true;
			}
			int ticksGame = GenTicks.TicksGame;
			if (ticksGame != tick)
			{
				tick = ticksGame;
				left = workerCompletionsPerTick;
			}
			if (left <= 0)
			{
				return false;
			}
			left--;
			return true;
		}
	}
	internal static class DigitalWorkerFactory
	{
		private static BackstoryDef safeChildhood;

		private static BackstoryDef safeAdulthood;

		private static bool backstoriesResolved;

		public static Pawn Create(int skillLevel, string nameShort, string nameNick)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Expected O, but got Unknown
			try
			{
				Pawn val = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer, (PlanetTile?)null);
				val.Name = (Name)new NameTriple("", nameShort, nameNick);
				PawnComponentsUtility.AddComponentsForSpawn(val);
				val.mindState.Active = false;
				if (val.story != null && val.story.traits != null && val.story.traits.allTraits != null)
				{
					val.story.traits.allTraits.Clear();
				}
				NeutralizeBackstories(val);
				if (val.relations != null)
				{
					val.relations.ClearAllRelations();
				}
				if (val.skills != null)
				{
					for (int i = 0; i < val.skills.skills.Count; i++)
					{
						val.skills.skills[i].Level = skillLevel;
					}
				}
				val.Notify_DisabledWorkTypesChanged();
				if (val.workSettings != null)
				{
					val.workSettings.EnableAndInitialize();
				}
				WarnIfSkillsStillDisabled(val);
				return val;
			}
			catch (Exception ex)
			{
				Log.Error("[DigitalStorage] 生成数字工人失败：" + ex);
				return null;
			}
		}

		private static void NeutralizeBackstories(Pawn p)
		{
			try
			{
				if (p != null && p.story != null)
				{
					if (!backstoriesResolved)
					{
						backstoriesResolved = true;
						safeChildhood = SafeBackstory((BackstorySlot)0);
						safeAdulthood = SafeBackstory((BackstorySlot)1);
					}
					if (safeChildhood != null && p.story.Childhood != safeChildhood)
					{
						p.story.Childhood = safeChildhood;
					}
					if (safeAdulthood != null && p.story.Adulthood != safeAdulthood)
					{
						p.story.Adulthood = safeAdulthood;
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 数字工人背景替换失败（沿用随机背景继续）：" + ex, 1146307158);
			}
		}

		private unsafe static BackstoryDef SafeBackstory(BackstorySlot slot)
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			List<BackstoryDef> allDefsListForReading = DefDatabase<BackstoryDef>.AllDefsListForReading;
			BackstoryDef val = null;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				BackstoryDef val2 = allDefsListForReading[i];
				if (val2 != null && val2.slot == slot && (int)val2.workDisables == 0 && ((int)slot != 0 || !val2.IsPlayerColonyChildBackstory) && (val == null || string.CompareOrdinal(((Def)val2).defName, ((Def)val).defName) < 0))
				{
					val = val2;
				}
			}
			if (val == null)
			{
				Log.WarningOnce("[DigitalStorage] 找不到不禁任何工作标签的 " + ((object)(*(BackstorySlot*)(&slot))/*cast due to constrained. prefix*/).ToString() + " 背景：数字工人会沿用随机背景，技能可能被读成 0。", 1146307159);
			}
			return val;
		}

		private static void WarnIfSkillsStillDisabled(Pawn p)
		{
			if (!DigitalStorageSettings.enableDebugLog || p == null || p.skills == null || p.skills.skills == null)
			{
				return;
			}
			string text = null;
			for (int i = 0; i < p.skills.skills.Count; i++)
			{
				SkillRecord val = p.skills.skills[i];
				if (val != null && val.def != null && val.TotallyDisabled)
				{
					text = ((text == null) ? ((Def)val.def).defName : (text + ", " + ((Def)val.def).defName));
				}
			}
			if (text != null)
			{
				Log.WarningOnce("[DigitalStorage] 数字工人仍有被禁的技能（" + text + "）：背景已换成不禁工作标签的，来源应在基因或 hediff。", 1146307160);
			}
		}
	}
	internal static class BillAutomationScope
	{
		public static bool SuppressCraftLetters;
	}
	internal static class BillBenchClaims
	{
		private static readonly Dictionary<Map, Dictionary<Thing, CompBillAutomation>> claims = new Dictionary<Map, Dictionary<Thing, CompBillAutomation>>();

		private static readonly List<Thing> tmp = new List<Thing>();

		private static Dictionary<Thing, CompBillAutomation> For(Map map)
		{
			if (!claims.TryGetValue(map, out var value))
			{
				value = new Dictionary<Thing, CompBillAutomation>();
				claims[map] = value;
			}
			return value;
		}

		public static CompBillAutomation OwnerOf(Map map, Thing bench)
		{
			if (map == null || bench == null)
			{
				return null;
			}
			if (!claims.TryGetValue(map, out var value))
			{
				return null;
			}
			if (!value.TryGetValue(bench, out var value2))
			{
				return null;
			}
			return value2;
		}

		public static bool TryClaim(Map map, Thing bench, CompBillAutomation me)
		{
			if (map == null || bench == null || me == null)
			{
				return false;
			}
			Dictionary<Thing, CompBillAutomation> dictionary = For(map);
			if (dictionary.TryGetValue(bench, out var value) && value != null && value != me)
			{
				return false;
			}
			dictionary[bench] = me;
			return true;
		}

		public static void Release(Map map, Thing bench, CompBillAutomation me)
		{
			if (map != null && bench != null && claims.TryGetValue(map, out var value))
			{
				if (value.TryGetValue(bench, out var value2) && value2 == me)
				{
					value.Remove(bench);
				}
				if (value.Count == 0)
				{
					claims.Remove(map);
				}
			}
		}

		public static void ReleaseAll(Map map, CompBillAutomation me)
		{
			if (map == null || me == null || !claims.TryGetValue(map, out var value))
			{
				return;
			}
			tmp.Clear();
			foreach (KeyValuePair<Thing, CompBillAutomation> item in value)
			{
				if (item.Value == me)
				{
					tmp.Add(item.Key);
				}
			}
			for (int i = 0; i < tmp.Count; i++)
			{
				value.Remove(tmp[i]);
			}
			tmp.Clear();
			if (value.Count == 0)
			{
				claims.Remove(map);
			}
		}

		public static void ClearAll()
		{
			claims.Clear();
			tmp.Clear();
		}
	}
	internal static class CraftIngredientClaims
	{
		private static readonly Dictionary<Thing, object> claims = new Dictionary<Thing, object>();

		private static readonly List<Thing> tmp = new List<Thing>();

		private static int callsSinceSweep;

		public static object OwnerOf(Thing t)
		{
			if (t == null)
			{
				return null;
			}
			if (!claims.TryGetValue(t, out var value))
			{
				return null;
			}
			return value;
		}

		public static bool TryClaim(Thing t, object owner)
		{
			if (t == null || owner == null)
			{
				return false;
			}
			if (++callsSinceSweep >= 400)
			{
				callsSinceSweep = 0;
				Sweep();
			}
			if (claims.TryGetValue(t, out var value) && value != null && value != owner)
			{
				return false;
			}
			claims[t] = owner;
			return true;
		}

		public static void Release(object owner)
		{
			if (owner == null)
			{
				return;
			}
			tmp.Clear();
			foreach (KeyValuePair<Thing, object> claim in claims)
			{
				if (claim.Value == owner)
				{
					tmp.Add(claim.Key);
				}
			}
			for (int i = 0; i < tmp.Count; i++)
			{
				claims.Remove(tmp[i]);
			}
			tmp.Clear();
		}

		public static void Clear()
		{
			claims.Clear();
			tmp.Clear();
			callsSinceSweep = 0;
		}

		private static void Sweep()
		{
			tmp.Clear();
			foreach (KeyValuePair<Thing, object> claim in claims)
			{
				Thing key = claim.Key;
				CraftLine craftLine = claim.Value as CraftLine;
				if (key == null || key.Destroyed || key.holdingOwner == null || (craftLine != null && craftLine.Bench == null))
				{
					tmp.Add(key);
				}
			}
			for (int i = 0; i < tmp.Count; i++)
			{
				if (tmp[i] != null)
				{
					claims.Remove(tmp[i]);
				}
			}
			tmp.Clear();
		}
	}
	internal sealed class CraftPlan : IExposable
	{
		public const int ModeForever = 0;

		public const int ModeCount = 1;

		public const int ModeTarget = 2;

		public RecipeDef recipe;

		public ThingDef allowedStuff;

		public ThingStyleDef styleDef;

		public bool FromJob;

		public int mode;

		public int remaining;

		public int targetCount = 10;

		public bool pauseWhenSatisfied;

		public bool paused;

		public bool suspended;

		public int completed;

		public readonly List<CraftLine> lines = new List<CraftLine>();

		public int countedCount;

		public int countedTick;

		public bool wantsWork = true;

		public Bill_Production CountBill;

		public BillStack CountStack;

		public bool Done
		{
			get
			{
				if (mode == 1)
				{
					return remaining <= 0;
				}
				return false;
			}
		}

		public bool Maintained
		{
			get
			{
				if (!suspended && !Done)
				{
					return recipe != null;
				}
				return false;
			}
		}

		public bool CanStartNewWork
		{
			get
			{
				if (!Maintained)
				{
					return false;
				}
				if (mode == 2)
				{
					return wantsWork;
				}
				return true;
			}
		}

		public int ResumeAt => Mathf.Max(0, targetCount - Mathf.Max(1, targetCount * 2 / 5));

		public void AddCount(int n)
		{
			if (mode == 2)
			{
				targetCount = Mathf.Max(0, targetCount + n);
				paused = false;
				wantsWork = true;
				return;
			}
			if (mode != 1)
			{
				mode = 1;
				remaining = 0;
			}
			remaining += n;
			if (remaining < 0)
			{
				remaining = 0;
			}
			suspended = false;
		}

		public void SetForever()
		{
			mode = 0;
			remaining = 0;
			suspended = false;
			wantsWork = true;
		}

		public void SetTarget(int target)
		{
			mode = 2;
			targetCount = Mathf.Max(0, target);
			suspended = false;
			paused = false;
			wantsWork = true;
			countedTick = 0;
		}

		public bool UpdateTargetState(int counted)
		{
			if (mode != 2)
			{
				return true;
			}
			if (pauseWhenSatisfied && counted >= targetCount)
			{
				paused = true;
			}
			if (counted <= ResumeAt || !pauseWhenSatisfied)
			{
				paused = false;
			}
			if (paused)
			{
				return false;
			}
			return counted < targetCount;
		}

		public void NoteCompleted()
		{
			completed++;
			if (mode == 1 && remaining > 0)
			{
				remaining--;
			}
			countedTick = 0;
		}

		public void ExposeData()
		{
			Scribe_Defs.Look<RecipeDef>(ref recipe, "recipe");
			Scribe_Values.Look<int>(ref mode, "mode", 0, false);
			Scribe_Values.Look<int>(ref remaining, "remaining", 0, false);
			Scribe_Values.Look<int>(ref targetCount, "targetCount", 10, false);
			Scribe_Values.Look<bool>(ref pauseWhenSatisfied, "pauseWhenSatisfied", false, false);
			Scribe_Values.Look<bool>(ref paused, "paused", false, false);
			Scribe_Values.Look<bool>(ref suspended, "suspended", false, false);
			Scribe_Values.Look<int>(ref completed, "completed", 0, false);
			Scribe_Defs.Look<ThingDef>(ref allowedStuff, "allowedStuff");
			Scribe_Defs.Look<ThingStyleDef>(ref styleDef, "styleDef");
			Scribe_Values.Look<bool>(ref FromJob, "fromJob", false, false);
		}
	}
	internal enum CraftResult
	{
		Done,
		NoBudget,
		Invalid
	}
	internal sealed class CraftLine
	{
		public CraftPlan Plan;

		public Thing Bench;

		public Bill_Production Bill;

		public BillStack TempStack;

		public Job Probe;

		public Thing[] Ingredients;

		public int[] Counts;

		public ThingDef StuffDef;

		public ThingDef AllowedStuff;

		public int BlockedSinceTick;

		public float BaseRate = 1f;

		public float WorkAmount;

		public float WorkLeft;

		public int NextAcquireTick;

		public string BlockKey;

		public bool Expanded;

		public string ExpandNote;

		public Mote_DS_WorkHand Hand;

		public Effecter Bar;

		private float strikeTimer;

		public bool HasWork
		{
			get
			{
				if (Bill != null)
				{
					return Ingredients != null;
				}
				return false;
			}
		}

		public float Progress01
		{
			get
			{
				if (WorkAmount <= 0f)
				{
					return 1f;
				}
				return Mathf.Clamp01(1f - WorkLeft / WorkAmount);
			}
		}

		public void ClearWork()
		{
			if (Ingredients != null)
			{
				CraftIngredientClaims.Release(this);
			}
			Probe = null;
			Ingredients = null;
			Counts = null;
			StuffDef = null;
			AllowedStuff = null;
			BlockedSinceTick = 0;
			BaseRate = 1f;
			WorkAmount = 0f;
			WorkLeft = 0f;
			Expanded = false;
			ExpandNote = null;
		}

		public void ClearAll()
		{
			ClearWork();
			CleanupVisual();
			Bench = null;
			Bill = null;
			TempStack = null;
			Plan = null;
			NextAcquireTick = 0;
			BlockKey = null;
		}

		public void TickVisual(Map map, int index, int cap)
		{
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0128: Unknown result type (might be due to invalid IL or missing references)
			//IL_012d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			Thing bench = Bench;
			if (index >= cap || bench == null || bench.Destroyed || !bench.Spawned || !HasWork)
			{
				CleanupVisual();
				return;
			}
			if (Hand == null || ((Thing)Hand).Destroyed || !((Thing)Hand).Spawned)
			{
				ThingDef namedSilentFail = DefDatabase<ThingDef>.GetNamedSilentFail("DS_WorkHand");
				if (namedSilentFail != null && ThingMaker.MakeThing(namedSilentFail, (ThingDef)null) is Mote_DS_WorkHand mote_DS_WorkHand)
				{
					GenSpawn.Spawn((Thing)(object)mote_DS_WorkHand, bench.PositionHeld, map, (WipeMode)0);
					Hand = mote_DS_WorkHand;
				}
			}
			if (Hand != null && !((Thing)Hand).Destroyed)
			{
				Vector3 drawPos = bench.DrawPos;
				drawPos.y = 0f;
				drawPos.z += 0.15f;
				((Mote)Hand).exactPosition = drawPos;
				((Mote)Hand).Maintain();
				strikeTimer -= 1f;
				if (strikeTimer <= 0f)
				{
					strikeTimer = 30f;
					Hand.Strike();
				}
			}
			if (Bar == null)
			{
				Bar = EffecterDefOf.ProgressBar.Spawn();
			}
			Bar.EffectTick(new TargetInfo(bench), TargetInfo.Invalid);
			object obj;
			if (Bar.children.Count <= 0)
			{
				obj = null;
			}
			else
			{
				SubEffecter obj2 = Bar.children[0];
				obj = ((SubEffecter_ProgressBar)(((obj2 is SubEffecter_ProgressBar) ? obj2 : null)?)).mote;
			}
			MoteProgressBar val = (MoteProgressBar)obj;
			if (val != null)
			{
				val.progress = Progress01;
				val.offsetZ = -0.5f;
				val.alwaysShow = true;
			}
		}

		public void CleanupVisual()
		{
			if (Hand != null && !((Thing)Hand).Destroyed)
			{
				((Thing)Hand).Destroy((DestroyMode)0);
			}
			Hand = null;
			if (Bar != null)
			{
				Bar.Cleanup();
				Bar = null;
			}
			strikeTimer = 0f;
		}
	}
	internal static class CraftUnlocks
	{
		private static readonly Dictionary<ThingDef, List<RecipeDef>> cache = new Dictionary<ThingDef, List<RecipeDef>>();

		private static readonly HashSet<RecipeDef> tmpSeen = new HashSet<RecipeDef>();

		public static List<RecipeDef> For(ThingDef benchDef)
		{
			if (cache.TryGetValue(benchDef, out var value))
			{
				return value;
			}
			value = new List<RecipeDef>();
			List<RecipeDef> allRecipes = benchDef.AllRecipes;
			if (allRecipes != null)
			{
				for (int i = 0; i < allRecipes.Count; i++)
				{
					RecipeDef val = allRecipes[i];
					if (IsAutomationCandidate(val))
					{
						value.Add(val);
					}
				}
			}
			cache[benchDef] = value;
			return value;
		}

		private static bool IsAutomationCandidate(RecipeDef r)
		{
			if (r == null)
			{
				return false;
			}
			if (r.IsSurgery)
			{
				return false;
			}
			if (r.products == null || r.products.Count <= 0)
			{
				if (r.specialProducts != null)
				{
					return r.specialProducts.Count > 0;
				}
				return false;
			}
			return true;
		}

		public static void MenuRecipes(HashSet<ThingDef> benchDefs, List<RecipeDef> outList)
		{
			outList.Clear();
			if (benchDefs == null || benchDefs.Count == 0)
			{
				return;
			}
			HashSet<RecipeDef> hashSet = tmpSeen;
			hashSet.Clear();
			foreach (ThingDef benchDef in benchDefs)
			{
				List<RecipeDef> list = For(benchDef);
				for (int i = 0; i < list.Count; i++)
				{
					RecipeDef val = list[i];
					if (!hashSet.Contains(val) && val.AvailableNow)
					{
						hashSet.Add(val);
						outList.Add(val);
					}
				}
			}
			hashSet.Clear();
			outList.Sort((RecipeDef a, RecipeDef b) => string.Compare(TaggedString.op_Implicit(((Def)a).LabelCap), TaggedString.op_Implicit(((Def)b).LabelCap), StringComparison.Ordinal));
		}

		public static void Survey(HashSet<ThingDef> benchDefs, out int total, out int unsupported, out int researchLocked, out int available)
		{
			total = 0;
			unsupported = 0;
			researchLocked = 0;
			available = 0;
			if (benchDefs == null || benchDefs.Count == 0)
			{
				return;
			}
			HashSet<RecipeDef> hashSet = tmpSeen;
			hashSet.Clear();
			foreach (ThingDef benchDef in benchDefs)
			{
				List<RecipeDef> allRecipes = benchDef.AllRecipes;
				if (allRecipes == null)
				{
					continue;
				}
				for (int i = 0; i < allRecipes.Count; i++)
				{
					RecipeDef val = allRecipes[i];
					if (val != null && hashSet.Add(val))
					{
						total++;
						if (!IsAutomationCandidate(val))
						{
							unsupported++;
						}
						else if (!val.AvailableNow)
						{
							researchLocked++;
						}
						else
						{
							available++;
						}
					}
				}
			}
			hashSet.Clear();
		}
	}
	internal enum CraftCategory
	{
		All,
		Weapon,
		Apparel,
		Manufactured,
		Food,
		Medical,
		Building,
		Other
	}
	internal static class CraftCategories
	{
		public static readonly CraftCategory[] Tabs = new CraftCategory[8]
		{
			CraftCategory.All,
			CraftCategory.Weapon,
			CraftCategory.Apparel,
			CraftCategory.Manufactured,
			CraftCategory.Food,
			CraftCategory.Medical,
			CraftCategory.Building,
			CraftCategory.Other
		};

		public static string LabelKey(CraftCategory c)
		{
			return c switch
			{
				CraftCategory.Weapon => "DS_Cat_Weapon", 
				CraftCategory.Apparel => "DS_Cat_Apparel", 
				CraftCategory.Manufactured => "DS_Cat_Manufactured", 
				CraftCategory.Food => "DS_Cat_Food", 
				CraftCategory.Medical => "DS_Cat_Medical", 
				CraftCategory.Building => "DS_Cat_Building", 
				CraftCategory.Other => "DS_Cat_Other", 
				_ => "DS_Cat_All", 
			};
		}

		public static ThingDef MainProduct(RecipeDef r)
		{
			if (r == null)
			{
				return null;
			}
			if (r.ProducedThingDef != null)
			{
				return r.ProducedThingDef;
			}
			if (r.products != null)
			{
				for (int i = 0; i < r.products.Count; i++)
				{
					ThingDefCountClass val = r.products[i];
					if (val != null && val.thingDef != null)
					{
						return val.thingDef;
					}
				}
			}
			return r.UIIconThing;
		}

		public static CraftCategory Of(RecipeDef r)
		{
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Invalid comparison between Unknown and I4
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Invalid comparison between Unknown and I4
			ThingDef val = MainProduct(r);
			if (val == null)
			{
				return CraftCategory.Other;
			}
			if (val.IsWeapon || val.IsRangedWeapon)
			{
				return CraftCategory.Weapon;
			}
			if (val.IsApparel)
			{
				return CraftCategory.Apparel;
			}
			if (val.IsMedicine || val.IsDrug)
			{
				return CraftCategory.Medical;
			}
			IngestibleProperties ingestible = val.ingestible;
			if (ingestible != null && (int)ingestible.foodType != 0)
			{
				return CraftCategory.Food;
			}
			if (val.building != null || (int)val.category == 3)
			{
				return CraftCategory.Building;
			}
			if ((int)val.category == 2)
			{
				return CraftCategory.Manufactured;
			}
			return CraftCategory.Other;
		}
	}
	internal static class BillStackSwap
	{
		private static readonly Dictionary<Type, FieldInfo> fieldCache = new Dictionary<Type, FieldInfo>();

		public static bool TrySwapIn(Thing bench, BillStack replacement, out BillStack original)
		{
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Expected O, but got Unknown
			original = null;
			if (bench == null || replacement == null)
			{
				return false;
			}
			Building_WorkTable val = (Building_WorkTable)(object)((bench is Building_WorkTable) ? bench : null);
			if (val != null)
			{
				original = val.billStack;
				val.billStack = replacement;
				return true;
			}
			FieldInfo fieldInfo = FieldFor(((object)bench).GetType());
			if (fieldInfo == null)
			{
				return false;
			}
			original = (BillStack)fieldInfo.GetValue(bench);
			fieldInfo.SetValue(bench, replacement);
			return true;
		}

		public static void Restore(Thing bench, BillStack original)
		{
			if (bench == null || original == null)
			{
				return;
			}
			Building_WorkTable val = (Building_WorkTable)(object)((bench is Building_WorkTable) ? bench : null);
			if (val != null)
			{
				val.billStack = original;
				return;
			}
			FieldInfo fieldInfo = FieldFor(((object)bench).GetType());
			if (fieldInfo != null)
			{
				fieldInfo.SetValue(bench, original);
			}
		}

		private static FieldInfo FieldFor(Type t)
		{
			if (fieldCache.TryGetValue(t, out var value))
			{
				return value;
			}
			value = AccessTools.Field(t, "billStack");
			if (value != null && !typeof(BillStack).IsAssignableFrom(value.FieldType))
			{
				value = null;
			}
			fieldCache[t] = value;
			return value;
		}
	}
	internal static class BillProbe
	{
		internal const int RetryTicks = 30;

		private const int CraftBackoffTicks = 120;

		private static int lastLogTick = -1;

		private static string lastLogKey;

		private static List<WorkGiverDef> doBillDefs;

		private static void ResetVanillaNegativeCache(Bill bill)
		{
			if (bill != null)
			{
				bill.nextTickToSearchForIngredients = 0;
			}
		}

		private static void LogProbe(CraftLine line, bool ok, int stale, int now)
		{
			string text = (ok ? "OK" : (line.BlockKey ?? "?"));
			string text2 = ((line.Bill != null && ((Bill)line.Bill).recipe != null) ? ((Def)((Bill)line.Bill).recipe).defName : "?");
			string text3 = text + "|" + text2;
			if (!(text3 == lastLogKey) || now - lastLogTick >= 300)
			{
				lastLogKey = text3;
				lastLogTick = now;
				Log.Message("[DigitalStorage] 制作代理取活 " + (ok ? "成功" : ("失败：" + text)) + "｜配方 " + text2 + "｜台子 " + ((line.Bench != null) ? ((Def)line.Bench.def).defName : "无") + ((stale > 0) ? ("｜清掉了原版负缓存（本来是 1.6 的 500~600 tick 致盲，还剩 " + stale + " tick）") : ""));
			}
		}

		private static List<WorkGiverDef> DoBillDefs()
		{
			if (doBillDefs != null)
			{
				return doBillDefs;
			}
			List<WorkGiverDef> list = new List<WorkGiverDef>();
			List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				WorkGiverDef val = allDefsListForReading[i];
				if (val != null && !(val.giverClass == null) && typeof(WorkGiver_DoBill).IsAssignableFrom(val.giverClass))
				{
					list.Add(val);
				}
			}
			list.Sort((WorkGiverDef a, WorkGiverDef b) => string.CompareOrdinal(((Def)a).defName, ((Def)b).defName));
			doBillDefs = list;
			return doBillDefs;
		}

		private static bool AnyGiverClaimsBench(List<WorkGiverDef> defs, ThingDef benchDef)
		{
			if (defs == null || benchDef == null)
			{
				return false;
			}
			for (int i = 0; i < defs.Count; i++)
			{
				WorkGiverDef val = defs[i];
				if (val != null && val.fixedBillGiverDefs != null && val.fixedBillGiverDefs.Contains(benchDef))
				{
					return true;
				}
			}
			return false;
		}

		public static bool TryAcquire(CompBillAutomation comp, CraftPlan plan, CraftLine line, Map map, Pawn w, int now)
		{
			int num = ((line.Bill != null) ? (((Bill)line.Bill).nextTickToSearchForIngredients - now) : 0);
			bool flag = TryAcquireInternal(comp, plan, line, map, w, now);
			if (DigitalStorageSettings.enableDebugLog && (num > 0 || !flag))
			{
				LogProbe(line, flag, num, now);
			}
			return flag;
		}

		private static bool TryAcquireInternal(CompBillAutomation comp, CraftPlan plan, CraftLine line, Map map, Pawn w, int now)
		{
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			line.ClearWork();
			line.BlockKey = "DS_BA_NoBill";
			if (plan == null || plan.recipe == null || !plan.Maintained)
			{
				return false;
			}
			Thing bench = line.Bench;
			if (bench == null || bench.Destroyed || !bench.Spawned)
			{
				return false;
			}
			IBillGiver val = (IBillGiver)(object)((bench is IBillGiver) ? bench : null);
			if (val == null)
			{
				return false;
			}
			if (!val.CurrentlyUsableForBills())
			{
				line.BlockKey = "DS_BA_BenchOff";
				line.NextAcquireTick = now + 30;
				return false;
			}
			if (FireUtility.IsBurning(bench))
			{
				line.BlockKey = "DS_BA_BenchOff";
				line.NextAcquireTick = now + 30;
				return false;
			}
			if (map.reservationManager != null && map.reservationManager.IsReservedByAnyoneOf(LocalTargetInfo.op_Implicit(bench), Faction.OfPlayer))
			{
				line.BlockKey = "DS_BA_BenchBusy";
				line.NextAcquireTick = now + 30;
				return false;
			}
			if (line.Bill == null || line.TempStack == null)
			{
				line.BlockKey = "DS_BA_Block_Unsupported";
				line.NextAcquireTick = now + 120;
				return false;
			}
			List<WorkGiverDef> list = DoBillDefs();
			if (list.Count == 0)
			{
				return false;
			}
			Job val2 = null;
			BillStack original = null;
			bool flag = false;
			DigitalWorkerScope.Enter(w, map, bench.PositionHeld);
			try
			{
				if (!BillStackSwap.TrySwapIn(bench, line.TempStack, out original))
				{
					line.BlockKey = "DS_BA_Block_Unsupported";
					line.NextAcquireTick = now + 120;
					return false;
				}
				flag = true;
				bool flag2 = (BenchWhitelistScope.Relaxed = !AnyGiverClaimsBench(list, bench.def) && DigitalStorageSettings.craftAnyBench);
				for (int i = 0; i < list.Count; i++)
				{
					WorkGiverDef val3 = list[i];
					if (!flag2 && val3.fixedBillGiverDefs != null && !val3.fixedBillGiverDefs.Contains(bench.def))
					{
						continue;
					}
					WorkGiver_DoBill val4;
					try
					{
						WorkGiver worker = val3.Worker;
						val4 = (WorkGiver_DoBill)(object)((worker is WorkGiver_DoBill) ? worker : null);
					}
					catch (Exception)
					{
						continue;
					}
					if (val4 != null)
					{
						ResetVanillaNegativeCache((Bill)(object)line.Bill);
						Job val5;
						try
						{
							val5 = ((WorkGiver_Scanner)val4).JobOnThing(w, bench, false);
						}
						catch (Exception ex2)
						{
							Log.ErrorOnce("[DigitalStorage] 制作代理取活失败（跳过这个 WorkGiver）：" + ((Def)val3).defName + " :: " + ex2, ((Def)val3).shortHash * 31 + 991);
							continue;
						}
						if (val5 != null && val5.bill != null && (object)val5.bill == line.Bill)
						{
							val2 = val5;
							break;
						}
					}
				}
				BenchWhitelistScope.Relaxed = false;
				if (val2 == null)
				{
					string text = Diagnose(bench, plan, map, w);
					if (text == "DS_BA_Block_Material" && RecipeExpansion.TryAcquire(comp, plan, line, map, w, now, bench))
					{
						return true;
					}
					line.BlockKey = text;
					line.NextAcquireTick = now + ((text == "DS_BA_Block_Material") ? 120 : 30);
					return false;
				}
				if (!ExtractIngredients(val2, line, map, out var things, out var counts, out var claimedConflict))
				{
					if (!claimedConflict && RecipeExpansion.TryAcquire(comp, plan, line, map, w, now, bench))
					{
						return true;
					}
					line.BlockKey = (claimedConflict ? "DS_BA_Block_Claimed" : "DS_BA_NoCoreMaterial");
					line.NextAcquireTick = now + 120;
					return false;
				}
				line.Probe = val2;
				line.Ingredients = things;
				line.Counts = counts;
				try
				{
					line.BaseRate = ComputeBaseRate(plan.recipe, bench, w);
					line.AllowedStuff = plan.allowedStuff;
					ApplyMaterial(line, plan);
					line.StuffDef = UftStuffDef(plan.recipe, things);
					line.WorkAmount = WorkAmountFor(plan.recipe, line.Bill, things, val2, line.StuffDef);
					line.WorkLeft = line.WorkAmount;
					line.Expanded = false;
					line.ExpandNote = null;
				}
				catch (Exception ex3)
				{
					Log.ErrorOnce("[DigitalStorage] 制作代理估价失败（本轮作废，原料认领已退回）：" + ex3, 771134);
					line.ClearWork();
					line.BlockKey = "DS_BA_NoBill";
					line.NextAcquireTick = now + 30;
					return false;
				}
				line.NextAcquireTick = 0;
				line.BlockKey = null;
				DevDrawProfiler.Bump("账单取活", 1);
				try
				{
					((Bill)line.Bill).Notify_DoBillStarted(w);
					((Bill)line.Bill).Notify_BillWorkStarted(w);
				}
				catch (Exception ex4)
				{
					Log.ErrorOnce("[DigitalStorage] bill.Notify_* 抛异常（已忽略）：" + ex4, 771133);
				}
			}
			finally
			{
				if (flag)
				{
					BillStackSwap.Restore(bench, original);
				}
				BenchWhitelistScope.Relaxed = false;
				DigitalWorkerScope.Exit(w);
			}
			return true;
		}

		private static string Diagnose(Thing bench, CraftPlan plan, Map map, Pawn w)
		{
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			RecipeDef recipe = plan.recipe;
			if (!recipe.AvailableNow)
			{
				return "DS_BA_Block_Research";
			}
			if (recipe.FirstSkillRequirementPawnDoesntSatisfy(w) != null)
			{
				return "DS_BA_Block_Skill";
			}
			if (!((plan.lines.Count > 0 && plan.lines[0].Bill != null) ? SafeAllowed(plan.lines[0].Bill, w) : SafeAllowedTransient(recipe, bench, w)))
			{
				return "DS_BA_Block_Restricted";
			}
			if (bench.def.hasInteractionCell && w != null && ((Thing)w).SpawnedOrAnyParentSpawned && ((Thing)w).MapHeld == map)
			{
				IntVec3 interactionCell = bench.InteractionCell;
				if (GenGrid.Impassable(interactionCell, map) || ForbidUtility.IsForbidden(interactionCell, w))
				{
					return "DS_BA_Block_Spot";
				}
			}
			return "DS_BA_Block_Material";
		}

		private static bool SafeAllowed(Bill_Production bill, Pawn w)
		{
			try
			{
				bool result = ((Bill)bill).PawnAllowedToStartAnew(w);
				JobFailReason.Clear();
				return result;
			}
			catch (Exception)
			{
				JobFailReason.Clear();
				return true;
			}
		}

		private static bool SafeAllowedTransient(RecipeDef recipe, Thing bench, Pawn w)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Expected O, but got Unknown
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Bill_Production val = new Bill_Production(recipe, (Precept_ThingStyle)null);
				IBillGiver val2 = (IBillGiver)(object)((bench is IBillGiver) ? bench : null);
				if (val2 != null)
				{
					new BillStack(val2).AddBill((Bill)(object)val);
				}
				return SafeAllowed(val, w);
			}
			catch (Exception)
			{
				JobFailReason.Clear();
				return true;
			}
		}

		internal static float ComputeBaseRate(RecipeDef r, Thing bench, Pawn w)
		{
			float num = ((r.workSpeedStat == null) ? 1f : StatExtension.GetStatValue((Thing)(object)w, r.workSpeedStat, true, -1));
			Building_WorkTable val = (Building_WorkTable)(object)((bench is Building_WorkTable) ? bench : null);
			if (r.workTableSpeedStat != null && val != null)
			{
				num *= StatExtension.GetStatValue((Thing)(object)val, r.workTableSpeedStat, true, -1);
			}
			return num;
		}

		private static Thing LastIngredient(Thing[] things, Job job)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			if (things != null && things.Length != 0)
			{
				return things[things.Length - 1];
			}
			LocalTargetInfo target = job.GetTarget((TargetIndex)2);
			return ((LocalTargetInfo)(ref target)).Thing;
		}

		private static void ApplyMaterial(CraftLine line, CraftPlan plan)
		{
			if (line == null || plan == null || plan.allowedStuff == null)
			{
				return;
			}
			Bill_Production bill = line.Bill;
			if (bill == null || ((Bill)bill).ingredientFilter == null)
			{
				return;
			}
			ThingDef allowedStuff = plan.allowedStuff;
			List<ThingDef> allDefsListForReading = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				ThingDef val = allDefsListForReading[i];
				if (val != null && val != allowedStuff)
				{
					((Bill)bill).ingredientFilter.SetAllow(val, false);
				}
			}
			((Bill)bill).ingredientFilter.SetAllow(allowedStuff, true);
		}

		internal static bool RecipeAcceptsStuff(RecipeDef recipe, ThingDef d)
		{
			if (recipe == null || d == null || !d.IsStuff)
			{
				return false;
			}
			if (recipe.defaultIngredientFilter != null && !recipe.defaultIngredientFilter.Allows(d))
			{
				return false;
			}
			if (recipe.ingredients != null)
			{
				for (int i = 0; i < recipe.ingredients.Count; i++)
				{
					IngredientCount val = recipe.ingredients[i];
					if (val != null && !val.IsFixedIngredient && val.filter != null && val.filter.Allows(d))
					{
						return true;
					}
				}
			}
			if (recipe.ingredients != null)
			{
				return recipe.ingredients.Count == 0;
			}
			return true;
		}

		internal static List<ThingDef> UsableStuffs(RecipeDef recipe)
		{
			List<ThingDef> list = new List<ThingDef>();
			if (recipe == null)
			{
				return list;
			}
			List<ThingDef> allDefsListForReading = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				ThingDef val = allDefsListForReading[i];
				if (val != null && val.IsStuff && RecipeAcceptsStuff(recipe, val))
				{
					list.Add(val);
				}
			}
			list.Sort((ThingDef a, ThingDef b) => string.Compare(TaggedString.op_Implicit(((Def)a).LabelCap), TaggedString.op_Implicit(((Def)b).LabelCap), StringComparison.CurrentCulture));
			return list;
		}

		private static ThingDef UftStuffDef(RecipeDef recipe, Thing[] things)
		{
			if (recipe == null || recipe.unfinishedThingDef == null)
			{
				return null;
			}
			if (!((BuildableDef)recipe.unfinishedThingDef).MadeFromStuff)
			{
				return null;
			}
			return BillCraftFunnel.DominantIngredient(recipe, things, null)?.def;
		}

		private static float WorkAmountFor(RecipeDef recipe, Bill_Production bill, Thing[] things, Job got, ThingDef uftStuff)
		{
			if (recipe != null && recipe.unfinishedThingDef != null)
			{
				try
				{
					return recipe.WorkAmountForStuff(uftStuff);
				}
				catch (Exception ex)
				{
					Log.ErrorOnce("[DigitalStorage] 制作代理算不出未完成品配方的工作量（退回普通取值）：" + ((Def)recipe).defName + " :: " + ex, ((Def)recipe).shortHash * 31 + 7717);
				}
			}
			return ((Bill)bill).GetWorkAmount(LastIngredient(things, got));
		}

		private static bool ExtractIngredients(Job job, CraftLine owner, Map map, out Thing[] things, out int[] counts, out bool claimedConflict)
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			claimedConflict = false;
			List<LocalTargetInfo> targetQueueB = job.targetQueueB;
			int num = targetQueueB?.Count ?? 0;
			things = (Thing[])(object)new Thing[num];
			counts = new int[num];
			for (int i = 0; i < num; i++)
			{
				LocalTargetInfo val = targetQueueB[i];
				Thing thing = ((LocalTargetInfo)(ref val)).Thing;
				things[i] = thing;
				counts[i] = ((job.countQueue != null && i < job.countQueue.Count) ? job.countQueue[i] : (thing?.stackCount ?? 0));
				if (thing == null || thing.Destroyed)
				{
					return false;
				}
				if (!(thing.ParentHolder is Building_StorageCore building_StorageCore) || ((Thing)building_StorageCore).Map != map)
				{
					return false;
				}
				if (counts[i] > thing.stackCount)
				{
					return false;
				}
			}
			for (int j = 0; j < num; j++)
			{
				if (!CraftIngredientClaims.TryClaim(things[j], owner))
				{
					CraftIngredientClaims.Release(owner);
					things = (Thing[])(object)new Thing[0];
					counts = new int[0];
					claimedConflict = true;
					return false;
				}
			}
			return true;
		}
	}
	internal static class BenchWhitelistScope
	{
		public static bool Relaxed;
	}
	internal static class BillCraftFunnel
	{
		public static CraftResult TryComplete(CompBillAutomation comp, CraftPlan plan, CraftLine line, Map map, Pawn w)
		{
			if (plan == null || plan.recipe == null || !plan.Maintained)
			{
				return CraftResult.Invalid;
			}
			Bill_Production bill = line.Bill;
			if (bill == null || ((Bill)bill).DeletedOrDereferenced)
			{
				return CraftResult.Invalid;
			}
			if (!plan.recipe.AvailableNow)
			{
				return CraftResult.Invalid;
			}
			Thing bench = line.Bench;
			if (bench == null || bench.Destroyed || !bench.Spawned)
			{
				return CraftResult.Invalid;
			}
			IBillGiver val = (IBillGiver)(object)((bench is IBillGiver) ? bench : null);
			if (val == null)
			{
				return CraftResult.Invalid;
			}
			if (!val.CurrentlyUsableForBills())
			{
				return CraftResult.Invalid;
			}
			if (!DigitalWorkBudget.AllowCompletion())
			{
				return CraftResult.NoBudget;
			}
			List<Thing> list = new List<Thing>();
			List<ThingOwner> list2 = new List<ThingOwner>();
			for (int i = 0; i < line.Ingredients.Length; i++)
			{
				Thing val2 = line.Ingredients[i];
				if (val2 == null || val2.Destroyed)
				{
					Rollback(list, list2);
					return CraftResult.Invalid;
				}
				if (!(val2.ParentHolder is Building_StorageCore building_StorageCore) || ((Thing)building_StorageCore).Map != map)
				{
					Rollback(list, list2);
					return CraftResult.Invalid;
				}
				int num = line.Counts[i];
				if (num <= 0)
				{
					continue;
				}
				if (num >= val2.stackCount)
				{
					list.Add(val2);
					list2.Add(null);
					continue;
				}
				Thing val3 = val2.SplitOff(num);
				if (val3 == null)
				{
					Rollback(list, list2);
					return CraftResult.Invalid;
				}
				list.Add(val3);
				list2.Add(building_StorageCore.GetDirectlyHeldThings());
			}
			Thing val4 = DominantIngredient(((Bill)bill).recipe, list, line.StuffDef);
			bool suppressCraftLetters = BillAutomationScope.SuppressCraftLetters;
			BillAutomationScope.SuppressCraftLetters = true;
			List<Thing> list3;
			try
			{
				list3 = GenRecipe.MakeRecipeProducts(((Bill)bill).recipe, w, list, val4, val, ((Bill)bill).precept, ((Bill)bill).style, ((Bill)bill).graphicIndexOverride).ToList();
			}
			catch (Exception ex)
			{
				Log.Error("[DigitalStorage] 制作代理生成产物失败（本次取消，料已放回）：" + ((Def)((Bill)bill).recipe).defName + " :: " + ex);
				Rollback(list, list2);
				return CraftResult.Invalid;
			}
			finally
			{
				BillAutomationScope.SuppressCraftLetters = suppressCraftLetters;
			}
			for (int j = 0; j < list.Count; j++)
			{
				try
				{
					((Bill)bill).recipe.Worker.ConsumeIngredient(list[j], ((Bill)bill).recipe, map);
				}
				catch (Exception ex2)
				{
					Log.Error("[DigitalStorage] 制作代理扣料失败（这一件没扣掉）：" + ((object)list[j])?.ToString() + " :: " + ex2);
				}
			}
			try
			{
				((Bill)bill).Notify_BillWorkFinished(w);
				((Bill)bill).Notify_IterationCompleted(w, list);
				RecordsUtility.Notify_BillDone(w, list3);
				if (list3.Count > 0)
				{
					Find.QuestManager.Notify_ThingsProduced(w, list3);
				}
			}
			catch (Exception ex3)
			{
				Log.Error("[DigitalStorage] 制作代理 bill 结算回调抛异常（产物已出，忽略）：" + ex3);
			}
			plan.NoteCompleted();
			StoreProducts(comp, plan, list3, map, bench);
			if (bill != null && ((Bill)bill).recipe != null)
			{
				CraftPlan craftPlan = CraftTree.FindPlan(comp, ((Bill)bill).recipe);
				if (craftPlan != null)
				{
					comp.RecountNow(craftPlan);
				}
			}
			comp.RecountNow(plan);
			DevDrawProfiler.Bump("账单完成", 1);
			return CraftResult.Done;
		}

		private static void StoreProducts(CompBillAutomation comp, CraftPlan plan, List<Thing> products, Map map, Thing bench)
		{
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			if (products == null || products.Count == 0)
			{
				return;
			}
			Building_StorageCore building_StorageCore = DigitalDropRedirect.NearestUsableCore(map, bench.PositionHeld);
			IntVec3 positionHeld = bench.PositionHeld;
			if (building_StorageCore == null)
			{
				building_StorageCore = FindNearestCoreIgnoringPower(map, bench.PositionHeld);
				if (building_StorageCore != null)
				{
					Log.ErrorOnce("[DigitalStorage] 没有【已通电】的核心，产物改送最近的核心（" + ((object)((Thing)building_StorageCore).Position/*cast due to constrained. prefix*/).ToString() + "）—— 检查核心供电。", 1146307480);
				}
			}
			for (int i = 0; i < products.Count; i++)
			{
				Thing val = products[i];
				if (val == null || val.Destroyed)
				{
					continue;
				}
				if (building_StorageCore != null && DigitalDropRedirect.TryIngestUnspawned(building_StorageCore, val))
				{
					comp.NoteProduct(val, stored: true);
					continue;
				}
				if (building_StorageCore != null && TryIngestIgnoringFilter(building_StorageCore, val))
				{
					comp.NoteProduct(val, stored: true);
					continue;
				}
				if (GenPlace.TryPlaceThing(val, positionHeld, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
				{
					comp.NoteProduct(val, stored: false);
					continue;
				}
				Log.Error("[DigitalStorage] 制作代理产物既塞不进核心也落不了地，已丢弃：" + (object)val);
				if (!val.Destroyed)
				{
					val.Destroy((DestroyMode)0);
				}
			}
		}

		private static Building_StorageCore FindNearestCoreIgnoringPower(Map map, IntVec3 near)
		{
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			if (map == null)
			{
				return null;
			}
			List<Thing> allThings = map.listerThings.AllThings;
			if (allThings == null)
			{
				return null;
			}
			Building_StorageCore result = null;
			float num = float.MaxValue;
			for (int i = 0; i < allThings.Count; i++)
			{
				if (allThings[i] is Building_StorageCore building_StorageCore && ((Thing)building_StorageCore).Spawned && !((Thing)building_StorageCore).Destroyed)
				{
					IntVec3 val = ((Thing)building_StorageCore).Position - near;
					float num2 = ((IntVec3)(ref val)).LengthHorizontalSquared;
					if (num2 < num)
					{
						num = num2;
						result = building_StorageCore;
					}
				}
			}
			return result;
		}

		private static bool TryIngestIgnoringFilter(Building_StorageCore core, Thing t)
		{
			if (core == null || t == null || t.Destroyed)
			{
				return false;
			}
			if (t.Spawned || t.holdingOwner != null)
			{
				return false;
			}
			int num = core.TryStoreIgnoringFilter(t, t.stackCount);
			if (num <= 0)
			{
				Log.ErrorOnce("[DigitalStorage] 合成产物未进核心：" + ((Def)t.def).defName + " | Spawned=" + ((Thing)core).Spawned + " 入库开关=" + core.HaulDestinationEnabled + " Powered=" + core.Powered + " 容量还能收=" + core.GetDirectlyHeldThings().GetCountCanAccept(t, true) + " ⇒ 将落在台子旁", 1146307481);
			}
			return num > 0;
		}

		internal static Thing DominantIngredient(RecipeDef recipe, IList<Thing> ingredients, ThingDef preferredStuff)
		{
			if (ingredients == null || ingredients.Count == 0)
			{
				return null;
			}
			if (preferredStuff != null)
			{
				for (int i = 0; i < ingredients.Count; i++)
				{
					Thing val = ingredients[i];
					if (val != null && val.def == preferredStuff)
					{
						return val;
					}
				}
			}
			if (recipe.productHasIngredientStuff)
			{
				return ingredients[0];
			}
			bool flag = false;
			if (recipe.products != null)
			{
				for (int j = 0; j < recipe.products.Count; j++)
				{
					ThingDefCountClass val2 = recipe.products[j];
					if (val2 != null && val2.thingDef != null && ((BuildableDef)val2.thingDef).MadeFromStuff)
					{
						flag = true;
						break;
					}
				}
			}
			if (recipe.unfinishedThingDef != null && ((BuildableDef)recipe.unfinishedThingDef).MadeFromStuff)
			{
				flag = true;
			}
			if (flag)
			{
				List<Thing> list = new List<Thing>();
				for (int k = 0; k < ingredients.Count; k++)
				{
					Thing val3 = ingredients[k];
					if (val3 != null && val3.def != null && val3.def.IsStuff)
					{
						list.Add(val3);
					}
				}
				if (list.Count > 0)
				{
					return GenCollection.RandomElementByWeight<Thing>((IEnumerable<Thing>)list, (Func<Thing, float>)((Thing t) => t.stackCount));
				}
				return ingredients[0];
			}
			return GenCollection.RandomElementByWeight<Thing>((IEnumerable<Thing>)ingredients, (Func<Thing, float>)((Thing t) => t.stackCount));
		}

		private static void Rollback(List<Thing> ingredients, List<ThingOwner> owners)
		{
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			for (int i = 0; i < ingredients.Count; i++)
			{
				if (i >= owners.Count || owners[i] == null)
				{
					continue;
				}
				Thing val = ingredients[i];
				if (val == null || val.Destroyed)
				{
					continue;
				}
				ThingOwner val2 = owners[i];
				if (!val2.TryAdd(val, true))
				{
					IThingHolder owner = val2.Owner;
					Thing val3 = (Thing)(object)((owner is Thing) ? owner : null);
					Map val4 = ((val3 == null) ? null : val3.MapHeld);
					if (val4 == null || !GenPlace.TryPlaceThing(val, val3.PositionHeld, val4, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
					{
						Log.Error("[DigitalStorage] 制作代理无法把原料放回容器，也没落成地：" + (object)val);
					}
				}
			}
		}

		public static string DescribeProduct(Thing p)
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			if (p == null || p.def == null)
			{
				return "?";
			}
			string text;
			try
			{
				text = p.LabelNoCount;
			}
			catch
			{
				text = TaggedString.op_Implicit(((Def)p.def).LabelCap);
			}
			QualityCategory val = default(QualityCategory);
			if (QualityUtility.TryGetQuality(p, ref val))
			{
				text = QualityUtility.GetLabel(val) + " " + text;
			}
			return text;
		}
	}
	internal static class RecipeExpansion
	{
		private sealed class Ctx
		{
			public Map map;

			public Thing bench;

			public CraftLine line;

			public RecipeDef finalRecipe;

			public Bill finalBill;

			public HashSet<ThingDef> benchDefs;

			public int maxDepth;

			public float factor;

			public Dictionary<ThingDef, int> avail = new Dictionary<ThingDef, int>();

			public Dictionary<ThingDef, int> need = new Dictionary<ThingDef, int>();

			public List<ThingDef> order = new List<ThingDef>();

			public Dictionary<ThingDef, List<Thing>> stacks = new Dictionary<ThingDef, List<Thing>>();

			public List<RecipeDef> path = new List<RecipeDef>();

			public Dictionary<RecipeDef, Bill> bills = new Dictionary<RecipeDef, Bill>();

			public Dictionary<ThingDef, RecipeDef> producers = new Dictionary<ThingDef, RecipeDef>();

			public float extraWork;

			public List<string> trail = new List<string>();
		}

		private const int MaxNodes = 24;

		public static bool TryAcquire(CompBillAutomation comp, CraftPlan plan, CraftLine line, Map map, Pawn w, int now, Thing bench)
		{
			try
			{
				return TryAcquireCore(comp, plan, line, map, w, now, bench);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 配方展开异常（本轮放弃，按缺料处理）：" + ex, 771139);
				if (line != null)
				{
					CraftIngredientClaims.Release(line);
					line.Expanded = false;
					line.ExpandNote = null;
				}
				return false;
			}
		}

		private static bool TryAcquireCore(CompBillAutomation comp, CraftPlan plan, CraftLine line, Map map, Pawn w, int now, Thing bench)
		{
			if (comp == null || plan == null || plan.recipe == null || line == null)
			{
				return false;
			}
			if (map == null || bench == null || w == null)
			{
				return false;
			}
			int num = Mathf.Clamp(DigitalStorageSettings.craftExpandDepth, 1, 3);
			float factor = Mathf.Clamp01(DigitalStorageSettings.craftExpandWorkFactor);
			Ctx ctx = new Ctx();
			ctx.map = map;
			ctx.bench = bench;
			ctx.line = line;
			ctx.finalRecipe = plan.recipe;
			ctx.finalBill = (Bill)(object)line.Bill;
			ctx.benchDefs = BenchDefs(comp);
			ctx.maxDepth = num;
			ctx.factor = factor;
			if (!FillAvail(ctx))
			{
				return false;
			}
			if (!Accumulate(ctx, plan.recipe, 1, num, out var fail))
			{
				line.ExpandNote = fail;
				return false;
			}
			if (ctx.need.Count == 0)
			{
				line.ExpandNote = "展开后什么都不缺（不该发生）";
				return false;
			}
			if (!Collect(ctx, out var things, out var counts))
			{
				CraftIngredientClaims.Release(line);
				line.ExpandNote = "展开后的原料在核心里凑不齐";
				return false;
			}
			line.Probe = null;
			line.Ingredients = things;
			line.Counts = counts;
			line.Expanded = true;
			line.ExpandNote = Describe(ctx);
			try
			{
				line.BaseRate = BillProbe.ComputeBaseRate(plan.recipe, bench, w);
				Thing val = ((things.Length != 0) ? things[things.Length - 1] : null);
				line.WorkAmount = ((Bill)line.Bill).GetWorkAmount(val) + ctx.extraWork;
				line.WorkLeft = line.WorkAmount;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 配方展开估价失败（本轮作废，原料认领已退回）：" + ex, 771137);
				line.ClearWork();
				line.BlockKey = "DS_BA_NoBill";
				line.NextAcquireTick = now + 30;
				return false;
			}
			line.NextAcquireTick = 0;
			line.BlockKey = null;
			DevDrawProfiler.Bump("账单取活", 1);
			try
			{
				((Bill)line.Bill).Notify_DoBillStarted(w);
				((Bill)line.Bill).Notify_BillWorkStarted(w);
			}
			catch (Exception ex2)
			{
				Log.ErrorOnce("[DigitalStorage] bill.Notify_* 抛异常（已忽略）：" + ex2, 771133);
			}
			return true;
		}

		private static bool FillAvail(Ctx ctx)
		{
			List<Thing> list = new List<Thing>();
			HaulSourceContents.CollectDirect(ctx.map, list);
			for (int i = 0; i < list.Count; i++)
			{
				Thing val = list[i];
				if (val != null && !val.Destroyed && val.def != null && val.stackCount > 0 && val.ParentHolder is Building_StorageCore building_StorageCore && ((Thing)building_StorageCore).Map == ctx.map)
				{
					ctx.avail.TryGetValue(val.def, out var value);
					ctx.avail[val.def] = value + val.stackCount;
					if (!ctx.stacks.TryGetValue(val.def, out var value2))
					{
						value2 = new List<Thing>();
						ctx.stacks[val.def] = value2;
					}
					value2.Add(val);
				}
			}
			foreach (KeyValuePair<ThingDef, List<Thing>> stack in ctx.stacks)
			{
				stack.Value.Sort((Thing a, Thing b) => b.stackCount.CompareTo(a.stackCount));
			}
			return ctx.avail.Count > 0;
		}

		private static HashSet<ThingDef> BenchDefs(CompBillAutomation comp)
		{
			HashSet<ThingDef> hashSet = new HashSet<ThingDef>();
			IList<Thing> benchesForReading = comp.BenchesForReading;
			if (benchesForReading == null)
			{
				return hashSet;
			}
			for (int i = 0; i < benchesForReading.Count; i++)
			{
				Thing val = benchesForReading[i];
				if (val != null && val.def != null)
				{
					hashSet.Add(val.def);
				}
			}
			return hashSet;
		}

		private static bool Accumulate(Ctx ctx, RecipeDef recipe, int batches, int depthLeft, out string fail)
		{
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_0145: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0188: Unknown result type (might be due to invalid IL or missing references)
			//IL_018d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0197: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0208: Unknown result type (might be due to invalid IL or missing references)
			//IL_020e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0213: Unknown result type (might be due to invalid IL or missing references)
			fail = null;
			if (recipe == null || batches <= 0)
			{
				return true;
			}
			if (ctx.path.Count >= 24)
			{
				fail = "展开链路超过 " + 24 + " 层";
				return false;
			}
			ctx.path.Add(recipe);
			try
			{
				for (int i = 0; i < recipe.ingredients.Count; i++)
				{
					IngredientCount val = recipe.ingredients[i];
					if (val == null || !val.IsFixedIngredient || val.FixedIngredient == null)
					{
						fail = TaggedString.op_Implicit(((Def)recipe).LabelCap + " 用了材料类别/计价材料（不支持展开）");
						return false;
					}
					ThingDef fixedIngredient = val.FixedIngredient;
					int num = CountRequired(val, recipe, ctx);
					if (num <= 0)
					{
						continue;
					}
					long num2 = (long)num * (long)batches;
					int num3 = (int)((num2 > 536870911) ? 536870911 : num2);
					int num4 = Math.Min(num3, AvailOf(ctx, fixedIngredient));
					if (num4 > 0)
					{
						AddNeed(ctx, fixedIngredient, num4);
						ctx.avail[fixedIngredient] = AvailOf(ctx, fixedIngredient) - num4;
					}
					int num5 = num3 - num4;
					if (num5 > 0)
					{
						if (fixedIngredient.IsStuff)
						{
							fail = TaggedString.op_Implicit("缺材料类别原料 " + ((Def)fixedIngredient).LabelCap + "（不展开）");
							return false;
						}
						if (depthLeft <= 0)
						{
							fail = TaggedString.op_Implicit("展开深度用完（还缺 " + ((Def)fixedIngredient).LabelCap + " ×") + num5 + "）";
							return false;
						}
						RecipeDef val2 = FindProducer(ctx, fixedIngredient);
						if (val2 == null)
						{
							fail = TaggedString.op_Implicit("找不到能做 " + ((Def)fixedIngredient).LabelCap + " 的已解锁配方（缺 ×") + num5 + "）";
							return false;
						}
						if (ctx.path.Contains(val2))
						{
							fail = TaggedString.op_Implicit("配方成环：" + ((Def)val2).LabelCap);
							return false;
						}
						int num6 = YieldOf(val2, fixedIngredient);
						if (num6 <= 0)
						{
							fail = TaggedString.op_Implicit(((Def)val2).LabelCap + " 的产物里没有 " + ((Def)fixedIngredient).LabelCap);
							return false;
						}
						int num7 = (num5 + num6 - 1) / num6;
						ctx.extraWork += val2.WorkAmountTotal((Thing)null) * (float)num7 * ctx.factor;
						ctx.trail.Add(((Def)fixedIngredient).defName + "×" + num5 + " ← " + ((Def)val2).defName + "×" + num7);
						if (!Accumulate(ctx, val2, num7, depthLeft - 1, out fail))
						{
							return false;
						}
					}
				}
				return true;
			}
			finally
			{
				ctx.path.RemoveAt(ctx.path.Count - 1);
			}
		}

		private static int AvailOf(Ctx ctx, ThingDef def)
		{
			if (!ctx.avail.TryGetValue(def, out var value))
			{
				return 0;
			}
			return value;
		}

		private static void AddNeed(Ctx ctx, ThingDef def, int n)
		{
			if (n > 0)
			{
				if (ctx.need.TryGetValue(def, out var value))
				{
					ctx.need[def] = value + n;
					return;
				}
				ctx.need[def] = n;
				ctx.order.Add(def);
			}
		}

		private static int CountRequired(IngredientCount ing, RecipeDef recipe, Ctx ctx)
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Expected O, but got Unknown
			Bill value;
			if (recipe == ctx.finalRecipe)
			{
				value = ctx.finalBill;
			}
			else if (!ctx.bills.TryGetValue(recipe, out value))
			{
				Bill_Production val = new Bill_Production(recipe, (Precept_ThingStyle)null);
				val.repeatMode = BillRepeatModeDefOf.Forever;
				ctx.bills[recipe] = (Bill)(object)val;
				value = (Bill)(object)val;
			}
			try
			{
				int num = ing.CountRequiredOfFor(ing.FixedIngredient, recipe, value);
				return (num >= 0) ? num : 0;
			}
			catch (Exception)
			{
				return 0;
			}
		}

		private static int YieldOf(RecipeDef producer, ThingDef def)
		{
			if (producer == null || producer.products == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < producer.products.Count; i++)
			{
				ThingDefCountClass val = producer.products[i];
				if (val != null && val.thingDef == def)
				{
					num += val.count;
				}
			}
			return num;
		}

		private static RecipeDef FindProducer(Ctx ctx, ThingDef def)
		{
			if (ctx.producers.TryGetValue(def, out var value))
			{
				return value;
			}
			RecipeDef val = null;
			List<RecipeDef> allDefsListForReading = DefDatabase<RecipeDef>.AllDefsListForReading;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				RecipeDef val2 = allDefsListForReading[i];
				if (val2 != null && val2 != ctx.finalRecipe && !val2.UsesUnfinishedThing && !val2.IsSurgery && val2.AvailableNow && val2.ingredients != null && val2.ingredients.Count != 0 && YieldOf(val2, def) > 0 && FixedIngredientsOnly(val2) && CraftableHere(ctx, val2))
				{
					float num = val2.WorkAmountTotal((Thing)null);
					float num2 = ((val == null) ? 0f : val.WorkAmountTotal((Thing)null));
					if (val == null || num < num2 || (num == num2 && string.Compare(((Def)val2).defName, ((Def)val).defName, StringComparison.Ordinal) < 0))
					{
						val = val2;
					}
				}
			}
			ctx.producers[def] = val;
			return val;
		}

		private static bool FixedIngredientsOnly(RecipeDef r)
		{
			for (int i = 0; i < r.ingredients.Count; i++)
			{
				IngredientCount val = r.ingredients[i];
				if (val == null || !val.IsFixedIngredient || val.FixedIngredient == null)
				{
					return false;
				}
			}
			return true;
		}

		private static bool CraftableHere(Ctx ctx, RecipeDef r)
		{
			if (ctx.benchDefs == null || ctx.benchDefs.Count == 0)
			{
				return false;
			}
			foreach (ThingDef benchDef in ctx.benchDefs)
			{
				List<RecipeDef> list = CraftUnlocks.For(benchDef);
				if (list == null)
				{
					continue;
				}
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i] == r)
					{
						return true;
					}
				}
			}
			return false;
		}

		private static bool Collect(Ctx ctx, out Thing[] things, out int[] counts)
		{
			List<Thing> list = new List<Thing>();
			List<int> list2 = new List<int>();
			for (int i = 0; i < ctx.order.Count; i++)
			{
				ThingDef key = ctx.order[i];
				int num = ctx.need[key];
				if (!ctx.stacks.TryGetValue(key, out var value))
				{
					return Fail(out things, out counts);
				}
				for (int j = 0; j < value.Count; j++)
				{
					if (num <= 0)
					{
						break;
					}
					Thing val = value[j];
					if (val == null || val.Destroyed)
					{
						continue;
					}
					object obj = CraftIngredientClaims.OwnerOf(val);
					if (obj == null || obj == ctx.line)
					{
						int num2 = Math.Min(num, val.stackCount);
						if (num2 > 0 && CraftIngredientClaims.TryClaim(val, ctx.line))
						{
							list.Add(val);
							list2.Add(num2);
							num -= num2;
						}
					}
				}
				if (num > 0)
				{
					return Fail(out things, out counts);
				}
			}
			things = list.ToArray();
			counts = list2.ToArray();
			return things.Length != 0;
		}

		private static bool Fail(out Thing[] things, out int[] counts)
		{
			things = null;
			counts = null;
			return false;
		}

		private static string Describe(Ctx ctx)
		{
			if (ctx.trail.Count == 0)
			{
				return null;
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < ctx.trail.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append("; ");
				}
				stringBuilder.Append(ctx.trail[i]);
			}
			return stringBuilder.ToString();
		}
	}
	internal static class CraftJobCode
	{
		public const int Ok = 0;

		public const int IncompletePlan = 1;

		public const int NoCpuFound = 2;

		public const int NoSuitableCpu = 3;

		public const int CpuOffline = 4;

		public const int CpuBusy = 5;

		public const int CpuTooSmall = 6;

		public const int MissingIngredient = 7;
	}
	internal sealed class CraftJobMissingRow
	{
		public ThingDef Def;

		public int Need;

		public int InCore;

		public int InFlight;

		public int Short => Mathf.Max(0, Need - InCore - InFlight);
	}
	internal static class CraftJobStatus
	{
		private static CraftJob cachedJob;

		private static int cachedTick = -99999;

		private static List<CraftJobMissingRow> cachedRows = new List<CraftJobMissingRow>();

		public static int CodeOf(CompBillAutomation comp, CraftJob job, out string text)
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_0191: Unknown result type (might be due to invalid IL or missing references)
			//IL_0196: Unknown result type (might be due to invalid IL or missing references)
			//IL_0155: Unknown result type (might be due to invalid IL or missing references)
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0168: Unknown result type (might be due to invalid IL or missing references)
			//IL_016d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0172: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0261: Unknown result type (might be due to invalid IL or missing references)
			//IL_0266: Unknown result type (might be due to invalid IL or missing references)
			//IL_022d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0232: Unknown result type (might be due to invalid IL or missing references)
			//IL_0240: Unknown result type (might be due to invalid IL or missing references)
			//IL_0245: Unknown result type (might be due to invalid IL or missing references)
			//IL_024a: Unknown result type (might be due to invalid IL or missing references)
			text = "";
			if (job == null)
			{
				return 0;
			}
			if (job.state == 1)
			{
				text = ((object)Translator.Translate("DS_JOB_St_Finished")/*cast due to constrained. prefix*/).ToString();
				return 0;
			}
			if (job.state == 2)
			{
				text = ((object)Translator.Translate("DS_JOB_St_Cancelled")/*cast due to constrained. prefix*/).ToString();
				return 0;
			}
			if (comp == null)
			{
				text = ((object)Translator.Translate("DS_JOB_St_NoProxy")/*cast due to constrained. prefix*/).ToString();
				return 2;
			}
			if (!comp.Enabled)
			{
				text = ((object)Translator.Translate("DS_JOB_St_Disabled")/*cast due to constrained. prefix*/).ToString();
				return 4;
			}
			if (!comp.Powered)
			{
				text = ((object)Translator.Translate("DS_JOB_St_NoPower")/*cast due to constrained. prefix*/).ToString();
				return 4;
			}
			for (int i = 0; i < job.steps.Count; i++)
			{
				CraftJobStep craftJobStep = job.steps[i];
				if (craftJobStep != null && craftJobStep.recipe != null && CraftTree.FindPlan(comp, craftJobStep.recipe) == null)
				{
					text = ((object)TranslatorFormattedStringExtensions.Translate("DS_JOB_St_Incomplete", NamedArgument.op_Implicit(((Def)craftJobStep.recipe).LabelCap))/*cast due to constrained. prefix*/).ToString();
					return 1;
				}
			}
			CraftPlan craftPlan = CraftTree.FindPlan(comp, job.rootRecipe);
			if (craftPlan == null)
			{
				text = ((object)TranslatorFormattedStringExtensions.Translate("DS_JOB_St_Incomplete", NamedArgument.op_Implicit((job.rootRecipe != null) ? ((object)((Def)job.rootRecipe).LabelCap/*cast due to constrained. prefix*/).ToString() : "?"))/*cast due to constrained. prefix*/).ToString();
				return 1;
			}
			if (CompBillAutomation.StepSatisfied(craftPlan))
			{
				text = ((object)Translator.Translate("DS_JOB_St_Done")/*cast due to constrained. prefix*/).ToString();
				return 0;
			}
			if (HasWork(comp, job))
			{
				text = ((object)Translator.Translate("DS_JOB_St_Crafting")/*cast due to constrained. prefix*/).ToString();
				return 0;
			}
			List<CraftJobMissingRow> list = Missing(comp, job);
			if (list.Count > 0)
			{
				text = ((object)TranslatorFormattedStringExtensions.Translate("DS_JOB_St_Missing", NamedArgument.op_Implicit(ShortSummary(list)))/*cast due to constrained. prefix*/).ToString();
				return 7;
			}
			if (comp.UsableBenchCountFor(job.rootRecipe) == 0)
			{
				text = ((object)TranslatorFormattedStringExtensions.Translate("DS_JOB_St_NoBench", NamedArgument.op_Implicit((job.rootRecipe != null) ? ((object)((Def)job.rootRecipe).LabelCap/*cast due to constrained. prefix*/).ToString() : "?"))/*cast due to constrained. prefix*/).ToString();
				return 3;
			}
			text = ((object)Translator.Translate("DS_JOB_St_Busy")/*cast due to constrained. prefix*/).ToString();
			return 5;
		}

		public static bool HasWork(CompBillAutomation comp, CraftJob job)
		{
			if (comp == null || job == null)
			{
				return false;
			}
			for (int i = 0; i < job.steps.Count; i++)
			{
				CraftJobStep craftJobStep = job.steps[i];
				if (craftJobStep == null || craftJobStep.recipe == null)
				{
					continue;
				}
				CraftPlan craftPlan = CraftTree.FindPlan(comp, craftJobStep.recipe);
				if (craftPlan == null || craftPlan.lines == null)
				{
					continue;
				}
				for (int j = 0; j < craftPlan.lines.Count; j++)
				{
					if (craftPlan.lines[j] != null && craftPlan.lines[j].HasWork)
					{
						return true;
					}
				}
			}
			return false;
		}

		public static int InFlight(CompBillAutomation comp, ThingDef def)
		{
			if (comp == null || def == null)
			{
				return 0;
			}
			IList<CraftPlan> plansForReading = comp.PlansForReading;
			if (plansForReading == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < plansForReading.Count; i++)
			{
				CraftPlan craftPlan = plansForReading[i];
				if (craftPlan == null || craftPlan.recipe == null || craftPlan.recipe.products == null || craftPlan.lines == null)
				{
					continue;
				}
				int num2 = 0;
				for (int j = 0; j < craftPlan.recipe.products.Count; j++)
				{
					ThingDefCountClass val = craftPlan.recipe.products[j];
					if (val != null && val.thingDef == def)
					{
						num2 += Mathf.Max(1, val.count);
					}
				}
				if (num2 <= 0)
				{
					continue;
				}
				for (int k = 0; k < craftPlan.lines.Count; k++)
				{
					CraftLine craftLine = craftPlan.lines[k];
					if (craftLine != null && craftLine.HasWork)
					{
						num += num2;
					}
				}
			}
			return num;
		}

		public static List<CraftJobMissingRow> Missing(CompBillAutomation comp, CraftJob job)
		{
			int num = ((Find.TickManager != null) ? Find.TickManager.TicksGame : 0);
			if (job == cachedJob && num - cachedTick < 30 && num >= cachedTick)
			{
				return cachedRows;
			}
			cachedJob = job;
			cachedTick = num;
			cachedRows = ComputeMissing(comp, job);
			return cachedRows;
		}

		private static List<CraftJobMissingRow> ComputeMissing(CompBillAutomation comp, CraftJob job)
		{
			List<CraftJobMissingRow> list = new List<CraftJobMissingRow>();
			if (comp == null || job == null || job.rootRecipe == null || job.rootProduct == null)
			{
				return list;
			}
			Map val = ((((ThingComp)comp).parent != null) ? ((Thing)((ThingComp)comp).parent).Map : null);
			if (val == null)
			{
				return list;
			}
			CraftTreeNode craftTreeNode;
			try
			{
				craftTreeNode = CraftTree.Build(comp, job.rootRecipe, job.rootProduct, job.wanted);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 合成状态：算缺料时建树失败（本次不报缺料）：" + ex, 553311);
				return list;
			}
			if (craftTreeNode == null)
			{
				return list;
			}
			Dictionary<ThingDef, int> coreStock = CraftTree.CoreStock(val);
			List<CraftTreeLeaf> list2 = CraftTree.CollectRawMaterials(craftTreeNode, coreStock);
			for (int i = 0; i < list2.Count; i++)
			{
				CraftTreeLeaf craftTreeLeaf = list2[i];
				if (craftTreeLeaf != null && !craftTreeLeaf.IsStuffSlot && craftTreeLeaf.Def != null)
				{
					CraftJobMissingRow craftJobMissingRow = new CraftJobMissingRow();
					craftJobMissingRow.Def = craftTreeLeaf.Def;
					craftJobMissingRow.Need = Mathf.Max(0, craftTreeLeaf.Count);
					craftJobMissingRow.InCore = Mathf.Max(0, craftTreeLeaf.InCore);
					craftJobMissingRow.InFlight = InFlight(comp, craftTreeLeaf.Def);
					if (craftJobMissingRow.Short > 0)
					{
						list.Add(craftJobMissingRow);
					}
				}
			}
			list.Sort((CraftJobMissingRow a, CraftJobMissingRow b) => b.Short - a.Short);
			return list;
		}

		public static string ShortSummary(List<CraftJobMissingRow> rows)
		{
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			if (rows == null || rows.Count == 0)
			{
				return "";
			}
			StringBuilder stringBuilder = new StringBuilder();
			int num = Mathf.Min(2, rows.Count);
			for (int i = 0; i < num; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append("、");
				}
				stringBuilder.Append(TaggedString.op_Implicit(((Def)rows[i].Def).LabelCap)).Append(" ×").Append(rows[i].Short);
			}
			if (rows.Count > num)
			{
				stringBuilder.Append(" …");
			}
			return stringBuilder.ToString();
		}

		public static string StepText(CompBillAutomation comp, CraftJob job, CraftJobStep step)
		{
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_011f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0124: Unknown result type (might be due to invalid IL or missing references)
			//IL_0138: Unknown result type (might be due to invalid IL or missing references)
			//IL_013d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00de: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
			if (step == null)
			{
				return "";
			}
			CraftPlan craftPlan = ((comp != null && step.recipe != null) ? CraftTree.FindPlan(comp, step.recipe) : null);
			if (craftPlan == null)
			{
				return ((object)TranslatorFormattedStringExtensions.Translate("DS_JOB_St_Incomplete", NamedArgument.op_Implicit((step.recipe != null) ? ((object)((Def)step.recipe).LabelCap/*cast due to constrained. prefix*/).ToString() : "?"))/*cast due to constrained. prefix*/).ToString();
			}
			if (CompBillAutomation.StepSatisfied(craftPlan))
			{
				return ((object)Translator.Translate("DS_JOB_St_Done")/*cast due to constrained. prefix*/).ToString();
			}
			if (craftPlan.lines != null)
			{
				for (int i = 0; i < craftPlan.lines.Count; i++)
				{
					CraftLine craftLine = craftPlan.lines[i];
					if (craftLine != null)
					{
						if (craftLine.HasWork)
						{
							return ((object)Translator.Translate("DS_JOB_St_Crafting")/*cast due to constrained. prefix*/).ToString();
						}
						if (!GenText.NullOrEmpty(craftLine.BlockKey))
						{
							return ((object)Translator.Translate(craftLine.BlockKey)/*cast due to constrained. prefix*/).ToString();
						}
					}
				}
			}
			if (craftPlan.lines == null || craftPlan.lines.Count <= 0)
			{
				return ((object)Translator.Translate("DS_JOB_St_Queue")/*cast due to constrained. prefix*/).ToString();
			}
			return ((object)Translator.Translate("DS_CA_Waiting")/*cast due to constrained. prefix*/).ToString();
		}

		public static void Progress(CompBillAutomation comp, CraftJob job, Dictionary<ThingDef, int> stock, out float pct, out string tag)
		{
			pct = 0f;
			tag = "";
			if (comp == null || job == null)
			{
				return;
			}
			CraftPlan craftPlan = CraftTree.FindPlan(comp, job.rootRecipe);
			if (craftPlan != null && craftPlan.mode == 1)
			{
				int num = Mathf.Max(0, craftPlan.completed);
				int num2 = num + Mathf.Max(0, craftPlan.remaining);
				pct = ((num2 > 0) ? Mathf.Clamp01((float)num / (float)num2) : 1f);
				tag = num + "/" + num2;
				return;
			}
			int value = 0;
			if (job.rootProduct != null)
			{
				stock?.TryGetValue(job.rootProduct, out value);
			}
			int num3 = Mathf.Max(1, job.wanted);
			int num4 = Mathf.Max(value, craftPlan?.countedCount ?? 0);
			pct = Mathf.Clamp01((float)num4 / (float)num3);
			tag = num4 + "/" + num3;
		}

		public static List<CompBillAutomation> AllProxies(Map map)
		{
			List<CompBillAutomation> list = new List<CompBillAutomation>();
			if (map == null)
			{
				return list;
			}
			List<Building> allBuildingsColonist = map.listerBuildings.allBuildingsColonist;
			for (int i = 0; i < allBuildingsColonist.Count; i++)
			{
				CompBillAutomation comp = ((ThingWithComps)allBuildingsColonist[i]).GetComp<CompBillAutomation>();
				if (comp != null)
				{
					list.Add(comp);
				}
			}
			return list;
		}

		public static int PlanBytesOf(CraftJob job)
		{
			if (job == null || job.steps == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < job.steps.Count; i++)
			{
				if (job.steps[i] != null)
				{
					num += Mathf.Max(0, job.steps[i].needCount);
				}
			}
			return num;
		}

		public static string BuildReport(CompBillAutomation comp)
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (comp == null)
			{
				return "[合成自检] comp = null";
			}
			int num = 0;
			int value = 0;
			IList<CraftPlan> plansForReading = comp.PlansForReading;
			if (plansForReading != null)
			{
				value = plansForReading.Count;
				for (int i = 0; i < plansForReading.Count; i++)
				{
					if (plansForReading[i] != null && plansForReading[i].lines != null)
					{
						num += plansForReading[i].lines.Count;
					}
				}
			}
			stringBuilder.Append("[合成自检] 代理=").Append((((ThingComp)comp).parent != null) ? ((Entity)((ThingComp)comp).parent).LabelShort : "?");
			stringBuilder.Append(" 电力=").Append(comp.Powered ? "有" : "无").Append('/')
				.Append(comp.Enabled ? "启用" : "停用");
			stringBuilder.Append(" 耗电=").Append(comp.CurrentWatts.ToString("#####0")).Append("W");
			stringBuilder.Append(" 产线=").Append(num).Append('/')
				.Append(value)
				.Append(" 单");
			stringBuilder.Append(" 合成=").Append((comp.JobsForReading != null) ? comp.JobsForReading.Count : 0);
			stringBuilder.Append(" 存储=").Append(comp.StorageTierLabel()).Append('(')
				.Append(comp.StorageBytes)
				.Append(" 件)");
			stringBuilder.Append(" 并行处理单元=").Append(comp.ParallelUnits).Append("(每步最多 ")
				.Append(comp.ParallelPerStep)
				.Append(" 张台)");
			stringBuilder.AppendLine();
			IList<CraftJob> jobsForReading = comp.JobsForReading;
			if (jobsForReading == null || jobsForReading.Count == 0)
			{
				stringBuilder.AppendLine("  （这台代理还没有合成请求）");
				return stringBuilder.ToString();
			}
			for (int j = 0; j < jobsForReading.Count && j < 12; j++)
			{
				CraftJob craftJob = jobsForReading[j];
				if (craftJob == null)
				{
					continue;
				}
				string text;
				int code = CodeOf(comp, craftJob, out text);
				Progress(comp, craftJob, CraftTree.CoreStock((((ThingComp)comp).parent != null) ? ((Thing)((ThingComp)comp).parent).Map : null), out var _, out var tag);
				stringBuilder.Append("  合成#").Append(j + 1).Append(' ')
					.Append((craftJob.rootRecipe != null) ? ((Def)craftJob.rootRecipe).defName : "?")
					.Append(" 状态=")
					.Append(StatusNameOf(code))
					.Append("（")
					.Append(text)
					.Append("）")
					.Append(" 进度=")
					.Append(tag)
					.Append(" 步骤=")
					.Append((craftJob.steps != null) ? craftJob.steps.Count : 0)
					.Append(" 规模=")
					.Append(PlanBytesOf(craftJob))
					.Append('/')
					.Append(comp.StorageBytes);
				stringBuilder.AppendLine();
				if (craftJob.steps != null)
				{
					for (int k = 0; k < craftJob.steps.Count && k < 16; k++)
					{
						CraftJobStep craftJobStep = craftJob.steps[k];
						if (craftJobStep != null && craftJobStep.recipe != null)
						{
							CraftPlan craftPlan = CraftTree.FindPlan(comp, craftJobStep.recipe);
							stringBuilder.Append("      ").Append(craftJobStep.IsRoot ? "[根] " : "     ").Append(((Def)craftJobStep.recipe).defName)
								.Append(" ×")
								.Append(craftJobStep.needCount)
								.Append("（做 ")
								.Append(craftJobStep.crafts)
								.Append(" 次，深度 ")
								.Append(craftJobStep.depth)
								.Append("）")
								.Append(" 订单=")
								.Append((craftPlan == null) ? "无" : (craftPlan.mode + "/" + craftPlan.lines.Count + " 线"))
								.Append(" 步骤状态=")
								.Append(StepText(comp, craftJob, craftJobStep));
							stringBuilder.AppendLine();
						}
					}
				}
				List<CraftJobMissingRow> list = Missing(comp, craftJob);
				if (list.Count == 0)
				{
					stringBuilder.AppendLine("      缺料：无");
					continue;
				}
				stringBuilder.Append("      缺料：");
				for (int l = 0; l < list.Count && l < 10; l++)
				{
					if (l > 0)
					{
						stringBuilder.Append('、');
					}
					stringBuilder.Append(((Def)list[l].Def).defName).Append(" 需要").Append(list[l].Need)
						.Append("/核心")
						.Append(list[l].InCore)
						.Append("/在途")
						.Append(list[l].InFlight)
						.Append("/缺")
						.Append(list[l].Short);
				}
				stringBuilder.AppendLine();
			}
			return stringBuilder.ToString();
		}

		public static string StatusNameOf(int code)
		{
			return code switch
			{
				1 => "INCOMPLETE_PLAN", 
				2 => "NO_CPU_FOUND", 
				3 => "NO_SUITABLE_CPU_FOUND", 
				4 => "CPU_OFFLINE", 
				5 => "CPU_BUSY", 
				6 => "CPU_TOO_SMALL", 
				7 => "MISSING_INGREDIENT", 
				_ => "OK", 
			};
		}

		public static Color ColorOf(int code)
		{
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			switch (code)
			{
			case 1:
			case 3:
			case 7:
				return AE2Draw.Bad;
			case 4:
				return AE2Draw.TextDimCol;
			case 5:
				return AE2Draw.Warn;
			default:
				return AE2Draw.Accent;
			}
		}
	}
	internal static class CraftJobState
	{
		public const int Crafting = 0;

		public const int Finished = 1;

		public const int Cancelled = 2;
	}
	internal sealed class CraftJob : IExposable
	{
		public RecipeDef rootRecipe;

		public ThingDef rootProduct;

		public int wanted = 1;

		public int state;

		public int createdTick;

		public int finishedTick = -1;

		public List<CraftJobStep> steps = new List<CraftJobStep>();

		public bool Active => state == 0;

		public CraftJobStep Root
		{
			get
			{
				if (steps.Count <= 0)
				{
					return null;
				}
				return steps[0];
			}
		}

		public int MaxDepth
		{
			get
			{
				int num = 0;
				for (int i = 0; i < steps.Count; i++)
				{
					if (steps[i] != null && steps[i].depth > num)
					{
						num = steps[i].depth;
					}
				}
				return num;
			}
		}

		public int IndexOfRecipe(RecipeDef r)
		{
			if (r == null)
			{
				return -1;
			}
			for (int i = 0; i < steps.Count; i++)
			{
				if (steps[i] != null && steps[i].recipe == r)
				{
					return i;
				}
			}
			return -1;
		}

		public void ExposeData()
		{
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Invalid comparison between Unknown and I4
			Scribe_Defs.Look<RecipeDef>(ref rootRecipe, "rootRecipe");
			Scribe_Defs.Look<ThingDef>(ref rootProduct, "rootProduct");
			Scribe_Values.Look<int>(ref wanted, "wanted", 1, false);
			Scribe_Values.Look<int>(ref state, "state", 0, false);
			Scribe_Values.Look<int>(ref createdTick, "createdTick", 0, false);
			Scribe_Values.Look<int>(ref finishedTick, "finishedTick", -1, false);
			Scribe_Collections.Look<CraftJobStep>(ref steps, "steps", (LookMode)2, Array.Empty<object>());
			if ((int)Scribe.mode != 4)
			{
				return;
			}
			if (steps == null)
			{
				steps = new List<CraftJobStep>();
			}
			int[] array = new int[steps.Count];
			List<CraftJobStep> list = new List<CraftJobStep>(steps.Count);
			for (int i = 0; i < steps.Count; i++)
			{
				CraftJobStep craftJobStep = steps[i];
				if (craftJobStep == null || craftJobStep.recipe == null)
				{
					array[i] = -1;
					continue;
				}
				array[i] = list.Count;
				list.Add(craftJobStep);
			}
			steps = list;
			for (int j = 0; j < steps.Count; j++)
			{
				CraftJobStep craftJobStep2 = steps[j];
				if (craftJobStep2.children == null)
				{
					craftJobStep2.children = new List<int>();
				}
				else
				{
					craftJobStep2.children.Clear();
				}
				int parent = craftJobStep2.parent;
				int num = ((parent >= 0 && parent < array.Length) ? array[parent] : (-1));
				craftJobStep2.parent = ((num >= 0 && num != j) ? num : (-1));
			}
			for (int k = 0; k < steps.Count; k++)
			{
				int parent2 = steps[k].parent;
				if (parent2 >= 0 && parent2 < steps.Count)
				{
					steps[parent2].children.Add(k);
				}
			}
		}
	}
	internal sealed class CraftJobStep : IExposable
	{
		public RecipeDef recipe;

		public ThingDef product;

		public int needCount = 1;

		public int crafts = 1;

		public int depth;

		public int parent = -1;

		public List<int> children = new List<int>();

		public bool IsRoot => parent < 0;

		public void ExposeData()
		{
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Invalid comparison between Unknown and I4
			Scribe_Defs.Look<RecipeDef>(ref recipe, "recipe");
			Scribe_Defs.Look<ThingDef>(ref product, "product");
			Scribe_Values.Look<int>(ref needCount, "needCount", 1, false);
			Scribe_Values.Look<int>(ref crafts, "crafts", 1, false);
			Scribe_Values.Look<int>(ref depth, "depth", 0, false);
			Scribe_Values.Look<int>(ref parent, "parent", -1, false);
			if ((int)Scribe.mode == 4 && children == null)
			{
				children = new List<int>();
			}
		}
	}
	internal class CraftTreeLeaf
	{
		public ThingDef Def;

		public string Label;

		public int Count;

		public int InCore;

		public bool IsStuffSlot;
	}
	internal class CraftTreeNode
	{
		public ThingDef Product;

		public RecipeDef Recipe;

		public List<RecipeDef> Alternatives = new List<RecipeDef>();

		public int Required;

		public int RequiredTotal;

		public int YieldPerCraft = 1;

		public int Crafts;

		public int InCore;

		public bool CoveredByCore;

		public bool NoRecipe;

		public bool CycleCut;

		public bool HasChanceProduct;

		public int Depth;

		public List<CraftTreeNode> Children = new List<CraftTreeNode>();

		public List<CraftTreeLeaf> Leaves = new List<CraftTreeLeaf>();
	}
	internal class CraftTreePlanRow
	{
		public ThingDef Product;

		public RecipeDef Recipe;

		public int Total;
	}
	internal static class CraftTree
	{
		private const int MaxDepth = 12;

		private static readonly Dictionary<ThingDef, RecipeDef> chosen = new Dictionary<ThingDef, RecipeDef>();

		public static void ForgetChoices()
		{
			chosen.Clear();
		}

		public static void RememberChoice(ThingDef product, RecipeDef recipe)
		{
			if (product != null && recipe != null)
			{
				chosen[product] = recipe;
			}
		}

		public static Dictionary<ThingDef, int> CoreStock(Map map)
		{
			Dictionary<ThingDef, int> dictionary = new Dictionary<ThingDef, int>();
			if (map == null)
			{
				return dictionary;
			}
			List<Building_StorageCore> list = CoreFinder.AllUsableCores(map);
			if (list == null)
			{
				return dictionary;
			}
			for (int i = 0; i < list.Count; i++)
			{
				Building_StorageCore building_StorageCore = list[i];
				if (building_StorageCore == null)
				{
					continue;
				}
				ThingOwner directlyHeldThings = building_StorageCore.GetDirectlyHeldThings();
				if (directlyHeldThings == null)
				{
					continue;
				}
				for (int j = 0; j < directlyHeldThings.Count; j++)
				{
					Thing val = directlyHeldThings[j];
					if (val != null && !val.Destroyed && val.def != null)
					{
						dictionary.TryGetValue(val.def, out var value);
						dictionary[val.def] = value + Math.Max(0, val.stackCount);
					}
				}
			}
			return dictionary;
		}

		private static bool Produces(RecipeDef r, ThingDef product)
		{
			if (r == null || product == null || r.products == null)
			{
				return false;
			}
			for (int i = 0; i < r.products.Count; i++)
			{
				ThingDefCountClass val = r.products[i];
				if (val != null && val.thingDef == product)
				{
					return true;
				}
			}
			return false;
		}

		public static List<RecipeDef> RecipesFor(CompBillAutomation comp, ThingDef product)
		{
			List<RecipeDef> list = RecipesForBase(comp, product);
			if (list == null)
			{
				list = new List<RecipeDef>();
			}
			if (product == null)
			{
				return list;
			}
			foreach (RecipeDef allDef in DefDatabase<RecipeDef>.AllDefs)
			{
				if (allDef == null || allDef.ProducedThingDef != product || allDef.recipeUsers == null)
				{
					continue;
				}
				bool flag = false;
				for (int i = 0; i < allDef.recipeUsers.Count; i++)
				{
					ThingDef val = allDef.recipeUsers[i];
					if (val != null && ((Def)val).defName != null && ((Def)val).defName.StartsWith("DigitalStorage_WorkerCraft"))
					{
						flag = true;
						break;
					}
				}
				if (flag && !list.Contains(allDef))
				{
					list.Add(allDef);
				}
			}
			return list;
		}

		private static List<RecipeDef> RecipesForBase(CompBillAutomation comp, ThingDef product)
		{
			List<RecipeDef> list = new List<RecipeDef>();
			if (comp == null || product == null)
			{
				return list;
			}
			IList<RecipeDef> unlockedRecipes = comp.UnlockedRecipes;
			if (unlockedRecipes == null)
			{
				return list;
			}
			for (int i = 0; i < unlockedRecipes.Count; i++)
			{
				RecipeDef val = unlockedRecipes[i];
				if (Produces(val, product))
				{
					list.Add(val);
				}
			}
			list.Sort(delegate(RecipeDef a, RecipeDef b)
			{
				int num2 = ((a.ingredients != null) ? a.ingredients.Count : 0);
				int num3 = ((b.ingredients != null) ? b.ingredients.Count : 0);
				return (num2 != num3) ? (num2 - num3) : string.CompareOrdinal(((Def)a).defName, ((Def)b).defName);
			});
			if (chosen.TryGetValue(product, out var value) && value != null)
			{
				int num = list.IndexOf(value);
				if (num > 0)
				{
					list.RemoveAt(num);
					list.Insert(0, value);
				}
			}
			return list;
		}

		public static bool HasCraftableIntermediate(CompBillAutomation comp, RecipeDef recipe)
		{
			if (comp == null || recipe == null || recipe.ingredients == null)
			{
				return false;
			}
			for (int i = 0; i < recipe.ingredients.Count; i++)
			{
				IngredientCount val = recipe.ingredients[i];
				if (val != null && val.IsFixedIngredient && val.FixedIngredient != null && RecipesFor(comp, val.FixedIngredient).Count > 0)
				{
					return true;
				}
			}
			return false;
		}

		public static CraftTreeNode Build(CompBillAutomation comp, RecipeDef rootRecipe, ThingDef rootProduct, int wanted)
		{
			CraftTreeNode craftTreeNode = new CraftTreeNode();
			craftTreeNode.Product = rootProduct;
			craftTreeNode.Recipe = rootRecipe;
			craftTreeNode.Depth = 0;
			if (comp == null || rootProduct == null || wanted <= 0)
			{
				return craftTreeNode;
			}
			Dictionary<ThingDef, int> avail = CoreStock((((ThingComp)comp).parent != null) ? ((Thing)((ThingComp)comp).parent).Map : null);
			craftTreeNode.Required = wanted;
			craftTreeNode.RequiredTotal = wanted;
			int num = 1;
			if (rootRecipe != null && rootRecipe.products != null)
			{
				int num2 = 0;
				for (int i = 0; i < rootRecipe.products.Count; i++)
				{
					ThingDefCountClass val = rootRecipe.products[i];
					if (val != null && val.thingDef == rootProduct)
					{
						num2 += Math.Max(1, val.count);
						if (val.IsChanceBased)
						{
							craftTreeNode.HasChanceProduct = true;
						}
					}
				}
				if (num2 > 0)
				{
					num = num2;
				}
			}
			craftTreeNode.YieldPerCraft = num;
			craftTreeNode.Crafts = (wanted + num - 1) / num;
			List<ThingDef> list = new List<ThingDef>();
			list.Add(rootProduct);
			if (rootRecipe != null && rootRecipe.ingredients != null)
			{
				for (int j = 0; j < rootRecipe.ingredients.Count; j++)
				{
					IngredientCount val2 = rootRecipe.ingredients[j];
					if (val2 == null)
					{
						continue;
					}
					int num3 = Mathf.CeilToInt(val2.CountFor(rootRecipe)) * craftTreeNode.Crafts;
					if (num3 > 0)
					{
						if (val2.IsFixedIngredient && val2.FixedIngredient != null)
						{
							craftTreeNode.Children.Add(BuildNode(comp, val2.FixedIngredient, num3, list, avail, 1));
						}
						else
						{
							craftTreeNode.Leaves.Add(MakeStuffLeaf(val2, rootRecipe, num3, avail));
						}
					}
				}
			}
			return craftTreeNode;
		}

		private static CraftTreeLeaf MakeStuffLeaf(IngredientCount ing, RecipeDef recipe, int need, Dictionary<ThingDef, int> avail)
		{
			return new CraftTreeLeaf
			{
				IsStuffSlot = true,
				Label = ing.SummaryFor(recipe),
				Count = need
			};
		}

		private static CraftTreeNode BuildNode(CompBillAutomation comp, ThingDef product, int wanted, List<ThingDef> path, Dictionary<ThingDef, int> avail, int depth)
		{
			CraftTreeNode craftTreeNode = new CraftTreeNode();
			craftTreeNode.Product = product;
			craftTreeNode.Depth = depth;
			if (product == null || wanted <= 0)
			{
				return craftTreeNode;
			}
			int value = 0;
			avail.TryGetValue(product, out value);
			int num = (craftTreeNode.InCore = Math.Min(value, wanted));
			avail[product] = value - num;
			int num2 = wanted - num;
			craftTreeNode.RequiredTotal = wanted;
			craftTreeNode.Required = num2;
			if (num2 <= 0)
			{
				craftTreeNode.CoveredByCore = true;
				return craftTreeNode;
			}
			for (int i = 0; i < path.Count; i++)
			{
				if (path[i] == product)
				{
					craftTreeNode.CycleCut = true;
					return craftTreeNode;
				}
			}
			if (depth >= 12)
			{
				craftTreeNode.NoRecipe = true;
				return craftTreeNode;
			}
			List<RecipeDef> list = RecipesFor(comp, product);
			if (list.Count == 0)
			{
				craftTreeNode.NoRecipe = true;
				return craftTreeNode;
			}
			RecipeDef val = (craftTreeNode.Recipe = list[0]);
			for (int j = 1; j < list.Count; j++)
			{
				craftTreeNode.Alternatives.Add(list[j]);
			}
			int num3 = 0;
			if (val.products != null)
			{
				for (int k = 0; k < val.products.Count; k++)
				{
					ThingDefCountClass val2 = val.products[k];
					if (val2 != null && val2.thingDef == product)
					{
						num3 += Math.Max(1, val2.count);
						if (val2.IsChanceBased)
						{
							craftTreeNode.HasChanceProduct = true;
						}
					}
				}
			}
			if (num3 <= 0)
			{
				num3 = 1;
			}
			craftTreeNode.YieldPerCraft = num3;
			craftTreeNode.Crafts = (num2 + num3 - 1) / num3;
			path.Add(product);
			if (val.ingredients != null)
			{
				for (int l = 0; l < val.ingredients.Count; l++)
				{
					IngredientCount val3 = val.ingredients[l];
					if (val3 == null)
					{
						continue;
					}
					int num4 = Mathf.CeilToInt(val3.CountFor(val)) * craftTreeNode.Crafts;
					if (num4 > 0)
					{
						if (val3.IsFixedIngredient && val3.FixedIngredient != null)
						{
							craftTreeNode.Children.Add(BuildNode(comp, val3.FixedIngredient, num4, path, avail, depth + 1));
						}
						else
						{
							craftTreeNode.Leaves.Add(MakeStuffLeaf(val3, val, num4, avail));
						}
					}
				}
			}
			path.RemoveAt(path.Count - 1);
			return craftTreeNode;
		}

		public static List<CraftTreePlanRow> FlattenIntermediate(CraftTreeNode root)
		{
			List<CraftTreePlanRow> list = new List<CraftTreePlanRow>();
			if (root == null)
			{
				return list;
			}
			Dictionary<ThingDef, CraftTreePlanRow> dictionary = new Dictionary<ThingDef, CraftTreePlanRow>();
			Collect(root, dictionary);
			foreach (KeyValuePair<ThingDef, CraftTreePlanRow> item in dictionary)
			{
				list.Add(item.Value);
			}
			list.Sort((CraftTreePlanRow a, CraftTreePlanRow b) => string.CompareOrdinal(((Def)a.Recipe).defName, ((Def)b.Recipe).defName));
			return list;
		}

		private static void Collect(CraftTreeNode node, Dictionary<ThingDef, CraftTreePlanRow> agg)
		{
			if (node == null)
			{
				return;
			}
			if (node.Recipe != null && node.RequiredTotal > 0 && !node.CoveredByCore && node.Depth > 0)
			{
				if (!agg.TryGetValue(node.Product, out var value))
				{
					value = new CraftTreePlanRow();
					value.Product = node.Product;
					value.Recipe = node.Recipe;
					value.Total = 0;
					agg[node.Product] = value;
				}
				value.Total += node.RequiredTotal;
			}
			for (int i = 0; i < node.Children.Count; i++)
			{
				Collect(node.Children[i], agg);
			}
		}

		public static List<CraftTreeLeaf> CollectRawMaterials(CraftTreeNode root, Dictionary<ThingDef, int> coreStock)
		{
			List<CraftTreeLeaf> list = new List<CraftTreeLeaf>();
			Dictionary<ThingDef, CraftTreeLeaf> dictionary = new Dictionary<ThingDef, CraftTreeLeaf>();
			Dictionary<string, CraftTreeLeaf> dictionary2 = new Dictionary<string, CraftTreeLeaf>();
			CollectRaw(root, dictionary, dictionary2);
			foreach (KeyValuePair<ThingDef, CraftTreeLeaf> item in dictionary)
			{
				if (coreStock != null && coreStock.TryGetValue(item.Key, out var value))
				{
					item.Value.InCore = value;
				}
				list.Add(item.Value);
			}
			foreach (KeyValuePair<string, CraftTreeLeaf> item2 in dictionary2)
			{
				list.Add(item2.Value);
			}
			list.Sort((CraftTreeLeaf a, CraftTreeLeaf b) => string.CompareOrdinal(a.Label, b.Label));
			return list;
		}

		private static void CollectRaw(CraftTreeNode node, Dictionary<ThingDef, CraftTreeLeaf> agg, Dictionary<string, CraftTreeLeaf> stuffAgg)
		{
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			if (node == null)
			{
				return;
			}
			if (node.Recipe == null && node.RequiredTotal > 0 && !node.CoveredByCore && node.Product != null)
			{
				if (!agg.TryGetValue(node.Product, out var value))
				{
					value = new CraftTreeLeaf();
					value.Def = node.Product;
					value.Label = TaggedString.op_Implicit(((Def)node.Product).LabelCap);
					value.Count = 0;
					agg[node.Product] = value;
				}
				value.Count += node.RequiredTotal;
			}
			for (int i = 0; i < node.Leaves.Count; i++)
			{
				CraftTreeLeaf craftTreeLeaf = node.Leaves[i];
				if (craftTreeLeaf != null)
				{
					if (!stuffAgg.TryGetValue(craftTreeLeaf.Label, out var value2))
					{
						value2 = new CraftTreeLeaf();
						value2.IsStuffSlot = true;
						value2.Label = craftTreeLeaf.Label;
						value2.Count = 0;
						stuffAgg[craftTreeLeaf.Label] = value2;
					}
					value2.Count += craftTreeLeaf.Count;
				}
			}
			for (int j = 0; j < node.Children.Count; j++)
			{
				CollectRaw(node.Children[j], agg, stuffAgg);
			}
		}

		public static void CountWarnings(CraftTreeNode root, out int noRecipe, out int cycle, out int chance, out int ambiguous)
		{
			noRecipe = 0;
			cycle = 0;
			chance = 0;
			ambiguous = 0;
			CountWarningsNode(root, ref noRecipe, ref cycle, ref chance, ref ambiguous);
		}

		private static void CountWarningsNode(CraftTreeNode node, ref int noRecipe, ref int cycle, ref int chance, ref int ambiguous)
		{
			if (node != null)
			{
				if (node.NoRecipe && node.Required > 0)
				{
					noRecipe++;
				}
				if (node.CycleCut)
				{
					cycle++;
				}
				if (node.HasChanceProduct)
				{
					chance++;
				}
				if (node.Alternatives.Count > 0)
				{
					ambiguous++;
				}
				for (int i = 0; i < node.Children.Count; i++)
				{
					CountWarningsNode(node.Children[i], ref noRecipe, ref cycle, ref chance, ref ambiguous);
				}
			}
		}

		public static int YieldOf(RecipeDef r, ThingDef product)
		{
			if (r == null || product == null || r.products == null)
			{
				return 1;
			}
			int num = 0;
			for (int i = 0; i < r.products.Count; i++)
			{
				ThingDefCountClass val = r.products[i];
				if (val != null && val.thingDef == product)
				{
					num += Math.Max(1, val.count);
				}
			}
			if (num <= 0)
			{
				return 1;
			}
			return num;
		}

		internal static CraftPlan FindPlan(CompBillAutomation comp, RecipeDef recipe)
		{
			if (comp == null || recipe == null)
			{
				return null;
			}
			IList<CraftPlan> plansForReading = comp.PlansForReading;
			if (plansForReading == null)
			{
				return null;
			}
			for (int i = 0; i < plansForReading.Count; i++)
			{
				if (plansForReading[i] != null && plansForReading[i].recipe == recipe)
				{
					return plansForReading[i];
				}
			}
			return null;
		}

		public static void ApplyToPlans(CompBillAutomation comp, RecipeDef recipe, ThingDef product, int wanted, out int added, out int skipped)
		{
			added = 0;
			skipped = 0;
			if (comp != null && recipe != null)
			{
				int wanted2 = Mathf.Max(1, wanted);
				int num = ((comp.PlansForReading != null) ? comp.PlansForReading.Count : 0);
				CraftJob craftJob = comp.SubmitJob(recipe, product, wanted2, withIntermediates: true);
				if (craftJob == null)
				{
					skipped++;
					return;
				}
				int num2 = ((comp.PlansForReading != null) ? comp.PlansForReading.Count : 0);
				added = Mathf.Max(0, num2 - num);
				skipped = Mathf.Max(0, craftJob.steps.Count - added);
			}
		}
	}
}
namespace DigitalStorage.Core
{
	internal static class CoreTier
	{
		public const int MaxLevel = 4;

		public const int BaseStacks = 500;

		private static readonly string[] ResearchByLevel = new string[4] { null, "DigitalStorage_CoreLevel2", "DigitalStorage_CoreLevel3", "DigitalStorage_CoreLevel4" };

		private static readonly int[] CapByLevel = new int[4] { 500, 1000, 1500, 3000 };

		private static int cachedLevel = 1;

		private static int cachedTick = int.MinValue;

		public static int Level
		{
			get
			{
				Ensure();
				return cachedLevel;
			}
		}

		public static int Cap
		{
			get
			{
				Ensure();
				return CapByLevel[cachedLevel - 1];
			}
		}

		private static void Ensure()
		{
			if (cachedLevel == 4)
			{
				return;
			}
			TickManager tickManager = Find.TickManager;
			if (tickManager == null)
			{
				return;
			}
			int ticksGame = tickManager.TicksGame;
			if (ticksGame == cachedTick)
			{
				return;
			}
			cachedTick = ticksGame;
			if (Find.ResearchManager == null)
			{
				return;
			}
			for (int num = ResearchByLevel.Length - 1; num >= 1; num--)
			{
				ResearchProjectDef namedSilentFail = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(ResearchByLevel[num]);
				if (namedSilentFail != null && namedSilentFail.IsFinished)
				{
					cachedLevel = num + 1;
					return;
				}
			}
			cachedLevel = 1;
		}
	}
	public static class HaulSourceContents
	{
		private static readonly List<Thing> tmpThings = new List<Thing>();

		private static readonly List<IHaulSource> tmpSources = new List<IHaulSource>();

		private static readonly List<IHaulSource> tmpRemoteSources = new List<IHaulSource>();

		private static readonly List<IHaulSource> tmpCoreSources = new List<IHaulSource>();

		public static List<IHaulSource> EnabledSources(Map map)
		{
			tmpSources.Clear();
			if (map == null)
			{
				return tmpSources;
			}
			HaulDestinationManager haulDestinationManager = map.haulDestinationManager;
			List<IHaulSource> list = ((haulDestinationManager != null) ? haulDestinationManager.AllHaulSourcesListForReading : null);
			if (list == null)
			{
				return tmpSources;
			}
			for (int i = 0; i < list.Count; i++)
			{
				IHaulSource val = list[i];
				if (val != null && val.HaulSourceEnabled)
				{
					tmpSources.Add(val);
				}
			}
			return tmpSources;
		}

		public static List<IHaulSource> RemoteCoreSources(Map exclude)
		{
			tmpRemoteSources.Clear();
			List<Map> maps = Find.Maps;
			if (maps == null)
			{
				return tmpRemoteSources;
			}
			for (int i = 0; i < maps.Count; i++)
			{
				Map val = maps[i];
				if (val == null || val == exclude)
				{
					continue;
				}
				HaulDestinationManager haulDestinationManager = val.haulDestinationManager;
				List<IHaulSource> list = ((haulDestinationManager != null) ? haulDestinationManager.AllHaulSourcesListForReading : null);
				if (list == null)
				{
					continue;
				}
				for (int j = 0; j < list.Count; j++)
				{
					if (list[j] is Building_StorageCore { HaulSourceEnabled: not false, IsUsableNow: not false } building_StorageCore)
					{
						tmpRemoteSources.Add((IHaulSource)(object)building_StorageCore);
					}
				}
			}
			return tmpRemoteSources;
		}

		public static bool AnyRemoteContent(Map exclude)
		{
			List<IHaulSource> list = RemoteCoreSources(exclude);
			for (int i = 0; i < list.Count; i++)
			{
				ThingOwner directlyHeldThings = ((IThingHolder)list[i]).GetDirectlyHeldThings();
				if (directlyHeldThings != null && directlyHeldThings.Count > 0)
				{
					return true;
				}
			}
			return false;
		}

		public static List<IHaulSource> CoreSources(Map map)
		{
			tmpCoreSources.Clear();
			if (map == null)
			{
				return tmpCoreSources;
			}
			HaulDestinationManager haulDestinationManager = map.haulDestinationManager;
			List<IHaulSource> list = ((haulDestinationManager != null) ? haulDestinationManager.AllHaulSourcesListForReading : null);
			if (list == null)
			{
				return tmpCoreSources;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] is Building_StorageCore { HaulSourceEnabled: not false, IsUsableNow: not false } building_StorageCore)
				{
					tmpCoreSources.Add((IHaulSource)(object)building_StorageCore);
				}
			}
			return tmpCoreSources;
		}

		public static bool AnyContent(Map map)
		{
			List<IHaulSource> list = EnabledSources(map);
			for (int i = 0; i < list.Count; i++)
			{
				ThingOwner directlyHeldThings = ((IThingHolder)list[i]).GetDirectlyHeldThings();
				if (directlyHeldThings != null && directlyHeldThings.Count > 0)
				{
					return true;
				}
			}
			return false;
		}

		public static void GatherAll(Map map, List<Thing> outThings)
		{
			outThings.Clear();
			List<IHaulSource> list = EnabledSources(map);
			for (int i = 0; i < list.Count; i++)
			{
				tmpThings.Clear();
				ThingOwnerUtility.GetAllThingsRecursively((IThingHolder)(object)list[i], tmpThings, true, (Predicate<IThingHolder>)null);
				for (int j = 0; j < tmpThings.Count; j++)
				{
					Thing val = tmpThings[j];
					if (val != null && !val.Destroyed)
					{
						outThings.Add(val);
					}
				}
			}
			tmpThings.Clear();
		}

		public static void CollectDirect(Map map, List<Thing> outThings)
		{
			outThings.Clear();
			List<IHaulSource> list = EnabledSources(map);
			for (int i = 0; i < list.Count; i++)
			{
				ThingOwner directlyHeldThings = ((IThingHolder)list[i]).GetDirectlyHeldThings();
				if (directlyHeldThings == null)
				{
					continue;
				}
				for (int j = 0; j < directlyHeldThings.Count; j++)
				{
					Thing val = directlyHeldThings[j];
					if (val != null && !val.Destroyed)
					{
						outThings.Add(val);
					}
				}
			}
		}

		public static Thing FindBest(Map map, Func<Thing, float> score, Predicate<Thing> validator)
		{
			if (map == null || validator == null)
			{
				return null;
			}
			return FindBestIn(EnabledSources(map), score, validator);
		}

		public static Thing FindBestIncludingRemote(Map map, Func<Thing, float> score, Predicate<Thing> validator)
		{
			if (map == null || validator == null)
			{
				return null;
			}
			Thing val = FindBestIn(EnabledSources(map), score, validator);
			if (val != null)
			{
				return val;
			}
			return FindBestIn(RemoteCoreSources(map), score, validator);
		}

		private static Thing FindBestIn(List<IHaulSource> sources, Func<Thing, float> score, Predicate<Thing> validator)
		{
			Thing val = null;
			float num = float.MinValue;
			for (int i = 0; i < sources.Count; i++)
			{
				tmpThings.Clear();
				ThingOwnerUtility.GetAllThingsRecursively((IThingHolder)(object)sources[i], tmpThings, true, (Predicate<IThingHolder>)null);
				for (int j = 0; j < tmpThings.Count; j++)
				{
					Thing val2 = tmpThings[j];
					if (val2 != null && !val2.Destroyed && validator(val2))
					{
						float num2 = score?.Invoke(val2) ?? 0f;
						if (val == null || num2 > num)
						{
							val = val2;
							num = num2;
						}
					}
				}
			}
			tmpThings.Clear();
			return val;
		}

		public static void GatherAllIncludingRemote(Map map, List<Thing> outThings)
		{
			GatherAll(map, outThings);
			AppendRemote(map, outThings);
		}

		public static void AppendRemote(Map exclude, List<Thing> outThings)
		{
			if (outThings == null)
			{
				return;
			}
			List<IHaulSource> list = RemoteCoreSources(exclude);
			for (int i = 0; i < list.Count; i++)
			{
				tmpThings.Clear();
				ThingOwnerUtility.GetAllThingsRecursively((IThingHolder)(object)list[i], tmpThings, true, (Predicate<IThingHolder>)null);
				for (int j = 0; j < tmpThings.Count; j++)
				{
					Thing val = tmpThings[j];
					if (val != null && !val.Destroyed)
					{
						outThings.Add(val);
					}
				}
			}
			tmpThings.Clear();
		}

		public static int CountOf(Map map, ThingDef def)
		{
			if (map == null || def == null)
			{
				return 0;
			}
			int num = 0;
			List<IHaulSource> list = EnabledSources(map);
			for (int i = 0; i < list.Count; i++)
			{
				tmpThings.Clear();
				ThingOwnerUtility.GetAllThingsRecursively((IThingHolder)(object)list[i], tmpThings, true, (Predicate<IThingHolder>)null);
				for (int j = 0; j < tmpThings.Count; j++)
				{
					Thing val = tmpThings[j];
					if (val != null && !val.Destroyed && val.def == def)
					{
						num += val.stackCount;
					}
				}
			}
			tmpThings.Clear();
			return num;
		}

		public static Thing ExtractToFeet(Thing t, int count, Pawn pawn)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			if (pawn == null)
			{
				return null;
			}
			int extracted;
			return ExtractTo(t, count, ((Thing)pawn).Position, ((Thing)pawn).Map, out extracted);
		}

		public static Thing ExtractTo(Thing t, int count, IntVec3 pos, Map map, out int extracted, bool forbid = false)
		{
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Unknown result type (might be due to invalid IL or missing references)
			extracted = 0;
			if (t == null || t.Destroyed || map == null)
			{
				return null;
			}
			if (count <= 0 || !((IntVec3)(ref pos)).IsValid)
			{
				return null;
			}
			IThingHolder parentHolder = t.ParentHolder;
			if (parentHolder == null)
			{
				return null;
			}
			if (parentHolder is Building_StorageCore { HaulSourceEnabled: false })
			{
				return null;
			}
			ThingOwner directlyHeldThings = parentHolder.GetDirectlyHeldThings();
			if (directlyHeldThings == null || !directlyHeldThings.Contains(t))
			{
				return null;
			}
			int num = Math.Min(count, t.stackCount);
			if (num <= 0)
			{
				return null;
			}
			Thing val;
			if (num >= t.stackCount)
			{
				directlyHeldThings.Remove(t);
				val = t;
			}
			else
			{
				val = t.SplitOff(num);
			}
			if (val == null)
			{
				return null;
			}
			Thing placed = null;
			Thing val2 = default(Thing);
			GenPlace.TryPlaceThing(val, pos, map, (ThingPlaceMode)1, ref val2, (Action<Thing, int>)delegate(Thing result, int amount)
			{
				placed = result;
				CompAutoIngest.MarkWithdrawn(result);
				if (forbid)
				{
					ForbidUtility.SetForbidden(result, true, false);
				}
			}, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			int num2 = ((!val.Destroyed) ? ((val.holdingOwner == null && !val.Spawned) ? val.stackCount : 0) : 0);
			extracted = num - num2;
			if (num2 > 0 && !directlyHeldThings.TryAdd(val, true))
			{
				GenPlace.TryPlaceThing(val, pos, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			}
			if (extracted <= 0)
			{
				return null;
			}
			return placed ?? val2;
		}

		public static int ExtractMatchingTo(Building_StorageCore core, int count, IntVec3 pos, Map map, Predicate<Thing> filter, bool forbid = true)
		{
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			if (core == null || !core.HaulSourceEnabled || filter == null || count <= 0 || map == null)
			{
				return 0;
			}
			int i;
			int extracted;
			for (i = 0; i < count; i += extracted)
			{
				Thing val = null;
				ThingOwner directlyHeldThings = core.GetDirectlyHeldThings();
				for (int j = 0; j < directlyHeldThings.Count; j++)
				{
					Thing val2 = directlyHeldThings[j];
					if (val2 != null && !val2.Destroyed && filter(val2) && (val == null || val2.stackCount > val.stackCount))
					{
						val = val2;
					}
				}
				if (val == null)
				{
					break;
				}
				int count2 = Math.Min(count - i, val.stackCount);
				ExtractTo(val, count2, pos, map, out extracted, forbid);
				if (extracted <= 0)
				{
					break;
				}
			}
			return i;
		}
	}
	public sealed class CoreStorageContainer : ThingOwner<Thing>
	{
		public CoreStorageContainer(IThingHolder owner)
			: base(owner)
		{
			((ThingOwner)this).dontTickContents = true;
		}

		private void UpdateCapacity()
		{
			((ThingOwner)this).maxStacks = Math.Max(((ThingOwner)this).Count, ((Building_StorageCore)(object)((ThingOwner)this).Owner).maxStacks);
		}

		public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true)
		{
			UpdateCapacity();
			return CountCanAcceptSafe(item, canMergeWithExistingStacks);
		}

		internal int CountCanAcceptSafe(Thing item, bool canMergeWithExistingStacks = true)
		{
			if (item == null || item.def == null || item.stackCount <= 0)
			{
				return 0;
			}
			if (((ThingOwner)this).maxStacks == 999999)
			{
				return item.stackCount;
			}
			long num = 0L;
			if (((ThingOwner)this).Count < ((ThingOwner)this).maxStacks)
			{
				num += (long)(((ThingOwner)this).maxStacks - ((ThingOwner)this).Count) * (long)item.def.stackLimit;
			}
			if (num >= item.stackCount)
			{
				return item.stackCount;
			}
			if (canMergeWithExistingStacks)
			{
				int i = 0;
				for (int count = ((ThingOwner)this).Count; i < count; i++)
				{
					Thing at = ((ThingOwner)this).GetAt(i);
					if (at != null && at.def != null && at.stackCount < at.def.stackLimit && at.CanStackWith(item))
					{
						num += at.def.stackLimit - at.stackCount;
						if (num >= item.stackCount)
						{
							return item.stackCount;
						}
					}
				}
			}
			if (num <= 0)
			{
				return 0;
			}
			return (int)Math.Min(num, item.stackCount);
		}

		private bool OwnerAllowsInsert(Thing item)
		{
			if (!(((ThingOwner)this).Owner is Building_StorageCore building_StorageCore) || !((Thing)building_StorageCore).Spawned)
			{
				return true;
			}
			return building_StorageCore.AllowsInsert(item);
		}

		public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
		{
			if (item == null || ((ThingOwner)this).GetCountCanAccept(item, canMergeWithExistingStacks) < item.stackCount)
			{
				return false;
			}
			if (!OwnerAllowsInsert(item))
			{
				return false;
			}
			return base.TryAdd(item, canMergeWithExistingStacks);
		}

		public override int TryAdd(Thing item, int count, bool canMergeWithExistingStacks = true)
		{
			if (item == null || count <= 0)
			{
				return 0;
			}
			if (!OwnerAllowsInsert(item))
			{
				return 0;
			}
			int num = Math.Min(count, ((ThingOwner)this).GetCountCanAccept(item, canMergeWithExistingStacks));
			return base.TryAdd(item, num, canMergeWithExistingStacks);
		}

		internal void AddLoaded(Thing item)
		{
			item.holdingOwner = (ThingOwner)(object)this;
			base.InnerListForReading.Add(item);
		}
	}
	public enum ItemGroup
	{
		Raw,
		Food,
		Manufactured,
		MedicalAmmo,
		BodyParts,
		Other
	}
	public static class ItemGrouping
	{
		private static readonly Dictionary<ThingDef, ItemGroup> cache = new Dictionary<ThingDef, ItemGroup>();

		public static ItemGroup GroupOf(ThingDef def)
		{
			if (def == null)
			{
				return ItemGroup.Other;
			}
			if (cache.TryGetValue(def, out var value))
			{
				return value;
			}
			value = ComputeGroup(def);
			cache[def] = value;
			return value;
		}

		private static ItemGroup ComputeGroup(ThingDef def)
		{
			if (def.thingCategories == null)
			{
				return ItemGroup.Other;
			}
			for (int i = 0; i < def.thingCategories.Count; i++)
			{
				for (ThingCategoryDef val = def.thingCategories[i]; val != null; val = val.parent)
				{
					switch (((Def)val).defName)
					{
					case "Medicine":
					case "Drugs":
					case "MortarShells":
						return ItemGroup.MedicalAmmo;
					case "BodyParts":
						return ItemGroup.BodyParts;
					}
				}
			}
			for (int j = 0; j < def.thingCategories.Count; j++)
			{
				for (ThingCategoryDef val2 = def.thingCategories[j]; val2 != null; val2 = val2.parent)
				{
					switch (((Def)val2).defName)
					{
					case "Foods":
						return ItemGroup.Food;
					case "ResourcesRaw":
						return ItemGroup.Raw;
					case "Textiles":
					case "Leathers":
					case "Wools":
					case "Manufactured":
						return ItemGroup.Manufactured;
					}
				}
			}
			return ItemGroup.Other;
		}

		public static string LabelKeyOf(ItemGroup g)
		{
			return g switch
			{
				ItemGroup.Raw => "DS_Group_Raw", 
				ItemGroup.Food => "DS_Group_Food", 
				ItemGroup.Manufactured => "DS_Group_Manufactured", 
				ItemGroup.MedicalAmmo => "DS_Group_MedicalAmmo", 
				ItemGroup.BodyParts => "DS_Group_BodyParts", 
				_ => "DS_Group_Other", 
			};
		}
	}
	internal static class ContainerAutoLoader
	{
		private static readonly List<TransferableOneWay> tmpEntries = new List<TransferableOneWay>();

		private static readonly List<Thing> tmpThings = new List<Thing>();

		private static readonly Dictionary<TransferableOneWay, int> tmpAlreadyLoading = new Dictionary<TransferableOneWay, int>();

		public static void FillTransporters(List<CompTransporter> transporters, Map map)
		{
			if (transporters == null || map == null)
			{
				return;
			}
			for (int i = 0; i < transporters.Count; i++)
			{
				CompTransporter val = transporters[i];
				if (val != null && ((ThingComp)val).parent != null && ((Thing)((ThingComp)val).parent).Spawned && val.innerContainer != null && val.leftToLoad != null && val.leftToLoad.Count != 0)
				{
					tmpEntries.Clear();
					tmpEntries.AddRange(val.leftToLoad);
					CollectAlreadyLoading((Thing)(object)((ThingComp)val).parent, JobDefOf.HaulToTransporter, val.leftToLoad, map);
					TransferFromCores(val.innerContainer, map);
				}
			}
		}

		public static void FillPortal(MapPortal portal)
		{
			if (portal != null && ((Thing)portal).Spawned && portal.leftToLoad != null && portal.leftToLoad.Count != 0)
			{
				ThingOwner directlyHeldThings = portal.GetDirectlyHeldThings();
				if (directlyHeldThings != null)
				{
					tmpEntries.Clear();
					tmpEntries.AddRange(portal.leftToLoad);
					CollectAlreadyLoading((Thing)(object)portal, JobDefOf.HaulToPortal, portal.leftToLoad, ((Thing)portal).Map);
					TransferFromCores(directlyHeldThings, ((Thing)portal).Map);
				}
			}
		}

		private static void CollectAlreadyLoading(Thing container, JobDef jobDef, List<TransferableOneWay> leftToLoad, Map map)
		{
			tmpAlreadyLoading.Clear();
			if (container == null || jobDef == null || leftToLoad == null || map == null)
			{
				return;
			}
			IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
			for (int i = 0; i < allPawnsSpawned.Count; i++)
			{
				Pawn val = allPawnsSpawned[i];
				if (val == null || val.CurJobDef != jobDef || val.jobs == null)
				{
					continue;
				}
				JobDriver curDriver = val.jobs.curDriver;
				JobDriver_HaulToContainer val2 = (JobDriver_HaulToContainer)(object)((curDriver is JobDriver_HaulToContainer) ? curDriver : null);
				if (val2 == null || val2.Container != container || val2.ThingToCarry == null)
				{
					continue;
				}
				JobDriver_HaulToTransporter val3 = (JobDriver_HaulToTransporter)(object)((val2 is JobDriver_HaulToTransporter) ? val2 : null);
				int initialCount;
				if (val3 != null)
				{
					initialCount = val3.initialCount;
				}
				else
				{
					JobDriver_HaulToPortal val4 = (JobDriver_HaulToPortal)(object)((val2 is JobDriver_HaulToPortal) ? val2 : null);
					if (val4 == null)
					{
						continue;
					}
					initialCount = val4.initialCount;
				}
				if (initialCount > 0)
				{
					TransferableOneWay val5 = TransferableUtility.TransferableMatchingDesperate(val2.ThingToCarry, leftToLoad, (TransferAsOneMode)1);
					if (val5 != null)
					{
						tmpAlreadyLoading.TryGetValue(val5, out var value);
						tmpAlreadyLoading[val5] = value + initialCount;
					}
				}
			}
		}

		private static void TransferFromCores(ThingOwner dest, Map map)
		{
			//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
			if (dest == null || map == null)
			{
				return;
			}
			for (int i = 0; i < tmpEntries.Count; i++)
			{
				TransferableOneWay val = tmpEntries[i];
				if (val == null)
				{
					continue;
				}
				int num = ((Transferable)val).CountToTransfer;
				if (num <= 0)
				{
					continue;
				}
				if (tmpAlreadyLoading.TryGetValue(val, out var value))
				{
					num -= value;
				}
				if (num <= 0)
				{
					continue;
				}
				tmpThings.Clear();
				tmpThings.AddRange(val.things);
				for (int j = 0; j < tmpThings.Count; j++)
				{
					if (num <= 0)
					{
						break;
					}
					Thing val2 = tmpThings[j];
					if (val2 == null || val2.Destroyed || !(val2.ParentHolder is Building_StorageCore { IsUsableNow: not false } building_StorageCore))
					{
						continue;
					}
					ThingOwner directlyHeldThings = building_StorageCore.GetDirectlyHeldThings();
					if (directlyHeldThings == null || !directlyHeldThings.Contains(val2) || (map.reservationManager != null && map.reservationManager.IsReservedByAnyoneOf(LocalTargetInfo.op_Implicit(val2), Faction.OfPlayer)))
					{
						continue;
					}
					int num2 = Math.Min(num, val2.stackCount);
					if (num2 > 0)
					{
						int stackCount = val2.stackCount;
						directlyHeldThings.TryTransferToContainer(val2, dest, num2, true);
						int num3 = (directlyHeldThings.Contains(val2) ? (stackCount - val2.stackCount) : num2);
						if (num3 <= 0)
						{
							break;
						}
						num -= num3;
					}
				}
			}
			tmpEntries.Clear();
			tmpThings.Clear();
			tmpAlreadyLoading.Clear();
		}
	}
	internal static class DigitalDropRedirect
	{
		private static Building_StorageCore core;

		public static Building_StorageCore Core => core;

		public static bool Active => core != null;

		public static void Begin(Map map, IntVec3 near)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			core = FindNearestUsableCore(map, near);
		}

		public static void End()
		{
			core = null;
		}

		public static Building_StorageCore NearestUsableCore(Map map, IntVec3 near)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return FindNearestUsableCore(map, near);
		}

		private static Building_StorageCore FindNearestUsableCore(Map map, IntVec3 near)
		{
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			if (map == null)
			{
				return null;
			}
			List<Building_StorageCore> list = CoreFinder.AllUsableCores(map);
			if (list == null || list.Count == 0)
			{
				return null;
			}
			Building_StorageCore result = null;
			float num = float.MaxValue;
			for (int i = 0; i < list.Count; i++)
			{
				Building_StorageCore building_StorageCore = list[i];
				if (building_StorageCore != null && CoreFinder.IsUsable(building_StorageCore))
				{
					IntVec3 val = ((Thing)building_StorageCore).Position - near;
					float num2 = ((IntVec3)(ref val)).LengthHorizontalSquared;
					if (num2 < num)
					{
						num = num2;
						result = building_StorageCore;
					}
				}
			}
			return result;
		}

		public static bool TryIngestUnspawned(Building_StorageCore target, Thing t)
		{
			if (target == null || t == null || t.Destroyed)
			{
				return false;
			}
			if (((Thing)target).Map == null || t.Spawned || t.holdingOwner != null)
			{
				return false;
			}
			if (!target.Accepts(t))
			{
				return false;
			}
			int stackCount = t.stackCount;
			if (target.GetDirectlyHeldThings().GetCountCanAccept(t, true) < stackCount)
			{
				return false;
			}
			return target.TryStore(t, stackCount) == stackCount;
		}
	}
	internal static class CoreSupply
	{
		public static List<Building_StorageCore> CoresNearestFirst(Map map, IntVec3 near)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			List<Building_StorageCore> list = CoreFinder.AllUsableCores(map);
			if (list.Count > 1)
			{
				list.Sort(delegate(Building_StorageCore a, Building_StorageCore b)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0011: Unknown result type (might be due to invalid IL or missing references)
					//IL_001d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0023: Unknown result type (might be due to invalid IL or missing references)
					//IL_0028: Unknown result type (might be due to invalid IL or missing references)
					//IL_002d: Unknown result type (might be due to invalid IL or missing references)
					IntVec3 val = ((Thing)a).Position - near;
					int lengthHorizontalSquared = ((IntVec3)(ref val)).LengthHorizontalSquared;
					val = ((Thing)b).Position - near;
					return lengthHorizontalSquared.CompareTo(((IntVec3)(ref val)).LengthHorizontalSquared);
				});
			}
			return list;
		}

		public static void GatherContents(Map map, IntVec3 near, List<Thing> outThings)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			if (map == null || outThings == null)
			{
				return;
			}
			List<Building_StorageCore> list = CoresNearestFirst(map, near);
			for (int i = 0; i < list.Count; i++)
			{
				Building_StorageCore building_StorageCore = list[i];
				if (building_StorageCore == null || !building_StorageCore.IsUsableNow)
				{
					continue;
				}
				ThingOwner directlyHeldThings = building_StorageCore.GetDirectlyHeldThings();
				if (directlyHeldThings == null)
				{
					continue;
				}
				for (int j = 0; j < directlyHeldThings.Count; j++)
				{
					Thing val = directlyHeldThings[j];
					if (val != null && !val.Destroyed)
					{
						outThings.Add(val);
					}
				}
			}
		}

		public static bool TakeAndPlace(Thing t, int count, Func<Thing, bool> place)
		{
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			if (t == null || t.Destroyed || place == null)
			{
				return false;
			}
			IThingHolder parentHolder = t.ParentHolder;
			if (parentHolder == null)
			{
				return false;
			}
			if (parentHolder is Building_StorageCore { HaulSourceEnabled: false })
			{
				return false;
			}
			ThingOwner directlyHeldThings = parentHolder.GetDirectlyHeldThings();
			if (directlyHeldThings == null || !directlyHeldThings.Contains(t))
			{
				return false;
			}
			int num = Math.Min(count, t.stackCount);
			if (num <= 0)
			{
				return false;
			}
			Map mapHeld = t.MapHeld;
			IntVec3 positionHeld = t.PositionHeld;
			Thing val;
			if (num >= t.stackCount)
			{
				directlyHeldThings.Remove(t);
				val = t;
			}
			else
			{
				val = t.SplitOff(num);
				if (val == null)
				{
					return false;
				}
			}
			if (place(val))
			{
				return true;
			}
			if (!directlyHeldThings.TryAdd(val, true) && mapHeld != null && ((IntVec3)(ref positionHeld)).IsValid)
			{
				GenPlace.TryPlaceThing(val, positionHeld, mapHeld, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			}
			return false;
		}
	}
	public static class WasteUtil
	{
		public static bool IsHazardousWaste(ThingDef def)
		{
			if (def?.comps == null)
			{
				return false;
			}
			for (int i = 0; i < def.comps.Count; i++)
			{
				if (def.comps[i] is CompProperties_DissolutionEffectPollution)
				{
					return true;
				}
				if (def.comps[i] is CompProperties_Dissolution)
				{
					return true;
				}
			}
			return false;
		}

		public static bool IsHazardousWaste(Thing t)
		{
			if (t != null)
			{
				return IsHazardousWaste(t.def);
			}
			return false;
		}
	}
}
namespace DigitalStorage.Compatibility
{
	internal class CoreSlotGroupAdapter : ISlotGroupParent, IStoreSettingsParent, IHaulDestination
	{
		private static readonly List<IntVec3> NoCells = new List<IntVec3>();

		private readonly Building_StorageCore core;

		private SlotGroup slotGroup;

		public Building_StorageCore Core => core;

		public bool HaulDestinationEnabled => false;

		public IntVec3 Position => ((Thing)core).PositionHeld;

		public Map Map => ((Thing)core).Map;

		public bool StorageTabVisible => false;

		public bool IgnoreStoredThingsBeauty => true;

		public string GroupingLabel => ((Entity)core).Label;

		public int GroupingOrder => 0;

		public CoreSlotGroupAdapter(Building_StorageCore core)
		{
			this.core = core;
		}

		public bool Accepts(Thing t)
		{
			return false;
		}

		public StorageSettings GetStoreSettings()
		{
			return core.GetStoreSettings();
		}

		public StorageSettings GetParentStoreSettings()
		{
			return core.GetParentStoreSettings();
		}

		public void Notify_SettingsChanged()
		{
		}

		public IEnumerable<IntVec3> AllSlotCells()
		{
			return NoCells;
		}

		public List<IntVec3> AllSlotCellsList()
		{
			return NoCells;
		}

		public void Notify_ReceivedThing(Thing newItem)
		{
		}

		public void Notify_LostThing(Thing newItem)
		{
		}

		public string SlotYielderLabel()
		{
			return ((Entity)core).Label;
		}

		public SlotGroup GetSlotGroup()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Expected O, but got Unknown
			if (slotGroup == null)
			{
				slotGroup = new SlotGroup((ISlotGroupParent)(object)this);
			}
			return slotGroup;
		}
	}
	public static class Legacy30Migration
	{
		public class LegacyLedger : IExposable
		{
			public List<string> keys;

			public List<long> counts;

			public bool HasStock
			{
				get
				{
					if (keys != null && counts != null)
					{
						return keys.Count > 0;
					}
					return false;
				}
			}

			public void ExposeData()
			{
				Scribe_Collections.Look<string>(ref keys, "stockKeys", (LookMode)1, Array.Empty<object>());
				Scribe_Collections.Look<long>(ref counts, "stockCounts", (LookMode)1, Array.Empty<object>());
			}
		}

		public static int Migrate(Building_StorageCore core, LegacyLedger ledger)
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_014a: Unknown result type (might be due to invalid IL or missing references)
			if (core == null || ledger == null || ledger.keys == null || ledger.counts == null)
			{
				return 0;
			}
			ThingOwner directlyHeldThings = core.GetDirectlyHeldThings();
			Map map = ((Thing)core).Map;
			IntVec3 positionHeld = ((Thing)core).PositionHeld;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = Math.Min(ledger.keys.Count, ledger.counts.Count);
			for (int i = 0; i < num5; i++)
			{
				long num6 = ledger.counts[i];
				if (num6 <= 0)
				{
					continue;
				}
				if (!TryParseKey(ledger.keys[i], out var def, out var stuff))
				{
					num4++;
					continue;
				}
				int num7 = ((def.stackLimit <= 0) ? 1 : def.stackLimit);
				long num8 = num6;
				while (num8 > 0)
				{
					int num9 = (int)Math.Min(num8, num7);
					Thing val;
					try
					{
						val = ThingMaker.MakeThing(def, stuff);
					}
					catch (Exception ex)
					{
						Log.Warning("[DigitalStorage] 3.0 迁移：无法生成 " + ((Def)def).defName + "：" + ex.Message);
						num4 += num9;
						break;
					}
					if (val == null)
					{
						num4 += num9;
						break;
					}
					val.stackCount = num9;
					num8 -= num9;
					if (directlyHeldThings.Count < core.maxStacks)
					{
						core.AddRestored(val);
						num++;
						num2 += num9;
						continue;
					}
					if (map != null && GenPlace.TryPlaceThing(val, positionHeld, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
					{
						num3 += num9;
						continue;
					}
					Log.Error("[DigitalStorage] 3.0 迁移：既塞不进核心也落不了地，销毁 " + num9 + " x " + ((Def)def).defName);
					val.Destroy((DestroyMode)0);
					num4 += num9;
					break;
				}
			}
			if (num > 0 || num3 > 0 || num4 > 0)
			{
				Log.Warning("[DigitalStorage] 3.0 存档迁移完成（" + ((Entity)core).LabelShort + "）：" + num2 + " 单位 / " + num + " 堆进入核心" + ((num3 > 0) ? ("；容器已满，" + num3 + " 单位落在核心脚下") : "") + ((num4 > 0) ? ("；跳过 " + num4 + " 单位（def 已不存在或无法生成）") : ""));
			}
			return num2;
		}

		public static void ApplyLegacyFilter(Building_StorageCore core, ThingFilter legacy)
		{
			if (core == null || legacy == null)
			{
				return;
			}
			ThingFilter val = core.GetStoreSettings()?.filter;
			if (val == null)
			{
				return;
			}
			int num = 0;
			foreach (ThingDef allDef in DefDatabase<ThingDef>.AllDefs)
			{
				if (allDef != null && !legacy.Allows(allDef) && val.Allows(allDef))
				{
					val.SetAllow(allDef, false);
					num++;
				}
			}
			if (num > 0)
			{
				Log.Warning("[DigitalStorage] 3.0 迁移：" + ((Entity)core).LabelShort + " 沿用旧存储筛选，禁用了 " + num + " 个 def。");
			}
		}

		private static bool TryParseKey(string raw, out ThingDef def, out ThingDef stuff)
		{
			def = null;
			stuff = null;
			if (string.IsNullOrEmpty(raw))
			{
				return false;
			}
			int num = raw.IndexOf('|');
			string text = ((num < 0) ? raw : raw.Substring(0, num));
			string text2 = ((num < 0) ? "-" : raw.Substring(num + 1));
			def = DefDatabase<ThingDef>.GetNamedSilentFail(text);
			if (def == null)
			{
				return false;
			}
			if (text2 != "-")
			{
				stuff = DefDatabase<ThingDef>.GetNamedSilentFail(text2);
			}
			return true;
		}
	}
	internal static class PhinixCompatPatch
	{
		private sealed class Target
		{
			public MethodBase method;

			public string label;

			public string availableField = "availableItems";

			public string filteredField;

			public string updateFilteredMethod;

			public string countsField;

			public bool excludeMinified;
		}

		private static readonly List<Target> installed = new List<Target>();

		private static Type stackedThingsType;

		private static MethodInfo groupThingsMethod;

		private static FieldInfo thingsField;

		public static int InstalledCount => installed.Count;

		public static void Install(Harmony harmony)
		{
			try
			{
				TryInstall(harmony, "PhinixClient.TradeWindow", "PreOpen", new Target
				{
					label = "Phinix 交易窗口",
					filteredField = "filteredAvailableItems",
					excludeMinified = false
				});
				TryInstall(harmony, "PhinixRedPacket.RedPacketTab", "RefreshAvailableItems", new Target
				{
					label = "Phinix 红包",
					filteredField = "filteredItems",
					updateFilteredMethod = "UpdateFilteredItems",
					countsField = "itemCounts",
					excludeMinified = true
				});
			}
			catch (Exception ex)
			{
				Log.Warning("[DigitalStorage] Phinix 兼容补丁安装失败（已忽略）：" + ex);
			}
		}

		private static void TryInstall(Harmony harmony, string typeName, string methodName, Target target)
		{
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cb: Expected O, but got Unknown
			try
			{
				Type type = AccessTools.TypeByName(typeName);
				if (type == null)
				{
					return;
				}
				MethodInfo methodInfo = AccessTools.Method(type, methodName, (Type[])null, (Type[])null);
				if (methodInfo == null)
				{
					return;
				}
				if (stackedThingsType == null)
				{
					stackedThingsType = AccessTools.TypeByName("PhinixClient.StackedThings");
					if (stackedThingsType == null)
					{
						return;
					}
					groupThingsMethod = AccessTools.Method(stackedThingsType, "GroupThings", (Type[])null, (Type[])null);
					thingsField = AccessTools.Field(stackedThingsType, "Things");
					if (groupThingsMethod == null || thingsField == null)
					{
						return;
					}
				}
				MethodInfo methodInfo2 = AccessTools.Method(typeof(PhinixCompatPatch), "Postfix", (Type[])null, (Type[])null);
				harmony.Patch((MethodBase)methodInfo, (HarmonyMethod)null, new HarmonyMethod(methodInfo2), (HarmonyMethod)null, (HarmonyMethod)null);
				target.method = methodInfo;
				installed.Add(target);
				if (Prefs.DevMode)
				{
					Log.Warning("[DigitalStorage] Phinix 兼容补丁已挂：" + target.label + " ← " + typeName + "." + methodName);
				}
			}
			catch (Exception ex)
			{
				Log.Warning("[DigitalStorage] Phinix 兼容补丁挂点失败（已忽略）：" + typeName + "." + methodName + " : " + ex.Message);
			}
		}

		private static void Postfix(object __instance, MethodBase __originalMethod)
		{
			try
			{
				if (__instance == null || __originalMethod == null)
				{
					return;
				}
				Target target = FindTarget(__originalMethod);
				if (target == null || TryGetAllItemsTradable() == false)
				{
					return;
				}
				List<Thing> list = CollectContents(target.excludeMinified);
				if (list.Count == 0 || AlreadyListed(__instance, target.availableField, list))
				{
					return;
				}
				object obj = groupThingsMethod.Invoke(null, new object[1] { list });
				if (obj != null)
				{
					AppendToFieldList(__instance, target.availableField, obj);
					if (!string.IsNullOrEmpty(target.updateFilteredMethod))
					{
						InvokeIfExists(__instance, target.updateFilteredMethod);
					}
					else if (!string.IsNullOrEmpty(target.filteredField) && target.filteredField != target.availableField)
					{
						AppendToFieldList(__instance, target.filteredField, obj);
					}
					if (!string.IsNullOrEmpty(target.countsField))
					{
						AddToCounts(__instance, target.countsField, obj);
					}
				}
			}
			catch (Exception ex)
			{
				if (Prefs.DevMode)
				{
					Log.Warning("[DigitalStorage] Phinix 兼容补丁 postfix 异常（已吞）：" + ex.Message);
				}
			}
		}

		private static Target FindTarget(MethodBase original)
		{
			for (int i = 0; i < installed.Count; i++)
			{
				Target target = installed[i];
				if (!(target.method == null))
				{
					if ((object)target.method == original)
					{
						return target;
					}
					if (target.method.Name == original.Name && target.method.DeclaringType == original.DeclaringType)
					{
						return target;
					}
				}
			}
			return null;
		}

		private static List<Thing> CollectContents(bool excludeMinified)
		{
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Invalid comparison between Unknown and I4
			List<Thing> list = new List<Thing>();
			List<Map> maps = Find.Maps;
			if (maps == null)
			{
				return list;
			}
			for (int i = 0; i < maps.Count; i++)
			{
				Map val = maps[i];
				if (val == null || !val.IsPlayerHome)
				{
					continue;
				}
				List<IHaulSource> list2 = HaulSourceContents.EnabledSources(val);
				for (int j = 0; j < list2.Count; j++)
				{
					if (!(list2[j] is Building_StorageCore building_StorageCore))
					{
						continue;
					}
					ThingOwner directlyHeldThings = building_StorageCore.GetDirectlyHeldThings();
					if (directlyHeldThings == null)
					{
						continue;
					}
					for (int k = 0; k < directlyHeldThings.Count; k++)
					{
						Thing val2 = directlyHeldThings[k];
						if (val2 != null && !val2.Destroyed && val2.def != null && (int)val2.def.category == 2 && !val2.def.IsCorpse && (!excludeMinified || !(val2 is MinifiedThing)))
						{
							list.Add(val2);
						}
					}
				}
			}
			return list;
		}

		public static bool? TryGetAllItemsTradable()
		{
			try
			{
				Type type = AccessTools.TypeByName("PhinixClient.Client");
				if (type == null)
				{
					return null;
				}
				object obj = AccessTools.Property(type, "Instance")?.GetValue(null) ?? AccessTools.Field(type, "Instance")?.GetValue(null);
				if (obj == null)
				{
					return null;
				}
				object obj2 = AccessTools.Property(type, "Settings")?.GetValue(obj);
				if (obj2 == null)
				{
					return null;
				}
				PropertyInfo propertyInfo = AccessTools.Property(obj2.GetType(), "AllItemsTradable");
				return (((propertyInfo == null) ? null : propertyInfo.GetValue(obj2)) is bool value) ? new bool?(value) : ((bool?)null);
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static void AppendToFieldList(object instance, string fieldName, object grouped)
		{
			IList list = AccessTools.Field(instance.GetType(), fieldName)?.GetValue(instance) as IList;
			IEnumerable enumerable = grouped as IEnumerable;
			if (list == null || enumerable == null)
			{
				return;
			}
			foreach (object item in enumerable)
			{
				list.Add(item);
			}
		}

		private static void InvokeIfExists(object instance, string methodName)
		{
			AccessTools.Method(instance.GetType(), methodName, (Type[])null, (Type[])null)?.Invoke(instance, null);
		}

		private static void AddToCounts(object instance, string fieldName, object grouped)
		{
			IDictionary dictionary = AccessTools.Field(instance.GetType(), fieldName)?.GetValue(instance) as IDictionary;
			IEnumerable enumerable = grouped as IEnumerable;
			if (dictionary == null || enumerable == null)
			{
				return;
			}
			foreach (object item in enumerable)
			{
				object obj = item.GetType().GetProperty("Count")?.GetValue(item);
				if (obj != null)
				{
					dictionary[item] = obj;
				}
			}
		}

		private static bool AlreadyListed(object instance, string fieldName, List<Thing> ours)
		{
			if (!(AccessTools.Field(instance.GetType(), fieldName)?.GetValue(instance) is IList { Count: not 0 } list) || thingsField == null)
			{
				return false;
			}
			HashSet<Thing> hashSet = new HashSet<Thing>(ours);
			for (int i = 0; i < list.Count; i++)
			{
				if (!(thingsField.GetValue(list[i]) is IList list2))
				{
					continue;
				}
				for (int j = 0; j < list2.Count; j++)
				{
					object obj = list2[j];
					Thing val = (Thing)((obj is Thing) ? obj : null);
					if (val != null && hashSet.Contains(val))
					{
						return true;
					}
				}
			}
			return false;
		}
	}
	internal static class PickUpAndHaulCompat
	{
		private const string PackageId = "Mehni.PickUpAndHaul";

		private const string PackageIdCn = "day29zh.PickUpAndHaul";

		private const string ProbeType = "PickUpAndHaul.CompHauledToInventory";

		private static bool probed;

		private static bool active;

		public static bool IsActive
		{
			get
			{
				if (probed)
				{
					return active;
				}
				probed = true;
				try
				{
					if (ModsConfig.IsActive("Mehni.PickUpAndHaul") || ModsConfig.IsActive("day29zh.PickUpAndHaul"))
					{
						active = true;
					}
					else
					{
						active = AccessTools.TypeByName("PickUpAndHaul.CompHauledToInventory") != null;
					}
				}
				catch (Exception ex)
				{
					active = false;
					Log.ErrorOnce("[DigitalStorage] 检测 Pick Up And Haul 时出错（按未安装处理）：" + ex, 1146307329);
				}
				return active;
			}
		}

		public static bool YieldToJobTargets
		{
			get
			{
				if (!DigitalStorageSettings.autoIngestYieldToJobs)
				{
					return IsActive;
				}
				return true;
			}
		}

		public static void LogOnce()
		{
			if (IsActive)
			{
				Log.Message("[DigitalStorage] 检测到 Pick Up And Haul：兼容已启用（批量拾取照常，自动收纳会对'被作业指着的东西'让位；核心的入库规则仍在容器插入漏斗上执行）");
			}
		}
	}
}
namespace DigitalStorage.Components
{
	[StaticConstructorOnStartup]
	public class Building_StorageCore : Building, IThingHolder, ISearchableContents, IHaulSource, IStoreSettingsParent, IHaulDestination, IApparelSource, IThingHolderTickable
	{
		private static readonly Material LightMat = MaterialPool.MatFrom("2.0/一束光", ShaderDatabase.MoteGlow);

		private static readonly Material OrbMat = MaterialPool.MatFrom("2.0/一个球", ShaderDatabase.Cutout);

		public ThingOwner<Thing> innerContainer;

		private StorageSettings storeSettings;

		private bool haulSourceEnabled = true;

		private static ThingFilter parentFilter;

		private bool haulDestinationEnabled = true;

		private int maxStacksField = 500;

		private StoragePriority storagePriorityField = (StoragePriority)3;

		private static readonly List<IntVec3> tmpInteractionCells = new List<IntVec3>(24);

		private int interactionCellCursor;

		private CoreSlotGroupAdapter compatSlotGroup;

		internal int lastHaulSweepTick;

		private Legacy30Migration.LegacyLedger legacyLedger;

		private ThingFilter legacyStorageFilter;

		private bool legacy30Migrated;

		private bool corpseFilterMigrated;

		private bool hazardousWasteFilterMigrated;

		private bool hazardousWasteFilterReopened;

		public int maxStacks => Math.Max(maxStacksField, CoreTier.Cap);

		public bool ShouldTickContents => false;

		public IThingHolder ParentHolder => (IThingHolder)(object)((Thing)this).Map;

		public ThingOwner SearchableContents => (ThingOwner)(object)innerContainer;

		public bool HaulSourceEnabled
		{
			get
			{
				if (haulSourceEnabled)
				{
					return IsUsableNow;
				}
				return false;
			}
		}

		public bool HaulDestinationEnabled
		{
			get
			{
				if (haulDestinationEnabled)
				{
					return IsUsableNow;
				}
				return false;
			}
		}

		internal bool DevHaulDestinationSwitchOn => haulDestinationEnabled;

		internal bool DevHaulSourceSwitchOn => haulSourceEnabled;

		public override List<IntVec3> InteractionCells
		{
			get
			{
				if (!((Thing)this).Spawned)
				{
					return ((Thing)this).InteractionCells;
				}
				Map map = ((Thing)this).Map;
				if (map == null)
				{
					return ((Thing)this).InteractionCells;
				}
				List<IntVec3> list = new List<IntVec3>(16);
				CollectInteractionCells(map, list);
				if (list.Count == 0)
				{
					return ((Thing)this).InteractionCells;
				}
				return list;
			}
		}

		public override IntVec3 InteractionCell
		{
			get
			{
				//IL_0009: Unknown result type (might be due to invalid IL or missing references)
				//IL_001a: Unknown result type (might be due to invalid IL or missing references)
				//IL_003b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0074: Unknown result type (might be due to invalid IL or missing references)
				//IL_0079: Unknown result type (might be due to invalid IL or missing references)
				//IL_007c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0096: Unknown result type (might be due to invalid IL or missing references)
				//IL_0085: Unknown result type (might be due to invalid IL or missing references)
				if (!((Thing)this).Spawned)
				{
					return ((Thing)this).InteractionCell;
				}
				Map map = ((Thing)this).Map;
				if (map == null)
				{
					return ((Thing)this).InteractionCell;
				}
				CollectInteractionCells(map, tmpInteractionCells);
				int count = tmpInteractionCells.Count;
				if (count == 0)
				{
					return ((Thing)this).InteractionCell;
				}
				interactionCellCursor++;
				if (interactionCellCursor >= count)
				{
					interactionCellCursor = 0;
				}
				int num = interactionCellCursor;
				for (int i = 0; i < count; i++)
				{
					IntVec3 val = tmpInteractionCells[(num + i) % count];
					if (!CellHasStandingMapPawn(map, val))
					{
						return val;
					}
				}
				return tmpInteractionCells[num];
			}
		}

		public bool ApparelSourceEnabled => HaulSourceEnabled;

		public bool StorageTabVisible => false;

		public bool CompatSlotGroupRegistered { get; private set; }

		internal CoreSlotGroupAdapter CompatSlotGroup
		{
			get
			{
				if (compatSlotGroup == null)
				{
					compatSlotGroup = new CoreSlotGroupAdapter(this);
				}
				return compatSlotGroup;
			}
		}

		public StoragePriority storagePriority
		{
			get
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				return storagePriorityField;
			}
			set
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_000b: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_001f: Unknown result type (might be due to invalid IL or missing references)
				if (storagePriorityField != value)
				{
					storagePriorityField = value;
					if (storeSettings != null)
					{
						storeSettings.Priority = value;
					}
					Notify_SettingsChanged();
				}
			}
		}

		public ThingFilter StorageFilter => GetStoreSettings().filter;

		public bool Powered
		{
			get
			{
				CompPowerTrader comp = ((ThingWithComps)this).GetComp<CompPowerTrader>();
				if (comp == null)
				{
					return true;
				}
				return comp.PowerOn;
			}
		}

		public bool IsUsableNow
		{
			get
			{
				if (((Thing)this).Spawned && !((Thing)this).Destroyed)
				{
					return Powered;
				}
				return false;
			}
		}

		public Building_StorageCore()
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			innerContainer = new CoreStorageContainer((IThingHolder)(object)this);
			((ThingOwner)innerContainer).dontTickContents = true;
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return (ThingOwner)(object)innerContainer;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, (IList<Thing>)innerContainer);
		}

		private void CollectInteractionCells(Map map, List<IntVec3> outCells)
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			outCells.Clear();
			if (outCells.Capacity < 24)
			{
				outCells.Capacity = 24;
			}
			CellRect val = GenAdj.OccupiedRect((Thing)(object)this);
			CellRect val2 = ((CellRect)(ref val)).ExpandedBy(1);
			int y = ((Thing)this).Position.y;
			IntVec3 val3 = default(IntVec3);
			for (int i = val2.minX; i <= val2.maxX; i++)
			{
				for (int j = val2.minZ; j <= val2.maxZ; j++)
				{
					((IntVec3)(ref val3))..ctor(i, y, j);
					if (!((CellRect)(ref val)).Contains(val3) && GenGrid.InBounds(val3, map) && GenGrid.Standable(val3, map))
					{
						outCells.Add(val3);
					}
				}
			}
		}

		private static bool CellHasStandingMapPawn(Map map, IntVec3 cell)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			List<Thing> list = map.thingGrid.ThingsListAtFast(cell);
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] is Pawn)
				{
					return true;
				}
			}
			return false;
		}

		public bool Accepts(Thing t)
		{
			if (t == null || t.def == null)
			{
				return false;
			}
			if (!((Thing)this).Spawned)
			{
				return false;
			}
			if ((object)t.ParentHolder == this)
			{
				return FilterAllows(t);
			}
			if (!AllowsInsert(t))
			{
				return false;
			}
			return ((ThingOwner)innerContainer).GetCountCanAccept(t, true) > 0;
		}

		private bool FilterAllows(Thing t)
		{
			StorageSettings val = GetStoreSettings();
			if (val != null && val.filter != null)
			{
				return val.filter.Allows(t);
			}
			return true;
		}

		internal bool AllowsInsert(Thing t)
		{
			if (t == null || t.def == null)
			{
				return false;
			}
			if (!haulDestinationEnabled)
			{
				return false;
			}
			if (!Powered)
			{
				return false;
			}
			return FilterAllows(t);
		}

		internal void AddRestored(Thing t)
		{
			if (t == null)
			{
				return;
			}
			if (innerContainer is CoreStorageContainer coreStorageContainer)
			{
				coreStorageContainer.AddLoaded(t);
				return;
			}
			ThingOwner<Thing> obj = innerContainer;
			if (obj != null)
			{
				((ThingOwner)obj).TryAdd(t, true);
			}
		}

		public bool RemoveApparel(Apparel apparel)
		{
			if (ApparelSourceEnabled)
			{
				return ((ThingOwner)innerContainer).Remove((Thing)(object)apparel);
			}
			return false;
		}

		public StorageSettings GetStoreSettings()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Expected O, but got Unknown
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			if (storeSettings == null)
			{
				storeSettings = new StorageSettings((IStoreSettingsParent)(object)this);
				storeSettings.filter.SetAllowAll(GetParentFilterPublic(), false);
				storeSettings.Priority = storagePriorityField;
			}
			return storeSettings;
		}

		public StorageSettings GetParentStoreSettings()
		{
			if (((Thing)this).def == null || ((Thing)this).def.building == null)
			{
				return null;
			}
			return ((Thing)this).def.building.fixedStorageSettings;
		}

		public void Notify_SettingsChanged()
		{
			if (((Thing)this).Spawned && ((Thing)this).MapHeld != null)
			{
				ListerHaulables listerHaulables = ((Thing)this).MapHeld.listerHaulables;
				if (listerHaulables != null)
				{
					listerHaulables.Notify_HaulSourceChanged((IHaulSource)(object)this);
				}
				HaulDestinationManager haulDestinationManager = ((Thing)this).MapHeld.haulDestinationManager;
				if (haulDestinationManager != null)
				{
					haulDestinationManager.Notify_HaulDestinationChangedPriority();
				}
			}
		}

		private void RegisterCompatSlotGroup()
		{
			HaulDestinationManager val = ((Thing)this).Map?.haulDestinationManager;
			if (val != null)
			{
				CoreSlotGroupAdapter coreSlotGroupAdapter = CompatSlotGroup;
				if (val.AllHaulDestinationsListForReading.Contains((IHaulDestination)(object)coreSlotGroupAdapter))
				{
					CompatSlotGroupRegistered = true;
					return;
				}
				val.AddHaulDestination((IHaulDestination)(object)coreSlotGroupAdapter);
				CompatSlotGroupRegistered = true;
			}
		}

		private void UnregisterCompatSlotGroup()
		{
			HaulDestinationManager val = ((Thing)this).Map?.haulDestinationManager;
			if (val == null || compatSlotGroup == null)
			{
				CompatSlotGroupRegistered = false;
				return;
			}
			if (val.AllHaulDestinationsListForReading.Contains((IHaulDestination)(object)compatSlotGroup))
			{
				val.RemoveHaulDestination((IHaulDestination)(object)compatSlotGroup);
			}
			CompatSlotGroupRegistered = false;
		}

		public int TryStore(Thing t, int count)
		{
			if (t == null || t.Destroyed || count <= 0 || !Accepts(t))
			{
				return 0;
			}
			if ((object)t.holdingOwner == innerContainer)
			{
				return 0;
			}
			int num = Math.Min(count, ((ThingOwner)innerContainer).GetCountCanAccept(t, true));
			if (num <= 0)
			{
				return 0;
			}
			ThingOwner holdingOwner = t.holdingOwner;
			if (t.Spawned)
			{
				return 0;
			}
			Thing val = t.SplitOff(num);
			if (val == null)
			{
				return 0;
			}
			if (val == t && holdingOwner != null)
			{
				holdingOwner.Remove(val);
			}
			if (((ThingOwner)innerContainer).TryAdd(val, true))
			{
				return num;
			}
			if (val != t)
			{
				t.TryAbsorbStack(val, false);
			}
			else if (holdingOwner != null)
			{
				holdingOwner.TryAdd(val, false);
			}
			return 0;
		}

		public int TryStoreIgnoringFilter(Thing t, int count)
		{
			if (t == null || t.Destroyed || count <= 0)
			{
				return 0;
			}
			if (!((Thing)this).Spawned)
			{
				return 0;
			}
			if (!haulDestinationEnabled)
			{
				return 0;
			}
			if (t.Spawned)
			{
				return 0;
			}
			if ((object)t.holdingOwner == innerContainer)
			{
				return 0;
			}
			bool flag = false;
			try
			{
				StorageSettings val = GetStoreSettings();
				if (val != null && val.filter != null && !val.filter.Allows(t))
				{
					val.filter.SetAllow(t.def, true);
					Notify_SettingsChanged();
					flag = true;
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 自动产物打开核心筛选失败：" + ex, 1146307483);
			}
			int num = TryStore(t, count);
			if (num <= 0 && flag)
			{
				try
				{
					StorageSettings val2 = GetStoreSettings();
					if (val2 != null && val2.filter != null)
					{
						val2.filter.SetAllow(t.def, false);
						Notify_SettingsChanged();
					}
				}
				catch
				{
				}
			}
			return num;
		}

		private void EnsureContainer()
		{
			if (innerContainer is CoreStorageContainer)
			{
				return;
			}
			ThingOwner<Thing> val = innerContainer;
			CoreStorageContainer coreStorageContainer = new CoreStorageContainer((IThingHolder)(object)this);
			if (val != null)
			{
				while (((ThingOwner)val).Count > 0)
				{
					Thing val2 = val[((ThingOwner)val).Count - 1];
					((ThingOwner)val).Remove(val2);
					coreStorageContainer.AddLoaded(val2);
				}
			}
			innerContainer = coreStorageContainer;
		}

		public ThingFilter GetParentFilterPublic()
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Expected O, but got Unknown
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Invalid comparison between Unknown and I4
			if (parentFilter == null)
			{
				parentFilter = new ThingFilter();
				foreach (ThingDef allDef in DefDatabase<ThingDef>.AllDefs)
				{
					if ((int)allDef.category == 2 || allDef.IsCorpse)
					{
						parentFilter.SetAllow(allDef, true);
					}
				}
			}
			return parentFilter;
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			EnsureContainer();
			GetStoreSettings();
			if (((Thing)this).Faction == null && ((Thing)this).def != null && ((Thing)this).def.CanHaveFaction)
			{
				((Thing)this).SetFaction(Faction.OfPlayer, (Pawn)null);
			}
			RegisterCompatSlotGroup();
			MigrateLegacy30IfNeeded();
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Invalid comparison between Unknown and I4
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Invalid comparison between Unknown and I4
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			if ((int)mode == 4 || (int)mode == 2)
			{
				DropAllContents();
			}
			UnregisterCompatSlotGroup();
			((Building)this).DeSpawn(mode);
		}

		public override void Destroy(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Invalid comparison between Unknown and I4
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Invalid comparison between Unknown and I4
			if (((Thing)this).Spawned && (int)mode != 4 && (int)mode != 2)
			{
				DropAllContents();
			}
			((Building)this).Destroy(mode);
		}

		private void DropAllContents()
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			if (innerContainer == null || ((ThingOwner)innerContainer).Count == 0)
			{
				return;
			}
			Map map = ((Thing)this).Map;
			if (map == null)
			{
				return;
			}
			((ThingOwner)innerContainer).TryDropAll(((Thing)this).Position, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, true);
			if (((ThingOwner)innerContainer).Count <= 0)
			{
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < ((ThingOwner)innerContainer).Count; i++)
			{
				Thing val = innerContainer[i];
				if (val != null)
				{
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append(", ");
					}
					stringBuilder.Append(((Def)(val.def?)).defName).Append(" x").Append(val.stackCount);
				}
			}
			Log.Error("[DigitalStorage] " + ((ThingOwner)innerContainer).Count + " 个物品无法落在存储核心脚下（空间不足），将随建筑一起销毁：" + stringBuilder);
		}

		protected override void ReceiveCompSignal(string signal)
		{
			((ThingWithComps)this).ReceiveCompSignal(signal);
			if (signal == "PowerTurnedOn" || signal == "PowerTurnedOff")
			{
				Notify_SettingsChanged();
			}
		}

		public override void ExposeData()
		{
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fe: Invalid comparison between Unknown and I4
			((Building)this).ExposeData();
			Scribe_Deep.Look<ThingOwner<Thing>>(ref innerContainer, "innerContainer", new object[1] { this });
			Scribe_Deep.Look<StorageSettings>(ref storeSettings, "storeSettings", new object[1] { this });
			Scribe_Values.Look<bool>(ref haulSourceEnabled, "haulSourceEnabled", true, false);
			Scribe_Values.Look<bool>(ref haulDestinationEnabled, "haulDestinationEnabled", true, false);
			Scribe_Values.Look<int>(ref maxStacksField, "maxStacks", 500, false);
			Scribe_Values.Look<StoragePriority>(ref storagePriorityField, "storagePriority", (StoragePriority)3, false);
			Scribe_Deep.Look<Legacy30Migration.LegacyLedger>(ref legacyLedger, "ledger", Array.Empty<object>());
			Scribe_Deep.Look<ThingFilter>(ref legacyStorageFilter, "storageFilter", Array.Empty<object>());
			Scribe_Values.Look<bool>(ref legacy30Migrated, "dsLegacy30Migrated", false, false);
			Scribe_Values.Look<bool>(ref corpseFilterMigrated, "dsCorpseFilterMigrated", false, false);
			Scribe_Values.Look<bool>(ref hazardousWasteFilterMigrated, "dsWasteFilterMigrated", false, false);
			Scribe_Values.Look<bool>(ref hazardousWasteFilterReopened, "dsWasteReopened", false, false);
			if ((int)Scribe.mode == 4)
			{
				EnsureContainer();
				((ThingOwner)innerContainer).dontTickContents = true;
				GetStoreSettings();
				MigrateCorpseFilterIfNeeded();
				MigrateHazardousWasteFilterIfNeeded();
			}
		}

		private void MigrateCorpseFilterIfNeeded()
		{
			if (corpseFilterMigrated)
			{
				return;
			}
			corpseFilterMigrated = true;
			try
			{
				StorageSettings val = GetStoreSettings();
				if (val == null || val.filter == null)
				{
					return;
				}
				ThingFilter parentFilterPublic = GetParentFilterPublic();
				bool flag = false;
				foreach (ThingDef allDef in DefDatabase<ThingDef>.AllDefs)
				{
					if (allDef.IsCorpse && parentFilterPublic.Allows(allDef) && !val.filter.Allows(allDef))
					{
						val.filter.SetAllow(allDef, true);
						flag = true;
					}
				}
				if (flag)
				{
					Notify_SettingsChanged();
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 尸体筛选一次性迁移失败（其余功能不受影响）：" + ex, 1146307585);
			}
		}

		private void MigrateHazardousWasteFilterIfNeeded()
		{
			if (hazardousWasteFilterReopened)
			{
				return;
			}
			bool num = hazardousWasteFilterMigrated;
			hazardousWasteFilterMigrated = true;
			hazardousWasteFilterReopened = true;
			if (!num)
			{
				return;
			}
			try
			{
				StorageSettings val = GetStoreSettings();
				if (val == null || val.filter == null)
				{
					return;
				}
				ThingFilter parentFilterPublic = GetParentFilterPublic();
				bool flag = false;
				foreach (ThingDef allDef in DefDatabase<ThingDef>.AllDefs)
				{
					if (WasteUtil.IsHazardousWaste(allDef) && parentFilterPublic.Allows(allDef) && !val.filter.Allows(allDef))
					{
						val.filter.SetAllow(allDef, true);
						flag = true;
					}
				}
				if (flag)
				{
					Notify_SettingsChanged();
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 有害垃圾筛选一次性重开失败（其余功能不受影响）：" + ex, 1146307587);
			}
		}

		private void MigrateLegacy30IfNeeded()
		{
			if (!legacy30Migrated && (legacyLedger != null || legacyStorageFilter != null))
			{
				legacy30Migrated = true;
				if (legacyStorageFilter != null)
				{
					Legacy30Migration.ApplyLegacyFilter(this, legacyStorageFilter);
					legacyStorageFilter = null;
				}
				if (legacyLedger != null)
				{
					Legacy30Migration.Migrate(this, legacyLedger);
					legacyLedger = null;
				}
			}
		}

		protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			((ThingWithComps)this).DrawAt(drawLoc, flip);
			Vector3 val = drawLoc;
			val.y = Altitudes.AltitudeFor((AltitudeLayer)17);
			Matrix4x4 val2 = Matrix4x4.TRS(val, Quaternion.identity, new Vector3(3f, 10f, 3f));
			Graphics.DrawMesh(MeshPool.plane10, val2, LightMat, 0);
			float num = Mathf.Sin(Time.realtimeSinceStartup * 2f) * 0.15f;
			Vector3 val3 = drawLoc;
			val3.y = Altitudes.AltitudeFor((AltitudeLayer)17) + 0.01f;
			val3.z += num;
			Matrix4x4 val4 = Matrix4x4.TRS(val3, Quaternion.identity, new Vector3(3f, 10f, 3f));
			Graphics.DrawMesh(MeshPool.plane10, val4, OrbMat, 0);
		}

		public override string GetInspectString()
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder();
			string inspectString = ((ThingWithComps)this).GetInspectString();
			if (!string.IsNullOrEmpty(inspectString))
			{
				stringBuilder.AppendLine(inspectString);
			}
			stringBuilder.AppendLine(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_CoreInspect", NamedArgument.op_Implicit(CoreTier.Level), NamedArgument.op_Implicit(((ThingOwner)innerContainer).Count), NamedArgument.op_Implicit(((ThingOwner)innerContainer).TotalStackCount), NamedArgument.op_Implicit(maxStacks))));
			stringBuilder.AppendLine(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_CorePriority", NamedArgument.op_Implicit(((object)System.Runtime.CompilerServices.Unsafe.As<StoragePriority, StoragePriority>(ref storagePriorityField)/*cast due to constrained. prefix*/).ToString()))));
			if (!Powered)
			{
				stringBuilder.AppendLine(TaggedString.op_Implicit(Translator.Translate("DS_NoPower")));
			}
			return stringBuilder.ToString().TrimEnd();
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			yield return (Gizmo)new Command_Action
			{
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("DS_StorageFilter")),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("DS_StorageFilterDesc")),
				icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel", true),
				action = delegate
				{
					Find.WindowStack.Add((Window)(object)new Dialog_StorageFilter(this));
				}
			};
			CompAutoIngest autoIngest = ((ThingWithComps)this).GetComp<CompAutoIngest>();
			if (autoIngest != null && autoIngest.IsResearched)
			{
				yield return (Gizmo)new Command_Toggle
				{
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("DS_AutoIngest")),
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("DS_AutoIngestDesc")),
					icon = (Texture)(object)ContentFinder<Texture2D>.Get("收纳", true),
					isActive = () => autoIngest.Enabled,
					toggleAction = delegate
					{
						autoIngest.Enabled = !autoIngest.Enabled;
					}
				};
			}
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building)this).GetGizmos();
		}
	}
	public class CompProperties_AutoIngest : CompProperties
	{
		public CompProperties_AutoIngest()
		{
			base.compClass = typeof(CompAutoIngest);
		}
	}
	public class CompAutoIngest : ThingComp
	{
		internal enum Reject
		{
			None,
			NullOrDestroyed,
			NotOnMap,
			InPrisonArea,
			OnBillGiver,
			OnBillGiverRim,
			Reserved,
			JobTarget,
			RecentlyWithdrawn
		}

		private Building_StorageCore core;

		private bool enabled = true;

		private int ingestRateCache = -1;

		private bool researchedCache;

		private int researchCheckTick = -1;

		private static readonly Dictionary<int, int> withdrawnUntil = new Dictionary<int, int>();

		private static readonly Dictionary<int, int> rimFirstSeen = new Dictionary<int, int>();

		private const int BillRimGraceTicks = 300;

		private const int RimFirstSeenKeepTicks = 60000;

		private static Thing[] candidateBuffer = (Thing[])(object)new Thing[30];

		private static Building_StorageCore[] candidateDestBuffer = new Building_StorageCore[30];

		private static int scanOffset;

		private static int lastSweepTick = -1;

		public bool Enabled
		{
			get
			{
				return enabled;
			}
			set
			{
				enabled = value;
			}
		}

		public bool IsResearched
		{
			get
			{
				EnsureResearchCache();
				return researchedCache;
			}
		}

		internal int IngestRate
		{
			get
			{
				EnsureResearchCache();
				if (ingestRateCache <= 0)
				{
					return 1;
				}
				return ingestRateCache;
			}
		}

		internal static int ScanOffset => scanOffset;

		internal static void ClearSessionCaches()
		{
			withdrawnUntil.Clear();
			rimFirstSeen.Clear();
			scanOffset = 0;
			lastSweepTick = -1;
		}

		public static void MarkWithdrawn(Thing t)
		{
			if (t != null)
			{
				withdrawnUntil[t.thingIDNumber] = Find.TickManager.TicksGame + 300;
			}
		}

		public static bool IsRecentlyWithdrawn(Thing t)
		{
			if (t != null && withdrawnUntil.TryGetValue(t.thingIDNumber, out var value))
			{
				return Find.TickManager.TicksGame < value;
			}
			return false;
		}

		public static void SweepWithdrawn()
		{
			int ticksGame = Find.TickManager.TicksGame;
			if (ticksGame - lastSweepTick < 2000)
			{
				return;
			}
			lastSweepTick = ticksGame;
			List<int> list = new List<int>();
			foreach (KeyValuePair<int, int> item in withdrawnUntil)
			{
				if (ticksGame >= item.Value)
				{
					list.Add(item.Key);
				}
			}
			for (int i = 0; i < list.Count; i++)
			{
				withdrawnUntil.Remove(list[i]);
			}
			List<int> list2 = new List<int>();
			foreach (KeyValuePair<int, int> item2 in rimFirstSeen)
			{
				if (ticksGame - item2.Value >= 60000)
				{
					list2.Add(item2.Key);
				}
			}
			for (int j = 0; j < list2.Count; j++)
			{
				rimFirstSeen.Remove(list2[j]);
			}
		}

		public override void Initialize(CompProperties props)
		{
			((ThingComp)this).Initialize(props);
			core = base.parent as Building_StorageCore;
			enabled = DigitalStorageSettings.autoIngestEnabled;
		}

		private void EnsureResearchCache()
		{
			int ticksGame = Find.TickManager.TicksGame;
			if (ticksGame == researchCheckTick && ingestRateCache > 0)
			{
				return;
			}
			researchCheckTick = ticksGame;
			ResearchProjectDef obj = ResearchProjectDef.Named("DigitalStorage_AutoIngest1");
			researchedCache = obj != null && obj.IsFinished;
			ResearchProjectDef obj2 = ResearchProjectDef.Named("DigitalStorage_AutoIngest3");
			if (obj2 != null && obj2.IsFinished)
			{
				ingestRateCache = 10;
				return;
			}
			ResearchProjectDef obj3 = ResearchProjectDef.Named("DigitalStorage_AutoIngest2");
			if (obj3 != null && obj3.IsFinished)
			{
				ingestRateCache = 5;
			}
			else
			{
				ingestRateCache = 1;
			}
		}

		public override void CompTick()
		{
			//IL_010e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_011c: Unknown result type (might be due to invalid IL or missing references)
			//IL_012e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0130: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).CompTick();
			if (core == null || !enabled || !core.Powered || !DigitalStorageSettings.autoIngestEnabled || (Find.TickManager.TicksGame + ((Thing)core).thingIDNumber) % 15 != 0)
			{
				return;
			}
			EnsureResearchCache();
			if (!researchedCache)
			{
				return;
			}
			Map map = ((Thing)core).Map;
			if (map == null || ((ThingOwner)core.innerContainer).Count >= core.maxStacks)
			{
				return;
			}
			SweepWithdrawn();
			int num = ((ingestRateCache <= 0) ? 1 : ingestRateCache);
			int num2 = 0;
			int num3 = num * 3;
			if (candidateBuffer.Length < num3)
			{
				candidateBuffer = (Thing[])(object)new Thing[num3];
				candidateDestBuffer = new Building_StorageCore[num3];
			}
			int bufCount = 0;
			CollectCandidates(map, num3, ref bufCount);
			for (int i = 0; i < bufCount; i++)
			{
				if (num2 >= num)
				{
					break;
				}
				Thing val = candidateBuffer[i];
				Building_StorageCore building_StorageCore = candidateDestBuffer[i];
				if (val != null && !val.Destroyed && val.Spawned && building_StorageCore != null && !((Thing)building_StorageCore).Destroyed)
				{
					IntVec3 positionHeld = val.PositionHeld;
					Vector3 drawPos = val.DrawPos;
					if (TryIngest(building_StorageCore, val))
					{
						num2++;
						SpawnIngestBeam(map, positionHeld, drawPos);
					}
				}
			}
			for (int j = 0; j < bufCount; j++)
			{
				candidateBuffer[j] = null;
				candidateDestBuffer[j] = null;
			}
		}

		private void CollectCandidates(Map map, int bufSize, ref int bufCount)
		{
			ICollection<Thing> collection = map.listerHaulables.ThingsPotentiallyNeedingHauling();
			int count = collection.Count;
			if (count <= 0)
			{
				return;
			}
			int num = scanOffset % count;
			if (num < 0)
			{
				num = 0;
			}
			scanOffset = (num + bufSize) % count;
			int num2 = Math.Max(32, bufSize * 8);
			int num3 = 0;
			int num4 = 0;
			foreach (Thing item in collection)
			{
				if (num4++ >= num)
				{
					if (bufCount >= bufSize || num3 >= num2)
					{
						return;
					}
					num3++;
					TryAddCandidate(item, map, ref bufCount);
				}
			}
			if (bufCount >= bufSize)
			{
				return;
			}
			num4 = 0;
			foreach (Thing item2 in collection)
			{
				if (num4++ >= num || bufCount >= bufSize || num3 >= num2)
				{
					break;
				}
				num3++;
				TryAddCandidate(item2, map, ref bufCount);
			}
		}

		private void TryAddCandidate(Thing t, Map map, ref int bufCount)
		{
			if (t != null && !t.Destroyed && CanCollect(t, map))
			{
				Building_StorageCore building_StorageCore = WouldVanillaHaulIntoCore(map, t);
				if (building_StorageCore != null)
				{
					candidateBuffer[bufCount] = t;
					candidateDestBuffer[bufCount] = building_StorageCore;
					bufCount++;
				}
			}
		}

		private static void SpawnIngestBeam(Map map, IntVec3 cell, Vector3 drawPos)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			ThingDef namedSilentFail = DefDatabase<ThingDef>.GetNamedSilentFail("DS_IngestBeam");
			if (namedSilentFail != null && GenSpawn.Spawn(namedSilentFail, cell, map, (WipeMode)0) is Mote_DS_IngestBeam mote_DS_IngestBeam)
			{
				((Mote)mote_DS_IngestBeam).exactPosition = drawPos;
				mote_DS_IngestBeam.Init(Rand.Range(0f, 360f));
			}
		}

		internal static bool TryIngest(Building_StorageCore core, Thing t)
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			if (core == null || t == null || t.Destroyed)
			{
				return false;
			}
			if (!core.Accepts(t))
			{
				return false;
			}
			Map map = ((Thing)core).Map;
			if (map == null)
			{
				return false;
			}
			IntVec3 positionHeld = t.PositionHeld;
			int num = Math.Min(t.stackCount, core.GetDirectlyHeldThings().GetCountCanAccept(t, true));
			if (num <= 0)
			{
				return false;
			}
			Thing val = t.SplitOff(num);
			if (val.Spawned)
			{
				((Entity)val).DeSpawn((DestroyMode)0);
			}
			if (core.TryStore(val, num) == num)
			{
				return true;
			}
			GenPlace.TryPlaceThing(val, positionHeld, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			return false;
		}

		internal static Reject RejectReason(Thing t, Map map)
		{
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			if (t == null || t.Destroyed)
			{
				return Reject.NullOrDestroyed;
			}
			if (!t.Spawned)
			{
				return Reject.NotOnMap;
			}
			if (IsOnBillGiver(t, map))
			{
				return Reject.OnBillGiver;
			}
			if (PickUpAndHaulCompat.YieldToJobTargets && IsTargetOfAnyPawnJob(t, map))
			{
				return Reject.JobTarget;
			}
			if (IsInPrisonArea(t, map))
			{
				return Reject.InPrisonArea;
			}
			if (map.reservationManager.IsReserved(LocalTargetInfo.op_Implicit(t)))
			{
				return Reject.Reserved;
			}
			if (IsRecentlyWithdrawn(t))
			{
				return Reject.RecentlyWithdrawn;
			}
			if (DigitalStorageSettings.autoIngestYieldToJobs && IsInWaitingBillGiverRim(t, map))
			{
				return Reject.OnBillGiverRim;
			}
			return Reject.None;
		}

		private static bool IsOnBillGiver(Thing t, Map map)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return GridsUtility.GetEdifice(t.PositionHeld, map) is IBillGiver;
		}

		private static bool IsTargetOfAnyPawnJob(Thing t, Map map)
		{
			IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
			for (int i = 0; i < allPawnsSpawned.Count; i++)
			{
				Pawn val = allPawnsSpawned[i];
				if (val == null || val.jobs == null)
				{
					continue;
				}
				if (JobTargets(t, val.jobs.curJob))
				{
					return true;
				}
				JobQueue jobQueue = val.jobs.jobQueue;
				if (jobQueue == null)
				{
					continue;
				}
				for (int j = 0; j < jobQueue.Count; j++)
				{
					if (JobTargets(t, jobQueue[j].job))
					{
						return true;
					}
				}
			}
			return false;
		}

		private static bool JobTargets(Thing t, Job job)
		{
			if (job == null)
			{
				return false;
			}
			if (((LocalTargetInfo)(ref job.targetA)).Thing == t)
			{
				return true;
			}
			if (((LocalTargetInfo)(ref job.targetB)).Thing == t)
			{
				return true;
			}
			if (((LocalTargetInfo)(ref job.targetC)).Thing == t)
			{
				return true;
			}
			if (!TargetQueueContains(job.targetQueueA, t))
			{
				return TargetQueueContains(job.targetQueueB, t);
			}
			return true;
		}

		private static bool TargetQueueContains(List<LocalTargetInfo> queue, Thing t)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			if (queue == null)
			{
				return false;
			}
			for (int i = 0; i < queue.Count; i++)
			{
				LocalTargetInfo val = queue[i];
				if (((LocalTargetInfo)(ref val)).Thing == t)
				{
					return true;
				}
			}
			return false;
		}

		private static bool IsInWaitingBillGiverRim(Thing t, Map map)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			if (!HasWaitingBillGiverAround(t.PositionHeld, map))
			{
				return false;
			}
			return !RimGraceElapsed(t);
		}

		private static bool HasWaitingBillGiverAround(IntVec3 pos, Map map)
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			for (int i = 0; i < GenAdj.AdjacentCells.Length; i++)
			{
				IntVec3 val = pos + GenAdj.AdjacentCells[i];
				if (!GenGrid.InBounds(val, map))
				{
					continue;
				}
				Building edifice = GridsUtility.GetEdifice(val, map);
				if (edifice != null && !((Thing)edifice).Destroyed)
				{
					IBillGiver val2 = (IBillGiver)(object)((edifice is IBillGiver) ? edifice : null);
					if (val2 != null && val2.BillStack != null && val2.BillStack.Count > 0)
					{
						return true;
					}
				}
			}
			return false;
		}

		private static bool RimGraceElapsed(Thing t)
		{
			int ticksGame = Find.TickManager.TicksGame;
			if (!rimFirstSeen.TryGetValue(t.thingIDNumber, out var value))
			{
				rimFirstSeen[t.thingIDNumber] = ticksGame;
				return false;
			}
			return ticksGame - value >= 300;
		}

		private static bool IsInPrisonArea(Thing t, Map map)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			Room room = GridsUtility.GetRoom(t.PositionHeld, map);
			if (room == null)
			{
				return false;
			}
			if (room.IsPrisonCell)
			{
				return true;
			}
			List<Region> regions = room.Regions;
			for (int i = 0; i < regions.Count; i++)
			{
				List<Thing> list = regions[i].ListerThings.ThingsInGroup((ThingRequestGroup)12);
				for (int j = 0; j < list.Count; j++)
				{
					Thing obj = list[j];
					Pawn val = (Pawn)(object)((obj is Pawn) ? obj : null);
					if (val != null && val.IsPrisoner)
					{
						return true;
					}
				}
			}
			return false;
		}

		private static bool CanCollect(Thing t, Map map)
		{
			return RejectReason(t, map) == Reject.None;
		}

		internal static Building_StorageCore WouldVanillaHaulIntoCore(Map map, Thing t)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			if (map == null || t == null || t.Destroyed)
			{
				return null;
			}
			StoragePriority val = StoreUtility.CurrentStoragePriorityOf(t, false);
			IHaulDestination val2 = default(IHaulDestination);
			if (!StoreUtility.TryFindBestBetterNonSlotGroupStorageFor(t, (Pawn)null, map, val, Faction.OfPlayer, ref val2, false, false))
			{
				return null;
			}
			if (!(val2 is Building_StorageCore building_StorageCore) || ((Thing)building_StorageCore).Destroyed)
			{
				return null;
			}
			try
			{
				IntVec3 val3 = default(IntVec3);
				if (StoreUtility.TryFindBestBetterStoreCellFor(t, (Pawn)null, map, val, Faction.OfPlayer, ref val3, false) && StoreUtility.GetSlotGroup(val3, map).Settings.Priority >= building_StorageCore.GetStoreSettings().Priority)
				{
					return null;
				}
			}
			catch (Exception)
			{
			}
			if (!building_StorageCore.Powered)
			{
				return null;
			}
			return building_StorageCore;
		}

		public override void PostExposeData()
		{
			((ThingComp)this).PostExposeData();
			Scribe_Values.Look<bool>(ref enabled, "autoIngestEnabled", true, false);
		}
	}
	internal static class AutoIngestDevTool
	{
		private const string Category = "DigitalStorage";

		private static readonly StringBuilder SB = new StringBuilder();

		[DebugAction(/*Could not decode attribute arguments.*/)]
		private unsafe static void DiagnoseCell()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return;
			}
			IntVec3 val = UI.MouseCell();
			if (GenGrid.InBounds(val, currentMap))
			{
				MoteMaker.ThrowText(((IntVec3)(ref val)).ToVector3Shifted(), currentMap, "DS 诊断 → 日志", -1f);
				SB.Length = 0;
				List<Thing> list = currentMap.thingGrid.ThingsListAt(val);
				StringBuilder sB = SB;
				string[] obj = new string[5] { "=== [DS 诊断] 格子 ", null, null, null, null };
				IntVec3 val2 = val;
				obj[1] = ((object)(*(IntVec3*)(&val2))/*cast due to constrained. prefix*/).ToString();
				obj[2] = "  共 ";
				obj[3] = list.Count.ToString();
				obj[4] = " 个 Thing ===";
				sB.AppendLine(string.Concat(obj));
				AppendGlobalState(currentMap);
				if (list.Count == 0)
				{
					SB.AppendLine("  （这一格上没有任何 Thing）");
				}
				for (int i = 0; i < list.Count; i++)
				{
					AppendThing(currentMap, val, list[i], i);
				}
				SB.AppendLine("=== [DS 诊断] 结束 ===");
				Log.Warning(SB.ToString());
			}
		}

		[DebugAction(/*Could not decode attribute arguments.*/)]
		private static void DumpAllUncollected()
		{
			//IL_0124: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return;
			}
			List<Building_StorageCore> list = FindAllCores(currentMap);
			if (list.Count == 0)
			{
				Log.Warning("[DS 汇总] 这张图上没有任何存储核心。");
				return;
			}
			SB.Length = 0;
			SB.AppendLine("=== [DS 汇总] 本图未被自动收纳的可搬物 ===");
			AppendGlobalState(currentMap);
			List<Thing> list2 = new List<Thing>();
			foreach (Thing item in currentMap.listerHaulables.ThingsPotentiallyNeedingHauling())
			{
				if (item != null && item.Spawned)
				{
					list2.Add(item);
				}
			}
			SB.AppendLine("  原版待搬表里的东西：" + list2.Count + " 件");
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			for (int i = 0; i < list2.Count; i++)
			{
				Thing val = list2[i];
				CompAutoIngest.Reject reject = CompAutoIngest.RejectReason(val, currentMap);
				if (reject != CompAutoIngest.Reject.None)
				{
					num++;
					if (num4 < 40)
					{
						num4++;
						SB.AppendLine("  [过滤] " + Brief(val) + " @ " + ((object)val.PositionHeld/*cast due to constrained. prefix*/).ToString() + " → " + RejText(reject));
					}
				}
				else if (CompAutoIngest.WouldVanillaHaulIntoCore(currentMap, val) == null)
				{
					num2++;
					if (num4 < 40)
					{
						num4++;
						SB.AppendLine("  [目的地] " + Brief(val) + " @ " + ((object)val.PositionHeld/*cast due to constrained. prefix*/).ToString() + " → " + DestReason(currentMap, val, list));
					}
				}
				else
				{
					num3++;
				}
			}
			SB.AppendLine("  ---- 小结 ----");
			SB.AppendLine("  过滤层挡下：" + num + " / 目的地层挡下：" + num2 + " / 应被吸入：" + num3);
			if (num4 >= 40)
			{
				SB.AppendLine("  （明细已截断到 40 行）");
			}
			SB.AppendLine("=== [DS 汇总] 结束 ===");
			Log.Warning(SB.ToString());
		}

		[DebugAction(/*Could not decode attribute arguments.*/)]
		private unsafe static void ForceIngestCell()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return;
			}
			IntVec3 val = UI.MouseCell();
			if (!GenGrid.InBounds(val, currentMap))
			{
				return;
			}
			MoteMaker.ThrowText(((IntVec3)(ref val)).ToVector3Shifted(), currentMap, "DS 强制收纳 → 日志", -1f);
			List<Thing> list = new List<Thing>(currentMap.thingGrid.ThingsListAt(val));
			SB.Length = 0;
			StringBuilder sB = SB;
			string[] obj = new string[5] { "=== [DS 强制收纳] 格子 ", null, null, null, null };
			IntVec3 val2 = val;
			obj[1] = ((object)(*(IntVec3*)(&val2))/*cast due to constrained. prefix*/).ToString();
			obj[2] = "  共 ";
			obj[3] = list.Count.ToString();
			obj[4] = " 个 Thing ===";
			sB.AppendLine(string.Concat(obj));
			List<Building_StorageCore> list2 = FindAllCores(currentMap);
			if (list2.Count == 0)
			{
				SB.AppendLine("  本图没有核心，无从下手。");
				SB.AppendLine("=== [DS 强制收纳] 结束 ===");
				Log.Warning(SB.ToString());
				return;
			}
			for (int i = 0; i < list.Count; i++)
			{
				Thing val3 = list[i];
				if (val3 == null)
				{
					continue;
				}
				SB.Append("  " + Brief(val3) + " → ");
				CompAutoIngest.Reject reject = CompAutoIngest.RejectReason(val3, currentMap);
				if (reject != CompAutoIngest.Reject.None)
				{
					SB.AppendLine("✗ 过滤层挡下：" + RejText(reject));
					continue;
				}
				Building_StorageCore building_StorageCore = CompAutoIngest.WouldVanillaHaulIntoCore(currentMap, val3);
				if (building_StorageCore == null)
				{
					SB.AppendLine("✗ 目的地层挡下：" + DestReason(currentMap, val3, list2));
				}
				else if (CompAutoIngest.TryIngest(building_StorageCore, val3))
				{
					SB.AppendLine("✓ **已吸进** " + ((Entity)building_StorageCore).LabelCap + " @ " + ((object)((Thing)building_StorageCore).Position/*cast due to constrained. prefix*/).ToString() + "（⇒ 判定全通过，症状是排不到队，去看 ⑤ 队列深度）");
				}
				else
				{
					SB.AppendLine("✗ TryIngest 返回 false —— 判定通过但落地失败。 Accepts=" + building_StorageCore.Accepts(val3) + "（注意：本函数**不会**刷新 @ 里的判定，因为东西已经被放回原处）");
				}
			}
			SB.AppendLine("=== [DS 强制收纳] 结束 ===");
			Log.Warning(SB.ToString());
		}

		[DebugAction(/*Could not decode attribute arguments.*/)]
		private unsafe static void DiagnosePawn(Pawn p)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_014b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0150: Unknown result type (might be due to invalid IL or missing references)
			//IL_016b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0170: Unknown result type (might be due to invalid IL or missing references)
			//IL_018b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0190: Unknown result type (might be due to invalid IL or missing references)
			if (p == null)
			{
				return;
			}
			Map map = ((Thing)p).Map;
			if (map == null)
			{
				return;
			}
			MoteMaker.ThrowText(((Thing)p).DrawPos, map, "DS pawn 诊断 → 日志", -1f);
			SB.Length = 0;
			SB.AppendLine("=== [DS pawn] " + ((Entity)p).LabelShort + "  " + ((Def)((Thing)p).def).defName + "  阵营=" + ((((Thing)p).Faction == null) ? "无" : ((Thing)p).Faction.Name) + "  倒地=" + p.Downed + "  死亡=" + p.Dead + " ===");
			Job curJob = p.CurJob;
			if (curJob == null)
			{
				SB.AppendLine("  当前 job = null（**它现在没有任何工作** —— 原版会在下一个思考周期给它派活）");
			}
			else
			{
				SB.AppendLine("  当前 job = " + ((Def)curJob.def).defName + "   「" + curJob.GetReport(p) + "」");
				StringBuilder sB = SB;
				string[] obj = new string[10] { "    targetA=", null, null, null, null, null, null, null, null, null };
				LocalTargetInfo targetA = curJob.targetA;
				obj[1] = ((object)(*(LocalTargetInfo*)(&targetA))/*cast due to constrained. prefix*/).ToString();
				obj[2] = " / B=";
				targetA = curJob.targetB;
				obj[3] = ((object)(*(LocalTargetInfo*)(&targetA))/*cast due to constrained. prefix*/).ToString();
				obj[4] = " / C=";
				targetA = curJob.targetC;
				obj[5] = ((object)(*(LocalTargetInfo*)(&targetA))/*cast due to constrained. prefix*/).ToString();
				obj[6] = " / count=";
				obj[7] = curJob.count.ToString();
				obj[8] = " / targetQueueB=";
				obj[9] = ((curJob.targetQueueB != null) ? curJob.targetQueueB.Count : 0).ToString();
				sB.AppendLine(string.Concat(obj));
				if (curJob.bill != null)
				{
					SB.AppendLine("    bill = " + curJob.bill.Label);
				}
			}
			JobDriver val = ((p.jobs == null) ? null : p.jobs.curDriver);
			if (val != null)
			{
				SB.AppendLine("  driver = " + ((object)val).GetType().Name + "  ended=" + val.ended + "  toils[" + val.CurToilIndex + "] = " + val.CurToilString);
			}
			SB.AppendLine("  jobs.debugLog = " + (p.jobs != null && p.jobs.debugLog) + "（点「切换这个 pawn 的 job 调试日志」可打开，日志里就会有逐事件序列）");
			if (p.jobs != null && p.jobs.jobQueue != null && p.jobs.jobQueue.Count > 0)
			{
				SB.Append("  排队中的 job（" + p.jobs.jobQueue.Count + "）：");
				for (int i = 0; i < p.jobs.jobQueue.Count; i++)
				{
					SB.Append(((Def)p.jobs.jobQueue[i].job.def).defName + " ");
				}
				SB.AppendLine();
			}
			if (p.workSettings != null && p.workSettings.EverWork)
			{
				SB.AppendLine("  工作类型：建造=" + p.workSettings.GetPriority(WorkTypeDefOf.Construction) + " / 搬运=" + p.workSettings.GetPriority(WorkTypeDefOf.Hauling) + "  （0 = 关闭）");
			}
			else
			{
				SB.AppendLine("  工作类型：workSettings 不可用（非玩家派系 / 机械体 / 无工作能力）");
			}
			AppendThinkTreeState(p);
			AppendHaulWorkload(map, p);
			AppendConstructionWorkload(map, p);
			SB.AppendLine("=== [DS pawn] 结束 ===");
			Log.Warning(SB.ToString());
		}

		private unsafe static void AppendThinkTreeState(Pawn p)
		{
			//IL_051b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0520: Unknown result type (might be due to invalid IL or missing references)
			//IL_0146: Unknown result type (might be due to invalid IL or missing references)
			//IL_0665: Unknown result type (might be due to invalid IL or missing references)
			SB.AppendLine("  —— 它能不能拿到活（思考树分支）——");
			try
			{
				SB.AppendLine("    IsMechanoid=" + p.RaceProps.IsMechanoid + "  IsColonyMech=" + p.IsColonyMech + "  IsColonyMechPlayerControlled=" + p.IsColonyMechPlayerControlled + "  精神状态=" + ((p.MentalStateDef == null) ? "无" : ((Def)p.MentalStateDef).defName) + "  Overseer=" + ((MechanitorUtility.GetOverseer(p) == null) ? "无" : ((Entity)MechanitorUtility.GetOverseer(p)).LabelShort));
				SB.AppendLine("    征召中=" + p.Drafted + "  正在移动=" + ((p.pather == null) ? "?" : p.pather.Moving.ToString()) + "  行动能力=" + p.health.capacities.CapableOf(PawnCapacityDefOf.Moving) + "  可站立格=" + GenGrid.Standable(((Thing)p).PositionHeld, ((Thing)p).MapHeld));
				if (!p.RaceProps.IsMechanoid || !p.IsColonyMech)
				{
					SB.AppendLine("    ⚠ 它不是「殖民地机械体」⇒ Drone.xml 里那条玩家机械体分支（含 JobGiver_Work）整条被跳过，它做不了任何工作（只剩战斗 / 闲逛）。");
				}
			}
			catch (Exception ex)
			{
				SB.AppendLine("    身份判定异常：" + ex.GetType().Name);
			}
			ThingComp val = FindDroneComp(p);
			if (val == null)
			{
				SB.AppendLine("    工作模式：这个 pawn 身上没有 AncotLibrary.CompDrone（不是 Ancot 无人机系）");
			}
			else
			{
				try
				{
					object obj = FieldValue(val, "workMode");
					object obj2 = FieldValue(PropValue(val, "Props"), "initialWorkMode");
					SB.AppendLine("    工作模式 CompDrone.workMode = " + DefName(obj) + "（出厂值 " + DefName(obj2) + "）  depleted=" + FieldValue(val, "depleted")?.ToString() + "  initialized=" + FieldValue(val, "initialized"));
					SB.AppendLine("    电量 PowerTicksLeft=" + PropValue(val, "PowerTicksLeft")?.ToString() + "  PercentFull=" + PropValue(val, "PercentFull")?.ToString() + "  IsSelfShutdown=" + PropValue(val, "IsSelfShutdown"));
					if (obj == null)
					{
						SB.AppendLine("    ⚠ workMode 为 null ⇒ 三个「工作模式」分支全不匹配，它会一直掉到闲逛 / IdleError。这是 Ancot 无人机最典型的「不动」原因。");
					}
					else if (DefName(obj) != "Ancot_Work")
					{
						SB.AppendLine("    ⚠ 当前不是 Ancot_Work（工作）模式 ⇒ 它按设计就不干活（充电 / 休眠）。");
					}
				}
				catch (Exception ex2)
				{
					SB.AppendLine("    工作模式读取异常：" + ex2.GetType().Name);
				}
			}
			try
			{
				Lord lord = LordUtility.GetLord(p);
				PawnDuty val2 = ((p.mindState == null) ? null : p.mindState.duty);
				SB.AppendLine("    领主 lord = " + ((lord == null) ? "无" : (((object)lord.LordJob).GetType().Name + " / toil=" + ((lord.CurLordToil == null) ? "null" : ((object)lord.CurLordToil).GetType().Name) + " / 在本 toil 已 " + lord.ticksInToil + " tick / 成员 " + lord.ownedPawns.Count)));
				SB.AppendLine("    职责 mindState.duty = " + ((val2 == null) ? "无" : ((Def)val2.def).defName));
				if (val2 != null)
				{
					SB.AppendLine("    ⚠ 有 duty ⇒ Drone.xml 里**领主分支排在 JobGiver_Work 前面**，它先被领主接管；领主不给活才轮到工作 / 闲逛。");
				}
			}
			catch (Exception ex3)
			{
				SB.AppendLine("    领主/duty 读取异常：" + ex3.GetType().Name);
			}
			Job curJob = p.CurJob;
			if (curJob != null)
			{
				StringBuilder sB = SB;
				string[] obj3 = new string[12]
				{
					"    job 指纹：",
					((Def)curJob.def).defName,
					" / driver=",
					(curJob.def.driverClass == null) ? "null" : curJob.def.driverClass.Name,
					" / expiryInterval=",
					curJob.expiryInterval.ToString(),
					" / checkOverrideOnExpire=",
					curJob.checkOverrideOnExpire.ToString(),
					" / overrideFacing=",
					null,
					null,
					null
				};
				Rot4 overrideFacing = curJob.overrideFacing;
				obj3[9] = ((object)(*(Rot4*)(&overrideFacing))/*cast due to constrained. prefix*/).ToString();
				obj3[10] = " / workGiver=";
				obj3[11] = ((curJob.workGiverDef == null) ? "无" : ((Def)curJob.workGiverDef).defName);
				sB.AppendLine(string.Concat(obj3));
				if (curJob.def == JobDefOf.Wait)
				{
					if (curJob.expiryInterval == 100)
					{
						SB.AppendLine("       ⇒ expiryInterval=100 = **JobGiver_IdleError**：它前面的每个节点都没给出 job。去 Player.log 搜「issued IdleError wait job」。");
					}
					else if (curJob.expiryInterval == 250)
					{
						SB.AppendLine("       ⇒ expiryInterval=250 = 上一件活以 Errored / ErroredPather 收场后的恢复等待：日志里那次的异常栈才是根因。");
					}
					else if (curJob.expiryInterval == 150)
					{
						SB.AppendLine("       ⇒ expiryInterval=150 = JobUtility.StartErrorRecoverJob：派活时抛异常了（日志里那次的异常栈才是根因）。");
					}
					else
					{
						SB.AppendLine("       ⇒ 不是 100 / 250 / 150 ⇒ 多半来自**领主 duty 的 JobGiver_Idle**（看上面的 duty）。");
					}
				}
			}
			try
			{
				if (p.mindState != null)
				{
					SB.AppendLine("    mindState.IsIdle=" + p.mindState.IsIdle + "  lastJobTag=" + ((object)System.Runtime.CompilerServices.Unsafe.As<JobTag, JobTag>(ref p.mindState.lastJobTag)/*cast due to constrained. prefix*/).ToString());
				}
				Area val3 = ((p.playerSettings == null) ? null : p.playerSettings.AreaRestrictionInPawnCurrentMap);
				SB.AppendLine("    活动区 = " + ((val3 == null) ? "无限制" : val3.Label) + ((val3 == null) ? "" : ("  它在区内=" + val3[((Thing)p).PositionHeld])));
				if (p.needs != null && p.needs.energy != null)
				{
					SB.AppendLine("    机械体能量 needs.energy = " + GenText.ToStringPercent(((Need)p.needs.energy).CurLevelPercentage));
				}
			}
			catch (Exception ex4)
			{
				SB.AppendLine("    状态读取异常：" + ex4.GetType().Name);
			}
		}

		private static void AppendHaulWorkload(Map map, Pawn p)
		{
			//IL_01de: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_0136: Unknown result type (might be due to invalid IL or missing references)
			//IL_0161: Unknown result type (might be due to invalid IL or missing references)
			ICollection<Thing> collection;
			try
			{
				collection = map.listerHaulables.ThingsPotentiallyNeedingHauling();
			}
			catch (Exception ex)
			{
				SB.AppendLine("  —— 搬运活盘点 ——");
				SB.AppendLine("      原版待搬表读取失败：" + ex.GetType().Name);
				return;
			}
			if (collection == null)
			{
				SB.AppendLine("  —— 搬运活盘点 ——");
				SB.AppendLine("      原版待搬表 = null");
				return;
			}
			SB.AppendLine("  —— 搬运活盘点 ——");
			SB.AppendLine("      原版待搬表 listerHaulables = " + collection.Count + " 件（WorkGiver_Haul 的候选**只有**这一张表里的东西）");
			WorkGiver_Scanner val = null;
			WorkGiverDef named = DefDatabase<WorkGiverDef>.GetNamed("Haul", false);
			if (named != null)
			{
				WorkGiver worker = named.Worker;
				val = (WorkGiver_Scanner)(object)((worker is WorkGiver_Scanner) ? worker : null);
			}
			int num = 0;
			foreach (Thing item in collection)
			{
				if (item == null)
				{
					continue;
				}
				if (num >= 3)
				{
					break;
				}
				num++;
				bool flag = false;
				string text;
				try
				{
					if (val != null)
					{
						flag = val.HasJobOnThing(p, item, false);
					}
					text = "  CanReserve=" + ReservationUtility.CanReserve(p, LocalTargetInfo.op_Implicit(item), 1, -1, (ReservationLayerDef)null, false) + "  CanReach=" + ReachabilityUtility.CanReach(p, LocalTargetInfo.op_Implicit(item), (PathEndMode)3, (Danger)3, false, false, (TraverseMode)0) + "  已被订=" + map.reservationManager.IsReservedByAnyoneOf(LocalTargetInfo.op_Implicit(item), ((Thing)p).Faction);
				}
				catch (Exception ex2)
				{
					text = "  判定异常：" + ex2.GetType().Name;
				}
				SB.AppendLine("      · " + ((item.def == null) ? "?" : ((Def)item.def).defName) + " @" + ((object)item.PositionHeld/*cast due to constrained. prefix*/).ToString() + "  HasJobOnThing=" + flag + text);
			}
			if (collection.Count == 0)
			{
				SB.AppendLine("      ⇒ 表是空的：**没有搬运活可派**。地表物品已被核心收走（算中即入），它不动是正确行为 —— 想让它干活就得有框架 / 蓝图（我们的「从核心取料送工地」）或者别把东西全收进核心。");
			}
		}

		private static ThingComp FindDroneComp(Pawn p)
		{
			List<ThingComp> allComps = ((ThingWithComps)p).AllComps;
			if (allComps == null)
			{
				return null;
			}
			for (int i = 0; i < allComps.Count; i++)
			{
				ThingComp val = allComps[i];
				if (val == null)
				{
					continue;
				}
				Type type = ((object)val).GetType();
				while (type != null)
				{
					if (type.Name == "CompDrone")
					{
						return val;
					}
					type = type.BaseType;
				}
			}
			return null;
		}

		private static object FieldValue(object obj, string name)
		{
			if (obj == null)
			{
				return null;
			}
			Type type = obj.GetType();
			while (type != null)
			{
				FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null)
				{
					return field.GetValue(obj);
				}
				type = type.BaseType;
			}
			return null;
		}

		private static object PropValue(object obj, string name)
		{
			if (obj == null)
			{
				return null;
			}
			Type type = obj.GetType();
			while (type != null)
			{
				PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (property != null && property.CanRead)
				{
					return property.GetValue(obj, null);
				}
				type = type.BaseType;
			}
			return null;
		}

		private static string DefName(object obj)
		{
			if (obj == null)
			{
				return "null";
			}
			Def val = (Def)((obj is Def) ? obj : null);
			if (val == null)
			{
				return obj.ToString();
			}
			return val.defName;
		}

		private static void AppendConstructionWorkload(Map map, Pawn p)
		{
			List<Thing> list = map.listerThings.ThingsInGroup((ThingRequestGroup)11);
			List<Thing> list2 = map.listerThings.ThingsInGroup((ThingRequestGroup)9);
			SB.AppendLine("  本图工地：框架 " + list.Count + " / 蓝图 " + list2.Count);
			int num = 0;
			int num2 = 12;
			for (int i = 0; i < list.Count && i < num2; i++)
			{
				Thing obj = list[i];
				IConstructible val = (IConstructible)(object)((obj is IConstructible) ? obj : null);
				if (val != null && DSConstructionDelivery.CanMakeJob(p, val, forced: false))
				{
					num++;
				}
			}
			for (int j = 0; j < list2.Count && j < num2; j++)
			{
				Thing obj2 = list2[j];
				IConstructible val2 = (IConstructible)(object)((obj2 is IConstructible) ? obj2 : null);
				if (val2 != null && DSConstructionDelivery.CanMakeJob(p, val2, forced: false))
				{
					num++;
				}
			}
			SB.AppendLine("  我们的「从核心取料送工地」现在能接手的（前 " + num2 + " 个里数）：" + num + " 个");
			if (num > 0)
			{
				SB.AppendLine("     ⇒ 有活。若它就是站着不干，看上面工作类型里「建造 / 搬运」的优先级是不是 0，");
			}
			if (num > 0)
			{
				SB.AppendLine("       或者它刚从这个工作类型上被踢下来（把 debugLog 打开看事件序列）。");
			}
		}

		[DebugAction(/*Could not decode attribute arguments.*/)]
		private static void ToggleJobDebugLog(Pawn p)
		{
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			if (p != null && p.jobs != null)
			{
				p.jobs.debugLog = !p.jobs.debugLog;
				if (((Thing)p).Map != null)
				{
					MoteMaker.ThrowText(((Thing)p).DrawPos, ((Thing)p).Map, "DS job日志 " + (p.jobs.debugLog ? "开" : "关"), -1f);
				}
				Log.Warning("[DS] " + ((Entity)p).LabelShort + " 的 job 调试日志 = " + p.jobs.debugLog);
			}
		}

		private static void AppendGlobalState(Map map)
		{
			//IL_017f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0184: Unknown result type (might be due to invalid IL or missing references)
			//IL_0247: Unknown result type (might be due to invalid IL or missing references)
			//IL_024c: Unknown result type (might be due to invalid IL or missing references)
			SB.AppendLine("--- 核心层 ---");
			SB.AppendLine("  设置·自动收纳总开关 autoIngestEnabled = " + DigitalStorageSettings.autoIngestEnabled);
			SB.AppendLine("  设置·避让作业 autoIngestYieldToJobs = " + DigitalStorageSettings.autoIngestYieldToJobs + "（关 = 算中即入：『作业目标 / 工作台旁边宽限』那两条闸门不参与判定）");
			SB.AppendLine("  兼容·Pick Up And Haul 在场 = " + PickUpAndHaulCompat.IsActive + " / 作业目标让位实际生效 = " + PickUpAndHaulCompat.YieldToJobTargets + "（PUAH 在场时强制生效：它一趟捡多堆，被瞬移走就白跑）");
			SB.AppendLine("  研究·DigitalStorage_AutoIngest1 = " + ResearchDone("DigitalStorage_AutoIngest1") + " / 2 = " + ResearchDone("DigitalStorage_AutoIngest2") + " / 3 = " + ResearchDone("DigitalStorage_AutoIngest3"));
			List<Building_StorageCore> list = FindAllCores(map);
			if (list.Count == 0)
			{
				SB.AppendLine("  ⚠ 本图没有任何存储核心 → 什么都没得吸。");
				return;
			}
			for (int i = 0; i < list.Count; i++)
			{
				Building_StorageCore building_StorageCore = list[i];
				CompAutoIngest comp = ((ThingWithComps)building_StorageCore).GetComp<CompAutoIngest>();
				SB.Append("  [" + i + "] " + ((Entity)building_StorageCore).LabelCap + " @ " + ((object)((Thing)building_StorageCore).Position/*cast due to constrained. prefix*/).ToString() + "  通电=" + building_StorageCore.Powered + " 出库=" + building_StorageCore.HaulSourceEnabled + " 入库=" + building_StorageCore.HaulDestinationEnabled);
				SB.Append(" AutoIngest组件=" + (comp != null));
				if (comp != null)
				{
					SB.Append(" 开关=" + comp.Enabled);
				}
				SB.Append(" 优先级=" + ((object)building_StorageCore.GetStoreSettings().Priority/*cast due to constrained. prefix*/).ToString());
				SB.Append(" 堆=" + ((ThingOwner)building_StorageCore.innerContainer).Count + "/" + building_StorageCore.maxStacks + "(Lv" + CoreTier.Level + ")");
				SB.Append(" 兼容替身=" + building_StorageCore.CompatSlotGroupRegistered);
				SB.AppendLine();
			}
		}

		private unsafe static void AppendThing(Map map, IntVec3 cell, Thing t, int index)
		{
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0120: Unknown result type (might be due to invalid IL or missing references)
			//IL_0128: Unknown result type (might be due to invalid IL or missing references)
			//IL_015d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0162: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
			SB.AppendLine("--- #" + index + " " + Brief(t) + " ---");
			if (t == null)
			{
				SB.AppendLine("  （null）");
				return;
			}
			bool flag = map.listerHaulables.ThingsPotentiallyNeedingHauling().Contains(t);
			SB.AppendLine("  ① 原版待搬表 listerHaulables：" + (flag ? "在" : "★不在"));
			if (!flag)
			{
				string text = HaulableGateReason(t, map);
				SB.AppendLine("     不在的原因（逐条问的原版 API）：" + ((text == null) ? "（四道门都不拦，应该在表里 —— 可能是增量维护的滞后，等 1 秒再看）" : text));
				SB.AppendLine("     ⇒ 自动收纳的候选**只来自这张表**，不在表里 = 我们根本看不见它。");
			}
			CompAutoIngest.Reject reject = CompAutoIngest.RejectReason(t, map);
			SB.AppendLine("  ② 我们的过滤 RejectReason = " + reject.ToString() + ((reject == CompAutoIngest.Reject.None) ? "" : ("（" + RejText(reject) + "）")));
			IHaulDestination val = StoreUtility.CurrentHaulDestinationOf(t);
			StoragePriority val2 = StoreUtility.CurrentStoragePriorityOf(t, false);
			SlotGroup val3 = map.haulDestinationManager.SlotGroupAt(t.PositionHeld);
			SB.AppendLine("  ③ 现在在哪：" + ((val == null) ? "裸地（无储存归属）" : (TypeName(val) + " / " + ((object)((IStoreSettingsParent)val).GetStoreSettings().Priority/*cast due to constrained. prefix*/).ToString())) + "；CurrentStoragePriority = " + ((object)(*(StoragePriority*)(&val2))/*cast due to constrained. prefix*/).ToString() + "；所在格子储存组 = " + ((val3 == null) ? "无" : ((object)val3.Settings.Priority/*cast due to constrained. prefix*/).ToString()));
			SB.AppendLine("     IsInAnyStorage=" + StoreUtility.IsInAnyStorage(t) + " / IsInValidBestStorage=" + StoreUtility.IsInValidBestStorage(t) + " / IsForbidden(玩家)=" + ForbidUtility.IsForbidden(t, Faction.OfPlayer) + " / EverHaulable=" + t.def.EverHaulable + " / alwaysHaulable=" + t.def.alwaysHaulable);
			List<Building_StorageCore> cores = FindAllCores(map);
			Building_StorageCore building_StorageCore = CompAutoIngest.WouldVanillaHaulIntoCore(map, t);
			SB.AppendLine("  ④ 原版会不会把它搬进核心：" + ((building_StorageCore == null) ? "★不会" : ("会 → " + ((Entity)building_StorageCore).LabelCap + " @ " + ((object)((Thing)building_StorageCore).Position/*cast due to constrained. prefix*/).ToString())));
			if (building_StorageCore == null)
			{
				SB.AppendLine("     原因：" + DestReason(map, t, cores));
			}
			AppendQueueDepth(map, t, cores);
			SB.AppendLine("  ⑥ 结论：" + Verdict(t, flag, reject, building_StorageCore, cores));
		}

		private static void AppendQueueDepth(Map map, Thing t, List<Building_StorageCore> cores)
		{
			if (cores.Count == 0 || t == null)
			{
				return;
			}
			int num = ((ThingWithComps)cores[0]).GetComp<CompAutoIngest>()?.IngestRate ?? 1;
			int num2 = num * 3;
			ICollection<Thing> collection = map.listerHaulables.ThingsPotentiallyNeedingHauling();
			int num3 = -1;
			int num4 = 0;
			int num5 = 0;
			foreach (Thing item in collection)
			{
				if (num5++ >= 800)
				{
					break;
				}
				if (item != null && !item.Destroyed && CompAutoIngest.RejectReason(item, map) == CompAutoIngest.Reject.None && CompAutoIngest.WouldVanillaHaulIntoCore(map, item) != null)
				{
					if (item == t)
					{
						num3 = num4;
					}
					num4++;
				}
			}
			SB.AppendLine("  ⑤ 队列深度（每 " + 15 + " tick 一轮）：待搬表 " + collection.Count + " 件 / 其中可吸 " + num4 + " 件 / 每轮上限 rate=" + num + "、缓冲 bufSize=" + num2 + " / 轮转起点=" + CompAutoIngest.ScanOffset);
			if (num3 < 0)
			{
				SB.AppendLine("     目标不在这 " + 800 + " 件的可吸集合里（可能它自己不可吸，见 ①~④）");
				return;
			}
			if (num3 < num)
			{
				SB.AppendLine("     目标位次 = " + num3 + "（< rate）⇒ 本轮就该轮到它。若持续不吸请把这整段发我。");
				return;
			}
			if (num3 < num2)
			{
				SB.AppendLine("     目标位次 = " + num3 + "（在 rate 与 bufSize 之间）⇒ 本轮或下一轮轮到它。");
				return;
			}
			int num6 = num3 / num + 1;
			SB.AppendLine("     目标位次 = " + num3 + " ⇒ 前面还有 " + num3 + " 件可吸的排在它前面，按每轮 " + num + " 件算，约 " + num6 + " 轮（≈ " + ((float)num6 * 15f / 60f).ToString("F1") + " 秒）后轮到它。");
		}

		private static string HaulableGateReason(Thing t, Map map)
		{
			if (ForbidUtility.IsForbidden(t, Faction.OfPlayer))
			{
				return "被禁止（F3 解禁后会立刻入表）";
			}
			if (!t.def.alwaysHaulable)
			{
				if (!t.def.EverHaulable)
				{
					return "def.EverHaulable = false（原版压根不搬这类东西）";
				}
				if (map.designationManager.DesignationOn(t, DesignationDefOf.Haul) == null && !StoreUtility.IsInAnyStorage(t))
				{
					return "没有「搬运」标记，而且它不在任何储存里（原版要求二者至少一个）";
				}
			}
			if (StoreUtility.IsInValidBestStorage(t))
			{
				return "★ 原版认为它**已经在最优储存里**（CurrentHaulDestination 接受了它） ⇒ `ShouldBeHaulable` 直接剔除。看 ③ 的优先级对比与下面 ④ 的原因";
			}
			IThingHolder parentHolder = t.ParentHolder;
			IHaulSource val = (IHaulSource)(object)((parentHolder is IHaulSource) ? parentHolder : null);
			if (val != null && !val.HaulSourceEnabled)
			{
				return "它所在的容器关掉了出库（HaulSourceEnabled = false）";
			}
			return null;
		}

		private unsafe static string DestReason(Map map, Thing t, List<Building_StorageCore> cores)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0103: Unknown result type (might be due to invalid IL or missing references)
			//IL_0108: Unknown result type (might be due to invalid IL or missing references)
			StoragePriority val = StoreUtility.CurrentStoragePriorityOf(t, false);
			IHaulDestination val2 = default(IHaulDestination);
			if (!StoreUtility.TryFindBestBetterNonSlotGroupStorageFor(t, (Pawn)null, map, val, Faction.OfPlayer, ref val2, false, false))
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append("原版没找到更好的**非格子**容器（当前优先级 ").Append(val).Append("；`acceptSamePriority: false` ⇒ 核心优先级必须**严格高于**它现在待的地方）");
				AppendDestinationTable(stringBuilder, map, t, cores, val);
				return stringBuilder.ToString();
			}
			if (!(val2 is Building_StorageCore building_StorageCore))
			{
				return "原版选的更好的容器不是我们的核心，而是 " + TypeName(val2) + "（核心输了）";
			}
			IntVec3 val3 = default(IntVec3);
			if (StoreUtility.TryFindBestBetterStoreCellFor(t, (Pawn)null, map, val, Faction.OfPlayer, ref val3, false))
			{
				SlotGroup val4 = map.haulDestinationManager.SlotGroupAt(val3);
				StoragePriority val5 = (StoragePriority)((val4 != null) ? ((int)val4.Settings.Priority) : 0);
				if (val5 >= building_StorageCore.GetStoreSettings().Priority)
				{
					string[] obj = new string[7] { "存在同级或更高优先级的**格子储存** ", null, null, null, null, null, null };
					IntVec3 val6 = val3;
					obj[1] = ((object)(*(IntVec3*)(&val6))/*cast due to constrained. prefix*/).ToString();
					obj[2] = "（优先级 ";
					obj[3] = ((object)(*(StoragePriority*)(&val5))/*cast due to constrained. prefix*/).ToString();
					obj[4] = " ≥ 核心 ";
					obj[5] = ((object)building_StorageCore.GetStoreSettings().Priority/*cast due to constrained. prefix*/).ToString();
					obj[6] = "）⇒ 刻意让给格子";
					return string.Concat(obj);
				}
			}
			if (!building_StorageCore.Powered)
			{
				return "选中的核心 " + ((Entity)building_StorageCore).LabelCap + " 没通电（断电的核心原版也不会搬进去）";
			}
			if (!building_StorageCore.Accepts(t))
			{
				return "核心 " + ((Entity)building_StorageCore).LabelCap + " 的 Accepts() 返回 false（存储筛选不允许 / 容量满 / 已经装不下）；筛选允许=" + FilterAllows(building_StorageCore, t) + " 堆=" + ((ThingOwner)building_StorageCore.innerContainer).Count + "/" + building_StorageCore.maxStacks + " stackLimit=" + ((t.def == null) ? "?" : t.def.stackLimit.ToString()) + " 还能装=" + ((ThingOwner)building_StorageCore.innerContainer).GetCountCanAccept(t, true) + " 还能装(long)=" + ((CoreStorageContainer)building_StorageCore.innerContainer).CountCanAcceptSafe(t);
			}
			return "（走到这里说明应该能进 —— 若仍不吸请把这整段发我）";
		}

		private unsafe static void AppendDestinationTable(StringBuilder sb, Map map, Thing t, List<Building_StorageCore> cores, StoragePriority curPrio)
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_0328: Unknown result type (might be due to invalid IL or missing references)
			//IL_0200: Unknown result type (might be due to invalid IL or missing references)
			//IL_0206: Unknown result type (might be due to invalid IL or missing references)
			sb.AppendLine();
			sb.AppendLine("     —— 目的地表 dump（原版搜索顺序 = 优先级降序；当前件优先级 " + ((object)(*(StoragePriority*)(&curPrio))/*cast due to constrained. prefix*/).ToString() + "）——");
			sb.AppendLine("     —— 我方复刻（不调原版方法，只按同一串门重扫一遍）会选中：" + ReplicateBestContainer(map, t, curPrio) + " ——");
			List<IHaulDestination> allHaulDestinationsListForReading = map.haulDestinationManager.AllHaulDestinationsListForReading;
			for (int i = 0; i < allHaulDestinationsListForReading.Count; i++)
			{
				IHaulDestination val = allHaulDestinationsListForReading[i];
				string value;
				try
				{
					StorageSettings storeSettings = ((IStoreSettingsParent)val).GetStoreSettings();
					value = ((storeSettings == null) ? "<null>" : ((object)storeSettings.Priority/*cast due to constrained. prefix*/).ToString());
				}
				catch (Exception ex)
				{
					value = "<ex:" + ex.GetType().Name + ">";
				}
				string value2;
				try
				{
					value2 = val.HaulDestinationEnabled.ToString();
				}
				catch (Exception ex2)
				{
					value2 = "<ex:" + ex2.GetType().Name + ">";
				}
				string value3;
				try
				{
					value3 = val.Accepts(t).ToString();
				}
				catch (Exception ex3)
				{
					value3 = "<ex:" + ex3.GetType().Name + ">";
				}
				sb.Append("       [").Append(i).Append("] ")
					.Append(TypeName(val))
					.Append(" prio=")
					.Append(value)
					.Append(" enabled=")
					.Append(value2)
					.Append(" accepts=")
					.Append(value3);
				if (val is ISlotGroupParent)
				{
					sb.Append("  ← 格子型：原版直接跳过");
				}
				Thing val2 = (Thing)(object)((val is Thing) ? val : null);
				if (val2 != null)
				{
					sb.Append(" spawned=").Append(val2.Spawned).Append(" 阵营=")
						.Append((val2.Faction == null) ? "<null>" : val2.Faction.Name)
						.Append(" 被禁=")
						.Append(ForbidUtility.IsForbidden(val2, Faction.OfPlayer))
						.Append(" 距离=")
						.Append(IntVec3Utility.DistanceTo(val2.PositionHeld, t.PositionHeld).ToString("F1"));
				}
				else
				{
					sb.Append("  ← 不是 Thing（没有阵营/被禁/距离这几道门）");
				}
				sb.AppendLine();
			}
			for (int j = 0; j < cores.Count; j++)
			{
				Building_StorageCore building_StorageCore = cores[j];
				sb.Append("       核心「").Append(((Entity)building_StorageCore).LabelCap).Append("」")
					.Append(" 在目的地表里=")
					.Append(allHaulDestinationsListForReading.Contains((IHaulDestination)(object)building_StorageCore))
					.Append(" spawned=")
					.Append(((Thing)building_StorageCore).Spawned)
					.Append(" 已毁=")
					.Append(((Thing)building_StorageCore).Destroyed)
					.Append(" 通电=")
					.Append(building_StorageCore.Powered)
					.Append(" IsUsableNow=")
					.Append(building_StorageCore.IsUsableNow)
					.Append(" 入库开关=")
					.Append(building_StorageCore.DevHaulDestinationSwitchOn)
					.Append(" HaulDestinationEnabled=")
					.Append(building_StorageCore.HaulDestinationEnabled)
					.Append(" 优先级=")
					.Append(building_StorageCore.GetStoreSettings().Priority)
					.Append(" Accepts=")
					.Append(building_StorageCore.Accepts(t))
					.Append(" 筛选允许=")
					.Append(FilterAllows(building_StorageCore, t))
					.Append(" 堆=")
					.Append(((ThingOwner)building_StorageCore.innerContainer).Count)
					.Append("/")
					.Append(building_StorageCore.maxStacks)
					.Append(" stackLimit=")
					.Append((t.def == null) ? "?" : t.def.stackLimit.ToString())
					.Append(" 还能装=")
					.Append(((ThingOwner)building_StorageCore.innerContainer).GetCountCanAccept(t, true))
					.Append(" 还能装(long)=")
					.Append(((CoreStorageContainer)building_StorageCore.innerContainer).CountCanAcceptSafe(t))
					.Append(" 被禁=")
					.Append(ForbidUtility.IsForbidden((Thing)(object)building_StorageCore, Faction.OfPlayer))
					.Append(" 阵营=")
					.Append((((Thing)building_StorageCore).Faction == null) ? "<null>" : ((Thing)building_StorageCore).Faction.Name)
					.AppendLine();
			}
		}

		private static string ReplicateBestContainer(Map map, Thing t, StoragePriority curPrio)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0146: Unknown result type (might be due to invalid IL or missing references)
			//IL_014b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0166: Unknown result type (might be due to invalid IL or missing references)
			//IL_016b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			List<IHaulDestination> allHaulDestinationsListInPriorityOrder = map.haulDestinationManager.AllHaulDestinationsListInPriorityOrder;
			IntVec3 val = (t.SpawnedOrAnyParentSpawned ? t.PositionHeld : IntVec3.Invalid);
			float num = float.MaxValue;
			IHaulDestination val2 = null;
			for (int i = 0; i < allHaulDestinationsListInPriorityOrder.Count; i++)
			{
				IHaulDestination val3 = allHaulDestinationsListInPriorityOrder[i];
				if (val3 is ISlotGroupParent || !val3.HaulDestinationEnabled)
				{
					continue;
				}
				StoragePriority priority;
				try
				{
					StorageSettings storeSettings = ((IStoreSettingsParent)val3).GetStoreSettings();
					if (storeSettings == null)
					{
						continue;
					}
					priority = storeSettings.Priority;
					goto IL_0072;
				}
				catch (Exception)
				{
				}
				continue;
				IL_0072:
				if (priority <= curPrio)
				{
					break;
				}
				float num2 = (((IntVec3)(ref val)).IsValid ? ((float)IntVec3Utility.DistanceToSquared(val, val3.Position)) : 0f);
				if (num2 > num)
				{
					continue;
				}
				bool flag;
				try
				{
					flag = val3.Accepts(t);
				}
				catch (Exception)
				{
					continue;
				}
				if (flag)
				{
					Thing val4 = (Thing)(object)((val3 is Thing) ? val3 : null);
					if (val4 == null || (val4.Faction == Faction.OfPlayer && !ForbidUtility.IsForbidden(val4, Faction.OfPlayer)))
					{
						val2 = val3;
						num = num2;
					}
				}
			}
			if (val2 == null)
			{
				return "<无>";
			}
			Thing val5 = (Thing)(object)((val2 is Thing) ? val2 : null);
			return TypeName(val2) + ((val5 == null) ? "" : ("「" + ((Entity)val5).LabelShort + "」")) + " prio=" + ((object)((IStoreSettingsParent)val2).GetStoreSettings().Priority/*cast due to constrained. prefix*/).ToString() + " @" + ((object)val2.Position/*cast due to constrained. prefix*/).ToString();
		}

		private static string Verdict(Thing t, bool inPool, CompAutoIngest.Reject rej, Building_StorageCore dest, List<Building_StorageCore> cores)
		{
			if (cores.Count == 0)
			{
				return "本图没有核心，不可能被吸。";
			}
			if (!inPool)
			{
				return "★ 卡在**候选层**：不在原版待搬表里 ⇒ 自动收纳看不见它。这是最常见的一类。";
			}
			if (rej != CompAutoIngest.Reject.None)
			{
				return "★ 卡在**过滤层**：" + RejText(rej);
			}
			if (dest == null)
			{
				return "★ 卡在**目的地层**：原版不会把它搬进核心（见 ④ 的原因）。";
			}
			return "①②③④ 全通过 ⇒ **它被检查时会通过**。是否真的吸到，取决于 ⑤ 队列深度（每轮只吸 rate 件）。要立刻验证就点「立刻收纳这一格（动手）」。";
		}

		private static List<Building_StorageCore> FindAllCores(Map map)
		{
			List<Building_StorageCore> list = new List<Building_StorageCore>();
			List<IHaulSource> allHaulSourcesListForReading = map.haulDestinationManager.AllHaulSourcesListForReading;
			for (int i = 0; i < allHaulSourcesListForReading.Count; i++)
			{
				if (allHaulSourcesListForReading[i] is Building_StorageCore item)
				{
					list.Add(item);
				}
			}
			return list;
		}

		private static bool FilterAllows(Building_StorageCore core, Thing t)
		{
			StorageSettings storeSettings = core.GetStoreSettings();
			if (storeSettings != null && storeSettings.filter != null)
			{
				return storeSettings.filter.Allows(t);
			}
			return true;
		}

		private static bool ResearchDone(string defName)
		{
			ResearchProjectDef val = ResearchProjectDef.Named(defName);
			if (val != null)
			{
				return val.IsFinished;
			}
			return false;
		}

		private static string RejText(CompAutoIngest.Reject r)
		{
			return r switch
			{
				CompAutoIngest.Reject.NullOrDestroyed => "null 或已销毁", 
				CompAutoIngest.Reject.NotOnMap => "不在图上（在别人背包/容器里）——不能对它 DeSpawn", 
				CompAutoIngest.Reject.InPrisonArea => "在关押区（牢房或有囚犯的房间）——囚犯的饭不能被收", 
				CompAutoIngest.Reject.OnBillGiver => "在工作台的材料区（= 工作台自己占的格）——收了会触发原版把料搬走的循环", 
				CompAutoIngest.Reject.OnBillGiverRim => "在**还在等料的工作台旁边一圈**里、还没躺够宽限（300 tick）——先让位给原版/搬运工，躺够了自己会被收", 
				CompAutoIngest.Reject.Reserved => "已被某个 pawn 预订", 
				CompAutoIngest.Reject.JobTarget => "★ 它是某个 pawn **当前或排队中作业的目标**（原版/搬运 mod 正准备拿它）——吸走会让那个作业找不到东西、反复重取", 
				CompAutoIngest.Reject.RecentlyWithdrawn => "刚被取出（300 tick 保护窗口内）", 
				_ => r.ToString(), 
			};
		}

		private static string TypeName(object o)
		{
			if (o != null)
			{
				return o.GetType().Name;
			}
			return "null";
		}

		private static string Brief(Thing t)
		{
			if (t == null)
			{
				return "null";
			}
			if (t.def != null)
			{
				return ((Def)t.def).defName + " x" + t.stackCount + " [id=" + t.thingIDNumber + "]";
			}
			return "(无 def)";
		}
	}
	internal static class DigitalWorkerDevTool
	{
		private const string Category = "DigitalStorage";

		private const int MaxForeignLines = 40;

		private static readonly StringBuilder SB = new StringBuilder();

		[DebugAction(/*Could not decode attribute arguments.*/)]
		private static void DiagnoseWorker()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return;
			}
			IntVec3 val = UI.MouseCell();
			if (GenGrid.InBounds(val, currentMap))
			{
				CompDigitalWorker compDigitalWorker = WorkerAt(currentMap, val);
				if (compDigitalWorker == null)
				{
					Log.Warning("[DS 代理] 这一格（" + val.x + ", " + val.z + "）上没有代理建筑（要含 CompDigitalWorker 的建筑）。");
					return;
				}
				MoteMaker.ThrowText(((Thing)((ThingComp)compDigitalWorker).parent).DrawPos, currentMap, "DS 代理诊断 → 日志", -1f);
				CompProperties_DigitalWorker props = compDigitalWorker.Props;
				List<WorkTypeDef> types = props?.workTypes;
				SB.Length = 0;
				SB.AppendLine("=== [DS 代理] " + ((Entity)((ThingComp)compDigitalWorker).parent).LabelCap.ToString() + "  def=" + ((Def)((Thing)((ThingComp)compDigitalWorker).parent).def).defName + " ===");
				SB.AppendLine("  启用的工作类型：" + WorkTypeNames(types));
				SB.AppendLine("  在干的活：" + compDigitalWorker.ActiveCount + " 件 / 第一件 = " + ((compDigitalWorker.CurrentTask == null) ? "(无)" : (compDigitalWorker.CurrentTask.Label + " → " + compDigitalWorker.CurrentTask.TargetLabel)));
				SB.AppendLine("  状态：启用=" + compDigitalWorker.Enabled + " 通电=" + compDigitalWorker.Powered + " 能干活=" + compDigitalWorker.CanWork + " 扫描间隔=" + (props?.scanIntervalTicks ?? 0) + " tick");
				Pawn workerIfCreated = compDigitalWorker.WorkerIfCreated;
				SB.AppendLine("  资质载体（假 pawn）：" + ((workerIfCreated == null) ? "(还没建；建筑还没干过活)" : (((Entity)workerIfCreated).LabelShort + "  固定资质=" + (props?.skillLevel ?? 0))));
				AppendPerWorkType(types);
				AppendForeignGivers(types);
				SB.AppendLine("=== [DS 代理] 结束 ===");
				Log.Warning(SB.ToString());
			}
		}

		private static void AppendPerWorkType(List<WorkTypeDef> types)
		{
			if (types == null || types.Count == 0)
			{
				SB.AppendLine("  ⚠\ufe0f 这台建筑一个工作类型都没配（XML 的 workTypes 为空 / defName 写错）⇒ 它什么都不会扫。");
				return;
			}
			List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
			for (int i = 0; i < types.Count; i++)
			{
				WorkTypeDef val = types[i];
				if (val == null)
				{
					SB.AppendLine("  · (null) —— workTypes 里有个 defName 解析不出来（写错字 / 那个工作类型所属的 mod 没装）");
					continue;
				}
				List<WorkGiver> givers = DigitalTaskRegistry.FindGivers(val);
				SB.AppendLine("  · 工作类型 " + ((Def)val).defName + "（" + val.labelShort + "） ⇒ 实际会用的 giver：" + GiverNames(givers));
				for (int j = 0; j < allDefsListForReading.Count; j++)
				{
					WorkGiverDef val2 = allDefsListForReading[j];
					if (val2 != null && val2.workType == val)
					{
						DigitalTaskAdapter digitalTaskAdapter = DigitalTaskRegistry.AdapterFor(val2.Worker);
						string text = ((digitalTaskAdapter == null) ? "✗ 没有适配器（这类活我们不认识）" : (digitalTaskAdapter.AcceptsWorkType(val) ? ("✓ " + digitalTaskAdapter.GetType().Name) : ("✗ 被 HomeWorkTypes 拒绝：" + digitalTaskAdapter.GetType().Name + " 只认 " + JoinStrings(digitalTaskAdapter.HomeWorkTypes))));
						SB.AppendLine("        - " + ((Def)val2).defName + "  [" + ClassName(val2) + "]  " + text);
					}
				}
			}
		}

		private static void AppendForeignGivers(List<WorkTypeDef> types)
		{
			List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
			SB.AppendLine("  · 全库扫描（类属于我们、但工作类型不在本建筑列表里）：");
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				WorkGiverDef val = allDefsListForReading[i];
				if (val == null)
				{
					continue;
				}
				DigitalTaskAdapter digitalTaskAdapter = DigitalTaskRegistry.AdapterFor(val.Worker);
				if (digitalTaskAdapter != null && !ContainsWorkType(types, val.workType))
				{
					num++;
					if (num2 < 40)
					{
						num2++;
						SB.AppendLine("        - " + DefNameOf(val.workType) + " / " + ((Def)val).defName + "  [" + ClassName(val) + "]  → " + digitalTaskAdapter.GetType().Name + "（本建筑没配这个工作类型，所以不会干）");
					}
				}
			}
			if (num == 0)
			{
				SB.AppendLine("        (无 —— 全库里没有类属于我们但落在本建筑工作类型之外的 giver)");
			}
			else if (num > num2)
			{
				SB.AppendLine("        ……共 " + num + " 条，只列了前 " + num2 + " 条");
			}
		}

		private static CompDigitalWorker WorkerAt(Map map, IntVec3 cell)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			List<Thing> list = map.thingGrid.ThingsListAt(cell);
			for (int i = 0; i < list.Count; i++)
			{
				Thing val = list[i];
				if (val != null && !val.Destroyed)
				{
					CompDigitalWorker compDigitalWorker = ThingCompUtility.TryGetComp<CompDigitalWorker>(val);
					if (compDigitalWorker != null)
					{
						return compDigitalWorker;
					}
				}
			}
			return null;
		}

		private static bool ContainsWorkType(List<WorkTypeDef> types, WorkTypeDef wt)
		{
			if (types == null || wt == null)
			{
				return false;
			}
			for (int i = 0; i < types.Count; i++)
			{
				if (types[i] == wt)
				{
					return true;
				}
			}
			return false;
		}

		private static string WorkTypeNames(List<WorkTypeDef> types)
		{
			if (types == null || types.Count == 0)
			{
				return "(空)";
			}
			string text = null;
			for (int i = 0; i < types.Count; i++)
			{
				string text2 = ((types[i] == null) ? "(null)" : ((Def)types[i]).defName);
				text = ((text == null) ? text2 : (text + "+" + text2));
			}
			return text;
		}

		private static string GiverNames(List<WorkGiver> givers)
		{
			if (givers == null || givers.Count == 0)
			{
				return "(无)";
			}
			string text = null;
			for (int i = 0; i < givers.Count; i++)
			{
				WorkGiver val = givers[i];
				string text2 = ((val == null || val.def == null) ? "(null)" : ((Def)val.def).defName);
				text = ((text == null) ? text2 : (text + ", " + text2));
			}
			return text;
		}

		private static string JoinStrings(string[] arr)
		{
			if (arr == null || arr.Length == 0)
			{
				return "(不限制)";
			}
			string text = null;
			for (int i = 0; i < arr.Length; i++)
			{
				text = ((text == null) ? arr[i] : (text + "/" + arr[i]));
			}
			return text;
		}

		private static string ClassName(WorkGiverDef def)
		{
			if (def != null && !(def.giverClass == null))
			{
				return def.giverClass.Name;
			}
			return "?";
		}

		private static string DefNameOf(WorkTypeDef wt)
		{
			if (wt != null)
			{
				return ((Def)wt).defName;
			}
			return "(无工作类型)";
		}
	}
	internal static class CraftAutomationDevTool
	{
		private const string Category = "DigitalStorage";

		private static readonly StringBuilder SB = new StringBuilder();

		private static readonly List<CompBillAutomation> tmpProxies = new List<CompBillAutomation>();

		private static readonly List<WorkGiverDef> tmpGivers = new List<WorkGiverDef>();

		[DebugAction(/*Could not decode attribute arguments.*/)]
		private static void DiagnoseBench()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			//IL_010c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0153: Unknown result type (might be due to invalid IL or missing references)
			//IL_0158: Unknown result type (might be due to invalid IL or missing references)
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return;
			}
			IntVec3 val = UI.MouseCell();
			if (GenGrid.InBounds(val, currentMap))
			{
				Thing val2 = BenchAt(currentMap, val);
				if (val2 == null)
				{
					Log.Warning("[DS 制作] 这一格（" + val.x + ", " + val.z + "）上没有可做工的建筑（要 Building + IBillGiver）。多格台子请点它占的任一格。");
					return;
				}
				MoteMaker.ThrowText(val2.DrawPos, currentMap, "DS 台子诊断 → 日志", -1f);
				SB.Length = 0;
				SB.AppendLine("=== [DS 制作] 工作台 " + ((Entity)val2).LabelCap + "  def=" + ((Def)val2.def).defName + "  位置=" + ((object)val2.PositionHeld/*cast due to constrained. prefix*/).ToString() + " ===");
				SB.AppendLine("  占位矩形=" + ((object)GenAdj.OccupiedRect(val2)/*cast due to constrained. prefix*/).ToString() + "  有交互格=" + val2.def.hasInteractionCell + (val2.def.hasInteractionCell ? ("  交互格=" + ((object)val2.InteractionCell/*cast due to constrained. prefix*/).ToString()) : ""));
				AppendRecipeStats(val2);
				AppendGiverCoverage(val2);
				AppendProxyCoverage(currentMap, val2);
				SB.AppendLine("=== [DS 制作] 结束 ===");
				Log.Warning(SB.ToString());
			}
		}

		[DebugAction(/*Could not decode attribute arguments.*/)]
		private static void DiagnoseProxy()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_025d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0262: Unknown result type (might be due to invalid IL or missing references)
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return;
			}
			IntVec3 val = UI.MouseCell();
			if (!GenGrid.InBounds(val, currentMap))
			{
				return;
			}
			CompBillAutomation compBillAutomation = ProxyAt(currentMap, val);
			if (compBillAutomation == null)
			{
				Log.Warning("[DS 制作] 这一格（" + val.x + ", " + val.z + "）上没有制作代理。");
				return;
			}
			MoteMaker.ThrowText(((Thing)((ThingComp)compBillAutomation).parent).DrawPos, currentMap, "DS 代理诊断 → 日志", -1f);
			int effectiveScanRadius = compBillAutomation.EffectiveScanRadius;
			SB.Length = 0;
			SB.AppendLine("=== [DS 制作] " + ((Entity)((ThingComp)compBillAutomation).parent).LabelCap + "  def=" + ((Def)((Thing)((ThingComp)compBillAutomation).parent).def).defName + " ===");
			SB.AppendLine("  设置：扫描半径=" + effectiveScanRadius + " 格（" + (effectiveScanRadius * 2 + 1) + "×" + (effectiveScanRadius * 2 + 1) + "）  识别 mod 台子=" + DigitalStorageSettings.craftAnyBench + "  XML scanRadius=" + compBillAutomation.Props.scanRadius + "（已不参与计算，仅兼容）");
			SB.AppendLine("  本次扫描结果：台子 " + compBillAutomation.BenchesForReading.Count + " 台 / 可选配方 " + compBillAutomation.UnlockedRecipes.Count + " 个");
			if (compBillAutomation.BenchesForReading.Count == 0)
			{
				SB.AppendLine("  ⚠\ufe0f 一台都没扫到：先确认台子是 Building + IBillGiver 且在范围内（看上面半径）。");
			}
			else
			{
				for (int i = 0; i < compBillAutomation.BenchesForReading.Count; i++)
				{
					Thing val2 = compBillAutomation.BenchesForReading[i];
					if (val2 != null && !val2.Destroyed)
					{
						CompBillAutomation compBillAutomation2 = BillBenchClaims.OwnerOf(currentMap, val2);
						SB.AppendLine("    · " + ((Def)val2.def).defName + " @" + ((object)val2.PositionHeld/*cast due to constrained. prefix*/).ToString() + "  配方=" + SafeRecipeCount(val2) + "  认领=" + ((compBillAutomation2 == null) ? "无人" : ((compBillAutomation2 == compBillAutomation) ? "本代理" : "(其它代理)")));
					}
				}
			}
			AppendProxyLineage(currentMap, compBillAutomation);
			SB.AppendLine("=== [DS 制作] 结束 ===");
			Log.Warning(SB.ToString());
		}

		private static void AppendRecipeStats(Thing bench)
		{
			List<RecipeDef> allRecipes = bench.def.AllRecipes;
			List<RecipeDef> list = CraftUnlocks.For(bench.def);
			int num = 0;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] != null && list[i].AvailableNow)
				{
					num++;
				}
			}
			SB.AppendLine("  配方：AllRecipes=" + (allRecipes?.Count ?? 0) + "  自动化候选=" + list.Count + "  研究后可用=" + num);
			if (allRecipes == null || allRecipes.Count == 0)
			{
				SB.AppendLine("  ⚠\ufe0f AllRecipes 为空 ⇒ 这个 def 根本不在 ThingRequestGroup.PotentialBillGiver 里，原版与代理都不会把它当工作台（多半是 XML 少了 recipeUsers / recipes）。");
			}
			else if (num == 0)
			{
				SB.AppendLine("  ⚠\ufe0f 候选配方全被 AvailableNow 挡着（研究 / meme / 派系标签）⇒ 面板里不会出现。");
			}
		}

		private static void AppendGiverCoverage(Thing bench)
		{
			tmpGivers.Clear();
			List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
			int num = 0;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				WorkGiverDef val = allDefsListForReading[i];
				if (val != null && !(val.giverClass == null) && typeof(WorkGiver_DoBill).IsAssignableFrom(val.giverClass))
				{
					num++;
					if (val.fixedBillGiverDefs != null && val.fixedBillGiverDefs.Contains(bench.def))
					{
						tmpGivers.Add(val);
					}
				}
			}
			SB.AppendLine("  原版 DoBill giver：" + num + " 个，其中认领这型台子的 " + tmpGivers.Count + " 个");
			if (tmpGivers.Count > 0)
			{
				StringBuilder stringBuilder = new StringBuilder();
				for (int j = 0; j < tmpGivers.Count; j++)
				{
					if (j > 0)
					{
						stringBuilder.Append(", ");
					}
					stringBuilder.Append(((Def)tmpGivers[j]).defName);
				}
				SB.AppendLine("    认领者：" + stringBuilder);
			}
			else
			{
				SB.AppendLine("    ⚠\ufe0f 没有 giver 认领 ⇒ 原版口径下代理**永远取不到活**；放宽开关（识别 mod 工作台）=" + DigitalStorageSettings.craftAnyBench + (DigitalStorageSettings.craftAnyBench ? "，已开 ⇒ 代理会放宽这道白名单" : "，**已关** ⇒ 把设置里这一项打开"));
			}
			tmpGivers.Clear();
		}

		private unsafe static void AppendProxyCoverage(Map map, Thing bench)
		{
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00de: Unknown result type (might be due to invalid IL or missing references)
			//IL_0171: Unknown result type (might be due to invalid IL or missing references)
			//IL_0176: Unknown result type (might be due to invalid IL or missing references)
			//IL_0190: Unknown result type (might be due to invalid IL or missing references)
			//IL_0192: Unknown result type (might be due to invalid IL or missing references)
			CollectProxies(map, tmpProxies);
			SB.AppendLine("  本图制作代理：" + tmpProxies.Count + " 台");
			if (tmpProxies.Count == 0)
			{
				SB.AppendLine("    ⚠\ufe0f 本图没有制作代理。");
			}
			for (int i = 0; i < tmpProxies.Count; i++)
			{
				CompBillAutomation compBillAutomation = tmpProxies[i];
				if (compBillAutomation != null && ((ThingComp)compBillAutomation).parent != null)
				{
					int effectiveScanRadius = compBillAutomation.EffectiveScanRadius;
					CellRect val = CellRect.CenteredOn(((Thing)((ThingComp)compBillAutomation).parent).PositionHeld, effectiveScanRadius);
					bool flag = CompBillAutomation.InScanRange(bench, val);
					int num = (int)IntVec3Utility.DistanceTo(bench.PositionHeld, ((Thing)((ThingComp)compBillAutomation).parent).PositionHeld);
					CompBillAutomation compBillAutomation2 = BillBenchClaims.OwnerOf(map, bench);
					SB.AppendLine("    · 代理 @" + ((object)((Thing)((ThingComp)compBillAutomation).parent).PositionHeld/*cast due to constrained. prefix*/).ToString() + " 半径=" + effectiveScanRadius + "  距离=" + num + "  在范围内=" + flag + "  认领=" + ((compBillAutomation2 == null) ? "无人" : ((compBillAutomation2 == compBillAutomation) ? "本代理" : "(另一台代理)")));
					if (!flag)
					{
						StringBuilder sB = SB;
						string[] obj = new string[5]
						{
							"        ↳ 台子占位矩形 ",
							((object)GenAdj.OccupiedRect(bench)/*cast due to constrained. prefix*/).ToString(),
							" 与扫描矩形 ",
							null,
							null
						};
						CellRect val2 = val;
						obj[3] = ((object)(*(CellRect*)(&val2))/*cast due to constrained. prefix*/).ToString();
						obj[4] = " 不相交，交互格也不在内 ⇒ 调大扫描半径或把代理挪近。";
						sB.AppendLine(string.Concat(obj));
					}
				}
			}
			tmpProxies.Clear();
		}

		private static void AppendProxyLineage(Map map, CompBillAutomation comp)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			int effectiveScanRadius = comp.EffectiveScanRadius;
			CellRect rect = CellRect.CenteredOn(((Thing)((ThingComp)comp).parent).PositionHeld, effectiveScanRadius);
			List<Thing> list = map.listerThings.ThingsInGroup((ThingRequestGroup)13);
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < list.Count; i++)
			{
				Thing val = list[i];
				if (val == null || val.Destroyed || !(val is Building) || !(val is IBillGiver) || (object)val == ((ThingComp)comp).parent)
				{
					continue;
				}
				num++;
				if (!CompBillAutomation.InScanRange(val, rect))
				{
					num2++;
					if (num2 <= 12)
					{
						SB.AppendLine("    漏： " + ((Def)val.def).defName + " @" + ((object)val.PositionHeld/*cast due to constrained. prefix*/).ToString() + "  距离=" + (int)IntVec3Utility.DistanceTo(val.PositionHeld, ((Thing)((ThingComp)comp).parent).PositionHeld) + "  占位=" + ((object)GenAdj.OccupiedRect(val)/*cast due to constrained. prefix*/).ToString());
					}
				}
			}
			SB.AppendLine("  地图上可做工建筑 " + num + " 台，范围外 " + num2 + " 台" + ((num2 > 12) ? "（只列前 12 台）" : ""));
		}

		private static int SafeRecipeCount(Thing bench)
		{
			return CraftUnlocks.For(bench.def)?.Count ?? 0;
		}

		private static Thing BenchAt(Map map, IntVec3 cell)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			List<Thing> list = map.thingGrid.ThingsListAt(cell);
			for (int i = 0; i < list.Count; i++)
			{
				Thing val = list[i];
				if (val != null && !val.Destroyed && val is Building && val is IBillGiver)
				{
					return val;
				}
			}
			return null;
		}

		private static CompBillAutomation ProxyAt(Map map, IntVec3 cell)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			List<Thing> list = map.thingGrid.ThingsListAt(cell);
			for (int i = 0; i < list.Count; i++)
			{
				Thing val = list[i];
				if (val != null && !val.Destroyed)
				{
					CompBillAutomation compBillAutomation = ThingCompUtility.TryGetComp<CompBillAutomation>(val);
					if (compBillAutomation != null)
					{
						return compBillAutomation;
					}
				}
			}
			return null;
		}

		private static void CollectProxies(Map map, List<CompBillAutomation> outList)
		{
			outList.Clear();
			List<Thing> allThings = map.listerThings.AllThings;
			for (int i = 0; i < allThings.Count; i++)
			{
				Thing val = allThings[i];
				if (val != null && !val.Destroyed)
				{
					CompBillAutomation compBillAutomation = ThingCompUtility.TryGetComp<CompBillAutomation>(val);
					if (compBillAutomation != null)
					{
						outList.Add(compBillAutomation);
					}
				}
			}
		}
	}
	internal static class SupplyDevTool
	{
		private const string Category = "DigitalStorage";

		[DebugAction(/*Could not decode attribute arguments.*/)]
		private static void DiagnoseSupplyGate()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_033a: Unknown result type (might be due to invalid IL or missing references)
			//IL_039e: Unknown result type (might be due to invalid IL or missing references)
			//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e6: Unknown result type (might be due to invalid IL or missing references)
			Map currentMap = Find.CurrentMap;
			if (currentMap == null)
			{
				return;
			}
			IntVec3 val = UI.MouseCell();
			if (!GenGrid.InBounds(val, currentMap))
			{
				return;
			}
			Thing val2 = null;
			List<Thing> thingList = GridsUtility.GetThingList(val, currentMap);
			for (int i = 0; i < thingList.Count; i++)
			{
				if (thingList[i] is Blueprint || thingList[i] is Frame)
				{
					val2 = thingList[i];
					break;
				}
			}
			StringBuilder stringBuilder = new StringBuilder();
			if (val2 == null)
			{
				Log.Warning("[DS 建造取料] 这一格（" + val.x + ", " + val.z + "）没有蓝图 / 框。");
				return;
			}
			stringBuilder.AppendLine("[DS 建造取料] " + ((Entity)val2).LabelShort + " (" + ((Def)val2.def).defName + ") @ (" + val.x + ", " + val.z + ")");
			Thing singleSelectedThing = Find.Selector.SingleSelectedThing;
			Pawn val3 = (Pawn)(object)((singleSelectedThing is Pawn) ? singleSelectedThing : null);
			if (val3 == null)
			{
				stringBuilder.AppendLine("· 没有选中单个小人 —— 右键菜单只对选中的小人出现（多选时原版 work-giver 菜单整体关闭）。");
			}
			IConstructible val4 = (IConstructible)(object)((val2 is IConstructible) ? val2 : null);
			stringBuilder.AppendLine("· IConstructible: " + ((val4 != null) ? "是" : "否 ⇒ 原版与我们都当不了建造目标"));
			if (val4 == null)
			{
				Log.Warning(stringBuilder.ToString());
				return;
			}
			List<ThingDefCountClass> list = null;
			try
			{
				list = val4.TotalMaterialCost();
			}
			catch (Exception ex)
			{
				stringBuilder.AppendLine("· TotalMaterialCost 抛异常: " + ex.Message);
			}
			if (list != null)
			{
				stringBuilder.AppendLine("· 账单 " + list.Count + " 项：");
				for (int j = 0; j < list.Count; j++)
				{
					ThingDefCountClass val5 = list[j];
					if (val5?.thingDef != null)
					{
						int num;
						try
						{
							num = val4.ThingCountNeeded(val5.thingDef);
						}
						catch
						{
							num = -999;
						}
						stringBuilder.AppendLine("    - " + ((Def)val5.thingDef).defName + " · 账单 " + val5.count + " · 还差 " + num + " · 本图核心里有 " + HaulSourceContents.CountOf(currentMap, val5.thingDef));
					}
				}
			}
			if (val3 == null)
			{
				Log.Warning(stringBuilder.ToString());
				return;
			}
			stringBuilder.AppendLine("· 小人 " + ((Entity)val3).LabelShort + "：");
			stringBuilder.AppendLine("    - Spawned / 同图: " + ((Thing)val3).Spawned + " / " + (((Thing)val3).Map == currentMap));
			stringBuilder.AppendLine("    - 可用核心（含其它图）: " + CoreFinder.AnyUsableCoreGlobal(val3));
			stringBuilder.AppendLine("    - CanReach(Touch, Deadly): " + ReachabilityUtility.CanReach(val3, LocalTargetInfo.op_Implicit(val2), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0));
			stringBuilder.AppendLine("    - IsForbidden / IsBurning: " + ForbidUtility.IsForbidden(val2, val3) + " / " + FireUtility.IsBurning(val2));
			stringBuilder.AppendLine("    - CanReserve / 忽略他人预约: " + ReservationUtility.CanReserve(val3, LocalTargetInfo.op_Implicit(val2), 1, -1, (ReservationLayerDef)null, false) + " / " + ReservationUtility.CanReserve(val3, LocalTargetInfo.op_Implicit(val2), 1, -1, (ReservationLayerDef)null, true));
			Pawn val6 = currentMap.reservationManager.FirstRespectedReserver(LocalTargetInfo.op_Implicit(val2), val3, (ReservationLayerDef)null);
			if (val6 != null)
			{
				stringBuilder.AppendLine("    - 当前被预留: " + ((Entity)val6).LabelShort);
			}
			JobFailReason.Clear();
			bool flag;
			try
			{
				flag = GenConstruct.CanConstruct(val2, val3, WorkTypeDefOf.Hauling, true, DigitalStorage_JobDefOf.DigitalStorage_WithdrawToConstruction);
			}
			catch (Exception ex2)
			{
				flag = false;
				stringBuilder.AppendLine("    - CanConstruct 抛异常: " + ex2.Message);
			}
			stringBuilder.AppendLine("    - GenConstruct.CanConstruct(Hauling, forced, 我们的 JobDef): " + flag + " · 理由: " + (JobFailReason.HaveReason ? JobFailReason.Reason.ToString() : "（无）"));
			bool flag2;
			Job job;
			try
			{
				flag2 = DSConstructionDelivery.TryMakeJob(val3, val4, forced: true, out job);
			}
			catch (Exception ex3)
			{
				flag2 = false;
				job = null;
				stringBuilder.AppendLine("    - TryMakeJob 抛异常: " + ex3.Message);
			}
			stringBuilder.AppendLine("    - DSConstructionDelivery.TryMakeJob(forced): " + flag2 + ((job != null) ? (" · count=" + job.count) : ""));
			if (job != null)
			{
				stringBuilder.AppendLine("    ⇒ 产品路径应当能出菜单项；若右键仍没有，看启动日志的补丁类清单里有没有本 provider。");
			}
			Log.Warning(stringBuilder.ToString());
		}
	}
	public class CompProperties_DigitalWorker : CompProperties
	{
		public List<WorkTypeDef> workTypes;

		public int skillLevel = 8;

		public float workSpeedMult = 0.8f;

		public int scanIntervalTicks = 60;

		public int maxParallelPerWorkType = 1;

		public int maxParallelTotal;

		public int maxVisualTasks = 6;

		public CompProperties_DigitalWorker()
		{
			base.compClass = typeof(CompDigitalWorker);
		}

		public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
		{
			foreach (string item in <>n__0(parentDef))
			{
				yield return item;
			}
			if (workTypes == null || workTypes.Count == 0)
			{
				yield return ((Def)parentDef).defName + "：CompProperties_DigitalWorker 必须指定至少一个 <workTypes><li>…</li></workTypes>。";
			}
			if (skillLevel < 0 || skillLevel > 20)
			{
				yield return ((Def)parentDef).defName + "：skillLevel 应在 0~20（收到 " + skillLevel + "）。";
			}
			if (workSpeedMult <= 0f)
			{
				yield return ((Def)parentDef).defName + "：workSpeedMult 必须 > 0（收到 " + workSpeedMult + "）。";
			}
			if (maxParallelPerWorkType < 1)
			{
				yield return ((Def)parentDef).defName + "：maxParallelPerWorkType 至少为 1（收到 " + maxParallelPerWorkType + "）。";
			}
			if (maxParallelTotal < 0)
			{
				yield return ((Def)parentDef).defName + "：maxParallelTotal 不能为负（0 = 不限，收到 " + maxParallelTotal + "）。";
			}
			if (maxVisualTasks < 0)
			{
				yield return ((Def)parentDef).defName + "：maxVisualTasks 不能为负（收到 " + maxVisualTasks + "）。";
			}
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<string> <>n__0(ThingDef parentDef)
		{
			return ((CompProperties)this).ConfigErrors(parentDef);
		}
	}
	public class CompDigitalWorker : ThingComp
	{
		private class ActiveWork
		{
			public DigitalTask task;

			public Map map;

			public Mote_DS_WorkHand hand;

			public Effecter bar;

			public Effecter hitFx;

			public int lastStrikes;
		}

		private Pawn worker;

		private readonly List<ActiveWork> works = new List<ActiveWork>();

		private int nextScanTick;

		private bool enabled = true;

		private const int MaxIterate = 4000;

		private const int MaxEnumerationsPerScan = 6000;

		private int scanBudget;

		private int scanSerial;

		private readonly Dictionary<WorkGiver, List<Thing>> candidateCache = new Dictionary<WorkGiver, List<Thing>>();

		private readonly Dictionary<WorkGiver, int> candidateSerial = new Dictionary<WorkGiver, int>();

		private readonly Dictionary<WorkGiver, List<IntVec3>> cellCache = new Dictionary<WorkGiver, List<IntVec3>>();

		private readonly Dictionary<WorkGiver, int> cellCacheTick = new Dictionary<WorkGiver, int>();

		private const int CellCacheRefreshTicks = 240;

		private const int MaxCellCandidates = 4000;

		private const int AddsPerScan = 60;

		private const int FillScanIntervalTicks = 15;

		public CompProperties_DigitalWorker Props => (CompProperties_DigitalWorker)(object)base.props;

		public int ActiveCount => works.Count;

		public DigitalTask CurrentTask
		{
			get
			{
				if (works.Count <= 0)
				{
					return null;
				}
				return works[0].task;
			}
		}

		public bool Enabled
		{
			get
			{
				return enabled;
			}
			set
			{
				enabled = value;
				if (!enabled)
				{
					ReleaseAll();
				}
			}
		}

		public bool Powered
		{
			get
			{
				CompPowerTrader comp = base.parent.GetComp<CompPowerTrader>();
				if (comp != null)
				{
					return comp.PowerOn;
				}
				return true;
			}
		}

		public bool CanWork
		{
			get
			{
				if (((Thing)base.parent).Spawned && !((Thing)base.parent).Destroyed && Powered)
				{
					return enabled;
				}
				return false;
			}
		}

		public Pawn Worker
		{
			get
			{
				EnsureWorker();
				return worker;
			}
		}

		public Pawn WorkerIfCreated => worker;

		public override void CompTick()
		{
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			if (!CanWork)
			{
				ReleaseAll();
				return;
			}
			Map map = ((Thing)base.parent).Map;
			if (map == null)
			{
				return;
			}
			Pawn val = Worker;
			if (val == null)
			{
				return;
			}
			SliceCandidates(map, val);
			long heapSince = DevDrawProfiler.HeapStamp();
			DigitalWorkerScope.Enter(val, map, ((Thing)base.parent).PositionHeld);
			DigitalDropRedirect.Begin(map, ((Thing)base.parent).PositionHeld);
			try
			{
				long since = DevDrawProfiler.Stamp();
				for (int num = works.Count - 1; num >= 0; num--)
				{
					if (!works[num].task.StillValid(val, map))
					{
						ReleaseWorkAt(num);
					}
				}
				DevDrawProfiler.Mark("DS-valid", since);
				since = DevDrawProfiler.Stamp();
				int ticksGame = Find.TickManager.TicksGame;
				if (ticksGame >= nextScanTick)
				{
					DevDrawProfiler.Bump("扫描", 1);
					int num2 = Math.Max(1, Props.scanIntervalTicks);
					int num3 = TotalParallelCap();
					if (num3 > 0 && works.Count < num3)
					{
						num2 = Math.Min(num2, 15);
					}
					nextScanTick = ticksGame + num2;
					scanBudget = 6000;
					scanSerial++;
					for (int i = 0; i < 60; i++)
					{
						if (scanBudget <= 0)
						{
							break;
						}
						if (!TryAddOneWork(map, val))
						{
							break;
						}
					}
					DevDrawProfiler.Bump("枚举", 6000 - scanBudget);
				}
				DevDrawProfiler.Mark("DS-scan", since);
				since = DevDrawProfiler.Stamp();
				for (int j = 0; j < works.Count; j++)
				{
					try
					{
						works[j].task.Work(val, map, Props.workSpeedMult);
					}
					catch (Exception ex)
					{
						Log.Error("[DigitalStorage] 代理建筑干活出错（丢这一件）：" + ((object)base.parent)?.ToString() + " → " + ex);
						ReleaseWorkAt(j);
						j--;
					}
				}
				DevDrawProfiler.Mark("DS-work", since);
				since = DevDrawProfiler.Stamp();
				for (int num4 = works.Count - 1; num4 >= 0; num4--)
				{
					if (works[num4].task.Finished)
					{
						DevDrawProfiler.Bump("完成", 1);
						ReleaseWorkAt(num4);
					}
				}
				DevDrawProfiler.Mark("DS-finish", since);
			}
			finally
			{
				DigitalDropRedirect.End();
				DigitalWorkerScope.Exit(val);
			}
			long since2 = DevDrawProfiler.Stamp();
			UpdateVisuals(map);
			DevDrawProfiler.Mark("DS-visual", since2);
			DevDrawProfiler.MarkAlloc("DS-allocMB", heapSince);
		}

		private bool TryAddOneWork(Map map, Pawn w)
		{
			List<WorkTypeDef> workTypes = Props.workTypes;
			if (workTypes == null)
			{
				return false;
			}
			if (Props.maxParallelTotal > 0 && works.Count >= Props.maxParallelTotal)
			{
				return false;
			}
			int num = Math.Max(1, Props.maxParallelPerWorkType);
			for (int i = 0; i < workTypes.Count; i++)
			{
				WorkTypeDef val = workTypes[i];
				if (val != null)
				{
					if (scanBudget <= 0)
					{
						return false;
					}
					if (CountWorksOf(val) < num && TryScanWorkType(map, w, val))
					{
						return true;
					}
				}
			}
			return false;
		}

		private int CountWorksOf(WorkTypeDef wt)
		{
			int num = 0;
			for (int i = 0; i < works.Count; i++)
			{
				if (works[i].task.workType == wt)
				{
					num++;
				}
			}
			return num;
		}

		private void SliceCandidates(Map map, Pawn w)
		{
			List<WorkTypeDef> workTypes = Props.workTypes;
			if (workTypes == null)
			{
				return;
			}
			for (int i = 0; i < workTypes.Count; i++)
			{
				WorkTypeDef val = workTypes[i];
				if (val == null)
				{
					continue;
				}
				List<WorkGiver> list = DigitalTaskRegistry.FindGivers(val);
				if (list == null)
				{
					continue;
				}
				for (int j = 0; j < list.Count; j++)
				{
					WorkGiver obj = list[j];
					WorkGiver_Scanner val2 = (WorkGiver_Scanner)(object)((obj is WorkGiver_Scanner) ? obj : null);
					if (val2 != null)
					{
						DigitalTaskRegistry.AdapterFor(list[j])?.SliceTick(map, w, val2);
					}
				}
			}
		}

		private int TotalParallelCap()
		{
			List<WorkTypeDef> workTypes = Props.workTypes;
			int num = Math.Max(1, Props.maxParallelPerWorkType) * ((workTypes == null || workTypes.Count <= 0) ? 1 : workTypes.Count);
			if (Props.maxParallelTotal > 0 && Props.maxParallelTotal < num)
			{
				num = Props.maxParallelTotal;
			}
			return num;
		}

		private bool TryScanWorkType(Map map, Pawn w, WorkTypeDef workType)
		{
			List<WorkGiver> list = DigitalTaskRegistry.FindGivers(workType);
			if (list == null)
			{
				return false;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (scanBudget <= 0)
				{
					return false;
				}
				if (TryScanGiver(map, w, workType, list[i]))
				{
					return true;
				}
			}
			return false;
		}

		private bool TryScanGiver(Map map, Pawn w, WorkTypeDef workType, WorkGiver giver)
		{
			WorkGiver_Scanner val = (WorkGiver_Scanner)(object)((giver is WorkGiver_Scanner) ? giver : null);
			if (val == null)
			{
				return false;
			}
			DigitalTaskAdapter digitalTaskAdapter = DigitalTaskRegistry.AdapterFor(giver);
			if (digitalTaskAdapter == null)
			{
				return false;
			}
			if (!DigitalTaskRegistry.PawnCanUse(giver, w, digitalTaskAdapter))
			{
				return false;
			}
			if (digitalTaskAdapter.CellBased)
			{
				return TryScanGiverCells(map, w, workType, val, digitalTaskAdapter);
			}
			List<Thing> candidates = GetCandidates(map, w, val, digitalTaskAdapter);
			if (candidates == null || candidates.Count == 0)
			{
				return false;
			}
			int num = 0;
			foreach (Thing item in candidates)
			{
				if (--scanBudget < 0 || ++num > 4000)
				{
					break;
				}
				if (item == null || item.Destroyed || !item.Spawned || ForbidUtility.IsForbidden(item, w) || DigitalWorkerClaims.OwnerOf(map, item) != null || !digitalTaskAdapter.CanTarget(w, item))
				{
					continue;
				}
				if (digitalTaskAdapter.TrustWorkGiver)
				{
					bool flag;
					try
					{
						DevDrawProfiler.Bump("试岗", 1);
						flag = val.HasJobOnThing(w, item, false);
					}
					catch (Exception ex)
					{
						Log.ErrorOnce("[DigitalStorage] 问 WorkGiver 时出错：" + ((Def)giver.def).defName + " → " + ex, ((Def)giver.def).shortHash * 31 + 9923);
						flag = false;
					}
					if (!flag)
					{
						continue;
					}
				}
				DigitalTask digitalTask = digitalTaskAdapter.MakeTask(item, this);
				digitalTask.workType = workType;
				if (digitalTask.StillValid(w, map))
				{
					digitalTask.Claim(map, this);
					works.Add(new ActiveWork
					{
						task = digitalTask,
						map = map
					});
					return true;
				}
			}
			return false;
		}

		private bool TryScanGiverCells(Map map, Pawn w, WorkTypeDef workType, WorkGiver_Scanner scanner, DigitalTaskAdapter adapter)
		{
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			List<IntVec3> candidateCells = GetCandidateCells(map, w, scanner, adapter);
			if (candidateCells == null || candidateCells.Count == 0)
			{
				return false;
			}
			int num = 0;
			for (int i = 0; i < candidateCells.Count; i++)
			{
				if (--scanBudget < 0)
				{
					break;
				}
				if (++num > 4000)
				{
					break;
				}
				IntVec3 val = candidateCells[i];
				if (!((IntVec3)(ref val)).IsValid || !GenGrid.InBounds(val, map) || DigitalWorkerClaims.CellOwnerOf(map, val) != null)
				{
					continue;
				}
				DigitalTask digitalTask = adapter.MakeCellTask(val, map, w, scanner, this);
				if (digitalTask != null)
				{
					digitalTask.workType = workType;
					if (digitalTask.StillValid(w, map))
					{
						digitalTask.Claim(map, this);
						works.Add(new ActiveWork
						{
							task = digitalTask,
							map = map
						});
						return true;
					}
				}
			}
			return false;
		}

		private List<IntVec3> GetCandidateCells(Map map, Pawn w, WorkGiver_Scanner scanner, DigitalTaskAdapter adapter)
		{
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_009b: Unknown result type (might be due to invalid IL or missing references)
			if (!cellCache.TryGetValue((WorkGiver)(object)scanner, out var value))
			{
				value = new List<IntVec3>();
				cellCache[(WorkGiver)(object)scanner] = value;
				cellCacheTick[(WorkGiver)(object)scanner] = -1;
			}
			int ticksGame = Find.TickManager.TicksGame;
			if (cellCacheTick.TryGetValue((WorkGiver)(object)scanner, out var value2) && value2 >= 0 && ticksGame - value2 < 240)
			{
				return value;
			}
			value.Clear();
			long since = DevDrawProfiler.Stamp();
			IEnumerable<IntVec3> enumerable = adapter.CandidateCells(map, w, scanner);
			if (enumerable != null)
			{
				foreach (IntVec3 item in enumerable)
				{
					if (value.Count >= 4000)
					{
						break;
					}
					value.Add(item);
				}
			}
			DevDrawProfiler.Mark("ScanSet", since);
			DevDrawProfiler.Bump("物化格", 1);
			cellCacheTick[(WorkGiver)(object)scanner] = ticksGame;
			return value;
		}

		private List<Thing> GetCandidates(Map map, Pawn w, WorkGiver_Scanner scanner, DigitalTaskAdapter adapter)
		{
			if (!candidateCache.TryGetValue((WorkGiver)(object)scanner, out var value))
			{
				value = new List<Thing>();
				candidateCache[(WorkGiver)(object)scanner] = value;
				candidateSerial[(WorkGiver)(object)scanner] = int.MinValue;
			}
			if (candidateSerial[(WorkGiver)(object)scanner] == scanSerial)
			{
				return value;
			}
			value.Clear();
			long since = DevDrawProfiler.Stamp();
			IEnumerable<Thing> enumerable = adapter.CandidateSet(map, w, scanner);
			if (enumerable != null)
			{
				foreach (Thing item in enumerable)
				{
					if (item != null)
					{
						value.Add(item);
					}
				}
			}
			DevDrawProfiler.Mark("ScanSet", since);
			DevDrawProfiler.Bump("物化", 1);
			candidateSerial[(WorkGiver)(object)scanner] = scanSerial;
			return value;
		}

		private void ReleaseWorkAt(int i)
		{
			ActiveWork activeWork = works[i];
			works.RemoveAt(i);
			activeWork.task.Release(activeWork.map, this);
			activeWork.task.Cleanup();
			CleanupVisual(activeWork);
		}

		public void ReleaseAll()
		{
			for (int num = works.Count - 1; num >= 0; num--)
			{
				ReleaseWorkAt(num);
			}
			DigitalWorkerClaims.ReleaseAll(this);
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = (DestroyMode)0)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostDeSpawn(map, mode);
			ReleaseAll();
			cellCache.Clear();
			cellCacheTick.Clear();
		}

		public override void PostExposeData()
		{
			((ThingComp)this).PostExposeData();
			Scribe_Values.Look<bool>(ref enabled, "enabled", true, false);
		}

		public override string CompInspectStringExtra()
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_010b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			if (!((Thing)base.parent).Spawned)
			{
				return null;
			}
			if (!Powered)
			{
				return ((object)Translator.Translate("DS_DW_InspectOff")/*cast due to constrained. prefix*/).ToString();
			}
			if (!enabled)
			{
				return ((object)Translator.Translate("DS_DW_InspectDisabled")/*cast due to constrained. prefix*/).ToString();
			}
			int num = TotalParallelCap();
			if (works.Count == 0)
			{
				if (Find.TickManager.TicksGame < nextScanTick)
				{
					return ((object)Translator.Translate("DS_DW_InspectIdle")/*cast due to constrained. prefix*/).ToString();
				}
				return ((object)Translator.Translate("DS_DW_InspectIdleNoTarget")/*cast due to constrained. prefix*/).ToString();
			}
			DigitalTask task = works[0].task;
			return ((object)TranslatorFormattedStringExtensions.Translate("DS_DW_InspectWork", NamedArgument.op_Implicit(task.Label), NamedArgument.op_Implicit(task.TargetLabel), NamedArgument.op_Implicit(Props.workSpeedMult.ToString("0.0")), NamedArgument.op_Implicit(Props.skillLevel), NamedArgument.op_Implicit(works.Count), NamedArgument.op_Implicit(num))/*cast due to constrained. prefix*/).ToString();
		}

		private void UpdateVisuals(Map map)
		{
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_0109: Unknown result type (might be due to invalid IL or missing references)
			//IL_0136: Unknown result type (might be due to invalid IL or missing references)
			//IL_0138: Unknown result type (might be due to invalid IL or missing references)
			//IL_015c: Unknown result type (might be due to invalid IL or missing references)
			//IL_015e: Unknown result type (might be due to invalid IL or missing references)
			//IL_022f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0231: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
			int num = Math.Max(0, Props.maxVisualTasks);
			for (int i = 0; i < works.Count; i++)
			{
				ActiveWork activeWork = works[i];
				if (i >= num)
				{
					CleanupVisual(activeWork);
					continue;
				}
				Thing target = activeWork.task.target;
				bool flag = target != null && !target.Destroyed && target.Spawned;
				IntVec3 val = (flag ? target.Position : activeWork.task.TargetCell);
				if (!((IntVec3)(ref val)).IsValid || !GenGrid.InBounds(val, map))
				{
					CleanupVisual(activeWork);
					continue;
				}
				Vector3 val2 = (flag ? target.DrawPos : ((IntVec3)(ref val)).ToVector3Shifted());
				TargetInfo val3 = (flag ? new TargetInfo(target) : new TargetInfo(val, map, false));
				if (activeWork.hand == null || ((Thing)activeWork.hand).Destroyed || !((Thing)activeWork.hand).Spawned)
				{
					ThingDef namedSilentFail = DefDatabase<ThingDef>.GetNamedSilentFail(activeWork.task.HandMoteDefName);
					if (namedSilentFail != null && ThingMaker.MakeThing(namedSilentFail, (ThingDef)null) is Mote_DS_WorkHand mote_DS_WorkHand)
					{
						GenSpawn.Spawn((Thing)(object)mote_DS_WorkHand, val, map, (WipeMode)0);
						activeWork.hand = mote_DS_WorkHand;
					}
				}
				if (activeWork.hand != null && !((Thing)activeWork.hand).Destroyed)
				{
					Vector3 exactPosition = val2;
					exactPosition.y = 0f;
					exactPosition.z += 0.15f;
					((Mote)activeWork.hand).exactPosition = exactPosition;
					((Mote)activeWork.hand).Maintain();
					if (activeWork.task.StrikeCount != activeWork.lastStrikes)
					{
						activeWork.lastStrikes = activeWork.task.StrikeCount;
						activeWork.hand.Strike();
						if (activeWork.task.HitEffecterDef != null)
						{
							if (activeWork.hitFx == null)
							{
								activeWork.hitFx = activeWork.task.HitEffecterDef.Spawn();
							}
							activeWork.hitFx.Trigger(val3, val3, -1);
						}
					}
				}
				float progress = activeWork.task.Progress01;
				if (progress < 0f)
				{
					if (activeWork.bar != null)
					{
						activeWork.bar.Cleanup();
						activeWork.bar = null;
					}
					continue;
				}
				if (activeWork.bar == null)
				{
					activeWork.bar = EffecterDefOf.ProgressBar.Spawn();
				}
				activeWork.bar.EffectTick(val3, TargetInfo.Invalid);
				object obj;
				if (activeWork.bar.children.Count <= 0)
				{
					obj = null;
				}
				else
				{
					SubEffecter obj2 = activeWork.bar.children[0];
					obj = ((SubEffecter_ProgressBar)(((obj2 is SubEffecter_ProgressBar) ? obj2 : null)?)).mote;
				}
				MoteProgressBar val4 = (MoteProgressBar)obj;
				if (val4 != null)
				{
					val4.progress = Mathf.Clamp01(progress);
					val4.offsetZ = -0.5f;
					val4.alwaysShow = true;
				}
			}
		}

		private void CleanupVisual(ActiveWork aw)
		{
			if (aw.hand != null && !((Thing)aw.hand).Destroyed)
			{
				((Thing)aw.hand).Destroy((DestroyMode)0);
			}
			aw.hand = null;
			if (aw.bar != null)
			{
				aw.bar.Cleanup();
				aw.bar = null;
			}
			if (aw.hitFx != null)
			{
				aw.hitFx.Cleanup();
				aw.hitFx = null;
			}
			aw.lastStrikes = 0;
		}

		private void EnsureWorker()
		{
			if (worker == null || ((Thing)worker).Destroyed)
			{
				string nameNick = ((Props.workTypes != null && Props.workTypes.Count > 0) ? ((Def)Props.workTypes[0]).defName : "?") + Props.skillLevel;
				worker = DigitalWorkerFactory.Create(Props.skillLevel, "数字工人", nameNick);
			}
		}
	}
	internal static class DigitalWorkerScope
	{
		private static readonly FieldInfo MapIndexField = AccessTools.Field(typeof(Thing), "mapIndexOrState");

		public static void Enter(Pawn pawn, Map map, IntVec3 cell)
		{
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			if (pawn != null && map != null && !(MapIndexField == null))
			{
				if (pawn.mindState != null)
				{
					pawn.mindState.Active = false;
				}
				((Thing)pawn).Position = (((IntVec3)(ref cell)).IsValid ? cell : map.Center);
				MapIndexField.SetValue(pawn, (sbyte)map.Index);
			}
		}

		public static void Exit(Pawn pawn)
		{
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			if (pawn != null && !(MapIndexField == null))
			{
				MapIndexField.SetValue(pawn, (sbyte)(-1));
				((Thing)pawn).Position = IntVec3.Invalid;
			}
		}
	}
	internal static class DigitalWorkerClaims
	{
		private static readonly Dictionary<Map, Dictionary<Thing, CompDigitalWorker>> claims = new Dictionary<Map, Dictionary<Thing, CompDigitalWorker>>();

		private static readonly Dictionary<Map, Dictionary<IntVec3, CompDigitalWorker>> cellClaims = new Dictionary<Map, Dictionary<IntVec3, CompDigitalWorker>>();

		public static bool AnyClaims
		{
			get
			{
				if (claims.Count <= 0)
				{
					return cellClaims.Count > 0;
				}
				return true;
			}
		}

		private static Dictionary<Thing, CompDigitalWorker> For(Map map)
		{
			if (!claims.TryGetValue(map, out var value))
			{
				value = new Dictionary<Thing, CompDigitalWorker>();
				claims[map] = value;
			}
			return value;
		}

		private static Dictionary<IntVec3, CompDigitalWorker> ForCells(Map map)
		{
			if (!cellClaims.TryGetValue(map, out var value))
			{
				value = new Dictionary<IntVec3, CompDigitalWorker>();
				cellClaims[map] = value;
			}
			return value;
		}

		public static CompDigitalWorker OwnerOf(Map map, Thing t)
		{
			if (map == null || t == null)
			{
				return null;
			}
			if (!claims.TryGetValue(map, out var value))
			{
				return null;
			}
			if (!value.TryGetValue(t, out var value2))
			{
				return null;
			}
			if (value2 == null || ((ThingComp)value2).parent == null || !((Thing)((ThingComp)value2).parent).Spawned)
			{
				value.Remove(t);
				return null;
			}
			return value2;
		}

		public static bool IsClaimedByOther(Map map, Thing t, CompDigitalWorker me)
		{
			CompDigitalWorker compDigitalWorker = OwnerOf(map, t);
			if (compDigitalWorker != null)
			{
				return compDigitalWorker != me;
			}
			return false;
		}

		public static bool IsClaimedByAnyone(Map map, Thing t)
		{
			return OwnerOf(map, t) != null;
		}

		public static void TryClaim(Map map, Thing t, CompDigitalWorker me)
		{
			if (map != null && t != null && me != null)
			{
				For(map)[t] = me;
			}
		}

		public static void Release(Map map, Thing t, CompDigitalWorker me)
		{
			if (map != null && t != null && claims.TryGetValue(map, out var value))
			{
				if (value.TryGetValue(t, out var value2) && value2 == me)
				{
					value.Remove(t);
				}
				if (value.Count == 0)
				{
					claims.Remove(map);
				}
			}
		}

		public static void ReleaseAll(CompDigitalWorker me)
		{
			if (me != null)
			{
				ReleaseAllIn(claims, me);
				ReleaseAllIn(cellClaims, me);
			}
		}

		public static CompDigitalWorker CellOwnerOf(Map map, IntVec3 c)
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			if (map == null || !((IntVec3)(ref c)).IsValid)
			{
				return null;
			}
			if (!cellClaims.TryGetValue(map, out var value))
			{
				return null;
			}
			if (!value.TryGetValue(c, out var value2))
			{
				return null;
			}
			if (value2 == null || ((ThingComp)value2).parent == null || !((Thing)((ThingComp)value2).parent).Spawned)
			{
				value.Remove(c);
				return null;
			}
			return value2;
		}

		public static void TryClaimCell(Map map, IntVec3 c, CompDigitalWorker me)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			if (map != null && ((IntVec3)(ref c)).IsValid && me != null)
			{
				ForCells(map)[c] = me;
			}
		}

		public static void ReleaseCell(Map map, IntVec3 c, CompDigitalWorker me)
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			if (map != null && ((IntVec3)(ref c)).IsValid && cellClaims.TryGetValue(map, out var value))
			{
				if (value.TryGetValue(c, out var value2) && value2 == me)
				{
					value.Remove(c);
				}
				if (value.Count == 0)
				{
					cellClaims.Remove(map);
				}
			}
		}

		private static void ReleaseAllIn<TKey>(Dictionary<Map, Dictionary<TKey, CompDigitalWorker>> table, CompDigitalWorker me)
		{
			if (table.Count == 0)
			{
				return;
			}
			List<Map> list = null;
			foreach (KeyValuePair<Map, Dictionary<TKey, CompDigitalWorker>> item in table)
			{
				List<TKey> list2 = null;
				foreach (KeyValuePair<TKey, CompDigitalWorker> item2 in item.Value)
				{
					if (item2.Value == me)
					{
						if (list2 == null)
						{
							list2 = new List<TKey>();
						}
						list2.Add(item2.Key);
					}
				}
				if (list2 != null)
				{
					for (int i = 0; i < list2.Count; i++)
					{
						item.Value.Remove(list2[i]);
					}
				}
				if (item.Value.Count == 0)
				{
					if (list == null)
					{
						list = new List<Map>();
					}
					list.Add(item.Key);
				}
			}
			if (list != null)
			{
				for (int j = 0; j < list.Count; j++)
				{
					table.Remove(list[j]);
				}
			}
		}
	}
	public class CompProperties_BillAutomation : CompProperties
	{
		public int skillLevel = 5;

		public float workSpeedMult = 0.8f;

		public int scanRadius = 6;

		public float basePowerWatts = 300f;

		public float benchWithoutPowerWatts = 100f;

		public int maxSlots = 64;

		public int scanIntervalTicks = 60;

		public int maxProbesPerTick = 2;

		public int noWorkRetryTicks = 30;

		public int maxVisualSlots = 6;

		public int messageIntervalTicks = 30;

		public CompProperties_BillAutomation()
		{
			base.compClass = typeof(CompBillAutomation);
		}

		public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
		{
			foreach (string item in <>n__0(parentDef))
			{
				yield return item;
			}
			if (skillLevel < 0 || skillLevel > 20)
			{
				yield return ((Def)parentDef).defName + "：skillLevel 应在 0~20（收到 " + skillLevel + "）。";
			}
			if (workSpeedMult <= 0f)
			{
				yield return ((Def)parentDef).defName + "：workSpeedMult 必须 > 0（收到 " + workSpeedMult + "）。";
			}
			if (scanRadius < 0)
			{
				yield return ((Def)parentDef).defName + "：scanRadius 不能为负（收到 " + scanRadius + "）。";
			}
			if (maxSlots < 1)
			{
				yield return ((Def)parentDef).defName + "：maxSlots 至少为 1（收到 " + maxSlots + "）。";
			}
			if (maxProbesPerTick < 1)
			{
				yield return ((Def)parentDef).defName + "：maxProbesPerTick 至少为 1（收到 " + maxProbesPerTick + "）。";
			}
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<string> <>n__0(ThingDef parentDef)
		{
			return ((CompProperties)this).ConfigErrors(parentDef);
		}
	}
	public class CompBillAutomation : ThingComp
	{
		private List<CraftPlan> plans = new List<CraftPlan>();

		private List<CraftJob> jobs = new List<CraftJob>();

		private readonly List<Thing> benches = new List<Thing>();

		private readonly HashSet<ThingDef> benchDefs = new HashSet<ThingDef>();

		private readonly List<RecipeDef> unlocked = new List<RecipeDef>();

		private Pawn worker;

		private int nextWorkerAttemptTick = -99999;

		private int nextScanTick;

		private bool enabled = true;

		private int overclockTier;

		private int craftingStorageTier = 3;

		private int parallelUnits = 8;

		private bool showRange;

		private List<IntVec3> rangeCells;

		private IntVec3 rangeCachedCenter = IntVec3.Invalid;

		private int rangeCachedRadius = -1;

		private float cachedWatts = -1f;

		private Texture2D texToggle;

		private Texture2D texOverclock;

		private Texture2D texRange;

		private Texture2D texPanel;

		private Map slotsMap;

		private readonly Dictionary<string, int> pendingStored = new Dictionary<string, int>();

		private readonly Dictionary<string, int> pendingDropped = new Dictionary<string, int>();

		private int lastMessageTick = -99999;

		private int completedCount;

		private int droppedCount;

		private readonly HashSet<Thing> usedBenches = new HashSet<Thing>();

		private readonly List<CraftPlan> demandPlans = new List<CraftPlan>();

		private const int RangeWholeMap = -1;

		private const int RangeDefault = 0;

		private int rangeOverride;

		public CompProperties_BillAutomation Props => (CompProperties_BillAutomation)(object)base.props;

		internal IList<CraftPlan> PlansForReading => plans;

		internal IList<RecipeDef> UnlockedRecipes => unlocked;

		internal IList<Thing> BenchesForReading => benches;

		public bool ShowRange => showRange;

		public int EffectiveScanRadius => Mathf.Clamp(DigitalStorageSettings.craftScanRadius, 2, 20);

		public int CompletedCount => completedCount;

		public int DroppedCount => droppedCount;

		public bool Powered
		{
			get
			{
				CompPowerTrader val = ThingCompUtility.TryGetComp<CompPowerTrader>((Thing)(object)base.parent);
				if (val != null)
				{
					return val.PowerOn;
				}
				return true;
			}
		}

		public bool CanWork
		{
			get
			{
				if (((Thing)base.parent).Spawned && !((Thing)base.parent).Destroyed && Powered)
				{
					return enabled;
				}
				return false;
			}
		}

		public bool Enabled
		{
			get
			{
				return enabled;
			}
			set
			{
				if (enabled != value)
				{
					enabled = value;
					ReleaseAll();
					RecalcWatts();
				}
			}
		}

		public int OverclockTier
		{
			get
			{
				return overclockTier;
			}
			set
			{
				int num = Mathf.Clamp(value, 0, 3);
				if (num != overclockTier)
				{
					overclockTier = num;
					RecalcWatts();
				}
			}
		}

		public float OverclockSpeedMult
		{
			get
			{
				if (overclockTier > 0)
				{
					if (overclockTier != 1)
					{
						if (overclockTier != 2)
						{
							return 9f;
						}
						return 6f;
					}
					return 3f;
				}
				return 1f;
			}
		}

		public float OverclockPowerMult
		{
			get
			{
				if (overclockTier > 0)
				{
					if (overclockTier != 1)
					{
						if (overclockTier != 2)
						{
							return 81f;
						}
						return 27f;
					}
					return 9f;
				}
				return 1f;
			}
		}

		public float CurrentWatts
		{
			get
			{
				float num = Props.basePowerWatts;
				if (enabled)
				{
					num += BenchWatts();
				}
				return num * OverclockPowerMult;
			}
		}

		public Pawn Worker
		{
			get
			{
				if (worker != null && !((Thing)worker).Destroyed)
				{
					return worker;
				}
				int ticksGame = Find.TickManager.TicksGame;
				if (ticksGame < nextWorkerAttemptTick)
				{
					return null;
				}
				nextWorkerAttemptTick = ticksGame + 250;
				worker = DigitalWorkerFactory.Create(Props.skillLevel, "制作代理", "代理");
				return worker;
			}
		}

		internal IList<CraftJob> JobsForReading
		{
			get
			{
				if (jobs == null)
				{
					jobs = new List<CraftJob>();
				}
				return jobs;
			}
		}

		public int StorageBytes => craftingStorageTier switch
		{
			0 => 1024, 
			2 => 16384, 
			3 => 65536, 
			_ => 4096, 
		};

		public int ParallelUnits => Mathf.Clamp(parallelUnits, 0, 8);

		public int ParallelPerStep => 1 + ParallelUnits;

		public int StorageUsed
		{
			get
			{
				int num = 0;
				IList<CraftJob> jobsForReading = JobsForReading;
				for (int i = 0; i < jobsForReading.Count; i++)
				{
					CraftJob craftJob = jobsForReading[i];
					if (craftJob != null && craftJob.Active)
					{
						num += PlanBytesOf(craftJob);
					}
				}
				return num;
			}
		}

		internal int ScanRadius
		{
			get
			{
				//IL_002a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0035: Unknown result type (might be due to invalid IL or missing references)
				if (rangeOverride == -1)
				{
					Map val = ((base.parent != null) ? ((Thing)base.parent).Map : null);
					if (val == null)
					{
						return 200;
					}
					return Math.Max(val.Size.x, val.Size.z);
				}
				if (rangeOverride <= 0)
				{
					return EffectiveScanRadius;
				}
				return rangeOverride;
			}
		}

		public void ToggleShowRange()
		{
			showRange = !showRange;
		}

		public unsafe List<IntVec3> RangeCellsForDrawing()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			int scanRadius = ScanRadius;
			IntVec3 positionHeld = ((Thing)base.parent).PositionHeld;
			if (rangeCells == null || rangeCachedCenter != positionHeld || rangeCachedRadius != scanRadius)
			{
				rangeCachedCenter = positionHeld;
				rangeCachedRadius = scanRadius;
				CellRect val = CellRect.CenteredOn(positionHeld, scanRadius);
				if (rangeCells == null)
				{
					rangeCells = new List<IntVec3>(((CellRect)(ref val)).Area);
				}
				else
				{
					rangeCells.Clear();
				}
				Enumerator enumerator = ((CellRect)(ref val)).GetEnumerator();
				try
				{
					while (((Enumerator)(ref enumerator)).MoveNext())
					{
						IntVec3 current = ((Enumerator)(ref enumerator)).Current;
						rangeCells.Add(current);
					}
				}
				finally
				{
					((IDisposable)(*(Enumerator*)(&enumerator))/*cast due to constrained. prefix*/).Dispose();
				}
			}
			return rangeCells;
		}

		public float BenchWatts()
		{
			float num = 0f;
			for (int i = 0; i < benches.Count; i++)
			{
				Thing val = benches[i];
				if (val != null && !val.Destroyed)
				{
					CompPowerTrader val2 = ThingCompUtility.TryGetComp<CompPowerTrader>(val);
					num += ((val2 == null) ? Props.benchWithoutPowerWatts : ((CompPower)val2).Props.PowerConsumption);
				}
			}
			return num;
		}

		public override void CompTick()
		{
			if (!CanWork)
			{
				ReleaseAll();
				return;
			}
			Map map = ((Thing)base.parent).Map;
			if (map == null)
			{
				return;
			}
			int ticksGame = Find.TickManager.TicksGame;
			if (ticksGame >= nextScanTick)
			{
				nextScanTick = ticksGame + Math.Max(1, Props.scanIntervalTicks);
				RescanBenches(map);
				SyncLines(map);
				RecalcWatts();
			}
			if (cachedWatts < 0f)
			{
				RecalcWatts();
			}
			ReassertPower();
			FlushMessages(ticksGame);
			if (plans.Count != 0)
			{
				Pawn val = Worker;
				if (val != null)
				{
					AdvanceAll(val, map, ticksGame);
					ProbeAll(map, val, ticksGame);
					UpdateVisuals(map);
				}
			}
		}

		private void MergePlayerCraftableRecipes()
		{
			try
			{
				List<RecipeDef> allDefsListForReading = DefDatabase<RecipeDef>.AllDefsListForReading;
				for (int i = 0; i < allDefsListForReading.Count; i++)
				{
					RecipeDef val = allDefsListForReading[i];
					if (val == null || val.ProducedThingDef == null || (val.researchPrerequisite != null && !val.researchPrerequisite.IsFinished) || val.recipeUsers == null || val.recipeUsers.Count == 0)
					{
						continue;
					}
					bool flag = false;
					for (int j = 0; j < val.recipeUsers.Count; j++)
					{
						if (flag)
						{
							break;
						}
						ThingDef val2 = val.recipeUsers[j];
						if (val2 == null)
						{
							continue;
						}
						for (int k = 0; k < benchDefs.Count; k++)
						{
							if (benchDefs.Contains(val2))
							{
								flag = true;
								break;
							}
						}
					}
					if (flag && !unlocked.Contains(val))
					{
						unlocked.Add(val);
					}
				}
			}
			catch (Exception ex)
			{
				Log.WarningOnce("[DigitalStorage] 并入全部可用配方时出错（已忽略）：" + ex.Message, 889912);
			}
		}

		private void RescanBenches(Map map)
		{
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			if (slotsMap != null && slotsMap != map)
			{
				ReleaseAll();
			}
			slotsMap = map;
			benches.Clear();
			benchDefs.Clear();
			int scanRadius = ScanRadius;
			CellRect rect = CellRect.CenteredOn(((Thing)base.parent).PositionHeld, scanRadius);
			List<Thing> list = map.listerThings.ThingsInGroup((ThingRequestGroup)13);
			for (int i = 0; i < list.Count; i++)
			{
				Thing val = list[i];
				if (val != null && !val.Destroyed && val.Spawned && (object)val != base.parent && val is Building && val is IBillGiver && InScanRange(val, rect))
				{
					List<RecipeDef> allRecipes = val.def.AllRecipes;
					if (allRecipes != null && allRecipes.Count != 0)
					{
						benches.Add(val);
						benchDefs.Add(val.def);
					}
				}
			}
			CraftUnlocks.MenuRecipes(benchDefs, unlocked);
			MergePlayerCraftableRecipes();
		}

		internal static bool InScanRange(Thing t, CellRect rect)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			if (t == null)
			{
				return false;
			}
			if (((CellRect)(ref rect)).Overlaps(GenAdj.OccupiedRect(t)))
			{
				return true;
			}
			if (t.def != null && t.def.hasInteractionCell)
			{
				return ((CellRect)(ref rect)).Contains(t.InteractionCell);
			}
			return false;
		}

		private void SyncLines(Map map)
		{
			int ticksGame = Find.TickManager.TicksGame;
			RefreshJobStates(map, ticksGame);
			RefreshUpstreamGating();
			bool flag = TotalLines() >= Math.Max(1, Props.maxSlots);
			for (int num = plans.Count - 1; num >= 0; num--)
			{
				CraftPlan craftPlan = plans[num];
				for (int num2 = craftPlan.lines.Count - 1; num2 >= 0; num2--)
				{
					CraftLine craftLine = craftPlan.lines[num2];
					Thing bench = craftLine.Bench;
					if (!craftLine.HasWork && craftLine.Bill != null)
					{
						if (IsStarvedForMaterials(craftLine))
						{
							if (craftLine.BlockedSinceTick <= 0)
							{
								craftLine.BlockedSinceTick = ticksGame;
							}
						}
						else
						{
							craftLine.BlockedSinceTick = 0;
						}
						bool num3 = !craftPlan.CanStartNewWork;
						bool flag2 = flag && craftLine.BlockedSinceTick > 0 && ticksGame - craftLine.BlockedSinceTick >= 150;
						if (num3 || flag2)
						{
							ReleaseLine(craftPlan, num2);
							continue;
						}
					}
					else if (craftLine.BlockedSinceTick != 0)
					{
						craftLine.BlockedSinceTick = 0;
					}
					if (!craftPlan.Maintained || bench == null || bench.Destroyed || !bench.Spawned || !BenchUsable(bench) || !CanCraft(bench, craftPlan.recipe))
					{
						ReleaseLine(craftPlan, num2);
					}
				}
			}
			usedBenches.Clear();
			for (int i = 0; i < plans.Count; i++)
			{
				CraftPlan craftPlan2 = plans[i];
				for (int j = 0; j < craftPlan2.lines.Count; j++)
				{
					if (craftPlan2.lines[j].Bench != null)
					{
						usedBenches.Add(craftPlan2.lines[j].Bench);
					}
				}
			}
			List<CraftPlan> list = JobOrderedPlans();
			int num4 = Math.Max(1, Props.maxSlots) - TotalLines();
			for (int k = 0; k < list.Count; k++)
			{
				if (num4 <= 0)
				{
					break;
				}
				CraftPlan craftPlan3 = list[k];
				if (!craftPlan3.Maintained)
				{
					continue;
				}
				RefreshTargetStateIfStale(craftPlan3, map);
				if (!craftPlan3.CanStartNewWork)
				{
					continue;
				}
				int num5 = Mathf.Min(FreeBenchCountFor(craftPlan3.recipe), Mathf.Min(Mathf.Max(1, ParallelPerStep), DemandLines(craftPlan3)));
				for (int l = craftPlan3.lines.Count; l < num5; l++)
				{
					if (num4 <= 0)
					{
						break;
					}
					if (craftPlan3.lines.Count >= DemandLines(craftPlan3))
					{
						continue;
					}
					Thing val = PickBench(craftPlan3.recipe);
					if (val != null)
					{
						if (!BillBenchClaims.TryClaim(map, val, this))
						{
							usedBenches.Add(val);
							continue;
						}
						usedBenches.Add(val);
						craftPlan3.lines.Add(MakeLine(craftPlan3, val));
						num4--;
					}
				}
			}
		}

		private static int DemandLines(CraftPlan plan)
		{
			if (plan.mode == 0)
			{
				return int.MaxValue;
			}
			if (plan.mode == 1)
			{
				return Math.Max(1, plan.remaining);
			}
			return Math.Max(1, plan.targetCount - plan.countedCount);
		}

		private CraftLine MakeLine(CraftPlan plan, Thing bench)
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Expected O, but got Unknown
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Expected O, but got Unknown
			CraftLine obj = new CraftLine
			{
				Plan = plan,
				Bench = bench
			};
			Bill_Production val = new Bill_Production(plan.recipe, (Precept_ThingStyle)null);
			val.repeatMode = BillRepeatModeDefOf.Forever;
			BillStack val2 = new BillStack((IBillGiver)(object)((bench is IBillGiver) ? bench : null));
			val2.AddBill((Bill)(object)val);
			obj.Bill = val;
			obj.TempStack = val2;
			return obj;
		}

		private void ReleaseLine(CraftPlan plan, int i)
		{
			CraftLine craftLine = plan.lines[i];
			if (slotsMap != null)
			{
				BillBenchClaims.Release(slotsMap, craftLine.Bench, this);
			}
			craftLine.ClearAll();
			plan.lines.RemoveAt(i);
		}

		public void ReleaseAll()
		{
			for (int i = 0; i < plans.Count; i++)
			{
				CraftPlan craftPlan = plans[i];
				for (int num = craftPlan.lines.Count - 1; num >= 0; num--)
				{
					craftPlan.lines[num].ClearAll();
				}
				craftPlan.lines.Clear();
			}
			if (slotsMap != null)
			{
				BillBenchClaims.ReleaseAll(slotsMap, this);
			}
			slotsMap = null;
			benches.Clear();
			usedBenches.Clear();
			demandPlans.Clear();
			pendingStored.Clear();
			pendingDropped.Clear();
		}

		private int TotalLines()
		{
			int num = 0;
			for (int i = 0; i < plans.Count; i++)
			{
				num += plans[i].lines.Count;
			}
			return num;
		}

		internal static bool BenchUsable(Thing bench)
		{
			IBillGiver val = (IBillGiver)(object)((bench is IBillGiver) ? bench : null);
			if (val == null)
			{
				return false;
			}
			if (!bench.Spawned || bench.Destroyed)
			{
				return false;
			}
			if (val.CurrentlyUsableForBills())
			{
				return !FireUtility.IsBurning(bench);
			}
			return false;
		}

		internal static bool CanCraft(Thing bench, RecipeDef recipe)
		{
			if (bench == null || recipe == null || bench.def == null)
			{
				return false;
			}
			List<RecipeDef> allRecipes = bench.def.AllRecipes;
			if (allRecipes == null)
			{
				return false;
			}
			for (int i = 0; i < allRecipes.Count; i++)
			{
				if (allRecipes[i] == recipe)
				{
					return true;
				}
			}
			return false;
		}

		internal int BenchTotalFor(RecipeDef recipe)
		{
			int num = 0;
			for (int i = 0; i < benches.Count; i++)
			{
				if (CanCraft(benches[i], recipe))
				{
					num++;
				}
			}
			return num;
		}

		internal int UsableBenchCountFor(RecipeDef recipe)
		{
			int num = 0;
			for (int i = 0; i < benches.Count; i++)
			{
				if (CanCraft(benches[i], recipe) && BenchUsable(benches[i]))
				{
					num++;
				}
			}
			return num;
		}

		private int FreeBenchCountFor(RecipeDef recipe)
		{
			int num = 0;
			for (int i = 0; i < benches.Count; i++)
			{
				Thing val = benches[i];
				if (CanCraft(val, recipe) && BenchUsable(val) && !usedBenches.Contains(val))
				{
					num++;
				}
			}
			return num;
		}

		private Thing PickBench(RecipeDef recipe)
		{
			for (int i = 0; i < benches.Count; i++)
			{
				Thing val = benches[i];
				if (CanCraft(val, recipe) && BenchUsable(val) && !usedBenches.Contains(val))
				{
					return val;
				}
			}
			return null;
		}

		private void AdvanceAll(Pawn w, Map map, int now)
		{
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			float num = Math.Max(0.01f, Props.workSpeedMult) * OverclockSpeedMult;
			DigitalWorkerScope.Enter(w, map, ((Thing)base.parent).PositionHeld);
			try
			{
				for (int i = 0; i < plans.Count; i++)
				{
					CraftPlan craftPlan = plans[i];
					for (int num2 = craftPlan.lines.Count - 1; num2 >= 0; num2--)
					{
						CraftLine craftLine = craftPlan.lines[num2];
						if (!craftLine.HasWork)
						{
							continue;
						}
						if (!StillValid(craftPlan, craftLine, map))
						{
							craftLine.ClearWork();
							craftLine.NextAcquireTick = 0;
							craftLine.BlockKey = null;
							continue;
						}
						craftLine.WorkLeft -= Math.Max(0f, craftLine.BaseRate) * num;
						try
						{
							((Bill)craftLine.Bill).Notify_PawnDidWork(w);
						}
						catch (Exception)
						{
						}
						if (craftLine.WorkLeft > 0f)
						{
							continue;
						}
						CraftResult craftResult;
						try
						{
							craftResult = BillCraftFunnel.TryComplete(this, craftPlan, craftLine, map, w);
						}
						catch (Exception ex2)
						{
							Log.ErrorOnce("[DigitalStorage] 制作代理结算异常（丢弃这一轮，料未扣）：" + ex2, 882244);
							craftLine.ClearWork();
							craftLine.NextAcquireTick = now + 300;
							continue;
						}
						if (craftResult == CraftResult.NoBudget)
						{
							craftLine.WorkLeft = 0f;
							continue;
						}
						craftLine.ClearWork();
						craftLine.NextAcquireTick = 0;
						if (!craftPlan.Done)
						{
							continue;
						}
						NotifyPlanDone(craftPlan);
						break;
					}
				}
			}
			finally
			{
				DigitalWorkerScope.Exit(w);
			}
		}

		private static bool StillValid(CraftPlan plan, CraftLine line, Map map)
		{
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			if (plan == null || !plan.Maintained)
			{
				return false;
			}
			if (!BenchUsable(line.Bench))
			{
				return false;
			}
			if (map.reservationManager != null && map.reservationManager.IsReservedByAnyoneOf(LocalTargetInfo.op_Implicit(line.Bench), Faction.OfPlayer))
			{
				return false;
			}
			for (int i = 0; i < line.Ingredients.Length; i++)
			{
				Thing val = line.Ingredients[i];
				if (val == null || val.Destroyed)
				{
					return false;
				}
				if (!(val.ParentHolder is Building_StorageCore building_StorageCore) || ((Thing)building_StorageCore).Map != map)
				{
					return false;
				}
				if (line.Counts[i] > val.stackCount)
				{
					return false;
				}
			}
			return true;
		}

		private void ProbeAll(Map map, Pawn w, int now)
		{
			int num = Math.Max(1, Props.maxProbesPerTick);
			for (int i = 0; i < plans.Count; i++)
			{
				if (num <= 0)
				{
					break;
				}
				CraftPlan craftPlan = plans[i];
				if (!craftPlan.Maintained || !craftPlan.CanStartNewWork)
				{
					continue;
				}
				for (int j = 0; j < craftPlan.lines.Count; j++)
				{
					if (num <= 0)
					{
						break;
					}
					CraftLine craftLine = craftPlan.lines[j];
					if (!craftLine.HasWork && now >= craftLine.NextAcquireTick)
					{
						num--;
						try
						{
							BillProbe.TryAcquire(this, craftPlan, craftLine, map, w, now);
						}
						catch (Exception ex)
						{
							Log.ErrorOnce("[DigitalStorage] 制作代理取活异常（已跳过这条线，5 秒后重试）：" + ex, 881133);
							craftLine.ClearWork();
							craftLine.BlockKey = "DS_BA_NoBill";
							craftLine.NextAcquireTick = now + 300;
						}
					}
				}
			}
		}

		private Bill_Production CountingBill(CraftPlan plan, Map map)
		{
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Expected O, but got Unknown
			//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Expected O, but got Unknown
			if (plan.CountBill != null && !((Bill)plan.CountBill).DeletedOrDereferenced)
			{
				return plan.CountBill;
			}
			if (plan == null || plan.recipe == null || map == null)
			{
				return null;
			}
			Thing val = null;
			for (int i = 0; i < benches.Count; i++)
			{
				if (CanCraft(benches[i], plan.recipe))
				{
					val = benches[i];
					break;
				}
			}
			IBillGiver val2 = (IBillGiver)(object)((val is IBillGiver) ? val : null);
			if (val2 == null)
			{
				return null;
			}
			Bill_Production val3 = new Bill_Production(plan.recipe, (Precept_ThingStyle)null);
			val3.repeatMode = BillRepeatModeDefOf.TargetCount;
			val3.targetCount = plan.targetCount;
			val3.pauseWhenSatisfied = plan.pauseWhenSatisfied;
			val3.unpauseWhenYouHave = plan.ResumeAt;
			BillStack val4 = new BillStack(val2);
			val4.AddBill((Bill)(object)val3);
			plan.CountBill = val3;
			plan.CountStack = val4;
			return val3;
		}

		private int CountProductsInCoreAndMap(CraftPlan plan, Map map)
		{
			if (plan == null || plan.recipe == null || plan.recipe.products == null)
			{
				return 0;
			}
			int num = 0;
			new List<ThingDef>();
			if (map != null)
			{
				List<Building_StorageCore> list = CoreFinder.AllUsableCores(map);
				if (list != null)
				{
					for (int i = 0; i < list.Count; i++)
					{
						Building_StorageCore building_StorageCore = list[i];
						if (building_StorageCore == null)
						{
							continue;
						}
						ThingOwner directlyHeldThings = building_StorageCore.GetDirectlyHeldThings();
						if (directlyHeldThings == null)
						{
							continue;
						}
						for (int j = 0; j < plan.recipe.products.Count; j++)
						{
							ThingDefCountClass val = plan.recipe.products[j];
							if (val == null || val.thingDef == null)
							{
								continue;
							}
							for (int k = 0; k < directlyHeldThings.Count; k++)
							{
								Thing val2 = directlyHeldThings[k];
								if (val2 != null && val2.def == val.thingDef)
								{
									num += val2.stackCount;
								}
							}
						}
					}
				}
			}
			if (map != null && map.listerThings != null)
			{
				for (int l = 0; l < plan.recipe.products.Count; l++)
				{
					ThingDefCountClass val3 = plan.recipe.products[l];
					if (val3 == null || val3.thingDef == null)
					{
						continue;
					}
					List<Thing> list2 = map.listerThings.ThingsOfDef(val3.thingDef);
					if (list2 == null)
					{
						continue;
					}
					for (int m = 0; m < list2.Count; m++)
					{
						Thing val4 = list2[m];
						if (val4 != null && val4.Spawned)
						{
							num += val4.stackCount;
						}
					}
				}
			}
			return num;
		}

		internal void RecountNow(CraftPlan plan)
		{
			if (plan != null && plan.mode == 2)
			{
				Map val = ((base.parent != null && ((Thing)base.parent).Spawned) ? ((Thing)base.parent).Map : null);
				if (val != null)
				{
					plan.countedCount = Math.Max(0, CountProductsInCoreAndMap(plan, val));
					plan.countedTick = Find.TickManager.TicksGame;
					plan.wantsWork = plan.UpdateTargetState(plan.countedCount);
				}
			}
		}

		internal void RefreshTargetStateIfStale(CraftPlan plan, Map map)
		{
			if (plan == null)
			{
				return;
			}
			if (plan.mode != 2)
			{
				plan.wantsWork = true;
				return;
			}
			int ticksGame = Find.TickManager.TicksGame;
			if (plan.countedTick <= 0 || ticksGame - plan.countedTick >= 30)
			{
				Bill_Production val = CountingBill(plan, map);
				if (val == null || ((Bill)val).Map == null)
				{
					plan.CountBill = null;
					plan.CountStack = null;
					plan.countedCount = 0;
					plan.countedTick = ticksGame;
					plan.wantsWork = true;
				}
				else
				{
					val.targetCount = plan.targetCount;
					val.pauseWhenSatisfied = plan.pauseWhenSatisfied;
					val.unpauseWhenYouHave = plan.ResumeAt;
					int val2 = CountProductsInCoreAndMap(plan, map);
					plan.countedCount = Math.Max(0, val2);
					plan.countedTick = ticksGame;
					plan.wantsWork = plan.UpdateTargetState(plan.countedCount);
				}
			}
		}

		internal bool CanTargetMode(CraftPlan plan)
		{
			if (plan == null || plan.recipe == null)
			{
				return false;
			}
			Bill_Production val = ((plan.CountBill != null && !((Bill)plan.CountBill).DeletedOrDereferenced) ? plan.CountBill : CountingBill(plan, ((Thing)base.parent).Map));
			if (val == null)
			{
				return true;
			}
			try
			{
				return plan.recipe.WorkerCounter.CanCountProducts(val);
			}
			catch (Exception)
			{
				return false;
			}
		}

		internal void CycleMode(CraftPlan plan)
		{
			if (plan == null)
			{
				return;
			}
			if (plan.mode == 0)
			{
				plan.mode = 1;
				plan.remaining = 0;
				plan.AddCount(1);
			}
			else if (plan.mode == 1)
			{
				if (CanTargetMode(plan))
				{
					plan.SetTarget((plan.targetCount > 0) ? plan.targetCount : 10);
				}
				else
				{
					plan.SetForever();
				}
			}
			else
			{
				plan.SetForever();
			}
		}

		internal bool IsProductNeededByActiveJob(ThingDef def)
		{
			if (def == null)
			{
				return false;
			}
			IList<CraftJob> list = jobs;
			if (list == null || list.Count == 0)
			{
				return false;
			}
			for (int i = 0; i < list.Count; i++)
			{
				CraftJob craftJob = list[i];
				if (craftJob == null || craftJob.state != 0)
				{
					continue;
				}
				if (craftJob.rootProduct == def)
				{
					return true;
				}
				List<CraftJobStep> steps = craftJob.steps;
				if (steps == null)
				{
					continue;
				}
				for (int j = 0; j < steps.Count; j++)
				{
					CraftJobStep craftJobStep = steps[j];
					if (craftJobStep != null && craftJobStep.product == def)
					{
						return true;
					}
				}
			}
			return false;
		}

		public string StorageTierLabel()
		{
			return craftingStorageTier switch
			{
				0 => "1k", 
				2 => "16k", 
				3 => "64k", 
				_ => "4k", 
			};
		}

		public void CycleStorageTier()
		{
			craftingStorageTier = (craftingStorageTier + 1) % 4;
		}

		public void CycleParallelUnits()
		{
			parallelUnits = (ParallelUnits + 1) % 9;
		}

		internal static int PlanBytesOf(CraftJob job)
		{
			if (job == null || job.steps == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < job.steps.Count; i++)
			{
				CraftJobStep craftJobStep = job.steps[i];
				if (craftJobStep != null)
				{
					num += Mathf.Max(0, craftJobStep.needCount);
				}
			}
			return num;
		}

		internal CraftJob JobForRecipe(RecipeDef recipe)
		{
			if (recipe == null)
			{
				return null;
			}
			for (int i = 0; i < jobs.Count; i++)
			{
				CraftJob craftJob = jobs[i];
				if (craftJob != null && craftJob.Active && craftJob.IndexOfRecipe(recipe) >= 0)
				{
					return craftJob;
				}
			}
			return null;
		}

		internal CraftJob SubmitJob(RecipeDef recipe, ThingDef product, int wanted, bool withIntermediates)
		{
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			if (recipe == null)
			{
				return null;
			}
			int num = Mathf.Max(1, wanted);
			if (product == null)
			{
				product = recipe.ProducedThingDef;
			}
			int num2 = PlanBytesForSubmit(recipe, product, num, withIntermediates);
			if (num2 > StorageBytes)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_JOB_St_TooSmall", NamedArgument.op_Implicit(((Def)recipe).LabelCap), NamedArgument.op_Implicit(num2.ToString("N0")), NamedArgument.op_Implicit(StorageTierLabel()))), LookTargets.op_Implicit(new TargetInfo(((Thing)base.parent).PositionHeld, ((Thing)base.parent).MapHeld, false)), MessageTypeDefOf.RejectInput, false);
				return null;
			}
			CraftJob craftJob = new CraftJob();
			craftJob.rootRecipe = recipe;
			craftJob.rootProduct = product;
			craftJob.wanted = num;
			craftJob.createdTick = Find.TickManager.TicksGame;
			craftJob.state = 0;
			CraftJobStep craftJobStep = new CraftJobStep();
			craftJobStep.recipe = recipe;
			craftJobStep.product = product;
			craftJobStep.needCount = num;
			craftJobStep.crafts = Mathf.Max(1, Mathf.CeilToInt((float)num / (float)Mathf.Max(1, CraftTree.YieldOf(recipe, product))));
			craftJobStep.depth = 0;
			craftJobStep.parent = -1;
			craftJob.steps.Add(craftJobStep);
			DeriveJobSteps(craftJob, craftJobStep, withIntermediates);
			JobsForReading.Add(craftJob);
			return craftJob;
		}

		private void DeriveJobSteps(CraftJob job, CraftJobStep root, bool withIntermediates)
		{
			if (job == null || root == null)
			{
				return;
			}
			HashSet<RecipeDef> hashSet = new HashSet<RecipeDef>();
			if (job.steps != null)
			{
				for (int i = 0; i < job.steps.Count; i++)
				{
					CraftJobStep craftJobStep = job.steps[i];
					if (craftJobStep != null && craftJobStep.recipe != null)
					{
						hashSet.Add(craftJobStep.recipe);
					}
				}
			}
			if (job.steps != null && job.steps.Count > 1)
			{
				job.steps.RemoveRange(1, job.steps.Count - 1);
			}
			List<CraftJobStep> list = new List<CraftJobStep>();
			if (withIntermediates && root.product != null)
			{
				CraftTreeNode node = CraftTree.Build(this, root.recipe, root.product, root.needCount);
				CollectJobSteps(job, 0, node, list);
				list.Sort((CraftJobStep a, CraftJobStep b) => b.depth - a.depth);
			}
			CraftPlan craftPlan = CraftTree.FindPlan(this, root.recipe);
			bool rootIsTarget = craftPlan == null || craftPlan.mode == 2;
			for (int num = 0; num < list.Count; num++)
			{
				EnsureStepPlan(list[num], isRoot: false, overwrite: true, rootIsTarget);
			}
			EnsureStepPlan(root, isRoot: true, overwrite: true, rootIsTarget);
			HashSet<RecipeDef> hashSet2 = new HashSet<RecipeDef>();
			for (int num2 = 0; num2 < job.steps.Count; num2++)
			{
				CraftJobStep craftJobStep2 = job.steps[num2];
				if (craftJobStep2 != null && craftJobStep2.recipe != null)
				{
					hashSet2.Add(craftJobStep2.recipe);
				}
			}
			foreach (RecipeDef item in hashSet)
			{
				if (item == null || hashSet2.Contains(item) || AnyJobHasRecipe(item))
				{
					continue;
				}
				for (int num3 = plans.Count - 1; num3 >= 0; num3--)
				{
					CraftPlan craftPlan2 = plans[num3];
					if (craftPlan2 != null && craftPlan2.recipe == item)
					{
						for (int num4 = craftPlan2.lines.Count - 1; num4 >= 0; num4--)
						{
							ReleaseLine(craftPlan2, num4);
						}
						plans.RemoveAt(num3);
					}
				}
			}
		}

		internal void NotifyRootCountChanged(CraftPlan plan)
		{
			if (plan == null || plan.recipe == null)
			{
				return;
			}
			CraftJob craftJob = AnyJobForRecipe(plan.recipe);
			if (craftJob == null || craftJob.rootRecipe != plan.recipe)
			{
				return;
			}
			int num;
			if (plan.mode == 2)
			{
				num = Mathf.Max(1, plan.targetCount);
			}
			else
			{
				if (plan.mode != 1)
				{
					return;
				}
				num = Mathf.Max(1, plan.remaining);
			}
			craftJob.wanted = num;
			CraftJobStep craftJobStep = ((craftJob.steps != null && craftJob.steps.Count > 0) ? craftJob.steps[0] : null);
			if (craftJobStep != null)
			{
				craftJobStep.needCount = num;
				craftJobStep.crafts = Mathf.Max(1, Mathf.CeilToInt((float)num / (float)Mathf.Max(1, CraftTree.YieldOf(plan.recipe, craftJobStep.product))));
				bool withIntermediates = craftJob.steps.Count > 1;
				DeriveJobSteps(craftJob, craftJobStep, withIntermediates);
			}
		}

		private int PlanBytesForSubmit(RecipeDef recipe, ThingDef product, int want, bool withIntermediates)
		{
			if (recipe == null)
			{
				return 0;
			}
			int num = Mathf.Max(1, want);
			if (!withIntermediates || product == null)
			{
				return num;
			}
			try
			{
				List<CraftTreePlanRow> list = CraftTree.FlattenIntermediate(CraftTree.Build(this, recipe, product, Mathf.Max(1, want)));
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i] != null)
					{
						num += Mathf.Max(0, list[i].Total);
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[DigitalStorage] 估算合成规模失败（按根的量算）：" + ex, 664422);
			}
			return num;
		}

		private void CollectJobSteps(CraftJob job, int parentIndex, CraftTreeNode node, List<CraftJobStep> upstream)
		{
			if (job == null || node == null || node.Children == null)
			{
				return;
			}
			for (int i = 0; i < node.Children.Count; i++)
			{
				CraftTreeNode craftTreeNode = node.Children[i];
				if (craftTreeNode == null || craftTreeNode.Recipe == null || craftTreeNode.Product == null || craftTreeNode.CoveredByCore || craftTreeNode.NoRecipe)
				{
					continue;
				}
				int num = job.IndexOfRecipe(craftTreeNode.Recipe);
				int num2;
				if (num >= 0)
				{
					CraftJobStep craftJobStep = job.steps[num];
					if (craftTreeNode.Depth > craftJobStep.depth)
					{
						craftJobStep.depth = craftTreeNode.Depth;
					}
					num2 = num;
				}
				else
				{
					CraftJobStep craftJobStep2 = new CraftJobStep();
					craftJobStep2.recipe = craftTreeNode.Recipe;
					craftJobStep2.product = craftTreeNode.Product;
					craftJobStep2.needCount = Mathf.Max(1, craftTreeNode.RequiredTotal);
					craftJobStep2.crafts = Mathf.Max(1, craftTreeNode.Crafts);
					craftJobStep2.depth = craftTreeNode.Depth;
					craftJobStep2.parent = ((parentIndex >= 0 && parentIndex < job.steps.Count) ? parentIndex : (-1));
					job.steps.Add(craftJobStep2);
					num2 = job.steps.Count - 1;
					if (craftJobStep2.parent >= 0)
					{
						job.steps[craftJobStep2.parent].children.Add(num2);
					}
					upstream.Add(craftJobStep2);
				}
				CollectJobSteps(job, num2, craftTreeNode, upstream);
			}
		}

		private CraftPlan EnsureStepPlan(CraftJobStep step, bool isRoot, bool overwrite, bool rootIsTarget)
		{
			if (step == null || step.recipe == null)
			{
				return null;
			}
			CraftPlan craftPlan = CraftTree.FindPlan(this, step.recipe);
			bool flag = craftPlan == null;
			if (flag)
			{
				AddPlan(step.recipe);
				craftPlan = CraftTree.FindPlan(this, step.recipe);
				if (craftPlan != null)
				{
					craftPlan.FromJob = true;
				}
			}
			if (craftPlan != null && (flag || overwrite))
			{
				int num = Mathf.Max(1, step.needCount);
				if (isRoot)
				{
					if (flag)
					{
						craftPlan.SetTarget(num);
						craftPlan.pauseWhenSatisfied = true;
					}
				}
				else if (rootIsTarget)
				{
					craftPlan.SetTarget(num);
					craftPlan.pauseWhenSatisfied = true;
				}
				else
				{
					craftPlan.mode = 1;
					craftPlan.remaining = num;
					craftPlan.pauseWhenSatisfied = false;
				}
				craftPlan.paused = false;
				craftPlan.suspended = false;
			}
			return craftPlan;
		}

		internal void CancelJob(CraftJob job)
		{
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			if (job != null)
			{
				job.state = 2;
				job.finishedTick = Find.TickManager.TicksGame;
				if (jobs != null)
				{
					jobs.Remove(job);
				}
				int num = PruneOrphanPlans();
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_JOB_Cancelled", NamedArgument.op_Implicit((job.rootRecipe == null) ? "?" : ((object)((Def)job.rootRecipe).LabelCap/*cast due to constrained. prefix*/).ToString()), NamedArgument.op_Implicit(job.steps.Count), NamedArgument.op_Implicit(num))), LookTargets.op_Implicit(new TargetInfo(((Thing)base.parent).PositionHeld, ((Thing)base.parent).MapHeld, false)), MessageTypeDefOf.NeutralEvent, false);
			}
		}

		private bool AnyJobHasRecipe(RecipeDef recipe)
		{
			if (recipe == null || jobs == null)
			{
				return false;
			}
			for (int i = 0; i < jobs.Count; i++)
			{
				CraftJob craftJob = jobs[i];
				if (craftJob != null && craftJob.IndexOfRecipe(recipe) >= 0)
				{
					return true;
				}
			}
			return false;
		}

		private int PruneOrphanPlans()
		{
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			int num = 0;
			for (int num2 = plans.Count - 1; num2 >= 0; num2--)
			{
				CraftPlan craftPlan = plans[num2];
				if (craftPlan != null && craftPlan.recipe != null && craftPlan.FromJob && !AnyJobHasRecipe(craftPlan.recipe))
				{
					for (int num3 = craftPlan.lines.Count - 1; num3 >= 0; num3--)
					{
						ReleaseLine(craftPlan, num3);
					}
					plans.RemoveAt(num2);
					num++;
				}
			}
			if (num > 0)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_CA_DeletedTree", NamedArgument.op_Implicit(num))), LookTargets.op_Implicit(new TargetInfo(((Thing)base.parent).PositionHeld, ((Thing)base.parent).MapHeld, false)), MessageTypeDefOf.NeutralEvent, false);
			}
			return num;
		}

		internal CraftJob AnyJobForRecipe(RecipeDef recipe)
		{
			if (recipe == null || jobs == null)
			{
				return null;
			}
			for (int i = 0; i < jobs.Count; i++)
			{
				CraftJob craftJob = jobs[i];
				if (craftJob != null && craftJob.IndexOfRecipe(recipe) >= 0)
				{
					return craftJob;
				}
			}
			return null;
		}

		internal bool IsSubordinateStep(RecipeDef recipe)
		{
			if (recipe == null || jobs == null)
			{
				return false;
			}
			for (int i = 0; i < jobs.Count; i++)
			{
				CraftJob craftJob = jobs[i];
				if (craftJob == null || craftJob.steps.Count == 0)
				{
					continue;
				}
				for (int j = 1; j < craftJob.steps.Count; j++)
				{
					CraftJobStep craftJobStep = craftJob.steps[j];
					if (craftJobStep != null && craftJobStep.recipe == recipe)
					{
						return true;
					}
				}
			}
			return false;
		}

		private void RefreshJobStates(Map map, int now)
		{
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00df: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0120: Unknown result type (might be due to invalid IL or missing references)
			if (jobs == null)
			{
				jobs = new List<CraftJob>();
			}
			for (int num = jobs.Count - 1; num >= 0; num--)
			{
				CraftJob craftJob = jobs[num];
				if (craftJob == null || craftJob.rootRecipe == null)
				{
					jobs.RemoveAt(num);
				}
				else if (!craftJob.Active)
				{
					if (craftJob.state == 1 && craftJob.finishedTick > 0 && now - craftJob.finishedTick > 5000)
					{
						jobs.RemoveAt(num);
					}
				}
				else
				{
					CraftPlan craftPlan = CraftTree.FindPlan(this, craftJob.rootRecipe);
					if (craftPlan == null)
					{
						jobs.RemoveAt(num);
					}
					else
					{
						RefreshTargetStateIfStale(craftPlan, map);
						if (StepSatisfied(craftPlan))
						{
							bool num2 = craftPlan.mode == 1;
							Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_JOB_Done", NamedArgument.op_Implicit(((Def)craftJob.rootRecipe).LabelCap), NamedArgument.op_Implicit(craftJob.wanted), NamedArgument.op_Implicit(craftJob.steps.Count))), LookTargets.op_Implicit(new TargetInfo(((Thing)base.parent).PositionHeld, ((Thing)base.parent).MapHeld, false)), MessageTypeDefOf.TaskCompletion, false);
							craftJob.state = 1;
							craftJob.finishedTick = now;
							jobs.RemoveAt(num);
							if (num2)
							{
								PruneOrphanPlans();
							}
						}
					}
				}
			}
		}

		private void RefreshUpstreamGating()
		{
			if (plans == null || plans.Count == 0)
			{
				return;
			}
			if (jobs == null)
			{
				jobs = new List<CraftJob>();
			}
			for (int i = 0; i < plans.Count; i++)
			{
				CraftPlan craftPlan = plans[i];
				if (craftPlan == null || craftPlan.recipe == null)
				{
					continue;
				}
				CraftJob craftJob = AnyJobForRecipe(craftPlan.recipe);
				bool flag = craftJob != null && craftJob.rootRecipe == craftPlan.recipe;
				bool flag2 = craftJob != null && !flag;
				if (flag)
				{
					if (!CountSatisfied(craftPlan))
					{
						craftPlan.paused = false;
					}
					continue;
				}
				if (flag2)
				{
					CraftPlan craftPlan2 = CraftTree.FindPlan(this, craftJob.rootRecipe);
					bool flag3 = craftPlan2 != null && CountSatisfied(craftPlan2);
					if (flag3 && !craftPlan.suspended)
					{
						craftPlan.suspended = true;
					}
					else if (!flag3 && craftPlan.suspended)
					{
						craftPlan.suspended = false;
					}
					continue;
				}
				bool flag4 = false;
				foreach (RecipeDef item in CollectDownstream(craftPlan.recipe))
				{
					if (item != craftPlan.recipe)
					{
						CraftPlan craftPlan3 = CraftTree.FindPlan(this, item);
						if (craftPlan3 != null && CountSatisfied(craftPlan3))
						{
							flag4 = true;
							break;
						}
					}
				}
				if (flag4 && !craftPlan.suspended)
				{
					craftPlan.suspended = true;
				}
				else if (!flag4 && craftPlan.suspended)
				{
					craftPlan.suspended = false;
				}
			}
		}

		private HashSet<RecipeDef> CollectDownstream(RecipeDef start)
		{
			HashSet<RecipeDef> hashSet = new HashSet<RecipeDef>();
			if (start == null)
			{
				return hashSet;
			}
			hashSet.Add(start);
			Dictionary<RecipeDef, List<ThingDef>> dictionary = new Dictionary<RecipeDef, List<ThingDef>>();
			for (int i = 0; i < plans.Count; i++)
			{
				CraftPlan craftPlan = plans[i];
				if (craftPlan == null || craftPlan.recipe == null || dictionary.ContainsKey(craftPlan.recipe))
				{
					continue;
				}
				List<ThingDef> list = new List<ThingDef>();
				if (craftPlan.recipe.products != null)
				{
					for (int j = 0; j < craftPlan.recipe.products.Count; j++)
					{
						ThingDefCountClass val = craftPlan.recipe.products[j];
						if (val != null && val.thingDef != null)
						{
							list.Add(val.thingDef);
						}
					}
				}
				dictionary[craftPlan.recipe] = list;
			}
			Queue<RecipeDef> queue = new Queue<RecipeDef>();
			queue.Enqueue(start);
			while (queue.Count > 0)
			{
				RecipeDef key = queue.Dequeue();
				if (!dictionary.TryGetValue(key, out var value) || value == null || value.Count == 0)
				{
					continue;
				}
				foreach (KeyValuePair<RecipeDef, List<ThingDef>> item in dictionary)
				{
					RecipeDef key2 = item.Key;
					if (hashSet.Contains(key2) || key2.ingredients == null)
					{
						continue;
					}
					bool flag = false;
					for (int k = 0; k < key2.ingredients.Count; k++)
					{
						if (flag)
						{
							break;
						}
						IngredientCount val2 = key2.ingredients[k];
						if (val2 == null || val2.filter == null)
						{
							continue;
						}
						IEnumerable<ThingDef> allowedThingDefs = val2.filter.AllowedThingDefs;
						if (allowedThingDefs == null)
						{
							continue;
						}
						foreach (ThingDef item2 in allowedThingDefs)
						{
							if (value.Contains(item2))
							{
								flag = true;
								break;
							}
						}
					}
					if (flag && hashSet.Add(key2))
					{
						queue.Enqueue(key2);
					}
				}
			}
			return hashSet;
		}

		internal static bool CountSatisfied(CraftPlan plan)
		{
			if (plan == null)
			{
				return false;
			}
			if (plan.mode == 2)
			{
				return plan.countedCount >= plan.targetCount;
			}
			if (plan.mode == 1)
			{
				return plan.remaining <= 0;
			}
			return false;
		}

		internal static bool StepSatisfied(CraftPlan plan)
		{
			if (plan == null)
			{
				return true;
			}
			if (plan.mode == 2)
			{
				return !plan.wantsWork;
			}
			if (plan.mode == 1)
			{
				return plan.Done;
			}
			return false;
		}

		private static bool IsStarvedForMaterials(CraftLine line)
		{
			if (line == null)
			{
				return false;
			}
			string blockKey = line.BlockKey;
			if (!(blockKey == "DS_BA_Block_Material"))
			{
				return blockKey == "DS_BA_NoCoreMaterial";
			}
			return true;
		}

		private List<CraftPlan> JobOrderedPlans()
		{
			List<CraftPlan> list = new List<CraftPlan>(plans);
			if (jobs == null || jobs.Count == 0 || plans.Count < 2)
			{
				return list;
			}
			Dictionary<RecipeDef, int> depth = new Dictionary<RecipeDef, int>();
			for (int i = 0; i < jobs.Count; i++)
			{
				CraftJob craftJob = jobs[i];
				if (craftJob == null)
				{
					continue;
				}
				for (int j = 0; j < craftJob.steps.Count; j++)
				{
					CraftJobStep craftJobStep = craftJob.steps[j];
					if (craftJobStep != null && craftJobStep.recipe != null)
					{
						depth.TryGetValue(craftJobStep.recipe, out var value);
						if (craftJobStep.depth > value)
						{
							depth[craftJobStep.recipe] = craftJobStep.depth;
						}
					}
				}
			}
			if (depth.Count == 0)
			{
				return list;
			}
			Dictionary<CraftPlan, int> index = new Dictionary<CraftPlan, int>();
			for (int k = 0; k < plans.Count; k++)
			{
				index[plans[k]] = k;
			}
			list.Sort(delegate(CraftPlan a, CraftPlan b)
			{
				int num = DepthOfRecipe(depth, a);
				int num2 = DepthOfRecipe(depth, b);
				if (num != num2)
				{
					return num2 - num;
				}
				index.TryGetValue(a, out var value2);
				index.TryGetValue(b, out var value3);
				return value2 - value3;
			});
			return list;
		}

		private static int DepthOfRecipe(Dictionary<RecipeDef, int> depth, CraftPlan plan)
		{
			if (plan == null || plan.recipe == null)
			{
				return 0;
			}
			if (!depth.TryGetValue(plan.recipe, out var value))
			{
				return 0;
			}
			return value;
		}

		internal bool HasPlan(RecipeDef recipe)
		{
			for (int i = 0; i < plans.Count; i++)
			{
				if (plans[i].recipe == recipe)
				{
					return true;
				}
			}
			return false;
		}

		internal void AddPlan(RecipeDef recipe)
		{
			if (recipe != null && !HasPlan(recipe))
			{
				CraftPlan craftPlan = new CraftPlan();
				craftPlan.recipe = recipe;
				craftPlan.mode = 0;
				plans.Add(craftPlan);
			}
		}

		internal int RemoveSelectedTree(CraftPlan anyPlanOfThatTree)
		{
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00df: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_0100: Unknown result type (might be due to invalid IL or missing references)
			if (anyPlanOfThatTree == null)
			{
				return 0;
			}
			CraftJob craftJob = ((anyPlanOfThatTree.recipe == null) ? null : AnyJobForRecipe(anyPlanOfThatTree.recipe));
			if (craftJob != null)
			{
				int count = craftJob.steps.Count;
				CancelJob(craftJob);
				return count;
			}
			HashSet<RecipeDef> hashSet = CollectDependentTree(anyPlanOfThatTree.recipe);
			if (hashSet.Count == 0)
			{
				hashSet.Add(anyPlanOfThatTree.recipe);
			}
			int num = 0;
			for (int num2 = plans.Count - 1; num2 >= 0; num2--)
			{
				CraftPlan craftPlan = plans[num2];
				if (craftPlan != null && craftPlan.recipe != null && hashSet.Contains(craftPlan.recipe))
				{
					for (int num3 = craftPlan.lines.Count - 1; num3 >= 0; num3--)
					{
						ReleaseLine(craftPlan, num3);
					}
					plans.RemoveAt(num2);
					num++;
				}
			}
			if (num > 0)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_CA_DeletedTree", NamedArgument.op_Implicit(num))), LookTargets.op_Implicit(new TargetInfo(((Thing)base.parent).PositionHeld, ((Thing)base.parent).MapHeld, false)), MessageTypeDefOf.NeutralEvent, false);
			}
			return num;
		}

		private HashSet<RecipeDef> CollectDependentTree(RecipeDef start)
		{
			HashSet<RecipeDef> hashSet = new HashSet<RecipeDef>();
			if (start == null)
			{
				return hashSet;
			}
			Dictionary<RecipeDef, List<ThingDef>> dictionary = new Dictionary<RecipeDef, List<ThingDef>>();
			for (int i = 0; i < plans.Count; i++)
			{
				CraftPlan craftPlan = plans[i];
				if (craftPlan == null || craftPlan.recipe == null || dictionary.ContainsKey(craftPlan.recipe))
				{
					continue;
				}
				List<ThingDef> list = new List<ThingDef>();
				if (craftPlan.recipe.products != null)
				{
					for (int j = 0; j < craftPlan.recipe.products.Count; j++)
					{
						ThingDefCountClass val = craftPlan.recipe.products[j];
						if (val != null && val.thingDef != null)
						{
							list.Add(val.thingDef);
						}
					}
				}
				dictionary[craftPlan.recipe] = list;
			}
			Queue<RecipeDef> queue = new Queue<RecipeDef>();
			hashSet.Add(start);
			queue.Enqueue(start);
			while (queue.Count > 0)
			{
				RecipeDef val2 = queue.Dequeue();
				if (val2 == null)
				{
					continue;
				}
				if (val2.ingredients != null)
				{
					for (int k = 0; k < val2.ingredients.Count; k++)
					{
						IngredientCount val3 = val2.ingredients[k];
						if (val3 == null || val3.filter == null)
						{
							continue;
						}
						IEnumerable<ThingDef> allowedThingDefs = val3.filter.AllowedThingDefs;
						if (allowedThingDefs == null)
						{
							continue;
						}
						foreach (ThingDef item in allowedThingDefs)
						{
							if (item == null)
							{
								continue;
							}
							foreach (KeyValuePair<RecipeDef, List<ThingDef>> item2 in dictionary)
							{
								if (item2.Value.Contains(item) && hashSet.Add(item2.Key))
								{
									queue.Enqueue(item2.Key);
								}
							}
						}
					}
				}
				List<ThingDef> value = null;
				dictionary.TryGetValue(val2, out value);
				if (value == null)
				{
					continue;
				}
				foreach (KeyValuePair<RecipeDef, List<ThingDef>> item3 in dictionary)
				{
					RecipeDef key = item3.Key;
					if (hashSet.Contains(key) || key.ingredients == null)
					{
						continue;
					}
					bool flag = false;
					for (int l = 0; l < key.ingredients.Count; l++)
					{
						if (flag)
						{
							break;
						}
						IngredientCount val4 = key.ingredients[l];
						if (val4 == null || val4.filter == null)
						{
							continue;
						}
						IEnumerable<ThingDef> allowedThingDefs2 = val4.filter.AllowedThingDefs;
						if (allowedThingDefs2 == null)
						{
							continue;
						}
						foreach (ThingDef item4 in allowedThingDefs2)
						{
							if (value.Contains(item4))
							{
								flag = true;
								break;
							}
						}
					}
					if (flag && hashSet.Add(key))
					{
						queue.Enqueue(key);
					}
				}
			}
			return hashSet;
		}

		internal void RemovePlan(CraftPlan plan, bool singleOnly = false)
		{
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			if (plan == null)
			{
				return;
			}
			if (singleOnly)
			{
				CraftJob craftJob = ((plan.recipe == null) ? null : AnyJobForRecipe(plan.recipe));
				if (craftJob != null && craftJob.steps != null)
				{
					for (int num = craftJob.steps.Count - 1; num >= 1; num--)
					{
						CraftJobStep craftJobStep = craftJob.steps[num];
						if (craftJobStep != null && craftJobStep.recipe == plan.recipe)
						{
							craftJob.steps.RemoveAt(num);
						}
					}
				}
				string text = ((plan.recipe == null) ? "?" : ((object)((Def)plan.recipe).LabelCap/*cast due to constrained. prefix*/).ToString());
				for (int num2 = plan.lines.Count - 1; num2 >= 0; num2--)
				{
					ReleaseLine(plan, num2);
				}
				plans.Remove(plan);
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_CA_DeletedOne", NamedArgument.op_Implicit(text))), LookTargets.op_Implicit(new TargetInfo(((Thing)base.parent).PositionHeld, ((Thing)base.parent).MapHeld, false)), MessageTypeDefOf.NeutralEvent, false);
				return;
			}
			CraftJob craftJob2 = ((plan.recipe == null) ? null : AnyJobForRecipe(plan.recipe));
			if (craftJob2 != null)
			{
				CancelJob(craftJob2);
				return;
			}
			for (int num3 = plan.lines.Count - 1; num3 >= 0; num3--)
			{
				ReleaseLine(plan, num3);
			}
			plans.Remove(plan);
		}

		internal void MovePlan(CraftPlan plan, int dir)
		{
			int num = plans.IndexOf(plan);
			if (num >= 0)
			{
				int num2 = num + dir;
				if (num2 >= 0 && num2 < plans.Count)
				{
					plans[num] = plans[num2];
					plans[num2] = plan;
				}
			}
		}

		public void RecalcWatts()
		{
			float num = Props.basePowerWatts;
			if (enabled)
			{
				num += BenchWatts();
			}
			cachedWatts = num * OverclockPowerMult;
			ReassertPower();
		}

		private void ReassertPower()
		{
			if (cachedWatts < 0f)
			{
				return;
			}
			CompPowerTrader val = ThingCompUtility.TryGetComp<CompPowerTrader>((Thing)(object)base.parent);
			if (val != null)
			{
				float num = 0f - cachedWatts;
				if (!Mathf.Approximately(val.PowerOutput, num))
				{
					val.PowerOutput = num;
				}
			}
		}

		private void UpdateVisuals(Map map)
		{
			int cap = Math.Max(0, Props.maxVisualSlots);
			int num = 0;
			for (int i = 0; i < plans.Count; i++)
			{
				CraftPlan craftPlan = plans[i];
				for (int j = 0; j < craftPlan.lines.Count; j++)
				{
					craftPlan.lines[j].TickVisual(map, num, cap);
					num++;
				}
			}
		}

		internal void NoteProduct(Thing p, bool stored)
		{
			if (p != null)
			{
				string key = BillCraftFunnel.DescribeProduct(p);
				int num = Math.Max(1, p.stackCount);
				Dictionary<string, int> obj = (stored ? pendingStored : pendingDropped);
				obj.TryGetValue(key, out var value);
				obj[key] = value + num;
				if (stored)
				{
					completedCount++;
				}
				else
				{
					droppedCount++;
				}
			}
		}

		internal void NotifyPlanDone(CraftPlan plan)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			if (plan != null && plan.recipe != null)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_BA_PlanDone", NamedArgument.op_Implicit(((Def)plan.recipe).LabelCap), NamedArgument.op_Implicit(plan.completed))), LookTargets.op_Implicit(new TargetInfo(((Thing)base.parent).PositionHeld, ((Thing)base.parent).MapHeld, false)), MessageTypeDefOf.TaskCompletion, false);
			}
		}

		private void FlushMessages(int now)
		{
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			if ((pendingStored.Count != 0 || pendingDropped.Count != 0) && now - lastMessageTick >= Math.Max(0, Props.messageIntervalTicks))
			{
				lastMessageTick = now;
				string text = BuildList(pendingStored);
				string text2 = BuildList(pendingDropped);
				TargetInfo val = default(TargetInfo);
				((TargetInfo)(ref val))..ctor(((Thing)base.parent).PositionHeld, ((Thing)base.parent).MapHeld, false);
				if (text != null)
				{
					Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_BA_Done", NamedArgument.op_Implicit(text))), LookTargets.op_Implicit(val), MessageTypeDefOf.TaskCompletion, false);
				}
				if (text2 != null)
				{
					Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_BA_DoneDropped", NamedArgument.op_Implicit(text2))), LookTargets.op_Implicit(val), MessageTypeDefOf.CautionInput, false);
				}
				pendingStored.Clear();
				pendingDropped.Clear();
			}
		}

		private static string BuildList(Dictionary<string, int> map)
		{
			if (map.Count == 0)
			{
				return null;
			}
			StringBuilder stringBuilder = new StringBuilder();
			int num = 0;
			foreach (KeyValuePair<string, int> item in map)
			{
				if (num >= 4)
				{
					stringBuilder.Append("…");
					break;
				}
				if (num > 0)
				{
					stringBuilder.Append("、");
				}
				stringBuilder.Append(item.Key);
				stringBuilder.Append(" ×");
				stringBuilder.Append(item.Value);
				num++;
			}
			return stringBuilder.ToString();
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (((Thing)base.parent).Spawned)
			{
				yield return (Gizmo)new Command_Toggle
				{
					icon = (Texture)(object)GizmoTex(ref texToggle, "UI/Gizmos/制作代理-开关"),
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("DS_BA_Toggle")),
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("DS_BA_ToggleDesc")),
					isActive = () => enabled,
					toggleAction = delegate
					{
						Enabled = !enabled;
					}
				};
				yield return (Gizmo)new Command_Action
				{
					icon = (Texture)(object)GizmoTex(ref texOverclock, "UI/Gizmos/制作代理-超频"),
					defaultLabel = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_BA_Overclock", NamedArgument.op_Implicit(OverclockLabel()))),
					defaultDesc = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_BA_OverclockDesc", NamedArgument.op_Implicit(CurrentWatts.ToString("#####0")))),
					action = delegate
					{
						//IL_0020: Unknown result type (might be due to invalid IL or missing references)
						//IL_002b: Unknown result type (might be due to invalid IL or missing references)
						//IL_0043: Unknown result type (might be due to invalid IL or missing references)
						//IL_0048: Unknown result type (might be due to invalid IL or missing references)
						//IL_0058: Unknown result type (might be due to invalid IL or missing references)
						//IL_0069: Unknown result type (might be due to invalid IL or missing references)
						OverclockTier = (overclockTier + 1) % 4;
						Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_BA_OverclockMsg", NamedArgument.op_Implicit(((Entity)base.parent).LabelShort), NamedArgument.op_Implicit(OverclockLabel()), NamedArgument.op_Implicit(CurrentWatts.ToString("#####0")))), LookTargets.op_Implicit(new TargetInfo(((Thing)base.parent).PositionHeld, ((Thing)base.parent).MapHeld, false)), MessageTypeDefOf.SilentInput, false);
					}
				};
				yield return (Gizmo)new Command_Action
				{
					icon = (Texture)(object)GizmoTex(ref texRange, "UI/Gizmos/制作代理-显示范围"),
					defaultLabel = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("DS_BA_RangeLabel", NamedArgument.op_Implicit(RangeLabel()))),
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("DS_BA_RangeDesc")),
					action = delegate
					{
						CycleRange();
					}
				};
				yield return (Gizmo)new Command_Action
				{
					icon = (Texture)(object)GizmoTex(ref texPanel, "UI/Gizmos/制作代理-显示范围"),
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("DS_AE2PREVIEW")),
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("DS_AE2PREVIEW_DESC")),
					action = delegate
					{
						Find.WindowStack.Add((Window)(object)new Window_AE2CraftPanel(this));
					}
				};
			}
		}

		private Texture2D GizmoTex(ref Texture2D cache, string path)
		{
			if ((Object)(object)cache == (Object)null)
			{
				cache = ContentFinder<Texture2D>.Get(path, true);
			}
			return cache;
		}

		public string OverclockLabel()
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			return overclockTier switch
			{
				1 => TaggedString.op_Implicit(Translator.Translate("DS_BA_OC_3")), 
				2 => TaggedString.op_Implicit(Translator.Translate("DS_BA_OC_6")), 
				3 => TaggedString.op_Implicit(Translator.Translate("DS_BA_OC_9")), 
				_ => TaggedString.op_Implicit(Translator.Translate("DS_BA_OC_Off")), 
			};
		}

		public override string CompInspectStringExtra()
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
			if (!((Thing)base.parent).Spawned)
			{
				return null;
			}
			if (!Powered)
			{
				return ((object)Translator.Translate("DS_BA_InspectOff")/*cast due to constrained. prefix*/).ToString();
			}
			if (!enabled)
			{
				return ((object)Translator.Translate("DS_BA_InspectDisabled")/*cast due to constrained. prefix*/).ToString();
			}
			int num = TotalLines();
			string text = ((object)TranslatorFormattedStringExtensions.Translate("DS_BA_InspectWork", NamedArgument.op_Implicit(num), NamedArgument.op_Implicit(plans.Count), NamedArgument.op_Implicit(CurrentWatts.ToString("#####0")), NamedArgument.op_Implicit(Props.workSpeedMult.ToString("0.0")), NamedArgument.op_Implicit(OverclockSpeedMult.ToString("0")), NamedArgument.op_Implicit(Props.skillLevel))/*cast due to constrained. prefix*/).ToString();
			if (benches.Count == 0)
			{
				text += ((object)Translator.Translate("DS_BA_InspectNoBench")/*cast due to constrained. prefix*/).ToString();
			}
			return text;
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			((ThingComp)this).PostSpawnSetup(respawningAfterLoad);
			craftingStorageTier = 3;
			parallelUnits = 8;
			RecalcWatts();
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = (DestroyMode)0)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			((ThingComp)this).PostDeSpawn(map, mode);
			if (slotsMap == null)
			{
				slotsMap = map;
			}
			ReleaseAll();
			slotsMap = null;
		}

		internal string RangeLabel()
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			if (rangeOverride == -1)
			{
				return ((object)Translator.Translate("DS_BA_RangeWholeMap")/*cast due to constrained. prefix*/).ToString();
			}
			int scanRadius = ScanRadius;
			return scanRadius * 2 + 1 + "x" + (scanRadius * 2 + 1);
		}

		internal void CycleRange()
		{
			int effectiveScanRadius = EffectiveScanRadius;
			int[] array = new int[6]
			{
				0,
				effectiveScanRadius,
				effectiveScanRadius + 4,
				effectiveScanRadius + 9,
				effectiveScanRadius + 17,
				-1
			};
			int num = 0;
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] == rangeOverride)
				{
					num = i;
					break;
				}
			}
			rangeOverride = array[(num + 1) % array.Length];
		}

		internal void SetRange(int r)
		{
			rangeOverride = ((r == -1) ? (-1) : Math.Max(0, Math.Min(60, r)));
		}

		public override void PostExposeData()
		{
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a4: Invalid comparison between Unknown and I4
			Scribe_Values.Look<int>(ref rangeOverride, "dsRangeOverride", 0, false);
			((ThingComp)this).PostExposeData();
			Scribe_Values.Look<bool>(ref enabled, "billAutoEnabled", true, false);
			Scribe_Values.Look<int>(ref overclockTier, "billAutoOverclock", 0, false);
			Scribe_Values.Look<bool>(ref showRange, "billAutoShowRange", false, false);
			Scribe_Values.Look<int>(ref craftingStorageTier, "billAutoStorageTier", 3, false);
			Scribe_Values.Look<int>(ref parallelUnits, "billAutoParallelUnits", 8, false);
			Scribe_Collections.Look<CraftPlan>(ref plans, "billAutoPlans", (LookMode)2, Array.Empty<object>());
			Scribe_Collections.Look<CraftJob>(ref jobs, "billAutoJobs", (LookMode)2, Array.Empty<object>());
			if ((int)Scribe.mode != 4)
			{
				return;
			}
			if (plans == null)
			{
				plans = new List<CraftPlan>();
			}
			if (jobs == null)
			{
				jobs = new List<CraftJob>();
			}
			for (int num = plans.Count - 1; num >= 0; num--)
			{
				if (plans[num] == null || plans[num].recipe == null)
				{
					plans.RemoveAt(num);
				}
			}
			cachedWatts = -1f;
			RecalcWatts();
		}
	}
}
