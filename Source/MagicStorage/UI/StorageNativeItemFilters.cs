using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    // Patch native display caches only. Complete transfer lists remain authoritative
    // for mass, automatic supplies, pricing and execution, including hidden selections.
    [StaticConstructorOnStartup]
    internal static class StorageNativeItemFilters
    {
        private static readonly ConditionalWeakTable<TransferableOneWayWidget, Dialog_StorageCaravan> owners =
            new ConditionalWeakTable<TransferableOneWayWidget, Dialog_StorageCaravan>();
        private static readonly FieldInfo itemsWidget = AccessTools.Field(typeof(Dialog_FormCaravan), "itemsTransfer");
        private static readonly FieldInfo suppliesWidget = AccessTools.Field(typeof(Dialog_FormCaravan), "travelSuppliesTransfer");
        private static readonly FieldInfo widgetSections = AccessTools.Field(typeof(TransferableOneWayWidget), "sections");
        private static readonly FieldInfo sectionCache = AccessTools.Field(AccessTools.Inner(typeof(TransferableOneWayWidget), "Section"), "cachedTransferables");
        private static readonly FieldInfo widgetCached = AccessTools.Field(typeof(TransferableOneWayWidget), "transferablesCached");
        private static readonly FieldInfo widgetScroll = AccessTools.Field(typeof(TransferableOneWayWidget), "scrollPosition");
        private static readonly FieldInfo widgetSearch = AccessTools.Field(typeof(TransferableOneWayWidget), "quickSearchWidget");
        private static readonly MethodInfo tradeCache = AccessTools.Method(typeof(Dialog_Trade), "CacheTradeables");
        private static readonly FieldInfo tradeScroll = AccessTools.Field(typeof(Dialog_Trade), "scrollPosition");
        private static readonly MethodInfo tradeChanged = AccessTools.Method(typeof(Dialog_Trade), "CountToTransferChanged");
        private static readonly MethodInfo caravanRebuild = AccessTools.Method(typeof(Dialog_FormCaravan), "CalculateAndRecacheTransferables");

        static StorageNativeItemFilters()
        {
            var harmony = new Harmony("mjcg.magicstorage.itemfilters");
            harmony.Patch(tradeCache, postfix: new HarmonyMethod(typeof(StorageNativeItemFilters), nameof(FilterTradeCache)));
            harmony.Patch(caravanRebuild, postfix: new HarmonyMethod(typeof(StorageNativeItemFilters), nameof(BindCaravanWidgets)));
            harmony.Patch(AccessTools.Method(typeof(TransferableOneWayWidget), "CacheTransferables"),
                postfix: new HarmonyMethod(typeof(StorageNativeItemFilters), nameof(FilterWidgetCache)));
        }

        private static void FilterTradeCache(Dialog_Trade __instance, List<Tradeable> ___cachedTradeables)
        {
            if (!MagicStorageMod.Settings.TradeItemFilters || !(__instance is Dialog_StorageTrade dialog)) return;
            ___cachedTradeables.RemoveAll(row => !row.IsCurrency && !dialog.ItemFilters.Allows(row.AnyThing));
            __instance.CommonSearchWidget.noResultsMatched = ___cachedTradeables.Count == 0;
        }

        private static IEnumerable<TransferableOneWayWidget> WidgetsFor(Dialog_StorageCaravan dialog)
        {
            if (itemsWidget.GetValue(dialog) is TransferableOneWayWidget items) yield return items;
            if (suppliesWidget.GetValue(dialog) is TransferableOneWayWidget supplies) yield return supplies;
        }

        private static void BindCaravanWidgets(Dialog_FormCaravan __instance)
        {
            if (!(__instance is Dialog_StorageCaravan dialog)) return;
            foreach (var widget in WidgetsFor(dialog))
            {
                owners.Remove(widget);
                owners.Add(widget, dialog);
            }
        }

        private static void FilterWidgetCache(TransferableOneWayWidget __instance)
        {
            if (!MagicStorageMod.Settings.CaravanItemFilters || !owners.TryGetValue(__instance, out var dialog)) return;
            foreach (object section in (IEnumerable)widgetSections.GetValue(__instance))
            {
                var rows = (List<TransferableOneWay>)sectionCache.GetValue(section);
                rows.RemoveAll(row => !(row.AnyThing is Pawn) && !dialog.ItemFilters.Allows(row.AnyThing));
            }
        }

        internal static void InvalidateTrade(Dialog_StorageTrade dialog)
        {
            tradeCache.Invoke(dialog, null);
            tradeScroll.SetValue(dialog, Vector2.zero);
        }

        internal static void InvalidateCaravan(Dialog_StorageCaravan dialog, bool resetSearch = false)
        {
            foreach (var widget in WidgetsFor(dialog))
            {
                widgetCached.SetValue(widget, false);
                widgetScroll.SetValue(widget, Vector2.zero);
                if (resetSearch) ((QuickSearchWidget)widgetSearch.GetValue(widget)).Reset();
            }
        }

        internal static void RebuildTrade(Dialog_StorageTrade dialog)
        {
            var selected = Capture(TradeSession.deal.AllTradeables);
            TradeSession.deal.Reset();
            Restore(TradeSession.deal.AllTradeables, selected);
            tradeChanged.Invoke(dialog, null);
            InvalidateTrade(dialog);
        }

        internal static void RebuildCaravan(Dialog_StorageCaravan dialog)
        {
            var selected = Capture(dialog.transferables);
            // The native rebuild recreates both item widgets and dirties all estimates.
            caravanRebuild.Invoke(dialog, null);
            Restore(dialog.transferables, selected);
            InvalidateCaravan(dialog);
        }

        internal static Dictionary<Thing, int> Capture(IEnumerable<Transferable> rows)
        {
            var result = new Dictionary<Thing, int>();
            foreach (var row in rows)
            {
                int count = row.CountToTransfer;
                IEnumerable<Thing> things;
                if (row is Tradeable trade)
                {
                    if (trade.IsCurrency) continue;
                    things = count < 0 ? trade.thingsColony : trade.thingsTrader;
                }
                else things = ((TransferableOneWay)row).things;
                int remaining = Math.Abs(count);
                foreach (var item in things)
                {
                    if (remaining == 0) break;
                    int take = Math.Min(remaining, item.stackCount);
                    result[item] = Math.Sign(count) * take;
                    remaining -= take;
                }
            }
            return result;
        }

        internal static void Restore(IEnumerable<Transferable> rows, Dictionary<Thing, int> selected)
        {
            foreach (var row in rows)
            {
                IEnumerable<Thing> things;
                if (row is Tradeable trade)
                {
                    if (trade.IsCurrency) continue;
                    things = trade.thingsColony.Concat(trade.thingsTrader);
                }
                else things = ((TransferableOneWay)row).things;
                long count = things.Sum(item => selected.TryGetValue(item, out int value) ? (long)value : 0L);
                row.AdjustTo((int)Math.Max(row.GetMinimumToTransfer(), Math.Min(row.GetMaximumToTransfer(), count)));
            }
        }
    }
}
