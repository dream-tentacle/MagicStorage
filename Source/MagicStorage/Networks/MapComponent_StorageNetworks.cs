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
        private readonly List<StorageNetwork> networks = new List<StorageNetwork>();
        private List<StorageRecoveryBatch> recovery = new List<StorageRecoveryBatch>();
        private ThingOwner<Thing> directlyHeldThings;
        private bool dirty = true;
        private readonly StorageOutgoingReservations reservations = new StorageOutgoingReservations();
        private readonly Queue<System.Action> pendingActions = new Queue<System.Action>();
        internal void NotifyNodeUnavailable(Thing node) => reservations.NotifyNodeUnavailable(node);
        internal void Defer(System.Action action) => pendingActions.Enqueue(action);
        private readonly List<Building_StorageReceiver> receivers = new List<Building_StorageReceiver>();
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
            MarkDirty();
        }

        internal void Unregister(CompStorageNode node)
        {
            if (!nodes.Remove(node)) return;
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
            if (dirty) Rebuild();
            reservations.Validate();
            reservations.DispatchNotifications();
            int pending = pendingActions.Count;
            while (pending-- > 0) pendingActions.Dequeue()();
            foreach (var receiver in receivers) receiver.ProcessIncoming();
            // Only exceptional, pending releases are revisited. Stored inventories never tick.
            if (recovery.Count > 0 && Find.TickManager.TicksGame % 250 == 0)
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

        internal void EnsureCurrent() { if (dirty) Rebuild(); }

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
