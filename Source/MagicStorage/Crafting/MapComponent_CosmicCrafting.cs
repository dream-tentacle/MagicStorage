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
        public MapComponent_CosmicCrafting(Map map) : base(map) { }
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
        internal Pawn AssignedWorker(CraftingOrder order) => assignments.TryGetValue(order, out var driver) && !driver.ended ? driver.pawn : null;
        internal void Release(CraftingOrder order, JobDriver_CosmicCrafting driver)
        { if (order != null && assignments.TryGetValue(order, out var owner) && owner == driver) assignments.Remove(order); }

        internal bool CanRun(Building_StorageCore core, CraftingOrder order)
        {
            if (!core.CanWork || core.Faction != Faction.OfPlayer || order?.Recipe == null ||
                !order.Recipe.AvailableNow || !CraftingRecipeCatalog.Supports(order.Recipe)) return false;
            if (order.Batch != null && !order.Batch.Valid(order)) return false;
            if (order.Batch?.Committed == true) return !order.Suspended;
            if (order.Mode == CraftingRepeatMode.TargetCount && !CraftingServices.Products.CanCount(order.Recipe)) return false;
            return order.ShouldRun(order.Mode == CraftingRepeatMode.TargetCount ? CraftingServices.Products.Count(core, order) : 0);
        }
        internal bool TryClaim(JobDriver_CosmicCrafting driver, out CraftingMaterialLease lease)
        {
            lease = null;
            var core = driver.Core; var order = driver.Order;
            if (order == null || core == null || !CanRun(core, order) || AssignedWorker(order) != null ||
                !CraftingServices.Workers.Allows(order, driver.pawn, driver.job.workGiverDef.workType)) return false;
            if (order.Batch == null)
            {
                if (!CraftingServices.Materials.TryPlan(core.Network, order, out var plan)) return false;
                var acquired = new CraftingMaterialLease(core.Network, driver.Bench, order, driver.OnMaterialsInvalidated);
                if (!acquired.Acquire(plan)) return false;
                lease = acquired;
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
                foreach (var order in core.Crafting.Orders)
                {
                    if (AssignedWorker(order) != null || !CraftingRecipeCatalog.HasType(order.Recipe, giver.workType) ||
                        !CanRun(core, order) || !CraftingServices.Workers.Allows(order, pawn, giver.workType) ||
                        (order.Batch == null && !CraftingServices.Materials.TryPlan(core.Network, order, out _))) continue;
                    var bench = FindBench(pawn, core.Network);
                    if (bench == null) break;
                    var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("MS_CosmicCrafting"), bench, core);
                    job.count = order.Id;
                    job.workGiverDef = giver;
                    lastCoreIds[giver.workType] = core.thingIDNumber;
                    return job;
                }
            }
            return null;
        }
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
        {
            if (!core.CanWork) return StorageNetwork.StatusLabel(core.Network);
            if (order.Batch != null && !order.Batch.Valid(order)) return "MS_Craft_BatchInvalid".Translate();
            if (order.Suspended) return "MS_Craft_Suspended".Translate();
            if (!order.Recipe.AvailableNow) return "MS_Craft_Locked".Translate();
            if (!CanRun(core, order)) return "MS_Craft_Satisfied".Translate();
            var worker = AssignedWorker(order);
            if (worker != null) return "MS_Craft_Assigned".Translate(worker.LabelShortCap);
            if (order.Batch == null && !CraftingServices.Materials.TryPlan(core.Network, order, out _)) return "MS_Craft_MissingMaterials".Translate();
            var eligible = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == core.Faction &&
                CraftingRecipeCatalog.Routes(order.Recipe).Any(w => CraftingServices.Workers.Allows(order, p, w.workType))).ToList();
            if (eligible.Count == 0) return "MS_Craft_NoWorker".Translate();
            return eligible.Any(p => FindBench(p, core.Network) != null) ? "MS_Craft_Ready".Translate() : "MS_Craft_NoBench".Translate();
        }

        public override void MapComponentTick()
        {
            // Also releases claims if a driver was abandoned before its toil setup completed.
            if (Find.TickManager.TicksGame % 60 != 0) return;
            foreach (var driver in assignments.Values.ToList())
            {
                if (driver.ended || driver.pawn.jobs.curDriver != driver) driver.ReleaseAssignment();
                else if (!driver.CanContinue()) driver.EndJobWith(JobCondition.Incompletable);
            }
        }

        public override void FinalizeInit()
        {
            // Restore runtime claims before idle pawns can claim a loaded job's order/materials.
            map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                if (pawn.jobs?.curDriver is JobDriver_CosmicCrafting driver && !driver.ended) driver.CanContinue();
        }
    }

    public sealed class WorkGiver_CosmicCrafting : WorkGiver
    {
        public override Job NonScanJob(Pawn pawn) => pawn.Map?.GetComponent<MapComponent_CosmicCrafting>().FindJob(pawn, def);
    }
}
