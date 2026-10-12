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
        internal Func<T, int> CountRequired;
    }
    internal static class CraftingMaterialPlanner
    {
        internal static bool TryPlan<T>(IList<CraftingMaterialStock<T>> stocks, IList<CraftingMaterialNeed<T>> needs,
            bool mix, bool wholeStacks, out Dictionary<T, int> result)
        {
            result = new Dictionary<T, int>();
            // Vanilla's no-mix whole-stack branch takes the first usable stack and
            // succeeds immediately, regardless of the declared ingredient count.
            // The allow-mix branch uses normal quantities even with this flag set.
            if (!mix && wholeStacks && needs.Count > 0)
            {
                foreach (var stock in stocks)
                    if (stock.Count > 0 && needs[0].Allows(stock.Item))
                    { result.Add(stock.Item, stock.Count); return true; }
                return false;
            }
            int[] used = new int[stocks.Count];
            var selectionOrder = new List<int>();
            foreach (var need in needs)
            {
                double left = need.Value;
                if (left <= 0) continue;
                object kind = null;
                if (!mix)
                {
                    // Vanilla groups all available stacks by def in encounter
                    // order before choosing the first def with enough units.
                    foreach (var group in Enumerable.Range(0, stocks.Count).GroupBy(i => stocks[i].Kind))
                    {
                        int first = group.Where(i => stocks[i].Value > 0 && need.Allows(stocks[i].Item) && stocks[i].Count > used[i])
                            .DefaultIfEmpty(-1).First();
                        if (first < 0) continue;
                        var stock = stocks[first];
                        int required = need.CountRequired != null ? need.CountRequired(stock.Item) : (int)Math.Ceiling(left / stock.Value);
                        long available = group.Where(i => need.Allows(stocks[i].Item))
                            .Sum(i => (long)Math.Max(0, stocks[i].Count - used[i]));
                        if (available >= required) { kind = stock.Kind; left = required; break; }
                    }
                    if (kind == null) return false;
                }
                for (int i = 0; i < stocks.Count && left > 0.0001; i++)
                {
                    var stock = stocks[i];
                    if (stock.Value <= 0 || !need.Allows(stock.Item) || (!mix && !Equals(stock.Kind, kind))) continue;
                    int available = Math.Max(0, stock.Count - used[i]);
                    int take = (int)Math.Min(available, mix ? Math.Ceiling(left / stock.Value) : left);
                    if (take > 0 && used[i] == 0) selectionOrder.Add(i);
                    used[i] += take; left -= take * (mix ? stock.Value : 1d);
                }
                if (left > 0.0001) return false;
            }
            foreach (int i in selectionOrder) result.Add(stocks[i].Item, used[i]);
            return true;
        }
    }
}
