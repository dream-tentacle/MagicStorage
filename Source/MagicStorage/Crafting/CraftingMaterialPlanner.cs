using System;
using System.Collections.Generic;
using System.Linq;

namespace MagicStorage
{
    // Engine-independent allocation over real-stack snapshots; one unit cannot satisfy two ingredients.
    internal sealed class CraftingMaterialStock<T>
    {
        internal T Item;
        internal object Kind;
        internal int Count;
        internal double Value;
    }
    internal sealed class CraftingMaterialNeed<T>
    {
        internal double Value;
        internal Predicate<T> Allows;
    }
    internal static class CraftingMaterialPlanner
    {
        internal static bool TryPlan<T>(IList<CraftingMaterialStock<T>> stocks, IList<CraftingMaterialNeed<T>> needs,
            bool mix, bool wholeStacks, out Dictionary<T, int> result)
        {
            result = new Dictionary<T, int>();
            int[] used = new int[stocks.Count];
            var selectionOrder = new List<int>();
            foreach (var need in needs)
            {
                double left = need.Value;
                if (left <= 0) continue;
                object kind = null;
                if (!mix)
                {
                    var totals = new Dictionary<object, double>();
                    foreach (int i in Enumerable.Range(0, stocks.Count))
                    {
                        var stock = stocks[i];
                        if (stock.Value <= 0 || !need.Allows(stock.Item)) continue;
                        if (!totals.ContainsKey(stock.Kind)) totals[stock.Kind] = 0;
                        totals[stock.Kind] += Math.Max(0, stock.Count - used[i]) * stock.Value;
                        if (totals[stock.Kind] + 0.0001 >= left) { kind = stock.Kind; break; }
                    }
                    if (kind == null) return false;
                }
                for (int i = 0; i < stocks.Count && left > 0.0001; i++)
                {
                    var stock = stocks[i];
                    if (stock.Value <= 0 || !need.Allows(stock.Item) || (!mix && !Equals(stock.Kind, kind))) continue;
                    int available = Math.Max(0, stock.Count - used[i]);
                    int take = wholeStacks ? available : (int)Math.Min(available, Math.Ceiling(left / stock.Value));
                    if (take > 0 && used[i] == 0) selectionOrder.Add(i);
                    used[i] += take; left -= take * stock.Value;
                }
                if (left > 0.0001) return false;
            }
            foreach (int i in selectionOrder) result.Add(stocks[i].Item, used[i]);
            return true;
        }
    }
}
