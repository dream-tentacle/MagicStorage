using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal interface ICraftingProductCounter
    {
        bool CanCount(RecipeDef recipe);
        long Count(Building_StorageCore core, CraftingOrder order, long stopAt = long.MaxValue);
    }
    internal sealed class MapAndNetworkCraftingProductCounter : ICraftingProductCounter
    {
        public bool CanCount(RecipeDef recipe) => recipe.specialProducts == null && recipe.products?.Count == 1;
        public long Count(Building_StorageCore core, CraftingOrder order, long stopAt = long.MaxValue)
        {
            if (stopAt <= 0 || !CanCount(order.Recipe) || !core.Spawned) return 0;
            var product = order.Recipe.products[0].thingDef;
            var countedDefs = new HashSet<ThingDef> { product };
            if (order.AdditionalCounts != null) countedDefs.UnionWith(order.AdditionalCounts.AllowedThingDefs);
            var filters = new CraftingProductFilters(order.Recipe);
            var seen = new HashSet<Thing>();
            long count = 0;
            // Unlike the resourceCounter fast path, this counts actual stacks consistently
            // with the same quality, condition and material filters in both locations.
            foreach (var def in countedDefs)
                foreach (var thing in core.Map.listerThings.ThingsOfDef(def))
                    if (Add(thing, order, product, filters, seen, ref count, stopAt)) return count;
            foreach (var thing in core.Map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing))
                if (Add(thing, order, product, filters, seen, ref count, stopAt)) return count;
            foreach (var pawn in core.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (Add(pawn.carryTracker?.CarriedThing, order, product, filters, seen, ref count, stopAt)) return count;
                if (!filters.IncludeEquipped || !order.IncludeEquipped || !pawn.IsFreeColonist) continue;
                if (pawn.equipment != null) foreach (var thing in pawn.equipment.AllEquipmentListForReading)
                    if (Add(thing, order, product, filters, seen, ref count, stopAt)) return count;
                if (pawn.apparel != null) foreach (var thing in pawn.apparel.WornApparel)
                    if (Add(thing, order, product, filters, seen, ref count, stopAt)) return count;
                if (pawn.inventory != null) foreach (var thing in pawn.inventory.innerContainer)
                    if (Add(thing, order, product, filters, seen, ref count, stopAt)) return count;
            }
            foreach (var source in core.Map.haulDestinationManager.AllHaulSourcesListForReading)
                foreach (var thing in source.GetDirectlyHeldThings())
                    if (Add(thing, order, product, filters, seen, ref count, stopAt)) return count;
            // Completed goods waiting for a free drop cell still count. Otherwise a
            // target-count order could manufacture them again while placement is blocked.
            var pending = new List<IThingHolder>();
            var manager = core.Map.GetComponent<MapComponent_StorageNetworks>();
            manager.GetChildHolders(pending);
            // A garment waiting for vanilla Wear is still a physical map item.
            foreach (var adapter in manager.ApparelAdapters)
                foreach (var thing in adapter.GetDirectlyHeldThings())
                    if (Add(thing, order, product, filters, seen, ref count, stopAt)) return count;
            foreach (var holder in pending)
            {
                var items = holder.GetDirectlyHeldThings();
                if (items != null) foreach (var thing in items)
                    if (Add(thing, order, product, filters, seen, ref count, stopAt)) return count;
            }
            // Select indexed def buckets before reading stacks. Wrapped products have the
            // wrapper's def in the index, so include MinifiedThing subclasses for inspection.
            core.Network?.VisitStoredStacksMatching(def => countedDefs.Contains(def) ||
                (def.thingClass != null && typeof(MinifiedThing).IsAssignableFrom(def.thingClass)),
                thing => !Add(thing, order, product, filters, seen, ref count, stopAt));
            return count;
        }
        private static bool Add(Thing outer, CraftingOrder order, ThingDef product, CraftingProductFilters filters,
            HashSet<Thing> seen, ref long count, long stopAt)
        {
            if (outer == null || outer.Destroyed) return false;
            Thing thing = outer.GetInnerIfMinified();
            if (thing == null || !seen.Add(thing)) return false;
            // The primary product keeps its existing count conditions. Additional items
            // use their own filter; ingredient restrictions must not exclude substitutes.
            if (thing.def == product)
            {
                if (filters.IncludeTainted && !order.IncludeTainted && thing is Apparel apparel && apparel.WornByCorpse) return false;
                if (filters.HitPoints && thing.def.useHitPoints && !order.HitPoints.IncludesEpsilon((float)thing.HitPoints / thing.MaxHitPoints)) return false;
                if (filters.Quality && thing.TryGetQuality(out var quality) && !order.Quality.Includes(quality)) return false;
                if (filters.AllowedStuff && order.LimitToAllowedStuff && !order.Ingredients.Allows(thing.Stuff)) return false;
            }
            else if (order.AdditionalCounts?.Allows(thing) != true) return false;
            if (thing.SpawnedOrAnyParentSpawned && thing.PositionHeld.Fogged(thing.MapHeld)) return false;
            long amount = (long)thing.stackCount * (outer == thing ? 1 : outer.stackCount);
            count += System.Math.Min(amount, stopAt - count);
            return count >= stopAt;
        }
    }

    internal static class CraftingServices
    {
        internal static ICraftingWorkerPolicy Workers = new VanillaCraftingWorkerPolicy();
        internal static ICraftingMaterialPolicy Materials = new NetworkCraftingMaterialPolicy();
        internal static ICraftingProductCounter Products = new MapAndNetworkCraftingProductCounter();
        internal static ICraftingWorkBehavior Work = new VanillaCraftingWorkBehavior();
        internal static ICraftingProductionPolicy Production = new VanillaCraftingProductionPolicy();
    }
}
