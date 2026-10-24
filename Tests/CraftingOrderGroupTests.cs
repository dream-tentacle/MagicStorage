using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class CraftingOrderGroupTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Crafting groups: " + message + "; expected " + expected + ", got " + actual); }

    internal static int Run()
    {
        bool previousPlacement = MagicStorageMod.Settings.NewWorkbenchOrdersFirst;
        MagicStorageMod.Settings.NewWorkbenchOrdersFirst = false;
        try
        {
            MembershipAndDeletion(); SaveAndLoad(); LegacyAndMissingGroups(); Dragging(); PriorityOrder(); NewOrderPlacement();
            return checks;
        }
        finally { MagicStorageMod.Settings.NewWorkbenchOrdersFirst = previousPlacement; }
    }

    private static void MembershipAndDeletion()
    {
        var list = new CraftingOrderList(); var recipe = new RecipeDef();
        var a = list.Add(recipe); var b = list.Add(recipe); var c = list.Add(new RecipeDef());
        var foreign = new CraftingOrderList().Add(recipe);
        Equal(false, list.GroupingMode, "grouping defaults off");
        Equal(null, a.GroupId, "new orders default to ungrouped");
        Equal(null, list.CreateGroup(new CraftingOrder[0], "Empty"), "empty selection creates no group");
        Equal(null, list.CreateGroup(new[] { foreign }, "Foreign"), "another core's orders cannot be grouped");
        var first = list.CreateGroup(new[] { a, a, c, foreign }, " Parts ");
        Equal("Parts", first.Name, "group names are trimmed");
        Equal(first.Id, a.GroupId, "first selected order joins group");
        Equal(null, b.GroupId, "another task using the same recipe stays independent");
        Equal(null, foreign.GroupId, "other core is untouched");
        int revision = a.Revision;
        var second = list.CreateGroup(new[] { a, b }, "Parts");
        Equal(second.Id, a.GroupId, "regrouping replaces the old membership");
        Equal(first.Id, c.GroupId, "remaining old group members are retained");
        Equal(2, list.GroupsInUse().Count(), "same names represent independent groups");
        list.RenameGroup(second, " Tools ");
        Equal("Tools", second.Name, "rename updates shared group name");
        Equal(second.Id, b.GroupId, "rename keeps stable membership");
        list.RenameGroup(second, " ");
        Equal("Tools", second.Name, "blank names are rejected");
        list.GroupingMode = true; first.Collapsed = true; list.UngroupedCollapsed = true;
        list.GroupingMode = false;
        Equal(second.Id, a.GroupId, "disabling mode preserves membership");
        Equal(true, first.Collapsed, "disabling mode preserves collapse state");
        Equal(true, list.UngroupedCollapsed, "ungrouped collapse state is retained");
        list.DeleteGroup(second);
        Equal(null, a.GroupId, "deleting group ungroups its first member");
        Equal(null, b.GroupId, "deleting group ungroups every member");
        Equal(first.Id, c.GroupId, "deleting group preserves other memberships");
        Equal(true, list.Orders.SequenceEqual(new[] { c, a, b }), "deleting group moves its members into the Ungrouped block");
        Equal(revision, a.Revision, "display changes do not invalidate production configuration");
        Equal(3, list.Orders.Count, "deleting a group never deletes tasks");
        var third = list.CreateGroup(new[] { c }, "Moved");
        Equal(true, list.GroupsInUse().SequenceEqual(new[] { third }), "moving all members removes the empty source group");
        list.Remove(c);
        Equal(0, list.GroupsInUse().Count(), "removing the last task leaves no phantom header");
    }

    private static void SaveAndLoad()
    {
        var list = new CraftingOrderList { GroupingMode = true, UngroupedCollapsed = true };
        var a = list.Add(new RecipeDef()); var b = list.Add(a.Recipe); var c = list.Add(new RecipeDef());
        var group = list.CreateGroup(new[] { a, c }, "Saved group"); group.Collapsed = true;
        var snapshot = Snapshot.Save(list);
        var loaded = snapshot.Load(); var restored = loaded.GroupsInUse().Single();
        Equal(true, loaded.GroupingMode, "grouping mode survives save and load");
        Equal(true, loaded.UngroupedCollapsed, "ungrouped collapse survives save and load");
        Equal(group.Id, restored.Id, "group identity survives save and load");
        Equal("Saved group", restored.Name, "group name survives save and load");
        Equal(true, restored.Collapsed, "group collapse survives save and load");
        Equal(group.Id, loaded.Orders[0].GroupId, "first task membership survives save and load");
        Equal(group.Id, loaded.Orders[1].GroupId, "group members retain their contiguous priority block after load");
        Equal(null, loaded.Orders[2].GroupId, "ungrouped duplicate recipe remains independent after load");
        loaded.GroupingMode = false;
        loaded = Snapshot.Save(loaded).Load();
        Equal(false, loaded.GroupingMode, "closed mode remains closed after load");
        Equal(true, loaded.GroupsInUse().Single().Collapsed, "closed mode retains group state in save");
        loaded.DeleteGroup(loaded.GroupsInUse().Single());
        loaded = Snapshot.Save(loaded).Load();
        Equal(true, loaded.Orders.All(order => order.GroupId == null), "deleted memberships stay cleared after load");
        Equal(0, loaded.GroupsInUse().Count(), "deleted group does not reappear after load");
    }

    private static void LegacyAndMissingGroups()
    {
        var list = new CraftingOrderList { GroupingMode = true, UngroupedCollapsed = true };
        var order = list.Add(new RecipeDef()); list.CreateGroup(new[] { order }, "Legacy").Collapsed = true;
        var old = Snapshot.Save(list);
        old.Root.Values.Remove("groupingMode"); old.Root.Values.Remove("ungroupedCollapsed"); old.Root.Children.Remove("groups");
        foreach (var node in old.Root.Children["orders"]) node.Values.Remove("groupId");
        var loaded = old.Load();
        Equal(false, loaded.GroupingMode, "old save without new fields defaults mode off");
        Equal(false, loaded.UngroupedCollapsed, "old save defaults ungrouped to expanded");
        Equal(null, loaded.Orders.Single().GroupId, "old save tasks load as ungrouped");
        Equal(0, loaded.GroupsInUse().Count(), "old save has no custom groups");
        var broken = Snapshot.Save(list); broken.Root.Children.Remove("groups");
        Equal(null, broken.Load().Orders.Single().GroupId, "missing group record safely restores ungrouped membership");
    }

    private static void Dragging()
    {
        var list = new CraftingOrderList { GroupingMode = true };
        var a = list.Add(new RecipeDef()); var b = list.Add(a.Recipe); var c = list.Add(new RecipeDef());
        var d = list.Add(new RecipeDef()); var hidden = list.Add(new RecipeDef());
        var first = list.CreateGroup(new[] { a, b }, "First");
        var second = list.CreateGroup(new[] { c, d }, "Second");
        first.Collapsed = true;
        var priorities = list.Orders.ToArray();
        list.MoveGroup(second, first, false);
        Equal(true, list.DisplayGroups().SequenceEqual(new[] { second, first, null }), "dragging header reorders whole groups");
        Equal(true, list.Orders.SequenceEqual(new[] { c, d, a, b, hidden }), "group drag moves actual production priority as one block");
        Equal(true, first.Collapsed, "group drag preserves collapse state");
        list.MoveGroup(null, second, false);
        Equal(true, list.DisplayGroups().SequenceEqual(new[] { null, second, first }), "Ungrouped can also be dragged");
        var saved = Snapshot.Save(list).Load();
        Equal(true, saved.DisplayGroups().Select(group => group?.Name).SequenceEqual(new[] { null, "Second", "First" }),
            "group order including Ungrouped survives save and load");
        int revision = a.Revision;
        Equal(true, list.MoveToGroup(a, second, d, false), "task can be dragged into another group before a target task");
        Equal(second.Id, a.GroupId, "cross-group task drag updates membership");
        Equal(true, list.Orders.SequenceEqual(new[] { hidden, c, a, d, b }), "task drop sets insertion position within the destination priority block");
        Equal(true, list.MoveToGroup(b, second), "folded-group header accepts task at group end");
        Equal(false, list.GroupsInUse().Contains(first), "moving the last task removes the empty source group");
        Equal(true, list.MoveToGroup(a, null, hidden, false), "task can be dragged out to Ungrouped");
        Equal(null, a.GroupId, "Ungrouped drop clears membership");
        Equal(a, list.Orders[0], "Ungrouped drop inserts at requested position");
        Equal(revision, a.Revision, "task dragging preserves in-progress configuration");
        var stale = new CraftingOrderGroup { Id = "removed", Name = "Removed" };
        priorities = list.Orders.ToArray();
        Equal(false, list.MoveToGroup(a, stale), "drop into deleted group is rejected");
        Equal(false, list.MoveToGroup(a, second, hidden), "anchor from another group is rejected");
        Equal(false, list.MoveToGroup(a, null, a), "dropping onto oneself is a no-op");
        Equal(true, list.Orders.SequenceEqual(priorities), "rejected drops preserve task priority");
        list.DeleteGroup(second);
        var emptyTarget = list.CreateGroup(new[] { c, d }, "Target");
        Equal(true, list.MoveToGroup(a, emptyTarget), "header drop works without a visible task anchor");
        Equal(emptyTarget.Id, a.GroupId, "header drop sets membership");
        var lone = new CraftingOrderList(); var only = lone.Add(new RecipeDef());
        lone.CreateGroup(new[] { only }, "Only group");
        Equal(true, lone.MoveToGroup(only, null), "empty Ungrouped accepts a task");
        Equal(null, only.GroupId, "empty Ungrouped drop clears membership");
        Equal(true, lone.Orders.Single() == only, "moving the sole task preserves it");
        var ordered = new CraftingOrderList();
        var x = ordered.Add(new RecipeDef()); var y = ordered.Add(new RecipeDef()); var z = ordered.Add(new RecipeDef());
        var left = ordered.CreateGroup(new[] { x }, "Left"); var middle = ordered.CreateGroup(new[] { y }, "Middle");
        var right = ordered.CreateGroup(new[] { z }, "Right");
        ordered.MoveGroup(null, middle, false);
        ordered.DeleteGroup(left);
        Equal(true, ordered.DisplayGroups().SequenceEqual(new[] { null, middle, right }), "deleting earlier group preserves Ungrouped position");
        ordered.MoveGroup(null, right, false);
        ordered.MoveToGroup(y, right);
        Equal(true, ordered.DisplayGroups().SequenceEqual(new[] { null, right }), "pruning empty earlier group preserves Ungrouped position");
    }

    private static void PriorityOrder()
    {
        var list = new CraftingOrderList();
        var a = list.Add(new RecipeDef()); var b = list.Add(new RecipeDef());
        var c = list.Add(new RecipeDef()); var d = list.Add(new RecipeDef());
        var first = list.CreateGroup(new[] { d, b }, "First");
        Equal(true, list.Orders.SequenceEqual(new[] { b, d, a, c }), "new group precedes source block and retains original member order");
        Equal(null, a.GroupId, "unselected task before the first selected member stays ungrouped");
        Equal(null, c.GroupId, "unselected task between members stays ungrouped");
        var second = list.CreateGroup(new[] { c, a }, "Second");
        Equal(true, list.Orders.SequenceEqual(new[] { b, d, a, c }), "new group occupies its source block's priority position");
        list.MoveGroup(second, first, false);
        Equal(true, list.Orders.SequenceEqual(new[] { a, c, b, d }), "moving a group moves all and only its members");
        list.MoveGroup(null, first, false);
        var added = list.Add(new RecipeDef());
        Equal(true, list.Orders.SequenceEqual(new[] { a, c, added, b, d }), "new task appends to the positioned Ungrouped block");
        Equal(null, added.GroupId, "new task remains ungrouped");
        var beforeToggle = list.Orders.ToArray(); list.GroupingMode = true; list.GroupingMode = false;
        Equal(true, list.Orders.SequenceEqual(beforeToggle), "mode switch preserves the authoritative priority queue");
        second.Collapsed = true; list.RenameGroup(second, "Renamed");
        Equal(true, list.Orders.SequenceEqual(beforeToggle), "folding and renaming preserve priority");
        list.Reorder(list.Orders.ToArray(), 4, 0);
        Equal(second.Id, d.GroupId, "flat drag across a group boundary updates membership");
        Equal(true, list.Orders.SequenceEqual(new[] { d, a, c, added, b }), "flat drag and grouped display retain identical priority");
        Equal(true, list.DisplayGroups().SelectMany(group => list.Orders.Where(order => order.GroupId == group?.Id)).SequenceEqual(list.Orders),
            "flattening grouped display exactly matches the production queue");
        var saved = Snapshot.Save(list).Load();
        Equal(true, saved.Orders.Select(order => order.Id).SequenceEqual(list.Orders.Select(order => order.Id)), "priority survives save and load");
        Equal(true, saved.DisplayGroups().SelectMany(group => saved.Orders.Where(order => order.GroupId == group?.Id)).SequenceEqual(saved.Orders),
            "loaded grouped display still matches the production queue");
        var legacy = Snapshot.Save(list); legacy.Root.Values.Remove("groupingPriorityVersion");
        legacy.Root.Children["orders"].Reverse();
        var migrated = legacy.Load();
        Equal(true, migrated.DisplayGroups().SelectMany(group => migrated.Orders.Where(order => order.GroupId == group?.Id)).SequenceEqual(migrated.Orders),
            "legacy display-only groups migrate into contiguous production blocks");
        Equal(list.Orders.Count, migrated.Orders.Count, "legacy priority migration preserves all tasks");
    }

    private static void NewOrderPlacement()
    {
        Equal(true, new MagicStorageSettings().NewWorkbenchOrdersFirst, "new-order-first setting defaults on");
        var settings = MagicStorageMod.Settings;
        settings.NewWorkbenchOrdersFirst = false;
        var list = new CraftingOrderList();
        var a = list.Add(new RecipeDef()); var b = list.Add(new RecipeDef());
        var c = list.Add(new RecipeDef()); var d = list.Add(new RecipeDef());
        var first = list.CreateGroup(new[] { a }, "First");
        var last = list.CreateGroup(new[] { c }, "Last");
        list.MoveGroup(null, last, false);
        settings.NewWorkbenchOrdersFirst = true;
        var front = list.Add(new RecipeDef());
        Equal(true, list.Orders.SequenceEqual(new[] { a, front, b, d, c }), "enabled setting inserts first inside Ungrouped");
        Equal(null, front.GroupId, "new front task stays ungrouped");
        Equal(true, list.DisplayGroups().SequenceEqual(new[] { first, null, last }), "front insertion preserves relative group priority");
        settings.NewWorkbenchOrdersFirst = false;
        var back = list.Add(new RecipeDef());
        Equal(true, list.Orders.SequenceEqual(new[] { a, front, b, d, back, c }), "disabled setting appends inside Ungrouped");
        Equal(null, back.GroupId, "new last task stays ungrouped");
        var empty = new CraftingOrderList(); var only = empty.Add(new RecipeDef());
        var group = empty.CreateGroup(new[] { only }, "Only");
        empty.MoveGroup(null, group, false); settings.NewWorkbenchOrdersFirst = true;
        var firstUngrouped = empty.Add(new RecipeDef());
        Equal(true, empty.Orders.SequenceEqual(new[] { firstUngrouped, only }), "empty positioned Ungrouped accepts the first task");

        var persisted = new Dictionary<string, object>();
        try
        {
            Scribe_Values.Boundary = (key, value, fallback) => { persisted[key] = value; return value; };
            var source = new MagicStorageSettings { NewWorkbenchOrdersFirst = false }; source.ExposeData();
            Equal(false, (bool)persisted["newWorkbenchOrdersFirst"], "disabled preference is serialized");
            Scribe_Values.Boundary = (key, value, fallback) => persisted.TryGetValue(key, out var stored) ? stored : fallback;
            var loaded = new MagicStorageSettings(); loaded.ExposeData();
            Equal(false, loaded.NewWorkbenchOrdersFirst, "disabled preference restores after settings load");
            persisted.Remove("newWorkbenchOrdersFirst"); loaded.ExposeData();
            Equal(true, loaded.NewWorkbenchOrdersFirst, "old settings without the key default to enabled");
            Scribe_Values.Boundary = null;
            loaded.NewWorkbenchOrdersFirst = false; loaded.RestoreDefaults();
            Equal(true, loaded.NewWorkbenchOrdersFirst, "restore defaults enables front insertion");
        }
        finally { Scribe_Values.Boundary = null; }
    }

    // Scribe boundary records the actual ExposeData calls and reconstructs fresh objects.
    // Native XML encoding and reference resolution remain game boundaries.
    private sealed class Node
    {
        internal readonly Dictionary<string, object> Values = new Dictionary<string, object>();
        internal readonly Dictionary<string, List<Node>> Children = new Dictionary<string, List<Node>>();
        internal RecipeDef Recipe;
    }
    private sealed class Snapshot
    {
        internal readonly Node Root = new Node();
        private Node current;
        private bool reading;
        internal static Snapshot Save(CraftingOrderList list)
        { var snapshot = new Snapshot(); snapshot.Visit(list, snapshot.Root); return snapshot; }
        internal CraftingOrderList Load()
        {
            var list = new CraftingOrderList(); reading = true; Visit(list, Root);
            Scribe.mode = LoadSaveMode.PostLoadInit;
            try { list.ExposeData(); } finally { Scribe.mode = LoadSaveMode.Inactive; }
            return list;
        }
        private void Visit(IExposable target, Node node)
        {
            Node previous = current; current = node;
            Scribe_Values.Boundary = Value; Scribe_Collections.Boundary = Collection;
            try { target.ExposeData(); }
            finally
            {
                current = previous;
                if (previous == null) { Scribe_Values.Boundary = null; Scribe_Collections.Boundary = null; }
            }
        }
        private object Value(string key, object value, object fallback)
        {
            if (reading) return current.Values.TryGetValue(key, out object saved) ? saved : fallback;
            current.Values[key] = value; return value;
        }
        private object Collection(string key, object value, LookMode mode)
        {
            if (mode != LookMode.Deep) return value;
            var list = (IList)Activator.CreateInstance(value.GetType());
            if (reading)
            {
                if (!current.Children.TryGetValue(key, out var children)) return list;
                Type element = value.GetType().GetGenericArguments()[0];
                foreach (var node in children)
                {
                    var item = (IExposable)Activator.CreateInstance(element);
                    if (item is CraftingOrder order) order.Recipe = node.Recipe;
                    Visit(item, node); list.Add(item);
                }
                return list;
            }
            var saved = new List<Node>(); current.Children[key] = saved;
            foreach (IExposable item in (IEnumerable)value)
            {
                var node = new Node { Recipe = (item as CraftingOrder)?.Recipe };
                Visit(item, node); saved.Add(node);
            }
            return value;
        }
    }
}
