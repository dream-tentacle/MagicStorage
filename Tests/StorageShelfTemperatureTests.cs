using System;
using System.Linq;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using Verse;

internal static class StorageShelfTemperatureTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string label)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(label + ": " + expected + " != " + actual); }

    private static Command_Toggle Toggle(Building_StorageSupplyShelf shelf) =>
        shelf.GetGizmos().OfType<Command_Toggle>().Single(command =>
            command.defaultLabel == "MS_Shelf_LowTemperatureMode".Translate());

    internal static int Run()
    {
        RuntimeHelpers.RunClassConstructor(typeof(StorageShelfTemperaturePatches).TypeHandle);
        foreach (int slots in new[] { 3, 10 })
        {
            var map = new Map();
            var shelf = new Building_StorageSupplyShelf { Map = map, Spawned = true, Faction = Faction.OfPlayer };
            shelf.def.category = ThingCategory.Building;
            shelf.def.building = new BuildingProperties { maxItemsInCell = slots };
            map.haulDestinationManager.SlotParents[shelf.Position] = shelf;
            var item = new Thing { Map = map, Spawned = true, Position = shelf.Position };
            Command_Toggle toggle = Toggle(shelf);
            Equal(false, toggle.isActive(), "cooling defaults off for both shelf sizes");
            Equal(25f, item.AmbientTemperature, "disabled shelf preserves native temperature");
            toggle.toggleAction();
            Equal(true, shelf.LowTemperatureMode, "command enables cooling");
            Equal(false, shelf.CanWork, "test shelf has no working network");
            foreach (var sample in new[] { (25f, -10f), (95f, -5f), (100f, 0f), (105f, 5f), (-10f, -10f), (-20f, -20f) })
            {
                item.TestAmbientTemperature = sample.Item1;
                Equal(sample.Item2, item.AmbientTemperature, "actual getter hook applies cooling and bounds");
                Equal(sample.Item1, item.TestAmbientTemperature, "native temperature source is unchanged");
            }
            item.TestAmbientTemperature = 25f;
            Equal(25f, shelf.AmbientTemperature, "shelf building itself is not cooled");
            item.def.category = ThingCategory.Pawn;
            Equal(25f, item.AmbientTemperature, "pawns on shelf cell are not cooled");
            item.def.category = ThingCategory.Item;
            item.Position = new IntVec3(20, 20);
            Equal(25f, item.AmbientTemperature, "items moved off shelf regain native temperature");
            item.Position = shelf.Position;
            item.Spawned = false;
            Equal(25f, item.AmbientTemperature, "picked up items regain native temperature");
            item.Spawned = true;
            toggle.toggleAction();
            Equal(25f, item.AmbientTemperature, "disabling cooling takes effect immediately");
            map.haulDestinationManager.SlotParents[shelf.Position] = new Building_StorageReceiver();
            toggle.toggleAction();
            Equal(25f, item.AmbientTemperature, "other storage buildings are unaffected");

            shelf.GetGizmos().OfType<Command_Action>().Single(command =>
                command.defaultLabel == "MS_Shelf_CopyPreset".Translate()).action();
            toggle.toggleAction();
            var target = new Building_StorageSupplyShelf { Faction = Faction.OfPlayer };
            target.GetGizmos().OfType<Command_Action>().Single(command =>
                command.defaultLabel == "MS_Shelf_PastePreset".Translate()).action();
            Equal(true, target.LowTemperatureMode, "preset snapshots cooling independently of source edits");

            var savedBoundary = Scribe_Values.Boundary;
            object saved = null;
            try
            {
                Scribe_Values.Boundary = (key, value, fallback) =>
                { if (key == "lowTemperatureMode") saved = value; return value; };
                target.ExposeData();
                Equal<object>(true, saved, "enabled cooling is written through the save boundary");
                Toggle(target).toggleAction();
                Scribe_Values.Boundary = (key, value, fallback) => key == "lowTemperatureMode" ? saved : value;
                target.ExposeData();
                Equal(true, target.LowTemperatureMode, "saved cooling state is restored");
            }
            finally { Scribe_Values.Boundary = savedBoundary; }
        }
        return assertions;
    }
}
