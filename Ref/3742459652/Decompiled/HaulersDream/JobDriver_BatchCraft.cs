using System;
using System.Collections.Generic;
using System.Linq;
using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_BatchCraft : JobDriver_DoBill
{
	private sealed class MixSlot
	{
		public int slotIndex;

		public double perRepValue;

		public readonly List<ThingDef> defs = new List<ThingDef>();
	}

	private struct MixCand
	{
		public ThingDef def;

		public float vpu;

		public int count;

		public Thing rep;

		public int stock;
	}

	private const TargetIndex BenchInd = (TargetIndex)1;

	private const TargetIndex LoadStackInd = (TargetIndex)2;

	private const TargetIndex PlaceCellInd = (TargetIndex)3;

	private List<ThingDef> ingredientDefs = new List<ThingDef>();

	private List<int> perRepCounts = new List<int>();

	private int repsTarget;

	private int repsDone;

	private int deadlineTick;

	private bool planResolved;

	private int roundsThisTrip;

	private int repsDoneAtTripStart;

	private int placeSlotCursor;

	private int placeSlotRemaining = -1;

	public bool ActivelyCrafting;

	private List<MixSlot> mixSlotsCache;

	private Dictionary<ThingDef, int> mixDefToSlotCache;

	private Bill mixModelBuiltFor;

	private List<ThingDef> mixDefUnionCache;

	private Building_WorkTable Bench
	{
		get
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = ((JobDriver)this).job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			return (Building_WorkTable)(object)((thing is Building_WorkTable) ? thing : null);
		}
	}

	private ThingOwner Inv
	{
		get
		{
			Pawn_InventoryTracker inventory = ((JobDriver)this).pawn.inventory;
			if (inventory == null)
			{
				return null;
			}
			return inventory.GetDirectlyHeldThings();
		}
	}

	private CompHauledToInventory Comp => ((ThingWithComps)((JobDriver)this).pawn).GetComp<CompHauledToInventory>();

	private bool MixingRecipe
	{
		get
		{
			Bill bill = ((JobDriver)this).job.bill;
			if (bill == null)
			{
				return false;
			}
			return bill.recipe?.allowMixingIngredients == true;
		}
	}

	private List<MixSlot> MixSlots()
	{
		Bill bill = ((JobDriver)this).job.bill;
		RecipeDef val = bill?.recipe;
		if (val == null || !val.allowMixingIngredients)
		{
			return mixSlotsCache ?? (mixSlotsCache = new List<MixSlot>());
		}
		if (mixSlotsCache != null && mixModelBuiltFor == bill)
		{
			return mixSlotsCache;
		}
		List<MixSlot> list = new List<MixSlot>();
		Dictionary<ThingDef, int> dictionary = new Dictionary<ThingDef, int>();
		List<IngredientCount> ingredients = val.ingredients;
		for (int i = 0; i < ingredients.Count; i++)
		{
			IngredientCount val2 = ingredients[i];
			MixSlot mixSlot = new MixSlot
			{
				slotIndex = i,
				perRepValue = val2.GetBaseCount()
			};
			foreach (ThingDef allowedThingDef in val2.filter.AllowedThingDefs)
			{
				if (allowedThingDef != null && (val2.IsFixedIngredient || bill.ingredientFilter == null || bill.ingredientFilter.Allows(allowedThingDef)) && !dictionary.ContainsKey(allowedThingDef))
				{
					dictionary[allowedThingDef] = i;
					mixSlot.defs.Add(allowedThingDef);
				}
			}
			list.Add(mixSlot);
		}
		mixSlotsCache = list;
		mixDefToSlotCache = dictionary;
		mixDefUnionCache = null;
		mixModelBuiltFor = bill;
		return mixSlotsCache;
	}

	private float ValuePerUnit(ThingDef def)
	{
		RecipeDef val = ((JobDriver)this).job.bill?.recipe;
		if (val == null || def == null)
		{
			return 0f;
		}
		return val.IngredientValueGetter.ValuePerUnitOf(def);
	}

	private bool IsMixDef(ThingDef def)
	{
		if (def == null)
		{
			return false;
		}
		MixSlots();
		if (mixDefToSlotCache != null)
		{
			return mixDefToSlotCache.ContainsKey(def);
		}
		return false;
	}

	public override void ExposeData()
	{
		((JobDriver_DoBill)this).ExposeData();
		Scribe_Collections.Look<ThingDef>(ref ingredientDefs, "hdBatchIngDefs", (LookMode)4, Array.Empty<object>());
		Scribe_Collections.Look<int>(ref perRepCounts, "hdBatchPerRep", (LookMode)1, Array.Empty<object>());
		Scribe_Values.Look<int>(ref repsTarget, "hdBatchRepsTarget", 0, false);
		Scribe_Values.Look<int>(ref repsDone, "hdBatchRepsDone", 0, false);
		Scribe_Values.Look<int>(ref deadlineTick, "hdBatchDeadline", 0, false);
		Scribe_Values.Look<bool>(ref planResolved, "hdBatchPlanResolved", false, false);
		Scribe_Values.Look<int>(ref placeSlotCursor, "hdBatchPlaceSlotCursor", 0, false);
		Scribe_Values.Look<int>(ref placeSlotRemaining, "hdBatchPlaceSlotRemaining", -1, false);
		Scribe_Values.Look<int>(ref roundsThisTrip, "hdBatchRoundsThisTrip", 0, false);
		Scribe_Values.Look<int>(ref repsDoneAtTripStart, "hdBatchRepsDoneAtTripStart", 0, false);
		if (ingredientDefs == null)
		{
			ingredientDefs = new List<ThingDef>();
		}
		if (perRepCounts == null)
		{
			perRepCounts = new List<int>();
		}
	}

	public override bool IsContinuation(Job j)
	{
		return false;
	}

	public override string GetReport()
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		RecipeDef val = ((JobDriver)this).job.bill?.recipe;
		string text = ((val == null) ? null : ((Def)(val.ProducedThingDef?)).label) ?? ((Def)(val?)).label ?? "?";
		return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.JobReport", NamedArgument.op_Implicit(text), NamedArgument.op_Implicit((repsDone + 1).ToString()), NamedArgument.op_Implicit(repsTarget.ToString())));
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		Building_WorkTable bench = Bench;
		if (bench == null)
		{
			return false;
		}
		if (!ReservationUtility.Reserve(((JobDriver)this).pawn, ((JobDriver)this).job.GetTarget((TargetIndex)1), ((JobDriver)this).job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false))
		{
			return false;
		}
		if (((Thing)bench).def.hasInteractionCell && !ReservationUtility.ReserveSittableOrSpot(((JobDriver)this).pawn, ((Thing)bench).InteractionCell, ((JobDriver)this).job, errorOnFailed))
		{
			return false;
		}
		return true;
	}

	public override void Notify_Starting()
	{
		((JobDriver)this).Notify_Starting();
		ResolvePlanFromHandoff();
	}

	private void ResolvePlanFromHandoff()
	{
		if (!planResolved)
		{
			CraftBatchPlan craftBatchPlan = BatchCraftHandoff.Consume(((JobDriver)this).job);
			if (craftBatchPlan != null && craftBatchPlan.bill != ((JobDriver)this).job.bill)
			{
				craftBatchPlan = null;
			}
			if (craftBatchPlan != null && craftBatchPlan.feasible)
			{
				ingredientDefs = new List<ThingDef>(craftBatchPlan.ingredientDefs);
				perRepCounts = new List<int>(craftBatchPlan.perRepCounts);
				repsTarget = craftBatchPlan.resolvedReps;
				deadlineTick = ((craftBatchPlan.timeoutTicks > 0) ? (Find.TickManager.TicksGame + craftBatchPlan.timeoutTicks) : 0);
			}
			else
			{
				RecoverPlanFromBill();
			}
			roundsThisTrip = RoundsThatFitNow();
			repsDoneAtTripStart = repsDone;
			planResolved = true;
		}
	}

	private void RecoverPlanFromBill()
	{
		ingredientDefs.Clear();
		perRepCounts.Clear();
		repsTarget = 0;
		Building_WorkTable bench = Bench;
		if (bench != null && ((JobDriver)this).job.bill != null)
		{
			CraftBatchPlan craftBatchPlan = CraftBatchPlanner.Resolve(((JobDriver)this).pawn, bench, ((JobDriver)this).job.bill, 1, 0);
			if (craftBatchPlan.feasible)
			{
				ingredientDefs = new List<ThingDef>(craftBatchPlan.ingredientDefs);
				perRepCounts = new List<int>(craftBatchPlan.perRepCounts);
				repsTarget = craftBatchPlan.resolvedReps;
			}
		}
	}

	public static void StartBatchCraftSynced(Pawn pawn, Building_WorkTable bench, Bill bill, int requestedReps, int timeoutTicks)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		if (pawn?.jobs == null || bench == null || bill == null)
		{
			return;
		}
		if (!BillRouteGate.MayRouteToInventory((Thing)(object)bench))
		{
			if (MultiplayerCompat.ShouldShowLocalFeedback)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.CouldNotStart")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
			}
			return;
		}
		CraftBatchPlan craftBatchPlan = CraftBatchPlanner.Resolve(pawn, bench, bill, requestedReps, timeoutTicks);
		if (craftBatchPlan == null || !craftBatchPlan.feasible)
		{
			if (MultiplayerCompat.ShouldShowLocalFeedback)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.CouldNotStart")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
			}
			return;
		}
		if (pawn.CurJobDef == HaulersDreamDefOf.HaulersDream_BatchCraft && pawn.CurJob?.bill == bill)
		{
			pawn.jobs.EndCurrentJob((JobCondition)16, false, true);
		}
		Job val = JobMaker.MakeJob(HaulersDreamDefOf.HaulersDream_BatchCraft, LocalTargetInfo.op_Implicit((Thing)(object)bench));
		val.count = 1;
		val.bill = bill;
		val.playerForced = true;
		BatchCraftHandoff.Set(val, craftBatchPlan);
		if (pawn.jobs.TryTakeOrderedJob(val, (JobTag?)(JobTag)0, false))
		{
			if (MultiplayerCompat.ShouldShowLocalFeedback)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.PlanCraft.Started", NamedArgument.op_Implicit(craftBatchPlan.resolvedReps), NamedArgument.op_Implicit(((Def)(bill.recipe.ProducedThingDef?)).label ?? bill.LabelCap))), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.TaskCompletion, false);
			}
			HDLog.Dbg(string.Format("[{0}] {1} batch crafting {2}× {3} ", "F-Craft1", pawn, craftBatchPlan.resolvedReps, ((Def)bill.recipe).defName) + $"(timeout {timeoutTicks} ticks, mass/rep {craftBatchPlan.massPerRepKg:0.0}kg).");
		}
		else
		{
			BatchCraftHandoff.Consume(val);
			if (MultiplayerCompat.ShouldShowLocalFeedback)
			{
				Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.PlanCraft.CouldNotStart")), LookTargets.op_Implicit((Thing)(object)pawn), MessageTypeDefOf.RejectInput, false);
			}
		}
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate
		{
			ActivelyCrafting = false;
			RegisterLeftoversForUnload();
			PawnUnloadChecker.CheckIfShouldUnload(((JobDriver)this).pawn, forced: true, behindQueuedWork: true);
		});
		ToilFailConditions.FailOn<JobDriver_BatchCraft>(this, (Func<bool>)delegate
		{
			Building_WorkTable bench = Bench;
			if (bench == null || !((Thing)bench).Spawned)
			{
				return true;
			}
			return ((JobDriver)this).job.bill == null || ((JobDriver)this).job.bill.DeletedOrDereferenced || !bench.CurrentlyUsableForBills();
		});
		ToilFailConditions.FailOnBurningImmobile<JobDriver_BatchCraft>(this, (TargetIndex)1);
		Toil gotoBench = Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)4, false);
		gotoBench.AddFinishAction((Action)delegate
		{
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings == null || !OverloadGate.NoOverload(settings))
			{
				int num = MaxRepsLoadable();
				if (num < repsTarget)
				{
					repsTarget = num;
				}
			}
		});
		Toil loadDecide = ToilMaker.MakeToil("HD_BatchLoadDecide");
		loadDecide.initAction = delegate
		{
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_0105: Unknown result type (might be due to invalid IL or missing references)
			bool flag = (MixingRecipe ? (MixSlots().Count == 0) : (ingredientDefs.Count == 0));
			if (repsTarget <= 0 || flag)
			{
				((JobDriver)this).JumpToToil(gotoBench);
			}
			else if (PastDeadline())
			{
				((JobDriver)this).JumpToToil(gotoBench);
			}
			else
			{
				Thing val = FindNeededStack();
				if (val == null)
				{
					((JobDriver)this).JumpToToil(gotoBench);
				}
				else if (BatchGatherCount(val) <= 0)
				{
					((JobDriver)this).JumpToToil(gotoBench);
				}
				else if (!ReservationUtility.Reserve(((JobDriver)this).pawn, LocalTargetInfo.op_Implicit(val), ((JobDriver)this).job, 1, -1, (ReservationLayerDef)null, false, false))
				{
					((JobDriver)this).JumpToToil(gotoBench);
				}
				else
				{
					((JobDriver)this).job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(val));
				}
			}
		};
		loadDecide.defaultCompleteMode = (ToilCompleteMode)1;
		yield return loadDecide;
		yield return SweepWalk.MakeToil((JobDriver)(object)this, (TargetIndex)2, "HD_BatchLoadGoto", loadDecide, delegate
		{
		}, () => false);
		yield return Toils_Haul.TakeToInventory((TargetIndex)2, (Func<int>)delegate
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			JobDriver_BatchCraft jobDriver_BatchCraft = this;
			LocalTargetInfo target = ((JobDriver)this).job.GetTarget((TargetIndex)2);
			return jobDriver_BatchCraft.BatchGatherCount(((LocalTargetInfo)(ref target)).Thing);
		});
		yield return Toils_Jump.Jump(loadDecide);
		yield return gotoBench;
		Toil done = ToilMaker.MakeToil("HD_BatchDone");
		done.initAction = delegate
		{
		};
		done.defaultCompleteMode = (ToilCompleteMode)1;
		Toil craftCheck = ToilMaker.MakeToil("HD_BatchCraftCheck");
		craftCheck.initAction = delegate
		{
			//IL_01de: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
			ActivelyCrafting = true;
			if (repsDone >= repsTarget)
			{
				((JobDriver)this).JumpToToil(done);
			}
			else if (PastDeadline())
			{
				((JobDriver)this).JumpToToil(done);
			}
			else if (((JobDriver)this).job.bill == null || ((JobDriver)this).job.bill.suspended)
			{
				((JobDriver)this).JumpToToil(done);
			}
			else if (ShouldYieldNow())
			{
				((JobDriver)this).JumpToToil(done);
			}
			else
			{
				Bill bill = ((JobDriver)this).job.bill;
				Bill_Production val = (Bill_Production)(object)((bill is Bill_Production) ? bill : null);
				if (val != null && val.repeatMode == BillRepeatModeDefOf.TargetCount && ((Bill)val).recipe.WorkerCounter.CanCountProducts(val))
				{
					int overshoot = HaulersDreamGameComponent.Instance?.BatchOvershootOf((Bill)(object)val) ?? 0;
					if (!BatchPausePolicy.MayCraftMore(CraftBatchPlanner.EffectiveProductCount(val), val.targetCount, overshoot, val.paused))
					{
						((JobDriver)this).JumpToToil(done);
						return;
					}
				}
				else if (!((JobDriver)this).job.bill.ShouldDoNow())
				{
					((JobDriver)this).JumpToToil(done);
					return;
				}
				if (!(MixingRecipe ? BuildMixForRep() : HasOneRepInInventory()))
				{
					if (PrepareNextTrip())
					{
						((JobDriver)this).JumpToToil(loadDecide);
					}
					else
					{
						((JobDriver)this).JumpToToil(done);
					}
				}
				else
				{
					placeSlotCursor = 0;
					placeSlotRemaining = -1;
					((JobDriver)this).job.placedThings = null;
					((JobDriver)this).job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit((Thing)null));
					((JobDriver)this).job.SetTarget((TargetIndex)3, LocalTargetInfo.op_Implicit((Thing)null));
				}
			}
		};
		craftCheck.defaultCompleteMode = (ToilCompleteMode)1;
		yield return craftCheck;
		Toil placeDecide = ToilMaker.MakeToil("HD_BatchPlaceDecide");
		Toil doRecipe = ToilMaker.MakeToil("HD_BatchDoRecipeMarker");
		doRecipe.initAction = delegate
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			((JobDriver)this).job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit((Thing)null));
		};
		doRecipe.defaultCompleteMode = (ToilCompleteMode)1;
		placeDecide.initAction = delegate
		{
			while (placeSlotCursor < ingredientDefs.Count)
			{
				if (placeSlotRemaining < 0)
				{
					placeSlotRemaining = perRepCounts[placeSlotCursor];
				}
				if (placeSlotRemaining > 0)
				{
					break;
				}
				placeSlotCursor++;
				placeSlotRemaining = -1;
			}
			if (placeSlotCursor >= ingredientDefs.Count)
			{
				((JobDriver)this).JumpToToil(doRecipe);
			}
			else
			{
				ThingDef def = ingredientDefs[placeSlotCursor];
				if (StartCarryRepSlot(def, placeSlotRemaining) <= 0)
				{
					AbortRepCarryPlace();
					((JobDriver)this).JumpToToil(done);
				}
			}
		};
		placeDecide.defaultCompleteMode = (ToilCompleteMode)1;
		yield return placeDecide;
		yield return ToilFailConditions.FailOnDespawnedNullOrForbidden<Toil>(Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)4, false), (TargetIndex)1);
		yield return Toils_JobTransforms.SetTargetToIngredientPlaceCell((TargetIndex)1, (TargetIndex)2, (TargetIndex)3);
		yield return PlaceRepSlotOnCell();
		yield return Toils_Jump.Jump(placeDecide);
		yield return doRecipe;
		yield return ToilFailConditions.FailOnCannotTouch<Toil>(ToilFailConditions.FailOnDespawnedNullOrForbidden<Toil>(ToilFailConditions.FailOnDespawnedNullOrForbiddenPlacedThings(Toils_Recipe.DoRecipeWork(), (TargetIndex)1), (TargetIndex)1), (TargetIndex)1, (PathEndMode)4);
		yield return FinishOneRepIntoInventory(done);
		yield return Toils_Jump.Jump(craftCheck);
		yield return done;
	}

	private Toil FinishOneRepIntoInventory(Toil doneToil)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		Toil obj = ToilMaker.MakeToil("HD_BatchFinishRep");
		obj.initAction = delegate
		{
			Pawn pawn = ((JobDriver)this).pawn;
			Map map = ((Thing)pawn).Map;
			Bill bill = ((JobDriver)this).job.bill;
			RecipeDef val = bill?.recipe;
			if (val == null)
			{
				((JobDriver)this).job.placedThings = null;
				((JobDriver)this).JumpToToil(doneToil);
				return;
			}
			List<Thing> list = null;
			List<Thing> list2 = null;
			try
			{
				list = CalculateIngredientsFromPlacedThings(val);
				if (list.Count == 0)
				{
					((JobDriver)this).JumpToToil(doneToil);
				}
				else
				{
					if (val.workSkill != null && pawn.skills != null)
					{
						float num = (float)base.ticksSpentDoingRecipeWork * 0.1f * val.workSkillLearnFactor;
						pawn.skills.GetSkill(val.workSkill).Learn(num, false, false);
					}
					Thing val2 = CalculateDominantIngredient(val, list);
					ThingStyleDef val3 = ComputeStyle(bill, val);
					list2 = GenRecipe.MakeRecipeProducts(val, pawn, list, val2, ((JobDriver_DoBill)this).BillGiver, bill.precept, val3, bill.graphicIndexOverride).ToList();
					for (int i = 0; i < list.Count; i++)
					{
						val.Worker.ConsumeIngredient(list[i], val, map);
					}
					bill.Notify_IterationCompleted(pawn, list);
					RecordsUtility.Notify_BillDone(pawn, list2);
					if (val.WorkAmountTotal((Thing)null) >= 10000f && list2.Count > 0)
					{
						TaleRecorder.RecordTale(TaleDefOf.CompletedLongCraftingProject, new object[2]
						{
							pawn,
							MinifyUtility.GetInnerIfMinified(list2[0]).def
						});
					}
					if (list2.Count > 0)
					{
						Find.QuestManager.Notify_ThingsProduced(pawn, list2);
					}
					Bill_Production val4 = (Bill_Production)(object)((bill is Bill_Production) ? bill : null);
					if (val4 != null && val4.repeatMode == BillRepeatModeDefOf.TargetCount)
					{
						map.resourceCounter.UpdateResourceCounts();
					}
					for (int j = 0; j < list2.Count; j++)
					{
						PlaceProductIntoInventory(list2[j]);
					}
					repsDone++;
					HDLog.Dbg($"{pawn} batch-crafted rep {repsDone}/{repsTarget} of {((Def)val).defName} " + $"({list2.Count} product stack(s) into inventory).");
				}
			}
			catch (Exception innerException)
			{
				if (list2 != null)
				{
					for (int k = 0; k < list2.Count; k++)
					{
						if (list2[k] != null && list2[k].holdingOwner == null && !list2[k].Spawned)
						{
							PlaceProductIntoInventory(list2[k]);
						}
					}
				}
				throw new Exception("[Hauler's Dream] batch-craft rep failed for " + ((Def)val).defName + " (placed ingredients on bench floor, products banked)", innerException);
			}
		};
		obj.defaultCompleteMode = (ToilCompleteMode)1;
		return obj;
	}

	private List<Thing> CalculateIngredientsFromPlacedThings(RecipeDef recipe)
	{
		List<Thing> list = new List<Thing>();
		List<ThingCountClass> placedThings = ((JobDriver)this).job.placedThings;
		if (placedThings != null)
		{
			for (int i = 0; i < placedThings.Count; i++)
			{
				ThingCountClass val = placedThings[i];
				if (val?.thing == null || val.Count <= 0)
				{
					continue;
				}
				Thing val2 = ((val.Count >= val.thing.stackCount) ? val.thing : val.thing.SplitOff(val.Count));
				val.Count = 0;
				if (list.Contains(val2))
				{
					continue;
				}
				list.Add(val2);
				if (recipe != null && recipe.autoStripCorpses)
				{
					IStrippable val3 = (IStrippable)(object)((val2 is IStrippable) ? val2 : null);
					if (val3 != null && val3.AnythingToStrip())
					{
						val3.Strip(true);
						continue;
					}
				}
				CorpseStripper.MaybeStripForCremation(((JobDriver)this).pawn, val2, recipe);
			}
		}
		((JobDriver)this).job.placedThings = null;
		return list;
	}

	private bool PastDeadline()
	{
		if (deadlineTick > 0)
		{
			return Find.TickManager.TicksGame >= deadlineTick;
		}
		return false;
	}

	private bool ShouldYieldNow()
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		Pawn_NeedsTracker val = ((JobDriver)this).pawn?.needs;
		int foodCategory = ((val?.food == null) ? (-1) : ((int)val.food.CurCategory));
		int restCategory = ((val?.rest == null) ? (-1) : ((int)val.rest.CurCategory));
		MentalBreaker val2 = ((JobDriver)this).pawn?.mindState?.mentalBreaker;
		bool mentalBreakImminent = val2 != null && (val2.BreakMajorIsImminent || val2.BreakExtremeIsImminent);
		bool inDanger = ((JobDriver)this).pawn != null && ((JobDriver)this).pawn.Downed;
		bool playerForced = ((JobDriver)this).job.playerForced;
		Pawn pawn = ((JobDriver)this).pawn;
		return BatchYieldPolicy.ShouldYield(playerForced, foodCategory, restCategory, mentalBreakImminent, pawn != null && pawn.Drafted, inDanger);
	}

	public bool ShouldFreezeRot(Thing t)
	{
		if (ActivelyCrafting && t != null)
		{
			if (!MixingRecipe)
			{
				return ingredientDefs.Contains(t.def);
			}
			return IsMixDef(t.def);
		}
		return false;
	}

	private int BatchGatherCount(Thing stack)
	{
		if (stack == null || !stack.Spawned)
		{
			return 0;
		}
		if (ForbidUtility.IsForbidden(stack, ((JobDriver)this).pawn) && !SweepForbidPolicy.MayTakeWhileForbidden(((JobDriver)this).job.playerForced, isOrderedAnchor: false))
		{
			return 0;
		}
		int num = (MixingRecipe ? MixNeededUnits(stack.def) : NeededUnits(stack.def));
		if (num <= 0)
		{
			return 0;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings != null && OverloadGate.NoOverload(settings))
		{
			int num2 = OverloadGate.CountToPickUp(((JobDriver)this).pawn, stack, settings);
			return Mathf.Min(Mathf.Min(num, num2), stack.stackCount);
		}
		return Mathf.Min(num, stack.stackCount);
	}

	private int NeededUnits(ThingDef def)
	{
		if (def == null)
		{
			return 0;
		}
		int num = LoadRepsTarget();
		int num2 = 0;
		for (int i = 0; i < ingredientDefs.Count; i++)
		{
			if (ingredientDefs[i] == def)
			{
				num2 += perRepCounts[i] * num;
			}
		}
		if (num2 <= 0)
		{
			return 0;
		}
		return Mathf.Max(0, num2 - InventoryCountOfDef(def));
	}

	private Thing FindNeededStack()
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		Map map = ((Thing)((JobDriver)this).pawn).Map;
		Bill bill = ((JobDriver)this).job.bill;
		float num = ((bill != null) ? (bill.ingredientSearchRadius * bill.ingredientSearchRadius) : float.MaxValue);
		Building_WorkTable bench = Bench;
		IntVec3 val = ((bench != null) ? ((Thing)bench).Position : ((Thing)((JobDriver)this).pawn).Position);
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		bool flag = settings != null && settings.cookMostStockFirst && SpoilingFirst.IsCookBill(bill, settings);
		bool flag2 = SpoilingFirst.AnyToggleOn(settings) || flag;
		Thing val2 = null;
		int num2 = int.MaxValue;
		List<ThingDef> list = (MixingRecipe ? MixDefUnion() : ingredientDefs);
		for (int i = 0; i < list.Count; i++)
		{
			ThingDef val3 = list[i];
			if (val3 == null)
			{
				continue;
			}
			bool flag3 = false;
			for (int j = 0; j < i; j++)
			{
				if (list[j] == val3)
				{
					flag3 = true;
					break;
				}
			}
			if (flag3 || (MixingRecipe ? MixNeededUnits(val3) : NeededUnits(val3)) <= 0)
			{
				continue;
			}
			List<Thing> list2 = map.listerThings.ThingsOfDef(val3);
			for (int k = 0; k < list2.Count; k++)
			{
				Thing val4 = list2[k];
				if (val4 == null || !val4.Spawned || ForbidUtility.IsForbidden(val4, ((JobDriver)this).pawn) || (bill != null && !InventoryShare.IsUsableForBill(val4, bill)))
				{
					continue;
				}
				IntVec3 val5 = val4.Position - val;
				if (!((float)((IntVec3)(ref val5)).LengthHorizontalSquared >= num) && ReservationUtility.CanReserve(((JobDriver)this).pawn, LocalTargetInfo.op_Implicit(val4), 1, -1, (ReservationLayerDef)null, false) && ReachabilityUtility.CanReach(((JobDriver)this).pawn, LocalTargetInfo.op_Implicit(val4), (PathEndMode)3, ExtraSweepReach.Ceiling(((JobDriver)this).pawn), false, false, (TraverseMode)0))
				{
					val5 = val4.Position - ((Thing)((JobDriver)this).pawn).Position;
					int lengthHorizontalSquared = ((IntVec3)(ref val5)).LengthHorizontalSquared;
					if (val2 == null || (flag2 ? SpoilingFirst.CookBetterThan(val4, lengthHorizontalSquared, val2, num2, flag, settings) : (lengthHorizontalSquared < num2)))
					{
						num2 = lengthHorizontalSquared;
						val2 = val4;
					}
				}
			}
		}
		return val2;
	}

	private int InventoryCountOfDef(ThingDef def)
	{
		ThingOwner inv = Inv;
		if (inv == null || def == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < inv.Count; i++)
		{
			if (inv[i]?.def == def)
			{
				num += inv[i].stackCount;
			}
		}
		return num;
	}

	private bool HasOneRepInInventory()
	{
		if (MixingRecipe)
		{
			List<MixSlot> list = MixSlots();
			if (list.Count == 0)
			{
				return false;
			}
			for (int i = 0; i < list.Count; i++)
			{
				double perRepValue = list[i].perRepValue;
				if (perRepValue > 0.0 && CurrentInventorySlotValue(list[i]) + 0.0001 < perRepValue)
				{
					return false;
				}
			}
			return true;
		}
		for (int j = 0; j < ingredientDefs.Count; j++)
		{
			ThingDef val = ingredientDefs[j];
			int num = 0;
			for (int k = 0; k < ingredientDefs.Count; k++)
			{
				if (ingredientDefs[k] == val)
				{
					num += perRepCounts[k];
				}
			}
			if (InventoryCountOfDef(val) < num)
			{
				return false;
			}
		}
		return ingredientDefs.Count > 0;
	}

	private int MaxRepsLoadable()
	{
		if (MixingRecipe)
		{
			List<MixSlot> list = MixSlots();
			int num = int.MaxValue;
			for (int i = 0; i < list.Count; i++)
			{
				double perRepValue = list[i].perRepValue;
				if (!(perRepValue <= 0.0))
				{
					int num2 = (int)Math.Floor((CurrentInventorySlotValue(list[i]) + 0.0001) / perRepValue);
					if (num2 < num)
					{
						num = num2;
					}
				}
			}
			if (num != int.MaxValue)
			{
				if (num >= 0)
				{
					return num;
				}
				return 0;
			}
			return 0;
		}
		int num3 = int.MaxValue;
		for (int j = 0; j < ingredientDefs.Count; j++)
		{
			ThingDef val = ingredientDefs[j];
			int num4 = 0;
			for (int k = 0; k < ingredientDefs.Count; k++)
			{
				if (ingredientDefs[k] == val)
				{
					num4 += perRepCounts[k];
				}
			}
			if (num4 > 0)
			{
				int num5 = InventoryCountOfDef(val) / num4;
				if (num5 < num3)
				{
					num3 = num5;
				}
			}
		}
		if (num3 != int.MaxValue)
		{
			return num3;
		}
		return 0;
	}

	private int LoadRepsTarget()
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !OverloadGate.NoOverload(settings))
		{
			return repsTarget;
		}
		return roundsThisTrip;
	}

	private int RoundsThatFitNow()
	{
		int num = repsTarget - repsDone;
		if (num <= 0)
		{
			return 0;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !OverloadGate.NoOverload(settings))
		{
			return num;
		}
		return CraftBatchMath.WholeRoundsThatFit(CarryMath.EffectiveCapacity(CarryCapacity.Of(((JobDriver)this).pawn), settings.carryLimitFraction) - MassUtility.GearAndInventoryMass(((JobDriver)this).pawn), freeBulk: CECompat.AvailableBulk(((JobDriver)this).pawn), massPerRoundKg: RoundMass(), bulkPerRound: RoundBulk(), remainingReps: num);
	}

	private float RoundMass()
	{
		if (MixingRecipe)
		{
			List<MixSlot> list = MixSlots();
			float num = 0f;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].perRepValue > 0.0)
				{
					num += (float)list[i].perRepValue * BestMassPerValue(list[i]);
				}
			}
			return num;
		}
		float num2 = 0f;
		for (int j = 0; j < ingredientDefs.Count; j++)
		{
			num2 += (float)perRepCounts[j] * StatExtension.GetStatValueAbstract((BuildableDef)(object)ingredientDefs[j], StatDefOf.Mass, (ThingDef)null);
		}
		return num2;
	}

	private float RoundBulk()
	{
		if (!CECompat.IsActive)
		{
			return 0f;
		}
		if (MixingRecipe)
		{
			List<MixSlot> list = MixSlots();
			float num = 0f;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].perRepValue > 0.0)
				{
					num += (float)list[i].perRepValue * BestBulkPerValue(list[i]);
				}
			}
			return num;
		}
		float num2 = 0f;
		for (int j = 0; j < ingredientDefs.Count; j++)
		{
			num2 += (float)perRepCounts[j] * CECompat.BulkPerUnitAbstract(ingredientDefs[j]);
		}
		return num2;
	}

	private float BestMassPerValue(MixSlot slot)
	{
		float num = 0f;
		for (int i = 0; i < slot.defs.Count; i++)
		{
			float num2 = ValuePerUnit(slot.defs[i]);
			if (!(num2 <= 0f))
			{
				float num3 = StatExtension.GetStatValueAbstract((BuildableDef)(object)slot.defs[i], StatDefOf.Mass, (ThingDef)null) / num2;
				if (num3 > num)
				{
					num = num3;
				}
			}
		}
		return num;
	}

	private float BestBulkPerValue(MixSlot slot)
	{
		float num = 0f;
		for (int i = 0; i < slot.defs.Count; i++)
		{
			float num2 = ValuePerUnit(slot.defs[i]);
			if (!(num2 <= 0f))
			{
				float num3 = CECompat.BulkPerUnitAbstract(slot.defs[i]) / num2;
				if (num3 > num)
				{
					num = num3;
				}
			}
		}
		return num;
	}

	private bool PrepareNextTrip()
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !OverloadGate.NoOverload(settings))
		{
			return false;
		}
		if (repsDone >= repsTarget)
		{
			return false;
		}
		if (repsDone <= repsDoneAtTripStart)
		{
			return false;
		}
		ActivelyCrafting = false;
		int num = RoundsThatFitNow();
		if (num <= 0)
		{
			DropBatchProducts();
			num = RoundsThatFitNow();
			if (num <= 0)
			{
				return false;
			}
		}
		roundsThisTrip = num;
		repsDoneAtTripStart = repsDone;
		if (FindNeededStack() == null)
		{
			return false;
		}
		return true;
	}

	private void DropBatchProducts()
	{
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		CompHauledToInventory comp = Comp;
		ThingOwner inv = Inv;
		if (comp == null || inv == null || ((Thing)((JobDriver)this).pawn).Map == null)
		{
			return;
		}
		Bill bill = ((JobDriver)this).job.bill;
		List<Thing> list = new List<Thing>(comp.PeekHashSet());
		Thing val2 = default(Thing);
		for (int i = 0; i < list.Count; i++)
		{
			Thing val = list[i];
			if (val != null && inv.Contains(val) && (bill == null || !InventoryShare.IsUsableForBill(val, bill)) && inv.TryDrop(val, ((Thing)((JobDriver)this).pawn).Position, ((Thing)((JobDriver)this).pawn).Map, (ThingPlaceMode)1, ref val2, (Action<Thing, int>)null, (Predicate<IntVec3>)null, true))
			{
				comp.Deregister(val);
			}
		}
	}

	private List<ThingDef> MixDefUnion()
	{
		List<MixSlot> list = MixSlots();
		if (mixDefUnionCache != null && mixModelBuiltFor == ((JobDriver)this).job.bill)
		{
			return mixDefUnionCache;
		}
		List<ThingDef> list2 = new List<ThingDef>();
		HashSet<ThingDef> hashSet = new HashSet<ThingDef>();
		for (int i = 0; i < list.Count; i++)
		{
			List<ThingDef> defs = list[i].defs;
			for (int j = 0; j < defs.Count; j++)
			{
				if (hashSet.Add(defs[j]))
				{
					list2.Add(defs[j]);
				}
			}
		}
		mixDefUnionCache = list2;
		return mixDefUnionCache;
	}

	private double CurrentInventorySlotValue(MixSlot slot)
	{
		ThingOwner inv = Inv;
		if (inv == null || slot == null)
		{
			return 0.0;
		}
		MixSlots();
		Bill bill = ((JobDriver)this).job.bill;
		double num = 0.0;
		for (int i = 0; i < inv.Count; i++)
		{
			Thing val = inv[i];
			if (val?.def != null && mixDefToSlotCache != null && mixDefToSlotCache.TryGetValue(val.def, out var value) && value == slot.slotIndex && (bill == null || InventoryShare.IsUsableForBill(val, bill)))
			{
				float num2 = ValuePerUnit(val.def);
				if (!(num2 <= 0f))
				{
					num += (double)val.stackCount * (double)num2;
				}
			}
		}
		return num;
	}

	private double SlotValueNeeded(MixSlot slot)
	{
		if (slot == null || slot.perRepValue <= 0.0)
		{
			return 0.0;
		}
		double num = slot.perRepValue * (double)LoadRepsTarget();
		double num2 = CurrentInventorySlotValue(slot);
		double num3 = num - num2;
		if (!(num3 > 0.0))
		{
			return 0.0;
		}
		return num3;
	}

	private int MixNeededUnits(ThingDef def)
	{
		if (def == null)
		{
			return 0;
		}
		MixSlots();
		if (mixDefToSlotCache == null || !mixDefToSlotCache.TryGetValue(def, out var value))
		{
			return 0;
		}
		List<MixSlot> list = MixSlots();
		if (value < 0 || value >= list.Count)
		{
			return 0;
		}
		float num = ValuePerUnit(def);
		if (num <= 0f)
		{
			return 0;
		}
		double num2 = SlotValueNeeded(list[value]);
		if (num2 <= 0.0)
		{
			return 0;
		}
		int num3 = Mathf.CeilToInt((float)(num2 / (double)num));
		if (num3 >= 0)
		{
			return num3;
		}
		return 0;
	}

	private bool BuildMixForRep()
	{
		ingredientDefs.Clear();
		perRepCounts.Clear();
		ThingOwner inv = Inv;
		Bill bill = ((JobDriver)this).job.bill;
		if (inv == null || bill == null)
		{
			return false;
		}
		List<MixSlot> list = MixSlots();
		if (list.Count == 0)
		{
			return false;
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		bool spoilingOn = SpoilingFirst.AnyToggleOn(settings);
		bool applyStock = settings != null && settings.cookMostStockFirst && SpoilingFirst.IsCookBill(bill, settings);
		for (int i = 0; i < list.Count; i++)
		{
			MixSlot mixSlot = list[i];
			double perRepValue = mixSlot.perRepValue;
			if (perRepValue <= 0.0)
			{
				continue;
			}
			List<MixCand> list2 = new List<MixCand>();
			List<ThingDef> defs = mixSlot.defs;
			for (int j = 0; j < defs.Count; j++)
			{
				ThingDef val = defs[j];
				if (val == null)
				{
					continue;
				}
				float num = ValuePerUnit(val);
				if (num <= 0f)
				{
					continue;
				}
				int num2 = 0;
				Thing val2 = null;
				for (int k = 0; k < inv.Count; k++)
				{
					Thing val3 = inv[k];
					if (val3?.def == val && InventoryShare.IsUsableForBill(val3, bill))
					{
						num2 += val3.stackCount;
						if (val2 == null || MoreSpoiled(val3, val2))
						{
							val2 = val3;
						}
					}
				}
				if (num2 > 0)
				{
					int stock = (applyStock ? SpoilingFirst.StockOfDef(val, ((Thing)((JobDriver)this).pawn).Map) : 0);
					list2.Add(new MixCand
					{
						def = val,
						vpu = num,
						count = num2,
						rep = val2,
						stock = stock
					});
				}
			}
			if (list2.Count == 0)
			{
				return false;
			}
			list2.Sort(delegate(MixCand a, MixCand b)
			{
				if (applyStock && a.stock != b.stock)
				{
					return b.stock.CompareTo(a.stock);
				}
				if (spoilingOn && a.rep != null && b.rep != null)
				{
					if (SpoilingFirst.BetterThan(a.rep, 0, b.rep, 0, settings))
					{
						return -1;
					}
					if (SpoilingFirst.BetterThan(b.rep, 0, a.rep, 0, settings))
					{
						return 1;
					}
				}
				int num5 = a.vpu.CompareTo(b.vpu);
				return (num5 != 0) ? num5 : ((Def)a.def).shortHash.CompareTo(((Def)b.def).shortHash);
			});
			double[] array = new double[list2.Count];
			int[] array2 = new int[list2.Count];
			for (int num3 = 0; num3 < list2.Count; num3++)
			{
				array[num3] = list2[num3].vpu;
				array2[num3] = list2[num3].count;
			}
			CraftBatchMath.MixFill mixFill = CraftBatchMath.MixFillSlot(perRepValue, array, array2);
			if (!mixFill.filled)
			{
				return false;
			}
			for (int num4 = 0; num4 < list2.Count; num4++)
			{
				if (mixFill.counts[num4] > 0)
				{
					ingredientDefs.Add(list2[num4].def);
					perRepCounts.Add(mixFill.counts[num4]);
				}
			}
		}
		return ingredientDefs.Count > 0;
	}

	private static bool MoreSpoiled(Thing a, Thing b)
	{
		int num = RotTicks(a);
		int num2 = RotTicks(b);
		return num < num2;
	}

	private static int RotTicks(Thing t)
	{
		CompRottable val = ((t != null) ? ThingCompUtility.TryGetComp<CompRottable>(t) : null);
		if (val == null || !val.Active)
		{
			return int.MaxValue;
		}
		return val.TicksUntilRotAtCurrentTemp;
	}

	private int StartCarryRepSlot(ThingDef def, int maxToCarry)
	{
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		ThingOwner inv = Inv;
		if (inv == null || def == null || maxToCarry <= 0)
		{
			return 0;
		}
		Bill bill = ((JobDriver)this).job.bill;
		Pawn_CarryTracker carryTracker = ((JobDriver)this).pawn.carryTracker;
		int num = ((((carryTracker != null) ? carryTracker.CarriedThing : null) != null && ((JobDriver)this).pawn.carryTracker.CarriedThing.def == def) ? ((JobDriver)this).pawn.carryTracker.CarriedThing.stackCount : 0);
		Pawn_CarryTracker carryTracker2 = ((JobDriver)this).pawn.carryTracker;
		int num2 = CraftBatchMath.CarryPassTarget(maxToCarry, num, (carryTracker2 != null) ? carryTracker2.AvailableStackSpace(def) : 0) - num;
		while (num2 > 0)
		{
			Thing val = null;
			for (int i = 0; i < inv.Count; i++)
			{
				Thing val2 = inv[i];
				if (val2?.def == def && (bill == null || InventoryShare.IsUsableForBill(val2, bill)))
				{
					val = val2;
					break;
				}
			}
			if (val == null)
			{
				break;
			}
			int num3 = Mathf.Min(num2, val.stackCount);
			int num4 = ((JobDriver)this).pawn.carryTracker.TryStartCarry(val, num3, false);
			if (num4 <= 0)
			{
				break;
			}
			if (Comp != null)
			{
				Comp.Deregister(val);
			}
			num2 -= num4;
		}
		Pawn_CarryTracker carryTracker3 = ((JobDriver)this).pawn.carryTracker;
		Thing val3 = ((carryTracker3 != null) ? carryTracker3.CarriedThing : null);
		if (val3 == null)
		{
			return 0;
		}
		((JobDriver)this).job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(val3));
		((JobDriver)this).job.count = val3.stackCount;
		return val3.stackCount;
	}

	private Toil PlaceRepSlotOnCell()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		Toil obj = ToilMaker.MakeToil("HD_BatchPlaceOnCell");
		obj.initAction = delegate
		{
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_0130: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0188: Unknown result type (might be due to invalid IL or missing references)
			//IL_019a: Unknown result type (might be due to invalid IL or missing references)
			Pawn_CarryTracker carryTracker = ((JobDriver)this).pawn.carryTracker;
			Thing val = ((carryTracker != null) ? carryTracker.CarriedThing : null);
			int placedCount;
			if (val == null)
			{
				placeSlotRemaining = 0;
				((JobDriver)this).job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit((Thing)null));
				((JobDriver)this).job.SetTarget((TargetIndex)3, LocalTargetInfo.op_Implicit((Thing)null));
			}
			else
			{
				int stackCount = val.stackCount;
				LocalTargetInfo target = ((JobDriver)this).job.GetTarget((TargetIndex)3);
				IntVec3 val2 = ((LocalTargetInfo)(ref target)).Cell;
				if (!((IntVec3)(ref val2)).IsValid)
				{
					Building_WorkTable bench = Bench;
					val2 = ((bench != null) ? ((Thing)bench).InteractionCell : ((Thing)((JobDriver)this).pawn).Position);
				}
				placedCount = 0;
				Thing val3 = default(Thing);
				if (!((JobDriver)this).pawn.carryTracker.TryDropCarriedThing(val2, (ThingPlaceMode)0, ref val3, (Action<Thing, int>)Record) && val.holdingOwner == null && !val.Spawned && !val.Destroyed)
				{
					GenPlace.TryPlaceThing(val, val2, ((Thing)((JobDriver)this).pawn).Map, (ThingPlaceMode)1, ref val3, (Action<Thing, int>)Record, (Predicate<IntVec3>)null, (Rot4?)null, 1);
				}
				Pawn_CarryTracker carryTracker2 = ((JobDriver)this).pawn.carryTracker;
				if (((carryTracker2 != null) ? carryTracker2.CarriedThing : null) != null)
				{
					((JobDriver)this).pawn.carryTracker.TryDropCarriedThing(((Thing)((JobDriver)this).pawn).Position, (ThingPlaceMode)1, ref val3, (Action<Thing, int>)null);
				}
				int num = ((placedCount > 0) ? placedCount : stackCount);
				placeSlotRemaining -= num;
				if (placeSlotRemaining <= 0)
				{
					placeSlotCursor++;
					placeSlotRemaining = -1;
				}
				((JobDriver)this).job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit((Thing)null));
				((JobDriver)this).job.SetTarget((TargetIndex)3, LocalTargetInfo.op_Implicit((Thing)null));
			}
			void Record(Thing th, int added)
			{
				//IL_0054: Unknown result type (might be due to invalid IL or missing references)
				placedCount += added;
				HaulAIUtility.UpdateJobWithPlacedThings(((JobDriver)this).job, th, added);
				if (th != null)
				{
					Map map = ((Thing)((JobDriver)this).pawn).Map;
					if (map != null)
					{
						map.physicalInteractionReservationManager.Reserve(((JobDriver)this).pawn, ((JobDriver)this).job, LocalTargetInfo.op_Implicit(th));
					}
				}
			}
		};
		obj.defaultCompleteMode = (ToilCompleteMode)1;
		return obj;
	}

	private void AbortRepCarryPlace()
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		Pawn_CarryTracker carryTracker = ((JobDriver)this).pawn.carryTracker;
		if (((carryTracker != null) ? carryTracker.CarriedThing : null) != null)
		{
			Thing val = default(Thing);
			((JobDriver)this).pawn.carryTracker.TryDropCarriedThing(((Thing)((JobDriver)this).pawn).Position, (ThingPlaceMode)1, ref val, (Action<Thing, int>)null);
		}
		((JobDriver)this).job.placedThings = null;
		placeSlotCursor = 0;
		placeSlotRemaining = -1;
		((JobDriver)this).job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit((Thing)null));
		((JobDriver)this).job.SetTarget((TargetIndex)3, LocalTargetInfo.op_Implicit((Thing)null));
	}

	private void PlaceProductIntoInventory(Thing product)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		if (product == null || product.Destroyed)
		{
			return;
		}
		ThingOwner inv = Inv;
		CompHauledToInventory comp = Comp;
		if (inv == null)
		{
			GenPlace.TryPlaceThing(product, ((Thing)((JobDriver)this).pawn).Position, ((Thing)((JobDriver)this).pawn).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			return;
		}
		int stackCount = product.stackCount;
		bool flag = inv.TryAddOrTransfer(product, true);
		if (comp != null && (flag || product.stackCount < stackCount))
		{
			int mergedCount = (flag ? stackCount : (stackCount - product.stackCount));
			Thing val = ((product.def.stackLimit == 1) ? product : (YieldRouter.InventoryStackOfDef(inv, product.def) ?? (flag ? product : null)));
			if (val != null)
			{
				comp.RegisterHauledItem(val, mergedCount);
			}
			comp.NotifyYieldPicked();
		}
		if (!flag && product.stackCount > 0 && !product.Destroyed)
		{
			GenPlace.TryPlaceThing(product, ((Thing)((JobDriver)this).pawn).Position, ((Thing)((JobDriver)this).pawn).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
		}
	}

	private void RegisterLeftoversForUnload()
	{
		CompHauledToInventory comp = Comp;
		ThingOwner inv = Inv;
		if (comp == null || inv == null)
		{
			return;
		}
		HashSet<ThingDef> hashSet = new HashSet<ThingDef>(ingredientDefs);
		if (MixingRecipe)
		{
			List<MixSlot> list = MixSlots();
			for (int i = 0; i < list.Count; i++)
			{
				List<ThingDef> defs = list[i].defs;
				for (int j = 0; j < defs.Count; j++)
				{
					hashSet.Add(defs[j]);
				}
			}
		}
		HashSet<ThingDef> hashSet2 = new HashSet<ThingDef>();
		List<ThingDefCountClass> list2 = ((JobDriver)this).job.bill?.recipe?.products;
		if (list2 != null)
		{
			for (int k = 0; k < list2.Count; k++)
			{
				if (list2[k]?.thingDef != null)
				{
					hashSet2.Add(list2[k].thingDef);
				}
			}
		}
		for (int l = 0; l < inv.Count; l++)
		{
			Thing val = inv[l];
			if (val != null && (hashSet.Contains(val.def) || hashSet2.Contains(val.def)))
			{
				comp.RegisterHauledItem(val);
			}
		}
	}

	private static Thing CalculateDominantIngredient(RecipeDef recipe, List<Thing> ingredients)
	{
		if (GenList.NullOrEmpty<Thing>((IList<Thing>)ingredients))
		{
			return null;
		}
		if (recipe.productHasIngredientStuff)
		{
			return ingredients[0];
		}
		if (GenCollection.Any<ThingDefCountClass>(recipe.products, (Predicate<ThingDefCountClass>)((ThingDefCountClass x) => ((BuildableDef)x.thingDef).MadeFromStuff)) || (recipe.unfinishedThingDef != null && ((BuildableDef)recipe.unfinishedThingDef).MadeFromStuff))
		{
			return GenCollection.RandomElementByWeight<Thing>(ingredients.Where((Thing x) => x.def.IsStuff), (Func<Thing, float>)((Thing x) => x.stackCount));
		}
		return GenCollection.RandomElementByWeight<Thing>((IEnumerable<Thing>)ingredients, (Func<Thing, float>)((Thing x) => x.stackCount));
	}

	private static ThingStyleDef ComputeStyle(Bill bill, RecipeDef recipe)
	{
		if (!ModsConfig.IdeologyActive || recipe.products == null || recipe.products.Count != 1)
		{
			return null;
		}
		if (!bill.globalStyle)
		{
			return bill.style;
		}
		return Faction.OfPlayer.ideos.PrimaryIdeo.style.StyleForThingDef(recipe.ProducedThingDef, (Precept)null)?.styleDef;
	}
}
