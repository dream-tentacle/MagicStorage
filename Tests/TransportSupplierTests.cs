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
        PresetClipboard(); PresetModeGuard(); PresetContainerTargets();
        TwoSidedInventory(); WeightLimits(); Reservations(); ReturnRejected(); FullNetworkSwap();
        AllReturnsReservedTogether(); AutoLoadingAndDirections(); Preservation(); QuantityEditing(); MassStoppingPoints();
        ManualFuel(); SupplyModes(); DirectTransport(); DirectFuel(); AtomicFuel(); ImmediateSupply(); CachedPreview();
        SharedLaunchCapacity(); AutomaticLoadingState();
        TargetInventoryLimits(); TransportInventoryLimits(); TargetLimitPersistence();
        FilterGroupingAndSelections();
        GenericManualSupply(); GenericDirectSupply(); GenericContainerSelection(); GenericTargetRules();
        VehicleSupply(); MultipleFuelComponents();
        return assertions;
    }

    private static void MultipleFuelComponents()
    {
        foreach (bool direct in new[] { false, true })
        {
            var f = new Fixture();
            var fuelA = new ThingDef(); var fuelB = new ThingDef();
            if (direct)
            {
                f.Supplier.TrySwitchMode(); f.Select(0, fuelA); f.Select(1, fuelB);
                f.Store(fuelA, 40); f.Store(fuelB, 40);
            }
            else { f.Cargo(fuelA, 40); f.Cargo(fuelB, 40); }
            var first = f.FuelTarget(fuelA); first.TargetFuelLevel = 10; first.Fuel = 10;
            var second = new CompRefuelable { parent = first.parent, TargetFuelLevel = 10 };
            second.Props.fuelFilter.SetAllow(fuelB, true);
            ((ThingWithComps)first.parent).AllComps.Add(second);
            var other = f.FuelTarget(fuelA); other.TargetFuelLevel = 10;
            f.Supplier.SupplyTransporter();
            Equal(10f, second.Fuel, "later component supplied in mode " + direct);
            Equal(10f, other.Fuel, "second object in front cell supplied in mode " + direct);
            Equal(30L, direct ? f.Net.GetTotalCount(fuelA) : f.Count(f.Supplier.Contents, fuelA), "first fuel consumed once");
            Equal(30L, direct ? f.Net.GetTotalCount(fuelB) : f.Count(f.Supplier.Contents, fuelB), "component fuel filter respected");
            first.Fuel = 0; first.allowAutoRefuel = false; second.Fuel = 5; other.Fuel = 5;
            f.Supplier.SupplyTransporter();
            Equal(5f, second.Fuel, "automatic threshold respected on later component");
            Equal(5f, other.Fuel, "automatic threshold respected on later object");
            f.Supplier.GetGizmos().OfType<Command_Action>().Single(g => g.defaultLabel == "MS_FuelSupply_Immediate").action();
            Equal(0f, first.Fuel, "disabled component does not block other components");
            Equal(10f, second.Fuel, "immediate command supplies later component");
            Equal(10f, other.Fuel, "immediate command supplies later object");
            Equal(25L, direct ? f.Net.GetTotalCount(fuelA) : f.Count(f.Supplier.Contents, fuelA), "immediate first fuel deficit exact");
            Equal(25L, direct ? f.Net.GetTotalCount(fuelB) : f.Count(f.Supplier.Contents, fuelB), "immediate second fuel deficit exact");
            Equal(3, f.Supplier.FrontRefuelables().Count, "all front fuel components found");
            Equal(3, f.Supplier.GetInspectString().Split('\n').Count(line => line.Contains("MS_Transport_FuelTarget")),
                "inspection includes each fuel component");
        }
    }

    private static Vehicles.VehiclePawn VehicleTarget(Fixture f, float capacity)
    {
        var vehicle = new Vehicles.VehiclePawn { Spawned = true, Map = f.Map, Position = f.Supplier.FrontCell,
            Faction = Faction.OfPlayer, Capacity = capacity };
        vehicle.def.category = ThingCategory.Pawn;
        f.Map.Ground.Add(vehicle);
        return vehicle;
    }

    private static void VehicleSupply()
    {
        var f = new Fixture(); var def = new ThingDef { transportMass = 2f };
        var vehicle = VehicleTarget(f, 20f); f.Cargo(def, 15);
        f.Supplier.SupplyTransporter();
        Equal(0, vehicle.CargoAdded, "vehicle support stays inactive before compatibility initialization");
        new VehicleFrameworkCompatibility().Apply(null);
        vehicle.VehicleGearMass = 2f;
        vehicle.inventory.innerContainer.TryAdd(new Thing { def = def, stackCount = 2 });
        vehicle.PendingCargo[def] = 10;
        Equal(false, f.Supplier.GenericContainerSupply, "vehicle dedicated supply needs no generic toggle");
        Equal(false, f.Supplier.GetGizmos().OfType<Command_Action>().Last().disabled, "vehicle enables immediate supply");
        f.Supplier.GetGizmos().OfType<Command_Action>().Last().action();
        Equal(9L, f.Count(vehicle.inventory.innerContainer, def), "manual cargo respects existing cargo and gear mass");
        Equal(8L, f.Count(f.Supplier.Contents, def), "manual overflow stays in supplier");
        Equal(1, vehicle.CargoAdded, "manual cargo uses native loading event");
        Equal(3, vehicle.PendingCargo[def], "manual cargo advances native pending manifest");
        f.Supplier.SupplyTransporter();
        Equal(1, vehicle.CargoAdded, "full vehicle accepts no additional cargo");
        vehicle.Capacity = 400f;
        Equal(400f, f.Supplier.TransferCapacity, "manual transfer window uses vehicle capacity");

        f = new Fixture(); vehicle = VehicleTarget(f, 100f); f.Cargo(def, 4);
        vehicle.Position = new IntVec3(4, 0); f.Supplier.SupplyTransporter();
        Equal(0, vehicle.CargoAdded, "vehicle beside front cell ignored");
        vehicle.Position = f.Supplier.FrontCell; vehicle.Faction = new Faction(); f.Supplier.SupplyTransporter();
        Equal(0, vehicle.CargoAdded, "foreign vehicle ignored");
        vehicle.Faction = Faction.OfPlayer; vehicle.Spawned = false; f.Supplier.SupplyTransporter();
        Equal(0, vehicle.CargoAdded, "despawned vehicle ignored");
        vehicle.Spawned = true; vehicle.Reject = true; f.Supplier.SupplyTransporter();
        Equal(4L, f.Count(f.Supplier.Contents, def), "rejected manual cargo is restored");
        vehicle.Reject = false; vehicle.AcceptLimit = 2; f.Supplier.SupplyTransporter();
        Equal(2L, f.Count(vehicle.inventory.innerContainer, def), "partial native acceptance retained");
        Equal(2L, f.Count(f.Supplier.Contents, def), "manual partial remainder restored");

        var high = new ThingDef { transportMass = 2f }; var low = new ThingDef { transportMass = 1f };
        f = new Fixture(); vehicle = VehicleTarget(f, 14f);
        f.Store(high, 10); f.Store(low, 10); f.Select(0, high); f.Select(1, low);
        f.Supplier.SetTargetLimit(0, 5); f.Supplier.SetTargetLimit(1, 10); f.Supplier.TrySwitchMode();
        vehicle.inventory.innerContainer.TryAdd(new Thing { def = high, stackCount = 2 });
        vehicle.PendingCargo[high] = 9;
        f.Supplier.SupplyTransporter();
        Equal(5L, f.Count(vehicle.inventory.innerContainer, high), "continuous cargo tops up only target deficit");
        Equal(4L, f.Count(vehicle.inventory.innerContainer, low), "lower priority uses only remaining vehicle mass");
        Equal(7L, f.Net.GetTotalCount(high), "continuous cargo withdraws only delivered amount");
        Equal(6, vehicle.PendingCargo[high], "continuous cargo advances manifest");
        Equal(0, f.Supplier.Contents.Count, "continuous cargo leaves supplier buffer empty");

        f = new Fixture(); vehicle = VehicleTarget(f, 100f); f.Store(def, 10); f.Select(0, def); f.Supplier.TrySwitchMode();
        f.Supplier.SetTargetLimit(0, 0); f.Supplier.SupplyTransporter();
        Equal(0, vehicle.CargoAdded, "zero target pauses vehicle supply");
        f.Supplier.SetTargetLimit(0, 7); vehicle.Reject = true; f.Supplier.SupplyTransporter();
        Equal(10L, f.Net.GetTotalCount(def), "rejected continuous cargo returns to network");
        vehicle.Reject = false; vehicle.AcceptLimit = 2; f.Supplier.SupplyTransporter();
        Equal(2L, f.Count(vehicle.inventory.innerContainer, def), "continuous native partial acceptance retained");
        Equal(8L, f.Net.GetTotalCount(def), "continuous partial remainder returns to network");

        // Native events can throw after acceptance; accepted cargo must never be refunded.
        f = new Fixture(); vehicle = VehicleTarget(f, 100f); f.Store(def, 10); f.Select(0, def);
        f.Supplier.SetTargetLimit(0, 4); f.Supplier.TrySwitchMode(); vehicle.ThrowAfterLoad = true;
        int errors = Log.Errors.Count;
        f.Supplier.SupplyTransporter();
        Equal(4L, f.Count(vehicle.inventory.innerContainer, def), "throwing event preserves accepted cargo");
        Equal(6L, f.Net.GetTotalCount(def), "throwing event cannot duplicate accepted cargo");
        var remaining = new List<Thing>(); f.Net.GetStacks(def, remaining);
        Equal(6, remaining.Sum(item => f.Net.AvailableToWithdraw(item)), "throwing event releases outgoing reservations");
        Log.Errors.RemoveRange(errors, Log.Errors.Count - errors);

        f = new Fixture(); vehicle = VehicleTarget(f, 100f); f.Cargo(def, 8); vehicle.ThrowAfterLoad = true;
        f.Supplier.SupplyTransporter();
        Equal(8L, f.Count(vehicle.inventory.innerContainer, def), "manual throwing event preserves accepted cargo");
        Equal(0, f.Supplier.Contents.Count, "manual throwing event cannot duplicate cargo");
    }
    private sealed class GenericBuilding : ThingWithComps, IThingHolder
    {
        internal ThingOwner Container;
        internal GenericBuilding() { def.category = ThingCategory.Building; Container = new ThingOwner(this); }
        public new IThingHolder ParentHolder => Map;
        public ThingOwner GetDirectlyHeldThings() => Container;
        public void GetChildHolders(List<IThingHolder> children) { }
    }
    private sealed class GenericComp : ThingComp, IThingHolder
    {
        internal readonly ThingOwner Container;
        internal GenericComp() { Container = new ThingOwner(this); }
        public IThingHolder ParentHolder => parent as IThingHolder;
        public ThingOwner GetDirectlyHeldThings() => Container;
        public void GetChildHolders(List<IThingHolder> children) { }
    }
    private sealed class LimitedContainer : ThingOwner
    {
        internal int Limit;
        internal ThingDef Allowed;
        internal bool RejectAdd;
        internal Action AfterAdd;
        internal LimitedContainer(IThingHolder owner, int limit) : base(owner) { Limit = limit; }
        public override int GetCountCanAccept(Thing item, bool merge = true) =>
            Allowed != null && item.def != Allowed ? 0 : Math.Min(item.stackCount, Math.Max(0, Limit - items.Sum(t => t.stackCount)));
        public override bool TryAdd(Thing item, bool merge = true)
        {
            if (RejectAdd) return false;
            bool added = base.TryAdd(item, merge);
            AfterAdd?.Invoke();
            return added;
        }
    }
    private static GenericBuilding GenericTarget(Fixture f)
    {
        var target = new GenericBuilding { Spawned = true, Map = f.Map, Position = new IntVec3(3, 0), Faction = Faction.OfPlayer };
        f.Map.Ground.Add(target);
        return target;
    }
    private static void ToggleGeneric(Fixture f) => f.Supplier.GetGizmos().OfType<Command_Toggle>().Single().toggleAction();

    private static Command_Action PresetCommand(Fixture f, string key) =>
        f.Supplier.GetGizmos().OfType<Command_Action>().Single(command => command.defaultLabel == key.Translate());

    private static void PresetClipboard()
    {
        var source = new Fixture();
        Command_Action paste = PresetCommand(source, "MS_Transport_PastePreset");
        Equal(true, paste.disabled, "supplier paste requires its own clipboard");
        Equal("MS_Transport_NeedCopiedPreset", paste.disabledReason, "empty clipboard explains how to enable paste");
        paste.action();
        Equal(false, source.Supplier.DirectSupply, "empty clipboard action leaves settings unchanged");
        var a = new ThingDef(); var b = new ThingDef(); var c = new ThingDef();
        source.Select(0, a); source.Select(1, b);
        source.Supplier.SetTargetLimit(0, 12); source.Supplier.SetTargetLimit(1, 0);
        source.Supplier.SetTargetLimit(2, int.MaxValue);
        source.Supplier.TrySwitchMode(); ToggleGeneric(source);
        Command_Action copy = PresetCommand(source, "MS_Transport_CopyPreset");
        Equal(false, copy.alsoClickIfOtherInGroupClicked, "grouped copy snapshots one supplier");
        Equal(StorageKeyBindingDefOf.MS_ShelfCopyPreset, copy.hotKey, "supplier shares copy shortcut");
        copy.action();
        source.Select(0, c); source.Supplier.SetTargetLimit(0, 1);
        source.Supplier.TrySwitchMode(); ToggleGeneric(source);
        source.Supplier.Spawned = false;

        var targets = new[] { new Fixture(), new Fixture() };
        foreach (Fixture target in targets)
        {
            target.Select(0, b); target.Select(1, a); target.Select(2, c);
            paste = PresetCommand(target, "MS_Transport_PastePreset");
            Equal(false, paste.disabled, "copied preset enables paste on other suppliers");
            Equal(true, paste.alsoClickIfOtherInGroupClicked, "paste participates in grouped activation");
            Equal(StorageKeyBindingDefOf.MS_ShelfPastePreset, paste.hotKey, "supplier shares paste shortcut");
            Equal(false, paste.groupKey == 0, "supplier paste stays separate from shelf commands");
            paste.action();
            Equal(a, target.Supplier.SelectedDef(0), "snapshot keeps first priority after source edits");
            Equal(b, target.Supplier.SelectedDef(1), "paste replaces swapped selections together");
            Equal<ThingDef>(null, target.Supplier.SelectedDef(2), "empty copied slot clears old selection");
            Equal(12, target.Supplier.TargetLimit(0), "snapshot keeps target limit after source edits");
            Equal(0, target.Supplier.TargetLimit(1), "paused item limit is copied");
            Equal(int.MaxValue, target.Supplier.TargetLimit(2), "large item limits are copied without clamping");
            Equal(true, target.Supplier.DirectSupply, "supply mode is copied");
            Equal(true, target.Supplier.GenericContainerSupply, "generic container switch is copied");
        }
        targets[0].Supplier.SetSelection(0, c); targets[0].Supplier.SetTargetLimit(0, 2);
        Equal(a, targets[1].Supplier.SelectedDef(0), "pasted selection lists are independent");
        Equal(12, targets[1].Supplier.TargetLimit(0), "pasted limit lists are independent");
        PresetCommand(targets[0], "MS_Transport_PastePreset").action();
        Equal(a, targets[0].Supplier.SelectedDef(0), "clipboard remains reusable after target edits");
        Equal(12, targets[0].Supplier.TargetLimit(0), "repeated paste restores copied limit");

        var empty = new Fixture();
        PresetCommand(empty, "MS_Transport_CopyPreset").action();
        PresetCommand(targets[0], "MS_Transport_PastePreset").action();
        Equal(true, Enumerable.Range(0, 3).All(i => targets[0].Supplier.SelectedDef(i) == null), "empty preset clears all selections");
        Equal(false, targets[0].Supplier.DirectSupply, "manual supply mode is copied");
        Equal(false, targets[0].Supplier.GenericContainerSupply, "disabled generic supply is copied");
    }

    private static void PresetModeGuard()
    {
        var source = new Fixture(); var a = new ThingDef(); var b = new ThingDef();
        source.Select(0, a); source.Supplier.SetTargetLimit(0, 8);
        source.Supplier.TrySwitchMode(); ToggleGeneric(source);
        PresetCommand(source, "MS_Transport_CopyPreset").action();
        var target = new Fixture(); target.Select(0, b); target.Supplier.SetTargetLimit(0, 3);
        Thing cargo = target.Cargo(b, 5);
        Command_Action paste = PresetCommand(target, "MS_Transport_PastePreset");
        Equal(true, paste.disabled, "loaded supplier blocks paste that changes mode");
        Equal("MS_Transport_ModeBlocked", paste.disabledReason, "blocked paste explains cargo requirement");
        paste.action();
        Equal(false, target.Supplier.DirectSupply, "blocked paste preserves supply mode");
        Equal(false, target.Supplier.GenericContainerSupply, "blocked paste preserves generic supply setting");
        Equal(b, target.Supplier.SelectedDef(0), "blocked paste preserves item selection");
        Equal(3, target.Supplier.TargetLimit(0), "blocked paste preserves target limit");
        Equal(cargo, target.Supplier.Contents.Single(), "blocked paste preserves cargo identity");
        Equal(5, cargo.stackCount, "blocked paste preserves cargo count");
        target.Supplier.Contents.Remove(cargo);
        Equal(false, PresetCommand(target, "MS_Transport_PastePreset").disabled, "emptying cargo enables mode-changing paste");
        paste.action();
        Equal(true, target.Supplier.DirectSupply, "previously blocked preset applies after cargo is removed");
        Equal(a, target.Supplier.SelectedDef(0), "previously blocked preset retains copied selection");

        source.Supplier.TrySwitchMode();
        PresetCommand(source, "MS_Transport_CopyPreset").action();
        var loaded = new Fixture(); loaded.Cargo(b, 7);
        paste = PresetCommand(loaded, "MS_Transport_PastePreset");
        Equal(false, paste.disabled, "loaded supplier accepts paste within the same mode");
        paste.action();
        Equal(a, loaded.Supplier.SelectedDef(0), "same-mode paste updates settings");
        Equal(7L, loaded.Count(loaded.Supplier.Contents, b), "same-mode paste leaves cargo untouched");

        // A command collected before cargo arrives must still guard its action.
        source.Supplier.TrySwitchMode(); PresetCommand(source, "MS_Transport_CopyPreset").action();
        var changed = new Fixture(); paste = PresetCommand(changed, "MS_Transport_PastePreset");
        Equal(false, paste.disabled, "empty supplier initially allows paste");
        changed.Cargo(b, 1); paste.action();
        Equal(false, changed.Supplier.DirectSupply, "late cargo prevents stale command from changing mode");
        Equal<ThingDef>(null, changed.Supplier.SelectedDef(0), "late cargo prevents partial preset application");
    }

    private static void PresetContainerTargets()
    {
        var source = new Fixture(); var itemDef = new ThingDef();
        source.Select(0, itemDef); source.Supplier.SetTargetLimit(0, 4);
        source.Supplier.TrySwitchMode(); ToggleGeneric(source);
        var sourceTarget = GenericTarget(source);
        var sourceComp = new GenericComp(); sourceComp.parent = sourceTarget; sourceTarget.AllComps.Add(sourceComp);
        source.Supplier.SelectGenericContainer(sourceTarget, 1);
        PresetCommand(source, "MS_Transport_CopyPreset").action();

        var target = new Fixture(); ToggleGeneric(target);
        var building = GenericTarget(target);
        var comp = new GenericComp(); comp.parent = building; building.AllComps.Add(comp);
        target.Supplier.SelectGenericContainer(building, 1);
        target.Store(itemDef, 10);
        PresetCommand(target, "MS_Transport_PastePreset").action();
        Equal(10L, target.Net.GetTotalCount(itemDef), "pasting does not withdraw stock immediately");
        Equal(0, comp.Container.Count, "pasting does not deliver items immediately");
        target.Supplier.SupplyTransporter();
        Equal(4L, target.Count(comp.Container, itemDef), "paste preserves this supplier's selected container");
        Equal(0, building.Container.Count, "paste does not choose another local container");
        Equal(0, sourceComp.Container.Count, "paste does not transfer to source supplier's target");
    }

    private static void GenericManualSupply()
    {
        var f = new Fixture(); var def = new ThingDef(); var other = new ThingDef();
        var target = GenericTarget(f);
        var container = new LimitedContainer(target, 5) { Allowed = def }; target.Container = container;
        f.Cargo(def, 8); f.Cargo(other, 3);
        var toggle = f.Supplier.GetGizmos().OfType<Command_Toggle>().Single();
        Equal(false, toggle.isActive(), "generic supply is disabled by default");
        Equal("MS_Transport_GenericSupplyDesc", toggle.defaultDesc, "generic toggle uses the compatibility description");
        f.Supplier.SupplyTransporter(); Equal(0, container.Count, "disabled generic supply leaves target untouched");
        toggle.toggleAction(); Equal(true, f.Supplier.GenericContainerSupply, "generic supply enabled");
        Equal(false, f.Supplier.GetGizmos().OfType<Command_Action>().Last().disabled, "single generic target enables immediate supply");
        f.Supplier.GetGizmos().OfType<Command_Action>().Last().action();
        Equal(5L, f.Count(container, def), "manual generic supply respects exposed acceptance limit");
        Equal(3L, f.Count(f.Supplier.Contents, def), "manual excess stays in supplier");
        Equal(3L, f.Count(f.Supplier.Contents, other), "rejected item type stays in supplier");
        container.Limit = 20; toggle.toggleAction(); f.Supplier.SupplyTransporter();
        Equal(5L, f.Count(container, def), "disabling stops generic delivery");
        toggle.toggleAction(); container.RejectAdd = true; f.Supplier.SupplyTransporter();
        Equal(3L, f.Count(f.Supplier.Contents, def), "late rejection returns manual cargo intact");
        Equal(0, f.Map.Ground.Count(t => t.def.category == ThingCategory.Item), "rejected cargo never spills");
    }
    private static void GenericDirectSupply()
    {
        var f = new Fixture(); var high = new ThingDef(); var low = new ThingDef(); var unselected = new ThingDef();
        var stack = f.Store(high, 8); f.Store(low, 10); f.Store(unselected, 4);
        f.Select(0, high); f.Select(1, low); f.Supplier.TrySwitchMode(); ToggleGeneric(f);
        var target = GenericTarget(f); var container = new LimitedContainer(target, 5); target.Container = container;
        object claim = new object(); f.Net.ReserveOutgoing(claim, stack, 6);
        f.Supplier.SupplyTransporter();
        Equal(2L, f.Count(container, high), "generic supply preserves reserved high-priority stock");
        Equal(3L, f.Count(container, low), "generic supply fills remaining capacity with lower priority");
        Equal(6L, f.Net.GetTotalCount(high), "reserved stock remains in network");
        Equal(7L, f.Net.GetTotalCount(low), "only accepted lower-priority amount withdrawn");
        Equal(4L, f.Net.GetTotalCount(unselected), "unselected items remain untouched");
        Equal(0, f.Supplier.Contents.Count, "generic continuous supply has no internal buffer");
        container.Limit = 20; container.RejectAdd = true; f.Supplier.SupplyTransporter();
        Equal(7L, f.Net.GetTotalCount(low), "late rejection restores network stock");
        container.RejectAdd = false;
        f.Manager.Unregister(f.Supplier.node); f.Supplier.SupplyTransporter();
        Equal(7L, f.Net.GetTotalCount(low), "disconnected generic supplier cannot withdraw");

        f = new Fixture(); f.Store(low, 2); f.Store(low, 3); f.Select(0, low); f.Supplier.TrySwitchMode(); ToggleGeneric(f);
        target = GenericTarget(f); container = new LimitedContainer(target, 20); target.Container = container;
        container.AfterAdd = () => target.Destroyed = true;
        f.Supplier.SupplyTransporter();
        Equal(2L, f.Count(container, low), "first transfer can invoke target callbacks");
        Equal(3L, f.Net.GetTotalCount(low), "target destruction stops further transfers");
    }
    private static void GenericContainerSelection()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Cargo(def, 4); ToggleGeneric(f);
        var target = GenericTarget(f); var comp = new GenericComp { parent = target }; target.AllComps.Add(comp);
        f.Supplier.SupplyTransporter();
        Equal(4L, f.Count(f.Supplier.Contents, def), "multiple containers wait for player selection");
        Equal(true, f.Supplier.GetGizmos().OfType<Command_Action>().Last().disabled, "ambiguous generic target disables immediate supply");
        var choose = f.Supplier.GetGizmos().OfType<Command_Action>().Single(g => g.defaultLabel == "MS_Transport_ChooseContainer");
        choose.action(); var menu = (FloatMenu)Find.WindowStack.Windows.Last();
        Equal(2, menu.options.Count, "picker lists building and component containers");
        menu.options[1].action(); f.Supplier.SupplyTransporter();
        Equal(0, target.Container.Count, "unselected building container untouched");
        Equal(4L, f.Count(comp.Container, def), "chosen component container receives cargo");
        f.Cargo(def, 2); choose.action(); menu = (FloatMenu)Find.WindowStack.Windows.Last();
        target.Spawned = false; var replacement = GenericTarget(f);
        replacement.AllComps.Add(new GenericComp { parent = replacement });
        menu.options[0].action(); f.Supplier.SupplyTransporter();
        Equal(2L, f.Count(f.Supplier.Contents, def), "stale selection cannot target a replacement building");
        Equal(false, f.Supplier.SelectGenericContainer(replacement, 2), "invalid container index rejected");
        Equal(true, f.Supplier.SelectGenericContainer(replacement, 0), "replacement target can be selected explicitly");
        f.Supplier.SupplyTransporter(); Equal(2L, f.Count(replacement.Container, def), "explicit new selection resumes supply");

        f = new Fixture(); target = GenericTarget(f); target.Container = null;
        comp = new GenericComp { parent = target }; target.AllComps.Add(comp); f.Cargo(def, 3); ToggleGeneric(f);
        f.Supplier.SupplyTransporter(); Equal(3L, f.Count(comp.Container, def), "single component-only container is discovered automatically");
    }
    private static void GenericTargetRules()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Cargo(def, 8); ToggleGeneric(f);
        var target = GenericTarget(f);
        target.Position = new IntVec3(2, 1); f.Supplier.SupplyTransporter(); Equal(0, target.Container.Count, "side container ignored");
        target.Position = new IntVec3(3, 0); target.Faction = new Faction(); f.Supplier.SupplyTransporter();
        Equal(0, target.Container.Count, "foreign generic container ignored");
        target.Faction = Faction.OfPlayer;
        var transporter = new CompTransporter { parent = target, MassCapacity = 3 }; target.AllComps.Add(transporter);
        f.Supplier.SupplyTransporter();
        Equal(3L, f.Count(transporter.innerContainer, def), "specialized transporter keeps its mass rules with generic mode on");
        Equal(0, target.Container.Count, "generic mode cannot bypass full transporter");
        f.Supplier.SupplyTransporter(); Equal(0, target.Container.Count, "full transporter does not fall back to another container");

        f = new Fixture(); f.Cargo(def, 4); ToggleGeneric(f); target = GenericTarget(f);
        var fuel = new CompRefuelable { parent = target, Fuel = 100, TargetFuelLevel = 100 }; fuel.Props.fuelFilter.SetAllow(def, true);
        target.AllComps.Add(fuel); f.Supplier.SupplyTransporter();
        Equal(0, target.Container.Count, "full fuel target does not bypass fuel rules through generic container");
        Equal(4L, f.Count(f.Supplier.Contents, def), "fuel target leaves unneeded material in supplier");
    }
    private static void FilterGroupingAndSelections()
    {
        var f = new Fixture(); var def = new ThingDef { stackLimit = 1000 };
        var first = f.Store(def, 5); var second = f.Store(def, 7);
        var firstCargo = f.Cargo(def, 3); var secondCargo = f.Cargo(def, 4);
        var matching = new HashSet<Thing> { first, firstCargo };
        Func<Thing, Thing, bool> sameRuleResult = (left, right) => matching.Contains(left) == matching.Contains(right);
        var previous = f.Rows();
        Equal(1, previous.Count, "native grouping initially combines both rule results");
        previous[0].SetChange(9);
        var split = StorageTransportExchange.Snapshot(f.Supplier, f.Net, sameRuleResult);
        Equal(2, split.Count, "rule matching results split otherwise identical items");
        var included = split.Single(row => matching.Contains(row.Representative));
        var excluded = split.Single(row => !matching.Contains(row.Representative));
        Equal(5L, included.NetworkCount, "matching network stacks stay in the matching row");
        Equal(3L, included.SupplyCount, "matching supplier stock joins the same row");
        Equal(7L, excluded.NetworkCount, "other network stock remains separate");
        Equal(4L, excluded.SupplyCount, "other supplier stock remains separate");
        StorageTransportExchange.RestoreSelections(previous, split, f.Net);
        Equal(5L, included.Change, "splitting keeps the first selected physical stack");
        Equal(4L, excluded.Change, "splitting keeps the remaining selected quantity");
        using (var preview = new StorageTransportPreview(f.Supplier, f.Net, split, 150))
        {
            var visible = split.Where(row => matching.Contains(row.Representative)).ToList();
            Equal(1, visible.Count, "filter hides the other row");
            Equal(16d, preview.ProjectedMass, "hidden selections still count toward projected mass");
            Equal(9L, split.Sum(row => row.Change), "filtering does not clear any selection");
        }
        Equal(true, f.Execute(split, 150, out _), "confirmation executes visible and hidden selected rows");
        Equal(3L, f.Net.GetTotalCount(def), "all selected outgoing stock leaves the network");
        Equal(16L, f.Count(f.Supplier.Contents, def), "all selected outgoing stock reaches supplier");

        f = new Fixture(); first = f.Store(def, 5); second = f.Store(def, 7);
        firstCargo = f.Cargo(def, 3); secondCargo = f.Cargo(def, 4);
        matching = new HashSet<Thing> { first, firstCargo };
        previous = f.Rows(); previous[0].SetChange(-6);
        split = StorageTransportExchange.Snapshot(f.Supplier, f.Net, sameRuleResult);
        StorageTransportExchange.RestoreSelections(previous, split, f.Net);
        included = split.Single(row => matching.Contains(row.Representative));
        excluded = split.Single(row => !matching.Contains(row.Representative));
        Equal(-3L, included.Change, "splitting preserves first return stack selection");
        Equal(-3L, excluded.Change, "splitting preserves remaining return quantity");
        var merged = f.Rows(); StorageTransportExchange.RestoreSelections(split, merged, f.Net);
        Equal(-6L, merged.Single().Change, "deleting a rule merges return selections without resetting them");

        previous = f.Rows(); previous[0].SetChange(9);
        var claim = new object(); f.Net.ReserveOutgoing(claim, first, 4);
        split = StorageTransportExchange.Snapshot(f.Supplier, f.Net, sameRuleResult);
        StorageTransportExchange.RestoreSelections(previous, split, f.Net);
        Equal(8L, split.Sum(row => row.Change), "restoration clamps to current unreserved availability");
        Equal(true, f.Net.HasOutgoingReservation(claim, first, 4), "restoration does not consume another task's reservation");
        f.Net.ReleaseOutgoing(claim);
    }
    private static void AutomaticLoadingState()
    {
        int originalTick = Find.TickManager.TicksGame;
        try
        {
            foreach (bool direct in new[] { false, true })
            foreach (bool shuttle in new[] { false, true })
            foreach (bool grouped in new[] { false, true })
            {
                var f = new Fixture();
                var itemDef = new ThingDef();
                var target = f.Target(150);
                target.parent.Faction = Faction.OfPlayer;
                if (shuttle) target.Shuttle = new CompShuttle { IsPlayerShuttle = true };
                else target.Launchable = new CompLaunchable_TransportPod();
                var peer = f.Target(150, 4);
                if (grouped) TransporterUtility.InitiateLoading(new[] { target, peer });
                int initialGroup = target.groupID;
                var orders = new List<TransferableOneWay> { new TransferableOneWay() };
                target.leftToLoad = orders;
                var cargo = new Thing { def = itemDef, stackCount = 2 };
                target.innerContainer.TryAdd(cargo);
                if (direct) { f.Supplier.TrySwitchMode(); f.Select(0, itemDef); }

                Find.TickManager.TicksGame = 0;
                f.Supplier.RunTransportTestTick();
                Equal(initialGroup, target.groupID, "no stock does not start or replace loading");
                if (direct) f.Store(itemDef, 10); else f.Cargo(itemDef, 10);
                Find.TickManager.TicksGame = 60000;
                f.Supplier.RunTransportTestTick();
                Equal(12L, f.Count(target.innerContainer, itemDef), "automatic tick delivers all ten items alongside existing cargo");
                Equal(true, target.groupID >= 0, "automatic delivery starts native loading for a player shuttle or solo pod");
                Equal(true, target.innerContainer.Contains(cargo), "existing cargo survives loading initialization");
                Equal(orders, target.leftToLoad, "loading initialization preserves native orders");
                Equal(1, orders.Count, "loading initialization preserves outstanding order entries");
                if (grouped) Equal(initialGroup, target.groupID, "automatic delivery preserves the existing group");
                else Equal(-1, peer.groupID, "nearby transporters are not implicitly grouped");
                int loadedGroup = target.groupID;
                if (direct) f.Store(itemDef, 10); else f.Cargo(itemDef, 10);
                Find.TickManager.TicksGame = 120000;
                f.Supplier.RunTransportTestTick();
                Equal(22L, f.Count(target.innerContainer, itemDef), "later automatic delivery still works");
                Equal(loadedGroup, target.groupID, "later delivery reuses the loading group");
                Equal(0L, direct ? f.Net.GetTotalCount(itemDef) : f.Count(f.Supplier.Contents, itemDef),
                    "all delivered items are removed from their source exactly once");
            }

            for (int kind = 0; kind < 4; kind++)
            {
                var f = new Fixture(); var itemDef = new ThingDef();
                var target = f.Target(kind == 3 ? 0 : 150);
                target.parent.Faction = kind == 2 ? new Faction() : Faction.OfPlayer;
                if (kind == 1) target.Shuttle = new CompShuttle(); // Quest shuttle.
                if (kind >= 2) target.Launchable = new CompLaunchable_TransportPod();
                f.Cargo(itemDef, 10);
                f.Supplier.RunTransportTestTick();
                Equal(-1, target.groupID, "ordinary/quest/foreign transporters and full pods are not initialized: " + kind);
                Equal(kind == 3 ? 0L : 10L, f.Count(target.innerContainer, itemDef), "existing cargo transfer rules remain intact: " + kind);
            }
        }
        finally { Find.TickManager.TicksGame = originalTick; }
    }

    private static void SharedLaunchCapacity()
    {
        var f = new Fixture();
        var heavy = new ThingDef { transportMass = 299 };
        var light = new ThingDef { transportMass = 1 };
        var first = f.Target(150); var second = f.Target(150, 4);
        foreach (var pod in new[] { first, second })
        {
            pod.parent.Faction = Faction.OfPlayer;
            pod.Launchable = new CompLaunchable_TransportPod();
            pod.parent.def = first.parent.def;
        }
        var order = new TransferableOneWay();
        first.leftToLoad = new List<TransferableOneWay> { order };
        second.innerContainer.TryAdd(new Thing { def = light, stackCount = 1 });
        f.Cargo(heavy, 1);
        f.Supplier.SupplyTransporter();
        Equal(1, f.Supplier.Contents.Count, "ungrouped pod cannot receive a single 299 kg item");
        Equal(true, StorageTransportGroups.TryCreate(new[] { first, second }, out _), "create loaded two-pod group");
        Equal(first.groupID, second.groupID, "pods share a native group ID");
        Equal(1L, f.Count(second.innerContainer, light), "grouping preserves already loaded cargo");
        Equal(order, first.leftToLoad[0], "grouping preserves outstanding native orders");
        Equal(300f, f.Supplier.TransferCapacity, "manual exchange uses total group capacity");
        f.Supplier.SupplyTransporter();
        Equal(299f, first.MassUsage, "single heavy item fits using shared capacity");
        Equal(1f, second.MassUsage, "other pod's existing cargo is counted");
        Equal(0, f.Supplier.Contents.Count, "heavy item leaves supplier intact");
        Equal(false, StorageTransportGroups.OverCapacity(first), "heavy pod can initiate a 300 kg group launch");
        Equal(false, StorageTransportGroups.OverCapacity(second), "light pod sees the same launch mass check");
        f.Cargo(light, 1); f.Supplier.SupplyTransporter();
        Equal(1L, f.Count(f.Supplier.Contents, light), "full group refuses excess manual cargo");
        int originalGroup = first.groupID;
        Equal(false, StorageTransportGroups.TryCreate(new[] { first, second }, out string error), "existing group cannot be overwritten");
        Equal("MS_Transport_GroupAlreadyAssigned", error, "already grouped selection explains rejection");
        Equal(originalGroup, first.groupID, "rejected grouping leaves original group intact");

        first.innerContainer.TryAdd(new Thing { def = light, stackCount = 1 });
        Equal(true, StorageTransportGroups.OverCapacity(first), "301 kg group is overweight from heavy pod");
        Equal(true, StorageTransportGroups.OverCapacity(second), "301 kg group is overweight from light pod too");
        second.parent.Spawned = false;
        Equal(150f, StorageTransportGroups.Capacity(first), "removed pod no longer contributes capacity");
        Equal(true, StorageTransportGroups.OverCapacity(first), "remaining pod cannot use removed capacity");

        var direct = new Fixture();
        var a = direct.Target(150); var b = direct.Target(150, 4);
        foreach (var pod in new[] { a, b })
        {
            pod.parent.Faction = Faction.OfPlayer;
            pod.Launchable = new CompLaunchable_TransportPod();
            pod.parent.def = a.parent.def;
        }
        direct.Store(heavy, 1); direct.Store(light, 5);
        direct.Select(0, heavy); direct.Select(1, light); direct.Supplier.TrySwitchMode();
        StorageTransportGroups.TryCreate(new[] { a, b }, out _);
        direct.Supplier.SupplyTransporter();
        Equal(300f, a.MassUsage, "continuous supply fills exactly to group capacity");
        Equal(4L, direct.Net.GetTotalCount(light), "continuous supply retains excess network stock");
        Equal(0, direct.Supplier.Contents.Count, "continuous group delivery uses no buffer");
        b.Shuttle = new CompShuttle();
        Equal(150f, StorageTransportGroups.Capacity(a), "mixed shuttle groups retain native single-pod rules");
        a.groupID = -1; b.groupID = -1;
        Equal(false, StorageTransportGroups.TryCreate(new[] { a, b }, out _), "quest shuttle cannot be grouped by this command");
        Equal(-1, a.groupID, "invalid grouping changes no group IDs");
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
    private static void TargetInventoryLimits()
    {
        var f = new Fixture(); var high = new ThingDef { stackLimit = 1000 }; var low = new ThingDef { stackLimit = 1000 };
        f.Store(high, 100, "a"); f.Store(high, 100, "b"); f.Store(low, 100);
        f.Select(0, high); f.Select(1, low); f.Supplier.TrySwitchMode(); ToggleGeneric(f);
        Equal(75, f.Supplier.TargetLimit(0), "new supply selection defaults to 75 items");
        f.Supplier.SetTargetLimit(1, 5);
        var target = GenericTarget(f);
        target.Container.TryAdd(new Thing { def = high, stackCount = 20, variant = "a" }, false);
        target.Container.TryAdd(new Thing { def = high, stackCount = 30, variant = "b" }, false);
        f.Supplier.SupplyTransporter();
        Equal(75L, f.Count(target.Container, high), "counts all variants and only supplies 25 missing items");
        Equal(175L, f.Net.GetTotalCount(high), "does not empty unlimited generic storage into target");
        Equal(5L, f.Count(target.Container, low), "moves on to lower priority after reaching high limit");
        f.Supplier.SupplyTransporter(true);
        Equal(175L, f.Net.GetTotalCount(high), "immediate supply also stops at inventory limit");
        target.Container.First(t => t.def == high).SplitOff(10).Destroy();
        f.Supplier.SupplyTransporter(true);
        Equal(75L, f.Count(target.Container, high), "consumption causes exact top-up");
        Equal(165L, f.Net.GetTotalCount(high), "top-up only consumes the deficit");
        f.Supplier.SetTargetLimit(0, 10); f.Supplier.SupplyTransporter();
        Equal(75L, f.Count(target.Container, high), "lowering limit does not reclaim excess target stock");
        f.Supplier.SetTargetLimit(0, 0);
        foreach (Thing item in target.Container.Where(t => t.def == high).ToList()) target.Container.Remove(item);
        f.Supplier.SupplyTransporter(true);
        Equal(0L, f.Count(target.Container, high), "zero pauses even an empty target and immediate supply");
        f.Supplier.SetTargetLimit(0, 75); f.Supplier.SupplyTransporter();
        var second = new Building_TransportSupplier { Map = f.Map, Position = f.Supplier.Position,
            Rotation = f.Supplier.Rotation, Faction = Faction.OfPlayer, Spawned = true };
        second.node = new CompStorageNode { parent = second }; f.Manager.Register(second.node); f.Manager.EnsureCurrent();
        second.SetSelection(0, high); second.TrySwitchMode();
        second.GetGizmos().OfType<Command_Toggle>().Single().toggleAction();
        second.SupplyTransporter();
        Equal(75L, f.Count(target.Container, high), "second supplier sees first supplier's existing target stock");
        f.Supplier.SetTargetLimit(0, int.MaxValue);
        foreach (Thing item in target.Container.Where(t => t.def == high).ToList()) target.Container.Remove(item);
        target.Container.TryAdd(new Thing { def = high, stackCount = 2000000000 }, false);
        target.Container.TryAdd(new Thing { def = high, stackCount = 2000000000 }, false);
        long remaining = f.Net.GetTotalCount(high);
        f.Supplier.SupplyTransporter();
        Equal(remaining, f.Net.GetTotalCount(high), "target counts exceeding int range do not wrap and request more");
    }

    private static void TransportInventoryLimits()
    {
        var f = new Fixture(); var high = new ThingDef(); var low = new ThingDef();
        f.Store(high, 40); f.Store(low, 40); f.Select(0, high); f.Select(1, low);
        f.Supplier.SetTargetLimit(0, 8); f.Supplier.SetTargetLimit(1, 10); f.Supplier.TrySwitchMode();
        var pod = f.Target(12); pod.innerContainer.TryAdd(new Thing { def = high, stackCount = 3 });
        f.Supplier.SupplyTransporter();
        Equal(8L, f.Count(pod.innerContainer, high), "transporter counts pre-existing cargo toward quantity cap");
        Equal(4L, f.Count(pod.innerContainer, low), "transporter still obeys mass capacity");
        pod.MassCapacity = 100; f.Supplier.SetTargetLimit(0, 0); f.Supplier.SetTargetLimit(1, 9);
        f.Supplier.SupplyTransporter(true);
        Equal(8L, f.Count(pod.innerContainer, high), "paused transporter item retains its existing cargo");
        Equal(9L, f.Count(pod.innerContainer, low), "immediate transporter supply observes item limit");
        var fuelFixture = new Fixture(); fuelFixture.Store(high, 40); fuelFixture.Store(low, 40);
        fuelFixture.Select(0, high); fuelFixture.Select(1, low);
        fuelFixture.Supplier.SetTargetLimit(0, 0); fuelFixture.Supplier.SetTargetLimit(1, 1);
        fuelFixture.Supplier.TrySwitchMode(); var fuel = fuelFixture.FuelTarget(high, low); fuel.TargetFuelLevel = 10;
        fuelFixture.Supplier.SupplyTransporter(true);
        Equal(40L, fuelFixture.Net.GetTotalCount(high), "zero disables selected fuel");
        Equal(30L, fuelFixture.Net.GetTotalCount(low), "positive fuel limits retain native fuel target semantics");
        Equal(10f, fuel.Fuel, "fuel refills to its own target rather than item count limit");
    }

    private static void TargetLimitPersistence()
    {
        var supplier = new Building_TransportSupplier();
        supplier.SetTargetLimit(0, 0); supplier.SetTargetLimit(1, 1234); supplier.SetTargetLimit(2, int.MaxValue);
        List<int> saved = null;
        try
        {
            Scribe_Collections.Boundary = (key, value, mode) =>
            {
                if (key == "transportTargetLimits")
                { Equal(LookMode.Value, mode, "limits saved by value"); saved = new List<int>((List<int>)value); }
                return value;
            };
            supplier.ExposeData();
            Equal(3, saved.Count, "all three limit slots sent to serialization");
            Scribe_Collections.Boundary = (key, value, mode) => key == "transportTargetLimits" ? new List<int>(saved) : value;
            var restored = new Building_TransportSupplier();
            Scribe.mode = LoadSaveMode.PostLoadInit; restored.ExposeData();
            Equal(0, restored.TargetLimit(0), "paused slot restored");
            Equal(1234, restored.TargetLimit(1), "custom limit restored");
            Equal(int.MaxValue, restored.TargetLimit(2), "large limit restored exactly");
            restored.SetTargetLimit(1, -1); Equal(0, restored.TargetLimit(1), "negative API input clamps to pause");
        }
        finally { Scribe_Collections.Boundary = null; Scribe.mode = LoadSaveMode.Inactive; }
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
        Equal("MS_Transport_ReturnFilterRejected", error, "return rejection identifies the storage filter");
        Equal(20L, f.Net.GetTotalCount(a), "withdrawal restored after rejected return"); Equal(10L, f.Count(f.Supplier.Contents, b), "return retained in supplier");
        Equal(0L, f.Count(f.Supplier.Contents, a), "no partial incoming delivery"); Equal(0, f.Map.Ground.Count, "rejected return not dropped");
        Equal(20L, f.Rows().Single(row => row.Representative.def == a).Available, "failed transaction releases claims");
    }
    private static void FullNetworkSwap()
    {
        var f = new Fixture(); f.Unit.SlotCapacity = 1; var a = new ThingDef(); var b = new ThingDef();
        f.Store(a, 10); f.Cargo(b, 20); var rows = f.Rows(); rows.Single(row => row.Representative.def == b).SetChange(-20);
        Equal(false, f.Execute(rows, 150, out string error), "full network rejects return"); Equal(20L, f.Count(f.Supplier.Contents, b), "full-network return preserved");
        Equal("MS_Transport_ReturnCapacityInsufficient", error, "full network reports insufficient capacity instead of a filter rejection");
        rows = f.Rows(); rows.Single(row => row.Representative.def == a).SetChange(10); rows.Single(row => row.Representative.def == b).SetChange(-20);
        Equal(true, f.Execute(rows, 150, out _), "withdrawal frees slot for same exchange's return");
        Equal(20L, f.Net.GetTotalCount(b), "returned stock occupies freed slot"); Equal(10L, f.Count(f.Supplier.Contents, a), "outgoing stock reaches supplier");
    }
    private static void AllReturnsReservedTogether()
    {
        var f = new Fixture(); f.Unit.SlotCapacity = 1; var a = new ThingDef(); var b = new ThingDef();
        f.Cargo(a, 5); f.Cargo(b, 5); var rows = f.Rows(); foreach (var row in rows) row.SetChange(-5);
        Equal(false, f.Execute(rows, 150, out string error), "two returns cannot compete for one free slot");
        Equal("MS_Transport_ReturnCapacityInsufficient", error, "combined returns report capacity reserved by the other returning item");
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
