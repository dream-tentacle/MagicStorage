using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MagicStorage;
using RimWorld;
using Verse;

internal static class StorageBuildMaterialTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string reason)
    { checks++; if (!Equals(expected, actual)) throw new Exception(reason + ": expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly ThingDef Material = new ThingDef();
        internal readonly Building_StorageUnit Unit;
        internal Fixture()
        {
            Find.CurrentMap = Map;
            Map.resourceCounter.AllCountedAmounts.Add(Material, 0);
            Unit = new Building_StorageUnit { Map = Map };
            Map.Manager.StorageUnits.Add(Unit);
        }
        internal Thing Store(Building_StorageUnit unit, ThingDef def, int count = 1)
        {
            var item = new Thing { def = def, stackCount = count, holdingOwner = unit.Inventory.Contents };
            if (!unit.Inventory.Stacks.TryGetValue(def, out var list)) unit.Inventory.Stacks.Add(def, list = new List<Thing>());
            list.Add(item);
            return item;
        }
        internal Designator_Build Open()
        { var build = new Designator_Build(); build.ProcessInput(new UnityEngine.Event()); return build; }
    }

    public static int Main()
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(typeof(StorageBuildMaterialPatches).TypeHandle);
            Equal(0, Log.Errors.Count, "production patch matched the native-shaped call site");
            StoredAndGround(); Eligibility(); NativeRules(); GuardedRewrite();
            Console.WriteLine("PASS: " + checks + " build material assertions (production Harmony transpiler executed; game UI and inventory boundaries stubbed).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void StoredAndGround()
    {
        var f = new Fixture();
        Equal(1, f.Open().Rejections, "no stock retains native rejection");
        Thing stack = f.Store(f.Unit, f.Material);
        Designator_Build build = f.Open();
        Equal(f.Material, build.Options.Single(), "one stored unit opens the menu without any ground material");
        Equal(0, build.Rejections, "storage-only stock avoids native empty-menu rejection");
        Equal(0, f.Map.resourceCounter.AllCountedAmounts[f.Material], "no resource refresh is needed or performed");
        var absent = new ThingDef();
        f.Map.resourceCounter.AllCountedAmounts.Add(absent, 500);
        Equal(false, f.Open().Options.Contains(absent), "absent compatible material stays hidden even with a stale positive resource count");
        Equal(1, f.Open().Options.Count, "owning one material does not reveal other compatible materials");
        f.Map.resourceCounter.AllCountedAmounts.Remove(absent);
        Equal(0, f.Map.listerThings.ThingsOfDef(f.Material).Count, "ordinary map queries still exclude stored stock");
        Equal(f.Unit.Inventory.Contents, stack.holdingOwner, "query does not transfer or mutate stored items");
        var second = new Building_StorageUnit { Map = f.Map };
        f.Map.Manager.StorageUnits.Add(second); f.Store(second, f.Material, 40);
        Equal(1, f.Open().Options.Count, "multiple storage units do not duplicate a material");
        var ground = new List<Thing> { new Thing { def = f.Material } };
        f.Map.listerThings.ByDef.Add(f.Material, ground);
        f.Unit.Inventory.CountQueries = second.Inventory.CountQueries = 0;
        Equal(1, f.Open().Options.Count, "ground and stored material produce only one option");
        Equal(0, f.Unit.Inventory.CountQueries + second.Inventory.CountQueries, "existing ground material skips inventory queries");
        Equal(1, ground.Count, "native ground list is never appended to");
        f.Map.listerThings.ByDef.Clear(); f.Unit.Inventory.Stacks.Clear(); second.Inventory.Stacks.Clear();
        Equal(1, f.Open().Rejections, "withdrawing the last stack immediately removes the option");
        f.Store(f.Unit, f.Material);
        var other = new Fixture();
        Equal(0, other.Open().Options.Count, "another map does not inherit stored material");
        Find.CurrentMap = f.Map;
        Equal(f.Material, f.Open().Options.Single(), "switching back reads the original map");
    }

    private static void Eligibility()
    {
        var f = new Fixture(); Thing stack = f.Store(f.Unit, f.Material);
        f.Unit.Faction = new Faction(); Equal(0, f.Open().Options.Count, "foreign stock excluded");
        f.Unit.Faction = Faction.OfPlayer; f.Unit.Spawned = false; Equal(0, f.Open().Options.Count, "unspawned storage excluded");
        f.Unit.Spawned = true; f.Unit.Destroyed = true; Equal(0, f.Open().Options.Count, "destroyed storage excluded");
        f.Unit.Destroyed = false; f.Unit.Map = new Map(); Equal(0, f.Open().Options.Count, "storage on another map excluded");
        f.Unit.Map = f.Map; f.Unit.Position = new IntVec3 { InFog = true }; Equal(0, f.Open().Options.Count, "fogged storage excluded");
        f.Unit.Position = new IntVec3(); stack.stackCount = 0; Equal(0, f.Open().Options.Count, "zero quantity excluded");
        stack.stackCount = 1; stack.Destroyed = true; Equal(0, f.Open().Options.Count, "destroyed stack excluded");
        stack.Destroyed = false; stack.holdingOwner = new object(); Equal(0, f.Open().Options.Count, "removed stack excluded even before index refresh");
        stack.holdingOwner = f.Unit.Inventory.Contents; Equal(1, f.Open().Options.Count, "valid stock restored without rebuilding network");
        f.Map.Manager.StorageUnits.Clear(); Equal(0, f.Open().Options.Count, "unregistered units excluded");
        f.Map.Manager = null; Equal(1, f.Open().Rejections, "missing manager retains native result");
    }

    private static void NativeRules()
    {
        var f = new Fixture(); f.Store(f.Unit, f.Material);
        f.Material.IsStuff = false; Equal(0, f.Open().Options.Count, "nonmaterial definitions excluded");
        f.Material.IsStuff = true; f.Material.stuffProps.Category = "Fabric";
        Equal(0, f.Open().Options.Count, "incompatible material categories excluded");
        f.Material.stuffProps.Category = "Metal";
        var rare = new ThingDef { BaseMarketValue = 100 };
        var common = new ThingDef(); common.stuffProps.commonality = 10;
        f.Map.resourceCounter.AllCountedAmounts.Add(rare, 0); f.Map.resourceCounter.AllCountedAmounts.Add(common, 0);
        f.Store(f.Unit, rare); f.Store(f.Unit, common);
        Equal(true, f.Open().Options.SequenceEqual(new[] { common, f.Material, rare }), "native material ordering retained");
        var blocked = new Designator_Build { CanInteract = false }; blocked.ProcessInput(new UnityEngine.Event());
        Equal(0, blocked.Options.Count, "interaction rejection cannot be bypassed");
        var fixedMaterial = new Designator_Build(); fixedMaterial.Building.MadeFromStuff = false;
        fixedMaterial.ProcessInput(new UnityEngine.Event()); Equal(1, fixedMaterial.NonStuffInputs, "fixed-material building uses native path");
        f.Unit.Inventory.Stacks.Clear(); DebugSettings.godMode = true;
        try { Equal(3, f.Open().Options.Count, "god mode still permits absent materials"); }
        finally { DebugSettings.godMode = false; }
    }

    private static void GuardedRewrite()
    {
        var query = AccessTools.Method(typeof(ListerThings), nameof(ListerThings.ThingsOfDef));
        var count = AccessTools.PropertyGetter(typeof(List<Thing>), nameof(List<Thing>.Count));
        var label = new DynamicMethod("labels", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
        var call = new CodeInstruction(OpCodes.Callvirt, query); call.labels.Add(label);
        call.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        var code = new[] { call, new CodeInstruction(OpCodes.Nop), new CodeInstruction(OpCodes.Callvirt, count) };
        var rewritten = StorageBuildMaterialPatches.IncludeStoredMaterials(code).ToList();
        Equal(3, rewritten.Count, "replacement inserts no instructions or branches");
        Equal(OpCodes.Call, rewritten[0].opcode, "instance call becomes stack-compatible static call");
        Equal(label, rewritten[0].labels.Single(), "branch label retained");
        Equal(ExceptionBlockType.BeginExceptionBlock, rewritten[0].blocks.Single().blockType, "exception boundary retained");
        foreach (int matches in new[] { 0, 2 })
        {
            var incompatible = new List<CodeInstruction>();
            for (int i = 0; i < matches; i++)
            { incompatible.Add(new CodeInstruction(OpCodes.Callvirt, query)); incompatible.Add(new CodeInstruction(OpCodes.Callvirt, count)); }
            var original = incompatible.Select(c => c.operand).ToArray();
            var unchanged = StorageBuildMaterialPatches.IncludeStoredMaterials(incompatible).ToList();
            Equal(true, unchanged.Select(c => c.operand).SequenceEqual(original), "missing or ambiguous match leaves IL unchanged");
        }
        var wrongUse = new[] { new CodeInstruction(OpCodes.Callvirt, query), new CodeInstruction(OpCodes.Pop) };
        Equal(query, StorageBuildMaterialPatches.IncludeStoredMaterials(wrongUse).First().operand, "query not consumed by Count remains unchanged");
        Equal(3, Log.Errors.Count, "each incompatible call-site shape produces a diagnostic");
    }
}
