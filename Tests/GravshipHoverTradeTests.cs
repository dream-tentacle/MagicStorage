using System;
using System.Linq;
using GravshipHoverTrade;
using HarmonyLib;
using MagicStorage;
using RimWorld;
using RimWorld.Planet;
using Verse;

internal static partial class StorageTradeIntegrationTests
{
    private sealed class HoverFixture
    {
        internal readonly Fixture Ground = new Fixture();
        internal readonly Gravship Ship = new Gravship();
        internal readonly Settlement Town = new Settlement { Faction = new Faction() };
        internal readonly ThingDef Def = new ThingDef();
        internal readonly Thing Stock;
        internal HoverFixture()
        {
            TradeDeal.OrdinaryGoods.Clear();
            Stock = Ground.Stock(Def, 10); Ground.Stock(ThingDefOf.Silver, 12);
            PortableOptions(Ground);
            Ground.Map.GetComponent<MapComponent_StorageNetworks>().Unregister(Ground.Core.node);
            Ground.Core.Spawned = false; Ground.Core.Map = null; Ground.Negotiator.Faction = Faction.OfPlayer;
            Ground.Core.def.size = new IntVec2(2, 2); Ground.Unit.def.size = new IntVec2(1, 2);
            Add(Ground.Core, 0); Add(Ground.Unit, 2);
            Ship.Settlement = Town; Ship.Pawns.Add(Ground.Negotiator);
            Town.Stock.TryAdd(new Thing { def = Def, stackCount = 8 });
            Town.Stock.TryAdd(new Thing { def = ThingDefOf.Silver, stackCount = 20 });
        }
        internal void Add(Thing thing, int x, int z = 0, Rot4 rotation = default(Rot4))
        {
            thing.Faction = Faction.OfPlayer; thing.def.category = ThingCategory.Building;
            if (thing is Building building && building.node == null) building.node = new CompStorageNode { parent = thing };
            if (thing is HoverStorageNode node) node.Node = new CompStorageNode { parent = thing };
            thing.TestExtraComp = thing is Building_StorageUnit unit ? unit.node :
                thing is Building b ? b.node : (thing as HoverStorageNode)?.Node;
            Ship.Cargo.Add(thing, new PositionData(new IntVec3(x, z), rotation));
        }
        internal FloatMenuOption Menu => GravshipHoverUtility.ChoicesAtFor(Ship).Last();
        internal Command_Action Button => Ship.GetGizmos().OfType<Command_Action>().Last();
        internal void Close() => Ground.Close(Find.WindowStack.WindowOfType<Dialog_Trade>());
    }

    private static void HoverCompatibility()
    {
        var harmony = new Harmony("magicstorage.tests.hovertrade");
        harmony.Patch(AccessTools.Method(typeof(TradeDeal), "AddAllTradeables"),
            postfix: new HarmonyMethod(typeof(Patch_TradeDeal_AddAllTradeables), nameof(Patch_TradeDeal_AddAllTradeables.Postfix)));
        new GravshipHoverTradeCompatibility().Apply(harmony);
        HoverEntriesAndSettlement(); HoverEligibility(); HoverSourceChanges(); HoverTopology();
    }

