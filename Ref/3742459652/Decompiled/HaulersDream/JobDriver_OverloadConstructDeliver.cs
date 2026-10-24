using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_OverloadConstructDeliver : JobDriver
{
	private const TargetIndex ResourceInd = (TargetIndex)1;

	private const TargetIndex NeederInd = (TargetIndex)2;

	private const TargetIndex PrimaryNeederInd = (TargetIndex)3;

	private ThingDef resourceDef;

	private int loadTargetUnits;

	private bool madeProgress;

	private Toil loadGotoToil;

	private Toil loadDecideToil;

	private Thing Needer
	{
		get
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)2);
			return ((LocalTargetInfo)(ref target)).Thing;
		}
	}

	private ThingOwner Inv
	{
		get
		{
			Pawn_InventoryTracker inventory = base.pawn.inventory;
			if (inventory == null)
			{
				return null;
			}
			return inventory.GetDirectlyHeldThings();
		}
	}

	public override void ExposeData()
	{
		((JobDriver)this).ExposeData();
		Scribe_Defs.Look<ThingDef>(ref resourceDef, "hdResourceDef");
		Scribe_Values.Look<int>(ref loadTargetUnits, "hdLoadTargetUnits", 0, false);
		Scribe_Values.Look<bool>(ref madeProgress, "hdMadeProgress", false, false);
	}

	public override void Notify_PatherFailed()
	{
		if (((JobDriver)this).CurToil != loadGotoToil)
		{
			((JobDriver)this).Notify_PatherFailed();
		}
		else
		{
			((JobDriver)this).JumpToToil(loadDecideToil);
		}
	}

	public override string GetReport()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		Thing needer = Needer;
		if (resourceDef == null || needer == null)
		{
			return TaggedString.op_Implicit(Translator.Translate("ReportHaulingUnknown"));
		}
		return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("ReportHaulingTo", NamedArgument.op_Implicit(((Def)resourceDef).label), NamedArgumentUtility.Named((object)((Entity)needer).LabelShort, "DESTINATION"), NamedArgumentUtility.Named((object)resourceDef, "THING")));
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		LocalTargetInfo val;
		if (resourceDef == null)
		{
			object obj = ((LocalTargetInfo)(ref base.job.targetA)).Thing?.def;
			if (obj == null)
			{
				if (base.job.targetQueueA == null || base.job.targetQueueA.Count <= 0)
				{
					obj = null;
				}
				else
				{
					val = base.job.targetQueueA[0];
					obj = ((LocalTargetInfo)(ref val)).Thing?.def;
				}
			}
			resourceDef = (ThingDef)obj;
		}
		if (loadTargetUnits <= 0)
		{
			loadTargetUnits = base.job.count;
		}
		if (((LocalTargetInfo)(ref base.job.targetA)).HasThing && !ReservationUtility.Reserve(base.pawn, base.job.targetA, base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false) && (resourceDef == null || InventoryCountOfDef() <= 0))
		{
			return false;
		}
		ReservationUtility.ReserveAsManyAsPossible(base.pawn, base.job.GetTargetQueue((TargetIndex)1), base.job, 1, -1, (ReservationLayerDef)null);
		Thing needer = Needer;
		IHaulEnroute val2 = (IHaulEnroute)(object)((needer is IHaulEnroute) ? needer : null);
		if (val2 != null && resourceDef != null && Needer.Spawned && ((Thing)base.pawn).Map != null)
		{
			if (ConstructDeliveryPlan.ShouldForcedTakeover(base.job.playerForced, EnrouteSafety.SpaceRemainingSafe(Needer, resourceDef, base.pawn)))
			{
				((Thing)base.pawn).Map.enrouteManager.InterruptEnroutePawns(val2, base.pawn);
			}
			int num = Mathf.Min(base.job.count, EnrouteSafety.SpaceRemainingSafe(Needer, resourceDef, base.pawn));
			if (num > 0)
			{
				((Thing)base.pawn).Map.enrouteManager.AddEnroute(val2, base.pawn, resourceDef, num);
			}
		}
		if (resourceDef != null && ((Thing)base.pawn).Map != null)
		{
			List<LocalTargetInfo> targetQueue = base.job.GetTargetQueue((TargetIndex)2);
			if (targetQueue != null)
			{
				for (int i = 0; i < targetQueue.Count; i++)
				{
					val = targetQueue[i];
					Thing thing = ((LocalTargetInfo)(ref val)).Thing;
					IHaulEnroute val3 = (IHaulEnroute)(object)((thing is IHaulEnroute) ? thing : null);
					if (val3 != null && thing.Spawned)
					{
						int num2 = EnrouteSafety.SpaceRemainingSafe(thing, resourceDef, base.pawn);
						if (num2 > 0)
						{
							((Thing)base.pawn).Map.enrouteManager.AddEnroute(val3, base.pawn, resourceDef, num2);
						}
					}
				}
			}
		}
		return true;
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		Thing needer = Needer;
		IntVec3 neederCell = ((needer != null) ? needer.Position : IntVec3.Invalid);
		((JobDriver)this).AddFinishAction((Action<JobCondition>)delegate
		{
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
			Thing needer2 = Needer;
			IHaulEnroute val2 = (IHaulEnroute)(object)((needer2 is IHaulEnroute) ? needer2 : null);
			if (val2 != null)
			{
				Map map = ((Thing)base.pawn).Map;
				if (map != null)
				{
					EnrouteManager enrouteManager = map.enrouteManager;
					if (enrouteManager != null)
					{
						enrouteManager.ReleaseFor(val2, base.pawn);
					}
				}
			}
			List<LocalTargetInfo> targetQueue = base.job.GetTargetQueue((TargetIndex)2);
			if (targetQueue != null)
			{
				for (int i = 0; i < targetQueue.Count; i++)
				{
					LocalTargetInfo val3 = targetQueue[i];
					Thing thing = ((LocalTargetInfo)(ref val3)).Thing;
					IHaulEnroute val4 = (IHaulEnroute)(object)((thing is IHaulEnroute) ? thing : null);
					if (val4 != null)
					{
						Map map2 = ((Thing)base.pawn).Map;
						if (map2 != null)
						{
							EnrouteManager enrouteManager2 = map2.enrouteManager;
							if (enrouteManager2 != null)
							{
								enrouteManager2.ReleaseFor(val4, base.pawn);
							}
						}
					}
				}
			}
			if (base.job.def == HaulersDreamDefOf.HaulersDream_ConstructDeliverBuild && base.job.playerForced && ((IntVec3)(ref neederCell)).IsValid)
			{
				ConstructTether.QueueNext(base.pawn, neederCell, madeProgress);
			}
			RegisterLeftoverForUnload();
		});
		ToilFailConditions.FailOn<JobDriver_OverloadConstructDeliver>(this, (Func<bool>)delegate
		{
			Thing needer2 = Needer;
			return (needer2 == null || needer2.Destroyed || !needer2.Spawned || ForbidUtility.IsForbidden(needer2, base.pawn)) && !HasMoreQueuedNeeders();
		});
		Toil deliverGoto = ((Needer is Blueprint || Needer is Frame) ? Toils_Goto.GotoBuild((TargetIndex)2) : Toils_Goto.GotoThing((TargetIndex)2, (PathEndMode)2, false));
		Toil val = ToilMaker.MakeToil("HD_LoadEntry");
		val.initAction = delegate
		{
			if (resourceDef == null)
			{
				((JobDriver)this).JumpToToil(deliverGoto);
			}
			else if (!ConstructDeliveryPlan.ShouldLoadBeforeDeliver(InventoryCountOfDef(), SpaceInNeeder()))
			{
				((JobDriver)this).JumpToToil(deliverGoto);
			}
		};
		val.defaultCompleteMode = (ToilCompleteMode)1;
		yield return val;
		Toil loadDecide = (loadDecideToil = ToilMaker.MakeToil("HD_LoadDecide"));
		loadDecide.initAction = delegate
		{
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			if (resourceDef == null)
			{
				((JobDriver)this).JumpToToil(deliverGoto);
			}
			else if (LoadTargetUnits() - InventoryCountOfDef() <= 0)
			{
				((JobDriver)this).JumpToToil(deliverGoto);
			}
			else if (OverloadHeadroomUnits() <= 0)
			{
				((JobDriver)this).JumpToToil(deliverGoto);
			}
			else
			{
				Thing val2 = NextResourceStack();
				if (val2 == null)
				{
					((JobDriver)this).JumpToToil(deliverGoto);
				}
				else
				{
					base.job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit(val2));
				}
			}
		};
		loadDecide.defaultCompleteMode = (ToilCompleteMode)1;
		yield return loadDecide;
		yield return loadGotoToil = SweepWalk.MakeToil((JobDriver)(object)this, (TargetIndex)1, "HD_LoadGoto", loadDecide, delegate
		{
		}, () => false);
		yield return Toils_Haul.TakeToInventory((TargetIndex)1, (Func<int>)delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing == null || !thing.Spawned)
			{
				return 0;
			}
			if (ForbidUtility.IsForbidden(thing, base.pawn) && !SweepForbidPolicy.MayTakeWhileForbidden(base.job.playerForced, isOrderedAnchor: false))
			{
				return 0;
			}
			int num = LoadTargetUnits() - InventoryCountOfDef();
			if (num > 0)
			{
				int num2 = OverloadGate.CountToPickUp(base.pawn, thing, HaulersDreamMod.Settings);
				int num3 = Mathf.Min(Mathf.Min(num, num2), thing.stackCount);
				if (num3 > 0)
				{
					madeProgress = true;
				}
				return num3;
			}
			return 0;
		});
		yield return Toils_Jump.Jump(loadDecide);
		yield return deliverGoto;
		Toil done = ToilMaker.MakeToil("HD_Done");
		done.initAction = delegate
		{
		};
		done.defaultCompleteMode = (ToilCompleteMode)1;
		Toil nextNeeder = ToilMaker.MakeToil("HD_NextNeeder");
		Toil deliverGotoNext = Toils_Goto.GotoBuild((TargetIndex)2);
		Toil deliverDecide = ToilMaker.MakeToil("HD_DeliverDecide");
		deliverDecide.initAction = delegate
		{
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			Thing needer2 = Needer;
			if (resourceDef == null || needer2 == null || needer2.Destroyed)
			{
				((JobDriver)this).JumpToToil(nextNeeder);
			}
			else
			{
				Thing val2 = InventoryStackOfDef();
				if (val2 == null)
				{
					((JobDriver)this).JumpToToil(done);
				}
				else
				{
					int num = SpaceInNeeder();
					if (num <= 0)
					{
						((JobDriver)this).JumpToToil(nextNeeder);
					}
					else
					{
						int num2 = Mathf.Min(Mathf.Min(num, val2.stackCount), base.pawn.carryTracker.MaxStackSpaceEver(resourceDef));
						if (num2 <= 0)
						{
							((JobDriver)this).JumpToToil(nextNeeder);
						}
						else
						{
							madeProgress = true;
							base.job.count = num2;
							base.job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit(val2));
						}
					}
				}
			}
		};
		deliverDecide.defaultCompleteMode = (ToilCompleteMode)1;
		yield return deliverDecide;
		yield return Toils_Haul.StartCarryThing((TargetIndex)1, false, false, false, false, true);
		yield return Toils_Construct.MakeSolidThingFromBlueprintIfNecessary((TargetIndex)2, (TargetIndex)3);
		yield return Toils_Haul.DepositHauledThingInContainer((TargetIndex)2, (TargetIndex)3, (Action)null);
		yield return Toils_Jump.Jump(deliverDecide);
		nextNeeder.initAction = delegate
		{
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01df: Unknown result type (might be due to invalid IL or missing references)
			//IL_0208: Unknown result type (might be due to invalid IL or missing references)
			//IL_021d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0248: Unknown result type (might be due to invalid IL or missing references)
			//IL_024d: Unknown result type (might be due to invalid IL or missing references)
			//IL_026e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0286: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
			Thing needer2 = Needer;
			IHaulEnroute val2 = (IHaulEnroute)(object)((needer2 is IHaulEnroute) ? needer2 : null);
			if (val2 != null)
			{
				Map map = ((Thing)base.pawn).Map;
				if (map != null)
				{
					EnrouteManager enrouteManager = map.enrouteManager;
					if (enrouteManager != null)
					{
						enrouteManager.ReleaseFor(val2, base.pawn);
					}
				}
			}
			if (InventoryStackOfDef() == null)
			{
				((JobDriver)this).JumpToToil(done);
			}
			else
			{
				List<LocalTargetInfo> targetQueue = base.job.GetTargetQueue((TargetIndex)2);
				if (targetQueue == null)
				{
					((JobDriver)this).JumpToToil(done);
				}
				else
				{
					LocalTargetInfo val3;
					for (int num = targetQueue.Count - 1; num >= 0; num--)
					{
						val3 = targetQueue[num];
						Thing thing = ((LocalTargetInfo)(ref val3)).Thing;
						int num2;
						int num3;
						if (thing != null && !thing.Destroyed && thing.Spawned && thing is IConstructible && !ForbidUtility.IsForbidden(thing, base.pawn))
						{
							num2 = (ReachabilityUtility.CanReach(base.pawn, LocalTargetInfo.op_Implicit(thing), (PathEndMode)2, (Danger)3, false, false, (TraverseMode)0) ? 1 : 0);
							if (num2 != 0)
							{
								num3 = EnrouteSafety.SpaceRemainingSafe(thing, resourceDef, base.pawn);
								goto IL_0123;
							}
						}
						else
						{
							num2 = 0;
						}
						num3 = 0;
						goto IL_0123;
						IL_0123:
						int num4 = num3;
						if (num2 == 0 || num4 <= 0)
						{
							IHaulEnroute val4 = (IHaulEnroute)(object)((thing is IHaulEnroute) ? thing : null);
							if (val4 != null)
							{
								Map map2 = ((Thing)base.pawn).Map;
								if (map2 != null)
								{
									EnrouteManager enrouteManager2 = map2.enrouteManager;
									if (enrouteManager2 != null)
									{
										enrouteManager2.ReleaseFor(val4, base.pawn);
									}
								}
							}
							targetQueue.RemoveAt(num);
						}
					}
					if (targetQueue.Count == 0)
					{
						((JobDriver)this).JumpToToil(done);
					}
					else
					{
						List<int> list = new List<int>(targetQueue.Count);
						List<int> list2 = new List<int>(targetQueue.Count);
						for (int i = 0; i < targetQueue.Count; i++)
						{
							val3 = targetQueue[i];
							IntVec3 position = ((LocalTargetInfo)(ref val3)).Thing.Position;
							list.Add(position.x);
							list2.Add(position.z);
						}
						int num5 = ConstructionBatchMath.NextNearestIndex(list, list2, ((Thing)base.pawn).Position.x, ((Thing)base.pawn).Position.z);
						if (num5 < 0)
						{
							((JobDriver)this).JumpToToil(done);
						}
						else
						{
							val3 = targetQueue[num5];
							Thing thing2 = ((LocalTargetInfo)(ref val3)).Thing;
							targetQueue.RemoveAt(num5);
							base.job.SetTarget((TargetIndex)2, LocalTargetInfo.op_Implicit(thing2));
							base.job.SetTarget((TargetIndex)3, LocalTargetInfo.op_Implicit(thing2));
							((JobDriver)this).JumpToToil(deliverGotoNext);
						}
					}
				}
			}
		};
		nextNeeder.defaultCompleteMode = (ToilCompleteMode)1;
		yield return nextNeeder;
		yield return deliverGotoNext;
		yield return Toils_Jump.Jump(deliverDecide);
		yield return done;
	}

	private bool HasMoreQueuedNeeders()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		List<LocalTargetInfo> targetQueue = base.job.GetTargetQueue((TargetIndex)2);
		if (targetQueue == null)
		{
			return false;
		}
		for (int i = 0; i < targetQueue.Count; i++)
		{
			LocalTargetInfo val = targetQueue[i];
			Thing thing = ((LocalTargetInfo)(ref val)).Thing;
			if (thing != null && !thing.Destroyed && thing.Spawned && thing is IConstructible && !ForbidUtility.IsForbidden(thing, base.pawn))
			{
				return true;
			}
		}
		return false;
	}

	private int SpaceInNeeder()
	{
		return EnrouteSafety.SpaceRemainingSafe(Needer, resourceDef, base.pawn);
	}

	private int LoadTargetUnits()
	{
		return Mathf.Max(SpaceInNeeder(), loadTargetUnits);
	}

	private bool MoreConstructWorkQueued()
	{
		return ConstructionMaterialHold.MoreConstructWorkQueued(base.pawn);
	}

	private Thing InventoryStackOfDef()
	{
		return YieldRouter.InventoryStackOfDef(Inv, resourceDef);
	}

	private int InventoryCountOfDef()
	{
		ThingOwner inv = Inv;
		if (inv == null || resourceDef == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < inv.Count; i++)
		{
			if (inv[i]?.def == resourceDef)
			{
				num += inv[i].stackCount;
			}
		}
		return num;
	}

	private int OverloadHeadroomUnits()
	{
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || resourceDef == null)
		{
			return 0;
		}
		int num = CECompat.FitUnitsByBulk(base.pawn, resourceDef);
		if (num <= 0)
		{
			return 0;
		}
		float maxCapacityKg = CarryCapacity.Of(base.pawn);
		float baseCapKg = CarryMath.EffectiveCapacity(maxCapacityKg, settings.carryLimitFraction);
		float currentMassKg = MassUtility.GearAndInventoryMass(base.pawn);
		float statValueAbstract = StatExtension.GetStatValueAbstract((BuildableDef)(object)resourceDef, StatDefOf.Mass, (ThingDef)null);
		return Mathf.Min(OverloadPolicy.UnitsToCarry(OverloadGate.NoOverloadFor(base.pawn, settings) ? 10 : settings.overloadLevel, maxCapacityKg, baseCapKg, currentMassKg, statValueAbstract, int.MaxValue, int.MaxValue), num);
	}

	private Thing NextResourceStack()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		List<LocalTargetInfo> targetQueue = base.job.GetTargetQueue((TargetIndex)1);
		while (targetQueue != null && targetQueue.Count > 0)
		{
			LocalTargetInfo val = targetQueue[0];
			Thing thing = ((LocalTargetInfo)(ref val)).Thing;
			targetQueue.RemoveAt(0);
			if (thing != null && thing.Spawned && !thing.Destroyed && !ForbidUtility.IsForbidden(thing, base.pawn) && (ReservationUtility.CanReserve(base.pawn, LocalTargetInfo.op_Implicit(thing), 1, -1, (ReservationLayerDef)null, false) || ((Thing)base.pawn).Map.reservationManager.ReservedBy(LocalTargetInfo.op_Implicit(thing), base.pawn, base.job)))
			{
				return thing;
			}
		}
		return null;
	}

	private void RegisterLeftoverForUnload()
	{
		if (resourceDef == null)
		{
			return;
		}
		CompHauledToInventory comp = ((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>();
		ThingOwner inv = Inv;
		if (comp == null || inv == null)
		{
			return;
		}
		bool flag = false;
		for (int i = 0; i < inv.Count; i++)
		{
			Thing val = inv[i];
			if (val != null && val.def == resourceDef)
			{
				comp.RegisterHauledItem(val);
				flag = true;
			}
		}
		if (flag)
		{
			comp.NotifyYieldPicked();
		}
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		bool flag2 = false;
		JobQueue val2 = base.pawn.jobs?.jobQueue;
		if (val2 != null)
		{
			for (int j = 0; j < val2.Count; j++)
			{
				if (val2[j]?.job == base.job)
				{
					flag2 = true;
					break;
				}
			}
		}
		if (flag && !flag2 && !MoreConstructWorkQueued() && settings != null && !settings.markForUnload)
		{
			PawnUnloadChecker.CheckIfShouldUnload(base.pawn, forced: true);
		}
	}
}
