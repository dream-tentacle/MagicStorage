using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class TransportSupplierTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string message)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }
    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit { SlotCapacity = 64 };
        internal readonly Building_TransportSupplier Supplier = new Building_TransportSupplier { Rotation = new Rot4 { value = 1 } };
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal StorageNetwork Net => Core.Network;
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Register(Supplier, 2); Manager.EnsureCurrent();
        }
        private void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageUnit unit) unit.node = node; else ((Building)thing).node = node;
            Manager.Register(node);
        }
        internal Thing Store(ThingDef def, int count, string variant = null)
        {
            var item = new Thing { def = def, stackCount = count, variant = variant };
            Unit.Inventory.Contents.TryAdd(item, false); return item;
        }
        internal Thing Cargo(ThingDef def, int count)
        {
            var item = new Thing { def = def, stackCount = count };
            Supplier.Contents.TryAdd(item, false); return item;
        }
        internal CompTransporter Target(float capacity, int x = 3, int z = 0)
        {
            var target = new ThingWithComps { Spawned = true, Map = Map, Position = new IntVec3(x, z) };
            var comp = new CompTransporter { parent = target, MassCapacity = capacity };
            target.AllComps.Add(comp); Map.Ground.Add(target); return comp;
        }
        internal CompRefuelable FuelTarget(params ThingDef[] fuels)
        {
            var target = new ThingWithComps { Spawned = true, Map = Map, Position = new IntVec3(3, 0), Faction = Faction.OfPlayer };
            var comp = new CompRefuelable { parent = target };
            foreach (ThingDef def in fuels) comp.Props.fuelFilter.SetAllow(def, true);
            target.AllComps.Add(comp); Map.Ground.Add(target); return comp;
        }
        internal void Select(int slot, ThingDef def)
        {
            StorageSettings.EverStorableFixedSettings().filter.SetAllow(def, true);
            Equal(true, Supplier.SetSelection(slot, def), "supply item selected");
        }
        internal long Count(ThingOwner owner, ThingDef def) => owner.Where(item => item.def == def).Sum(item => (long)item.stackCount);
        internal List<StorageTransportRow> Rows() => StorageTransportExchange.Snapshot(Supplier, Net);
        internal bool Execute(List<StorageTransportRow> rows, double capacity, out string error) =>
            StorageTransportExchange.Execute(Supplier, Net, rows, capacity, out error);
    }
    internal static int Run()
    {
        assertions = 0;
        TwoSidedInventory(); WeightLimits(); Reservations(); ReturnRejected(); FullNetworkSwap();
        AllReturnsReservedTogether(); AutoLoadingAndDirections(); Preservation(); QuantityEditing(); MassStoppingPoints();
        ManualFuel(); SupplyModes(); DirectTransport(); DirectFuel(); AtomicFuel(); ImmediateSupply(); CachedPreview();
        return assertions;
    }
    private static void ManualFuel()
    {
        var f = new Fixture(); var fuel = new ThingDef(); var other = new ThingDef();
        var target = f.FuelTarget(fuel); target.TargetFuelLevel = 10; target.Fuel = 2;
        f.Cargo(fuel, 12); f.Cargo(other, 5); f.Supplier.SupplyTransporter();
        Equal(10f, target.Fuel, "native fuel delivery reaches target level");
        Equal(4L, f.Count(f.Supplier.Contents, fuel), "excess fuel retained");
        Equal(5L, f.Count(f.Supplier.Contents, other), "wrong fuel retained");
        f.Supplier.SupplyTransporter(); Equal(1, target.RefuelCalls, "full fuel target is not fed again");
        target.Fuel = 0; target.allowAutoRefuel = false; f.Supplier.SupplyTransporter();
        Equal(0f, target.Fuel, "automatic refueling toggle respected");
        target.allowAutoRefuel = true; target.Props.allowRefuelIfNotEmpty = false; target.Fuel = 1;
        f.Supplier.SupplyTransporter(); Equal(1f, target.Fuel, "empty-only refueling respected");
        target.Props.allowRefuelIfNotEmpty = true; target.Fuel = 9.5f;
        f.Supplier.SupplyTransporter(); Equal(4L, f.Count(f.Supplier.Contents, fuel), "sub-unit fuel gap consumes nothing");
        target.Fuel = 0; target.Props.FuelMultiplierCurrentDifficulty = 2; target.TargetFuelLevel = 6;
        f.Supplier.SupplyTransporter(); Equal(6f, target.Fuel, "native per-item fuel multiplier applied");
        Equal(1L, f.Count(f.Supplier.Contents, fuel), "multiplier consumes correct item count");
        target.parent.Faction = new Faction(); target.Fuel = 0; f.Supplier.SupplyTransporter();
        Equal(0f, target.Fuel, "foreign fuel target ignored");
        var half = new Fixture(); var halfTarget = half.FuelTarget(fuel);
        halfTarget.Props.fuelCapacity = 60; halfTarget.TargetFuelLevel = 60; halfTarget.Fuel = 30;
        half.Cargo(fuel, 60); half.Supplier.SupplyTransporter();
        Equal(30f, halfTarget.Fuel, "manual supplier respects native threshold on half-full generator");
        Equal(60L, half.Count(half.Supplier.Contents, fuel), "manual stock remains above threshold");
        halfTarget.Fuel = 18; half.Supplier.SupplyTransporter();
        Equal(60f, halfTarget.Fuel, "manual supplier refills at native threshold");
        Equal(18L, half.Count(half.Supplier.Contents, fuel), "manual supply consumes missing 42 units");
    }
    private static void ImmediateSupply()
    {
        var manual = new Fixture(); var fuel = new ThingDef(); var target = manual.FuelTarget(fuel);
        target.Props.fuelCapacity = 60; target.TargetFuelLevel = 60; target.Fuel = 30; manual.Cargo(fuel, 60);
        var button = manual.Supplier.GetGizmos().OfType<Command_Action>().Last();
        Equal("MS_FuelSupply_Immediate", button.defaultLabel, "material supplier has immediate gizmo");
        button.action(); Equal(60f, target.Fuel, "manual immediate command refuels above threshold");
        Equal(30L, manual.Count(manual.Supplier.Contents, fuel), "manual immediate consumes exact missing fuel");
        target.Fuel = 30; target.allowAutoRefuel = false; button.action();
        Equal(30f, target.Fuel, "manual immediate preserves automatic refueling switch");
        Equal(30L, manual.Count(manual.Supplier.Contents, fuel), "disabled target consumes no manual stock");
        var direct = new Fixture(); var item = direct.Store(fuel, 60); direct.Select(0, fuel); direct.Supplier.TrySwitchMode();
        target = direct.FuelTarget(fuel); target.Props.fuelCapacity = 60; target.TargetFuelLevel = 60; target.Fuel = 30;
        object claim = new object(); direct.Net.ReserveOutgoing(claim, item, 40);
        direct.Supplier.GetGizmos().OfType<Command_Action>().Last().action();
        Equal(50f, target.Fuel, "continuous immediate uses only unreserved stock");
        Equal(40L, direct.Net.GetTotalCount(fuel), "reserved stock retained after immediate command");
        Equal(0, direct.Supplier.Contents.Count, "continuous immediate has no supplier buffer");
        target.Fuel = 30; target.allowAutoRefuel = false; direct.Net.ReleaseOutgoing(claim);
        direct.Supplier.GetGizmos().OfType<Command_Action>().Last().action();
        Equal(30f, target.Fuel, "continuous immediate preserves target switch");
        var pod = new Fixture(); pod.Cargo(fuel, 8); var transport = pod.Target(5);
        pod.Supplier.GetGizmos().OfType<Command_Action>().Last().action();
        Equal(5L, pod.Count(transport.innerContainer, fuel), "immediate material supply also loads transporter");
        Equal(3L, pod.Count(pod.Supplier.Contents, fuel), "immediate transporter load respects capacity");
        var empty = new Fixture(); empty.Target(10);
        Equal(true, empty.Supplier.GetGizmos().OfType<Command_Action>().Last().disabled, "empty manual supplier disables immediate command");
    }
    private static void SupplyModes()
    {
        var f = new Fixture(); var def = new ThingDef(); Thing cargo = f.Cargo(def, 2);
        var toggle = f.Supplier.GetGizmos().OfType<Command_Action>().First();
        Equal(true, toggle.disabled, "loaded supplier uses native disabled gizmo");
        Equal("MS_Transport_ModeBlocked", toggle.disabledReason, "native tooltip receives reason without markup");
        Equal(false, f.Supplier.TrySwitchMode(), "loaded supplier cannot switch");
        Equal(2L, f.Count(f.Supplier.Contents, def), "switch neither deletes nor returns contents");
        f.Supplier.Contents.Remove(cargo); f.Target(10).innerContainer.TryAdd(cargo);
        Equal(true, f.Supplier.TrySwitchMode(), "target contents do not block mode switch");
        Equal(true, f.Supplier.DirectSupply, "continuous mode selected");
        Equal(false, f.Supplier.GetGizmos().OfType<Command_Action>().First().disabled, "empty supplier enables native gizmo");
        f.Select(0, def); Equal(false, f.Supplier.SetSelection(1, def), "duplicate supply item rejected");
        Equal(false, f.Supplier.SetSelection(3, def), "fourth slot rejected");
        Equal(true, f.Supplier.SetSelection(0, null), "supply slot cleared");
        Equal(false, f.Execute(f.Rows(), 150, out _), "stale manual exchange cannot store items in continuous mode");
        Equal(true, f.Supplier.TrySwitchMode(), "empty supplier can switch back");
        Equal(false, f.Supplier.DirectSupply, "manual mode restored");
    }
    private static void DirectTransport()
    {
        var f = new Fixture(); var high = new ThingDef(); var low = new ThingDef(); var absent = new ThingDef();
        var highStack = f.Store(high, 10); f.Store(low, 10); f.Select(0, high); f.Select(1, absent); f.Select(2, low);
        var target = f.Target(8); object claim = new object(); f.Net.ReserveOutgoing(claim, highStack, 5);
        f.Supplier.TrySwitchMode(); f.Supplier.SupplyTransporter();
        Equal(5L, f.Count(target.innerContainer, high), "higher priority supplied first while excluding reservations");
        Equal(3L, f.Count(target.innerContainer, low), "missing middle priority skipped and lower fills capacity");
        Equal(5L, f.Net.GetTotalCount(high), "reserved source stock remains");
        Equal(7L, f.Net.GetTotalCount(low), "lower priority only supplies remaining capacity");
        Equal(0, f.Supplier.Contents.Count, "continuous transport has no supplier buffer");
        f.Supplier.SupplyTransporter(); Equal(7L, f.Net.GetTotalCount(low), "full target causes no withdrawal");
        target.MassCapacity = 100; f.Manager.Unregister(f.Supplier.node); f.Supplier.SupplyTransporter();
        Equal(7L, f.Unit.Inventory.GetTotalCount(low), "disconnected continuous supplier cannot deliver");
        Equal(0, f.Supplier.Contents.Count, "disconnect creates no supplier stock");
        var mixed = new Fixture(); var heavy = new ThingDef { transportMass = 20 }; var light = new ThingDef();
        mixed.Store(heavy, 1); mixed.Store(light, 3); mixed.Select(0, heavy); mixed.Select(1, light);
        mixed.Supplier.TrySwitchMode(); var pod = mixed.Target(5); mixed.Supplier.SupplyTransporter();
        Equal(0L, mixed.Count(pod.innerContainer, heavy), "unfit high priority stays in network");
        Equal(3L, mixed.Count(pod.innerContainer, light), "lower priority can use otherwise unusable capacity");
    }
    private static void DirectFuel()
    {
        var f = new Fixture(); var high = new ThingDef(); var low = new ThingDef(); var wrong = new ThingDef();
        var highStack = f.Store(high, 10); f.Store(low, 10); f.Store(wrong, 20);
        f.Select(0, high); f.Select(1, wrong); f.Select(2, low); object claim = new object();
        f.Net.ReserveOutgoing(claim, highStack, 7); var target = f.FuelTarget(high, low); target.TargetFuelLevel = 5;
        f.Supplier.TrySwitchMode(); f.Supplier.SupplyTransporter();
        Equal(5f, target.Fuel, "continuous supply refuels directly");
        Equal(7L, f.Net.GetTotalCount(high), "available higher-priority fuel used first");
        Equal(8L, f.Net.GetTotalCount(low), "lower-priority fuel completes remaining need");
        Equal(20L, f.Net.GetTotalCount(wrong), "incompatible selected item never withdrawn");
        Equal(0, f.Supplier.Contents.Count, "continuous fuel has no supplier storage");
        Equal(0, f.Map.Ground.Count(t => t.def.category == ThingCategory.Item && !(t is ThingWithComps)), "fuel does not spill to ground");
        target.Fuel = 0; target.parent.Position = new IntVec3(2, 1); f.Supplier.SupplyTransporter();
        Equal(0f, target.Fuel, "side fuel target ignored");
        var both = new Fixture(); var fuel = new ThingDef(); var fuelTarget = both.FuelTarget(fuel);
        var transporter = new CompTransporter { parent = fuelTarget.parent, MassCapacity = 5 };
        ((ThingWithComps)fuelTarget.parent).AllComps.Add(transporter); both.Cargo(fuel, 3); both.Supplier.SupplyTransporter();
        Equal(3L, both.Count(transporter.innerContainer, fuel), "transporter takes priority on dual-component target");
        Equal(0f, fuelTarget.Fuel, "dual-component target is not also refueled");
        var half = new Fixture(); half.Store(high, 60); half.Select(0, high); half.Supplier.TrySwitchMode();
        var halfTarget = half.FuelTarget(high); halfTarget.Props.fuelCapacity = 60;
        halfTarget.TargetFuelLevel = 60; halfTarget.Fuel = 30; half.Supplier.SupplyTransporter();
        Equal(30f, halfTarget.Fuel, "continuous material supply respects native threshold");
        Equal(60L, half.Net.GetTotalCount(high), "continuous supply keeps stock above threshold");
        halfTarget.Fuel = 18; half.Supplier.SupplyTransporter();
        Equal(60f, halfTarget.Fuel, "continuous supply refills at native threshold");
        Equal(18L, half.Net.GetTotalCount(high), "continuous supply consumes missing 42 units");
    }
    private static void AtomicFuel()
    {
        var f = new Fixture(); var a = new ThingDef(); var b = new ThingDef(); f.Select(0, a); f.Select(1, b);
        var target = f.FuelTarget(a, b); target.Props.atomicFueling = true; target.Props.fuelCapacity = 10; target.TargetFuelLevel = 10;
        f.Store(a, 4); f.Store(b, 5); f.Supplier.TrySwitchMode(); f.Supplier.SupplyTransporter();
        Equal(0f, target.Fuel, "atomic fuel waits for full amount");
        Equal(4L, f.Net.GetTotalCount(a), "insufficient atomic fuel leaves first type untouched");
        Equal(5L, f.Net.GetTotalCount(b), "insufficient atomic fuel leaves second type untouched");
        f.Store(b, 1); f.Supplier.SupplyTransporter(); Equal(10f, target.Fuel, "atomic fuel can combine selected types");
        Equal(0L, f.Net.GetTotalCount(a), "atomic first type consumed"); Equal(0L, f.Net.GetTotalCount(b), "atomic second type consumed");
        Equal(0, f.Supplier.Contents.Count, "atomic delivery never fills supplier");
        var manual = new Fixture(); target = manual.FuelTarget(a); target.Props.atomicFueling = true;
        target.Props.fuelCapacity = 10; target.TargetFuelLevel = 10; manual.Cargo(a, 9); manual.Supplier.SupplyTransporter();
        Equal(0, target.RefuelCalls, "manual atomic supply does not call Refuel with insufficient stock");
        Equal(9L, manual.Count(manual.Supplier.Contents, a), "manual atomic stock preserved");
    }
    private static void TwoSidedInventory()
    {
        var f = new Fixture(); var a = new ThingDef(); var b = new ThingDef { transportMass = 2 };
        f.Store(a, 30); f.Cargo(a, 5); f.Cargo(b, 20);
        var rows = f.Rows();
        Equal(2, rows.Count, "supplier-only stock remains visible");
        var shared = rows.Single(row => row.Representative.def == a);
        Equal(30L, shared.NetworkCount, "network side count"); Equal(5L, shared.SupplyCount, "supplier side count");
        shared.SetChange(10); rows.Single(row => row.Representative.def == b).SetChange(-7);
        Equal(20L, shared.NetworkAfter, "network preview"); Equal(15L, shared.SupplyAfter, "supplier preview");
        Equal(41d, StorageTransportExchange.ProjectedMass(f.Supplier, f.Net, rows), "bidirectional final mass");
        Equal(true, f.Execute(rows, 150, out _), "two-way exchange commits");
        Equal(20L, f.Net.GetTotalCount(a), "withdrawn network count"); Equal(7L, f.Net.GetTotalCount(b), "returned network count");
        Equal(15L, f.Count(f.Supplier.Contents, a), "supplier receives"); Equal(13L, f.Count(f.Supplier.Contents, b), "supplier returns");
        Equal(0, f.Map.Ground.Count, "exchange never spawns cargo on ground");
    }
    private static void WeightLimits()
    {
        var f = new Fixture(); var def = new ThingDef { transportMass = 2, stackLimit = 1000 };
        f.Store(def, 100); f.Cargo(def, 10); var rows = f.Rows(); rows[0].SetChange(66);
        Equal(152d, StorageTransportExchange.ProjectedMass(f.Supplier, f.Net, rows), "existing cargo counts toward limit");
        Equal(false, f.Execute(rows, 150, out string error), "overweight rejected"); Equal("MS_Transport_Overweight", error, "overweight reason");
        Equal(100L, f.Net.GetTotalCount(def), "overweight leaves network unchanged"); Equal(10L, f.Count(f.Supplier.Contents, def), "overweight leaves supplier unchanged");
        rows[0].SetChange(65); Equal(true, f.Execute(rows, 150, out _), "exact limit allowed"); Equal(150d, f.Supplier.StoredMass, "final mass exact");
        Equal(150f, f.Supplier.TransferCapacity, "default limit"); var target = f.Target(80);
        Equal(150f, f.Supplier.TransferCapacity, "smaller transporter never lowers minimum"); target.MassCapacity = 500;
        Equal(500f, f.Supplier.TransferCapacity, "total capacity used rather than remaining capacity");
        target.innerContainer.TryAdd(new Thing { def = def, stackCount = 200 });
        Equal(500f, f.Supplier.TransferCapacity, "loaded target still supplies total capacity");
        target.parent.DeSpawn(); Equal(150f, f.Supplier.TransferCapacity, "new window capacity drops when target leaves");
        var excess = new Fixture(); excess.Cargo(def, 100); rows = excess.Rows(); rows[0].SetChange(-25);
        Equal(true, excess.Execute(rows, 150, out _), "existing excess can be returned until final mass fits");
    }
    private static void Reservations()
    {
        var f = new Fixture(); var def = new ThingDef(); var item = f.Store(def, 40); object claim = new object();
        f.Net.ReserveOutgoing(claim, item, 30); var row = f.Rows()[0];
        Equal(10L, row.Available, "reserved items excluded from quantities"); row.SetChange(100); Equal(10L, row.Change, "positive amount clamped");
        Equal(true, f.Execute(new List<StorageTransportRow> { row }, 150, out _), "available part transfers");
        Equal(30L, f.Net.GetTotalCount(def), "reservation remains untouched"); f.Net.ReleaseOutgoing(claim);
        var rows = f.Rows(); rows[0].SetChange(30); f.Net.ReserveOutgoing(claim, item, 20);
        Equal(false, f.Execute(rows, 150, out _), "new reservation invalidates stale selection");
        Equal(30L, f.Net.GetTotalCount(def), "stale commit moves nothing"); f.Net.ReleaseOutgoing(claim);
        var old = f.Net; rows = f.Rows(); rows[0].SetChange(1); f.Manager.MarkDirty(); f.Manager.EnsureCurrent();
        Equal(false, StorageTransportExchange.Execute(f.Supplier, old, rows, 150, out _), "old topology refused");
    }
    private static void ReturnRejected()
    {
        var f = new Fixture(); var a = new ThingDef(); var b = new ThingDef(); f.Store(a, 20); f.Cargo(b, 10);
        var rows = f.Rows(); rows.Single(row => row.Representative.def == a).SetChange(5); rows.Single(row => row.Representative.def == b).SetChange(-10);
        f.Core.Settings.allow = false;
        Equal(false, f.Execute(rows, 150, out string error), "filter-rejected return cancels exchange");
        Equal("MS_Transport_NetworkCannotAccept", error, "return rejection reason");
        Equal(20L, f.Net.GetTotalCount(a), "withdrawal restored after rejected return"); Equal(10L, f.Count(f.Supplier.Contents, b), "return retained in supplier");
        Equal(0L, f.Count(f.Supplier.Contents, a), "no partial incoming delivery"); Equal(0, f.Map.Ground.Count, "rejected return not dropped");
        Equal(20L, f.Rows().Single(row => row.Representative.def == a).Available, "failed transaction releases claims");
    }
    private static void FullNetworkSwap()
    {
        var f = new Fixture(); f.Unit.SlotCapacity = 1; var a = new ThingDef(); var b = new ThingDef();
        f.Store(a, 10); f.Cargo(b, 20); var rows = f.Rows(); rows.Single(row => row.Representative.def == b).SetChange(-20);
        Equal(false, f.Execute(rows, 150, out _), "full network rejects return"); Equal(20L, f.Count(f.Supplier.Contents, b), "full-network return preserved");
        rows = f.Rows(); rows.Single(row => row.Representative.def == a).SetChange(10); rows.Single(row => row.Representative.def == b).SetChange(-20);
        Equal(true, f.Execute(rows, 150, out _), "withdrawal frees slot for same exchange's return");
        Equal(20L, f.Net.GetTotalCount(b), "returned stock occupies freed slot"); Equal(10L, f.Count(f.Supplier.Contents, a), "outgoing stock reaches supplier");
    }
    private static void AllReturnsReservedTogether()
    {
        var f = new Fixture(); f.Unit.SlotCapacity = 1; var a = new ThingDef(); var b = new ThingDef();
        f.Cargo(a, 5); f.Cargo(b, 5); var rows = f.Rows(); foreach (var row in rows) row.SetChange(-5);
        Equal(false, f.Execute(rows, 150, out _), "two returns cannot compete for one free slot");
        Equal(5L, f.Count(f.Supplier.Contents, a), "first return untouched if second cannot fit");
        Equal(5L, f.Count(f.Supplier.Contents, b), "second return untouched"); Equal(0L, f.Net.UsedSlots, "no partial return before reservation completes");
    }
    private static void AutoLoadingAndDirections()
    {
        var f = new Fixture(); var def = new ThingDef { transportMass = 2 }; f.Cargo(def, 20);
        int ticks = Find.TickManager.TicksGame;
        Find.TickManager.TicksGame = 0; f.Supplier.RunTransportTestTick();
        var target = f.Target(50, 2, 1); f.Supplier.SupplyTransporter(); Equal(20L, f.Count(f.Supplier.Contents, def), "side transporter ignored");
        target.parent.Position = new IntVec3(3, 0); target.innerContainer.TryAdd(new Thing { def = def, stackCount = 20 });
        try
        {
            Find.TickManager.TicksGame = 59; f.Supplier.RunTransportTestTick(); Equal(20L, f.Count(f.Supplier.Contents, def), "no transfer between one-second intervals");
            Find.TickManager.TicksGame = 60; f.Supplier.RunTransportTestTick(); Equal(15L, f.Count(f.Supplier.Contents, def), "split load respects remaining capacity");
            Equal(25L, f.Count(target.innerContainer, def), "target topped up to capacity");
            Find.TickManager.TicksGame = 120; f.Supplier.RunTransportTestTick(); Equal(15L, f.Count(f.Supplier.Contents, def), "full target leaves excess held");
        }
        finally { Find.TickManager.TicksGame = ticks; }
        target.parent.DeSpawn(); f.Supplier.SupplyTransporter(); Equal(15L, f.Count(f.Supplier.Contents, def), "departed target leaves cargo held");
        target = f.Target(100); f.Manager.Unregister(f.Supplier.node); f.Supplier.SupplyTransporter();
        Equal(0L, f.Count(f.Supplier.Contents, def), "held cargo can load after network disconnects"); Equal(15L, f.Count(target.innerContainer, def), "replacement target receives held cargo");
        Equal(0, StorageTransportExchange.CountThatFits(new Thing { def = new ThingDef { transportMass = 0.000001f } }, 0), "positive tiny mass cannot fit zero remaining capacity");
        Equal(3, StorageTransportExchange.CountThatFits(new Thing { def = new ThingDef { transportMass = 0 }, stackCount = 3 }, 0), "weightless cargo can fit at capacity");
    }
    private static void Preservation()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Cargo(def, 20); f.Map.DropBudget = 0;
        f.Supplier.Destroy(DestroyMode.Deconstruct); var holders = new List<IThingHolder>(); f.Manager.GetChildHolders(holders);
        Equal(20L, holders.OfType<StorageRecoveryBatch>().Sum(batch => batch.PendingCount), "blocked destruction release stays saved");
        f.Map.DropBudget = 100; foreach (var batch in holders.OfType<StorageRecoveryBatch>()) batch.TryRelease(f.Map, 8);
        Equal(20L, f.Map.Ground.Sum(item => (long)item.stackCount), "release preserves count");
        f = new Fixture(); for (int i = 0; i < 100; i++) f.Cargo(new ThingDef(), 1);
        Equal(100, f.Supplier.Contents.Count, "supplier has no storage-unit slot cap");
    }
    private static void QuantityEditing()
    {
        var row = new StorageTransportRow { Available = 5000000000L, SupplyCount = 100 };
        row.EditChange("-500"); Equal(-100L, row.Change, "return clamped to cargo");
        row.EditChange("5000000000"); Equal(5000000000L, row.Change, "long network count exact");
        row.EditChange("-"); Equal(0L, row.Change, "unfinished negative input clears selection");
        row.EditChange(long.MinValue.ToString()); Equal(-100L, row.Change, "minimum long safe");
    }

    private static void CachedPreview()
    {
        var f = new Fixture(); var a = new ThingDef { transportMass = 2, stackLimit = 1000 }; var b = new ThingDef { transportMass = 3 };
        f.Store(a, 200); f.Cargo(a, 10); f.Store(b, 30); f.Cargo(b, 10);
        var rows = f.Rows(); var row = rows.Single(r => r.Representative.def == a);
        var other = rows.Single(r => r.Representative.def == b);
        using (var preview = new StorageTransportPreview(f.Supplier, f.Net, rows, 150))
        {
            Thing.StatReads = 0;
            for (int i = 0; i < 100; i++)
            {
                Equal(50d, preview.StoredMass, "cached initial cargo mass");
                Equal(50d, preview.ProjectedMass, "unchanged preview mass");
                Equal(50L, preview.MassLimit(row), "cached weight stopping point");
            }
            Equal(0, Thing.StatReads, "repeated preview reads never request mass stats");
            other.SetChange(10);
            Equal(80d, preview.ProjectedMass, "editing a different row updates total weight");
            Equal(35L, preview.MassLimit(row), "editing a different row invalidates cached stopping point");
            other.SetChange(-10); row.SetChange(65);
            Equal(150d, preview.ProjectedMass, "planned returns free cached capacity");
            Equal(65L, preview.MassLimit(row), "cached return and withdrawal limit agrees with live algorithm");
            row.EditChange("-");
            Equal(20d, preview.ProjectedMass, "unfinished quantity entry clears selection and refreshes preview");
            Equal("-", row.Buffer, "unfinished input is retained");
            preview.ResetSelections();
            Equal(50d, preview.ProjectedMass, "reset returns weight to original cargo");
            Equal(true, rows.All(r => r.Change == 0), "reset clears all rows together");
            Equal(0, Thing.StatReads, "edits and resets reuse captured stack masses");
            row.SetChange(50); a.transportMass = 4;
            Equal(150d, preview.ProjectedMass, "preview remains the paused inventory snapshot");
            Equal(false, f.Execute(rows, 150, out string error), "confirmation rejects mass changed outside the paused window");
            Equal("MS_Transport_Overweight", error, "confirmation uses live weight validation");
            Equal(true, Thing.StatReads > 0, "confirmation reads native mass stats afresh");
            Equal(200L, f.Net.GetTotalCount(a), "rejected stale preview preserves stored stock");
            Equal(10L, f.Count(f.Supplier.Contents, a), "rejected stale preview preserves cargo");
        }
        using (var refreshed = new StorageTransportPreview(f.Supplier, f.Net, f.Rows(), 150))
        {
            Equal(70d, refreshed.StoredMass, "refresh captures changed actual masses");
        }
        var mixed = new Fixture(); var def = new ThingDef();
        Thing light = mixed.Store(def, 10), heavy = mixed.Store(def, 10); heavy.Stuff = new ThingDef { transportMass = 4 };
        var grouped = new StorageTransportRow { Available = 20, NetworkCount = 20 };
        grouped.NetworkStacks.Add(light); grouped.NetworkStacks.Add(heavy);
        using (var preview = new StorageTransportPreview(mixed.Supplier, mixed.Net, new List<StorageTransportRow> { grouped }, 20))
        {
            Equal(12L, preview.MassLimit(grouped), "cache preserves different actual masses within one group");
            grouped.SetChange(12); Equal(18d, preview.ProjectedMass, "cached mixed-stack preview matches actual transfer order");
        }
    }

    private static void MassStoppingPoints()
    {
        var f = new Fixture(); var a = new ThingDef { transportMass = 2, stackLimit = 1000 };
        var b = new ThingDef { transportMass = 3 };
        f.Store(a, 200); f.Cargo(a, 10); f.Store(b, 30); f.Cargo(b, 10);
        var rows = f.Rows(); var row = rows.Single(r => r.Representative.def == a);
        var other = rows.Single(r => r.Representative.def == b);
        Equal(50L, StorageTransportExchange.MassLimitChange(f.Supplier, f.Net, rows, row, 150), "M includes existing cargo");
        other.SetChange(10);
        Equal(35L, StorageTransportExchange.MassLimitChange(f.Supplier, f.Net, rows, row, 150), "M includes every other selection");
        other.SetChange(-10);
        Equal(65L, StorageTransportExchange.MassLimitChange(f.Supplier, f.Net, rows, row, 150), "M uses space freed by planned returns");
        row.SetChange(-5);
        long limit = StorageTransportExchange.MassLimitChange(f.Supplier, f.Net, rows, row, 150);
        Equal(65L, limit, "M replaces a negative selection with the maximum incoming change");
        Equal(-5L, row.Change, "finding the M threshold does not edit a selection");
        row.SetChange(limit);
        Equal(150d, StorageTransportExchange.ProjectedMass(f.Supplier, f.Net, rows), "M reaches the exact limit");
        row.SetChange(limit + 1);
        Equal(152d, StorageTransportExchange.ProjectedMass(f.Supplier, f.Net, rows), "next item would exceed the limit");
        Equal(200L, StorageTransportExchange.MassLimitChange(f.Supplier, f.Net, rows, row, 500), "M uses captured transporter capacity and clamps to stock");

        object claim = new object(); f.Net.ReserveOutgoing(claim, row.NetworkStacks[0], 180);
        Equal(20L, StorageTransportExchange.MassLimitChange(f.Supplier, f.Net, rows, row, 150), "M excludes reservations added after the snapshot");
        f.Net.ReleaseOutgoing(claim);

        var mixed = new Fixture(); var def = new ThingDef { stackLimit = 1000 };
        Thing light = mixed.Store(def, 10), heavy = mixed.Store(def, 10);
        heavy.Stuff = new ThingDef { transportMass = 4 };
        var grouped = new StorageTransportRow { Available = 20, NetworkCount = 20 };
        grouped.NetworkStacks.Add(light); grouped.NetworkStacks.Add(heavy);
        rows = new List<StorageTransportRow> { grouped };
        Equal(12L, StorageTransportExchange.MassLimitChange(mixed.Supplier, mixed.Net, rows, grouped, 20), "M weighs actual mixed stacks rather than the representative");
        grouped.SetChange(12);
        Equal(18d, StorageTransportExchange.ProjectedMass(mixed.Supplier, mixed.Net, rows), "M allows only complete items");

        var excess = new Fixture(); excess.Cargo(a, 100); rows = excess.Rows();
        Equal(-25L, StorageTransportExchange.MassLimitChange(excess.Supplier, excess.Net, rows, rows[0], 150), "overweight stock has a negative mass threshold");

        var zero = new Fixture(); def = new ThingDef { transportMass = 0, stackLimit = int.MaxValue };
        for (int i = 0; i < 3; i++) { zero.Unit.SlotCapacity = i + 1; zero.Store(def, 2000000000); }
        rows = zero.Rows();
        Equal(6000000000L, StorageTransportExchange.MassLimitChange(zero.Supplier, zero.Net, rows, rows[0], 0), "weightless items and long quantities can all fit");
        var tiny = new Fixture(); def = new ThingDef { transportMass = 0.000001f };
        tiny.Store(def, 10); rows = tiny.Rows();
        Equal(0L, StorageTransportExchange.MassLimitChange(tiny.Supplier, tiny.Net, rows, rows[0], 0), "tolerance cannot add a whole tiny item at zero capacity");

        var fraction = new Fixture(); def = new ThingDef { transportMass = 0.1f, stackLimit = 1000 };
        fraction.Store(def, 100); rows = fraction.Rows();
        Equal(10L, StorageTransportExchange.MassLimitChange(fraction.Supplier, fraction.Net, rows, rows[0], 1), "float stats can fill a mathematical exact limit");
    }
}
