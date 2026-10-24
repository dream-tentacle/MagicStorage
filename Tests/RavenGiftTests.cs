using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MagicStorage;
using RavenRace.Features.FusangOrganization.UI;
using RavenRace.Features.Operator.Gifting;
using RavenRace.Features.Operator.UI;
using RimWorld;
using Verse;

internal static class RavenGiftTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string text)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Raven gift: " + text + "; expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly RadioSession Session;
        internal readonly RadioGiftPage Page;
        internal readonly ThingDef Def = new ThingDef { stackLimit = 1000, category = ThingCategory.Item };
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture()
        {
            RadioGiftActions.Refuse = false; RadioGiftActions.BeforeOriginal = null;
            GiftingManager.Favor = GiftingManager.Calls = 0; GiftingManager.BeforeGift = null;
            Thing.BeforePaymentTestSplit = null;
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Manager.EnsureCurrent();
            Session = new RadioSession(Map); Page = new RadioGiftPage(Session);
        }
        internal void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageCore core) { core.node = node; Map.listerBuildings.allBuildingsColonist.Add(core); }
            if (thing is Building_StorageUnit unit) unit.node = node;
            Manager.Register(node);
        }
        internal Thing Stock(int count, ThingDef def = null, Building_StorageUnit unit = null)
        {
            var item = new Thing { def = def ?? Def, stackCount = count };
            Equal(true, (unit ?? Unit).Inventory.Contents.TryAdd(item, false), "fixture stores real stack"); return item;
        }
        internal Thing Ground(int count)
        {
            var item = new Thing { def = Def, stackCount = count, Spawned = true, Map = Map, Position = new IntVec3(8, 0) };
            Map.Ground.Add(item); var beacon = new Building_OrbitalTradeBeacon();
            beacon.TradeableCells.Add(item.Position); Map.Beacons.Add(beacon); return item;
        }
        internal TransferableOneWay Row(ThingDef def = null) => Page.Rows.Single(group => group.AnyThing.def == (def ?? Def));
        internal List<Thing> Sources() => new RadioGiftTrader(Map).ColonyThingsWillingToBuy(null).ToList();
        internal bool Submit() => RadioGiftActions.Submit(Session, Page.Rows);
    }

    internal static int Run()
    {
        Uninstalled();
        var harmony = new Harmony("magicstorage.tests.ravengift");
        new RavenGiftCompatibility().Apply(harmony);
        QuantitiesAndOriginalCallbacks(); MixedAndNativeContainers(); NativeOnly(); InvalidSelection();
        RefusalAndExceptions(); StagingFailure(); PartialCompletion(); ScopeAndReentry();
        Console.WriteLine("PASS: " + checks + " Raven gift compatibility assertions.");
        return checks;
    }

    private static void Uninstalled()
    {
        var f = new Fixture(); Thing stock = f.Stock(100); var claim = new object(); f.Core.Network.ReserveOutgoing(claim, stock, 40);
        var module = new RavenGiftCompatibility();
        Equal(0, f.Sources().Count, "constructing an inactive module installs no inventory patches");
        var row = new TransferableOneWay(); row.things.Add(stock);
        Equal(100, row.MaxCount, "inactive module leaves native count getter untouched");
        Equal(false, Harmony.GetPatchInfo(AccessTools.PropertyGetter(typeof(TransferableOneWay), "MaxCount"))?.Owners.Contains("magicstorage.tests.ravengift") ?? false,
            "inactive module installs no global count patch");
        f.Core.Network.ReleaseOutgoing(claim);
    }

    private static void QuantitiesAndOriginalCallbacks()
    {
        var f = new Fixture(); Thing stock = f.Stock(100); var claim = new object(); f.Core.Network.ReserveOutgoing(claim, stock, 40);
        Equal(0, f.Map.Beacons.Count, "storage fixture has no beacons");
        Equal(stock, f.Sources().Single(), "gift provider exposes actual storage reference without beacon");
        var ordinary = new TransferableOneWay(); ordinary.things.Add(stock);
        Equal(100, ordinary.MaxCount, "ordinary transferable remains unmodified after patch installation");
        f.Page.RefreshForTest(); Equal(60, f.Row().MaxCount, "gift row subtracts only reserved portion");
        f.Row().AdjustTo(80); Equal(60, f.Row().CountToTransfer, "gift selection clamps to available count");
        f.Row().AdjustTo(55); int calls = RadioGiftActions.OriginalCalls;
        Equal(true, f.Submit(), "partially reserved stack can gift free quantity");
        Equal(calls + 1, RadioGiftActions.OriginalCalls, "original Submit performs the gift");
        Equal(55, GiftingManager.Favor, "original favor callback receives exact quantity");
        Equal("Native receipt: 55", f.Session.Receipt, "native receipt preserved");
        Equal(45L, f.Core.Network.GetTotalCount(f.Def), "real stored goods deducted exactly");
        Equal(true, f.Core.Network.HasOutgoingReservation(claim, stock, 40), "other task claim survives");
        Equal(5, f.Row().MaxCount, "live gift availability reflects completed deduction");
        Equal(stock, f.Row().things.Single(), "caller source references restored after submission");
        f.Core.Network.ReleaseOutgoing(claim); Equal(45, f.Row().MaxCount, "released external claim becomes available without altering stack");
        f.Page.RefreshForTest(); Equal(45, f.Row().MaxCount, "refresh produces scoped replacement groups");
    }

    private static void MixedAndNativeContainers()
    {
        var f = new Fixture(); Thing stock = f.Stock(30), ground = f.Ground(5);
        var held = new Thing { def = new ThingDef { category = ThingCategory.Item }, stackCount = 4 };
        var holder = new ThingOwner(null); holder.TryAdd(held, false);
        RadioGiftTrader.ExtraNative[f.Map] = new List<Thing> { held };
        f.Page.RefreshForTest(); f.Row().AdjustTo(12); f.Row(held.def).AdjustTo(3);
        Equal(35, f.Row().MaxCount, "ground and core quantities aggregate");
        Equal(true, f.Submit(), "mixed native ground, held-container and stored gifts succeed");
        Equal(true, ground.Destroyed, "native ground gift consumed first");
        Equal(23L, f.Core.Network.GetTotalCount(f.Def), "network only covers selected shortfall");
        Equal(1, held.stackCount, "native container consumption retained");
        Equal(15, GiftingManager.Favor, "native callback sees all three real sources");
        Equal(1, f.Session.Receipts, "one native receipt for mixed gift");

        f = new Fixture(); stock = f.Stock(30); ground = f.Ground(5); f.Page.RefreshForTest(); f.Row().AdjustTo(2);
        RadioGiftActions.BeforeOriginal = (session, groups) =>
        {
            Equal(false, groups[0].things.Contains(stock), "unneeded storage references removed before native gift");
            Equal(false, new RadioGiftTrader(f.Map).ColonyThingsWillingToBuy(null).Contains(stock), "unprepared storage excluded during native recheck");
        };
        try { Equal(true, f.Submit(), "selection fulfilled entirely by ground remains native"); }
        finally { RadioGiftActions.BeforeOriginal = null; }
        Equal(30L, f.Core.Network.GetTotalCount(f.Def), "unneeded core inventory untouched");
        Equal(3, ground.stackCount, "only selected ground quantity consumed");
    }

    private static void NativeOnly()
    {
        var f = new Fixture(); Thing ground = f.Ground(8); f.Page.RefreshForTest(); f.Row().AdjustTo(3);
        int calls = RadioGiftActions.OriginalCalls;
        Equal(true, f.Submit(), "without stored selection native gift still succeeds");
        Equal(calls + 1, RadioGiftActions.OriginalCalls, "native-only gift not replaced");
        Equal(5, ground.stackCount, "native-only deduction unchanged");
        var generic = new TransferableOneWay(); generic.things.Add(ground); Equal(5, generic.MaxCount, "unrelated one-way UI retains ordinary count");
        Equal(false, Harmony.GetPatchInfo(AccessTools.Method(typeof(TradeUtility), "AllLaunchableThingsForTrade"))?.Owners.Contains("magicstorage.tests.ravengift") ?? false,
            "gift module never patches shared launchable enumeration");
    }

    private static void InvalidSelection()
    {
        var f = new Fixture(); Thing stock = f.Stock(100); f.Page.RefreshForTest(); f.Row().AdjustTo(70);
        var claim = new object(); f.Core.Network.ReserveOutgoing(claim, stock, 40); int calls = RadioGiftActions.OriginalCalls;
        Equal(false, f.Submit(), "new reservations reject stale selected amount");
        Equal(calls, RadioGiftActions.OriginalCalls, "preparation failure never runs original favor logic");
        Equal(0, GiftingManager.Favor, "failed selection grants no favor");
        Equal(100, stock.stackCount, "failed selection consumes nothing");
        Equal(true, f.Core.Network.HasOutgoingReservation(claim, stock, 40), "failure leaves existing reservation intact");
        f.Page.RefreshForTest(); Equal(60, f.Row().CountToTransfer, "refresh clamps retained selection to new available quantity");
        f.Core.Network.ReleaseOutgoing(claim);
        f.Manager.Unregister(f.Core.node);
        Equal(false, f.Submit(), "core loss refuses previously displayed stock");
        Equal(100, stock.stackCount, "core loss cannot turn into direct raw stack consumption");
        Equal(true, !string.IsNullOrEmpty(f.Session.ReportText), "failure reports through Raven session");
    }

    private static void RefusalAndExceptions()
    {
        var f = new Fixture(); Thing stock = f.Stock(100); f.Page.RefreshForTest(); f.Row().AdjustTo(30);
        RadioGiftActions.BeforeOriginal = (session, groups) =>
        {
            Equal(70L, f.Core.Network.GetTotalCount(f.Def), "network quantity staged before native callback");
            Equal(30, groups[0].things.Single().stackCount, "only actual selected amount is staged");
            Equal(false, groups[0].things.Single().holdingOwner.Owner is Building_StorageUnit, "native split targets staging rather than storage owner");
            Equal(30, new RadioGiftTrader(f.Map).ColonyThingsWillingToBuy(null).Single().stackCount, "native validation sees prepared stack");
        };
        RadioGiftActions.Refuse = true;
        try { Equal(false, f.Submit(), "native refusal preserved"); }
        finally { RadioGiftActions.Refuse = false; RadioGiftActions.BeforeOriginal = null; }
        Equal(100L, f.Core.Network.GetTotalCount(f.Def), "native refusal restores all staged stock");
        Equal(0, GiftingManager.Favor, "native refusal grants no favor");
        Equal(stock, f.Row().things.Single(), "refusal restores row source reference");
        RadioGiftActions.BeforeOriginal = (session, groups) => { throw new InvalidOperationException("Controlled native gift failure"); };
        try { f.Submit(); throw new Exception("Expected native exception"); }
        catch (InvalidOperationException) { }
        finally { RadioGiftActions.BeforeOriginal = null; }
        Equal(100L, f.Core.Network.GetTotalCount(f.Def), "native exception restores unconsumed stock");
        Equal(true, f.Submit(), "failure does not poison next submission");
        Equal(70L, f.Core.Network.GetTotalCount(f.Def), "retry consumes only once");
    }

    private static void StagingFailure()
    {
        var f = new Fixture(); Thing first = f.Stock(10); var defB = new ThingDef(); Thing second = f.Stock(20, defB);
        f.Page.RefreshForTest(); f.Row().AdjustTo(8); f.Row(defB).AdjustTo(15);
        int calls = RadioGiftActions.OriginalCalls;
        Thing.BeforePaymentTestSplit = item => { if (item == second) throw new InvalidOperationException("Controlled second withdrawal failure"); };
        try { Equal(false, f.Submit(), "later staging failure aborts the whole preparation"); }
        finally { Thing.BeforePaymentTestSplit = null; }
        Equal(calls, RadioGiftActions.OriginalCalls, "failed staging never enters native gift");
        Equal(10L, f.Core.Network.GetTotalCount(f.Def), "already staged first stack restored");
        Equal(20L, f.Core.Network.GetTotalCount(defB), "second stack untouched");
        Equal(10, f.Core.Network.AvailableToWithdraw(first), "failed staging releases first claim");
        Equal(20, f.Core.Network.AvailableToWithdraw(second), "failed staging releases remaining claims");
        Equal(0, GiftingManager.Favor, "staging failure grants no favor");
    }

    private static void PartialCompletion()
    {
        var f = new Fixture(); f.Stock(10); var defB = new ThingDef(); f.Stock(20, defB);
        f.Page.RefreshForTest(); f.Row().AdjustTo(10); f.Row(defB).AdjustTo(20);
        GiftingManager.BeforeGift = (item, count) => { if (item.def == defB) throw new InvalidOperationException("Controlled second gift callback failure"); };
        try { f.Submit(); throw new Exception("Expected second gift failure"); }
        catch (InvalidOperationException) { }
        finally { GiftingManager.BeforeGift = null; }
        Equal(0L, f.Core.Network.GetTotalCount(f.Def), "completed gift never reconstructed by cleanup");
        Equal(20L, f.Core.Network.GetTotalCount(defB), "unconsumed second gift restored");
        Equal(10, GiftingManager.Favor, "original completed favor retained exactly once");
        Equal(0, f.Session.Receipts, "exception does not invent success receipt");
    }

    private static void ScopeAndReentry()
    {
        var f = new Fixture(); f.Stock(10); var coreB = new Building_StorageCore(); var unitB = new Building_StorageUnit();
        f.Register(coreB, 50); f.Register(unitB, 51); f.Manager.EnsureCurrent(); f.Stock(15, unit: unitB);
        f.Page.RefreshForTest(); Equal(25, f.Row().MaxCount, "eligible separate networks aggregate once");
        f.Row().AdjustTo(12); bool nested = true;
        RadioGiftActions.BeforeOriginal = (session, groups) => { nested = RadioGiftActions.Submit(session, groups); };
        try { Equal(true, f.Submit(), "outer gift survives refused reentry"); }
        finally { RadioGiftActions.BeforeOriginal = null; }
        Equal(false, nested, "nested gift refused before duplicate consumption");
        Equal(12, GiftingManager.Favor, "reentry cannot duplicate favor");
        Equal(13L, f.Core.Network.GetTotalCount(f.Def) + coreB.Network.GetTotalCount(f.Def), "multi-network real stock conserved");
        var other = new Fixture(); other.Stock(30); Equal(1, other.Sources().Count, "another map enumerates its own core only");
        f.Page.RefreshForTest(); f.Row().AdjustTo(1); f.Page.Rows.Add(f.Row());
        int calls = RadioGiftActions.OriginalCalls;
        Equal(false, f.Submit(), "duplicated gift groups rejected");
        Equal(calls, RadioGiftActions.OriginalCalls, "duplicate selection never reaches native consumption");
    }
}
