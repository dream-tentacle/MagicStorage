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
    internal sealed class CraftingMaterialShortage
    {
        internal int NeedIndex;
        internal object Kind;
        internal double Available, Required;
    }
    internal static class CraftingMaterialPlanner
    {
        internal static bool TryPlan<T>(IList<CraftingMaterialStock<T>> stocks, IList<CraftingMaterialNeed<T>> needs,
            bool mix, bool wholeStacks, out Dictionary<T, int> result,
            List<CraftingMaterialShortage> shortages = null)
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
                shortages?.Add(new CraftingMaterialShortage { NeedIndex = 0, Required = 1 });
                return false;
            }
            int[] used = new int[stocks.Count];
            var selectionOrder = new List<int>();
            bool complete = true;
            for (int needIndex = 0; needIndex < needs.Count; needIndex++)
            {
                var need = needs[needIndex];
                double left = need.Value;
                if (left <= 0) continue;
                object kind = null;
                double bestAvailable = 0, bestRequired = left;
                if (!mix)
                {
                    bool enough = false;
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
                        if (available > 0 && (kind == null || available / (double)required > bestAvailable / bestRequired))
                        { kind = stock.Kind; bestAvailable = available; bestRequired = required; }
                        if (available >= required) { kind = stock.Kind; left = required; enough = true; break; }
                    }
                    if (!enough)
                    {
                        // No complete kind was found. Continue only for diagnostics,
                        // consuming its available part so later needs cannot count it again.
                        if (shortages == null) return false;
                        shortages.Add(new CraftingMaterialShortage { NeedIndex = needIndex, Kind = kind,
                            Available = bestAvailable, Required = bestRequired });
                        complete = false;
                        left = bestRequired;
                    }
                }
                double requiredValue = left;
                for (int i = 0; i < stocks.Count && left > 0.0001; i++)
                {
                    var stock = stocks[i];
                    if (stock.Value <= 0 || !need.Allows(stock.Item) || (!mix && !Equals(stock.Kind, kind))) continue;
                    int available = Math.Max(0, stock.Count - used[i]);
                    int take = (int)Math.Min(available, mix ? Math.Ceiling(left / stock.Value) : left);
                    if (take > 0 && used[i] == 0) selectionOrder.Add(i);
                    used[i] += take; left -= take * (mix ? stock.Value : 1d);
                }
                if (left > 0.0001)
                {
                    if (shortages == null) return false;
                    if (mix) shortages.Add(new CraftingMaterialShortage { NeedIndex = needIndex,
                        Available = requiredValue - left, Required = requiredValue });
                    complete = false;
                }
            }
            if (!complete) return false;
            foreach (int i in selectionOrder) result.Add(stocks[i].Item, used[i]);
            return true;
        }
    }
}
