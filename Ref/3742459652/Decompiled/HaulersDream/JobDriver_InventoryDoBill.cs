using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_InventoryDoBill : JobDriver_DoBill
{
	private int loadIndex;

	private ThingDef loadDef;

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

	public override bool IsContinuation(Job j)
	{
		return false;
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		((JobDriver)this).AddEndCondition((Func<JobCondition>)(() => (JobCondition)4));
		((JobDriver)this).AddEndCondition((Func<JobCondition>)delegate
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = ((JobDriver)this).GetActor().jobs.curJob.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			return (thing is Building && !thing.Spawned) ? ((JobCondition)4) : ((JobCondition)1);
		});
		ToilFailConditions.FailOnBurningImmobile<JobDriver_InventoryDoBill>(this, (TargetIndex)1);
		ToilFailConditions.FailOn<JobDriver_InventoryDoBill>(this, (Func<bool>)delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = ((JobDriver)this).job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			IBillGiver val4 = (IBillGiver)(object)((thing is IBillGiver) ? thing : null);
			if (val4 != null)
			{
				if (((JobDriver)this).job.bill == null || ((JobDriver)this).job.bill.DeletedOrDereferenced)
				{
					return true;
				}
				if (!val4.CurrentlyUsableForBills())
				{
					return true;
				}
			}
			return false;
		});
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate
		{
			RegisterLeftoverIngredientsForUnload();
		});
		Toil gotoBillGiver = Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)4, false);
		Toil val = ToilMaker.MakeToil("HD_InvBill_Start");
		val.initAction = delegate
		{
			((JobDriver)this).EndJobWith((JobCondition)4);
		};
		val.defaultCompleteMode = (ToilCompleteMode)1;
		yield return val;
		Toil collectStart = ToilMaker.MakeToil("HD_InvBill_CollectStart");
		collectStart.initAction = delegate
		{
		};
		collectStart.defaultCompleteMode = (ToilCompleteMode)1;
		Toil loadDecide = ToilMaker.MakeToil("HD_InvBill_LoadDecide");
		loadDecide.initAction = delegate
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_010c: Unknown result type (might be due to invalid IL or missing references)
			//IL_019c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0132: Unknown result type (might be due to invalid IL or missing references)
			List<LocalTargetInfo> targetQueueB = ((JobDriver)this).job.targetQueueB;
			if (targetQueueB == null)
			{
				((JobDriver)this).JumpToToil(collectStart);
			}
			else
			{
				LocalTargetInfo val4;
				while (loadIndex < targetQueueB.Count)
				{
					val4 = targetQueueB[loadIndex];
					Thing thing = ((LocalTargetInfo)(ref val4)).Thing;
					if (thing != null && thing.Spawned && !ForbidUtility.IsForbidden(thing, ((JobDriver)this).pawn) && !(thing.ParentHolder is Pawn_InventoryTracker))
					{
						break;
					}
					loadIndex++;
				}
				if (loadIndex >= targetQueueB.Count)
				{
					((JobDriver)this).JumpToToil(collectStart);
				}
				else
				{
					val4 = targetQueueB[loadIndex];
					Thing thing2 = ((LocalTargetInfo)(ref val4)).Thing;
					if (OverloadGate.CountToPickUp(((JobDriver)this).pawn, thing2, HaulersDreamMod.Settings) <= 0)
					{
						((JobDriver)this).JumpToToil(collectStart);
					}
					else if (!ReservationUtility.CanReserve(((JobDriver)this).pawn, LocalTargetInfo.op_Implicit(thing2), 1, -1, (ReservationLayerDef)null, false) && !((Thing)((JobDriver)this).pawn).Map.reservationManager.ReservedBy(LocalTargetInfo.op_Implicit(thing2), ((JobDriver)this).pawn, ((JobDriver)this).job))
					{
						loadIndex++;
						((JobDriver)this).JumpToToil(loadDecide);
					}
					else
					{
						loadDef = thing2.def;
						((JobDriver)this).job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(thing2));
					}
				}
			}
		};
		loadDecide.defaultCompleteMode = (ToilCompleteMode)1;
		yield return loadDecide;
		Toil val2 = ToilMaker.MakeToil("HD_InvBill_LoadGoto");
		val2.initAction = delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = ((JobDriver)this).job.GetTarget((TargetIndex)2);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing == null || !thing.Spawned)
			{
				loadIndex++;
				((JobDriver)this).JumpToToil(loadDecide);
			}
			else
			{
				((JobDriver)this).pawn.pather.StartPath(LocalTargetInfo.op_Implicit(thing), (PathEndMode)3);
			}
		};
		val2.defaultCompleteMode = (ToilCompleteMode)2;
		yield return val2;
		yield return Toils_Haul.TakeToInventory((TargetIndex)2, (Func<int>)delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = ((JobDriver)this).job.GetTarget((TargetIndex)2);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing == null || !thing.Spawned)
			{
				return 0;
			}
			int num = ((((JobDriver)this).job.countQueue != null && loadIndex < ((JobDriver)this).job.countQueue.Count) ? ((JobDriver)this).job.countQueue[loadIndex] : thing.stackCount);
			if (num <= 0)
			{
				return 0;
			}
			HaulersDreamSettings settings = HaulersDreamMod.Settings;
			if (settings != null && OverloadGate.NoOverload(settings))
			{
				num = Mathf.Min(num, OverloadGate.CountToPickUp(((JobDriver)this).pawn, thing, settings));
			}
			return Mathf.Min(num, thing.stackCount);
		});
		Toil val3 = ToilMaker.MakeToil("HD_InvBill_Relink");
		val3.initAction = delegate
		{
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			List<LocalTargetInfo> targetQueueB = ((JobDriver)this).job.targetQueueB;
			if (targetQueueB != null && loadIndex < targetQueueB.Count && loadDef != null)
			{
				Thing val4 = YieldRouter.InventoryStackOfDef(Inv, loadDef);
				if (val4 != null)
				{
					targetQueueB[loadIndex] = LocalTargetInfo.op_Implicit(val4);
				}
			}
			loadIndex++;
			((JobDriver)this).JumpToToil(loadDecide);
		};
		val3.defaultCompleteMode = (ToilCompleteMode)1;
		yield return val3;
		yield return collectStart;
		yield return Toils_Jump.JumpIf(gotoBillGiver, (Func<bool>)(() => GenList.NullOrEmpty<LocalTargetInfo>((IList<LocalTargetInfo>)((JobDriver)this).job.GetTargetQueue((TargetIndex)2))));
		foreach (Toil item in JobDriver_DoBill.CollectIngredientsToils((TargetIndex)2, (TargetIndex)1, (TargetIndex)3, false, true, ((JobDriver_DoBill)this).BillGiver is Building_WorkTableAutonomous))
		{
			yield return item;
		}
		yield return gotoBillGiver;
		yield return Toils_Recipe.MakeUnfinishedThingIfNeeded();
		yield return ToilFailConditions.FailOnCannotTouch<Toil>(ToilFailConditions.FailOnDespawnedNullOrForbiddenPlacedThings(Toils_Recipe.DoRecipeWork(), (TargetIndex)1), (TargetIndex)1, (PathEndMode)4);
		yield return Toils_Recipe.CheckIfRecipeCanFinishNow();
		yield return FinishRecipeIntoInventory();
	}

	private Toil FinishRecipeIntoInventory()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		Toil obj = ToilMaker.MakeToil("HD_InvBill_Finish");
		obj.initAction = delegate
		{
			Pawn pawn = ((JobDriver)this).pawn;
			Map map = ((Thing)pawn).Map;
			Job curJob = pawn.jobs.curJob;
			RecipeDef val = curJob.bill?.recipe;
			if (val == null)
			{
				pawn.jobs.EndCurrentJob((JobCondition)64, true, true);
			}
			else
			{
				if (val.workSkill != null && !val.UsesUnfinishedThing && pawn.skills != null)
				{
					float num = (float)base.ticksSpentDoingRecipeWork * 0.1f * val.workSkillLearnFactor;
					pawn.skills.GetSkill(val.workSkill).Learn(num, false, false);
				}
				List<Thing> list = CalculateIngredients(curJob, pawn);
				Thing val2 = CalculateDominantIngredient(curJob, list);
				ThingStyleDef val3 = ComputeStyle(curJob.bill, val);
				List<Thing> list2 = GenRecipe.MakeRecipeProducts(val, pawn, list, val2, ((JobDriver_DoBill)this).BillGiver, curJob.bill.precept, val3, curJob.bill.graphicIndexOverride).ToList();
				ConsumeIngredients(list, val, map);
				curJob.bill.Notify_IterationCompleted(pawn, list);
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
				for (int i = 0; i < list2.Count; i++)
				{
					PlaceProductIntoInventory(list2[i]);
				}
				pawn.jobs.EndCurrentJob((JobCondition)2, true, true);
			}
		};
		obj.defaultCompleteMode = (ToilCompleteMode)1;
		return obj;
	}

	private static List<Thing> CalculateIngredients(Job job, Pawn actor)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		LocalTargetInfo target = job.GetTarget((TargetIndex)2);
		Thing thing = ((LocalTargetInfo)(ref target)).Thing;
		UnfinishedThing val = (UnfinishedThing)(object)((thing is UnfinishedThing) ? thing : null);
		if (val != null)
		{
			List<Thing> ingredients = val.ingredients;
			job.RecipeDef.Worker.ConsumeIngredient((Thing)(object)val, job.RecipeDef, ((Thing)actor).Map);
			job.placedThings = null;
			return ingredients;
		}
		List<Thing> list = new List<Thing>();
		if (job.placedThings != null)
		{
			for (int i = 0; i < job.placedThings.Count; i++)
			{
				ThingCountClass val2 = job.placedThings[i];
				if (val2.Count <= 0)
				{
					continue;
				}
				Thing val3 = ((val2.Count >= val2.thing.stackCount) ? val2.thing : val2.thing.SplitOff(val2.Count));
				val2.Count = 0;
				if (list.Contains(val3))
				{
					continue;
				}
				list.Add(val3);
				if (job.RecipeDef.autoStripCorpses)
				{
					IStrippable val4 = (IStrippable)(object)((val3 is IStrippable) ? val3 : null);
					if (val4 != null && val4.AnythingToStrip())
					{
						val4.Strip(true);
					}
				}
			}
		}
		job.placedThings = null;
		return list;
	}

	private static Thing CalculateDominantIngredient(Job job, List<Thing> ingredients)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		LocalTargetInfo target = job.GetTarget((TargetIndex)2);
		Thing thing = ((LocalTargetInfo)(ref target)).Thing;
		UnfinishedThing uft = (UnfinishedThing)(object)((thing is UnfinishedThing) ? thing : null);
		if (uft != null && ((BuildableDef)((Thing)uft).def).MadeFromStuff)
		{
			return uft.ingredients.First((Thing ing) => ing.def == ((Thing)uft).Stuff);
		}
		if (GenList.NullOrEmpty<Thing>((IList<Thing>)ingredients))
		{
			return null;
		}
		RecipeDef recipeDef = job.RecipeDef;
		if (recipeDef.productHasIngredientStuff)
		{
			return ingredients[0];
		}
		if (GenCollection.Any<ThingDefCountClass>(recipeDef.products, (Predicate<ThingDefCountClass>)((ThingDefCountClass x) => ((BuildableDef)x.thingDef).MadeFromStuff)) || (recipeDef.unfinishedThingDef != null && ((BuildableDef)recipeDef.unfinishedThingDef).MadeFromStuff))
		{
			return GenCollection.RandomElementByWeight<Thing>(ingredients.Where((Thing x) => x.def.IsStuff), (Func<Thing, float>)((Thing x) => x.stackCount));
		}
		return GenCollection.RandomElementByWeight<Thing>((IEnumerable<Thing>)ingredients, (Func<Thing, float>)((Thing x) => x.stackCount));
	}

	private static void ConsumeIngredients(List<Thing> ingredients, RecipeDef recipe, Map map)
	{
		for (int i = 0; i < ingredients.Count; i++)
		{
			recipe.Worker.ConsumeIngredient(ingredients[i], recipe, map);
		}
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

	private void PlaceProductIntoInventory(Thing product)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
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
			Thing val = ((product.def.stackLimit == 1) ? product : (YieldRouter.InventoryStackOfDef(inv, product.def) ?? (flag ? product : null)));
			if (val != null)
			{
				comp.RegisterHauledItem(val);
			}
			comp.NotifyYieldPicked();
		}
		if (!flag && product.stackCount > 0 && !product.Destroyed)
		{
			GenPlace.TryPlaceThing(product, ((Thing)((JobDriver)this).pawn).Position, ((Thing)((JobDriver)this).pawn).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
		}
	}

	private void RegisterLeftoverIngredientsForUnload()
	{
		CompHauledToInventory comp = Comp;
		ThingOwner inv = Inv;
		RecipeDef val = ((JobDriver)this).job.bill?.recipe;
		if (comp == null || inv == null || val?.ingredients == null)
		{
			return;
		}
		HashSet<ThingDef> hashSet = new HashSet<ThingDef>();
		for (int i = 0; i < val.ingredients.Count; i++)
		{
			foreach (ThingDef allowedThingDef in val.ingredients[i].filter.AllowedThingDefs)
			{
				hashSet.Add(allowedThingDef);
			}
		}
		for (int j = 0; j < inv.Count; j++)
		{
			Thing val2 = inv[j];
			if (val2 != null && hashSet.Contains(val2.def))
			{
				comp.RegisterHauledItem(val2);
			}
		}
	}
}
