using System;
using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_UnloadCarrierInBulk : JobDriver
{
	private const TargetIndex CarrierInd = (TargetIndex)1;

	private const TargetIndex ItemInd = (TargetIndex)3;

	private int pullLoops;

	private const int MaxPullLoops = 256;

	private const int AiUpdateInterval = 60;

	[NonSerialized]
	private Thing handTail;

	[ThreadStatic]
	private static List<BulkUnloadCarrierPolicy.CarrierStack> scratchStacks;

	[NonSerialized]
	private bool pendingToHands;

	private Pawn Carrier
	{
		get
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			return (Pawn)(object)((thing is Pawn) ? thing : null);
		}
	}

	private static HaulersDreamSettings Settings => HaulersDreamMod.Settings;

	public override void ExposeData()
	{
		((JobDriver)this).ExposeData();
		Scribe_Values.Look<int>(ref pullLoops, "hdUcibPullLoops", 0, false);
	}

	public override string GetReport()
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		Pawn carrier = Carrier;
		return TaggedString.op_Implicit((carrier != null) ? TranslatorFormattedStringExtensions.Translate("HaulersDream.UnloadCarrier.Report", NamedArgument.op_Implicit(((Entity)carrier).LabelShort)) : TranslatorFormattedStringExtensions.Translate("HaulersDream.UnloadCarrier.Report", NamedArgument.op_Implicit("")));
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = Settings;
		if (settings == null || !settings.reserveCarrierOnUnload)
		{
			return true;
		}
		return ReservationUtility.Reserve(base.pawn, LocalTargetInfo.op_Implicit((Thing)(object)Carrier), base.job, 1, -1, (ReservationLayerDef)null, errorOnFailed, false);
	}

	public override void Notify_Starting()
	{
		((JobDriver)this).Notify_Starting();
		Pawn carrier = Carrier;
		if (carrier?.inventory != null && BulkUnloadGate.PlayerMayUnload(base.pawn, carrier))
		{
			carrier.inventory.UnloadEverything = true;
		}
	}

	public override IEnumerable<Toil> MakeNewToils()
	{
		ToilFailConditions.FailOnDespawnedOrNull<JobDriver_UnloadCarrierInBulk>(this, (TargetIndex)1);
		ToilFailConditions.FailOnForbidden<JobDriver_UnloadCarrierInBulk>(this, (TargetIndex)1);
		bool mayUnload = BulkUnloadGate.PlayerMayUnload(base.pawn, Carrier);
		ToilFailConditions.FailOn<JobDriver_UnloadCarrierInBulk>(this, (Func<bool>)(() => !mayUnload));
		ToilFailConditions.FailOn<JobDriver_UnloadCarrierInBulk>(this, (Func<bool>)(() => Gen.IsHashIntervalTick((Thing)(object)base.pawn, 60) && AnotherPawnClaims(Carrier)));
		yield return Toils_Goto.GotoThing((TargetIndex)1, (PathEndMode)2, false);
		Toil selectNext = ToilMaker.MakeToil("HD_Ucib_SelectNext");
		Toil finalize = ToilMaker.MakeToil("HD_Ucib_Finalize");
		selectNext.initAction = delegate
		{
			//IL_0251: Unknown result type (might be due to invalid IL or missing references)
			if (++pullLoops > 256)
			{
				((JobDriver)this).JumpToToil(finalize);
			}
			else
			{
				Pawn carrier = Carrier;
				if (carrier == null || !((Thing)carrier).Spawned || carrier.Dead || carrier.inventory == null)
				{
					((JobDriver)this).JumpToToil(finalize);
				}
				else
				{
					ThingOwner<Thing> innerContainer = carrier.inventory.innerContainer;
					if (innerContainer == null || ((ThingOwner)innerContainer).Count == 0)
					{
						((JobDriver)this).JumpToToil(finalize);
					}
					else
					{
						List<BulkUnloadCarrierPolicy.CarrierStack> list = scratchStacks ?? (scratchStacks = new List<BulkUnloadCarrierPolicy.CarrierStack>());
						list.Clear();
						for (int i = 0; i < ((ThingOwner)innerContainer).Count; i++)
						{
							Thing val2 = innerContainer[i];
							if (val2 != null && !val2.Destroyed)
							{
								list.Add(new BulkUnloadCarrierPolicy.CarrierStack(i, StatExtension.GetStatValue(val2, StatDefOf.Mass, true, -1), val2.stackCount));
							}
						}
						float freeSpaceKg = MassUtility.FreeSpace(base.pawn);
						BulkUnloadCarrierPolicy.PullPlan pullPlan = BulkUnloadCarrierPolicy.PlanNextPull(list, freeSpaceKg);
						if (pullPlan.ChosenIndex < 0 || pullPlan.ChosenIndex >= ((ThingOwner)innerContainer).Count || pullPlan.Count <= 0)
						{
							((JobDriver)this).JumpToToil(finalize);
						}
						else
						{
							Thing val3 = innerContainer[pullPlan.ChosenIndex];
							if (val3 == null || val3.Destroyed)
							{
								((JobDriver)this).JumpToToil(selectNext);
							}
							else
							{
								int num2 = pullPlan.Count;
								bool toHands = pullPlan.ToHands;
								if (!toHands)
								{
									int num3 = CECompat.MaxFitCount(base.pawn, val3);
									if (num3 <= 0)
									{
										pullPlan = BulkUnloadCarrierPolicy.PlanNextPull(list, 0f);
										if (pullPlan.ChosenIndex < 0 || pullPlan.ChosenIndex >= ((ThingOwner)innerContainer).Count || pullPlan.Count <= 0)
										{
											((JobDriver)this).JumpToToil(finalize);
											return;
										}
										val3 = innerContainer[pullPlan.ChosenIndex];
										if (val3 == null || val3.Destroyed)
										{
											((JobDriver)this).JumpToToil(selectNext);
											return;
										}
										num2 = pullPlan.Count;
										toHands = pullPlan.ToHands;
									}
									else
									{
										num2 = Math.Min(num2, num3);
									}
								}
								base.job.SetTarget((TargetIndex)3, LocalTargetInfo.op_Implicit(val3));
								base.job.count = num2;
								pendingToHands = toHands;
							}
						}
					}
				}
			}
		};
		selectNext.defaultCompleteMode = (ToilCompleteMode)1;
		yield return selectNext;
		int num = Settings?.visualUnloadDelay ?? 0;
		yield return (num > 0) ? Toils_General.Wait(num, (TargetIndex)0) : Toils_General.Label();
		Toil val = ToilMaker.MakeToil("HD_Ucib_Transfer");
		val.initAction = delegate
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			Pawn carrier = Carrier;
			LocalTargetInfo target = base.job.GetTarget((TargetIndex)3);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (carrier == null || carrier.inventory == null || thing == null || thing.Destroyed)
			{
				((JobDriver)this).JumpToToil(selectNext);
			}
			else
			{
				ThingOwner<Thing> innerContainer = carrier.inventory.innerContainer;
				if (innerContainer == null || !((ThingOwner)innerContainer).Contains(thing))
				{
					((JobDriver)this).JumpToToil(selectNext);
				}
				else
				{
					int val2 = ((base.job.count > 0) ? base.job.count : thing.stackCount);
					val2 = Math.Min(val2, thing.stackCount);
					if (val2 <= 0)
					{
						((JobDriver)this).JumpToToil(finalize);
					}
					else if (pendingToHands)
					{
						Thing val3 = default(Thing);
						if (innerContainer.TryTransferToContainer(thing, (ThingOwner)(object)base.pawn.carryTracker.innerContainer, val2, ref val3, true) > 0 && val3 != null)
						{
							handTail = val3;
						}
						AfterTransferRefresh(carrier);
						((JobDriver)this).JumpToToil(finalize);
					}
					else
					{
						ThingOwner<Thing> val4 = base.pawn.inventory?.innerContainer;
						Thing val5 = default(Thing);
						if (val4 == null)
						{
							((JobDriver)this).JumpToToil(finalize);
						}
						else if (innerContainer.TryTransferToContainer(thing, (ThingOwner)(object)val4, val2, ref val5, false) > 0 && val5 != null)
						{
							((ThingWithComps)base.pawn).GetComp<CompHauledToInventory>()?.RegisterHauledItem(val5);
							AfterTransferRefresh(carrier);
							((JobDriver)this).JumpToToil(selectNext);
						}
						else
						{
							((JobDriver)this).JumpToToil(finalize);
						}
					}
				}
			}
		};
		val.defaultCompleteMode = (ToilCompleteMode)1;
		yield return val;
		finalize.initAction = delegate
		{
			if (handTail != null && !handTail.Destroyed)
			{
				Pawn_CarryTracker carryTracker = base.pawn.carryTracker;
				if (carryTracker != null)
				{
					ThingOwner<Thing> innerContainer = carryTracker.innerContainer;
					if (((innerContainer != null) ? new bool?(((ThingOwner)innerContainer).Contains(handTail)) : ((bool?)null)) == true)
					{
						Job val2 = HaulAIUtility.HaulToStorageJob(base.pawn, handTail, true);
						if (val2 != null && base.pawn.jobs != null)
						{
							val2.playerForced = base.job.playerForced;
							base.pawn.jobs.jobQueue.EnqueueFirst(val2, (JobTag?)(JobTag)0);
						}
					}
				}
			}
			PawnUnloadChecker.CheckIfShouldUnload(base.pawn, forced: true, behindQueuedWork: true);
			((JobDriver)this).EndJobWith((JobCondition)2);
		};
		finalize.defaultCompleteMode = (ToilCompleteMode)1;
		yield return finalize;
	}

	private bool AnotherPawnClaims(Pawn carrier)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		if (carrier != null)
		{
			Pawn pawn = base.pawn;
			if (((pawn == null) ? null : ((Thing)pawn).Map?.reservationManager) != null)
			{
				List<Reservation> reservationsReadOnly = ((Thing)base.pawn).Map.reservationManager.ReservationsReadOnly;
				if (reservationsReadOnly == null)
				{
					return false;
				}
				for (int i = 0; i < reservationsReadOnly.Count; i++)
				{
					Reservation val = reservationsReadOnly[i];
					if (val == null)
					{
						continue;
					}
					Pawn claimant = val.Claimant;
					if (claimant != null && claimant != base.pawn && !claimant.Dead)
					{
						LocalTargetInfo target = val.Target;
						if ((object)((LocalTargetInfo)(ref target)).Thing == carrier)
						{
							return true;
						}
					}
				}
				return false;
			}
		}
		return false;
	}

	private static void AfterTransferRefresh(Pawn carrier)
	{
		if (carrier == null || !carrier.RaceProps.packAnimal)
		{
			return;
		}
		Pawn_InventoryTracker inventory = carrier.inventory;
		if (inventory != null)
		{
			ThingOwner<Thing> innerContainer = inventory.innerContainer;
			if (((innerContainer != null) ? new int?(((ThingOwner)innerContainer).Count) : ((int?)null)) == 0)
			{
				carrier.Drawer.renderer.SetAllGraphicsDirty();
			}
		}
	}
}