    private static void HoverEntriesAndSettlement()
    {
        var f = new HoverFixture();
        var loose = new Thing { def = f.Def, stackCount = 7 };
        f.Ship.Cargo.Add(loose, new PositionData(new IntVec3(0, 0), Rot4.North));
        Equal(2, GravshipHoverUtility.ChoicesAtFor(f.Ship).Count, "hover menu retains native option and adds one storage option");
        Equal(2, f.Ship.GetGizmos().Count(), "world gizmos retain native command and add one storage button");
        Equal(false, f.Button.Disabled, "valid carried network enables button");
        int fills = Patch_TradeDeal_AddAllTradeables.CargoFills;
        f.Menu.action();
        Equal(true, TradeSession.trader is GravshipTradeProxy proxy && ReferenceEquals(proxy.Ship, f.Ship) &&
            ReferenceEquals(proxy.Settlement, f.Town), "hover trade keeps original mod's merchant proxy");
        Equal(true, Find.WindowStack.WindowOfType<Dialog_StorageTrade>() != null, "hover trade uses portable API and remains safe with DTI enabled");
        var deal = TradeSession.deal;
        Equal(10, deal.AllTradeables.Find(line => line.ThingDef == f.Def).CountHeldBy(Transactor.Colony), "cargo postfix cannot add loose cargo to magic inventory");
        Equal(fills, Patch_TradeDeal_AddAllTradeables.CargoFills, "hover cargo injection skipped only during bound storage trade");
        deal.AllTradeables.Find(line => line.ThingDef == f.Def).CountToTransfer = 3;
        Equal(true, deal.TryExecute(out _), "hover purchase succeeds");
        Equal(9L, f.Ground.Unit.Inventory.GetTotalCount(ThingDefOf.Silver), "hover purchase pays exact stored silver");
        Equal(10, f.Ship.Things.Where(item => item.def == f.Def).Sum(item => item.stackCount), "purchases enter cargo alongside existing seven loose goods");
        Equal(10, f.Ship.ThingPlacements.Keys.Where(item => item.def == f.Def).Sum(item => item.stackCount), "adapter does not prime the landing cache before purchases");
        deal.AllTradeables.Find(line => line.ThingDef == f.Def).CountToTransfer = -4;
        Equal(true, deal.TryExecute(out _), "hover sale uses shared inventory settlement");
        Equal(6L, f.Ground.Unit.Inventory.GetTotalCount(f.Def), "hover sale subtracts exact stored goods");
        Equal(7, loose.stackCount, "hover storage sale leaves loose cargo untouched");
        Equal(4, f.Ship.Things.Where(item => item.def == ThingDefOf.Silver).Sum(item => item.stackCount), "sale proceeds retain original cargo delivery");
        Equal(fills, Patch_TradeDeal_AddAllTradeables.CargoFills, "refresh after purchases and sales never reinjects cargo");
        f.Close();

        bool replacement = DynamicTradeInterface.Patches.Dialog_TradePatches.Replace;
        DynamicTradeInterface.Patches.Dialog_TradePatches.Replace = false;
        try
        {
            GravshipHoverUtility.ChoicesAtFor(f.Ship)[0].action();
            Equal(fills + 1, Patch_TradeDeal_AddAllTradeables.CargoFills, "ordinary hover trading retains cargo injection");
            Equal(10, TradeSession.deal.AllTradeables.Find(line => line.ThingDef == f.Def).CountHeldBy(Transactor.Colony), "ordinary hover option still lists original cargo");
            Equal(false, StorageTradeSession.TryGet(TradeSession.deal, out _), "ordinary hover option never acquires magic session");
            f.Close();
        }
        finally { DynamicTradeInterface.Patches.Dialog_TradePatches.Replace = replacement; }
    }

    private static void HoverEligibility()
    {
        var f = new HoverFixture(); f.Ground.Negotiator.AllowedTrade = false;
        Equal(null, f.Menu.action, "hover menu obeys settlement trade permission");
        Equal(true, f.Button.Disabled, "hover button obeys settlement trade permission");
        f.Ground.Negotiator.AllowedTrade = true; f.Ground.Negotiator.skills.record.TotallyDisabled = true;
        Equal(null, f.Menu.action, "social incapacity blocks hover storage trade");
        f.Ground.Negotiator.skills.record.TotallyDisabled = false;
        f.Town.Faction.Hostile = true; Equal(null, f.Menu.action, "hostile settlement blocked"); f.Town.Faction.Hostile = false;
        f.Town.Faction.def.permanentEnemy = true; Equal(null, f.Menu.action, "permanent enemy blocked"); f.Town.Faction.def.permanentEnemy = false;
        f.Town.HasMap = true; Equal(null, f.Menu.action, "mapped settlement blocked"); f.Town.HasMap = false;
        f.Town.Available = false; Equal(null, f.Menu.action, "unavailable settlement blocked"); f.Town.Available = true;
        f.Ship.Pawns.Clear(); Equal(null, f.Menu.action, "missing crew negotiator blocked");
        f.Ship.destinationTile = 1;
        Equal(0, GravshipHoverUtility.ChoicesAtFor(f.Ship).Count, "flying away removes settlement options");
        Equal(1, f.Ship.GetGizmos().Count(), "flying away removes storage button");
    }

