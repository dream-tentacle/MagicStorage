using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ASEL;

[StaticConstructorOnStartup]
public class Building_MonolynProducer : Building, IThingHolder, ISuspendableThingHolder
{
	public int time;

	public bool pause = false;

	private static readonly Texture2D CancelIcon = ContentFinder<Texture2D>.Get("UI/CancelBuilding", true);

	public bool insufficientLight = false;

	public float lightConsumption;

	public List<ThingDefCountClass> requiredCost = new List<ThingDefCountClass>();

	public bool working = false;

	public int count;

	public MonolynRecipeDef recipe;

	public CompLightStorage lightStorage;

	public CompLightReceiver receiver;

	public int selectedOption = 1;

	private Sustainer sustainerWorking;

	protected bool soundPlayed = false;

	public float Speed = 1f;

	public ThingOwner innerContainer;

	private MonolynProductionExtension cachedExtension;

	private MonolynProductionExtension extension => cachedExtension ?? (cachedExtension = ((Def)((Thing)this).def).GetModExtension<MonolynProductionExtension>());

	public virtual bool IsContentsSuspended => true;

	public virtual bool workStarted => working && time < recipe.recipeTime;

	protected virtual bool canStartNow => true;

	public virtual string productionOption
	{
		get
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			if (selectedOption == 3)
			{
				return TaggedString.op_Implicit(Translator.Translate("ForeverMN"));
			}
			if (selectedOption == 2)
			{
				return TaggedString.op_Implicit(Translator.Translate("TargetCountMN"));
			}
			return TaggedString.op_Implicit(Translator.Translate("RepeatCountMN"));
		}
	}

	protected virtual string pauseText => TaggedString.op_Implicit(Translator.Translate("pauseTextNormal"));

	public Building_MonolynProducer()
	{
		innerContainer = (ThingOwner)(object)new ThingOwner<Thing>((IThingHolder)(object)this, (LookMode)2, true);
	}

	public void GetChildHolders(List<IThingHolder> outChildren)
	{
		ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, (IList<Thing>)GetDirectlyHeldThings());
	}

	public ThingOwner GetDirectlyHeldThings()
	{
		return innerContainer;
	}

	public virtual string productionOptionB(int option = 1)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		return option switch
		{
			3 => TaggedString.op_Implicit(Translator.Translate("ForeverMN")), 
			2 => TaggedString.op_Implicit(Translator.Translate("TargetCountMN")), 
			_ => TaggedString.op_Implicit(Translator.Translate("RepeatCountMN")), 
		};
	}

	public float AdjustTime(float value)
	{
		float num = Speed;
		if (num <= 0f)
		{
			num = 0.0001f;
		}
		float num2 = value / num;
		if (num2 < 0f)
		{
			num2 = 0f;
		}
		return num2;
	}

	protected virtual void playStartSound()
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		soundPlayed = true;
		if (extension.workStartSound != null)
		{
			SoundStarter.PlayOneShot(extension.workStartSound, SoundInfo.InMap(new TargetInfo(((Thing)this).Position, ((Thing)this).Map, false), (MaintenanceType)0));
		}
	}

	public void ResetCurrent(bool forceInsufficientLight = true, bool adjustTime = false)
	{
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Expected O, but got Unknown
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Expected O, but got Unknown
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Expected O, but got Unknown
		receiver.SignalDistributorsToDistribute();
		if (lightStorage.currentLight >= lightConsumption)
		{
			insufficientLight = false;
			return;
		}
		soundPlayed = false;
		if (forceInsufficientLight)
		{
			insufficientLight = true;
		}
		requiredCost.Clear();
		if (adjustTime)
		{
			time = (int)AdjustTime(recipe.recipeTime);
		}
		else
		{
			time = recipe.recipeTime;
		}
		innerContainer.TryDropAll(((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, true);
		if (recipe.useCostList && recipe.CostList != null)
		{
			foreach (ThingDefCountClass cost in recipe.CostList)
			{
				ThingDefCountClass item = new ThingDefCountClass(cost.thingDef, cost.count);
				requiredCost.Add(item);
			}
		}
		if (recipe.bloodCost > 0)
		{
			if (recipe.catalyst)
			{
				ThingDefCountClass item2 = new ThingDefCountClass(MNThingDefOf.MonolynBlackCatalyst, recipe.bloodCost);
				requiredCost.Add(item2);
			}
			else
			{
				ThingDefCountClass item3 = new ThingDefCountClass(MNThingDefOf.BlackBlood, recipe.bloodCost);
				requiredCost.Add(item3);
			}
		}
	}

	protected override void Tick()
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		((ThingWithComps)this).Tick();
		if (extension.sustainer != null && working && !pause)
		{
			if (sustainerWorking == null || (sustainerWorking.Ended && HasSufficientMaterials() && !insufficientLight))
			{
				sustainerWorking = SoundStarter.TrySpawnSustainer(extension.sustainer, SoundInfo.InMap(TargetInfo.op_Implicit((Thing)(object)this), (MaintenanceType)1));
			}
			else if (working && !insufficientLight && HasSufficientMaterials())
			{
				if (!soundPlayed)
				{
					playStartSound();
				}
				sustainerWorking.Maintain();
			}
		}
		if (!working)
		{
			return;
		}
		if (Gen.IsHashIntervalTick((Thing)(object)this, 2500) && recipe != null && recipe.recipeType != "Mech")
		{
			if (selectedOption == 2)
			{
				if (HasEnoughResource(((Thing)this).Map, recipe.thing, count))
				{
					if (recipe.canPause)
					{
						pause = true;
					}
					else if (!workStarted)
					{
						pause = true;
					}
				}
				else
				{
					pause = false;
				}
			}
			else
			{
				pause = false;
			}
		}
		if (!Gen.IsHashIntervalTick((Thing)(object)this, 300) || !HasSufficientMaterials() || pause || (!canStartNow && !workStarted))
		{
			return;
		}
		if (lightStorage.currentLight >= lightConsumption)
		{
			insufficientLight = false;
			if (time <= 300)
			{
				lightStorage.UseLight(lightConsumption);
				Finish();
			}
			if (time > 300)
			{
				time -= 300;
				lightStorage.UseLight(lightConsumption);
			}
		}
		else
		{
			receiver.SignalDistributorsToDistribute();
			if (lightStorage.currentLight <= lightConsumption)
			{
				insufficientLight = true;
			}
		}
	}

	private bool HasEnoughResource(Map map, ThingDef thingDef, int requiredAmount)
	{
		int num = map.resourceCounter.GetCount(thingDef);
		return num >= requiredAmount;
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		((Building)this).SpawnSetup(map, respawningAfterLoad);
		lightStorage = ((ThingWithComps)this).GetComp<CompLightStorage>();
		receiver = ((ThingWithComps)this).GetComp<CompLightReceiver>();
		ASELComponent component = Current.Game.GetComponent<ASELComponent>();
		component.producers.Add(this);
	}

	public override void Destroy(DestroyMode mode = (DestroyMode)0)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		((Building)this).Destroy(mode);
		ASELComponent component = Current.Game.GetComponent<ASELComponent>();
		component.producers.Remove(this);
	}

	public virtual void StartWork(MonolynRecipeDef r, int c)
	{
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Expected O, but got Unknown
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Expected O, but got Unknown
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Expected O, but got Unknown
		recipe = r;
		if (recipe.useCostList && recipe.CostList != null)
		{
			foreach (ThingDefCountClass cost in recipe.CostList)
			{
				ThingDefCountClass item = new ThingDefCountClass(cost.thingDef, cost.count);
				requiredCost.Add(item);
			}
		}
		if (recipe.bloodCost > 0)
		{
			if (r.catalyst)
			{
				ThingDefCountClass item2 = new ThingDefCountClass(MNThingDefOf.MonolynBlackCatalyst, recipe.bloodCost);
				requiredCost.Add(item2);
			}
			else
			{
				ThingDefCountClass item3 = new ThingDefCountClass(MNThingDefOf.BlackBlood, recipe.bloodCost);
				requiredCost.Add(item3);
			}
		}
		count = c;
		time = recipe.recipeTime;
		float num = recipe.recipeTime;
		float num2 = recipe.lightCost;
		if (recipe.recipeTime <= 0)
		{
			lightConsumption = recipe.lightCost;
		}
		else
		{
			lightConsumption = num2 / num * 300f;
		}
		if (lightStorage.maxLightP < lightConsumption)
		{
			lightStorage.maxLightP += lightConsumption;
		}
		working = true;
	}

	public virtual void Notify_MaterialReceived(Thing material)
	{
		foreach (ThingDefCountClass item in requiredCost)
		{
			if (item.thingDef == material.def)
			{
				item.count -= material.stackCount;
				Thing material2 = material.SplitOff(material.stackCount);
				ContainMaterials(material2);
				if (item.count <= 0)
				{
					item.count = 0;
					requiredCost.Remove(item);
				}
				break;
			}
		}
	}

	public void ContainMaterials(Thing material)
	{
		DelayedActionManager.Instance.Schedule(1, delegate
		{
			innerContainer.TryAdd(material, true);
		});
	}

	public virtual bool HasSufficientMaterials()
	{
		foreach (ThingDefCountClass item in requiredCost)
		{
			if (item.count > 0)
			{
				return false;
			}
		}
		return true;
	}

	public virtual void Finish()
	{
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_05df: Expected O, but got Unknown
		//IL_05af: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b6: Expected O, but got Unknown
		//IL_0481: Unknown result type (might be due to invalid IL or missing references)
		//IL_0488: Expected O, but got Unknown
		//IL_0458: Unknown result type (might be due to invalid IL or missing references)
		//IL_045f: Expected O, but got Unknown
		//IL_0735: Unknown result type (might be due to invalid IL or missing references)
		//IL_073c: Expected O, but got Unknown
		//IL_070c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0713: Expected O, but got Unknown
		//IL_0547: Unknown result type (might be due to invalid IL or missing references)
		//IL_054e: Expected O, but got Unknown
		//IL_03f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f7: Expected O, but got Unknown
		//IL_06a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ab: Expected O, but got Unknown
		soundPlayed = false;
		if (!GenList.NullOrEmpty<Thing>((IList<Thing>)innerContainer))
		{
			innerContainer.RemoveAll((Predicate<Thing>)((Thing thing) => true));
		}
		requiredCost.Clear();
		Thing val2 = default(Thing);
		if (recipe.recipeType == "Mech")
		{
			Pawn val = PawnGenerator.GeneratePawn(new PawnGenerationRequest(recipe.pawnKindDef, ((Thing)this).Faction, (PawnGenerationContext)2, (PlanetTile?)PlanetTile.op_Implicit(-1), true, false, false, true, true, 20f, false, true, false, true, false, false, false, false, false, 0f, 0f, (Pawn)null, 1f, (Predicate<Pawn>)null, (Predicate<Pawn>)null, (IEnumerable<TraitDef>)null, (IEnumerable<TraitDef>)null, (float?)null, (float?)null, (float?)null, (Gender?)null, (string)null, (string)null, (RoyalTitleDef)null, (Ideo)null, false, false, false, false, (List<GeneDef>)null, (List<GeneDef>)null, (XenotypeDef)null, (CustomXenotype)null, (List<XenotypeDef>)null, 0f, (DevelopmentalStage)8, (Func<XenotypeDef, PawnKindDef>)null, (FloatRange?)null, (FloatRange?)null, false, false, false, -1, 0, false));
			GenDrop.TryDropSpawn((Thing)(object)val, ((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)1, ref val2, (Action<Thing, int>)null, (Predicate<IntVec3>)null, true);
			val.ageTracker.AgeBiologicalTicks = 0L;
			val.ageTracker.AgeChronologicalTicks = 0L;
			Effecter val3 = MNEffecterDefOf.ConstructSpawned.Spawn();
			val3.Trigger(TargetInfo.op_Implicit((Thing)(object)val), TargetInfo.op_Implicit((Thing)(object)val), -1);
			val3.Cleanup();
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("ConstructPrinted", NamedArgument.op_Implicit((Thing)(object)val))), LookTargets.op_Implicit((Thing)(object)val), MessageTypeDefOf.TaskCompletion, false);
			CompOverseerSubject val4 = ThingCompUtility.TryGetComp<CompOverseerSubject>((Thing)(object)val);
			if (val4 != null)
			{
				CompFormatorOverseer compFormatorOverseer = ThingCompUtility.TryGetComp<CompFormatorOverseer>((Thing)(object)this);
				if (compFormatorOverseer != null)
				{
					compFormatorOverseer.tunedTo.relations.AddDirectRelation(PawnRelationDefOf.Overseer, val);
				}
			}
		}
		else
		{
			foreach (ThingDefCountClass product in ((RecipeDef)recipe).products)
			{
				for (int num = 0; num < product.count; num++)
				{
					Thing val5 = ThingMaker.MakeThing(product.thingDef, (ThingDef)null);
					GenDrop.TryDropSpawn(val5, ((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)1, ref val2, (Action<Thing, int>)null, (Predicate<IntVec3>)null, true);
				}
			}
		}
		insufficientLight = false;
		if (selectedOption == 1 || selectedOption == 0 || recipe.recipeType == "Mech")
		{
			if (count <= 1)
			{
				if (recipe.recipeType != "Mech")
				{
					Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("ProductionComplete", NamedArgument.op_Implicit((Def)(object)recipe.thing))), LookTargets.op_Implicit((Thing)(object)this), MessageTypeDefOf.TaskCompletion, false);
				}
				recipe = null;
				lightConsumption = 0f;
				lightStorage.maxLightP = lightStorage.Props.maxLight;
				time = 0;
				count = 0;
				working = false;
			}
			else
			{
				count--;
				if (recipe.heatGeneration <= 0f)
				{
					time = recipe.recipeTime;
				}
				else
				{
					time = (int)AdjustTime(recipe.recipeTime);
				}
				if (recipe.useCostList && recipe.CostList != null)
				{
					foreach (ThingDefCountClass cost in recipe.CostList)
					{
						ThingDefCountClass item = new ThingDefCountClass(cost.thingDef, cost.count);
						requiredCost.Add(item);
					}
				}
				if (recipe.bloodCost > 0)
				{
					if (recipe.catalyst)
					{
						ThingDefCountClass item2 = new ThingDefCountClass(MNThingDefOf.MonolynBlackCatalyst, recipe.bloodCost);
						requiredCost.Add(item2);
					}
					else
					{
						ThingDefCountClass item3 = new ThingDefCountClass(MNThingDefOf.BlackBlood, recipe.bloodCost);
						requiredCost.Add(item3);
					}
				}
			}
		}
		if (selectedOption == 2)
		{
			if (recipe.heatGeneration > 0f)
			{
				time = (int)AdjustTime(recipe.recipeTime);
			}
			else
			{
				time = recipe.recipeTime;
			}
			if (recipe.useCostList && recipe.CostList != null)
			{
				foreach (ThingDefCountClass cost2 in recipe.CostList)
				{
					ThingDefCountClass item4 = new ThingDefCountClass(cost2.thingDef, cost2.count);
					requiredCost.Add(item4);
				}
			}
			if (recipe.bloodCost > 0)
			{
				if (recipe.catalyst)
				{
					ThingDefCountClass item5 = new ThingDefCountClass(MNThingDefOf.MonolynBlackCatalyst, recipe.bloodCost);
					requiredCost.Add(item5);
				}
				else
				{
					ThingDefCountClass item6 = new ThingDefCountClass(MNThingDefOf.BlackBlood, recipe.bloodCost);
					requiredCost.Add(item6);
				}
			}
		}
		if (selectedOption != 3)
		{
			return;
		}
		count = 0;
		if (recipe.heatGeneration > 0f)
		{
			time = (int)AdjustTime(recipe.recipeTime);
		}
		else
		{
			time = recipe.recipeTime;
		}
		if (recipe.useCostList && recipe.CostList != null)
		{
			foreach (ThingDefCountClass cost3 in recipe.CostList)
			{
				ThingDefCountClass item7 = new ThingDefCountClass(cost3.thingDef, cost3.count);
				requiredCost.Add(item7);
			}
		}
		if (recipe.bloodCost > 0)
		{
			if (recipe.catalyst)
			{
				ThingDefCountClass item8 = new ThingDefCountClass(MNThingDefOf.MonolynBlackCatalyst, recipe.bloodCost);
				requiredCost.Add(item8);
			}
			else
			{
				ThingDefCountClass item9 = new ThingDefCountClass(MNThingDefOf.BlackBlood, recipe.bloodCost);
				requiredCost.Add(item9);
			}
		}
	}

	public virtual void Cancel()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		soundPlayed = false;
		innerContainer.TryDropAll(((Thing)this).Position, ((Thing)this).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, true);
		insufficientLight = false;
		recipe = null;
		insufficientLight = false;
		requiredCost.Clear();
		lightConsumption = 0f;
		lightStorage.maxLightP = lightStorage.Props.maxLight;
		time = 0;
		working = false;
		count = 0;
	}

	public override string GetInspectString()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		string text = ((ThingWithComps)this).GetInspectString();
		if (time > 0 && HasSufficientMaterials())
		{
			if (!GenText.NullOrEmpty(text))
			{
				text += "\n";
			}
			text = TaggedString.op_Implicit(text + (Translator.Translate("FabricatorTimeLeft") + " : " + (((float)time / 2500f).ToString("F1") + Translator.Translate("Hours"))));
			if (insufficientLight)
			{
				if (!GenText.NullOrEmpty(text))
				{
					text += "\n";
				}
				text = TaggedString.op_Implicit(text + Translator.Translate("insufficientLightFabricator"));
			}
			if (pause)
			{
				if (!GenText.NullOrEmpty(text))
				{
					text += "\n";
				}
				text += pauseText;
			}
		}
		if (!HasSufficientMaterials())
		{
			if (!GenText.NullOrEmpty(text))
			{
				text += "\n";
			}
			text = TaggedString.op_Implicit(text + Translator.Translate("WaitingForMaterials"));
			text += "\n";
			text += GetCostListTooltip();
		}
		return text;
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		foreach (Gizmo item in <>n__0())
		{
			yield return item;
		}
		if (working)
		{
			Command_Action command_ActionC = new Command_Action();
			((Command)command_ActionC).defaultLabel = TaggedString.op_Implicit(Translator.Translate("CommandCancelProductionMono"));
			((Command)command_ActionC).defaultDesc = TaggedString.op_Implicit(Translator.Translate("CommandCancelProductionMonoDesc"));
			((Command)command_ActionC).icon = (Texture)(object)CancelIcon;
			((Command)command_ActionC).hotKey = KeyBindingDefOf.Designator_Cancel;
			command_ActionC.action = delegate
			{
				Cancel();
			};
			((Command)command_ActionC).activateSound = SoundDefOf.Designate_Cancel;
			yield return (Gizmo)(object)command_ActionC;
		}
		if (!DebugSettings.ShowDevGizmos)
		{
			yield break;
		}
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = "FillMaterials",
			action = delegate
			{
				foreach (ThingDefCountClass item2 in requiredCost)
				{
					item2.count = 0;
				}
			}
		};
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = "Finish",
			action = delegate
			{
				Finish();
			}
		};
	}

	public override void ExposeData()
	{
		((Building)this).ExposeData();
		Scribe_Values.Look<float>(ref lightConsumption, "lightConsumption", 0f, false);
		Scribe_Values.Look<bool>(ref working, "working", false, false);
		Scribe_Collections.Look<ThingDefCountClass>(ref requiredCost, "cost", (LookMode)2, Array.Empty<object>());
		Scribe_Values.Look<int>(ref time, "timeRemaining", 0, false);
		Scribe_Values.Look<bool>(ref pause, "pause", false, false);
		Scribe_Defs.Look<MonolynRecipeDef>(ref recipe, "receipe");
		Scribe_Values.Look<int>(ref count, "count", 0, false);
		Scribe_Values.Look<int>(ref selectedOption, "selectedOption", 0, false);
		Scribe_Values.Look<bool>(ref soundPlayed, "sound", false, false);
		Scribe_Deep.Look<ThingOwner>(ref innerContainer, "innerContainer", new object[1] { this });
	}

	public string GetCostListTooltip()
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		List<string> list = new List<string>();
		foreach (ThingDefCountClass item2 in requiredCost)
		{
			string item = $"{((Def)item2.thingDef).LabelCap}: {item2.count}";
			list.Add(item);
		}
		return string.Join("\n", list);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerable<Gizmo> <>n__0()
	{
		return ((Building)this).GetGizmos();
	}
}
