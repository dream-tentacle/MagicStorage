using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    public sealed class MapComponent_CosmicCrafting : MapComponent
    {
        private readonly List<Building_StorageCore> cores = new List<Building_StorageCore>();
        private readonly List<Building_CosmicWorkbench> benches = new List<Building_CosmicWorkbench>();
        private readonly Dictionary<WorkTypeDef, int> lastCoreIds = new Dictionary<WorkTypeDef, int>();
        private readonly Dictionary<CraftingOrder, JobDriver_CosmicCrafting> assignments = new Dictionary<CraftingOrder, JobDriver_CosmicCrafting>();
        private StorageRefreshTimer maintenanceTimer;
        public MapComponent_CosmicCrafting(Map map) : base(map) { }
        internal IEnumerable<Thing> Workbenches => benches.Where(b => b.Spawned && b.Map == map);
        internal void Register(Building_StorageCore core) { if (!cores.Contains(core)) { cores.Add(core); cores.Sort((a,b) => a.thingIDNumber.CompareTo(b.thingIDNumber)); } }
        internal void Register(Building_CosmicWorkbench bench) { if (!benches.Contains(bench)) benches.Add(bench); }
        internal void Unregister(Building_StorageCore core) { cores.Remove(core); CancelWhere(d => d.Core == core); }
        internal void Unregister(Building_CosmicWorkbench bench) { benches.Remove(bench); CancelWhere(d => d.Bench == bench); }
        private void CancelWhere(System.Predicate<JobDriver_CosmicCrafting> predicate)
        {
            foreach (var driver in assignments.Values.ToList())
                if (predicate(driver)) map.GetComponent<MapComponent_StorageNetworks>().Defer(() =>
                { if (!driver.ended) driver.EndJobWith(JobCondition.Incompletable); });
        }
        internal Pawn AssignedWorker(CraftingOrder order) => assignments.TryGetValue(order, out var driver) &&
            !driver.ended && driver.pawn?.jobs?.curDriver == driver && driver.pawn.CurJob == driver.job
                ? driver.pawn : null;
        internal bool IsAssigned(CraftingOrder order, JobDriver_CosmicCrafting driver) =>
            assignments.TryGetValue(order, out var owner) && owner == driver;
        internal void Release(CraftingOrder order, JobDriver_CosmicCrafting driver)
        { if (order != null && assignments.TryGetValue(order, out var owner) && owner == driver) assignments.Remove(order); }

        internal bool CanRun(Building_StorageCore core, CraftingOrder order)
        {
            if (!core.CanWork || core.Faction != Faction.OfPlayer || order?.Recipe == null ||
                !order.Recipe.AvailableNow || !CraftingRecipeCatalog.Supports(order.Recipe)) return false;
            if (order.Batch != null && !order.Batch.Valid(order)) return false;
            if (order.Batch?.Committed == true) return !order.Suspended;
            // Preserve ShouldRun's pause-state bookkeeping without evaluating any inventory.
            if (order.Suspended) return order.ShouldRun(0);
            if (order.Mode == CraftingRepeatMode.TargetCount && !CraftingServices.Products.CanCount(order.Recipe)) return false;
            return order.ShouldRun(order.Mode == CraftingRepeatMode.TargetCount
                ? CraftingServices.Products.Count(core, order, order.ProductCountLimit) : 0);
        }
        internal bool CanClaimOrder(Building_StorageCore core, CraftingOrder order, Pawn pawn, WorkGiverDef giver) =>
            core != null && order != null && giver != null && AssignedWorker(order) == null &&
            CraftingServices.Workers.Allows(order, pawn, giver.workType) && CanRun(core, order);

        private bool MaterialsAvailable(Building_StorageCore core, CraftingOrder order, bool immediate = false)
        {
            int tick = Find.TickManager.TicksGame;
            if (!immediate && order.LastMaterialFailureTick is int last && tick >= last &&
                (long)tick - last < MagicStorageMod.Settings.CraftingMaterialRecheckTicks) return false;
            bool available = CraftingServices.Materials.TryPlan(core.Network, order, out _);
            order.LastMaterialFailureTick = available ? (int?)null : tick;
            return available;
        }

        internal bool CanStartOrder(JobDriver_CosmicCrafting driver)
        {
            var core = driver.Core; var order = driver.Order;
            return CanClaimOrder(core, order, driver.pawn, driver.job.workGiverDef) &&
                (order.Batch != null || MaterialsAvailable(core, order, true));
        }

        internal bool TryClaim(JobDriver_CosmicCrafting driver)
        {
            var core = driver.Core; var order = driver.Order;
            if (!CanClaimOrder(core, order, driver.pawn, driver.job.workGiverDef)) return false;
            if (order.Batch == null)
            {
                if (!CraftingServices.Materials.TryPlan(core.Network, order, out var plan))
                { order.LastMaterialFailureTick = Find.TickManager.TicksGame; return false; }
                order.LastMaterialFailureTick = null;
                if (!CraftingMaterialPreparation.Prepare(core, order, plan)) return false;
            }
            assignments[order] = driver;
            return true;
        }
        internal Job FindJob(Pawn pawn, WorkGiverDef giver)
        {
            map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            // A periodic vanilla override must be allowed to keep the current job.
            if (pawn.jobs.curDriver is JobDriver_CosmicCrafting active && !active.ended &&
                pawn.CurJob.workGiverDef == giver && active.CanContinue()) return pawn.CurJob;
            if (cores.Count == 0) return null;
            lastCoreIds.TryGetValue(giver.workType, out int lastId);
            int start = cores.FindIndex(c => c.thingIDNumber > lastId);
            if (start < 0) start = 0;
            for (int n = 0; n < cores.Count; n++)
            {
                var core = cores[(start + n) % cores.Count];
                if (!core.CanWork || core.Faction != pawn.Faction) continue;
                var order = FindOrder(pawn, giver, core, out _, FloatMenuMakerMap.makingFor == pawn, false);
                if (order == null) continue;
                var bench = FindBench(pawn, core.Network);
                if (bench == null) continue;
                lastCoreIds[giver.workType] = core.thingIDNumber;
                return MakeJob(giver, bench, core, order);
            }
            return null;
        }
        private CraftingOrder FindOrder(Pawn pawn, WorkGiverDef giver, Building_StorageCore core, out string reason,
            bool immediate = false, bool reportFailure = true)
        {
            reason = null;
            foreach (var order in core.Crafting.Orders)
            {
                if (!CraftingRecipeCatalog.HasType(order.Recipe, giver.workType)) continue;
                // Only this pawn's eligible orders may contribute an availability reason.
                // This also avoids counting products or scanning materials for excluded orders.
                if (!CraftingServices.Workers.Allows(order, pawn, giver.workType)) continue;
                string failure = null;
                var worker = AssignedWorker(order);
                if (worker != null)
                { if (!reportFailure) continue; failure = "MS_Craft_Assigned".Translate(worker.LabelShortCap); }
                else if (!CanRun(core, order))
                { if (!reportFailure) continue; failure = Status(core, order, false); }
                else if (order.Batch == null && !MaterialsAvailable(core, order, immediate))
                { if (!reportFailure) continue; failure = "MS_Craft_MissingMaterials".Translate(); }
                if (failure == null) return order;
                if (reason == null) reason = failure;
            }
            if (reportFailure && reason == null) reason = "MS_Craft_NoOrdersForWorkType".Translate();
            return null;
        }
        private static Job MakeJob(WorkGiverDef giver, Building_CosmicWorkbench bench, Building_StorageCore core, CraftingOrder order)
        {
            var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("MS_CosmicCrafting"), bench, core);
            job.count = order.Id;
            job.workGiverDef = giver;
            return job;
        }
        internal Job FindJobOnBench(Pawn pawn, WorkGiverDef giver, Building_CosmicWorkbench bench, bool forced = false)
        {
            if (bench == null || !bench.Spawned || bench.Map != map || bench.Faction != pawn.Faction) return null;
            // Pawn-level eligibility takes precedence over occupied benches/orders.
            // Vanilla cannot report this itself when our scanner returns no job.
            if (pawn.WorkTypeIsDisabled(giver.workType))
            { JobFailReason.Is("MS_Craft_Incapable".Translate()); return null; }
            if (pawn.workSettings == null || !pawn.workSettings.WorkIsActive(giver.workType))
            { JobFailReason.Is("MS_Craft_WorkDisabled".Translate()); return null; }
            map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            var network = bench.Network;
            if (network?.CanWork != true)
            { JobFailReason.Is(StorageNetwork.StatusLabel(network)); return null; }
            // Return the actual running job so vanilla can display "already working".
            if (pawn.jobs.curDriver is JobDriver_CosmicCrafting active && !active.ended &&
                active.Bench == bench && pawn.CurJob.workGiverDef == giver && active.CanContinue()) return pawn.CurJob;
            if (!CanUseBench(pawn, bench, network))
            { JobFailReason.Is("MS_Craft_NoBench".Translate()); return null; }
            var order = FindOrder(pawn, giver, network.Core, out string reason, forced || FloatMenuMakerMap.makingFor == pawn);
            if (order == null) { JobFailReason.Is(reason); return null; }
            return MakeJob(giver, bench, network.Core, order);
        }
        internal Job FindPrioritizedJob(Pawn pawn, WorkGiverDef giver, IntVec3 cell) =>
            FindJobOnBench(pawn, giver, benches.FirstOrDefault(b => b.Spawned && b.Map == map && b.OccupiedRect().Contains(cell)));
        internal static bool CanUseBench(Pawn pawn, Building_CosmicWorkbench bench, StorageNetwork network)
        {
            return bench != null && bench.Spawned && bench.Network == network && network != null && network.CanWork &&
                bench.Faction == pawn.Faction && !bench.IsForbidden(pawn) && !bench.IsBurning() &&
                bench.InteractionCell.InBounds(pawn.Map) && !bench.InteractionCell.IsForbidden(pawn) &&
                bench.InteractionCell.Standable(pawn.Map) && pawn.CanReserveAndReach(bench, PathEndMode.InteractionCell, Danger.Some) &&
                pawn.CanReserveSittableOrSpot(bench.InteractionCell);
        }
        private Building_CosmicWorkbench FindBench(Pawn pawn, StorageNetwork network) => benches
            .Where(b => CanUseBench(pawn, b, network)).OrderBy(b => pawn.Position.DistanceToSquared(b.Position)).FirstOrDefault();

        internal string Status(Building_StorageCore core, CraftingOrder order)
            => Status(core, order, null);

        // A result passed by FindOrder is valid for this immediate call only, not a persistent cache.
        private string Status(Building_StorageCore core, CraftingOrder order, bool? canRun)
        {
            if (!core.CanWork) return StorageNetwork.StatusLabel(core.Network);
            if (order.Batch != null && !order.Batch.Valid(order)) return "MS_Craft_BatchInvalid".Translate();
            if (order.Suspended) return "MS_Craft_Suspended".Translate();
            if (!order.Recipe.AvailableNow) return "MS_Craft_Locked".Translate();
            if (!(canRun ?? CanRun(core, order))) return "MS_Craft_Satisfied".Translate();
            var worker = AssignedWorker(order);
            if (worker != null) return "MS_Craft_Assigned".Translate(worker.LabelShortCap);
            if (order.Batch == null && !MaterialsAvailable(core, order)) return "MS_Craft_MissingMaterials".Translate();
            var eligible = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == core.Faction &&
                CraftingRecipeCatalog.Routes(order.Recipe).Any(w => CraftingServices.Workers.Allows(order, p, w.workType))).ToList();
            if (eligible.Count == 0) return "MS_Craft_NoWorker".Translate();
            return eligible.Any(p => FindBench(p, core.Network) != null) ? "MS_Craft_Ready".Translate() : "MS_Craft_NoBench".Translate();
        }

        public override void MapComponentTick()
        {
            // Prepared materials belong to the order. Clean abandoned worker assignments
            // independently so one broken driver cannot block other workers from resuming.
            if (!maintenanceTimer.IsDue(StorageRefreshTask.CraftingMaintenance, Find.TickManager.TicksGame)) return;
            foreach (var pair in assignments.ToList())
            {
                var driver = pair.Value;
                bool release = false;
                try
                {
                    if (driver.ended || driver.pawn?.jobs?.curDriver != driver ||
                        driver.pawn.CurJob != driver.job || driver.pawn.Map != map) release = true;
                    else if (!driver.CanContinue())
                    {
                        release = true;
                        driver.EndJobWith(JobCondition.Incompletable);
                    }
                }
                catch (System.Exception exception)
                {
                    release = true;
                    Log.Error("[MagicStorage] Could not maintain crafting worker assignment: " + exception);
                }
                finally
                {
                    if (release) { driver.ReleaseAssignment(); Release(pair.Key, driver); }
                }
            }
        }

        public override void FinalizeInit()
        {
            // Old walking jobs have no saved batch: prepare once on load. Existing
            // batches only restore their worker assignment and never take materials again.
            map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                if (pawn.jobs?.curDriver is JobDriver_CosmicCrafting driver && !driver.ended && !driver.CanContinue())
                {
                    try { driver.EndJobWith(JobCondition.Incompletable); }
                    finally { driver.ReleaseAssignment(); }
                }
        }
    }

    public sealed class WorkGiver_CosmicCrafting : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) =>
            pawn.Map?.GetComponent<MapComponent_CosmicCrafting>().Workbenches;
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false) =>
            pawn.Map?.GetComponent<MapComponent_CosmicCrafting>().FindJobOnBench(pawn, def, thing as Building_CosmicWorkbench, forced);
        public override Job NonScanJob(Pawn pawn)
        {
            var dispatcher = pawn.Map?.GetComponent<MapComponent_CosmicCrafting>();
            if (dispatcher == null) return null;
            // Vanilla asks NonScanJob before scanning the prioritized cell. Restrict
            // that entry point too, or subsequent orders could switch workbenches.
            var priority = pawn.mindState.priorityWork;
            if (priority.IsPrioritized && priority.WorkGiver?.workType == def.workType)
                return dispatcher.FindPrioritizedJob(pawn, def, priority.Cell);
            return dispatcher.FindJob(pawn, def);
        }
    }
}
