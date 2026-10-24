using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HaulersDream;

public static class HaulersDreamDebugActions_MakeColony
{
	[DebugAction(/*Could not decode attribute arguments.*/)]
	private static void MakeColony()
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dc: Expected O, but got Unknown
		//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_030f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0319: Unknown result type (might be due to invalid IL or missing references)
		//IL_0339: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
		bool godMode = DebugSettings.godMode;
		DebugSettings.godMode = true;
		DebugViewSettings.drawOpportunisticJobs = true;
		Thing.allowDestroyNonDestroyable = true;
		if (Autotests_ColonyMaker.usedCells == null)
		{
			Autotests_ColonyMaker.usedCells = new BoolGrid(Autotests_ColonyMaker.Map);
		}
		else
		{
			Autotests_ColonyMaker.usedCells.ClearAndResizeTo(Autotests_ColonyMaker.Map);
		}
		Autotests_ColonyMaker.overRect = new CellRect(Autotests_ColonyMaker.Map.Center.x - 50, Autotests_ColonyMaker.Map.Center.z - 50, 100, 50);
		Autotests_ColonyMaker.DeleteAllSpawnedPawns();
		GenDebug.ClearArea(Autotests_ColonyMaker.overRect, Find.CurrentMap);
		Autotests_ColonyMaker.Map.wealthWatcher.ForceRecount(false);
		CellRect val = default(CellRect);
		Autotests_ColonyMaker.TryGetFreeRect(90, 30, ref val);
		foreach (ThingDef item in DefDatabase<ThingDef>.AllDefs.Where((ThingDef def) => typeof(Building_WorkTable).IsAssignableFrom(def.thingClass)))
		{
			Thing obj = Autotests_ColonyMaker.TryMakeBuilding(item);
			Building_WorkTable val2 = (Building_WorkTable)(object)((obj is Building_WorkTable) ? obj : null);
			if (val2 == null)
			{
				continue;
			}
			foreach (RecipeDef allRecipe in ((Thing)val2).def.AllRecipes)
			{
				val2.billStack.AddBill(BillUtility.MakeNewBill(allRecipe, (Precept_ThingStyle)null));
			}
		}
		val = ((CellRect)(ref val)).ContractedBy(1);
		foreach (ThingDef item2 in DefDatabase<ThingDef>.AllDefs.Where((ThingDef def) => DebugThingPlaceHelper.IsDebugSpawnable(def, false) && (int)def.category == 2).ToList())
		{
			DebugThingPlaceHelper.DebugSpawn(item2, ((CellRect)(ref val)).RandomCell, -1, true, (ThingStyleDef)null, true, (WipeMode?)null);
		}
		int num = 30;
		List<TimeAssignmentDef> times = Enumerable.Repeat<TimeAssignmentDef>(TimeAssignmentDefOf.Work, 24).ToList();
		for (int num2 = 0; num2 < num; num2++)
		{
			Pawn val3 = PawnGenerator.GeneratePawn(Faction.OfPlayer.def.basicMemberKind, Faction.OfPlayer, (PlanetTile?)null);
			val3.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.NewColonyOptimism, (Pawn)null, (Precept)null);
			val3.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.NewColonyHope, (Pawn)null, (Precept)null);
			val3.timetable.times = times;
			foreach (WorkTypeDef allDef in DefDatabase<WorkTypeDef>.AllDefs)
			{
				if (!val3.WorkTypeIsDisabled(allDef))
				{
					val3.workSettings.SetPriority(allDef, 3);
				}
			}
			val3.workSettings.Disable(WorkTypeDefOf.Hauling);
			GenSpawn.Spawn((Thing)(object)val3, ((CellRect)(ref val)).RandomCell, Autotests_ColonyMaker.Map, (WipeMode)0);
		}
		Designator_ZoneAddStockpile_Resources val4 = new Designator_ZoneAddStockpile_Resources();
		CellRect val5 = default(CellRect);
		for (int num3 = 0; num3 < 7; num3++)
		{
			Autotests_ColonyMaker.TryGetFreeRect(8, 8, ref val5);
			val5 = ((CellRect)(ref val5)).ContractedBy(1);
			((Designator)val4).DesignateMultiCell(((CellRect)(ref val5)).Cells);
			((Zone_Stockpile)Autotests_ColonyMaker.Map.zoneManager.ZoneAt(((CellRect)(ref val5)).CenterCell)).settings.Priority = (StoragePriority)2;
		}
		Autotests_ColonyMaker.ClearAllHomeArea();
		Autotests_ColonyMaker.FillWithHomeArea(Autotests_ColonyMaker.overRect);
		DebugSettings.godMode = godMode;
		Thing.allowDestroyNonDestroyable = false;
	}
}
