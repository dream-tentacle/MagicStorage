using System;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using Verse;

internal static class StorageResourceCounterTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string message)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(message + ": " + expected + " != " + actual); }
    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly ThingDef Food = new ThingDef(), Medicine = new ThingDef();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit First = new Building_StorageUnit(), Second = new Building_StorageUnit();
        internal readonly ResourceCounter Counter;
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Map.ResourceDefs.Add(Food); Map.ResourceDefs.Add(Medicine);
            Register(Core, 0); Register(First, 1); Register(Second, 2);
            Manager.FinalizeInit();
            Counter = new ResourceCounter(Map);
        }
        internal void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageUnit unit) unit.node = node;
            if (thing is Building_StorageCore core) core.node = node;
            Manager.Register(node);
        }
        internal Thing Store(Building_StorageUnit unit, ThingDef def, int count)
        {
            var item = new Thing { def = def, stackCount = count };
            if (!unit.Inventory.Contents.TryAdd(item, false)) throw new Exception("fixture insertion failed");
            return item;
        }
    }
    internal static int Run()
    {
        RuntimeHelpers.RunClassConstructor(typeof(StorageResourceCounter).TypeHandle);
        CountingAndEligibility(); OwnershipAndTopology(); TransfersAndClaims();
        return assertions;
    }
    private static void CountingAndEligibility()
    {
        var f = new Fixture();
        f.Map.NativeStoredResources.Add(new Thing { def = f.Food, stackCount = 10 });
        Thing first = f.Store(f.First, f.Food, 20);
        f.Store(f.Second, f.Food, 30); f.Store(f.First, f.Medicine, 4);
        f.Counter.UpdateResourceCounts();
        Equal(60, f.Counter.GetCount(f.Food), "native ground stock plus two units");
        Equal(4, f.Counter.GetCount(f.Medicine), "network-only resource added");
        Equal(4, f.Counter.EligibilityChecks, "native eligibility used for stored items");
        f.Counter.UpdateResourceCounts();
        Equal(60, f.Counter.GetCount(f.Food), "refresh does not accumulate previous additions");
        f.Map.ExcludedResources.Add(first); f.Counter.UpdateResourceCounts();
        Equal(40, f.Counter.GetCount(f.Food), "native freshness or fog rejection respected");
        f.Map.ExcludedResources.Clear();
        var uncounted = new ThingDef { CountAsResource = false };
        f.Store(f.First, uncounted, 7);
        var missing = new ThingDef(); f.Store(f.First, missing, 8);
        f.Counter.UpdateResourceCounts();
        Equal(false, f.Counter.AllCountedAmounts.ContainsKey(uncounted), "nonresources not inserted");
        Equal(false, f.Counter.AllCountedAmounts.ContainsKey(missing), "native resource catalog retained");
        Equal(60, f.Counter.GetCount(f.Food), "unrelated definitions do not change totals");
        f.Map.NativeStoredResources.Add(new MinifiedThing { InnerThing = new Thing { def = f.Medicine, stackCount = 2 } });
        var packed = new MinifiedThing { InnerThing = new Thing { def = f.Medicine, stackCount = 3 } };
        f.First.Inventory.Contents.TryAdd(packed, false); f.Counter.UpdateResourceCounts();
        Equal(9, f.Counter.GetCount(f.Medicine), "minified inner resource follows native count convention");
        f.Map.NativeStoredResources.Clear();
        f.Map.NativeStoredResources.Add(new Thing { def = f.Food, stackCount = int.MaxValue - 1 });
        f.Counter.UpdateResourceCounts();
        Equal(int.MaxValue, f.Counter.GetCount(f.Food), "large totals do not overflow to negative");
    }
    private static void OwnershipAndTopology()
    {
        var f = new Fixture(); f.Store(f.First, f.Food, 20); f.Store(f.Second, f.Food, 30);
        f.Second.Faction = new Faction(); f.Counter.UpdateResourceCounts();
        Equal(20, f.Counter.GetCount(f.Food), "other factions excluded");
        f.Second.Faction = Faction.OfPlayer; f.Second.Spawned = false; f.Counter.UpdateResourceCounts();
        Equal(20, f.Counter.GetCount(f.Food), "unspawned units excluded");
        f.Second.Spawned = true; f.Second.Destroyed = true; f.Counter.UpdateResourceCounts();
        Equal(20, f.Counter.GetCount(f.Food), "destroyed units excluded");
        f.Second.Destroyed = false;
        f.Manager.Unregister(f.Second.node); f.Counter.UpdateResourceCounts();
        Equal(20, f.Counter.GetCount(f.Food), "unregistered units removed immediately");
        f.Manager.Register(f.Second.node); f.Manager.Register(f.Second.node); f.Counter.UpdateResourceCounts();
        Equal(50, f.Counter.GetCount(f.Food), "duplicate registration cannot duplicate counts");
        f.Manager.Unregister(f.Core.node); f.Manager.EnsureCurrent(); f.Counter.UpdateResourceCounts();
        Equal(false, f.First.Network.CanWork, "fixture network disconnected");
        Equal(50, f.Counter.GetCount(f.Food), "disconnected stock still owned and counted");
        var other = new Fixture(); other.Store(other.First, other.Food, 11); other.Counter.UpdateResourceCounts();
        Equal(11, other.Counter.GetCount(other.Food), "maps have independent counts");
        Equal(50, f.Counter.GetCount(f.Food), "other map refresh leaves first map alone");
    }
    private static void TransfersAndClaims()
    {
        var f = new Fixture(); Thing source = f.Store(f.First, f.Food, 40);
        object claim = new object();
        Equal(15, f.Core.Network.ReserveOutgoing(claim, source, 15), "outgoing quantity reserved");
        f.Counter.UpdateResourceCounts();
        Equal(40, f.Counter.GetCount(f.Food), "reservations do not subtract owned inventory");
        f.Core.Network.ReleaseOutgoing(claim);
        var shelf = new Building_StorageSupplyShelf { Map = f.Map, Spawned = true, Faction = Faction.OfPlayer };
        shelf.node = new CompStorageNode { parent = shelf }; f.Manager.Register(shelf.node);
        f.Manager.EnsureCurrent();
        var destination = new ThingOwner(null);
        Equal(12, f.Core.Network.TryTransferTo(source, 12, destination).Transferred, "real withdrawal");
        Thing output = destination[0]; destination.Remove(output);
        f.Map.NativeStoredResources.Add(output); f.Counter.UpdateResourceCounts();
        Equal(40, f.Counter.GetCount(f.Food), "shelf withdrawal changes location without double counting");
        f.Map.NativeStoredResources.Clear(); f.Counter.UpdateResourceCounts();
        Equal(28, f.Counter.GetCount(f.Food), "consumed ground stock disappears from total");
        f.Store(f.Second, f.Food, 5); f.Counter.UpdateResourceCounts();
        Equal(33, f.Counter.GetCount(f.Food), "newly stored stock appears on refresh");
    }
}
