using System;
using System.Collections;
using HaulersDream.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public class JobDriver_LoadVehicleInBulk : JobDriver_LoadInBulkBase
{
	private const TargetIndex VehicleInd = (TargetIndex)1;

	private Thing Vehicle
	{
		get
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			LocalTargetInfo target = ((JobDriver)this).job.GetTarget((TargetIndex)1);
			return ((LocalTargetInfo)(ref target)).Thing;
		}
	}

	protected override string ToilPrefix => "HD_Lvib";

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look<int>(ref loadIndex, "hdLvibLoadIndex", 0, false);
		Scribe_Values.Look<int>(ref depositLoops, "hdLvibDepositLoops", 0, false);
		Scribe_Values.Look<int>(ref passes, "hdLvibPasses", 0, false);
	}

	public override string GetReport()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate("HaulersDream.LoadVehicle.Report"));
	}

	protected override IManagedLoadable BuildLoadable()
	{
		Thing vehicle = Vehicle;
		if (vehicle == null)
		{
			return null;
		}
		return VehicleLoadTarget.TryCreate(vehicle);
	}

	protected override void OnExtraClaim()
	{
		VehicleFrameworkCompat.ReserveVehicle(Vehicle, ((JobDriver)this).pawn, ((JobDriver)this).job);
	}

	protected override void OnReleaseExtraClaims()
	{
		VehicleFrameworkCompat.ReleaseVehicleClaims(((JobDriver)this).pawn);
	}

	protected override bool FindTargetStillValid()
	{
		Thing vehicle = Vehicle;
		if (vehicle != null)
		{
			return vehicle.Spawned;
		}
		return false;
	}

	protected override void DepositOne(Thing thing, ThingOwner inner, CompHauledToInventory hcomp, IManagedLoadable adp, ref bool movedAny)
	{
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		Thing vehicle = Vehicle;
		int remaining = VehicleFrameworkCompat.RemainingDemandForThing(vehicle, thing);
		int num = VehicleLoadPlanPolicy.DepositUnits(InventorySurplus.SurplusOf(((JobDriver)this).pawn, thing), remaining);
		if (num <= 0)
		{
			return;
		}
		Thing val = inner.Take(thing, num);
		if (val == null)
		{
			return;
		}
		ThingDef def = val.def;
		Global.IsExecutingManagedUnload = true;
		int num2;
		try
		{
			num2 = VehicleFrameworkCompat.AddOrTransfer(vehicle, val, num);
		}
		finally
		{
			Global.IsExecutingManagedUnload = false;
		}
		if (num2 > 0)
		{
			movedAny = true;
			HaulersDreamGameComponent.Instance?.LoadNotifyDeposited(((JobDriver)this).pawn, adp, def, num2);
		}
		if (!val.Destroyed && !val.Spawned && val.ParentHolder == null)
		{
			if (inner.TryAdd(val, true))
			{
				hcomp.RegisterHauledItem(val);
			}
			else
			{
				GenPlace.TryPlaceThing(val, ((Thing)((JobDriver)this).pawn).Position, ((Thing)((JobDriver)this).pawn).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
			}
		}
		if (!inner.Contains(thing) && !inner.Contains(val))
		{
			hcomp.Deregister(thing);
		}
	}

	protected override bool HasDepositable()
	{
		return HasDepositableForVehicle();
	}

	private static int VehicleRemainingForDef(Thing vehicle, ThingDef def)
	{
		if (def == null)
		{
			return 0;
		}
		IList list = VehicleFrameworkCompat.CargoToLoad(vehicle);
		if (list == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			object obj = list[i];
			TransferableOneWay val = (TransferableOneWay)((obj is TransferableOneWay) ? obj : null);
			if (val != null && ((Transferable)val).ThingDef == def && ((Transferable)val).CountToTransfer > 0)
			{
				num += ((Transferable)val).CountToTransfer;
			}
		}
		return num;
	}

	private bool HasDepositableForVehicle()
	{
		Thing vehicle = Vehicle;
		CompHauledToInventory comp = ((ThingWithComps)((JobDriver)this).pawn).GetComp<CompHauledToInventory>();
		ThingOwner<Thing> val = ((JobDriver)this).pawn.inventory?.innerContainer;
		if (vehicle == null || comp == null || val == null)
		{
			return false;
		}
		foreach (Thing item in comp.GetHashSet())
		{
			if (item != null && !item.Destroyed && ((ThingOwner)val).Contains(item) && InventorySurplus.SurplusOf(((JobDriver)this).pawn, item) > 0 && VehicleRemainingForDef(vehicle, item.def) > 0)
			{
				return true;
			}
		}
		return false;
	}
}
