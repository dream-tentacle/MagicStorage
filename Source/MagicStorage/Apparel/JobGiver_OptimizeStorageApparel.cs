using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    public sealed class JobGiver_OptimizeStorageApparel : JobGiver_OptimizeApparel
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var policy = StorageApparelServices.Policy;
            bool eligible = policy.CanOptimize(pawn) &&
                (DebugViewSettings.debugApparelOptimize || Find.TickManager.TicksGame >= pawn.mindState.nextApparelOptimizeTick);
            int nextTick = pawn.mindState.nextApparelOptimizeTick;
            // Preserve vanilla's recoloring, removal, map searches and scheduling.
            Job original = base.TryGiveJob(pawn);
            if (!eligible || (original != null && original.def != JobDefOf.Wear)) return original;
            var manager = pawn.Map.GetComponent<MapComponent_StorageNetworks>();
            manager.EnsureCurrent();
            if (manager.ApparelAdapters.Count == 0) return original;
            using (policy.BeginScoring(pawn))
            {
                var scores = pawn.apparel.WornApparel.Select(a => policy.RawScore(pawn, a)).ToList();
                float bestGain = original?.GetTarget(TargetIndex.A).Thing is Apparel originalApparel
                    ? policy.ScoreGain(pawn, originalApparel, scores) : 0f;
                Apparel best = null;
                Building_StorageApparelAdapter chosen = null;
                var visited = new HashSet<StorageNetwork>();
                // Equivalent scores keep the native choice; network ties use the nearer outlet.
                foreach (var adapter in manager.ApparelAdapters.OrderBy(a => a.Position.DistanceToSquared(pawn.Position)))
                {
                    if (!StorageApparelServices.CanAccess(pawn, adapter) || adapter.HasApparel ||
                        !pawn.CanReserveAndReach(adapter, PathEndMode.InteractionCell, pawn.NormalMaxDanger()) ||
                        !pawn.CanReserveSittableOrSpot(adapter.InteractionCell) || !visited.Add(adapter.Network)) continue;
                    var candidates = new List<Thing>();
                    adapter.Network.GetMatchingStacks(def => def.IsApparel, candidates);
                    foreach (Thing item in candidates)
                    {
                        if (!(item is Apparel apparel) || adapter.Network.AvailableToWithdraw(item) < 1 ||
                            !(item.holdingOwner?.Owner is Building_StorageUnit unit) || unit.IsForbidden(pawn) || unit.IsBurning() ||
                            !policy.Allows(pawn, apparel)) continue;
                        float gain = policy.ScoreGain(pawn, apparel, scores);
                        if (gain < 0.05f || gain <= bestGain) continue;
                        best = apparel; chosen = adapter; bestGain = gain;
                    }
                }
                if (best == null) return original;
                // Vanilla may have scheduled a later retry after finding nothing on the map.
                // A successful network choice follows the timing of a successful Wear job.
                pawn.mindState.nextApparelOptimizeTick = nextTick;
                return JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("MS_TakeStorageApparel"), best, chosen, chosen.Network.Core);
            }
        }
    }
}
