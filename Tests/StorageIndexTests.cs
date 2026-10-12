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
                foreach (Thing item in f.Unit.Inventory.Contents) if (item.def == type) expected += item.stackCount;
                Equal(expected, f.Unit.Inventory.GetTotalCount(type), "unit summary matches independent physical inventory");
                Equal(expected, f.Core.Network.GetTotalCount(type), "network summary matches independent physical inventory");
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
