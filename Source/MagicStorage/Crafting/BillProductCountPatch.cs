using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Adds stored network stock to a vanilla bill's "do until X" count.
    //
    // RecipeWorkerCounter.CountProducts answers from the map's ResourceCounter only when the bill
    // has no product filters; every other case counts real map things, which stored stock is not.
    // The ResourceCounter already carries network stock (StorageResourceCounter), so the fast path
    // must never see this addition. Everything below is a delta on top of vanilla's own answer.
    [StaticConstructorOnStartup]
    internal static class BillProductCountPatch
    {
        private static bool loggedFailure;

        static BillProductCountPatch()
        {
            new Harmony("mjcg.magicstorage.billcount").Patch(
                AccessTools.Method(typeof(RecipeWorkerCounter), nameof(RecipeWorkerCounter.CountProducts)),
                postfix: new HarmonyMethod(typeof(BillProductCountPatch), nameof(CountProducts)));
        }

        private static void CountProducts(RecipeWorkerCounter __instance, Bill_Production bill, ref int __result)
        {
            RecipeDef recipe = __instance?.recipe;
            if (recipe?.products == null || recipe.products.Count == 0 || bill?.Map == null) return;
            ThingDef product = recipe.products[0].thingDef;
            if (product == null) return;
            // The included group is an explicit "count this stockpile" instruction; stored stock
            // lives in no group, so adding it would contradict the player's choice.
            if (UsesResourceCounter(bill, product) || bill.GetIncludeSlotGroup() != null) return;
            try { __result = (int)Math.Min(int.MaxValue, (long)__result + StoredCount(bill, product)); }
            catch (Exception exception)
            {
                // A bill must keep working even if the storage scan fails.
                if (loggedFailure) return;
                loggedFailure = true;
                Log.Error("[MagicStorage] Bill target count could not read stored stock: " + exception);
            }
        }

        // Vanilla's fast path condition, copied from RecipeWorkerCounter.CountProducts. Meeting it
        // means the map's ResourceCounter answered, and that number already contains stored stock.
        private static bool UsesResourceCounter(Bill_Production bill, ThingDef product)
        {
            return product.CountAsResource && !bill.includeEquipped &&
                (bill.includeTainted || !product.IsApparel || !product.apparel.careIfWornByCorpse) &&
                bill.hpRange.min == 0f && bill.hpRange.max == 1f &&
                bill.qualityRange.min == QualityCategory.Awful &&
                bill.qualityRange.max == QualityCategory.Legendary &&
                !bill.limitToAllowedStuff;
        }

        // Vanilla's slow path is itself an uncached scan of every map thing of the product, so this
        // is the same kind of work on the same call. Only units the ResourceCounter readout counts
        // are used, so the two numbers cannot disagree.
        private static int StoredCount(Bill_Production bill, ThingDef product)
        {
            Map map = bill.Map;
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            if (manager == null) return 0;
            manager.EnsureCurrent();
            var networks = new HashSet<StorageNetwork>();
            foreach (Building_StorageUnit unit in manager.StorageUnits)
                if (StorageResourceCounter.IsCounted(unit, map)) networks.Add(unit.Network);
            if (networks.Count == 0) return 0;

            var filters = new StoredProductFilter(bill, product);
            var stacks = new List<Thing>();
            long count = 0;
            foreach (StorageNetwork network in networks)
            {
                stacks.Clear();
                network.GetStacks(product, stacks);
                foreach (Thing stack in stacks)
                    if (!stack.Destroyed && filters.Allows(stack)) count += stack.stackCount;
                if (!product.Minifiable) continue;
                // Minified products are indexed under the wrapper's def, as vanilla also counts
                // them separately from the plain product list. The visitor returns true so the
                // scan keeps going; returning false would stop it after this stack.
                long minified = 0;
                network.VisitStoredStacksMatching(
                    def => def.thingClass != null && typeof(MinifiedThing).IsAssignableFrom(def.thingClass),
                    outer => { minified += MinifiedAmount(outer, filters); return true; });
                count += minified;
            }
            return (int)Math.Min(int.MaxValue, count);
        }

        private static long MinifiedAmount(Thing outer, StoredProductFilter filters)
        {
            // The plain product list already covered a wrapper whose own def is the product.
            if (outer == null || outer.Destroyed || outer.def == filters.Product) return 0;
            Thing inner = outer.GetInnerIfMinified();
            if (inner == null || inner.Destroyed || inner == outer || !filters.Allows(inner)) return 0;
            return (long)outer.stackCount * inner.stackCount;
        }

        // Vanilla filters products with RecipeWorkerCounter.CountValidThing. Its fog check is
        // dropped: stored things are never spawned, and fogged units are excluded up front.
        private sealed class StoredProductFilter
        {
            internal readonly ThingDef Product;
            private readonly bool includeTainted, limitToAllowedStuff;
            private readonly FloatRange hitPoints;
            private readonly QualityRange quality;
            private readonly ThingFilter ingredients;

            internal StoredProductFilter(Bill_Production bill, ThingDef product)
            {
                Product = product;
                includeTainted = bill.includeTainted;
                limitToAllowedStuff = bill.limitToAllowedStuff;
                hitPoints = bill.hpRange;
                quality = bill.qualityRange;
                ingredients = bill.ingredientFilter;
            }

            internal bool Allows(Thing thing)
            {
                if (thing.def != Product) return false;
                if (!includeTainted && Product.IsApparel && thing is Apparel apparel && apparel.WornByCorpse)
                    return false;
                if (Product.useHitPoints &&
                    !hitPoints.IncludesEpsilon((float)thing.HitPoints / thing.MaxHitPoints)) return false;
                CompQuality compQuality = thing.TryGetComp<CompQuality>();
                if (compQuality != null && !quality.Includes(compQuality.Quality)) return false;
                // Vanilla calls Allows(thing.Stuff) unconditionally when this is set. A product
                // that is not made from stuff has no Stuff, which Allows rejects, so the guard on
                // MadeFromStuff is deliberately absent here.
                if (limitToAllowedStuff &&
                    (ingredients == null || !ingredients.Allows(thing.Stuff))) return false;
                return true;
            }
        }
    }
}
