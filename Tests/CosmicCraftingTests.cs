using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;
using Verse.AI;

internal static class CosmicCraftingTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Cosmic crafting: " + message + "; expected " + expected + ", got " + actual); }
    internal static int Run()
    {
        OrderReordering(); FilteredOrderReordering(); Modes(); Ingredients(); WorkerRules(); AssignmentLifecycle(); ProductCounts(); AdditionalProductCounts(); MissingAdditionalCounts(); ProductFilterApplicability(); LoadAndBuildings();
        ProductionCompletion(); ConfigurableWorkSpeed(); ProductionOverflow(); ProductionFloorDelivery(); ProductionCancellationAndFailure();
        ReservationInvalidation();
        CoreContainerSelection();
        BenchSelectionAndPriority();
        OrderedJobPreflight();
        WorkerFailurePrecedesOccupation();
        FixedCostMaterials();
        VanillaMaterialSelection();
        IndexedProductCounts(); LimitedProductCounts(); SuspendedProductChecks(); SingleProductCheckPerOrder();
        RefreshDoesNotChangeWork();
        MaterialRetryCaching(); EarlyWorkerEligibility();
        return checks;
    }

    private static void OrderReordering()
    {
        var list = new CraftingOrderList();
        var a = list.Add(new RecipeDef()); var b = list.Add(new RecipeDef());
        var c = list.Add(new RecipeDef()); var d = list.Add(new RecipeDef());
        b.Suspended = true; b.RepeatCount = 7;
        int revision = b.Revision;
        list.Reorder(2, 0);
        Equal(true, list.Orders.SequenceEqual(new[] { c, a, b, d }), "drag backward uses the destination insertion slot");
        list.Reorder(0, 4);
        Equal(true, list.Orders.SequenceEqual(new[] { a, b, d, c }), "drag to the end adjusts for source removal");
        list.Reorder(0, 3);
        Equal(true, list.Orders.SequenceEqual(new[] { b, d, a, c }), "drag forward inserts before the original target");
        list.Reorder(0, 1); list.Reorder(1, 1);
        Equal(true, list.Orders.SequenceEqual(new[] { b, d, a, c }), "dropping immediately before or after oneself does not move the order");
        list.Reorder(-1, 0); list.Reorder(4, 0); list.Reorder(0, -1); list.Reorder(0, 5);
        Equal(true, list.Orders.SequenceEqual(new[] { b, d, a, c }), "invalid drag indexes cannot mutate the order list");
        Equal(b, list.Find(b.Id), "dragging preserves task identity");
        Equal(true, b.Suspended, "dragging preserves suspension");
        Equal(7, b.RepeatCount, "dragging preserves remaining production count");
        Equal(revision, b.Revision, "priority changes do not invalidate in-progress task configuration");
    }

    private static void FilteredOrderReordering()
    {
        var list = new CraftingOrderList();
        var hiddenFirst = list.Add(new RecipeDef()); var a = list.Add(new RecipeDef());
        var hiddenMiddle = list.Add(new RecipeDef()); var b = list.Add(new RecipeDef());
        var c = list.Add(new RecipeDef()); var hiddenLast = list.Add(new RecipeDef());
        list.Reorder(new[] { a, b, c }, 0, 3);
        Equal(true, list.Orders.SequenceEqual(new[] { hiddenFirst, b, hiddenMiddle, c, a, hiddenLast }),
            "filtered forward drag changes only visible slots and preserves hidden task positions");
        list.Reorder(new[] { b, c, a }, 2, 0);
        Equal(true, list.Orders.SequenceEqual(new[] { hiddenFirst, a, hiddenMiddle, b, c, hiddenLast }),
            "filtered backward drag preserves hidden tasks before, between and after visible tasks");
        var expected = list.Orders.ToArray();
        list.Reorder(new[] { a, b, c }, 1, 2);
        list.Reorder(new[] { a }, 0, 1);
        list.Reorder(new CraftingOrder[0], 0, 0);
        list.Reorder(null, 0, 0);
        Equal(true, list.Orders.SequenceEqual(expected), "adjacent, single-item and empty filtered drags preserve all tasks");
        list.Reorder(new[] { a, a, c }, 0, 3);
        Equal(true, list.Orders.SequenceEqual(expected), "duplicate visible tasks cannot overwrite hidden slots");
        list.Reorder(new[] { c, b, a }, 0, 3);
        Equal(true, list.Orders.SequenceEqual(expected), "out-of-order visible snapshots are rejected atomically");
        list.Remove(b);
        expected = list.Orders.ToArray();
        list.Reorder(new[] { a, b, c }, 0, 3);
        Equal(true, list.Orders.SequenceEqual(expected), "a task removed after the snapshot cannot corrupt filtered drag results");
    }

    private sealed class CountingMaterialPolicy : ICraftingMaterialPolicy
    {
        internal int Calls;
        internal readonly ICraftingMaterialPolicy Inner = new NetworkCraftingMaterialPolicy();
        public bool TryPlan(StorageNetwork network, CraftingOrder order, out Dictionary<Thing, int> allocations)
        { Calls++; return Inner.TryPlan(network, order, out allocations); }
    }

    private static void MaterialRetryCaching()
    {
        var previousPolicy = CraftingServices.Materials;
        int previousTick = Find.TickManager.TicksGame;
        var previousMenuPawn = FloatMenuMakerMap.makingFor;
        try
        {
            MagicStorageMod.Settings.RestoreDefaults(); Find.TickManager.TicksGame = 10000;
            var f = new Fixture(); var a = f.Core(0); var order = f.Order(a.Item1, 20);
            var unit = (Building_StorageUnit)a.Item3.holdingOwner.Owner;
            var policy = new CountingMaterialPolicy(); CraftingServices.Materials = policy;
            Equal(null, f.Dispatch.FindJob(f.Pawn, f.Giver), "first missing-material query performs a real search");
            Equal(1, policy.Calls, "initial material search counted once");
            Equal((int?)10000, order.LastMaterialFailureTick, "failed search stores actual check time");
            var other = new Pawn { Map = f.Map, Spawned = true, Faction = Faction.OfPlayer };
            Equal(null, f.Dispatch.FindJob(other, f.Giver), "other pawn shares this order's failure cache");
            Equal(1, policy.Calls, "other pawn does not rescan missing materials");
            Equal("MS_Craft_MissingMaterials", f.Dispatch.Status(a.Item1, order), "window status shares failure result");
            Equal(1, policy.Calls, "status query never resets or rescans a fresh failure");
            unit.Inventory.Contents.TryAdd(new Thing { def = f.Material, stackCount = 20 });
            order.Changed(); MagicStorageMod.Settings.OverallRefreshSeconds = 60;
            Find.TickManager.TicksGame = 10900;
            Equal(null, f.Dispatch.FindJob(f.Pawn, f.Giver), "inventory and order changes do not invalidate time-only failure cache");
            Equal(1, policy.Calls, "fresh stock waits for independent thirty-second interval");
            Equal((int?)10000, order.LastMaterialFailureTick, "cache hit does not slide the deadline forward");
            Find.TickManager.TicksGame = 11800;
            Equal(true, f.Dispatch.FindJob(f.Pawn, f.Giver) != null, "next query at retry boundary discovers added materials");
            Equal(2, policy.Calls, "elapsed retry interval performs one fresh search");
            Equal(null, order.LastMaterialFailureTick, "successful search clears failure cache");
            Equal(true, f.Dispatch.FindJob(other, f.Giver) != null, "success is not cached as a material plan");
            Equal(3, policy.Calls, "another successful query still plans materials live");
            unit.Inventory.Contents.Remove(a.Item3);
            Equal(null, f.Dispatch.FindJob(f.Pawn, f.Giver), "new failure begins a new retry interval");
            Equal(4, policy.Calls, "new failure was actually checked");
            Find.TickManager.TicksGame = 15400;
            f.Map.GetComponent<MapComponent_StorageNetworks>().MapComponentTick(); f.Dispatch.MapComponentTick();
            Equal(4, policy.Calls, "elapsed intervals without queries never trigger background material checks");
            Equal((int?)11800, order.LastMaterialFailureTick, "expiration never flushes or rewrites failure time in background");
            Equal(null, f.Dispatch.FindJob(f.Pawn, f.Giver), "late query retries only once rather than replaying missed intervals");
            Equal(5, policy.Calls, "late retry counted once");
            MagicStorageMod.Settings.CraftingMaterialRecheckSeconds = 60; Find.TickManager.TicksGame = 17200;
            Equal(null, f.Dispatch.FindJob(f.Pawn, f.Giver), "longer setting immediately extends current failure interval");
            Equal(5, policy.Calls, "current cache reads live retry setting");
            MagicStorageMod.Settings.CraftingMaterialRecheckSeconds = 1;
            Equal(null, f.Dispatch.FindJob(f.Pawn, f.Giver), "shorter setting allows retry on next query");
            Equal(6, policy.Calls, "shorter interval does not require cache reset");
            MagicStorageMod.Settings.CraftingMaterialRecheckSeconds = 0;
            f.Dispatch.FindJob(f.Pawn, f.Giver); f.Dispatch.FindJob(f.Pawn, f.Giver);
            Equal(8, policy.Calls, "zero setting checks every query even within the same tick");
            MagicStorageMod.Settings.CraftingMaterialRecheckSeconds = 30;
            unit.Inventory.Contents.TryAdd(a.Item3, false);
            FloatMenuMakerMap.makingFor = f.Pawn;
            var job = new WorkGiver_CosmicCrafting { def = f.Giver }.JobOnThing(f.Pawn, a.Item2);
            Equal(true, job != null, "native menu generation bypasses missing-material delay");
            Equal(9, policy.Calls, "manual menu checks materials immediately");
            FloatMenuMakerMap.makingFor = null;
            unit.Inventory.Contents.Remove(a.Item3);
            var driver = new JobDriver_CosmicCrafting { pawn = f.Pawn, job = job };
            Equal(false, f.Dispatch.TryClaim(driver, out _), "formal claim rejects materials lost after successful find");
            Equal(10, policy.Calls, "formal claim still computes a fresh material plan");
            unit.Inventory.Contents.TryAdd(a.Item3, false);
            Equal(true, new WorkGiver_CosmicCrafting { def = f.Giver }.JobOnThing(f.Pawn, a.Item2, true) != null,
                "forced job query also bypasses failure cache");
            Equal(11, policy.Calls, "forced query searches again despite same-tick failure");
            unit.Inventory.Contents.Remove(a.Item3); f.Dispatch.FindJob(f.Pawn, f.Giver);
            Find.TickManager.TicksGame = 100;
            f.Dispatch.FindJob(f.Pawn, f.Giver);
            Equal(13, policy.Calls, "clock rewind allows a fresh check rather than retaining future failure forever");
        }
        finally
        {
            CraftingServices.Materials = previousPolicy; Find.TickManager.TicksGame = previousTick;
            FloatMenuMakerMap.makingFor = previousMenuPawn; MagicStorageMod.Settings.RestoreDefaults();
        }
    }

    private static void EarlyWorkerEligibility()
    {
        var previousPolicy = CraftingServices.Materials;
        try
        {
            var f = new Fixture(); var a = f.Core(0); var order = f.Order(a.Item1);
            order.Mode = CraftingRepeatMode.TargetCount; order.TargetCount = 10; order.Worker = new Pawn();
            var products = (TestProductCounter)CraftingServices.Products;
            var materials = new CountingMaterialPolicy(); CraftingServices.Materials = materials;
            Equal(null, f.Dispatch.FindJob(f.Pawn, f.Giver), "automatic work excludes another named worker");
            Equal(0, products.Calls, "wrong named worker never counts products");
            Equal(0, materials.Calls, "wrong named worker never scans ingredients");
            order.Worker = null; order.Recipe.minimumSkill = 20;
            Equal(null, f.Dispatch.FindJob(f.Pawn, f.Giver), "automatic work excludes insufficient recipe skill");
            Equal(0, products.Calls, "insufficient skill never counts products");
            Equal(0, materials.Calls, "insufficient skill never scans ingredients");
            order.Recipe.minimumSkill = 0; order.Worker = new Pawn();
            products.Amount = 10; JobFailReason.Clear();
            Equal(null, f.Dispatch.FindJobOnBench(f.Pawn, f.Giver, a.Item2, true), "manual query keeps satisfied-order precedence");
            Equal("MS_Craft_Satisfied", JobFailReason.Reason, "manual failure reason retains original ordering");
            Equal(1, products.Calls, "manual reason may count products for original precedence");
            products.Amount = 0;
            Equal(null, f.Dispatch.FindJobOnBench(f.Pawn, f.Giver, a.Item2, true), "manual query rejects wrong named worker below target");
            Equal("MS_Craft_SelectedWorkerNotAllowed", JobFailReason.Reason, "manual worker restriction reason preserved");
            Equal(0, materials.Calls, "manual wrong worker still never plans ingredients");
            order.Worker = null;
            Equal(true, f.Dispatch.FindJob(f.Pawn, f.Giver) != null, "eligible worker can immediately find the same order");
            Equal(1, materials.Calls, "eligible worker does perform live material planning");
        }
        finally { CraftingServices.Materials = previousPolicy; }
    }

    private sealed class TestPackedProduct : MinifiedThing { }

    private static void RefreshDoesNotChangeWork()
    {
        var settings = MagicStorageMod.Settings;
        try
        {
            foreach (int seconds in new[] { 1, 60 })
            {
                settings.SimplifiedMode = true; settings.OverallRefreshSeconds = seconds;
                var f = new Fixture(); var a = f.Core(0); var order = f.Order(a.Item1, 3);
                order.Mode = CraftingRepeatMode.RepeatCount; order.RepeatCount = 3;
                var policy = (TestProductionPolicy)CraftingServices.Production; policy.Required = 5;
                var driver = f.Start(f.Dispatch.FindJob(f.Pawn, f.Giver));
                var work = driver.TestToils.Last();
                work.tickIntervalAction(2);
                Equal(0.4f, order.Progress, "refresh interval does not change actual work progression");
                Equal(0.2f, f.Pawn.CraftingXp, "refresh interval does not multiply experience");
                work.tickIntervalAction(3600);
                Equal(0.5f, f.Pawn.CraftingXp, "large update grants experience only for work needed to finish");
                Equal(4L, a.Item1.Network.GetTotalCount(policy.Product), "large update completes exactly one batch");
                Equal(1, policy.MakeCalls, "large update never replays production for missed refresh cycles");
                Equal(2, order.RepeatCount, "only one repeat credited");
                Equal(7L, a.Item1.Network.GetTotalCount(f.Material), "exactly one batch's ingredients consumed");
            }
        }
        finally { settings.RestoreDefaults(); }
    }

    private static void IndexedProductCounts()
    {
        var f = new Fixture(); var a = f.Core(0);
        var unit = (Building_StorageUnit)a.Item3.holdingOwner.Owner;
        unit.SlotCapacity = 10010;
        var product = new ThingDef { hasQualityComp = true };
        var extra = new ThingDef(); var unrelated = new ThingDef();
        var packedDef = new ThingDef { thingClass = typeof(TestPackedProduct) };
        var recipe = new RecipeDef { route = f.Giver };
        recipe.products.Add(new ThingDefCountClass { thingDef = product });
        var order = a.Item1.Crafting.Add(recipe);
        order.AdditionalCounts.SetAllow(extra, true);
        order.AdditionalCounts.SetAllow(product, true);
        var normal = new Thing { def = product, stackCount = 4, hasQuality = true, quality = QualityCategory.Poor };
        unit.Inventory.Contents.TryAdd(normal, false);
        unit.Inventory.Contents.TryAdd(new Thing { def = extra, stackCount = 5 }, false);
        var wrappedProduct = new TestPackedProduct
        {
            def = packedDef, stackCount = 2,
            InnerThing = new Thing { def = product, stackCount = 3, hasQuality = true, quality = QualityCategory.Good }
        };
        unit.Inventory.Contents.TryAdd(wrappedProduct, false);
        unit.Inventory.Contents.TryAdd(new TestPackedProduct
            { def = packedDef, InnerThing = new Thing { def = extra, stackCount = 2 } }, false);
        unit.Inventory.Contents.TryAdd(new TestPackedProduct
            { def = packedDef, InnerThing = new Thing { def = unrelated, stackCount = 9 } }, false);
        for (int i = 0; i < 10000; i++)
            if (!unit.Inventory.Contents.TryAdd(new Thing { def = unrelated }, false))
                throw new Exception("Large product-count fixture ran out of slots.");
        var claim = new object();
        Equal(4, a.Item1.Network.ReserveOutgoing(claim, normal, 4), "product count fixture reserves all normal products");
        var counter = new MapAndNetworkCraftingProductCounter();
        Thing.InnerLookups = 0;
        Equal(17L, counter.Count(a.Item1, order), "indexed products, additional goods and wrapped quantities all counted once");
        Equal(5, Thing.InnerLookups, "ten thousand unrelated stacks never enter the item-counting path");
        Equal(0, a.Item1.Network.AvailableToWithdraw(normal), "physical count includes stock already reserved by another job");
        order.Quality = new QualityRange { min = QualityCategory.Normal, max = QualityCategory.Legendary };
        Equal(13L, counter.Count(a.Item1, order), "wrapped primary product still observes quality while additional goods retain their own rules");
        long revision = a.Item1.Network.Revision;
        wrappedProduct.InnerThing.quality = QualityCategory.Poor;
        Equal(revision, a.Item1.Network.Revision, "quality changes do not alter the quantity revision");
        Equal(7L, counter.Count(a.Item1, order), "fresh filtering sees quality changes without relying on network revision");
        wrappedProduct.InnerThing.quality = QualityCategory.Good;
        f.Register(new Building_StorageCore(), 3);
        Equal(false, a.Item1.Network.CanWork, "duplicate core makes fixture network unavailable");
        Equal(13L, counter.Count(a.Item1, order), "read-only counting preserves physical inventory visibility with duplicate cores");
        var consumable = new List<Thing>();
        a.Item1.Network.GetMatchingStacks(def => def == product, consumable);
        Equal(0, consumable.Count, "consumption queries continue to require a working network");
    }

    private static void LimitedProductCounts()
    {
        var f = new Fixture(); var a = f.Core(0);
        var unit = (Building_StorageUnit)a.Item3.holdingOwner.Owner; unit.SlotCapacity = 10010;
        var product = new ThingDef(); var order = f.Order(a.Item1);
        order.Recipe.products.Add(new ThingDefCountClass { thingDef = product });
        order.Mode = CraftingRepeatMode.TargetCount; order.TargetCount = 10;
        for (int i = 0; i < 10000; i++)
            if (!unit.Inventory.Contents.TryAdd(new Thing { def = product }, false))
                throw new Exception("Large limited-count fixture ran out of slots.");
        var counter = new MapAndNetworkCraftingProductCounter();
        Thing.InnerLookups = 0;
        Equal(10L, counter.Count(a.Item1, order, order.ProductCountLimit), "target count stops at required quantity");
        Equal(10, Thing.InnerLookups, "only ten of ten thousand matching network stacks inspected");
        Thing.InnerLookups = 0;
        Equal(0L, counter.Count(a.Item1, order, 0), "zero target needs no inventory traversal");
        Equal(0, Thing.InnerLookups, "zero target never inspects a stack");
        f.Map.listerThings.Things.Add(new Thing { def = product, stackCount = 50, Spawned = true, Map = f.Map });
        Thing.InnerLookups = 0;
        Equal(10L, counter.Count(a.Item1, order, 10), "one map stack can satisfy target");
        Equal(1, Thing.InnerLookups, "satisfied map count skips the network entirely");

        f = new Fixture(); a = f.Core(0); unit = (Building_StorageUnit)a.Item3.holdingOwner.Owner;
        product = new ThingDef { hasQualityComp = true }; order = f.Order(a.Item1);
        order.Recipe.products.Add(new ThingDefCountClass { thingDef = product });
        order.Mode = CraftingRepeatMode.TargetCount; order.TargetCount = 10; order.PauseWhenSatisfied = true; order.UnpauseAt = 5;
        var goods = new Thing { def = product, stackCount = 10, hasQuality = true, quality = QualityCategory.Good };
        unit.Inventory.Contents.TryAdd(goods, false); CraftingServices.Products = counter;
        Equal(false, f.Dispatch.CanRun(a.Item1, order), "enough goods pause the order");
        Equal(true, order.Paused, "satisfaction sets paused state");
        Equal(6L, order.ProductCountLimit, "paused count only distinguishes above resume threshold");
        unit.Inventory.Contents.Remove(goods); goods.stackCount = 8; unit.Inventory.Contents.TryAdd(goods, false);
        Equal(false, f.Dispatch.CanRun(a.Item1, order), "capped count preserves pause above resume threshold");
        unit.Inventory.Contents.Remove(goods); goods.stackCount = 5; unit.Inventory.Contents.TryAdd(goods, false);
        Equal(5L, counter.Count(a.Item1, order, 10), "below target count remains exact");
        Equal(true, f.Dispatch.CanRun(a.Item1, order), "order resumes exactly at resume threshold");
        Equal(false, order.Paused, "resume clears paused state");
        order.Quality = new QualityRange { min = QualityCategory.Excellent, max = QualityCategory.Legendary };
        Equal(0L, counter.Count(a.Item1, order, 1), "early exit still respects quality filters");
        order.Quality = new QualityRange { min = QualityCategory.Normal, max = QualityCategory.Legendary };
        var packedDef = new ThingDef { thingClass = typeof(TestPackedProduct) };
        unit.Inventory.Contents.TryAdd(new TestPackedProduct { def = packedDef, stackCount = 2,
            InnerThing = new Thing { def = product, stackCount = 3, hasQuality = true, quality = QualityCategory.Good } }, false);
        Equal(10L, counter.Count(a.Item1, order, 10), "wrapped quantities saturate safely at target");
        Equal(11L, counter.Count(a.Item1, order), "uncapped caller can still request exact quantity");
    }

    private static void SuspendedProductChecks()
    {
        var f = new Fixture(); var a = f.Core(0); var order = f.Order(a.Item1);
        order.Mode = CraftingRepeatMode.TargetCount; order.TargetCount = 10;
        order.Suspended = true; order.PauseWhenSatisfied = true; order.UnpauseAt = 3;
        var counter = (TestProductCounter)CraftingServices.Products; counter.Amount = 20;
        Equal(false, f.Dispatch.CanRun(a.Item1, order), "manually suspended target order cannot run");
        Equal(0, counter.Calls, "manual suspension never counts inventory");
        Equal(false, order.Paused, "manual suspension does not activate target hysteresis");
        order.Mode = CraftingRepeatMode.Forever; order.Paused = true;
        Equal(false, f.Dispatch.CanRun(a.Item1, order), "suspended forever order remains stopped");
        Equal(false, order.Paused, "non-target mode retains native pause-state reset");
        order.Mode = CraftingRepeatMode.TargetCount; order.Paused = true; order.Suspended = false; counter.Amount = 7;
        Equal(false, f.Dispatch.CanRun(a.Item1, order), "target-paused order waits for its low-water mark");
        Equal(1, counter.Calls, "target-paused order still checks live count for resumption");
        counter.Amount = 3;
        Equal(true, f.Dispatch.CanRun(a.Item1, order), "target-paused order resumes at low-water mark");
        Equal(false, order.Paused, "resumption clears target pause");
        counter.Amount = 10;
        Equal(false, f.Dispatch.CanRun(a.Item1, order), "target order pauses again when satisfied");
        Equal(true, order.Paused, "satisfaction sets target pause");
        Equal(3, counter.Calls, "each live target decision still reads an up-to-date count");
    }

    private static void SingleProductCheckPerOrder()
    {
        var f = new Fixture(); var a = f.Core(0); var first = f.Order(a.Item1);
        first.Mode = CraftingRepeatMode.TargetCount; first.TargetCount = 10;
        var counter = (TestProductCounter)CraftingServices.Products; counter.Amount = 10;
        JobFailReason.Clear();
        Equal(null, f.Dispatch.FindJobOnBench(f.Pawn, f.Giver, a.Item2), "satisfied order produces no work");
        Equal(1, counter.Calls, "failure explanation reuses its immediately preceding target check");
        Equal("MS_Craft_Satisfied", JobFailReason.Reason, "satisfied-order failure reason preserved");
        counter.Calls = 0;
        Equal("MS_Craft_Satisfied", f.Dispatch.Status(a.Item1, first), "standalone status still performs its own check");
        Equal(1, counter.Calls, "standalone status is not a stale cached result");
        var second = f.Order(a.Item1); second.Mode = CraftingRepeatMode.TargetCount; second.TargetCount = 10;
        counter.Calls = 0;
        Equal(null, f.Dispatch.FindJobOnBench(f.Pawn, f.Giver, a.Item2), "multiple satisfied orders produce no work");
        Equal(2, counter.Calls, "each rejected order is counted once rather than twice");
        counter.Amount = 9; counter.Calls = 0;
        Equal(true, f.Dispatch.FindJobOnBench(f.Pawn, f.Giver, a.Item2) != null, "new lower quantity immediately makes work available");
        Equal(1, counter.Calls, "accepted first order needs only one target check");
    }
    private sealed class AdjustedIngredientWorker : RecipeWorker
    {
        internal int Calls;
        public override float GetIngredientCount(IngredientCount ingredient, Bill bill)
        { Calls++; return ingredient.GetBaseCount() * 2; }
    }
    private static void FixedCostMaterials()
    {
        var f = new Fixture(); var a = f.Core(0); var order = f.Order(a.Item1);
        var unit = (Building_StorageUnit)a.Item3.holdingOwner.Owner;
        unit.SlotCapacity = 64; a.Item3.holdingOwner.Remove(a.Item3);
        // Generated fixed-cost recipes can leave both recipe-wide filters empty.
        order.Recipe.fixedIngredientFilter.Allowed.Clear(); order.Ingredients.Allowed.Clear();
        order.Recipe.ingredients.Clear();
        var defs = new[] { new ThingDef(), new ThingDef(), new ThingDef(), new ThingDef() };
        int[] available = { 793, 214, 300, 44 }, required = { 200, 20, 50, 4 };
        for (int i = 0; i < defs.Length; i++)
        {
            var ingredient = new IngredientCount { count = required[i] }; ingredient.filter.Allowed.Add(defs[i]);
            order.Recipe.ingredients.Add(ingredient);
            int remaining = available[i];
            while (remaining > 0)
            {
                int count = Math.Min(remaining, defs[i].stackLimit);
                Equal(true, unit.Inventory.Contents.TryAdd(new Thing { def = defs[i], stackCount = count }), "fixed-cost stock stored");
                remaining -= count;
            }
        }
        var policy = new NetworkCraftingMaterialPolicy();
        Equal(true, policy.TryPlan(a.Item1.Network, order, out var plan), "all four fixed ingredients survive empty recipe-wide filters");
        for (int i = 0; i < defs.Length; i++)
            Equal(required[i], plan.Where(p => p.Key.def == defs[i]).Sum(p => p.Value), "fixed ingredient quantity across stacks");
        var driver = f.Start(f.Dispatch.FindJob(f.Pawn, f.Giver));
        for (int i = 0; i < defs.Length; i++)
            Equal(required[i], order.Batch.Ingredients.Where(t => t.def == defs[i]).Sum(t => t.stackCount), "fixed-cost job collects exact ingredient count");
        driver.EndJobWith(JobCondition.InterruptForced);

        // Variable ingredients continue to respect both the recipe and player filters.
        f = new Fixture(); a = f.Core(0); order = f.Order(a.Item1);
        order.Recipe.ingredients[0].filter.Allowed.Add(new ThingDef());
        order.Ingredients.Allowed.Clear();
        Equal(false, policy.TryPlan(a.Item1.Network, order, out _), "non-fixed ingredient respects player exclusion");
        order.Ingredients.Allowed.Add(f.Material); order.Recipe.fixedIngredientFilter.Allowed.Clear();
        Equal(false, policy.TryPlan(a.Item1.Network, order, out _), "non-fixed ingredient respects recipe-wide exclusion");
        order.Recipe.fixedIngredientFilter.Allowed.Add(f.Material);
        Equal(true, policy.TryPlan(a.Item1.Network, order, out _), "variable ingredient allowed by both filters");

        var worker = new AdjustedIngredientWorker(); order.Recipe.Worker = worker;
        Equal(false, policy.TryPlan(a.Item1.Network, order, out _), "no-mix custom worker count requires twelve instead of six");
        Equal(true, worker.Calls > 0, "no-mix quantity calls the vanilla worker count hook");
        order.Recipe.allowMixingIngredients = true;
        Equal(true, policy.TryPlan(a.Item1.Network, order, out plan), "vanilla mixed branch uses the base ingredient value");
        Equal(6, plan.Values.Sum(), "mixed quantity follows vanilla's base-count branch");
    }
    private static void VanillaMaterialSelection()
    {
        var stocks = new List<CraftingMaterialStock<string>>
        {
            new CraftingMaterialStock<string> { Item = "nearA", Kind = "A", Count = 1, Value = 1 },
            new CraftingMaterialStock<string> { Item = "nearB", Kind = "B", Count = 10, Value = 1 },
            new CraftingMaterialStock<string> { Item = "farA", Kind = "A", Count = 9, Value = 1 }
        };
        var needs = new List<CraftingMaterialNeed<string>> { new CraftingMaterialNeed<string> { Value = 10, Allows = _ => true } };
        Equal(true, CraftingMaterialPlanner.TryPlan(stocks, needs, false, false, out var plan), "no-mix aggregates all stacks before selecting a def");
        Equal(false, plan.ContainsKey("nearB"), "first encountered sufficient def wins even when its later stack is farther away");
        Equal(9, plan["farA"], "selected def draws from multiple stacks");
        needs.Add(new CraftingMaterialNeed<string> { Value = 100, Allows = _ => false });
        Equal(true, CraftingMaterialPlanner.TryPlan(stocks, needs, false, true, out plan), "whole-stack branch ignores count and succeeds after first usable stack");
        Equal(1, plan.Count, "whole-stack branch chooses only the first usable stack");
        Equal(1, plan["nearA"], "whole-stack branch accepts a stack smaller than the declared need");
        needs.RemoveAt(1); needs[0].Value = 2;
        Equal(true, CraftingMaterialPlanner.TryPlan(stocks, needs, true, true, out plan), "mixed branch ignores whole-stack flag");
        Equal(2, plan.Values.Sum(), "mixed branch takes only the quantity required");

        var f = new Fixture(); var a = f.Core(0); var order = f.Order(a.Item1, 4);
        var otherDef = new ThingDef(); var ingredient = order.Recipe.ingredients[0]; ingredient.filter.Allowed.Add(otherDef);
        order.Recipe.fixedIngredientFilter.Allowed.Add(otherDef); order.Ingredients.Allowed.Add(otherDef);
        order.Recipe.IngredientValueGetter.Values[f.Material] = 2;
        var farUnit = new Building_StorageUnit(); f.Register(farUnit, 3);
        var cheaperFar = new Thing { def = otherDef, stackCount = 10 }; farUnit.Inventory.Contents.TryAdd(cheaperFar);
        f.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
        var policy = new NetworkCraftingMaterialPolicy();
        Equal(true, policy.TryPlan(a.Item1.Network, order, out var planThings), "no-mix plans nearby higher-value material");
        Equal(true, planThings.ContainsKey(a.Item3), "no-mix orders by distance before value");
        Equal(2, planThings[a.Item3], "no-mix rounds the converted quantity once per ingredient def");
        order.Recipe.allowMixingIngredients = true;
        Equal(true, policy.TryPlan(a.Item1.Network, order, out planThings), "mixed branch plans lower-value material");
        Equal(true, planThings.ContainsKey(cheaperFar), "mixed branch orders by value before distance");
        Equal(4, planThings[cheaperFar], "mixed branch uses value-weighted ingredient amounts");
    }
    private static void WorkerFailurePrecedesOccupation()
    {
        var f = new Fixture(); var a = f.Core(0); var order = f.Order(a.Item1);
        var scanner = new WorkGiver_CosmicCrafting { def = f.Giver };
        var driver = f.Start(scanner.JobOnThing(f.Pawn, a.Item2, true));
        var other = new Pawn { Spawned = true, Map = f.Map, Faction = Faction.OfPlayer };
        other.workSettings.Unassigned.Add(f.Type);
        f.Map.reservationManager.Reserved.Add(a.Item2);
        JobFailReason.Clear();
        Equal(null, scanner.JobOnThing(other, a.Item2, true), "unassigned pawn cannot use occupied bench");
        Equal("MS_Craft_WorkDisabled", JobFailReason.Reason, "work not assigned takes precedence over bench occupation");
        f.Map.reservationManager.Reserved.Remove(a.Item2);
        Equal(null, scanner.JobOnThing(other, a.Item2, true), "unassigned pawn cannot take occupied order");
        Equal("MS_Craft_WorkDisabled", JobFailReason.Reason, "work not assigned takes precedence over assigned order");
        other.workSettings.Disabled.Add(f.Type);
        Equal(null, scanner.JobOnThing(other, a.Item2, true), "incapable pawn cannot take occupied order");
        Equal("MS_Craft_Incapable", JobFailReason.Reason, "inherent inability is distinguished from work not assigned");
        other.workSettings.Disabled.Clear(); other.workSettings.Unassigned.Clear();
        Equal(null, scanner.JobOnThing(other, a.Item2, true), "eligible pawn still cannot take occupied order");
        Equal("MS_Craft_Assigned", JobFailReason.Reason, "eligible pawn still receives the occupation reason");
        Equal(f.Pawn.CurJob, scanner.JobOnThing(f.Pawn, a.Item2, true), "actual worker is still recognized as already working");
        Equal(f.Pawn, f.Dispatch.AssignedWorker(order), "menu checks do not disturb the existing worker");
        driver.EndJobWith(JobCondition.InterruptForced);
    }
    private static void OrderedJobPreflight()
    {
        var f = new Fixture(); var a = f.Core(0); var order = f.Order(a.Item1);
        var scanner = new WorkGiver_CosmicCrafting { def = f.Giver };
        var job = scanner.JobOnThing(f.Pawn, a.Item2, true);
        job.playerForced = true;
        // Job.TryMakePreToilReservations uses GetCachedDriver before the job starts.
        // Pawn_JobTracker.StartJob then creates a different driver using MakeDriver.
        var cachedDriver = new JobDriver_CosmicCrafting { pawn = f.Pawn, job = job };
        Equal(true, cachedDriver.TryMakePreToilReservations(true), "vanilla ordered-job preflight succeeds");
        Equal(null, f.Dispatch.AssignedWorker(order), "temporary cached driver must not claim the order");
        Equal(10, a.Item1.Network.AvailableToWithdraw(a.Item3), "temporary cached driver must not reserve network materials");
        f.Pawn.mindState.priorityWork.Set(a.Item2.Position, f.Giver);
        var actualDriver = f.Start(job);
        Equal(f.Pawn, f.Dispatch.AssignedWorker(order), "actual driver claims the ordered job");
        Equal(true, actualDriver.CanContinue(), "ordered work continues after preflight and actual start");
        Equal(6, order.Batch.Ingredients[0].stackCount, "actual driver collects the required materials");
        actualDriver.EndJobWith(JobCondition.InterruptForced);
        Equal(null, f.Dispatch.AssignedWorker(order), "actual driver releases its own assignment");
    }
    private static void BenchSelectionAndPriority()
    {
        var f = new Fixture(); var a = f.Core(0); var b = f.Core(10);
        var first = f.Order(a.Item1); var second = f.Order(a.Item1, 4); f.Order(b.Item1);
        var extraBench = new Building_CosmicWorkbench(); f.Register(extraBench, 3); f.Dispatch.Register(extraBench);
        var scanner = new WorkGiver_CosmicCrafting { def = f.Giver };
        Equal(true, scanner.PotentialWorkThingsGlobal(f.Pawn).Contains(extraBench), "clicked workbench is exposed to vanilla menu");
        Equal(null, scanner.JobOnThing(f.Pawn, a.Item1, true), "core does not offer workbench jobs");
        var preview = scanner.JobOnThing(f.Pawn, extraBench, true);
        Equal(extraBench, preview.targetA, "right click fixes the selected bench even when another is closer");
        Equal(first.Id, preview.count, "right click respects order sequence");
        Equal(null, f.Dispatch.AssignedWorker(first), "menu preview does not assign worker");
        Equal(10, a.Item1.Network.AvailableToWithdraw(a.Item3), "menu preview does not reserve materials");
        first.Suspended = true;
        Equal(second.Id, scanner.JobOnThing(f.Pawn, extraBench, true).count, "suspended earlier order is skipped");
        first.Suspended = false;
        var otherGiver = new WorkGiverDef { workType = new WorkTypeDef() };
        second.Recipe.route = otherGiver;
        var otherScanner = new WorkGiver_CosmicCrafting { def = otherGiver };
        var otherJob = otherScanner.JobOnThing(f.Pawn, extraBench, true);
        Equal(second.Id, otherJob.count, "other category selects its own order");
        var previewDriver = new JobDriver_CosmicCrafting { pawn = f.Pawn, job = preview };
        Equal(false, previewDriver.IsSameJobAs(otherJob), "different categories on same bench are different jobs");
        Equal(true, previewDriver.IsSameJobAs(scanner.JobOnThing(f.Pawn, extraBench, true)), "same bench and order match");
        second.Recipe.route = f.Giver;
        Equal(false, previewDriver.IsSameJobAs(JobFor(second)), "different orders on same bench are different jobs");

        a.Item2.width = 3;
        f.Pawn.mindState.priorityWork.Set(a.Item2.Position + new IntVec3(1, 0), f.Giver);
        Equal(a.Item2, scanner.NonScanJob(f.Pawn).targetA, "priority recognizes any occupied cell of the selected bench");
        var driver = f.Start(scanner.NonScanJob(f.Pawn));
        a.Item2.width = 1;
        Equal(f.Pawn.CurJob, scanner.JobOnThing(f.Pawn, a.Item2, true), "menu recognizes the already running order");
        f.Pawn.mindState.priorityWork.Set(extraBench.Position, f.Giver);
        Equal(extraBench, scanner.NonScanJob(f.Pawn).targetA, "priority overrides automatic keep-current-job on another bench");
        driver.EndJobWith(JobCondition.InterruptForced); f.Pawn.jobs.curDriver = null; f.Pawn.jobs.curJob = null;
        first.Suspended = true; second.Suspended = true;
        JobFailReason.Clear();
        Equal(null, scanner.NonScanJob(f.Pawn), "no selected-network order cannot fall back to another network");
        Equal("MS_Craft_Suspended", JobFailReason.Reason, "unavailable order supplies menu failure reason");
        f.Pawn.mindState.priorityWork.Clear();
        Equal(b.Item1, scanner.NonScanJob(f.Pawn).targetB, "ordinary work still searches other networks");
        first.Suspended = false;
        f.Pawn.mindState.priorityWork.Set(extraBench.Position, f.Giver);
        f.Pawn.Unreachable.Add(extraBench);
        Equal(null, scanner.NonScanJob(f.Pawn), "priority cannot substitute a reachable bench for an unreachable selected one");
        f.Pawn.Unreachable.Clear();
        f.Pawn.workSettings.Disabled.Add(f.Type);
        Equal(null, scanner.JobOnThing(f.Pawn, extraBench, true), "forced work respects work settings");
        Equal("MS_Craft_Incapable", JobFailReason.Reason, "inherent work disability explains unavailable work");
        f.Pawn.workSettings.Disabled.Clear(); first.Worker = new Pawn();
        Equal(null, scanner.JobOnThing(f.Pawn, extraBench, true), "forced work respects named worker");
        first.Worker = null; first.Recipe.minimumSkill = 20;
        Equal(null, scanner.JobOnThing(f.Pawn, extraBench, true), "forced work respects mandatory recipe skill");
        first.Recipe.minimumSkill = 0; first.Batch.Cancel(); first.Batch = null; a.Item3.stackCount = 0;
        Equal(null, scanner.JobOnThing(f.Pawn, extraBench, true), "forced work requires available materials");
        Equal("MS_Craft_MissingMaterials", JobFailReason.Reason, "missing ingredients explain unavailable work");
        Equal(null, new WorkGiver_CosmicCrafting { def = new WorkGiverDef { workType = new WorkTypeDef() } }
            .JobOnThing(f.Pawn, extraBench, true), "missing category offers no executable job");
        Equal("MS_Craft_NoOrdersForWorkType", JobFailReason.Reason, "missing category supplies menu failure reason");

        Job JobFor(CraftingOrder order)
        {
            var job = JobMaker.MakeJob(preview.def, extraBench, a.Item1);
            job.count = order.Id; job.workGiverDef = f.Giver;
            return job;
        }
    }
    private static void CoreContainerSelection()
    {
        var core = new Building_StorageCore();
        // ContainingSelectionUtility.SelectableContainedThings directly casts and
        // enumerates this result. Exercise that boundary without a null guard.
        var direct = core.GetDirectlyHeldThings();
        Equal(0, new List<Thing>((IEnumerable<Thing>)direct).Count, "new core direct container can be enumerated by vanilla selection");
        Equal(direct, core.GetDirectlyHeldThings(), "selection gets a stable container");
        Equal(core, direct.Owner, "direct container belongs to the selected core");
        Equal(false, ReferenceEquals(direct, new Building_StorageCore().GetDirectlyHeldThings()), "cores do not share their container");

        var f = new Fixture(); var a = f.Core(0); var order = f.Order(a.Item1);
        f.Start(f.Dispatch.FindJob(f.Pawn, f.Giver));
        var children = new List<IThingHolder>(); a.Item1.GetChildHolders(children);
        Equal(1, children.Count, "core still exposes its crafting batch as a child holder");
        Equal(order.Batch, children[0], "holder traversal reaches the actual batch");
        Equal(6, children[0].GetDirectlyHeldThings().Sum(t => t.stackCount), "materials stay in the batch");
        Equal(0, new List<Thing>((IEnumerable<Thing>)a.Item1.GetDirectlyHeldThings()).Count, "selection during crafting safely enumerates empty direct container");
        Equal(0, new List<Thing>((IEnumerable<Thing>)f.Map.GetComponent<MapComponent_StorageNetworks>().GetDirectlyHeldThings()).Count,
            "network root also returns an enumerable direct container");
    }
    private static void ReservationInvalidation()
    {
        var f = new Fixture(); var a = f.Core(0); var order = f.Order(a.Item1);
        var job = f.Dispatch.FindJob(f.Pawn, f.Giver);
        var pending = new JobDriver_CosmicCrafting { pawn = f.Pawn, job = job };
        f.Pawn.jobs.curDriver = pending; f.Pawn.jobs.curJob = job;
        Equal(true, pending.TryMakePreToilReservations(false), "walking worker reserves materials");
        pending.SetupToils();
        a.Item3.holdingOwner.Remove(a.Item3);
        Equal(false, pending.ended, "inventory callback cannot interrupt jobs reentrantly");
        f.Map.GetComponent<MapComponent_StorageNetworks>().MapComponentTick();
        Equal(true, pending.ended, "missing material ends walking worker through reservation callback");
        Equal(null, f.Dispatch.AssignedWorker(order), "missing material releases order assignment");
        Equal(null, order.Batch, "failed pre-work reservation creates no batch");

        f = new Fixture(); a = f.Core(0); order = f.Order(a.Item1);
        var driver = f.Start(f.Dispatch.FindJob(f.Pawn, f.Giver));
        driver.TestToils.Last().tickIntervalAction(1);
        float progress = order.Progress;
        f.Map.GetComponent<MapComponent_StorageNetworks>().MarkDirty();
        Equal(true, driver.CanContinue(), "pawn tick before map tick resolves topology without interrupting valid work");
        var unit = (Building_StorageUnit)a.Item3.holdingOwner.Owner;
        // A parallel node preserves the core-to-bench connection after the source is removed.
        f.Register(new Thing(), 1);
        var manager = f.Map.GetComponent<MapComponent_StorageNetworks>();
        manager.Unregister(unit.node); manager.MapComponentTick();
        Equal(false, driver.ended, "batch no longer depends on original unit");
        Equal(progress, order.Progress, "unit loss retains work already done");
        Equal(6, order.Batch.Ingredients[0].stackCount, "batch still owns collected ingredients");
        Equal(true, driver.CanContinue(), "work continues while bench and core remain connected");
    }
    private static void Modes()
    {
        bool paused = false;
        Equal(true, CraftingRunPolicy.ShouldRun(CraftingRepeatMode.TargetCount, false, 1, 10, true, 5, 9, ref paused), "below target");
        Equal(false, CraftingRunPolicy.ShouldRun(CraftingRepeatMode.TargetCount, false, 1, 10, true, 5, 10, ref paused), "target satisfied");
        Equal(true, paused, "hysteresis entered");
        Equal(false, CraftingRunPolicy.ShouldRun(CraftingRepeatMode.TargetCount, false, 1, 10, true, 5, 8, ref paused), "wait for low water mark");
        Equal(true, CraftingRunPolicy.ShouldRun(CraftingRepeatMode.TargetCount, false, 1, 10, true, 5, 5, ref paused), "resume at threshold");
        Equal(false, paused, "hysteresis released");
        Equal(false, CraftingRunPolicy.ShouldRun(CraftingRepeatMode.RepeatCount, false, 0, 10, false, 5, 0, ref paused), "zero repeats");
        Equal(true, CraftingRunPolicy.ShouldRun(CraftingRepeatMode.Forever, false, 0, 10, false, 5, long.MaxValue, ref paused), "forever ignores counts");
        Equal(false, CraftingRunPolicy.ShouldRun(CraftingRepeatMode.Forever, true, 1, 10, false, 5, 0, ref paused), "suspension wins");
    }
    private static void Ingredients()
    {
        var stocks = new List<CraftingMaterialStock<string>>
        {
            new CraftingMaterialStock<string> { Item="wood1", Kind="wood", Count=3, Value=1 },
            new CraftingMaterialStock<string> { Item="wood2", Kind="wood", Count=4, Value=1 },
            new CraftingMaterialStock<string> { Item="steel", Kind="steel", Count=4, Value=2 }
        };
        var needs = new List<CraftingMaterialNeed<string>> { new CraftingMaterialNeed<string> { Value=7, Allows=_=>true } };
        Equal(true, CraftingMaterialPlanner.TryPlan(stocks, needs, false, false, out var plan), "one kind across stacks");
        Equal(3, plan["wood1"], "first partial stack"); Equal(4, plan["wood2"], "second partial stack");
        needs[0].Value=10;
        Equal(false, CraftingMaterialPlanner.TryPlan(stocks, needs, false, false, out _), "cannot mix material kinds");
        Equal(true, CraftingMaterialPlanner.TryPlan(stocks, needs, true, false, out plan), "mixed material values");
        Equal(2, plan["steel"], "round up mixed value");
        needs = new List<CraftingMaterialNeed<string>> {
            new CraftingMaterialNeed<string> { Value=7, Allows=s=>s.StartsWith("wood") },
            new CraftingMaterialNeed<string> { Value=1, Allows=s=>s.StartsWith("wood") } };
        Equal(false, CraftingMaterialPlanner.TryPlan(stocks, needs, true, false, out _), "overlapping filters cannot double-spend");
        needs.RemoveAt(1); needs[0].Value=1;
        Equal(true, CraftingMaterialPlanner.TryPlan(stocks, needs, false, true, out plan), "whole-stack recipe");
        Equal(3, plan["wood1"], "whole-stack quantity");
        stocks[0].Value=0; stocks[1].Count=0;
        Equal(false, CraftingMaterialPlanner.TryPlan(stocks, needs, true, false, out _), "zero-value and unavailable ingredients");
    }
    private static void WorkerRules()
    {
        var type = new WorkTypeDef();
        var recipe = new RecipeDef { route = new WorkGiverDef { workType=type }, workSkill=new SkillDef(), minimumSkill=5 };
        var order = new CraftingOrder(1,recipe) { SkillRange = new IntRange(12,20) };
        var pawn = new Pawn { Spawned=true };
        var policy = new VanillaCraftingWorkerPolicy();
        Equal(false,policy.Allows(order,pawn,type),"user minimum skill");
        order.Worker=pawn;
        Equal(true,policy.Allows(order,pawn,type),"specific pawn bypasses user range");
        pawn.skills.record.Level=4;
        Equal(false,policy.Allows(order,pawn,type),"specific pawn still needs hard recipe skill");
        pawn.skills.record.Level=15; order.Worker=null; order.WorkerKind=CraftingWorkerKind.Slaves;
        Equal(false,policy.Allows(order,pawn,type),"slaves-only filter"); pawn.IsSlave=true;
        Equal(true,policy.Allows(order,pawn,type),"slave allowed");
        order.WorkerKind=CraftingWorkerKind.Mechs;
        Equal(false,policy.Allows(order,pawn,type),"mech filter");
        pawn.IsColonyMechPlayerControlled=true; pawn.IsColonyMech=true; pawn.RaceProps.IsMechanoid=true;
        pawn.skills=null; pawn.RaceProps.mechFixedSkillLevel=15;
        Equal(true,policy.Allows(order,pawn,type),"mech fixed skill");
        order.WorkerKind=CraftingWorkerKind.NonMechs;
        Equal(false,policy.Allows(order,pawn,type),"non-mech filter");
        order.WorkerKind=CraftingWorkerKind.Anyone; recipe.route.workTags=WorkTags.Intellectual; pawn.DisabledTags=WorkTags.Intellectual;
        Equal(false,policy.Allows(order,pawn,type),"source workgiver tags preserved");
        pawn.DisabledTags=WorkTags.None; recipe.route.canBeDoneByMechs=false;
        Equal(false,policy.Allows(order,pawn,type),"source workgiver mech restriction");
        recipe.route.canBeDoneByMechs=true; recipe.mechanitorOnlyRecipe=true;
        Equal(false,policy.Allows(order,pawn,type),"mechanitor recipe restriction");
        pawn.mechanitor=true;
        Equal(true,policy.Allows(order,pawn,type),"mechanitor permitted");
        pawn.workSettings.Disabled.Add(type);
        Equal(false,policy.Allows(order,pawn,type),"disabled work cannot be assigned");
    }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly WorkTypeDef Type = new WorkTypeDef();
        internal readonly ThingDef Material = new ThingDef();
        internal readonly Pawn Pawn;
        internal readonly WorkGiverDef Giver;
        internal MapComponent_CosmicCrafting Dispatch => Map.GetComponent<MapComponent_CosmicCrafting>();
        internal Fixture()
        {
            Map.component = new MapComponent_StorageNetworks(Map);
            Pawn = new Pawn { Spawned=true, Map=Map, Faction=Faction.OfPlayer };
            Giver = new WorkGiverDef { workType=Type };
            Map.mapPawns.AllPawnsSpawned.Add(Pawn);
            CraftingServices.Products = new TestProductCounter();
            CraftingServices.Production = new TestProductionPolicy();
        }
        internal Tuple<Building_StorageCore, Building_CosmicWorkbench, Thing> Core(int x)
        {
            var core = new Building_StorageCore(); Register(core,x);
            var unit = new Building_StorageUnit(); Register(unit,x+1);
            var bench = new Building_CosmicWorkbench(); Register(bench,x+2);
            Dispatch.Register(core); Dispatch.Register(bench);
            var material = new Thing { def=Material, stackCount=10 };
            unit.Inventory.Contents.TryAdd(material);
            Map.GetComponent<MapComponent_StorageNetworks>().MapComponentTick();
            return Tuple.Create(core,bench,material);
        }
        internal void Register(Thing thing,int x)
        {
            thing.Spawned=true; thing.Map=Map; thing.Position=new IntVec3(x,0); thing.Faction=Faction.OfPlayer;
            var node=new CompStorageNode { parent=thing };
            if(thing is Building_StorageCore core) core.node=node;
            if(thing is Building_StorageUnit unit) unit.node=node;
            if(thing is Building_CosmicWorkbench bench) bench.node=node;
            Map.GetComponent<MapComponent_StorageNetworks>().Register(node);
        }
        internal CraftingOrder Order(Building_StorageCore core,int amount=6)
        {
            var recipe = new RecipeDef { route=Giver };
            recipe.fixedIngredientFilter.Allowed.Add(Material);
            var ingredient=new IngredientCount { count=amount }; ingredient.filter.Allowed.Add(Material); recipe.ingredients.Add(ingredient);
            var order=core.Crafting.Add(recipe); order.Mode=CraftingRepeatMode.Forever;
            return order;
        }
        internal JobDriver_CosmicCrafting Start(Job job)
        {
            var driver=new JobDriver_CosmicCrafting { pawn=Pawn, job=job };
            Pawn.jobs.curJob=job; Pawn.jobs.curDriver=driver;
            Equal(true,driver.TryMakePreToilReservations(false),"job reserves successfully");
            driver.SetupToils(); driver.RunTestToils();
            return driver;
        }
    }
    private static void AssignmentLifecycle()
    {
        var f=new Fixture(); var a=f.Core(0); var b=f.Core(10);
        var oa=f.Order(a.Item1); var ob=f.Order(b.Item1);
        var first=f.Dispatch.FindJob(f.Pawn,f.Giver);
        Equal(a.Item1, first.targetB,"first core selected");
        var driver=f.Start(first);
        Equal(4,a.Item1.Network.AvailableToWithdraw(a.Item3),"assigned materials withheld from withdrawals");
        Equal(f.Pawn,f.Dispatch.AssignedWorker(oa),"order has one worker");
        Equal(6,oa.Batch.Ingredients[0].stackCount,"materials preserved inside unfinished batch");
        Equal(first,f.Dispatch.FindJob(f.Pawn,f.Giver),"override keeps current valid job");
        var other=new Pawn { Map=f.Map,Spawned=true,Faction=Faction.OfPlayer };
        var next=f.Dispatch.FindJob(other,f.Giver);
        Equal(b.Item1,next.targetB,"round robin proceeds to next core");
        Equal(null,f.Dispatch.FindJob(other,new WorkGiverDef { workType=new WorkTypeDef() }),"wrong work type gets no job");
        var competing=new JobDriver_CosmicCrafting { pawn=other,job=JobMaker.MakeJob(first.def,a.Item2,a.Item1) };
        competing.job.count=oa.Id; competing.job.workGiverDef=f.Giver;
        Equal(false,competing.TryMakePreToilReservations(false),"second pawn cannot claim same order");
        var oc=f.Order(a.Item1);
        Equal(false,CraftingServices.Materials.TryPlan(a.Item1.Network,oc,out _),"second order cannot reuse claimed materials");
        // Override invocation is real production code; the engine's resulting need job is a controlled boundary.
        Find.TickManager.TicksGame=driver.WorkStartedAt+100;
        driver.TestToils.Last().tickIntervalAction(1);
        Equal(0,f.Pawn.jobs.OverrideChecks,"no premature long-work check");
        f.Pawn.jobs.OnOverride=()=>driver.EndJobWith(JobCondition.InterruptForced);
        Find.TickManager.TicksGame=4000;
        driver.TestToils.Last().tickIntervalAction(1);
        Equal(1,f.Pawn.jobs.OverrideChecks,"long-running work consults vanilla job override");
        Equal(4,a.Item1.Network.AvailableToWithdraw(a.Item3),"interruption keeps unfinished batch materials isolated");
        Equal(null,f.Dispatch.AssignedWorker(oa),"interruption releases order");
        Equal(1,oa.RepeatCount,"test work never decrements repeats");
        Equal(2,f.Pawn.ComfortTicks,"working grants chair comfort");
        f.Pawn.jobs.curDriver=null; f.Pawn.jobs.curJob=null;
        // Round-robin cursor wraps after the last core queried.
        var restarted=f.Dispatch.FindJob(f.Pawn,f.Giver);
        Equal(a.Item1,restarted.targetB,"round robin wraps");
        driver=f.Start(restarted); oa.Suspended=true; oa.Changed();
        Equal(false,driver.CanContinue(),"suspended order ends current work"); driver.EndJobWith(JobCondition.Incompletable);
        oa.Suspended=false; f.Pawn.jobs.curDriver=null; f.Pawn.jobs.curJob=null;
        driver=f.Start(f.Dispatch.FindJob(f.Pawn,f.Giver));
        var activeCore=driver.Core;
        var activeOrder=driver.Order;
        activeCore.Crafting.Remove(activeOrder);
        Equal(false,driver.CanContinue(),"deleted order ends current work"); driver.EndJobWith(JobCondition.Incompletable);

        oa.Batch.Cancel(); oa.Batch=null;
        var lease=new CraftingMaterialLease(a.Item1.Network);
        var foreign=new Thing { def=f.Material,stackCount=10 };
        Equal(false,lease.Acquire(new Dictionary<Thing,int>{{a.Item3,6},{foreign,1}}),"partial reservation rolls back on foreign stack");
        Equal(10,a.Item1.Network.AvailableToWithdraw(a.Item3),"rollback restores first stack");
        Equal(true,lease.Acquire(new Dictionary<Thing,int>{{a.Item3,6}}),"lease can be reacquired");
        f.Map.GetComponent<MapComponent_StorageNetworks>().MarkDirty();
        Equal(false,lease.Valid(oa),"network invalidation invalidates lease"); lease.Release();
        f.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
        Equal(10,a.Item1.Network.AvailableToWithdraw(a.Item3),"rebuild has no stale claim");

        oa.Mode=CraftingRepeatMode.TargetCount; oa.TargetCount=10;
        ((TestProductCounter)CraftingServices.Products).Amount=10;
        Equal(false,f.Dispatch.CanRun(a.Item1,oa),"count policy stops at target");
        ((TestProductCounter)CraftingServices.Products).Amount=9;
        Equal(true,f.Dispatch.CanRun(a.Item1,oa),"count policy resumes below target");
        var extra=new Building_StorageCore(); f.Register(extra,3);
        f.Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
        Equal(false,f.Dispatch.CanRun(a.Item1,oa),"multiple cores disable crafting");
    }

    private sealed class HaulSource : IHaulSource
    { internal ThingOwner Items = new ThingOwner(null); public ThingOwner GetDirectlyHeldThings() => Items; }
    private static void ProductCounts()
    {
        var f=new Fixture(); var core=f.Core(0).Item1; var another=f.Core(10).Item1;
        var recipe=new RecipeDef { route=f.Giver };
        var product=new ThingDef { IsApparel=true, apparel=new ApparelProperties(), MadeFromStuff=true, hasQualityComp=true };
        recipe.products.Add(new ThingDefCountClass { thingDef=product });
        var order=core.Crafting.Add(recipe);
        var counter=new MapAndNetworkCraftingProductCounter();
        void Store(Building_StorageCore dest,int n)
        { var source=new ThingOwner(null); var thing=new Thing { def=product,stackCount=n }; source.TryAdd(thing); dest.Network.TryStore(source,thing,n); }
        Store(core,7); Store(another,30);
        var loose=new Thing { def=product,stackCount=4 }; f.Map.listerThings.Things.Add(loose);
        var carried=new Thing { def=product,stackCount=3 }; f.Pawn.carryTracker.innerContainer.TryAdd(carried);
        var equipment=new Thing { def=product,stackCount=2 }; f.Pawn.equipment.AllEquipmentListForReading.Add(equipment);
        var inventory=new Thing { def=product,stackCount=5 }; f.Pawn.inventory.innerContainer.TryAdd(inventory);
        var minified=new MinifiedThing { InnerThing=new Thing { def=product,stackCount=1 },stackCount=2 };
        f.Map.listerThings.Things.Add(minified);
        var holder=new HaulSource(); holder.Items.TryAdd(new Thing { def=product,stackCount=6 });
        f.Map.haulDestinationManager.AllHaulSourcesListForReading.Add(holder);
        Equal(22L,counter.Count(core,order),"map + carry + minified + haul source + current network only");
        f.Map.listerThings.Things.Add(loose);
        Equal(22L,counter.Count(core,order),"each actual object counted once");
        order.IncludeEquipped=true;
        Equal(29L,counter.Count(core,order),"optional equipment and inventory");
        loose.HitPoints=40; order.HitPoints=new FloatRange(0.5f,1f);
        Equal(25L,counter.Count(core,order),"condition filter uses stack quantities");
        inventory.hasQuality=true; inventory.quality=QualityCategory.Poor;
        order.Quality=new QualityRange { min=QualityCategory.Normal,max=QualityCategory.Legendary };
        Equal(20L,counter.Count(core,order),"quality filter");
        var tainted=new Apparel { def=product,stackCount=1,WornByCorpse=true }; f.Map.listerThings.Things.Add(tainted);
        Equal(21L,counter.Count(core,order),"tainted apparel included by default");
        order.IncludeTainted=false;
        Equal(20L,counter.Count(core,order),"tainted apparel excluded");
        order.LimitToAllowedStuff=true; order.Ingredients.Allowed.Add(f.Material); equipment.Stuff=f.Material;
        Equal(2L,counter.Count(core,order),"allowed material filter");
        recipe.specialProducts=new object();
        Equal(false,counter.CanCount(recipe),"special-output recipe has no generic target counter");
        recipe.specialProducts=null; recipe.products.Add(new ThingDefCountClass { thingDef=f.Material });
        Equal(false,counter.CanCount(recipe),"multiple outputs need a dedicated counter");
    }

    private static void AdditionalProductCounts()
    {
        var f = new Fixture(); var core = f.Core(0).Item1; var other = f.Core(10).Item1;
        var simple = new ThingDef { useHitPoints = false };
        var lavish = new ThingDef { useHitPoints = false };
        var packaged = new ThingDef { useHitPoints = false };
        var recipe = new RecipeDef { route = f.Giver };
        recipe.products.Add(new ThingDefCountClass { thingDef = simple });
        var order = core.Crafting.Add(recipe); order.Mode = CraftingRepeatMode.TargetCount; order.TargetCount = 10;
        var counter = new MapAndNetworkCraftingProductCounter(); CraftingServices.Products = counter;
        void Store(Building_StorageCore target, ThingDef def, int amount)
        {
            var source = new ThingOwner(null); var item = new Thing { def = def, stackCount = amount };
            source.TryAdd(item); Equal(amount, target.Network.TryStore(source, item, amount).Transferred, "additional-count fixture stores actual stock");
        }
        f.Map.listerThings.Things.Add(new Thing { def = simple, stackCount = 3 });
        var extra = new Thing { def = lavish, stackCount = 4 }; f.Map.listerThings.Things.Add(extra);
        Store(core, packaged, 3); Store(other, packaged, 20);
        f.Map.listerThings.Things.Add(new Thing { def = f.Material, stackCount = 999 });
        Equal(0, order.AdditionalCounts.AllowedThingDefs.Count(), "additional selections default empty");
        Equal(3L, counter.Count(core, order), "unselected substitutes do not count");
        order.AdditionalCounts.SetAllow(lavish, true);
        Equal(7L, counter.Count(core, order), "selected map substitute adds its full stack count");
        Equal(true, f.Dispatch.CanRun(core, order), "production continues below combined target");
        order.AdditionalCounts.SetAllow(packaged, true);
        Equal(10L, counter.Count(core, order), "map meals plus packaged meals in current network reach target");
        Equal(false, f.Dispatch.CanRun(core, order), "combined count prevents another production iteration");
        order.AdditionalCounts.SetAllow(simple, true);
        Equal(10L, counter.Count(core, order), "selecting primary product does not count it twice");
        f.Map.listerThings.Things.Add(extra);
        var holder = new HaulSource(); holder.Items.TryAdd(extra);
        f.Map.haulDestinationManager.AllHaulSourcesListForReading.Add(holder);
        Equal(10L, counter.Count(core, order), "substitute shared by map and haul-source enumeration counted once");
        f.Pawn.carryTracker.innerContainer.TryAdd(new Thing { def = lavish, stackCount = 2 });
        Equal(12L, counter.Count(core, order), "carried substitute uses same counting path");
        f.Pawn.inventory.innerContainer.TryAdd(new Thing { def = packaged, stackCount = 50 });
        Equal(12L, counter.Count(core, order), "food inventory exclusions remain unchanged");
        var pending = f.Map.GetComponent<MapComponent_StorageNetworks>().CreateRecovery(core.Position);
        pending.Contents.TryAdd(new Thing { def = packaged, stackCount = 2 });
        Equal(14L, counter.Count(core, order), "selected goods awaiting placement also count");
        f.Map.listerThings.Things.Add(new MinifiedThing { InnerThing = new Thing { def = lavish, stackCount = 1 }, stackCount = 2 });
        Equal(16L, counter.Count(core, order), "additional minified items count their contained quantity");
        f.Map.listerThings.Things.Add(new Thing { def = packaged, stackCount = 100, Destroyed = true });
        Equal(16L, counter.Count(core, order), "destroyed substitutes excluded");
        Equal(0, core.Crafting.Add(recipe).AdditionalCounts.AllowedThingDefs.Count(), "each order owns independent selections");
        order.PauseWhenSatisfied = true; order.UnpauseAt = 5;
        Equal(false, f.Dispatch.CanRun(core, order), "combined target activates pause-until-low mode");
        Equal(true, order.Paused, "pause state records satisfaction");
        order.AdditionalCounts.SetDisallowAll();
        Equal(3L, counter.Count(core, order), "clearing substitutes keeps primary product count");
        Equal(true, f.Dispatch.CanRun(core, order), "count dropping below resume threshold restarts production");
        Equal(false, order.Paused, "resume resets pause state");
        order.Mode = CraftingRepeatMode.RepeatCount; order.RepeatCount = 1;
        order.AdditionalCounts.SetAllow(packaged, true);
        Equal(true, f.Dispatch.CanRun(core, order), "additional counts do not affect repeat-count mode");

        // Selecting the primary type again must not bypass its existing quality conditions.
        simple.hasQualityComp = true;
        var primary = f.Map.listerThings.Things.First(t => t.def == simple);
        primary.hasQuality = true; primary.quality = QualityCategory.Poor;
        order.Quality = new QualityRange { min = QualityCategory.Normal, max = QualityCategory.Legendary };
        order.AdditionalCounts.SetDisallowAll(); order.AdditionalCounts.SetAllow(simple, true);
        Equal(0L, counter.Count(core, order), "primary product's count conditions cannot be bypassed by selecting it again");
        order.AdditionalCounts.SetAllow(packaged, true);
        Equal(5L, counter.Count(core, order), "substitutes do not inherit primary product quality restrictions");
    }

    private static void MissingAdditionalCounts()
    {
        var f = new Fixture(); var core = f.Core(0).Item1;
        var product = new ThingDef { useHitPoints = false };
        var recipe = new RecipeDef { route = f.Giver };
        recipe.products.Add(new ThingDefCountClass { thingDef = product });
        var order = core.Crafting.Add(recipe);
        order.Mode = CraftingRepeatMode.TargetCount; order.TargetCount = 5;
        order.AdditionalCounts = null;
        var counter = new MapAndNetworkCraftingProductCounter(); CraftingServices.Products = counter;
        f.Map.listerThings.Things.Add(new Thing { def = product, stackCount = 3 });
        // Counting also traverses unrelated held stock; a missing filter must reject it.
        f.Pawn.carryTracker.innerContainer.TryAdd(new Thing { def = f.Material, stackCount = 99 });
        Equal(3L, counter.Count(core, order), "missing additional filter still counts primary products and excludes unrelated held items");
        Equal(true, f.Dispatch.CanRun(core, order), "missing additional filter allows production below target");
        order.TargetCount = 3;
        Equal(false, f.Dispatch.CanRun(core, order), "missing additional filter stops production at the primary target");
        Equal("MS_Craft_Satisfied", f.Dispatch.Status(core, order).ToString(), "list status remains available with a missing additional filter");

        Scribe.mode = LoadSaveMode.PostLoadInit;
        try { order.ExposeData(); }
        finally { Scribe.mode = LoadSaveMode.Inactive; }
        Equal(true, order.AdditionalCounts != null, "post-load initializes a missing additional filter");
        Equal(0, order.AdditionalCounts.AllowedThingDefs.Count(), "recovered additional filter starts with no selections");
        Equal(3L, counter.Count(core, order), "post-load initialization preserves primary product counts");
        var existing = order.AdditionalCounts;
        existing.SetAllow(f.Material, true);
        Scribe.mode = LoadSaveMode.PostLoadInit;
        try { order.ExposeData(); }
        finally { Scribe.mode = LoadSaveMode.Inactive; }
        Equal(existing, order.AdditionalCounts, "post-load preserves an existing additional filter");
        Equal(112L, counter.Count(core, order), "existing selections count carried stock and the fixture's 10 stored materials after post-load");
    }

    private static void ProductFilterApplicability()
    {
        var f=new Fixture(); var core=f.Core(0).Item1;
        var product=new ThingDef { useHitPoints=false }; // Native stone block capabilities.
        var recipe=new RecipeDef(); recipe.products.Add(new ThingDefCountClass { thingDef=product });
        var order=core.Crafting.Add(recipe); var counter=new MapAndNetworkCraftingProductCounter();
        var filters=new CraftingProductFilters(recipe);
        Equal(false,filters.IncludeEquipped,"stone blocks have no equipment control");
        Equal(false,filters.IncludeTainted,"stone blocks have no tainted control");
        Equal(false,filters.HitPoints,"stone blocks have no condition control");
        Equal(false,filters.Quality,"stone blocks have no quality control");
        Equal(false,filters.AllowedStuff,"being a material does not mean made from selectable stuff");
        var loose=new Thing { def=product,stackCount=7,HitPoints=20 };
        f.Map.listerThings.Things.Add(loose);
        var source=new ThingOwner(null); var stored=new Thing { def=product,stackCount=5,HitPoints=20 };
        source.TryAdd(stored); core.Network.TryStore(source,stored,5);
        f.Pawn.inventory.innerContainer.TryAdd(new Thing { def=product,stackCount=3 });
        order.IncludeEquipped=true; order.IncludeTainted=false; order.LimitToAllowedStuff=true;
        order.HitPoints=new FloatRange(0.8f,1f);
        order.Quality=new QualityRange { min=QualityCategory.Legendary,max=QualityCategory.Legendary };
        Equal(12L,counter.Count(core,order),"hidden settings cannot exclude map or network stone blocks or enable inventory counting");
        bool paused=false;
        Equal(false,CraftingRunPolicy.ShouldRun(CraftingRepeatMode.TargetCount,false,1,12,false,5,counter.Count(core,order),ref paused),
            "target reached even when obsolete material restriction remains set");

        product.useHitPoints=true; product.IsWeapon=true; product.hasQualityComp=true;
        filters=new CraftingProductFilters(recipe);
        Equal(true,filters.IncludeEquipped,"weapons support equipment counting");
        Equal(false,filters.IncludeTainted,"weapons do not support tainted clothing");
        Equal(true,filters.HitPoints,"durable weapons support condition");
        Equal(true,filters.Quality,"quality component enables quality control");
        Equal(false,filters.AllowedStuff,"fixed-material weapons have no allowed-stuff control");
        order.Quality=QualityRange.All;
        Equal(3L,counter.Count(core,order),"visible condition filter applies while equipped inventory is enabled");
        order.HitPoints=new FloatRange(0f,1f); order.IncludeEquipped=false;
        loose.hasQuality=true; loose.quality=QualityCategory.Poor;
        stored.hasQuality=true; stored.quality=QualityCategory.Good;
        order.Quality=new QualityRange { min=QualityCategory.Normal,max=QualityCategory.Legendary };
        Equal(5L,counter.Count(core,order),"visible quality filter consistently filters map and network");

        product.IsWeapon=false; product.IsApparel=true; product.apparel=new ApparelProperties { careIfWornByCorpse=false };
        filters=new CraftingProductFilters(recipe);
        Equal(true,filters.IncludeEquipped,"apparel supports equipped counting");
        Equal(false,filters.IncludeTainted,"apparel ignoring corpse taint has no tainted control");
        var tainted=new Apparel { def=product,stackCount=1,WornByCorpse=true };
        f.Map.listerThings.Things.Add(tainted);
        Equal(6L,counter.Count(core,order),"hidden tainted setting cannot exclude clothing which ignores corpse taint");
        product.apparel.careIfWornByCorpse=true;
        Equal(true,new CraftingProductFilters(recipe).IncludeTainted,"corpse-sensitive apparel enables tainted control");
        Equal(5L,counter.Count(core,order),"visible tainted filter excludes worn-by-corpse apparel");

        product.MadeFromStuff=true; stored.Stuff=f.Material; order.Ingredients.Allowed.Add(f.Material);
        Equal(true,new CraftingProductFilters(recipe).AllowedStuff,"selectable material enables allowed-stuff filter");
        Equal(5L,counter.Count(core,order),"visible allowed-stuff filter counts matching material");
        order.Ingredients.Allowed.Clear();
        Equal(0L,counter.Count(core,order),"visible allowed-stuff filter excludes disallowed material");
        Equal(false,new CraftingProductFilters(new RecipeDef()).Quality,"no product has no product controls");
    }

    private static void LoadAndBuildings()
    {
        var f=new Fixture(); var a=f.Core(0); var order=f.Order(a.Item1);
        var job=f.Dispatch.FindJob(f.Pawn,f.Giver); var original=f.Start(job);
        original.ReleaseAssignment();
        var loaded=new JobDriver_CosmicCrafting { pawn=f.Pawn,job=job };
        f.Pawn.jobs.curDriver=loaded; loaded.SetupToils();
        f.Dispatch.FinalizeInit();
        Equal(f.Pawn,f.Dispatch.AssignedWorker(order),"loaded job restores order claim before new assignment");
        Equal(4,a.Item1.Network.AvailableToWithdraw(a.Item3),"loaded job restores material claim");
        Equal(true,loaded.CanContinue(),"loaded job remains valid");
        f.Map.GetComponent<MapComponent_StorageNetworks>().FinalizeInit();
        Equal(true,loaded.CanContinue(),"network finalization order does not cancel a loaded job");
        Equal(4,a.Item1.Network.AvailableToWithdraw(a.Item3),"replacement network restores exact claim");
        // Whole-stack recipes cannot silently take just the unreserved part of a real stack.
        var whole=f.Order(a.Item1,1); whole.Recipe.ignoreIngredientCountTakeEntireStacks=true;
        Equal(true,CraftingServices.Materials.TryPlan(a.Item1.Network,whole,out _),"whole stack can use separate unclaimed remainder");
        f.Dispatch.Unregister(a.Item2);
        Equal(false,loaded.ended,"bench removal defers job callbacks until lifecycle completes");
        f.Map.GetComponent<MapComponent_StorageNetworks>().MapComponentTick();
        Equal(true,loaded.ended,"removing bench ends assigned job");
        Equal(4,a.Item1.Network.AvailableToWithdraw(a.Item3),"bench removal preserves unfinished ingredients");
        Equal(null,f.Dispatch.AssignedWorker(order),"bench removal releases order");
        f.Dispatch.Register(a.Item2); f.Pawn.jobs.curDriver=null; f.Pawn.jobs.curJob=null;
        var driver=f.Start(f.Dispatch.FindJob(f.Pawn,f.Giver));
        f.Dispatch.Unregister(a.Item1);
        f.Map.GetComponent<MapComponent_StorageNetworks>().MapComponentTick();
        Equal(true,driver.ended,"removing core ends assigned job");
        Equal(4,a.Item1.Network.AvailableToWithdraw(a.Item3),"despawn preserves core-owned batch");
        Equal(true,a.Item1.Crafting.CancelAll(true),"core destruction can refund every batch");
        Equal(10,a.Item1.Network.AvailableToWithdraw(a.Item3),"cancel returns unfinished ingredients");
    }

    private static void ConfigurableWorkSpeed()
    {
        var settings = MagicStorageMod.Settings;
        try
        {
            var f = new Fixture(); var a = f.Core(0); var order = f.Order(a.Item1, 3);
            var policy = (TestProductionPolicy)CraftingServices.Production;
            policy.Required = 100; policy.Speed = 2;
            var driver = f.Start(f.Dispatch.FindJob(f.Pawn, f.Giver));
            var batch = order.Batch; var toil = driver.TestToils.Last();
            settings.CosmicWorkbenchWorkSpeedPercent = 50;
            toil.tickIntervalAction(4);
            Equal(0.04f, order.Progress, "minimum speed multiplies existing worker speed by half");
            settings.CosmicWorkbenchWorkSpeedPercent = 1000;
            toil.tickIntervalAction(2);
            Equal(0.44f, order.Progress, "maximum speed applies immediately to unfinished work");
            Equal(batch, order.Batch, "changing speed preserves the current batch");
            settings.RestoreDefaults();
            toil.tickIntervalAction(3);
            Equal(0.5f, order.Progress, "restoring defaults immediately restores normal speed");
            toil.tickIntervalAction(25);
            Equal(JobCondition.Succeeded, driver.EndCondition.Value, "adjusted batch completes normally");
            Equal(7L, a.Item1.Network.GetTotalCount(f.Material), "speed changes do not consume extra materials");
            Equal(4L, a.Item1.Network.GetTotalCount(policy.Product), "speed changes preserve product quantity");
            Equal(1, policy.MakeCalls, "speed changes never duplicate production");
        }
        finally { settings.RestoreDefaults(); }
    }

    private static void ProductionCompletion()
    {
        var f=new Fixture(); var a=f.Core(0); var order=f.Order(a.Item1,3);
        order.Mode=CraftingRepeatMode.RepeatCount; order.RepeatCount=2;
        var policy=(TestProductionPolicy)CraftingServices.Production; policy.Required=10; policy.Speed=2;
        var driver=f.Start(f.Dispatch.FindJob(f.Pawn,f.Giver));
        var batch=order.Batch;
        var toil=driver.TestToils.Last();
        Equal(TargetIndex.None,toil.ProgressTarget,"native progress mote is anchored to pawn");
        Equal(0f,toil.ProgressGetter(),"mote starts from saved order progress");
        toil.tickIntervalAction(2);
        Equal(0.4f,order.Progress,"work speed affects batch progress");
        Equal(order.Progress,toil.ProgressGetter(),"list and mote share one progress source");
        Equal(7L,a.Item1.Network.GetTotalCount(f.Material),"materials removed from available inventory only once");
        Equal(3,batch.Ingredients[0].stackCount,"unfinished materials retain actual objects");
        driver.EndJobWith(JobCondition.InterruptForced);
        Equal(0.4f,order.Progress,"interruption retains work");
        var replacement=new Pawn { Spawned=true,Map=f.Map,Faction=Faction.OfPlayer };
        f.Map.mapPawns.AllPawnsSpawned.Add(replacement);
        var next=f.Dispatch.FindJob(replacement,f.Giver);
        var resumed=new JobDriver_CosmicCrafting { pawn=replacement,job=next };
        replacement.jobs.curJob=next; replacement.jobs.curDriver=resumed;
        Equal(true,resumed.TryMakePreToilReservations(false),"different eligible worker can resume");
        resumed.SetupToils(); resumed.RunTestToils();
        Equal(batch,order.Batch,"resume uses same saved batch");
        Equal(7L,a.Item1.Network.GetTotalCount(f.Material),"resume does not collect materials again");
        Equal(0.4f,resumed.TestToils.Last().ProgressGetter(),"resumed mote shows retained progress");
        resumed.TestToils.Last().tickIntervalAction(3);
        Equal(JobCondition.Succeeded,resumed.EndCondition.Value,"completed job succeeds");
        Equal(null,order.Batch,"completed batch cleared from order");
        Equal(1,order.RepeatCount,"one completed iteration decrements one repeat");
        Equal(4L,a.Item1.Network.GetTotalCount(policy.Product),"product enters current network");
        Equal(1,policy.MakeCalls,"one generation call"); Equal(1,policy.ConsumeCalls,"one ingredient consumption");
        Equal(1,policy.NotifyCalls,"one completion notification");
        Equal(0.2f,f.Pawn.CraftingXp,"first worker receives only own contribution");
        Equal(0.3f,replacement.CraftingXp,"replacement receives only own contribution");
        Equal(true,batch.Complete(order,replacement),"completion retry is idempotent");
        Equal(1,order.RepeatCount,"completion retry cannot decrement twice");
        Equal(4L,a.Item1.Network.GetTotalCount(policy.Product),"completion retry cannot duplicate output");
        Equal(1,policy.MakeCalls,"completion retry cannot regenerate products");
        Equal(0,batch.Ingredients.Count,"consumed inputs leave no dangling holder entries");
    }

    private static void ProductionOverflow()
    {
        var f=new Fixture(); var a=f.Core(0);
        var unit=(Building_StorageUnit)a.Item3.holdingOwner.Owner; unit.SlotCapacity=1;
        var order=f.Order(a.Item1,3); var policy=(TestProductionPolicy)CraftingServices.Production; policy.Required=1;
        var driver=f.Start(f.Dispatch.FindJob(f.Pawn,f.Giver)); driver.TestToils.Last().tickIntervalAction(1);
        Equal(0L,a.Item1.Network.GetTotalCount(policy.Product),"full network cannot accept output");
        Equal(4,f.Map.Ground.Where(t=>t.def==policy.Product).Sum(t=>t.stackCount),"overflow drops whole output near core");
        Equal(a.Item1.Position,f.Map.Ground.Single(t=>t.def==policy.Product).Position,"core is output drop origin");
        Equal(7L,a.Item1.Network.GetTotalCount(f.Material),"overflow does not refund consumed inputs");

        f=new Fixture(); a=f.Core(0); order=f.Order(a.Item1,3);
        policy=(TestProductionPolicy)CraftingServices.Production; policy.Required=1;
        driver=f.Start(f.Dispatch.FindJob(f.Pawn,f.Giver));
        a.Item1.Settings.allow=false; f.Map.DropBudget=0;
        driver.TestToils.Last().tickIntervalAction(1);
        Equal(JobCondition.Succeeded,driver.EndCondition.Value,"blocked placement safely queues already completed output");
        var holders=new List<IThingHolder>(); f.Map.GetComponent<MapComponent_StorageNetworks>().GetChildHolders(holders);
        Equal(4,holders.Sum(h=>h.GetDirectlyHeldThings()?.Count > 0 ? h.GetDirectlyHeldThings()[0].stackCount : 0),"unplaceable output retained in saved recovery holder");
        order.Recipe.products.Add(new ThingDefCountClass { thingDef=policy.Product });
        Equal(4L,new MapAndNetworkCraftingProductCounter().Count(a.Item1,order),"pending drop counts toward production target");
        f.Map.DropBudget=100; Find.TickManager.TicksGame=250;
        f.Map.GetComponent<MapComponent_StorageNetworks>().MapComponentTick();
        Equal(4,f.Map.Ground.Where(t=>t.def==policy.Product).Sum(t=>t.stackCount),"pending output drops when placement becomes possible");

        f=new Fixture(); a=f.Core(0); unit=(Building_StorageUnit)a.Item3.holdingOwner.Owner; unit.SlotCapacity=2;
        order=f.Order(a.Item1,3); policy=(TestProductionPolicy)CraftingServices.Production; policy.Required=1;
        var source=new ThingOwner(null); var existing=new Thing { def=policy.Product,stackCount=73 }; source.TryAdd(existing);
        a.Item1.Network.TryStore(source,existing,73);
        driver=f.Start(f.Dispatch.FindJob(f.Pawn,f.Giver)); driver.TestToils.Last().tickIntervalAction(1);
        Equal(75L,a.Item1.Network.GetTotalCount(policy.Product),"output merges into existing partial stack");
        Equal(2,f.Map.Ground.Where(t=>t.def==policy.Product).Sum(t=>t.stackCount),"only unstoreable remainder drops");
    }

    private static void ProductionFloorDelivery()
    {
        var f=new Fixture(); var a=f.Core(0); var order=f.Order(a.Item1,3);
        var policy=(TestProductionPolicy)CraftingServices.Production; policy.Required=2;
        f.Pawn.Position=new IntVec3(2,1);
        var driver=f.Start(f.Dispatch.FindJob(f.Pawn,f.Giver));
        driver.TestToils.Last().tickIntervalAction(1);
        float progress=order.Progress;
        order.DropProductsOnFloor=true;
        Equal(true,driver.CanContinue(),"changing output destination does not interrupt worker");
        Equal(progress,order.Progress,"changing output destination retains progress");
        var batch=order.Batch;
        driver.TestToils.Last().tickIntervalAction(1);
        Equal(JobCondition.Succeeded,driver.EndCondition.Value,"floor delivery completes iteration");
        Equal(0L,a.Item1.Network.GetTotalCount(policy.Product),"floor delivery bypasses available network capacity");
        Equal(4,f.Map.Ground.Where(t=>t.def==policy.Product).Sum(t=>t.stackCount),"floor delivery releases every product");
        Equal(f.Pawn.Position,f.Map.Ground.Single(t=>t.def==policy.Product).Position,"floor delivery uses worker position at bench, not core");
        Equal(7L,a.Item1.Network.GetTotalCount(f.Material),"floor delivery consumes inputs once");
        Equal(true,batch.Complete(order,f.Pawn),"floor delivery retry is idempotent");
        Equal(1,policy.MakeCalls,"floor delivery cannot regenerate products");
        Equal(4,f.Map.Ground.Where(t=>t.def==policy.Product).Sum(t=>t.stackCount),"floor delivery retry cannot duplicate products");

        f=new Fixture(); a=f.Core(0); order=f.Order(a.Item1,3); order.DropProductsOnFloor=true;
        policy=(TestProductionPolicy)CraftingServices.Production; policy.Required=1;
        f.Pawn.Position=new IntVec3(2,1); var origin=f.Pawn.Position;
        f.Map.DropBudget=0;
        driver=f.Start(f.Dispatch.FindJob(f.Pawn,f.Giver)); driver.TestToils.Last().tickIntervalAction(1);
        Equal(JobCondition.Succeeded,driver.EndCondition.Value,"blocked floor delivery queues completed products");
        Equal(0L,a.Item1.Network.GetTotalCount(policy.Product),"blocked floor delivery still bypasses network");
        order.Recipe.products.Add(new ThingDefCountClass { thingDef=policy.Product });
        Equal(4L,new MapAndNetworkCraftingProductCounter().Count(a.Item1,order),"pending floor products count toward target");
        f.Pawn.Position=new IntVec3(20,20);
        f.Map.DropBudget=100; Find.TickManager.TicksGame=250;
        f.Map.GetComponent<MapComponent_StorageNetworks>().MapComponentTick();
        Equal(4,f.Map.Ground.Where(t=>t.def==policy.Product).Sum(t=>t.stackCount),"blocked floor delivery eventually releases products");
        Equal(origin,f.Map.Ground.Single(t=>t.def==policy.Product).Position,"pending delivery retains workbench origin after worker leaves");
        Equal(1,policy.MakeCalls,"pending floor delivery never regenerates products");

        f=new Fixture(); a=f.Core(0); order=f.Order(a.Item1,3); order.DropProductsOnFloor=true;
        driver=f.Start(f.Dispatch.FindJob(f.Pawn,f.Giver));
        driver.EndJobWith(JobCondition.InterruptForced);
        a.Item1.Crafting.Remove(order);
        Equal(10L,a.Item1.Network.GetTotalCount(f.Material),"floor output option does not change cancellation refunds");
        Equal(0,f.Map.Ground.Count,"cancellation does not drop refundable materials at bench");
    }

    private static void ProductionCancellationAndFailure()
    {
        var f=new Fixture(); var a=f.Core(0); var order=f.Order(a.Item1,3);
        var driver=f.Start(f.Dispatch.FindJob(f.Pawn,f.Giver)); driver.TestToils.Last().tickIntervalAction(50);
        order.Suspended=true; order.Changed();
        Equal(false,driver.CanContinue(),"pause releases worker while retaining progress"); driver.EndJobWith(JobCondition.Incompletable);
        Equal(true,order.Progress>0,"paused progress remains visible");
        a.Item1.Crafting.Remove(order);
        Equal(10L,a.Item1.Network.GetTotalCount(f.Material),"deleting unfinished order refunds ingredients");
        Equal(null,order.Batch,"deleted batch leaves no hidden material holder");

        f=new Fixture(); a=f.Core(0); order=f.Order(a.Item1,3);
        var policy=(TestProductionPolicy)CraftingServices.Production; policy.Required=1; policy.ThrowGeneration=true;
        int expectedErrorIndex=Log.Errors.Count;
        driver=f.Start(f.Dispatch.FindJob(f.Pawn,f.Giver)); driver.TestToils.Last().tickIntervalAction(1);
        Equal(true,order.Batch.Faulted,"partial generation failure quarantines batch");
        Equal(true,order.Suspended,"failed production suspends order");
        Equal(0,policy.ConsumeCalls,"generation failure cannot consume input");
        Equal(0L,a.Item1.Network.GetTotalCount(policy.Product),"partial generation output never published");
        Equal(false,order.Batch.Complete(order,f.Pawn),"failed callbacks not automatically rerun");
        Equal(1,policy.MakeCalls,"failed callbacks run once");
        Equal(expectedErrorIndex+1,Log.Errors.Count,"failed generation reports exactly one error");
        Log.Errors.RemoveAt(expectedErrorIndex);
        a.Item1.Crafting.Remove(order);
        Equal(10L,a.Item1.Network.GetTotalCount(f.Material),"failed generation cancellation refunds inputs");
        Equal(0,f.Map.Ground.Where(t=>t.def==policy.Product).Sum(t=>t.stackCount),"failed generation cancellation cannot duplicate staged output");

        f=new Fixture(); a=f.Core(0); order=f.Order(a.Item1,3);
        driver=f.Start(f.Dispatch.FindJob(f.Pawn,f.Giver));
        Equal(true,a.Item1.Crafting.CancelAll(false),"destroying core can release every unfinished batch");
        Equal(3,f.Map.Ground.Where(t=>t.def==f.Material).Sum(t=>t.stackCount),"core destruction drops unfinished ingredients");
        Equal(7L,a.Item1.Network.GetTotalCount(f.Material),"core destruction does not duplicate remaining unit stock");
    }
}
