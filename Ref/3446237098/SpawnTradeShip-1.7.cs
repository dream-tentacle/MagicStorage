using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace CeleTech.Base;

[StaticConstructorOnStartup]
public class SpawnTradeShip
{
	public bool SpawnShip(string shipDefName)
	{
		Map currentMap = Find.CurrentMap;
		_ = Find.AnyPlayerHomeMap;
		Thing val = MakeTraderShip(currentMap, shipDefName);
		if (val != null)
		{
			return LandShip(currentMap, val);
		}
		return false;
	}

	private static Thing MakeTraderShip(Map map, string ShipName)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		Thing obj = ThingMaker.MakeThing(CMC_Def.CMC_TraderShuttle, (ThingDef)null);
		TradeShip val = new TradeShip(DefDatabase<TraderKindDef>.GetNamed(ShipName, true), (Faction)null);
		((PassingShip)val).name = TaggedString.op_Implicit(Translator.Translate("CMC_TradeShipName"));
		if (val == null)
		{
			throw new InvalidOperationException();
		}
		ThingCompUtility.TryGetComp<Comp_TraderShuttle>(obj).GenerateInternalTradeShip(map, val.def);
		return obj;
	}

	private unsafe bool UsableLZ(Building buildingTT, out Thing blocker)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		blocker = null;
		CellRect val = GenAdj.OccupiedRect((Thing)(object)buildingTT);
		Enumerator enumerator = ((CellRect)(ref val)).GetEnumerator();
		try
		{
			while (((Enumerator)(ref enumerator)).MoveNext())
			{
				foreach (Thing thing in GridsUtility.GetThingList(((Enumerator)(ref enumerator)).Current, Find.CurrentMap))
				{
					if (thing is Skyfaller)
					{
						blocker = thing;
						return false;
					}
					if (thing.def.IsBlueprint || thing.def.IsFrame)
					{
						blocker = thing;
						return false;
					}
					if (!(thing is Pawn) && (int)thing.def.Fillage != 0)
					{
						blocker = thing;
						return false;
					}
				}
			}
		}
		finally
		{
			((IDisposable)(*(Enumerator*)(&enumerator))/*cast due to constrained. prefix*/).Dispose();
		}
		return true;
	}

	public virtual bool LandShip(Map map, Thing ship)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 val = IntVec3.Invalid;
		Comp_TraderShuttle comp_TraderShuttle = ThingCompUtility.TryGetComp<Comp_TraderShuttle>(ship);
		List<Thing> list = map.listerThings.ThingsOfDef(CMC_Def.CMC_LandPlatform);
		List<Building> list2 = new List<Building>();
		if (list != null && list.Count > 0)
		{
			foreach (Thing item in list)
			{
				Building val2 = (Building)(object)((item is Building) ? item : null);
				CompPowerTrader val3 = ThingCompUtility.TryGetComp<CompPowerTrader>(item);
				if (val3 != null && val3.PowerOn && UsableLZ(val2, out var _))
				{
					list2.Add(val2);
				}
			}
		}
		if (list2.Count > 0)
		{
			val = ((Thing)GenCollection.RandomElement<Building>((IEnumerable<Building>)list2)).Position;
		}
		if (((IntVec3)(ref val)).IsValid)
		{
			Messages.Message(TaggedString.op_Implicit(Translator.Translate("Message_CMC_TraderLanded")), LookTargets.op_Implicit(ship), MessageTypeDefOf.PositiveEvent, true);
			GenSpawn.Spawn((Thing)(object)SkyfallerMaker.MakeSkyfaller(comp_TraderShuttle.Props.landAnimation, ship), val, map, (WipeMode)0);
			return true;
		}
		Messages.Message(TaggedString.op_Implicit(Translator.Translate("Message_CMC_TraderCantLanded")), LookTargets.op_Implicit(ship), MessageTypeDefOf.NeutralEvent, true);
		return false;
	}

	public static bool FindAnyLandingSpot(out IntVec3 spot, Faction faction, Map map, IntVec2? size)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (!DropCellFinder.FindSafeLandingSpot(ref spot, faction, map, 0, 15, 25, size, (IntVec3?)null))
		{
			IntVec3 val = DropCellFinder.RandomDropSpot(map, true);
			if (!DropCellFinder.TryFindDropSpotNear(val, map, ref spot, false, false, false, size, true))
			{
				spot = val;
			}
		}
		return true;
	}

	public static void FindCloseLandingSpot(out IntVec3 spot, Faction faction, Map map, IntVec2? size)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 val = default(IntVec3);
		int num = 0;
		foreach (Building item in map.listerBuildings.allBuildingsColonist.Where((Building x) => ((Thing)x).def.size.x > 1 || ((Thing)x).def.size.z > 1))
		{
			val += ((Thing)item).Position;
			num++;
		}
		if (num == 0)
		{
			FindAnyLandingSpot(out spot, faction, map, size);
			return;
		}
		val.x /= num;
		val.z /= num;
		int num2 = 20;
		float num3 = 999999f;
		spot = default(IntVec3);
		for (int num4 = 0; num4 < num2; num4++)
		{
			FindAnyLandingSpot(out var spot2, faction, map, size);
			IntVec3 val2 = spot2 - val;
			if ((float)((IntVec3)(ref val2)).LengthManhattan < num3)
			{
				val2 = spot2 - val;
				num3 = ((IntVec3)(ref val2)).LengthManhattan;
				spot = spot2;
			}
		}
	}
}
