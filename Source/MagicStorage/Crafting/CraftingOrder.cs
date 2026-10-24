using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public enum CraftingRepeatMode { RepeatCount, TargetCount, Forever }
    public enum CraftingWorkerKind { Anyone, Slaves, Mechs, NonMechs, Printers }

    // Orders and batches are owned by a core, outside the vanilla BillStack/worktable pipeline.
    public sealed class CraftingOrder : IExposable
    {
        internal const int UnlimitedSkillLevel = 21;

        public int Id;
        public RecipeDef Recipe;
        public string GroupId;
        public CraftingRepeatMode Mode;
        public int RepeatCount = 1, TargetCount = 10, UnpauseAt = 5;
        public bool Suspended, PauseWhenSatisfied, Paused;
        public bool DropProductsOnFloor;
        public Pawn Worker;
        public CraftingWorkerKind WorkerKind;
        public IntRange SkillRange = new IntRange(0, UnlimitedSkillLevel);
        public ThingFilter Ingredients = new ThingFilter();
        public ThingFilter AdditionalCounts = new ThingFilter();
        public bool IncludeEquipped, IncludeTainted = true, LimitToAllowedStuff;
        public FloatRange HitPoints = new FloatRange(0f, 1f);
        public QualityRange Quality = QualityRange.All;
        public int Revision;
        public CraftingBatch Batch;
        // Runtime-only failure cache; no background refresh or inventory invalidation.
        internal int? LastMaterialFailureTick;
        public float Progress => Batch?.Progress ?? 0f;
        // A paused order only needs to distinguish counts above its resume threshold.
        internal long ProductCountLimit => PauseWhenSatisfied && Paused
            ? Math.Min((long)TargetCount, (long)UnpauseAt + 1) : TargetCount;

        public CraftingOrder() { }
        public CraftingOrder(int id, RecipeDef recipe)
        {
            Id = id; Recipe = recipe;
            Ingredients.CopyAllowancesFrom(recipe.defaultIngredientFilter ?? recipe.fixedIngredientFilter);
        }

        public void Changed() { Revision++; }
        internal bool AllowsSkillLevel(int level) => level >= SkillRange.min &&
            (SkillRange.max == UnlimitedSkillLevel || level <= SkillRange.max);
        internal bool ShouldRun(long products) => CraftingRunPolicy.ShouldRun(Mode, Suspended,
            RepeatCount, TargetCount, PauseWhenSatisfied, UnpauseAt, products, ref Paused);

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Defs.Look(ref Recipe, "recipe");
            Scribe_Values.Look(ref GroupId, "groupId");
            Scribe_Deep.Look(ref Batch, "batch");
            Scribe_Values.Look(ref Mode, "mode");
            Scribe_Values.Look(ref RepeatCount, "repeatCount", 1);
            Scribe_Values.Look(ref TargetCount, "targetCount", 10);
            Scribe_Values.Look(ref UnpauseAt, "unpauseAt", 5);
            Scribe_Values.Look(ref Suspended, "suspended");
            Scribe_Values.Look(ref DropProductsOnFloor, "dropProductsOnFloor", false);
            Scribe_Values.Look(ref PauseWhenSatisfied, "pauseWhenSatisfied");
            Scribe_Values.Look(ref Paused, "paused");
            Scribe_References.Look(ref Worker, "worker");
            Scribe_Values.Look(ref WorkerKind, "workerKind");
            Scribe_Values.Look(ref SkillRange, "skillRange", new IntRange(0, UnlimitedSkillLevel));
            Scribe_Deep.Look(ref Ingredients, "ingredients");
            Scribe_Deep.Look(ref AdditionalCounts, "additionalCounts");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && AdditionalCounts == null)
                AdditionalCounts = new ThingFilter();
            Scribe_Values.Look(ref IncludeEquipped, "includeEquipped");
            Scribe_Values.Look(ref IncludeTainted, "includeTainted", true);
            Scribe_Values.Look(ref LimitToAllowedStuff, "limitToAllowedStuff");
            Scribe_Values.Look(ref HitPoints, "hitPoints", new FloatRange(0f, 1f));
            Scribe_Values.Look(ref Quality, "quality", QualityRange.All);
        }
    }

    public sealed class CraftingOrderGroup : IExposable
    {
        public string Id;
        public string Name;
        public bool Collapsed;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref Name, "name");
            Scribe_Values.Look(ref Collapsed, "collapsed", false);
        }
    }

    public sealed class CraftingOrderList : IExposable
    {
        private List<CraftingOrder> orders = new List<CraftingOrder>();
        private List<CraftingOrderGroup> groups = new List<CraftingOrderGroup>();
        private int nextId = 1;
        public bool GroupingMode;
        public bool UngroupedCollapsed;
        private int ungroupedPosition = int.MaxValue;
        private int groupingPriorityVersion = 1;
        public IReadOnlyList<CraftingOrder> Orders => orders;
        internal IEnumerable<CraftingOrderGroup> GroupsInUse()
        {
            return DisplayGroups().FindAll(group => group != null);
        }

        internal List<CraftingOrderGroup> DisplayGroups()
        {
            var byId = new Dictionary<string, CraftingOrderGroup>();
            foreach (var group in groups) byId[group.Id] = group;
            var result = new List<CraftingOrderGroup>();
            var seen = new HashSet<string>();
            foreach (var order in orders)
            {
                if (!seen.Add(order.GroupId)) continue;
                result.Add(order.GroupId != null && byId.TryGetValue(order.GroupId, out var group) ? group : null);
            }
            if (!seen.Contains(null)) result.Insert(Math.Min(ungroupedPosition, result.Count), null);
            return result;
        }

        // The real production queue is authoritative. Every group occupies one block;
        // regroup by explicit membership, never by the interval between its endpoints.
        private void SetGroupOrder(List<CraftingOrderGroup> display)
        {
            display.RemoveAll(group => group != null && !orders.Exists(order => order.GroupId == group.Id));
            var reordered = new List<CraftingOrder>(orders.Count);
            foreach (var group in display) reordered.AddRange(orders.FindAll(order => order.GroupId == group?.Id));
            orders.Clear(); orders.AddRange(reordered);
            int index = display.IndexOf(null);
            ungroupedPosition = index == display.Count - 1 ? int.MaxValue : index;
            PruneEmptyGroups();
        }

        internal void MoveGroup(CraftingOrderGroup moved, CraftingOrderGroup target, bool after)
        {
            var display = DisplayGroups();
            if (moved == target || !display.Contains(moved) || !display.Contains(target)) return;
            display.Remove(moved);
            display.Insert(display.IndexOf(target) + (after ? 1 : 0), moved);
            SetGroupOrder(display);
        }

        internal bool MoveToGroup(CraftingOrder moved, CraftingOrderGroup target, CraftingOrder anchor = null, bool after = true)
        {
            if (moved == null || !orders.Contains(moved) || target != null && !groups.Contains(target) ||
                anchor == moved || anchor != null && (!orders.Contains(anchor) || anchor.GroupId != target?.Id)) return false;
            var display = DisplayGroups();
            orders.Remove(moved);
            int index = anchor == null ? orders.FindLastIndex(order => order.GroupId == target?.Id) + 1
                : orders.IndexOf(anchor) + (after ? 1 : 0);
            if (anchor == null && index == 0) index = orders.Count;
            moved.GroupId = target?.Id;
            orders.Insert(index, moved);
            SetGroupOrder(display);
            return true;
        }

        internal CraftingOrderGroup CreateGroup(IEnumerable<CraftingOrder> selected, string name)
        {
            if (selected == null || string.IsNullOrWhiteSpace(name)) return null;
            var selection = new HashSet<CraftingOrder>(selected);
            var members = orders.FindAll(selection.Contains);
            if (members.Count == 0) return null;
            var display = DisplayGroups();
            var sourceGroup = members[0].GroupId == null ? null : groups.Find(candidate => candidate.Id == members[0].GroupId);
            var group = new CraftingOrderGroup { Id = Guid.NewGuid().ToString("N"), Name = name.Trim() };
            display.Insert(display.IndexOf(sourceGroup), group);
            groups.Add(group);
            foreach (var order in members) order.GroupId = group.Id;
            SetGroupOrder(display);
            return group;
        }

        internal void RenameGroup(CraftingOrderGroup group, string name)
        { if (groups.Contains(group) && !string.IsNullOrWhiteSpace(name)) group.Name = name.Trim(); }

        internal void DeleteGroup(CraftingOrderGroup group)
        {
            int index = group == null ? -1 : groups.IndexOf(group);
            if (index < 0) return;
            var display = DisplayGroups(); display.Remove(group);
            groups.RemoveAt(index);
            foreach (var order in orders) if (order.GroupId == group.Id) order.GroupId = null;
            SetGroupOrder(display);
        }

        private void PruneEmptyGroups()
        {
            var used = new HashSet<string>();
            foreach (var order in orders) if (order.GroupId != null) used.Add(order.GroupId);
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                if (used.Contains(groups[i].Id)) continue;
                groups.RemoveAt(i);
            }
        }
        public CraftingOrder Add(RecipeDef recipe)
        {
            var display = DisplayGroups();
            var order = new CraftingOrder(nextId++, recipe);
            if (MagicStorageMod.Settings.NewWorkbenchOrdersFirst) orders.Insert(0, order);
            else orders.Add(order);
            SetGroupOrder(display);
            return order;
        }
        public CraftingOrder Find(int id) => orders.Find(order => order.Id == id);
        public void Remove(CraftingOrder order)
        {
            if (order.Batch != null && !order.Batch.Cancel()) return;
            var display = DisplayGroups();
            order.Batch = null; order.Changed(); orders.Remove(order);
            SetGroupOrder(display);
        }
        internal bool CancelAll(bool tryStore)
        {
            foreach (var order in orders)
            {
                if (order.Batch != null && !order.Batch.Cancel(tryStore)) return false;
                order.Batch = null; order.Changed();
            }
            return true;
        }
        public void Move(CraftingOrder order, int delta)
        {
            int index = orders.IndexOf(order), target = index + delta;
            if (index < 0 || target < 0 || target >= orders.Count) return;
            Reorder(index, target + (delta > 0 ? 1 : 0));
        }
        internal void Reorder(int from, int insertionIndex) => Reorder(orders, from, insertionIndex);

        internal void Reorder(IReadOnlyList<CraftingOrder> visible, int from, int insertionIndex)
        {
            // Native ReorderableWidget supplies an insertion slot in the list before removal.
            if (visible == null || from < 0 || from >= visible.Count || insertionIndex < 0 || insertionIndex > visible.Count) return;
            int target = insertionIndex > from ? insertionIndex - 1 : insertionIndex;
            if (target == from) return;
            var positions = new List<int>();
            foreach (var order in visible)
            {
                int position = orders.IndexOf(order);
                // Reject stale or duplicate snapshots before changing any order.
                if (position < 0 || (positions.Count > 0 && position <= positions[positions.Count - 1])) return;
                positions.Add(position);
            }
            // Flat mode presents the same queue. Crossing a group boundary transfers
            // membership too, so reopening grouped mode cannot change the displayed order.
            var anchor = visible[insertionIndex == visible.Count ? visible.Count - 1 : insertionIndex];
            if (anchor.GroupId != visible[from].GroupId)
            {
                var destination = anchor.GroupId == null ? null : groups.Find(group => group.Id == anchor.GroupId);
                MoveToGroup(visible[from], destination, anchor, insertionIndex == visible.Count);
                return;
            }
            var reordered = new List<CraftingOrder>(visible);
            var moved = reordered[from];
            reordered.RemoveAt(from);
            reordered.Insert(target, moved);
            // Write back only the visible slots, leaving every hidden task in its original position.
            for (int i = 0; i < positions.Count; i++) orders[positions[i]] = reordered[i];
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref orders, "orders", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Collections.Look(ref groups, "groups", LookMode.Deep);
            Scribe_Values.Look(ref GroupingMode, "groupingMode", false);
            Scribe_Values.Look(ref UngroupedCollapsed, "ungroupedCollapsed", false);
            Scribe_Values.Look(ref ungroupedPosition, "ungroupedPosition", int.MaxValue);
            Scribe_Values.Look(ref groupingPriorityVersion, "groupingPriorityVersion", 0);
            if (groups == null) groups = new List<CraftingOrderGroup>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (orders == null) orders = new List<CraftingOrder>();
                orders.RemoveAll(o => o?.Recipe == null);
                var ids = new HashSet<string>();
                groups.RemoveAll(group => group == null || string.IsNullOrEmpty(group.Id) || !ids.Add(group.Id));
                foreach (var order in orders)
                    if (order.GroupId != null && !ids.Contains(order.GroupId)) order.GroupId = null;
                PruneEmptyGroups();
                if (ungroupedPosition < 0) ungroupedPosition = int.MaxValue;
                if (groupingPriorityVersion == 0)
                {
                    // Upgrade the earlier display-only grouping order into the actual queue.
                    var display = new List<CraftingOrderGroup>(groups);
                    display.Insert(Math.Min(ungroupedPosition, display.Count), null);
                    SetGroupOrder(display);
                    groupingPriorityVersion = 1;
                }
                else SetGroupOrder(DisplayGroups());
            }
        }
    }
}
