using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_LoadPortalInBulk : JobDriver_LoadInBulkBase
{
	private const TargetIndex PortalInd = (TargetIndex)1;

	private MapPortal Portal
	{
		get
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = ((JobDriver)this).job.GetTarget((TargetIndex)1);
			Thing thing = ((LocalTargetInfo)(ref target)).Thing;
			return (MapPortal)(object)((thing is MapPortal) ? thing : null);
		}
	}

	protected override string ToilPrefix => "HD_Lpib";

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look<int>(ref loadIndex, "hdLpibLoadIndex", 0, false);
		Scribe_Values.Look<int>(ref depositLoops, "hdLpibDepositLoops", 0, false);
		Scribe_Values.Look<int>(ref passes, "hdLpibPasses", 0, false);
	}

	public override string GetReport()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.LoadPortal.Report"));
	}

	protected override IManagedLoadable BuildLoadable()
	{
		MapPortal portal = Portal;
		if (portal == null)
		{
			return null;
		}
		return MapPortalBulkTarget.TryCreate(portal);
	}

	protected override bool FindTargetStillValid()
	{
		MapPortal portal = Portal;
		if (portal != null)
		{
			return ((Thing)portal).Spawned;
		}
		return false;
	}

	protected override void DepositOne(Thing thing, ThingOwner inner, CompHauledToInventory hcomp, IManagedLoadable adp, ref bool movedAny)
	{
		MapPortal portal = Portal;
		if (portal == null || !((Thing)portal).Spawned)
		{
			return;
		}
		ThingOwner directlyHeldThings = portal.GetDirectlyHeldThings();
		if (directlyHeldThings == null)
		{
			return;
		}
		int val = PortalRemainingFor(portal, thing);
		int num = Math.Min(InventorySurplus.SurplusOf(((JobDriver)this).pawn, thing), val);
		if (num <= 0)
		{
			return;
		}
		ThingDef def = thing.def;
		int stackCount = thing.stackCount;
		Global.IsExecutingManagedPortalUnload = true;
		try
		{
			Thing val2 = default(Thing);
			inner.TryTransferToContainer(thing, directlyHeldThings, num, ref val2, false);
		}
		finally
		{
			Global.IsExecutingManagedPortalUnload = false;
		}
		bool flag = thing.Destroyed || !inner.Contains(thing);
		int num2 = stackCount - ((!flag) ? thing.stackCount : 0);
		if (num2 > 0)
		{
			movedAny = true;
			HaulersDreamGameComponent.Instance?.LoadNotifyDeposited(((JobDriver)this).pawn, adp, def, num2);
			if (flag)
			{
				hcomp.Deregister(thing);
			}
		}
	}

	protected override bool HasDepositable()
	{
		return HasDepositableForPortal();
	}

	private static int PortalRemainingFor(MapPortal portal, Thing item)
	{
		List<TransferableOneWay> list = portal?.leftToLoad;
		if (list == null || item?.def == null)
		{
			return 0;
		}
		TransferableOneWay obj = TransferableUtility.TransferableMatchingDesperate(item, list, (TransferAsOneMode)1);
		int num = ((obj != null) ? ((Transferable)obj).CountToTransfer : 0);
		if (num <= 0)
		{
			return 0;
		}
		return num;
	}

	private bool HasDepositableForPortal()
	{
		MapPortal portal = Portal;
		CompHauledToInventory comp = ((ThingWithComps)((JobDriver)this).pawn).GetComp<CompHauledToInventory>();
		ThingOwner<Thing> val = ((JobDriver)this).pawn.inventory?.innerContainer;
		if (portal == null || comp == null || val == null)
		{
			return false;
		}
		foreach (Thing item in comp.GetHashSet())
		{
			if (item != null && !item.Destroyed && ((ThingOwner)val).Contains(item) && InventorySurplus.SurplusOf(((JobDriver)this).pawn, item) > 0 && PortalRemainingFor(portal, item) > 0)
			{
				return true;
			}
		}
		return false;
	}
}
