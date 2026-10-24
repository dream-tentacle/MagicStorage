using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_LoadTransportersInBulk : JobDriver_LoadInBulkBase
{
	private const TargetIndex TransporterInd = (TargetIndex)1;

	private CompTransporter Transporter
	{
		get
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = ((JobDriver)this).job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			if (thing == null)
			{
				return null;
			}
			return ThingCompUtility.TryGetComp<CompTransporter>(thing);
		}
	}

	private static int AiUpdateInterval => Mathf.Max(10, JobDriver_LoadInBulkBase.Settings?.bulkLoadAiUpdateFrequency ?? 60);

	protected override string ToilPrefix => "HD_Ltib";

	internal int RevalidateInterval => AiUpdateInterval;

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look<int>(ref loadIndex, "hdLtibLoadIndex", 0, false);
		Scribe_Values.Look<int>(ref depositLoops, "hdLtibDepositLoops", 0, false);
		Scribe_Values.Look<int>(ref passes, "hdLtibPasses", 0, false);
	}

	public override string GetReport()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.LoadTransporter.Report"));
	}

	protected override IManagedLoadable BuildLoadable()
	{
		CompTransporter transporter = Transporter;
		if (transporter == null)
		{
			return null;
		}
		return LoadTransportersAdapter.TryCreate(transporter);
	}

	protected override bool FindTargetStillValid()
	{
		CompTransporter transporter = Transporter;
		if (transporter != null && ((ThingComp)transporter).parent != null)
		{
			return ((Thing)((ThingComp)transporter).parent).Spawned;
		}
		return false;
	}

	protected override void OnPreDepositLoop(IManagedLoadable adp)
	{
		TransportLoadTargetRedirect.ValidateAndRedirectCurrentTarget(this, (LoadTransportersAdapter)adp);
	}

	protected override void DepositOne(Thing thing, ThingOwner inner, CompHauledToInventory hcomp, IManagedLoadable adp, ref bool movedAny)
	{
		LoadTransportersAdapter loadTransportersAdapter = (LoadTransportersAdapter)adp;
		CompTransporter val = loadTransportersAdapter.ActiveMemberFor(thing);
		if (val == null)
		{
			return;
		}
		int val2 = LoadTransportersAdapter.MemberRemainingFor(val, thing);
		int num = Math.Min(InventorySurplus.SurplusOf(((JobDriver)this).pawn, thing), val2);
		if (num <= 0)
		{
			return;
		}
		ThingOwner innerContainer = val.innerContainer;
		if (innerContainer == null)
		{
			return;
		}
		Global.IsExecutingManagedUnload = true;
		int num2;
		try
		{
			Thing val3 = default(Thing);
			num2 = inner.TryTransferToContainer(thing, innerContainer, num, ref val3, false);
		}
		finally
		{
			Global.IsExecutingManagedUnload = false;
		}
		if (num2 > 0)
		{
			movedAny = true;
			HaulersDreamGameComponent.Instance?.LoadNotifyDeposited(((JobDriver)this).pawn, loadTransportersAdapter, thing.def, num2);
			if (!inner.Contains(thing))
			{
				hcomp.Deregister(thing);
			}
		}
	}

	protected override bool HasDepositable()
	{
		return HasDepositableForGroup();
	}

	private static int GroupRemainingFor(LoadTransportersAdapter adp, Thing item)
	{
		if (adp == null || item?.def == null)
		{
			return 0;
		}
		int num = 0;
		IReadOnlyList<CompTransporter> readOnlyList = adp.Group;
		for (int i = 0; i < readOnlyList.Count; i++)
		{
			num += LoadTransportersAdapter.MemberRemainingFor(readOnlyList[i], item);
		}
		return num;
	}

	private bool HasDepositableForGroup()
	{
		CompHauledToInventory comp = ((ThingWithComps)((JobDriver)this).pawn).GetComp<CompHauledToInventory>();
		ThingOwner<Thing> val = ((JobDriver)this).pawn.inventory?.innerContainer;
		LoadTransportersAdapter loadTransportersAdapter = EnsureAdapter() as LoadTransportersAdapter;
		if (comp == null || val == null || loadTransportersAdapter == null)
		{
			return false;
		}
		foreach (Thing item in comp.GetHashSet())
		{
			if (item != null && !item.Destroyed && ((ThingOwner)val).Contains(item) && InventorySurplus.SurplusOf(((JobDriver)this).pawn, item) > 0 && GroupRemainingFor(loadTransportersAdapter, item) > 0)
			{
				return true;
			}
		}
		return false;
	}
}
