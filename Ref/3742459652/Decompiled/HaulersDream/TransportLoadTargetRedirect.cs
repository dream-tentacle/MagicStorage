using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class TransportLoadTargetRedirect
{
	[ThreadStatic]
	private static HashSet<ThingDef> scratchCarriedDefs;

	public static bool ValidateAndRedirectCurrentTarget(JobDriver_LoadTransportersInBulk driver, LoadTransportersAdapter adapter)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		if (driver == null || adapter == null)
		{
			return false;
		}
		Pawn pawn = ((JobDriver)driver).pawn;
		if (pawn == null || !Gen.IsHashIntervalTick((Thing)(object)pawn, Math.Max(10, driver.RevalidateInterval)))
		{
			return false;
		}
		Job job = ((JobDriver)driver).job;
		LocalTargetInfo target = job.GetTarget((TargetIndex)1);
		Thing thing = ((LocalTargetInfo)(ref target)).Thing;
		CompTransporter val = ((thing != null) ? ThingCompUtility.TryGetComp<CompTransporter>(thing) : null);
		HashSet<ThingDef> hashSet = CarriedSurplusDefs(pawn);
		if (hashSet.Count == 0)
		{
			return false;
		}
		if (val != null && MemberNeedsAny(val, hashSet))
		{
			return false;
		}
		foreach (CompTransporter item in adapter.Group)
		{
			if (((ThingComp)(item?)).parent != null && ((Thing)((ThingComp)item).parent).Spawned && item != val && MemberNeedsAny(item, hashSet))
			{
				job.SetTarget((TargetIndex)1, LocalTargetInfo.op_Implicit((Thing)(object)((ThingComp)item).parent));
				return true;
			}
		}
		return false;
	}

	private static HashSet<ThingDef> CarriedSurplusDefs(Pawn pawn)
	{
		HashSet<ThingDef> hashSet = scratchCarriedDefs ?? (scratchCarriedDefs = new HashSet<ThingDef>());
		hashSet.Clear();
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		ThingOwner<Thing> val = pawn.inventory?.innerContainer;
		if (comp == null || val == null)
		{
			return hashSet;
		}
		foreach (Thing item in comp.PeekHashSet())
		{
			if (item != null && !item.Destroyed && ((ThingOwner)val).Contains(item) && item.def != null && InventorySurplus.SurplusOf(pawn, item) > 0)
			{
				hashSet.Add(item.def);
			}
		}
		return hashSet;
	}

	private static bool MemberNeedsAny(CompTransporter member, HashSet<ThingDef> defs)
	{
		List<TransferableOneWay> list = member?.leftToLoad;
		if (list == null)
		{
			return false;
		}
		for (int i = 0; i < list.Count; i++)
		{
			TransferableOneWay val = list[i];
			if (val != null && ((Transferable)val).CountToTransfer > 0 && ((Transferable)val).ThingDef != null && defs.Contains(((Transferable)val).ThingDef))
			{
				return true;
			}
		}
		return false;
	}
}
