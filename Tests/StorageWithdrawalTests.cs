using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using Verse;

internal static class StorageWithdrawalTests
{
    private static int assertions;
    private static void Equal<T>(T expected, T actual, string label)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(label + ": " + expected + " != " + actual); }
    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal StorageNetwork Net => Core.Network;
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Core.Map = Unit.Map = Map; Core.Spawned = Unit.Spawned = true;
            Core.node = new CompStorageNode { parent = Core }; Unit.node = new CompStorageNode { parent = Unit };
            var component = Map.GetComponent<MapComponent_StorageNetworks>();
            component.Register(Core.node); component.Register(Unit.node); component.EnsureCurrent();
        }
        internal Thing Add(ThingDef def, int count, string variant = null)
        { var item = new Thing { def = def, stackCount = count, variant = variant }; Unit.Inventory.Contents.TryAdd(item, false); return item; }
        internal StorageRecoveryBatch Pending()
        {
            var children = new List<IThingHolder>(); Map.GetComponent<MapComponent_StorageNetworks>().GetChildHolders(children);
            return children.OfType<StorageRecoveryBatch>().Last();
        }
    }
    public static int Run()
    {
        Grouping(); MealTradeGrouping(); ReservationsAndPartial(); Revalidation(); PendingAndFlags(); LongCounts();
        TradeQuantityEditing(); MinifiedPresentation();
        return assertions;
    }
    private static void Grouping()
    {
        var f = new Fixture(); var def = new ThingDef();
        f.Add(def, 20); f.Add(def, 30); f.Add(def, 5, "other material");
        var bookDef = new ThingDef { stackLimit = 1, tradeNeverStack = true }; var book1 = f.Add(bookDef, 1); var book2 = f.Add(bookDef, 1);
        var rows = StorageWithdrawal.Snapshot(f.Net);
        Equal(4, rows.Count, "materials grouped, stateful objects separate");
        Equal(50L, rows.Single(r => r.Stacks.Count == 2).Total, "summed material stacks");
        Equal(false, rows.Single(r => r.Representative == book1).Stacks.Contains(book2), "book identities distinct");
        var stateful = new ThingWithComps { def = def, stackCount = 3 };
        stateful.AllComps.Add(new ThingComp()); f.Unit.Inventory.Contents.TryAdd(stateful, false);
        Equal(4, StorageWithdrawal.Snapshot(f.Net).Count, "components do not override native display grouping");
        var bookRow = rows.Single(r => r.Representative == book1); bookRow.SetRequested(1);
        var result = StorageWithdrawal.Execute(f.Core, f.Net, new[] { bookRow }, false);
        Equal(1L, result.Withdrawn, "book withdrawn"); Equal(book1, f.Map.Ground[0], "real book identity preserved");
        Equal(true, f.Unit.Inventory.Contents.Contains(book2), "other book remains");
    }

    private static void MealTradeGrouping()
    {
        var f = new Fixture(); var mealDef = new ThingDef { stackLimit = 10 };
        for (int i = 0; i < 4; i++)
        {
            var meal = new ThingWithComps { def = mealDef, stackCount = 10 };
            meal.AllComps.Add(new ThingComp());
            f.Unit.Inventory.Contents.TryAdd(meal, false);
        }
        RimWorld.TransferableUtility.Calls = 0;
        var rows = StorageWithdrawal.Snapshot(f.Net);
        Equal(1, rows.Count, "four full meal stacks display as one row");
        Equal(40L, rows[0].Total, "meal row totals forty");
        Equal(4, f.Unit.Inventory.UsedSlots, "display grouping leaves physical slots unchanged");
        Equal(true, RimWorld.TransferableUtility.Calls > 0, "grouping delegates to native predicate");
        Equal(RimWorld.TransferAsOneMode.Normal, RimWorld.TransferableUtility.LastMode, "same mode as active trade rows");
        rows[0].SetRequested(25);
        var result = StorageWithdrawal.Execute(f.Core, f.Net, rows, false);
        Equal(25L, result.Withdrawn, "withdrawal spans grouped physical stacks");
        Equal(15L, f.Net.GetTotalCount(mealDef), "remaining meals conserved");
        Equal(2, f.Unit.Inventory.UsedSlots, "remaining physical stacks occupy two slots");
        Equal(15L, StorageWithdrawal.Snapshot(f.Net)[0].Total, "remaining grouped row updated");

        var weapons = new Fixture(); var weaponDef = new ThingDef { stackLimit = 1, IsWeapon = true };
        weapons.Add(weaponDef, 1, "same quality"); weapons.Add(weaponDef, 1, "same quality");
        weapons.Add(weaponDef, 1, "different quality");
        rows = StorageWithdrawal.Snapshot(weapons.Net);
        Equal(2, rows.Count, "native-compatible equipment can share display row despite stack limit one");
        Equal(2L, rows.Single(row => row.Stacks.Count == 2).Total, "equipment count is combined only for matching native group");
    }
    private static void ReservationsAndPartial()
    {
        var f = new Fixture(); var def = new ThingDef(); var item = f.Add(def, 50);
        object eater = new object(); f.Net.ReserveOutgoing(eater, item, 17);
        var rows = StorageWithdrawal.Snapshot(f.Net);
        Equal(33L, rows[0].Available, "food claim excluded");
        rows[0].SetRequested(999); Equal(33L, rows[0].Requested, "request clamped");
        var result = StorageWithdrawal.Execute(f.Core, f.Net, rows, false);
        Equal(33L, result.Withdrawn, "unreserved portion withdrawn");
        Equal(17L, f.Net.GetTotalCount(def), "reserved remainder remains indexed");
        f.Net.ReleaseOutgoing(eater);
        var delivery = new object(); var incoming = new Thing { def = def, stackCount = 5 };
        f.Net.ReserveIncoming(delivery, incoming, 5);
        Equal(0L, StorageWithdrawal.Snapshot(f.Net)[0].Available, "incoming merge target protected");
        f.Net.ReleaseIncoming(delivery);
        rows = StorageWithdrawal.Snapshot(f.Net); rows[0].SetRequested(17);
        f.Net.ReserveOutgoing(eater, item, 10);
        result = StorageWithdrawal.Execute(f.Core, f.Net, rows, false);
        Equal(7L, result.Withdrawn, "new claim rechecked at commit");
        Equal(10L, result.Requested - result.Withdrawn, "unfulfilled request counted");
    }
    private static void Revalidation()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Add(def, 10);
        var old = f.Net; var rows = StorageWithdrawal.Snapshot(old); rows[0].SetRequested(10);
        f.Map.GetComponent<MapComponent_StorageNetworks>().MarkDirty();
        f.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
        Equal(0L, StorageWithdrawal.Execute(f.Core, old, rows, false).Withdrawn, "old topology refused");
        var second = new Building_StorageCore { Map = f.Map, Spawned = true };
        second.node = new CompStorageNode { parent = second };
        f.Map.GetComponent<MapComponent_StorageNetworks>().Register(second.node);
        f.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
        rows = StorageWithdrawal.Snapshot(f.Net);
        Equal(10L, rows[0].Total, "multiple cores still readable");
        Equal(0L, rows[0].Available, "multiple cores no withdrawals");
        rows[0].Requested = 10;
        Equal(0L, StorageWithdrawal.Execute(f.Core, f.Net, rows, false).Withdrawn, "invalid network rejects commit");
    }
    private static void PendingAndFlags()
    {
        var f = new Fixture(); var def = new ThingDef(); f.Add(def, 40);
        var rows = StorageWithdrawal.Snapshot(f.Net); rows[0].SetRequested(40);
        f.Map.DropBudget = 12;
        var result = StorageWithdrawal.Execute(f.Core, f.Net, rows, true);
        Equal(40L, result.Withdrawn, "confirmed withdrawal leaves units");
        Equal(28L, result.Pending, "partial drop remainder retained");
        Equal(true, f.Map.Ground[0].forbidden, "placed split forbidden");
        Equal(28L, f.Pending().PendingCount, "pending real objects owned by map holder");
        f.Map.DropBudget = 100; f.Pending().TryRelease(f.Map, 8);
        Equal(0L, f.Pending().PendingCount, "retry drains pending");
        Equal(40, f.Map.Ground.Sum(t => t.stackCount), "retry neither loses nor duplicates");

        var blocked = new Fixture(); var original = new Thing { def = def, stackCount = 1, forbidden = false };
        blocked.Map.Ground.Add(original); blocked.Add(def, 2);
        rows = StorageWithdrawal.Snapshot(blocked.Net); rows[0].SetRequested(2);
        result = StorageWithdrawal.Execute(blocked.Core, blocked.Net, rows, true);
        Equal(false, original.forbidden, "unrelated ground flag unchanged");
        Equal(2L, result.Pending, "incompatible destination rejected by validator");
        blocked.Map.Ground.Clear(); blocked.Pending().TryRelease(blocked.Map, 8);
        Equal(true, blocked.Map.Ground[0].forbidden, "retry retains forbid choice");
    }
    private static void TradeQuantityEditing()
    {
        var row = new StorageItemRow { Available = 40 };
        row.EditRequested("-25");
        Equal(25L, row.Requested, "trade selling sign maps to withdrawal");
        Equal("-25", row.Buffer, "negative quantity remains visible");
        row.EditRequested("-99"); Equal(40L, row.Requested, "input capped at availability");
        row.EditRequested("-"); Equal(0L, row.Requested, "unfinished sign never retains stale withdrawal");
        Equal("-", row.Buffer, "partial negative input supported");
        row.EditRequested("-10"); row.EditRequested(""); Equal(0L, row.Requested, "clearing input clears request");
        row.EditRequested("25"); Equal(0L, row.Requested, "buy direction unavailable");
        row.EditRequested("-10"); row.EditRequested("garbage"); Equal(10L, row.Requested, "invalid text ignored");
        row.Available = 4294967294L; row.EditRequested("-4294967294");
        Equal(4294967294L, row.Requested, "quantities retain precision above int limit");
        row.EditRequested(long.MinValue.ToString()); Equal(row.Available, row.Requested, "minimum long cannot overflow negation");
    }

    private static void MinifiedPresentation()
    {
        var f = new Fixture(); var inside = new Thing { def = new ThingDef { category = ThingCategory.Building }, variant = "inner building" };
        var outer = new MinifiedThing { def = new ThingDef { stackLimit = 1 }, InnerThing = inside };
        f.Unit.Inventory.Contents.TryAdd(outer, false);
        var rows = StorageWithdrawal.Snapshot(f.Net);
        Equal(inside, rows[0].DisplayThing, "presentation uses inner building");
        Equal(outer, rows[0].Representative, "ownership still tracks actual package");
        rows[0].SetRequested(1);
        var result = StorageWithdrawal.Execute(f.Core, f.Net, rows, false);
        Equal(1L, result.Withdrawn, "package withdrawn once");
        Equal(outer, f.Map.Ground[0], "ground receives package, not detached inner building");
        Equal(inside, ((MinifiedThing)f.Map.Ground[0]).InnerThing, "contained building retained");
    }

    private static void LongCounts()
    {
        var f = new Fixture(); var def = new ThingDef { stackLimit = int.MaxValue };
        // One slot per holder avoids vanilla's unrelated int multiplication in capacity checks.
        f.Unit.SlotCapacity = 1;
        f.Add(def, int.MaxValue);
        var second = new Building_StorageUnit { Map = f.Map, Spawned = true, SlotCapacity = 1 };
        second.node = new CompStorageNode { parent = second };
        f.Map.GetComponent<MapComponent_StorageNetworks>().Register(second.node);
        f.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
        second.Inventory.Contents.TryAdd(new Thing { def = def, stackCount = int.MaxValue }, false);
        var row = StorageWithdrawal.Snapshot(f.Net)[0];
        Equal(4294967294L, row.Total, "totals exceed int range");
        row.SetRequested(long.MaxValue); Equal(row.Total, row.Requested, "long quantity clamp");
        row.SetRequested(-10); Equal(0L, row.Requested, "negative quantity clamp");
    }
}
