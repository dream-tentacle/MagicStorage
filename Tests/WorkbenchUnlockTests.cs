using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class WorkbenchUnlockTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string message)
    { assertions++; if (!Equals(expected, actual)) throw new Exception("Workbench unlock: " + message + "; expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly Building_StorageSupplyShelf Shelf = new Building_StorageSupplyShelf();
        internal readonly ThingDef Steel = new ThingDef(), Component = new ThingDef();
        internal readonly ThingDef Table;
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            var parent = new StorageSettings(); parent.filter.SetAllow(Steel, true);
            Shelf.def.building = new BuildingProperties { fixedStorageSettings = parent };
            Thing[] buildings = { Core, Unit, Shelf };
            for (int i = 0; i < buildings.Length; i++)
            {
                var thing = buildings[i];
                thing.Map = Map; thing.Position = new IntVec3(i, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
                var node = new CompStorageNode { parent = thing };
                if (thing == Unit) Unit.node = node; else ((Building)thing).node = node;
                Map.GetComponent<MapComponent_StorageNetworks>().Register(node);
            }
            Map.haulDestinationManager.SlotParents[Shelf.Position] = Shelf;
            Map.GetComponent<MapComponent_StorageNetworks>().FinalizeInit();
            Table = new ThingDef { defName = "UnlockTestBench", costList = new List<ThingDefCountClass>
                { new ThingDefCountClass(Steel, 10), new ThingDefCountClass(Component, 2) } };
        }
        internal Thing Store(ThingDef def, int count)
        {
            var item = new Thing { def = def, stackCount = count };
            Unit.Inventory.Contents.TryAdd(item, false); return item;
        }
        internal Thing Stock(int count)
        {
            Shelf.SetSelection(0, Steel); Store(Steel, count); Shelf.RefreshSupply();
            return Shelf.GetSlotGroup().HeldThings.Single();
        }
    }

    internal static int Run()
    {
        var oldGame = Current.Game; bool oldMode = MagicStorageMod.Settings.WorkbenchUnlockMode;
        try
        {
            Equal(false, new MagicStorageSettings().WorkbenchUnlockMode, "mode defaults off");
            MagicStorageMod.Settings.WorkbenchUnlockMode = true; Current.Game = new Game();
            var free = new ThingDef { defName = "FreeTable" };
            Equal(true, CraftingWorkbenchUnlocks.IsUnlocked(free), "no materials means automatically unlocked");
            var f = new Fixture();
            Equal(false, CraftingWorkbenchUnlocks.IsUnlocked(f.Table), "costly workbench starts locked");
            var steel = f.Store(f.Steel, 9); f.Store(f.Component, 2);
            Equal(false, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "insufficient stock cannot unlock");
            Equal(9, steel.stackCount, "a shortage consumes no partial payment");
            f.Store(f.Steel, 1);
            Equal(true, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "complete network payment unlocks");
            Equal(0L, f.Core.Network.GetTotalCount(f.Steel), "exact steel cost consumed");
            Equal(0L, f.Core.Network.GetTotalCount(f.Component), "exact component cost consumed");
            Equal(true, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "duplicate unlock is harmless without materials");
            Equal(true, CraftingWorkbenchUnlocks.IsUnlocked(new ThingDef { defName = f.Table.defName, costList = f.Table.costList }), "identity uses defName, not Def object references");
            var another = new Fixture();
            Equal(true, CraftingWorkbenchUnlocks.IsUnlocked(another.Table), "other maps and cores share the save's unlocks");

            Current.Game.Unlocks.Unlock("RemovedMod_Workbench"); Current.Game.Unlocks.ExposeData();
            Equal(true, Scribe_Collections.SavedUnlocks.ContainsKey("RemovedMod_Workbench"), "missing mod names remain plain dictionary keys");
            Current.Game = new Game(); Scribe_Collections.LoadingUnlocks = true;
            Current.Game.Unlocks.ExposeData(); Scribe_Collections.LoadingUnlocks = false;
            Equal(true, Current.Game.Unlocks.IsUnlocked("RemovedMod_Workbench"), "loading needs no Def lookup for removed mods");
            Equal(true, CraftingWorkbenchUnlocks.IsUnlocked(f.Table), "unlock data survives serialization boundary");
            Current.Game = new Game();
            Equal(false, CraftingWorkbenchUnlocks.IsUnlocked(f.Table), "another save has independent unlocks");
            MagicStorageMod.Settings.WorkbenchUnlockMode = false;
            Equal(true, CraftingWorkbenchUnlocks.IsUnlocked(f.Table), "disabled mode bypasses paid unlocks");
            Equal(false, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "disabled mode never charges materials");
            MagicStorageMod.Settings.WorkbenchUnlockMode = true;

            f = new Fixture(); var shelfStock = f.Stock(6); f.Store(f.Steel, 4); f.Store(f.Component, 2);
            Equal(true, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "network and shelf stock combine for payment");
            Equal(true, shelfStock.Destroyed, "shelf stock is consumed as real items");
            Equal(0L, f.Core.Network.GetTotalCount(f.Steel), "network portion also consumed");
            Current.Game = new Game();
            f = new Fixture(); shelfStock = f.Stock(10); f.Store(f.Component, 2);
            f.Map.reservationManager.Reserved.Add(shelfStock);
            Equal(false, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "reserved shelf stock is excluded");
            Equal(10, shelfStock.stackCount, "reserved shelf stock remains intact");
            f.Map.reservationManager.Reserved.Remove(shelfStock);
            f.Shelf.node.Network = new StorageNetwork();
            Equal(false, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "a different network's shelf cannot pay");

            f = new Fixture(); f.Table.MadeFromStuff = true; f.Table.CostStuffCount = 10;
            f.Table.AllowedStuffs.Add(f.Steel);
            f.Table.AdjustedCosts = new List<ThingDefCountClass>
                { new ThingDefCountClass(f.Steel, 3), new ThingDefCountClass(f.Steel, 7), new ThingDefCountClass(f.Component, 2) };
            Equal(false, CraftingWorkbenchUnlocks.TryGetCosts(f.Table, null, out _), "a material must be selected");
            Equal(false, CraftingWorkbenchUnlocks.TryGetCosts(f.Table, f.Component, out _), "invalid material rejected");
            Equal(true, CraftingWorkbenchUnlocks.TryGetCosts(f.Table, f.Steel, out var costs), "legal selection accepted");
            Equal(10, costs.Single(cost => cost.thingDef == f.Steel).count, "engine-adjusted duplicate costs combine");
            f.Store(f.Steel, 10); f.Store(f.Component, 2);
            Equal(true, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, f.Steel), "chosen legal material payment succeeds");
            Current.Game = new Game();
            f = new Fixture(); f.Store(f.Steel, 10); f.Store(f.Component, 2); f.Table.IsResearchFinished = false;
            Equal(false, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "payment cannot bypass research");
            Equal(10L, f.Core.Network.GetTotalCount(f.Steel), "failed research gate consumes nothing");
            f.Table.IsResearchFinished = true;
            foreach (string invalidName in new[] { null, "", "   " })
            {
                f.Table.defName = invalidName;
                int errors = Log.Errors.Count;
                Equal(false, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "invalid defName rejects payment");
                Equal(errors + 1, Log.Errors.Count, "invalid defName logs one explicit error before payment");
                Equal(true, Log.Errors.Last().Contains("defName"), "error identifies the invalid field");
                Equal(10L, f.Core.Network.GetTotalCount(f.Steel), "invalid defName consumes no steel");
                Equal(2L, f.Core.Network.GetTotalCount(f.Component), "invalid defName consumes no components");
                Current.Game.Unlocks.Unlock(invalidName);
                Equal(errors + 2, Log.Errors.Count, "recording entry point also reports invalid defName");
                Equal(false, Current.Game.Unlocks.IsUnlocked(invalidName), "invalid name is never recorded");
                Log.Errors.RemoveRange(errors, Log.Errors.Count - errors);
            }
            PlanningAndShelfPayment();
            return assertions;
        }
        finally
        {
            Current.Game = oldGame; MagicStorageMod.Settings.WorkbenchUnlockMode = oldMode;
            Scribe_Collections.LoadingUnlocks = false;
        }
    }

    private static void PlanningAndShelfPayment()
    {
        Current.Game = new Game();
        var f = new Fixture();
        var shelfStock = f.Stock(6);
        var stored = f.Store(f.Steel, 14);
        f.Store(f.Component, 2);
        Equal(true, CraftingWorkbenchUnlocks.TryGetCosts(f.Table, null, out var costs), "preview resolves fixed costs");
        Equal(true, CraftingWorkbenchUnlocks.TryPlan(f.Core, costs, out var counts, out var plan), "preview finds a complete payment");
        Equal(20L, counts[f.Steel], "preview includes both network and shelf quantities");
        Equal(10, plan[stored], "shared planner takes the exact cost from the network first");
        Equal(false, plan.ContainsKey(shelfStock), "shelf supplies are spared when network stock is sufficient");
        Equal(14, stored.stackCount, "preview does not withdraw network stock");
        Equal(6, shelfStock.stackCount, "preview does not withdraw shelf stock");
        Equal(true, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "network-first preview agrees with payment");
        Equal(4L, f.Core.Network.GetTotalCount(f.Steel), "payment leaves excess network stock");
        Equal(6, shelfStock.stackCount, "payment preserves unused shelf supplies");

        Current.Game = new Game();
        f = new Fixture(); shelfStock = f.Stock(10); f.Store(f.Component, 2);
        CraftingWorkbenchUnlocks.TryGetCosts(f.Table, null, out costs);
        Equal(true, CraftingWorkbenchUnlocks.TryPlan(f.Core, costs, out counts, out plan), "shelf alone can supply a required material");
        Equal(10L, counts[f.Steel], "preview reports shelf-only material availability");
        Equal(10, plan[shelfStock], "shelf-only payment is included in the shared plan");
        f.Map.physicalInteractionReservationManager.Reserved.Add(shelfStock);
        Equal(false, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "confirm replans when shelf stock becomes reserved after preview");
        Equal(10, shelfStock.stackCount, "stale preview cannot consume reserved shelf stock");
        Equal(2L, f.Core.Network.GetTotalCount(f.Component), "stale preview consumes none of the other materials");
        Equal(false, CraftingWorkbenchUnlocks.TryPlan(f.Core, costs, out counts, out plan), "updated preview also rejects reserved shelf stock");
        Equal(false, counts.ContainsKey(f.Steel), "unavailable shelf stock disappears from preview counts");
        f.Map.physicalInteractionReservationManager.Reserved.Clear();
        Equal(true, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "released shelf stock can pay for the unlock");
        Equal(true, shelfStock.Destroyed, "successful payment consumes the real shelf stock");

        Current.Game = new Game();
        f = new Fixture(); shelfStock = f.Stock(6); stored = f.Store(f.Steel, 4); f.Store(f.Component, 2);
        CraftingWorkbenchUnlocks.TryGetCosts(f.Table, null, out costs);
        var owner = new object();
        Equal(2, f.Core.Network.ReserveOutgoing(owner, stored, 2), "another consumer reserves part of the network stock");
        Equal(false, CraftingWorkbenchUnlocks.TryPlan(f.Core, costs, out counts, out plan), "shared plan respects partial network reservations");
        Equal(8L, counts[f.Steel], "preview sums only unreserved network stock and usable shelf stock");
        Equal(false, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "payment agrees with the insufficient preview");
        Equal(6, shelfStock.stackCount, "shortage leaves shelf stock untouched");
        f.Core.Network.ReleaseOutgoing(owner);
        Equal(true, CraftingWorkbenchUnlocks.TryPlan(f.Core, costs, out counts, out plan), "released network stock completes the mixed-source plan");
        Equal(4, plan[stored], "mixed-source plan uses network stock first");
        Equal(6, plan[shelfStock], "mixed-source plan fills the shortfall from the shelf");
        Equal(true, CraftingWorkbenchUnlocks.TryUnlock(f.Core, f.Table, null), "mixed-source preview agrees with payment");
    }
}
