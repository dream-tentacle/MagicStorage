using System;
using System.Collections.Generic;
using MagicStorage;
using Verse;

internal static class StorageIndexTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string message)
    { assertions++; if (!Equals(expected, actual)) throw new Exception("Storage index: " + message + ": " + expected + " != " + actual); }

    private sealed class Client : IStorageReservationClient
    {
        internal int Calls;
        public void OnReservationInvalidated(StorageReservation reservation, StorageReservationFailure reason) { Calls++; }
    }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit { SlotCapacity = 1000 };
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Manager.EnsureCurrent();
        }
        internal CompStorageNode Register(Thing thing, int x, int z = 0)
        {
            thing.Map = Map; thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            thing.Position = new IntVec3(x, z);
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageUnit unit) unit.node = node;
            if (thing is Building_StorageCore core) core.node = node;
            Manager.Register(node); return node;
        }
        internal Thing Store(ThingDef def, int count, string variant = null)
        {
            var item = new Thing { def = def, stackCount = count, variant = variant };
            if (!Unit.Inventory.Contents.TryAdd(item, false)) throw new Exception("fixture insertion failed");
            return item;
        }
    }

    internal static int Run()
    {
        LocalChanges(); RebuildWithoutInventoryScan(); IncrementalMergeAndSplit(); LoadIndexReconstruction();
        ReservationIndexQueries(); TwoCoreMergeInvalidatesClaims(); MixedMutations();
        PartialStackLifecycle(); PartialStackAdmissionCost();
        return assertions;
    }

    private static void LocalChanges()
    {
        var f = new Fixture();
        for (int i = 0; i < 600; i++) f.Store(new ThingDef(), 1);
        var def = new ThingDef(); Thing first = f.Store(def, 20), second = f.Store(def, 7), third = f.Store(def, 9);
        f.Unit.Inventory.Contents.IndexReads = 0;
        var output = new ThingOwner(null);
        Equal(5, f.Core.Network.TryTransferTo(first, 5, output).Transferred, "partial withdrawal");
        Equal(31L, f.Core.Network.GetTotalCount(def), "partial withdrawal updates summary");
        f.Unit.Inventory.Contents.Remove(second);
        Equal(24L, f.Core.Network.GetTotalCount(def), "middle stack removal updates summary");
        Equal(true, f.Unit.Inventory.Contents.TryAdd(new Thing { def = def, stackCount = 3 }), "merge using type bucket");
        Equal(27L, f.Core.Network.GetTotalCount(def), "merge updates summary");
        var stacks = new List<Thing>(); f.Core.Network.GetStacks(def, stacks);
        Equal(2, stacks.Count, "swap deletion retains remaining nodes");
        Equal(true, stacks.Contains(third), "swapped node remains indexed");
        Equal(0, f.Unit.Inventory.Contents.IndexReads, "local changes never scan unrelated inventory slots");
    }

    private static int PartialCount(StorageInventory inventory, ThingDef def)
    {
        var stacks = new List<Thing>(); inventory.GetPartialStacks(def, stacks); return stacks.Count;
    }

    private static void PartialStackLifecycle()
    {
        var f = new Fixture(); var def = new ThingDef();
        Thing first = f.Store(def, 70), middle = f.Store(def, 71), last = f.Store(def, 72);
        Equal(3, PartialCount(f.Unit.Inventory, def), "new partial stacks enter index");
        f.Unit.Inventory.Contents.Remove(middle);
        Equal(2, PartialCount(f.Unit.Inventory, def), "middle removal swaps final partial stack into its place");
        Equal(true, f.Unit.Inventory.Contents.TryAdd(new Thing { def = def, stackCount = 8 }), "merge traverses snapshot while partial index removes full stacks");
        Equal(75, first.stackCount, "first partial filled"); Equal(75, last.stackCount, "swapped partial also filled");
        Equal(0, PartialCount(f.Unit.Inventory, def), "full stacks leave partial index");
        Equal(10, f.Core.Network.TryTransferTo(first, 10, new ThingOwner(null)).Transferred, "partially withdraw from full stack");
        Equal(1, PartialCount(f.Unit.Inventory, def), "partial withdrawal reintroduces formerly full stack");
        Equal(true, f.Unit.Inventory.Contents.TryAdd(new Thing { def = def, stackCount = 10 }), "new goods find reopened merge space");
        Equal(0, PartialCount(f.Unit.Inventory, def), "refilled stack removed again");
        f.Unit.Inventory.Contents.Remove(last);
        Equal(0, PartialCount(f.Unit.Inventory, def), "removing full stack leaves no stale candidates");
        first.stackCount = 20; f.Unit.Inventory.RefreshStack(first); f.Unit.Inventory.RefreshStack(first);
        Equal(1, PartialCount(f.Unit.Inventory, def), "repeated refresh never duplicates a partial stack");
        var replacementDef = new ThingDef(); first.def = replacementDef; f.Unit.Inventory.RefreshStack(first);
        Equal(0, PartialCount(f.Unit.Inventory, def), "changed def leaves old candidate bucket");
        Equal(1, PartialCount(f.Unit.Inventory, replacementDef), "changed def enters new candidate bucket");
        first.stackCount = 75; f.Unit.Inventory.RebuildIndex(); f.Manager.MarkDirty(); f.Manager.EnsureCurrent();
        Equal(0, PartialCount(f.Unit.Inventory, replacementDef), "load rebuild excludes full stacks");
        first.stackCount = 40; f.Unit.Inventory.RebuildIndex(); f.Manager.MarkDirty(); f.Manager.EnsureCurrent();
        Equal(1, PartialCount(f.Unit.Inventory, replacementDef), "load rebuild restores partial candidates");
        first.Destroyed = true; f.Unit.Inventory.RefreshStack(first);
        Equal(0, PartialCount(f.Unit.Inventory, replacementDef), "destroyed stack leaves candidate index");

        f = new Fixture(); def = new ThingDef(); first = f.Store(def, 20, "A");
        Thing.StackChecks = 0;
        Equal(5, f.Unit.Inventory.Contents.GetCountCanAccept(new Thing { def = def, stackCount = 5 }), "free slot provides sufficient capacity");
        Equal(0, Thing.StackChecks, "enough empty space skips compatibility scan");
        f.Unit.SlotCapacity = 1;
        Equal(0, f.Core.Network.GetCountCanAccept(new Thing { def = def, variant = "B", stackCount = 1 }), "same def with incompatible state cannot merge");
        first.variant = "B";
        Equal(1, f.Core.Network.GetCountCanAccept(new Thing { def = def, variant = "B", stackCount = 1 }), "compatibility is checked live without changing quantity or index");
    }

    private static void PartialStackAdmissionCost()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Unit.SlotCapacity = 10003;
        for (int i = 0; i < 10000; i++) f.Store(def, 75);
        Thing first = f.Store(def, 70); f.Store(def, 71); f.Store(def, 72);
        var incoming = new Thing { def = def, stackCount = 13 };
        f.Unit.Inventory.Contents.IndexReads = 0; Thing.StackChecks = 0;
        Equal(12, f.Core.Network.GetCountCanAccept(incoming), "network sums the three partial stacks only");
        Equal(3, Thing.StackChecks, "ten thousand full stacks never enter admission compatibility checks");
        Thing.StackChecks = 0;
        Equal(1, f.Unit.Inventory.Contents.GetCountCanAccept(new Thing { def = def, stackCount = 1 }), "container accepts target from first candidate");
        Equal(1, Thing.StackChecks, "capacity scan stops as soon as enough space is found");
        var claim = new object(); Thing.StackChecks = 0;
        Equal(12, f.Core.Network.ReserveIncoming(claim, incoming, 12), "reservation uses all unclaimed partial space");
        Equal(3, Thing.StackChecks, "reservation does not inspect full stacks");
        Thing.StackChecks = 0;
        Equal(0, f.Core.Network.GetCountCanAccept(incoming), "fully claimed candidates cannot be reused");
        Equal(0, Thing.StackChecks, "fully claimed space skips compatibility calls");
        Equal(3, PartialCount(f.Unit.Inventory, def), "reservations do not remove physical partial candidates");
        f.Core.Network.ReleaseIncoming(claim);
        var outgoing = new object(); f.Core.Network.ReserveOutgoing(outgoing, first, 1);
        Equal(7, f.Core.Network.GetCountCanAccept(incoming), "outgoing-reserved partial stack excluded from new merges");
        f.Core.Network.ReleaseOutgoing(outgoing);
        Thing.StackChecks = 0;
        Equal(true, f.Unit.Inventory.Contents.TryAdd(new Thing { def = def, stackCount = 12 }), "direct container merge fills all three candidates");
        Equal(true, Thing.StackChecks <= 9, "capacity and merging never revisit ten thousand full stacks");
        Equal(0, PartialCount(f.Unit.Inventory, def), "all filled candidates removed without skipping swapped entries");
        Equal(750225L, f.Core.Network.GetTotalCount(def), "large merge preserves exact indexed total");
        Equal(0, f.Unit.Inventory.Contents.IndexReads, "admission and merge do not scan physical container slots");
    }

    private static void RebuildWithoutInventoryScan()
    {
        var f = new Fixture();
        for (int i = 0; i < 600; i++) f.Store(new ThingDef(), 2);
        var def = new ThingDef(); f.Store(def, 17);
        f.Unit.Inventory.Contents.IndexReads = 0;
        f.Manager.MarkDirty(); f.Manager.EnsureCurrent();
        Equal(17L, f.Core.Network.GetTotalCount(def), "explicit rebuild combines persistent summaries");
        Equal(0, f.Unit.Inventory.Contents.IndexReads, "topology rebuild never reconstructs per-stack inventory index");
        var old = f.Core.Network;
        var isolated = new Building_StorageUnit(); f.Register(isolated, 50);
        Equal(old, f.Core.Network, "isolated addition preserves existing network identity");
        Equal(true, old.CanWork, "isolated addition leaves existing network usable");
        f.Manager.EnsureCurrent();
        Equal(0, f.Unit.Inventory.Contents.IndexReads, "node addition does not scan stock");
    }

    private static void IncrementalMergeAndSplit()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Store(def, 20);
        var remote = new Building_StorageUnit();
        remote.Inventory.Contents.TryAdd(new Thing { def = def, stackCount = 30 }, false);
        f.Register(remote, 3);
        Equal(StorageNetworkStatus.NoCore, remote.Network.Status, "remote inventory starts in separate component");
        var original = f.Core.Network;
        var wire = f.Register(new Thing(), 2);
        Equal(original, remote.Network, "smaller component merged into existing larger component");
        Equal(50L, original.GetTotalCount(def), "merged cached totals include loaded remote unit");
        Equal(0, remote.Inventory.Contents.IndexReads, "merge uses summary without scanning remote stock");
        f.Manager.Unregister(wire);
        Equal(false, original.CanWork, "deletion disables stale graph immediately");
        f.Manager.EnsureCurrent();
        Equal(20L, f.Core.Network.GetTotalCount(def), "split isolates original stock");
        Equal(30L, remote.Network.GetTotalCount(def), "split preserves remote stock");
        Equal(StorageNetworkStatus.NoCore, remote.Network.Status, "split removes remote access to core");
        Equal(0, remote.Inventory.Contents.IndexReads, "deletion rebuild does not scan stock");
    }

    private static void MixedMutations()
    {
        var f = new Fixture(); var random = new Random(314159);
        var defs = new[] { new ThingDef(), new ThingDef(), new ThingDef(), new ThingDef() };
        for (int step = 0; step < 200; step++)
        {
            var def = defs[random.Next(defs.Length)];
            var stacks = new List<Thing>(); f.Unit.Inventory.GetStacks(def, stacks);
            if (stacks.Count == 0 || random.Next(2) == 0) f.Store(def, random.Next(1, 30), step.ToString());
            else
            {
                Thing item = stacks[random.Next(stacks.Count)];
                f.Core.Network.TryTransferTo(item, random.Next(1, item.stackCount + 1), new ThingOwner(null));
            }
            if (step % 25 == 0) { f.Manager.MarkDirty(); f.Manager.EnsureCurrent(); }
            foreach (var type in defs)
            {
                long expected = 0;
                var expectedPartial = new HashSet<Thing>();
                foreach (Thing item in f.Unit.Inventory.Contents)
                    if (item.def == type)
                    {
                        expected += item.stackCount;
                        if (!item.Destroyed && item.stackCount > 0 && item.stackCount < item.def.stackLimit) expectedPartial.Add(item);
                    }
                Equal(expected, f.Unit.Inventory.GetTotalCount(type), "unit summary matches independent physical inventory");
                Equal(expected, f.Core.Network.GetTotalCount(type), "network summary matches independent physical inventory");
                var actualPartial = new List<Thing>(); f.Unit.Inventory.GetPartialStacks(type, actualPartial);
                Equal(expectedPartial.Count, actualPartial.Count, "mixed mutations never duplicate or omit partial candidates");
                Equal(true, expectedPartial.SetEquals(actualPartial), "partial candidates match independent physical inventory");
            }
        }
    }

    private static void LoadIndexReconstruction()
    {
        var f = new Fixture(); var def = new ThingDef(); Thing item = f.Store(def, 20);
        // Deserialization supplies real contents; rebuild their derived indexes once.
        item.stackCount = 11;
        f.Unit.Inventory.Contents.IndexReads = 0;
        f.Unit.Inventory.RebuildIndex();
        Equal(1, f.Unit.Inventory.Contents.IndexReads, "load rebuild scans physical contents once");
        f.Manager.MarkDirty(); f.Manager.EnsureCurrent();
        Equal(11L, f.Core.Network.GetTotalCount(def), "loaded graph combines reconstructed summaries");
        Equal(1, f.Unit.Inventory.Contents.IndexReads, "loaded topology does not rescan contents");
        f.Core.Network.TryTransferTo(item, 3, new ThingOwner(null));
        Equal(8L, f.Core.Network.GetTotalCount(def), "loaded stack resumes incremental updates");
    }

    private static void ReservationIndexQueries()
    {
        var f = new Fixture(); var def = new ThingDef(); Thing item = f.Store(def, 20);
        object a = new object(), b = new object();
        Equal(7, f.Core.Network.ReserveOutgoing(a, item, 7), "first indexed reservation");
        Equal(5, f.Core.Network.ReserveOutgoing(b, item, 5), "second indexed reservation");
        Equal(8, f.Core.Network.AvailableToWithdraw(item), "cached reserved total");
        Equal(15, f.Core.Network.AvailableToWithdraw(item, a), "query excludes own claim");
        Equal(9, f.Core.Network.ReserveOutgoing(a, item, 9), "amend reservation updates cached total");
        Equal(6, f.Core.Network.AvailableToWithdraw(item), "amended cached total");
        f.Core.Network.ReleaseOutgoing(a);
        Equal(15, f.Core.Network.AvailableToWithdraw(item), "release removes cached contribution");
        Equal(5, f.Core.Network.TryWithdrawReserved(b, item, 5, new ThingOwner(null), out _).Transferred,
            "collection removes cached contribution before mutation");
        Equal(15, f.Core.Network.AvailableToWithdraw(item), "collected claim no longer counted");
    }

    private static void TwoCoreMergeInvalidatesClaims()
    {
        var f = new Fixture(); var def = new ThingDef(); Thing first = f.Store(def, 20);
        var remote = new Building_StorageUnit();
        var second = new Thing { def = def, stackCount = 30 };
        remote.Inventory.Contents.TryAdd(second, false);
        f.Register(remote, 3); var otherCore = new Building_StorageCore(); f.Register(otherCore, 4);
        var a = new Client(); var b = new Client();
        Equal(5, f.Core.Network.ReserveOutgoing(new object(), first, 5, client: a), "first network claim");
        Equal(7, otherCore.Network.ReserveOutgoing(new object(), second, 7, client: b), "second network claim");
        f.Register(new Thing(), 2);
        Equal(StorageNetworkStatus.MultipleCores, f.Core.Network.Status, "incremental merge detects conflicting cores");
        Equal(50L, f.Core.Network.GetTotalCount(def), "disabled merged network retains owned-stock summary");
        f.Manager.MapComponentTick(); f.Manager.MapComponentTick();
        Equal(1, a.Calls, "first network owner notified once");
        Equal(1, b.Calls, "second network owner notified once");
        f.Manager.Unregister(otherCore.node); f.Manager.EnsureCurrent();
        Equal(true, f.Core.Network.CanWork, "deleting extra core restores network");
        Equal(50L, f.Core.Network.GetTotalCount(def), "restored network retains all stock");
    }
}
