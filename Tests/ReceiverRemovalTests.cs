using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using Verse;

// Production network, receiver, recovery and resource-counter code runs here.
// Spawn/despawn callbacks, the conduit body and physical placement are engine boundaries.
internal static class ReceiverRemovalTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string label)
    {
        assertions++;
        if (!Equals(expected, actual)) throw new Exception(label + ": " + expected + " != " + actual);
    }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map { UseNativeStorageLifecycle = true };
        internal readonly ThingDef Stored = new ThingDef(), Buffered = new ThingDef();
        internal readonly Building_StorageCore CoreA, CoreB;
        internal readonly Building_StorageUnit UnitA, UnitB;
        internal readonly Building BridgeA, BridgeB;
        internal readonly Building_StorageReceiver ReceiverA, ReceiverB;
        internal readonly Building_StorageSupplyShelf Shelf;
        internal readonly ResourceCounter Counter;
        internal readonly int InitialErrors = Log.Errors.Count;
        internal readonly bool Conflict;
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();

        internal Fixture(bool conflict)
        {
            Conflict = conflict;
            Map.component = new MapComponent_StorageNetworks(Map);
            Map.ResourceDefs.Add(Stored); Map.ResourceDefs.Add(Buffered);
            CoreA = Spawn(new Building_StorageCore(), 0);
            CoreA.Settings.Priority = StoragePriority.Critical;
            UnitA = AddUnit(1, 17);
            BridgeA = Spawn(new Building(), 2);
            ReceiverA = Spawn(new Building_StorageReceiver(), 3);
            CoreB = Spawn(new Building_StorageCore(), 10);
            CoreB.Settings.Priority = StoragePriority.Normal;
            UnitB = AddUnit(11, 29);
            BridgeB = Spawn(new Building(), 12);
            ReceiverB = Spawn(new Building_StorageReceiver(), 13);
            Shelf = new Building_StorageSupplyShelf();
            Shelf.def.building = new BuildingProperties();
            Shelf.GetStoreSettings().Priority = StoragePriority.Important;
            Spawn(Shelf, 20, networkNode: false);
            Manager.FinalizeInit();
            // Add the second core only after both live destinations have been sorted.
            if (conflict) Spawn(new Building_StorageCore(), 11, 1);
            Manager.EnsureCurrent();
            Buffer(ReceiverA, 7); Buffer(ReceiverB, 11);
            Counter = new ResourceCounter(Map);
        }

        private T Spawn<T>(T building, int x, int z = 0, bool networkNode = true) where T : Building
        {
            building.Position = new IntVec3(x, z);
            building.Faction = Faction.OfPlayer;
            building.def.category = ThingCategory.Building;
            if (networkNode) building.node = new CompStorageNode { parent = building };
            building.SpawnSetup(Map, false);
            return building;
        }

        private Building_StorageUnit AddUnit(int x, int count)
        {
            var unit = new Building_StorageUnit
            { Map = Map, Spawned = true, Position = new IntVec3(x, 0), Faction = Faction.OfPlayer };
            unit.node = new CompStorageNode { parent = unit };
            Manager.Register(unit.node);
            Equal(true, unit.Inventory.Contents.TryAdd(new Thing { def = Stored, stackCount = count }),
                "fixture stores stock");
            return unit;
        }

        private void Buffer(Building_StorageReceiver receiver, int count)
        {
            var item = new Thing
            { def = Buffered, stackCount = count, Map = Map, Spawned = true, Position = receiver.Position };
            Map.Ground.Add(item);
            // A native job owns these stacks; intake must leave them on the map.
            Map.reservationManager.Reserved.Add(item);
        }

        internal void Check(string stage, Building removed = null)
        {
            ISlotGroupParent[] expected = new ISlotGroupParent[] { ReceiverA, ReceiverB, Shelf }
                .Where(parent => ((Building)parent).Spawned).ToArray();
            var destinations = Map.haulDestinationManager;
            Equal(expected.Length, destinations.AllHaulDestinationsListForReading.Count, stage + " destination count");
            Equal(expected.Length, destinations.AllHaulDestinationsListForReading.Distinct().Count(), stage + " unique destinations");
            Equal(true, expected.All(destinations.AllHaulDestinationsListForReading.Contains), stage + " every destination retained");
            Equal(expected.Length, destinations.AllGroupsListForReading.Count, stage + " group count");
            Equal(expected.Length, destinations.AllGroupsListForReading.Distinct().Count(), stage + " unique groups");
            foreach (ISlotGroupParent parent in expected)
            {
                Equal(true, destinations.AllGroupsListForReading.Contains(parent.GetSlotGroup()), stage + " matching group");
                Equal(parent, destinations.SlotGroupParentAt(((Building)parent).Position), stage + " matching grid entry");
            }
            if (removed is ISlotGroupParent)
                Equal<ISlotGroupParent>(null, destinations.SlotGroupParentAt(removed.Position), stage + " removed cell cleared");
            Equal(true, destinations.AllGroupsListForReading.All(group =>
                ((Building)group.parent).Spawned && ((Building)group.parent).Map == Map), stage + " no despawned groups");
            // This traverses every native group, as does TryStartCarry's resource refresh.
            Counter.UpdateResourceCounts();
            long expectedStored = (UnitA.Network.CanWork ? 17 : 0) + (UnitB.Network.CanWork ? 29 : 0);
            Equal(expectedStored, (long)Counter.GetCount(Stored), stage + " visible stored resources");
            int expectedBuffered = Map.Ground.Where(item => item.def == Buffered && item.Spawned &&
                destinations.SlotGroupParentAt(item.Position) != null).Sum(item => item.stackCount);
            Equal(expectedBuffered, Counter.GetCount(Buffered), stage + " visible buffered resources");
            var holders = new List<IThingHolder>(); Manager.GetChildHolders(holders);
            long total = UnitA.Inventory.GetTotalCount(Stored) + UnitB.Inventory.GetTotalCount(Stored) +
                Map.Ground.Where(item => item.Spawned).Sum(item => (long)item.stackCount) +
                holders.Cast<StorageRecoveryBatch>().Sum(batch => batch.PendingCount);
            Equal(64L, total, stage + " all items conserved");
            Equal(InitialErrors, Log.Errors.Count, stage + " no registration errors");
            Equal(1, destinations.MaxSortDepth, stage + " no reentrant sorting");
        }
    }

    internal static int Run()
    {
        RuntimeHelpers.RunClassConstructor(typeof(StorageResourceCounter).TypeHandle);
        int scenarios = 0;
        foreach (bool conflict in new[] { false, true })
        foreach (bool blockedDrop in new[] { false, true })
        foreach (string target in new[] { "receiver A", "receiver B", "core A", "core B", "bridge A", "bridge B" })
        {
            var f = new Fixture(conflict);
            string label = target + ", conflict=" + conflict + ", blockedDrop=" + blockedDrop;
            Equal(true, f.ReceiverA.CanWork, label + " first network initially works");
            Equal(!conflict, f.ReceiverB.CanWork, label + " second network initial state");
            Equal(false, ReferenceEquals(f.CoreA.Network, f.CoreB.Network), label + " two separate networks");
            Equal(conflict ? StorageNetworkStatus.MultipleCores : StorageNetworkStatus.Ready,
                f.CoreB.Network.Status, label + " second network status");
            f.Check(label + " before removal");
            Building removed = target == "receiver A" ? (Building)f.ReceiverA :
                target == "receiver B" ? f.ReceiverB : target == "core A" ? f.CoreA :
                target == "core B" ? f.CoreB : target == "bridge A" ? f.BridgeA : f.BridgeB;
            f.Map.DropBudget = blockedDrop ? 0 : int.MaxValue;
            removed.DeSpawn(DestroyMode.Deconstruct);
            Equal(false, removed.Spawned, label + " building despawned");
            Equal<Map>(null, removed.Map, label + " despawned building has no map");
            f.Check(label + " immediately after removal", removed);
            bool unitAReady = target != "core A";
            bool unitBReady = conflict ? target == "core B" : target != "core B";
            Equal(unitAReady, f.UnitA.Network.CanWork, label + " first network stock availability");
            Equal(unitBReady, f.UnitB.Network.CanWork, label + " second network stock availability");
            Equal(unitAReady && target != "receiver A" && target != "bridge A", f.ReceiverA.CanWork,
                label + " first receiver availability");
            Equal(unitBReady && target != "receiver B" && target != "bridge B", f.ReceiverB.CanWork,
                label + " second receiver availability");
            Equal((unitAReady ? 17 : 0) + (unitBReady ? 29 : 0), f.Counter.GetCount(f.Stored),
                label + " exact accessible stock after removal");
            if (target == "core B")
                Equal(conflict, f.ReceiverB.CanWork, label + " removing one of two cores restores intake");
            if (target == "bridge B")
                Equal(StorageNetworkStatus.NoCore, f.ReceiverB.Network.Status, label + " disconnected receiver has no core");
            Find.TickManager.TicksGame += 60; f.Manager.MapComponentTick();
            f.Check(label + " after rebuild", removed);
            f.Map.DropBudget = int.MaxValue;
            removed.SpawnSetup(f.Map, false);
            Find.TickManager.TicksGame += 1000; f.Manager.MapComponentTick();
            f.Check(label + " after respawn");
            Equal(true, f.ReceiverA.CanWork, label + " first network restored");
            Equal(!conflict, f.ReceiverB.CanWork, label + " second network restored");
            Equal(18, f.Counter.GetCount(f.Buffered), label + " both buffers count after recovery");
            var holders = new List<IThingHolder>(); f.Manager.GetChildHolders(holders);
            Equal(0L, holders.Cast<StorageRecoveryBatch>().Sum(batch => batch.PendingCount), label + " recovery drained");
            scenarios++;
        }
        Console.WriteLine("PASS: receiver removal regression (" + scenarios + " scenarios, " + assertions + " assertions; game boundaries stubbed).");
        return assertions;
    }
}
