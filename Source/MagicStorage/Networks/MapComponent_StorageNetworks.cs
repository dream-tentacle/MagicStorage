using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class MapComponent_StorageNetworks : MapComponent, IThingHolder
    {
        private readonly HashSet<CompStorageNode> nodes = new HashSet<CompStorageNode>();
        private readonly Dictionary<IntVec3, List<CompStorageNode>> grid = new Dictionary<IntVec3, List<CompStorageNode>>();
        private readonly Dictionary<CompStorageNode, List<IntVec3>> occupied = new Dictionary<CompStorageNode, List<IntVec3>>();
        private readonly HashSet<StorageNetwork> networks = new HashSet<StorageNetwork>();
        private List<StorageRecoveryBatch> recovery = new List<StorageRecoveryBatch>();
        private ThingOwner<Thing> directlyHeldThings;
        private bool dirty = true;
        private bool topologyChanged;
        private StorageRefreshTimer maintenanceTimer, recoveryTimer;
        private readonly StorageOutgoingReservations reservations = new StorageOutgoingReservations();
        private readonly Queue<System.Action> pendingActions = new Queue<System.Action>();
        internal void NotifyNodeUnavailable(Thing node) => reservations.NotifyNodeUnavailable(node);
        internal void Defer(System.Action action) => pendingActions.Enqueue(action);
        private readonly List<Building_StorageReceiver> receivers = new List<Building_StorageReceiver>();
        private readonly List<Building_StorageCore> cores = new List<Building_StorageCore>();
        internal IReadOnlyList<Building_StorageCore> StorageCores => cores;
        private readonly List<Building_StorageUnit> units = new List<Building_StorageUnit>();
        internal IReadOnlyList<Building_StorageUnit> StorageUnits => units;
        private readonly List<Building_StorageSupplyShelf> supplyShelves = new List<Building_StorageSupplyShelf>();
        internal IReadOnlyList<Building_StorageSupplyShelf> SupplyShelves => supplyShelves;
        private readonly List<Building_StorageApparelAdapter> apparelAdapters = new List<Building_StorageApparelAdapter>();
        internal IReadOnlyList<Building_StorageApparelAdapter> ApparelAdapters => apparelAdapters;

        public MapComponent_StorageNetworks(Map map) : base(map) { }
        public IThingHolder ParentHolder => map;
        public ThingOwner GetDirectlyHeldThings() => directlyHeldThings ??
            (directlyHeldThings = new ThingOwner<Thing>(this, false, LookMode.Deep) { dontTickContents = true });
        public void GetChildHolders(List<IThingHolder> children) { children.AddRange(recovery); }

        internal void Register(CompStorageNode node)
        {
            if (!nodes.Add(node)) return;
            if (node.parent is Building_StorageCore core) cores.Add(core);
            if (node.parent is Building_StorageReceiver receiver) receivers.Add(receiver);
            if (node.parent is Building_StorageUnit unit) units.Add(unit);
            if (node.parent is Building_StorageSupplyShelf shelf) supplyShelves.Add(shelf);
            if (node.parent is Building_StorageApparelAdapter adapter) apparelAdapters.Add(adapter);
            List<IntVec3> cells = new List<IntVec3>();
            foreach (IntVec3 cell in node.parent.OccupiedRect())
            {
                cells.Add(cell);
                if (!grid.TryGetValue(cell, out var list)) grid.Add(cell, list = new List<CompStorageNode>());
                list.Add(node);
                DirtyDrawing(cell);
            }
            occupied.Add(node, cells);
            if (!dirty) ConnectIncrementally(node, cells);
        }

        internal void Unregister(CompStorageNode node)
        {
            if (!nodes.Remove(node)) return;
            if (node.parent is Building_StorageCore core) cores.Remove(core);
            NotifyNodeUnavailable(node.parent);
            if (node.parent is Building_StorageReceiver receiver) receivers.Remove(receiver);
            if (node.parent is Building_StorageUnit unit) units.Remove(unit);
            if (node.parent is Building_StorageSupplyShelf shelf) supplyShelves.Remove(shelf);
            if (node.parent is Building_StorageApparelAdapter adapter) apparelAdapters.Remove(adapter);
            if (occupied.TryGetValue(node, out var cells))
            {
                foreach (IntVec3 cell in cells)
                {
                    var list = grid[cell];
                    list.Remove(node);
                    if (list.Count == 0) grid.Remove(cell);
                    DirtyDrawing(cell);
                }
                occupied.Remove(node);
            }
            node.Network?.Invalidate();
            node.Network = null;
            MarkDirty();
        }

        public void MarkDirty()
        {
            dirty = true;
            foreach (var network in networks) network.Invalidate();
        }

        private void ConnectIncrementally(CompStorageNode node, List<IntVec3> cells)
        {
            var adjacent = new HashSet<StorageNetwork>();
            foreach (var cell in cells)
            {
                CollectAdjacent(cell, node, adjacent);
                foreach (var direction in GenAdj.CardinalDirections)
                    CollectAdjacent(cell + direction, node, adjacent);
            }
            StorageNetwork target = null;
            foreach (var network in adjacent)
                if (target == null || network.NodeCount > target.NodeCount) target = network;
            if (target == null)
            {
                target = new StorageNetwork(reservations);
                networks.Add(target);
            }
            target.AddNode(node);
            foreach (var network in adjacent)
                if (network != target)
                {
                    target.Absorb(network);
                    networks.Remove(network);
                }
            target.FinishRebuild();
            topologyChanged = true;
        }

        private void CollectAdjacent(IntVec3 cell, CompStorageNode added, HashSet<StorageNetwork> result)
        {
            if (!grid.TryGetValue(cell, out var list)) return;
            foreach (var node in list)
                if (node != added && node.parent.Faction == added.parent.Faction && node.Network != null)
                    result.Add(node.Network);
        }

        internal void NotifyFactionChanged(Thing thing)
        {
            foreach (var cell in thing.OccupiedRect()) DirtyDrawing(cell);
            MarkDirty();
        }

        private void DirtyDrawing(IntVec3 cell)
        {
            map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Things, true, false);
        }

        public bool HasNodeAt(IntVec3 cell, Thing from)
        {
            if (!grid.TryGetValue(cell, out var list)) return false;
            foreach (var node in list)
                if (node.parent.Faction == from.Faction) return true;
            return false;
        }

        public override void MapComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            // Topology changes and queued invalidations are events, not polling. Handle
            // them promptly so removing a building cannot leave a live job or claim.
            if (dirty) Rebuild();
            else if (topologyChanged) FinishTopologyChange();
            if (maintenanceTimer.IsDue(StorageRefreshTask.NetworkMaintenance, tick)) reservations.Validate();
            reservations.DispatchNotifications();
            int pending = pendingActions.Count;
            while (pending-- > 0) pendingActions.Dequeue()();
            foreach (var receiver in receivers) receiver.ProcessIncoming();
            // Only exceptional, pending releases are revisited. Stored inventories never tick.
            if (recovery.Count > 0 && recoveryTimer.IsDue(StorageRefreshTask.RecoveryRelease, tick))
            {
                var batch = recovery[0];
                batch.TryRelease(map, 8);
                recovery.RemoveAt(0);
                if (batch.HasContents) recovery.Add(batch);
            }
        }

        private void Rebuild()
        {
            foreach (var network in networks) network.Invalidate();
            networks.Clear();
            HashSet<CompStorageNode> visited = new HashSet<CompStorageNode>();
            Queue<CompStorageNode> queue = new Queue<CompStorageNode>();
            foreach (var root in nodes)
            {
                if (!visited.Add(root)) continue;
                StorageNetwork network = new StorageNetwork(reservations);
                networks.Add(network);
                queue.Enqueue(root);
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    network.AddNode(node);
                    foreach (var cell in occupied[node])
                    {
                        EnqueueAt(cell, root, visited, queue);
                        foreach (var direction in GenAdj.CardinalDirections)
                            EnqueueAt(cell + direction, root, visited, queue);
                    }
                }
                network.FinishRebuild();
            }
            dirty = false;
            FinishTopologyChange();
        }

        private void FinishTopologyChange()
        {
            topologyChanged = false;
            // Topology can switch a receiver to a different core/priority or disable it.
            NotifyReceiverSettingsChanged();
            reservations.Validate();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.jobs?.curDriver is IStorageNetworkClient client && !pawn.jobs.curDriver.ended)
                    client.OnStorageNetworksRebuilt();
            }
        }

        internal void NotifyReceiverSettingsChanged()
        {
            map.haulDestinationManager.Notify_HaulDestinationChangedPriority();
            foreach (var receiver in receivers) receiver.Notify_SettingsChanged();
        }

        internal void EnsureCurrent()
        {
            if (dirty) Rebuild();
            else if (topologyChanged) FinishTopologyChange();
        }

        private void EnqueueAt(IntVec3 cell, CompStorageNode root, HashSet<CompStorageNode> visited, Queue<CompStorageNode> queue)
        {
            if (!grid.TryGetValue(cell, out var list)) return;
            foreach (var node in list)
                if (node.parent.Faction == root.parent.Faction && visited.Add(node)) queue.Enqueue(node);
        }

        internal StorageRecoveryBatch CreateRecovery(IntVec3 origin)
        {
            StorageRecoveryBatch batch = new StorageRecoveryBatch(this, origin);
            recovery.Add(batch);
            return batch;
        }

        internal void ReleaseRecovery(StorageRecoveryBatch batch)
        {
            batch.TryRelease(map, int.MaxValue);
            if (!batch.HasContents) recovery.Remove(batch);
        }

        internal void PreserveLooseThing(Thing item, IntVec3 origin)
        {
            if (item == null || item.Destroyed || item.Spawned || item.holdingOwner != null) return;
            CreateRecovery(origin).Contents.TryAdd(item, false);
            Log.Warning("[MagicStorage] A failed transfer was preserved for release near " + origin + ".");
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref recovery, "storageRecovery", LookMode.Deep, this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (recovery == null) recovery = new List<StorageRecoveryBatch>();
                recovery.RemoveAll(batch => batch == null || !batch.HasContents);
                MarkDirty();
            }
        }

        public override void FinalizeInit() { MarkDirty(); Rebuild(); }
    }
}
