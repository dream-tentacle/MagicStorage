using System;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MagicStorage;
using Verse;

// Verified engine boundary from both installed Nivarian versions.
namespace Nivarian
{
    public class NivarianBill
    {
        public enum PrinterRepeatMode { RepeatCount, TargetCount, Forever }
        public RecipeDef recipe;
        public int targetCount;
        public PrinterRepeatMode repeatMode = PrinterRepeatMode.TargetCount;
        public bool suspended;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool ShouldDoNow(Map map)
        {
            if (suspended || recipe == null || map == null) return false;
            ThingDef product = recipe.products.FirstOrDefault()?.thingDef;
            if (product == null) return false;
            if (repeatMode == PrinterRepeatMode.Forever) return true;
            if (repeatMode == PrinterRepeatMode.RepeatCount) return targetCount > 0;
            return repeatMode == PrinterRepeatMode.TargetCount &&
                map.listerThings.ThingsOfDef(product).Sum(t => t.stackCount) < targetCount;
        }
    }
}

internal static class NivarianBillTests
{
    internal static int Run()
    {
        new NivarianBillCompatibility().Apply(new Harmony("tests.nivarian.bill"));
        int assertions = 0;
        Action<bool, bool, string> check = (expected, actual, label) =>
        { assertions++; if (expected != actual) throw new Exception("Nivarian bill: " + label); };
        var map = new Map();
        map.component = new MapComponent_StorageNetworks(map);
        var manager = map.GetComponent<MapComponent_StorageNetworks>();
        var core = new Building_StorageCore { Map = map, Spawned = true, Faction = Faction.OfPlayer };
        core.node = new CompStorageNode { parent = core }; manager.Register(core.node);
        var units = new Building_StorageUnit[2];
        for (int i = 0; i < units.Length; i++)
        {
            var unit = new Building_StorageUnit { Map = map, Spawned = true,
                Faction = Faction.OfPlayer, Position = new IntVec3(i + 1, 0) };
            unit.node = new CompStorageNode { parent = unit }; manager.Register(unit.node); units[i] = unit;
        }
        manager.FinalizeInit();
        var def = new ThingDef { CountAsResource = false };
        var stored = new Thing { def = def, stackCount = 20 };
        units[0].Inventory.Contents.TryAdd(stored, false);
        units[1].Inventory.Contents.TryAdd(new Thing { def = def, stackCount = 30 }, false);
        var ground = new Thing { def = def, stackCount = 10 };
        map.listerThings.Things.Add(ground);
        var bill = new Nivarian.NivarianBill { recipe = new RecipeDef(), targetCount = 60 };
        bill.recipe.products.Add(new ThingDefCountClass(def, 1));
        check(false, bill.ShouldDoNow(map), "ground and two units reach target, including nonresources");
        bill.targetCount = 61;
        check(true, bill.ShouldDoNow(map), "same network not counted once per unit");
        bill.targetCount = 60;
        ground.stackCount--;
        check(true, bill.ShouldDoNow(map), "consuming a product resumes production");
        ground.stackCount++;
        core.Network.ReserveOutgoing(new object(), stored, 10);
        check(false, bill.ShouldDoNow(map), "reserved stock still counts");
        map.Fog.Add(units[0].Position);
        check(true, bill.ShouldDoNow(map), "fogged storage excluded");
        map.Fog.Clear();
        units[0].Inventory.Contents.Remove(stored);
        map.listerThings.Things.Add(stored);
        check(false, bill.ShouldDoNow(map), "withdrawal to ground preserves total");
        bill.targetCount = 61;
        check(true, bill.ShouldDoNow(map), "withdrawn items not counted twice");
        bill.suspended = true;
        check(false, bill.ShouldDoNow(map), "suspension preserved");
        bill.suspended = false;
        bill.targetCount = 1;
        bill.repeatMode = Nivarian.NivarianBill.PrinterRepeatMode.RepeatCount;
        check(true, bill.ShouldDoNow(map), "repeat count unaffected");
        bill.repeatMode = Nivarian.NivarianBill.PrinterRepeatMode.Forever;
        check(true, bill.ShouldDoNow(map), "forever unaffected");
        bill.repeatMode = Nivarian.NivarianBill.PrinterRepeatMode.TargetCount;
        check(true, NivarianBillCompatibility.BelowTarget(new Map(), def, 1), "other map stock excluded");
        bill.recipe.products.Insert(0, new ThingDefCountClass(new ThingDef(), 1));
        check(true, bill.ShouldDoNow(map), "only first recipe product determines target");
        return assertions;
    }
}