    private static void HoverSourceChanges()
    {
        foreach (string change in new[] { "core", "unit", "crew", "destination", "layout", "new core", "owner" })
        {
            var f = new HoverFixture(); var cachedPlacements = f.Ship.ThingPlacements;
            var oldAction = f.Menu.action; f.Button.action(); var deal = TradeSession.deal;
            deal.AllTradeables.Find(line => line.ThingDef == f.Def).CountToTransfer = -2;
            switch (change)
            {
                case "core": f.Ship.Cargo.Remove(f.Ground.Core); break;
                case "unit": f.Ship.Cargo.Remove(f.Ground.Unit); break;
                case "crew": f.Ship.Pawns.Clear(); break;
                case "destination": f.Ship.destinationTile = 1; break;
                case "layout": f.Ship.Cargo[f.Ground.Unit] = new PositionData(new IntVec3(50, 0), Rot4.North); break;
                case "new core": f.Add(new Building_StorageCore(), 3); break;
                case "owner": f.Ground.Core.Faction = new Faction(); break;
            }
            Equal(false, deal.TryExecute(out _), "hover confirmation rechecks " + change);
            Equal(10, f.Stock.stackCount, "hover source change preserves real stock: " + change);
            f.Close(); oldAction();
            Equal(0, Find.WindowStack.Windows.Count, "stale hover action cannot reopen after " + change);
        }
    }

    private static void HoverTopology()
    {
        var f = new HoverFixture(); f.Ship.Cargo.Remove(f.Ground.Core);
        Equal(null, f.Menu.action, "unconnected carried units do not bypass core requirement");
        f = new HoverFixture(); f.Add(new Building_StorageCore(), 3);
        Equal(null, f.Menu.action, "two connected cores disable the carried network");
        f = new HoverFixture(); f.Ground.Core.Faction = new Faction();
        Equal(null, f.Menu.action, "foreign core cannot activate player inventory");
        f = new HoverFixture(); f.Ship.Cargo[f.Ground.Unit] = new PositionData(new IntVec3(4, 0), Rot4.North);
        Equal(null, f.Menu.action, "gap between carried nodes disables storage trade");
        f.Add(new HoverStorageNode(), 2); f.Add(new HoverStorageNode(), 3);
        Equal(true, f.Menu.action != null, "carried conduits connect network across gap");
        var disconnected = new Building_StorageUnit(); f.Add(disconnected, 50);
        disconnected.Inventory.Contents.TryAdd(new Thing { def = f.Def, stackCount = 40 });
        var baseUnit = new Building_StorageUnit { Faction = Faction.OfPlayer };
        baseUnit.Inventory.Contents.TryAdd(new Thing { def = f.Def, stackCount = 30 });
        f.Menu.action();
        Equal(10, TradeSession.deal.AllTradeables.Find(line => line.ThingDef == f.Def).CountHeldBy(Transactor.Colony), "disconnected and off-ship inventories excluded");
        f.Close();
        f = new HoverFixture(); f.Ground.Unit.def.size = new IntVec2(1, 3);
        f.Ship.Cargo[f.Ground.Unit] = new PositionData(new IntVec3(3, 0), Rot4.East);
        Equal(true, f.Menu.action != null, "rotated multi-cell unit connects by full occupied footprint");
    }
}
