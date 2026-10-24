using System;
using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

internal static class InventoryDrop
{
	internal unsafe static bool TryDropPreferHome(Pawn pawn, Thing thing, int count, string site, out Thing result)
	{
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
		result = null;
		ThingOwner<Thing> val = pawn?.inventory?.innerContainer;
		Map map = ((pawn != null) ? ((Thing)pawn).Map : null);
		if (val == null || map == null || thing == null || thing.Destroyed)
		{
			return false;
		}
		AreaManager areaManager = map.areaManager;
		Area_Home homeArea = ((areaManager != null) ? areaManager.Home : null);
		bool flag = UnloadFallbackPolicy.PreferHomeAreaDrop(map.IsPlayerHome, homeArea != null && ((Area)homeArea).TrueCount > 0);
		int stackCount = thing.stackCount;
		bool flag2 = false;
		if (flag)
		{
			Predicate<IntVec3> nearPlaceValidator = (IntVec3 c) => GenGrid.InBounds(c, map) && ((Area)homeArea)[c];
			flag2 = TryDropMarked((ThingOwner)(object)val, thing, count, out result, nearPlaceValidator);
		}
		bool flag3 = flag2;
		if (!flag2)
		{
			bool stackGone = thing.Destroyed || !((ThingOwner)val).Contains(thing);
			int num = UnloadFallbackPolicy.RemainingToDrop(count, stackCount, thing.stackCount, stackGone);
			if (num > 0)
			{
				Thing val2 = result;
				flag2 = TryDropMarked((ThingOwner)(object)val, thing, num, out result, null);
				if (result == null)
				{
					result = val2;
				}
			}
			else
			{
				flag2 = result != null;
			}
		}
		Thing obj = result;
		IntVec3 cell = ((obj != null) ? obj.Position : IntVec3.Invalid);
		HDLog.Dbg($"drop[{site}] {((Entity)pawn).LabelShort}: {((Entity)thing).LabelShort} x{count} -> " + (flag2 ? ((object)(*(IntVec3*)(&cell))/*cast due to constrained. prefix*/).ToString() : "FAILED") + " " + $"(pawnAt={((Thing)pawn).Position}, pawnInHome={IsInHome(map, ((Thing)pawn).Position)}, " + $"preferHome={flag}, homePassWon={flag3}, " + "landedInHome=" + (flag2 ? IsInHome(map, cell).ToString() : "n/a") + ")");
		return flag2;
	}

	private static bool TryDropMarked(ThingOwner inner, Thing thing, int count, out Thing result, Predicate<IntVec3> nearPlaceValidator)
	{
		HaulChurnGuard.EnterPlacementWrapper();
		try
		{
			return inner.TryDrop(thing, (ThingPlaceMode)1, count, ref result, (Action<Thing, int>)null, nearPlaceValidator);
		}
		finally
		{
			HaulChurnGuard.ExitPlacementWrapper();
		}
	}

	internal static bool IsInHome(Map map, IntVec3 cell)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (map == null)
		{
			obj = null;
		}
		else
		{
			AreaManager areaManager = map.areaManager;
			obj = ((areaManager != null) ? areaManager.Home : null);
		}
		Area_Home val = (Area_Home)obj;
		if (val != null && ((IntVec3)(ref cell)).IsValid && GenGrid.InBounds(cell, map))
		{
			return ((Area)val)[cell];
		}
		return false;
	}
}
