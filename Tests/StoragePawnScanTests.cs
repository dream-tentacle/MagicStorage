using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MagicStorage;
using Verse;

internal static class StoragePawnScanTests
{
    private static int checks;
    private static Map skippedMap, prefixThrowMap, postfixThrowMap;
    private static readonly Exception injected = new InvalidOperationException("Injected pawn scan failure");

    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly TestStorageHolder Storage = new TestStorageHolder();
        internal readonly MagicStorage.TestsContainers.TestStorageHolder NestedNamespace = new MagicStorage.TestsContainers.TestStorageHolder();
        internal readonly TestVanillaHolder Vanilla = new TestVanillaHolder();
        internal readonly MagicStorageOther.TestUnrelatedHolder Unrelated = new MagicStorageOther.TestUnrelatedHolder();
        internal readonly Pawn Pawn = new Pawn();
        internal Fixture()
        {
            Storage.Contents.Add(new Pawn());
            NestedNamespace.Contents.Add(new Pawn());
            Vanilla.Contents.Add(Pawn); Vanilla.Contents.Add(new Pawn { Dead = true });
            Unrelated.Contents.Add(new Thing());
            Map.Holders.AddRange(new IThingHolder[] { Storage, Vanilla, NestedNamespace, Unrelated });
        }
        internal List<IThingHolder> Collect()
        { var holders = new List<IThingHolder>(); Map.GetChildHolders(holders); return holders; }
    }

    public static int Main()
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(typeof(StoragePawnScanPatches).TypeHandle);
            OrdinaryAndScoped(); NestedStorage(); NestedQueries(); Exceptions(); DifferentMapAndThread(); OtherPatches();
            Console.WriteLine("PASS: " + checks + " pawn scan assertions (production Harmony prefix/postfix/finalizer executed; native holder traversal boundaries stubbed).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void OrdinaryAndScoped()
    {
        var f = new Fixture();
        for (int i = 0; i < 10000; i++) f.Storage.Contents.Add(new Thing());
        Equal(4, f.Collect().Count, "ordinary collection preserves all holders");
        Equal(f.Pawn, f.Map.mapPawns.AllPawnsUnspawned.Single(), "only vanilla living pawn is returned");
        Equal(0, f.Storage.Reads, "ten thousand stored items and stored pawn are not traversed");
        Equal(0, f.NestedNamespace.Reads, "child namespace storage holders are also excluded");
        Equal(1, f.Vanilla.Reads, "vanilla containers are still read");
        Equal(1, f.Unrelated.Reads, "similar namespace prefix is not misclassified");
        Equal(4, f.Collect().Count, "normal return restores holder visibility for item queries");
        Equal(10001, f.Storage.GetDirectlyHeldThings().Count, "filtering removes no real inventory");
    }

    private static void NestedQueries()
    {
        var outer = new Fixture(); var inner = new Fixture();
        outer.Map.BeforeCollect = () =>
        {
            Equal(inner.Pawn, inner.Map.mapPawns.AllPawnsUnspawned.Single(), "nested query returns the inner map's pawn");
        };
        Equal(outer.Pawn, outer.Map.mapPawns.AllPawnsUnspawned.Single(), "nested return restores outer scope");
        Equal(0, outer.Storage.Reads, "outer storage remains excluded after inner query returns");
        Equal(0, inner.Storage.Reads, "inner storage excluded in its own scope");
        outer.Map.BeforeCollect = null;
        Equal(4, inner.Collect().Count, "inner map visible after nesting completes");
        Equal(4, outer.Collect().Count, "outer map visible after nesting completes");
    }

    private static void NestedStorage()
    {
        var f = new Fixture(); var nestedStorage = new TestStorageHolder();
        nestedStorage.Contents.Add(new Pawn());
        var nestedVanilla = new TestVanillaHolder(); var pawn = new Pawn(); nestedVanilla.Contents.Add(pawn);
        f.Vanilla.Children.Add(nestedStorage); f.Vanilla.Children.Add(nestedVanilla);
        var pawns = f.Map.mapPawns.AllPawnsUnspawned;
        Equal(2, pawns.Count, "vanilla nested pawn remains visible while nested storage pawn is excluded");
        Equal(true, pawns.Contains(pawn), "descendant filter preserves ordinary containers");
        Equal(0, nestedStorage.Reads, "storage nested under a vanilla holder is never visited");
        var things = new List<Thing>();
        ThingOwnerUtility.GetAllThingsRecursively(f.Vanilla, things);
        Equal(4, things.Count, "ordinary recursive item scan still includes nested storage contents");
        ThingOwnerUtility.GetAllThingsRecursively(f.Vanilla, things, passCheck: holder => holder != nestedVanilla);
        Equal(3, things.Count, "ordinary caller's existing predicate remains effective");
    }

    private static void ExpectInjected(Action action)
    {
        try { action(); throw new Exception("Expected injected exception."); }
        catch (Exception error)
        { Equal(injected, error, "original exception is preserved rather than suppressed"); }
    }

    private static void Exceptions()
    {
        var f = new Fixture(); f.Map.BeforeCollect = () => { throw injected; };
        ExpectInjected(() => { var pawns = f.Map.mapPawns.AllPawnsUnspawned; });
        f.Map.BeforeCollect = null;
        Equal(4, f.Collect().Count, "throwing original getter restores ordinary visibility");
        Equal(f.Pawn, f.Map.mapPawns.AllPawnsUnspawned.Single(), "next query works after an exception");
        var inner = new Fixture(); inner.Map.BeforeCollect = () => { throw injected; };
        f.Map.BeforeCollect = () => ExpectInjected(() => { var pawns = inner.Map.mapPawns.AllPawnsUnspawned; });
        Equal(f.Pawn, f.Map.mapPawns.AllPawnsUnspawned.Single(), "caught inner exception restores active outer scope");
        f.Map.BeforeCollect = null; inner.Map.BeforeCollect = null;
        Equal(4, f.Collect().Count, "outer scope exits normally after caught inner exception");
    }

    private static void DifferentMapAndThread()
    {
        var f = new Fixture(); var other = new Fixture();
        f.Map.BeforeCollect = () =>
        {
            Equal(4, other.Collect().Count, "ordinary collection on another map is not filtered");
            int count = Task.Run(() => f.Collect().Count).GetAwaiter().GetResult();
            Equal(4, count, "another thread does not inherit the pawn-query flag");
        };
        // Avoid invoking the callback recursively on the worker thread.
        Action callback = f.Map.BeforeCollect;
        f.Map.BeforeCollect = () =>
        {
            f.Map.BeforeCollect = null;
            try { callback(); }
            finally { f.Map.BeforeCollect = callback; }
        };
        Equal(f.Pawn, f.Map.mapPawns.AllPawnsUnspawned.Single(), "main thread still filters after unrelated collection");
        f.Map.BeforeCollect = null;
        Equal(0, f.Storage.Reads, "unrelated operations do not disable the active main-thread filter");
    }

    private static bool OtherPrefix(Map ___map, ref List<Pawn> __result)
    {
        if (ReferenceEquals(___map, prefixThrowMap)) throw injected;
        if (!ReferenceEquals(___map, skippedMap)) return true;
        __result = new List<Pawn>();
        return false;
    }

    private static void OtherPostfix(Map ___map)
    { if (ReferenceEquals(___map, postfixThrowMap)) throw injected; }

    private static void OtherPatches()
    {
        var harmony = new Harmony("tests.magicstorage.pawnscan.otherpatches");
        harmony.Patch(AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.AllPawnsUnspawned)),
            prefix: new HarmonyMethod(typeof(StoragePawnScanTests), nameof(OtherPrefix)) { priority = Priority.First },
            postfix: new HarmonyMethod(typeof(StoragePawnScanTests), nameof(OtherPostfix)));
        var f = new Fixture(); var inner = new Fixture(); skippedMap = inner.Map;
        f.Map.BeforeCollect = () => Equal(0, inner.Map.mapPawns.AllPawnsUnspawned.Count, "other prefix skips inner query");
        Equal(f.Pawn, f.Map.mapPawns.AllPawnsUnspawned.Single(), "skipped inner prefix cannot clear the outer scope");
        f.Map.BeforeCollect = null; skippedMap = null;
        Equal(4, f.Collect().Count, "outer visibility restored after skipped nested query");
        prefixThrowMap = inner.Map;
        f.Map.BeforeCollect = () => ExpectInjected(() => { var pawns = inner.Map.mapPawns.AllPawnsUnspawned; });
        Equal(f.Pawn, f.Map.mapPawns.AllPawnsUnspawned.Single(), "exception preceding our prefix does not overwrite outer state");
        f.Map.BeforeCollect = null; prefixThrowMap = null;
        Equal(4, f.Collect().Count, "scope restored after preceding prefix throws");
        postfixThrowMap = f.Map;
        ExpectInjected(() => { var pawns = f.Map.mapPawns.AllPawnsUnspawned; });
        postfixThrowMap = null;
        Equal(4, f.Collect().Count, "finalizer restores state when a postfix throws");
    }
}
