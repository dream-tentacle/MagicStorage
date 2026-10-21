// Actual Harmony adapters and selection preservation run against controlled native
// UI/cache shapes. Drawing, game prices and route planning are engine boundaries.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using Verse;

internal static class NativeItemFilterTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }
    private static Tradeable Trade(Thing item, int selected, bool colony = true)
    {
        var row = new Tradeable { CountToTransfer = selected };
        (colony ? row.thingsColony : row.thingsTrader).Add(item); return row;
    }
    private static TransferableOneWay Cargo(Thing item, int selected)
    { var row = new TransferableOneWay { CountToTransfer = selected }; row.things.Add(item); return row; }

    public static int Main()
    {
        try { Run(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Run()
    {
        RuntimeHelpers.RunClassConstructor(typeof(StorageNativeItemFilters).TypeHandle);
        var allowed = new Thing { stackCount = 9, Matches = true };
        var hidden = new Thing { stackCount = 8, Matches = false };
        var currency = new Thing { stackCount = 100, Matches = false, Currency = true };
        TradeSession.deal = new TradeDeal();
        var selling = Trade(hidden, -4); var buying = Trade(allowed, 3, false);
        var money = Trade(currency, 0); money.Currency = true;
        TradeSession.deal.AllTradeables.AddRange(new[] { selling, buying, money });

        var nativeTrade = new Dialog_Trade(); var storageTrade = new Dialog_StorageTrade();
        storageTrade.CacheForTest();
        Equal(3, storageTrade.Shown.Count, "disabled switch keeps every storage trade row");
        MagicStorageMod.Settings.TradeItemFilters = true;
        nativeTrade.CacheForTest(); storageTrade.CacheForTest();
        Equal(3, nativeTrade.Shown.Count, "enabled option does not affect an ordinary trade window");
        Equal(2, storageTrade.Shown.Count, "storage trade display hides a nonmatching row");
        Equal(true, storageTrade.Shown.Contains(money), "currency row always remains available");
        Equal(3, TradeSession.deal.AllTradeables.Count, "filter never mutates the complete deal");
        Equal(-4, selling.CountToTransfer, "hidden sale stays selected");
        StorageNativeItemFilters.RebuildTrade(storageTrade);
        Equal(-4, TradeSession.deal.AllTradeables.Single(row => row.thingsColony.Contains(hidden)).CountToTransfer,
            "rule rebuild preserves hidden sales");
        Equal(3, TradeSession.deal.AllTradeables.Single(row => row.thingsTrader.Contains(allowed)).CountToTransfer,
            "rule rebuild preserves purchases");
        Equal(1, storageTrade.Changed, "trade rebuild notifies native statistics");
        MagicStorageMod.Settings.TradeItemFilters = false; storageTrade.CacheForTest();
        Equal(3, storageTrade.Shown.Count, "disabling restores the full display cache");

        var nativeCaravan = new Dialog_FormCaravan(); var storageCaravan = new Dialog_StorageCaravan();
        var pawn = new Pawn { Matches = false };
        var cargo = new[] { Cargo(allowed, 2), Cargo(hidden, 5), Cargo(pawn, 1) };
        nativeCaravan.transferables.AddRange(cargo); storageCaravan.transferables.AddRange(cargo);
        nativeCaravan.RebuildForTest(); storageCaravan.RebuildForTest();
        foreach (var row in storageCaravan.transferables)
            row.CountToTransfer = cargo.Single(original => original.AnyThing == row.AnyThing).CountToTransfer;
        Equal(3, storageCaravan.Items.Shown.Count, "caravan disabled option leaves the display unfiltered");
        MagicStorageMod.Settings.CaravanItemFilters = true;
        StorageNativeItemFilters.InvalidateCaravan(storageCaravan);
        Equal(3, nativeCaravan.Items.Shown.Count, "ordinary caravan widgets are not registered for filtering");
        Equal(2, storageCaravan.Items.Shown.Count, "registered storage caravan hides nonmatching cargo");
        Equal(true, storageCaravan.Items.Shown.Any(row => row.AnyThing is Pawn), "pawn rows are protected");
        Equal(3, storageCaravan.transferables.Count, "complete cargo and pawn list remains authoritative");
        Equal(5, storageCaravan.transferables.Single(row => row.AnyThing == hidden).CountToTransfer,
            "hidden caravan cargo stays selected");
        Equal(2, storageCaravan.Supplies.Shown.Count, "travel supplies widget shares filtering");
        StorageNativeItemFilters.RebuildCaravan(storageCaravan);
        Equal(5, storageCaravan.transferables.Single(row => row.AnyThing == hidden).CountToTransfer,
            "rebuild preserves hidden cargo counts");
        Equal(1, storageCaravan.transferables.Single(row => row.AnyThing is Pawn).CountToTransfer,
            "rebuild preserves pawn selection");
        Equal(2, storageCaravan.Items.Shown.Count, "new widgets are rebound after a rebuild");
        Equal(true, MagicStorageMod.Settings.CaravanItemFilters && !MagicStorageMod.Settings.TradeItemFilters,
            "the switches act independently");
        MagicStorageMod.Settings.CaravanItemFilters = false;
        StorageNativeItemFilters.InvalidateCaravan(storageCaravan);
        Equal(3, storageCaravan.Items.Shown.Count, "disabling restores all caravan rows");

        var merged = new TransferableOneWay(); merged.things.AddRange(new[] { allowed, hidden }); merged.CountToTransfer = 12;
        var selected = StorageNativeItemFilters.Capture(new[] { merged });
        var split = new[] { Cargo(hidden, 0), Cargo(allowed, 0) };
        StorageNativeItemFilters.Restore(split, selected);
        Equal(9, split[1].CountToTransfer, "physical stack selection survives row reordering");
        Equal(3, split[0].CountToTransfer, "row splitting preserves the remaining selected count");
        hidden.stackCount = 1; StorageNativeItemFilters.Restore(split, selected);
        Equal(1, split[0].CountToTransfer, "selection clamps to changed available quantities");
        Console.WriteLine("PASS: " + checks + " native item filter adapter assertions (production Harmony patches; native UI/cache shapes stubbed).");
    }
}

namespace UnityEngine { public struct Vector2 { public static Vector2 zero => default(Vector2); } }
namespace Verse
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class StaticConstructorOnStartupAttribute : Attribute { }
    public class Thing { public int stackCount = 1; public bool Matches = true, Currency; }
    public class Pawn : Thing { }
    public class QuickSearchWidget { public bool noResultsMatched; public void Reset() { } }
}
namespace RimWorld
{
    public abstract class Transferable
    {
        public int CountToTransfer;
        public abstract int GetMinimumToTransfer();
        public abstract int GetMaximumToTransfer();
        public void AdjustTo(int value) { CountToTransfer = value; }
    }
    public class TransferableOneWay : Transferable
    {
        public readonly List<Thing> things = new List<Thing>();
        public Thing AnyThing => things.FirstOrDefault();
        public override int GetMinimumToTransfer() => 0;
        public override int GetMaximumToTransfer() => things.Sum(item => item.stackCount);
    }
    public class Tradeable : Transferable
    {
        public readonly List<Thing> thingsColony = new List<Thing>(), thingsTrader = new List<Thing>();
        public bool Currency;
        public bool IsCurrency => Currency;
        public Thing AnyThing => thingsColony.FirstOrDefault() ?? thingsTrader.FirstOrDefault();
        public override int GetMinimumToTransfer() => -thingsColony.Sum(item => item.stackCount);
        public override int GetMaximumToTransfer() => thingsTrader.Sum(item => item.stackCount);
    }
    public static class TradeSession { public static TradeDeal deal; }
    public class TradeDeal
    {
        public readonly List<Tradeable> AllTradeables = new List<Tradeable>();
        public void Reset()
        {
            var next = new List<Tradeable>();
            foreach (var row in AllTradeables)
            {
                var clone = new Tradeable { Currency = row.Currency };
                clone.thingsColony.AddRange(row.thingsColony); clone.thingsTrader.AddRange(row.thingsTrader); next.Add(clone);
            }
            AllTradeables.Clear(); AllTradeables.AddRange(next);
        }
    }
    public class Dialog_Trade
    {
        private List<Tradeable> cachedTradeables = new List<Tradeable>();
        private UnityEngine.Vector2 scrollPosition;
        public QuickSearchWidget CommonSearchWidget { get; } = new QuickSearchWidget();
        public int Changed;
        public IReadOnlyList<Tradeable> Shown => cachedTradeables;
        public void CacheForTest() { CacheTradeables(); }
        [MethodImpl(MethodImplOptions.NoInlining)] private void CacheTradeables() { cachedTradeables = TradeSession.deal.AllTradeables.ToList(); }
        [MethodImpl(MethodImplOptions.NoInlining)] private void CountToTransferChanged() { Changed++; }
    }
    public class Dialog_FormCaravan
    {
        public List<TransferableOneWay> transferables = new List<TransferableOneWay>();
        private TransferableOneWayWidget itemsTransfer, travelSuppliesTransfer;
        public TransferableOneWayWidget Items => itemsTransfer;
        public TransferableOneWayWidget Supplies => travelSuppliesTransfer;
        public void RebuildForTest() { CalculateAndRecacheTransferables(); }
        [MethodImpl(MethodImplOptions.NoInlining)] private void CalculateAndRecacheTransferables()
        {
            transferables = transferables.Select(row => { var clone = new TransferableOneWay(); clone.things.AddRange(row.things); return clone; }).ToList();
            itemsTransfer = new TransferableOneWayWidget(transferables);
            travelSuppliesTransfer = new TransferableOneWayWidget(transferables);
        }
    }
    public class TransferableOneWayWidget
    {
        private struct Section { public List<TransferableOneWay> transferables, cachedTransferables; }
        private readonly List<Section> sections = new List<Section>();
        private bool transferablesCached;
        private UnityEngine.Vector2 scrollPosition;
        private QuickSearchWidget quickSearchWidget = new QuickSearchWidget();
        public TransferableOneWayWidget(List<TransferableOneWay> rows)
        { sections.Add(new Section { transferables = rows, cachedTransferables = new List<TransferableOneWay>() }); }
        public List<TransferableOneWay> Shown
        { get { if (!transferablesCached) CacheTransferables(); return sections[0].cachedTransferables; } }
        [MethodImpl(MethodImplOptions.NoInlining)] private void CacheTransferables()
        {
            foreach (var section in sections) { section.cachedTransferables.Clear(); section.cachedTransferables.AddRange(section.transferables); }
            transferablesCached = true;
        }
    }
}
namespace MagicStorage
{
    internal sealed class Settings { public bool CaravanItemFilters, TradeItemFilters; }
    internal static class MagicStorageMod { internal static Settings Settings = new Settings(); }
    internal sealed class StorageItemFilterPanel { internal bool Allows(Thing item) => item != null && item.Matches; }
    internal sealed class Dialog_StorageTrade : Dialog_Trade { internal StorageItemFilterPanel ItemFilters = new StorageItemFilterPanel(); }
    internal sealed class Dialog_StorageCaravan : Dialog_FormCaravan { internal StorageItemFilterPanel ItemFilters = new StorageItemFilterPanel(); }
}
